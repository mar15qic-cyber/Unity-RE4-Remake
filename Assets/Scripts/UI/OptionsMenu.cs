using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 选项菜单：音量/画面/操作说明
/// 可被标题菜单和暂停菜单复用
/// </summary>
public class OptionsMenu : MonoBehaviour
{
    /// <summary>中文含义：界面视觉主题配置。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()、OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>中文含义：面板。使用位置：OptionsMenu.EnsureEnhancedMenu()、OptionsMenu.FinishHide()、OptionsMenu.HideImmediate() 等共 7 处。</summary>
    [Header("面板引用")]
    public GameObject panel;
    /// <summary>中文含义：画布组。使用位置：OptionsMenu.EnsureEnhancedMenu()、OptionsMenu.FadeIn()、OptionsMenu.FadeOut() 等共 5 处。</summary>
    public CanvasGroup canvasGroup;
    /// <summary>中文含义：背景音乐滑块。使用位置：OptionsMenu.Initialize()、OptionsMenu.LoadVolumeSettings()、OptionsMenu.OnSfxChanged(float)。</summary>
    [Header("音量")]
    public Slider bgmSlider;
    /// <summary>中文含义：音效滑块。使用位置：OptionsMenu.Initialize()、OptionsMenu.LoadVolumeSettings()、OptionsMenu.OnBgmChanged(float)。</summary>
    public Slider sfxSlider;
    /// <summary>中文含义：背景音乐值文字。使用位置：OptionsMenu.UpdateVolumeTexts(float, float)。</summary>
    public Text bgmValueText;
    /// <summary>中文含义：音效值文字。使用位置：OptionsMenu.UpdateVolumeTexts(float, float)。</summary>
    public Text sfxValueText;
    /// <summary>中文含义：全屏切换。使用位置：OptionsMenu.Initialize()、OptionsMenu.LoadDisplaySettings()。</summary>
    [Header("画面")]
    public Toggle fullscreenToggle;
    /// <summary>中文含义：分辨率下拉框。使用位置：OptionsMenu.Initialize()、OptionsMenu.LoadDisplaySettings()。</summary>
    public Dropdown resolutionDropdown;
    /// <summary>中文含义：分辨率标签。</summary>
    public Text resolutionLabel;
    /// <summary>中文含义：控件集合文字。使用位置：OptionsMenu.LoadControlsText()。</summary>
    [Header("操作说明")]
    public Text controlsText;
    /// <summary>中文含义：后方按钮。使用位置：OptionsMenu.Initialize()。</summary>
    [Header("按钮")]
    public Button backButton;
    /// <summary>中文含义：留空时会在本对象上自动补齐。优先复用完整的音量、画面和操控设置页面；留空时会在本对象上自动补齐。使用位置：OptionsMenu.EnsureEnhancedMenu()、OptionsMenu.Hide()、OptionsMenu.Show() 等共 4 处。</summary>
    [Header("三分页设置页面")]
    [Tooltip("优先复用完整的音量、画面和操控设置页面；留空时会在本对象上自动补齐。")]
    public OptionsTabManager enhancedMenu;
    /// <summary>中文含义：是否使用增强的菜单。使用位置：OptionsMenu.Show()。</summary>
    public bool useEnhancedMenu = true;
    /// <summary>中文含义：设置页签、返回、恢复默认、瞄准模式及按键绑定按钮。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Header("统一视觉素材")]
    [Tooltip("设置页签、返回、恢复默认、瞄准模式及按键绑定按钮")]
    public Sprite buttonSprite;
    /// <summary>中文含义：音量和镜头灵敏度滑块轨道。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("音量和镜头灵敏度滑块轨道")]
    public Sprite sliderTrackSprite;
    /// <summary>中文含义：音量和镜头灵敏度滑块方块。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("音量和镜头灵敏度滑块方块")]
    public Sprite sliderKnobSprite;
    /// <summary>中文含义：声音开启状态的图标。音量大于零时显示。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("音量大于零时显示")]
    public Sprite soundOnIcon;
    /// <summary>中文含义：声音关闭状态的图标。音量等于零时显示。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("音量等于零时显示")]
    public Sprite soundOffIcon;
    /// <summary>中文含义：勾选框启用状态的图片。勾选项启用时显示。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("勾选项启用时显示")]
    public Sprite tickBoxOnSprite;
    /// <summary>中文含义：勾选框关闭状态的图片。勾选项关闭时显示。使用位置：OptionsMenu.EnsureEnhancedMenu()。</summary>
    [Tooltip("勾选项关闭时显示")]
    public Sprite tickBoxOffSprite;
    /// <summary>中文含义：已关闭时的通知回调。关闭选项菜单后的回调</summary>
    [HideInInspector]
    public System.Action onClosed;
    /// <summary>中文含义：可用分辨率集合。使用位置：OptionsMenu.LoadDisplaySettings()、OptionsMenu.OnResolutionChanged(int)。</summary>
    private Resolution[] availableResolutions;
    /// <summary>中文含义：界面淡入淡出的持续时间。使用位置：OptionsMenu.FadeIn()、OptionsMenu.FadeOut()。</summary>
    private float fadeDuration = 0.25f;
    /// <summary>中文含义：是否已初始化。使用位置：OptionsMenu.Initialize()。</summary>
    private bool initialized;
    /// <summary>中文含义：是否可见。使用位置：OptionsMenu.Hide()、OptionsMenu.OnEnhancedMenuClosed()、OptionsMenu.Show() 等共 4 处。</summary>
    private bool isVisible;
    /// <summary>中文含义：是否隐藏中。使用位置：OptionsMenu.FinishHide()、OptionsMenu.Hide()、OptionsMenu.OnEnhancedMenuClosed() 等共 4 处。</summary>
    private bool hiding;
    /// <summary>中文含义：音量服务。使用位置：OptionsMenu.LoadVolumeSettings()、OptionsMenu.OnBgmChanged(float)、OptionsMenu.OnSfxChanged(float) 等共 6 处。</summary>
    private IAudioVolumeService volumeService;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsMenu.Initialize()、OptionsMenu.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        TryBindAudioService();
        Initialize();
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） OptionsMenu.HideImmediate()、OptionsMenu.Initialize()、OptionsMenu.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        TryBindAudioService();
        Initialize();
        if (!isVisible)
        {
            HideImmediate();
        }
    }

    /// <summary>Initialize 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、OptionsMenu.LoadControlsText()、OptionsMenu.LoadDisplaySettings()、OptionsMenu.LoadVolumeSettings()、UnityEvent.AddListener(UnityAction)、UnityEvent&lt;bool&gt;.AddListener(UnityAction&lt;bool&gt;) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Awake()、OptionsMenu.Show()、OptionsMenu.Start()；下游（含内部回调体） GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、OptionsMenu.LoadControlsText()、OptionsMenu.LoadDisplaySettings()、OptionsMenu.LoadVolumeSettings()、UnityEvent.AddListener(UnityAction)、UnityEvent&lt;bool&gt;.AddListener(UnityAction&lt;bool&gt;) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        if (panel == null)
        {
            panel = gameObject;
        }

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && panel != null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }

        // 初始化音量滑块
        LoadVolumeSettings();
        // 初始化画面设置
        LoadDisplaySettings();
        // 初始化操作说明
        LoadControlsText();
        // 绑定事件
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        }

        if (resolutionDropdown != null)
        {
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、OptionsMenu.Hide()、OptionsMenu.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (volumeService == null)
        {
            TryBindAudioService();
        }

        if (panel == null || !panel.activeSelf)
        {
            return;
        }

        if (enhancedMenu != null && enhancedMenu.enabled)
        {
            return;
        }

        // 按 ESC 或 Backspace 返回
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            Hide();
        }
    }

    /// <summary>TryBindAudioService 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Awake()、OptionsMenu.LoadVolumeSettings()、OptionsMenu.OnBgmChanged(float)、OptionsMenu.OnSfxChanged(float)、OptionsMenu.Start() 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryBindAudioService()
    {
        if (AudioManager.HasInstance)
        {
            volumeService = AudioManager.Instance as IAudioVolumeService;
        }
        else
        {
            volumeService = null;
        }
    }

    /// <summary>显示选项菜单</summary>
    /// <remarks>静态调用链：上游 GameUIManager.OpenOptionsFromPause()、TitleScreenManager.OpenOptions()；下游（含内部回调体） GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopAllCoroutines()、OptionsMenu.EnsureEnhancedMenu()、OptionsMenu.FadeIn()、OptionsMenu.Initialize() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        bool wasVisible = isVisible && !hiding;
        Initialize();
        if (panel == null)
        {
            return;
        }

        isVisible = true;
        hiding = false;

        panel.transform.SetAsLastSibling();
        // panelRect（面板矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：OptionsMenu.Show()。
RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.localScale = Vector3.one;
        }

        if (useEnhancedMenu && EnsureEnhancedMenu())
        {
            enhancedMenu.Show();
            return;
        }

        panel.SetActive(true);
        if (!wasVisible)
        {
            AudioManager.PlayInterface(AudioEventNames.UISettingsOpen);
        }
        LoadVolumeSettings();
        LoadDisplaySettings();
        StopAllCoroutines();
        StartCoroutine(FadeIn());
    }

    /// <summary>隐藏选项菜单</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()、GameUIManager.HideOptions()、OptionsMenu.Update()、TitleScreenManager.Update()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopAllCoroutines()、OptionsMenu.FadeOut()、OptionsMenu.FinishHide()、OptionsMenu.SaveAllSettings()、OptionsTabManager.Hide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        if (hiding || !isVisible)
        {
            return;
        }

        if (enhancedMenu != null && enhancedMenu.gameObject.activeSelf)
        {
            enhancedMenu.Hide();
            return;
        }

        isVisible = false;
        hiding = true;
        AudioManager.PlayInterface(AudioEventNames.UISettingsCancel);
        // 保存设置
        SaveAllSettings();
        if (!isActiveAndEnabled)
        {
            FinishHide();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    /// <summary>FadeIn 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Show()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeIn()
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        // elapsed（已用）：使用位置：OptionsMenu.FadeIn()。
float elapsed = 0f;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、OptionsMenu.FadeIn()。
float t = elapsed / fadeDuration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    /// <summary>FadeOut 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、OptionsMenu.FinishHide()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Hide()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、OptionsMenu.FinishHide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOut()
    {
        if (canvasGroup == null)
        {
            FinishHide();
            yield break;
        }

        // elapsed（已用）：使用位置：OptionsMenu.FadeOut()。
float elapsed = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、OptionsMenu.FadeOut()。
float t = elapsed / fadeDuration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        FinishHide();
    }

    /// <summary>FinishHide 的业务处理入口；本方法直接依赖 Action.Invoke()、GameObject.SetActive(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.FadeOut()、OptionsMenu.Hide()；下游（含内部回调体） Action.Invoke()、GameObject.SetActive(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void FinishHide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        hiding = false;
        // receiverValue2（判空前缓存的已关闭时的通知回调）：从已关闭时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、OptionsMenu.FinishHide()。
global::System.Action receiverValue2 = onClosed;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke();
        }
    }

    /// <summary>EnsureEnhancedMenu 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;OptionsTabManager&gt;()、Component.GetComponentInParent&lt;UIThemeBinder&gt;()、GameObject.AddComponent&lt;OptionsTabManager&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Show()；下游（含内部回调体） Component.GetComponent&lt;OptionsTabManager&gt;()、Component.GetComponentInParent&lt;UIThemeBinder&gt;()、GameObject.AddComponent&lt;OptionsTabManager&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool EnsureEnhancedMenu()
    {
        if (enhancedMenu == null)
        {
            enhancedMenu = GetComponent<OptionsTabManager>();
        }

        if (enhancedMenu == null)
        {
            enhancedMenu = gameObject.AddComponent<OptionsTabManager>();
        }

        if (enhancedMenu == null)
        {
            return false;
        }

        enhancedMenu.visualTheme = visualTheme;
        // assignmentReceiver3（赋值前保存的目标留空时会在本对象上自动补齐）：从增强的菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：OptionsMenu.EnsureEnhancedMenu()。
global::OptionsTabManager assignmentReceiver3 = enhancedMenu;
        if (panel != null)
        {
            assignmentReceiver3.panelRoot = panel;
        }
        else
        {
            assignmentReceiver3.panelRoot = gameObject;
        }

        enhancedMenu.canvasGroup = canvasGroup;
        if (buttonSprite != null)
        {
            enhancedMenu.buttonSprite = buttonSprite;
        }

        if (sliderTrackSprite != null)
        {
            enhancedMenu.sliderTrackSprite = sliderTrackSprite;
        }

        if (sliderKnobSprite != null)
        {
            enhancedMenu.sliderKnobSprite = sliderKnobSprite;
        }

        if (soundOnIcon != null)
        {
            enhancedMenu.soundOnIcon = soundOnIcon;
        }

        if (soundOffIcon != null)
        {
            enhancedMenu.soundOffIcon = soundOffIcon;
        }

        if (tickBoxOnSprite != null)
        {
            enhancedMenu.tickBoxOnSprite = tickBoxOnSprite;
        }

        if (tickBoxOffSprite != null)
        {
            enhancedMenu.tickBoxOffSprite = tickBoxOffSprite;
        }

        // themeBinder（主题绑定器）：保存 Component.GetComponentInParent<UIThemeBinder>() 的返回结果，供当前步骤使用。使用位置：OptionsMenu.EnsureEnhancedMenu()。
UIThemeBinder themeBinder = GetComponentInParent<UIThemeBinder>();
        if (themeBinder != null && themeBinder.theme != null)
        {
            // theme（主题）：使用位置：OptionsMenu.EnsureEnhancedMenu()。
UIVisualTheme theme = themeBinder.theme;
            if (enhancedMenu.panelSprite == null)
            {
                enhancedMenu.panelSprite = theme.panelBackground;
            }

            if (enhancedMenu.rowSprite == null)
            {
                enhancedMenu.rowSprite = theme.buttonNormal;
            }

            if (enhancedMenu.buttonSprite == null && buttonSprite == null)
            {
                enhancedMenu.buttonSprite = theme.buttonHighlighted;
            }
        }

        enhancedMenu.onClosed = OnEnhancedMenuClosed;
        return true;
    }

    /// <summary>OnEnhancedMenuClosed 的业务处理入口；本方法直接依赖 Action.Invoke()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnhancedMenuClosed()
    {
        isVisible = false;
        hiding = false;
        // receiverValue5（判空前缓存的已关闭时的通知回调）：从已关闭时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、OptionsMenu.OnEnhancedMenuClosed()。
global::System.Action receiverValue5 = onClosed;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Invoke();
        }
    }

    /// <summary>HideImmediate 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Start()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

#region ===== 音量设置 =====
    /// <summary>LoadVolumeSettings 的业务处理入口；本方法直接依赖 IAudioVolumeService.GetCategoryVolume(AudioCategory)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)、Slider.SetValueWithoutNotify(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Initialize()、OptionsMenu.Show()；下游（含内部回调体） IAudioVolumeService.GetCategoryVolume(AudioCategory)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)、Slider.SetValueWithoutNotify(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadVolumeSettings()
    {
        TryBindAudioService();
        // bgm（背景音乐）：使用位置：作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数、作为 Slider.SetValueWithoutNotify(float) 的参数、OptionsMenu.LoadVolumeSettings()。
float bgm;
        if (volumeService != null)
        {
            bgm = volumeService.GetCategoryVolume(AudioCategory.BGM);
        }
        else
        {
            bgm = 0.8f;
        }

        // sfx（音效）：使用位置：作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数、作为 Slider.SetValueWithoutNotify(float) 的参数、OptionsMenu.LoadVolumeSettings()。
float sfx;
        if (volumeService != null)
        {
            sfx = volumeService.GetCategoryVolume(AudioCategory.SFX);
        }
        else
        {
            sfx = 1f;
        }

        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(bgm);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfx);
        }

        UpdateVolumeTexts(bgm, sfx);
    }

    /// <summary>OnBgmChanged 的业务处理入口；本方法直接依赖 IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、object.ReferenceEquals(object, object)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、object.ReferenceEquals(object, object)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：作为 IAudioVolumeService.SetCategoryVolume(AudioCategory, float) 的参数、OptionsMenu.OnBgmChanged(float)。</param>
    void OnBgmChanged(float value)
    {
        TryBindAudioService();
        // receiverValue8（判空前缓存的音量服务）：从音量服务读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、OptionsMenu.OnBgmChanged(float)。
global::IAudioVolumeService receiverValue8 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.SetCategoryVolume(AudioCategory.BGM, value);
        }

        // argumentValue10（按原顺序缓存的实参值）：从值读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数。
float argumentValue10 = value;
        // selectedValue9（分支选出的音效滑块的值）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数、OptionsMenu.OnBgmChanged(float)。
float selectedValue9;
        if (sfxSlider != null)
        {
            selectedValue9 = sfxSlider.value;
        }
        else
        {
            selectedValue9 = 1f;
        }

        UpdateVolumeTexts(argumentValue10, selectedValue9);
    }

    /// <summary>OnSfxChanged 的业务处理入口；本方法直接依赖 IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、object.ReferenceEquals(object, object)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IAudioVolumeService.SetCategoryVolume(AudioCategory, float)、object.ReferenceEquals(object, object)、OptionsMenu.TryBindAudioService()、OptionsMenu.UpdateVolumeTexts(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：作为 IAudioVolumeService.SetCategoryVolume(AudioCategory, float) 的参数、作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数。</param>
    void OnSfxChanged(float value)
    {
        TryBindAudioService();
        // receiverValue11（判空前缓存的音量服务）：从音量服务读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、OptionsMenu.OnSfxChanged(float)。
global::IAudioVolumeService receiverValue11 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.SetCategoryVolume(AudioCategory.SFX, value);
        }

        // selectedValue12（分支选出的背景音乐滑块的值）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 OptionsMenu.UpdateVolumeTexts(float, float) 的参数、OptionsMenu.OnSfxChanged(float)。
float selectedValue12;
        if (bgmSlider != null)
        {
            selectedValue12 = bgmSlider.value;
        }
        else
        {
            selectedValue12 = 0.8f;
        }

        UpdateVolumeTexts(selectedValue12, value);
    }

    /// <summary>UpdateVolumeTexts 的业务处理入口；本方法直接依赖 Mathf.RoundToInt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.LoadVolumeSettings()、OptionsMenu.OnBgmChanged(float)、OptionsMenu.OnSfxChanged(float)；下游（含内部回调体） Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bgm">中文含义：背景音乐。使用位置：作为 Mathf.RoundToInt(float) 的参数。</param>
    /// <param name="sfx">中文含义：音效。使用位置：作为 Mathf.RoundToInt(float) 的参数。</param>
    void UpdateVolumeTexts(float bgm, float sfx)
    {
        if (bgmValueText != null)
        {
            bgmValueText.text = Mathf.RoundToInt(bgm * 100) + "";
        }

        if (sfxValueText != null)
        {
            sfxValueText.text = Mathf.RoundToInt(sfx * 100) + "";
        }
    }

#endregion
#region ===== 画面设置 =====
    /// <summary>LoadDisplaySettings 的业务处理入口；本方法直接依赖 Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.SetValueWithoutNotify(int)、GameSettingsData.ReadInteger(string, int)、List&lt;Resolution&gt;.List(IEnumerable&lt;Resolution&gt;)、List&lt;Resolution&gt;.ToArray()、List&lt;string&gt;.Add(string) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Initialize()、OptionsMenu.Show()；下游（含内部回调体） Dropdown.AddOptions(List&lt;string&gt;)、Dropdown.ClearOptions()、Dropdown.SetValueWithoutNotify(int)、GameSettingsData.ReadInteger(string, int)、List&lt;Resolution&gt;.List(IEnumerable&lt;Resolution&gt;)、List&lt;Resolution&gt;.ToArray()、List&lt;string&gt;.Add(string) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadDisplaySettings()
    {
        // 全屏
        // isFull（是否完整）：使用位置：作为 SettingsApplicationAdapter.ApplyFullScreen(bool) 的参数、作为 Toggle.SetIsOnWithoutNotify(bool) 的参数。
bool isFull = GameSettingsData.ReadInteger("Fullscreen", 1) == 1;
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(isFull);
        }

        SettingsApplicationAdapter.ApplyFullScreen(isFull);
        // 分辨率
        availableResolutions = new List<Resolution>(Screen.resolutions).ToArray();
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();
            // options（选项集合）：使用位置：作为 Dropdown.AddOptions(List<string>) 的参数、OptionsMenu.LoadDisplaySettings()。
List<string> options = new List<string>();
            // currentIndex（当前索引）：使用位置：作为 Dropdown.SetValueWithoutNotify(int) 的参数、OptionsMenu.LoadDisplaySettings()。
int currentIndex = 0;
            // i（当前遍历索引）：使用位置：OptionsMenu.LoadDisplaySettings()。
for (int i = 0; i < availableResolutions.Length; i++)
            {
                // r（当前分辨率）：使用位置：作为 Mathf.RoundToInt(float) 的参数、OptionsMenu.LoadDisplaySettings()。
var r = availableResolutions[i];
                // leftOperand14（计算前缓存的左侧文本选择）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：OptionsMenu.LoadDisplaySettings()。
string leftOperand14 = r.width + " x " + r.height;
                // selectedValue13（分支选出的轮次目标整数的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：OptionsMenu.LoadDisplaySettings()。
string selectedValue13;
                if (r.refreshRateRatio.value > 0)
                {
                    selectedValue13 = " (" + Mathf.RoundToInt((float)r.refreshRateRatio.value) + "Hz)";
                }
                else
                {
                    selectedValue13 = "";
                }

                // label（标签）：使用位置：作为 List<string>.Add(string) 的参数。
string label = leftOperand14 + (selectedValue13);
                options.Add(label);
                if (r.width == Screen.currentResolution.width && r.height == Screen.currentResolution.height)
                {
                    currentIndex = i;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.SetValueWithoutNotify(currentIndex);
        }
    }

    /// <summary>OnFullscreenToggled 的业务处理入口；本方法直接依赖 SettingsApplicationAdapter.ModifyFullScreen(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsApplicationAdapter.ModifyFullScreen(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="isFull">中文含义：是否完整。使用位置：作为 SettingsApplicationAdapter.ModifyFullScreen(bool) 的参数。</param>
    void OnFullscreenToggled(bool isFull)
    {
        SettingsApplicationAdapter.ModifyFullScreen(isFull);
    }

    /// <summary>OnResolutionChanged 的业务处理入口；本方法直接依赖 SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：OptionsMenu.OnResolutionChanged(int)。</param>
    void OnResolutionChanged(int index)
    {
        if (availableResolutions == null || index >= availableResolutions.Length)
        {
            return;
        }

        // res（资源）：使用位置：作为 SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool) 的参数。
var res = availableResolutions[index];
        SettingsApplicationAdapter.ModifyResolution(res.width, res.height, Screen.fullScreen, true);
    }

#endregion
#region ===== 操作说明 =====
    /// <summary>LoadControlsText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Initialize()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadControlsText()
    {
        if (controlsText == null)
        {
            return;
        }

        controlsText.text = "WASD —— 移动\n" + "鼠标右键(按住) —— 瞄准\n" + "鼠标左键 —— 射击\n" + "LeftShift(按住) —— 跑步\n" + "R —— 换弹\n" + "F —— 近战攻击\n" + "ESC —— 暂停";
    }

#endregion
#region ===== 持久化 =====
    /// <summary>SaveAllSettings 的业务处理入口；本方法直接依赖 GameSettingsData.SaveValue()、IAudioVolumeService.SaveVolumeSettings()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsMenu.Hide()；下游（含内部回调体） GameSettingsData.SaveValue()、IAudioVolumeService.SaveVolumeSettings()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void SaveAllSettings()
    {
        // receiverValue15（判空前缓存的音量服务）：从音量服务读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、OptionsMenu.SaveAllSettings()。
global::IAudioVolumeService receiverValue15 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.SaveVolumeSettings();
        }

        GameSettingsData.SaveValue();
    }
#endregion
}
