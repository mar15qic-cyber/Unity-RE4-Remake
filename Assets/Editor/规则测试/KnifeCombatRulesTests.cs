using System;
using System.Collections.Generic;
using NUnit.Framework;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Game.RuleTests","小刀战斗规则测试")]

public sealed class KnifeCombatRulesTests
{
    [Test]
    public void IncomingAttackAlreadyContactOnlyAfterPressGuardCannotTraceBackDamageImmunity()
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 1.01);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(1, true, true));
    }
    [Test]
    public void RestoringHeldGuardDoesNotCreateAParryAttempt()
    {
        var rules = new KnifeCombatRules();
        rules.ResumeHeldGuard(1);
        Assert.IsTrue(rules.GuardHeld);
        Assert.AreEqual(KnifeAction.EnterGuard, rules.CurrentAction);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(1.05, true, true));
        Assert.AreEqual(KnifeParryFailure.NoInput, rules.LastParryFailure);

        rules.GuardInput(false, 1.07);
        rules.GuardInput(true, 1.08);
        Assert.AreEqual(KnifeParryOutcome.Perfect, rules.ResolutionParry(1.09, true, true));
    }

    [Test]
    public void SingleClickOnlyCauseOnceAttack()
    {
        var rules = new KnifeCombatRules(); var knifeCount = new HashSet<long>();
        rules.IntervalTraversed += segment => knifeCount.Add(segment.instance);
        rules.AttackInput(); rules.Advance(3, 3);
        Assert.AreEqual(1, knifeCount.Count); Assert.IsFalse(rules.Active);
    }
    [Test]
    public void QuickButtonMashingOnlyCacheNextAttack()
    {
        var rules = new KnifeCombatRules(); var order = new List<KnifeAction>();
        rules.ActionHasSwitch += (action, instance, offset) => { if (KnifeCombatRules.IsAttackValue(action)) order.Add(action); };
        rules.AttackInput(); for (int iteration = 0; iteration < 100; iteration++) rules.AttackInput();
        rules.Advance(3, 3);
        CollectionAssert.AreEqual(new[] { KnifeAction.PrimarySlash, KnifeAction.Backslash }, order);
    }
    [Test]
    public void ComboLinkPointAfterClickAgainStartup()
    {
        var rules = new KnifeCombatRules(); rules.AttackInput(); rules.Advance(.60, .60);
        Assert.AreEqual(KnifeAction.PrimarySlashRecovery, rules.CurrentAction);
        rules.AttackInput(); Assert.AreEqual(KnifeAction.PrimarySlash, rules.CurrentAction); Assert.AreEqual(0, rules.ActionTime);
    }
    [TestCase(false, 1)]
    [TestCase(true, 2)]
    public void ThrustSingleClickAndDoubleClickSeparatelyConsumeIndependentClip(bool ordinalTwoIteration, int expected)
    {
        var rules = new KnifeCombatRules(); var knife = new HashSet<long>();
        rules.IntervalTraversed += segment => knife.Add(segment.instance);
        rules.GuardInput(true, 0); rules.AttackInput(); if (ordinalTwoIteration) rules.AttackInput();
        rules.Advance(3, 3);
        Assert.AreEqual(expected, knife.Count); Assert.AreEqual(KnifeAction.HoldGuard, rules.CurrentAction);
    }
    [Test]
    public void ReleasedGuardClearHasCacheThrust()
    {
        var rules = new KnifeCombatRules(); var knife = new HashSet<long>(); rules.IntervalTraversed += segment => knife.Add(segment.instance);
        rules.GuardInput(true, 0); rules.AttackInput(); rules.AttackInput();
        rules.Advance(.2, .2); rules.GuardInput(false, .2); rules.Advance(2, 2.2);
        Assert.AreEqual(1, knife.Count); Assert.IsFalse(rules.Active);
    }
    [Test]
    public void ReleasedThrustEntersLowerGuardAfterTheContactWindow()
    {
        var rules = new KnifeCombatRules();
        rules.GuardInput(true, 0);
        rules.AttackInput();
        var timing = rules.Query(KnifeAction.ThrustOne);
        double release = System.Math.Min(timing.duration,
            System.Math.Max(timing.hitEnd, timing.recoveryCancelBegin));

        rules.Advance(release * .5, release * .5);
        rules.GuardInput(false, release * .5);
        Assert.AreEqual(KnifeAction.ThrustOne, rules.CurrentAction);

        rules.Advance(release * .5 + .001, release + .001);
        Assert.AreEqual(KnifeAction.LowerGuard, rules.CurrentAction);
        Assert.IsTrue(rules.CanHandoff);
    }

    [TestCase(.099, true)]
    [TestCase(.101, false)]
    [TestCase(.25, false)]
    [TestCase(.441, true)]
    public void PrimarySlashPressPhaseAllowGuardCancel(double timestamp, bool shouldCancel)
    {
        var rules = new KnifeCombatRules(); rules.AttackInput(); rules.Advance(timestamp, timestamp); rules.GuardInput(true, timestamp);
        Assert.AreEqual(shouldCancel, rules.CurrentAction == KnifeAction.EnterGuard);
        Assert.AreEqual(shouldCancel, rules.HasGuardEligibility(timestamp));
    }
    [Test]
    public void DelayCancelMustNotAgainCalculatePerfectTime()
    {
        var rules = new KnifeCombatRules(); rules.AttackInput(); rules.Advance(.2, .2); rules.GuardInput(true, .2);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.25, true, true));
        rules.Advance(.25, .45);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.45, true, true));
        Assert.AreEqual(KnifeParryFailure.Expired, rules.LastParryFailure);
    }
    [TestCase(.109999, KnifeParryOutcome.Perfect)]
    [TestCase(.11, KnifeParryOutcome.Perfect)]
    [TestCase(.110001, KnifeParryOutcome.Normal)]
    [TestCase(.2, KnifeParryOutcome.Normal)]
    [TestCase(.200001, KnifeParryOutcome.Failure)]
    [TestCase(.8, KnifeParryOutcome.Failure)]
    public void PerfectWindowBoundary(double contactTimestamp, KnifeParryOutcome expected)
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 0); rules.Advance(contactTimestamp, contactTimestamp);
        Assert.AreEqual(expected, rules.ResolutionParry(contactTimestamp, true, true));
    }
    [Test]
    public void SameAttemptPressedOnlyConsumeOncePerfectAndOrdinalTwoEnemyStillIndependentResolution()
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 0);
        var first = rules.ResolutionParry(.06, true, true); rules.ParryReaction(first, KnifeIncomingAttackCategory.High);
        Assert.AreEqual(KnifeParryOutcome.Perfect, first);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.06, true, true));
        Assert.AreEqual(KnifeParryFailure.Consumed, rules.LastParryFailure);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.06, false, true));
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.06, true, false));
    }
    [Test]
    public void HoldInputNotRefreshWindowAgainIterationPressedOnlyThenRefresh()
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 0); rules.GuardInput(true, 1);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(1.01, true, true));
        rules.GuardInput(false, 1.02); rules.GuardInput(true, 1.03);
        Assert.AreEqual(KnifeParryOutcome.Perfect, rules.ResolutionParry(1.04, true, true));
    }
    [Test]
    public void TapCannotBecomeAsContinuousGuard()
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 0); rules.GuardInput(false, .02);
        Assert.IsTrue(rules.HasGuardEligibility(.19)); Assert.IsFalse(rules.HasGuardEligibility(.201));
        rules.Advance(.4, .4); Assert.IsFalse(rules.Active);
    }
    [Test]
    public void NormalParryConsumesAttemptAndReactionAcceptsNewPress()
    {
        var rules = new KnifeCombatRules();
        rules.GuardInput(true, 0);
        Assert.AreEqual(KnifeParryOutcome.Normal, rules.ResolutionParry(.15, true, true));
        rules.ParryReaction(KnifeParryOutcome.Normal, KnifeIncomingAttackCategory.High);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.16, true, true));
        rules.GuardInput(false, .16);
        rules.GuardInput(true, .17);
        Assert.AreEqual(KnifeParryOutcome.Perfect, rules.ResolutionParry(.18, true, true));
    }

    [Test]
    public void LaterInputCannotSavePastContactOrOverwriteEarlierValidAttempt()
    {
        var rules = new KnifeCombatRules();
        rules.GuardInput(true, .12);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.11, true, true));
        Assert.AreEqual(KnifeParryFailure.InputAfterContact, rules.LastParryFailure);
        rules.Interrupt();
        rules.GuardInput(true, .2, 10);
        rules.GuardInput(false, .22);
        rules.GuardInput(true, .31, 11);
        Assert.AreEqual(KnifeParryOutcome.Perfect, rules.ResolutionParry(.29, true, true));
        Assert.AreEqual(10, rules.LastResolvedAttempt.sequence);
        Assert.IsFalse(rules.CurrentAttempt.consumed);
    }

    [Test]
    public void HistoricalContactCannotUseGuardOpenedLaterInSameFrame()
    {
        var rules = new KnifeCombatRules();
        rules.AttackInput();
        rules.Advance(.3, .3);
        rules.GuardInput(true, .3);
        rules.Advance(.16, .46);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.43, true, true));
        Assert.AreEqual(KnifeParryFailure.PhaseBlocked, rules.LastParryFailure);
        Assert.AreEqual(KnifeParryOutcome.Normal, rules.ResolutionParry(.45, true, true));
    }

    [TestCase(30, .09, KnifeParryOutcome.Perfect)]
    [TestCase(60, .09, KnifeParryOutcome.Perfect)]
    [TestCase(120, .09, KnifeParryOutcome.Perfect)]
    [TestCase(30, .15, KnifeParryOutcome.Normal)]
    [TestCase(60, .15, KnifeParryOutcome.Normal)]
    [TestCase(120, .15, KnifeParryOutcome.Normal)]
    [TestCase(30, .25, KnifeParryOutcome.Failure)]
    [TestCase(60, .25, KnifeParryOutcome.Failure)]
    [TestCase(120, .25, KnifeParryOutcome.Failure)]
    public void TimestampedTapKeepsOutcomeAcrossFrameRates(int fps, double lead, KnifeParryOutcome expected)
    {
        var rules = new KnifeCombatRules();
        double pressed = .6 - lead;
        bool down = false;
        bool up = false;
        double advanced = 0;
        for (int frame = 1; frame <= fps; frame++)
        {
            double now = frame / (double)fps;
            if (!down && now >= pressed)
            {
                rules.Advance(pressed - advanced, pressed);
                advanced = pressed;
                rules.GuardInput(true, pressed);
                down = true;
            }
            if (!up && now >= pressed + .015)
            {
                rules.Advance(pressed + .015 - advanced, pressed + .015);
                advanced = pressed + .015;
                rules.GuardInput(false, advanced);
                up = true;
            }
            rules.Advance(now - advanced, now);
            advanced = now;
            if (now >= .6)
            {
                Assert.AreEqual(expected, rules.ResolutionParry(.6, true, true));
                return;
            }
        }
        Assert.Fail("未执行接触裁定");
    }
    [Test]
    public void InterruptClearCacheAndPerfectEligibility()
    {
        var rules = new KnifeCombatRules(); rules.GuardInput(true, 0); rules.AttackInput(); rules.AttackInput();
        rules.Interrupt(); Assert.IsFalse(rules.Active); Assert.IsFalse(rules.HasCache);
        Assert.AreEqual(KnifeParryOutcome.Failure, rules.ResolutionParry(.03, true, true));
    }
    [Test]
    public void SingleFrameCrossEntireActivePhaseStillCompleteReport()
    {
        var rules = new KnifeCombatRules(); double totalInterval = 0;
        rules.IntervalTraversed += segment => totalInterval += segment.end - segment.begin;
        rules.AttackInput(); rules.Advance(1, 1);
        Assert.AreEqual(.28, totalInterval, .000001);
    }
    [Test]
    public void HitStopZeroAdvanceStillAllowCacheInput()
    {
        var rules = new KnifeCombatRules(); rules.AttackInput(); rules.Advance(.2, .2);
        double originalProgress = rules.ActionTime; rules.AttackInput(); rules.Advance(0, .265);
        Assert.AreEqual(originalProgress, rules.ActionTime); Assert.IsTrue(rules.HasCache);
        rules.Advance(.4, .665); Assert.AreEqual(KnifeAction.Backslash, rules.CurrentAction);
    }
    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void SameTimestampAtDifferentFrameRateProduceSameAttackOrderAndHitInterval(int frameRate)
    {
        var rules = new KnifeCombatRules(); var order = new List<KnifeAction>(); double hitTotalLong = 0;
        rules.ActionHasSwitch += (action, instance, offset) => { if (KnifeCombatRules.IsAttackValue(action)) order.Add(action); };
        rules.IntervalTraversed += segment => hitTotalLong += segment.end - segment.begin;
        double[] click = { .011, .121, .122, .123, .701 };
        int input = 0; double advanced = 0;
        for (int frame = 1; frame <= frameRate * 4; frame++)
        {
            double endPoint = frame / (double)frameRate;
            while (input < click.Length && click[input] <= endPoint)
            { double timestamp = click[input++]; rules.Advance(timestamp - advanced, timestamp); advanced = timestamp; rules.AttackInput(); }
            rules.Advance(endPoint - advanced, endPoint); advanced = endPoint;
        }
        CollectionAssert.AreEqual(new[] { KnifeAction.PrimarySlash, KnifeAction.Backslash, KnifeAction.PrimarySlash }, order);
        Assert.AreEqual(.82, hitTotalLong, .000001); Assert.IsFalse(rules.Active);
    }
}
