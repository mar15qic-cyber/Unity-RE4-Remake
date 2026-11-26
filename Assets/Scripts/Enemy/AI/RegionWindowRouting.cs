using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 RegionWindowRouting；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "分区窗口选路")]
// 分别查询实际步行段，比较直达、一次翻窗和借室内中转的成本。
internal sealed class RegionWindowRouting
{
    /// <summary>距离表。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<(Vector3, Vector3), float> distanceTable = new Dictionary<(Vector3, Vector3), float>();
    /// <summary>lastTarget 表示“上一次目标”；lastPosition 表示“上一次位置”。</summary>
    private Vector3 lastTarget, lastPosition;
    /// <summary>lastOutside 表示“是否上一次外部”；lastTargetOutside 表示“是否上一次目标外部”。</summary>
    private bool lastOutside, lastTargetOutside;
    /// <summary>版本。</summary>
    private int version;
    /// <summary>是否具有计算结束。</summary>
    /// <summary>目标已连接区域。</summary>
    private uint? targetConnectedRegion;
    /// <summary>路径平滑。</summary>
    private FunnelModifier pathSmoothing;
    /// <summary>下一次刷新。</summary>
    private float nextRefresh;
    private int pendingPathCount;
    /// <summary>最佳。</summary>
    private EnemyWindowTraversalPlan best;
    /// <summary>角色。</summary>
    private VillagerController character;
    private readonly List<EnemyWindowTraversalPlan> generalPlans = new();
    private readonly HashSet<VillagerController> countedDirectCompetitors = new HashSet<VillagerController>();
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
    /// <param name="enemy">敌人。</param>
    /// <param name="target">目标。</param>
    /// <param name="outside">是否外部。</param>
    /// <param name="targetOutside">是否目标外部。</param>
    /// <param name="result">当前敌人翻窗通行方案。输出参数，由本入口写入并交给调用方。</param>
    public bool Select(VillagerController enemy, Vector3 target, bool outside, bool targetOutside,
        IReadOnlyDictionary<EnemyWindowTraversalRoute, float> blockedRoutes, out EnemyWindowTraversalPlan result)
    {
        character = enemy;
        bool refreshDistances = Time.time >= nextRefresh && pendingPathCount == 0;
        bool staleRoute = best.route && (!best.route.isActiveAndEnabled || best.route.windowPoint == null ||
            best.route.windowPoint.IsBarricaded);
        if (version == 0 || outside != lastOutside || targetOutside != lastTargetOutside || refreshDistances || (lastTarget - target).sqrMagnitude > 1f || (lastPosition - enemy.transform.position).sqrMagnitude > 4f || staleRoute)
        {
            // 是否切换侧边。
bool switchSide = outside != lastOutside || targetOutside != lastTargetOutside;
            // 判空前缓存的导航图节点。
global::Pathfinding.GraphNode receiverValue1 = Projection(target, targetOutside).node;
            // 目标区域。
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
            nextRefresh = Time.time + 0.35f;
            version++;
            distanceTable.Clear();
            pendingPathCount = 0;
        }

        // 路径长度可以缓存，队列成本和禁用状态必须每次重新比较。
        if (best.route != null && IsTemporarilyBlocked(best.route))
        {
            best = default;
        }

        // 最短。
float shortest;
        if (outside == targetOutside)
        {
            shortest = WalkLength(lastPosition, lastTarget, outside, targetOutside);
        }
        else
        {
            shortest = float.PositiveInfinity;
        }

        // 选定。
EnemyWindowTraversalPlan selected = default;
        // originalEntryPointAvailable（是否原始入口点可用）：使用位置：Compare(float, EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
        float previousRouteCost = float.PositiveInfinity;
        // 是否存在待处理的返回。
bool pendingReturn = float.IsNaN(shortest);
        EnemyWindowTraversal actorTraversal = null;
        if (enemy != null)
        {
            actorTraversal = enemy.ArchitectureReadSpecialTraversal;
        }

        // window（窗口）：每轮从敌人翻窗通行路线的生效路线集合取得一个元素，仅代表当前这一项。使用位置：RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。
foreach (var window in EnemyWindowTraversalRoute.ActiveRoutes)
        {
            if (IsTemporarilyBlocked(window) || window.platformRegionId != HouseThreeRegionNavigation.Current.outerPlatformIdentifier
                || !window.TryBuildPlanFromSide(enemy, outside, out var /* 中文含义：进入。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enter, true))
            {
                continue;
            }

            // 前方线段。
float frontSegment = WalkLength(lastPosition, enter.sourceGround, outside, outside);
            if (float.IsPositiveInfinity(frontSegment))
            {
                continue;
            }

            // frontSegmentCost（前方线段代价）：使用位置：作为 Compare(float, EnemyWindowTraversalPlan) 的参数。
            float walkSpeed = Mathf.Max(.1f, enemy.Agent.speed);
            float baseWindowDuration = Mathf.Max(0.1f, window.traversalDuration);
            // 原版翻窗轨迹与落地收尾真实耗时约 3.6 秒，按真实占用时长折算排队与远端竞争代价，促使同层多敌分流到空闲窗口。
            float effectiveWindowDuration = Mathf.Max(baseWindowDuration, 3.6f);
            float queueDelay = window.EstimateWindowQueueDelay(enemy);
            float occupiedDiversionPenalty = 0f;
            if (queueDelay > 0f)
            {
                queueDelay = queueDelay * (effectiveWindowDuration / baseWindowDuration);
                occupiedDiversionPenalty = 2.5f * walkSpeed;
            }
            int remoteApproachCount = CountRemoteWindowCompetitors(enemy, window);
            if (remoteApproachCount > 0)
            {
                queueDelay += remoteApproachCount * effectiveWindowDuration * 0.75f;
            }

            float exitBlockedPenalty = 0f;
            if (actorTraversal != null && !actorTraversal.IsWindowLandingClear(enter))
            {
                // 窗外落点被其他存活敌人紧贴堵住时，优先分流到同层其他畅通窗口，避免屋内原地排队傻等。
                exitBlockedPenalty = 6.5f * walkSpeed;
            }

            float frontSegmentCost = frontSegment + (window.traversalDuration + queueDelay) * walkSpeed + occupiedDiversionPenalty + exitBlockedPenalty;
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

                    float secondExitPenalty = 0f;
                    if (actorTraversal != null && !actorTraversal.IsWindowLandingClear(awayOpen))
                    {
                        secondExitPenalty = 6.5f * walkSpeed;
                    }

                    Compare(frontSegmentCost + WalkLength(enter.destinationGround, awayOpen.sourceGround, !outside, !outside) + (exitPoint.traversalDuration + exitPoint.EstimateWindowQueueDelay(enemy)) * walkSpeed + secondExitPenalty + WalkLength(awayOpen.destinationGround, lastTarget, outside, targetOutside), enter);
                }
            }
        }

        /// <summary>Compare 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="cost">代价。</param>
        /// <param name="entryPoint">入口点。</param>
        bool IsTemporarilyBlocked(EnemyWindowTraversalRoute route)
        {
            return blockedRoutes.TryGetValue(route, out float until) && Time.time < until;
        }

        bool currentBestCostReady = false;
        void Compare(float cost, EnemyWindowTraversalPlan entryPoint)
        {
            if (float.IsNaN(cost))
            {
                pendingReturn = true;
                return;
            }

            float adjustedCost = cost;
            if (best.route == entryPoint.route)
            {
                currentBestCostReady = true;
                if (!float.IsInfinity(cost))
                {
                    adjustedCost = Mathf.Max(0f, cost - 1.5f);
                    previousRouteCost = Mathf.Min(previousRouteCost, adjustedCost);
                }
            }

            if (adjustedCost < shortest)
            {
                shortest = adjustedCost;
                selected = entryPoint;
            }
        }

        bool canEvaluateBestSwitch = best.route == null
            || IsTemporarilyBlocked(best.route)
            || (!pendingReturn && pendingPathCount == 0 && currentBestCostReady);
        if (canEvaluateBestSwitch && (!pendingReturn || selected.route != null))
        {
            float switchMargin = Mathf.Max(.35f, enemy.Agent.speed * .22f) + 1.15f;
            if (previousRouteCost > shortest + switchMargin || float.IsInfinity(previousRouteCost))
            {
                best = selected;
            }
        }

        result = best;
        return result.route && result.route.isActiveAndEnabled;
    }

    private static int CountRemoteWindowCompetitors(VillagerController enemy, EnemyWindowTraversalRoute route)
    {
        if (route == null || !EnemyAIManager.HasInstance)
        {
            return 0;
        }

        IReadOnlyList<VillagerController> allEnemies = EnemyAIManager.Instance.ReadEnemies;
        if (allEnemies == null)
        {
            return 0;
        }

        Vector3 routeAnchor = route.transform.position;
        float selfSqrDistance = HorizontalSqrDistanceXZ(enemy.transform.position, routeAnchor);
        int selfId = enemy.GetInstanceID();
        int remoteCount = 0;
        for (int i = 0; i < allEnemies.Count; i++)
        {
            VillagerController other = allEnemies[i];
            if (other == null || other == enemy || !other.isActiveAndEnabled || other.IsDead)
            {
                continue;
            }

            if (route.IsQueuedOrOwner(other))
            {
                continue;
            }

            EnemyWindowTraversal otherTraversal = other.ArchitectureReadSpecialTraversal;
            if (otherTraversal != null && otherTraversal.ReadActiveOrPendingWindowRoute() == route)
            {
                // 只统计比当前敌人更优先接近该窗口的远端竞争者，消除双敌对称互罚导致的同步换窗震荡。
                float otherSqrDistance = HorizontalSqrDistanceXZ(other.transform.position, routeAnchor);
                if (otherSqrDistance + 0.05f < selfSqrDistance
                    || (Mathf.Abs(otherSqrDistance - selfSqrDistance) <= 0.05f && other.GetInstanceID() < selfId))
                {
                    remoteCount++;
                }
            }
        }

        return remoteCount;
    }

    /// <summary>Projection 的业务处理入口；本方法直接依赖 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)、RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool)；下游（含内部回调体） HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="outside">是否外部。</param>
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
            nextRefresh = Time.time + .35f;
            foreach (var route in EnemyWindowTraversalRoute.ActiveRoutes)
            {
                if (route == null || !route.isActiveAndEnabled || !string.IsNullOrEmpty(route.platformRegionId)
                    || (route.transform.position - lastPosition).sqrMagnitude > 22f * 22f
                    || (route.transform.position - target).sqrMagnitude > 18f * 18f)
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
        bool hasFirstPortal = PursuitNavigationTopology.TryFindFirstPortal(lastPosition, lastTarget, out DoorController firstDoor, out int sourceSide);
        if (float.IsPositiveInfinity(direct) && hasFirstPortal && firstDoor != null && firstDoor.isActiveAndEnabled && sourceSide != 0
            && !firstDoor.IsSiegeDoorBetween(enemy.transform.position, target))
        {
            Vector3 exteriorPoint = firstDoor.PassageCenter + firstDoor.PassageNormal * (sourceSide * 0.85f);
            Vector3 interiorPoint = firstDoor.PassageCenter - firstDoor.PassageNormal * (sourceSide * 0.85f);
            float approachDoor = WalkLength(origin, GeneralProjection(exteriorPoint));
            float exitDoor = WalkLength(GeneralProjection(interiorPoint), destination);
            if (!float.IsPositiveInfinity(approachDoor) && !float.IsPositiveInfinity(exitDoor))
            {
                if (float.IsNaN(approachDoor) || float.IsNaN(exitDoor))
                {
                    direct = float.NaN;
                }
                else
                {
                    direct = approachDoor + exitDoor + 1.7f;
                }
            }
        }

        HouseThreeTraversalGeometry geometry = enemy.door03TraversalRoute;
        if (geometry != null && geometry.entranceDoor != null &&
            geometry.entranceDoor.IsSiegeDoorBetween(enemy.transform.position, target))
        {
            direct = float.PositiveInfinity;
        }

        float bestSelectedCost = float.PositiveInfinity;
        float walkSpeed = Mathf.Max(.5f, enemy.Agent.speed);
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
            if (float.IsPositiveInfinity(approach) || float.IsPositiveInfinity(exit))
            {
                continue;
            }

            if (!float.IsNaN(direct) && !float.IsPositiveInfinity(direct) && !float.IsNaN(exit) && exit >= direct - 0.5f)
            {
                continue;
            }

            CountRouteCompetitors(
                enemy,
                lastTarget,
                hasFirstPortal,
                firstDoor,
                sourceSide,
                plan.route,
                plan.sourceGround,
                out int directCompetitors,
                out int remoteRouteCompetitors,
                out int totalWindowCompetitors);

            if (!float.IsNaN(direct) && !float.IsPositiveInfinity(direct) && direct <= 7.5f && directCompetitors <= 2)
            {
                continue;
            }

            bool hasDoorwayBottleneck = hasFirstPortal && firstDoor != null && sourceSide != 0 && directCompetitors >= 2;
            bool isIndoorFlankTarget = hasDoorwayBottleneck;
            if (!isIndoorFlankTarget && !float.IsNaN(exit) && exit <= 7.5f)
            {
                if (float.IsPositiveInfinity(direct) || (!float.IsNaN(direct) && !float.IsNaN(approach) && direct > approach + exit + 2.0f))
                {
                    isIndoorFlankTarget = true;
                }
            }

            float effectiveExit = exit;
            float directThreshold = direct - 0.75f;
            bool enableFlankSplit = isIndoorFlankTarget
                && directCompetitors > totalWindowCompetitors
                && !float.IsNaN(approach)
                && approach <= 26f;
            if (enableFlankSplit)
            {
                effectiveExit = exit * 0.25f;
                int advantage = directCompetitors - totalWindowCompetitors;
                if (hasDoorwayBottleneck)
                {
                    directThreshold = direct + directCompetitors * 8.5f + advantage * 18.0f - 0.75f;
                }
                else
                {
                    directThreshold = direct + directCompetitors * 6.5f + advantage * 10.0f - 0.75f;
                }
            }
            else if (isIndoorFlankTarget && directCompetitors > 0)
            {
                directThreshold = direct + directCompetitors * 4.0f - 0.75f;
            }

            float farWindowPenalty = remoteRouteCompetitors * (plan.route.traversalDuration * walkSpeed + 3.5f);
            float cost = approach + effectiveExit
                + walkSpeed * (plan.route.traversalDuration + plan.route.EstimateWindowQueueDelay(enemy))
                + farWindowPenalty;
            if (!float.IsNaN(cost) && best.route == plan.route)
            {
                cost = Mathf.Max(0f, cost - 1.5f);
            }

            if (!float.IsNaN(cost) && cost < directThreshold && cost < bestSelectedCost)
            {
                result = plan;
                bestSelectedCost = cost;
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

    private void CountRouteCompetitors(
        VillagerController self,
        Vector3 targetPos,
        bool hasFirstPortal,
        DoorController firstDoor,
        int sourceSide,
        EnemyWindowTraversalRoute candidateRoute,
        Vector3 candidateSourceGround,
        out int directCompetitors,
        out int remoteRouteCompetitors,
        out int totalWindowCompetitors)
    {
        countedDirectCompetitors.Clear();
        remoteRouteCompetitors = 0;

        if (hasFirstPortal && firstDoor != null && sourceSide != 0 && firstDoor.TraversalOccupancy != null)
        {
            var waiters = firstDoor.TraversalOccupancy.ReadQueue(sourceSide);
            for (int i = 0; i < waiters.Count; i++)
            {
                VillagerController waiter = waiters[i];
                if (waiter == self)
                {
                    break;
                }

                if (waiter != null && waiter.isActiveAndEnabled && waiter.IsActive && !waiter.IsDead)
                {
                    countedDirectCompetitors.Add(waiter);
                }
            }
        }

        Vector3 chokePoint = targetPos;
        if (hasFirstPortal && firstDoor != null)
        {
            chokePoint = firstDoor.PassageCenter;
        }

        float selfChokeDistance = HorizontalDistanceXZ(self.transform.position, chokePoint);
        float selfWindowSqrDistance = HorizontalSqrDistanceXZ(self.transform.position, candidateSourceGround);

        EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager != null)
        {
            IReadOnlyList<VillagerController> allEnemies = manager.ReadEnemies;
            for (int i = 0; i < allEnemies.Count; i++)
            {
                VillagerController other = allEnemies[i];
                if (other == null || other == self || !other.isActiveAndEnabled || !other.IsActive || other.IsDead)
                {
                    continue;
                }

                EnemyWindowTraversal otherTraversal = other.ArchitectureReadSpecialTraversal;
                bool otherTraversingWindow = otherTraversal != null && otherTraversal.IsWindowTraversalActive;
                if (other.CurrentState != VillagerState.Chase && !otherTraversingWindow && !other.DoorwayTraverseMiddle)
                {
                    continue;
                }

                if (HorizontalSqrDistanceXZ(other.transform.position, targetPos) > 24f * 24f)
                {
                    continue;
                }

                EnemyWindowTraversalRoute otherRoute = null;
                if (otherTraversal != null)
                {
                    otherRoute = otherTraversal.ReadActiveOrPendingWindowRoute();
                }

                if (otherRoute == null)
                {
                    if (!countedDirectCompetitors.Contains(other))
                    {
                        float otherChokeDistance = HorizontalDistanceXZ(other.transform.position, chokePoint);
                        if (otherChokeDistance + 0.1f < selfChokeDistance)
                        {
                            countedDirectCompetitors.Add(other);
                        }
                    }
                }
                else if (otherRoute == candidateRoute && !candidateRoute.IsQueuedOrOwner(other))
                {
                    float otherWindowSqrDistance = HorizontalSqrDistanceXZ(other.transform.position, candidateSourceGround);
                    if (otherTraversingWindow || otherWindowSqrDistance + 0.01f < selfWindowSqrDistance)
                    {
                        remoteRouteCompetitors++;
                    }
                }
            }
        }

        directCompetitors = countedDirectCompetitors.Count;
        totalWindowCompetitors = candidateRoute.ReadOccupiedAndWaitingCount(self) + remoteRouteCompetitors;
    }

    private static float HorizontalDistanceXZ(Vector3 first, Vector3 second)
    {
        float dx = first.x - second.x;
        float dz = first.z - second.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float HorizontalSqrDistanceXZ(Vector3 first, Vector3 second)
    {
        float dx = first.x - second.x;
        float dz = first.z - second.z;
        return dx * dx + dz * dz;
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
    /// <param name="startPoint">起始点。</param>
    /// <param name="endPoint">结束点。</param>
    /// <param name="startPointOutside">是否起始点外部。</param>
    /// <param name="endPointOutside">是否结束点外部。</param>
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

        // 键。
var key = (start.position, end.position);
        if (distanceTable.TryGetValue(key, out float /* 中文含义：长度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ length))
        {
            return length;
        }

        // 请求版本。
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

            // 赋值前保存的目标距离表。
global::System.Collections.Generic.Dictionary<(global::UnityEngine.Vector3, global::UnityEngine.Vector3), float> assignmentReceiver4 = distanceTable;
            // 赋值前保存的索引键。
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
