using UnityEngine;
using UnityEngine.UI;

/// <summary>以原版 1080p 布局显示 W-870 双弧及中心点。</summary>
public sealed class ShotgunArcCrosshair : Image
{
    public void SetLayoutScale(float unit)
    {
        if (sprite == null) return;
        // 图块以原版双倍纹理密度保存。
        Vector2 size = sprite.rect.size * (0.5f * unit);
        if (rectTransform.sizeDelta != size) rectTransform.sizeDelta = size;
    }
}
