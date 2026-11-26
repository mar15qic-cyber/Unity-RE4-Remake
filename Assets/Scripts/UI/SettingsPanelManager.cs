using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// 设置面板管理器
/// 管理图像、音频、控制、操作、游戏、语言、显示HUD、重置设置等子面板
/// </summary>
public class SettingsPanelManager : MonoBehaviour
{
    /// <summary>设置面板根节点。</summary>
    [Header("面板引用")]
    public GameObject settingsPanelRoot;
    /// <summary>设置画布组。</summary>
    public CanvasGroup settingsCanvasGroup;
    /// <summary>图像设置面板。</summary>
    [Header("子面板")]
    public GameObject imageSettingsPanel;
    /// <summary>音频设置面板。</summary>
    public GameObject audioSettingsPanel;
    /// <summary>控制设置面板。</summary>
    public GameObject controlSettingsPanel;
    /// <summary>玩法设置面板。</summary>
    public GameObject gameplaySettingsPanel;
    /// <summary>语言设置面板。</summary>
    public GameObject languageSettingsPanel;
    /// <summary>战斗显示界面设置面板。</summary>
    public GameObject hudSettingsPanel;
    /// <summary>图像按钮。</summary>
    [Header("导航按钮")]
    public Button imageButton;
    /// <summary>音频按钮。</summary>
    public Button audioButton;
    /// <summary>控制按钮。</summary>
    public Button controlButton;
    /// <summary>玩法按钮。</summary>
    public Button gameplayButton;
    /// <summary>语言按钮。</summary>
    public Button languageButton;
    /// <summary>战斗显示界面按钮。</summary>
    public Button hudButton;
    /// <summary>重置按钮。</summary>
    public Button resetButton;
    /// <summary>后方按钮。</summary>
    public Button backButton;
    /// <summary>图像按钮文字。</summary>
    [Header("导航按钮文本")]
    public Text imageButtonText;
    /// <summary>音频按钮文字。</summary>
    public Text audioButtonText;
    /// <summary>控制按钮文字。</summary>
    public Text controlButtonText;
    /// <summary>玩法按钮文字。</summary>
    public Text gameplayButtonText;
    /// <summary>语言按钮文字。</summary>
    public Text languageButtonText;
    /// <summary>战斗显示界面按钮文字。</summary>
    public Text hudButtonText;
    /// <summary>生效按钮颜色。</summary>
    [Header("颜色设置")]
    public Color activeButtonColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>非活动按钮颜色。</summary>
    public Color inactiveButtonColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>确认对话框。</summary>
    [Header("确认对话框")]
    public GameObject confirmDialog;
    /// <summary>确认对话框标题。</summary>
    public Text confirmDialogTitle;
    /// <summary>确认对话框消息。</summary>
    public Text confirmDialogMessage;
    /// <summary>确认对话框确认按钮。</summary>
    public Button confirmDialogYesButton;
    /// <summary>确认对话框无按钮。</summary>
    public Button confirmDialogNoButton;
    // 私有状态
    /// <summary>当前面板索引。</summary>
    private int currentPanelIndex = -1;
    /// <summary>全部面板集合。</summary>
    private List<GameObject> allPanels = new List<GameObject>();
    /// <summary>全部按钮文字集合。</summary>
    private List<Text> allButtonTexts = new List<Text>();
    /// <summary>音量服务。</summary>
    private IAudioVolumeService volumeService;
    // 事件
    /// <summary>设置已关闭时的通知回调。事件通信成员 OnSettingsClosed，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action OnSettingsClosed;
    /// <summary>设置重置时的通知回调。事件通信成员 OnSettingsReset，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action OnSettingsReset;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SettingsPanelManager.HideSettings()、SettingsPanelManager.InitializeSettings()、SettingsPanelManager.SetupEventListeners()、SettingsPanelManager.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        TryBindAudioService();
        InitializeSettings();
        SetupEventListeners();
        HideSettings();
    }

    /// <summary>TryBindAudioService 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.ResetAllSettings()、SettingsPanelManager.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

    /// <summary>
    /// 初始化设置面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.Start()；下游（含内部回调体） GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.SetActive(bool)、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Clear()、List&lt;Text&gt;.Add(Text)、List&lt;Text&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializeSettings()
    {
        // 收集所有子面板
        allPanels.Clear();
        if (imageSettingsPanel != null)
        {
            allPanels.Add(imageSettingsPanel);
        }

        if (audioSettingsPanel != null)
        {
            allPanels.Add(audioSettingsPanel);
        }

        if (controlSettingsPanel != null)
        {
            allPanels.Add(controlSettingsPanel);
        }

        if (gameplaySettingsPanel != null)
        {
            allPanels.Add(gameplaySettingsPanel);
        }

        if (languageSettingsPanel != null)
        {
            allPanels.Add(languageSettingsPanel);
        }

        if (hudSettingsPanel != null)
        {
            allPanels.Add(hudSettingsPanel);
        }

        // 收集所有按钮文本
        allButtonTexts.Clear();
        if (imageButtonText != null)
        {
            allButtonTexts.Add(imageButtonText);
        }

        if (audioButtonText != null)
        {
            allButtonTexts.Add(audioButtonText);
        }

        if (controlButtonText != null)
        {
            allButtonTexts.Add(controlButtonText);
        }

        if (gameplayButtonText != null)
        {
            allButtonTexts.Add(gameplayButtonText);
        }

        if (languageButtonText != null)
        {
            allButtonTexts.Add(languageButtonText);
        }

        if (hudButtonText != null)
        {
            allButtonTexts.Add(hudButtonText);
        }

        // 确保CanvasGroup存在
        if (settingsCanvasGroup == null && settingsPanelRoot != null)
        {
            settingsCanvasGroup = settingsPanelRoot.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
            {
                settingsCanvasGroup = settingsPanelRoot.AddComponent<CanvasGroup>();
            }
        }

        // 隐藏确认对话框
        if (confirmDialog != null)
        {
            confirmDialog.SetActive(false);
        }
    }

    /// <summary>
    /// 设置事件监听器
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.Start()；下游（含内部回调体） SettingsPanelManager.ShowPanel(int)、UnityEvent.AddListener(UnityAction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SetupEventListeners()
    {
        // 导航按钮
        if (imageButton != null)
        {
            imageButton.onClick.AddListener(() => ShowPanel(0));
        }

        if (audioButton != null)
        {
            audioButton.onClick.AddListener(() => ShowPanel(1));
        }

        if (controlButton != null)
        {
            controlButton.onClick.AddListener(() => ShowPanel(2));
        }

        if (gameplayButton != null)
        {
            gameplayButton.onClick.AddListener(() => ShowPanel(3));
        }

        if (languageButton != null)
        {
            languageButton.onClick.AddListener(() => ShowPanel(4));
        }

        if (hudButton != null)
        {
            hudButton.onClick.AddListener(() => ShowPanel(5));
        }

        // 功能按钮
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(HideSettings);
        }

        // 确认对话框按钮
        if (confirmDialogYesButton != null)
        {
            confirmDialogYesButton.onClick.AddListener(OnConfirmReset);
        }

        if (confirmDialogNoButton != null)
        {
            confirmDialogNoButton.onClick.AddListener(OnCancelReset);
        }
    }

    /// <summary>
    /// 显示设置面板
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、SettingsPanelManager.FadeInSettings()、SettingsPanelManager.LoadAllSettings()、SettingsPanelManager.ShowPanel(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowSettings()
    {
        if (settingsPanelRoot == null)
        {
            return;
        }

        settingsPanelRoot.SetActive(true);
        LoadAllSettings();
        ShowPanel(0); // 默认显示图像设置
        // 淡入动画
        StartCoroutine(FadeInSettings());
    }

    /// <summary>
    /// 隐藏设置面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.Start()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、SettingsPanelManager.FadeOutSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideSettings()
    {
        StartCoroutine(FadeOutSettings());
    }

    /// <summary>
    /// 显示指定子面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.SetupEventListeners()、SettingsPanelManager.ShowSettings()；下游（含内部回调体） GameObject.SetActive(bool)、SettingsPanelManager.LoadPanelSettings(int)、SettingsPanelManager.UpdateButtonColors(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="panelIndex">面板索引。</param>
    private void ShowPanel(int panelIndex)
    {
        if (panelIndex < 0 || panelIndex >= allPanels.Count)
        {
            return;
        }

        currentPanelIndex = panelIndex;
        // 隐藏所有面板
        // 当前遍历索引。
for (int i = 0; i < allPanels.Count; i++)
        {
            if (allPanels[i] != null)
            {
                allPanels[i].SetActive(i == panelIndex);
            }
        }

        // 更新按钮颜色
        UpdateButtonColors(panelIndex);
        // 加载对应面板的设置
        LoadPanelSettings(panelIndex);
    }

    /// <summary>
    /// 更新按钮颜色
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.ShowPanel(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="activeIndex">生效索引。</param>
    private void UpdateButtonColors(int activeIndex)
    {
        // 当前遍历索引。
for (int i = 0; i < allButtonTexts.Count; i++)
        {
            if (allButtonTexts[i] != null)
            {
                // 赋值前保存的目标全部按钮文字集合。
global::UnityEngine.UI.Text assignmentReceiver2 = allButtonTexts[i];
                if ((i == activeIndex))
                {
                    assignmentReceiver2.color = activeButtonColor;
                }
                else
                {
                    assignmentReceiver2.color = inactiveButtonColor;
                }

                // 赋值前保存的目标全部按钮文字集合。
global::UnityEngine.UI.Text assignmentReceiver4 = allButtonTexts[i];
                if ((i == activeIndex))
                {
                    assignmentReceiver4.fontStyle = FontStyle.Bold;
                }
                else
                {
                    assignmentReceiver4.fontStyle = FontStyle.Normal;
                }
            }
        }
    }

    /// <summary>
    /// 加载指定面板的设置
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadAllSettings()、SettingsPanelManager.ResetAllSettings()、SettingsPanelManager.ShowPanel(int)；下游（含内部回调体） SettingsPanelManager.LoadAudioSettings()、SettingsPanelManager.LoadControlSettings()、SettingsPanelManager.LoadGameplaySettings()、SettingsPanelManager.LoadHudSettings()、SettingsPanelManager.LoadImageSettings()、SettingsPanelManager.LoadLanguageSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="panelIndex">面板索引。</param>
    private void LoadPanelSettings(int panelIndex)
    {
        switch (panelIndex)
        {
            case 0: // 图像设置
                LoadImageSettings();
                break;
            case 1: // 音频设置
                LoadAudioSettings();
                break;
            case 2: // 控制设置
                LoadControlSettings();
                break;
            case 3: // 游戏设置
                LoadGameplaySettings();
                break;
            case 4: // 语言设置
                LoadLanguageSettings();
                break;
            case 5: // HUD设置
                LoadHudSettings();
                break;
        }
    }

    /// <summary>
    /// 加载所有设置
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.ShowSettings()；下游（含内部回调体） SettingsPanelManager.LoadPanelSettings(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadAllSettings()
    {
        // 当前遍历索引。
for (int i = 0; i < allPanels.Count; i++)
        {
            LoadPanelSettings(i);
        }
    }

#region ===== 各面板设置加载 =====
    /// <summary>LoadImageSettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadImageSettings()
    {
        // 图像设置加载逻辑
        Debug.Log("加载图像设置");
    }

    /// <summary>LoadAudioSettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadAudioSettings()
    {
        // 音频设置加载逻辑
        Debug.Log("加载音频设置");
    }

    /// <summary>LoadControlSettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadControlSettings()
    {
        // 控制设置加载逻辑
        Debug.Log("加载控制设置");
    }

    /// <summary>LoadGameplaySettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadGameplaySettings()
    {
        // 游戏设置加载逻辑
        Debug.Log("加载游戏设置");
    }

    /// <summary>LoadLanguageSettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadLanguageSettings()
    {
        // 语言设置加载逻辑
        Debug.Log("加载语言设置");
    }

    /// <summary>LoadHudSettings 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.LoadPanelSettings(int)；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadHudSettings()
    {
        // HUD设置加载逻辑
        Debug.Log("加载HUD设置");
    }

#endregion
#region ===== 重置设置 =====
    /// <summary>
    /// 重置按钮点击
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnResetClicked()
    {
        if (confirmDialog != null)
        {
            if (confirmDialogTitle != null)
            {
                confirmDialogTitle.text = "重置设置";
            }

            if (confirmDialogMessage != null)
            {
                confirmDialogMessage.text = "确定要将所有设置恢复为默认值吗？";
            }

            confirmDialog.SetActive(true);
        }
    }

    /// <summary>
    /// 确认重置
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、GameObject.SetActive(bool)、object.ReferenceEquals(object, object)、SettingsPanelManager.ResetAllSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnConfirmReset()
    {
        if (confirmDialog != null)
        {
            confirmDialog.SetActive(false);
        }

        ResetAllSettings();
        // 判空前缓存的设置重置时的通知回调。
global::System.Action receiverValue6 = OnSettingsReset;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Invoke();
        }
    }

    /// <summary>
    /// 取消重置
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnCancelReset()
    {
        if (confirmDialog != null)
        {
            confirmDialog.SetActive(false);
        }
    }

    /// <summary>
    /// 重置所有设置
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.OnConfirmReset()；下游（含内部回调体） SettingsApplicationAdapter.ResetPreviousPanelSet(IAudioVolumeService)、SettingsPanelManager.LoadPanelSettings(int)、SettingsPanelManager.TryBindAudioService()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetAllSettings()
    {
        TryBindAudioService();
        SettingsApplicationAdapter.ResetPreviousPanelSet(volumeService);
        // 重新加载当前面板设置
        if (currentPanelIndex >= 0)
        {
            LoadPanelSettings(currentPanelIndex);
        }
    }

#endregion
#region ===== 动画 =====
    /// <summary>
    /// 淡入设置面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.ShowSettings()；下游（含内部回调体） Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private System.Collections.IEnumerator FadeInSettings()
    {
        if (settingsCanvasGroup == null)
        {
            yield break;
        }

        settingsCanvasGroup.alpha = 0f;
        settingsCanvasGroup.interactable = true;
        settingsCanvasGroup.blocksRaycasts = true;
        // 已用。
float elapsed = 0f;
        // 持续时间。
float duration = 0.3f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            settingsCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        settingsCanvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 淡出设置面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.HideSettings()；下游（含内部回调体） Mathf.Lerp(float, float, float)、SettingsPanelManager.FinishHideSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private System.Collections.IEnumerator FadeOutSettings()
    {
        if (settingsCanvasGroup == null)
        {
            FinishHideSettings();
            yield break;
        }

        // 已用。
float elapsed = 0f;
        // 持续时间。
float duration = 0.3f;
        settingsCanvasGroup.interactable = false;
        settingsCanvasGroup.blocksRaycasts = false;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / duration;
            t = t * t * (3f - 2f * t);
            settingsCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        FinishHideSettings();
    }

    /// <summary>
    /// 完成隐藏设置面板
    /// </summary>
    /// <remarks>静态调用链：上游 SettingsPanelManager.FadeOutSettings()；下游（含内部回调体） Action.Invoke()、GameObject.SetActive(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FinishHideSettings()
    {
        if (settingsPanelRoot != null)
        {
            settingsPanelRoot.SetActive(false);
        }

        // 判空前缓存的设置已关闭时的通知回调。
global::System.Action receiverValue7 = OnSettingsClosed;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.Invoke();
        }
    }

#endregion
#region ===== 公共接口 =====
    /// <summary>
    /// 获取当前激活的面板索引
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int GetCurrentPanelIndex()
    {
        return currentPanelIndex;
    }

    /// <summary>
    /// 设置面板启用状态
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="panelIndex">面板索引。</param>
    /// <param name="isEnabled">是否启用。</param>
    public void SetPanelEnabled(int panelIndex, bool isEnabled)
    {
        if (panelIndex < 0 || panelIndex >= allPanels.Count)
        {
            return;
        }

        // 这里可以添加面板启用/禁用逻辑
        Debug.Log($"设置面板 {panelIndex} 启用状态: {isEnabled}");
    }
#endregion
}
