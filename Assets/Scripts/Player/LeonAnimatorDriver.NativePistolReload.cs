using UnityEngine;

public partial class LeonAnimatorDriver
{
    private const int FirstNativePistolReload = 20;
    internal const int PistolReloadFrames = 195;
    internal const int PistolMagazineCommitFrame = 99;
    internal const int PistolEmptyMagazineDetachFrame = 27;
    internal const int PistolTacticalMagazineDetachFrame = 53;
    internal const int PistolSlideReleaseFrame = 139;
    private static readonly int[] PistolReloadHashes =
    {
        Animator.StringToHash(PistolReloadStateName(false, false)),
        Animator.StringToHash(PistolReloadStateName(true, false)),
        Animator.StringToHash(PistolReloadStateName(false, true)),
        Animator.StringToHash(PistolReloadStateName(true, true))
    };

    internal static string PistolReloadStateName(bool empty, bool crouched)
    {
        string name = "NativePistol";
        if (crouched)
        {
            name += "Crouch";
        }
        if (empty)
        {
            return name + "EmptyReload";
        }
        return name + "TacticalReload";
    }

    internal static int PistolReloadAction(bool empty, bool crouched)
    {
        int action = FirstNativePistolReload;
        if (crouched)
        {
            action += 2;
        }
        if (empty)
        {
            action++;
        }
        return action;
    }

    private int NativePistolReloadIndex
    {
        get
        {
            if (!IsValid)
            {
                return -1;
            }
            int index = animator.GetInteger(HashWeaponAction) - FirstNativePistolReload;
            if (index < 0 || index >= PistolReloadHashes.Length)
            {
                return -1;
            }
            return index;
        }
    }

    internal bool NativePistolReloadActive => NativePistolReloadIndex >= 0;
    internal bool NativePistolEmptyReload => NativePistolReloadIndex is 1 or 3;

    private bool IsNativePistolReloadPlaying()
    {
        if (!IsValid)
        {
            return false;
        }
        int current = animator.GetCurrentAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        int next = animator.GetNextAnimatorStateInfo(LayerWeaponAction).shortNameHash;
        return System.Array.IndexOf(PistolReloadHashes, current) >= 0 || System.Array.IndexOf(PistolReloadHashes, next) >= 0;
    }

    private int ResolvePistolReloadAction()
    {
        bool empty = ownerController != null && ownerController.GetCurrentAmmo() == 0;
        int action = PistolReloadAction(empty, isCrouching);
        if (animator.HasState(LayerWeaponAction, PistolReloadHashes[action - FirstNativePistolReload]))
        {
            return action;
        }
        return (int)WeaponActionType.Reload;
    }

    internal bool TryReadNativePistolReload(out float progress)
    {
        progress = 0f;
        int index = NativePistolReloadIndex;
        if (index < 0)
        {
            return false;
        }

        int hash = PistolReloadHashes[index];
        var state = animator.GetCurrentAnimatorStateInfo(LayerWeaponAction);
        if (state.shortNameHash != hash)
        {
            state = animator.GetNextAnimatorStateInfo(LayerWeaponAction);
            if (state.shortNameHash != hash)
            {
                return false;
            }
        }
        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }
}
