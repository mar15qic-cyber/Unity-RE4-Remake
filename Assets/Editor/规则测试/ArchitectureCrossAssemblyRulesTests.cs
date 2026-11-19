using NUnit.Framework;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Game.RuleTests","架构跨程序集规则测试")]

public sealed class ArchitectureCrossAssemblyRulesTests
{
    [Test]
    public void ReloadAfterInterruptPreviousCallbackNotDeductStock()
    {
        var weapon = new WeaponRuntimeModule();
        weapon.InitializeWeapon(1, 0);
        Assert.IsTrue(weapon.BeginReload(1, 5, 10));
        weapon.CancelReload(1);
        Assert.IsTrue(weapon.BeginReload(1, 5, 11));
        int stock = 7;
        Assert.AreEqual(0, weapon.CommitReload(1, 5, 10, stock, false, x => stock -= x));
        Assert.AreEqual(5, weapon.CommitReload(1, 5, 11, stock, false, x => stock -= x));
        Assert.AreEqual(2, stock);
        Assert.AreEqual(2f, BodyPartDamageRules.QueryBodyPartMultiplier(HitboxType.Head));
    }

    [Test]
    public void StorageRoundTripCountConservation()
    {
        var definition = ScriptableObject.CreateInstance<ItemData>();
        try
        {
            definition.itemId = "回归弹药";
            var inventory = new InventoryRulesModel();
            inventory.Initialize(2, 2, new[]{definition});
            var storage = new StorageRulesModel();
            storage.Initialize(null);
            Assert.IsTrue(inventory.AddItem(definition, 3));
            Assert.IsTrue(storage.CommitTransferToStorage(inventory.GetItemAt(0,0), inventory));
            Assert.AreEqual(0, inventory.GetItemCount(definition.itemId));
            Assert.IsTrue(storage.TransferToInventory(storage.GetStorageItems()[0], inventory));
            Assert.AreEqual(3, inventory.GetItemCount(definition.itemId));
            Assert.IsEmpty(storage.GetStorageItems());
        }
        finally { Object.DestroyImmediate(definition); }
    }

    [Test]
    public void NewMissionTargetOnlyByConfigurationValueDetermine()
    {
        var mission = new MissionRulesModel();
        mission.Initialize(1,0,90,.5f);
        mission.BeginFirstWave();
        mission.RegisterGenerate(17,MissionEnemyType.Ganado);
        mission.CompleteCurrentWaveGenerate(true);
        Assert.IsTrue(mission.RegisterDeath(17,true));
        Assert.IsFalse(mission.RegisterDeath(17,true));
        Assert.IsTrue(mission.ReachElimination);
        Assert.AreEqual(90,mission.Snapshot().SurvivalDuration);
    }

    [Test]
    public void VillageReinforcementCreditSurvivesDelayedSpawns()
    {
        var mission = new MissionRulesModel();
        mission.InitializeVillage(15, 600f, 8, 4);
        mission.BeginFirstWave();
        for (int identity = 1; identity <= 8; identity++)
        {
            Assert.IsTrue(mission.RegisterGenerate(identity, MissionEnemyType.Ganado));
        }

        mission.AdvanceTime(30f);
        Assert.AreEqual(0f, mission.UsedTime);
        mission.CompleteCurrentWaveGenerate(true);
        for (int identity = 1; identity <= 3; identity++)
        {
            Assert.IsTrue(mission.RegisterDeath(identity, true));
        }

        Assert.AreEqual(0, mission.PendingReinforcements);
        Assert.IsTrue(mission.RegisterDeath(4, true));
        Assert.AreEqual(4, mission.PendingReinforcements);
        Assert.IsTrue(mission.RegisterGenerate(9, MissionEnemyType.Sawman, true));
        Assert.IsFalse(mission.RegisterGenerate(9, MissionEnemyType.Sawman, true));
        Assert.AreEqual(3, mission.PendingReinforcements);

        for (int identity = 5; identity <= 8; identity++)
        {
            Assert.IsTrue(mission.RegisterDeath(identity, true));
        }

        Assert.AreEqual(7, mission.PendingReinforcements);
        for (int identity = 10; identity <= 16; identity++)
        {
            Assert.IsTrue(mission.RegisterGenerate(identity, MissionEnemyType.Ganado, true));
        }

        Assert.AreEqual(8, mission.AliveCount);
        Assert.IsFalse(mission.RegisterGenerate(17, MissionEnemyType.Ganado, true));
        for (int identity = 9; identity <= 15; identity++)
        {
            Assert.IsTrue(mission.RegisterDeath(identity, true));
        }

        Assert.IsTrue(mission.ReachElimination);
        Assert.AreEqual(0, mission.PendingReinforcements);
        Assert.IsFalse(mission.RegisterGenerate(17, MissionEnemyType.Ganado, true));
    }
}
