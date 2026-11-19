using NUnit.Framework;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp-Editor","架构玩家武器测试")]

public sealed class ArchitecturePlayerWeaponTests
{
    [Test]
    public void MotionHandoffRevokePreviousTokenAndDeathCannotBeingNormalMovementPreempt()
    {
        var objectValue = new GameObject("玩家运动交接测试");
        try
        {
            var executor = objectValue.AddComponent<LeonActionMotionDriver>();
            InitializeExecutor(executor);
            var normal = executor.AuthorizeMotion(MotionSource.NormalMovement, 0);
            var vault = executor.AuthorizeMotion(MotionSource.Traversal, 3);
            Assert.IsFalse(executor.CommitPosition(normal, Vector3.one));
            Assert.IsTrue(executor.CommitPosition(vault, Vector3.one));
            executor.ReleaseMotion(vault);
            var subsequent = executor.AuthorizeMotion(MotionSource.Traversal, 4);
            executor.ReleaseMotion(vault);
            Assert.IsTrue(executor.TokenValid(subsequent));
            executor.BeginDeathMotion();
            Assert.IsFalse(executor.CommitPosition(subsequent, Vector3.zero));
            Assert.IsFalse(executor.AuthorizeMotion(MotionSource.NormalMovement, 0).Valid);
            Assert.AreEqual(MotionSource.Death, executor.CurrentMotionToken.Source);
        }
        finally { Object.DestroyImmediate(objectValue); }
    }
    [Test]
    public void PlayerModuleBoundaryNotAllowExternalWriteStateOrPairedActionDisplacement()
    {
        var failure = new System.Collections.Generic.List<string>();
        PlayerModuleBoundaryValidator.Validate(failure);
        Assert.IsEmpty(failure, string.Join("\n", failure));
    }

    [Test]
    public void ConflictRulesHoldMeleeCanInterruptAndTraversalRejectHitReaction()
    {
        Assert.AreEqual(RejectionReason.None, PlayerActionConflictRules.Check(ActionKind.HitReaction, false, true, true, true, false, false));
        Assert.AreEqual(RejectionReason.ActionConflict, PlayerActionConflictRules.Check(ActionKind.HitReaction, false, false, true, false, false, false));
        Assert.AreEqual(RejectionReason.ActionConflict, PlayerActionConflictRules.Check(ActionKind.Reload, false, false, false, false, true, false));
        Assert.AreEqual(RejectionReason.HasDeath, PlayerActionConflictRules.Check(ActionKind.PairedActionAction, true, false, false, false, false, false));
    }
    [Test]
    public void FireOnlyDeductOnceAndKnifeWhiffStillOccupancyCooldown()
    {
        var module = new WeaponRuntimeModule();
        module.InitializeWeapon(1, 3);
        Assert.AreEqual(RejectionReason.None, module.TryFire(1, false, 1f, .5f));
        Assert.AreEqual(RejectionReason.CooldownUnfinished, module.TryFire(1, false, 1.1f, .5f));
        Assert.AreEqual(2, module.Query(1).magazine);
        Assert.AreEqual(RejectionReason.None, module.TryFire(1, true, 2f, .5f));
        Assert.AreEqual(2, module.Query(1).magazine);
        Assert.AreEqual(RejectionReason.CooldownUnfinished, module.TryFire(1, true, 2.1f, .5f));
    }

    [Test]
    public void CancelBackOfPreviousReloadMustNotDeductStockOrOverrideNewTransaction()
    {
        var module = new WeaponRuntimeModule();
        module.InitializeWeapon(1, 0);
        int reserveAmmo = 10;
        Assert.IsTrue(module.BeginReload(1, 6, 10));
        module.CancelReload(1);
        Assert.IsTrue(module.BeginReload(1, 6, 11));
        Assert.AreEqual(0, module.CommitReload(1, 6, 10, reserveAmmo, false, count => reserveAmmo -= count));
        Assert.AreEqual(10, reserveAmmo);
        Assert.AreEqual(6, module.CommitReload(1, 6, 11, reserveAmmo, false, count => reserveAmmo -= count));
        Assert.AreEqual(0, module.CommitReload(1, 6, 11, reserveAmmo, false, count => reserveAmmo -= count));
        Assert.AreEqual(10, reserveAmmo + module.Query(1).magazine);
    }

    [Test]
    public void PerShellReloadInterruptHoldAmmoConservation()
    {
        var module = new WeaponRuntimeModule();
        module.InitializeWeapon(1, 1);
        int reserveAmmo = 3;
        module.BeginReload(1, 6, 8);
        Assert.AreEqual(RejectionReason.ActionConflict, module.TryFire(1, false, 1f, .1f));
        Assert.AreEqual(1, module.CommitReload(1, 6, 8, reserveAmmo, true, count => reserveAmmo -= count));
        module.CancelReload(1);
        Assert.AreEqual(0, module.CommitReload(1, 6, 8, reserveAmmo, true, count => reserveAmmo -= count));
        Assert.AreEqual(4, reserveAmmo + module.Query(1).magazine);
        Assert.IsFalse(module.Query(1).isReload);
    }

    [Test]
    public void DifferentWeaponOfMagazineAndCooldownMutuallyIndependent()
    {
        var module = new WeaponRuntimeModule();
        module.InitializeWeapon(1, 1);
        module.InitializeWeapon(2, 5);
        module.TryFire(1, false, 1, 1);
        Assert.AreEqual(RejectionReason.AssetInsufficient, module.TryFire(1, false, 2, 1));
        Assert.AreEqual(RejectionReason.None, module.TryFire(2, false, 1, 1));
        Assert.AreEqual(4, module.Query(2).magazine);
        Assert.AreEqual(RejectionReason.NotYetReady, module.CheckFire(99, false, 1, 1));
    }

    [Test]
    public void StateSnapshotReadOnlyAndDeathProjectionPriority()
    {
        var snapshot = new PlayerActionSnapshot(LeonState.Walking, PlayerWeaponPhase.Shoot, PlayerFullBodyPhase.EndEnd, 2, 3);
        Assert.AreEqual(LeonState.Dead, snapshot.CompatibleState);
        Assert.IsNull(typeof(LeonController).GetProperty("currentState").SetMethod);
        Assert.IsNull(typeof(LeonController).GetProperty("currentWeapon").SetMethod);
        Assert.IsNull(typeof(WeaponBase).GetProperty("currentAmmo").SetMethod);
        Assert.IsNull(typeof(WeaponBase).GetProperty("isReloading").SetMethod);
    }

    [TestCase(HitboxType.Head, 2f)]
    [TestCase(HitboxType.Chest, 1f)]
    [TestCase(HitboxType.Pelvis, 1f)]
    [TestCase(HitboxType.LeftArm, .8f)]
    [TestCase(HitboxType.RightArm, .8f)]
    [TestCase(HitboxType.LeftLeg, .8f)]
    [TestCase(HitboxType.RightLeg, .8f)]
    public void BodyPartMultiplierHoldOriginalValue(HitboxType bodyPart, float multiplier)
    {
        Assert.AreEqual(multiplier, DamageResolutionModule.QueryBodyPartMultiplier(bodyPart));
    }

    [Test]
    public void PairedActionMotionTokenRejectPreviousSessionAndCanRepeatedRelease()
    {
        var objectValue = new GameObject("配对运动测试");
        try
        {
            var executor = objectValue.AddComponent<LeonActionMotionDriver>();
            InitializeExecutor(executor);
            Assert.IsTrue(executor.RequestPairedActionMotion(1));
            executor.ReleasePairedActionMotion(1);
            Assert.IsTrue(executor.RequestPairedActionMotion(2));
            executor.ReleasePairedActionMotion(1);
            Assert.IsFalse(executor.CommitPairedActionPose(1, Vector3.one, Quaternion.identity, true));
            Assert.AreEqual(2, executor.CurrentPairedActionSession);
            Assert.IsTrue(executor.CommitPairedActionPose(2, Vector3.one, Quaternion.identity, true));
            executor.ReleasePairedActionMotion(2);
            executor.ReleasePairedActionMotion(2);
            Assert.AreEqual(0, executor.CurrentPairedActionSession);
        }
        finally { Object.DestroyImmediate(objectValue); }
    }
    private static void InitializeExecutor(LeonActionMotionDriver executor)
    {
        // 编辑器测试不会自动执行普通运行组件的启用回调，显式推进租用生命周期。
        typeof(LeonActionMotionDriver).GetMethod("OnEnable",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(executor, null);
    }
}
