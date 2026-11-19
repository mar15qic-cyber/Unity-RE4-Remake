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
    /// <summary>中文含义：生效路线集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、EnemyWindowTraversalRoute.OnDisable()、EnemyWindowTraversalRoute.OnEnable() 等共 4 处。</summary>
    private static readonly List<EnemyWindowTraversalRoute> activeRoutes = new List<EnemyWindowTraversalRoute>();
    /// <summary>中文含义：窗口点。使用位置：作为 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 的参数、作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan) 等共 14 处。</summary>
    [Header("窗口引用")]
    public WindowTraversalPoint windowPoint;
    /// <summary>中文含义：平台区域编号。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3) 的参数、作为 EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool) 的参数、作为 IEnumerable&lt;EnemyWindowTraversalRoute&gt;.Any&lt;EnemyWindowTraversalRoute&gt;(Func&lt;EnemyWindowTraversalRoute, bool&gt;) 的参数 等共 14 处。</summary>
    [Header("平台侧")]
    public string platformRegionId = "二楼外平台";
    /// <summary>中文含义：窗口本地前方向一侧填一，反方向一侧填负一。使用位置：EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.Start()、EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3) 等共 7 处。</summary>
    [Tooltip("窗口本地前方向一侧填一，反方向一侧填负一。")]
    [Range(-1, 1)]
    public int platformSideSign = 1;
    /// <summary>中文含义：是否允许双方方向集合。使用位置：EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)、StrictAstarGraphBaker.CanPassValidWindowReach(GraphNode, GraphNode)。</summary>
    public bool allowBothDirections = true;
    /// <summary>中文含义：最小平台侧边深度。</summary>
    [Min(0f)]
    public float minimumPlatformSideDepth = 0.15f;
    /// <summary>中文含义：平台级别容差。</summary>
    [Min(0.1f)]
    public float platformLevelTolerance = 0.9f;
    /// <summary>中文含义：捕获距离。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyWindowTraversal.TryBeginPendingWindowTraversal()。</summary>
    [Header("敌人队列")]
    [Min(0.1f)]
    public float captureDistance = 0.48f;
    /// <summary>中文含义：队列间距。使用位置：作为 Mathf.CeilToInt(float) 的参数、作为 Mathf.Max(float, float) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。</summary>
    [Min(0.2f)]
    public float queueSpacing = 0.9f;
    /// <summary>中文含义：通行持续时间。使用位置：作为 Compare(float, EnemyWindowTraversalPlan) 的参数、作为 Mathf.Max(float, float) 的参数、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。</summary>
    [Min(0.1f)]
    public float traversalDuration = 1.07f;
    /// <summary>中文含义：最大来源级别差值。使用位置：作为 EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool) 的参数、EnemyWindowTraversal.TryBeginPendingWindowTraversal()、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan) 等共 4 处。</summary>
    [Min(0.1f)]
    public float maximumSourceLevelDifference = 0.85f;
    /// <summary>中文含义：等待者集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Mathf.Max(int, int) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、EnemyWindowTraversalRoute.CancelWait(VillagerController) 等共 9 处。</summary>
    private readonly List<VillagerController> waiters = new List<VillagerController>();
    /// <summary>中文含义：拥有者。使用位置：作为 List&lt;VillagerController&gt;.Remove(VillagerController) 的参数、EnemyWindowTraversalRoute.CancelWait(VillagerController)、EnemyWindowTraversalRoute.CleanupWaiters() 等共 8 处。</summary>
    private VillagerController owner;
    /// <summary>中文含义：通行登记。只读字段，初始化后不由普通写入口替换。使用位置：EnemyWindowTraversalRoute.CleanupWaiters()、EnemyWindowTraversalRoute.OnDisable()、EnemyWindowTraversalRoute.OnEnable() 等共 8 处。</summary>
    private readonly GameArchitecture.TraversalTokenRegistry traversalRegister = new GameArchitecture.TraversalTokenRegistry();
    /// <summary>中文含义：占用凭证。使用位置：作为 EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason) 的参数、作为 TraversalTokenRegistry.Release(TraversalToken, ReleaseReason) 的参数、EnemyWindowTraversalRoute.CleanupWaiters() 等共 6 处。</summary>
    private GameArchitecture.TraversalToken occupancyToken;
    /// <summary>中文含义：路线视图。只读字段，初始化后不由普通写入口替换。使用位置：EnemyWindowTraversalRoute.ActiveRoutes.get。</summary>
    private static readonly System.Collections.ObjectModel.ReadOnlyCollection<EnemyWindowTraversalRoute> routeView = activeRoutes.AsReadOnly();
    /// <summary>中文含义：生效路线集合。使用位置：EnemyWindowTraversal.InitializePlatformRegionState()、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)、LeonWindowTraversal.InitializeEnemyPlatformRegionState() 等共 4 处。</summary>
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
        owner = null;
    }

    /// <summary>TryBuildPlan 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EnemyWindowTraversal&gt;()、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;EnemyWindowTraversal&gt;()、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、EnemyWindowTraversalRoute.EnsureWindowReference()、EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3) 的参数、作为 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool) 的参数、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="targetPosition">中文含义：目标位置。</param>
    /// <param name="targetOnPlatform">中文含义：是否目标事件平台。使用位置：EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)。</param>
    /// <param name="plan">中文含义：方案。输出参数，由本入口写入并交给调用方。使用位置：作为 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool) 的参数、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)。</param>
    public bool TryBuildPlan(VillagerController enemy, Vector3 targetPosition, bool targetOnPlatform, out EnemyWindowTraversalPlan plan)
    {
        plan = default;
        EnsureWindowReference();
        if (enemy == null || windowPoint == null)
        {
            return false;
        }

        // enemyOnPlatform（是否敌人事件平台）：使用位置：作为 EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool) 的参数、作为 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool) 的参数、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)。
bool enemyOnPlatform;
        // traversal（通行）：保存 Component.GetComponent<EnemyWindowTraversal>() 的返回结果，供当前步骤使用。使用位置：EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)。
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
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Mathf.Abs(float) 的参数、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</param>
    /// <param name="sourceOnPlatform">中文含义：是否来源事件平台。使用位置：EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</param>
    /// <param name="plan">中文含义：方案。输出参数，由本入口写入并交给调用方。使用位置：EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</param>
    /// <param name="planFarEnd">中文含义：是否方案远端结束。使用位置：EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</param>
    public bool TryBuildPlanFromSide(VillagerController enemy, bool sourceOnPlatform, out EnemyWindowTraversalPlan plan, bool planFarEnd = false)
    {
        plan = default;
        EnsureWindowReference();
        if (enemy == null || windowPoint == null || (planFarEnd && !windowPoint.isActiveAndEnabled))
        {
            return false;
        }

        // sourceSideSign（来源侧边符号）：使用位置：作为 WindowTraversalPoint.GetApproachProbePosition(int) 的参数、作为 WindowTraversalPoint.GetLandingProbePosition(int) 的参数、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。
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

        // travelDirection（行进方向）：使用位置：作为 Quaternion.LookRotation(Vector3, Vector3) 的参数、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。
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
    /// <param name="point">中文含义：点。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3) 的参数。</param>
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

    internal bool CanEnterWindowQueue(VillagerController enemy)
    {
        CleanupWaiters();
        return windowPoint != null && !windowPoint.FallingBodyInOpening && (owner == null || owner == enemy)
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
    /// <param name="point">中文含义：点。使用位置：作为 EnemyWindowTraversalRoute.IsPointOnPlatformSide(Vector3) 的参数。</param>
    public static bool IsPointOnAnyPlatformSide(Vector3 point)
    {
        // i（当前遍历索引）：使用位置：作为 List<EnemyWindowTraversalRoute>.this[int] 的参数、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)。
for (int i = 0; i < activeRoutes.Count; i++)
        {
            // route（路线）：使用位置：EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)。
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
    /// <param name="enemy">中文含义：敌人。使用位置：作为 List&lt;VillagerController&gt;.Contains(VillagerController) 的参数、作为 List&lt;VillagerController&gt;.IndexOf(VillagerController) 的参数、作为 List&lt;VillagerController&gt;.Insert(int, VillagerController) 的参数 等共 8 处。</param>
    /// <param name="plan">中文含义：方案。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数 等共 4 处。</param>
    public Vector3 RegisterAndGetWaitingPoint(VillagerController enemy, EnemyWindowTraversalPlan plan)
    {
        CleanupWaiters();
        // 远处和楼下角色先正常赶往入口，抵达同层窗前才参与实体排队。
        // 否则水平距离很近的楼下角色会占据队首，却无法开始翻越。
        if (enemy != owner && (Mathf.Abs(enemy.transform.position.y - plan.sourceGround.y) > maximumSourceLevelDifference || Vector3.ProjectOnPlane(enemy.transform.position - plan.sourceGround, Vector3.up).sqrMagnitude > 9f))
        {
            waiters.Remove(enemy);
            return plan.sourceGround;
        }

        if (!waiters.Contains(enemy))
        {
            // 同帧发现窗口的顺序来自脚本更新顺序，不代表抵达窗口的顺序。
            // 新加入者按窗前距离插队，避免远处队首被近处等候者堵在后方。
            // 已开始翻越的占用者始终保留优先权。
            // newcomerOffset（新加入者偏移量）：使用位置：EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
Vector3 newcomerOffset = enemy.transform.position - plan.sourceGround;
            newcomerOffset.y = 0f;
            // insertIndex（插入索引）：使用位置：作为 List<VillagerController>.Insert(int, VillagerController) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
int insertIndex = waiters.Count;
            // i（当前遍历索引）：使用位置：作为 List<VillagerController>.this[int] 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
for (int i = 0; i < waiters.Count; i++)
            {
                if (waiters[i] == owner)
                {
                    continue;
                }

                // waiterOffset（等待者偏移量）：使用位置：EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
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
        // currentDistanceWindow（当前距离窗口）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
Vector3 currentDistanceWindow = Vector3.ProjectOnPlane(enemy.transform.position - plan.sourceGround, Vector3.up);
        if (owner == null && waiters.Count > 1 && waiters[0] != enemy && currentDistanceWindow.magnitude <= Mathf.Max(captureDistance, queueSpacing * 1.5f))
        {
            // queueFirstDistanceWindow（队列首个距离窗口）：使用位置：EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
float queueFirstDistanceWindow = Vector3.ProjectOnPlane(waiters[0].transform.position - plan.sourceGround, Vector3.up).magnitude;
            if (queueFirstDistanceWindow > currentDistanceWindow.magnitude + Mathf.Max(.25f, queueSpacing * .5f))
            {
                waiters.Remove(enemy);
                waiters.Insert(0, enemy);
            }
        }

        // index（当前元素索引）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.CeilToInt(float) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
int index = Mathf.Max(0, waiters.IndexOf(enemy));
        // outward（向外）：使用位置：EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
Vector3 outward = windowPoint.NormalWorld * plan.sourceSideSign;
        outward.y = 0f;
        if (outward.sqrMagnitude > 0.001f)
        {
            outward.Normalize();
        }

        // waitingPoint（等待中点）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 waitingPoint = plan.sourceGround + outward * index * queueSpacing;
        if (index == 0)
        {
            return plan.sourceGround;
        }

        // 窄平台的队尾可能越过木板边缘，导航投影仍会给出同层节点。
        // 等候点必须有真实的同层支撑；没有空间时原地等待前方释放，
        // 不能为了排队先走下无法一步返回的低平台。
        // waitingGround（等待中地面）：使用位置：作为 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 的参数、作为 Mathf.Abs(float) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
Vector3 waitingGround = plan.sourceGround;
        // sampleCount（采样数量）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
int sampleCount = Mathf.Clamp(Mathf.CeilToInt(index * queueSpacing / 0.2f), 1, 32);
        // sampleIndex（采样索引）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)。
for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            // sample（采样）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 的参数。
Vector3 sample = Vector3.Lerp(plan.sourceGround, waitingPoint, sampleIndex / (float)sampleCount);
            if (!TryFindGround(sample, out waitingGround) || Mathf.Abs(waitingGround.y - plan.sourceGround.y) > Mathf.Max(0.05f, enemy.maximumGroundStepUp))
            {
                return enemy.transform.position;
            }
        }

        return waitingGround;
    }

    /// <summary>TryAcquire 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken) 的参数。</param>
    public bool TryAcquire(VillagerController enemy) => TryRequest(enemy, out _);
    /// <summary>中文含义：通行身份标识。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。</summary>
    public GameArchitecture.ObjectIdentity TraversalIdentity => traversalRegister.Identity;

    /// <summary>RequestTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.RequestWindowOccupancy(EnemyWindowTraversalRoute)、EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)；下游（含内部回调体） EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken) 的参数、作为 TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle) 的参数、EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)。</param>
    /// <param name="request">中文含义：请求。使用位置：作为 TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle) 的参数。</param>
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

        // reason（原因）：保存 TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle) 的返回结果，供当前步骤使用。使用位置：作为 TraversalRequestResult.Reject(RejectionReason) 的参数、EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)。
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
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext) 的参数、EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。</param>
    /// <param name="token">中文含义：凭证。输出参数，由本入口写入并交给调用方。使用位置：EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。</param>
    public bool TryRequest(VillagerController enemy, out GameArchitecture.TraversalToken token)
    {
        // action（动作）：使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。
global::GameArchitecture.ActionHandle action;
        if (enemy != null && enemy.actionCoordinator != null)
        {
            action = enemy.actionCoordinator.CurrentHandle;
        }
        else
        {
            action = default;
        }

        // result（当前通行请求结果）：保存 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext) 的返回结果，供当前步骤使用。使用位置：EnemyWindowTraversalRoute.TryRequest(VillagerController, out TraversalToken)。
var result = RequestTraversal(enemy, new GameArchitecture.RequestContext("通路兼容入口", action.Object, TraversalIdentity, action.Id));
        token = result.Token;
        return result.HasAccept;
    }

    /// <summary>TryRequestCore 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)；下游（含内部回调体） EnemyWindowTraversalRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 TraversalTokenRegistry.Register(ActionHandle) 的参数、EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)。</param>
    /// <param name="token">中文含义：凭证。输出参数，由本入口写入并交给调用方。使用位置：EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)。</param>
    private bool TryRequestCore(VillagerController enemy, out GameArchitecture.TraversalToken token)
    {
        token = default;
        if (!isActiveAndEnabled || enemy == null || enemy.actionCoordinator == null || !enemy.actionCoordinator.CurrentHandle.Valid)
        {
            return false;
        }

        CleanupWaiters();
        if (windowPoint == null || windowPoint.FallingBodyInOpening || owner != null && owner != enemy)
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
    /// <param name="enemy">中文含义：敌人。使用位置：作为 List&lt;VillagerController&gt;.Remove(VillagerController) 的参数、EnemyWindowTraversalRoute.Release(VillagerController)。</param>
    public void Release(VillagerController enemy)
    {
        if (owner == enemy)
        {
            ReleaseTraversal(occupancyToken, GameArchitecture.ReleaseReason.Cancel);
        }

        waiters.Remove(enemy);
    }

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
    /// <param name="platformSide">中文含义：是否平台侧边。使用位置：EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)。</param>
    /// <param name="result">中文含义：结果。输出参数，由本入口写入并交给调用方。使用位置：作为 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 的参数、EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)。</param>
    public bool TryAcquireWindowSideTread(bool platformSide, out Vector3 result)
    {
        EnsureWindowReference();
        result = default;
        if (windowPoint)
        {
            // callReceiver6（调用前缓存的窗口点）：从窗口点读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 的参数。
global::WindowTraversalPoint callReceiver6 = windowPoint;
            // selectedValue5（分支选出的平台侧边符号）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 WindowTraversalPoint.GetApproachProbePosition(int) 的参数、EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)。
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
    /// <param name="probePosition">中文含义：探测位置。使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。</param>
    /// <param name="groundPoint">中文含义：地面点。输出参数，由本入口写入并交给调用方。使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。</param>
    private bool TryFindGround(Vector3 probePosition, out Vector3 groundPoint)
    {
        groundPoint = default;
        // origin（原点）：使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
Vector3 origin = probePosition;
        origin.y = windowPoint.OpeningCenterWorld.y + windowPoint.openingHeight * 0.5f + windowPoint.groundProbeHeight;
        // hits（命中结果）：保存 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, windowPoint.groundProbeDistance, windowPoint.groundMask, QueryTriggerInteraction.Ignore);
        // nearestDistance（最近距离）：使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
float nearestDistance = float.MaxValue;
        // found（是否已找到）：使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
bool found = false;
        // i（当前遍历索引）：使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
for (int i = 0; i < hits.Length; i++)
        {
            // candidate（候选）：使用位置：作为 WindowTraversalPoint.ShouldIgnoreCollider(Collider) 的参数、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)。
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
    /// <param name="token">中文含义：凭证。使用位置：作为 TraversalTokenRegistry.Valid(TraversalToken) 的参数。</param>
    public bool TraversalValid(GameArchitecture.TraversalToken token) => isActiveAndEnabled && traversalRegister.Valid(token);
    /// <summary>ReleaseTraversal 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.Remove(VillagerController)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversalRoute.Release(VillagerController)；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 TraversalTokenRegistry.Release(TraversalToken, ReleaseReason) 的参数。</param>
    /// <param name="reason">中文含义：原因。使用位置：作为 TraversalTokenRegistry.Release(TraversalToken, ReleaseReason) 的参数。</param>
    public bool ReleaseTraversal(GameArchitecture.TraversalToken token, GameArchitecture.ReleaseReason reason)
    {
        if (!traversalRegister.Release(token, reason))
        {
            return false;
        }

        waiters.Remove(owner);
        owner = null;
        occupancyToken = default;
        return true;
    }

    /// <summary>CancelWait 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：作为 List&lt;VillagerController&gt;.Remove(VillagerController) 的参数、EnemyWindowTraversalRoute.CancelWait(VillagerController)。</param>
    public void CancelWait(VillagerController character)
    {
        if (owner != character)
        {
            waiters.Remove(character);
        }
    }

    /// <summary>CleanupWaiters 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryRequestCore(VillagerController, out TraversalToken)；下游（含内部回调体） List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupWaiters()
    {
        // i（当前遍历索引）：使用位置：作为 List<VillagerController>.RemoveAt(int) 的参数、作为 List<VillagerController>.this[int] 的参数、EnemyWindowTraversalRoute.CleanupWaiters()。
for (int i = waiters.Count - 1; i >= 0; i--)
        {
            // waiter（等待者）：使用位置：EnemyWindowTraversalRoute.CleanupWaiters()。
VillagerController waiter = waiters[i];
            if (waiter == null || waiter.IsDead || !waiter.isActiveAndEnabled
                || waiter != owner && waiter.CurrentState != VillagerState.Chase)
            {
                waiters.RemoveAt(i);
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
    /// <summary>中文含义：路线。使用位置：作为 EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool) 的参数、作为 EnemyWindowTraversal.RequestWindowOccupancy(EnemyWindowTraversalRoute) 的参数、Compare(float, EnemyWindowTraversalPlan) 等共 10 处。</summary>
    public EnemyWindowTraversalRoute route;
    /// <summary>中文含义：来源地面。使用位置：作为 EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数 等共 8 处。</summary>
    public Vector3 sourceGround;
    /// <summary>中文含义：目的地地面。使用位置：作为 EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool) 的参数、作为 RegionWindowRouting.WalkLength(Vector3, Vector3, bool, bool) 的参数、EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan) 等共 4 处。</summary>
    public Vector3 destinationGround;
    /// <summary>中文含义：行进方向。使用位置：EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</summary>
    public Vector3 travelDirection;
    /// <summary>中文含义：朝向旋转。使用位置：EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</summary>
    public Quaternion facingRotation;
    /// <summary>中文含义：来源侧边符号。使用位置：EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)。</summary>
    public int sourceSideSign;
}
