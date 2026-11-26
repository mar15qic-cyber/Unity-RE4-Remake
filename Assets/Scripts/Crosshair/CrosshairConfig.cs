using UnityEngine;

/// <summary>
/// 准心配置 — 每种武器一个 ScriptableObject 资产
/// 数据驱动：新增武器只需创建新的 .asset 文件，不改代码
/// </summary>
[CreateAssetMenu(fileName = "CrosshairConfig", menuName = "RE4/Crosshair Config")]
public class CrosshairConfig : ScriptableObject
{
    /// <summary>准星类型。</summary>
    [Header("准心类型")]
    public CrosshairType crosshairType = CrosshairType.Cross;
    /// <summary>最小准心尺寸 (px, spread=0 时)。</summary>
    [Header("尺寸")]
    [Tooltip("最小准心尺寸 (px, spread=0 时)")]
    public float minSize = 6f;
    /// <summary>最大准心尺寸 (px, spread=1 时)。</summary>
    [Tooltip("最大准心尺寸 (px, spread=1 时)")]
    public float maxSize = 40f;
    /// <summary>线段粗细 / 点大小。</summary>
    [Tooltip("线段粗细 / 点大小")]
    public float thickness = 1f;
    /// <summary>线段长度 (Cross 类型)。</summary>
    [Tooltip("线段长度 (Cross 类型)")]
    public float lineLength = 9f;
    [Tooltip("原版四向准星的九宫格图块。尺寸以 1080p 为参考。")]
    public Sprite lineSprite;
    /// <summary>散布曲线。0~1 归一化 spread → 0~1 UI 尺寸比例曲线。</summary>
    [Header("Spread 映射")]
    [Tooltip("0~1 归一化 spread → 0~1 UI 尺寸比例曲线")]
    public AnimationCurve spreadCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    /// <summary>基础颜色。</summary>
    [Header("颜色")]
    public Color baseColor = new Color(1f, 1f, 1f, 0.8f);
    /// <summary>命中颜色。</summary>
    public Color hitColor = new Color(1f, 0.4f, 0.1f, 0.9f);
    /// <summary>爆头颜色。</summary>
    public Color headshotColor = new Color(1f, 0.9f, 0.2f, 1f);
    /// <summary>弱点颜色。</summary>
    public Color weakspotColor = new Color(0.9f, 0.1f, 0.5f, 1f);
    /// <summary>击杀颜色。</summary>
    public Color killColor = new Color(1f, 1f, 1f, 1f);
    /// <summary>命中反馈持续时间 (秒)。</summary>
    [Header("命中反馈")]
    [Tooltip("命中反馈持续时间 (秒)")]
    public float hitFeedbackDuration = 0.15f;
    /// <summary>中文含义：命中时缩放倍率。</summary>
    [Tooltip("命中时缩放倍率")]
    public float hitFeedbackScale = 1.3f;
    /// <summary>命中反馈消退速度。</summary>
    [Tooltip("命中反馈消退速度")]
    public float hitFeedbackFadeSpeed = 6f;
    /// <summary>是否显示内部瞄准。</summary>
    [Header("显示模式")]
    public bool showInAim = true;
    /// <summary>是否显示内部髋部开火。</summary>
    public bool showInHipFire = true;
    /// <summary>是否显示内部换弹。</summary>
    public bool showInReload = false;
    /// <summary>瞄准淡入速度。</summary>
    [Header("过渡")]
    public float aimFadeInSpeed = 8f;
    /// <summary>瞄准淡出速度。</summary>
    public float aimFadeOutSpeed = 6f;
    /// <summary>中文含义：武器切换淡入淡出速度。</summary>
    public float weaponSwitchFadeSpeed = 5f;
    /// <summary>是否动态散布。</summary>
    [Header("RE4R 风格")]
    public bool dynamicSpread = true;
    /// <summary>动态散布速度。</summary>
    public float dynamicSpreadSpeed = 10f;
    /// <summary>每次成功开火叠加的准心扩张比例。</summary>
    [Header("开火扩张")]
    [Tooltip("每次成功开火叠加的准心扩张比例")]
    [Range(0f, 1f)]
    public float fireBloomAmount = 0.6f;
    /// <summary>开火扩张每秒恢复的比例。</summary>
    [Tooltip("开火扩张每秒恢复的比例")]
    [Min(0.01f)]
    public float fireBloomRecoverySpeed = 2.2f;
}
