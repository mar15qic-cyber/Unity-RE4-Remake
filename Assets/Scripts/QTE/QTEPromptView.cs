using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 快速反应提示界面。仅订阅只读事件，不参与成功或失败判定。
/// </summary>
[DefaultExecutionOrder(10200)]
public class QTEPromptView : MonoBehaviour
{
    /// <summary>当前里昂配对动作协调器。</summary>
    [Header("会话来源")]
    public LeonQTECoordinator coordinator;
    /// <summary>根节点分组。</summary>
    [Header("界面引用")]
    public CanvasGroup rootGroup;
    /// <summary>键标签。</summary>
    public Text keyLabel;
    /// <summary>键图标。</summary>
    public Image keyIcon;
    /// <summary>时间标签。</summary>
    public Text timeLabel;
    /// <summary>时间填充。</summary>
    public Image timeFill;
    /// <summary>进度环。</summary>
    public Image progressRing;
    /// <summary>进度线段集合。</summary>
    public Image[] progressSegments;
    /// <summary>提示根节点。</summary>
    [Header("世界跟随")]
    public RectTransform promptRoot;
    /// <summary>屏幕偏移量。</summary>
    public Vector2 screenOffset = new Vector2(0f, 105f);
    /// <summary>跟随平滑速度。</summary>
    [Min(1f)]
    public float followSmoothSpeed = 18f;
    /// <summary>非活动颜色。</summary>
    [Header("颜色")]
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.2f);
    /// <summary>生效颜色。</summary>
    public Color activeColor = new Color(0.95f, 0.75f, 0.12f, 1f);
    /// <summary>可见会话编号。</summary>
    private int visibleSessionId;
    /// <summary>是否已开始。</summary>
    private bool started;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>父级画布。</summary>
    private Canvas parentCanvas;
    /// <summary>世界空间摄像机。</summary>
    private Camera worldCamera;
    /// <summary>当前目标。</summary>
    private Transform currentTarget;
    /// <summary>当前动作文字。</summary>
    private string currentActionText = string.Empty;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponentInParent&lt;Canvas&gt;()、QTEPromptView.ApplyReferenceStyle()、QTEPromptView.BindEvents()、QTEPromptView.DisableDisplayRaycasterIfUnused()、QTEPromptView.HandlePromptAvailabilityChanged(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (promptRoot == null && keyLabel != null)
        {
            promptRoot = keyLabel.transform.parent as RectTransform;
        }

        if (promptRoot == null && rootGroup != null)
        {
            promptRoot = rootGroup.transform as RectTransform;
        }

        if (promptRoot != null)
        {
            parentCanvas = promptRoot.GetComponentInParent<Canvas>();
        }

        worldCamera = Camera.main;
        DisableDisplayRaycasterIfUnused();
        ApplyReferenceStyle();
        started = true;
        BindEvents();
        HandlePromptAvailabilityChanged(coordinator != null && coordinator.IsPromptAvailable);
    }

    /// <summary>DisableDisplayRaycasterIfUnused 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;GraphicRaycaster&gt;()、Component.GetComponentsInChildren&lt;EventTrigger&gt;(bool)、Component.GetComponentsInChildren&lt;QTEPromptView&gt;(bool)、Component.GetComponentsInChildren&lt;Selectable&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.Start()；下游（含内部回调体） Component.GetComponent&lt;GraphicRaycaster&gt;()、Component.GetComponentsInChildren&lt;EventTrigger&gt;(bool)、Component.GetComponentsInChildren&lt;QTEPromptView&gt;(bool)、Component.GetComponentsInChildren&lt;Selectable&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void DisableDisplayRaycasterIfUnused()
    {
        if (parentCanvas == null)
        {
            return;
        }

        // 射线检测器。
GraphicRaycaster raycaster = parentCanvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            return;
        }

        if (parentCanvas.GetComponentsInChildren<QTEPromptView>(true).Length != 1)
        {
            return;
        }

        if (parentCanvas.GetComponentsInChildren<Selectable>(true).Length > 0)
        {
            return;
        }

        if (parentCanvas.GetComponentsInChildren<UnityEngine.EventSystems.EventTrigger>(true).Length > 0)
        {
            return;
        }

        raycaster.enabled = false;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTEPromptView.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (started)
        {
            BindEvents();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTEPromptView.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        UnbindEvents();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、QTEPromptView.FollowTarget()、QTEPromptView.UpdateKeyLabel(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (rootGroup == null || rootGroup.alpha <= 0.001f)
        {
            return;
        }

        if (coordinator != null && coordinator.TryGetPromptPresentation(out Transform /* 中文含义：目标变换节点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target, out string /* 中文含义：动作文字。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ actionText))
        {
            currentTarget = target;
            currentActionText = actionText;
            UpdateKeyLabel(true);
        }

        FollowTarget();
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.OnEnable()、QTEPromptView.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnQTESessionStarted += HandleSessionStarted;
        coordinator.OnQTEPhaseChanged += HandlePhaseChanged;
        coordinator.OnQTEProgressChanged += HandleProgressChanged;
        coordinator.OnQTESessionEnded += HandleSessionEnded;
        coordinator.OnQTEPromptAvailabilityChanged += HandlePromptAvailabilityChanged;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnQTESessionStarted -= HandleSessionStarted;
        coordinator.OnQTEPhaseChanged -= HandlePhaseChanged;
        coordinator.OnQTEProgressChanged -= HandleProgressChanged;
        coordinator.OnQTESessionEnded -= HandleSessionEnded;
        coordinator.OnQTEPromptAvailabilityChanged -= HandlePromptAvailabilityChanged;
        eventsBound = false;
    }

    /// <summary>HandlePromptAvailabilityChanged 的业务处理入口；本方法直接依赖 LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、object.ReferenceEquals(object, object)、QTEPromptView.SetProgress(int)、QTEPromptView.SetVisible(bool)、QTEPromptView.UpdateKeyLabel(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.Start()；下游（含内部回调体） LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、object.ReferenceEquals(object, object)、QTEPromptView.SetProgress(int)、QTEPromptView.SetVisible(bool)、QTEPromptView.UpdateKeyLabel(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="available">是否可用。</param>
    private void HandlePromptAvailabilityChanged(bool available)
    {
        if (visibleSessionId != 0)
        {
            return;
        }

        if (!available)
        {
            currentTarget = null;
            currentActionText = string.Empty;
            SetVisible(false);
            return;
        }

        // 判空前缓存的当前里昂配对动作协调器。
global::LeonQTECoordinator receiverValue1 = coordinator;
        // 条件访问得到的尝试取得提示表现的返回结果。
bool? optionalValue2;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            optionalValue2 = receiverValue1.TryGetPromptPresentation(out currentTarget, out currentActionText);
        }
        else
        {
            optionalValue2 = default;
        }

        UpdateKeyLabel(true);
        if (keyIcon != null)
        {
            keyIcon.enabled = true;
        }

        if (progressRing != null)
        {
            progressRing.fillAmount = 0f;
        }

        SetProgress(0);
        SetVisible(true);
    }

    /// <summary>HandleSessionStarted 的业务处理入口；本方法直接依赖 LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、QTEPromptView.SetProgress(int)、QTEPromptView.SetVisible(bool)、QTEPromptView.UpdateKeyLabel(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、QTEPromptView.SetProgress(int)、QTEPromptView.SetVisible(bool)、QTEPromptView.UpdateKeyLabel(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="kind">种类。</param>
    private void HandleSessionStarted(int sessionId, QTEKind kind)
    {
        visibleSessionId = sessionId;
        if (kind == QTEKind.KnifeAssassination)
        {
            currentActionText = "暗杀";
        }
        else if (kind == QTEKind.RearGrab)
        {
            currentActionText = "连按或长按挣脱";
        }
        else
        {
            currentActionText = "挣脱";
        }

        if (coordinator != null && coordinator.TryGetPromptPresentation(out Transform /* 中文含义：目标变换节点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target, out _))
        {
            currentTarget = target;
        }

        UpdateKeyLabel(true);
        if (keyIcon != null)
        {
            keyIcon.enabled = true;
        }

        if (timeLabel != null)
        {
            timeLabel.text = string.Empty;
        }

        SetProgress(0);
        if (timeFill != null)
        {
            timeFill.fillAmount = 1f;
        }

        if (progressRing != null)
        {
            progressRing.fillAmount = 0f;
        }

        SetVisible(true);
    }

    /// <summary>HandlePhaseChanged 的业务处理入口；本方法直接依赖 QTEPromptView.SetVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTEPromptView.SetVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="phase">阶段。</param>
    private void HandlePhaseChanged(int sessionId, QTEPhase phase)
    {
        if (sessionId != visibleSessionId)
        {
            return;
        }

        if (phase == QTEPhase.SuccessResult || phase == QTEPhase.FailureResult)
        {
            SetVisible(false);
            return;
        }

        // 输入是否打开。
bool inputOpen = phase == QTEPhase.WaitingForInput;
        if (keyLabel != null)
        {
            // 赋值前保存的目标键标签。
global::UnityEngine.UI.Text assignmentReceiver4 = keyLabel;
            if (inputOpen)
            {
                assignmentReceiver4.color = activeColor;
            }
            else
            {
                assignmentReceiver4.color = inactiveColor;
            }

            // 赋值前保存的目标键标签。
global::UnityEngine.UI.Text assignmentReceiver6 = keyLabel;
            if (inputOpen)
            {
                assignmentReceiver6.text = "F  " + currentActionText;
            }
            else
            {
                assignmentReceiver6.text = "准备";
            }
        }

        if (keyIcon != null)
        {
            keyIcon.enabled = inputOpen || phase == QTEPhase.Requested || phase == QTEPhase.Aligning || phase == QTEPhase.Entering;
        }
    }

    /// <summary>HandleProgressChanged 的业务处理入口；本方法直接依赖 float.ToString(string)、QTEPromptView.SetProgress(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） float.ToString(string)、QTEPromptView.SetProgress(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="progress">进度。</param>
    private void HandleProgressChanged(QTEProgressInfo progress)
    {
        if (progress.sessionId != visibleSessionId)
        {
            return;
        }

        SetProgress(progress.presses);
        if (timeFill != null)
        {
            timeFill.fillAmount = progress.NormalizedTime;
        }

        if (progressRing != null)
        {
            progressRing.fillAmount = progress.NormalizedProgress;
        }

        if (timeLabel != null)
        {
            timeLabel.text = progress.remainingTime.ToString("0.0");
        }
    }

    /// <summary>HandleSessionEnded 的业务处理入口；本方法直接依赖 QTEPromptView.SetVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTEPromptView.SetVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="endInfo">结束信息。</param>
    private void HandleSessionEnded(QTESessionEndInfo endInfo)
    {
        if (endInfo.sessionId != visibleSessionId)
        {
            return;
        }

        visibleSessionId = 0;
        currentTarget = null;
        currentActionText = string.Empty;
        SetVisible(false);
    }

    /// <summary>SetProgress 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.HandleProgressChanged(QTEProgressInfo)、QTEPromptView.HandlePromptAvailabilityChanged(bool)、QTEPromptView.HandleSessionStarted(int, QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="presses">按下次数。</param>
    private void SetProgress(int presses)
    {
        if (progressSegments == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < progressSegments.Length; i++)
        {
            if (progressSegments[i] != null)
            {
                // 赋值前保存的目标进度线段集合。
global::UnityEngine.UI.Image assignmentReceiver8 = progressSegments[i];
                if (i < presses)
                {
                    assignmentReceiver8.color = activeColor;
                }
                else
                {
                    assignmentReceiver8.color = inactiveColor;
                }
            }
        }
    }

    /// <summary>SetVisible 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.HandlePhaseChanged(int, QTEPhase)、QTEPromptView.HandlePromptAvailabilityChanged(bool)、QTEPromptView.HandleSessionEnded(QTESessionEndInfo)、QTEPromptView.HandleSessionStarted(int, QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="visible">是否可见。</param>
    private void SetVisible(bool visible)
    {
        if (rootGroup == null)
        {
            return;
        }

        // 赋值前保存的目标根节点分组。
global::UnityEngine.CanvasGroup assignmentReceiver10 = rootGroup;
        if (visible)
        {
            assignmentReceiver10.alpha = 1f;
        }
        else
        {
            assignmentReceiver10.alpha = 0f;
        }

        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;
    }

    /// <summary>UpdateKeyLabel 的业务处理入口；本方法直接依赖 string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.HandlePromptAvailabilityChanged(bool)、QTEPromptView.HandleSessionStarted(int, QTEKind)、QTEPromptView.LateUpdate()；下游（含内部回调体） string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="active">是否生效。</param>
    private void UpdateKeyLabel(bool active)
    {
        if (keyLabel == null)
        {
            return;
        }

        // 动作文字。
string actionText;
        if (string.IsNullOrEmpty(currentActionText))
        {
            actionText = "交互";
        }
        else
        {
            actionText = currentActionText;
        }

        keyLabel.text = "F  " + actionText;
        // 赋值前保存的目标键标签。
global::UnityEngine.UI.Text assignmentReceiver13 = keyLabel;
        if (active)
        {
            assignmentReceiver13.color = activeColor;
        }
        else
        {
            assignmentReceiver13.color = inactiveColor;
        }
    }

    /// <summary>FollowTarget 的业务处理入口；本方法直接依赖 Camera.WorldToScreenPoint(Vector3)、Component.GetComponentInChildren&lt;Collider&gt;()、Mathf.Exp(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Lerp(Vector2, Vector2, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.LateUpdate()；下游（含内部回调体） Camera.WorldToScreenPoint(Vector3)、Component.GetComponentInChildren&lt;Collider&gt;()、Mathf.Exp(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FollowTarget()
    {
        if (promptRoot == null || currentTarget == null)
        {
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (worldCamera == null)
        {
            return;
        }

        // 目标碰撞体。
Collider targetCollider = currentTarget.GetComponentInChildren<Collider>();
        // 世界位置。
Vector3 worldPosition;
        if (targetCollider != null)
        {
            worldPosition = targetCollider.bounds.center;
        }
        else
        {
            worldPosition = currentTarget.position + Vector3.up * 1.25f;
        }

        // 屏幕坐标点。
Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            return;
        }

        screenPoint += (Vector3)screenOffset;
        // 画布矩形。
RectTransform canvasRect;
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.transform as RectTransform;
        }
        else
        {
            canvasRect = null;
        }

        // 局部坐标点。
global::UnityEngine.Vector2 localPoint = default;
        // 短路判断得到的分支判断。
bool conditionResult20;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace && canvasRect != null)
        {
            // 按原顺序缓存的实参画布矩形。
global::UnityEngine.RectTransform argumentValue18 = canvasRect;
            // 按原顺序缓存的实参屏幕坐标点。
global::UnityEngine.Vector2 argumentValue19 = screenPoint;
            // 分支选出的父级画布的世界空间摄像机。
global::UnityEngine.Camera selectedValue17;
            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                selectedValue17 = null;
            }
            else
            {
                selectedValue17 = parentCanvas.worldCamera;
            }

            conditionResult20 = RectTransformUtility.ScreenPointToLocalPointInRectangle(argumentValue18, argumentValue19, selectedValue17, out localPoint);
        }
        else
        {
            conditionResult20 = false;
        }

        if (conditionResult20)
        {
            promptRoot.anchoredPosition = Vector2.Lerp(promptRoot.anchoredPosition, localPoint, 1f - Mathf.Exp(-followSmoothSpeed * Time.unscaledDeltaTime));
        }
        else
        {
            promptRoot.position = screenPoint;
        }
    }

    /// <summary>ApplyReferenceStyle 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Transform.SetAsLastSibling()、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Transform.SetAsLastSibling()、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyReferenceStyle()
    {
        if (promptRoot != null)
        {
            promptRoot.sizeDelta = new Vector2(190f, 64f);
            // 面板。
Image panel = promptRoot.GetComponent<Image>();
            if (panel != null)
            {
                panel.color = new Color(0.035f, 0.035f, 0.035f, 0.72f);
                panel.raycastTarget = false;
            }
        }

        if (keyLabel != null)
        {
            keyLabel.rectTransform.anchorMin = keyLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            keyLabel.rectTransform.anchoredPosition = new Vector2(8f, 8f);
            keyLabel.rectTransform.sizeDelta = new Vector2(160f, 38f);
            keyLabel.fontSize = 22;
            keyLabel.alignment = TextAnchor.MiddleCenter;
            keyLabel.raycastTarget = false;
            keyLabel.transform.SetAsLastSibling();
        }

        if (timeLabel != null)
        {
            timeLabel.raycastTarget = false;
            timeLabel.gameObject.SetActive(false);
        }

        if (timeFill != null)
        {
            // 时间矩形。
RectTransform timeRect = timeFill.rectTransform;
            timeRect.anchorMin = timeRect.anchorMax = new Vector2(0.5f, 0.5f);
            timeRect.anchoredPosition = new Vector2(0f, -25f);
            timeRect.sizeDelta = new Vector2(152f, 3f);
            timeFill.raycastTarget = false;
        }

        if (keyIcon != null)
        {
            keyIcon.rectTransform.anchorMin = keyIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            keyIcon.rectTransform.anchoredPosition = new Vector2(-62f, 8f);
            keyIcon.rectTransform.sizeDelta = new Vector2(40f, 40f);
            keyIcon.raycastTarget = false;
        }

        if (progressRing != null)
        {
            progressRing.rectTransform.anchorMin = progressRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            progressRing.rectTransform.anchoredPosition = new Vector2(-62f, 8f);
            progressRing.rectTransform.sizeDelta = new Vector2(48f, 48f);
            progressRing.raycastTarget = false;
        }

        if (progressSegments != null)
        {
            // 当前遍历索引。
for (int i = 0; i < progressSegments.Length; i++)
            {
                if (progressSegments[i] != null)
                {
                    progressSegments[i].raycastTarget = false;
                    progressSegments[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
