using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教学操作提示界面，只负责显示按键块、操作文字和淡入淡出。
/// </summary>
public class TutorialPromptUI : MonoBehaviour
{
    /// <summary>提示分组。</summary>
    [Header("界面引用")]
    public CanvasGroup promptGroup;
    /// <summary>面板根节点。</summary>
    public RectTransform panelRoot;
    /// <summary>键容器。</summary>
    public RectTransform keyContainer;
    /// <summary>提示文字。</summary>
    public Text promptText;
    /// <summary>锚定位置。</summary>
    [Header("样式")]
    public Vector2 anchoredPosition = new Vector2(210f, 58f);
    /// <summary>面板留白。</summary>
    public Vector2 panelPadding = new Vector2(18f, 10f);
    /// <summary>键尺寸。</summary>
    public Vector2 keySize = new Vector2(42f, 34f);
    /// <summary>键间距。</summary>
    public float keySpacing = 8f;
    /// <summary>面板颜色。</summary>
    public Color panelColor = new Color(0.03f, 0.03f, 0.03f, 0.58f);
    /// <summary>键颜色。</summary>
    public Color keyColor = new Color(0.12f, 0.12f, 0.12f, 0.76f);
    /// <summary>键边框颜色。</summary>
    public Color keyBorderColor = new Color(0.82f, 0.82f, 0.82f, 0.62f);
    /// <summary>文字颜色。</summary>
    public Color textColor = new Color(0.96f, 0.94f, 0.9f, 1f);
    /// <summary>界面淡入淡出的持续时间。</summary>
    [Header("淡入淡出")]
    [Min(0.01f)]
    public float fadeDuration = 0.18f;
    /// <summary>已生成对象集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    /// <summary>淡入淡出协程。</summary>
    private Coroutine fadeRoutine;
    /// <summary>自动隐藏协程。</summary>
    private Coroutine autoHideRoutine;
    /// <summary>是否具有已显示提示。</summary>
    private bool hasShownPrompt;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） TutorialPromptUI.EnsureRuntimeTree()、TutorialPromptUI.HideImmediate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        EnsureRuntimeTree();
        if (!hasShownPrompt)
        {
            HideImmediate();
        }
    }

    /// <summary>ShowPrompt 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.EnsureRuntimeTree()、TutorialPromptUI.HideAfterSeconds(float)、TutorialPromptUI.RebuildKeys(string[], bool)、TutorialPromptUI.StartFade(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.TryShowAimShootTutorial()、TutorialManager.TryShowMoveTutorial()、TutorialManager.TryShowReloadTutorial()；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.EnsureRuntimeTree()、TutorialPromptUI.HideAfterSeconds(float)、TutorialPromptUI.RebuildKeys(string[], bool)、TutorialPromptUI.StartFade(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="keyLabels">键标签集合。</param>
    /// <param name="actionText">动作文字。</param>
    /// <param name="visibleSeconds">可见秒。</param>
    /// <param name="showPlusSigns">是否显示加号符号集合。</param>
    public void ShowPrompt(string[] keyLabels, string actionText, float visibleSeconds, bool showPlusSigns)
    {
        EnsureRuntimeTree();
        RebuildKeys(keyLabels, showPlusSigns);
        hasShownPrompt = true;
        if (promptText != null)
        {
            promptText.text = actionText;
        }

        if (promptGroup != null)
        {
            promptGroup.gameObject.SetActive(true);
        }

        StartFade(1f);
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
        }

        if (visibleSeconds > 0f)
        {
            autoHideRoutine = StartCoroutine(HideAfterSeconds(visibleSeconds));
        }
        else
        {
            autoHideRoutine = null;
        }
    }

    /// <summary>HideAfter 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.HideAfterSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialManager.CompleteMoveTutorial()；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.HideAfterSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="seconds">秒。</param>
    public void HideAfter(float seconds)
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
        }

        autoHideRoutine = StartCoroutine(HideAfterSeconds(seconds));
    }

    /// <summary>Hide 的业务处理入口；本方法直接依赖 MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.StartFade(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.StartFade(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Hide()
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        StartFade(0f);
    }

    /// <summary>HideImmediate 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.Start()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideImmediate()
    {
        if (promptGroup == null)
        {
            return;
        }

        promptGroup.alpha = 0f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;
        promptGroup.gameObject.SetActive(false);
    }

    /// <summary>EnsureRuntimeTree 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;Canvas&gt;()、GameObject.AddComponent&lt;Canvas&gt;()、GameObject.AddComponent&lt;CanvasScaler&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;ContentSizeFitter&gt;()、GameObject.GetComponent&lt;HorizontalLayoutGroup&gt;() 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.ShowPrompt(string[], string, float, bool)、TutorialPromptUI.Start()；下游（含内部回调体） Component.GetComponentInParent&lt;Canvas&gt;()、GameObject.AddComponent&lt;Canvas&gt;()、GameObject.AddComponent&lt;CanvasScaler&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;ContentSizeFitter&gt;()、GameObject.GetComponent&lt;HorizontalLayoutGroup&gt;() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureRuntimeTree()
    {
        if (promptGroup != null && panelRoot != null && keyContainer != null && promptText != null)
        {
            return;
        }

        // 画布。
Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 180;
            // 缩放器。
CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (promptGroup == null)
        {
            // 分组对象。
GameObject groupObject = new GameObject("教学操作提示", typeof(RectTransform), typeof(CanvasGroup));
            groupObject.transform.SetParent(transform, false);
            promptGroup = groupObject.GetComponent<CanvasGroup>();
            // 分组矩形。
RectTransform groupRect = groupObject.GetComponent<RectTransform>();
            groupRect.anchorMin = new Vector2(0.5f, 0.5f);
            groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.pivot = new Vector2(0.5f, 0.5f);
            groupRect.anchoredPosition = anchoredPosition;
        }

        if (panelRoot == null)
        {
            // 面板对象。
GameObject panelObject = new GameObject("提示框背景", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            panelObject.transform.SetParent(promptGroup.transform, false);
            panelRoot = panelObject.GetComponent<RectTransform>();
            panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            panelRoot.pivot = new Vector2(0.5f, 0.5f);
            // 面板图像。
Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = panelColor;
            panelImage.raycastTarget = false;
            // 布局。
HorizontalLayoutGroup layout = panelObject.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 14f;
            layout.padding = new RectOffset(Mathf.RoundToInt(panelPadding.x), Mathf.RoundToInt(panelPadding.x), Mathf.RoundToInt(panelPadding.y), Mathf.RoundToInt(panelPadding.y));
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            // 尺寸适配器。
ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (keyContainer == null)
        {
            // 键根节点。
GameObject keyRoot = new GameObject("按键组", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            keyRoot.transform.SetParent(panelRoot, false);
            keyContainer = keyRoot.GetComponent<RectTransform>();
            // 键布局。
HorizontalLayoutGroup keyLayout = keyRoot.GetComponent<HorizontalLayoutGroup>();
            keyLayout.childAlignment = TextAnchor.MiddleCenter;
            keyLayout.spacing = keySpacing;
            keyLayout.childControlWidth = true;
            keyLayout.childControlHeight = true;
            keyLayout.childForceExpandWidth = false;
            keyLayout.childForceExpandHeight = false;
        }

        if (promptText == null)
        {
            // 文字对象。
GameObject textObject = new GameObject("操作文字", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(panelRoot, false);
            promptText = textObject.GetComponent<Text>();
            promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            promptText.fontSize = 25;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.color = textColor;
            promptText.raycastTarget = false;
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText.verticalOverflow = VerticalWrapMode.Overflow;
            // 布局元素。
LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.minWidth = 96f;
            layoutElement.minHeight = keySize.y;
        }

        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;
    }

    /// <summary>RebuildKeys 的业务处理入口；本方法直接依赖 TutorialPromptUI.ClearSpawnedObjects()、TutorialPromptUI.CreateKeyBlock(string)、TutorialPromptUI.CreatePlusSign()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.ShowPrompt(string[], string, float, bool)；下游（含内部回调体） TutorialPromptUI.ClearSpawnedObjects()、TutorialPromptUI.CreateKeyBlock(string)、TutorialPromptUI.CreatePlusSign()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="keyLabels">键标签集合。</param>
    /// <param name="showPlusSigns">是否显示加号符号集合。</param>
    private void RebuildKeys(string[] keyLabels, bool showPlusSigns)
    {
        ClearSpawnedObjects();
        if (keyLabels == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < keyLabels.Length; i++)
        {
            CreateKeyBlock(keyLabels[i]);
            if (showPlusSigns && i < keyLabels.Length - 1)
            {
                CreatePlusSign();
            }
        }
    }

    /// <summary>CreateKeyBlock 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;Outline&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.RebuildKeys(string[], bool)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;Outline&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="label">标签。</param>
    private void CreateKeyBlock(string label)
    {
        // 键对象。
GameObject keyObject = new GameObject("按键_" + label, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Outline));
        keyObject.transform.SetParent(keyContainer, false);
        spawnedObjects.Add(keyObject);
        // 背景。
Image background = keyObject.GetComponent<Image>();
        background.color = keyColor;
        background.raycastTarget = false;
        // 轮廓。
Outline outline = keyObject.GetComponent<Outline>();
        outline.effectColor = keyBorderColor;
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        // 布局元素。
LayoutElement layoutElement = keyObject.GetComponent<LayoutElement>();
        // 宽度。
float width;
        if (label != null && label.Length > 1)
        {
            width = Mathf.Max(keySize.x, label.Length * 23f);
        }
        else
        {
            width = keySize.x;
        }

        layoutElement.minWidth = width;
        layoutElement.minHeight = keySize.y;
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = keySize.y;
        // 文字对象。
GameObject textObject = new GameObject("按键文字", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(keyObject.transform, false);
        // 文字矩形。
RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        // 文字。
Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label;
        // 赋值前保存的目标文字。
global::UnityEngine.UI.Text assignmentReceiver3 = text;
        if (label != null && label.Length > 1)
        {
            assignmentReceiver3.fontSize = 18;
        }
        else
        {
            assignmentReceiver3.fontSize = 23;
        }

        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.raycastTarget = false;
    }

    /// <summary>CreatePlusSign 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.RebuildKeys(string[], bool)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;Text&gt;()、List&lt;GameObject&gt;.Add(GameObject)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CreatePlusSign()
    {
        // 加号对象。
GameObject plusObject = new GameObject("加号", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        plusObject.transform.SetParent(keyContainer, false);
        spawnedObjects.Add(plusObject);
        // 文字。
Text text = plusObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "+";
        text.fontSize = 24;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = textColor;
        text.raycastTarget = false;
        // 布局元素。
LayoutElement layoutElement = plusObject.GetComponent<LayoutElement>();
        layoutElement.minWidth = 12f;
        layoutElement.minHeight = keySize.y;
    }

    /// <summary>ClearSpawnedObjects 的业务处理入口；本方法直接依赖 List&lt;GameObject&gt;.Clear()、Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.RebuildKeys(string[], bool)；下游（含内部回调体） List&lt;GameObject&gt;.Clear()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearSpawnedObjects()
    {
        // 当前遍历索引。
for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
    }

    /// <summary>StartFade 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.FadeTo(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.Hide()、TutorialPromptUI.HideAfterSeconds(float)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、TutorialPromptUI.FadeTo(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetAlpha">目标透明度。</param>
    private void StartFade(float targetAlpha)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeTo(targetAlpha));
    }

    /// <summary>HideAfterSeconds 的业务处理入口；本方法直接依赖 TutorialPromptUI.StartFade(float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.HideAfter(float)、TutorialPromptUI.ShowPrompt(string[], string, float, bool)；下游（含内部回调体） TutorialPromptUI.StartFade(float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="seconds">秒。</param>
    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        StartFade(0f);
        autoHideRoutine = null;
    }

    /// <summary>FadeTo 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、Mathf.Approximately(float, float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TutorialPromptUI.StartFade(float)；下游（含内部回调体） GameObject.SetActive(bool)、Mathf.Approximately(float, float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetAlpha">目标透明度。</param>
    private IEnumerator FadeTo(float targetAlpha)
    {
        if (promptGroup == null)
        {
            yield break;
        }

        // 起始透明度。
float startAlpha = promptGroup.alpha;
        // 已用。
float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = Mathf.Clamp01(elapsed / fadeDuration);
            promptGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        promptGroup.alpha = targetAlpha;
        if (Mathf.Approximately(targetAlpha, 0f))
        {
            promptGroup.gameObject.SetActive(false);
        }
    }
}
