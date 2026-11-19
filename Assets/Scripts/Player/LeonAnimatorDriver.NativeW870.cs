using UnityEngine;

internal enum W870ReloadStage
{
    None,
    Start,
    Loop,
    Finish,
    EmptyFinish
}

public partial class LeonAnimatorDriver
{
    private const int W870StandingReloadAction = 8;
    private const int W870CrouchReloadAction = 12;
    private const int W870ReloadStages = 4;
    private static readonly int[] W870ReloadHashes = CreateW870ReloadHashes();
    internal const int W870HolsterAction = 16;
    internal const int W870EquipAction = 17;
    internal const string W870HolsterName = "NativeW870Holster";
    internal const string W870EquipName = "NativeW870Equip";
    internal const string HolsterSourceWeapon = "HolsterSourceWeapon";
    internal const int W870DrawFrames = 121;
    internal const int W870HolsterFrames = 35;
    internal const int W870RevealFrame = 10;
    internal const int W870GripFrame = 32;
    internal const int W870StowFrame = 22;
    private static readonly int W870HolsterHash = Animator.StringToHash(W870HolsterName);
    private static readonly int W870EquipHash = Animator.StringToHash(W870EquipName);
    private static readonly int HolsterSourceWeaponHash = Animator.StringToHash(HolsterSourceWeapon);

    internal W870ReloadStage W870Stage
    {
        get
        {
            if (!IsValid)
            {
                return W870ReloadStage.None;
            }
            int action = animator.GetInteger(HashWeaponAction) - W870StandingReloadAction;
            if (action < 0 || action >= W870ReloadHashes.Length)
            {
                return W870ReloadStage.None;
            }
            return (W870ReloadStage)(action % W870ReloadStages + 1);
        }
    }

    internal bool NativeW870ReloadActive => W870Stage != W870ReloadStage.None;
    internal bool UsesNativeReloadLayerFade => NativeCqbrReloadActive || NativeW870ReloadActive || NativePistolReloadActive;
    // 动作命令会比状态退出回调早清除一帧。换弹退出前继续保持当前的基础步相。
    internal bool PreservesNativeReloadGait => UsesNativeReloadLayerFade || TryReadNativeCqbrReload(out _)
        || IsNativeW870ReloadPlaying() || IsNativePistolReloadPlaying();
    internal bool SupportsNativeW870Switch => IsValid && HasNativeW870SwitchStates;
    private bool HasNativeW870SwitchStates => animator.HasState(LayerWeaponAction, W870EquipHash)
        && animator.HasState(LayerWeaponAction, W870HolsterHash);
    private bool NativeW870SwitchActive => IsValid
        && animator.GetInteger(HashWeaponAction) is W870HolsterAction or W870EquipAction;
    private bool UsesWeaponEquipLayerFade => NativePistolEquipActive || IsValid && (animator.GetInteger(HashWeaponAction) == W870EquipAction
        || animator.GetInteger(HashWeaponAction) == (int)WeaponActionType.Equip && SupportsNativeW870Switch);
    private bool UsesNativeWeaponLayerFade => UsesNativeReloadLayerFade || NativeW870SwitchActive
        || NativePistolHolsterActive || UsesWeaponEquipLayerFade;

    internal bool IsNativeW870SwitchPlaying()
    {
        if (!IsValid)
        {
            return false;
        }
        int current = animator.GetCurrentAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        int next = animator.GetNextAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        return current == W870EquipHash || current == W870HolsterHash || next == W870EquipHash || next == W870HolsterHash;
    }

    internal static string W870ReloadStateName(W870ReloadStage stage, bool crouched)
    {
        if (crouched)
        {
            return "NativeW870CrouchReload" + stage;
        }
        return "NativeW870Reload" + stage;
    }

    internal static int W870ReloadAction(W870ReloadStage stage, bool crouched)
    {
        int first = W870StandingReloadAction;
        if (crouched)
        {
            first = W870CrouchReloadAction;
        }
        return first + (int)stage - 1;
    }

    private static int[] CreateW870ReloadHashes()
    {
        var hashes = new int[2 * W870ReloadStages];
        for (int stance = 0; stance < 2; stance++)
        {
            for (int stage = 1; stage <= W870ReloadStages; stage++)
            {
                hashes[stance * W870ReloadStages + stage - 1] =
                    Animator.StringToHash(W870ReloadStateName((W870ReloadStage)stage, stance == 1));
            }
        }
        return hashes;
    }

    private int ResolveW870ReloadAction()
    {
        int action = W870ReloadAction(W870ReloadStage.Start, isCrouching);
        if (animator.HasState(LayerWeaponAction, W870ReloadHashes[action - W870StandingReloadAction]))
        {
            return action;
        }
        return (int)WeaponActionType.Reload;
    }

    internal bool IsNativeW870ReloadPlaying()
    {
        if (!IsValid)
        {
            return false;
        }
        return System.Array.IndexOf(W870ReloadHashes, animator.GetCurrentAnimatorStateInfo(LayerWeaponAction).shortNameHash) >= 0
            || System.Array.IndexOf(W870ReloadHashes, animator.GetNextAnimatorStateInfo(LayerWeaponAction).shortNameHash) >= 0;
    }

    internal void AdvanceNativeW870Reload(W870ReloadStage stage)
    {
        bool crouched = animator.GetInteger(HashWeaponAction) >= W870CrouchReloadAction;
        animator.SetInteger(HashWeaponAction, W870ReloadAction(stage, crouched));
    }
}
