using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 不依赖额外贴图的环形进度图形，填充值沿圆弧线性变化。
/// </summary>
public class HealthRingGraphic : MaskableGraphic
{
    /// <summary>中文含义：厚度。使用位置：作为 Mathf.Max(float, float) 的参数、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。</summary>
    [Header("圆环形状")]
    [Range(1f, 40f)]
    public float thickness = 10f;
    /// <summary>中文含义：起始角度。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、HealthRingGraphic.OnPopulateMesh(VertexHelper)。</summary>
    [Range(-360f, 360f)]
    public float startAngle = -130f;
    /// <summary>中文含义：扫描角度。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、HealthRingGraphic.OnPopulateMesh(VertexHelper)。</summary>
    [Range(1f, 360f)]
    public float sweepAngle = 260f;
    /// <summary>中文含义：线段数量。使用位置：作为 Mathf.CeilToInt(float) 的参数、CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)。</summary>
    [Range(12, 180)]
    public int segmentCount = 96;
    /// <summary>中文含义：是否顺时针。使用位置：CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、HealthRingGraphic.OnPopulateMesh(VertexHelper)。</summary>
    public bool clockwise = true;
    /// <summary>中文含义：填充大小。使用位置：作为 Mathf.Approximately(float, float) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)、HealthRingGraphic.SetFillAmount(float)。</summary>
    [Header("填充")]
    [Range(0f, 1f)]
    public float fillAmount = 1f;
    /// <summary>SetFillAmount 的业务处理入口；本方法直接依赖 Graphic.SetVerticesDirty()、Mathf.Approximately(float, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.ConfigureRing(HealthRingGraphic, Color, float)、CombatHUD.UpdateHealthFill(float, float, bool)；下游（含内部回调体） Graphic.SetVerticesDirty()、Mathf.Approximately(float, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public void SetFillAmount(float value)
    {
        // clamped（限制后的）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、HealthRingGraphic.SetFillAmount(float)。
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
    /// <param name="vertexHelper">中文含义：顶点辅助器。使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。</param>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (fillAmount <= 0f)
        {
            return;
        }

        // rect（矩形）：保存 Graphic.GetPixelAdjustedRect() 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
Rect rect = GetPixelAdjustedRect();
        // center（中心）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
Vector2 center = rect.center;
        // outerRadius（外侧半径）：使用位置：作为 Mathf.Max(float, float) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
float outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        // innerRadius（内部半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
float innerRadius = Mathf.Max(0f, outerRadius - thickness);
        // direction（方向）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
float direction;
        if (clockwise)
        {
            direction = -1f;
        }
        else
        {
            direction = 1f;
        }

        // visibleSweep（可见扫描）：使用位置：作为 Mathf.CeilToInt(float) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
float visibleSweep = sweepAngle * fillAmount;
        // usedSegments（已使用的线段集合）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
int usedSegments = Mathf.Max(1, Mathf.CeilToInt(segmentCount * visibleSweep / 360f));
        // vertexColor（顶点颜色）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
Color32 vertexColor = color;
        // i（当前遍历索引）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
for (int i = 0; i <= usedSegments; i++)
        {
            // ratio（比例）：使用位置：作为 Vector2.Vector2(float, float) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
float ratio = i / (float)usedSegments;
            // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = (startAngle + direction * visibleSweep * ratio) * Mathf.Deg2Rad;
            // normal（表面法线）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // outer（外侧）：使用位置：作为 VertexHelper.AddVert(UIVertex) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
UIVertex outer = UIVertex.simpleVert;
            outer.color = vertexColor;
            outer.position = center + normal * outerRadius;
            outer.uv0 = new Vector2(ratio, 1f);
            vertexHelper.AddVert(outer);
            // inner（内部）：使用位置：作为 VertexHelper.AddVert(UIVertex) 的参数、HealthRingGraphic.OnPopulateMesh(VertexHelper)。
UIVertex inner = UIVertex.simpleVert;
            inner.color = vertexColor;
            inner.position = center + normal * innerRadius;
            inner.uv0 = new Vector2(ratio, 0f);
            vertexHelper.AddVert(inner);
        }

        // i（当前遍历索引）：使用位置：HealthRingGraphic.OnPopulateMesh(VertexHelper)。
for (int i = 0; i < usedSegments; i++)
        {
            // index（当前元素索引）：使用位置：作为 VertexHelper.AddTriangle(int, int, int) 的参数。
int index = i * 2;
            vertexHelper.AddTriangle(index, index + 2, index + 1);
            vertexHelper.AddTriangle(index + 2, index + 3, index + 1);
        }
    }
}
