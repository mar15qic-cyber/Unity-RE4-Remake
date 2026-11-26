using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人窗口翻越的场景数据。
/// 只有显式挂载本组件的窗口才允许敌人通过。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WindowTraversalPoint))]
public class EnemyWindowTraversalRoute : MonoBehaviour
{
    /// <summary>生效路线集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<EnemyWindowTraversalRoute> activeRoutes = new List<EnemyWindowTraversalRoute>();
    /// <summary>窗口点。</summary>
    [Header("窗口引用")]
    public WindowTraversalPoint windowPoint;
    /// <summary>平台区域编号。</summary>
    [Header("平台侧")]
    public string platformRegionId = "二楼外平台";
    /// <summary>窗口本地前方向一侧填一，反方向一侧填负一。</summary>
    [Tooltip("窗口本地前方向一侧填一，反方向一侧填负一。")]
    [Range(-1, 1)]
    public int platformSideSign = 1;
    /// <summary>是否允许双方方向集合。</summary>
    public bool allowBothDirections = true;
    /// <summary>中文含义：最小平台侧边深度。</summary>
    [Min(0f)]
    public float minimumPlatformSideDepth = 0.15f;
    /// <summary>中文含义：平台级别容差。</summary>
    [Min(0.1f)]
    public float platformLevelTolerance = 0.9f;
    /// <summary>捕获距离。</summary>
    [Header("敌人队列")]
    [Min(0.1f)]
    public float captureDistance = 0.48f;
    /// <summary>队列间距。</summary>
    [Min(0.2f)]
    public float queueSpacing = 0.9f;
    /// <summary>通行持续时间。</summary>
    [Min(0.1f)]
    public float traversalDuration = 1.07f;
    /// <summary>最大来源级别差值。</summary>
    [Min(0.1f)]
    public float maximumSourceLevelDifference = 0.85f;
    /// <summary>等待者集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<VillagerController> waiters = new List<VillagerController>();
    /// <summary>记录每个等待者当前所属窗侧（1 与 -1 隔离队列排位），防止屋内与屋外相互推挤排位。</summary>
    private readonly Dictionary<VillagerController, int> waiterSideSigns = new Dictionary<VillagerController, int>();
    /// <summary>拥有者。</summary>
    private VillagerController owner;
    /// <summary>通行登记。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly GameArchitecture.TraversalTokenRegistry traversalRegister = new GameArchitecture.TraversalTokenRegistry();
    /// <summary>占用凭证。</summary>
    private GameArchitecture.TraversalToken occupancyToken;
    /// <summary>路线视图。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly System.Collections.ObjectModel.ReadOnlyCollection<EnemyWindowTraversalRoute> routeView = activeRoutes.AsReadOnly();
    /// <summary>生效路线集合。</summary>
    public static IReadOnlyList<EnemyWindowTraversalRoute> ActiveRoutes => routeView;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;WindowTraversalPoint&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (windowPoint == null)
        {
            windowPoint = GetComponent<WindowTraversalPoint>();
        }

        if (platformSideSign >= 0)
        {
            platformSideSign = 1;
        }
        else
        {
            platformSideSign = -1;
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyWindowTraversalRoute&gt;.Add(EnemyWindowTraversalRoute)、List&lt;EnemyWindowTraversalRoute&gt;.Contains(EnemyWindowTraversalRoute)、Object.GetInstanceID()、TraversalTokenRegistry.OpenRelease(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        EnsureWindowReference();
        traversalRegister.OpenRelease(GetInstanceID());
        if (!activeRoutes.Contains(this))
        {
            activeRoutes.Add(this);
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyWindowTraversalRoute&gt;.Remove(EnemyWindowTraversalRoute)、List&lt;VillagerController&gt;.Clear()、TraversalTokenRegistry.Close()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        traversalRegister.Close();
        occupancyToken = default;
        activeRoutes.Remove(this);
        waiters.Clear();
        waiterSideSigns.Clear();
        owner = null;
    }

    /// <summary>TryBuildPlan 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EnemyWindowTraversal&gt;()、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;EnemyWindowTraversal&gt;()、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="targetPosition">中文含义：目标位置。</param>
    /// <param name="targetOnPlatform">是否目标事件平台。</param>
    /// <param name="plan">方案。输出参数，由本入口写入并交给调用方。</param>
    public bool TryBuildPlan(VillagerController enemy, Vector3 targetPosition, bool targetOnPlatform, out EnemyWindowTraversalPlan plan)
    {
        plan = default;
        EnsureWindowReference();
        if (enemy == null || windowPoint == null || windowPoint.IsBarricaded)
        {
            return false;
        }

        // 是否敌人事件平台。
bool enemyOnPlatform;
        // 通行。
EnemyWindowTraversal traversal = enemy.GetComponent<EnemyWindowTraversal>();
        if (traversal == null || !traversal.TryGetPlatformRegionState(platformRegionId, out enemyOnPlatform))
        {
            enemyOnPlatform = IsPointOnPlatformSide(enemy.transform.position);
        }

        if (enemyOnPlatform == targetOnPlatform)
        {
            return false;
        }

        return TryBuildPlanFromSide(enemy, enemyOnPlatform, out plan);
    }

    /// <summary>TryBuildPlanFromSide 的业务处理入口；本方法直接依赖 EnemyWindowTraversalPlan.EnemyWindowTraversalPlan()、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、Mathf.Abs(float)、Quaternion.LookRotation(Vector3, Vector3)、Vector3.Normalize()、WindowTraversalPoint.GetApproachProbePosition(int) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)；下游（含内部回调体） EnemyWindowTraversalPlan.EnemyWindowTraversalPlan()、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、Mathf.Abs(float)、Quaternion.LookRotation(Vector3, Vector3)、Vector3.Normalize()、WindowTraversalPoint.GetApproachProbePosition(int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="sourceOnPlatform">是否来源事件平台。</param>
    /// <param name="plan">方案。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="planFarEnd">是否方案远端结束。</param>
    public bool TryBuildPlanFromSide(VillagerController enemy, bool sourceOnPlatform, out EnemyWindowTraversalPlan plan, bool planFarEnd = false)
    {
        plan = default;
        EnsureWindowReference();
        if (enemy == null || windowPoint == null || windowPoint.IsBarricaded || (planFarEnd && !windowPoint.isActiveAndEnabled))
        {
            return false;
        }

        // 来源侧边符号。
int sourceSideSign;
        if (sourceOnPlatform)
        {
            sourceSideSign = platformSideSign;
        }
        else
        {
            sourceSideSign = -platformSideSign;
        }

        if (!allowBothDirections && sourceSideSign == platformSideSign)
        {
            return false;
        }

        if (!TryFindGround(windowPoint.GetApproachProbePosition(sourceSideSign), out Vector3 /* 中文含义：来源地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceGround) || !TryFindGround(windowPoint.GetLandingProbePosition(sourceSideSign), out Vector3 /* 中文含义：目的地地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ destinationGround))
        {
            return false;
        }

        if (!planFarEnd && Mathf.Abs(enemy.transform.position.y - sourceGround.y) > maximumSourceLevelDifference)
        {
            return false;
        }
        if (string.IsNullOrEmpty(platformRegionId))
        {
            float sill = windowPoint.OpeningCenterWorld.y - windowPoint.openingHeight * .5f;
            float rise = sill - sourceGround.y;
            float fall = sourceGround.y - destinationGround.y;
            if (windowPoint.isFence || rise < -.12f || rise > 1.35f || fall < -.32f
                || fall > windowPoint.ResolveMaximumLandingHeightDifference(.6f))
            {
                return false;
            }
        }

        // 行进方向。
Vector3 travelDirection = destinationGround - sourceGround;
        travelDirection.y = 0f;
        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        travelDirection.Normalize();
        plan = new EnemyWindowTraversalPlan
        {
            route = this,
            sourceGround = sourceGround,
            destinationGround = destinationGround,
            travelDirection = travelDirection,
            facingRotation = Quaternion.LookRotation(travelDirection, Vector3.up),
            sourceSideSign = sourceSideSign
        };
        return true;
    }

    /// <summary>IsPointOnPlatformSide 的业务处理入口；本方法直接依赖 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、EnemyWindowTraversalRoute.EnsureWindowReference()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)、StrictAstarGraphBaker.ValidateElevatedPlatformNodes(RecastGraph, out bool)；下游（含内部回调体） EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、EnemyWindowTraversalRoute.EnsureWindowReference()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    public bool IsPointOnPlatformSide(Vector3 point)
    {
        EnsureWindowReference();
        return windowPoint != null && EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(platformRegionId, point);
    }

    internal bool TryResolveAlternateLanding(Vector3 candidate, Vector3 original, out Vector3 ground)
    {
        var region = HouseThreeRegionNavigation.Current;
        return TryFindGround(candidate, out ground) && Mathf.Abs(ground.y - original.y) <= 0.18f
            && region != null && region.TreadRegion(ground) == region.TreadRegion(original);
    }

    internal float EstimateWindowQueueDelay(VillagerController enemy)
    {
        CleanupWaiters();
        int ahead = waiters.IndexOf(enemy);
        if (ahead < 0)
        {
            ahead = waiters.Count;
        }
        if (owner != null && owner != enemy && !waiters.Contains(owner))
        {
            ahead++;
        }
        return ahead * traversalDuration;
    }

    internal int ReadOccupiedAndWaitingCount(VillagerController exclude)
    {
        CleanupWaiters();
        int count = 0;
        for (int i = 0; i < waiters.Count; i++)
        {
            VillagerController waiter = waiters[i];
            if (waiter != null && waiter != exclude)
            {
                count++;
            }
        }

        if (owner != null && owner != exclude && !waiters.Contains(owner))
        {
            count++;
        }

        return count;
    }

    internal bool IsQueuedOrOwner(VillagerController enemy)
    {
        CleanupWaiters();
        if (enemy == null)
        {
            return false;
        }

        if (owner == enemy)
        {
            return true;
        }

        return waiters.Contains(enemy);
    }

    internal bool CanEnterWindowQueue(VillagerController enemy)
    {
        CleanupWaiters();
        return windowPoint != null && !windowPoint.IsPassageReservedByOther(this) && (owner == null || owner == enemy)
            && (waiters.Count == 0 || waiters[0] == enemy);
    }

    internal bool TryReadQueueLeader(VillagerController enemy, out VillagerController leader, out int queueIndex)
    {
        CleanupWaiters();
        queueIndex = waiters.IndexOf(enemy);
        leader = owner;
        if (leader == null && waiters.Count > 0)
        {
            leader = waiters[0];
        }
        return leader != null && leader != enemy;
    }

    /// <summary>IsPointOnAnyPlatformSide 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.EndAttack()、EnemyPlatformDropRoute.TryBuildPlan(VillagerController, Vector3, out EnemyPlatformDropPlan)、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.ShouldPursueByDrop(Vector3)、VillagerController.IsStandingAttackSurfaceStable()；下游（含内部回调体） EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    public static bool IsPointOnAnyPlatformSide(Vector3 point)
    {
        // 当前遍历索引。
for (int i = 0; i < activeRoutes.Count; i++)
        {
            // 路线。
EnemyWindowTraversalRoute route = activeRoutes[i];
            if (route != null && route.isActiveAndEnabled && route.IsPointOnPlatformSide(point))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>RegisterAndGetWaitingPoint 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.CleanupWaiters()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.IndexOf(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)、Mathf.Abs(float) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)；下游（含内部回调体） EnemyWindowTraversalRoute.CleanupWaiters()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.IndexOf(VillagerController)、List&lt;VillagerController&gt;.Insert(int, VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)、Mathf.Abs(float) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="plan">方案。</param>
    public Vector3 RegisterAndGetWaitingPoint(VillagerController enemy, EnemyWindowTraversalPlan plan)
    {
        CleanupWaiters();
        // 远处和楼下角色先正常赶往入口，抵达同层窗前才参与实体排队。
        // 否则水平距离很近的楼下角色会占据队首，却无法开始翻越。
        if (enemy != owner && (Mathf.Abs(enemy.transform.position.y - plan.sourceGround.y) > maximumSourceLevelDifference || Vector3.ProjectOnPlane(enemy.transform.position - plan.sourceGround, Vector3.up).sqrMagnitude > 9f))
        {
            waiters.Remove(enemy);
            waiterSideSigns.Remove(enemy);
            return plan.sourceGround;
        }

        waiterSideSigns[enemy] = plan.sourceSideSign;

        if (!waiters.Contains(enemy))
        {
            // 同帧发现窗口的顺序来自脚本更新顺序，不代表抵达窗口的顺序。
            // 新加入者按窗前距离插队，避免远处队首被近处等候者堵在后方。
            // 已开始翻越的占用者始终保留优先权。
            // 新加入者偏移量。
Vector3 newcomerOffset = enemy.transform.position - plan.sourceGround;
            newcomerOffset.y = 0f;
            // 插入索引。
int insertIndex = waiters.Count;
            // 当前遍历索引。
for (int i = 0; i < waiters.Count; i++)
            {
                if (waiters[i] == owner)
                {
                    continue;
                }

                // 等待者偏移量。
Vector3 waiterOffset = waiters[i].transform.position - plan.sourceGround;
                waiterOffset.y = 0f;
                if (newcomerOffset.sqrMagnitude + 0.01f < waiterOffset.sqrMagnitude)
                {
                    insertIndex = i;
                    break;
                }
            }

            waiters.Insert(insertIndex, enemy);
        }

        // 前排通过后，原队首可能被已经走到窗前的后排挡住。
        // 空闲窗口优先接纳实际抵达入口的人；只在距离明显领先时换首，
        // 不逐帧重排整队，也不抢占正在翻越的角色。
        // 当前距离窗口。
Vector3 currentDistanceWindow = Vector3.ProjectOnPlane(enemy.transform.position - plan.sourceGround, Vector3.up);
        if (owner == null && waiters.Count > 1 && waiters[0] != enemy && currentDistanceWindow.magnitude <= Mathf.Max(captureDistance, queueSpacing * 1.5f))
        {
            // 队列首个距离窗口。
float queueFirstDistanceWindow = Vector3.ProjectOnPlane(waiters[0].transform.position - plan.sourceGround, Vector3.up).magnitude;
            if (queueFirstDistanceWindow > currentDistanceWindow.magnitude + Mathf.Max(.25f, queueSpacing * .5f))
            {
                waiters.Remove(enemy);
                waiters.Insert(0, enemy);
            }
        }

        // 同一窗侧分别计算排位索引，避免对面楼层排队占满本侧候场位。
        int index = 0;
        for (int i = 0; i < waiters.Count; i++)
        {
            VillagerController candidate = waiters[i];
            if (candidate == enemy)
            {
                break;
            }

            if (waiterSideSigns.TryGetValue(candidate, out int candidateSide) && candidateSide == plan.sourceSideSign)
            {
                index++;
            }
        }

        // 向外。
Vector3 outward = windowPoint.NormalWorld * plan.sourceSideSign;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.001f)
        {
            outward.Normalize();
        }

        // 等待中点。
Vector3 waitingPoint = plan.sourceGround + outward * index * queueSpacing;
        if (index == 0)
        {
            return plan.sourceGround;
        }

        // 窄平台的队尾可能越过木板边缘，先尝试沿墙侧向分摊排队位，无有效支撑再原地等待前方释放。
        Vector3 waitingGround = plan.sourceGround;
        int sampleCount = Mathf.Clamp(Mathf.CeilToInt(index * queueSpacing / 0.2f), 1, 32);
        bool straightLineValid = true;
        for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            Vector3 sample = Vector3.Lerp(plan.sourceGround, waitingPoint, sampleIndex / (float)sampleCount);
            if (!TryFindGround(sample, out waitingGround) || Mathf.Abs(waitingGround.y - plan.sourceGround.y) > Mathf.Max(0.15f, enemy.maximumGroundStepUp))
            {
                straightLineValid = false;
                break;
            }
        }

        if (straightLineValid)
        {
            return waitingGround;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, outward);
        float stepSide;
        if ((index & 1) == 1)
        {
            stepSide = 1f;
        }
        else
        {
            stepSide = -1f;
        }

        int tier = (index + 1) / 2;
        float[] tangentMultipliers = { stepSide * tier, -stepSide * tier, stepSide * (tier + 1) };
        for (int candidateIndex = 0; candidateIndex < tangentMultipliers.Length; candidateIndex++)
        {
            Vector3 lateralCandidate = plan.sourceGround + outward * 0.45f + tangent * (tangentMultipliers[candidateIndex] * Mathf.Max(0.65f, queueSpacing * 0.8f));
            if (TryFindGround(lateralCandidate, out Vector3 lateralGround) &&
                Mathf.Abs(lateralGround.y - plan.sourceGround.y) <= Mathf.Max(0.15f, enemy.maximumGroundStepUp))
            {
                return lateralGround;
            }
        }

        return enemy.transform.position;
    }

    /// <summary>TryAcquire 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public bool TryAcquire(VillagerController enemy) => TryRequest(enemy, out _);
    /// <summary>通行身份标识。</summary>
    public GameArchitecture.ObjectIdentity TraversalIdentity => traversalRegister.Identity;

    /// <summary>RequestTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.RequestWindowOccupancy(EnemyWindowTraversalRoute)、EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)；下游（含内部回调体） EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="request">请求。</param>
    public GameArchitecture.TraversalRequestResult RequestTraversal(VillagerController enemy, GameArchitecture.RequestContext request)
    {
        if (!isActiveAndEnabled || enemy == null || enemy.actionCoordinator == null)
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.ObjectHasInvalidate);
        }

        if (enemy.IsDead)
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.HasDeath);
        }

        // 原因。
var reason = traversalRegister.ValidateRequest(request, enemy.actionCoordinator.CurrentHandle);
        if (reason != GameArchitecture.RejectionReason.None)
        {
            return GameArchitecture.TraversalRequestResult.Reject(reason);
        }

        if (TryRequestCore(enemy, out var /* 中文含义：凭证。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ token))
        {
            return GameArchitecture.TraversalRequestResult.Accept(token);
        }
        else
        {
            return GameArchitecture.TraversalRequestResult.Reject(GameArchitecture.RejectionReason.PermissionBeingOccupancy);
        }
    }

    /// <summary>TryRequest 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.TryAcquire(VillagerController)；下游（含内部回调体） EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="token">凭证。输出参数，由本入口写入并交给调用方。</param>
    public bool TryRequest(VillagerController enemy, out GameArchitecture.TraversalToken token)
    {
        // 动作。
global::GameArchitecture.ActionHandle action;
        if (enemy != null && enemy.actionCoordinator != null)
        {
            action = enemy.actionCoordinator.CurrentHandle;
        }
        else
        {
            action = default;
        }

        // 当前通行请求结果。
var result = RequestTraversal(enemy, new GameArchitecture.RequestContext("通路兼容入口", action.Object, TraversalIdentity, action.Id));
        token = result.Token;
        return result.HasAccept;
    }

    /// <summary>TryRequestCore 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)；下游（含内部回调体） EnemyWindowTraversalRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="token">凭证。输出参数，由本入口写入并交给调用方。</param>
    private bool TryRequestCore(VillagerController enemy, out GameArchitecture.TraversalToken token)
    {
        token = default;
        if (!isActiveAndEnabled || enemy == null || enemy.actionCoordinator == null || !enemy.actionCoordinator.CurrentHandle.Valid)
        {
            return false;
        }

        CleanupWaiters();
        if (windowPoint == null || windowPoint.IsPassageReservedByOther(this) || owner != null && owner != enemy)
        {
            return false;
        }

        if (waiters.Count > 0 && waiters[0] != enemy)
        {
            return false;
        }

        owner = enemy;
        occupancyToken = traversalRegister.Register(enemy.actionCoordinator.CurrentHandle);
        token = occupancyToken;
        return token.Valid;
    }

    /// <summary>Release 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public void Release(VillagerController enemy)
    {
        if (owner == enemy)
        {
            ReleaseTraversal(occupancyToken, GameArchitecture.ReleaseReason.Cancel);
        }

        waiters.Remove(enemy);
        waiterSideSigns.Remove(enemy);
    }

    internal bool HasCrossingActor => owner != null && TraversalValid(occupancyToken);

    /// <summary>EnsureWindowReference 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;WindowTraversalPoint&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)；下游（含内部回调体） Component.GetComponent&lt;WindowTraversalPoint&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureWindowReference()
    {
        if (windowPoint == null)
        {
            windowPoint = GetComponent<WindowTraversalPoint>();
        }
    }

    /// <summary>TryAcquireWindowSideTread 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、WindowTraversalPoint.GetApproachProbePosition(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StrictAstarGraphBaker.ApplyHouseThreeRegionRules(HouseThreeRegionNavigation, AstarPath)、StrictAstarGraphBaker.CanPassValidWindowReach(GraphNode, GraphNode)、StrictAstarGraphBaker.ValidatePlatformSeamWalk(HouseThreeRegionNavigation, AstarPath)；下游（含内部回调体） EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、WindowTraversalPoint.GetApproachProbePosition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="platformSide">是否平台侧边。</param>
    /// <param name="result">结果。输出参数，由本入口写入并交给调用方。</param>
    public bool TryAcquireWindowSideTread(bool platformSide, out Vector3 result)
    {
        EnsureWindowReference();
        result = default;
        if (windowPoint)
        {
            // 调用前缓存的窗口点。
global::WindowTraversalPoint callReceiver6 = windowPoint;
            // 分支选出的平台侧边符号。
int selectedValue5;
            if (platformSide)
            {
                selectedValue5 = platformSideSign;
            }
            else
            {
                selectedValue5 = -platformSideSign;
            }

            return TryFindGround(callReceiver6.GetApproachProbePosition(selectedValue5), out result);
        }
        else
        {
            return false;
        }
    }

    /// <summary>TryFindGround 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、WindowTraversalPoint.ShouldIgnoreCollider(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、WindowTraversalPoint.ShouldIgnoreCollider(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probePosition">探测位置。</param>
    /// <param name="groundPoint">地面点。输出参数，由本入口写入并交给调用方。</param>
    private bool TryFindGround(Vector3 probePosition, out Vector3 groundPoint)
    {
        groundPoint = default;
        // 原点。
Vector3 origin = probePosition;
        origin.y = windowPoint.OpeningCenterWorld.y + windowPoint.openingHeight * 0.5f + windowPoint.groundProbeHeight;
        // 命中结果。
RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, windowPoint.groundProbeDistance, windowPoint.groundMask, QueryTriggerInteraction.Ignore);
        // 最近距离。
float nearestDistance = float.MaxValue;
        // 是否已找到。
bool found = false;
        // 当前遍历索引。
for (int i = 0; i < hits.Length; i++)
        {
            // 候选。
Collider candidate = hits[i].collider;
            if (candidate == null || windowPoint.ShouldIgnoreCollider(candidate) || candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null || hits[i].normal.y < 0.45f || hits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            groundPoint = hits[i].point;
            found = true;
        }

        return found;
    }

    /// <summary>TraversalValid 的业务处理入口；本方法直接依赖 TraversalTokenRegistry.Valid(TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.Update()；下游（含内部回调体） TraversalTokenRegistry.Valid(TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    public bool TraversalValid(GameArchitecture.TraversalToken token) => isActiveAndEnabled && traversalRegister.Valid(token);
    /// <summary>ReleaseTraversal 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.Remove(VillagerController)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversalRoute.Release(VillagerController)；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="reason">原因。</param>
    public bool ReleaseTraversal(GameArchitecture.TraversalToken token, GameArchitecture.ReleaseReason reason)
    {
        if (!traversalRegister.Release(token, reason))
        {
            return false;
        }

        if (owner != null)
        {
            waiterSideSigns.Remove(owner);
        }
        waiters.Remove(owner);
        owner = null;
        occupancyToken = default;
        return true;
    }

    /// <summary>CancelWait 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    public void CancelWait(VillagerController character)
    {
        if (owner != character)
        {
            waiters.Remove(character);
            if (character != null)
            {
                waiterSideSigns.Remove(character);
            }
        }
    }

    /// <summary>CleanupWaiters 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)；下游（含内部回调体） List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupWaiters()
    {
        // 当前遍历索引。
for (int i = waiters.Count - 1; i >= 0; i--)
        {
            // 等待者。
VillagerController waiter = waiters[i];
            if (waiter == null || waiter.IsDead || !waiter.isActiveAndEnabled
                || waiter != owner && waiter.CurrentState != VillagerState.Chase)
            {
                waiters.RemoveAt(i);
                if (waiter != null)
                {
                    waiterSideSigns.Remove(waiter);
                }
            }
        }

        if (owner == null || owner.IsDead || !owner.isActiveAndEnabled || owner.actionCoordinator == null || occupancyToken.Action.Object != owner.actionCoordinator.Identity)
        {
            traversalRegister.Release(occupancyToken, GameArchitecture.ReleaseReason.Deactivate);
            owner = null;
            occupancyToken = default;
        }
    }
}

/// <summary>敌人行为、动作与通行的结构化数据 EnemyWindowTraversalPlan；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
public struct EnemyWindowTraversalPlan
{
    /// <summary>路线。</summary>
    public EnemyWindowTraversalRoute route;
    /// <summary>来源地面。</summary>
    public Vector3 sourceGround;
    /// <summary>目的地地面。</summary>
    public Vector3 destinationGround;
    /// <summary>行进方向。</summary>
    public Vector3 travelDirection;
    /// <summary>朝向旋转。</summary>
    public Quaternion facingRotation;
    /// <summary>来源侧边符号。</summary>
    public int sourceSideSign;
}
