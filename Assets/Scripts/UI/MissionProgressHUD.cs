using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>第一章任务常驻进度界面。</summary>
public class MissionProgressHUD : MonoBehaviour
{
    /// <summary>中文含义：根节点分组。使用位置：MissionProgressHUD.BuildInterface()、MissionProgressHUD.Hide()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</summary>
    [Header("界面引用")]
    public CanvasGroup rootGroup;
    /// <summary>中文含义：标题文字。使用位置：作为 MissionProgressHUD.SetLine(RectTransform, float, float) 的参数、MissionProgressHUD.BuildInterface()。</summary>
    public Text titleText;
    /// <summary>中文含义：计时器文字。使用位置：作为 MissionProgressHUD.SetLine(RectTransform, float, float) 的参数、MissionProgressHUD.BuildInterface()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</summary>
    public Text timerText;
    /// <summary>中文含义：村民敌人进度文字。使用位置：作为 MissionProgressHUD.SetLine(RectTransform, float, float) 的参数、MissionProgressHUD.BuildInterface()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</summary>
    public Text ganadoProgressText;
    /// <summary>中文含义：电锯敌人进度文字。使用位置：作为 MissionProgressHUD.SetLine(RectTransform, float, float) 的参数、MissionProgressHUD.BuildInterface()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</summary>
    public Text sawmanProgressText;
    /// <summary>中文含义：接口字体。使用位置：MissionProgressHUD.BuildInterface()、MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。</summary>
    [Header("字体")]
    public Font interfaceFont;
    /// <summary>中文含义：任务管理器。使用位置：作为 MissionProgressHUD.UpdateView(MissionProgressSnapshot) 的参数、MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)、MissionProgressHUD.ConnectPresentation() 等共 4 处。</summary>
    private ChapterOneMissionManager missionManager;
    /// <summary>中文含义：游戏管理器。使用位置：MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)、MissionProgressHUD.ConnectPresentation()、MissionProgressHUD.Unbind()。</summary>
    private GameManager gameManager;
    /// <summary>中文含义：是否接口已构建。使用位置：MissionProgressHUD.BuildInterface()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</summary>
    private bool interfaceBuilt;
    private bool siegeAnnounced;
    private Text siegeTitle;
    private Coroutine siegeNotice;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MissionProgressHUD.BuildInterface()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        BuildInterface();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MissionProgressHUD.ConnectPresentation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable() => ConnectPresentation();
    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MissionProgressHUD.Unbind()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable() => Unbind();
    /// <summary>BindingCurrentSession 的业务处理入口；本方法直接依赖 MissionProgressHUD.ConnectPresentation()、MissionProgressHUD.Unbind()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） MissionProgressHUD.ConnectPresentation()、MissionProgressHUD.Unbind()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mission">中文含义：任务。使用位置：MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)。</param>
    /// <param name="match">中文含义：对局。使用位置：MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)。</param>
    internal void BindingCurrentSession(ChapterOneMissionManager mission, GameManager match)
    {
        Unbind();
        siegeAnnounced = false;
        missionManager = mission;
        gameManager = match;
        if (isActiveAndEnabled)
        {
            ConnectPresentation();
        }
    }

    /// <summary>ConnectPresentation 的业务处理入口；本方法直接依赖 MissionProgressHUD.Hide()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)、MissionProgressHUD.OnEnable()；下游（含内部回调体） MissionProgressHUD.Hide()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConnectPresentation()
    {
        if (missionManager == null || gameManager == null)
        {
            return;
        }

        missionManager.ProgressChanged += UpdateView;
        gameManager.OnPlayerDied += Hide;
        gameManager.OnMissionCompleted += OnMissionCompleted;
        UpdateView(missionManager.LatestSnapshot);
        if (gameManager.IsTerminal)
        {
            Hide();
        }
    }

    /// <summary>Unbind 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.BindingCurrentSession(ChapterOneMissionManager, GameManager)、MissionProgressHUD.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Unbind()
    {
        Hide();
        if (missionManager != null)
        {
            missionManager.ProgressChanged -= UpdateView;
        }

        if (gameManager != null)
        {
            gameManager.OnPlayerDied -= Hide;
            gameManager.OnMissionCompleted -= OnMissionCompleted;
        }
    }

    /// <summary>OnMissionCompleted 的业务处理入口；本方法直接依赖 MissionProgressHUD.Hide()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MissionProgressHUD.Hide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前任务结果。</param>
    void OnMissionCompleted(MissionResult result)
    {
        Hide();
    }

    /// <summary>Hide 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.ConnectPresentation()、MissionProgressHUD.OnMissionCompleted(MissionResult)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Hide()
    {
        if (siegeNotice != null)
        {
            StopCoroutine(siegeNotice);
            siegeNotice = null;
        }

        if (siegeTitle != null)
        {
            siegeTitle.enabled = false;
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
        }
    }

    /// <summary>UpdateView 的业务处理入口；本方法直接依赖 Mathf.CeilToInt(float)、MissionProgressHUD.BuildInterface()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.ConnectPresentation()；下游（含内部回调体） Mathf.CeilToInt(float)、MissionProgressHUD.BuildInterface()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="progress">中文含义：进度。使用位置：作为 Mathf.CeilToInt(float) 的参数、MissionProgressHUD.UpdateView(MissionProgressSnapshot)。</param>
    void UpdateView(MissionProgressSnapshot progress)
    {
        if (!interfaceBuilt)
        {
            BuildInterface();
        }

        if (missionManager != null && missionManager.UsesBellEnding)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            if (progress.IsCompleted || missionManager.IsBellEnding)
            {
                Hide();
            }
            else if (progress.IsRunning && !siegeAnnounced)
            {
                siegeAnnounced = true;
                siegeNotice = StartCoroutine(RevealSiegeTitle());
            }
            return;
        }

        if (rootGroup != null)
        {
            // assignmentReceiver1（赋值前保存的目标根节点分组）：从根节点分组读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：MissionProgressHUD.UpdateView(MissionProgressSnapshot)。
global::UnityEngine.CanvasGroup assignmentReceiver1 = rootGroup;
            if (progress.IsCompleted ||
                (missionManager != null && missionManager.UsesBellEnding && (!progress.IsRunning || missionManager.IsBellEnding)))
            {
                assignmentReceiver1.alpha = 0f;
            }
            else
            {
                assignmentReceiver1.alpha = 1f;
            }
        }

        // remaining（剩余）：保存 Mathf.CeilToInt(float) 的返回结果，供当前步骤使用。使用位置：MissionProgressHUD.UpdateView(MissionProgressSnapshot)。
int remaining = Mathf.CeilToInt(progress.RemainingSeconds);
        // minutes（分钟）：使用位置：MissionProgressHUD.UpdateView(MissionProgressSnapshot)。
int minutes = remaining / 60;
        // seconds（秒）：使用位置：MissionProgressHUD.UpdateView(MissionProgressSnapshot)。
int seconds = remaining % 60;
        if (timerText != null)
        {
            timerText.text = $"生存倒计时  {minutes:00}:{seconds:00}";
        }

        bool showCombinedKills = missionManager != null && missionManager.UsesBellEnding;
        if (ganadoProgressText != null)
        {
            if (showCombinedKills)
            {
                ganadoProgressText.text = $"击杀敌人  {progress.GanadoKills + progress.SawmanKills}/{missionManager.BellKillTarget}";
            }
            else
            {
                ganadoProgressText.text = $"普通敌人  {progress.GanadoKills}/{progress.GanadoTarget}";
            }
        }

        if (sawmanProgressText != null)
        {
            sawmanProgressText.enabled = !showCombinedKills;
            sawmanProgressText.text = $"电锯敌人  {progress.SawmanKills}/{progress.SawmanTarget}";
        }
    }

    IEnumerator RevealSiegeTitle()
    {
        if (siegeTitle == null)
        {
            RectTransform canvas = GetComponent<RectTransform>();
            siegeTitle = CreateText("围攻目标", canvas, "抵御围攻", 25, FontStyle.Normal);
            siegeTitle.raycastTarget = false;
            siegeTitle.alignment = TextAnchor.MiddleCenter;
            RectTransform rect = siegeTitle.rectTransform;
            rect.anchorMin = new Vector2(0.26f, 0.78f);
            rect.anchorMax = new Vector2(0.74f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        siegeTitle.enabled = true;
        for (float elapsed = 0f; elapsed < 4.3f; elapsed += Time.unscaledDeltaTime)
        {
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.4f, elapsed));
            float disappear = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(3.5f, 4.3f, elapsed));
            siegeTitle.color = new Color(0.94f, 0.9f, 0.81f, appear * disappear);
            yield return null;
        }

        siegeTitle.enabled = false;
        siegeNotice = null;
    }

    /// <summary>BuildInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.Start()、MissionProgressHUD.UpdateView(MissionProgressSnapshot)；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;RectTransform&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

        // rootRect（根节点矩形）：使用位置：作为 MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle) 的参数、MissionProgressHUD.BuildInterface()。
RectTransform rootRect;
        if (rootGroup == null)
        {
            // root（根游戏对象）：使用位置：MissionProgressHUD.BuildInterface()。
GameObject root = new GameObject("第一章任务进度", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            rootRect = root.GetComponent<RectTransform>();
            rootGroup = root.GetComponent<CanvasGroup>();
            // background（背景）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：MissionProgressHUD.BuildInterface()。
Image background = root.GetComponent<Image>();
            background.color = new Color(0.015f, 0.02f, 0.025f, 0.72f);
            background.raycastTarget = false;
        }
        else
        {
            rootRect = rootGroup.GetComponent<RectTransform>();
        }

        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(28f, -28f);
        rootRect.sizeDelta = new Vector2(330f, 150f);
        rootGroup.alpha = 1f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
        if (titleText == null)
        {
            titleText = CreateText("任务标题", rootRect, "第一章任务", 22, FontStyle.Bold);
        }

        if (timerText == null)
        {
            timerText = CreateText("生存倒计时", rootRect, "生存倒计时  04:00", 24, FontStyle.Bold);
        }

        if (ganadoProgressText == null)
        {
            ganadoProgressText = CreateText("普通敌人进度", rootRect, "普通敌人  0/15", 19, FontStyle.Normal);
        }

        if (sawmanProgressText == null)
        {
            sawmanProgressText = CreateText("电锯敌人进度", rootRect, "电锯敌人  0/2", 19, FontStyle.Normal);
        }

        SetLine(titleText.rectTransform, 0.71f, 0.96f);
        SetLine(timerText.rectTransform, 0.46f, 0.72f);
        SetLine(ganadoProgressText.rectTransform, 0.23f, 0.47f);
        SetLine(sawmanProgressText.rectTransform, 0.02f, 0.25f);
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.BuildInterface()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。</param>
    /// <param name="style">中文含义：样式。使用位置：MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。</param>
    Text CreateText(string objectName, Transform parent, string value, int size, FontStyle style)
    {
        // textObject（文字对象）：使用位置：MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。
GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：MissionProgressHUD.CreateText(string, Transform, string, int, FontStyle)。
Text text = textObject.GetComponent<Text>();
        text.font = interfaceFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>SetLine 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionProgressHUD.BuildInterface()；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rect">中文含义：矩形。使用位置：MissionProgressHUD.SetLine(RectTransform, float, float)。</param>
    /// <param name="minimumY">中文含义：最小纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="maximumY">中文含义：最大纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    static void SetLine(RectTransform rect, float minimumY, float maximumY)
    {
        rect.anchorMin = new Vector2(0f, minimumY);
        rect.anchorMax = new Vector2(1f, maximumY);
        rect.offsetMin = new Vector2(18f, 0f);
        rect.offsetMax = new Vector2(-18f, 0f);
    }
}
