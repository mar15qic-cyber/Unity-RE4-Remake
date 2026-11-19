using UnityEngine;
using UnityEngine.UI;

/// <summary>左右开口圆弧与固定中心点，直接生成网格以保持线宽。</summary>
[RequireComponent(typeof(CanvasRenderer))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "霰弹枪弧线准星")]
public sealed class ShotgunArcCrosshair : MaskableGraphic
{
    /// <summary>中文含义：半宽。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector2.Vector2(float, float) 的参数、ShotgunArcCrosshair.OnPopulateMesh(VertexHelper) 等共 4 处。</summary>
    [Header("准星形状")]
    [UnityEngine.Serialization.FormerlySerializedAs("半宽")]
    [UnityEngine.InspectorName("半宽")]
    public float halfWidth = 136f;
    /// <summary>中文含义：线宽。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数、ShotgunArcCrosshair.SetSize(float, float)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("线宽")]
    [UnityEngine.InspectorName("线宽")]
    public float lineWidth = 1.4f;
    /// <summary>中文含义：中心点半径。使用位置：作为 VertexHelper.AddVert(Vector3, Color32, Vector4) 的参数。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("中心点半径")]
    [UnityEngine.InspectorName("中心点半径")]
    public float centerPointRadius = 2f;
    /// <summary>SetSize 的业务处理入口；本方法直接依赖 Graphic.SetVerticesDirty()、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairUI.UpdateSpread()；下游（含内部回调体） Graphic.SetVerticesDirty()、Mathf.Abs(float)、Mathf.Approximately(float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="size">中文含义：尺寸。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="thickThin">中文含义：粗线细线。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public void SetSize(float size, float thickThin)
    {
        // newHalfWidth（新一半宽度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、ShotgunArcCrosshair.SetSize(float, float)。
float newHalfWidth = Mathf.Max(8f, size);
        // newLineWidth（新线宽度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、ShotgunArcCrosshair.SetSize(float, float)。
float newLineWidth = Mathf.Max(0.6f, thickThin);
        if (Mathf.Abs(halfWidth - newHalfWidth) < 0.05f && Mathf.Approximately(lineWidth, newLineWidth))
        {
            return;
        }

        halfWidth = newHalfWidth;
        lineWidth = newLineWidth;
        SetVerticesDirty();
    }

    /// <summary>实现基类的 OnPopulateMesh 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Cos(float)、Mathf.Lerp(float, float, float)、Mathf.Sin(float)、ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float)、VertexHelper.AddTriangle(int, int, int) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mesh">中文含义：网格。使用位置：作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数、ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)。</param>
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        // direction（方向）：使用位置：作为 Vector2.Vector2(float, float) 的参数、ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)。
for (int direction = -1; direction <= 1; direction += 2)
        {
            // radius（半径）：使用位置：作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数。
float radius = halfWidth * 0.52f;
            // circleCenter（圆形中心）：使用位置：作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数。
Vector2 circleCenter = new Vector2(direction * halfWidth * 0.48f, 0f);
            // segmented（分段的）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)。
const int segmented = 48;
            // segment（线段）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)。
for (int segment = 0; segment < segmented; segment++)
            {
                // startAngle（起始角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float startAngle = Mathf.Lerp(-55f, 55f, segment / (float)segmented) * Mathf.Deg2Rad;
                // endAngle（结束角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float endAngle = Mathf.Lerp(-55f, 55f, (segment + 1f) / segmented) * Mathf.Deg2Rad;
                // startDirection（起始方向）：使用位置：作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数。
Vector2 startDirection = new Vector2(direction * Mathf.Cos(startAngle), Mathf.Sin(startAngle));
                // endDirection（结束方向）：使用位置：作为 ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2) 的参数。
Vector2 endDirection = new Vector2(direction * Mathf.Cos(endAngle), Mathf.Sin(endAngle));
                AddFourEdgeShape(mesh, circleCenter + startDirection * (radius - lineWidth * 0.5f), circleCenter + startDirection * (radius + lineWidth * 0.5f), circleCenter + endDirection * (radius + lineWidth * 0.5f), circleCenter + endDirection * (radius - lineWidth * 0.5f));
            }
        }

        // segment（线段）：使用位置：ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)。
for (int segment = 0; segment < 24; segment++)
        {
            // startAngle（起始角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float startAngle = segment * Mathf.PI / 12f;
            // endAngle（结束角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float endAngle = (segment + 1) * Mathf.PI / 12f;
            // startPoint（起始点）：使用位置：作为 VertexHelper.AddTriangle(int, int, int) 的参数。
int startPoint = mesh.currentVertCount;
            mesh.AddVert(Vector3.zero, color, Vector2.zero);
            mesh.AddVert(new Vector3(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * centerPointRadius, color, Vector2.zero);
            mesh.AddVert(new Vector3(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * centerPointRadius, color, Vector2.zero);
            mesh.AddTriangle(startPoint, startPoint + 1, startPoint + 2);
        }
    }

    /// <summary>AddFourEdgeShape 的业务处理入口；本方法直接依赖 VertexHelper.AddTriangle(int, int, int)、VertexHelper.AddVert(Vector3, Color32, Vector4)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ShotgunArcCrosshair.OnPopulateMesh(VertexHelper)；下游（含内部回调体） VertexHelper.AddTriangle(int, int, int)、VertexHelper.AddVert(Vector3, Color32, Vector4)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mesh">中文含义：网格。使用位置：ShotgunArcCrosshair.AddFourEdgeShape(VertexHelper, Vector2, Vector2, Vector2, Vector2)。</param>
    /// <param name="first">中文含义：首个。使用位置：作为 VertexHelper.AddVert(Vector3, Color32, Vector4) 的参数。</param>
    /// <param name="second">中文含义：第二。使用位置：作为 VertexHelper.AddVert(Vector3, Color32, Vector4) 的参数。</param>
    /// <param name="third">中文含义：第三。使用位置：作为 VertexHelper.AddVert(Vector3, Color32, Vector4) 的参数。</param>
    /// <param name="fourth">中文含义：第四。使用位置：作为 VertexHelper.AddVert(Vector3, Color32, Vector4) 的参数。</param>
    private void AddFourEdgeShape(VertexHelper mesh, Vector2 first, Vector2 second, Vector2 third, Vector2 fourth)
    {
        // startPoint（起始点）：使用位置：作为 VertexHelper.AddTriangle(int, int, int) 的参数。
int startPoint = mesh.currentVertCount;
        mesh.AddVert(first, color, Vector2.zero);
        mesh.AddVert(second, color, Vector2.zero);
        mesh.AddVert(third, color, Vector2.zero);
        mesh.AddVert(fourth, color, Vector2.zero);
        mesh.AddTriangle(startPoint, startPoint + 1, startPoint + 2);
        mesh.AddTriangle(startPoint, startPoint + 2, startPoint + 3);
    }
}
