using UnityEngine;

/// <summary>在新村庄游戏相机渲染时限制高画质的实时阴影开销。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class VillageCameraShadowBudget : MonoBehaviour
{
    private RenderScope shadowScope;

    private void OnPreCull()
    {
        shadowScope.TryApply();
    }

    private void OnPostRender()
    {
        shadowScope.Restore();
    }

    private void OnDisable()
    {
        shadowScope.Restore();
    }

    /// <summary>为一次相机绘制临时覆盖内置管线的全局阴影值。</summary>
    public struct RenderScope
    {
        private const float ShadowDistance = 72f;
        private const int ShadowCascades = 2;
        private const float NearCascadeSplit = 0.20f;

        private bool applied;
        private float previousDistance;
        private int previousCascades;
        private float previousSplit;

        public bool TryApply()
        {
            if (applied || QualitySettings.shadowDistance <= ShadowDistance || QualitySettings.shadowCascades <= ShadowCascades)
            {
                return false;
            }

            previousDistance = QualitySettings.shadowDistance;
            previousCascades = QualitySettings.shadowCascades;
            previousSplit = QualitySettings.shadowCascade2Split;
            applied = true;
            QualitySettings.shadowDistance = ShadowDistance;
            QualitySettings.shadowCascades = ShadowCascades;
            QualitySettings.shadowCascade2Split = NearCascadeSplit;
            return true;
        }

        public void Restore()
        {
            if (!applied)
            {
                return;
            }

            applied = false;
            QualitySettings.shadowDistance = previousDistance;
            QualitySettings.shadowCascades = previousCascades;
            QualitySettings.shadowCascade2Split = previousSplit;
        }
    }
}
