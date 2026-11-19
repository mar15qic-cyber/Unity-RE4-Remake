using UnityEngine;

public partial class LeonAnimatorDriver
{
    internal const int PistolHolsterAction = 18;
    internal const int PistolEquipAction = 19;
    internal const string PistolHolsterState = "NativePistolHolster";
    internal const string PistolEquipState = "NativePistolEquip";
    internal const int PistolDrawFrames = 50;
    internal const int PistolHolsterFrames = 35;
    internal const int PistolDrawContactFrame = 20;
    internal const int PistolRevealFrame = 21;
    internal const int PistolStowFrame = 18;
    private static readonly int PistolHolsterHash = Animator.StringToHash(PistolHolsterState);
    private static readonly int PistolEquipHash = Animator.StringToHash(PistolEquipState);

    internal bool SupportsNativePistolSwitch => IsValid && animator.HasState(LayerWeaponAction, PistolEquipHash)
        && animator.HasState(LayerWeaponAction, PistolHolsterHash);
    internal bool NativePistolHolsterActive => IsValid && animator.GetInteger(HashWeaponAction) == PistolHolsterAction;
    internal bool NativePistolEquipActive => IsValid && animator.GetInteger(HashWeaponAction) == PistolEquipAction;

    internal bool IsNativePistolSwitchPlaying()
    {
        if (!IsValid)
        {
            return false;
        }

        int current = animator.GetCurrentAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        int next = animator.GetNextAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        return current == PistolEquipHash || current == PistolHolsterHash || next == PistolEquipHash || next == PistolHolsterHash;
    }
}
