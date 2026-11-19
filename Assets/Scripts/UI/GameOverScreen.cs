using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏死亡结算画面：红色闪现、暗化、结算数据、重新挑战与返回标题。
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    /// <summary>中文含义：主要面板。使用位置：GameOverScreen.BuildInterface()、GameOverScreen.GameOverSequence()、GameOverScreen.Hide() 等共 6 处。</summary>
    [Header("面板引用")]
    public CanvasGroup mainPanel;
    /// <summary>中文含义：深色叠加层。使用位置：作为 GameOverScreen.SetImageAlpha(Image, float) 的参数、作为 GameOverScreen.Stretch(RectTransform) 的参数、GameOverScreen.BuildInterface() 等共 4 处。</summary>
    public Image darkOverlay;
    /// <summary>中文含义：红色闪光叠加层。使用位置：作为 GameOverScreen.SetImageAlpha(Image, float) 的参数、作为 GameOverScreen.Stretch(RectTransform) 的参数、GameOverScreen.BuildInterface() 等共 5 处。</summary>
    public Image redFlashOverlay;
    /// <summary>中文含义：当前用户处于死亡文字。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 GameOverScreen.SetTextAlpha(Text, float) 的参数、GameOverScreen.BuildInterface() 等共 4 处。</summary>
    public Text youAreDeadText;
    /// <summary>中文含义：结果文字。使用位置：作为 GameOverScreen.FadeText(Text, float, float) 的参数、作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 GameOverScreen.SetTextAlpha(Text, float) 的参数 等共 5 处。</summary>
    public Text resultText;
    /// <summary>中文含义：操作提示栏文字。使用位置：作为 GameOverScreen.FadeText(Text, float, float) 的参数、作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、作为 GameOverScreen.SetTextAlpha(Text, float) 的参数 等共 4 处。</summary>
    public Text hintText;
    /// <summary>中文含义：选项文字集合。使用位置：作为 GameOverScreen.SetTextAlpha(Text, float) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、GameOverScreen.BuildInterface() 等共 7 处。</summary>
    public Text[] optionTexts;
    /// <summary>中文含义：选择指示器。使用位置：GameOverScreen.BuildInterface()、GameOverScreen.ResetVisualState()、GameOverScreen.UpdateSelection()。</summary>
    [Header("指示条")]
    public RectTransform selectionIndicator;
    /// <summary>中文含义：留空时从资源目录读取默认配置。优先使用集中配置；留空时从资源目录读取默认配置。使用位置：GameOverScreen.ResolveVisualAssets()。</summary>
    [Header("视觉资源")]
    [Tooltip("优先使用集中配置；留空时从资源目录读取默认配置。")]
    public GameOverVisualConfig visualConfig;
    /// <summary>中文含义：死亡标题字体，默认与开始界面标题一致。使用位置：作为 GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color) 的参数、GameOverScreen.ResolveVisualAssets()。</summary>
    [Tooltip("死亡标题字体，默认与开始界面标题一致。")]
    public Font titleFont;
    /// <summary>中文含义：接口字体。使用位置：作为 GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color) 的参数、GameOverScreen.ResolveVisualAssets()。</summary>
    public Font interfaceFont;
    /// <summary>中文含义：按钮精灵图。使用位置：GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)、GameOverScreen.FadeOption(int, float)、GameOverScreen.ResolveVisualAssets() 等共 4 处。</summary>
    public Sprite buttonSprite;
    /// <summary>中文含义：红色闪光持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。</summary>
    [Header("动效参数")]
    public float redFlashDuration = 0.4f;
    /// <summary>中文含义：变暗延迟。使用位置：作为 WaitForSecondsRealtime.WaitForSecondsRealtime(float) 的参数。</summary>
    public float darkenDelay = 0.3f;
    /// <summary>中文含义：变暗持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。</summary>
    public float darkenDuration = 0.6f;
    /// <summary>中文含义：文字显露持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。</summary>
    public float textRevealDuration = 1.2f;
    /// <summary>中文含义：选项集合延迟。使用位置：作为 WaitForSecondsRealtime.WaitForSecondsRealtime(float) 的参数。</summary>
    public float optionsDelay = 1.5f;
    /// <summary>中文含义：选定颜色。使用位置：作为 GameOverScreen.CreateImage(string, Transform, Color) 的参数。</summary>
    [Header("颜色")]
    public Color selectedColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>中文含义：普通状态颜色。使用位置：GameOverScreen.UpdateSelection()。</summary>
    public Color normalColor = new Color(1f, 1f, 1f, 0.72f);
    /// <summary>中文含义：深色颜色。使用位置：作为 Color.Color(float, float, float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、GameOverScreen.GameOverSequence()。</summary>
    public Color darkColor = new Color(0.01f, 0.012f, 0.016f, 0.9f);
    /// <summary>中文含义：选项按钮集合。使用位置：作为 GameOverScreen.CreateImage(string, Transform, Color) 的参数、作为 GameOverScreen.SetImageAlpha(Image, float) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 8 处。</summary>
    private RawImage deathTitleFilm;
    private const int DeathTitleColumns = 6;
    private const int DeathTitleRows = 8;
    private const int DeathTitleFrames = DeathTitleColumns * DeathTitleRows;
    private Button[] optionButtons = Array.Empty<Button>();
    /// <summary>中文含义：选定索引。使用位置：作为 GameOverScreen.ExecuteOption(int) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数 等共 7 处。</summary>
    private int selectedIndex;
    /// <summary>中文含义：是否可以交互。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、GameOverScreen.ExecuteOption(int)、GameOverScreen.GameOverSequence() 等共 6 处。</summary>
    private bool canInteract;
    /// <summary>中文含义：是否生效。使用位置：GameOverScreen.Hide()、GameOverScreen.Show()、GameOverScreen.Start() 等共 4 处。</summary>
    private bool isActive;
    /// <summary>中文含义：是否接口已构建。使用位置：GameOverScreen.BuildInterface()、GameOverScreen.Show()。</summary>
    private bool interfaceBuilt;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、GameOverScreen.BuildInterface()、GameOverScreen.ResetVisualState()、GameOverScreen.ResolveVisualAssets()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        ResolveVisualAssets();
        BuildInterface();
        if (isActive)
        {
            return;
        }

        ResetVisualState();
        if (mainPanel != null)
        {
            mainPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>触发展示死亡结算画面。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.OnPlayerDied()；下游（含内部回调体） GameObject.SetActive(bool)、GameOverScreen.BuildInterface()、GameOverScreen.GameOverSequence()、GameOverScreen.ResetVisualState()、GameOverScreen.ResolveVisualAssets()、GameOverScreen.UpdateResultText()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        if (isActive)
        {
            return;
        }

        if (GameUIManager.HasInstance && GameUIManager.Instance.currentState != GameUIManager.State.GameOver)
        {
            GameUIManager.Instance.RegisterModal(this, GameUIManager.State.GameOver, GameUIManager.Instance.pauseOnGameOver, true, true);
        }

        if (!interfaceBuilt)
        {
            ResolveVisualAssets();
            BuildInterface();
        }

        isActive = true;
        selectedIndex = 0;
        transform.SetAsLastSibling();
        if (mainPanel != null)
        {
            mainPanel.gameObject.SetActive(true);
        }

        ResetVisualState();
        UpdateResultText();
        // receiverValue1（判空前缓存的界面光标管理器的实例）：从界面光标管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameOverScreen.Show()。
global::UICursorManager receiverValue1 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.ShowCustomCursor();
        }

        StartCoroutine(GameOverSequence());
    }

    /// <summary>隐藏死亡结算画面。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、MonoBehaviour.StopAllCoroutines()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        isActive = false;
        canInteract = false;
        StopAllCoroutines();
        if (mainPanel != null)
        {
            mainPanel.gameObject.SetActive(false);
        }

        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.UnregisterModal(this);
        }
    }

    /// <summary>ResolveVisualAssets 的业务处理入口；本方法直接依赖 Resources.GetBuiltinResource&lt;Font&gt;(string)、Resources.Load&lt;GameOverVisualConfig&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()、GameOverScreen.Start()；下游（含内部回调体） Resources.GetBuiltinResource&lt;Font&gt;(string)、Resources.Load&lt;GameOverVisualConfig&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolveVisualAssets()
    {
        if (visualConfig == null)
        {
            visualConfig = Resources.Load<GameOverVisualConfig>("UI/死亡结算视觉配置");
        }

        if (visualConfig != null)
        {
            if (titleFont == null)
            {
                titleFont = visualConfig.titleFont;
            }

            if (interfaceFont == null)
            {
                interfaceFont = visualConfig.interfaceFont;
            }

            if (buttonSprite == null)
            {
                buttonSprite = visualConfig.buttonSprite;
            }
        }

        if (interfaceFont == null && optionTexts != null)
        {
            // i（当前遍历索引）：使用位置：GameOverScreen.ResolveVisualAssets()。
for (int i = 0; i < optionTexts.Length; i++)
            {
                if (optionTexts[i] == null || optionTexts[i].font == null)
                {
                    continue;
                }

                interfaceFont = optionTexts[i].font;
                break;
            }
        }

        if (interfaceFont == null)
        {
            interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (titleFont == null)
        {
            titleFont = interfaceFont;
        }
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;Outline&gt;()、GameObject.SetActive(bool)、GameOverScreen.CreateImage(string, Transform, Color) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()、GameOverScreen.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;Outline&gt;()、GameObject.SetActive(bool)、GameOverScreen.CreateImage(string, Transform, Color) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildInterface()
    {
        if (interfaceBuilt)
        {
            return;
        }

        interfaceBuilt = true;
        if (mainPanel == null)
        {
            mainPanel = GetComponent<CanvasGroup>();
        }

        if (mainPanel == null)
        {
            mainPanel = gameObject.AddComponent<CanvasGroup>();
        }

        // root（根界面矩形变换）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 GameOverScreen.Stretch(RectTransform) 的参数、GameOverScreen.BuildInterface()。
RectTransform root = GetComponent<RectTransform>();
        if (root != null)
        {
            Stretch(root);
        }

        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、GameOverScreen.BuildInterface()。
for (int i = transform.childCount - 1; i >= 0; i--)
        {
            // child（子级）：使用位置：作为 Object.Destroy(Object) 的参数、GameOverScreen.BuildInterface()。
GameObject child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        darkOverlay = CreateImage("死亡暗幕", transform, new Color(darkColor.r, darkColor.g, darkColor.b, 0f));
        Stretch(darkOverlay.rectTransform);
        darkOverlay.raycastTarget = true;
        // leftShade（左侧阴影色）：保存 GameOverScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数。
Image leftShade = CreateImage("左侧渐暗", transform, new Color(0f, 0f, 0f, 0.38f));
        SetAnchors(leftShade.rectTransform, new Vector2(0f, 0f), new Vector2(0.28f, 1f));
        // rightShade（右侧阴影色）：保存 GameOverScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数。
Image rightShade = CreateImage("右侧渐暗", transform, new Color(0f, 0f, 0f, 0.38f));
        SetAnchors(rightShade.rectTransform, new Vector2(0.72f, 0f), new Vector2(1f, 1f));
        redFlashOverlay = CreateImage("受击红色闪现", transform, new Color(0.55f, 0f, 0f, 0f));
        Stretch(redFlashOverlay.rectTransform);
        redFlashOverlay.raycastTarget = false;
        youAreDeadText = CreateText("死亡标题", transform, "YOU ARE DEAD", titleFont, 92, TextAnchor.MiddleCenter, new Color(0.78f, 0.06f, 0.045f, 0f));
        SetAnchors(youAreDeadText.rectTransform, new Vector2(0.16f, 0.62f), new Vector2(0.84f, 0.81f));
        youAreDeadText.horizontalOverflow = HorizontalWrapMode.Overflow;
        youAreDeadText.verticalOverflow = VerticalWrapMode.Overflow;
        Texture2D titleFilm = Resources.Load<Texture2D>("UI/DeathTitleFilm");
        if (titleFilm != null)
        {
            var film = new GameObject("死亡标题原版动效", typeof(RectTransform), typeof(RawImage));
            film.transform.SetParent(transform, false);
            deathTitleFilm = film.GetComponent<RawImage>();
            deathTitleFilm.texture = titleFilm;
            deathTitleFilm.raycastTarget = false;
            deathTitleFilm.enabled = false;
            // 取样裁切区在参考画面中的位置。
            SetAnchors(deathTitleFilm.rectTransform, new Vector2(.1875f, .535185f), new Vector2(.8125f, .775926f));
            youAreDeadText.enabled = false;
        }
        // outline（轮廓）：保存 GameObject.AddComponent<Outline>() 的返回结果，供当前步骤使用。使用位置：GameOverScreen.BuildInterface()。
Outline outline = youAreDeadText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(4f, -4f);
        // divider（分隔线）：保存 GameOverScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数。
Image divider = CreateImage("标题分隔线", transform, new Color(0.7f, 0.07f, 0.05f, 0.78f));
        SetAnchors(divider.rectTransform, new Vector2(0.28f, 0.59f), new Vector2(0.72f, 0.594f));
        if (deathTitleFilm != null)
        {
            divider.gameObject.SetActive(false);
        }
        resultText = CreateText("本局结算", transform, string.Empty, interfaceFont, 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
        SetAnchors(resultText.rectTransform, new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.56f));
        resultText.lineSpacing = 1.25f;
        optionButtons = new Button[2];
        optionTexts = new Text[2];
        optionButtons[0] = CreateOptionButton("重新挑战按钮", "重新挑战", 0, new Vector2(0.31f, 0.115f), new Vector2(0.49f, 0.177f));
        optionButtons[1] = CreateOptionButton("返回标题按钮", "返回标题画面", 1, new Vector2(0.51f, 0.115f), new Vector2(0.69f, 0.177f));
        // indicator（指示器）：保存 GameOverScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：GameOverScreen.BuildInterface()。
Image indicator = CreateImage("选择指示线", optionButtons[0].transform, selectedColor);
        selectionIndicator = indicator.rectTransform;
        selectionIndicator.anchorMin = new Vector2(0f, 0.12f);
        selectionIndicator.anchorMax = new Vector2(0f, 0.88f);
        selectionIndicator.pivot = new Vector2(0f, 0.5f);
        selectionIndicator.anchoredPosition = Vector2.zero;
        selectionIndicator.sizeDelta = new Vector2(5f, 0f);
        hintText = CreateText("操作提示", transform, string.Empty, interfaceFont, 16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
        SetAnchors(hintText.rectTransform, new Vector2(0.34f, 0.07f), new Vector2(0.66f, 0.12f));
    }

    /// <summary>CreateOptionButton 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;() 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.BuildInterface()；下游（含内部回调体） Color.Color(float, float, float, float)、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="label">中文含义：标签。使用位置：作为 GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color) 的参数。</param>
    /// <param name="index">中文含义：索引。使用位置：GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。</param>
    /// <param name="anchorMin">中文含义：锚点最小。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数。</param>
    /// <param name="anchorMax">中文含义：锚点最大。使用位置：作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数。</param>
    Button CreateOptionButton(string objectName, string label, int index, Vector2 anchorMin, Vector2 anchorMax)
    {
        // go（当前游戏对象）：使用位置：作为 GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color) 的参数、作为 GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。
GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。
Image image = go.GetComponent<Image>();
        image.color = Color.clear;
        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.interactable = false;
        // labelText（标签文字）：保存 GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color) 的返回结果，供当前步骤使用。使用位置：作为 GameOverScreen.Stretch(RectTransform) 的参数、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。
Text labelText = CreateText("文字", go.transform, label, interfaceFont, 23, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
        optionTexts[index] = labelText;
        ConfigureTerminalOption(go.transform, labelText, index);
        // capturedIndex（已捕获索引）：使用位置：作为 GameOverScreen.ExecuteOption(int) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
int capturedIndex = index;
        button.onClick.AddListener(() => ExecuteOption(capturedIndex));
        // trigger（触发器）：保存 GameObject.AddComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。
EventTrigger trigger = go.AddComponent<EventTrigger>();
        // enter（进入）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)。
EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enter.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            if (!canInteract || selectedIndex == capturedIndex)
            {
                return;
            }

            selectedIndex = capturedIndex;
            PlayHoverSound();
            UpdateSelection();
        });
        trigger.triggers.Add(enter);
        return button;
    }

    /// <summary>GameOverSequence 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameOverScreen.FadeOption(int, float)、GameOverScreen.FadeText(Text, float, float)、GameOverScreen.UpdateSelection()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()；下游（含内部回调体） GameObject.SetActive(bool)、GameOverScreen.FadeOption(int, float)、GameOverScreen.FadeText(Text, float, float)、GameOverScreen.UpdateSelection()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator GameOverSequence()
    {
        if (redFlashOverlay != null)
        {
            redFlashOverlay.gameObject.SetActive(true);
            // flash（闪光）：使用位置：GameOverScreen.GameOverSequence()。
Color flash = redFlashOverlay.color;
            // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。
float elapsed = 0f;
            while (elapsed < redFlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、GameOverScreen.GameOverSequence()。
float progress = Mathf.Clamp01(elapsed / redFlashDuration);
                if (progress < 0.3f)
                {
                    flash.a = Mathf.Lerp(0f, 0.72f, progress / 0.3f);
                }
                else
                {
                    flash.a = Mathf.Lerp(0.72f, 0f, (progress - 0.3f) / 0.7f);
                }

                redFlashOverlay.color = flash;
                yield return null;
            }

            flash.a = 0f;
            redFlashOverlay.color = flash;
            redFlashOverlay.gameObject.SetActive(false);
        }

        yield return new WaitForSecondsRealtime(darkenDelay);
        if (darkOverlay != null)
        {
            // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。
float elapsed = 0f;
            // dark（深色）：使用位置：GameOverScreen.GameOverSequence()。
Color dark = darkOverlay.color;
            while (elapsed < darkenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、GameOverScreen.GameOverSequence()。
float progress = Mathf.Clamp01(elapsed / darkenDuration);
                progress = 1f - Mathf.Pow(1f - progress, 3f);
                dark.a = Mathf.Lerp(0f, darkColor.a, progress);
                darkOverlay.color = dark;
                yield return null;
            }

            dark.a = darkColor.a;
            darkOverlay.color = dark;
        }

        if (deathTitleFilm != null)
        {
            deathTitleFilm.enabled = true;
            float start = Time.unscaledTime;
            int frame = 0;
            while (frame < DeathTitleFrames - 1)
            {
                frame = Mathf.Min(DeathTitleFrames - 1, Mathf.FloorToInt((Time.unscaledTime - start) * 24f));
                float x = (frame % DeathTitleColumns) / (float)DeathTitleColumns;
                float y = (DeathTitleRows - 1 - frame / DeathTitleColumns) / (float)DeathTitleRows;
                deathTitleFilm.uvRect = new Rect(x, y, 1f / DeathTitleColumns, 1f / DeathTitleRows);
                yield return null;
            }
        }
        else if (youAreDeadText != null)
        {
            // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.GameOverSequence()。
float elapsed = 0f;
            // titleRect（标题矩形）：使用位置：GameOverScreen.GameOverSequence()。
RectTransform titleRect = youAreDeadText.rectTransform;
            while (elapsed < textRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、GameOverScreen.GameOverSequence()。
float progress = Mathf.Clamp01(elapsed / textRevealDuration);
                if (progress < 0.5f)
                {
                    progress = 2f * progress * progress;
                }
                else
                {
                    progress = 1f - Mathf.Pow(-2f * progress + 2f, 2f) / 2f;
                }

                // titleColor（标题颜色）：使用位置：GameOverScreen.GameOverSequence()。
Color titleColor = youAreDeadText.color;
                titleColor.a = progress;
                youAreDeadText.color = titleColor;
                titleRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, progress);
                yield return null;
            }
        }

        yield return FadeText(resultText, 0.45f, 0.82f);
        yield return new WaitForSecondsRealtime(optionsDelay);
        // i（当前遍历索引）：使用位置：作为 GameOverScreen.FadeOption(int, float) 的参数、GameOverScreen.GameOverSequence()。
for (int i = 0; i < optionButtons.Length; i++)
        {
            yield return FadeOption(i, 0.18f);
        }

        yield return FadeText(hintText, 0.2f, 0.55f);
        selectedIndex = 0;
        canInteract = true;
        mainPanel.interactable = true;
        // i（当前遍历索引）：使用位置：GameOverScreen.GameOverSequence()。
for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].interactable = true;
        }

        UpdateSelection();
    }

    /// <summary>FadeOption 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.GameOverSequence()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：GameOverScreen.FadeOption(int, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.FadeOption(int, float)。</param>
    IEnumerator FadeOption(int index, float duration)
    {
        if (index < 0 || index >= optionButtons.Length)
        {
            yield break;
        }

        Text label = optionTexts[index];
        CanvasGroup option = optionButtons[index].GetComponent<CanvasGroup>();
        label.color = Color.white;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            option.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        option.alpha = 1f;
    }

    /// <summary>FadeText 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.GameOverSequence()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：GameOverScreen.FadeText(Text, float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.FadeText(Text, float, float)。</param>
    /// <param name="targetAlpha">中文含义：目标透明度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</param>
    IEnumerator FadeText(Text text, float duration, float targetAlpha)
    {
        if (text == null)
        {
            yield break;
        }

        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、GameOverScreen.FadeText(Text, float, float)。
float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // color（颜色）：使用位置：GameOverScreen.FadeText(Text, float, float)。
Color color = text.color;
            color.a = Mathf.Lerp(0f, targetAlpha, Mathf.Clamp01(elapsed / duration));
            text.color = color;
            yield return null;
        }
    }

    /// <summary>ResetVisualState 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameOverScreen.SetImageAlpha(Image, float)、GameOverScreen.SetTextAlpha(Text, float)、MonoBehaviour.StopAllCoroutines()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()、GameOverScreen.Start()；下游（含内部回调体） GameObject.SetActive(bool)、GameOverScreen.SetImageAlpha(Image, float)、GameOverScreen.SetTextAlpha(Text, float)、MonoBehaviour.StopAllCoroutines()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResetVisualState()
    {
        StopAllCoroutines();
        canInteract = false;
        if (mainPanel != null)
        {
            mainPanel.alpha = 1f;
            mainPanel.interactable = false;
            mainPanel.blocksRaycasts = true;
        }

        SetImageAlpha(darkOverlay, 0f);
        SetImageAlpha(redFlashOverlay, 0f);
        if (redFlashOverlay != null)
        {
            redFlashOverlay.gameObject.SetActive(false);
        }

        SetTextAlpha(youAreDeadText, 0f);
        if (deathTitleFilm != null)
        {
            deathTitleFilm.enabled = false;
        }
        SetTextAlpha(resultText, 0f);
        SetTextAlpha(hintText, 0f);
        // i（当前遍历索引）：使用位置：GameOverScreen.ResetVisualState()。
for (int i = 0; i < optionButtons.Length; i++)
        {
            optionButtons[i].interactable = false;
            optionButtons[i].GetComponent<CanvasGroup>().alpha = 0f;
            SetTextAlpha(optionTexts[i], 0f);
        }

        if (selectionIndicator != null)
        {
            selectionIndicator.gameObject.SetActive(false);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameOverScreen.HandleKeyboardInput()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isActive || !canInteract)
        {
            return;
        }

        HandleKeyboardInput();
    }

    /// <summary>HandleKeyboardInput 的业务处理入口；本方法直接依赖 GameOverScreen.ExecuteOption(int)、GameOverScreen.Navigate(int)、Input.GetKeyDown(KeyCode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Update()；下游（含内部回调体） GameOverScreen.ExecuteOption(int)、GameOverScreen.Navigate(int)、Input.GetKeyDown(KeyCode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ExecuteOption(0);
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExecuteOption(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            ExecuteOption(selectedIndex);
        }
    }

    /// <summary>Navigate 的业务处理入口；本方法直接依赖 GameOverScreen.PlayHoverSound()、GameOverScreen.UpdateSelection()、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.HandleKeyboardInput()；下游（含内部回调体） GameOverScreen.PlayHoverSound()、GameOverScreen.UpdateSelection()、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</param>
    void Navigate(int direction)
    {
        // next（下一次）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：GameOverScreen.Navigate(int)。
int next = Mathf.Clamp(selectedIndex + direction, 0, optionTexts.Length - 1);
        if (next == selectedIndex)
        {
            return;
        }

        selectedIndex = next;
        PlayHoverSound();
        UpdateSelection();
    }

    /// <summary>UpdateSelection 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)、GameOverScreen.GameOverSequence()、GameOverScreen.Navigate(int)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSelection()
    {
        for (int index = 0; index < optionTexts.Length; index++)
        {
            Text label = optionTexts[index];
            label.color = normalColor;
            if (index == selectedIndex)
            {
                label.color = Color.white;
            }
        }
    }

    internal static void ConfigureTerminalOption(Transform button, Text label, int index)
    {
        button.gameObject.AddComponent<CanvasGroup>();
        string glyphName = "Key_Space";
        if (index == 1)
        {
            glyphName = "Key_Escape";
        }

        Texture2D texture = Resources.Load<Texture2D>("NativeInteractionArt/" + glyphName);
        if (texture != null)
        {
            GameObject holder = new GameObject("键位范围", typeof(RectTransform));
            holder.transform.SetParent(button, false);
            SetAnchors(holder.GetComponent<RectTransform>(), new Vector2(0.04f, 0.23f), new Vector2(0.30f, 0.77f));
            GameObject glyph = new GameObject("原版键位", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            glyph.transform.SetParent(holder.transform, false);
            RawImage image = glyph.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            AspectRatioFitter aspect = glyph.GetComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = (float)texture.width / texture.height;
        }

        label.alignment = TextAnchor.MiddleLeft;
        SetAnchors(label.rectTransform, new Vector2(0.35f, 0f), Vector2.one);
    }

    /// <summary>UpdateResultText 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)、TimeSpan.FromSeconds(double)、TimeSpan.ToString(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()；下游（含内部回调体） Mathf.Max(float, float)、MissionProgressSnapshot.MissionProgressSnapshot(int, int, int, int, float, float, bool, bool)、TimeSpan.FromSeconds(double)、TimeSpan.ToString(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateResultText()
    {
        if (resultText == null)
        {
            return;
        }

        // progress（进度）：使用位置：作为 Mathf.Max(float, float) 的参数、GameOverScreen.UpdateResultText()。
MissionProgressSnapshot progress;
        if (ChapterOneMissionManager.HasInstance)
        {
            progress = ChapterOneMissionManager.Instance.LatestSnapshot;
        }
        else
        {
            progress = new MissionProgressSnapshot(0, 15, 0, 2, 0f, 240f, false, false);
        }

        // elapsed（已用）：保存 TimeSpan.FromSeconds(double) 的返回结果，供当前步骤使用。使用位置：GameOverScreen.UpdateResultText()。
TimeSpan elapsed = TimeSpan.FromSeconds(Mathf.Max(0f, progress.ElapsedSeconds));
        resultText.text = "本局结算\n" + "普通敌人  " + progress.GanadoKills + "/" + progress.GanadoTarget + "     电锯敌人  " + progress.SawmanKills + "/" + progress.SawmanTarget + "     时间  " + elapsed.ToString(@"mm\:ss");
    }

    /// <summary>ExecuteOption 的业务处理入口；本方法直接依赖 Application.CanStreamedLevelBeLoaded(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameOverScreen.PlayConfirmSound()、GameUIManager.ClearAllModals()、SceneManager.GetActiveScene()、SceneManager.LoadScene(int) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)、GameOverScreen.HandleKeyboardInput()；下游（含内部回调体） Application.CanStreamedLevelBeLoaded(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameOverScreen.PlayConfirmSound()、GameUIManager.ClearAllModals()、SceneManager.GetActiveScene()、SceneManager.LoadScene(int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：GameOverScreen.ExecuteOption(int)。</param>
    void ExecuteOption(int index)
    {
        if (!canInteract)
        {
            return;
        }

        canInteract = false;
        PlayConfirmSound();
        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.ClearAllModals();
        }

        GameManager.ResetScenePermission();
        if (index == 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (SceneManager.sceneCountInBuildSettings > 0)
        {
            SceneManager.LoadScene(0);
        }
        else if (Application.CanStreamedLevelBeLoaded("TitleScene"))
        {
            SceneManager.LoadScene("TitleScene");
        }
        else
        {
            Debug.LogError("[死亡结算] 找不到可用的开始场景。");
        }
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：GameOverScreen.CreateImage(string, Transform, Color)。</param>
    Image CreateImage(string objectName, Transform parent, Color color)
    {
        // go（当前游戏对象）：使用位置：GameOverScreen.CreateImage(string, Transform, Color)。
GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：GameOverScreen.CreateImage(string, Transform, Color)。
Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.BuildInterface()、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。</param>
    /// <param name="font">中文含义：字体。使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：作为 Mathf.Max(int, int) 的参数、GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。</param>
    /// <param name="alignment">中文含义：对齐状态。使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。</param>
    Text CreateText(string objectName, Transform parent, string value, Font font, int size, TextAnchor alignment, Color color)
    {
        // go（当前游戏对象）：使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。
GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：GameOverScreen.CreateText(string, Transform, string, Font, int, TextAnchor, Color)。
Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, size - 24);
        text.resizeTextMaxSize = size;
        return text;
    }

    /// <summary>SetAnchors 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.BuildInterface()、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="min">中文含义：最小。使用位置：GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="max">中文含义：最大。使用位置：GameOverScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.BuildInterface()、GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：GameOverScreen.Stretch(RectTransform)。</param>
    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>SetImageAlpha 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.ResetVisualState()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="image">中文含义：图像。使用位置：GameOverScreen.SetImageAlpha(Image, float)。</param>
    /// <param name="alpha">中文含义：透明度。使用位置：GameOverScreen.SetImageAlpha(Image, float)。</param>
    void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        // color（颜色）：使用位置：GameOverScreen.SetImageAlpha(Image, float)。
Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    /// <summary>SetTextAlpha 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.ResetVisualState()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：GameOverScreen.SetTextAlpha(Text, float)。</param>
    /// <param name="alpha">中文含义：透明度。使用位置：GameOverScreen.SetTextAlpha(Text, float)。</param>
    void SetTextAlpha(Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        // color（颜色）：使用位置：GameOverScreen.SetTextAlpha(Text, float)。
Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    /// <summary>PlayHoverSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.CreateOptionButton(string, string, int, Vector2, Vector2)、GameOverScreen.Navigate(int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayHoverSound()
    {
        // receiverValue19（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameOverScreen.PlayHoverSound()。
global::AudioManager receiverValue19 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            receiverValue19.PlaySfx(AudioEventNames.UIHover);
        }
    }

    /// <summary>PlayConfirmSound 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.ExecuteOption(int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayConfirmSound()
    {
        // receiverValue20（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameOverScreen.PlayConfirmSound()。
global::AudioManager receiverValue20 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.PlaySfx(AudioEventNames.UIConfirm);
        }
    }
}
