using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 波次清空结算面板
/// 从下方滑入，数字跳表动画，2.5秒后自动消失
/// </summary>
public class WaveClearedUI : MonoBehaviour
{
    /// <summary>中文含义：面板。使用位置：WaveClearedUI.HideImmediate()、WaveClearedUI.HideSequence()、WaveClearedUI.Show(int, int) 等共 5 处。</summary>
    [Header("面板引用")]
    public CanvasGroup panel;
    /// <summary>中文含义：面板矩形。使用位置：WaveClearedUI.HideImmediate()、WaveClearedUI.HideSequence()、WaveClearedUI.ShowSequence(int, int) 等共 4 处。</summary>
    public RectTransform panelRect;
    /// <summary>中文含义：分值文字。使用位置：WaveClearedUI.CountUpDisplay(int, int)。</summary>
    [Header("结算文本")]
    public Text scoreText;
    /// <summary>中文含义：击杀数文字。使用位置：WaveClearedUI.CountUpDisplay(int, int)。</summary>
    public Text killsText;
    /// <summary>中文含义：滑移向上持续时间。使用位置：WaveClearedUI.ShowSequence(int, int)。</summary>
    [Header("动效参数")]
    public float slideUpDuration = 0.35f;
    /// <summary>中文含义：数量向上持续时间。使用位置：WaveClearedUI.CountUpDisplay(int, int)。</summary>
    public float countUpDuration = 0.5f;
    /// <summary>中文含义：自动隐藏延迟。使用位置：作为 WaitForSecondsRealtime.WaitForSecondsRealtime(float) 的参数。</summary>
    public float autoHideDelay = 2.5f;
    /// <summary>中文含义：已隐藏时的通知回调。自动消失后的回调</summary>
    [HideInInspector]
    public System.Action onHidden;
    /// <summary>中文含义：原始位置。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、WaveClearedUI.HideImmediate()、WaveClearedUI.HideSequence() 等共 5 处。</summary>
    private Vector2 originalPosition;
    /// <summary>中文含义：自动隐藏协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、WaveClearedUI.HideImmediate()、WaveClearedUI.Show(int, int)。</summary>
    private Coroutine autoHideRoutine;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (panelRect == null && panel != null)
        {
            panelRect = panel.GetComponent<RectTransform>();
        }

        if (panelRect != null)
        {
            originalPosition = panelRect.anchoredPosition;
        }

        if (panel != null)
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }

    /// <summary>显示波次结算</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、WaveClearedUI.ShowSequence(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="score">中文含义：分值。使用位置：作为 WaveClearedUI.ShowSequence(int, int) 的参数。</param>
    /// <param name="kills">中文含义：击杀数。使用位置：作为 WaveClearedUI.ShowSequence(int, int) 的参数。</param>
    public void Show(int score, int kills)
    {
        if (panel == null)
        {
            return;
        }

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
        }

        panel.gameObject.SetActive(true);
        StartCoroutine(ShowSequence(score, kills));
    }

    /// <summary>ShowSequence 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、MonoBehaviour.StartCoroutine(IEnumerator)、Vector2.Lerp(Vector2, Vector2, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)、WaveClearedUI.CountUpDisplay(int, int)、WaveClearedUI.HideSequence()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WaveClearedUI.Show(int, int)；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、MonoBehaviour.StartCoroutine(IEnumerator)、Vector2.Lerp(Vector2, Vector2, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)、WaveClearedUI.CountUpDisplay(int, int)、WaveClearedUI.HideSequence()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="score">中文含义：分值。使用位置：作为 WaveClearedUI.CountUpDisplay(int, int) 的参数。</param>
    /// <param name="kills">中文含义：击杀数。使用位置：作为 WaveClearedUI.CountUpDisplay(int, int) 的参数。</param>
    IEnumerator ShowSequence(int score, int kills)
    {
        // 从下方滑入
        if (panelRect != null)
        {
            // startPos（起始位置）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、WaveClearedUI.ShowSequence(int, int)。
Vector2 startPos = originalPosition + Vector2.down * 120f;
            panelRect.anchoredPosition = startPos;
            // elapsed（已用）：使用位置：WaveClearedUI.ShowSequence(int, int)。
float elapsed = 0f;
            while (elapsed < slideUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Pow(float, float) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、WaveClearedUI.ShowSequence(int, int)。
float t = elapsed / slideUpDuration;
                t = 1f - Mathf.Pow(1f - t, 3f);
                panelRect.anchoredPosition = Vector2.Lerp(startPos, originalPosition, t);
                yield return null;
            }

            panelRect.anchoredPosition = originalPosition;
        }

        // 淡入
        // fadeElapsed（淡入淡出已用）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、WaveClearedUI.ShowSequence(int, int)。
float fadeElapsed = 0f;
        while (fadeElapsed < 0.2f)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(0f, 1f, fadeElapsed / 0.2f);
            yield return null;
        }

        panel.alpha = 1f;
        // 数字跳表
        yield return StartCoroutine(CountUpDisplay(score, kills));
        // 等待后自动消失
        yield return new WaitForSecondsRealtime(autoHideDelay);
        yield return StartCoroutine(HideSequence());
    }

    /// <summary>CountUpDisplay 的业务处理入口；本方法直接依赖 int.ToString()、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、Mathf.RoundToInt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WaveClearedUI.ShowSequence(int, int)；下游（含内部回调体） int.ToString()、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetScore">中文含义：目标分值。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、WaveClearedUI.CountUpDisplay(int, int)。</param>
    /// <param name="targetKills">中文含义：目标击杀数。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、WaveClearedUI.CountUpDisplay(int, int)。</param>
    IEnumerator CountUpDisplay(int targetScore, int targetKills)
    {
        // elapsed（已用）：使用位置：WaveClearedUI.CountUpDisplay(int, int)。
float elapsed = 0f;
        // startScore（起始分值）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float startScore = 0f;
        // startKills（起始击杀数）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float startKills = 0f;
        while (elapsed < countUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、WaveClearedUI.CountUpDisplay(int, int)。
float t = elapsed / countUpDuration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            // currentScore（当前分值）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：WaveClearedUI.CountUpDisplay(int, int)。
int currentScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, t));
            // currentKills（当前击杀数）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：WaveClearedUI.CountUpDisplay(int, int)。
int currentKills = Mathf.RoundToInt(Mathf.Lerp(startKills, targetKills, t));
            if (scoreText != null)
            {
                scoreText.text = currentScore.ToString();
            }

            if (killsText != null)
            {
                killsText.text = currentKills.ToString();
            }

            yield return null;
        }

        if (scoreText != null)
        {
            scoreText.text = targetScore.ToString();
        }

        if (killsText != null)
        {
            killsText.text = targetKills.ToString();
        }
    }

    /// <summary>HideSequence 的业务处理入口；本方法直接依赖 Action.Invoke()、GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、object.ReferenceEquals(object, object)、Vector2.Lerp(Vector2, Vector2, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WaveClearedUI.ShowSequence(int, int)；下游（含内部回调体） Action.Invoke()、GameObject.SetActive(bool)、Mathf.Lerp(float, float, float)、Mathf.Pow(float, float)、object.ReferenceEquals(object, object)、Vector2.Lerp(Vector2, Vector2, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator HideSequence()
    {
        // 向下滑出 + 淡出
        // elapsed（已用）：使用位置：WaveClearedUI.HideSequence()。
float elapsed = 0f;
        // duration（持续时间）：使用位置：WaveClearedUI.HideSequence()。
float duration = 0.3f;
        // startAlpha（起始透明度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float startAlpha = panel.alpha;
        // startPos（起始位置）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、WaveClearedUI.HideSequence()。
Vector2 startPos;
        if (panelRect != null)
        {
            startPos = panelRect.anchoredPosition;
        }
        else
        {
            startPos = Vector2.zero;
        }

        // endPos（结束位置）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
Vector2 endPos = startPos + Vector2.down * 80f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Pow(float, float) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数 等共 4 处。
float t = elapsed / duration;
            t = 1f - Mathf.Pow(1f - t, 3f);
            panel.alpha = Mathf.Lerp(startAlpha, 0f, t);
            if (panelRect != null)
            {
                panelRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            }

            yield return null;
        }

        panel.alpha = 0f;
        panel.gameObject.SetActive(false);
        if (panelRect != null)
        {
            panelRect.anchoredPosition = originalPosition;
        }

        // receiverValue2（判空前缓存的已隐藏时的通知回调）：从已隐藏时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WaveClearedUI.HideSequence()。
global::System.Action receiverValue2 = onHidden;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke();
        }
    }

    /// <summary>立即隐藏（不带动画）</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideImmediate()
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
        }

        if (panel != null)
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = originalPosition;
        }
    }
}
