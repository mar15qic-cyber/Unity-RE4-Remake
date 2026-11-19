using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>第一章通关结算画面。</summary>
public class MissionClearedScreen : MonoBehaviour
{
    /// <summary>中文含义：主要面板。使用位置：MissionClearedScreen.BuildInterface()、MissionClearedScreen.Hide()、MissionClearedScreen.RevealSequence() 等共 5 处。</summary>
    [Header("面板引用")]
    public CanvasGroup mainPanel;
    /// <summary>中文含义：标题文字。使用位置：作为 MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、MissionClearedScreen.BuildInterface()。</summary>
    public Text titleText;
    private static readonly int SmokeTimeProperty = Shader.PropertyToID("_SmokeTime");
    private RawImage prologueSmoke;
    private Material prologueSmokeMaterial;
    private float smokeStarted;
    private AudioSource prologueCue;
    /// <summary>中文含义：路线文字。使用位置：作为 MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、MissionClearedScreen.BuildInterface()、MissionClearedScreen.UpdateResult(MissionResult)。</summary>
    public Text routeText;
    /// <summary>中文含义：结果文字。使用位置：作为 MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、MissionClearedScreen.BuildInterface()、MissionClearedScreen.UpdateResult(MissionResult)。</summary>
    public Text resultText;
    /// <summary>中文含义：操作提示栏文字。使用位置：作为 MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、MissionClearedScreen.BuildInterface()。</summary>
    public Text hintText;
    /// <summary>中文含义：选择指示器。使用位置：MissionClearedScreen.BuildInterface()、MissionClearedScreen.UpdateSelection()。</summary>
    public RectTransform selectionIndicator;
    /// <summary>中文含义：标题字体。使用位置：作为 MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle) 的参数、MissionClearedScreen.BuildInterface()。</summary>
    [Header("字体")]
    public Font titleFont;
    /// <summary>中文含义：接口字体。使用位置：作为 MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle) 的参数、MissionClearedScreen.BuildInterface()。</summary>
    public Font interfaceFont;
    /// <summary>中文含义：显露持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、MissionClearedScreen.RevealSequence()。</summary>
    [Header("动效")]
    [Min(0.1f)]
    public float revealDuration = 0.65f;
    /// <summary>中文含义：选项集合延迟。使用位置：作为 WaitForSecondsRealtime.WaitForSecondsRealtime(float) 的参数。</summary>
    [Min(0f)]
    public float optionsDelay = 0.45f;
    /// <summary>中文含义：选项按钮集合。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 MissionClearedScreen.CreateImage(string, Transform, Color) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 6 处。</summary>
    private Button[] optionButtons = Array.Empty<Button>();
    /// <summary>中文含义：选项文字集合。使用位置：作为 MissionClearedScreen.Stretch(RectTransform) 的参数、MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float) 等共 4 处。</summary>
    private Text[] optionTexts = Array.Empty<Text>();
    /// <summary>中文含义：是否接口已构建。使用位置：MissionClearedScreen.BuildInterface()、MissionClearedScreen.Show(MissionResult)。</summary>
    private bool interfaceBuilt;
    /// <summary>中文含义：是否生效。使用位置：MissionClearedScreen.Hide()、MissionClearedScreen.Show(MissionResult)、MissionClearedScreen.Start() 等共 4 处。</summary>
    private bool isActive;
    internal bool IsShowing => isActive;
    /// <summary>中文含义：是否可以交互。使用位置：作为 GameObject.SetActive(bool) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、MissionClearedScreen.ExecuteOption(int) 等共 7 处。</summary>
    private bool canInteract;
    /// <summary>中文含义：选定索引。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 MissionClearedScreen.ExecuteOption(int) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数 等共 6 处。</summary>
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
    /// <param name="result">中文含义：当前任务结果。使用位置：作为 MissionClearedScreen.UpdateResult(MissionResult) 的参数。</param>
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
        // receiverValue1（判空前缓存的界面光标管理器的实例）：从界面光标管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MissionClearedScreen.Show(MissionResult)。
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
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、MissionClearedScreen.RevealSequence()。
float elapsed = 0f;
        // rect（矩形）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.RevealSequence()。
RectTransform rect = mainPanel.GetComponent<RectTransform>();
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、MissionClearedScreen.RevealSequence()。
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
    /// <param name="result">中文含义：当前任务结果。使用位置：MissionClearedScreen.UpdateResult(MissionResult)。</param>
    void UpdateResult(MissionResult result)
    {
        // progress（进度）：使用位置：作为 Mathf.FloorToInt(float) 的参数、MissionClearedScreen.UpdateResult(MissionResult)。
MissionProgressSnapshot progress = result.Progress;
        // assignmentReceiver2（赋值前保存的目标路线文字）：从路线文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MissionClearedScreen.UpdateResult(MissionResult)。
global::UnityEngine.UI.Text assignmentReceiver2 = routeText;
        if (result.CompletionReason == MissionCompletionReason.Extermination)
        {
            assignmentReceiver2.text = "歼灭路线达成";
        }
        else
        {
            assignmentReceiver2.text = "生存路线达成";
        }

        // elapsed（已用）：保存 Mathf.FloorToInt(float) 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.UpdateResult(MissionResult)。
int elapsed = Mathf.FloorToInt(progress.ElapsedSeconds);
        // minutes（分钟）：使用位置：MissionClearedScreen.UpdateResult(MissionResult)。
int minutes = elapsed / 60;
        // seconds（秒）：使用位置：MissionClearedScreen.UpdateResult(MissionResult)。
int seconds = elapsed % 60;
        resultText.text = $"用时  {minutes:00}:{seconds:00}\n" + $"普通敌人  {progress.GanadoKills}/{progress.GanadoTarget}\n" + $"电锯敌人  {progress.SawmanKills}/{progress.SawmanTarget}";
    }

    /// <summary>Navigate 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、Mathf.Clamp(int, int, int)、MissionClearedScreen.UpdateSelection()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.Update()；下游（含内部回调体） AudioManager.PlaySfx(string)、Mathf.Clamp(int, int, int)、MissionClearedScreen.UpdateSelection()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</param>
    void Navigate(int direction)
    {
        // next（下一次）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.Navigate(int)。
int next = Mathf.Clamp(selectedIndex + direction, 0, optionButtons.Length - 1);
        if (next == selectedIndex)
        {
            return;
        }

        selectedIndex = next;
        // receiverValue4（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MissionClearedScreen.Navigate(int)。
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
        // i（当前遍历索引）：使用位置：MissionClearedScreen.UpdateSelection()。
for (int i = 0; i < optionTexts.Length; i++)
        {
            // selected（是否选定）：使用位置：MissionClearedScreen.UpdateSelection()。
bool selected = i == selectedIndex;
            // assignmentReceiver5（赋值前保存的目标选项文字集合）：从选项文字集合读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MissionClearedScreen.UpdateSelection()。
global::UnityEngine.UI.Text assignmentReceiver5 = optionTexts[i];
            if (selected)
            {
                assignmentReceiver5.fontStyle = FontStyle.Bold;
            }
            else
            {
                assignmentReceiver5.fontStyle = FontStyle.Normal;
            }

            // assignmentReceiver7（赋值前保存的目标选项文字集合）：从选项文字集合读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MissionClearedScreen.UpdateSelection()。
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

    /// <summary>ExecuteOption 的业务处理入口；本方法直接依赖 Application.CanStreamedLevelBeLoaded(string)、AudioManager.PlaySfx(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameUIManager.ClearAllModals()、object.ReferenceEquals(object, object)、SceneManager.GetActiveScene() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.CreateOptionButton(string, string, int, float, float)、MissionClearedScreen.Update()；下游（含内部回调体） Application.CanStreamedLevelBeLoaded(string)、AudioManager.PlaySfx(string)、Debug.LogError(object)、GameManager.ResetScenePermission()、GameUIManager.ClearAllModals()、object.ReferenceEquals(object, object)、SceneManager.GetActiveScene() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="index">中文含义：索引。使用位置：MissionClearedScreen.ExecuteOption(int)。</param>
    void ExecuteOption(int index)
    {
        if (!canInteract)
        {
            return;
        }

        canInteract = false;
        // receiverValue9（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MissionClearedScreen.ExecuteOption(int)。
global::AudioManager receiverValue9 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.PlaySfx(AudioEventNames.UIConfirm);
        }

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
            Debug.LogError("[通关结算] 找不到可用的标题场景。");
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

        // root（根界面矩形变换）：保存 Component.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 MissionClearedScreen.Stretch(RectTransform) 的参数。
RectTransform root = GetComponent<RectTransform>();
        Stretch(root);
        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、MissionClearedScreen.BuildInterface()。
for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // background（背景）：保存 MissionClearedScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：作为 MissionClearedScreen.Stretch(RectTransform) 的参数、MissionClearedScreen.BuildInterface()。
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
        // indicator（指示器）：保存 MissionClearedScreen.CreateImage(string, Transform, Color) 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.BuildInterface()。
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
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="label">中文含义：标签。使用位置：作为 MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle) 的参数。</param>
    /// <param name="index">中文含义：索引。使用位置：MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。</param>
    /// <param name="minimumY">中文含义：最小纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="maximumY">中文含义：最大纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    Button CreateOptionButton(string objectName, string label, int index, float minimumY, float maximumY)
    {
        // buttonObject（按钮对象）：使用位置：作为 MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle) 的参数、作为 MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2) 的参数、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。
GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        float left = 0.57f + index * 0.18f;
        SetAnchors(buttonObject.GetComponent<RectTransform>(), new Vector2(left, minimumY), new Vector2(left + 0.17f, maximumY));
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。
Image image = buttonObject.GetComponent<Image>();
        image.color = Color.clear;
        // button（按钮）：保存 GameObject.GetComponent<Button>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。
Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        // capturedIndex（已捕获索引）：使用位置：作为 MissionClearedScreen.ExecuteOption(int) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
int capturedIndex = index;
        button.onClick.AddListener(() => ExecuteOption(capturedIndex));
        optionTexts[index] = CreateText("文字", buttonObject.transform, label, interfaceFont, 23, FontStyle.Normal);
        GameOverScreen.ConfigureTerminalOption(buttonObject.transform, optionTexts[index], index);
        // trigger（触发器）：保存 GameObject.AddComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。
EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        // enter（进入）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)。
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
            // receiverValue10（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
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
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：MissionClearedScreen.CreateImage(string, Transform, Color)。</param>
    static Image CreateImage(string objectName, Transform parent, Color color)
    {
        // imageObject（图像对象）：使用位置：MissionClearedScreen.CreateImage(string, Transform, Color)。
GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.CreateImage(string, Transform, Color)。
Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Mathf.Max(int, int)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。</param>
    /// <param name="font">中文含义：字体。使用位置：MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：作为 Mathf.Max(int, int) 的参数、MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。</param>
    /// <param name="style">中文含义：样式。使用位置：MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。</param>
    static Text CreateText(string objectName, Transform parent, string value, Font font, int size, FontStyle style)
    {
        // textObject（文字对象）：使用位置：MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。
GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：MissionClearedScreen.CreateText(string, Transform, string, Font, int, FontStyle)。
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
    /// <param name="rect">中文含义：矩形。使用位置：MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="minimum">中文含义：最小。使用位置：MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    /// <param name="maximum">中文含义：最大。使用位置：MissionClearedScreen.SetAnchors(RectTransform, Vector2, Vector2)。</param>
    static void SetAnchors(RectTransform rect, Vector2 minimum, Vector2 maximum)
    {
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>Stretch 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionClearedScreen.BuildInterface()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：MissionClearedScreen.Stretch(RectTransform)。</param>
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
