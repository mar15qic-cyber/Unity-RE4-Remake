using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 不依赖额外贴图的环形进度图形，填充值沿圆弧线性变化。
/// </summary>
public class HealthRingGraphic : MaskableGraphic
{
    /// <summary>厚度。</summary>
    [Header("圆环形状")]
    [Range(1f, 40f)]
    public float thickness = 10f;
    /// <summary>起始角度。</summary>
    [Range(-360f, 360f)]
    public float startAngle = -130f;
    /// <summary>扫描角度。</summary>
    [Range(1f, 360f)]
    public float sweepAngle = 260f;
    /// <summary>线段数量。</summary>
    [Range(12, 180)]
    public int segmentCount = 96;
    /// <summary>是否顺时针。</summary>
    public bool clockwise = true;
    /// <summary>填充大小。</summary>
    [Header("填充")]
    [Range(0f, 1f)]
    public float fillAmount = 1f;
    /// <summary>SetFillAmount 的业务处理入口；本方法直接依赖 Graphic.SetVerticesDirty()、Mathf.Approximately(float, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、CombatHUD.UpdateHealthFill(float, float, bool)；下游（含内部回调体） Graphic.SetVerticesDirty()、Mathf.Approximately(float, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    public void SetFillAmount(float value)
    {
        // 限制后的。
float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(fillAmount, clamped))
        {
            return;
        }

        fillAmount = clamped;
        SetVerticesDirty();
    }

    /// <summary>实现基类的 OnPopulateMesh 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Graphic.GetPixelAdjustedRect()、Mathf.CeilToInt(float)、Mathf.Cos(float)、Mathf.Max(float, float)、Mathf.Max(int, int)、Mathf.Min(float, float)、Mathf.Sin(float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="vertexHelper">顶点辅助器。</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (fillAmount <= 0f)
        {
            return;
        }

        // 矩形。
Rect rect = GetPixelAdjustedRect();
        // 中心。
Vector2 center = rect.center;
        // 外侧半径。
float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        // 内部半径。
float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        // 方向。
float direction;
        if (clockwise)
        {
            direction = -1f;
        }
        else
        {
            direction = 1f;
        }

        // 可见扫描。
float visibleSweep = sweepAngle * fillAmount;
        // 已使用的线段集合。
int usedSegments = Mathf.Max(1, Mathf.CeilToInt(segmentCount * visibleSweep / 360f));
        // 顶点颜色。
Color32 vertexColor = color;
        // 当前遍历索引。
for (int i = 0; i <= usedSegments; i++)
        {
            // 比例。
float ratio = i / (float)usedSegments;
            // 角度。
float angle = (startAngle + direction * visibleSweep * ratio) * Mathf.Deg2Rad;
            // 表面法线。
Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // 外侧。
UIVertex outer = UIVertex.simpleVert;
            outer.color = vertexColor;
            outer.position = center + normal * outerRadius;
            outer.uv0 = new Vector2(ratio, 1f);
            vertexHelper.AddVert(outer);
            // 内部。
UIVertex inner = UIVertex.simpleVert;
            inner.color = vertexColor;
            inner.position = center + normal * innerRadius;
            inner.uv0 = new Vector2(ratio, 0f);
            vertexHelper.AddVert(inner);
        }

        // 当前遍历索引。
for (int i = 0; i < usedSegments; i++)
        {
            // 当前元素索引。
int index = i * 2;
            vertexHelper.AddTriangle(index, index + 2, index + 1);
            vertexHelper.AddTriangle(index + 2, index + 3, index + 1);
        }
    }
}
