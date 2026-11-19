using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构阶段五测试")]

public sealed class ArchitecturePhaseFiveTests
{
    private readonly List<ItemData> configuration = new List<ItemData>();
    private ItemData Item(string name, int width = 1, int height = 1, int stack = 10)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemId = name; item.displayName = name; item.gridWidth = width; item.gridHeight = height; item.maxStack = stack;
        configuration.Add(item); return item;
    }
    private InventoryRulesModel Inventory(int width = 2, int height = 2)
    { var model = new InventoryRulesModel(); model.Initialize(width, height, configuration.ToArray()); return model; }
    [TearDown] public void Cleanup() { foreach (var item in configuration) UnityEngine.Object.DestroyImmediate(item); configuration.Clear(); }

    [Test] public void ItemValueSnapshotNotFollowContainerSubsequentChange()
    {
        var item = Item("药"); var model = Inventory(); model.AddItem(item, 3);
        var snapshot = model.ReadSnapshot(); model.RemoveItem("药", 2);
        Assert.AreEqual(3, snapshot[0].Count); Assert.AreEqual(1, model.GetItemCount("药"));
        Assert.Throws<NotSupportedException>(() => ((IList<ItemStateSnapshot>)snapshot).Clear());
    }

    [Test] public void PermissionQueryAndMissionAdvanceNotProduceContinuousManagedAllocate()
    {
        var permission = new GameplayPermissionService(); var mission = new MissionRulesModel();
        mission.Initialize(15, 2, 360, .3f); mission.BeginFirstWave(); mission.CompleteCurrentWaveGenerate(true);
        for (int i = 0; i < 100; i++) if (permission.Current.AllowMissionTimer) mission.AdvanceTime(.001f);
        long starting = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) if (permission.Current.AllowMissionTimer) mission.AdvanceTime(.001f);
        long allocate = GC.GetAllocatedBytesForCurrentThread() - starting;
        Assert.AreEqual(0, allocate); Assert.Greater(mission.UsedTime, 10);
    }

    [Test] public void IntegrationBoundaryCheckWithoutOutOfBoundsPosition()
    { var failure = new List<string>(); PhaseFiveBoundaryValidator.Validate(failure); Assert.IsEmpty(failure, string.Join("\n", failure)); }

    [Test] public void UpdateModalOccupancyWillNotInstantReleasePause()
    {
        var service = new GameplayPermissionService(); var source = new ObjectIdentity(1, 1);
        var handle = service.Request(source, true, true, true, 0); int changeCount = 0;
        service.HasChange += state => changeCount++;
        Assert.IsTrue(service.Update(handle, true, true, true, 0));
        Assert.AreEqual(0, changeCount); Assert.AreEqual(1, service.OccupancyCount);
        Assert.IsFalse(service.Current.AllowInput);
    }

    [Test] public void SpaceInsufficientNotPartialDeductEnterStack()
    {
        var item = Item("弹药", 1, 1, 2); var model = Inventory(2, 1);
        Assert.IsTrue(model.AddItem(item, 3)); int eventValue = 0; model.OnInventoryChanged += () => eventValue++;
        Assert.IsFalse(model.AddItem(item, 2)); Assert.AreEqual(3, model.GetItemCount("弹药")); Assert.AreEqual(0, eventValue);
    }
    [Test] public void QueryCollectionAndInstanceWithoutPublicWriteEntryPoint()
    {
        var item = Item("物品"); var model = Inventory(); model.AddItem(item);
        Assert.Throws<NotSupportedException>(() => ((IList<GridItem>)model.GetItems()).Clear());
        foreach (string name in new[] { "item", "count", "gridX", "gridY", "rotation", "currentDurability" })
            Assert.IsNull(typeof(GridItem).GetProperty(name).GetSetMethod(), name);
    }
    [Test] public void CrossContainerInstanceCannotMovementOrRotation()
    {
        var item = Item("物品"); var first = Inventory(); var second = Inventory(); first.AddItem(item);
        var instance = first.GetFirstItem("物品"); Assert.IsFalse(second.MoveItem(instance, 0, 0)); Assert.IsFalse(second.RotateItem(instance));
    }
    [Test] public void CollisionMovementFailureHoldOriginalPosition()
    {
        var item = Item("物品", 1, 1, 1); var model = Inventory(2, 1); model.AddItem(item, 2);
        var instance = model.GetItems()[0]; int horizontal = instance.gridX;
        Assert.IsFalse(model.MoveItem(instance, 1, 0)); Assert.AreEqual(horizontal, instance.gridX);
    }
    [Test] public void CraftDoesNotFitTimeRestoreCountAndOriginalInstance()
    {
        var rawMaterial = Item("原料"); var outputAsset = Item("产物", 2, 2);
        outputAsset.craftingIngredients = new[] { new CraftingIngredientRequirement { itemId = "原料", count = 2 } };
        var model = Inventory(1, 1); model.AddItem(rawMaterial, 2); var originalInstance = model.GetFirstItem("原料");
        Assert.IsFalse(model.TryCraft(outputAsset, out _)); Assert.AreSame(originalInstance, model.GetFirstItem("原料")); Assert.AreEqual(2, originalInstance.count);
    }
    [Test] public void CraftSuccessOnlyNotificationCommittedCount()
    {
        var rawMaterial = Item("原料"); var outputAsset = Item("产物");
        outputAsset.craftingIngredients = new[] { new CraftingIngredientRequirement { itemId = "原料", count = 2 } };
        var model = Inventory(); model.AddItem(rawMaterial, 2); int eventValue = 0;
        model.OnInventoryChanged += () => { eventValue++; Assert.AreEqual(0, model.GetItemCount("原料")); Assert.AreEqual(1, model.GetItemCount("产物")); };
        Assert.IsTrue(model.TryCraft(outputAsset, out int countValue)); Assert.AreEqual(1, countValue); Assert.AreEqual(1, eventValue);
    }
    [Test] public void StorageTransferConservationAndPreserveDurability()
    {
        var knife = Item("刀", 1, 1, 1); knife.usesDurability = true; knife.maxDurability = 20;
        var model = Inventory(); var storage = new StorageRulesModel(); model.AddItem(knife); model.TryConsumeDurability("刀", 7, out _);
        var original = model.GetFirstItem("刀"); Assert.IsTrue(storage.CommitTransferToStorage(original, model));
        Assert.AreEqual(0, model.GetItemCount("刀")); Assert.AreEqual(13, storage.GetStorageItems()[0].currentDurability);
        Assert.IsTrue(storage.TransferToInventory(original, model)); Assert.IsFalse(storage.TransferToInventory(original, model));
        Assert.AreSame(original, model.GetFirstItem("刀")); Assert.AreEqual(13, original.currentDurability);
    }
    [Test] public void StorageGetReturnSpaceInsufficientBothSidesUnchanged()
    {
        var largeItem = Item("大件", 2, 2); var model = Inventory(1, 1); var storage = new StorageRulesModel(); storage.Initialize(largeItem);
        Assert.IsFalse(storage.TransferToInventory(storage.GetStorageItems()[0], model)); Assert.AreEqual(1, storage.GetStorageItems().Count); Assert.AreEqual(0, model.GetItems().Count);
    }
    [Test] public void StoredPreviousCallbackAndNewSessionCallbackCannotCommit()
    {
        var item = Item("枪"); var model = Inventory(); var storage = new StorageRulesModel(); var service = new ItemUseService(); model.AddItem(item);
        int id = service.BeginStored(model, model.GetFirstItem("枪"), 1); service.CancelStored();
        Assert.AreEqual(0, service.CompleteStored(id, true, true, 1, storage));
        id = service.BeginStored(model, model.GetFirstItem("枪"), 1); model.Initialize(2, 2, configuration.ToArray()); model.AddItem(item);
        Assert.AreEqual(-2, service.CompleteStored(id, true, true, 1, storage)); Assert.AreEqual(1, model.GetItemCount("枪"));
    }
    [Test] public void StoredSuccessDuplicateCallbackOnlyCommitOnce()
    {
        var item = Item("枪"); var model = Inventory(); var storage = new StorageRulesModel(); var service = new ItemUseService(); model.AddItem(item);
        int id = service.BeginStored(model, model.GetFirstItem("枪"), 1);
        Assert.AreEqual(1, service.CompleteStored(id, true, true, 1, storage)); Assert.AreEqual(0, service.CompleteStored(id, true, true, 1, storage)); Assert.AreEqual(1, storage.GetStorageItems().Count);
    }
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构阶段五测试+使用目标")]
    private sealed class UseTarget : IItemUseTarget
    {
        public bool Valid => true;
        public float CurrentHealth { get; private set; } = 20;
        public float HealthMaximum { get; private set; } = 100;
        public bool HasWeaponModule => true;
        [UnityEngine.Serialization.FormerlySerializedAs("弹药")]
        [UnityEngine.InspectorName("弹药")]
        public int ammo;
        public void IncreaseHealthMaximum(float count) { HealthMaximum += count; }
        public void Healing(float count) { CurrentHealth = Mathf.Min(HealthMaximum, CurrentHealth + count); }
        public void IncreaseCompatibleReserveAmmo(WeaponType type, int count) { ammo += count; }
    }
    private RequestContext UseRequest(int id = 1) => new RequestContext("测试使用", new ObjectIdentity(1, 1), new ObjectIdentity(2, 1), id);
    [Test] public void HealingTransactionNoneInterfaceAlsoCanValidateAndDuplicateRequestReject()
    {
        var medicine = Item("药"); medicine.itemType = ItemType.Healing; medicine.healAmount = 30;
        var model = Inventory(); model.AddItem(medicine, 2); var service = new ItemUseService(); var target = new UseTarget(); var instance = model.GetFirstItem("药");
        int notification = 0;
        model.OnInventoryChanged += () => { notification++; Assert.AreEqual(50, target.CurrentHealth); Assert.AreEqual(1, instance.count); };
        Assert.IsTrue(service.RequestUse(UseRequest(), model, instance, target).Committed);
        Assert.IsFalse(service.RequestUse(UseRequest(), model, instance, target).Committed);
        Assert.AreEqual(50, target.CurrentHealth); Assert.AreEqual(1, instance.count);
        Assert.AreEqual(1, notification);
    }
    [Test] public void AmmoTransferHoldOriginalCountConvert()
    {
        var ammo = Item("弹药"); ammo.itemType = ItemType.Ammo; ammo.ammoAmount = 5;
        var model = Inventory(); model.AddItem(ammo, 3); var target = new UseTarget();
        Assert.IsTrue(new ItemUseService().RequestUse(UseRequest(), model, model.GetFirstItem("弹药"), target).Committed);
        Assert.AreEqual(15, target.ammo); Assert.AreEqual(0, model.GetItemCount("弹药"));
    }
    [Test] public void TwoLayerPauseReleaseAmongOneElementCannotRestoreGameplay()
    {
        var service = new GameplayPermissionService(); var identity = new ObjectIdentity(1, 1);
        var first = service.Request(identity, true, true, true, 0); var second = service.Request(identity, true, true, true, 0);
        service.Release(first); Assert.IsFalse(service.Current.AllowSimulation); Assert.IsFalse(service.Current.AllowInput);
        service.Release(second); Assert.IsTrue(service.Current.AllowSimulation); Assert.IsTrue(service.Current.AllowInput);
    }
    [Test] public void PauseAfterCompletionRestoreStillBeingOccupancyOfSlowAction()
    {
        var service = new GameplayPermissionService(); var identity = new ObjectIdentity(1, 1);
        var slow = service.Request(identity, false, false, false, .2f); var pause = service.Request(identity, true, true, true, 0);
        service.Release(pause); Assert.AreEqual(.2f, service.Current.TimeMultiplier); service.Release(slow); Assert.AreEqual(1, service.Current.TimeMultiplier);
    }
    [Test] public void PairedActionOccupancyNotChangeMissionTimerWhileInterfaceOccupancyBlockTimer()
    {
        var service = new GameplayPermissionService(); service.Request(new ObjectIdentity(1, 1), true, false, false);
        Assert.IsFalse(service.Current.AllowInput); Assert.IsTrue(service.Current.AllowMissionTimer);
        service.Request(new ObjectIdentity(2, 1), false, true, false); Assert.IsFalse(service.Current.AllowMissionTimer);
    }
    [Test] public void PreviousServiceTokenAndRepeatedReleaseCannotAffectNewOccupancy()
    {
        var first = new GameplayPermissionService(); var second = new GameplayPermissionService(); var identity = new ObjectIdentity(1, 1);
        var previous = first.Request(identity, true, true, true, 0); var newValue = second.Request(identity, true, true, true, 0);
        Assert.IsFalse(second.Release(previous)); Assert.IsFalse(second.Current.AllowInput); Assert.IsTrue(second.Release(newValue)); Assert.IsFalse(second.Release(newValue));
    }
    [Test] public void CloseServiceReleaseAllRecordAndRejectNewRequest()
    {
        var service = new GameplayPermissionService(); var identity = new ObjectIdentity(1, 1); service.Request(identity, true, true, true, 0);
        service.Close(); service.Close(); Assert.AreEqual(0, service.OccupancyCount); Assert.IsFalse(service.Request(identity, true, true, true).Valid);
    }
    [Test] public void MatchEndedHoldFirstElementResultAndAfterDeathNotAcceptVictory()
    {
        var first = new MatchFlow(); Assert.IsTrue(first.CommitDeath()); Assert.IsFalse(first.CommitLevelComplete(default, false));
        var second = new MatchFlow(); Assert.IsFalse(second.CommitLevelComplete(default, true)); Assert.IsTrue(second.CommitLevelComplete(default, false)); Assert.IsFalse(second.CommitDeath());
    }
    [Test] public void MissionDuplicateDeathNotDuplicateAccumulatedAndPressOriginalRatioAdvance()
    {
        var rules = new MissionRulesModel(); rules.Initialize(15, 2, 360, .3f); rules.BeginFirstWave();
        for (int i = 1; i <= 7; i++) rules.RegisterGenerate(i, MissionEnemyType.Ganado);
        rules.CompleteCurrentWaveGenerate(true);
        for (int i = 1; i <= 4; i++) rules.RegisterDeath(i, true);
        Assert.IsFalse(rules.CanAdvanceWave); Assert.IsFalse(rules.RegisterDeath(1, true)); rules.RegisterDeath(5, true);
        Assert.IsTrue(rules.CanAdvanceWave); Assert.AreEqual(5, rules.NormalKill);
    }
    [Test] public void MissionDeathStopAndSurvivalTimeMaximumHoldOriginalRules()
    {
        var rules = new MissionRulesModel(); rules.Initialize(15, 2, 360, .3f); rules.CompleteCurrentWaveGenerate(true); rules.AdvanceTime(400);
        Assert.AreEqual(360, rules.UsedTime); Assert.IsTrue(rules.ReachSurvival); rules.Stop(); rules.AdvanceTime(20); Assert.IsFalse(rules.RuntimeMiddle);
    }
    [Test] public void DropSnapshotNotExposeOrInheritExternalWritableDictionary()
    {
        var dictionary = new Dictionary<string, int> { { "pistol", 1 }, { "材料", 3 } };
        var snapshot = new DropResourceSnapshot(dictionary, true, true, .5f, 10, 0, 0); dictionary.Clear();
        Assert.IsTrue(snapshot.HasWeapon(WeaponType.Pistol)); Assert.AreEqual(3, snapshot.Count("材料")); Assert.AreEqual(10, snapshot.ReserveAmmo(WeaponType.Pistol));
    }
    [Test] public void PhaseFiveRulesNotReverseLookupSceneOrInterfaceAndCompatibleWrapperNoneOrdinalTwoCopyItemCollection()
    {
        foreach (string file in new[] { "架构/背包规则/InventoryRulesModel.cs", "架构/背包规则/StorageRulesModel.cs", "架构/任务规则/MissionRulesModel.cs", "Core/MatchFlow.cs", "Core/GameplayPermissionService.cs", "Drop/DropWeightRules.cs" })
        {
            string code = File.ReadAllText("Assets/Scripts/" + file);
            foreach (string outOfBounds in new[] { ".Instance", "Resources.Load", "FindObject", "GameUIManager", "MonoBehaviour", "Time.timeScale" }) Assert.IsFalse(code.Contains(outOfBounds), file + "：" + outOfBounds);
        }
        foreach (Type type in new[] { typeof(InventoryManager), typeof(StorageBoxManager) })
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) Assert.AreNotEqual(typeof(List<GridItem>), field.FieldType, type.Name + "：" + field.Name);
        Assert.IsFalse(File.ReadAllText("Assets/Scripts/UI/GameUIManager.cs").Contains("Time.timeScale ="));
        Assert.IsFalse(File.ReadAllText("Assets/Scripts/Managers/ChapterOneMissionManager.cs").Contains("GameUIManager"));
    }
}
