using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>第一章通关结算画面。</summary>
public class MissionClearedScreen : MonoBehaviour
{
    /// <summary>主要面板。</summary>
    [Header("面板引用")]
    public CanvasGroup mainPanel;
    /// <summary>标题文字。</summary>
    public Text titleText;
    private static readonly int SmokeTimeProperty = Shader.PropertyToID("_SmokeTime");
    private RawImage prologueSmoke;
    private Material prologueSmokeMaterial;
    private float smokeStarted;
    private AudioSource prologueCue;
    /// <summary>路线文字。</summary>
    public Text routeText;
    /// <summary>结果文字。</summary>
    public Text resultText;
    /// <summary>操作提示栏文字。</summary>
    public Text hintText;
    /// <summary>选择指示器。</summary>
    public RectTransform selectionIndicator;
    /// <summary>标题字体。</summary>
    [Header("字体")]
    public Font titleFont;
    /// <summary>接口字体。</summary>
    public Font interfaceFont;
    [Header("视觉主题")]
    [Tooltip("用于读取章节加载背景图与统一字体。")]
    public UIVisualTheme visualTheme;
    /// <summary>显露持续时间。</summary>
    [Header("动效")]
    [Min(0.1f)]
    public float revealDuration = 0.65f;
    /// <summary>选项集合延迟。</summary>
    [Min(0f)]
    public float optionsDelay = 0.45f;
    /// <summary>选项按钮集合。</summary>
    private Button[] optionButtons = Array.Empty<Button>();
    /// <summary>选项文字集合。</summary>
    private Text[] optionTexts = Array.Empty<Text>();
    /// <summary>是否接口已构建。</summary>
    private bool interfaceBuilt;
    /// <summary>是否生效。</summary>
    private bool isActive;
    internal bool IsShowing => isActive;
    /// <summary>是否可以交互。</summary>
    private bool canInteract;
    /// <summary>选定索引。</summary>
    private int selectedIndex;
    private Image completionBackground;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、MissionClearedScreen.BuildInterface()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        BuildInterface();
        if (!isActive && mainPanel != null)
        {
            mainPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Input.GetKeyDown(KeyCode)、MissionClearedScreen.ExecuteOption(int)、MissionClearedScreen.Navigate(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (isActive && prologueSmoke != null && prologueSmoke.enabled)
        {
            UpdatePrologueSmoke();
        }

        if (!isActive || !canInteract)
        {
            return;
        }

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

    private void UpdatePrologueSmoke()
    {
        float elapsed = Time.unscaledTime - smokeStarted;
        float opacity = Mathf.SmoothStep(0f, 1f, elapsed / 1.5f);
        prologueSmoke.color = new Color(1f, 1f, 1f, opacity);
        prologueSmokeMaterial.SetFloat(SmokeTimeProperty, elapsed);
    }

    private void OnDestroy()
    {
        if (prologueSmokeMaterial != null)
        {
            Destroy(prologueSmokeMaterial);
        }
    }

    /// <summary>显示第一章通关结算。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.OnMissionCompleted(MissionResult)；下游（含内部回调体） GameObject.SetActive(bool)、MissionClearedScreen.BuildInterface()、MissionClearedScreen.RevealSequence()、MissionClearedScreen.UpdateResult(MissionResult)、MissionClearedScreen.UpdateSelection()、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前任务结果。</param>
    public void Show(MissionResult result, bool bellEnding = false)
    {
        if (isActive)
        {
            return;
        }

        if (!interfaceBuilt)
        {
            BuildInterface();
        }

        isActive = true;
        canInteract = false;
        selectedIndex = 0;
        transform.SetAsLastSibling();
        mainPanel.gameObject.SetActive(true);
        mainPanel.alpha = 0f;
        mainPanel.interactable = false;
        mainPanel.blocksRaycasts = true;
        UpdateResult(result);
        completionBackground.color = new Color(0.01f, 0.025f, 0.02f, 0.94f);
        if (bellEnding)
        {
            completionBackground.color = Color.black;
            if (prologueCue != null && prologueCue.clip != null)
            {
                prologueCue.Play();
            }
        }
        if (prologueSmoke != null)
        {
            prologueSmoke.enabled = bellEnding;
            prologueSmoke.color = Color.clear;
            smokeStarted = Time.unscaledTime;
            if (bellEnding)
            {
                UpdatePrologueSmoke();
            }
        }
        titleText.enabled = true;
        titleText.text = "第一章完成";
        titleText.alignment = TextAnchor.MiddleCenter;
        SetAnchors(titleText.rectTransform, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.84f));
        if (bellEnding)
        {
            titleText.text = "序章完";
            titleText.alignment = TextAnchor.MiddleRight;
            SetAnchors(titleText.rectTransform, new Vector2(0.62f, 0.205f), new Vector2(0.91f, 0.36f));
        }
        routeText.enabled = !bellEnding;
        resultText.enabled = !bellEnding;
        hintText.enabled = !bellEnding;
        UpdateSelection();
        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue1 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.ShowCustomCursor();
        }

        StartCoroutine(RevealSequence());
    }

    /// <summary>Hide 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、MonoBehaviour.StopAllCoroutines()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.UnregisterModal(Object)、MonoBehaviour.StopAllCoroutines()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        isActive = false;
        canInteract = false;
        StopAllCoroutines();
        if (prologueCue != null)
        {
            prologueCue.Stop();
        }
        if (mainPanel != null)
        {
            mainPanel.gameObject.SetActive(false);
        }

        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.UnregisterModal(this);
        }
    }

    /// <summary>RevealSequence 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;RectTransform&gt;()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、MissionClearedScreen.UpdateSelection()、WaitForSecondsRealtime.WaitForSecondsRealtime(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.Show(MissionResult)；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、MissionClearedScreen.UpdateSelection()、WaitForSecondsRealtime.WaitForSecondsRealtime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator RevealSequence()
    {
        // 已用。
float elapsed = 0f;
        // 矩形。
RectTransform rect = mainPanel.GetComponent<RectTransform>();
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 进度。
float progress = Mathf.Clamp01(elapsed / revealDuration);
            progress = 1f - Mathf.Pow(1f - progress, 3f);
            mainPanel.alpha = progress;
            if (rect != null)
            {
                rect.localScale = Vector3.one * Mathf.Lerp(0.97f, 1f, progress);
            }

            yield return null;
        }

        mainPanel.alpha = 1f;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        yield return new WaitForSecondsRealtime(optionsDelay);
        canInteract = true;
        mainPanel.interactable = true;
        // button（按钮）：每轮从选项按钮集合取得一个元素，仅代表当前这一项。使用位置：MissionClearedScreen.RevealSequence()。
foreach (Button button in optionButtons)
        {
            button.interactable = true;
        }

        UpdateSelection();
    }

    /// <summary>UpdateResult 的业务处理入口；本方法直接依赖 Mathf.FloorToInt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.Show(MissionResult)；下游（含内部回调体） Mathf.FloorToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前任务结果。</param>
    void UpdateResult(MissionResult result)
    {
        // 进度。
MissionProgressSnapshot progress = result.Progress;
        // 赋值前保存的目标路线文字。
global::UnityEngine.UI.Text assignmentReceiver2 = routeText;
        if (result.CompletionReason == MissionCompletionReason.Extermination)
        {
            assignmentReceiver2.text = "歼灭路线达成";
        }
        else
        {
            assignmentReceiver2.text = "生存路线达成";
        }

        // 已用。
int elapsed = Mathf.FloorToInt(progress.ElapsedSeconds);
        // 分钟。
int minutes = elapsed / 60;
        // 秒。
int seconds = elapsed % 60;
        resultText.text = $"用时  {minutes:00}:{seconds:00}\n" + $"普通敌人  {progress.GanadoKills}/{progress.GanadoTarget}\n" + $"电锯敌人  {progress.SawmanKills}/{progress.SawmanTarget}";
    }

    /// <summary>Navigate 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、Mathf.Clamp(int, int, int)、MissionClearedScreen.UpdateSelection()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.Update()；下游（含内部回调体） AudioManager.PlaySfx(string)、Mathf.Clamp(int, int, int)、MissionClearedScreen.UpdateSelection()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    void Navigate(int direction)
    {
        // 下一次。
int next = Mathf.Clamp(selectedIndex + direction, 0, optionButtons.Length - 1);
        if (next == selectedIndex)
        {
            return;
        }

        selectedIndex = next;
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue4 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.PlaySfx(AudioEventNames.UIHover);
        }

        UpdateSelection();
    }

    /// <summary>UpdateSelection 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.CreateOptionButton(string, string, int, float, float)、MissionClearedScreen.Navigate(int)、MissionClearedScreen.RevealSequence()、MissionClearedScreen.Show(MissionResult)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateSelection()
    {
        // 当前遍历索引。
for (int i = 0; i < optionTexts.Length; i++)
        {
            // 是否选定。
bool selected = i == selectedIndex;
            // 赋值前保存的目标选项文字集合。
global::UnityEngine.UI.Text assignmentReceiver5 = optionTexts[i];
            if (selected)
            {
                assignmentReceiver5.fontStyle = FontStyle.Bold;
            }
            else
            {
                assignmentReceiver5.fontStyle = FontStyle.Normal;
            }

            // 赋值前保存的目标选项文字集合。
global::UnityEngine.UI.Text assignmentReceiver7 = optionTexts[i];
            if (selected)
            {
                assignmentReceiver7.color = Color.white;
            }
            else
            {
                assignmentReceiver7.color = new Color(1f, 1f, 1f, 0.62f);
            }
        }

        if (selectionIndicator == null || optionButtons.Length == 0)
        {
            return;
        }

        selectionIndicator.gameObject.SetActive(false);
        selectionIndicator.SetParent(optionButtons[selectedIndex].transform, false);
        selectionIndicator.anchorMin = new Vector2(0f, 0.12f);
        selectionIndicator.anchorMax = new Vector2(0f, 0.88f);
        selectionIndicator.pivot = new Vector2(0f, 0.5f);
        selectionIndicator.anchoredPosition = Vector2.zero;
        selectionIndicator.sizeDelta = new Vector2(5f, 0f);
    }

    /// <summary>确认结算选项，并保持本局暂停直到目标场景加载。</summary>
    /// <param name="index">索引。</param>
    void ExecuteOption(int index)
    {
        if (!canInteract)
        {
            return;
        }

        canInteract = false;
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue9 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.PlaySfx(AudioEventNames.UIConfirm);
        }

        if (GameManager.HasInstance)
        {
            GameManager.Instance.HoldPauseForSceneExit();
        }

        if (index == 0)
        {
            StartReloadWithLoadingView();
            return;
        }

        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.ClearAllModals();
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
            Debug.LogError("[通关结算] 找不到可用的标题场景。");
        }
    }

    private void StartReloadWithLoadingView()
    {
        isActive = false;
        StopAllCoroutines();
        if (prologueCue != null)
        {
            prologueCue.Stop();
        }

        string scenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath))
        {
            scenePath = SceneManagerEx.MainScenePath;
        }

        if (string.IsNullOrEmpty(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
        {
            scenePath = SceneManager.GetActiveScene().name;
        }

        UIVisualTheme resolvedTheme = ResolveVisualTheme();
        Texture2D backdrop = null;
        Font loadingFont = interfaceFont;
        if (resolvedTheme != null)
        {
            backdrop = resolvedTheme.chapterLoadingBackdrop;
            if (loadingFont == null)
            {
                loadingFont = resolvedTheme.interfaceFont;
            }
        }

        if (loadingFont == null)
        {
            loadingFont = titleFont;
        }

        Transform hostParent = transform.parent;
        if (hostParent == null)
        {
            hostParent = transform;
        }

        ChapterLoadingView loadingView = ChapterLoadingView.Create(hostParent, backdrop, loadingFont);
        loadingView.transform.SetAsLastSibling();

        if (mainPanel != null)
        {
            mainPanel.alpha = 0f;
            mainPanel.interactable = false;
            mainPanel.blocksRaycasts = false;
        }

        if (UICursorManager.Instance != null)
        {
            UICursorManager.Instance.ShowCustomCursor();
        }

        loadingView.BeginAsyncSceneLoad(scenePath, PlayReloadContinueSound, () =>
        {
            if (GameUIManager.HasInstance)
            {
                GameUIManager.Instance.ClearAllModals();
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
    }

    private UIVisualTheme ResolveVisualTheme()
    {
        if (visualTheme != null)
        {
            return visualTheme;
        }

        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pauseMenu != null && pauseMenu.visualTheme != null)
        {
            return pauseMenu.visualTheme;
        }

        return null;
    }

    private static void PlayReloadContinueSound()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlaySfx(AudioEventNames.UIConfirm);
        }
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.SetActive(bool)、MissionClearedScreen.CreateImage(string, Transform, Color)、MissionClearedScreen.CreateOptionButton(string, string, int, float, float) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.Show(MissionResult)、MissionClearedScreen.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;RectTransform&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、GameObject.SetActive(bool)、MissionClearedScreen.CreateImage(string, Transform, Color)、MissionClearedScreen.CreateOptionButton(string, string, int, float, float) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildInterface()
    {
        if (interfaceBuilt)
        {
            return;
        }

        interfaceBuilt = true;
        if (interfaceFont == null)
        {
            interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (titleFont == null)
        {
            titleFont = interfaceFont;
        }

        if (mainPanel == null)
        {
            mainPanel = GetComponent<CanvasGroup>();
        }

        if (mainPanel == null)
        {
            mainPanel = gameObject.AddComponent<CanvasGroup>();
        }

        // 根界面矩形变换。
RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        // 当前遍历索引。
for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // 背景。
Image background = CreateImage("通关暗幕", transform, new Color(0.01f, 0.025f, 0.02f, 0.94f));
        completionBackground = background;
        prologueCue = gameObject.AddComponent<AudioSource>();
        prologueCue.playOnAwake = false;
        prologueCue.ignoreListenerPause = true;
        prologueCue.clip = Resources.Load<AudioClip>("UI/PrologueClose");
        prologueCue.volume = .65f;
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        Shader smokeShader = Resources.Load<Shader>("UI/PrologueSmokeBackdrop");
        if (smokeShader != null && smokeShader.isSupported)
        {
            GameObject smoke = new GameObject("序章烟雾", typeof(RectTransform), typeof(RawImage));
            smoke.transform.SetParent(transform, false);
            prologueSmoke = smoke.GetComponent<RawImage>();
            prologueSmokeMaterial = new Material(smokeShader);
            prologueSmoke.material = prologueSmokeMaterial;
            prologueSmoke.raycastTarget = false;
            prologueSmoke.enabled = false;
            SetAnchors(prologueSmoke.rectTransform, new Vector2(-.10f, -.12f), new Vector2(.76f, .76f));
        }
        titleText = CreateText("通关标题", transform, "第一章完成", titleFont, 72, FontStyle.Bold);
        SetAnchors(titleText.rectTransform, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.84f));
        routeText = CreateText("通关路线", transform, string.Empty, interfaceFont, 30, FontStyle.Bold);
        routeText.color = new Color(0.72f, 0.95f, 0.76f, 1f);
        SetAnchors(routeText.rectTransform, new Vector2(0.28f, 0.59f), new Vector2(0.72f, 0.67f));
        resultText = CreateText("通关数据", transform, string.Empty, interfaceFont, 24, FontStyle.Normal);
        resultText.lineSpacing = 1.3f;
        SetAnchors(resultText.rectTransform, new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.57f));
        optionButtons = new Button[2];
        optionTexts = new Text[2];
        optionButtons[0] = CreateOptionButton("重新挑战按钮", "重新挑战", 0, 0.075f, 0.137f);
        optionButtons[1] = CreateOptionButton("返回标题按钮", "返回标题画面", 1, 0.075f, 0.137f);
        // 指示器。
Image indicator = CreateImage("选择指示线", optionButtons[0].transform, new Color(0.6f, 0.95f, 0.7f, 1f));
        selectionIndicator = indicator.rectTransform;
        hintText = CreateText("操作提示", transform, "鼠标左键／回车  确定", interfaceFont, 16, FontStyle.Normal);
        hintText.color = new Color(1f, 1f, 1f, 0.58f);
        SetAnchors(hintText.rectTransform, new Vector2(0.32f, 0.07f), new Vector2(0.68f, 0.12f));
        // button（按钮）：每轮从选项按钮集合取得一个元素，仅代表当前这一项。使用位置：MissionClearedScreen.BuildInterface()。
foreach (Button button in optionButtons)
        {
            button.interactable = false;
        }

        selectionIndicator.gameObject.SetActive(false);
    }

    /// <summary>CreateOptionButton 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、Color.Color(float, float, float, float)、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()；下游（含内部回调体） AudioManager.PlaySfx(string)、Color.Color(float, float, float, float)、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">对象名称。</param>
    /// <param name="label">标签。</param>
    /// <param name="index">索引。</param>
    /// <param name="minimumY">最小纵向。</param>
    /// <param name="maximumY">最大纵向。</param>
    Button CreateOptionButton(string objectName, string label, int index, float minimumY, float maximumY)
    {
        // 按钮对象。
GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        float left = 0.57f + index * 0.18f;
        SetAnchors(buttonObject.GetComponent<RectTransform>(), new Vector2(left, minimumY), new Vector2(left + 0.17f, maximumY));
        // 图像。
Image image = buttonObject.GetComponent<Image>();
        image.color = Color.clear;
        // 按钮。
Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        // 已捕获索引。
int capturedIndex = index;
        button.onClick.AddListener(() => ExecuteOption(capturedIndex));
        optionTexts[index] = CreateText("文字", buttonObject.transform, label, interfaceFont, 23, FontStyle.Normal);
        GameOverScreen.ConfigureTerminalOption(buttonObject.transform, optionTexts[index], index);
        // 触发器。
EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        // 进入。
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
            // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue10 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue10, null))
            {
                receiverValue10.PlaySfx(AudioEventNames.UIHover);
            }

            UpdateSelection();
        });
        trigger.triggers.Add(enter);
        return button;
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">对象名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="color">颜色。</param>
    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        // 图像对象。
GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        // 图像。
Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">对象名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="value">值。</param>
    /// <param name="font">字体。</param>
    /// <param name="size">尺寸。</param>
    /// <param name="style">样式。</param>
    static Text CreateText(string objectName, Transform parent, string value, Font font, int size, FontStyle style)
    {
        // 文字对象。
GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        // 文字。
Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(14, size - 20);
        text.resizeTextMaxSize = size;
        return text;
    }

    /// <summary>SetAnchors 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">矩形。</param>
    /// <param name="minimum">最小。</param>
    /// <param name="maximum">最大。</param>
    static void SetAnchors(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">矩形。</param>
    static void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
