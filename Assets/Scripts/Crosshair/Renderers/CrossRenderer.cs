using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 十字准心渲染器 — 四线段策略
/// 负责：4 条 RectTransform 的位置、颜色、透明度
/// 不负责：任何业务判断、Spread 计算、状态管理
/// </summary>
public class CrossRenderer : MonoBehaviour, ICrosshairRenderer
{
    /// <summary>中文含义：线顶部。使用位置：CrossRenderer.ApplySizes()、CrossRenderer.ApplySpread(float, float)、CrossRenderer.CollectImages()。</summary>
    [Header("线段引用")]
    [SerializeField]
    private RectTransform lineTop;
    /// <summary>中文含义：线底部。使用位置：CrossRenderer.ApplySizes()、CrossRenderer.ApplySpread(float, float)、CrossRenderer.CollectImages()。</summary>
    [SerializeField]
    private RectTransform lineBottom;
    /// <summary>中文含义：线左侧。使用位置：CrossRenderer.ApplySizes()、CrossRenderer.ApplySpread(float, float)、CrossRenderer.CollectImages()。</summary>
    [SerializeField]
    private RectTransform lineLeft;
    /// <summary>中文含义：线右侧。使用位置：CrossRenderer.ApplySizes()、CrossRenderer.ApplySpread(float, float)、CrossRenderer.CollectImages()。</summary>
    [SerializeField]
    private RectTransform lineRight;
    /// <summary>中文含义：图像集合。使用位置：CrossRenderer.ApplyAlpha(float)、CrossRenderer.ApplyColor(Color, Color, float)、CrossRenderer.CollectImages()。</summary>
    private Image[] _images;
    /// <summary>中文含义：厚度。当前配置（由 View 在 Activate 时注入）</summary>
    private float _thickness;
    /// <summary>中文含义：线长度。使用位置：作为 Vector2.Vector2(float, float) 的参数、CrossRenderer.ApplySpread(float, float)、CrossRenderer.Configure(float, float)。</summary>
    private float _lineLength;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrossRenderer.CollectImages()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Awake()
    {
        CollectImages();
    }

    /// <summary>CollectImages 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrossRenderer.Awake()；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CollectImages()
    {
        // count（数量）：使用位置：CrossRenderer.CollectImages()。
int count = 0;
        if (lineTop)
        {
            count++;
        }

        if (lineBottom)
        {
            count++;
        }

        if (lineLeft)
        {
            count++;
        }

        if (lineRight)
        {
            count++;
        }

        _images = new Image[count];
        // i（当前遍历索引）：使用位置：CrossRenderer.CollectImages()。
int i = 0;
        if (lineTop)
        {
            _images[i++] = lineTop.GetComponent<Image>();
        }

        if (lineBottom)
        {
            _images[i++] = lineBottom.GetComponent<Image>();
        }

        if (lineLeft)
        {
            _images[i++] = lineLeft.GetComponent<Image>();
        }

        if (lineRight)
        {
            _images[i++] = lineRight.GetComponent<Image>();
        }
    }

    /// <summary>由 CrosshairView 调用，注入配置参数</summary>
    /// <remarks>静态调用链：上游 CrosshairView.SwitchRenderer(CrosshairConfig)；下游（含内部回调体） CrossRenderer.ApplySizes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="thickness">中文含义：厚度。使用位置：CrossRenderer.Configure(float, float)。</param>
    /// <param name="lineLength">中文含义：线长度。使用位置：CrossRenderer.Configure(float, float)。</param>
    public void Configure(float thickness, float lineLength)
    {
        _thickness = thickness;
        _lineLength = lineLength;
        ApplySizes();
    }

    /// <summary>ApplySizes 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrossRenderer.Configure(float, float)、CrossRenderer.OnActivate()；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplySizes()
    {
        if (lineTop)
        {
            lineTop.sizeDelta = new Vector2(_thickness, _lineLength);
        }

        if (lineBottom)
        {
            lineBottom.sizeDelta = new Vector2(_thickness, _lineLength);
        }

        if (lineLeft)
        {
            lineLeft.sizeDelta = new Vector2(_lineLength, _thickness);
        }

        if (lineRight)
        {
            lineRight.sizeDelta = new Vector2(_lineLength, _thickness);
        }
    }

    /// <summary>ApplySpread 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="normalizedSpread">中文含义：归一化散布。</param>
    /// <param name="uiSize">中文含义：界面尺寸。使用位置：CrossRenderer.ApplySpread(float, float)。</param>
    public void ApplySpread(float normalizedSpread, float uiSize)
    {
        // gap（间隙）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float gap = uiSize + _lineLength * 0.5f;
        if (lineTop)
        {
            lineTop.anchoredPosition = new Vector2(0f, gap);
        }

        if (lineBottom)
        {
            lineBottom.anchoredPosition = new Vector2(0f, -gap);
        }

        if (lineLeft)
        {
            lineLeft.anchoredPosition = new Vector2(-gap, 0f);
        }

        if (lineRight)
        {
            lineRight.anchoredPosition = new Vector2(gap, 0f);
        }
    }

    /// <summary>ApplyColor 的业务处理入口；本方法直接依赖 Color.Lerp(Color, Color, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Lerp(Color, Color, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseColor">中文含义：基础颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、CrossRenderer.ApplyColor(Color, Color, float)。</param>
    /// <param name="hitColor">中文含义：命中颜色。使用位置：作为 Color.Lerp(Color, Color, float) 的参数。</param>
    /// <param name="hitBlend">中文含义：命中混合。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public void ApplyColor(Color baseColor, Color hitColor, float hitBlend)
    {
        // c（当前颜色）：保存 Color.Lerp(Color, Color, float) 的返回结果，供当前步骤使用。使用位置：CrossRenderer.ApplyColor(Color, Color, float)。
Color c = Color.Lerp(baseColor, hitColor, Mathf.Clamp01(hitBlend));
        if (_images == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：CrossRenderer.ApplyColor(Color, Color, float)。
for (int i = 0; i < _images.Length; i++)
        {
            if (_images[i] != null)
            {
                // imgColor（图像颜色）：使用位置：CrossRenderer.ApplyColor(Color, Color, float)。
Color imgColor = c;
                imgColor.a *= baseColor.a; // 尊重 base alpha 作为整体透明度基准
                _images[i].color = imgColor;
            }
        }
    }

    /// <summary>ApplyAlpha 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="alpha">中文含义：透明度。使用位置：CrossRenderer.ApplyAlpha(float)。</param>
    public void ApplyAlpha(float alpha)
    {
        if (_images == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：CrossRenderer.ApplyAlpha(float)。
for (int i = 0; i < _images.Length; i++)
        {
            if (_images[i] != null)
            {
                // c（当前颜色）：使用位置：CrossRenderer.ApplyAlpha(float)。
Color c = _images[i].color;
                c.a = alpha;
                _images[i].color = c;
            }
        }
    }

    /// <summary>OnActivate 的业务处理入口；本方法直接依赖 CrossRenderer.ApplySizes()、GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrossRenderer.ApplySizes()、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnActivate()
    {
        gameObject.SetActive(true);
        ApplySizes();
    }

    /// <summary>OnDeactivate 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnDeactivate()
    {
        gameObject.SetActive(false);
    }
}
