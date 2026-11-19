using UnityEngine;

/// <summary>
/// 准心运行时数据状态 — 纯数据，无逻辑，无 MonoBehaviour
/// 由 CrosshairPresenter 写入，由 CrosshairView 只读访问
/// </summary>
public class CrosshairModel
{
    /// <summary>中文含义：当前散布。当前真实 Spread (0~1)，来自 WeaponBase</summary>
    public float CurrentSpread;
    /// <summary>中文含义：已显示散布。平滑后的 Spread (Lerp 结果)</summary>
    public float DisplayedSpread;
    /// <summary>中文含义：配置。当前武器配置</summary>
    public CrosshairConfig Config;
    /// <summary>中文含义：是否可见。综合可见性 (考虑了 Aim/Reload/Pause/Death/Inventory)</summary>
    public bool IsVisible;
    /// <summary>中文含义：当前命中类型。当前命中类型</summary>
    public HitType CurrentHitType;
    /// <summary>中文含义：命中计时器。命中反馈计时器 (>0 表示反馈进行中)</summary>
    public float HitTimer;
    /// <summary>中文含义：阶段。当前显示阶段</summary>
    public CrosshairPhase Phase;
    /// <summary>中文含义：计算后的尺寸。计算后的 UI 尺寸 (px)</summary>
    public float ComputedSize;
    /// <summary>中文含义：命中混合。命中反馈混合值 (0=无反馈, 1=满反馈)</summary>
    public float HitBlend;
    /// <summary>构造并初始化 CrosshairModel 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.Awake()、CrosshairPresenter.Model.get；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public CrosshairModel()
    {
        CurrentSpread = 0f;
        DisplayedSpread = 0f;
        Config = null;
        IsVisible = false;
        CurrentHitType = HitType.Normal;
        HitTimer = 0f;
        Phase = CrosshairPhase.Hidden;
        ComputedSize = 0f;
        HitBlend = 0f;
    }

    /// <summary>重置为初始状态</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Reset()
    {
        CurrentSpread = 0f;
        DisplayedSpread = 0f;
        IsVisible = false;
        HitTimer = 0f;
        Phase = CrosshairPhase.Hidden;
        ComputedSize = 0f;
        HitBlend = 0f;
        CurrentHitType = HitType.Normal;
    }
}
