using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构迁移测试")]

public sealed class ArchitectureMigrationTests
{
    private string assetPath;
    private string recordPath;

    [SetUp]
    public void CreateIndependentValidateAsset()
    {
        string id = Guid.NewGuid().ToString("N");
        assetPath = "Assets/Editor/架构迁移验证" + id + ".prefab";
        recordPath = "Docs/架构改造/迁移记录/自动验证/" + id + ".json";
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("架构迁移验证对象");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<VillageEntranceCombatTrigger>();
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TearDown]
    public void CleanupCurrentValidateAsset()
    {
        if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath);
    }

    private ArchitectureAssetMigrationTool.MigrationPlan Plan() => new ArchitectureAssetMigrationTool.MigrationPlan
    {
        assetPath = assetPath, hostType = typeof(VillageEntranceCombatTrigger).FullName,
        newComponentType = typeof(EnemyAIManager).FullName, referenceField = "manager",
        newComponentConfiguration = "{\"maxActiveDistance\":27.5}"
    };

    [Test]
    public void PreviewNotChangeAssetAndDuplicateApplyNotNewlyAddedDifference()
    {
        string originalSummary = ArchitectureAssetMigrationTool.CalculateSummary(assetPath);
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        Assert.AreEqual(1, record.objectValue.Count);
        Assert.AreEqual(originalSummary, ArchitectureAssetMigrationTool.CalculateSummary(assetPath));
        ArchitectureAssetMigrationTool.Apply(record, recordPath);
        ArchitectureAssetMigrationTool.Validate(record);
        string newSummary = ArchitectureAssetMigrationTool.CalculateSummary(assetPath);
        ArchitectureAssetMigrationTool.Apply(record, recordPath);
        Assert.AreEqual(newSummary, ArchitectureAssetMigrationTool.CalculateSummary(assetPath));
        Assert.AreEqual(0, ArchitectureAssetMigrationTool.Preview(Plan()).objectValue.Count);
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            Assert.AreEqual(1, root.GetComponents<EnemyAIManager>().Length);
            Assert.AreSame(root.GetComponent<EnemyAIManager>(), root.GetComponent<VillageEntranceCombatTrigger>().manager);
            Assert.AreEqual(27.5f, root.GetComponent<EnemyAIManager>().maxActiveDistance);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void FallbackOnlyRemoveCurrentNewlyAddedComponentAndBinding()
    {
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        ArchitectureAssetMigrationTool.Apply(record, recordPath);
        // 从磁盘重新读取记录，验证回退不依赖原来的组件实例编号。
        record = JsonUtility.FromJson<ArchitectureAssetMigrationTool.MigrationRecord>(File.ReadAllText(recordPath));
        ArchitectureAssetMigrationTool.Fallback(record, recordPath);
        ArchitectureAssetMigrationTool.Fallback(record, recordPath);
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            Assert.IsNull(root.GetComponent<EnemyAIManager>());
            Assert.IsNotNull(root.GetComponent<VillageEntranceCombatTrigger>());
            Assert.IsNull(root.GetComponent<VillageEntranceCombatTrigger>().manager);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void UserAtMigrationBackModifyAssetTimeRejectFallback()
    {
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        ArchitectureAssetMigrationTool.Apply(record, recordPath);
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            root.GetComponent<EnemyAIManager>().maxActiveDistance = 99f;
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        string userSummary = ArchitectureAssetMigrationTool.CalculateSummary(assetPath);
        Assert.Throws<InvalidOperationException>(() => ArchitectureAssetMigrationTool.Fallback(record, recordPath));
        Assert.AreEqual(userSummary, ArchitectureAssetMigrationTool.CalculateSummary(assetPath));
    }

    [Test]
    public void HasHasComponentOnlyBindingNotOverrideConfigurationAndFallbackPreserveComponent()
    {
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            root.AddComponent<EnemyAIManager>().maxActiveDistance = 123f;
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        Assert.IsFalse(record.objectValue[0].newlyAddedComponent);
        ArchitectureAssetMigrationTool.Apply(record, recordPath);
        ArchitectureAssetMigrationTool.Fallback(record, recordPath);
        root = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            Assert.AreEqual(123f, root.GetComponent<EnemyAIManager>().maxActiveDistance);
            Assert.IsNull(root.GetComponent<VillageEntranceCombatTrigger>().manager);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void PreviewBackAssetChangeMustNotContinueApply()
    {
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        try { root.transform.localPosition = Vector3.right; PrefabUtility.SaveAsPrefabAsset(root, assetPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Assert.Throws<InvalidOperationException>(() => ArchitectureAssetMigrationTool.Apply(record, recordPath));
        Assert.IsFalse(File.Exists(recordPath));
    }

    [Test]
    public void PreviewObjectBeingTamperTimeMustNotIncreaseNotYetRegisterChange()
    {
        var record = ArchitectureAssetMigrationTool.Preview(Plan());
        record.objectValue[0].newlyAddedBinding = false;
        Assert.Throws<InvalidOperationException>(() => ArchitectureAssetMigrationTool.Apply(record, recordPath));
        Assert.IsFalse(File.Exists(recordPath));
    }

    [Test]
    public void SceneMigrationCanReloadValidateAndFallback()
    {
        string scenePath = assetPath.Replace(".prefab", ".unity");
        Scene originalActiveScene = SceneManager.GetActiveScene();
        // 使用已保存的空模板，避免替换测试框架自身的无标题运行场景。
        Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Editor/架构迁移验证模板.unity", scenePath));
        Scene validateScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            var root = new GameObject("场景迁移验证对象");
            SceneManager.MoveGameObjectToScene(root, validateScene);
            root.AddComponent<VillageEntranceCombatTrigger>();
            Assert.IsTrue(EditorSceneManager.SaveScene(validateScene, scenePath));
            var scenePlan = Plan();
            scenePlan.assetPath = scenePath;
            var record = ArchitectureAssetMigrationTool.Preview(scenePlan);
            ArchitectureAssetMigrationTool.Apply(record, recordPath);
            ArchitectureAssetMigrationTool.Validate(record);
            EditorSceneManager.CloseScene(validateScene, true);
            validateScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var manager = validateScene.GetRootGameObjects()[0].GetComponent<EnemyAIManager>();
            Assert.IsNotNull(manager);
            Assert.AreEqual(27.5f, manager.maxActiveDistance);
            ArchitectureAssetMigrationTool.Fallback(record, recordPath);
            EditorSceneManager.CloseScene(validateScene, true);
            validateScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            Assert.IsNull(validateScene.GetRootGameObjects()[0].GetComponent<EnemyAIManager>());
        }
        finally
        {
            if (validateScene.IsValid() && validateScene.isLoaded) EditorSceneManager.CloseScene(validateScene, true);
            AssetDatabase.DeleteAsset(scenePath);
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded) SceneManager.SetActiveScene(originalActiveScene);
        }
    }
}
