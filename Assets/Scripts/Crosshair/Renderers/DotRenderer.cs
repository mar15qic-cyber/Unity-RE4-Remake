using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单点准心渲染器 — Dot 策略
/// 负责：单个 Image 的尺寸、颜色、透明度
/// 不负责：任何业务判断
/// </summary>
public class DotRenderer : MonoBehaviour, ICrosshairRenderer
{
    /// <summary>中文含义：点积。使用位置：DotRenderer.ApplySpread(float, float)、DotRenderer.Awake()。</summary>
    [Header("点引用")]
    [SerializeField]
    private RectTransform dot;
    /// <summary>中文含义：图像。使用位置：DotRenderer.ApplyAlpha(float)、DotRenderer.ApplyColor(Color, Color, float)、DotRenderer.Awake()。</summary>
    private Image _image;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Awake()
    {
        if (dot != null)
        {
            _image = dot.GetComponent<Image>();
        }
    }

    /// <summary>ApplySpread 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="normalizedSpread">中文含义：归一化散布。</param>
    /// <param name="uiSize">中文含义：界面尺寸。使用位置：DotRenderer.ApplySpread(float, float)。</param>
    public void ApplySpread(float normalizedSpread, float uiSize)
    {
        if (dot != null)
        {
            dot.sizeDelta = Vector2.one * uiSize;
        }
    }

    /// <summary>ApplyColor 的业务处理入口；本方法直接依赖 Color.Lerp(Color, Color, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Lerp(Color, Color, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseColor">中文含义：基础颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数。</param>
    /// <param name="hitColor">中文含义：命中颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数。</param>
    /// <param name="hitBlend">中文含义：命中混合。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public void ApplyColor(Color baseColor, Color hitColor, float hitBlend)
    {
        if (_image == null)
        {
            return;
        }

        // c（当前颜色）：保存 Color.Lerp(Color, Color, float) 的返回结果，供当前步骤使用。使用位置：DotRenderer.ApplyColor(Color, Color, float)。
Color c = Color.Lerp(baseColor, hitColor, Mathf.Clamp01(hitBlend));
        _image.color = c;
    }

    /// <summary>ApplyAlpha 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="alpha">中文含义：透明度。使用位置：DotRenderer.ApplyAlpha(float)。</param>
    public void ApplyAlpha(float alpha)
    {
        if (_image == null)
        {
            return;
        }

        // c（当前颜色）：使用位置：DotRenderer.ApplyAlpha(float)。
Color c = _image.color;
        c.a = alpha;
        _image.color = c;
    }

    /// <summary>OnActivate 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnActivate()
    {
        gameObject.SetActive(true);
    }

    /// <summary>OnDeactivate 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnDeactivate()
    {
        gameObject.SetActive(false);
    }
}
