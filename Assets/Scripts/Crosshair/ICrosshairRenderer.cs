using UnityEngine;

/// <summary>
/// 准心渲染策略接口 — 每种准心类型一个实现
/// 职责：纯 UI 渲染，不包含任何业务逻辑
/// </summary>
public interface ICrosshairRenderer
{
    /// <summary>应用扩散 — 更新 UI 元素位置/尺寸</summary>
    /// <param name="normalizedSpread">中文含义：归一化散布。归一化 Spread (0~1)</param>
    /// <param name="uiSize">中文含义：界面尺寸。计算后的 UI 尺寸 (px)</param>
    /// <remarks>静态调用链：上游 CrosshairView.ApplyModel(CrosshairModel)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplySpread(float normalizedSpread, float uiSize);
    /// <summary>应用颜色 — 基础色与命中色混合</summary>
    /// <param name="baseColor">中文含义：基础颜色。基础颜色</param>
    /// <param name="hitColor">中文含义：命中颜色。命中反馈颜色</param>
    /// <param name="hitBlend">中文含义：命中混合。命中混合值 (0=基础色, 1=命中色)</param>
    /// <remarks>静态调用链：上游 CrosshairView.ApplyModel(CrosshairModel)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyColor(Color baseColor, Color hitColor, float hitBlend);
    /// <summary>应用透明度</summary>
    /// <param name="alpha">中文含义：透明度。目标透明度 (0~1)</param>
    /// <remarks>静态调用链：上游 CrosshairView.ApplyModel(CrosshairModel)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyAlpha(float alpha);
    /// <summary>激活此渲染器 — 显示并初始化 UI 元素</summary>
    /// <remarks>静态调用链：上游 CrosshairView.SwitchRenderer(CrosshairConfig)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnActivate();
    /// <summary>停用此渲染器 — 隐藏 UI 元素</summary>
    /// <remarks>静态调用链：上游 CrosshairView.SwitchRenderer(CrosshairConfig)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDeactivate();
}
