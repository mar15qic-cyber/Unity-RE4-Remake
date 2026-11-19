using UnityEngine;

/// <summary>
/// 准心配置 — 每种武器一个 ScriptableObject 资产
/// 数据驱动：新增武器只需创建新的 .asset 文件，不改代码
/// </summary>
[CreateAssetMenu(fileName = "CrosshairConfig", menuName = "RE4/Crosshair Config")]
public class CrosshairConfig : ScriptableObject
{
    /// <summary>中文含义：准星类型。使用位置：作为 CrosshairView.SelectRenderer(CrosshairType) 的参数、CrosshairPresenter.UpdateVisibility()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool) 等共 7 处。</summary>
    [Header("准心类型")]
    public CrosshairType crosshairType = CrosshairType.Cross;
    /// <summary>中文含义：最小准心尺寸 (px, spread=0 时)。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、CrosshairPresenter.Update() 等共 4 处。</summary>
    [Header("尺寸")]
    [Tooltip("最小准心尺寸 (px, spread=0 时)")]
    public float minSize = 6f;
    /// <summary>中文含义：最大准心尺寸 (px, spread=1 时)。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairPresenter.Update()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool) 等共 4 处。</summary>
    [Tooltip("最大准心尺寸 (px, spread=1 时)")]
    public float maxSize = 40f;
    /// <summary>中文含义：线段粗细 / 点大小。使用位置：作为 CrossRenderer.Configure(float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 ShotgunArcCrosshair.SetSize(float, float) 的参数 等共 4 处。</summary>
    [Tooltip("线段粗细 / 点大小")]
    public float thickness = 1f;
    /// <summary>中文含义：线段长度 (Cross 类型)。使用位置：作为 CrossRenderer.Configure(float, float) 的参数、作为 Mathf.Max(float, float) 的参数、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool) 等共 4 处。</summary>
    [Tooltip("线段长度 (Cross 类型)")]
    public float lineLength = 9f;
    /// <summary>中文含义：散布曲线。0~1 归一化 spread → 0~1 UI 尺寸比例曲线。使用位置：CrosshairPresenter.Update()、CrosshairUI.HandleWeaponFired()、CrosshairUI.UpdateSpread()。</summary>
    [Header("Spread 映射")]
    [Tooltip("0~1 归一化 spread → 0~1 UI 尺寸比例曲线")]
    public AnimationCurve spreadCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    /// <summary>中文含义：基础颜色。使用位置：CrosshairUI.SetVisualState(bool, bool)、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    [Header("颜色")]
    public Color baseColor = new Color(1f, 1f, 1f, 0.8f);
    /// <summary>中文含义：命中颜色。使用位置：CrosshairView.GetHitColor(CrosshairModel)。</summary>
    public Color hitColor = new Color(1f, 0.4f, 0.1f, 0.9f);
    /// <summary>中文含义：爆头颜色。使用位置：CrosshairView.GetHitColor(CrosshairModel)。</summary>
    public Color headshotColor = new Color(1f, 0.9f, 0.2f, 1f);
    /// <summary>中文含义：弱点颜色。使用位置：CrosshairView.GetHitColor(CrosshairModel)。</summary>
    public Color weakspotColor = new Color(0.9f, 0.1f, 0.5f, 1f);
    /// <summary>中文含义：击杀颜色。使用位置：CrosshairView.GetHitColor(CrosshairModel)。</summary>
    public Color killColor = new Color(1f, 1f, 1f, 1f);
    /// <summary>中文含义：命中反馈持续时间 (秒)。使用位置：作为 Mathf.Clamp01(float) 的参数、CrosshairPresenter.HandleHitConfirmed(HitResult)。</summary>
    [Header("命中反馈")]
    [Tooltip("命中反馈持续时间 (秒)")]
    public float hitFeedbackDuration = 0.15f;
    /// <summary>中文含义：命中时缩放倍率。</summary>
    [Tooltip("命中时缩放倍率")]
    public float hitFeedbackScale = 1.3f;
    /// <summary>中文含义：命中反馈消退速度。使用位置：CrosshairPresenter.Update()。</summary>
    [Tooltip("命中反馈消退速度")]
    public float hitFeedbackFadeSpeed = 6f;
    /// <summary>中文含义：是否显示内部瞄准。使用位置：CrosshairPresenter.UpdateVisibility()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</summary>
    [Header("显示模式")]
    public bool showInAim = true;
    /// <summary>中文含义：是否显示内部髋部开火。使用位置：CrosshairPresenter.HandleAimChanged(bool)、CrosshairPresenter.HandleReloadingChanged(bool)、CrosshairPresenter.UpdateVisibility() 等共 4 处。</summary>
    public bool showInHipFire = true;
    /// <summary>中文含义：是否显示内部换弹。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)、CrosshairPresenter.UpdateVisibility()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</summary>
    public bool showInReload = false;
    /// <summary>中文含义：瞄准淡入速度。使用位置：CrosshairView.GetFadeSpeed(CrosshairModel)。</summary>
    [Header("过渡")]
    public float aimFadeInSpeed = 8f;
    /// <summary>中文含义：瞄准淡出速度。使用位置：CrosshairView.GetFadeSpeed(CrosshairModel)。</summary>
    public float aimFadeOutSpeed = 6f;
    /// <summary>中文含义：武器切换淡入淡出速度。</summary>
    public float weaponSwitchFadeSpeed = 5f;
    /// <summary>中文含义：是否动态散布。使用位置：CrosshairPresenter.Update()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)、CrosshairUI.UpdateSpread()。</summary>
    [Header("RE4R 风格")]
    public bool dynamicSpread = true;
    /// <summary>中文含义：动态散布速度。使用位置：作为 Mathf.Max(float, float) 的参数、CrosshairPresenter.Update()、CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</summary>
    public float dynamicSpreadSpeed = 10f;
    /// <summary>中文含义：每次成功开火叠加的准心扩张比例。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    [Header("开火扩张")]
    [Tooltip("每次成功开火叠加的准心扩张比例")]
    [Range(0f, 1f)]
    public float fireBloomAmount = 0.6f;
    /// <summary>中文含义：开火扩张每秒恢复的比例。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("开火扩张每秒恢复的比例")]
    [Min(0.01f)]
    public float fireBloomRecoverySpeed = 2.2f;
}
