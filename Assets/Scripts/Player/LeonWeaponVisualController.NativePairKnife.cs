using UnityEngine;

public partial class LeonWeaponVisualController
{
    [Header("原版配对小刀")]
    [Tooltip("原版 wp5000，挂在原动画 RE4_WeaponSocket 下，使用提取的握持变换。")]
    public Transform nativePairKnife;
    private bool nativePairKnifeActive;

    internal bool BeginNativePairKnife()
    {
        if (nativePairKnife == null)
        {
            return false;
        }
        EndAssassinationKnifePose();
        EndCombatKnifeGripPose();
        SetKnifeModeVisible(true);
        nativePairKnifeActive = true;
        SetKnifeVisible(false);
        nativePairKnife.gameObject.SetActive(true);
        UpdateSpareKnifeAppearance(nativePairKnife, true, new Vector3(0f, .004f, .074f), new Vector3(0f, .0041f, .2673f));
        return true;
    }

    internal void EndNativePairKnife()
    {
        if (!nativePairKnifeActive)
        {
            return;
        }
        nativePairKnifeActive = false;
        UpdateSpareKnifeAppearance(null, false, Vector3.zero, Vector3.zero);
        if (nativePairKnife != null)
        {
            nativePairKnife.gameObject.SetActive(false);
        }
    }

    internal bool TryReadNativePairBlade(out Vector3 bladeBase, out Vector3 bladeTip)
    {
        bladeBase = default;
        bladeTip = default;
        if (!nativePairKnifeActive || nativePairKnife == null || !nativePairKnife.gameObject.activeInHierarchy)
        {
            return false;
        }
        // 与已有原版动作验收器相同的 wp5000 刀身几何坐标。
        bladeBase = nativePairKnife.TransformPoint(new Vector3(0f, .004f, .074f));
        bladeTip = nativePairKnife.TransformPoint(new Vector3(0f, .0041f, .2673f));
        return true;
    }
}
