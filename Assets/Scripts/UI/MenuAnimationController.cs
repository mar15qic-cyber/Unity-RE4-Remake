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
    /// <summary>悬停缩放倍率。</summary>
    [Header("悬停动画")]
    public float hoverScaleMultiplier = 1.1f;
    /// <summary>悬停持续时间。</summary>
    public float hoverDuration = 0.2f;
    /// <summary>悬停曲线。</summary>
    public AnimationCurve hoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>选择冲击效果缩放。</summary>
    [Header("选择动画")]
    public float selectPunchScale = 1.2f;
    /// <summary>选择持续时间。</summary>
    public float selectDuration = 0.3f;
    /// <summary>选择曲线。</summary>
    public AnimationCurve selectCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>界面淡入淡出的持续时间。</summary>
    [Header("过渡动画")]
    public float fadeDuration = 0.3f;
    /// <summary>滑移持续时间。</summary>
    public float slideDuration = 0.4f;
    /// <summary>滑移曲线。</summary>
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    /// <summary>中文含义：高亮颜色。</summary>
    [Header("颜色动画")]
    public Color highlightColor = new Color(0.85f, 0.1f, 0.1f, 1f);
    /// <summary>中文含义：普通状态颜色。</summary>
    public Color normalColor = new Color(1f, 1f, 1f, 0.7f);
    /// <summary>颜色过渡时长。</summary>
    public float colorTransitionDuration = 0.2f;
    // 私有状态
    /// <summary>生效动画集合。</summary>
    private Hashtable activeAnimations = new Hashtable();
    /// <summary>
    /// 悬停动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.HoverAnimationCoroutine(GameObject, bool, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标游戏对象。</param>
    /// <param name="isHovering">是否悬停中。</param>
    public void PlayHoverAnimation(GameObject target, bool isHovering)
    {
        if (target == null)
        {
            return;
        }

        // 动画编号。
string animationId = "hover_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(HoverAnimationCoroutine(target, isHovering, animationId));
    }

    /// <summary>
    /// 悬停动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.PlayHoverAnimation(GameObject, bool)；下游（含内部回调体） AnimationCurve.Evaluate(float)、GameObject.GetComponent&lt;RectTransform&gt;()、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标游戏对象。</param>
    /// <param name="isHovering">是否悬停中。</param>
    /// <param name="animationId">动画编号。</param>
    private IEnumerator HoverAnimationCoroutine(GameObject target, bool isHovering, string animationId)
    {
        activeAnimations[animationId] = true;
        // 界面矩形变换。
RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            yield break;
        }

        // 起始缩放。
Vector3 startScale = rectTransform.localScale;
        // 目标缩放。
Vector3 targetScale;
        if (isHovering)
        {
            targetScale = Vector3.one * hoverScaleMultiplier;
        }
        else
        {
            targetScale = Vector3.one;
        }

        // 已用。
float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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
    /// <param name="target">目标游戏对象。</param>
    /// <param name="onComplete">完成回调。</param>
    public void PlaySelectAnimation(GameObject target, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // 动画编号。
string animationId = "select_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SelectAnimationCoroutine(target, onComplete, animationId));
    }

    /// <summary>
    /// 选择动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.PlaySelectAnimation(GameObject, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、GameObject.GetComponent&lt;RectTransform&gt;()、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标游戏对象。</param>
    /// <param name="onComplete">完成回调。</param>
    /// <param name="animationId">动画编号。</param>
    private IEnumerator SelectAnimationCoroutine(GameObject target, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        // 界面矩形变换。
RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            // 判空前缓存的完成回调。
global::System.Action receiverValue2 = onComplete;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.Invoke();
            }

            yield break;
        }

        // 原始缩放。
Vector3 originalScale = Vector3.one;
        // 冲击效果缩放。
Vector3 punchScale = originalScale * selectPunchScale;
        // 放大阶段
        // 已用。
float elapsed = 0f;
        while (elapsed < selectDuration * 0.5f)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
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
            // 当前界面动画的归一化进度。
float t = elapsed / (selectDuration * 0.5f);
            t = selectCurve.Evaluate(t);
            rectTransform.localScale = Vector3.Lerp(punchScale, originalScale, t);
            yield return null;
        }

        rectTransform.localScale = originalScale;
        activeAnimations.Remove(animationId);
        // 判空前缓存的完成回调。
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
    /// <param name="canvasGroup">画布组。</param>
    /// <param name="onComplete">完成回调。</param>
    public void FadeIn(CanvasGroup canvasGroup, Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            return;
        }

        // 动画编号。
string animationId = "fade_in_" + canvasGroup.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(FadeAnimationCoroutine(canvasGroup, 0f, 1f, fadeDuration, onComplete, animationId));
    }

    /// <summary>
    /// 淡出动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.FadeAnimationCoroutine(CanvasGroup, float, float, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="canvasGroup">画布组。</param>
    /// <param name="onComplete">完成回调。</param>
    public void FadeOut(CanvasGroup canvasGroup, Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            return;
        }

        // 动画编号。
string animationId = "fade_out_" + canvasGroup.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(FadeAnimationCoroutine(canvasGroup, 1f, 0f, fadeDuration, onComplete, animationId));
    }

    /// <summary>
    /// 淡入淡出动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.FadeIn(CanvasGroup, Action)、MenuAnimationController.FadeOut(CanvasGroup, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、Mathf.Lerp(float, float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="canvasGroup">画布组。</param>
    /// <param name="from">来源。</param>
    /// <param name="to">目标。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="onComplete">完成回调。</param>
    /// <param name="animationId">动画编号。</param>
    private IEnumerator FadeAnimationCoroutine(CanvasGroup canvasGroup, float from, float to, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        canvasGroup.alpha = from;
        canvasGroup.interactable = (to > 0.5f);
        canvasGroup.blocksRaycasts = (to > 0.5f);
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / duration;
            t = hoverCurve.Evaluate(t);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
        activeAnimations.Remove(animationId);
        // 判空前缓存的完成回调。
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
    /// <param name="target">目标界面矩形变换。</param>
    /// <param name="fromPosition">来源位置。</param>
    /// <param name="toPosition">目标位置。</param>
    /// <param name="onComplete">完成回调。</param>
    public void SlideIn(RectTransform target, Vector2 fromPosition, Vector2 toPosition, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // 动画编号。
string animationId = "slide_in_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SlideAnimationCoroutine(target, fromPosition, toPosition, slideDuration, onComplete, animationId));
    }

    /// <summary>
    /// 滑出动画
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MenuAnimationController.SlideAnimationCoroutine(RectTransform, Vector2, Vector2, float, Action, string)、MenuAnimationController.StopAnimation(string)、MonoBehaviour.StartCoroutine(IEnumerator)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标界面矩形变换。</param>
    /// <param name="fromPosition">来源位置。</param>
    /// <param name="toPosition">目标位置。</param>
    /// <param name="onComplete">完成回调。</param>
    public void SlideOut(RectTransform target, Vector2 fromPosition, Vector2 toPosition, Action onComplete = null)
    {
        if (target == null)
        {
            return;
        }

        // 动画编号。
string animationId = "slide_out_" + target.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(SlideAnimationCoroutine(target, fromPosition, toPosition, slideDuration, onComplete, animationId));
    }

    /// <summary>
    /// 滑动动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.SlideIn(RectTransform, Vector2, Vector2, Action)、MenuAnimationController.SlideOut(RectTransform, Vector2, Vector2, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标界面矩形变换。</param>
    /// <param name="from">来源。</param>
    /// <param name="to">目标。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="onComplete">完成回调。</param>
    /// <param name="animationId">动画编号。</param>
    private IEnumerator SlideAnimationCoroutine(RectTransform target, Vector2 from, Vector2 to, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        target.anchoredPosition = from;
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / duration;
            t = slideCurve.Evaluate(t);
            target.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        target.anchoredPosition = to;
        activeAnimations.Remove(animationId);
        // 判空前缓存的完成回调。
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
    /// <param name="textComponent">文字组件。</param>
    /// <param name="targetColor">目标颜色。</param>
    /// <param name="onComplete">完成回调。</param>
    public void ColorTransition(Text textComponent, Color targetColor, Action onComplete = null)
    {
        if (textComponent == null)
        {
            return;
        }

        // 动画编号。
string animationId = "color_" + textComponent.GetInstanceID();
        StopAnimation(animationId);
        StartCoroutine(ColorTransitionCoroutine(textComponent, targetColor, colorTransitionDuration, onComplete, animationId));
    }

    /// <summary>
    /// 颜色渐变动画协程
    /// </summary>
    /// <remarks>静态调用链：上游 MenuAnimationController.ColorTransition(Text, Color, Action)；下游（含内部回调体） Action.Invoke()、AnimationCurve.Evaluate(float)、Color.Lerp(Color, Color, float)、Hashtable.ContainsKey(object)、Hashtable.Remove(object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textComponent">文字组件。</param>
    /// <param name="targetColor">目标颜色。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="onComplete">完成回调。</param>
    /// <param name="animationId">动画编号。</param>
    private IEnumerator ColorTransitionCoroutine(Text textComponent, Color targetColor, float duration, Action onComplete, string animationId)
    {
        activeAnimations[animationId] = true;
        // 起始颜色。
Color startColor = textComponent.color;
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!activeAnimations.ContainsKey(animationId))
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / duration;
            t = hoverCurve.Evaluate(t);
            textComponent.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        textComponent.color = targetColor;
        activeAnimations.Remove(animationId);
        // 判空前缓存的完成回调。
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
    /// <param name="animationId">动画编号。</param>
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
    /// <param name="animationId">动画编号。</param>
    public bool IsAnimationPlaying(string animationId)
    {
        return activeAnimations.ContainsKey(animationId);
    }
}
