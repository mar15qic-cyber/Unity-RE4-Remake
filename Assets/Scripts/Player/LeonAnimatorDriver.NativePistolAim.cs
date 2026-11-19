using UnityEngine;

internal enum PistolAimPose
{
    None, Raise, Lower, ReverseRaise, ReverseLower, Hold, Shot, LastShot, DryFire
}

public partial class LeonAnimatorDriver
{
    internal const int PistolFireFrames = 50;
    private static readonly int[] PistolAimHashes = CreatePistolAimHashes();
    private PistolAimPose pistolAimPose;
    private float pistolAimRequestedProgress;
    private bool pistolAimFadingOut;

    internal static string PistolAimStateName(PistolAimPose pose) => "NativePistolAim" + pose;

    private static int[] CreatePistolAimHashes()
    {
        var hashes = new int[(int)PistolAimPose.DryFire + 1];
        for (int index = 1; index < hashes.Length; index++)
        {
            hashes[index] = Animator.StringToHash(PistolAimStateName((PistolAimPose)index));
        }
        return hashes;
    }

    internal bool UsesNativePistolAim => IsValid && currentWeaponType == WeaponType.Pistol && !isCrouching
        && !traversalLandingBasePoseLocked && activeFullBodyAction == FullBodyActionType.None
        && animator.HasState(LayerRifleCrouch, PistolAimHashes[(int)PistolAimPose.Hold]);
    internal bool NativePistolOwnsAimArms => pistolAimPose != PistolAimPose.None;
    internal bool NativePistolFireCycleActive => pistolAimPose is PistolAimPose.Shot or PistolAimPose.LastShot or PistolAimPose.DryFire;
    private bool NativePistolAimReady => pistolAimPose == PistolAimPose.Hold || NativePistolFireCycleActive;
    internal bool NativePistolBlocksFire => currentAiming && UsesNativePistolAim
        && (!NativePistolAimReady || NativePistolFireCycleActive);
    internal bool CanShowAimReticle => !UsesNativePistolAim || NativePistolAimReady;

    internal float NativePistolAimCorrectionWeight
    {
        get
        {
            if (!NativePistolOwnsAimArms || pistolAimFadingOut || animator.GetLayerWeight(LayerFullBodyAction) > 0.02f)
            {
                return 0f;
            }
            float weight = animator.GetLayerWeight(LayerRifleCrouch) * (1f - animator.GetLayerWeight(LayerWeaponAction));
            switch (pistolAimPose)
            {
                case PistolAimPose.Raise:
                case PistolAimPose.ReverseLower:
                    return weight * Mathf.SmoothStep(0f, 1f, ReadNativePistolAimProgress());
                case PistolAimPose.Lower:
                case PistolAimPose.ReverseRaise:
                    return weight * Mathf.SmoothStep(1f, 0f, ReadNativePistolAimProgress());
                default:
                    return weight;
            }
        }
    }

    private bool TryPlayNativePistolShot(bool lastRound)
    {
        if (!currentAiming || !UsesNativePistolAim || pistolAimPose != PistolAimPose.Hold)
        {
            return false;
        }
        var pose = PistolAimPose.Shot;
        if (lastRound)
        {
            pose = PistolAimPose.LastShot;
        }
        PlayNativePistolAim(pose, 1f / PistolFireFrames);
        return true;
    }

    internal bool TryPlayNativePistolDryFire()
    {
        if (!currentAiming || !UsesNativePistolAim || pistolAimPose != PistolAimPose.Hold)
        {
            return false;
        }
        PlayNativePistolAim(PistolAimPose.DryFire);
        return true;
    }

    private void UpdateNativePistolAim()
    {
        if (!UsesNativePistolAim)
        {
            EndNativePistolAim();
            return;
        }
        if (pistolAimFadingOut)
        {
            if (currentAiming)
            {
                pistolAimFadingOut = false;
                BlendLongGunCrouchLayerTo(1f);
                ReverseNativePistolAim(1f);
            }
            else if (!blendLongGunCrouchLayer)
            {
                EndNativePistolAim();
            }
            return;
        }
        if (!NativePistolOwnsAimArms)
        {
            if (currentAiming)
            {
                BlendLongGunCrouchLayerTo(1f);
                PlayNativePistolAim(PistolAimPose.Raise);
            }
            return;
        }
        if (pistolAimPose == PistolAimPose.Hold)
        {
            if (!currentAiming)
            {
                PlayNativePistolAim(PistolAimPose.Lower);
            }
            return;
        }

        float progress = ReadNativePistolAimProgress();
        bool raising = pistolAimPose is PistolAimPose.Raise or PistolAimPose.ReverseLower;
        bool lowering = pistolAimPose is PistolAimPose.Lower or PistolAimPose.ReverseRaise;
        if (progress < 0.999f)
        {
            if (raising && !currentAiming || lowering && currentAiming)
            {
                ReverseNativePistolAim(progress);
            }
            return;
        }
        if (currentAiming)
        {
            PlayNativePistolAim(PistolAimPose.Hold);
        }
        else if (lowering)
        {
            pistolAimFadingOut = true;
            BlendLongGunCrouchLayerTo(0f);
        }
        else
        {
            PlayNativePistolAim(PistolAimPose.Lower);
        }
    }

    private void ReverseNativePistolAim(float progress)
    {
        var pose = PistolAimPose.ReverseRaise;
        switch (pistolAimPose)
        {
            case PistolAimPose.ReverseRaise:
                pose = PistolAimPose.Raise;
                break;
            case PistolAimPose.Lower:
                pose = PistolAimPose.ReverseLower;
                break;
            case PistolAimPose.ReverseLower:
                pose = PistolAimPose.Lower;
                break;
        }
        PlayNativePistolAim(pose, 1f - progress);
    }

    private void PlayNativePistolAim(PistolAimPose pose, float progress = 0f)
    {
        pistolAimPose = pose;
        pistolAimRequestedProgress = progress;
        animator.SetLayerWeight(LayerAim, 0f);
        animator.Play(PistolAimHashes[(int)pose], LayerRifleCrouch, progress);
    }

    private void EndNativePistolAim()
    {
        if (!NativePistolOwnsAimArms)
        {
            return;
        }
        pistolAimPose = PistolAimPose.None;
        pistolAimFadingOut = false;
        blendLongGunCrouchLayer = false;
        animator.SetLayerWeight(LayerRifleCrouch, 0f);
        animator.Play(HashRifleCrouchEmpty, LayerRifleCrouch, 0f);
        SetAimLayerWeight(compatibilityAimLayerWeight);
    }

    private float ReadNativePistolAimProgress()
    {
        var state = animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch);
        if (state.shortNameHash != PistolAimHashes[(int)pistolAimPose])
        {
            return pistolAimRequestedProgress;
        }
        return Mathf.Clamp01(state.normalizedTime);
    }
}
