using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// 敌人窗口翻越与平台坠落执行器。
/// 普通寻路只负责到达窗口两侧，本组件独占翻窗曲线与空中重力。
/// </summary>
[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
[RequireComponent(typeof(VillagerController))]
public partial class EnemyWindowTraversal : MonoBehaviour
{
    private readonly HitQueryBuffer windowHits = new();

    /// <summary>RequestWindowOccupancy 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginPendingWindowTraversal()；下游（含内部回调体） EnemyWindowTraversalRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="route">路线。</param>
    private bool RequestWindowOccupancy(EnemyWindowTraversalRoute route)
    {
        // 动作。
var action = controller.actionCoordinator.CurrentHandle;
        // 当前通行请求结果。
var result = route.RequestTraversal(controller, new GameArchitecture.RequestContext("窗口通行执行", action.Object, route.TraversalIdentity, action.Id));
        windowToken = result.Token;
        return result.HasAccept;
    }

    /// <summary>RequestJumpLandOccupancy 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginPendingDropTraversal()；下游（含内部回调体） EnemyPlatformDropRoute.RequestTraversal(VillagerController, RequestContext)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="route">路线。</param>
    private bool RequestJumpLandOccupancy(EnemyPlatformDropRoute route)
    {
        // 动作。
var action = controller.actionCoordinator.CurrentHandle;
        // 当前通行请求结果。
var result = route.RequestTraversal(controller, new GameArchitecture.RequestContext("平台跳落执行", action.Object, route.TraversalIdentity, action.Id));
        jumpLandToken = result.Token;
        return result.HasAccept;
    }

    /// <summary>通行运动凭证。</summary>
    private GameArchitecture.MotionControlToken traversalMotionToken;
    /// <summary>窗口凭证。</summary>
    private GameArchitecture.TraversalToken windowToken;
    /// <summary>跳跃落地凭证。</summary>
    private GameArchitecture.TraversalToken jumpLandToken;
    /// <summary>CommitTraversalPose 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.UpdateDropMotion(float)、EnemyWindowTraversal.UpdateWindowTraversal()；下游（含内部回调体） CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="orientation">朝向。</param>
    private bool CommitTraversalPose(Vector3 position, Quaternion orientation)
    {
        return controller != null && controller.motionExecutor != null && controller.motionExecutor.CommitPose(traversalMotionToken, position, orientation);
    }

    /// <summary>最大路线投影距离。编译期常量，保持既有值与消费者映射。</summary>
    private const float maximumRouteProjectionDistance = 0.6f;
    /// <summary>村民敌人控制器。</summary>
    [Header("组件引用")]
    public VillagerController controller;
    /// <summary>寻路代理。</summary>
    public PathAgent agent;
    /// <summary>动画控制器。</summary>
    public Animator animator;
    /// <summary>移动碰撞体。</summary>
    public CapsuleCollider movementCollider;
    /// <summary>窗口弧线净空。</summary>
    [Header("翻窗轨迹")]
    [Min(0f)]
    public float windowArcClearance = 0.18f;
    /// <summary>动画混合时长。</summary>
    [Min(0.02f)]
    public float animationBlendDuration = 0.14f;
    /// <summary>窗口运动起始归一化时间。</summary>
    [Range(0f, 0.4f)]
    public float windowMotionStartNormalizedTime = 0.12f;
    /// <summary>窗口运动结束归一化时间。</summary>
    [Range(0.6f, 1f)]
    public float windowMotionEndNormalizedTime = 0.9f;
    /// <summary>边缘探测距离。</summary>
    [Header("平台坠落")]
    [Min(0.1f)]
    public float edgeProbeDistance = 0.7f;
    /// <summary>支撑探测距离。</summary>
    [Min(0.1f)]
    public float supportProbeDistance = 0.55f;
    /// <summary>最大掉落探测距离。</summary>
    [Min(1f)]
    public float maximumDropProbeDistance = 8f;
    /// <summary>最小掉落高度。</summary>
    [Min(0.5f)]
    public float minimumDropHeight = 1.2f;
    /// <summary>掉落初始向上速度。</summary>
    [Min(0f)]
    public float dropInitialUpwardSpeed = 1.1f;
    /// <summary>中文含义：掉落水平速度。</summary>
    [Min(0f)]
    public float dropHorizontalSpeed = 1.15f;
    /// <summary>掉落重力。</summary>
    [Min(0.1f)]
    public float dropGravity = 18f;
    /// <summary>掉落起始持续时间。</summary>
    [Min(0.05f)]
    public float dropStartDuration = 0.33f;
    /// <summary>掉落落地持续时间。</summary>
    [Min(0.05f)]
    public float dropLandingDuration = 0.5f;
    [SerializeField] private AnimationClip nativeDropStart;
    [SerializeField] private AnimationClip nativeDropFall;
    [SerializeField] private AnimationClip nativeDropLanding;
    /// <summary>是否启用通行诊断。</summary>
    [Header("翻窗诊断")]
    public bool enableTraversalDiagnostic;
    /// <summary>通行诊断间隔。</summary>
    [Min(0.1f)]
    public float traversalDiagnosticInterval = 0.5f;
    /// <summary>窗口交接持续时间。</summary>
    [Min(0.02f)]
    public float windowHandoffDuration = 0.08f;
    /// <summary>通行交接直到。</summary>
    private float traversalHandoffUntil = float.NegativeInfinity;
    /// <summary>通行交接位置。</summary>
    private Vector3 traversalHandoffPosition;
    /// <summary>通行交接旋转。</summary>
    private Quaternion traversalHandoffRotation;
    /// <summary>通行交接恢复执行是否仍待处理。</summary>
    private bool traversalHandoffResumePending;
    private float platformExitClearanceUntil;
    private Vector3 platformExitClearanceTarget;
    private EnemyWindowTraversalRoute delayedReleaseWindowRoute;
    private GameArchitecture.TraversalToken delayedReleaseWindowToken;
    private float delayedReleaseWindowUntil;
    private Vector3 delayedReleaseWindowOrigin;
    /// <summary>阶段。</summary>
    private TraversalPhase phase;
    /// <summary>待处理方案。</summary>
    private EnemyWindowTraversalPlan pendingPlan;
    /// <summary>是否存在待处理的方案。</summary>
    private bool hasPendingPlan;
    /// <summary>当前路线。</summary>
    private EnemyWindowTraversalRoute currentRoute;
    private WindowTraversalPoint fallOpening;
    private Vector3 fallExit;
    private GameArchitecture.ObjectIdentity fallIdentity;

    internal EnemyWindowTraversalRoute ReadActiveOrPendingWindowRoute()
    {
        if (currentRoute != null)
        {
            return currentRoute;
        }

        if (hasPendingPlan)
        {
            return pendingPlan.route;
        }

        return null;
    }

    internal bool IsClearingOpening(WindowTraversalPoint opening)
    {
        if (fallOpening == null || !isActiveAndEnabled || controller == null
            || controller.actionCoordinator.Identity != fallIdentity)
        {
            fallOpening = null;
            return false;
        }
        Vector3 offset = transform.position - fallOpening.OpeningCenterWorld;
        float depth = Mathf.Abs(Vector3.Dot(offset, fallOpening.NormalWorld));
        if (depth > fallOpening.wallThickness * .5f + .63f || offset.y < -2.1f)
        {
            fallOpening = null;
            return false;
        }
        return opening == fallOpening;
    }

    internal bool IgnoreInterruptedSill(Collider other)
    {
        return fallOpening != null && IsClearingOpening(fallOpening) && fallOpening.ShouldIgnoreCollider(other);
    }

    internal bool ClearingImpactOpening => fallOpening != null && IsClearingOpening(fallOpening);

    internal Vector3 ReadWindowImpactStep(Vector3 displacement)
    {
        if (fallOpening == null || !IsClearingOpening(fallOpening))
        {
            return displacement;
        }
        Vector3 remaining = Vector3.ProjectOnPlane(fallExit - transform.position, Vector3.up);
        float distance = Mathf.Max(displacement.magnitude, 2.5f * Time.deltaTime);
        Vector3 step = Vector3.ClampMagnitude(remaining, distance);
        if (step.sqrMagnitude < .000001f)
        {
            return Vector3.zero;
        }
        Vector3 root = transform.position;
        Vector3 direction = step.normalized;
        float allowed = step.magnitude;
        RaycastHit[] hits = windowHits.CapsuleCast(root + Vector3.up * .29f, root + Vector3.up * .98f,
            .24f, direction, allowed + .025f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out int hitCount);
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = hits[hitIndex];
            if (hit.collider.transform.IsChildOf(transform) || fallOpening.ShouldIgnorePathCollider(hit.collider)
                || hit.normal.y > .65f || Vector3.Dot(hit.normal, direction) >= -.04f)
            {
                continue;
            }
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .025f));
        }
        return direction * allowed;
    }

    internal bool TryInterruptInOpening(Vector3 attacker, out Vector3 impactSource)
    {
        impactSource = attacker;
        if (!IsWindowTraversalActive || currentRoute == null || currentRoute.windowPoint == null)
        {
            return false;
        }
        var opening = currentRoute.windowPoint;
        Vector3 normal = opening.NormalWorld;
        float depth = Vector3.Dot(transform.position - opening.OpeningCenterWorld, normal);
        float impact = Vector3.Dot(transform.position - attacker, normal);
        float side = Mathf.Sign(depth);
        if (Mathf.Abs(depth) < .22f)
        {
            side = Mathf.Sign(impact);
        }
        Vector3 destination = motionDestination;
        if (side * Vector3.Dot(destination - opening.OpeningCenterWorld, normal) < 0f)
        {
            destination = motionStart;
        }
        if (Mathf.Abs(Vector3.Dot(destination - opening.OpeningCenterWorld, normal)) < .7f)
        {
            destination += normal * side * .7f;
        }
        Vector3 direction = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized;
        fallExit = destination + direction * .28f;
        impactSource = transform.position - direction;
        fallOpening = opening;
        fallIdentity = controller.actionCoordinator.Identity;
        opening.HoldForFallingActor(this);
        CancelTraversal(false);
        return true;
    }

    /// <summary>运动起始。</summary>
    private Vector3 motionStart;
    /// <summary>运动控制。</summary>
    private Vector3 motionControl;
    /// <summary>运动目的地。</summary>
    private Vector3 motionDestination;
    /// <summary>运动旋转。</summary>
    private Quaternion motionRotation;
    /// <summary>运动起始旋转。</summary>
    private Quaternion motionStartRotation;
    /// <summary>掉落水平速度。</summary>
    private Vector3 dropHorizontalVelocity;
    /// <summary>掉落竖直速度。</summary>
    private float dropVerticalSpeed;
    /// <summary>掉落落地位置。</summary>
    private Vector3 dropLandingPosition;
    /// <summary>阶段开始时间。</summary>
    private float phaseStartedTime;
    /// <summary>前一次移动碰撞体是否启用。</summary>
    private bool previousMovementColliderEnabled;
    /// <summary>跟踪的平台区域编号。</summary>
    private string trackedPlatformRegionId;
    /// <summary>是否具有平台区域状态。</summary>
    private bool hasPlatformRegionState;
    /// <summary>是否事件跟踪的平台区域。</summary>
    private bool isOnTrackedPlatformRegion;
    /// <summary>是否存在待处理的窗口目的地事件平台。</summary>
    private bool pendingWindowDestinationOnPlatform;
    /// <summary>上一次平台支撑时间。</summary>
    /// <summary>动画导航图。</summary>
    private PlayableGraph animationGraph;
    /// <summary>动画片段可播放。</summary>
    private AnimationClipPlayable clipPlayable;
    /// <summary>待处理掉落方案。</summary>
    private EnemyPlatformDropPlan pendingDropPlan;
    private Vector3 pendingDropApproach;
    /// <summary>当前掉落路线。</summary>
    private EnemyPlatformDropRoute currentDropRoute;
    /// <summary>是否存在待处理的掉落方案。</summary>
    private bool hasPendingDropPlan;
    /// <summary>是否前一次待处理命中。</summary>
    private bool previousPendingHit;
    /// <summary>上一次通行诊断。</summary>
    private string lastTraversalDiagnostic;
    /// <summary>下一次通行诊断时间。</summary>
    private float nextTraversalDiagnosticTime;
    /// <summary>是否生效。</summary>
    public bool IsActive => phase != TraversalPhase.None;
    /// <summary>是否移动受保护的。</summary>
    public bool IsMovementProtected => IsActive || traversalHandoffResumePending || HasActiveLadderTraversal;
    /// <summary>中文含义：是否翻窗通行生效。</summary>
    public bool IsWindowTraversalActive => phase == TraversalPhase.Window;
    /// <summary>中文含义：是否掉落生效。</summary>
    public bool IsDropActive => phase == TraversalPhase.DropStart || phase == TraversalPhase.DropFall || phase == TraversalPhase.DropLanding;
    /// <summary>是否存在待处理的路线。</summary>
    public bool HasPendingRoute => hasPendingPlan;
    /// <summary>是否存在待处理的掉落路线。</summary>
    public bool HasPendingDropRoute => hasPendingDropPlan;
    public bool HasActivePlatformExitClearance
    {
        get
        {
            if (Time.time < platformExitClearanceUntil && IsCurrentActorOnPlatform())
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>敌人行为、动作与通行使用的 TraversalPhase 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum TraversalPhase
    {
        /// <summary>中文含义：无（未选择或尚未发生）。</summary>
        None,
        /// <summary>中文含义：窗口。</summary>
        Window,
        /// <summary>中文含义：掉落起始。</summary>
        DropStart,
        /// <summary>中文含义：掉落下落。</summary>
        DropFall,
        /// <summary>中文含义：掉落落地。</summary>
        DropLanding
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.InitializePlatformRegionState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        CacheComponents();
        InitializePlatformRegionState();
    }

    /// <summary>是否事件跟踪的平台。</summary>
    public bool IsOnTrackedPlatform => IsCurrentActorOnPlatform();

    /// <summary>TryGetPlatformRegionState 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)、EnemyWindowTraversalRoute.TryBuildPlan(VillagerController, Vector3, bool, out EnemyWindowTraversalPlan)；下游（含内部回调体） EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="regionId">区域编号。</param>
    /// <param name="isOnPlatform">是否事件平台。输出参数，由本入口写入并交给调用方。</param>
    public bool TryGetPlatformRegionState(string regionId, out bool isOnPlatform)
    {
        InitializePlatformRegionState();
        isOnPlatform = false;
        if (!hasPlatformRegionState || string.IsNullOrWhiteSpace(regionId) || trackedPlatformRegionId != regionId)
        {
            return false;
        }

        isOnPlatform = IsCurrentActorOnPlatform();
        return true;
    }

    internal bool TryReadPlatformExitClearance(out Vector3 destination)
    {
        destination = platformExitClearanceTarget;
        if (platformExitClearanceUntil <= 0f)
        {
            return false;
        }

        bool expired = Time.time >= platformExitClearanceUntil || !IsCurrentActorOnPlatform() || controller == null;
        if (!expired)
        {
            Vector3 remaining = destination - transform.position;
            remaining.y = 0f;
            Vector3 walkedFromLanding = transform.position - traversalHandoffPosition;
            walkedFromLanding.y = 0f;
            if (remaining.sqrMagnitude < 0.30f * 0.30f
                || walkedFromLanding.sqrMagnitude >= 1.25f * 1.25f
                || !controller.CanMoveDirectlyToNavigationPoint(destination, true))
            {
                expired = true;
            }
        }

        if (expired)
        {
            platformExitClearanceUntil = 0f;
            if (agent != null && agent.enabled)
            {
                agent.RequestImmediateRepath();
            }
            if (controller != null)
            {
                controller.InvalidateChaseRefreshFrame();
            }
            return false;
        }

        return true;
    }

    private void BeginPlatformExitClearance()
    {
        platformExitClearanceUntil = 0f;
        HouseThreeRegionNavigation region = HouseThreeRegionNavigation.Current;
        if (region == null || controller == null)
        {
            return;
        }

        Vector3 heading = Vector3.ProjectOnPlane(traversalHandoffRotation * Vector3.forward, Vector3.up).normalized;
        Vector3 landing = traversalHandoffPosition;
        var start = region.ReadSameSideNavigationPoint(landing, true, .9f, .5f);
        if (start.node == null)
        {
            return;
        }

        // 优先沿着木板走廊切向与玩家方向脱离窗台落点，不再因近距离同伴阻挡或垂直阈值错失出口。
        float sideSign = 1f;
        if (controller.Player != null)
        {
            Vector3 toPlayer = Vector3.ProjectOnPlane(controller.Player.transform.position - landing, Vector3.up);
            if (toPlayer.sqrMagnitude > 0.001f && Vector3.SignedAngle(heading, toPlayer, Vector3.up) < 0f)
            {
                sideSign = -1f;
            }
        }

        float[] probeDistances = new float[] { 1.65f, 1.35f, 1.05f };
        float[] candidateAngles = new float[]
        {
            0f,
            45f * sideSign,
            75f * sideSign,
            90f * sideSign,
            -45f * sideSign,
            -75f * sideSign,
            -90f * sideSign
        };
        for (int distanceIndex = 0; distanceIndex < probeDistances.Length; distanceIndex++)
        {
            float probeDistance = probeDistances[distanceIndex];
            for (int angleIndex = 0; angleIndex < candidateAngles.Length; angleIndex++)
            {
                float angle = candidateAngles[angleIndex];
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * heading;
                var clear = region.ReadSameSideNavigationPoint(landing + direction * probeDistance, true, .65f, .55f);
                if (clear.node == null || start.node.Area != clear.node.Area
                    || Vector3.Dot(clear.position - landing, heading) < -0.05f
                    || !controller.CanMoveDirectlyToNavigationPoint(clear.position, true))
                {
                    continue;
                }

                platformExitClearanceTarget = clear.position;
                platformExitClearanceUntil = Time.time + 0.75f;
                return;
            }
        }
    }

    /// <summary>TryResolveSamePlatformPursuitDestination 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RefreshChaseDestination()；下游（含内部回调体） EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">玩家位置。</param>
    /// <param name="pursuitDestination">追击目的地。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolveSamePlatformPursuitDestination(Vector3 playerPosition, out Vector3 pursuitDestination)
    {
        pursuitDestination = playerPosition;
        InitializePlatformRegionState();
        if (!IsCurrentActorOnPlatform() || string.IsNullOrWhiteSpace(trackedPlatformRegionId))
        {
            return false;
        }

        if (!ResolveTargetPlatformState(trackedPlatformRegionId, playerPosition))
        {
            return false;
        }

        // 区域。
var region = HouseThreeRegionNavigation.Current;
        if (region != null)
        {
            // 接近点。
var approachPoint = region.ReadChaseApproachPoint(playerPosition, true);
            if (approachPoint.node == null)
            {
                return false;
            }

            var origin = region.ReadSameSideNavigationPoint(transform.position, true, 1.25f, .6f);
            if (origin.node == null || origin.node.Area != approachPoint.node.Area)
            {
                return false;
            }

            pursuitDestination = approachPoint.position;
        }

        return true;
    }

    /// <summary>InitializePlatformRegionState 的业务处理入口；本方法直接依赖 string.IsNullOrWhiteSpace(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.ShouldPursueByDrop(Vector3)、EnemyWindowTraversal.Start()、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializePlatformRegionState()
    {
        if (hasPlatformRegionState)
        {
            return;
        }

        // 路线集合。
IReadOnlyList<EnemyWindowTraversalRoute> routes = EnemyWindowTraversalRoute.ActiveRoutes;
        // 当前遍历索引。
for (int i = 0; i < routes.Count; i++)
        {
            // 路线。
EnemyWindowTraversalRoute route = routes[i];
            if (route == null || !route.isActiveAndEnabled || string.IsNullOrWhiteSpace(route.platformRegionId))
            {
                continue;
            }

            trackedPlatformRegionId = route.platformRegionId;
            hasPlatformRegionState = true;
            isOnTrackedPlatformRegion = false;
            return;
        }
    }

    /// <summary>IsCurrentActorOnPlatform 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.IsOnTrackedPlatform.get、EnemyWindowTraversal.RecordPlatformSupportBeforeMovement()、EnemyWindowTraversal.ShouldPursueByDrop(Vector3)、EnemyWindowTraversal.TryBeginDropFromHit(Vector3)、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool) 等共 7 处；下游（含内部回调体） EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsCurrentActorOnPlatform()
    {
        InitializePlatformRegionState();
        if (HouseThreeRegionNavigation.Current != null && !IsMovementProtected)
        {
            isOnTrackedPlatformRegion = HouseThreeRegionNavigation.Current.ReadPlatformOwnership(transform.position, isOnTrackedPlatformRegion);
        }

        if (hasPlatformRegionState)
        {
            return isOnTrackedPlatformRegion;
        }

        return EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(transform.position);
    }

    /// <summary>ResolveTargetPlatformState 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWindowTraversal&gt;()、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.ShouldPursueByDrop(Vector3)、EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） Component.GetComponent&lt;LeonWindowTraversal&gt;()、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="regionId">区域编号。</param>
    /// <param name="targetPosition">目标位置。</param>
    private bool ResolveTargetPlatformState(string regionId, Vector3 targetPosition)
    {
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue1 = EnemyAIManager.Instance;
        // 玩家。
LeonController player;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            player = receiverValue1.PlayerTarget;
        }
        else
        {
            player = default;
        }

        if (player != null)
        {
            // 通行。
LeonWindowTraversal traversal = player.GetComponent<LeonWindowTraversal>();
            if (traversal != null && traversal.TryGetEnemyPlatformRegionState(regionId, out bool /* 中文含义：是否跟踪的状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ trackedState))
            {
                return trackedState;
            }
        }

        return EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(regionId, targetPosition);
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.Valid(MotionControlToken)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyPlatformDropRoute.TraversalValid(TraversalToken)、EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.RecordPlatformSupportBeforeMovement()、EnemyWindowTraversal.UpdateSpecialTraversal() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        CacheComponents();
        if (controller == null)
        {
            return;
        }

        if (HasActiveLadderTraversal)
        {
            TickLadderTraversal();
            return;
        }

        if (controller.IsDead)
        {
            ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Cancel);
            CancelTraversal(false);
            return;
        }

        TickDelayedWindowTokenRelease();

        if ((IsActive || traversalHandoffResumePending) && !controller.motionExecutor.Valid(traversalMotionToken))
        {
            CancelTraversal(false);
            return;
        }

        if (currentRoute != null && !currentRoute.TraversalValid(windowToken) || currentDropRoute != null && !currentDropRoute.TraversalValid(jumpLandToken))
        {
            CancelTraversal(false);
            return;
        }

        if (IsActive)
        {
            UpdateSpecialTraversal();
            if (IsActive)
            {
                controller.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
                // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue3 = EnemyAIManager.Instance;
                if (!global::System.Object.ReferenceEquals(receiverValue3, null))
                {
                    receiverValue3.ReleaseCombatReservation(controller);
                }
            }

            return;
        }

    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.Valid(MotionControlToken)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、EnemyWindowTraversal.HasPendingWindowPriority() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (HasActiveLadderTraversal)
        {
            ApplyLadderContacts();
            return;
        }

        if (TryReadLadderApproach(out _))
        {
            if (CanBeginLadderTraversal())
            {
                controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Traversal, nameof(EnemyWindowTraversal));
            }
            return;
        }

        if (controller == null || controller.IsDead)
        {
            traversalHandoffResumePending = false;
            ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Cancel);
            return;
        }

        TickDelayedWindowTokenRelease();

        if (traversalHandoffResumePending)
        {
            if (controller == null || !controller.motionExecutor.Valid(traversalMotionToken))
            {
                CancelTraversal(false);
                return;
            }

            if (controller.IsDead || controller.HasPendingHit || controller.CurrentState == VillagerState.Staggered)
            {
                traversalHandoffResumePending = false;
                traversalHandoffUntil = float.NegativeInfinity;
                if (!controller.IsDead)
                {
                    controller.CompleteTraversalAndResumeChase();
                }

                return;
            }

            CommitTraversalPose(traversalHandoffPosition, traversalHandoffRotation);
            controller.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
            // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue4 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.ReleaseCombatReservation(controller);
            }

            if (Time.time < traversalHandoffUntil)
            {
                return;
            }

            traversalHandoffResumePending = false;
            controller.CompleteTraversalAndResumeChase();
        }

        if (IsActive)
        {
            controller.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
            // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue5 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                receiverValue5.ReleaseCombatReservation(controller);
            }

            return;
        }

        // 是否已接收响应式命中。
bool receivedReactiveHit = controller.HasPendingHit && !previousPendingHit;
        previousPendingHit = controller.HasPendingHit;
        // 玩家。
LeonController player = controller.Player;
        if (receivedReactiveHit && player != null && !HasPendingWindowPriority())
        {
            // 命中表面法线。
Vector3 hitNormal = player.transform.position - transform.position;
            if (TryBeginDropFromHit(hitNormal))
            {
                return;
            }
        }

        if (controller.CurrentState == VillagerState.Chase && player != null)
        {
            if (TryReadPlatformExitClearance(out _))
            {
                ClearPendingRoute();
                ClearPendingDropPlan();
                return;
            }

            if (TryResolveSamePlatformPursuitDestination(player.transform.position, out _))
            {
                ClearPendingRoute();
                ClearPendingDropPlan();
                return;
            }

            if (TryResolveSpecialPursuitDestination(player.transform.position, out _))
            {
                if (hasPendingDropPlan)
                {
                    TryBeginPendingDropTraversal();
                }
                else
                {
                    TryBeginPendingWindowTraversal();
                }

                // 未抵达入口或正在排队时，同样保持当前路线所有权；实际导航写入统一由 RefreshChaseDestination 负责。
                return;
            }

            ClearPendingRoute();
            ClearPendingDropPlan();
        }
        else
        {
            ClearPendingRoute();
            ClearPendingDropPlan();
        }

        // 移动方向。
Vector3 movementDirection;
        if (agent != null)
        {
            movementDirection = agent.desiredDirection;
        }
        else
        {
            movementDirection = transform.forward;
        }

        if (!HasPendingWindowPriority())
        {
            TryBeginDropAfterMovement(movementDirection);
        }
    }

    /// <summary>HasPendingWindowPriority 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.LateUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasPendingWindowPriority()
    {
        return hasPendingPlan && pendingPlan.route != null;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversal.CancelTraversal(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        FinishLadderTraversal(false, "角色停用");
        if (controller != null)
        {
            var site = VillageLadderSite.GetAssignment(controller);
            if (site != null)
            {
                site.ClearAssignment(controller, "角色停用");
            }
        }

        blockedWindows.Clear();
        windowRetreatUntil = 0f;
        ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Deactivate);
        CancelTraversal(false);
    }

    /// <summary>TryResolveSpecialPursuitDestination 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.LateUpdate()、VillagerController.RefreshChaseDestination()；下游（含内部回调体） EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="destination">目的地。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolveSpecialPursuitDestination(Vector3 targetPosition, out Vector3 destination)
    {
        if (controller != null && controller.HasSiegeBreachOrder)
        {
            destination = default;
            return false;
        }
        if (TryReadLadderApproach(out destination))
        {
            return true;
        }
        // 普通更新和帧尾执行必须共用同一优先级，不能互相清除路线。
        // 仅当外平台角色已逼近窗台准备翻回屋内时才保留原回屋计划，否则优先跳落追击地面目标。
        bool closeToPendingWindow = hasPendingPlan && pendingPlan.route != null
            && HorizontalDistance(transform.position, pendingPlan.sourceGround) <= 1.2f;
        if (closeToPendingWindow && IsCurrentActorOnPlatform() && TryResolvePursuitDestination(targetPosition, out destination))
        {
            ClearPendingDropPlan();
            return true;
        }

        if (TryResolveDropPursuitDestination(targetPosition, out destination))
        {
            ClearPendingRoute();
            ClearRegionWindowRoute();
            return true;
        }

        if (TryResolvePursuitDestination(targetPosition, out destination))
        {
            windowRetreatUntil = 0f;
            return true;
        }
        if (Time.time < windowRetreatUntil && controller != null && controller.CurrentState == VillagerState.Chase)
        {
            destination = windowRetreatPoint;
            return true;
        }
        return false;
    }

    /// <summary>ClearRegionWindowRoute 的业务处理入口；本方法直接依赖 RegionWindowRouting.ClearValue()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） RegionWindowRouting.ClearValue()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearRegionWindowRoute()
    {
        regionSelectWindow.ClearValue();
        generalSelectWindow.ClearValue();
    }

    internal void ResetStalledPursuitPlans()
    {
        if (IsActive)
        {
            return;
        }

        ClearPendingRoute();
        ClearPendingDropPlan();
        ClearRegionWindowRoute();
        windowRetreatUntil = 0f;
    }

    internal void CancelPendingSiegeDetour()
    {
        ClearPendingRoute();
        ClearPendingDropPlan();
        ClearRegionWindowRoute();
        windowRetreatUntil = 0f;
    }
    /// <summary>TryResolvePursuitDestination 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、RegionWindowRouting.Select(VillagerController, Vector3, bool, bool, out EnemyWindowTraversalPlan)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="pursuitDestination">追击目的地。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolvePursuitDestination(Vector3 targetPosition, out Vector3 pursuitDestination)
    {
        pursuitDestination = targetPosition;
        CacheComponents();
        if (controller == null || controller.IsDead || controller.IsQTEControlled || controller.HasPendingCrawl || controller.HasPendingStandUp || IsMovementProtected)
        {
            ClearPendingRoute();
            return false;
        }

        if (HouseThreeRegionNavigation.Current == null)
        {
            ClearPendingRoute();
            return false;
        }

        // 是否外部。
bool outside = IsCurrentActorOnPlatform();
        // 是否目标外部。
bool targetOutside = ResolveTargetPlatformState(trackedPlatformRegionId, targetPosition);
        if (outside || targetOutside)
        {
            // 接近点。
var approachPoint = HouseThreeRegionNavigation.Current.ReadChaseApproachPoint(targetPosition, targetOutside);
            if (approachPoint.node != null)
            {
                targetPosition = approachPoint.position;
            }
        }

        if ((!outside && !targetOutside) || !regionSelectWindow.Select(controller, targetPosition, outside, targetOutside, blockedWindows, out var /* 中文含义：方案。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ plan))
        {
            if (generalSelectWindow.SelectGeneral(controller, targetPosition, blockedWindows, out var otherPlan))
            {
                return RegisterWindowEntryPoint(otherPlan, out pursuitDestination);
            }
            ClearPendingRoute();
            return false;
        }

        return RegisterWindowEntryPoint(plan, out pursuitDestination);
    }

    /// <summary>区域选择窗口。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly RegionWindowRouting regionSelectWindow = new RegionWindowRouting();
    private readonly RegionWindowRouting generalSelectWindow = new RegionWindowRouting();
    private readonly Dictionary<EnemyWindowTraversalRoute, float> blockedWindows = new();
    private float windowApproachBest;
    private float windowProgressAt;
    private VillagerController windowQueueLeader;
    private Vector3 windowQueueLeaderPosition;
    private int windowQueueIndex = -1;
    private Vector3 windowRetreatPoint;
    private float windowRetreatUntil;
    /// <summary>RegisterWindowEntryPoint 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversalRoute.CancelWait(VillagerController)、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、object.ReferenceEquals(object, object)、VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversalRoute.CancelWait(VillagerController)、EnemyWindowTraversalRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyWindowTraversalPlan)、object.ReferenceEquals(object, object)、VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bestPlan">最佳方案。</param>
    /// <param name="pursuitDestination">追击目的地。输出参数，由本入口写入并交给调用方。</param>
    private bool RegisterWindowEntryPoint(EnemyWindowTraversalPlan bestPlan, out Vector3 pursuitDestination)
    {
        if (hasPendingPlan && pendingPlan.route != bestPlan.route)
        {
            // 判空前缓存的待处理方案的路线。
global::EnemyWindowTraversalRoute receiverValue7 = pendingPlan.route;
            if (!global::System.Object.ReferenceEquals(receiverValue7, null))
            {
                receiverValue7.CancelWait(controller);
            }
        }

        if (!hasPendingPlan || pendingPlan.route != bestPlan.route || pendingPlan.sourceSideSign != bestPlan.sourceSideSign)
        {
            windowApproachBest = HorizontalDistance(transform.position, bestPlan.sourceGround);
            windowProgressAt = Time.time;
            windowQueueLeader = null;
            windowQueueIndex = -1;
        }
        pendingPlan = bestPlan;
        hasPendingPlan = true;
        // 等待中点。
Vector3 waitingPoint = bestPlan.route.RegisterAndGetWaitingPoint(controller, bestPlan);
        if (HorizontalDistance(waitingPoint, bestPlan.sourceGround) < 0.35f
            && HorizontalDistance(transform.position, bestPlan.sourceGround) < 1.15f
            && bestPlan.route.CanEnterWindowQueue(controller)
            && controller.CanMoveDirectlyToNavigationPoint(bestPlan.sourceGround, true))
        {
            // 队首已与窗台处于同一踏面，保留真实入口，避免投影回较远的寻路节点。
            pursuitDestination = bestPlan.sourceGround;
            return true;
        }
        if (controller.TryResolveReachableNavigationPoint(waitingPoint, out Vector3 /* 中文含义：解算后的等待中点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ resolvedWaitingPoint) && HorizontalDistance(resolvedWaitingPoint, waitingPoint) <= maximumRouteProjectionDistance)
        {
            pursuitDestination = resolvedWaitingPoint;
        }
        else
        {
            if (bestPlan.route.CanEnterWindowQueue(controller))
            {
                pursuitDestination = bestPlan.sourceGround;
            }
            else
            {
                // 队尾超出踏面时留在当前支撑点，避免多个角色同时挤向窗沿。
                pursuitDestination = transform.position;
            }
        }

        return true;
    }

    /// <summary>TryResolveDropPursuitDestination 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.CancelWait(VillagerController)、EnemyPlatformDropRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyPlatformDropPlan)、EnemyPlatformDropRoute.TryBuildPlan(VillagerController, Vector3, out EnemyPlatformDropPlan)、EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.ShouldPursueByDrop(Vector3) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyPlatformDropRoute.CancelWait(VillagerController)、EnemyPlatformDropRoute.RegisterAndGetWaitingPoint(VillagerController, EnemyPlatformDropPlan)、EnemyPlatformDropRoute.TryBuildPlan(VillagerController, Vector3, out EnemyPlatformDropPlan)、EnemyWindowTraversal.CacheComponents()、EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.ShouldPursueByDrop(Vector3) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="pursuitDestination">追击目的地。输出参数，由本入口写入并交给调用方。</param>
    public bool TryResolveDropPursuitDestination(Vector3 targetPosition, out Vector3 pursuitDestination)
    {
        pursuitDestination = targetPosition;
        CacheComponents();
        if (controller == null || controller.IsDead || controller.IsQTEControlled || controller.HasPendingCrawl
            || controller.HasPendingStandUp || IsActive || !ShouldPursueByDrop(targetPosition))
        {
            ClearPendingDropPlan();
            return false;
        }

        EnemyPlatformDropPlan bestPlan = default;
        Vector3 bestApproach = default;
        float bestScore = float.MaxValue;
        foreach (EnemyPlatformDropRoute route in EnemyPlatformDropRoute.ActiveRoutes)
        {
            if (route == null || !route.isActiveAndEnabled || !route.TryBuildPlan(controller, targetPosition, out EnemyPlatformDropPlan plan))
            {
                continue;
            }

            float allowedSourceLevelDiff = Mathf.Max(route.maximumSourceLevelDifference, 1.45f);
            if (!controller.TryResolveReachableNavigationPoint(plan.sourceGround, out Vector3 approach)
                || HorizontalDistance(approach, plan.sourceGround) > maximumRouteProjectionDistance
                || Mathf.Abs(approach.y - plan.sourceGround.y) > allowedSourceLevelDiff)
            {
                continue;
            }

            bool keepsConfirmedRoute = hasPendingDropPlan && pendingDropPlan.route == route;
            // 入口与实际起跳踏面分开。只有同一片外平台才允许沿网格绕过屋脊和木桥拐角。
            var regions = HouseThreeRegionNavigation.Current;
            bool sharesPlatformSurface = regions != null
                && regions.TreadRegion(transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform
                && regions.TreadRegion(approach) == VillageTraversalRegion.SecondFloorOuterPlatform;
            if (!keepsConfirmedRoute && !sharesPlatformSurface && !controller.CanMoveDirectlyToNavigationPoint(approach, true))
            {
                continue;
            }

            int activeQueueLoad = route.ReadActiveQueueLoad(controller);
            float queueDiversionPenalty = activeQueueLoad * 1.8f;
            float score = HorizontalDistance(transform.position, approach) + HorizontalDistance(targetPosition, plan.landingGround) + queueDiversionPenalty;
            if (keepsConfirmedRoute)
            {
                score -= route.queueSpacing;
            }
            if (score < bestScore)
            {
                bestPlan = plan;
                bestApproach = approach;
                bestScore = score;
            }
        }

        if (bestPlan.route == null)
        {
            ClearPendingDropPlan();
            return false;
        }

        if (hasPendingDropPlan && pendingDropPlan.route != bestPlan.route && pendingDropPlan.route != null)
        {
            pendingDropPlan.route.CancelWait(controller);
        }

        pendingDropPlan = bestPlan;
        pendingDropApproach = bestApproach;
        hasPendingDropPlan = true;
        pursuitDestination = bestApproach;
        Vector3 waitingPoint = bestPlan.route.RegisterAndGetWaitingPoint(controller, bestPlan, bestApproach);
        float allowedWaitingLevelDiff = Mathf.Max(bestPlan.route.maximumSourceLevelDifference, 1.45f);
        if (waitingPoint != bestApproach
            && controller.TryResolveReachableNavigationPoint(waitingPoint, out Vector3 resolvedWaitingPoint)
            && HorizontalDistance(resolvedWaitingPoint, waitingPoint) <= maximumRouteProjectionDistance
            && Mathf.Abs(resolvedWaitingPoint.y - bestPlan.sourceGround.y) <= allowedWaitingLevelDiff)
        {
            pursuitDestination = resolvedWaitingPoint;
        }
        return true;
    }

    /// <summary>TryBeginPendingDropTraversal 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.RequestJumpLandOccupancy(EnemyPlatformDropRoute)、EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.LateUpdate()；下游（含内部回调体） EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.RequestJumpLandOccupancy(EnemyPlatformDropRoute)、EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryBeginPendingDropTraversal()
    {
        if (IsActive || !hasPendingDropPlan || pendingDropPlan.route == null)
        {
            return IsActive;
        }

        EnemyPlatformDropRoute route = pendingDropPlan.route;
        float captureDistance = route.captureDistance;
        bool agentStoppedAtApproach = agent != null
            && (agent.CurrentPathState == PathAgentState.Arrived || agent.CurrentPathState == PathAgentState.Failed)
            && HorizontalDistance(agent.Destination, pendingDropApproach) <= 0.45f;
        if (agentStoppedAtApproach)
        {
            // 寻路代理提前停止或被同伴挤偏时，将捕获半径放宽至超过双敌最小间距，避免死锁在房檐。
            float widenedCapture = 0.95f;
            if (agent != null && agent.waypointReachedRadius > widenedCapture)
            {
                widenedCapture = agent.waypointReachedRadius;
            }
            captureDistance = Mathf.Max(captureDistance, widenedCapture);
        }

        float allowedLevelDiff = Mathf.Max(route.maximumSourceLevelDifference, 1.45f);
        float horizontalToApproach = HorizontalDistance(transform.position, pendingDropApproach);
        bool canReachApproach = controller.CanMoveDirectlyToNavigationPoint(pendingDropApproach, true)
            || (agentStoppedAtApproach && horizontalToApproach <= 0.95f);
        if (horizontalToApproach > captureDistance
            || Mathf.Abs(transform.position.y - pendingDropPlan.sourceGround.y) > allowedLevelDiff
            || !canReachApproach
            || !RequestJumpLandOccupancy(route))
        {
            return false;
        }

        EnemyPlatformDropPlan plan = pendingDropPlan;
        currentDropRoute = route;
        hasPendingDropPlan = false;
        pendingDropPlan = default;
        pendingDropApproach = default;
        if (TryBeginDrop(plan.travelDirection, plan.landingProbe))
        {
            return true;
        }

        route.ReleaseTraversal(jumpLandToken, GameArchitecture.ReleaseReason.Cancel);
        currentDropRoute = null;
        return false;
    }

    /// <summary>ShouldPursueByDrop 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyWindowTraversal.InitializePlatformRegionState()、EnemyWindowTraversal.IsCurrentActorOnPlatform()、EnemyWindowTraversal.ResolveTargetPlatformState(string, Vector3)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">目标位置。</param>
    private bool ShouldPursueByDrop(Vector3 targetPosition)
    {
        InitializePlatformRegionState();
        // 是否目标事件平台。
bool targetOnPlatform;
        if (hasPlatformRegionState)
        {
            targetOnPlatform = ResolveTargetPlatformState(trackedPlatformRegionId, targetPosition);
        }
        else
        {
            targetOnPlatform = EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(targetPosition);
        }

        float levelDifference = transform.position.y - targetPosition.y;
        return IsCurrentActorOnPlatform() && (!targetOnPlatform || levelDifference >= minimumDropHeight)
            && levelDifference >= minimumDropHeight * .75f;
    }

    /// <summary>UpdateSpecialTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.BeginDropFallAnimation()、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.LoopCurrentClipIfNeeded()、EnemyWindowTraversal.UpdateDropMotion(float)、EnemyWindowTraversal.UpdateWindowTraversal()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.Update()；下游（含内部回调体） EnemyWindowTraversal.BeginDropFallAnimation()、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.LoopCurrentClipIfNeeded()、EnemyWindowTraversal.UpdateDropMotion(float)、EnemyWindowTraversal.UpdateWindowTraversal()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool UpdateSpecialTraversal()
    {
        if (!IsActive)
        {
            return false;
        }

        if (controller == null || controller.IsDead)
        {
            CancelTraversal(false);
            return false;
        }

        // 本帧时间间隔。
float deltaTime = Mathf.Max(0f, Time.deltaTime);
        switch (phase)
        {
            case TraversalPhase.Window:
                UpdateWindowTraversal();
                break;
            case TraversalPhase.DropStart:
                UpdateDropMotion(deltaTime);
                if (Time.time - phaseStartedTime >= dropStartDuration)
                {
                    BeginDropFallAnimation();
                }

                break;
            case TraversalPhase.DropFall:
                UpdateDropMotion(deltaTime);
                if (nativeDropFall == null)
                {
                    LoopCurrentClipIfNeeded();
                }
                break;
            case TraversalPhase.DropLanding:
                if (Time.time - phaseStartedTime >= dropLandingDuration)
                {
                    CompleteTraversal();
                }

                break;
        }

        return IsActive;
    }

    /// <summary>TryBeginPendingWindowTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool)、EnemyWindowTraversal.RequestWindowOccupancy(EnemyWindowTraversalRoute)、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.LateUpdate()；下游（含内部回调体） EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.HorizontalDistance(Vector3, Vector3)、EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool)、EnemyWindowTraversal.RequestWindowOccupancy(EnemyWindowTraversalRoute)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryBeginPendingWindowTraversal()
    {
        if (IsActive || !hasPendingPlan || pendingPlan.route == null)
        {
            return IsActive;
        }
        if (controller == null)
        {
            ClearPendingRoute();
            return false;
        }

        EnemyWindowTraversalRoute route = pendingPlan.route;
        float distance = HorizontalDistance(transform.position, pendingPlan.sourceGround);
        if (distance < windowApproachBest - .12f)
        {
            windowApproachBest = distance;
            windowProgressAt = Time.time;
        }

        bool queued = !route.CanEnterWindowQueue(controller);
        float captureDistance = Mathf.Max(0.45f, route.captureDistance);
        if (agent != null && agent.CurrentPathState == PathAgentState.Arrived
            && controller.CanMoveDirectlyToNavigationPoint(pendingPlan.sourceGround, true))
        {
            captureDistance = Mathf.Max(captureDistance, maximumRouteProjectionDistance);
        }
        else if (agent != null && agent.CurrentPathState == PathAgentState.Failed && !queued
            && distance <= .75f && controller.CanMoveDirectlyToNavigationPoint(pendingPlan.sourceGround, true))
        {
            captureDistance = .75f;
        }

        if (queued)
        {
            bool hasAlternativeWindow = HasUnblockedAlternativeWindow(route);
            if (route.TryReadQueueLeader(controller, out VillagerController leader, out int queueIndex))
            {
                bool changedLeader = leader != windowQueueLeader;
                bool advancedInQueue = windowQueueIndex >= 0 && queueIndex >= 0 && queueIndex < windowQueueIndex;
                bool leaderMoved = (leader.transform.position - windowQueueLeaderPosition).sqrMagnitude >= .08f * .08f;
                // 晋升队首或更换领队时必定重置等待计时；仅前人微移受是否有替代空闲窗约束。
                if (changedLeader || advancedInQueue || (!hasAlternativeWindow && leaderMoved))
                {
                    // 前人正在接近或通过窗口时，队伍本身也在取得进展。
                    windowProgressAt = Time.time;
                    windowQueueLeaderPosition = leader.transform.position;
                }
                else if (leaderMoved)
                {
                    windowQueueLeaderPosition = leader.transform.position;
                }
                windowQueueLeader = leader;
                windowQueueIndex = queueIndex;
            }
            return DelayPendingWindow("窗口队首或占用尚未释放", distance);
        }
        if (distance > captureDistance)
        {
            return DelayPendingWindow("无法接到翻窗起点", distance);
        }
        if (Mathf.Abs(transform.position.y - pendingPlan.sourceGround.y) > route.maximumSourceLevelDifference)
        {
            return DelayPendingWindow("翻窗起点不在当前踏面", distance);
        }
        if (!TryResolveNativeWindowLanding(ref pendingPlan))
        {
            return DelayPendingWindow("翻窗落点受阻", distance);
        }
        if (!RequestWindowOccupancy(route))
        {
            return DelayPendingWindow("翻窗占用未获准", distance);
        }

        if (enableTraversalDiagnostic)
        {
            ReportTraversalDiagnostic("开始翻窗", $"路线={route.name}", true);
        }
        windowRetreatUntil = 0f;
        BeginWindowTraversal(pendingPlan);
        return true;
    }

    private bool DelayPendingWindow(string reason, float distance)
    {
        bool pathFailed = agent != null && agent.CurrentPathState == PathAgentState.Failed;
        bool landingBlocked = string.Equals(reason, "翻窗落点受阻", System.StringComparison.Ordinal);
        bool queueWaiting = string.Equals(reason, "窗口队首或占用尚未释放", System.StringComparison.Ordinal);
        float waitLimit = 1.25f;
        if (landingBlocked)
        {
            waitLimit = 1.10f;
        }
        else if (queueWaiting)
        {
            if (HasUnblockedAlternativeWindow(pendingPlan.route))
            {
                waitLimit = 0.90f;
            }
            else
            {
                waitLimit = 1.50f;
            }
        }

        if (Time.time - windowProgressAt < waitLimit || distance > 3f && !pathFailed)
        {
            return false;
        }

        var plan = pendingPlan;
        bool isGeneralWindow = string.IsNullOrEmpty(plan.route.platformRegionId);
        bool hasAlternativeWindow = HasUnblockedAlternativeWindow(plan.route);
        if (!isGeneralWindow && !hasAlternativeWindow)
        {
            // 同区域没有其他可用且空闲的替代窗时，禁止拉黑最后一个可达窗口，也禁止向后倒退堵塞通道。
            windowProgressAt = Time.time;
            windowRetreatUntil = 0f;
            return false;
        }

        float blockDuration = 2.5f;
        if (isGeneralWindow)
        {
            blockDuration = 6.0f;
        }
        else if (landingBlocked)
        {
            blockDuration = 3.5f;
        }
        blockedWindows[plan.route] = Time.time + blockDuration;
        if (enableTraversalDiagnostic)
        {
            ReportTraversalDiagnostic("重新选择通路", $"路线={plan.route.name}，原因={reason}，距离={distance:F2}", true);
        }

        windowRetreatUntil = 0f;
        ClearPendingRoute();
        ClearRegionWindowRoute();
        return false;
    }

    private bool HasUnblockedAlternativeWindow(EnemyWindowTraversalRoute excludedRoute)
    {
        if (excludedRoute == null || string.IsNullOrEmpty(excludedRoute.platformRegionId))
        {
            return false;
        }

        bool sourceOnPlatform = IsCurrentActorOnPlatform();
        IReadOnlyList<EnemyWindowTraversalRoute> routes = EnemyWindowTraversalRoute.ActiveRoutes;
        for (int i = 0; i < routes.Count; i++)
        {
            EnemyWindowTraversalRoute candidate = routes[i];
            if (candidate == null || candidate == excludedRoute || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (candidate.platformRegionId != excludedRoute.platformRegionId)
            {
                continue;
            }

            if (candidate.windowPoint == null || candidate.windowPoint.IsBarricaded)
            {
                continue;
            }

            if (blockedWindows.TryGetValue(candidate, out float blockedUntil) && Time.time < blockedUntil)
            {
                continue;
            }

            if (!candidate.TryBuildPlanFromSide(controller, sourceOnPlatform, out EnemyWindowTraversalPlan candidatePlan, true))
            {
                continue;
            }

            if (candidate.ReadOccupiedAndWaitingCount(controller) == 0 && IsWindowLandingClear(candidatePlan))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>RecordPlatformSupportBeforeMovement 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.IsCurrentActorOnPlatform()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.Update()；下游（含内部回调体） EnemyWindowTraversal.IsCurrentActorOnPlatform()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool TryFindWindowRetreat(EnemyWindowTraversalPlan plan, float distance)
    {
        var region = HouseThreeRegionNavigation.Current;
        if (region == null || distance > 3f)
        {
            return false;
        }

        Vector3 origin = transform.position;
        Vector3 away = -plan.travelDirection;
        Vector3 lateral = Vector3.Cross(Vector3.up, away);
        VillageTraversalRegion localRegion = region.TreadRegion(origin);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector3 offset = away;
            if (attempt == 1)
            {
                offset = (away + lateral).normalized;
            }
            else if (attempt == 2)
            {
                offset = (away - lateral).normalized;
            }

            Vector3 target = origin + offset * 1.25f;
            if (!controller.TryResolveReachableNavigationPoint(target, out Vector3 resolved)
                || HorizontalDistance(target, resolved) > .35f || Mathf.Abs(origin.y - resolved.y) > .28f
                || region.TreadRegion(resolved) != localRegion || !controller.CanMoveDirectlyToNavigationPoint(resolved, true))
            {
                continue;
            }
            windowRetreatPoint = resolved;
            return true;
        }
        return false;
    }

    public bool TryBeginDropAfterMovement(Vector3 movementDirection)
    {
        return CanBeginDrop() && TryBeginUnsupportedDrop();
    }

    internal bool TryBeginUnsupportedDrop()
    {
        if (IsActive || controller == null || controller.IsDead || controller.IsQTEControlled
            || controller.OwnsImpactGroundMotion || controller.IsNavigationTraversalProtected
            || controller.HasPendingCrawl || controller.HasPendingStandUp || controller.HasPhysicalEnemySupport())
        {
            return false;
        }

        return TryBeginDrop(transform.forward, transform.position, true);
    }

    public bool TryBeginDropFromHit(Vector3 hitNormal)
    {
        if (IsActive || !CanBeginDrop() || !IsCurrentActorOnPlatform())
        {
            return false;
        }

        // 推开方向。
Vector3 pushDirection = -hitNormal;
        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        pushDirection.Normalize();
        // 边缘探测。
Vector3 edgeProbe = transform.position + pushDirection * edgeProbeDistance;
        if (hasPlatformRegionState && !string.IsNullOrWhiteSpace(trackedPlatformRegionId) && EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(trackedPlatformRegionId, edgeProbe))
        {
            return false;
        }

        if (HasGroundSupport(edgeProbe, supportProbeDistance))
        {
            return false;
        }

        return TryBeginDrop(pushDirection, edgeProbe);
    }

    /// <summary>CancelForDeath 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.CancelTraversal(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyWindowTraversal.CancelTraversal(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelForDeath()
    {
        FinishLadderTraversal(false, "角色死亡");
        CancelTraversal(false);
    }

    /// <summary>BeginWindowTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.PlayTraversalClip(AnimationClip)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginPendingWindowTraversal()；下游（含内部回调体） EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.PlayTraversalClip(AnimationClip)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="plan">方案。</param>
    private void BeginWindowTraversal(EnemyWindowTraversalPlan plan)
    {
        currentRoute = plan.route;
        hasPendingPlan = false;
        if (plan.route != null)
        {
            // 计算前缓存的左侧方案的来源侧边符号。
int leftOperand11 = plan.sourceSideSign;
            // 分支选出的1。
int selectedValue10;
            if (plan.route.platformSideSign >= 0)
            {
                selectedValue10 = 1;
            }
            else
            {
                selectedValue10 = -1;
            }

            pendingWindowDestinationOnPlatform = leftOperand11 != (selectedValue10);
        }
        else
        {
            pendingWindowDestinationOnPlatform = false;
        }

        if (plan.route != null && !string.IsNullOrEmpty(plan.route.platformRegionId))
        {
            trackedPlatformRegionId = plan.route.platformRegionId;
        }

        motionStart = transform.position;
        motionDestination = plan.destinationGround;
        motionStartRotation = transform.rotation;
        motionRotation = plan.facingRotation;
        // 中心。
Vector3 center = plan.route.windowPoint.OpeningCenterWorld;
        // 窗台顶部。
float sillTop = center.y - plan.route.windowPoint.openingHeight * 0.5f;
        motionControl = (motionStart + motionDestination) * 0.5f;
        // 期望顶点。
float desiredApex = Mathf.Max(Mathf.Max(motionStart.y, motionDestination.y), sillTop + windowArcClearance);
        motionControl.y = desiredApex * 2f - (motionStart.y + motionDestination.y) * 0.5f;
        BeginTraversalState();
        phase = TraversalPhase.Window;
        phaseStartedTime = Time.time;
        if (BeginNativeWindow(plan))
        {
            return;
        }

        // 分支选出的敌人通行动画资源库的生效的窗口攀爬穿过。
global::UnityEngine.AnimationClip selectedValue13;
        if (EnemyTraversalAnimationLibrary.Active != null)
        {
            selectedValue13 = EnemyTraversalAnimationLibrary.Active.windowClimbThrough;
        }
        else
        {
            selectedValue13 = null;
        }

        PlayTraversalClip(selectedValue13);
    }

    /// <summary>UpdateWindowTraversal 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、EnemyWindowTraversal.CompleteTraversal()、Mathf.Clamp01(float)、Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateSpecialTraversal()；下游（含内部回调体） EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、EnemyWindowTraversal.CompleteTraversal()、Mathf.Clamp01(float)、Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateWindowTraversal()
    {
        if (nativeWindowMixer.IsValid())
        {
            UpdateNativeWindow();
            return;
        }

        // 持续时间。
float duration;
        if (currentRoute != null)
        {
            duration = Mathf.Max(0.1f, currentRoute.traversalDuration);
        }
        else
        {
            duration = 1.07f;
        }

        // 原始进度。
float rawProgress = Mathf.Clamp01((Time.time - phaseStartedTime) / duration);
        // 运动进度。
float motionProgress = Mathf.InverseLerp(windowMotionStartNormalizedTime, Mathf.Max(windowMotionStartNormalizedTime + 0.01f, windowMotionEndNormalizedTime), rawProgress);
        // 平滑后的进度。
float smoothedProgress = motionProgress * motionProgress * (3f - 2f * motionProgress);
        // 剩余。
float remaining = 1f - smoothedProgress;
        // 位置。
Vector3 position = remaining * remaining * motionStart + 2f * remaining * smoothedProgress * motionControl + smoothedProgress * smoothedProgress * motionDestination;
        // 旋转持续时间。
float rotationDuration = Mathf.Max(0.01f, animationBlendDuration);
        // 旋转进度。
float rotationProgress = Mathf.Clamp01((Time.time - phaseStartedTime) / rotationDuration);
        // 旋转。
Quaternion rotation = Quaternion.Slerp(motionStartRotation, motionRotation, rotationProgress);
        if (CommitTraversalPose(position, rotation))
        {
            FractureWindowOnCrossing(position);
        }
        if (rawProgress >= 1f)
        {
            CommitTraversalPose(motionDestination, motionRotation);
            traversalHandoffPosition = motionDestination;
            traversalHandoffRotation = motionRotation;
            traversalHandoffUntil = Time.time + 0.02f;
            traversalHandoffResumePending = true;
            CompleteTraversal();
        }
    }

    private void FractureWindowOnCrossing(Vector3 position)
    {
        if (currentRoute == null || currentRoute.windowPoint == null)
        {
            return;
        }

        WindowTraversalPoint window = currentRoute.windowPoint;
        if (window.fracture == null || window.fracture.IsBroken)
        {
            return;
        }

        Vector3 direction = motionRotation * Vector3.forward;
        if (Vector3.Dot(position - window.OpeningCenterWorld, direction) >= -0.18f)
        {
            window.fracture.Fracture(window.OpeningCenterWorld, direction);
        }
    }

    /// <summary>TryBeginDrop 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.PlayTraversalClip(AnimationClip)、EnemyWindowTraversal.TryFindDropLanding(Vector3, out Vector3)、Mathf.Max(float, float)、Mathf.Sqrt(float)、Quaternion.LookRotation(Vector3, Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginDropAfterMovement(Vector3)、EnemyWindowTraversal.TryBeginDropFromHit(Vector3)、EnemyWindowTraversal.TryBeginPendingDropTraversal()；下游（含内部回调体） EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.PlayTraversalClip(AnimationClip)、EnemyWindowTraversal.TryFindDropLanding(Vector3, out Vector3)、Mathf.Max(float, float)、Mathf.Sqrt(float)、Quaternion.LookRotation(Vector3, Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontalDirection">水平方向。</param>
    /// <param name="landingProbePosition">落地探测位置。</param>
    private bool TryBeginDrop(Vector3 horizontalDirection, Vector3 landingProbePosition, bool alreadyAirborne = false)
    {
        float dropHeight = minimumDropHeight;
        if (alreadyAirborne)
        {
            dropHeight = .12f;
        }

        if (!TryFindDropLanding(landingProbePosition, dropHeight, out Vector3 /* 中文含义：落地位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ landingPosition))
        {
            return false;
        }

        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude <= 0.001f)
        {
            horizontalDirection = transform.forward;
        }

        horizontalDirection.Normalize();
        // 重力。
float gravity = Mathf.Max(0.01f, dropGravity);
        // 竖直掉落。
float verticalDrop = Mathf.Max(0f, transform.position.y - landingPosition.y);
        // 下落持续时间。
float fallDuration = (dropInitialUpwardSpeed + Mathf.Sqrt(dropInitialUpwardSpeed * dropInitialUpwardSpeed + 2f * gravity * verticalDrop)) / gravity;
        // 水平偏移量。
Vector3 horizontalOffset = landingPosition - transform.position;
        horizontalOffset.y = 0f;
        ClearPendingRoute();
        motionRotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
        dropHorizontalVelocity = horizontalOffset / Mathf.Max(0.1f, fallDuration);
        dropVerticalSpeed = dropInitialUpwardSpeed;
        dropLandingPosition = landingPosition;
        BeginTraversalState();
        if (alreadyAirborne)
        {
            dropVerticalSpeed = 0f;
            dropHorizontalVelocity = Vector3.zero;
            BeginDropFallAnimation();
            return true;
        }

        phase = TraversalPhase.DropStart;
        phaseStartedTime = Time.time;
        // 分支选出的敌人通行动画资源库的生效的掉落跳跃起始。
global::UnityEngine.AnimationClip selectedValue15;
        if (EnemyTraversalAnimationLibrary.Active != null)
        {
            selectedValue15 = EnemyTraversalAnimationLibrary.Active.dropJumpStart;
        }
        else
        {
            selectedValue15 = null;
        }

        if (nativeDropStart != null)
        {
            PlayTraversalClip(nativeDropStart, dropStartDuration);
        }
        else
        {
            PlayTraversalClip(selectedValue15);
        }
        return true;
    }

    /// <summary>BeginDropFallAnimation 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.PlayTraversalClip(AnimationClip)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateSpecialTraversal()；下游（含内部回调体） EnemyWindowTraversal.PlayTraversalClip(AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginDropFallAnimation()
    {
        phase = TraversalPhase.DropFall;
        phaseStartedTime = Time.time;
        // 分支选出的敌人通行动画资源库的生效的掉落下落循环。
global::UnityEngine.AnimationClip selectedValue16;
        if (EnemyTraversalAnimationLibrary.Active != null)
        {
            selectedValue16 = EnemyTraversalAnimationLibrary.Active.dropFallLoop;
        }
        else
        {
            selectedValue16 = null;
        }

        if (nativeDropFall != null)
        {
            PlayTraversalClip(nativeDropFall, 0f);
        }
        else
        {
            PlayTraversalClip(selectedValue16);
        }
    }

    /// <summary>UpdateDropMotion 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.BeginDropLanding()、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateSpecialTraversal()；下游（含内部回调体） EnemyWindowTraversal.BeginDropLanding()、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="deltaTime">本帧时间间隔。</param>
    private void UpdateDropMotion(float deltaTime)
    {
        dropVerticalSpeed -= dropGravity * deltaTime;
        // 位置。
Vector3 position = transform.position;
        position += dropHorizontalVelocity * deltaTime;
        position.y += dropVerticalSpeed * deltaTime;
        if (dropVerticalSpeed <= 0f && position.y <= dropLandingPosition.y
            || Time.time - phaseStartedTime >= 1.65f)
        {
            CommitTraversalPose(dropLandingPosition, motionRotation);
            BeginDropLanding();
            return;
        }

        CommitTraversalPose(position, motionRotation);
    }

    /// <summary>BeginDropLanding 的业务处理入口；本方法直接依赖 EnemyWindowTraversal.PlayTraversalClip(AnimationClip)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateDropMotion(float)；下游（含内部回调体） EnemyWindowTraversal.PlayTraversalClip(AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginDropLanding()
    {
        phase = TraversalPhase.DropLanding;
        phaseStartedTime = Time.time;
        // 分支选出的敌人通行动画资源库的生效的掉落落地。
global::UnityEngine.AnimationClip selectedValue17;
        if (EnemyTraversalAnimationLibrary.Active != null)
        {
            selectedValue17 = EnemyTraversalAnimationLibrary.Active.dropLanding;
        }
        else
        {
            selectedValue17 = null;
        }

        if (nativeDropLanding != null)
        {
            PlayTraversalClip(nativeDropLanding, dropLandingDuration);
        }
        else
        {
            PlayTraversalClip(selectedValue17);
        }
    }

    /// <summary>BeginTraversalState 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyWindowTraversal.CacheComponents()、object.ReferenceEquals(object, object)、VillagerController.PrepareForSpecialTraversal()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyWindowTraversal.CacheComponents()、object.ReferenceEquals(object, object)、VillagerController.PrepareForSpecialTraversal()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginTraversalState(bool keepMovementCollider = false)
    {
        ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Complete);
        CacheComponents();
        controller.PrepareForSpecialTraversal();
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue18 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.ReleaseCombatReservation(controller);
        }

        controller.SetMovementMode(EnemyMovementMode.TraversalCodeDriven);
        traversalMotionToken = controller.motionExecutor.CurrentToken;
        previousMovementColliderEnabled = movementCollider != null && movementCollider.enabled;
        if (movementCollider != null && !keepMovementCollider)
        {
            movementCollider.enabled = false;
        }
    }

    /// <summary>CompleteTraversal 的业务处理入口；本方法直接依赖 EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool)、EnemyWindowTraversal.StopTraversalClip()、EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateSpecialTraversal()、EnemyWindowTraversal.UpdateWindowTraversal()；下游（含内部回调体） EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.CommitTraversalPose(Vector3, Quaternion)、EnemyWindowTraversal.ReportTraversalDiagnostic(string, string, bool)、EnemyWindowTraversal.StopTraversalClip()、EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CompleteTraversal()
    {
        // 已完成路线。
EnemyWindowTraversalRoute completedRoute = currentRoute;
        // 已完成掉落路线。
EnemyPlatformDropRoute completedDropRoute = currentDropRoute;
        // 是否已完成窗口。
bool completedWindow = phase == TraversalPhase.Window;
        // 是否已完成掉落。
bool completedDrop = phase == TraversalPhase.DropStart || phase == TraversalPhase.DropFall || phase == TraversalPhase.DropLanding;
        // 是否目的地事件平台。
bool destinationOnPlatform = pendingWindowDestinationOnPlatform;
        if (completedWindow)
        {
            CommitTraversalPose(motionDestination, motionRotation);
            traversalHandoffPosition = motionDestination;
            traversalHandoffRotation = motionRotation;
            traversalHandoffUntil = Time.time + Mathf.Max(0.02f, windowHandoffDuration);
            traversalHandoffResumePending = true;
        }

        currentRoute = null;
        currentDropRoute = null;
        phase = TraversalPhase.None;
        pendingWindowDestinationOnPlatform = false;
        StopTraversalClip();
        if (movementCollider != null)
        {
            movementCollider.enabled = previousMovementColliderEnabled;
        }

        Physics.SyncTransforms();

        if (agent != null && agent.enabled)
        {
            agent.InvalidatePursuitResolutionCache();
            agent.ResetPath();
        }

        ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Complete);
        if (completedWindow && completedRoute != null)
        {
            // 延后释放窗口占用至交接完成且开始脱离落点，避免后续敌人同一帧误判落点受阻并拉黑窗口。
            delayedReleaseWindowRoute = completedRoute;
            delayedReleaseWindowToken = windowToken;
            delayedReleaseWindowOrigin = motionDestination;
            delayedReleaseWindowUntil = Time.time + Mathf.Max(0.02f, windowHandoffDuration) + 0.45f;
        }
        else if (completedRoute != null)
        {
            completedRoute.ReleaseTraversal(windowToken, GameArchitecture.ReleaseReason.Complete);
        }

        // 判空前缓存的已完成掉落路线。
global::EnemyPlatformDropRoute receiverValue21 = completedDropRoute;
        // 条件访问得到的释放通行的返回结果。
bool? optionalValue22;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            optionalValue22 = receiverValue21.ReleaseTraversal(jumpLandToken, GameArchitecture.ReleaseReason.Complete);
        }
        else
        {
            optionalValue22 = default;
        }

        if (completedRoute != null)
        {
            if (!string.IsNullOrEmpty(completedRoute.platformRegionId))
            {
                trackedPlatformRegionId = completedRoute.platformRegionId;
                hasPlatformRegionState = true;
                isOnTrackedPlatformRegion = destinationOnPlatform;
            }
            else if (HouseThreeRegionNavigation.Current != null)
            {
                isOnTrackedPlatformRegion = HouseThreeRegionNavigation.Current.ReadPlatformOwnership(transform.position, false);
            }
            if (isOnTrackedPlatformRegion)
            {
                BeginPlatformExitClearance();
                if (agent != null && agent.enabled)
                {
                    agent.RequestImmediateRepath();
                }
            }

            ClearRegionWindowRoute();
        }
        else if (completedDrop && hasPlatformRegionState)
        {
            isOnTrackedPlatformRegion = false;
        }

        if (controller == null || controller.IsDead)
        {
            return;
        }

        controller.ClearStaleSawmanIntroTauntAfterTraversal();

        if (completedDrop)
        {
            // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue23 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue23, null))
            {
                receiverValue23.InvalidatePursuitDestination(controller);
            }
        }

        if (!completedWindow)
        {
            controller.CompleteTraversalAndResumeChase();
        }

        if (completedRoute != null && enableTraversalDiagnostic)
        {
            ReportTraversalDiagnostic("完成翻窗", $"路线={completedRoute.name}，" + $"当前位置平台={isOnTrackedPlatformRegion}", true);
        }
    }

    /// <summary>CancelTraversal 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.StopTraversalClip()、EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、PathAgent.InvalidatePursuitResolutionCache() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelForDeath()、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.OnDisable()、EnemyWindowTraversal.Update()、EnemyWindowTraversal.UpdateSpecialTraversal()；下游（含内部回调体） EnemyPlatformDropRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、EnemyWindowTraversal.ClearPendingDropPlan()、EnemyWindowTraversal.ClearPendingRoute()、EnemyWindowTraversal.ClearRegionWindowRoute()、EnemyWindowTraversal.StopTraversalClip()、EnemyWindowTraversalRoute.ReleaseTraversal(TraversalToken, ReleaseReason)、PathAgent.InvalidatePursuitResolutionCache() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="resumeMovement">是否恢复执行移动。</param>
    private void TickDelayedWindowTokenRelease()
    {
        if (delayedReleaseWindowRoute == null)
        {
            return;
        }

        bool interrupted = controller == null || controller.IsDead || controller.HasPendingHit || controller.IsQTEControlled;
        if (interrupted)
        {
            ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Cancel);
            return;
        }

        if (traversalHandoffResumePending)
        {
            return;
        }

        bool movedAway = HorizontalDistance(transform.position, delayedReleaseWindowOrigin) >= 0.65f;
        if (movedAway || Time.time >= delayedReleaseWindowUntil)
        {
            ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Complete);
        }
    }

    private void ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason reason)
    {
        if (delayedReleaseWindowRoute != null)
        {
            delayedReleaseWindowRoute.ReleaseTraversal(delayedReleaseWindowToken, reason);
            delayedReleaseWindowRoute = null;
            delayedReleaseWindowToken = default;
            delayedReleaseWindowUntil = 0f;
        }
    }

    private void CancelTraversal(bool resumeMovement)
    {
        // 之前是否生效。
bool wasActive = IsActive;
        traversalHandoffResumePending = false;
        traversalHandoffUntil = float.NegativeInfinity;
        platformExitClearanceUntil = 0f;
        ReleaseDelayedWindowToken(GameArchitecture.ReleaseReason.Cancel);
        if (movementCollider != null && wasActive)
        {
            movementCollider.enabled = previousMovementColliderEnabled;
        }

        if (agent != null && agent.enabled && wasActive)
        {
            agent.InvalidatePursuitResolutionCache();
            agent.ResetPath();
        }

        if (currentRoute != null)
        {
            currentRoute.ReleaseTraversal(windowToken, GameArchitecture.ReleaseReason.Cancel);
        }

        currentRoute = null;
        if (currentDropRoute != null)
        {
            currentDropRoute.ReleaseTraversal(jumpLandToken, GameArchitecture.ReleaseReason.Cancel);
        }

        currentDropRoute = null;
        ClearPendingRoute();
        ClearPendingDropPlan();
        ClearRegionWindowRoute();
        phase = TraversalPhase.None;
        StopTraversalClip();
        if (!wasActive || controller == null || controller.IsDead)
        {
            return;
        }

        if (!resumeMovement)
        {
            if (!HasActiveLadderTraversal && !controller.IsQTEControlled
                && controller.CurrentMovementMode == EnemyMovementMode.TraversalCodeDriven)
            {
                controller.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
            }

            return;
        }

        controller.SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
    }

    /// <summary>ClearPendingRoute 的业务处理入口；本方法直接依赖 EnemyWindowTraversalRoute.CancelWait(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)、EnemyWindowTraversal.TryBeginPendingWindowTraversal()、EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3) 等共 6 处；下游（含内部回调体） EnemyWindowTraversalRoute.CancelWait(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPendingRoute()
    {
        if (hasPendingPlan)
        {
            // 判空前缓存的待处理方案的路线。
global::EnemyWindowTraversalRoute receiverValue24 = pendingPlan.route;
            if (!global::System.Object.ReferenceEquals(receiverValue24, null))
            {
                receiverValue24.CancelWait(controller);
            }
        }

        pendingPlan = default;
        hasPendingPlan = false;
    }

    /// <summary>ClearPendingDropPlan 的业务处理入口；本方法直接依赖 EnemyPlatformDropRoute.CancelWait(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyPlatformDropRoute.CancelWait(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPendingDropPlan()
    {
        if (hasPendingDropPlan)
        {
            // 判空前缓存的待处理掉落方案的路线。
global::EnemyPlatformDropRoute receiverValue25 = pendingDropPlan.route;
            if (!global::System.Object.ReferenceEquals(receiverValue25, null))
            {
                receiverValue25.CancelWait(controller);
            }
        }

        pendingDropPlan = default;
        pendingDropApproach = default;
        hasPendingDropPlan = false;
    }

    /// <summary>CanBeginDrop 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginDropAfterMovement(Vector3)、EnemyWindowTraversal.TryBeginDropFromHit(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanBeginDrop()
    {
        if (controller == null || controller.IsDead || controller.IsQTEControlled || controller.OwnsImpactGroundMotion
            || controller.HasPendingCrawl || controller.HasPendingStandUp)
        {
            return false;
        }

        return controller.CurrentState == VillagerState.Chase || controller.CurrentState == VillagerState.Staggered;
    }

    /// <summary>HasGroundSupport 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginDropAfterMovement(Vector3)、EnemyWindowTraversal.TryBeginDropFromHit(Vector3)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="distance">距离。</param>
    private bool HasGroundSupport(Vector3 position, float distance)
    {
        // 与普通追击的足底支撑尺度一致，避免木板窄缝被误认成平台边缘。
        // 支撑半径。
float supportRadius;
        if (movementCollider != null)
        {
            supportRadius = Mathf.Clamp(movementCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * 0.5f, 0.05f, 0.175f);
        }
        else
        {
            supportRadius = 0.1f;
        }

        // 命中结果。
        RaycastHit[] hits = windowHits.SphereCast(position + Vector3.up * 0.25f, supportRadius, Vector3.down, Mathf.Max(0.1f, distance) + 0.25f, ~0, QueryTriggerInteraction.Ignore, out int hitCount);
        // 当前遍历索引。
        for (int i = 0; i < hitCount; i++)
        {
            // 候选。
Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null || hits[i].normal.y < 0.45f)
            {
                continue;
            }

            return true;
        }

        // 球体起点局部嵌入坡面时，球检测可能没有有效法线；足底仍有地面就不能跳落。
        hits = windowHits.Raycast(position + Vector3.up * 0.25f, Vector3.down, Mathf.Max(0.1f, distance) + 0.25f, ~0, QueryTriggerInteraction.Ignore, out hitCount);
        // 当前遍历索引。
        for (int i = 0; i < hitCount; i++)
        {
            // 候选。
Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null || hits[i].normal.y < 0.45f || hits[i].point.y > position.y + 0.05f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>TryFindDropLanding 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probePosition">探测位置。</param>
    /// <param name="landingPosition">落地位置。输出参数，由本入口写入并交给调用方。</param>
    private bool TryFindDropLanding(Vector3 probePosition, float minimumHeight, out Vector3 landingPosition)
    {
        landingPosition = default;
        // 原点。
Vector3 origin = probePosition + Vector3.up * 0.5f;
        // 命中结果。
        RaycastHit[] hits = windowHits.Raycast(origin, Vector3.down, maximumDropProbeDistance, ~0, QueryTriggerInteraction.Ignore, out int hitCount);
        // 最近距离。
float nearestDistance = float.MaxValue;
        // 是否已找到。
bool found = false;
        // 当前遍历索引。
        for (int i = 0; i < hitCount; i++)
        {
            // 候选。
Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || hits[i].normal.y < 0.45f || hits[i].distance >= nearestDistance)
            {
                continue;
            }

            // 落点只能采用场景地面，不能落在先跳下去的敌人或玩家头顶。
            if (candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            landingPosition = hits[i].point;
            found = true;
        }

        // 上层有踏面时不能越过它，把楼下命中当作跳落目标。
        return found && transform.position.y - landingPosition.y >= minimumHeight;
    }

    /// <summary>PlayTraversalClip 的业务处理入口；本方法直接依赖 AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationPlayableOutput.Create(PlayableGraph, string, Animator)、AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationClipPlayable&gt;(AnimationClipPlayable)、EnemyWindowTraversal.StopTraversalClip()、PlayableGraph.Create(string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.BeginDropFallAnimation()、EnemyWindowTraversal.BeginDropLanding()、EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversal.TryBeginDrop(Vector3, Vector3)；下游（含内部回调体） AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationPlayableOutput.Create(PlayableGraph, string, Animator)、AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationClipPlayable&gt;(AnimationClipPlayable)、EnemyWindowTraversal.StopTraversalClip()、PlayableGraph.Create(string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">动画片段。</param>
    private void PlayTraversalClip(AnimationClip clip, float duration = 0f)
    {
        StopTraversalClip();
        if (controller != null && controller.animationPresenter != null)
        {
            clip = controller.animationPresenter.ResolveClipForSkeleton(animator, clip);
        }
        if (animator == null || clip == null)
        {
            return;
        }

        animationGraph = PlayableGraph.Create("敌人特殊通行动画");
        animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        clipPlayable = AnimationClipPlayable.Create(animationGraph, clip);
        if (duration > 0.01f)
        {
            clipPlayable.SetSpeed(clip.length / duration);
        }
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);
        // 输出。
AnimationPlayableOutput output = AnimationPlayableOutput.Create(animationGraph, "敌人特殊通行动画输出", animator);
        output.SetSourcePlayable(clipPlayable);
        animationGraph.Play();
    }

    /// <summary>LoopCurrentClipIfNeeded 的业务处理入口；本方法直接依赖 AnimationClipPlayable.GetAnimationClip()、AnimationClipPlayable.GetTime&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、PlayableGraph.IsValid()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.UpdateSpecialTraversal()；下游（含内部回调体） AnimationClipPlayable.GetAnimationClip()、AnimationClipPlayable.GetTime&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、PlayableGraph.IsValid()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoopCurrentClipIfNeeded()
    {
        if (!animationGraph.IsValid() || !clipPlayable.IsValid())
        {
            return;
        }

        // 持续时间。
double duration;
        if (clipPlayable.GetAnimationClip() != null)
        {
            duration = clipPlayable.GetAnimationClip().length;
        }
        else
        {
            duration = 0d;
        }

        if (duration <= 0.01d || clipPlayable.GetTime() < duration)
        {
            return;
        }

        clipPlayable.SetTime(clipPlayable.GetTime() % duration);
    }

    /// <summary>StopTraversalClip 的业务处理入口；本方法直接依赖 PlayableGraph.Destroy()、PlayableGraph.IsValid()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.PlayTraversalClip(AnimationClip)；下游（含内部回调体） PlayableGraph.Destroy()、PlayableGraph.IsValid()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopTraversalClip()
    {
        if (animationGraph.IsValid())
        {
            animationGraph.Destroy();
        }

        clipPlayable = default;
        nativeWindowMixer = default;
    }

    /// <summary>CacheComponents 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;PathAgent&gt;()、Component.GetComponent&lt;VillagerController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.Start()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.Update()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;PathAgent&gt;()、Component.GetComponent&lt;VillagerController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheComponents()
    {
        if (controller == null)
        {
            controller = GetComponent<VillagerController>();
        }

        if (agent == null)
        {
            agent = GetComponent<PathAgent>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementCollider == null)
        {
            movementCollider = GetComponent<CapsuleCollider>();
        }
    }

    /// <summary>HorizontalDistance 的业务处理入口；本方法直接依赖 Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)、EnemyWindowTraversal.TryBeginPendingDropTraversal()、EnemyWindowTraversal.TryBeginPendingWindowTraversal()、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">首个。</param>
    /// <param name="second">第二。</param>
    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    /// <summary>ReportTraversalDiagnostic 的业务处理入口；本方法直接依赖 Debug.Log(object, Object)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.TryBeginPendingWindowTraversal()；下游（含内部回调体） Debug.Log(object, Object)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stage">阶段。</param>
    /// <param name="details">详细记录。</param>
    /// <param name="force">是否力度。</param>
    private void ReportTraversalDiagnostic(string stage, string details, bool force = false)
    {
        if (!enableTraversalDiagnostic)
        {
            return;
        }

        // 消息。
string message = $"{stage}|{details}";
        if (!force && message == lastTraversalDiagnostic && Time.time < nextTraversalDiagnosticTime)
        {
            return;
        }

        lastTraversalDiagnostic = message;
        nextTraversalDiagnosticTime = Time.time + Mathf.Max(0.1f, traversalDiagnosticInterval);
        Debug.Log($"[敌人翻窗诊断][{gameObject.name}][帧{Time.frameCount}] " + $"{stage}：{details}", this);
    }
}
