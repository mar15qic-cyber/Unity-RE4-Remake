using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Pathfinding;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FilePath = System.IO.Path;

/// <summary>
/// 正式村庄全地图踏面通过性自动检验工具。
/// 基于可行走寻路节点与玩家物理胶囊尺寸执行八方向低矮障碍扫掠，定位卡脚碎石与砖块碰撞体并生成报告。
/// </summary>
public static class VillageFootingAuditTool
{
    private const float PlayerRadius = 0.35f;
    private const float PlayerHeight = 1.75f;
    private const float LowSweepBottomOffset = 0.025f;
    private const float LowSweepTopOffset = 0.26f;
    private const float UpperSweepBottomOffset = 0.34f;
    private const float UpperSweepTopOffset = 1.72f;
    private const float SweepDistance = 0.72f;
    private const float SampleGridSpacing = 0.85f;
    private const string ReportAssetPath = "Docs/验收/全地图踏面通过性检验报告.md";

    private sealed class BlockerStat
    {
        public Collider Collider;
        public string HierarchyPath;
        public string ColliderType;
        public Vector3 BoundsCenter;
        public Vector3 BoundsSize;
        public float MinGroundProtrusion;
        public float MaxGroundProtrusion;
        public int SnagHitCount;
        public Vector3 FirstSamplePoint;
        public bool IsWalkThroughDetail;
        public bool IsRecommendedWalkThrough;
        public string CategoryReason;
    }

    [MenuItem("工具/场景诊断/全地图踏面通过性检验")]
    public static void RunAuditMenu()
    {
        string summary = RunAuditAndWriteReport(false);
        Debug.Log(summary);
    }

    [MenuItem("工具/场景诊断/自动放行低矮碎石砖块碰撞体")]
    public static void ApplySafeWalkThroughMenu()
    {
        string summary = RunAuditAndWriteReport(true);
        Debug.Log(summary);
    }

    public static string RunAuditAndWriteReport(bool applySafeWalkThrough)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            throw new InvalidOperationException("当前没有已加载的活动场景，无法执行踏面通过性检验。");
        }

        LeonActionMotionDriver driver = UnityEngine.Object.FindFirstObjectByType<LeonActionMotionDriver>();
        HashSet<Collider> existingWalkThrough = CollectConfiguredWalkThrough(driver);

        List<Vector3> samplePoints = CollectWalkableSamplePoints(activeScene);
        if (samplePoints.Count == 0)
        {
            throw new InvalidOperationException("未能采样到可行走地面节点，请确认当前场景存在有效的地面碰撞体或 A* 缓存。");
        }

        Dictionary<Collider, BlockerStat> statsByCollider = new Dictionary<Collider, BlockerStat>();
        int totalRays = 0;
        int snagHitEvents = 0;

        Vector3[] directions = BuildEightDirections();
        Collider playerCapsule = null;
        if (driver != null)
        {
            playerCapsule = driver.GetComponent<CapsuleCollider>();
        }

        Physics.SyncTransforms();

        for (int p = 0; p < samplePoints.Count; p++)
        {
            Vector3 groundPoint = samplePoints[p];
            for (int d = 0; d < directions.Length; d++)
            {
                totalRays++;
                Vector3 dir = directions[d];

                // 跳过上半身本就被墙壁、门框、围栏或低梁挡住的方向，只统计“上方畅通但底部卡脚”的阻塞。
                if (HasUpperBodyObstruction(groundPoint, dir, playerCapsule))
                {
                    continue;
                }

                if (TryFindLowFootBlocker(groundPoint, dir, playerCapsule, existingWalkThrough, out RaycastHit hit))
                {
                    snagHitEvents++;
                    Collider blocker = hit.collider;
                    BlockerStat stat;
                    if (!statsByCollider.TryGetValue(blocker, out stat))
                    {
                        stat = CreateBlockerStat(blocker, existingWalkThrough.Contains(blocker));
                        stat.FirstSamplePoint = groundPoint;
                        statsByCollider[blocker] = stat;
                    }

                    stat.SnagHitCount++;
                    float topProtrusion = blocker.bounds.max.y - groundPoint.y;
                    if (topProtrusion < stat.MinGroundProtrusion)
                    {
                        stat.MinGroundProtrusion = topProtrusion;
                    }
                    if (topProtrusion > stat.MaxGroundProtrusion)
                    {
                        stat.MaxGroundProtrusion = topProtrusion;
                    }
                }
            }
        }

        List<BlockerStat> sorted = new List<BlockerStat>(statsByCollider.Values);
        sorted.Sort(CompareBlockerStat);

        int addedWalkThroughCount = 0;
        if (applySafeWalkThrough && driver != null)
        {
            addedWalkThroughCount = ApplyRecommendedWalkThroughDetails(driver, sorted, existingWalkThrough);
            if (addedWalkThroughCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                // 放行后立刻复查一次真实剩余阻断点。
                return RunAuditAndWriteReportInternal(activeScene, driver, samplePoints.Count, totalRays, addedWalkThroughCount);
            }
        }

        WriteMarkdownReport(activeScene, samplePoints.Count, totalRays, snagHitEvents, sorted, addedWalkThroughCount);

        int remainingUncovered = 0;
        int recommendedCount = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            if (!sorted[i].IsWalkThroughDetail)
            {
                remainingUncovered++;
                if (sorted[i].IsRecommendedWalkThrough)
                {
                    recommendedCount++;
                }
            }
        }

        return string.Format(
            "全地图踏面通过性检验完成：采样点 {0}，八向测试 {1} 次，卡脚碰撞体 {2} 个（其中可安全放行碎石/砖块 {3} 个），报告已写入 {4}",
            samplePoints.Count,
            totalRays,
            remainingUncovered,
            recommendedCount,
            ReportAssetPath);
    }

    private static string RunAuditAndWriteReportInternal(
        Scene activeScene,
        LeonActionMotionDriver driver,
        int previousSampleCount,
        int previousRays,
        int addedWalkThroughCount)
    {
        HashSet<Collider> updatedWalkThrough = CollectConfiguredWalkThrough(driver);
        List<Vector3> samplePoints = CollectWalkableSamplePoints(activeScene);
        Dictionary<Collider, BlockerStat> statsByCollider = new Dictionary<Collider, BlockerStat>();
        Vector3[] directions = BuildEightDirections();
        Collider playerCapsule = driver.GetComponent<CapsuleCollider>();
        int snagHitEvents = 0;

        Physics.SyncTransforms();

        for (int p = 0; p < samplePoints.Count; p++)
        {
            Vector3 groundPoint = samplePoints[p];
            for (int d = 0; d < directions.Length; d++)
            {
                Vector3 dir = directions[d];
                if (HasUpperBodyObstruction(groundPoint, dir, playerCapsule))
                {
                    continue;
                }

                if (TryFindLowFootBlocker(groundPoint, dir, playerCapsule, updatedWalkThrough, out RaycastHit hit))
                {
                    snagHitEvents++;
                    Collider blocker = hit.collider;
                    BlockerStat stat;
                    if (!statsByCollider.TryGetValue(blocker, out stat))
                    {
                        stat = CreateBlockerStat(blocker, updatedWalkThrough.Contains(blocker));
                        stat.FirstSamplePoint = groundPoint;
                        statsByCollider[blocker] = stat;
                    }

                    stat.SnagHitCount++;
                    float topProtrusion = blocker.bounds.max.y - groundPoint.y;
                    if (topProtrusion < stat.MinGroundProtrusion)
                    {
                        stat.MinGroundProtrusion = topProtrusion;
                    }
                    if (topProtrusion > stat.MaxGroundProtrusion)
                    {
                        stat.MaxGroundProtrusion = topProtrusion;
                    }
                }
            }
        }

        List<BlockerStat> sorted = new List<BlockerStat>(statsByCollider.Values);
        sorted.Sort(CompareBlockerStat);
        WriteMarkdownReport(activeScene, samplePoints.Count, samplePoints.Count * 8, snagHitEvents, sorted, addedWalkThroughCount);

        return string.Format(
            "全地图踏面通过性检验与自动放行完成：新增放行 {0} 个低矮碎石/砖块碰撞体；复查采样点 {1}，剩余卡脚碰撞体 {2} 个（命中 {3} 次），报告已更新至 {4}",
            addedWalkThroughCount,
            samplePoints.Count,
            sorted.Count,
            snagHitEvents,
            ReportAssetPath);
    }

    private static int CompareBlockerStat(BlockerStat a, BlockerStat b)
    {
        return b.SnagHitCount.CompareTo(a.SnagHitCount);
    }

    private static Vector3[] BuildEightDirections()
    {
        float diag = Mathf.Sqrt(0.5f);
        return new[]
        {
            new Vector3(0f, 0f, 1f),
            new Vector3(diag, 0f, diag),
            new Vector3(1f, 0f, 0f),
            new Vector3(diag, 0f, -diag),
            new Vector3(0f, 0f, -1f),
            new Vector3(-diag, 0f, -diag),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-diag, 0f, diag)
        };
    }

    private static HashSet<Collider> CollectConfiguredWalkThrough(LeonActionMotionDriver driver)
    {
        HashSet<Collider> set = new HashSet<Collider>();
        if (driver != null)
        {
            SerializedObject so = new SerializedObject(driver);
            SerializedProperty arr = so.FindProperty("walkThroughDetails");
            if (arr != null && arr.isArray)
            {
                for (int i = 0; i < arr.arraySize; i++)
                {
                    Collider c = arr.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                    if (c != null)
                    {
                        set.Add(c);
                    }
                }
            }
        }

        // 与 LeonActionMotionDriver.IsWalkThroughDecorationMesh 保持一致的运行时放行规则。
        MeshCollider[] meshColliders = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        for (int i = 0; i < meshColliders.Length; i++)
        {
            MeshCollider mc = meshColliders[i];
            if (IsRuntimeDecorationMesh(mc))
            {
                set.Add(mc);
            }
        }

        return set;
    }

    private static bool IsRuntimeDecorationMesh(MeshCollider obstacle)
    {
        if (obstacle == null || !obstacle.enabled || obstacle.isTrigger)
        {
            return false;
        }

        string objectName = obstacle.name;
        if (objectName == "sm13_521_01" && obstacle.sharedMesh != null && obstacle.sharedMesh.name == "6defebdab5550d57f8b7")
        {
            return true;
        }

        if (objectName == "sm13_502_01" || objectName == "sm13_502_04"
            || objectName == "sm11_512_01" || objectName == "sm11_513_00"
            || objectName.StartsWith("sm13_518_", StringComparison.Ordinal))
        {
            return true;
        }

        if ((objectName == "sm25_518_00" || objectName == "sm25_534_00" || objectName == "sm25_534_01"
            || objectName == "sm55_550_00" || objectName == "sm55_550_01" || objectName == "sm53_510_00"
            || objectName == "sm53_512_00" || objectName == "sm33_520_01")
            && obstacle.bounds.size.y <= 0.25f)
        {
            return true;
        }

        Transform parent = obstacle.transform.parent;
        if (parent != null && parent.name == "Grass")
        {
            return true;
        }

        return false;
    }

    private static List<Vector3> CollectWalkableSamplePoints(Scene scene)
    {
        Dictionary<Vector3Int, Vector3> deduplicated = new Dictionary<Vector3Int, Vector3>();
        AstarPath astar = UnityEngine.Object.FindFirstObjectByType<AstarPath>();
        if (astar != null)
        {
            bool loadedFromCache = false;
            int beforeCount = CountGraphNodes(astar);
            if (beforeCount == 0 && astar.data != null && astar.data.file_cachedStartup != null)
            {
                astar.data.DeserializeGraphs(astar.data.file_cachedStartup.bytes);
                loadedFromCache = true;
            }

            if (astar.data != null && astar.data.graphs != null)
            {
                for (int g = 0; g < astar.data.graphs.Length; g++)
                {
                    NavGraph graph = astar.data.graphs[g];
                    if (graph is RecastGraph recast)
                    {
                        recast.GetNodes(node =>
                        {
                            if (!node.Walkable)
                            {
                                return;
                            }

                            Vector3 pos = (Vector3)node.position;
                            Vector3Int key = new Vector3Int(
                                Mathf.RoundToInt(pos.x / SampleGridSpacing),
                                Mathf.RoundToInt(pos.y / 0.6f),
                                Mathf.RoundToInt(pos.z / SampleGridSpacing));
                            if (!deduplicated.ContainsKey(key))
                            {
                                // 将节点位置吸附到真实物理地面。
                                if (TrySnapToGround(pos, out Vector3 snapped))
                                {
                                    deduplicated[key] = snapped;
                                }
                            }
                        });
                    }
                }
            }

            if (loadedFromCache)
            {
                // 不保留多余编辑器状态修改；缓存节点只读使用。
            }
        }

        // 补充围绕水井石槽及广场关键区域的加密网格采样（确保 3 分 42 秒附近全覆盖）。
        for (float x = -16f; x <= 16f; x += 0.75f)
        {
            for (float z = -16f; z <= 16f; z += 0.75f)
            {
                Vector3 probe = new Vector3(x, 2.5f, z);
                if (TrySnapToGround(probe, out Vector3 snapped))
                {
                    Vector3Int key = new Vector3Int(
                        Mathf.RoundToInt(snapped.x / 0.75f),
                        Mathf.RoundToInt(snapped.y / 0.6f),
                        Mathf.RoundToInt(snapped.z / 0.75f));
                    if (!deduplicated.ContainsKey(key))
                    {
                        deduplicated[key] = snapped;
                    }
                }
            }
        }

        return new List<Vector3>(deduplicated.Values);
    }

    private static int CountGraphNodes(AstarPath astar)
    {
        if (astar == null || astar.data == null || astar.data.graphs == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < astar.data.graphs.Length; i++)
        {
            NavGraph g = astar.data.graphs[i];
            if (g != null)
            {
                total += g.CountNodes();
            }
        }

        return total;
    }

    private static bool TrySnapToGround(Vector3 seedPos, out Vector3 groundPos)
    {
        Vector3 origin = new Vector3(seedPos.x, seedPos.y + 1.2f, seedPos.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 2.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.PositiveInfinity;
        groundPos = Vector3.zero;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.attachedRigidbody != null)
            {
                continue;
            }
            if (hit.normal.y < 0.72f)
            {
                continue;
            }
            if (IsIgnoredDynamicOrActorCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                groundPos = hit.point;
                found = true;
            }
        }

        return found;
    }

    private static bool HasUpperBodyObstruction(Vector3 groundPoint, Vector3 dir, Collider playerCapsule)
    {
        Vector3 bottom = groundPoint + Vector3.up * (UpperSweepBottomOffset + PlayerRadius);
        Vector3 top = groundPoint + Vector3.up * (UpperSweepTopOffset - PlayerRadius);
        if (top.y < bottom.y)
        {
            top.y = bottom.y;
        }

        RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, PlayerRadius * 0.92f, dir, SweepDistance + 0.12f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null || col == playerCapsule || col.attachedRigidbody != null)
            {
                continue;
            }
            if (IsIgnoredDynamicOrActorCollider(col))
            {
                continue;
            }
            return true;
        }

        return false;
    }

    private static bool TryFindLowFootBlocker(
        Vector3 groundPoint,
        Vector3 dir,
        Collider playerCapsule,
        HashSet<Collider> walkThroughSet,
        out RaycastHit bestHit)
    {
        bestHit = default;
        float closest = float.PositiveInfinity;

        Vector3 lowP1 = groundPoint + Vector3.up * (LowSweepBottomOffset + 0.08f);
        Vector3 lowP2 = groundPoint + Vector3.up * LowSweepTopOffset;
        RaycastHit[] hits = Physics.CapsuleCastAll(lowP1, lowP2, PlayerRadius * 0.94f, dir, SweepDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider col = hit.collider;
            if (col == null || col == playerCapsule || col.attachedRigidbody != null || col.isTrigger)
            {
                continue;
            }
            if (walkThroughSet.Contains(col) || IsIgnoredDynamicOrActorCollider(col))
            {
                continue;
            }
            // 斜坡或正常平坦地面本身不计作阻塞。
            if (hit.normal.y >= 0.65f || Vector3.Dot(hit.normal, dir) > -0.25f)
            {
                continue;
            }

            float protrusion = col.bounds.max.y - groundPoint.y;
            if (protrusion < 0.015f || protrusion > 0.36f)
            {
                continue;
            }

            // 排除大面积整块墙体/地形，只检真正卡脚的局部台阶/砖块/碎石/杂物边缘。
            if (col.bounds.size.y > 0.65f)
            {
                continue;
            }

            if (hit.distance < closest)
            {
                closest = hit.distance;
                bestHit = hit;
            }
        }

        return !float.IsInfinity(closest);
    }

    private static bool IsIgnoredDynamicOrActorCollider(Collider col)
    {
        if (col.GetComponentInParent<LeonController>() != null)
        {
            return true;
        }
        if (col.GetComponentInParent<VillagerController>() != null)
        {
            return true;
        }
        if (col.GetComponentInParent<DoorController>() != null)
        {
            return true;
        }
        if (col.GetComponentInParent<WindowTraversalPoint>() != null)
        {
            return true;
        }
        if (col.GetComponentInParent<ItemPickup>() != null)
        {
            return true;
        }

        int layer = col.gameObject.layer;
        if (layer == LayerMask.NameToLayer("Player") || layer == LayerMask.NameToLayer("Enemy")
            || layer == LayerMask.NameToLayer("EnemyHitbox"))
        {
            return true;
        }

        return false;
    }

    private static BlockerStat CreateBlockerStat(Collider col, bool isWalkThrough)
    {
        string path = GetHierarchyPath(col.transform);
        Bounds b = col.bounds;
        bool recommended = EvaluateWalkThroughRecommendation(col, path, b, out string reason);
        return new BlockerStat
        {
            Collider = col,
            HierarchyPath = path,
            ColliderType = col.GetType().Name,
            BoundsCenter = b.center,
            BoundsSize = b.size,
            MinGroundProtrusion = float.PositiveInfinity,
            MaxGroundProtrusion = float.NegativeInfinity,
            SnagHitCount = 0,
            IsWalkThroughDetail = isWalkThrough,
            IsRecommendedWalkThrough = recommended,
            CategoryReason = reason
        };
    }

    private static bool EvaluateWalkThroughRecommendation(Collider col, string path, Bounds b, out string reason)
    {
        // 合法可交互箱子、木桶、捕兽夹、机关与门栅必须保留实体阻挡。
        if (col.GetComponentInParent<DestructibleWoodenBox>() != null
            || col.GetComponentInParent<VillageLegTrap>() != null
            || col.GetComponentInParent<VillageEntranceGate>() != null
            || col.GetComponentInParent<WindowBarricade>() != null
            || path.IndexOf("gm84_", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("gm82_", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("gm80_", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("gm03_", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            reason = "可破坏木箱/捕兽夹/门栅/动态机关（保留阻挡）";
            return false;
        }

        if (path.IndexOf("Door03", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Stair", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Ladder", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("Tower", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("踏面", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            reason = "楼梯/塔楼/踏面结构（交由抬脚机制跨越，保留实体）";
            return false;
        }

        // 体积扁平且属于地表杂物/铺地砖片/碎石袋的碰撞体，适合直接纳入 walkThroughDetails。
        bool flatDebris = b.size.y <= 0.30f && b.size.x <= 4.5f && b.size.z <= 4.5f;
        bool isGroundDebrisAsset = path.IndexOf("/sm23_530_00/", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("/sm59_575_00/", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("/Puddle/", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("/Props/props/", StringComparison.OrdinalIgnoreCase) >= 0 && b.size.y <= 0.24f && b.size.x <= 2.2f && b.size.z <= 2.2f
            || col.name == "Collision Capsule" && b.size.y <= 0.26f;

        if (flatDebris && isGroundDebrisAsset && path.IndexOf("/Terrain/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            reason = "低矮地面碎石/砖块/瓦砾/水洼装饰体（推荐放行）";
            return true;
        }

        if (b.size.y <= 0.14f && b.size.x <= 2.5f && b.size.z <= 2.5f
            && path.IndexOf("/Terrain/", StringComparison.OrdinalIgnoreCase) < 0)
        {
            reason = "极薄地面凸起饰物（推荐放行）";
            return true;
        }

        if (path.IndexOf("/Terrain/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            reason = "地形边缘/路缘石（由通用抬脚跨越）";
            return false;
        }

        reason = "常规低矮阻挡（由通用抬脚跨越或人工核查）";
        return false;
    }

    private static int ApplyRecommendedWalkThroughDetails(
        LeonActionMotionDriver driver,
        List<BlockerStat> stats,
        HashSet<Collider> existingWalkThrough)
    {
        SerializedObject so = new SerializedObject(driver);
        SerializedProperty arr = so.FindProperty("walkThroughDetails");
        if (arr == null || !arr.isArray)
        {
            return 0;
        }

        List<Collider> merged = new List<Collider>();
        HashSet<Collider> unique = new HashSet<Collider>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            Collider c = arr.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
            if (c != null && unique.Add(c))
            {
                merged.Add(c);
            }
        }

        int added = 0;
        for (int i = 0; i < stats.Count; i++)
        {
            BlockerStat s = stats[i];
            if (s.Collider != null && s.IsRecommendedWalkThrough && !existingWalkThrough.Contains(s.Collider) && unique.Add(s.Collider))
            {
                merged.Add(s.Collider);
                added++;
            }
        }

        if (added > 0)
        {
            arr.arraySize = merged.Count;
            for (int i = 0; i < merged.Count; i++)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = merged[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
        }

        return added;
    }

    private static void WriteMarkdownReport(
        Scene scene,
        int sampleCount,
        int totalRays,
        int snagHitEvents,
        List<BlockerStat> stats,
        int addedWalkThroughCount)
    {
        string fullPath = FilePath.GetFullPath(ReportAssetPath);
        string dir = FilePath.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# 全地图踏面通过性检验报告");
        sb.AppendLine();
        sb.AppendLine("- 检验场景: `" + scene.path + "`");
        sb.AppendLine("- 检验时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("- 玩家物理胶囊基准: 半径 `0.35m`，身高 `1.75m`");
        sb.AppendLine("- 地面采样点数: `" + sampleCount + "`，八向扫掠次数: `" + totalRays + "`");
        sb.AppendLine("- 检出卡脚碰撞体数: `" + stats.Count + "`，合计卡脚命中次数: `" + snagHitEvents + "`");
        if (addedWalkThroughCount > 0)
        {
            sb.AppendLine("- 本次新增纳入 `walkThroughDetails` 的纯装饰碎石/砖块碰撞体: `" + addedWalkThroughCount + "`");
        }
        sb.AppendLine();
        sb.AppendLine("## 阻塞碰撞体明细（按命中次数降序）");
        sb.AppendLine();
        sb.AppendLine("| 命中次数 | 碰撞体类型 | 坐标 (X, Y, Z) | 包围盒尺寸 (X, Y, Z) | 突出地面高度 | 类别评估 | 路径 |");
        sb.AppendLine("|---:|---|---|---|---|---|---|");

        int limit = Mathf.Min(stats.Count, 120);
        for (int i = 0; i < limit; i++)
        {
            BlockerStat s = stats[i];
            sb.AppendLine(string.Format(
                "| {0} | `{1}` | `({2:F2}, {3:F2}, {4:F2})` | `({5:F2}, {6:F2}, {7:F2})` | `{8:F2}m ~ {9:F2}m` | {10} | `{11}` |",
                s.SnagHitCount,
                s.ColliderType,
                s.BoundsCenter.x, s.BoundsCenter.y, s.BoundsCenter.z,
                s.BoundsSize.x, s.BoundsSize.y, s.BoundsSize.z,
                s.MinGroundProtrusion, s.MaxGroundProtrusion,
                s.CategoryReason,
                s.HierarchyPath));
        }

        File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static string GetHierarchyPath(Transform t)
    {
        if (t == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
