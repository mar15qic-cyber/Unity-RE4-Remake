using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 RegionWindowRouting；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "分区窗口选路")]
// 分别查询实际步行段，比较直达、一次翻窗和借室内中转的成本。
internal sealed class RegionWindowRouting
{
    /// <summary>中文含义：距离表。只读字段，初始化后不由普通写入口替换。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan) 等共 4 处。</summary>
    private readonly Dictionary<(Vector3, Vector3), float> distanceTable = new Dictionary<(Vector3, Vector3), float>();
    /// <summary>中文含义：lastTarget 表示“上一次目标”；lastPosition 表示“上一次位置”。使用位置：作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。；使用位置：作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private Vector3 lastTarget, lastPosition;
    /// <summary>中文含义：lastOutside 表示“是否上一次外部”；lastTargetOutside 表示“是否上一次目标外部”。使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。；使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private bool lastOutside, lastTargetOutside;
    /// <summary>中文含义：版本。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan) 等共 4 处。</summary>
    private int version;
    /// <summary>中文含义：是否具有计算结束。使用位置：RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    /// <summary>中文含义：目标已连接区域。使用位置：RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private uint? targetConnectedRegion;
    /// <summary>中文含义：路径平滑。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private FunnelModifier pathSmoothing;
    /// <summary>中文含义：下一次刷新。使用位置：RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private float nextRefresh;
    private int pendingPathCount;
    /// <summary>中文含义：最佳。使用位置：Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.ClearValue()、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private EnemyWindowTraversalPlan best;
    /// <summary>中文含义：角色。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    private VillagerController character;
    private readonly List<EnemyWindowTraversalPlan> generalPlans = new();
    /// <summary>ClearValue 的业务处理入口；本方法直接依赖 Dictionary&lt;(Vector3, Vector3), float&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ClearRegionWindowRoute()；下游（含内部回调体） Dictionary&lt;(Vector3, Vector3), float&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearValue()
    {
        version++;
        distanceTable.Clear();
        generalPlans.Clear();
        pendingPathCount = 0;
        best = default;
        targetConnectedRegion = null;
        nextRefresh = float.NegativeInfinity;
    }

    /// <summary>Select 的业务处理入口；本方法直接依赖 Compare(float, EnemyWindowTraversalPlan)、Component.GetComponent&lt;FunnelModifier&gt;()、Dictionary&lt;(Vector3, Vector3), float&gt;.Clear()、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)、float.IsNaN(float)、float.IsPositiveInfinity(float)、Mathf.Max(float, float) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)；下游（含内部回调体） Compare(float, EnemyWindowTraversalPlan)、Component.GetComponent&lt;FunnelModifier&gt;()、Dictionary&lt;(Vector3, Vector3), float&gt;.Clear()、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)、float.IsNaN(float)、float.IsPositiveInfinity(float)、Mathf.Max(float, float) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool) 的参数、作为 Mathf.Max(float, float) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="target">中文含义：目标。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="outside">中文含义：是否外部。使用位置：作为 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool) 的参数、作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="targetOutside">中文含义：是否目标外部。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数、作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="result">中文含义：当前敌人翻窗通行方案。输出参数，由本入口写入并交给调用方。使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</param>
    public bool Select(VillagerController enemy, Vector3 target, bool outside, bool targetOutside,
        IReadOnlyDictionary<EnemyWindowTraversalRoute, float> blockedRoutes, out EnemyWindowTraversalPlan result)
    {
        character = enemy;
        bool refreshDistances = Time.time >= nextRefresh && pendingPathCount == 0;
        bool staleRoute = best.route && (!best.route.isActiveAndEnabled || best.route.windowPoint == null ||
            best.route.windowPoint.IsBarricaded);
        if (version == 0 || outside != lastOutside || targetOutside != lastTargetOutside || refreshDistances || (lastTarget - target).sqrMagnitude > 1f || (lastPosition - enemy.transform.position).sqrMagnitude > 4f || staleRoute)
        {
            // switchSide（是否切换侧边）：使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
bool switchSide = outside != lastOutside || targetOutside != lastTargetOutside;
            // receiverValue1（判空前缓存的导航图节点）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
global::Pathfinding.GraphNode receiverValue1 = Projection(target, targetOutside).node;
            // targetRegion（目标区域）：使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
uint? targetRegion;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                targetRegion = receiverValue1.Area;
            }
            else
            {
                targetRegion = default;
            }

            if (switchSide || targetRegion != targetConnectedRegion || best.route && !best.route.isActiveAndEnabled)
            {
                best = default;
            }

            targetConnectedRegion = targetRegion;
            if (!pathSmoothing)
            {
                pathSmoothing = enemy.GetComponent<FunnelModifier>();
            }

            lastOutside = outside;
            lastTargetOutside = targetOutside;
            lastTarget = target;
            lastPosition = enemy.transform.position;
            nextRefresh = Time.time + 1f;
            version++;
            distanceTable.Clear();
            pendingPathCount = 0;
        }

        // 路径长度可以缓存，队列成本和禁用状态必须每次重新比较。
        if (best.route != null && IsTemporarilyBlocked(best.route))
        {
            best = default;
        }

        // shortest（最短）：使用位置：作为 float.IsNaN(float) 的参数、Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
float shortest;
        if (outside == targetOutside)
        {
            shortest = WalkLength(lastPosition, lastTarget, outside, targetOutside);
        }
        else
        {
            shortest = float.PositiveInfinity;
        }

        // selected（选定）：使用位置：Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
EnemyWindowTraversalPlan selected = default;
        // originalEntryPointAvailable（是否原始入口点可用）：使用位置：Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
        float previousRouteCost = float.PositiveInfinity;
        // pendingReturn（是否存在待处理的返回）：保存 float.IsNaN(float) 的返回结果，供当前步骤使用。使用位置：Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
bool pendingReturn = float.IsNaN(shortest);
        // window（窗口）：每轮从敌人翻窗通行路线的生效路线集合取得一个元素，仅代表当前这一项。使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
foreach (var window in EnemyWindowTraversalRoute.ActiveRoutes)
        {
            if (IsTemporarilyBlocked(window) || window.platformRegionId != HouseThreeRegionNavigation.Current.outerPlatformIdentifier
                || !window.TryBuildPlanFromSide(enemy, outside, out var /* 中文含义：进入。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enter, true))
            {
                continue;
            }

            // frontSegment（前方线段）：保存 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的返回结果，供当前步骤使用。使用位置：作为 float.IsPositiveInfinity(float) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
float frontSegment = WalkLength(lastPosition, enter.sourceGround, outside, outside);
            if (float.IsPositiveInfinity(frontSegment))
            {
                continue;
            }

            // frontSegmentCost（前方线段代价）：使用位置：作为 Compare(float, EnemyWindowTraversalPlan) 的参数。
            float walkSpeed = Mathf.Max(.1f, enemy.Agent.speed);
            float frontSegmentCost = frontSegment + (window.traversalDuration + window.EstimateWindowQueueDelay(enemy)) * walkSpeed;
            if (outside != targetOutside)
            {
                Compare(frontSegmentCost + WalkLength(enter.destinationGround, lastTarget, !outside, targetOutside), enter);
            }
            else
            {
                // exitPoint（退出点）：每轮从敌人翻窗通行路线的生效路线集合取得一个元素，仅代表当前这一项。使用位置：作为 Compare(float, EnemyWindowTraversalPlan) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
foreach (var exitPoint in EnemyWindowTraversalRoute.ActiveRoutes)
                {
                    if (exitPoint == window || IsTemporarilyBlocked(exitPoint) || exitPoint.platformRegionId != window.platformRegionId || !exitPoint.TryBuildPlanFromSide(enemy, !outside, out var /* 中文含义：远离打开。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ awayOpen, true))
                    {
                        continue;
                    }

                    Compare(frontSegmentCost + WalkLength(enter.destinationGround, awayOpen.sourceGround, !outside, !outside) + (exitPoint.traversalDuration + exitPoint.EstimateWindowQueueDelay(enemy)) * walkSpeed + WalkLength(awayOpen.destinationGround, lastTarget, outside, targetOutside), enter);
                }
            }
        }

        /// <summary>Compare 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="cost">中文含义：代价。使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数、Compare(float, EnemyWindowTraversalPlan)。</param>
        /// <param name="entryPoint">中文含义：入口点。使用位置：Compare(float, EnemyWindowTraversalPlan)。</param>
        bool IsTemporarilyBlocked(EnemyWindowTraversalRoute route)
        {
            return blockedRoutes.TryGetValue(route, out float until) && Time.time < until;
        }

        void Compare(float cost, EnemyWindowTraversalPlan entryPoint)
        {
            if (float.IsNaN(cost))
            {
                pendingReturn = true;
                return;
            }

            if (best.route == entryPoint.route && !float.IsInfinity(cost))
            {
                previousRouteCost = Mathf.Min(previousRouteCost, cost);
            }

            if (cost < shortest)
            {
                shortest = cost;
                selected = entryPoint;
            }
        }

        if (!pendingReturn || selected.route != null)
        {
            if (previousRouteCost > shortest + Mathf.Max(.5f, enemy.Agent.speed * .35f) || float.IsInfinity(previousRouteCost))
            {
                best = selected;
            }

        }

        result = best;
        return result.route && result.route.isActiveAndEnabled;
    }

    /// <summary>Projection 的业务处理入口；本方法直接依赖 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)、RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool)；下游（含内部回调体） HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    /// <param name="outside">中文含义：是否外部。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    internal bool SelectGeneral(VillagerController enemy, Vector3 target,
        IReadOnlyDictionary<EnemyWindowTraversalRoute, float> blockedRoutes, out EnemyWindowTraversalPlan result)
    {
        result = default;
        if (AstarPath.active == null || AstarPath.active.isScanning)
        {
            return false;
        }
        if (version == 0 || Time.time >= nextRefresh && pendingPathCount == 0
            || (target - lastTarget).sqrMagnitude > 1f || (enemy.transform.position - lastPosition).sqrMagnitude > 4f)
        {
            var previous = best;
            ClearValue();
            best = previous;
            lastPosition = enemy.transform.position;
            lastTarget = target;
            pathSmoothing = enemy.GetComponent<FunnelModifier>();
            nextRefresh = Time.time + .65f;
            foreach (var route in EnemyWindowTraversalRoute.ActiveRoutes)
            {
                if (route == null || !route.isActiveAndEnabled || !string.IsNullOrEmpty(route.platformRegionId)
                    || (route.transform.position - lastPosition).sqrMagnitude > 14f * 14f
                    || (route.transform.position - target).sqrMagnitude > 16f * 16f)
                {
                    continue;
                }
                if (route.TryBuildPlanFromSide(enemy, false, out var entry, true))
                {
                    generalPlans.Add(entry);
                }
                if (route.TryBuildPlanFromSide(enemy, true, out var reverse, true))
                {
                    generalPlans.Add(reverse);
                }
            }
        }

        var origin = GeneralProjection(lastPosition);
        var destination = GeneralProjection(lastTarget);
        float direct = WalkLength(origin, destination);
        HouseThreeTraversalGeometry geometry = enemy.door03TraversalRoute;
        if (geometry != null && geometry.entranceDoor != null &&
            geometry.entranceDoor.IsSiegeDoorBetween(enemy.transform.position, target))
        {
            direct = float.PositiveInfinity;
        }
        float bestCost = direct - .75f;
        foreach (var plan in generalPlans)
        {
            if (plan.route == null || !plan.route.isActiveAndEnabled || plan.route.windowPoint == null ||
                plan.route.windowPoint.IsBarricaded
                || blockedRoutes.TryGetValue(plan.route, out float until) && Time.time < until)
            {
                continue;
            }
            float approach = WalkLength(origin, GeneralProjection(plan.sourceGround));
            float exit = WalkLength(GeneralProjection(plan.destinationGround), destination);
            float cost = approach + exit + Mathf.Max(.5f, enemy.Agent.speed)
                * (plan.route.traversalDuration + plan.route.EstimateWindowQueueDelay(enemy));
            if (!float.IsNaN(cost) && cost < bestCost)
            {
                result = plan;
                bestCost = cost;
            }
        }
        if (float.IsNaN(direct) && best.route != null && best.route.isActiveAndEnabled &&
            best.route.windowPoint != null && !best.route.windowPoint.IsBarricaded
            && (!blockedRoutes.TryGetValue(best.route, out float expiry) || Time.time >= expiry))
        {
            result = best;
        }
        else
        {
            best = result;
        }
        return result.route != null;
    }

    private static NNInfo GeneralProjection(Vector3 position)
    {
        var nearest = AstarPath.active.GetNearest(position, NearestNodeConstraint.Walkable);
        if (Mathf.Abs(nearest.position.y - position.y) > .65f
            || (nearest.position - position).sqrMagnitude > 1.25f * 1.25f)
        {
            return default;
        }
        return nearest;
    }

    private NNInfo Projection(Vector3 position, bool outside) => HouseThreeRegionNavigation.Current.ReadSameSideNavigationPoint(position, outside, character.Agent.destinationSnapDistance, character.Agent.graphSnapHeight);
    /// <summary>WalkLength 的业务处理入口；本方法直接依赖 ABPath.Construct(Vector3, Vector3, OnPathDelegate)、AstarPath.StartPath(Path, bool, bool)、Dictionary&lt;(Vector3, Vector3), float&gt;.TryGetValue((Vector3, Vector3), out float)、FunnelModifier.Apply(Path)、Path.GetTotalLength()、RegionWindowRouting.Projection(Vector3, bool)、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)；下游（含内部回调体） ABPath.Construct(Vector3, Vector3, OnPathDelegate)、AstarPath.StartPath(Path, bool, bool)、Dictionary&lt;(Vector3, Vector3), float&gt;.TryGetValue((Vector3, Vector3), out float)、FunnelModifier.Apply(Path)、Path.GetTotalLength()、RegionWindowRouting.Projection(Vector3, bool)、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="startPoint">中文含义：起始点。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数。</param>
    /// <param name="endPoint">中文含义：结束点。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数。</param>
    /// <param name="startPointOutside">中文含义：是否起始点外部。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数。</param>
    /// <param name="endPointOutside">中文含义：是否结束点外部。使用位置：作为 RegionWindowRouting.Projection(Vector3, bool) 的参数。</param>
    private float WalkLength(Vector3 startPoint, Vector3 endPoint, bool startPointOutside, bool endPointOutside)
    {
        return WalkLength(Projection(startPoint, startPointOutside), Projection(endPoint, endPointOutside));
    }

    private float WalkLength(NNInfo start, NNInfo end)
    {
        if (start.node == null || end.node == null || start.node.Area != end.node.Area)
        {
            return float.PositiveInfinity;
        }

        if (start.node == end.node)
        {
            return Vector3.Distance(start.position, end.position);
        }

        // key（键）：使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、作为 Dictionary<(Vector3, Vector3), float>.this[(Vector3, Vector3)] 的参数、作为 Dictionary<(Vector3, Vector3), float>.TryGetValue((Vector3, Vector3), out float) 的参数。
var key = (start.position, end.position);
        if (distanceTable.TryGetValue(key, out float /* 中文含义：长度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ length))
        {
            return length;
        }

        // requestVersion（请求版本）：使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数。
int requestVersion = version;
        distanceTable[key] = float.NaN;
        pendingPathCount++;
        AstarPath.StartPath(ABPath.Construct(start.position, end.position, /* 中文含义：路径。 */ path =>
        {
            if (requestVersion != version)
            {
                return;
            }

            pendingPathCount = Mathf.Max(0, pendingPathCount - 1);

            if (!path.error && pathSmoothing && pathSmoothing.enabled)
            {
                pathSmoothing.Apply(path);
            }

            // assignmentReceiver4（赋值前保存的目标距离表）：从距离表读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数。
global::System.Collections.Generic.Dictionary<(global::UnityEngine.Vector3, global::UnityEngine.Vector3), float> assignmentReceiver4 = distanceTable;
            // assignmentIndex5（赋值前保存的索引键）：从键读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<(Vector3, Vector3), float>.this[(Vector3, Vector3)] 的参数。
(global::UnityEngine.Vector3, global::UnityEngine.Vector3) assignmentIndex5 = key;
            if (path.error)
            {
                assignmentReceiver4[assignmentIndex5] = float.PositiveInfinity;
            }
            else
            {
                assignmentReceiver4[assignmentIndex5] = path.GetTotalLength();
            }
        }));
        return float.NaN;
    }
}
