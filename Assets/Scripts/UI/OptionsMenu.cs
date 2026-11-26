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
    /// <summary>界面视觉主题配置。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>面板。</summary>
    [Header("面板引用")]
    public GameObject panel;
    /// <summary>画布组。</summary>
    public CanvasGroup canvasGroup;
    /// <summary>背景音乐滑块。</summary>
    [Header("音量")]
    public Slider bgmSlider;
    /// <summary>音效滑块。</summary>
    public Slider sfxSlider;
    /// <summary>背景音乐值文字。</summary>
    public Text bgmValueText;
    /// <summary>音效值文字。</summary>
    public Text sfxValueText;
    /// <summary>全屏切换。</summary>
    [Header("画面")]
    public Toggle fullscreenToggle;
    /// <summary>分辨率下拉框。</summary>
    public Dropdown resolutionDropdown;
    /// <summary>中文含义：分辨率标签。</summary>
    public Text resolutionLabel;
    /// <summary>控件集合文字。</summary>
    [Header("操作说明")]
    public Text controlsText;
    /// <summary>后方按钮。</summary>
    [Header("按钮")]
    public Button backButton;
    /// <summary>留空时会在本对象上自动补齐。优先复用完整的音量、画面和操控设置页面；留空时会在本对象上自动补齐。</summary>
    [Header("三分页设置页面")]
    [Tooltip("优先复用完整的音量、画面和操控设置页面；留空时会在本对象上自动补齐。")]
    public OptionsTabManager enhancedMenu;
    /// <summary>是否使用增强的菜单。</summary>
    public bool useEnhancedMenu = true;
    /// <summary>设置页签、返回、恢复默认、瞄准模式及按键绑定按钮。</summary>
    [Header("统一视觉素材")]
    [Tooltip("设置页签、返回、恢复默认、瞄准模式及按键绑定按钮")]
    public Sprite buttonSprite;
    /// <summary>音量和镜头灵敏度滑块轨道。</summary>
    [Tooltip("音量和镜头灵敏度滑块轨道")]
    public Sprite sliderTrackSprite;
    /// <summary>音量和镜头灵敏度滑块方块。</summary>
    [Tooltip("音量和镜头灵敏度滑块方块")]
    public Sprite sliderKnobSprite;
    /// <summary>声音开启状态的图标。音量大于零时显示。</summary>
    [Tooltip("音量大于零时显示")]
    public Sprite soundOnIcon;
    /// <summary>声音关闭状态的图标。音量等于零时显示。</summary>
    [Tooltip("音量等于零时显示")]
    public Sprite soundOffIcon;
    /// <summary>勾选框启用状态的图片。勾选项启用时显示。</summary>
    [Tooltip("勾选项启用时显示")]
    public Sprite tickBoxOnSprite;
    /// <summary>勾选框关闭状态的图片。勾选项关闭时显示。</summary>
    [Tooltip("勾选项关闭时显示")]
    public Sprite tickBoxOffSprite;
    /// <summary>中文含义：已关闭时的通知回调。关闭选项菜单后的回调</summary>
    [HideInInspector]
    public System.Action onClosed;
    /// <summary>可用分辨率集合。</summary>
    private Resolution[] availableResolutions;
    /// <summary>界面淡入淡出的持续时间。</summary>
    private float fadeDuration = 0.25f;
    /// <summary>是否已初始化。</summary>
    private bool initialized;
    /// <summary>是否可见。</summary>
    private bool isVisible;
    /// <summary>是否隐藏中。</summary>
    private bool hiding;
    /// <summary>音量服务。</summary>
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
        // 面板矩形。
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

        // 已用。
float elapsed = 0f;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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

        // 已用。
float elapsed = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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
        // 判空前缓存的已关闭时的通知回调。
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
        // 赋值前保存的目标留空时会在本对象上自动补齐。
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

        // 主题绑定器。
UIThemeBinder themeBinder = GetComponentInParent<UIThemeBinder>();
        if (themeBinder != null && themeBinder.theme != null)
        {
            // 主题。
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
        // 判空前缓存的已关闭时的通知回调。
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
        // 背景音乐。
float bgm;
        if (volumeService != null)
        {
            bgm = volumeService.GetCategoryVolume(AudioCategory.BGM);
        }
        else
        {
            bgm = 0.8f;
        }

        // 音效。
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
    /// <param name="value">值。</param>
    void OnBgmChanged(float value)
    {
        TryBindAudioService();
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue8 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.SetCategoryVolume(AudioCategory.BGM, value);
        }

        // 按原顺序缓存的实参值。
float argumentValue10 = value;
        // 分支选出的音效滑块的值。
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
    /// <param name="value">值。</param>
    void OnSfxChanged(float value)
    {
        TryBindAudioService();
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue11 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.SetCategoryVolume(AudioCategory.SFX, value);
        }

        // 分支选出的背景音乐滑块的值。
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
    /// <param name="bgm">背景音乐。</param>
    /// <param name="sfx">音效。</param>
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
        // 是否完整。
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
            // 选项集合。
List<string> options = new List<string>();
            // 当前索引。
int currentIndex = 0;
            // 当前遍历索引。
for (int i = 0; i < availableResolutions.Length; i++)
            {
                // 当前分辨率。
var r = availableResolutions[i];
                // 计算前缓存的左侧文本选择。
string leftOperand14 = r.width + " x " + r.height;
                // 分支选出的轮次目标整数的返回结果。
string selectedValue13;
                if (r.refreshRateRatio.value > 0)
                {
                    selectedValue13 = " (" + Mathf.RoundToInt((float)r.refreshRateRatio.value) + "Hz)";
                }
                else
                {
                    selectedValue13 = "";
                }

                // 标签。
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
    /// <param name="isFull">是否完整。</param>
    void OnFullscreenToggled(bool isFull)
    {
        SettingsApplicationAdapter.ModifyFullScreen(isFull);
    }

    /// <summary>OnResolutionChanged 的业务处理入口；本方法直接依赖 SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsApplicationAdapter.ModifyResolution(int, int, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">索引。</param>
    void OnResolutionChanged(int index)
    {
        if (availableResolutions == null || index >= availableResolutions.Length)
        {
            return;
        }

        // 资源。
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
        // 判空前缓存的音量服务。
global::IAudioVolumeService receiverValue15 = volumeService;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.SaveVolumeSettings();
        }

        GameSettingsData.SaveValue();
    }
#endregion
}
