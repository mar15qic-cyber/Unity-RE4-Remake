using UnityEngine;

/// <summary>
/// 里昂窗口翻越控制器。
/// 负责窗口尺寸、窗台高度、路径与落点校验，并在全身动作期间接管刚体运动。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LeonController))]
public class LeonWindowTraversal : MonoBehaviour
{
    /// <summary>中文含义：里昂玩家控制器。使用位置：LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.ReleaseNormalTraversalGameplay() 等共 6 处。</summary>
    [Header("组件引用")]
    public LeonController controller;
    /// <summary>中文含义：战斗。使用位置：LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)。</summary>
    public LeonCombat combat;
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BindEvents()、LeonWindowTraversal.CacheComponents() 等共 10 处。</summary>
    public LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.ReleaseNormalTraversalGameplay() 等共 5 处。</summary>
    public LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：动画控制器。使用位置：LeonWindowTraversal.CacheComponents()、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)。</summary>
    public Animator animator;
    /// <summary>中文含义：身体。使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics() 等共 11 处。</summary>
    public Rigidbody body;
    /// <summary>中文含义：角色运动执行器。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.CacheComponents() 等共 10 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("运动执行")]
    [UnityEngine.InspectorName("运动执行")]
    public LeonActionMotionDriver motionExecutor;
    [Tooltip("当前地图的窗口、楼层与外平台归属。")]
    public HouseThreeRegionNavigation platformNavigation;
    /// <summary>中文含义：通行运动。使用位置：作为 LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3) 的参数、作为 LeonActionMotionDriver.CommitPosition(MotionControlToken, Vector3) 的参数、作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数 等共 7 处。</summary>
    private GameArchitecture.MotionControlToken traversalMotion;
    /// <summary>中文含义：掉落摄像机。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.CommitDropImpact() 等共 5 处。</summary>
    public RE4RThirdPersonCamera dropCamera;
    /// <summary>中文含义：掉落过渡时长。使用位置：作为 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float) 的参数。</summary>
    [Header("高落差着地")]
    [Range(0.05f, 0.25f)]
    public float dropTransitionDuration = 0.09f;
    /// <summary>中文含义：落地预测提前量时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Range(0.1f, 0.5f)]
    public float landingPredictionLeadTime = 0.28f;
    /// <summary>中文含义：落地接触距离。使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.UpdateAutomaticFall()。</summary>
    [Min(0.02f)]
    public float landingContactDistance = 0.12f;
    /// <summary>中文含义：落地速度阻尼。使用位置：作为 Vector3.MoveTowards(Vector3, Vector3, float) 的参数。</summary>
    [Min(0.1f)]
    public float landingVelocityDamping = 12f;
    /// <summary>中文含义：身体碰撞体。使用位置：作为 Mathf.Abs(float) 的参数、作为 Transform.TransformPoint(Vector3) 的参数、LeonWindowTraversal.BeginAutomaticDrop() 等共 11 处。</summary>
    public CapsuleCollider bodyCollider;
    /// <summary>中文含义：最小开口宽度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Header("窗口资格")]
    [Min(0.1f)]
    public float minimumOpeningWidth = 0.8f;
    /// <summary>中文含义：最小开口高度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Min(0.1f)]
    public float minimumOpeningHeight = 1f;
    /// <summary>中文含义：最小窗台高度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Min(0f)]
    public float minimumSillHeight = 0.55f;
    /// <summary>中文含义：最大窗台高度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Min(0.1f)]
    public float maximumSillHeight = 1.25f;
    /// <summary>中文含义：最大墙体厚度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Min(0.05f)]
    public float maximumWallThickness = 0.65f;
    /// <summary>中文含义：最大朝向角度。使用位置：作为 WindowTraversalPoint.ResolveMaximumFacingAngle(float) 的参数。</summary>
    [Range(0f, 90f)]
    public float maximumFacingAngle = 45f;
    /// <summary>中文含义：最大落地高度差值。使用位置：作为 WindowTraversalPoint.ResolveMaximumLandingHeightDifference(float) 的参数。</summary>
    [Min(0f)]
    public float maximumLandingHeightDifference = 1f;
    /// <summary>中文含义：通行持续时间。使用位置：LeonWindowTraversal.HandleFixedUpdate()。</summary>
    [Header("运动轨迹")]
    [Min(0.1f)]
    public float traversalDuration = 1.07f;
    /// <summary>中文含义：弧线净空。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float arcClearance = 0.18f;
    /// <summary>中文含义：路径校验采样集合。使用位置：作为 Mathf.Max(int, int) 的参数。</summary>
    [Range(3, 12)]
    public int pathValidationSamples = 7;
    /// <summary>中文含义：通行胶囊高度。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    [Min(0.5f)]
    public float traversalCapsuleHeight = 1.05f;
    /// <summary>中文含义：请求超时。使用位置：LeonWindowTraversal.Update()。</summary>
    [Min(0.1f)]
    public float requestTimeout = 1.5f;
    /// <summary>中文含义：通行超时。使用位置：LeonWindowTraversal.Update()。</summary>
    [Min(0.5f)]
    public float traversalTimeout = 3f;
    private bool isOnEnemyPlatformRegion;
    /// <summary>中文含义：生效窗口。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数 等共 10 处。</summary>
    private WindowTraversalPoint activeWindow;
    /// <summary>中文含义：生效方案。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、作为 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的参数、作为 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 的参数 等共 12 处。</summary>
    private TraversalPlan activePlan;
    /// <summary>中文含义：请求是否仍待处理。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.HandleFixedUpdate()、LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction) 等共 6 处。</summary>
    private bool requestPending;
    /// <summary>中文含义：通行是否已开始。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.HandleFixedUpdate() 等共 8 处。</summary>
    private bool traversalStarted;
    /// <summary>中文含义：移动是否已完成。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction)、LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction) 等共 5 处。</summary>
    private bool movementCompleted;
    /// <summary>中文含义：请求开始时间。使用位置：LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)、LeonWindowTraversal.Update()。</summary>
    private float requestStartedTime;
    private RE4.ChapterOne.ChapterOnePairMotion traversalTracks;
    /// <summary>中文含义：通行开始时间。使用位置：LeonWindowTraversal.HandleFixedUpdate()、LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction)、LeonWindowTraversal.Update()。</summary>
    private float traversalStartedTime;
    /// <summary>中文含义：生效事务编号。使用位置：作为 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int) 的参数、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction) 等共 7 处。</summary>
    private int activeTransactionId;
    /// <summary>中文含义：是否已绑定事件。使用位置：LeonWindowTraversal.BindEvents()、LeonWindowTraversal.UnbindEvents()。</summary>
    private bool eventsBound;
    /// <summary>中文含义：是否前一次运动学。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.EndTraversalPhysics()。</summary>
    private bool previousKinematic;
    /// <summary>中文含义：是否前一次使用重力。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.EndTraversalPhysics()。</summary>
    private bool previousUseGravity;
    /// <summary>中文含义：前一次身体碰撞体是否启用。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.EndTraversalPhysics()。</summary>
    private bool previousBodyColliderEnabled;
    /// <summary>中文含义：是否具有延后的命中。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.QueueDeferredHit(LeonHitDirection) 等共 4 处。</summary>
    private bool hasDeferredHit;
    /// <summary>中文含义：运动阶段。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.CommitDropImpact() 等共 6 处。</summary>
    private TraversalMotionPhase motionPhase;
    /// <summary>中文含义：是否玩法已释放。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.HandleFixedUpdate() 等共 4 处。</summary>
    private bool gameplayReleased;
    /// <summary>中文含义：是否通行物理已捕获。使用位置：LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.EndTraversalPhysics()。</summary>
    private bool traversalPhysicsCaptured;
    /// <summary>中文含义：是否已经申请本次落地动作。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.CommitDropImpact() 等共 5 处。</summary>
    private bool landingActionRequested;
    /// <summary>中文含义：最快向下速度。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonWindowTraversal.BeginAutomaticDrop() 等共 5 处。</summary>
    private float fastestDownwardVelocity;
    private float landingRollElapsed;
    /// <summary>中文含义：掉落开始时间。使用位置：LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.UpdateAutomaticFall()。</summary>
    private float dropStartedTime;
    /// <summary>中文含义：延后的命中方向。使用位置：LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.QueueDeferredHit(LeonHitDirection)。</summary>
    private LeonHitDirection deferredHitDirection;
    /// <summary>中文含义：是否通行中。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonHighDropLanding.HandleFixedUpdate()、LeonWindowTraversal.FinishTraversal(bool) 等共 7 处。</summary>
    public bool IsTraversing => requestPending || traversalStarted;

    /// <summary>玩家控制、动作与运动使用的 TraversalMotionPhase 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum TraversalMotionPhase
    {
        /// <summary>中文含义：无（未选择或尚未发生）。</summary>
        None,
        /// <summary>中文含义：攀爬中。</summary>
        Climbing,
        /// <summary>中文含义：下落中。</summary>
        Falling,
        /// <summary>中文含义：落地。</summary>
        Landing
    }

    /// <summary>玩家控制、动作与运动的结构化数据 TraversalPlan；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct TraversalPlan
    {
        /// <summary>中文含义：起始位置。使用位置：作为 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 的参数、LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
        public Vector3 startPosition;
        /// <summary>中文含义：控制位置。使用位置：LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
        public Vector3 controlPosition;
        /// <summary>中文含义：结束位置。使用位置：作为 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 的参数、LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.FinishTraversal(bool) 等共 4 处。</summary>
        public Vector3 endPosition;
        /// <summary>中文含义：落地位置。使用位置：LeonWindowTraversal.CommitDropImpact()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
        public Vector3 landingPosition;
        /// <summary>中文含义：行进方向。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、LeonWindowTraversal.UpdateAutomaticFall()。</summary>
        public Vector3 travelDirection;
        /// <summary>中文含义：朝向旋转。使用位置：作为 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
        public Quaternion facingRotation;
        /// <summary>中文含义：是否自动掉落。使用位置：LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.HandleFixedUpdate()、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction) 等共 5 处。</summary>
        public bool automaticDrop;
        /// <summary>中文含义：掉落高度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
        public float dropHeight;
        public RE4.ChapterOne.ChapterOnePairMotion.Track track;
        public float trackEndTime;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.BindEvents()、LeonWindowTraversal.CacheComponents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        CacheComponents();
        BindEvents();
    }

    public bool TryGetEnemyPlatformRegionState(string regionId, out bool isOnPlatform)
    {
        isOnPlatform = false;
        if (platformNavigation == null || string.IsNullOrWhiteSpace(regionId) ||
            platformNavigation.outerPlatformIdentifier != regionId)
        {
            return false;
        }

        if (!IsTraversing)
        {
            RefreshPlatformOwnership();
        }

        isOnPlatform = isOnEnemyPlatformRegion;
        return true;
    }

    private void RefreshPlatformOwnership()
    {
        if (platformNavigation != null)
        {
            isOnEnemyPlatformRegion = platformNavigation.ReadPlatformOwnership(transform.position, isOnEnemyPlatformRegion);
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (coordinator != null)
        {
            BindEvents();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        UnbindEvents();
        if (IsTraversing)
        {
            FinishTraversal(false);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.CancelTraversal()、LeonWindowTraversal.FinishTraversal(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (requestPending && Time.time - requestStartedTime > requestTimeout)
        {
            // receiverValue2（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.Update()。
global::LeonAnimationActionCoordinator receiverValue2 = coordinator;
            // optionalValue3（条件访问得到的取消通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.Update()。
bool? optionalValue3;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                optionalValue3 = receiverValue2.CancelTraversal();
            }
            else
            {
                optionalValue3 = default;
            }

            FinishTraversal(false);
            return;
        }

        if (traversalStarted && Time.time - traversalStartedTime > traversalTimeout)
        {
            // receiverValue4（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.Update()。
global::LeonAnimationActionCoordinator receiverValue4 = coordinator;
            // optionalValue5（条件访问得到的取消通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.Update()。
bool? optionalValue5;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                optionalValue5 = receiverValue4.CancelTraversal();
            }
            else
            {
                optionalValue5 = default;
            }

            FinishTraversal(false);
        }
    }

    /// <summary>Unity 固定时间步更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void FixedUpdate()
    {
    // 固定帧由 LeonController 统一调度，避免移动系统在同一帧改写下落速度。
    }

    /// <summary>HandleFixedUpdate 的业务处理入口；本方法直接依赖 LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.MoveAlongPlan(float)、LeonWindowTraversal.ReleaseNormalTraversalGameplay()、LeonWindowTraversal.UpdateAutomaticFall()、LeonWindowTraversal.UpdateLandingRecovery()、Mathf.Clamp(float, float, float)、Mathf.Clamp01(float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.FixedUpdate()；下游（含内部回调体） LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.MoveAlongPlan(float)、LeonWindowTraversal.ReleaseNormalTraversalGameplay()、LeonWindowTraversal.UpdateAutomaticFall()、LeonWindowTraversal.UpdateLandingRecovery()、Mathf.Clamp(float, float, float)、Mathf.Clamp01(float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HandleFixedUpdate()
    {
        if (requestPending)
        {
            return true;
        }

        if (gameplayReleased)
        {
            return false;
        }

        if (!traversalStarted)
        {
            return false;
        }

        if (motionPhase == TraversalMotionPhase.Climbing)
        {
            // duration（持续时间）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.HandleFixedUpdate()。
            float elapsed = Time.time - traversalStartedTime;
            float progress;
            if (activePlan.track != null)
            {
                progress = Mathf.Clamp01(elapsed / activePlan.trackEndTime);
                Vector3 position = SampleTraversalPosition(activePlan, progress);
                CommitWindowFracture(elapsed, position);
                SetBodyPose(position, activePlan.facingRotation);
            }
            else
            {
                float duration = traversalDuration;
                if (activePlan.automaticDrop && activeWindow != null)
                {
                    duration *= Mathf.Clamp(activeWindow.automaticDropReleaseNormalizedTime, 0.5f, 0.95f);
                }

                progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
                MoveAlongPlan(progress, elapsed);
            }
            if (progress >= 1f)
            {
                if (activePlan.automaticDrop)
                {
                    BeginAutomaticDrop();
                }
                else
                {
                    ReleaseNormalTraversalGameplay();
                }
            }
        }
        else if (motionPhase == TraversalMotionPhase.Falling)
        {
            UpdateAutomaticFall();
        }
        else if (motionPhase == TraversalMotionPhase.Landing)
        {
            UpdateLandingRecovery();
        }

        return true;
    }

    private void CommitWindowFracture(float elapsed, Vector3 nextPosition)
    {
        if (coordinator == null || motionExecutor == null || activeWindow == null ||
            activeWindow.fracture == null || activeWindow.fracture.IsBroken ||
            coordinator.CurrentFullBodyTransactionId != activeTransactionId || !motionExecutor.TokenValid(traversalMotion))
        {
            return;
        }

        // 0372 以前肩穿窗。根轨提前到窗面时也要在提交位移前结算玻璃。
        float contactTime = 10f / 60f;
        float contactHeight = -0.20f;
        if (activePlan.automaticDrop)
        {
            contactTime = 14f / 60f;
            contactHeight = 0.08f;
        }

        Vector3 opening = activeWindow.OpeningCenterWorld;
        float approachDistance = Vector3.Dot(opening - nextPosition, activePlan.travelDirection);
        // 在角色和镜头进入窗洞前结算玻璃，保留较早的动画接触帧。
        if (elapsed < contactTime && approachDistance > 0.48f)
        {
            return;
        }

        Vector3 across = activePlan.facingRotation * Vector3.right;
        Vector3 point = activeWindow.OpeningCenterWorld + across * 0.32f + Vector3.up * contactHeight;
        activeWindow.fracture.Fracture(point, activePlan.travelDirection);
    }

    /// <summary>CanTraverse 的业务处理入口；本方法直接依赖 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WindowTraversalPoint.CanInteract(Transform)；下游（含内部回调体） LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="window">中文含义：窗口。使用位置：作为 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan) 的参数。</param>
    private int previewFrame = -1;
    private WindowTraversalPoint previewWindow;
    private TraversalPlan previewPlan;

    public bool CanTraverse(WindowTraversalPoint window)
    {
        if (window == null || window.IsPassageReservedByOther(this))
        {
            return false;
        }

        if (!TryBuildPlan(window, out var plan))
        {
            return false;
        }
        previewFrame = Time.frameCount;
        previewWindow = window;
        previewPlan = plan;
        return true;
    }

    /// <summary>TryStartTraversal 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonAnimationActionCoordinator.RequestTraversal()、LeonCombat.InterruptForQTE()、LeonController.ChangeState(LeonState)、LeonController.SetFreeLooking(bool)、LeonController.SetLookInput(Vector2)、LeonController.SetMoveInput(Vector3) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WindowTraversalPoint.Interact()；下游（含内部回调体） LeonActionMotionDriver.AuthorizeMotion(MotionSource, int)、LeonAnimationActionCoordinator.RequestTraversal()、LeonCombat.InterruptForQTE()、LeonController.ChangeState(LeonState)、LeonController.SetFreeLooking(bool)、LeonController.SetLookInput(Vector2)、LeonController.SetMoveInput(Vector3) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="window">中文含义：窗口。使用位置：作为 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan) 的参数、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)。</param>
    public bool TryStartTraversal(WindowTraversalPoint window)
    {
        CacheComponents();
        BindEvents();
        TraversalPlan plan;
        if (window != null && previewFrame == Time.frameCount && previewWindow == window)
        {
            plan = previewPlan;
        }
        else if (!TryBuildPlan(window, out plan))
        {
            return false;
        }

        if (coordinator == null || combat == null || !window.TryReservePassage(this))
        {
            return false;
        }

        combat.InterruptForQTE();
        controller.ResetMovementInputForWindow();
        controller.SetLookInput(Vector2.zero);
        controller.SetFreeLooking(false);
        activeWindow = window;
        activePlan = plan;
        requestPending = true;
        traversalStarted = false;
        movementCompleted = false;
        requestStartedTime = Time.time;
        hasDeferredHit = false;
        LeonAnimatorDriver animationDriver = GetComponent<LeonAnimatorDriver>();
        if (animationDriver != null && plan.track != null)
        {
            animationDriver.PrepareWindowClimb(plan.automaticDrop && !window.isFence, window.isFence);
        }

        if (!coordinator.RequestTraversal())
        {
            if (animationDriver != null)
            {
                animationDriver.PrepareWindowClimb(false);
            }
            ClearActiveTraversal();
            return false;
        }

        activeTransactionId = coordinator.CurrentFullBodyTransactionId;
        if (motionExecutor != null)
        {
            traversalMotion = motionExecutor.AuthorizeMotion(GameArchitecture.MotionSource.Traversal, activeTransactionId);
        }
        else
        {
            traversalMotion = default;
        }

        controller.ChangeState(LeonState.Traversing);
        // receiverValue7（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)。
global::LeonWeaponVisualController receiverValue7 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.SetTraversalWeaponVisible(false);
        }

        return true;
    }

    /// <summary>QueueDeferredHit 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：LeonWindowTraversal.QueueDeferredHit(LeonHitDirection)。</param>
    public void QueueDeferredHit(LeonHitDirection direction)
    {
        if (!IsTraversing)
        {
            return;
        }

        hasDeferredHit = true;
        deferredHitDirection = direction;
    }

    /// <summary>TryBuildPlan 的业务处理入口；本方法直接依赖 LeonController.CanAct()、LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.GetRootPositionOnGround(Vector3)、LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)、LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)、LeonWindowTraversal.TraversalPlan.TraversalPlan() 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.CanTraverse(WindowTraversalPoint)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） LeonController.CanAct()、LeonWindowTraversal.CacheComponents()、LeonWindowTraversal.GetRootPositionOnGround(Vector3)、LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)、LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)、LeonWindowTraversal.TraversalPlan.TraversalPlan() 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="window">中文含义：窗口。使用位置：作为 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint) 的参数、作为 LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint) 的参数、作为 LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint) 的参数 等共 5 处。</param>
    /// <param name="plan">中文含义：方案。输出参数，由本入口写入并交给调用方。使用位置：作为 LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</param>
    private bool TryBuildPlan(WindowTraversalPoint window, out TraversalPlan plan)
    {
        plan = default;
        CacheComponents();
        if (window == null || controller == null || body == null)
        {
            return false;
        }

        if (!controller.NewPipelineValid || IsTraversing)
        {
            return false;
        }

        if (!controller.CanAct() || controller.IsBusy)
        {
            return false;
        }

        if (coordinator != null && (coordinator.HasActiveFullBodyAction || coordinator.HasActiveWeaponAction || coordinator.WaitingForWeaponExit))
        {
            return false;
        }

        if (window.openingWidth < minimumOpeningWidth || window.openingHeight < minimumOpeningHeight || window.wallThickness > maximumWallThickness)
        {
            return false;
        }

        // center（中心）：使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 center = window.OpeningCenterWorld;
        // normal（表面法线）：使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 normal = window.NormalWorld;
        // sideSign（侧边符号）：保存 LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint) 的返回结果，供当前步骤使用。使用位置：作为 WindowTraversalPoint.GetApproachProbePosition(int) 的参数、作为 WindowTraversalPoint.GetAutomaticDropReleasePosition(int) 的参数、作为 WindowTraversalPoint.GetLandingProbePosition(int) 的参数 等共 4 处。
int sideSign = ResolveTraversalSourceSide(window);
        if (!window.isFence)
        {
            Vector3 fromOpening = body.position - center;
            float depth = Mathf.Abs(Vector3.Dot(fromOpening, normal));
            float lateral = Mathf.Abs(Vector3.Dot(fromOpening, Vector3.Cross(Vector3.up, normal).normalized));
            if (depth > Mathf.Min(window.interactRange, window.wallThickness * .5f + window.approachOffset + .42f)
                || lateral > window.openingWidth * .5f + .12f)
            {
                return false;
            }
        }

        if (!window.allowBothDirections && sideSign < 0)
        {
            return false;
        }

        // travelDirection（行进方向）：使用位置：作为 Quaternion.LookRotation(Vector3, Vector3) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 travelDirection = -normal * sideSign;
        travelDirection.y = 0f;
        if (travelDirection.sqrMagnitude < 0.001f)
        {
            return false;
        }

        travelDirection.Normalize();
        // facing（朝向）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (facing.sqrMagnitude < 0.001f)
        {
            facing = travelDirection;
        }

        facing.Normalize();
        if (Vector3.Angle(facing, travelDirection) > window.ResolveMaximumFacingAngle(maximumFacingAngle))
        {
            return false;
        }

        if (!TryFindGround(window, window.GetApproachProbePosition(sideSign), out Vector3 /* 中文含义：来源地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceGround))
        {
            return false;
        }

        if (!TryFindGround(window, window.GetLandingProbePosition(sideSign), out Vector3 /* 中文含义：目的地地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ destinationGround))
        {
            return false;
        }

        // startPosition（起始位置）：保存 LeonWindowTraversal.GetRootPositionOnGround(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 4 处。
Vector3 startPosition = GetRootPositionOnGround(sourceGround);
        // landingPosition（落地位置）：保存 LeonWindowTraversal.GetRootPositionOnGround(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 landingPosition = GetRootPositionOnGround(destinationGround);
        if (Vector3.Distance(transform.position, startPosition) > window.interactRange)
        {
            return false;
        }

        // sillTop（窗台顶部）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
float sillTop = center.y - window.openingHeight * 0.5f;
        // sillHeight（窗台高度）：使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
float sillHeight = sillTop - sourceGround.y;
        if (sillHeight < minimumSillHeight || sillHeight > maximumSillHeight)
        {
            return false;
        }

        // descendingHeight（下降高度）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
float descendingHeight = sourceGround.y - destinationGround.y;
        // automaticDrop（是否自动掉落）：使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
bool automaticDrop = window.enableAutomaticDropLanding && descendingHeight >= window.minimumAutomaticDropHeight;
        // allowedHeightDifference（允许的高度差值）：保存 WindowTraversalPoint.ResolveMaximumLandingHeightDifference(float) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
float allowedHeightDifference = window.ResolveMaximumLandingHeightDifference(maximumLandingHeightDifference);
        if (!automaticDrop && Mathf.Abs(destinationGround.y - sourceGround.y) > allowedHeightDifference)
        {
            return false;
        }

        if (!HasStandingClearance(landingPosition, window))
        {
            return false;
        }

        // endPosition（结束位置）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 endPosition = landingPosition;
        if (automaticDrop)
        {
            // release（释放）：保存 WindowTraversalPoint.GetAutomaticDropReleasePosition(int) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
Vector3 release = window.GetAutomaticDropReleasePosition(sideSign);
            endPosition = new Vector3(release.x, startPosition.y - 0.08f, release.z);
        }

        // controlPosition（控制位置）：使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
Vector3 controlPosition = (startPosition + endPosition) * 0.5f;
        // desiredApex（期望顶点）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。
float desiredApex = Mathf.Max(Mathf.Max(startPosition.y, endPosition.y), sillTop + arcClearance);
        controlPosition.y = desiredApex * 2f - (startPosition.y + endPosition.y) * 0.5f;
        plan = new TraversalPlan
        {
            startPosition = startPosition,
            controlPosition = controlPosition,
            endPosition = endPosition,
            landingPosition = landingPosition,
            travelDirection = travelDirection,
            facingRotation = Quaternion.LookRotation(travelDirection, Vector3.up),
            automaticDrop = automaticDrop,
            dropHeight = Mathf.Max(0f, descendingHeight)
        };
        ResolveTraversalTrack(ref plan, window);
        if (window.isFence && plan.track == null)
        {
            return false;
        }

        if (!window.isFence && !automaticDrop && plan.track != null)
        {
            Vector3 rollProbe = window.GetLandingProbePosition(sideSign) + travelDirection * 0.9f;
            if (TryFindGround(window, rollProbe, out Vector3 rollGround) &&
                Mathf.Abs(rollGround.y - destinationGround.y) <= 0.22f)
            {
                TraversalPlan rollPlan = plan;
                rollPlan.landingPosition = GetRootPositionOnGround(rollGround);
                rollPlan.endPosition = rollPlan.landingPosition;
                if (HasStandingClearance(rollPlan.landingPosition, window) && IsPathClear(rollPlan, window))
                {
                    plan = rollPlan;
                    return true;
                }
            }
        }

        return IsPathClear(plan, window);
    }

    private void ResolveTraversalTrack(ref TraversalPlan plan, WindowTraversalPoint point)
    {
        LeonAnimatorDriver driver = GetComponent<LeonAnimatorDriver>();
        if (driver == null || !driver.HasNativeWindowVariants || (point.isFence && !driver.HasFenceVault))
        {
            return;
        }

        if (traversalTracks == null)
        {
            traversalTracks = Resources.Load<RE4.ChapterOne.ChapterOnePairMotion>(
                RE4.ChapterOne.ChapterOnePairMotion.ResourceName);
        }

        string clipName = "LeonTraverse_0372";
        int endFrame = 126;
        if (plan.automaticDrop && point.isFence)
        {
            clipName = "LeonTraverse_0370";
            endFrame = 48;
        }
        else if (plan.automaticDrop)
        {
            clipName = "LeonTraverse_0375";
            endFrame = 40;
        }
        else if (point.isFence)
        {
            clipName = "LeonTraverse_0370";
            endFrame = 100;
        }

        if (traversalTracks != null && traversalTracks.TryGetNamedTrack(clipName, out plan.track))
        {
            plan.trackEndTime = endFrame / plan.track.clip.frameRate;
        }
    }

    private static Vector3 SampleTraversalPosition(TraversalPlan plan, float progress)
    {
        Vector3 root = plan.track.Sample(progress * plan.trackEndTime).position;
        Vector3 end = plan.track.Sample(plan.trackEndTime).position;
        float along = Mathf.Clamp01(root.z / Mathf.Max(0.1f, end.z));
        Vector3 position = Vector3.Lerp(plan.startPosition, plan.endPosition, along);
        float lateral = Mathf.Clamp(root.x - end.x * along, -0.18f, 0.18f);
        return position + plan.facingRotation * new Vector3(lateral, root.y - end.y * along, 0f);
    }

    /// <summary>ResolveTraversalSourceSide 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EnemyWindowTraversalRoute&gt;()、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、Vector3.Dot(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） Component.GetComponent&lt;EnemyWindowTraversalRoute&gt;()、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、Vector3.Dot(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="window">中文含义：窗口。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)。</param>
    private int ResolveTraversalSourceSide(WindowTraversalPoint window)
    {
        // 斜窗的无限延伸平面会穿过室内。靠窗侧移时，即使没有越过墙体，
        // 点积也可能换号；有明确平台归属的窗口必须沿用已完成翻越的区域状态。
        // route（路线）：保存 Component.GetComponent<EnemyWindowTraversalRoute>() 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool) 的参数、LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)。
EnemyWindowTraversalRoute route = window.GetComponent<EnemyWindowTraversalRoute>();
        if (route != null && route.isActiveAndEnabled && TryGetEnemyPlatformRegionState(route.platformRegionId, out bool /* 中文含义：是否平台时的通知回调。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ onPlatform))
        {
            // platformSign（平台符号）：使用位置：LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)。
int platformSign;
            if (route.platformSideSign >= 0)
            {
                platformSign = 1;
            }
            else
            {
                platformSign = -1;
            }

            if (onPlatform)
            {
                return platformSign;
            }
            else
            {
                return -platformSign;
            }
        }

        // normal（表面法线）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 normal = Vector3.ProjectOnPlane(window.NormalWorld, Vector3.up);
        // position（位置）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、LeonWindowTraversal.ResolveTraversalSourceSide(WindowTraversalPoint)。
Vector3 position;
        if (body != null)
        {
            position = body.position;
        }
        else
        {
            position = transform.position;
        }

        if (Vector3.Dot(position - window.OpeningCenterWorld, normal) >= 0f)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>TryFindGround 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)、WindowTraversalPoint.ShouldIgnoreCollider(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)、WindowTraversalPoint.ShouldIgnoreCollider(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="window">中文含义：窗口。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。</param>
    /// <param name="probePosition">中文含义：探测位置。使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。</param>
    /// <param name="groundPoint">中文含义：地面点。输出参数，由本入口写入并交给调用方。使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。</param>
    private bool TryFindGround(WindowTraversalPoint window, Vector3 probePosition, out Vector3 groundPoint)
    {
        // origin（原点）：使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
Vector3 origin = probePosition;
        origin.y = window.OpeningCenterWorld.y + window.openingHeight * 0.5f + window.groundProbeHeight;
        // hits（命中结果）：保存 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, window.groundProbeDistance, window.groundMask, QueryTriggerInteraction.Ignore);
        // nearestDistance（最近距离）：使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
float nearestDistance = float.MaxValue;
        // foundGround（是否找到的地面）：使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
bool foundGround = false;
        groundPoint = default;
        // i（当前遍历索引）：使用位置：LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
for (int i = 0; i < hits.Length; i++)
        {
            // candidate（候选）：使用位置：作为 WindowTraversalPoint.ShouldIgnoreCollider(Collider) 的参数、LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)。
Collider candidate = hits[i].collider;
            if (candidate == null)
            {
                continue;
            }

            if (candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            // 墙体侧面和其他角色不能成为翻窗的落地承载面。
            if (hits[i].normal.y < 0.45f || candidate.GetComponentInParent<VillagerController>() != null || candidate.GetComponentInParent<LeonController>() != null)
            {
                continue;
            }

            if (window.ShouldIgnoreCollider(candidate))
            {
                continue;
            }

            if (hits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            groundPoint = hits[i].point;
            foundGround = true;
        }

        return foundGround;
    }

    /// <summary>GetRootPositionOnGround 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） Mathf.Abs(float)、Transform.TransformPoint(Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="groundPoint">中文含义：地面点。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    private Vector3 GetRootPositionOnGround(Vector3 groundPoint)
    {
        // bottomOffset（底部偏移量）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonWindowTraversal.GetRootPositionOnGround(Vector3)。
float bottomOffset = 0f;
        if (bodyCollider != null && bodyCollider.direction == 1)
        {
            // scaledHeight（缩放后的高度）：使用位置：LeonWindowTraversal.GetRootPositionOnGround(Vector3)。
float scaledHeight = bodyCollider.height * Mathf.Abs(bodyCollider.transform.lossyScale.y);
            // bottomWorld（底部世界空间）：使用位置：LeonWindowTraversal.GetRootPositionOnGround(Vector3)。
float bottomWorld = bodyCollider.transform.TransformPoint(bodyCollider.center).y - scaledHeight * 0.5f;
            bottomOffset = bottomWorld - transform.position.y;
        }

        return new Vector3(groundPoint.x, groundPoint.y - bottomOffset + 0.02f, groundPoint.z);
    }

    /// <summary>HasStandingClearance 的业务处理入口；本方法直接依赖 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)、LeonWindowTraversal.GetCapsuleHeight()、LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)、LeonWindowTraversal.GetCapsuleRadius()、Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)、LeonWindowTraversal.GetCapsuleHeight()、LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)、LeonWindowTraversal.GetCapsuleRadius()、Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rootPosition">中文含义：根节点位置。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数。</param>
    /// <param name="window">中文含义：窗口。使用位置：作为 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint) 的参数、作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数。</param>
    private bool HasStandingClearance(Vector3 rootPosition, WindowTraversalPoint window)
    {
        // radius（半径）：保存 LeonWindowTraversal.GetCapsuleRadius() 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数、作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数。
float radius = GetCapsuleRadius();
        // height（高度）：保存 LeonWindowTraversal.GetCapsuleHeight() 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数。
float height = GetCapsuleHeight();
        GetCapsulePoints(rootPosition, height, radius, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top);
        // overlaps（重叠结果）：保存 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint) 的参数。
Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, window.obstacleMask, QueryTriggerInteraction.Ignore);
        return !ContainsBlockingCollider(overlaps, window);
    }

    /// <summary>IsPathClear 的业务处理入口；本方法直接依赖 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)、LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)、LeonWindowTraversal.GetCapsuleRadius()、Mathf.Max(float, float)、Mathf.Max(int, int)、Mathf.Min(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)、LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)、LeonWindowTraversal.GetCapsuleRadius()、Mathf.Max(float, float)、Mathf.Max(int, int)、Mathf.Min(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="plan">中文含义：方案。使用位置：作为 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的参数。</param>
    /// <param name="window">中文含义：窗口。使用位置：作为 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint) 的参数、作为 Mathf.Min(float, float) 的参数、作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数。</param>
    private bool IsPathClear(TraversalPlan plan, WindowTraversalPoint window)
    {
        // radius（半径）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数 等共 4 处。
float radius = Mathf.Min(GetCapsuleRadius() * 0.9f, window.openingWidth * 0.5f - 0.06f);
        if (radius <= 0.05f)
        {
            return false;
        }

        // height（高度）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数、作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)。
float height = Mathf.Min(traversalCapsuleHeight, window.openingHeight - 0.06f);
        height = Mathf.Max(height, radius * 2f + 0.02f);
        // sampleCount（采样数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)。
int sampleCount = Mathf.Max(3, pathValidationSamples);
        // i（当前遍历索引）：使用位置：LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)。
for (int i = 1; i < sampleCount - 1; i++)
        {
            // progress（进度）：使用位置：作为 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的参数。
float progress = i / (float)(sampleCount - 1);
            // rootPosition（根节点位置）：保存 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3) 的参数。
            Vector3 rootPosition = EvaluateQuadratic(plan, progress);
            // 窗口根轨沿地面移动，窗台净空按角色翻身经过的弧线验证。
            if (window.isFence && plan.track != null)
            {
                rootPosition = SampleTraversalPosition(plan, progress);
            }
            GetCapsulePoints(rootPosition, height, radius, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top);
            // overlaps（重叠结果）：保存 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint) 的参数。
Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, window.obstacleMask, QueryTriggerInteraction.Ignore);
            if (ContainsBlockingCollider(overlaps, window))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ContainsBlockingCollider 的业务处理入口；本方法直接依赖 Transform.IsChildOf(Transform)、WindowTraversalPoint.ShouldIgnorePathCollider(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)；下游（含内部回调体） Transform.IsChildOf(Transform)、WindowTraversalPoint.ShouldIgnorePathCollider(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="overlaps">中文含义：重叠结果。使用位置：LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)。</param>
    /// <param name="window">中文含义：窗口。使用位置：LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)。</param>
    private bool ContainsBlockingCollider(Collider[] overlaps, WindowTraversalPoint window)
    {
        // i（当前遍历索引）：使用位置：LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)。
for (int i = 0; i < overlaps.Length; i++)
        {
            // candidate（候选）：使用位置：作为 WindowTraversalPoint.ShouldIgnorePathCollider(Collider) 的参数、LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)。
Collider candidate = overlaps[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            if (window.ShouldIgnorePathCollider(candidate))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>GetCapsulePoints 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rootPosition">中文含义：根节点位置。使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。</param>
    /// <param name="height">中文含义：高度。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。</param>
    /// <param name="bottom">中文含义：底部。输出参数，由本入口写入并交给调用方。使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。</param>
    /// <param name="top">中文含义：顶部。输出参数，由本入口写入并交给调用方。使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。</param>
    private void GetCapsulePoints(Vector3 rootPosition, float height, float radius, out Vector3 bottom, out Vector3 top)
    {
        // rootToBottom（根节点目标底部）：使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。
float rootToBottom = 0.04f;
        if (bodyCollider != null && bodyCollider.direction == 1)
        {
            // scaledHeight（缩放后的高度）：使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。
float scaledHeight = bodyCollider.height * Mathf.Abs(bodyCollider.transform.lossyScale.y);
            // bottomWorld（底部世界空间）：使用位置：作为 Mathf.Max(float, float) 的参数。
float bottomWorld = bodyCollider.transform.TransformPoint(bodyCollider.center).y - scaledHeight * 0.5f;
            rootToBottom = Mathf.Max(0.02f, bottomWorld - transform.position.y + 0.04f);
        }

        // segment（线段）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.GetCapsulePoints(Vector3, float, float, out Vector3, out Vector3)。
float segment = Mathf.Max(0f, height - radius * 2f);
        bottom = rootPosition + Vector3.up * (rootToBottom + radius);
        top = bottom + Vector3.up * segment;
    }

    /// <summary>GetCapsuleRadius 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)、LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)、LeonWindowTraversal.TryGetDropGroundDistance(out float)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float GetCapsuleRadius()
    {
        if (bodyCollider == null)
        {
            return 0.35f;
        }

        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = bodyCollider.transform.lossyScale;
        // radialScale（径向缩放）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonWindowTraversal.GetCapsuleRadius()。
float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        return bodyCollider.radius * radialScale;
    }

    /// <summary>GetCapsuleHeight 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HasStandingClearance(Vector3, WindowTraversalPoint)；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float GetCapsuleHeight()
    {
        if (bodyCollider == null)
        {
            return 1.94f;
        }

        return bodyCollider.height * Mathf.Abs(bodyCollider.transform.lossyScale.y);
    }

    /// <summary>HandleTraversalEntered 的业务处理入口；本方法直接依赖 LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.BeginTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction)。</param>
    private void HandleTraversalEntered(AnimationActionTransaction transaction)
    {
        if (transaction.transactionId != activeTransactionId)
        {
            return;
        }

        if (transaction.fullBodyActionType != FullBodyActionType.WindowClimbThrough)
        {
            return;
        }

        if (!requestPending)
        {
            return;
        }

        requestPending = false;
        traversalStarted = true;
        movementCompleted = false;
        motionPhase = TraversalMotionPhase.Climbing;
        traversalStartedTime = Time.time;
        BeginTraversalPhysics();
        SetBodyPose(activePlan.startPosition, activePlan.facingRotation);
    }

    /// <summary>HandleTraversalCompleted 的业务处理入口；本方法直接依赖 LeonWindowTraversal.ReleaseNormalTraversalGameplay()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.ReleaseNormalTraversalGameplay()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.EndDropCamera()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction)。</param>
    private void HandleTraversalCompleted(AnimationActionTransaction transaction)
    {
        if (!traversalStarted || transaction.transactionId != activeTransactionId)
        {
            return;
        }

        if (!activePlan.automaticDrop && transaction.fullBodyActionType == FullBodyActionType.WindowClimbThrough)
        {
            ReleaseNormalTraversalGameplay();
            return;
        }

        if (activePlan.automaticDrop && transaction.fullBodyActionType == FullBodyActionType.WindowDropLand
            && motionPhase == TraversalMotionPhase.Landing)
        {
            movementCompleted = true;
            // receiverValue12（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction)。
global::RE4RThirdPersonCamera receiverValue12 = dropCamera;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                receiverValue12.EndDropCamera();
            }
        }
    }

    /// <summary>HandleTraversalExited 的业务处理入口；本方法直接依赖 LeonWindowTraversal.FinishTraversal(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.FinishTraversal(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonWindowTraversal.HandleTraversalExited(AnimationActionTransaction)。</param>
    private void HandleTraversalExited(AnimationActionTransaction transaction)
    {
        if (transaction.transactionId != activeTransactionId)
        {
            return;
        }

        FinishTraversal(true);
    }

    /// <summary>HandleTraversalInterrupted 的业务处理入口；本方法直接依赖 LeonWindowTraversal.FinishTraversal(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWindowTraversal.FinishTraversal(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonWindowTraversal.HandleTraversalInterrupted(AnimationActionTransaction)。</param>
    private void HandleTraversalInterrupted(AnimationActionTransaction transaction)
    {
        if (transaction.transactionId != activeTransactionId)
        {
            return;
        }

        FinishTraversal(false);
    }

    /// <summary>MoveAlongPlan 的业务处理入口；本方法直接依赖 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleFixedUpdate()；下游（含内部回调体） LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="progress">中文含义：进度。使用位置：作为 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的参数。</param>
    private void MoveAlongPlan(float progress, float elapsed)
    {
        // position（位置）：保存 LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float) 的返回结果，供当前步骤使用。使用位置：作为 LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 的参数。
        Vector3 position = EvaluateQuadratic(activePlan, progress);
        CommitWindowFracture(elapsed, position);
        SetBodyPose(position, activePlan.facingRotation);
    }

    /// <summary>ReleaseNormalTraversalGameplay 的业务处理入口；本方法直接依赖 LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、LeonWindowTraversal.RefreshPlatformOwnership()、LeonWindowTraversal.EndTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleFixedUpdate()、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction)；下游（含内部回调体） LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、LeonWindowTraversal.RefreshPlatformOwnership()、LeonWindowTraversal.EndTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ReleaseNormalTraversalGameplay()
    {
        if (gameplayReleased || activePlan.automaticDrop)
        {
            return;
        }

        movementCompleted = true;
        SetBodyPose(activePlan.endPosition, activePlan.facingRotation);
        RefreshPlatformOwnership();

        EndTraversalPhysics();
        gameplayReleased = true;
        // receiverValue13（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.ReleaseNormalTraversalGameplay()。
global::LeonWeaponVisualController receiverValue13 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            receiverValue13.SetTraversalWeaponVisible(true);
        }

        if (controller != null && controller.currentState != LeonState.Dead)
        {
            controller.ChangeState(LeonState.Idle);
        }
    }

    /// <summary>BeginAutomaticDrop 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.CancelTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleFixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.CancelTraversal()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginAutomaticDrop()
    {
        if (activeWindow == null || body == null)
        {
            // receiverValue14（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginAutomaticDrop()。
global::LeonAnimationActionCoordinator receiverValue14 = coordinator;
            // optionalValue15（条件访问得到的取消通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginAutomaticDrop()。
bool? optionalValue15;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                optionalValue15 = receiverValue14.CancelTraversal();
            }
            else
            {
                optionalValue15 = default;
            }

            FinishTraversal(false);
            return;
        }

        SetBodyPose(activePlan.endPosition, activePlan.facingRotation);
        if (!coordinator.AdvanceTraversalAction(FullBodyActionType.WindowDropFall, dropTransitionDuration))
        {
            // receiverValue16（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginAutomaticDrop()。
global::LeonAnimationActionCoordinator receiverValue16 = coordinator;
            // optionalValue17（条件访问得到的取消通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginAutomaticDrop()。
bool? optionalValue17;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                optionalValue17 = receiverValue16.CancelTraversal();
            }
            else
            {
                optionalValue17 = default;
            }

            FinishTraversal(false);
            return;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = previousBodyColliderEnabled;
        }

        body.isKinematic = previousKinematic;
        body.useGravity = previousUseGravity;
        // receiverValue18（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginAutomaticDrop()。
global::LeonActionMotionDriver receiverValue18 = motionExecutor;
        // optionalValue19（条件访问得到的提交角度速度的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginAutomaticDrop()。
bool? optionalValue19;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            optionalValue19 = receiverValue18.CommitAngleSpeed(traversalMotion, Vector3.zero);
        }
        else
        {
            optionalValue19 = default;
        }

        // receiverValue20（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginAutomaticDrop()。
global::LeonActionMotionDriver receiverValue20 = motionExecutor;
        // optionalValue21（条件访问得到的移动速度是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginAutomaticDrop()。
bool? optionalValue21;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            optionalValue21 = receiverValue20.CommitSpeed(traversalMotion, activePlan.travelDirection * activeWindow.automaticDropHorizontalSpeed + Vector3.down * 0.2f);
        }
        else
        {
            optionalValue21 = default;
        }

        motionPhase = TraversalMotionPhase.Falling;
        landingActionRequested = false;
        fastestDownwardVelocity = body.linearVelocity.y;
        dropStartedTime = Time.time;
        // receiverValue22（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginAutomaticDrop()。
global::RE4RThirdPersonCamera receiverValue22 = dropCamera;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.BeginDropCamera();
        }
    }

    /// <summary>UpdateAutomaticFall 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonWindowTraversal.CommitDropImpact()、LeonWindowTraversal.RequestLandingAction()、LeonWindowTraversal.TryGetDropGroundDistance(out float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleFixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonWindowTraversal.CommitDropImpact()、LeonWindowTraversal.RequestLandingAction()、LeonWindowTraversal.TryGetDropGroundDistance(out float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateAutomaticFall()
    {
        if (body == null || activeWindow == null)
        {
            return;
        }

        Vector3 velocity = body.linearVelocity;
        fastestDownwardVelocity = Mathf.Min(fastestDownwardVelocity, velocity.y);
        Vector3 horizontal = activePlan.travelDirection * activeWindow.automaticDropHorizontalSpeed;
        if (motionExecutor != null)
        {
            motionExecutor.CommitSpeed(traversalMotion, new Vector3(horizontal.x, velocity.y, horizontal.z));
        }

        if (!LeonDropContact.TryReadSupport(bodyCollider, transform, activeWindow, activeWindow.groundMask, 1.5f, out float distance))
        {
            return;
        }

        if (!landingActionRequested && LeonDropContact.IsLandingImminent(body, distance, landingPredictionLeadTime))
        {
            RequestLandingAction();
        }

        if (landingActionRequested && LeonDropContact.IsContact(body, distance, landingContactDistance))
        {
            CommitDropImpact();
        }
    }

    /// <summary>RequestLandingAction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.CommitDropImpact()、LeonWindowTraversal.UpdateAutomaticFall()；下游（含内部回调体） LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RequestLandingAction()
    {
        float blendDuration = Mathf.Min(dropTransitionDuration, 2f * Time.fixedDeltaTime);
        landingActionRequested = coordinator.AdvanceTraversalAction(FullBodyActionType.WindowDropLand, blendDuration);
    }

    /// <summary>CommitDropImpact 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitPosition(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonWindowTraversal.RequestLandingAction()、Mathf.Abs(float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.NotifyDropImpact(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.UpdateAutomaticFall()；下游（含内部回调体） LeonActionMotionDriver.CommitPosition(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonWindowTraversal.RequestLandingAction()、Mathf.Abs(float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.NotifyDropImpact(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CommitDropImpact()
    {
        motionPhase = TraversalMotionPhase.Landing;
        landingRollElapsed = LeonDropContact.RollContactTime;
        if (TryGetComponent<NativeActionAudio>(out var actionAudio))
        {
            actionAudio.PlayLandingContact(body.position, activeWindow.groundMask);
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

    /// <summary>UpdateLandingRecovery 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)、Vector3.MoveTowards(Vector3, Vector3, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleFixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)、Vector3.MoveTowards(Vector3, Vector3, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateLandingRecovery()
    {
        LeonDropContact.AdvanceRoll(body, motionExecutor, traversalMotion, coordinator, ref landingRollElapsed);
    }

    /// <summary>EvaluateQuadratic 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.IsPathClear(LeonWindowTraversal.TraversalPlan, WindowTraversalPoint)、LeonWindowTraversal.MoveAlongPlan(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="plan">中文含义：方案。使用位置：LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)。</param>
    /// <param name="progress">中文含义：进度。使用位置：LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)。</param>
    private static Vector3 EvaluateQuadratic(TraversalPlan plan, float progress)
    {
        // remaining（剩余）：使用位置：LeonWindowTraversal.EvaluateQuadratic(LeonWindowTraversal.TraversalPlan, float)。
float remaining = 1f - progress;
        return remaining * remaining * plan.startPosition + 2f * remaining * progress * plan.controlPosition + progress * progress * plan.endPosition;
    }

    /// <summary>SetBodyPose 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction)、LeonWindowTraversal.MoveAlongPlan(float)、LeonWindowTraversal.ReleaseNormalTraversalGameplay()；下游（含内部回调体） LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion) 的参数。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：作为 LeonActionMotionDriver.CommitTraversalPose(MotionControlToken, Vector3, Quaternion) 的参数。</param>
    private void SetBodyPose(Vector3 position, Quaternion rotation)
    {
        // receiverValue32（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)。
global::LeonActionMotionDriver receiverValue32 = motionExecutor;
        // optionalValue33（条件访问得到的提交通行姿态的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.SetBodyPose(Vector3, Quaternion)。
bool? optionalValue33;
        if (!global::System.Object.ReferenceEquals(receiverValue32, null))
        {
            optionalValue33 = receiverValue32.CommitTraversalPose(traversalMotion, position, rotation);
        }
        else
        {
            optionalValue33 = default;
        }
    }

    /// <summary>BeginTraversalPhysics 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.HandleTraversalEntered(AnimationActionTransaction)；下游（含内部回调体） LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginTraversalPhysics()
    {
        traversalPhysicsCaptured = true;
        if (body != null)
        {
            previousKinematic = body.isKinematic;
            previousUseGravity = body.useGravity;
            // receiverValue34（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginTraversalPhysics()。
global::LeonActionMotionDriver receiverValue34 = motionExecutor;
            // optionalValue35（条件访问得到的移动速度是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginTraversalPhysics()。
bool? optionalValue35;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                optionalValue35 = receiverValue34.CommitSpeed(traversalMotion, Vector3.zero);
            }
            else
            {
                optionalValue35 = default;
            }

            // receiverValue36（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.BeginTraversalPhysics()。
global::LeonActionMotionDriver receiverValue36 = motionExecutor;
            // optionalValue37（条件访问得到的提交角度速度的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.BeginTraversalPhysics()。
bool? optionalValue37;
            if (!global::System.Object.ReferenceEquals(receiverValue36, null))
            {
                optionalValue37 = receiverValue36.CommitAngleSpeed(traversalMotion, Vector3.zero);
            }
            else
            {
                optionalValue37 = default;
            }

            body.isKinematic = true;
            body.useGravity = false;
        }

        if (bodyCollider != null)
        {
            previousBodyColliderEnabled = bodyCollider.enabled;
            bodyCollider.enabled = false;
        }
    }

    /// <summary>EndTraversalPhysics 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.ReleaseNormalTraversalGameplay()；下游（含内部回调体） LeonActionMotionDriver.CommitAngleSpeed(MotionControlToken, Vector3)、LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EndTraversalPhysics()
    {
        if (!traversalPhysicsCaptured)
        {
            return;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = previousBodyColliderEnabled;
        }

        if (body != null)
        {
            body.isKinematic = previousKinematic;
            body.useGravity = previousUseGravity;
            // receiverValue38（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.EndTraversalPhysics()。
global::LeonActionMotionDriver receiverValue38 = motionExecutor;
            // optionalValue39（条件访问得到的移动速度是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.EndTraversalPhysics()。
bool? optionalValue39;
            if (!global::System.Object.ReferenceEquals(receiverValue38, null))
            {
                optionalValue39 = receiverValue38.CommitSpeed(traversalMotion, Vector3.zero);
            }
            else
            {
                optionalValue39 = default;
            }

            // receiverValue40（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.EndTraversalPhysics()。
global::LeonActionMotionDriver receiverValue40 = motionExecutor;
            // optionalValue41（条件访问得到的提交角度速度的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonWindowTraversal.EndTraversalPhysics()。
bool? optionalValue41;
            if (!global::System.Object.ReferenceEquals(receiverValue40, null))
            {
                optionalValue41 = receiverValue40.CommitAngleSpeed(traversalMotion, Vector3.zero);
            }
            else
            {
                optionalValue41 = default;
            }
        }

        traversalPhysicsCaptured = false;
    }

    /// <summary>FinishTraversal 的业务处理入口；本方法直接依赖 LeonController.ChangeState(LeonState)、LeonController.HandleDeferredTraversalHit(LeonHitDirection)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.RefreshPlatformOwnership()、LeonWindowTraversal.EndTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.HandleTraversalExited(AnimationActionTransaction)、LeonWindowTraversal.HandleTraversalInterrupted(AnimationActionTransaction)、LeonWindowTraversal.OnDisable()、LeonWindowTraversal.Update()；下游（含内部回调体） LeonController.ChangeState(LeonState)、LeonController.HandleDeferredTraversalHit(LeonHitDirection)、LeonWeaponVisualController.SetTraversalWeaponVisible(bool)、LeonWindowTraversal.ClearActiveTraversal()、LeonWindowTraversal.RefreshPlatformOwnership()、LeonWindowTraversal.EndTraversalPhysics()、LeonWindowTraversal.SetBodyPose(Vector3, Quaternion) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reachedDestination">中文含义：是否已抵达目的地。使用位置：LeonWindowTraversal.FinishTraversal(bool)。</param>
    private void FinishTraversal(bool reachedDestination)
    {
        // wasActive（之前是否生效）：使用位置：LeonWindowTraversal.FinishTraversal(bool)。
bool wasActive = IsTraversing;
        // wasAutomaticDrop（之前是否自动掉落）：使用位置：LeonWindowTraversal.FinishTraversal(bool)。
bool shouldPlayDeferredHit = hasDeferredHit && controller != null && controller.currentState != LeonState.Dead;
        // hitDirection（命中方向）：使用位置：作为 LeonController.HandleDeferredTraversalHit(LeonHitDirection) 的参数。
LeonHitDirection hitDirection = deferredHitDirection;
        if (traversalStarted)
        {
            if (reachedDestination && !gameplayReleased && !activePlan.automaticDrop)
            {
                SetBodyPose(activePlan.endPosition, activePlan.facingRotation);
            }

            if (reachedDestination)
            {
                RefreshPlatformOwnership();
            }

            EndTraversalPhysics();
        }

        // receiverValue43（判空前缓存的掉落摄像机）：从掉落摄像机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.FinishTraversal(bool)。
global::RE4RThirdPersonCamera receiverValue43 = dropCamera;
        if (!global::System.Object.ReferenceEquals(receiverValue43, null))
        {
            receiverValue43.EndDropCamera();
        }

        // receiverValue44（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.FinishTraversal(bool)。
global::LeonWeaponVisualController receiverValue44 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.SetTraversalWeaponVisible(true);
        }

        if (controller != null && controller.currentState != LeonState.Dead)
        {
            controller.ChangeState(LeonState.Idle);
        }

        ClearActiveTraversal();
        if (wasActive && shouldPlayDeferredHit)
        {
            controller.HandleDeferredTraversalHit(hitDirection);
        }
    }

    /// <summary>ClearActiveTraversal 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ReleaseMotion(MotionControlToken)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） LeonActionMotionDriver.ReleaseMotion(MotionControlToken)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearActiveTraversal()
    {
        // receiverValue45（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonWindowTraversal.ClearActiveTraversal()。
global::LeonActionMotionDriver receiverValue45 = motionExecutor;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.ReleaseMotion(traversalMotion);
        }

        traversalMotion = default;
        if (activeWindow != null)
        {
            activeWindow.ReleasePassage(this);
            activeWindow = null;
        }
        activePlan = default;
        requestPending = false;
        traversalStarted = false;
        movementCompleted = false;
        activeTransactionId = 0;
        hasDeferredHit = false;
        deferredHitDirection = LeonHitDirection.Center;
        motionPhase = TraversalMotionPhase.None;
        gameplayReleased = false;
        traversalPhysicsCaptured = false;
        landingActionRequested = false;
        fastestDownwardVelocity = 0f;
        dropStartedTime = 0f;
    }

    /// <summary>CacheComponents 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.Start()、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

        if (combat == null)
        {
            combat = GetComponent<LeonCombat>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (dropCamera == null)
        {
            dropCamera = Object.FindFirstObjectByType<RE4RThirdPersonCamera>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider>();
        }
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.OnEnable()、LeonWindowTraversal.Start()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnTraversalActionEntered += HandleTraversalEntered;
        coordinator.OnTraversalActionCompleted += HandleTraversalCompleted;
        coordinator.OnTraversalActionExited += HandleTraversalExited;
        coordinator.OnTraversalActionInterrupted += HandleTraversalInterrupted;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnTraversalActionEntered -= HandleTraversalEntered;
        coordinator.OnTraversalActionCompleted -= HandleTraversalCompleted;
        coordinator.OnTraversalActionExited -= HandleTraversalExited;
        coordinator.OnTraversalActionInterrupted -= HandleTraversalInterrupted;
        eventsBound = false;
    }
}
