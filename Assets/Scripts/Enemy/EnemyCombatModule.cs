using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 KnifeEnemyAttackProfile；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀敌人攻击段")]
public sealed class KnifeEnemyAttackProfile
{
    public const float RootSampleInterval = 1f / 30f;
    /// <summary>是否可招架。</summary>
    [Header("本招式的接触与招架")]
    [UnityEngine.Serialization.FormerlySerializedAs("可招架")]
    [UnityEngine.InspectorName("可招架")]
    [Newtonsoft.Json.JsonProperty("可招架")]
    public bool canParry = true;
    /// <summary>类别。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("类别")]
    [UnityEngine.InspectorName("类别")]
    [Newtonsoft.Json.JsonProperty("类别")]
    public GameArchitecture.KnifeIncomingAttackCategory category;
    /// <summary>接触归一化点。</summary>
    [Range(.05f, .95f)]
    [UnityEngine.Serialization.FormerlySerializedAs("接触归一化点")]
    [UnityEngine.InspectorName("接触归一化点")]
    [Newtonsoft.Json.JsonProperty("接触归一化点")]
    public float contactNormalizedPoint = .55f;
    /// <summary>提示提前。</summary>
    [Range(0, .5f)]
    [UnityEngine.Serialization.FormerlySerializedAs("提示提前")]
    [UnityEngine.InspectorName("提示提前")]
    [Newtonsoft.Json.JsonProperty("提示提前")]
    public float promptEarly = .18f;
    /// <summary>接触距离。</summary>
    [Min(.1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("接触距离")]
    [UnityEngine.InspectorName("接触距离")]
    [Newtonsoft.Json.JsonProperty("接触距离")]
    public float contactDistance = 1.25f;
    [HideInInspector] public float swingStartDistance;
    [Tooltip("正面站立目标的最大起手根节点距离。零表示沿用原有起手规则。")]
    [Min(0f)] public float maximumStartDistance;
    [Tooltip("本招边缘接触容差，最多五厘米。")]
    [Range(0f, .05f)] public float contactPadding;
    [Tooltip("挥击有效段结束前减少的横向根位移比例。")]
    [Range(0f, 1f)] public float windupLateralReduction;
    [HideInInspector] public Vector3[] planarRootTrajectory = Array.Empty<Vector3>();
    [HideInInspector] public bool usePoseContact;
    /// <summary>接触角度。</summary>
    [Range(10, 180)]
    [UnityEngine.Serialization.FormerlySerializedAs("接触角度")]
    [UnityEngine.InspectorName("接触角度")]
    [Newtonsoft.Json.JsonProperty("接触角度")]
    public float contactAngle = 100;
    /// <summary>来源动画片段。</summary>
    [Header("逐招标定的接触轨迹；时间使用源片段秒数")]
    public AnimationClip sourceClip;
    /// <summary>接触线段集合。</summary>
    public KnifeEnemyAttackSegment[] contactSegments = Array.Empty<KnifeEnemyAttackSegment>();
}

/// <summary>敌人行为、动作与通行组件 EnemyCombatModule，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>敌人攻击与接触结算宿主，结合角色当前动作、时序及命中信息提交战斗事实；小刀接触通过既有攻击上下文与招架入口通信。</remarks>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人战斗模块")]
public sealed partial class EnemyCombatModule : MonoBehaviour
{
    /// <summary>宿主。</summary>
    [Header("角色组装引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("宿主")]
    [UnityEngine.InspectorName("宿主")]
    public VillagerController host;
    /// <summary>小刀攻击配置。</summary>
    [Header("站立攻击对应的招架表现")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀攻击配置")]
    [UnityEngine.InspectorName("小刀攻击配置")]
    private KnifeEnemyAttackProfile[] knifeAttackConfiguration = Array.Empty<KnifeEnemyAttackProfile>();
    [SerializeField] private KnifeEnemyAttackProfile[] standingWeaponAttackProfiles = Array.Empty<KnifeEnemyAttackProfile>();

    private KnifeEnemyAttackProfile[] ActiveKnifeAttackProfiles
    {
        get
        {
            if (standingWeaponAttackStartRange > 0f)
            {
                return standingWeaponAttackProfiles;
            }
            return knifeAttackConfiguration;
        }
    }

    internal const int ArmedDisarmHitDirection = 6;
    internal const int ArmedStandingLeftLegHitDirection = 7;
    internal const int ArmedStandingRightLegHitDirection = 8;
    internal const int ArmedFallingLeftLegHitDirection = 9;
    internal const int ArmedFallingRightLegHitDirection = 10;
    /// <summary>是否具有小刀攻击配置档集合。</summary>
    public bool HasKnifeAttackProfiles
    {
        get
        {
            var profiles = ActiveKnifeAttackProfiles;
            return profiles != null && profiles.Length > 0;
        }
    }

#if UNITY_EDITOR
    /// <summary>ConfigureKnifeAttackProfiles 的业务处理入口；本方法直接依赖 Array.Clone()、Array.Empty&lt;KnifeEnemyAttackProfile&gt;()、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.BindingEnemy(VillagerController)、KnifeContactProfileBuilder.Build()；下游（含内部回调体） Array.Clone()、Array.Empty&lt;KnifeEnemyAttackProfile&gt;()、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profiles">配置档集合。</param>
    public void ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[] profiles)
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("攻击配置只能在编辑模式制作。");
        }

        if (profiles == null)
        {
            knifeAttackConfiguration = Array.Empty<KnifeEnemyAttackProfile>();
        }
        else
        {
            knifeAttackConfiguration = (KnifeEnemyAttackProfile[])profiles.Clone();
        }
    }

    internal void ConfigureStandingWeaponAttacks(KnifeEnemyAttackProfile[] profiles, float standingWeaponRange)
    {
        standingWeaponAttackProfiles = (KnifeEnemyAttackProfile[])profiles.Clone();
        standingWeaponAttackStartRange = standingWeaponRange;
    }
#endif

    internal void SwitchToUnarmedAttacks()
    {
        standingWeaponAttackStartRange = 0f;
        ResetKnifeContactTimeline();
    }

    /// <summary>小刀传入攻击编号。</summary>
    public long KnifeIncomingAttackId { get; private set; }

    /// <summary>当前小刀攻击移动。</summary>
    private int currentKnifeAttackMove;
    /// <summary>当前小刀配置。</summary>
    private KnifeEnemyAttackProfile CurrentKnifeConfiguration
    {
        get
        {
            var profiles = ActiveKnifeAttackProfiles;
            if (profiles != null && currentKnifeAttackMove >= 0 && currentKnifeAttackMove < profiles.Length)
            {
                return profiles[currentKnifeAttackMove];
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>CreateKnifeIncomingAttack 的业务处理入口；本方法直接依赖 KnifeIncomingAttackContext.KnifeIncomingAttackContext()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.LateUpdate()、EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） KnifeIncomingAttackContext.KnifeIncomingAttackContext()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="segment">线段。</param>
    /// <param name="damage">伤害。</param>
    /// <param name="contact">接触。</param>
    /// <param name="direction">方向。</param>
    private KnifeIncomingAttackContext CreateKnifeIncomingAttack(int segment, float damage, Vector3 contact, Vector3 direction)
    {
        // 本次新建的小刀传入攻击上下文。
global::KnifeIncomingAttackContext createdValue2 = new KnifeIncomingAttackContext();
        createdValue2.attacker = host;
        createdValue2.attackId = KnifeIncomingAttackId;
        createdValue2.attackSegment = segment;
        createdValue2.damage = damage;
        createdValue2.contactPosition = contact;
        createdValue2.direction = direction;
        createdValue2.contactTime = Time.timeAsDouble;
        createdValue2.canParry = enableKnifeParry && chainsawBridge == null && CurrentState == VillagerState.Attack && CurrentKnifeConfiguration != null && CurrentKnifeConfiguration.canParry;
        if (CurrentKnifeConfiguration != null)
        {
            createdValue2.category = CurrentKnifeConfiguration.category;
        }
        else
        {
            createdValue2.category = GameArchitecture.KnifeIncomingAttackCategory.CloseRange;
        }

        return createdValue2;
    }

    /// <summary>中文含义：战斗模块。</summary>
    private EnemyCombatModule CombatModule => host.combatModule;
    /// <summary>感知模块。</summary>
    private EnemyPerceptionModule PerceptionModule => host.perceptionModule;
    /// <summary>动作协调器。</summary>
    private EnemyActionCoordinator ActionCoordinator => host.actionCoordinator;
    /// <summary>动画显示器。</summary>
    private EnemyAnimationAdapter AnimationPresenter => host.animationPresenter;

    /// <summary>是否爬行姿态。</summary>
    private bool crawlPose;
    /// <summary>是否内部爬行姿态。</summary>
    public bool IsInCrawlPose => crawlPose;

    /// <summary>是否存在待处理的命中。</summary>
    private bool pendingHit;
    /// <summary>是否读取待处理命中。</summary>
    internal bool ReadPendingHit => pendingHit;

    /// <summary>是否存在待处理的爬行。</summary>
    private bool pendingCrawl;
    /// <summary>是否读取待处理爬行。</summary>
    internal bool ReadPendingCrawl => pendingCrawl;

    /// <summary>是否存在待处理的起身。</summary>
    private bool pendingStandUp;
    /// <summary>是否读取待处理起身。</summary>
    internal bool ReadPendingStandUp => pendingStandUp;

    /// <summary>是否存在待处理的绊倒。</summary>
    private bool pendingTripOver;
    /// <summary>是否读取待处理绊倒。</summary>
    internal bool ReadPendingTripOver => pendingTripOver;

    /// <summary>是否攻击结束。</summary>
    private bool attackFinished;
    /// <summary>是否读取攻击结束。</summary>
    internal bool ReadAttackFinished => attackFinished;

    /// <summary>是否命中结束。</summary>
    private bool hitFinished;
    /// <summary>是否读取命中结束。</summary>
    internal bool ReadHitFinished => hitFinished;

    /// <summary>是否起身结束。</summary>
    private bool standUpFinished;
    /// <summary>是否读取起身结束。</summary>
    internal bool ReadStandUpFinished => standUpFinished;

    /// <summary>待处理命中方向。</summary>
    private int pendingHitDirection;
    /// <summary>读取待处理命中方向。</summary>
    internal int ReadPendingHitDirection => pendingHitDirection;

    /// <summary>待处理硬直方向。</summary>
    private int pendingStaggerDirection;
    /// <summary>读取待处理硬直方向。</summary>
    internal int ReadPendingStaggerDirection => pendingStaggerDirection;

    /// <summary>待处理硬直级别。</summary>
    private int pendingStaggerLevel;
    /// <summary>读取待处理硬直级别。</summary>
    internal int ReadPendingStaggerLevel => pendingStaggerLevel;

    /// <summary>是否存在待处理的站立硬直。</summary>
    private bool pendingStandingStagger;
    /// <summary>是否读取待处理站立硬直。</summary>
    internal bool ReadPendingStandingStagger => pendingStandingStagger;

    /// <summary>是否存在待处理的站立身体硬直体术。</summary>
    private bool pendingStandingStaggerBodyTechnique;
    /// <summary>是否读取待处理站立身体硬直体术。</summary>
    internal bool ReadPendingStandingStaggerBodyTechnique => pendingStandingStaggerBodyTechnique;

    /// <summary>是否当前站立身体硬直体术。</summary>
    private bool currentStandingStaggerBodyTechnique;
    /// <summary>是否读取当前站立身体硬直体术。</summary>
    internal bool ReadCurrentStandingStaggerBodyTechnique => currentStandingStaggerBodyTechnique;

    /// <summary>体术窗口结束时间。</summary>
    private float bodyTechniqueWindowEndTime;
    /// <summary>读取体术窗口结束时间。</summary>
    internal float ReadBodyTechniqueWindowEndTime => bodyTechniqueWindowEndTime;

    /// <summary>是否延后的站立命中完成。</summary>
    private bool deferredStandingHitCompletion;
    /// <summary>是否读取延后的站立命中完成。</summary>
    internal bool ReadDeferredStandingHitCompletion => deferredStandingHitCompletion;

    /// <summary>上一次攻击时间。</summary>
    private float lastAttackTime = float.NegativeInfinity;
    /// <summary>读取上一次攻击时间。</summary>
    internal float ReadLastAttackTime => lastAttackTime;

    /// <summary>累计腿部伤害。</summary>
    private float accumulatedLegDamage;
    /// <summary>读取累计腿部伤害。</summary>
    internal float ReadAccumulatedLegDamage => accumulatedLegDamage;
    /// <summary>电锯人累计单膝跪地硬直值（按部位加权：腿部 1.6x、头部 1.15x、其余 0.65x）。</summary>
    private float sawmanKneelDamage;

    /// <summary>累计韧性伤害。</summary>
    private float accumulatedPoiseDamage;
    /// <summary>读取累计韧性伤害。</summary>
    internal float ReadAccumulatedPoiseDamage => accumulatedPoiseDamage;

    /// <summary>上一次韧性伤害时间。</summary>
    private float lastPoiseDamageTime = float.NegativeInfinity;
    /// <summary>读取上一次韧性伤害时间。</summary>
    internal float ReadLastPoiseDamageTime => lastPoiseDamageTime;

    /// <summary>受击反应免疫直到。</summary>
    private float hitReactionImmuneUntil;
    /// <summary>读取受击反应免疫直到。</summary>
    internal float ReadHitReactionImmuneUntil => hitReactionImmuneUntil;

    /// <summary>下一次暴击硬直允许的时间。</summary>
    private float nextCriticalStaggerAllowedTime;
    /// <summary>读取下一次暴击硬直允许的时间。</summary>
    internal float ReadNextCriticalStaggerAllowedTime => nextCriticalStaggerAllowedTime;

    /// <summary>是否永久爬行。</summary>
    private bool permanentCrawl;
    /// <summary>是否读取永久爬行。</summary>
    internal bool ReadPermanentCrawl => permanentCrawl;

    /// <summary>攻击命中遮罩。</summary>
    private int attackHitMask;
    /// <summary>常规挥击命中是否具有恢复窗口。</summary>
    private bool normalSwingHitHasRecoverWindow;
    /// <summary>读取攻击命中遮罩。</summary>
    internal int ReadAttackHitMask => attackHitMask;

    /// <summary>站立招架窗口是否打开。</summary>
    private bool standingParryWindowOpen;
    /// <summary>读取站立招架窗口是否打开。</summary>
    internal bool ReadStandingParryWindowOpen => standingParryWindowOpen;

    /// <summary>强制下一次命中方向。</summary>
    private int forcedNextHitDirection = -1;
    /// <summary>读取强制下一次命中方向。</summary>
    internal int ReadForcedNextHitDirection => forcedNextHitDirection;

    /// <summary>硬直恢复开始时间。</summary>
    private float staggerRecoveryStartedTime = float.NegativeInfinity;
    /// <summary>读取硬直恢复开始时间。</summary>
    internal float ReadStaggerRecoveryStartedTime => staggerRecoveryStartedTime;

    /// <summary>攻击恢复位置。</summary>
    private Vector3 attackRecoveryPosition;
    /// <summary>读取攻击恢复位置。</summary>
    internal Vector3 ReadAttackRecoveryPosition => attackRecoveryPosition;

    /// <summary>攻击恢复旋转。</summary>
    private Quaternion attackRecoveryRotation;
    /// <summary>读取攻击恢复旋转。</summary>
    internal Quaternion ReadAttackRecoveryRotation => attackRecoveryRotation;

    /// <summary>攻击恢复是否仍待处理。</summary>
    private bool attackRecoveryPending;
    /// <summary>读取攻击恢复是否仍待处理。</summary>
    internal bool ReadAttackRecoveryPending => attackRecoveryPending;

    /// <summary>起身稳定化结束点所在。</summary>
    private float standUpStabilizationEndsAt;
    /// <summary>读取起身稳定化结束点所在。</summary>
    internal float ReadStandUpStabilizationEndsAt => standUpStabilizationEndsAt;
    /// <summary>是否可以评估爬行战斗动作。</summary>
    private bool CanEvaluateCrawlCombatAction => host.CanEvaluateCrawlCombatAction;
    /// <summary>是否可以评估站立战斗动作。</summary>
    private bool CanEvaluateStandingCombatAction => host.CanEvaluateStandingCombatAction;
    /// <summary>是否可以日志移动诊断。</summary>
    private bool CanLogMovementDiagnostic => host.ArchitectureReadCanLogMovementDiagnostic;
    /// <summary>当前移动模式。</summary>
    private EnemyMovementMode CurrentMovementMode => host.ArchitectureReadCurrentMovementMode;
    /// <summary>当前状态。</summary>
    private VillagerState CurrentState => host.ArchitectureReadCurrentState;
    /// <summary>攻击的哈希编号。</summary>
    private static int HashATK => VillagerController.ArchitectureReadHashATK;
    /// <summary>攻击01状态的哈希编号。</summary>
    private static int HashAttack01State => VillagerController.ArchitectureReadHashAttack01State;
    /// <summary>攻击02状态的哈希编号。</summary>
    private static int HashAttack02State => VillagerController.ArchitectureReadHashAttack02State;
    /// <summary>攻击03状态的哈希编号。</summary>
    private static int HashAttack03State => VillagerController.ArchitectureReadHashAttack03State;
    /// <summary>攻击索引的哈希编号。</summary>
    private static int HashAttackIndex => VillagerController.ArchitectureReadHashAttackIndex;
    /// <summary>是否可以绊倒的哈希编号。</summary>
    private static int HashCanTripOver => VillagerController.ArchitectureReadHashCanTripOver;
    /// <summary>命中的哈希编号。</summary>
    private static int HashHIT => VillagerController.ArchitectureReadHashHIT;
    /// <summary>命中方向的哈希编号。</summary>
    private static int HashHitDir => VillagerController.ArchitectureReadHashHitDir;
    /// <summary>命中触发器的哈希编号。</summary>
    private static int HashHitTrigger => VillagerController.ArchitectureReadHashHitTrigger;
    /// <summary>是否爬行的哈希编号。</summary>
    private static int HashIsCrawl => VillagerController.ArchitectureReadHashIsCrawl;
    /// <summary>移动速度的哈希编号。</summary>
    private static int HashMoveSpeed => VillagerController.ArchitectureReadHashMoveSpeed;
    /// <summary>搜索模式的哈希编号。</summary>
    private static int HashSearchMode => VillagerController.ArchitectureReadHashSearchMode;
    /// <summary>硬直方向的哈希编号。</summary>
    private static int HashStaggerDirection => VillagerController.ArchitectureReadHashStaggerDirection;
    /// <summary>硬直级别的哈希编号。</summary>
    private static int HashStaggerLevel => VillagerController.ArchitectureReadHashStaggerLevel;
    /// <summary>硬直触发器的哈希编号。</summary>
    private static int HashStaggerTrigger => VillagerController.ArchitectureReadHashStaggerTrigger;
    /// <summary>转向方向的哈希编号。</summary>
    private static int HashTurnDir => VillagerController.ArchitectureReadHashTurnDir;
    /// <summary>是否攻击就绪。</summary>
    private bool IsAttackReady => host.IsAttackReady;
    /// <summary>是否死亡。</summary>
    private bool IsDead => host.IsDead;
    /// <summary>是否配对动作受控。</summary>
    private bool IsQTEControlled => host.IsQTEControlled;
    /// <summary>玩家。</summary>
    private LeonController Player => host.Player;
    /// <summary>玩家距离。</summary>
    private float PlayerDistance => host.PlayerDistance;
    /// <summary>寻路代理。</summary>
    private PathAgent agent => host.ArchitectureReadAgent;
    /// <summary>动画控制器。</summary>
    private Animator anim => host.ArchitectureReadAnim;
    /// <summary>攻击接触留白。</summary>
    private float attackContactPadding => host.attackContactPadding;
    /// <summary>攻击伤害。</summary>
    private float attackDamage => host.attackDamage;
    /// <summary>攻击命中角度。</summary>
    private float attackHitAngle => host.attackHitAngle;
    /// <summary>攻击命中范围。</summary>
    private float attackHitRange => host.attackHitRange;
    /// <summary>攻击范围极小容差。</summary>
    private static float attackRangeEpsilon => VillagerController.ArchitectureReadAttackRangeEpsilon;
    /// <summary>攻击起始范围。</summary>
    [SerializeField] private float standingWeaponAttackStartRange;

    private float attackStartRange => host.attackStartRange;

    internal float StandingAttackStartRange
    {
        get
        {
            float calibratedRange = CalibratedStandingAttackRange();
            if (calibratedRange > 0f)
            {
                return calibratedRange;
            }

            if (standingWeaponAttackStartRange > 0f)
            {
                return standingWeaponAttackStartRange;
            }

            return attackStartRange;
        }
    }
    /// <summary>警觉记忆持续时间。</summary>
    private float awarenessMemoryDuration => host.awarenessMemoryDuration;
    /// <summary>行为树。</summary>
    private BehaviorTree behaviorTree => host.ArchitectureReadBehaviorTree;
    /// <summary>身体暴击硬直概率。</summary>
    private float bodyCriticalStaggerChance => host.bodyCriticalStaggerChance;
    /// <summary>缓存玩家。</summary>
    private LeonController cachedPlayer => host.ArchitectureReadCachedPlayer;
    /// <summary>电锯桥接。</summary>
    private ChainsawManAnimationBridge chainsawBridge => host.ArchitectureReadChainsawBridge;
    /// <summary>电锯友方伤害角度。</summary>
    private float chainsawFriendlyFireAngle => host.chainsawFriendlyFireAngle;
    /// <summary>电锯友方伤害伤害倍率。</summary>
    private float chainsawFriendlyFireDamageMultiplier => host.chainsawFriendlyFireDamageMultiplier;
    /// <summary>电锯友方伤害高度容差。</summary>
    private float chainsawFriendlyFireHeightTolerance => host.chainsawFriendlyFireHeightTolerance;
    /// <summary>电锯友伤最多命中目标数。</summary>
    private int chainsawFriendlyFireMaxTargets => host.chainsawFriendlyFireMaxTargets;
    /// <summary>电锯友方伤害范围。</summary>
    private float chainsawFriendlyFireRange => host.chainsawFriendlyFireRange;
    /// <summary>碰撞体控制器。</summary>
    private EnemyColliderController colliderCtrl => host.ArchitectureReadColliderCtrl;
    /// <summary>是否战斗已接战。</summary>
    private bool combatEngaged => host.perceptionModule.ReadCombatEngaged;
    /// <summary>战斗保持退出范围。</summary>
    private float combatHoldExitRange => host.combatHoldExitRange;
    /// <summary>爬行攻击起始角度。</summary>
    private float crawlAttackStartAngle => host.crawlAttackStartAngle;
    /// <summary>爬行战斗竖直容差。</summary>
    private float crawlCombatVerticalTolerance => host.crawlCombatVerticalTolerance;
    /// <summary>暴击硬直冷却。</summary>
    private float criticalStaggerCooldown => host.criticalStaggerCooldown;
    /// <summary>暴击硬直伤害楼层。</summary>
    private float criticalStaggerDamageFloor => host.criticalStaggerDamageFloor;
    /// <summary>是否暴击硬直开放体术。</summary>
    private bool criticalStaggerOpensBodyTechnique => host.criticalStaggerOpensBodyTechnique;
    /// <summary>当前敌人上下文。</summary>
    private EnemyContext ctx => host.ArchitectureReadCtx;
    /// <summary>当前生命值。</summary>
    private float currentHealth => host.currentHealth;
    /// <summary>精英体术窗口。</summary>
    private float eliteBodyTechniqueWindow => host.eliteBodyTechniqueWindow;
    /// <summary>是否启用电锯友方伤害。</summary>
    private bool enableChainsawFriendlyFire => host.enableChainsawFriendlyFire;
    /// <summary>是否启用暴击硬直。</summary>
    private bool enableCriticalStagger => host.enableCriticalStagger;
    /// <summary>是否启用敌人友方伤害。</summary>
    private bool enableEnemyFriendlyFire => host.enableEnemyFriendlyFire;
    /// <summary>是否启用小刀招架。</summary>
    private bool enableKnifeParry => host.enableKnifeParry;
    /// <summary>敌人友方伤害角度。</summary>
    private float enemyFriendlyFireAngle => host.enemyFriendlyFireAngle;
    /// <summary>敌人友方伤害伤害倍率。</summary>
    private float enemyFriendlyFireDamageMultiplier => host.enemyFriendlyFireDamageMultiplier;
    /// <summary>敌人友方伤害高度容差。</summary>
    private float enemyFriendlyFireHeightTolerance => host.enemyFriendlyFireHeightTolerance;
    /// <summary>单次敌人友伤最多命中的目标数。</summary>
    private int enemyFriendlyFireMaxTargets => host.enemyFriendlyFireMaxTargets;
    /// <summary>敌人友方伤害范围。</summary>
    private float enemyFriendlyFireRange => host.enemyFriendlyFireRange;
    /// <summary>是否完全察觉所属玩家。</summary>
    private bool fullyAwareOfPlayer => host.perceptionModule.ReadFullyAwareOfPlayer;
    /// <summary>头部暴击硬直概率。</summary>
    private float headCriticalStaggerChance => host.headCriticalStaggerChance;
    /// <summary>受击反应免疫。</summary>
    private float hitReactionImmunity => host.hitReactionImmunity;
    /// <summary>上一次已见时间。</summary>
    private float lastSeenTime => host.perceptionModule.ReadLastSeenTime;
    /// <summary>腿部倒地伤害。</summary>
    private float legCollapseDamage => host.legCollapseDamage;
    /// <summary>最大生命值。</summary>
    private float maxHealth => host.maxHealth;
    /// <summary>任务敌人类型。</summary>
    private MissionEnemyType missionEnemyType => host.missionEnemyType;
    /// <summary>常规体术窗口。</summary>
    private float normalBodyTechniqueWindow => host.normalBodyTechniqueWindow;
    /// <summary>可感知状态。</summary>
    private VillagerState observableState => host.actionCoordinator.ReadObservableState;
    /// <summary>电锯敌人身体暴击硬直概率倍率。</summary>
    private float sawmanBodyCriticalStaggerChanceMultiplier => host.sawmanBodyCriticalStaggerChanceMultiplier;
    /// <summary>电锯敌人暴击伤害楼层额外加成。</summary>
    private float sawmanCriticalDamageFloorBonus => host.sawmanCriticalDamageFloorBonus;
    /// <summary>电锯敌人头部暴击硬直概率倍率。</summary>
    private float sawmanHeadCriticalStaggerChanceMultiplier => host.sawmanHeadCriticalStaggerChanceMultiplier;
    /// <summary>电锯敌人受击反应阈值倍率。</summary>
    private float sawmanHitReactionThresholdMultiplier => host.sawmanHitReactionThresholdMultiplier;
    /// <summary>电锯敌人韧性伤害倍率。</summary>
    private float sawmanPoiseDamageMultiplier => host.sawmanPoiseDamageMultiplier;
    /// <summary>视野范围。</summary>
    private float sightRange => host.sightRange;
    /// <summary>特殊通行。</summary>
    private EnemyWindowTraversal specialTraversal => host.ArchitectureReadSpecialTraversal;
    /// <summary>硬直阈值。</summary>
    private float staggerThreshold => host.staggerThreshold;
    /// <summary>是否硬直触发器参数可用。</summary>
    private bool staggerTriggerParameterAvailable => host.animationPresenter.ReadStaggerTriggerParameterAvailable;
    /// <summary>起身稳定化持续时间。</summary>
    private float standUpStabilizationDuration => host.standUpStabilizationDuration;
    /// <summary>站立攻击动画数量。</summary>
    private int standingAttackAnimationCount => host.standingAttackAnimationCount;
    /// <summary>站立攻击后备命中归一化时间。</summary>
    private float standingAttackFallbackHitNormalizedTime => host.standingAttackFallbackHitNormalizedTime;
    /// <summary>站立战斗竖直容差。</summary>
    private float standingCombatVerticalTolerance => host.standingCombatVerticalTolerance;
    /// <summary>是否支持爬行。</summary>
    private bool supportsCrawl => host.supportsCrawl;
    /// <summary>是否支持绊倒。</summary>
    private bool supportsTripOver => host.supportsTripOver;
    /// <summary>是否使用电锯敌人命中韧性。</summary>
    private bool useSawmanHitToughness => host.useSawmanHitToughness;
    /// <summary>生命模块。</summary>
    private CharacterHealthModule HealthModule => host.healthModule;

    /// <summary>BecomeFullyAwareOfPlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.BecomeFullyAwareOfPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） EnemyPerceptionModule.BecomeFullyAwareOfPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BecomeFullyAwareOfPlayer()
    {
        PerceptionModule.BecomeFullyAwareOfPlayer(true);
    }

    /// <summary>BeginChase 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginChase(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ConsumeHit()、EnemyCombatModule.EndAttack()；下游（含内部回调体） EnemyActionCoordinator.BeginChase(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="notifyCombatEngaged">是否通知战斗已接战。</param>
    private void BeginChase(bool notifyCombatEngaged = true)
    {
        ActionCoordinator.BeginChase(notifyCombatEngaged);
    }

    /// <summary>BeginDeath 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginDeath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） EnemyActionCoordinator.BeginDeath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginDeath()
    {
        ActionCoordinator.BeginDeath();
    }

    /// <summary>CanSeePlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.CanSeePlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanEngagePlayerAtCloseRange()；下游（含内部回调体） EnemyPerceptionModule.CanSeePlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanSeePlayer()
    {
        return PerceptionModule.CanSeePlayer();
    }

    /// <summary>DiagLog 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeDiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginCrawl()、EnemyCombatModule.BeginHit()、EnemyCombatModule.BeginStandUp()、EnemyCombatModule.CancelStandUpAndResumeCrawl()、EnemyCombatModule.CompleteStandUp() 等共 13 处；下游（含内部回调体） VillagerController.ArchitectureInvokeDiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tag">标签。</param>
    /// <param name="msg">消息内容。</param>
    private void DiagLog(string tag, string msg)
    {
        host.ArchitectureInvokeDiagLog(tag, msg);
    }

    /// <summary>FacePlayerImmediately 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeFacePlayerImmediately()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawlAttack()；下游（含内部回调体） VillagerController.ArchitectureInvokeFacePlayerImmediately()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FacePlayerImmediately()
    {
        host.ArchitectureInvokeFacePlayerImmediately();
    }

    /// <summary>HasAttackRecoveryGroundSupport 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeHasAttackRecoveryGroundSupport()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.EndAttack()；下游（含内部回调体） VillagerController.ArchitectureInvokeHasAttackRecoveryGroundSupport()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasAttackRecoveryGroundSupport()
    {
        return host.ArchitectureInvokeHasAttackRecoveryGroundSupport();
    }

    /// <summary>HasClearLineToPlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.HasClearLineToPlayer(bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanEngagePlayerAtCloseRange()、EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） EnemyPerceptionModule.HasClearLineToPlayer(bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requireSightAngle">是否需要视野角度。</param>
    /// <param name="maximumDistance">最大距离。</param>
    private bool HasClearLineToPlayer(bool requireSightAngle, float maximumDistance)
    {
        return PerceptionModule.HasClearLineToPlayer(requireSightAngle, maximumDistance);
    }

    /// <summary>IsStandingAttackSurfaceStable 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeIsStandingAttackSurfaceStable()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanAttack()、EnemyCombatModule.EndAttack()；下游（含内部回调体） VillagerController.ArchitectureInvokeIsStandingAttackSurfaceStable()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsStandingAttackSurfaceStable()
    {
        return host.ArchitectureInvokeIsStandingAttackSurfaceStable();
    }

    /// <summary>PlayCrawlIdleImmediately 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayCrawlIdleImmediately()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ForceCrawlPoseAfterQTE()；下游（含内部回调体） EnemyAnimationAdapter.PlayCrawlIdleImmediately()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void PlayCrawlIdleImmediately()
    {
        AnimationPresenter.PlayCrawlIdleImmediately();
    }

    /// <summary>PlayVoice 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawlAttack()、EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">语音类型。</param>
    private void PlayVoice(ZombieVoiceType voiceType)
    {
        AnimationPresenter.PlayVoice(voiceType);
    }

    /// <summary>RefreshChaseDestination 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeRefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ConsumeHit()、EnemyCombatModule.EndAttack()；下游（含内部回调体） VillagerController.ArchitectureInvokeRefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RefreshChaseDestination()
    {
        return host.ArchitectureInvokeRefreshChaseDestination();
    }

    /// <summary>RequiresNavigationTraversalToPlayer 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeRequiresNavigationTraversalToPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanCrawlAttack()；下游（含内部回调体） VillagerController.ArchitectureInvokeRequiresNavigationTraversalToPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RequiresNavigationTraversalToPlayer()
    {
        return host.ArchitectureInvokeRequiresNavigationTraversalToPlayer();
    }

    /// <summary>SetMovementMode 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyKnifeParryStagger(bool)、EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawl()、EnemyCombatModule.BeginCrawlAttack()、EnemyCombatModule.BeginHit() 等共 15 处；下游（含内部回调体） VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementMode">移动模式。</param>
    /// <param name="source">来源。</param>
    /// <param name="line">线。</param>
    private void SetMovementMode(EnemyMovementMode movementMode, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        host.ArchitectureInvokeSetMovementMode(movementMode, source, line);
    }

    /// <summary>SetObservableState 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.SetObservableState(VillagerState, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawl()、EnemyCombatModule.BeginCrawlAttack()、EnemyCombatModule.BeginHit()、EnemyCombatModule.BeginStandUp() 等共 8 处；下游（含内部回调体） EnemyActionCoordinator.SetObservableState(VillagerState, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前敌人状态。</param>
    /// <param name="source">来源。</param>
    /// <param name="line">线。</param>
    private void SetObservableState(VillagerState state, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        ActionCoordinator.SetObservableState(state, source, line);
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginCrawl()、EnemyCombatModule.CancelStandUpAndResumeCrawl()、EnemyCombatModule.CompleteStandUp()、EnemyCombatModule.EndAttack()、EnemyCombatModule.ForceCrawlPoseAfterQTE() 等共 7 处；下游（含内部回调体） VillagerController.ArchitectureInvokeSnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SnapToGround()
    {
        host.ArchitectureInvokeSnapToGround();
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ArchitectureInvokeSnapToGround(float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probeHeight">探测高度。</param>
    /// <param name="probeDistance">探测距离。</param>
    /// <param name="allowLargeDrop">是否允许较大掉落。</param>
    private void SnapToGround(float probeHeight, float probeDistance, bool allowLargeDrop)
    {
        host.ArchitectureInvokeSnapToGround(probeHeight, probeDistance, allowLargeDrop);
    }

    /// <summary>StopAndClearMovement 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeStopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyKnifeParryStagger(bool)、EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawlAttack()、EnemyCombatModule.BeginHit()、EnemyCombatModule.BeginStandUp() 等共 6 处；下游（含内部回调体） VillagerController.ArchitectureInvokeStopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopAndClearMovement()
    {
        host.ArchitectureInvokeStopAndClearMovement();
    }

    /// <summary>UpdateHealthAnimationParameter 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.UpdateHealthAnimationParameter()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） EnemyAnimationAdapter.UpdateHealthAnimationParameter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateHealthAnimationParameter()
    {
        AnimationPresenter.UpdateHealthAnimationParameter();
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TakeDamage(float)；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    internal void TakeDamage(float damage)
    {
        TakeDamage(damage, default, HitboxType.Chest);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TakeDamage(float, Vector3)；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    internal void TakeDamage(float damage, Vector3 hitPoint)
    {
        TakeDamage(damage, hitPoint, HitboxType.Chest);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3)、EnemyCombatModule.TakeDamage(float)、EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3)、VillagerController.TakeDamage(float, Vector3, HitboxType)；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitboxType">命中盒类型。</param>
    internal void TakeDamage(float damage, Vector3 hitPoint, HitboxType hitboxType)
    {
        TakeDamage(damage, hitPoint, hitboxType, Vector3.zero);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 CharacterHealthModule.CommitCompatibleDamage(float)、EnemyActionCoordinator.RevokePreviousAnimationCallback()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyCombatModule.BecomeFullyAwareOfPlayer()、EnemyCombatModule.BeginDeath()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.PlayVoice(ZombieVoiceType) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)、VillagerController.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） CharacterHealthModule.CommitCompatibleDamage(float)、EnemyActionCoordinator.RevokePreviousAnimationCallback()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyCombatModule.BecomeFullyAwareOfPlayer()、EnemyCombatModule.BeginDeath()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.PlayVoice(ZombieVoiceType) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitboxType">命中盒类型。</param>
    /// <param name="hitNormal">命中表面法线。</param>
    internal void TakeDamage(float damage, Vector3 hitPoint, HitboxType hitboxType, Vector3 hitNormal)
    {
        ApplyIncomingDamage(damage, hitPoint, hitboxType, hitNormal, false, out _);
    }

    private void ApplyIncomingDamage(float damage, Vector3 hitPoint, HitboxType hitboxType, Vector3 hitNormal,
        bool deferReaction, out bool collapse)
    {
        collapse = false;
        // 诊断记录中的受击前生命值。
float diagHealthBefore = currentHealth;
        // 诊断比例之前。
float diagRatioBefore;
        if (maxHealth > 0f)
        {
            diagRatioBefore = currentHealth / maxHealth;
        }
        else
        {
            diagRatioBefore = 0f;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("伤害入口", $"伤害={damage:F1}, 部位={hitboxType}, 命中前生命={diagHealthBefore:F1}, 比例={diagRatioBefore:F3}");
        }

        if (TryInterceptLethalRevival(damage, hitPoint))
        {
            UpdateHealthAnimationParameter();
            return;
        }
        if (HasGrabWindup && damage > 0f)
        {
            CancelNativeCombat(true);
        }
        bool revivedBeforeHit = HealthModule.LifePhase == GameArchitecture.EnemyLifePhase.Revived;
        HealthModule.CommitCompatibleDamage(damage);
        UpdateHealthAnimationParameter();
        // 诊断比例之后。
float diagRatioAfter;
        if (maxHealth > 0f)
        {
            diagRatioAfter = currentHealth / maxHealth;
        }
        else
        {
            diagRatioAfter = 0f;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("伤害结算", $"命中后生命={currentHealth:F1}, 比例={diagRatioAfter:F3}");
        }

        if (currentHealth <= 0f)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("伤害入口", "致命伤害，进入死亡");
            }

            // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue6 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                receiverValue6.ReleaseCombatReservation(host);
            }

            // 生命已经提交终局死亡，结算不能依赖行为树是否正在更新。
            // BeginDeath 自身持有一次性保护，行为树后续重复请求不会重复掉落或计数。
            var windowTraversal = host.ArchitectureReadSpecialTraversal;
            Vector3 impactSource = hitPoint - hitNormal;
            if (Player != null)
            {
                impactSource = Player.transform.position;
            }
            if (windowTraversal != null && windowTraversal.TryInterruptInOpening(impactSource, out Vector3 fallSource)
                && BeginTerminalDirectionalKnockdown(fallSource, 1f, true))
            {
                return;
            }
            BeginDeath();
            if (revivedBeforeHit && !IsQTEControlled && !NativeDeathActive)
            {
                Vector3 away = Vector3.ProjectOnPlane(hitNormal, Vector3.up);
                Vector3 source = transform.position - away;
                if (away.sqrMagnitude < .001f)
                {
                    source = hitPoint;
                }
                BeginTerminalDirectionalKnockdown(source);
            }

            return;
        }

        var traversal = host.ArchitectureReadSpecialTraversal;
        if (currentHealth < diagHealthBefore && traversal != null && traversal.DeferLadderGroundReaction)
        {
            // 枪械由已经提交的整枪命中决定是否脱手，保留散弹的一次判定。
            if (deferReaction || traversal.TryReactToLadderDamage())
            {
                return;
            }
        }

        if (NativeCombatActive)
        {
            return;
        }

        BecomeFullyAwareOfPlayer();
        // 非致命伤根据受击部位播放对应语音
        // 语音类型。
ZombieVoiceType voiceType = hitboxType switch
        {
            HitboxType.Head => ZombieVoiceType.Headshot,
            HitboxType.LeftArm or HitboxType.RightArm or HitboxType.LeftLeg or HitboxType.RightLeg => ZombieVoiceType.LimbHit,
            _ => ZombieVoiceType.Hurt
        };
        if (currentHealth < diagHealthBefore)
        {
            PlayVoice(voiceType);
        }
        // 韧性倍率。
float poiseMultiplier = hitboxType switch
        {
            HitboxType.Head => 1.4f,
            HitboxType.LeftLeg or HitboxType.RightLeg => 0.7f,
            HitboxType.LeftArm or HitboxType.RightArm => 0.8f,
            _ => 1f
        };
        // 韧性伤害。
float poiseDamage = damage * poiseMultiplier * ResolveHitPoiseResistanceMultiplier();
        accumulatedPoiseDamage += poiseDamage;
        lastPoiseDamageTime = Time.time;
        // 是否腿部命中。
bool isLegHit = hitboxType == HitboxType.LeftLeg || hitboxType == HitboxType.RightLeg;
        // 是否应当进入爬行。
bool shouldEnterCrawl = false;
        // 是否应当绊倒。
bool shouldTripOver = false;
        if (isLegHit && (supportsCrawl || missionEnemyType == MissionEnemyType.Sawman))
        {
            accumulatedLegDamage += damage;
        }
        if (missionEnemyType == MissionEnemyType.Sawman)
        {
            float kneelContribution;
            if (isLegHit)
            {
                kneelContribution = damage * 1.6f;
            }
            else if (hitboxType == HitboxType.Head)
            {
                kneelContribution = damage * 1.15f;
            }
            else
            {
                kneelContribution = damage * 0.65f;
            }
            sawmanKneelDamage += kneelContribution;
        }

        if (isLegHit && supportsCrawl)
        {
            // 生命比例。
float healthRatio;
            if (maxHealth > 0f)
            {
                healthRatio = currentHealth / maxHealth;
            }
            else
            {
                healthRatio = 0f;
            }

            shouldTripOver = !permanentCrawl && healthRatio < 0.4f && supportsTripOver;
            if (shouldTripOver && !deferReaction)
            {
                permanentCrawl = true;
                shouldEnterCrawl = true;
            }
            else if (shouldTripOver)
            {
                // 整次射击汇总后再区分散弹击倒与永久爬行。
                shouldEnterCrawl = true;
            }

            if (shouldTripOver)
            {
                pendingTripOver = true;
            }
            else if (!pendingHit)
            {
                pendingTripOver = false;
            }
        }

        if (deferReaction)
        {
            collapse = shouldEnterCrawl;
            return;
        }

        ScheduleDamageReaction(damage, hitPoint, hitboxType, hitNormal, shouldEnterCrawl);
    }

    private void ScheduleDamageReaction(float damage, Vector3 hitPoint, HitboxType hitboxType, Vector3 hitNormal, bool shouldEnterCrawl)
    {
        bool isLegHit = hitboxType == HitboxType.LeftLeg || hitboxType == HitboxType.RightLeg;
        // 解算后的反应阈值。
float resolvedReactionThreshold = ResolveHitReactionThreshold();
        // 是否应当反应。
bool shouldReact = shouldEnterCrawl || (Time.time >= hitReactionImmuneUntil && accumulatedPoiseDamage >= resolvedReactionThreshold);
        if (CanLogMovementDiagnostic)
        {
            DiagLog("受击判定", $"腿部={isLegHit}, 转爬行={shouldEnterCrawl}, 韧性伤害={accumulatedPoiseDamage:F1}, 阈值={resolvedReactionThreshold:F1}, 触发={shouldReact}");
        }

        if (!shouldReact)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("受击判定", "不触发受击反应，直接返回");
            }

            return;
        }

        // 是否强制重型硬直。
bool forcedHeavyStagger = forcedNextHitDirection == 5;
        // criticalStandingStagger（是否暴击站立硬直）：使用位置：EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)。
        bool armedStandingHit = standingWeaponAttackStartRange > 0f && !pendingCrawl
            && !IsQTEControlled && forcedNextHitDirection < 0;
        bool disarm = armedStandingHit && hitboxType == HitboxType.RightArm;
        bool criticalStandingStagger = !disarm && !forcedHeavyStagger && !pendingCrawl && !isLegHit
            && (!UsesNativeBodyTechnique || hitboxType != HitboxType.Head)
            && ShouldTriggerCriticalStagger(hitboxType, damage);
        bool sawmanKneelStagger = missionEnemyType == MissionEnemyType.Sawman
            && !disarm && !forcedHeavyStagger && !pendingCrawl && !IsQTEControlled
            && (sawmanKneelDamage >= 45f
                || (isLegHit && (damage >= 22f || accumulatedLegDamage >= 32f)));
        bool sawmanHeavyStagger = missionEnemyType == MissionEnemyType.Sawman
            && !sawmanKneelStagger
            && !disarm && !forcedHeavyStagger && !pendingCrawl && !isLegHit
            && (criticalStandingStagger || damage >= 45f || hitboxType == HitboxType.Head);
        bool repeatChestHit = hitboxType == HitboxType.Chest && HasUnfinishedStandingHit
            && !shouldEnterCrawl && forcedNextHitDirection < 0 && !IsQTEControlled;
        if (repeatChestHit && (!criticalStandingStagger || pendingStandingStagger))
        {
            // 行为树仍在等待当前动作的结束通知。普通追击命中只结算韧性和免疫间隔。
            accumulatedPoiseDamage = 0f;
            hitReactionImmuneUntil = Time.time + hitReactionImmunity;
            return;
        }

        ClearNativeBodyReaction();
        // 是否使用站立硬直。
bool useStandingStagger = !sawmanKneelStagger && (forcedHeavyStagger || criticalStandingStagger || sawmanHeavyStagger);
        pendingStandingStagger = useStandingStagger;
        if (useStandingStagger)
        {
            pendingHitDirection = 0;
            if (forcedHeavyStagger || (sawmanHeavyStagger && (hitboxType == HitboxType.Head || damage >= 45f)))
            {
                pendingStaggerDirection = 0;
            }
            else
            {
                pendingStaggerDirection = ResolveStaggerDirection(hitNormal, hitPoint);
            }

            pendingStaggerLevel = ResolveStaggerLevel(hitboxType, forcedHeavyStagger || sawmanHeavyStagger);
            pendingStandingStaggerBodyTechnique = !UsesNativeBodyTechnique && (sawmanHeavyStagger || !criticalStandingStagger || criticalStaggerOpensBodyTechnique);
        }
        else
        {
            if (sawmanKneelStagger)
            {
                accumulatedLegDamage = 0f;
                sawmanKneelDamage = 0f;
                if (hitboxType == HitboxType.RightLeg)
                {
                    pendingHitDirection = NativeRightKneelHitDirection;
                }
                else
                {
                    pendingHitDirection = NativeLeftKneelHitDirection;
                }
            }
            else if (forcedNextHitDirection >= 0)
            {
                pendingHitDirection = forcedNextHitDirection;
            }
            else
            {
                pendingHitDirection = EnemyHitbox.ToHitDir(hitboxType, hitPoint, transform);
                if (disarm)
                {
                    pendingHitDirection = ArmedDisarmHitDirection;
                }
                else if (armedStandingHit && shouldEnterCrawl && hitboxType == HitboxType.LeftLeg)
                {
                    pendingHitDirection = ArmedFallingLeftLegHitDirection;
                }
                else if (armedStandingHit && shouldEnterCrawl && hitboxType == HitboxType.RightLeg)
                {
                    pendingHitDirection = ArmedFallingRightLegHitDirection;
                }
                else if (armedStandingHit && hitboxType == HitboxType.LeftLeg)
                {
                    pendingHitDirection = ArmedStandingLeftLegHitDirection;
                }
                else if (armedStandingHit && hitboxType == HitboxType.RightLeg)
                {
                    pendingHitDirection = ArmedStandingRightLegHitDirection;
                }
                else if (UsesNativeBodyTechnique && !pendingCrawl && !shouldEnterCrawl)
                {
                    if (hitboxType == HitboxType.LeftLeg)
                    {
                        pendingHitDirection = NativeLeftLegFlinchDirection;
                    }
                    else if (hitboxType == HitboxType.RightLeg)
                    {
                        pendingHitDirection = NativeRightLegFlinchDirection;
                    }
                    else if (hitboxType == HitboxType.Pelvis || hitboxType == HitboxType.Head)
                    {
                        pendingHitDirection = EnemyHitbox.ToHitDir(HitboxType.Chest, hitPoint, transform);
                    }
                }
            }

            pendingStaggerDirection = 0;
            pendingStaggerLevel = 0;
            pendingStandingStaggerBodyTechnique = false;
        }

        MarkDamageReactionPending(shouldEnterCrawl);
        if (repeatChestHit && criticalStandingStagger)
        {
            // 已在受击任务中时，行为树不会再次调用 OnStart，需要立即进入新的硬直状态。
            ActionCoordinator.RequestAction(GameArchitecture.ActionKind.HitReaction, "躯干追击触发暴击硬直");
        }
    }

    private void MarkDamageReactionPending(bool shouldEnterCrawl)
    {
        pendingCrawl = pendingCrawl || shouldEnterCrawl;
        pendingHit = true;
        hitFinished = false;
        accumulatedPoiseDamage = 0f;
        ActionCoordinator.RevokePreviousAnimationCallback();
        hitReactionImmuneUntil = Time.time + hitReactionImmunity;
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue10 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.ReleaseCombatReservation(host);
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("受击标记", $"pendingHit={pendingHit}, pendingCrawl={pendingCrawl}, pendingStandUp={pendingStandUp}, " + $"pendingTripOver={pendingTripOver}, permanentCrawl={permanentCrawl}, " + $"允许体术={pendingStandingStaggerBodyTechnique}, HitDir={pendingHitDirection}");
        }
    }

    /// <summary>TakeBodyTechniqueDamage 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.AllowLowerOnceVoiceBypassCooldown()、EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TakeBodyTechniqueDamage(float, Vector3)；下游（含内部回调体） EnemyAnimationAdapter.AllowLowerOnceVoiceBypassCooldown()、EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    private int cachedKneelStateForBodyTechnique;
    private float cachedKneelStateTime = float.NegativeInfinity;
    private bool pendingSawKneelExitCrossfade;

    internal int ReadCurrentKneelState()
    {
        if (currentNativeBodyReaction == NativeBodyReaction.LeftKneel)
        {
            return 1;
        }

        if (currentNativeBodyReaction == NativeBodyReaction.RightKneel)
        {
            return 2;
        }

        return AnimationPresenter.ReadActiveKneelStateFromAnimator();
    }

    internal int CaptureBodyTechniqueKneelState()
    {
        int captured = ReadCurrentKneelState();
        if (captured > 0)
        {
            cachedKneelStateForBodyTechnique = captured;
            cachedKneelStateTime = Time.time;
        }

        return captured;
    }

    internal int ConsumeCachedBodyTechniqueKneelState()
    {
        int live = ReadCurrentKneelState();
        if (live > 0)
        {
            cachedKneelStateForBodyTechnique = 0;
            cachedKneelStateTime = float.NegativeInfinity;
            return live;
        }

        if (cachedKneelStateForBodyTechnique > 0 && Time.time - cachedKneelStateTime <= 1.2f)
        {
            int captured = cachedKneelStateForBodyTechnique;
            cachedKneelStateForBodyTechnique = 0;
            cachedKneelStateTime = float.NegativeInfinity;
            return captured;
        }

        cachedKneelStateForBodyTechnique = 0;
        cachedKneelStateTime = float.NegativeInfinity;
        return 0;
    }

    internal void TakeBodyTechniqueDamage(float damage, Vector3 hitPoint)
    {
        int capturedKneel = ReadCurrentKneelState();
        cachedKneelStateForBodyTechnique = capturedKneel;
        if (capturedKneel > 0)
        {
            cachedKneelStateTime = Time.time;
            if (missionEnemyType == MissionEnemyType.Sawman)
            {
                pendingSawKneelExitCrossfade = true;
            }
        }
        else
        {
            cachedKneelStateTime = float.NegativeInfinity;
        }

        // 体术命中的受击语音不允许被全局语音间隔吞掉：
        // 踢击总发生在敌人攻击/吼叫刚结束之后，1.5 秒冷却几乎必然拦截
        AnimationPresenter.AllowLowerOnceVoiceBypassCooldown();
        TakeHeavyFrontDamage(damage, hitPoint);
    }

    /// <summary>TakeHeavyFrontDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3)、VillagerController.TakeHeavyFrontDamage(float, Vector3)；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    /// <param name="hitPoint">命中点位置。</param>
    internal void TakeHeavyFrontDamage(float damage, Vector3 hitPoint)
    {
        if (cachedKneelStateForBodyTechnique == 0 || Time.time - cachedKneelStateTime > 0.05f)
        {
            int capturedKneel = ReadCurrentKneelState();
            if (capturedKneel > 0)
            {
                cachedKneelStateForBodyTechnique = capturedKneel;
                cachedKneelStateTime = Time.time;
                if (missionEnemyType == MissionEnemyType.Sawman)
                {
                    pendingSawKneelExitCrossfade = true;
                }
            }
        }

        forcedNextHitDirection = 5;
        try
        {
            TakeDamage(damage, hitPoint, HitboxType.Chest);
        }
        finally
        {
            forcedNextHitDirection = -1;
        }
    }

    /// <summary>ResolveStaggerLevel 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerController.ResolveStaggerLevel(HitboxType, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitboxType">命中盒类型。</param>
    /// <param name="forcedHeavyStagger">是否强制重型硬直。</param>
    internal int ResolveStaggerLevel(HitboxType hitboxType, bool forcedHeavyStagger)
    {
        if (forcedHeavyStagger)
        {
            return 2;
        }

        if (hitboxType == HitboxType.Head)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>ShouldTriggerCriticalStagger 的业务处理入口；本方法直接依赖 EnemyCombatModule.UsesSawmanHitToughness()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerController.ShouldTriggerCriticalStagger(HitboxType, float)；下游（含内部回调体） EnemyCombatModule.UsesSawmanHitToughness()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitboxType">命中盒类型。</param>
    /// <param name="damage">伤害。</param>
    internal bool ShouldTriggerCriticalStagger(HitboxType hitboxType, float damage)
    {
        // 解算后的伤害楼层。
float resolvedDamageFloor = criticalStaggerDamageFloor;
        if (UsesSawmanHitToughness())
        {
            resolvedDamageFloor += sawmanCriticalDamageFloorBonus;
        }

        if (!enableCriticalStagger || Time.time < nextCriticalStaggerAllowedTime || damage < resolvedDamageFloor)
        {
            return false;
        }

        // 概率。
float chance;
        if (hitboxType == HitboxType.Head)
        {
            chance = headCriticalStaggerChance;
        }
        else
        {
            if (hitboxType == HitboxType.Chest || hitboxType == HitboxType.Pelvis || hitboxType == HitboxType.LeftArm || hitboxType == HitboxType.RightArm)
            {
                chance = bodyCriticalStaggerChance;
            }
            else
            {
                chance = 0f;
            }
        }

        if (UsesSawmanHitToughness())
        {
            // 复合赋值前保存的概率。
float previousValue14 = chance;
            // 分支选出的电锯敌人头部暴击硬直概率倍率。
float selectedValue15;
            if (hitboxType == HitboxType.Head)
            {
                selectedValue15 = sawmanHeadCriticalStaggerChanceMultiplier;
            }
            else
            {
                selectedValue15 = sawmanBodyCriticalStaggerChanceMultiplier;
            }

            chance = (float)(previousValue14 * selectedValue15);
        }

        if (chance <= 0f || UnityEngine.Random.value > chance)
        {
            return false;
        }

        nextCriticalStaggerAllowedTime = Time.time + criticalStaggerCooldown;
        return true;
    }

    /// <summary>UsesSawmanHitToughness 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginHit()、EnemyCombatModule.ResolveHitPoiseResistanceMultiplier()、EnemyCombatModule.ResolveHitReactionThreshold()、EnemyCombatModule.ShouldTriggerCriticalStagger(HitboxType, float)、VillagerController.UsesSawmanHitToughness()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool UsesSawmanHitToughness()
    {
        return useSawmanHitToughness && missionEnemyType == MissionEnemyType.Sawman;
    }

    /// <summary>ResolveHitPoiseResistanceMultiplier 的业务处理入口；本方法直接依赖 EnemyCombatModule.UsesSawmanHitToughness()、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerController.ResolveHitPoiseResistanceMultiplier()；下游（含内部回调体） EnemyCombatModule.UsesSawmanHitToughness()、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal float ResolveHitPoiseResistanceMultiplier()
    {
        if (UsesSawmanHitToughness())
        {
            return Mathf.Clamp01(sawmanPoiseDamageMultiplier);
        }
        else
        {
            return 1f;
        }
    }

    /// <summary>ResolveHitReactionThreshold 的业务处理入口；本方法直接依赖 EnemyCombatModule.UsesSawmanHitToughness()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerController.ResolveHitReactionThreshold()；下游（含内部回调体） EnemyCombatModule.UsesSawmanHitToughness()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal float ResolveHitReactionThreshold()
    {
        // 倍率。
float multiplier;
        if (UsesSawmanHitToughness())
        {
            multiplier = Mathf.Max(1f, sawmanHitReactionThresholdMultiplier);
        }
        else
        {
            multiplier = 1f;
        }

        return staggerThreshold * multiplier;
    }

    /// <summary>ResolveStaggerDirection 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Transform.InverseTransformDirection(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerController.ResolveStaggerDirection(Vector3, Vector3)；下游（含内部回调体） Mathf.Abs(float)、Transform.InverseTransformDirection(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitNormal">命中表面法线。</param>
    /// <param name="hitPoint">命中点位置。</param>
    internal int ResolveStaggerDirection(Vector3 hitNormal, Vector3 hitPoint)
    {
        // 世界空间方向。
Vector3 worldDirection = hitNormal;
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f && hitPoint != default)
        {
            worldDirection = hitPoint - transform.position;
            worldDirection.y = 0f;
        }

        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            worldDirection = transform.forward;
        }

        // 局部方向。
Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
        if (Mathf.Abs(localDirection.z) >= Mathf.Abs(localDirection.x))
        {
            if (localDirection.z >= 0f)
            {
                return 0;
            }
            else
            {
                return 1;
            }
        }

        if (localDirection.x < 0f)
        {
            return 2;
        }
        else
        {
            return 3;
        }
    }

    /// <summary>CanAttack 的业务处理入口；本方法直接依赖 EnemyAIManager.CanAcquireAttackReservation(VillagerController)、EnemyCombatModule.CanEngagePlayerAtCloseRange()、EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsStandingAttackSurfaceStable()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、VillagerController.CanAttack()；下游（含内部回调体） EnemyAIManager.CanAcquireAttackReservation(VillagerController)、EnemyCombatModule.CanEngagePlayerAtCloseRange()、EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsStandingAttackSurfaceStable()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool CanAttack()
    {
        EnemyAIManager manager = EnemyAIManager.Instance;
        return CanStartStandingAttack() && manager != null && manager.CanAcquireAttackReservation(host);
    }

    internal bool CanStartStandingAttack()
    {
        if (chainsawBridge != null && (!chainsawBridge.HasStartedChainsaw || chainsawBridge.InPlaceIntimidationMiddle))
        {
            return false;
        }

        if (!host.ShouldChasePlayer || !CanEvaluateStandingCombatAction || host.IsNavigationTraversalProtected ||
            !IsAttackReady || !IsStandingAttackSurfaceStable())
        {
            return false;
        }

        if (host.HasSiegeBreachOrder)
        {
            return ResolveSiegeDoor() != null;
        }
        return ResolveSiegeDoor() != null || IsPlayerWithinAttackStartRange() &&
            CanEngagePlayerAtCloseRange() && CanReachWithStandingPose();
    }

    internal bool CanCrawlAttack()
    {
        EnemyAIManager manager = EnemyAIManager.Instance;
        return CanStartCrawlAttack() && manager != null && manager.CanAcquireAttackReservation(host);
    }

    internal bool CanStartCrawlAttack()
    {
        if (!CanEvaluateCrawlCombatAction || !IsAttackReady || !IsPlayerWithinAttackStartRange())
        {
            return false;
        }

        bool isBodyContact = IsPlayerWithinBodyContactRange();
        return (isBodyContact || !RequiresNavigationTraversalToPlayer()) &&
            IsFacingPlayerForCrawlAttack() && CanEngagePlayerAtCloseRange();
    }

    /// <summary>BeginHit 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.TriggerPresentation(int)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyAnimationAdapter.TriggerPresentation(int)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool skipNextStaggerTriggerPresentation;

    private bool IsAnimatorInStaggerState()
    {
        return AnimationPresenter.IsInStaggerState();
    }

    internal void BeginHit()
    {
        if (!pendingHit || IsDead)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("BeginHit", $"跳过: pendingHit={pendingHit}, IsDead={IsDead}");
            }

            return;
        }

        bool consumeSkipStaggerTrigger = skipNextStaggerTriggerPresentation;
        skipNextStaggerTriggerPresentation = false;
        bool consumeSawKneelExitCrossfade = pendingSawKneelExitCrossfade;
        pendingSawKneelExitCrossfade = false;

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginHit", $"进入: pendingCrawl={pendingCrawl}, pendingTripOver={pendingTripOver}, HitDir={pendingHitDirection}");
        }

        staggerRecoveryStartedTime = float.NegativeInfinity;
        StopAndClearMovement();
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue20 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.ReleaseCombatReservation(host);
        }

        SetObservableState(VillagerState.Staggered);
        currentNativeBodyReaction = pendingNativeBodyReaction;
        pendingNativeBodyReaction = NativeBodyReaction.None;
        nativeBodyReactionAction = ActionCoordinator.CurrentHandle;
        currentStandingStaggerBodyTechnique = pendingStandingStagger && pendingStandingStaggerBodyTechnique;
        if (currentStandingStaggerBodyTechnique)
        {
            // 窗口。
float window;
            if (UsesSawmanHitToughness())
            {
                window = eliteBodyTechniqueWindow;
            }
            else
            {
                window = normalBodyTechniqueWindow;
            }

            bodyTechniqueWindowEndTime = Time.time + Mathf.Max(0.2f, window);
        }
        else
        {
            bodyTechniqueWindowEndTime = 0f;
        }

        deferredStandingHitCompletion = false;
        // 分支选出的敌人移动模式的完全已停止。
global::EnemyMovementMode selectedValue23;
        if (pendingStandingStagger)
        {
            // 重冲击与体术兜底硬直需要保留动作根位移与外部击退，避免原地踏步。
            if (pendingHitDirection == 5 || pendingStaggerLevel >= 2)
            {
                selectedValue23 = EnemyMovementMode.ActionRootMotion;
            }
            else
            {
                selectedValue23 = EnemyMovementMode.FullyStopped;
            }
        }
        else
        {
            if (pendingCrawl)
            {
                selectedValue23 = EnemyMovementMode.FullRootMotion;
            }
            else
            {
                selectedValue23 = EnemyMovementMode.ActionRootMotion;
            }
        }

        SetMovementMode(selectedValue23);
        hitFinished = false;
        if (anim != null)
        {
            AnimationPresenter.SetIntegerValue(HashHitDir, pendingHitDirection);
            if (staggerTriggerParameterAvailable)
            {
                AnimationPresenter.SetIntegerValue(HashStaggerDirection, pendingStaggerDirection);
                AnimationPresenter.SetIntegerValue(HashStaggerLevel, pendingStaggerLevel);
            }

            AnimationPresenter.SetBoolean(HashIsCrawl, pendingCrawl);
            AnimationPresenter.SetBoolean(HashCanTripOver, pendingTripOver);
            AnimationPresenter.SetBoolean(HashATK, false);
            AnimationPresenter.SetBoolean(HashHIT, true);
            bool nativeReactionPresented = AnimationPresenter.TryPresentNativeBodyReaction(currentNativeBodyReaction);
            bool alreadyInStagger = consumeSkipStaggerTrigger && IsAnimatorInStaggerState();
            if (!nativeReactionPresented && consumeSawKneelExitCrossfade && pendingStandingStagger && anim != null
                && missionEnemyType == MissionEnemyType.Sawman)
            {
                AnimationPresenter.CrossFadeStaggerStateImmediate("Base Layer.Stagger.Stagger_Heavy_Front", 0.22f, 0f, false);
            }
            else if (!nativeReactionPresented && pendingStandingStagger && staggerTriggerParameterAvailable)
            {
                if (!alreadyInStagger)
                {
                    AnimationPresenter.TriggerPresentation(HashStaggerTrigger);
                }
            }
            else if (!nativeReactionPresented && pendingStandingStagger)
            {
                AnimationPresenter.SetIntegerValue(HashHitDir, 5);
                if (!alreadyInStagger)
                {
                    AnimationPresenter.TriggerPresentation(HashHitTrigger);
                }
            }
            else if (!nativeReactionPresented)
            {
                AnimationPresenter.TriggerPresentation(HashHitTrigger);
            }
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginHit", $"退出: 状态={observableState}, 移动模式={CurrentMovementMode}, " + $"IsCrawl={pendingCrawl}, CanTripOver={pendingTripOver}");
        }
    }

    /// <summary>ConsumeHit 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.BeginChase(bool)、EnemyCombatModule.CanEngagePlayerAtCloseRange()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.IsPlayerWithinCombatHoldRange()、EnemyCombatModule.RefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConsumeHit()；下游（含内部回调体） EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.BeginChase(bool)、EnemyCombatModule.CanEngagePlayerAtCloseRange()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.IsPlayerWithinCombatHoldRange()、EnemyCombatModule.RefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ConsumeHit()
    {
        // 旧行为树任务的退出回调不能夺走原版接触或倒地动作。
        if (NativeCombatActive)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("ConsumeHit", $"进入: pendingCrawl={pendingCrawl}, fullyAware={fullyAwareOfPlayer}, " + $"Player={Player != null}, inCombatHoldRange={Player != null && IsPlayerWithinCombatHoldRange()}");
        }

        // 是否应当混合站立硬直恢复。
bool shouldBlendStandingStaggerRecovery = pendingStandingStagger;
        ClearNativeBodyReaction();
        pendingHit = false;
        hitFinished = false;
        pendingTripOver = false;
        pendingStandingStagger = false;
        pendingStandingStaggerBodyTechnique = false;
        currentStandingStaggerBodyTechnique = false;
        bodyTechniqueWindowEndTime = 0f;
        deferredStandingHitCompletion = false;
        pendingStaggerDirection = 0;
        pendingStaggerLevel = 0;
        if (anim != null)
        {
            AnimationPresenter.SetBoolean(HashHIT, false);
            AnimationPresenter.SetBoolean(HashCanTripOver, false);
            AnimationPresenter.ClearTrigger(HashHitTrigger);
            if (staggerTriggerParameterAvailable)
            {
                AnimationPresenter.ClearTrigger(HashStaggerTrigger);
            }
        }

        if (WillPursueAfterHit())
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("ConsumeHit", "受击结束→进入追击(BeginChase)");
            }

            BeginChase();
            if (shouldBlendStandingStaggerRecovery)
            {
                staggerRecoveryStartedTime = Time.time;
                ctx.TargetMoveSpeed = 0f;
            }

            RefreshChaseDestination();
        }
        else
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("ConsumeHit", $"受击结束→未进入追击: pendingCrawl={pendingCrawl}, " + $"fullyAware={fullyAwareOfPlayer}, " + $"inCombatHoldRange={Player != null && IsPlayerWithinCombatHoldRange()}");
            }
        }
    }

    internal void BeginPairedSurvivorStagger(float blendSeconds = 0.14f)
    {
        if (IsDead)
        {
            return;
        }

        staggerRecoveryStartedTime = float.NegativeInfinity;
        StopAndClearMovement();
        global::EnemyAIManager aiManager = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(aiManager, null))
        {
            aiManager.ReleaseCombatReservation(host);
        }

        ClearNativeBodyReaction();
        pendingCrawl = false;
        pendingTripOver = false;
        pendingStandingStagger = true;
        pendingStandingStaggerBodyTechnique = false;
        currentStandingStaggerBodyTechnique = false;
        bodyTechniqueWindowEndTime = 0f;
        deferredStandingHitCompletion = false;
        pendingHitDirection = 0;
        pendingStaggerDirection = 0;
        pendingStaggerLevel = 1;
        pendingHit = true;
        hitFinished = false;
        skipNextStaggerTriggerPresentation = true;
        SetObservableState(VillagerState.Staggered);
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        ApplyPairedSurvivorStaggerAnimator(true, Mathf.Max(0.05f, blendSeconds), 0.04f, true);
    }

    internal void MaintainPairedSurvivorStaggerState(bool restoreAnimatorIfIdle)
    {
        if (IsDead || !HasUnfinishedStandingHit)
        {
            return;
        }

        SetObservableState(VillagerState.Staggered);
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        skipNextStaggerTriggerPresentation = true;
        bool needCrossfade = restoreAnimatorIfIdle && !IsAnimatorInStaggerState();
        ApplyPairedSurvivorStaggerAnimator(needCrossfade, 0.08f, 0.14f, false);
    }

    private void ApplyPairedSurvivorStaggerAnimator(bool crossfadeStagger, float blendSeconds, float normalizedOffset, bool updateNow)
    {
        if (anim == null)
        {
            return;
        }

        AnimationPresenter.SetIntegerValue(HashHitDir, 0);
        if (staggerTriggerParameterAvailable)
        {
            AnimationPresenter.SetIntegerValue(HashStaggerDirection, 0);
            AnimationPresenter.SetIntegerValue(HashStaggerLevel, 1);
        }

        AnimationPresenter.SetBoolean(HashIsCrawl, false);
        AnimationPresenter.SetBoolean(HashCanTripOver, false);
        AnimationPresenter.SetBoolean(HashATK, false);
        AnimationPresenter.SetBoolean(HashHIT, true);
        if (crossfadeStagger)
        {
            AnimationPresenter.CrossFadeStaggerStateImmediate("Base Layer.Stagger.Stagger_Body_Front", blendSeconds, normalizedOffset, updateNow);
        }
    }

    /// <summary>BeginCrawl 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginCrawl()
    {
        if (IsDead)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("BeginCrawl", "跳过: IsDead=true");
            }

            // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue24 = colliderCtrl;
            if (!global::System.Object.ReferenceEquals(receiverValue24, null))
            {
                receiverValue24.SetDisabled();
            }

            return;
        }

        if (!supportsCrawl)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("BeginCrawl", "跳过: supportsCrawl=false");
            }

            pendingCrawl = false;
            pendingStandUp = false;
            if (ctx != null)
            {
                crawlPose = false;
                ctx.HasCrawlMoveIntent = false;
            }

            AnimationPresenter.SetBoolean(HashIsCrawl, false);
            // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue25 = colliderCtrl;
            if (!global::System.Object.ReferenceEquals(receiverValue25, null))
            {
                receiverValue25.SetStanding();
            }

            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginCrawl", $"进入: permanentCrawl={permanentCrawl}");
        }

        pendingCrawl = true;
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue26 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            receiverValue26.ReleaseCombatReservation(host);
        }

        pendingStandUp = false;
        SetObservableState(VillagerState.Crawl);
        SetMovementMode(EnemyMovementMode.CrawlCodeDriven);
        host.ResetCrawlOrientationCache();
        crawlPose = true;
        ctx.HasCrawlMoveIntent = true;
        // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue27 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.SetCrawl();
        }

        SnapToGround();
        AnimationPresenter.SetBoolean(HashIsCrawl, true);
        if (agent != null)
        {
            agent.isStopped = false;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginCrawl", $"退出: 状态={observableState}, 移动模式={CurrentMovementMode}, " + $"IsCrawl={ctx.IsCrawl}, permanentCrawl={permanentCrawl}");
        }
    }

    /// <summary>RequestStandUp 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.StopAndClearMovement()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RequestStandUp()；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.StopAndClearMovement()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RequestStandUp()
    {
        if (permanentCrawl)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("RequestStandUp", "跳过: permanentCrawl=true, 永久爬行不允许起身");
            }

            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("RequestStandUp", "进入: 请求起身");
        }

        pendingCrawl = false;
        pendingStandUp = true;
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue28 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.ReleaseCombatReservation(host);
        }

        crawlPose = false;
        ctx.HasCrawlMoveIntent = false;
        AnimationPresenter.SetBoolean(HashIsCrawl, false);
        StopAndClearMovement();
        // 起身动画包含约零点七六米的纵向根位移。使用动作根运动只保留
        // 水平位移并持续贴地，避免模型先被抬高再被地面校正瞬间拉回。
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
    }

    /// <summary>BeginStandUp 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.StopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginStandUp()
    {
        if (IsDead)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("BeginStandUp", "跳过: IsDead");
            }

            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginStandUp", "进入");
        }

        SetObservableState(VillagerState.StandUp);
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        standUpFinished = false;
        StopAndClearMovement();
        AnimationPresenter.SetBoolean(HashIsCrawl, false);
    }

    /// <summary>CompleteStandUp 的业务处理入口；本方法直接依赖 EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SnapToGround()、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CompleteStandUp()；下游（含内部回调体） EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SnapToGround()、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CompleteStandUp()
    {
        if (permanentCrawl)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("CompleteStandUp", "跳过: permanentCrawl=true, 永久爬行不允许完成起身");
            }

            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("CompleteStandUp", "进入: 起身完成");
        }

        pendingStandUp = false;
        accumulatedLegDamage = Mathf.Min(accumulatedLegDamage, legCollapseDamage * 0.25f);
        // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue29 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue29, null))
        {
            receiverValue29.SetStanding();
        }

        SnapToGround();
        standUpStabilizationEndsAt = Time.time + Mathf.Max(0f, standUpStabilizationDuration);
        ActionCoordinator.ResumeAfterStandUp();
    }

    /// <summary>CancelStandUpAndResumeCrawl 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.SnapToGround()、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CancelStandUpAndResumeCrawl()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.SnapToGround()、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CancelStandUpAndResumeCrawl()
    {
        if (IsDead)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("CancelStandUp", "起身超时→恢复爬行");
        }

        pendingStandUp = false;
        pendingCrawl = true;
        standUpFinished = false;
        crawlPose = true;
        ctx.HasCrawlMoveIntent = true;
        SetObservableState(VillagerState.Crawl);
        SetMovementMode(EnemyMovementMode.CrawlCodeDriven);
        host.ResetCrawlOrientationCache();
        // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue30 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue30, null))
        {
            receiverValue30.SetCrawl();
        }

        AnimationPresenter.SetBoolean(HashATK, false);
        AnimationPresenter.SetBoolean(HashIsCrawl, true);
        if (agent != null)
        {
            agent.isStopped = false;
        }

        SnapToGround();
    }

    /// <summary>ForceCrawlPoseAfterQTE 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyCombatModule.PlayCrawlIdleImmediately()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.SnapToGround()、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ForceCrawlPoseAfterQTE()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyColliderController.SetCrawl()、EnemyCombatModule.PlayCrawlIdleImmediately()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)、EnemyCombatModule.SnapToGround()、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ForceCrawlPoseAfterQTE()
    {
        if (IsDead || !supportsCrawl)
        {
            return;
        }

        pendingCrawl = true;
        pendingStandUp = false;
        pendingTripOver = false;
        if (ctx != null)
        {
            crawlPose = true;
            ctx.HasCrawlMoveIntent = true;
        }

        SetObservableState(VillagerState.Crawl);
        SetMovementMode(EnemyMovementMode.CrawlCodeDriven);
        host.ResetCrawlOrientationCache();
        // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue31 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue31, null))
        {
            receiverValue31.SetCrawl();
        }

        if (anim != null)
        {
            AnimationPresenter.SetBoolean(HashATK, false);
            AnimationPresenter.SetBoolean(HashHIT, false);
            AnimationPresenter.SetBoolean(HashIsCrawl, true);
            AnimationPresenter.SetBoolean(HashCanTripOver, false);
            PlayCrawlIdleImmediately();
        }

        SnapToGround();
    }

    /// <summary>BeginAttack 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.SelectAttackIndex(float, int)、EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyCombatModule.CaptureAttackRecoveryAnchor()、EnemyCombatModule.FacePlayerImmediately()、EnemyCombatModule.PlayVoice(ZombieVoiceType) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)；下游（含内部回调体） ChainsawManAnimationBridge.SelectAttackIndex(float, int)、EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyCombatModule.CaptureAttackRecoveryAnchor()、EnemyCombatModule.FacePlayerImmediately()、EnemyCombatModule.PlayVoice(ZombieVoiceType) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private int rearAssistSession;
    internal bool BeginAttack()
    {
        siegeAttackDoor = ResolveSiegeDoor();
        siegeAttackLanded = false;
        rearAssistSession = 0;
        if (Player != null && Player.TryGetComponent<LeonQTECoordinator>(out var rearPair) && rearPair.IsRearHelper(host))
        {
            rearAssistSession = rearPair.CurrentSessionId;
        }
        if (rearAssistSession == 0 && HealthModule.LifePhase == GameArchitecture.EnemyLifePhase.Revived)
        {
            return TryBeginGrabAttack(Player, QTEKind.FrontGrab);
        }
        int attackIndex;
        if (chainsawBridge != null)
        {
            float attackDistance = PlayerDistance;
            if (siegeAttackDoor != null)
            {
                attackDistance = Vector3.Distance(transform.position, siegeAttackDoor.PassageCenter);
            }
            attackIndex = chainsawBridge.SelectAttackIndex(attackDistance, standingAttackAnimationCount);
        }
        else
        {
            attackIndex = SelectStandingAttack();
            if (attackIndex < 0)
            {
                return false;
            }
        }

        KnifeIncomingAttackId++;
        CaptureAttackRecoveryAnchor();
        ActionCoordinator.ExitCombatOccupancy();
        StopAndClearMovement();
        SetObservableState(VillagerState.Attack);
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        attackFinished = false;
        attackHitMask = 0;
        normalSwingHitHasRecoverWindow = false;
        standingParryWindowOpen = false;
        lastAttackTime = Time.time;
        if (siegeAttackDoor != null)
        {
            host.ArchitectureInvokeFacePosition(siegeAttackDoor.PassageCenter, 50f);
        }
        else
        {
            FacePlayerImmediately();
        }

        AnimationPresenter.SetIntegerValue(HashAttackIndex, attackIndex);
        currentKnifeAttackMove = attackIndex;
        ResetKnifeContactTimeline();
        AnimationPresenter.SetBoolean(HashATK, true);
        if (siegeAttackDoor == null && PerceptionModule != null)
        {
            PerceptionModule.MarkPersonallyDetectedPlayer();
        }

        PlayVoice(ZombieVoiceType.Attack);
        return true;
    }

    /// <summary>BeginCrawlAttack 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyColliderController.SetCrawl()、EnemyCombatModule.FacePlayerImmediately()、EnemyCombatModule.PlayVoice(ZombieVoiceType)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)；下游（含内部回调体） EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyColliderController.SetCrawl()、EnemyCombatModule.FacePlayerImmediately()、EnemyCombatModule.PlayVoice(ZombieVoiceType)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool BeginCrawlAttack()
    {
        if (!supportsCrawl)
        {
            return false;
        }

        KnifeIncomingAttackId++;
        ActionCoordinator.ExitCombatOccupancy();
        StopAndClearMovement();
        pendingCrawl = true;
        crawlPose = true;
        // 判空前缓存的碰撞体控制器。
global::EnemyColliderController receiverValue33 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.SetCrawl();
        }

        SetObservableState(VillagerState.CrawlAttack);
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        attackFinished = false;
        attackHitMask = 0;
        lastAttackTime = Time.time;
        FacePlayerImmediately();
        AnimationPresenter.SetFloatValue(HashMoveSpeed, 0f);
        AnimationPresenter.SetBoolean(HashIsCrawl, true);
        AnimationPresenter.SetBoolean(HashATK, true);
        if (siegeAttackDoor == null && PerceptionModule != null)
        {
            PerceptionModule.MarkPersonallyDetectedPlayer();
        }

        PlayVoice(ZombieVoiceType.Attack);
        return true;
    }

    /// <summary>EndAttack 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitActionPose(Vector3, Quaternion)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.BeginChase(bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.HasAttackRecoveryGroundSupport()、EnemyCombatModule.IsStandingAttackSurfaceStable() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.CloseAction()、VillagerController.EndAttack()；下游（含内部回调体） CharacterMotionExecutor.CommitActionPose(Vector3, Quaternion)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.BeginChase(bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.HasAttackRecoveryGroundSupport()、EnemyCombatModule.IsStandingAttackSurfaceStable() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void EndAttack()
    {
        // 旧行为树任务的退出回调不能夺走原版接触或倒地动作。
        if (NativeCombatActive)
        {
            return;
        }

        if (host == null)
        {
            return;
        }

        standingParryWindowOpen = false;
        HandleOnParryWindowClose();
        if (anim != null)
        {
            AnimationPresenter.SetBoolean(HashATK, false);
        }

        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.ReleaseAttack(host, ActionCoordinator.CurrentHandle);
        }

        attackFinished = true;
        // 行为树停用与场景卸载只能结束动作，不能再访问玩家、贴地或发起追击。
        if (!ActionCoordinator.CanReceiveRequest || !host.isActiveAndEnabled || !isActiveAndEnabled || !gameObject.activeInHierarchy || !gameObject.scene.isLoaded || behaviorTree == null || !behaviorTree.isActiveAndEnabled)
        {
            attackRecoveryPending = false;
            SetMovementMode(EnemyMovementMode.FullyStopped);
            return;
        }

        if (!attackRecoveryPending)
        {
            return;
        }

        attackRecoveryPending = false;
        SetMovementMode(EnemyMovementMode.FullyStopped);
        SnapToGround();
        // 已标记的屋顶和木板仍由追击贴地接管。缝隙可能同时漏过射线与
        // 小范围支撑探测，不能因此把敌人写回数米外的攻击起点。
        if (!host.HasPhysicalEnemySupport() && specialTraversal != null && specialTraversal.TryBeginUnsupportedDrop())
        {
            return;
        }

        if (IsDead || IsQTEControlled || pendingHit || pendingCrawl || pendingStandUp || !combatEngaged || Player == null)
        {
            return;
        }

        BeginChase();
        RefreshChaseDestination();
    }

    /// <summary>EndCrawlAttack 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EndCrawlAttack()；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void EndCrawlAttack()
    {
        AnimationPresenter.SetBoolean(HashATK, false);
        SetMovementMode(EnemyMovementMode.FullyStopped);
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue34 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.ReleaseCombatReservation(host);
        }
    }

    /// <summary>RestoreCrawlAfterAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RestoreCrawlAfterAttack()；下游（含内部回调体） EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetObservableState(VillagerState, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RestoreCrawlAfterAttack()
    {
        if (IsDead || !pendingCrawl)
        {
            return;
        }

        if (pendingHit || pendingStandUp)
        {
            return;
        }

        crawlPose = true;
        SetObservableState(VillagerState.Crawl);
        SetMovementMode(EnemyMovementMode.CrawlCodeDriven);
    }

    /// <summary>ClearActionParameters 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ClearActionParameters()；下游（含内部回调体） EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetIntegerValue(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ClearActionParameters()
    {
        if (HasUnfinishedStandingHit && !IsQTEControlled)
        {
            return;
        }

        ClearNativeBodyReaction();
        AnimationPresenter.SetBoolean(HashATK, false);
        AnimationPresenter.SetBoolean(HashHIT, false);
        AnimationPresenter.SetBoolean(HashSearchMode, false);
        AnimationPresenter.SetIntegerValue(HashTurnDir, 0);
        pendingStandingStaggerBodyTechnique = false;
        currentStandingStaggerBodyTechnique = false;
        if (anim != null)
        {
            AnimationPresenter.ClearTrigger(HashHitTrigger);
            if (staggerTriggerParameterAvailable)
            {
                AnimationPresenter.ClearTrigger(HashStaggerTrigger);
            }
        }
    }

    /// <summary>IsPlayerWithinBodyContactRange 的业务处理入口；本方法直接依赖 EnemyColliderController.GetHorizontalReach(Vector3)、EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanCrawlAttack()、EnemyCombatModule.CanEngagePlayerAtCloseRange()、VillagerController.IsPlayerWithinBodyContactRange()；下游（含内部回调体） EnemyColliderController.GetHorizontalReach(Vector3)、EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsPlayerWithinBodyContactRange()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerWithinCombatVerticalRange(player))
        {
            return false;
        }

        // 目标玩家。
Vector3 toPlayer = GetHorizontalVectorToPlayerBody(player);
        // 距离。
float distance = toPlayer.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        // 身体抵达。
float bodyReach;
        if (colliderCtrl != null)
        {
            bodyReach = colliderCtrl.GetHorizontalReach(toPlayer / distance);
        }
        else
        {
            bodyReach = 0.35f;
        }

        return distance <= bodyReach + attackContactPadding + attackRangeEpsilon;
    }

    /// <summary>CanEngagePlayerAtCloseRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.CanSeePlayer()、EnemyCombatModule.HasClearLineToPlayer(bool, float)、EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsPlayerWithinBodyContactRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanAttack()、EnemyCombatModule.CanCrawlAttack()、EnemyCombatModule.ConsumeHit()、VillagerController.CanEngagePlayerAtCloseRange()；下游（含内部回调体） EnemyCombatModule.CanSeePlayer()、EnemyCombatModule.HasClearLineToPlayer(bool, float)、EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsPlayerWithinBodyContactRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool CanEngagePlayerAtCloseRange()
    {
        if (!IsPlayerWithinAttackStartRange())
        {
            return false;
        }

        // 贴身时，角色碰撞体的重叠会使视线射线偶发落在无关碰撞体上。
        // 攻击动画仍会在命中帧再次校验视线，因此这里允许已接触目标的敌人稳定出招。
        if (IsPlayerWithinBodyContactRange())
        {
            return true;
        }

        if (CanSeePlayer())
        {
            return true;
        }

        if (Time.time - lastSeenTime > awarenessMemoryDuration)
        {
            return false;
        }

        return HasClearLineToPlayer(false, sightRange);
    }

    /// <summary>IsPlayerWithinAttackStartRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanAttack()、EnemyCombatModule.CanCrawlAttack()、EnemyCombatModule.CanEngagePlayerAtCloseRange()、VillagerController.IsPlayerWithinAttackStartRange()；下游（含内部回调体） EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsPlayerWithinAttackStartRange()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerWithinCombatVerticalRange(player))
        {
            return false;
        }

        float calibratedRange = CalibratedStandingAttackRange();
        if (!crawlPose && calibratedRange > 0f)
        {
            return HorizontalSwingDistance(player) <= calibratedRange;
        }

        if (!crawlPose && standingWeaponAttackStartRange > 0f)
        {
            return HorizontalSwingDistance(player) <= standingWeaponAttackStartRange;
        }

        return GetHorizontalVectorToPlayerBody(player).magnitude <= attackStartRange;
    }

    /// <summary>IsPlayerWithinCombatHoldRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ConsumeHit()、VillagerController.IsPlayerWithinCombatHoldRange()；下游（含内部回调体） EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)、EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsPlayerWithinCombatHoldRange()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerWithinCombatVerticalRange(player))
        {
            return false;
        }

        // exitRange（退出范围）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyCombatModule.IsPlayerWithinCombatHoldRange()。
        if (!crawlPose && standingWeaponAttackStartRange > 0f)
        {
            return HorizontalSwingDistance(player) <= standingWeaponAttackStartRange + 0.25f;
        }
        float exitRange = Mathf.Max(attackStartRange, combatHoldExitRange);
        return GetHorizontalVectorToPlayerBody(player).magnitude <= exitRange;
    }

    /// <summary>IsPlayerWithinCombatVerticalRange 的业务处理入口；本方法直接依赖 Component.TryGetComponent&lt;EnemyWindowTraversal&gt;(out EnemyWindowTraversal)、Component.TryGetComponent&lt;LeonWindowTraversal&gt;(out LeonWindowTraversal)、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、HouseThreeRegionNavigation.TreadRegion(Vector3)、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsPlayerWithinBodyContactRange()、EnemyCombatModule.IsPlayerWithinCombatHoldRange()、VillagerController.IsPlayerWithinCombatVerticalRange(LeonController)；下游（含内部回调体） Component.TryGetComponent&lt;EnemyWindowTraversal&gt;(out EnemyWindowTraversal)、Component.TryGetComponent&lt;LeonWindowTraversal&gt;(out LeonWindowTraversal)、EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool)、HouseThreeRegionNavigation.TreadRegion(Vector3)、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">玩家。</param>
    internal bool IsPlayerWithinCombatVerticalRange(LeonController player)
    {
        if (player == null)
        {
            return false;
        }

        // 区域。
var region = HouseThreeRegionNavigation.Current;
        if (region != null)
        {
            // 是否敌人所在平台。
bool enemyAtPlatform = region.TreadRegion(transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform;
            // 是否玩家所在平台。
bool playerAtPlatform = region.TreadRegion(player.transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform;
            if (TryGetComponent<EnemyWindowTraversal>(out var /* 中文含义：敌人通行。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enemyTraversal) && enemyTraversal.TryGetPlatformRegionState(region.outerPlatformIdentifier, out var /* 中文含义：是否敌人归属。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enemyOwnership))
            {
                enemyAtPlatform = enemyOwnership;
            }

            if (player.TryGetComponent<LeonWindowTraversal>(out var /* 中文含义：玩家通行。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ playerTraversal) && playerTraversal.TryGetEnemyPlatformRegionState(region.outerPlatformIdentifier, out var /* 中文含义：是否玩家归属。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ playerOwnership))
            {
                playerAtPlatform = playerOwnership;
            }

            if (enemyAtPlatform != playerAtPlatform)
            {
                return false;
            }
        }

        // 容差。
float tolerance;
        if (pendingCrawl || CurrentState == VillagerState.Crawl || CurrentState == VillagerState.CrawlAttack)
        {
            tolerance = crawlCombatVerticalTolerance;
        }
        else
        {
            tolerance = standingCombatVerticalTolerance;
        }

        return Mathf.Abs(player.transform.position.y - transform.position.y) <= Mathf.Max(0.1f, tolerance);
    }

    /// <summary>CaptureAttackRecoveryAnchor 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()、VillagerController.CaptureAttackRecoveryAnchor()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CaptureAttackRecoveryAnchor()
    {
        attackRecoveryPosition = transform.position;
        attackRecoveryRotation = transform.rotation;
        attackRecoveryPending = true;
    }

    /// <summary>IsFacingPlayerForAttack 的业务处理入口；本方法直接依赖 Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsFacingPlayerForAttack()；下游（含内部回调体） Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsFacingPlayerForAttack()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        // 方向。
Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, direction) <= attackHitAngle * 0.5f;
    }

    /// <summary>IsFacingPlayerForCrawlAttack 的业务处理入口；本方法直接依赖 Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanCrawlAttack()、VillagerController.IsFacingPlayerForCrawlAttack()；下游（含内部回调体） Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsFacingPlayerForCrawlAttack()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        // 方向。
Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        return Vector3.Angle(transform.forward, direction) <= crawlAttackStartAngle * 0.5f;
    }

    /// <summary>GetHorizontalVectorToPlayerBody 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Collider&gt;()、Mathf.Clamp(float, float, float)、Transform.IsChildOf(Transform)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsPlayerWithinAttackStartRange()、EnemyCombatModule.IsPlayerWithinBodyContactRange()、EnemyCombatModule.IsPlayerWithinCombatHoldRange()、EnemyCombatModule.TryApplyAttackHit(int)、VillagerController.GetHorizontalVectorToPlayerBody(LeonController)；下游（含内部回调体） Component.GetComponentsInChildren&lt;Collider&gt;()、Mathf.Clamp(float, float, float)、Transform.IsChildOf(Transform)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">玩家。</param>
    internal Vector3 GetHorizontalVectorToPlayerBody(LeonController player)
    {
        // 后备。
Vector3 fallback = player.transform.position - transform.position;
        fallback.y = 0f;
        // 玩家碰撞体集合。
Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
        if (playerColliders == null || playerColliders.Length == 0)
        {
            return fallback;
        }

        // 原点。
Vector3 origin = transform.position;
        // 最近距离。
float nearestDistance = float.MaxValue;
        // 最近向量。
Vector3 nearestVector = fallback;
        // 是否已找到。
bool found = false;
        // playerCollider（玩家碰撞体）：每轮从玩家碰撞体集合取得一个元素，仅代表当前这一项。使用位置：EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)。
foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger || playerCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            // 包围盒。
Bounds bounds = playerCollider.bounds;
            // 最近点。
Vector3 closestPoint = new Vector3(Mathf.Clamp(origin.x, bounds.min.x, bounds.max.x), origin.y, Mathf.Clamp(origin.z, bounds.min.z, bounds.max.z));
            // 水平向量。
Vector3 horizontalVector = closestPoint - origin;
            horizontalVector.y = 0f;
            // 距离。
float distance = horizontalVector.magnitude;
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestVector = horizontalVector;
            found = true;
        }

        if (found)
        {
            return nearestVector;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>ResolveFallbackHitboxType 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Transform.InverseTransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveFallbackHitboxType(Vector3)；下游（含内部回调体） Mathf.Abs(float)、Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">命中点位置。</param>
    internal HitboxType ResolveFallbackHitboxType(Vector3 hitPoint)
    {
        // 局部坐标点。
Vector3 localPoint = transform.InverseTransformPoint(hitPoint);
        if (localPoint.y >= 1.42f)
        {
            return HitboxType.Head;
        }

        if (localPoint.y >= 0.82f)
        {
            if (Mathf.Abs(localPoint.x) >= 0.24f)
            {
                if (localPoint.x <= 0f)
                {
                    return HitboxType.LeftArm;
                }
                else
                {
                    return HitboxType.RightArm;
                }
            }

            return HitboxType.Chest;
        }

        if (localPoint.y >= 0.5f)
        {
            return HitboxType.Pelvis;
        }

        if (localPoint.x <= 0f)
        {
            return HitboxType.LeftLeg;
        }
        else
        {
            return HitboxType.RightLeg;
        }
    }

    /// <summary>HandleOnAttackComplete 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnAttackComplete()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnAttackComplete()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack))
        {
            return;
        }

        if (CurrentState == VillagerState.Attack && chainsawBridge == null)
        {
            normalSwingHitHasRecoverWindow = true;
        }

        standingParryWindowOpen = false;
        HandleOnParryWindowClose();
        AnimationPresenter.SetBoolean(HashATK, false);
        SetMovementMode(EnemyMovementMode.FullyStopped);
    }

    /// <summary>NotifyAttackStateMachineExited 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.HandleOnAttackStateMachineExit()、VillagerController.NotifyAttackStateMachineExited()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void NotifyAttackStateMachineExited()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && !attackRecoveryPending))
        {
            return;
        }

        attackFinished = true;
    }

    /// <summary>NotifyCrawlAttackStateExited 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.HandleOnCrawlAttackExit()、VillagerController.NotifyCrawlAttackStateExited()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void NotifyCrawlAttackStateExited()
    {
        if (IsDead || IsQTEControlled || CurrentState != VillagerState.CrawlAttack)
        {
            return;
        }

        attackFinished = true;
    }

    /// <summary>HandleOnAttackStateMachineExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyAttackStateMachineExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnAttackStateMachineExit()；下游（含内部回调体） EnemyCombatModule.NotifyAttackStateMachineExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnAttackStateMachineExit()
    {
        NotifyAttackStateMachineExited();
    }

    /// <summary>HandleOnCrawlAttackExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyCrawlAttackStateExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnCrawlAttackExit()；下游（含内部回调体） EnemyCombatModule.NotifyCrawlAttackStateExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnCrawlAttackExit()
    {
        NotifyCrawlAttackStateExited();
    }

    /// <summary>HandleOnColliderStand 的业务处理入口；本方法直接依赖 EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.SnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnColliderStand()；下游（含内部回调体） EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.SnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnColliderStand()
    {
        if (colliderCtrl == null)
        {
            return;
        }

        // 起身动画中段的旧事件不能提前扩张站立胶囊体。此时身体仍处于
        // 低姿态，提前切换会与地面和周围敌人重叠，制造单帧吸附。
        // 站立碰撞体统一在 CompleteStandUp 中恢复。
        if (pendingStandUp || CurrentState == VillagerState.StandUp)
        {
            return;
        }

        if (IsDead)
        {
            colliderCtrl.SetDisabled();
        }
        else if (supportsCrawl && pendingCrawl)
        {
            colliderCtrl.SetCrawl();
        }
        else
        {
            colliderCtrl.SetStanding();
        }

        SnapToGround();
    }

    /// <summary>HandleOnColliderCrawl 的业务处理入口；本方法直接依赖 EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnColliderCrawl()；下游（含内部回调体） EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnColliderCrawl()
    {
        if (colliderCtrl == null)
        {
            return;
        }

        if (IsDead)
        {
            colliderCtrl.SetDisabled();
            return;
        }

        // 腿部受击动画也携带此事件。只有控制器已经确认进入爬行时，
        // 才允许把站立胶囊体切换为低矮盒体。
        if (!supportsCrawl || !pendingCrawl)
        {
            colliderCtrl.SetStanding();
            if (CanLogMovementDiagnostic)
            {
                DiagLog("OnColliderCrawl", $"忽略无效爬行碰撞事件: supportsCrawl={supportsCrawl}, pendingCrawl={pendingCrawl}");
            }

            return;
        }

        colliderCtrl.SetCrawl();
    }

    /// <summary>HandleOnColliderDisable 的业务处理入口；本方法直接依赖 EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.SnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnColliderDisable()；下游（含内部回调体） EnemyColliderController.SetCrawl()、EnemyColliderController.SetDisabled()、EnemyColliderController.SetStanding()、EnemyCombatModule.SnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnColliderDisable()
    {
        if (colliderCtrl == null)
        {
            return;
        }

        if (IsDead)
        {
            colliderCtrl.SetDisabled();
        }
        else if (supportsCrawl && pendingCrawl)
        {
            colliderCtrl.SetCrawl();
        }
        else
        {
            colliderCtrl.SetStanding();
        }

        SnapToGround();
    }

    /// <summary>HandleOnHitComplete 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnHitComplete()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnHitComplete()
    {
        if (IsDead || IsQTEControlled || CurrentState != VillagerState.Staggered || StandingHitRecoveryActive)
        {
            return;
        }

        ClearNativeBodyReaction();
        if (currentStandingStaggerBodyTechnique && Time.time < bodyTechniqueWindowEndTime)
        {
            deferredStandingHitCompletion = true;
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("OnHitComplete", "进入: HIT→false，保留当前移动模式");
        }

        if (!TryBeginStandingHitRecovery())
        {
            ReleaseStandingHitPose();
        }
    }

    /// <summary>UpdateStandingHitCompletion 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateStandingHitCompletion()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateStandingHitCompletion()
    {
        if (UpdateNativeStandingHitRecovery())
        {
            return;
        }

        if (!deferredStandingHitCompletion || Time.time < bodyTechniqueWindowEndTime)
        {
            return;
        }

        deferredStandingHitCompletion = false;
        currentStandingStaggerBodyTechnique = false;
        bodyTechniqueWindowEndTime = 0f;
        if (TryBeginStandingHitRecovery()) return;
        ReleaseStandingHitPose();
        hitFinished = true;
    }

    private bool WillPursueAfterHit()
    {
        return !pendingCrawl && fullyAwareOfPlayer && Player != null
            && (!IsPlayerWithinCombatHoldRange() || !CanEngagePlayerAtCloseRange());
    }

    private void ReleaseStandingHitPose()
    {
        AnimationPresenter.SetBoolean(HashHIT, false);
        bool upperBodyHit = pendingHitDirection == 1 || pendingHitDirection == 2
            || (pendingStandingStagger && pendingStaggerLevel == 0);
        if (upperBodyHit && chainsawBridge != null && chainsawBridge.HasStartedChainsaw && chainsawBridge.HasResolvedIntroTaunt
            && !host.IsNavigationTraversalProtected && WillPursueAfterHit()
            && AnimationPresenter.BlendSawHitIntoPursuit())
        {
            // 直接过渡会绕过受击子状态机的 Exit 节点，沿用该节点的受击结束处理。
            NotifyHitStateMachineExited();
        }
    }

    /// <summary>NotifyHitStateMachineExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.HandleOnHitStateMachineExit()、VillagerController.NotifyHitStateMachineExited()；下游（含内部回调体） EnemyCombatModule.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void NotifyHitStateMachineExited()
    {
        if (IsDead || IsQTEControlled || CurrentState != VillagerState.Staggered || !pendingHit || StandingHitRecoveryActive)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("OnHitStateMachineExit", $"受击状态机退出, hitFinished→true, " + $"pendingCrawl={pendingCrawl}, permanentCrawl={permanentCrawl}");
        }

        ClearNativeBodyReaction();
        hitFinished = !currentStandingStaggerBodyTechnique || Time.time >= bodyTechniqueWindowEndTime;
        if (!hitFinished)
        {
            deferredStandingHitCompletion = true;
        }
    }

    /// <summary>HandleOnHitStateMachineExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyHitStateMachineExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnHitStateMachineExit()；下游（含内部回调体） EnemyCombatModule.NotifyHitStateMachineExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnHitStateMachineExit()
    {
        NotifyHitStateMachineExited();
    }

    /// <summary>HandleOnStandUpComplete 的业务处理入口；本方法直接依赖 EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnStandUpComplete()；下游（含内部回调体） EnemyCombatModule.DiagLog(string, string)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnStandUpComplete()
    {
        if (IsDead || IsQTEControlled || CurrentState != VillagerState.StandUp)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("OnStandUpComplete", "起身完成事件, 等待状态退出统一恢复碰撞体与贴地");
        }

        SetMovementMode(EnemyMovementMode.FullyStopped);
    }

    /// <summary>NotifyStandUpStateExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.HandleOnStandUpStateExit()、VillagerController.NotifyStandUpStateExited()；下游（含内部回调体） EnemyCombatModule.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void NotifyStandUpStateExited()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.StandUp && !pendingStandUp))
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("OnStandUpStateExit", $"起身状态退出, standUpFinished→true, permanentCrawl={permanentCrawl}");
        }

        standUpFinished = true;
    }

    /// <summary>HandleOnStandUpStateExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyStandUpStateExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnStandUpStateExit()；下游（含内部回调体） EnemyCombatModule.NotifyStandUpStateExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnStandUpStateExit()
    {
        NotifyStandUpStateExited();
    }

    /// <summary>HandleOnAttackHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.TryApplyAttackHit(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnAttackHit()；下游（含内部回调体） EnemyCombatModule.TryApplyAttackHit(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnAttackHit()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack))
        {
            return;
        }

        TryApplyAttackHit(0);
    }

    /// <summary>HandleOnParryWindowOpen 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Component.GetComponent&lt;LeonCombat&gt;()、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.RegisterIncomingAttackPrompt(KnifeIncomingAttackContext)、LeonCombat.OpenParryOpportunity(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.UpdateStandingAttackHitFallback()、VillagerController.OnParryWindowOpen()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Component.GetComponent&lt;LeonCombat&gt;()、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.RegisterIncomingAttackPrompt(KnifeIncomingAttackContext)、LeonCombat.OpenParryOpportunity(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnParryWindowOpen()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack))
        {
            return;
        }

        if (!enableKnifeParry || chainsawBridge != null || CurrentState != VillagerState.Attack || normalSwingHitHasRecoverWindow || attackFinished)
        {
            return;
        }

        // player（玩家）：使用位置：EnemyCombatModule.HandleOnParryWindowOpen()。
        if (UsesKnifeContactTimeline)
        {
            return;
        }

LeonController player = Player;
        if (player == null)
        {
            return;
        }

        // 战斗。
var combat = player.GetComponent<LeonCombat>();
        if (combat != null && combat.knifeModule != null && combat.knifeModule.Ready)
        {
            // 传入攻击。
var incomingAttack = CreateKnifeIncomingAttack(0, attackDamage, transform.position + Vector3.up, transform.forward);
            // 当前动画状态信息。
var state = anim.GetCurrentAnimatorStateInfo(0);
            // 标记。
float marker;
            if (CurrentKnifeConfiguration != null)
            {
                marker = CurrentKnifeConfiguration.contactNormalizedPoint;
            }
            else
            {
                marker = standingAttackFallbackHitNormalizedTime;
            }

            incomingAttack.expectedContactTime = Time.timeAsDouble + Mathf.Max(0, marker - state.normalizedTime) * state.length;
            combat.knifeModule.RegisterIncomingAttackPrompt(incomingAttack);
        }
        else
        {
            // 判空前缓存的战斗。
global::LeonCombat receiverValue41 = combat;
            if (!global::System.Object.ReferenceEquals(receiverValue41, null))
            {
                receiverValue41.OpenParryOpportunity(host);
            }
        }
    }

    /// <summary>HandleOnParryWindowClose 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonCombat&gt;()、KnifeCombatModule.CloseIncomingAttackPrompt(VillagerController)、LeonCombat.CloseParryOpportunity(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyCombatModule.ApplyKnifeParryStagger(bool)、EnemyCombatModule.EndAttack()、EnemyCombatModule.HandleOnAttackComplete()、EnemyCombatModule.TryApplyAttackHit(int) 等共 7 处；下游（含内部回调体） Component.GetComponent&lt;LeonCombat&gt;()、KnifeCombatModule.CloseIncomingAttackPrompt(VillagerController)、LeonCombat.CloseParryOpportunity(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnParryWindowClose()
    {
        // 玩家。
LeonController player = cachedPlayer;
        if (player == null)
        {
            return;
        }

        // 战斗。
var combat = player.GetComponent<LeonCombat>();
        // 判空前缓存的战斗。
global::LeonCombat receiverValue42 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            // 判空前缓存的接收者值42的小刀战斗模块。
global::KnifeCombatModule receiverValue43 = receiverValue42.knifeModule;
            if (!global::System.Object.ReferenceEquals(receiverValue43, null))
            {
                receiverValue43.CloseIncomingAttackPrompt(host);
            }
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue44 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.CloseParryOpportunity(host);
        }
    }

    /// <summary>HandleOnAttackFirstHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.TryApplyAttackHit(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnAttackFirstHit()；下游（含内部回调体） EnemyCombatModule.TryApplyAttackHit(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnAttackFirstHit()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack))
        {
            return;
        }

        TryApplyAttackHit(0);
    }

    /// <summary>HandleOnAttackSecondHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.TryApplyAttackHit(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、VillagerController.OnAttackSecondHit()；下游（含内部回调体） EnemyCombatModule.TryApplyAttackHit(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void HandleOnAttackSecondHit()
    {
        if (IsDead || IsQTEControlled || (CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack))
        {
            return;
        }

        TryApplyAttackHit(1);
    }

    /// <summary>TryApplyAttackHit 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.GetChainsawDamageForward()、ChainsawManAnimationBridge.GetChainsawDamageOrigin()、ChainsawManAnimationBridge.PlayChainsawHit()、ChainsawManAnimationBridge.ResolveAttackDamage(float)、Component.GetComponent&lt;LeonCombat&gt;()、EnemyCombatModule.ApplyKnifeParryStagger(bool)、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3) 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnAttackFirstHit()、EnemyCombatModule.HandleOnAttackHit()、EnemyCombatModule.HandleOnAttackSecondHit()、EnemyCombatModule.UpdateStandingAttackHitFallback()、VillagerController.TryApplyAttackHit(int)；下游（含内部回调体） ChainsawManAnimationBridge.GetChainsawDamageForward()、ChainsawManAnimationBridge.GetChainsawDamageOrigin()、ChainsawManAnimationBridge.PlayChainsawHit()、ChainsawManAnimationBridge.ResolveAttackDamage(float)、Component.GetComponent&lt;LeonCombat&gt;()、EnemyCombatModule.ApplyKnifeParryStagger(bool)、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3) 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitIndex">命中索引。</param>
    internal void TryApplyAttackHit(int hitIndex)
    {
        if (UsesKnifeContactTimeline)
        {
            // 旧事件只报告标记，逐帧轨迹是普通攻击的唯一接触入口。
            return;
        }

        // 安全命中索引。
int safeHitIndex = Mathf.Clamp(hitIndex, 0, 30);
        // 命中遮罩。
int hitMask = 1 << safeHitIndex;
        bool nativeSawContact = UsesNativeSawContact;
        if (currentHealth <= 0 || (!nativeSawContact && (attackHitMask & hitMask) != 0))
        {
            return;
        }

        if (siegeAttackDoor != null)
        {
            if (!nativeSawContact && !siegeAttackLanded)
            {
                siegeAttackLanded = siegeAttackDoor.TrySawSiegeDoor(chainsawBridge.GetChainsawDamageOrigin(), 0.65f);
            }
            return;
        }

        // 普通站立攻击只在挥击点取样一次，动画事件和逐帧兜底共用这次机会。
        if (CurrentState == VillagerState.Attack && chainsawBridge == null)
        {
            if (normalSwingHitHasRecoverWindow || attackFinished)
            {
                return;
            }

            normalSwingHitHasRecoverWindow = true;
        }

        HandleOnParryWindowClose();
        standingParryWindowOpen = false;
        // 原点。
Vector3 origin;
        if (chainsawBridge != null)
        {
            origin = chainsawBridge.GetChainsawDamageOrigin();
        }
        else
        {
            origin = transform.position + Vector3.up;
        }

        // 攻击前向。
Vector3 attackForward;
        if (chainsawBridge != null)
        {
            attackForward = chainsawBridge.GetChainsawDamageForward();
        }
        else
        {
            attackForward = transform.forward;
        }

        // 解算后的伤害。
float resolvedDamage;
        if (chainsawBridge != null)
        {
            resolvedDamage = chainsawBridge.ResolveAttackDamage(attackDamage);
        }
        else
        {
            resolvedDamage = attackDamage;
        }

        // normalConfiguration（常规配置）：使用位置：EnemyCombatModule.TryApplyAttackHit(int)。
        if (nativeSawContact)
        {
            // 原有动画事件继续处理友伤和提示。里昂的受击由刀链扫掠结算。
            if ((nativeSawFriendlyHitMask & hitMask) == 0)
            {
                nativeSawFriendlyHitMask |= hitMask;
                if (TryApplyFriendlyFire(origin, attackForward, resolvedDamage))
                {
                    chainsawBridge.PlayChainsawHit();
                }
            }
            return;
        }

global::KnifeEnemyAttackProfile normalConfiguration;
        if (CurrentState == VillagerState.Attack && chainsawBridge == null)
        {
            normalConfiguration = CurrentKnifeConfiguration;
        }
        else
        {
            normalConfiguration = null;
        }

        // 当前范围。
float currentRange;
        if (normalConfiguration != null)
        {
            currentRange = normalConfiguration.contactDistance;
        }
        else
        {
            currentRange = attackHitRange;
        }

        // 当前角度。
float currentAngle;
        if (normalConfiguration != null)
        {
            currentAngle = normalConfiguration.contactAngle;
        }
        else
        {
            currentAngle = attackHitAngle;
        }

        // 是否已应用命中。
bool appliedHit = false;
        // 玩家。
LeonController player = Player;
        if (player != null)
        {
            // 目标。
Vector3 target = player.transform.position + Vector3.up;
            // 身体方向。
Vector3 bodyDirection = GetHorizontalVectorToPlayerBody(player);
            // 朝向方向。
Vector3 facingDirection = player.transform.position - transform.position;
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude <= 0.001f)
            {
                facingDirection = bodyDirection;
            }

            // 是否内部命中范围。
bool isWithinHitRange = bodyDirection.magnitude <= currentRange + attackRangeEpsilon;
            // 是否内部命中角度。
bool isWithinHitAngle = facingDirection.sqrMagnitude <= 0.001f || Vector3.Angle(attackForward, facingDirection) <= currentAngle * 0.5f;
            // 是否具有清除攻击线。
bool hasClearAttackLine = HasClearLineToPlayer(false, Mathf.Max(attackHitRange + 0.5f, PlayerDistance + 0.1f));
            if (isWithinHitRange && isWithinHitAngle && hasClearAttackLine)
            {
                // 判空前缓存的取得组件的返回结果。
global::LeonCombat receiverValue51 = player.GetComponent<LeonCombat>();
                // 新小刀。
global::KnifeCombatModule newKnife;
                if (!global::System.Object.ReferenceEquals(receiverValue51, null))
                {
                    newKnife = receiverValue51.knifeModule;
                }
                else
                {
                    newKnife = default;
                }

                if (chainsawBridge == null && newKnife != null && newKnife.Ready)
                {
                    newKnife.CommitIncomingAttack(CreateKnifeIncomingAttack(safeHitIndex, resolvedDamage, Vector3.Lerp(origin, target, .65f), attackForward));
                    appliedHit = true;
                }
                else
                {
                    // 招架结果。
KnifeParryResult parryResult;
                    if (enableKnifeParry && chainsawBridge == null)
                    {
                        // 判空前缓存的取得组件的返回结果。
global::LeonCombat receiverValue53 = player.GetComponent<LeonCombat>();
                        // 空值判断前缓存的尝试解算招架的返回结果。
global::KnifeParryResult? availableValue55;
                        if (!global::System.Object.ReferenceEquals(receiverValue53, null))
                        {
                            availableValue55 = receiverValue53.TryResolveParry(host, origin, attackForward);
                        }
                        else
                        {
                            availableValue55 = default;
                        }

                        if (availableValue55.HasValue)
                        {
                            parryResult = availableValue55.Value;
                        }
                        else
                        {
                            parryResult = KnifeParryResult.None;
                        }
                    }
                    else
                    {
                        parryResult = KnifeParryResult.None;
                    }

                    if (parryResult != KnifeParryResult.None)
                    {
                        ApplyKnifeParryStagger(parryResult == KnifeParryResult.Perfect);
                    }
                    else
                    {
                        player.TakeDamageFrom(resolvedDamage, origin, target);
                        if (host.TryGetComponent<EnemyHeldWeapon>(out var heldWeapon))
                        {
                            heldWeapon.PlayHitAudio(target);
                        }
                    }

                    appliedHit = true;
                }
            }
        }

        if (TryApplyFriendlyFire(origin, attackForward, resolvedDamage))
        {
            appliedHit = true;
        }

        if (appliedHit)
        {
            // 判空前缓存的电锯桥接。
global::ChainsawManAnimationBridge receiverValue58 = chainsawBridge;
            if (!global::System.Object.ReferenceEquals(receiverValue58, null))
            {
                receiverValue58.PlayChainsawHit();
            }

            attackHitMask |= hitMask;
        }
    }

    /// <summary>ApplyKnifeParryStagger 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.StopAndClearMovement()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TryApplyAttackHit(int)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、VillagerController.ApplyKnifeParryStagger(bool)；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.StopAndClearMovement()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="perfect">是否完美。</param>
    internal void ApplyKnifeParryStagger(bool perfect)
    {
        standingParryWindowOpen = false;
        HandleOnParryWindowClose();
        attackFinished = true;
        attackRecoveryPending = false;
        pendingHit = true;
        hitFinished = false;
        pendingCrawl = false;
        pendingTripOver = false;
        pendingStandingStagger = true;
        ClearNativeBodyReaction();
        pendingStandingStaggerBodyTechnique = perfect && !UsesNativeBodyTechnique;
        if (perfect && UsesNativeBodyTechnique)
        {
            pendingNativeBodyReaction = NativeBodyReaction.PerfectParry;
        }
        pendingStaggerDirection = 0;
        if (perfect)
        {
            pendingStaggerLevel = 2;
        }
        else
        {
            pendingStaggerLevel = 1;
        }

        pendingHitDirection = 5;
        accumulatedPoiseDamage = 0f;
        AnimationPresenter.SetBoolean(HashATK, false);
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue60 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue60, null))
        {
            receiverValue60.ReleaseCombatReservation(host);
        }

        SetMovementMode(EnemyMovementMode.FullyStopped);
        StopAndClearMovement();
        // 接触已经确认，同帧经动作协调器提交敌人反应，不等待行为树下一次轮询。
        ActionCoordinator.RequestAction(GameArchitecture.ActionKind.HitReaction, "小刀弹反接触");
    }

    /// <summary>UpdateStandingAttackHitFallback 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.IsStandingAttackAnimation(AnimatorStateInfo)、EnemyCombatModule.TryApplyAttackHit(int) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateStandingAttackHitFallback()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.IsStandingAttackAnimation(AnimatorStateInfo)、EnemyCombatModule.TryApplyAttackHit(int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateStandingAttackHitFallback()
    {
        if (UsesKnifeContactTimeline)
        {
            return;
        }

        if (anim == null || chainsawBridge != null || CurrentState != VillagerState.Attack || normalSwingHitHasRecoverWindow || attackFinished)
        {
            if (standingParryWindowOpen)
            {
                standingParryWindowOpen = false;
                HandleOnParryWindowClose();
            }

            return;
        }

        // 当前动画状态信息。
AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!IsStandingAttackAnimation(stateInfo) && anim.IsInTransition(0))
        {
            stateInfo = anim.GetNextAnimatorStateInfo(0);
        }

        if (!IsStandingAttackAnimation(stateInfo))
        {
            return;
        }

        // 接触点。
float contactPoint;
        if (CurrentKnifeConfiguration != null)
        {
            contactPoint = CurrentKnifeConfiguration.contactNormalizedPoint;
        }
        else
        {
            contactPoint = standingAttackFallbackHitNormalizedTime;
        }

        // 按原顺序缓存的实参0.05f。
float argumentValue64 = 0.05f;
        // 计算前缓存的左侧接触点。
float leftOperand63 = contactPoint;
        // 分支选出的当前小刀配置的提示提前。
float selectedValue62;
        if (CurrentKnifeConfiguration != null)
        {
            selectedValue62 = CurrentKnifeConfiguration.promptEarly;
        }
        else
        {
            selectedValue62 = .18f;
        }

        // 招架打开时间。
float parryOpenTime = Mathf.Max(argumentValue64, leftOperand63 - (selectedValue62));
        if (!standingParryWindowOpen && stateInfo.normalizedTime >= parryOpenTime && stateInfo.normalizedTime < contactPoint)
        {
            standingParryWindowOpen = true;
            HandleOnParryWindowOpen();
        }

        if (stateInfo.normalizedTime < contactPoint)
        {
            return;
        }

        // 动画事件未到时由兜底完成同一次取样。
        TryApplyAttackHit(0);
    }

    /// <summary>IsStandingAttackAnimation 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.LateUpdate()、EnemyCombatModule.UpdateStandingAttackHitFallback()、VillagerController.IsStandingAttackAnimation(AnimatorStateInfo)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateInfo">当前动画状态信息。</param>
    internal static bool IsStandingAttackAnimation(AnimatorStateInfo stateInfo)
    {
        return stateInfo.shortNameHash == HashAttack01State || stateInfo.shortNameHash == HashAttack02State || stateInfo.shortNameHash == HashAttack03State;
    }

    /// <summary>TryApplyFriendlyFire 的业务处理入口；本方法直接依赖 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、Mathf.Abs(float)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsInactive, FindObjectsSortMode)、object.ReferenceEquals(object, object)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()、VillagerController.TakeHeavyFrontDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.LateUpdate()、EnemyCombatModule.TryApplyAttackHit(int)、VillagerController.TryApplyFriendlyFire(Vector3, Vector3, float)；下游（含内部回调体） EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、Mathf.Abs(float)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsInactive, FindObjectsSortMode)、object.ReferenceEquals(object, object)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()、VillagerController.TakeHeavyFrontDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">原点。</param>
    /// <param name="forward">前向。</param>
    /// <param name="baseDamage">基础伤害。</param>
    internal bool TryApplyFriendlyFire(Vector3 origin, Vector3 forward, float baseDamage)
    {
        // 是否使用电锯调参。
bool useChainsawTuning = enableChainsawFriendlyFire && chainsawBridge != null && missionEnemyType == MissionEnemyType.Sawman;
        // 是否启用。
bool enabled;
        if (useChainsawTuning)
        {
            enabled = enableChainsawFriendlyFire;
        }
        else
        {
            enabled = enableEnemyFriendlyFire;
        }

        // 范围。
float range;
        if (useChainsawTuning)
        {
            range = chainsawFriendlyFireRange;
        }
        else
        {
            range = enemyFriendlyFireRange;
        }

        // 角度。
float angle;
        if (useChainsawTuning)
        {
            angle = chainsawFriendlyFireAngle;
        }
        else
        {
            angle = enemyFriendlyFireAngle;
        }

        // 伤害倍率。
float damageMultiplier;
        if (useChainsawTuning)
        {
            damageMultiplier = chainsawFriendlyFireDamageMultiplier;
        }
        else
        {
            damageMultiplier = enemyFriendlyFireDamageMultiplier;
        }

        // 最大目标数。
int maxTargets;
        if (useChainsawTuning)
        {
            maxTargets = chainsawFriendlyFireMaxTargets;
        }
        else
        {
            maxTargets = enemyFriendlyFireMaxTargets;
        }

        // 高度容差。
float heightTolerance;
        if (useChainsawTuning)
        {
            heightTolerance = chainsawFriendlyFireHeightTolerance;
        }
        else
        {
            heightTolerance = enemyFriendlyFireHeightTolerance;
        }

        if (!enabled || range <= 0f || damageMultiplier <= 0f || maxTargets == 0)
        {
            return false;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        forward.Normalize();
        // 范围平方。
float rangeSqr = range * range;
        // 一半角度。
float halfAngle = angle * 0.5f;
        // 伤害。
float damage = baseDamage * damageMultiplier;
        // 是否已应用。
bool applied = false;
        // 已应用数量。
int appliedCount = 0;
        // 候选集合。
VillagerController[] candidates = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // candidate（候选）：每轮从候选集合取得一个元素，仅代表当前这一项。使用位置：EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float)。
foreach (var candidate in candidates)
        {
            if (candidate == null || candidate == host || candidate.IsDead)
            {
                continue;
            }

            // 命中点位置。
Vector3 hitPoint = candidate.transform.position + Vector3.up * 1.1f;
            // 偏移量。
Vector3 offset = hitPoint - origin;
            // 高度。
float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            if (height > heightTolerance || offset.sqrMagnitude <= 0.0001f || offset.sqrMagnitude > rangeSqr)
            {
                continue;
            }

            // 方向。
Vector3 direction = offset.normalized;
            if (Vector3.Angle(forward, direction) > halfAngle)
            {
                continue;
            }

            // 受击者在确认硬直或死亡后自行接管移动。
            candidate.TakeHeavyFrontDamage(damage, hitPoint);
            if (chainsawBridge == null && host.TryGetComponent<EnemyHeldWeapon>(out var heldWeapon))
            {
                heldWeapon.PlayHitAudio(hitPoint);
            }
            // 判空前缓存的敌人命中效果管理器的实例。
global::EnemyHitEffectManager receiverValue71 = EnemyHitEffectManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue71, null))
            {
                receiverValue71.PlayBloodEffect(hitPoint, -direction, HitboxType.Chest, playImpactSound: false);
            }

            applied = true;
            appliedCount++;
            if (maxTargets > 0 && appliedCount >= maxTargets)
            {
                break;
            }
        }

        return applied;
    }

    /// <summary>AbandonAttackRestore 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.PrepareForSpecialTraversal()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void AbandonAttackRestore()
    {
        attackRecoveryPending = false;
    }

    /// <summary>ClearDeathCombatRequest 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ClearDeathCombatRequest()
    {
        ClearNativeBodyReaction();
        pendingHit = false;
        pendingCrawl = false;
        pendingStandUp = false;
        pendingTripOver = false;
        pendingStandingStagger = false;
        pendingStandingStaggerBodyTechnique = false;
        currentStandingStaggerBodyTechnique = false;
    }

    /// <summary>AdvanceStaggerRestore 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void AdvanceStaggerRestore()
    {
        if (accumulatedPoiseDamage > 0f && Time.time - lastPoiseDamageTime >= host.poiseDecayDelay)
        {
            accumulatedPoiseDamage = Mathf.Max(0f, accumulatedPoiseDamage - host.poiseDecayPerSecond * Time.deltaTime);
        }
    }

    /// <summary>CalculateStaggerRestoreMultiplier 的业务处理入口；本方法直接依赖 float.IsNegativeInfinity(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.GetTargetMoveSpeed()；下游（含内部回调体） float.IsNegativeInfinity(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal float CalculateStaggerRestoreMultiplier()
    {
        if (float.IsNegativeInfinity(staggerRecoveryStartedTime))
        {
            return 1f;
        }

        // 比例。
float ratio = Mathf.Clamp01((Time.time - staggerRecoveryStartedTime - host.staggerRecoveryMovementDelay) / Mathf.Max(0.1f, host.staggerRecoveryAccelerationDuration));
        if (ratio >= 1f)
        {
            staggerRecoveryStartedTime = float.NegativeInfinity;
        }

        return Mathf.SmoothStep(0f, 1f, ratio);
    }

    /// <summary>ApplyDeadChainsawDanger 的业务处理入口；本方法直接依赖 EnemyCombatModule.ApplyDeadChainsawPulse()、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginDeathContinuousDamage()；下游（含内部回调体） EnemyCombatModule.ApplyDeadChainsawPulse()、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private IEnumerator ApplyDeadChainsawDanger()
    {
        // 结束时间。
float endTime = Time.time + deadChainsawDangerDuration;
        // 等待。
WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, deadChainsawDamageInterval));
        // 身份标识。
var identity = ActionCoordinator.Identity;
        while (Time.time < endTime && isActiveAndEnabled && host.isActiveAndEnabled && identity == ActionCoordinator.Identity)
        {
            ApplyDeadChainsawPulse();
            yield return wait;
        }
    }

    /// <summary>ApplyDeadChainsawPulse 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.GetChainsawDamageOrigin()、EnemyCombatModule.IsPointInsideDeadChainsawDanger(Vector3, Vector3, float)、EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、HashSet&lt;LeonController&gt;.Add(LeonController)、HashSet&lt;LeonController&gt;.Clear()、HashSet&lt;VillagerController&gt;.Add(VillagerController)、HashSet&lt;VillagerController&gt;.Clear() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyDeadChainsawDanger()；下游（含内部回调体） ChainsawManAnimationBridge.GetChainsawDamageOrigin()、EnemyCombatModule.IsPointInsideDeadChainsawDanger(Vector3, Vector3, float)、EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、HashSet&lt;LeonController&gt;.Add(LeonController)、HashSet&lt;LeonController&gt;.Clear()、HashSet&lt;VillagerController&gt;.Add(VillagerController)、HashSet&lt;VillagerController&gt;.Clear() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyDeadChainsawPulse()
    {
        if (chainsawBridge == null || deadChainsawDangerRadius <= 0f || deadChainsawDamageMultiplier <= 0f)
        {
            return;
        }

        // 原点。
Vector3 origin = chainsawBridge.GetChainsawDamageOrigin();
        // 半径平方。
float radiusSqr = deadChainsawDangerRadius * deadChainsawDangerRadius;
        // 伤害。
float damage = attackDamage * deadChainsawDamageMultiplier;
        deadChainsawHitPlayers.Clear();
        // 玩家。
LeonController player = Player;
        if (player != null && player.currentState != LeonState.Dead && IsPointInsideDeadChainsawDanger(player.transform.position + Vector3.up, origin, radiusSqr))
        {
            deadChainsawHitPlayers.Add(player);
            player.TakeDamageFrom(damage, origin, player.transform.position + Vector3.up);
        }

        deadChainsawHitEnemies.Clear();
        // 候选集合。
VillagerController[] candidates = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // candidate（候选）：每轮从候选集合取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<VillagerController>.Add(VillagerController) 的参数、作为 HashSet<VillagerController>.Contains(VillagerController) 的参数、EnemyCombatModule.ApplyDeadChainsawPulse()。
foreach (var candidate in candidates)
        {
            if (candidate == null || candidate == host || candidate.IsDead || deadChainsawHitEnemies.Contains(candidate))
            {
                continue;
            }

            // 命中点位置。
Vector3 hitPoint = candidate.transform.position + Vector3.up;
            if (!IsPointInsideDeadChainsawDanger(hitPoint, origin, radiusSqr))
            {
                continue;
            }

            deadChainsawHitEnemies.Add(candidate);
            // 受击者在确认硬直或死亡后自行接管移动。
            candidate.TakeHeavyFrontDamage(damage, hitPoint);
            // 表面法线。
Vector3 normal = hitPoint - origin;
            if (normal.sqrMagnitude <= 0.0001f)
            {
                normal = -transform.forward;
            }

            // 判空前缓存的敌人命中效果管理器的实例。
global::EnemyHitEffectManager receiverValue72 = EnemyHitEffectManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue72, null))
            {
                receiverValue72.PlayBloodEffect(hitPoint, normal.normalized, HitboxType.Chest, playImpactSound: false);
            }
        }
    }

    /// <summary>IsPointInsideDeadChainsawDanger 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyDeadChainsawPulse()；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="origin">原点。</param>
    /// <param name="radiusSqr">半径平方。</param>
    private bool IsPointInsideDeadChainsawDanger(Vector3 point, Vector3 origin, float radiusSqr)
    {
        // 偏移量。
Vector3 offset = point - origin;
        if (Mathf.Abs(offset.y) > chainsawFriendlyFireHeightTolerance)
        {
            return false;
        }

        offset.y = 0f;
        return offset.sqrMagnitude <= radiusSqr;
    }

    /// <summary>死亡电锯命中玩家集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<LeonController> deadChainsawHitPlayers = new HashSet<LeonController>();
    /// <summary>死亡电锯命中敌人集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<VillagerController> deadChainsawHitEnemies = new HashSet<VillagerController>();
    /// <summary>死亡后方伤害协程。</summary>
    private Coroutine deadBackDamageCoroutine;
    /// <summary>BeginDeathContinuousDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.ApplyDeadChainsawDanger()、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） EnemyCombatModule.ApplyDeadChainsawDanger()、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginDeathContinuousDamage()
    {
        if (deadBackDamageCoroutine == null)
        {
            deadBackDamageCoroutine = StartCoroutine(ApplyDeadChainsawDanger());
        }
    }

    /// <summary>StopContinuousDamage 的业务处理入口；本方法直接依赖 MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.CloseAction()、EnemyCombatModule.OnDisable()；下游（含内部回调体） MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void StopContinuousDamage()
    {
        if (deadBackDamageCoroutine == null)
        {
            return;
        }

        StopCoroutine(deadBackDamageCoroutine);
        deadBackDamageCoroutine = null;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.StopContinuousDamage()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        CancelNativeCombat(false);
        nativeDeathTrack = null;
        assassinationEndTime = 0f;
        nativeDeathAction = default;
        ClearStandingMelee();
        ClearNativeBodyReaction();
        lastBodyTechniqueShot = default;
        bodyTechniquePoise = default;
        bodyTechniquePoiseTime = default;
        StopContinuousDamage();
    }

    /// <summary>死亡电锯危险持续时间。</summary>
    private float deadChainsawDangerDuration => host.deadChainsawDangerDuration;
    /// <summary>死亡电锯伤害间隔。</summary>
    private float deadChainsawDamageInterval => host.deadChainsawDamageInterval;
    /// <summary>死亡电锯危险半径。</summary>
    private float deadChainsawDangerRadius => host.deadChainsawDangerRadius;
    /// <summary>死亡电锯伤害倍率。</summary>
    private float deadChainsawDamageMultiplier => host.deadChainsawDamageMultiplier;
}
