using UnityEngine;

internal enum PistolAimPose
{
    None, Raise, Lower, ReverseRaise, ReverseLower, Hold, Shot, LastShot, DryFire
}

public partial class LeonAnimatorDriver
{
    private float nextPistolShotTime;
    internal const string PistolBreathLayerName = "Stationary Pistol ADS";
    internal const string PistolBreathStateName = "NativePistolStationaryBreath";
    private static readonly int PistolBreathState = Animator.StringToHash(PistolBreathStateName);
    internal const string W870BreathLayerName = "Stationary W870 ADS";
    internal const string W870BreathStateName = "NativeW870StationaryBreath";
    private static readonly int W870BreathState = Animator.StringToHash(W870BreathStateName);
    private static readonly int[] PistolAimHashes = CreatePistolAimHashes();
    private PistolAimPose pistolAimPose;
    private float pistolAimRequestedProgress;
    private bool pistolAimFadingOut;
    private struct StandingBreath
    {
        public bool active;
        public float cameraWeight;
    }
    private StandingBreath pistolBreath;
    private StandingBreath w870Breath;

    internal bool TryReadStandingBreath(out float phase, out float weight)
    {
        phase = 0f;
        bool shotgun = currentWeaponType == WeaponType.Shotgun;
        weight = shotgun ? w870Breath.cameraWeight : pistolBreath.cameraWeight;
        if (weight <= 0f || animator == null || (!shotgun && currentWeaponType != WeaponType.Pistol))
            return false;
        int layer = animator.GetLayerIndex(shotgun ? W870BreathLayerName : PistolBreathLayerName);
        if (layer < 0) return false;
        var state = animator.GetCurrentAnimatorStateInfo(layer);
        phase = state.normalizedTime;
        return state.shortNameHash == (shotgun ? W870BreathState : PistolBreathState);
    }

    private void UpdateStationaryGunBreath(string layerName, int state, WeaponType weapon, bool holding, bool firing, ref StandingBreath breath)
    {
        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0)
            return;

        float weight = animator.GetLayerWeight(layer);
        bool aiming = ownerController != null && currentWeaponType == weapon
            && ownerController.currentState == LeonState.Aiming && currentAiming
            && holding && activeFullBodyAction == FullBodyActionType.None;
        bool stationary = aiming && ownerController.IsGrounded && !ownerController.IsInjured && !isCrouching
            && ownerController.MoveInput.sqrMagnitude <= 0.0025f && !animator.IsInTransition(0)
            && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == NativeIdleState;
        bool restart = stationary && weight <= 0f && !breath.active;
        // 开火和泵动接管手臂，站定瞄准继续沿同一呼吸相位前进。
        bool shootingInPlace = breath.active && currentWeaponType == weapon
                && ownerController != null && ownerController.IsAiming && ownerController.IsGrounded
                && (ownerController.currentState == LeonState.Shooting
                    || ownerController.currentState == LeonState.Aiming && (holding || firing))
                && !ownerController.IsInjured && !isCrouching && ownerController.MoveInput.sqrMagnitude <= 0.0025f
                && activeFullBodyAction == FullBodyActionType.None;
        breath.active = stationary || shootingInPlace;
        breath.cameraWeight = Mathf.MoveTowards(breath.cameraWeight, breath.active ? 1f : 0f, Time.deltaTime / 0.15f);
        if (restart)
            animator.Play(state, layer, 0f);

        // 移动与放枪从当前呼吸姿势淡出，武器事务和受击立即接管骨骼。
        bool interrupt = activeFullBodyAction != FullBodyActionType.None || firing
            || currentWeaponType != weapon || ownerController == null
            || ownerController.currentState is LeonState.Reloading or LeonState.Dead or LeonState.QTE
            || animator.GetLayerWeight(LayerWeaponAction) > 0.01f;
        float next = interrupt ? 0f : Mathf.MoveTowards(weight, stationary ? 1f : 0f, Time.deltaTime / 0.15f);
        if (weight != next)
            animator.SetLayerWeight(layer, next);
    }

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
    private bool PistolShotRecovering => pistolAimPose is PistolAimPose.Shot or PistolAimPose.LastShot;
    private bool CanStartPistolShot => pistolAimPose == PistolAimPose.Hold
        || PistolShotRecovering && Time.time >= nextPistolShotTime;
    internal bool NativePistolBlocksFire => currentAiming && UsesNativePistolAim && !CanStartPistolShot;
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
        if (!currentAiming || !UsesNativePistolAim || !CanStartPistolShot)
        {
            return false;
        }
        var pose = PistolAimPose.Shot;
        if (lastRound)
        {
            pose = PistolAimPose.LastShot;
        }
        var handler = ownerController != null ? ownerController.GetComponent<LeonWeaponHandler>() : null;
        var weapon = handler != null ? handler.pistol : null;
        nextPistolShotTime = Time.time + (weapon != null && weapon.data != null ? weapon.GetFireInterval() : 0f);
        PlayNativePistolAim(pose);
        return true;
    }

    internal bool TryPlayNativePistolDryFire()
    {
        if (!currentAiming || !UsesNativePistolAim || !CanStartPistolShot)
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
        if (PistolShotRecovering && !currentAiming && Time.time >= nextPistolShotTime)
        {
            PlayNativePistolAim(PistolAimPose.Lower);
            return;
        }
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
