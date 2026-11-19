using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// 菜单动画控制器
/// 管理菜单项的悬停、选择、过渡动画
/// 支持DOTween以外的纯协程动画
/// </summary>
public class MenuAnimationController : MonoBehaviour
{
    /// <summary>中文含义：悬停缩放倍率。使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。</summary>
    [Header("悬停动画")]
    public float hoverScaleMultiplier = 1.1f;
    /// <summary>中文含义：悬停持续时间。使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。</summary>
    public float hoverDuration = 0.2f;
    /// <summary>中文含义：悬停曲线。使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)、MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。</summary>
    public AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>中文含义：选择冲击效果缩放。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。</summary>
    [Header("选择动画")]
    public float selectPunchScale = 1.2f;
    /// <summary>中文含义：选择持续时间。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。</summary>
    public float selectDuration = 0.3f;
    /// <summary>中文含义：选择曲线。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。</summary>
    public AnimationCurve selectCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>中文含义：界面淡入淡出的持续时间。使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数。</summary>
    [Header("过渡动画")]
    public float fadeDuration = 0.3f;
    /// <summary>中文含义：滑移持续时间。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</summary>
    public float slideDuration = 0.4f;
    /// <summary>中文含义：滑移曲线。使用位置：MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</summary>
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>中文含义：高亮颜色。</summary>
    [Header("颜色动画")]
    public Color highlightColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>中文含义：普通状态颜色。</summary>
    public Color normalColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>中文含义：颜色过渡时长。使用位置：作为 MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string) 的参数。</summary>
    public float colorTransitionDuration = 0.2f;
    // 私有状态
    /// <summary>中文含义：生效动画集合。使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)、MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string) 等共 8 处。</summary>
    private Hashtable activeAnimations = new Hashtable();
    /// <summary>
    /// 悬停动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：作为 MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string) 的参数、MenuAnimationController.PlayHoverAnimation(GameObject, bool)。</param>
    /// <param name="isHovering">中文含义：是否悬停中。使用位置：作为 MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string) 的参数。</param>
    public void PlayHoverAnimation(GameObject target, bool isHovering)
    {
        if (target == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "hover_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(HoverAnimationCoroutine(target, isHovering, animationId));
    }

    /// <summary>
    /// 悬停动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.PlayHoverAnimation(GameObject, bool)；下游（含内部回调体） AnimationCurve.Evaluate(float)、GameObject.GetComponent&lt;RectTransform&gt;()、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。</param>
    /// <param name="isHovering">中文含义：是否悬停中。使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。</param>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数、作为 Hashtable.this[object] 的参数。</param>
    private IEnumerator HoverAnimationCoroutine(GameObject target, bool isHovering, string animationId)
    {
        activeAnimations[animationId] = true;
        // rectTransform（界面矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。
RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            yield break;
        }

        // startScale（起始缩放）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 startScale = rectTransform.localScale;
        // targetScale（目标缩放）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。
Vector3 targetScale;
        if (isHovering)
        {
            targetScale = Vector3.one * hoverScaleMultiplier;
        }
        else
        {
            targetScale = Vector3.one;
        }

        // elapsed（已用）：使用位置：MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。
float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)。
float t = elapsed / hoverDuration;
            t = hoverCurve.Evaluate(t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        rectTransform.localScale = targetScale;
        activeAnimations.Remove(animationId);
    }

    /// <summary>
    /// 选择动画（按下效果）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：作为 MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string) 的参数、MenuAnimationController.PlaySelectAnimation(GameObject, Action)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string) 的参数。</param>
    public void PlaySelectAnimation(GameObject target, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "select_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SelectAnimationCoroutine(target, onComplete, animationId));
    }

    /// <summary>
    /// 选择动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.PlaySelectAnimation(GameObject, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、GameObject.GetComponent&lt;RectTransform&gt;()、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。</param>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数、作为 Hashtable.this[object] 的参数。</param>
    private IEnumerator SelectAnimationCoroutine(GameObject target, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        // rectTransform（界面矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            // receiverValue2（判空前缓存的完成回调）：从完成回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
global::System.Action receiverValue2 = onComplete;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.Invoke();
            }

            yield break;
        }

        // originalScale（原始缩放）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
Vector3 originalScale = Vector3.one;
        // punchScale（冲击效果缩放）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 punchScale = originalScale * selectPunchScale;
        // 放大阶段
        // elapsed（已用）：使用位置：MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
float elapsed = 0f;
        while (elapsed < selectDuration * 0.5f)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
float t = elapsed / (selectDuration * 0.5f);
            t = selectCurve.Evaluate(t);
            rectTransform.localScale = Vector3.Lerp(originalScale, punchScale, t);
            yield return null;
        }

        // 缩小阶段
        elapsed = 0f;
        while (elapsed < selectDuration * 0.5f)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
float t = elapsed / (selectDuration * 0.5f);
            t = selectCurve.Evaluate(t);
            rectTransform.localScale = Vector3.Lerp(punchScale, originalScale, t);
            yield return null;
        }

        rectTransform.localScale = originalScale;
        activeAnimations.Remove(animationId);
        // receiverValue3（判空前缓存的完成回调）：从完成回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MenuAnimationController.SelectAnimationCoroutine(GameObject, Action, string)。
global::System.Action receiverValue3 = onComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Invoke();
        }
    }

    /// <summary>
    /// 淡入动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="canvasGroup">中文含义：画布组。使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数、MenuAnimationController.FadeIn(CanvasGroup, Action)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数。</param>
    public void FadeIn(CanvasGroup canvasGroup, Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "fade_in_" + canvasGroup.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(FadeAnimationCoroutine(canvasGroup, 0f, 1f, fadeDuration, onComplete, animationId));
    }

    /// <summary>
    /// 淡出动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="canvasGroup">中文含义：画布组。使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数、MenuAnimationController.FadeOut(CanvasGroup, Action)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数。</param>
    public void FadeOut(CanvasGroup canvasGroup, Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "fade_out_" + canvasGroup.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(FadeAnimationCoroutine(canvasGroup, 1f, 0f, fadeDuration, onComplete, animationId));
    }

    /// <summary>
    /// 淡入淡出动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.FadeIn(CanvasGroup, Action)、MenuAnimationController.FadeOut(CanvasGroup, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、Mathf.Lerp(float, float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="canvasGroup">中文含义：画布组。使用位置：MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。</param>
    /// <param name="from">中文含义：来源。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。</param>
    /// <param name="to">中文含义：目标。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。</param>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数、作为 Hashtable.this[object] 的参数。</param>
    private IEnumerator FadeAnimationCoroutine(CanvasGroup canvasGroup, float from, float to, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        canvasGroup.alpha = from;
        canvasGroup.interactable = (to > 0.5f);
        canvasGroup.blocksRaycasts = (to > 0.5f);
        // elapsed（已用）：使用位置：MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。
float t = elapsed / duration;
            t = hoverCurve.Evaluate(t);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        activeAnimations.Remove(animationId);
        // receiverValue4（判空前缓存的完成回调）：从完成回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)。
global::System.Action receiverValue4 = onComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Invoke();
        }
    }

    /// <summary>
    /// 滑入动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标界面矩形变换。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数、MenuAnimationController.SlideIn(RectTransform, Vector2, Vector2, Action)。</param>
    /// <param name="fromPosition">中文含义：来源位置。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    /// <param name="toPosition">中文含义：目标位置。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    public void SlideIn(RectTransform target, Vector2 fromPosition, Vector2 toPosition, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "slide_in_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SlideAnimationCoroutine(target, fromPosition, toPosition, slideDuration, onComplete, animationId));
    }

    /// <summary>
    /// 滑出动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标界面矩形变换。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数、MenuAnimationController.SlideOut(RectTransform, Vector2, Vector2, Action)。</param>
    /// <param name="fromPosition">中文含义：来源位置。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    /// <param name="toPosition">中文含义：目标位置。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数。</param>
    public void SlideOut(RectTransform target, Vector2 fromPosition, Vector2 toPosition, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "slide_out_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SlideAnimationCoroutine(target, fromPosition, toPosition, slideDuration, onComplete, animationId));
    }

    /// <summary>
    /// 滑动动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.SlideIn(RectTransform, Vector2, Vector2, Action)、MenuAnimationController.SlideOut(RectTransform, Vector2, Vector2, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标界面矩形变换。使用位置：MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</param>
    /// <param name="from">中文含义：来源。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</param>
    /// <param name="to">中文含义：目标。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。</param>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数、作为 Hashtable.this[object] 的参数。</param>
    private IEnumerator SlideAnimationCoroutine(RectTransform target, Vector2 from, Vector2 to, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        target.anchoredPosition = from;
        // elapsed（已用）：使用位置：MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。
float t = elapsed / duration;
            t = slideCurve.Evaluate(t);
            target.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        target.anchoredPosition = to;
        activeAnimations.Remove(animationId);
        // receiverValue5（判空前缓存的完成回调）：从完成回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)。
global::System.Action receiverValue5 = onComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Invoke();
        }
    }

    /// <summary>
    /// 颜色渐变动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textComponent">中文含义：文字组件。使用位置：作为 MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string) 的参数、MenuAnimationController.ColorTransition(Text, Color, Action)。</param>
    /// <param name="targetColor">中文含义：目标颜色。使用位置：作为 MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string) 的参数。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：作为 MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string) 的参数。</param>
    public void ColorTransition(Text textComponent, Color targetColor, Action onComplete = null)
    {
        if (textComponent == null)
        {
            return;
        }

        // animationId（动画编号）：使用位置：作为 MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string) 的参数、作为 MenuAnimationController.StopAnimation(string) 的参数。
string animationId = "color_" + textComponent.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(ColorTransitionCoroutine(textComponent, targetColor, colorTransitionDuration, onComplete, animationId));
    }

    /// <summary>
    /// 颜色渐变动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.ColorTransition(Text, Color, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Color.Lerp(Color, Color, float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textComponent">中文含义：文字组件。使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。</param>
    /// <param name="targetColor">中文含义：目标颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。</param>
    /// <param name="onComplete">中文含义：完成回调。使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。</param>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数、作为 Hashtable.this[object] 的参数。</param>
    private IEnumerator ColorTransitionCoroutine(Text textComponent, Color targetColor, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        // startColor（起始颜色）：使用位置：作为 Color.Lerp(Color, Color, float) 的参数。
Color startColor = textComponent.color;
        // elapsed（已用）：使用位置：MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、作为 Color.Lerp(Color, Color, float) 的参数、MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。
float t = elapsed / duration;
            t = hoverCurve.Evaluate(t);
            textComponent.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        textComponent.color = targetColor;
        activeAnimations.Remove(animationId);
        // receiverValue6（判空前缓存的完成回调）：从完成回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、MenuAnimationController.ColorTransitionCoroutine(Text, Color, float, Action, string)。
global::System.Action receiverValue6 = onComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Invoke();
        }
    }

    /// <summary>
    /// 停止指定动画
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.ColorTransition(Text, Color, Action)、MenuAnimationController.FadeIn(CanvasGroup, Action)、MenuAnimationController.FadeOut(CanvasGroup, Action)、MenuAnimationController.PlayHoverAnimation(GameObject, bool)、MenuAnimationController.PlaySelectAnimation(GameObject, Action) 等共 7 处；下游（含内部回调体） Hashtable.ContainsKey(object)、Hashtable.Remove(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数、作为 Hashtable.Remove(object) 的参数。</param>
    public void StopAnimation(string animationId)
    {
        if (activeAnimations.ContainsKey(animationId))
        {
            activeAnimations.Remove(animationId);
        }
    }

    /// <summary>
    /// 停止所有动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Hashtable.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopAllAnimations()
    {
        activeAnimations.Clear();
    }

    /// <summary>
    /// 检查动画是否正在播放
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Hashtable.ContainsKey(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animationId">中文含义：动画编号。使用位置：作为 Hashtable.ContainsKey(object) 的参数。</param>
    public bool IsAnimationPlaying(string animationId)
    {
        return activeAnimations.ContainsKey(animationId);
    }
}
