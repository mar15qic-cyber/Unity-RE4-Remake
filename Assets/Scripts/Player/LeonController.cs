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

    /// <summary>初始状态。</summary>
    [Header("当前状态")]
    [UnityEngine.Serialization.FormerlySerializedAs("currentState")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始状态")]
    [UnityEngine.InspectorName("初始状态")]
    private LeonState initialState = LeonState.Idle;
    /// <summary>当前状态。</summary>
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

    /// <summary>初始武器。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("currentWeapon")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始武器")]
    [UnityEngine.InspectorName("初始武器")]
    private WeaponType initialWeaponValue = WeaponType.Pistol;
    /// <summary>初始装备。</summary>
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

    /// <summary>当前武器。</summary>
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

    /// <summary>玩家摄像机。</summary>
    [Header("引用")]
    public Camera playerCamera;
    /// <summary>瞄准目标。</summary>
    public Transform aimTarget;
    /// <summary>中文含义：武器挂点。</summary>
    public Transform weaponSocket;
    /// <summary>最大生命值。</summary>
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
    /// <summary>运行速度。</summary>
    public float runSpeed = 3.90f;
    /// <summary>旋转速度。</summary>
    public float rotationSpeed = 10f;
    /// <summary>中文含义：射击移动速度。</summary>
    public float shootMoveSpeed = 1.83f;
    /// <summary>蹲伏时的胶囊体高度，不能小于直径。</summary>
    [Header("蹲伏碰撞")]
    [Tooltip("蹲伏时的胶囊体高度，不能小于直径")]
    public float crouchColliderHeight = 1.2f;
    /// <summary>起身空间检测额外增加的半径余量。</summary>
    [Tooltip("起身空间检测额外增加的半径余量")]
    public float crouchClearancePadding = 0.02f;
    /// <summary>参与起身空间检测的碰撞层。</summary>
    [Tooltip("参与起身空间检测的碰撞层")]
    public LayerMask crouchObstructionMask = ~0;
    /// <summary>攻击来源横向分量超过此值时，播放对应的左侧或右侧受击动画。</summary>
    [Header("受击方向")]
    [Tooltip("攻击来源横向分量超过此值时，播放对应的左侧或右侧受击动画")]
    [Range(0f, 1f)]
    public float hitSideThreshold = 0.35f;
    /// <summary>生命比例小于等于该值时进入受伤状态。</summary>
    [Header("低血量受伤状态")]
    [Tooltip("生命比例小于等于该值时进入受伤状态")]
    [Range(0.05f, 0.9f)]
    public float injuredEnterRatio = 0.35f;
    /// <summary>生命比例恢复至该值以上才退出受伤状态（迟滞区间防止抖动）。</summary>
    [Tooltip("生命比例恢复至该值以上才退出受伤状态（迟滞区间防止抖动）")]
    [Range(0.05f, 1f)]
    public float injuredExitRatio = 0.40f;
    /// <summary>中文含义：是否受伤。逻辑受伤状态：进入阈值立即生效，退出需达到恢复阈值</summary>
    public bool IsInjured { get; private set; }

    /// <summary>背后环抱等 QTE 释放后的短暂无敌截止时间。</summary>
    private float qteReleaseInvulnerableUntil;

    /// <summary>当前是否处于 QTE 释放后的短暂无敌窗口内。</summary>
    internal bool IsQTEReleaseInvulnerable
    {
        get
        {
            return Time.time < qteReleaseInvulnerableUntil;
        }
    }

    /// <summary>授予玩家一段短暂的 QTE 释放后普通伤害无敌窗口。</summary>
    internal void GrantQTEReleaseInvulnerability(float duration)
    {
        if (duration > 0f)
        {
            qteReleaseInvulnerableUntil = Mathf.Max(qteReleaseInvulnerableUntil, Time.time + duration);
        }
    }

    /// <summary>死亡动画完成通知丢失时，等待多久后强制进入死亡结算界面。</summary>
    [Header("死亡流程")]
    [Tooltip("死亡动画完成通知丢失时，等待多久后强制进入死亡结算界面")]
    [Min(0.1f)]
    public float deathResultFallbackDelay = 2.5f;
    /// <summary>横向瞄准平滑。</summary>
    [Header("Aim Offset 平滑")]
    public float horAimSmooth = 0.75f;
    /// <summary>纵向瞄准平滑。</summary>
    public float verAimSmooth = 8f;
    /// <summary>纵向瞄准增益。抬头/低头动画触发增益，&gt;1 放大 verAim 让 BlendTree 更容易进入上下方向。</summary>
    [Tooltip("抬头/低头动画触发增益，>1 放大 verAim 让 BlendTree 更容易进入上下方向")]
    public float verAimGain = 1.5f;
    /// <summary>低头角度下限。</summary>
    [Tooltip("低头角度下限")]
    public float verAimMin = -45f;
    /// <summary>平滑横向瞄准。</summary>
    private float smoothHorAim;
    /// <summary>平滑纵向瞄准。</summary>
    private float smoothVerAim;
    // === 运行时输入状态 ===
    /// <summary>输入状态。</summary>
    [SerializeField]
    private InputState inputState;
    /// <summary>中文含义：输入数据。输入状态（只读访问）</summary>
    public ref readonly InputState Input => ref inputState;
    // === 向后兼容属性 ===
    /// <summary>移动输入。</summary>
    public Vector3 MoveInput => inputState.moveInput;
    /// <summary>观察输入。</summary>
    public Vector2 LookInput => inputState.lookInput;
    /// <summary>是否正在冲刺。</summary>
    public bool IsSprinting => inputState.isSprinting;
    /// <summary>中文含义：当前平面速度。角色当前实际水平移动速度。镜头等表现层必须以此值判断是否已经进入奔跑， 不能只根据按键状态提前切换。</summary>
    public float CurrentPlanarSpeed { get => HorizontalSpeed.magnitude; }
    /// <summary>是否实际生效的冲刺中。</summary>
    public bool IsEffectiveSprinting => inputState.isSprinting && !inputState.isCrouching && inputState.moveInput.sqrMagnitude > 0.01f && currentState != LeonState.Aiming && currentState != LeonState.Shooting && currentState != LeonState.Reloading && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying && (currentState != LeonState.Staggered || animDriver != null && animDriver.HitMovementReleased) && currentState != LeonState.QTE && currentState != LeonState.Traversing && currentState != LeonState.Dead;

    /// <summary>自由观察。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly FreeLookState freeObserve = new FreeLookState();
    /// <summary>是否自由观察中。</summary>
    public bool IsFreeLooking => freeObserve.Phase == FreeLookPhase.Observe;
    /// <summary>当前自由环顾阶段。</summary>
    public FreeLookPhase CurrentFreeLookPhase => freeObserve.Phase;
    /// <summary>是否隔离观察摄像机转向。</summary>
    public bool IsolateObserveCameraSteering => freeObserve.IsolateCameraSteering;
    /// <summary>是否跳跃中。</summary>
    public bool IsJumping => inputState.isJumping;
    /// <summary>是否正在蹲伏。</summary>
    public bool IsCrouching => inputState.isCrouching;
    internal bool IsFirearmStanceBlocked => IsCrouching || animDriver != null && animDriver.IsRisingForFirearm;
    /// <summary>是否着地。</summary>
    public bool IsGrounded => inputState.isGrounded;

    // === 输入写入方法（由 LeonInputBridge 调用）===
    /// <summary>SetMoveInput 的业务处理入口；本方法直接依赖 FreeLookState.UpdateMovementInput(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnMove(InputAction.CallbackContext) 等共 7 处；下游（含内部回调体） FreeLookState.UpdateMovementInput(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
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
    /// <param name="value">值。</param>
    public void SetLookInput(Vector2 value) => inputState.lookInput = value;
    /// <summary>SetSprinting 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.BeginDrop()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnSprintEnd(InputAction.CallbackContext) 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">是否值。</param>
    public void SetSprinting(bool value) => inputState.isSprinting = value && !inputState.isCrouching;
    /// <summary>SetFreeLooking 的业务处理入口；本方法直接依赖 FreeLookState.SetKeyBinding(bool, bool, float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnFreeLookEnd(InputAction.CallbackContext)、LeonInputBridge.OnFreeLookStart(InputAction.CallbackContext)、LeonInputBridge.Update() 等共 6 处；下游（含内部回调体） FreeLookState.SetKeyBinding(bool, bool, float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">是否值。</param>
    public void SetFreeLooking(bool value)
    {
        inputState.isFreeLooking = value;
        // 原始阶段。
var originalPhase = freeObserve.Phase;
        freeObserve.SetKeyBinding(value, AllowFreeObserve && !IsAiming, ViewHorizontalAngle);
        if (originalPhase != freeObserve.Phase)
        {
            // 判空前缓存的摄像机控制器。
global::RE4CameraController receiverValue5 = cameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                // 判空前缓存的接收者值5的玩法驱动器。
global::RE4RThirdPersonCamera receiverValue6 = receiverValue5.gameplayDriver;
                if (!global::System.Object.ReferenceEquals(receiverValue6, null))
                {
                    receiverValue6.SynchronizeObserveDirection();
                }
            }
        }
    }

    /// <summary>视图水平角度。</summary>
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

    /// <summary>是否允许自由观察。</summary>
    private bool AllowFreeObserve => isActiveAndEnabled && applyHasFocus && (inputBridge == null || inputBridge.AllowFreeViewInput) && (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput) && (!GameUIManager.HasInstance || !GameUIManager.Instance.IsPlayerInputBlocked) && (cameraController == null || !cameraController.HasExternalCamera) && currentState != LeonState.Staggered && currentState != LeonState.Dead && currentState != LeonState.QTE && currentState != LeonState.Traversing && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying;

    /// <summary>应用是否具有焦点。</summary>
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
    /// <param name="input">输入数据。</param>
    public Vector3 CalculateMovementDirection(Vector3 input)
    {
        // 引用。
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
    /// <param name="timeStepSize">时间步进尺寸。</param>
    /// <param name="duration">持续时间。</param>
    public float AdvanceFreeViewRecentering(float timeStepSize, float duration) => freeObserve.AdvanceRecentering(timeStepSize, duration, transform.eulerAngles.y);
    /// <summary>InterruptFreeViewRecentering 的业务处理入口；本方法直接依赖 FreeLookState.InterruptRecentering()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)；下游（含内部回调体） FreeLookState.InterruptRecentering()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InterruptFreeViewRecentering()
    {
        freeObserve.InterruptRecentering();
        // 判空前缓存的摄像机控制器。
global::RE4CameraController receiverValue9 = cameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            // 判空前缓存的接收者值9的玩法驱动器。
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
            // 判空前缓存的摄像机控制器。
global::RE4CameraController receiverValue11 = cameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue11, null))
            {
                // 判空前缓存的接收者值11的玩法驱动器。
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
    /// <param name="hasFocus">是否具有焦点。</param>
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
    /// <summary>水平速度。</summary>
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

    /// <summary>运动刚体。</summary>
    private Rigidbody rb;
    /// <summary>移动行为。</summary>
    private LeonLocomotion locomotion;
    /// <summary>战斗。</summary>
    private LeonCombat combat;
    /// <summary>管理当前装备与武器切换的组件。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>摄像机控制器。</summary>
    private RE4CameraController cameraController;
    // === 管线组件缓存 ===
    /// <summary>动画驱动器。</summary>
    private LeonAnimatorDriver animDriver;
    /// <summary>动画协调器。</summary>
    private LeonAnimationActionCoordinator animCoordinator;
    /// <summary>当前里昂动画绑定组件。</summary>
    private LeonAnimRig animRig;
    /// <summary>武器视觉表现控制器。</summary>
    private LeonWeaponVisualController weaponVisualController;
    /// <summary>当前里昂配对动作协调器。</summary>
    private LeonQTECoordinator qteCoordinator;
    /// <summary>高处跳落落地。</summary>
    private LeonHighDropLanding highDropLanding;
    /// <summary>翻窗通行。</summary>
    private LeonWindowTraversal windowTraversal;
    /// <summary>输入桥接。</summary>
    private LeonInputBridge inputBridge;
    /// <summary>玩家音频。</summary>
    private LeonPlayerAudio playerAudio;
    /// <summary>胶囊碰撞体。</summary>
    private CapsuleCollider capsuleCollider;
    /// <summary>站立碰撞体高度。</summary>
    private float standingColliderHeight;
    /// <summary>站立碰撞体中心。</summary>
    private Vector3 standingColliderCenter;
    /// <summary>蹲伏中碰撞体中心。</summary>
    private Vector3 crouchingColliderCenter;
    /// <summary>蹲伏重叠结果集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Collider[] crouchOverlapResults = new Collider[32];
    /// <summary>新动作管线是否有效。</summary>
    private bool newPipelineValid;
    /// <summary>死亡序号是否已开始。</summary>
    private bool deathSequenceStarted;
    /// <summary>是否死亡完成已报告。</summary>
    private bool deathCompletionReported;
    /// <summary>死亡完成后备协程。</summary>
    private Coroutine deathCompletionFallbackRoutine;
    /// <summary>是否伤害分辨率内部进度。</summary>
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
    /// <param name="current">当前。</param>
    /// <param name="max">最大。</param>
    private void HandleHealthChanged(float current, float max)
    {
        // 判空前缓存的生命变化时的通知回调。
global::System.Action<float, float> receiverValue14 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.Invoke(current, max);
        }

        // 比例。
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
            // 判空前缓存的动画驱动器。
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
        // 初始武器。
WeaponType initialWeapon;
        if (currentWeapon == WeaponType.Rifle || currentWeapon == WeaponType.Shotgun || currentWeapon == WeaponType.Knife)
        {
            initialWeapon = currentWeapon;
        }
        else
        {
            initialWeapon = WeaponType.Pistol;
        }

        // 判空前缓存的管理当前装备与武器切换的组件。
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
            // 判空前缓存的管理当前装备与武器切换的组件。
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
        // 判空前缓存的动画协调器。
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

        // 判空前缓存的移动行为。
global::LeonLocomotion receiverValue21 = locomotion;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.HandleMovement();
        }
    }

    /// <summary>动作结束当帧立即恢复常规移动控制凭证并同步持续按住的移动输入。</summary>
    internal void SyncNormalMovementAuthorityImmediate()
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        if (animCoordinator != null)
        {
            animCoordinator.SynchronizeMotionControlAuthority();
        }

        if (inputBridge != null)
        {
            inputBridge.RestoreMovementInput();
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

        // 局部摄像机方向。
Vector3 localCamDir = transform.InverseTransformDirection(playerCamera.transform.forward);
        // 横向瞄准。
float horAim = Mathf.Atan2(localCamDir.x, localCamDir.z) * Mathf.Rad2Deg;
        // 纵向瞄准。
float verAim = Mathf.Asin(Mathf.Clamp(localCamDir.y, -1f, 1f)) * Mathf.Rad2Deg;
        // 是否瞄准中。
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

        // 摄像机方向。
Vector3 cameraDirection = transform.InverseTransformDirection(playerCamera.transform.forward);
        smoothHorAim = Mathf.Atan2(cameraDirection.x, cameraDirection.z) * Mathf.Rad2Deg;
        smoothVerAim = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(cameraDirection.y, -1f, 1f)) * Mathf.Rad2Deg * verAimGain, verAimMin, 45f);
        animDriver.SetAimAngles(smoothHorAim, smoothVerAim);
    }

    /// <summary>Heal 的业务处理入口；本方法直接依赖 HealthComponent.Heal(float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.IItemUseTarget.Healing(float)；下游（含内部回调体） HealthComponent.Heal(float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">大小。</param>
    public void Heal(float amount)
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        // 判空前缓存的生命。
global::HealthComponent receiverValue22 = Health;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.Heal(amount);
        }
    }

    /// <summary>ChangeState 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、LeonCombat.ApplyGuardPose()、LeonCombat.EndShoot()、LeonCombat.EndTemporaryKnife(bool)、LeonCombat.ExecuteFirearmAttack() 等共 23 处；下游（含内部回调体） LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="newState">新状态。</param>
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

        // 判空前缓存的动画协调器。
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
        return !IsHeldByTrap && CanActWithoutTrap();
    }

    internal bool CanActWithoutTrap()
    {
        if (animCoordinator == null || !animCoordinator.isActiveAndEnabled)
        {
            return false;
        }

        return currentState != LeonState.Staggered && currentState != LeonState.Dead && currentState != LeonState.QTE && currentState != LeonState.MeleeAttacking && currentState != LeonState.Parrying && currentState != LeonState.Traversing;
    }

    internal LeonTrapAvatar TrapAvatar { get; set; }
    internal bool IsHeldByTrap => TrapAvatar != null && TrapAvatar.IsPinned;

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
    /// <param name="damage">伤害。</param>
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
    /// <param name="crouching">是否蹲伏中。</param>
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
        // 数量。
int count = Physics.OverlapCapsuleNonAlloc(standingTop, crouchingTop, radius, crouchOverlapResults, crouchObstructionMask, QueryTriggerInteraction.Ignore);
        // 当前遍历索引。
for (int i = 0; i < count; i++)
        {
            // 命中。
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
        // 最小高度。
float minimumHeight = capsuleCollider.radius * 2f;
        crouchColliderHeight = Mathf.Clamp(crouchColliderHeight, minimumHeight, standingColliderHeight);
        // 站立底部。
float standingBottom = standingColliderCenter.y - standingColliderHeight * 0.5f;
        crouchingColliderCenter = standingColliderCenter;
        crouchingColliderCenter.y = standingBottom + crouchColliderHeight * 0.5f;
    }

    /// <summary>ApplyCrouchState 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetCrouching(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ForceExitCrouch()、LeonController.TrySetCrouching(bool)；下游（含内部回调体） LeonAnimatorDriver.SetCrouching(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="crouching">是否蹲伏中。</param>
    private void ApplyCrouchState(bool crouching)
    {
        inputState.isCrouching = crouching;
        if (crouching)
        {
            inputState.isSprinting = false;
        }

        // 赋值前保存的目标胶囊碰撞体。
global::UnityEngine.CapsuleCollider assignmentReceiver24 = capsuleCollider;
        if (crouching)
        {
            assignmentReceiver24.height = crouchColliderHeight;
        }
        else
        {
            assignmentReceiver24.height = standingColliderHeight;
        }

        // 赋值前保存的目标胶囊碰撞体。
global::UnityEngine.CapsuleCollider assignmentReceiver26 = capsuleCollider;
        if (crouching)
        {
            assignmentReceiver26.center = crouchingColliderCenter;
        }
        else
        {
            assignmentReceiver26.center = standingColliderCenter;
        }

        // 判空前缓存的动画驱动器。
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
    /// <param name="localCenter">局部中心。</param>
    /// <param name="localHeight">局部高度。</param>
    /// <param name="radiusPadding">半径留白。</param>
    /// <param name="top">顶部。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="bottom">底部。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="radius">半径。输出参数，由本入口写入并交给调用方。</param>
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
        // 缩放。
Vector3 scale = transform.lossyScale;
        // 轴。
Vector3 axis;
        // 高度缩放。
float heightScale;
        // 半径缩放。
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

        // 世界空间中心。
Vector3 worldCenter = transform.TransformPoint(localCenter);
        radius = capsuleCollider.radius * radiusScale + radiusPadding;
        // 一半线段。
float halfSegment = Mathf.Max(0f, localHeight * heightScale * 0.5f - radius);
        top = worldCenter + axis * halfSegment;
        bottom = worldCenter - axis * halfSegment;
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        ApplyDamage(damage, hitPoint, ResolveHitDirection(hitPoint));
    }

    /// <summary>
    /// 带攻击来源的近战伤害入口，用于选择左、中、右受击动画。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyDeadChainsawPulse()、EnemyCombatModule.TryApplyAttackHit(int)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.ResolveHitDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="damageSourcePosition">伤害来源位置。</param>
    /// <param name="hitPoint">命中点位置。</param>
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
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitDirection">命中方向。</param>
    internal void TakeEnvironmentalImpact(float damage, Vector3 source, Vector3 contact, bool knockdown)
    {
        if (Health == null || Health.IsDead)
        {
            return;
        }

        LeonHitDirection direction = ResolveHitDirection(source);
        if (!knockdown || !TryGetComponent(out LeonEnvironmentalFall fall) || !fall.CanStart)
        {
            ApplyDamage(damage, contact, direction);
            return;
        }

        float before = Health.CurrentHealth;
        ApplyDamage(damage, contact, direction, default, false);
        if (Health.IsDead || Health.CurrentHealth >= before)
        {
            return;
        }

        if (TrapAvatar != null)
        {
            TrapAvatar.ReleaseTrap();
        }

        Vector3 away = transform.position - source;
        if (!fall.TryStart(away) && combat != null)
        {
            combat.HandleHit(direction);
        }
    }

    private void ApplyDamage(float damage, Vector3 hitPoint, LeonHitDirection hitDirection, Vector3 hitNormal = default,
        bool useStandingReaction = true)
    {
        if (Health == null || Health.IsDead)
        {
            return;
        }

        if (qteCoordinator != null && qteCoordinator.BlocksOrdinaryDamage)
        {
            return;
        }

        if (IsQTEReleaseInvulnerable)
        {
            return;
        }

        // 伤害结算期间阻止生命事件直接修改动画层
        damageResolutionInProgress = true;
        float previousHealth = Health.CurrentHealth;
        Health.TakeDamage(damage);
        damageResolutionInProgress = false;
        if (Health.CurrentHealth < previousHealth && TrapAvatar != null)
        {
            TrapAvatar.ReactToDamage();
        }
        if (combat != null)
        {
            combat.ForceStopShoot();
        }

        PlayDamageFeedback(hitPoint, previousHealth - Health.CurrentHealth, hitNormal);
        if (Health.IsDead)
        {
            // 死亡优先级最高：清除待处理受伤切换，死亡动画完整覆盖
            // 判空前缓存的动画驱动器。
global::LeonAnimatorDriver receiverValue29 = // 死亡优先级最高：清除待处理受伤切换，死亡动画完整覆盖
 animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue29, null))
            {
                receiverValue29.ClearPendingInjuredTransition();
            }

            BeginDeathSequence();
        }
        else if (!useStandingReaction)
        {
            ForceExitCrouch();
        }
        else if (windowTraversal != null && windowTraversal.IsTraversing)
        {
            // 翻越中的延迟受击：保留待处理受伤目标，等待延迟受击的恢复阶段
            // 判空前缓存的动画驱动器。
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
            // 命中是否已开始。
bool hitStarted = combat != null && combat.HandleHit(hitDirection);
            if (hitStarted || IsHitAnimationActive())
            {
                // 受击成功启动（或已有受击动画在播）：只更新待处理目标
                // 判空前缓存的动画驱动器。
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
                // 判空前缓存的动画驱动器。
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
    /// <param name="hitDirection">命中方向。</param>
    public void HandleDeferredTraversalHit(LeonHitDirection hitDirection)
    {
        if (currentState == LeonState.Dead)
        {
            return;
        }

        // 命中是否已开始。
bool hitStarted = combat != null && combat.HandleHit(hitDirection);
        if (hitStarted || IsHitAnimationActive())
        {
            // 判空前缓存的动画驱动器。
global::LeonAnimatorDriver receiverValue33 = animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue33, null))
            {
                receiverValue33.RequestInjuredState(IsInjured, true);
            }
        }
        else
        {
            // 判空前缓存的动画驱动器。
global::LeonAnimatorDriver receiverValue34 = animDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                receiverValue34.RequestInjuredState(IsInjured, false);
            }
        }
    }

    /// <summary>ResolveHitDirection 的业务处理入口；本方法直接依赖 Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.TakeDamage(float, Vector3)、LeonController.TakeDamageFrom(float, Vector3, Vector3)；下游（含内部回调体） Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damageSourcePosition">伤害来源位置。</param>
    private LeonHitDirection ResolveHitDirection(Vector3 damageSourcePosition)
    {
        // 局部来源。
Vector3 localSource = transform.InverseTransformPoint(damageSourcePosition);
        localSource.y = 0f;
        if (localSource.sqrMagnitude <= 0.0001f)
        {
            return LeonHitDirection.Center;
        }

        // 归一化水平。
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
    /// <param name="sessionId">会话编号。</param>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitNormal">命中表面法线。</param>
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
        // 判空前缓存的战斗。
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

        // 判空前缓存的敌人命中效果管理器的实例。
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
        // 判空前缓存的输入桥接。
global::LeonInputBridge receiverValue37 = inputBridge;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.LockForDeath();
        }

        // 判空前缓存的动画协调器。
global::LeonAnimationActionCoordinator receiverValue38 = animCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue38, null))
        {
            receiverValue38.StopCurrentHorizontalMotion();
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue39 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue39, null))
        {
            receiverValue39.HandleDeath();
        }

        // 判空前缓存的玩家音频。
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

        var motion = GetComponent<LeonActionMotionDriver>();
        if (motion != null && motion.CorpseFalling)
        {
            if (deathCompletionFallbackRoutine != null)
            {
                StopCoroutine(deathCompletionFallbackRoutine);
            }
            deathCompletionFallbackRoutine = StartCoroutine(WaitForCorpseLanding(motion));
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
    private IEnumerator WaitForCorpseLanding(LeonActionMotionDriver motion)
    {
        float elapsed = 0f;
        while (motion != null && motion.CorpseFalling && elapsed < 1.5f)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        deathCompletionFallbackRoutine = null;
        ReportDeathCompletion();
    }

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
    /// <param name="hitPoint">命中点位置。</param>
    private void PlayDamageFeedback(Vector3 hitPoint, float healthLost, Vector3 hitNormal = default, bool spawnGenericImpact = true)
    {
        if (playerAudio != null && Health != null && !Health.IsDead && healthLost > 0f)
        {
            playerAudio.PlayHurt(healthLost / Mathf.Max(1f, Health.MaxHealth));
        }

        // 判空前缓存的伤害系统的实例。
global::DamageSystem receiverValue43 = DamageSystem.Instance;
        if (spawnGenericImpact && receiverValue43 != null)
        {
            if (hitNormal.sqrMagnitude < .001f)
            {
                hitNormal = transform.forward;
            }

            receiverValue43.SpawnHitEffect(hitPoint, hitNormal, false);
        }

        // 判空前缓存的战斗战斗显示界面的实例。
global::CombatHUD receiverValue44 = CombatHUD.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.HitFlash();
        }

        // 判空前缓存的摄像机控制器。
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
        // 判空前缓存的战斗。
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
    /// <param name="preserveMovementInput">是否保留移动输入。</param>
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
            // 判空前缓存的移动行为。
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
    /// <param name="count">数量。</param>
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
    /// <param name="count">数量。</param>
    void IItemUseTarget.Healing(float count) => Heal(count);
    /// <summary>IItemUseTarget.IncreaseCompatibleReserveAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.AddReserveAmmo(WeaponType, int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.AddReserveAmmo(WeaponType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    /// <param name="count">数量。</param>
    void IItemUseTarget.IncreaseCompatibleReserveAmmo(WeaponType type, int count)
    {
        // 判空前缓存的管理当前装备与武器切换的组件。
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
        // 判空前缓存的管理当前装备与武器切换的组件。
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
        // 判空前缓存的管理当前装备与武器切换的组件。
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
    /// <param name="slot">槽位。</param>
    public void SwitchWeapon(int slot)
    {
        TrySwitchWeapon((WeaponType)slot);
    }

    /// <summary>TrySwitchWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.SwitchTo(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.SwitchWeapon(int)；下游（含内部回调体） LeonWeaponHandler.SwitchTo(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    public bool TrySwitchWeapon(WeaponType weaponType)
    {
        return weaponHandler != null && weaponHandler.SwitchTo(weaponType);
    }

    /// <summary>中文含义：是否正在射击。</summary>
    public bool IsShooting => currentState == LeonState.Shooting;
    /// <summary>是否正在瞄准。</summary>
    public bool IsAiming => combat != null && combat.IsAiming;
    /// <summary>是否小刀攻击生效。</summary>
    public bool IsKnifeAttackActive => combat != null && combat.IsKnifeAttackActive;
    /// <summary>是否忙碌。</summary>
    public bool IsBusy => currentState == LeonState.Shooting || currentState == LeonState.Reloading || currentState == LeonState.QTE || currentState == LeonState.MeleeAttacking || currentState == LeonState.Traversing;

    /// <summary>TriggerMeleeCameraPush 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、RE4CameraController.TriggerMeleePush()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.StartMeleeAction(FullBodyActionType, int)；下游（含内部回调体） object.ReferenceEquals(object, object)、RE4CameraController.TriggerMeleePush()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TriggerMeleeCameraPush()
    {
        // 判空前缓存的摄像机控制器。
global::RE4CameraController receiverValue54 = cameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue54, null))
        {
            receiverValue54.TriggerMeleePush();
        }
    }

    /// <summary>RegisterCameraController 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.BindPlayer(LeonController, Transform)、RE4CameraController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controllerReference">控制器引用。</param>
    /// <param name="cameraReference">摄像机引用。</param>
    /// <param name="aimTargetReference">瞄准目标引用。</param>
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
    /// <param name="controllerReference">控制器引用。</param>
    public void UnregisterCameraController(RE4CameraController controllerReference)
    {
        if (cameraController != controllerReference)
        {
            return;
        }

        ResetFreeView();
        cameraController = null;
    }

    /// <summary>摄像机控制器。</summary>
    public RE4CameraController CameraController => cameraController;

    internal void ApplyKnifeCameraHeading(float heading, float pitch)
    {
        if (combat != null && combat.knifeModule != null)
        {
            combat.knifeModule.ApplyCameraHeading(heading, pitch);
        }
    }

    internal bool IsHoldingKnifeGuard => combat != null && combat.knifeModule != null && combat.knifeModule.IsHoldingGuardPose;
    internal bool IsKnifeGuardAiming => combat != null && combat.knifeModule != null && combat.knifeModule.IsGuardAiming;
    internal bool IsKnifeThrustAttack
    {
        get
        {
            return combat != null && combat.knifeModule != null && combat.knifeModule.IsThrustAttack;
        }
    }
    // === 管线访问器 ===
    /// <summary>中文含义：动画驱动器。</summary>
    public LeonAnimatorDriver AnimDriver => animDriver;
    /// <summary>中文含义：动画协调器。</summary>
    public LeonAnimationActionCoordinator AnimCoordinator => animCoordinator;
    /// <summary>中文含义：动画绑定组件。</summary>
    public LeonAnimRig AnimRig => animRig;
    /// <summary>中文含义：配对动作协调器。</summary>
    public LeonQTECoordinator QTECoordinator => qteCoordinator;
    /// <summary>新动作管线是否有效。</summary>
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
