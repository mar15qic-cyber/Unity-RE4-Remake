using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// RE4 风格标题画面管理器
/// 控制：标题画面 → 过渡动画 → 菜单 → 场景加载
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    private AudioEventData titleMusic;
    private AudioEventData mainMenuMusic;
    private AudioEventData titleStartCue;
    /// <summary>界面视觉主题配置。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>标题演出。</summary>
    [Header("标题演出")]
    [UnityEngine.Serialization.FormerlySerializedAs("标题演出")]
    [UnityEngine.InspectorName("标题演出")]
    public TitleTypewriterPresentation titlePresentation;
    /// <summary>标题面板。</summary>
    [Header("界面引用")]
    public GameObject titlePanel;
    /// <summary>菜单面板。</summary>
    public GameObject menuPanel;
    /// <summary>标识图文字。</summary>
    public Text logoText;
    /// <summary>按下任意按钮文字。</summary>
    public Text pressAnyButtonText;
    /// <summary>菜单物品列表。</summary>
    public Text[] menuItems;
    /// <summary>选择指示器。</summary>
    public RectTransform selectionIndicator;
    /// <summary>菜单按钮集合。</summary>
    [Header("菜单项按钮（用于鼠标点击）")]
    public Button[] menuButtons;
    /// <summary>粒子集合容器。</summary>
    [Header("背景粒子")]
    public RectTransform particlesContainer;
    /// <summary>粒子预制件。</summary>
    public GameObject particlePrefab;
    /// <summary>粒子数量。</summary>
    public int particleCount = 12;
    /// <summary>菜单悬停缩放。</summary>
    [Header("选项菜单")]
    [Header("联动缩放")]
    public MenuHoverScale menuHoverScale;
    /// <summary>选项集合菜单。</summary>
    public OptionsTabManager optionsMenu;
    /// <summary>新版分页菜单存在时优先使用新版。兼容当前标题场景中的简易选项菜单；新版分页菜单存在时优先使用新版。</summary>
    [Tooltip("兼容当前标题场景中的简易选项菜单；新版分页菜单存在时优先使用新版")]
    public OptionsMenu simpleOptionsMenu;
    /// <summary>中文含义：标识图移动持续时间。</summary>
    [Header("过渡设置")]
    public float logoMoveDuration = 0.5f;
    /// <summary>中文含义：标识图缩小缩放。</summary>
    public float logoShrinkScale = 0.4f;
    /// <summary>中文含义：标识图目标位置。</summary>
    public Vector2 logoTargetPos = new Vector2(-350, 380);
    /// <summary>中文含义：标识图淡入淡出目标。</summary>
    public float logoFadeTarget = 0.55f;
    /// <summary>菜单滑移持续时间。</summary>
    [Header("菜单滑入")]
    public float menuSlideDuration = 0.35f;
    /// <summary>中文含义：菜单滑移延迟。</summary>
    public float menuSlideDelay = 0.12f;
    /// <summary>菜单滑移偏移量。</summary>
    public float menuSlideOffset = 180f;
    /// <summary>呼吸起伏速度。</summary>
    [Header("呼吸灯")]
    public float breatheSpeed = 0.8f;
    /// <summary>呼吸起伏最小透明度。</summary>
    public float breatheMinAlpha = 0.2f;
    /// <summary>呼吸起伏最大透明度。</summary>
    public float breatheMaxAlpha = 0.85f;
    [Header("村庄实景")]
    [Tooltip("标题和主菜单共用的正式村庄取景。")]
    public Texture2D titleVillageBackdrop;
    [Tooltip("章节加载界面使用的村庄广场取景。")]
    public Texture2D chapterVillageBackdrop;

    private Texture2D titleBackdropShade;
    private GameObject villageScenery;

    /// <summary>选定颜色。</summary>
    [Header("颜色")]
    public Color selectedColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>普通状态颜色。</summary>
    public Color normalColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>禁用状态颜色。</summary>
    public Color disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
    // 菜单项标签
    /// <summary>菜单标签集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] MenuLabels =
    {
        "继续游戏",
        "开始游戏",
        "读取存档",
        "设置",
        "退出游戏"
    };
    // 初始可用状态：CONTINUE 和 LOAD GAME 禁用（无存档系统）
    /// <summary>菜单启用默认。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly bool[] MenuEnabledDefault =
    {
        false,
        true,
        false,
        true,
        true
    };
    /// <summary>界面交互与显示使用的 TitleState 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum TitleState
    {
        /// <summary>中文含义：标题。</summary>
        Title,
        /// <summary>中文含义：过渡中。</summary>
        Transitioning,
        /// <summary>中文含义：菜单。</summary>
        Menu,
        /// <summary>中文含义：选项集合。</summary>
        Options,
        /// <summary>中文含义：加载。</summary>
        Loading
    }

    /// <summary>当前标题状态。</summary>
    private TitleState state = TitleState.Title;
    /// <summary>选定索引。</summary>
    private int selectedIndex = 1; // 默认选中 NEW GAME
    /// <summary>按下按钮分组。</summary>
    private CanvasGroup pressButtonGroup;
    /// <summary>标识图分组。</summary>
    private CanvasGroup logoGroup;
    /// <summary>标识图矩形。</summary>
    private RectTransform logoRect;
    /// <summary>菜单物品分组集合。</summary>
    private CanvasGroup[] menuItemGroups;
    /// <summary>菜单启用。</summary>
    private bool[] menuEnabled;
    /// <summary>是否加载。</summary>
    private bool isLoading;
    private void CreateVillageBackdrop()
    {
        if (titleVillageBackdrop != null)
        {
            var backdrop = ChapterLoadingView.CreateBackdrop(transform, "正式村庄主菜单背景", titleVillageBackdrop);
            backdrop.SetAsFirstSibling();
            var sceneryPrefab = Resources.Load<GameObject>("TitleVillage/PlazaScenery");
            if (sceneryPrefab != null)
            {
                var menuCamera = Camera.main;
                if (menuCamera != null && menuCamera.gameObject.scene == gameObject.scene)
                {
                    menuCamera.cullingMask = 0;
                }
                foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (light.gameObject.scene == gameObject.scene && light.type == LightType.Directional)
                    {
                        light.enabled = false;
                    }
                }
                villageScenery = Instantiate(sceneryPrefab);
                SceneManager.MoveGameObjectToScene(villageScenery, gameObject.scene);
                villageScenery.AddComponent<TitleVillagePan>().Begin(backdrop.GetComponentInChildren<RawImage>());
            }
            titleBackdropShade = ChapterLoadingView.AddBackdropShade(backdrop, .70f, .22f, .06f);
        }
    }

    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameManager.ResetScenePermission()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        // 时间缩放是跨场景的全局状态。结算界面会暂停玩法，标题场景必须主动接管并恢复正常时间。
        GameManager.ResetScenePermission();
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Button&gt;()、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、Component.GetComponentInChildren&lt;OptionsMenu&gt;(bool)、Component.GetComponentInChildren&lt;OptionsTabManager&gt;(bool)、GameObject.AddComponent&lt;Button&gt;() 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (visualTheme != null)
        {
            selectedColor = visualTheme.menuText;
            normalColor = visualTheme.menuMuted;
            disabledColor = new Color(0.4f, 0.42f, 0.39f, 0.45f);
            MenuThemeApplier.Apply(transform, visualTheme);
        }

        // 当前遍历索引。
for (int i = 0; menuItems != null && i < menuItems.Length && i < MenuLabels.Length; i++)
        {
            if (menuItems[i] != null)
            {
                menuItems[i].text = MenuLabels[i];
            }
        }

        if (menuHoverScale != null)
        {
            menuHoverScale.enabled = false;
        }

        menuHoverScale = null;
        // 初始化各组件引用
        if (pressAnyButtonText != null)
        {
            // 空值判断前缓存的取得组件的返回结果。
global::UnityEngine.CanvasGroup availableValue1 = pressAnyButtonText.GetComponent<CanvasGroup>();
            if (!global::System.Object.ReferenceEquals(availableValue1, null))
            {
                pressButtonGroup = availableValue1;
            }
            else
            {
                pressButtonGroup = pressAnyButtonText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (logoText != null)
        {
            logoRect = logoText.GetComponent<RectTransform>();
            // 空值判断前缓存的取得组件的返回结果。
global::UnityEngine.CanvasGroup availableValue3 = logoText.GetComponent<CanvasGroup>();
            if (!global::System.Object.ReferenceEquals(availableValue3, null))
            {
                logoGroup = availableValue3;
            }
            else
            {
                logoGroup = logoText.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 初始化菜单项 CanvasGroup
        if (menuItems != null)
        {
            menuItemGroups = new CanvasGroup[menuItems.Length];
            menuEnabled = new bool[menuItems.Length];
            // 当前遍历索引。
for (int i = 0; i < menuItems.Length; i++)
            {
                if (menuItems[i] == null)
                {
                    continue;
                }

                // 赋值前保存的目标菜单物品分组集合。
global::UnityEngine.CanvasGroup[] assignmentReceiver5 = menuItemGroups;
                // 赋值前保存的索引当前遍历索引。
int assignmentIndex6 = i;
                // 空值判断前缓存的取得组件的返回结果。
global::UnityEngine.CanvasGroup availableValue7 = menuItems[i].GetComponent<CanvasGroup>();
                if (!global::System.Object.ReferenceEquals(availableValue7, null))
                {
                    assignmentReceiver5[assignmentIndex6] = availableValue7;
                }
                else
                {
                    assignmentReceiver5[assignmentIndex6] = menuItems[i].gameObject.AddComponent<CanvasGroup>();
                }

                // 赋值前保存的目标菜单启用。
bool[] assignmentReceiver9 = menuEnabled;
                // 赋值前保存的索引当前遍历索引。
int assignmentIndex10 = i;
                if ((i < MenuEnabledDefault.Length))
                {
                    assignmentReceiver9[assignmentIndex10] = MenuEnabledDefault[i];
                }
                else
                {
                    assignmentReceiver9[assignmentIndex10] = true;
                }
            }
        }

        // 绑定菜单按钮点击
        if (menuButtons != null)
        {
            // 当前遍历索引。
for (int i = 0; i < menuButtons.Length && i < menuItems.Length; i++)
            {
                if (menuButtons[i] == null)
                {
                    continue;
                }

                // 当前元素索引。
int index = i;
                menuButtons[i].onClick.AddListener(() => OnMenuItemClicked(index));
            }
        }
        else if (menuItems != null)
        {
            // 如果没赋值按钮，自动添加 Button 组件
            menuButtons = new Button[menuItems.Length];
            // 当前遍历索引。
for (int i = 0; i < menuItems.Length; i++)
            {
                if (menuItems[i] == null)
                {
                    continue;
                }

                // 按钮。
Button btn = menuItems[i].GetComponent<Button>();
                if (btn == null)
                {
                    btn = menuItems[i].gameObject.AddComponent<Button>();
                }

                menuButtons[i] = btn;
                // 当前元素索引。
int index = i;
                btn.onClick.AddListener(() => OnMenuItemClicked(index));
            }
        }

        // 自动寻找两种选项菜单实现，避免场景引用缺失导致无法打开
        if (optionsMenu == null)
        {
            optionsMenu = GetComponentInChildren<OptionsTabManager>(true);
        }

        if (simpleOptionsMenu == null)
        {
            simpleOptionsMenu = GetComponentInChildren<OptionsMenu>(true);
        }

        // 绑定选项菜单回调
        if (optionsMenu != null)
        {
            optionsMenu.onClosed = OnOptionsClosed;
        }
        else if (simpleOptionsMenu != null)
        {
            simpleOptionsMenu.onClosed = OnOptionsClosed;
        }

        // 初始状态
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (titlePanel != null)
        {
            titlePanel.SetActive(true);
        }

        CreateVillageBackdrop();
        Cursor.lockState = CursorLockMode.None;
        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue12 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            receiverValue12.ShowCustomCursor();
        }

        // 启动背景粒子
        if (particlesContainer != null)
        {
            StartCoroutine(BackgroundParticles());
        }

        // 初始刷新菜单显示
        RefreshMenuDisplay();
        titleMusic = Resources.Load<AudioEventData>("TitleAudio/TitleMusic");
        mainMenuMusic = Resources.Load<AudioEventData>("TitleAudio/MainMenuMusic");
        titleStartCue = Resources.Load<AudioEventData>("TitleAudio/TitleStartCue");
        if (titleMusic != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayBGM(titleMusic);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)、OptionsMenu.Hide()、OptionsTabManager.Hide()、TitleScreenManager.HandleMenuKeyboard()、TitleScreenManager.HandleMenuMouse() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        switch (state)
        {
            case TitleState.Title:
                if (titlePresentation != null && !titlePresentation.Complete)
                {
                    if (pressButtonGroup != null)
                    {
                        pressButtonGroup.alpha = 0;
                    }

                    if (Input.anyKeyDown)
                    {
                        titlePresentation.Skip();
                    }

                    break;
                }

                UpdateBreatheEffect();
                if (Input.anyKeyDown)
                {
                    StartCoroutine(TransitionToMenu());
                }

                break;
            case TitleState.Menu:
                HandleMenuKeyboard();
                HandleMenuMouse();
                break;
            case TitleState.Options:
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (optionsMenu != null)
                    {
                        optionsMenu.Hide();
                    }
                    else
                    {
                        // 判空前缓存的新版分页菜单存在时优先使用新版。
global::OptionsMenu receiverValue13 = simpleOptionsMenu;
                        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
                        {
                            receiverValue13.Hide();
                        }
                    }
                }

                break;
        }
    }

#region ===== 背景粒子 =====
    /// <summary>BackgroundParticles 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;Image&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;RectTransform&gt;()、Mathf.Abs(float) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;Image&gt;()、GameObject.GameObject(string)、GameObject.GetComponent&lt;RectTransform&gt;()、Mathf.Abs(float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator BackgroundParticles()
    {
        if (particlesContainer == null)
        {
            yield break;
        }

        // 动态创建粒子
        // 当前遍历索引。
for (int i = 0; i < particleCount; i++)
        {
            if (i < particlesContainer.childCount)
            {
                continue;
            }

            // 点积。
GameObject dot;
            if (particlePrefab != null)
            {
                dot = Instantiate(particlePrefab, particlesContainer);
            }
            else
            {
                dot = new GameObject("Particle" + i);
                dot.transform.SetParent(particlesContainer, false);
                // 界面图像组件。
Image img = dot.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, Random.Range(0.05f, 0.25f));
                dot.AddComponent<CanvasGroup>();
                // 界面矩形变换。
RectTransform rt = dot.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(Random.Range(2f, 6f), Random.Range(2f, 6f));
            }
        }

        // 为每个子对象创建动画
        // 子对象集合。
RectTransform[] children = new RectTransform[particlesContainer.childCount];
        // 速度集合。
Vector2[] velocities = new Vector2[particlesContainer.childCount];
        // 透明度集合。
float[] alphas = new float[particlesContainer.childCount];
        // 当前遍历索引。
for (int i = 0; i < particlesContainer.childCount; i++)
        {
            children[i] = particlesContainer.GetChild(i) as RectTransform;
            if (children[i] == null)
            {
                continue;
            }

            // 随机初始位置
            children[i].anchoredPosition = new Vector2(Random.Range(-600f, 600f), Random.Range(-400f, 400f));
            velocities[i] = new Vector2(Random.Range(-8f, 8f), Random.Range(15f, 40f));
            alphas[i] = Random.Range(0.5f, 1.5f);
        }

        while (state != TitleState.Loading)
        {
            // 本次计算的时间步长。
float dt = Time.unscaledDeltaTime;
            // 当前遍历索引。
for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null)
                {
                    continue;
                }

                // 位置。
Vector2 pos = children[i].anchoredPosition;
                pos += velocities[i] * dt;
                // 超出屏幕则循环
                if (pos.y > 450f)
                {
                    pos.y = -450f;
                    pos.x = Random.Range(-600f, 600f);
                }

                if (pos.x > 650f)
                {
                    pos.x = -650f;
                }

                if (pos.x < -650f)
                {
                    pos.x = 650f;
                }

                children[i].anchoredPosition = pos;
                // 透明度脉动
                // 画布组。
CanvasGroup cg = children[i].GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    alphas[i] += dt * Random.Range(0.3f, 1f);
                    cg.alpha = Mathf.Abs(Mathf.Sin(alphas[i])) * 0.3f + 0.05f;
                }
            }

            yield return null;
        }
    }

#endregion
#region ===== 呼吸灯 =====
    /// <summary>UpdateBreatheEffect 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、Mathf.Sin(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Update()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、Mathf.Sin(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateBreatheEffect()
    {
        if (pressButtonGroup == null)
        {
            return;
        }

        // 使用修正正弦：慢速呼吸，停留时间长（模拟 RE4 原版节奏）
        // 正弦。
float sin = Mathf.Sin(Time.unscaledTime * breatheSpeed * Mathf.PI * 1.6f);
        // 标题呼吸亮度在零到一之间的变化比例。
float t = (sin + 1f) * 0.5f;
        // 让高亮停留更久
        t = Mathf.Pow(t, 0.7f);
        pressButtonGroup.alpha = Mathf.Lerp(breatheMinAlpha, breatheMaxAlpha, t);
    }

#endregion
#region ===== 标题 → 菜单过渡 =====
    /// <summary>TransitionToMenu 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Update()；下游（含内部回调体） GameObject.SetActive(bool)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator TransitionToMenu()
    {
        state = TitleState.Transitioning;
        var audio = AudioManager.Instance;
        if (audio != null && titleStartCue != null)
        {
            audio.Play(titleStartCue);
        }
        else
        {
            AudioManager.PlayInterface(AudioEventNames.UITitleStart);
        }
        // 已用。
float elapsed = 0f;
        // 持续时间。
float duration = 0.4f;
        // 第一步：LOGO和提示文字淡出
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = Mathf.Clamp01(elapsed / duration);
            // 使用SmoothStep曲线
            t = t * t * (3f - 2f * t);
            if (logoGroup != null)
            {
                logoGroup.alpha = 1f - t;
            }

            if (pressButtonGroup != null)
            {
                pressButtonGroup.alpha = 1f - t;
            }

            yield return null;
        }

        // 完全隐藏标题面板
        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        // 第二步：显示菜单面板
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        if (audio != null && mainMenuMusic != null)
        {
            audio.PlayBGM(mainMenuMusic);
        }

        // 第三步：菜单项逐个淡入
        if (menuItemGroups != null)
        {
            // 当前遍历索引。
for (int i = 0; i < menuItemGroups.Length; i++)
            {
                if (menuItemGroups[i] != null)
                {
                    menuItemGroups[i].alpha = 0f;
                }
            }

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                // 当前界面动画的归一化进度。
float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                // 当前遍历索引。
for (int i = 0; i < menuItemGroups.Length; i++)
                {
                    if (menuItemGroups[i] != null)
                    {
                        menuItemGroups[i].alpha = Mathf.Lerp(0f, 1f, t);
                    }
                }

                yield return null;
            }

            // 当前遍历索引。
for (int i = 0; i < menuItemGroups.Length; i++)
            {
                if (menuItemGroups[i] != null)
                {
                    menuItemGroups[i].alpha = 1f;
                }
            }
        }

        state = TitleState.Menu;
    }

    /// <summary>SlideMenuItemIn 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;RectTransform&gt;()、Mathf.Pow(float, float)、Vector2.Lerp(Vector2, Vector2, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、Mathf.Pow(float, float)、Vector2.Lerp(Vector2, Vector2, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    /// <param name="delay">延迟。</param>
    IEnumerator SlideMenuItemIn(int index, float delay)
    {
        // 界面矩形变换。
RectTransform rt = menuItems[index].GetComponent<RectTransform>();
        // 画布组。
CanvasGroup cg = menuItemGroups[index];
        if (rt == null || cg == null)
        {
            yield break;
        }

        cg.alpha = 0f;
        cg.interactable = menuEnabled[index];
        cg.blocksRaycasts = menuEnabled[index];
        yield return new WaitForSecondsRealtime(delay);
        // 目标位置。
Vector2 targetPos = rt.anchoredPosition;
        // 起始位置。
Vector2 startPos = targetPos + Vector2.left * menuSlideOffset;
        rt.anchoredPosition = startPos;
        cg.alpha = 1f;
        // 已用。
float elapsed = 0f;
        while (elapsed < menuSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / menuSlideDuration;
            // EaseOutBack（轻微过冲）
            // 候选值1。
float c1 = 1.70158f;
            // 候选值3。
float c3 = c1 + 1f;
            t = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
    }

#endregion
#region ===== 菜单导航 =====
    /// <summary>HandleMenuKeyboard 的业务处理入口；本方法直接依赖 Input.GetKeyDown(KeyCode)、TitleScreenManager.ExecuteMenuAction(int)、TitleScreenManager.NavigateMenu(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Update()；下游（含内部回调体） Input.GetKeyDown(KeyCode)、TitleScreenManager.ExecuteMenuAction(int)、TitleScreenManager.NavigateMenu(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleMenuKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            NavigateMenu(-1);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            NavigateMenu(1);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteMenuAction(selectedIndex);
        }
    }

    /// <summary>
    /// 鼠标检测：仅处理点击确认，悬停缩放由 MenuHoverScale 管理
    /// </summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Update()；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、MenuHoverScale.ClearHover()、MenuHoverScale.SetHoveredIndex(int)、object.ReferenceEquals(object, object)、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)、TitleScreenManager.PlayHoverSound()、TitleScreenManager.RefreshMenuDisplay()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleMenuMouse()
    {
        // 鼠标位置。
Vector2 mousePos = Input.mousePosition;
        // 当前遍历索引。
for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
            {
                continue;
            }

            // 界面矩形变换。
RectTransform rt = menuItems[i].GetComponent<RectTransform>();
            if (rt == null)
            {
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos))
            {
                // 键盘选中跟随鼠标位置
                if (selectedIndex != i)
                {
                    selectedIndex = i;
                    PlayHoverSound();
                    RefreshMenuDisplay();
                    // 判空前缓存的菜单悬停缩放。
global::MenuHoverScale receiverValue14 = menuHoverScale;
                    if (!global::System.Object.ReferenceEquals(receiverValue14, null))
                    {
                        receiverValue14.SetHoveredIndex(i);
                    }
                }

                // 点击统一由按钮处理，避免一次点击重复提交。
                return;
            }
        }

        // 鼠标未在任何菜单项上，清除悬停效果
        // 判空前缓存的菜单悬停缩放。
global::MenuHoverScale receiverValue15 = // 鼠标未在任何菜单项上，清除悬停效果
 menuHoverScale;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.ClearHover();
        }
    }

    /// <summary>NavigateMenu 的业务处理入口；本方法直接依赖 MenuHoverScale.SetHoveredIndex(int)、object.ReferenceEquals(object, object)、TitleScreenManager.PlayHoverSound()、TitleScreenManager.RefreshMenuDisplay()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuKeyboard()；下游（含内部回调体） MenuHoverScale.SetHoveredIndex(int)、object.ReferenceEquals(object, object)、TitleScreenManager.PlayHoverSound()、TitleScreenManager.RefreshMenuDisplay()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    void NavigateMenu(int direction)
    {
        // 旧索引。
int oldIndex = selectedIndex;
        // 尝试次数。
int attempts = 0;
        do
        {
            selectedIndex = (selectedIndex + direction + menuItems.Length) % menuItems.Length;
            attempts++;
        }
        while (!menuEnabled[selectedIndex] && attempts < menuItems.Length);
        if (selectedIndex != oldIndex)
        {
            PlayHoverSound();
            RefreshMenuDisplay();
            // 同步联动缩放效果到键盘选中项
            // 判空前缓存的菜单悬停缩放。
global::MenuHoverScale receiverValue16 = // 同步联动缩放效果到键盘选中项
 menuHoverScale;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.SetHoveredIndex(selectedIndex);
            }
        }
    }

    /// <summary>OnMenuItemClicked 的业务处理入口；本方法直接依赖 TitleScreenManager.ExecuteMenuAction(int)、TitleScreenManager.RefreshMenuDisplay()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Start()；下游（含内部回调体） TitleScreenManager.ExecuteMenuAction(int)、TitleScreenManager.RefreshMenuDisplay()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnMenuItemClicked(int index)
    {
        if (state != TitleState.Menu || isLoading)
        {
            return;
        }

        if (!menuEnabled[index])
        {
            AudioManager.PlayInterface(AudioEventNames.UIError);
            return;
        }

        selectedIndex = index;
        RefreshMenuDisplay();
        ExecuteMenuAction(index);
    }

    /// <summary>RefreshMenuDisplay 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuMouse()、TitleScreenManager.NavigateMenu(int)、TitleScreenManager.OnMenuItemClicked(int)、TitleScreenManager.OnOptionsClosed()、TitleScreenManager.Start()；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RefreshMenuDisplay()
    {
        // 当前遍历索引。
for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null)
            {
                continue;
            }

            // 是否选定。
bool isSelected = (i == selectedIndex);
            // 是否启用。
bool isEnabled = menuEnabled[i];
            if (isEnabled)
            {
                // 赋值前保存的目标菜单物品列表。
global::UnityEngine.UI.Text assignmentReceiver17 = menuItems[i];
                if (isSelected)
                {
                    assignmentReceiver17.color = selectedColor;
                }
                else
                {
                    assignmentReceiver17.color = normalColor;
                }

                // 赋值前保存的目标菜单物品列表。
global::UnityEngine.UI.Text assignmentReceiver19 = menuItems[i];
                if (isSelected)
                {
                    assignmentReceiver19.fontStyle = FontStyle.Bold;
                }
                else
                {
                    assignmentReceiver19.fontStyle = FontStyle.Normal;
                }
            }
            else
            {
                menuItems[i].color = disabledColor;
                menuItems[i].fontStyle = FontStyle.Normal;
            }
        // 缩放由 MenuHoverScale 统一管理，此处不再操作 localScale
        }

        // 移动左侧指示条
        if (selectionIndicator != null && selectedIndex < menuItems.Length && menuItems[selectedIndex] != null)
        {
            // 目标矩形变换。
RectTransform targetRt = menuItems[selectedIndex].GetComponent<RectTransform>();
            if (targetRt != null)
            {
                selectionIndicator.gameObject.SetActive(true);
                // 位置。
Vector2 pos = selectionIndicator.anchoredPosition;
                pos.y = targetRt.anchoredPosition.y;
                selectionIndicator.anchoredPosition = pos;
            }
        }
    }

    /// <summary>ExecuteMenuAction 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、TitleScreenManager.LoadGameScene()、TitleScreenManager.OpenOptions()、TitleScreenManager.PlayConfirmSound()、TitleScreenManager.QuitGame()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuKeyboard()、TitleScreenManager.OnMenuItemClicked(int)；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、TitleScreenManager.LoadGameScene()、TitleScreenManager.OpenOptions()、TitleScreenManager.PlayConfirmSound()、TitleScreenManager.QuitGame()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void ExecuteMenuAction(int index)
    {
        if (state != TitleState.Menu || isLoading)
        {
            return;
        }

        if (!menuEnabled[index])
        {
            return;
        }

        switch (index)
        {
            case 0: // CONTINUE —— 暂不可用
                break;
            case 1: // NEW GAME
                PlayConfirmSound();
                StartCoroutine(LoadGameScene());
                break;
            case 2: // LOAD GAME —— 暂不可用
                break;
            case 3: // OPTIONS
                OpenOptions();
                break;
            case 4: // EXIT
                ShowQuitConfirmation();
                break;
        }
    }

#endregion
#region ===== 选项菜单 =====
    /// <summary>OpenOptions 的业务处理入口；本方法直接依赖 bool.ToString()、Debug.Log(object)、Debug.LogError(object)、Debug.LogWarning(object)、GameObject.SetActive(bool)、OptionsMenu.Show()、OptionsTabManager.Show()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.ExecuteMenuAction(int)；下游（含内部回调体） bool.ToString()、Debug.Log(object)、Debug.LogError(object)、Debug.LogWarning(object)、GameObject.SetActive(bool)、OptionsMenu.Show()、OptionsTabManager.Show()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OpenOptions()
    {
        state = TitleState.Options;
        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        Debug.Log("[菜单系统] OpenOptions 开始执行");
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            Debug.Log("[菜单系统] menuPanel 已隐藏");
        }
        else
        {
            Debug.LogWarning("[菜单系统] menuPanel 为 null！");
        }

        // 计算前缓存的左侧文字“[菜单系统] 分页选项菜单引用状态: ”。
string leftOperand22 = "[菜单系统] 分页选项菜单引用状态: ";
        // 分支选出的文字“未配置”。
string selectedValue21;
        if (optionsMenu != null)
        {
            selectedValue21 = "有效 → " + optionsMenu.name;
        }
        else
        {
            selectedValue21 = "未配置";
        }

        Debug.Log(leftOperand22 + (selectedValue21));
        // 计算前缓存的左侧文字“[菜单系统] 简易选项菜单引用状态: ”。
string leftOperand24 = "[菜单系统] 简易选项菜单引用状态: ";
        // 分支选出的文字“未配置”。
string selectedValue23;
        if (simpleOptionsMenu != null)
        {
            selectedValue23 = "有效 → " + simpleOptionsMenu.name;
        }
        else
        {
            selectedValue23 = "未配置";
        }

        Debug.Log(leftOperand24 + (selectedValue23));
        if (optionsMenu != null)
        {
            // 计算前缓存的左侧文字“[菜单系统] optionsMenu.panelRoot: ”。
string leftOperand26 = "[菜单系统] optionsMenu.panelRoot: ";
            // 分支选出的选项集合菜单的面板根节点的名称。
string selectedValue25;
            if (optionsMenu.panelRoot != null)
            {
                selectedValue25 = optionsMenu.panelRoot.name;
            }
            else
            {
                selectedValue25 = "null";
            }

            Debug.Log(leftOperand26 + (selectedValue25));
            optionsMenu.Show();
            // 计算前缓存的左侧文本选择。
string leftOperand28 = "[菜单系统] Show() 调用完成，panelRoot.activeSelf=";
            // 分支选出的转换后的文字。
string selectedValue27;
            if (optionsMenu.panelRoot != null)
            {
                selectedValue27 = optionsMenu.panelRoot.activeSelf.ToString();
            }
            else
            {
                selectedValue27 = "null";
            }

            Debug.Log(leftOperand28 + (selectedValue27));
        }
        else if (simpleOptionsMenu != null)
        {
            // 计算前缓存的左侧文字“[菜单系统] simpleOptionsMenu.panel: ”。
string leftOperand30 = "[菜单系统] simpleOptionsMenu.panel: ";
            // 分支选出的简单选项集合菜单的面板的名称。
string selectedValue29;
            if (simpleOptionsMenu.panel != null)
            {
                selectedValue29 = simpleOptionsMenu.panel.name;
            }
            else
            {
                selectedValue29 = "null";
            }

            Debug.Log(leftOperand30 + (selectedValue29));
            simpleOptionsMenu.Show();
            // 计算前缓存的左侧文本选择。
string leftOperand32 = "[菜单系统] Show() 调用完成，panel.activeSelf=";
            // 分支选出的转换后的文字。
string selectedValue31;
            if (simpleOptionsMenu.panel != null)
            {
                selectedValue31 = simpleOptionsMenu.panel.activeSelf.ToString();
            }
            else
            {
                selectedValue31 = "null";
            }

            Debug.Log(leftOperand32 + (selectedValue31));
        }
        else
        {
            state = TitleState.Menu;
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }

            Debug.LogError("[菜单系统] 未找到任何可用的选项菜单组件，请检查 TitleScreenManager 的选项菜单引用");
        }
    }

    /// <summary>OnOptionsClosed 的业务处理入口；本方法直接依赖 Debug.Log(object)、GameObject.SetActive(bool)、TitleScreenManager.RefreshMenuDisplay()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、GameObject.SetActive(bool)、TitleScreenManager.RefreshMenuDisplay()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnOptionsClosed()
    {
        Debug.Log("[菜单系统] OnOptionsClosed 被调用，准备返回主菜单");
        state = TitleState.Menu;
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        RefreshMenuDisplay();
        Debug.Log("[菜单系统] 已返回主菜单");
    }

#endregion
#region ===== 场景加载 =====
    private void OnDestroy()
    {
        if (villageScenery != null)
        {
            Destroy(villageScenery);
        }
        var audio = AudioManager.Instance;
        if (audio != null)
        {
            audio.StopBGM(titleMusic);
            audio.StopBGM(mainMenuMusic);
        }
        if (titleBackdropShade != null)
        {
            Destroy(titleBackdropShade);
        }
    }

    IEnumerator LoadGameScene()
    {
        if (isLoading)
        {
            yield break;
        }

        string scenePath = SceneManagerEx.MainScenePath;
        if (!Application.CanStreamedLevelBeLoaded(scenePath))
        {
            Debug.LogError("正式村庄未加入构建场景：" + scenePath);
            yield break;
        }

        GameManager.ResetScenePermission();
        isLoading = true;
        state = TitleState.Loading;
        SetMenuButtonsInteractable(false);

        Font font = null;
        if (visualTheme != null)
        {
            font = visualTheme.interfaceFont;
        }

        if (font == null && pressAnyButtonText != null)
        {
            font = pressAnyButtonText.font;
        }

        var view = ChapterLoadingView.Create(transform, chapterVillageBackdrop, font);
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        // 先让章节画面绘制一帧，再启动异步加载。
        yield return null;
        AsyncOperation operation = SceneManager.LoadSceneAsync(scenePath);
        if (operation == null)
        {
            Destroy(view.gameObject);
            isLoading = false;
            state = TitleState.Menu;
            if (menuPanel != null)
            {
                menuPanel.SetActive(true);
            }

            SetMenuButtonsInteractable(true);
            Debug.LogError("无法开始加载正式村庄：" + scenePath);
            yield break;
        }

        operation.allowSceneActivation = false;
        if (villageScenery != null)
        {
            villageScenery.SetActive(false);
        }
        while (operation.progress < 0.9f)
        {
            view.SetProgress(operation.progress / 0.9f);
            yield return null;
        }

        view.ShowContinue();
        while (!view.Confirmed)
        {
            yield return null;
        }

        PlayConfirmSound();
        if (UICursorManager.Instance != null)
        {
            UICursorManager.Instance.HideCustomCursor();
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        yield return view.FadeToBlack();
        operation.allowSceneActivation = true;
        yield return operation;
    }

    /// <summary>SetMenuButtonsInteractable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.LoadGameScene()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactable">是否可交互。</param>
    void SetMenuButtonsInteractable(bool interactable)
    {
        if (menuButtons == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < menuButtons.Length; i++)
        {
            if (menuButtons[i] != null)
            {
                menuButtons[i].interactable = interactable && menuEnabled != null && i < menuEnabled.Length && menuEnabled[i];
            }
        }
    }

#endregion
#region ===== 退出 =====
    /// <summary>QuitGame 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.ExecuteMenuAction(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ShowQuitConfirmation()
    {
        state = TitleState.Transitioning;
        var dialog = new GameObject("退出游戏确认", typeof(RectTransform), typeof(OriginalQuitConfirmation));
        dialog.transform.SetParent(menuPanel.transform.parent, false);
        dialog.transform.SetAsLastSibling();
        dialog.GetComponent<OriginalQuitConfirmation>().Initialize(visualTheme,
            () => StartCoroutine(QuitGame()), () => state = TitleState.Menu);
    }

    IEnumerator QuitGame()
    {
        isLoading = true;
        yield return AudioManager.PlayInterfaceBeforeTransition(AudioEventNames.UIQuitConfirm);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

#endregion
#region ===== 音效钩子 =====
    /// <summary>PlayHoverSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.HandleMenuMouse()、TitleScreenManager.NavigateMenu(int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayHoverSound()
    {
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue38 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue38, null))
        {
            receiverValue38.PlaySfx(AudioEventNames.UITitleHover);
        }
    }

    /// <summary>PlayConfirmSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.ExecuteMenuAction(int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayConfirmSound()
    {
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue39 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue39, null))
        {
            receiverValue39.PlaySfx(AudioEventNames.UITitleConfirm);
        }
    }
#endregion
}
