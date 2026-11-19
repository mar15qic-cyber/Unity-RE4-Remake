using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 严格敌人寻路烘焙中的局部高层合法区域。
/// 边界模式只为已有的真实碰撞表面提供白名单，不生成任何承载几何。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class EnemyNavigationElevatedWalkableVolume : MonoBehaviour
{
    /// <summary>中文含义：生效体积区域集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyNavigationElevatedWalkableVolume.ActiveVolumes.get、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryNavigationPoint(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3) 等共 8 处。</summary>
    private static readonly List<EnemyNavigationElevatedWalkableVolume> activeVolumes = new List<EnemyNavigationElevatedWalkableVolume>();
    /// <summary>中文含义：区域编号。使用位置：作为 IEnumerable&lt;EnemyWindowTraversalRoute&gt;.Any&lt;EnemyWindowTraversalRoute&gt;(Func&lt;EnemyWindowTraversalRoute, bool&gt;) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsRegionHorizontalPoint(string, Vector3) 等共 6 处。</summary>
    [Header("区域标识")]
    public string regionId = "二楼外平台";
    /// <summary>中文含义：中心偏移量。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。</summary>
    [Header("合法寻路范围")]
    public Vector3 centerOffset;
    /// <summary>中文含义：尺寸。使用位置：作为 Mathf.Abs(float) 的参数。</summary>
    public Vector3 size = new Vector3(3f, 0.5f, 3f);
    /// <summary>中文含义：水平留白。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)。</summary>
    [Min(0f)]
    public float horizontalPadding = 0.08f;
    /// <summary>中文含义：是否使用边界多边形。使用位置：EnemyNavigationElevatedWalkableVolume.ContainsBoundaryNavigationPoint(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3) 等共 8 处。</summary>
    [Header("真实表面边界")]
    public bool useBoundaryPolygon;
    /// <summary>中文含义：边界点集合。使用位置：EnemyNavigationElevatedWalkableVolume.OnDrawGizmosSelected()、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float) 等共 4 处。</summary>
    public Transform[] boundaryPoints;
    /// <summary>中文含义：边界留白。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)。</summary>
    [Min(0f)]
    public float boundaryPadding = 0.12f;
    /// <summary>中文含义：边界竖直容差。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)。</summary>
    [Min(0.05f)]
    public float boundaryVerticalTolerance = 0.7f;
    /// <summary>中文含义：是否烘焙木板细缝。使用位置：StrictAstarGraphBaker.CreatePlatformTreadScanSupplementalSurface(HouseThreeRegionNavigation)、StrictAstarGraphBaker.ValidatePlatformSeamWalk(HouseThreeRegionNavigation, AstarPath)。</summary>
    [Header("木板细缝的导航采样（只生成烘焙输入，不生成物理地面）")]
    [UnityEngine.Serialization.FormerlySerializedAs("烘焙木板细缝")]
    [UnityEngine.InspectorName("烘焙木板细缝")]
    public bool bakeWoodPlankFineSeams;
    /// <summary>中文含义：生效体积区域集合。使用位置：StrictAstarGraphBaker.CollectProtectedNavigationAreas()。</summary>
    public static IReadOnlyList<EnemyNavigationElevatedWalkableVolume> ActiveVolumes => activeVolumes;

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyNavigationElevatedWalkableVolume&gt;.Add(EnemyNavigationElevatedWalkableVolume)、List&lt;EnemyNavigationElevatedWalkableVolume&gt;.Contains(EnemyNavigationElevatedWalkableVolume)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (!activeVolumes.Contains(this))
        {
            activeVolumes.Add(this);
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyNavigationElevatedWalkableVolume&gt;.Remove(EnemyNavigationElevatedWalkableVolume)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        activeVolumes.Remove(this);
    }

    /// <summary>GetWorldBounds 的业务处理入口；本方法直接依赖 Bounds.Bounds(Vector3, Vector3)、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)、EnemyNavigationElevatedWalkableVolume.OnDrawGizmosSelected()、StrictAstarGraphBaker.CollectProtectedNavigationAreas()、StrictAstarGraphBaker.CreatePlatformTreadScanSupplementalSurface(HouseThreeRegionNavigation) 等共 6 处；下游（含内部回调体） Bounds.Bounds(Vector3, Vector3)、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="additionalHorizontalPadding">中文含义：额外水平留白。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public Bounds GetWorldBounds(float additionalHorizontalPadding = 0f)
    {
        // padding（留白）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
float padding = Mathf.Max(0f, horizontalPadding + additionalHorizontalPadding);
        if (useBoundaryPolygon && TryGetBoundaryBounds(padding, out Bounds /* 中文含义：边界包围盒。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ boundaryBounds))
        {
            return boundaryBounds;
        }

        // worldCenter（世界空间中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Bounds.Bounds(Vector3, Vector3) 的参数。
Vector3 worldCenter = transform.TransformPoint(centerOffset);
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = transform.lossyScale;
        // worldSize（世界空间尺寸）：使用位置：作为 Bounds.Bounds(Vector3, Vector3) 的参数。
Vector3 worldSize = new Vector3(Mathf.Abs(size.x * scale.x) + padding * 2f, Mathf.Abs(size.y * scale.y), Mathf.Abs(size.z * scale.z) + padding * 2f);
        return new Bounds(worldCenter, worldSize);
    }

    /// <summary>ContainsNavigationPoint 的业务处理入口；本方法直接依赖 Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryNavigationPoint(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsNavigationPointInAny(Vector3, float)、EnemyNavigationElevatedWalkableVolume.ContainsRegionPoint(string, Vector3)、Read(bool, out Vector3) 等共 6 处；下游（含内部回调体） Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Bounds.Contains(Vector3) 的参数、作为 EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float) 的参数、作为 Mathf.Abs(float) 的参数。</param>
    /// <param name="additionalHorizontalPadding">中文含义：额外水平留白。使用位置：作为 EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float) 的参数、作为 Mathf.Max(float, float) 的参数。</param>
    public bool ContainsNavigationPoint(Vector3 point, float additionalHorizontalPadding = 0f)
    {
        if (!useBoundaryPolygon)
        {
            return GetWorldBounds(additionalHorizontalPadding).Contains(point);
        }

        if (!TryGetBoundarySample(point, out float /* 中文含义：水平距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ horizontalDistance, out float /* 中文含义：采样高度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sampleHeight))
        {
            return false;
        }

        // allowedPadding（允许的留白）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)。
float allowedPadding = Mathf.Max(0f, boundaryPadding + horizontalPadding + additionalHorizontalPadding);
        return horizontalDistance <= allowedPadding && Mathf.Abs(point.y - sampleHeight) <= boundaryVerticalTolerance;
    }

    /// <summary>ContainsHorizontalPoint 的业务处理入口；本方法直接依赖 Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.ContainsRegionHorizontalPoint(string, Vector3)、StrictAstarGraphBaker.CreatePlatformTreadScanSupplementalSurface(HouseThreeRegionNavigation)；下游（含内部回调体） Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Bounds.Contains(Vector3) 的参数、作为 EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)。</param>
    public bool ContainsHorizontalPoint(Vector3 point)
    {
        if (!useBoundaryPolygon)
        {
            // bounds（包围盒）：保存 EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float) 的返回结果，供当前步骤使用。使用位置：EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)。
Bounds bounds = GetWorldBounds();
            point.y = bounds.center.y;
            return bounds.Contains(point);
        }

        if (!TryGetBoundarySample(point, out float /* 中文含义：水平距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ horizontalDistance, out _))
        {
            return false;
        }

        return horizontalDistance <= boundaryPadding + horizontalPadding;
    }

    /// <summary>TryGetBoundarySample 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)、float.IsPositiveInfinity(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Sqrt(float)、Vector2.Dot(Vector2, Vector2)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)、float.IsPositiveInfinity(float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Sqrt(float)、Vector2.Dot(Vector2, Vector2)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="horizontalDistance">中文含义：水平距离。输出参数，由本入口写入并交给调用方。使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。</param>
    /// <param name="sampleHeight">中文含义：采样高度。输出参数，由本入口写入并交给调用方。使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。</param>
    private bool TryGetBoundarySample(Vector3 point, out float horizontalDistance, out float sampleHeight)
    {
        horizontalDistance = float.PositiveInfinity;
        sampleHeight = 0f;
        if (boundaryPoints == null || boundaryPoints.Length < 3)
        {
            return false;
        }

        // first（第一个变换节点）：使用位置：作为 EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Transform first = boundaryPoints[0];
        if (first == null)
        {
            return false;
        }

        // horizontalPoint（水平点）：使用位置：作为 EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Vector2 horizontalPoint = new Vector2(point.x, point.z);
        // i（当前遍历索引）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
for (int i = 1; i < boundaryPoints.Length - 1; i++)
        {
            // second（第二个变换节点）：使用位置：作为 EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Transform second = boundaryPoints[i];
            // third（第三）：使用位置：作为 EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Transform third = boundaryPoints[i + 1];
            if (second == null || third == null)
            {
                continue;
            }

            if (!TryGetTriangleCoordinates(horizontalPoint, first.position, second.position, third.position, out Vector3 /* 中文含义：坐标集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ coordinates))
            {
                continue;
            }

            horizontalDistance = 0f;
            sampleHeight = first.position.y * coordinates.x + second.position.y * coordinates.y + third.position.y * coordinates.z;
            return true;
        }

        // nearestSqrDistance（最近距离平方）：使用位置：作为 float.IsPositiveInfinity(float) 的参数、作为 Mathf.Sqrt(float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
float nearestSqrDistance = float.PositiveInfinity;
        // i（当前遍历索引）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
for (int i = 0; i < boundaryPoints.Length; i++)
        {
            // start（起始）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Vector2(float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Transform start = boundaryPoints[i];
            // end（结束）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Vector2(float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Transform end = boundaryPoints[(i + 1) % boundaryPoints.Length];
            if (start == null || end == null)
            {
                continue;
            }

            // startHorizontal（起始水平）：使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Vector2 startHorizontal = new Vector2(start.position.x, start.position.z);
            // endHorizontal（结束水平）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Vector2 endHorizontal = new Vector2(end.position.x, end.position.z);
            // segment（线段）：使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Vector2 segment = endHorizontal - startHorizontal;
            // segmentSqrLength（线段平方长度）：使用位置：作为 Mathf.Clamp01(float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
float segmentSqrLength = segment.sqrMagnitude;
            // progress（进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
float progress;
            if (segmentSqrLength > 0.0001f)
            {
                progress = Mathf.Clamp01(Vector2.Dot(horizontalPoint - startHorizontal, segment) / segmentSqrLength);
            }
            else
            {
                progress = 0f;
            }

            // closest（最近）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
Vector2 closest = startHorizontal + segment * progress;
            // sqrDistance（距离平方）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)。
float sqrDistance = (horizontalPoint - closest).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            sampleHeight = Mathf.Lerp(start.position.y, end.position.y, progress);
        }

        if (float.IsPositiveInfinity(nearestSqrDistance))
        {
            return false;
        }

        horizontalDistance = Mathf.Sqrt(nearestSqrDistance);
        return true;
    }

    /// <summary>TryGetTriangleCoordinates 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.TryGetBoundarySample(Vector3, out float, out float)；下游（含内部回调体） Mathf.Abs(float)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。</param>
    /// <param name="first">中文含义：首个。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="second">中文含义：第二。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="third">中文含义：第三。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="coordinates">中文含义：坐标集合。输出参数，由本入口写入并交给调用方。使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。</param>
    private static bool TryGetTriangleCoordinates(Vector2 point, Vector3 first, Vector3 second, Vector3 third, out Vector3 coordinates)
    {
        // a（起点（二维向量））：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
Vector2 a = new Vector2(first.x, first.z);
        // b（终点（二维向量））：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
Vector2 b = new Vector2(second.x, second.z);
        // c（候选值（二维向量））：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
Vector2 c = new Vector2(third.x, third.z);
        // denominator（分母）：使用位置：作为 Mathf.Abs(float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        if (Mathf.Abs(denominator) <= 0.0001f)
        {
            coordinates = default;
            return false;
        }

        // firstWeight（首个权重）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
float firstWeight = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
        // secondWeight（第二权重）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
float secondWeight = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
        // thirdWeight（第三权重）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
float thirdWeight = 1f - firstWeight - secondWeight;
        // tolerance（容差）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetTriangleCoordinates(Vector2, Vector3, Vector3, Vector3, out Vector3)。
const float tolerance = 0.001f;
        if (firstWeight < -tolerance || secondWeight < -tolerance || thirdWeight < -tolerance)
        {
            coordinates = default;
            return false;
        }

        coordinates = new Vector3(firstWeight, secondWeight, thirdWeight);
        return true;
    }

    /// <summary>TryGetBoundaryBounds 的业务处理入口；本方法直接依赖 Bounds.Bounds(Vector3, Vector3)、Mathf.Max(float, float)、Vector3.Max(Vector3, Vector3)、Vector3.Min(Vector3, Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)；下游（含内部回调体） Bounds.Bounds(Vector3, Vector3)、Mathf.Max(float, float)、Vector3.Max(Vector3, Vector3)、Vector3.Min(Vector3, Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="padding">中文含义：留白。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="bounds">中文含义：包围盒。输出参数，由本入口写入并交给调用方。使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。</param>
    private bool TryGetBoundaryBounds(float padding, out Bounds bounds)
    {
        bounds = default;
        if (boundaryPoints == null)
        {
            return false;
        }

        // found（是否已找到）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。
bool found = false;
        // minimum（最小）：使用位置：作为 Bounds.Bounds(Vector3, Vector3) 的参数、作为 Vector3.Min(Vector3, Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。
Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        // maximum（最大）：使用位置：作为 Bounds.Bounds(Vector3, Vector3) 的参数、作为 Vector3.Max(Vector3, Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。
Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        // i（当前遍历索引）：使用位置：EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。
for (int i = 0; i < boundaryPoints.Length; i++)
        {
            // boundaryPoint（边界点）：使用位置：作为 Vector3.Max(Vector3, Vector3) 的参数、作为 Vector3.Min(Vector3, Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.TryGetBoundaryBounds(float, out Bounds)。
Transform boundaryPoint = boundaryPoints[i];
            if (boundaryPoint == null)
            {
                continue;
            }

            minimum = Vector3.Min(minimum, boundaryPoint.position);
            maximum = Vector3.Max(maximum, boundaryPoint.position);
            found = true;
        }

        if (!found)
        {
            return false;
        }

        // horizontalExpansion（水平扩展）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float horizontalExpansion = Mathf.Max(0f, boundaryPadding + padding);
        minimum -= new Vector3(horizontalExpansion, boundaryVerticalTolerance, horizontalExpansion);
        maximum += new Vector3(horizontalExpansion, boundaryVerticalTolerance, horizontalExpansion);
        bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        return true;
    }

    /// <summary>ContainsBoundaryNavigationPoint 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StrictAstarGraphBaker.ApplyObstacleTopPruning(RecastGraph)、StrictAstarGraphBaker.ApplyPhysicalObstaclePruning(RecastGraph)、StrictAstarGraphBaker.ApplyTorsoClearancePruning(RecastGraph)；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float) 的参数。</param>
    public static bool ContainsBoundaryNavigationPoint(Vector3 point)
    {
        // i（当前遍历索引）：使用位置：作为 List<EnemyNavigationElevatedWalkableVolume>.this[int] 的参数、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryNavigationPoint(Vector3)。
for (int i = 0; i < activeVolumes.Count; i++)
        {
            // volume（音量）：使用位置：EnemyNavigationElevatedWalkableVolume.ContainsBoundaryNavigationPoint(Vector3)。
EnemyNavigationElevatedWalkableVolume volume = activeVolumes[i];
            if (volume != null && volume.isActiveAndEnabled && volume.useBoundaryPolygon && volume.ContainsNavigationPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ContainsNavigationPointInAny 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StrictAstarGraphBaker.ApplyHouseHeightPruning(RecastGraph)、StrictAstarGraphBaker.CollectProtectedNavigationAreas()、StrictAstarGraphBaker.ValidateWalkableHeights(RecastGraph)；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float) 的参数。</param>
    /// <param name="additionalHorizontalPadding">中文含义：额外水平留白。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float) 的参数。</param>
    public static bool ContainsNavigationPointInAny(Vector3 point, float additionalHorizontalPadding = 0f)
    {
        // i（当前遍历索引）：使用位置：作为 List<EnemyNavigationElevatedWalkableVolume>.this[int] 的参数、EnemyNavigationElevatedWalkableVolume.ContainsNavigationPointInAny(Vector3, float)。
for (int i = 0; i < activeVolumes.Count; i++)
        {
            // volume（音量）：使用位置：EnemyNavigationElevatedWalkableVolume.ContainsNavigationPointInAny(Vector3, float)。
EnemyNavigationElevatedWalkableVolume volume = activeVolumes[i];
            if (volume != null && volume.isActiveAndEnabled && volume.ContainsNavigationPoint(point, additionalHorizontalPadding))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ContainsRegionHorizontalPoint 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRegionId">中文含义：目标区域编号。使用位置：作为 string.IsNullOrWhiteSpace(string) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsRegionHorizontalPoint(string, Vector3)。</param>
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsHorizontalPoint(Vector3) 的参数。</param>
    public static bool ContainsRegionHorizontalPoint(string targetRegionId, Vector3 point)
    {
        if (string.IsNullOrWhiteSpace(targetRegionId))
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：作为 List<EnemyNavigationElevatedWalkableVolume>.this[int] 的参数、EnemyNavigationElevatedWalkableVolume.ContainsRegionHorizontalPoint(string, Vector3)。
for (int i = 0; i < activeVolumes.Count; i++)
        {
            // volume（音量）：使用位置：EnemyNavigationElevatedWalkableVolume.ContainsRegionHorizontalPoint(string, Vector3)。
EnemyNavigationElevatedWalkableVolume volume = activeVolumes[i];
            if (volume == null || !volume.isActiveAndEnabled || volume.regionId != targetRegionId)
            {
                continue;
            }

            if (volume.ContainsHorizontalPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ContainsRegionPoint 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRegionId">中文含义：目标区域编号。使用位置：作为 string.IsNullOrWhiteSpace(string) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsRegionPoint(string, Vector3)。</param>
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float) 的参数。</param>
    public static bool ContainsRegionPoint(string targetRegionId, Vector3 point)
    {
        if (string.IsNullOrWhiteSpace(targetRegionId))
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：作为 List<EnemyNavigationElevatedWalkableVolume>.this[int] 的参数、EnemyNavigationElevatedWalkableVolume.ContainsRegionPoint(string, Vector3)。
for (int i = 0; i < activeVolumes.Count; i++)
        {
            // volume（音量）：使用位置：EnemyNavigationElevatedWalkableVolume.ContainsRegionPoint(string, Vector3)。
EnemyNavigationElevatedWalkableVolume volume = activeVolumes[i];
            if (volume == null || !volume.isActiveAndEnabled || volume.regionId != targetRegionId)
            {
                continue;
            }

            if (volume.ContainsNavigationPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ContainsBoundaryRegionPoint 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、EnemyWindowTraversal.TryBeginDropAfterMovement(Vector3)、EnemyWindowTraversal.TryBeginDropFromHit(Vector3)、EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRegionId">中文含义：目标区域编号。使用位置：作为 string.IsNullOrWhiteSpace(string) 的参数、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)。</param>
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsNavigationPoint(Vector3, float) 的参数。</param>
    public static bool ContainsBoundaryRegionPoint(string targetRegionId, Vector3 point)
    {
        if (string.IsNullOrWhiteSpace(targetRegionId))
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：作为 List<EnemyNavigationElevatedWalkableVolume>.this[int] 的参数、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)。
for (int i = 0; i < activeVolumes.Count; i++)
        {
            // volume（音量）：使用位置：EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)。
EnemyNavigationElevatedWalkableVolume volume = activeVolumes[i];
            if (volume == null || !volume.isActiveAndEnabled || !volume.useBoundaryPolygon || volume.regionId != targetRegionId)
            {
                continue;
            }

            if (volume.ContainsNavigationPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float)、Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireCube(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 0.95f, 0.75f);
        if (useBoundaryPolygon && boundaryPoints != null && boundaryPoints.Length >= 3)
        {
            // i（当前遍历索引）：使用位置：EnemyNavigationElevatedWalkableVolume.OnDrawGizmosSelected()。
for (int i = 0; i < boundaryPoints.Length; i++)
            {
                // start（起始）：使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.OnDrawGizmosSelected()。
Transform start = boundaryPoints[i];
                // end（结束）：使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、EnemyNavigationElevatedWalkableVolume.OnDrawGizmosSelected()。
Transform end = boundaryPoints[(i + 1) % boundaryPoints.Length];
                if (start != null && end != null)
                {
                    Gizmos.DrawLine(start.position, end.position);
                }
            }

            return;
        }

        // bounds（包围盒）：保存 EnemyNavigationElevatedWalkableVolume.GetWorldBounds(float) 的返回结果，供当前步骤使用。使用位置：作为 Gizmos.DrawWireCube(Vector3, Vector3) 的参数。
Bounds bounds = GetWorldBounds();
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
