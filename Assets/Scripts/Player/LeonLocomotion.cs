using UnityEngine;

/// <summary>玩家控制、动作与运动组件 LeonLocomotion，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>玩家刚体运动宿主，结合控制器输入、自由环顾移动参考与动作许可执行移动，并把运动参数交给现有动画适配器。</remarks>
public class LeonLocomotion : MonoBehaviour
{
    /// <summary>中文含义：当前移动阶段。使用位置：LeonAnimationActionCoordinator.PlayerSnapshot.get、LeonLocomotion.CommitMovementPhase(LeonState)。</summary>
    public LeonState MovementPhase { get; private set; } = LeonState.Idle;

    /// <summary>CommitMovementPhase 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、LeonAnimationActionCoordinator.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="phase">中文含义：阶段。使用位置：LeonLocomotion.CommitMovementPhase(LeonState)。</param>
    internal void CommitMovementPhase(LeonState phase)
    {
        if (phase == LeonState.Idle || phase == LeonState.Walking || phase == LeonState.Running || phase == LeonState.Sprinting)
        {
            MovementPhase = phase;
        }
    }

    /// <summary>中文含义：角色运动执行器。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonLocomotion.HandleMovement()、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 等共 5 处。</summary>
    [Header("运动执行引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("运动执行")]
    [UnityEngine.InspectorName("运动执行")]
    public LeonActionMotionDriver motionExecutor;
    /// <summary>中文含义：常规移动控制凭证。使用位置：作为 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion) 的参数、作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数 等共 4 处。</summary>
    private GameArchitecture.MotionControlToken movementToken;
    /// <summary>中文含义：里昂玩家控制器。使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、作为 Quaternion.Lerp(Quaternion, Quaternion, float) 的参数 等共 8 处。</summary>
    private LeonController controller;
    /// <summary>中文含义：战斗。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)、LeonLocomotion.Start()。</summary>
    private LeonCombat combat;
    /// <summary>中文含义：运动刚体。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、LeonLocomotion.HandleMovement() 等共 4 处。</summary>
    private Rigidbody rb;
    /// <summary>中文含义：胶囊碰撞体。使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonLocomotion.Start()。</summary>
    private CapsuleCollider capsuleCollider;
    // 上帧速度，用于平滑过渡
    /// <summary>中文含义：当前平滑后的移动速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonLocomotion.DampToZero()、LeonLocomotion.HandleMovement() 等共 4 处。</summary>
    private float currentSpeed;
    // 新管线移动值缓存（不再从Animator读取）
    /// <summary>中文含义：平滑后的水平移动输入。使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonLocomotion.DampToZero() 等共 6 处。</summary>
    private float smoothHorizontal;
    /// <summary>中文含义：平滑后的纵向移动输入。使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonLocomotion.DampToZero() 等共 6 处。</summary>
    private float smoothVertical;
    /// <summary>中文含义：平滑输入长度。使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 7 处。</summary>
    private float smoothInputMagnitude;
    // 新管线组件引用
    private LeonAnimatorDriver driver;
    /// <summary>中文含义：蹲伏速度。使用位置：LeonLocomotion.HandleMovement()。</summary>
    [Header("蹲伏移动")]
    [SerializeField, Min(0f)]
    private float crouchSpeed = 1.35f;
    /// <summary>中文含义：受伤行走前向速度。使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。</summary>
    [Header("受伤移动速度（受伤且非蹲伏时生效）")]
    [SerializeField, Min(0f)]
    private float injuredWalkForwardSpeed = 1.35f;
    /// <summary>中文含义：受伤行走后退速度。使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。</summary>
    [SerializeField, Min(0f)]
    private float injuredWalkBackwardSpeed = 1.10f;
    /// <summary>中文含义：受伤横移速度。使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。</summary>
    [SerializeField, Min(0f)]
    private float injuredStrafeSpeed = 0.95f;
    /// <summary>中文含义：受伤慢跑速度。使用位置：LeonLocomotion.HandleMovement()。</summary>
    [SerializeField, Min(0f)]
    private float injuredJogSpeed = 2.35f;
    /// <summary>中文含义：受伤输入阻尼系数。受伤状态下移动输入方向与幅度的平滑系数（正常状态为每秒10）。使用位置：LeonLocomotion.HandleMovement()。</summary>
    [Tooltip("受伤状态下移动输入方向与幅度的平滑系数（正常状态为每秒10）")]
    [SerializeField, Range(1f, 15f)]
    private float injuredInputDampFactor = 6f;
    /// <summary>中文含义：当前转向角度。使用位置：作为 LeonAnimatorDriver.SetTurnAngle(float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Approximately(float, float) 的参数 等共 7 处。</summary>
    private float currentTurnAngle;
    /// <summary>中文含义：冲刺起步动作期间，从当前速度平滑提升至冲刺速度的时长。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</summary>
    [Header("冲刺起停惯性")]
    [Tooltip("冲刺起步动作期间，从当前速度平滑提升至冲刺速度的时长")]
    [SerializeField, Range(0.1f, 1f)]
    private float sprintStartDuration = 0.36f;
    /// <summary>中文含义：冲刺急停动作期间保留向前惯性并完成减速的时长。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</summary>
    [Tooltip("冲刺急停动作期间保留向前惯性并完成减速的时长")]
    [SerializeField, Range(0.1f, 1f)]
    private float sprintStopDuration = 0.38f;
    /// <summary>中文含义：输入低于该值时视为松开移动键，开始冲刺急停惯性。使用位置：LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    [Tooltip("输入低于该值时视为松开移动键，开始冲刺急停惯性")]
    [SerializeField, Range(0.01f, 0.2f)]
    private float sprintStopInputThreshold = 0.05f;
    /// <summary>中文含义：是否前一次实际生效的冲刺中。使用位置：LeonLocomotion.Start()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private bool previousEffectiveSprinting;
    /// <summary>中文含义：冲刺起始是否生效。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.Start()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private bool sprintStartActive;
    /// <summary>中文含义：冲刺起始已用。使用位置：作为 Mathf.Min(float, float) 的参数、LeonLocomotion.HandleMovement()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private float sprintStartElapsed;
    /// <summary>中文含义：冲刺起始速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private float sprintStartSpeed;
    /// <summary>中文含义：冲刺停止是否生效。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.IsQuickStopDeceleration.get、LeonLocomotion.Start() 等共 4 处。</summary>
    private bool sprintStopActive;
    /// <summary>中文含义：是否快速停止减速度。使用位置：LeonFootAction.IsStepping.get。</summary>
    public bool IsQuickStopDeceleration => sprintStopActive || driver != null && driver.IsNativeSprintStopping;

    /// <summary>中文含义：冲刺停止已用。使用位置：作为 Mathf.Min(float, float) 的参数、LeonLocomotion.HandleMovement()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private float sprintStopElapsed;
    /// <summary>中文含义：冲刺停止速度。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private float sprintStopSpeed;
    /// <summary>中文含义：冲刺停止方向。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private Vector3 sprintStopDirection;
    /// <summary>中文含义：上一次移动方向。使用位置：LeonLocomotion.HandleMovement()、LeonLocomotion.Start()、LeonLocomotion.UpdateSprintTransitionState(bool, float)。</summary>
    private Vector3 lastMoveDirection;
    private LeonItemPickup itemPickup;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;Rigidbody&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        controller = GetComponent<LeonController>();
        if (motionExecutor == null)
        {
            motionExecutor = GetComponent<LeonActionMotionDriver>();
        }

        combat = GetComponent<LeonCombat>();
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        driver = GetComponent<LeonAnimatorDriver>();
        itemPickup = GetComponent<LeonItemPickup>();
        smoothHorizontal = 0f;
        smoothVertical = 0f;
        smoothInputMagnitude = 0f;
        previousEffectiveSprinting = false;
        sprintStartActive = false;
        sprintStopActive = false;
        lastMoveDirection = transform.forward;
    }

    /// <summary>HandleMovement 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimatorDriver.ReadStartStepDisplacementRatio()、LeonAnimatorDriver.SetMovement(float, float, float, bool)、LeonController.CalculateMovementDirection(Vector3)、LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)、LeonLocomotion.DampToZero()、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.FixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3)、LeonAnimatorDriver.ReadStartStepDisplacementRatio()、LeonAnimatorDriver.SetMovement(float, float, float, bool)、LeonController.CalculateMovementDirection(Vector3)、LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)、LeonLocomotion.DampToZero()、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleMovement()
    {
        if (motionExecutor != null && motionExecutor.CurrentMotionToken.Source == GameArchitecture.MotionSource.NormalMovement)
        {
            movementToken = motionExecutor.CurrentMotionToken;
        }
        else
        {
            movementToken = default;
        }

        if (controller == null)
        {
            return;
        }

        if (!controller.NewPipelineValid)
        {
            return;
        }

        if (rb == null)
        {
            return;
        }

        if (movementToken.Valid && (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput
            || GameUIManager.HasInstance && GameUIManager.Instance.IsPlayerInputBlocked))
        {
            if (driver != null)
            {
                driver.ReleaseNativeGait(false);
            }
            sprintStartActive = false;
            sprintStopActive = false;
            previousEffectiveSprinting = false;
            currentSpeed = 0f;
            motionExecutor.ClearNativeGaitObstruction();
            motionExecutor.StopHorizontalSpeed(movementToken);
            return;
        }

        // state（当前里昂状态）：使用位置：LeonLocomotion.HandleMovement()。
var state = controller.currentState;
        if (state == LeonState.Staggered && driver != null && driver.HitMovementReleased)
            state = LeonState.Walking;
        // input（输入数据）：使用位置：作为 LeonController.CalculateMovementDirection(Vector3) 的参数、作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、LeonLocomotion.HandleMovement()。
var input = controller.MoveInput;
        // cam（摄像机）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、作为 LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool) 的参数。
var cam = controller.playerCamera;
        // 死亡、受击期间限制移动
        if (combat != null && combat.IsPositionedMelee)
        {
            if (driver != null)
            {
                driver.ReleaseNativeGait(false);
                driver.SetMovement(0f, 0f, 0f, false);
            }

            ResetTurnAngle();
            sprintStartActive = false;
            sprintStopActive = false;
            smoothHorizontal = 0f;
            smoothVertical = 0f;
            smoothInputMagnitude = 0f;
            currentSpeed = 0f;
            if (motionExecutor != null)
            {
                if (combat.TryGetPositionedMeleeMotion(out Quaternion facing, out Vector3 meleeVelocity))
                {
                    motionExecutor.CommitNormalOrientation(movementToken, Quaternion.RotateTowards(rb.rotation, facing, 720f * Time.fixedDeltaTime));
                }
                meleeVelocity.y = rb.linearVelocity.y;
                motionExecutor.CommitSpeed(movementToken, meleeVelocity);
            }
            return;
        }

        if (state == LeonState.Dead || state == LeonState.Staggered || state == LeonState.QTE || state == LeonState.Traversing)
        {
            if (driver != null)
            {
                driver.ReleaseNativeGait(false);
            }
            ResetTurnAngle();
            DampToZero();
            // receiverValue2（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.HandleMovement()。
global::LeonActionMotionDriver receiverValue2 = motionExecutor;
            // optionalValue3（条件访问得到的移动速度是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonLocomotion.HandleMovement()。
bool? optionalValue3;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                optionalValue3 = receiverValue2.CommitSpeed(movementToken, new Vector3(0f, rb.linearVelocity.y, 0f));
            }
            else
            {
                optionalValue3 = default;
            }

            return;
        }

        // isShooting（是否正在射击）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、作为 LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool) 的参数、LeonLocomotion.HandleMovement()。
bool isShooting = state == LeonState.Shooting;
        // isAiming（是否正在瞄准）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、作为 LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool) 的参数、LeonLocomotion.HandleMovement()。
bool isAiming = controller.IsAiming;
        // isReloading（是否正在换弹）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、作为 LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool) 的参数、LeonLocomotion.HandleMovement()。
bool isReloading = state == LeonState.Reloading;
        // isMeleeing（是否近战中）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数、作为 LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool) 的参数、LeonLocomotion.HandleMovement()。
bool isMeleeing = state == LeonState.MeleeAttacking || combat != null && combat.knifeModule != null && combat.knifeModule.Active;
        // isKnifeAttack（是否小刀攻击）：使用位置：作为 LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3) 的参数。
bool isKnifeAttack = combat != null && combat.IsKnifeAttackActive;
        // isCrouching（是否正在蹲伏）：使用位置：LeonLocomotion.HandleMovement()。
bool isCrouching = controller.IsCrouching;
        // 原地举枪继续使用持枪待机的骨盆和脊椎。方向输入仍交给瞄准步态。
        bool keepNativeAimIdle = driver != null && !driver.IsNativeGaitMoving && input.sqrMagnitude <= 0.0025f
            && (driver.NativeCqbrOwnsAimArms && driver.IsNativeCqbrCarrying
                || driver.NativeW870OwnsAimArms && driver.IsNativeW870Carrying
                || driver.NativePistolOwnsAimArms && driver.IsNativePistolCarrying);
        bool injured = controller.IsInjured;
        // 松开格挡时先恢复持刀基础层，收势直接淡入真实待机或当前步态。
        bool releasingGuard = state == LeonState.Parrying && combat != null && combat.knifeModule != null
            && combat.knifeModule.CurrentAction == GameArchitecture.KnifeAction.LowerGuard
            && !combat.knifeModule.IsFinishingThrust;
        bool allowNativeMovement = !isMeleeing || releasingGuard;
        bool isolateCameraSteering = controller.IsolateObserveCameraSteering;
        // 自由环顾只锁定移动参考，进入观察和回正时都沿用当前步态。
        bool basicGait = driver != null && movementToken.Valid && driver.CanUseBasicGait
            && !injured && !isCrouching && !isAiming && !isShooting && !isReloading && allowNativeMovement
            && (IsBasicMovementState(state) || releasingGuard);
        bool running = basicGait && controller.IsEffectiveSprinting;
        bool sprintingForward = !basicGait && controller.IsEffectiveSprinting && input.z > .1f;
        var direction = LeonAnimatorDriver.NativeDirection.Forward;
        if (!isolateCameraSteering && !running && !sprintingForward)
        {
            direction = ReadDirectionalInput(input);
        }
        else
        {
            lastDirectionalInput = LeonAnimatorDriver.NativeDirection.Forward;
        }

        bool directionalInput = direction != LeonAnimatorDriver.NativeDirection.Forward;
        bool nativeAim = isAiming && (controller.currentWeapon == WeaponType.Pistol || injured);
        bool directionalRequest = directionalInput || nativeAim && input.sqrMagnitude > .0025f;
        bool nativeDirectional = directionalRequest && driver != null && !isCrouching &&
            driver.CanUseNativeDirectionalGait(direction, nativeAim, injured);
        bool finishDirectional = driver != null && input.sqrMagnitude <= .0025f &&
            (driver.IsNativeDirectionalMoving || nativeAim && driver.IsNativeGaitMoving);
        bool nativeGait = driver != null && motionExecutor != null && movementToken.Valid
            && driver.CanUseNativeGait(isCrouching, injured)
            && (!directionalRequest || nativeDirectional)
            && (!isAiming && !isShooting || keepNativeAimIdle || nativeDirectional || finishDirectional)
            && (!isReloading || driver.PreservesNativeReloadGait) && allowNativeMovement;
        if (isShooting || isAiming || isReloading || isMeleeing)
        {
            ResetTurnAngle();
        }

        ResetTurnAngle();

        Vector3 moveDir = controller.CalculateMovementDirection(input);
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = moveDir;
        }

        // localMove（局部移动）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数。
Vector3 localMove = Quaternion.Inverse(rb.rotation) * moveDir;
        // inputMag（输入弹匣）：使用位置：作为 LeonLocomotion.UpdateSprintTransitionState(bool, float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonLocomotion.HandleMovement()。
float inputMag = input.magnitude;
        // 受伤时输入方向平滑从每秒 10 降为每秒 6，正常状态继续使用原值
        // leftOperand7（计算前缓存的左侧物理步时间间隔）：从物理步时间间隔读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonLocomotion.HandleMovement()。
float leftOperand7 = Time.fixedDeltaTime;
        // selectedValue6（分支选出的受伤输入阻尼系数）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonLocomotion.HandleMovement()。
float selectedValue6;
        if (controller.IsInjured)
        {
            selectedValue6 = injuredInputDampFactor;
        }
        else
        {
            selectedValue6 = 10f;
        }

        // 受伤时输入方向平滑从每秒 10 降为每秒 6，正常状态继续使用原值
        // dampFactor（阻尼系数）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float dampFactor = leftOperand7 * (selectedValue6);
        // walkFwd（行走前向）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float walkFwd = 1.83f;
        // walkBwd（行走后向）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float walkBwd = 1.68f;
        // strafeLeft（横移左侧）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float strafeLeft = 1.51f;
        // strafeRight（横移右侧）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float strafeRight = 1.95f;
        // runFwd（运行前向）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float runFwd = 3.24f;
        // runBwd（运行后向）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float runBwd = 3.24f;
        // strafeRunLeft（横移运行左侧）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float strafeRunLeft = 3.24f;
        // strafeRunRight（横移运行右侧）：使用位置：作为 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的参数。
float strafeRunRight = 3.24f;
        // knifeActive（小刀是否生效）：使用位置：LeonLocomotion.HandleMovement()。
bool knifeActive = combat != null && combat.knifeModule != null && combat.knifeModule.Active;
        // isSprinting（是否正在冲刺）：使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、作为 LeonLocomotion.UpdateSprintTransitionState(bool, float) 的参数、LeonLocomotion.HandleMovement()。
bool isSprinting = controller.IsEffectiveSprinting && !knifeActive && !directionalInput;
        UpdateSprintTransitionState(isSprinting, inputMag);
        // targetInputMag（目标输入弹匣）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonLocomotion.HandleMovement()。
float targetInputMag;
        if (knifeActive)
        {
            targetInputMag = Mathf.Min(.5f, inputMag);
        }
        else
        {
            if (isSprinting)
            {
                targetInputMag = 0.55f;
            }
            else
            {
                targetInputMag = inputMag;
            }
        }

        // 使用自身缓存进行平滑，不读取Animator
        smoothHorizontal = Mathf.Lerp(smoothHorizontal, localMove.x, dampFactor);
        smoothVertical = Mathf.Lerp(smoothVertical, localMove.z, dampFactor);
        smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, targetInputMag, dampFactor);
        // 通过Driver写入动画参数
        if (driver != null)
        {
            driver.SetMovement(smoothHorizontal, smoothVertical, smoothInputMagnitude, isSprinting);
        }

        if ((!nativeGait || !basicGait) && (!nativeGait || inputMag > 0.05f || !driver.IsNativeGaitMoving))
        {
            HandleRotation(input, cam, isShooting, isAiming, isReloading, isMeleeing, isKnifeAttack, moveDir, directionalInput);
        }
        bool keepNativeInjuryDuringKnife = knifeActive && controller.IsInjured && driver != null
            && controller.currentWeapon == WeaponType.Knife && driver.CanUseNativeGait(isCrouching, true);
        if (keepNativeInjuryDuringKnife)
        {
            driver.HoldNativeInjuryDuringKnifeAction();
        }
        if (motionExecutor != null && combat != null && combat.knifeModule != null
            && combat.knifeModule.ConsumeRootMotion(Time.fixedDeltaTime, out var knifeStep))
        {
            if (driver != null && !keepNativeInjuryDuringKnife)
            {
                driver.ReleaseNativeGait(false);
            }
            Vector3 velocityFromKnife = knifeStep / Time.fixedDeltaTime;
            velocityFromKnife.y = rb.linearVelocity.y;
            motionExecutor.CommitSpeed(movementToken, velocityFromKnife);
            currentSpeed = Vector3.ProjectOnPlane(velocityFromKnife, Vector3.up).magnitude;
            return;
        }

        if (nativeGait)
        {
            sprintStartActive = false;
            sprintStopActive = false;
            float paceInput = inputMag;
            Vector3 obstructionHeading = moveDir;
            if (driver.IsBasicRunTurning || driver.IsBasicWalkBraking)
            {
                obstructionHeading = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).normalized;
                if (obstructionHeading.sqrMagnitude < 0.01f)
                {
                    obstructionHeading = moveDir;
                }
            }
            bool obstructed = motionExecutor.CheckNativeGaitObstruction(obstructionHeading);
            if (obstructed)
            {
                paceInput = 0f;
                driver.CancelBasicGaitTransition();
            }
            Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            currentSpeed = planarVelocity.magnitude;
            float runLean = 0f;
            bool forwardRun = running && input.z > 0.1f;
            if (forwardRun)
            {
                runLean = Mathf.Clamp(input.x / input.z, -1f, 1f);
            }
            Vector3 facing = rb.rotation * Vector3.forward;
            float incomingSpeed = currentSpeed;
            if (obstructed)
            {
                incomingSpeed = 0f;
            }
            driver.DriveNativeGait(moveDir, paceInput, isSprinting, isCrouching, injured, direction, nativeAim,
                basicGait, running, forwardRun, runLean, facing, incomingSpeed);
            if (basicGait && inputMag > 0.05f && !driver.IsBasicRunTurning)
            {
                Vector3 desiredFacing = moveDir;
                if (!running && !isolateCameraSteering && cam != null)
                {
                    desiredFacing = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * Vector3.forward;
                }
                if (desiredFacing.sqrMagnitude > 0.01f)
                {
                    motionExecutor.CommitNormalOrientation(movementToken, Quaternion.RotateTowards(
                        rb.rotation, Quaternion.LookRotation(desiredFacing), 540f * Time.fixedDeltaTime));
                }
            }
            float pickupEntrySpeed = 0f;
            if (!obstructed && paceInput > 0.05f && itemPickup != null)
            {
                pickupEntrySpeed = itemPickup.MovingEntrySpeed * Mathf.Clamp01(paceInput);
            }
            motionExecutor.PrepareNativeGaitStep(movementToken, planarVelocity, pickupEntrySpeed);
            return;
        }
        if (driver != null && !keepNativeInjuryDuringKnife)
        {
            driver.ReleaseNativeGait(inputMag > 0.05f, isCrouching);
        }
        if (motionExecutor != null)
        {
            motionExecutor.ClearNativeGaitObstruction();
        }
        // 速度计算（使用自身缓存的InputMagnitude）
        // 受伤且非蹲伏时使用受伤步速/慢跑速度；瞄准、射击与换弹也用受伤步行速度。
        // 刚体仍是唯一位移来源，不读取受伤动画根位移。
        // loopSpeed（循环速度）：使用位置：LeonLocomotion.HandleMovement()。
float loopSpeed;
        if (isCrouching)
        {
            loopSpeed = crouchSpeed;
        }
        else if (controller.IsInjured)
        {
            if (isSprinting)
            {
                loopSpeed = injuredJogSpeed;
            }
            else
            {
                // 瞄准、射击与换弹也使用受伤步行速度（多为后退/横移方向）
                loopSpeed = ComputeDirectionalSpeed(localMove, injuredWalkForwardSpeed, injuredWalkBackwardSpeed, injuredStrafeSpeed, injuredStrafeSpeed);
            }
        }
        else if (isSprinting)
        {
            loopSpeed = controller.runSpeed;
        }
        else if (isShooting || isAiming || isReloading)
        {
            loopSpeed = ComputeDirectionalSpeed(localMove, walkFwd, walkBwd, strafeLeft, strafeRight);
        }
        else
        {
            if (smoothInputMagnitude > 0.5f)
            {
                // blend（混合）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float blend = Mathf.Clamp01((smoothInputMagnitude - 0.5f) / 0.5f);
                // walkSpd（行走速度）：保存 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float walkSpd = ComputeDirectionalSpeed(localMove, walkFwd, walkBwd, strafeLeft, strafeRight);
                // runSpd（运行速度）：保存 LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float runSpd = ComputeDirectionalSpeed(localMove, runFwd, runBwd, strafeRunLeft, strafeRunRight);
                loopSpeed = Mathf.Lerp(walkSpd, runSpd, blend);
            }
            else
            {
                loopSpeed = ComputeDirectionalSpeed(localMove, walkFwd, walkBwd, strafeLeft, strafeRight);
            }
        }

        if (knifeActive)
        {
            loopSpeed = ComputeDirectionalSpeed(localMove, walkFwd, walkBwd, strafeLeft, strafeRight) * combat.knifeModule.MovementRatio;
        }

        // targetSpeed（目标速度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.Min(float, float) 的参数。
float targetSpeed = inputMag * loopSpeed;
        // velocityDirection（速度方向）：使用位置：作为 LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(LeonController, Rigidbody, CapsuleCollider, Vector3, LeonActionMotionDriver, MotionControlToken) 的参数、LeonLocomotion.HandleMovement()。
Vector3 velocityDirection = moveDir;
        // finalSpeed（最终速度）：使用位置：作为 Mathf.Min(float, float) 的参数、LeonLocomotion.HandleMovement()。
float finalSpeed;
        if (sprintStopActive)
        {
            sprintStopElapsed = Mathf.Min(sprintStopElapsed + Time.fixedDeltaTime, sprintStopDuration);
            // progress（进度）：使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数、LeonLocomotion.HandleMovement()。
float progress = sprintStopElapsed / Mathf.Max(0.01f, sprintStopDuration);
            // 前半段保留较多速度，后半段快速刹停，贴合急停动作的重心前送。
            // remainingSpeed（剩余速度）：使用位置：LeonLocomotion.HandleMovement()。
float remainingSpeed = 1f - Mathf.SmoothStep(0f, 1f, progress);
            finalSpeed = sprintStopSpeed * remainingSpeed;
            velocityDirection = sprintStopDirection;
            if (progress >= 1f)
            {
                sprintStopActive = false;
            }
        }
        else if (sprintStartActive)
        {
            sprintStartElapsed = Mathf.Min(sprintStartElapsed + Time.fixedDeltaTime, sprintStartDuration);
            // progress（进度）：使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数、LeonLocomotion.HandleMovement()。
float progress = sprintStartElapsed / Mathf.Max(0.01f, sprintStartDuration);
            // 起步先让重心压低，再逐步把位移提升到循环冲刺速度。
            finalSpeed = Mathf.Lerp(sprintStartSpeed, targetSpeed, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                sprintStartActive = false;
            }
        }
        else
        {
            finalSpeed = Mathf.Lerp(currentSpeed, targetSpeed, dampFactor);
            if (driver != null && inputMag > 0f && !isSprinting && !controller.IsInjured && !isCrouching && !isMeleeing && !isReloading && !isAiming && !isShooting)
            {
                // startStepRatio（起始步进比例）：保存 LeonAnimatorDriver.ReadStartStepDisplacementRatio() 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、LeonLocomotion.HandleMovement()。
float startStepRatio = driver.ReadStartStepDisplacementRatio();
                if (startStepRatio < 1f)
                {
                    finalSpeed = Mathf.Min(finalSpeed, targetSpeed * startStepRatio);
                }
            }
        }

        if (knifeActive)
        {
            finalSpeed = Mathf.Min(finalSpeed, targetSpeed);
        }

        currentSpeed = finalSpeed;
        // velocity（速度）：使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、LeonLocomotion.HandleMovement()。
Vector3 velocity = velocityDirection * finalSpeed;
        velocity.y = rb.linearVelocity.y;
        // receiverValue10（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.HandleMovement()。
global::LeonActionMotionDriver receiverValue10 = motionExecutor;
        // optionalValue11（条件访问得到的移动速度是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonLocomotion.HandleMovement()。
bool? optionalValue11;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            optionalValue11 = receiverValue10.CommitSpeed(movementToken, velocity);
        }
        else
        {
            optionalValue11 = default;
        }

        if (!LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(controller, rb, capsuleCollider, velocityDirection, motionExecutor, movementToken) &&
            motionExecutor != null && controller.IsGrounded && finalSpeed > 0.1f)
        {
            motionExecutor.AssistWalkingStep(velocityDirection, finalSpeed);
        }
    }

    internal void ApplyNativeGaitStairAssist(Vector3 direction, GameArchitecture.MotionControlToken token)
    {
        LeonPlayerStairRampRuntime.ApplyPlayerMotionAssist(controller, rb, capsuleCollider, direction, motionExecutor, token);
    }

    internal bool HasGroundForTerrainStep => controller != null && controller.IsGrounded;

    private static bool IsBasicMovementState(LeonState state)
    {
        return state == LeonState.Idle || state == LeonState.Walking
            || state == LeonState.Running || state == LeonState.Sprinting;
    }

    /// <summary>
    /// 将输入状态变化转换为冲刺起步和急停的位移阶段。
    /// 急停只在松开移动键时触发；松开冲刺键但继续移动会自然降回普通跑步。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="isSprinting">中文含义：是否正在冲刺。使用位置：LeonLocomotion.UpdateSprintTransitionState(bool, float)。</param>
    /// <param name="inputMagnitude">中文含义：输入长度。使用位置：LeonLocomotion.UpdateSprintTransitionState(bool, float)。</param>
    private void UpdateSprintTransitionState(bool isSprinting, float inputMagnitude)
    {
        if (isSprinting && !previousEffectiveSprinting)
        {
            sprintStartActive = true;
            sprintStartElapsed = 0f;
            sprintStartSpeed = currentSpeed;
            sprintStopActive = false;
        }

        if (previousEffectiveSprinting && !isSprinting && inputMagnitude <= sprintStopInputThreshold)
        {
            sprintStopActive = currentSpeed > 0.01f;
            sprintStopElapsed = 0f;
            sprintStopSpeed = currentSpeed;
            if (lastMoveDirection.sqrMagnitude > 0.0001f)
            {
                sprintStopDirection = lastMoveDirection.normalized;
            }
            else
            {
                sprintStopDirection = transform.forward;
            }

            sprintStartActive = false;
        }

        if (sprintStopActive && inputMagnitude > sprintStopInputThreshold)
        {
            sprintStopActive = false;
        }

        if (!isSprinting)
        {
            sprintStartActive = false;
        }

        previousEffectiveSprinting = isSprinting;
    }

    /// <summary>
    /// 快速反应事件成功退出时，立即把仍按住的移动方向写入基础移动层。
    /// 全身动作层随后淡出时会直接露出对应移动姿态，避免先闪回站立再起步。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.FinishQTEState(bool)；下游（含内部回调体） LeonAnimatorDriver.SetMovement(float, float, float, bool)、Transform.InverseTransformDirection(Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResumeAfterQTESuccess()
    {
        if (controller == null || driver == null)
        {
            return;
        }

        // input（输入数据）：使用位置：LeonLocomotion.ResumeAfterQTESuccess()。
Vector3 input = controller.MoveInput;
        // playerCamera（玩家摄像机）：使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、LeonLocomotion.ResumeAfterQTESuccess()。
Camera playerCamera = controller.playerCamera;
        if (input.sqrMagnitude <= 0.0001f || playerCamera == null)
        {
            smoothHorizontal = 0f;
            smoothVertical = 0f;
            smoothInputMagnitude = 0f;
            driver.SetMovement(0f, 0f, 0f, false);
            return;
        }

        // cameraForward（摄像机前向）：使用位置：LeonLocomotion.ResumeAfterQTESuccess()。
Vector3 cameraForward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        // cameraRight（摄像机右侧）：使用位置：LeonLocomotion.ResumeAfterQTESuccess()。
Vector3 cameraRight = Vector3.ProjectOnPlane(playerCamera.transform.right, Vector3.up).normalized;
        // moveDirection（移动方向）：使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数。
Vector3 moveDirection = (cameraForward * input.z + cameraRight * input.x).normalized;
        // localMove（局部移动）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonLocomotion.ResumeAfterQTESuccess()。
Vector3 localMove = transform.InverseTransformDirection(moveDirection);
        smoothHorizontal = localMove.x;
        smoothVertical = localMove.z;
        smoothInputMagnitude = input.magnitude;
        driver.SetMovement(smoothHorizontal, smoothVertical, smoothInputMagnitude, controller.IsEffectiveSprinting);
    }

#if false
    /// <summary>
    /// 以前方最近立面为起点确认完整踏面，再将角色贴合到踏面高度。
    /// 不保留向上惯性，避免逐级抛起造成卡顿以及楼梯末端短暂浮空。
    /// </summary>

    /// <remarks>历史停用方法，位于 #if false，不参与运行时调用链。旧流程在本类方法间同步传递探测参数和结果；位置写入沿已有 CharacterMotionExecutor 凭证入口完成。</remarks>
    /// <param name = "moveDirection">当前移动方向，用于确定楼梯探测朝向。</param>
    /// <param name = "velocity">按引用传入的运动速度；旧流程成功贴合踏面后将竖直速度归零。</param>
    private void ApplyStairAssist(Vector3 moveDirection, ref Vector3 velocity)
    {
        if (capsuleCollider == null || !controller.IsGrounded || moveDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        // scale：胶囊所在对象的世界缩放，用于将半径、中心和高度换算到世界空间。
        Vector3 scale = transform.lossyScale;
        // radius：按对象横向缩放换算后的胶囊半径，用于探测距离和净空检测。
        float radius = capsuleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        // height：世界空间胶囊高度，至少覆盖两倍半径。
        float height = Mathf.Max(radius * 2f, capsuleCollider.height * Mathf.Abs(scale.y));
        // bottomOffset：胶囊底部相对角色根节点的高度偏移，用于求脚底高度。
        float bottomOffset = capsuleCollider.center.y * scale.y - height * 0.5f;
        // horizontalDirection：贴在水平面上的单位移动方向，用于朝楼梯立面和踏面发射探测。
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;
        // currentFootHeight：本次探测使用的脚底世界高度，是踏面上升量的基准。
        float currentFootHeight = rb.position.y + bottomOffset;
        if (!HasStableBaseGround(currentFootHeight))
        {
            return;
        }

        if (!HasStairRiser(horizontalDirection, currentFootHeight, radius, out RaycastHit /* riserHit：当前候选楼梯立面的射线命中结果。 */
        riserHit))
        {
            return;
        }

        if (!TryFindStableTread(horizontalDirection, riserHit.point, currentFootHeight, out RaycastHit /* hit：通过踏面稳定性筛选后的命中结果。 */
        hit))
        {
            return;
        }

        // rise：候选踏面相对当前脚底的上升高度，用于限制单次抬升。
        float rise = hit.point.y - currentFootHeight;
        if (rise < Mathf.Max(0.08f, stairMinimumRise) || rise > Mathf.Min(stairAssistHeight, stairMaximumRise, 0.26f))
        {
            return;
        }

        // sameTread：候选踏面是否与最近一次辅助使用的踏面等高，用于避免重复抬升。
        bool sameTread = Mathf.Abs(hit.point.y - lastStairAssistHeight) <= Mathf.Max(0.005f, stairTreadHeightTolerance);
        if (sameTread && Time.time < nextStairAssistTime)
        {
            return;
        }

        // candidateRoot：通过净空检查后准备提交的角色根节点位置。
        Vector3 candidateRoot = rb.position + Vector3.up * (rise + Mathf.Max(0f, stairLandingOffset));
        // candidateCenter：候选位置下胶囊中心的世界坐标。
        Vector3 candidateCenter = candidateRoot + transform.TransformVector(capsuleCollider.center);
        // halfSegment：胶囊圆柱部分的半长，用于计算两端球心。
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        // top：候选胶囊上端球心，用于胶囊重叠检测。
        Vector3 top = candidateCenter + Vector3.up * halfSegment;
        // bottom：候选胶囊下端球心，用于胶囊重叠检测。
        Vector3 bottom = candidateCenter - Vector3.up * halfSegment;
        // overlapCount：本次胶囊净空检查写入预分配结果数组的数量。
        int overlapCount = Physics.OverlapCapsuleNonAlloc(top, bottom, Mathf.Max(0.01f, radius - 0.02f), stairClearanceResults, stairGroundMask, QueryTriggerInteraction.Ignore);
        // i：当前命中结果索引；仅遍历本次检测返回的有效区间。
        for (int i = 0; i < overlapCount; i++)
        {
            // other：当前净空检查命中的碰撞体，用于排除自身和目标踏面。
            Collider other = stairClearanceResults[i];
            if (other == null || other == capsuleCollider || other.transform == transform || other.transform.IsChildOf(transform) || other == hit.collider)
            {
                continue;
            }

            return;
        }

        if (motionExecutor != null)
        {
            motionExecutor.CommitPosition(movementToken, candidateRoot);
        }

        velocity.y = 0f;
        lastStairAssistHeight = hit.point.y;
        nextStairAssistTime = Time.time + Mathf.Max(0.12f, stairAssistInterval);
    }

    /// <summary>旧楼梯流程的脚下支撑检查，从当前脚底附近向下探测静态近水平地面。</summary>
    /// <remarks>历史停用方法，位于 #if false，不参与运行时调用链。旧流程在本类方法间同步传递探测参数和结果；位置写入沿已有 CharacterMotionExecutor 凭证入口完成。</remarks>
    /// <param name = "currentFootHeight">本次探测使用的脚底世界高度，是踏面上升量的基准。</param>
    private bool HasStableBaseGround(float currentFootHeight)
    {
        // origin：本次射线探测的世界空间起点。
        Vector3 origin = new Vector3(rb.position.x, currentFootHeight + 0.18f, rb.position.z);
        // hitCount：本次射线检测写入预分配结果数组的有效数量。
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, stairSurfaceHits, 0.32f, stairGroundMask, QueryTriggerInteraction.Ignore);
        // nearestDistance：当前已接受候选的最近射线距离；初始最大值表示尚未找到候选。
        float nearestDistance = float.MaxValue;
        // i：当前命中结果索引；仅遍历本次检测返回的有效区间。
        for (int i = 0; i < hitCount; i++)
        {
            // groundHit：当前脚下支撑面的射线命中结果。
            RaycastHit groundHit = stairSurfaceHits[i];
            if (!IsValidStaticStairSurface(groundHit.collider) || groundHit.distance >= nearestDistance)
            {
                continue;
            }

            if (Mathf.Abs(groundHit.point.y - currentFootHeight) > 0.08f)
            {
                continue;
            }

            if (Vector3.Angle(groundHit.normal, Vector3.up) > Mathf.Clamp(stairMaximumBaseAngle, 1f, 8f))
            {
                continue;
            }

            nearestDistance = groundHit.distance;
            return true;
        }

        return false;
    }

    /// <summary>旧楼梯流程的立面筛选，沿移动方向寻找距离、角度和朝向均有效的最近静态表面。</summary>
    /// <remarks>历史停用方法，位于 #if false，不参与运行时调用链。旧流程在本类方法间同步传递探测参数和结果；位置写入沿已有 CharacterMotionExecutor 凭证入口完成。</remarks>
    /// <param name = "horizontalDirection">贴在水平面上的单位移动方向，用于朝楼梯立面和踏面发射探测。</param>
    /// <param name = "currentFootHeight">本次探测使用的脚底世界高度，是踏面上升量的基准。</param>
    /// <param name = "radius">按对象横向缩放换算后的胶囊半径，用于探测距离和净空检测。</param>
    /// <param name = "resolvedRiser">输出经过静态表面、角度和朝向筛选的最近立面命中。</param>
    private bool HasStairRiser(Vector3 horizontalDirection, float currentFootHeight, float radius, out RaycastHit resolvedRiser)
    {
        resolvedRiser = default;
        // origin：本次射线探测的世界空间起点。
        Vector3 origin = new Vector3(rb.position.x, currentFootHeight + Mathf.Min(Mathf.Max(0.02f, stairRiserProbeHeight), stairAssistHeight * 0.5f), rb.position.z);
        // maximumDistance：立面前向探测的最大距离，由胶囊半径和前探配置共同决定。
        float maximumDistance = radius + Mathf.Max(0.01f, stairForwardProbe) + 0.08f;
        // hitCount：本次射线检测写入预分配结果数组的有效数量。
        int hitCount = Physics.RaycastNonAlloc(origin, horizontalDirection, stairRiserHits, maximumDistance, stairGroundMask, QueryTriggerInteraction.Ignore);
        // nearestDistance：当前已接受候选的最近射线距离；初始最大值表示尚未找到候选。
        float nearestDistance = float.MaxValue;
        // i：当前命中结果索引；仅遍历本次检测返回的有效区间。
        for (int i = 0; i < hitCount; i++)
        {
            // riserHit：当前候选楼梯立面的射线命中结果。
            RaycastHit riserHit = stairRiserHits[i];
            // surface：待核对的碰撞表面，过滤自身、触发器和动态角色。
            Collider surface = riserHit.collider;
            if (surface == null || surface == capsuleCollider || surface.transform == transform || surface.transform.IsChildOf(transform) || !IsValidStaticStairSurface(surface) || riserHit.distance >= nearestDistance)
            {
                continue;
            }

            // surfaceAngle：立面法线与竖直方向的夹角，用于筛选近似竖直表面。
            float surfaceAngle = Vector3.Angle(riserHit.normal, Vector3.up);
            if (surfaceAngle < Mathf.Clamp(stairMinimumRiserAngle, 75f, 89f))
            {
                continue;
            }

            // horizontalNormal：立面法线在水平面的投影，用于检查其朝向是否迎向角色。
            Vector3 horizontalNormal = riserHit.normal;
            horizontalNormal.y = 0f;
            if (horizontalNormal.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            horizontalNormal.Normalize();
            if (Vector3.Dot(horizontalDirection, -horizontalNormal) < 0.5f)
            {
                continue;
            }

            nearestDistance = riserHit.distance;
            resolvedRiser = riserHit;
        }

        return nearestDistance < float.MaxValue;
    }

    /// <summary>旧楼梯流程的踏面检查，在两个水平深度采样并核对静态表面、上升高度和踏面连续性。</summary>
    /// <remarks>历史停用方法，位于 #if false，不参与运行时调用链。旧流程在本类方法间同步传递探测参数和结果；位置写入沿已有 CharacterMotionExecutor 凭证入口完成。</remarks>
    /// <param name = "horizontalDirection">贴在水平面上的单位移动方向，用于朝楼梯立面和踏面发射探测。</param>
    /// <param name = "riserPoint">已识别立面的命中点，作为踏面深度采样的基点。</param>
    /// <param name = "currentFootHeight">本次探测使用的脚底世界高度，是踏面上升量的基准。</param>
    /// <param name = "resolvedTread">输出通过深度和高度一致性检查的踏面命中结果。</param>
    private bool TryFindStableTread(Vector3 horizontalDirection, Vector3 riserPoint, float currentFootHeight, out RaycastHit resolvedTread)
    {
        resolvedTread = default;
        // minimumDepth：踏面必须提供的最小水平支撑深度。
        float minimumDepth = Mathf.Max(0.18f, stairMinimumTreadDepth);
        // sampleDepths：立面后方的近端和远端踏面探测深度。
        float[] sampleDepths =
        {
            Mathf.Min(0.06f, minimumDepth * 0.35f),
            minimumDepth
        };
        // expectedSurface：首个深度样本接受的碰撞表面，后续样本据此核对连续性。
        Collider expectedSurface = null;
        // expectedTreadHeight：首个深度样本接受的踏面高度，后续样本据此核对平整度。
        float expectedTreadHeight = 0f;
        // maximumRise：现有配置共同限制后的单次最大台阶高度。
        float maximumRise = Mathf.Min(stairAssistHeight, stairMaximumRise, 0.26f);
        // sampleIndex：当前踏面深度样本的索引，首个样本建立一致性基准。
        for (int sampleIndex = 0; sampleIndex < sampleDepths.Length; sampleIndex++)
        {
            // samplePosition：当前深度样本的世界坐标和向下探测起点。
            Vector3 samplePosition = riserPoint + horizontalDirection * sampleDepths[sampleIndex];
            samplePosition.y = currentFootHeight + maximumRise + 0.12f;
            // hitCount：本次射线检测写入预分配结果数组的有效数量。
            int hitCount = Physics.RaycastNonAlloc(samplePosition, Vector3.down, stairSurfaceHits, maximumRise + 0.24f, stairGroundMask, QueryTriggerInteraction.Ignore);
            // nearestDistance：当前已接受候选的最近射线距离；初始最大值表示尚未找到候选。
            float nearestDistance = float.MaxValue;
            // nearestTread：当前深度样本接受的最近有效踏面命中。
            RaycastHit nearestTread = default;
            // i：当前命中结果索引；仅遍历本次检测返回的有效区间。
            for (int i = 0; i < hitCount; i++)
            {
                // treadHit：待筛选的踏面射线命中结果。
                RaycastHit treadHit = stairSurfaceHits[i];
                if (!IsValidStaticStairSurface(treadHit.collider) || treadHit.distance >= nearestDistance)
                {
                    continue;
                }

                // rise：候选踏面相对当前脚底的上升高度，用于限制单次抬升。
                float rise = treadHit.point.y - currentFootHeight;
                if (rise < Mathf.Max(0.08f, stairMinimumRise) || rise > maximumRise || Vector3.Angle(treadHit.normal, Vector3.up) > Mathf.Clamp(stairMaximumTreadAngle, 1f, 10f))
                {
                    continue;
                }

                if (sampleIndex > 0)
                {
                    // sameSurface：当前深度样本是否属于前一样本的连续表面或既有村庄整体模型。
                    bool sameSurface = treadHit.collider == expectedSurface;
                    if (!sameSurface || Mathf.Abs(treadHit.point.y - expectedTreadHeight) > Mathf.Clamp(stairTreadHeightTolerance, 0.005f, 0.035f))
                    {
                        continue;
                    }
                }

                nearestDistance = treadHit.distance;
                nearestTread = treadHit;
            }

            if (nearestDistance >= float.MaxValue)
            {
                return false;
            }

            if (sampleIndex == 0)
            {
                expectedSurface = nearestTread.collider;
                expectedTreadHeight = nearestTread.point.y;
                resolvedTread = nearestTread;
            }
        }

        return true;
    }

    /// <summary>旧楼梯流程的表面过滤，排除触发器、刚体、门和角色，仅接受既有静态环境约定。</summary>
    /// <remarks>历史停用方法，位于 #if false，不参与运行时调用链。旧流程在本类方法间同步传递探测参数和结果；位置写入沿已有 CharacterMotionExecutor 凭证入口完成。</remarks>
    /// <param name = "surface">待核对的碰撞表面，过滤自身、触发器和动态角色。</param>
    private static bool IsValidStaticStairSurface(Collider surface)
    {
        if (surface == null || surface.isTrigger)
        {
            return false;
        }

        if (surface.attachedRigidbody != null || surface.GetComponentInParent<Rigidbody>() != null || surface.GetComponentInParent<DoorController>() != null || surface.GetComponentInParent<VillagerController>() != null || surface.GetComponentInParent<LeonController>() != null)
        {
            return false;
        }

        return surface.gameObject.isStatic;
    }

#endif
    private LeonAnimatorDriver.NativeDirection lastDirectionalInput;

    private LeonAnimatorDriver.NativeDirection ReadDirectionalInput(Vector3 input)
    {
        if (input.sqrMagnitude <= .01f)
        {
            lastDirectionalInput = LeonAnimatorDriver.NativeDirection.Forward;
            return lastDirectionalInput;
        }

        float angle = Mathf.Atan2(-input.x, input.z) * Mathf.Rad2Deg;
        if (Mathf.Abs(Mathf.DeltaAngle((int)lastDirectionalInput * 45f, angle)) <= 26.5f)
        {
            return lastDirectionalInput;
        }

        int sector = Mathf.FloorToInt((angle + 22.5f) / 45f);
        lastDirectionalInput = (LeonAnimatorDriver.NativeDirection)((sector + 8) % 8);
        return lastDirectionalInput;
    }

    /// <summary>HandleRotation 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)、object.ReferenceEquals(object, object)、Quaternion.Euler(float, float, float)、Quaternion.Lerp(Quaternion, Quaternion, float)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；下游（含内部回调体） LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)、object.ReferenceEquals(object, object)、Quaternion.Euler(float, float, float)、Quaternion.Lerp(Quaternion, Quaternion, float)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="input">中文含义：输入数据。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="cam">中文含义：摄像机。使用位置：作为 Quaternion.Euler(float, float, float) 的参数、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="isShooting">中文含义：是否正在射击。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="isAiming">中文含义：是否正在瞄准。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="isReloading">中文含义：是否正在换弹。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="isMeleeing">中文含义：是否近战中。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="isKnifeAttack">中文含义：是否小刀攻击。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    /// <param name="moveDir">中文含义：移动方向。使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。</param>
    void HandleRotation(Vector3 input, Camera cam, bool isShooting, bool isAiming, bool isReloading, bool isMeleeing,
        bool isKnifeAttack, Vector3 moveDir, bool directionalInput)
    {
        if (combat != null && combat.knifeModule != null && combat.knifeModule.Active)
        {
            return;
        }

        if (controller.IsolateObserveCameraSteering)
        {
            // 环顾、回正和持续按键的交接阶段只允许移动输入主动改变身体朝向。
            if (input.magnitude > 0.1f && moveDir.sqrMagnitude > 0.0001f)
            {
                // receiverValue15（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
global::LeonActionMotionDriver receiverValue15 = motionExecutor;
                // optionalValue16（条件访问得到的常规朝向是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
bool? optionalValue16;
                if (!global::System.Object.ReferenceEquals(receiverValue15, null))
                {
                    optionalValue16 = receiverValue15.CommitNormalOrientation(movementToken, Quaternion.Lerp(rb.rotation, Quaternion.LookRotation(moveDir), controller.rotationSpeed * Time.fixedDeltaTime));
                }
                else
                {
                    optionalValue16 = default;
                }
            }

            return;
        }

        if (input.magnitude > 0.1f)
        {
            bool holdCameraFacing = directionalInput && !isMeleeing && cam != null;
            if (holdCameraFacing && !isAiming && !isShooting && !isReloading)
            {
                if (motionExecutor != null)
                {
                    var targetRotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
                    motionExecutor.CommitNormalOrientation(movementToken,
                        Quaternion.RotateTowards(rb.rotation, targetRotation, 540f * Time.fixedDeltaTime));
                }
            }
            else if (!isShooting && !isAiming && !isReloading && (!isMeleeing || isKnifeAttack))
            {
                // targetRot（目标旋转）：保存 Quaternion.LookRotation(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Lerp(Quaternion, Quaternion, float) 的参数。
var targetRot = Quaternion.LookRotation(moveDir);
                // receiverValue17（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
global::LeonActionMotionDriver receiverValue17 = motionExecutor;
                // optionalValue18（条件访问得到的常规朝向是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
bool? optionalValue18;
                if (!global::System.Object.ReferenceEquals(receiverValue17, null))
                {
                    optionalValue18 = receiverValue17.CommitNormalOrientation(movementToken, Quaternion.Lerp(rb.rotation, targetRot, controller.rotationSpeed * Time.fixedDeltaTime));
                }
                else
                {
                    optionalValue18 = default;
                }
            }
            else if (isShooting || isAiming || isReloading)
            {
                // camY（摄像机纵向）：使用位置：作为 Quaternion.Euler(float, float, float) 的参数。
var camY = cam.transform.eulerAngles.y;
                // targetRot（目标旋转）：保存 Quaternion.Euler(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Lerp(Quaternion, Quaternion, float) 的参数。
var targetRot = Quaternion.Euler(0, camY, 0);
                // receiverValue19（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
global::LeonActionMotionDriver receiverValue19 = motionExecutor;
                // optionalValue20（条件访问得到的常规朝向是否成功提交）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)。
bool? optionalValue20;
                if (!global::System.Object.ReferenceEquals(receiverValue19, null))
                {
                    optionalValue20 = receiverValue19.CommitNormalOrientation(movementToken, Quaternion.Lerp(rb.rotation, targetRot, 20f * Time.fixedDeltaTime));
                }
                else
                {
                    optionalValue20 = default;
                }
            }
        }
        else if ((isAiming || isShooting) && cam != null && motionExecutor != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            motionExecutor.CommitNormalOrientation(movementToken,
                Quaternion.Lerp(rb.rotation, targetRotation, controller.rotationSpeed * Time.fixedDeltaTime));
        }

    }





    /// <summary>ResetTurnAngle 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetTurnAngle(float)、Mathf.Approximately(float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    private void ResetTurnAngle()
    {
        if (Mathf.Approximately(currentTurnAngle, 0f))
        {
            return;
        }

        currentTurnAngle = 0f;
        // receiverValue27（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonLocomotion.ResetTurnAngle()。
global::LeonAnimatorDriver receiverValue27 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.SetTurnAngle(0f);
        }
    }

    /// <summary>DampToZero 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetMovement(float, float, float, bool)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；下游（含内部回调体） LeonAnimatorDriver.SetMovement(float, float, float, bool)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void DampToZero()
    {
        // damp（阻尼）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float damp = Time.fixedDeltaTime * 15f;
        smoothHorizontal = Mathf.Lerp(smoothHorizontal, 0f, damp);
        smoothVertical = Mathf.Lerp(smoothVertical, 0f, damp);
        smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, 0f, damp);
        currentSpeed = Mathf.Lerp(currentSpeed, 0f, damp);
        if (driver != null)
        {
            driver.SetMovement(smoothHorizontal, smoothVertical, smoothInputMagnitude, false);
        }
    }

    /// <summary>ComputeDirectionalSpeed 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="localMove">中文含义：局部移动。使用位置：作为 Mathf.Abs(float) 的参数、LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。</param>
    /// <param name="fwd">中文含义：前向。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。</param>
    /// <param name="bwd">中文含义：后向。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。</param>
    /// <param name="left">中文含义：左侧。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。</param>
    /// <param name="right">中文含义：右侧。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。</param>
    float ComputeDirectionalSpeed(Vector3 localMove, float fwd, float bwd, float left, float right)
    {
        // absX（绝对值横向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。
float absX = Mathf.Abs(localMove.x);
        // absZ（绝对值深度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。
float absZ = Mathf.Abs(localMove.z);
        // total（总计）：使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。
float total = absX + absZ;
        if (total < 0.001f)
        {
            return 0f;
        }

        // zSpeed（深度速度）：使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。
float zSpeed;
        if (localMove.z >= 0)
        {
            zSpeed = fwd;
        }
        else
        {
            zSpeed = bwd;
        }

        // xSpeed（横向速度）：使用位置：LeonLocomotion.ComputeDirectionalSpeed(Vector3, float, float, float, float)。
float xSpeed;
        if (localMove.x >= 0)
        {
            xSpeed = right;
        }
        else
        {
            xSpeed = left;
        }

        return (zSpeed * absZ + xSpeed * absX) / total;
    }
}
