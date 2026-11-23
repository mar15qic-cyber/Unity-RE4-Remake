using NUnit.Framework;

public sealed class VillageEntranceRulesTests
{
    private MissionRulesModel rules;

    [SetUp]
    public void StartVillage()
    {
        rules = new MissionRulesModel();
        rules.InitializeVillage(15, 600f, 8, 4);
        rules.BeginFirstWave();
        rules.CompleteCurrentWaveGenerate(true);
    }

    [Test]
    public void HouseCrossingWinsWhenBothLocationsQualify()
    {
        rules.ObserveVillageEntrances(true, true, 6, 120f);
        Assert.AreEqual(VillageEntrance.House, rules.PendingEntrance);
        Assert.IsTrue(rules.CanSpawnAtEntrance(VillageEntrance.House));

        Assert.IsTrue(rules.RegisterGenerate(16, MissionEnemyType.Sawman));
        Assert.IsTrue(rules.CompleteEntrance(VillageEntrance.House));
        rules.RegisterDeath(16, true);
        rules.AdvanceTime(120f);
        rules.ObserveVillageEntrances(true, true, 6, 120f);
        Assert.AreEqual(VillageEntrance.None, rules.PendingEntrance);
    }

    [Test]
    public void FarmDoesNotAddAnotherEnemyWhenHouseIsEnteredBeforeFarmEnemyDies()
    {
        rules.ObserveVillageEntrances(false, true, 6, 120f);
        Assert.IsTrue(rules.RegisterGenerate(16, MissionEnemyType.Sawman));
        rules.CompleteEntrance(VillageEntrance.Farm);

        rules.ObserveVillageEntrances(true, false, 6, 120f);
        Assert.AreEqual(VillageEntrance.House, rules.PendingEntrance);
        Assert.IsFalse(rules.CanSpawnAtEntrance(VillageEntrance.House));

        rules.RegisterDeath(16, true);
        Assert.IsFalse(rules.CanSpawnAtEntrance(VillageEntrance.House));
    }

    [Test]
    public void EnteringHouseAfterFarmKillCanSpawnSecondEnemy()
    {
        rules.ObserveVillageEntrances(false, true, 6, 120f);
        Assert.IsTrue(rules.CanSpawnAtEntrance(VillageEntrance.Farm));
        Assert.IsTrue(rules.RegisterGenerate(16, MissionEnemyType.Sawman));
        rules.CompleteEntrance(VillageEntrance.Farm);
        rules.RegisterDeath(16, true);

        rules.ObserveVillageEntrances(true, false, 6, 120f);
        Assert.IsTrue(rules.CanSpawnAtEntrance(VillageEntrance.House));
        Assert.IsTrue(rules.RegisterGenerate(17, MissionEnemyType.Sawman));
        rules.CompleteEntrance(VillageEntrance.House);
        Assert.AreEqual(1, rules.ChainsawAlive);
        Assert.AreEqual(2, rules.ChainsawHasGenerate);
    }

    [Test]
    public void HouseKeepsItsSequenceWhileFarmSpawnIsPending()
    {
        rules.AdvanceTime(120f);
        rules.ObserveVillageEntrances(false, false, 6, 120f);
        rules.ObserveVillageEntrances(true, true, 6, 120f);
        Assert.AreEqual(VillageEntrance.Farm, rules.PendingEntrance);
        rules.RegisterGenerate(16, MissionEnemyType.Sawman);
        rules.CompleteEntrance(VillageEntrance.Farm);
        Assert.AreEqual(VillageEntrance.House, rules.PendingEntrance);
        Assert.IsFalse(rules.CanSpawnAtEntrance(VillageEntrance.House));
    }

    [Test]
    public void SixthNormalKillQueuesFarmOnceEvenIfTimerAndGateQualifyLater()
    {
        for (int id = 1; id <= 6; id++)
        {
            rules.RegisterGenerate(id, MissionEnemyType.Ganado);
            rules.RegisterDeath(id, true);
        }
        rules.ObserveVillageEntrances(false, false, 6, 120f);
        Assert.AreEqual(VillageEntrance.Farm, rules.PendingEntrance);
        rules.RegisterGenerate(16, MissionEnemyType.Sawman);
        rules.CompleteEntrance(VillageEntrance.Farm);
        rules.AdvanceTime(120f);
        rules.ObserveVillageEntrances(false, true, 6, 120f);
        Assert.AreEqual(VillageEntrance.None, rules.PendingEntrance);
    }

    [Test]
    public void StoppingMatchClearsPendingEntrance()
    {
        rules.ObserveVillageEntrances(false, true, 6, 120f);
        rules.Stop();

        Assert.AreEqual(VillageEntrance.None, rules.PendingEntrance);
        Assert.IsFalse(rules.CanSpawnAtEntrance(VillageEntrance.Farm));
    }
}
