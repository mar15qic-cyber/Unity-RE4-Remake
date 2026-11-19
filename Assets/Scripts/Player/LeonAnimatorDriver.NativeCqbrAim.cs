using UnityEngine;

public partial class LeonAnimatorDriver
{
    private enum CqbrPose { None, Raise, Lower, ReverseRaise, ReverseLower, AimHold, Shot, LastShot, DryFire, Carry }

    internal const float NativeCqbrShotSeconds = 0.10f;
    internal const string NativeCqbrStandingReload = "NativeCqbrReload";
    internal const string NativeCqbrCrouchReload = "NativeCqbrCrouchReload";
    internal const int NativeCqbrStandingReloadAction = 6;
    internal const int NativeCqbrCrouchReloadAction = 7;
    internal const int NativeCqbrReloadFrames = 230;
    internal const float NativeCqbrMagazineDetachTime = 54f / NativeCqbrReloadFrames;
    internal const float NativeCqbrMagazineCommitTime = 142f / NativeCqbrReloadFrames;
    internal const float NativeCqbrBoltReleaseTime = 182f / NativeCqbrReloadFrames;
    private const float NativeReloadEnterBlendSeconds = 0.20f;
    private const float NativeReloadExitBlendSeconds = 0.24f;
    private static readonly int CqbrStandingReloadHash = Animator.StringToHash(NativeCqbrStandingReload);
    private static readonly int CqbrCrouchReloadHash = Animator.StringToHash(NativeCqbrCrouchReload);
    internal static string NativeCqbrPoseName(string pose) => "NativeCqbr" + pose;
    private static readonly int[] CqbrPoseHashes =
    {
        0,
        Animator.StringToHash(NativeCqbrPoseName("Raise")),
        Animator.StringToHash(NativeCqbrPoseName("Lower")),
        Animator.StringToHash(NativeCqbrPoseName("ReverseRaise")),
        Animator.StringToHash(NativeCqbrPoseName("ReverseLower")),
        Animator.StringToHash(NativeCqbrPoseName("AimHold")),
        Animator.StringToHash(NativeCqbrPoseName("Shot")),
        Animator.StringToHash(NativeCqbrPoseName("LastShot")),
        Animator.StringToHash(NativeCqbrPoseName("DryFire")),
        Animator.StringToHash("NativeCqbrCarryUpper")
    };
    private CqbrPose cqbrPose;
    private float cqbrRequestedProgress;
    private float compatibilityAimLayerWeight;

    private bool CanDriveNativeCqbrAim => SupportsNativeCqbr && currentWeaponType == WeaponType.Rifle
        && !isCrouching && !traversalLandingBasePoseLocked
        && animator.HasState(LayerRifleCrouch, CqbrPoseHashes[(int)CqbrPose.AimHold]);
    internal bool NativeCqbrOwnsAimArms => cqbrPose != CqbrPose.None && cqbrPose != CqbrPose.Carry;
    internal bool NativeCqbrBlocksFire => NativeCqbrReloadActive || TryReadNativeCqbrReload(out _)
        || currentAiming && CanDriveNativeCqbrAim && cqbrPose != CqbrPose.AimHold;

    internal bool NativeCqbrReloadActive => IsValid && animator.GetInteger(HashWeaponAction)
        is NativeCqbrStandingReloadAction or NativeCqbrCrouchReloadAction;

    private int ResolveWeaponActionParameter(WeaponActionType action)
    {
        if (currentWeaponType == WeaponType.Pistol && action == WeaponActionType.Reload)
        {
            return ResolvePistolReloadAction();
        }
        if (currentWeaponType == WeaponType.Pistol && SupportsNativePistolSwitch)
        {
            if (action == WeaponActionType.Holster)
            {
                return PistolHolsterAction;
            }
            if (action == WeaponActionType.Equip)
            {
                return PistolEquipAction;
            }
        }
        if (currentWeaponType == WeaponType.Shotgun)
        {
            if (action == WeaponActionType.Reload)
            {
                return ResolveW870ReloadAction();
            }
            if (SupportsNativeW870Switch)
            {
                if (action == WeaponActionType.Holster)
                {
                    return W870HolsterAction;
                }
                if (action == WeaponActionType.Equip)
                {
                    return W870EquipAction;
                }
            }
        }
        if (action == WeaponActionType.Reload && currentWeaponType == WeaponType.Rifle && SupportsNativeCqbr)
        {
            if (isCrouching && animator.HasState(LayerWeaponAction, CqbrCrouchReloadHash))
            {
                return NativeCqbrCrouchReloadAction;
            }
            if (!isCrouching && animator.HasState(LayerWeaponAction, CqbrStandingReloadHash))
            {
                return NativeCqbrStandingReloadAction;
            }
        }
        return (int)action;
    }

    internal bool TryReadNativeCqbrReload(out float progress)
    {
        progress = 0f;
        if (!IsValid)
        {
            return false;
        }

        var state = animator.GetCurrentAnimatorStateInfo(LayerWeaponAction);
        if (state.shortNameHash != CqbrStandingReloadHash && state.shortNameHash != CqbrCrouchReloadHash)
        {
            state = animator.GetNextAnimatorStateInfo(LayerWeaponAction);
            if (state.shortNameHash != CqbrStandingReloadHash && state.shortNameHash != CqbrCrouchReloadHash)
            {
                return false;
            }
        }
        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }

    internal float NativeCqbrAimCorrectionWeight
    {
        get
        {
            if (!NativeCqbrOwnsAimArms || animator.GetLayerWeight(LayerFullBodyAction) > 0.02f)
            {
                return 0f;
            }
            float weaponWeight = animator.GetLayerWeight(LayerWeaponAction);
            float aimWeight = 1f;
            if (NativeCqbrReloadActive)
            {
                aimWeight -= weaponWeight;
            }
            else if (weaponWeight > 0.02f)
            {
                return 0f;
            }
            float progress = ReadNativeCqbrProgress();
            switch (cqbrPose)
            {
                case CqbrPose.Raise:
                case CqbrPose.ReverseLower:
                    return aimWeight * Mathf.SmoothStep(0f, 1f, progress);
                case CqbrPose.Lower:
                case CqbrPose.ReverseRaise:
                    return aimWeight * Mathf.SmoothStep(1f, 0f, progress);
                default:
                    return aimWeight;
            }
        }
    }

    internal bool ReadNativeCqbrMechanism(out float progress, out bool last, out bool dry)
    {
        last = cqbrPose == CqbrPose.LastShot;
        dry = cqbrPose == CqbrPose.DryFire;
        progress = 0f;
        if (cqbrPose != CqbrPose.Shot && !last && !dry)
        {
            return false;
        }
        progress = ReadNativeCqbrProgress();
        return true;
    }

    internal void TryPlayNativeCqbrDryFire()
    {
        if (currentAiming && CanDriveNativeCqbrAim && cqbrPose == CqbrPose.AimHold)
        {
            PlayNativeCqbrPose(CqbrPose.DryFire);
        }
    }

    private bool TryPlayNativeCqbrShot(bool lastRound)
    {
        if (!currentAiming || !CanDriveNativeCqbrAim || cqbrPose != CqbrPose.AimHold)
        {
            return false;
        }
        var pose = CqbrPose.Shot;
        if (lastRound)
        {
            pose = CqbrPose.LastShot;
        }
        PlayNativeCqbrPose(pose);
        return true;
    }

    private void UpdateNativeCqbrAim()
    {
        if (!CanDriveNativeCqbrAim || activeFullBodyAction != FullBodyActionType.None)
        {
            EndNativeCqbrAim();
            return;
        }
        if (cqbrPose == CqbrPose.None || cqbrPose == CqbrPose.Carry)
        {
            if (currentAiming)
            {
                PlayNativeCqbrPose(CqbrPose.Raise);
            }
            else if (animator.HasState(LayerRifleCrouch, CqbrPoseHashes[(int)CqbrPose.Carry])
                && (cqbrPose != CqbrPose.Carry || animator.GetLayerWeight(LayerRifleCrouch) < 0.99f
                    || blendLongGunCrouchLayer && longGunCrouchLayerTarget < 0.99f
                    || animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch).shortNameHash != CqbrPoseHashes[(int)CqbrPose.Carry]))
            {
                // 方向步态只提供位移与身体姿势，双手由当前长枪的持枪片段接管。
                PlayNativeCqbrPose(CqbrPose.Carry);
            }
            return;
        }

        float progress = ReadNativeCqbrProgress();
        bool raising = cqbrPose is CqbrPose.Raise or CqbrPose.ReverseLower;
        bool lowering = cqbrPose is CqbrPose.Lower or CqbrPose.ReverseRaise;
        if (progress < 0.999f)
        {
            if (raising && !currentAiming || lowering && currentAiming)
            {
                ReverseNativeCqbrPose(progress);
            }
            else if (cqbrPose == CqbrPose.AimHold && !currentAiming)
            {
                PlayNativeCqbrPose(CqbrPose.Lower);
            }
            return;
        }
        if (currentAiming && lowering)
        {
            PlayNativeCqbrPose(CqbrPose.Raise);
        }
        else if (currentAiming && cqbrPose != CqbrPose.AimHold)
        {
            PlayNativeCqbrPose(CqbrPose.AimHold);
        }
        else if (!currentAiming && lowering)
        {
            if (!IsNativeCqbrCarrying || !animator.IsInTransition(0))
            {
                EndNativeCqbrAim();
            }
        }
        else if (!currentAiming)
        {
            PlayNativeCqbrPose(CqbrPose.Lower);
        }
    }

    private void ReverseNativeCqbrPose(float progress)
    {
        var target = CqbrPose.ReverseRaise;
        switch (cqbrPose)
        {
            case CqbrPose.ReverseRaise:
                target = CqbrPose.Raise;
                break;
            case CqbrPose.Lower:
                target = CqbrPose.ReverseLower;
                break;
            case CqbrPose.ReverseLower:
                target = CqbrPose.Lower;
                break;
        }
        PlayNativeCqbrPose(target, 1f - progress);
    }

    private void PlayNativeCqbrPose(CqbrPose pose, float progress = 0f)
    {
        cqbrPose = pose;
        cqbrRequestedProgress = progress;
        longGunCrouchLayerExitPending = false;
        blendLongGunCrouchLayer = false;
        animator.SetLayerWeight(LayerAim, 0f);
        animator.SetLayerWeight(LayerRifleCrouch, 1f);
        animator.Play(CqbrPoseHashes[(int)pose], LayerRifleCrouch, progress);
    }

    private void EndNativeCqbrAim()
    {
        if (cqbrPose == CqbrPose.None)
        {
            return;
        }
        cqbrPose = CqbrPose.None;
        animator.Play(HashRifleCrouchEmpty, LayerRifleCrouch, 0f);
        if (!isCrouching || SupportsNativeLongGunCrouch)
        {
            animator.SetLayerWeight(LayerRifleCrouch, 0f);
        }
        SetAimLayerWeight(compatibilityAimLayerWeight);
    }

    private float ReadNativeCqbrProgress()
    {
        var state = animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch);
        if (state.shortNameHash != CqbrPoseHashes[(int)cqbrPose])
        {
            return cqbrRequestedProgress;
        }
        return Mathf.Clamp01(state.normalizedTime);
    }
}
