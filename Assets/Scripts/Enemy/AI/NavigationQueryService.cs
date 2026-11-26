using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 NavigationQueryService；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "导航查询服务")]
// 每局由敌群管理器持有；节点中心的评分保持原有规则，不把楼上目标吸附到楼下。
internal sealed class NavigationQueryService : IDisposable
{
    /// <summary>单元格子级。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<Vector2Int, List<GraphNode>> cellChild = new(1024);
    /// <summary>候选。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<GraphNode> candidate = new(256);
    /// <summary>等待。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<PathAgent> wait = new(32);
    /// <summary>投影缓存。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly ProjectionEntry[] projectionCache = new ProjectionEntry[64];
    /// <summary>登记回调。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Action<GraphNode> registerCallback;
    /// <summary>图像。</summary>
    private AstarPath image;
    /// <summary>索引图集。</summary>
    private NavGraph[] indexAtlas;
    /// <summary>pendingRebuild 表示“是否存在待处理的重建”；hasClose 表示“是否具有关闭”。</summary>
    private bool pendingRebuild = true, hasClose;
    /// <summary>cacheCursor 表示“缓存光标”；trimFrame 表示“裁剪帧”；trimCount 表示“裁剪数量”。</summary>
    private int cacheCursor, trimFrame = -1, trimCount;
    /// <summary>裁剪等待。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<PathAgent> trimWait = new(32);
    /// <summary>下一次清理。</summary>
    private float nextCleanup;
    /// <summary>版本。</summary>
    internal int Version { get; private set; }
    /// <summary>是否可以查询。</summary>
    internal bool CanQuery => !hasClose && !pendingRebuild && image != null && image == AstarPath.active && ReferenceEquals(indexAtlas, image.graphs) && !image.isScanning;

    /// <summary>敌人行为、动作与通行的结构化数据 ProjectionEntry；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "导航查询服务+投影项")]
    private struct ProjectionEntry
    {
        /// <summary>hasSet 表示“是否具有集合”；success 表示“是否成功”。</summary>
        internal bool hasSet, success;
        /// <summary>区域。</summary>
        internal uint region;
        /// <summary>target 表示“目标”；result 表示“结果”。</summary>
        internal Vector3 target, result;
        /// <summary>radius 表示“半径”；heightDifference 表示“高度差值”；fallback 表示“后备”；maximum 表示“最大”；expired 表示“已过期”。</summary>
        internal float radius, heightDifference, fallback, maximum, expired;
    }

    /// <summary>构造并初始化 NavigationQueryService 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.BindingCurrentSession()；下游（含内部回调体） NavigationQueryService.Update()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal NavigationQueryService()
    {
        registerCallback = RegisterNode;
        AstarPath.OnGraphsUpdated += ImageHasChange;
        AstarPath.OnLatePostScan += ImageHasChange;
        Update();
    }

    /// <summary>ImageHasChange 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="currentImage">中文含义：当前图像。</param>
    private void ImageHasChange(AstarPath currentImage)
    {
        pendingRebuild = true;
        Version++;
    }

    /// <summary>Invalidate 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void Invalidate()
    {
        pendingRebuild = true;
        Version++;
    }

    /// <summary>Update 的业务处理入口；本方法直接依赖 Array.Clear(Array, int, int)、Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Clear()、NavGraph.GetNodes(Action&lt;GraphNode&gt;)、NavigationCollisionCache.InvalidateHierarchy(Transform)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.Update()、NavigationQueryService.NavigationQueryService()；下游（含内部回调体） Array.Clear(Array, int, int)、Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Clear()、NavGraph.GetNodes(Action&lt;GraphNode&gt;)、NavigationCollisionCache.InvalidateHierarchy(Transform)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void Update()
    {
        if (hasClose)
        {
            return;
        }

        if (image != AstarPath.active || (image != null && !ReferenceEquals(indexAtlas, image.graphs)))
        {
            pendingRebuild = true;
        }

        if (pendingRebuild && AstarPath.active != null && !AstarPath.active.isScanning)
        {
            image = AstarPath.active;
            indexAtlas = image.graphs;
            cellChild.Clear();
            Array.Clear(projectionCache, 0, projectionCache.Length);
            if (image.graphs != null)
            {
                // singleImage（单一图像）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：NavigationQueryService.Update()。
foreach (var singleImage in image.graphs)
                {
                    // 判空前缓存的单一图像。
global::Pathfinding.NavGraph receiverValue1 = singleImage;
                    if (!global::System.Object.ReferenceEquals(receiverValue1, null))
                    {
                        receiverValue1.GetNodes(registerCallback);
                    }
                }
            }

            pendingRebuild = false;
            Version++;
            CombatPerformanceCounters.indexRebuildCount++;
        }

        if (Time.unscaledTime >= nextCleanup)
        {
            nextCleanup = Time.unscaledTime + 1f;
            NavigationCollisionCache.InvalidateHierarchy(null);
        }
    }

    /// <summary>Key 的业务处理入口；本方法直接依赖 Mathf.FloorToInt(float)、Vector2Int.Vector2Int(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、NavigationQueryService.RegisterNode(GraphNode)；下游（含内部回调体） Mathf.FloorToInt(float)、Vector2Int.Vector2Int(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    private static Vector2Int Key(Vector3 position) => new(Mathf.FloorToInt(position.x / 2f), Mathf.FloorToInt(position.z / 2f));
    /// <summary>RegisterNode 的业务处理入口；本方法直接依赖 Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Add(Vector2Int, List&lt;GraphNode&gt;)、Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.TryGetValue(Vector2Int, out List&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.Add(GraphNode)、List&lt;GraphNode&gt;.List(int)、NavigationQueryService.Key(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Add(Vector2Int, List&lt;GraphNode&gt;)、Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.TryGetValue(Vector2Int, out List&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.Add(GraphNode)、List&lt;GraphNode&gt;.List(int)、NavigationQueryService.Key(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="node">节点。</param>
    private void RegisterNode(GraphNode node)
    {
        if (node == null || !node.Walkable)
        {
            return;
        }

        // 坐标。
var coordinate = Key((Vector3)node.position);
        if (!cellChild.TryGetValue(coordinate, out var /* 中文含义：列表。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ list))
        {
            cellChild.Add(coordinate, list = new List<GraphNode>(16));
        }

        list.Add(node);
    }

    /// <summary>RecoverCollection 的业务处理入口；本方法直接依赖 Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.TryGetValue(Vector2Int, out List&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.AddRange(IEnumerable&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.Clear()、NavigationQueryService.Key(Vector3)、Vector2Int.Vector2Int(int, int)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3)、PathAgent.ResolvePhysicallyReachablePathOrigin()、VillagerController.RecoverInvalidNavigationSpawn()；下游（含内部回调体） Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.TryGetValue(Vector2Int, out List&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.AddRange(IEnumerable&lt;GraphNode&gt;)、List&lt;GraphNode&gt;.Clear()、NavigationQueryService.Key(Vector3)、Vector2Int.Vector2Int(int, int)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="center">中心。</param>
    /// <param name="radius">半径。</param>
    /// <param name="output">输出。</param>
    internal bool RecoverCollection(Vector3 center, float radius, List<GraphNode> output)
    {
        output.Clear();
        if (!CanQuery)
        {
            return false;
        }

        // 最小。
Vector2Int minimum = Key(center - new Vector3(radius, 0, radius));
        // 最大。
Vector2Int maximum = Key(center + new Vector3(radius, 0, radius));
        // 水平。
for (int horizontal = minimum.x; horizontal <= maximum.x; horizontal++)
        {
            // 竖直。
for (int vertical = minimum.y; vertical <= maximum.y; vertical++)
            {
                if (cellChild.TryGetValue(new Vector2Int(horizontal, vertical), out var /* 中文含义：列表。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ list))
                {
                    output.AddRange(list);
                }
            }
        }

        CombatPerformanceCounters.candidateNodeCount += output.Count;
        return true;
    }

    /// <summary>Projection 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、NavigationQueryService.ProjectionEntry.ProjectionEntry()、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、ProfilerMarker.Auto()、Vector3.Equals(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)；下游（含内部回调体） Mathf.Abs(float)、NavigationQueryService.ProjectionEntry.ProjectionEntry()、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、ProfilerMarker.Auto()、Vector3.Equals(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标。</param>
    /// <param name="region">区域。</param>
    /// <param name="radius">半径。</param>
    /// <param name="heightDifference">高度差值。</param>
    /// <param name="fallback">后备。</param>
    /// <param name="maximum">最大。</param>
    /// <param name="result">结果。输出参数，由本入口写入并交给调用方。</param>
    internal bool Projection(Vector3 target, uint region, float radius, float heightDifference, float fallback, float maximum, out Vector3 result)
    {
        result = target;
        if (!CanQuery)
        {
            return false;
        }

        using (CombatPerformanceCounters.projectionMarker.Auto())
        {
            // 序号。
for (int sequence = 0; sequence < projectionCache.Length; sequence++)
            {
                // 当前投影入口。
ref var item = ref projectionCache[sequence];
                if (item.hasSet && item.expired > Time.time && item.region == region && item.target.Equals(target) && item.radius == radius && item.heightDifference == heightDifference && item.fallback == fallback && item.maximum == maximum)
                {
                    result = item.result;
                    return item.success;
                }
            }

            RecoverCollection(target, radius, candidate);

            // 中文含义：findTo 为“是否查找目标”。；hasFallback 为“是否具有后备”。
bool findTo = false, hasFallback = false;

            // 中文含义：best 为“最佳”。；fallbackBest 为“后备最佳”。
float best = float.PositiveInfinity, fallbackBest = float.PositiveInfinity;

            // 中文含义：bestPoint 为“最佳点”。；fallbackPoint 为“后备点”。
Vector3 bestPoint = target, fallbackPoint = target;
            // node（节点）：每轮从候选取得一个元素，仅代表当前这一项。使用位置：NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3)。
foreach (var node in candidate)
            {
                if (node == null || node.Destroyed || !node.Walkable || node.Area != region)
                {
                    continue;
                }

                // 点。
Vector3 point = (Vector3)node.position;

                // 中文含义：horizontalDifference 为“水平差值”。；verticalDifference 为“竖直差值”。；heightDifferenceValue 为“高度差值值”。
float horizontalDifference = point.x - target.x, verticalDifference = point.z - target.z, heightDifferenceValue = Mathf.Abs(point.y - target.y);
                // 平面距离平方。
float planarDistanceSquared = horizontalDifference * horizontalDifference + verticalDifference * verticalDifference;
                if (planarDistanceSquared > radius * radius)
                {
                    continue;
                }

                if (heightDifferenceValue <= heightDifference)
                {
                    // 部件数量。
float partCount = planarDistanceSquared + heightDifferenceValue * heightDifferenceValue * 4f;
                    if (partCount < best)
                    {
                        best = partCount;
                        bestPoint = point;
                        findTo = true;
                    }
                }
                else
                {
                    // 落地差值。
float landDifference = target.y - point.y;
                    if (landDifference <= heightDifference || landDifference > fallback || point.y > maximum)
                    {
                        continue;
                    }

                    // 部件数量。
float partCount = planarDistanceSquared + landDifference * landDifference * .2f;
                    if (partCount < fallbackBest)
                    {
                        fallbackBest = partCount;
                        fallbackPoint = point;
                        hasFallback = true;
                    }
                }
            }

            // 是否成功。
bool success = findTo || hasFallback;
            if (findTo)
            {
                result = bestPoint;
            }
            else
            {
                if (hasFallback)
                {
                    result = fallbackPoint;
                }
                else
                {
                    result = target;
                }
            }

            projectionCache[cacheCursor++ % projectionCache.Length] = new ProjectionEntry
            {
                hasSet = true,
                success = success,
                region = region,
                target = target,
                result = result,
                radius = radius,
                heightDifference = heightDifference,
                fallback = fallback,
                maximum = maximum,
                expired = Time.time + .2f
            };
            if (cacheCursor == int.MaxValue)
            {
                cacheCursor = 0;
            }

            return success;
        }
    }

    /// <summary>QueueUp 的业务处理入口；本方法直接依赖 List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.Contains(PathAgent)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.RequestPath()；下游（含内部回调体） List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.Contains(PathAgent)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="agent">寻路代理。</param>
    internal void QueueUp(PathAgent agent)
    {
        if (hasClose || wait.Contains(agent))
        {
            return;
        }

        agent.pathfindingEnqueueTime = Time.time;
        wait.Add(agent);
    }

    /// <summary>Revoke 的业务处理入口；本方法直接依赖 List&lt;PathAgent&gt;.Remove(PathAgent)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.CancelPath()、PathAgent.isStopped.set、PathAgent.RejectUnreachableDestination()；下游（含内部回调体） List&lt;PathAgent&gt;.Remove(PathAgent)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="agent">寻路代理。</param>
    internal void Revoke(PathAgent agent)
    {
        wait.Remove(agent);
        trimWait.Remove(agent);
    }

    // 普通请求在队尾轮转，超过等待阈值的请求优先于新的紧急请求。
    /// <summary>CommitRequest 的业务处理入口；本方法直接依赖 List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.RemoveAt(int)、Math.Max(double, double)、PathAgent.TryCommitPathfinding()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.LateUpdate()；下游（含内部回调体） List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.RemoveAt(int)、Math.Max(double, double)、PathAgent.TryCommitPathfinding()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CommitRequest()
    {
        if (!CanQuery)
        {
            return;
        }

        // 数量。
int count = 0;
        // 数量值。
for (int countValue = wait.Count; countValue > 0 && wait.Count > 0 && count < 4; countValue--)
        {
            // 选定。
int selected = 0;
            // 序号。
for (int sequence = 0; sequence < wait.Count; sequence++)
            {
                // 当前寻路代理。
var item = wait[sequence];
                if (item == null)
                {
                    selected = sequence;
                    break;
                }

                if (Time.time - item.pathfindingEnqueueTime >= .2f)
                {
                    selected = sequence;
                    break;
                }

                if (item.UrgentPathfinding)
                {
                    selected = sequence;
                }
            }

            // 寻路代理。
var agent = wait[selected];
            wait.RemoveAt(selected);
            if (agent == null || !agent.isActiveAndEnabled || !agent.HasDestination || agent.isStopped)
            {
                continue;
            }

            if (!agent.TryCommitPathfinding())
            {
                wait.Add(agent);
                continue;
            }

            CombatPerformanceCounters.maximumQueueUpSeconds = Math.Max(CombatPerformanceCounters.maximumQueueUpSeconds, Time.time - agent.pathfindingEnqueueTime);
            count++;
        }
    }

    /// <summary>RequestTrim 的业务处理入口；本方法直接依赖 List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.Contains(PathAgent)、List&lt;PathAgent&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceToVisibleWaypoint()；下游（含内部回调体） List&lt;PathAgent&gt;.Add(PathAgent)、List&lt;PathAgent&gt;.Contains(PathAgent)、List&lt;PathAgent&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="agent">寻路代理。</param>
    internal bool RequestTrim(PathAgent agent)
    {
        if (trimFrame != Time.frameCount)
        {
            trimFrame = Time.frameCount;
            trimCount = 0;
        }

        agent.latestTrimRequest = Time.time;
        if (!trimWait.Contains(agent))
        {
            trimWait.Add(agent);
        }

        while (trimWait.Count > 0 && (trimWait[0] == null || !trimWait[0].isActiveAndEnabled || trimWait[0].isStopped || Time.time - trimWait[0].latestTrimRequest > .2f))
        {
            trimWait.RemoveAt(0);
        }

        if (trimCount >= 2 || trimWait.Count == 0 || trimWait[0] != agent)
        {
            return false;
        }

        trimWait.RemoveAt(0);
        trimCount++;
        CombatPerformanceCounters.trimCount++;
        return true;
    }

    /// <summary>Dispose 的业务处理入口；本方法直接依赖 Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Clear()、List&lt;GraphNode&gt;.Clear()、List&lt;PathAgent&gt;.Clear()、NavigationCollisionCache.ClearValue()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.BindingCurrentSession()、EnemyAIManager.OnDestroy()；下游（含内部回调体） Dictionary&lt;Vector2Int, List&lt;GraphNode&gt; &gt;.Clear()、List&lt;GraphNode&gt;.Clear()、List&lt;PathAgent&gt;.Clear()、NavigationCollisionCache.ClearValue()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Dispose()
    {
        if (hasClose)
        {
            return;
        }

        hasClose = true;
        AstarPath.OnGraphsUpdated -= ImageHasChange;
        AstarPath.OnLatePostScan -= ImageHasChange;
        cellChild.Clear();
        wait.Clear();
        candidate.Clear();
        trimWait.Clear();
        NavigationCollisionCache.ClearValue();
    }
}
