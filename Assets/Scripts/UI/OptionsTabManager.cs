using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 标题选项页面：音量、画面、操控三个顶部页签。
/// 界面在缺少场景引用时自动建立，所有视觉素材仍可在检视面板中替换。
/// </summary>
/// <remarks>设置分页宿主，改键通过 InputBindingPersistence 保存，镜头设置经 RE4CameraController 提交，其他设置沿当前适配器应用。</remarks>
public class OptionsTabManager : MonoBehaviour
{
    /// <summary>界面视觉主题配置。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>面板根节点。</summary>
    [Header("基础引用")]
    public GameObject panelRoot;
    /// <summary>画布组。</summary>
    public CanvasGroup canvasGroup;
    /// <summary>输入动作资源。</summary>
    public InputActionAsset inputActionAsset;
    /// <summary>面板精灵图。</summary>
    [Header("可替换视觉素材")]
    public Sprite panelSprite;
    /// <summary>行精灵图。</summary>
    public Sprite rowSprite;
    /// <summary>接口字体。</summary>
    public Font interfaceFont;
    /// <summary>叠加层颜色。</summary>
    public Color overlayColor = new Color(0f, 0f, 0f, 0.78f);
    /// <summary>面板颜色。</summary>
    public Color panelColor = new Color(0.035f, 0.035f, 0.035f, 0.9f);
    /// <summary>选定颜色。</summary>
    public Color selectedColor = new Color(0.72f, 0.72f, 0.68f, 0.42f);
    /// <summary>强调色颜色。</summary>
    public Color accentColor = new Color(0.72f, 0.08f, 0.06f, 1f);
    /// <summary>按钮悬停样式图片。</summary>
    [Header("按钮素材")]
    [Tooltip("按钮悬停样式图片")]
    public Sprite buttonSprite;
    /// <summary>勾选框启用状态的图片。勾选框启用图片。</summary>
    [Tooltip("勾选框启用图片")]
    public Sprite tickBoxOnSprite;
    /// <summary>勾选框关闭状态的图片。勾选框关闭图片。</summary>
    [Tooltip("勾选框关闭图片")]
    public Sprite tickBoxOffSprite;
    /// <summary>滑块轨道图片。</summary>
    [Header("滑块素材")]
    [Tooltip("滑块轨道图片")]
    public Sprite sliderTrackSprite;
    /// <summary>滑块拖动方块图片。</summary>
    [Tooltip("滑块拖动方块图片")]
    public Sprite sliderKnobSprite;
    /// <summary>声音开启状态的图标。音量大于零时显示的图标。</summary>
    [Header("音量图标")]
    [Tooltip("音量大于零时显示的图标")]
    public Sprite soundOnIcon;
    /// <summary>声音关闭状态的图标。音量为零时显示的图标。</summary>
    [Tooltip("音量为零时显示的图标")]
    public Sprite soundOffIcon;
    /// <summary>在检视面板中增加或修改按键绑定项。</summary>
    [Header("可扩展按键绑定配置")]
    [Tooltip("在检视面板中增加或修改按键绑定项")]
    public List<BindingConfig> bindingConfigs = new List<BindingConfig>();
    /// <summary>背景音乐滑块。</summary>
    [Header("旧版场景兼容引用")]
    [HideInInspector]
    public Slider bgmSlider;
    /// <summary>音效滑块。</summary>
    [HideInInspector]
    public Slider sfxSlider;
    /// <summary>灵敏度滑块横向。</summary>
    [HideInInspector]
    public Slider sensitivitySliderX;
    /// <summary>灵敏度滑块纵向。</summary>
    [HideInInspector]
    public Slider sensitivitySliderY;
    /// <summary>瞄准灵敏度滑块。</summary>
    [HideInInspector]
    public Slider aimSensitivitySlider;
    /// <summary>面板音频。</summary>
    [HideInInspector]
    public GameObject panelAudio;
    /// <summary>面板显示。</summary>
    [HideInInspector]
    public GameObject panelDisplay;
    /// <summary>面板玩法。</summary>
    [HideInInspector]
    public GameObject panelGameplay;
    /// <summary>已关闭时的通知回调。</summary>
    [HideInInspector]
    public Action onClosed;
    /// <summary>分页按钮集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<Button> tabButtons = new List<Button>();
    /// <summary>分页文字集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<Text> tabTexts = new List<Text>();
    /// <summary>页面集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<GameObject> pages = new List<GameObject>();
    /// <summary>绑定行数。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<BindingRow> bindingRows = new List<BindingRow>();
    /// <summary>可用分辨率集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<Resolution> availableResolutions = new List<Resolution>();
    /// <summary>主控音量滑块。</summary>
    private Slider masterVolumeSlider;
    /// <summary>主控音量值。</summary>
    private Text masterVolumeValue;
    /// <summary>音量图标。</summary>
    private Image volumeIcon;
    /// <summary>灵敏度值横向。</summary>
    private Text sensitivityValueX;
    /// <summary>灵敏度值纵向。</summary>
    private Text sensitivityValueY;
    /// <summary>瞄准灵敏度值。</summary>
    private Text aimSensitivityValue;
    /// <summary>分辨率下拉框。</summary>
    private Dropdown resolutionDropdown;
    /// <summary>质量下拉框。</summary>
    private Dropdown qualityDropdown;
    /// <summary>帧速率下拉框。</summary>
    private Dropdown frameRateDropdown;
    /// <summary>抗锯齿下拉框。</summary>
    private Dropdown antiAliasingDropdown;
    /// <summary>图形接口下拉框。</summary>
    private Dropdown graphicsApiDropdown;
    /// <summary>向量同步切换。</summary>
    private Toggle vSyncToggle;
    /// <summary>全屏切换。</summary>
    [HideInInspector]
    public Toggle fullscreenToggle;
    /// <summary>反转纵向切换。</summary>
    [HideInInspector]
    public Toggle invertYToggle;
    /// <summary>瞄准模式保持按钮。</summary>
    private Button aimModeHoldButton;
    /// <summary>瞄准模式切换按钮。</summary>
    private Button aimModeToggleButton;
    /// <summary>描述标题。</summary>
    private Text descriptionTitle;
    /// <summary>描述身体。</summary>
    private Text descriptionBody;
    /// <summary>状态文字。</summary>
    private Text statusText;
    /// <summary>恢复按钮。</summary>
    private Button restoreButton;
    /// <summary>重新绑定操作。</summary>
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    /// <summary>当前分页。</summary>
    private int currentTab;
    /// <summary>是否已初始化。</summary>
    private bool initialized;
    /// <summary>是否隐藏中。</summary>
    private bool hiding;
    /// <summary>显示是否已经申请。</summary>
    private bool showRequested;
    private bool restoringDefaults;
    private GameObject focusedSettingRow;
    /// <summary>界面淡入淡出的持续时间。</summary>
    private float fadeDuration = 0.2f;
    /// <summary>摄像机控制器。</summary>
    private RE4CameraController cameraController;
    /// <summary>音量服务。</summary>
    private IAudioVolumeService volumeService;
    /// <summary>界面交互与显示模块 BindingRow；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class BindingRow
    {
        /// <summary>动作。</summary>
        public InputAction action;
        /// <summary>绑定索引。</summary>
        public int bindingIndex;
        /// <summary>显示名称。</summary>
        public string displayName;
        /// <summary>按钮。</summary>
        public Button button;
        /// <summary>值文字。</summary>
        public Text valueText;
    }

    /// <summary>界面交互与显示模块 BindingConfig；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    public class BindingConfig
    {
        /// <summary>显示名称。</summary>
        public string displayName = "新按键";
        /// <summary>动作名称。</summary>
        public string actionName = "";
        /// <summary>组合部件。</summary>
        public string compositePart = "";
        /// <summary>描述。</summary>
        public string description = "点击当前按键后，再按下新的键即可完成绑定；按退出键可取消。";
        /// <summary>中文含义：是否允许重新绑定。</summary>
        public bool allowRebind = true;
        /// <summary>中文含义：是否允许键盘。</summary>
        public bool allowKeyboard = true;
        /// <summary>中文含义：是否允许鼠标。</summary>
        public bool allowMouse = true;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、OptionsTabManager.Initialize()、OptionsTabManager.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        TryBindAudioService();
        Initialize();
        if (!showRequested)
        {
            panelRoot.SetActive(false);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、OptionsTabManager.Hide()、OptionsTabManager.SelectTab(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!initialized || panelRoot == null || !panelRoot.activeSelf || hiding)
        {
            return;
        }

        // 重新绑定期间禁止所有页签快捷键和关闭操作
        if (rebindOperation != null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            Hide();
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            SelectTab((currentTab + pages.Count - 1) % pages.Count);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            SelectTab((currentTab + 1) % pages.Count);
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsTabManager.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        TryBindAudioService();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsTabManager.CancelRebinding()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        if (volumeService != null)
        {
            volumeService.MasterVolumeChanged -= OnExternalVolumeChanged;
        }

        volumeService = null;
        CancelRebinding();
    }

    /// <summary>TryBindAudioService 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.OnEnable()、OptionsTabManager.OnMasterVolumeChanged(float)、OptionsTabManager.Start()；下游（含内部回调体） object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryBindAudioService()
    {
        // 下一次服务。
IAudioVolumeService nextService;
        if (AudioManager.HasInstance)
        {
            nextService = AudioManager.Instance as IAudioVolumeService;
        }
        else
        {
            nextService = null;
        }

        if (ReferenceEquals(nextService, volumeService))
        {
            return;
        }

        if (volumeService != null)
        {
            volumeService.MasterVolumeChanged -= OnExternalVolumeChanged;
        }

        volumeService = nextService;
        if (volumeService != null)
        {
            volumeService.MasterVolumeChanged += OnExternalVolumeChanged;
        }
    }

    /// <summary>
    /// 外部音量变化时同步滑块显示
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsTabManager.UpdateVolumeValue(float)、Slider.SetValueWithoutNotify(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    void OnExternalVolumeChanged(float value)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(value);
        }

        UpdateVolumeValue(value);
    }

    /// <summary>Show 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Text&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponentsInChildren&lt;Toggle&gt;(bool)、GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、OptionsTabManager.FadeIn()、OptionsTabManager.Initialize() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Show()、TitleScreenManager.OpenOptions()；下游（含内部回调体） Component.GetComponent&lt;Text&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponentsInChildren&lt;Toggle&gt;(bool)、GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、OptionsTabManager.FadeIn()、OptionsTabManager.Initialize() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        bool wasShowing = showRequested;
        showRequested = true;
        Initialize();
        if (panelRoot == null)
        {
            return;
        }

        hiding = false;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        // 面板矩形。
RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.localScale = Vector3.one;
        }

        LoadSettings();
        // toggle（切换）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：OptionsTabManager.Show()。
foreach (var toggle in panelRoot.GetComponentsInChildren<Toggle>(true))
        {
            // 标签。
var label = toggle.transform.Find("开关状态");
            if (label != null)
            {
                // 赋值前保存的目标取得组件的返回结果。
global::UnityEngine.UI.Text assignmentReceiver2 = label.GetComponent<Text>();
                if (toggle.isOn)
                {
                    assignmentReceiver2.text = "开启";
                }
                else
                {
                    assignmentReceiver2.text = "关闭";
                }
            }
        }

        focusedSettingRow = null;
        SelectTab(currentTab);
        StartCoroutine(FadeIn());
        if (!wasShowing)
        {
            AudioManager.PlayInterface(AudioEventNames.UISettingsOpen);
        }
    }

    /// <summary>Hide 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、IAudioVolumeService.SaveVolumeSettings()、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)、OptionsTabManager.CancelRebinding()、OptionsTabManager.FadeOut()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Hide()、OptionsTabManager.Update()、TitleScreenManager.Update()；下游（含内部回调体） GameSettingsData.SaveValue()、IAudioVolumeService.SaveVolumeSettings()、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)、OptionsTabManager.CancelRebinding()、OptionsTabManager.FadeOut()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        if (!initialized || hiding || panelRoot == null || !panelRoot.activeSelf)
        {
            return;
        }

        hiding = true;
        showRequested = false;
        CancelRebinding();
        AudioManager.PlayInterface(AudioEventNames.UISettingsCancel);
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue4 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.SaveVolumeSettings();
        }

        GameSettingsData.SaveValue();
        StartCoroutine(FadeOut());
    }

    /// <summary>Initialize 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;InputActionAsset&gt;(string)、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、InputBindingPersistence.Load(InputActionAsset)、List&lt;OptionsTabManager.BindingConfig&gt;.List()、MenuThemeApplier.Apply(Transform, UIVisualTheme) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Show()、OptionsTabManager.Start()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;InputActionAsset&gt;(string)、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、InputBindingPersistence.Load(InputActionAsset)、List&lt;OptionsTabManager.BindingConfig&gt;.List()、MenuThemeApplier.Apply(Transform, UIVisualTheme) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        if (interfaceFont == null)
        {
            interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (inputActionAsset == null)
        {
            ResolveInputActionAssetFromPlayer();
#if UNITY_EDITOR
            if (inputActionAsset == null)
            {
                inputActionAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/Input/LeonInputActions.inputactions");
            }
#endif
        }

        // 如果检视面板未配置绑定列表，使用默认列表。
        if (bindingConfigs == null || bindingConfigs.Count == 0)
        {
            bindingConfigs = new List<BindingConfig>
            {
                new BindingConfig
                {
                    displayName = "向前移动",
                    actionName = "Move",
                    compositePart = "Up"
                },
                new BindingConfig
                {
                    displayName = "向后移动",
                    actionName = "Move",
                    compositePart = "Down"
                },
                new BindingConfig
                {
                    displayName = "向左移动",
                    actionName = "Move",
                    compositePart = "Left"
                },
                new BindingConfig
                {
                    displayName = "向右移动",
                    actionName = "Move",
                    compositePart = "Right"
                },
                new BindingConfig
                {
                    displayName = "瞄准",
                    actionName = "Aim"
                },
                new BindingConfig
                {
                    displayName = "射击",
                    actionName = "Shoot"
                },
                new BindingConfig
                {
                    displayName = "装填",
                    actionName = "Reload"
                },
                new BindingConfig
                {
                    displayName = "招架",
                    actionName = "Parry"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 1",
                    actionName = "QuickSelect1"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 2",
                    actionName = "QuickSelect2"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 3",
                    actionName = "QuickSelect3"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 4",
                    actionName = "QuickSelect4"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 5",
                    actionName = "QuickSelect5"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 6",
                    actionName = "QuickSelect6"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 7",
                    actionName = "QuickSelect7"
                },
                new BindingConfig
                {
                    displayName = "武器快捷槽 8",
                    actionName = "QuickSelect8"
                },
                new BindingConfig
                {
                    displayName = "互动／拾取",
                    actionName = "Interact"
                },
                new BindingConfig
                {
                    displayName = "奔跑",
                    actionName = "Sprint"
                },
                new BindingConfig
                {
                    displayName = "蹲伏",
                    actionName = "Crouch"
                },
                new BindingConfig
                {
                    displayName = "自由视角",
                    actionName = "FreeLook"
                },
                new BindingConfig
                {
                    displayName = "打开背包",
                    actionName = "Inventory"
                },
                new BindingConfig
                {
                    displayName = "整理背包",
                    actionName = "Sort"
                },
                new BindingConfig
                {
                    displayName = "暂停",
                    actionName = "Pause"
                }
            };
        }

        InputBindingPersistence.Load(inputActionAsset);
        if (visualTheme != null)
        {
            if (visualTheme.interfaceFont != null)
            {
                interfaceFont = visualTheme.interfaceFont;
            }
            else
            {
                interfaceFont = interfaceFont;
            }

            panelColor = visualTheme.menuPanel;
            selectedColor = visualTheme.menuSelection;
            overlayColor = new Color(0.008f, 0.010f, 0.012f, 0.96f);
            accentColor = visualTheme.menuText;
            buttonSprite = null;
            rowSprite = null;
            panelSprite = null;
            sliderTrackSprite = null;
            sliderKnobSprite = null;
        }

        BuildInterface();
        MenuThemeApplier.Apply(panelRoot.transform, visualTheme);
        RefinedMenuStyle.ApplySet(panelRoot.transform, visualTheme);
        OriginalMenuPresentation.ApplySettings(panelRoot.transform, visualTheme);
        BindEvents();
    }

    /// <summary>ResolveInputActionAssetFromPlayer 的业务处理入口；本方法直接依赖 FieldInfo.GetValue(object)、Object.FindObjectsByType&lt;LeonInputBridge&gt;(FindObjectsInactive, FindObjectsSortMode)、Type.GetFields(BindingFlags)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Initialize()；下游（含内部回调体） FieldInfo.GetValue(object)、Object.FindObjectsByType&lt;LeonInputBridge&gt;(FindObjectsInactive, FindObjectsSortMode)、Type.GetFields(BindingFlags)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolveInputActionAssetFromPlayer()
    {
        // 桥梁集合。
LeonInputBridge[] bridges = FindObjectsByType<LeonInputBridge>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (bridges.Length == 0)
        {
            return;
        }

        // 字段集合。
System.Reflection.FieldInfo[] fields = typeof(LeonInputBridge).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        // 当前遍历索引。
for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType != typeof(InputActionAsset))
            {
                continue;
            }

            inputActionAsset = fields[i].GetValue(bridges[0]) as InputActionAsset;
            if (inputActionAsset != null)
            {
                return;
            }
        }
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、List&lt;Button&gt;.Add(Button)、List&lt;Text&gt;.Add(Text) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Initialize()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、List&lt;Button&gt;.Add(Button)、List&lt;Text&gt;.Add(Text) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildInterface()
    {
        // child（子级）：每轮从面板根节点的变换取得一个元素，仅代表当前这一项。使用位置：OptionsTabManager.BuildInterface()。
foreach (Transform child in panelRoot.transform)
        {
            child.gameObject.SetActive(false);
        }

        // 背景板。
Image backdrop = panelRoot.GetComponent<Image>();
        if (backdrop != null)
        {
            backdrop.sprite = null;
            backdrop.color = Color.clear;
            backdrop.raycastTarget = false;
        }

        // 旧根节点。
Transform oldRoot = panelRoot.transform.Find("现代选项布局");
        if (oldRoot != null)
        {
            Destroy(oldRoot.gameObject);
        }

        // 根游戏对象。
GameObject root = CreatePanel("现代选项布局", panelRoot.transform, overlayColor);
        Stretch(root.GetComponent<RectTransform>());
        // 标题栏。
GameObject header = CreatePanel("顶部栏", root.transform, new Color(0f, 0f, 0f, 0.45f));
        SetAnchors(header, new Vector2(0f, 0.86f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        // 标题。
Text title = CreateText("标题", header.transform, "选项", 24, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(title.gameObject, new Vector2(0.05f, 0.22f), new Vector2(0.22f, 0.82f), Vector2.zero, Vector2.zero);
        title.fontStyle = FontStyle.Normal;
        // 分页名称列表。
string[] tabNames =
        {
            "音量",
            "画面设置",
            "操控选项"
        };
        // 当前遍历索引。
for (int i = 0; i < tabNames.Length; i++)
        {
            // 分页索引。
int tabIndex = i;
            // 分页。
Button tab = CreateButton(tabNames[i] + "页签", header.transform, tabNames[i], 20);
            // 分页左侧。
float tabLeft = 0.29f + i * 0.15f;
            SetAnchors(tab.gameObject, new Vector2(tabLeft, 0.18f), new Vector2(tabLeft + 0.13f, 0.82f), Vector2.zero, Vector2.zero);
            tab.onClick.AddListener(() => SelectTab(tabIndex));
            tabButtons.Add(tab);
            tabTexts.Add(tab.GetComponentInChildren<Text>());
        }

        // 快捷键。
Text shortcut = CreateText("页签提示", header.transform, "Q  上一页    E  下一页", 16, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.55f));
        SetAnchors(shortcut.gameObject, new Vector2(0.72f, 0.02f), new Vector2(0.95f, 0.19f), Vector2.zero, Vector2.zero);
        // 内容。
GameObject content = CreatePanel("内容区域", root.transform, panelColor);
        SetAnchors(content, new Vector2(0.075f, 0.13f), new Vector2(0.925f, 0.83f), Vector2.zero, Vector2.zero);
        // 左侧。
GameObject left = CreatePanel("设置列表", content.transform, new Color(0f, 0f, 0f, 0.2f));
        SetAnchors(left, new Vector2(0f, 0f), new Vector2(0.54f, 1f), Vector2.zero, Vector2.zero);
        // 分隔器。
GameObject separator = CreatePanel("分隔线", content.transform, new Color(1f, 1f, 1f, 0.32f));
        SetAnchors(separator, new Vector2(0.54f, 0.02f), new Vector2(0.5407f, 0.96f), Vector2.zero, Vector2.zero);
        // 右侧。
GameObject right = CreatePanel("说明区域", content.transform, new Color(0f, 0f, 0f, 0.12f));
        SetAnchors(right, new Vector2(0.542f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        descriptionTitle = CreateText("说明标题", right.transform, "全局音量", 22, TextAnchor.UpperLeft, Color.white);
        SetAnchors(descriptionTitle.gameObject, new Vector2(0.07f, 0.79f), new Vector2(0.98f, 0.94f), Vector2.zero, Vector2.zero);
        descriptionTitle.fontStyle = FontStyle.Normal;
        descriptionBody = CreateText("说明内容", right.transform, "调节游戏中音乐、环境声、枪声和界面音效的总体音量。", 18, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));
        SetAnchors(descriptionBody.gameObject, new Vector2(0.07f, 0.42f), new Vector2(0.98f, 0.76f), Vector2.zero, Vector2.zero);
        statusText = CreateText("状态提示", right.transform, string.Empty, 17, TextAnchor.LowerLeft, new Color(0.95f, 0.82f, 0.32f, 1f));
        SetAnchors(statusText.gameObject, new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.28f), Vector2.zero, Vector2.zero);
        BuildVolumePage(left.transform);
        BuildDisplayPage(left.transform);
        BuildControlsPage(left.transform);
        // 后方。
Button back = CreateButton("返回按钮", root.transform, "返回", 18);
        SetAnchors(back.gameObject, new Vector2(0.82f, 0.035f), new Vector2(0.93f, 0.085f), Vector2.zero, Vector2.zero);
        back.onClick.AddListener(Hide);
        restoreButton = CreateButton("恢复默认按钮", root.transform, "恢复默认", 18);
        SetAnchors(restoreButton.gameObject, new Vector2(0.63f, 0.035f), new Vector2(0.79f, 0.085f), Vector2.zero, Vector2.zero);
    }

    /// <summary>BuildVolumePage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、List&lt;GameObject&gt;.Add(GameObject)、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePage(string, Transform)、OptionsTabManager.CreateSectionTitle(Transform, string)、OptionsTabManager.CreateSettingRow(Transform, string, float, float) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、List&lt;GameObject&gt;.Add(GameObject)、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePage(string, Transform)、OptionsTabManager.CreateSectionTitle(Transform, string)、OptionsTabManager.CreateSettingRow(Transform, string, float, float) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    void BuildVolumePage(Transform parent)
    {
        // 页面。
GameObject page = CreatePage("音量页面", parent);
        pages.Add(page);
        CreateSectionTitle(page.transform, "音量设置");
        // 行。
GameObject row = CreateSettingRow(page.transform, "总音量", 0.75f, 0.85f);
        masterVolumeSlider = CreateSlider("总音量滑块", row.transform);
        SetAnchors(masterVolumeSlider.gameObject, new Vector2(0.42f, 0.24f), new Vector2(0.78f, 0.76f), Vector2.zero, Vector2.zero);
        masterVolumeValue = CreateText("总音量数值", row.transform, "100%", 20, TextAnchor.MiddleRight, Color.white);
        SetAnchors(masterVolumeValue.gameObject, new Vector2(0.80f, 0f), new Vector2(0.90f, 1f), Vector2.zero, Vector2.zero);
        // 音量状态图标
        // 图标游戏对象。
GameObject iconGo = new GameObject("音量图标", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        volumeIcon = iconGo.GetComponent<Image>();
        volumeIcon.raycastTarget = false;
        volumeIcon.preserveAspect = true;
        SetAnchors(iconGo, new Vector2(0.90f, 0.2f), new Vector2(0.98f, 0.8f), Vector2.zero, Vector2.zero);
        AddHoverDescription(row, "全局音量", "拖动滑块即可同时调整音乐、音效和环境声音量。");
    }

    /// <summary>BuildDisplayPage 的业务处理入口；本方法直接依赖 List&lt;GameObject&gt;.Add(GameObject)、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePage(string, Transform)、OptionsTabManager.CreateSectionTitle(Transform, string)、OptionsTabManager.CreateSettingRow(Transform, string, float, float)、OptionsTabManager.CreateStandardDropdown(string, Transform)、OptionsTabManager.CreateToggle(string, Transform) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildInterface()；下游（含内部回调体） List&lt;GameObject&gt;.Add(GameObject)、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePage(string, Transform)、OptionsTabManager.CreateSectionTitle(Transform, string)、OptionsTabManager.CreateSettingRow(Transform, string, float, float)、OptionsTabManager.CreateStandardDropdown(string, Transform)、OptionsTabManager.CreateToggle(string, Transform) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    void BuildDisplayPage(Transform parent)
    {
        // 页面。
GameObject page = CreatePage("画面页面", parent);
        pages.Add(page);
        CreateSectionTitle(page.transform, "画面设置");
        // 分辨率行。
GameObject resolutionRow = CreateSettingRow(page.transform, "分辨率", 0.75f, 0.85f);
        resolutionDropdown = CreateStandardDropdown("分辨率下拉框", resolutionRow.transform);
        SetAnchors(resolutionDropdown.gameObject, new Vector2(0.42f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        AddHoverDescription(resolutionRow, "分辨率", "选择游戏输出画面的宽度和高度。选择后立即生效。");
        // 全屏行。
GameObject fullscreenRow = CreateSettingRow(page.transform, "全屏显示", 0.63f, 0.73f);
        fullscreenToggle = CreateToggle("全屏开关", fullscreenRow.transform);
        SetAnchors(fullscreenToggle.gameObject, new Vector2(0.82f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        AddHoverDescription(fullscreenRow, "全屏显示", "开启后使用全屏模式；关闭后切换为窗口模式。");
        // 质量行。
GameObject qualityRow = CreateSettingRow(page.transform, "画质预设", 0.51f, 0.61f);
        qualityDropdown = CreateStandardDropdown("画质预设下拉框", qualityRow.transform);
        SetAnchors(qualityDropdown.gameObject, new Vector2(0.62f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        AddHoverDescription(qualityRow, "画质预设", "高画质为默认档。最高画质提供更远的阴影和更高的细节等级。");
        // 帧速率行。
GameObject frameRateRow = CreateSettingRow(page.transform, "帧率上限", 0.39f, 0.49f);
        frameRateDropdown = CreateStandardDropdown("帧率上限下拉框", frameRateRow.transform);
        SetAnchors(frameRateDropdown.gameObject, new Vector2(0.62f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        AddHoverDescription(frameRateRow, "帧率上限", "关闭垂直同步后生效，可限制显卡负载、温度和功耗。");
        // 向量同步行。
GameObject vSyncRow = CreateSettingRow(page.transform, "垂直同步", 0.27f, 0.37f);
        vSyncToggle = CreateToggle("垂直同步开关", vSyncRow.transform);
        SetAnchors(vSyncToggle.gameObject, new Vector2(0.82f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        AddHoverDescription(vSyncRow, "垂直同步", "使画面输出与显示器刷新同步，减少撕裂；启用时帧率上限由显示器刷新率决定。");
        // 抗锯齿行。
GameObject antiAliasingRow = CreateSettingRow(page.transform, "多重采样抗锯齿", 0.15f, 0.25f);
        antiAliasingDropdown = CreateStandardDropdown("抗锯齿下拉框", antiAliasingRow.transform);
        SetAnchors(antiAliasingDropdown.gameObject, new Vector2(0.62f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        AddHoverDescription(antiAliasingRow, "多重采样抗锯齿", "减少物体边缘锯齿。采样倍率越高，画面越平滑，显卡负担也越高。");
        // 图形接口行。
GameObject graphicsApiRow = CreateSettingRow(page.transform, "图形接口", 0.03f, 0.13f);
        graphicsApiDropdown = CreateStandardDropdown("图形接口下拉框", graphicsApiRow.transform);
        SetAnchors(graphicsApiDropdown.gameObject, new Vector2(0.62f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero);
        AddHoverDescription(graphicsApiRow, "图形接口", "项目构建同时支持第十一版和第十二版微软图形接口。所选接口将在下次启动时自动重启一次后生效。");
    }

    /// <summary>BuildControlsPage 的业务处理入口；本方法直接依赖 Canvas.ForceUpdateCanvases()、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;RectMask2D&gt;()、GameObject.AddComponent&lt;ScrollRect&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;ContentSizeFitter&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 29 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildInterface()；下游（含内部回调体） Canvas.ForceUpdateCanvases()、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;RectMask2D&gt;()、GameObject.AddComponent&lt;ScrollRect&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;ContentSizeFitter&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 29 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    void BuildControlsPage(Transform parent)
    {
        // 页面。
GameObject page = CreatePage("操控页面", parent);
        pages.Add(page);
        CreateSectionTitle(page.transform, "操控选项");
        // 固定设置区域：反转纵轴
        // 反转行。
GameObject invertRow = CreateSettingRow(page.transform, "反转纵轴", 0.78f, 0.88f);
        invertYToggle = CreateToggle("反转纵轴开关", invertRow.transform);
        SetAnchors(invertYToggle.gameObject, new Vector2(0.82f, 0.18f), new Vector2(0.94f, 0.82f), Vector2.zero, Vector2.zero);
        AddHoverDescription(invertRow, "反转纵轴", "开启后，鼠标向上移动会让镜头向下转动。");
        // 固定设置区域：瞄准操作方式（双按钮选择器）
        // 瞄准行。
GameObject aimRow = CreatePanel("瞄准操作方式行", page.transform, new Color(1f, 1f, 1f, 0.055f));
        SetAnchors(aimRow, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);
        // 瞄准标签。
Text aimLabel = CreateText("瞄准模式名称", aimRow.transform, "瞄准操作方式", 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.86f));
        SetAnchors(aimLabel.gameObject, new Vector2(0.04f, 0f), new Vector2(0.4f, 1f), Vector2.zero, Vector2.zero);
        aimModeHoldButton = CreateButton("长按按钮", aimRow.transform, "长按", 18);
        SetAnchors(aimModeHoldButton.gameObject, new Vector2(0.44f, 0.1f), new Vector2(0.66f, 0.9f), Vector2.zero, Vector2.zero);
        aimModeHoldButton.onClick.AddListener(() => SetAimMode(0));
        aimModeToggleButton = CreateButton("切换按钮", aimRow.transform, "切换", 18);
        SetAnchors(aimModeToggleButton.gameObject, new Vector2(0.68f, 0.1f), new Vector2(0.90f, 0.9f), Vector2.zero, Vector2.zero);
        aimModeToggleButton.onClick.AddListener(() => SetAimMode(1));
        AddHoverDescription(aimRow, "瞄准操作方式", "长按=按住瞄准键进入瞄准，松开退出。切换=按一次进入瞄准，再按一次退出。");
        // 分隔线
        // 分隔。
GameObject sep = CreatePanel("分隔线", page.transform, new Color(1f, 1f, 1f, 0.2f));
        SetAnchors(sep, new Vector2(0.04f, 0.665f), new Vector2(0.96f, 0.67f), Vector2.zero, Vector2.zero);
        // 按键绑定滚动区域
        // 视口。
GameObject viewport = CreatePanel("按键滚动视口", page.transform, new Color(0f, 0f, 0f, 0.15f));
        SetAnchors(viewport, new Vector2(0.04f, 0.05f), new Vector2(0.95f, 0.66f), Vector2.zero, Vector2.zero);
        viewport.AddComponent<RectMask2D>();
        // 滚动条
        // 滚动条游戏对象。
GameObject scrollbarGo = new GameObject("滚动条", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGo.transform.SetParent(page.transform, false);
        // sbRt（文本构建器矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。
var sbRt = scrollbarGo.GetComponent<RectTransform>();
        SetAnchors(scrollbarGo, new Vector2(0.958f, 0.05f), new Vector2(0.972f, 0.66f), Vector2.zero, Vector2.zero);
        // 文本构建器背景。
var sbBg = scrollbarGo.GetComponent<Image>();
        sbBg.color = new Color(1f, 1f, 1f, 0.1f);
        // 滚动条。
var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        // 滑块
        // 滑移中区域。
GameObject slidingArea = new GameObject("滑动区域", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarGo.transform, false);
        // 滚动区域矩形变换。
var saRt = slidingArea.GetComponent<RectTransform>();
        saRt.anchorMin = Vector2.zero;
        saRt.anchorMax = Vector2.one;
        saRt.offsetMin = new Vector2(2, 2);
        saRt.offsetMax = new Vector2(-2, -2);
        // 句柄。
GameObject handle = CreatePanel("滑块", slidingArea.transform, new Color(0.72f, 0.72f, 0.68f, 0.8f));
        Stretch(handle.GetComponent<RectTransform>());
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        // 内容列表
        // 列表。
GameObject list = new GameObject("按键列表", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(viewport.transform, false);
        // 列表矩形。
RectTransform listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = new Vector2(0f, 0f);
        // 布局。
VerticalLayoutGroup layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        // 尺寸适配器。
ContentSizeFitter fitter = list.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        // 滚动。
ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = listRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        BuildSensitivityRows(list.transform);
        CreateLayoutSectionTitle(list.transform, "按键绑定");
        BuildBindingRows(list.transform);
        // 强制重建布局
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listRect);
    }

    /// <summary>BuildSensitivityRows 的业务处理入口；本方法直接依赖 OptionsTabManager.CreateLayoutSectionTitle(Transform, string)、OptionsTabManager.CreateSensitivityRow(Transform, string, string, float, float, out Text)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)；下游（含内部回调体） OptionsTabManager.CreateLayoutSectionTitle(Transform, string)、OptionsTabManager.CreateSensitivityRow(Transform, string, string, float, float, out Text)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    void BuildSensitivityRows(Transform parent)
    {
        CreateLayoutSectionTitle(parent, "镜头灵敏度");
        sensitivitySliderX = CreateSensitivityRow(parent, "横向灵敏度", "调节镜头左右旋转的响应速度。", RE4CameraController.MinimumLookSensitivity, RE4CameraController.MaximumLookSensitivity, out sensitivityValueX);
        sensitivitySliderY = CreateSensitivityRow(parent, "纵向灵敏度", "调节镜头上下旋转的响应速度。", RE4CameraController.MinimumLookSensitivity, RE4CameraController.MaximumLookSensitivity, out sensitivityValueY);
        aimSensitivitySlider = CreateSensitivityRow(parent, "瞄准灵敏度", "调节进入瞄准状态后的镜头响应倍率。", RE4CameraController.MinimumLookSensitivity, RE4CameraController.MaximumAimSensitivity, out aimSensitivityValue);
    }

    /// <summary>CreateSensitivityRow 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.AddComponent&lt;LayoutElement&gt;()、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.CreateSlider(string, Transform)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildSensitivityRows(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.AddComponent&lt;LayoutElement&gt;()、OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.CreateSlider(string, Transform)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="label">标签。</param>
    /// <param name="description">描述。</param>
    /// <param name="minimum">最小。</param>
    /// <param name="maximum">最大。</param>
    /// <param name="valueText">值文字。输出参数，由本入口写入并交给调用方。</param>
    Slider CreateSensitivityRow(Transform parent, string label, string description, float minimum, float maximum, out Text valueText)
    {
        // 行。
GameObject row = CreatePanel(label + "行", parent, new Color(1f, 1f, 1f, 0.055f));
        // 布局元素。
LayoutElement layoutElement = row.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 54f;
        layoutElement.minHeight = 54f;
        // 标签文字。
Text labelText = CreateText("名称", row.transform, label, 18, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.86f));
        SetAnchors(labelText.gameObject, new Vector2(0.04f, 0f), new Vector2(0.31f, 1f), Vector2.zero, Vector2.zero);
        // 滑块。
Slider slider = CreateSlider(label + "滑块", row.transform);
        slider.minValue = minimum;
        slider.maxValue = maximum;
        SetAnchors(slider.gameObject, new Vector2(0.33f, 0.22f), new Vector2(0.80f, 0.78f), Vector2.zero, Vector2.zero);
        valueText = CreateText("数值", row.transform, "1.0", 18, TextAnchor.MiddleRight, Color.white);
        SetAnchors(valueText.gameObject, new Vector2(0.82f, 0f), new Vector2(0.95f, 1f), Vector2.zero, Vector2.zero);
        AddHoverDescription(row, label, description);
        return slider;
    }

    /// <summary>CreateLayoutSectionTitle 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.AddComponent&lt;LayoutElement&gt;()、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildSensitivityRows(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.AddComponent&lt;LayoutElement&gt;()、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="title">标题。</param>
    void CreateLayoutSectionTitle(Transform parent, string title)
    {
        // 文字。
Text text = CreateText(title + "标题", parent, title, 19, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.62f));
        // 元素。
LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 34f;
        element.minHeight = 34f;
    }

    /// <summary>BuildBindingRows 的业务处理入口；本方法直接依赖 Canvas.ForceUpdateCanvases()、Color.Color(float, float, float, float)、Debug.LogWarning(object)、GameObject.AddComponent&lt;LayoutElement&gt;()、InputActionAsset.FindActionMap(string, bool)、InputActionMap.FindAction(string, bool)、LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)；下游（含内部回调体） Canvas.ForceUpdateCanvases()、Color.Color(float, float, float, float)、Debug.LogWarning(object)、GameObject.AddComponent&lt;LayoutElement&gt;()、InputActionAsset.FindActionMap(string, bool)、InputActionMap.FindAction(string, bool)、LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    void BuildBindingRows(Transform parent)
    {
        bindingRows.Clear();
        // 玩家映射。
InputActionMap playerMap;
        if (inputActionAsset != null)
        {
            playerMap = inputActionAsset.FindActionMap("Player");
        }
        else
        {
            playerMap = null;
        }

        if (playerMap == null)
        {
            statusText.text = "未绑定玩家输入资源，无法修改按键。";
            return;
        }

        // config（当前绑定配置）：每轮从绑定配置集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogWarning(object) 的参数、作为 InputActionMap.FindAction(string, bool) 的参数、作为 OptionsTabManager.AddHoverDescription(GameObject, string, string) 的参数 等共 8 处。
foreach (BindingConfig config in bindingConfigs)
        {
            // 兼容仍序列化旧近战配置的场景。
            if (config.actionName == "Melee")
            {
                continue;
            }

            if (string.IsNullOrEmpty(config.actionName))
            {
                Debug.LogWarning("[选项] 绑定配置缺少 actionName: " + config.displayName);
                continue;
            }

            // 动作。
InputAction action = playerMap.FindAction(config.actionName);
            if (action == null)
            {
                Debug.LogWarning("[选项] 输入动作不存在: " + config.actionName + " (" + config.displayName + ")");
                continue;
            }

            // 绑定索引。
int bindingIndex = FindBindingIndex(action, config.compositePart);
            if (bindingIndex < 0)
            {
                Debug.LogWarning("[选项] 绑定索引未找到: " + config.actionName + " part=" + config.compositePart);
                continue;
            }

            // 按钮。
Button button = CreateButton("按键_" + config.displayName, parent, string.Empty, 18);
            // 元素。
LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 48f;
            element.minHeight = 48f;
            // 名称文字。
Text nameText = CreateText("功能名称", button.transform, config.displayName, 18, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.82f));
            SetAnchors(nameText.gameObject, new Vector2(0.04f, 0f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);
            // 值文字。
Text valueText = CreateText("当前按键", button.transform, string.Empty, 18, TextAnchor.MiddleRight, Color.white);
            SetAnchors(valueText.gameObject, new Vector2(0.6f, 0f), new Vector2(0.94f, 1f), Vector2.zero, Vector2.zero);
            // 行。
BindingRow row = new BindingRow
            {
                action = action,
                bindingIndex = bindingIndex,
                displayName = config.displayName,
                button = button,
                valueText = valueText
            };
            button.onClick.AddListener(() => BeginRebinding(row));
            AddHoverDescription(button.gameObject, config.displayName, config.description);
            bindingRows.Add(row);
        }

        RefreshBindingLabels();
        // 重建布局
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(parent as RectTransform);
    }

    /// <summary>FindBindingIndex 的业务处理入口；本方法直接依赖 OptionsTabManager.IsKeyboardOrMouse(string)、string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)；下游（含内部回调体） OptionsTabManager.IsKeyboardOrMouse(string)、string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">动作。</param>
    /// <param name="compositePart">组合部件。</param>
    int FindBindingIndex(InputAction action, string compositePart)
    {
        // 当前遍历索引。
for (int i = 0; i < action.bindings.Count; i++)
        {
            // 绑定。
InputBinding binding = action.bindings[i];
            if (binding.isComposite)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(compositePart))
            {
                if (binding.isPartOfComposite && string.Equals(binding.name, compositePart, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                continue;
            }

            if (!binding.isPartOfComposite && IsKeyboardOrMouse(binding.effectivePath))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>IsKeyboardOrMouse 的业务处理入口；本方法直接依赖 string.IsNullOrEmpty(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.FindBindingIndex(InputAction, string)；下游（含内部回调体） string.IsNullOrEmpty(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    bool IsKeyboardOrMouse(string path)
    {
        return !string.IsNullOrEmpty(path) && (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>BindEvents 的业务处理入口；本方法直接依赖 UnityEvent.AddListener(UnityAction)、UnityEvent&lt;bool&gt;.AddListener(UnityAction&lt;bool&gt;)、UnityEvent&lt;float&gt;.AddListener(UnityAction&lt;float&gt;)、UnityEvent&lt;int&gt;.AddListener(UnityAction&lt;int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Initialize()；下游（含内部回调体） UnityEvent.AddListener(UnityAction)、UnityEvent&lt;bool&gt;.AddListener(UnityAction&lt;bool&gt;)、UnityEvent&lt;float&gt;.AddListener(UnityAction&lt;float&gt;)、UnityEvent&lt;int&gt;.AddListener(UnityAction&lt;int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BindEvents()
    {
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        sensitivitySliderX.onValueChanged.AddListener(OnSensitivityChanged);
        sensitivitySliderY.onValueChanged.AddListener(OnSensitivityChanged);
        aimSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);
        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
        graphicsApiDropdown.onValueChanged.AddListener(OnGraphicsApiChanged);
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
        restoreButton.onClick.AddListener(RestoreDefaults);
    }

    /// <summary>LoadSettings 的业务处理入口；本方法直接依赖 GameSettingsData.ReadInteger(string, int)、GameSettingsData.ReadSmallCount(string, float)、IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)、OptionsTabManager.BuildGraphicsOptions()、OptionsTabManager.BuildResolutionOptions()、OptionsTabManager.RefreshBindingLabels() 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Show()；下游（含内部回调体） GameSettingsData.ReadInteger(string, int)、GameSettingsData.ReadSmallCount(string, float)、IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)、OptionsTabManager.BuildGraphicsOptions()、OptionsTabManager.BuildResolutionOptions()、OptionsTabManager.RefreshBindingLabels() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadSettings()
    {
        TryBindAudioService();
        // 音量。
float volume;
        if (volumeService != null)
        {
            volume = volumeService.MasterVolume;
        }
        else
        {
            volume = 1f;
        }

        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue8 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.SetMasterVolume(volume);
        }

        masterVolumeSlider.SetValueWithoutNotify(volume);
        UpdateVolumeValue(volume);
        // 调用前缓存的全屏切换。
global::UnityEngine.UI.Toggle callReceiver11 = fullscreenToggle;
        // 按原顺序缓存的实参文字“Fullscreen”。
string argumentValue10 = "Fullscreen";
        // 分支选出的1。
int selectedValue9;
        if (Screen.fullScreen)
        {
            selectedValue9 = 1;
        }
        else
        {
            selectedValue9 = 0;
        }

        callReceiver11.SetIsOnWithoutNotify(GameSettingsData.ReadInteger(argumentValue10, selectedValue9) == 1);
        invertYToggle.SetIsOnWithoutNotify(GameSettingsData.ReadInteger("InvertY", 0) == 1);
        UpdateAimModeUI(GameSettingsData.ReadInteger("AimMode", 0));
        // 水平灵敏度。
float horizontalSensitivity = GameSettingsData.ReadSmallCount(RE4RThirdPersonCamera.HorizontalSensitivityPreferenceKey, RE4RThirdPersonCamera.DefaultHorizontalSensitivity);
        // 竖直灵敏度。
float verticalSensitivity = GameSettingsData.ReadSmallCount(RE4RThirdPersonCamera.VerticalSensitivityPreferenceKey, RE4RThirdPersonCamera.DefaultVerticalSensitivity);
        // 瞄准灵敏度。
float aimSensitivity = GameSettingsData.ReadSmallCount(RE4RThirdPersonCamera.AimSensitivityPreferenceKey, RE4RThirdPersonCamera.DefaultAimSensitivity);
        sensitivitySliderX.SetValueWithoutNotify(horizontalSensitivity);
        sensitivitySliderY.SetValueWithoutNotify(verticalSensitivity);
        aimSensitivitySlider.SetValueWithoutNotify(aimSensitivity);
        UpdateSensitivityValues();
        ResolveCameraController();
        SettingsApplicationAdapter.ApplyHasSaveSensitivity(horizontalSensitivity, verticalSensitivity, aimSensitivity, cameraController);
        BuildGraphicsOptions();
        BuildResolutionOptions();
        RefreshBindingLabels();
        statusText.text = string.Empty;
    }

    /// <summary>BuildGraphicsOptions 的业务处理入口；本方法直接依赖 Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.RefreshShownValue()、Dropdown.SetValueWithoutNotify(int)、GraphicsSettingsRuntime.GetAntiAliasingIndex()、GraphicsSettingsRuntime.GetFrameRateIndex()、GraphicsSettingsRuntime.GetGraphicsApiPreference() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()；下游（含内部回调体） Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.RefreshShownValue()、Dropdown.SetValueWithoutNotify(int)、GraphicsSettingsRuntime.GetAntiAliasingIndex()、GraphicsSettingsRuntime.GetFrameRateIndex()、GraphicsSettingsRuntime.GetGraphicsApiPreference() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildGraphicsOptions()
    {
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string> { "低", "高", "最高" });
        qualityDropdown.SetValueWithoutNotify(GraphicsSettingsRuntime.GetQualityPreset());
        qualityDropdown.RefreshShownValue();
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(new List<string> { "不限制", "30 帧", "60 帧", "90 帧", "120 帧", "144 帧" });
        frameRateDropdown.SetValueWithoutNotify(GraphicsSettingsRuntime.GetFrameRateIndex());
        frameRateDropdown.RefreshShownValue();
        vSyncToggle.SetIsOnWithoutNotify(GraphicsSettingsRuntime.GetVSync());
        antiAliasingDropdown.ClearOptions();
        antiAliasingDropdown.AddOptions(new List<string> { "关闭", "二倍采样", "四倍采样", "八倍采样" });
        antiAliasingDropdown.SetValueWithoutNotify(GraphicsSettingsRuntime.GetAntiAliasingIndex());
        antiAliasingDropdown.RefreshShownValue();
        graphicsApiDropdown.ClearOptions();
        graphicsApiDropdown.AddOptions(new List<string> { "微软图形接口 11", "微软图形接口 12" });
        graphicsApiDropdown.SetValueWithoutNotify(GraphicsSettingsRuntime.GetGraphicsApiPreference());
        graphicsApiDropdown.RefreshShownValue();
    }

    /// <summary>BuildResolutionOptions 的业务处理入口；本方法直接依赖 Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.RefreshShownValue()、Dropdown.SetValueWithoutNotify(int)、GameSettingsData.ReadInteger(string, int)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.HashSet() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.RestoreDefaults()；下游（含内部回调体） Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.RefreshShownValue()、Dropdown.SetValueWithoutNotify(int)、GameSettingsData.ReadInteger(string, int)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.HashSet() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildResolutionOptions()
    {
        availableResolutions.Clear();
        // 已见。
HashSet<string> seen = new HashSet<string>();
        // 固定基础列表
        // 基础资源。
int[][] baseRes =
        {
            new[]
            {
                1024,
                768
            },
            new[]
            {
                1280,
                720
            },
            new[]
            {
                1366,
                768
            },
            new[]
            {
                1600,
                900
            },
            new[]
            {
                1920,
                1080
            },
            new[]
            {
                2560,
                1440
            }
        };
        // r（当前整数数组）：每轮从基础资源取得一个元素，仅代表当前这一项。使用位置：作为 List<Resolution>.Add(Resolution) 的参数、OptionsTabManager.BuildResolutionOptions()。
foreach (int[] r in baseRes)
        {
            // 键。
string key = r[0] + "×" + r[1];
            if (seen.Add(key))
            {
                availableResolutions.Add(new Resolution { width = r[0], height = r[1] });
            }
        }

        // 合并系统分辨率，过滤过小尺寸
        // resolution（分辨率）：每轮从屏幕的分辨率集合取得一个元素，仅代表当前这一项。使用位置：作为 List<Resolution>.Add(Resolution) 的参数、OptionsTabManager.BuildResolutionOptions()。
foreach (Resolution resolution in Screen.resolutions)
        {
            if (resolution.width < 800 || resolution.height < 600)
            {
                continue;
            }

            // 键。
string key = resolution.width + "×" + resolution.height;
            if (seen.Add(key))
            {
                availableResolutions.Add(resolution);
            }
        }

        // 按面积排序
        availableResolutions.Sort(( /* 中文含义：当前分辨率。 */a, /* 中文含义：当前分辨率。 */ b) => (a.width * a.height).CompareTo(b.width * b.height));
        // 标签集合。
List<string> labels = new List<string>();
        // 选定。
int selected = 0;
        // 已保存宽度。
int savedWidth = GameSettingsData.ReadInteger("ResolutionWidth", 1920);
        // 已保存高度。
int savedHeight = GameSettingsData.ReadInteger("ResolutionHeight", 1080);
        // 如果保存的分辨率无效（如编辑器自由窗口尺寸），默认选 1920×1080
        // 是否找到的已保存。
bool foundSaved = false;
        // 当前遍历索引。
for (int i = 0; i < availableResolutions.Count; i++)
        {
            // 分辨率。
Resolution resolution = availableResolutions[i];
            labels.Add(resolution.width + " × " + resolution.height);
            if (resolution.width == savedWidth && resolution.height == savedHeight)
            {
                selected = i;
                foundSaved = true;
            }
        }

        if (!foundSaved)
        {
            // 尝试找 1920×1080
            // 当前遍历索引。
for (int i = 0; i < availableResolutions.Count; i++)
            {
                if (availableResolutions[i].width == 1920 && availableResolutions[i].height == 1080)
                {
                    selected = i;
                    break;
                }
            }
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
        resolutionDropdown.SetValueWithoutNotify(selected);
        resolutionDropdown.RefreshShownValue();
    }

    /// <summary>OnMasterVolumeChanged 的业务处理入口；本方法直接依赖 IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)、OptionsTabManager.TryBindAudioService()、OptionsTabManager.UpdateVolumeValue(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IAudioVolumeService.SetMasterVolume(float)、object.ReferenceEquals(object, object)、OptionsTabManager.TryBindAudioService()、OptionsTabManager.UpdateVolumeValue(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    void OnMasterVolumeChanged(float value)
    {
        TryBindAudioService();
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue12 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            receiverValue12.SetMasterVolume(value);
        }

        UpdateVolumeValue(value);
        PlaySettingAdjustment();
    }

    /// <summary>UpdateVolumeValue 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Mathf.RoundToInt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.OnExternalVolumeChanged(float)、OptionsTabManager.OnMasterVolumeChanged(float)；下游（含内部回调体） Color.Color(float, float, float, float)、Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    void UpdateVolumeValue(float value)
    {
        masterVolumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        if (volumeIcon != null)
        {
            if (value <= 0f)
            {
                volumeIcon.sprite = soundOffIcon;
                volumeIcon.color = new Color(1f, 1f, 1f, 0.7f);
            }
            else
            {
                volumeIcon.sprite = soundOnIcon;
                volumeIcon.color = Color.white;
            }
        }
    }

    /// <summary>OnResolutionChanged 的业务处理入口；本方法直接依赖 SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= availableResolutions.Count)
        {
            return;
        }

        // resolution（分辨率）：使用位置：作为 SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool) 的参数、OptionsTabManager.OnResolutionChanged(int)。
        PlaySettingAdjustment();
Resolution resolution = availableResolutions[index];
        SettingsApplicationAdapter.ModifyResolution(resolution.width, resolution.height, fullscreenToggle.isOn, false);
        statusText.text = "分辨率已切换为 " + resolution.width + " × " + resolution.height;
    }

    /// <summary>OnFullscreenChanged 的业务处理入口；本方法直接依赖 SettingsApplicationAdapter.ModifyFullScreen(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsApplicationAdapter.ModifyFullScreen(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fullscreen">是否全屏。</param>
    void OnFullscreenChanged(bool fullscreen)
    {
        PlaySettingAdjustment();
        SettingsApplicationAdapter.ModifyFullScreen(fullscreen);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver13 = statusText;
        if (fullscreen)
        {
            assignmentReceiver13.text = "已切换为全屏显示。";
        }
        else
        {
            assignmentReceiver13.text = "已切换为窗口显示。";
        }
    }

    /// <summary>OnQualityChanged 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.SetQualityPreset(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.SetQualityPreset(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnQualityChanged(int index)
    {
        PlaySettingAdjustment();
        GraphicsSettingsRuntime.SetQualityPreset(index);
        statusText.text = "画质预设已切换为" + qualityDropdown.options[index].text + "。";
    }

    /// <summary>OnFrameRateChanged 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.SetFrameRateIndex(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.SetFrameRateIndex(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnFrameRateChanged(int index)
    {
        PlaySettingAdjustment();
        GraphicsSettingsRuntime.SetFrameRateIndex(index);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver15 = statusText;
        if (vSyncToggle.isOn)
        {
            assignmentReceiver15.text = "帧率上限已保存；关闭垂直同步后生效。";
        }
        else
        {
            assignmentReceiver15.text = "帧率上限已切换为" + frameRateDropdown.options[index].text + "。";
        }
    }

    /// <summary>OnVSyncChanged 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.SetVSync(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.SetVSync(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enabled">是否启用。</param>
    void OnVSyncChanged(bool enabled)
    {
        PlaySettingAdjustment();
        GraphicsSettingsRuntime.SetVSync(enabled);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver17 = statusText;
        if (enabled)
        {
            assignmentReceiver17.text = "垂直同步已开启，帧率将跟随显示器刷新率。";
        }
        else
        {
            assignmentReceiver17.text = "垂直同步已关闭，已使用所选帧率上限。";
        }
    }

    /// <summary>OnAntiAliasingChanged 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.SetAntiAliasingIndex(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.SetAntiAliasingIndex(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnAntiAliasingChanged(int index)
    {
        PlaySettingAdjustment();
        GraphicsSettingsRuntime.SetAntiAliasingIndex(index);
        statusText.text = "抗锯齿已切换为" + antiAliasingDropdown.options[index].text + "。";
    }

    /// <summary>OnGraphicsApiChanged 的业务处理入口；本方法直接依赖 GraphicsSettingsRuntime.IsPreferredGraphicsApiActive()、GraphicsSettingsRuntime.SetGraphicsApiPreference(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GraphicsSettingsRuntime.IsPreferredGraphicsApiActive()、GraphicsSettingsRuntime.SetGraphicsApiPreference(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnGraphicsApiChanged(int index)
    {
        PlaySettingAdjustment();
        GraphicsSettingsRuntime.SetGraphicsApiPreference(index);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver19 = statusText;
        if (GraphicsSettingsRuntime.IsPreferredGraphicsApiActive())
        {
            assignmentReceiver19.text = "当前已经使用所选图形接口。";
        }
        else
        {
            assignmentReceiver19.text = "图形接口选择已保存，将在下次启动时自动重启一次后生效。";
        }
    }

    /// <summary>OnInvertYChanged 的业务处理入口；本方法直接依赖 OptionsTabManager.ResolveCameraController()、SettingsApplicationAdapter.ModifyVerticalAxis(bool, RE4CameraController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsTabManager.ResolveCameraController()、SettingsApplicationAdapter.ModifyVerticalAxis(bool, RE4CameraController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="inverted">是否反转后的。</param>
    void OnInvertYChanged(bool inverted)
    {
        PlaySettingAdjustment();
        ResolveCameraController();
        SettingsApplicationAdapter.ModifyVerticalAxis(inverted, cameraController);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver21 = statusText;
        if (inverted)
        {
            assignmentReceiver21.text = "纵轴方向已反转。";
        }
        else
        {
            assignmentReceiver21.text = "纵轴方向已恢复默认。";
        }
    }

    /// <summary>OnSensitivityChanged 的业务处理入口；本方法直接依赖 OptionsTabManager.ResolveCameraController()、OptionsTabManager.UpdateSensitivityValues()、SettingsApplicationAdapter.ModifySensitivity(float, float, float, RE4CameraController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsTabManager.ResolveCameraController()、OptionsTabManager.UpdateSensitivityValues()、SettingsApplicationAdapter.ModifySensitivity(float, float, float, RE4CameraController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。</param>
    void OnSensitivityChanged(float value)
    {
        PlaySettingAdjustment();
        ResolveCameraController();
        SettingsApplicationAdapter.ModifySensitivity(sensitivitySliderX.value, sensitivitySliderY.value, aimSensitivitySlider.value, cameraController);
        UpdateSensitivityValues();
        statusText.text = "镜头灵敏度已更新。";
    }

    /// <summary>ResolveCameraController 的业务处理入口；本方法直接依赖 Object.FindFirstObjectByType&lt;RE4CameraController&gt;(FindObjectsInactive)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.OnInvertYChanged(bool)、OptionsTabManager.OnSensitivityChanged(float)；下游（含内部回调体） Object.FindFirstObjectByType&lt;RE4CameraController&gt;(FindObjectsInactive)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolveCameraController()
    {
        if (cameraController != null)
        {
            return;
        }

        cameraController = FindFirstObjectByType<RE4CameraController>(FindObjectsInactive.Include);
    }

    /// <summary>UpdateSensitivityValues 的业务处理入口；本方法直接依赖 float.ToString(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.OnSensitivityChanged(float)；下游（含内部回调体） float.ToString(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSensitivityValues()
    {
        if (sensitivityValueX != null)
        {
            sensitivityValueX.text = sensitivitySliderX.value.ToString("0.0");
        }

        if (sensitivityValueY != null)
        {
            sensitivityValueY.text = sensitivitySliderY.value.ToString("0.0");
        }

        if (aimSensitivityValue != null)
        {
            aimSensitivityValue.text = aimSensitivitySlider.value.ToString("0.0");
        }
    }

    /// <summary>SetAimMode 的业务处理入口；本方法直接依赖 OptionsTabManager.UpdateAimModeUI(int)、SettingsApplicationAdapter.ModifyAimMode(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.RestoreDefaults()；下游（含内部回调体） OptionsTabManager.UpdateAimModeUI(int)、SettingsApplicationAdapter.ModifyAimMode(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">模式。</param>
    void SetAimMode(int mode)
    {
        PlaySettingAdjustment();
        SettingsApplicationAdapter.ModifyAimMode(mode);
        UpdateAimModeUI(mode);
        // 赋值前保存的目标状态文字。
global::UnityEngine.UI.Text assignmentReceiver23 = statusText;
        if (mode == 1)
        {
            assignmentReceiver23.text = "瞄准模式已切换为切换模式。";
        }
        else
        {
            assignmentReceiver23.text = "瞄准模式已切换为长按模式。";
        }
    }

    /// <summary>UpdateAimModeUI 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.LoadSettings()、OptionsTabManager.SetAimMode(int)；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">模式。</param>
    void UpdateAimModeUI(int mode)
    {
        if (aimModeHoldButton != null)
        {
            // 保持图像。
var holdImg = aimModeHoldButton.image;
            // 赋值前保存的目标保持图像。
global::UnityEngine.UI.Image assignmentReceiver25 = holdImg;
            if (mode == 0)
            {
                assignmentReceiver25.color = new Color(.80f, .82f, .76f, .23f);
            }
            else
            {
                assignmentReceiver25.color = new Color(1f, 1f, 1f, .035f);
            }

            // 标签。
var label = aimModeHoldButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                // 赋值前保存的目标标签。
global::UnityEngine.UI.Text assignmentReceiver27 = label;
                if (mode == 0)
                {
                    assignmentReceiver27.text = "● 长按";
                }
                else
                {
                    assignmentReceiver27.text = "长按";
                }
            }
        }

        if (aimModeToggleButton != null)
        {
            // 切换图像。
var toggleImg = aimModeToggleButton.image;
            // 赋值前保存的目标切换图像。
global::UnityEngine.UI.Image assignmentReceiver29 = toggleImg;
            if (mode == 1)
            {
                assignmentReceiver29.color = new Color(.80f, .82f, .76f, .23f);
            }
            else
            {
                assignmentReceiver29.color = new Color(1f, 1f, 1f, .035f);
            }

            // 标签。
var label = aimModeToggleButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                // 赋值前保存的目标标签。
global::UnityEngine.UI.Text assignmentReceiver31 = label;
                if (mode == 1)
                {
                    assignmentReceiver31.text = "● 切换";
                }
                else
                {
                    assignmentReceiver31.text = "切换";
                }
            }
        }
    }

    /// <summary>BeginRebinding 的业务处理入口；本方法直接依赖 InputAction.ApplyBindingOverride(int, string)、InputAction.Disable()、InputAction.PerformInteractiveRebinding(int)、InputAction.RemoveBindingOverride(int)、InputActionRebindingExtensions.RebindingOperation.OnCancel(Action&lt;InputActionRebindingExtensions.RebindingOperation&gt;)、InputActionRebindingExtensions.RebindingOperation.OnComplete(Action&lt;InputActionRebindingExtensions.RebindingOperation&gt;)、InputActionRebindingExtensions.RebindingOperation.Start() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)；下游（含内部回调体） InputAction.ApplyBindingOverride(int, string)、InputAction.Disable()、InputAction.PerformInteractiveRebinding(int)、InputAction.RemoveBindingOverride(int)、InputActionRebindingExtensions.RebindingOperation.OnCancel(Action&lt;InputActionRebindingExtensions.RebindingOperation&gt;)、InputActionRebindingExtensions.RebindingOperation.OnComplete(Action&lt;InputActionRebindingExtensions.RebindingOperation&gt;)、InputActionRebindingExtensions.RebindingOperation.Start() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="row">行。</param>
    void BeginRebinding(BindingRow row)
    {
        if (row == null || row.action == null || rebindOperation != null)
        {
            return;
        }

        // 禁用所有页签按钮和其他绑定行
        SetInteractable(false);
        // 旧绑定。
InputBinding oldBinding = row.action.bindings[row.bindingIndex];
        // 旧覆盖路径。
string oldOverridePath = oldBinding.overridePath;
        // 动作之前是否启用。
bool actionWasEnabled = row.action.enabled;
        if (actionWasEnabled)
        {
            row.action.Disable();
        }

        AudioManager.PlayInterface(AudioEventNames.UISettingsConfirm);
        row.valueText.text = "等待输入…";
        statusText.text = "请按下[" + row.displayName + "]的新按键；按退出键取消。";
        rebindOperation = row.action.PerformInteractiveRebinding(row.bindingIndex).WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll").WithCancelingThrough("<Keyboard>/escape").OnCancel( /* 中文含义：操作。 */operation =>
        {
            FinishRebinding(row, actionWasEnabled);
            statusText.text = "已取消按键修改。";
            if (!hiding)
            {
                AudioManager.PlayInterface(AudioEventNames.UISettingsCancel);
            }
        }).OnComplete( /* 中文含义：操作。 */operation =>
        {
            // 新路径。
string newPath = row.action.bindings[row.bindingIndex].effectivePath;
            // 冲突。
BindingRow conflict = FindConflict(row, newPath);
            if (conflict != null)
            {
                if (string.IsNullOrEmpty(oldOverridePath))
                {
                    row.action.RemoveBindingOverride(row.bindingIndex);
                }
                else
                {
                    row.action.ApplyBindingOverride(row.bindingIndex, oldOverridePath);
                }

                AudioManager.PlayInterface(AudioEventNames.UISettingsError);
                statusText.text = "该按键已用于[" + conflict.displayName + "]，本次修改未保存。";
            }
            else
            {
                InputBindingPersistence.SaveValue(inputActionAsset);
                AudioManager.PlayInterface(AudioEventNames.UISettingsConfirm);
                statusText.text = "[" + row.displayName + "]按键已更新。";
            }

            FinishRebinding(row, actionWasEnabled);
        });
        rebindOperation.Start();
    }

    /// <summary>SetInteractable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BeginRebinding(OptionsTabManager.BindingRow)、OptionsTabManager.FinishRebinding(OptionsTabManager.BindingRow, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactable">是否可交互。</param>
    void SetInteractable(bool interactable)
    {
        // tab（分页）：每轮从分页按钮集合取得一个元素，仅代表当前这一项。使用位置：OptionsTabManager.SetInteractable(bool)。
foreach (var tab in tabButtons)
        {
            if (tab != null)
            {
                tab.interactable = interactable;
            }
        }

        // row（行）：每轮从绑定行数取得一个元素，仅代表当前这一项。使用位置：OptionsTabManager.SetInteractable(bool)。
foreach (var row in bindingRows)
        {
            if (row.button != null)
            {
                row.button.interactable = interactable;
            }
        }
    }

    /// <summary>FindConflict 的业务处理入口；本方法直接依赖 string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BeginRebinding(OptionsTabManager.BindingRow)；下游（含内部回调体） string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源绑定行。</param>
    /// <param name="path">路径。</param>
    BindingRow FindConflict(BindingRow source, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // row（行）：每轮从绑定行数取得一个元素，仅代表当前这一项。使用位置：作为 ReadOnlyArray<InputBinding>.this[int] 的参数、作为 string.Equals(string, string, StringComparison) 的参数、OptionsTabManager.FindConflict(OptionsTabManager.BindingRow, string)。
foreach (BindingRow row in bindingRows)
        {
            if (row == source)
            {
                continue;
            }

            if (string.Equals(row.action.bindings[row.bindingIndex].effectivePath, path, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>FinishRebinding 的业务处理入口；本方法直接依赖 InputAction.Enable()、InputActionRebindingExtensions.RebindingOperation.Dispose()、object.ReferenceEquals(object, object)、OptionsTabManager.RefreshBindingLabels()、OptionsTabManager.SetInteractable(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BeginRebinding(OptionsTabManager.BindingRow)；下游（含内部回调体） InputAction.Enable()、InputActionRebindingExtensions.RebindingOperation.Dispose()、object.ReferenceEquals(object, object)、OptionsTabManager.RefreshBindingLabels()、OptionsTabManager.SetInteractable(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="row">行。</param>
    /// <param name="actionWasEnabled">动作之前是否启用。</param>
    void FinishRebinding(BindingRow row, bool actionWasEnabled)
    {
        // 判空前缓存的重新绑定操作。
global::UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation receiverValue33 = rebindOperation;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.Dispose();
        }

        rebindOperation = null;
        if (actionWasEnabled)
        {
            row.action.Enable();
        }

        RefreshBindingLabels();
        SetInteractable(true);
    }

    /// <summary>CancelRebinding 的业务处理入口；本方法直接依赖 InputActionRebindingExtensions.RebindingOperation.Cancel()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Hide()、OptionsTabManager.OnDisable()；下游（含内部回调体） InputActionRebindingExtensions.RebindingOperation.Cancel()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CancelRebinding()
    {
        if (rebindOperation == null)
        {
            return;
        }

        rebindOperation.Cancel();
    }

    /// <summary>RefreshBindingLabels 的业务处理入口；本方法直接依赖 InputControlPath.ToHumanReadableString(string, InputControlPath.HumanReadableStringOptions, InputControl)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)、OptionsTabManager.FinishRebinding(OptionsTabManager.BindingRow, bool)、OptionsTabManager.LoadSettings()、OptionsTabManager.RestoreDefaults()；下游（含内部回调体） InputControlPath.ToHumanReadableString(string, InputControlPath.HumanReadableStringOptions, InputControl)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RefreshBindingLabels()
    {
        // row（行）：每轮从绑定行数取得一个元素，仅代表当前这一项。使用位置：作为 ReadOnlyArray<InputBinding>.this[int] 的参数、OptionsTabManager.RefreshBindingLabels()。
foreach (BindingRow row in bindingRows)
        {
            // 路径。
string path = row.action.bindings[row.bindingIndex].effectivePath;
            // 标签。
string label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            // 赋值前保存的目标行的值文字。
global::UnityEngine.UI.Text assignmentReceiver34 = row.valueText;
            if (string.IsNullOrEmpty(label))
            {
                assignmentReceiver34.text = "未设置";
            }
            else
            {
                assignmentReceiver34.text = label;
            }
        }
    }

    /// <summary>RestoreDefaults 的业务处理入口；本方法直接依赖 InputBindingPersistence.RestoreDefault(InputActionAsset)、OptionsTabManager.BuildResolutionOptions()、OptionsTabManager.RefreshBindingLabels()、OptionsTabManager.SetAimMode(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InputBindingPersistence.RestoreDefault(InputActionAsset)、OptionsTabManager.BuildResolutionOptions()、OptionsTabManager.RefreshBindingLabels()、OptionsTabManager.SetAimMode(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RestoreDefaults()
    {
        restoringDefaults = true;
        try
        {
            ApplyDefaults();
        }
        finally
        {
            restoringDefaults = false;
        }

        AudioManager.PlayInterface(AudioEventNames.UISettingsConfirm);
    }

    private void ApplyDefaults()
    {
        if (currentTab == 0)
        {
            masterVolumeSlider.value = 1f;
            statusText.text = "总音量已恢复为默认值。";
        }
        else if (currentTab == 1)
        {
            fullscreenToggle.isOn = true;
            qualityDropdown.value = 1;
            frameRateDropdown.value = 2;
            vSyncToggle.isOn = true;
            antiAliasingDropdown.value = 1;
            graphicsApiDropdown.value = 0;
            BuildResolutionOptions();
            // 选择 1920×1080
            // 当前遍历索引。
for (int i = 0; i < availableResolutions.Count; i++)
            {
                if (availableResolutions[i].width == 1920 && availableResolutions[i].height == 1080)
                {
                    resolutionDropdown.value = i;
                    break;
                }
            }

            statusText.text = "画面设置已恢复为默认值。";
        }
        else
        {
            invertYToggle.isOn = false;
            SetAimMode(0);
            sensitivitySliderX.value = RE4RThirdPersonCamera.DefaultHorizontalSensitivity;
            sensitivitySliderY.value = RE4RThirdPersonCamera.DefaultVerticalSensitivity;
            aimSensitivitySlider.value = RE4RThirdPersonCamera.DefaultAimSensitivity;
            InputBindingPersistence.RestoreDefault(inputActionAsset);
            RefreshBindingLabels();
            statusText.text = "操控按键、纵轴方向和镜头灵敏度已恢复为默认值。";
        }
    }

    /// <summary>SelectTab 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Mathf.Clamp(int, int, int)、OptionsTabManager.SetDescription(string, string)、Transform.Find(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildInterface()、OptionsTabManager.Show()、OptionsTabManager.Update()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Mathf.Clamp(int, int, int)、OptionsTabManager.SetDescription(string, string)、Transform.Find(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void SelectTab(int index)
    {
        if (pages.Count == 0)
        {
            return;
        }

        int nextTab = Mathf.Clamp(index, 0, pages.Count - 1);
        if (showRequested && !hiding && nextTab != currentTab)
        {
            focusedSettingRow = null;
            AudioManager.PlayInterface(AudioEventNames.UISettingsTab);
        }

        currentTab = nextTab;
        statusText.text = string.Empty;
        // 当前遍历索引。
for (int i = 0; i < pages.Count; i++)
        {
            pages[i].SetActive(i == currentTab);
            // 赋值前保存的目标分页文字集合。
global::UnityEngine.UI.Text assignmentReceiver36 = tabTexts[i];
            if (i == currentTab)
            {
                assignmentReceiver36.color = Color.white;
            }
            else
            {
                assignmentReceiver36.color = new Color(1f, 1f, 1f, 0.52f);
            }

            // 赋值前保存的目标分页文字集合。
global::UnityEngine.UI.Text assignmentReceiver38 = tabTexts[i];
            if (i == currentTab)
            {
                assignmentReceiver38.fontStyle = FontStyle.Bold;
            }
            else
            {
                assignmentReceiver38.fontStyle = FontStyle.Normal;
            }

            tabButtons[i].image.color = Color.clear;
            // 下划线。
Transform underline = tabButtons[i].transform.Find("行分隔");
            if (underline != null)
            {
                // 赋值前保存的目标取得组件的返回结果。
global::UnityEngine.UI.Image assignmentReceiver40 = underline.GetComponent<Image>();
                if (i == currentTab)
                {
                    assignmentReceiver40.color = new Color(.88f, .86f, .80f, .9f);
                }
                else
                {
                    assignmentReceiver40.color = new Color(.64f, .65f, .59f, .15f);
                }
            }
        }

        if (currentTab == 0)
        {
            SetDescription("全局音量", "调节游戏中音乐、环境声、枪声和界面音效的总体音量。");
        }
        else if (currentTab == 1)
        {
            SetDescription("画面设置", "可调整分辨率、显示模式、画质、帧率、垂直同步、抗锯齿与图形接口。图形接口在下次启动时生效。");
        }
        else
        {
            SetDescription("操控选项", "可调整镜头灵敏度、纵轴方向、瞄准模式，并重新绑定全部操作按键。");
        }
    }

    /// <summary>SetDescription 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.AddHoverDescription(GameObject, string, string)、OptionsTabManager.SelectTab(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="title">标题。</param>
    /// <param name="body">身体。</param>
    void SetDescription(string title, string body)
    {
        descriptionTitle.text = title;
        descriptionBody.text = body;
    }

    /// <summary>AddHoverDescription 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;MenuRowFeedback&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.AddComponent&lt;MenuRowFeedback&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.GetComponentsInChildren&lt;Selectable&gt;(bool) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)、OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)、OptionsTabManager.BuildVolumePage(Transform)、OptionsTabManager.CreateSensitivityRow(Transform, string, string, float, float, out Text)；下游（含内部回调体） Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;MenuRowFeedback&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.AddComponent&lt;MenuRowFeedback&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.GetComponentsInChildren&lt;Selectable&gt;(bool) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标游戏对象。</param>
    /// <param name="title">标题。</param>
    /// <param name="body">身体。</param>
    private void FocusSettingRow(GameObject row, string title, string body)
    {
        SetDescription(title, body);
        if (showRequested && !hiding && rebindOperation == null && focusedSettingRow != row)
        {
            focusedSettingRow = row;
            AudioManager.PlayInterface(AudioEventNames.UISettingsHover);
        }
    }

    private void PlaySettingAdjustment()
    {
        if (showRequested && !hiding && !restoringDefaults)
        {
            AudioManager.PlayInterface(AudioEventNames.UISettingsAdjust);
        }
    }

    void AddHoverDescription(GameObject target, string title, string body)
    {
        // 触发器。
EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.AddComponent<EventTrigger>();
        }

        // 当前入口。
EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        entry.callback.AddListener(_ => FocusSettingRow(target, title, body));
        trigger.triggers.Add(entry);
        // control（控制）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：OptionsTabManager.AddHoverDescription(GameObject, string, string)。
foreach (Selectable control in target.GetComponentsInChildren<Selectable>(true))
        {
            // 导航触发器。
EventTrigger navigationTrigger = control.GetComponent<EventTrigger>();
            if (navigationTrigger == null)
            {
                navigationTrigger = control.gameObject.AddComponent<EventTrigger>();
            }

            // 选定。
var selected = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Select
            };
            selected.callback.AddListener(_ => FocusSettingRow(target, title, body));
            navigationTrigger.triggers.Add(selected);
            // 悬停。
var hover = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            hover.callback.AddListener(_ => FocusSettingRow(target, title, body));
            navigationTrigger.triggers.Add(hover);
            // 空值判断前缓存的取得组件的返回结果。
global::MenuRowFeedback availableValue42 = control.GetComponent<MenuRowFeedback>();
            // 反馈。
global::MenuRowFeedback feedback;
            if (!global::System.Object.ReferenceEquals(availableValue42, null))
            {
                feedback = availableValue42;
            }
            else
            {
                feedback = control.gameObject.AddComponent<MenuRowFeedback>();
            }

            selected.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ => feedback.ScrollIntoViewport());
        }
    }

    /// <summary>FadeIn 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Show()；下游（含内部回调体） Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeIn()
    {
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        // 已用。
float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    /// <summary>FadeOut 的业务处理入口；本方法直接依赖 Action.Invoke()、GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Hide()；下游（含内部回调体） Action.Invoke()、GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOut()
    {
        // 已用。
float elapsed = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        // 起始透明度。
float startAlpha = canvasGroup.alpha;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        hiding = false;
        // 判空前缓存的已关闭时的通知回调。
global::System.Action receiverValue44 = onClosed;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.Invoke();
        }

        panelRoot.SetActive(false);
    }

    /// <summary>CreatePage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、OptionsTabManager.Stretch(RectTransform)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)、OptionsTabManager.BuildVolumePage(Transform)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、OptionsTabManager.Stretch(RectTransform)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    GameObject CreatePage(string name, Transform parent)
    {
        // 页面。
GameObject page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        return page;
    }

    /// <summary>CreateSectionTitle 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)、OptionsTabManager.BuildVolumePage(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="title">标题。</param>
    void CreateSectionTitle(Transform parent, string title)
    {
        // 文字。
Text text = CreateText("分组标题", parent, title, 24, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.62f));
        SetAnchors(text.gameObject, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);
    }

    /// <summary>CreateSettingRow 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)、OptionsTabManager.BuildVolumePage(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="label">标签。</param>
    /// <param name="bottom">底部。</param>
    /// <param name="top">顶部。</param>
    GameObject CreateSettingRow(Transform parent, string label, float bottom, float top)
    {
        // 行。
GameObject row = CreatePanel(label + "行", parent, new Color(1f, 1f, 1f, 0.055f));
        SetAnchors(row, new Vector2(0.04f, bottom), new Vector2(0.96f, top), Vector2.zero, Vector2.zero);
        // 文字。
Text text = CreateText("名称", row.transform, label, 20, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.86f));
        SetAnchors(text.gameObject, new Vector2(0.04f, 0f), new Vector2(0.4f, 1f), Vector2.zero, Vector2.zero);
        return row;
    }

    /// <summary>CreatePanel 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildInterface()、OptionsTabManager.CreateSensitivityRow(Transform, string, string, float, float, out Text)、OptionsTabManager.CreateSettingRow(Transform, string, float, float)、OptionsTabManager.CreateSlider(string, Transform) 等共 6 处；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="color">颜色。</param>
    GameObject CreatePanel(string name, Transform parent, Color color)
    {
        // 面板。
GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        // 图像。
Image image = panel.GetComponent<Image>();
        image.sprite = panelSprite;
        // 赋值前保存的目标图像。
global::UnityEngine.UI.Image assignmentReceiver45 = image;
        if (panelSprite != null)
        {
            assignmentReceiver45.type = Image.Type.Sliced;
        }
        else
        {
            assignmentReceiver45.type = Image.Type.Simple;
        }

        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)、OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildInterface()、OptionsTabManager.BuildVolumePage(Transform)、OptionsTabManager.CreateButton(string, Transform, string, int) 等共 9 处；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="value">值。</param>
    /// <param name="size">尺寸。</param>
    /// <param name="alignment">对齐状态。</param>
    /// <param name="color">颜色。</param>
    Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        // 当前游戏对象。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        // 文字。
Text text = go.GetComponent<Text>();
        text.font = interfaceFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.resizeTextForBestFit = false;
        text.resizeTextMinSize = Mathf.Max(12, size - 8);
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>CreateButton 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.Stretch(RectTransform)、string.IsNullOrEmpty(string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)、OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、OptionsTabManager.CreateText(string, Transform, string, int, TextAnchor, Color)、OptionsTabManager.Stretch(RectTransform)、string.IsNullOrEmpty(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="label">标签。</param>
    /// <param name="size">尺寸。</param>
    Button CreateButton(string name, Transform parent, string label, int size)
    {
        // 当前游戏对象。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        // 图像。
Image image = go.GetComponent<Image>();
        // 赋值前保存的目标图像。
global::UnityEngine.UI.Image assignmentReceiver47 = image;
        if (buttonSprite != null)
        {
            assignmentReceiver47.sprite = buttonSprite;
        }
        else
        {
            assignmentReceiver47.sprite = rowSprite;
        }

        // 赋值前保存的目标图像。
global::UnityEngine.UI.Image assignmentReceiver49 = image;
        if (image.sprite != null)
        {
            assignmentReceiver49.type = Image.Type.Sliced;
        }
        else
        {
            assignmentReceiver49.type = Image.Type.Simple;
        }

        // 赋值前保存的目标图像。
global::UnityEngine.UI.Image assignmentReceiver51 = image;
        if (image.sprite != null)
        {
            assignmentReceiver51.color = new Color(1f, 1f, 1f, 0.72f);
        }
        else
        {
            assignmentReceiver51.color = new Color(1f, 1f, 1f, 0.07f);
        }

        // 按钮。
Button button = go.GetComponent<Button>();
        // 颜色集合。
ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.78f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        if (!string.IsNullOrEmpty(label))
        {
            // 文字。
Text text = CreateText("文字", go.transform, label, size, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform);
        }

        return button;
    }

    /// <summary>CreateSlider 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Slider&gt;()、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildVolumePage(Transform)、OptionsTabManager.CreateSensitivityRow(Transform, string, string, float, float, out Text)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Slider&gt;()、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    Slider CreateSlider(string name, Transform parent)
    {
        // 当前游戏对象。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        // 滑块。
Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        // 背景。
GameObject background = CreatePanel("轨道", go.transform, new Color(1f, 1f, 1f, 0.22f));
        SetAnchors(background, new Vector2(0f, 0.47f), new Vector2(1f, 0.53f), Vector2.zero, Vector2.zero);
        // 背景图像。
var bgImg = background.GetComponent<Image>();
        if (sliderTrackSprite != null)
        {
            bgImg.sprite = sliderTrackSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
        }

        // 填充区域。
GameObject fillArea = new GameObject("填充区域", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        SetAnchors(fillArea, new Vector2(0f, 0.46f), new Vector2(1f, 0.54f), Vector2.zero, Vector2.zero);
        // 填充。
GameObject fill = CreatePanel("填充", fillArea.transform, new Color(0.82f, 0.82f, 0.78f, 1f));
        Stretch(fill.GetComponent<RectTransform>());
        // 句柄区域。
GameObject handleArea = new GameObject("滑块区域", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>());
        // 句柄。
GameObject handle = CreatePanel("滑块", handleArea.transform, Color.white);
        // 句柄矩形。
RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(7f, 22f);
        // 句柄图像。
var handleImg = handle.GetComponent<Image>();
        if (sliderKnobSprite != null)
        {
            handleImg.sprite = sliderKnobSprite;
            handleImg.type = Image.Type.Simple;
            handleImg.preserveAspect = true;
            handleImg.color = Color.white;
        }

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    /// <summary>CreateToggle 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Toggle&gt;()、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Toggle&gt;()、OptionsTabManager.CreatePanel(string, Transform, Color)、OptionsTabManager.SetAnchors(GameObject, Vector2, Vector2, Vector2, Vector2) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    Toggle CreateToggle(string name, Transform parent)
    {
        // 当前游戏对象。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);
        // 切换。
Toggle toggle = go.GetComponent<Toggle>();
        // 背景。
GameObject background = CreatePanel("边框", go.transform, new Color(1f, 1f, 1f, 0.24f));
        Stretch(background.GetComponent<RectTransform>());
        // 背景图像。
var bgImg = background.GetComponent<Image>();
        if (tickBoxOffSprite != null)
        {
            bgImg.sprite = tickBoxOffSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
            bgImg.preserveAspect = true;
        }

        // 勾选标记。
GameObject checkmark = CreatePanel("选中标记", background.transform, Color.white);
        SetAnchors(checkmark, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        // 检查图像。
var checkImg = checkmark.GetComponent<Image>();
        if (tickBoxOnSprite != null)
        {
            checkImg.sprite = tickBoxOnSprite;
            checkImg.type = Image.Type.Simple;
            checkImg.preserveAspect = true;
            checkImg.color = Color.white;
        }
        else
        {
            checkImg.color = accentColor;
        }

        toggle.targetGraphic = bgImg;
        toggle.graphic = checkImg;
        return toggle;
    }

    /// <summary>
    /// 使用 DefaultControls.CreateDropdown 创建标准下拉框结构，
    /// 然后替换视觉素材。确保 Template/Viewport/Content/Item 结构正确。
    /// </summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildDisplayPage(Transform)；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;ContentSizeFitter&gt;()、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;Mask&gt;()、Component.GetComponent&lt;RectTransform&gt;()、Component.GetComponent&lt;Scrollbar&gt;()、Component.GetComponent&lt;ScrollRect&gt;() 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    /// <param name="parent">父级。</param>
    Dropdown CreateStandardDropdown(string name, Transform parent)
    {
        // 使用引擎默认下拉框结构
        // 默认资源集合。
var defaultResources = new DefaultControls.Resources();
        // 留空 sprite 让默认结构先创建，之后替换
        if (buttonSprite != null)
        {
            // 留空 sprite 让默认结构先创建，之后替换
            defaultResources.standard = buttonSprite;
        }
        else
        {
            // 留空 sprite 让默认结构先创建，之后替换
            defaultResources.standard = null;
        }

        if (panelSprite != null)
        {
            defaultResources.background = panelSprite;
        }
        else
        {
            defaultResources.background = defaultResources.standard;
        }

        defaultResources.inputField = defaultResources.standard;
        defaultResources.knob = defaultResources.standard;
        if (tickBoxOnSprite != null)
        {
            defaultResources.checkmark = tickBoxOnSprite;
        }
        else
        {
            defaultResources.checkmark = defaultResources.standard;
        }

        defaultResources.dropdown = defaultResources.standard;
        defaultResources.mask = null;
        // 当前游戏对象。
GameObject go = DefaultControls.CreateDropdown(defaultResources);
        go.name = name;
        go.transform.SetParent(parent, false);
        // 下拉框。
Dropdown dropdown = go.GetComponent<Dropdown>();
        // 下拉框矩形变换。
RectTransform dropdownRt = go.GetComponent<RectTransform>();
        Stretch(dropdownRt);
        // 设置主框 Image
        // 主要图像。
var mainImg = go.GetComponent<Image>();
        mainImg.sprite = buttonSprite;
        // 赋值前保存的目标主要图像。
global::UnityEngine.UI.Image assignmentReceiver56 = mainImg;
        if (buttonSprite != null)
        {
            assignmentReceiver56.type = Image.Type.Sliced;
        }
        else
        {
            assignmentReceiver56.type = Image.Type.Simple;
        }

        mainImg.color = new Color(1f, 1f, 1f, 0.15f);
        // 设置 Caption Text
        // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue58 = dropdownRt.Find("Label");
        // 说明文字文字。
global::UnityEngine.UI.Text captionText;
        if (!global::System.Object.ReferenceEquals(receiverValue58, null))
        {
            captionText = receiverValue58.GetComponent<Text>();
        }
        else
        {
            captionText = default;
        }

        if (captionText != null)
        {
            captionText.font = interfaceFont;
            captionText.fontSize = 18;
            captionText.color = Color.white;
            captionText.alignment = TextAnchor.MiddleLeft;
            captionText.raycastTarget = false;
        }

        dropdown.captionText = captionText;
        // 设置箭头
        // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue60 = dropdownRt.Find("Arrow");
        // 箭头。
global::UnityEngine.UI.Image arrow;
        if (!global::System.Object.ReferenceEquals(receiverValue60, null))
        {
            arrow = receiverValue60.GetComponent<Image>();
        }
        else
        {
            arrow = default;
        }

        if (arrow != null)
        {
            arrow.color = new Color(1f, 1f, 1f, 0.6f);
        }

        // 设置 Template
        // 模板。
var template = dropdownRt.Find("Template") as RectTransform;
        if (template != null)
        {
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, 5f);
            template.sizeDelta = new Vector2(0f, 220f);
            // Template 背景
            // 模板图像。
var templateImg = template.GetComponent<Image>();
            templateImg.color = new Color(0.06f, 0.06f, 0.06f, 0.98f);
            templateImg.raycastTarget = true;
            // ScrollRect
            // 滚动。
var scroll = template.GetComponent<ScrollRect>();
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.horizontal = false;
            scroll.vertical = true;
            // Viewport
            // 视口。
var viewport = template.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                // 视口图像。
var vpImg = viewport.GetComponent<Image>();
                // 遮罩必须写入模板缓冲；隐藏遮罩图形由遮罩组件负责。
                vpImg.color = Color.white;
                vpImg.raycastTarget = true;
                // 遮罩。
var mask = viewport.GetComponent<Mask>();
                if (mask != null)
                {
                    mask.showMaskGraphic = false;
                }

                // Content
                // 内容。
var content = viewport.Find("Content") as RectTransform;
                if (content != null)
                {
                    // 内容布局。
var contentLayout = content.GetComponent<VerticalLayoutGroup>();
                    if (contentLayout != null)
                    {
                        contentLayout.spacing = 2f;
                        contentLayout.childControlHeight = true;
                        contentLayout.childForceExpandWidth = true;
                        contentLayout.childForceExpandHeight = false;
                    }

                    // 内容尺寸适配器。
var contentFitter = content.GetComponent<ContentSizeFitter>();
                    if (contentFitter != null)
                    {
                        contentFitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
                    }

                    // Item
                    // 当前变换节点。
var item = content.Find("Item");
                    if (item != null)
                    {
                        // 物品矩形变换。
var itemRt = item.GetComponent<RectTransform>();
                        itemRt.sizeDelta = new Vector2(0f, 36f);
                        // 物品切换。
var itemToggle = item.GetComponent<Toggle>();
                        if (itemToggle != null)
                        {
                            // Item Background
                            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue62 = item.transform.Find("Item Background");
                            // 物品背景。
global::UnityEngine.UI.Image itemBg;
                            if (!global::System.Object.ReferenceEquals(receiverValue62, null))
                            {
                                itemBg = receiverValue62.GetComponent<Image>();
                            }
                            else
                            {
                                itemBg = default;
                            }

                            if (itemBg != null)
                            {
                                itemBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
                            }

                            // Item Checkmark
                            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue64 = item.transform.Find("Item Checkmark");
                            // 勾选标记。
global::UnityEngine.UI.Image checkmark;
                            if (!global::System.Object.ReferenceEquals(receiverValue64, null))
                            {
                                checkmark = receiverValue64.GetComponent<Image>();
                            }
                            else
                            {
                                checkmark = default;
                            }

                            if (checkmark != null)
                            {
                                if (tickBoxOnSprite != null)
                                {
                                    checkmark.sprite = tickBoxOnSprite;
                                    checkmark.type = Image.Type.Simple;
                                    checkmark.preserveAspect = true;
                                }

                                checkmark.color = new Color(0.72f, 0.72f, 0.68f, 0.8f);
                            }

                            // Item Label
                            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue66 = item.transform.Find("Item Label");
                            // 物品标签。
global::UnityEngine.UI.Text itemLabel;
                            if (!global::System.Object.ReferenceEquals(receiverValue66, null))
                            {
                                itemLabel = receiverValue66.GetComponent<Text>();
                            }
                            else
                            {
                                itemLabel = default;
                            }

                            if (itemLabel != null)
                            {
                                itemLabel.font = interfaceFont;
                                itemLabel.fontSize = 18;
                                itemLabel.color = Color.white;
                                itemLabel.alignment = TextAnchor.MiddleLeft;
                                itemLabel.raycastTarget = false;
                            }

                            dropdown.itemText = itemLabel;
                        }
                    }
                }
            }

            // Scrollbar
            // 滚动条。
var scrollbar = template.Find("Scrollbar") as RectTransform;
            if (scrollbar != null)
            {
                // 文本构建器图像。
var sbImg = scrollbar.GetComponent<Image>();
                sbImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                // 文本构建器。
var sb = scrollbar.GetComponent<Scrollbar>();
                if (sb != null)
                {
                    // 滑移中区域。
var slidingArea = scrollbar.Find("Sliding Area") as RectTransform;
                    if (slidingArea != null)
                    {
                        // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue68 = slidingArea.Find("Handle");
                        // 句柄。
global::UnityEngine.UI.Image handle;
                        if (!global::System.Object.ReferenceEquals(receiverValue68, null))
                        {
                            handle = receiverValue68.GetComponent<Image>();
                        }
                        else
                        {
                            handle = default;
                        }

                        if (handle != null)
                        {
                            handle.color = new Color(0.72f, 0.72f, 0.68f, 0.8f);
                        }
                    }
                }

                scroll.verticalScrollbar = sb;
            }

            dropdown.template = template;
            template.gameObject.SetActive(false);
        }

        return dropdown;
    }

    /// <summary>SetAnchors 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;RectTransform&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildBindingRows(Transform)、OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildDisplayPage(Transform)、OptionsTabManager.BuildInterface()、OptionsTabManager.BuildVolumePage(Transform) 等共 10 处；下游（含内部回调体） GameObject.GetComponent&lt;RectTransform&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="go">当前游戏对象。</param>
    /// <param name="min">最小。</param>
    /// <param name="max">最大。</param>
    /// <param name="offsetMin">偏移量最小。</param>
    /// <param name="offsetMax">偏移量最大。</param>
    void SetAnchors(GameObject go, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        // 矩形。
RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.BuildControlsPage(Transform)、OptionsTabManager.BuildInterface()、OptionsTabManager.CreateButton(string, Transform, string, int)、OptionsTabManager.CreatePage(string, Transform)、OptionsTabManager.CreateSlider(string, Transform) 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">矩形。</param>
    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
