using UnityEngine;

internal enum W870Pose
{
    None, Carry, EmptyCarry, Raise, Lower, ReverseRaise, ReverseLower,
    Aim, EmptyAim, Shot, LastShot, Pump, DryFire
}

public partial class LeonAnimatorDriver
{
    private static readonly int[] W870PoseHashes = CreateW870PoseHashes();
    private static readonly int W870CrouchEnter = Animator.StringToHash("Rifle Crouch Enter");
    private W870Pose w870Pose;
    private float w870RequestedProgress;
    private bool w870CaseMarkerCrossed;

    internal static string W870PoseName(W870Pose pose) => "NativeW870" + pose;

    private static int[] CreateW870PoseHashes()
    {
        var hashes = new int[(int)W870Pose.DryFire + 1];
        for (int i = 1; i < hashes.Length; i++)
        {
            hashes[i] = Animator.StringToHash(W870PoseName((W870Pose)i));
        }
        return hashes;
    }

    internal bool SupportsNativeW870Aim => IsValid && currentWeaponType == WeaponType.Shotgun
        && animator.HasState(LayerRifleCrouch, W870PoseHashes[(int)W870Pose.Aim]);
    private bool CanDriveNativeW870Aim => SupportsNativeW870Aim && !isCrouching
        && !longGunCrouchLayerExitPending && !traversalLandingBasePoseLocked
        && activeFullBodyAction == FullBodyActionType.None;

    internal bool NativeW870OwnsAimArms => w870Pose != W870Pose.None;
    internal bool NativeW870ShotActive => w870Pose is W870Pose.Shot or W870Pose.LastShot;
    internal bool NativeW870FireCycleActive => NativeW870ShotActive || w870Pose == W870Pose.Pump;
    internal bool TryReadW870ShotPumpPhase(out float phase)
    {
        phase = 0f;
        if (!NativeW870FireCycleActive) return false;
        // 0500 / 0510 have 49 and 51 source frames at the same rate.
        float progress = ReadNativeW870PoseProgress();
        phase = w870Pose == W870Pose.Pump ? 0.49f + 0.51f * progress : 0.49f * progress;
        return true;
    }
    private bool IsW870AimPose => w870Pose is W870Pose.Aim or W870Pose.EmptyAim;
    internal bool NativeW870BlocksFire => NativeW870FireCycleActive
        || currentAiming && SupportsNativeW870Aim && !IsW870AimPose && !isCrouching;

    internal float NativeW870AimCorrectionWeight
    {
        get
        {
            if (!NativeW870OwnsAimArms || animator.GetLayerWeight(LayerFullBodyAction) > 0.02f)
            {
                return 0f;
            }
            float weight = animator.GetLayerWeight(LayerRifleCrouch) * (1f - animator.GetLayerWeight(LayerWeaponAction));
            switch (w870Pose)
            {
                case W870Pose.Carry:
                case W870Pose.EmptyCarry:
                    return 0f;
                case W870Pose.Raise:
                case W870Pose.ReverseLower:
                    return weight * Mathf.SmoothStep(0f, 1f, ReadNativeW870PoseProgress());
                case W870Pose.Lower:
                case W870Pose.ReverseRaise:
                    return weight * Mathf.SmoothStep(1f, 0f, ReadNativeW870PoseProgress());
                default:
                    return weight;
            }
        }
    }

    private bool TryPlayNativeW870Shot(bool lastRound)
    {
        if (!CanDriveNativeW870Aim || !currentAiming || !IsW870AimPose)
        {
            return false;
        }
        var pose = W870Pose.Shot;
        if (lastRound)
        {
            pose = W870Pose.LastShot;
        }
        PlayNativeW870Pose(pose);
        return true;
    }

    internal bool TryPlayNativeW870DryFire()
    {
        if (!CanDriveNativeW870Aim || !currentAiming || !IsW870AimPose)
        {
            return false;
        }
        PlayNativeW870Pose(W870Pose.DryFire);
        return true;
    }

    private void UpdateNativeGunPose()
    {
        if (currentWeaponType != WeaponType.Pistol)
        {
            EndNativePistolAim();
        }
        if (currentWeaponType == WeaponType.Shotgun)
        {
            UpdateNativeCqbrAim();
            UpdateNativeW870Aim();
        }
        else
        {
            UpdateNativeW870Aim();
            UpdateNativeCqbrAim();
        }
        if (currentWeaponType == WeaponType.Pistol)
        {
            UpdateNativePistolAim();
        }
        UpdateNativeKnifeCarry();
    }

    private void UpdateNativeW870Aim()
    {
        if (!CanDriveNativeW870Aim)
        {
            EndNativeW870Pose();
            return;
        }

        bool empty = ownerController != null && ownerController.GetCurrentAmmo() == 0;
        var carry = W870Pose.Carry;
        var hold = W870Pose.Aim;
        if (empty)
        {
            carry = W870Pose.EmptyCarry;
            hold = W870Pose.EmptyAim;
        }

        if (w870Pose is W870Pose.None or W870Pose.Carry or W870Pose.EmptyCarry)
        {
            if (currentAiming)
            {
                PlayNativeW870Pose(W870Pose.Raise);
            }
            else if (w870Pose != carry || animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch).shortNameHash != W870PoseHashes[(int)carry]
                || blendLongGunCrouchLayer && longGunCrouchLayerTarget < 0.99f
                || !blendLongGunCrouchLayer && animator.GetLayerWeight(LayerRifleCrouch) < 0.99f)
            {
                PlayNativeW870Pose(carry);
            }
            return;
        }
        if (IsW870AimPose)
        {
            if (!currentAiming)
            {
                PlayNativeW870Pose(W870Pose.Lower);
            }
            else if (w870Pose != hold)
            {
                PlayNativeW870Pose(hold);
            }
            return;
        }

        float progress = ReadNativeW870PoseProgress();
        if (w870Pose == W870Pose.Pump && !w870CaseMarkerCrossed && progress >= 5f / 51f)
        {
            w870CaseMarkerCrossed = true;
            if (ownerController != null && ownerController.TryGetComponent<LeonWeaponHandler>(out var weapons))
            {
                weapons.GetWeapon(WeaponType.Shotgun)?.PlayChapterOnePumpCase();
            }
        }
        bool raising = w870Pose is W870Pose.Raise or W870Pose.ReverseLower;
        bool lowering = w870Pose is W870Pose.Lower or W870Pose.ReverseRaise;
        if (progress < 0.999f)
        {
            if (raising && !currentAiming || lowering && currentAiming)
            {
                ReverseNativeW870Pose(progress);
            }
            return;
        }
        if (w870Pose is W870Pose.Shot or W870Pose.LastShot)
        {
            PlayNativeW870Pose(W870Pose.Pump);
        }
        else if (lowering && !currentAiming)
        {
            PlayNativeW870Pose(carry);
        }
        else if (lowering)
        {
            PlayNativeW870Pose(W870Pose.Raise);
        }
        else if (currentAiming)
        {
            PlayNativeW870Pose(hold);
        }
        else
        {
            PlayNativeW870Pose(W870Pose.Lower);
        }
    }

    private void ReverseNativeW870Pose(float progress)
    {
        var reverse = W870Pose.ReverseRaise;
        switch (w870Pose)
        {
            case W870Pose.ReverseRaise:
                reverse = W870Pose.Raise;
                break;
            case W870Pose.Lower:
                reverse = W870Pose.ReverseLower;
                break;
            case W870Pose.ReverseLower:
                reverse = W870Pose.Lower;
                break;
        }
        PlayNativeW870Pose(reverse, 1f - progress);
    }

    private void PlayNativeW870Pose(W870Pose pose, float progress = 0f)
    {
        if (!NativeW870OwnsAimArms || longGunCrouchLayerTarget < 0.99f
            || !blendLongGunCrouchLayer && animator.GetLayerWeight(LayerRifleCrouch) < 0.99f)
        {
            BlendLongGunCrouchLayerTo(1f);
        }
        w870Pose = pose;
        if (pose == W870Pose.Pump)
        {
            w870CaseMarkerCrossed = false;
        }
        w870RequestedProgress = progress;
        animator.SetLayerWeight(LayerAim, 0f);
        animator.Play(W870PoseHashes[(int)pose], LayerRifleCrouch, progress);
    }

    private void EndNativeW870Pose()
    {
        if (!NativeW870OwnsAimArms)
        {
            return;
        }
        if (NativeW870FireCycleActive && ownerController != null
            && ownerController.TryGetComponent<LeonWeaponHandler>(out var weapons))
        {
            weapons.GetWeapon(WeaponType.Shotgun)?.CancelChapterOnePumpCase();
        }
        w870Pose = W870Pose.None;
        if (isCrouching && currentWeaponType == WeaponType.Shotgun && !SupportsNativeLongGunCrouch
            && animator.HasState(LayerRifleCrouch, W870CrouchEnter))
        {
            animator.CrossFadeInFixedTime(W870CrouchEnter, 0.16f, LayerRifleCrouch, 0f);
        }
        else
        {
            animator.SetLayerWeight(LayerRifleCrouch, 0f);
            animator.Play(HashRifleCrouchEmpty, LayerRifleCrouch, 0f);
        }
        SetAimLayerWeight(compatibilityAimLayerWeight);
    }

    private float ReadNativeW870PoseProgress()
    {
        var state = animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch);
        if (state.shortNameHash != W870PoseHashes[(int)w870Pose])
        {
            return w870RequestedProgress;
        }
        return Mathf.Clamp01(state.normalizedTime);
    }
}
