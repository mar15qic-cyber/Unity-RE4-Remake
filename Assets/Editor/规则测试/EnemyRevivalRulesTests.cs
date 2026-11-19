using GameArchitecture;
using NUnit.Framework;

public sealed class EnemyRevivalRulesTests
{
    [Test]
    public void RevivalWaitStartsAtLoopEntryAndRejectsDuplicateOrExpiredSchedule()
    {
        var rules = new EnemyRevivalRules();
        Assert.IsTrue(rules.BeginDying(double.MaxValue));
        long revision = rules.Revision;
        Assert.IsFalse(rules.BeginReviving(revision, 100d));
        Assert.IsFalse(rules.ScheduleWait(revision - 1, 102d));
        Assert.IsTrue(rules.ScheduleWait(revision, 102d));
        Assert.IsFalse(rules.ScheduleWait(revision, 103d));
        Assert.IsFalse(rules.BeginReviving(revision, 101.9d));
        Assert.IsTrue(rules.BeginReviving(revision, 102d));
    }

    [Test]
    public void DyingWaitsForDeadlineAndCanReviveOnlyOnce()
    {
        var rules = new EnemyRevivalRules();
        Assert.IsTrue(rules.BeginDying(12d));
        long revision = rules.Revision;
        Assert.IsFalse(rules.BeginReviving(revision, 11.999d));
        Assert.IsFalse(rules.BeginReviving(revision, double.NaN));
        Assert.IsTrue(rules.BeginReviving(revision, 12d));
        Assert.IsTrue(rules.CompleteRevival(revision));
        Assert.IsFalse(rules.CompleteRevival(revision));
        Assert.IsFalse(rules.BeginDying(20d));
        Assert.AreEqual(EnemyLifePhase.Revived, rules.Phase);
    }

    [TestCase(EnemyLifePhase.Dying)]
    [TestCase(EnemyLifePhase.Reviving)]
    [TestCase(EnemyLifePhase.Revived)]
    public void TerminalDeathCannotBeUndoneByOldTimers(EnemyLifePhase phase)
    {
        var rules = new EnemyRevivalRules();
        rules.BeginDying(0d);
        long revision = rules.Revision;
        if (phase != EnemyLifePhase.Dying)
        {
            rules.BeginReviving(revision, 0d);
        }
        if (phase == EnemyLifePhase.Revived)
        {
            rules.CompleteRevival(revision);
        }
        Assert.IsTrue(rules.ConfirmDeath());
        Assert.IsFalse(rules.ConfirmDeath());
        Assert.IsFalse(rules.BeginReviving(revision, 50d));
        Assert.IsFalse(rules.CompleteRevival(revision));
        Assert.IsFalse(rules.BeginDying(100d));
        Assert.AreEqual(EnemyLifePhase.Dead, rules.Phase);
    }

    [Test]
    public void DisableInvalidatesPendingTransition()
    {
        var rules = new EnemyRevivalRules();
        rules.BeginDying(0d);
        long revision = rules.Revision;
        rules.InvalidatePendingTransition();
        Assert.IsFalse(rules.BeginReviving(revision, 1d));
        Assert.IsTrue(rules.BeginReviving(rules.Revision, 1d));
    }

    [TestCase(false, 100f, 0f, false)]
    [TestCase(true, 15f, .149f, true)]
    [TestCase(true, 15f, .15f, false)]
    [TestCase(true, 20f, .199f, true)]
    [TestCase(true, 20f, .20f, false)]
    [TestCase(true, 0f, 0f, false)]
    public void RevivalDecisionUsesProvidedRandom(bool eligible, float probability, float random, bool expected)
    {
        Assert.AreEqual(expected, EnemyRevivalRules.ShouldRevive(eligible, probability, random));
    }

    [Test]
    public void WarningRejectsExpiredActionAndReusedObject()
    {
        var action = new ActionHandle(new ObjectIdentity(42, 1), 7);
        var warning = new EnemyAttackWarningSnapshot(action, 1d, 2d, true);
        Assert.IsTrue(warning.IsCurrent(action, 1d));
        Assert.IsFalse(warning.IsCurrent(action, .99d));
        Assert.IsFalse(warning.IsCurrent(action, 2.01d));
        Assert.IsFalse(warning.IsCurrent(new ActionHandle(new ObjectIdentity(42, 1), 8), 1.5d));
        Assert.IsFalse(warning.IsCurrent(new ActionHandle(new ObjectIdentity(42, 2), 7), 1.5d));
    }
}
