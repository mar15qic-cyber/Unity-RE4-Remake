using NUnit.Framework;
using UnityEngine;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","自由视角测试")]

public sealed class FreeLookTests
{
    private static Vector3 Movement(FreeLookState state, Vector3 input, float cameraAngle)
    {
        var rotation = Quaternion.Euler(0f, cameraAngle, 0f);
        return state.CalculateMovementDirection(input, rotation * Vector3.forward, rotation * Vector3.right);
    }

    private static void DirectionRelativeEqual(Vector3 expected, Vector3 actual)
        => Assert.Less(Vector3.Distance(expected, actual), 0.0001f);

    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(360f)]
    [TestCase(720f)]
    public void FreeLookNotChangeFrontEnterRoute(float observeAngle)
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 30f);
        DirectionRelativeEqual(Quaternion.Euler(0f, 30f, 0f) * Vector3.forward,
            Movement(state, Vector3.forward, observeAngle));
        Assert.IsTrue(state.IsolateCameraSteering);
    }

    [Test]
    public void ChangeMovementKeyStillUseEnterTimeOfReferenceDirection()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        DirectionRelativeEqual(Vector3.left, Movement(state, Vector3.left, 180f));
        DirectionRelativeEqual(Vector3.back, Movement(state, Vector3.back, 180f));
        DirectionRelativeEqual((Vector3.forward + Vector3.right).normalized,
            Movement(state, Vector3.forward + Vector3.right, 180f));
    }

    [Test]
    public void ObserveAndRecenteringIntervalStopMovementWillNotLostMovementReference()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.UpdateMovementInput(Vector3.zero);
        Assert.IsTrue(state.MovementReferenceHasLock);
        state.SetKeyBinding(false, true, 180f);
        state.UpdateMovementInput(Vector3.zero);
        Assert.IsTrue(state.MovementReferenceHasLock);
        DirectionRelativeEqual(Vector3.forward, Movement(state, Vector3.forward, 180f));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void DifferentFrameRateLowerAsSpecifiedDurationAlongShortestPathRecentering(int frameRate)
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.SetKeyBinding(false, true, 170f);
        float angle = 170f;
        int frameCount = Mathf.CeilToInt(0.3f * frameRate) + 1;
        for (int sequence = 0; sequence < frameCount; sequence++)
        {
            angle = state.AdvanceRecentering(1f / frameRate, 0.3f, -170f);
            Assert.That(Mathf.DeltaAngle(170f, angle), Is.InRange(-0.001f, 20.001f));
        }
        Assert.AreEqual(FreeLookPhase.Normal, state.Phase);
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(-170f, angle)), 0.001f);
    }

    [Test]
    public void RecenteringFollowCurrentBodyOrientation()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.SetKeyBinding(false, true, 180f);
        state.AdvanceRecentering(0.1f, 0.3f, 0f);
        float result = state.AdvanceRecentering(0.21f, 0.3f, 90f);
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(90f, result)), 0.001f);
    }

    [Test]
    public void RecenteringAfterCompletionContinuousSideMoveNotExtraTurnCorner()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.SetKeyBinding(false, true, 180f);
        state.AdvanceRecentering(0.3f, 0.3f, -90f);
        state.UpdateMovementInput(Vector3.left);
        DirectionRelativeEqual(Vector3.left, Movement(state, Vector3.left, -90f));
        state.UpdateMovementInput(Vector3.zero);
        Assert.IsFalse(state.MovementReferenceHasLock);
        DirectionRelativeEqual(Vector3.back, Movement(state, Vector3.left, -90f));
    }

    [Test]
    public void InterruptRecenteringAndAgainFreeLookAllPreserveNotYetReleaseOfMovementReference()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 20f);
        state.SetKeyBinding(false, true, 180f);
        state.InterruptRecentering();
        Assert.AreEqual(FreeLookPhase.Normal, state.Phase);
        Assert.IsTrue(state.MovementReferenceHasLock);
        state.SetKeyBinding(true, true, 120f);
        Assert.AreEqual(FreeLookPhase.Observe, state.Phase);
        Assert.AreEqual(20f, state.MovementReferenceAngle);
    }

    [Test]
    public void RecenteringMiddleAgainHeldFromCurrentViewRestoreObserve()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.SetKeyBinding(false, true, 180f);
        state.AdvanceRecentering(0.1f, 0.3f, 0f);
        state.SetKeyBinding(true, true, 120f);
        Assert.AreEqual(FreeLookPhase.Observe, state.Phase);
        Assert.AreEqual(0f, state.MovementReferenceAngle);
    }

    [Test]
    public void AimOrInterruptWillCleanupRouteLockAndMustReleaseKeyPressAgain()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, true, 0f);
        state.Reset();
        Assert.AreEqual(FreeLookPhase.Normal, state.Phase);
        Assert.IsFalse(state.MovementReferenceHasLock);
        state.SetKeyBinding(true, true, 180f);
        Assert.AreEqual(FreeLookPhase.Normal, state.Phase);
        state.SetKeyBinding(false, true, 180f);
        state.SetKeyBinding(true, true, 180f);
        Assert.AreEqual(FreeLookPhase.Observe, state.Phase);
        Assert.AreEqual(180f, state.MovementReferenceAngle);
    }

    [Test]
    public void AimOrInputBlockTimePressedWillNotAtReleaseBlockBackAutomaticObserve()
    {
        var state = new FreeLookState();
        state.SetKeyBinding(true, false, 0f);
        state.SetKeyBinding(true, true, 0f);
        Assert.AreEqual(FreeLookPhase.Normal, state.Phase);
        state.SetKeyBinding(false, true, 0f);
        state.SetKeyBinding(true, true, 0f);
        Assert.AreEqual(FreeLookPhase.Observe, state.Phase);
    }
}
