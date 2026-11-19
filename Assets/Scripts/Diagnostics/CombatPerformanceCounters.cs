using Unity.Profiling;
using UnityEngine;

/// <summary>运行诊断模块 CombatPerformanceCounters；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "战斗性能计数")]
internal static class CombatPerformanceCounters
{
    /// <summary>中文含义：snapshotCount 表示“快照数量”；categoryHit 表示“类别命中”；categoryMissed 表示“类别未命中的”；candidateNodeCount 表示“候选节点数量”；indexRebuildCount 表示“索引重建数量”。使用位置：CombatPerformanceCounters.Reset()、EnemyCombatTimeline.LateUpdate()、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、NavigationCollisionCache.Read(Collider)、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、NavigationCollisionCache.Read(Collider)、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、NavigationQueryService.Update()、PerformanceSamplingEntry.Tick()。</summary>
    internal static long snapshotCount, categoryHit, categoryMissed, candidateNodeCount, indexRebuildCount;
    /// <summary>中文含义：pathfindingCommit 表示“寻路提交”；pathfindingCancel 表示“寻路取消”；pathExpandCapacity 表示“路径展开容量”；physicsQueryCount 表示“物理查询数量”；trimCount 表示“裁剪数量”；traversalCacheHit 表示“通行缓存命中”。使用位置：CombatPerformanceCounters.Reset()、PathAgent.TryCommitPathfinding()、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、PathAgent.CancelCalculate()。；使用位置：CombatPerformanceCounters.Reset()、PathAgent.CachePathProperty()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 等共 5 处。；使用位置：CombatPerformanceCounters.Reset()、PerformanceSamplingEntry.Tick()、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 等共 21 处。；使用位置：CombatPerformanceCounters.Reset()、NavigationQueryService.RequestTrim(PathAgent)、PerformanceSamplingEntry.Tick()。；使用位置：CombatPerformanceCounters.Reset()、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
    internal static long pathfindingCommit, pathfindingCancel, pathExpandCapacity, physicsQueryCount, trimCount, traversalCacheHit;
    /// <summary>中文含义：最大队列向上秒。使用位置：作为 Math.Max(double, double) 的参数、CombatPerformanceCounters.Reset()、NavigationQueryService.CommitRequest() 等共 4 处。</summary>
    internal static double maximumQueueUpSeconds;
    /// <summary>中文含义：快照标记。只读字段，初始化后不由普通写入口替换。使用位置：EnemyCombatTimeline.LateUpdate()。</summary>
    internal static readonly ProfilerMarker snapshotMarker = new("战斗诊断.采集快照");
    /// <summary>中文含义：障碍标记。只读字段，初始化后不由普通写入口替换。使用位置：NavigationCollisionCache.IsObstacle(Collider)。</summary>
    internal static readonly ProfilerMarker obstacleMarker = new("敌人导航.障碍分类");
    /// <summary>中文含义：投影标记。只读字段，初始化后不由普通写入口替换。使用位置：NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3)。</summary>
    internal static readonly ProfilerMarker projectionMarker = new("敌人导航.局部投影");
    /// <summary>中文含义：路径标记。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。</summary>
    internal static readonly ProfilerMarker pathMarker = new("敌人导航.接收路径");
    /// <summary>中文含义：裁剪标记。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.AdvanceToVisibleWaypoint()。</summary>
    internal static readonly ProfilerMarker trimMarker = new("敌人导航.裁剪检测");
    /// <summary>Reset 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
        snapshotCount = categoryHit = categoryMissed = candidateNodeCount = indexRebuildCount = 0;
        pathfindingCommit = pathfindingCancel = pathExpandCapacity = physicsQueryCount = trimCount = traversalCacheHit = 0;
        maximumQueueUpSeconds = 0;
    }
}
