using UnityEngine;
using System;
using System.Collections;

/// <summary>玩家控制、动作与运动使用的 LeonState 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum LeonState
{
    /// <summary>中文含义：待机。</summary>
    Idle,
    /// <summary>中文含义：行走中。</summary>
    Walking,
    /// <summary>中文含义：运行中。</summary>
    Running,
    /// <summary>中文含义：冲刺中。</summary>
    Sprinting,
    /// <summary>中文含义：瞄准中。</summary>
    Aiming,
    /// <summary>中文含义：射击中。</summary>
    Shooting,
    /// <summary>中文含义：换弹中。</summary>
    Reloading,
    /// <summary>中文含义：近战攻击中。</summary>
    MeleeAttacking,
    /// <summary>中文含义：招架中。</summary>
    Parrying,
    /// <summary>中文含义：通行中。</summary>
    Traversing,
    /// <summary>中文含义：硬直中。</summary>
    Staggered,
    /// <summary>中文含义：配对动作。</summary>
    QTE,
    /// <summary>中文含义：死亡。</summary>
    Dead
}

/// <summary>
/// 里昂受击动画方向，数值必须与受击混合树阈值保持一致。
/// </summary>
public enum LeonHitDirection
{
    /// <summary>中文含义：中心。</summary>
    Center = 0,
    /// <summary>中文含义：右侧。</summary>
    Right = 1,
    /// <summary>中文含义：左侧。</summary>
    Left = 2
}

/// <summary>玩家控制、动作与运动组件 LeonController，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>玩家组件外壳与兼容查询入口。LeonInputBridge 提交输入意图，移动交给 LeonLocomotion，战斗交给 LeonCombat；生命、装备及动作事务分别沿现有组件查询和写入，不在外壳复制这些状态。</remarks>
public class LeonController : Singleton<LeonController>, IManualSingleton, IDamageable, IItemUseTarget
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：初始状态。使用位置：作为 LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState) 的参数、LeonController.currentState.get。</summary>
    [Header("当前状态")]
    [UnityEngine.Serialization.FormerlySerializedAs("currentState")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始状态")]
    [UnityEngine.InspectorName("初始状态")]
    private LeonState initialState = LeonState.Idle;
    /// <summary>中文含义：当前状态。使用位置：CrosshairUI.Update()、DropManager.CanRollDrop()、EnemyCombatModule.ApplyDeadChainsawPulse() 等共 63 处。</summary>
    public LeonState currentState
    {
        get
        {
            if (animCoordinator != null)
            {
                return animCoordinator.PlayerSnapshot.CompatibleState;
            }
            else
            {
                return initialState;
            }
        }
    }

    /// <summary>中文含义：初始武器。使用位置：LeonController.InitialEquipment.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("currentWeapon")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始武器")]
    [UnityEngine.InspectorName("初始武器")]
    private WeaponType initialWeaponValue = WeaponType.Pistol;
    /// <summary>中文含义：初始装备。使用位置：作为 LeonWeaponHandler.InitializeRuntimeModule(WeaponType) 的参数、LeonController.currentWeapon.get、LeonWeaponHandler.Start()。</summary>
    public WeaponType InitialEquipment
    {
        get
        {
            if (initialWeaponValue == WeaponType.Rifle || initialWeaponValue == WeaponType.Shotgun || initialWeaponValue == WeaponType.Knife)
            {
                return initialWeaponValue;
            }
            else
            {
                return WeaponType.Pistol;
            }
        }
    }

    /// <summary>中文含义：当前武器。使用位置：作为 Action&lt;WeaponType&gt;.Invoke(WeaponType) 的参数、作为 Debug.LogWarning(object) 的参数、作为 LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType) 的参数 等共 35 处。</summary>
    public WeaponType currentWeapon
    {
        get
        {
            if (weaponHandler != null)
            {
                return weaponHandler.CurrentEquipment;
            }
            else
            {
                return InitialEquipment;
            }
        }
    }

    /// <summary>中文含义：玩家摄像机。使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、LeonAnimRig.LateUpdate()、LeonController.CalculateMovementDirection(Vector3) 等共 12 处。</summary>
    [Header("引用")]
    public Camera playerCamera;
    /// <summary>中文含义：瞄准目标。使用位置：LeonController.RegisterCameraController(RE4CameraController, Camera, Transform)、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。</summary>
    public Transform aimTarget;
    /// <summary>中文含义：武器挂点。</summary>
    public Transform weaponSocket;
    /// <summary>中文含义：最大生命值。使用位置：作为 CombatHUD.UpdateHealthFill(float, float, bool) 的参数、作为 HealthComponent.Initialize(float) 的参数、作为 Mathf.Max(float, float) 的参数 等共 7 处。</summary>
    [Header("数值")]
    public float maxHealth = 100f;
    /// <summary>中文含义：生命。血量组件</summary>
    public HealthComponent Health { get; private set; }

    /// <summary>中文含义：当前生命值。当前血量（转发到 HealthComponent）</summary>
    public float currentHealth
    {
        get
        {
            if (Health != null)
            {
                return Health.CurrentHealth;
            }
            else
            {
                return maxHealth;
            }
        }
    }

    /// <summary>中文含义：生命变化时的通知回调。血量变化事件 (current, max)</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>中文含义：行走速度。</summary>
    [Header("移动")]
    public float walkSpeed = 1.83f;
    /// <summary>中文含义：运行速度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonLocomotion.HandleMovement()。</summary>
    public float runSpeed = 3.90f;
    /// <summary>中文含义：旋转速度。使用位置：作为 Quaternion.Lerp(Quaternion, Quaternion, float) 的参数。</summary>
    public float rotationSpeed = 10f;
    /// <summary>中文含义：射击移动速度。</summary>
    public float shootMoveSpeed = 1.83f;
    /// <summary>中文含义：蹲伏时的胶囊体高度，不能小于直径。使用位置：作为 LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、LeonController.ApplyCrouchState(bool) 等共 4 处。</summary>
    [Header("蹲伏碰撞")]
    [Tooltip("蹲伏时的胶囊体高度，不能小于直径")]
    public float crouchColliderHeight = 1.2f;
    /// <summary>中文含义：起身空间检测额外增加的半径余量。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("起身空间检测额外增加的半径余量")]
    public float crouchClearancePadding = 0.02f;
    /// <summary>中文含义：参与起身空间检测的碰撞层。使用位置：作为 Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数。</summary>
    [Tooltip("参与起身空间检测的碰撞层")]
    public LayerMask crouchObstructionMask = ~0;
    /// <summary>中文含义：攻击来源横向分量超过此值时，播放对应的左侧或右侧受击动画。使用位置：LeonController.ResolveHitDirection(Vector3)。</summary>
    [Header("受击方向")]
    [Tooltip("攻击来源横向分量超过此值时，播放对应的左侧或右侧受击动画")]
    [Range(0f, 1f)]
    public float hitSideThreshold = 0.35f;
    /// <summary>中文含义：生命比例小于等于该值时进入受伤状态。使用位置：LeonController.HandleHealthChanged(float, float)。</summary>
    [Header("低血量受伤状态")]
    [Tooltip("生命比例小于等于该值时进入受伤状态")]
    [Range(0.05f, 0.9f)]
    public float injuredEnterRatio = 0.35f;
    /// <summary>中文含义：生命比例恢复至该值以上才退出受伤状态（迟滞区间防止抖动）。使用位置：LeonController.HandleHealthChanged(float, float)。</summary>
    [Tooltip("生命比例恢复至该值以上才退出受伤状态（迟滞区间防止抖动）")]
    [Range(0.05f, 1f)]
    public float injuredExitRatio = 0.40f;
    /// <summary>中文含义：是否受伤。逻辑受伤状态：进入阈值立即生效，退出需达到恢复阈值</summary>
    public bool IsInjured { get; private set; }

    /// <summary>中文含义：死亡动画完成通知丢失时，等待多久后强制进入死亡结算界面。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("死亡流程")]
    [Tooltip("死亡动画完成通知丢失时，等待多久后强制进入死亡结算界面")]
    [Min(0.1f)]
    public float deathResultFallbackDelay = 2.5f;
    /// <summary>中文含义：横向瞄准平滑。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Header("Aim Offset 平滑")]
    public float horAimSmooth = 0.75f;
    /// <summary>中文含义：纵向瞄准平滑。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    public float verAimSmooth = 8f;
    /// <summary>中文含义：纵向瞄准增益。抬头/低头动画触发增益，&gt;1 放大 verAim 让 BlendTree 更容易进入上下方向。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonController.UpdateAnimatorParameters()。</summary>
    [Tooltip("抬头/低头动画触发增益，>1 放大 verAim 让 BlendTree 更容易进入上下方向")]
    public float verAimGain = 1.5f;
    /// <summary>中文含义：低头角度下限。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Tooltip("低头角度下限")]
    public float verAimMin = -45f;
    /// <summary>中文含义：平滑横向瞄准。使用位置：作为 LeonAnimatorDriver.SetAimAngles(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonController.SynchronizeEnterAimAngle() 等共 4 处。</summary>
    private float smoothHorAim;
    /// <summary>中文含义：平滑纵向瞄准。使用位置：作为 LeonAnimatorDriver.SetAimAngles(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonController.SynchronizeEnterAimAngle() 等共 4 处。</summary>
    private float smoothVerAim;
    // === 运行时输入状态 ===
    /// <summary>中文含义：输入状态。使用位置：作为 LeonAnimatorDriver.SetCrouching(bool) 的参数、作为 LeonController.TrySetCrouching(bool) 的参数、LeonController.ApplyCrouchState(bool) 等共 23 处。</summary>
    [SerializeField]
    private InputState inputState;
    /// <summary>中文含义：输入数据。输入状态（只读访问）</summary>
    public ref readonly InputState Input => ref inputState;
    // === 向后兼容属性 ===
    /// <summary>中文含义：移动输入。使用位置：作为 FreeLookState.UpdateMovementInput(Vector3) 的参数、作为 Mathf.Clamp01(float) 的参数、FootstepController.Update() 等共 18 处。</summary>
    public Vector3 MoveInput => inputState.moveInput;
    /// <summary>中文含义：观察输入。使用位置：RE4RThirdPersonCamera.GetLookInput()。</summary>
    public Vector2 LookInput => inputState.lookInput;
    /// <summary>中文含义：是否正在冲刺。使用位置：LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool)。</summary>
    public bool IsSprinting => inputState.isSprinting;
    /// <summary>中文含义：当前平面速度。角色当前实际水平移动速度。镜头等表现层必须以此值判断是否已经进入奔跑， 不能只根据按键状态提前切换。</summary>
    public float CurrentPlanarSpeed { get => HorizontalSpeed.magnitude; }
    /// <summary>中文含义：是否实际生效的冲刺中。使用位置：作为 LeonAnimatorDriver.SetMovement(float, float, float, bool) 的参数、FootstepController.PlayFootstep()、FootstepController.Update() 等共 7 处。</summary>
    public bool IsEffectiveSprinting => inputState.isSprinting && !inputState.isCrouching && inputState.moveInput.sqrMagnitude > 0.01f && currentState != LeonState.Aiming && currentState != LeonState.Shooting && currentState != LeonState.Reloading && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying && currentState != LeonState.Staggered && currentState != LeonState.QTE && currentState != LeonState.Traversing && currentState != LeonState.Dead;

    /// <summary>中文含义：自由观察。只读字段，初始化后不由普通写入口替换。使用位置：LeonController.AdvanceFreeViewRecentering(float, float)、LeonController.CalculateMovementDirection(Vector3)、LeonController.CurrentFreeLookPhase.get 等共 11 处。</summary>
    private readonly FreeLookState freeObserve = new FreeLookState();
    /// <summary>中文含义：是否自由观察中。使用位置：LeonAnimRig.Update()。</summary>
    public bool IsFreeLooking => freeObserve.Phase == FreeLookPhase.Observe;
    /// <summary>中文含义：当前自由环顾阶段。使用位置：RE4RThirdPersonCamera.DriveInput(float)。</summary>
    public FreeLookPhase CurrentFreeLookPhase => freeObserve.Phase;
    /// <summary>中文含义：是否隔离观察摄像机转向。使用位置：LeonLocomotion.HandleRotation(Vector3, Camera, bool, bool, bool, bool, bool, Vector3)、LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool)、RE4RThirdPersonCamera.DriveInput(float)。</summary>
    public bool IsolateObserveCameraSteering => freeObserve.IsolateCameraSteering;
    /// <summary>中文含义：是否跳跃中。使用位置：LeonFootAction.AllowGroundContact.get、LeonFootAction.FixedUpdate()。</summary>
    public bool IsJumping => inputState.isJumping;
    /// <summary>中文含义：是否正在蹲伏。使用位置：LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.Update()、LeonInputBridge.EnterQTEInput(int) 等共 11 处。</summary>
    public bool IsCrouching => inputState.isCrouching;
    internal bool IsFirearmStanceBlocked => IsCrouching || animDriver != null && animDriver.IsRisingForFirearm;
    /// <summary>中文含义：是否着地。使用位置：FootstepController.Update()、LeonFootAction.AllowGroundContact.get、LeonFootAction.FixedUpdate() 等共 4 处。</summary>
    public bool IsGrounded => inputState.isGrounded;

    // === 输入写入方法（由 LeonInputBridge 调用）===
    /// <summary>SetMoveInput 的业务处理入口；本方法直接依赖 FreeLookState.UpdateMovementInput(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnMove(InputAction.CallbackContext) 等共 7 处；下游（含内部回调体） FreeLookState.UpdateMovementInput(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：作为 FreeLookState.UpdateMovementInput(Vector3) 的参数、LeonController.SetMoveInput(Vector3)。</param>
    public void SetMoveInput(Vector3 value)
    {
        inputState.moveInput = value;
        freeObserve.UpdateMovementInput(value);
    }

    internal void ResetMovementInputForWindow()
    {
        if (inputBridge != null)
        {
            inputBridge.RequestMovementInputRefresh();
        }

        SetMoveInput(Vector3.zero);
        SetSprinting(false);
    }

    /// <summary>SetLookInput 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnLook(InputAction.CallbackContext)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)、PerformanceSamplingEntry.Drive(int, double)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：LeonController.SetLookInput(Vector2)。</param>
    public void SetLookInput(Vector2 value) => inputState.lookInput = value;
    /// <summary>SetSprinting 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.BeginDrop()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnSprintEnd(InputAction.CallbackContext) 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：是否值。使用位置：LeonController.SetSprinting(bool)。</param>
    public void SetSprinting(bool value) => inputState.isSprinting = value && !inputState.isCrouching;
    /// <summary>SetFreeLooking 的业务处理入口；本方法直接依赖 FreeLookState.SetKeyBinding(bool, bool, float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnFreeLookEnd(InputAction.CallbackContext)、LeonInputBridge.OnFreeLookStart(InputAction.CallbackContext)、LeonInputBridge.Update() 等共 6 处；下游（含内部回调体） FreeLookState.SetKeyBinding(bool, bool, float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：是否值。使用位置：作为 FreeLookState.SetKeyBinding(bool, bool, float) 的参数、LeonController.SetFreeLooking(bool)。</param>
    public void SetFreeLooking(bool value)
    {
        inputState.isFreeLooking = value;
        // originalPhase（原始阶段）：使用位置：LeonController.SetFreeLooking(bool)。
var originalPhase = freeObserve.Phase;
        freeObserve.SetKeyBinding(value, AllowFreeObserve && !IsAiming, ViewHorizontalAngle);
        if (originalPhase != freeObserve.Phase)
        {
            // receiverValue5（判空前缓存的摄像机控制器）：从摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.SetFreeLooking(bool)。
global::RE4CameraController receiverValue5 = cameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                // receiverValue6（判空前缓存的接收者值5的玩法驱动器）：从接收者值5的玩法驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.SetFreeLooking(bool)。
global::RE4RThirdPersonCamera receiverValue6 = receiverValue5.gameplayDriver;
                if (!global::System.Object.ReferenceEquals(receiverValue6, null))
                {
                    receiverValue6.SynchronizeObserveDirection();
                }
            }
        }
    }

    /// <summary>中文含义：视图水平角度。使用位置：作为 FreeLookState.SetKeyBinding(bool, bool, float) 的参数。</summary>
    private float ViewHorizontalAngle
    {
        get
        {
            if (playerCamera != null)
            {
                return playerCamera.transform.eulerAngles.y;
            }
            else
            {
                return transform.eulerAngles.y;
            }
        }
    }

    /// <summary>中文含义：是否允许自由观察。使用位置：作为 FreeLookState.SetKeyBinding(bool, bool, float) 的参数、LeonController.UpdateFreeLookState()。</summary>
    private bool AllowFreeObserve => isActiveAndEnabled && applyHasFocus && (inputBridge == null || inputBridge.AllowFreeViewInput) && (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput) && (!GameUIManager.HasInstance || !GameUIManager.Instance.IsPlayerInputBlocked) && (cameraController == null || !cameraController.HasExternalCamera) && currentState != LeonState.Staggered && currentState != LeonState.Dead && currentState != LeonState.QTE && currentState != LeonState.Traversing && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying;

    /// <summary>中文含义：应用是否具有焦点。使用位置：LeonController.AllowFreeObserve.get、LeonController.OnApplicationFocus(bool)。</summary>
    private bool applyHasFocus = true;
    /// <summary>UpdateFreeLookState 的业务处理入口；本方法直接依赖 FreeLookState.UpdateMovementInput(Vector3)、LeonController.ResetFreeView()、LeonController.TakeOverFreeViewAim()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.FixedUpdate()、LeonController.Update()、RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） FreeLookState.UpdateMovementInput(Vector3)、LeonController.ResetFreeView()、LeonController.TakeOverFreeViewAim()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void UpdateFreeLookState()
    {
        if (!AllowFreeObserve)
        {
            if (freeObserve.IsolateCameraSteering || inputState.isFreeLooking)
            {
                ResetFreeView();
            }
        }
        else if (IsAiming && (freeObserve.IsolateCameraSteering || inputState.isFreeLooking))
        {
            TakeOverFreeViewAim();
        }

        freeObserve.UpdateMovementInput(MoveInput);
    }

    /// <summary>CalculateMovementDirection 的业务处理入口；本方法直接依赖 FreeLookState.CalculateMovementDirection(Vector3, Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；下游（含内部回调体） FreeLookState.CalculateMovementDirection(Vector3, Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="input">中文含义：输入数据。使用位置：作为 FreeLookState.CalculateMovementDirection(Vector3, Vector3, Vector3) 的参数。</param>
    public Vector3 CalculateMovementDirection(Vector3 input)
    {
        // reference（引用）：使用位置：作为 FreeLookState.CalculateMovementDirection(Vector3, Vector3, Vector3) 的参数、LeonController.CalculateMovementDirection(Vector3)。
global::UnityEngine.Transform reference;
        if (playerCamera != null)
        {
            reference = playerCamera.transform;
        }
        else
        {
            reference = transform;
        }

        return freeObserve.CalculateMovementDirection(input, reference.forward, reference.right);
    }

    /// <summary>AdvanceFreeViewRecentering 的业务处理入口；本方法直接依赖 FreeLookState.AdvanceRecentering(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)；下游（含内部回调体） FreeLookState.AdvanceRecentering(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="timeStepSize">中文含义：时间步进尺寸。使用位置：作为 FreeLookState.AdvanceRecentering(float, float, float) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 FreeLookState.AdvanceRecentering(float, float, float) 的参数。</param>
    public float AdvanceFreeViewRecentering(float timeStepSize, float duration) => freeObserve.AdvanceRecentering(timeStepSize, duration, transform.eulerAngles.y);
    /// <summary>InterruptFreeViewRecentering 的业务处理入口；本方法直接依赖 FreeLookState.InterruptRecentering()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)；下游（含内部回调体） FreeLookState.InterruptRecentering()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InterruptFreeViewRecentering()
    {
        freeObserve.InterruptRecentering();
        // receiverValue9（判空前缓存的摄像机控制器）：从摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.InterruptFreeViewRecentering()。
global::RE4CameraController receiverValue9 = cameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            // receiverValue10（判空前缓存的接收者值9的玩法驱动器）：从接收者值9的玩法驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.InterruptFreeViewRecentering()。
global::RE4RThirdPersonCamera receiverValue10 = receiverValue9.gameplayDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue10, null))
            {
                receiverValue10.SynchronizeObserveDirection();
            }
        }
    }

    /// <summary>TakeOverFreeViewAim 的业务处理入口；本方法直接依赖 FreeLookState.Reset()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleAim(bool)、LeonController.UpdateFreeLookState()；下游（含内部回调体） FreeLookState.Reset()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TakeOverFreeViewAim()
    {
        if (freeObserve.IsolateCameraSteering)
        {
            // receiverValue11（判空前缓存的摄像机控制器）：从摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.TakeOverFreeViewAim()。
global::RE4CameraController receiverValue11 = cameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue11, null))
            {
                // receiverValue12（判空前缓存的接收者值11的玩法驱动器）：从接收者值11的玩法驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.TakeOverFreeViewAim()。
global::RE4RThirdPersonCamera receiverValue12 = receiverValue11.gameplayDriver;
                if (!global::System.Object.ReferenceEquals(receiverValue12, null))
                {
                    receiverValue12.SynchronizeObserveDirection(true);
                }
            }
        }

        freeObserve.Reset();
    }

    /// <summary>ResetFreeView 的业务处理入口；本方法直接依赖 FreeLookState.Reset()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.OnApplicationFocus(bool)、LeonController.OnDisable()、LeonController.UnregisterCameraController(RE4CameraController)、LeonController.UpdateFreeLookState()、LeonInputBridge.OnDisable() 等共 11 处；下游（含内部回调体） FreeLookState.Reset()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetFreeView()
    {
        freeObserve.Reset();
    }

    /// <summary>OnApplicationFocus 的业务处理入口；本方法直接依赖 LeonController.ResetFreeView()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ResetFreeView()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hasFocus">中文含义：是否具有焦点。使用位置：LeonController.OnApplicationFocus(bool)。</param>
    void OnApplicationFocus(bool hasFocus)
    {
        applyHasFocus = hasFocus;
        if (!hasFocus)
        {
            ResetFreeView();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ResetFreeView()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable() => ResetFreeView();
    /// <summary>中文含义：水平速度。使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、LeonController.CurrentPlanarSpeed.get。</summary>
    internal Vector3 HorizontalSpeed
    {
        get
        {
            if (rb != null)
            {
                return Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            }
            else
            {
                return Vector3.zero;
            }
        }
    }

    /// <summary>中文含义：运动刚体。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、LeonController.CanStandUp()、LeonController.HorizontalSpeed.get 等共 4 处。</summary>
    private Rigidbody rb;
    /// <summary>中文含义：移动行为。使用位置：作为 LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState) 的参数、LeonController.FinishQTEState(bool)、LeonController.FixedUpdate() 等共 4 处。</summary>
    private LeonLocomotion locomotion;
    /// <summary>中文含义：战斗。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.BeginDeathSequence()、LeonController.HandleDeferredTraversalHit(LeonHitDirection) 等共 9 处。</summary>
    private LeonCombat combat;
    /// <summary>中文含义：管理当前装备与武器切换的组件。使用位置：LeonController.CanReload()、LeonController.CompleteReload()、LeonController.currentWeapon.get 等共 12 处。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>中文含义：摄像机控制器。使用位置：LeonController.AllowFreeObserve.get、LeonController.CameraController.get、LeonController.InterruptFreeViewRecentering() 等共 9 处。</summary>
    private RE4CameraController cameraController;
    // === 管线组件缓存 ===
    /// <summary>中文含义：动画驱动器。使用位置：LeonController.AnimDriver.get、LeonController.ApplyCrouchState(bool)、LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 等共 8 处。</summary>
    private LeonAnimatorDriver animDriver;
    /// <summary>中文含义：动画协调器。使用位置：LeonController.AnimCoordinator.get、LeonController.BeginDeathSequence()、LeonController.CanAct() 等共 9 处。</summary>
    private LeonAnimationActionCoordinator animCoordinator;
    /// <summary>中文含义：当前里昂动画绑定组件。使用位置：LeonController.AnimRig.get、LeonController.InitPipeline()。</summary>
    private LeonAnimRig animRig;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonController.InitPipeline()。</summary>
    private LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：当前里昂配对动作协调器。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.InitPipeline()、LeonController.QTECoordinator.get 等共 4 处。</summary>
    private LeonQTECoordinator qteCoordinator;
    /// <summary>中文含义：高处跳落落地。使用位置：LeonController.FixedUpdate()、LeonController.InitPipeline()。</summary>
    private LeonHighDropLanding highDropLanding;
    /// <summary>中文含义：翻窗通行。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.FixedUpdate()、LeonController.InitPipeline()。</summary>
    private LeonWindowTraversal windowTraversal;
    /// <summary>中文含义：输入桥接。使用位置：LeonController.AllowFreeObserve.get、LeonController.BeginDeathSequence()、LeonController.InitPipeline()。</summary>
    private LeonInputBridge inputBridge;
    /// <summary>中文含义：玩家音频。使用位置：LeonController.BeginDeathSequence()、LeonController.Start()。</summary>
    private LeonPlayerAudio playerAudio;
    /// <summary>中文含义：胶囊碰撞体。使用位置：LeonController.ApplyCrouchState(bool)、LeonController.CanStandUp()、LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float) 等共 6 处。</summary>
    private CapsuleCollider capsuleCollider;
    /// <summary>中文含义：站立碰撞体高度。使用位置：作为 LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、LeonController.ApplyCrouchState(bool) 等共 4 处。</summary>
    private float standingColliderHeight;
    /// <summary>中文含义：站立碰撞体中心。使用位置：作为 LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float) 的参数、LeonController.ApplyCrouchState(bool)、LeonController.InitializeCrouchCollider()。</summary>
    private Vector3 standingColliderCenter;
    /// <summary>中文含义：蹲伏中碰撞体中心。使用位置：作为 LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float) 的参数、LeonController.ApplyCrouchState(bool)、LeonController.InitializeCrouchCollider()。</summary>
    private Vector3 crouchingColliderCenter;
    /// <summary>中文含义：蹲伏重叠结果集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、LeonController.CanStandUp()。</summary>
    private readonly Collider[] crouchOverlapResults = new Collider[32];
    /// <summary>中文含义：新动作管线是否有效。使用位置：LeonController.HandleHealthChanged(float, float)、LeonController.InitPipeline()、LeonController.NewPipelineValid.get 等共 6 处。</summary>
    private bool newPipelineValid;
    /// <summary>中文含义：死亡序号是否已开始。使用位置：LeonController.BeginDeathSequence()、LeonController.HandleDeathAnimationCompleted()、LeonController.ReportDeathCompletion() 等共 4 处。</summary>
    private bool deathSequenceStarted;
    /// <summary>中文含义：是否死亡完成已报告。使用位置：LeonController.HandleDeathAnimationCompleted()、LeonController.ReportDeathCompletion()、LeonController.WaitForDeathCompletionFallback()。</summary>
    private bool deathCompletionReported;
    /// <summary>中文含义：死亡完成后备协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、LeonController.BeginDeathSequence()、LeonController.HandleDeathAnimationCompleted() 等共 5 处。</summary>
    private Coroutine deathCompletionFallbackRoutine;
    /// <summary>中文含义：是否伤害分辨率内部进度。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.HandleHealthChanged(float, float)。</summary>
    private bool damageResolutionInProgress;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;HealthComponent&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonLocomotion&gt;()、Component.GetComponent&lt;LeonPlayerAudio&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、Component.GetComponent&lt;Rigidbody&gt;() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        groundMotion = GetComponent<LeonActionMotionDriver>();
        Health = GetComponent<HealthComponent>();
        if (Health == null)
        {
            Health = gameObject.AddComponent<HealthComponent>();
        }

        Health.Initialize(maxHealth);
        Health.OnHealthChanged += HandleHealthChanged;
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        locomotion = GetComponent<LeonLocomotion>();
        combat = GetComponent<LeonCombat>();
        weaponHandler = GetComponent<LeonWeaponHandler>();
        playerAudio = GetComponent<LeonPlayerAudio>();
        if (playerAudio == null)
        {
            playerAudio = gameObject.AddComponent<LeonPlayerAudio>();
        }

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        InitializeCrouchCollider();
        InitPipeline();
    }

    /// <summary>
    /// 生命值变化统一处理：先转发外部事件，再更新逻辑受伤状态。
    /// 伤害结算期间不直接修改动画层，由结算流程决定延迟或立即同步。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;float, float&gt;.Invoke(float, float)、LeonAnimatorDriver.RequestInjuredState(bool, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">中文含义：当前。使用位置：作为 Action&lt;float, float&gt;.Invoke(float, float) 的参数、LeonController.HandleHealthChanged(float, float)。</param>
    /// <param name="max">中文含义：最大。使用位置：作为 Action&lt;float, float&gt;.Invoke(float, float) 的参数、LeonController.HandleHealthChanged(float, float)。</param>
    private void HandleHealthChanged(float current, float max)
    {
        // receiverValue14（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.HandleHealthChanged(float, float)。
global::System.Action<float, float> receiverValue14 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.Invoke(current, max);
        }

        // ratio（比例）：使用位置：LeonController.HandleHealthChanged(float, float)。
float ratio;
        if (max > 0f)
        {
            ratio = current / max;
        }
        else
        {
            ratio = 0f;
        }

        if (!IsInjured && ratio <= injuredEnterRatio)
        {
            IsInjured = true;
        }
        else if (IsInjured && ratio >= injuredExitRatio)
        {
            IsInjured = false;
        }

        if (damageResolutionInProgress)
        {
            return;
        }

        // 治疗或其他非受击生命变化：立即按普通过渡同步受伤层
        if (newPipelineValid)
        {
            // receiverValue16（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.HandleHealthChanged(float, float)。
global::LeonAnimatorDriver receiverValue16 = animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.RequestInjuredState(IsInjured, false);
            }
        }
    }

    /// <summary>InitPipeline 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonHighDropLanding&gt;()、Component.GetComponent&lt;LeonInputBridge&gt;()、Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;() 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.Start()；下游（含内部回调体） Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonHighDropLanding&gt;()、Component.GetComponent&lt;LeonInputBridge&gt;()、Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitPipeline()
    {
        animDriver = GetComponent<LeonAnimatorDriver>();
        animCoordinator = GetComponent<LeonAnimationActionCoordinator>();
        animRig = GetComponent<LeonAnimRig>();
        weaponVisualController = GetComponent<LeonWeaponVisualController>();
        qteCoordinator = GetComponent<LeonQTECoordinator>();
        highDropLanding = GetComponent<LeonHighDropLanding>();
        windowTraversal = GetComponent<LeonWindowTraversal>();
        inputBridge = GetComponent<LeonInputBridge>();
        if (animDriver == null)
        {
            Debug.LogError("[LeonController] LeonAnimatorDriver 未找到，管线停止");
            newPipelineValid = false;
            return;
        }

        if (animCoordinator == null)
        {
            Debug.LogError("[LeonController] LeonAnimationActionCoordinator 未找到，管线停止");
            newPipelineValid = false;
            return;
        }

        if (animRig == null)
        {
            Debug.LogError("[LeonController] LeonAnimRig 未找到，管线停止");
            newPipelineValid = false;
            return;
        }

        if (!animDriver.ValidateAnimatorContract())
        {
            Debug.LogError("[LeonController] Animator契约验证失败，管线停止");
            newPipelineValid = false;
            return;
        }

        if (weaponVisualController == null)
        {
            Debug.LogError("[LeonController] LeonWeaponVisualController 未找到，管线停止");
            newPipelineValid = false;
            return;
        }

        animCoordinator.InitializePlayerState(locomotion, initialState);
        newPipelineValid = true;
        animCoordinator.OnDeathAnimationCompleted += HandleDeathAnimationCompleted;
        // initialWeapon（初始武器）：使用位置：作为 LeonAnimatorDriver.InitializeAnimatorContract(WeaponType) 的参数、LeonController.InitPipeline()。
WeaponType initialWeapon;
        if (currentWeapon == WeaponType.Rifle || currentWeapon == WeaponType.Shotgun || currentWeapon == WeaponType.Knife)
        {
            initialWeapon = currentWeapon;
        }
        else
        {
            initialWeapon = WeaponType.Pistol;
        }

        // receiverValue18（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.InitPipeline()。
global::LeonWeaponHandler receiverValue18 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.InitializeRuntimeModule(InitialEquipment);
        }

        initialWeapon = currentWeapon;
        animDriver.InitializeAnimatorContract(initialWeapon);
        animDriver.SetCrouching(inputState.isCrouching);
        // 正式视觉初始化握手
        if (currentWeapon == WeaponType.Pistol || currentWeapon == WeaponType.Rifle || currentWeapon == WeaponType.Shotgun || currentWeapon == WeaponType.Knife)
        {
            weaponVisualController.Initialize(currentWeapon);
        }
        else
        {
            // receiverValue19（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.InitPipeline()。
global::LeonWeaponHandler receiverValue19 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue19, null))
            {
                receiverValue19.ForceSyncCurrentWeapon(WeaponType.Pistol, false);
            }

            weaponVisualController.Initialize(WeaponType.Pistol);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.UpdateAnimatorParameters()、LeonController.UpdateFreeLookState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        UpdateFreeLookState();
        if (currentState == LeonState.Dead)
        {
            return;
        }

        UpdateAnimatorParameters();
    }

    /// <summary>Unity 固定时间步更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.SynchronizeMotionControlAuthority()、LeonController.CheckGrounded()、LeonController.UpdateFreeLookState()、LeonHighDropLanding.HandleFixedUpdate()、LeonLocomotion.HandleMovement()、LeonWindowTraversal.HandleFixedUpdate()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void FixedUpdate()
    {
        UpdateFreeLookState();
        // receiverValue20（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.FixedUpdate()。
global::LeonAnimationActionCoordinator receiverValue20 = animCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.SynchronizeMotionControlAuthority();
        }

        if (currentState == LeonState.Dead)
        {
            return;
        }

        CheckGrounded();
        if (windowTraversal != null && windowTraversal.HandleFixedUpdate())
        {
            return;
        }

        if (highDropLanding != null && highDropLanding.HandleFixedUpdate())
        {
            return;
        }

        if (inputBridge != null)
        {
            inputBridge.RestoreMovementInput();
        }

        // receiverValue21（判空前缓存的移动行为）：从移动行为读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.FixedUpdate()。
global::LeonLocomotion receiverValue21 = locomotion;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.HandleMovement();
        }
    }

    /// <summary>UpdateAnimatorParameters 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetAimAngles(float, float)、Mathf.Asin(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Lerp(float, float, float)、Transform.InverseTransformDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.Update()；下游（含内部回调体） LeonAnimatorDriver.SetAimAngles(float, float)、Mathf.Asin(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Lerp(float, float, float)、Transform.InverseTransformDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateAnimatorParameters()
    {
        if (playerCamera == null)
        {
            return;
        }

        // localCamDir（局部摄像机方向）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Atan2(float, float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数。
Vector3 localCamDir = transform.InverseTransformDirection(playerCamera.transform.forward);
        // horAim（横向瞄准）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float horAim = Mathf.Atan2(localCamDir.x, localCamDir.z) * Mathf.Rad2Deg;
        // verAim（纵向瞄准）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonController.UpdateAnimatorParameters()。
float verAim = Mathf.Asin(Mathf.Clamp(localCamDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        // aiming（是否瞄准中）：使用位置：LeonController.UpdateAnimatorParameters()。
bool aiming = combat != null && combat.IsAiming;
        if (!aiming)
        {
            verAim = 0f;
        }
        else
        {
            verAim *= verAimGain;
            verAim = Mathf.Clamp(verAim, verAimMin, 90f);
        }

        smoothHorAim = Mathf.Lerp(smoothHorAim, horAim, Time.deltaTime * horAimSmooth);
        smoothVerAim = Mathf.Lerp(smoothVerAim, verAim, Time.deltaTime * verAimSmooth);
        if (newPipelineValid)
        {
            animDriver.SetAimAngles(smoothHorAim, smoothVerAim);
        }
    // 管线无效时不写入Animator，禁止回退旧写入
    }

    /// <summary>SynchronizeEnterAimAngle 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetAimAngles(float, float)、Mathf.Asin(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Transform.InverseTransformDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleAim(bool)；下游（含内部回调体） LeonAnimatorDriver.SetAimAngles(float, float)、Mathf.Asin(float)、Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Transform.InverseTransformDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SynchronizeEnterAimAngle()
    {
        if (playerCamera == null || !newPipelineValid)
        {
            return;
        }

        // cameraDirection（摄像机方向）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Atan2(float, float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数。
Vector3 cameraDirection = transform.InverseTransformDirection(playerCamera.transform.forward);
        smoothHorAim = Mathf.Atan2(cameraDirection.x, cameraDirection.z) * Mathf.Rad2Deg;
        smoothVerAim = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(cameraDirection.y, -1f, 1f)) * Mathf.Rad2Deg * verAimGain, verAimMin, 45f);
        animDriver.SetAimAngles(smoothHorAim, smoothVerAim);
    }

    /// <summary>Heal 的业务处理入口；本方法直接依赖 HealthComponent.Heal(float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.IItemUseTarget.Healing(float)；下游（含内部回调体） HealthComponent.Heal(float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">中文含义：大小。使用位置：作为 HealthComponent.Heal(float) 的参数。</param>
    public void Heal(float amount)
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        // receiverValue22（判空前缓存的生命）：从生命读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.Heal(float)。
global::HealthComponent receiverValue22 = Health;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.Heal(amount);
        }
    }

    /// <summary>ChangeState 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、LeonCombat.ApplyGuardPose()、LeonCombat.EndShoot()、LeonCombat.EndTemporaryKnife(bool)、LeonCombat.ExecuteFirearmAttack() 等共 23 处；下游（含内部回调体） LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="newState">中文含义：新状态。使用位置：作为 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState) 的参数。</param>
    public void ChangeState(LeonState newState)
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        if (inputBridge != null && newState != currentState &&
            (newState == LeonState.Traversing || newState == LeonState.MeleeAttacking))
        {
            inputBridge.RequestMovementInputRefresh();
        }

        // receiverValue23（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ChangeState(LeonState)。
global::LeonAnimationActionCoordinator receiverValue23 = animCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue23, null))
        {
            receiverValue23.CommitCompatibleState(newState);
        }
    }

    /// <summary>CanAct 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.AdvanceQuickSelectSwitch()、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanAct()
    {
        if (animCoordinator == null || !animCoordinator.isActiveAndEnabled)
        {
            return false;
        }

        return currentState != LeonState.Staggered && currentState != LeonState.Dead && currentState != LeonState.QTE && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying && currentState != LeonState.Traversing;
    }

    private LeonActionMotionDriver groundMotion;
    private readonly RaycastHit[] groundContacts = new RaycastHit[16];

    private void CheckGrounded()
    {
        const float rayLength = 1.2f;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit first, rayLength))
        {
            inputState.isGrounded = false;
            return;
        }
        if (groundMotion == null || !groundMotion.IsWalkThroughDetail(first.collider))
        {
            inputState.isGrounded = true;
            return;
        }
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundContacts, rayLength);
        inputState.isGrounded = false;
        for (int i = 0; i < count; i++)
        {
            if (!groundMotion.IsWalkThroughDetail(groundContacts[i].collider))
            {
                inputState.isGrounded = true;
                return;
            }
        }
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DamageSystem.ApplyDamageToPlayer(float)；下游（含内部回调体） LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 的参数。</param>
    public void TakeDamage(float damage)
    {
        ApplyDamage(damage, transform.position + Vector3.up, LeonHitDirection.Center);
    }

    /// <summary>
    /// 在站立和蹲伏之间切换。头顶空间不足时拒绝起身。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnCrouch(InputAction.CallbackContext)；下游（含内部回调体） LeonController.TrySetCrouching(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool ToggleCrouch()
    {
        return TrySetCrouching(!inputState.isCrouching);
    }

    /// <summary>
    /// 请求切换蹲伏姿态。蹲伏只作为姿态标记，不占用角色主状态。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.PrepareForQTE()、LeonController.ToggleCrouch()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.OnMelee(InputAction.CallbackContext)、LeonInputBridge.OnParry(InputAction.CallbackContext) 等共 6 处；下游（含内部回调体） LeonController.ApplyCrouchState(bool)、LeonController.CanStandUp()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="crouching">中文含义：是否蹲伏中。使用位置：作为 LeonController.ApplyCrouchState(bool) 的参数、LeonController.TrySetCrouching(bool)。</param>
    public bool TrySetCrouching(bool crouching)
    {
        if (inputState.isCrouching == crouching)
        {
            return true;
        }

        if (!newPipelineValid || capsuleCollider == null)
        {
            return false;
        }

        if (currentState == LeonState.Dead || currentState == LeonState.Staggered || currentState == LeonState.QTE || currentState == LeonState.MeleeAttacking || currentState == LeonState.Parrying || currentState == LeonState.Traversing)
        {
            return false;
        }

        if (crouching && currentWeapon != WeaponType.Pistol && currentWeapon != WeaponType.Rifle && currentWeapon != WeaponType.Shotgun)
        {
            return false;
        }

        if (crouching && (currentState == LeonState.Shooting || combat != null && combat.HasAimIntent))
        {
            return false;
        }

        if (!crouching && !CanStandUp())
        {
            return false;
        }

        ApplyCrouchState(crouching);
        return true;
    }

    /// <summary>
    /// 判断站立胶囊体是否会与外部碰撞体重叠。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.TrySetCrouching(bool)；下游（含内部回调体） LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)、Mathf.Max(float, float)、Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanStandUp()
    {
        if (capsuleCollider == null)
        {
            return true;
        }

        GetWorldCapsule(standingColliderCenter, standingColliderHeight, Mathf.Max(0f, crouchClearancePadding), out Vector3 /* 中文含义：站立顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ standingTop, out _, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius);
        GetWorldCapsule(crouchingColliderCenter, crouchColliderHeight, Mathf.Max(0f, crouchClearancePadding), out Vector3 /* 中文含义：蹲伏中顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ crouchingTop, out _, out _);
        // count（数量）：保存 Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：LeonController.CanStandUp()。
int count = Physics.OverlapCapsuleNonAlloc(standingTop, crouchingTop, radius, crouchOverlapResults, crouchObstructionMask, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：LeonController.CanStandUp()。
for (int i = 0; i < count; i++)
        {
            // hit（命中）：使用位置：LeonController.CanStandUp()。
Collider hit = crouchOverlapResults[i];
            crouchOverlapResults[i] = null;
            if (hit == null || hit == capsuleCollider)
            {
                continue;
            }

            if (hit.attachedRigidbody != null && hit.attachedRigidbody == rb)
            {
                continue;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return count < crouchOverlapResults.Length;
    }

    /// <summary>InitializeCrouchCollider 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.Start()；下游（含内部回调体） Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializeCrouchCollider()
    {
        inputState.isCrouching = false;
        if (capsuleCollider == null)
        {
            return;
        }

        standingColliderHeight = capsuleCollider.height;
        standingColliderCenter = capsuleCollider.center;
        // minimumHeight（最小高度）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
float minimumHeight = capsuleCollider.radius * 2f;
        crouchColliderHeight = Mathf.Clamp(crouchColliderHeight, minimumHeight, standingColliderHeight);
        // standingBottom（站立底部）：使用位置：LeonController.InitializeCrouchCollider()。
float standingBottom = standingColliderCenter.y - standingColliderHeight * 0.5f;
        crouchingColliderCenter = standingColliderCenter;
        crouchingColliderCenter.y = standingBottom + crouchColliderHeight * 0.5f;
    }

    /// <summary>ApplyCrouchState 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetCrouching(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ForceExitCrouch()、LeonController.TrySetCrouching(bool)；下游（含内部回调体） LeonAnimatorDriver.SetCrouching(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="crouching">中文含义：是否蹲伏中。使用位置：作为 LeonAnimatorDriver.SetCrouching(bool) 的参数、LeonController.ApplyCrouchState(bool)。</param>
    private void ApplyCrouchState(bool crouching)
    {
        inputState.isCrouching = crouching;
        if (crouching)
        {
            inputState.isSprinting = false;
        }

        // assignmentReceiver24（赋值前保存的目标胶囊碰撞体）：从胶囊碰撞体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonController.ApplyCrouchState(bool)。
global::UnityEngine.CapsuleCollider assignmentReceiver24 = capsuleCollider;
        if (crouching)
        {
            assignmentReceiver24.height = crouchColliderHeight;
        }
        else
        {
            assignmentReceiver24.height = standingColliderHeight;
        }

        // assignmentReceiver26（赋值前保存的目标胶囊碰撞体）：从胶囊碰撞体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonController.ApplyCrouchState(bool)。
global::UnityEngine.CapsuleCollider assignmentReceiver26 = capsuleCollider;
        if (crouching)
        {
            assignmentReceiver26.center = crouchingColliderCenter;
        }
        else
        {
            assignmentReceiver26.center = standingColliderCenter;
        }

        // receiverValue28（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ApplyCrouchState(bool)。
global::LeonAnimatorDriver receiverValue28 = animDriver;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.SetCrouching(crouching);
        }
    }

    /// <summary>ForceExitCrouch 的业务处理入口；本方法直接依赖 LeonController.ApplyCrouchState(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.BeginDeathSequence()；下游（含内部回调体） LeonController.ApplyCrouchState(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ForceExitCrouch()
    {
        if (!inputState.isCrouching)
        {
            return;
        }

        ApplyCrouchState(false);
    }

    /// <summary>GetWorldCapsule 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.CanStandUp()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="localCenter">中文含义：局部中心。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。</param>
    /// <param name="localHeight">中文含义：局部高度。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="radiusPadding">中文含义：半径留白。使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。</param>
    /// <param name="top">中文含义：顶部。输出参数，由本入口写入并交给调用方。使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。</param>
    /// <param name="bottom">中文含义：底部。输出参数，由本入口写入并交给调用方。使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。</param>
    /// <param name="radius">中文含义：半径。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Max(float, float) 的参数、LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。</param>
    internal bool TryGetCurrentWorldCapsule(out Vector3 top, out Vector3 bottom, out float radius)
    {
        top = bottom = default;
        radius = 0f;
        if (capsuleCollider == null || !capsuleCollider.enabled)
        {
            return false;
        }

        GetWorldCapsule(capsuleCollider.center, capsuleCollider.height, 0f, out top, out bottom, out radius);
        return true;
    }

    private void GetWorldCapsule(Vector3 localCenter, float localHeight, float radiusPadding, out Vector3 top, out Vector3 bottom, out float radius)
    {
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = transform.lossyScale;
        // axis（轴）：使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。
Vector3 axis;
        // heightScale（高度缩放）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。
float heightScale;
        // radiusScale（半径缩放）：使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。
float radiusScale;
        switch (capsuleCollider.direction)
        {
            case 0:
                axis = transform.right;
                heightScale = Mathf.Abs(scale.x);
                radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                break;
            case 2:
                axis = transform.forward;
                heightScale = Mathf.Abs(scale.z);
                radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                break;
            default:
                axis = transform.up;
                heightScale = Mathf.Abs(scale.y);
                radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                break;
        }

        // worldCenter（世界空间中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。
Vector3 worldCenter = transform.TransformPoint(localCenter);
        radius = capsuleCollider.radius * radiusScale + radiusPadding;
        // halfSegment（一半线段）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonController.GetWorldCapsule(Vector3, float, float, out Vector3, out Vector3, out float)。
float halfSegment = Mathf.Max(0f, localHeight * heightScale * 0.5f - radius);
        top = worldCenter + axis * halfSegment;
        bottom = worldCenter - axis * halfSegment;
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 的参数、作为 LeonController.ResolveHitDirection(Vector3) 的参数。</param>
    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        ApplyDamage(damage, hitPoint, ResolveHitDirection(hitPoint));
    }

    /// <summary>
    /// 带攻击来源的近战伤害入口，用于选择左、中、右受击动画。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyDeadChainsawPulse()、EnemyCombatModule.TryApplyAttackHit(int)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 的参数。</param>
    /// <param name="damageSourcePosition">中文含义：伤害来源位置。使用位置：作为 LeonController.ResolveHitDirection(Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 LeonController.ApplyDamage(float, Vector3, LeonHitDirection) 的参数。</param>
    public void TakeDamageFrom(float damage, Vector3 damageSourcePosition, Vector3 hitPoint)
    {
        ApplyDamage(damage, hitPoint, ResolveHitDirection(damageSourcePosition));
    }

    internal void ApplyRearHoldDamage(float damage)
    {
        if (Health == null || Health.IsDead || damage <= 0f)
        {
            return;
        }
        Health.TakeDamage(damage);
        if (Health.IsDead)
        {
            qteCoordinator.Cancel(qteCoordinator.CurrentSessionId, QTECancelReason.PlayerDied);
            BeginDeathSequence();
        }
    }

    private float nextStandingMeleeDamageAt;

    internal void TakeMeleeContactDamage(float damage, Vector3 source, Vector3 point, Vector3 normal)
    {
        if (Time.time < nextStandingMeleeDamageAt || Health == null)
        {
            return;
        }

        float healthBefore = Health.CurrentHealth;
        ApplyDamage(damage, point, ResolveHitDirection(source), normal);
        if (Health.CurrentHealth < healthBefore)
        {
            nextStandingMeleeDamageAt = Time.time + .65f;
        }
    }

    /// <summary>ApplyDamage 的业务处理入口；本方法直接依赖 HealthComponent.TakeDamage(float)、LeonAnimatorDriver.ClearPendingInjuredTransition()、LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonCombat.ForceStopShoot()、LeonCombat.HandleHit(LeonHitDirection)、LeonController.BeginDeathSequence()、LeonController.ForceExitCrouch() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.TakeDamage(float, Vector3)、LeonController.TakeDamage(float)、LeonController.TakeDamageFrom(float, Vector3, Vector3)；下游（含内部回调体） HealthComponent.TakeDamage(float)、LeonAnimatorDriver.ClearPendingInjuredTransition()、LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonCombat.ForceStopShoot()、LeonCombat.HandleHit(LeonHitDirection)、LeonController.BeginDeathSequence()、LeonController.ForceExitCrouch() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 HealthComponent.TakeDamage(float) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 LeonController.PlayDamageFeedback(Vector3) 的参数。</param>
    /// <param name="hitDirection">中文含义：命中方向。使用位置：作为 LeonCombat.HandleHit(LeonHitDirection) 的参数、作为 LeonWindowTraversal.QueueDeferredHit(LeonHitDirection) 的参数。</param>
    private void ApplyDamage(float damage, Vector3 hitPoint, LeonHitDirection hitDirection, Vector3 hitNormal = default)
    {
        if (Health == null || Health.IsDead)
        {
            return;
        }

        if (qteCoordinator != null && qteCoordinator.BlocksOrdinaryDamage)
        {
            return;
        }

        // 伤害结算期间阻止生命事件直接修改动画层
        damageResolutionInProgress = true;
        float previousHealth = Health.CurrentHealth;
        Health.TakeDamage(damage);
        damageResolutionInProgress = false;
        if (combat != null)
        {
            combat.ForceStopShoot();
        }

        PlayDamageFeedback(hitPoint, previousHealth - Health.CurrentHealth, hitNormal);
        if (Health.IsDead)
        {
            // 死亡优先级最高：清除待处理受伤切换，死亡动画完整覆盖
            // receiverValue29（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。
global::LeonAnimatorDriver receiverValue29 = // 死亡优先级最高：清除待处理受伤切换，死亡动画完整覆盖
 animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue29, null))
            {
                receiverValue29.ClearPendingInjuredTransition();
            }

            BeginDeathSequence();
        }
        else if (windowTraversal != null && windowTraversal.IsTraversing)
        {
            // 翻越中的延迟受击：保留待处理受伤目标，等待延迟受击的恢复阶段
            // receiverValue30（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。
global::LeonAnimatorDriver receiverValue30 = // 翻越中的延迟受击：保留待处理受伤目标，等待延迟受击的恢复阶段
 animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue30, null))
            {
                receiverValue30.RequestInjuredState(IsInjured, true);
            }

            windowTraversal.QueueDeferredHit(hitDirection);
        }
        else
        {
            ForceExitCrouch();
            // hitStarted（命中是否已开始）：使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。
bool hitStarted = combat != null && combat.HandleHit(hitDirection);
            if (hitStarted || IsHitAnimationActive())
            {
                // 受击成功启动（或已有受击动画在播）：只更新待处理目标
                // receiverValue31（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。
global::LeonAnimatorDriver receiverValue31 = // 受击成功启动（或已有受击动画在播）：只更新待处理目标
 animDriver;
                if (!global::System.Object.ReferenceEquals(receiverValue31, null))
                {
                    receiverValue31.RequestInjuredState(IsInjured, true);
                }
            }
            else
            {
                // 受击请求失败且无受击动画在播：立即按普通淡入同步受伤层
                // receiverValue32（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。
global::LeonAnimatorDriver receiverValue32 = // 受击请求失败且无受击动画在播：立即按普通淡入同步受伤层
 animDriver;
                if (!global::System.Object.ReferenceEquals(receiverValue32, null))
                {
                    receiverValue32.RequestInjuredState(IsInjured, false);
                }
            }
        }
    }

    /// <summary>当前是否有受击全身事务正在播放（用于受击期间的再次伤害判断）</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.HandleDeferredTraversalHit(LeonHitDirection)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsHitAnimationActive()
    {
        return animCoordinator != null && animCoordinator.IsHitTransactionActive;
    }

    /// <summary>
    /// 窗口翻越收尾时播放延迟受击的入口。
    /// 受击请求失败时立即按普通淡入同步受伤层，避免待处理目标永久挂起。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.FinishTraversal(bool)；下游（含内部回调体） LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonCombat.HandleHit(LeonHitDirection)、LeonController.IsHitAnimationActive()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitDirection">中文含义：命中方向。使用位置：作为 LeonCombat.HandleHit(LeonHitDirection) 的参数。</param>
    public void HandleDeferredTraversalHit(LeonHitDirection hitDirection)
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        // hitStarted（命中是否已开始）：使用位置：LeonController.HandleDeferredTraversalHit(LeonHitDirection)。
bool hitStarted = combat != null && combat.HandleHit(hitDirection);
        if (hitStarted || IsHitAnimationActive())
        {
            // receiverValue33（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.HandleDeferredTraversalHit(LeonHitDirection)。
global::LeonAnimatorDriver receiverValue33 = animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue33, null))
            {
                receiverValue33.RequestInjuredState(IsInjured, true);
            }
        }
        else
        {
            // receiverValue34（判空前缓存的动画驱动器）：从动画驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.HandleDeferredTraversalHit(LeonHitDirection)。
global::LeonAnimatorDriver receiverValue34 = animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                receiverValue34.RequestInjuredState(IsInjured, false);
            }
        }
    }

    /// <summary>ResolveHitDirection 的业务处理入口；本方法直接依赖 Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.TakeDamage(float, Vector3)、LeonController.TakeDamageFrom(float, Vector3, Vector3)；下游（含内部回调体） Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damageSourcePosition">中文含义：伤害来源位置。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。</param>
    private LeonHitDirection ResolveHitDirection(Vector3 damageSourcePosition)
    {
        // localSource（局部来源）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：LeonController.ResolveHitDirection(Vector3)。
Vector3 localSource = transform.InverseTransformPoint(damageSourcePosition);
        localSource.y = 0f;
        if (localSource.sqrMagnitude <= 0.0001f)
        {
            return LeonHitDirection.Center;
        }

        // normalizedHorizontal（归一化水平）：使用位置：LeonController.ResolveHitDirection(Vector3)。
float normalizedHorizontal = localSource.x / localSource.magnitude;
        if (normalizedHorizontal >= hitSideThreshold)
        {
            return LeonHitDirection.Right;
        }

        if (normalizedHorizontal <= -hitSideThreshold)
        {
            return LeonHitDirection.Left;
        }

        return LeonHitDirection.Center;
    }

    /// <summary>
    /// 快速反应事件失败专用伤害入口。
    /// 只有当前失败会话可以消费一次许可，并且不会启动普通受击事务。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CommitBiteDamage(int)；下游（含内部回调体） EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3)、HealthComponent.TakeDamage(float)、LeonCombat.ForceStopShoot()、LeonController.BeginDeathSequence()、LeonController.PlayDamageFeedback(Vector3)、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.TryConsumeFailureDamageAuthorization(int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonQTECoordinator.Cancel(int, QTECancelReason) 的参数、作为 LeonQTECoordinator.TryConsumeFailureDamageAuthorization(int) 的参数。</param>
    /// <param name="damage">中文含义：伤害。使用位置：作为 HealthComponent.TakeDamage(float) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3) 的参数、作为 LeonController.PlayDamageFeedback(Vector3) 的参数。</param>
    /// <param name="hitNormal">中文含义：命中表面法线。使用位置：作为 EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3) 的参数、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)。</param>
    public bool TryApplyQTEFailureDamage(int sessionId, float damage, Vector3 hitPoint, Vector3 hitNormal, bool playBlood = true)
    {
        if (Health == null || Health.IsDead || qteCoordinator == null)
        {
            return false;
        }

        if (!qteCoordinator.TryConsumeFailureDamageAuthorization(sessionId))
        {
            return false;
        }

        float previousHealth = Health.CurrentHealth;
        Health.TakeDamage(damage);
        // receiverValue35（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)。
global::LeonCombat receiverValue35 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue35, null))
        {
            receiverValue35.ForceStopShoot();
        }

        PlayDamageFeedback(hitPoint, previousHealth - Health.CurrentHealth, hitNormal, false);
        if (hitNormal == Vector3.zero)
        {
            hitNormal = transform.forward;
        }

        // receiverValue36（判空前缓存的敌人命中效果管理器的实例）：从敌人命中效果管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)。
global::EnemyHitEffectManager receiverValue36 = EnemyHitEffectManager.Instance;
        if (playBlood && previousHealth > Health.CurrentHealth && receiverValue36 != null)
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.StabBlood, hitPoint, hitNormal);
        }

        if (!Health.IsDead)
        {
            return true;
        }

        if (!qteCoordinator.OwnsNativeFailureDeath(sessionId))
        {
            qteCoordinator.Cancel(sessionId, QTECancelReason.PlayerDied);
            BeginDeathSequence();
        }
        return true;
    }

    internal void CompleteNativeQTEDeath()
    {
        if (animCoordinator != null && qteCoordinator != null && qteCoordinator.HoldsNativeDeathPose)
        {
            animCoordinator.UseCompletedPairDeathPose();
        }
        BeginDeathSequence();
        HandleDeathAnimationCompleted();
    }

    /// <summary>
    /// 生命值归零后的唯一死亡入口。这里只锁定玩法并启动死亡动画，
    /// 游戏结束界面由死亡动画完成通知触发。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)；下游（含内部回调体） LeonAnimationActionCoordinator.StopCurrentHorizontalMotion()、LeonCombat.HandleDeath()、LeonController.ChangeState(LeonState)、LeonController.ForceExitCrouch()、LeonController.WaitForDeathCompletionFallback()、LeonInputBridge.LockForDeath()、LeonPlayerAudio.PlayDeath() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginDeathSequence()
    {
        if (deathSequenceStarted)
        {
            return;
        }

        deathSequenceStarted = true;
        inputState.moveInput = Vector3.zero;
        inputState.lookInput = Vector2.zero;
        inputState.isSprinting = false;
        inputState.isFreeLooking = false;
        ForceExitCrouch();
        // receiverValue37（判空前缓存的输入桥接）：从输入桥接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.BeginDeathSequence()。
global::LeonInputBridge receiverValue37 = inputBridge;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.LockForDeath();
        }

        // receiverValue38（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.BeginDeathSequence()。
global::LeonAnimationActionCoordinator receiverValue38 = animCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue38, null))
        {
            receiverValue38.StopCurrentHorizontalMotion();
        }

        // receiverValue39（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.BeginDeathSequence()。
global::LeonCombat receiverValue39 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue39, null))
        {
            receiverValue39.HandleDeath();
        }

        // receiverValue40（判空前缓存的玩家音频）：从玩家音频读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.BeginDeathSequence()。
global::LeonPlayerAudio receiverValue40 = playerAudio;
        if (!global::System.Object.ReferenceEquals(receiverValue40, null))
        {
            receiverValue40.PlayDeath();
        }

        ChangeState(LeonState.Dead);
        deathCompletionFallbackRoutine = StartCoroutine(WaitForDeathCompletionFallback());
    }

    /// <summary>HandleDeathAnimationCompleted 的业务处理入口；本方法直接依赖 LeonController.ReportDeathCompletion()、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ReportDeathCompletion()、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandleDeathAnimationCompleted()
    {
        if (!deathSequenceStarted || deathCompletionReported || currentState != LeonState.Dead)
        {
            return;
        }

        if (!ReportDeathCompletion())
        {
            return;
        }

        if (deathCompletionFallbackRoutine != null)
        {
            StopCoroutine(deathCompletionFallbackRoutine);
            deathCompletionFallbackRoutine = null;
        }
    }

    /// <summary>WaitForDeathCompletionFallback 的业务处理入口；本方法直接依赖 LeonController.ReportDeathCompletion()、Mathf.Max(float, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.BeginDeathSequence()；下游（含内部回调体） LeonController.ReportDeathCompletion()、Mathf.Max(float, float)、WaitForSecondsRealtime.WaitForSecondsRealtime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator WaitForDeathCompletionFallback()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, deathResultFallbackDelay));
        deathCompletionFallbackRoutine = null;
        var driver = GetComponent<LeonAnimatorDriver>();
        while (!deathCompletionReported && deathSequenceStarted && currentState == LeonState.Dead &&
            (GameManager.Instance == null || driver != null && driver.IsDeathAnimationPlaying()))
        {
            yield return null;
        }

        ReportDeathCompletion();
    }

    /// <summary>ReportDeathCompletion 的业务处理入口；本方法直接依赖 GameManager.OnPlayerDeath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.HandleDeathAnimationCompleted()、LeonController.WaitForDeathCompletionFallback()；下游（含内部回调体） GameManager.OnPlayerDeath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool ReportDeathCompletion()
    {
        if (!deathSequenceStarted || deathCompletionReported || currentState != LeonState.Dead || GameManager.Instance == null)
        {
            return false;
        }

        deathCompletionReported = true;
        GameManager.Instance.OnPlayerDeath();
        return true;
    }

    /// <summary>PlayDamageFeedback 的业务处理入口；本方法直接依赖 AudioManager.Play3D(string, Vector3)、CombatHUD.HitFlash()、DamageSystem.SpawnHitEffect(Vector3, Vector3, bool)、object.ReferenceEquals(object, object)、RE4CameraController.Shake(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)；下游（含内部回调体） AudioManager.Play3D(string, Vector3)、CombatHUD.HitFlash()、DamageSystem.SpawnHitEffect(Vector3, Vector3, bool)、object.ReferenceEquals(object, object)、RE4CameraController.Shake(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 DamageSystem.SpawnHitEffect(Vector3, Vector3, bool) 的参数。</param>
    private void PlayDamageFeedback(Vector3 hitPoint, float healthLost, Vector3 hitNormal = default, bool spawnGenericImpact = true)
    {
        if (playerAudio != null && Health != null && !Health.IsDead && healthLost > 0f)
        {
            playerAudio.PlayHurt(healthLost / Mathf.Max(1f, Health.MaxHealth));
        }

        // receiverValue43（判空前缓存的伤害系统的实例）：从伤害系统的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.PlayDamageFeedback(Vector3)。
global::DamageSystem receiverValue43 = DamageSystem.Instance;
        if (spawnGenericImpact && receiverValue43 != null)
        {
            if (hitNormal.sqrMagnitude < .001f)
            {
                hitNormal = transform.forward;
            }

            receiverValue43.SpawnHitEffect(hitPoint, hitNormal, false);
        }

        // receiverValue44（判空前缓存的战斗战斗显示界面的实例）：从战斗战斗显示界面的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.PlayDamageFeedback(Vector3)。
global::CombatHUD receiverValue44 = CombatHUD.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.HitFlash();
        }

        // receiverValue45（判空前缓存的摄像机控制器）：从摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.PlayDamageFeedback(Vector3)。
global::RE4CameraController receiverValue45 = cameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.Shake(0.1f, 1.5f);
        }
    }

    /// <summary>PrepareForQTE 的业务处理入口；本方法直接依赖 LeonCombat.InterruptForQTE()、LeonController.ChangeState(LeonState)、LeonController.TrySetCrouching(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） LeonCombat.InterruptForQTE()、LeonController.ChangeState(LeonState)、LeonController.TrySetCrouching(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool PrepareForQTE()
    {
        if (currentState == LeonState.Dead)
        {
            return false;
        }

        if (inputState.isCrouching && !TrySetCrouching(false))
        {
            return false;
        }

        inputState.moveInput = Vector3.zero;
        inputState.lookInput = Vector2.zero;
        inputState.isSprinting = false;
        inputState.isFreeLooking = false;
        // receiverValue46（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.PrepareForQTE()。
global::LeonCombat receiverValue46 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue46, null))
        {
            receiverValue46.InterruptForQTE();
        }

        ChangeState(LeonState.QTE);
        return true;
    }

    /// <summary>FinishQTEState 的业务处理入口；本方法直接依赖 LeonController.ChangeState(LeonState)、LeonLocomotion.ResumeAfterQTESuccess()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） LeonController.ChangeState(LeonState)、LeonLocomotion.ResumeAfterQTESuccess()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveMovementInput">中文含义：是否保留移动输入。使用位置：LeonController.FinishQTEState(bool)。</param>
    public void FinishQTEState(bool preserveMovementInput = false)
    {
        if (currentState != LeonState.QTE)
        {
            return;
        }

        if (!preserveMovementInput)
        {
            inputState.moveInput = Vector3.zero;
        }

        inputState.lookInput = Vector2.zero;
        if (!preserveMovementInput)
        {
            inputState.isSprinting = false;
        }

        inputState.isFreeLooking = false;
        ChangeState(LeonState.Idle);
        if (preserveMovementInput)
        {
            // receiverValue47（判空前缓存的移动行为）：从移动行为读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.FinishQTEState(bool)。
global::LeonLocomotion receiverValue47 = locomotion;
            if (!global::System.Object.ReferenceEquals(receiverValue47, null))
            {
                receiverValue47.ResumeAfterQTESuccess();
            }
        }
    }

    /// <summary>中文含义：索引物品使用目标是否有效。</summary>
    bool IItemUseTarget.Valid => this != null;

    /// <summary>中文含义：索引物品使用目标当前生命值。</summary>
    float IItemUseTarget.CurrentHealth => currentHealth;

    /// <summary>中文含义：索引物品使用目标生命最大。</summary>
    float IItemUseTarget.HealthMaximum
    {
        get
        {
            if (Health != null)
            {
                return Health.MaxHealth;
            }
            else
            {
                return maxHealth;
            }
        }
    }

    /// <summary>中文含义：索引物品使用目标是否具有武器模块。</summary>
    bool IItemUseTarget.HasWeaponModule => weaponHandler != null;

    /// <summary>IItemUseTarget.IncreaseHealthMaximum 的业务处理入口；本方法直接依赖 HealthComponent.IncreaseMaxHealth(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） HealthComponent.IncreaseMaxHealth(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="count">中文含义：数量。使用位置：作为 HealthComponent.IncreaseMaxHealth(float) 的参数。</param>
    void IItemUseTarget.IncreaseHealthMaximum(float count)
    {
        if (Health != null)
        {
            Health.IncreaseMaxHealth(count);
            maxHealth = Health.MaxHealth;
        }
    }

    /// <summary>IItemUseTarget.Healing 的业务处理入口；本方法直接依赖 LeonController.Heal(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.Heal(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="count">中文含义：数量。使用位置：作为 LeonController.Heal(float) 的参数。</param>
    void IItemUseTarget.Healing(float count) => Heal(count);
    /// <summary>IItemUseTarget.IncreaseCompatibleReserveAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.AddReserveAmmo(WeaponType, int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.AddReserveAmmo(WeaponType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前武器类别。使用位置：作为 LeonWeaponHandler.AddReserveAmmo(WeaponType, int) 的参数。</param>
    /// <param name="count">中文含义：数量。使用位置：作为 LeonWeaponHandler.AddReserveAmmo(WeaponType, int) 的参数。</param>
    void IItemUseTarget.IncreaseCompatibleReserveAmmo(WeaponType type, int count)
    {
        // receiverValue49（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.IItemUseTarget.IncreaseCompatibleReserveAmmo(WeaponType, int)。
global::LeonWeaponHandler receiverValue49 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.AddReserveAmmo(type, count);
        }
    }

    /// <summary>FireWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.Fire()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()、LeonCombat.Update()；下游（含内部回调体） LeonWeaponHandler.Fire()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool FireWeapon() => weaponHandler != null && weaponHandler.Fire();

    internal void FlushWeaponMuzzleFlashes()
    {
        weaponHandler?.GetCurrentWeapon()?.FlushChapterOneMuzzleFlashes();
    }
    /// <summary>CompleteReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CompleteReload()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.CompleteReload()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteReload()
    {
        // receiverValue50（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.CompleteReload()。
global::LeonWeaponHandler receiverValue50 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.CompleteReload();
        }
    }

    /// <summary>StartReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.StartReload()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleReload()；下游（含内部回调体） LeonWeaponHandler.StartReload()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StartReload()
    {
        // receiverValue51（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.StartReload()。
global::LeonWeaponHandler receiverValue51 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue51, null))
        {
            receiverValue51.StartReload();
        }
    }

    /// <summary>CanReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CanReload()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleReload()；下游（含内部回调体） LeonWeaponHandler.CanReload()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanReload()
    {
        return weaponHandler != null && weaponHandler.CanReload();
    }

    /// <summary>GetCurrentAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetCurrentAmmo()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()；下游（含内部回调体） LeonWeaponHandler.GetCurrentAmmo()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int GetCurrentAmmo()
    {
        if (weaponHandler != null)
        {
            return weaponHandler.GetCurrentAmmo();
        }
        else
        {
            return 0;
        }
    }

    /// <summary>GetMaxAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetMaxAmmo()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.GetMaxAmmo()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int GetMaxAmmo()
    {
        if (weaponHandler != null)
        {
            return weaponHandler.GetMaxAmmo();
        }
        else
        {
            return 0;
        }
    }

    /// <summary>SwitchWeapon 的业务处理入口；本方法直接依赖 LeonController.TrySwitchWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.TrySwitchWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="slot">中文含义：槽位。使用位置：作为 LeonController.TrySwitchWeapon(WeaponType) 的参数。</param>
    public void SwitchWeapon(int slot)
    {
        TrySwitchWeapon((WeaponType)slot);
    }

    /// <summary>TrySwitchWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.SwitchTo(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.SwitchWeapon(int)；下游（含内部回调体） LeonWeaponHandler.SwitchTo(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">中文含义：武器类型。使用位置：作为 LeonWeaponHandler.SwitchTo(WeaponType) 的参数。</param>
    public bool TrySwitchWeapon(WeaponType weaponType)
    {
        return weaponHandler != null && weaponHandler.SwitchTo(weaponType);
    }

    /// <summary>中文含义：是否正在射击。</summary>
    public bool IsShooting => currentState == LeonState.Shooting;
    /// <summary>中文含义：是否正在瞄准。使用位置：作为 FreeLookState.SetKeyBinding(bool, bool, float) 的参数、CrosshairPresenterValue.Update()、CrosshairUI.Update() 等共 7 处。</summary>
    public bool IsAiming => combat != null && combat.IsAiming;
    /// <summary>中文含义：是否小刀攻击生效。使用位置：RE4RThirdPersonCamera.UpdateMeleePush(float)。</summary>
    public bool IsKnifeAttackActive => combat != null && combat.IsKnifeAttackActive;
    /// <summary>中文含义：是否忙碌。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    public bool IsBusy => currentState == LeonState.Shooting || currentState == LeonState.Reloading || currentState == LeonState.QTE || currentState == LeonState.MeleeAttacking || currentState == LeonState.Traversing;

    /// <summary>TriggerMeleeCameraPush 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、RE4CameraController.TriggerMeleePush()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.StartMeleeAction(FullBodyActionType, int)；下游（含内部回调体） object.ReferenceEquals(object, object)、RE4CameraController.TriggerMeleePush()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TriggerMeleeCameraPush()
    {
        // receiverValue54（判空前缓存的摄像机控制器）：从摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonController.TriggerMeleeCameraPush()。
global::RE4CameraController receiverValue54 = cameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue54, null))
        {
            receiverValue54.TriggerMeleePush();
        }
    }

    /// <summary>RegisterCameraController 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.BindPlayer(LeonController, Transform)、RE4CameraController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controllerReference">中文含义：控制器引用。使用位置：LeonController.RegisterCameraController(RE4CameraController, Camera, Transform)。</param>
    /// <param name="cameraReference">中文含义：摄像机引用。使用位置：LeonController.RegisterCameraController(RE4CameraController, Camera, Transform)。</param>
    /// <param name="aimTargetReference">中文含义：瞄准目标引用。使用位置：LeonController.RegisterCameraController(RE4CameraController, Camera, Transform)。</param>
    public void RegisterCameraController(RE4CameraController controllerReference, Camera cameraReference, Transform aimTargetReference)
    {
        cameraController = controllerReference;
        if (cameraReference != null)
        {
            playerCamera = cameraReference;
        }

        if (aimTargetReference != null)
        {
            aimTarget = aimTargetReference;
        }
    }

    /// <summary>UnregisterCameraController 的业务处理入口；本方法直接依赖 LeonController.ResetFreeView()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.BindPlayer(LeonController, Transform)、RE4CameraController.OnDestroy()、RE4CameraController.UnbindPlayer(LeonController)；下游（含内部回调体） LeonController.ResetFreeView()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controllerReference">中文含义：控制器引用。使用位置：LeonController.UnregisterCameraController(RE4CameraController)。</param>
    public void UnregisterCameraController(RE4CameraController controllerReference)
    {
        if (cameraController != controllerReference)
        {
            return;
        }

        ResetFreeView();
        cameraController = null;
    }

    /// <summary>中文含义：摄像机控制器。使用位置：KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、LeonWeaponHandler.Fire() 等共 4 处。</summary>
    public RE4CameraController CameraController => cameraController;

    internal void ApplyKnifeCameraHeading(float heading)
    {
        if (combat != null && combat.knifeModule != null)
        {
            combat.knifeModule.ApplyCameraHeading(heading);
        }
    }

    internal bool IsKnifeGuardAiming => combat != null && combat.knifeModule != null && combat.knifeModule.IsGuardAiming;
    // === 管线访问器 ===
    /// <summary>中文含义：动画驱动器。</summary>
    public LeonAnimatorDriver AnimDriver => animDriver;
    /// <summary>中文含义：动画协调器。</summary>
    public LeonAnimationActionCoordinator AnimCoordinator => animCoordinator;
    /// <summary>中文含义：动画绑定组件。</summary>
    public LeonAnimRig AnimRig => animRig;
    /// <summary>中文含义：配对动作协调器。</summary>
    public LeonQTECoordinator QTECoordinator => qteCoordinator;
    /// <summary>中文含义：新动作管线是否有效。使用位置：KnifeCombatModule.CanExecute.get、LeonCombat.CanBeginParryAction.get、LeonCombat.ExecuteFirearmAttack() 等共 23 处。</summary>
    public bool NewPipelineValid => newPipelineValid;

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MonoBehaviour.StopCoroutine(Coroutine)、Singleton&lt;LeonController&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        if (deathCompletionFallbackRoutine != null)
        {
            StopCoroutine(deathCompletionFallbackRoutine);
            deathCompletionFallbackRoutine = null;
        }

        if (animCoordinator != null)
        {
            animCoordinator.OnDeathAnimationCompleted -= HandleDeathAnimationCompleted;
        }

        if (Health != null)
        {
            Health.OnHealthChanged -= HandleHealthChanged;
        }

        base.OnDestroy();
    }
}
