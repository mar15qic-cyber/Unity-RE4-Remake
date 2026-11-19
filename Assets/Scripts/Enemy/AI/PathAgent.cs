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
    /// <summary>中文含义：重新寻路速率。使用位置：作为 Mathf.Max(float, float) 的参数、PathAgent.Update()。</summary>
    [Header("寻路设置")]
    public float repathRate = 0.5f;
    /// <summary>中文含义：路径点已抵达半径。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3) 等共 4 处。</summary>
    public float waypointReachedRadius = 0.5f;
    /// <summary>中文含义：目的地变化阈值。使用位置：PathAgent.SetResolvedDestination(Vector3)、SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool)、VillagerController.UpdateChaseProgressRecovery()。</summary>
    public float destinationChangeThreshold = 0.5f;
    /// <summary>中文含义：已抵达目的地变化阈值。使用位置：作为 Mathf.Max(float, float) 的参数、PathAgent.SetResolvedDestination(Vector3)、SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool)。</summary>
    public float arrivedDestinationChangeThreshold = 0.2f;
    /// <summary>中文含义：已失败重试延迟。使用位置：PathAgent.MarkPathFailed(bool)。</summary>
    public float failedRetryDelay = 1f;
    /// <summary>中文含义：导航图吸附距离。使用位置：作为 Mathf.Max(float, float) 的参数、作为 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的参数、PathAgent.isOnNavMesh.get 等共 7 处。</summary>
    public float graphSnapDistance = 3f;
    /// <summary>中文含义：导航图吸附高度。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的参数 等共 4 处。</summary>
    public float graphSnapHeight = 1f;
    /// <summary>中文含义：目的地吸附距离。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、作为 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3) 的参数。</summary>
    public float destinationSnapDistance = 1.25f;
    /// <summary>中文含义：目的地吸附高度。使用位置：作为 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3) 的参数。</summary>
    public float destinationSnapHeight = 1.5f;
    /// <summary>中文含义：追击目的地吸附距离。使用位置：作为 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的参数。</summary>
    public float pursuitDestinationSnapDistance = 4f;
    /// <summary>中文含义：追击目的地吸附高度。使用位置：作为 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的参数。</summary>
    public float pursuitDestinationSnapHeight = 2f;
    /// <summary>中文含义：追击目标级别容差。追击目标投影必须接近目标真实楼层，避免二层目标被投影回楼梯或一层。使用位置：作为 Mathf.Max(float, float) 的参数、PathAgent.SetResolvedDestination(Vector3)。</summary>
    [Tooltip("追击目标投影必须接近目标真实楼层，避免二层目标被投影回楼梯或一层")]
    [Min(0.1f)]
    public float pursuitTargetLevelTolerance = 0.35f;
    /// <summary>中文含义：追击地面后备最大掉落。玩家站在谷堆等非法抬高表面时，允许回退到同一区域的较低地面。楼梯采用连续踏面路径。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("玩家站在谷堆等非法抬高表面时，允许回退到同一区域的较低地面。楼梯采用连续踏面路径。")]
    [Min(0.1f)]
    public float pursuitGroundFallbackMaximumDrop = 1.8f;
    /// <summary>中文含义：追击目标解析间隔，避免每名敌人每帧遍历整张导航图。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("追击目标解析间隔，避免每名敌人每帧遍历整张导航图。")]
    [Min(0.05f)]
    public float pursuitResolveInterval = 0.2f;
    /// <summary>中文含义：玩家移动超过该距离时立即重新解析追击目标。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("玩家移动超过该距离时立即重新解析追击目标。")]
    [Min(0.05f)]
    public float pursuitResolveMoveThreshold = 0.35f;
    /// <summary>中文含义：最大普通路径纵向。使用位置：作为 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3) 的参数。</summary>
    public float maxOrdinaryPathY = 2.75f;
    /// <summary>中文含义：最大路径高度上方端点集合。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float maxPathHeightAboveEndpoints = 2f;
    /// <summary>中文含义：最大路径竖直步进。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float maxPathVerticalStep = 0.6f;
    /// <summary>中文含义：路径点竖直已抵达容差。使用位置：作为 Mathf.Max(float, float) 的参数、SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 等共 5 处。</summary>
    public float waypointVerticalReachedTolerance = 0.35f;
    /// <summary>中文含义：最大路径坡度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(1f, 89f)]
    public float maxPathSlope = 50f;
    /// <summary>中文含义：是否启用路径点观察前方。使用位置：PathAgent.AdvanceToVisibleWaypoint()。</summary>
    [Header("路径拐点裁剪")]
    public bool enableWaypointLookAhead = true;
    /// <summary>中文含义：最大路径点观察前方。使用位置：作为 Mathf.Clamp(int, int, int) 的参数。</summary>
    [Range(1, 12)]
    public int maxWaypointLookAhead = 8;
    /// <summary>中文含义：是否显示路径辅助线集合。使用位置：PathAgent.OnDrawGizmosSelected()。</summary>
    public bool showPathGizmos;
    /// <summary>中文含义：最大静态障碍危险区集合。使用位置：作为 Mathf.Max(int, int) 的参数。</summary>
    [Header("静态障碍路线规避")]
    [Min(1)]
    public int maximumStaticObstacleHazards = 3;
    /// <summary>中文含义：静态障碍危险区存活时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float staticObstacleHazardLifetime = 4f;
    /// <summary>中文含义：静态障碍危险区留白。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float staticObstacleHazardPadding = 0.8f;
    /// <summary>中文含义：静态障碍代价倍率。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(1f)]
    public float staticObstacleCostMultiplier = 12f;
    /// <summary>中文含义：静态障碍重新寻路延迟。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.05f)]
    public float staticObstacleRepathDelay = 0.1f;
    /// <summary>中文含义：是否当前有效路径始终保持追击主线。首次确认真实静态障碍后只辅助重算一次；当前有效路径始终保持追击主线。使用位置：PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。</summary>
    [Header("追击辅助重算")]
    [Tooltip("首次确认真实静态障碍后只辅助重算一次；当前有效路径始终保持追击主线。")]
    public bool repathOnNewValidatedHazard = true;
    /// <summary>中文含义：寻路请求器。使用位置：PathAgent.CancelCalculate()、PathAgent.CancelPath()、PathAgent.ForceRepath() 等共 11 处。</summary>
    private Seeker seeker;
    /// <summary>中文含义：移动控制器。使用位置：PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CanAdvancePastCompressedSteppedWaypoint()、PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;) 等共 5 处。</summary>
    private VillagerController movementController;
    /// <summary>中文含义：导航连接集合。使用位置：PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])、PathAgent.IsNavigationLinkEndpoint(Vector3)、PathAgent.Start()。</summary>
    private EnemyNavigationPreciseLink[] navigationLinks;
    /// <summary>中文含义：目的地。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 Mathf.Abs(float) 的参数、PathAgent.Destination.get 等共 5 处。</summary>
    private Vector3 destination;
    /// <summary>中文含义：是否具有目的地。使用位置：PathAgent.ApplyPendingPath()、PathAgent.CancelPath()、PathAgent.ForceRepath() 等共 11 处。</summary>
    private bool hasDestination;
    /// <summary>中文含义：是否已停止。使用位置：PathAgent.ApplyPendingPath()、PathAgent.desiredDirection.get、PathAgent.ForceRepath() 等共 11 处。</summary>
    private bool stopped;
    /// <summary>中文含义：路径。使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Min(int, int) 的参数 等共 29 处。</summary>
    private List<Vector3> path = new(64);
    /// <summary>中文含义：局部候选。只读字段，初始化后不由普通写入口替换。使用位置：作为 NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;) 的参数、PathAgent.ResolvePhysicallyReachablePathOrigin()。</summary>
    private readonly List<GraphNode> localCandidate = new(128);
    /// <summary>中文含义：待处理路径。使用位置：作为 List&lt;Vector3&gt;.this[int] 的参数、作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数 等共 10 处。</summary>
    private List<Vector3> pendingPath = new(64);
    /// <summary>中文含义：精确路径点。使用位置：PathAgent.CachePathProperty()、PathAgent.IsWaypointReached(Vector3)、PathAgent.RequiresPrecisePathFollowing.get。</summary>
    private bool[] preciseWaypoint = new bool[64];
    /// <summary>中文含义：后续精确路径点。使用位置：PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CachePathProperty()、PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)。</summary>
    private bool[] subsequentPreciseWaypoint = new bool[65];
    /// <summary>中文含义：剩余线段长型。使用位置：PathAgent.CachePathProperty()、PathAgent.remainingDistance.get。</summary>
    private float[] remainingSegmentLong = new float[64];
    /// <summary>中文含义：投影图像版本。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private int projectionImageVersion = -1;
    /// <summary>中文含义：路径图像版本。使用位置：PathAgent.Update()。</summary>
    private int pathImageVersion = -1;
    /// <summary>中文含义：路径查询服务。使用位置：PathAgent.Update()。</summary>
    private NavigationQueryService pathQueryService;
    /// <summary>中文含义：是否跨越图层请求。使用位置：PathAgent.SetResolvedDestination(Vector3)、PathAgent.TryCommitPathfinding()、PathAgent.UrgentPathfinding.get。</summary>
    private bool crossLayerRequest;
    /// <summary>中文含义：请求对象池。使用位置：PathAgent.Start()、PathAgent.TryCommitPathfinding()。</summary>
    private RequestContext[] requestPool;
    /// <summary>中文含义：pathfindingEnqueueTime 表示“寻路入队时间”；latestTrimRequest 表示“最新裁剪请求”。使用位置：作为 Math.Max(double, double) 的参数、NavigationQueryService.CommitRequest()、NavigationQueryService.QueueUp(PathAgent)。；使用位置：NavigationQueryService.RequestTrim(PathAgent)。</summary>
    internal float pathfindingEnqueueTime, latestTrimRequest;
    /// <summary>中文含义：是否紧急寻路。使用位置：NavigationQueryService.CommitRequest()。</summary>
    internal bool UrgentPathfinding => !HasUsablePathData() || crossLayerRequest;

    /// <summary>中文含义：查询。使用位置：PathAgent.AdvanceToVisibleWaypoint()、PathAgent.CancelPath()、PathAgent.isStopped.set 等共 13 处。</summary>
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

    /// <summary>中文含义：待处理路径版本。使用位置：PathAgent.ApplyPendingPath()、PathAgent.ClearPathData()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。</summary>
    private int pendingPathVersion;
    /// <summary>中文含义：当前路径点。使用位置：作为 List&lt;Vector3&gt;.this[int] 的参数、作为 Mathf.Min(int, int) 的参数、PathAgent.AdvanceToVisibleWaypoint() 等共 17 处。</summary>
    private int currentWaypoint;
    /// <summary>中文含义：请求版本。使用位置：PathAgent.ApplyPendingPath()、PathAgent.CancelPath()、PathAgent.ForceRepath() 等共 11 处。</summary>
    private int requestVersion;
    /// <summary>中文含义：上一次重新寻路时间。使用位置：PathAgent.ForceRepath()、PathAgent.Start()、PathAgent.StartStaticObstacleRepath() 等共 5 处。</summary>
    private float lastRepathTime;
    /// <summary>中文含义：下一次重试时间。使用位置：PathAgent.ForceRepath()、PathAgent.MarkPathFailed(bool)、PathAgent.StartStaticObstacleRepath() 等共 5 处。</summary>
    private float nextRetryTime;
    /// <summary>中文含义：路径请求原点。使用位置：作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.TryCommitPathfinding()。</summary>
    private Vector3 pathRequestOrigin;
    /// <summary>中文含义：静态障碍危险区集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float) 的参数、PathAgent.CancelPath()、PathAgent.IsAvoidingStaticObstacle.get 等共 7 处。</summary>
    private readonly List<StaticObstacleHazard> staticObstacleHazards = new List<StaticObstacleHazard>();
    /// <summary>中文含义：下一次静态障碍重新寻路时间。使用位置：PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)、PathAgent.TryStartQueuedStaticObstacleRepath()。</summary>
    private float nextStaticObstacleRepathTime;
    /// <summary>中文含义：是否存在待处理的静态障碍重新寻路。使用位置：PathAgent.CancelPath()、PathAgent.isStopped.set、PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 等共 6 处。</summary>
    private bool pendingStaticObstacleRepath;
    /// <summary>中文含义：是否缓存了待执行的静态障碍重新寻路。使用位置：PathAgent.CancelPath()、PathAgent.isStopped.set、PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 等共 7 处。</summary>
    private bool queuedStaticObstacleRepath;
    /// <summary>中文含义：追击分辨率缓存是否已初始化。使用位置：PathAgent.InvalidatePursuitResolutionCache()、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private bool pursuitResolutionCacheInitialized;
    /// <summary>中文含义：是否缓存追击分辨率已成功。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private bool cachedPursuitResolutionSucceeded;
    /// <summary>中文含义：缓存原始追击目标。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private Vector3 cachedRawPursuitTarget;
    /// <summary>中文含义：缓存解算后的追击目标。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private Vector3 cachedResolvedPursuitTarget;
    /// <summary>中文含义：缓存追击原点区域。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private uint cachedPursuitOriginArea;
    /// <summary>中文含义：下一次追击解算时间。使用位置：PathAgent.InvalidatePursuitResolutionCache()、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</summary>
    private float nextPursuitResolveTime;
    /// <summary>中文含义：下一次路径点观察前方时间。使用位置：PathAgent.AdvanceToVisibleWaypoint()、PathAgent.Start()。</summary>
    private float nextWaypointLookAheadTime;
    /// <summary>中文含义：追击分辨率失败开始时间。使用位置：作为 float.IsNegativeInfinity(float) 的参数、PathAgent.ClearPursuitResolutionFailure()、PathAgent.MarkPursuitResolutionFailed() 等共 4 处。</summary>
    private float pursuitResolutionFailureStartedTime = float.NegativeInfinity;
    /// <summary>中文含义：当前路径状态。使用位置：作为 StringBuilder.Append(string) 的参数、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、EnemyPerceptionModule.UpdateInvestigation() 等共 30 处。</summary>
    public PathAgentState CurrentPathState { get; private set; } = PathAgentState.Idle;

    /// <summary>中文含义：当前路径点索引。使用位置：VillagerController.CollectCombatTimelineSnapshot()。</summary>
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

    /// <summary>中文含义：路径请求版本。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    public int PathRequestVersion => requestVersion;
    /// <summary>中文含义：是否具有路径。使用位置：作为 StringBuilder.Append(string) 的参数、PathAgent.desiredDirection.get、PathAgent.remainingDistance.get 等共 7 处。</summary>
    public bool hasPath => CurrentPathState == PathAgentState.Valid && path != null && currentWaypoint < path.Count;
    /// <summary>中文含义：是否具有目的地。使用位置：作为 StringBuilder.Append(string) 的参数、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 等共 5 处。</summary>
    public bool HasDestination => hasDestination;
    /// <summary>中文含义：目的地。使用位置：作为 DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3) 的参数、作为 EnemyAIManager.IsStableSurroundArrival(VillagerController, Vector3, Vector3, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数 等共 6 处。</summary>
    public Vector3 Destination => destination;
    /// <summary>中文含义：是否避让中静态障碍。</summary>
    public bool IsAvoidingStaticObstacle => staticObstacleHazards.Count > 0;
    /// <summary>中文含义：是否具有追击分辨率失败。使用位置：PathAgent.ClearPursuitResolutionFailure()、PathAgent.MarkPursuitResolutionFailed()、PathAgent.PursuitResolutionFailureDuration.get 等共 4 处。</summary>
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
    /// <param name="waypoint">中文含义：路径点。输出参数，由本入口写入并交给调用方。使用位置：PathAgent.TryGetCurrentWaypoint(out Vector3)。</param>
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

    /// <summary>中文含义：是否已停止。使用位置：作为 EnemyActionCoordinator.DiagLog(string, string) 的参数、作为 StringBuilder.Append(string) 的参数、EnemyActionCoordinator.BeginAlert() 等共 35 处。</summary>
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
                // receiverValue4（判空前缓存的查询）：从查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PathAgent.isStopped.set。
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

    /// <summary>中文含义：剩余距离。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.UpdateChaseProgressRecovery()。</summary>
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

    /// <summary>中文含义：期望速度。使用位置：作为 StringBuilder.Append(string) 的参数。</summary>
    public Vector3 desiredVelocity
    {
        get
        {
            return desiredDirection * speed;
        }
    }

    /// <summary>中文含义：期望方向。使用位置：作为 StringBuilder.Append(string) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3) 的参数 等共 17 处。</summary>
    public Vector3 desiredDirection
    {
        get
        {
            if (stopped || !hasPath)
            {
                return Vector3.zero;
            }

            // direction（方向）：使用位置：PathAgent.desiredDirection.get。
Vector3 direction = path[currentWaypoint] - transform.position;
            direction.y = 0f;
            // verticalContinuationRadius（竖直衔接半径）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.desiredDirection.get。
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

    /// <summary>中文含义：速度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 StringBuilder.Append(string) 的参数、作为 VillagerController.ResolveStandingRootMotionSpeed(float) 的参数 等共 6 处。</summary>
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

            // nearest（最近）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、PathAgent.isOnNavMesh.get。
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
    /// <param name="traversal">中文含义：通行。使用位置：PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])。</param>
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

        // funnel（漏斗）：保存 Component.GetComponent<FunnelModifier>() 的返回结果，供当前步骤使用。使用位置：PathAgent.Start()。
FunnelModifier funnel = GetComponent<FunnelModifier>();
        if (funnel != null)
        {
            funnel.splitAtEveryPortal = true;
        }

        lastRepathTime = float.NegativeInfinity;
        requestPool = new RequestContext[4];
        // sequence（序号）：使用位置：PathAgent.Start()。
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
        // currentQuery（当前查询）：使用位置：PathAgent.Update()。
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
    /// <param name="target">中文含义：目标。使用位置：作为 PathAgent.TryResolveReachableDestination(Vector3, out Vector3) 的参数。</param>
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
    /// <param name="target">中文含义：目标。使用位置：作为 PathAgent.TryResolvePursuitDestination(Vector3, out Vector3) 的参数。</param>
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
    /// <param name="resolvedTarget">中文含义：解算后的目标。使用位置：作为 Mathf.Abs(float) 的参数、PathAgent.SetResolvedDestination(Vector3)。</param>
    private bool SetResolvedDestination(Vector3 resolvedTarget)
    {
        // threshold（阈值）：使用位置：PathAgent.SetResolvedDestination(Vector3)。
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
        // receiverValue7（判空前缓存的查询）：从查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PathAgent.RejectUnreachableDestination()。
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
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3) 的参数。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：作为 PathAgent.TryResolveDestination(Vector3, float, float, out Vector3) 的参数。</param>
    public bool TryResolveReachableDestination(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        return TryResolveDestination(rawTarget, destinationSnapDistance, destinationSnapHeight, out resolvedTarget);
    }

    /// <summary>TryResolvePursuitDestination 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.SetPursuitDestination(Vector3)、VillagerController.TryResolvePursuitNavigationPoint(Vector3, out Vector3)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的参数、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：作为 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的参数、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。</param>
    public bool TryResolvePursuitDestination(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return false;
        }

        // originInfo（原点信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、作为 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的参数 等共 4 处。
NNInfo originInfo = AstarPath.active.GetNearest(transform.position, NearestNodeConstraint.Walkable);
        if (originInfo.node == null || HorizontalDistance(transform.position, originInfo.position) > graphSnapDistance || Mathf.Abs(transform.position.y - ((Vector3)originInfo.position).y) > Mathf.Max(0.1f, graphSnapHeight))
        {
            return false;
        }

        // imageVersion（图像版本）：使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。
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

        // moveThreshold（移动阈值）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。
float moveThreshold = Mathf.Max(0.05f, pursuitResolveMoveThreshold);
        // sameCachedRequest（是否相同缓存请求）：使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。
bool sameCachedRequest = pursuitResolutionCacheInitialized && cachedPursuitOriginArea == originInfo.node.Area && (rawTarget - cachedRawPursuitTarget).sqrMagnitude < moveThreshold * moveThreshold;
        if (sameCachedRequest && Time.time < nextPursuitResolveTime)
        {
            resolvedTarget = cachedResolvedPursuitTarget;
            return cachedPursuitResolutionSucceeded;
        }

        // succeeded（是否已成功）：保存 PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3) 的返回结果，供当前步骤使用。使用位置：PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。
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
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3) 的参数、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)。</param>
    /// <param name="originInfo">中文含义：原点信息。使用位置：作为 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3) 的参数。</param>
    /// <param name="maximumSnapDistance">中文含义：最大吸附距离。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="maximumSnapHeight">中文含义：最大吸附高度。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：作为 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3) 的参数、PathAgent.TryResolveDestinationOnTargetLevel(Vector3, NNInfo, float, float, out Vector3)。</param>
    private bool TryResolveDestinationOnTargetLevel(Vector3 rawTarget, NNInfo originInfo, float maximumSnapDistance, float maximumSnapHeight, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (Query == null || !Query.CanQuery)
        {
            return false;
        }

        // tolerance（容差）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、作为 NavigationQueryService.Projection(Vector3, uint, float, float, float, float, out Vector3) 的参数。
float tolerance = Mathf.Min(Mathf.Max(.1f, pursuitTargetLevelTolerance), Mathf.Max(.1f, maximumSnapHeight));
        return Query.Projection(rawTarget, originInfo.node.Area, Mathf.Max(.1f, maximumSnapDistance), tolerance, Mathf.Max(tolerance, pursuitGroundFallbackMaximumDrop), maxOrdinaryPathY, out resolvedTarget);
    }

    /// <summary>TryResolveDestination 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.TryResolveReachableDestination(Vector3, out Vector3)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、PathAgent.HorizontalDistance(Vector3, Vector3)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、作为 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的参数 等共 4 处。</param>
    /// <param name="maximumSnapDistance">中文含义：最大吸附距离。使用位置：作为 Mathf.Max(float, float) 的参数、作为 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的参数。</param>
    /// <param name="maximumSnapHeight">中文含义：最大吸附高度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的参数。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)。</param>
    private bool TryResolveDestination(Vector3 rawTarget, float maximumSnapDistance, float maximumSnapHeight, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return false;
        }

        // originInfo（原点信息）：保存 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)。
NNInfo originInfo = ReadNormalNavigationPoint(transform.position, graphSnapDistance, graphSnapHeight);
        // targetInfo（目标信息）：保存 PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)。
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

        // snapDistance（吸附距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.TryResolveDestination(Vector3, float, float, out Vector3)。
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
    /// <param name="position">中文含义：位置。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、作为 HouseThreeRegionNavigation.TreadRegion(Vector3) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    /// <param name="heightDifference">中文含义：高度差值。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    private NNInfo ReadNormalNavigationPoint(Vector3 position, float distance, float heightDifference)
    {
        // region（区域）：使用位置：PathAgent.ReadNormalNavigationPoint(Vector3, float, float)。
var region = HouseThreeRegionNavigation.Current;
        if (region != null)
        {
            // regionValue（区域值）：保存 HouseThreeRegionNavigation.TreadRegion(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)。
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
    /// <param name="hit">中文含义：命中。使用位置：作为 PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float) 的参数、作为 PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float) 的参数、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。</param>
    /// <param name="bodyRadius">中文含义：身体半径。使用位置：作为 Mathf.Max(float, float) 的参数、作为 PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float) 的参数。</param>
    public bool RegisterStaticObstacleHazard(RaycastHit hit, float bodyRadius)
    {
        if (!IsValidatedStaticObstacleHit(hit, bodyRadius))
        {
            return false;
        }

        PruneExpiredStaticObstacleHazards();
        // radius（半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float) 的参数。
float radius = Mathf.Max(0.5f, bodyRadius + Mathf.Max(0.1f, staticObstacleHazardPadding));
        // expiryTime（有效期限时间）：使用位置：作为 PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float) 的参数。
float expiryTime = Time.time + Mathf.Max(0.1f, staticObstacleHazardLifetime);
        // existingIndex（已有索引）：使用位置：作为 List<PathAgent.StaticObstacleHazard>.this[int] 的参数、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
int existingIndex = -1;
        // index（当前元素索引）：使用位置：作为 List<PathAgent.StaticObstacleHazard>.this[int] 的参数、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
for (int index = 0; index < staticObstacleHazards.Count; index++)
        {
            // existing（已有）：使用位置：作为 Mathf.Min(float, float) 的参数、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
StaticObstacleHazard existing = staticObstacleHazards[index];
            // offset（偏移量）：使用位置：PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
Vector3 offset = existing.center - hit.point;
            offset.y = 0f;
            if (offset.sqrMagnitude <= Mathf.Min(existing.radius, radius) * Mathf.Min(existing.radius, radius))
            {
                existingIndex = index;
                break;
            }
        }

        // hazard（危险区）：使用位置：作为 List<PathAgent.StaticObstacleHazard>.Add(PathAgent.StaticObstacleHazard) 的参数、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
StaticObstacleHazard hazard = new StaticObstacleHazard(hit.point, radius, expiryTime);
        // isNewHazard（是否新危险区）：使用位置：PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
bool isNewHazard = existingIndex < 0;
        if (!isNewHazard)
        {
            staticObstacleHazards[existingIndex] = hazard;
        }
        else
        {
            // maximumCount（最大数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)。
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
        // receiverValue9（判空前缓存的查询）：从查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PathAgent.CancelPath()。
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
    /// <param name="hit">中文含义：命中。使用位置：PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。</param>
    /// <param name="bodyRadius">中文含义：身体半径。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private bool IsValidatedStaticObstacleHit(RaycastHit hit, float bodyRadius)
    {
        if (hit.collider == null)
        {
            return false;
        }

        // minimumDistance（最小距离）：使用位置：PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。
float minimumDistance = 0.005f;
        if (hit.distance <= minimumDistance)
        {
            return false;
        }

        // point（点）：使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数、PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。
Vector3 point = hit.point;
        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
        {
            return false;
        }

        // offset（偏移量）：使用位置：PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。
Vector3 offset = point - transform.position;
        offset.y = 0f;
        // maximumDistance（最大距离）：使用位置：PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。
float maximumDistance = Mathf.Max(0.5f, bodyRadius) + hit.distance + 0.35f;
        if (offset.sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        if (point.sqrMagnitude <= 0.000001f && transform.position.sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        // normal（表面法线）：使用位置：PathAgent.IsValidatedStaticObstacleHit(RaycastHit, float)。
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

        if (!crossLayerRequest && Time.time - lastRepathTime < Mathf.Max(.05f, repathRate))
        {
            return false;
        }

        if (!crossLayerRequest && Time.time < nextRetryTime)
        {
            return false;
        }

        // context（上下文）：使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、PathAgent.TryCommitPathfinding()。
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
        // request（请求）：保存 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的返回结果，供当前步骤使用。使用位置：作为 Seeker.StartPath(Path) 的参数、PathAgent.TryCommitPathfinding()。
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
        /// <summary>中文含义：请求。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PathAgent.TryCommitPathfinding()。</summary>
        internal Path request;
        /// <summary>中文含义：路径标识符。使用位置：PathAgent.RequestContext.Complete(Path)、PathAgent.TryCommitPathfinding()。</summary>
        internal ushort pathIdentifier;
        /// <summary>中文含义：version 表示“版本”；imageVersion 表示“图像版本”。使用位置：PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.TryCommitPathfinding()。；使用位置：PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.TryCommitPathfinding()。</summary>
        internal int version, imageVersion;
        /// <summary>中文含义：图像服务。使用位置：PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.TryCommitPathfinding()。</summary>
        internal NavigationQueryService imageService;
        /// <summary>中文含义：startPoint 表示“起始点”；endPoint 表示“结束点”。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、作为 PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 等共 4 处。；使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.TryCommitPathfinding()。</summary>
        internal Vector3 startPoint, endPoint;
        /// <summary>中文含义：回调。只读字段，初始化后不由普通写入口替换。使用位置：作为 ABPath.Construct(Vector3, Vector3, OnPathDelegate) 的参数、PathAgent.RequestContext.RequestContext(PathAgent)。</summary>
        internal readonly OnPathDelegate callback;
        /// <summary>中文含义：避开。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.TryCommitPathfinding()。</summary>
        internal readonly StaticObstacleTraversalProvider avoid = new();
        /// <summary>中文含义：所属寻路代理。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.RequestContext.Complete(Path)、PathAgent.RequestContext.RequestContext(PathAgent)。</summary>
        private readonly PathAgent host;
        /// <summary>构造并初始化 RequestContext 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="host">中文含义：所属寻路代理。使用位置：PathAgent.RequestContext.RequestContext(PathAgent)。</param>
        internal RequestContext(PathAgent host)
        {
            this.host = host;
            callback = Complete;
        }

        /// <summary>Complete 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="result">中文含义：当前路径。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 PathAgent.OnPathComplete(Path, PathAgent.RequestContext) 的参数、PathAgent.RequestContext.Complete(Path)。</param>
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

        var target = AstarPath.active.GetNearest(destination, NearestNodeConstraint.Walkable);
        if (TryFindConnectedPathOrigin(target, out Vector3 entry))
        {
            return entry;
        }

        // 保留原有重试入口。角色仍须通过实际运动检查才能沿路走过去。
        var nearest = AstarPath.active.GetNearest(transform.position, NearestNodeConstraint.Walkable);
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
        // currentPosition（当前位置）：使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 GraphNode.ClosestPointOnNode(Vector3) 的参数、作为 Mathf.Abs(float) 的参数 等共 5 处。
Vector3 currentPosition = transform.position;
        origin = currentPosition;
        if (movementController == null || AstarPath.active == null || targetInfo.node == null)
        {
            return false;
        }

        // nearestInfo（最近信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 PathAgent.LocatedInStaticHazardZone(Vector3) 的参数、作为 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool) 的参数、PathAgent.ResolvePhysicallyReachablePathOrigin()。
NNInfo nearestInfo = AstarPath.active.GetNearest(currentPosition, NearestNodeConstraint.Walkable);
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

        // maximumDistance（最大距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 NavigationQueryService.RecoverCollection(Vector3, float, List<GraphNode>) 的参数、PathAgent.ResolvePhysicallyReachablePathOrigin()。
float maximumDistance = Mathf.Max(0.5f, graphSnapDistance);
        // bestDistanceSqr（最佳距离平方）：使用位置：PathAgent.ResolvePhysicallyReachablePathOrigin()。
float bestDistanceSqr = maximumDistance * maximumDistance;
        // bestPoint（最佳点）：使用位置：PathAgent.ResolvePhysicallyReachablePathOrigin()。
Vector3 bestPoint = currentPosition;
        // found（是否已找到）：使用位置：PathAgent.ResolvePhysicallyReachablePathOrigin()。
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
            // point（点）：保存 GraphNode.ClosestPointOnNode(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.LocatedInStaticHazardZone(Vector3) 的参数、作为 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool) 的参数 等共 4 处。
Vector3 point = node.ClosestPointOnNode(currentPosition);
            if (LocatedInStaticHazardZone(point))
            {
                continue;
            }

            if (Mathf.Abs(point.y - currentPosition.y) > Mathf.Max(.1f, graphSnapHeight))
            {
                continue;
            }

            // distanceSqr（距离平方）：使用位置：PathAgent.ResolvePhysicallyReachablePathOrigin()。
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
    /// <param name="position">中文含义：位置。使用位置：PathAgent.LocatedInStaticHazardZone(Vector3)。</param>
    private bool LocatedInStaticHazardZone(Vector3 position)
    {
        // obstacle（障碍）：每轮从静态障碍危险区集合取得一个元素，仅代表当前这一项。使用位置：PathAgent.LocatedInStaticHazardZone(Vector3)。
foreach (var obstacle in staticObstacleHazards)
        {
            // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、PathAgent.LocatedInStaticHazardZone(Vector3)。
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
    /// <param name="result">中文含义：当前路径。使用位置：作为 List&lt;Vector3&gt;.AddRange(IEnumerable&lt;Vector3&gt;) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。</param>
    /// <param name="context">中文含义：上下文。使用位置：作为 PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。</param>
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
            // capacity（容量）：使用位置：作为 Mathf.NextPowerOfTwo(int) 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。
int capacity = result.vectorPath.Count + 1;
            if (pendingPath.Capacity < capacity)
            {
                pendingPath.Capacity = Mathf.NextPowerOfTwo(capacity);
                CombatPerformanceCounters.pathExpandCapacity++;
            }

            pendingPath.AddRange(result.vectorPath);
            // endPoint（结束点）：使用位置：作为 List<Vector3>.Add(Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数 等共 4 处。
Vector3 endPoint = context.endPoint;
            if (HorizontalDistance(pendingPath[pendingPath.Count - 1], endPoint) > .05f || Mathf.Abs(pendingPath[pendingPath.Count - 1].y - endPoint.y) > .05f)
            {
                pendingPath.Add(endPoint);
            }

            // region（区域）：使用位置：PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。
var region = HouseThreeRegionNavigation.Current;
            if (region != null)
            {
                // sequence（序号）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)。
for (int sequence = 0; sequence < pendingPath.Count; sequence++)
                {
                    // waypoint（路径点）：使用位置：作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
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

        // previousBuffer（前一次缓冲区）：使用位置：PathAgent.ApplyPendingPath()。
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
            // capacity（容量）：保存 Mathf.NextPowerOfTwo(int) 的返回结果，供当前步骤使用。使用位置：PathAgent.CachePathProperty()。
int capacity = Mathf.NextPowerOfTwo(path.Count);
            preciseWaypoint = new bool[capacity];
            subsequentPreciseWaypoint = new bool[capacity + 1];
            remainingSegmentLong = new float[capacity];
            CombatPerformanceCounters.pathExpandCapacity++;
        }

        subsequentPreciseWaypoint[path.Count] = false;
        // sequence（序号）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.CachePathProperty()。
for (int sequence = path.Count - 1; sequence >= 0; sequence--)
        {
            preciseWaypoint[sequence] = IsNavigationLinkEndpoint(path[sequence]);
            subsequentPreciseWaypoint[sequence] = preciseWaypoint[sequence] || subsequentPreciseWaypoint[sequence + 1];
            // assignmentReceiver12（赋值前保存的目标剩余线段长型）：从剩余线段长型读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PathAgent.CachePathProperty()。
float[] assignmentReceiver12 = remainingSegmentLong;
            // assignmentIndex13（赋值前保存的索引序号）：从序号读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：PathAgent.CachePathProperty()。
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
    /// <param name="completedPath">中文含义：已完成路径。使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)。</param>
    private int ResolvePendingPathHandoffWaypoint(List<Vector3> completedPath)
    {
        if (completedPath == null || completedPath.Count <= 1)
        {
            return 0;
        }

        // 交接点必须与角色当前可抵达的高度层一致。此前这里允许的
        // 高度差高于拐点抵达判定，重算路径会把角色交给一个悬在头顶的
        // 拐点；角色只能围着该点横向绕行，随后又因停滞再次重算。
        // allowedVerticalDifference（允许的竖直差值）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float allowedVerticalDifference = Mathf.Max(0.05f, waypointVerticalReachedTolerance);
        // handoffDistance（交接距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float handoffDistance = Mathf.Max(1.1f, HorizontalDistance(transform.position, pathRequestOrigin) + waypointReachedRadius * 1.5f);
        // maximumDistanceSqr（最大距离平方）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float maximumDistanceSqr = handoffDistance * handoffDistance;
        // bestFollowingWaypoint（最佳跟随中路径点）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
int bestFollowingWaypoint = 0;
        // bestDistanceSqr（最佳距离平方）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float bestDistanceSqr = float.PositiveInfinity;
        // currentPosition（当前位置）：使用位置：作为 Mathf.Abs(float) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 currentPosition = transform.position;
        // containsPreciseLink（是否包含精确连接）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
bool containsPreciseLink = subsequentPreciseWaypoint[0];
        // index（当前元素索引）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
for (int index = 0; index < completedPath.Count - 1; index++)
        {
            // start（起始）：使用位置：作为 Mathf.Abs(float) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 start = completedPath[index];
            // end（结束）：使用位置：作为 Mathf.Abs(float) 的参数、作为 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool) 的参数、作为 VillagerController.HasContinuousSteppedNavigationGround(Vector3) 的参数 等共 4 处。
Vector3 end = completedPath[index + 1];
            if (Mathf.Abs(currentPosition.y - start.y) > allowedVerticalDifference || Mathf.Abs(currentPosition.y - end.y) > allowedVerticalDifference)
            {
                continue;
            }

            // horizontalStart（水平起始）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 horizontalStart = start;
            // horizontalEnd（水平结束）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 horizontalEnd = end;
            // horizontalCurrent（水平当前）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 horizontalCurrent = currentPosition;
            horizontalStart.y = 0f;
            horizontalEnd.y = 0f;
            horizontalCurrent.y = 0f;
            // segment（线段）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 segment = horizontalEnd - horizontalStart;
            // segmentLengthSqr（线段长度平方）：使用位置：作为 Mathf.Clamp01(float) 的参数、PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.0001f)
            {
                continue;
            }

            // segmentProgress（线段进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
float segmentProgress = Mathf.Clamp01(Vector3.Dot(horizontalCurrent - horizontalStart, segment) / segmentLengthSqr);
            // closestPoint（最近点）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
Vector3 closestPoint = horizontalStart + segment * segmentProgress;
            // distanceSqr（距离平方）：使用位置：PathAgent.ResolvePendingPathHandoffWaypoint(List<Vector3>)。
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
        else
        {
            return 0;
        }
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

        // tailPoint（尾部点）：使用位置：作为 Mathf.Min(int, int) 的参数。
int tailPoint = path.Count - 1;
        // candidatePoint（候选点）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：PathAgent.AdvanceToVisibleWaypoint()。
int candidatePoint = Mathf.Min(tailPoint, currentWaypoint + Mathf.Clamp(maxWaypointLookAhead, 1, 12));
        // tryValue（尝试值）：使用位置：PathAgent.AdvanceToVisibleWaypoint()。
int tryValue = 0;
        // 一次最多两个候选且全局两段；预算不足只保留原路径，绝不放宽地面检测。
        // point（点）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.AdvanceToVisibleWaypoint()。
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
    /// <param name="waypoint">中文含义：路径点。使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数。</param>
    private bool IsWaypointReached(Vector3 waypoint)
    {
        // radius（半径）：使用位置：PathAgent.IsWaypointReached(Vector3)。
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
    /// <summary>中文含义：是否需要精确路径跟随中。使用位置：PathAgent.AdvanceToVisibleWaypoint()、VillagerController.ResolveCrowdOverlap()、VillagerController.ResolveStandingPathDirectionForCurrentFrame() 等共 5 处。</summary>
    public bool RequiresPrecisePathFollowing => path != null && currentWaypoint < path.Count && (preciseWaypoint[currentWaypoint] || (currentWaypoint > 0 && preciseWaypoint[currentWaypoint - 1]));

    /// <summary>IsNavigationLinkEndpoint 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 PathAgent.CachePathProperty()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waypoint">中文含义：路径点。使用位置：PathAgent.IsNavigationLinkEndpoint(Vector3)。</param>
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

            // link（连接）：使用位置：PathAgent.IsNavigationLinkEndpoint(Vector3)。
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

        // waypoint（路径点）：使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、作为 VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3) 的参数。
Vector3 waypoint = path[currentWaypoint];
        if (HorizontalDistance(transform.position, waypoint) > Mathf.Max(0.05f, waypointReachedRadius) || Mathf.Abs(transform.position.y - waypoint.y) <= Mathf.Max(0.05f, waypointVerticalReachedTolerance))
        {
            return false;
        }

        return movementController.CanAdvancePastCompressedSteppedWaypoint(waypoint, path[currentWaypoint + 1]);
    }

    /// <summary>MarkPathFailed 的业务处理入口；本方法直接依赖 PathAgent.ClearPathData()、PathAgent.HasUsablePathData()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PathAgent.MarkPursuitResolutionFailed()、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、PathAgent.RejectUnreachableDestination()、PathAgent.RequestPath()；下游（含内部回调体） PathAgent.ClearPathData()、PathAgent.HasUsablePathData()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveCurrentPath">中文含义：是否保留当前路径。使用位置：PathAgent.MarkPathFailed(bool)。</param>
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
    /// <param name="points">中文含义：点集合。使用位置：PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3)。</param>
    /// <param name="startPoint">中文含义：起始点。使用位置：作为 Mathf.Max(float, float) 的参数、PathAgent.IsPathHeightValid(List&lt;Vector3&gt;, Vector3, Vector3)。</param>
    /// <param name="endPoint">中文含义：结束点。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private bool IsPathHeightValid(List<Vector3> points, Vector3 startPoint, Vector3 endPoint)
    {
        if (points == null || points.Count == 0)
        {
            return false;
        }

        // endpointCeiling（端点天花板）：使用位置：PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
float endpointCeiling = Mathf.Max(startPoint.y, endPoint.y) + Mathf.Max(0.1f, maxPathHeightAboveEndpoints);
        // i（当前遍历索引）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
for (int i = 0; i < points.Count; i++)
        {
            // point（点）：使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
Vector3 point = points[i];
            if (point.y > endpointCeiling)
            {
                return false;
            }

            // 漏掉起点到第一拐点的校验，会让漏斗裁剪后的首个高点绕过
            // 高差和坡度限制。它正是角色在平地追击时反复对准高处节点
            // 的来源之一。
            // previous（前一次）：使用位置：作为 Mathf.Abs(float) 的参数、作为 PathAgent.HorizontalDistance(Vector3, Vector3) 的参数、PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
Vector3 previous;
            if (i == 0)
            {
                previous = startPoint;
            }
            else
            {
                previous = points[i - 1];
            }

            // verticalDistance（竖直距离）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Atan2(float, float) 的参数、PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
float verticalDistance = Mathf.Abs(point.y - previous.y);
            // horizontalDistance（水平距离）：保存 PathAgent.HorizontalDistance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
float horizontalDistance = HorizontalDistance(previous, point);
            // slope（坡度）：使用位置：PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
float slope = Mathf.Atan2(verticalDistance, Mathf.Max(0.001f, horizontalDistance)) * Mathf.Rad2Deg;
            // maximumVerticalStep（最大竖直步进）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PathAgent.IsPathHeightValid(List<Vector3>, Vector3, Vector3)。
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
        // now（当前时间）：使用位置：PathAgent.PruneExpiredStaticObstacleHazards()。
float now = Time.time;
        // index（当前元素索引）：使用位置：作为 List<PathAgent.StaticObstacleHazard>.RemoveAt(int) 的参数、作为 List<PathAgent.StaticObstacleHazard>.this[int] 的参数、PathAgent.PruneExpiredStaticObstacleHazards()。
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
        /// <summary>中文含义：中心。使用位置：作为 PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float) 的参数、PathAgent.LocatedInStaticHazardZone(Vector3)、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float) 等共 4 处。</summary>
        public Vector3 center;
        /// <summary>中文含义：半径。使用位置：作为 Mathf.Min(float, float) 的参数、作为 PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float) 的参数、PathAgent.LocatedInStaticHazardZone(Vector3) 等共 4 处。</summary>
        public float radius;
        /// <summary>中文含义：有效期限时间。使用位置：PathAgent.PruneExpiredStaticObstacleHazards()、PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float)。</summary>
        public float expiryTime;
        /// <summary>构造并初始化 StaticObstacleHazard 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="center">中文含义：中心。使用位置：PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float)。</param>
        /// <param name="radius">中文含义：半径。使用位置：PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float)。</param>
        /// <param name="expiryTime">中文含义：有效期限时间。使用位置：PathAgent.StaticObstacleHazard.StaticObstacleHazard(Vector3, float, float)。</param>
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
        /// <summary>中文含义：中心。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)、PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。</summary>
        public readonly Vector3 center;
        /// <summary>中文含义：半径。只读字段，初始化后不由普通写入口替换。使用位置：PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)、PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。</summary>
        public readonly float radius;
        /// <summary>构造并初始化 StaticObstacleHazardSnapshot 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="center">中文含义：中心。使用位置：PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)。</param>
        /// <param name="radius">中文含义：半径。使用位置：PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)。</param>
        public StaticObstacleHazardSnapshot(Vector3 center, float radius)
        {
            this.center = center;
            this.radius = radius;
        }
    }

    /// <summary>敌人行为、动作与通行模块 StaticObstacleTraversalProvider；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class StaticObstacleTraversalProvider : ITraversalProvider
    {
        /// <summary>中文含义：危险区集合。使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)。</summary>
        private StaticObstacleHazardSnapshot[] hazards = new StaticObstacleHazardSnapshot[3];
        /// <summary>中文含义：障碍数量。使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)。</summary>
        private int obstacleCount;
        /// <summary>中文含义：代价倍率。使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)。</summary>
        private float costMultiplier;
        /// <summary>Set 的业务处理入口；本方法直接依赖 Mathf.NextPowerOfTwo(int)、PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PathAgent.TryCommitPathfinding()；下游（含内部回调体） Mathf.NextPowerOfTwo(int)、PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="obstacle">中文含义：障碍。使用位置：作为 Mathf.NextPowerOfTwo(int) 的参数、作为 PathAgent.StaticObstacleHazardSnapshot.StaticObstacleHazardSnapshot(Vector3, float) 的参数、PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)。</param>
        /// <param name="multiplier">中文含义：倍率。使用位置：PathAgent.StaticObstacleTraversalProvider.Set(List&lt;PathAgent.StaticObstacleHazard&gt;, float)。</param>
        internal void Set(List<StaticObstacleHazard> obstacle, float multiplier)
        {
            if (hazards.Length < obstacle.Count)
            {
                hazards = new StaticObstacleHazardSnapshot[Mathf.NextPowerOfTwo(obstacle.Count)];
                CombatPerformanceCounters.pathExpandCapacity++;
            }

            obstacleCount = obstacle.Count;
            costMultiplier = multiplier;
            // sequence（序号）：使用位置：作为 List<PathAgent.StaticObstacleHazard>.this[int] 的参数、PathAgent.StaticObstacleTraversalProvider.Set(List<PathAgent.StaticObstacleHazard>, float)。
for (int sequence = 0; sequence < obstacleCount; sequence++)
            {
                hazards[sequence] = new StaticObstacleHazardSnapshot(obstacle[sequence].center, obstacle[sequence].radius);
            }
        }

        /// <summary>CanTraverse 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode, GraphNode)；下游（含内部回调体） DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalConstraint">中文含义：通行约束。按引用传入，读写会作用于调用方原存储位置。使用位置：作为 DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode) 的参数。</param>
        /// <param name="node">中文含义：节点。使用位置：作为 DefaultITraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode) 的参数。</param>
        public bool CanTraverse(ref TraversalConstraint traversalConstraint, GraphNode node)
        {
            return DefaultITraversalProvider.CanTraverse(ref traversalConstraint, node);
        }

        /// <summary>CanTraverse 的业务处理入口；本方法直接依赖 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalConstraint">中文含义：通行约束。按引用传入，读写会作用于调用方原存储位置。使用位置：作为 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode) 的参数。</param>
        /// <param name="from">中文含义：来源。</param>
        /// <param name="to">中文含义：目标。使用位置：作为 PathAgent.StaticObstacleTraversalProvider.CanTraverse(ref TraversalConstraint, GraphNode) 的参数。</param>
        public bool CanTraverse(ref TraversalConstraint traversalConstraint, GraphNode from, GraphNode to)
        {
            return CanTraverse(ref traversalConstraint, to);
        }

        /// <summary>GetConnectionCost 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalCosts">中文含义：通行代价集合。按引用传入，读写会作用于调用方原存储位置。使用位置：作为 DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode) 的参数。</param>
        /// <param name="from">中文含义：来源。使用位置：作为 DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode) 的参数。</param>
        /// <param name="to">中文含义：目标。使用位置：作为 DefaultITraversalProvider.GetConnectionCost(ref TraversalCosts, GraphNode, GraphNode) 的参数。</param>
        public uint GetConnectionCost(ref TraversalCosts traversalCosts, GraphNode from, GraphNode to)
        {
            return DefaultITraversalProvider.GetConnectionCost(ref traversalCosts, from, to);
        }

        /// <summary>GetTraversalCostMultiplier 的业务处理入口；本方法直接依赖 DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="traversalCosts">中文含义：通行代价集合。按引用传入，读写会作用于调用方原存储位置。使用位置：作为 DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode) 的参数。</param>
        /// <param name="node">中文含义：节点。使用位置：作为 DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode) 的参数、PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。</param>
        public float GetTraversalCostMultiplier(ref TraversalCosts traversalCosts, GraphNode node)
        {
            // baseMultiplier（基础倍率）：保存 DefaultITraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode) 的返回结果，供当前步骤使用。使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。
float baseMultiplier = DefaultITraversalProvider.GetTraversalCostMultiplier(ref traversalCosts, node);
            // point（点）：使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。
Vector3 point = (Vector3)node.position;
            // index（当前元素索引）：使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。
for (int index = 0; index < obstacleCount; index++)
            {
                // offset（偏移量）：使用位置：PathAgent.StaticObstacleTraversalProvider.GetTraversalCostMultiplier(ref TraversalCosts, GraphNode)。
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
    /// <param name="first">中文含义：首个。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、PathAgent.HorizontalDistance(Vector3, Vector3)。</param>
    /// <param name="second">中文含义：第二。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、PathAgent.HorizontalDistance(Vector3, Vector3)。</param>
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
        // previous（前一次）：使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、PathAgent.OnDrawGizmosSelected()。
Vector3 previous = transform.position;
        // i（当前遍历索引）：使用位置：作为 List<Vector3>.this[int] 的参数、PathAgent.OnDrawGizmosSelected()。
for (int i = currentWaypoint; i < path.Count; i++)
        {
            Gizmos.DrawLine(previous, path[i]);
            previous = path[i];
        }
    }
}
