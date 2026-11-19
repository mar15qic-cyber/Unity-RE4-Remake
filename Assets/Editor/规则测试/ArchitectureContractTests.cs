using System;
using System.Collections.Generic;
using NUnit.Framework;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Game.RuleTests","架构契约测试")]

public sealed class ArchitectureContractTests
{
    private static readonly ObjectIdentity identity = new ObjectIdentity(42, 1);
    private static RequestContext Request(ObjectIdentity target) => new RequestContext("规则测试", identity, target, 1);

    [Test]
    public void UninitializedAndInvalidIdentityCannotBeginAction()
    {
        var interval = new ActionLifetime();
        Assert.AreEqual(RejectionReason.NotYetReady, interval.TryBegin(Request(identity)).Reason);
        Assert.AreEqual(RejectionReason.ParameterInvalid, interval.TryBegin(default).Reason);
        Assert.Throws<ArgumentException>(() => interval.BeginLease(default));
    }

    [Test]
    public void PreviousActionCallbackCannotReleaseSubsequentAction()
    {
        var interval = new ActionLifetime();
        interval.BeginLease(identity);
        var ordinalOnce = interval.TryBegin(Request(identity));
        Assert.IsTrue(ordinalOnce.HasAccept);
        Assert.AreEqual(RejectionReason.ActionConflict, interval.TryBegin(Request(identity)).Reason);
        Assert.IsTrue(interval.TryRelease(ordinalOnce.Handle));
        var ordinalTwoIteration = interval.TryBegin(Request(identity));
        Assert.IsFalse(interval.TryRelease(ordinalOnce.Handle));
        Assert.IsTrue(interval.StillValid(ordinalTwoIteration.Handle));
        Assert.Greater(ordinalTwoIteration.Handle.Id, ordinalOnce.Handle.Id);
    }

    [Test]
    public void PreviousLeaseAndSameSequenceOtherObjectAllCannotPassCheck()
    {
        var interval = new ActionLifetime();
        interval.BeginLease(identity);
        var previousHandle = interval.TryBegin(Request(identity)).Handle;
        interval.Close();
        var newIdentity = new ObjectIdentity(42, 2);
        interval.BeginLease(newIdentity);
        var newHandle = interval.TryBegin(Request(newIdentity)).Handle;
        Assert.IsFalse(interval.StillValid(previousHandle));
        Assert.IsFalse(interval.StillValid(new ActionHandle(new ObjectIdentity(43, 2), newHandle.Id)));
        Assert.IsTrue(interval.StillValid(newHandle));
        Assert.Throws<InvalidOperationException>(() => interval.BeginLease(newIdentity));
        Assert.Throws<InvalidOperationException>(() => interval.BeginLease(identity));
    }

    [Test]
    public void RepeatedReleaseAndCloseWithoutAdditionalEffect()
    {
        var interval = new ActionLifetime();
        interval.BeginLease(identity);
        var handle = interval.TryBegin(Request(identity)).Handle;
        Assert.IsTrue(interval.TryRelease(handle));
        Assert.IsFalse(interval.TryRelease(handle));
        interval.Close();
        interval.Close();
        Assert.IsFalse(interval.Current.Valid);
        Assert.IsFalse(interval.StillValid(handle));
    }

    [Test]
    public void DefaultResultNotBeingTreatAsSuccessAndRejectHasReason()
    {
        Assert.IsFalse(default(ActionResult).HasAccept);
        Assert.AreNotEqual(RejectionReason.None, default(ActionResult).Reason);
        Assert.Throws<ArgumentException>(() => ActionResult.Accept(default));
        Assert.Throws<ArgumentException>(() => ActionResult.HasBuffer(default));
        Assert.Throws<ArgumentException>(() => ActionResult.Reject(RejectionReason.None));
    }

    [Test]
    public void DiagnosticsRingOverrideHoldTimeOrderAndSnapshotIndependent()
    {
        var recorder = new ArchitectureDiagnosticLog(2) { Enable = true };
        for (int time = 1; time <= 3; time++)
            recorder.Record(new ArchitectureDiagnosticEntry(Request(identity), default, DiagnosticEvent.StateChange, RejectionReason.None, ActionKind.None, ActionKind.Attack, time));
        var snapshot = recorder.ReadSnapshot();
        Assert.AreEqual(2, snapshot.Count);
        Assert.AreEqual(2, snapshot[0].Time);
        Assert.AreEqual(3, snapshot[1].Time);
        recorder.ClearValue();
        Assert.AreEqual(2, snapshot.Count);
        Assert.AreEqual(0, recorder.ReadSnapshot().Count);
        Assert.Throws<NotSupportedException>(() => ((IList<ArchitectureDiagnosticEntry>)snapshot)[0] = default);
    }

    [Test]
    public void HasCloseDiagnosticsDoesNotSaveRecord()
    {
        var recorder = new ArchitectureDiagnosticLog();
        recorder.Record(default);
        Assert.AreEqual(0, recorder.ReadSnapshot().Count);
    }

}
