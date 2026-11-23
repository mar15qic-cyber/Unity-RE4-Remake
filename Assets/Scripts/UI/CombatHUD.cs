using UnityEngine;
using UnityEngine.UI;

/// <summary>界面交互与显示组件 CombatHUD，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>正式生命、弹药、武器与小刀状态显示宿主，读取角色和武器已提交状态并更新缓存与图形，不拥有伤害或库存结算。</remarks>
[DefaultExecutionOrder(10100)]
public class CombatHUD : Singleton<CombatHUD>
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：激光点积。使用位置：CombatHUD.DisableLegacyCrosshair()、CombatHUD.SetLaserVisible(bool)。</summary>
    [Header("Laser Dot")]
    public RectTransform laserDot;
    /// <summary>中文含义：激光点积基础尺寸。</summary>
    public float laserDotBaseSize = 6f;
    /// <summary>中文含义：激光点积最大尺寸。</summary>
    public float laserDotMaxSize = 18f;
    /// <summary>中文含义：散布平滑速度。</summary>
    public float spreadSmoothSpeed = 8f;
    /// <summary>中文含义：弹药分组。</summary>
    [Header("Ammo Display")]
    public CanvasGroup ammoGroup;
    /// <summary>中文含义：弹药文字。使用位置：作为 CombatHUD.ConfigureAmmoText(Text) 的参数、CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.TryInitializeHealthGauge()。</summary>
    public Text ammoText;
    /// <summary>中文含义：武器名称文字。使用位置：作为 CombatHUD.ConfigureWeaponName(Text) 的参数、CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.RefreshAmmoText()。</summary>
    public Text weaponNameText;
    /// <summary>中文含义：弹药显示持续时间。</summary>
    public float ammoShowDuration = 3f;
    /// <summary>中文含义：弹药淡入淡出速度。</summary>
    public float ammoFadeSpeed = 4f;
    /// <summary>中文含义：武器图标。使用位置：作为 CombatHUD.ConfigureWeaponIcon(Image) 的参数、CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.TryInitializeHealthGauge()。</summary>
    [Header("武器图标")]
    public Image weaponIcon;
    /// <summary>中文含义：手枪图标。使用位置：CombatHUD.ResolveWeaponSprite(WeaponType, Sprite)、WeaponWheelUI.ResolveIcon(WeaponWheelUI.WeaponSlot, WeaponBase)。</summary>
    public Sprite pistolIcon;
    /// <summary>中文含义：步枪图标。使用位置：CombatHUD.ResolveWeaponSprite(WeaponType, Sprite)、WeaponWheelUI.ResolveIcon(WeaponWheelUI.WeaponSlot, WeaponBase)。</summary>
    public Sprite rifleIcon;
    /// <summary>中文含义：霰弹枪图标。使用位置：CombatHUD.ResolveWeaponSprite(WeaponType, Sprite)、WeaponWheelUI.ResolveIcon(WeaponWheelUI.WeaponSlot, WeaponBase)。</summary>
    public Sprite shotgunIcon;
    /// <summary>中文含义：小刀图标。使用位置：CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.ConfigureKnifeStatus()、CombatHUD.ResolveWeaponSprite(WeaponType, Sprite) 等共 5 处。</summary>
    public Sprite knifeIcon;
    /// <summary>中文含义：小刀状态精灵图。右下角状态栏使用的战斗小刀切片。为空时会从现有界面图集自动加载。使用位置：CombatHUD.ConfigureKnifeStatus()、CombatHUD.LoadKnifeStatusSprites()、CombatHUD.UpdateKnifeDurability()。</summary>
    [Tooltip("右下角状态栏使用的战斗小刀切片。为空时会从现有界面图集自动加载。")]
    public Sprite knifeStatusSprite;
    /// <summary>中文含义：损坏小刀状态精灵图。小刀破损后使用的状态栏切片。为空时会沿用战斗小刀切片并降低亮度。使用位置：CombatHUD.LoadKnifeStatusSprites()、CombatHUD.UpdateKnifeDurability()。</summary>
    [Tooltip("小刀破损后使用的状态栏切片。为空时会沿用战斗小刀切片并降低亮度。")]
    public Sprite brokenKnifeStatusSprite;
    /// <summary>中文含义：血迹暗角。使用位置：CombatHUD.Start()、CombatHUD.UpdateBloodVignette()。</summary>
    [Header("Blood Vignette")]
    public CanvasGroup bloodVignette;
    /// <summary>中文含义：最大暗角透明度。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    public float maxVignetteAlpha = 0.2f;
    /// <summary>中文含义：暗角平滑速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    public float vignetteSmoothSpeed = 3f;
    /// <summary>中文含义：里昂。使用位置：作为 CombatHUD.UpdateHealthFill(float, float, bool) 的参数、作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Max(float, float) 的参数 等共 10 处。</summary>
    private LeonController leon;
    /// <summary>中文含义：管理当前装备与武器切换的组件。使用位置：CombatHUD.RefreshAmmoText()、CombatHUD.TryInitializeHealthGauge()、CombatHUD.UpdateAmmoDisplay()。</summary>
    private LeonWeaponHandler weaponHandler;
    private const float GaugeHoldDuration = 2.5f;
    private const float GaugeFadeDuration = 0.25f;
    private const float GaugeFadeInDuration = 0.10f;
    private const float KnifeLossDuration = 0.38f;
    private const float KnifeBreakDuration = 0.5f;
    private static readonly Vector2 KnifeIconPosition = new Vector2(-57f, 58f);
    private const float KnifeBarWidth = 42f;
    private CanvasGroup gaugeGroup;
    private WeaponWheelUI weaponWheel;
    private InventoryManager observedInventory;
    private GridItem observedKnife;
    private float lastHealth;
    private float revealUntil;
    private bool wasBlocked;
    private RectTransform inventoryGaugeParent;
    private Transform gameplayGaugeParent;
    private int gameplayGaugeSibling;
    private float cachedHealthMaximum;
    private const float BaseHealthSweep = 168f;
    private const float MaximumHealthSweep = 270f;
    private WeaponType lastFirearm;
    private bool hasLastFirearm;
    private Image knifeTrack;
    private Image knifeLoss;
    private Image knifeBreakGlow;
    private Image knifeBrokenMark;
    private float knifeRatio;
    private float knifeLossStart;
    private float knifeLossEnd;
    private float knifeLossUntil;
    private float knifeBreakUntil;
    private bool hasBrokenKnife;
    /// <summary>中文含义：命中闪光透明度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数、CombatHUD.HitFlash() 等共 4 处。</summary>
    [Header("受击闪烁")]
    private float hitFlashAlpha;
    /// <summary>中文含义：命中闪光淡入淡出速度。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。</summary>
    public float hitFlashFadeSpeed = 3f;
    /// <summary>中文含义：命中闪光最大透明度。使用位置：CombatHUD.HitFlash()。</summary>
    [Range(0f, 0.5f)]
    public float hitFlashMaxAlpha = 0.3f;
    /// <summary>中文含义：血量环外框（Sprite 68）。使用位置：作为 CombatHUD.StretchToParent(RectTransform) 的参数、CombatHUD.BuildOrRepairHealthGauge()。</summary>
    [Header("血量环 UI")]
    [Tooltip("血量环外框（Sprite 68）")]
    public Image healthFrame;
    /// <summary>中文含义：血量环填充（Sprite 69）。使用位置：CombatHUD.BuildOrRepairHealthGauge()。</summary>
    [Tooltip("血量环填充（Sprite 69）")]
    public Image healthFill;
    /// <summary>中文含义：血量环平滑变化速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Tooltip("血量环平滑变化速度")]
    public float healthLerpSpeed = 3f;
    /// <summary>中文含义：血量环尺寸（像素）。使用位置：CombatHUD.BuildOrRepairHealthGauge()。</summary>
    [Tooltip("血量环尺寸（像素）")]
    public float healthGaugeSize = 140f;
    /// <summary>中文含义：血量环相对屏幕右下角的偏移。使用位置：CombatHUD.BuildOrRepairHealthGauge()。</summary>
    [Tooltip("血量环相对屏幕右下角的偏移")]
    public Vector2 healthGaugeOffset = new Vector2(-134f, 138f);
    /// <summary>中文含义：低血量时填充颜色。使用位置：CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    [Tooltip("低血量时填充颜色")]
    public Color healthLowColor = new Color(0.8f, 0.1f, 0.1f, 1f);
    /// <summary>中文含义：正常血量时填充颜色。使用位置：作为 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float) 的参数、CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    [Tooltip("正常血量时填充颜色")]
    public Color healthNormalColor = new Color(0.48f, 0.70f, 0.38f, 0.96f);
    /// <summary>中文含义：生命武器环总根节点，留空时会在运行时自动建立。使用位置：作为 CombatHUD.CreateImage(string, RectTransform) 的参数、作为 CombatHUD.CreateRing(string, RectTransform) 的参数、作为 CombatHUD.CreateText(string, RectTransform, int) 的参数 等共 5 处。</summary>
    [Header("RE4 风格生命武器环")]
    [Tooltip("生命武器环总根节点，留空时会在运行时自动建立。")]
    public RectTransform healthGaugeRoot;
    /// <summary>中文含义：暗色的完整圆弧背景。使用位置：作为 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float) 的参数、CombatHUD.BuildOrRepairHealthGauge()。</summary>
    [Tooltip("暗色的完整圆弧背景。")]
    public HealthRingGraphic healthRingBackground;
    /// <summary>中文含义：随当前血量线性缩短的绿色圆弧。使用位置：作为 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float) 的参数、CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.UpdateHealthFill(float, float, bool) 等共 4 处。</summary>
    [Tooltip("随当前血量线性缩短的绿色圆弧。")]
    public HealthRingGraphic healthRingValue;
    /// <summary>中文含义：常驻的小刀状态图标，后续可接入耐久度。使用位置：CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.ConfigureKnifeStatus()、CombatHUD.UpdateKnifeDurability()。</summary>
    [Tooltip("常驻的小刀状态图标，后续可接入耐久度。")]
    public Image knifeStatusIcon;
    /// <summary>中文含义：小刀耐久度线，当前默认显示满耐久。使用位置：CombatHUD.ConfigureKnifeStatus()、CombatHUD.UpdateKnifeDurability()。</summary>
    [Tooltip("小刀耐久度线，当前默认显示满耐久。")]
    public Image knifeStatusLine;
    /// <summary>中文含义：生命环背景颜色。使用位置：作为 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float) 的参数。</summary>
    public Color healthRingBackgroundColor = new Color(0.62f, 0.65f, 0.67f, 0.48f);
    /// <summary>中文含义：生命环厚度。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。</summary>
    public float healthRingThickness = 8f;
    /// <summary>中文含义：招架提示分组。使用位置：CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.UpdateParryPrompt()。</summary>
    [Header("弹反提示")]
    public CanvasGroup parryPromptGroup;
    /// <summary>中文含义：招架提示键标签。使用位置：作为 CombatHUD.StretchToParent(RectTransform) 的参数、CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.UpdateParryPrompt()。</summary>
    public Text parryPromptKeyLabel;
    private Image parryKeyImage;
    private KnifeParryPromptKind shownParryPrompt;
    /// <summary>中文含义：招架提示动作标签。使用位置：CombatHUD.BuildOrRepairParryPrompt()。</summary>
    public Text parryPromptActionLabel;
    /// <summary>中文含义：招架提示小刀图标。使用位置：CombatHUD.BuildOrRepairParryPrompt()。</summary>
    public Image parryPromptKnifeIcon;
    /// <summary>中文含义：招架提示偏移量。使用位置：CombatHUD.BuildOrRepairParryPrompt()。</summary>
    public Vector2 parryPromptOffset = new Vector2(-255f, 150f);
    /// <summary>中文含义：招架提示颜色。使用位置：CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.UpdateParryPrompt()。</summary>
    public Color parryPromptColor = new Color(0.96f, 0.97f, 0.98f, 0.96f);
    /// <summary>中文含义：显示生命。使用位置：作为 CombatHUD.UpdateHealthFill(float, float, bool) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 6 处。</summary>
    private float displayHealth;
    /// <summary>中文含义：内部武器图标。使用位置：CombatHUD.RefreshAmmoText()、CombatHUD.TryInitializeHealthGauge()。</summary>
    private Image innerWeaponIcon;
    /// <summary>中文含义：内部弹药文字。使用位置：CombatHUD.RefreshAmmoText()、CombatHUD.TryInitializeHealthGauge()。</summary>
    private Text innerAmmoText;
    /// <summary>中文含义：生命计量条是否已初始化。使用位置：CombatHUD.TryInitializeHealthGauge()、CombatHUD.Update()。</summary>
    private bool healthGaugeInitialized;
    // 战斗信息缓存：只有原始数据变化时才更新文本和图标。
    /// <summary>中文含义：弹药缓存是否已初始化。使用位置：CombatHUD.RefreshAmmoText()、CombatHUD.Update()。</summary>
    private bool ammoCacheInitialized;
    /// <summary>中文含义：缓存当前弹匣弹量。使用位置：CombatHUD.RefreshAmmoText()。</summary>
    private int cachedCurrentAmmo;
    /// <summary>中文含义：缓存背包备弹。使用位置：CombatHUD.RefreshAmmoText()。</summary>
    private int cachedReserveAmmo;
    /// <summary>中文含义：缓存弹药武器类型。使用位置：CombatHUD.RefreshAmmoText()。</summary>
    private WeaponType cachedAmmoWeaponType;
    /// <summary>中文含义：缓存弹药武器。使用位置：CombatHUD.RefreshAmmoText()。</summary>
    private WeaponBase cachedAmmoWeapon;
    /// <summary>中文含义：缓存弹药精灵图。使用位置：CombatHUD.RefreshAmmoText()。</summary>
    private Sprite cachedAmmoSprite;
    /// <summary>中文含义：缓存生命比例。使用位置：作为 Mathf.Abs(float) 的参数、CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    private float cachedHealthRatio = -1f;
    /// <summary>中文含义：缓存生命颜色。使用位置：CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    private Color cachedHealthColor;
    /// <summary>中文含义：是否缓存生命所在目标。使用位置：CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    private bool cachedHealthAtTarget;
    /// <summary>中文含义：生命视觉表现缓存是否已初始化。使用位置：CombatHUD.Update()、CombatHUD.UpdateHealthFill(float, float, bool)。</summary>
    private bool healthVisualCacheInitialized;
    /// <summary>中文含义：战斗。使用位置：CombatHUD.TryInitializeHealthGauge()、CombatHUD.UpdateParryPrompt()。</summary>
    private LeonCombat combat;
    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Singleton&lt;CombatHUD&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        if (leon != null)
        {
            leon.OnHealthChanged -= OnHealthChanged;
        }

        UnbindInventory();
        base.OnDestroy();
    }

    private void OnEnable()
    {
        if (healthGaugeInitialized && leon != null)
        {
            lastHealth = leon.currentHealth;
            leon.OnHealthChanged += OnHealthChanged;
            wasBlocked = true;
        }
    }

    private void OnDisable()
    {
        if (healthGaugeInitialized && leon != null)
        {
            leon.OnHealthChanged -= OnHealthChanged;
        }
        RestoreGameplayGauge();
        UnbindInventory();
        revealUntil = 0f;
        if (gaugeGroup != null)
        {
            gaugeGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 受击瞬间触发血红闪烁
    /// </summary>
    /// <remarks>静态调用链：上游 CombatHUD.OnHealthChanged(float, float)、LeonController.PlayDamageFeedback(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HitFlash()
    {
        hitFlashAlpha = hitFlashMaxAlpha;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CombatHUD.DisableLegacyCrosshair()、CombatHUD.LoadKnifeStatusSprites()、CombatHUD.TryInitializeHealthGauge()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        LoadKnifeStatusSprites();
        if (bloodVignette)
        {
            bloodVignette.alpha = 0f;
            bloodVignette.blocksRaycasts = false;
        }

        DisableLegacyCrosshair();
        TryInitializeHealthGauge();
    }

    /// <summary>LoadKnifeStatusSprites 的业务处理入口；本方法直接依赖 Resources.LoadAll&lt;Sprite&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Start()；下游（含内部回调体） Resources.LoadAll&lt;Sprite&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadKnifeStatusSprites()
    {
        Sprite nativeKnife = GetNativeHudSprite("CombatKnife");
        if (nativeKnife != null)
        {
            knifeStatusSprite = nativeKnife;
        }

        Sprite nativeBrokenKnife = GetNativeHudSprite("BrokenKnife");
        if (nativeBrokenKnife != null)
        {
            brokenKnifeStatusSprite = nativeBrokenKnife;
        }
    }

    /// <summary>TryInitializeHealthGauge 的业务处理入口；本方法直接依赖 CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.RefreshAmmoText()、CombatHUD.UpdateHealthFill(float, float, bool)、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、GameObject.FindGameObjectWithTag(string)、GameObject.GetComponent&lt;LeonController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Start()、CombatHUD.Update()；下游（含内部回调体） CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.RefreshAmmoText()、CombatHUD.UpdateHealthFill(float, float, bool)、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、GameObject.FindGameObjectWithTag(string)、GameObject.GetComponent&lt;LeonController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void TryInitializeHealthGauge()
    {
        if (healthGaugeInitialized)
        {
            return;
        }

        if (leon == null)
        {
            leon = LeonController.Instance;
            if (leon == null)
            {
                // playerObject（玩家对象）：保存 GameObject.FindGameObjectWithTag(string) 的返回结果，供当前步骤使用。使用位置：CombatHUD.TryInitializeHealthGauge()。
GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    leon = playerObject.GetComponent<LeonController>();
                }
            }
        }

        if (leon != null && weaponHandler == null)
        {
            weaponHandler = leon.GetComponent<LeonWeaponHandler>();
        }

        if (leon != null && combat == null)
        {
            combat = leon.GetComponent<LeonCombat>();
        }

        if (leon == null || weaponHandler == null)
        {
            return;
        }

        weaponWheel = leon.GetComponent<WeaponWheelUI>();
        BuildOrRepairHealthGauge();
        innerWeaponIcon = weaponIcon;
        innerAmmoText = ammoText;
        displayHealth = leon.currentHealth;
        lastHealth = displayHealth;
        leon.OnHealthChanged += OnHealthChanged;
        UpdateHealthFill(leon.currentHealth, ReadHealthMaximum(), true);
        RefreshAmmoText();
        healthGaugeInitialized = true;
    }

    /// <summary>BuildOrRepairHealthGauge 的业务处理入口；本方法直接依赖 CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.ConfigureAmmoText(Text)、CombatHUD.ConfigureKnifeStatus()、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、CombatHUD.ConfigureWeaponIcon(Image)、CombatHUD.ConfigureWeaponName(Text)、CombatHUD.CreateImage(string, RectTransform) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.TryInitializeHealthGauge()；下游（含内部回调体） CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.ConfigureAmmoText(Text)、CombatHUD.ConfigureKnifeStatus()、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、CombatHUD.ConfigureWeaponIcon(Image)、CombatHUD.ConfigureWeaponName(Text)、CombatHUD.CreateImage(string, RectTransform) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildOrRepairHealthGauge()
    {
        if (healthGaugeRoot == null)
        {
            // existing（已有）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：CombatHUD.BuildOrRepairHealthGauge()。
Transform existing = transform.Find("RE4生命武器环");
            if (existing != null)
            {
                healthGaugeRoot = existing.GetComponent<RectTransform>();
            }
        }

        if (healthGaugeRoot == null)
        {
            // root（根游戏对象）：使用位置：CombatHUD.BuildOrRepairHealthGauge()。
GameObject root = new GameObject("RE4生命武器环", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            healthGaugeRoot = root.GetComponent<RectTransform>();
        }

        // bottomRight（右下角）：使用位置：CombatHUD.BuildOrRepairHealthGauge()。
Vector2 bottomRight = new Vector2(1f, 0f);
        healthGaugeRoot.anchorMin = bottomRight;
        healthGaugeRoot.anchorMax = bottomRight;
        healthGaugeRoot.pivot = bottomRight;
        // resolvedSize（解算后的尺寸）：使用位置：CombatHUD.BuildOrRepairHealthGauge()。
float resolvedSize;
        if (healthGaugeSize < 140f || healthGaugeSize > 360f)
        {
            resolvedSize = 140f;
            healthNormalColor = new Color(0.48f, 0.70f, 0.38f, 0.96f);
        }
        else
        {
            resolvedSize = healthGaugeSize;
        }

        // resolvedOffset（解算后的偏移量）：使用位置：CombatHUD.BuildOrRepairHealthGauge()。
Vector2 resolvedOffset = healthGaugeOffset;
        // legacyOffset（是否旧版偏移量）：使用位置：CombatHUD.BuildOrRepairHealthGauge()。
bool legacyOffset = resolvedOffset.x > -16f || resolvedOffset.x < -240f || resolvedOffset.y < 16f || resolvedOffset.y > 240f;
        if (legacyOffset)
        {
            resolvedOffset = new Vector2(-134f, 138f);
        }

        healthGaugeRoot.anchoredPosition = resolvedOffset;
        healthGaugeRoot.sizeDelta = Vector2.one * resolvedSize;
        healthGaugeRoot.SetAsLastSibling();
        gaugeGroup = healthGaugeRoot.GetComponent<CanvasGroup>();
        if (gaugeGroup == null)
        {
            gaugeGroup = healthGaugeRoot.gameObject.AddComponent<CanvasGroup>();
        }
        gaugeGroup.alpha = 0f;
        gaugeGroup.interactable = false;
        gaugeGroup.blocksRaycasts = false;
        if (healthRingBackground == null)
        {
            healthRingBackground = CreateRing("生命环背景", healthGaugeRoot);
        }

        if (healthRingValue == null)
        {
            healthRingValue = CreateRing("生命环数值", healthGaugeRoot);
        }

        ConfigureRing(healthRingBackground, healthRingBackgroundColor, 1f);
        ConfigureRing(healthRingValue, healthNormalColor, 1f);
        if (healthFill != null)
        {
            healthFill.enabled = false;
        }

        if (healthFrame != null)
        {
            healthFrame.enabled = false;
        }

        if (weaponIcon == null)
        {
            weaponIcon = CreateImage("当前武器图标", healthGaugeRoot);
        }

        ConfigureWeaponIcon(weaponIcon);
        if (ammoText == null)
        {
            ammoText = CreateText("当前弹药", healthGaugeRoot, 30);
        }

        ConfigureAmmoText(ammoText);
        if (weaponNameText != null)
        {
            weaponNameText.enabled = false;
        }

        if (knifeStatusIcon == null)
        {
            knifeStatusIcon = CreateImage("小刀状态图标", healthGaugeRoot);
        }

        ConfigureKnifeStatus();
        BuildOrRepairParryPrompt();
    }

    /// <summary>CreateRing 的业务处理入口；本方法直接依赖 CombatHUD.StretchToParent(RectTransform)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;HealthRingGraphic&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） CombatHUD.StretchToParent(RectTransform)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;HealthRingGraphic&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    HealthRingGraphic CreateRing(string objectName, RectTransform parent)
    {
        // ring（环）：使用位置：CombatHUD.CreateRing(string, RectTransform)。
GameObject ring = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(HealthRingGraphic));
        ring.transform.SetParent(parent, false);
        // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 CombatHUD.StretchToParent(RectTransform) 的参数。
RectTransform rect = ring.GetComponent<RectTransform>();
        StretchToParent(rect);
        return ring.GetComponent<HealthRingGraphic>();
    }

    /// <summary>ConfigureRing 的业务处理入口；本方法直接依赖 CombatHUD.StretchToParent(RectTransform)、HealthRingGraphic.SetFillAmount(float)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） CombatHUD.StretchToParent(RectTransform)、HealthRingGraphic.SetFillAmount(float)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ring">中文含义：环。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。</param>
    /// <param name="color">中文含义：颜色。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。</param>
    /// <param name="fill">中文含义：填充。使用位置：作为 HealthRingGraphic.SetFillAmount(float) 的参数。</param>
    void ConfigureRing(HealthRingGraphic ring, Color color, float fill)
    {
        if (ring == null)
        {
            return;
        }

        // rect（矩形）：使用位置：作为 CombatHUD.StretchToParent(RectTransform) 的参数、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。
RectTransform rect = ring.rectTransform;
        rect.SetParent(healthGaugeRoot, false);
        StretchToParent(rect);
        ring.raycastTarget = false;
        ring.thickness = Mathf.Clamp(healthRingThickness, 6f, 8f);
        ring.startAngle = 90f;
        ring.sweepAngle = MaximumHealthSweep;
        ring.clockwise = true;
        ring.segmentCount = 112;
        ring.color = color;
        ring.SetFillAmount(fill);
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.ConfigureKnifeStatus()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    Image CreateImage(string objectName, RectTransform parent)
    {
        // imageObject（图像对象）：使用位置：CombatHUD.CreateImage(string, RectTransform)。
GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：CombatHUD.CreateImage(string, RectTransform)。
Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.BuildOrRepairParryPrompt()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="fontSize">中文含义：字体尺寸。使用位置：CombatHUD.CreateText(string, RectTransform, int)。</param>
    Text CreateText(string objectName, RectTransform parent, int fontSize)
    {
        // textObject（文字对象）：使用位置：CombatHUD.CreateText(string, RectTransform, int)。
GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：CombatHUD.CreateText(string, RectTransform, int)。
Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>ConfigureWeaponIcon 的业务处理入口；本方法直接依赖 Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="image">中文含义：图像。使用位置：CombatHUD.ConfigureWeaponIcon(Image)。</param>
    void ConfigureWeaponIcon(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.rectTransform.SetParent(healthGaugeRoot, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.anchoredPosition = new Vector2(0f, 9f);
        image.rectTransform.sizeDelta = new Vector2(82f, 34f);
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.transform.SetAsLastSibling();
    }

    /// <summary>ConfigureAmmoText 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） Color.Color(float, float, float, float)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：CombatHUD.ConfigureAmmoText(Text)。</param>
    void ConfigureAmmoText(Text text)
    {
        if (text == null)
        {
            return;
        }

        text.rectTransform.SetParent(healthGaugeRoot, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = new Vector2(0f, -29f);
        text.rectTransform.sizeDelta = new Vector2(116f, 33f);
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 1f, 1f, 0.94f);
        text.raycastTarget = false;
        text.transform.SetAsLastSibling();
    }

    /// <summary>ConfigureWeaponName 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） Color.Color(float, float, float, float)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <summary>ConfigureKnifeStatus 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、CombatHUD.CreateImage(string, RectTransform)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） Color.Color(float, float, float, float)、CombatHUD.CreateImage(string, RectTransform)、Transform.SetAsLastSibling()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConfigureKnifeStatus()
    {
        ConfigureKnifeRect(knifeStatusIcon.rectTransform, KnifeIconPosition, new Vector2(35f, 31f));
        knifeStatusIcon.preserveAspect = true;
        knifeStatusIcon.raycastTarget = false;

        knifeBreakGlow = CreateImage("断刀红光", healthGaugeRoot);
        ConfigureKnifeRect(knifeBreakGlow.rectTransform, KnifeIconPosition, new Vector2(49f, 49f));
        knifeBreakGlow.sprite = GetNativeHudSprite("KnifeBreakGlow");
        knifeBreakGlow.preserveAspect = true;
        knifeBreakGlow.enabled = false;
        knifeStatusIcon.transform.SetAsLastSibling();

        knifeBrokenMark = CreateImage("小刀报废红线", healthGaugeRoot);
        ConfigureKnifeRect(knifeBrokenMark.rectTransform, KnifeIconPosition, new Vector2(30f, 3f));
        knifeBrokenMark.color = new Color(1f, 0.19f, 0.16f, 0.96f);
        knifeBrokenMark.enabled = false;

        knifeTrack = CreateImage("耐久条底轨", healthGaugeRoot);
        ConfigureKnifeRect(knifeTrack.rectTransform, new Vector2(-76f + KnifeBarWidth * 0.5f, 35f), new Vector2(KnifeBarWidth, 3f));
        knifeTrack.color = new Color(0.8f, 0.82f, 0.78f, 0.29f);
        if (knifeStatusLine == null)
        {
            knifeStatusLine = CreateImage("小刀耐久", healthGaugeRoot);
        }
        ConfigureKnifeRect(knifeStatusLine.rectTransform, new Vector2(-76f, 35f), new Vector2(KnifeBarWidth, 3f));
        knifeStatusLine.rectTransform.pivot = new Vector2(0f, 0.5f);
        knifeStatusLine.type = Image.Type.Simple;
        knifeStatusLine.raycastTarget = false;
        knifeStatusLine.transform.SetAsLastSibling();

        knifeLoss = CreateImage("本次小刀耐久损失", healthGaugeRoot);
        ConfigureKnifeRect(knifeLoss.rectTransform, new Vector2(-76f, 35f), new Vector2(0f, 3f));
        knifeLoss.rectTransform.pivot = new Vector2(0f, 0.5f);
        knifeLoss.enabled = false;
    }

    private void ConfigureKnifeRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.SetParent(healthGaugeRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    /// <summary>BuildOrRepairParryPrompt 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、CombatHUD.CreateImage(string, RectTransform)、CombatHUD.CreateText(string, RectTransform, int)、CombatHUD.StretchToParent(RectTransform)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;Outline&gt;() 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()；下游（含内部回调体） Color.Color(float, float, float, float)、CombatHUD.CreateImage(string, RectTransform)、CombatHUD.CreateText(string, RectTransform, int)、CombatHUD.StretchToParent(RectTransform)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;Outline&gt;() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildOrRepairParryPrompt()
    {
        if (parryPromptGroup == null)
        {
            // existing（已有）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：CombatHUD.BuildOrRepairParryPrompt()。
Transform existing = transform.Find("弹反提示");
            if (existing != null)
            {
                parryPromptGroup = existing.GetComponent<CanvasGroup>();
            }
        }

        if (parryPromptGroup == null)
        {
            // root（根游戏对象）：使用位置：CombatHUD.BuildOrRepairParryPrompt()。
GameObject root = new GameObject("弹反提示", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            parryPromptGroup = root.GetComponent<CanvasGroup>();
        }

        var rootRect = parryPromptGroup.GetComponent<RectTransform>();
        ConfigureKnifeRect(rootRect, new Vector2(KnifeIconPosition.x, 22f), new Vector2(43f, 19f));
        rootRect.pivot = new Vector2(.5f, .5f);
        var existingKey = rootRect.Find("空格键");
        if (existingKey != null)
        {
            parryKeyImage = existingKey.GetComponent<Image>();
        }
        if (parryKeyImage == null)
        {
            parryKeyImage = CreateImage("空格键", rootRect);
        }
        StretchToParent(parryKeyImage.rectTransform);
        parryKeyImage.sprite = Resources.Load<Sprite>("NativeInteractionArt/Key_Space");
        parryKeyImage.preserveAspect = true;
        parryKeyImage.color = Color.white;
        parryKeyImage.raycastTarget = false;
        if (parryKeyImage.TryGetComponent<Outline>(out var outline))
        {
            outline.enabled = false;
        }
        if (parryPromptKeyLabel == null)
        {
            parryPromptKeyLabel = CreateText("按键文字", rootRect, 13);
        }
        parryPromptKeyLabel.rectTransform.SetParent(rootRect, false);
        StretchToParent(parryPromptKeyLabel.rectTransform);
        parryPromptKeyLabel.resizeTextForBestFit = true;
        parryPromptKeyLabel.resizeTextMinSize = 8;
        parryPromptKeyLabel.resizeTextMaxSize = 13;
        parryPromptKeyLabel.alignment = TextAnchor.MiddleCenter;
        if (parryPromptKnifeIcon != null)
        {
            parryPromptKnifeIcon.enabled = false;
        }
        if (parryPromptActionLabel == null)
        {
            parryPromptActionLabel = CreateText("重按提示", rootRect, 13);
        }

        RectTransform actionRect = parryPromptActionLabel.rectTransform;
        actionRect.SetParent(rootRect, false);
        actionRect.anchorMin = new Vector2(.5f, .5f);
        actionRect.anchorMax = actionRect.anchorMin;
        actionRect.pivot = actionRect.anchorMin;
        actionRect.anchoredPosition = Vector2.zero;
        actionRect.sizeDelta = new Vector2(84f, 19f);
        parryPromptActionLabel.alignment = TextAnchor.MiddleCenter;
        parryPromptActionLabel.text = "松开再按";
        parryPromptActionLabel.color = parryPromptColor;
        parryPromptActionLabel.raycastTarget = false;
        parryPromptActionLabel.enabled = false;
        shownParryPrompt = KnifeParryPromptKind.None;
        parryPromptGroup.alpha = 0f;
        parryPromptGroup.interactable = false;
        parryPromptGroup.blocksRaycasts = false;
    }

    /// <summary>StretchToParent 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.BuildOrRepairHealthGauge()、CombatHUD.BuildOrRepairParryPrompt()、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、CombatHUD.CreateRing(string, RectTransform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：CombatHUD.StretchToParent(RectTransform)。</param>
    void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>OnHealthChanged 的业务处理入口；本方法直接依赖 CombatHUD.HitFlash()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CombatHUD.HitFlash()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">中文含义：当前。使用位置：CombatHUD.OnHealthChanged(float, float)。</param>
    /// <param name="max">中文含义：最大。</param>
    void OnHealthChanged(float current, float max)
    {
        if (current < lastHealth)
        {
            HitFlash();
        }

        if (!Mathf.Approximately(current, lastHealth) && isActiveAndEnabled && !IsCombatVisualBlocked())
        {
            RevealGauge();
        }
        lastHealth = current;
    }

    /// <summary>UpdateHealthFill 的业务处理入口；本方法直接依赖 HealthRingGraphic.SetFillAmount(float)、Mathf.Abs(float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.TryInitializeHealthGauge()、CombatHUD.UpdateSmoothHealth()；下游（含内部回调体） HealthRingGraphic.SetFillAmount(float)、Mathf.Abs(float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">中文含义：当前。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    /// <param name="max">中文含义：最大。使用位置：作为 Mathf.Clamp01(float) 的参数、CombatHUD.UpdateHealthFill(float, float, bool)。</param>
    /// <param name="isFinalTarget">中文含义：是否最终目标。使用位置：CombatHUD.UpdateHealthFill(float, float, bool)。</param>
    void UpdateHealthFill(float current, float max, bool isFinalTarget = false)
    {
        // ratio（比例）：使用位置：作为 HealthRingGraphic.SetFillAmount(float) 的参数、作为 Mathf.Abs(float) 的参数、CombatHUD.UpdateHealthFill(float, float, bool)。
float ratio;
        if (max > 0f)
        {
            ratio = Mathf.Clamp01(current / max);
        }
        else
        {
            ratio = 0f;
        }

        // targetColor（目标颜色）：使用位置：CombatHUD.UpdateHealthFill(float, float, bool)。
Color targetColor;
        if (ratio < 0.25f)
        {
            targetColor = healthLowColor;
        }
        else
        {
            targetColor = healthNormalColor;
        }

        if (healthVisualCacheInitialized && cachedHealthMaximum == max && Mathf.Abs(cachedHealthRatio - ratio) < 0.001f && cachedHealthColor == targetColor && (!isFinalTarget || cachedHealthAtTarget))
        {
            return;
        }

        cachedHealthMaximum = max;
        cachedHealthRatio = ratio;
        cachedHealthColor = targetColor;
        cachedHealthAtTarget = isFinalTarget;
        healthVisualCacheInitialized = true;
        if (healthRingValue == null)
        {
            return;
        }

        float startingMaximum = max;
        if (leon.Health != null && leon.Health.InitialMaximum > 0f)
        {
            startingMaximum = leon.Health.InitialMaximum;
        }

        float baseHealthMaximum = Mathf.Max(1f, startingMaximum);
        float bonus = Mathf.Max(0f, max - baseHealthMaximum);
        float growth = bonus / (baseHealthMaximum * 0.5f + bonus);
        float availableArc = Mathf.Lerp(BaseHealthSweep, MaximumHealthSweep, growth) / MaximumHealthSweep;
        healthRingBackground.SetFillAmount(availableArc);
        healthRingValue.SetFillAmount(availableArc * ratio);
        healthRingValue.color = targetColor;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CombatHUD.IsCombatVisualBlocked()、CombatHUD.RefreshAmmoText()、CombatHUD.TryInitializeHealthGauge()、CombatHUD.UpdateAmmoDisplay()、CombatHUD.UpdateBloodVignette()、CombatHUD.UpdateKnifeDurability()、CombatHUD.UpdateParryPrompt() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!healthGaugeInitialized)
        {
            TryInitializeHealthGauge();
        }

        if (!healthGaugeInitialized)
        {
            return;
        }

        BindInventory();
        bool showingInventoryGauge = inventoryGaugeParent != null && inventoryGaugeParent.gameObject.activeInHierarchy
            && (!GameUIManager.HasInstance || GameUIManager.Instance.currentState == GameUIManager.State.Inventory);
        bool blocked = leon.currentState == LeonState.Dead || (!showingInventoryGauge && IsCombatVisualBlocked());
        if (blocked)
        {
            gaugeGroup.alpha = 0f;
            revealUntil = 0f;
            knifeLossUntil = 0f;
            knifeBreakUntil = 0f;
            wasBlocked = true;
            return;
        }

        if (wasBlocked)
        {
            displayHealth = leon.currentHealth;
            healthVisualCacheInitialized = false;
            wasBlocked = false;
        }

        RefreshAmmoText();
        UpdateSmoothHealth();
        UpdateKnifeDurability();
        if (showingInventoryGauge)
        {
            gaugeGroup.alpha = 1f;
        }
        else
        {
            UpdateBloodVignette();
            UpdateGaugeVisibility();
        }
    }

    internal void ShowInventoryGauge(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        TryInitializeHealthGauge();
        if (!healthGaugeInitialized || inventoryGaugeParent == parent)
        {
            return;
        }

        RestoreGameplayGauge();
        gameplayGaugeParent = healthGaugeRoot.parent;
        gameplayGaugeSibling = healthGaugeRoot.GetSiblingIndex();
        inventoryGaugeParent = parent;
        healthGaugeRoot.SetParent(parent, false);
        healthGaugeRoot.SetAsLastSibling();
        gaugeGroup.alpha = 1f;
    }

    internal void RestoreGameplayGauge()
    {
        if (gameplayGaugeParent == null || healthGaugeRoot == null)
        {
            return;
        }

        healthGaugeRoot.SetParent(gameplayGaugeParent, false);
        healthGaugeRoot.SetSiblingIndex(gameplayGaugeSibling);
        inventoryGaugeParent = null;
        gameplayGaugeParent = null;
        revealUntil = 0f;
        gaugeGroup.alpha = 0f;
    }

    private float ReadHealthMaximum()
    {
        if (leon.Health != null)
        {
            return leon.Health.MaxHealth;
        }

        return leon.maxHealth;
    }

    private void RevealGauge()
    {
        revealUntil = Time.unscaledTime + GaugeHoldDuration;
    }

    private void UpdateGaugeVisibility()
    {
        bool weaponActive = weaponHandler.IsSwitching || leon.currentState == LeonState.Shooting;
        if (combat != null)
        {
            weaponActive |= combat.IsParryPromptVisible || combat.IsAiming || combat.IsReloading || combat.IsKnifeAttackActive
                || (combat.knifeModule != null && combat.knifeModule.Active);
        }

        if (weaponActive || (weaponWheel != null && weaponWheel.IsOpen))
        {
            RevealGauge();
        }

        if (revealUntil <= 0f)
        {
            gaugeGroup.alpha = 0f;
            return;
        }

        if (Time.unscaledTime <= revealUntil)
        {
            gaugeGroup.alpha = Mathf.MoveTowards(gaugeGroup.alpha, 1f, Time.unscaledDeltaTime / GaugeFadeInDuration);
        }
        else
        {
            gaugeGroup.alpha = Mathf.MoveTowards(gaugeGroup.alpha, 0f, Time.unscaledDeltaTime / GaugeFadeDuration);
        }
    }

    private void LateUpdate()
    {
        UpdateParryPrompt();
    }

    /// <summary>IsCombatVisualBlocked 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool IsCombatVisualBlocked()
    {
        if (GameUIManager.HasInstance && (GameUIManager.Instance.currentState != GameUIManager.State.Gameplay || GameUIManager.Instance.IsCursorRequested))
        {
            return true;
        }

        if (InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
        {
            return true;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return true;
        }

        return false;
    }

    /// <summary>UpdateKnifeDurability 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Color.Lerp(Color, Color, float)、InventoryManager.GetFirstItem(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Update()；下游（含内部回调体） Color.Color(float, float, float, float)、Color.Lerp(Color, Color, float)、InventoryManager.GetFirstItem(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindInventory()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (observedInventory == inventory)
        {
            return;
        }

        UnbindInventory();
        observedInventory = inventory;
        if (observedInventory != null)
        {
            observedInventory.OnInventoryChanged += OnInventoryChanged;
        }
        ReadKnifeStatus(false);
    }

    private void UnbindInventory()
    {
        if (observedInventory != null)
        {
            observedInventory.OnInventoryChanged -= OnInventoryChanged;
        }
        observedInventory = null;
        observedKnife = null;
        knifeRatio = 0f;
        knifeLossUntil = 0f;
        knifeBreakUntil = 0f;
    }

    private void OnInventoryChanged()
    {
        ReadKnifeStatus(isActiveAndEnabled && !IsCombatVisualBlocked());
    }

    private void ReadKnifeStatus(bool showLoss)
    {
        GridItem usable = null;
        hasBrokenKnife = false;
        if (observedInventory != null)
        {
            usable = observedInventory.GetPreferredKnife();
            hasBrokenKnife = observedInventory.GetFirstItem("broken_knife") != null;
        }

        float newRatio = 0f;
        if (usable != null)
        {
            newRatio = usable.DurabilityNormalized;
        }

        if (usable != null && usable.currentDurability == 0)
        {
            hasBrokenKnife = true;
        }

        bool justBroke = observedKnife != null && observedKnife.item != null
            && (observedKnife.item.itemId == "broken_knife" ||
                (ReferenceEquals(usable, observedKnife) && observedKnife.currentDurability == 0 && knifeRatio > 0f));
        bool lostDurability = justBroke || (usable != null && ReferenceEquals(usable, observedKnife) && newRatio < knifeRatio);
        if (showLoss && lostDurability)
        {
            knifeLossStart = newRatio;
            if (justBroke)
            {
                knifeLossStart = 0f;
                knifeBreakUntil = Time.unscaledTime + KnifeBreakDuration;
            }
            float lossEnd = Mathf.Max(knifeRatio, knifeLossStart);
            if (knifeLossUntil > Time.unscaledTime && !justBroke)
            {
                lossEnd = Mathf.Max(lossEnd, knifeLossEnd);
            }
            knifeLossEnd = lossEnd;
            knifeLossUntil = Time.unscaledTime + KnifeLossDuration;
            RevealGauge();
        }
        else if (!ReferenceEquals(usable, observedKnife))
        {
            knifeLossUntil = 0f;
        }

        observedKnife = usable;
        knifeRatio = newRatio;
    }

    void UpdateKnifeDurability()
    {
        float now = Time.unscaledTime;
        bool showingLoss = now < knifeLossUntil;
        bool showingBreak = now < knifeBreakUntil;
        bool hasUsableKnife = observedKnife != null && observedKnife.currentDurability != 0;
        bool showBroken = hasBrokenKnife && !hasUsableKnife && !showingBreak;
        bool showKnife = hasUsableKnife || showBroken || showingBreak;
        knifeTrack.enabled = !showBroken && (hasUsableKnife || showingLoss);
        knifeStatusLine.enabled = hasUsableKnife && !showingBreak;
        knifeBrokenMark.enabled = showBroken;
        UpdateKnifeBreakPulse(showingBreak, now);
        knifeStatusLine.rectTransform.sizeDelta = new Vector2(KnifeBarWidth * knifeRatio, 3f);
        knifeStatusLine.color = Color.Lerp(new Color(0.87f, 0.28f, 0.22f, 1f), new Color(0.91f, 0.94f, 0.88f, 1f), knifeRatio);

        Sprite sprite = knifeStatusSprite;
        if (sprite == null)
        {
            sprite = knifeIcon;
        }
        knifeStatusIcon.color = Color.white;
        if ((showingBreak || showBroken) && brokenKnifeStatusSprite != null)
        {
            sprite = brokenKnifeStatusSprite;
        }
        if (showBroken)
        {
            knifeStatusIcon.color = new Color(0.60f, 0.57f, 0.54f, 0.76f);
        }
        knifeStatusIcon.sprite = sprite;
        knifeStatusIcon.enabled = showKnife && sprite != null;

        knifeLoss.enabled = showingLoss;
        if (showingLoss)
        {
            knifeLoss.rectTransform.anchoredPosition = new Vector2(-76f + KnifeBarWidth * knifeLossStart, 35f);
            knifeLoss.rectTransform.sizeDelta = new Vector2(KnifeBarWidth * (knifeLossEnd - knifeLossStart), 3f);
            float remaining = Mathf.Clamp01((knifeLossUntil - now) / KnifeLossDuration);
            knifeLoss.color = new Color(1f, 0.82f, 0.48f, remaining);
        }
    }

    private void UpdateKnifeBreakPulse(bool showingBreak, float now)
    {
        float scale = 1f;
        knifeBreakGlow.enabled = showingBreak && knifeBreakGlow.sprite != null;
        if (showingBreak)
        {
            float phase = 1f - Mathf.Clamp01((knifeBreakUntil - now) / KnifeBreakDuration);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 1f, phase));
            scale = Mathf.Lerp(1.58f, 1f, settle);
            float glowAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.96f, phase));
            knifeBreakGlow.color = new Color(1f, 0.06f, 0.04f, glowAlpha);
        }

        knifeStatusIcon.rectTransform.localScale = Vector3.one * scale;
        knifeBreakGlow.rectTransform.localScale = Vector3.one * scale;
    }

    /// <summary>UpdateParryPrompt 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonCombat&gt;()、Mathf.Sin(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Update()；下游（含内部回调体） Component.GetComponent&lt;LeonCombat&gt;()、Mathf.Sin(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateParryPrompt()
    {
        if (parryPromptGroup == null || parryKeyImage == null || parryPromptKeyLabel == null || parryPromptActionLabel == null)
        {
            return;
        }

        if (combat == null && leon != null)
        {
            combat = leon.GetComponent<LeonCombat>();
        }

        LeonInputBridge inputBridge = null;
        bool canReadInput = leon != null && combat != null && !IsCombatVisualBlocked()
            && leon.TryGetComponent(out inputBridge) && inputBridge.CanReadParryInput;
        KnifeParryPromptKind prompt = KnifeParryPromptKind.None;
        if (combat != null && combat.knifeModule != null)
        {
            bool held = canReadInput && inputBridge.IsParryInputHeld;
            prompt = combat.knifeModule.ReadParryPrompt(canReadInput, held);
        }
        else if (canReadInput && combat.IsParryPromptVisible)
        {
            prompt = KnifeParryPromptKind.Perfect;
        }

        if (shownParryPrompt == prompt)
        {
            return;
        }

        shownParryPrompt = prompt;
        bool showKey = prompt == KnifeParryPromptKind.Perfect;
        parryKeyImage.enabled = false;
        parryPromptKeyLabel.enabled = false;
        parryPromptActionLabel.enabled = prompt == KnifeParryPromptKind.PressAgain;
        if (showKey && inputBridge != null)
        {
            string binding = inputBridge.GetParryBindingDisplayName();
            bool nativeSpace = binding == "空格" && parryKeyImage.sprite != null;
            parryKeyImage.enabled = nativeSpace;
            parryPromptKeyLabel.enabled = !nativeSpace;
            parryPromptKeyLabel.text = binding;
            parryPromptKeyLabel.color = parryPromptColor;
        }

        if (prompt == KnifeParryPromptKind.None)
        {
            parryPromptGroup.alpha = 0f;
        }
        else
        {
            parryPromptGroup.alpha = 1f;
        }
    }

    /// <summary>UpdateSmoothHealth 的业务处理入口；本方法直接依赖 CombatHUD.UpdateHealthFill(float, float, bool)、Mathf.Abs(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Update()；下游（含内部回调体） CombatHUD.UpdateHealthFill(float, float, bool)、Mathf.Abs(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSmoothHealth()
    {
        if (healthRingValue == null)
        {
            return;
        }

        // targetHealth（目标生命）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CombatHUD.UpdateSmoothHealth()。
float targetHealth = leon.currentHealth;
        displayHealth = Mathf.Lerp(displayHealth, targetHealth, healthLerpSpeed * Time.unscaledDeltaTime);
        if (Mathf.Abs(displayHealth - targetHealth) < 0.1f)
        {
            displayHealth = targetHealth;
        }

        UpdateHealthFill(displayHealth, ReadHealthMaximum(), Mathf.Abs(displayHealth - targetHealth) < 0.0001f);
    }

    /// <summary>SetLaserVisible 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.DisableLegacyCrosshair()；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="v">中文含义：是否显示激光瞄准线。使用位置：CombatHUD.SetLaserVisible(bool)。</param>
    public void SetLaserVisible(bool v)
    {
        // receiverValue15（判空前缓存的激光点积）：从激光点积读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CombatHUD.SetLaserVisible(bool)。
global::UnityEngine.RectTransform receiverValue15 = laserDot;
        // img（界面图像组件）：使用位置：CombatHUD.SetLaserVisible(bool)。
global::UnityEngine.UI.Image img;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            img = receiverValue15.GetComponent<Image>();
        }
        else
        {
            img = default;
        }

        if (img)
        {
            // c（当前颜色）：使用位置：CombatHUD.SetLaserVisible(bool)。
Color c = img.color;
            if (v)
            {
                c.a = 0.85f;
            }
            else
            {
                c.a = 0f;
            }

            img.color = c;
        }
    }

    /// <summary>DisableLegacyCrosshair 的业务处理入口；本方法直接依赖 CombatHUD.SetLaserVisible(bool)、GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Start()、CrosshairUI.DisableCompetingCrosshairs()；下游（含内部回调体） CombatHUD.SetLaserVisible(bool)、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void DisableLegacyCrosshair()
    {
        SetLaserVisible(false);
        if (laserDot != null)
        {
            laserDot.gameObject.SetActive(false);
        }
    }

    public bool TryGetDisplayedFirearm(out WeaponType type)
    {
        type = default;
        if (weaponHandler == null)
        {
            return false;
        }

        WeaponType equipment = weaponHandler.CurrentEquipment;
        if (equipment != WeaponType.Knife && weaponHandler.HasWeaponInInventory(equipment)
            && weaponHandler.GetWeapon(equipment) != null)
        {
            lastFirearm = equipment;
            hasLastFirearm = true;
        }

        if (hasLastFirearm && weaponHandler.HasWeaponInInventory(lastFirearm))
        {
            type = lastFirearm;
            return true;
        }

        return false;
    }

    void RefreshAmmoText()
    {
        if (!TryGetDisplayedFirearm(out WeaponType type))
        {
            innerWeaponIcon.enabled = false;
            innerAmmoText.enabled = false;
            ammoCacheInitialized = false;
            return;
        }

        WeaponBase weapon = weaponHandler.GetWeapon(type);
        if (weapon == null)
        {
            innerWeaponIcon.enabled = false;
            innerAmmoText.enabled = false;
            ammoCacheInitialized = false;
            return;
        }

        int magazine = weapon.currentAmmo;
        int reserve = weaponHandler.GetReserveAmmo(type);
        if (!ammoCacheInitialized || cachedAmmoWeaponType != type || cachedAmmoWeapon != weapon)
        {
            cachedAmmoSprite = GetFirearmHudSprite(type, weapon);
            innerWeaponIcon.sprite = cachedAmmoSprite;
            innerWeaponIcon.enabled = cachedAmmoSprite != null;
            cachedAmmoWeapon = weapon;
            cachedAmmoWeaponType = type;
        }

        if (!ammoCacheInitialized || cachedCurrentAmmo != magazine || cachedReserveAmmo != reserve)
        {
            innerAmmoText.text = FormatAmmo(magazine, reserve);
            cachedCurrentAmmo = magazine;
            cachedReserveAmmo = reserve;
        }
        innerAmmoText.enabled = true;
        ammoCacheInitialized = true;
    }

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> hudSprites =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    internal static Sprite GetNativeHudSprite(string name)
    {
        if (!hudSprites.TryGetValue(name, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>("NativeCombatHUD/" + name);
            hudSprites.Add(name, sprite);
        }
        return sprite;
    }

    internal static Sprite GetFirearmHudSprite(WeaponType type, WeaponBase weapon)
    {
        Sprite sprite = null;
        switch (type)
        {
            case WeaponType.Pistol:
                sprite = GetNativeHudSprite("SG09R");
                break;
            case WeaponType.Rifle:
                sprite = GetNativeHudSprite("CQBR");
                break;
            case WeaponType.Shotgun:
                sprite = GetNativeHudSprite("W870");
                break;
        }

        if (sprite == null && weapon != null && weapon.data != null)
        {
            sprite = weapon.data.hudIcon;
        }
        return sprite;
    }

    internal static string FormatAmmo(int magazine, int reserve)
    {
        string magazineColor = "#A5D589";
        if (magazine == 0)
        {
            magazineColor = "#EA7E6A";
        }
        return $"<color={magazineColor}>{magazine}</color><color=#B9B9AE>/{reserve}</color>";
    }

    /// <summary>UpdateBloodVignette 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.Sin(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.Update()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.Sin(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateBloodVignette()
    {
        if (bloodVignette == null)
        {
            return;
        }

        if (hitFlashAlpha > 0f)
        {
            hitFlashAlpha = Mathf.MoveTowards(hitFlashAlpha, 0f, hitFlashFadeSpeed * Time.deltaTime);
        }

        // maxHealth（最大生命值）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数。
float maxHealth = Mathf.Max(0.001f, leon.maxHealth);
        // hp（生命值）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float hp = Mathf.Clamp01(leon.currentHealth / maxHealth);
        // targetAlpha（目标透明度）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float targetAlpha;
        // comfortableMaximumAlpha（舒适最大透明度）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float comfortableMaximumAlpha = Mathf.Min(maxVignetteAlpha, 0.2f);
        if (hp >= 0.35f)
        {
            targetAlpha = 0f;
        }
        else if (hp <= 0.12f)
        {
            targetAlpha = comfortableMaximumAlpha;
        }
        else
        {
            targetAlpha = Mathf.Lerp(comfortableMaximumAlpha, 0f, (hp - 0.12f) / 0.23f);
        }

        // currentAlpha（当前透明度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float currentAlpha = bloodVignette.alpha;
        // desiredAlpha（期望透明度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float desiredAlpha = Mathf.Max(targetAlpha, hitFlashAlpha);
        // nextAlpha（下一次透明度）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Max(float, float) 的参数、CombatHUD.UpdateBloodVignette()。
float nextAlpha = Mathf.Lerp(currentAlpha, desiredAlpha, Time.deltaTime * vignetteSmoothSpeed);
        if (Mathf.Abs(nextAlpha - desiredAlpha) < 0.001f)
        {
            nextAlpha = desiredAlpha;
        }

        if (hp < 0.2f)
        {
            // pulse（脉冲）：使用位置：作为 Mathf.Min(float, float) 的参数。
float pulse = Mathf.Abs(Mathf.Sin(Time.time * 1.4f)) * 0.04f;
            nextAlpha = Mathf.Max(nextAlpha, Mathf.Min(0.24f, targetAlpha + pulse));
        }

        if (currentAlpha != nextAlpha)
        {
            bloodVignette.alpha = nextAlpha;
        }
    }
}
