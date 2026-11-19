using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 准心表现层 — 纯 Lerp + 策略切换
/// 职责：
///   - 读取 CrosshairModel 做平滑过渡（Alpha / Size / Color）
///   - 根据 Config.crosshairType 切换 ICrosshairRenderer 策略
///   - 将计算结果委托给当前 Renderer 渲染
///
/// 禁止：
///   - 访问任何游戏系统（LeonController / WeaponBase / GameManager 等）
///   - 业务逻辑判断（IsAiming / WeaponType / Reload 等）
///   - Spread 计算
///   - 状态管理
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CrosshairView : MonoBehaviour
{
    /// <summary>中文含义：跨越渲染器。使用位置：CrosshairView.SelectRenderer(CrosshairType)、CrosshairView.SwitchRenderer(CrosshairConfig)。</summary>
    [Header("渲染策略引用 (场景中预挂)")]
    [SerializeField]
    private CrossRenderer crossRenderer;
    /// <summary>中文含义：点积渲染器。使用位置：CrosshairView.SelectRenderer(CrosshairType)。</summary>
    [SerializeField]
    private DotRenderer dotRenderer;
    /// <summary>中文含义：圆形渲染器。使用位置：CrosshairView.SelectRenderer(CrosshairType)。</summary>
    [SerializeField]
    private CircleRenderer circleRenderer;
    /// <summary>中文含义：空值渲染器。使用位置：CrosshairView.SelectRenderer(CrosshairType)。</summary>
    [SerializeField]
    private NullRenderer nullRenderer;
    /// <summary>中文含义：尺寸平滑速度。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Header("平滑参数")]
    [SerializeField]
    private float sizeSmoothSpeed = 12f;
    /// <summary>中文含义：当前渲染器。使用位置：CrosshairView.ApplyModel(CrosshairModel)、CrosshairView.SwitchRenderer(CrosshairConfig)。</summary>
    private ICrosshairRenderer _currentRenderer;
    /// <summary>中文含义：当前类型。使用位置：CrosshairView.SwitchRenderer(CrosshairConfig)。</summary>
    private CrosshairType _currentType = CrosshairType.None;
    // Lerp 当前值
    /// <summary>中文含义：当前透明度。使用位置：作为 ICrosshairRenderer.ApplyAlpha(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CrosshairView.ApplyModel(CrosshairModel) 等共 5 处。</summary>
    private float _currentAlpha;
    /// <summary>中文含义：当前尺寸。使用位置：作为 ICrosshairRenderer.ApplySpread(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CrosshairView.ApplyModel(CrosshairModel) 等共 4 处。</summary>
    private float _currentSize;
    /// <summary>中文含义：当前颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、作为 ICrosshairRenderer.ApplyColor(Color, Color, float) 的参数、CrosshairView.ApplyModel(CrosshairModel) 等共 4 处。</summary>
    private Color _currentColor;
    // Lerp 目标值（由 Model 计算）
    /// <summary>中文含义：目标透明度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    private float _targetAlpha;
    /// <summary>中文含义：目标尺寸。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    private float _targetSize;
    /// <summary>中文含义：目标颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    private Color _targetColor;
    /// <summary>中文含义：目标命中颜色。使用位置：作为 ICrosshairRenderer.ApplyColor(Color, Color, float) 的参数、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    private Color _targetHitColor;
    /// <summary>中文含义：命中混合。使用位置：作为 ICrosshairRenderer.ApplyColor(Color, Color, float) 的参数、CrosshairView.ApplyModel(CrosshairModel)。</summary>
    private float _hitBlend;
    /// <summary>中文含义：画布组。使用位置：CrosshairView.ApplyModel(CrosshairModel)、CrosshairView.Awake()、CrosshairView.SetAlphaImmediate(float) 等共 4 处。</summary>
    private CanvasGroup _canvasGroup;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;CanvasGroup&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        _currentAlpha = 0f;
        _currentSize = 0f;
        _currentColor = Color.white;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    /// <summary>
    /// 由 CrosshairPresenter 每帧调用 — 推送最新 Model 数据
    /// </summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.Update()；下游（含内部回调体） Color.Lerp(Color, Color, float)、CrosshairView.GetFadeSpeed(CrosshairModel)、CrosshairView.GetHitColor(CrosshairModel)、ICrosshairRenderer.ApplyAlpha(float)、ICrosshairRenderer.ApplyColor(Color, Color, float)、ICrosshairRenderer.ApplySpread(float, float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：作为 CrosshairView.GetFadeSpeed(CrosshairModel) 的参数、作为 CrosshairView.GetHitColor(CrosshairModel) 的参数、作为 ICrosshairRenderer.ApplySpread(float, float) 的参数 等共 4 处。</param>
    public void ApplyModel(CrosshairModel model)
    {
        if (model == null || model.Config == null)
        {
            return;
        }

        // --- 计算目标值（纯数学，无业务逻辑）---
        // 1. 透明度目标
        if (model.IsVisible)
        {
            // --- 计算目标值（纯数学，无业务逻辑）---
            // 1. 透明度目标
            _targetAlpha = model.Config.baseColor.a;
        }
        else
        {
            // --- 计算目标值（纯数学，无业务逻辑）---
            // 1. 透明度目标
            _targetAlpha = 0f;
        }

        // 2. 尺寸目标
        _targetSize = model.ComputedSize;
        // 3. 颜色目标 — 基础色与命中色混合
        _hitBlend = model.HitBlend;
        _targetColor = model.Config.baseColor;
        _targetHitColor = GetHitColor(model);
        // --- 执行 Lerp 平滑 ---
        // fadeSpeed（淡入淡出速度）：保存 CrosshairView.GetFadeSpeed(CrosshairModel) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float fadeSpeed = GetFadeSpeed(model);
        // dt（本次计算的时间步长）：使用位置：作为 Color.Lerp(Color, Color, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数。
float dt = Time.deltaTime;
        _currentAlpha = Mathf.Lerp(_currentAlpha, _targetAlpha, dt * fadeSpeed);
        _currentSize = Mathf.Lerp(_currentSize, _targetSize, dt * sizeSmoothSpeed);
        _currentColor = Color.Lerp(_currentColor, _targetColor, dt * sizeSmoothSpeed);
        // --- 委托 Renderer 渲染 ---
        if (_currentRenderer != null)
        {
            _currentRenderer.ApplySpread(model.DisplayedSpread, _currentSize);
            _currentRenderer.ApplyColor(_currentColor, _targetHitColor, _hitBlend);
            _currentRenderer.ApplyAlpha(_currentAlpha);
        }

        // CanvasGroup 透明度
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = _currentAlpha;
        }
    }

    /// <summary>
    /// 切换渲染策略 — 由 Presenter 在武器切换时调用
    /// </summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.Update()；下游（含内部回调体） CrosshairView.SelectRenderer(CrosshairType)、CrossRenderer.Configure(float, float)、ICrosshairRenderer.OnActivate()、ICrosshairRenderer.OnDeactivate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="config">中文含义：当前准星配置。使用位置：作为 CrosshairView.SelectRenderer(CrosshairType) 的参数、作为 CrossRenderer.Configure(float, float) 的参数、CrosshairView.SwitchRenderer(CrosshairConfig)。</param>
    public void SwitchRenderer(CrosshairConfig config)
    {
        if (config == null)
        {
            return;
        }

        // 同类型无需切换
        if (config.crosshairType == _currentType && _currentRenderer != null)
        {
            return;
        }

        // 停用旧策略
        if (_currentRenderer != null)
        {
            _currentRenderer.OnDeactivate();
        }

        _currentType = config.crosshairType;
        _currentRenderer = SelectRenderer(config.crosshairType);
        // 激活新策略
        if (_currentRenderer != null)
        {
            _currentRenderer.OnActivate();
            // 注入配置参数（仅 CrossRenderer 需要）
            if (_currentType == CrosshairType.Cross && crossRenderer != null)
            {
                crossRenderer.Configure(config.thickness, config.lineLength);
            }
        }
    }

    /// <summary>强制设置当前 Alpha（跳过 Lerp），用于初始化或瞬间切换</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="alpha">中文含义：透明度。使用位置：CrosshairView.SetAlphaImmediate(float)。</param>
    public void SetAlphaImmediate(float alpha)
    {
        _currentAlpha = alpha;
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = alpha;
        }
    }

    /// <summary>SelectRenderer 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairView.SwitchRenderer(CrosshairConfig)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前准星类型。使用位置：CrosshairView.SelectRenderer(CrosshairType)。</param>
    private ICrosshairRenderer SelectRenderer(CrosshairType type)
    {
        switch (type)
        {
            case CrosshairType.Cross:
                return crossRenderer;
            case CrosshairType.Dot:
                return dotRenderer;
            case CrosshairType.Circle:
                return circleRenderer;
            case CrosshairType.Laser:
                return null; // 未来实现
            case CrosshairType.None:
                return nullRenderer;
            default:
                return null;
        }
    }

    /// <summary>GetHitColor 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairView.ApplyModel(CrosshairModel)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：CrosshairView.GetHitColor(CrosshairModel)。</param>
    private Color GetHitColor(CrosshairModel model)
    {
        if (model.Config == null)
        {
            return Color.white;
        }

        switch (model.CurrentHitType)
        {
            case HitType.Normal:
                return model.Config.hitColor;
            case HitType.Headshot:
                return model.Config.headshotColor;
            case HitType.Weakspot:
                return model.Config.weakspotColor;
            case HitType.Kill:
                return model.Config.killColor;
            default:
                return model.Config.hitColor;
        }
    }

    /// <summary>GetFadeSpeed 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairView.ApplyModel(CrosshairModel)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="model">中文含义：模型。使用位置：CrosshairView.GetFadeSpeed(CrosshairModel)。</param>
    private float GetFadeSpeed(CrosshairModel model)
    {
        if (model.Config == null)
        {
            return 6f;
        }

        // 隐藏时用 FadeOut，显示时用 FadeIn
        if (model.IsVisible)
        {
            return model.Config.aimFadeInSpeed;
        }
        else
        {
            return model.Config.aimFadeOutSpeed;
        }
    }
}
