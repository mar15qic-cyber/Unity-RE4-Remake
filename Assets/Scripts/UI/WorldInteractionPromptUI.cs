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
    /// <summary>提示根对象（CanvasGroup）。</summary>
    [Header("引用")]
    [Tooltip("提示根对象（CanvasGroup）")]
    public CanvasGroup promptGroup;
    /// <summary>F 键图片。</summary>
    [Tooltip("F 键图片")]
    public Image keyIcon;
    /// <summary>行为描述文本。</summary>
    [Tooltip("行为描述文本")]
    public Text promptText;
    /// <summary>提示相对物品世界位置的屏幕偏移。</summary>
    [Header("物品跟随")]
    [Tooltip("提示相对物品世界位置的屏幕偏移。")]
    public Vector2 itemScreenOffset = new Vector2(0f, 56f);
    /// <summary>跟随平滑速度。</summary>
    [Min(1f)]
    public float followSmoothSpeed = 18f;
    /// <summary>淡入淡出速度。</summary>
    [Header("淡入淡出")]
    public float fadeSpeed = 8f;
    /// <summary>淡入淡出协程。</summary>
    private Coroutine fadeRoutine;
    /// <summary>是否已显示。</summary>
    private bool isShown;
    /// <summary>提示矩形。</summary>
    private RectTransform promptRect;
    /// <summary>父级画布。</summary>
    private Canvas parentCanvas;
    /// <summary>世界空间摄像机。</summary>
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
            // 面板。
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

        // 玩家。
var player = LeonController.Instance;
        if (player == null)
        {
            if (isShown)
            {
                Hide();
            }

            return;
        }

        // 附近。
var nearby = InteractionSystem.FindNearby(player.transform);
        if (nearby != null)
        {
            // 文字。
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
    /// <param name="text">文字。</param>
    /// <param name="interactable">可交互。</param>
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
    /// <param name="interactable">可交互。</param>
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

        // 目标碰撞体。
Collider targetCollider = component.GetComponent<Collider>();
        // 世界位置。
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

        // 屏幕坐标点。
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
bool conditionResult6;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace && canvasRect != null)
        {
            // 按原顺序缓存的实参画布矩形。
global::UnityEngine.RectTransform argumentValue4 = canvasRect;
            // 按原顺序缓存的实参屏幕坐标点。
global::UnityEngine.Vector2 argumentValue5 = screenPoint;
            // 分支选出的父级画布的世界空间摄像机。
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
    /// <param name="target">目标。</param>
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
