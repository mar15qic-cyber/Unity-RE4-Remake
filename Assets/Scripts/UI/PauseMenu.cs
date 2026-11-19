using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏内暂停菜单。
/// 运行时在现有 PausePanel 下重建界面，避免依赖脆弱的场景子节点引用。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>中文含义：界面视觉主题配置。使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数、InterfaceReworkBuilder.ConfigurationOtherInterface()、PauseMenu.BuildInterface() 等共 4 处。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>界面交互与显示使用的 MenuAction 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    public enum MenuAction
    {
        /// <summary>中文含义：恢复执行。</summary>
        Resume,
        /// <summary>中文含义：加载游戏。</summary>
        LoadGame,
        /// <summary>中文含义：选项集合。</summary>
        Options,
        /// <summary>中文含义：教学。</summary>
        Tutorial,
        /// <summary>中文含义：额外内容。</summary>
        ExtraContent,
        /// <summary>中文含义：挑战。</summary>
        Challenges,
        /// <summary>中文含义：结果集合。</summary>
        Results,
        /// <summary>中文含义：退出目标标题。</summary>
        QuitToTitle
    }

    /// <summary>界面交互与显示模块 MenuEntry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    public sealed class MenuEntry
    {
        /// <summary>中文含义：标签。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.EnsureDefaultEntries()。</summary>
        public string label;
        /// <summary>中文含义：描述。使用位置：PauseMenu.EnsureDefaultEntries()、PauseMenu.UpdateSelection()。</summary>
        [TextArea]
        public string description;
        /// <summary>中文含义：动作。使用位置：PauseMenu.EnsureDefaultEntries()、PauseMenu.ExecuteSelected()。</summary>
        public MenuAction action;
        /// <summary>中文含义：是否启用。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.EnsureDefaultEntries() 等共 7 处。</summary>
        public bool enabled = true;
    }

    /// <summary>中文含义：暂停面板。使用位置：PauseMenu.BuildInterface()、PauseMenu.FadeOut()、PauseMenu.SetInteractionEnabled(bool) 等共 5 处。</summary>
    [Header("面板引用")]
    public CanvasGroup pausePanel;
    /// <summary>中文含义：深色叠加层。使用位置：PauseMenu.BuildInterface()。</summary>
    public CanvasGroup darkOverlay;
    /// <summary>中文含义：标题文字。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildInterface()、PauseMenu.CacheExistingFont() 等共 4 处。</summary>
    public Text titleText;
    /// <summary>中文含义：菜单物品列表。使用位置：PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.UpdateSelection()。</summary>
    public Text[] menuItems;
    /// <summary>中文含义：选择指示器。使用位置：PauseMenu.BuildInterface()、PauseMenu.SetInteractionEnabled(bool)、PauseMenu.UpdateSelection()。</summary>
    [Header("指示条")]
    public RectTransform selectionIndicator;
    /// <summary>中文含义：确认对话框。使用位置：作为 PauseMenu.CreateButton(string, Transform, string) 的参数、作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数 等共 7 处。</summary>
    [Header("确认弹窗")]
    public GameObject confirmDialog;
    /// <summary>中文含义：确认标题。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildConfirmDialog()、PauseMenu.ShowConfirmDialog()。</summary>
    public Text confirmTitle;
    /// <summary>中文含义：确认消息。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildConfirmDialog()、PauseMenu.ShowConfirmDialog()。</summary>
    public Text confirmMessage;
    /// <summary>中文含义：确认确认按钮。使用位置：作为 PauseMenu.BindConfirmPointer(Button, int) 的参数、作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 PauseMenu.SetButtonVisual(Button, bool) 的参数 等共 5 处。</summary>
    public Button confirmYesButton;
    /// <summary>中文含义：确认无按钮。使用位置：作为 PauseMenu.BindConfirmPointer(Button, int) 的参数、作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 PauseMenu.SetButtonVisual(Button, bool) 的参数 等共 5 处。</summary>
    public Button confirmNoButton;
    /// <summary>中文含义：接口字体。使用位置：PauseMenu.BuildInterface()、PauseMenu.CacheExistingFont()、PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。</summary>
    [Header("可替换视觉素材")]
    public Font interfaceFont;
    /// <summary>中文含义：选定行精灵图。使用位置：PauseMenu.BuildInterface()、PauseMenu.CreateButton(string, Transform, string)、PauseMenu.SetButtonVisual(Button, bool) 等共 4 处。</summary>
    public Sprite selectedRowSprite;
    /// <summary>中文含义：对话框帧精灵图。使用位置：PauseMenu.BuildConfirmDialog()。</summary>
    public Sprite dialogFrameSprite;
    /// <summary>中文含义：是否捕获冻结的背景。使用位置：PauseMenu.ShowRoutine()。</summary>
    public bool captureFrozenBackground = true;
    /// <summary>中文含义：背景降采样。使用位置：作为 Mathf.Max(int, int) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。</summary>
    [Range(1, 4)]
    public int backgroundDownsample = 2;
    /// <summary>中文含义：背景柔化着色器。使用位置：作为 Material.Material(Shader) 的参数、PauseMenu.CaptureFrozenBackground()、VillageInterfaceRefinementConfigurator.ApplyScene()。</summary>
    [Header("冻结背景柔化")]
    [UnityEngine.Serialization.FormerlySerializedAs("背景柔化着色器")]
    [UnityEngine.InspectorName("背景柔化着色器")]
    public Shader backgroundSofteningShader;
    /// <summary>中文含义：条目集合。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数 等共 10 处。</summary>
    [Header("菜单配置")]
    public MenuEntry[] entries;
    /// <summary>中文含义：优先返回构建设置中的首个场景，避免标题场景改名后失效。使用位置：PauseMenu.QuitToTitleNow()。</summary>
    [Header("标题画面跳转")]
    [Tooltip("优先返回构建设置中的首个场景，避免标题场景改名后失效。")]
    public int titleSceneBuildIndex;
    /// <summary>中文含义：构建索引不可用时使用的标题场景名称。使用位置：作为 Application.CanStreamedLevelBeLoaded(string) 的参数、作为 SceneManager.LoadScene(string, LoadSceneMode) 的参数、作为 string.IsNullOrWhiteSpace(string) 的参数。</summary>
    [Tooltip("构建索引不可用时使用的标题场景名称。")]
    public string titleSceneName = "TitleScreen";
    /// <summary>中文含义：界面淡入淡出的持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、PauseMenu.FadeOut() 等共 4 处。</summary>
    [Header("动效")]
    public float fadeDuration = 0.2f;
    /// <summary>中文含义：选定颜色。使用位置：PauseMenu.BuildInterface()、PauseMenu.UpdateSelection()。</summary>
    [Header("颜色")]
    public Color selectedColor = Color.white;
    /// <summary>中文含义：普通状态颜色。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int) 等共 5 处。</summary>
    public Color normalColor = new Color(1f, 1f, 1f, 0.68f);
    /// <summary>中文含义：禁用状态颜色。使用位置：PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.UpdateSelection()。</summary>
    public Color disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.42f);
    /// <summary>中文含义：强调色颜色。使用位置：作为 PauseMenu.CreateImage(string, Transform, Color) 的参数、PauseMenu.BuildInterface()。</summary>
    public Color accentColor = new Color(0.72f, 0.08f, 0.06f, 1f);
    /// <summary>中文含义：叠加层颜色。使用位置：作为 PauseMenu.CreateImage(string, Transform, Color) 的参数、PauseMenu.BuildInterface()。</summary>
    public Color overlayColor = new Color(0.015f, 0.02f, 0.035f, 0.82f);
    /// <summary>中文含义：恢复执行时的通知回调。暂停菜单关闭时的回调。</summary>
    [HideInInspector]
    public Action onResume;
    /// <summary>中文含义：打开选项集合时的通知回调。打开选项的回调。</summary>
    [HideInInspector]
    public Action onOpenOptions;
    /// <summary>中文含义：是否可见。</summary>
    public bool IsVisible => isVisible;
    /// <summary>中文含义：是否具有嵌套面板。</summary>
    public bool HasNestedPanel => isConfirming || isSubpageVisible;

    /// <summary>中文含义：行背景集合。只读字段，初始化后不由普通写入口替换。使用位置：PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.UpdateSelection()。</summary>
    private readonly List<Image> rowBackgrounds = new List<Image>();
    /// <summary>中文含义：行矩形集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数、PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int) 等共 5 处。</summary>
    private readonly List<RectTransform> rowRects = new List<RectTransform>();
    /// <summary>中文含义：冻结的背景。使用位置：作为 PauseMenu.Stretch(RectTransform) 的参数、PauseMenu.BuildInterface()、PauseMenu.CaptureFrozenBackground() 等共 5 处。</summary>
    private RawImage frozenBackground;
    /// <summary>中文含义：描述文字。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildInterface()、PauseMenu.SetInteractionEnabled(bool) 等共 4 处。</summary>
    private Text descriptionText;
    /// <summary>中文含义：进度文字。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildInterface()、PauseMenu.SetInteractionEnabled(bool) 等共 4 处。</summary>
    private Text progressText;
    /// <summary>中文含义：底栏提示文字。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildInterface()、PauseMenu.CloseSubpage() 等共 6 处。</summary>
    private Text footerHintText;
    /// <summary>中文含义：子页面面板。使用位置：作为 PauseMenu.CreateButton(string, Transform, string) 的参数、作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数 等共 8 处。</summary>
    private GameObject subpagePanel;
    /// <summary>中文含义：子页面标题。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildSubpage()、PauseMenu.ShowResults() 等共 4 处。</summary>
    private Text subpageTitle;
    /// <summary>中文含义：子页面身体。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildSubpage()、PauseMenu.ShowResults() 等共 4 处。</summary>
    private Text subpageBody;
    /// <summary>中文含义：子页面关闭按钮。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildSubpage()。</summary>
    private Button subpageCloseButton;
    /// <summary>中文含义：冻结的纹理。使用位置：作为 Object.Destroy(Object) 的参数、PauseMenu.CaptureFrozenBackground()、PauseMenu.ReleaseFrozenBackground()。</summary>
    private Texture2D frozenTexture;
    /// <summary>中文含义：选定索引。使用位置：作为 List&lt;RectTransform&gt;.this[int] 的参数、作为 PauseMenu.FindEnabledIndex(int, int) 的参数、作为 UnityEvent.AddListener(UnityAction) 的参数 等共 9 处。</summary>
    private int selectedIndex;
    /// <summary>中文含义：确认选择。使用位置：作为 PauseMenu.SetButtonVisual(Button, bool) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、PauseMenu.HandleConfirmKeyboard() 等共 4 处。</summary>
    private int confirmSelection;
    /// <summary>中文含义：是否可见。使用位置：作为 UnityEvent.AddListener(UnityAction) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、PauseMenu.Hide() 等共 7 处。</summary>
    private bool isVisible;
    /// <summary>中文含义：是否确认中。使用位置：作为 UnityEvent.AddListener(UnityAction) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、PauseMenu.HandleBackRequest() 等共 9 处。</summary>
    private bool isConfirming;
    /// <summary>中文含义：是否子页面可见。使用位置：作为 UnityEvent.AddListener(UnityAction) 的参数、PauseMenu.CloseSubpage()、PauseMenu.HandleBackRequest() 等共 9 处。</summary>
    private bool isSubpageVisible;
    /// <summary>中文含义：交互是否启用。使用位置：作为 UnityEvent.AddListener(UnityAction) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、PauseMenu.Hide() 等共 7 处。</summary>
    private bool interactionEnabled = true;
    /// <summary>中文含义：是否接口已构建。使用位置：PauseMenu.BuildInterface()。</summary>
    private bool interfaceBuilt;
    /// <summary>中文含义：是否切换中场景。使用位置：PauseMenu.QuitToTitleNow()。</summary>
    private bool isChangingScene;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PauseMenu.BuildInterface()、PauseMenu.CacheExistingFont()、PauseMenu.EnsureDefaultEntries()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        CacheExistingFont();
        EnsureDefaultEntries();
        BuildInterface();
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (isVisible)
        {
            return;
        }

        if (pausePanel != null)
        {
            pausePanel.alpha = 0f;
            pausePanel.interactable = false;
            pausePanel.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PauseMenu.ReleaseFrozenBackground()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        ReleaseFrozenBackground();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、PauseMenu.CloseSubpage()、PauseMenu.HandleConfirmKeyboard()、PauseMenu.HandleKeyboardInput()、PauseMenu.HandleMouseInput()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isVisible || !interactionEnabled)
        {
            return;
        }

        if (isConfirming)
        {
            HandleConfirmKeyboard();
            return;
        }

        if (isSubpageVisible)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseSubpage();
            }

            return;
        }

        HandleKeyboardInput();
        HandleMouseInput();
    }

    /// <summary>显示暂停菜单。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ShowPause()；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopAllCoroutines()、PauseMenu.BuildInterface()、PauseMenu.FindEnabledIndex(int, int)、PauseMenu.HideNestedPanels()、PauseMenu.ShowRoutine() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        if (isVisible)
        {
            return;
        }

        isVisible = true;
        isConfirming = false;
        isSubpageVisible = false;
        interactionEnabled = true;
        selectedIndex = FindEnabledIndex(-1, 1);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        BuildInterface();
        HideNestedPanels();
        UpdateProgressText();
        UpdateSelection();
        StopAllCoroutines();
        StartCoroutine(ShowRoutine());
        AudioManager.PlayInterface(AudioEventNames.UIOpen);
    }

    /// <summary>隐藏暂停菜单。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HidePause()、PauseMenu.ExecuteSelected()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopAllCoroutines()、PauseMenu.FadeOut()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        if (!isVisible)
        {
            return;
        }

        isVisible = false;
        isConfirming = false;
        isSubpageVisible = false;
        interactionEnabled = false;
        StopAllCoroutines();
        StartCoroutine(FadeOut());
        AudioManager.PlayInterface(AudioEventNames.UIClose);
    }

    /// <summary>
    /// 设置页打开时禁止暂停菜单在背后继续响应按键。
    /// </summary>
    /// <remarks>静态调用链：上游 GameUIManager.OnOptionsClosed()、GameUIManager.OpenOptionsFromPause()；下游（含内部回调体） GameObject.SetActive(bool)、Transform.Find(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enabled">中文含义：是否启用。使用位置：作为 GameObject.SetActive(bool) 的参数、PauseMenu.SetInteractionEnabled(bool)。</param>
    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        // 设置保留冻结背景，但隐藏暂停菜单的文字和选中标记。
        // list（列表）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：PauseMenu.SetInteractionEnabled(bool)。
var list = transform.Find("菜单列表");
        if (list != null)
        {
            list.gameObject.SetActive(enabled);
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(enabled);
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(enabled);
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(enabled);
        }

        if (footerHintText != null)
        {
            footerHintText.gameObject.SetActive(enabled);
        }

        if (selectionIndicator != null)
        {
            selectionIndicator.gameObject.SetActive(enabled);
        }

        if (pausePanel != null)
        {
            pausePanel.interactable = enabled;
            pausePanel.blocksRaycasts = enabled;
        }
    }

    /// <summary>
    /// 由统一界面中枢优先处理退出键。返回 true 表示只关闭了子面板。
    /// </summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()；下游（含内部回调体） PauseMenu.CloseSubpage()、PauseMenu.OnConfirmNo()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HandleBackRequest()
    {
        if (isChangingScene)
        {
            return true;
        }

        if (isConfirming)
        {
            OnConfirmNo();
            return true;
        }

        if (isSubpageVisible)
        {
            CloseSubpage();
            return true;
        }

        return false;
    }

    /// <summary>ShowRoutine 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Pow(float, float)、PauseMenu.CaptureFrozenBackground()、WaitForEndOfFrame.WaitForEndOfFrame()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Show()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Pow(float, float)、PauseMenu.CaptureFrozenBackground()、WaitForEndOfFrame.WaitForEndOfFrame()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator ShowRoutine()
    {
        if (pausePanel == null)
        {
            yield break;
        }

        pausePanel.alpha = 0f;
        pausePanel.interactable = false;
        pausePanel.blocksRaycasts = true;
        if (captureFrozenBackground && frozenBackground != null)
        {
            yield return new WaitForEndOfFrame();
            CaptureFrozenBackground();
        }

        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、PauseMenu.ShowRoutine()。
float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Pow(float, float) 的参数、PauseMenu.ShowRoutine()。
float t = Mathf.Clamp01(elapsed / fadeDuration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            pausePanel.alpha = t;
            yield return null;
        }

        pausePanel.alpha = 1f;
        pausePanel.interactable = true;
        pausePanel.blocksRaycasts = true;
    }

    /// <summary>FadeOut 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、PauseMenu.HideNestedPanels()、PauseMenu.ReleaseFrozenBackground()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Hide()；下游（含内部回调体） GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、PauseMenu.HideNestedPanels()、PauseMenu.ReleaseFrozenBackground()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOut()
    {
        if (pausePanel != null)
        {
            pausePanel.interactable = false;
            pausePanel.blocksRaycasts = false;
            // startAlpha（起始透明度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float startAlpha = pausePanel.alpha;
            // elapsed（已用）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、PauseMenu.FadeOut()。
float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                pausePanel.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }

            pausePanel.alpha = 0f;
        }

        HideNestedPanels();
        ReleaseFrozenBackground();
        gameObject.SetActive(false);
    }

    /// <summary>CaptureFrozenBackground 的业务处理入口；本方法直接依赖 Graphics.Blit(Texture, RenderTexture, Material)、Graphics.Blit(Texture, RenderTexture)、Material.Material(Shader)、Material.SetVector(string, Vector4)、Mathf.Clamp(int, int, int)、Mathf.Max(int, int)、Mathf.RoundToInt(float) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ShowRoutine()；下游（含内部回调体） Graphics.Blit(Texture, RenderTexture, Material)、Graphics.Blit(Texture, RenderTexture)、Material.Material(Shader)、Material.SetVector(string, Vector4)、Mathf.Clamp(int, int, int)、Mathf.Max(int, int)、Mathf.RoundToInt(float) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CaptureFrozenBackground()
    {
        ReleaseFrozenBackground();
        // source（来源二维纹理）：保存 ScreenCapture.CaptureScreenshotAsTexture() 的返回结果，供当前步骤使用。使用位置：作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.RoundToInt(float) 的参数 等共 5 处。
Texture2D source = ScreenCapture.CaptureScreenshotAsTexture();
        if (source == null)
        {
            return;
        }

        // width（宽度）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.RoundToInt(float) 的参数、作为 Rect.Rect(float, float, float, float) 的参数、作为 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的参数 等共 4 处。
int width = Mathf.Clamp(source.width / Mathf.Max(1, backgroundDownsample), 640, 1280);
        // height（高度）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：作为 Rect.Rect(float, float, float, float) 的参数、作为 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的参数、作为 Texture2D.Texture2D(int, int, TextureFormat, bool) 的参数。
int height = Mathf.RoundToInt(width * source.height / (float)source.width);
        // temporary（临时）：保存 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的返回结果，供当前步骤使用。使用位置：作为 Graphics.Blit(Texture, RenderTexture, Material) 的参数、作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数 等共 4 处。
RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        temporary.filterMode = FilterMode.Bilinear;
        temporary.wrapMode = TextureWrapMode.Clamp;
        Graphics.Blit(source, temporary);
        if (backgroundSofteningShader != null)
        {
            // material（材质）：使用位置：作为 Graphics.Blit(Texture, RenderTexture, Material) 的参数、作为 Object.Destroy(Object) 的参数、PauseMenu.CaptureFrozenBackground()。
var material = new Material(backgroundSofteningShader);
            // pass（通过）：保存 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat) 的返回结果，供当前步骤使用。使用位置：作为 Graphics.Blit(Texture, RenderTexture, Material) 的参数、作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数、PauseMenu.CaptureFrozenBackground()。
var pass = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            pass.filterMode = FilterMode.Bilinear;
            pass.wrapMode = TextureWrapMode.Clamp;
            // i（当前遍历索引）：使用位置：作为 Vector4.Vector4(float, float, float, float) 的参数、PauseMenu.CaptureFrozenBackground()。
for (int i = 0; i < 2; i++)
            {
                material.SetVector("_Direction", new Vector4(1f + i * .5f, 0, 0, 0));
                Graphics.Blit(temporary, pass, material);
                material.SetVector("_Direction", new Vector4(0, 1f + i * .5f, 0, 0));
                Graphics.Blit(pass, temporary, material);
            }

            RenderTexture.ReleaseTemporary(pass);
            Destroy(material);
        }

        // previous（前一次）：使用位置：PauseMenu.CaptureFrozenBackground()。
RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        frozenTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
        frozenTexture.name = "暂停背景快照";
        frozenTexture.filterMode = FilterMode.Bilinear;
        frozenTexture.wrapMode = TextureWrapMode.Clamp;
        frozenTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        frozenTexture.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        Destroy(source);
        frozenBackground.texture = frozenTexture;
        frozenBackground.color = Color.white;
    }

    /// <summary>ReleaseFrozenBackground 的业务处理入口；本方法直接依赖 Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.CaptureFrozenBackground()、PauseMenu.FadeOut()、PauseMenu.OnDestroy()；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ReleaseFrozenBackground()
    {
        if (frozenBackground != null)
        {
            frozenBackground.texture = null;
        }

        if (frozenTexture == null)
        {
            return;
        }

        Destroy(frozenTexture);
        frozenTexture = null;
    }

    /// <summary>HandleKeyboardInput 的业务处理入口；本方法直接依赖 Input.GetKeyDown(KeyCode)、PauseMenu.ExecuteSelected()、PauseMenu.Navigate(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Update()；下游（含内部回调体） Input.GetKeyDown(KeyCode)、PauseMenu.ExecuteSelected()、PauseMenu.Navigate(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            Navigate(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            Navigate(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteSelected();
        }
    }

    /// <summary>HandleMouseInput 的业务处理入口；本方法直接依赖 PauseMenu.PlayHoverSound()、PauseMenu.UpdateSelection()、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Update()；下游（含内部回调体） PauseMenu.PlayHoverSound()、PauseMenu.UpdateSelection()、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleMouseInput()
    {
        // mousePosition（鼠标位置）：使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数。
Vector2 mousePosition = Input.mousePosition;
        // i（当前遍历索引）：使用位置：作为 List<RectTransform>.this[int] 的参数、PauseMenu.HandleMouseInput()。
for (int i = 0; i < rowRects.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(rowRects[i], mousePosition))
            {
                continue;
            }

            if (entries[i].enabled && selectedIndex != i)
            {
                selectedIndex = i;
                PlayHoverSound();
                UpdateSelection();
            }

            return;
        }
    }

    /// <summary>Navigate 的业务处理入口；本方法直接依赖 PauseMenu.FindEnabledIndex(int, int)、PauseMenu.PlayHoverSound()、PauseMenu.UpdateSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.HandleKeyboardInput()；下游（含内部回调体） PauseMenu.FindEnabledIndex(int, int)、PauseMenu.PlayHoverSound()、PauseMenu.UpdateSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 PauseMenu.FindEnabledIndex(int, int) 的参数。</param>
    void Navigate(int direction)
    {
        // next（下一次）：保存 PauseMenu.FindEnabledIndex(int, int) 的返回结果，供当前步骤使用。使用位置：PauseMenu.Navigate(int)。
int next = FindEnabledIndex(selectedIndex, direction);
        if (next == selectedIndex)
        {
            return;
        }

        selectedIndex = next;
        PlayHoverSound();
        UpdateSelection();
    }

    /// <summary>FindEnabledIndex 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Navigate(int)、PauseMenu.Show()；下游（含内部回调体） Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="start">中文含义：起始。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、PauseMenu.FindEnabledIndex(int, int)。</param>
    /// <param name="direction">中文含义：方向。使用位置：PauseMenu.FindEnabledIndex(int, int)。</param>
    int FindEnabledIndex(int start, int direction)
    {
        if (entries == null || entries.Length == 0)
        {
            return 0;
        }

        // index（当前元素索引）：使用位置：PauseMenu.FindEnabledIndex(int, int)。
int index = start;
        // i（当前遍历索引）：使用位置：PauseMenu.FindEnabledIndex(int, int)。
for (int i = 0; i < entries.Length; i++)
        {
            index = (index + direction + entries.Length) % entries.Length;
            if (entries[index] != null && entries[index].enabled)
            {
                return index;
            }
        }

        return Mathf.Clamp(start, 0, entries.Length - 1);
    }

    /// <summary>ExecuteSelected 的业务处理入口；本方法直接依赖 Action.Invoke()、GameUIManager.OpenOptionsFromPause()、object.ReferenceEquals(object, object)、PauseMenu.Hide()、PauseMenu.PlayConfirmSound()、PauseMenu.ShowConfirmDialog()、PauseMenu.ShowResults() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.HandleKeyboardInput()；下游（含内部回调体） Action.Invoke()、GameUIManager.OpenOptionsFromPause()、object.ReferenceEquals(object, object)、PauseMenu.Hide()、PauseMenu.PlayConfirmSound()、PauseMenu.ShowConfirmDialog()、PauseMenu.ShowResults() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ExecuteSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= entries.Length)
        {
            return;
        }

        // entry（当前菜单入口）：使用位置：PauseMenu.ExecuteSelected()。
MenuEntry entry = entries[selectedIndex];
        if (entry == null || !entry.enabled)
        {
            return;
        }

        switch (entry.action)
        {
            case MenuAction.Resume:
                Hide();
                // receiverValue1（判空前缓存的恢复执行时的通知回调）：从恢复执行时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PauseMenu.ExecuteSelected()。
global::System.Action receiverValue1 = onResume;
                if (!global::System.Object.ReferenceEquals(receiverValue1, null))
                {
                    receiverValue1.Invoke();
                }

                break;
            case MenuAction.Options:
                if (GameUIManager.HasInstance)
                {
                    GameUIManager.Instance.OpenOptionsFromPause();
                }
                else
                {
                    // receiverValue2（判空前缓存的打开选项集合时的通知回调）：从打开选项集合时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PauseMenu.ExecuteSelected()。
global::System.Action receiverValue2 = onOpenOptions;
                    if (!global::System.Object.ReferenceEquals(receiverValue2, null))
                    {
                        receiverValue2.Invoke();
                    }
                }

                break;
            case MenuAction.Tutorial:
                ShowTutorial();
                break;
            case MenuAction.Results:
                ShowResults();
                break;
            case MenuAction.QuitToTitle:
                PlayConfirmSound();
                ShowConfirmDialog();
                break;
        }
    }

    /// <summary>UpdateSelection 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.CloseSubpage()、PauseMenu.HandleMouseInput()、PauseMenu.Navigate(int)、PauseMenu.OnConfirmNo() 等共 6 处；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSelection()
    {
        // i（当前遍历索引）：使用位置：作为 List<Image>.this[int] 的参数、PauseMenu.UpdateSelection()。
for (int i = 0; i < menuItems.Length; i++)
        {
            // selected（是否选定）：使用位置：PauseMenu.UpdateSelection()。
bool selected = i == selectedIndex;
            // enabled（是否启用）：使用位置：PauseMenu.UpdateSelection()。
bool enabled = entries[i].enabled;
            // item（当前文字组件）：使用位置：PauseMenu.UpdateSelection()。
Text item = menuItems[i];
            // assignmentReceiver3（赋值前保存的目标当前文字组件）：从物品读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.UpdateSelection()。
global::UnityEngine.UI.Text assignmentReceiver3 = item;
            if (!enabled)
            {
                assignmentReceiver3.color = disabledColor;
            }
            else
            {
                if (selected)
                {
                    assignmentReceiver3.color = selectedColor;
                }
                else
                {
                    assignmentReceiver3.color = normalColor;
                }
            }

            // assignmentReceiver6（赋值前保存的目标当前文字组件）：从物品读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.UpdateSelection()。
global::UnityEngine.UI.Text assignmentReceiver6 = item;
            if (selected && enabled)
            {
                assignmentReceiver6.fontStyle = FontStyle.Bold;
            }
            else
            {
                assignmentReceiver6.fontStyle = FontStyle.Normal;
            }

            // background（背景）：使用位置：PauseMenu.UpdateSelection()。
Image background = rowBackgrounds[i];
            background.enabled = selected && enabled;
            if (background.enabled)
            {
                background.sprite = selectedRowSprite;
                // assignmentReceiver8（赋值前保存的目标背景）：从背景读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.UpdateSelection()。
global::UnityEngine.UI.Image assignmentReceiver8 = background;
                if (selectedRowSprite != null)
                {
                    assignmentReceiver8.type = Image.Type.Sliced;
                }
                else
                {
                    assignmentReceiver8.type = Image.Type.Simple;
                }

                // assignmentReceiver10（赋值前保存的目标背景）：从背景读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.UpdateSelection()。
global::UnityEngine.UI.Image assignmentReceiver10 = background;
                if (selectedRowSprite != null)
                {
                    assignmentReceiver10.color = new Color(1f, 1f, 1f, 0.18f);
                }
                else
                {
                    assignmentReceiver10.color = new Color(1f, 1f, 1f, 0.08f);
                }
            }
        }

        if (selectionIndicator != null)
        {
            // show（是否显示）：使用位置：作为 GameObject.SetActive(bool) 的参数、PauseMenu.UpdateSelection()。
bool show = selectedIndex >= 0 && selectedIndex < rowRects.Count && entries[selectedIndex].enabled;
            selectionIndicator.gameObject.SetActive(show);
            if (show)
            {
                // target（目标界面矩形变换）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数。
RectTransform target = rowRects[selectedIndex];
                selectionIndicator.SetParent(target, false);
                selectionIndicator.anchorMin = new Vector2(0f, 0.12f);
                selectionIndicator.anchorMax = new Vector2(0f, 0.88f);
                selectionIndicator.pivot = new Vector2(0f, 0.5f);
                selectionIndicator.anchoredPosition = Vector2.zero;
                selectionIndicator.sizeDelta = new Vector2(4f, 0f);
            }
        }

        if (descriptionText != null && selectedIndex >= 0 && selectedIndex < entries.Length)
        {
            descriptionText.text = entries[selectedIndex].description;
        }
    }

    /// <summary>UpdateProgressText 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)、TimeSpan.FromSeconds(double)、TimeSpan.ToString(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Show()；下游（含内部回调体） Mathf.Max(float, float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)、TimeSpan.FromSeconds(double)、TimeSpan.ToString(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateProgressText()
    {
        // progress（进度）：使用位置：作为 Mathf.Max(float, float) 的参数、PauseMenu.UpdateProgressText()。
MissionProgressSnapshot progress;
        if (ChapterOneMissionManager.HasInstance)
        {
            progress = ChapterOneMissionManager.Instance.LatestSnapshot;
        }
        else
        {
            progress = new MissionProgressSnapshot(0, 15, 0, 2, 0f, 240f, false, false);
        }

        // elapsed（已用）：保存 TimeSpan.FromSeconds(double) 的返回结果，供当前步骤使用。使用位置：PauseMenu.UpdateProgressText()。
TimeSpan elapsed = TimeSpan.FromSeconds(Mathf.Max(0f, progress.ElapsedSeconds));
        progressText.text = "第一章    │    村庄    │    击杀 " + progress.TotalKills + "    │    " + elapsed.ToString(@"hh\:mm\:ss");
    }

    /// <summary>ShowTutorial 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ExecuteSelected()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ShowTutorial()
    {
        AudioManager.PlayInterface(AudioEventNames.UIPage);
        isSubpageVisible = true;
        subpagePanel.SetActive(true);
        subpageTitle.text = "游戏教程";
        subpageBody.text = "移动：W A S D\n" + "奔跑：左 Shift\n" + "瞄准：鼠标右键\n" + "射击：瞄准时按鼠标左键\n" + "装填：R\n" + "互动／拾取：F\n" + "近战：E\n" + "背包：Tab\n\n" + "击败敌人可以获得分数。注意弹药、距离和敌人的包围路线。";
        footerHintText.text = "查看当前游戏的基础操作";
    }

    /// <summary>ShowResults 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Mathf.CeilToInt(float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ExecuteSelected()；下游（含内部回调体） GameObject.SetActive(bool)、Mathf.CeilToInt(float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ShowResults()
    {
        AudioManager.PlayInterface(AudioEventNames.UIPage);
        // progress（进度）：使用位置：作为 Mathf.CeilToInt(float) 的参数、PauseMenu.ShowResults()。
MissionProgressSnapshot progress;
        if (ChapterOneMissionManager.HasInstance)
        {
            progress = ChapterOneMissionManager.Instance.LatestSnapshot;
        }
        else
        {
            progress = new MissionProgressSnapshot(0, 15, 0, 2, 0f, 240f, false, false);
        }

        // remaining（剩余）：保存 Mathf.CeilToInt(float) 的返回结果，供当前步骤使用。使用位置：PauseMenu.ShowResults()。
int remaining = Mathf.CeilToInt(progress.RemainingSeconds);
        // minutes（分钟）：使用位置：PauseMenu.ShowResults()。
int minutes = remaining / 60;
        // seconds（秒）：使用位置：PauseMenu.ShowResults()。
int seconds = remaining % 60;
        isSubpageVisible = true;
        subpagePanel.SetActive(true);
        subpageTitle.text = "游戏成绩";
        subpageBody.text = "普通敌人                    " + progress.GanadoKills + "/" + progress.GanadoTarget + "\n\n" + "电锯敌人                    " + progress.SawmanKills + "/" + progress.SawmanTarget + "\n\n" + "生存倒计时                  " + $"{minutes:00}:{seconds:00}";
        footerHintText.text = "查看第一章任务的当前进度";
    }

    /// <summary>CloseSubpage 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、PauseMenu.UpdateSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.HandleBackRequest()、PauseMenu.Update()；下游（含内部回调体） GameObject.SetActive(bool)、PauseMenu.UpdateSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CloseSubpage()
    {
        AudioManager.PlayInterface(AudioEventNames.UICancel);
        isSubpageVisible = false;
        subpagePanel.SetActive(false);
        UpdateSelection();
        footerHintText.text = "空格  确定        Esc  返回";
    }

    /// <summary>ShowConfirmDialog 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、PauseMenu.UpdateConfirmSelection()、Transform.SetAsLastSibling()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ExecuteSelected()；下游（含内部回调体） GameObject.SetActive(bool)、PauseMenu.UpdateConfirmSelection()、Transform.SetAsLastSibling()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ShowConfirmDialog()
    {
        isConfirming = true;
        confirmSelection = 1;
        confirmDialog.SetActive(true);
        confirmDialog.transform.SetAsLastSibling();
        confirmTitle.text = "返回标题画面";
        confirmMessage.text = "当前未保存的进度将会丢失。\n确定要退出本局游戏吗？";
        UpdateConfirmSelection();
    }

    /// <summary>HandleConfirmKeyboard 的业务处理入口；本方法直接依赖 Input.GetKeyDown(KeyCode)、PauseMenu.OnConfirmNo()、PauseMenu.OnConfirmYes()、PauseMenu.UpdateConfirmSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Update()；下游（含内部回调体） Input.GetKeyDown(KeyCode)、PauseMenu.OnConfirmNo()、PauseMenu.OnConfirmYes()、PauseMenu.UpdateConfirmSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleConfirmKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
        {
            confirmSelection = 1 - confirmSelection;
            PlayHoverSound();
            UpdateConfirmSelection();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            if (confirmSelection == 0)
            {
                OnConfirmYes();
            }
            else
            {
                OnConfirmNo();
            }
        }
    }

    /// <summary>UpdateConfirmSelection 的业务处理入口；本方法直接依赖 PauseMenu.SetButtonVisual(Button, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BindConfirmPointer(Button, int)、PauseMenu.HandleConfirmKeyboard()、PauseMenu.ShowConfirmDialog()；下游（含内部回调体） PauseMenu.SetButtonVisual(Button, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateConfirmSelection()
    {
        SetButtonVisual(confirmYesButton, confirmSelection == 0);
        SetButtonVisual(confirmNoButton, confirmSelection == 1);
    }

    /// <summary>SetButtonVisual 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.UpdateConfirmSelection()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponentInChildren&lt;Text&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="button">中文含义：按钮。使用位置：PauseMenu.SetButtonVisual(Button, bool)。</param>
    /// <param name="selected">中文含义：是否选定。使用位置：PauseMenu.SetButtonVisual(Button, bool)。</param>
    void SetButtonVisual(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        // image（图像）：使用位置：PauseMenu.SetButtonVisual(Button, bool)。
Image image = button.image;
        if (image != null)
        {
            // assignmentReceiver14（赋值前保存的目标图像）：从图像读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.SetButtonVisual(Button, bool)。
global::UnityEngine.UI.Image assignmentReceiver14 = image;
            if (selected)
            {
                assignmentReceiver14.color = new Color(0.8f, 0.8f, 0.78f, 0.75f);
            }
            else
            {
                // argumentValue16（按原顺序缓存的实参1f）：从1f读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Color.Color(float, float, float, float) 的参数。
                assignmentReceiver14.color = Color.clear;
            }
        }

        // label（标签）：保存 Component.GetComponentInChildren<Text>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.SetButtonVisual(Button, bool)。
Text label = button.GetComponentInChildren<Text>();
        if (label != null)
        {
            // assignmentReceiver20（赋值前保存的目标标签）：从标签读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.SetButtonVisual(Button, bool)。
global::UnityEngine.UI.Text assignmentReceiver20 = label;
            if (selected)
            {
                assignmentReceiver20.color = Color.white;
            }
            else
            {
                assignmentReceiver20.color = normalColor;
            }
        }
    }

    /// <summary>OnConfirmYes 的业务处理入口；本方法直接依赖 PauseMenu.QuitToTitleNow()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.HandleConfirmKeyboard()；下游（含内部回调体） PauseMenu.QuitToTitleNow()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnConfirmYes()
    {
        if (!isChangingScene)
        {
            StartCoroutine(QuitToTitle());
        }
    }

    /// <summary>OnConfirmNo 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、PauseMenu.UpdateSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.HandleBackRequest()、PauseMenu.HandleConfirmKeyboard()；下游（含内部回调体） GameObject.SetActive(bool)、PauseMenu.UpdateSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnConfirmNo()
    {
        AudioManager.PlayInterface(AudioEventNames.UICancel);
        isConfirming = false;
        confirmDialog.SetActive(false);
        UpdateSelection();
    }

    /// <summary>QuitToTitleNow 的业务处理入口；本方法直接依赖 Application.CanStreamedLevelBeLoaded(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameUIManager.ClearAllModals()、SceneManager.LoadScene(int, LoadSceneMode)、SceneManager.LoadScene(string, LoadSceneMode)、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.OnConfirmYes()；下游（含内部回调体） Application.CanStreamedLevelBeLoaded(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameUIManager.ClearAllModals()、SceneManager.LoadScene(int, LoadSceneMode)、SceneManager.LoadScene(string, LoadSceneMode)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator QuitToTitle()
    {
        isChangingScene = true;
        interactionEnabled = false;
        if (confirmYesButton != null)
        {
            confirmYesButton.interactable = false;
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.interactable = false;
        }

        yield return AudioManager.PlayInterfaceBeforeTransition(AudioEventNames.UIQuitConfirm);
        GameManager.ResetScenePermission();
        // sceneBuildIndex（场景构建索引）：使用位置：作为 SceneManager.LoadScene(int, LoadSceneMode) 的参数、PauseMenu.QuitToTitleNow()。
int sceneBuildIndex = -1;
        if (titleSceneBuildIndex >= 0 && titleSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            sceneBuildIndex = titleSceneBuildIndex;
        }
        else if (!string.IsNullOrWhiteSpace(titleSceneName) && Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            if (GameUIManager.HasInstance)
            {
                GameUIManager.Instance.ClearAllModals();
            }

            SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
            yield break;
        }

        if (sceneBuildIndex < 0)
        {
            Debug.LogError("[暂停菜单] 找不到标题场景，请检查构建设置中的第一个场景。");
            isChangingScene = false;
            interactionEnabled = true;
            if (confirmYesButton != null)
            {
                confirmYesButton.interactable = true;
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.interactable = true;
            }

            yield break;
        }

        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.ClearAllModals();
        }

        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    /// <summary>HideNestedPanels 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.FadeOut()、PauseMenu.Show()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HideNestedPanels()
    {
        if (confirmDialog != null)
        {
            confirmDialog.SetActive(false);
        }

        if (subpagePanel != null)
        {
            subpagePanel.SetActive(false);
        }
    }

    /// <summary>EnsureDefaultEntries 的业务处理入口；本方法直接依赖 PauseMenu.MenuEntry.MenuEntry()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Awake()；下游（含内部回调体） PauseMenu.MenuEntry.MenuEntry()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void EnsureDefaultEntries()
    {
        if (entries != null && entries.Length == 8)
        {
            return;
        }

        entries = new[]
        {
            new MenuEntry
            {
                label = "返回游戏",
                description = "退出暂停菜单并返回游戏",
                action = MenuAction.Resume,
                enabled = true
            },
            new MenuEntry
            {
                label = "加载游戏",
                description = "演示版本暂未开放存档读取功能",
                action = MenuAction.LoadGame,
                enabled = false
            },
            new MenuEntry
            {
                label = "设置选项",
                description = "调整音量、画面和操控设置",
                action = MenuAction.Options,
                enabled = true
            },
            new MenuEntry
            {
                label = "游戏教程",
                description = "查看移动、战斗和互动操作",
                action = MenuAction.Tutorial,
                enabled = true
            },
            new MenuEntry
            {
                label = "额外内容",
                description = "演示版本暂未开放额外内容",
                action = MenuAction.ExtraContent,
                enabled = false
            },
            new MenuEntry
            {
                label = "挑战列表",
                description = "演示版本暂未开放挑战系统",
                action = MenuAction.Challenges,
                enabled = false
            },
            new MenuEntry
            {
                label = "游戏成绩",
                description = "查看当前波次、击败数量和得分",
                action = MenuAction.Results,
                enabled = true
            },
            new MenuEntry
            {
                label = "退出游戏",
                description = "结束当前游戏并返回标题画面",
                action = MenuAction.QuitToTitle,
                enabled = true
            }
        };
    }

    /// <summary>CacheExistingFont 的业务处理入口；本方法直接依赖 Component.GetComponentInChildren&lt;Text&gt;(bool)、Resources.GetBuiltinResource&lt;Font&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Awake()；下游（含内部回调体） Component.GetComponentInChildren&lt;Text&gt;(bool)、Resources.GetBuiltinResource&lt;Font&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CacheExistingFont()
    {
        if (interfaceFont != null)
        {
            return;
        }

        if (titleText != null && titleText.font != null)
        {
            interfaceFont = titleText.font;
            return;
        }

        // existingText（已有文字）：保存 Component.GetComponentInChildren<Text>(bool) 的返回结果，供当前步骤使用。使用位置：PauseMenu.CacheExistingFont()。
Text existingText = GetComponentInChildren<Text>(true);
        if (existingText != null && existingText.font != null)
        {
            interfaceFont = existingText.font;
        }

        if (interfaceFont == null)
        {
            interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;() 等共 24 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.Awake()、PauseMenu.Show()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;RectTransform&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;() 等共 24 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildInterface()
    {
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

            selectedColor = visualTheme.menuText;
            normalColor = visualTheme.menuMuted;
            accentColor = visualTheme.menuLine;
            overlayColor = new Color(0.015f, 0.022f, 0.018f, 0.64f);
            selectedRowSprite = null;
        }

        if (interfaceBuilt)
        {
            return;
        }

        interfaceBuilt = true;
        // rootRect（根节点矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 PauseMenu.Stretch(RectTransform) 的参数、PauseMenu.BuildInterface()。
RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        Stretch(rootRect);
        pausePanel = GetComponent<CanvasGroup>();
        if (pausePanel == null)
        {
            pausePanel = gameObject.AddComponent<CanvasGroup>();
        }

        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、PauseMenu.BuildInterface()。
for (int i = transform.childCount - 1; i >= 0; i--)
        {
            // child（子级）：使用位置：作为 Object.Destroy(Object) 的参数、PauseMenu.BuildInterface()。
GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        frozenBackground = CreateRawImage("冻结背景", transform);
        Stretch(frozenBackground.rectTransform);
        frozenBackground.color = Color.black;
        frozenBackground.raycastTarget = false;
        // overlay（叠加层）：保存 PauseMenu.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 PauseMenu.Stretch(RectTransform) 的参数、PauseMenu.BuildInterface()。
Image overlay = CreateImage("深色遮罩", transform, overlayColor);
        Stretch(overlay.rectTransform);
        darkOverlay = overlay.gameObject.AddComponent<CanvasGroup>();
        // leftShade（左侧阴影色）：保存 PauseMenu.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数。
Image leftShade = CreateImage("左侧渐暗", transform, new Color(0f, 0f, 0f, 0.48f));
        SetAnchors(leftShade.rectTransform, new Vector2(0f, 0f), new Vector2(0.38f, 1f));
        titleText = CreateText("暂停标题", transform, "暂停", 22, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.52f));
        SetAnchors(titleText.rectTransform, new Vector2(0.082f, 0.82f), new Vector2(0.28f, 0.88f));
        // menuRoot（菜单根节点）：使用位置：作为 PauseMenu.BuildMenuRow(Transform, int) 的参数、作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildInterface()。
GameObject menuRoot = new GameObject("菜单列表", typeof(RectTransform), typeof(VerticalLayoutGroup));
        menuRoot.transform.SetParent(transform, false);
        SetAnchors(menuRoot.GetComponent<RectTransform>(), new Vector2(0.075f, 0.25f), new Vector2(0.34f, 0.80f));
        // menuLayout（菜单布局）：保存 GameObject.GetComponent<VerticalLayoutGroup>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.BuildInterface()。
VerticalLayoutGroup menuLayout = menuRoot.GetComponent<VerticalLayoutGroup>();
        menuLayout.spacing = 4f;
        menuLayout.childControlHeight = false;
        menuLayout.childForceExpandHeight = false;
        menuLayout.childAlignment = TextAnchor.UpperLeft;
        menuItems = new Text[entries.Length];
        rowBackgrounds.Clear();
        rowRects.Clear();
        // i（当前遍历索引）：使用位置：作为 PauseMenu.BuildMenuRow(Transform, int) 的参数、PauseMenu.BuildInterface()。
for (int i = 0; i < entries.Length; i++)
        {
            BuildMenuRow(menuRoot.transform, i);
        }

        selectionIndicator = CreateImage("选择指示线", transform, accentColor).rectTransform;
        descriptionText = CreateText("功能说明", transform, string.Empty, 19, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.72f));
        SetAnchors(descriptionText.rectTransform, new Vector2(0.34f, 0.055f), new Vector2(0.72f, 0.115f));
        progressText = CreateText("游戏进度", transform, string.Empty, 18, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.68f));
        SetAnchors(progressText.rectTransform, new Vector2(0.082f, 0.105f), new Vector2(0.54f, 0.165f));
        footerHintText = CreateText("按键提示", transform, "空格  确定        Esc  返回", 16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.62f));
        SetAnchors(footerHintText.rectTransform, new Vector2(0.38f, 0.012f), new Vector2(0.68f, 0.06f));
        BuildConfirmDialog();
        BuildSubpage();
        MenuThemeApplier.Apply(transform, visualTheme);
        if (confirmYesButton != null)
        {
            confirmYesButton.transition = Selectable.Transition.None;
        }
        if (confirmNoButton != null)
        {
            confirmNoButton.transition = Selectable.Transition.None;
        }
    }

    /// <summary>BuildMenuRow 的业务处理入口；本方法直接依赖 EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;Button&gt;()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;RectTransform&gt;() 等共 18 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildInterface()；下游（含内部回调体） EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;Button&gt;()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;RectTransform&gt;() 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="index">中文含义：索引。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、PauseMenu.BuildMenuRow(Transform, int)。</param>
    void BuildMenuRow(Transform parent, int index)
    {
        // row（行）：使用位置：PauseMenu.BuildMenuRow(Transform, int)。
GameObject row = new GameObject("菜单项_" + index + "_" + entries[index].label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 List<RectTransform>.Add(RectTransform) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 48f);
        // layout（布局）：保存 GameObject.GetComponent<LayoutElement>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.BuildMenuRow(Transform, int)。
LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.preferredHeight = 48f;
        layout.minHeight = 48f;
        // background（背景）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：作为 List<Image>.Add(Image) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
Image background = row.GetComponent<Image>();
        background.color = Color.clear;
        background.raycastTarget = true;
        // button（按钮）：保存 GameObject.AddComponent<Button>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.BuildMenuRow(Transform, int)。
Button button = row.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.interactable = entries[index].enabled;
        // capturedIndex（已捕获索引）：使用位置：作为 UnityEvent.AddListener(UnityAction) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
int capturedIndex = index;
        button.onClick.AddListener(() =>
        {
            if (!isVisible || !interactionEnabled || isConfirming || isSubpageVisible)
            {
                return;
            }

            selectedIndex = capturedIndex;
            UpdateSelection();
            ExecuteSelected();
        });
        // trigger（触发器）：保存 GameObject.AddComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.BuildMenuRow(Transform, int)。
EventTrigger trigger = row.AddComponent<EventTrigger>();
        // enter（进入）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            if (!isVisible || !interactionEnabled || !entries[capturedIndex].enabled)
            {
                return;
            }

            selectedIndex = capturedIndex;
            UpdateSelection();
        });
        trigger.triggers.Add(enter);
        // argumentValue24（按原顺序缓存的实参文字“文字”）：从文字“文字”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。
string argumentValue24 = "文字";
        // argumentValue25（按原顺序缓存的实参行的变换）：从行的变换读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。
global::UnityEngine.Transform argumentValue25 = row.transform;
        // argumentValue26（按原顺序缓存的实参条目集合的标签）：从条目集合的标签读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。
string argumentValue26 = entries[index].label;
        // argumentValue27（按原顺序缓存的实参27）：从27读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。
int argumentValue27 = 27;
        // argumentValue28（按原顺序缓存的实参文字锚点的中间左侧）：从文字锚点的中间左侧读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。
global::UnityEngine.TextAnchor argumentValue28 = TextAnchor.MiddleLeft;
        // selectedValue23（分支选出的普通状态颜色）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
global::UnityEngine.Color selectedValue23;
        if (entries[index].enabled)
        {
            selectedValue23 = normalColor;
        }
        else
        {
            selectedValue23 = disabledColor;
        }

        // label（标签）：保存 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的返回结果，供当前步骤使用。使用位置：作为 PauseMenu.SetAnchors(RectTransform, Vector2, Vector2) 的参数、PauseMenu.BuildMenuRow(Transform, int)。
Text label = CreateText(argumentValue24, argumentValue25, argumentValue26, argumentValue27, argumentValue28, selectedValue23);
        SetAnchors(label.rectTransform, new Vector2(0.08f, 0f), new Vector2(0.96f, 1f));
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 18;
        label.resizeTextMaxSize = 27;
        menuItems[index] = label;
        rowBackgrounds.Add(background);
        rowRects.Add(rect);
    }

    /// <summary>BuildConfirmDialog 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、PauseMenu.BindConfirmPointer(Button, int)、PauseMenu.CreateButton(string, Transform, string) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、PauseMenu.BindConfirmPointer(Button, int)、PauseMenu.CreateButton(string, Transform, string) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildConfirmDialog()
    {
        confirmDialog = CreateImage("退出确认弹窗", transform, new Color(0.002f, 0.002f, 0.002f, 0.998f)).gameObject;
        SetAnchors(confirmDialog.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        confirmTitle = CreateText("标题", confirmDialog.transform, "返回标题画面", 22, TextAnchor.MiddleCenter, Color.white);
        SetAnchors(confirmTitle.rectTransform, new Vector2(0.2f, 0.58f), new Vector2(0.8f, 0.63f));
        confirmMessage = CreateText("正文", confirmDialog.transform, string.Empty, 20, TextAnchor.MiddleCenter, normalColor);
        SetAnchors(confirmMessage.rectTransform, new Vector2(0.2f, 0.49f), new Vector2(0.8f, 0.58f));
        confirmYesButton = CreateButton("确认", confirmDialog.transform, "是");
        SetAnchors(confirmYesButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.39f), new Vector2(0.59f, 0.44f));
        confirmNoButton = CreateButton("取消", confirmDialog.transform, "否");
        SetAnchors(confirmNoButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.33f), new Vector2(0.59f, 0.38f));
        OriginalMenuPresentation.ApplyConfirmation(confirmDialog.transform, visualTheme, interfaceFont);
        confirmYesButton.onClick.AddListener(OnConfirmYes);
        confirmNoButton.onClick.AddListener(OnConfirmNo);
        BindConfirmPointer(confirmYesButton, 0);
        BindConfirmPointer(confirmNoButton, 1);
        confirmDialog.SetActive(false);
    }

    /// <summary>BindConfirmPointer 的业务处理入口；本方法直接依赖 EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、List&lt;EventTrigger.Entry&gt;.Add(EventTrigger.Entry)、PauseMenu.PlayHoverSound()、PauseMenu.UpdateConfirmSelection()、UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildConfirmDialog()；下游（含内部回调体） EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、List&lt;EventTrigger.Entry&gt;.Add(EventTrigger.Entry)、PauseMenu.PlayHoverSound()、PauseMenu.UpdateConfirmSelection()、UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="button">中文含义：按钮。使用位置：PauseMenu.BindConfirmPointer(Button, int)。</param>
    /// <param name="index">中文含义：索引。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数。</param>
    void BindConfirmPointer(Button button, int index)
    {
        if (button == null)
        {
            return;
        }

        // trigger（触发器）：保存 GameObject.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.BindConfirmPointer(Button, int)。
EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        // enter（进入）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、PauseMenu.BindConfirmPointer(Button, int)。
EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            if (!isVisible || !interactionEnabled || !isConfirming)
            {
                return;
            }

            confirmSelection = index;
            PlayHoverSound();
            UpdateConfirmSelection();
        });
        trigger.triggers.Add(enter);
    }

    /// <summary>BuildSubpage 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、PauseMenu.CreateButton(string, Transform, string)、PauseMenu.CreateImage(string, Transform, Color)、PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、PauseMenu.CreateButton(string, Transform, string)、PauseMenu.CreateImage(string, Transform, Color)、PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildSubpage()
    {
        subpagePanel = CreateImage("信息子页面", transform, new Color(0.025f, 0.028f, 0.035f, 0.97f)).gameObject;
        SetAnchors(subpagePanel.GetComponent<RectTransform>(), new Vector2(0.36f, 0.2f), new Vector2(0.82f, 0.8f));
        subpageTitle = CreateText("标题", subpagePanel.transform, string.Empty, 30, TextAnchor.MiddleLeft, Color.white);
        SetAnchors(subpageTitle.rectTransform, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.93f));
        subpageBody = CreateText("正文", subpagePanel.transform, string.Empty, 21, TextAnchor.UpperLeft, normalColor);
        SetAnchors(subpageBody.rectTransform, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.78f));
        subpageBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        subpageBody.verticalOverflow = VerticalWrapMode.Overflow;
        subpageCloseButton = CreateButton("关闭", subpagePanel.transform, "返回");
        SetAnchors(subpageCloseButton.GetComponent<RectTransform>(), new Vector2(0.7f, 0.055f), new Vector2(0.92f, 0.14f));
        subpageCloseButton.onClick.AddListener(CloseSubpage);
        subpagePanel.SetActive(false);
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildConfirmDialog()、PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.BuildSubpage()、PauseMenu.CreateButton(string, Transform, string)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="alignment">中文含义：对齐状态。使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。</param>
    Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment, Color color)
    {
        // go（当前游戏对象）：使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)。
Text text = go.GetComponent<Text>();
        text.font = interfaceFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildConfirmDialog()、PauseMenu.BuildInterface()、PauseMenu.BuildSubpage()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：PauseMenu.CreateImage(string, Transform, Color)。</param>
    Image CreateImage(string name, Transform parent, Color color)
    {
        // go（当前游戏对象）：使用位置：PauseMenu.CreateImage(string, Transform, Color)。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.CreateImage(string, Transform, Color)。
Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateRawImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RawImage&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RawImage&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    RawImage CreateRawImage(string name, Transform parent)
    {
        // go（当前游戏对象）：使用位置：PauseMenu.CreateRawImage(string, Transform)。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RawImage>();
    }

    /// <summary>CreateButton 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)、PauseMenu.Stretch(RectTransform)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildConfirmDialog()、PauseMenu.BuildSubpage()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color)、PauseMenu.Stretch(RectTransform)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="label">中文含义：标签。使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数。</param>
    Button CreateButton(string name, Transform parent, string label)
    {
        // go（当前游戏对象）：使用位置：作为 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的参数、PauseMenu.CreateButton(string, Transform, string)。
GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.CreateButton(string, Transform, string)。
Image image = go.GetComponent<Image>();
        // assignmentReceiver31（赋值前保存的目标图像）：从图像读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.CreateButton(string, Transform, string)。
global::UnityEngine.UI.Image assignmentReceiver31 = image;
        if (selectedRowSprite != null)
        {
            assignmentReceiver31.color = new Color(1f, 1f, 1f, 0.72f);
        }
        else
        {
            assignmentReceiver31.color = new Color(1f, 1f, 1f, 0.08f);
        }

        image.sprite = selectedRowSprite;
        // assignmentReceiver33（赋值前保存的目标图像）：从图像读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PauseMenu.CreateButton(string, Transform, string)。
global::UnityEngine.UI.Image assignmentReceiver33 = image;
        if (selectedRowSprite != null)
        {
            assignmentReceiver33.type = Image.Type.Sliced;
        }
        else
        {
            assignmentReceiver33.type = Image.Type.Simple;
        }

        // button（按钮）：保存 GameObject.GetComponent<Button>() 的返回结果，供当前步骤使用。使用位置：PauseMenu.CreateButton(string, Transform, string)。
Button button = go.GetComponent<Button>();
        // colors（颜色集合）：使用位置：PauseMenu.CreateButton(string, Transform, string)。
ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.88f, 0.78f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        // text（文字）：保存 PauseMenu.CreateText(string, Transform, string, int, TextAnchor, Color) 的返回结果，供当前步骤使用。使用位置：作为 PauseMenu.Stretch(RectTransform) 的参数。
Text text = CreateText("文字", go.transform, label, 19, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
        return button;
    }

    /// <summary>SetAnchors 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildConfirmDialog()、PauseMenu.BuildInterface()、PauseMenu.BuildMenuRow(Transform, int)、PauseMenu.BuildSubpage()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：PauseMenu.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="min">中文含义：最小。使用位置：PauseMenu.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="max">中文含义：最大。使用位置：PauseMenu.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BuildInterface()、PauseMenu.CreateButton(string, Transform, string)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：PauseMenu.Stretch(RectTransform)。</param>
    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>PlayHoverSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.BindConfirmPointer(Button, int)、PauseMenu.HandleMouseInput()、PauseMenu.Navigate(int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayHoverSound()
    {
        // receiverValue35（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PauseMenu.PlayHoverSound()。
global::AudioManager receiverValue35 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue35, null))
        {
            receiverValue35.PlaySfx(AudioEventNames.UIHover);
        }
    }

    /// <summary>PlayConfirmSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ExecuteSelected()；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayConfirmSound()
    {
        // receiverValue36（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PauseMenu.PlayConfirmSound()。
global::AudioManager receiverValue36 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue36, null))
        {
            receiverValue36.PlaySfx(AudioEventNames.UIConfirm);
        }
    }
}
