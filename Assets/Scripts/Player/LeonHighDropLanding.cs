using UnityEngine;
using RE4.ChapterOne;
using GameArchitecture;

internal static class LeonDropContact
{
    private static ChapterOnePairMotion.Track landingRollTrack;

    internal static bool TryReadSupport(CapsuleCollider capsule, Transform player, WindowTraversalPoint window,
        LayerMask mask, float maxDistance, out float distance)
    {
        distance = float.PositiveInfinity;
        if (capsule == null || !capsule.enabled)
        {
            return false;
        }

        Bounds bounds = capsule.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.7f;
        const float probeLift = 0.12f;
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + probeLift, bounds.center.z);
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, Vector3.down, maxDistance + probeLift, mask,
            QueryTriggerInteraction.Ignore);
        var motion = player.GetComponent<LeonActionMotionDriver>();
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(player) || hit.normal.y < 0.65f ||
                (motion != null && motion.IsWalkThroughDetail(hit.collider)))
            {
                continue;
            }

            if (window != null && window.ShouldIgnoreCollider(hit.collider))
            {
                continue;
            }

            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - probeLift));
        }

        return distance <= maxDistance;
    }

    internal static bool IsLandingImminent(Rigidbody body, float distance, float configuredLeadTime)
    {
        float lead = Mathf.Clamp(configuredLeadTime, Time.fixedDeltaTime, 2f * Time.fixedDeltaTime);
        float downwardSpeed = Mathf.Max(0f, -body.linearVelocity.y);
        float displacement = downwardSpeed * lead + 0.5f * Mathf.Abs(Physics.gravity.y) * lead * lead;
        return distance <= Mathf.Max(0.04f, displacement);
    }

    internal static bool IsContact(Rigidbody body, float distance, float contactDistance)
    {
        return distance <= Mathf.Min(contactDistance, 0.035f) && Mathf.Abs(body.linearVelocity.y) <= 0.5f;
    }

    // 落地姿势保留 0374 的源帧时钟。根轨仅消费接地后的翻滚区间。
    internal const string RollLandingClipName = "LeonTraverse_0374_DropLanding";
    internal const float RollContactTime = 27f / 60f;
    private const float RollPlantEnd = 65f / 60f;

    internal static void AdvanceRoll(Rigidbody body, LeonActionMotionDriver motion, MotionControlToken token,
        LeonAnimationActionCoordinator coordinator, ref float previousTime)
    {
        if (body == null || motion == null || !motion.TokenValid(token))
        {
            return;
        }

        if (coordinator == null || !coordinator.TryReadLandingRollTime(out float clipTime))
        {
            motion.CommitSpeed(token, Vector3.up * body.linearVelocity.y);
            return;
        }

        if (landingRollTrack == null)
        {
            var motionTracks = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            if (motionTracks == null || !motionTracks.TryGetNamedTrack("LeonTraverse_0374", out landingRollTrack))
            {
                return;
            }
        }

        float step = Time.fixedDeltaTime;
        float source = previousTime;
        float next = Mathf.Clamp(clipTime, source, RollPlantEnd);
        Vector3 localDelta = landingRollTrack.Sample(next).position - landingRollTrack.Sample(source).position;
        previousTime = next;
        Vector3 shift = Vector3.ProjectOnPlane(body.rotation * localDelta, Vector3.up);
        float allowedDistance = shift.magnitude;
        if (allowedDistance > 0.001f)
        {
            Vector3 direction = shift / allowedDistance;
            if (body.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                Bounds bounds = capsule.bounds;
                float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
                Vector3 half = Vector3.up * Mathf.Max(0f, bounds.extents.y - radius);
                foreach (Collider obstacle in Physics.OverlapCapsule(bounds.center - half, bounds.center + half,
                    radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.attachedRigidbody == body || obstacle.transform.IsChildOf(body.transform))
                    {
                        continue;
                    }
                    if (Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out var away, out _) &&
                        away.y < .65f && Vector3.Dot(away, direction) < -.05f)
                    {
                        allowedDistance = 0f;
                        break;
                    }
                }
            }
            var hits = body.SweepTestAll(direction, allowedDistance + 0.04f, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.normal.y < 0.65f && Vector3.Dot(hit.normal, direction) < -0.05f)
                {
                    allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - 0.04f));
                }
            }

            shift = direction * allowedDistance;
        }

        float verticalSpeed = body.linearVelocity.y;
        motion.CommitSpeed(token, shift / step + Vector3.up * verticalSpeed);
    }
}

/// <summary>
/// 里昂通用高落差下落控制器。
/// 玩家从已解锁的平台边缘自然走出后自动接管下落、着地和镜头表现，
/// 不创建交互提示，也不要求再次按下交互键。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LeonController))]
public class LeonHighDropLanding : MonoBehaviour
{
    /// <summary>中文含义：里昂玩家控制器。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.CanBeginDrop() 等共 4 处。</summary>
    [Header("组件引用")]
    public LeonController controller;
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.BindEvents()、LeonHighDropLanding.CacheComponents() 等共 8 处。</summary>
    public LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.FinishDrop()。</summary>
    public LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：翻窗通行。使用位置：LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.HandleFixedUpdate()。</summary>
    public LeonWindowTraversal windowTraversal;
    /// <summary>中文含义：掉落摄像机。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.FinishDrop() 等共 5 处。</summary>
    public RE4RThirdPersonCamera dropCamera;
    /// <summary>中文含义：身体。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonHighDropLanding.BeginDrop() 等共 6 处。</summary>
    public Rigidbody body;
    /// <summary>中文含义：角色运动执行器。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.FinishDrop() 等共 4 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("运动执行")]
    [UnityEngine.InspectorName("运动执行")]
    public LeonActionMotionDriver motionExecutor;
    /// <summary>中文含义：通行运动。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、作为 LeonActionMotionDriver.ReleaseMotion(MotionControlToken) 的参数、LeonHighDropLanding.BeginDrop() 等共 4 处。</summary>
    private GameArchitecture.MotionControlToken traversalMotion;
    /// <summary>中文含义：身体碰撞体。使用位置：LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.GetCapsuleRadius()、LeonHighDropLanding.TryGetGroundDistance(out float)。</summary>
    public CapsuleCollider bodyCollider;
    /// <summary>中文含义：最小掉落高度。使用位置：LeonHighDropLanding.HandleFixedUpdate()。</summary>
    [Header("高落差识别")]
    [Min(1f)]
    public float minimumDropHeight = 2f;
    /// <summary>中文含义：最大地面探测距离。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    [Min(2f)]
    public float maximumGroundProbeDistance = 7.5f;
    /// <summary>中文含义：腾空确认时间。使用位置：LeonHighDropLanding.HandleFixedUpdate()。</summary>
    [Range(0.02f, 0.2f)]
    public float airborneConfirmationTime = 0.06f;
    /// <summary>中文含义：地面遮罩。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    public LayerMask groundMask = ~0;
    /// <summary>中文含义：过渡时长。使用位置：作为 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float) 的参数。</summary>
    [Header("动作衔接")]
    [Range(0.05f, 0.25f)]
    public float transitionDuration = 0.09f;
    /// <summary>中文含义：落地预测提前量时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Range(0.1f, 0.5f)]
    public float landingPredictionLeadTime = 0.28f;
    /// <summary>中文含义：落地接触距离。使用位置：作为 Mathf.Max(float, float) 的参数、LeonHighDropLanding.UpdateActiveDrop()。</summary>
    [Min(0.02f)]
    public float landingContactDistance = 0.12f;
    /// <summary>中文含义：落地速度阻尼。使用位置：作为 Vector3.MoveTowards(Vector3, Vector3, float) 的参数。</summary>
    [Min(0.1f)]
    public float landingVelocityDamping = 12f;
    /// <summary>中文含义：动作超时。使用位置：LeonHighDropLanding.Update()。</summary>
    [Min(0.5f)]
    public float actionTimeout = 4f;
    /// <summary>中文含义：是否生效掉落。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.HandleFixedUpdate() 等共 9 处。</summary>
    private bool activeDrop;
    /// <summary>中文含义：是否已经申请本次落地动作。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.UpdateActiveDrop()。</summary>
    private bool landingActionRequested;
    /// <summary>中文含义：是否落地已提交。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.UpdateActiveDrop()。</summary>
    private bool landingCommitted;
    /// <summary>中文含义：是否已绑定事件。使用位置：LeonHighDropLanding.BindEvents()、LeonHighDropLanding.UnbindEvents()。</summary>
    private bool eventsBound;
    /// <summary>中文含义：事务编号。使用位置：作为 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int) 的参数、LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop() 等共 6 处。</summary>
    private int transactionId;
    /// <summary>中文含义：腾空已用。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.HandleFixedUpdate()。</summary>
    private float airborneElapsed;
    /// <summary>中文含义：动作开始时间。使用位置：LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.Update()。</summary>
    private float actionStartedTime;
    /// <summary>中文含义：最快向下速度。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonHighDropLanding.BeginDrop() 等共 5 处。</summary>
    private float fastestDownwardVelocity;
    private float landingRollElapsed;
    /// <summary>中文含义：是否生效。使用位置：LeonFootAction.AllowGroundContact.get。</summary>
    public bool IsActive => activeDrop;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonHighDropLanding.BindEvents()、LeonHighDropLanding.CacheComponents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        CacheComponents();
        BindEvents();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonHighDropLanding.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (coordinator != null)
        {
            BindEvents();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        UnbindEvents();
        if (activeDrop)
        {
            FinishDrop();
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.CancelTraversal()、LeonHighDropLanding.FinishDrop()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!activeDrop)
        {
            return;
        }

        if (Time.time - actionStartedTime <= actionTimeout)
        {
            return;
        }

        // receiverValue1（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.Update()。
global::LeonAnimationActionCoordinator receiverValue1 = coordinator;
        // optionalValue2（条件访问得到的取消通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonHighDropLanding.Update()。
bool? optionalValue2;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            optionalValue2 = receiverValue1.CancelTraversal();
        }
        else
        {
            optionalValue2 = default;
        }

        FinishDrop();
    }

    /// <summary>HandleFixedUpdate 的业务处理入口；本方法直接依赖 LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.CanBeginDrop()、LeonHighDropLanding.HasImmediateGroundSupport()、LeonHighDropLanding.TryGetGroundDistance(out float)、LeonHighDropLanding.UpdateActiveDrop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.FixedUpdate()；下游（含内部回调体） LeonHighDropLanding.BeginDrop()、LeonHighDropLanding.CacheComponents()、LeonHighDropLanding.CanBeginDrop()、LeonHighDropLanding.HasImmediateGroundSupport()、LeonHighDropLanding.TryGetGroundDistance(out float)、LeonHighDropLanding.UpdateActiveDrop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HandleFixedUpdate()
    {
        CacheComponents();
        if (activeDrop)
        {
            UpdateActiveDrop();
            return true;
        }

        if (windowTraversal != null && windowTraversal.IsTraversing)
        {
            airborneElapsed = 0f;
            return false;
        }

        if (!CanBeginDrop())
        {
            airborneElapsed = 0f;
            return false;
        }

        if (HasImmediateGroundSupport())
        {
            airborneElapsed = 0f;
            return false;
        }

        if (!TryGetGroundDistance(out float /* 中文含义：距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ distance) || distance < minimumDropHeight)
        {
            airborneElapsed = 0f;
            return false;
        }

        airborneElapsed += Time.fixedDeltaTime;
        if (airborneElapsed < airborneConfirmationTime)
        {
            return false;
        }

        return BeginDrop();
    }

    /// <summary>CanBeginDrop 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanBeginDrop()
    {
        if (controller == null || coordinator == null || body == null || !controller.NewPipelineValid || controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || controller.currentState == LeonState.QTE || controller.currentState == LeonState.MeleeAttacking || controller.currentState == LeonState.Parrying || controller.currentState == LeonState.Traversing)
        {
            return false;
        }

        if (coordinator.HasActiveFullBodyAction)
        {
            return false;
        }

        return body.linearVelocity.y <= -0.05f;
    }

    /// <summary>BeginDrop 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonController.ChangeState(LeonState)、LeonController.SetSprinting(bool)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.BeginDropCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonController.ChangeState(LeonState)、LeonController.SetSprinting(bool)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.BeginDropCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool BeginDrop()
    {
        airborneElapsed = 0f;
        if (!coordinator.RequestAutomaticDrop())
        {
            return false;
        }

        activeDrop = true;
        landingActionRequested = false;
        landingCommitted = false;
        transactionId = coordinator.CurrentFullBodyTransactionId;
        if (motionExecutor != null)
        {
            traversalMotion = motionExecutor.AuthorizeMotion(GameArchitecture.MotionSource.Traversal, transactionId);
        }
        else
        {
            traversalMotion = default;
        }

        actionStartedTime = Time.time;
        fastestDownwardVelocity = body.linearVelocity.y;
        controller.SetSprinting(false);
        controller.ChangeState(LeonState.Traversing);
        // receiverValue4（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.BeginDrop()。
global::LeonWeaponVisualController receiverValue4 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.SetTraversalWeaponVisible(false);
        }

        // receiverValue5（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.BeginDrop()。
global::RE4RThirdPersonCamera receiverValue5 = dropCamera;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.BeginDropCamera();
        }

        return true;
    }

    /// <summary>UpdateActiveDrop 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonHighDropLanding.TryGetGroundDistance(out float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonHighDropLanding.TryGetGroundDistance(out float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateActiveDrop()
    {
        if (body == null)
        {
            return;
        }

        if (landingCommitted)
        {
            LeonDropContact.AdvanceRoll(body, motionExecutor, traversalMotion, coordinator, ref landingRollElapsed);
            return;
        }

        fastestDownwardVelocity = Mathf.Min(fastestDownwardVelocity, body.linearVelocity.y);
        if (!TryGetGroundDistance(out float distance))
        {
            return;
        }

        if (!landingActionRequested && LeonDropContact.IsLandingImminent(body, distance, landingPredictionLeadTime))
        {
            float blendDuration = Mathf.Min(transitionDuration, 2f * Time.fixedDeltaTime);
            landingActionRequested = coordinator.AdvanceTraversalAction(FullBodyActionType.WindowDropLand, blendDuration);
        }

        if (landingActionRequested && LeonDropContact.IsContact(body, distance, landingContactDistance))
        {
            landingCommitted = true;
            landingRollElapsed = LeonDropContact.RollContactTime;
            if (TryGetComponent<NativeActionAudio>(out var actionAudio))
            {
                actionAudio.PlayLandingContact(body.position, groundMask);
            }
            if (motionExecutor != null)
            {
                motionExecutor.CommitSpeed(traversalMotion, Vector3.zero);
            }

            if (dropCamera != null)
            {
                dropCamera.NotifyDropImpact(Mathf.Abs(fastestDownwardVelocity));
            }
        }
    }

    /// <summary>TryGetGroundDistance 的业务处理入口；本方法直接依赖 LeonHighDropLanding.GetCapsuleRadius()、Mathf.Max(float, float)、Mathf.Min(float, float)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()、LeonHighDropLanding.UpdateActiveDrop()；下游（含内部回调体） LeonHighDropLanding.GetCapsuleRadius()、Mathf.Max(float, float)、Mathf.Min(float, float)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="distance">中文含义：距离。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Min(float, float) 的参数、LeonHighDropLanding.TryGetGroundDistance(out float)。</param>
    private bool TryGetGroundDistance(out float distance)
    {
        return LeonDropContact.TryReadSupport(bodyCollider, transform, null, groundMask, maximumGroundProbeDistance, out distance);
    }

    /// <summary>HasImmediateGroundSupport 的业务处理入口；本方法直接依赖 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()；下游（含内部回调体） Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasImmediateGroundSupport()
    {
        return LeonDropContact.TryReadSupport(bodyCollider, transform, null, groundMask, 0.35f, out _);
    }

    /// <summary>HandleTraversalCompleted 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonHighDropLanding.HandleTraversalCompleted(AnimationActionTransaction)。</param>
    private void HandleTraversalCompleted(AnimationActionTransaction transaction)
    {
        if (!activeDrop || transaction.transactionId != transactionId || transaction.fullBodyActionType != FullBodyActionType.WindowDropLand)
        {
            return;
        }

        // receiverValue11（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.HandleTraversalCompleted(AnimationActionTransaction)。
global::RE4RThirdPersonCamera receiverValue11 = dropCamera;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.EndDropCamera();
        }
    }

    /// <summary>HandleTraversalExited 的业务处理入口；本方法直接依赖 LeonHighDropLanding.FinishDrop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonHighDropLanding.FinishDrop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonHighDropLanding.HandleTraversalExited(AnimationActionTransaction)。</param>
    private void HandleTraversalExited(AnimationActionTransaction transaction)
    {
        if (!activeDrop || transaction.transactionId != transactionId)
        {
            return;
        }

        FinishDrop();
    }

    /// <summary>HandleTraversalInterrupted 的业务处理入口；本方法直接依赖 LeonHighDropLanding.FinishDrop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonHighDropLanding.FinishDrop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonHighDropLanding.HandleTraversalInterrupted(AnimationActionTransaction)。</param>
    private void HandleTraversalInterrupted(AnimationActionTransaction transaction)
    {
        if (!activeDrop || transaction.transactionId != transactionId)
        {
            return;
        }

        FinishDrop();
    }

    /// <summary>FinishDrop 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ReleaseMotion(MotionControlToken)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleTraversalExited(AnimationActionTransaction)、LeonHighDropLanding.HandleTraversalInterrupted(AnimationActionTransaction)、LeonHighDropLanding.OnDisable()、LeonHighDropLanding.Update()；下游（含内部回调体） LeonActionMotionDriver.ReleaseMotion(MotionControlToken)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FinishDrop()
    {
        // receiverValue12（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.FinishDrop()。
global::LeonActionMotionDriver receiverValue12 = motionExecutor;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            receiverValue12.ReleaseMotion(traversalMotion);
        }

        traversalMotion = default;
        // wasActive（之前是否生效）：使用位置：LeonHighDropLanding.FinishDrop()。
bool wasActive = activeDrop;
        activeDrop = false;
        landingActionRequested = false;
        landingCommitted = false;
        transactionId = 0;
        airborneElapsed = 0f;
        fastestDownwardVelocity = 0f;
        // receiverValue13（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.FinishDrop()。
global::RE4RThirdPersonCamera receiverValue13 = dropCamera;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            receiverValue13.EndDropCamera();
        }

        // receiverValue14（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonHighDropLanding.FinishDrop()。
global::LeonWeaponVisualController receiverValue14 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.SetTraversalWeaponVisible(true);
        }

        if (wasActive && controller != null && controller.currentState != LeonState.Dead)
        {
            controller.ChangeState(LeonState.Idle);
        }
    }

    /// <summary>CacheComponents 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponent&lt;LeonWindowTraversal&gt;()、Component.GetComponent&lt;Rigidbody&gt;() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.HandleFixedUpdate()、LeonHighDropLanding.Start()；下游（含内部回调体） Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponent&lt;LeonWindowTraversal&gt;()、Component.GetComponent&lt;Rigidbody&gt;() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheComponents()
    {
        if (motionExecutor == null)
        {
            motionExecutor = GetComponent<LeonActionMotionDriver>();
        }

        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        if (windowTraversal == null)
        {
            windowTraversal = GetComponent<LeonWindowTraversal>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider>();
        }

        if (dropCamera == null)
        {
            dropCamera = Object.FindFirstObjectByType<RE4RThirdPersonCamera>();
        }
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.OnEnable()、LeonHighDropLanding.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnTraversalActionCompleted += HandleTraversalCompleted;
        coordinator.OnTraversalActionExited += HandleTraversalExited;
        coordinator.OnTraversalActionInterrupted += HandleTraversalInterrupted;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnTraversalActionCompleted -= HandleTraversalCompleted;
        coordinator.OnTraversalActionExited -= HandleTraversalExited;
        coordinator.OnTraversalActionInterrupted -= HandleTraversalInterrupted;
        eventsBound = false;
    }
}
