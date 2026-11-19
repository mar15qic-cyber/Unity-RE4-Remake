using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 主游戏场景世界交互提示组件
/// 通过 InteractionSystem.FindNearby() 获取最近可交互对象，
/// 在屏幕下方安全区域显示 F 键图片和行为描述文本。
/// 暂停、死亡、打开背包、打开寄存箱时自动隐藏。
/// </summary>
public class WorldInteractionPromptUI : MonoBehaviour
{
    /// <summary>中文含义：提示根对象（CanvasGroup）。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数、WorldInteractionPromptUI.FadeOutAndDeactivate() 等共 6 处。</summary>
    [Header("引用")]
    [Tooltip("提示根对象（CanvasGroup）")]
    public CanvasGroup promptGroup;
    /// <summary>中文含义：F 键图片。使用位置：WorldInteractionPromptUI.ApplyReferenceStyle()、WorldInteractionPromptUI.Start()。</summary>
    [Tooltip("F 键图片")]
    public Image keyIcon;
    /// <summary>中文含义：行为描述文本。使用位置：WorldInteractionPromptUI.ApplyReferenceStyle()、WorldInteractionPromptUI.Show(string, IInteractable)、WorldInteractionPromptUI.Start()。</summary>
    [Tooltip("行为描述文本")]
    public Text promptText;
    /// <summary>中文含义：提示相对物品世界位置的屏幕偏移。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。</summary>
    [Header("物品跟随")]
    [Tooltip("提示相对物品世界位置的屏幕偏移。")]
    public Vector2 itemScreenOffset = new Vector2(0f, 56f);
    /// <summary>中文含义：跟随平滑速度。使用位置：作为 Mathf.Exp(float) 的参数。</summary>
    [Min(1f)]
    public float followSmoothSpeed = 18f;
    /// <summary>中文含义：淡入淡出速度。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。</summary>
    [Header("淡入淡出")]
    public float fadeSpeed = 8f;
    /// <summary>中文含义：淡入淡出协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、WorldInteractionPromptUI.Hide()、WorldInteractionPromptUI.Show(string, IInteractable)。</summary>
    private Coroutine fadeRoutine;
    /// <summary>中文含义：是否已显示。使用位置：WorldInteractionPromptUI.Hide()、WorldInteractionPromptUI.Show(string, IInteractable)、WorldInteractionPromptUI.Update()。</summary>
    private bool isShown;
    /// <summary>中文含义：提示矩形。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、WorldInteractionPromptUI.ApplyReferenceStyle()、WorldInteractionPromptUI.FollowInteractable(IInteractable) 等共 4 处。</summary>
    private RectTransform promptRect;
    /// <summary>中文含义：父级画布。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)、WorldInteractionPromptUI.Start()。</summary>
    private Canvas parentCanvas;
    /// <summary>中文含义：世界空间摄像机。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)、WorldInteractionPromptUI.Start()。</summary>
    private Camera worldCamera;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、Component.GetComponentInParent&lt;Canvas&gt;()、WorldInteractionPromptUI.ApplyReferenceStyle()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (promptGroup != null)
        {
            promptRect = promptGroup.GetComponent<RectTransform>();
            parentCanvas = promptGroup.GetComponentInParent<Canvas>();
        }

        worldCamera = Camera.main;
        ApplyReferenceStyle();
        if (promptGroup != null)
        {
            promptGroup.alpha = 0f;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
        }

        // 关闭不必要的 raycastTarget
        if (keyIcon != null)
        {
            keyIcon.raycastTarget = false;
        }

        if (promptText != null)
        {
            promptText.raycastTarget = false;
        }
    }

    /// <summary>ApplyReferenceStyle 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyReferenceStyle()
    {
        if (promptRect != null)
        {
            promptRect.sizeDelta = new Vector2(240f, 56f);
            // panel（面板）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：WorldInteractionPromptUI.ApplyReferenceStyle()。
Image panel = promptRect.GetComponent<Image>();
            if (panel != null)
            {
                panel.color = new Color(0.035f, 0.035f, 0.035f, 0.68f);
            }
        }

        if (keyIcon != null)
        {
            keyIcon.rectTransform.anchoredPosition = new Vector2(30f, -28f);
            keyIcon.rectTransform.sizeDelta = new Vector2(40f, 40f);
            keyIcon.preserveAspect = true;
        }

        if (promptText != null)
        {
            promptText.rectTransform.anchoredPosition = new Vector2(142f, -28f);
            promptText.rectTransform.sizeDelta = new Vector2(180f, 40f);
            promptText.fontSize = 20;
            promptText.alignment = TextAnchor.MiddleLeft;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IInteractable.GetPromptText()、InteractionSystem.FindNearby(Transform)、string.IsNullOrEmpty(string)、WorldInteractionPromptUI.Hide()、WorldInteractionPromptUI.IsUIBlocking()、WorldInteractionPromptUI.Show(string, IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (IsUIBlocking())
        {
            if (isShown)
            {
                Hide();
            }

            return;
        }

        // player（玩家）：使用位置：作为 InteractionSystem.FindNearby(Transform) 的参数、WorldInteractionPromptUI.Update()。
var player = LeonController.Instance;
        if (player == null)
        {
            if (isShown)
            {
                Hide();
            }

            return;
        }

        // nearby（附近）：保存 InteractionSystem.FindNearby(Transform) 的返回结果，供当前步骤使用。使用位置：作为 WorldInteractionPromptUI.Show(string, IInteractable) 的参数、WorldInteractionPromptUI.Update()。
var nearby = InteractionSystem.FindNearby(player.transform);
        if (nearby != null)
        {
            // text（文字）：保存 IInteractable.GetPromptText() 的返回结果，供当前步骤使用。使用位置：作为 string.IsNullOrEmpty(string) 的参数、作为 WorldInteractionPromptUI.Show(string, IInteractable) 的参数。
string text = nearby.GetPromptText();
            if (string.IsNullOrEmpty(text))
            {
                if (isShown)
                {
                    Hide();
                }

                return;
            }

            Show(text, nearby, player);
        }
        else
        {
            if (isShown)
            {
                Hide();
            }
        }
    }

    /// <summary>检查 UI 是否阻断游戏交互</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；下游（含内部回调体） GameUIManager.CanPlayerAct()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool IsUIBlocking()
    {
        if (GameManager.HasInstance && (GameManager.Instance.IsTerminal || !GameManager.Instance.Permission.Current.AllowInput))
        {
            return true;
        }

        if (GameUIManager.HasInstance && !GameUIManager.Instance.CanPlayerAct())
        {
            return true;
        }

        if (InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
        {
            return true;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return true;
        }

        return false;
    }

    /// <summary>Show 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、WorldInteractionPromptUI.FadeTo(float)、WorldInteractionPromptUI.FollowInteractable(IInteractable)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、WorldInteractionPromptUI.FadeTo(float)、WorldInteractionPromptUI.FollowInteractable(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：WorldInteractionPromptUI.Show(string, IInteractable)。</param>
    /// <param name="interactable">中文含义：可交互。使用位置：作为 WorldInteractionPromptUI.FollowInteractable(IInteractable) 的参数。</param>
    void Show(string text, IInteractable interactable, LeonController player)
    {
        bool showKeyIcon = true;
        if (interactable is VillageLadderSite && player.TryGetComponent<LeonInputBridge>(out var bridge))
        {
            string binding = bridge.ReadInteractBindingLabel();
            if (!string.IsNullOrWhiteSpace(binding) &&
                (!string.Equals(binding, "F", System.StringComparison.OrdinalIgnoreCase) || keyIcon == null || keyIcon.sprite == null))
            {
                text = "[" + binding + "] " + text;
                showKeyIcon = false;
            }
        }

        if (keyIcon != null)
        {
            keyIcon.enabled = showKeyIcon;
        }

        if (promptText != null)
        {
            promptText.text = text;
        }

        FollowInteractable(interactable);
        if (!isShown)
        {
            isShown = true;
            if (promptGroup != null)
            {
                promptGroup.gameObject.SetActive(true);
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeTo(1f));
        }
    }

    /// <summary>FollowInteractable 的业务处理入口；本方法直接依赖 Camera.WorldToScreenPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Mathf.Exp(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Lerp(Vector2, Vector2, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Show(string, IInteractable)；下游（含内部回调体） Camera.WorldToScreenPoint(Vector3)、Component.GetComponent&lt;Collider&gt;()、Mathf.Exp(float)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactable">中文含义：可交互。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。</param>
    void FollowInteractable(IInteractable interactable)
    {
        if (promptRect == null || interactable is not Component /* 中文含义：组件。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ component)
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

        // targetCollider（目标碰撞体）：保存 Component.GetComponent<Collider>() 的返回结果，供当前步骤使用。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。
Collider targetCollider = component.GetComponent<Collider>();
        // worldPosition（世界位置）：使用位置：作为 Camera.WorldToScreenPoint(Vector3) 的参数、WorldInteractionPromptUI.FollowInteractable(IInteractable)。
Vector3 worldPosition;
        if (interactable is VillageLadderSite ladder)
        {
            worldPosition = ladder.PromptPosition;
        }
        else if (targetCollider != null)
        {
            worldPosition = targetCollider.bounds.center;
        }
        else
        {
            worldPosition = component.transform.position;
        }

        // screenPoint（屏幕坐标点）：保存 Camera.WorldToScreenPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。
Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
        if (interactable is VillageLadderSite && (screenPoint.z <= 0f ||
            screenPoint.x < 125f || screenPoint.x > Screen.width - 125f ||
            screenPoint.y < 40f || screenPoint.y > Screen.height - 105f))
        {
            screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.18f, 1f);
        }
        else if (screenPoint.z <= 0f)
        {
            return;
        }

        screenPoint += (Vector3)itemScreenOffset;
        // canvasRect（画布矩形）：使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。
RectTransform canvasRect;
        if (parentCanvas != null)
        {
            canvasRect = parentCanvas.transform as RectTransform;
        }
        else
        {
            canvasRect = null;
        }

        // localPoint（局部坐标点）：使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
global::UnityEngine.Vector2 localPoint = default;
        // conditionResult6（短路判断得到的分支判断）：保存短路分支结果，未选中的分支不执行。使用位置：WorldInteractionPromptUI.FollowInteractable(IInteractable)。
bool conditionResult6;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace && canvasRect != null)
        {
            // argumentValue4（按原顺序缓存的实参画布矩形）：从画布矩形读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数。
global::UnityEngine.RectTransform argumentValue4 = canvasRect;
            // argumentValue5（按原顺序缓存的实参屏幕坐标点）：从屏幕坐标点读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数。
global::UnityEngine.Vector2 argumentValue5 = screenPoint;
            // selectedValue3（分支选出的父级画布的世界空间摄像机）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、WorldInteractionPromptUI.FollowInteractable(IInteractable)。
global::UnityEngine.Camera selectedValue3;
            if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                selectedValue3 = null;
            }
            else
            {
                selectedValue3 = parentCanvas.worldCamera;
            }

            conditionResult6 = RectTransformUtility.ScreenPointToLocalPointInRectangle(argumentValue4, argumentValue5, selectedValue3, out localPoint);
        }
        else
        {
            conditionResult6 = false;
        }

        if (conditionResult6)
        {
            if (!isShown && interactable is VillageLadderSite)
            {
                promptRect.anchoredPosition = localPoint;
                return;
            }

            promptRect.anchoredPosition = Vector2.Lerp(promptRect.anchoredPosition, localPoint, 1f - Mathf.Exp(-followSmoothSpeed * Time.unscaledDeltaTime));
        }
        else
        {
            promptRect.position = screenPoint;
        }
    }

    /// <summary>Hide 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、WorldInteractionPromptUI.FadeOutAndDeactivate()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Update()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、WorldInteractionPromptUI.FadeOutAndDeactivate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Hide()
    {
        isShown = false;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOutAndDeactivate());
    }

    /// <summary>FadeTo 的业务处理入口；本方法直接依赖 Mathf.Approximately(float, float)、Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Show(string, IInteractable)；下游（含内部回调体） Mathf.Approximately(float, float)、Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数、WorldInteractionPromptUI.FadeTo(float)。</param>
    IEnumerator FadeTo(float target)
    {
        if (promptGroup == null)
        {
            yield break;
        }

        while (!Mathf.Approximately(promptGroup.alpha, target))
        {
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        promptGroup.alpha = target;
    }

    /// <summary>FadeOutAndDeactivate 的业务处理入口；本方法直接依赖 Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WorldInteractionPromptUI.Hide()；下游（含内部回调体） Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOutAndDeactivate()
    {
        if (promptGroup == null)
        {
            yield break;
        }

        while (promptGroup.alpha > 0.01f)
        {
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, 0f, fadeSpeed * Time.unscaledDeltaTime);
            yield return null;
        }

        promptGroup.alpha = 0f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;
    }
}
