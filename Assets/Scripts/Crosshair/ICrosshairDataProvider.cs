using System;

/// <summary>
/// 准心数据源接口 — 由 WeaponBase 实现
/// 提供 Spread、配置、命中事件
/// </summary>
public interface ICrosshairDataProvider
{
    /// <summary>获取归一化 Spread (0~1)，来自 WeaponBase.currentSpread</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    float GetSpreadNormalized();
    /// <summary>获取当前武器的准心配置</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    CrosshairConfig GetCrosshairConfig();
    /// <summary>中文含义：命中通知回调。命中事件 — 武器命中时触发，携带命中类型信息</summary>
    /// <remarks>TODO(Stage 7 Cleanup): 移除 — Crosshair 已迁移至 CombatEventDispatcher.OnHitConfirmed</remarks>
    event Action<HitInfo> OnHit;
}

/// <summary>
/// 武器切换通知接口 — 由 LeonWeaponHandler 实现
/// 当武器切换时，推送新的 DataProvider 给准心系统
/// </summary>
public interface ICrosshairWeaponProvider
{
    /// <summary>中文含义：数据提供者已切换时的通知回调。武器切换事件 — 推送新的 DataProvider</summary>
    event Action<ICrosshairDataProvider> OnDataProviderSwitched;
}
