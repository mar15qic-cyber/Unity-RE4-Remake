using UnityEngine;
using System.Collections;

/// <summary>玩家控制、动作与运动使用的 KnifeParryResult 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum KnifeParryResult
{
    /// <summary>中文含义：没有发生招架。</summary>
    None = 0,
    /// <summary>中文含义：普通招架。</summary>
    Normal = 1,
    /// <summary>中文含义：完美招架。</summary>
    Perfect = 2
}

/// <summary>玩家控制、动作与运动组件 LeonCombat，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>玩家战斗输入和动作申请入口；枪械、小刀与配对沿已绑定武器、KnifeCombatModule 和协调器入口执行，不创建平行伤害判定。</remarks>
public class LeonCombat : MonoBehaviour, ICrosshairStateProvider
{
    /// <summary>中文含义：小刀战斗模块。使用位置：EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.TryApplyAttackHit(int) 等共 23 处。</summary>
    [Header("独立小刀战斗")]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀模块")]
    [UnityEngine.InspectorName("小刀模块")]
    public KnifeCombatModule knifeModule;
    /// <summary>中文含义：是否希望进入或保持瞄准状态。使用位置：LeonCombat.EndTemporaryKnife(bool)、LeonCombat.HandleAim(bool)。</summary>
    private bool desiredAim;
    private bool aimWaitingForStand;
    private bool cqbrShotWaitingForAim;
    private bool pistolTriggerQueued;
    /// <summary>中文含义：小刀招架火花预制件。使用位置：作为 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion) 的参数、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 等共 4 处。</summary>
    private GameObject knifeParrySparkPrefab;
    /// <summary>中文含义：里昂玩家控制器。使用位置：作为 Debug.LogWarning(object) 的参数、作为 LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType) 的参数、作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数 等共 37 处。</summary>
    private LeonController controller;
    /// <summary>中文含义：管理当前装备与武器切换的组件。使用位置：LeonCombat.CanBeginParryAction.get、LeonCombat.ConsumeKnifeDurability(int)、LeonCombat.ExecuteFirearmAttack() 等共 17 处。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>中文含义：里昂动画控制器驱动器。使用位置：LeonCombat.ApplyGuardPose()、LeonCombat.BeginTemporaryKnife()、LeonCombat.EndTemporaryKnife(bool) 等共 12 处。</summary>
    private LeonAnimatorDriver driver;
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：作为 Debug.LogError(object) 的参数、作为 LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType) 的参数、LeonCombat.BindCoordinatorEvents() 等共 14 处。</summary>
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：当前里昂动画绑定组件。使用位置：LeonCombat.ApplyGuardPose()、LeonCombat.EndTemporaryKnife(bool)、LeonCombat.ForceAimOff() 等共 7 处。</summary>
    private LeonAnimRig animRig;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonCombat.ApplyGuardPose()、LeonCombat.ClearParryState(bool)、LeonCombat.HandleParry() 等共 6 处。</summary>
    private LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：手枪模型。</summary>
    [Header("武器模型")]
    public GameObject pistolModel;
    /// <summary>中文含义：步枪模型。</summary>
    public GameObject rifleModel;
    /// <summary>中文含义：小刀模型。</summary>
    public GameObject knifeModel;
    /// <summary>中文含义：手枪视觉表现模型。</summary>
    [Header("武器视觉模型（骨骼下）")]
    public GameObject pistolVisualModel;
    /// <summary>中文含义：步枪视觉表现模型。</summary>
    public GameObject rifleVisualModel;
    /// <summary>中文含义：开火间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("射击设置")]
    public float fireInterval = 0.5f;
    /// <summary>中文含义：射击动画锁定时间。半自动武器射击后至少保持射击状态的时间，用来等待手部射击动作收尾。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("半自动武器射击后至少保持射击状态的时间，用来等待手部射击动作收尾。")]
    [Min(0f)]
    public float shootAnimationLockTime = 0.9f;
    /// <summary>中文含义：区分轻按与持续按住射击键的时间阈值。</summary>
    public float tapHoldThreshold = 0.2f;
    /// <summary>中文含义：霰弹枪是否采用逐发装填。霰弹枪换弹时逐发补入弹仓，而不是动画完成时一次性填满。使用位置：LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)。</summary>
    [Header("霰弹枪逐发装填")]
    [Tooltip("霰弹枪换弹时逐发补入弹仓，而不是动画完成时一次性填满。")]
    public bool shotgunShellByShellReload = true;
    /// <summary>中文含义：霰弹枪开始逐发装填前的等待时间。进入换弹后第一发装入前的等待时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("进入换弹后第一发装入前的等待时间。")]
    [Min(0f)]
    public float shotgunShellLoadInitialDelay = 0.45f;
    /// <summary>中文含义：霰弹枪每装入一发弹药的间隔。每发霰弹之间的装填间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("每发霰弹之间的装填间隔。")]
    [Min(0.05f)]
    public float shotgunShellLoadInterval = 0.42f;
    /// <summary>中文含义：完美招架判定时窗。使用位置：LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)。</summary>
    [Header("小刀弹反")]
    [Range(0.03f, 0.25f)]
    public float perfectParryWindow = 0.11f;
    /// <summary>中文含义：招架有效判定的持续时间。使用位置：LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)。</summary>
    [Min(0.1f)]
    public float parryActiveDuration = 0.55f;
    /// <summary>中文含义：每次小刀攻击消耗的耐久量。使用位置：作为 LeonCombat.ConsumeKnifeDurability(int) 的参数。</summary>
    [Min(1)]
    public int knifeAttackDurabilityCost = 1;
    /// <summary>中文含义：每次小刀招架消耗的耐久量。使用位置：作为 LeonCombat.ConsumeKnifeDurability(int) 的参数。</summary>
    [Min(1)]
    public int knifeParryDurabilityCost = 4;
    /// <summary>中文含义：普通招架命中后的顿帧持续时间。使用位置：LeonCombat.ParryHitStopRoutine(VillagerController, bool)。</summary>
    [Min(0f)]
    public float normalParryHitStop = 0.035f;
    /// <summary>中文含义：完美招架命中后的顿帧持续时间。使用位置：LeonCombat.ParryHitStopRoutine(VillagerController, bool)。</summary>
    [Min(0f)]
    public float perfectParryHitStop = 0.065f;
    /// <summary>中文含义：是否允许在没有招架提示时切换小刀模式。</summary>
    public bool allowKnifeToggleWithoutParryPrompt = true;
    /// <summary>中文含义：是否允许在当前突刺中缓存下一次小刀攻击。为 true 时允许在快速突刺过程中预存下一次攻击；为 false 时本次突刺内的重复攻击输入不会排队。动作尚未退出时再次按下攻击，会缓存下一次突刺并在恢复帧立即衔接。使用位置：LeonCombat.HandleKnifeAttack()。</summary>
    [Header("小刀快速突刺")]
    [Tooltip("动作尚未退出时再次按下攻击，会缓存下一次突刺并在恢复帧立即衔接")]
    public bool allowBufferedKnifeAttack = true;
    /// <summary>中文含义：最近一次按下射击键的时间。</summary>
    float shootPressTime;
    /// <summary>中文含义：射击键是否仍按住。使用位置：LeonCombat.ExecuteFirearmAttack()、LeonCombat.ExecuteFirearmAttackReleased()、LeonCombat.ForceStopShoot() 等共 5 处。</summary>
    bool isShootHeld;
    /// <summary>中文含义：开火协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、LeonCombat.ForceStopShoot()。</summary>
    Coroutine fireRoutine;
    /// <summary>中文含义：射击动作锁定解除的截止时间。使用位置：LeonCombat.EndShoot()、LeonCombat.ExecuteFirearmAttack()、LeonCombat.ExecuteFirearmAttackReleased() 等共 6 处。</summary>
    private float shootLockedUntil;
    /// <summary>中文含义：是否缓存了等待处理的射击松开输入。使用位置：LeonCombat.EndShoot()、LeonCombat.ExecuteFirearmAttack()、LeonCombat.ExecuteFirearmAttackReleased() 等共 6 处。</summary>
    private bool shootReleaseQueued;
    /// <summary>中文含义：是否正在瞄准。使用位置：LeonCombat.EndShoot()、LeonCombat.ExecuteFirearmAttack()、LeonCombat.ForceAimOff() 等共 7 处。</summary>
    bool isAiming;
    /// <summary>中文含义：瞄准动画图层权重。使用位置：作为 LeonAnimatorDriver.SetAimLayerWeight(float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Approximately(float, float) 的参数 等共 9 处。</summary>
    private float aimLayerWeight;
    /// <summary>中文含义：目标瞄准动画图层权重。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 9 处。</summary>
    private float targetAimLayerWeight;
    /// <summary>中文含义：瞄准图层混合速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    public float aimLayerBlendSpeed = 4f;
    // 管线状态
    /// <summary>中文含义：当前换弹动作事务编号。使用位置：作为 Debug.LogError(object) 的参数、作为 LeonCombat.BeginShotgunShellReloadIfNeeded(int) 的参数、LeonCombat.HandleReload() 等共 8 处。</summary>
    private int currentReloadTxId = -1;
    private bool cqbrReloadStartedEmpty;
    private bool reloadFireRequested;
    private bool reloadFireHeld;
    private bool w870TriggerQueued;
    private bool w870ReloadQueued;
    /// <summary>中文含义：是否已绑定事件。使用位置：LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents()。</summary>
    private bool eventsBound;
    /// <summary>中文含义：当前近战动作的事务编号。记录协调器批准的当前近战事务，用于拒绝其他事务的完成、命中和退出回调；没有有效事务时保留既有负值哨兵。使用位置：LeonCombat.OnMeleeActionCompleted(AnimationActionTransaction)、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)、LeonCombat.OnMeleeActionInterrupted(AnimationActionTransaction) 等共 5 处。</summary>
    private int currentMeleeTxId = -1;
    /// <summary>中文含义：近战动作是否仍在执行。为 true 表示已进入的近战动作仍未结束；阻断冲突操作，并决定新的小刀攻击输入是否进入连击缓存。使用位置：LeonCombat.CanBeginParryAction.get、LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleAim(bool) 等共 11 处。</summary>
    private bool meleeActionActive;
    /// <summary>中文含义：是否有等待当前突刺退出后衔接的小刀攻击。当前快速突刺未退出时，再次攻击且允许连击缓存会置为 true；动作正常退出时用它决定是否衔接下一刀并清除；动作中断、组件停用或小刀损坏也会清除。使用位置：LeonCombat.ConsumeKnifeDurability(int)、LeonCombat.HandleKnifeAttack()、LeonCombat.OnDisable() 等共 5 处。</summary>
    private bool queuedKnifeAttack;
    /// <summary>中文含义：切入小刀模式前装备的武器类型。使用位置：LeonCombat.TryToggleKnifeMode()。</summary>
    private WeaponType weaponBeforeKnife = WeaponType.Pistol;
    /// <summary>中文含义：是否缓存了尚未执行的小刀模式切换请求。近战或切枪尚未结束时按切换键会翻转此标记；Update 在动作允许后消费请求并切换模式，停用或强制退出小刀时清除。使用位置：LeonCombat.ForceExitKnifeMode()、LeonCombat.OnDisable()、LeonCombat.ProcessQueuedKnifeToggle() 等共 4 处。</summary>
    private bool queuedKnifeToggle;
    /// <summary>中文含义：当前正在执行的近战动作类型。使用位置：LeonCombat.HandleKnifeAttack()、LeonCombat.IsKnifeAttackActive.get、LeonCombat.IsParrying.get 等共 6 处。</summary>
    private FullBodyActionType currentMeleeAction = FullBodyActionType.None;
    /// <summary>中文含义：待提交近战命中的木箱目标。使用位置：LeonCombat.OnMeleeActionExited(AnimationActionTransaction)、LeonCombat.OnMeleeActionInterrupted(AnimationActionTransaction)、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction) 等共 4 处。</summary>
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
    /// <summary>中文含义：霰弹枪逐发装填使用的换弹事务编号。使用位置：LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)、LeonCombat.IsShotgunShellReloadActive(int) 等共 5 处。</summary>
    private int shotgunShellReloadTxId = -1;
    /// <summary>中文含义：霰弹枪换弹动画是否已完成。使用位置：LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.StopShotgunShellReload(bool) 等共 4 处。</summary>
    private bool shotgunShellReloadAnimationCompleted;
    /// <summary>中文含义：霰弹枪逐发补弹是否已完成。使用位置：LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.ShotgunShellReloadRoutine(int)、LeonCombat.StopShotgunShellReload(bool) 等共 4 处。</summary>
    private bool shotgunShellReloadLoadsCompleted;
    /// <summary>中文含义：招架机会来源。使用位置：LeonCombat.ClearParryState(bool)、LeonCombat.CloseParryOpportunity(VillagerController)、LeonCombat.IsParryPromptVisible.get 等共 5 处。</summary>
    private VillagerController parryOpportunitySource;
    /// <summary>中文含义：是否处于可招架的机会时窗。使用位置：LeonCombat.ClearParryState(bool)、LeonCombat.CloseParryOpportunity(VillagerController)、LeonCombat.HandleParry() 等共 6 处。</summary>
    private bool parryOpportunityOpen;
    /// <summary>中文含义：是否已准备接收本次招架输入。使用位置：LeonCombat.ClearParryState(bool)、LeonCombat.CloseParryOpportunity(VillagerController)、LeonCombat.HandleParry() 等共 7 处。</summary>
    private bool parryInputArmed;
    /// <summary>中文含义：最近一次按下招架键的时间。使用位置：作为 Mathf.Max(float, float) 的参数、LeonCombat.ClearParryState(bool)、LeonCombat.HandleParry() 等共 4 处。</summary>
    private float parryPressedTime;
    /// <summary>中文含义：招架命中顿帧协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、LeonCombat.ParryHitStopRoutine(VillagerController, bool)、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。</summary>
    private Coroutine parryHitStopRoutine;
    /// <summary>中文含义：是否保持防守输入。由防守输入更新；与当前招架和小刀动作共同决定保持防守、恢复枪械姿态等后续处理。使用位置：LeonCombat.CommitGuardInput(bool, double)、LeonCombat.ForceExitKnifeMode()、LeonCombat.HandleAim(bool) 等共 11 处。</summary>
    private bool guardHeld;
    /// <summary>中文含义：开始保持防守的时间。使用位置：LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleParry()。</summary>
    private float guardBeginTime;
    /// <summary>中文含义：瞄准状态变化时发出的通知。事件通信成员 OnAimStateChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonCombat.ForceAimOff()、LeonCombat.HandleAim(bool)。</summary>
    public event System.Action<bool> OnAimStateChanged;
    /// <summary>中文含义：换弹状态变化时发出的通知。事件通信成员 OnReloadingChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonCombat.HandleReload()、LeonCombat.OnReloadAnimExited(AnimationActionTransaction)、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。</summary>
    public event System.Action<bool> OnReloadingChanged;
    /// <summary>中文含义：换弹动作完全退出时发出的通知。事件通信成员 OnReloadFullyExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonCombat.OnReloadAnimExited(AnimationActionTransaction)、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。</summary>
    public event System.Action OnReloadFullyExited;
    /// <summary>中文含义：配对体术命中时发出的通知。事件通信成员 OnQTEBodyTechniqueHit，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonQTECoordinator.BindEvents()、LeonQTECoordinator.UnbindEvents()。</summary>
    public event System.Action<int, FullBodyActionType> OnQTEBodyTechniqueHit;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;KnifeCombatModule&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (knifeModule == null)
        {
            knifeModule = GetComponent<KnifeCombatModule>();
        }

        controller = GetComponent<LeonController>();
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

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.BindCoordinatorEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        BindCoordinatorEvents();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()、LeonCombat.StopShotgunShellReload(bool)、LeonCombat.UnbindCoordinatorEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        guardHeld = false;
        StopParryHitStop();
        RestoreFirearmPose();
        queuedKnifeToggle = false;
        queuedKnifeAttack = false;
        ClearParryState(true);
        StopShotgunShellReload(false);
        UnbindCoordinatorEvents();
    }

    /// <summary>BindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnEnable()、LeonCombat.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

    /// <summary>UnbindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
    /// <param name="aim">中文含义：是否瞄准。使用位置：作为 Action&lt;bool&gt;.Invoke(bool) 的参数、作为 LeonAnimatorDriver.SetAimState(bool) 的参数、LeonCombat.HandleAim(bool)。</param>
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
            // receiverValue1（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleAim(bool)。
global::LeonController receiverValue1 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.TakeOverFreeViewAim();
            }
        }

        if (aim && !isAiming)
        {
            // receiverValue2（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleAim(bool)。
global::LeonController receiverValue2 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.SynchronizeEnterAimAngle();
            }

            if (driver == null || !driver.UsesNativePistolAim)
            {
                aimLayerWeight = 1f;
            }
            // receiverValue3（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleAim(bool)。
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
                // callReceiver6（调用前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.HandleAim(bool)。
global::LeonAnimRig callReceiver6 = animRig;
                // selectedValue5（分支选出的反向动力学模式的瞄准）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIKMode(IKMode) 的参数、LeonCombat.HandleAim(bool)。
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
            // callReceiver8（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.HandleAim(bool)。
global::LeonController callReceiver8 = controller;
            // selectedValue7（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.HandleAim(bool)。
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

        // receiverValue9（判空前缓存的瞄准状态变化时发出的通知）：从瞄准状态变化时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleAim(bool)。
global::System.Action<bool> receiverValue9 = OnAimStateChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.Invoke(aim);
        }
    }

    /// <summary>中文含义：是否正在瞄准。使用位置：KnifeCombatModule.Update()、LeonController.IsAiming.get、LeonController.UpdateAnimatorParameters()。</summary>
    public bool IsAiming => isAiming;
    internal bool HasAimIntent => isAiming || aimWaitingForStand;
    /// <summary>中文含义：是否正在换弹。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    public bool IsReloading => controller != null && controller.currentState == LeonState.Reloading;
    /// <summary>中文含义：是否处于小刀模式。使用位置：作为 LeonAnimatorDriver.SetKnifeMode(bool) 的参数、作为 LeonWeaponVisualController.SetKnifeModeVisible(bool) 的参数、LeonCombat.HandleAim(bool) 等共 7 处。</summary>
    public bool IsKnifeMode => controller != null && controller.currentWeapon == WeaponType.Knife;
    internal bool IsHoldingTemporaryKnife => temporaryKnifeEquipped && IsKnifeMode;
    internal WeaponType TemporaryKnifeReturnWeapon => knifeReturnWeapon;

    /// <summary>中文含义：是否小刀攻击生效。使用位置：LeonController.IsKnifeAttackActive.get、LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonLocomotion.HandleMovement()。</summary>
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

    /// <summary>中文含义：是否招架提示可见。使用位置：CombatHUD.UpdateParryPrompt()、LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleParry()。</summary>
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

    /// <summary>中文含义：是否招架中。</summary>
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

        if (isShootHeld && controller.currentState == LeonState.Shooting
            && (driver == null || !driver.NativeCqbrBlocksFire))
        {
            // receiverValue12（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.Update()。
global::LeonWeaponHandler receiverValue12 = weaponHandler;
            // weapon（当前武器组件）：使用位置：作为 LeonCombat.GetShootLockDuration(WeaponBase) 的参数、LeonCombat.Update()。
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
                // fired（是否已开火）：保存 LeonController.FireWeapon() 的返回结果，供当前步骤使用。使用位置：LeonCombat.Update()。
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
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatModule.CommitAttack(double) 的参数。</param>
    public void CommitAttackInput(double time)
    {
        if (knifeModule != null && knifeModule.Ready)
        {
            knifeModule.CommitAttack(time);
        }
        else
        {
            ExecuteFirearmAttack();
        }
    }

    /// <summary>ExecuteFirearmAttack 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.TriggerFire()、LeonCombat.CanAct()、LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.IsFireBlockedByWeaponAction()、LeonController.ChangeState(LeonState)、LeonController.FireWeapon()、LeonController.GetCurrentAmmo() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()、LeonCombat.CommitAttackInput(double)；下游（含内部回调体） LeonAnimatorDriver.TriggerFire()、LeonCombat.CanAct()、LeonCombat.GetShootLockDuration(WeaponBase)、LeonCombat.IsFireBlockedByWeaponAction()、LeonController.ChangeState(LeonState)、LeonController.FireWeapon()、LeonController.GetCurrentAmmo() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ExecuteFirearmAttack()
    {
        if (controller == null || controller.IsFirearmStanceBlocked || IsFireBlockedByWeaponAction())
        {
            return;
        }

        if (!isAiming || controller.currentWeapon == WeaponType.Knife)
        {
            return;
        }

        if (!CanAct())
        {
            return;
        }

        if (!isAiming && controller.currentState != LeonState.Aiming)
        {
            return;
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
            return;
        }

        if (driver != null && driver.SupportsNativeW870Aim
            && (driver.NativeW870BlocksFire || controller.currentState == LeonState.Shooting))
        {
            w870TriggerQueued = true;
            w870ReloadQueued = false;
            return;
        }

        if (driver != null && driver.UsesNativePistolAim
            && (driver.NativePistolBlocksFire || controller.currentState == LeonState.Shooting))
        {
            pistolTriggerQueued = true;
            return;
        }
        if (controller.currentState == LeonState.Shooting)
        {
            return;
        }

        if (driver != null && driver.NativeCqbrBlocksFire)
        {
            cqbrShotWaitingForAim = true;
            return;
        }
        if (controller.GetCurrentAmmo() <= 0)
        {
            VillageLeonDialogue.Report(controller, VillageSpeechCue.DryFire);
            if (driver != null && !driver.TryPlayNativePistolDryFire() && !driver.TryPlayNativeW870DryFire())
            {
                driver.TryPlayNativeCqbrDryFire();
            }
            // receiverValue14（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ExecuteFirearmAttack()。
global::LeonWeaponHandler receiverValue14 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                // receiverValue15（判空前缓存的取得当前武器的返回结果）：从取得当前武器的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ExecuteFirearmAttack()。
global::WeaponBase receiverValue15 = receiverValue14.GetCurrentWeapon();
                if (!global::System.Object.ReferenceEquals(receiverValue15, null))
                {
                    // receiverValue16（判空前缓存的接收者值15的武器音频引用）：从接收者值15的武器音频引用读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ExecuteFirearmAttack()。
global::WeaponAudio receiverValue16 = receiverValue15.WeaponAudioRef;
                    if (!global::System.Object.ReferenceEquals(receiverValue16, null))
                    {
                        receiverValue16.PlayDryFire();
                    }
                }
            }

            return;
        }

        if (!controller.NewPipelineValid)
        {
            return;
        }

        isShootHeld = true;
        CancelInvoke(nameof(EndShoot));
        controller.ChangeState(LeonState.Shooting);
        // fired（是否已开火）：保存 LeonController.FireWeapon() 的返回结果，供当前步骤使用。使用位置：LeonCombat.ExecuteFirearmAttack()。
bool fired = controller.FireWeapon();
        if (!fired)
        {
            isShootHeld = false;
            // callReceiver18（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.ExecuteFirearmAttack()。
global::LeonController callReceiver18 = controller;
            // selectedValue17（分支选出的里昂状态的瞄准中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.ExecuteFirearmAttack()。
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
            return;
        }

        if (driver != null)
        {
            driver.TriggerFire(controller.GetCurrentAmmo() == 0);
        }

        // receiverValue19（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ExecuteFirearmAttack()。
global::LeonWeaponHandler receiverValue19 = weaponHandler;
        // weapon（当前武器组件）：使用位置：作为 LeonCombat.GetShootLockDuration(WeaponBase) 的参数、LeonCombat.ExecuteFirearmAttack()。
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
    }

    /// <summary>HandleShootRelease 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackReleased(double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） LeonCombat.CommitAttackReleased(double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleShootRelease()
    {
        CommitAttackReleased(Time.timeAsDouble);
    }

    /// <summary>CommitAttackReleased 的业务处理入口；本方法直接依赖 KnifeCombatModule.CommitAttackReleased(double)、LeonCombat.ExecuteFirearmAttackReleased()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleShootRelease()、LeonInputBridge.OnShootReleased(InputAction.CallbackContext)；下游（含内部回调体） KnifeCombatModule.CommitAttackReleased(double)、LeonCombat.ExecuteFirearmAttackReleased()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatModule.CommitAttackReleased(double) 的参数。</param>
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

        // receiverValue21（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.UpdateShootStateLock()。
global::LeonWeaponHandler receiverValue21 = weaponHandler;
        // weapon（当前武器组件）：使用位置：LeonCombat.UpdateShootStateLock()。
global::WeaponBase weapon;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            weapon = receiverValue21.GetCurrentWeapon();
        }
        else
        {
            weapon = default;
        }

        // isAuto（是否自动）：使用位置：LeonCombat.UpdateShootStateLock()。
bool isAuto = weapon != null && weapon.data != null && weapon.data.fireMode == FireMode.Auto;
        if (!isAuto || !isShootHeld || shootReleaseQueued)
        {
            EndShoot();
        }
    }

    /// <summary>GetShootLockDuration 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、WeaponBase.GetFireInterval()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()、LeonCombat.Update()；下游（含内部回调体） Mathf.Max(float, float)、WeaponBase.GetFireInterval()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器组件。使用位置：作为 Mathf.Max(float, float) 的参数、LeonCombat.GetShootLockDuration(WeaponBase)。</param>
    private float GetShootLockDuration(WeaponBase weapon)
    {
        // lockDuration（锁定持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、LeonCombat.GetShootLockDuration(WeaponBase)。
float lockDuration = Mathf.Max(0f, fireInterval);
        if (weapon != null && weapon.data != null)
        {
            lockDuration = Mathf.Max(lockDuration, weapon.GetFireInterval());
            if (weapon.data.fireMode != FireMode.Auto)
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
        CancelInvoke(nameof(EndShoot));
        shootLockedUntil = 0f;
        shootReleaseQueued = false;
        if (controller.currentState == LeonState.Shooting)
        {
            // callReceiver25（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.EndShoot()。
global::LeonController callReceiver25 = controller;
            // selectedValue24（分支选出的里昂状态的瞄准中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.EndShoot()。
global::LeonState selectedValue24;
            if (isAiming)
            {
                selectedValue24 = LeonState.Aiming;
            }
            else
            {
                // selectedValue23（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonCombat.EndShoot()。
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
    }

    /// <summary>ForceStopShoot 的业务处理入口；本方法直接依赖 MonoBehaviour.CancelInvoke(string)、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.BeginTemporaryKnife()、LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleDeath()、LeonCombat.HandleHit(LeonHitDirection)、LeonCombat.HandleKnifeAttack() 等共 14 处；下游（含内部回调体） MonoBehaviour.CancelInvoke(string)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceStopShoot()
    {
        cqbrShotWaitingForAim = false;
        pistolTriggerQueued = false;
        w870TriggerQueued = false;
        w870ReloadQueued = false;
        ClearReloadFireRequest();
        isShootHeld = false;
        shootLockedUntil = 0f;
        shootReleaseQueued = false;
        if (fireRoutine != null)
        {
            StopCoroutine(fireRoutine);
            fireRoutine = null;
        }

        CancelInvoke(nameof(EndShoot));
    }

    /// <summary>InterruptForQTE 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.PrepareForQTE()、LeonInputBridge.EnterQTEInput(int)、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void InterruptForQTE()
    {
        // receiverValue26（判空前缓存的小刀战斗模块）：从小刀战斗模块读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.InterruptForQTE()。
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
        if (knifeModule != null && knifeModule.Active)
        {
            return;
        }

        if (controller.currentWeapon == WeaponType.Knife || meleeActionActive || guardHeld)
        {
            return;
        }

        if (!CanAct())
        {
            return;
        }

        if (controller.currentState == LeonState.Reloading)
        {
            return;
        }

        if (controller.currentState == LeonState.Shooting || driver != null && driver.NativeW870FireCycleActive)
        {
            if (driver != null && driver.SupportsNativeW870Aim && controller.CanReload())
            {
                w870ReloadQueued = true;
                w870TriggerQueued = false;
            }
            return;
        }

        if (!controller.CanReload())
        {
            return;
        }

        if (!controller.NewPipelineValid)
        {
            return;
        }

        // ok（是否成功）：使用位置：LeonCombat.HandleReload()。
bool ok = coordinator.RequestWeaponAction(coordinator.CreateActionRequest("玩家战斗入口", GameArchitecture.ActionKind.Reload), controller.currentWeapon).HasAccept;
        if (!ok)
        {
            return;
        }

        currentReloadTxId = coordinator.CurrentWeaponTransactionId;
        pistolTriggerQueued = false;
        cqbrReloadStartedEmpty = controller.GetCurrentAmmo() == 0;
        controller.ChangeState(LeonState.Reloading);
        controller.StartReload();
        BeginShotgunShellReloadIfNeeded(currentReloadTxId);
        // receiverValue27（判空前缓存的换弹状态变化时发出的通知）：从换弹状态变化时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleReload()。
global::System.Action<bool> receiverValue27 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.Invoke(true);
        }
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
        if (driver == null || !driver.UsesNativePistolAim || !isAiming || !CanAct())
        {
            pistolTriggerQueued = false;
            return;
        }
        if (driver.NativePistolBlocksFire || controller.currentState is LeonState.Shooting or LeonState.Reloading)
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
            HandleReload();
            return;
        }
        if (!isAiming || driver.NativeW870BlocksFire || driver.WeaponActionLayerWeight > 0.05f)
        {
            return;
        }
        var weapon = weaponHandler.GetCurrentWeapon();
        if (weapon == null || weapon.currentAmmo > 0 && !weapon.CanFire())
        {
            return;
        }
        w870TriggerQueued = false;
        ExecuteFirearmAttack();
        ExecuteFirearmAttackReleased();
    }

    /// <summary>OnReloadAnimCompleted 的业务处理入口；本方法直接依赖 Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CompleteReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.CompleteReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">中文含义：动画动作事务。使用位置：作为 Debug.LogError(object) 的参数、作为 LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 的参数、作为 LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction) 的参数 等共 6 处。</param>
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
            // ok（是否成功）：保存 LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 的返回结果，供当前步骤使用。使用位置：LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)。
bool ok = coordinator.CompleteReloadAfterBusinessCommit(tx.transactionId);
            if (!ok)
            {
                Debug.LogError("[LeonCombat] CompleteReloadAfterBusinessCommit 失败: txId=" + tx.transactionId + " currentReloadTxId=" + currentReloadTxId + " txAction=" + tx.weaponActionType + " phase=" + coordinator.CurrentPhase + " hasActiveWeaponTx=" + coordinator.HasActiveWeaponAction + " entrySeq=" + tx.stateEntrySequence + " WeaponAction=unknown");
            }
        }
    }

    /// <summary>OnReloadAnimExited 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.RestoreStateAfterWeaponAction()、LeonCombat.StopShotgunShellReload(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、Action&lt;bool&gt;.Invoke(bool)、LeonCombat.RestoreStateAfterWeaponAction()、LeonCombat.StopShotgunShellReload(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">中文含义：动画动作事务。使用位置：LeonCombat.OnReloadAnimExited(AnimationActionTransaction)。</param>
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
        // receiverValue28（判空前缓存的换弹状态变化时发出的通知）：从换弹状态变化时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnReloadAnimExited(AnimationActionTransaction)。
global::System.Action<bool> receiverValue28 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.Invoke(false);
        }

        if (controller.currentState == LeonState.Reloading)
        {
            RestoreStateAfterWeaponAction();
        }

        // receiverValue29（判空前缓存的换弹动作完全退出时发出的通知）：从换弹动作完全退出时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnReloadAnimExited(AnimationActionTransaction)。
global::System.Action receiverValue29 = OnReloadFullyExited;
        if (!global::System.Object.ReferenceEquals(receiverValue29, null))
        {
            receiverValue29.Invoke();
        }
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
            // receiverValue30（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreStateAfterWeaponAction()。
global::LeonAnimatorDriver receiverValue30 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue30, null))
            {
                receiverValue30.SetAimState(true);
            }

            targetAimLayerWeight = 1f;
            // receiverValue31（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreStateAfterWeaponAction()。
global::LeonAnimRig receiverValue31 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue31, null))
            {
                receiverValue31.SetIKMode(IKMode.Aim);
            }

            return;
        }

        // callReceiver33（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.RestoreStateAfterWeaponAction()。
global::LeonController callReceiver33 = controller;
        // selectedValue32（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.RestoreStateAfterWeaponAction()。
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
    /// <param name="transactionId">中文含义：事务编号。使用位置：作为 LeonCombat.ShotgunShellReloadRoutine(int) 的参数、LeonCombat.BeginShotgunShellReloadIfNeeded(int)。</param>
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
    /// <param name="transactionId">中文含义：事务编号。使用位置：作为 LeonCombat.IsShotgunShellReloadActive(int) 的参数、作为 LeonCombat.TryCompleteShotgunShellReload(int) 的参数、作为 LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int) 的参数。</param>
    private IEnumerator ShotgunShellReloadRoutine(int transactionId)
    {
        // initialDelay（初始延迟）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 WaitForSeconds.WaitForSeconds(float) 的参数、LeonCombat.ShotgunShellReloadRoutine(int)。
float initialDelay = Mathf.Max(0f, shotgunShellLoadInitialDelay);
        if (initialDelay > 0f)
        {
            yield return new WaitForSeconds(initialDelay);
        }

        while (IsShotgunShellReloadActive(transactionId) && weaponHandler != null && weaponHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
        {
            weaponHandler.LoadOneRoundFromReserve(WeaponType.Shotgun, transactionId);
            if (!weaponHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
            {
                break;
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, shotgunShellLoadInterval));
        }

        shotgunShellReloadLoadsCompleted = true;
        shotgunShellReloadRoutine = null;
        TryCompleteShotgunShellReload(transactionId);
    }

    /// <summary>IsShotgunShellReloadActive 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ShotgunShellReloadRoutine(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：LeonCombat.IsShotgunShellReloadActive(int)。</param>
    private bool IsShotgunShellReloadActive(int transactionId)
    {
        return transactionId == currentReloadTxId && transactionId == shotgunShellReloadTxId && controller != null && controller.currentWeapon == WeaponType.Shotgun && controller.currentState == LeonState.Reloading;
    }

    /// <summary>IsShotgunReloadTransaction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">中文含义：动画动作事务。使用位置：LeonCombat.IsShotgunReloadTransaction(AnimationActionTransaction)。</param>
    internal bool IsShotgunReloadTransaction(AnimationActionTransaction tx)
    {
        return shotgunShellByShellReload && tx.weaponActionType == WeaponActionType.Reload && tx.transactionId == shotgunShellReloadTxId && tx.sourceWeaponType == WeaponType.Shotgun;
    }

    /// <summary>TryCompleteShotgunShellReload 的业务处理入口；本方法直接依赖 Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonWeaponHandler.CancelReload(WeaponType)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.ShotgunShellReloadRoutine(int)；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonWeaponHandler.CancelReload(WeaponType)、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：作为 Debug.LogError(object) 的参数、作为 LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 的参数、LeonCombat.TryCompleteShotgunShellReload(int)。</param>
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

        // receiverValue34（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.TryCompleteShotgunShellReload(int)。
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

        // ok（是否成功）：保存 LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 的返回结果，供当前步骤使用。使用位置：LeonCombat.TryCompleteShotgunShellReload(int)。
bool ok = coordinator.CompleteReloadAfterBusinessCommit(transactionId);
        if (!ok)
        {
            Debug.LogError("[LeonCombat] 霰弹枪逐发装填提交失败: txId=" + transactionId + " currentReloadTxId=" + currentReloadTxId + " phase=" + coordinator.CurrentPhase + " hasActiveWeaponTx=" + coordinator.HasActiveWeaponAction);
        }
    }

    /// <summary>StopShotgunShellReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CancelReload(WeaponType)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.BeginShotgunShellReloadIfNeeded(int)、LeonCombat.OnDisable()、LeonCombat.OnReloadAnimExited(AnimationActionTransaction)、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)；下游（含内部回调体） LeonWeaponHandler.CancelReload(WeaponType)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="cancelWeaponReload">中文含义：是否取消武器换弹。使用位置：LeonCombat.StopShotgunShellReload(bool)。</param>
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
            // receiverValue35（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.StopShotgunShellReload(bool)。
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
        // receiverValue37（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleParry()。
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
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：LeonCombat.OpenParryOpportunity(VillagerController)。</param>
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
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：LeonCombat.CloseParryOpportunity(VillagerController)。</param>
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
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：作为 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult) 的参数、LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)。</param>
    /// <param name="contactPoint">中文含义：接触点。使用位置：作为 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult) 的参数。</param>
    /// <param name="attackDirection">中文含义：攻击方向。使用位置：作为 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult) 的参数。</param>
    public KnifeParryResult TryResolveParry(VillagerController source, Vector3 contactPoint, Vector3 attackDirection)
    {
        if (!parryInputArmed || source == null || source != parryOpportunitySource || (!guardHeld && Time.time - parryPressedTime > parryActiveDuration) || controller == null || controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || controller.currentState == LeonState.QTE)
        {
            return KnifeParryResult.None;
        }

        // inputAge（输入已存在时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)。
float inputAge = Mathf.Max(0f, Time.time - parryPressedTime);
        // result（当前小刀招架结果）：使用位置：作为 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult) 的参数、LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)。
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

    /// <summary>中文含义：是否可以开始招架动作。使用位置：LeonCombat.CommitGuardInput(bool, double)、LeonCombat.IsParryPromptVisible.get、LeonCombat.OpenParryOpportunity(VillagerController)。</summary>
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
        // enteringKnifeMode（是否进入中小刀模式）：使用位置：LeonCombat.TryToggleKnifeMode()。
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

        // targetWeapon（目标武器）：使用位置：作为 Debug.LogWarning(object) 的参数、作为 LeonWeaponHandler.SwitchTo(WeaponType) 的参数、LeonCombat.TryToggleKnifeMode()。
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
    /// <param name="held">中文含义：是否保持按住。使用位置：作为 LeonCombat.CommitGuardInput(bool, double) 的参数。</param>
    public void HandleKnifeHold(bool held)
    {
        CommitGuardInput(held, Time.timeAsDouble);
    }

    /// <summary>CommitGuardInput 的业务处理入口；本方法直接依赖 KnifeCombatModule.CommitGuard(bool, double)、LeonCombat.ApplyGuardPose()、LeonCombat.CanAct()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.HandleParry() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleKnifeHold(bool)、LeonInputBridge.OnParry(InputAction.CallbackContext)；下游（含内部回调体） KnifeCombatModule.CommitGuard(bool, double)、LeonCombat.ApplyGuardPose()、LeonCombat.CanAct()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、LeonCombat.HandleParry() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="held">中文含义：是否保持按住。使用位置：作为 KnifeCombatModule.CommitGuard(bool, double) 的参数、LeonCombat.CommitGuardInput(bool, double)。</param>
    /// <param name="timeValue">中文含义：时间值。使用位置：作为 KnifeCombatModule.CommitGuard(bool, double) 的参数。</param>
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
        // receiverValue41（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ApplyGuardPose()。
global::LeonAnimatorDriver receiverValue41 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue41, null))
        {
            receiverValue41.SetKnifeMode(true);
        }

        // receiverValue42（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ApplyGuardPose()。
global::LeonAnimRig receiverValue42 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            receiverValue42.SetWeapon(WeaponType.Knife);
        }

        // receiverValue43（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ApplyGuardPose()。
global::LeonAnimRig receiverValue43 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue43, null))
        {
            receiverValue43.EndKnifeActionIK();
        }

        // receiverValue44（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ApplyGuardPose()。
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

        // receiverValue45（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreFirearmPose()。
global::LeonAnimatorDriver receiverValue45 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.SetKnifeMode(IsKnifeMode);
        }

        if (!IsKnifeMode)
        {
            // receiverValue46（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreFirearmPose()。
global::LeonAnimatorDriver receiverValue46 = driver;
            // optionalValue47（条件访问得到的集合武器类型的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonCombat.RestoreFirearmPose()。
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

        // receiverValue48（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreFirearmPose()。
global::LeonAnimRig receiverValue48 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue48, null))
        {
            receiverValue48.SetWeapon(controller.currentWeapon);
        }

        // receiverValue49（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreFirearmPose()。
global::LeonAnimRig receiverValue49 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.EndKnifeActionIK();
        }

        // receiverValue50（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.RestoreFirearmPose()。
global::LeonWeaponVisualController receiverValue50 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.SetKnifeModeVisible(IsKnifeMode);
        }

        if (controller.currentState == LeonState.Parrying)
        {
            // callReceiver52（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.RestoreFirearmPose()。
global::LeonController callReceiver52 = controller;
            // selectedValue51（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.RestoreFirearmPose()。
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
    /// <param name="action">中文含义：动作。使用位置：作为 LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int) 的参数、LeonCombat.StartMeleeAction(FullBodyActionType, int)。</param>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int) 的参数。</param>
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
            // receiverValue53（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.StartMeleeAction(FullBodyActionType, int)。
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
    /// <param name="techniqueIndex">中文含义：体术索引。使用位置：LeonCombat.TryStartQTEBodyTechnique(int, int)。</param>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 LeonCombat.StartMeleeAction(FullBodyActionType, int) 的参数、LeonCombat.TryStartQTEBodyTechnique(int, int)。</param>
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

        // action（动作）：使用位置：作为 LeonCombat.StartMeleeAction(FullBodyActionType, int) 的参数、LeonCombat.TryStartQTEBodyTechnique(int, int)。
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
    /// <param name="tx">中文含义：动画动作事务。使用位置：LeonCombat.OnMeleeActionCompleted(AnimationActionTransaction)。</param>
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
    /// <param name="tx">中文含义：动画动作事务。使用位置：LeonCombat.OnMeleeActionExited(AnimationActionTransaction)。</param>
    private void OnMeleeActionExited(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        // wasParry（刚退出或中断的动作是否为小刀招架）：根据退出或中断事务的动作类型计算；用于决定是否清理本次招架状态。使用位置：LeonCombat.OnMeleeActionExited(AnimationActionTransaction)。
bool wasParry = tx.fullBodyActionType == FullBodyActionType.KnifeParry;
        // restartKnifeAttack（当前动作退出后是否立即衔接下一次小刀攻击）：只有刚退出的是快速突刺、已有排队攻击，且装备组件确认小刀仍可用时才为 true；退出清理完成后据此启动下一次突刺。使用位置：LeonCombat.OnMeleeActionExited(AnimationActionTransaction)。
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
            // callReceiver56（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.OnMeleeActionExited(AnimationActionTransaction)。
global::LeonController callReceiver56 = controller;
            // selectedValue55（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)。
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
    }

    /// <summary>OnMeleeActionInterrupted 的业务处理入口；本方法直接依赖 LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ClearParryState(bool)、LeonCombat.RestoreFirearmPose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">中文含义：动画动作事务。使用位置：LeonCombat.OnMeleeActionInterrupted(AnimationActionTransaction)。</param>
    private void OnMeleeActionInterrupted(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        // wasParry（刚退出或中断的动作是否为小刀招架）：根据退出或中断事务的动作类型计算；用于决定是否清理本次招架状态。使用位置：LeonCombat.OnMeleeActionInterrupted(AnimationActionTransaction)。
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
    /// <param name="tx">中文含义：动画动作事务。使用位置：作为 Action&lt;int, FullBodyActionType&gt;.Invoke(int, FullBodyActionType) 的参数、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)。</param>
    private void OnMeleeHitWindow(AnimationActionTransaction tx)
    {
        if (tx.transactionId != currentMeleeTxId)
        {
            return;
        }

        if (tx.fullBodyActionType == FullBodyActionType.KnifeQuickAttack)
        {
            // receiverValue57（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)。
global::LeonWeaponHandler receiverValue57 = weaponHandler;
            // knife（小刀）：使用位置：LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)。
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
        // receiverValue62（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ForceAimOff()。
global::LeonAnimatorDriver receiverValue62 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue62, null))
        {
            receiverValue62.SetAimState(false);
        }

        // receiverValue63（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ForceAimOff()。
global::LeonAnimatorDriver receiverValue63 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue63, null))
        {
            receiverValue63.SetAimLayerWeight(0f);
        }

        // receiverValue64（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ForceAimOff()。
global::LeonAnimRig receiverValue64 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue64, null))
        {
            receiverValue64.SetIKMode(IKMode.None);
        }

        // receiverValue65（判空前缓存的瞄准状态变化时发出的通知）：从瞄准状态变化时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ForceAimOff()。
global::System.Action<bool> receiverValue65 = OnAimStateChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue65, null))
        {
            receiverValue65.Invoke(false);
        }
    }

    /// <summary>ConsumeKnifeDurability 的业务处理入口；本方法直接依赖 InventoryManager.TryConsumeDurability(string, int, out bool)、LeonWeaponHandler.HandleKnifeBroken()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonCombat.TryResolveParry(VillagerController, Vector3, Vector3)；下游（含内部回调体） InventoryManager.TryConsumeDurability(string, int, out bool)、LeonWeaponHandler.HandleKnifeBroken()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">中文含义：大小。使用位置：作为 InventoryManager.TryConsumeDurability(string, int, out bool) 的参数。</param>
    private void ConsumeKnifeDurability(int amount)
    {
        // inventoryManager（背包数据与操作宿主）：使用位置：LeonCombat.ConsumeKnifeDurability(int)。
InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            return;
        }

        if (inventoryManager.TryConsumeKnife(inventoryManager.GetPreferredKnife(), amount, out bool /* 中文含义：是否损坏。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ broken) && broken)
        {
            queuedKnifeAttack = false;
            // receiverValue66（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.ConsumeKnifeDurability(int)。
global::LeonWeaponHandler receiverValue66 = weaponHandler;
            if (!global::System.Object.ReferenceEquals(receiverValue66, null))
            {
                receiverValue66.HandleKnifeBroken();
            }
        }
    }

    /// <summary>ClearParryState 的业务处理入口；本方法直接依赖 LeonWeaponVisualController.SetKnifeModeVisible(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.HandleDeath()、LeonCombat.HandleHit(LeonHitDirection)、LeonCombat.HandleParry()、LeonCombat.InterruptForQTE() 等共 8 处；下游（含内部回调体） LeonWeaponVisualController.SetKnifeModeVisible(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreWeaponVisual">中文含义：是否恢复武器视觉表现。使用位置：LeonCombat.ClearParryState(bool)。</param>
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
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：作为 LeonCombat.ParryHitStopRoutine(VillagerController, bool) 的参数。</param>
    /// <param name="contactPoint">中文含义：接触点。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。</param>
    /// <param name="attackDirection">中文含义：攻击方向。使用位置：作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数。</param>
    /// <param name="result">中文含义：当前小刀招架结果。使用位置：LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。</param>
    internal void PresentChainsawDefense(VillagerController source, Vector3 contactPoint, Vector3 attackDirection)
    {
        PlayParryFeedback(source, contactPoint, attackDirection, KnifeParryResult.Normal);
    }

    private void PlayParryFeedback(VillagerController source, Vector3 contactPoint, Vector3 attackDirection, KnifeParryResult result)
    {
        // perfect（是否完美）：使用位置：作为 LeonCombat.ParryHitStopRoutine(VillagerController, bool) 的参数、作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
bool perfect = result == KnifeParryResult.Perfect;
        // playerContact（玩家接触）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
Vector3 playerContact = transform.position + Vector3.up * 1.15f + transform.forward * 0.55f;
        // resolvedContact（解算后的接触）：使用位置：作为 AudioManager.Play3D(string, Vector3) 的参数、作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
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
        // receiverValue68（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
global::AudioManager receiverValue68 = AudioManager.Instance;
        // optionalValue69（条件访问得到的播放3距离的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
global::AudioPlayHandle? optionalValue69;
        if (!global::System.Object.ReferenceEquals(receiverValue68, null))
        {
            optionalValue69 = receiverValue68.Play3D(AudioEventNames.ChainsawHit, resolvedContact);
        }
        else
        {
            optionalValue69 = default;
        }

        // receiverValue70（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
global::LeonController receiverValue70 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue70, null))
        {
            // receiverValue71（判空前缓存的接收者值70的摄像机控制器）：从接收者值70的摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
global::RE4CameraController receiverValue71 = receiverValue70.CameraController;
            if (!global::System.Object.ReferenceEquals(receiverValue71, null))
            {
                // argumentValue74（按原顺序缓存的实参0.16f）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 RE4CameraController.Shake(float, float) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
float argumentValue74;
                if (perfect)
                {
                    argumentValue74 = 0.16f;
                }
                else
                {
                    argumentValue74 = 0.1f;
                }

                // selectedValue73（分支选出的2.2f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 RE4CameraController.Shake(float, float) 的参数、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)。
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
            yield return new WaitForSecondsRealtime(duration);
        }
        RestoreParryPlayerSpeed();
        parryHitStopRoutine = null;
    }

    /// <summary>SpawnParrySparks 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、ParticleSystem.Burst.Burst(float, short)、ParticleSystem.EmissionModule.SetBursts(ParticleSystem.Burst[]) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、LeonCombat.PresentKnifeSparks(Vector3, Vector3, bool)；下游（含内部回调体） GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、ParticleSystem.Burst.Burst(float, short)、ParticleSystem.EmissionModule.SetBursts(ParticleSystem.Burst[]) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion) 的参数。</param>
    /// <param name="attackDirection">中文含义：攻击方向。使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。</param>
    /// <param name="perfect">中文含义：是否完美。使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。</param>
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

        // direction（方向）：使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
Vector3 direction = Vector3.up;
        if (attackDirection.sqrMagnitude > .001f)
        {
            direction = -attackDirection.normalized;
        }

        // rotation（旋转）：保存 Quaternion.LookRotation(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform) 的参数、作为 Object.Instantiate<GameObject>(GameObject, Vector3, Quaternion) 的参数。
Quaternion rotation = Quaternion.LookRotation(direction);
        // pool（对象池）：使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
GameObjectPoolManager pool = GameObjectPoolManager.Instance;
        // effectObject（效果对象）：使用位置：作为 GameObjectPoolManager.ReturnDelayed(GameObject, float) 的参数、作为 Object.Destroy(Object, float) 的参数、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
GameObject effectObject;
        if (pool != null)
        {
            effectObject = pool.Get(knifeParrySparkPrefab, position, rotation);
        }
        else
        {
            effectObject = Instantiate(knifeParrySparkPrefab, position, rotation);
        }

        if (effectObject == null)
        {
            return;
        }

        // particles（粒子集合）：保存 GameObject.GetComponent<ParticleSystem>() 的返回结果，供当前步骤使用。使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
ParticleSystem particles = effectObject.GetComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        // main（主要）：使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
ParticleSystem.MainModule main = particles.main;
        // shape（形状）：使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
ParticleSystem.ShapeModule shape = particles.shape;
        // count（数量）：使用位置：作为 ParticleSystem.Burst.Burst(float, short) 的参数、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
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

        // emission（发射）：使用位置：LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。
ParticleSystem.EmissionModule emission = particles.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
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
    /// <param name="hitDirection">中文含义：命中方向。使用位置：作为 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection) 的参数。</param>
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
        // ok（是否成功）：保存 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection) 的返回结果，供当前步骤使用。使用位置：LeonCombat.HandleHit(LeonHitDirection)。
bool ok = coordinator.RequestHit(hitDirection);
        if (!ok)
        {
            return false;
        }

        // receiverValue80（判空前缓存的小刀战斗模块）：从小刀战斗模块读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleHit(LeonHitDirection)。
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
    /// <param name="tx">中文含义：动画动作事务。</param>
    private void OnHitAnimCompleted(AnimationActionTransaction tx)
    {
        // 受击唯一完成入口：由 FullBodyActionSMB → Coordinator 触发
        if (controller.currentState == LeonState.Dead)
        {
            return;
        }

        // callReceiver82（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.OnHitAnimCompleted(AnimationActionTransaction)。
global::LeonController callReceiver82 = controller;
        // selectedValue81（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.OnHitAnimCompleted(AnimationActionTransaction)。
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
    }

    // ==================== 死亡 ====================
    /// <summary>HandleDeath 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestDeath()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.BeginDeathSequence()；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)、LeonAnimationActionCoordinator.RequestDeath()、LeonCombat.ClearParryState(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceExitKnifeMode()、LeonCombat.ForceStopShoot()、LeonController.ChangeState(LeonState) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleDeath()
    {
        // receiverValue83（判空前缓存的小刀战斗模块）：从小刀战斗模块读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.HandleDeath()。
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
    /// <param name="tx">中文含义：动画动作事务。使用位置：作为 LeonWeaponHandler.CancelReload(WeaponType) 的参数、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。</param>
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
        // receiverValue84（判空前缓存的管理当前装备与武器切换的组件）：从管理当前装备与武器切换的组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。
global::LeonWeaponHandler receiverValue84 = weaponHandler;
        if (!global::System.Object.ReferenceEquals(receiverValue84, null))
        {
            receiverValue84.CancelReload(tx.sourceWeaponType);
        }

        currentReloadTxId = -1;
        // receiverValue85（判空前缓存的换弹状态变化时发出的通知）：从换弹状态变化时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。
global::System.Action<bool> receiverValue85 = OnReloadingChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue85, null))
        {
            receiverValue85.Invoke(false);
        }

        // receiverValue86（判空前缓存的换弹动作完全退出时发出的通知）：从换弹动作完全退出时发出的通知读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)。
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
        if (controller == null || coordinator == null || !coordinator.isActiveAndEnabled)
        {
            return false;
        }

        return controller.currentState != LeonState.Staggered && controller.currentState != LeonState.Dead && controller.currentState != LeonState.QTE && controller.currentState != LeonState.MeleeAttacking && controller.currentState != LeonState.Traversing;
    }

    /// <summary>BeginTemporaryKnife 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.PrepareTransaction(bool)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonCombat.ForceAimOff()、LeonCombat.ForceStopShoot()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
            || coordinator.HasActiveFullBodyAction || driver != null && driver.IsBlendingOutKnifePose)
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
        // receiverValue87（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.BeginTemporaryKnife()。
global::LeonAnimatorDriver receiverValue87 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue87, null))
        {
            receiverValue87.SetKnifeMode(false);
        }
    }

    /// <summary>EndTemporaryKnife 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonCombat.HandleAim(bool)、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.EndTransaction(bool)；下游（含内部回调体） LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.EndKnifeActionIK()、LeonAnimRig.SetWeapon(WeaponType)、LeonCombat.HandleAim(bool)、LeonController.ChangeState(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreAim">中文含义：是否恢复瞄准。使用位置：LeonCombat.EndTemporaryKnife(bool)。</param>
    public void EndTemporaryKnife(bool restoreAim, bool afterThrust = false)
    {
        // receiverValue88（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.EndTemporaryKnife(bool)。
global::LeonAnimatorDriver receiverValue88 = driver;
        if (temporaryKnifeEquipped && IsKnifeMode)
        {
            knifeReturnAt = Time.timeAsDouble;
            if (!afterThrust && weaponHandler != null && weaponHandler.HasUsableKnife())
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

        // receiverValue89（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.EndTemporaryKnife(bool)。
global::LeonAnimRig receiverValue89 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue89, null))
        {
            receiverValue89.SetWeapon(controller.currentWeapon);
        }

        // receiverValue90（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonCombat.EndTemporaryKnife(bool)。
global::LeonAnimRig receiverValue90 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue90, null))
        {
            receiverValue90.EndKnifeActionIK();
        }

        if (controller.currentState == LeonState.MeleeAttacking || controller.currentState == LeonState.Parrying)
        {
            // callReceiver92（调用前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonCombat.EndTemporaryKnife(bool)。
global::LeonController callReceiver92 = controller;
            // selectedValue91（分支选出的里昂状态的行走中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、LeonCombat.EndTemporaryKnife(bool)。
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
    }

    /// <summary>PresentKnifeSparks 的业务处理入口；本方法直接依赖 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数。</param>
    /// <param name="perfect">中文含义：是否完美。使用位置：作为 LeonCombat.SpawnParrySparks(Vector3, Vector3, bool) 的参数。</param>
    public void PresentKnifeSparks(Vector3 position, Vector3 direction, bool perfect)
    {
        SpawnParrySparks(position, direction, perfect);
    }

    /// <summary>IsFireBlockedByWeaponAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CommitGuardInput(bool, double)、LeonCombat.ExecuteFirearmAttack()、LeonCombat.HandleKnifeAttack()、LeonCombat.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFireBlockedByWeaponAction()
    {
        if (weaponHandler != null && weaponHandler.IsFireBlockedByWeaponAction)
        {
            return true;
        }

        return coordinator != null && (coordinator.HasPendingWeaponSwitch || coordinator.WaitingForWeaponExit || coordinator.IsWeaponSwitchInProgress);
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.TryToggleKnifeMode()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonCombat.IsGunWeapon(WeaponType)。</param>
    private static bool IsGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }
}
