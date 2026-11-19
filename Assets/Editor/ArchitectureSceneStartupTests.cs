using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构场景启动测试")]

public sealed class ArchitectureSceneStartupTests
{
    private Scene scene;
    private bool selfRowLoad;
    private SceneStartupEntry entryPoint;

    [OneTimeSetUp]
    public void LoadScene()
    {
        scene = SceneManager.GetSceneByPath(SceneManagerEx.MainScenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(SceneManagerEx.MainScenePath, OpenSceneMode.Additive);
            selfRowLoad = true;
        }
        var all = new System.Collections.Generic.List<SceneStartupEntry>();
        foreach (var root in scene.GetRootGameObjects()) all.AddRange(root.GetComponentsInChildren<SceneStartupEntry>(true));
        Assert.AreEqual(1, all.Count);
        entryPoint = all[0];
    }

    [OneTimeTearDown]
    public void CloseSceneValue()
    {
        if (selfRowLoad && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    [Test]
    public void ProductionAssetReloadBackCanPassStartupFrontValidate()
    {
        Assert.GreaterOrEqual(SceneUtility.GetBuildIndexByScenePath(SceneManagerEx.MainScenePath), 0);
        Assert.AreEqual(-1, SceneUtility.GetBuildIndexByScenePath("Assets/GameScene.unity"));
        Assert.IsNull(entryPoint.ConfigurationError());
        Assert.IsTrue(AssetDatabase.Contains(entryPoint.mission.waveConfiguration));
        Assert.AreEqual(5, entryPoint.playerStair.rampSources.Length);
    }

    [Test]
    public void ProductionWindowsKeepTheirOuterPlatformBoundary()
    {
        var navigation = entryPoint.regionNavigation;
        int boundaryCount = 0;
        int windowCount = 0;
        Physics.SyncTransforms();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var volume in root.GetComponentsInChildren<EnemyNavigationElevatedWalkableVolume>())
            {
                if (!volume.enabled || !volume.useBoundaryPolygon || volume.regionId != navigation.outerPlatformIdentifier)
                {
                    continue;
                }

                Assert.IsNotNull(volume.boundaryPoints, volume.name);
                Assert.GreaterOrEqual(volume.boundaryPoints.Length, 3, volume.name);
                foreach (var point in volume.boundaryPoints)
                {
                    Assert.IsTrue(point != null && point.gameObject.scene == scene, volume.name);
                }
                boundaryCount++;
            }

            foreach (var route in root.GetComponentsInChildren<EnemyWindowTraversalRoute>())
            {
                if (!route.enabled || route.platformRegionId != navigation.outerPlatformIdentifier)
                {
                    continue;
                }

                Assert.IsTrue(route.TryAcquireWindowSideTread(true, out var outerTread), route.name);
                Assert.AreEqual(VillageTraversalRegion.SecondFloorOuterPlatform, navigation.TreadRegion(outerTread), route.name);
                windowCount++;
            }
        }

        Assert.Greater(boundaryCount, 0);
        Assert.Greater(windowCount, 0);
    }

    [Test]
    public void ConfigurationErrorReportFieldAndPreserveInputValue()
    {
        var configuration = Object.Instantiate(entryPoint.mission.waveConfiguration);
        try
        {
            var serialize = new SerializedObject(configuration);
            serialize.FindProperty("nextWaveRemainingRatio").floatValue = 2f;
            serialize.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(configuration.Validate(out string field));
            Assert.AreEqual("nextWaveRemainingRatio", field);
            serialize.Update();
            Assert.AreEqual(2f, serialize.FindProperty(field).floatValue);
        }
        finally { Object.DestroyImmediate(configuration); }
    }

    [Test]
    public void EnemyGroupPlayerLostTimeReportSpecificBinding()
    {
        var player = entryPoint.enemyGroup.scenePlayer;
        try
        {
            entryPoint.enemyGroup.scenePlayer = null;
            Assert.AreEqual("敌群.场景玩家", entryPoint.ConfigurationError());
        }
        finally { entryPoint.enemyGroup.scenePlayer = player; }
    }
}

