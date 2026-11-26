using System;
using System.Collections;
using BehaviorDesigner.Runtime;
using GameArchitecture;
using UnityEngine;

/// <summary>
/// 村民配对动作参与者。
/// 仅负责敌人侧会话锁、人工智能与寻路暂停、动画层写入和资源恢复。
/// </summary>
[DisallowMultipleComponent]
public partial class VillagerQTEParticipant : MonoBehaviour, IEnemyQTEParticipant
{
    private ActionHandle consumedNativeBodyReaction;
    private bool UsesNativeBodyTechnique => controller != null && controller.combatModule != null
        && controller.combatModule.UsesNativeBodyTechnique;

    /// <summary>配对动作图层名称。编译期常量，保持既有值与消费者映射。</summary>
    private const string QTELayerName = "QTE Override";
    /// <summary>体术入口标签。编译期常量，保持既有值与消费者映射。</summary>
    private const string BodyTechniqueEntryTag = "BodyTechniqueEntry";
    /// <summary>配对动作动作的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashQTEAction = Animator.StringToHash("QTEAction");
    /// <summary>配对动作进入状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashQTEEnterState = Animator.StringToHash("QTEEnter");
    /// <summary>攻击的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashATK = Animator.StringToHash("ATK");
    /// <summary>是否爬行的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashIsCrawl = Animator.StringToHash("IsCrawl");
    /// <summary>命中的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashHIT = Animator.StringToHash("HIT");
    /// <summary>死亡的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashDIE = Animator.StringToHash("DIE");
    /// <summary>硬直方向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashStaggerDirection = Animator.StringToHash("StaggerDirection");
    /// <summary>村民敌人控制器。</summary>
    [Header("组件引用")]
    public VillagerController controller;
    /// <summary>动画控制器。</summary>
    public Animator animator;
    /// <summary>寻路代理。</summary>
    public PathAgent pathAgent;
    /// <summary>行为树。</summary>
    public BehaviorTree behaviorTree;
    /// <summary>身体。</summary>
    public Rigidbody body;
    /// <summary>参与者碰撞体集合。</summary>
    [Header("碰撞体")]
    public Collider[] participantColliders;
    /// <summary>中文含义：动画变体集合。</summary>
    [Header("快速反应动画变体")]
    public QTEAnimationVariant[] animationVariants;
    /// <summary>配对动作入口图层混合时长。</summary>
    [Header("快速反应动画接管")]
    [Min(0.01f)]
    public float qteEntryLayerBlendDuration = 0.1f;
    /// <summary>是否只允许独立测试场景启用，生产预制件必须保持关闭。只允许独立测试场景启用，生产预制件必须保持关闭。</summary>
    [Header("独立测试场景")]
    [Tooltip("只允许独立测试场景启用，生产预制件必须保持关闭")]
    public bool keepAIDisabledForIsolatedTest;
    /// <summary>正面抓取策略距离。</summary>
    [Header("行为树抓取策略")]
    [Min(0.1f)]
    public float frontGrabStrategyDistance = 1.25f;
    /// <summary>正面抓取策略朝向角度。</summary>
    [Range(0f, 180f)]
    public float frontGrabStrategyFacingAngle = 27f;
    /// <summary>正面抓取概率。</summary>
    [Range(0f, 1f)]
    public float frontGrabProbability = 0.10f;
    /// <summary>后侧抓取策略距离。</summary>
    [Min(0.1f)]
    public float rearGrabStrategyDistance = 1.3f;
    /// <summary>后侧抓取策略朝向角度。</summary>
    [Range(0f, 180f)]
    public float rearGrabStrategyFacingAngle = 23f;
    /// <summary>背后擒抱每次策略决策的触发概率。</summary>
    [Tooltip("背后擒抱每次策略决策的触发概率")]
    [Range(0f, 1f)]
    public float rearGrabProbability = 0.18f;
    /// <summary>策略决策间隔。</summary>
    [Min(0.1f)]
    public float strategyDecisionInterval = 0.75f;
    /// <summary>成功冷却。</summary>
    [Header("抓取冷却")]
    [Min(0f)]
    public float successCooldown = 8f;
    /// <summary>失败冷却。</summary>
    [Min(0f)]
    public float failureCooldown = 10f;
    /// <summary>已取消冷却。</summary>
    [Min(0f)]
    public float cancelledCooldown = 3f;
    /// <summary>拒绝重试延迟。</summary>
    [Min(0f)]
    public float rejectionRetryDelay = 0.75f;
    /// <summary>全局忙碌重试延迟。</summary>
    [Min(0f)]
    public float globalBusyRetryDelay = 0.4f;
    /// <summary>腿部撕咬策略距离。</summary>
    [Header("倒地咬腿策略")]
    [Min(0.1f)]
    public float legBiteStrategyDistance = 2.1f;
    /// <summary>腿部撕咬成功冷却。</summary>
    [Min(0f)]
    public float legBiteSuccessCooldown = 6f;
    /// <summary>腿部撕咬失败冷却。</summary>
    [Min(0f)]
    public float legBiteFailureCooldown = 4f;
    /// <summary>腿部撕咬已取消冷却。</summary>
    [Min(0f)]
    public float legBiteCancelledCooldown = 2f;
    /// <summary>是否启用电锯执行。</summary>
    [Header("电锯处决策略")]
    public bool enableChainsawExecution;
    /// <summary>电锯执行策略距离。</summary>
    [Min(0.1f)]
    public float chainsawExecutionStrategyDistance = 2.05f;
    /// <summary>电锯执行策略朝向角度。</summary>
    [Range(0f, 180f)]
    public float chainsawExecutionStrategyFacingAngle = 50f;
    /// <summary>电锯执行常规生命概率。</summary>
    [Range(0f, 1f)]
    public float chainsawExecutionNormalHealthProbability = 0.60f;
    /// <summary>电锯执行低生命阈值。</summary>
    [Range(0.05f, 0.95f)]
    public float chainsawExecutionLowHealthThreshold = 0.35f;
    /// <summary>电锯执行低生命概率。</summary>
    [Range(0f, 1f)]
    public float chainsawExecutionLowHealthProbability = 0.85f;
    /// <summary>电锯执行概率重试延迟。</summary>
    [Min(0.1f)]
    public float chainsawExecutionProbabilityRetryDelay = 0.8f;
    /// <summary>电锯执行成功冷却。</summary>
    [Min(0f)]
    public float chainsawExecutionSuccessCooldown = 8f;
    /// <summary>电锯执行失败冷却。</summary>
    [Min(0f)]
    public float chainsawExecutionFailureCooldown = 12f;
    /// <summary>电锯执行已取消冷却。</summary>
    [Min(0f)]
    public float chainsawExecutionCancelledCooldown = 4f;
    /// <summary>是否启用体术机会。</summary>
    [Header("体术硬直")]
    public bool enableBodyTechniqueOpportunity = true;
    /// <summary>体术提示距离。</summary>
    [Min(0.1f)]
    public float bodyTechniquePromptDistance = 2.6f;
    /// <summary>体术玩家朝向角度。</summary>
    [Range(0f, 180f)]
    public float bodyTechniquePlayerFacingAngle = 110f;
    /// <summary>体术敌人朝向角度。</summary>
    [Range(0f, 180f)]
    public float bodyTechniqueEnemyFacingAngle = 165f;
    /// <summary>体术伤害。</summary>
    [Min(0f)]
    public float bodyTechniqueDamage = 42f;
    /// <summary>旋转中挂钩伤害倍率。</summary>
    [Min(0f)]
    public float spinningHookDamageMultiplier = 1.15f;
    /// <summary>体术冷却时间。</summary>
    [Min(0f)]
    public float bodyTechniqueCooldown = 0.6f;
    /// <summary>体术打开归一化时间。</summary>
    [Range(0.05f, 0.95f)]
    public float bodyTechniqueOpenNormalizedTime = 0.42f;
    /// <summary>体术关闭归一化时间。</summary>
    [Range(0.05f, 1f)]
    public float bodyTechniqueCloseNormalizedTime = 0.78f;
    /// <summary>体术最小窗口持续时间。</summary>
    [Min(0.05f)]
    public float bodyTechniqueMinimumWindowDuration = 0.35f;
    /// <summary>身体硬直体术打开归一化时间。</summary>
    [Header("站立硬直体术入口")]
    [Range(0.05f, 0.95f)]
    public float staggerBodyTechniqueOpenNormalizedTime = 0.12f;
    /// <summary>身体硬直体术关闭归一化时间。</summary>
    [Range(0.05f, 1f)]
    public float staggerBodyTechniqueCloseNormalizedTime = 0.9f;
    /// <summary>是否启用体术飞溅伤害。</summary>
    [Header("体术范围波及")]
    public bool enableBodyTechniqueSplashDamage = true;
    /// <summary>体术飞溅半径。</summary>
    [Min(0.1f)]
    public float bodyTechniqueSplashRadius = 2.35f;
    /// <summary>体术飞溅角度。</summary>
    [Range(0f, 180f)]
    public float bodyTechniqueSplashAngle = 150f;
    /// <summary>体术飞溅伤害倍率。</summary>
    [Min(0f)]
    public float bodyTechniqueSplashDamageMultiplier = 0.55f;
    /// <summary>体术飞溅最大目标数。</summary>
    [Min(0)]
    public int bodyTechniqueSplashMaxTargets = 4;
    /// <summary>是否启用体术击退。</summary>
    [Header("体术击退")]
    public bool enableBodyTechniqueKnockback = true;
    /// <summary>体术击退距离。</summary>
    [Min(0f)]
    public float bodyTechniqueKnockbackDistance = 0.9f;
    /// <summary>体术击退持续时间。</summary>
    [Min(0.05f)]
    public float bodyTechniqueKnockbackDuration = 0.28f;
    /// <summary>体术飞溅击退距离。</summary>
    [Min(0f)]
    public float bodyTechniqueSplashKnockbackDistance = 0.55f;
    /// <summary>体术飞溅击退持续时间。</summary>
    [Min(0.05f)]
    public float bodyTechniqueSplashKnockbackDuration = 0.22f;
    /// <summary>配对动作图层索引。</summary>
    private int qteLayerIndex = -1;
    /// <summary>活动会话编号。</summary>
    private int activeSessionId;
    /// <summary>当前动作。</summary>
    private EnemyQTEAction currentAction;
    /// <summary>退出中的动作。</summary>
    private EnemyQTEAction outgoingAction;
    /// <summary>当前进入序号。</summary>
    private int currentEntrySequence;
    /// <summary>退出中的进入序号。</summary>
    private int outgoingEntrySequence;
    /// <summary>入口是否已绑定。</summary>
    private bool entryBound;
    /// <summary>动作是否已完成。</summary>
    private bool actionCompleted;
    /// <summary>结束是否已经申请。</summary>
    private bool endRequested;
    /// <summary>是否已释放。</summary>
    private bool released = true;
    /// <summary>是否保留暗杀死亡姿态。</summary>
    private bool preserveAssassinationDeathPose;
    /// <summary>是否已开始。</summary>
    private bool started;
    /// <summary>是否停用。</summary>
    private bool isDeactivate;
    /// <summary>是否可以恢复智能。</summary>
    private bool CanRestoreIntelligence => !isDeactivate && Application.isPlaying && isActiveAndEnabled && gameObject.scene.isLoaded && controller != null && controller.isActiveAndEnabled && !controller.IsDead && BehaviorManager.instance != null && BehaviorManager.instance.isActiveAndEnabled;

    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>行为之前是否启用。</summary>
    private bool behaviorWasEnabled;
    /// <summary>寻路代理之前是否启用。</summary>
    private bool pathAgentWasEnabled;
    /// <summary>寻路代理之前是否已停止。</summary>
    private bool pathAgentWasStopped;
    /// <summary>是否动画控制器应用根运动。</summary>
    private bool animatorApplyRootMotion;
    /// <summary>配对动作动画控制器速度。</summary>
    private float qteAnimatorSpeed = 1f;
    /// <summary>配对动作动画图层权重。</summary>
    private float qteLayerWeight;
    /// <summary>移动模式。</summary>
    private EnemyMovementMode movementMode;
    /// <summary>身体之前是否运动学。</summary>
    private bool bodyWasKinematic;
    /// <summary>是否身体已使用的重力。</summary>
    private bool bodyUsedGravity;
    /// <summary>身体约束集合。</summary>
    private RigidbodyConstraints bodyConstraints;
    /// <summary>下一次正面抓取允许的时间。</summary>
    private float nextFrontGrabAllowedTime;
    /// <summary>下一次腿部撕咬允许的时间。</summary>
    private float nextLegBiteAllowedTime;
    /// <summary>下一次策略决策时间。</summary>
    private float nextStrategyDecisionTime;
    /// <summary>是否策略决策通过的。</summary>
    private bool strategyDecisionPassed;
    /// <summary>下一次后侧策略决策时间。</summary>
    private float nextRearStrategyDecisionTime;
    /// <summary>是否后侧策略决策通过的。</summary>
    private bool rearStrategyDecisionPassed;
    /// <summary>是否正面抓取停用。</summary>
    private bool frontGrabDisabled;
    /// <summary>是否腿部撕咬停用。</summary>
    private bool legBiteDisabled;
    /// <summary>是否电锯执行停用。</summary>
    private bool chainsawExecutionDisabled;
    /// <summary>下一次电锯执行允许的时间。</summary>
    private float nextChainsawExecutionAllowedTime;
    /// <summary>下一次电锯执行决策时间。</summary>
    private float nextChainsawExecutionDecisionTime;
    /// <summary>是否电锯执行决策通过的。</summary>
    private bool chainsawExecutionDecisionPassed;
    /// <summary>体术机会是否打开。</summary>
    private bool bodyTechniqueOpportunityOpen;
    /// <summary>体术机会到期时间。</summary>
    private float bodyTechniqueOpportunityExpiresAt;
    /// <summary>体术索引。</summary>
    private int bodyTechniqueIndex;
    /// <summary>生效体术会话编号。</summary>
    private int activeBodyTechniqueSessionId;
    /// <summary>是否体术已预留。</summary>
    private bool bodyTechniqueReserved;
    /// <summary>是否体术动画窗口已消耗。</summary>
    private bool bodyTechniqueAnimationWindowConsumed;
    /// <summary>体术行为之前是否启用。</summary>
    private bool bodyTechniqueBehaviorWasEnabled;
    /// <summary>体术路径之前是否启用。</summary>
    private bool bodyTechniquePathWasEnabled;
    /// <summary>体术路径之前是否已停止。</summary>
    private bool bodyTechniquePathWasStopped;
    /// <summary>体术移动模式。</summary>
    private EnemyMovementMode bodyTechniqueMovementMode;
    /// <summary>体术动画控制器速度。</summary>
    private float bodyTechniqueAnimatorSpeed = 1f;
    /// <summary>下一次体术允许的时间。</summary>
    private float nextBodyTechniqueAllowedTime;
    /// <summary>生效种类。</summary>
    private QTEKind activeKind;
    /// <summary>配对动作入口图层混合协程。</summary>
    private Coroutine qteEntryLayerBlendRoutine;
    /// <summary>动画覆盖。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly QTEAnimatorOverrideScope animationOverride = new QTEAnimatorOverrideScope();
    /// <summary>命中左侧腿部状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashHitLeftLegState = Animator.StringToHash("Hit_LeftLeg");
    /// <summary>命中右侧腿部状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashHitRightLegState = Animator.StringToHash("Hit_RightLeg");
    /// <summary>中文含义：根节点。</summary>
    public Transform Root => transform;
    /// <summary>中文含义：动画控制器。</summary>
    public Animator Animator => animator;
    /// <summary>中文含义：碰撞体集合。</summary>
    public Collider[] Colliders => participantColliders;
    /// <summary>是否可用。</summary>
    public bool IsAvailable => isActiveAndEnabled && started;
    /// <summary>是否存活。</summary>
    public bool IsAlive => controller != null && !controller.IsDead;
    /// <summary>活动会话编号。</summary>
    public int ActiveSessionId => activeSessionId;

    /// <summary>CommitAssassinationResult 的业务处理入口；本方法直接依赖 DamageResolutionModule.CommitPairedActionHeavyStrike(VillagerController, float, Vector3)、VillagerController.BeginDeath()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()、VillagerQTEParticipant.PrepareAssassinationDeathPose(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DamageResolutionModule.CommitPairedActionHeavyStrike(VillagerController, float, Vector3)、VillagerController.BeginDeath()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()、VillagerQTEParticipant.PrepareAssassinationDeathPose(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">会话。</param>
    /// <param name="damage">伤害。</param>
    public bool CommitAssassinationResult(int session, float damage)
    {
        if (session <= 0 || session != activeSessionId || controller == null || controller.IsDead)
        {
            return false;
        }

        if (controller.missionEnemyType == MissionEnemyType.Ganado)
        {
            PrepareAssassinationDeathPose(session);
            controller.BeginDeath();
            return true;
        }

        controller.StopAndClearMovement();
        controller.SetMovementMode(EnemyMovementMode.FullyStopped);
        if ((activeKind == QTEKind.GroundFinisher || activeKind == QTEKind.KnifeAssassination)
            && controller.missionEnemyType == MissionEnemyType.Sawman)
        {
            if (damage >= controller.currentHealth)
            {
                PrepareAssassinationDeathPose(session);
            }
            sawKnifeHitSession = session;
            controller.combatModule.CommitPairedKnifeDamage(damage, transform.position + Vector3.up * 1.1f);
            return true;
        }
        DamageResolutionModule.CommitPairedActionHeavyStrike(controller, damage, transform.position + Vector3.up * 1.1f);
        if (controller.currentHealth <= 0f)
        {
            PrepareAssassinationDeathPose(session);
            controller.BeginDeath();
        }

        return true;
    }

    /// <summary>CommitPairedActionPose 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">会话。</param>
    /// <param name="position">位置。</param>
    /// <param name="orientation">朝向。</param>
    public bool CommitPairedActionPose(int session, Vector3 position, Quaternion orientation)
    {
        if (session <= 0 || session != activeSessionId && session != activeBodyTechniqueSessionId || controller == null || controller.motionExecutor == null)
        {
            return false;
        }

        // 凭证。
var token = controller.motionExecutor.CurrentToken;
        return token.Source == GameArchitecture.MotionSource.PairedActionAction && controller.motionExecutor.CommitPose(token, position, orientation, true);
    }

    /// <summary>是否保留中暗杀死亡姿态。</summary>
    public bool IsPreservingAssassinationDeathPose => preserveAssassinationDeathPose;
    /// <summary>是否应当消耗暗杀根运动。</summary>
    public bool ShouldConsumeAssassinationRootMotion => activeSessionId != 0 && !released && activeKind == QTEKind.KnifeAssassination && animator != null && qteLayerIndex >= 0 && animator.GetLayerWeight(qteLayerIndex) > 0.001f;
    /// <summary>是否控制中敌人智能。</summary>
    public bool IsControllingEnemyAI => activeSessionId != 0 || activeBodyTechniqueSessionId != 0 || keepAIDisabledForIsolatedTest;

    /// <summary>动作进入。事件通信成员 ActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int, EnemyQTEAction, int> ActionEntered;
    /// <summary>动作完成。事件通信成员 ActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int, EnemyQTEAction, int> ActionCompleted;
    /// <summary>动作退出。事件通信成员 ActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int, EnemyQTEAction, int, bool> ActionExited;
    /// <summary>撕咬接触。事件通信成员 BiteContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int> BiteContact;
    /// <summary>暗杀接触。事件通信成员 AssassinationContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int> AssassinationContact;
    /// <summary>已失效。事件通信成员 Invalidated，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<int, QTECancelReason> Invalidated;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;BehaviorTree&gt;()、Component.GetComponent&lt;PathAgent&gt;()、Component.GetComponent&lt;Rigidbody&gt;()、Component.GetComponent&lt;VillagerController&gt;()、Component.GetComponentsInChildren&lt;Collider&gt;(bool)、EnemyAnimationAdapter.SetIntegerValue(int, int) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<VillagerController>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (pathAgent == null)
        {
            pathAgent = GetComponent<PathAgent>();
        }

        if (behaviorTree == null)
        {
            behaviorTree = GetComponent<BehaviorTree>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (participantColliders == null || participantColliders.Length == 0)
        {
            participantColliders = GetComponentsInChildren<Collider>(true);
        }

        EnsureParameterCache();
        if (qteLayerIndex >= 0)
        {
            controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
        }

        if (controller != null)
        {
            controller.Died += HandleControllerDied;
        }

        started = true;
        RegisterPromptCandidate();
        BindAnimationEvents();
        if (keepAIDisabledForIsolatedTest)
        {
            SuspendAIForIsolatedTest();
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerQTEParticipant.BindAnimationEvents()、VillagerQTEParticipant.RegisterPromptCandidate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        isDeactivate = false;
        if (started)
        {
            BindAnimationEvents();
            RegisterPromptCandidate();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、EnemyAIManager.UnregisterPromptCandidate(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Remove(VillagerQTEParticipant)、object.ReferenceEquals(object, object)、VillagerQTEParticipant.CancelBodyTechnique(int)、VillagerQTEParticipant.ClearAnimationVariant()、VillagerQTEParticipant.Release(int) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        consumedNativeBodyReaction = default;
        isDeactivate = true;
        ActiveParticipants.Remove(this);
        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.UnregisterPromptCandidate(this);
        }

        StopQTEEntryLayerBlend();
        if (activeBodyTechniqueSessionId != 0)
        {
            CancelBodyTechnique(activeBodyTechniqueSessionId);
        }

        if (activeSessionId != 0)
        {
            // 判空前缓存的已失效。
global::System.Action<int, global::QTECancelReason> receiverValue1 = Invalidated;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.Invoke(activeSessionId, QTECancelReason.ParticipantDisabled);
            }
        }

        if (activeSessionId != 0)
        {
            Release(activeSessionId);
        }

        preserveAssassinationDeathPose = false;
        preserveSurvivorStaggerOnRelease = false;
        ClearAnimationVariant();
        UnbindAnimationEvents();
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、List&lt;VillagerQTEParticipant&gt;.Remove(VillagerQTEParticipant)、object.ReferenceEquals(object, object)、VillagerQTEParticipant.CancelBodyTechnique(int)、VillagerQTEParticipant.ClearAnimationVariant()、VillagerQTEParticipant.Release(int)、VillagerQTEParticipant.StopQTEEntryLayerBlend() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        isDeactivate = true;
        ActiveParticipants.Remove(this);
        StopQTEEntryLayerBlend();
        if (controller != null)
        {
            controller.Died -= HandleControllerDied;
        }

        if (activeBodyTechniqueSessionId != 0)
        {
            CancelBodyTechnique(activeBodyTechniqueSessionId);
        }

        if (activeSessionId != 0)
        {
            // 判空前缓存的已失效。
global::System.Action<int, global::QTECancelReason> receiverValue2 = Invalidated;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.Invoke(activeSessionId, QTECancelReason.EnemyDestroyed);
            }
        }

        if (activeSessionId != 0)
        {
            Release(activeSessionId);
        }

        preserveAssassinationDeathPose = false;
        preserveSurvivorStaggerOnRelease = false;
        ClearAnimationVariant();
        UnbindAnimationEvents();
    }

    /// <summary>OnApplicationQuit 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnApplicationQuit()
    {
        isDeactivate = true;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、object.ReferenceEquals(object, object)、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()、VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (activeSessionId != 0 && !IsAlive && !preserveAssassinationDeathPose)
        {
            // 判空前缓存的已失效。
global::System.Action<int, global::QTECancelReason> receiverValue3 = Invalidated;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Invoke(activeSessionId, QTECancelReason.EnemyDied);
            }
        }

        if (!UsesNativeBodyTechnique)
        {
            UpdateBodyTechniqueAnimationWindow();
        }
        if (bodyTechniqueOpportunityOpen && Time.time > bodyTechniqueOpportunityExpiresAt)
        {
            CloseBodyTechniqueOpportunity();
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetLayerWeight(int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、Mathf.Approximately(float, float)、VillagerQTEParticipant.SuspendAIForIsolatedTest()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (!started || animator == null || qteLayerIndex < 0)
        {
            return;
        }

        if (preserveAssassinationDeathPose)
        {
            if (!Mathf.Approximately(animator.GetLayerWeight(qteLayerIndex), 1f))
            {
                controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
            }

            return;
        }

        if (activeSessionId != 0)
        {
            return;
        }

        if (!Mathf.Approximately(animator.GetLayerWeight(qteLayerIndex), 0f))
        {
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
        }

        if (keepAIDisabledForIsolatedTest)
        {
            SuspendAIForIsolatedTest();
        }
    }

    /// <summary>BindAnimationEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.OnEnable()、VillagerQTEParticipant.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindAnimationEvents()
    {
        if (eventsBound)
        {
            return;
        }

        EnemyQTEStateSMB.OnActionEntered += HandleActionEntered;
        EnemyQTEStateSMB.OnActionCompleted += HandleActionCompleted;
        EnemyQTEStateSMB.OnActionInterrupted += HandleActionInterrupted;
        EnemyQTEStateSMB.OnActionExited += HandleActionExited;
        eventsBound = true;
    }

    /// <summary>UnbindAnimationEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.OnDestroy()、VillagerQTEParticipant.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindAnimationEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        EnemyQTEStateSMB.OnActionEntered -= HandleActionEntered;
        EnemyQTEStateSMB.OnActionCompleted -= HandleActionCompleted;
        EnemyQTEStateSMB.OnActionInterrupted -= HandleActionInterrupted;
        EnemyQTEStateSMB.OnActionExited -= HandleActionExited;
        eventsBound = false;
    }

    /// <summary>StopQTEEntryLayerBlend 的业务处理入口；本方法直接依赖 MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.BeginAction(int, EnemyQTEAction)、VillagerQTEParticipant.EndAction(int)、VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.OnDestroy()、VillagerQTEParticipant.OnDisable() 等共 8 处；下游（含内部回调体） MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopQTEEntryLayerBlend()
    {
        if (qteEntryLayerBlendRoutine == null)
        {
            return;
        }

        StopCoroutine(qteEntryLayerBlendRoutine);
        qteEntryLayerBlendRoutine = null;
    }

    /// <summary>BlendQTEEntryLayer 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetLayerWeightValue(int, float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.BeginAction(int, EnemyQTEAction)；下游（含内部回调体） EnemyAnimationAdapter.SetLayerWeightValue(int, float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    private IEnumerator BlendQTEEntryLayer(int sessionId)
    {
        // 先让目标状态在零权重下完成首次求值，避免空状态以满权重接管骨架。
        yield return null;
        // 持续时间。
float duration = Mathf.Max(0.01f, qteEntryLayerBlendDuration);
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            if (sessionId != activeSessionId || released || animator == null || qteLayerIndex < 0)
            {
                qteEntryLayerBlendRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            // 进度。
float progress = Mathf.Clamp01(elapsed / duration);
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        if (sessionId == activeSessionId && !released && animator != null && qteLayerIndex >= 0)
        {
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
        }

        qteEntryLayerBlendRoutine = null;
    }

    /// <summary>CanStart 的业务处理入口；本方法直接依赖 VillagerController.CanBeAssassinatedBy(LeonController)、VillagerQTEParticipant.CanUseRearBiteNeckQTE()、VillagerQTEParticipant.EnsureParameterCache()、VillagerQTEParticipant.HasIntegerParameter(string)、VillagerQTEParticipant.SatisfySurroundingStandingPosition(LeonController, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)、VillagerQTEParticipant.CanAttemptChainsawExecution(LeonController)、VillagerQTEParticipant.CanAttemptFrontGrab(LeonController)、VillagerQTEParticipant.CanAttemptLegBite(LeonController) 等共 6 处；下游（含内部回调体） VillagerController.CanBeAssassinatedBy(LeonController)、VillagerQTEParticipant.CanUseRearBiteNeckQTE()、VillagerQTEParticipant.EnsureParameterCache()、VillagerQTEParticipant.HasIntegerParameter(string)、VillagerQTEParticipant.SatisfySurroundingStandingPosition(LeonController, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    /// <param name="kind">种类。</param>
    /// <param name="reason">原因。输出参数，由本入口写入并交给调用方。</param>
    public bool CanStart(LeonController leon, QTEKind kind, out QTECancelReason reason)
    {
        if (kind != QTEKind.FrontGrab && kind != QTEKind.RearBiteNeck && kind != QTEKind.KnifeAssassination && kind != QTEKind.ChainsawExecution && kind != QTEKind.GroundFinisher)
        {
            reason = QTECancelReason.UnsupportedKind;
            return false;
        }

        if (!IsAvailable || !IsAlive || activeSessionId != 0)
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (leon == null || leon.currentState == LeonState.Dead)
        {
            reason = QTECancelReason.PlayerUnavailable;
            return false;
        }

        if ((kind == QTEKind.FrontGrab || kind == QTEKind.RearBiteNeck) && !HasConfirmedGrabContact(leon, kind) && !SatisfySurroundingStandingPosition(leon, kind == QTEKind.FrontGrab))
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (kind == QTEKind.RearBiteNeck && !CanUseRearBiteNeckQTE())
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (kind == QTEKind.GroundFinisher && !TryReadGroundFinisher(out _))
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        EnsureParameterCache();
        if (animator == null || animator.runtimeAnimatorController == null || qteLayerIndex < 0 || !HasIntegerParameter("QTEAction"))
        {
            reason = QTECancelReason.ResourceMissing;
            return false;
        }

        if (kind == QTEKind.KnifeAssassination && (controller == null || !controller.CanBeAssassinatedBy(leon)))
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (kind == QTEKind.ChainsawExecution && (!enableChainsawExecution || chainsawExecutionDisabled))
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        reason = QTECancelReason.None;
        return true;
    }

    /// <summary>ApplyAnimationVariant 的业务处理入口；本方法直接依赖 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="variant">变体。</param>
    public bool ApplyAnimationVariant(QTEAnimationVariant variant)
    {
        if (variant == null || !variant.IsComplete || animator == null)
        {
            return false;
        }

        if (animator.isHuman)
        {
            return animationOverride.Apply(animator, variant.enemyEnter, variant.enemyHold, variant.enemySuccess, variant.enemyFailure);
        }

        var presenter = controller.animationPresenter;
        return animationOverride.Apply(animator,
            presenter.ResolveClipForSkeleton(animator, variant.enemyEnter),
            presenter.ResolveClipForSkeleton(animator, variant.enemyHold),
            presenter.ResolveClipForSkeleton(animator, variant.enemySuccess),
            presenter.ResolveClipForSkeleton(animator, variant.enemyFailure));
    }

    /// <summary>ClearAnimationVariant 的业务处理入口；本方法直接依赖 QTEAnimatorOverrideScope.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.OnDestroy()、VillagerQTEParticipant.OnDisable()、VillagerQTEParticipant.Release(int)；下游（含内部回调体） QTEAnimatorOverrideScope.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearAnimationVariant()
    {
        animationOverride.Clear();
    }

    /// <summary>SatisfySurroundingStandingPosition 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)、VillagerController.PlantNearHouseThreeTread(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)；下游（含内部回调体） Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)、VillagerController.PlantNearHouseThreeTread(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">玩家。</param>
    /// <param name="front">是否前方。</param>
    private bool SatisfySurroundingStandingPosition(LeonController player, bool front)
    {
        // 偏移量。
Vector3 offset = player.transform.position - transform.position;
        if (Mathf.Abs(offset.y) > .28f)
        {
            return false;
        }

        offset.y = 0f;
        // 距离。
float distance;
        if (front)
        {
            distance = frontGrabStrategyDistance;
        }
        else
        {
            distance = rearGrabStrategyDistance;
        }

        // 角度。
float angle;
        if (front)
        {
            angle = frontGrabStrategyFacingAngle;
        }
        else
        {
            angle = rearGrabStrategyFacingAngle;
        }

        // 短路判断得到的角度的返回结果。
bool conditionResult8;
        if (offset.sqrMagnitude < .000001f || offset.sqrMagnitude > distance * distance || Vector3.Angle(transform.forward, offset) > angle)
        {
            conditionResult8 = true;
        }
        else
        {
            // 按原顺序缓存的实参玩家的变换的前向。
global::UnityEngine.Vector3 argumentValue7 = player.transform.forward;
            // 分支选出的偏移量。
global::UnityEngine.Vector3 selectedValue6;
            if (front)
            {
                selectedValue6 = -offset;
            }
            else
            {
                selectedValue6 = offset;
            }

            conditionResult8 = Vector3.Angle(argumentValue7, selectedValue6) > angle;
        }

        if (conditionResult8)
        {
            return false;
        }

        return controller && !controller.IsNavigationTraversalProtected && !controller.PlantNearHouseThreeTread(transform.position) && !controller.PlantNearHouseThreeTread(player.transform.position);
    }

    /// <summary>CanAttemptFrontGrab 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptFrontGrabQTE.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)、TryStartFrontGrabQTE.OnUpdate()；下游（含内部回调体） Mathf.Max(float, float)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    public bool CanAttemptFrontGrab(LeonController leon)
    {
        if (frontGrabDisabled || Time.time < nextFrontGrabAllowedTime)
        {
            return false;
        }

        if (!CanWindupGrab(leon, QTEKind.FrontGrab))
        {
            strategyDecisionPassed = false;
            return false;
        }

        if (strategyDecisionPassed)
        {
            return true;
        }

        if (Time.time < nextStrategyDecisionTime)
        {
            return false;
        }

        nextStrategyDecisionTime = Time.time + Mathf.Max(2.5f, strategyDecisionInterval);
        strategyDecisionPassed = frontGrabProbability > 0f && UnityEngine.Random.value < frontGrabProbability;
        return strategyDecisionPassed;
    }

    /// <summary>CanAttemptRearBiteNeck 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanUseRearBiteNeckQTE()、VillagerQTEParticipant.EvaluateRearGrabProbability()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptEnemyQTEBase.OnUpdate()、CanAttemptFrontGrabQTE.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)、TryStartFrontGrabQTE.OnUpdate()、TryStartFrontGrabQTE.RecordStandingGrabRejection(QTECancelReason)；下游（含内部回调体） VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanUseRearBiteNeckQTE()、VillagerQTEParticipant.EvaluateRearGrabProbability()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    public bool CanAttemptRearBiteNeck(LeonController leon)
    {
        if (!CanUseRearBiteNeckQTE())
        {
            return false;
        }

        if (frontGrabDisabled || Time.time < nextFrontGrabAllowedTime)
        {
            return false;
        }

        if (!CanWindupGrab(leon, QTEKind.RearBiteNeck))
        {
            rearStrategyDecisionPassed = false;
            return false;
        }

        return EvaluateRearGrabProbability();
    }

    /// <summary>EvaluateRearGrabProbability 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool EvaluateRearGrabProbability()
    {
        if (rearGrabProbability <= 0f)
        {
            rearStrategyDecisionPassed = false;
            return false;
        }

        // 条件节点与动作节点共用一次判定，失败后等待决策间隔再重试。
        if (rearStrategyDecisionPassed)
        {
            return true;
        }

        if (Time.time < nextRearStrategyDecisionTime)
        {
            return false;
        }

        nextRearStrategyDecisionTime = Time.time + Mathf.Max(2.5f, strategyDecisionInterval);
        rearStrategyDecisionPassed = rearGrabProbability >= 1f || UnityEngine.Random.value < Mathf.Clamp01(rearGrabProbability);
        return rearStrategyDecisionPassed;
    }

    /// <summary>CanAttemptLegBite 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptEnemyQTEBase.OnUpdate()、CanCrawlAttackNow.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)；下游（含内部回调体） VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    public bool CanAttemptLegBite(LeonController leon)
    {
        if (legBiteDisabled || Time.time < nextLegBiteAllowedTime)
        {
            return false;
        }

        if (!CanStart(leon, QTEKind.LegBite, out _))
        {
            return false;
        }

        // 偏移量。
Vector3 offset = leon.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude > 0.001f && offset.magnitude <= legBiteStrategyDistance;
    }

    /// <summary>CanUseRearBiteNeckQTE 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool CanUseRearBiteNeckQTE()
    {
        if (controller == null || controller.missionEnemyType != MissionEnemyType.Ganado)
        {
            return false;
        }

        if (controller.TryGetComponent<EnemyHeldWeapon>(out var heldWeapon) && heldWeapon.IsHeld)
        {
            return false;
        }

        return true;
    }

    /// <summary>CanAttemptChainsawExecution 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Max(float, float)、Vector3.Angle(Vector3, Vector3)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptEnemyQTEBase.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Max(float, float)、Vector3.Angle(Vector3, Vector3)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    public bool CanAttemptChainsawExecution(LeonController leon)
    {
        if (chainsawExecutionDisabled || Time.time < nextChainsawExecutionAllowedTime)
        {
            return false;
        }

        if (!CanStart(leon, QTEKind.ChainsawExecution, out _))
        {
            return false;
        }

        // 从敌人指向玩家的方向向量。
Vector3 enemyToPlayer = leon.transform.position - transform.position;
        enemyToPlayer.y = 0f;
        // 距离。
float distance = enemyToPlayer.magnitude;
        if (distance <= 0.001f || distance > chainsawExecutionStrategyDistance)
        {
            chainsawExecutionDecisionPassed = false;
            return false;
        }

        if (Vector3.Angle(transform.forward, enemyToPlayer / distance) > chainsawExecutionStrategyFacingAngle)
        {
            chainsawExecutionDecisionPassed = false;
            return false;
        }

        if (chainsawExecutionDecisionPassed)
        {
            return true;
        }

        if (Time.time < nextChainsawExecutionDecisionTime)
        {
            return false;
        }

        // 最大生命值。
float maximumHealth = Mathf.Max(1f, leon.maxHealth);
        // 生命比例。
float healthRatio = Mathf.Clamp01(leon.currentHealth / maximumHealth);
        // 概率。
float probability;
        if (healthRatio <= chainsawExecutionLowHealthThreshold)
        {
            probability = chainsawExecutionLowHealthProbability;
        }
        else
        {
            probability = chainsawExecutionNormalHealthProbability;
        }

        nextChainsawExecutionDecisionTime = Time.time + Mathf.Max(0.1f, strategyDecisionInterval);
        chainsawExecutionDecisionPassed = UnityEngine.Random.value <= Mathf.Clamp01(probability);
        if (chainsawExecutionDecisionPassed)
        {
            return true;
        }

        nextChainsawExecutionAllowedTime = Time.time + Mathf.Max(0.1f, chainsawExecutionProbabilityRetryDelay);
        return false;
    }

    /// <summary>UpdateBodyTechniqueAnimationWindow 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Animator.StringToHash(string)、AnimatorStateInfo.IsTag(string)、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()、VillagerQTEParticipant.OpenBodyTechniqueOpportunity(int, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.Update()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Animator.StringToHash(string)、AnimatorStateInfo.IsTag(string)、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()、VillagerQTEParticipant.OpenBodyTechniqueOpportunity(int, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private int hitWindowState;
    private float hitWindowTimeScale = 1f;

    internal void SetHitWindowClock(int state, float scale)
    {
        hitWindowState = state;
        hitWindowTimeScale = scale > 0f ? scale : 1f;
    }

    private void UpdateBodyTechniqueAnimationWindow()
    {
        if (!enableBodyTechniqueOpportunity || animator == null || activeSessionId != 0 || activeBodyTechniqueSessionId != 0 || !IsAlive || controller == null || controller.CurrentState != VillagerState.Staggered)
        {
            bodyTechniqueAnimationWindowConsumed = false;
            CloseBodyTechniqueOpportunity();
            return;
        }

        // 当前动画状态信息。
AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            stateInfo = animator.GetNextAnimatorStateInfo(0);
        }

        // 是否左侧腿部。
        bool isLeftLeg = stateInfo.shortNameHash == HashHitLeftLegState;
        // 是否右侧腿部。
        bool isRightLeg = stateInfo.shortNameHash == HashHitRightLegState;
        // isStandingStagger（是否站立硬直（动画参数或状态路径的哈希编号））：使用位置：作为 VillagerQTEParticipant.ResolveBodyTechniqueIndex(bool, bool) 的参数、VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()。
        bool isStandingStagger = stateInfo.IsTag(BodyTechniqueEntryTag) || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Body_Front") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Body_Back") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Body_Left") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Body_Right") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Head_Front") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Head_Back") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Head_Left") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Head_Right") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Heavy_Front") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Heavy_Back") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Heavy_Left") || stateInfo.shortNameHash == Animator.StringToHash("Stagger_Heavy_Right");
        bool isSawKneelLeft = controller.missionEnemyType == MissionEnemyType.Sawman && stateInfo.fullPathHash == SawKneelLeft;
        bool isSawKneelRight = controller.missionEnemyType == MissionEnemyType.Sawman && stateInfo.fullPathHash == SawKneelRight;
        bool isSawKneel = isSawKneelLeft || isSawKneelRight;
        if (!isLeftLeg && !isRightLeg && !isStandingStagger && !isSawKneel)
        {
            bodyTechniqueAnimationWindowConsumed = false;
            CloseBodyTechniqueOpportunity();
            return;
        }

        if (isStandingStagger && !controller.CanOpenBodyTechniqueFromCurrentStagger)
        {
            bodyTechniqueAnimationWindowConsumed = false;
            CloseBodyTechniqueOpportunity();
            return;
        }

        // 动画归一化播放时间。
        float normalizedTime = stateInfo.normalizedTime;
        if (hitWindowState == stateInfo.fullPathHash) normalizedTime *= hitWindowTimeScale;
        // 打开时间。
        float openTime;
        if (isSawKneel)
        {
            openTime = 0.09f;
        }
        else if (isStandingStagger)
        {
            openTime = staggerBodyTechniqueOpenNormalizedTime;
        }
        else
        {
            openTime = bodyTechniqueOpenNormalizedTime;
        }

        // 关闭时间。
        float closeTime;
        if (isSawKneel)
        {
            if (isSawKneelRight)
            {
                closeTime = 0.76f;
            }
            else
            {
                closeTime = 0.74f;
            }
        }
        else if (isStandingStagger)
        {
            closeTime = staggerBodyTechniqueCloseNormalizedTime;
        }
        else
        {
            closeTime = bodyTechniqueCloseNormalizedTime;
        }

        if (normalizedTime < openTime)
        {
            CloseBodyTechniqueOpportunity();
            return;
        }

        if (normalizedTime > closeTime)
        {
            bool keepSawmanWindowOpen = controller != null
                && controller.missionEnemyType == MissionEnemyType.Sawman
                && controller.combatModule != null
                && controller.combatModule.ReadCurrentStandingStaggerBodyTechnique
                && Time.time < controller.combatModule.ReadBodyTechniqueWindowEndTime;
            if (keepSawmanWindowOpen)
            {
                if (!bodyTechniqueAnimationWindowConsumed)
                {
                    bodyTechniqueAnimationWindowConsumed = true;
                    OpenBodyTechniqueOpportunity(ResolveBodyTechniqueIndex(isRightLeg, isStandingStagger), bodyTechniqueMinimumWindowDuration);
                }
                return;
            }
            bodyTechniqueAnimationWindowConsumed = false;
            CloseBodyTechniqueOpportunity();
            return;
        }

        if (bodyTechniqueAnimationWindowConsumed)
        {
            return;
        }

        bodyTechniqueAnimationWindowConsumed = true;
        OpenBodyTechniqueOpportunity(ResolveBodyTechniqueIndex(isRightLeg, isStandingStagger), bodyTechniqueMinimumWindowDuration);
    }

    /// <summary>OpenBodyTechniqueOpportunity 的业务处理入口；本方法直接依赖 Mathf.Clamp(int, int, int)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)、VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()；下游（含内部回调体） Mathf.Clamp(int, int, int)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="techniqueIndex">体术索引。</param>
    /// <param name="minimumDuration">最小持续时间。</param>
    public void OpenBodyTechniqueOpportunity(int techniqueIndex, float minimumDuration)
    {
        if (UsesNativeBodyTechnique || !enableBodyTechniqueOpportunity || activeSessionId != 0
            || activeBodyTechniqueSessionId != 0 || !IsAlive || Time.time < nextBodyTechniqueAllowedTime)
        {
            return;
        }

        bodyTechniqueIndex = Mathf.Clamp(techniqueIndex, 0, 1);
        bodyTechniqueOpportunityOpen = true;
        bodyTechniqueOpportunityExpiresAt = Mathf.Max(bodyTechniqueOpportunityExpiresAt, Time.time + Mathf.Max(0.05f, minimumDuration));
    }

    /// <summary>ResolveBodyTechniqueIndex 的业务处理入口；本方法直接依赖 Animator.GetInteger(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()；下游（含内部回调体） Animator.GetInteger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="isRightLeg">是否右侧腿部。</param>
    /// <param name="isStandingStagger">是否站立硬直。</param>
    private int ResolveBodyTechniqueIndex(bool isRightLeg, bool isStandingStagger)
    {
        if (!isStandingStagger)
        {
            if (isRightLeg)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        if (animator == null)
        {
            return 0;
        }

        // 方向。
int direction = animator.GetInteger(HashStaggerDirection);
        if (direction == 3)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>CloseBodyTechniqueOpportunity 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyBodyTechniqueOpportunitySMB.OnStateExit(Animator, AnimatorStateInfo, int)、EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)、VillagerQTEParticipant.Update()、VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CloseBodyTechniqueOpportunity()
    {
        if (activeBodyTechniqueSessionId != 0)
        {
            return;
        }

        bodyTechniqueOpportunityOpen = false;
        bodyTechniqueOpportunityExpiresAt = 0f;
    }

    /// <summary>CanStartBodyTechnique 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)；下游（含内部回调体） Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    /// <param name="techniqueIndex">体术索引。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="reason">原因。输出参数，由本入口写入并交给调用方。</param>
    public bool CanStartBodyTechnique(LeonController leon, out int techniqueIndex, out QTECancelReason reason)
    {
        techniqueIndex = bodyTechniqueIndex;
        bool native = UsesNativeBodyTechnique;
        if (!enableBodyTechniqueOpportunity || Time.time < nextBodyTechniqueAllowedTime)
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (native)
        {
            if (Time.timeScale <= 0f || !TryReadNativeBodyTechnique(out _, out techniqueIndex))
            {
                reason = QTECancelReason.EnemyUnavailable;
                return false;
            }
        }
        else if (!bodyTechniqueOpportunityOpen || Time.time > bodyTechniqueOpportunityExpiresAt)
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (!IsAvailable || !IsAlive || activeSessionId != 0 || activeBodyTechniqueSessionId != 0)
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (controller == null || controller.CurrentState != VillagerState.Staggered)
        {
            reason = QTECancelReason.EnemyUnavailable;
            return false;
        }

        if (leon == null || leon.currentState == LeonState.Dead || leon.currentState == LeonState.Staggered || leon.currentState == LeonState.QTE || leon.currentState == LeonState.MeleeAttacking)
        {
            reason = QTECancelReason.PlayerUnavailable;
            return false;
        }

        return HasBodyTechniqueApproach(leon, out reason);
    }

    /// <summary>根据是否具备站立硬直背刺条件与可用小刀，动态裁决正面体术允许的最大敌人朝向角，实现 360 度无死区分区及无刀回退。</summary>
    internal static float ResolveBodyTechniqueMaxEnemyFacingAngle(float defaultMaxAngle, bool canStaggerBackstab,
        bool playerHasUsableKnife, float staggerBackstabRearAngle)
    {
        if (canStaggerBackstab && playerHasUsableKnife)
        {
            return Mathf.Clamp(180f - staggerBackstabRearAngle, 0f, defaultMaxAngle);
        }

        return defaultMaxAngle;
    }

    internal bool HasBodyTechniqueApproach(LeonController leon, out QTECancelReason reason)
    {
        Vector3 direction = leon.transform.position - transform.position;
        float height = Mathf.Abs(direction.y);
        direction.y = 0f;
        float distance = direction.magnitude;
        if (distance <= .001f || distance > bodyTechniquePromptDistance || height > 1.25f)
        {
            reason = QTECancelReason.DistanceInvalid;
            return false;
        }

        direction /= distance;
        float enemyFacing = Vector3.Angle(transform.forward, direction);
        float playerFacing = Vector3.Angle(leon.transform.forward, -direction);
        bool canStaggerBackstab = controller != null && controller.CanBeStaggerBackstabbedBy(leon);
        LeonWeaponHandler weaponHandler = leon.GetComponent<LeonWeaponHandler>();
        bool playerHasUsableKnife = weaponHandler != null && weaponHandler.HasUsableKnife();
        float staggerBackstabRearAngle = 95f;
        LeonQTECoordinator coordinator = leon.GetComponent<LeonQTECoordinator>();
        if (coordinator != null && coordinator.config != null)
        {
            staggerBackstabRearAngle = coordinator.config.staggerBackstabRearAngle;
        }

        float maxEnemyFacing = ResolveBodyTechniqueMaxEnemyFacingAngle(bodyTechniqueEnemyFacingAngle,
            canStaggerBackstab, playerHasUsableKnife, staggerBackstabRearAngle);
        if (enemyFacing > maxEnemyFacing || playerFacing > bodyTechniquePlayerFacingAngle)
        {
            reason = QTECancelReason.FacingInvalid;
            return false;
        }

        if (UsesNativeBodyTechnique && !controller.combatModule.HasNativeBodyTechniqueApproach(leon, bodyTechniquePromptDistance))
        {
            reason = QTECancelReason.DistanceInvalid;
            return false;
        }

        reason = QTECancelReason.None;
        return true;
    }

    public bool TryReserveBodyTechnique(int sessionId)
    {
        if (sessionId <= 0 || activeBodyTechniqueSessionId != 0)
        {
            return false;
        }

        if (UsesNativeBodyTechnique)
        {
            if (!CanStartBodyTechnique(controller.Player, out _, out _) || !TryReadNativeBodyTechnique(out ActionHandle action, out _))
            {
                return false;
            }

            consumedNativeBodyReaction = action;
        }
        else if (!bodyTechniqueOpportunityOpen || Time.time > bodyTechniqueOpportunityExpiresAt || !IsAlive
            || activeSessionId != 0 || controller == null || controller.CurrentState != VillagerState.Staggered)
        {
            return false;
        }

        activeBodyTechniqueSessionId = sessionId;
        bodyTechniqueReserved = true;
        bodyTechniqueOpportunityOpen = false;
        bodyTechniqueOpportunityExpiresAt = 0f;
        bodyTechniqueBehaviorWasEnabled = behaviorTree != null && behaviorTree.enabled && behaviorTree.gameObject.activeInHierarchy;
        bodyTechniquePathWasEnabled = pathAgent != null && pathAgent.enabled;
        bodyTechniquePathWasStopped = pathAgent != null && pathAgent.isStopped;
        if (controller != null)
        {
            bodyTechniqueMovementMode = controller.CurrentMovementMode;
        }
        else
        {
            bodyTechniqueMovementMode = EnemyMovementMode.FullyStopped;
        }

        if (animator != null && animator.speed > 0.01f)
        {
            bodyTechniqueAnimatorSpeed = animator.speed;
        }
        else
        {
            bodyTechniqueAnimatorSpeed = 1f;
        }

        if (behaviorTree != null)
        {
            behaviorTree.DisableBehavior();
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue16 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            receiverValue16.StopAndClearMovement();
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue17 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.SetMovementMode(EnemyMovementMode.FullyStopped);
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue18 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.BeginQTEGroundLock();
        }

        if (pathAgent != null)
        {
            pathAgent.isStopped = true;
        }

        if (controller != null && controller.combatModule != null)
        {
            int capturedKneel = controller.combatModule.CaptureBodyTechniqueKneelState();
            if (capturedKneel > 0 && animator != null && controller.animationPresenter != null)
            {
                // 跪姿体术预留期间放慢起身尾段推进，防止玩家跑向起脚途中敌人提前站起。
                controller.animationPresenter.SetPlaySpeed(0.12f);
            }
        }

        return true;
    }

    private bool TryReadNativeBodyTechnique(out ActionHandle action, out int techniqueIndex)
    {
        techniqueIndex = 0;
        if (controller.combatModule.TryReadTrappedOpportunity(out action))
        {
            return action != consumedNativeBodyReaction;
        }
        if (!controller.combatModule.TryReadNativeBodyReaction(out NativeBodyReaction reaction, out action)
            || action == consumedNativeBodyReaction || !controller.animationPresenter.IsNativeBodyTechniquePoseReady(reaction))
        {
            return false;
        }

        if (reaction is NativeBodyReaction.HeadStagger or NativeBodyReaction.PerfectParry)
        {
            techniqueIndex = 1;
        }

        return true;
    }

    /// <summary>CancelBodyTechnique 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.ReleaseBodyTechniqueReservation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CancelBodyTechniqueIfActive()、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)、VillagerQTEParticipant.OnDestroy()、VillagerQTEParticipant.OnDisable()；下游（含内部回调体） VillagerQTEParticipant.ReleaseBodyTechniqueReservation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public void CancelBodyTechnique(int sessionId)
    {
        if (sessionId <= 0 || sessionId != activeBodyTechniqueSessionId)
        {
            return;
        }

        ReleaseBodyTechniqueReservation();
    }

    /// <summary>ApplyBodyTechniqueHit 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、object.ReferenceEquals(object, object)、Vector3.Lerp(Vector3, Vector3, float)、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()、VillagerController.TakeBodyTechniqueDamage(float, Vector3)、VillagerQTEParticipant.ApplyBodyTechniqueKnockback(Transform) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HandleBodyTechniqueHit(int, FullBodyActionType)；下游（含内部回调体） Mathf.Max(float, float)、object.ReferenceEquals(object, object)、Vector3.Lerp(Vector3, Vector3, float)、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()、VillagerController.TakeBodyTechniqueDamage(float, Vector3)、VillagerQTEParticipant.ApplyBodyTechniqueKnockback(Transform) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="actionType">动作类型。</param>
    /// <param name="attacker">攻击者。</param>
    public bool ApplyBodyTechniqueHit(int sessionId, FullBodyActionType actionType, Transform attacker, out Vector3 hitPoint)
    {
        hitPoint = transform.position + Vector3.up * 1.1f;
        if (sessionId <= 0 || sessionId != activeBodyTechniqueSessionId || !IsAlive)
        {
            return false;
        }

        // 伤害。
float damage = bodyTechniqueDamage;
        if (actionType == FullBodyActionType.QTESpinningHookKick)
        {
            damage *= Mathf.Max(0f, spinningHookDamageMultiplier);
        }

        // hitPoint（命中点位置）：使用位置：作为 VillagerController.TakeBodyTechniqueDamage(float, Vector3) 的参数、作为 VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform) 的参数、VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)。
        if (attacker != null && UsesNativeBodyTechnique)
        {
            Vector3 separation = transform.position - attacker.position;
            if (Vector3.ProjectOnPlane(separation, Vector3.up).sqrMagnitude > 1.60f * 1.60f
                || !controller.combatModule.HasNativeBodyTechniqueApproach(controller.Player, 1.60f))
            {
                return false;
            }
        }
        if (actionType == FullBodyActionType.QTERightCutKick)
        {
            hitPoint = transform.position + Vector3.up * 0.75f;
        }

        if (animator != null && controller != null && controller.animationPresenter != null)
        {
            float restoredSpeed = 1f;
            if (!Mathf.Approximately(bodyTechniqueAnimatorSpeed, 0f))
            {
                restoredSpeed = bodyTechniqueAnimatorSpeed;
            }
            controller.animationPresenter.SetPlaySpeed(restoredSpeed);
        }

        global::VillagerController receiverValue19 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            receiverValue19.StopAndClearMovement();
        }

        global::VillagerController receiverValue20 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.SetMovementMode(EnemyMovementMode.FullyStopped);
        }

        global::VillagerController receiverValue21 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.TakeBodyTechniqueDamage(damage, hitPoint);
        }

        if (attacker != null)
        {
            ChapterOneEffects.TryPlay(ChapterOneEffect.KickImpact, hitPoint, attacker.forward);
        }
        float travelScale = 1f;
        if (actionType == FullBodyActionType.QTESpinningHookKick)
        {
            travelScale = 1.2f;
        }
        Vector3 kickDirection = ResolveBodyTechniqueKnockbackDirection(attacker, transform.position);
        if (attacker != null && actionType == FullBodyActionType.QTERightCutKick)
        {
            kickDirection = Vector3.ProjectOnPlane(kickDirection - attacker.right * .8f, Vector3.up).normalized;
        }
        Vector3 virtualContactSource = transform.position - kickDirection;
        if (attacker == null || controller == null || controller.combatModule == null || !controller.combatModule.TryBeginBodyTechniqueKnockdown(virtualContactSource, travelScale))
        {
            ApplyBodyTechniqueKnockback(attacker, kickDirection);
        }
        ReleaseBodyTechniqueReservation();
        ApplyBodyTechniqueSplashDamage(damage, hitPoint, attacker);
        return true;
    }

    /// <summary>ApplyBodyTechniqueKnockback 的业务处理入口；本方法直接依赖 VillagerController.BeginExternalKnockback(Vector3, float, float)、VillagerQTEParticipant.ResolveBodyTechniqueKnockbackDirection(Transform, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)；下游（含内部回调体） VillagerController.BeginExternalKnockback(Vector3, float, float)、VillagerQTEParticipant.ResolveBodyTechniqueKnockbackDirection(Transform, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="attacker">攻击者。</param>
    private void ApplyBodyTechniqueKnockback(Transform attacker, Vector3 direction = default)
    {
        if (!enableBodyTechniqueKnockback || controller == null || bodyTechniqueKnockbackDistance <= 0f)
        {
            return;
        }

        // direction（方向）：保存 VillagerQTEParticipant.ResolveBodyTechniqueKnockbackDirection(Transform, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.BeginExternalKnockback(Vector3, float, float) 的参数。
        if (direction.sqrMagnitude <= .001f)
        {
            direction = ResolveBodyTechniqueKnockbackDirection(attacker, transform.position);
        }
        float effectiveDistance = Mathf.Max(bodyTechniqueKnockbackDistance, 1.55f);
        float effectiveDuration = Mathf.Max(bodyTechniqueKnockbackDuration, 0.45f);
        controller.BeginExternalKnockback(direction, effectiveDistance, effectiveDuration);
    }

    /// <summary>ResolveBodyTechniqueKnockbackDirection 的业务处理入口；本方法直接依赖 Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueKnockback(Transform)；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="attacker">攻击者。</param>
    /// <param name="targetPosition">目标位置。</param>
    private Vector3 ResolveBodyTechniqueKnockbackDirection(Transform attacker, Vector3 targetPosition)
    {
        // 方向。
Vector3 direction = Vector3.zero;
        if (attacker != null)
        {
            direction = targetPosition - attacker.position;
            direction.y = 0f;
            // 前向。
Vector3 forward = attacker.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f)
            {
                forward.Normalize();
                if (direction.sqrMagnitude <= 0.0001f || Vector3.Dot(direction.normalized, forward) < 0.2f)
                {
                    direction = forward;
                }
            }
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = -transform.forward;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }
        else
        {
            return Vector3.forward;
        }
    }

    /// <summary>ApplyBodyTechniqueSplashDamage 的业务处理入口；本方法直接依赖 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、Mathf.Abs(float)、Object.FindObjectsByType&lt;VillagerQTEParticipant&gt;(FindObjectsInactive, FindObjectsSortMode)、object.ReferenceEquals(object, object)、Vector3.Angle(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.Normalize() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)；下游（含内部回调体） EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、Mathf.Abs(float)、Object.FindObjectsByType&lt;VillagerQTEParticipant&gt;(FindObjectsInactive, FindObjectsSortMode)、object.ReferenceEquals(object, object)、Vector3.Angle(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.Normalize() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mainDamage">主要伤害。</param>
    /// <param name="mainHitPoint">主要命中点。</param>
    /// <param name="attacker">攻击者。</param>
    private void ApplyBodyTechniqueSplashDamage(float mainDamage, Vector3 mainHitPoint, Transform attacker)
    {
        if (!enableBodyTechniqueSplashDamage || bodyTechniqueSplashRadius <= 0f || bodyTechniqueSplashDamageMultiplier <= 0f || bodyTechniqueSplashMaxTargets == 0)
        {
            return;
        }

        // 中心。
Vector3 center = transform.position;
        // 攻击前向。
Vector3 attackForward;
        if (attacker != null)
        {
            attackForward = attacker.forward;
        }
        else
        {
            attackForward = transform.forward;
        }

        attackForward.y = 0f;
        if (attackForward.sqrMagnitude <= 0.0001f)
        {
            attackForward = transform.forward;
        }

        attackForward.Normalize();
        // 半径平方。
float radiusSqr = bodyTechniqueSplashRadius * bodyTechniqueSplashRadius;
        // 一半角度。
float halfAngle = bodyTechniqueSplashAngle * 0.5f;
        // 飞溅伤害。
float splashDamage = mainDamage * bodyTechniqueSplashDamageMultiplier;
        // 已应用数量。
int appliedCount = 0;
        // 候选集合。
VillagerQTEParticipant[] candidates = FindObjectsByType<VillagerQTEParticipant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // candidate（候选）：每轮从候选集合取得一个元素，仅代表当前这一项。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform)。
foreach (var candidate in candidates)
        {
            if (candidate == null || candidate == this || !candidate.IsAlive || candidate.controller == null)
            {
                continue;
            }

            // 偏移量。
Vector3 offset = candidate.transform.position - center;
            // 高度。
float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            if (height > 1.4f || offset.sqrMagnitude <= 0.0001f || offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            // 方向。
Vector3 direction = offset.normalized;
            if (halfAngle > 0f && Vector3.Angle(attackForward, direction) > halfAngle)
            {
                continue;
            }

            // 命中点位置。
Vector3 hitPoint = Vector3.Lerp(candidate.transform.position + Vector3.up * 1.1f, mainHitPoint, 0.25f);
            candidate.controller.StopAndClearMovement();
            candidate.controller.SetMovementMode(EnemyMovementMode.FullyStopped);
            candidate.controller.TakeHeavyFrontDamage(splashDamage, hitPoint);
            float proximity = 1f - Mathf.Clamp01(offset.magnitude / bodyTechniqueSplashRadius);
            float travelScale = Mathf.Lerp(.7f, .9f, proximity);
            Vector3 source = candidate.transform.position - direction;
            if (!candidate.controller.combatModule.TryBeginBodyTechniqueKnockdown(source, travelScale)
                && enableBodyTechniqueKnockback && bodyTechniqueSplashKnockbackDistance > 0f)
            {
                candidate.controller.BeginExternalKnockback(direction, bodyTechniqueSplashKnockbackDistance, bodyTechniqueSplashKnockbackDuration);
            }

            // receiverValue24（判空前缓存的敌人命中效果管理器的实例）：从敌人命中效果管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform)。
            ChapterOneEffects.TryPlay(ChapterOneEffect.KickImpact, hitPoint, direction);

            appliedCount++;
            if (bodyTechniqueSplashMaxTargets > 0 && appliedCount >= bodyTechniqueSplashMaxTargets)
            {
                break;
            }
        }
    }

    /// <summary>TryStartLegBiteFromAI 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonQTECoordinator&gt;()、EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.CanAttemptLegBite(LeonController)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawl.OnUpdate()；下游（含内部回调体） Component.GetComponent&lt;LeonQTECoordinator&gt;()、EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.CanAttemptLegBite(LeonController)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    public bool TryStartLegBiteFromAI(LeonController leon)
    {
        if (controller == null || !controller.IsCombatEngaged || !CanAttemptLegBite(leon) || EnemyAIManager.Instance == null || !EnemyAIManager.Instance.TryAcquireQTEReservation(controller, QTEKind.LegBite))
        {
            return false;
        }

        // 当前里昂配对动作协调器。
LeonQTECoordinator coordinator = leon.GetComponent<LeonQTECoordinator>();
        if (coordinator == null)
        {
            RecordQTERejection(QTECancelReason.ResourceMissing, QTEKind.LegBite);
            EnemyAIManager.Instance.CompleteQTEAttempt(controller, QTEKind.LegBite, false);
            return false;
        }

        // 是否已开始。
bool started = coordinator.TryStart(QTEKind.LegBite, this, out _);
        if (!started)
        {
            RecordQTERejection(coordinator.LastRejectionReason, QTEKind.LegBite);
        }

        EnemyAIManager.Instance.CompleteQTEAttempt(controller, QTEKind.LegBite, started);
        return started;
    }

    /// <summary>RecordFrontGrabRejection 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.RecordQTERejection(QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerQTEParticipant.RecordQTERejection(QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reason">原因。</param>
    public void RecordFrontGrabRejection(QTECancelReason reason)
    {
        RecordQTERejection(reason);
    }

    /// <summary>RecordQTERejection 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.RecordFrontGrabRejection(QTECancelReason)；下游（含内部回调体） VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reason">原因。</param>
    public void RecordQTERejection(QTECancelReason reason)
    {
        RecordQTERejection(reason, QTEKind.FrontGrab);
    }

    /// <summary>RecordQTERejection 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TryStartEnemyQTEBase.OnUpdate()、TryStartFrontGrabQTE.RecordStandingGrabRejection(QTECancelReason)、TryStartFrontGrabQTE.TryStartStandingGrab(LeonQTECoordinator, QTEKind)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason)、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)；下游（含内部回调体） Debug.LogWarning(object, Object)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reason">原因。</param>
    /// <param name="kind">种类。</param>
    public void RecordQTERejection(QTECancelReason reason, QTEKind kind)
    {
        strategyDecisionPassed = false;
        rearStrategyDecisionPassed = false;
        if (kind == QTEKind.ChainsawExecution)
        {
            chainsawExecutionDecisionPassed = false;
        }

        if (reason == QTECancelReason.ResourceMissing || reason == QTECancelReason.UnsupportedKind)
        {
            if (kind == QTEKind.LegBite)
            {
                legBiteDisabled = true;
            }
            else if (kind == QTEKind.ChainsawExecution)
            {
                chainsawExecutionDisabled = true;
            }
            else
            {
                frontGrabDisabled = true;
            }

            Debug.LogWarning($"村民快速反应事件缺少必要资源或类型配置错误，" + $"已禁用本次生命周期内的对应资格：{kind}。", this);
            return;
        }

        // 延迟。
float delay;
        if (reason == QTECancelReason.GlobalSessionBusy)
        {
            delay = globalBusyRetryDelay;
        }
        else
        {
            delay = rejectionRetryDelay;
        }

        if (kind == QTEKind.LegBite)
        {
            nextLegBiteAllowedTime = Mathf.Max(nextLegBiteAllowedTime, Time.time + delay);
            return;
        }

        if (kind == QTEKind.ChainsawExecution)
        {
            nextChainsawExecutionAllowedTime = Mathf.Max(nextChainsawExecutionAllowedTime, Time.time + delay);
            return;
        }

        nextFrontGrabAllowedTime = Mathf.Max(nextFrontGrabAllowedTime, Time.time + delay);
        nextStrategyDecisionTime = nextFrontGrabAllowedTime;
        nextRearStrategyDecisionTime = nextFrontGrabAllowedTime;
    }

    private bool preserveSurvivorStaggerOnRelease;

    internal void BeginPairedSurvivorStagger(float blendSeconds = 0.14f)
    {
        if (!IsAlive)
        {
            return;
        }

        preserveSurvivorStaggerOnRelease = true;
        if (controller != null && controller.combatModule != null)
        {
            controller.combatModule.BeginPairedSurvivorStagger(blendSeconds);
        }
    }

    /// <summary>TryAcquire 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、EnemyActionCoordinator.RevokePreviousAnimationCallback()、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.BeginQTEGroundLock() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetLayerWeight(int)、EnemyActionCoordinator.RevokePreviousAnimationCallback()、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.BeginQTEGroundLock() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="kind">种类。</param>
    public bool TryAcquire(int sessionId, QTEKind kind)
    {
        if (sessionId <= 0 || activeSessionId != 0 || !IsAlive)
        {
            return false;
        }

        activeSessionId = sessionId;
        activeKind = kind;
        released = false;
        preserveAssassinationDeathPose = false;
        preserveSurvivorStaggerOnRelease = false;
        currentAction = EnemyQTEAction.None;
        outgoingAction = EnemyQTEAction.None;
        currentEntrySequence = 0;
        outgoingEntrySequence = 0;
        entryBound = false;
        actionCompleted = false;
        endRequested = false;
        strategyDecisionPassed = false;
        rearStrategyDecisionPassed = false;
        chainsawExecutionDecisionPassed = false;
        nextChainsawExecutionDecisionTime = Time.time + strategyDecisionInterval;
        nextStrategyDecisionTime = Time.time + strategyDecisionInterval;
        nextRearStrategyDecisionTime = nextStrategyDecisionTime;
        if (kind == QTEKind.GroundFinisher && controller != null && controller.combatModule != null)
        {
            controller.combatModule.SuspendNativeCombatForGroundFinisher();
        }
        if ((kind == QTEKind.FrontGrab || kind == QTEKind.RearBiteNeck || kind == QTEKind.LegBite || kind == QTEKind.ChainsawExecution)
            && controller != null && controller.perceptionModule != null)
        {
            controller.perceptionModule.MarkPersonallyDetectedPlayer();
        }
        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue26 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            // 判空前缓存的接收者值26的动作协调器。
global::EnemyActionCoordinator receiverValue27 = receiverValue26.actionCoordinator;
            if (!global::System.Object.ReferenceEquals(receiverValue27, null))
            {
                receiverValue27.RevokePreviousAnimationCallback();
            }
        }

        behaviorWasEnabled = behaviorTree != null && behaviorTree.enabled && behaviorTree.gameObject.activeInHierarchy;
        pathAgentWasEnabled = pathAgent != null && pathAgent.enabled;
        pathAgentWasStopped = pathAgent != null && pathAgent.isStopped;
        animatorApplyRootMotion = animator != null && animator.applyRootMotion;
        if (animator != null)
        {
            qteAnimatorSpeed = animator.speed;
            // 瞬时命中停顿不能变成整段配对结束后的永久播放速度。
            if (qteAnimatorSpeed <= 0f)
            {
                qteAnimatorSpeed = 1f;
            }
        }
        else
        {
            qteAnimatorSpeed = 1f;
        }

        if (qteLayerIndex >= 0)
        {
            qteLayerWeight = animator.GetLayerWeight(qteLayerIndex);
        }
        else
        {
            qteLayerWeight = 0f;
        }

        if (controller != null)
        {
            movementMode = controller.CurrentMovementMode;
        }
        else
        {
            movementMode = EnemyMovementMode.FullyStopped;
        }

        if (body != null)
        {
            bodyWasKinematic = body.isKinematic;
            bodyUsedGravity = body.useGravity;
            bodyConstraints = body.constraints;
        }

        if (behaviorTree != null)
        {
            behaviorTree.DisableBehavior();
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue31 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue31, null))
        {
            receiverValue31.StopAndClearMovement();
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue32 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue32, null))
        {
            receiverValue32.SetMovementMode(EnemyMovementMode.FullyStopped);
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue33 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.BeginQTEGroundLock();
        }

        if (pathAgent != null)
        {
            pathAgent.isStopped = true;
            pathAgent.enabled = false;
        }

        if (animator != null)
        {
            controller.animationPresenter.SetRootMotion(false);
            controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
            StopQTEEntryLayerBlend();
            if (qteLayerIndex >= 0)
            {
                controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
            }
        }

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }

        return true;
    }

    /// <summary>BeginAction 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayState(int, int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、MonoBehaviour.StartCoroutine(IEnumerator)、VillagerQTEParticipant.BlendQTEEntryLayer(int)、VillagerQTEParticipant.IsLegalTransition(EnemyQTEAction, EnemyQTEAction)、VillagerQTEParticipant.StopQTEEntryLayerBlend()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAnimationAdapter.PlayState(int, int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、MonoBehaviour.StartCoroutine(IEnumerator)、VillagerQTEParticipant.BlendQTEEntryLayer(int)、VillagerQTEParticipant.IsLegalTransition(EnemyQTEAction, EnemyQTEAction)、VillagerQTEParticipant.StopQTEEntryLayerBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="action">动作。</param>
    internal void RecordNativePairResult(int sessionId, bool escaped)
    {
        if (sessionId == activeSessionId && !released)
        {
            currentAction = EnemyQTEAction.Failure;
            if (escaped)
            {
                currentAction = EnemyQTEAction.Success;
            }
        }
    }

    public bool BeginAction(int sessionId, EnemyQTEAction action)
    {
        if (sessionId != activeSessionId || released || animator == null)
        {
            return false;
        }

        if (!IsLegalTransition(currentAction, action))
        {
            return false;
        }

        if (currentAction != EnemyQTEAction.None && (!entryBound || !actionCompleted))
        {
            return false;
        }

        // 是否入口动作。
bool isEntryAction = currentAction == EnemyQTEAction.None && action == EnemyQTEAction.Enter;
        outgoingAction = currentAction;
        outgoingEntrySequence = currentEntrySequence;
        currentAction = action;
        currentEntrySequence = 0;
        entryBound = false;
        actionCompleted = false;
        endRequested = false;
        controller.animationPresenter.SetIntegerValue(HashQTEAction, (int)action);
        if (isEntryAction && qteLayerIndex >= 0)
        {
            // 直接进入目标片段，彻底绕过空状态到入口状态的固定时长过渡。
            StopQTEEntryLayerBlend();
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
            controller.animationPresenter.PlayState(HashQTEEnterState, qteLayerIndex, 0f);
            qteEntryLayerBlendRoutine = StartCoroutine(BlendQTEEntryLayer(sessionId));
        }
        else if (qteLayerIndex >= 0)
        {
            StopQTEEntryLayerBlend();
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
        }

        return true;
    }

    /// <summary>EndAction 的业务处理入口；本方法直接依赖 Action&lt;int, EnemyQTEAction, int, bool&gt;.Invoke(int, EnemyQTEAction, int, bool)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、object.ReferenceEquals(object, object)、VillagerController.ForceCrawlPoseAfterQTE()、VillagerQTEParticipant.StopQTEEntryLayerBlend()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, EnemyQTEAction, int, bool&gt;.Invoke(int, EnemyQTEAction, int, bool)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、object.ReferenceEquals(object, object)、VillagerController.ForceCrawlPoseAfterQTE()、VillagerQTEParticipant.StopQTEEntryLayerBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public bool EndAction(int sessionId)
    {
        if (sessionId != activeSessionId || released || animator == null)
        {
            return false;
        }

        if (currentAction != EnemyQTEAction.Success && currentAction != EnemyQTEAction.Failure)
        {
            return false;
        }

        if (!entryBound || !actionCompleted || endRequested)
        {
            return false;
        }

        endRequested = true;
        StopQTEEntryLayerBlend();
        if (preserveAssassinationDeathPose)
        {
            // 刺杀目标已经由配对动画完成倒地，不能把覆盖层降为零。
            // 否则动画器会在下一次求值时短暂露出基础层站姿，随后
            // 又被死亡保姿逻辑恢复，形成一帧腿部抽跳。
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
            // 判空前缓存的动作退出。
global::System.Action<int, global::EnemyQTEAction, int, bool> receiverValue34 = ActionExited;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                receiverValue34.Invoke(activeSessionId, currentAction, currentEntrySequence, true);
            }

            return true;
        }

        if (activeKind == QTEKind.LegBite && controller != null && controller.HasPendingCrawl)
        {
            controller.ForceCrawlPoseAfterQTE();
            controller.animationPresenter.SetBoolean(HashIsCrawl, true);
            controller.animationPresenter.SetBoolean(HashATK, false);
        }

        controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
        // 同时将 Override 层权重置零，避免 QTE→Empty 的 0.05s 混合期间
        // QTE 站立姿势与基础层爬行动画混合产生"一帧站起来"的视觉。
        // OnStateExit 仍会正常触发（Unity 不论层权重都处理状态机转换）。
        if (qteLayerIndex >= 0)
        {
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
        }

        if (activeKind == QTEKind.LegBite && controller != null && controller.HasPendingCrawl)
        {
            controller.ForceCrawlPoseAfterQTE();
        }

        return true;
    }

    /// <summary>Release 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetPlaySpeed(float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.OnDestroy()、VillagerQTEParticipant.OnDisable()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetPlaySpeed(float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public void Release(int sessionId)
    {
        if (sessionId != activeSessionId || released)
        {
            return;
        }

        if (controller != null && controller.IsDead)
        {
            ForceExitQTEForDeath(sessionId);
            return;
        }

        // 已完成动作。
EnemyQTEAction completedAction = currentAction;
        // 之前是否腿部撕咬。
bool wasLegBite = activeKind == QTEKind.LegBite;
        bool needsGroundRecovery = activeKind == QTEKind.RearGrab && completedAction == EnemyQTEAction.Success;
        // 之前是否电锯执行。
bool wasChainsawExecution = activeKind == QTEKind.ChainsawExecution;
        // 是否应当恢复爬行姿态。
bool shouldRestoreCrawlPose = wasLegBite && controller != null && controller.HasPendingCrawl;
        released = true;
        StopQTEEntryLayerBlend();
        if (animator != null)
        {
            if (shouldRestoreCrawlPose)
            {
                // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue35 = controller;
                if (!global::System.Object.ReferenceEquals(receiverValue35, null))
                {
                    receiverValue35.ForceCrawlPoseAfterQTE();
                }

                controller.animationPresenter.SetBoolean(HashIsCrawl, true);
                controller.animationPresenter.SetBoolean(HashATK, false);
            }

            controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
            if (qteLayerIndex >= 0)
            {
                controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, qteLayerWeight);
            }

            controller.animationPresenter.SetRootMotion(animatorApplyRootMotion);
            controller.animationPresenter.SetPlaySpeed(qteAnimatorSpeed);
        }

        if (body != null)
        {
            body.isKinematic = bodyWasKinematic;
            body.useGravity = bodyUsedGravity;
            body.constraints = bodyConstraints;
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue36 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue36, null))
        {
            receiverValue36.EndQTEGroundLock();
        }

        if (shouldRestoreCrawlPose)
        {
            // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue37 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue37, null))
            {
                receiverValue37.ForceCrawlPoseAfterQTE();
            }
        }

        // 必须在恢复移动模式和启用行为树之前清除动画覆盖，
        // 因为恢复原始 AnimatorController 会重置 Animator 状态机（Base Layer 回到 Locomotion，
        // IsCrawl 等参数重置为默认值）。清除后立即恢复爬行参数，确保行为树启用时
        // Animator 已处于正确的爬行状态，避免 BeginCrawlAttack 同时设置 ATK=true 和 IsCrawl=true
        // 时 Locomotion 的 Attack 转换（排在 Crawl 转换前面）先触发导致站立攻击。
        ClearAnimationVariant();
        if (preserveSurvivorStaggerOnRelease && controller != null && !controller.IsDead && controller.combatModule != null)
        {
            controller.combatModule.MaintainPairedSurvivorStaggerState(true);
        }
        if (shouldRestoreCrawlPose)
        {
            // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue38 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue38, null))
            {
                receiverValue38.ForceCrawlPoseAfterQTE();
            }
        }

        if (CanRestoreIntelligence && !keepAIDisabledForIsolatedTest)
        {
            if (!preserveSurvivorStaggerOnRelease)
            {
                controller.SetMovementMode(movementMode);
            }
            if (shouldRestoreCrawlPose)
            {
                controller.animationPresenter.SetBoolean(HashIsCrawl, true);
                controller.animationPresenter.SetBoolean(HashATK, false);
                controller.SetMovementMode(EnemyMovementMode.CrawlCodeDriven);
                controller.ForceCrawlPoseAfterQTE();
            }
        }

        if (pathAgent != null)
        {
            if (keepAIDisabledForIsolatedTest)
            {
                pathAgent.isStopped = true;
                pathAgent.enabled = false;
            }
            else if (CanRestoreIntelligence)
            {
                pathAgent.enabled = pathAgentWasEnabled;
                if (pathAgentWasEnabled)
                {
                    pathAgent.isStopped = pathAgentWasStopped;
                }
            }
        }

        if (behaviorTree != null && behaviorWasEnabled && CanRestoreIntelligence && !keepAIDisabledForIsolatedTest)
        {
            behaviorTree.EnableBehavior();
        }

        if (!isDeactivate && keepAIDisabledForIsolatedTest)
        {
            SuspendAIForIsolatedTest();
        }

        // 冷却。
float cooldown;
        if (completedAction == EnemyQTEAction.Success)
        {
            if (wasLegBite)
            {
                cooldown = legBiteSuccessCooldown;
            }
            else
            {
                if (wasChainsawExecution)
                {
                    cooldown = chainsawExecutionSuccessCooldown;
                }
                else
                {
                    cooldown = successCooldown;
                }
            }
        }
        else
        {
            if (completedAction == EnemyQTEAction.Failure)
            {
                if (wasLegBite)
                {
                    cooldown = legBiteFailureCooldown;
                }
                else
                {
                    if (wasChainsawExecution)
                    {
                        cooldown = chainsawExecutionFailureCooldown;
                    }
                    else
                    {
                        cooldown = failureCooldown;
                    }
                }
            }
            else
            {
                if (wasLegBite)
                {
                    cooldown = legBiteCancelledCooldown;
                }
                else
                {
                    if (wasChainsawExecution)
                    {
                        cooldown = chainsawExecutionCancelledCooldown;
                    }
                    else
                    {
                        cooldown = cancelledCooldown;
                    }
                }
            }
        }

        if (wasLegBite)
        {
            nextLegBiteAllowedTime = Time.time + cooldown;
        }
        else if (wasChainsawExecution)
        {
            nextChainsawExecutionAllowedTime = Time.time + cooldown;
        }
        else
        {
            nextFrontGrabAllowedTime = Time.time + cooldown;
            nextStrategyDecisionTime = nextFrontGrabAllowedTime;
            nextRearStrategyDecisionTime = nextFrontGrabAllowedTime;
        }

        strategyDecisionPassed = false;
        rearStrategyDecisionPassed = false;
        chainsawExecutionDecisionPassed = false;
        activeSessionId = 0;
        activeKind = QTEKind.FrontGrab;
        currentAction = EnemyQTEAction.None;
        outgoingAction = EnemyQTEAction.None;
        currentEntrySequence = 0;
        outgoingEntrySequence = 0;
        entryBound = false;
        actionCompleted = false;
        endRequested = false;
        if (CanRestoreIntelligence && !keepAIDisabledForIsolatedTest && !shouldRestoreCrawlPose)
        {
            if (sawKnifeHitSession == sessionId)
            {
                // 存活转重硬直时需要保留这次受击状态与 HIT=true，只有非保活分支才提前消费受击。
                sawKnifeHitSession = 0;
                if (!preserveSurvivorStaggerOnRelease)
                {
                    controller.combatModule.ConsumeHit();
                }
            }
            controller.ResumeAfterQTE(needsGroundRecovery);
        }
    }

    /// <summary>PrepareAssassinationDeathPose 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、VillagerQTEParticipant.StopQTEEntryLayerBlend()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CommitAssassinationResult(int, float)；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、VillagerQTEParticipant.StopQTEEntryLayerBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public bool PrepareAssassinationDeathPose(int sessionId)
    {
        bool allowedKind = activeKind == QTEKind.KnifeAssassination
            || activeKind == QTEKind.GroundFinisher
            || activeKind == QTEKind.FrontGrab;
        if (sessionId != activeSessionId || released || !allowedKind || animator == null)
        {
            return false;
        }

        if (activeKind != QTEKind.FrontGrab && qteLayerIndex < 0)
        {
            return false;
        }

        preserveAssassinationDeathPose = true;
        StopQTEEntryLayerBlend();
        controller.animationPresenter.SetBoolean(HashHIT, false);
        controller.animationPresenter.SetBoolean(HashATK, false);
        controller.animationPresenter.SetBoolean(HashDIE, false);
        if (qteLayerIndex >= 0)
        {
            controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
        }
        return true;
    }

    internal void CancelAssassinationDeathPose(int sessionId)
    {
        if (sessionId == activeSessionId)
        {
            preserveAssassinationDeathPose = false;
        preserveSurvivorStaggerOnRelease = false;
        }
    }

    /// <summary>ForceExitQTEForDeath 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetPlaySpeed(float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()、VillagerQTEParticipant.Release(int)；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.SetLayerWeightValue(int, float)、EnemyAnimationAdapter.SetPlaySpeed(float)、EnemyAnimationAdapter.SetRootMotion(bool)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public void ForceExitQTEForDeath(int sessionId)
    {
        if (sessionId > 0 && activeSessionId != 0 && sessionId != activeSessionId)
        {
            return;
        }

        // 是否此前具有活动会话。
bool hadActiveSession = activeSessionId != 0;
        // 是否保持暗杀姿态。
bool keepAssassinationPose = preserveAssassinationDeathPose && animator != null && qteLayerIndex >= 0;
        released = true;
        StopQTEEntryLayerBlend();
        if (animator != null)
        {
            controller.animationPresenter.SetBoolean(HashHIT, false);
            controller.animationPresenter.SetBoolean(HashATK, false);
            controller.animationPresenter.SetBoolean(HashDIE, !keepAssassinationPose);
            if (keepAssassinationPose)
            {
                // 保留配对层，让刺杀片段自然播到最终倒地姿势。
                // 这里若立刻冻结会截断脚部根位移，导致刺入瞬间双脚异常。
                controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
                controller.animationPresenter.SetRootMotion(false);
                controller.animationPresenter.SetPlaySpeed(0f);
            }
            else
            {
                controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
                if (qteLayerIndex >= 0)
                {
                    controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
                }

                // 调用前缓存的控制器的动画显示器。
global::EnemyAnimationAdapter callReceiver48 = controller.animationPresenter;
                // 分支选出的控制器的使用根运动。
bool selectedValue47;
                if (controller != null)
                {
                    selectedValue47 = controller.useRootMotion;
                }
                else
                {
                    selectedValue47 = animatorApplyRootMotion;
                }

                callReceiver48.SetRootMotion(selectedValue47);
            }
        }

        if (hadActiveSession && body != null)
        {
            body.isKinematic = bodyWasKinematic;
            body.useGravity = bodyUsedGravity;
            body.constraints = bodyConstraints;
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue49 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.EndQTEGroundLock();
        }

        if (!keepAssassinationPose)
        {
            ClearAnimationVariant();
        }

        if (animator != null)
        {
            controller.animationPresenter.SetBoolean(HashHIT, false);
            controller.animationPresenter.SetBoolean(HashATK, false);
            controller.animationPresenter.SetBoolean(HashDIE, !keepAssassinationPose);
            if (keepAssassinationPose)
            {
                controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 1f);
            }
            else
            {
                controller.animationPresenter.SetIntegerValue(HashQTEAction, 0);
                if (qteLayerIndex >= 0)
                {
                    controller.animationPresenter.SetLayerWeightValue(qteLayerIndex, 0f);
                }
            }
        }

        if (pathAgent != null)
        {
            pathAgent.isStopped = true;
            pathAgent.enabled = false;
        }

        if (behaviorTree != null)
        {
            behaviorTree.DisableBehavior();
        }

        activeSessionId = 0;
        activeKind = QTEKind.FrontGrab;
        currentAction = EnemyQTEAction.None;
        outgoingAction = EnemyQTEAction.None;
        currentEntrySequence = 0;
        outgoingEntrySequence = 0;
        entryBound = false;
        actionCompleted = false;
        endRequested = false;
        if (!keepAssassinationPose)
        {
            preserveAssassinationDeathPose = false;
        preserveSurvivorStaggerOnRelease = false;
        }
    }

    /// <summary>NotifyBiteContactFromAnimation 的业务处理入口；本方法直接依赖 Action&lt;int&gt;.Invoke(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyQTEAnimationEventRelay.OnQTEBiteContact()；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyBiteContactFromAnimation()
    {
        if (activeSessionId == 0 || currentAction != EnemyQTEAction.Failure)
        {
            return;
        }

        // 判空前缓存的撕咬接触。
global::System.Action<int> receiverValue50 = BiteContact;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.Invoke(activeSessionId);
        }
    }

    /// <summary>NotifyAssassinationContactFromAnimation 的业务处理入口；本方法直接依赖 Action&lt;int&gt;.Invoke(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyQTEAnimationEventRelay.OnQTEAssassinationContact()；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyAssassinationContactFromAnimation()
    {
        if (activeSessionId == 0 || activeKind != QTEKind.KnifeAssassination || (currentAction != EnemyQTEAction.Enter && currentAction != EnemyQTEAction.Success))
        {
            return;
        }

        // 判空前缓存的暗杀接触。
global::System.Action<int> receiverValue51 = AssassinationContact;
        if (!global::System.Object.ReferenceEquals(receiverValue51, null))
        {
            receiverValue51.Invoke(activeSessionId);
        }
    }

    /// <summary>ReleaseBodyTechniqueReservation 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetPlaySpeed(float)、Mathf.Approximately(float, float)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerQTEParticipant.SuspendAIForIsolatedTest()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)、VillagerQTEParticipant.CancelBodyTechnique(int)；下游（含内部回调体） EnemyAnimationAdapter.SetPlaySpeed(float)、Mathf.Approximately(float, float)、object.ReferenceEquals(object, object)、VillagerController.EndQTEGroundLock()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerQTEParticipant.SuspendAIForIsolatedTest()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ReleaseBodyTechniqueReservation()
    {
        if (!bodyTechniqueReserved)
        {
            activeBodyTechniqueSessionId = 0;
            return;
        }

        // 已释放会话编号。
int releasedSessionId = activeBodyTechniqueSessionId;
        bodyTechniqueReserved = false;
        activeBodyTechniqueSessionId = 0;
        bodyTechniqueOpportunityOpen = false;
        bodyTechniqueOpportunityExpiresAt = 0f;
        nextBodyTechniqueAllowedTime = Time.time + bodyTechniqueCooldown;
        if (animator != null)
        {
            // 调用前缓存的控制器的动画显示器。
global::EnemyAnimationAdapter callReceiver53 = controller.animationPresenter;
            // 分支选出的1f。
float selectedValue52;
            if (Mathf.Approximately(bodyTechniqueAnimatorSpeed, 0f))
            {
                selectedValue52 = 1f;
            }
            else
            {
                selectedValue52 = bodyTechniqueAnimatorSpeed;
            }

            callReceiver53.SetPlaySpeed(selectedValue52);
        }

        // 判空前缓存的村民敌人控制器。
global::VillagerController receiverValue54 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue54, null))
        {
            receiverValue54.EndQTEGroundLock();
        }

        if (CanRestoreIntelligence)
        {
            bool preserveCombatReactionMode = controller != null
                && (controller.IsDead
                    || controller.CurrentState == VillagerState.Staggered
                    || controller.CurrentState == VillagerState.Crawl
                    || (controller.combatModule != null && controller.combatModule.NativeCombatActive));
            if (!preserveCombatReactionMode)
            {
                controller.SetMovementMode(bodyTechniqueMovementMode);
            }
        }

        if (CanRestoreIntelligence && pathAgent != null && bodyTechniquePathWasEnabled)
        {
            pathAgent.isStopped = bodyTechniquePathWasStopped;
        }

        if (behaviorTree != null && bodyTechniqueBehaviorWasEnabled && CanRestoreIntelligence && !keepAIDisabledForIsolatedTest)
        {
            behaviorTree.EnableBehavior();
        }

        if (!isDeactivate && keepAIDisabledForIsolatedTest)
        {
            SuspendAIForIsolatedTest();
        }

        if (releasedSessionId != 0)
        {
            strategyDecisionPassed = false;
            rearStrategyDecisionPassed = false;
        }
    }

    /// <summary>HandleActionEntered 的业务处理入口；本方法直接依赖 Action&lt;int, EnemyQTEAction, int&gt;.Invoke(int, EnemyQTEAction, int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, EnemyQTEAction, int&gt;.Invoke(int, EnemyQTEAction, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画控制器。</param>
    /// <param name="action">动作。</param>
    /// <param name="sequence">序号。</param>
    private void HandleActionEntered(Animator source, EnemyQTEAction action, int sequence)
    {
        if (source != animator || activeSessionId == 0 || released)
        {
            return;
        }

        if (entryBound || action != currentAction)
        {
            return;
        }

        entryBound = true;
        currentEntrySequence = sequence;
        // 判空前缓存的动作进入。
global::System.Action<int, global::EnemyQTEAction, int> receiverValue55 = ActionEntered;
        if (!global::System.Object.ReferenceEquals(receiverValue55, null))
        {
            receiverValue55.Invoke(activeSessionId, action, sequence);
        }
    }

    /// <summary>HandleActionCompleted 的业务处理入口；本方法直接依赖 Action&lt;int, EnemyQTEAction, int&gt;.Invoke(int, EnemyQTEAction, int)、EnemyAnimationAdapter.SetPlaySpeed(float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, EnemyQTEAction, int&gt;.Invoke(int, EnemyQTEAction, int)、EnemyAnimationAdapter.SetPlaySpeed(float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画控制器。</param>
    /// <param name="action">动作。</param>
    /// <param name="sequence">序号。</param>
    private void HandleActionCompleted(Animator source, EnemyQTEAction action, int sequence)
    {
        if (source != animator || activeSessionId == 0 || released)
        {
            return;
        }

        if (!entryBound || actionCompleted || action != currentAction || sequence != currentEntrySequence)
        {
            return;
        }

        actionCompleted = true;
        // 判空前缓存的动作完成。
global::System.Action<int, global::EnemyQTEAction, int> receiverValue56 = ActionCompleted;
        if (!global::System.Object.ReferenceEquals(receiverValue56, null))
        {
            receiverValue56.Invoke(activeSessionId, action, sequence);
        }

        if (preserveAssassinationDeathPose && action == EnemyQTEAction.Success && animator != null)
        {
            // 完成事件触发后立即冻结在配对动作的最终采样帧，
            // 阻止状态机在下一帧继续混出普通站姿而拉扯腿部骨骼。
            controller.animationPresenter.SetPlaySpeed(0f);
        }
    }

    /// <summary>HandleActionInterrupted 的业务处理入口；本方法直接依赖 Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画控制器。</param>
    /// <param name="action">动作。</param>
    /// <param name="sequence">序号。</param>
    private void HandleActionInterrupted(Animator source, EnemyQTEAction action, int sequence)
    {
        if (source != animator || activeSessionId == 0 || released)
        {
            return;
        }

        if (action == outgoingAction && sequence == outgoingEntrySequence)
        {
            return;
        }

        if (action != currentAction || sequence != currentEntrySequence)
        {
            return;
        }

        // 判空前缓存的已失效。
global::System.Action<int, global::QTECancelReason> receiverValue57 = Invalidated;
        if (!global::System.Object.ReferenceEquals(receiverValue57, null))
        {
            receiverValue57.Invoke(activeSessionId, QTECancelReason.AnimationInterrupted);
        }
    }

    /// <summary>HandleActionExited 的业务处理入口；本方法直接依赖 Action&lt;int, EnemyQTEAction, int, bool&gt;.Invoke(int, EnemyQTEAction, int, bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, EnemyQTEAction, int, bool&gt;.Invoke(int, EnemyQTEAction, int, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画控制器。</param>
    /// <param name="action">动作。</param>
    /// <param name="sequence">序号。</param>
    /// <param name="wasCompleted">之前是否已完成。</param>
    private void HandleActionExited(Animator source, EnemyQTEAction action, int sequence, bool wasCompleted)
    {
        if (source != animator || activeSessionId == 0 || released)
        {
            return;
        }

        if (action == outgoingAction && sequence == outgoingEntrySequence)
        {
            return;
        }

        if (action != currentAction || sequence != currentEntrySequence)
        {
            return;
        }

        // 判空前缓存的动作退出。
global::System.Action<int, global::EnemyQTEAction, int, bool> receiverValue58 = ActionExited;
        if (!global::System.Object.ReferenceEquals(receiverValue58, null))
        {
            receiverValue58.Invoke(activeSessionId, action, sequence, wasCompleted);
        }
    }

    /// <summary>HandleControllerDied 的业务处理入口；本方法直接依赖 Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int, QTECancelReason&gt;.Invoke(int, QTECancelReason)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源村民敌人控制器。</param>
    private void HandleControllerDied(VillagerController source)
    {
        if (activeSessionId != 0 && !preserveAssassinationDeathPose)
        {
            // 判空前缓存的已失效。
global::System.Action<int, global::QTECancelReason> receiverValue59 = Invalidated;
            if (!global::System.Object.ReferenceEquals(receiverValue59, null))
            {
                receiverValue59.Invoke(activeSessionId, QTECancelReason.EnemyDied);
            }
        }
    }

    /// <summary>HasIntegerParameter 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.EnsureParameterCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)；下游（含内部回调体） VillagerQTEParticipant.EnsureParameterCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameterName">参数名称。</param>
    private bool HasIntegerParameter(string parameterName)
    {
        EnsureParameterCache();
        return parameterName == "QTEAction" && cachedQTEAction;
    }

    // 没有智能管理器的隔离测试场景也保留候选注册，避免兼容路径逐帧查找。
    /// <summary>生效参与者集合。只读字段，初始化后不由普通写入口替换。</summary>
    internal static readonly System.Collections.Generic.List<VillagerQTEParticipant> ActiveParticipants = new();
    /// <summary>ResetPromptCandidates 的业务处理入口；本方法直接依赖 List&lt;VillagerQTEParticipant&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;VillagerQTEParticipant&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPromptCandidates() => ActiveParticipants.Clear();
    /// <summary>RegisterPromptCandidate 的业务处理入口；本方法直接依赖 EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Add(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Contains(VillagerQTEParticipant)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.OnEnable()、VillagerQTEParticipant.Start()；下游（含内部回调体） EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Add(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Contains(VillagerQTEParticipant)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RegisterPromptCandidate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!ActiveParticipants.Contains(this))
        {
            ActiveParticipants.Add(this);
        }

        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.RegisterPromptCandidate(this);
        }
    }

    /// <summary>缓存参数控制器。</summary>
    private RuntimeAnimatorController cachedParameterController;
    /// <summary>parameterCacheInitialized 表示“参数缓存是否已初始化”；cachedQTEAction 表示“是否缓存配对动作动作”。</summary>
    private bool parameterCacheInitialized, cachedQTEAction;
    /// <summary>EnsureParameterCache 的业务处理入口；本方法直接依赖 Animator.GetLayerIndex(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、VillagerQTEParticipant.HasIntegerParameter(string)、VillagerQTEParticipant.Start()；下游（含内部回调体） Animator.GetLayerIndex(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureParameterCache()
    {
        // 当前运行时动画控制器控制器。
RuntimeAnimatorController current;
        if (animator != null)
        {
            current = animator.runtimeAnimatorController;
        }
        else
        {
            current = null;
        }

        if (parameterCacheInitialized && object.ReferenceEquals(current, cachedParameterController))
        {
            return;
        }

        parameterCacheInitialized = true;
        cachedParameterController = current;
        cachedQTEAction = false;
        if (animator != null && current != null)
        {
            qteLayerIndex = animator.GetLayerIndex(QTELayerName);
        }
        else
        {
            qteLayerIndex = -1;
        }

        if (animator == null || current == null)
        {
            return;
        }

        // parameter（参数）：每轮从动画控制器的参数集合取得一个元素，仅代表当前这一项。使用位置：VillagerQTEParticipant.EnsureParameterCache()。
foreach (var parameter in animator.parameters)
        {
            if (parameter.nameHash == HashQTEAction && parameter.type == AnimatorControllerParameterType.Int)
            {
                cachedQTEAction = true;
            }
        }
    }

    /// <summary>IsLegalTransition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.BeginAction(int, EnemyQTEAction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">当前敌人配对动作动作。</param>
    /// <param name="next">下一个敌人配对动作动作。</param>
    private static bool IsLegalTransition(EnemyQTEAction current, EnemyQTEAction next)
    {
        return (current == EnemyQTEAction.None && next == EnemyQTEAction.Enter) || (current == EnemyQTEAction.Enter && next == EnemyQTEAction.Hold) || (current == EnemyQTEAction.Enter && next == EnemyQTEAction.Success) || (current == EnemyQTEAction.Hold && (next == EnemyQTEAction.Success || next == EnemyQTEAction.Failure));
    }

    /// <summary>SuspendAIForIsolatedTest 的业务处理入口；本方法直接依赖 VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.LateUpdate()、VillagerQTEParticipant.Release(int)、VillagerQTEParticipant.ReleaseBodyTechniqueReservation()、VillagerQTEParticipant.Start()；下游（含内部回调体） VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SuspendAIForIsolatedTest()
    {
        if (behaviorTree != null)
        {
            behaviorTree.DisableBehavior();
        }

        if (pathAgent != null)
        {
            pathAgent.isStopped = true;
            pathAgent.enabled = false;
        }

        if (controller != null)
        {
            controller.IsActive = false;
            controller.StopAndClearMovement();
            controller.SetMovementMode(EnemyMovementMode.FullyStopped);
        }
    }
}
