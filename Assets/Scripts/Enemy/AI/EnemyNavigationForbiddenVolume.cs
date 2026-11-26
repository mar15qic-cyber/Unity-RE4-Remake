using UnityEngine;

/// <summary>
/// 敌人导航专用禁行体积。
/// 它不参与普通物理碰撞，仅在严格烘焙时从寻路图中移除被覆盖节点。
/// </summary>
[ExecuteAlways]
public class EnemyNavigationForbiddenVolume : MonoBehaviour
{
    /// <summary>相对于对象原点的中心偏移。</summary>
    [Header("禁行范围")]
    [Tooltip("相对于对象原点的中心偏移。")]
    public Vector3 centerOffset;
    /// <summary>未扩张前的本地尺寸。</summary>
    [Tooltip("未扩张前的本地尺寸。")]
    public Vector3 size = new Vector3(3.6f, 3f, 3.6f);
    /// <summary>在敌人半径之外追加的水平安全边距。</summary>
    [Tooltip("在敌人半径之外追加的水平安全边距。")]
    [Min(0f)]
    public float horizontalPadding = 0.1f;
    /// <summary>是否启用后仅剔除高于指定高度的节点，保留障碍物四周的地面环带。启用后仅剔除高于指定高度的节点，保留障碍物四周的地面环带。</summary>
    [Header("高度筛选")]
    [Tooltip("启用后仅剔除高于指定高度的节点，保留障碍物四周的地面环带。")]
    public bool pruneOnlyAboveMinimumHeight = true;
    /// <summary>剔除谷堆顶部节点的世界高度下限。</summary>
    [Tooltip("剔除谷堆顶部节点的世界高度下限。")]
    public float minimumNavigationHeight = 0.65f;
    /// <summary>是否保留可行走区域。启用后仅作可视化，不会剔除节点。用于楼梯桥接和合法高层区域。</summary>
    [Header("楼梯保护")]
    [Tooltip("启用后仅作可视化，不会剔除节点。用于楼梯桥接和合法高层区域。")]
    public bool preserveWalkableArea;
    /// <summary>GetWorldBounds 的业务处理入口；本方法直接依赖 Bounds.Bounds(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyNavigationForbiddenVolume.ContainsNavigationPoint(Vector3, float)、EnemyNavigationForbiddenVolume.OnDrawGizmos()；下游（含内部回调体） Bounds.Bounds(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="agentRadius">寻路代理半径。</param>
    public Bounds GetWorldBounds(float agentRadius = 0f)
    {
        // 缩放。
Vector3 scale = transform.lossyScale;
        // 世界空间尺寸。
Vector3 worldSize = new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), Mathf.Abs(size.z * scale.z));
        // 留白。
float padding = Mathf.Max(0f, horizontalPadding + agentRadius);
        worldSize.x += padding * 2f;
        worldSize.z += padding * 2f;
        return new Bounds(transform.TransformPoint(centerOffset), worldSize);
    }

    /// <summary>ContainsNavigationPoint 的业务处理入口；本方法直接依赖 Bounds.Contains(Vector3)、EnemyNavigationForbiddenVolume.GetWorldBounds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StrictAstarGraphBaker.ApplyNavigationForbiddenVolumePruning(RecastGraph)；下游（含内部回调体） Bounds.Contains(Vector3)、EnemyNavigationForbiddenVolume.GetWorldBounds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="agentRadius">寻路代理半径。</param>
    public bool ContainsNavigationPoint(Vector3 point, float agentRadius)
    {
        if (preserveWalkableArea || !GetWorldBounds(agentRadius).Contains(point))
        {
            return false;
        }

        return !pruneOnlyAboveMinimumHeight || point.y >= minimumNavigationHeight;
    }

    /// <summary>Unity 场景辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、EnemyNavigationForbiddenVolume.GetWorldBounds(float)、Gizmos.DrawCube(Vector3, Vector3)、Gizmos.DrawWireCube(Vector3, Vector3)、Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmos()
    {
        // 包围盒。
Bounds bounds = GetWorldBounds();
        if (pruneOnlyAboveMinimumHeight)
        {
            // 上部最小。
float upperMin = Mathf.Max(bounds.min.y, minimumNavigationHeight);
            bounds.min = new Vector3(bounds.min.x, upperMin, bounds.min.z);
        }

        if (preserveWalkableArea)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.2f);
        }
        else
        {
            Gizmos.color = new Color(0.95f, 0.25f, 0.1f, 0.24f);
        }

        Gizmos.DrawCube(bounds.center, bounds.size);
        if (preserveWalkableArea)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.9f);
        }
        else
        {
            Gizmos.color = new Color(0.95f, 0.25f, 0.1f, 0.9f);
        }

        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
