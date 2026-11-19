using System;
using System.IO;
using System.Linq;
using Pathfinding;
using Pathfinding.Serialization;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using FilePath = System.IO.Path;

internal static class VillageNavigationBaker
{
    [MenuItem("Tools/Enemy/生成正式村庄导航候选图")]
    public static void BakeFromMenu()
    {
        string path = EditorUtility.SaveFilePanel("保存村庄导航候选图", "Library", "VillageNavigationCandidate", "bytes");
        if (!string.IsNullOrEmpty(path))
        {
            Debug.Log(BakeCandidate(path));
        }
    }

    public static string BakeCandidate(string outputPath)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != SceneManagerEx.MainScenePath || scene.isDirty || SceneManager.sceneCount != 1)
        {
            throw new InvalidOperationException("请单独打开并保存正式村庄，再生成导航候选图。");
        }

        AstarPath astar = Components<AstarPath>(scene).Single();
        var region = Components<HouseThreeRegionNavigation>(scene).Single();
        string productionPath = FilePath.GetFullPath(AssetDatabase.GetAssetPath(region.productionCache));
        if (string.Equals(FilePath.GetFullPath(outputPath), productionPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("候选图请另存，验收后再发布到正式缓存。");
        }

        Collider[] doors = Components<DoorController>(scene)
            .Select(door => new SerializedObject(door).FindProperty("physicalCollider").objectReferenceValue as Collider)
            .Where(collider => collider != null && collider.enabled).Distinct().ToArray();
        NodeLink2[] links = Components<NodeLink2>(scene).Where(link => link.enabled).ToArray();
        AstarPath previousActive = AstarPath.active;
        byte[] previousData = astar.data.GetData();
        bool previousCacheStartup = astar.data.cacheStartup;
        byte[] candidate;
        string report;

        try
        {
            AstarPath.active = astar;
            astar.data.cacheStartup = false;
            astar.data.DeserializeGraphs();
            RecastGraph graph = astar.data.recastGraph;
            foreach (Collider door in doors)
            {
                door.enabled = false;
            }

            foreach (NodeLink2 link in links)
            {
                link.enabled = false;
            }

            Physics.SyncTransforms();
            astar.Scan(graph);
            if (!ValidateGraph(scene, astar, region, out report))
            {
                throw new InvalidOperationException(report);
            }

            candidate = astar.data.SerializeGraphs(new SerializeSettings { nodes = true });
        }
        finally
        {
            foreach (Collider door in doors)
            {
                door.enabled = true;
            }

            foreach (NodeLink2 link in links)
            {
                link.enabled = true;
            }

            try
            {
                astar.data.SetData(previousData);
                astar.data.cacheStartup = previousCacheStartup;
                RestoreGraph(astar);
                Physics.SyncTransforms();
            }
            finally
            {
                AstarPath.active = previousActive;
            }
        }

        File.WriteAllBytes(outputPath, candidate);
        return report;
    }

    public static bool ValidatePublished(Scene scene, out string report)
    {
        var astar = Components<AstarPath>(scene).SingleOrDefault();
        var region = Components<HouseThreeRegionNavigation>(scene).SingleOrDefault();
        if (astar == null || region == null || region.productionCache == null ||
            astar.data.file_cachedStartup != region.productionCache)
        {
            report = "正式场景的 A* 缓存引用不一致。";
            return false;
        }

        AstarPath previous = AstarPath.active;
        try
        {
            AstarPath.active = astar;
            if (CountWalkable(astar) == 0)
            {
                astar.data.LoadFromCache();
            }

            return ValidateGraph(scene, astar, region, out report);
        }
        finally
        {
            AstarPath.active = previous;
        }
    }

    private static bool ValidateGraph(Scene scene, AstarPath astar, HouseThreeRegionNavigation region, out string report)
    {
        var house = region.traversal;
        if (house == null || !house.IsReady || !house.HasEntranceSupport || region.ConfigurationError() != null)
        {
            report = "三号屋的门槛、楼层或窗口路线配置失效。";
            return false;
        }

        var mission = Components<ChapterOneMissionManager>(scene).Single();
        foreach (Transform spawn in mission.spawnPoints)
        {
            if (spawn == null || !HasNearbyNode(astar, spawn.position))
            {
                report = $"任务出生点缺少可走节点 {spawn}";
                return false;
            }
        }

        var windows = Components<EnemyWindowTraversalRoute>(scene);
        foreach (var window in windows)
        {
            if (!window.TryAcquireWindowSideTread(false, out Vector3 inside) || !HasNearbyNode(astar, inside) ||
                !window.TryAcquireWindowSideTread(true, out Vector3 outside) || !HasNearbyNode(astar, outside))
            {
                report = $"窗口两侧缺少踏面或可走节点 {window.name}";
                return false;
            }
        }

        var drops = Components<EnemyPlatformDropRoute>(scene);
        foreach (var drop in drops)
        {
            if (!HasNearbyNode(astar, drop.transform.position))
            {
                report = $"平台跳落起点缺少可走节点 {drop.name}";
                return false;
            }
        }

        GraphNode lower = astar.GetNearest(house.LowerEntry, NearestNodeConstraint.Walkable).node;
        GraphNode upper = astar.GetNearest(house.UpperPlatformRelease, NearestNodeConstraint.Walkable).node;
        if (lower == null || upper == null || lower == upper ||
            lower.ContainsOutgoingConnection(upper) || upper.ContainsOutgoingConnection(lower))
        {
            report = "三号屋楼梯节点缺失或存在跨层直连。";
            return false;
        }

        ABPath path = ABPath.Construct(house.ExteriorGate, house.UpperPlatformRelease);
        AstarPath.StartPath(path);
        path.BlockUntilCalculated();
        if (path.error || !path.path.Any(node => region.TreadRegion((Vector3)node.position) == VillageTraversalRegion.Stair))
        {
            report = "三号屋从门外到二楼的路线必须经过楼梯。" + path.errorLog;
            return false;
        }

        report = $"可走节点 {CountWalkable(astar)}，出生点 {mission.spawnPoints.Length}，窗口 {windows.Length}，跳落起点 {drops.Length}，上楼路径 {path.path.Count} 个节点。";
        return true;
    }

    private static bool HasNearbyNode(AstarPath astar, Vector3 position)
    {
        NNInfo nearest = astar.GetNearest(position, NearestNodeConstraint.Walkable);
        return nearest.node != null && (position - nearest.position).sqrMagnitude <= .6f * .6f;
    }

    private static int CountWalkable(AstarPath astar)
    {
        int count = 0;
        astar.data.GetNodes(node =>
        {
            if (node.Walkable)
            {
                count++;
            }
        });
        return count;
    }

    private static void RestoreGraph(AstarPath astar)
    {
        if (astar.data.cacheStartup && astar.data.file_cachedStartup != null)
        {
            astar.data.LoadFromCache();
        }
        else
        {
            astar.data.DeserializeGraphs();
        }
    }

    private static T[] Components<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
