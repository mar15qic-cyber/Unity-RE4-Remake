using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行使用的 PathAgentState 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum PathAgentState
{
    /// <summary>中文含义：待机。</summary>
    Idle,
    /// <summary>中文含义：计算中。</summary>
    Calculating,
    /// <summary>中文含义：有效。</summary>
    Valid,
    /// <summary>中文含义：已抵达。</summary>
    Arrived,
    /// <summary>中文含义：已失败。</summary>
    Failed,
    /// <summary>中文含义：已取消。</summary>
    Cancelled
}

/// <summary>
/// 使用寻路插件计算路径，只输出水平移动方向，不直接修改角色位置。
/// 实际位移统一由 VillagerController.OnAnimatorMove() 执行。
/// </summary>
/// <remarks>A 星路径与移动方向宿主，联合分区导航和正式通路选择路线；实际根对象位姿由运动执行器凭授权提交，异步请求及通路释放遵守既有编号检查。</remarks>
[RequireComponent(typeof(Seeker))]
[RequireComponent(typeof(FunnelModifier))]
[DefaultExecutionOrder(-100)]
public class PathAgent : MonoBehaviour
{
    /// <summary>重新寻路速率。</summary>
    [Header("寻路设置")]
    public float repathRate = 0.5f;
    /// <summary>路径点已抵达半径。</summary>
    public float waypointReachedRadius = 0.5f;
    /// <summary>目的地变化阈值。</summary>
    public float destinationChangeThreshold = 0.5f;
    /// <summary>已抵达目的地变化阈值。</summary>
    public float arrivedDestinationChangeThreshold = 0.2f;
    /// <summary>已失败重试延迟。</summary>
    public float failedRetryDelay = 1f;
    /// <summary>导航图吸附距离。</summary>
    public float graphSnapDistance = 3f;
    /// <summary>导航图吸附高度。</summary>
    public float graphSnapHeight = 1f;
    /// <summary>目的地吸附距离。</summary>
    public float destinationSnapDistance = 1.25f;
    /// <summary>目的地吸附高度。</summary>
    public float destinationSnapHeight = 1.5f;
    /// <summary>追击目的地吸附距离。</summary>
    public float pursuitDestinationSnapDistance = 4f;
    /// <summary>追击目的地吸附高度。</summary>
    public float pursuitDestinationSnapHeight = 2f;
    /// <summary>追击目标级别容差。追击目标投影必须接近目标真实楼层，避免二层目标被投影回楼梯或一层。</summary>
    [Tooltip("追击目标投影必须接近目标真实楼层，避免二层目标被投影回楼梯或一层")]
    [Min(0.1f)]
    public float pursuitTargetLevelTolerance = 0.35f;
    /// <summary>追击地面后备最大掉落。玩家站在谷堆等非法抬高表面时，允许回退到同一区域的较低地面。楼梯采用连续踏面路径。</summary>
    [Tooltip("玩家站在谷堆等非法抬高表面时，允许回退到同一区域的较低地面。楼梯采用连续踏面路径。")]
    [Min(0.1f)]
    public float pursuitGroundFallbackMaximumDrop = 1.8f;
    /// <summary>追击目标解析间隔，避免每名敌人每帧遍历整张导航图。</summary>
    [Tooltip("追击目标解析间隔，避免每名敌人每帧遍历整张导航图。")]
    [Min(0.05f)]
    public float pursuitResolveInterval = 0.2f;
    /// <summary>玩家移动超过该距离时立即重新解析追击目标。</summary>
    [Tooltip("玩家移动超过该距离时立即重新解析追击目标。")]
    [Min(0.05f)]
    public float pursuitResolveMoveThreshold = 0.35f;
    /// <summary>最大普通路径纵向。</summary>
    public float maxOrdinaryPathY = 2.75f;
    /// <summary>最大路径高度上方端点集合。</summary>
    public float maxPathHeightAboveEndpoints = 2f;
    /// <summary>最大路径竖直步进。</summary>
    public float maxPathVerticalStep = 0.6f;
    /// <summary>路径点竖直已抵达容差。</summary>
    public float waypointVerticalReachedTolerance = 0.35f;
    /// <summary>最大路径坡度。</summary>
    [Range(1f, 89f)]
    public float maxPathSlope = 50f;
    /// <summary>是否启用路径点观察前方。</summary>
    [Header("路径拐点裁剪")]
    public bool enableWaypointLookAhead = true;
    /// <summary>最大路径点观察前方。</summary>
    [Range(1, 12)]
    public int maxWaypointLookAhead = 8;
    /// <summary>是否显示路径辅助线集合。</summary>
    public bool showPathGizmos;
    /// <summary>最大静态障碍危险区集合。</summary>
    [Header("静态障碍路线规避")]
    [Min(1)]
    public int maximumStaticObstacleHazards = 3;
    /// <summary>静态障碍危险区存活时长。</summary>
    [Min(0.1f)]
    public float staticObstacleHazardLifetime = 4f;
    /// <summary>静态障碍危险区留白。</summary>
    [Min(0.1f)]
    public float staticObstacleHazardPadding = 0.8f;
    /// <summary>静态障碍代价倍率。</summary>
    [Min(1f)]
    public float staticObstacleCostMultiplier = 12f;
    /// <summary>静态障碍重新寻路延迟。</summary>
    [Min(0.05f)]
    public float staticObstacleRepathDelay = 0.1f;
    /// <summary>是否当前有效路径始终保持追击主线。首次确认真实静态障碍后只辅助重算一次；当前有效路径始终保持追击主线。</summary>
    [Header("追击辅助重算")]
    [Tooltip("首次确认真实静态障碍后只辅助重算一次；当前有效路径始终保持追击主线。")]
    public bool repathOnNewValidatedHazard = true;
    /// <summary>寻路请求器。</summary>
    private Seeker seeker;
    /// <summary>移动控制器。</summary>
    private VillagerController movementController;
    /// <summary>导航连接集合。</summary>
    private EnemyNavigationPreciseLink[] navigationLinks;
    /// <summary>目的地。</summary>
    private Vector3 destination;
    /// <summary>是否具有目的地。</summary>
    private bool hasDestination;
    /// <summary>是否已停止。</summary>
    private bool stopped;
    /// <summary>路径。</summary>
    private List<Vector3> path = new(64);
    /// <summary>局部候选。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<GraphNode> localCandidate = new(128);
    /// <summary>待处理路径。</summary>
    private List<Vector3> pendingPath = new(64);
    /// <summary>精确路径点。</summary>
    private bool[] preciseWaypoint = new bool[64];
    /// <summary>后续精确路径点。</summary>
    private bool[] subsequentPreciseWaypoint = new bool[65];
    /// <summary>剩余线段长型。</summary>
    private float[] remainingSegmentLong = new float[64];
    /// <summary>投影图像版本。</summary>
    private int projectionImageVersion = -1;
    /// <summary>路径图像版本。</summary>
    private int pathImageVersion = -1;
    /// <summary>路径查询服务。</summary>
    private NavigationQueryService pathQueryService;
    /// <summary>是否跨越图层请求。</summary>
    private bool crossLayerRequest;
    /// <summary>请求对象池。</summary>
    private RequestContext[] requestPool;
    /// <summary>pathfindingEnqueueTime 表示“寻路入队时间”；latestTrimRequest 表示“最新裁剪请求”。</summary>
    internal float pathfindingEnqueueTime, latestTrimRequest;
    /// <summary>是否紧急寻路。</summary>
    internal bool UrgentPathfinding => !HasUsablePathData() || crossLayerRequest;

    /// <summary>查询。</summary>
    private NavigationQueryService Query
    {
        get
        {
            if (EnemyAIManager.HasInstance)
            {
                return EnemyAIManager.Instance.NavigationQuery;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>待处理路径版本。</summary>
    private int pendingPathVersion;
    /// <summary>当前路径点。</summary>
    private int currentWaypoint;
    /// <summary>请求版本。</summary>
    private int requestVersion;
    /// <summary>上一次重新寻路时间。</summary>
    private float lastRepathTime;
    /// <summary>下一次重试时间。</summary>
    private float nextRetryTime;
    /// <summary>路径请求原点。</summary>
    private Vector3 pathRequestOrigin;
    /// <summary>静态障碍危险区集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<StaticObstacleHazard> staticObstacleHazards = new List<StaticObstacleHazard>();
    /// <summary>下一次静态障碍重新寻路时间。</summary>
    private float nextStaticObstacleRepathTime;
    /// <summary>是否存在待处理的静态障碍重新寻路。</summary>
    private bool pendingStaticObstacleRepath;
    /// <summary>是否缓存了待执行的静态障碍重新寻路。</summary>
    private bool queuedStaticObstacleRepath;
    /// <summary>追击分辨率缓存是否已初始化。</summary>
    private bool pursuitResolutionCacheInitialized;
    /// <summary>是否缓存追击分辨率已成功。</summary>
    private bool cachedPursuitResolutionSucceeded;
    /// <summary>缓存原始追击目标。</summary>
    private Vector3 cachedRawPursuitTarget;
    /// <summary>缓存解算后的追击目标。</summary>
    private Vector3 cachedResolvedPursuitTarget;
    /// <summary>缓存追击原点区域。</summary>
    private uint cachedPursuitOriginArea;
    /// <summary>下一次追击解算时间。</summary>
    private float nextPursuitResolveTime;
    /// <summary>下一次路径点观察前方时间。</summary>
    private float nextWaypointLookAheadTime;
    /// <summary>追击分辨率失败开始时间。</summary>
    private float pursuitResolutionFailureStartedTime = float.NegativeInfinity;
    /// <summary>当前路径状态。</summary>
    public PathAgentState CurrentPathState { get; private set; } = PathAgentState.Idle;

    /// <summary>当前路径点索引。</summary>
    public int CurrentWaypointIndex
    {
        get
        {
            if (path.Count > 0)
            {
                return currentWaypoint;
            }
            else
            {
                return -1;
            }
        }
    }

    /// <summary>路径请求版本。</summary>
    public int PathRequestVersion => requestVersion;
    /// <summary>是否具有路径。</summary>
    public bool hasPath => CurrentPathState == PathAgentState.Valid && path != null && currentWaypoint < path.Count;
    /// <summary>是否具有目的地。</summary>
    public bool HasDestination => hasDestination;
    /// <summary>目的地。</summary>
    public Vector3 Destination => destination;
    /// <summary>中文含义：是否避让中静态障碍。</summary>
    public bool IsAvoidingStaticObstacle => staticObstacleHazards.Count > 0;
    /// <summary>是否具有追击分辨率失败。</summary>
    public bool HasPursuitResolutionFailure { get; private set; }

    /// <summary>中文含义：追击分辨率失败持续时间。</summary>
    public float PursuitResolutionFailureDuration
    {
        get
        {
            if (HasPursuitResolutionFailure && !float.IsNegativeInfinity(pursuitResolutionFailureStartedTime))
            {
                return Time.time - pursuitResolutionFailureStartedTime;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>TryGetCurrentWaypoint 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)、VillagerController.CollectCombatTimelineSnapshot()、VillagerController.IsStandingAttackSurfaceStable()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.ResolveAllowedPathStepUp() 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waypoint">路径点。输出参数，由本入口写入并交给调用方。</param>
    public bool TryGetCurrentWaypoint(out Vector3 waypoint)
    {
        waypoint = default;
        if (!hasPath)
        {
            return false;
        }

        waypoint = path[currentWaypoint];
        return true;
    }

    /// <summary>是否已停止。</summary>
    public bool isStopped
    {
        get => stopped;
        set
        {
            if (stopped == value)
            {
                return;
            }

            stopped = value;
            if (stopped)
            {
                // 判空前缓存的查询。
global::NavigationQueryService receiverValue4 = Query;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    receiverValue4.Revoke(this);
                }

                pendingPath.Clear();
                requestVersion++;
                pendingStaticObstacleRepath = false;
                queuedStaticObstacleRepath = false;
                if (seeker != null && !seeker.IsDone())
                {
                    CancelCalculate();
                }

                path.Clear();
                currentWaypoint = 0;
                CurrentPathState = PathAgentState.Cancelled;
            }
        }
    }

    /// <summary>剩余距离。</summary>
    public float remainingDistance
    {
        get
        {
            if (!hasPath)
            {
                return 0f;
            }

            return Vector3.Distance(transform.position, path[currentWaypoint]) + remainingSegmentLong[currentWaypoint];
        }
    }

    /// <summary>期望速度。</summary>
    public Vector3 desiredVelocity
    {
        get
        {
            return desiredDirection * speed;
        }
    }

    /// <summary>期望方向。</summary>
    public Vector3 desiredDirection
    {
        get
        {
            if (stopped || !hasPath)
            {
                return Vector3.zero;
            }

            // 方向。
Vector3 direction = path[currentWaypoint] - transform.position;
            direction.y = 0f;
            // 竖直衔接半径。
float verticalContinuationRadius = Mathf.Min(waypointReachedRadius * 0.25f, 0.08f);
            if (direction.sqrMagnitude <= verticalContinuationRadius * verticalContinuationRadius && Mathf.Abs(path[currentWaypoint].y - transform.position.y) > Mathf.Max(0.05f, waypointVerticalReachedTolerance) && currentWaypoint + 1 < path.Count)
            {
                direction = path[currentWaypoint + 1] - transform.position;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }
            else
            {
                return direction.normalized;
            }
        }
    }

    /// <summary>速度。</summary>
    public float speed { get; set; } = 3.5f;

    /// <summary>中文含义：是否事件导航网格。</summary>
    public bool isOnNavMesh
    {
        get
        {
            if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
            {
                return false;
            }

            // 最近。
NNInfo nearest = AstarPath.active.GetNearest(transform.position, NearestNodeConstraint.Walkable);
            if (nearest.node == null)
            {
                return false;
            }

            return Vector3.Distance(transform.position, nearest.position) <= graphSnapDistance;
        }
    }

    /// <summary>BindingPreciseTraversal 的业务处理入口；本方法直接依赖 PathAgent.CachePathProperty()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.Generate(GameObject)、PathAgent.Start()；下游（含内部回调体） PathAgent.CachePathProperty()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="traversal">通行。</param>
    internal void BindingPreciseTraversal(EnemyNavigationPreciseLink[] traversal)
    {
        navigationLinks = traversal;
        if (path.Count > 0)
        {
            CachePathProperty();
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;FunnelModifier&gt;()、Component.GetComponent&lt;Seeker&gt;()、Component.GetComponent&lt;VillagerController&gt;()、Object.GetInstanceID()、PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])、PathAgent.RequestContext.RequestContext(PathAgent)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        seeker = GetComponent<Seeker>();
        movementController = GetComponent<VillagerController>();
        if (navigationLinks == null && EnemyAIManager.HasInstance)
        {
            BindingPreciseTraversal(EnemyAIManager.Instance.preciseTraversal);
        }

        if (seeker != null)
        {
            seeker.drawGizmos = false;
            seeker.detailedGizmos = false;
        }

        // 漏斗。
FunnelModifier funnel = GetComponent<FunnelModifier>();
        if (funnel != null)
        {
            funnel.splitAtEveryPortal = true;
        }

        lastRepathTime = float.NegativeInfinity;
        requestPool = new RequestContext[4];
        // 序号。
for (int sequence = 0; sequence < requestPool.Length; sequence++)
        {
            requestPool[sequence] = new RequestContext(this);
        }

        nextWaypointLookAheadTime = Time.time + (GetInstanceID() & 15) * .00625f;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;Vector3&gt;.Clear()、PathAgent.AdvanceWaypoint()、PathAgent.ApplyPendingPath()、PathAgent.CachePathProperty()、PathAgent.PruneExpiredStaticObstacleHazards()、PathAgent.RequestPath()、PathAgent.TryStartQueuedStaticObstacleRepath() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        PruneExpiredStaticObstacleHazards();
        // 当前查询。
var currentQuery = Query;
        if (currentQuery != null && currentQuery.CanQuery && (pathQueryService != currentQuery || pathImageVersion != currentQuery.Version))
        {
            pathQueryService = currentQuery;
            pathImageVersion = currentQuery.Version;
            requestVersion++;
            pendingPath.Clear();
            if (path.Count > 0)
            {
                CachePathProperty();
            }

            if (hasDestination && !stopped)
            {
                RequestPath();
            }
        }

        ApplyPendingPath();
        if (stopped || !hasDestination || seeker == null)
        {
            return;
        }

        AdvanceWaypoint();
        TryStartQueuedStaticObstacleRepath();
        if (CurrentPathState == PathAgentState.Valid || CurrentPathState == PathAgentState.Calculating || CurrentPathState == PathAgentState.Arrived)
        {
            return;
        }

        if (CurrentPathState == PathAgentState.Failed && Time.time < nextRetryTime)
        {
            return;
        }

        if (Time.time - lastRepathTime >= repathRate && seeker.IsDone())
        {
            RequestPath();
        }
    }

    /// <summary>SetDestination 的业务处理入口；本方法直接依赖 PathAgent.ClearPursuitResolutionFailure()、PathAgent.RejectUnreachableDestination()、PathAgent.SetResolvedDestination(Vector3)、PathAgent.TryResolveReachableDestination(Vector3, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.RequestPathTarget(Vector3)、EnemyPerceptionModule.UpdateInvestigation()、EnemyWindowTraversal.LateUpdate()、VillagerController.RefreshChaseDestination()；下游（含内部回调体） PathAgent.ClearPursuitResolutionFailure()、PathAgent.RejectUnreachableDestination()、PathAgent.SetResolvedDestination(Vector3)、PathAgent.TryResolveReachableDestination(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标。</param>
    public bool SetDestination(Vector3 target)
    {
        if (Query == null || !Query.CanQuery)
        {
            return false;
        }

        if (!TryResolveReachableDestination(target, out Vector3 /* 中文含义：解算后的目标。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ resolvedTarget))
        {
            RejectUnreachableDestination();
            return false;
        }

        ClearPursuitResolutionFailure();
        return SetResolvedDestination(resolvedTarget);
    }

    /// <summary>
    /// 追击专用目标设置。目标必须投影到与玩家同一高度层的可达节点，
    /// 因此二楼玩家不会被错误投影到一楼或楼梯中段。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.RefreshChaseDestination()；下游（含内部回调体） PathAgent.ClearPursuitResolutionFailure()、PathAgent.MarkPursuitResolutionFailed()、PathAgent.SetResolvedDestination(Vector3)、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标。</param>
    public bool SetPursuitDestination(Vector3 target)
    {
        if (Query == null || !Query.CanQuery)
        {
            return false;
        }

        if (!TryResolvePursuitDestination(target, out Vector3 /* 中文含义：解算后的目标。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ resolvedTarget))
        {
            MarkPursuitResolutionFailed();
            return false;
        }

        ClearPursuitResolutionFailure();
        return SetResolvedDestination(resolvedTarget);
    }

    /// <summary>SetResolvedDestination 的业务处理入口；本方法直接依赖 List&lt;Vector3&gt;.Clear()、Mathf.Abs(float)、PathAgent.HasUsablePathData()、PathAgent.RequestPath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetDestination(Vector3)、PathAgent.SetPursuitDestination(Vector3)；下游（含内部回调体） List&lt;Vector3&gt;.Clear()、Mathf.Abs(float)、PathAgent.HasUsablePathData()、PathAgent.RequestPath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="resolvedTarget">解算后的目标。</param>
    private bool SetResolvedDestination(Vector3 resolvedTarget)
    {
        // 阈值。
float threshold;
        if (CurrentPathState == PathAgentState.Arrived)
        {
            threshold = arrivedDestinationChangeThreshold;
        }
        else
        {
            threshold = destinationChangeThreshold;
        }

        if (hasDestination && (resolvedTarget - destination).sqrMagnitude < threshold * threshold)
        {
            return false;
        }

        crossLayerRequest = hasDestination && Mathf.Abs(resolvedTarget.y - destination.y) > pursuitTargetLevelTolerance;
        destination = resolvedTarget;
        hasDestination = true;
        requestVersion++;
        pendingPath.Clear();
        pendingStaticObstacleRepath = false;
        queuedStaticObstacleRepath = false;
        if (!HasUsablePathData())
        {
            CurrentPathState = PathAgentState.Idle;
        }

        if (isActiveAndEnabled && !stopped && seeker != null)
        {
            RequestPath();
        }

        return true;
    }

    /// <summary>RejectUnreachableDestination 的业务处理入口；本方法直接依赖 NavigationQueryService.Revoke(PathAgent)、object.ReferenceEquals(object, object)、PathAgent.CancelCalculate()、PathAgent.MarkPathFailed(bool)、Seeker.IsDone()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetDestination(Vector3)；下游（含内部回调体） NavigationQueryService.Revoke(PathAgent)、object.ReferenceEquals(object, object)、PathAgent.CancelCalculate()、PathAgent.MarkPathFailed(bool)、Seeker.IsDone()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RejectUnreachableDestination()
    {
        // 判空前缓存的查询。
global::NavigationQueryService receiverValue7 = Query;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.Revoke(this);
        }

        requestVersion++;
        pendingStaticObstacleRepath = false;
        queuedStaticObstacleRepath = false;
        if (seeker != null && !seeker.IsDone())
        {
            CancelCalculate();
        }

        hasDestination = false;
        MarkPathFailed();
    }

    /// <summary>MarkPursuitResolutionFailed 的业务处理入口；本方法直接依赖 PathAgent.HasUsablePathData()、PathAgent.MarkPathFailed(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetPursuitDestination(Vector3)；下游（含内部回调体） PathAgent.HasUsablePathData()、PathAgent.MarkPathFailed(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void MarkPursuitResolutionFailed()
    {
        if (HasPursuitResolutionFailure)
        {
            return;
        }

        HasPursuitResolutionFailure = true;
        pursuitResolutionFailureStartedTime = Time.time;
        // 追击投影的单次失败不能抹掉上一条有效主线路径。
        // 如果已经没有路径，则保留旧目的地，让寻路代理仍可按间隔重试。
        if (HasUsablePathData())
        {
            CurrentPathState = PathAgentState.Valid;
            return;
        }

        MarkPathFailed();
    }

    /// <summary>ClearPursuitResolutionFailure 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PathAgent.CancelPath()、PathAgent.SetDestination(Vector3)、PathAgent.SetPursuitDestination(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPursuitResolutionFailure()
    {
        HasPursuitResolutionFailure = false;
        pursuitResolutionFailureStartedTime = float.NegativeInfinity;
    }

    /// <summary>InvalidatePursuitResolutionCache 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、PathAgent.CancelPath()、VillagerController.ApplyPendingPursuitRegionChange() 等共 9 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InvalidatePursuitResolutionCache()
    {
        pursuitResolutionCacheInitialized = false;
        nextPursuitResolveTime = 0f;
    }

    /// <summary>TryResolveReachableDestination 的业务处理入口；本方法直接依赖 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetDestination(Vector3)、VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3)；下游（含内部回调体） PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">原始目标。</param>
    /// <param name="resolvedTarget">解算后的目标。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolveReachableDestination(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        return TryResolveDestination(rawTarget, destinationSnapDistance, destinationSnapHeight, out resolvedTarget);
    }

    /// <summary>TryResolvePursuitDestination 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetPursuitDestination(Vector3)、VillagerController.TryResolvePursuitNavigationPoint(Vector3, out Vector3)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">原始目标。</param>
    /// <param name="resolvedTarget">解算后的目标。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolvePursuitDestination(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return false;
        }

        // originInfo（原点信息）：保存同侧高度约束查询的返回结果，避免穿透薄墙或吸到上方屋檐。
NNInfo originInfo = ReadNormalNavigationPoint(transform.position, 2.5f, 1.5f);
        if (originInfo.node == null || HorizontalDistance(transform.position, originInfo.position) > graphSnapDistance || Mathf.Abs(transform.position.y - ((Vector3)originInfo.position).y) > Mathf.Max(0.1f, graphSnapHeight))
        {
            return false;
        }

        // 图像版本。
int imageVersion;
        if (Query != null)
        {
            imageVersion = Query.Version;
        }
        else
        {
            imageVersion = -1;
        }

        if (projectionImageVersion != imageVersion)
        {
            pursuitResolutionCacheInitialized = false;
            projectionImageVersion = imageVersion;
        }

        // 移动阈值。
float moveThreshold = Mathf.Max(0.05f, pursuitResolveMoveThreshold);
        // 是否相同缓存请求。
bool sameCachedRequest = pursuitResolutionCacheInitialized && cachedPursuitOriginArea == originInfo.node.Area && (rawTarget - cachedRawPursuitTarget).sqrMagnitude < moveThreshold * moveThreshold;
        if (sameCachedRequest && Time.time < nextPursuitResolveTime)
        {
            resolvedTarget = cachedResolvedPursuitTarget;
            return cachedPursuitResolutionSucceeded;
        }

        // 是否已成功。
bool succeeded = TryResolveDestinationOnTargetLevel(rawTarget, originInfo, pursuitDestinationSnapDistance, pursuitDestinationSnapHeight, out resolvedTarget);
        pursuitResolutionCacheInitialized = true;
        cachedPursuitResolutionSucceeded = succeeded;
        cachedRawPursuitTarget = rawTarget;
        cachedResolvedPursuitTarget = resolvedTarget;
        cachedPursuitOriginArea = originInfo.node.Area;
        nextPursuitResolveTime = Time.time + Mathf.Max(0.05f, pursuitResolveInterval);
        return succeeded;
    }

    /// <summary>TryResolveDestinationOnTargetLevel 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Min(float, float)、NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Min(float, float)、NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">原始目标。</param>
    /// <param name="originInfo">原点信息。</param>
    /// <param name="maximumSnapDistance">最大吸附距离。</param>
    /// <param name="maximumSnapHeight">最大吸附高度。</param>
    /// <param name="resolvedTarget">解算后的目标。输出参数，由本入口写入并交给调用方。</param>
    private bool TryResolveDestinationOnTargetLevel(Vector3 rawTarget, NNInfo originInfo, float maximumSnapDistance, float maximumSnapHeight, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (Query == null || !Query.CanQuery)
        {
            return false;
        }

        // 容差。
float tolerance = Mathf.Min(Mathf.Max(.1f, pursuitTargetLevelTolerance), Mathf.Max(.1f, maximumSnapHeight));
        return Query.Projection(rawTarget, originInfo.node.Area, Mathf.Max(.1f, maximumSnapDistance), tolerance, Mathf.Max(tolerance, pursuitGroundFallbackMaximumDrop), maxOrdinaryPathY, out resolvedTarget);
    }

    /// <summary>TryResolveDestination 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryResolveReachableDestination(Vector3, out Vector3)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">原始目标。</param>
    /// <param name="maximumSnapDistance">最大吸附距离。</param>
    /// <param name="maximumSnapHeight">最大吸附高度。</param>
    /// <param name="resolvedTarget">解算后的目标。输出参数，由本入口写入并交给调用方。</param>
    private bool TryResolveDestination(Vector3 rawTarget, float maximumSnapDistance, float maximumSnapHeight, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return false;
        }

        // 原点信息。
NNInfo originInfo = ReadNormalNavigationPoint(transform.position, graphSnapDistance, graphSnapHeight);
        // 目标信息。
NNInfo targetInfo = ReadNormalNavigationPoint(rawTarget, maximumSnapDistance, maximumSnapHeight);
        if (originInfo.node == null || targetInfo.node == null)
        {
            return false;
        }

        if (HorizontalDistance(transform.position, originInfo.position) > graphSnapDistance)
        {
            return false;
        }

        if (Mathf.Abs(transform.position.y - ((Vector3)originInfo.position).y) > Mathf.Max(0.1f, graphSnapHeight))
        {
            return false;
        }

        // 吸附距离。
float snapDistance = Mathf.Max(0.1f, maximumSnapDistance);
        if (HorizontalDistance(rawTarget, targetInfo.position) > snapDistance)
        {
            return false;
        }

        if (Mathf.Abs(rawTarget.y - ((Vector3)targetInfo.position).y) > Mathf.Max(0.1f, maximumSnapHeight))
        {
            return false;
        }

        if (originInfo.node.Area != targetInfo.node.Area && !TryFindConnectedPathOrigin(targetInfo, out _))
        {
            return false;
        }

        resolvedTarget = targetInfo.position;
        return true;
    }

    /// <summary>ReadNormalNavigationPoint 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)、HouseThreeRegionNavigation.TreadRegion(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)、HouseThreeRegionNavigation.TreadRegion(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="distance">距离。</param>
    /// <param name="heightDifference">高度差值。</param>
    private NNInfo ReadNormalNavigationPoint(Vector3 position, float distance, float heightDifference)
    {
        // 区域。
var region = HouseThreeRegionNavigation.Current;
        if (region != null)
        {
            // 区域值。
var regionValue = region.TreadRegion(position);
            if (regionValue == VillageTraversalRegion.SecondFloorIndoors || regionValue == VillageTraversalRegion.SecondFloorOuterPlatform)
            {
                return region.ReadSameSideNavigationPoint(position, regionValue == VillageTraversalRegion.SecondFloorOuterPlatform, distance, heightDifference);
            }
        }

        return AstarPath.active.GetNearest(position, NearestNodeConstraint.Walkable);
    }

    /// <summary>ResetPath 的业务处理入口；本方法直接依赖 PathAgent.CancelPath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.ResolveStandingPathDirectionForCurrentFrame()、VillagerController.UpdateCrowdBypass()；下游（含内部回调体） PathAgent.CancelPath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetPath()
    {
        CancelPath();
    }

    /// <summary>ForceRepath 的业务处理入口；本方法直接依赖 PathAgent.HasUsablePathData()、PathAgent.RequestPath()、Seeker.IsDone()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、VillagerController.TryRecoverIncompletePursuitArrival()、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)、VillagerController.UpdateChaseProgressRecovery()；下游（含内部回调体） PathAgent.HasUsablePathData()、PathAgent.RequestPath()、Seeker.IsDone()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool ForceRepath()
    {
        if (!hasDestination || stopped || seeker == null || !isActiveAndEnabled || !seeker.IsDone())
        {
            return false;
        }

        requestVersion++;
        if (!HasUsablePathData())
        {
            CurrentPathState = PathAgentState.Idle;
        }

        lastRepathTime = float.NegativeInfinity;
        nextRetryTime = 0f;
        RequestPath();
        return true;
    }

    /// <summary>RegisterStaticObstacleHazard 的业务处理入口；本方法直接依赖 List&lt;PathAgent.StaticObstacleHazard&gt;.Add(PathAgent.StaticObstacleHazard)、List&lt;PathAgent.StaticObstacleHazard&gt;.RemoveAt(int)、Mathf.Max(float, float)、Mathf.Max(int, int)、Mathf.Min(float, float)、PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)、PathAgent.PruneExpiredStaticObstacleHazards() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)；下游（含内部回调体） List&lt;PathAgent.StaticObstacleHazard&gt;.Add(PathAgent.StaticObstacleHazard)、List&lt;PathAgent.StaticObstacleHazard&gt;.RemoveAt(int)、Mathf.Max(float, float)、Mathf.Max(int, int)、Mathf.Min(float, float)、PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)、PathAgent.PruneExpiredStaticObstacleHazards() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hit">命中。</param>
    /// <param name="bodyRadius">身体半径。</param>
    public bool RegisterStaticObstacleHazard(RaycastHit hit, float bodyRadius)
    {
        if (!IsValidatedStaticObstacleHit(hit, bodyRadius))
        {
            return false;
        }

        PruneExpiredStaticObstacleHazards();
        // 半径。
float radius = Mathf.Max(0.5f, bodyRadius + Mathf.Max(0.1f, staticObstacleHazardPadding));
        // 有效期限时间。
float expiryTime = Time.time + Mathf.Max(0.1f, staticObstacleHazardLifetime);
        HouseThreeRegionNavigation outerRegion = HouseThreeRegionNavigation.Current;
        if (outerRegion != null && outerRegion.IsOnSecondFloorOuterPlatform(transform.position))
        {
            // 狭窄木板桥与二楼外平台避免单个刮蹭产生大半径长效障碍，防止封死唯一通行通道。
            radius = Mathf.Min(radius, 0.45f);
            expiryTime = Time.time + Mathf.Clamp(staticObstacleHazardLifetime, 0.1f, 1.8f);
        }
        // 已有索引。
int existingIndex = -1;
        // 当前元素索引。
for (int index = 0; index < staticObstacleHazards.Count; index++)
        {
            // 已有。
StaticObstacleHazard existing = staticObstacleHazards[index];
            // 偏移量。
Vector3 offset = existing.center - hit.point;
            offset.y = 0f;
            if (offset.sqrMagnitude <= Mathf.Min(existing.radius, radius) * Mathf.Min(existing.radius, radius))
            {
                existingIndex = index;
                break;
            }
        }

        // 危险区。
StaticObstacleHazard hazard = new StaticObstacleHazard(hit.point, radius, expiryTime);
        // 是否新危险区。
bool isNewHazard = existingIndex < 0;
        if (!isNewHazard)
        {
            staticObstacleHazards[existingIndex] = hazard;
        }
        else
        {
            // 最大数量。
int maximumCount = Mathf.Max(1, maximumStaticObstacleHazards);
            if (staticObstacleHazards.Count >= maximumCount)
            {
                staticObstacleHazards.RemoveAt(0);
            }

            staticObstacleHazards.Add(hazard);
        }

        // 追击本身永远不让位给重算。这里只在首次确认真实障碍时，
        // 在保留当前有效路线的前提下申请一次辅助路径。
        if (!hasDestination || stopped || seeker == null || !isActiveAndEnabled || !repathOnNewValidatedHazard || !isNewHazard)
        {
            return true;
        }

        if (Time.time < nextStaticObstacleRepathTime)
        {
            queuedStaticObstacleRepath = true;
            return true;
        }

        nextStaticObstacleRepathTime = Time.time + Mathf.Max(0.05f, staticObstacleRepathDelay);
        if (!seeker.IsDone())
        {
            // 避免取消正在计算的请求而产生无意义的取消日志。
            // 当前请求结束后会丢弃其结果并立即使用危险区快照重算。
            pendingStaticObstacleRepath = true;
            return true;
        }

        StartStaticObstacleRepath();
        return true;
    }

    /// <summary>CancelPath 的业务处理入口；本方法直接依赖 List&lt;PathAgent.StaticObstacleHazard&gt;.Clear()、NavigationQueryService.Revoke(PathAgent)、object.ReferenceEquals(object, object)、PathAgent.CancelCalculate()、PathAgent.ClearPathData()、PathAgent.ClearPursuitResolutionFailure()、PathAgent.InvalidatePursuitResolutionCache() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.OnDestroy()、PathAgent.OnDisable()、PathAgent.ResetPath()、VillagerController.SetLODActive(bool)、VillagerController.StopAndClearMovement() 等共 6 处；下游（含内部回调体） List&lt;PathAgent.StaticObstacleHazard&gt;.Clear()、NavigationQueryService.Revoke(PathAgent)、object.ReferenceEquals(object, object)、PathAgent.CancelCalculate()、PathAgent.ClearPathData()、PathAgent.ClearPursuitResolutionFailure()、PathAgent.InvalidatePursuitResolutionCache() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelPath()
    {
        // 判空前缓存的查询。
global::NavigationQueryService receiverValue9 = Query;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.Revoke(this);
        }

        requestVersion++;
        pendingStaticObstacleRepath = false;
        queuedStaticObstacleRepath = false;
        if (seeker != null && !seeker.IsDone())
        {
            CancelCalculate();
        }

        ClearPathData();
        hasDestination = false;
        staticObstacleHazards.Clear();
        ClearPursuitResolutionFailure();
        InvalidatePursuitResolutionCache();
        CurrentPathState = PathAgentState.Cancelled;
    }

    /// <summary>IsValidatedStaticObstacleHit 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hit">命中。</param>
    /// <param name="bodyRadius">身体半径。</param>
    private bool IsValidatedStaticObstacleHit(RaycastHit hit, float bodyRadius)
    {
        if (hit.collider == null)
        {
            return false;
        }

        // 最小距离。
float minimumDistance = 0.005f;
        if (hit.distance <= minimumDistance)
        {
            return false;
        }

        // 点。
Vector3 point = hit.point;
        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
        {
            return false;
        }

        // 偏移量。
Vector3 offset = point - transform.position;
        offset.y = 0f;
        // 最大距离。
float maximumDistance = Mathf.Max(0.5f, bodyRadius) + hit.distance + 0.35f;
        if (offset.sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        if (point.sqrMagnitude <= 0.000001f && transform.position.sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        // 表面法线。
Vector3 normal = hit.normal;
        return normal.sqrMagnitude > 0.0001f;
    }

    /// <summary>TryStartQueuedStaticObstacleRepath 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、PathAgent.StartStaticObstacleRepath()、Seeker.IsDone()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.Update()；下游（含内部回调体） Mathf.Max(float, float)、PathAgent.StartStaticObstacleRepath()、Seeker.IsDone()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryStartQueuedStaticObstacleRepath()
    {
        if (!queuedStaticObstacleRepath || stopped || !hasDestination || seeker == null || !seeker.IsDone() || Time.time < nextStaticObstacleRepathTime)
        {
            return;
        }

        queuedStaticObstacleRepath = false;
        nextStaticObstacleRepathTime = Time.time + Mathf.Max(0.05f, staticObstacleRepathDelay);
        StartStaticObstacleRepath();
    }

    /// <summary>StartStaticObstacleRepath 的业务处理入口；本方法直接依赖 PathAgent.HasUsablePathData()、PathAgent.RequestPath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)、PathAgent.TryStartQueuedStaticObstacleRepath()；下游（含内部回调体） PathAgent.HasUsablePathData()、PathAgent.RequestPath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StartStaticObstacleRepath()
    {
        requestVersion++;
        if (!HasUsablePathData())
        {
            CurrentPathState = PathAgentState.Idle;
        }

        lastRepathTime = float.NegativeInfinity;
        nextRetryTime = 0f;
        RequestPath();
    }

    /// <summary>RequestPath 的业务处理入口；本方法直接依赖 NavigationQueryService.QueueUp(PathAgent)、PathAgent.HasUsablePathData()、PathAgent.MarkPathFailed(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ForceRepath()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.SetResolvedDestination(Vector3)、PathAgent.StartStaticObstacleRepath()、PathAgent.Update()；下游（含内部回调体） NavigationQueryService.QueueUp(PathAgent)、PathAgent.HasUsablePathData()、PathAgent.MarkPathFailed(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <summary>在完成翻窗退出或通行切换后立即请求重新寻路，不受常规重寻路间隔节流。</summary>
    public void RequestImmediateRepath()
    {
        lastRepathTime = float.NegativeInfinity;
        nextRetryTime = 0f;
        if (hasDestination)
        {
            RequestPath();
        }
    }

    private void RequestPath()
    {
        if (Query == null)
        {
            MarkPathFailed(HasUsablePathData());
            return;
        }

        Query.QueueUp(this);
    }

    /// <summary>CancelCalculate 的业务处理入口；本方法直接依赖 Seeker.CancelCurrentPathRequest(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.CancelPath()、PathAgent.isStopped.set、PathAgent.RejectUnreachableDestination()；下游（含内部回调体） Seeker.CancelCurrentPathRequest(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CancelCalculate()
    {
        CombatPerformanceCounters.pathfindingCancel++;
        seeker.CancelCurrentPathRequest();
    }

    /// <summary>TryCommitPathfinding 的业务处理入口；本方法直接依赖 ABPath.Construct(Vector3, Vector3, OnPathDelegate)、Mathf.Max(float, float)、PathAgent.PruneExpiredStaticObstacleHazards()、PathAgent.ResolvePhysicallyReachablePathOrigin()、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)、Seeker.IsDone()、Seeker.StartPath(Path)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationQueryService.CommitRequest()；下游（含内部回调体） ABPath.Construct(Vector3, Vector3, OnPathDelegate)、Mathf.Max(float, float)、PathAgent.PruneExpiredStaticObstacleHazards()、PathAgent.ResolvePhysicallyReachablePathOrigin()、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)、Seeker.IsDone()、Seeker.StartPath(Path)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool TryCommitPathfinding()
    {
        if (!isActiveAndEnabled || stopped || !hasDestination || seeker == null || !seeker.IsDone() || Query == null || !Query.CanQuery || requestPool == null)
        {
            return false;
        }

        if (!crossLayerRequest && HasUsablePathData() && Time.time - lastRepathTime < Mathf.Max(.05f, repathRate))
        {
            return false;
        }

        if (!crossLayerRequest && Time.time < nextRetryTime)
        {
            return false;
        }

        // 上下文。
RequestContext context = null;
        // item（当前请求上下文）：每轮从请求对象池取得一个元素，仅代表当前这一项。使用位置：PathAgent.TryCommitPathfinding()。
foreach (var item in requestPool)
        {
            if (item.request == null || item.request.pathID != item.pathIdentifier || item.request.PipelineState == PathState.Returned)
            {
                context = item;
                break;
            }
        }

        if (context == null)
        {
            return false;
        }

        context.version = ++requestVersion;
        context.imageService = Query;
        context.imageVersion = Query.Version;
        context.startPoint = ResolvePhysicallyReachablePathOrigin();
        context.endPoint = destination;
        pathRequestOrigin = context.startPoint;
        lastRepathTime = Time.time;
        crossLayerRequest = false;
        PruneExpiredStaticObstacleHazards();
        context.avoid.Set(staticObstacleHazards, Mathf.Max(1, staticObstacleCostMultiplier));
        // 请求。
ABPath request = ABPath.Construct(context.startPoint, context.endPoint, context.callback);
        context.request = request;
        context.pathIdentifier = request.pathID;
        if (staticObstacleHazards.Count > 0)
        {
            request.traversalConstraint.traversalProvider = context.avoid;
            request.traversalCosts.traversalProvider = context.avoid;
        }

        if (!hasPath)
        {
            CurrentPathState = PathAgentState.Calculating;
        }

        CombatPerformanceCounters.pathfindingCommit++;
        seeker.StartPath(request);
        return true;
    }

    /// <summary>敌人行为、动作与通行模块 RequestContext；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "PathAgent+请求上下文")]
    private sealed class RequestContext
    {
        /// <summary>请求。</summary>
        internal Path request;
        /// <summary>路径标识符。</summary>
        internal ushort pathIdentifier;
        /// <summary>version 表示“版本”；imageVersion 表示“图像版本”。</summary>
        internal int version, imageVersion;
        /// <summary>图像服务。</summary>
        internal NavigationQueryService imageService;
        /// <summary>startPoint 表示“起始点”；endPoint 表示“结束点”。</summary>
        internal Vector3 startPoint, endPoint;
        /// <summary>回调。只读字段，初始化后不由普通写入口替换。</summary>
        internal readonly OnPathDelegate callback;
        /// <summary>避开。只读字段，初始化后不由普通写入口替换。</summary>
        internal readonly StaticObstacleTraversalProvider avoid = new();
        /// <summary>所属寻路代理。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly PathAgent host;
        /// <summary>构造并初始化 RequestContext 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="host">所属寻路代理。</param>
        internal RequestContext(PathAgent host)
        {
            this.host = host;
            callback = Complete;
        }

        /// <summary>Complete 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="result">当前路径。</param>
        private void Complete(Path result)
        {
            if (host != null && ReferenceEquals(request, result) && result.pathID == pathIdentifier)
            {
                host.OnPathComplete(result, this);
            }
        }
    }

    /// <summary>ResolvePhysicallyReachablePathOrigin 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、GraphNode.ClosestPointOnNode(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、PathAgent.LocatedInStaticHazardZone(Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryCommitPathfinding()；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、GraphNode.ClosestPointOnNode(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、PathAgent.LocatedInStaticHazardZone(Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolvePhysicallyReachablePathOrigin()
    {
        if (AstarPath.active == null)
        {
            return transform.position;
        }

        var target = ReadNormalNavigationPoint(destination, 2.5f, 1.5f);
        if (TryFindConnectedPathOrigin(target, out Vector3 entry))
        {
            return entry;
        }

        // 保留原有重试入口。角色仍须通过实际运动检查才能沿路走过去。
        var nearest = ReadNormalNavigationPoint(transform.position, 2.5f, 1.5f);
        if (target.node != null && nearest.node != null && nearest.node.Area == target.node.Area)
        {
            return nearest.position;
        }
        return transform.position;
    }

    internal bool TryFindConnectedPathOrigin(Vector3 target, out Vector3 origin)
    {
        origin = transform.position;
        if (AstarPath.active == null)
        {
            return false;
        }

        return TryFindConnectedPathOrigin(ReadNormalNavigationPoint(target, destinationSnapDistance, destinationSnapHeight), out origin);
    }

    private bool TryFindConnectedPathOrigin(NNInfo targetInfo, out Vector3 origin)
    {
        // 当前位置。
Vector3 currentPosition = transform.position;
        origin = currentPosition;
        if (movementController == null || AstarPath.active == null || targetInfo.node == null)
        {
            return false;
        }

        // 最近信息。
NNInfo nearestInfo = ReadNormalNavigationPoint(currentPosition, 2.5f, 1.5f);
        if (nearestInfo.node != null && nearestInfo.node.Area == targetInfo.node.Area)
        {
            if (HorizontalDistance(currentPosition, nearestInfo.position) <= graphSnapDistance
                && Mathf.Abs(currentPosition.y - ((Vector3)nearestInfo.position).y) <= graphSnapHeight
                && !LocatedInStaticHazardZone(nearestInfo.position) && movementController.CanMoveDirectlyToNavigationPoint(nearestInfo.position, true))
            {
                origin = nearestInfo.position;
                return true;
            }
        // 图上连通不等于胶囊能走到该点。先查找物理可达的相邻起点，
        // 否则贴住台沿时每次重算都会再次把角色交给同一个障碍后方。
        }

        // 最大距离。
float maximumDistance = Mathf.Max(0.5f, graphSnapDistance);
        // 最佳距离平方。
float bestDistanceSqr = maximumDistance * maximumDistance;
        // 最佳点。
Vector3 bestPoint = currentPosition;
        // 是否已找到。
bool found = false;
        if (Query == null || !Query.RecoverCollection(currentPosition, maximumDistance, localCandidate))
        {
            return false;
        }

        // node（节点）：每轮从局部候选取得一个元素，仅代表当前这一项。使用位置：PathAgent.ResolvePhysicallyReachablePathOrigin()。
foreach (var node in localCandidate)
        {
            if (node == null || node.Destroyed || !node.Walkable || node.Area != targetInfo.node.Area)
            {
                continue;
            }

            // 接入节点的实际表面，而非可能位于角色身后很远的三角形中心。
            // 点。
Vector3 point = node.ClosestPointOnNode(currentPosition);
            if (LocatedInStaticHazardZone(point))
            {
                continue;
            }

            if (Mathf.Abs(point.y - currentPosition.y) > Mathf.Max(.1f, graphSnapHeight))
            {
                continue;
            }

            // 距离平方。
float distanceSqr = (point - currentPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || !movementController.CanMoveDirectlyToNavigationPoint(point, true))
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestPoint = point;
            found = true;
        }

        origin = bestPoint;
        return found;
    }

    /// <summary>LocatedInStaticHazardZone 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ResolvePhysicallyReachablePathOrigin()；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    private bool LocatedInStaticHazardZone(Vector3 position)
    {
        // obstacle（障碍）：每轮从静态障碍危险区集合取得一个元素，仅代表当前这一项。使用位置：PathAgent.LocatedInStaticHazardZone(Vector3)。
foreach (var obstacle in staticObstacleHazards)
        {
            // 偏移量。
var offset = position - obstacle.center;
            if (Mathf.Abs(offset.y) > graphSnapHeight)
            {
                continue;
            }

            offset.y = 0f;
            if (offset.sqrMagnitude < obstacle.radius * obstacle.radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>OnPathComplete 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.AddRange(IEnumerable&lt;Vector3&gt;)、List&lt;Vector3&gt;.Clear()、Mathf.Abs(float)、Mathf.NextPowerOfTwo(int)、PathAgent.HasUsablePathData() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.RequestContext.Complete(Path)；下游（含内部回调体） HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.AddRange(IEnumerable&lt;Vector3&gt;)、List&lt;Vector3&gt;.Clear()、Mathf.Abs(float)、Mathf.NextPowerOfTwo(int)、PathAgent.HasUsablePathData() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前路径。</param>
    /// <param name="context">上下文。</param>
    private void OnPathComplete(Path result, RequestContext context)
    {
        if (!isActiveAndEnabled || stopped || !hasDestination || context.version != requestVersion)
        {
            return;
        }

        if (context.imageService != null && (context.imageService != Query || !context.imageService.CanQuery || context.imageVersion != context.imageService.Version))
        {
            RequestPath();
            return;
        }

        using (CombatPerformanceCounters.pathMarker.Auto())
        {
            if (pendingStaticObstacleRepath)
            {
                pendingStaticObstacleRepath = false;
                queuedStaticObstacleRepath = true;
            }

            if (result.error || result.vectorPath == null || result.vectorPath.Count == 0)
            {
                MarkPathFailed(HasUsablePathData());
                return;
            }

            pendingPath.Clear();
            // 容量。
int capacity = result.vectorPath.Count + 1;
            if (pendingPath.Capacity < capacity)
            {
                pendingPath.Capacity = Mathf.NextPowerOfTwo(capacity);
                CombatPerformanceCounters.pathExpandCapacity++;
            }

            pendingPath.AddRange(result.vectorPath);
            // 结束点。
Vector3 endPoint = context.endPoint;
            if (HorizontalDistance(pendingPath[pendingPath.Count - 1], endPoint) > .05f || Mathf.Abs(pendingPath[pendingPath.Count - 1].y - endPoint.y) > .05f)
            {
                pendingPath.Add(endPoint);
            }

            // 区域。
var region = HouseThreeRegionNavigation.Current;
            if (region != null)
            {
                // 序号。
for (int sequence = 0; sequence < pendingPath.Count; sequence++)
                {
                    // 路径点。
Vector3 waypoint = pendingPath[sequence];
                    if (region.traversal.TryResolveStairHeight(waypoint, out float /* 中文含义：踏面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ tread) && Mathf.Abs(waypoint.y - tread) < .38f)
                    {
                        pendingPath[sequence] = new Vector3(waypoint.x, tread, waypoint.z);
                    }
                }
            }

            if (!IsPathHeightValid(pendingPath, context.startPoint, endPoint))
            {
                pendingPath.Clear();
                MarkPathFailed(HasUsablePathData());
                return;
            }

            pendingPathVersion = context.version;
            pathRequestOrigin = context.startPoint;
        }
    }

    /// <summary>ApplyPendingPath 的业务处理入口；本方法直接依赖 List&lt;Vector3&gt;.Clear()、PathAgent.AdvanceWaypoint()、PathAgent.CachePathProperty()、PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.Update()；下游（含内部回调体） List&lt;Vector3&gt;.Clear()、PathAgent.AdvanceWaypoint()、PathAgent.CachePathProperty()、PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyPendingPath()
    {
        if (pendingPath.Count == 0)
        {
            return;
        }

        if (pendingPathVersion != requestVersion || stopped || !hasDestination)
        {
            pendingPath.Clear();
            return;
        }

        // 前一次缓冲区。
var previousBuffer = path;
        path = pendingPath;
        pendingPath = previousBuffer;
        pendingPath.Clear();
        pendingPathVersion = 0;
        CachePathProperty();
        currentWaypoint = ResolvePendingPathHandoffWaypoint(path);
        CurrentPathState = PathAgentState.Valid;
        AdvanceWaypoint();
    }

    /// <summary>CachePathProperty 的业务处理入口；本方法直接依赖 Mathf.NextPowerOfTwo(int)、PathAgent.IsNavigationLinkEndpoint(Vector3)、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ApplyPendingPath()、PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])、PathAgent.Update()；下游（含内部回调体） Mathf.NextPowerOfTwo(int)、PathAgent.IsNavigationLinkEndpoint(Vector3)、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CachePathProperty()
    {
        if (preciseWaypoint.Length < path.Count)
        {
            // 容量。
int capacity = Mathf.NextPowerOfTwo(path.Count);
            preciseWaypoint = new bool[capacity];
            subsequentPreciseWaypoint = new bool[capacity + 1];
            remainingSegmentLong = new float[capacity];
            CombatPerformanceCounters.pathExpandCapacity++;
        }

        subsequentPreciseWaypoint[path.Count] = false;
        // 序号。
for (int sequence = path.Count - 1; sequence >= 0; sequence--)
        {
            preciseWaypoint[sequence] = IsNavigationLinkEndpoint(path[sequence]);
            subsequentPreciseWaypoint[sequence] = preciseWaypoint[sequence] || subsequentPreciseWaypoint[sequence + 1];
            // 赋值前保存的目标剩余线段长型。
float[] assignmentReceiver12 = remainingSegmentLong;
            // 赋值前保存的索引序号。
int assignmentIndex13 = sequence;
            if (sequence + 1 < path.Count)
            {
                assignmentReceiver12[assignmentIndex13] = remainingSegmentLong[sequence + 1] + Vector3.Distance(path[sequence], path[sequence + 1]);
            }
            else
            {
                assignmentReceiver12[assignmentIndex13] = 0;
            }
        }
    }

    /// <summary>ResolvePendingPathHandoffWaypoint 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、Vector3.Dot(Vector3, Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HasContinuousSteppedNavigationGround(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ApplyPendingPath()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、Vector3.Dot(Vector3, Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="completedPath">已完成路径。</param>
    private int ResolvePendingPathHandoffWaypoint(List<Vector3> completedPath)
    {
        if (completedPath == null || completedPath.Count <= 1)
        {
            return 0;
        }

        // 交接点必须与角色当前可抵达的高度层一致。此前这里允许的
        // 高度差高于拐点抵达判定，重算路径会把角色交给一个悬在头顶的
        // 拐点；角色只能围着该点横向绕行，随后又因停滞再次重算。
        // 允许的竖直差值。
float allowedVerticalDifference = Mathf.Max(0.05f, waypointVerticalReachedTolerance);
        // 交接距离。
float handoffDistance = Mathf.Max(1.1f, HorizontalDistance(transform.position, pathRequestOrigin) + waypointReachedRadius * 1.5f);
        // 最大距离平方。
float maximumDistanceSqr = handoffDistance * handoffDistance;
        // 最佳跟随中路径点。
int bestFollowingWaypoint = 0;
        // 最佳距离平方。
float bestDistanceSqr = float.PositiveInfinity;
        // 当前位置。
Vector3 currentPosition = transform.position;
        // 是否包含精确连接。
bool containsPreciseLink = subsequentPreciseWaypoint[0];
        // 当前元素索引。
for (int index = 0; index < completedPath.Count - 1; index++)
        {
            // 起始。
Vector3 start = completedPath[index];
            // 结束。
Vector3 end = completedPath[index + 1];
            if (Mathf.Abs(currentPosition.y - start.y) > allowedVerticalDifference || Mathf.Abs(currentPosition.y - end.y) > allowedVerticalDifference)
            {
                continue;
            }

            // 水平起始。
Vector3 horizontalStart = start;
            // 水平结束。
Vector3 horizontalEnd = end;
            // 水平当前。
Vector3 horizontalCurrent = currentPosition;
            horizontalStart.y = 0f;
            horizontalEnd.y = 0f;
            horizontalCurrent.y = 0f;
            // 线段。
Vector3 segment = horizontalEnd - horizontalStart;
            // 线段长度平方。
float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.0001f)
            {
                continue;
            }

            // 线段进度。
float segmentProgress = Mathf.Clamp01(Vector3.Dot(horizontalCurrent - horizontalStart, segment) / segmentLengthSqr);
            // 最近点。
Vector3 closestPoint = horizontalStart + segment * segmentProgress;
            // 距离平方。
float distanceSqr = (horizontalCurrent - closestPoint).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || distanceSqr > maximumDistanceSqr)
            {
                continue;
            }

            if (movementController != null && (!movementController.CanMoveDirectlyToNavigationPoint(end, true) || (containsPreciseLink && !movementController.HasContinuousSteppedNavigationGround(end))))
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestFollowingWaypoint = index + 1;
        }

        if (bestDistanceSqr <= maximumDistanceSqr)
        {
            return bestFollowingWaypoint;
        }

        if (completedPath.Count >= 2 && HouseThreeRegionNavigation.Current != null
            && HouseThreeRegionNavigation.Current.IsOnSecondFloorOuterPlatform(currentPosition))
        {
            Vector3 wp0 = completedPath[0];
            Vector3 wp1 = completedPath[1];
            Vector3 cur = currentPosition;
            wp0.y = 0f;
            wp1.y = 0f;
            cur.y = 0f;
            Vector3 seg = wp1 - wp0;
            Vector3 toWp0 = wp0 - cur;
            if (seg.sqrMagnitude > 0.0001f && Vector3.Dot(toWp0, seg) < -0.02f
                && (wp1 - cur).sqrMagnitude <= 1.15f * 1.15f)
            {
                return 1;
            }
        }

        return 0;
    }

    /// <summary>AdvanceWaypoint 的业务处理入口；本方法直接依赖 PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CanAdvancePastCompressedSteppedWaypoint()、PathAgent.ClearPathData()、PathAgent.IsWaypointReached(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ApplyPendingPath()、PathAgent.Update()；下游（含内部回调体） PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CanAdvancePastCompressedSteppedWaypoint()、PathAgent.ClearPathData()、PathAgent.IsWaypointReached(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void AdvanceWaypoint()
    {
        if (path.Count == 0)
        {
            return;
        }

        while (currentWaypoint < path.Count && (IsWaypointReached(path[currentWaypoint]) || CanAdvancePastCompressedSteppedWaypoint()))
        {
            currentWaypoint++;
        }

        // 若路径起点落在角色后方（例如重算起点回落到最近节点），而后续非精确拐点已在抵达半径内，
        // 直接推进到其下一路点，避免角色为触碰身后的 wp[0] 原地调头并迎面撞死后方同伴。
        if (currentWaypoint < path.Count && !RequiresPrecisePathFollowing)
        {
            int maxProbe = Mathf.Min(path.Count - 1, currentWaypoint + 2);
            for (int probe = maxProbe; probe > currentWaypoint; probe--)
            {
                bool crossesPrecise = false;
                for (int check = currentWaypoint; check <= probe; check++)
                {
                    if (preciseWaypoint[check])
                    {
                        crossesPrecise = true;
                        break;
                    }
                }

                if (crossesPrecise)
                {
                    continue;
                }

                if (IsWaypointReached(path[probe]))
                {
                    currentWaypoint = probe + 1;
                    break;
                }
            }
        }

        if (currentWaypoint >= path.Count)
        {
            ClearPathData();
            CurrentPathState = PathAgentState.Arrived;
            return;
        }

        AdvanceToVisibleWaypoint();
    }

    /// <summary>AdvanceToVisibleWaypoint 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)、Mathf.Min(int, int)、NavigationQueryService.RequestTrim(PathAgent)、PathAgent.HorizontalDistance(Vector3, Vector3)、ProfilerMarker.Auto()、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceWaypoint()；下游（含内部回调体） Mathf.Clamp(int, int, int)、Mathf.Min(int, int)、NavigationQueryService.RequestTrim(PathAgent)、PathAgent.HorizontalDistance(Vector3, Vector3)、ProfilerMarker.Auto()、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void AdvanceToVisibleWaypoint()
    {
        if (!enableWaypointLookAhead || movementController == null || RequiresPrecisePathFollowing || movementController.IsNavigationTraversalProtected || Time.time < nextWaypointLookAheadTime || currentWaypoint >= path.Count || subsequentPreciseWaypoint[currentWaypoint] || Query == null)
        {
            return;
        }

        // 尾部点。
int tailPoint = path.Count - 1;
        // 候选点。
int candidatePoint = Mathf.Min(tailPoint, currentWaypoint + Mathf.Clamp(maxWaypointLookAhead, 1, 12));
        // 尝试值。
int tryValue = 0;
        // 一次最多两个候选且全局两段；预算不足只保留原路径，绝不放宽地面检测。
        // 点。
for (int point = candidatePoint; point > currentWaypoint && tryValue < 2; point--)
        {
            if (HorizontalDistance(transform.position, path[point]) > 6f)
            {
                continue;
            }

            if (!Query.RequestTrim(this))
            {
                return;
            }

            tryValue++;
            nextWaypointLookAheadTime = Time.time + .1f;
            using (CombatPerformanceCounters.trimMarker.Auto())
                if (movementController.CanMoveDirectlyToNavigationPoint(path[point], true))
                {
                    currentWaypoint = point;
                    return;
                }
        }

        if (tryValue == 0)
        {
            nextWaypointLookAheadTime = Time.time + .1f;
        }
    }

    /// <summary>IsWaypointReached 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceWaypoint()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waypoint">路径点。</param>
    private bool IsWaypointReached(Vector3 waypoint)
    {
        // 半径。
float radius;
        if (currentWaypoint < path.Count && preciseWaypoint[currentWaypoint])
        {
            radius = Mathf.Min(0.025f, waypointReachedRadius);
        }
        else
        {
            radius = waypointReachedRadius;
        }

        return HorizontalDistance(transform.position, waypoint) <= radius && Mathf.Abs(transform.position.y - waypoint.y) <= Mathf.Max(0.05f, waypointVerticalReachedTolerance);
    }

    // 人工连接的端点位于实际通路中心，不能用普通路点半米的容差切角。
    /// <summary>是否需要精确路径跟随中。</summary>
    public bool RequiresPrecisePathFollowing => path != null && currentWaypoint < path.Count && (preciseWaypoint[currentWaypoint] || (currentWaypoint > 0 && preciseWaypoint[currentWaypoint - 1]));

    /// <summary>IsNavigationLinkEndpoint 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PathAgent.CachePathProperty()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waypoint">路径点。</param>
    private bool IsNavigationLinkEndpoint(Vector3 waypoint)
    {
        if (navigationLinks == null)
        {
            return false;
        }

        // preciseLink（精确连接）：每轮从导航连接集合取得一个元素，仅代表当前这一项。使用位置：PathAgent.IsNavigationLinkEndpoint(Vector3)。
foreach (EnemyNavigationPreciseLink preciseLink in navigationLinks)
        {
            if (preciseLink == null || !preciseLink.isActiveAndEnabled)
            {
                continue;
            }

            // 连接。
NodeLink2 link = preciseLink.link;
            if (link == null || !link.isActiveAndEnabled || link.end == null)
            {
                continue;
            }

            if ((waypoint - link.transform.position).sqrMagnitude <= 0.08f * 0.08f || (waypoint - link.end.position).sqrMagnitude <= 0.08f * 0.08f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 低精度寻路图会把数级连续台阶压缩成一个悬在踏面上方的高路点。
    /// 角色水平抵达并越过该点后，若仍强制满足同一帧的垂直误差，期望方向
    /// 会在旧路点与下一路点之间反复翻转。只有移动控制器确认后续整段存在
    /// 连续可踩踏地面时才跳过该中间点，最终目的地仍必须按完整到达条件完成。
    /// </summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceWaypoint()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanAdvancePastCompressedSteppedWaypoint()
    {
        if (movementController == null || path == null || currentWaypoint < 0 || currentWaypoint + 1 >= path.Count)
        {
            return false;
        }

        // 路径点。
Vector3 waypoint = path[currentWaypoint];
        if (HorizontalDistance(transform.position, waypoint) > Mathf.Max(0.05f, waypointReachedRadius) || Mathf.Abs(transform.position.y - waypoint.y) <= Mathf.Max(0.05f, waypointVerticalReachedTolerance))
        {
            return false;
        }

        return movementController.CanAdvancePastCompressedSteppedWaypoint(waypoint, path[currentWaypoint + 1]);
    }

    /// <summary>MarkPathFailed 的业务处理入口；本方法直接依赖 PathAgent.ClearPathData()、PathAgent.HasUsablePathData()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.MarkPursuitResolutionFailed()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.RejectUnreachableDestination()、PathAgent.RequestPath()；下游（含内部回调体） PathAgent.ClearPathData()、PathAgent.HasUsablePathData()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveCurrentPath">是否保留当前路径。</param>
    private void MarkPathFailed(bool preserveCurrentPath = false)
    {
        if (preserveCurrentPath && HasUsablePathData())
        {
            CurrentPathState = PathAgentState.Valid;
            nextRetryTime = Time.time + failedRetryDelay;
            return;
        }

        ClearPathData();
        CurrentPathState = PathAgentState.Failed;
        nextRetryTime = Time.time + failedRetryDelay;
    }

    /// <summary>HasUsablePathData 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PathAgent.ForceRepath()、PathAgent.MarkPathFailed(bool)、PathAgent.MarkPursuitResolutionFailed()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.RequestPath() 等共 8 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasUsablePathData()
    {
        return path != null && currentWaypoint < path.Count;
    }

    /// <summary>ClearPathData 的业务处理入口；本方法直接依赖 List&lt;Vector3&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceWaypoint()、PathAgent.CancelPath()、PathAgent.MarkPathFailed(bool)；下游（含内部回调体） List&lt;Vector3&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPathData()
    {
        path.Clear();
        pendingPath.Clear();
        pendingPathVersion = 0;
        currentWaypoint = 0;
    }

    /// <summary>IsPathHeightValid 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.OnPathComplete(Path, PathAgent.RequestContext)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="points">点集合。</param>
    /// <param name="startPoint">起始点。</param>
    /// <param name="endPoint">结束点。</param>
    private bool IsPathHeightValid(List<Vector3> points, Vector3 startPoint, Vector3 endPoint)
    {
        if (points == null || points.Count == 0)
        {
            return false;
        }

        // 端点天花板。
float endpointCeiling = Mathf.Max(startPoint.y, endPoint.y) + Mathf.Max(0.1f, maxPathHeightAboveEndpoints);
        // 当前遍历索引。
for (int i = 0; i < points.Count; i++)
        {
            // 点。
Vector3 point = points[i];
            if (point.y > endpointCeiling)
            {
                return false;
            }

            // 漏掉起点到第一拐点的校验，会让漏斗裁剪后的首个高点绕过
            // 高差和坡度限制。它正是角色在平地追击时反复对准高处节点
            // 的来源之一。
            // 前一次。
Vector3 previous;
            if (i == 0)
            {
                previous = startPoint;
            }
            else
            {
                previous = points[i - 1];
            }

            // 竖直距离。
float verticalDistance = Mathf.Abs(point.y - previous.y);
            // 水平距离。
float horizontalDistance = HorizontalDistance(previous, point);
            // 坡度。
float slope = Mathf.Atan2(verticalDistance, Mathf.Max(0.001f, horizontalDistance)) * Mathf.Rad2Deg;
            // 最大竖直步进。
float maximumVerticalStep = Mathf.Max(0.1f, maxPathVerticalStep);
            if (verticalDistance > maximumVerticalStep && (horizontalDistance <= waypointReachedRadius * 0.5f || slope > Mathf.Clamp(maxPathSlope, 1f, 89f)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>PruneExpiredStaticObstacleHazards 的业务处理入口；本方法直接依赖 List&lt;PathAgent.StaticObstacleHazard&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)、PathAgent.TryCommitPathfinding()、PathAgent.Update()；下游（含内部回调体） List&lt;PathAgent.StaticObstacleHazard&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void PruneExpiredStaticObstacleHazards()
    {
        // 当前时间。
float now = Time.time;
        // 当前元素索引。
for (int index = staticObstacleHazards.Count - 1; index >= 0; index--)
        {
            if (staticObstacleHazards[index].expiryTime <= now)
            {
                staticObstacleHazards.RemoveAt(index);
            }
        }
    }

    /// <summary>敌人行为、动作与通行的结构化数据 StaticObstacleHazard；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct StaticObstacleHazard
    {
        /// <summary>中心。</summary>
        public Vector3 center;
        /// <summary>半径。</summary>
        public float radius;
        /// <summary>有效期限时间。</summary>
        public float expiryTime;
        /// <summary>构造并初始化 StaticObstacleHazard 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="center">中心。</param>
        /// <param name="radius">半径。</param>
        /// <param name="expiryTime">有效期限时间。</param>
        public StaticObstacleHazard(Vector3 center, float radius, float expiryTime)
        {
            this.center = center;
            this.radius = radius;
            this.expiryTime = expiryTime;
        }
    }

    /// <summary>敌人行为、动作与通行的结构化数据 StaticObstacleHazardSnapshot；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct StaticObstacleHazardSnapshot
    {
        /// <summary>中心。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly Vector3 center;
        /// <summary>半径。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly float radius;
        /// <summary>构造并初始化 StaticObstacleHazardSnapshot 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="center">中心。</param>
        /// <param name="radius">半径。</param>
        public StaticObstacleHazardSnapshot(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    /// <summary>敌人行为、动作与通行模块 StaticObstacleTraversalProvider；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class StaticObstacleTraversalProvider : ITraversalProvider
    {
        /// <summary>危险区集合。</summary>
        private StaticObstacleHazardSnapshot[] hazards = new StaticObstacleHazardSnapshot[3];
        /// <summary>障碍数量。</summary>
        private int obstacleCount;
        /// <summary>代价倍率。</summary>
        private float costMultiplier;
        /// <summary>Set 的业务处理入口；本方法直接依赖 Mathf.NextPowerOfTwo(int)、PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PathAgent.TryCommitPathfinding()；下游（含内部回调体） Mathf.NextPowerOfTwo(int)、PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="obstacle">障碍。</param>
        /// <param name="multiplier">倍率。</param>
        internal void Set(List<StaticObstacleHazard> obstacle, float multiplier)
        {
            if (hazards.Length < obstacle.Count)
            {
                hazards = new StaticObstacleHazardSnapshot[Mathf.NextPowerOfTwo(obstacle.Count)];
                CombatPerformanceCounters.pathExpandCapacity++;
            }

            obstacleCount = obstacle.Count;
            costMultiplier = multiplier;
            // 序号。
for (int sequence = 0; sequence < obstacleCount; sequence++)
            {
                hazards[sequence] = new StaticObstacleHazardSnapshot(obstacle[sequence].center, obstacle[sequence].radius);
            }
        }

        /// <summary>CanTraverse 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode, GraphNode)；下游（含内部回调体） DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalConstraint">通行约束。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="node">节点。</param>
        public bool CanTraverse(ref TraversalConstraint traversalConstraint, GraphNode node)
        {
            return DefaultITraversalProvider.CanTraverse(ref traversalConstraint, node);
        }

        /// <summary>CanTraverse 的业务处理入口；本方法直接依赖 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalConstraint">通行约束。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="from">中文含义：来源。</param>
        /// <param name="to">目标。</param>
        public bool CanTraverse(ref TraversalConstraint traversalConstraint, GraphNode from, GraphNode to)
        {
            return CanTraverse(ref traversalConstraint, to);
        }

        /// <summary>GetConnectionCost 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalCosts">通行代价集合。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="from">来源。</param>
        /// <param name="to">目标。</param>
        public uint GetConnectionCost(ref TraversalCosts traversalCosts, GraphNode from, GraphNode to)
        {
            return DefaultITraversalProvider.GetConnectionCost(ref traversalCosts, from, to);
        }

        /// <summary>GetTraversalCostMultiplier 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalCosts">通行代价集合。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="node">节点。</param>
        public float GetTraversalCostMultiplier(ref TraversalCosts traversalCosts, GraphNode node)
        {
            // 基础倍率。
float baseMultiplier = DefaultITraversalProvider.GetTraversalCostMultiplier(ref traversalCosts, node);
            // 点。
Vector3 point = (Vector3)node.position;
            // 当前元素索引。
for (int index = 0; index < obstacleCount; index++)
            {
                // 偏移量。
Vector3 offset = point - hazards[index].center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= hazards[index].radius * hazards[index].radius)
                {
                    return baseMultiplier * costMultiplier;
                }
            }

            return baseMultiplier;
        }
    }

    /// <summary>HorizontalDistance 的业务处理入口；本方法直接依赖 Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CanAdvancePastCompressedSteppedWaypoint()、PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3)、PathAgent.IsWaypointReached(Vector3)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 等共 8 处；下游（含内部回调体） Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">首个。</param>
    /// <param name="second">第二。</param>
    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PathAgent.CancelPath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        CancelPath();
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PathAgent.CancelPath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        CancelPath();
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Gizmos.DrawLine(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        if (!showPathGizmos || path == null || currentWaypoint >= path.Count)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        // 前一次。
Vector3 previous = transform.position;
        // 当前遍历索引。
for (int i = currentWaypoint; i < path.Count; i++)
        {
            Gizmos.DrawLine(previous, path[i]);
            previous = path[i];
        }
    }
}
