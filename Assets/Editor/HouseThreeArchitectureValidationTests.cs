using NUnit.Framework;
using Pathfinding;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构三号房校验测试")]

public sealed class HouseThreeArchitectureValidationTests
{
    private HouseThreeTraversalGeometry route;
    private UnityEngine.SceneManagement.Scene testScene;
    private bool byTestLoad;
    [OneTimeSetUp] public void LoadProductionAsset()
    {
        testScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(SceneManagerEx.MainScenePath);
        if (!testScene.IsValid() || !testScene.isLoaded)
        {
            testScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SceneManagerEx.MainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            byTestLoad = true;
        }
    }
    [OneTimeTearDown] public void UnloadTestAsset()
    {
        if (byTestLoad && testScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(testScene, true);
    }
    [SetUp] public void Prepare()
    {
        // 编辑模式不会自动载入运行时缓存，测试需显式准备同一份正式导航数据。
        Assert.IsNotNull(AstarPath.active);
        if (AstarPath.active.data.cacheStartup && AstarPath.active.data.file_cachedStartup != null)
        {
            bool hasNode = false;
            AstarPath.active.data.GetNodes(node => { if (node.Walkable) hasNode = true; });
            if (!hasNode) AstarPath.active.data.LoadFromCache();
        }
        route = HouseThreeTraversalGeometry.Active;
        Assert.IsNotNull(route, "请在正式主场景执行路线集成测试");
        Assert.IsTrue(route.IsReady);
    }
    [Test] public void ValidDoorControlHandoffAndStairImageConnectedWillNotBeingMisclassify()
    {
        Assert.IsTrue(VillageNavigationBaker.ValidatePublished(testScene, out string report), report);
    }
    [Test] public void MissingNativeDoorThresholdFailsProductionValidation()
    {
        var threshold = route.nativeDoorThreshold;
        try
        {
            route.nativeDoorThreshold = null;
            Assert.IsFalse(VillageNavigationBaker.ValidatePublished(testScene, out _));
        }
        finally { route.nativeDoorThreshold = threshold; }
    }
    [Test] public void StairStartEndPointDirectConnectionDetourMustBeingRejectAndRestoreBackPass()
    {
        var start = AstarPath.active.GetNearest(route.LowerEntry, NearestNodeConstraint.Walkable).node;
        var end = AstarPath.active.GetNearest(route.UpperPlatformRelease, NearestNodeConstraint.Walkable).node;
        Assert.IsFalse(start.ContainsOutgoingConnection(end));
        Assert.IsFalse(end.ContainsOutgoingConnection(start));
        try
        {
            GraphNode.Connect(start, end, 1);
            Assert.IsFalse(VillageNavigationBaker.ValidatePublished(testScene, out _));
        }
        finally { GraphNode.Disconnect(start, end); }
        Assert.IsTrue(VillageNavigationBaker.ValidatePublished(testScene, out string report), report);
    }
    [Test] public void NotYetReadyRouteCannotPassRuntimeContract()
    {
        var door = route.entranceDoor;
        try { route.entranceDoor = null; Assert.IsFalse(VillageNavigationBaker.ValidatePublished(testScene, out _)); }
        finally { route.entranceDoor = door; }
    }
}
