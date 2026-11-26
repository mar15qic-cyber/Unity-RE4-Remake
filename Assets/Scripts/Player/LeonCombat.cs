using UnityEngine;
using System.Collections;

/// <summary>玩家控制、动作与运动使用的 KnifeParryResult 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum KnifeParryResult
{
    /// <summary>没有发生招架。</summary>
    None = 0,
    /// <summary>普通招架。</summary>
    Normal = 1,
    /// <summary>完美招架。</summary>
    Perfect = 2
}

/// <summary>玩家控制、动作与运动组件 LeonCombat，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>玩家战斗输入和动作申请入口；枪械、小刀与配对沿已绑定武器、KnifeCombatModule 和协调器入口执行，不创建平行伤害判定。</remarks>
public class LeonCombat : MonoBehaviour, ICrosshairStateProvider
{
    /// <summary>小刀战斗模块。</summary>
    [Header("独立小刀战斗")]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀模块")]
    [UnityEngine.InspectorName("小刀模块")]
    public KnifeCombatModule knifeModule;
    /// <summary>是否希望进入或保持瞄准状态。</summary>
    private bool desiredAim;
    private bool aimWaitingForStand;
    private bool cqbrShotWaitingForAim;
    private bool pistolTriggerQueued;
    private double pistolTriggerQueuedAt;
    /// <summary>小刀招架火花预制件。</summary>
    private GameObject knifeParrySparkPrefab;
    /// <summary>里昂玩家控制器。</summary>
    private LeonController controller;
    /// <summary>管理当前装备与武器切换的组件。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>里昂动画控制器驱动器。</summary>
    private LeonAnimatorDriver driver;
    /// <summary>当前玩家动画动作协调器。</summary>
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>当前里昂动画绑定组件。</summary>
    private LeonAnimRig animRig;
    /// <summary>武器视觉表现控制器。</summary>
    private LeonWeaponVisualController weaponVisualController;
    /// <summary>手枪模型。</summary>
    [Header("武器模型")]
    public GameObject pistolModel;
    /// <summary>步枪模型。</summary>
    public GameObject rifleModel;
    /// <summary>小刀模型。</summary>
    public GameObject knifeModel;
    /// <summary>手枪视觉表现模型。</summary>
    [Header("武器视觉模型（骨骼下）")]
    public GameObject pistolVisualModel;
    /// <summary>步枪视觉表现模型。</summary>
    public GameObject rifleVisualModel;
    /// <summary>开火间隔。</summary>
    [Header("射击设置")]
    public float fireInterval = 0.5f;
    /// <summary>射击动画锁定时间。半自动武器射击后至少保持射击状态的时间，用来等待手部射击动作收尾。</summary>
    [Tooltip("半自动武器射击后至少保持射击状态的时间，用来等待手部射击动作收尾。")]
    [Min(0f)]
    public float shootAnimationLockTime = 0.9f;
    /// <summary>区分轻按与持续按住射击键的时间阈值。</summary>
    public float tapHoldThreshold = 0.2f;
    /// <summary>霰弹枪是否采用逐发装填。霰弹枪换弹时逐发补入弹仓，而不是动画完成时一次性填满。</summary>
    [Header("霰弹枪逐发装填")]
    [Tooltip("霰弹枪换弹时逐发补入弹仓，而不是动画完成时一次性填满。")]
    public bool shotgunShellByShellReload = true;
    /// <summary>霰弹枪开始逐发装填前的等待时间。进入换弹后第一发装入前的等待时间。</summary>
    [Tooltip("进入换弹后第一发装入前的等待时间。")]
    [Min(0f)]
    public float shotgunShellLoadInitialDelay = 0.45f;
    /// <summary>霰弹枪每装入一发弹药的间隔。每发霰弹之间的装填间隔。</summary>
    [Tooltip("每发霰弹之间的装填间隔。")]
    [Min(0.05f)]
    public float shotgunShellLoadInterval = 0.42f;
    /// <summary>完美招架判定时窗。</summary>
    [Header("小刀弹反")]
    [Range(0.03f, 0.25f)]
    public float perfectParryWindow = 0.11f;
    /// <summary>招架有效判定的持续时间。</summary>
    [Min(0.1f)]
    public float parryActiveDuration = 0.55f;
    /// <summary>每次小刀攻击消耗的耐久量。</summary>
    [Min(1)]
    public int knifeAttackDurabilityCost = 1;
    /// <summary>每次小刀招架消耗的耐久量。</summary>
    [Min(1)]
    public int knifeParryDurabilityCost = 4;
    /// <summary>普通招架命中后的顿帧持续时间。</summary>
    [Min(0f)]
    public float normalParryHitStop = 0.035f;
    /// <summary>完美招架命中后的顿帧持续时间。</summary>
    [Min(0f)]
    public float perfectParryHitStop = 0.065f;
    /// <summary>是否允许在没有招架提示时切换小刀模式。</summary>
    public bool allowKnifeToggleWithoutParryPrompt = true;
    /// <summary>是否允许在当前突刺中缓存下一次小刀攻击。为 true 时允许在快速突刺过程中预存下一次攻击；为 false 时本次突刺内的重复攻击输入不会排队。动作尚未退出时再次按下攻击，会缓存下一次突刺并在恢复帧立即衔接。</summary>
    [Header("小刀快速突刺")]
    [Tooltip("动作尚未退出时再次按下攻击，会缓存下一次突刺并在恢复帧立即衔接")]
    public bool allowBufferedKnifeAttack = true;
    /// <summary>最近一次按下射击键的时间。</summary>
    float shootPressTime;
    /// <summary>射击键是否仍按住。</summary>
    bool isShootHeld;
    /// <summary>开火协程。</summary>
    Coroutine fireRoutine;
    /// <summary>射击动作锁定解除的截止时间。</summary>
    private float shootLockedUntil;
    /// <summary>是否缓存了等待处理的射击松开输入。</summary>
    private bool shootReleaseQueued;
    /// <summary>是否正在瞄准。</summary>
    bool isAiming;
    /// <summary>瞄准动画图层权重。</summary>
    private float aimLayerWeight;
    /// <summary>目标瞄准动画图层权重。</summary>
    private float targetAimLayerWeight;
    /// <summary>瞄准图层混合速度。</summary>
    public float aimLayerBlendSpeed = 4f;
    // 管线状态
    /// <summary>当前换弹动作事务编号。</summary>
    private int currentReloadTxId = -1;
    private bool cqbrReloadStartedEmpty;
    private bool reloadFireRequested;
    private bool reloadFireHeld;
    private bool w870TriggerQueued;
    private double w870TriggerQueuedAt;
    private bool w870ReloadQueued;
    private double w870ReloadQueuedAt;
    private bool magazineReloadQueued;
    private double magazineReloadQueuedAt;
    private const double FirearmQueueLifetime = 0.26;
    private const double W870ReloadQueueLifetime = 0.85;
    private LeonInputBridge inputBridge;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>当前近战动作的事务编号。记录协调器批准的当前近战事务，用于拒绝其他事务的完成、命中和退出回调；没有有效事务时保留既有负值哨兵。</summary>
    private int currentMeleeTxId = -1;
    /// <summary>近战动作是否仍在执行。为 true 表示已进入的近战动作仍未结束；阻断冲突操作，并决定新的小刀攻击输入是否进入连击缓存。</summary>
    private bool meleeActionActive;
    /// <summary>是否有等待当前突刺退出后衔接的小刀攻击。当前快速突刺未退出时，再次攻击且允许连击缓存会置为 true；动作正常退出时用它决定是否衔接下一刀并清除；动作中断、组件停用或小刀损坏也会清除。</summary>
    private bool queuedKnifeAttack;
    /// <summary>切入小刀模式前装备的武器类型。</summary>
    private WeaponType weaponBeforeKnife = WeaponType.Pistol;
    /// <summary>是否缓存了尚未执行的小刀模式切换请求。近战或切枪尚未结束时按切换键会翻转此标记；Update 在动作允许后消费请求并切换模式，停用或强制退出小刀时清除。</summary>
    private bool queuedKnifeToggle;
    /// <summary>当前正在执行的近战动作类型。</summary>
    private FullBodyActionType currentMeleeAction = FullBodyActionType.None;
    /// <summary>待提交近战命中的木箱目标。</summary>
    private DestructibleWoodenBox pendingWoodenBoxMeleeTarget;
    private VillagerQTEParticipant pendingEnemyMeleeTarget;
    private Transform boxStrikeStart;
    private Transform boxStrikeEnd;

    private const float EnemyKickReach = 0.94f;

    internal bool IsPositionedMelee => meleeActionActive
        && (MeleeActionKinds.IsWoodenBox(currentMeleeAction) || MeleeActionKinds.IsEnemyKick(currentMeleeAction));

    internal bool TryGetPositionedMeleeMotion(out Quaternion rotation, out Vector3 velocity)
    {
        rotation = transform.rotation;
        velocity = Vector3.zero;
        bool hasProgress = driver.TryReadMeleeProgress(currentMeleeAction, out float progress);
        if (pendingWoodenBoxMeleeTarget != null)
        {
            Vector3 toBox = Vector3.ProjectOnPlane(pendingWoodenBoxMeleeTarget.GetMeleeKickPoint(transform) - transform.position, Vector3.up);
            if (toBox.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            rotation = Quaternion.LookRotation(toBox);
            if (hasProgress)
            {
                BoxStrikeWindow(currentMeleeAction, out float first, out _, out _);
                if (progress < first)
                {
                    float reach = 0.42f;
                    if (currentMeleeAction == FullBodyActionType.WoodenBoxStomp)
                    {
                        reach = 0.49f;
                    }
                    else if (currentMeleeAction == FullBodyActionType.WoodenBoxKick)
                    {
                        reach = 0.59f;
                    }
                    // 只在出手前靠近，接触与收招阶段保持站位。
                    float speed = Mathf.Clamp((toBox.magnitude - reach) / Time.fixedDeltaTime, 0f, 1.35f);
                    velocity = toBox.normalized * speed;
                }
            }
            return true;
        }

        if (pendingEnemyMeleeTarget == null || !hasProgress || progress >= MeleeActionKinds.EnemyKickContactTime(currentMeleeAction))
        {
            return false;
        }

        Vector3 toEnemy = Vector3.ProjectOnPlane(pendingEnemyMeleeTarget.transform.position - transform.position, Vector3.up);
        if (toEnemy.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        // 出腿相对角色朝向有侧向偏移，助跑末尾才逐步对齐脚的轨迹。
        float sourceFrame = progress * MeleeActionKinds.EnemyKickFrames(currentMeleeAction);
        float facingWeight = Mathf.InverseLerp(42f, 54f, sourceFrame);
        float yawOffset = 10f;
        if (currentMeleeAction == FullBodyActionType.QTESpinningHookKick)
        {
            yawOffset = -27f;
        }
        rotation = Quaternion.LookRotation(toEnemy) * Quaternion.Euler(0f, yawOffset * facingWeight, 0f);
        float approachSpeed = Mathf.Clamp((toEnemy.magnitude - EnemyKickReach) / Time.fixedDeltaTime, 0f, 3.4f);
        velocity = toEnemy.normalized * approachSpeed;
        return true;
    }

    private void LateUpdate()
    {
        if (!meleeActionActive || pendingWoodenBoxMeleeTarget == null
            || !driver.TryReadMeleeProgress(currentMeleeAction, out float progress))
        {
            return;
        }

        BoxStrikeWindow(currentMeleeAction, out float first, out float last, out float radius);
        if (progress < first)
        {
            return;
        }

        if (progress <= last && boxStrikeStart != null && boxStrikeEnd != null &&
            pendingWoodenBoxMeleeTarget.TryBreakAtContact(transform, boxStrikeStart.position, boxStrikeEnd.position, radius))
        {
            pendingWoodenBoxMeleeTarget = null;
            return;
        }

        // 即使动画更新跨过了接触帧，也只对已对齐且仍在触及范围内的箱体结算一次。
        if (progress >= last)
        {
            bool broken = pendingWoodenBoxMeleeTarget.TryBreakAtAlignedContact(transform, currentMeleeAction);
            pendingWoodenBoxMeleeTarget = null;
            if (!broken)
            {
                // 接触失败不能产生成功音效或掉落，也不能在后续帧重试旧目标。
                AudioManager.PlayInterface(AudioEventNames.UIError);
            }
        }
    }

    private static void BoxStrikeWindow(FullBodyActionType action, out float first, out float last, out float radius)
    {
        radius = 0.075f;
        switch (action)
        {
            case FullBodyActionType.WoodenBoxStomp:
                first = 25f / 135f;
                last = 33f / 135f;
                break;
            case FullBodyActionType.WoodenBoxElbow:
                first = 27f / 120f;
                last = 35f / 120f;
                radius = 0.11f;
                break;
            case FullBodyActionType.WoodenBoxPistolElbow:
                first = 29f / 137f;
                last = 37f / 137f;
                radius = 0.11f;
                break;
            default:
                first = 25f / 140f;
                last = 31f / 140f;
                break;
        }
    }

    private void BindBoxStrikeBones(FullBodyActionType action)
    {
        HumanBodyBones start = HumanBodyBones.RightFoot;
        HumanBodyBones end = HumanBodyBones.RightToes;
        switch (action)
        {
            case FullBodyActionType.WoodenBoxElbow:
                start = HumanBodyBones.LeftLowerArm;
                end = HumanBodyBones.LeftHand;
                break;
            case FullBodyActionType.WoodenBoxPistolElbow:
                start = HumanBodyBones.RightLowerArm;
                end = HumanBodyBones.RightHand;
                break;
        }
        boxStrikeStart = AnimatorBoneBindings.Resolve(driver.Animator, start);
        boxStrikeEnd = AnimatorBoneBindings.Resolve(driver.Animator, end);
    }
    private Coroutine shotgunShellReloadRoutine;
    /// <summary>霰弹枪逐发装填使用的换弹事务编号。</summary>
    private int shotgunShellReloadTxId = -1;
    /// <summary>霰弹枪换弹动画是否已完成。</summary>
    private bool shotgunShellReloadAnimationCompleted;
    /// <summary>霰弹枪逐发补弹是否已完成。</summary>
    private bool shotgunShellReloadLoadsCompleted;
    /// <summary>招架机会来源。</summary>
    private VillagerController parryOpportunitySource;
    /// <summary>是否处于可招架的机会时窗。</summary>
    private bool parryOpportunityOpen;
    /// <summary>是否已准备接收本次招架输入。</summary>
    private bool parryInputArmed;
    /// <summary>最近一次按下招架键的时间。</summary>
    private float parryPressedTime;
    /// <summary>招架命中顿帧协程。</summary>
    private Coroutine parryHitStopRoutine;
    /// <summary>是否保持防守输入。由防守输入更新；与当前招架和小刀动作共同决定保持防守、恢复枪械姿态等后续处理。</summary>
    private bool guardHeld;
    /// <summary>开始保持防守的时间。</summary>
    private float guardBeginTime;
    /// <summary>瞄准状态变化时发出的通知。事件通信成员 OnAimStateChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<bool> OnAimStateChanged;
    /// <summary>换弹状态变化时发出的通知。事件通信成员 OnReloadingChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<bool> OnReloadingChanged;
    /// <summary>换弹动作完全退出时发出的通知。事件通信成员 OnReloadFullyExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action OnReloadFullyExited;
    /// <summary>配对体术命中时发出的通知。事件通信成员 OnQTEBodyTechniqueHit，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<int, FullBodyActionType> OnQTEBodyTechniqueHit;
    /// <summary>由 Unity 在组件启动时调用；缓存战斗组件、加载招架特效并绑定动作协调器事件。</summary>
    void Start()
    {
        if (knifeModule == null)
        {
            knifeModule = GetComponent<KnifeCombatModule>();
        }

        controller = GetComponent<LeonController>();
        inputBridge = GetComponent<LeonInputBridge>();
        knifeParrySparkPrefab = Resources.Load<GameObject>("Combat/KnifeParrySparks");
        weaponHandler = GetComponent<LeonWeaponHandler>();
        driver = GetComponent<LeonAnimatorDriver>();
        coordinator = GetComponent<LeonAnimationActionCoordinator>();
        animRig = GetComponent<LeonAnimRig>();
        weaponVisualController = GetComponent<LeonWeaponVisualController>();
        aimLayerWeight = 0f;
        targetAimLayerWeight = 0f;
        BindCoordinatorEvents();
    }

    /// <summary>由 Unity 在组件启用时调用，交由 BindCoordinatorEvents 绑定动作协调器事件。</summary>
    void OnEnable()
    {
        BindCoordinatorEvents();
    }

    /// <summary>由 Unity 在组件停用时调用；清理战斗状态、恢复枪械姿态并解除协调器事件绑定。</summary>
    void OnDisable()
    {
        guardHeld = false;
        StopParryHitStop();
        RestoreFirearmPose();
        queuedKnifeToggle = false;
        queuedKnifeAttack = false;
        magazineReloadQueued = false;
        ClearParryState(true);
        StopShotgunShellReload(false);
        UnbindCoordinatorEvents();
    }

    /// <summary>由 Start 或 OnEnable 调用，将动作协调器事件绑定到本组件的战斗回调。</summary>
    private void BindCoordinatorEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnReloadAnimationCompleted += OnReloadAnimCompleted;
        coordinator.OnReloadExited += OnReloadAnimExited;
        coordinator.OnHitAnimationCompleted += OnHitAnimCompleted;
        coordinator.OnWeaponActionInterrupted += OnWeaponActionInterrupted;
        coordinator.OnMeleeActionCompleted += OnMeleeActionCompleted;
        coordinator.OnMeleeActionExited += OnMeleeActionExited;
        coordinator.OnMeleeActionInterrupted += OnMeleeActionInterrupted;
        coordinator.OnMeleeHitWindow += OnMeleeHitWindow;
        eventsBound = true;
    }

    /// <summary>由 OnDisable 调用，解除本组件对动作协调器事件的订阅。</summary>
    private void UnbindCoordinatorEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (coordinator != null)
        {
            coordinator.OnReloadAnimationCompleted -= OnReloadAnimCompleted;
            coordinator.OnReloadExited -= OnReloadAnimExited;
            coordinator.OnHitAnimationCompleted -= OnHitAnimCompleted;
            coordinator.OnWeaponActionInterrupted -= OnWeaponActionInterrupted;
            coordinator.OnMeleeActionCompleted -= OnMeleeActionCompleted;
            coordinator.OnMeleeActionExited -= OnMeleeActionExited;
            coordinator.OnMeleeActionInterrupted -= OnMeleeActionInterrupted;
            coordinator.OnMeleeHitWindow -= OnMeleeHitWindow;
        }

        eventsBound = false;
    }

    // ==================== 瞄准 ====================
    /// <summary>HandleAim 的业务处理入口；本方法直接依赖 Action&lt;bool&gt;.Invoke(bool)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.CanAct()、LeonController.ChangeState(LeonState)、LeonController.SynchronizeEnterAimAngle() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.EndTemporaryKnife(bool)、LeonInputBridge.LockForDeath()、LeonInputBridge.OnAim(InputAction.CallbackContext)、LeonInputBridge.OnDisable()、LeonInputBridge.SyncAimAfterWeaponWheel() 等共 7 处；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.CanAct()、LeonController.ChangeState(LeonState)、LeonController.SynchronizeEnterAimAngle() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="aim">是否瞄准。</param>
    public void HandleAim(bool aim)
    {
        desiredAim = aim;
        if (!aim)
        {
            aimWaitingForStand = false;
            cqbrShotWaitingForAim = false;
            pistolTriggerQueued = false;
            w870TriggerQueued = false;
            ClearReloadFireRequest();
        }
        if (knifeModule != null && knifeModule.Active)
        {
            return;
        }

        if (aim && temporaryKnifeEquipped && IsKnifeMode)
        {
            // 主动瞄准通过收刀和拔枪事务接回枪械。
            knifeReturnAt = Time.timeAsDouble;
            return;
        }

        if (aim && (IsKnifeMode || meleeActionActive || guardHeld))
        {
            return;
        }

        if (aim && !CanAct())
        {
            return;
        }

        if (aim)
        {
            if (controller.IsCrouching)
            {
                if (IsReloading && driver != null && driver.UsesNativeReloadLayerFade)
                {
                    aimWaitingForStand = true;
                    return;
                }
                controller.TrySetCrouching(false);
            }
            aimWaitingForStand = controller.IsFirearmStanceBlocked;
            if (aimWaitingForStand)
            {
                return;
            }
        }

        if (aim)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue1 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.TakeOverFreeViewAim();
            }
        }

        if (aim && !isAiming)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue2 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.SynchronizeEnterAimAngle();
            }

            if (driver == null || !driver.UsesNativePistolAim)
            {
                aimLayerWeight = 1f;
            }
            // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue3 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.SetAimLayerWeight(aimLayerWeight);
            }
        }

        isAiming = aim;
        if (aim)
        {
            targetAimLayerWeight = 1f;
        }
        else
        {
            targetAimLayerWeight = 0f;
        }

        if (controller != null && controller.NewPipelineValid)
        {
            if (driver != null)
            {
                driver.SetAimState(aim);
            }

            if (animRig != null)
            {
                // 调用前缓存的当前里昂动画绑定组件。
global::LeonAnimRig callReceiver6 = animRig;
                // 分支选出的反向动力学模式的瞄准。
global::IKMode selectedValue5;
                if (aim)
                {
                    selectedValue5 = IKMode.Aim;
                }
                else
                {
                    selectedValue5 = IKMode.None;
                }

                callReceiver6.SetIKMode(selectedValue5);
            }
        }

        if (aim && (!IsReloading || driver == null
            || !driver.UsesNativeReloadLayerFade && !driver.TryReadNativeCqbrReload(out _)
                && !driver.IsNativeW870ReloadPlaying()))
        {
            controller.ChangeState(LeonState.Aiming);
        }
        else if (controller.currentState == LeonState.Aiming)
        {
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver8 = controller;
            // 分支选出的里昂状态的行走中。
global::LeonState selectedValue7;
            if (controller.MoveInput.magnitude > 0.1f)
            {
                selectedValue7 = LeonState.Walking;
            }
            else
            {
                selectedValue7 = LeonState.Idle;
            }

            callReceiver8.ChangeState(selectedValue7);
        }

        // 判空前缓存的瞄准状态变化时发出的通知。
global::System.Action<bool> receiverValue9 = OnAimStateChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.Invoke(aim);
        }
    }

    /// <summary>是否正在瞄准。</summary>
    public bool IsAiming => isAiming;
    internal bool HasAimIntent => isAiming || aimWaitingForStand;
    /// <summary>是否正在换弹。</summary>
    public bool IsReloading => controller != null && controller.currentState == LeonState.Reloading;
    /// <summary>是否处于小刀模式。</summary>
    public bool IsKnifeMode => controller != null && controller.currentWeapon == WeaponType.Knife;
    internal bool IsHoldingTemporaryKnife => temporaryKnifeEquipped && IsKnifeMode;
    internal WeaponType TemporaryKnifeReturnWeapon => knifeReturnWeapon;

    /// <summary>是否小刀攻击生效。</summary>
    public bool IsKnifeAttackActive
    {
        get
        {
            if (knifeModule != null)
            {
                return knifeModule.IsAttack;
            }
            else
            {
                return meleeActionActive && currentMeleeAction == FullBodyActionType.KnifeQuickAttack;
            }
        }
    }

    /// <summary>是否招架提示可见。</summary>
    public bool IsParryPromptVisible
    {
        get
        {
            if (knifeModule != null)
            {
                return knifeModule.ShowParryPrompt;
            }
            else
            {
                return parryOpportunityOpen && parryOpportunitySource != null && !parryInputArmed && CanBeginParryAction;
            }
        }
    }

    /// <summary>是否招架中。</summary>
    public bool IsParrying
    {
        get
        {
            if (knifeModule != null)
            {
                return knifeModule.CanParryNow;
            }

            return parryInputArmed || currentMeleeAction == FullBodyActionType.KnifeParry;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.TriggerFire()、LeonCombat.EndShoot()、LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.HandleKnifeHold(bool)、LeonCombat.IsFireBlockedByWeaponAction()、LeonCombat.ProcessQueuedKnifeToggle() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (controller == null)
        {
            return;
        }

        if (!controller.NewPipelineValid)
        {
            return;
        }

        UpdateTemporaryKnifeCarry();

        if (aimWaitingForStand && desiredAim && Time.timeScale > 0f)
        {
            HandleAim(true);
        }

        if (guardHeld && weaponHandler != null && !weaponHandler.HasUsableKnife())
        {
            HandleKnifeHold(false);
        }

        // 瞄准层权重通过Driver设置
        if (!Mathf.Approximately(aimLayerWeight, targetAimLayerWeight))
        {
            aimLayerWeight = Mathf.Lerp(aimLayerWeight, targetAimLayerWeight, Time.deltaTime * aimLayerBlendSpeed);
            if (Mathf.Abs(aimLayerWeight - targetAimLayerWeight) < 0.01f)
            {
                aimLayerWeight = targetAimLayerWeight;
            }

            if (driver != null)
            {
                driver.SetAimLayerWeight(aimLayerWeight);
            }
        }

        UpdateShootStateLock();
        UpdateReloadFireRequest();
        if (controller.IsFirearmStanceBlocked || IsFireBlockedByWeaponAction())
        {
            if (controller.currentState == LeonState.Shooting)
            {
                EndShoot();
            }

            ProcessQueuedKnifeToggle();
            return;
        }

        // Auto连发
        if (cqbrShotWaitingForAim && isAiming && driver != null && !driver.NativeCqbrBlocksFire)
        {
            cqbrShotWaitingForAim = false;
            ExecuteFirearmAttack();
        }

        UpdatePistolQueuedInput();
        UpdateW870QueuedInput();
        UpdateMagazineReloadQueuedInput();

        if (isShootHeld && controller.currentState == LeonState.Shooting
            && (driver == null || !driver.NativeCqbrBlocksFire))
        {
            // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue12 = weaponHandler;
            // 当前武器组件。
global::WeaponBase weapon;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                weapon = receiverValue12.GetCurrentWeapon();
            }
            else
            {
                weapon = default;
            }

            if (weapon != null && weapon.data != null && weapon.data.fireMode == FireMode.Auto && weapon.CanFire())
            {
                // 是否已开火。
bool fired = controller.FireWeapon();
                if (fired && driver != null)
                {
                    shootLockedUntil = Time.time + GetShootLockDuration(weapon);
                    shootReleaseQueued = false;
                    driver.TriggerFire(controller.GetCurrentAmmo() == 0);
                }
            }
        }

        ProcessQueuedKnifeToggle();
    }

    // ==================== 射击 ====================
    /// <summary>HandleShoot 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackInput(double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） LeonCombat.CommitAttackInput(double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleShoot()
    {
        CommitAttackInput(Time.timeAsDouble);
    }

    /// <summary>CommitAttackInput 的业务处理入口；本方法直接依赖 KnifeCombatModule.CommitAttack(double)、LeonCombat.ExecuteFirearmAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleShoot()、LeonInputBridge.OnShoot(InputAction.CallbackContext)；下游（含内部回调体） KnifeCombatModule.CommitAttack(double)、LeonCombat.ExecuteFirearmAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    public void CommitAttackInput(double time)
    {
        TryCommitAttackInput(time);
    }

    internal bool TryCommitAttackInput(double time)
    {
        if (knifeModule != null && knifeModule.Ready)
        {
            knifeModule.CommitAttack(time);
            return true;
        }

        return TryExecuteFirearmAttack();
    }

    /// <summary>ExecuteFirearmAttack 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.TriggerFire()、LeonCombat.CanAct()、LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.IsFireBlockedByWeaponAction()、LeonController.ChangeState(LeonState)、LeonController.FireWeapon()、LeonController.GetCurrentAmmo() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()、LeonCombat.CommitAttackInput(double)；下游（含内部回调体） LeonAnimatorDriver.TriggerFire()、LeonCombat.CanAct()、LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.IsFireBlockedByWeaponAction()、LeonController.ChangeState(LeonState)、LeonController.FireWeapon()、LeonController.GetCurrentAmmo() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ExecuteFirearmAttack()
    {
        TryExecuteFirearmAttack();
    }

    private bool TryExecuteFirearmAttack()
    {
        if (controller == null || controller.IsFirearmStanceBlocked || IsFireBlockedByWeaponAction())
        {
            return false;
        }

        if (!isAiming || controller.currentWeapon == WeaponType.Knife)
        {
            // 未举枪时点击开火不留在预输入通道中等待，除非存在待恢复的举枪意图。
            if (inputBridge != null && inputBridge.HasAimIntentForReplay)
            {
                return false;
            }
            return true;
        }

        if (!CanAct())
        {
            return false;
        }

        if (!isAiming && controller.currentState != LeonState.Aiming)
        {
            return true;
        }

        if (controller.currentState == LeonState.Reloading)
        {
            if (driver != null && (driver.UsesNativeReloadLayerFade || driver.TryReadNativeCqbrReload(out _)
                || driver.IsNativeW870ReloadPlaying()))
            {
                reloadFireRequested = true;
                reloadFireHeld = true;
                if (controller.currentWeapon == WeaponType.Shotgun)
                {
                    coordinator.RequestNativeW870ReloadStop();
                }
            }
            return true;
        }

        if (driver != null && driver.SupportsNativeW870Aim
            && (driver.NativeW870BlocksFire || controller.currentState == LeonState.Shooting))
        {
            w870TriggerQueued = true;
            w870TriggerQueuedAt = Time.timeAsDouble;
            w870ReloadQueued = false;
            return true;
        }

        if (driver != null && driver.UsesNativePistolAim
            && (driver.NativePistolBlocksFire || controller.currentState == LeonState.Shooting))
        {
            pistolTriggerQueued = true;
            pistolTriggerQueuedAt = Time.timeAsDouble;
            return true;
        }
        if (controller.currentState == LeonState.Shooting)
        {
            return true;
        }

        if (driver != null && driver.NativeCqbrBlocksFire)
        {
            cqbrShotWaitingForAim = true;
            return true;
        }
        if (controller.GetCurrentAmmo() <= 0)
        {
            VillageLeonDialogue.Report(controller, VillageSpeechCue.DryFire);
            if (driver != null && !driver.TryPlayNativePistolDryFire() && !driver.TryPlayNativeW870DryFire())
            {
                driver.TryPlayNativeCqbrDryFire();
            }
            // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue14 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                // 判空前缓存的取得当前武器的返回结果。
global::WeaponBase receiverValue15 = receiverValue14.GetCurrentWeapon();
                if (!global::System.Object.ReferenceEquals(receiverValue15, null))
                {
                    // 判空前缓存的接收者值15的武器音频引用。
global::WeaponAudio receiverValue16 = receiverValue15.WeaponAudioRef;
                    if (!global::System.Object.ReferenceEquals(receiverValue16, null))
                    {
                        receiverValue16.PlayDryFire();
                    }
                }
            }

            return true;
        }

        if (!controller.NewPipelineValid)
        {
            return true;
        }

        isShootHeld = true;
        controller.ChangeState(LeonState.Shooting);
        // 是否已开火。
bool fired = controller.FireWeapon();
        if (!fired)
        {
            isShootHeld = false;
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver18 = controller;
            // 分支选出的里昂状态的瞄准中。
global::LeonState selectedValue17;
            if (isAiming)
            {
                selectedValue17 = LeonState.Aiming;
            }
            else
            {
                selectedValue17 = LeonState.Idle;
            }

            callReceiver18.ChangeState(selectedValue17);
            return true;
        }

        if (driver != null)
        {
            driver.TriggerFire(controller.GetCurrentAmmo() == 0);
        }

        // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue19 = weaponHandler;
        // 当前武器组件。
global::WeaponBase weapon;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            weapon = receiverValue19.GetCurrentWeapon();
        }
        else
        {
            weapon = default;
        }

        shootLockedUntil = Time.time + GetShootLockDuration(weapon);
        shootReleaseQueued = false;
        return true;
    }

    /// <summary>HandleShootRelease 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackReleased(double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） LeonCombat.CommitAttackReleased(double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleShootRelease()
    {
        CommitAttackReleased(Time.timeAsDouble);
    }

    /// <summary>CommitAttackReleased 的业务处理入口；本方法直接依赖 KnifeCombatModule.CommitAttackReleased(double)、LeonCombat.ExecuteFirearmAttackReleased()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleShootRelease()、LeonInputBridge.OnShootReleased(InputAction.CallbackContext)；下游（含内部回调体） KnifeCombatModule.CommitAttackReleased(double)、LeonCombat.ExecuteFirearmAttackReleased()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    public void CommitAttackReleased(double time)
    {
        if (knifeModule != null && knifeModule.Ready)
        {
            knifeModule.CommitAttackReleased(time);
        }
        else
        {
            ExecuteFirearmAttackReleased();
        }
    }

    /// <summary>ExecuteFirearmAttackReleased 的业务处理入口；本方法直接依赖 LeonCombat.EndShoot()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()、LeonCombat.CommitAttackReleased(double)；下游（含内部回调体） LeonCombat.EndShoot()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ExecuteFirearmAttackReleased()
    {
        cqbrShotWaitingForAim = false;
        reloadFireHeld = false;
        isShootHeld = false;
        if (controller == null || controller.currentState != LeonState.Shooting)
        {
            return;
        }

        if (Time.time >= shootLockedUntil)
        {
            EndShoot();
            return;
        }

        shootReleaseQueued = true;
    }

    /// <summary>UpdateShootStateLock 的业务处理入口；本方法直接依赖 LeonCombat.EndShoot()、LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.Update()；下游（含内部回调体） LeonCombat.EndShoot()、LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateShootStateLock()
    {
        if (controller == null || controller.currentState != LeonState.Shooting)
        {
            return;
        }

        if (Time.time < shootLockedUntil)
        {
            return;
        }

        // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue21 = weaponHandler;
        // 当前武器组件。
global::WeaponBase weapon;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            weapon = receiverValue21.GetCurrentWeapon();
        }
        else
        {
            weapon = default;
        }

        // 是否自动。
bool isAuto = weapon != null && weapon.data != null && weapon.data.fireMode == FireMode.Auto;
        if (!isAuto || !isShootHeld || shootReleaseQueued)
        {
            EndShoot();
        }
    }

    /// <summary>GetShootLockDuration 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、WeaponBase.GetFireInterval()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()、LeonCombat.Update()；下游（含内部回调体） Mathf.Max(float, float)、WeaponBase.GetFireInterval()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器组件。</param>
    private float GetShootLockDuration(WeaponBase weapon)
    {
        // 锁定持续时间。
float lockDuration = Mathf.Max(0f, fireInterval);
        if (weapon != null && weapon.data != null)
        {
            lockDuration = Mathf.Max(lockDuration, weapon.GetFireInterval());
            bool nativePistol = weapon.data.weaponType == WeaponType.Pistol && driver != null && driver.UsesNativePistolAim;
            if (weapon.data.fireMode != FireMode.Auto && !nativePistol)
            {
                lockDuration = Mathf.Max(lockDuration, shootAnimationLockTime);
            }
        }
        else
        {
            lockDuration = Mathf.Max(lockDuration, shootAnimationLockTime);
        }

        return lockDuration;
    }

    /// <summary>EndShoot 的业务处理入口；本方法直接依赖 LeonController.ChangeState(LeonState)、MonoBehaviour.CancelInvoke(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttackReleased()、LeonCombat.Update()、LeonCombat.UpdateShootStateLock()；下游（含内部回调体） LeonController.ChangeState(LeonState)、MonoBehaviour.CancelInvoke(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void EndShoot()
    {
        shootLockedUntil = 0f;
        shootReleaseQueued = false;
        if (controller.currentState == LeonState.Shooting)
        {
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver25 = controller;
            // 分支选出的里昂状态的瞄准中。
global::LeonState selectedValue24;
            if (isAiming)
            {
                selectedValue24 = LeonState.Aiming;
            }
            else
            {
                // 分支选出的里昂状态的行走中。
global::LeonState selectedValue23;
                if (controller.MoveInput.magnitude > 0.1f)
                {
                    selectedValue23 = LeonState.Walking;
                }
                else
                {
                    selectedValue23 = LeonState.Idle;
                }

                selectedValue24 = (selectedValue23);
            }

            callReceiver25.ChangeState(selectedValue24);
        }

        NotifyInputWindowOpened();
    }

    /// <summary>ForceStopShoot 的业务处理入口；本方法直接依赖 MonoBehaviour.CancelInvoke(string)、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.BeginTemporaryKnife()、LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleDeath()、LeonCombat.HandleHit(LeonHitDirection)、LeonCombat.HandleKnifeAttack() 等共 14 处；下游（含内部回调体） MonoBehaviour.CancelInvoke(string)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceStopShoot()
    {
        cqbrShotWaitingForAim = false;
        pistolTriggerQueued = false;
        w870TriggerQueued = false;
        w870ReloadQueued = false;
        magazineReloadQueued = false;
        ClearReloadFireRequest();
        isShootHeld = false;
        shootLockedUntil = 0f;
        shootReleaseQueued = false;
        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

    }

    /// <summary>InterruptForQTE 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.PrepareForQTE()、LeonInputBridge.EnterQTEInput(int)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InterruptForQTE()
    {
        // 判空前缓存的小刀战斗模块。
global::KnifeCombatModule receiverValue26 = knifeModule;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            receiverValue26.Interrupt(false);
        }

        ClearParryState(true);
        ForceStopShoot();
        ForceAimOff();
        ForceExitKnifeMode();
    }

    // ==================== 换弹 ====================
    /// <summary>HandleReload 的业务处理入口；本方法直接依赖 Action&lt;bool&gt;.Invoke(bool)、LeonAnimationActionCoordinator.CreateActionRequest(string, ActionKind)、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)、LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.CanAct()、LeonController.CanReload()、LeonController.ChangeState(LeonState) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnReload(InputAction.CallbackContext)；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、LeonAnimationActionCoordinator.CreateActionRequest(string, ActionKind)、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)、LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.CanAct()、LeonController.CanReload()、LeonController.ChangeState(LeonState) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleReload()
    {
        TryHandleReload();
    }

    internal bool TryHandleReload()
    {
        if (knifeModule != null && knifeModule.Active)
        {
            return false;
        }

        if (controller.currentWeapon == WeaponType.Knife)
        {
            return true;
        }

        if (meleeActionActive || guardHeld || !CanAct() || IsFireBlockedByWeaponAction())
        {
            return false;
        }

        if (controller.currentState == LeonState.Reloading)
        {
            return true;
        }

        if (controller.currentState == LeonState.Shooting || (driver != null && driver.NativeW870FireCycleActive))
        {
            if (driver != null && driver.SupportsNativeW870Aim && controller.CanReload())
            {
                w870ReloadQueued = true;
                w870ReloadQueuedAt = Time.timeAsDouble;
                w870TriggerQueued = false;
                return true;
            }

            if (controller.CanReload())
            {
                magazineReloadQueued = true;
                magazineReloadQueuedAt = Time.timeAsDouble;
                return true;
            }

            return true;
        }

        if (!controller.CanReload() || !controller.NewPipelineValid)
        {
            return true;
        }

        // 是否成功。
bool ok = coordinator.RequestWeaponAction(coordinator.CreateActionRequest("玩家战斗入口", GameArchitecture.ActionKind.Reload), controller.currentWeapon).HasAccept;
        if (!ok)
        {
            return false;
        }

        magazineReloadQueued = false;
        currentReloadTxId = coordinator.CurrentWeaponTransactionId;
        pistolTriggerQueued = false;
        cqbrReloadStartedEmpty = controller.GetCurrentAmmo() == 0;
        controller.ChangeState(LeonState.Reloading);
        controller.StartReload();
        BeginShotgunShellReloadIfNeeded(currentReloadTxId);
        // 判空前缓存的换弹状态变化时发出的通知。
global::System.Action<bool> receiverValue27 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.Invoke(true);
        }

        return true;
    }

    private void UpdateReloadFireRequest()
    {
        if (!reloadFireRequested || !isAiming || driver == null
            || controller.currentWeapon is not (WeaponType.Pistol or WeaponType.Rifle or WeaponType.Shotgun))
        {
            return;
        }

        if (IsReloading)
        {
            if (controller.currentWeapon == WeaponType.Shotgun)
            {
                return;
            }
            if (controller.GetCurrentAmmo() == 0 || !CanInterruptMagazineReloadForFire())
            {
                return;
            }

            bool held = reloadFireHeld;
            if (coordinator.InterruptNativeMagazineReloadForFire())
            {
                reloadFireRequested = true;
                reloadFireHeld = held;
                RestoreStateAfterWeaponAction();
            }
            return;
        }

        if (controller.currentState != LeonState.Aiming || controller.IsFirearmStanceBlocked
            || IsFireBlockedByWeaponAction() || driver.NativeCqbrBlocksFire || driver.NativeW870BlocksFire || driver.NativeW870ReloadActive
            || driver.NativePistolReloadActive || driver.NativePistolBlocksFire
            || driver.IsNativeW870ReloadPlaying() || driver.WeaponActionLayerWeight > 0.05f)
        {
            return;
        }

        bool keepFiring = reloadFireHeld;
        ClearReloadFireRequest();
        ExecuteFirearmAttack();
        if (!keepFiring)
        {
            ExecuteFirearmAttackReleased();
        }
    }

    private bool CanInterruptMagazineReloadForFire()
    {
        if (controller.currentWeapon == WeaponType.Pistol)
        {
            if (!driver.TryReadNativePistolReload(out float pistolProgress))
            {
                return false;
            }
            float frame = pistolProgress * LeonAnimatorDriver.PistolReloadFrames;
            if (driver.NativePistolEmptyReload)
            {
                return frame >= LeonAnimatorDriver.PistolSlideReleaseFrame;
            }
            return frame < LeonAnimatorDriver.PistolTacticalMagazineDetachFrame
                || frame >= LeonAnimatorDriver.PistolMagazineCommitFrame;
        }

        if (!driver.TryReadNativeCqbrReload(out float progress))
        {
            return false;
        }
        // 弹匣开始松脱后先走到插匣。空仓还要等枪机完全复位。
        float ready = LeonAnimatorDriver.NativeCqbrMagazineCommitTime;
        if (cqbrReloadStartedEmpty)
        {
            ready = 196f / LeonAnimatorDriver.NativeCqbrReloadFrames;
        }
        return progress < 26f / LeonAnimatorDriver.NativeCqbrReloadFrames || progress >= ready;
    }

    private void ClearReloadFireRequest()
    {
        reloadFireRequested = false;
        reloadFireHeld = false;
    }

    private void UpdatePistolQueuedInput()
    {
        if (!pistolTriggerQueued)
        {
            return;
        }
        if (Time.timeAsDouble - pistolTriggerQueuedAt > FirearmQueueLifetime)
        {
            pistolTriggerQueued = false;
            return;
        }
        if (driver == null || !driver.UsesNativePistolAim || !isAiming || !CanAct())
        {
            pistolTriggerQueued = false;
            return;
        }
        if (driver.NativePistolBlocksFire || controller.currentState == LeonState.Shooting || controller.currentState == LeonState.Reloading)
        {
            return;
        }

        pistolTriggerQueued = false;
        ExecuteFirearmAttack();
        ExecuteFirearmAttackReleased();
    }

    private void UpdateW870QueuedInput()
    {
        if (!w870TriggerQueued && !w870ReloadQueued)
        {
            return;
        }
        double now = Time.timeAsDouble;
        if (w870TriggerQueued && now - w870TriggerQueuedAt > FirearmQueueLifetime)
        {
            w870TriggerQueued = false;
        }
        if (w870ReloadQueued && now - w870ReloadQueuedAt > W870ReloadQueueLifetime)
        {
            w870ReloadQueued = false;
        }
        if (!w870TriggerQueued && !w870ReloadQueued)
        {
            return;
        }
        if (driver == null || !driver.SupportsNativeW870Aim || !CanAct())
        {
            w870TriggerQueued = false;
            w870ReloadQueued = false;
            return;
        }
        if (IsReloading || controller.currentState == LeonState.Shooting || driver.NativeW870FireCycleActive)
        {
            return;
        }
        if (w870ReloadQueued)
        {
            w870ReloadQueued = false;
            TryHandleReload();
            return;
        }
        if (!isAiming || driver.NativeW870BlocksFire || driver.WeaponActionLayerWeight > 0.05f)
        {
            return;
        }
        var weapon = weaponHandler.GetCurrentWeapon();
        if (weapon == null || (weapon.currentAmmo > 0 && !weapon.CanFire()))
        {
            return;
        }
        w870TriggerQueued = false;
        ExecuteFirearmAttack();
        ExecuteFirearmAttackReleased();
    }

    private void UpdateMagazineReloadQueuedInput()
    {
        if (!magazineReloadQueued)
        {
            return;
        }

        if (Time.timeAsDouble - magazineReloadQueuedAt > FirearmQueueLifetime)
        {
            magazineReloadQueued = false;
            return;
        }

        if (controller == null || !CanAct() || IsFireBlockedByWeaponAction())
        {
            return;
        }

        if (controller.currentState == LeonState.Shooting || controller.currentState == LeonState.Reloading)
        {
            return;
        }

        magazineReloadQueued = false;
        TryHandleReload();
    }

    /// <summary>OnReloadAnimCompleted 的业务处理入口；本方法直接依赖 Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CompleteReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CompleteReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnReloadAnimCompleted(AnimationActionTransaction tx)
    {
        if (!controller.NewPipelineValid)
        {
            return;
        }

        if (tx.transactionId != currentReloadTxId)
        {
            return;
        }

        if (tx.weaponActionType != WeaponActionType.Reload)
        {
            return;
        }

        // 在 Weapon Action 图层权重撤掉之前，先恢复瞄准表现
        if (isAiming && driver != null)
        {
            driver.SetAimState(true);
            aimLayerWeight = targetAimLayerWeight;
            driver.SetAimLayerWeight(aimLayerWeight);
        }

        if (IsShotgunReloadTransaction(tx))
        {
            shotgunShellReloadAnimationCompleted = true;
            TryCompleteShotgunShellReload(tx.transactionId);
            return;
        }

        if (tx.sourceWeaponType == WeaponType.Shotgun && driver != null && driver.NativeW870ReloadActive)
        {
            weaponHandler.CancelReload(WeaponType.Shotgun);
        }
        else
        {
            weaponHandler.CompleteReload(tx.transactionId);
        }
        if (coordinator != null)
        {
            // 是否成功。
bool ok = coordinator.CompleteReloadAfterBusinessCommit(tx.transactionId);
            if (!ok)
            {
                Debug.LogError("[LeonCombat] CompleteReloadAfterBusinessCommit 失败: txId=" + tx.transactionId + " currentReloadTxId=" + currentReloadTxId + " txAction=" + tx.weaponActionType + " phase=" + coordinator.CurrentPhase + " hasActiveWeaponTx=" + coordinator.HasActiveWeaponAction + " entrySeq=" + tx.stateEntrySequence + " WeaponAction=unknown");
            }
        }
    }

    /// <summary>OnReloadAnimExited 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.RestoreStateAfterWeaponAction()、LeonCombat.StopShotgunShellReload(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.RestoreStateAfterWeaponAction()、LeonCombat.StopShotgunShellReload(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnReloadAnimExited(AnimationActionTransaction tx)
    {
        if (!controller.NewPipelineValid)
        {
            return;
        }

        if (tx.transactionId != currentReloadTxId)
        {
            return;
        }

        if (tx.weaponActionType != WeaponActionType.Reload)
        {
            return;
        }

        currentReloadTxId = -1;
        StopShotgunShellReload(false);
        // 判空前缓存的换弹状态变化时发出的通知。
global::System.Action<bool> receiverValue28 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.Invoke(false);
        }

        if (controller.currentState == LeonState.Reloading)
        {
            RestoreStateAfterWeaponAction();
        }

        global::System.Action receiverValue29 = OnReloadFullyExited;
        if (!global::System.Object.ReferenceEquals(receiverValue29, null))
        {
            receiverValue29.Invoke();
        }

        NotifyInputWindowOpened();
    }

    /// <summary>
    /// 武器动作完全退出后，根据持续输入恢复真实的战斗状态。
    /// 不能只按移动输入恢复，否则按住瞄准完成换弹后会错误进入普通移动。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimExited(AnimationActionTransaction)；下游（含内部回调体） LeonAnimatorDriver.SetAimState(bool)、LeonAnimRig.SetIKMode(IKMode)、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestoreStateAfterWeaponAction()
    {
        if (controller == null)
        {
            return;
        }

        if (isAiming)
        {
            controller.ChangeState(LeonState.Aiming);
            // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue30 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue30, null))
            {
                receiverValue30.SetAimState(true);
            }

            targetAimLayerWeight = 1f;
            // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue31 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue31, null))
            {
                receiverValue31.SetIKMode(IKMode.Aim);
            }

            return;
        }

        // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver33 = controller;
        // 分支选出的里昂状态的行走中。
global::LeonState selectedValue32;
        if (controller.MoveInput.magnitude > 0.1f)
        {
            selectedValue32 = LeonState.Walking;
        }
        else
        {
            selectedValue32 = LeonState.Idle;
        }

        callReceiver33.ChangeState(selectedValue32);
    }

    /// <summary>BeginShotgunShellReloadIfNeeded 的业务处理入口；本方法直接依赖 LeonCombat.ShotgunShellReloadRoutine(int)、LeonCombat.StopShotgunShellReload(bool)、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleReload()；下游（含内部回调体） LeonCombat.ShotgunShellReloadRoutine(int)、LeonCombat.StopShotgunShellReload(bool)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">事务编号。</param>
    private void BeginShotgunShellReloadIfNeeded(int transactionId)
    {
        if (!shotgunShellByShellReload || controller == null || controller.currentWeapon != WeaponType.Shotgun
            || driver != null && driver.NativeW870ReloadActive)
        {
            return;
        }

        StopShotgunShellReload(false);
        shotgunShellReloadTxId = transactionId;
        shotgunShellReloadAnimationCompleted = false;
        shotgunShellReloadLoadsCompleted = false;
        shotgunShellReloadRoutine = StartCoroutine(ShotgunShellReloadRoutine(transactionId));
    }

    /// <summary>ShotgunShellReloadRoutine 的业务处理入口；本方法直接依赖 LeonCombat.IsShotgunShellReloadActive(int)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.BeginShotgunShellReloadIfNeeded(int)；下游（含内部回调体） LeonCombat.IsShotgunShellReloadActive(int)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">事务编号。</param>
    private IEnumerator ShotgunShellReloadRoutine(int transactionId)
    {
        // 初始延迟。
float initialDelay = Mathf.Max(0f, shotgunShellLoadInitialDelay);
        if (initialDelay > 0f)
        {
            float readyAt = Time.time + initialDelay;
            do
            {
                yield return null;
            }
            while (Time.time < readyAt);
        }

        while (IsShotgunShellReloadActive(transactionId) && weaponHandler != null && weaponHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
        {
            weaponHandler.LoadOneRoundFromReserve(WeaponType.Shotgun, transactionId);
            if (!weaponHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
            {
                break;
            }

            float nextShellAt = Time.time + Mathf.Max(0.05f, shotgunShellLoadInterval);
            do
            {
                yield return null;
            }
            while (Time.time < nextShellAt);
        }

        shotgunShellReloadLoadsCompleted = true;
        shotgunShellReloadRoutine = null;
        TryCompleteShotgunShellReload(transactionId);
    }

    /// <summary>IsShotgunShellReloadActive 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ShotgunShellReloadRoutine(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">事务编号。</param>
    private bool IsShotgunShellReloadActive(int transactionId)
    {
        return transactionId == currentReloadTxId && transactionId == shotgunShellReloadTxId && controller != null && controller.currentWeapon == WeaponType.Shotgun && controller.currentState == LeonState.Reloading;
    }

    /// <summary>IsShotgunReloadTransaction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    internal bool IsShotgunReloadTransaction(AnimationActionTransaction tx)
    {
        return shotgunShellByShellReload && tx.weaponActionType == WeaponActionType.Reload && tx.transactionId == shotgunShellReloadTxId && tx.sourceWeaponType == WeaponType.Shotgun;
    }

    /// <summary>TryCompleteShotgunShellReload 的业务处理入口；本方法直接依赖 Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonWeaponHandler.CancelReload(WeaponType)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.ShotgunShellReloadRoutine(int)；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonWeaponHandler.CancelReload(WeaponType)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">事务编号。</param>
    private void TryCompleteShotgunShellReload(int transactionId)
    {
        if (transactionId != currentReloadTxId || transactionId != shotgunShellReloadTxId || !shotgunShellReloadAnimationCompleted)
        {
            return;
        }

        if (!shotgunShellReloadLoadsCompleted && weaponHandler != null && weaponHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
        {
            return;
        }

        // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue34 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.CancelReload(WeaponType.Shotgun);
        }

        shotgunShellReloadTxId = -1;
        shotgunShellReloadAnimationCompleted = false;
        shotgunShellReloadLoadsCompleted = false;
        if (coordinator == null)
        {
            return;
        }

        // 是否成功。
bool ok = coordinator.CompleteReloadAfterBusinessCommit(transactionId);
        if (!ok)
        {
            Debug.LogError("[LeonCombat] 霰弹枪逐发装填提交失败: txId=" + transactionId + " currentReloadTxId=" + currentReloadTxId + " phase=" + coordinator.CurrentPhase + " hasActiveWeaponTx=" + coordinator.HasActiveWeaponAction);
        }
    }

    /// <summary>StopShotgunShellReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CancelReload(WeaponType)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.OnDisable()、LeonCombat.OnReloadAnimExited(AnimationActionTransaction)、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)；下游（含内部回调体） LeonWeaponHandler.CancelReload(WeaponType)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="cancelWeaponReload">是否取消武器换弹。</param>
    private void StopShotgunShellReload(bool cancelWeaponReload)
    {
        if (shotgunShellReloadRoutine != null)
        {
            StopCoroutine(shotgunShellReloadRoutine);
            shotgunShellReloadRoutine = null;
        }

        shotgunShellReloadTxId = -1;
        shotgunShellReloadAnimationCompleted = false;
        shotgunShellReloadLoadsCompleted = false;
        if (cancelWeaponReload)
        {
            // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue35 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue35, null))
            {
                receiverValue35.CancelReload(WeaponType.Shotgun);
            }
        }
    }

    // ==================== 近战 ====================
    /// <summary>校验木箱交互并启动对应的近战动画，在接触帧执行破坏。</summary>
    public bool TryStartWoodenBoxMelee(DestructibleWoodenBox target)
    {
        if (target == null || !CanAct() || controller.currentState == LeonState.Reloading || controller.currentState == LeonState.Shooting || !target.CanInteract(transform))
        {
            return false;
        }

        FullBodyActionType action = target.SelectMeleeAction(transform, controller.currentWeapon);
        if (driver == null || !driver.HasPreparedMeleeState(action))
        {
            return false;
        }

        ForceStopShoot();
        ForceAimOff();
        pendingWoodenBoxMeleeTarget = target;
        BindBoxStrikeBones(action);
        if (StartMeleeAction(action))
        {
            return true;
        }

        pendingWoodenBoxMeleeTarget = null;
        return false;
    }

    /// <summary>HandleParry 的业务处理入口；本方法直接依赖 LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.OpenParryOpportunity(VillagerController)；下游（含内部回调体） LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleParry()
    {
        if (!IsParryPromptVisible)
        {
            return;
        }

        ForceStopShoot();
        ForceAimOff();
        parryInputArmed = true;
        if (guardHeld)
        {
            parryPressedTime = guardBeginTime;
        }
        else
        {
            parryPressedTime = Time.time;
        }

        parryOpportunityOpen = false;
        // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue37 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.SetKnifeModeVisible(true);
        }

        if (StartMeleeAction(FullBodyActionType.KnifeParry))
        {
            controller.ChangeState(LeonState.Parrying);
            return;
        }

        ClearParryState(true);
    }

    /// <summary>OpenParryOpportunity 的业务处理入口；本方法直接依赖 LeonCombat.HandleParry()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowOpen()；下游（含内部回调体） LeonCombat.HandleParry()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源村民敌人控制器。</param>
    public void OpenParryOpportunity(VillagerController source)
    {
        if (source == null || parryInputArmed || !CanBeginParryAction)
        {
            return;
        }

        parryOpportunitySource = source;
        parryOpportunityOpen = true;
        if (guardHeld)
        {
            HandleParry();
        }
    }

    /// <summary>CloseParryOpportunity 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowClose()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源村民敌人控制器。</param>
    public void CloseParryOpportunity(VillagerController source)
    {
        if (source == null || source != parryOpportunitySource)
        {
            return;
        }

        parryOpportunityOpen = false;
        if (!parryInputArmed)
        {
            parryOpportunitySource = null;
        }
    }

    /// <summary>TryResolveParry 的业务处理入口；本方法直接依赖 LeonCombat.ConsumeKnifeDurability(int)、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） LeonCombat.ConsumeKnifeDurability(int)、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源村民敌人控制器。</param>
    /// <param name="contactPoint">接触点。</param>
    /// <param name="attackDirection">攻击方向。</param>
    public KnifeParryResult TryResolveParry(VillagerController source, Vector3 contactPoint, Vector3 attackDirection)
    {
        if (!parryInputArmed || source == null || source != parryOpportunitySource || (!guardHeld && Time.time - parryPressedTime > parryActiveDuration) || controller == null || controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || controller.currentState == LeonState.QTE)
        {
            return KnifeParryResult.None;
        }

        // 输入已存在时间。
float inputAge = Mathf.Max(0f, Time.time - parryPressedTime);
        // 当前小刀招架结果。
KnifeParryResult result;
        if (inputAge <= perfectParryWindow)
        {
            result = KnifeParryResult.Perfect;
        }
        else
        {
            result = KnifeParryResult.Normal;
        }

        ConsumeKnifeDurability(knifeParryDurabilityCost);
        parryInputArmed = false;
        parryOpportunityOpen = false;
        parryOpportunitySource = null;
        PlayParryFeedback(source, contactPoint, attackDirection, result);
        return result;
    }

    /// <summary>是否可以开始招架动作。</summary>
    private bool CanBeginParryAction => controller != null && controller.NewPipelineValid && controller.currentState != LeonState.Dead && controller.currentState != LeonState.Staggered && controller.currentState != LeonState.QTE && controller.currentState != LeonState.Reloading && !meleeActionActive && weaponHandler != null && weaponHandler.HasUsableKnife() && (weaponHandler == null || !weaponHandler.IsSwitching);

    /// <summary>
    /// 切换小刀常驻装备模式。小刀会保持装备，直到下一次切换回手枪。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.TestRuntimeKnifeSwitch()；下游（含内部回调体） LeonCombat.ShouldQueueKnifeToggle()、LeonCombat.TryToggleKnifeMode()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ToggleKnifeMode()
    {
        if (knifeModule != null)
        {
            return;
        }

        if (ShouldQueueKnifeToggle())
        {
            queuedKnifeToggle = !queuedKnifeToggle;
            return;
        }

        TryToggleKnifeMode();
    }

    /// <summary>ProcessQueuedKnifeToggle 的业务处理入口；本方法直接依赖 LeonCombat.ShouldQueueKnifeToggle()、LeonCombat.TryToggleKnifeMode()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.Update()；下游（含内部回调体） LeonCombat.ShouldQueueKnifeToggle()、LeonCombat.TryToggleKnifeMode()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ProcessQueuedKnifeToggle()
    {
        if (!queuedKnifeToggle || ShouldQueueKnifeToggle())
        {
            return;
        }

        queuedKnifeToggle = false;
        TryToggleKnifeMode();
    }

    /// <summary>ShouldQueueKnifeToggle 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ProcessQueuedKnifeToggle()、LeonCombat.ToggleKnifeMode()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool ShouldQueueKnifeToggle()
    {
        return meleeActionActive || (weaponHandler != null && weaponHandler.IsSwitching);
    }

    /// <summary>TryToggleKnifeMode 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.IsGunWeapon(WeaponType)、LeonWeaponHandler.SwitchTo(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ProcessQueuedKnifeToggle()、LeonCombat.ToggleKnifeMode()；下游（含内部回调体） Debug.LogWarning(object)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.IsGunWeapon(WeaponType)、LeonWeaponHandler.SwitchTo(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool TryToggleKnifeMode()
    {
        if (controller == null || weaponHandler == null || !controller.NewPipelineValid)
        {
            return false;
        }

        if (controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || controller.currentState == LeonState.QTE || controller.currentState == LeonState.Reloading)
        {
            return false;
        }

        ForceStopShoot();
        ForceAimOff();
        // 是否进入中小刀模式。
bool enteringKnifeMode = controller.currentWeapon != WeaponType.Knife;
        if (enteringKnifeMode)
        {
            if (IsGunWeapon(controller.currentWeapon))
            {
                weaponBeforeKnife = controller.currentWeapon;
            }
            else
            {
                weaponBeforeKnife = WeaponType.Pistol;
            }
        }

        // 目标武器。
WeaponType targetWeapon;
        if (enteringKnifeMode)
        {
            targetWeapon = WeaponType.Knife;
        }
        else
        {
            targetWeapon = weaponBeforeKnife;
        }

        if (weaponHandler.SwitchTo(targetWeapon))
        {
            return true;
        }

        if (!enteringKnifeMode && targetWeapon != WeaponType.Pistol && weaponHandler.SwitchTo(WeaponType.Pistol))
        {
            weaponBeforeKnife = WeaponType.Pistol;
            return true;
        }

        Debug.LogWarning("[里昂战斗] 小刀切换被拒绝：" + controller.currentWeapon + " -> " + targetWeapon);
        return false;
    }

    /// <summary>
    /// 按住进入防守姿态，松开恢复原枪，不改变已装备枪械和弹药。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonCombat.Update()、LeonInputBridge.OnDisable()、LeonInputBridge.SetGameplayInputEnabled(bool)；下游（含内部回调体） LeonCombat.CommitGuardInput(bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="held">是否保持按住。</param>
    public void HandleKnifeHold(bool held)
    {
        CommitGuardInput(held, Time.timeAsDouble);
    }

    /// <summary>CommitGuardInput 的业务处理入口；本方法直接依赖 KnifeCombatModule.CommitGuard(bool, double)、LeonCombat.ApplyGuardPose()、LeonCombat.CanAct()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.HandleParry() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleKnifeHold(bool)、LeonInputBridge.OnParry(InputAction.CallbackContext)；下游（含内部回调体） KnifeCombatModule.CommitGuard(bool, double)、LeonCombat.ApplyGuardPose()、LeonCombat.CanAct()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.HandleParry() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="held">是否保持按住。</param>
    /// <param name="timeValue">时间值。</param>
    public void CommitGuardInput(bool held, double timeValue)
    {
        if (knifeModule != null)
        {
            knifeModule.CommitGuard(held, timeValue);
            return;
        }

        if (held)
        {
            if (guardHeld || !CanBeginParryAction || !CanAct() || IsFireBlockedByWeaponAction())
            {
                return;
            }

            guardHeld = true;
            guardBeginTime = Time.time;
            ForceStopShoot();
            ForceAimOff();
            ApplyGuardPose();
            if (IsParryPromptVisible)
            {
                HandleParry();
            }

            return;
        }

        guardHeld = false;
        if (!meleeActionActive)
        {
            ClearParryState(false);
            RestoreFirearmPose();
        }
    }

    /// <summary>ApplyGuardPose 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyGuardPose()
    {
        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue41 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue41, null))
        {
            receiverValue41.SetKnifeMode(true);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue42 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            receiverValue42.SetWeapon(WeaponType.Knife);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue43 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue43, null))
        {
            receiverValue43.EndKnifeActionIK();
        }

        // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue44 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.SetKnifeModeVisible(true);
        }

        controller.ChangeState(LeonState.Parrying);
    }

    /// <summary>RestoreFirearmPose 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.ForceExitKnifeMode()、LeonCombat.OnDisable()、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)、LeonCombat.OnMeleeActionInterrupted(AnimationActionTransaction)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonController.ChangeState(LeonState)、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestoreFirearmPose()
    {
        if (controller == null)
        {
            return;
        }

        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue45 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.SetKnifeMode(IsKnifeMode);
        }

        if (!IsKnifeMode)
        {
            // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue46 = driver;
            // 条件访问得到的集合武器类型的返回结果。
bool? optionalValue47;
            if (!global::System.Object.ReferenceEquals(receiverValue46, null))
            {
                optionalValue47 = receiverValue46.SetWeaponType(controller.currentWeapon);
            }
            else
            {
                optionalValue47 = default;
            }
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue48 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue48, null))
        {
            receiverValue48.SetWeapon(controller.currentWeapon);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue49 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.EndKnifeActionIK();
        }

        // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue50 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.SetKnifeModeVisible(IsKnifeMode);
        }

        if (controller.currentState == LeonState.Parrying)
        {
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver52 = controller;
            // 分支选出的里昂状态的行走中。
global::LeonState selectedValue51;
            if (controller.MoveInput.magnitude > 0.1f)
            {
                selectedValue51 = LeonState.Walking;
            }
            else
            {
                selectedValue51 = LeonState.Idle;
            }

            callReceiver52.ChangeState(selectedValue51);
        }
    }

    /// <summary>HandleKnifeAttack 的业务处理入口；本方法直接依赖 LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.IsFireBlockedByWeaponAction()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonWeaponHandler.HasUsableKnife()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.IsFireBlockedByWeaponAction()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonWeaponHandler.HasUsableKnife()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandleKnifeAttack()
    {
        if (controller == null || !controller.NewPipelineValid || controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || controller.currentState == LeonState.QTE)
        {
            return;
        }

        if (controller.currentState == LeonState.Reloading || controller.currentState == LeonState.Traversing || IsFireBlockedByWeaponAction())
        {
            return;
        }

        if (weaponHandler == null || !weaponHandler.HasUsableKnife())
        {
            return;
        }

        if (meleeActionActive)
        {
            if (allowBufferedKnifeAttack && currentMeleeAction == FullBodyActionType.KnifeQuickAttack)
            {
                queuedKnifeAttack = true;
            }

            return;
        }

        queuedKnifeAttack = false;
        ForceStopShoot();
        ForceAimOff();
        StartMeleeAction(FullBodyActionType.KnifeQuickAttack);
    }

    /// <summary>StartMeleeAction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonController.TriggerMeleeCameraPush()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleKnifeAttack()、LeonCombat.HandleParry()、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)、LeonCombat.TryStartQTEBodyTechnique(int, int)、LeonCombat.TryStartWoodenBoxMelee(DestructibleWoodenBox)；下游（含内部回调体） LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonController.TriggerMeleeCameraPush()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">动作。</param>
    /// <param name="qteSessionId">配对动作会话编号。</param>
    private bool StartMeleeAction(FullBodyActionType action, int qteSessionId = 0, float normalizedStart = 0f)
    {
        if (coordinator == null || !coordinator.RequestMeleeAction(action, qteSessionId, normalizedStart))
        {
            return false;
        }

        currentMeleeTxId = coordinator.CurrentFullBodyTransactionId;
        currentMeleeAction = action;
        meleeActionActive = true;
        if (action == FullBodyActionType.KnifeQuickAttack || action == FullBodyActionType.KnifeParry)
        {
            // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue53 = weaponVisualController;
            if (!global::System.Object.ReferenceEquals(receiverValue53, null))
            {
                receiverValue53.SetKnifeModeVisible(true);
            }
        }

        if (action == FullBodyActionType.KnifeQuickAttack)
        {
            controller.TriggerMeleeCameraPush();
        }

        controller.ChangeState(LeonState.MeleeAttacking);
        return true;
    }

    /// <summary>
    /// 后续硬直快速反应事件调用的体术入口。
    /// 普通输入不得传入伪造会话编号。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStartNearestBodyTechnique(out int)；下游（含内部回调体） KnifeCombatModule.YieldToMeleeFinisher()、LeonCombat.StartMeleeAction(FullBodyActionType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="techniqueIndex">体术索引。</param>
    /// <param name="qteSessionId">配对动作会话编号。</param>
    public bool TryStartQTEBodyTechnique(int techniqueIndex, int qteSessionId, VillagerQTEParticipant target = null)
    {
        if (knifeModule != null && !knifeModule.YieldToMeleeFinisher())
        {
            return false;
        }

        if (qteSessionId <= 0 || IsKnifeMode && !IsHoldingTemporaryKnife || meleeActionActive)
        {
            return false;
        }

        // 动作。
FullBodyActionType action;
        if (techniqueIndex == 1)
        {
            action = FullBodyActionType.QTESpinningHookKick;
        }
        else
        {
            action = FullBodyActionType.QTERightCutKick;
        }

        ForceStopShoot();
        ForceAimOff();
        pendingEnemyMeleeTarget = target;
        float normalizedStart = 0f;
        if (target != null)
        {
            float distance = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).magnitude;
            float approachFrames = Mathf.Max(0f, distance - EnemyKickReach) * 60f / 3.4f;
            float startFrame = Mathf.Clamp(51f - approachFrames, 18f, 42f);
            normalizedStart = startFrame / MeleeActionKinds.EnemyKickFrames(action);
        }
        if (StartMeleeAction(action, qteSessionId, normalizedStart))
        {
            return true;
        }
        pendingEnemyMeleeTarget = null;
        return false;
    }

    /// <summary>OnMeleeActionCompleted 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.CompleteMeleeAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.CompleteMeleeAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnMeleeActionCompleted(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        switch (tx.fullBodyActionType)
        {
            case FullBodyActionType.KnifeQuickAttack:
            case FullBodyActionType.KnifeParry:
                coordinator.CompleteMeleeAction();
                break;
            case FullBodyActionType.QTERightCutKick:
            case FullBodyActionType.QTESpinningHookKick:
            case FullBodyActionType.WoodenBoxKick:
            case FullBodyActionType.WoodenBoxStomp:
            case FullBodyActionType.WoodenBoxElbow:
            case FullBodyActionType.WoodenBoxPistolElbow:
                coordinator.CompleteMeleeAction();
                break;
        }
    }

    /// <summary>OnMeleeActionExited 的业务处理入口；本方法直接依赖 LeonCombat.ApplyGuardPose()、LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonWeaponHandler.HasUsableKnife()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ApplyGuardPose()、LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()、LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonController.ChangeState(LeonState)、LeonWeaponHandler.HasUsableKnife()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnMeleeActionExited(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        // 刚退出或中断的动作是否为小刀招架。
bool wasParry = tx.fullBodyActionType == FullBodyActionType.KnifeParry;
        // 当前动作退出后是否立即衔接下一次小刀攻击。
bool restartKnifeAttack = tx.fullBodyActionType == FullBodyActionType.KnifeQuickAttack && queuedKnifeAttack && weaponHandler != null && weaponHandler.HasUsableKnife();
        currentMeleeTxId = -1;
        currentMeleeAction = FullBodyActionType.None;
        meleeActionActive = false;
        queuedKnifeAttack = false;
        pendingWoodenBoxMeleeTarget = null;
        pendingEnemyMeleeTarget = null;
        if (wasParry)
        {
            ClearParryState(true);
        }

        if (controller.currentState != LeonState.Dead && controller.currentState != LeonState.QTE && controller.currentState != LeonState.Staggered)
        {
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver56 = controller;
            // 分支选出的里昂状态的行走中。
global::LeonState selectedValue55;
            if (controller.MoveInput.magnitude > 0.1f)
            {
                selectedValue55 = LeonState.Walking;
            }
            else
            {
                selectedValue55 = LeonState.Idle;
            }

            callReceiver56.ChangeState(selectedValue55);
        }

        if (guardHeld && weaponHandler != null && weaponHandler.HasUsableKnife())
        {
            ApplyGuardPose();
        }
        else
        {
            RestoreFirearmPose();
        }

        if (restartKnifeAttack)
        {
            StartMeleeAction(FullBodyActionType.KnifeQuickAttack);
        }
        else
        {
            NotifyInputWindowOpened();
        }
    }

    /// <summary>OnMeleeActionInterrupted 的业务处理入口；本方法直接依赖 LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnMeleeActionInterrupted(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        // 刚退出或中断的动作是否为小刀招架。
bool wasParry = tx.fullBodyActionType == FullBodyActionType.KnifeParry;
        currentMeleeTxId = -1;
        currentMeleeAction = FullBodyActionType.None;
        meleeActionActive = false;
        queuedKnifeAttack = false;
        pendingWoodenBoxMeleeTarget = null;
        pendingEnemyMeleeTarget = null;
        guardHeld = false;
        RestoreFirearmPose();
        if (wasParry)
        {
            ClearParryState(true);
        }
    }

    /// <summary>OnMeleeHitWindow 的业务处理入口；本方法直接依赖 Action&lt;int, FullBodyActionType&gt;.Invoke(int, FullBodyActionType)、AudioManager.Play3D(string, Vector3)、Debug.LogError(object)、DestructibleWoodenBox.TryBreakByMelee(Transform)、LeonCombat.ConsumeKnifeDurability(int)、LeonWeaponHandler.GetWeapon(WeaponType)、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, FullBodyActionType&gt;.Invoke(int, FullBodyActionType)、AudioManager.Play3D(string, Vector3)、Debug.LogError(object)、DestructibleWoodenBox.TryBreakByMelee(Transform)、LeonCombat.ConsumeKnifeDurability(int)、LeonWeaponHandler.GetWeapon(WeaponType)、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnMeleeHitWindow(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        if (tx.fullBodyActionType == FullBodyActionType.KnifeQuickAttack)
        {
            // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue57 = weaponHandler;
            // 小刀。
WeaponBase knife;
            if (!global::System.Object.ReferenceEquals(receiverValue57, null))
            {
                knife = receiverValue57.GetWeapon(WeaponType.Knife);
            }
            else
            {
                knife = default;
            }

            if (knife == null || knife.data == null)
            {
                Debug.LogError("[LeonCombat] 小刀命中窗口触发，但小刀武器数据未绑定");
                return;
            }

            if (knife.FireAt(transform.position + transform.forward * 1.2f + Vector3.up))
            {
                ConsumeKnifeDurability(knifeAttackDurabilityCost);
            }

            return;
        }

        if (MeleeActionKinds.IsEnemyKick(tx.fullBodyActionType) && tx.qteSessionId > 0)
        {
            if (OnQTEBodyTechniqueHit != null)
            {
                OnQTEBodyTechniqueHit.Invoke(tx.qteSessionId, tx.fullBodyActionType);
            }
            pendingEnemyMeleeTarget = null;
        }
    }

    private void ForceAimOff()
    {
        w870TriggerQueued = false;
        isAiming = false;
        aimLayerWeight = 0f;
        targetAimLayerWeight = 0f;
        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue62 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue62, null))
        {
            receiverValue62.SetAimState(false);
        }

        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue63 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue63, null))
        {
            receiverValue63.SetAimLayerWeight(0f);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue64 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue64, null))
        {
            receiverValue64.SetIKMode(IKMode.None);
        }

        // 判空前缓存的瞄准状态变化时发出的通知。
global::System.Action<bool> receiverValue65 = OnAimStateChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue65, null))
        {
            receiverValue65.Invoke(false);
        }
    }

    /// <summary>ConsumeKnifeDurability 的业务处理入口；本方法直接依赖 InventoryManager.TryConsumeDurability(string, int, out bool)、LeonWeaponHandler.HandleKnifeBroken()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)；下游（含内部回调体） InventoryManager.TryConsumeDurability(string, int, out bool)、LeonWeaponHandler.HandleKnifeBroken()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">大小。</param>
    private void ConsumeKnifeDurability(int amount)
    {
        // 背包数据与操作宿主。
InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            return;
        }

        if (inventoryManager.TryConsumeKnife(inventoryManager.GetPreferredKnife(), amount, out bool /* 中文含义：是否损坏。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ broken) && broken)
        {
            queuedKnifeAttack = false;
            // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue66 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue66, null))
            {
                receiverValue66.HandleKnifeBroken();
            }
        }
    }

    /// <summary>ClearParryState 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.SetKnifeModeVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleDeath()、LeonCombat.HandleHit(LeonHitDirection)、LeonCombat.HandleParry()、LeonCombat.InterruptForQTE() 等共 8 处；下游（含内部回调体） LeonWeaponVisualController.SetKnifeModeVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreWeaponVisual">是否恢复武器视觉表现。</param>
    private void ClearParryState(bool restoreWeaponVisual)
    {
        parryOpportunityOpen = false;
        parryInputArmed = false;
        parryOpportunitySource = null;
        parryPressedTime = 0f;
        if (restoreWeaponVisual && weaponVisualController != null)
        {
            weaponVisualController.SetKnifeModeVisible(IsKnifeMode);
        }
    }

    /// <summary>PlayParryFeedback 的业务处理入口；本方法直接依赖 AudioManager.Play3D(string, Vector3)、LeonCombat.ParryHitStopRoutine(VillagerController, bool)、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、RE4CameraController.Shake(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)；下游（含内部回调体） AudioManager.Play3D(string, Vector3)、LeonCombat.ParryHitStopRoutine(VillagerController, bool)、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、RE4CameraController.Shake(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源村民敌人控制器。</param>
    /// <param name="contactPoint">接触点。</param>
    /// <param name="attackDirection">攻击方向。</param>
    /// <param name="result">当前小刀招架结果。</param>
    internal void PresentChainsawDefense(VillagerController source, Vector3 contactPoint, Vector3 attackDirection)
    {
        PlayParryFeedback(source, contactPoint, attackDirection, KnifeParryResult.Normal);
    }

    private void PlayParryFeedback(VillagerController source, Vector3 contactPoint, Vector3 attackDirection, KnifeParryResult result)
    {
        // 是否完美。
bool perfect = result == KnifeParryResult.Perfect;
        // 玩家接触。
Vector3 playerContact = transform.position + Vector3.up * 1.15f + transform.forward * 0.55f;
        // 解算后的接触。
Vector3 resolvedContact;
        if (contactPoint == Vector3.zero)
        {
            resolvedContact = playerContact;
        }
        else
        {
            resolvedContact = Vector3.Lerp(playerContact, contactPoint, 0.35f);
        }

        SpawnParrySparks(resolvedContact, attackDirection, perfect);
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue68 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue69;
        if (!global::System.Object.ReferenceEquals(receiverValue68, null))
        {
            optionalValue69 = receiverValue68.Play3D(AudioEventNames.ChainsawHit, resolvedContact);
        }
        else
        {
            optionalValue69 = default;
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue70 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue70, null))
        {
            // 判空前缓存的接收者值70的摄像机控制器。
global::RE4CameraController receiverValue71 = receiverValue70.CameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue71, null))
            {
                // 按原顺序缓存的实参0.16f。
float argumentValue74;
                if (perfect)
                {
                    argumentValue74 = 0.16f;
                }
                else
                {
                    argumentValue74 = 0.1f;
                }

                // 分支选出的2.2f。
float selectedValue73;
                if (perfect)
                {
                    selectedValue73 = 2.2f;
                }
                else
                {
                    selectedValue73 = 1.45f;
                }

                receiverValue71.Shake(argumentValue74, selectedValue73);
            }
        }

        StopParryHitStop();
        parryHitStopRoutine = StartCoroutine(ParryHitStopRoutine(source, perfect));
    }

    private void StopParryHitStop()
    {
        if (parryHitStopRoutine != null)
        {
            StopCoroutine(parryHitStopRoutine);
            parryHitStopRoutine = null;
        }
        RestoreParryPlayerSpeed();
    }

    private void RestoreParryPlayerSpeed()
    {
        if (driver != null)
        {
            driver.SetLegacyParryHitStop(false);
        }
    }

    private IEnumerator ParryHitStopRoutine(VillagerController source, bool perfect)
    {
        float duration = normalParryHitStop;
        if (perfect)
        {
            duration = perfectParryHitStop;
        }
        // 敌人停帧由同一个组件延长，避免两套恢复值互相把速度恢复成零。
        if (source != null)
        {
            KnifeHitStop enemyStop = source.GetComponent<KnifeHitStop>();
            if (enemyStop == null)
            {
                enemyStop = source.gameObject.AddComponent<KnifeHitStop>();
                enemyStop.animator = source.GetComponent<Animator>();
            }
            enemyStop.Pause(duration);
        }
        if (driver != null)
        {
            driver.SetLegacyParryHitStop(true);
        }
        if (duration > 0f)
        {
            float resumeAt = Time.realtimeSinceStartup + duration;
            do
            {
                yield return null;
            }
            while (Time.realtimeSinceStartup < resumeAt);
        }
        RestoreParryPlayerSpeed();
        parryHitStopRoutine = null;
    }

    /// <summary>SpawnParrySparks 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、ParticleSystem.Burst.Burst(float, short)、ParticleSystem.EmissionModule.SetBursts(ParticleSystem.Burst[]) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、LeonCombat.PresentKnifeSparks(Vector3, Vector3, bool)；下游（含内部回调体） GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、ParticleSystem.Burst.Burst(float, short)、ParticleSystem.EmissionModule.SetBursts(ParticleSystem.Burst[]) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="attackDirection">攻击方向。</param>
    /// <param name="perfect">是否完美。</param>
    private void SpawnParrySparks(Vector3 position, Vector3 attackDirection, bool perfect)
    {
        var kind = ChapterOneEffect.Parry;
        if (perfect)
        {
            kind = ChapterOneEffect.PerfectParry;
        }
        if (ChapterOneEffects.TryPlay(kind, position, -attackDirection))
        {
            return;
        }
        if (knifeParrySparkPrefab == null)
        {
            return;
        }

        // 方向。
Vector3 direction = Vector3.up;
        if (attackDirection.sqrMagnitude > .001f)
        {
            direction = -attackDirection.normalized;
        }

        // 旋转。
Quaternion rotation = Quaternion.LookRotation(direction);
        // 对象池。
GameObjectPoolManager pool = GameObjectPoolManager.Instance;
        // 效果对象。
GameObject effectObject;
        if (pool != null)
        {
            effectObject = pool.GetVisualPool(knifeParrySparkPrefab).Get(position, rotation, null);
        }
        else
        {
            effectObject = Instantiate(knifeParrySparkPrefab, position, rotation);
        }

        if (effectObject == null)
        {
            return;
        }

        // 粒子集合。
        ParticleSystem particles = effectObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            if (pool != null)
            {
                pool.Return(effectObject);
            }
            else
            {
                Destroy(effectObject);
            }
            return;
        }
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // 主要。
ParticleSystem.MainModule main = particles.main;
        // 形状。
ParticleSystem.ShapeModule shape = particles.shape;
        // 数量。
short count;
        if (perfect)
        {
            main.startLifetime = .22f;
            main.startSpeed = 5.5f;
            main.startSize = .045f;
            shape.angle = 48f;
            count = 20;
        }
        else
        {
            main.startLifetime = .15f;
            main.startSpeed = 3.8f;
            main.startSize = .032f;
            shape.angle = 34f;
            count = 11;
        }

        // 发射。
ParticleSystem.EmissionModule emission = particles.emission;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, count));
        particles.Play();
        if (pool != null)
        {
            pool.ReturnDelayed(effectObject, .5f);
        }
        else
        {
            Destroy(effectObject, .5f);
        }
    }

    /// <summary>ForceExitKnifeMode 的业务处理入口；本方法直接依赖 LeonCombat.RestoreFirearmPose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleDeath()、LeonCombat.HandleHit(LeonHitDirection)、LeonCombat.InterruptForQTE()；下游（含内部回调体） LeonCombat.RestoreFirearmPose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ForceExitKnifeMode()
    {
        queuedKnifeToggle = false;
        guardHeld = false;
        RestoreFirearmPose();
    }

    // ==================== 受击 ====================
    /// <summary>HandleHit 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.HandleDeferredTraversalHit(LeonHitDirection)；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitDirection">命中方向。</param>
    public bool HandleHit(LeonHitDirection hitDirection)
    {
        if (controller.currentState == LeonState.Dead)
        {
            return false;
        }

        if (!controller.NewPipelineValid)
        {
            return false;
        }

        ClearParryState(true);
        ForceExitKnifeMode();
        // 是否成功。
bool ok = coordinator.RequestHit(hitDirection);
        if (!ok)
        {
            return false;
        }

        // 判空前缓存的小刀战斗模块。
global::KnifeCombatModule receiverValue80 = knifeModule;
        if (!global::System.Object.ReferenceEquals(receiverValue80, null))
        {
            receiverValue80.Interrupt(false);
        }

        controller.ChangeState(LeonState.Staggered);
        ForceStopShoot();
        return true;
    }

    /// <summary>OnHitAnimCompleted 的业务处理入口；本方法直接依赖 LeonController.ChangeState(LeonState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ChangeState(LeonState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnHitAnimCompleted(AnimationActionTransaction tx)
    {
        // 受击唯一完成入口：由 FullBodyActionSMB → Coordinator 触发
        if (controller.currentState == LeonState.Dead)
        {
            return;
        }

        // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver82 = controller;
        // 分支选出的里昂状态的行走中。
global::LeonState selectedValue81;
        if (controller.MoveInput.magnitude > 0.1f)
        {
            selectedValue81 = LeonState.Walking;
        }
        else
        {
            selectedValue81 = LeonState.Idle;
        }

        callReceiver82.ChangeState(selectedValue81);
        NotifyInputWindowOpened();
    }

    // ==================== 死亡 ====================
    /// <summary>HandleDeath 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestDeath()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.BeginDeathSequence()；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestDeath()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleDeath()
    {
        // 判空前缓存的小刀战斗模块。
global::KnifeCombatModule receiverValue83 = knifeModule;
        if (!global::System.Object.ReferenceEquals(receiverValue83, null))
        {
            receiverValue83.Interrupt(false);
        }

        if (!controller.NewPipelineValid)
        {
            return;
        }

        ClearParryState(true);
        coordinator.RequestDeath();
        controller.ChangeState(LeonState.Dead);
        ForceStopShoot();
        ForceAimOff();
        ForceExitKnifeMode();
    }

    // ==================== 动画事件回调 ====================
    /// <summary>OnWeaponActionInterrupted 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.StopShotgunShellReload(bool)、LeonWeaponHandler.CancelReload(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.StopShotgunShellReload(bool)、LeonWeaponHandler.CancelReload(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnWeaponActionInterrupted(AnimationActionTransaction tx)
    {
        if (!controller.NewPipelineValid)
        {
            return;
        }

        if (tx.weaponActionType != WeaponActionType.Reload)
        {
            return;
        }

        if (tx.transactionId != currentReloadTxId)
        {
            return;
        }

        ClearReloadFireRequest();
        StopShotgunShellReload(false);
        // 判空前缓存的管理当前装备与武器切换的组件。
global::LeonWeaponHandler receiverValue84 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue84, null))
        {
            receiverValue84.CancelReload(tx.sourceWeaponType);
        }

        currentReloadTxId = -1;
        // 判空前缓存的换弹状态变化时发出的通知。
global::System.Action<bool> receiverValue85 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue85, null))
        {
            receiverValue85.Invoke(false);
        }

        // 判空前缓存的换弹动作完全退出时发出的通知。
global::System.Action receiverValue86 = OnReloadFullyExited;
        if (!global::System.Object.ReferenceEquals(receiverValue86, null))
        {
            receiverValue86.Invoke();
        }
    }

    // ==================== 武器模型 ====================
    /// <summary>CanAct 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.ExecuteFirearmAttack()、LeonCombat.HandleAim(bool)、LeonCombat.HandleReload()、LeonCombat.TryStartWoodenBoxMelee(DestructibleWoodenBox)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanAct()
    {
        if (controller == null || coordinator == null || !coordinator.isActiveAndEnabled || controller.IsHeldByTrap)
        {
            return false;
        }

        return controller.currentState != LeonState.Staggered && controller.currentState != LeonState.Dead && controller.currentState != LeonState.QTE && controller.currentState != LeonState.MeleeAttacking && controller.currentState != LeonState.Traversing;
    }

    /// <summary>BeginTemporaryKnife 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.PrepareTransaction(bool)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool BeginTemporaryKnifeCarryAfterQTE(WeaponType returnWeapon, bool startReturnImmediately)
    {
        bool isGunReturnWeapon = returnWeapon == WeaponType.Pistol
            || returnWeapon == WeaponType.Shotgun
            || returnWeapon == WeaponType.Rifle;
        if (controller == null || controller.currentState == LeonState.Dead
            || driver == null || !driver.SupportsNativeKnifeCarry
            || weaponHandler == null || !isGunReturnWeapon)
        {
            return false;
        }

        if (!weaponHandler.HasWeaponInInventory(WeaponType.Knife) || !weaponHandler.HasWeaponInInventory(returnWeapon))
        {
            return false;
        }

        ForceAimOff();
        knifeReturnWeapon = returnWeapon;
        temporaryKnifeEquipped = true;
        if (startReturnImmediately)
        {
            knifeReturnAt = Time.timeAsDouble;
        }
        else
        {
            knifeReturnAt = Time.timeAsDouble + TemporaryKnifeCarrySeconds;
        }

        weaponHandler.ForceSyncCurrentWeapon(WeaponType.Knife, true);
        if (weaponVisualController != null)
        {
            weaponVisualController.SetKnifeModeVisible(true);
        }

        return true;
    }

    private const float TemporaryKnifeCarrySeconds = 5f;
    private bool temporaryKnifeEquipped;
    private WeaponType knifeReturnWeapon;
    private double knifeReturnAt = double.PositiveInfinity;

    private void UpdateTemporaryKnifeCarry()
    {
        if (!temporaryKnifeEquipped || weaponHandler == null || weaponHandler.IsSwitching)
        {
            return;
        }

        if (!IsKnifeMode)
        {
            temporaryKnifeEquipped = false;
            knifeReturnAt = double.PositiveInfinity;
            if (desiredAim)
            {
                HandleAim(true);
            }
            return;
        }

        if (Time.timeScale <= 0f || Time.timeAsDouble < knifeReturnAt || !CanAct()
            || knifeModule != null && knifeModule.Active
            || coordinator.HasActiveFullBodyAction)
        {
            return;
        }

        if (!weaponHandler.HasWeaponInInventory(knifeReturnWeapon))
        {
            knifeReturnWeapon = WeaponType.Pistol;
            if (!weaponHandler.HasWeaponInInventory(knifeReturnWeapon))
            {
                temporaryKnifeEquipped = false;
                return;
            }
        }
        weaponHandler.SwitchTo(knifeReturnWeapon);
    }

    public void BeginTemporaryKnife()
    {
        ForceStopShoot();
        ForceAimOff();
        if (driver != null && driver.SupportsNativeKnifeCarry && weaponHandler != null && controller != null)
        {
            if (IsGunWeapon(controller.currentWeapon))
            {
                knifeReturnWeapon = controller.currentWeapon;
                temporaryKnifeEquipped = true;
                weaponHandler.ForceSyncCurrentWeapon(WeaponType.Knife, true);
            }
            knifeReturnAt = double.PositiveInfinity;
            return;
        }
        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue87 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue87, null))
        {
            receiverValue87.SetKnifeMode(false);
        }
    }

    /// <summary>EndTemporaryKnife 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonCombat.HandleAim(bool)、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.EndTransaction(bool)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonCombat.HandleAim(bool)、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreAim">是否恢复瞄准。</param>
    public void EndTemporaryKnife(bool restoreAim)
    {
        // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue88 = driver;
        if (temporaryKnifeEquipped && IsKnifeMode)
        {
            knifeReturnAt = Time.timeAsDouble;
            if (!desiredAim && weaponHandler != null && weaponHandler.HasUsableKnife())
            {
                knifeReturnAt += TemporaryKnifeCarrySeconds;
            }
        }
        else if (!global::System.Object.ReferenceEquals(receiverValue88, null))
        {
            receiverValue88.SetKnifeMode(false);
        }

        if (controller == null)
        {
            return;
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue89 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue89, null))
        {
            receiverValue89.SetWeapon(controller.currentWeapon);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue90 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue90, null))
        {
            receiverValue90.EndKnifeActionIK();
        }

        if (controller.currentState == LeonState.MeleeAttacking || controller.currentState == LeonState.Parrying)
        {
            // 调用前缓存的里昂玩家控制器。
global::LeonController callReceiver92 = controller;
            // 分支选出的里昂状态的行走中。
global::LeonState selectedValue91;
            if (controller.MoveInput.magnitude > .1f)
            {
                selectedValue91 = LeonState.Walking;
            }
            else
            {
                selectedValue91 = LeonState.Idle;
            }

            callReceiver92.ChangeState(selectedValue91);
        }

        if (restoreAim && desiredAim && !temporaryKnifeEquipped)
        {
            HandleAim(true);
        }

        NotifyInputWindowOpened();
    }

    private void NotifyInputWindowOpened()
    {
        if (inputBridge != null)
        {
            inputBridge.NotifyActionWindowOpened();
        }
    }

    /// <summary>PresentKnifeSparks 的业务处理入口；本方法直接依赖 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="direction">方向。</param>
    /// <param name="perfect">是否完美。</param>
    public void PresentKnifeSparks(Vector3 position, Vector3 direction, bool perfect)
    {
        SpawnParrySparks(position, direction, perfect);
    }

    /// <summary>IsFireBlockedByWeaponAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.ExecuteFirearmAttack()、LeonCombat.HandleKnifeAttack()、LeonCombat.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsFireBlockedByWeaponAction()
    {
        if (weaponHandler != null && weaponHandler.IsFireBlockedByWeaponAction)
        {
            return true;
        }

        return coordinator != null && (coordinator.HasPendingWeaponSwitch || coordinator.WaitingForWeaponExit || coordinator.IsWeaponSwitchInProgress);
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.TryToggleKnifeMode()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    private static bool IsGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }
}
