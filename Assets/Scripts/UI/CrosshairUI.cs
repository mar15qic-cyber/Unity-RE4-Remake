using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 唯一准心控制器。
/// 各武器复用同一组视觉节点，仅切换数据配置；小刀使用中心单点。
/// </summary>
/// <remarks>正式准星显示入口，联合当前武器配置、瞄准状态和已提交命中反馈更新表现；初始化会停用旧准星组件以避免重复显示。</remarks>
[DefaultExecutionOrder(-80)]
public class CrosshairUI : MonoBehaviour
{
    /// <summary>中文含义：实例。使用位置：CrosshairUI.Awake()、CrosshairUI.OnDestroy()。</summary>
    public static CrosshairUI Instance { get; private set; }

    /// <summary>中文含义：线顶部。使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、作为 CrosshairUI.SetPosition(RectTransform, float, float) 的参数 等共 6 处。</summary>
    [Header("准心线段")]
    public RectTransform lineTop;
    /// <summary>中文含义：线底部。使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、作为 CrosshairUI.SetPosition(RectTransform, float, float) 的参数 等共 6 处。</summary>
    public RectTransform lineBottom;
    /// <summary>中文含义：线左侧。使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、作为 CrosshairUI.SetPosition(RectTransform, float, float) 的参数 等共 6 处。</summary>
    public RectTransform lineLeft;
    /// <summary>中文含义：线右侧。使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、作为 CrosshairUI.SetPosition(RectTransform, float, float) 的参数 等共 6 处。</summary>
    public RectTransform lineRight;
    /// <summary>中文含义：准心所属画布。未赋值时仅沿父级查找，找不到会输出诊断。使用位置：CrosshairUI.EnsureVisuals()。</summary>
    [Header("画布引用")]
    [Tooltip("准心所属画布。未赋值时仅沿父级查找，找不到会输出诊断。")]
    public Canvas targetCanvas;
    /// <summary>中文含义：中心点积。使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、CrosshairUI.ApplyStyle() 等共 6 处。</summary>
    public RectTransform centerDot;
    /// <summary>中文含义：留空时使用当前武器数据中的准心配置。使用位置：CrosshairUI.GetInspectorConfig(WeaponType)。</summary>
    [Header("武器准心配置")]
    [Tooltip("留空时使用当前武器数据中的准心配置。")]
    public CrosshairConfig pistolConfig;
    /// <summary>中文含义：留空时使用当前武器数据中的准心配置。使用位置：CrosshairUI.GetInspectorConfig(WeaponType)。</summary>
    [Tooltip("留空时使用当前武器数据中的准心配置。")]
    public CrosshairConfig rifleConfig;
    /// <summary>中文含义：留空时使用当前武器数据中的准心配置。使用位置：CrosshairUI.GetInspectorConfig(WeaponType)。</summary>
    [Tooltip("留空时使用当前武器数据中的准心配置。")]
    public CrosshairConfig shotgunConfig;
    /// <summary>中文含义：预留给未来小刀操作的单点准心配置。使用位置：CrosshairUI.GetInspectorConfig(WeaponType)。</summary>
    [Tooltip("预留给未来小刀操作的单点准心配置。")]
    public CrosshairConfig knifeConfig;
    /// <summary>中文含义：散布平滑速度。使用位置：CrosshairUI.UpdateSpread()。</summary>
    [Header("显示过渡")]
    public float spreadSmoothSpeed = 12f;
    /// <summary>中文含义：透明度平滑速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    public float alphaSmoothSpeed = 14f;
    /// <summary>中文含义：后备颜色。使用位置：CrosshairUI.CreatePart(Transform, string)、CrosshairUI.SetVisualState(bool, bool)。</summary>
    public Color fallbackColor = new Color(1f, 1f, 1f, 0.86f);
    /// <summary>中文含义：画布组。使用位置：作为 Mathf.Abs(float) 的参数、CrosshairUI.EnsureVisuals()、CrosshairUI.SetVisualState(bool, bool)。</summary>
    private CanvasGroup canvasGroup;
    /// <summary>中文含义：里昂。使用位置：作为 Mathf.Clamp01(float) 的参数、CrosshairUI.RefreshWeaponConfig(bool)、CrosshairUI.ResolvePlayer() 等共 5 处。</summary>
    private LeonController leon;
    /// <summary>中文含义：管理当前装备与武器切换的组件。使用位置：CrosshairUI.RefreshWeaponConfig(bool)、CrosshairUI.ResolvePlayer()、CrosshairUI.Update() 等共 4 处。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>中文含义：当前配置。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数 等共 11 处。</summary>
    private CrosshairConfig currentConfig;
    /// <summary>中文含义：当前武器类型。使用位置：作为 CrosshairUI.GetFallbackConfig(WeaponType) 的参数、作为 CrosshairUI.GetInspectorConfig(WeaponType) 的参数、作为 LeonWeaponHandler.GetWeapon(WeaponType) 的参数 等共 6 处。</summary>
    [SerializeField, Min(0.15f), Tooltip("从举枪完成到稳定瞄准的收束时间。")]
    private float aimSettleTime = 1.25f;
    private float aimSettleProgress;
    private bool guardReticleActive;
    private Image centerDotImage;
    private Sprite originalCenterSprite;
    private Sprite knifeDotSprite;
    private WeaponType currentWeaponType;
    /// <summary>中文含义：后备手枪配置。使用位置：CrosshairUI.GetFallbackConfig(WeaponType)。</summary>
    private CrosshairConfig fallbackPistolConfig;
    /// <summary>中文含义：后备步枪配置。使用位置：CrosshairUI.GetFallbackConfig(WeaponType)。</summary>
    private CrosshairConfig fallbackRifleConfig;
    /// <summary>中文含义：后备霰弹枪配置。使用位置：CrosshairUI.GetFallbackConfig(WeaponType)。</summary>
    private CrosshairConfig fallbackShotgunConfig;
    /// <summary>中文含义：后备小刀配置。使用位置：CrosshairUI.GetFallbackConfig(WeaponType)。</summary>
    private CrosshairConfig fallbackKnifeConfig;
    /// <summary>中文含义：已订阅武器。使用位置：CrosshairUI.SetSubscribedWeapon(WeaponBase)。</summary>
    private WeaponBase subscribedWeapon;
    /// <summary>中文含义：已显示尺寸。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 ShotgunArcCrosshair.SetSize(float, float) 的参数 等共 6 处。</summary>
    private float displayedSize;
    /// <summary>中文含义：已显示透明度。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CrosshairUI.SetVisualState(bool, bool)。</summary>
    private float displayedAlpha;
    /// <summary>中文含义：开火泛光。使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Max(float, float) 的参数 等共 8 处。</summary>
    private float fireBloom;
    /// <summary>中文含义：是否已初始化。使用位置：CrosshairUI.Start()、CrosshairUI.Update()。</summary>
    private bool initialized;
    /// <summary>中文含义：视觉表现状态是否已初始化。使用位置：CrosshairUI.SetVisualState(bool, bool)。</summary>
    private bool visualStateInitialized;
    /// <summary>中文含义：是否前一次显示点积。使用位置：CrosshairUI.SetVisualState(bool, bool)。</summary>
    private bool previousShowDot;
    /// <summary>中文含义：是否前一次显示跨越。使用位置：CrosshairUI.SetVisualState(bool, bool)。</summary>
    private bool previousShowCross;
    private float lineUnit = 1f;
    /// <summary>中文含义：霰弹枪弹药准星。使用位置：CrosshairUI.EnsureVisuals()、CrosshairUI.HideOwnedVisuals()、CrosshairUI.SetVisualState(bool, bool) 等共 4 处。</summary>
    private ShotgunArcCrosshair shotgunAmmoCrosshair;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairUI.HideOwnedVisuals()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            HideOwnedVisuals();
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairUI.DisableCompetingCrosshairs()、CrosshairUI.EnsureVisuals()、CrosshairUI.RefreshWeaponConfig(bool)、CrosshairUI.ResolvePlayer()、CrosshairUI.SetVisualState(bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        EnsureVisuals();
        centerDotImage = centerDot.GetComponent<Image>();
        if (centerDotImage != null)
        {
            originalCenterSprite = centerDotImage.overrideSprite;
            knifeDotSprite = Resources.Load<Sprite>("UI/KnifeReticle");
        }
        ResolvePlayer();
        DisableCompetingCrosshairs();
        initialized = true;
        RefreshWeaponConfig(true);
        SetVisualState(false, true);
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairUI.SetSubscribedWeapon(WeaponBase)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        SetSubscribedWeapon(null);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairUI.RecoverFireBloom()、CrosshairUI.RefreshWeaponConfig(bool)、CrosshairUI.ResolvePlayer()、CrosshairUI.SetVisualState(bool, bool)、CrosshairUI.UpdateSpread()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (leon == null || weaponHandler == null)
        {
            ResolvePlayer();
            if (leon == null || weaponHandler == null)
            {
                RecoverFireBloom();
                SetVisualState(false, false);
                return;
            }
        }

        RefreshWeaponConfig(false);
        // gameplayVisible（玩法是否可见）：使用位置：CrosshairUI.Update()。
bool gameplayVisible = GameUIManager.Instance == null || GameUIManager.Instance.currentState == GameUIManager.State.Gameplay;
        // isReloading（是否正在换弹）：使用位置：CrosshairUI.Update()。
bool isReloading = leon.currentState == LeonState.Reloading;
        // visible（是否可见）：使用位置：作为 CrosshairUI.SetVisualState(bool, bool) 的参数、CrosshairUI.Update()。
        bool poseReady = leon.AnimDriver == null || leon.AnimDriver.CanShowAimReticle;
        bool guardAiming = leon.TryGetComponent<KnifeCombatModule>(out var knife) && knife.IsGuardAiming;
        bool useKnifeDot = guardAiming || currentWeaponType == WeaponType.Knife;
        if (guardReticleActive != useKnifeDot && centerDotImage != null)
        {
            if (useKnifeDot)
            {
                centerDotImage.overrideSprite = knifeDotSprite;
            }
            else
            {
                centerDotImage.overrideSprite = originalCenterSprite;
            }
        }
        guardReticleActive = useKnifeDot;
        bool visible = gameplayVisible && (guardAiming || leon.IsAiming && poseReady)
            && !isReloading && currentConfig != null;
        if (visible)
        {
            if (aimSettleProgress == 0f)
            {
                displayedSize = currentConfig.maxSize;
            }

            aimSettleProgress = Mathf.MoveTowards(aimSettleProgress, 1f,
                Time.deltaTime / Mathf.Max(0.15f, aimSettleTime));
            UpdateSpread();
        }
        else
        {
            aimSettleProgress = 0f;
            RecoverFireBloom();
        }

        SetVisualState(visible, false);
    }

    /// <summary>ResolvePlayer 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、GameObject.FindGameObjectWithTag(string)、GameObject.GetComponent&lt;LeonController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Start()、CrosshairUI.Update()；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、GameObject.FindGameObjectWithTag(string)、GameObject.GetComponent&lt;LeonController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolvePlayer()
    {
        leon = LeonController.Instance;
        if (leon == null)
        {
            // player（玩家）：保存 GameObject.FindGameObjectWithTag(string) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.ResolvePlayer()。
GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                leon = player.GetComponent<LeonController>();
            }
        }

        if (leon != null)
        {
            weaponHandler = leon.GetComponent<LeonWeaponHandler>();
        }
    }

    /// <summary>RefreshWeaponConfig 的业务处理入口；本方法直接依赖 CrosshairUI.ApplyStyle()、CrosshairUI.GetFallbackConfig(WeaponType)、CrosshairUI.GetInspectorConfig(WeaponType)、CrosshairUI.SetSubscribedWeapon(WeaponBase)、LeonWeaponHandler.GetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Start()、CrosshairUI.Update()；下游（含内部回调体） CrosshairUI.ApplyStyle()、CrosshairUI.GetFallbackConfig(WeaponType)、CrosshairUI.GetInspectorConfig(WeaponType)、CrosshairUI.SetSubscribedWeapon(WeaponBase)、LeonWeaponHandler.GetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="force">中文含义：是否力度。使用位置：CrosshairUI.RefreshWeaponConfig(bool)。</param>
    void RefreshWeaponConfig(bool force)
    {
        if (leon == null || weaponHandler == null)
        {
            return;
        }

        if (!force && currentWeaponType == leon.currentWeapon && currentConfig != null)
        {
            return;
        }

        currentWeaponType = leon.currentWeapon;
        aimSettleProgress = 0f;
        // weapon（当前武器组件）：保存 LeonWeaponHandler.GetWeapon(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 CrosshairUI.SetSubscribedWeapon(WeaponBase) 的参数、CrosshairUI.RefreshWeaponConfig(bool)。
WeaponBase weapon = weaponHandler.GetWeapon(currentWeaponType);
        SetSubscribedWeapon(weapon);
        // weaponDataMatches（是否武器数据匹配项）：使用位置：CrosshairUI.RefreshWeaponConfig(bool)。
bool weaponDataMatches = weapon != null && weapon.data != null && weapon.data.weaponType == currentWeaponType;
        // weaponConfig（武器配置）：使用位置：CrosshairUI.RefreshWeaponConfig(bool)。
CrosshairConfig weaponConfig;
        if (weaponDataMatches)
        {
            weaponConfig = weapon.data.crosshairConfig;
        }
        else
        {
            weaponConfig = null;
        }

        // inspectorConfig（检视面板配置）：保存 CrosshairUI.GetInspectorConfig(WeaponType) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.RefreshWeaponConfig(bool)。
CrosshairConfig inspectorConfig = GetInspectorConfig(currentWeaponType);
        if (inspectorConfig != null)
        {
            currentConfig = inspectorConfig;
        }
        else
        {
            if (weaponConfig != null)
            {
                currentConfig = weaponConfig;
            }
            else
            {
                currentConfig = GetFallbackConfig(currentWeaponType);
            }
        }

        if (currentConfig != null)
        {
            displayedSize = currentConfig.maxSize;
        }
        else
        {
            displayedSize = 0f;
        }

        fireBloom = 0f;
        ApplyStyle();
    }

    /// <summary>SetSubscribedWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.OnDestroy()、CrosshairUI.RefreshWeaponConfig(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器组件。使用位置：CrosshairUI.SetSubscribedWeapon(WeaponBase)。</param>
    void SetSubscribedWeapon(WeaponBase weapon)
    {
        if (subscribedWeapon == weapon)
        {
            return;
        }

        if (subscribedWeapon != null)
        {
            subscribedWeapon.OnFired -= HandleWeaponFired;
        }

        subscribedWeapon = weapon;
        if (subscribedWeapon != null)
        {
            subscribedWeapon.OnFired += HandleWeaponFired;
        }
    }

    /// <summary>HandleWeaponFired 的业务处理入口；本方法直接依赖 AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimationCurve.Evaluate(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleWeaponFired()
    {
        if (currentWeaponType == WeaponType.Shotgun || currentConfig == null || currentConfig.crosshairType == CrosshairType.Dot || currentConfig.crosshairType == CrosshairType.None)
        {
            return;
        }

        fireBloom = Mathf.Clamp01(fireBloom + Mathf.Clamp01(currentConfig.fireBloomAmount));
        // curveValue（曲线值）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairUI.HandleWeaponFired()。
float curveValue;
        if (currentConfig.spreadCurve != null)
        {
            curveValue = currentConfig.spreadCurve.Evaluate(fireBloom);
        }
        else
        {
            curveValue = fireBloom;
        }

        // shotSize（射击尺寸）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float shotSize = Mathf.Lerp(currentConfig.minSize, currentConfig.maxSize, curveValue);
        // 开火帧立即张开，收缩仍由每帧平滑处理。
        displayedSize = Mathf.Max(displayedSize, shotSize);
    }

    /// <summary>GetInspectorConfig 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.RefreshWeaponConfig(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">中文含义：武器类型。使用位置：CrosshairUI.GetInspectorConfig(WeaponType)。</param>
    CrosshairConfig GetInspectorConfig(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                return pistolConfig;
            case WeaponType.Rifle:
                return rifleConfig;
            case WeaponType.Shotgun:
                return shotgunConfig;
            case WeaponType.Knife:
                return knifeConfig;
            default:
                return null;
        }
    }

    /// <summary>GetFallbackConfig 的业务处理入口；本方法直接依赖 CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.RefreshWeaponConfig(bool)；下游（含内部回调体） CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">中文含义：武器类型。使用位置：CrosshairUI.GetFallbackConfig(WeaponType)。</param>
    CrosshairConfig GetFallbackConfig(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Pistol:
                if (fallbackPistolConfig == null)
                {
                    fallbackPistolConfig = CreateFallbackConfig("手枪后备准心", CrosshairType.Cross, 2f, 15f, 12f, true);
                    fallbackPistolConfig.thickness = 4f;
                    fallbackPistolConfig.dynamicSpreadSpeed = 36f;
                    fallbackPistolConfig.lineSprite = Resources.Load<Sprite>("UI/SG09RReticle");
                }

                return fallbackPistolConfig;
            case WeaponType.Rifle:
                if (fallbackRifleConfig == null)
                {
                    fallbackRifleConfig = CreateFallbackConfig("步枪后备准心", CrosshairType.Cross, 9f, 54f, 7f, true);
                }

                return fallbackRifleConfig;
            case WeaponType.Shotgun:
                if (fallbackShotgunConfig == null)
                {
                    fallbackShotgunConfig = CreateFallbackConfig("霰弹枪后备准心", CrosshairType.Circle, 136f, 158f, 0f, true);
                }

                return fallbackShotgunConfig;
            case WeaponType.Knife:
                if (fallbackKnifeConfig == null)
                {
                    fallbackKnifeConfig = CreateFallbackConfig("小刀后备准心", CrosshairType.Dot, 4f, 4f, 0f, false);
                }

                return fallbackKnifeConfig;
            default:
                return null;
        }
    }

    /// <summary>CreateFallbackConfig 的业务处理入口；本方法直接依赖 ScriptableObject.CreateInstance&lt;CrosshairConfig&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.GetFallbackConfig(WeaponType)；下游（含内部回调体） ScriptableObject.CreateInstance&lt;CrosshairConfig&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="configName">中文含义：配置名称。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    /// <param name="type">中文含义：当前准星类型。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    /// <param name="minSize">中文含义：最小尺寸。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    /// <param name="maxSize">中文含义：最大尺寸。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    /// <param name="lineLength">中文含义：线长度。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    /// <param name="dynamicSpread">中文含义：是否动态散布。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。</param>
    CrosshairConfig CreateFallbackConfig(string configName, CrosshairType type, float minSize, float maxSize, float lineLength, bool dynamicSpread)
    {
        // config（当前准星配置）：保存 ScriptableObject.CreateInstance<CrosshairConfig>() 的返回结果，供当前步骤使用。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。
CrosshairConfig config = ScriptableObject.CreateInstance<CrosshairConfig>();
        config.name = configName;
        config.hideFlags = HideFlags.HideAndDontSave;
        config.crosshairType = type;
        config.minSize = minSize;
        config.maxSize = maxSize;
        if (type == CrosshairType.Dot)
        {
            config.thickness = minSize;
        }

        config.lineLength = lineLength;
        config.showInAim = true;
        config.showInHipFire = false;
        config.showInReload = false;
        config.dynamicSpread = dynamicSpread;
        // assignmentReceiver6（赋值前保存的目标当前准星配置）：从配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：CrosshairUI.CreateFallbackConfig(string, CrosshairType, float, float, float, bool)。
global::CrosshairConfig assignmentReceiver6 = config;
        if (maxSize > 40f)
        {
            assignmentReceiver6.dynamicSpreadSpeed = 15f;
        }
        else
        {
            assignmentReceiver6.dynamicSpreadSpeed = 12f;
        }

        return config;
    }

    /// <summary>UpdateSpread 的业务处理入口；本方法直接依赖 AnimationCurve.Evaluate(float)、CrosshairUI.SetPosition(RectTransform, float, float)、LeonWeaponHandler.GetWeapon(WeaponType)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Update()；下游（含内部回调体） AnimationCurve.Evaluate(float)、CrosshairUI.SetPosition(RectTransform, float, float)、LeonWeaponHandler.GetWeapon(WeaponType)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSpread()
    {
        if (guardReticleActive)
        {
            if (centerDot != null)
            {
                centerDot.sizeDelta = Vector2.one * 9f;
            }
            return;
        }
        if (currentConfig == null || weaponHandler == null)
        {
            return;
        }

        if (currentWeaponType == WeaponType.Shotgun)
        {
            if (shotgunAmmoCrosshair != null)
                shotgunAmmoCrosshair.SetLayoutScale(ReadCanvasUnit(shotgunAmmoCrosshair.transform.parent));
            return;
        }

        // weapon（当前武器组件）：保存 LeonWeaponHandler.GetWeapon(WeaponType) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.UpdateSpread()。
WeaponBase weapon = weaponHandler.GetWeapon(currentWeaponType);
        // weaponSpread（武器散布）：使用位置：作为 Mathf.Max(float, float) 的参数、CrosshairUI.UpdateSpread()。
float weaponSpread;
        if (weapon != null)
        {
            weaponSpread = weapon.GetSpreadNormalized();
        }
        else
        {
            weaponSpread = 0f;
        }

        // moveSpread（移动散布）：使用位置：作为 Mathf.Max(float, float) 的参数、CrosshairUI.UpdateSpread()。
float moveSpread;
        if (leon != null)
        {
            moveSpread = Mathf.Clamp01(leon.MoveInput.magnitude * 0.7f);
        }
        else
        {
            moveSpread = 0f;
        }

        // normalized（归一化）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 AnimationCurve.Evaluate(float) 的参数、CrosshairUI.UpdateSpread()。
        float focusSpread = 1f - Mathf.SmoothStep(0f, 1f, aimSettleProgress);
        if (currentConfig.lineSprite != null)
        {
            // 原作先缓慢收拢，最后一小段快速合到聚焦间距。
            float finish = Mathf.InverseLerp(.88f, 1f, aimSettleProgress);
            focusSpread = 1f - .4f * aimSettleProgress * aimSettleProgress - .6f * Mathf.SmoothStep(0f, 1f, finish);
            if (!Mathf.Approximately(lineUnit, ReadLineUnit())) ApplyStyle();
        }
        float normalized = Mathf.Max(Mathf.Max(weaponSpread, moveSpread), Mathf.Max(fireBloom, focusSpread));
        // curveValue（曲线值）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairUI.UpdateSpread()。
float curveValue;
        if (currentConfig.spreadCurve != null)
        {
            curveValue = currentConfig.spreadCurve.Evaluate(normalized);
        }
        else
        {
            curveValue = normalized;
        }

        // targetSize（目标尺寸）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float targetSize = Mathf.Lerp(currentConfig.minSize, currentConfig.maxSize, curveValue);
        // speed（速度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairUI.UpdateSpread()。
float speed;
        if (currentConfig.dynamicSpread)
        {
            speed = Mathf.Max(0.01f, currentConfig.dynamicSpreadSpeed);
        }
        else
        {
            speed = spreadSmoothSpeed;
        }

        displayedSize = Mathf.Lerp(displayedSize, targetSize, 1f - Mathf.Exp(-Time.deltaTime * speed));
        fireBloom = Mathf.MoveTowards(fireBloom, 0f, Mathf.Max(0.01f, currentConfig.fireBloomRecoverySpeed) * Time.deltaTime);
        if (currentConfig.crosshairType == CrosshairType.Dot)
        {
            if (centerDot != null)
            {
                centerDot.sizeDelta = Vector2.one * Mathf.Max(2f, displayedSize);
            }

            return;
        }

        // lineLength（线长度）：使用位置：CrosshairUI.UpdateSpread()。
float lineLength = currentConfig.lineLength;
        // gap（间隙）：使用位置：作为 CrosshairUI.SetPosition(RectTransform, float, float) 的参数。
float gap = (displayedSize + lineLength * 0.5f) * lineUnit;
        SetPosition(lineTop, 0f, gap);
        SetPosition(lineBottom, 0f, -gap);
        SetPosition(lineLeft, -gap, 0f);
        SetPosition(lineRight, gap, 0f);
    }

    /// <summary>RecoverFireBloom 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Update()；下游（含内部回调体） Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RecoverFireBloom()
    {
        if (currentConfig == null)
        {
            fireBloom = 0f;
            return;
        }

        fireBloom = Mathf.MoveTowards(fireBloom, 0f, Mathf.Max(0.01f, currentConfig.fireBloomRecoverySpeed) * Time.deltaTime);
    }

    /// <summary>ApplyStyle 的业务处理入口；本方法直接依赖 CrosshairUI.SetSize(RectTransform, float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.RefreshWeaponConfig(bool)；下游（含内部回调体） CrosshairUI.SetSize(RectTransform, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyStyle()
    {
        if (currentConfig == null)
        {
            return;
        }

        // thickness（厚度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 CrosshairUI.SetSize(RectTransform, float, float) 的参数。
        lineUnit = ReadLineUnit();
        float thickness = Mathf.Max(1f, currentConfig.thickness) * lineUnit;
        // length（长度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 CrosshairUI.SetSize(RectTransform, float, float) 的参数。
        float length = Mathf.Max(2f, currentConfig.lineLength) * lineUnit;
        SetSize(lineTop, thickness, length);
        SetSize(lineBottom, thickness, length);
        SetSize(lineLeft, length, thickness);
        SetSize(lineRight, length, thickness);
        if (centerDot != null)
        {
            centerDot.sizeDelta = Vector2.one * Mathf.Max(2f, currentConfig.minSize);
        }
    }

    /// <summary>SetVisualState 的业务处理入口；本方法直接依赖 CrosshairUI.SetColor(RectTransform, Color)、CrosshairUI.SetGraphicEnabled(RectTransform, bool)、Mathf.Abs(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Start()、CrosshairUI.Update()；下游（含内部回调体） CrosshairUI.SetColor(RectTransform, Color)、CrosshairUI.SetGraphicEnabled(RectTransform, bool)、Mathf.Abs(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">中文含义：是否可见。使用位置：CrosshairUI.SetVisualState(bool, bool)。</param>
    /// <param name="immediate">中文含义：是否立即。使用位置：CrosshairUI.SetVisualState(bool, bool)。</param>
    void SetVisualState(bool visible, bool immediate)
    {
        // type（当前准星类型）：使用位置：CrosshairUI.SetVisualState(bool, bool)。
CrosshairType type;
        if (currentConfig != null)
        {
            type = currentConfig.crosshairType;
        }
        else
        {
            type = CrosshairType.None;
        }

        // showShotgunAmmo（是否显示霰弹枪弹药）：使用位置：CrosshairUI.SetVisualState(bool, bool)。
bool showShotgunAmmo = visible && !guardReticleActive && currentWeaponType == WeaponType.Shotgun;
        // showDot（是否显示点积）：使用位置：作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、CrosshairUI.SetVisualState(bool, bool)。
bool showDot = visible && !showShotgunAmmo && (guardReticleActive || type == CrosshairType.Dot);
        // showCross（是否显示跨越）：使用位置：作为 CrosshairUI.SetGraphicEnabled(RectTransform, bool) 的参数、CrosshairUI.SetVisualState(bool, bool)。
bool showCross = visible && !guardReticleActive && !showShotgunAmmo && type == CrosshairType.Cross;
        if (shotgunAmmoCrosshair != null)
        {
            shotgunAmmoCrosshair.enabled = showShotgunAmmo;
        }

        // targetAlpha（目标透明度）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CrosshairUI.SetVisualState(bool, bool)。
float targetAlpha;
        if (visible)
        {
            targetAlpha = 1f;
        }
        else
        {
            targetAlpha = 0f;
        }

        if (immediate)
        {
            displayedAlpha = targetAlpha;
        }
        else
        {
            displayedAlpha = Mathf.Lerp(displayedAlpha, targetAlpha, Time.deltaTime * alphaSmoothSpeed);
        }

        if (Mathf.Abs(displayedAlpha - targetAlpha) < 0.001f)
        {
            displayedAlpha = targetAlpha;
        }

        // alphaReachedTarget（是否透明度已抵达目标）：使用位置：CrosshairUI.SetVisualState(bool, bool)。
bool alphaReachedTarget = displayedAlpha == targetAlpha;
        if (canvasGroup != null && canvasGroup.alpha != displayedAlpha && (alphaReachedTarget || Mathf.Abs(canvasGroup.alpha - displayedAlpha) > 0.001f))
        {
            canvasGroup.alpha = displayedAlpha;
        }

        // visualStateChanged（视觉表现状态是否发生变化）：使用位置：CrosshairUI.SetVisualState(bool, bool)。
bool visualStateChanged = !visualStateInitialized || previousShowDot != showDot || previousShowCross != showCross;
        if (visualStateChanged)
        {
            SetGraphicEnabled(centerDot, showDot);
            SetGraphicEnabled(lineTop, showCross);
            SetGraphicEnabled(lineBottom, showCross);
            SetGraphicEnabled(lineLeft, showCross);
            SetGraphicEnabled(lineRight, showCross);
            previousShowDot = showDot;
            previousShowCross = showCross;
            visualStateInitialized = true;
        }

        // 隐藏后只保留透明度和后坐扩散收敛，避免继续写入图形颜色。
        if (!visible)
        {
            return;
        }

        // color（颜色）：使用位置：作为 CrosshairUI.SetColor(RectTransform, Color) 的参数、CrosshairUI.SetVisualState(bool, bool)。
Color color;
        if (guardReticleActive)
        {
            color = Color.white;
        }
        else if (currentConfig != null)
        {
            color = currentConfig.baseColor;
        }
        else
        {
            color = fallbackColor;
        }

        if (shotgunAmmoCrosshair != null)
        {
            shotgunAmmoCrosshair.color = color;
        }

        color.a *= displayedAlpha;
        SetColor(centerDot, color);
        SetColor(lineTop, color);
        SetColor(lineBottom, color);
        SetColor(lineLeft, color);
        SetColor(lineRight, color);
    }

    /// <summary>EnsureVisuals 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;ShotgunArcCrosshair&gt;()、Component.GetComponentInParent&lt;Canvas&gt;()、Component.TryGetComponent&lt;Shadow&gt;(out Shadow)、CrosshairUI.CreatePart(Transform, string)、Debug.LogError(object, Object) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;ShotgunArcCrosshair&gt;()、Component.GetComponentInParent&lt;Canvas&gt;()、Component.TryGetComponent&lt;Shadow&gt;(out Shadow)、CrosshairUI.CreatePart(Transform, string)、Debug.LogError(object, Object) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void EnsureVisuals()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas == null)
        {
            Debug.LogError("[准心界面] 未设置所属画布引用，且父级没有画布；已停止创建准心视觉节点。", this);
            return;
        }

        // visualParent（视觉表现父级）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数、CrosshairUI.EnsureVisuals()。
Transform visualParent = targetCanvas.transform;
        // panel（面板）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 CrosshairUI.CreatePart(Transform, string) 的参数、作为 Transform.IsChildOf(Transform) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 4 处。
Transform panel = transform.Find("统一准心显示");
        if (panel == null && visualParent != transform)
        {
            panel = visualParent.Find("统一准心显示");
        }

        if (panel == null)
        {
            // panelObject（面板对象）：使用位置：CrosshairUI.EnsureVisuals()。
GameObject panelObject = new GameObject("统一准心显示", typeof(RectTransform), typeof(CanvasGroup));
            panelObject.transform.SetParent(visualParent, false);
            panel = panelObject.transform;
            // panelRect（面板矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：CrosshairUI.EnsureVisuals()。
RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
        else if (panel.parent != visualParent && !visualParent.IsChildOf(panel))
        {
            panel.SetParent(visualParent, false);
        }

        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (lineTop == null)
        {
            lineTop = CreatePart(panel, "上");
        }

        if (lineBottom == null)
        {
            lineBottom = CreatePart(panel, "下");
        }

        if (lineLeft == null)
        {
            lineLeft = CreatePart(panel, "左");
        }

        if (lineRight == null)
        {
            lineRight = CreatePart(panel, "右");
        }

        if (centerDot == null)
        {
            centerDot = CreatePart(panel, "中心点");
        }

        // arcNode（弧线节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.EnsureVisuals()。
Transform arcNode = panel.Find("霰弹枪弧线");
        if (arcNode == null)
        {
            // node（节点）：使用位置：CrosshairUI.EnsureVisuals()。
GameObject node = new GameObject("霰弹枪弧线", typeof(RectTransform), typeof(CanvasRenderer), typeof(ShotgunArcCrosshair));
            node.transform.SetParent(panel, false);
            arcNode = node.transform;
        }

        shotgunAmmoCrosshair = arcNode.GetComponent<ShotgunArcCrosshair>();
        shotgunAmmoCrosshair.rectTransform.anchorMin = shotgunAmmoCrosshair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        shotgunAmmoCrosshair.sprite = Resources.Load<Sprite>("UI/W870Reticle");
        shotgunAmmoCrosshair.raycastTarget = false;
        shotgunAmmoCrosshair.enabled = false;
        // lineSegment（线线段）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：CrosshairUI.EnsureVisuals()。
foreach (RectTransform lineSegment in new[]
        {
            lineTop,
            lineBottom,
            lineLeft,
            lineRight
        }

        )
        {
            if (!lineSegment.TryGetComponent(out Shadow /* 中文含义：投影。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ projection))
            {
                projection = lineSegment.gameObject.AddComponent<Shadow>();
            }

            projection.effectColor = new Color(0f, 0f, 0f, 0.48f);
            projection.effectDistance = new Vector2(0.6f, -0.6f);
        }
    }

    /// <summary>CreatePart 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.EnsureVisuals()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="partName">中文含义：部件名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    RectTransform CreatePart(Transform parent, string partName)
    {
        // part（部件）：使用位置：CrosshairUI.CreatePart(Transform, string)。
GameObject part = new GameObject(partName, typeof(RectTransform), typeof(Image));
        part.transform.SetParent(parent, false);
        // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：CrosshairUI.CreatePart(Transform, string)。
RectTransform rect = part.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：CrosshairUI.CreatePart(Transform, string)。
Image image = part.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = fallbackColor;
        return rect;
    }

    /// <summary>DisableCompetingCrosshairs 的业务处理入口；本方法直接依赖 CombatHUD.DisableLegacyCrosshair()、Component.GetComponentsInChildren&lt;CircleRenderer&gt;(bool)、Component.GetComponentsInChildren&lt;CrossRenderer&gt;(bool)、Component.GetComponentsInChildren&lt;DotRenderer&gt;(bool)、CrosshairUI.HideLegacyPart(RectTransform)、CrosshairUI.HideOwnedVisuals()、GameObject.SetActive(bool) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Start()；下游（含内部回调体） CombatHUD.DisableLegacyCrosshair()、Component.GetComponentsInChildren&lt;CircleRenderer&gt;(bool)、Component.GetComponentsInChildren&lt;CrossRenderer&gt;(bool)、Component.GetComponentsInChildren&lt;DotRenderer&gt;(bool)、CrosshairUI.HideLegacyPart(RectTransform)、CrosshairUI.HideOwnedVisuals()、GameObject.SetActive(bool) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void DisableCompetingCrosshairs()
    {
        // allControllers（全部控制器集合）：保存 Object.FindObjectsByType<CrosshairUI>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrosshairUI[] allControllers = FindObjectsByType<CrosshairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // i（当前遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int i = 0; i < allControllers.Length; i++)
        {
            // other（另一个准星界面）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrosshairUI other = allControllers[i];
            if (other == this)
            {
                continue;
            }

            other.HideOwnedVisuals();
            other.enabled = false;
        }

        // oldCrosshairs（旧准星集合）：保存 Object.FindObjectsByType<CrosshairPresenterValue>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrosshairPresenterValue[] oldCrosshairs = FindObjectsByType<CrosshairPresenterValue>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // i（当前遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int i = 0; i < oldCrosshairs.Length; i++)
        {
            // oldCrosshair（旧准星）：使用位置：作为 CrosshairUI.HideLegacyPart(RectTransform) 的参数、CrosshairUI.DisableCompetingCrosshairs()。
CrosshairPresenterValue oldCrosshair = oldCrosshairs[i];
            if (oldCrosshair == null)
            {
                continue;
            }

            oldCrosshair.enabled = false;
            HideLegacyPart(oldCrosshair.Top);
            HideLegacyPart(oldCrosshair.Bottom);
            HideLegacyPart(oldCrosshair.Left);
            HideLegacyPart(oldCrosshair.Right);
            HideLegacyPart(oldCrosshair.Center);
        }

        // presenters（显示器集合）：保存 Object.FindObjectsByType<CrosshairPresenter>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrosshairPresenter[] presenters = FindObjectsByType<CrosshairPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // i（当前遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int i = 0; i < presenters.Length; i++)
        {
            // presenter（显示器）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrosshairPresenter presenter = presenters[i];
            if (presenter == null)
            {
                continue;
            }

            presenter.enabled = false;
            // crossRenderers（跨越渲染器集合）：保存 Component.GetComponentsInChildren<CrossRenderer>(bool) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CrossRenderer[] crossRenderers = presenter.GetComponentsInChildren<CrossRenderer>(true);
            // dotRenderers（点积渲染器集合）：保存 Component.GetComponentsInChildren<DotRenderer>(bool) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
DotRenderer[] dotRenderers = presenter.GetComponentsInChildren<DotRenderer>(true);
            // circleRenderers（圆形渲染器集合）：保存 Component.GetComponentsInChildren<CircleRenderer>(bool) 的返回结果，供当前步骤使用。使用位置：CrosshairUI.DisableCompetingCrosshairs()。
CircleRenderer[] circleRenderers = presenter.GetComponentsInChildren<CircleRenderer>(true);
            // j（内层遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int j = 0; j < crossRenderers.Length; j++)
            {
                crossRenderers[j].gameObject.SetActive(false);
            }

            // j（内层遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int j = 0; j < dotRenderers.Length; j++)
            {
                dotRenderers[j].gameObject.SetActive(false);
            }

            // j（内层遍历索引）：使用位置：CrosshairUI.DisableCompetingCrosshairs()。
for (int j = 0; j < circleRenderers.Length; j++)
            {
                circleRenderers[j].gameObject.SetActive(false);
            }
        }

        if (CombatHUD.Instance != null)
        {
            CombatHUD.Instance.DisableLegacyCrosshair();
        }
    }

    /// <summary>HideLegacyPart 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()、CrosshairUI.IsOwnedPart(RectTransform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.DisableCompetingCrosshairs()；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、CrosshairUI.IsOwnedPart(RectTransform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="part">中文含义：部件。使用位置：作为 CrosshairUI.IsOwnedPart(RectTransform) 的参数、CrosshairUI.HideLegacyPart(RectTransform)。</param>
    void HideLegacyPart(RectTransform part)
    {
        if (part == null || IsOwnedPart(part))
        {
            return;
        }

        // image（图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：CrosshairUI.HideLegacyPart(RectTransform)。
Image image = part.GetComponent<Image>();
        if (image != null)
        {
            image.enabled = false;
        }
    }

    /// <summary>IsOwnedPart 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.HideLegacyPart(RectTransform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="part">中文含义：部件。使用位置：CrosshairUI.IsOwnedPart(RectTransform)。</param>
    bool IsOwnedPart(RectTransform part)
    {
        return part == lineTop || part == lineBottom || part == lineLeft || part == lineRight || part == centerDot;
    }

    /// <summary>HideOwnedVisuals 的业务处理入口；本方法直接依赖 CrosshairUI.SetGraphicEnabled(RectTransform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.Awake()、CrosshairUI.DisableCompetingCrosshairs()；下游（含内部回调体） CrosshairUI.SetGraphicEnabled(RectTransform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HideOwnedVisuals()
    {
        if (shotgunAmmoCrosshair != null)
        {
            shotgunAmmoCrosshair.enabled = false;
        }

        SetGraphicEnabled(lineTop, false);
        SetGraphicEnabled(lineBottom, false);
        SetGraphicEnabled(lineLeft, false);
        SetGraphicEnabled(lineRight, false);
        SetGraphicEnabled(centerDot, false);
    }

    /// <summary>SetPosition 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.UpdateSpread()；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：CrosshairUI.SetPosition(RectTransform, float, float)。</param>
    /// <param name="x">中文含义：横向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    void SetPosition(RectTransform rect, float x, float y)
    {
        if (rect != null)
        {
            rect.anchoredPosition = new Vector2(x, y);
        }
    }

    /// <summary>SetSize 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.ApplyStyle()；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：CrosshairUI.SetSize(RectTransform, float, float)。</param>
    /// <param name="width">中文含义：宽度。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="height">中文含义：高度。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    float ReadLineUnit()
    {
        if (currentConfig.lineSprite == null) return 1f;
        return ReadCanvasUnit(lineTop != null ? lineTop.parent : transform);
    }

    float ReadCanvasUnit(Transform parent)
    {
        if (targetCanvas == null) return 1f;
        var canvas = targetCanvas.rootCanvas;
        float panelScale = canvas.transform.InverseTransformVector(parent.TransformVector(Vector3.up)).magnitude;
        return canvas.pixelRect.height / (1080f * Mathf.Max(.001f, canvas.scaleFactor * panelScale));
    }

    void SetSize(RectTransform rect, float width, float height)
    {
        if (rect == null) return;
        rect.sizeDelta = new Vector2(width, height);
        if (!rect.TryGetComponent<Image>(out var image)) return;
        var sprite = currentConfig.lineSprite;
        image.overrideSprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        if (rect.TryGetComponent<Shadow>(out var shadow)) shadow.enabled = sprite == null;
        // 原图块的十个边缘纹素在 1080p 占一个像素。
        image.pixelsPerUnitMultiplier = sprite != null && targetCanvas != null
            ? 10f * targetCanvas.rootCanvas.referencePixelsPerUnit / (sprite.pixelsPerUnit * Mathf.Max(.001f, lineUnit))
            : 1f;
    }

    /// <summary>SetGraphicEnabled 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.HideOwnedVisuals()、CrosshairUI.SetVisualState(bool, bool)；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：CrosshairUI.SetGraphicEnabled(RectTransform, bool)。</param>
    /// <param name="value">中文含义：是否值。使用位置：CrosshairUI.SetGraphicEnabled(RectTransform, bool)。</param>
    void SetGraphicEnabled(RectTransform rect, bool value)
    {
        // image（图像）：使用位置：CrosshairUI.SetGraphicEnabled(RectTransform, bool)。
Image image;
        if (rect != null)
        {
            image = rect.GetComponent<Image>();
        }
        else
        {
            image = null;
        }

        if (image != null && image.enabled != value)
        {
            image.enabled = value;
        }
    }

    /// <summary>SetColor 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.SetVisualState(bool, bool)；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：CrosshairUI.SetColor(RectTransform, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：CrosshairUI.SetColor(RectTransform, Color)。</param>
    void SetColor(RectTransform rect, Color color)
    {
        // image（图像）：使用位置：CrosshairUI.SetColor(RectTransform, Color)。
Image image;
        if (rect != null)
        {
            image = rect.GetComponent<Image>();
        }
        else
        {
            image = null;
        }

        if (image != null && image.color != color)
        {
            image.color = color;
        }
    }
}
