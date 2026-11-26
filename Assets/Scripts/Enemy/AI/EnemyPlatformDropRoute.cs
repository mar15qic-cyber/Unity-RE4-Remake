using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 二楼外平台的敌人主动跳落路线。
/// 敌人先寻路到平台内接近点，再沿明确方向越过边缘并落到下层。
/// </summary>
[DisallowMultipleComponent]
public class EnemyPlatformDropRoute : MonoBehaviour
{
    private readonly HitQueryBuffer platformGroundHits = new();

    /// <summary>生效路线集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<EnemyPlatformDropRoute> activeRoutes = new List<EnemyPlatformDropRoute>();
    /// <summary>释放偏移量。</summary>
    [Header("接近与释放")]
    [Min(0.1f)]
    public float releaseOffset = 0.75f;
    /// <summary>捕获距离。</summary>
    [Min(0.1f)]
    public float captureDistance = 0.3f;
    /// <summary>队列间距。</summary>
    [Min(0.2f)]
    public float queueSpacing = 0.9f;
    /// <summary>最大来源级别差值。</summary>
    [Min(0.1f)]
    public float maximumSourceLevelDifference = 0.9f;
    /// <summary>来源探测高度。</summary>
    [Header("落点探测")]
    [Min(0.1f)]
    public float sourceProbeHeight = 1.2f;
    /// <summary>来源探测距离。</summary>
    [Min(0.5f)]
    public float sourceProbeDistance = 3f;
    /// <summary>最大掉落探测距离。</summary>
    [Min(0.5f)]
    public float maximumDropProbeDistance = 8f;
    /// <summary>最小掉落高度。</summary>
    [Min(0.5f)]
    public float minimumDropHeight = 1.2f;
    /// <summary>地面遮罩。</summary>
    public LayerMask groundMask = ~0;
    /// <summary>等待者集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<VillagerController> waiters = new List<VillagerController>();
    /// <summary>拥有者。</summary>
    private VillagerController owner;
    /// <summary>通行登记。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly GameArchitecture.TraversalTokenRegistry traversalRegister = new GameArchitecture.TraversalTokenRegistry();
    /// <summary>占用凭证。</summary>
    private GameArchitecture.TraversalToken occupancyToken;
    /// <summary>路线视图。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly System.Collections.ObjectModel.ReadOnlyCollection<EnemyPlatformDropRoute> routeView = activeRoutes.AsReadOnly();
    /// <summary>生效路线集合。</summary>
    public static IReadOnlyList<EnemyPlatformDropRoute> ActiveRoutes => routeView;

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyPlatformDropRoute&gt;.Add(EnemyPlatformDropRoute)、List&lt;EnemyPlatformDropRoute&gt;.Contains(EnemyPlatformDropRoute)、Object.GetInstanceID()、TraversalTokenRegistry.OpenRelease(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        traversalRegister.OpenRelease(GetInstanceID());
        if (!activeRoutes.Contains(this))
        {
            activeRoutes.Add(this);
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;EnemyPlatformDropRoute&gt;.Remove(EnemyPlatformDropRoute)、List&lt;VillagerController&gt;.Clear()、TraversalTokenRegistry.Close()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        traversalRegister.Close();
        occupancyToken = default;
        activeRoutes.Remove(this);
        waiters.Clear();
        owner = null;
    }

    /// <summary>TryBuildPlan 的业务处理入口；本方法直接依赖 EnemyPlatformDropPlan.EnemyPlatformDropPlan()、EnemyPlatformDropRoute.TryFindLandingGround(Vector3, float, out Vector3)、EnemyPlatformDropRoute.TryFindSourceGround(out Vector3)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、Mathf.Abs(float)、Quaternion.LookRotation(Vector3, Vector3)、Vector3.Normalize() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyPlatformDropPlan.EnemyPlatformDropPlan()、EnemyPlatformDropRoute.TryFindLandingGround(Vector3, float, out Vector3)、EnemyPlatformDropRoute.TryFindSourceGround(out Vector3)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、Mathf.Abs(float)、Quaternion.LookRotation(Vector3, Vector3)、Vector3.Normalize() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="plan">方案。输出参数，由本入口写入并交给调用方。</param>
    public bool TryBuildPlan(VillagerController enemy, Vector3 targetPosition, out EnemyPlatformDropPlan plan)
    {
        plan = default;
        if (enemy == null)
        {
            return false;
        }

        HouseThreeRegionNavigation region = HouseThreeRegionNavigation.Current;
        bool onPlatform;
        if (region != null)
        {
            onPlatform = region.IsOnSecondFloorOuterPlatform(enemy.transform.position);
        }
        else
        {
            onPlatform = EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(enemy.transform.position);
        }

        if (!onPlatform)
        {
            return false;
        }

        if (!TryFindSourceGround(out Vector3 /* 中文含义：来源地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceGround))
        {
            return false;
        }

        float allowedSourceLevelDiff = Mathf.Max(maximumSourceLevelDifference, 1.45f);
        if (Mathf.Abs(enemy.transform.position.y - sourceGround.y) > allowedSourceLevelDiff)
        {
            return false;
        }

        // 行进方向。
Vector3 travelDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (travelDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        travelDirection.Normalize();
        // 落地探测。
Vector3 landingProbe = sourceGround + travelDirection * releaseOffset;
        if (!TryFindLandingGround(landingProbe, sourceGround.y, out Vector3 /* 中文含义：落地地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ landingGround))
        {
            return false;
        }

        if (sourceGround.y - landingGround.y < minimumDropHeight || sourceGround.y - targetPosition.y < minimumDropHeight * 0.75f)
        {
            return false;
        }

        plan = new EnemyPlatformDropPlan
        {
            route = this,
            sourceGround = sourceGround,
            landingProbe = landingProbe,
            landingGround = landingGround,
            travelDirection = travelDirection,
            facingRotation = Quaternion.LookRotation(travelDirection, Vector3.up)
        };
        return true;
    }

    /// <summary>RegisterAndGetWaitingPoint 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.CleanupWaiters()、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.IndexOf(VillagerController)、Mathf.Max(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyPlatformDropRoute.CleanupWaiters()、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.Contains(VillagerController)、List&lt;VillagerController&gt;.IndexOf(VillagerController)、Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="plan">方案。</param>
    public int ReadActiveQueueLoad(VillagerController excludeEnemy)
    {
        CleanupWaiters();
        int count = 0;
        if (owner != null && owner != excludeEnemy)
        {
            count++;
        }

        for (int i = 0; i < waiters.Count; i++)
        {
            VillagerController waiter = waiters[i];
            if (waiter != null && waiter != owner && waiter != excludeEnemy)
            {
                count++;
            }
        }

        return count;
    }

    public Vector3 RegisterAndGetWaitingPoint(VillagerController enemy, EnemyPlatformDropPlan plan, Vector3 approach)
    {
        CleanupWaiters();
        Vector3 offset = Vector3.ProjectOnPlane(enemy.transform.position - approach, Vector3.up);
        float allowedSourceLevelDiff = Mathf.Max(maximumSourceLevelDifference, 1.45f);
        if (enemy != owner && (offset.sqrMagnitude > 9f
            || Mathf.Abs(enemy.transform.position.y - plan.sourceGround.y) > allowedSourceLevelDiff))
        {
            waiters.Remove(enemy);
            return approach;
        }

        if (!waiters.Contains(enemy))
        {
            waiters.Add(enemy);
        }

        // 按当前到起跳点的水平距离升序维护等待队列，确保最靠近边沿的敌人始终处于队首。
        for (int i = 1; i < waiters.Count; i++)
        {
            VillagerController current = waiters[i];
            float currentSqr = Vector3.ProjectOnPlane(current.transform.position - approach, Vector3.up).sqrMagnitude;
            int currentId = current.GetInstanceID();
            int insertIndex = i - 1;
            while (insertIndex >= 0)
            {
                VillagerController compare = waiters[insertIndex];
                float compareSqr = Vector3.ProjectOnPlane(compare.transform.position - approach, Vector3.up).sqrMagnitude;
                if (compareSqr < currentSqr || (compareSqr == currentSqr && compare.GetInstanceID() <= currentId))
                {
                    break;
                }

                waiters[insertIndex + 1] = compare;
                insertIndex--;
            }

            waiters[insertIndex + 1] = current;
        }

        int index = Mathf.Max(0, waiters.IndexOf(enemy));
        if (index == 0)
        {
            return approach;
        }

        // 二楼外平台纵深较窄，后排等待点沿房檐切线左右错开并少量后撤，避免退入墙内失败后全员抢同一点。
        Vector3 tangent = Vector3.Cross(Vector3.up, plan.travelDirection);
        if (tangent.sqrMagnitude > 0.0001f)
        {
            tangent.Normalize();
        }

        int sideSign;
        if (index % 2 == 1)
        {
            sideSign = 1;
        }
        else
        {
            sideSign = -1;
        }

        int laneStep = (index + 1) / 2;
        float lateralOffset = laneStep * queueSpacing * 0.85f * sideSign;
        float setbackOffset = Mathf.Min(0.55f, laneStep * 0.25f);
        return approach + tangent * lateralOffset - plan.travelDirection * setbackOffset;
    }

    /// <summary>TryAcquire 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.TryRequest(VillagerController, out TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyPlatformDropRoute.TryRequest(VillagerController, out TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public bool TryAcquire(VillagerController enemy) => TryRequest(enemy, out _);
    /// <summary>通行身份标识。</summary>
    public GameArchitecture.ObjectIdentity TraversalIdentity => traversalRegister.Identity;

    /// <summary>RequestTraversal 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.TryRequest(VillagerController, out TraversalToken)、EnemyWindowTraversal.RequestJumpLandOccupancy(EnemyPlatformDropRoute)；下游（含内部回调体） EnemyPlatformDropRoute.TryRequestCore(VillagerController, out TraversalToken)、TraversalRequestResult.Accept(TraversalToken)、TraversalRequestResult.Reject(RejectionReason)、TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

    /// <summary>TryRequest 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.TryAcquire(VillagerController)；下游（含内部回调体） EnemyPlatformDropRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

    /// <summary>TryRequestCore 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.RequestTraversal(VillagerController, RequestContext)；下游（含内部回调体） EnemyPlatformDropRoute.CleanupWaiters()、TraversalTokenRegistry.Register(ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
        if (owner != null && owner != enemy)
        {
            return false;
        }

        if (waiters.Count > 0 && waiters[0] != enemy)
        {
            // 已抵达边沿且明显更靠近跳落点的角色允许立即越过尚被挡在身后的队首起跳。
            Vector3 headOffset = Vector3.ProjectOnPlane(waiters[0].transform.position - transform.position, Vector3.up);
            Vector3 actorOffset = Vector3.ProjectOnPlane(enemy.transform.position - transform.position, Vector3.up);
            if (headOffset.magnitude <= actorOffset.magnitude + 0.15f)
            {
                return false;
            }
        }

        owner = enemy;
        occupancyToken = traversalRegister.Register(enemy.actionCoordinator.CurrentHandle);
        token = occupancyToken;
        return token.Valid;
    }

    /// <summary>Release 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public void Release(VillagerController enemy)
    {
        if (owner == enemy)
        {
            ReleaseTraversal(occupancyToken, GameArchitecture.ReleaseReason.Cancel);
        }

        waiters.Remove(enemy);
    }

    /// <summary>TryFindSourceGround 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.TryFindGround(Vector3, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.TryBuildPlan(VillagerController, Vector3, out EnemyPlatformDropPlan)；下游（含内部回调体） EnemyPlatformDropRoute.TryFindGround(Vector3, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceGround">来源地面。输出参数，由本入口写入并交给调用方。</param>
    private bool TryFindSourceGround(out Vector3 sourceGround)
    {
        // 原点。
Vector3 origin = transform.position + Vector3.up * sourceProbeHeight;
        return TryFindGround(origin, sourceProbeDistance + sourceProbeHeight, out sourceGround);
    }

    /// <summary>TryFindLandingGround 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.TryFindGround(Vector3, float, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.TryBuildPlan(VillagerController, Vector3, out EnemyPlatformDropPlan)；下游（含内部回调体） EnemyPlatformDropRoute.TryFindGround(Vector3, float, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="landingProbe">落地探测。</param>
    /// <param name="sourceHeight">来源高度。</param>
    /// <param name="landingGround">落地地面。输出参数，由本入口写入并交给调用方。</param>
    private bool TryFindLandingGround(Vector3 landingProbe, float sourceHeight, out Vector3 landingGround)
    {
        // 原点。
Vector3 origin = landingProbe + Vector3.up * 0.5f;
        if (!TryFindGround(origin, maximumDropProbeDistance, out landingGround))
        {
            return false;
        }

        return sourceHeight - landingGround.y >= minimumDropHeight;
    }

    /// <summary>TryFindGround 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.TryFindLandingGround(Vector3, float, out Vector3)、EnemyPlatformDropRoute.TryFindSourceGround(out Vector3)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">原点。</param>
    /// <param name="distance">距离。</param>
    /// <param name="groundPoint">地面点。输出参数，由本入口写入并交给调用方。</param>
    private bool TryFindGround(Vector3 origin, float distance, out Vector3 groundPoint)
    {
        groundPoint = default;
        // 命中结果。
        RaycastHit[] hits = platformGroundHits.Raycast(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore, out int hitCount);
        // 最近距离。
float nearestDistance = float.MaxValue;
        // 是否已找到。
bool found = false;
        // 当前遍历索引。
        for (int i = 0; i < hitCount; i++)
        {
            // 候选。
Collider candidate = hits[i].collider;
            if (candidate == null || candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null || hits[i].normal.y < 0.45f || hits[i].distance >= nearestDistance)
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
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.Release(VillagerController)、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.TryBeginPendingDropTraversal()；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="reason">原因。</param>
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
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    public void CancelWait(VillagerController character)
    {
        if (owner != character)
        {
            waiters.Remove(character);
        }
    }

    /// <summary>CleanupWaiters 的业务处理入口；本方法直接依赖 List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPlatformDropRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyPlatformDropPlan)、EnemyPlatformDropRoute.TryRequestCore(VillagerController, out TraversalToken)；下游（含内部回调体） List&lt;VillagerController&gt;.RemoveAt(int)、TraversalTokenRegistry.Release(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupWaiters()
    {
        // 当前遍历索引。
for (int i = waiters.Count - 1; i >= 0; i--)
        {
            // 等待者。
VillagerController waiter = waiters[i];
            if (waiter == null || waiter.IsDead || !waiter.isActiveAndEnabled)
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

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireSphere(Vector3, float)、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        // 方向。
Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, captureDistance);
        Gizmos.DrawLine(transform.position, transform.position + direction * releaseOffset);
        Gizmos.DrawWireSphere(transform.position + direction * releaseOffset, 0.12f);
    }
}

/// <summary>敌人行为、动作与通行的结构化数据 EnemyPlatformDropPlan；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
public struct EnemyPlatformDropPlan
{
    /// <summary>路线。</summary>
    public EnemyPlatformDropRoute route;
    /// <summary>来源地面。</summary>
    public Vector3 sourceGround;
    /// <summary>落地探测。</summary>
    public Vector3 landingProbe;
    /// <summary>落地地面。</summary>
    public Vector3 landingGround;
    /// <summary>行进方向。</summary>
    public Vector3 travelDirection;
    /// <summary>朝向旋转。</summary>
    public Quaternion facingRotation;
}
