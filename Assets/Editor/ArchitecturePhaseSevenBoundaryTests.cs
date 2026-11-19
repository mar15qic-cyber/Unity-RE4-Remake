using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构阶段七边界测试")]

public sealed class ArchitecturePhaseSevenBoundaryTests
{
    [Test]
    public void ProductionCompilationImageAndRulesInvokeAllValid()
    {
        var failure = new List<string>();
        PhaseSevenCompilationBoundary.Validate(failure);
        Assert.IsEmpty(failure, string.Join("\n", failure));
    }

    [Test]
    public void CanLocateExtraAssemblyAndRulesLayerFindScene()
    {
        var failure = new List<string>();
        PhaseSevenCompilationBoundary.ValidateDependency("测试规则文件", new[] {"Game.Contracts", "Assembly-CSharp"}, new[] {"Game.Contracts"}, failure);
        StringAssert.Contains("测试规则文件", failure.Single());
        StringAssert.Contains("Assembly-CSharp", failure.Single());
        Assert.IsTrue(PhaseSevenCompilationBoundary.OutOfBoundsInvoke(typeof(InventoryRulesModel), typeof(GameObject).GetMethod("Find")));
        Assert.IsFalse(PhaseSevenCompilationBoundary.OutOfBoundsInvoke(typeof(GameUIManager), typeof(GameObject).GetMethod("Find")));
    }

    [Test]
    public void ProductionItemAssetStillCanPassPreviousFootCurrentIdentifierParse()
    {
        var asset = AssetDatabase.FindAssets("t:ItemData", new[]{"Assets/Resources/Items"});
        Assert.IsNotEmpty(asset);
        foreach (var identifier in asset)
        {
            string path = AssetDatabase.GUIDToAssetPath(identifier);
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            Assert.NotNull(item, path);
            Assert.AreEqual(typeof(ItemData), MonoScript.FromScriptableObject(item).GetClass(), path);
            Assert.IsNotEmpty(item.itemId, path);
        }
        Assert.AreNotEqual(typeof(InventoryManager).Assembly, typeof(GridItem).Assembly);
        Assert.IsFalse(typeof(GridItem).GetProperty("count").GetSetMethod(true).IsPublic);
    }
}
