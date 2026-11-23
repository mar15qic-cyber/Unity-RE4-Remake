using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 VillagerController，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>敌人外壳与兼容入口，联接生命、感知、战斗、动作、动画及运动模块；已拆分状态按现有模块归属查询，角色根位姿沿 CharacterMotionExecutor 授权提交。</remarks>
[RequireComponent(typeof(PathAgent))]
[RequireComponent(typeof(Animator))]
public partial class VillagerController : MonoBehaviour, IDamageable
{
    // 门洞通行必须是独立的短生命周期流程，不能仅靠“是否正在穿门”
    // 一个布尔值与普通追击互相猜测。三号屋上楼入口仍使用同一阶段名，
    // 但由其专用路线负责目标点与楼梯交接。
    /// <summary>敌人行为、动作与通行使用的 PursuitIngressPhase 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum PursuitIngressPhase
    {
        /// <summary>中文含义：无（未选择或尚未发生）。</summary>
        None,
        /// <summary>中文含义：接近。</summary>
        Approach,
        /// <summary>中文含义：等待通道。</summary>
        WaitingForPassage,
        /// <summary>中文含义：跨越。</summary>
        Crossing,
        /// <summary>中文含义：清理中入口。</summary>
        ClearingEntrance
    }

    /// <summary>中文含义：攻击范围极小容差。编译期常量，保持既有值与消费者映射。使用位置：VillagerController.ArchitectureReadAttackRangeEpsilon.get。</summary>
    private const float attackRangeEpsilon = 0.02f;
    /// <summary>中文含义：楼梯物理表面上方路线容差。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    private const float stairPhysicalSurfaceAboveRouteTolerance = 0.14f;
    /// <summary>中文含义：楼梯物理表面下方路线容差。编译期常量，保持既有值与消费者映射。使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。</summary>
    private const float stairPhysicalSurfaceBelowRouteTolerance = 0.22f;
    /// <summary>中文含义：是否具有登记门洞。使用位置：VillagerController.RegisterPursuitIngressWaiter(DoorController)、VillagerController.UnregisterPursuitIngressWaiter()。</summary>
    private DoorController hasRegisterDoorway;
    /// <summary>中文含义：门洞通行凭证。使用位置：作为 DoorController.ReleaseTraversal(TraversalToken, ReleaseReason) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.UnregisterPursuitIngressWaiter()。</summary>
    private GameArchitecture.TraversalToken doorwayTraversalToken;
    /// <summary>中文含义：当前门洞。使用位置：DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int)、DoorwayTraversalOccupancy.Valid(VillagerController, int)。</summary>
    internal DoorController CurrentDoorway => pursuitIngressDoor;
    /// <summary>中文含义：门洞来源侧边。使用位置：DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int)、DoorwayTraversalOccupancy.Valid(VillagerController, int)。</summary>
    internal int DoorwaySourceSide => pursuitIngressSourceSide;
    /// <summary>中文含义：是否门洞通行中间。使用位置：CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、DoorwayTraversalOccupancy.CleanupDirection()、DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int) 等共 4 处。</summary>
    internal bool DoorwayTraverseMiddle => pursuitIngressCrossing;
    /// <summary>中文含义：当前楼梯通行。使用位置：DoorwayTraversalOccupancy.Valid(VillagerController, int)。</summary>
    internal HouseThreeTraversalGeometry CurrentStairTraversal => door03TraversalRoute;

    /// <summary>中文含义：移动速度的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数、作为 EnemyAnimationAdapter.SetFloatValue(int, float) 的参数、VillagerController.ArchitectureReadHashMoveSpeed.get。</summary>
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    /// <summary>中文含义：移动行为播放速率的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashLocomotionPlaybackRate.get。</summary>
    private static readonly int HashLocomotionPlaybackRate = Animator.StringToHash("LocomotionPlaybackRate");
    /// <summary>中文含义：是否具有移动行为播放速率。</summary>
    private bool hasLocomotionPlaybackRate
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadHasLocomotionPlaybackRate;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：移动行为播放速率。</summary>
    private float locomotionPlaybackRate
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadLocomotionPlaybackRate;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：移动横向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAnimationAdapter.SetFloatValue(int, float) 的参数、VillagerController.ArchitectureReadHashMoveX.get。</summary>
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    /// <summary>中文含义：移动纵向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAnimationAdapter.SetFloatValue(int, float) 的参数、VillagerController.ArchitectureReadHashMoveY.get。</summary>
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    /// <summary>中文含义：行走的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAnimationAdapter.SetBoolean(int, bool) 的参数、VillagerController.ArchitectureReadHashWALK.get。</summary>
    private static readonly int HashWALK = Animator.StringToHash("WALK");
    /// <summary>中文含义：生命比例的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashHealthRatio.get。</summary>
    private static readonly int HashHealthRatio = Animator.StringToHash("HealthRatio");
    /// <summary>中文含义：搜索模式的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAnimationAdapter.SetBoolean(int, bool) 的参数、VillagerController.ArchitectureReadHashSearchMode.get。</summary>
    private static readonly int HashSearchMode = Animator.StringToHash("SearchMode");
    /// <summary>中文含义：待机混合的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashIdleBlend.get。</summary>
    private static readonly int HashIdleBlend = Animator.StringToHash("IdleBlend");
    /// <summary>中文含义：转向方向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAnimationAdapter.SetIntegerValue(int, int) 的参数、VillagerController.ArchitectureReadHashTurnDir.get。</summary>
    private static readonly int HashTurnDir = Animator.StringToHash("TurnDir");
    /// <summary>中文含义：攻击索引的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashAttackIndex.get。</summary>
    private static readonly int HashAttackIndex = Animator.StringToHash("AttackIndex");
    /// <summary>中文含义：爬行待机状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashCrawlIdleState.get。</summary>
    private static readonly int HashCrawlIdleState = Animator.StringToHash("CrawlIdle");
    /// <summary>中文含义：爬行待机路径的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashCrawlIdlePath.get。</summary>
    private static readonly int HashCrawlIdlePath = Animator.StringToHash("Base Layer.Crawl.CrawlIdle");
    /// <summary>中文含义：爬行子级状态路径的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashCrawlSubStatePath.get。</summary>
    private static readonly int HashCrawlSubStatePath = Animator.StringToHash("Crawl.CrawlIdle");
    /// <summary>中文含义：命中方向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashHitDir.get。</summary>
    private static readonly int HashHitDir = Animator.StringToHash("HitDir");
    /// <summary>中文含义：硬直方向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashStaggerDirection.get。</summary>
    private static readonly int HashStaggerDirection = Animator.StringToHash("StaggerDirection");
    /// <summary>中文含义：硬直级别的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashStaggerLevel.get。</summary>
    private static readonly int HashStaggerLevel = Animator.StringToHash("StaggerLevel");
    /// <summary>中文含义：是否爬行的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashIsCrawl.get。</summary>
    private static readonly int HashIsCrawl = Animator.StringToHash("IsCrawl");
    /// <summary>中文含义：是否可以绊倒的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashCanTripOver.get。</summary>
    private static readonly int HashCanTripOver = Animator.StringToHash("CanTripOver");
    /// <summary>中文含义：攻击的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashATK.get。</summary>
    private static readonly int HashATK = Animator.StringToHash("ATK");
    /// <summary>中文含义：命中的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashHIT.get。</summary>
    private static readonly int HashHIT = Animator.StringToHash("HIT");
    /// <summary>中文含义：死亡的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashDIE.get。</summary>
    private static readonly int HashDIE = Animator.StringToHash("DIE");
    /// <summary>中文含义：命中触发器的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashHitTrigger.get。</summary>
    private static readonly int HashHitTrigger = Animator.StringToHash("HitTrigger");
    /// <summary>中文含义：硬直触发器的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashStaggerTrigger.get。</summary>
    private static readonly int HashStaggerTrigger = Animator.StringToHash("StaggerTrigger");
    /// <summary>中文含义：攻击01状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashAttack01State.get。</summary>
    private static readonly int HashAttack01State = Animator.StringToHash("Attack01");
    /// <summary>中文含义：攻击02状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashAttack02State.get。</summary>
    private static readonly int HashAttack02State = Animator.StringToHash("Attack02");
    /// <summary>中文含义：攻击03状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.ArchitectureReadHashAttack03State.get。</summary>
    private static readonly int HashAttack03State = Animator.StringToHash("Attack03");
    /// <summary>中文含义：最大生命值。使用位置：作为 CharacterHealthModule.Initialize(float) 的参数、作为 Mathf.Clamp01(float) 的参数、EnemyAnimationAdapter.maxHealth.get 等共 6 处。</summary>
    [Header("属性")]
    public float maxHealth = 50f;
    /// <summary>中文含义：当前生命值。使用位置：作为 DamageResult.DamageResult(DamageRequest, bool, RejectionReason, float, bool) 的参数、作为 HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool) 的参数、作为 Mathf.Clamp01(float) 的参数 等共 9 处。</summary>
    public float currentHealth
    {
        get
        {
            if (healthModule != null)
            {
                return healthModule.CurrentHealth;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：攻击伤害。使用位置：EnemyCombatModule.attackDamage.get。</summary>
    public float attackDamage = 10f;
    /// <summary>中文含义：攻击冷却时间。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.IsAttackReady.get。</summary>
    public float attackCooldown = 2f;
    /// <summary>中文含义：追逐速度。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数。</summary>
    public float chaseSpeed = EnemyContext.StandingChaseMoveParameter;
    /// <summary>中文含义：巡逻速度。使用位置：EnemyActionCoordinator.patrolSpeed.get。</summary>
    public float patrolSpeed = 0.35f;
    /// <summary>中文含义：生命模块。使用位置：作为 EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule) 的参数、EnemyActionCoordinator.HealthModule.get、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List&lt;string&gt;) 等共 9 处。</summary>
    [Header("架构模块引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("生命模块")]
    [UnityEngine.InspectorName("生命模块")]
    public CharacterHealthModule healthModule;
    /// <summary>中文含义：战斗模块。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数 等共 103 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("战斗模块")]
    [UnityEngine.InspectorName("战斗模块")]
    public EnemyCombatModule combatModule;
    /// <summary>中文含义：感知模块。使用位置：EnemyActionCoordinator.PerceptionModule.get、EnemyAnimationAdapter.PerceptionModule.get、EnemyCombatModule.combatEngaged.get 等共 37 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("感知模块")]
    [UnityEngine.InspectorName("感知模块")]
    public EnemyPerceptionModule perceptionModule;
    /// <summary>中文含义：动作协调。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、作为 TraversalTokenRegistry.Register(ActionHandle) 的参数、作为 TraversalTokenRegistry.ValidateRequest(RequestContext, ActionHandle) 的参数 等共 90 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动作协调")]
    [UnityEngine.InspectorName("动作协调")]
    public EnemyActionCoordinator actionCoordinator;
    /// <summary>中文含义：动画表现。使用位置：EnemyActionCoordinator.AnimationPresenter.get、EnemyAnimationAdapter.AnimationPresenter.get、EnemyCombatModule.AnimationPresenter.get 等共 46 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动画表现")]
    [UnityEngine.InspectorName("动画表现")]
    public EnemyAnimationAdapter animationPresenter;
    /// <summary>中文含义：角色运动执行器。使用位置：作为 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool) 的参数、EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyActionCoordinator.CloseAction() 等共 27 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("运动执行")]
    [UnityEngine.InspectorName("运动执行")]
    public CharacterMotionExecutor motionExecutor;
    /// <summary>中文含义：任务敌人类型。使用位置：作为 HashSet&lt;VillagerController&gt;.RemoveWhere(Predicate&lt;VillagerController&gt;) 的参数、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)、EnemyActionCoordinator.missionEnemyType.get 等共 11 处。</summary>
    [Header("任务分类")]
    public MissionEnemyType missionEnemyType = MissionEnemyType.Ganado;
    /// <summary>中文含义：是否暗杀展示待机。仅用于入村刺杀镜头展示。启用后保持待机，不参与行为树、追击和任务敌人统计。使用位置：ChapterOneMissionManager.RemoveUnmanagedRuntimeEnemies()、EnemyAIManager.RegisterEnemy(VillagerController)、VillagerController.EnsureBehaviorTreeRunning() 等共 6 处。</summary>
    [Header("展示模式")]
    [Tooltip("仅用于入村刺杀镜头展示。启用后保持待机，不参与行为树、追击和任务敌人统计。")]
    public bool assassinationShowcaseIdle;
    /// <summary>中文含义：攻击接触留白。使用位置：作为 Gizmos.DrawWireSphere(Vector3, float) 的参数、EnemyCombatModule.attackContactPadding.get。</summary>
    [Header("攻击判定")]
    [Min(0f)]
    public float attackContactPadding = 0.05f;
    /// <summary>中文含义：攻击起始范围。使用位置：作为 Gizmos.DrawWireSphere(Vector3, float) 的参数、作为 Mathf.Max(float, float) 的参数、EnemyCombatModule.attackStartRange.get。</summary>
    [Min(0.1f)]
    public float attackStartRange = 1.2f;
    /// <summary>中文含义：攻击命中范围。使用位置：作为 IEnumerable&lt;int&gt;.Select&lt;int, KnifeEnemyAttackProfile&gt;(Func&lt;int, KnifeEnemyAttackProfile&gt;) 的参数、EnemyCombatModule.attackHitRange.get。</summary>
    [Min(0.1f)]
    public float attackHitRange = 1.25f;
    /// <summary>中文含义：战斗保持退出范围。使用位置：EnemyCombatModule.combatHoldExitRange.get。</summary>
    [Min(0.1f)]
    public float combatHoldExitRange = 1.6f;
    /// <summary>中文含义：攻击命中角度。使用位置：作为 IEnumerable&lt;int&gt;.Select&lt;int, KnifeEnemyAttackProfile&gt;(Func&lt;int, KnifeEnemyAttackProfile&gt;) 的参数、EnemyCombatModule.attackHitAngle.get。</summary>
    [Range(10f, 180f)]
    public float attackHitAngle = 100f;
    /// <summary>中文含义：爬行攻击起始角度。使用位置：EnemyCombatModule.crawlAttackStartAngle.get。</summary>
    [Range(10f, 180f)]
    public float crawlAttackStartAngle = 145f;
    /// <summary>中文含义：站立攻击后备命中归一化时间。使用位置：作为 IEnumerable&lt;int&gt;.Select&lt;int, KnifeEnemyAttackProfile&gt;(Func&lt;int, KnifeEnemyAttackProfile&gt;) 的参数、EnemyCombatModule.standingAttackFallbackHitNormalizedTime.get。</summary>
    [Range(0.05f, 0.95f)]
    public float standingAttackFallbackHitNormalizedTime = 0.55f;
    /// <summary>中文含义：是否启用小刀招架。使用位置：作为 IEnumerable&lt;int&gt;.Select&lt;int, KnifeEnemyAttackProfile&gt;(Func&lt;int, KnifeEnemyAttackProfile&gt;) 的参数、EnemyCombatModule.enableKnifeParry.get。</summary>
    [Header("小刀弹反")]
    public bool enableKnifeParry = true;
    /// <summary>中文含义：小刀局部停顿。使用位置：CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)、KnifeCombatBuilder.BindingEnemy(VillagerController)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext) 等共 5 处。</summary>
    [Header("小刀接触停顿")]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀局部停顿")]
    [UnityEngine.InspectorName("小刀局部停顿")]
    public KnifeHitStop knifeLocalHitStop;
    /// <summary>中文含义：站立战斗竖直容差。使用位置：EnemyAIManager.GetDirectPressureScore(VillagerController, Vector3)、EnemyCombatModule.standingCombatVerticalTolerance.get。</summary>
    [Header("近战路径约束")]
    [Min(0.1f)]
    public float standingCombatVerticalTolerance = 0.75f;
    /// <summary>中文含义：爬行战斗竖直容差。使用位置：EnemyCombatModule.crawlCombatVerticalTolerance.get。</summary>
    [Min(0.1f)]
    public float crawlCombatVerticalTolerance = 0.55f;
    /// <summary>中文含义：直接接近停止中距离。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float directApproachStoppingDistance = 0.9f;
    /// <summary>中文含义：稳定攻击表面坡度。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.IsStandingAttackSurfaceStable()。</summary>
    [Range(1f, 89f)]
    public float stableAttackSurfaceSlope = 28f;
    /// <summary>中文含义：是否启用电锯友方伤害。使用位置：EnemyCombatModule.enableChainsawFriendlyFire.get。</summary>
    [Header("电锯误伤")]
    public bool enableChainsawFriendlyFire = true;
    /// <summary>中文含义：电锯友方伤害范围。使用位置：EnemyCombatModule.chainsawFriendlyFireRange.get。</summary>
    [Min(0.1f)]
    public float chainsawFriendlyFireRange = 1.85f;
    /// <summary>中文含义：电锯友方伤害角度。使用位置：EnemyCombatModule.chainsawFriendlyFireAngle.get。</summary>
    [Range(10f, 180f)]
    public float chainsawFriendlyFireAngle = 125f;
    /// <summary>中文含义：电锯友方伤害伤害倍率。使用位置：EnemyCombatModule.chainsawFriendlyFireDamageMultiplier.get。</summary>
    [Min(0f)]
    public float chainsawFriendlyFireDamageMultiplier = 0.8f;
    /// <summary>中文含义：电锯友伤最多命中目标数。使用位置：EnemyCombatModule.chainsawFriendlyFireMaxTargets.get。</summary>
    [Min(0)]
    public int chainsawFriendlyFireMaxTargets = 3;
    /// <summary>中文含义：电锯友方伤害高度容差。使用位置：EnemyCombatModule.chainsawFriendlyFireHeightTolerance.get。</summary>
    [Min(0f)]
    public float chainsawFriendlyFireHeightTolerance = 1.45f;
    /// <summary>中文含义：死亡电锯危险持续时间。使用位置：EnemyActionCoordinator.deadChainsawDangerDuration.get、EnemyCombatModule.deadChainsawDangerDuration.get。</summary>
    [Min(0f)]
    public float deadChainsawDangerDuration = 2f;
    /// <summary>中文含义：死亡电锯危险半径。使用位置：EnemyCombatModule.deadChainsawDangerRadius.get。</summary>
    [Min(0.1f)]
    public float deadChainsawDangerRadius = 1.25f;
    /// <summary>中文含义：死亡电锯伤害间隔。使用位置：EnemyCombatModule.deadChainsawDamageInterval.get。</summary>
    [Min(0.05f)]
    public float deadChainsawDamageInterval = 0.35f;
    /// <summary>中文含义：死亡电锯伤害倍率。使用位置：EnemyCombatModule.deadChainsawDamageMultiplier.get。</summary>
    [Min(0f)]
    public float deadChainsawDamageMultiplier = 0.45f;
    /// <summary>中文含义：是否启用敌人友方伤害。使用位置：EnemyCombatModule.enableEnemyFriendlyFire.get。</summary>
    [Header("敌人误伤")]
    public bool enableEnemyFriendlyFire = true;
    /// <summary>中文含义：敌人友方伤害范围。使用位置：EnemyCombatModule.enemyFriendlyFireRange.get。</summary>
    [Min(0.1f)]
    public float enemyFriendlyFireRange = 1.25f;
    /// <summary>中文含义：敌人友方伤害角度。使用位置：EnemyCombatModule.enemyFriendlyFireAngle.get。</summary>
    [Range(10f, 180f)]
    public float enemyFriendlyFireAngle = 85f;
    /// <summary>中文含义：敌人友方伤害伤害倍率。使用位置：EnemyCombatModule.enemyFriendlyFireDamageMultiplier.get。</summary>
    [Min(0f)]
    public float enemyFriendlyFireDamageMultiplier = 0.35f;
    /// <summary>中文含义：单次敌人友伤最多命中的目标数。使用位置：EnemyCombatModule.enemyFriendlyFireMaxTargets.get。</summary>
    [Min(0)]
    public int enemyFriendlyFireMaxTargets = 1;
    /// <summary>中文含义：敌人友方伤害高度容差。使用位置：EnemyCombatModule.enemyFriendlyFireHeightTolerance.get。</summary>
    [Min(0f)]
    public float enemyFriendlyFireHeightTolerance = 1.1f;
    /// <summary>中文含义：站立攻击动画数量。使用位置：作为 Mathf.Min(int, int) 的参数、EnemyCombatModule.standingAttackAnimationCount.get。</summary>
    [Header("动画配置")]
    [Min(1)]
    public int standingAttackAnimationCount = 3;
    /// <summary>中文含义：是否支持爬行。使用位置：EnemyCombatModule.supportsCrawl.get、VillagerController.CanEvaluateCrawlCombatAction.get。</summary>
    public bool supportsCrawl = true;
    /// <summary>中文含义：是否支持绊倒。使用位置：EnemyCombatModule.supportsTripOver.get。</summary>
    public bool supportsTripOver = true;
    /// <summary>中文含义：巡逻半径。使用位置：作为 Gizmos.DrawWireSphere(Vector3, float) 的参数、SelectPatrolPoint.OnUpdate()。</summary>
    [Header("巡逻")]
    public float patrolRadius = 10f;
    /// <summary>中文含义：静止巡逻最小持续时间。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float stationaryPatrolMinDuration = 1.5f;
    /// <summary>中文含义：静止巡逻最大持续时间。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float stationaryPatrolMaxDuration = 3f;
    /// <summary>中文含义：静止巡逻最大转向角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(0f, 180f)]
    public float stationaryPatrolMaxTurnAngle = 75f;
    /// <summary>中文含义：静止巡逻转向速度。使用位置：EnemyActionCoordinator.stationaryPatrolTurnSpeed.get。</summary>
    [Min(1f)]
    public float stationaryPatrolTurnSpeed = 55f;
    /// <summary>中文含义：敌人语音配置资产。使用位置：EnemyAnimationAdapter.voiceProfile.get。</summary>
    [Header("语音")]
    [Tooltip("敌人语音配置资产")]
    public ZombieAudioProfile voiceProfile;
    /// <summary>中文含义：语音播放最小间隔（秒），防止多个敌人同时持续播放。使用位置：EnemyAnimationAdapter.voiceMinInterval.get。</summary>
    [Tooltip("语音播放最小间隔（秒），防止多个敌人同时持续播放")]
    public float voiceMinInterval = 1.5f;
    /// <summary>中文含义：是否使用根运动。使用位置：作为 EnemyAnimationAdapter.SetRootMotion(bool) 的参数、EnemyAnimationAdapter.InitializeAnimationParameter()、VillagerQTEParticipant.ForceExitQTEForDeath(int)。</summary>
    [Header("根运动")]
    public bool useRootMotion = true;
    /// <summary>中文含义：地面探测高度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数 等共 7 处。</summary>
    [Header("地面贴合")]
    [Min(0.1f)]
    public float groundProbeHeight = 0.6f;
    /// <summary>中文含义：地面探测距离。使用位置：作为 Mathf.Max(float, float) 的参数、作为 VillagerController.SnapToGround(float, float, bool) 的参数。</summary>
    [Min(0.1f)]
    public float groundProbeDistance = 2f;
    /// <summary>中文含义：地面偏移量。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 等共 8 处。</summary>
    public float groundOffset;
    /// <summary>中文含义：最大地面步进向上。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 等共 6 处。</summary>
    [Min(0.05f)]
    public float maximumGroundStepUp = 0.45f;
    /// <summary>中文含义：最大地面掉落。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。</summary>
    [Min(0.05f)]
    public float maximumGroundDrop = 0.8f;
    /// <summary>中文含义：最大地面坡度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 5 处。</summary>
    [Range(1f, 89f)]
    public float maximumGroundSlope = 50f;
    /// <summary>中文含义：移动碰撞皮肤。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数 等共 6 处。</summary>
    [Min(0.001f)]
    public float movementCollisionSkin = 0.03f;
    /// <summary>中文含义：楼梯视觉表现步进高度。导航斜桥没有命中真实踏面时，按此高度离散贴合台阶，避免沿斜面滑行。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("导航斜桥没有命中真实踏面时，按此高度离散贴合台阶，避免沿斜面滑行")]
    [Min(0.05f)]
    public float stairVisualStepHeight = 0.18f;
    /// <summary>中文含义：是否启用局部避障。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。</summary>
    [Header("局部避障")]
    public bool enableLocalAvoidance = true;
    /// <summary>中文含义：敌人避障半径。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。</summary>
    [Min(0.1f)]
    public float enemyAvoidanceRadius = 1.1f;
    /// <summary>中文含义：敌人避障强度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float enemyAvoidanceStrength = 1.25f;
    /// <summary>中文含义：敌人避障侧边偏置。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float enemyAvoidanceSideBias = 0.35f;
    /// <summary>中文含义：敌人避障侧边保持持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float enemyAvoidanceSideHoldDuration = 0.8f;
    /// <summary>中文含义：敌人避障最大权重。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(0f, 0.45f)]
    public float enemyAvoidanceMaximumWeight = 0.32f;
    /// <summary>中文含义：敌人避障最大角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(5f, 60f)]
    public float enemyAvoidanceMaximumAngle = 32f;
    /// <summary>中文含义：阻挡重新寻路延迟。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</summary>
    [Min(0.1f)]
    public float blockedRepathDelay = 0.65f;
    /// <summary>中文含义：阻挡重新寻路间隔。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</summary>
    [Min(0.1f)]
    public float blockedRepathInterval = 0.8f;
    /// <summary>中文含义：重叠恢复距离。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Vector3.ClampMagnitude(Vector3, float) 的参数。</summary>
    [Min(0.01f)]
    public float overlapRecoveryDistance = 0.08f;
    /// <summary>中文含义：追逐进度超时。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("追击停滞恢复")]
    [Min(0.2f)]
    public float chaseProgressTimeout = 1f;
    /// <summary>中文含义：追逐进度距离。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float chaseProgressDistance = 0.06f;
    /// <summary>中文含义：追逐恢复间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float chaseRecoveryInterval = 0.65f;
    /// <summary>中文含义：行为树在路径失败状态下重新解析可达追击点的间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("行为树在路径失败状态下重新解析可达追击点的间隔。")]
    [Min(0.2f)]
    public float chasePathRecoveryInterval = 0.75f;
    /// <summary>中文含义：路径抵达点距里昂超过此值时，不能视为已完成追击。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("追击终点校验")]
    [Tooltip("路径抵达点距里昂超过此值时，不能视为已完成追击。")]
    [Min(0.1f)]
    public float incompletePursuitArrivalDistance = 1.7f;
    /// <summary>中文含义：未完成追击时的重算间隔，避免每帧提交相同路径。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("未完成追击时的重算间隔，避免每帧提交相同路径。")]
    [Min(0.2f)]
    public float incompletePursuitRepathInterval = 0.75f;
    /// <summary>中文含义：视野范围。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyCombatModule.sightRange.get、EnemyPerceptionModule.sightRange.get。</summary>
    [Header("行为设置")]
    public float sightRange = 30f;
    /// <summary>中文含义：爬行持续时间。使用位置：ExecuteCrawl.OnStart()。</summary>
    public float crawlDuration = 8f;
    /// <summary>中文含义：爬行转向速度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float crawlTurnSpeed = 7f;
    /// <summary>中文含义：爬行方向转向速率。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(1f)]
    public float crawlDirectionTurnRate = 140f;
    /// <summary>中文含义：动作安全性超时。使用位置：ExecuteAttack.OnStart()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawStart.OnStart() 等共 7 处。</summary>
    public float actionSafetyTimeout = 4.5f;
    /// <summary>中文含义：起身稳定化持续时间。使用位置：EnemyCombatModule.standUpStabilizationDuration.get。</summary>
    [Min(0f)]
    public float standUpStabilizationDuration = 0.18f;
    /// <summary>中文含义：是否启用智能门开口。使用位置：VillagerController.TryOpenDoorOnPath(Vector3)。</summary>
    [Header("敌人开门")]
    public bool enableAIDoorOpening = true;
    /// <summary>中文含义：门打开探测距离。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryOpenDoorOnPath(Vector3)。</summary>
    [Min(0.1f)]
    public float doorOpenProbeDistance = 1.15f;
    /// <summary>中文含义：门打开探测半径。使用位置：作为 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform) 的参数、作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float doorOpenProbeRadius = 1.35f;
    /// <summary>中文含义：门打开探测角度。使用位置：作为 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform) 的参数。</summary>
    [Range(1f, 180f)]
    public float doorOpenProbeAngle = 115f;
    /// <summary>中文含义：门打开探测间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.05f)]
    public float doorOpenProbeInterval = 0.35f;
    /// <summary>中文含义：队首越过门洞平面后，至少离开此深度才释放下一名敌人。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("门洞通行")]
    [Tooltip("队首越过门洞平面后，至少离开此深度才释放下一名敌人。")]
    [Min(0.2f)]
    public float pursuitIngressReleaseDepth = 0.42f;
    /// <summary>中文含义：视野角度。使用位置：EnemyPerceptionModule.sightAngle.get。</summary>
    [Header("感知与警觉")]
    [Range(30f, 180f)]
    public float sightAngle = 110f;
    /// <summary>中文含义：关闭警觉范围。使用位置：EnemyPerceptionModule.closeAwarenessRange.get。</summary>
    [Min(0.1f)]
    public float closeAwarenessRange = 1.25f;
    /// <summary>中文含义：警觉构建持续时间。</summary>
    [Min(0.1f)]
    public float awarenessBuildDuration = 0.8f;
    /// <summary>中文含义：警觉衰减持续时间。使用位置：EnemyPerceptionModule.awarenessDecayDuration.get。</summary>
    [Min(0.1f)]
    public float awarenessDecayDuration = 1.5f;
    /// <summary>中文含义：警觉记忆持续时间。使用位置：EnemyCombatModule.awarenessMemoryDuration.get。</summary>
    [Min(0f)]
    public float awarenessMemoryDuration = 5f;
    /// <summary>中文含义：枪声警觉。使用位置：EnemyPerceptionModule.gunshotAwareness.get。</summary>
    [Range(0f, 1f)]
    public float gunshotAwareness = 0.45f;
    /// <summary>中文含义：调查搜索持续时间。使用位置：EnemyPerceptionModule.investigationSearchDuration.get。</summary>
    [Min(0.1f)]
    public float investigationSearchDuration = 3f;
    /// <summary>中文含义：调查抵达距离。使用位置：EnemyPerceptionModule.investigationArrivalDistance.get。</summary>
    [Min(0.1f)]
    public float investigationArrivalDistance = 0.8f;
    /// <summary>中文含义：警戒转向速度。使用位置：EnemyActionCoordinator.alertTurnSpeed.get、EnemyPerceptionModule.alertTurnSpeed.get。</summary>
    [Min(0.1f)]
    public float alertTurnSpeed = 4f;
    /// <summary>中文含义：追逐解除接触范围。使用位置：VillagerController.IsPlayerWithinChaseDisengageRange(LeonController)。</summary>
    [Header("战斗锁定")]
    [Min(1f)]
    public float chaseDisengageRange = 35f;
    /// <summary>中文含义：解除接触确认持续时间。</summary>
    [Min(0f)]
    public float disengageConfirmationDuration = 0.6f;
    /// <summary>中文含义：追逐起始移动速度。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数、EnemyActionCoordinator.chaseStartMoveSpeed.get。</summary>
    [Header("追击过渡")]
    [Range(0f, 1f)]
    public float chaseStartMoveSpeed = 0.3f;
    /// <summary>中文含义：追逐加速度持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    [Min(0.1f)]
    public float chaseAccelerationDuration = 1.4f;
    /// <summary>中文含义：移动混合速度。使用位置：EnemyAnimationAdapter.movementBlendSpeed.get。</summary>
    [Min(0.1f)]
    public float movementBlendSpeed = 4f;
    /// <summary>中文含义：硬直恢复移动延迟。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    [Min(0f)]
    public float staggerRecoveryMovementDelay = 0.1f;
    /// <summary>中文含义：硬直恢复加速度持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float staggerRecoveryAccelerationDuration = 0.35f;
    /// <summary>中文含义：移动方向混合时间。使用位置：EnemyAnimationAdapter.movementDirectionBlendTime.get、SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool)。</summary>
    [Min(0f)]
    public float movementDirectionBlendTime = .1f;
    /// <summary>中文含义：站立朝向写入帧。使用位置：VillagerController.RotateTowardsDirection(Vector3, float)。</summary>
    private int standingFacingWriteFrame = -1;
    /// <summary>中文含义：是否站立直接追击用于帧。使用位置：VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.ResolveStandingPathDirectionForCurrentFrame() 等共 4 处。</summary>
    private bool standingDirectPursuitForFrame;
    /// <summary>中文含义：站立方向转向速率。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(1f)]
    public float standingDirectionTurnRate = 300f;
    /// <summary>中文含义：站立方向死亡区。</summary>
    [Range(0f, 20f)]
    public float standingDirectionDeadZone = 4f;
    /// <summary>中文含义：局部避障转向速率。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(1f)]
    public float localAvoidanceTurnRate = 480f;
    /// <summary>中文含义：移动朝向停止角度。最终移动方向与身体前方超过该角度时降低平移速度，优先完成转身。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    [Tooltip("最终移动方向与身体前方超过该角度时降低平移速度，优先完成转身。")]
    [Range(5f, 89f)]
    public float movementFacingStopAngle = 35f;
    /// <summary>中文含义：最终移动方向与身体前方进入该角度内后恢复完整追击速度。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    [Tooltip("最终移动方向与身体前方进入该角度内后恢复完整追击速度。")]
    [Range(0f, 89f)]
    public float movementFacingFullSpeedAngle = 20f;
    /// <summary>中文含义：进入该距离后优先直接追击玩家，不再停在战术环绕点等待。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("进入该距离后优先直接追击玩家，不再停在战术环绕点等待")]
    [Min(1f)]
    public float closeDirectPursuitRange = 8f;
    /// <summary>中文含义：实际移动速度响应。真实水平速度的平滑响应速度，避免单帧碰撞变化直接传入脚步动画。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAnimationAdapter.actualMovementSpeedResponse.get。</summary>
    [Header("移动动画同步")]
    [Tooltip("真实水平速度的平滑响应速度，避免单帧碰撞变化直接传入脚步动画。")]
    [Min(1f)]
    public float actualMovementSpeedResponse = 12f;
    /// <summary>中文含义：移动动画起始宽限持续时间。开始移动后的短暂预启动时间，避免首帧已有位移但脚步仍停在待机。使用位置：EnemyAnimationAdapter.movementAnimationStartGraceDuration.get。</summary>
    [Tooltip("开始移动后的短暂预启动时间，避免首帧已有位移但脚步仍停在待机。")]
    [Min(0f)]
    public float movementAnimationStartGraceDuration = 0.12f;
    /// <summary>中文含义：低于该真实速度时视为没有产生有效位移，脚步会平滑停止。使用位置：EnemyAnimationAdapter.locomotionStopSpeed.get。</summary>
    [Tooltip("低于该真实速度时视为没有产生有效位移，脚步会平滑停止。")]
    [Min(0f)]
    public float locomotionStopSpeed = 0.03f;
    /// <summary>中文含义：移动行为停止确认持续时间。真实位移持续不足达到该时间后才停止脚步，避免碰撞隔帧造成动画反复启停。使用位置：EnemyAnimationAdapter.locomotionStopConfirmationDuration.get。</summary>
    [Tooltip("真实位移持续不足达到该时间后才停止脚步，避免碰撞隔帧造成动画反复启停。")]
    [Min(0.05f)]
    public float locomotionStopConfirmationDuration = 0.22f;
    /// <summary>中文含义：停止后必须持续产生有效位移达到该时间，才重新进入走路动画。使用位置：EnemyAnimationAdapter.locomotionStartConfirmationDuration.get。</summary>
    [Tooltip("停止后必须持续产生有效位移达到该时间，才重新进入走路动画。")]
    [Min(0.05f)]
    public float locomotionStartConfirmationDuration = 0.12f;
    /// <summary>中文含义：移动行为重新开始速度。已停止的脚步重新启动所需的真实水平速度，必须高于停止阈值形成迟滞。使用位置：EnemyAnimationAdapter.locomotionRestartSpeed.get。</summary>
    [Tooltip("已停止的脚步重新启动所需的真实水平速度，必须高于停止阈值形成迟滞。")]
    [Min(0f)]
    public float locomotionRestartSpeed = 0.08f;
    /// <summary>中文含义：是否使用站立动画速度用于路径。站立追击是否使用动画根位移采样决定代码推进速度。关闭后始终使用寻路速度，避免动画、碰撞与位移互相等待。使用位置：EnemyAnimationAdapter.useStandingAnimationSpeedForPath.get、SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool)、VillagerController.ResolveStandingRootMotionSpeed(float)。</summary>
    [Tooltip("站立追击是否使用动画根位移采样决定代码推进速度。关闭后始终使用寻路速度，避免动画、碰撞与位移互相等待。")]
    public bool useStandingAnimationSpeedForPath;
    internal bool UsesNativeStandingStride => useStandingAnimationSpeedForPath && anim != null && !anim.isHuman;
    internal float NativeGaitDistanceToStop
    {
        get
        {
            float remaining = float.PositiveInfinity;
            if (agent != null && agent.hasPath)
            {
                remaining = Mathf.Max(0f, agent.remainingDistance - Mathf.Min(0.35f, agent.waypointReachedRadius));
            }
            if (standingDirectPursuitForFrame && CurrentState == VillagerState.Chase)
            {
                remaining = Mathf.Min(remaining, Mathf.Max(0f, PlayerDistance - combatModule.StandingApproachRange));
            }
            return remaining;
        }
    }
    /// <summary>中文含义：保持站立移动行为期间是否受阻。存在明确追击位移意图时保持行走动画，避免碰撞的短暂阻塞反复切换走路与待机。使用位置：EnemyAnimationAdapter.keepStandingLocomotionWhileBlocked.get。</summary>
    [Tooltip("存在明确追击位移意图时保持行走动画，避免碰撞的短暂阻塞反复切换走路与待机。")]
    public bool keepStandingLocomotionWhileBlocked = true;
    /// <summary>中文含义：腿部倒地伤害。使用位置：EnemyCombatModule.legCollapseDamage.get。</summary>
    [Header("腿部破坏")]
    [Min(0f)]
    public float legCollapseDamage = 36f;
    /// <summary>中文含义：永久爬行腿部伤害。</summary>
    [Min(0f)]
    public float permanentCrawlLegDamage = 54f;
    /// <summary>中文含义：永久爬行生命比例。</summary>
    [Range(0.1f, 1f)]
    public float permanentCrawlHealthRatio = 0.55f;
    /// <summary>中文含义：硬直阈值。使用位置：EnemyCombatModule.staggerThreshold.get。</summary>
    [Header("受击韧性")]
    [Min(0.1f)]
    public float staggerThreshold = 20f;
    /// <summary>中文含义：受击反应免疫。使用位置：EnemyCombatModule.hitReactionImmunity.get。</summary>
    [Min(0f)]
    public float hitReactionImmunity = 0.4f;
    /// <summary>中文含义：韧性衰减延迟。使用位置：EnemyCombatModule.AdvanceStaggerRestore()。</summary>
    [Min(0f)]
    public float poiseDecayDelay = 1.5f;
    /// <summary>中文含义：韧性衰减每次第二。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float poiseDecayPerSecond = 8f;
    /// <summary>中文含义：是否启用暴击硬直。使用位置：EnemyCombatModule.enableCriticalStagger.get。</summary>
    [Header("枪械暴击硬直")]
    public bool enableCriticalStagger = true;
    /// <summary>中文含义：头部暴击硬直概率。使用位置：EnemyCombatModule.headCriticalStaggerChance.get。</summary>
    [Range(0f, 1f)]
    public float headCriticalStaggerChance = 0.2f;
    /// <summary>中文含义：身体暴击硬直概率。使用位置：EnemyCombatModule.bodyCriticalStaggerChance.get。</summary>
    [Range(0f, 1f)]
    public float bodyCriticalStaggerChance = 0.06f;
    /// <summary>中文含义：暴击硬直伤害楼层。使用位置：EnemyCombatModule.criticalStaggerDamageFloor.get。</summary>
    [Min(0f)]
    public float criticalStaggerDamageFloor = 12f;
    /// <summary>中文含义：暴击硬直冷却。使用位置：EnemyCombatModule.criticalStaggerCooldown.get。</summary>
    [Min(0f)]
    public float criticalStaggerCooldown = 0.35f;
    /// <summary>中文含义：是否暴击硬直开放体术。使用位置：EnemyCombatModule.criticalStaggerOpensBodyTechnique.get。</summary>
    public bool criticalStaggerOpensBodyTechnique = true;
    /// <summary>中文含义：常规体术窗口。使用位置：EnemyCombatModule.normalBodyTechniqueWindow.get。</summary>
    [Header("体术硬直窗口")]
    [Min(0.2f)]
    public float normalBodyTechniqueWindow = 1.35f;
    /// <summary>中文含义：精英体术窗口。使用位置：EnemyCombatModule.eliteBodyTechniqueWindow.get。</summary>
    [Min(0.2f)]
    public float eliteBodyTechniqueWindow = 1.05f;
    /// <summary>中文含义：是否使用电锯敌人命中韧性。使用位置：EnemyCombatModule.useSawmanHitToughness.get。</summary>
    [Header("电锯人受击抗性")]
    public bool useSawmanHitToughness = true;
    /// <summary>中文含义：电锯敌人受击反应阈值倍率。使用位置：EnemyCombatModule.sawmanHitReactionThresholdMultiplier.get。</summary>
    [Min(1f)]
    public float sawmanHitReactionThresholdMultiplier = 1.5f;
    /// <summary>中文含义：电锯敌人韧性伤害倍率。使用位置：EnemyCombatModule.sawmanPoiseDamageMultiplier.get。</summary>
    [Range(0f, 1f)]
    public float sawmanPoiseDamageMultiplier = 0.75f;
    /// <summary>中文含义：电锯敌人头部暴击硬直概率倍率。使用位置：EnemyCombatModule.sawmanHeadCriticalStaggerChanceMultiplier.get。</summary>
    [Range(0f, 1f)]
    public float sawmanHeadCriticalStaggerChanceMultiplier = 0.75f;
    /// <summary>中文含义：电锯敌人身体暴击硬直概率倍率。使用位置：EnemyCombatModule.sawmanBodyCriticalStaggerChanceMultiplier.get。</summary>
    [Range(0f, 1f)]
    public float sawmanBodyCriticalStaggerChanceMultiplier = 0.4f;
    /// <summary>中文含义：电锯敌人暴击伤害楼层额外加成。使用位置：EnemyCombatModule.sawmanCriticalDamageFloorBonus.get。</summary>
    [Min(0f)]
    public float sawmanCriticalDamageFloorBonus = 8f;
    /// <summary>中文含义：是否生效。使用位置：DoorwayTraversalOccupancy.Valid(VillagerController, int)、EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyAIManager.SelectSurroundSlot(VillagerController, Vector3, float, int, bool, out int) 等共 12 处。</summary>
    [HideInInspector]
    public bool IsActive = true;
    /// <summary>中文含义：是否启用移动诊断。开启后仅记录状态变化、关键函数进入退出及每0.2秒采样，关闭时无任何日志。使用位置：EnemyActionCoordinator.Commit(ActionRequest, bool)、EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、EnemyTaskBase.DiagTaskEnd() 等共 9 处。</summary>
    [Header("移动诊断")]
    [Tooltip("开启后仅记录状态变化、关键函数进入退出及每0.2秒采样，关闭时无任何日志")]
    public bool enableMovementDiagnostic;
    /// <summary>中文含义：诊断敌人数量。使用位置：EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)、EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.Number(VillagerController) 等共 7 处。</summary>
    [Header("时间轴测试身份")]
    [HideInInspector]
    public int diagnosticEnemyNumber;
    /// <summary>中文含义：诊断波次。使用位置：作为 EnemyCombatTimeline.Register(VillagerController, int) 的参数、EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 4 处。</summary>
    [HideInInspector]
    public int diagnosticWave;
    /// <summary>中文含义：时间轴动画是否已初始化。使用位置：VillagerController.RecordTimelineAnimationChanges()。</summary>
    private bool timelineAnimationInitialized;
    /// <summary>中文含义：时间轴基础动画。使用位置：VillagerController.RecordTimelineAnimationChanges()。</summary>
    private int timelineBaseAnimation;
    /// <summary>中文含义：时间轴下一次动画。使用位置：VillagerController.RecordTimelineAnimationChanges()。</summary>
    private int timelineNextAnimation;
    /// <summary>中文含义：是否时间轴动画过渡。使用位置：VillagerController.RecordTimelineAnimationChanges()。</summary>
    private bool timelineAnimationTransition;
    /// <summary>中文含义：人群绕过是否生效。使用位置：VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.IsCrowdBypassing.get、VillagerController.UpdateCrowdBypass()。</summary>
    private bool crowdBypassActive;
    /// <summary>中文含义：人群绕过目的地。使用位置：作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.GetCurrentCombatTarget() 等共 4 处。</summary>
    private Vector3 crowdBypassDestination;
    /// <summary>中文含义：人群绕过玩家锚点。使用位置：作为 Mathf.Abs(float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.BeginTemporaryAvoidance(Vector3)。</summary>
    private Vector3 crowdBypassPlayerAnchor;
    /// <summary>中文含义：人群绕过到期时间。使用位置：VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.IsCrowdBypassing.get、VillagerController.UpdateCrowdBypass()。</summary>
    private float crowdBypassExpiresAt;
    /// <summary>中文含义：下一次人群绕过时间。使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。</summary>
    private float nextCrowdBypassTime;
    /// <summary>中文含义：上一次移动阻挡者敌人。使用位置：作为 VillagerController.TryStartCrowdBypass(VillagerController) 的参数、VillagerController.CollectCombatTimelineSnapshot()、VillagerController.ResetBlockedMovement() 等共 6 处。</summary>
    private VillagerController lastMovementBlockerEnemy;
    /// <summary>中文含义：是否具有已掉落战利品。</summary>
    private bool hasDroppedLoot
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadHasDroppedLoot;
            }
            else
            {
                return default;
            }
        }
    }

    // 只保护死亡执行流程的一次性提交；死亡事实由生命模块持有。
    /// <summary>中文含义：死亡执行是否具有开始。</summary>
    private bool DeathExecuteHasBegin
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadDeathExecuteHasBegin;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的命中。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CanBeAssassinatedBy(LeonController)、VillagerController.CanEvaluateCrawlCombatAction.get 等共 9 处。</summary>
    private bool pendingHit
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingHit;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的爬行。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CanBeAssassinatedBy(LeonController)、VillagerController.CanEvaluateCrawlCombatAction.get 等共 9 处。</summary>
    private bool pendingCrawl
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingCrawl;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的起身。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CanBeAssassinatedBy(LeonController)、VillagerController.CanEvaluateCrawlCombatAction.get 等共 9 处。</summary>
    private bool pendingStandUp
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingStandUp;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的绊倒。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CanBeAssassinatedBy(LeonController)。</summary>
    private bool pendingTripOver
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingTripOver;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否攻击结束。使用位置：VillagerController.AttackFinished.get、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    private bool attackFinished
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAttackFinished;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否命中结束。使用位置：VillagerController.HitFinished.get。</summary>
    private bool hitFinished
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadHitFinished;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否起身结束。使用位置：VillagerController.StandUpFinished.get。</summary>
    private bool standUpFinished
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadStandUpFinished;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：待处理命中方向。</summary>
    private int pendingHitDirection
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingHitDirection;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：待处理硬直方向。</summary>
    private int pendingStaggerDirection
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingStaggerDirection;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：待处理硬直级别。</summary>
    private int pendingStaggerLevel
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingStaggerLevel;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的站立硬直。</summary>
    private bool pendingStandingStagger
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingStandingStagger;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否存在待处理的站立身体硬直体术。</summary>
    private bool pendingStandingStaggerBodyTechnique
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPendingStandingStaggerBodyTechnique;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否当前站立身体硬直体术。使用位置：VillagerController.CanOpenBodyTechniqueFromCurrentStagger.get。</summary>
    private bool currentStandingStaggerBodyTechnique
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadCurrentStandingStaggerBodyTechnique;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：体术窗口结束时间。使用位置：VillagerController.CanOpenBodyTechniqueFromCurrentStagger.get。</summary>
    private float bodyTechniqueWindowEndTime
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadBodyTechniqueWindowEndTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否延后的站立命中完成。</summary>
    private bool deferredStandingHitCompletion
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadDeferredStandingHitCompletion;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：上一次攻击时间。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.IsAttackReady.get。</summary>
    private float lastAttackTime
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadLastAttackTime;
            }
            else
            {
                return float.NegativeInfinity;
            }
        }
    }

    /// <summary>中文含义：上一次语音时间。</summary>
    private float lastVoiceTime
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadLastVoiceTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否绕过语音冷却下一次调用。下一次 PlayVoice 调用是否绕过全局语音间隔（体术命中反馈专用）。</summary>
    private bool bypassVoiceCooldownNextCall
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadBypassVoiceCooldownNextCall;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：当前语音句柄。</summary>
    private AudioPlayHandle currentVoiceHandle
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadCurrentVoiceHandle;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：累计腿部伤害。</summary>
    private float accumulatedLegDamage
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAccumulatedLegDamage;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：累计韧性伤害。</summary>
    private float accumulatedPoiseDamage
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAccumulatedPoiseDamage;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：上一次韧性伤害时间。</summary>
    private float lastPoiseDamageTime
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadLastPoiseDamageTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：受击反应免疫直到。</summary>
    private float hitReactionImmuneUntil
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadHitReactionImmuneUntil;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：下一次暴击硬直允许的时间。</summary>
    private float nextCriticalStaggerAllowedTime
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadNextCriticalStaggerAllowedTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否永久爬行。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.IsPermanentlyCrawling.get。</summary>
    private bool permanentCrawl
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadPermanentCrawl;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：警觉。使用位置：VillagerController.Awareness.get、VillagerController.HasAnyPlayerAwareness.get。</summary>
    private float awareness
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadAwareness;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否完全察觉所属玩家。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CanBeAssassinatedBy(LeonController)、VillagerController.CanEvaluateCrawlCombatAction.get 等共 8 处。</summary>
    private bool fullyAwareOfPlayer
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadFullyAwareOfPlayer;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否战斗已接战。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.CanBeAssassinatedBy(LeonController) 等共 9 处。</summary>
    private bool combatEngaged
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadCombatEngaged;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：外部解除接触自从。</summary>
    private float outsideDisengageSince
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadOutsideDisengageSince;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：玩家是否可见。</summary>
    private bool playerVisible
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadPlayerVisible;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：上一次已见时间。</summary>
    private float lastSeenTime
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadLastSeenTime;
            }
            else
            {
                return float.NegativeInfinity;
            }
        }
    }

    /// <summary>中文含义：上一次已知玩家位置。使用位置：VillagerController.GetCurrentCombatTarget()。</summary>
    private Vector3 lastKnownPlayerPosition
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadLastKnownPlayerPosition;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否具有调查目标。</summary>
    private bool hasInvestigationTarget
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadHasInvestigationTarget;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否调查搜索中。</summary>
    private bool investigationSearching
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadInvestigationSearching;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：调查搜索结束时间。</summary>
    private float investigationSearchEndTime
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadInvestigationSearchEndTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：下一次调查转向时间。</summary>
    private float nextInvestigationTurnTime
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadNextInvestigationTurnTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：调查观察目标。</summary>
    private Vector3 investigationLookTarget
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadInvestigationLookTarget;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：可见性评估帧。</summary>
    private int visibilityEvaluationFrame
    {
        get
        {
            if (perceptionModule != null)
            {
                return perceptionModule.ReadVisibilityEvaluationFrame;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：追逐开始时间。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    private float chaseStartedTime
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadChaseStartedTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：硬直恢复开始时间。</summary>
    private float staggerRecoveryStartedTime
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadStaggerRecoveryStartedTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：可感知状态。使用位置：作为 StringBuilder.Append(string) 的参数、作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.CurrentState.get。</summary>
    private VillagerState observableState
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadObservableState;
            }
            else
            {
                return VillagerState.Idle;
            }
        }
    }

    /// <summary>中文含义：巡逻中心。使用位置：VillagerController.PatrolCenter.get。</summary>
    private Vector3 patrolCenter
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadPatrolCenter;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：巡逻目标。使用位置：VillagerController.PatrolTarget.get。</summary>
    private Vector3 patrolTarget
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadPatrolTarget;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否具有巡逻目标。使用位置：VillagerController.HasPatrolTarget.get。</summary>
    private bool hasPatrolTarget
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadHasPatrolTarget;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：攻击命中遮罩。</summary>
    private int attackHitMask
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAttackHitMask;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：站立招架窗口是否打开。</summary>
    private bool standingParryWindowOpen
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadStandingParryWindowOpen;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：战斗保持是否生效。使用位置：VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.CollectCombatTimelineSnapshot()、VillagerController.ShouldHoldCombatPosition.get。</summary>
    private bool combatHoldActive
    {
        get
        {
            if (actionCoordinator != null)
            {
                return actionCoordinator.ReadCombatHoldActive;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否生命比例参数可用。</summary>
    private bool healthRatioParameterAvailable
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadHealthRatioParameterAvailable;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：是否硬直触发器参数可用。</summary>
    private bool staggerTriggerParameterAvailable
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadStaggerTriggerParameterAvailable;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：配对动作地面是否锁定。使用位置：VillagerController.BeginQTEGroundLock()、VillagerController.EndQTEGroundLock()、VillagerController.OnAnimatorMove()。</summary>
    private bool qteGroundLocked;
    /// <summary>中文含义：配对动作地面纵向。使用位置：VillagerController.BeginQTEGroundLock()。</summary>
    private float qteGroundY;
    /// <summary>中文含义：强制下一次命中方向。</summary>
    private int forcedNextHitDirection
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadForcedNextHitDirection;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：缓存玩家。使用位置：VillagerController.ArchitectureReadCachedPlayer.get、VillagerController.ResolvePlayer()。</summary>
    private LeonController cachedPlayer;
    /// <summary>中文含义：下一次玩家恢复时间。使用位置：VillagerController.ResolvePlayer()。</summary>
    private float nextPlayerRecoveryTime;
    /// <summary>中文含义：下一次管理器登记时间。使用位置：VillagerController.RecoverManagerRegistration()。</summary>
    private float nextManagerRegistrationTime;
    /// <summary>中文含义：下一次门打开探测时间。使用位置：VillagerController.TryOpenDoorOnPath(Vector3)。</summary>
    private float nextDoorOpenProbeTime;
    /// <summary>中文含义：外部击退协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、VillagerController.BeginExternalKnockback(Vector3, float, float)、VillagerController.DriveExternalKnockback(Vector3, float, float)。</summary>
    private Coroutine externalKnockbackRoutine;
    /// <summary>中文含义：上一次稳定站立移动方向。使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.ArchitectureReadLastStableStandingMoveDirection.get、VillagerController.CollectCombatTimelineSnapshot() 等共 8 处。</summary>
    private Vector3 lastStableStandingMoveDirection;
    /// <summary>中文含义：上一次稳定避障方向。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、VillagerController.BeginTemporaryAvoidance(Vector3) 等共 7 处。</summary>
    private Vector3 lastStableAvoidanceDirection;
    /// <summary>中文含义：站立路径方向帧。使用位置：VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.ResolveStandingPathDirectionForCurrentFrame() 等共 4 处。</summary>
    private int standingPathDirectionFrame = -1;
    /// <summary>中文含义：站立路径方向用于帧。使用位置：VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.ResolveStandingPathDirectionForCurrentFrame()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)。</summary>
    private Vector3 standingPathDirectionForFrame;
    /// <summary>中文含义：局部避障侧边符号。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.StopAndClearMovement()。</summary>
    private int localAvoidanceSideSign;
    /// <summary>中文含义：局部避障配对方编号。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.StopAndClearMovement()。</summary>
    private int localAvoidancePartnerId;
    /// <summary>中文含义：局部避障侧边保持直到。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.StopAndClearMovement()。</summary>
    private float localAvoidanceSideHoldUntil;
    /// <summary>中文含义：追击入口门。使用位置：作为 DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController) 的参数、作为 PursuitNavigationTopology.TryGetDoorRegions(DoorController, out int, out int) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数 等共 26 处。</summary>
    private DoorController pursuitIngressDoor;
    /// <summary>中文含义：已完成追击入口门。使用位置：VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.ClearPursuitIngress()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3) 等共 4 处。</summary>
    private DoorController completedPursuitIngressDoor;
    /// <summary>中文含义：追击入口来源侧边。使用位置：作为 DoorwayTraversalOccupancy.QueueFirstYieldRow(VillagerController, int) 的参数、作为 DoorwayTraversalOccupancy.ReadQueue(int) 的参数、作为 DoorwayTraversalOccupancy.Register(VillagerController, int) 的参数 等共 11 处。</summary>
    private int pursuitIngressSourceSide;
    /// <summary>中文含义：是否追击入口跨越。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.ClearPursuitIngress() 等共 9 处。</summary>
    private bool pursuitIngressCrossing;
    /// <summary>中文含义：追击入口阶段。使用位置：作为 EnemyCombatTimeline.Describe&lt;VillagerController.PursuitIngressPhase&gt;(VillagerController.PursuitIngressPhase) 的参数、作为 StringBuilder.Append(string) 的参数、VillagerController.ClearPursuitIngress() 等共 6 处。</summary>
    private PursuitIngressPhase pursuitIngressPhase;
    /// <summary>中文含义：追击入口进度门。使用位置：VillagerController.ResetPursuitIngressHeadProgress()、VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。</summary>
    private DoorController pursuitIngressProgressDoor;
    /// <summary>中文含义：追击入口进度位置。使用位置：作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.ResetPursuitIngressHeadProgress()、VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。</summary>
    private Vector3 pursuitIngressProgressPosition;
    /// <summary>中文含义：追击入口上一次进度时间。使用位置：作为 float.IsNegativeInfinity(float) 的参数、VillagerController.ResetPursuitIngressHeadProgress()、VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。</summary>
    private float pursuitIngressLastProgressTime = float.NegativeInfinity;
    /// <summary>中文含义：是否门03室内转向通过的。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.ClearPursuitIngress()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3) 等共 4 处。</summary>
    private bool door03InteriorTurnPassed;
    /// <summary>中文含义：待处理追击区域版本。使用位置：VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.ReceivePursuitRegionChange(int, Vector3)。</summary>
    private int pendingPursuitRegionVersion = -1;
    /// <summary>中文含义：已应用追击区域版本。使用位置：VillagerController.ApplyPendingPursuitRegionChange()。</summary>
    private int appliedPursuitRegionVersion = -1;
    /// <summary>中文含义：待处理追击区域玩家位置。使用位置：作为 EnemyPerceptionModule.UpdateRegionMemory(Vector3) 的参数、VillagerController.ReceivePursuitRegionChange(int, Vector3)。</summary>
    private Vector3 pendingPursuitRegionPlayerPosition;
    /// <summary>中文含义：上一次稳定爬行移动方向。使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、VillagerController.ArchitectureReadLastStableCrawlMoveDirection.get、VillagerController.ResetCrawlOrientationCache() 等共 6 处。</summary>
    private Vector3 lastStableCrawlMoveDirection;
    /// <summary>中文含义：爬行朝向是否已初始化。使用位置：VillagerController.ResetCrawlOrientationCache()、VillagerController.ResolveStableCrawlMoveDirection()、VillagerController.StopAndClearMovement() 等共 4 处。</summary>
    private bool crawlFacingInitialized;
    /// <summary>中文含义：攻击恢复位置。</summary>
    private Vector3 attackRecoveryPosition
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAttackRecoveryPosition;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：攻击恢复旋转。</summary>
    private Quaternion attackRecoveryRotation
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAttackRecoveryRotation;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：攻击恢复是否仍待处理。</summary>
    private bool attackRecoveryPending
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadAttackRecoveryPending;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：实际移动采样位置。使用位置：VillagerController.Start()、VillagerController.UpdateActualMovementSample()。</summary>
    private Vector3 actualMovementSamplePosition;
    /// <summary>中文含义：实际水平移动速度。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、VillagerController.ArchitectureReadActualHorizontalMovementSpeed.get、VillagerController.IsCrowdMovementStalled() 等共 4 处。</summary>
    private float actualHorizontalMovementSpeed;
    /// <summary>中文含义：请求的站立移动参数。</summary>
    private float requestedStandingMoveParameter
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadRequestedStandingMoveParameter;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：上一次站立指令指定的速度。使用位置：VillagerController.CollectCombatTimelineSnapshot()。</summary>
    private float lastStandingCommandedSpeed
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadLastStandingCommandedSpeed;
            }
            else
            {
                return default;
            }
        }
    }

    // 可选地记录站立动画根位移速度，用于兼容旧配置与移动诊断。
    // 默认由寻路速度驱动代码位移，避免动画、碰撞与位移形成反馈闭环。
    /// <summary>中文含义：站立根运动速度。使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数 等共 9 处。</summary>
    private float standingRootMotionSpeed;
    /// <summary>中文含义：是否具有站立根运动速度。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.OnAnimatorMove()、VillagerController.ResolveStandingRootMotionSpeed(float) 等共 4 处。</summary>
    private bool hasStandingRootMotionSpeed;
    /// <summary>中文含义：站立移动意图开始时间。</summary>
    private float standingMoveIntentStartedTime
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadStandingMoveIntentStartedTime;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：实际移动采样是否已初始化。使用位置：VillagerController.Start()、VillagerController.UpdateActualMovementSample()。</summary>
    private bool actualMovementSampleInitialized;
    /// <summary>中文含义：是否此前具有站立移动意图。</summary>
    private bool hadStandingMoveIntent
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadHadStandingMoveIntent;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：站立移动行为是否可见。</summary>
    private bool standingLocomotionVisible
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadStandingLocomotionVisible;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：移动行为阻挡持续时间。</summary>
    private float locomotionBlockedDuration
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadLocomotionBlockedDuration;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：移动行为移动中持续时间。</summary>
    private float locomotionMovingDuration
    {
        get
        {
            if (animationPresenter != null)
            {
                return animationPresenter.ReadLocomotionMovingDuration;
            }
            else
            {
                return default;
            }
        }
    }

    // --- 移动诊断私有状态 ---
    /// <summary>中文含义：诊断采样计时器。使用位置：VillagerController.DiagUpdate()。</summary>
    private float diagSampleTimer;
    /// <summary>中文含义：诊断上一次采样位置。使用位置：VillagerController.DiagBuildSnapshot()、VillagerController.DiagSample()。</summary>
    private Vector3 diagLastSamplePos;
    /// <summary>中文含义：是否诊断首个采样。使用位置：VillagerController.DiagBuildSnapshot()、VillagerController.DiagSample()。</summary>
    private bool diagFirstSample = true;
    /// <summary>中文含义：诊断前一次动画哈希编号集合。使用位置：VillagerController.DiagTrackAnimationState()。</summary>
    private int[] diagPrevAnimHashes = new int[4];
    /// <summary>中文含义：是否诊断前一次动画初始化。使用位置：VillagerController.DiagTrackAnimationState()。</summary>
    private bool diagPrevAnimInit;
    // 每帧缓冲，仅在0.2s采样时输出
    /// <summary>中文含义：诊断事件动画移动增量位置。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.OnAnimatorMove()。</summary>
    private Vector3 diagOnAnimMoveDeltaPos;
    /// <summary>中文含义：诊断事件动画移动前置位置。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.OnAnimatorMove()。</summary>
    private Vector3 diagOnAnimMovePrePos;
    /// <summary>中文含义：诊断事件动画移动柱位置。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.OnAnimatorMove()。</summary>
    private Vector3 diagOnAnimMovePostPos;
    /// <summary>中文含义：诊断事件动画移动模式。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.OnAnimatorMove()。</summary>
    private EnemyMovementMode diagOnAnimMoveMode;
    /// <summary>中文含义：是否诊断事件动画移动已调用。使用位置：VillagerController.DiagSample()、VillagerController.OnAnimatorMove()。</summary>
    private bool diagOnAnimMoveCalled;
    /// <summary>中文含义：诊断更新冷却前置位置。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagUpdateCDPrePos;
    /// <summary>中文含义：诊断更新冷却柱位置。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagUpdateCDPostPos;
    /// <summary>中文含义：诊断更新冷却期望方向。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagUpdateCDDesiredDir;
    /// <summary>中文含义：诊断更新冷却速度。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private float diagUpdateCDSpeed;
    /// <summary>中文含义：诊断更新冷却标签。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagSample()、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private string diagUpdateCDTag;
    /// <summary>中文含义：是否诊断站立路径意图。使用位置：VillagerController.UpdateCodeDrivenMovement()。</summary>
    private bool diagStandingPathIntent;
    /// <summary>中文含义：诊断站立请求的位移。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagStandingRequestedDisplacement;
    /// <summary>中文含义：诊断站立避障位移。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagStandingAvoidanceDisplacement;
    /// <summary>中文含义：诊断站立朝向受限位移。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.UpdateCodeDrivenMovement()。</summary>
    private Vector3 diagStandingFacingLimitedDisplacement;
    /// <summary>中文含义：寻路代理。使用位置：作为 DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3) 的参数、作为 EnemyAIManager.IsStableSurroundArrival(VillagerController, Vector3, Vector3, float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数 等共 61 处。</summary>
    private PathAgent agent;
    /// <summary>中文含义：特殊通行。使用位置：VillagerController.ArchitectureReadSpecialTraversal.get、VillagerController.CanEvaluateStandingCombatAction.get、VillagerController.IsNavigationTraversalProtected.get 等共 7 处。</summary>
    private EnemyWindowTraversal specialTraversal;
    /// <summary>中文含义：动画控制器。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、作为 StringBuilder.Append(string) 的参数、作为 VillagerController.DiagLog(string, string) 的参数 等共 21 处。</summary>
    private Animator anim;
    /// <summary>中文含义：行为树。使用位置：VillagerController.ArchitectureReadBehaviorTree.get、VillagerController.EnsureBehaviorTreeRunning()、VillagerController.MaintainAssassinationShowcaseIdle() 等共 5 处。</summary>
    private BehaviorTree behaviorTree;
    /// <summary>中文含义：当前敌人上下文。使用位置：作为 EnemyContext.MoveSpeedToAgentSpeed(float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、作为 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3) 的参数 等共 17 处。</summary>
    private EnemyContext ctx;
    /// <summary>中文含义：碰撞体控制器。使用位置：VillagerController.ArchitectureReadColliderCtrl.get、VillagerController.OnDrawGizmosSelected()、VillagerController.Start()。</summary>
    private EnemyColliderController colliderCtrl;
    /// <summary>中文含义：电锯桥接。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数、VillagerController.ArchitectureReadChainsawBridge.get、VillagerController.GetTargetMoveSpeed() 等共 6 处。</summary>
    private ChainsawManAnimationBridge chainsawBridge;
    /// <summary>中文含义：配对动作参与者。使用位置：VillagerController.ArchitectureReadQteParticipant.get、VillagerController.EnsureBehaviorTreeRunning()、VillagerController.IsQTEControlled.get 等共 5 处。</summary>
    private VillagerQTEParticipant qteParticipant;
    /// <summary>中文含义：移动碰撞体。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float) 的参数 等共 11 处。</summary>
    private CapsuleCollider movementCollider;
    /// <summary>中文含义：门03通行路线。使用位置：作为 Mathf.Max(float, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、作为 VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry) 的参数 等共 11 处。</summary>
    [Header("场景通路引用")]
    public HouseThreeTraversalGeometry door03TraversalRoute;
    /// <summary>中文含义：起身稳定化结束点所在。</summary>
    private float standUpStabilizationEndsAt
    {
        get
        {
            if (combatModule != null)
            {
                return combatModule.ReadStandUpStabilizationEndsAt;
            }
            else
            {
                return default;
            }
        }
    }

    /// <summary>中文含义：地面命中结果。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数 等共 12 处。</summary>
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    /// <summary>中文含义：移动命中结果。只读字段，初始化后不由普通写入口替换。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数 等共 9 处。</summary>
    private readonly RaycastHit[] movementHits = new RaycastHit[16];
    /// <summary>中文含义：已抬高移动命中结果。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.HasRaisedMovementBlocker(Collider, Vector3, Vector3, float, Vector3, float)。</summary>
    private readonly RaycastHit[] raisedMovementHits = new RaycastHit[8];
    /// <summary>中文含义：移动重叠结果。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、作为 VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController) 的参数 等共 6 处。</summary>
    private readonly Collider[] movementOverlaps = new Collider[16];
    /// <summary>中文含义：移动阻挡持续时间。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.IsCrowdMovementStalled()、VillagerController.ResetBlockedMovement() 等共 5 处。</summary>
    private float movementBlockedDuration;
    /// <summary>中文含义：下一次阻挡重新寻路时间。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</summary>
    private float nextBlockedRepathTime;
    /// <summary>中文含义：追逐进度位置。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、VillagerController.UpdateChaseProgressRecovery()。</summary>
    private Vector3 chaseProgressPosition;
    /// <summary>中文含义：追逐无进度持续时间。使用位置：VillagerController.IsCrowdMovementStalled()、VillagerController.UpdateChaseProgressRecovery()。</summary>
    private float chaseNoProgressDuration;
    /// <summary>中文含义：下一次追逐恢复时间。使用位置：VillagerController.UpdateChaseProgressRecovery()。</summary>
    private float nextChaseRecoveryTime;
    /// <summary>中文含义：追逐进度是否已初始化。使用位置：VillagerController.UpdateChaseProgressRecovery()。</summary>
    private bool chaseProgressInitialized;
    /// <summary>中文含义：追逐恢复尝试次数。使用位置：作为 Mathf.Min(int, int) 的参数、VillagerController.UpdateChaseProgressRecovery()。</summary>
    private int chaseRecoveryAttempts;
    /// <summary>中文含义：追逐最佳剩余距离。使用位置：VillagerController.UpdateChaseProgressRecovery()。</summary>
    private float chaseBestRemainingDistance;
    /// <summary>中文含义：追逐进度目的地。使用位置：VillagerController.UpdateChaseProgressRecovery()。</summary>
    private Vector3 chaseProgressDestination;
    /// <summary>中文含义：下一次未完成追击重新寻路时间。使用位置：VillagerController.TryRecoverIncompletePursuitArrival()。</summary>
    private float nextIncompletePursuitRepathTime;
    /// <summary>中文含义：当前移动模式。使用位置：作为 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle) 的参数、作为 EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode) 的参数、作为 EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode) 的参数 等共 26 处。</summary>
    public EnemyMovementMode CurrentMovementMode
    {
        get
        {
            if (motionExecutor != null)
            {
                return motionExecutor.Mode;
            }
            else
            {
                return EnemyMovementMode.FullyStopped;
            }
        }
    }

    /// <summary>中文含义：当前状态。使用位置：作为 EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState) 的参数、作为 HashSet&lt;VillagerController&gt;.RemoveWhere(Predicate&lt;VillagerController&gt;) 的参数、作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数 等共 40 处。</summary>
    public VillagerState CurrentState { get => observableState; }
    /// <summary>中文含义：是否死亡。使用位置：CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、CharacterMotionExecutor.Valid(MotionControlToken)、ChasePlayer.OnUpdate() 等共 77 处。</summary>
    public bool IsDead => healthModule == null || healthModule.HasDeath;
    /// <summary>中文含义：是否运行时已初始化。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.CloseAction()、EnemyActionCoordinator.RestoreRequest() 等共 11 处。</summary>
    public bool IsRuntimeInitialized => ctx != null && agent != null && anim != null;
    /// <summary>中文含义：是否存在待处理的命中。使用位置：ChasePlayer.OnUpdate()、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyFootAction.AllowGroundContact.get 等共 21 处。</summary>
    public bool HasPendingHit => pendingHit;
    /// <summary>中文含义：是否存在待处理的爬行。使用位置：ChasePlayer.OnUpdate()、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyStateSnapshot.EnemyStateSnapshot(VillagerController) 等共 16 处。</summary>
    public bool HasPendingCrawl => pendingCrawl;
    /// <summary>中文含义：是否存在待处理的起身。使用位置：ChasePlayer.OnUpdate()、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyStateSnapshot.EnemyStateSnapshot(VillagerController) 等共 16 处。</summary>
    public bool HasPendingStandUp => pendingStandUp;
    /// <summary>中文含义：是否永久爬行中。使用位置：EnemyFootAction.AllowGroundContact.get、EnemyFootAction.AttackStartupMiddle.get、ExecuteCrawl.OnUpdate() 等共 4 处。</summary>
    public bool IsPermanentlyCrawling => permanentCrawl;
    /// <summary>中文含义：是否攻击结束。使用位置：作为 HashSet&lt;VillagerController&gt;.RemoveWhere(Predicate&lt;VillagerController&gt;) 的参数、EnemyStateSnapshot.EnemyStateSnapshot(VillagerController)、ExecuteAttack.OnUpdate() 等共 5 处。</summary>
    public bool AttackFinished => attackFinished;
    /// <summary>中文含义：是否命中结束。使用位置：EnemyStateSnapshot.EnemyStateSnapshot(VillagerController)、ExecuteHit.OnUpdate()。</summary>
    public bool HitFinished => hitFinished;
    /// <summary>中文含义：是否起身结束。使用位置：EnemyStateSnapshot.EnemyStateSnapshot(VillagerController)、ExecuteStandUp.OnUpdate()。</summary>
    public bool StandUpFinished => standUpFinished;
    /// <summary>中文含义：是否可以打开体术来源当前硬直。使用位置：VillagerQTEParticipant.UpdateBodyTechniqueAnimationWindow()。</summary>
    public bool CanOpenBodyTechniqueFromCurrentStagger => currentStandingStaggerBodyTechnique && Time.time < bodyTechniqueWindowEndTime;
    /// <summary>中文含义：是否可以评估站立战斗动作。使用位置：EnemyCombatModule.CanEvaluateStandingCombatAction.get、VillagerController.CanEvaluateStandingQTEAction.get、VillagerController.ShouldHoldCombatPosition.get。</summary>
    public bool CanEvaluateStandingCombatAction => !IsDead && (specialTraversal == null || !specialTraversal.IsMovementProtected) && !pendingHit && !pendingCrawl && !pendingStandUp && fullyAwareOfPlayer && Player != null && IsCombatEngaged;
    /// <summary>中文含义：是否可以评估站立配对动作动作。使用位置：CanAttemptEnemyQTEBase.OnUpdate()、CanAttemptFrontGrabQTE.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind) 等共 5 处。</summary>
    public bool CanEvaluateStandingQTEAction => CanEvaluateStandingCombatAction && !IsNavigationTraversalProtected && CanSeePlayer();
    /// <summary>中文含义：是否可以评估爬行战斗动作。使用位置：CanAttemptEnemyQTEBase.OnUpdate()、CanCrawlAttackNow.OnUpdate()、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind) 等共 7 处。</summary>
    public bool CanEvaluateCrawlCombatAction => supportsCrawl && !IsDead && pendingCrawl && !pendingHit && !pendingStandUp && fullyAwareOfPlayer && Player != null && IsCombatEngaged;
    /// <summary>中文含义：巡逻中心。使用位置：SelectPatrolPoint.OnUpdate()。</summary>
    public Vector3 PatrolCenter => patrolCenter;
    /// <summary>中文含义：是否具有巡逻目标。使用位置：MoveToPatrolPoint.OnStart()、MoveToPatrolPoint.OnUpdate()。</summary>
    public bool HasPatrolTarget => hasPatrolTarget;
    /// <summary>中文含义：巡逻目标。使用位置：作为 EnemyActionCoordinator.RequestPathTarget(Vector3) 的参数。</summary>
    public Vector3 PatrolTarget => patrolTarget;

    /// <summary>中文含义：寻路代理。使用位置：作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数、作为 IEnumerable&lt;VillagerController&gt;.Where&lt;VillagerController&gt;(Func&lt;VillagerController, bool&gt;) 的参数、作为 Mathf.Max(float, float) 的参数 等共 5 处。</summary>
    public PathAgent Agent
    {
        get
        {
            if (agent == null)
            {
                agent = GetComponent<PathAgent>();
            }

            return agent;
        }
    }

    /// <summary>TryResolveReachableNavigationPoint 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;PathAgent&gt;()、PathAgent.TryResolveReachableDestination(Vector3, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)、EnemyAIManager.SelectSurroundSlot(VillagerController, Vector3, float, int, bool, out int)、EnemyWindowTraversal.RegisterWindowEntryPoint(EnemyWindowTraversalPlan, out Vector3)、EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3) 等共 6 处；下游（含内部回调体） Component.GetComponent&lt;PathAgent&gt;()、PathAgent.TryResolveReachableDestination(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 PathAgent.TryResolveReachableDestination(Vector3, out Vector3) 的参数、VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3)。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：作为 PathAgent.TryResolveReachableDestination(Vector3, out Vector3) 的参数、VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3)。</param>
    public bool TryResolveReachableNavigationPoint(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (agent == null)
        {
            agent = GetComponent<PathAgent>();
        }

        return agent != null && agent.TryResolveReachableDestination(rawTarget, out resolvedTarget);
    }

    /// <summary>TryResolvePursuitNavigationPoint 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;PathAgent&gt;()、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;PathAgent&gt;()、PathAgent.TryResolvePursuitDestination(Vector3, out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rawTarget">中文含义：原始目标。使用位置：作为 PathAgent.TryResolvePursuitDestination(Vector3, out Vector3) 的参数、VillagerController.TryResolvePursuitNavigationPoint(Vector3, out Vector3)。</param>
    /// <param name="resolvedTarget">中文含义：解算后的目标。输出参数，由本入口写入并交给调用方。使用位置：作为 PathAgent.TryResolvePursuitDestination(Vector3, out Vector3) 的参数、VillagerController.TryResolvePursuitNavigationPoint(Vector3, out Vector3)。</param>
    public bool TryResolvePursuitNavigationPoint(Vector3 rawTarget, out Vector3 resolvedTarget)
    {
        resolvedTarget = rawTarget;
        if (agent == null)
        {
            agent = GetComponent<PathAgent>();
        }

        return agent != null && agent.TryResolvePursuitDestination(rawTarget, out resolvedTarget);
    }

    /// <summary>中文含义：玩家。使用位置：作为 EnemyAIManager.IsAtStableSurroundPosition(VillagerController, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数 等共 43 处。</summary>
    public LeonController Player => ResolvePlayer();

    /// <summary>中文含义：玩家距离。使用位置：作为 ChainsawManAnimationBridge.BeginIntroTaunt(float) 的参数、作为 ChainsawManAnimationBridge.ResolveChaseMoveSpeed(float, float, float) 的参数、EnemyCombatModule.PlayerDistance.get 等共 5 处。</summary>
    public float PlayerDistance
    {
        get
        {
            if (Player != null)
            {
                return Vector3.Distance(transform.position, Player.transform.position);
            }
            else
            {
                return float.MaxValue;
            }
        }
    }

    /// <summary>中文含义：是否玩家内部攻击范围。使用位置：IsInAttackRange.OnUpdate()。</summary>
    public bool IsPlayerInAttackRange => IsPlayerWithinBodyContactRange();
    /// <summary>中文含义：是否攻击就绪。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyCombatModule.IsAttackReady.get、EnemyStateSnapshot.EnemyStateSnapshot(VillagerController) 等共 4 处。</summary>
    public bool IsAttackReady => Time.time - lastAttackTime >= attackCooldown;
    /// <summary>中文含义：警觉。</summary>
    public float Awareness => awareness;
    /// <summary>中文含义：是否完全察觉所属玩家。使用位置：InvestigatePlayer.OnUpdate()、NeedsChainsawStart.OnUpdate()。</summary>
    public bool IsFullyAwareOfPlayer => fullyAwareOfPlayer;
    /// <summary>中文含义：是否战斗已接战。使用位置：作为 HashSet&lt;VillagerController&gt;.RemoveWhere(Predicate&lt;VillagerController&gt;) 的参数、ChasePlayer.OnUpdate()、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3) 等共 15 处。</summary>
    public bool IsCombatEngaged => combatEngaged;

    /// <summary>中文含义：是否具有追逐路径失败。使用位置：EnemyAIManager.GetDirectPressureScore(VillagerController, Vector3)、ExecuteChaseRecovery.OnUpdate()、ExecuteCombat.OnUpdate() 等共 4 处。</summary>
    public bool HasChasePathFailure
    {
        get
        {
            if (agent == null)
            {
                agent = GetComponent<PathAgent>();
            }

            return agent != null && (agent.HasPursuitResolutionFailure || agent.CurrentPathState == PathAgentState.Failed);
        }
    }

    /// <summary>中文含义：是否具有未完成追击抵达。使用位置：ExecuteCombat.OnUpdate()、VillagerController.TryRecoverIncompletePursuitArrival()、VillagerController.UpdateChaseExecution() 等共 4 处。</summary>
    public bool HasIncompletePursuitArrival
    {
        get
        {
            if (agent == null)
            {
                agent = GetComponent<PathAgent>();
            }

            // player（玩家）：使用位置：作为 EnemyAIManager.IsStableSurroundArrival(VillagerController, Vector3, Vector3, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.HasIncompletePursuitArrival.get。
LeonController player = Player;
            if (agent == null || player == null || !ShouldChasePlayer || agent.CurrentPathState != PathAgentState.Arrived)
            {
                return false;
            }

            EnemyAIManager tacticalManager = EnemyAIManager.Instance;
            if (tacticalManager != null &&
                (tacticalManager.TryReadTacticalMovement(this, out _) || tacticalManager.TryGetInterceptionDestination(this, out _)))
            {
                return false;
            }

            // acceptedDistance（已接受距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasIncompletePursuitArrival.get。
float acceptedDistance = Mathf.Max(incompletePursuitArrivalDistance, attackStartRange + agent.waypointReachedRadius + 0.15f);
            // manager（当前敌人智能管理器）：使用位置：VillagerController.HasIncompletePursuitArrival.get。
EnemyAIManager manager = EnemyAIManager.Instance;
            // arrivedAtAssignedSurroundSlot（是否已抵达所在已分配包围槽位）：使用位置：VillagerController.HasIncompletePursuitArrival.get。
bool arrivedAtAssignedSurroundSlot = manager != null && manager.IsStableSurroundArrival(this, player.transform.position, agent.Destination, Mathf.Max(agent.waypointReachedRadius, agent.arrivedDestinationChangeThreshold));
            // destinationStillFar（是否目的地仍然远端）：使用位置：VillagerController.HasIncompletePursuitArrival.get。
bool destinationStillFar = HorizontalDistance(agent.Destination, player.transform.position) > acceptedDistance;
            // enemyStillFar（是否敌人仍然远端）：使用位置：VillagerController.HasIncompletePursuitArrival.get。
bool enemyStillFar = HorizontalDistance(transform.position, player.transform.position) > acceptedDistance;
            // 玩家可能位于二楼平台正下方或外墙另一侧。此时水平距离很小，
            // 但“已到达”只代表抵达了投影节点，并不代表已经能够接近玩家。
            // 继续使用近身可达性校验，避免已到达状态绕过所有恢复分支。
            // requiresTraversal（是否需要通行）：保存 VillagerController.RequiresNavigationTraversalToPlayer() 的返回结果，供当前步骤使用。使用位置：VillagerController.HasIncompletePursuitArrival.get。
bool requiresTraversal = RequiresNavigationTraversalToPlayer();
            if (arrivedAtAssignedSurroundSlot && !requiresTraversal)
            {
                return false;
            }

            return destinationStillFar || enemyStillFar || requiresTraversal;
        }
    }

    /// <summary>中文含义：是否村庄入口战斗锁定。使用位置：VillagerController.ArchitectureReadIsVillageEntranceCombatLocked.get、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    private bool IsVillageEntranceCombatLocked
    {
        get
        {
            // manager（当前敌人智能管理器）：使用位置：VillagerController.IsVillageEntranceCombatLocked.get。
EnemyAIManager manager = EnemyAIManager.Instance;
            return manager != null && !manager.IsCombatUnlocked;
        }
    }

    /// <summary>中文含义：是否配对动作受控。使用位置：作为 HashSet&lt;VillagerController&gt;.RemoveWhere(Predicate&lt;VillagerController&gt;) 的参数、CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、EnemyActionCoordinator.CallbackValid(ActionHandle) 等共 28 处。</summary>
    public bool IsQTEControlled => qteParticipant != null && qteParticipant.IsControllingEnemyAI;
    /// <summary>中文含义：是否导航通行受保护的。使用位置：ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawStart.OnStart()、NeedsChainsawIntroTaunt.OnUpdate() 等共 12 处。</summary>
    public bool IsNavigationTraversalProtected => (specialTraversal != null && specialTraversal.IsMovementProtected) || pursuitIngressCrossing;
    /// <summary>中文含义：是否内部攻击起始范围。</summary>
    internal bool NeedsCombatTraversal()
    {
        LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (IsNavigationTraversalProtected || pursuitIngressDoor != null ||
            (specialTraversal != null && (specialTraversal.HasPendingRoute || specialTraversal.HasPendingDropRoute)) ||
            !IsPlayerWithinCombatVerticalRange(player))
        {
            return true;
        }

        return TryResolveCurrentPursuitRegions(player.transform.position, out int enemyRegion, out int targetRegion) &&
            enemyRegion != targetRegion;
    }

    public bool IsInAttackStartRange => IsPlayerWithinAttackStartRange();
    /// <summary>中文含义：是否具有任意玩家警觉。使用位置：EnemyStateSnapshot.EnemyStateSnapshot(VillagerController)、IdleWait.OnUpdate()、InvestigatePlayer.OnUpdate() 等共 5 处。</summary>
    public bool HasAnyPlayerAwareness => combatEngaged || fullyAwareOfPlayer || awareness > 0.01f;

    /// <summary>CanBeAssassinatedBy 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">中文含义：里昂。使用位置：VillagerController.CanBeAssassinatedBy(LeonController)。</param>
    public bool CanBeAssassinatedBy(LeonController leon)
    {
        if (leon == null || IsDead || !IsActive || pendingHit || pendingCrawl || pendingStandUp || pendingTripOver || CurrentState == VillagerState.Crawl || CurrentState == VillagerState.CrawlAttack || CurrentState == VillagerState.Staggered || CurrentState == VillagerState.Dead || CurrentState == VillagerState.Attack || CurrentState == VillagerState.Chase)
        {
            return false;
        }

        return !fullyAwareOfPlayer && !combatEngaged;
    }

    /// <summary>中文含义：是否应当调查玩家。使用位置：ShouldInvestigatePlayer.OnUpdate()。</summary>
    public bool ShouldInvestigatePlayer => false;

    /// <summary>中文含义：是否应当保持战斗位置。使用位置：ExecuteChaseRecovery.OnUpdate()、ExecuteCombat.OnUpdate()、HoldCombatPosition.OnUpdate() 等共 4 处。</summary>
    public bool ShouldHoldCombatPosition
    {
        get
        {
            if (IsCrowdBypassing || !CanEvaluateStandingCombatAction || RequiresNavigationTraversalToPlayer() || !IsStandingAttackSurfaceStable())
            {
                return false;
            }

            // manager（当前敌人智能管理器）：使用位置：VillagerController.ShouldHoldCombatPosition.get。
EnemyAIManager manager = EnemyAIManager.Instance;
            // atAssignedSurroundPosition（是否所在已分配包围位置）：使用位置：VillagerController.ShouldHoldCombatPosition.get。
bool atAssignedSurroundPosition = manager != null && manager.IsAtStableSurroundPosition(this, Player.transform.position);
            // insideHoldRange（是否内部保持范围）：使用位置：VillagerController.ShouldHoldCombatPosition.get。
bool insideHoldRange;
            if (combatHoldActive)
            {
                insideHoldRange = IsPlayerWithinCombatHoldRange();
            }
            else
            {
                insideHoldRange = IsPlayerWithinAttackStartRange();
            }

            insideHoldRange |= atAssignedSurroundPosition;
            if (!insideHoldRange)
            {
                return false;
            }

            // insideAttackStartRange（是否内部攻击起始范围）：保存 VillagerController.IsPlayerWithinAttackStartRange() 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldHoldCombatPosition.get。
bool insideAttackStartRange = IsPlayerWithinAttackStartRange();
            if (insideAttackStartRange && (!CanEngagePlayerAtCloseRange()
                || (!atAssignedSurroundPosition && !combatModule.CanReachWithStandingPose())))
            {
                return false;
            }

            if (manager == null || manager.HasAttackReservation(this))
            {
                return false;
            }

            if (!insideAttackStartRange)
            {
                return atAssignedSurroundPosition;
            }

            return !IsAttackReady || !manager.CanAcquireAttackReservation(this);
        }
    }

    /// <summary>中文含义：是否应当追逐玩家。使用位置：ChasePlayer.OnUpdate()、ShouldChasePlayer.OnUpdate()、VillagerController.HasIncompletePursuitArrival.get 等共 10 处。</summary>
    public bool ShouldChasePlayer => combatEngaged && Player != null && !IsDead && !pendingHit && !pendingCrawl && !pendingStandUp && !IsQTEControlled && CurrentState != VillagerState.Attack && CurrentState != VillagerState.CrawlAttack && CurrentState != VillagerState.Staggered && CurrentState != VillagerState.StandUp && CurrentState != VillagerState.Dead;
    /// <summary>中文含义：是否需要导航目标玩家。</summary>
    public bool RequiresNavigationToPlayer => RequiresNavigationTraversalToPlayer();
    /// <summary>中文含义：是否需要直接通行目标玩家。</summary>
    public bool RequiresDirectTraversalToPlayer => RequiresDirectNavigationTraversalToPlayer();
    /// <summary>中文含义：当前追逐目标。使用位置：作为 EnemyActionCoordinator.RequestPathTarget(Vector3) 的参数、VillagerController.RefreshChaseDestination()、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3) 等共 4 处。</summary>
    public Vector3 CurrentChaseTarget => GetCurrentCombatTarget();
    /// <summary>中文含义：状态快照。使用位置：CannotSeePlayer.OnUpdate()、CanSeePlayer.OnUpdate()、HasCrawlRequest.OnUpdate() 等共 8 处。</summary>
    public EnemyStateSnapshot StateSnapshot => new EnemyStateSnapshot(this);

    /// <summary>中文含义：已死亡。事件通信成员 Died，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)、ChapterOneMissionManager.UnsubscribeAllEnemies() 等共 9 处。</summary>
    public event Action<VillagerController> Died;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterHealthModule.Initialize(float)、CharacterMotionExecutor.BindingRigidbody(Rigidbody)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;BehaviorTree&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;ChainsawManAnimationBridge&gt;()、Component.GetComponent&lt;EnemyColliderController&gt;() 等共 33 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (knifeLocalHitStop == null)
        {
            knifeLocalHitStop = GetComponent<KnifeHitStop>();
        }

        if (healthModule == null || healthModule.gameObject != gameObject || combatModule == null || perceptionModule == null || actionCoordinator == null || animationPresenter == null || motionExecutor == null || motionExecutor.host != this || combatModule.host != this || perceptionModule.host != this || actionCoordinator.host != this || animationPresenter.host != this)
        {
            Debug.LogError("敌人模块引用不完整或绑定到其他角色，停止开放该角色：" + name + ".生命模块", this);
            enabled = false;
            return;
        }

        healthModule.Initialize(maxHealth);
        EnemyCombatTimeline.Register(this);
        agent = GetComponent<PathAgent>();
        specialTraversal = GetComponent<EnemyWindowTraversal>();
        anim = GetComponent<Animator>();
        animationPresenter.InitializeAnimationParameter();
        behaviorTree = GetComponent<BehaviorTree>();
        colliderCtrl = GetComponent<EnemyColliderController>();
        chainsawBridge = GetComponent<ChainsawManAnimationBridge>();
        qteParticipant = GetComponent<VillagerQTEParticipant>();
        movementCollider = GetComponent<CapsuleCollider>();
        BindingTraversalGeometry();
        if (colliderCtrl == null)
        {
            colliderCtrl = gameObject.AddComponent<EnemyColliderController>();
        }

        EnsureRuntimeHitboxCoverage();
        LeonPlayerStairRampRuntime.RegisterCharacter(this);
        if (BipedFootPlanting.EnemyGroundIKEnabled && anim && anim.isHuman && !GetComponent<EnemyFootAction>())
        {
            gameObject.AddComponent<EnemyFootAction>();
        }

        // rb（运动刚体）：保存 Component.GetComponent<Rigidbody>() 的返回结果，供当前步骤使用。使用位置：作为 CharacterMotionExecutor.BindingRigidbody(Rigidbody) 的参数、VillagerController.Start()。
var rb = GetComponent<Rigidbody>();
        motionExecutor.BindingRigidbody(rb);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        UpdateHealthAnimationParameter();
        actionCoordinator.ApproveMotionMode(EnemyMovementMode.FullyStopped);
        SnapToGround(groundProbeHeight, Mathf.Max(groundProbeDistance, 5f), true);
        RecoverInvalidNavigationSpawn();
        actionCoordinator.InitializeAction();
        actualMovementSamplePosition = transform.position;
        actualMovementSampleInitialized = true;
        // createdValue70（本次新建的敌人上下文）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：VillagerController.Start()。
global::EnemyContext createdValue70 = new EnemyContext(healthModule, combatModule);
        // receiverValue71（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.Start()。
global::EnemyAIManager receiverValue71 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue71, null))
        {
            createdValue70.Player = receiverValue71.PlayerTarget;
        }
        else
        {
            createdValue70.Player = default;
        }

        if (missionEnemyType == MissionEnemyType.Sawman)
        {
            createdValue70.MaximumStandingMoveParameter = EnemyContext.StandingWalkMoveParameter;
        }
        else
        {
            createdValue70.MaximumStandingMoveParameter = EnemyContext.StandingChaseMoveParameter;
        }

        ctx = createdValue70;
        perceptionModule.SampleLineOfSight();
        if (behaviorTree != null)
        {
            if (assassinationShowcaseIdle)
            {
                behaviorTree.DisableBehavior();
            }
            else
            {
                behaviorTree.StartWhenEnabled = true;
                behaviorTree.RestartWhenComplete = true;
                behaviorTree.EnableBehavior();
            }
        }
        else
        {
            Debug.LogError("敌人缺少行为设计器运行组件，无法启动智能决策。", this);
        }

        SetMovementMode(EnemyMovementMode.FullyStopped);
        if (assassinationShowcaseIdle)
        {
            BeginIdle();
        }
        else
        {
            // receiverValue74（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.Start()。
global::EnemyAIManager receiverValue74 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue74, null))
            {
                receiverValue74.RegisterEnemy(this);
            }
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RestoreRequest()、EnemyCombatTimeline.Register(VillagerController, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (IsRuntimeInitialized && !IsDead)
        {
            // receiverValue75（判空前缓存的动作协调）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.OnEnable()。
global::EnemyActionCoordinator receiverValue75 = actionCoordinator;
            if (!global::System.Object.ReferenceEquals(receiverValue75, null))
            {
                receiverValue75.RestoreRequest();
            }

            EnemyCombatTimeline.Register(this, diagnosticWave);
        }
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.UnregisterEnemy(VillagerController)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、LeonPlayerStairRampRuntime.RemoveCharacter(VillagerController)、object.ReferenceEquals(object, object)、VillagerController.UnregisterPursuitIngressWaiter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        LeonPlayerStairRampRuntime.RemoveCharacter(this);
        UnregisterPursuitIngressWaiter();
        // receiverValue76（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.OnDestroy()。
global::HouseThreeRegionNavigation receiverValue76 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue76, null))
        {
            receiverValue76.ReleaseCharacter(this);
        }

        // 场景卸载时管理器可能先被销毁，禁止通过单例访问器重新创建管理器。
        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.UnregisterEnemy(this);
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.Revoke()、CombatDiagnosticFields.Text(string, string)、EnemyActionCoordinator.CloseAction()、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、EnemyCombatTimeline.Unregister(VillagerController) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        corpsePhysics.Release();
        corpseSupportApplied = false;
        orderedToBreach = false;
        ClearCombatGroundMotion();
        if (externalKnockbackRoutine != null)
        {
            StopCoroutine(externalKnockbackRoutine);
            externalKnockbackRoutine = null;
        }
        if (animationPresenter != null)
        {
            animationPresenter.ResetNativeFootContacts();
            animationPresenter.ReleaseVoice();
        }

        if (EnemyCombatTimeline.IsRecord(this))
        {
            EnemyCombatTimeline.Record(this, "组件停用", CombatDiagnosticFields.Text("状态", EnemyCombatTimeline.Describe(CurrentState)));
        }

        EnemyCombatTimeline.Unregister(this);
        NavigationCollisionCache.InvalidateHierarchy(transform);
        EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager != null)
        {
            manager.ReleaseCombatReservation(this);
            manager.InvalidatePursuitDestination(this);
        }

        // receiverValue77（判空前缓存的动作协调）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.OnDisable()。
global::EnemyActionCoordinator receiverValue77 = actionCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue77, null))
        {
            receiverValue77.CloseAction();
        }

        // receiverValue78（判空前缓存的角色运动执行器）：从角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.OnDisable()。
global::CharacterMotionExecutor receiverValue78 = motionExecutor;
        if (!global::System.Object.ReferenceEquals(receiverValue78, null))
        {
            receiverValue78.Revoke();
        }

        if (combatModule != null && IsRuntimeInitialized)
        {
            EndAttack();
        }

        UnregisterPursuitIngressWaiter();
        // receiverValue79（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.OnDisable()。
global::HouseThreeRegionNavigation receiverValue79 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue79, null))
        {
            receiverValue79.ReleaseCharacter(this);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.AdvanceStaggerRestore()、EnemyPerceptionModule.SampleLineOfSight()、VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.DiagUpdate()、VillagerController.EnsureBehaviorTreeRunning()、VillagerController.MaintainAssassinationShowcaseIdle()、VillagerController.RecoverInvalidNavigationSpawn() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (animationPresenter != null && Time.deltaTime > 0f)
        {
            animationPresenter.RestoreNativeFootPose();
        }

        if (IsRuntimeInitialized && IsDead && combatModule != null)
        {
            combatModule.TickNativeDeathPresentation();
        }
        if (IsRuntimeInitialized && !corpsePhysics.Active && externalKnockbackRoutine == null && combatSupportLostTime > 0f
            && !IsNavigationTraversalProtected && !IsQTEControlled && !combatModule.NativeCombatActive && !combatModule.NativeDeathActive)
        {
            CommitCombatGroundMotion(transform.rotation, 0f, out bool landed);
            if (landed && !IsDead && ShouldChasePlayer)
            {
                RefreshChaseDestination();
            }
            if (!landed)
            {
                return;
            }
        }
        if (!IsActive || IsDead || !IsRuntimeInitialized)
        {
            return;
        }

        if (bellWithdrawing)
        {
            // 让已开始的倒地、起身及复起先结算，随后才能交给撤离路线。
            if (combatModule.TickNativeCombat())
            {
                return;
            }
            combatModule.UpdateStandingHitCompletion();
            TryStartBellRoute();
            UpdateActualMovementSample();
            UpdateBellReaction();
            UpdateAnimator();
            animationPresenter.UpdateNativeStandingGait();
            if (!UsesNativeStandingStride)
            {
                UpdateCodeDrivenMovement();
            }
            return;
        }

        if (knifeLocalHitStop != null && knifeLocalHitStop.IsHitStop)
        {
            return;
        }

        if (combatModule != null && combatModule.TickNativeCombat())
        {
            return;
        }

        if (waitingSpawnNavigationRestore && EnemyAIManager.HasInstance && EnemyAIManager.Instance.NavigationQuery != null && EnemyAIManager.Instance.NavigationQuery.CanQuery)
        {
            waitingSpawnNavigationRestore = false;
            RecoverInvalidNavigationSpawn();
        }

        if (assassinationShowcaseIdle && !IsQTEControlled)
        {
            MaintainAssassinationShowcaseIdle();
            return;
        }

        RecoverManagerRegistration();
        perceptionModule.SampleLineOfSight();
        EnsureBehaviorTreeRunning();
        ctx.Player = Player;
        ctx.IsActive = IsActive;
        UpdateCrowdBypass();
        UpdateAwareness();
        combatModule.AdvanceStaggerRestore();
        UpdateActualMovementSample();
        ApplyPendingPursuitRegionChange();
        UpdateChaseExecution();
        UpdateStandingHitCompletion();
        UpdateCrawlFacingBeforeAnimator();
        UpdateAnimator();
        animationPresenter.UpdateNativeStandingGait();
        UpdateStandingAttackHitFallback();
        // movementStartPosition（移动起始位置）：使用位置：作为 VillagerController.UpdateLocomotionPlaybackRate(Vector3) 的参数。
Vector3 movementStartPosition = transform.position;
        if (!UsesNativeStandingStride || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion)
        {
            UpdateCodeDrivenMovement();
        }
        UpdateLocomotionPlaybackRate(movementStartPosition);
        UpdateChaseProgressRecovery();
        DiagUpdate();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ReconcileStandingPursuitFacingAfterAnimation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static readonly int CorpseDeathState = Animator.StringToHash("Death");
    private bool corpseSupportApplied;
    private readonly CorpseBodyPhysics corpsePhysics = new CorpseBodyPhysics();

    void LateUpdate()
    {
        if (!IsDead)
        {
            corpsePhysics.Release();
            corpseSupportApplied = false;
        }
        else if (!IsQTEControlled && !IsNavigationTraversalProtected && externalKnockbackRoutine == null &&
            anim != null && motionExecutor != null && motionExecutor.CurrentToken.Source == GameArchitecture.MotionSource.Death)
        {
            if (!corpseSupportApplied && !corpsePhysics.Active)
            {
                AnimatorStateInfo death = anim.GetCurrentAnimatorStateInfo(0);
                bool ordinaryDeath = !anim.IsInTransition(0) && death.shortNameHash == CorpseDeathState;
                bool ready = ordinaryDeath && death.normalizedTime >= .99f;
                if (combatModule != null && combatModule.NativeDeathActive)
                {
                    ready = combatModule.NativeDeathPoseReady;
                }
                else if (!ready && ordinaryDeath)
                {
                    corpsePhysics.TryBegin(anim, transform, true);
                }
                if (ready && !corpsePhysics.TryBegin(anim, transform, false))
                {
                    FitCorpseToGround();
                }
            }
            if (corpsePhysics.Active)
            {
                Pose pose = corpsePhysics.ReadPose();
                corpseSupportApplied = motionExecutor.CommitPose(motionExecutor.CurrentToken, pose.position, pose.rotation, true);
            }
        }

        ReconcileStandingPursuitFacingAfterAnimation();
        if (animationPresenter != null)
        {
            animationPresenter.UpdateNativeFootContacts(diagStandingPathIntent, diagStandingRequestedDisplacement);
        }
        if (bellWithdrawing && animationPresenter != null)
        {
            bool looking = bellLooking && !IsDead && !IsQTEControlled && !IsNavigationTraversalProtected
                && !HasPendingHit && !combatModule.NativeCombatActive;
            animationPresenter.ApplyBellHeadPose(looking, bellDestination);
        }
    }

    /// <summary>
    /// 站立追击的位移由代码驱动，但动画评估发生在普通更新之后，可能把本帧
    /// 已经对齐的身体朝向重新写回旧动画朝向。这里在动画完成后只校正追击朝向，
    /// 使下一帧的朝向限速与实际路径使用同一个方向。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.LateUpdate()；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、VillagerController.ResolveLocalAvoidanceMinimumForwardAlignment()、VillagerController.ResolveStandingPursuitFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FitCorpseToGround()
    {
        Vector3 normalSum = Vector3.zero;
        int count = 0;
        float lift = 0f;
        foreach (Transform bone in anim.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name != "Hip" && bone.name != "Spine_2" && bone.name != "L_Foot" && bone.name != "R_Foot")
            {
                continue;
            }

            Vector3 origin = bone.position + Vector3.up * 0.35f;
            int hits = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 0.7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit support = default;
            float nearest = float.MaxValue;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.collider == null || hit.distance >= nearest || hit.normal.y < 0.72f
                    || NavigationCollisionCache.Enemy(hit.collider) != null || NavigationCollisionCache.Player(hit.collider) != null
                    || IsNavigationObstacle(hit.collider))
                {
                    continue;
                }

                support = hit;
                nearest = hit.distance;
            }

            if (support.collider != null)
            {
                count++;
                normalSum += support.normal;
                lift = Mathf.Max(lift, support.point.y - bone.position.y + 0.045f);
            }
        }

        if (count >= 2)
        {
            // 斜面上的尸体跟随地表倾斜，小幅抬高已压入地面的姿势。
            Quaternion slope = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normalSum.normalized), 18f);
            corpseSupportApplied = motionExecutor.CommitActionPose(transform.position + Vector3.up * Mathf.Clamp(lift, 0f, 0.16f), slope * transform.rotation);
        }
    }

    private void ReconcileStandingPursuitFacingAfterAnimation()
    {
        if (CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion || CurrentState != VillagerState.Chase || !ShouldChasePlayer || agent == null || !agent.enabled || agent.isStopped)
        {
            return;
        }

        // 普通更新已经为本帧冻结了追击方向。动画结束后不能再直接读取
        // 原始寻路拐点，否则一个刚完成的重算会在同一帧重新覆盖平滑方向。
        // pathDirection（路径方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数、VillagerController.ReconcileStandingPursuitFacingAfterAnimation()。
Vector3 pathDirection;
        if (standingPathDirectionFrame == Time.frameCount)
        {
            pathDirection = standingPathDirectionForFrame;
        }
        else
        {
            pathDirection = lastStableStandingMoveDirection;
        }

        pathDirection.y = 0f;
        if (pathDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        pathDirection.Normalize();
        if (standingDirectPursuitForFrame && lastStableAvoidanceDirection.sqrMagnitude <= 0.0001f)
        {
            // 尚未产生避让方向时，沿本帧追击方向恢复朝向。
            // 不能沿用上一帧的同伴避让方向，否则动画完成后仍会把
            // 敌人重新扭向左右，形成看似在搜寻目标的晃动。
            RotateTowardsDirection(pathDirection, ResolveStandingPursuitFacingTurnRate());
            return;
        }

        // facingDirection（朝向方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数、VillagerController.ReconcileStandingPursuitFacingAfterAnimation()。
Vector3 facingDirection = lastStableAvoidanceDirection;
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f || Vector3.Dot(facingDirection.normalized, pathDirection) < ResolveLocalAvoidanceMinimumForwardAlignment())
        {
            facingDirection = pathDirection;
        }

        RotateTowardsDirection(facingDirection, ResolveStandingPursuitFacingTurnRate());
    }

    /// <summary>UpdateChaseExecution 的业务处理入口；本方法直接依赖 VillagerController.MaintainIncompletePursuitArrival()、VillagerController.RefreshChaseDestination()、VillagerController.UpdateCombatFacing()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） VillagerController.MaintainIncompletePursuitArrival()、VillagerController.RefreshChaseDestination()、VillagerController.UpdateCombatFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateChaseExecution()
    {
        if (CurrentState != VillagerState.Chase || !ShouldChasePlayer || agent == null || !agent.enabled)
        {
            return;
        }

        // 行为树是追击和近身占位的唯一决策者；控制器只在已进入追击后
        // 维护目标位置，避免同一帧重新进入追击并覆盖占位或攻击状态。
        RefreshChaseDestination();
        if (HasIncompletePursuitArrival)
        {
            // 抵达的只是投影节点，不是里昂附近。保持追击状态并交给低频
            // 重投影恢复，禁止把它当作正常的近身待机。
            MaintainIncompletePursuitArrival();
            return;
        }

        // 实际抵达后再转向目标，楼梯途中保持路径朝向。
        if (agent.CurrentPathState == PathAgentState.Arrived)
        {
            UpdateCombatFacing();
        }
    }

    /// <summary>RefreshChaseDestination 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)、HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3)、object.ReferenceEquals(object, object)、PathAgent.SetDestination(Vector3)、PathAgent.SetPursuitDestination(Vector3) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeRefreshChaseDestination()、VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.CompleteTraversalAndResumeChase()、VillagerController.ResolveStandingPathDirectionForCurrentFrame()、VillagerController.TryRecoverChasePath() 等共 9 处；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3)、HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3)、object.ReferenceEquals(object, object)、PathAgent.SetDestination(Vector3)、PathAgent.SetPursuitDestination(Vector3) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool RefreshChaseDestination()
    {
        if (!isActiveAndEnabled || IsDead || actionCoordinator == null || !actionCoordinator.CanReceiveRequest || agent == null || !agent.enabled)
        {
            return false;
        }

        // 翻窗曲线及落地交接期间，普通追击不能重新写入路径或抢回移动。
        if (specialTraversal != null && specialTraversal.IsMovementProtected)
        {
            return false;
        }

        if (HasSiegeBreachOrder)
        {
            return ApproachSiegeDoor();
        }

        if (specialTraversal != null && specialTraversal.TryReadPlatformExitClearance(out Vector3 clearance))
        {
            agent.isStopped = false;
            return agent.SetDestination(clearance);
        }

        if (specialTraversal != null && specialTraversal.TryReadLadderApproach(out Vector3 ladderApproach))
        {
            agent.isStopped = false;
            return agent.SetDestination(ladderApproach);
        }

        // player（玩家）：使用位置：作为 HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3) 的参数、作为 PathAgent.SetDestination(Vector3) 的参数、VillagerController.RefreshChaseDestination()。
        LeonController player = Player;
        EnemyAIManager tacticalManager = EnemyAIManager.Instance;
        if (chainsawBridge != null && player != null && door03TraversalRoute != null &&
            door03TraversalRoute.entranceDoor != null &&
            door03TraversalRoute.entranceDoor.IsSiegeDoorBetween(transform.position, player.transform.position) &&
            TryGetSameLevelPursuitIngressDestination(player.transform.position, out Vector3 siegeApproach))
        {
            if (CurrentState == VillagerState.Attack)
            {
                return false;
            }

            agent.isStopped = false;
            return agent.SetPursuitDestination(siegeApproach);
        }

        if (specialTraversal != null && player != null)
        {
            // playerPosition（玩家位置）：使用位置：作为 EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3) 的参数、作为 EnemyWindowTraversal.TryResolveSpecialPursuitDestination(Vector3, out Vector3) 的参数。
Vector3 playerPosition = player.transform.position;
            if (specialTraversal.TryResolveSamePlatformPursuitDestination(playerPosition, out Vector3 platformDestination))
            {
                // 翻越后先追踪同一平台上的玩家，避免屋内拦截点抢占目的地。
                if (tacticalManager != null)
                {
                    tacticalManager.ReleaseCombatReservation(this);
                }

                agent.isStopped = false;
                return agent.SetDestination(platformDestination);
            }

            if (specialTraversal.TryResolveSpecialPursuitDestination(playerPosition, out Vector3 /* 中文含义：通行目的地。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ traversalDestination))
            {
                // 特殊通行入口已按可达表面精确投影，不能再次投影到导航节点中心。
                // 二次投影叠加抵达半径，会让敌人停在翻窗捕获范围以外。
                agent.isStopped = false;
                return agent.SetDestination(traversalDestination);
            }
        }

        if (!IsCrowdBypassing && tacticalManager != null && tacticalManager.TryGetInterceptionDestination(this, out var interceptionPoint))
        {
            float arrivalRadius = Mathf.Max(agent.waypointReachedRadius + 0.25f, 0.55f);
            if (HorizontalDistance(transform.position, interceptionPoint) <= arrivalRadius)
            {
                agent.isStopped = true;
                UpdateCombatFacing();
                return false;
            }

            if (agent.HasDestination && HasChasePathFailure &&
                HorizontalDistance(agent.Destination, interceptionPoint) < arrivalRadius)
            {
                tacticalManager.RejectInterception(this);
            }
            else
            {
                agent.isStopped = false;
                return agent.SetDestination(interceptionPoint);
            }
        }

        // region（区域）：使用位置：VillagerController.RefreshChaseDestination()。
var region = HouseThreeRegionNavigation.Current;
        if (region != null && player != null && NeedsCombatTraversal() &&
            region.CrossLayerOrIndoorsChase(transform.position, player.transform.position))
        {
            if (pursuitIngressDoor != null)
            {
                ClearPursuitIngress();
            }

            // 正在比较窗口或等待梯位时，只提交当前图上确实可到达的追击目标。
            if (agent.TryResolveReachableDestination(player.transform.position, out Vector3 reachableTarget))
            {
                agent.isStopped = false;
                return agent.SetDestination(reachableTarget);
            }
            return false;
        }

        // chaseTarget（追逐目标）：使用位置：作为 PathAgent.SetPursuitDestination(Vector3) 的参数、VillagerController.RefreshChaseDestination()。
Vector3 chaseTarget = CurrentChaseTarget;
        // passageReference（通道引用）：使用位置：作为 VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3) 的参数、VillagerController.RefreshChaseDestination()。
Vector3 passageReference;
        if (player != null)
        {
            passageReference = player.transform.position;
        }
        else
        {
            passageReference = chaseTarget;
        }

        if (TryGetSameLevelPursuitIngressDestination(passageReference, out Vector3 /* 中文含义：相同级别入口目的地。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sameLevelIngressDestination))
        {
            agent.isStopped = false;
            return agent.SetPursuitDestination(sameLevelIngressDestination);
        }

        if (!IsCrowdBypassing && tacticalManager != null && tacticalManager.ShouldPressAtCloseRange(this) &&
            IsInAttackStartRange && IsStandingAttackSurfaceStable())
        {
            agent.isStopped = true;
            UpdateCombatFacing();
            return false;
        }

        if (!IsCrowdBypassing && tacticalManager != null && tacticalManager.TryReadTacticalMovement(this, out var movement))
        {
            if (movement != EnemyAIManager.TacticalMovement.Blocked && player != null &&
                agent.TryGetCurrentWaypoint(out var waypoint) &&
                !EnemyAIManager.IsSafeTacticalSegment(transform.position, waypoint, player.transform.position,
                    EnemyAIManager.GetMovementExclusionRadius(this)))
            {
                float oldTargetTolerance = agent.arrivedDestinationChangeThreshold;
                bool failedCurrentTarget = (agent.Destination - chaseTarget).sqrMagnitude < oldTargetTolerance * oldTargetTolerance;
                agent.CancelPath();
                if (failedCurrentTarget)
                {
                    tacticalManager.RejectCurrentTacticalRoute(this);
                    chaseTarget = tacticalManager.GetCombatDestination(this, player.transform.position);
                    tacticalManager.TryReadTacticalMovement(this, out movement);
                }
            }

            if (movement == EnemyAIManager.TacticalMovement.Blocked)
            {
                agent.isStopped = true;
                UpdateCombatFacing();
                return false;
            }

            agent.isStopped = false;
            float tolerance = agent.arrivedDestinationChangeThreshold;
            if (agent.HasDestination && (agent.Destination - chaseTarget).sqrMagnitude < tolerance * tolerance)
            {
                if (agent.CurrentPathState == PathAgentState.Cancelled || agent.CurrentPathState == PathAgentState.Failed)
                {
                    return agent.ForceRepath();
                }

                return false;
            }

            return agent.SetDestination(chaseTarget);
        }

        agent.isStopped = false;
        return agent.SetPursuitDestination(chaseTarget);
    }

    /// <summary>
    /// 玩家与敌人处于同一楼层但分居门两侧时，也必须沿用完整的门洞阶段。
    /// 包围槽位只负责进屋后的站位，不能让敌人隔着墙直接朝槽位移动。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.RefreshChaseDestination()；下游（含内部回调体） DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController)、EnemyAIManager.InvalidatePursuitDestination(VillagerController)、Mathf.Abs(float)、Mathf.Sign(float)、object.ReferenceEquals(object, object)、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">中文含义：目标位置。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数、作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数 等共 6 处。</param>
    /// <param name="ingressDestination">中文含义：入口目的地。输出参数，由本入口写入并交给调用方。使用位置：作为 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。</param>
    private bool TryGetSameLevelPursuitIngressDestination(Vector3 targetPosition, out Vector3 ingressDestination)
    {
        ingressDestination = targetPosition;
        // 只要真实区域已经不同，旧的“完成过某扇门”记录就不再具有
        // 路由权威性。由区域拓扑重新选择第一道通道，避免多门建筑中
        // 反向复用旧门或继续追逐另一房间的包围点。
        // hasPursuitRegions（是否具有追击区域集合）：保存 VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
bool hasPursuitRegions = TryResolveCurrentPursuitRegions(targetPosition, out int /* 中文含义：敌人区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enemyRegion, out int /* 中文含义：目标区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ targetRegion);
        if (completedPursuitIngressDoor != null && hasPursuitRegions && enemyRegion != targetRegion)
        {
            completedPursuitIngressDoor = null;
        }

        if (pursuitIngressDoor == null && completedPursuitIngressDoor != null)
        {
            if (!completedPursuitIngressDoor.isActiveAndEnabled)
            {
                completedPursuitIngressDoor = null;
            }
            else
            {
                // completedCenter（已完成中心）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 completedCenter = completedPursuitIngressDoor.PassageCenter;
                // completedNormal（已完成常规）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 completedNormal = completedPursuitIngressDoor.PassageNormal;
                // enemySide（敌人侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
float enemySide = Vector3.Dot(transform.position - completedCenter, completedNormal);
                // targetSide（目标侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数。
float targetSide = Vector3.Dot(targetPosition - completedCenter, completedNormal);
                // remainsInTargetRegion（是否保留内部目标区域）：使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
bool remainsInTargetRegion;
                if (hasPursuitRegions)
                {
                    remainsInTargetRegion = enemyRegion == targetRegion;
                }
                else
                {
                    remainsInTargetRegion = Mathf.Abs(enemySide) <= 0.12f || Mathf.Abs(targetSide) <= 0.12f || Mathf.Sign(enemySide) == Mathf.Sign(targetSide);
                }

                if (remainsInTargetRegion)
                {
                    // combatTarget（战斗目标）：使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数。
Vector3 combatTarget = CurrentChaseTarget;
                    if (DoorController.TryFindPassageTowardDestination(targetPosition, combatTarget, out _))
                    {
                        // 敌人与里昂已经处于门的同一侧，但缓存的包围
                        // 点仍落在另一房间。该点绝不能覆盖当前追击：
                        // 先直追里昂并使管理器下帧重建同侧包围点，避免
                        // 敌人刚出门又被旧室内站位拉回去。
                        // receiverValue84（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
global::EnemyAIManager receiverValue84 = // 敌人与里昂已经处于门的同一侧，但缓存的包围
 // 点仍落在另一房间。该点绝不能覆盖当前追击：
                        // 先直追里昂并使管理器下帧重建同侧包围点，避免
                        // 敌人刚出门又被旧室内站位拉回去。
                        EnemyAIManager.Instance;
                        if (!global::System.Object.ReferenceEquals(receiverValue84, null))
                        {
                            receiverValue84.InvalidatePursuitDestination(this);
                        }

                        ingressDestination = targetPosition;
                        return true;
                    }

                    // 敌人与里昂仍位于刚穿过门的同一侧时，当前建筑入口
                    // 已经完成。此时继续遍历其他门的无限法线平面会把远处
                    // 建筑误判为下一道入口，导致敌人刚进门便转身离开。
                    // 返回当前战斗站位作为有效目标，同时保留入口完成记录，
                    // 避免外层的无入口清理分支在下一帧重新启用错误选门。
                    ingressDestination = CurrentChaseTarget;
                    return true;
                }

                // 里昂重新移动到已完成门的另一侧时，必须先复用刚刚
                // 穿过的同一扇门进行反向通行。不能先清除记录再在全部
                // 门中重新猜测：屋内敌人的目标线可能恰好穿过别的门面，
                // 于是会先朝错误方向折返，表现为里昂已出门而敌人又退回
                // 屋内。三号屋由既有楼梯专用流程处理，不能在此接管。
                if (door03TraversalRoute == null)
                {
                    door03TraversalRoute = HouseThreeTraversalGeometry.Active;
                }

                // leftOperand86（计算前缓存的左侧已完成追击入口门）：从已完成追击入口门读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
global::UnityEngine.Object leftOperand86 = completedPursuitIngressDoor;
                // selectedValue85（分支选出的门03通行路线的入口门）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
global::DoorController selectedValue85;
                if (door03TraversalRoute != null)
                {
                    selectedValue85 = door03TraversalRoute.entranceDoor;
                }
                else
                {
                    selectedValue85 = null;
                }

                if (leftOperand86 != (selectedValue85))
                {
                    pursuitIngressDoor = completedPursuitIngressDoor;
                    completedPursuitIngressDoor = null;
                    pursuitIngressCrossing = false;
                    pursuitIngressPhase = PursuitIngressPhase.Approach;
                    if (enemySide >= 0f)
                    {
                        pursuitIngressSourceSide = 1;
                    }
                    else
                    {
                        pursuitIngressSourceSide = -1;
                    }

                    door03InteriorTurnPassed = false;
                    ResetPursuitIngressHeadProgress();
                    return TryGetPursuitIngressDestination(targetPosition, out ingressDestination);
                }

                completedPursuitIngressDoor = null;
            }
        }

        if (pursuitIngressDoor != null)
        {
            if (!pursuitIngressDoor.isActiveAndEnabled)
            {
                ClearPursuitIngress();
                return false;
            }

            // 同层追击中里昂已经回到敌人所在一侧时，门洞阶段必须立即
            // 失效。不能因为敌人上一帧刚切为穿门就继续把目标锁在门内；
            // 那会造成里昂在屋外而敌人仍被门洞吸附。
            {
                // targetOffset（目标偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
Vector3 targetOffset = targetPosition - pursuitIngressDoor.PassageCenter;
                targetOffset.y = 0f;
                // targetSide（目标侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数。
float targetSide = Vector3.Dot(targetOffset, pursuitIngressDoor.PassageNormal);
                // targetReturnedToSource（是否目标返回的目标来源）：使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
bool targetReturnedToSource = (!hasPursuitRegions || enemyRegion == targetRegion) && Mathf.Abs(targetSide) > 0.12f && Mathf.Sign(targetSide) == pursuitIngressSourceSide;
                if (hasPursuitRegions && PursuitNavigationTopology.TryGetDoorRegions(pursuitIngressDoor, out int /* 中文含义：正向区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ positiveRegion, out int /* 中文含义：负值区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ negativeRegion))
                {
                    // 屋后地面与室内可能处于门平面的同侧，必须比较门的真实来源区域。
                    // 敌人刚跨过门平面也不能取消出门，否则会丢掉出口清场阶段并倒退。
                    // sourceRegion（来源区域）：使用位置：VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
int sourceRegion;
                    if (pursuitIngressSourceSide > 0)
                    {
                        sourceRegion = positiveRegion;
                    }
                    else
                    {
                        sourceRegion = negativeRegion;
                    }

                    targetReturnedToSource = targetRegion == sourceRegion;
                }

                if (targetReturnedToSource)
                {
                    ClearPursuitIngress();
                    return false;
                }
            }

            return TryGetPursuitIngressDestination(targetPosition, out ingressDestination);
        }

        // passageDoor（通道门）：使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数、作为 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
DoorController passageDoor;
        // topologySourceSide（拓扑来源侧边）：使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数、作为 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
int topologySourceSide;
        if (hasPursuitRegions)
        {
            if (enemyRegion == targetRegion || !PursuitNavigationTopology.TryFindFirstPortal(enemyRegion, targetRegion, out passageDoor, out topologySourceSide))
            {
                return false;
            }
        }
        else if (!DoorController.TryFindPassageTowardDestination(transform.position, targetPosition, out passageDoor, out topologySourceSide))
        {
            return false;
        }

        pursuitIngressDoor = passageDoor;
        completedPursuitIngressDoor = null;
        pursuitIngressCrossing = false;
        pursuitIngressPhase = PursuitIngressPhase.Approach;
        door03InteriorTurnPassed = false;
        // targetFromDoor（目标来源门）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
Vector3 targetFromDoor = targetPosition - pursuitIngressDoor.PassageCenter;
        targetFromDoor.y = 0f;
        // destinationSide（目的地侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)。
float destinationSide = Vector3.Dot(targetFromDoor, pursuitIngressDoor.PassageNormal);
        if (Mathf.Abs(destinationSide) <= 0.12f)
        {
            ClearPursuitIngress();
            return false;
        }

        if (topologySourceSide != 0)
        {
            pursuitIngressSourceSide = topologySourceSide;
        }
        else
        {
            if (destinationSide >= 0f)
            {
                pursuitIngressSourceSide = -1;
            }
            else
            {
                pursuitIngressSourceSide = 1;
            }
        }

        return TryGetPursuitIngressDestination(targetPosition, out ingressDestination);
    }

    /// <summary>TryResolveCurrentPursuitRegions 的业务处理入口；本方法直接依赖 EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int)、Mathf.Abs(float)、PursuitNavigationTopology.TryGetRegion(Vector3, out int)、VillagerController.HorizontalDistance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RequiresNavigationTraversalToPlayer()、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int)、Mathf.Abs(float)、PursuitNavigationTopology.TryGetRegion(Vector3, out int)、VillagerController.HorizontalDistance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">中文含义：目标位置。使用位置：作为 Mathf.Abs(float) 的参数、作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数。</param>
    /// <param name="enemyRegion">中文含义：敌人区域。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数、VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)。</param>
    /// <param name="targetRegion">中文含义：目标区域。输出参数，由本入口写入并交给调用方。使用位置：作为 EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int) 的参数、作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数、VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)。</param>
    private bool TryResolveCurrentPursuitRegions(Vector3 targetPosition, out int enemyRegion, out int targetRegion)
    {
        enemyRegion = -1;
        targetRegion = -1;
        if (!PursuitNavigationTopology.TryGetRegion(transform.position, out enemyRegion))
        {
            return false;
        }

        // player（玩家）：使用位置：作为 Mathf.Abs(float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)。
LeonController player = Player;
        // manager（当前敌人智能管理器）：使用位置：VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)。
EnemyAIManager manager = EnemyAIManager.Instance;
        // targetsPlayer（是否目标列表玩家）：使用位置：VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)。
bool targetsPlayer = player != null && HorizontalDistance(targetPosition, player.transform.position) <= 0.1f && Mathf.Abs(targetPosition.y - player.transform.position.y) <= 0.1f;
        if (targetsPlayer && manager != null && manager.TryGetCurrentPlayerPursuitRegion(out targetRegion))
        {
            return true;
        }

        return PursuitNavigationTopology.TryGetRegion(targetPosition, out targetRegion);
    }

    /// <summary>TryGetPursuitIngressDestination 的业务处理入口；本方法直接依赖 DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)、DoorController.TryOpenForAI(Transform)、DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)、HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Max(int, int) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)；下游（含内部回调体） DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)、DoorController.TryOpenForAI(Transform)、DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext)、HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Max(int, int) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stairApproachDestination">中文含义：楼梯接近目的地。使用位置：作为 DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</param>
    /// <param name="ingressDestination">中文含义：入口目的地。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</param>
    private bool TryGetPursuitIngressDestination(Vector3 stairApproachDestination, out Vector3 ingressDestination)
    {
        ingressDestination = stairApproachDestination;
        if (door03TraversalRoute == null)
        {
            door03TraversalRoute = HouseThreeTraversalGeometry.Active;
        }

        // targetsDoor03StairRoute（是否目标列表门03楼梯路线）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool targetsDoor03StairRoute = door03TraversalRoute != null && door03TraversalRoute.IsReady && HorizontalDistance(stairApproachDestination, door03TraversalRoute.LowerEntry) <= 1.25f;
        // holdsDoor03Entrance（是否持有门03入口）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool holdsDoor03Entrance = door03TraversalRoute != null && door03TraversalRoute.IsReady && pursuitIngressDoor == door03TraversalRoute.entranceDoor;
        // usesDoor03Route（是否使用次数门03路线）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool usesDoor03Route = targetsDoor03StairRoute || holdsDoor03Entrance && pursuitIngressSourceSide == ResolveDoor03ExteriorSourceSide(door03TraversalRoute);
        if (usesDoor03Route && door03TraversalRoute.IsOnInteriorSide(transform.position, 0.16f))
        {
            // 仅“已进入门洞穿越阶段后到达门内侧”才算真实进屋。不能只以
            // 当前位于门内侧判定，否则初始就在室内或被路径投影到门内的
            // 敌人会抢占首个进屋者诊断。
            // completedDoorCrossing（是否已完成门跨越）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool completedDoorCrossing = pursuitIngressDoor == door03TraversalRoute.entranceDoor && pursuitIngressCrossing;
            if (completedDoorCrossing)
            {
                UnregisterPursuitIngressWaiter();
            }

            completedPursuitIngressDoor = door03TraversalRoute.entranceDoor;
            pursuitIngressDoor = null;
            pursuitIngressSourceSide = 0;
            pursuitIngressCrossing = false;
            pursuitIngressPhase = PursuitIngressPhase.None;
            return false;
        }

        if (pursuitIngressDoor == null || !pursuitIngressDoor.isActiveAndEnabled)
        {
            // 楼梯入口属于固定建筑，先按入口反查唯一最近的门。首次跨层
            // 追击一律从该门的外侧进入，不能让出生在屋后或路口的敌人
            // 因与入口恰好落在同一法线侧而绕过进门阶段。
            if (usesDoor03Route)
            {
                pursuitIngressDoor = door03TraversalRoute.entranceDoor;
                pursuitIngressSourceSide = -1;
                pursuitIngressPhase = PursuitIngressPhase.Approach;
                door03InteriorTurnPassed = false;
            }
            else
            {
                if (!DoorController.TryFindNearestPassageToDestination(stairApproachDestination, out pursuitIngressDoor))
                {
                    return false;
                }

                pursuitIngressPhase = PursuitIngressPhase.Approach;
            }

            if (completedPursuitIngressDoor == pursuitIngressDoor)
            {
                UnregisterPursuitIngressWaiter();
                pursuitIngressDoor = null;
                pursuitIngressPhase = PursuitIngressPhase.None;
                return false;
            }

            completedPursuitIngressDoor = null;
            // passageNormal（通道常规）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 passageNormal;
            if (usesDoor03Route)
            {
                passageNormal = door03TraversalRoute.InteriorGate - door03TraversalRoute.DoorGate;
            }
            else
            {
                passageNormal = pursuitIngressDoor.PassageNormal;
            }

            passageNormal.y = 0f;
            if (passageNormal.sqrMagnitude <= 0.0001f)
            {
                ClearPursuitIngress();
                return false;
            }

            passageNormal.Normalize();
            if (!usesDoor03Route)
            {
                // destinationOffset（目的地偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 destinationOffset = stairApproachDestination - pursuitIngressDoor.PassageCenter;
                destinationOffset.y = 0f;
                // destinationSide（目的地侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float destinationSide = Vector3.Dot(destinationOffset, passageNormal);
                if (Mathf.Abs(destinationSide) <= 0.12f)
                {
                    ClearPursuitIngress();
                    return false;
                }

                if (destinationSide >= 0f)
                {
                    pursuitIngressSourceSide = -1;
                }
                else
                {
                    pursuitIngressSourceSide = 1;
                }
            }

            pursuitIngressCrossing = false;
            pursuitIngressPhase = PursuitIngressPhase.Approach;
        }

        // center（中心）：使用位置：作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 center;
        if (usesDoor03Route)
        {
            center = door03TraversalRoute.DoorGate;
        }
        else
        {
            center = pursuitIngressDoor.PassageCenter;
        }

        // normal（表面法线）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 normal;
        if (usesDoor03Route)
        {
            normal = door03TraversalRoute.InteriorGate - center;
        }
        else
        {
            normal = pursuitIngressDoor.PassageNormal;
        }

        normal.y = 0f;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            ClearPursuitIngress();
            return false;
        }

        normal.Normalize();
        // currentOffset（当前偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 currentOffset = transform.position - center;
        currentOffset.y = 0f;
        // currentSide（当前侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float currentSide = Vector3.Dot(currentOffset, normal);
        // distanceToDoor（距离目标门）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float distanceToDoor = currentOffset.magnitude;
        // passageTangent（通道切线）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
Vector3 passageTangent;
        if (usesDoor03Route)
        {
            passageTangent = new Vector3(-normal.z, 0f, normal.x);
        }
        else
        {
            passageTangent = pursuitIngressDoor.PassageTangent;
        }

        // lateralDistance（侧向距离）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float lateralDistance = Mathf.Abs(Vector3.Dot(currentOffset, passageTangent));
        // approachDistance（接近距离）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float approachDistance;
        if (usesDoor03Route)
        {
            approachDistance = HorizontalDistance(center, door03TraversalRoute.ExteriorGate);
        }
        else
        {
            approachDistance = Mathf.Max(0.35f, doorOpenProbeDistance * 0.65f);
        }

        // crossingDistance（跨越距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float crossingDistance = Mathf.Max(approachDistance + 0.2f, doorOpenProbeRadius);
        // bodyRadius（身体半径）：保存 VillagerController.ResolveMovementHazardRadius() 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float bodyRadius = ResolveMovementHazardRadius();
        // passageHalfWidth（通道一半宽度）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float passageHalfWidth;
        if (usesDoor03Route)
        {
            passageHalfWidth = door03TraversalRoute.entranceHalfWidth;
        }
        else
        {
            passageHalfWidth = pursuitIngressDoor.PassageHalfWidth;
        }

        // safePassageHalfWidth（安全通道一半宽度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float safePassageHalfWidth = Mathf.Max(0.08f, passageHalfWidth - bodyRadius - Mathf.Max(0.01f, movementCollisionSkin));
        // ingressQueue（入口队列）：保存 VillagerController.RegisterPursuitIngressWaiter(DoorController) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(int, int) 的参数、作为 VillagerController.TryPromoteBlockingPursuitIngressWaiter(ReadOnlyCollection<VillagerController>) 的参数。
System.Collections.ObjectModel.ReadOnlyCollection<VillagerController> ingressQueue = RegisterPursuitIngressWaiter(pursuitIngressDoor);
        // queueIndex（队列索引）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
int queueIndex = Mathf.Max(0, ingressQueue.IndexOf(this));
        // isQueueHead（是否队列头部）：使用位置：作为 VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool isQueueHead = queueIndex == 0;
        // queueSeparation（队列分离量）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float queueSeparation;
        if (usesDoor03Route)
        {
            queueSeparation = Mathf.Max(door03TraversalRoute.entranceQueueSpacing, bodyRadius * 2f + 0.18f);
        }
        else
        {
            queueSeparation = Mathf.Max(0.75f, bodyRadius * 2f + 0.18f);
        }

        if (!pursuitIngressCrossing)
        {
            if (isQueueHead)
            {
                pursuitIngressPhase = PursuitIngressPhase.Approach;
            }
            else
            {
                pursuitIngressPhase = PursuitIngressPhase.WaitingForPassage;
            }

            if (usesDoor03Route)
            {
                ingressDestination = door03TraversalRoute.ExteriorGate - normal * (queueIndex * queueSeparation);
            }
            else
            {
                ingressDestination = center + normal * pursuitIngressSourceSide * (approachDistance + queueIndex * queueSeparation);
            }

            if (isQueueHead && distanceToDoor <= crossingDistance && !pursuitIngressDoor.IsOpen)
            {
                if (pursuitIngressDoor.IsSiegeLatched && chainsawBridge != null && combatModule.CanAttack())
                {
                    actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, "电锯破门");
                }
                else
                {
                    pursuitIngressDoor.TryOpenForAI(transform);
                }
            }

            // sourceDepth（来源深度）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float sourceDepth = currentSide * pursuitIngressSourceSide;
            // isAlignedWithPassage（是否已对齐配合通道）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool isAlignedWithPassage = lateralDistance <= safePassageHalfWidth && sourceDepth >= -0.08f && sourceDepth <= approachDistance + 0.12f;
            // headProgressTimedOut（是否头部进度定时的输出）：保存 VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool headProgressTimedOut = UpdatePursuitIngressHeadProgress(pursuitIngressDoor, isQueueHead);
            if (!usesDoor03Route && headProgressTimedOut && TryPromoteBlockingPursuitIngressWaiter(ingressQueue))
            {
                queueIndex = Mathf.Max(0, ingressQueue.IndexOf(this));
                isQueueHead = queueIndex == 0;
                if (isQueueHead)
                {
                    pursuitIngressPhase = PursuitIngressPhase.Approach;
                }
                else
                {
                    pursuitIngressPhase = PursuitIngressPhase.WaitingForPassage;
                }

                ingressDestination = center + normal * pursuitIngressSourceSide * (approachDistance + queueIndex * queueSeparation);
                headProgressTimedOut = false;
            }

            // isInsideRecoveryCapture（是否内部恢复捕获）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool isInsideRecoveryCapture = lateralDistance <= Mathf.Max(passageHalfWidth, safePassageHalfWidth + 0.2f) && sourceDepth >= -0.16f && sourceDepth <= approachDistance + bodyRadius + 0.2f;
            if (isQueueHead && (isAlignedWithPassage || headProgressTimedOut && isInsideRecoveryCapture) && IsPursuitIngressDirectionAvailable(pursuitIngressDoor, pursuitIngressSourceSide) && pursuitIngressDoor.IsPassageReady)
            {
                // doorwayRequest（门洞请求）：保存 DoorwayTraversalOccupancy.RequestTraversal(VillagerController, int, RequestContext) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
var doorwayRequest = pursuitIngressDoor.TraversalOccupancy.RequestTraversal(this, pursuitIngressSourceSide, new GameArchitecture.RequestContext("追击门洞通行", actionCoordinator.Identity, pursuitIngressDoor.TraversalOccupancy.TraversalIdentity, actionCoordinator.CurrentHandle.Id));
                if (!doorwayRequest.HasAccept)
                {
                    return false;
                }

                doorwayTraversalToken = doorwayRequest.Token;
                pursuitIngressCrossing = true;
                pursuitIngressPhase = PursuitIngressPhase.Crossing;
                ResetPursuitIngressHeadProgress();
                if (usesDoor03Route)
                {
                    ingressDestination = door03TraversalRoute.InteriorGate;
                }
                else
                {
                    ingressDestination = center - normal * pursuitIngressSourceSide * Mathf.Max(0.45f, approachDistance + 0.2f);
                }
            }

            if (!isQueueHead && !usesDoor03Route)
            {
                Vector3 laneSide = Vector3.Cross(Vector3.up, normal);
                if (queueIndex % 2 == 0)
                {
                    laneSide = -laneSide;
                }

                Vector3 offsetPoint = ingressDestination + laneSide * Mathf.Min(0.5f, bodyRadius + 0.1f);
                if (TryResolveReachableNavigationPoint(offsetPoint, out var staggeredPoint) &&
                    HorizontalDistance(offsetPoint, staggeredPoint) < 0.25f &&
                    Mathf.Abs(staggeredPoint.y - ingressDestination.y) < 0.35f &&
                    Vector3.Dot(staggeredPoint - center, normal) * pursuitIngressSourceSide > approachDistance &&
                    CanMoveDirectlyToNavigationPoint(staggeredPoint))
                {
                    ingressDestination = staggeredPoint;
                }
            }

            return true;
        }

        // exitDistance（退出距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float exitDistance = Mathf.Max(0.45f, approachDistance + 0.2f);
        if (usesDoor03Route)
        {
            ingressDestination = door03TraversalRoute.InteriorGate;
        }
        else
        {
            ingressDestination = center - normal * pursuitIngressSourceSide * exitDistance;
        }

        // passedEntrance（是否通过的入口）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool passedEntrance;
        if (usesDoor03Route)
        {
            passedEntrance = door03TraversalRoute.IsOnInteriorSide(transform.position, 0.16f);
        }
        else
        {
            passedEntrance = currentSide * pursuitIngressSourceSide < -0.2f;
        }

        if (passedEntrance)
        {
            pursuitIngressPhase = PursuitIngressPhase.ClearingEntrance;
        }

        // 普通平层门洞的队首若已取得穿门权却长期没有实际位移，说明
        // 门叶、门框或门内的人体碰撞正在阻塞它。此时不能永久持有队首，
        // 应退回外侧重新接近；有后续敌人时同时把自己让到队尾，避免整队
        // 被一个卡住的敌人锁死。三号屋入口仍沿用既有专用流程，确保
        // 已验证的进屋上楼与下楼分支不改变。
        // crossingTimedOut（是否跨越定时的输出）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
bool crossingTimedOut = !usesDoor03Route && !passedEntrance && UpdatePursuitIngressHeadProgress(pursuitIngressDoor, isQueueHead);
        if (crossingTimedOut)
        {
            pursuitIngressCrossing = false;
            pursuitIngressPhase = PursuitIngressPhase.Approach;
            YieldPursuitIngressQueueHead(pursuitIngressDoor);
            queueIndex = Mathf.Max(0, ingressQueue.IndexOf(this));
            ingressDestination = center + normal * pursuitIngressSourceSide * (approachDistance + queueIndex * queueSeparation);
            ResetPursuitIngressHeadProgress();
            return true;
        }

        // interiorDepth（室内深度）：使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float interiorDepth = -currentSide * pursuitIngressSourceSide;
        // releaseDepth（释放深度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。
float releaseDepth = Mathf.Max(Mathf.Max(0.2f, pursuitIngressReleaseDepth), bodyRadius + movementCollisionSkin);
        if (passedEntrance && interiorDepth >= releaseDepth && lateralDistance <= passageHalfWidth + bodyRadius)
        {
            UnregisterPursuitIngressWaiter();
            completedPursuitIngressDoor = pursuitIngressDoor;
            pursuitIngressDoor = null;
            pursuitIngressSourceSide = 0;
            pursuitIngressCrossing = false;
            pursuitIngressPhase = PursuitIngressPhase.None;
            return false;
        }

        return true;
    }

    /// <summary>ClearPursuitIngress 的业务处理入口；本方法直接依赖 VillagerController.ResetPursuitIngressHeadProgress()、VillagerController.UnregisterPursuitIngressWaiter()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.PrepareForSpecialTraversal()、VillagerController.RefreshChaseDestination()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） VillagerController.ResetPursuitIngressHeadProgress()、VillagerController.UnregisterPursuitIngressWaiter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPursuitIngress()
    {
        UnregisterPursuitIngressWaiter();
        pursuitIngressDoor = null;
        completedPursuitIngressDoor = null;
        pursuitIngressSourceSide = 0;
        pursuitIngressCrossing = false;
        pursuitIngressPhase = PursuitIngressPhase.None;
        door03InteriorTurnPassed = false;
        ResetPursuitIngressHeadProgress();
    }

    /// <summary>ResolveDoor03ExteriorSourceSide 的业务处理入口；本方法直接依赖 Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.Valid(VillagerController, int)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="route">中文含义：路线。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)。</param>
    internal static int ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry route)
    {
        if (route == null || !route.IsReady || route.entranceDoor == null)
        {
            return -1;
        }

        // inward（向内）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)。
Vector3 inward = route.InteriorGate - route.DoorGate;
        inward.y = 0f;
        if (inward.sqrMagnitude <= 0.0001f)
        {
            return -1;
        }

        // interiorSide（室内侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)。
float interiorSide = Vector3.Dot(inward.normalized, route.entranceDoor.PassageNormal);
        if (interiorSide >= 0f)
        {
            return -1;
        }
        else
        {
            return 1;
        }
    }

    /// <summary>RegisterPursuitIngressWaiter 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.Register(VillagerController, int)、VillagerController.UnregisterPursuitIngressWaiter()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.Register(VillagerController, int)、VillagerController.UnregisterPursuitIngressWaiter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">中文含义：门。使用位置：VillagerController.RegisterPursuitIngressWaiter(DoorController)。</param>
    private System.Collections.ObjectModel.ReadOnlyCollection<VillagerController> RegisterPursuitIngressWaiter(DoorController door)
    {
        if (hasRegisterDoorway != door)
        {
            UnregisterPursuitIngressWaiter();
        }

        hasRegisterDoorway = door;
        return door.TraversalOccupancy.Register(this, pursuitIngressSourceSide);
    }

    /// <summary>TryPromoteBlockingPursuitIngressWaiter 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Count(string, double)、DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、VillagerController.ResetPursuitIngressHeadProgress()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、VillagerController.ResetPursuitIngressHeadProgress()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waiters">中文含义：等待者集合。</param>
    private bool TryPromoteBlockingPursuitIngressWaiter(System.Collections.ObjectModel.ReadOnlyCollection<VillagerController> waiters)
    {
        // blocker（阻挡者）：使用位置：作为 DoorwayTraversalOccupancy.TryPromoteForwardBlocker(VillagerController, VillagerController, int) 的参数、VillagerController.TryPromoteBlockingPursuitIngressWaiter(ReadOnlyCollection<VillagerController>)。
VillagerController blocker = lastMovementBlockerEnemy;
        if (pursuitIngressDoor == null || !pursuitIngressDoor.TraversalOccupancy.TryPromoteForwardBlocker(this, blocker, pursuitIngressSourceSide))
        {
            return false;
        }

        ResetPursuitIngressHeadProgress();
        blocker.ResetPursuitIngressHeadProgress();
        if (EnemyCombatTimeline.IsRecord(this))
        {
            // argumentValue108（按原顺序缓存的实参当前）：从当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::VillagerController argumentValue108 = this;
            // argumentValue109（按原顺序缓存的实参文字“门洞队首恢复”）：从文字“门洞队首恢复”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
string argumentValue109 = "门洞队首恢复";
            // argumentValue107（按原顺序缓存的实参文字“前方队员”）：从文字“前方队员”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
string argumentValue107 = "前方队员";
            // selectedValue106（分支选出的阻挡者的诊断敌人数量）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.TryPromoteBlockingPursuitIngressWaiter(ReadOnlyCollection<VillagerController>)。
int selectedValue106;
            if (blocker != null)
            {
                selectedValue106 = blocker.diagnosticEnemyNumber;
            }
            else
            {
                selectedValue106 = 0;
            }

            EnemyCombatTimeline.Record(argumentValue108, argumentValue109, CombatDiagnosticFields.Count(argumentValue107, selectedValue106));
        }

        return true;
    }

    /// <summary>YieldPursuitIngressQueueHead 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.QueueFirstYieldRow(VillagerController, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.QueueFirstYieldRow(VillagerController, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">中文含义：门。使用位置：VillagerController.YieldPursuitIngressQueueHead(DoorController)。</param>
    private void YieldPursuitIngressQueueHead(DoorController door)
    {
        if (door != null)
        {
            door.TraversalOccupancy.QueueFirstYieldRow(this, pursuitIngressSourceSide);
        }
    }

    /// <summary>UnregisterPursuitIngressWaiter 的业务处理入口；本方法直接依赖 DoorController.ReleaseTraversal(TraversalToken, ReleaseReason)、DoorwayTraversalOccupancy.QueryToken(VillagerController)、DoorwayTraversalOccupancy.Release(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ClearPursuitIngress()、VillagerController.OnDestroy()、VillagerController.OnDisable()、VillagerController.RegisterPursuitIngressWaiter(DoorController)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorController.ReleaseTraversal(TraversalToken, ReleaseReason)、DoorwayTraversalOccupancy.QueryToken(VillagerController)、DoorwayTraversalOccupancy.Release(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnregisterPursuitIngressWaiter()
    {
        if (hasRegisterDoorway != null)
        {
            // hasRelease（是否具有释放）：保存 DoorController.ReleaseTraversal(TraversalToken, ReleaseReason) 的返回结果，供当前步骤使用。使用位置：VillagerController.UnregisterPursuitIngressWaiter()。
bool hasRelease = hasRegisterDoorway.ReleaseTraversal(doorwayTraversalToken, GameArchitecture.ReleaseReason.Cancel);
            // 旧占用可能已由通路清理，但角色后来又进入了等候队列。
            // 仅撤销没有新占用的等候登记，旧凭证不能误删新占用。
            if (!hasRelease && !hasRegisterDoorway.TraversalOccupancy.QueryToken(this).Valid)
            {
                hasRegisterDoorway.TraversalOccupancy.Release(this);
            }
        }

        doorwayTraversalToken = default;
        hasRegisterDoorway = null;
    }

    /// <summary>CleanupPursuitIngressWaiters 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.CleanupInvalidate()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsPursuitIngressQueueHead()、VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.CleanupInvalidate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupPursuitIngressWaiters()
    {
        if (pursuitIngressDoor != null)
        {
            pursuitIngressDoor.TraversalOccupancy.CleanupInvalidate();
        }
    }

    /// <summary>IsPursuitIngressDirectionAvailable 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.DirectionAvailable(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.DirectionAvailable(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">中文含义：门。使用位置：VillagerController.IsPursuitIngressDirectionAvailable(DoorController, int)。</param>
    /// <param name="sourceSide">中文含义：来源侧边。使用位置：作为 DoorwayTraversalOccupancy.DirectionAvailable(int) 的参数。</param>
    private static bool IsPursuitIngressDirectionAvailable(DoorController door, int sourceSide)
    {
        return door != null && door.TraversalOccupancy.DirectionAvailable(sourceSide);
    }

    /// <summary>
    /// 门洞方向锁只属于正在穿门的敌人，不能属于整条等候队列。
    /// 玩家反向离屋后，旧方向队列可能仍有尚未完成状态清理的成员；
    /// 若继续用“队列非空”维持方向锁，门内的新队首会永久停在门口。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） float.IsNegativeInfinity(float)、Mathf.Max(float, float)、VillagerController.HorizontalDistance(Vector3, Vector3)、VillagerController.ResetPursuitIngressHeadProgress()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">中文含义：门。使用位置：VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。</param>
    /// <param name="isQueueHead">中文含义：是否队列头部。使用位置：VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。</param>
    private bool UpdatePursuitIngressHeadProgress(DoorController door, bool isQueueHead)
    {
        if (!isQueueHead || door == null)
        {
            ResetPursuitIngressHeadProgress();
            return false;
        }

        if (pursuitIngressProgressDoor != door || float.IsNegativeInfinity(pursuitIngressLastProgressTime))
        {
            pursuitIngressProgressDoor = door;
            pursuitIngressProgressPosition = transform.position;
            pursuitIngressLastProgressTime = Time.time;
            return false;
        }

        // requiredProgress（所需进度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)。
float requiredProgress = Mathf.Max(0.02f, chaseProgressDistance);
        if (HorizontalDistance(transform.position, pursuitIngressProgressPosition) >= requiredProgress)
        {
            pursuitIngressProgressPosition = transform.position;
            pursuitIngressLastProgressTime = Time.time;
            return false;
        }

        return Time.time - pursuitIngressLastProgressTime >= Mathf.Max(1f, chaseProgressTimeout);
    }

    /// <summary>ResetPursuitIngressHeadProgress 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ClearPursuitIngress()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryPromoteBlockingPursuitIngressWaiter(ReadOnlyCollection&lt;VillagerController&gt;)、VillagerController.UpdatePursuitIngressHeadProgress(DoorController, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetPursuitIngressHeadProgress()
    {
        pursuitIngressProgressDoor = null;
        pursuitIngressProgressPosition = transform.position;
        pursuitIngressLastProgressTime = float.NegativeInfinity;
    }

    /// <summary>IsPursuitIngressQueueHead 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.ReadQueue(int)、VillagerController.CleanupPursuitIngressWaiters()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） DoorwayTraversalOccupancy.ReadQueue(int)、VillagerController.CleanupPursuitIngressWaiters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsPursuitIngressQueueHead()
    {
        if (pursuitIngressDoor == null)
        {
            return false;
        }

        CleanupPursuitIngressWaiters();
        // waiters（等待者集合）：保存 DoorwayTraversalOccupancy.ReadQueue(int) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsPursuitIngressQueueHead()。
var waiters = pursuitIngressDoor.TraversalOccupancy.ReadQueue(pursuitIngressSourceSide);
        return waiters.Count > 0 && waiters[0] == this;
    }

    /// <summary>MaintainChasePathRecoveryFacing 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ExitCombatOccupancy()、VillagerController.BeginChase(bool)、VillagerController.UpdateCombatFacing()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteChaseRecovery.OnUpdate()、ExecuteCombat.OnUpdate()；下游（含内部回调体） EnemyActionCoordinator.ExitCombatOccupancy()、VillagerController.BeginChase(bool)、VillagerController.UpdateCombatFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void MaintainChasePathRecoveryFacing()
    {
        if (!ShouldChasePlayer || agent == null)
        {
            return;
        }

        actionCoordinator.ExitCombatOccupancy();
        if (CurrentState != VillagerState.Chase)
        {
            BeginChase();
        }

        agent.isStopped = false;
        if (ctx != null)
        {
            ctx.TargetMoveSpeed = 0f;
        }

        UpdateCombatFacing();
    }

    /// <summary>MaintainIncompletePursuitArrival 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ExitCombatOccupancy()、VillagerController.BeginChase(bool)、VillagerController.UpdateCombatFacing()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCombat.OnUpdate()、VillagerController.UpdateChaseExecution()；下游（含内部回调体） EnemyActionCoordinator.ExitCombatOccupancy()、VillagerController.BeginChase(bool)、VillagerController.UpdateCombatFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void MaintainIncompletePursuitArrival()
    {
        if (!ShouldChasePlayer || agent == null)
        {
            return;
        }

        actionCoordinator.ExitCombatOccupancy();
        if (CurrentState != VillagerState.Chase || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion)
        {
            BeginChase();
        }

        agent.isStopped = false;
        if (ctx != null)
        {
            ctx.TargetMoveSpeed = 0f;
        }

        UpdateCombatFacing();
    }

    /// <summary>TryRecoverIncompletePursuitArrival 的业务处理入口；本方法直接依赖 EnemyAIManager.InvalidatePursuitDestination(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)、PathAgent.ForceRepath()、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.RefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateChaseProgressRecovery()；下游（含内部回调体） EnemyAIManager.InvalidatePursuitDestination(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)、PathAgent.ForceRepath()、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.RefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool TryRecoverIncompletePursuitArrival()
    {
        if (!HasIncompletePursuitArrival || agent == null || Time.time < nextIncompletePursuitRepathTime)
        {
            return false;
        }

        nextIncompletePursuitRepathTime = Time.time + Mathf.Max(0.2f, incompletePursuitRepathInterval);
        agent.InvalidatePursuitResolutionCache();
        // receiverValue110（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.TryRecoverIncompletePursuitArrival()。
global::EnemyAIManager receiverValue110 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue110, null))
        {
            receiverValue110.InvalidatePursuitDestination(this);
        }

        // requested（是否已经申请）：保存 VillagerController.RefreshChaseDestination() 的返回结果，供当前步骤使用。使用位置：VillagerController.TryRecoverIncompletePursuitArrival()。
bool requested = RefreshChaseDestination();
        return requested || agent.ForceRepath();
    }

    /// <summary>TryRecoverChasePath 的业务处理入口；本方法直接依赖 EnemyAIManager.InvalidatePursuitDestination(VillagerController)、object.ReferenceEquals(object, object)、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.RefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteChaseRecovery.OnUpdate()、ExecuteCombat.OnUpdate()；下游（含内部回调体） EnemyAIManager.InvalidatePursuitDestination(VillagerController)、object.ReferenceEquals(object, object)、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.RefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ContinueTacticalCombat()
    {
        if (!ShouldChasePlayer || IsNavigationTraversalProtected)
        {
            return;
        }

        if (CurrentState != VillagerState.Chase || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion)
        {
            actionCoordinator.RequestAction(GameArchitecture.ActionKind.Chase, nameof(ContinueTacticalCombat));
        }

        UpdateChaseExecution();
    }

    public bool TryRecoverChasePath()
    {
        if (!ShouldChasePlayer || agent == null || !agent.enabled)
        {
            return false;
        }

        agent.InvalidatePursuitResolutionCache();
        // receiverValue111（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.TryRecoverChasePath()。
global::EnemyAIManager receiverValue111 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue111, null))
        {
            receiverValue111.InvalidatePursuitDestination(this);
        }

        return RefreshChaseDestination();
    }

    /// <summary>EnsureBehaviorTreeRunning 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureBehaviorTreeRunning()
    {
        if (assassinationShowcaseIdle || behaviorTree == null || !behaviorTree.isActiveAndEnabled || behaviorTree.ExecutionStatus != BehaviorDesigner.Runtime.Tasks.TaskStatus.Inactive || (qteParticipant != null && qteParticipant.IsControllingEnemyAI))
        {
            return;
        }

        behaviorTree.EnableBehavior();
    }

    /// <summary>MaintainAssassinationShowcaseIdle 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、VillagerController.BeginIdle()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、VillagerController.BeginIdle()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void MaintainAssassinationShowcaseIdle()
    {
        if (behaviorTree != null && behaviorTree.ExecutionStatus != BehaviorDesigner.Runtime.Tasks.TaskStatus.Inactive)
        {
            behaviorTree.DisableBehavior();
        }

        if (CurrentState != VillagerState.Idle || CurrentMovementMode != EnemyMovementMode.FullyStopped)
        {
            BeginIdle();
        }

        animationPresenter.SetFloatValue(HashMoveX, 0f);
        animationPresenter.SetFloatValue(HashMoveY, 0f);
        animationPresenter.SetBoolean(HashSearchMode, false);
    }

    /// <summary>ResolvePlayer 的业务处理入口；本方法直接依赖 Object.FindFirstObjectByType&lt;LeonController&gt;()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Player.get；下游（含内部回调体） Object.FindFirstObjectByType&lt;LeonController&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private LeonController ResolvePlayer()
    {
        // receiverValue112（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.ResolvePlayer()。
global::EnemyAIManager receiverValue112 = EnemyAIManager.Instance;
        // managerTarget（管理器目标）：使用位置：VillagerController.ResolvePlayer()。
LeonController managerTarget;
        if (!global::System.Object.ReferenceEquals(receiverValue112, null))
        {
            managerTarget = receiverValue112.PlayerTarget;
        }
        else
        {
            managerTarget = default;
        }

        if (managerTarget != null)
        {
            cachedPlayer = managerTarget;
            return cachedPlayer;
        }

        if (cachedPlayer == null && Time.time >= nextPlayerRecoveryTime)
        {
            nextPlayerRecoveryTime = Time.time + 0.5f;
            cachedPlayer = FindFirstObjectByType<LeonController>();
        }

        return cachedPlayer;
    }

    /// <summary>中文含义：生成候选。只读字段，初始化后不由普通写入口替换。使用位置：作为 NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。</summary>
    private readonly List<GraphNode> spawnCandidate = new(128);
    /// <summary>中文含义：是否等待中生成导航恢复。使用位置：VillagerController.RecoverInvalidNavigationSpawn()、VillagerController.Update()。</summary>
    private bool waitingSpawnNavigationRestore;
    /// <summary>RecoverInvalidNavigationSpawn 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、Physics.SyncTransforms()、VillagerController.IsNavigationTorsoClear(Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()、VillagerController.Update()；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationQueryService.RecoverCollection(Vector3, float, List&lt;GraphNode&gt;)、Physics.SyncTransforms()、VillagerController.IsNavigationTorsoClear(Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RecoverInvalidNavigationSpawn()
    {
        if (movementCollider == null || AstarPath.active == null || AstarPath.active.data == null || IsNavigationTorsoClear(transform.position))
        {
            return;
        }

        // nearest（最近）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
NNInfo nearest = AstarPath.active.GetNearest(transform.position, NearestNodeConstraint.Walkable);
        if (nearest.node == null)
        {
            return;
        }

        // origin（原点）：使用位置：作为 NavigationQueryService.RecoverCollection(Vector3, float, List<GraphNode>) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
Vector3 origin = transform.position;
        // argumentValue115（按原顺序缓存的实参2f）：从2f读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue115 = 2f;
        // selectedValue114（分支选出的寻路代理的导航图吸附距离）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
float selectedValue114;
        if (agent != null)
        {
            selectedValue114 = agent.graphSnapDistance;
        }
        else
        {
            selectedValue114 = 3f;
        }

        // maximumDistance（最大距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 NavigationQueryService.RecoverCollection(Vector3, float, List<GraphNode>) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
float maximumDistance = Mathf.Max(argumentValue115, selectedValue114);
        // bestDistanceSqr（最佳距离平方）：使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
float bestDistanceSqr = maximumDistance * maximumDistance;
        // bestPosition（最佳位置）：使用位置：作为 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
Vector3 bestPosition = origin;
        // found（是否已找到）：使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
bool found = false;
        // query（查询）：使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
global::NavigationQueryService query;
        if (EnemyAIManager.HasInstance)
        {
            query = EnemyAIManager.Instance.NavigationQuery;
        }
        else
        {
            query = null;
        }

        if (query == null || !query.RecoverCollection(origin, maximumDistance, spawnCandidate))
        {
            waitingSpawnNavigationRestore = true;
            return;
        }

        // node（节点）：每轮从生成候选取得一个元素，仅代表当前这一项。使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
foreach (var node in spawnCandidate)
        {
            if (node == null || !node.Walkable || node.Area != nearest.node.Area)
            {
                continue;
            }

            // point（点）：使用位置：作为 VillagerController.IsNavigationTorsoClear(Vector3) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
Vector3 point = (Vector3)node.position;
            // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.RecoverInvalidNavigationSpawn()。
Vector3 offset = point - origin;
            if (Mathf.Abs(offset.y) > Mathf.Max(0.8f, groundProbeDistance))
            {
                continue;
            }

            // distanceSqr（距离平方）：使用位置：VillagerController.RecoverInvalidNavigationSpawn()。
float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr || !IsNavigationTorsoClear(point))
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestPosition = point;
            found = true;
        }

        if (!found)
        {
            return;
        }

        motionExecutor.CommitPose(motionExecutor.CurrentToken, bestPosition, transform.rotation, true);
        Physics.SyncTransforms();
        SnapToGround();
    }

    /// <summary>IsNavigationTorsoClear 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Transform.IsChildOf(Transform) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RecoverInvalidNavigationSpawn()；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Abs(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Transform.IsChildOf(Transform) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、VillagerController.IsNavigationTorsoClear(Vector3)。</param>
    private bool IsNavigationTorsoClear(Vector3 position)
    {
        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = transform.lossyScale;
        // radius（半径）：使用位置：作为 Mathf.Max(float, float) 的参数。
float radius = movementCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        // height（高度）：使用位置：作为 Mathf.Max(float, float) 的参数。
float height = movementCollider.height * Mathf.Abs(scale.y);
        // center（中心）：使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数。
Vector3 center = position + Vector3.up * Mathf.Max(radius + 0.25f, height * 0.55f);
        CombatPerformanceCounters.physicsQueryCount++;
        // count（数量）：保存 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsNavigationTorsoClear(Vector3)。
int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.15f, radius * 0.82f), movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.IsNavigationTorsoClear(Vector3)。
for (int i = 0; i < count; i++)
        {
            // other（另一个碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsEnemyStairBridgeCollider(Collider) 的参数 等共 4 处。
Collider other = movementOverlaps[i];
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(other) != null || NavigationCollisionCache.Player(other) != null || DoorController.IsInsideOpenPassage(position, 1.35f))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>RecoverManagerRegistration 的业务处理入口；本方法直接依赖 EnemyAIManager.RegisterEnemy(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyAIManager.RegisterEnemy(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RecoverManagerRegistration()
    {
        if (Time.time < nextManagerRegistrationTime)
        {
            return;
        }

        nextManagerRegistrationTime = Time.time + 0.5f;
        // receiverValue117（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.RecoverManagerRegistration()。
global::EnemyAIManager receiverValue117 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue117, null))
        {
            receiverValue117.RegisterEnemy(this);
        }
    }

    /// <summary>UpdateCrawlFacingBeforeAnimator 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)、Vector3.Normalize()、VillagerController.ResolveCrawlFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)、Vector3.Normalize()、VillagerController.ResolveCrawlFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateCrawlFacingBeforeAnimator()
    {
        if (CurrentMovementMode != EnemyMovementMode.CrawlCodeDriven || CurrentState != VillagerState.Crawl || ctx == null || !ctx.HasCrawlMoveIntent)
        {
            return;
        }

        // direction（方向）：使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数、VillagerController.UpdateCrawlFacingBeforeAnimator()。
Vector3 direction = lastStableCrawlMoveDirection;
        if (direction.sqrMagnitude <= 0.0001f && agent != null && agent.enabled && !agent.isStopped && agent.desiredDirection.sqrMagnitude > 0.0001f)
        {
            direction = agent.desiredDirection;
        }

        if (direction.sqrMagnitude <= 0.0001f && ctx.Player != null)
        {
            direction = ctx.Player.transform.position - transform.position;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        // 首次获得匍匐追击方向时先对齐身体，确保动画不会因进入状态
        // 时的旧朝向切入匍匐后退；之后的目标绕背只按速率平滑转向。
        if (!crawlFacingInitialized)
        {
            motionExecutor.CommitNormalOrientation(Quaternion.LookRotation(direction));
            crawlFacingInitialized = true;
            return;
        }

        RotateTowardsDirection(direction, ResolveCrawlFacingTurnRate());
    }

    /// <summary>UpdateAnimator 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.UpdateAnimator()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyAnimationAdapter.UpdateAnimator()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateAnimator()
    {
        animationPresenter.UpdateAnimator();
    }

    /// <summary>UpdateActualMovementSample 的业务处理入口；本方法直接依赖 Mathf.Exp(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） Mathf.Exp(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateActualMovementSample()
    {
        // currentPosition（当前位置）：使用位置：VillagerController.UpdateActualMovementSample()。
Vector3 currentPosition = transform.position;
        if (!actualMovementSampleInitialized)
        {
            actualMovementSamplePosition = currentPosition;
            actualMovementSampleInitialized = true;
            actualHorizontalMovementSpeed = 0f;
            return;
        }

        // displacement（位移）：使用位置：VillagerController.UpdateActualMovementSample()。
Vector3 displacement = currentPosition - actualMovementSamplePosition;
        displacement.y = 0f;
        // rawSpeed（原始速度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、VillagerController.UpdateActualMovementSample()。
float rawSpeed;
        if (Time.deltaTime > 0.0001f)
        {
            rawSpeed = displacement.magnitude / Time.deltaTime;
        }
        else
        {
            rawSpeed = 0f;
        }

        // response（响应）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float response = 1f - Mathf.Exp(-Mathf.Max(1f, actualMovementSpeedResponse) * Time.deltaTime);
        actualHorizontalMovementSpeed = Mathf.Lerp(actualHorizontalMovementSpeed, rawSpeed, response);
        actualMovementSamplePosition = currentPosition;
    }

    /// <summary>UpdateStandingLocomotionVisibility 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hasMoveIntent">中文含义：是否具有移动意图。使用位置：作为 EnemyAnimationAdapter.UpdateStandingLocomotionVisibility(bool) 的参数。</param>
    private void UpdateStandingLocomotionVisibility(bool hasMoveIntent)
    {
        animationPresenter.UpdateStandingLocomotionVisibility(hasMoveIntent);
    }

    /// <summary>SetMovementDirection 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetMovementDirection(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAnimationAdapter.SetMovementDirection(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="moveX">中文含义：移动横向。使用位置：作为 EnemyAnimationAdapter.SetMovementDirection(float, float) 的参数。</param>
    /// <param name="moveY">中文含义：移动纵向。使用位置：作为 EnemyAnimationAdapter.SetMovementDirection(float, float) 的参数。</param>
    private void SetMovementDirection(float moveX, float moveY)
    {
        animationPresenter.SetMovementDirection(moveX, moveY);
    }

    /// <summary>UpdateHealthAnimationParameter 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.UpdateHealthAnimationParameter()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeUpdateHealthAnimationParameter()、VillagerController.Start()；下游（含内部回调体） EnemyAnimationAdapter.UpdateHealthAnimationParameter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateHealthAnimationParameter()
    {
        animationPresenter.UpdateHealthAnimationParameter();
    }

    /// <summary>SetLODActive 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetEnable(bool)、object.ReferenceEquals(object, object)、PathAgent.CancelPath()、VillagerController.ClearCombatEngagement(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.UpdateEnemyStates()；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetEnable(bool)、object.ReferenceEquals(object, object)、PathAgent.CancelPath()、VillagerController.ClearCombatEngagement(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="active">中文含义：是否生效。使用位置：作为 EnemyAnimationAdapter.SetEnable(bool) 的参数、VillagerController.SetLODActive(bool)。</param>
    public void SetLODActive(bool active)
    {
        if (IsDead && active)
        {
            return;
        }

        if (IsActive == active)
        {
            return;
        }

        IsActive = active;
        if (!active)
        {
            // receiverValue119（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.SetLODActive(bool)。
global::EnemyAIManager receiverValue119 = EnemyAIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue119, null))
            {
                receiverValue119.ReleaseCombatReservation(this);
            }

            ClearCombatEngagement(false);
        }

        if (agent != null)
        {
            if (!active)
            {
                agent.CancelPath();
            }

            agent.enabled = active;
        }

        if (anim != null)
        {
            animationPresenter.SetEnable(active);
        }

        if (behaviorTree != null)
        {
            if (active && !assassinationShowcaseIdle)
            {
                behaviorTree.EnableBehavior();
            }
            else
            {
                behaviorTree.DisableBehavior();
            }
        }
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.TakeDamage(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeDamage(float) 的参数。</param>
    public void TakeDamage(float damage)
    {
        combatModule.TakeDamage(damage);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3) 的参数。</param>
    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        combatModule.TakeDamage(damage, hitPoint);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType) 的参数。</param>
    /// <param name="hitboxType">中文含义：命中盒类型。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType) 的参数。</param>
    public void TakeDamage(float damage, Vector3 hitPoint, HitboxType hitboxType)
    {
        combatModule.TakeDamage(damage, hitPoint, hitboxType);
    }

    /// <summary>TakeDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)；下游（含内部回调体） EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    /// <param name="hitboxType">中文含义：命中盒类型。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    /// <param name="hitNormal">中文含义：命中表面法线。使用位置：作为 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3) 的参数。</param>
    public void TakeDamage(float damage, Vector3 hitPoint, HitboxType hitboxType, Vector3 hitNormal)
    {
        combatModule.TakeDamage(damage, hitPoint, hitboxType, hitNormal);
    }

    /// <summary>TakeBodyTechniqueDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)；下游（含内部回调体） EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.TakeBodyTechniqueDamage(float, Vector3) 的参数。</param>
    public void TakeBodyTechniqueDamage(float damage, Vector3 hitPoint)
    {
        combatModule.TakeBodyTechniqueDamage(damage, hitPoint);
    }

    /// <summary>BeginExternalKnockback 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、Vector3.Normalize()、VillagerController.DriveExternalKnockback(Vector3, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ApplyBodyTechniqueKnockback(Transform)、VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform)；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、Vector3.Normalize()、VillagerController.DriveExternalKnockback(Vector3, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 VillagerController.DriveExternalKnockback(Vector3, float, float) 的参数、VillagerController.BeginExternalKnockback(Vector3, float, float)。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 VillagerController.DriveExternalKnockback(Vector3, float, float) 的参数、VillagerController.BeginExternalKnockback(Vector3, float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 VillagerController.DriveExternalKnockback(Vector3, float, float) 的参数、VillagerController.BeginExternalKnockback(Vector3, float, float)。</param>
    public void BeginExternalKnockback(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0f || duration <= 0f)
        {
            return;
        }

        direction.Normalize();
        if (externalKnockbackRoutine != null)
        {
            StopCoroutine(externalKnockbackRoutine);
        }

        externalKnockbackRoutine = StartCoroutine(DriveExternalKnockback(direction, distance, duration));
    }

    /// <summary>DriveExternalKnockback 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、VillagerController.MaintainGroundContact()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.StopAndClearMovement()、VillagerController.TryExecuteStairActionDisplacement(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.BeginExternalKnockback(Vector3, float, float)；下游（含内部回调体） Mathf.Clamp01(float)、VillagerController.MaintainGroundContact()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.StopAndClearMovement()、VillagerController.TryExecuteStairActionDisplacement(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。</param>
    /// <param name="distance">中文含义：距离。使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、VillagerController.DriveExternalKnockback(Vector3, float, float)。</param>
    private IEnumerator DriveExternalKnockback(Vector3 direction, float distance, float duration)
    {
        StopAndClearMovement();
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、VillagerController.DriveExternalKnockback(Vector3, float, float)。
float elapsed = 0f;
        // previousDistance（前一次距离）：使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。
float previousDistance = 0f;
        while (elapsed < duration)
        {
            if (IsQTEControlled || IsNavigationTraversalProtected)
            {
                ClearCombatGroundMotion();
                break;
            }
            elapsed += Time.deltaTime;
            // normalized（归一化）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。
float normalized = Mathf.Clamp01(elapsed / duration);
            // eased（缓动后的）：使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。
float eased = 1f - (1f - normalized) * (1f - normalized);
            // currentDistance（当前距离）：使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。
float currentDistance = distance * eased;
            // deltaDistance（增量距离）：使用位置：VillagerController.DriveExternalKnockback(Vector3, float, float)。
float deltaDistance = currentDistance - previousDistance;
            previousDistance = currentDistance;
            if (deltaDistance > 0f)
            {
                // displacement（位移）：使用位置：作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数、作为 VillagerController.TryExecuteStairActionDisplacement(Vector3) 的参数。
Vector3 displacement = direction * deltaDistance;
                MoveWithCollision(displacement, true, false);
            }
            CommitCombatGroundMotion(transform.rotation, 0f, out _);

            yield return null;
        }

        externalKnockbackRoutine = null;
    }

    /// <summary>TakeHeavyFrontDamage 的业务处理入口；本方法直接依赖 EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitPairedActionHeavyStrike(VillagerController, float, Vector3)、EnemyCombatModule.ApplyDeadChainsawPulse()、EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float)、VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform)；下游（含内部回调体） EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.TakeHeavyFrontDamage(float, Vector3) 的参数。</param>
    public void TakeHeavyFrontDamage(float damage, Vector3 hitPoint)
    {
        combatModule.TakeHeavyFrontDamage(damage, hitPoint);
    }

    /// <summary>ResolveStaggerLevel 的业务处理入口；本方法直接依赖 EnemyCombatModule.ResolveStaggerLevel(HitboxType, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ResolveStaggerLevel(HitboxType, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitboxType">中文含义：命中盒类型。使用位置：作为 EnemyCombatModule.ResolveStaggerLevel(HitboxType, bool) 的参数。</param>
    /// <param name="forcedHeavyStagger">中文含义：是否强制重型硬直。使用位置：作为 EnemyCombatModule.ResolveStaggerLevel(HitboxType, bool) 的参数。</param>
    private int ResolveStaggerLevel(HitboxType hitboxType, bool forcedHeavyStagger)
    {
        return combatModule.ResolveStaggerLevel(hitboxType, forcedHeavyStagger);
    }

    /// <summary>ShouldTriggerCriticalStagger 的业务处理入口；本方法直接依赖 EnemyCombatModule.ShouldTriggerCriticalStagger(HitboxType, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ShouldTriggerCriticalStagger(HitboxType, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitboxType">中文含义：命中盒类型。使用位置：作为 EnemyCombatModule.ShouldTriggerCriticalStagger(HitboxType, float) 的参数。</param>
    /// <param name="damage">中文含义：伤害。使用位置：作为 EnemyCombatModule.ShouldTriggerCriticalStagger(HitboxType, float) 的参数。</param>
    private bool ShouldTriggerCriticalStagger(HitboxType hitboxType, float damage)
    {
        return combatModule.ShouldTriggerCriticalStagger(hitboxType, damage);
    }

    /// <summary>UsesSawmanHitToughness 的业务处理入口；本方法直接依赖 EnemyCombatModule.UsesSawmanHitToughness()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.UsesSawmanHitToughness()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool UsesSawmanHitToughness()
    {
        return combatModule.UsesSawmanHitToughness();
    }

    /// <summary>ResolveHitPoiseResistanceMultiplier 的业务处理入口；本方法直接依赖 EnemyCombatModule.ResolveHitPoiseResistanceMultiplier()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ResolveHitPoiseResistanceMultiplier()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveHitPoiseResistanceMultiplier()
    {
        return combatModule.ResolveHitPoiseResistanceMultiplier();
    }

    /// <summary>ResolveHitReactionThreshold 的业务处理入口；本方法直接依赖 EnemyCombatModule.ResolveHitReactionThreshold()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ResolveHitReactionThreshold()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveHitReactionThreshold()
    {
        return combatModule.ResolveHitReactionThreshold();
    }

    /// <summary>ResolveStaggerDirection 的业务处理入口；本方法直接依赖 EnemyCombatModule.ResolveStaggerDirection(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ResolveStaggerDirection(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitNormal">中文含义：命中表面法线。使用位置：作为 EnemyCombatModule.ResolveStaggerDirection(Vector3, Vector3) 的参数。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.ResolveStaggerDirection(Vector3, Vector3) 的参数。</param>
    private int ResolveStaggerDirection(Vector3 hitNormal, Vector3 hitPoint)
    {
        return combatModule.ResolveStaggerDirection(hitNormal, hitPoint);
    }

    /// <summary>CanSeePlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.CanSeePlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeCanSeePlayer()、VillagerController.CanEvaluateStandingQTEAction.get；下游（含内部回调体） EnemyPerceptionModule.CanSeePlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanSeePlayer()
    {
        return perceptionModule.CanSeePlayer();
    }

    /// <summary>EvaluatePlayerVisibility 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.EvaluatePlayerVisibility()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyPerceptionModule.EvaluatePlayerVisibility()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool EvaluatePlayerVisibility()
    {
        return perceptionModule.EvaluatePlayerVisibility();
    }

    /// <summary>CanAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.CanAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttackNow.OnUpdate()、ExecuteChaseRecovery.OnUpdate()、ExecuteCombat.OnUpdate()；下游（含内部回调体） EnemyCombatModule.CanAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanAttack()
    {
        return combatModule.CanAttack();
    }

    /// <summary>CanCrawlAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.CanCrawlAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanCrawlAttackNow.OnUpdate()、ExecuteCrawl.OnUpdate()；下游（含内部回调体） EnemyCombatModule.CanCrawlAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanCrawlAttack()
    {
        return combatModule.CanCrawlAttack();
    }

    /// <summary>SetObservableState 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.SetObservableState(VillagerState, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeSetObservableState(VillagerState, string, int)、VillagerController.CompleteTraversalAndResumeChase()；下游（含内部回调体） EnemyActionCoordinator.SetObservableState(VillagerState, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前敌人状态。使用位置：作为 EnemyActionCoordinator.SetObservableState(VillagerState, string, int) 的参数。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 EnemyActionCoordinator.SetObservableState(VillagerState, string, int) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 EnemyActionCoordinator.SetObservableState(VillagerState, string, int) 的参数。</param>
    private void SetObservableState(VillagerState state, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        actionCoordinator.SetObservableState(state, source, line);
    }

    /// <summary>PrepareForSpecialTraversal 的业务处理入口；本方法直接依赖 EnemyCombatModule.AbandonAttackRestore()、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)、VillagerController.ClearPursuitIngress()、VillagerController.StopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.BeginTraversalState()；下游（含内部回调体） EnemyCombatModule.AbandonAttackRestore()、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)、VillagerController.ClearPursuitIngress()、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PrepareForSpecialTraversal()
    {
        ClearCombatGroundMotion();
        // 特殊动作离开普通行走，立即释放入口让行登记与门洞占用。
        // receiverValue120（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.PrepareForSpecialTraversal()。
global::HouseThreeRegionNavigation receiverValue120 = // 特殊动作离开普通行走，立即释放入口让行登记与门洞占用。
 HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue120, null))
        {
            receiverValue120.ReleaseCharacter(this);
        }

        ClearPursuitIngress();
        // 旧攻击的结束回调不得恢复跨窗前的世界坐标。
        combatModule.AbandonAttackRestore();
        StopAndClearMovement();
    }

    /// <summary>CompleteTraversalAndResumeChase 的业务处理入口；本方法直接依赖 VillagerController.RefreshChaseDestination()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.SetObservableState(VillagerState, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.CompleteTraversal()、EnemyWindowTraversal.LateUpdate()；下游（含内部回调体） VillagerController.RefreshChaseDestination()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.SetObservableState(VillagerState, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteTraversalAndResumeChase()
    {
        if (IsDead)
        {
            return;
        }

        if (IsQTEControlled || pendingHit || pendingCrawl || pendingStandUp)
        {
            SetMovementMode(EnemyMovementMode.FullyStopped);
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
            }

            return;
        }

        if (bellWithdrawing)
        {
            bellRouteStarted = false;
            bellLooking = false;
            TryStartBellRoute();
            return;
        }

        SetObservableState(VillagerState.Chase);
        SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        RefreshChaseDestination();
    }

    /// <summary>StopAndClearMovement 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Text(string, string)、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.RequestStopMovement()、ExecuteChainsawIntroTaunt.OnEnd()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawStart.OnEnd()、ExecuteChainsawStart.OnStart() 等共 14 处；下游（含内部回调体） CombatDiagnosticFields.Text(string, string)、EnemyAnimationAdapter.ResetMovementPresentationCache()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopAndClearMovement()
    {
        if (EnemyCombatTimeline.IsRecord(this))
        {
            EnemyCombatTimeline.Record(this, "停止并清路径", CombatDiagnosticFields.Text("原状态", EnemyCombatTimeline.Describe(CurrentState)), CombatDiagnosticFields.Text("原移动", EnemyCombatTimeline.Describe(CurrentMovementMode)));
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("StopAndClear", "进入");
        }

        // 停止当前移动不等于放弃楼梯排队资格。电锯启动、威慑、攻击收尾等
        // 短动作都会调用本方法；若在这里注销，敌人会每次动作切换都掉到队尾，
        // 形成入口原地不动、后来者越过以及电锯敌人始终无法上楼的循环。
        if (agent != null)
        {
            agent.isStopped = true;
            agent.CancelPath();
        }

        if (ctx != null)
        {
            ctx.HasCrawlMoveIntent = false;
        }

        lastStableStandingMoveDirection = Vector3.zero;
        lastStableAvoidanceDirection = Vector3.zero;
        localAvoidanceSideSign = 0;
        localAvoidancePartnerId = 0;
        localAvoidanceSideHoldUntil = 0f;
        lastStableCrawlMoveDirection = Vector3.zero;
        crawlFacingInitialized = false;
        animationPresenter.ResetMovementPresentationCache();
        standingRootMotionSpeed = 0f;
        hasStandingRootMotionSpeed = false;
        if (anim != null)
        {
            animationPresenter.SetFloatValue(HashMoveSpeed, 0f);
            animationPresenter.SetFloatValue(HashMoveX, 0f);
            animationPresenter.SetFloatValue(HashMoveY, 0f);
            animationPresenter.SetBoolean(HashWALK, false);
            animationPresenter.SetIntegerValue(HashTurnDir, 0);
        }
    }

    /// <summary>
    /// 停止寻路，但保留当前移动参数交给动画在后续帧平滑减速。
    /// 用于追击进入近战待命，避免行为树切换时跑步动画被瞬间切回待机。
    /// </summary>
    /// <remarks>静态调用链：上游 HoldCombatPosition.OnEnd()、VillagerController.ArchitectureInvokeStopPathMovementWithAnimationBlend()；下游（含内部回调体） CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、PathAgent.CancelPath()、VillagerController.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopPathMovementWithAnimationBlend()
    {
        if (EnemyCombatTimeline.IsRecord(this))
        {
            EnemyCombatTimeline.Record(this, "淡出停步", CombatDiagnosticFields.Text("状态", EnemyCombatTimeline.Describe(CurrentState)));
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("StopPathBlend", "进入");
        }

        if (agent != null)
        {
            agent.isStopped = true;
            agent.CancelPath();
        }

        if (ctx != null)
        {
            ctx.HasCrawlMoveIntent = false;
        }
    }

    /// <summary>BeginHit 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginHit()
    {
        actionCoordinator.RequestAction(GameArchitecture.ActionKind.HitReaction, "兼容动作入口");
    }

    /// <summary>ConsumeHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.ConsumeHit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteHit.OnUpdate()；下游（含内部回调体） EnemyCombatModule.ConsumeHit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ConsumeHit()
    {
        combatModule.ConsumeHit();
    }

    /// <summary>BeginCrawl 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginCrawl()
    {
        actionCoordinator.RequestAction(GameArchitecture.ActionKind.Crawl, "兼容动作入口");
    }

    /// <summary>RequestStandUp 的业务处理入口；本方法直接依赖 EnemyCombatModule.RequestStandUp()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawl.OnUpdate()；下游（含内部回调体） EnemyCombatModule.RequestStandUp()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RequestStandUp()
    {
        combatModule.RequestStandUp();
    }

    /// <summary>BeginStandUp 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginStandUp()
    {
        actionCoordinator.RequestAction(GameArchitecture.ActionKind.GetUp, "兼容动作入口");
    }

    /// <summary>CompleteStandUp 的业务处理入口；本方法直接依赖 EnemyCombatModule.CompleteStandUp()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteStandUp.OnUpdate()；下游（含内部回调体） EnemyCombatModule.CompleteStandUp()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteStandUp()
    {
        combatModule.CompleteStandUp();
    }

    /// <summary>CancelStandUpAndResumeCrawl 的业务处理入口；本方法直接依赖 EnemyCombatModule.CancelStandUpAndResumeCrawl()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteStandUp.OnUpdate()；下游（含内部回调体） EnemyCombatModule.CancelStandUpAndResumeCrawl()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelStandUpAndResumeCrawl()
    {
        combatModule.CancelStandUpAndResumeCrawl();
    }

    /// <summary>ForceCrawlPoseAfterQTE 的业务处理入口；本方法直接依赖 EnemyCombatModule.ForceCrawlPoseAfterQTE()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.EndAction(int)、VillagerQTEParticipant.Release(int)；下游（含内部回调体） EnemyCombatModule.ForceCrawlPoseAfterQTE()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceCrawlPoseAfterQTE()
    {
        combatModule.ForceCrawlPoseAfterQTE();
    }

    /// <summary>PlayCrawlIdleImmediately 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayCrawlIdleImmediately()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokePlayCrawlIdleImmediately()；下游（含内部回调体） EnemyAnimationAdapter.PlayCrawlIdleImmediately()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void PlayCrawlIdleImmediately()
    {
        animationPresenter.PlayCrawlIdleImmediately();
    }

    /// <summary>BeginAttack 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool BeginAttack()
    {
        return actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, "兼容动作入口").HasAccept;
    }

    /// <summary>BeginCrawlAttack 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool BeginCrawlAttack()
    {
        return actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, "兼容动作入口", true).HasAccept;
    }

    /// <summary>EndAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.EndAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteAttack.OnEnd()、ExecuteAttack.OnUpdate()、ExecuteCombat.OnEnd()、ExecuteCombat.OnUpdate()、VillagerController.OnDisable()；下游（含内部回调体） EnemyCombatModule.EndAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndAttack()
    {
        combatModule.EndAttack();
    }

    /// <summary>EndCrawlAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.EndCrawlAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawlAttack.OnEnd()、ExecuteCrawlAttack.OnUpdate()；下游（含内部回调体） EnemyCombatModule.EndCrawlAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndCrawlAttack()
    {
        combatModule.EndCrawlAttack();
    }

    /// <summary>RestoreCrawlAfterAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.RestoreCrawlAfterAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawlAttack.OnEnd()、ExecuteCrawlAttack.OnUpdate()；下游（含内部回调体） EnemyCombatModule.RestoreCrawlAfterAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RestoreCrawlAfterAttack()
    {
        combatModule.RestoreCrawlAfterAttack();
    }

    /// <summary>BeginChase 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginChase(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()、VillagerController.ApplyPendingPursuitRegionChange()、VillagerController.ArchitectureInvokeBeginChase(bool)、VillagerController.MaintainChasePathRecoveryFacing()、VillagerController.MaintainIncompletePursuitArrival()；下游（含内部回调体） EnemyActionCoordinator.BeginChase(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="notifyCombatEngaged">中文含义：是否通知战斗已接战。使用位置：作为 EnemyActionCoordinator.BeginChase(bool) 的参数。</param>
    public void BeginChase(bool notifyCombatEngaged = true)
    {
        actionCoordinator.BeginChase(notifyCombatEngaged);
    }

    /// <summary>BeginAlert 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginAlert()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InvestigatePlayer.OnStart()；下游（含内部回调体） EnemyActionCoordinator.BeginAlert()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginAlert()
    {
        actionCoordinator.BeginAlert();
    }

    /// <summary>UpdateAlertFacing 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.UpdateAlertFacing()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyPerceptionModule.UpdateAlertFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void UpdateAlertFacing()
    {
        perceptionModule.UpdateAlertFacing();
    }

    /// <summary>BeginCombatHold 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginCombatHold()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCombat.OnUpdate()、HoldCombatPosition.OnStart()；下游（含内部回调体） EnemyActionCoordinator.BeginCombatHold()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginCombatHold()
    {
        actionCoordinator.BeginCombatHold();
    }

    /// <summary>UpdateCombatFacing 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.UpdateCombatFacing()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCombat.OnUpdate()、HoldCombatPosition.OnUpdate()、VillagerController.MaintainChasePathRecoveryFacing()、VillagerController.MaintainIncompletePursuitArrival()、VillagerController.UpdateChaseExecution()；下游（含内部回调体） EnemyActionCoordinator.UpdateCombatFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void UpdateCombatFacing()
    {
        actionCoordinator.UpdateCombatFacing();
    }

    /// <summary>BeginPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginPatrol()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.BeginPatrol()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginPatrol()
    {
        actionCoordinator.BeginPatrol();
    }

    /// <summary>BeginStationaryPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginStationaryPatrol(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PatrolInPlace.OnStart()；下游（含内部回调体） EnemyActionCoordinator.BeginStationaryPatrol(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="signedTurnAngle">中文含义：有符号转向角度。使用位置：作为 EnemyActionCoordinator.BeginStationaryPatrol(float) 的参数。</param>
    public void BeginStationaryPatrol(float signedTurnAngle)
    {
        actionCoordinator.BeginStationaryPatrol(signedTurnAngle);
    }

    /// <summary>ReleaseStationaryPatrolTurn 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ReleaseStationaryPatrolTurn()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PatrolInPlace.OnUpdate()；下游（含内部回调体） EnemyActionCoordinator.ReleaseStationaryPatrolTurn()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReleaseStationaryPatrolTurn()
    {
        actionCoordinator.ReleaseStationaryPatrolTurn();
    }

    /// <summary>UpdateStationaryPatrolFacing 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.UpdateStationaryPatrolFacing(Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PatrolInPlace.OnUpdate()；下游（含内部回调体） EnemyActionCoordinator.UpdateStationaryPatrolFacing(Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRotation">中文含义：目标旋转。使用位置：作为 EnemyActionCoordinator.UpdateStationaryPatrolFacing(Quaternion) 的参数。</param>
    public void UpdateStationaryPatrolFacing(Quaternion targetRotation)
    {
        actionCoordinator.UpdateStationaryPatrolFacing(targetRotation);
    }

    /// <summary>EndStationaryPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.EndStationaryPatrol()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PatrolInPlace.OnEnd()；下游（含内部回调体） EnemyActionCoordinator.EndStationaryPatrol()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndStationaryPatrol()
    {
        actionCoordinator.EndStationaryPatrol();
    }

    /// <summary>BeginIdle 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginIdle()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.MaintainAssassinationShowcaseIdle()、VillagerController.Start()；下游（含内部回调体） EnemyActionCoordinator.BeginIdle()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginIdle()
    {
        actionCoordinator.BeginIdle();
    }

    /// <summary>SetPatrolTarget 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.SetPatrolTarget(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SelectPatrolPoint.OnUpdate()；下游（含内部回调体） EnemyActionCoordinator.SetPatrolTarget(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 EnemyActionCoordinator.SetPatrolTarget(Vector3) 的参数。</param>
    public void SetPatrolTarget(Vector3 target)
    {
        actionCoordinator.SetPatrolTarget(target);
    }

    /// <summary>ClearPatrolTarget 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ClearPatrolTarget()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MoveToPatrolPoint.OnEnd()、SelectPatrolPoint.OnUpdate()；下游（含内部回调体） EnemyActionCoordinator.ClearPatrolTarget()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearPatrolTarget()
    {
        actionCoordinator.ClearPatrolTarget();
    }

    /// <summary>BeginDeath 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginDeath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeBeginDeath()、VillagerQTEParticipant.CommitAssassinationResult(int, float)；下游（含内部回调体） EnemyActionCoordinator.BeginDeath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginDeath()
    {
        actionCoordinator.BeginDeath();
    }

    /// <summary>ApplyDeathAnimatorParameters 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.ApplyDeathAnimatorParameters()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeApplyDeathAnimatorParameters()；下游（含内部回调体） EnemyAnimationAdapter.ApplyDeathAnimatorParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyDeathAnimatorParameters()
    {
        animationPresenter.ApplyDeathAnimatorParameters();
    }

    /// <summary>ClearActionParameters 的业务处理入口；本方法直接依赖 EnemyCombatModule.ClearActionParameters()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteHit.OnEnd()、ExecuteStandUp.OnUpdate()、StopAndClearMovement.OnUpdate()；下游（含内部回调体） EnemyCombatModule.ClearActionParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearActionParameters()
    {
        combatModule.ClearActionParameters();
    }

    /// <summary>GetPatrolTurnDirection 的业务处理入口；本方法直接依赖 Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeGetPatrolTurnDirection(float)；下游（含内部回调体） Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="signedTurnAngle">中文含义：有符号转向角度。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.GetPatrolTurnDirection(float)。</param>
    private static int GetPatrolTurnDirection(float signedTurnAngle)
    {
        if (Mathf.Abs(signedTurnAngle) >= 135f)
        {
            return 3;
        }

        if (signedTurnAngle < 0f)
        {
            return 1;
        }
        else
        {
            return 2;
        }
    }

    /// <summary>IsPlayerWithinBodyContactRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsPlayerWithinBodyContactRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsPlayerInAttackRange.get、VillagerController.RequiresNavigationTraversalToPlayer()；下游（含内部回调体） EnemyCombatModule.IsPlayerWithinBodyContactRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsPlayerWithinBodyContactRange()
    {
        return combatModule.IsPlayerWithinBodyContactRange();
    }

    /// <summary>CanEngagePlayerAtCloseRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.CanEngagePlayerAtCloseRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ShouldHoldCombatPosition.get；下游（含内部回调体） EnemyCombatModule.CanEngagePlayerAtCloseRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanEngagePlayerAtCloseRange()
    {
        return combatModule.CanEngagePlayerAtCloseRange();
    }

    /// <summary>IsPlayerWithinAttackStartRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsPlayerWithinAttackStartRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsInAttackStartRange.get、VillagerController.ResolveStableCrawlMoveDirection()、VillagerController.ShouldHoldCombatPosition.get、VillagerController.ShouldUseCloseDirectPursuitTarget()；下游（含内部回调体） EnemyCombatModule.IsPlayerWithinAttackStartRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsPlayerWithinAttackStartRange()
    {
        return combatModule.IsPlayerWithinAttackStartRange();
    }

    /// <summary>IsPlayerWithinCombatHoldRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsPlayerWithinCombatHoldRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ShouldHoldCombatPosition.get；下游（含内部回调体） EnemyCombatModule.IsPlayerWithinCombatHoldRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsPlayerWithinCombatHoldRange()
    {
        return combatModule.IsPlayerWithinCombatHoldRange();
    }

    /// <summary>IsPlayerWithinCombatVerticalRange 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RequiresDirectNavigationTraversalToPlayer()、VillagerController.RequiresNavigationTraversalToPlayer()、VillagerController.ShouldUseCloseDirectPursuitTarget()；下游（含内部回调体） EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：作为 EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController) 的参数。</param>
    private bool IsPlayerWithinCombatVerticalRange(LeonController player)
    {
        return combatModule.IsPlayerWithinCombatVerticalRange(player);
    }

    /// <summary>RequiresNavigationTraversalToPlayer 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Vector3.Distance(Vector3, Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HasClearLineToPlayer(bool, float)、VillagerController.IsApproachingEnemyStairBridge() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeRequiresNavigationTraversalToPlayer()、VillagerController.HasIncompletePursuitArrival.get、VillagerController.RequiresNavigationToPlayer.get、VillagerController.ShouldHoldCombatPosition.get、VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Vector3.Distance(Vector3, Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HasClearLineToPlayer(bool, float)、VillagerController.IsApproachingEnemyStairBridge() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RequiresNavigationTraversalToPlayer()
    {
        // player（玩家）：使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数 等共 6 处。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        // 隔着已打开的门能看见玩家，不代表已经完成出门。
        // 门洞队员和跨区域敌人必须先完成通路，不能把室内兜底位置
        // 当作稳定包围槽而切到待命，停住后便不会再刷新队首阶段。
        if (pursuitIngressDoor != null || TryResolveCurrentPursuitRegions(player.transform.position, out int /* 中文含义：敌人区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enemyRegion, out int /* 中文含义：目标区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ targetRegion) && enemyRegion != targetRegion)
        {
            return true;
        }

        if (specialTraversal != null && (specialTraversal.IsMovementProtected || specialTraversal.HasPendingRoute || specialTraversal.HasPendingDropRoute))
        {
            return true;
        }

        if (!IsPlayerWithinCombatVerticalRange(player))
        {
            return true;
        }

        // lineCheckDistance（线检查距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.HasClearLineToPlayer(bool, float) 的参数。
float lineCheckDistance = Mathf.Max(sightRange, Vector3.Distance(transform.position, player.transform.position) + 1f);
        if (fullyAwareOfPlayer && !HasClearLineToPlayer(false, lineCheckDistance))
        {
            return true;
        }

        if (IsPlayerWithinBodyContactRange())
        {
            return false;
        }

        // passageProbeRadius（通道探测半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数。
float passageProbeRadius = Mathf.Max(doorOpenProbeRadius + 0.6f, directApproachStoppingDistance);
        if (DoorController.IsInsideOpenPassage(transform.position, passageProbeRadius) || DoorController.IsInsideOpenPassage(player.transform.position, passageProbeRadius))
        {
            return true;
        }

        // offset（偏移量）：使用位置：VillagerController.RequiresNavigationTraversalToPlayer()。
Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        // distance（距离）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、VillagerController.RequiresNavigationTraversalToPlayer()。
float distance = offset.magnitude;
        // stoppingDistance（停止中距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、VillagerController.RequiresNavigationTraversalToPlayer()。
float stoppingDistance = Mathf.Max(0.1f, directApproachStoppingDistance);
        if (distance <= stoppingDistance)
        {
            return false;
        }

        // approachPoint（接近点）：使用位置：作为 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool) 的参数、VillagerController.RequiresNavigationTraversalToPlayer()。
Vector3 approachPoint = transform.position + offset / distance * (distance - stoppingDistance);
        approachPoint.y = Mathf.Lerp(transform.position.y, player.transform.position.y, (distance - stoppingDistance) / distance);
        return !CanMoveDirectlyToNavigationPoint(approachPoint);
    }

    /// <summary>RequiresDirectNavigationTraversalToPlayer 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Max(float, float)、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.IsPlayerWithinCombatVerticalRange(LeonController)、VillagerController.IsStandingOnEnemyStairBridge()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RequiresDirectTraversalToPlayer.get；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Max(float, float)、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.IsPlayerWithinCombatVerticalRange(LeonController)、VillagerController.IsStandingOnEnemyStairBridge()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RequiresDirectNavigationTraversalToPlayer()
    {
        // player（玩家）：使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、作为 VillagerController.IsPlayerWithinCombatVerticalRange(LeonController) 的参数、VillagerController.RequiresDirectNavigationTraversalToPlayer()。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (!IsPlayerWithinCombatVerticalRange(player))
        {
            return true;
        }

        // passageProbeRadius（通道探测半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数。
float passageProbeRadius = Mathf.Max(doorOpenProbeRadius + 0.6f, directApproachStoppingDistance);
        return DoorController.IsInsideOpenPassage(transform.position, passageProbeRadius) || DoorController.IsInsideOpenPassage(player.transform.position, passageProbeRadius);
    }

    /// <summary>IsStandingAttackSurfaceStable 的业务处理入口；本方法直接依赖 Collider.Raycast(Ray, out RaycastHit, float)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeIsStandingAttackSurfaceStable()、VillagerController.ShouldHoldCombatPosition.get；下游（含内部回调体） Collider.Raycast(Ray, out RaycastHit, float)、EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3)、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsStandingAttackSurfaceStable()
    {
        // 玩家堵在台阶上时，前排可在同一段实体踏面上攻击，命中仍由原有条件检查。
        if (Player && PlantNearHouseThreeTread(transform.position) && PlantNearHouseThreeTread(Player.transform.position) && Mathf.Abs(transform.position.y - Player.transform.position.y) < .55f && (specialTraversal == null || !specialTraversal.IsMovementProtected))
        {
            return true;
        }

        // 已标记的可走屋顶允许在其坡面站立作战。坡面前方路点的高度差
        // 不等于脚下悬空；楼梯和翻越仍由专用移动保护，不能在途中攻击。
        if (IsNavigationTraversalProtected)
        {
            return false;
        }

        // onElevatedSurface（是否高架表面时的通知回调）：保存 EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsStandingAttackSurfaceStable()。
bool onElevatedSurface = EnemyWindowTraversalRoute.IsPointOnAnyPlatformSide(transform.position);
        if (!onElevatedSurface && agent != null && agent.CurrentPathState == PathAgentState.Valid && agent.TryGetCurrentWaypoint(out Vector3 /* 中文含义：路径点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ waypoint) && Mathf.Abs(waypoint.y - transform.position.y) > Mathf.Max(0.1f, agent.waypointVerticalReachedTolerance))
        {
            return false;
        }

        // origin（原点）：使用位置：作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.IsStandingAttackSurfaceStable()。
Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.6f, groundProbeHeight);
        CombatPerformanceCounters.physicsQueryCount++;
        // 攻击起手与行走使用相同的足底支撑范围，避免已走到木板缝上
        // 却被中心射线误判悬空，始终保持追击状态顶住玩家。
        // hitCount（命中数量）：保存 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsStandingAttackSurfaceStable()。
int hitCount = Physics.SphereCastNonAlloc(origin, NavigationFootBottomRadius(), Vector3.down, groundHits, Mathf.Max(1.2f, groundProbeHeight + maximumGroundDrop), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // nearestDistance（最近距离）：使用位置：VillagerController.IsStandingAttackSurfaceStable()。
float nearestDistance = float.MaxValue;
        // nearestHit（最近命中）：使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.IsStandingAttackSurfaceStable()。
RaycastHit nearestHit = default;
        // foundGround（是否找到的地面）：使用位置：VillagerController.IsStandingAttackSurfaceStable()。
bool foundGround = false;
        // i（当前遍历索引）：使用位置：VillagerController.IsStandingAttackSurfaceStable()。
for (int i = 0; i < hitCount; i++)
        {
            // hit（命中）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 Ray.Ray(Vector3, Vector3) 的参数 等共 7 处。
RaycastHit hit = groundHits[i];
            if (hit.collider != null && hit.collider.Raycast(new Ray(hit.point + Vector3.up * .1f, Vector3.down), out RaycastHit /* 中文含义：踏面命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ treadHit, .2f))
            {
                hit = treadHit;
            }

            // conditionResult124（短路判断得到的限制的返回结果）：保存短路分支结果，未选中的分支不执行。使用位置：VillagerController.IsStandingAttackSurfaceStable()。
bool conditionResult124;
            if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(hit.collider) != null || NavigationCollisionCache.Player(hit.collider) != null || IsNavigationObstacle(hit.collider))
            {
                conditionResult124 = true;
            }
            else
            {
                // leftOperand123（计算前缓存的左侧角度的返回结果）：从角度的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.IsStandingAttackSurfaceStable()。
float leftOperand123 = Vector3.Angle(hit.normal, Vector3.up);
                // selectedValue122（分支选出的最大的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、VillagerController.IsStandingAttackSurfaceStable()。
float selectedValue122;
                if (onElevatedSurface)
                {
                    selectedValue122 = Mathf.Max(stableAttackSurfaceSlope, maximumGroundSlope);
                }
                else
                {
                    selectedValue122 = stableAttackSurfaceSlope;
                }

                conditionResult124 = leftOperand123 > Mathf.Clamp(selectedValue122, 1f, 89f);
            }

            if (conditionResult124 || origin.y - hit.point.y >= nearestDistance)
            {
                continue;
            }

            nearestDistance = origin.y - hit.point.y;
            nearestHit = hit;
            foundGround = true;
        }

        return foundGround && Mathf.Abs(nearestHit.point.y + groundOffset - transform.position.y) <= Mathf.Max(0.1f, maximumGroundDrop);
    }

    /// <summary>HasAttackRecoveryGroundSupport 的业务处理入口；本方法直接依赖 Collider.Raycast(Ray, out RaycastHit, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Ray.Ray(Vector3, Vector3) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeHasAttackRecoveryGroundSupport()；下游（含内部回调体） Collider.Raycast(Ray, out RaycastHit, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Ray.Ray(Vector3, Vector3) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasAttackRecoveryGroundSupport()
    {
        // 攻击起手要求平稳站位，但收尾只需仍有可站立的脚底支撑。
        // 木板缝隙与倾斜边缘可能漏掉中心射线，不能因此瞬移回攻击起点。
        // supportRadius（支撑半径）：保存 VillagerController.NavigationFootBottomRadius() 的返回结果，供当前步骤使用。使用位置：作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
float supportRadius = NavigationFootBottomRadius();
        // probeHeight（探测高度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
float probeHeight = Mathf.Max(0.6f, groundProbeHeight);
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasAttackRecoveryGroundSupport()。
int hitCount = Physics.SphereCastNonAlloc(transform.position + Vector3.up * probeHeight, supportRadius, Vector3.down, groundHits, probeHeight + Mathf.Max(0.1f, maximumGroundDrop), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.HasAttackRecoveryGroundSupport()。
for (int i = 0; i < hitCount; i++)
        {
            // hit（命中）：使用位置：作为 Ray.Ray(Vector3, Vector3) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.HasAttackRecoveryGroundSupport()。
RaycastHit hit = groundHits[i];
            // candidate（候选）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsEnemyStairBridgeCollider(Collider) 的参数 等共 5 处。
Collider candidate = hit.collider;
            if (candidate == null || NavigationCollisionCache.Enemy(candidate) != null || NavigationCollisionCache.Player(candidate) != null || IsNavigationObstacle(candidate))
            {
                continue;
            }

            // 球体扫到木板棱边时返回的是接触方向；在接触点向下复查
            // 承重面的真实法线，不能把平木板的边缘误认为陡坡。
            if (candidate.Raycast(new Ray(hit.point + Vector3.up * 0.1f, Vector3.down), out RaycastHit /* 中文含义：表面命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ surfaceHit, 0.2f))
            {
                hit = surfaceHit;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > Mathf.Clamp(maximumGroundSlope, 1f, 89f))
            {
                continue;
            }

            // heightChange（高度变化）：使用位置：VillagerController.HasAttackRecoveryGroundSupport()。
float heightChange = hit.point.y + groundOffset - transform.position.y;
            if (heightChange <= Mathf.Max(0.05f, maximumGroundStepUp) && heightChange >= -Mathf.Max(0.05f, maximumGroundDrop))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>CaptureAttackRecoveryAnchor 的业务处理入口；本方法直接依赖 EnemyCombatModule.CaptureAttackRecoveryAnchor()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.CaptureAttackRecoveryAnchor()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CaptureAttackRecoveryAnchor()
    {
        combatModule.CaptureAttackRecoveryAnchor();
    }

    /// <summary>IsFacingPlayerForAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsFacingPlayerForAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.IsFacingPlayerForAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFacingPlayerForAttack()
    {
        return combatModule.IsFacingPlayerForAttack();
    }

    /// <summary>IsFacingPlayerForCrawlAttack 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsFacingPlayerForCrawlAttack()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.IsFacingPlayerForCrawlAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFacingPlayerForCrawlAttack()
    {
        return combatModule.IsFacingPlayerForCrawlAttack();
    }

    /// <summary>GetHorizontalVectorToPlayerBody 的业务处理入口；本方法直接依赖 EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：作为 EnemyCombatModule.GetHorizontalVectorToPlayerBody(LeonController) 的参数。</param>
    private Vector3 GetHorizontalVectorToPlayerBody(LeonController player)
    {
        return combatModule.GetHorizontalVectorToPlayerBody(player);
    }

    /// <summary>HasClearLineToPlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.HasClearLineToPlayer(bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeHasClearLineToPlayer(bool, float)、VillagerController.RequiresNavigationTraversalToPlayer()；下游（含内部回调体） EnemyPerceptionModule.HasClearLineToPlayer(bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requireSightAngle">中文含义：是否需要视野角度。使用位置：作为 EnemyPerceptionModule.HasClearLineToPlayer(bool, float) 的参数。</param>
    /// <param name="maximumDistance">中文含义：最大距离。使用位置：作为 EnemyPerceptionModule.HasClearLineToPlayer(bool, float) 的参数。</param>
    private bool HasClearLineToPlayer(bool requireSightAngle, float maximumDistance)
    {
        return perceptionModule.HasClearLineToPlayer(requireSightAngle, maximumDistance);
    }

    /// <summary>ResolveFallbackHitboxType 的业务处理入口；本方法直接依赖 EnemyCombatModule.ResolveFallbackHitboxType(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、WeaponBase.ProcessHit(RaycastHit, float)；下游（含内部回调体） EnemyCombatModule.ResolveFallbackHitboxType(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：作为 EnemyCombatModule.ResolveFallbackHitboxType(Vector3) 的参数。</param>
    public HitboxType ResolveFallbackHitboxType(Vector3 hitPoint)
    {
        return combatModule.ResolveFallbackHitboxType(hitPoint);
    }

    /// <summary>IsPlayerWithinChaseDisengageRange 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：VillagerController.IsPlayerWithinChaseDisengageRange(LeonController)。</param>
    private bool IsPlayerWithinChaseDisengageRange(LeonController player)
    {
        if (player == null)
        {
            return false;
        }

        // offset（偏移量）：使用位置：VillagerController.IsPlayerWithinChaseDisengageRange(LeonController)。
Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= chaseDisengageRange * chaseDisengageRange;
    }

    /// <summary>ClearCombatEngagement 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ClearCombatEngagement(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeClearCombatEngagement(bool)、VillagerController.SetLODActive(bool)；下游（含内部回调体） EnemyPerceptionModule.ClearCombatEngagement(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveInvestigation">中文含义：是否保留调查。使用位置：作为 EnemyPerceptionModule.ClearCombatEngagement(bool) 的参数。</param>
    private void ClearCombatEngagement(bool preserveInvestigation)
    {
        perceptionModule.ClearCombatEngagement(preserveInvestigation);
    }

    /// <summary>UpdateAwareness 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.UpdateAwareness()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyPerceptionModule.UpdateAwareness()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateAwareness()
    {
        perceptionModule.UpdateAwareness();
    }

    /// <summary>BecomeFullyAwareOfPlayer 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.BecomeFullyAwareOfPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeBecomeFullyAwareOfPlayer()；下游（含内部回调体） EnemyPerceptionModule.BecomeFullyAwareOfPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BecomeFullyAwareOfPlayer()
    {
        perceptionModule.BecomeFullyAwareOfPlayer();
    }

    /// <summary>ReceiveSharedCombatAlert 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ReceiveSharedCombatAlert(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)、EnemyAIManager.UpdateMovementNoiseAwareness()、EnemyAIManager.UpdateSharedCombatAlert()；下游（含内部回调体） EnemyPerceptionModule.ReceiveSharedCombatAlert(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyPerceptionModule.ReceiveSharedCombatAlert(Vector3) 的参数。</param>
    public void ReceiveSharedCombatAlert(Vector3 playerPosition)
    {
        perceptionModule.ReceiveSharedCombatAlert(playerPosition);
    }

    /// <summary>
    /// 记录玩家跨区域版本。真正的路径切换在敌人自己的更新中执行，
    /// 这样三号门与楼梯专用流程可以把刷新延迟到安全出口，而不会被
    /// 管理器更新顺序从中间打断。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、EnemyAIManager.UpdatePlayerPursuitRegion()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="regionVersion">中文含义：区域版本。使用位置：VillagerController.ReceivePursuitRegionChange(int, Vector3)。</param>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：VillagerController.ReceivePursuitRegionChange(int, Vector3)。</param>
    public void ReceivePursuitRegionChange(int regionVersion, Vector3 playerPosition)
    {
        if (regionVersion <= pendingPursuitRegionVersion || IsDead)
        {
            return;
        }

        pendingPursuitRegionVersion = regionVersion;
        pendingPursuitRegionPlayerPosition = playerPosition;
    }

    /// <summary>ApplyPendingPursuitRegionChange 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;PathAgent&gt;()、EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyPerceptionModule.UpdateRegionMemory(Vector3)、object.ReferenceEquals(object, object)、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.BeginChase(bool)、VillagerController.ClearPursuitIngress()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） Component.GetComponent&lt;PathAgent&gt;()、EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyPerceptionModule.UpdateRegionMemory(Vector3)、object.ReferenceEquals(object, object)、PathAgent.InvalidatePursuitResolutionCache()、VillagerController.BeginChase(bool)、VillagerController.ClearPursuitIngress()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyPendingPursuitRegionChange()
    {
        if (pendingPursuitRegionVersion <= appliedPursuitRegionVersion || IsDead || IsQTEControlled)
        {
            return;
        }

        // 普通门已经取得穿越权时先完成到另一侧的释放，避免在门洞
        // 中央反向后同时占用两个方向。下一帧会从真实区域重建路线。
        if (pursuitIngressDoor != null && pursuitIngressCrossing)
        {
            return;
        }

        appliedPursuitRegionVersion = pendingPursuitRegionVersion;
        perceptionModule.UpdateRegionMemory(pendingPursuitRegionPlayerPosition);
        if (pursuitIngressDoor != null || completedPursuitIngressDoor != null)
        {
            ClearPursuitIngress();
        }

        // receiverValue125（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.ApplyPendingPursuitRegionChange()。
global::EnemyAIManager receiverValue125 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue125, null))
        {
            receiverValue125.InvalidatePursuitDestination(this);
        }

        if (agent == null)
        {
            agent = GetComponent<PathAgent>();
        }

        if (agent != null && agent.enabled)
        {
            agent.InvalidatePursuitResolutionCache();
            agent.isStopped = false;
        }

        if (combatEngaged && (combatHoldActive || CurrentState != VillagerState.Chase || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion))
        {
            BeginChase(false);
        }
    }

    /// <summary>ReceiveVillagePursuitOrder 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.EngageAllMissionEnemies(Vector3)、EnemyAIManager.RegisterEnemy(VillagerController)、EnemyAIManager.UpdateEnemyStates()；下游（含内部回调体） EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3) 的参数。</param>
    public void ReceiveVillagePursuitOrder(Vector3 playerPosition)
    {
        perceptionModule.ReceiveVillagePursuitOrder(playerPosition);
    }

    /// <summary>ReceiveGunshot 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ReceiveGunshot(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)；下游（含内部回调体） EnemyPerceptionModule.ReceiveGunshot(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 EnemyPerceptionModule.ReceiveGunshot(Vector3) 的参数。</param>
    public void ReceiveGunshot(Vector3 position)
    {
        perceptionModule.ReceiveGunshot(position);
    }

    /// <summary>UpdateInvestigation 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.UpdateInvestigation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InvestigatePlayer.OnUpdate()；下游（含内部回调体） EnemyPerceptionModule.UpdateInvestigation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool UpdateInvestigation()
    {
        return perceptionModule.UpdateInvestigation();
    }

    /// <summary>GetCurrentCombatTarget 的业务处理入口；本方法直接依赖 EnemyAIManager.GetCombatDestination(VillagerController, Vector3)、VillagerController.ShouldUseCloseDirectPursuitTarget()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CurrentChaseTarget.get；下游（含内部回调体） EnemyAIManager.GetCombatDestination(VillagerController, Vector3)、VillagerController.ShouldUseCloseDirectPursuitTarget()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 GetCurrentCombatTarget()
    {
        if (HasSiegeBreachOrder)
        {
            return door03TraversalRoute.ExteriorGate;
        }
        // player（玩家）：使用位置：VillagerController.GetCurrentCombatTarget()。
LeonController player = Player;
        if (player == null)
        {
            return lastKnownPlayerPosition;
        }

        // targetPosition（目标位置）：使用位置：作为 EnemyAIManager.GetCombatDestination(VillagerController, Vector3) 的参数、VillagerController.GetCurrentCombatTarget()。
Vector3 targetPosition;
        if (combatEngaged || fullyAwareOfPlayer)
        {
            targetPosition = player.transform.position;
        }
        else
        {
            targetPosition = lastKnownPlayerPosition;
        }

        // manager（当前敌人智能管理器）：使用位置：VillagerController.GetCurrentCombatTarget()。
EnemyAIManager manager = EnemyAIManager.Instance;
        if (IsCrowdBypassing)
        {
            return crowdBypassDestination;
        }

        if (fullyAwareOfPlayer && manager != null)
        {
            if (ShouldUseCloseDirectPursuitTarget())
            {
                return player.transform.position;
            }

            return manager.GetCombatDestination(this, targetPosition);
        }

        if (fullyAwareOfPlayer && ShouldUseCloseDirectPursuitTarget())
        {
            return player.transform.position;
        }

        return targetPosition;
    }

    /// <summary>TryResolveCloseDirectPursuitDirection 的业务处理入口；本方法直接依赖 VillagerController.RequiresNavigationTraversalToPlayer()、VillagerController.ShouldUseCloseDirectPursuitTarget()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirection()、VillagerController.ResolveStandingPathDirectionForCurrentFrame()；下游（含内部回调体） VillagerController.RequiresNavigationTraversalToPlayer()、VillagerController.ShouldUseCloseDirectPursuitTarget()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)。</param>
    private bool TryResolveCloseDirectPursuitDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (HasSiegeBreachOrder || pursuitIngressDoor != null || !ShouldUseCloseDirectPursuitTarget() || RequiresNavigationTraversalToPlayer())
        {
            return false;
        }

        // player（玩家）：使用位置：VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        // offset（偏移量）：使用位置：VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)。
Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        // distance（距离）：使用位置：VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)。
float distance = offset.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        direction = offset / distance;
        return true;
    }

    /// <summary>ShouldUseCloseDirectPursuitTarget 的业务处理入口；本方法直接依赖 EnemyAIManager.ShouldUseDirectPressurePursuit(VillagerController, Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Max(float, float)、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.IsPlayerWithinAttackStartRange()、VillagerController.IsPlayerWithinCombatVerticalRange(LeonController)、VillagerController.IsStandingOnEnemyStairBridge()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.GetCurrentCombatTarget()、VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)；下游（含内部回调体） EnemyAIManager.ShouldUseDirectPressurePursuit(VillagerController, Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Max(float, float)、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.IsPlayerWithinAttackStartRange()、VillagerController.IsPlayerWithinCombatVerticalRange(LeonController)、VillagerController.IsStandingOnEnemyStairBridge()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool ShouldUseCloseDirectPursuitTarget()
    {
        // player（玩家）：使用位置：作为 EnemyAIManager.ShouldUseDirectPressurePursuit(VillagerController, Vector3) 的参数、作为 VillagerController.IsPlayerWithinCombatVerticalRange(LeonController) 的参数、VillagerController.ShouldUseCloseDirectPursuitTarget()。
LeonController player = Player;
        if (player == null || IsCrowdBypassing || !combatEngaged || !IsPlayerWithinCombatVerticalRange(player) || IsPlayerWithinAttackStartRange())
        {
            return false;
        }

        // 外平台的木板接缝必须由当前导航段持续控制。接缝高度和视线的微小
        // 变化会让近距离直追逐帧开关，清空路径后又回头追旧入口，导致反复转身。
        // 到达导航末端后仍可按原有实体支撑检查接近目标。
        // region（区域）：使用位置：VillagerController.ShouldUseCloseDirectPursuitTarget()。
var region = HouseThreeRegionNavigation.Current;
        if (region != null && agent != null && agent.CurrentPathState != PathAgentState.Arrived && region.TreadRegion(transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform)
        {
            return false;
        }

        // offset（偏移量）：使用位置：VillagerController.ShouldUseCloseDirectPursuitTarget()。
Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        // directRange（直接范围）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldUseCloseDirectPursuitTarget()。
float directRange = Mathf.Max(1f, closeDirectPursuitRange);
        if (offset.sqrMagnitude > directRange * directRange)
        {
            return false;
        }

        // manager（当前敌人智能管理器）：使用位置：VillagerController.ShouldUseCloseDirectPursuitTarget()。
EnemyAIManager manager = EnemyAIManager.Instance;
        return manager == null || manager.ShouldUseDirectPressurePursuit(this, player.transform.position);
    }

    /// <summary>CalculateStandingAdvanceSpeed 的业务处理入口；本方法直接依赖 EnemyContext.MoveSpeedToAgentSpeed(float)、Mathf.Clamp(float, float, float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateAnimator()；下游（含内部回调体） EnemyContext.MoveSpeedToAgentSpeed(float)、Mathf.Clamp(float, float, float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animationParameter">中文含义：动画参数。使用位置：作为 EnemyContext.MoveSpeedToAgentSpeed(float) 的参数。</param>
    internal float CalculateStandingAdvanceSpeed(float animationParameter)
    {
        // baseline（基准）：保存 EnemyContext.MoveSpeedToAgentSpeed(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.CalculateStandingAdvanceSpeed(float)。
float baseline = ctx.MoveSpeedToAgentSpeed(animationParameter);
        // player（玩家）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.CalculateStandingAdvanceSpeed(float)。
var player = ctx.Player;
        if (!player || CurrentState != VillagerState.Chase)
        {
            return baseline;
        }

        // interval（间隔）：使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.CalculateStandingAdvanceSpeed(float)。
Vector3 interval = player.transform.position - transform.position;
        interval.y = 0f;
        // pullOpenSpeed（拉近打开速度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Min(float, float) 的参数。
float pullOpenSpeed = Mathf.Max(0f, Vector3.Dot(player.HorizontalSpeed, interval.normalized));
        // walkCatchUpSpeed（行走捕获向上速度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.CalculateStandingAdvanceSpeed(float)。
float walkCatchUpSpeed = Mathf.Max(2.05f, Mathf.Min(3.45f, pullOpenSpeed + .22f));
        // cruiseSpeed（巡航速度）：使用位置：VillagerController.CalculateStandingAdvanceSpeed(float)。
float cruiseSpeed;
        if (player.IsEffectiveSprinting)
        {
            cruiseSpeed = Mathf.Lerp(2.05f, Mathf.Clamp(pullOpenSpeed + .18f, 2.05f, player.runSpeed + .18f), Mathf.InverseLerp(3f, 8f, interval.magnitude));
        }
        else
        {
            cruiseSpeed = walkCatchUpSpeed;
        }

        return baseline * cruiseSpeed / EnemyContext.StandingChaseAgentSpeed;
    }

    /// <summary>GetTargetMoveSpeed 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResolveChaseMoveSpeed(float, float, float)、EnemyCombatModule.CalculateStaggerRestoreMultiplier()、EnemyContext.ClampStandingMoveParameter(float)、Mathf.Clamp(float, float, float)、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeGetTargetMoveSpeed()、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） ChainsawManAnimationBridge.ResolveChaseMoveSpeed(float, float, float)、EnemyCombatModule.CalculateStaggerRestoreMultiplier()、EnemyContext.ClampStandingMoveParameter(float)、Mathf.Clamp(float, float, float)、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float GetTargetMoveSpeed()
    {
        if (CurrentState != VillagerState.Chase && CurrentState != VillagerState.Alert)
        {
            return ctx.TargetMoveSpeed;
        }

        // normalizedTime（动画归一化播放时间）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数。
float normalizedTime = Mathf.Clamp01((Time.time - chaseStartedTime) / chaseAccelerationDuration);
        // requestedSpeed（请求的速度）：保存 EnemyContext.ClampStandingMoveParameter(float) 的返回结果，供当前步骤使用。使用位置：作为 ChainsawManAnimationBridge.ResolveChaseMoveSpeed(float, float, float) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、VillagerController.GetTargetMoveSpeed()。
float requestedSpeed = ctx.ClampStandingMoveParameter(chaseSpeed);
        if (chainsawBridge != null)
        {
            // healthRatio（生命比例）：使用位置：作为 ChainsawManAnimationBridge.ResolveChaseMoveSpeed(float, float, float) 的参数、VillagerController.GetTargetMoveSpeed()。
float healthRatio;
            if (maxHealth > 0f)
            {
                healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
            }
            else
            {
                healthRatio = 0f;
            }

            requestedSpeed = ctx.ClampStandingMoveParameter(chainsawBridge.ResolveChaseMoveSpeed(requestedSpeed, PlayerDistance, healthRatio));
        }

        // startSpeed（起始速度）：保存 EnemyContext.ClampStandingMoveParameter(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.SmoothStep(float, float, float) 的参数。
float startSpeed = ctx.ClampStandingMoveParameter(chaseStartMoveSpeed);
        // targetSpeed（目标速度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数、VillagerController.GetTargetMoveSpeed()。
float targetSpeed = Mathf.SmoothStep(startSpeed, Mathf.Clamp(requestedSpeed, 0.1f, ctx.MaximumStandingMoveParameter), normalizedTime);
        EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager != null && manager.TryReadTacticalMovement(this, out var movement) &&
            (movement == EnemyAIManager.TacticalMovement.Circle || movement == EnemyAIManager.TacticalMovement.Yield))
        {
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(patrolSpeed, 0.35f));
        }

        targetSpeed *= combatModule.CalculateStaggerRestoreMultiplier();
        targetSpeed = ctx.ClampStandingMoveParameter(targetSpeed);
        ctx.TargetMoveSpeed = targetSpeed;
        return targetSpeed;
    }

    /// <summary>FacePlayerImmediately 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeFacePlayerImmediately()；下游（含内部回调体） CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FacePlayerImmediately()
    {
        if (Player == null)
        {
            return;
        }

        // direction（方向）：使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、VillagerController.FacePlayerImmediately()。
Vector3 direction = Player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            motionExecutor.CommitNormalOrientation(Quaternion.LookRotation(direction.normalized));
        }
    }

    /// <summary>FacePosition 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)、Quaternion.Slerp(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeFacePosition(Vector3, float)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.LookRotation(Vector3)、Quaternion.Slerp(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">中文含义：目标位置。使用位置：VillagerController.FacePosition(Vector3, float)。</param>
    /// <param name="turnSpeed">中文含义：转向速度。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。</param>
    private void FacePosition(Vector3 targetPosition, float turnSpeed)
    {
        // direction（方向）：使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、VillagerController.FacePosition(Vector3, float)。
Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        // targetRotation（目标旋转）：保存 Quaternion.LookRotation(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。
Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        motionExecutor.CommitNormalOrientation(Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed));
    }

    /// <summary>EnsureRuntimeHitboxCoverage 的业务处理入口；本方法直接依赖 LayerMask.NameToLayer(string)、Vector3.Vector3(float, float, float)、VillagerController.ConfigureRuntimeArmCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)、VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeLimb(HumanBodyBones, HitboxType, float, float, float, int)、VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；下游（含内部回调体） LayerMask.NameToLayer(string)、Vector3.Vector3(float, float, float)、VillagerController.ConfigureRuntimeArmCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)、VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeLimb(HumanBodyBones, HitboxType, float, float, float, int)、VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void EnsureRuntimeHitboxCoverage()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
        if (AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Head) == null)
        {
            return;
        }

        // hitboxLayer（命中盒图层）：保存 LayerMask.NameToLayer(string) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConfigureRuntimeArmCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int) 的参数、作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数、作为 VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int) 的参数 等共 6 处。
int hitboxLayer = LayerMask.NameToLayer("EnemyHitbox");
        if (hitboxLayer < 0)
        {
            return;
        }

        Vector3 headCenter = Vector3.zero;
        if (!anim.isHuman)
        {
            Transform head = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Head);
            Transform neck = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.Neck);
            if (neck != null)
            {
                headCenter = head.InverseTransformVector((head.position - neck.position).normalized * 0.06f);
            }
            HumanBodyBones chestEnd = HumanBodyBones.Head;
            if (neck != null)
            {
                chestEnd = HumanBodyBones.Neck;
            }
            ConfigureRuntimeBoneCapsule(HumanBodyBones.Chest, chestEnd, HitboxType.Chest, 0.18f, hitboxLayer);
        }
        else
        {
            ConfigureRuntimeCapsule(HumanBodyBones.Chest, HitboxType.Chest, 0.18f, 0.35f, new Vector3(0f, 0.1f, 0f), hitboxLayer);
        }

        ConfigureRuntimeSphere(HumanBodyBones.Head, HitboxType.Head, 0.12f, headCenter, hitboxLayer);
        ConfigureRuntimeCapsule(HumanBodyBones.Hips, HitboxType.Pelvis, 0.16f, 0.25f, Vector3.zero, hitboxLayer);
        ConfigureRuntimeBoneCapsule(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HitboxType.LeftArm, 0.10f, hitboxLayer);
        ConfigureRuntimeBoneCapsule(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HitboxType.LeftArm, 0.10f, hitboxLayer);
        ConfigureRuntimeBoneCapsule(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HitboxType.RightArm, 0.10f, hitboxLayer);
        ConfigureRuntimeBoneCapsule(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HitboxType.RightArm, 0.10f, hitboxLayer);
        ConfigureRuntimeHandSphere(HumanBodyBones.LeftHand, HitboxType.LeftArm, 0.09f, hitboxLayer);
        ConfigureRuntimeHandSphere(HumanBodyBones.RightHand, HitboxType.RightArm, 0.09f, hitboxLayer);
        if (!anim.isHuman)
        {
            ConfigureRuntimeBoneCapsule(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HitboxType.LeftLeg, 0.1f, hitboxLayer);
            ConfigureRuntimeBoneCapsule(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HitboxType.LeftLeg, 0.095f, hitboxLayer);
            ConfigureRuntimeBoneCapsule(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HitboxType.RightLeg, 0.1f, hitboxLayer);
            ConfigureRuntimeBoneCapsule(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HitboxType.RightLeg, 0.095f, hitboxLayer);
            return;
        }
        ConfigureRuntimeLimb(HumanBodyBones.LeftUpperLeg, HitboxType.LeftLeg, 0.1f, 0.38f, -0.1f, hitboxLayer);
        ConfigureRuntimeLimb(HumanBodyBones.LeftLowerLeg, HitboxType.LeftLeg, 0.095f, 0.4f, -0.12f, hitboxLayer);
        ConfigureRuntimeLimb(HumanBodyBones.RightUpperLeg, HitboxType.RightLeg, 0.1f, 0.38f, -0.1f, hitboxLayer);
        ConfigureRuntimeLimb(HumanBodyBones.RightLowerLeg, HitboxType.RightLeg, 0.095f, 0.4f, -0.12f, hitboxLayer);
    }

    /// <summary>ConfigureRuntimeSphere 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EnsureRuntimeHitboxCoverage()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)。</param>
    /// <param name="center">中文含义：中心。使用位置：VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    private void ConfigureRuntimeSphere(HumanBodyBones bone, HitboxType type, float radius, Vector3 center, int layer)
    {
        // target（目标变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数、VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)。
        Transform target = AnimatorBoneBindings.Resolve(anim, bone);
        if (target == null)
        {
            return;
        }

        // hitCollider（命中的碰撞体）：保存 Component.GetComponent<SphereCollider>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)。
SphereCollider hitCollider = target.GetComponent<SphereCollider>();
        if (hitCollider == null)
        {
            hitCollider = target.gameObject.AddComponent<SphereCollider>();
        }

        hitCollider.radius = ToBoneColliderRadius(target, radius);
        hitCollider.center = center;
        hitCollider.isTrigger = true;
        ConfigureRuntimeHitbox(target, type, layer);
    }

    /// <summary>ConfigureRuntimeCapsule 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Mathf.Max(float, float)、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConfigureRuntimeLimb(HumanBodyBones, HitboxType, float, float, float, int)、VillagerController.EnsureRuntimeHitboxCoverage()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Mathf.Max(float, float)、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)。</param>
    /// <param name="height">中文含义：高度。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="center">中文含义：中心。使用位置：VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    private void ConfigureRuntimeCapsule(HumanBodyBones bone, HitboxType type, float radius, float height, Vector3 center, int layer)
    {
        // target（目标变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)。
        Transform target = AnimatorBoneBindings.Resolve(anim, bone);
        if (target == null)
        {
            return;
        }

        // hitCollider（命中的碰撞体）：保存 Component.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)。
CapsuleCollider hitCollider = target.GetComponent<CapsuleCollider>();
        if (hitCollider == null)
        {
            hitCollider = target.gameObject.AddComponent<CapsuleCollider>();
        }

        float localRadius = ToBoneColliderRadius(target, radius);
        float axisScale = Mathf.Max(0.0001f, Mathf.Abs(target.lossyScale.y));
        hitCollider.radius = localRadius;
        hitCollider.height = Mathf.Max(height / axisScale, localRadius * 2f);
        hitCollider.center = center;
        hitCollider.direction = 1;
        hitCollider.isTrigger = true;
        ConfigureRuntimeHitbox(target, type, layer);
    }

    /// <summary>ConfigureRuntimeLimb 的业务处理入口；本方法直接依赖 Vector3.Vector3(float, float, float)、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EnsureRuntimeHitboxCoverage()；下游（含内部回调体） Vector3.Vector3(float, float, float)、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数。</param>
    /// <param name="height">中文含义：高度。使用位置：作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数。</param>
    /// <param name="centerY">中文含义：中心纵向。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int) 的参数。</param>
    private void ConfigureRuntimeLimb(HumanBodyBones bone, HitboxType type, float radius, float height, float centerY, int layer)
    {
        ConfigureRuntimeCapsule(bone, type, radius, height, new Vector3(0f, centerY, 0f), layer);
    }

    /// <summary>ConfigureRuntimeArmCapsule 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EnsureRuntimeHitboxCoverage()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    /// <param name="nextBone">中文含义：下一次骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    private void ConfigureRuntimeBoneCapsule(HumanBodyBones bone, HumanBodyBones nextBone, HitboxType type, float radius, int layer)
    {
        // current（当前变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数、VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
        Transform current = AnimatorBoneBindings.Resolve(anim, bone);
        // next（下一个变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
        Transform next = AnimatorBoneBindings.Resolve(anim, nextBone);
        if (current == null || next == null)
        {
            return;
        }

        // localOffset（局部偏移量）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
Vector3 localOffset = current.InverseTransformPoint(next.position);
        // boneLength（骨骼长度）：使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
float boneLength = localOffset.magnitude;
        if ((next.position - current.position).sqrMagnitude < 0.000001f)
        {
            return;
        }

        // center（中心）：使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
Vector3 center = localOffset * 0.5f;
        // absX（绝对值横向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
float absX = Mathf.Abs(localOffset.x);
        // absY（绝对值纵向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
float absY = Mathf.Abs(localOffset.y);
        // absZ（绝对值深度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
float absZ = Mathf.Abs(localOffset.z);
        // direction（方向）：使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
int direction;
        if (absX >= absY && absX >= absZ)
        {
            direction = 0;
        }
        else
        {
            // selectedValue129（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
int selectedValue129;
            if (absY >= absZ)
            {
                selectedValue129 = 1;
            }
            else
            {
                selectedValue129 = 2;
            }

            direction = (selectedValue129);
        }

        // height（高度）：使用位置：作为 Mathf.Max(float, float) 的参数。
        float localRadius = ToBoneColliderRadius(current, radius);
        float height = boneLength + localRadius;
        // hitCollider（命中的碰撞体）：保存 Component.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)。
CapsuleCollider hitCollider = current.GetComponent<CapsuleCollider>();
        if (hitCollider == null)
        {
            hitCollider = current.gameObject.AddComponent<CapsuleCollider>();
        }

        hitCollider.radius = localRadius;
        hitCollider.height = Mathf.Max(height, localRadius * 2f);
        hitCollider.center = center;
        hitCollider.direction = direction;
        hitCollider.isTrigger = true;
        ConfigureRuntimeHitbox(current, type, layer);
    }

    /// <summary>ConfigureRuntimeHandSphere 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EnsureRuntimeHitboxCoverage()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()、VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数。</param>
    private void ConfigureRuntimeHandSphere(HumanBodyBones bone, HitboxType type, float radius, int layer)
    {
        // target（目标变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int) 的参数、VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)。
        Transform target = AnimatorBoneBindings.Resolve(anim, bone);
        if (target == null)
        {
            return;
        }

        // hitCollider（命中的碰撞体）：保存 Component.GetComponent<SphereCollider>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)。
SphereCollider hitCollider = target.GetComponent<SphereCollider>();
        if (hitCollider == null)
        {
            hitCollider = target.gameObject.AddComponent<SphereCollider>();
        }

        hitCollider.radius = ToBoneColliderRadius(target, radius);
        hitCollider.center = Vector3.zero;
        hitCollider.isTrigger = true;
        ConfigureRuntimeHitbox(target, type, layer);
    }

    /// <summary>ConfigureRuntimeHitbox 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConfigureRuntimeBoneCapsule(HumanBodyBones, HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeCapsule(HumanBodyBones, HitboxType, float, float, Vector3, int)、VillagerController.ConfigureRuntimeHandSphere(HumanBodyBones, HitboxType, float, int)、VillagerController.ConfigureRuntimeSphere(HumanBodyBones, HitboxType, float, Vector3, int)；下游（含内部回调体） Component.GetComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标变换节点。使用位置：VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。</param>
    private static float ToBoneColliderRadius(Transform bone, float radius)
    {
        // 原版骨架节点的缩放可能为 100，命中尺寸按场景米数配置。
        Vector3 scale = bone.lossyScale;
        float largestAxis = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return radius / Mathf.Max(0.0001f, largestAxis);
    }

    private static void ConfigureRuntimeHitbox(Transform target, HitboxType type, int layer)
    {
        target.gameObject.layer = layer;
        // hitbox（命中盒）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ConfigureRuntimeHitbox(Transform, HitboxType, int)。
EnemyHitbox hitbox = target.GetComponent<EnemyHitbox>();
        if (hitbox == null)
        {
            hitbox = target.gameObject.AddComponent<EnemyHitbox>();
        }

        hitbox.hitboxType = type;
    }

    /// <summary>
    /// 播放敌人语音（带冷却和并发限制）
    /// 死亡语音可以抢占普通受击冷却
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokePlayVoice(ZombieVoiceType)；下游（含内部回调体） EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">中文含义：语音类型。使用位置：作为 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType) 的参数。</param>
    private void PlayVoice(ZombieVoiceType voiceType)
    {
        animationPresenter.PlayVoice(voiceType);
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyColliderController.GetMaximumHorizontalReach()、Gizmos.DrawWireSphere(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // bodyReach（身体抵达）：使用位置：作为 Gizmos.DrawWireSphere(Vector3, float) 的参数、VillagerController.OnDrawGizmosSelected()。
float bodyReach;
        if (colliderCtrl != null)
        {
            bodyReach = colliderCtrl.GetMaximumHorizontalReach();
        }
        else
        {
            bodyReach = 0.35f;
        }

        Gizmos.DrawWireSphere(transform.position, bodyReach + attackContactPadding);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, attackStartRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }

    /// <summary>OnAttackComplete 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnAttackComplete()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnAttackComplete()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAttackComplete()
    {
        combatModule.HandleOnAttackComplete();
    }

    /// <summary>NotifyAttackStateMachineExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyAttackStateMachineExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.NotifyAttackStateMachineExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyAttackStateMachineExited()
    {
        combatModule.NotifyAttackStateMachineExited();
    }

    /// <summary>NotifyCrawlAttackStateExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyCrawlAttackStateExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.NotifyCrawlAttackStateExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyCrawlAttackStateExited()
    {
        combatModule.NotifyCrawlAttackStateExited();
    }

    /// <summary>OnAttackStateMachineExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnAttackStateMachineExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnAttackStateMachineExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAttackStateMachineExit()
    {
        combatModule.HandleOnAttackStateMachineExit();
    }

    /// <summary>OnCrawlAttackExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnCrawlAttackExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnCrawlAttackExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnCrawlAttackExit()
    {
        combatModule.HandleOnCrawlAttackExit();
    }

    /// <summary>OnColliderStand 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnColliderStand()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnColliderStand()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnColliderStand()
    {
        combatModule.HandleOnColliderStand();
    }

    /// <summary>OnColliderCrawl 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnColliderCrawl()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnColliderCrawl()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnColliderCrawl()
    {
        combatModule.HandleOnColliderCrawl();
    }

    /// <summary>OnColliderDisable 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnColliderDisable()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnColliderDisable()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnColliderDisable()
    {
        combatModule.HandleOnColliderDisable();
    }

    /// <summary>OnHitComplete 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnHitComplete()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnHitComplete()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnHitComplete()
    {
        combatModule.HandleOnHitComplete();
    }

    /// <summary>UpdateStandingHitCompletion 的业务处理入口；本方法直接依赖 EnemyCombatModule.UpdateStandingHitCompletion()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyCombatModule.UpdateStandingHitCompletion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateStandingHitCompletion()
    {
        combatModule.UpdateStandingHitCompletion();
    }

    /// <summary>NotifyHitStateMachineExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyHitStateMachineExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.NotifyHitStateMachineExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyHitStateMachineExited()
    {
        combatModule.NotifyHitStateMachineExited();
    }

    /// <summary>OnHitStateMachineExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnHitStateMachineExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnHitStateMachineExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnHitStateMachineExit()
    {
        combatModule.HandleOnHitStateMachineExit();
    }

    /// <summary>OnStandUpComplete 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnStandUpComplete()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnStandUpComplete()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnStandUpComplete()
    {
        combatModule.HandleOnStandUpComplete();
    }

    /// <summary>NotifyStandUpStateExited 的业务处理入口；本方法直接依赖 EnemyCombatModule.NotifyStandUpStateExited()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.NotifyStandUpStateExited()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyStandUpStateExited()
    {
        combatModule.NotifyStandUpStateExited();
    }

    /// <summary>OnStandUpStateExit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnStandUpStateExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnStandUpStateExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnStandUpStateExit()
    {
        combatModule.HandleOnStandUpStateExit();
    }

    /// <summary>OnAttackHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnAttackHit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnAttackHit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAttackHit()
    {
        combatModule.HandleOnAttackHit();
    }

    /// <summary>OnParryWindowOpen 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnParryWindowOpen()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnParryWindowOpen()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnParryWindowOpen()
    {
        combatModule.HandleOnParryWindowOpen();
    }

    /// <summary>OnParryWindowClose 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnParryWindowClose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnParryWindowClose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnParryWindowClose()
    {
        combatModule.HandleOnParryWindowClose();
    }

    /// <summary>OnAttackFirstHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnAttackFirstHit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnAttackFirstHit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAttackFirstHit()
    {
        combatModule.HandleOnAttackFirstHit();
    }

    /// <summary>OnAttackSecondHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.HandleOnAttackSecondHit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.HandleOnAttackSecondHit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAttackSecondHit()
    {
        combatModule.HandleOnAttackSecondHit();
    }

    /// <summary>TryApplyAttackHit 的业务处理入口；本方法直接依赖 EnemyCombatModule.TryApplyAttackHit(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.TryApplyAttackHit(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitIndex">中文含义：命中索引。使用位置：作为 EnemyCombatModule.TryApplyAttackHit(int) 的参数。</param>
    void TryApplyAttackHit(int hitIndex)
    {
        combatModule.TryApplyAttackHit(hitIndex);
    }

    /// <summary>ApplyKnifeParryStagger 的业务处理入口；本方法直接依赖 EnemyCombatModule.ApplyKnifeParryStagger(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.ApplyKnifeParryStagger(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="perfect">中文含义：是否完美。使用位置：作为 EnemyCombatModule.ApplyKnifeParryStagger(bool) 的参数。</param>
    private void ApplyKnifeParryStagger(bool perfect)
    {
        combatModule.ApplyKnifeParryStagger(perfect);
    }

    /// <summary>UpdateStandingAttackHitFallback 的业务处理入口；本方法直接依赖 EnemyCombatModule.UpdateStandingAttackHitFallback()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyCombatModule.UpdateStandingAttackHitFallback()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateStandingAttackHitFallback()
    {
        combatModule.UpdateStandingAttackHitFallback();
    }

    /// <summary>IsStandingAttackAnimation 的业务处理入口；本方法直接依赖 EnemyCombatModule.IsStandingAttackAnimation(AnimatorStateInfo)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.IsStandingAttackAnimation(AnimatorStateInfo)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateInfo">中文含义：当前动画状态信息。使用位置：作为 EnemyCombatModule.IsStandingAttackAnimation(AnimatorStateInfo) 的参数。</param>
    private static bool IsStandingAttackAnimation(AnimatorStateInfo stateInfo)
    {
        return EnemyCombatModule.IsStandingAttackAnimation(stateInfo);
    }

    /// <summary>TryApplyFriendlyFire 的业务处理入口；本方法直接依赖 EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float) 的参数。</param>
    /// <param name="forward">中文含义：前向。使用位置：作为 EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float) 的参数。</param>
    /// <param name="baseDamage">中文含义：基础伤害。使用位置：作为 EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float) 的参数。</param>
    private bool TryApplyFriendlyFire(Vector3 origin, Vector3 forward, float baseDamage)
    {
        return combatModule.TryApplyFriendlyFire(origin, forward, baseDamage);
    }

    /// <summary>OnAnimatorMove 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、float.IsInfinity(float)、float.IsNaN(float)、Mathf.Exp(float)、Mathf.Lerp(float, float, float)、Mathf.MoveTowards(float, float, float)、VillagerController.MaintainGroundContact() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、float.IsInfinity(float)、float.IsNaN(float)、Mathf.Exp(float)、Mathf.Lerp(float, float, float)、Mathf.MoveTowards(float, float, float)、VillagerController.MaintainGroundContact() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnAnimatorMove()
    {
        if (combatModule != null && (combatModule.NativeCombatActive || combatModule.NativeDeathActive))
        {
            return;
        }
        if (anim == null)
        {
            return;
        }
        if (IsCombatFalling && !IsQTEControlled)
        {
            return;
        }

        // deltaPos（增量位置）：使用位置：作为 CharacterMotionExecutor.CommitNormalPosition(Vector3) 的参数、作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数、作为 VillagerController.TryExecuteStairActionDisplacement(Vector3) 的参数 等共 4 处。
Vector3 deltaPos = anim.deltaPosition;
        // 刺杀时只消费动画提供的水平位移，垂直方向仍由贴地锁定负责。
        // 其他快速反应事件继续由配对协调器完全控制位置。
        if (qteGroundLocked)
        {
            if (qteParticipant != null && qteParticipant.ShouldConsumeAssassinationRootMotion)
            {
                deltaPos.y = 0f;
                motionExecutor.CommitNormalPosition(transform.position + deltaPos);
            }

            MaintainGroundContact();
            return;
        }

        // diagPrePos（诊断前置位置）：使用位置：VillagerController.OnAnimatorMove()。
Vector3 diagPrePos = transform.position;
        switch (CurrentMovementMode)
        {
            case EnemyMovementMode.FullyStopped:
                MaintainGroundContact();
                break;
            case EnemyMovementMode.StandingPathRootMotion:
            {
                // standingDelta（站立增量）：使用位置：VillagerController.OnAnimatorMove()。
Vector3 standingDelta = deltaPos;
                standingDelta.y = 0f;
                if (UsesNativeStandingStride)
                {
                    // 根位移与支撑脚使用同一帧姿势，避开上一帧步幅滞后造成的滑动。
                    float stride = standingDelta.magnitude;
                    if (animationPresenter.HasRevivedGait)
                    {
                        stride = animationPresenter.RevivedGaitStride;
                    }
                    UpdateCodeDrivenMovement(stride);
                    break;
                }
                // sampledSpeed（采样到的速度）：使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 4 处。
float sampledSpeed;
                if (Time.deltaTime > 0.0001f)
                {
                    sampledSpeed = standingDelta.magnitude / Time.deltaTime;
                }
                else
                {
                    sampledSpeed = 0f;
                }

                if (sampledSpeed > 0.001f && !float.IsNaN(sampledSpeed) && !float.IsInfinity(sampledSpeed))
                {
                    standingRootMotionSpeed = Mathf.Lerp(standingRootMotionSpeed, sampledSpeed, 1f - Mathf.Exp(-16f * Time.deltaTime));
                    hasStandingRootMotionSpeed = true;
                }
                else if (hasStandingRootMotionSpeed)
                {
                    standingRootMotionSpeed = Mathf.MoveTowards(standingRootMotionSpeed, 0f, 12f * Time.deltaTime);
                }

                break;
            }

            case EnemyMovementMode.ActionRootMotion:
                deltaPos.y = 0f;
                // strictAttackCollision（是否严格攻击碰撞）：使用位置：作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数。
                bool strictAttackCollision = CurrentState == VillagerState.Attack || CurrentState == VillagerState.CrawlAttack
                    || CurrentState == VillagerState.Staggered;
                if (CurrentState == VillagerState.Attack)
                {
                    deltaPos = combatModule.AdjustStandingKnifeRootMotion(deltaPos);
                }

                if (!TryExecuteStairActionDisplacement(deltaPos))
                {
                    MoveWithCollision(deltaPos, strictAttackCollision);
                }

                MaintainGroundContact();
                break;
            case EnemyMovementMode.CrawlCodeDriven:
                break;
            case EnemyMovementMode.FullRootMotion:
                if (IsDead)
                {
                    if (!corpseSupportApplied)
                    {
                        Vector3 step = Vector3.ProjectOnPlane(deltaPos, Vector3.up);
                        float distance = step.magnitude;
                        if (distance > 0.0001f && TryGetMovementCapsule(out Vector3 top, out Vector3 bottom, out float radius, true)
                            && TryFindMovementBlocker(top, bottom, radius, step / distance, distance + movementCollisionSkin,
                                out RaycastHit hit, false, true, true))
                        {
                            step = step / distance * Mathf.Clamp(hit.distance - movementCollisionSkin, 0f, distance);
                        }
                        motionExecutor.CommitNormalPosition(transform.position + step);
                        CommitCombatGroundMotion(transform.rotation, 0f, out _);
                    }
                }
                else if (!TryExecuteStairActionDisplacement(deltaPos))
                {
                    // 倒地受击的根轨含俯身和后退。水平位移受墙面阻挡，脚底高度由踏面接管。
                    MoveWithCollision(deltaPos, true, false);
                    MaintainGroundContact();
                }
                break;
        }

        // 缓冲到采样时输出，不每帧打印
        if (enableMovementDiagnostic)
        {
            diagOnAnimMoveCalled = true;
            diagOnAnimMoveMode = CurrentMovementMode;
            diagOnAnimMoveDeltaPos = deltaPos;
            diagOnAnimMovePrePos = diagPrePos;
            diagOnAnimMovePostPos = transform.position;
        }
    }

    /// <summary>
    /// 站立敌人仍通过代码完成碰撞与避让，但每帧推进距离取自动画根位移采样。
    /// 若动画尚未产出首帧根位移才使用寻路速度，避免状态切换首帧原地停顿。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fallbackSpeed">中文含义：后备速度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数、VillagerController.ResolveStandingRootMotionSpeed(float)。</param>
    private float ResolveStandingRootMotionSpeed(float fallbackSpeed)
    {
        if (!useStandingAnimationSpeedForPath)
        {
            return Mathf.Max(0f, fallbackSpeed);
        }

        if (!hasStandingRootMotionSpeed || float.IsNaN(standingRootMotionSpeed) || float.IsInfinity(standingRootMotionSpeed))
        {
            return fallbackSpeed;
        }

        // 停步动画可能短暂给出零速度；有效追击不能反过来永远等待动画起步。
        if (CurrentState == VillagerState.Chase && fallbackSpeed > 0f)
        {
            return Mathf.Clamp(standingRootMotionSpeed, fallbackSpeed * .9f, fallbackSpeed);
        }

        return Mathf.Max(standingRootMotionSpeed, Mathf.Max(0f, fallbackSpeed) * 0.15f);
    }

    /// <summary>UpdateLocomotionPlaybackRate 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementStartPosition">中文含义：移动起始位置。使用位置：作为 EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3) 的参数。</param>
    private void UpdateLocomotionPlaybackRate(Vector3 movementStartPosition)
    {
        animationPresenter.UpdateLocomotionPlaybackRate(movementStartPosition);
    }

    /// <summary>UpdateCodeDrivenMovement 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyContext.ClampStandingMoveParameter(float)、EnemyContext.MoveSpeedToAgentSpeed(float)、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)、Mathf.Max(float, float)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Vector3.ClampMagnitude(Vector3, float) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyContext.ClampStandingMoveParameter(float)、EnemyContext.MoveSpeedToAgentSpeed(float)、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)、Mathf.Max(float, float)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Vector3.ClampMagnitude(Vector3, float) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateCodeDrivenMovement(float nativeStandingDistance = 0f)
    {
        actionCoordinator.ApproveMotionMode(CurrentMovementMode);
        // diagPrePos（诊断前置位置）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
Vector3 diagPrePos = transform.position;
        if (CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion)
        {
            if (chainsawBridge && chainsawBridge.InPlaceIntimidationMiddle)
            {
                MaintainGroundContact();
                return;
            }

            ResolveCrowdOverlap();
            // moveDirection（移动方向）：保存 VillagerController.ResolveStandingPathDirectionForCurrentFrame() 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3) 的参数、作为 VillagerController.TryOpenDoorOnPath(Vector3) 的参数、VillagerController.UpdateCodeDrivenMovement()。
Vector3 moveDirection = ResolveStandingPathDirectionForCurrentFrame();
            TryOpenDoorOnPath(moveDirection);
            if (agent != null && agent.enabled)
            {
                // hasPathDirection（是否具有路径方向）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
bool hasPathDirection = !agent.isStopped && moveDirection.sqrMagnitude > 0.01f;
                // movementSpeed（移动速度）：保存 VillagerController.ResolveStandingRootMotionSpeed(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.UpdateCodeDrivenMovement()。
float movementSpeed = ResolveStandingRootMotionSpeed(agent.speed);
                // pursuitIngressNeedsMovement（是否追击入口需要移动）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
bool pursuitIngressNeedsMovement = pursuitIngressDoor != null && hasPathDirection;
                if (pursuitIngressNeedsMovement && movementSpeed <= 0.001f && !UsesNativeStandingStride)
                {
                    // 门框碰撞可能让行走动画暂时隐藏，而站立位移速度又读取
                    // 动画根运动。若不在门洞阶段补回追击速度，动画与位移会
                    // 相互等待并永久停在门前。仅对仍有门洞方向的敌人生效，
                    // 已抵达排队点的后续敌人继续保持原位。
                    movementSpeed = ctx.MoveSpeedToAgentSpeed(ctx.ClampStandingMoveParameter(GetTargetMoveSpeed()));
                }

                if (!UsesNativeStandingStride)
                {
                    movementSpeed = Mathf.Max(0f, movementSpeed - EnemyContext.groundDecelerationAmount);
                }
                // selectedValue133（分支选出的移动方向）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.UpdateCodeDrivenMovement()。
global::UnityEngine.Vector3 selectedValue133;
                if (hasPathDirection)
                {
                    selectedValue133 = moveDirection;
                }
                else
                {
                    selectedValue133 = transform.forward;
                }

                // displacement（位移）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
                float distance = movementSpeed * Time.deltaTime;
                if (UsesNativeStandingStride)
                {
                    distance = nativeStandingDistance;
                }
Vector3 displacement = (selectedValue133) * distance;
                {
                    // 这些向量同时供低频时间轴使用，避免停滞时只看到意图速度。
                    diagStandingPathIntent = hasPathDirection;
                    diagStandingRequestedDisplacement = displacement;
                    diagStandingAvoidanceDisplacement = Vector3.zero;
                    diagStandingFacingLimitedDisplacement = Vector3.zero;
                }

                if (hasPathDirection)
                {
                    // 门洞队首和正在穿门的敌人必须沿门洞中心推进。此时若再
                    // 叠加侧向避让，会在门框前反复换边并堵住后续整条队列。
                    // ownsPursuitIngressLane（是否持有追击入口通行带）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
bool ownsPursuitIngressLane = pursuitIngressDoor != null && (pursuitIngressCrossing || IsPursuitIngressQueueHead());
                    // isCloseDirectPursuit（是否关闭直接追击）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
bool isCloseDirectPursuit = standingDirectPursuitForFrame;
                    // directPursuitDirection（直接追击方向）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
Vector3 directPursuitDirection = moveDirection;
                    // 窄通路沿中心推进，开阔处允许同伴避让。
                    // 身体朝向跟随本帧真实移动方向，短暂侧向目标完成后恢复直追，
                    // 避免后方一直朝同一条直线挤压停步的同伴。
                    // bypassLateralAvoidance（是否绕过侧向避障）：使用位置：VillagerController.UpdateCodeDrivenMovement()。
bool bypassLateralAvoidance = ownsPursuitIngressLane || (agent != null && agent.RequiresPrecisePathFollowing);
                    // pursuitDisplacement（追击位移）：使用位置：作为 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3) 的参数、VillagerController.UpdateCodeDrivenMovement()。
Vector3 pursuitDisplacement;
                    if (isCloseDirectPursuit)
                    {
                        pursuitDisplacement = directPursuitDirection * displacement.magnitude;
                    }
                    else
                    {
                        pursuitDisplacement = displacement;
                    }

                    // avoidanceDisplacement（避障位移）：使用位置：作为 VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3) 的参数、作为 VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3) 的参数、VillagerController.UpdateCodeDrivenMovement()。
Vector3 avoidanceDisplacement;
                    if (bypassLateralAvoidance)
                    {
                        avoidanceDisplacement = pursuitDisplacement;
                    }
                    else
                    {
                        avoidanceDisplacement = ResolveLocalAvoidanceDisplacement(pursuitDisplacement);
                    }

                    if (!bypassLateralAvoidance)
                    {
                        avoidanceDisplacement = ConstrainAvoidanceToPursuitDirection(avoidanceDisplacement, moveDirection);
                    }

                    // 身体必须持续对齐实际追击方向，再计算前进限速。
                    // 仅在完全没有位移时才转向，会长期保留侧移或后退姿态。
                    // facingDirection（朝向方向）：使用位置：作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数。
Vector3 facingDirection = avoidanceDisplacement.normalized;
                    RotateTowardsDirection(facingDirection, ResolveStandingPursuitFacingTurnRate());
                    // facingLimitedDisplacement（朝向受限位移）：保存 VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3) 的参数、作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数 等共 4 处。
Vector3 facingLimitedDisplacement = ResolveFacingLimitedStandingDisplacement(avoidanceDisplacement);
                    // 高速追击时也必须准确落到窄通路端点，避免跨过端点后反复折返。
                    if (agent.RequiresPrecisePathFollowing && agent.TryGetCurrentWaypoint(out Vector3 /* 中文含义：精确路径点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ preciseWaypoint))
                    {
                        // waypointOffset（路径点偏移量）：使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、VillagerController.UpdateCodeDrivenMovement()。
Vector3 waypointOffset = preciseWaypoint - transform.position;
                        waypointOffset.y = 0f;
                        facingLimitedDisplacement = Vector3.ClampMagnitude(facingLimitedDisplacement, waypointOffset.magnitude);
                    }

                    {
                        diagStandingAvoidanceDisplacement = avoidanceDisplacement;
                        diagStandingFacingLimitedDisplacement = facingLimitedDisplacement;
                    }

                    if (HouseThreeRegionNavigation.Current != null && !HouseThreeRegionNavigation.Current.StairAllowFrontEnter(this, facingLimitedDisplacement))
                    {
                        facingLimitedDisplacement = Vector3.zero;
                    }

                    // actualDisplacement（实际位移）：保存 VillagerController.MoveWithCollision(Vector3, bool, bool) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.UpdateStandingFacingFromActualDisplacement(Vector3) 的参数、VillagerController.UpdateCodeDrivenMovement()。
Vector3 actualDisplacement = MoveWithCollision(facingLimitedDisplacement, false, false);
                    if (actualDisplacement.sqrMagnitude > 0.000001f)
                    {
                        UpdateStandingFacingFromActualDisplacement(actualDisplacement);
                    }
                    else
                    {
                        RotateTowardsDirection(facingDirection, ResolveStandingPursuitFacingTurnRate());
                    }
                }
            }

            MaintainGroundContact();
            if (enableMovementDiagnostic && agent != null)
            {
                diagUpdateCDPrePos = diagPrePos;
                diagUpdateCDPostPos = transform.position;
                diagUpdateCDDesiredDir = agent.desiredDirection;
                diagUpdateCDSpeed = agent.speed;
                diagUpdateCDTag = "StandingPathRootMotion";
            }

            return;
        }

        if (CurrentMovementMode != EnemyMovementMode.CrawlCodeDriven)
        {
            return;
        }

        // moveDir（移动方向）：保存 VillagerController.ResolveStableCrawlMoveDirection() 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3) 的参数、作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数、作为 VillagerController.TryOpenDoorOnPath(Vector3) 的参数 等共 4 处。
Vector3 moveDir = ResolveStableCrawlMoveDirection();
        TryOpenDoorOnPath(moveDir);
        if (moveDir != Vector3.zero)
        {
            RotateTowardsDirection(moveDir, ResolveCrawlFacingTurnRate());
            // finalDisplacement（最终位移）：保存 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数。
Vector3 finalDisplacement = ResolveLocalAvoidanceDisplacement(moveDir * ctx.CrawlSpeed * Time.deltaTime);
            MoveWithCollision(finalDisplacement, false, false);
        }

        MaintainGroundContact();
        if (enableMovementDiagnostic)
        {
            diagUpdateCDPrePos = diagPrePos;
            diagUpdateCDPostPos = transform.position;
            diagUpdateCDDesiredDir = moveDir;
            if (ctx != null)
            {
                diagUpdateCDSpeed = ctx.CrawlSpeed;
            }
            else
            {
                diagUpdateCDSpeed = 0f;
            }

            diagUpdateCDTag = "CrawlCodeDriven";
        }
    }

    /// <summary>UpdateChaseProgressRecovery 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Text(string, string)、EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、Mathf.Max(float, float)、Mathf.Min(int, int) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） CombatDiagnosticFields.Text(string, string)、EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、Mathf.Max(float, float)、Mathf.Min(int, int) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateChaseProgressRecovery()
    {
        if (chainsawBridge && chainsawBridge.InPlaceIntimidationMiddle)
        {
            chaseProgressInitialized = false;
            return;
        }

        // expectsMovement（是否期望移动）：使用位置：VillagerController.UpdateChaseProgressRecovery()。
bool expectsMovement = CurrentState == VillagerState.Chase && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && ShouldChasePlayer && agent != null && agent.enabled && !agent.isStopped && agent.HasDestination && ((agent.CurrentPathState == PathAgentState.Valid && agent.remainingDistance > Mathf.Max(0.2f, agent.waypointReachedRadius)) || HasIncompletePursuitArrival);
        if (!expectsMovement)
        {
            chaseProgressInitialized = false;
            chaseNoProgressDuration = 0f;
            chaseRecoveryAttempts = 0;
            return;
        }

        if (!chaseProgressInitialized)
        {
            chaseProgressInitialized = true;
            chaseProgressPosition = transform.position;
            chaseBestRemainingDistance = agent.remainingDistance;
            chaseProgressDestination = agent.Destination;
            chaseNoProgressDuration = 0f;
            chaseRecoveryAttempts = 0;
            return;
        }

        if ((agent.Destination - chaseProgressDestination).sqrMagnitude >= agent.destinationChangeThreshold * agent.destinationChangeThreshold)
        {
            chaseProgressPosition = transform.position;
            chaseBestRemainingDistance = agent.remainingDistance;
            chaseProgressDestination = agent.Destination;
            chaseNoProgressDuration = 0f;
            chaseRecoveryAttempts = 0;
            return;
        }

        // currentRemainingDistance（当前剩余距离）：使用位置：VillagerController.UpdateChaseProgressRecovery()。
float currentRemainingDistance = agent.remainingDistance;
        // requiredProgress（所需进度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.UpdateChaseProgressRecovery()。
float requiredProgress = Mathf.Max(0.01f, chaseProgressDistance);
        // movedEnough（是否已移动足够）：使用位置：VillagerController.UpdateChaseProgressRecovery()。
bool movedEnough = Vector3.Distance(transform.position, chaseProgressPosition) >= requiredProgress;
        // reducedRemainingDistance（是否降低后的剩余距离）：使用位置：VillagerController.UpdateChaseProgressRecovery()。
bool reducedRemainingDistance = chaseBestRemainingDistance - currentRemainingDistance >= requiredProgress;
        if (movedEnough && (reducedRemainingDistance || IsInsideTraversalBottleneck(Vector3.zero)))
        {
            chaseProgressPosition = transform.position;
            chaseBestRemainingDistance = currentRemainingDistance;
            chaseNoProgressDuration = 0f;
            chaseRecoveryAttempts = 0;
            return;
        }

        chaseNoProgressDuration += Time.deltaTime;
        if (chaseNoProgressDuration < Mathf.Max(0.2f, chaseProgressTimeout) || Time.time < nextChaseRecoveryTime)
        {
            return;
        }

        if (specialTraversal != null && specialTraversal.TryReadPlatformExitClearance(out _) &&
            agent.CurrentPathState == PathAgentState.Valid)
        {
            // 窗外窄处的前一名村民可能仍在离开窗台，先维持当前路径。
            return;
        }

        nextChaseRecoveryTime = Time.time + Mathf.Max(0.1f, chaseRecoveryInterval);
        chaseNoProgressDuration = 0f;
        chaseRecoveryAttempts = Mathf.Min(chaseRecoveryAttempts + 1, 1);
        if (HasIncompletePursuitArrival)
        {
            // 到达了错误投影点时，remainingDistance 为零，不能沿用
            // 正常路径的进度判定；低频重投影后继续保持追击意图。
            TryRecoverIncompletePursuitArrival();
            chaseProgressPosition = transform.position;
            chaseBestRemainingDistance = agent.remainingDistance;
            chaseProgressDestination = agent.Destination;
            return;
        }

        // 有效路径不等于真实追击。超过时限没有缩短路径时，立即废弃
        // 旧目标缓存并重新请求以里昂为终点的路径，不能只重置计时。
        lastStableStandingMoveDirection = Vector3.zero;
        agent.InvalidatePursuitResolutionCache();
        // receiverValue137（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.UpdateChaseProgressRecovery()。
global::EnemyAIManager receiverValue137 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue137, null))
        {
            receiverValue137.InvalidatePursuitDestination(this);
        }

        // targetChanged（目标是否发生变化）：保存 VillagerController.RefreshChaseDestination() 的返回结果，供当前步骤使用。使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.UpdateChaseProgressRecovery()。
bool targetChanged = RefreshChaseDestination();
        // requested（是否已经申请）：使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数。
bool requested = targetChanged || agent.ForceRepath();
        if (EnemyCombatTimeline.IsRecord(this))
        {
            EnemyCombatTimeline.Record(this, "停滞路径重算", CombatDiagnosticFields.Text("目标变化", EnemyCombatTimeline.Describe(targetChanged)), CombatDiagnosticFields.Text("请求已发起", EnemyCombatTimeline.Describe(requested)));
        }

        chaseProgressPosition = transform.position;
        chaseBestRemainingDistance = agent.remainingDistance;
        chaseProgressDestination = agent.Destination;
    }

    /// <summary>ResolveStableCrawlMoveDirection 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Vector3.Normalize()、Vector3.RotateTowards(Vector3, Vector3, float, float)、VillagerController.IsPlayerWithinAttackStartRange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Mathf.Max(float, float)、Vector3.Normalize()、Vector3.RotateTowards(Vector3, Vector3, float, float)、VillagerController.IsPlayerWithinAttackStartRange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveStableCrawlMoveDirection()
    {
        if (ctx == null || !ctx.HasCrawlMoveIntent)
        {
            lastStableCrawlMoveDirection = Vector3.zero;
            crawlFacingInitialized = false;
            return Vector3.zero;
        }

        // candidate（候选）：使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、VillagerController.ResolveStableCrawlMoveDirection()。
Vector3 candidate = Vector3.zero;
        // shouldFacePlayerForCloseCombat（是否应当面玩家用于关闭战斗）：使用位置：VillagerController.ResolveStableCrawlMoveDirection()。
bool shouldFacePlayerForCloseCombat = CanEvaluateCrawlCombatAction && IsPlayerWithinAttackStartRange() && ctx.Player != null;
        // hasReliablePathDirection（是否具有可靠路径方向）：使用位置：VillagerController.ResolveStableCrawlMoveDirection()。
bool hasReliablePathDirection = agent != null && agent.enabled && !agent.isStopped && agent.CurrentPathState == PathAgentState.Valid && agent.desiredDirection.sqrMagnitude > 0.01f;
        // 进入攻击距离后，朝向必须直接追随里昂。此时若继续沿用路径
        // 最后一个切线方向，爬行敌人会停在里昂脚边却始终达不到攻击
        // 朝向条件，行为树也就无法切入爬行攻击或咬腿交互。
        if (shouldFacePlayerForCloseCombat)
        {
            candidate = ctx.Player.transform.position - transform.position;
            candidate.y = 0f;
        }
        else if (hasReliablePathDirection)
        {
            candidate = agent.desiredDirection;
        }
        else if (ctx.Player != null)
        {
            candidate = ctx.Player.transform.position - transform.position;
            candidate.y = 0f;
        }

        candidate.y = 0f;
        if (candidate.sqrMagnitude <= 0.01f)
        {
            return Vector3.zero;
        }

        candidate.Normalize();
        if (lastStableCrawlMoveDirection.sqrMagnitude <= 0.01f)
        {
            lastStableCrawlMoveDirection = candidate;
            return lastStableCrawlMoveDirection;
        }

        // maxRadians（最大弧度）：使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数。
float maxRadians = Mathf.Deg2Rad * Mathf.Max(1f, crawlDirectionTurnRate) * Time.deltaTime;
        lastStableCrawlMoveDirection = Vector3.RotateTowards(lastStableCrawlMoveDirection, candidate, maxRadians, 0f);
        if (lastStableCrawlMoveDirection.sqrMagnitude > 0.01f)
        {
            lastStableCrawlMoveDirection.Normalize();
        }

        return lastStableCrawlMoveDirection;
    }

    /// <summary>SetMovementMode 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyAnimationAdapter.SetRootMotion(bool)、EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.RequestMovementMode(EnemyMovementMode)、EnemyWindowTraversal.BeginTraversalState()、EnemyWindowTraversal.CancelTraversal(bool)、EnemyWindowTraversal.LateUpdate()、EnemyWindowTraversal.Update() 等共 16 处；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyAnimationAdapter.SetRootMotion(bool)、EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementMode">中文含义：移动模式。使用位置：作为 EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode) 的参数、作为 EnemyAnimationAdapter.SetRootMotion(bool) 的参数、作为 EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode) 的参数 等共 5 处。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 EnemyCombatTimeline.Describe(string) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。</param>
    public void SetMovementMode(EnemyMovementMode movementMode, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        // prev（前一次）：使用位置：作为 EnemyCombatTimeline.Describe<EnemyMovementMode>(EnemyMovementMode) 的参数、作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.SetMovementMode(EnemyMovementMode, string, int)。
EnemyMovementMode prev = CurrentMovementMode;
        actionCoordinator.ApproveMotionMode(movementMode);
        if (prev != movementMode)
        {
            if (EnemyCombatTimeline.IsRecord(this))
            {
                EnemyCombatTimeline.Record(this, "移动模式切换", CombatDiagnosticFields.Text("原移动", EnemyCombatTimeline.Describe(prev)), CombatDiagnosticFields.Text("新移动", EnemyCombatTimeline.Describe(movementMode)), CombatDiagnosticFields.Text("入口", EnemyCombatTimeline.Describe(source)), CombatDiagnosticFields.Count("调用行号", line));
            }
        }

        standingPathDirectionFrame = -1;
        standingPathDirectionForFrame = Vector3.zero;
        if (anim != null)
        {
            // 站立追击的位置和朝向都由代码与路径共同驱动。保留动画根旋转
            // 会在脚本校正后再次覆盖身体方向，造成侧移、抽动或原地转圈。
            // 攻击、受击、死亡等动作状态仍保留原有根运动。
            animationPresenter.SetRootMotion(useRootMotion &&
                (movementMode != EnemyMovementMode.StandingPathRootMotion || UsesNativeStandingStride));
        }

        if (enableMovementDiagnostic && prev != movementMode)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("移动模式切换", $"{prev} → {movementMode}, 状态={observableState}");
            }
        }
    }

    /// <summary>RotateTowardsPath 的业务处理入口；本方法直接依赖 VillagerController.ResolveStandingPursuitFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ResolveStandingPursuitFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RotateTowardsPath()
    {
        if (agent == null || !agent.enabled)
        {
            return false;
        }

        // desired（期望）：使用位置：作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数、VillagerController.RotateTowardsPath()。
Vector3 desired = agent.desiredDirection;
        desired.y = 0f;
        if (desired.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        RotateTowardsDirection(desired, ResolveStandingPursuitFacingTurnRate());
        return true;
    }

    /// <summary>ResolveStandingPathDirection 的业务处理入口；本方法直接依赖 DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)、Mathf.Max(float, float)、Vector3.Normalize()、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.StabilizeStandingDirection(Vector3)、VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)、VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirectionForCurrentFrame()；下游（含内部回调体） DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)、Mathf.Max(float, float)、Vector3.Normalize()、VillagerController.IsApproachingEnemyStairBridge()、VillagerController.StabilizeStandingDirection(Vector3)、VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)、VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveStandingPathDirection()
    {
        if (agent == null || !agent.enabled || agent.isStopped)
        {
            lastStableStandingMoveDirection = Vector3.zero;
            return Vector3.zero;
        }

        // desired（期望）：使用位置：作为 VillagerController.StabilizeStandingDirection(Vector3) 的参数、VillagerController.ResolveStandingPathDirection()。
Vector3 desired = agent.desiredDirection;
        desired.y = 0f;
        if (desired.sqrMagnitude <= 0.0001f)
        {
            if (TryResolvePursuitIngressFinalSteering(out Vector3 /* 中文含义：入口转向方向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ ingressSteeringDirection))
            {
                lastStableStandingMoveDirection = ingressSteeringDirection;
                return ingressSteeringDirection;
            }

            if (TryResolveCloseDirectPursuitDirection(out Vector3 /* 中文含义：直接追击方向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ directPursuitDirection))
            {
                return StabilizeStandingDirection(directPursuitDirection);
            }

            lastStableStandingMoveDirection = Vector3.zero;
            return Vector3.zero;
        }

        desired.Normalize();
        // 固定入口队列和穿门阶段已经把目标限制在门外、门洞、门内三点。
        // 此时通用门洞推算会再次按门模型朝向生成一个侧向向量，覆盖正确的
        // 寻路方向，造成队首在门外横移、反复摆动并永久占用队列。专用入口
        // 必须独占方向控制，后续敌人才会在队首穿门后依次推进。
        if (pursuitIngressDoor != null)
        {
            lastStableStandingMoveDirection = desired;
            return desired;
        }

        if (DoorController.TryResolveOpenPassageDirection(transform.position, agent.Destination, Mathf.Max(1.25f, doorOpenProbeRadius + 0.35f), out Vector3 /* 中文含义：通道方向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ passageDirection))
        {
            lastStableStandingMoveDirection = passageDirection;
            return passageDirection;
        }

        // 追击方向只能来自已经确认的路径。旧的贴墙侧移会在同一帧用碰撞
        // 法线覆盖路径方向，进而与高代价重算交替改向，造成原地打转。
        // 真实受阻由 MoveWithCollision 登记危险区，路径完成前保持这一追击朝向。
        return StabilizeStandingDirection(desired);
    }

    /// <summary>
    /// 寻路代理会在门外排队点的抵达半径边缘提前结束路径。
    /// 此后沿当前门洞阶段的精确目标继续短距离推进，直到真正对齐并穿门。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirection()；下游（含内部回调体） Vector3.Normalize()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。输出参数，由本入口写入并交给调用方。使用位置：作为 VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3) 的参数、VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)。</param>
    private bool TryResolvePursuitIngressFinalSteering(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (pursuitIngressDoor == null || agent == null || agent.CurrentPathState != PathAgentState.Arrived)
        {
            return false;
        }

        if (TryResolveDoor03IngressFinalSteering(out direction))
        {
            return true;
        }

        // player（玩家）：使用位置：VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)。
LeonController player = Player;
        // targetPosition（目标位置）：使用位置：作为 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3) 的参数、VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)。
Vector3 targetPosition;
        if (player != null)
        {
            targetPosition = player.transform.position;
        }
        else
        {
            targetPosition = CurrentChaseTarget;
        }

        if (!TryGetPursuitIngressDestination(targetPosition, out Vector3 /* 中文含义：入口目的地。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ ingressDestination))
        {
            return false;
        }

        direction = ingressDestination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    /// <summary>TryResolveDoor03IngressFinalSteering 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.ReadQueue(int)、Mathf.Max(float, float)、ReadOnlyCollection&lt;VillagerController&gt;.IndexOf(VillagerController)、Vector3.Normalize()、VillagerController.CleanupPursuitIngressWaiters()、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)、VillagerController.ResolveMovementHazardRadius()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryResolvePursuitIngressFinalSteering(out Vector3)；下游（含内部回调体） DoorwayTraversalOccupancy.ReadQueue(int)、Mathf.Max(float, float)、ReadOnlyCollection&lt;VillagerController&gt;.IndexOf(VillagerController)、Vector3.Normalize()、VillagerController.CleanupPursuitIngressWaiters()、VillagerController.ResolveDoor03ExteriorSourceSide(HouseThreeTraversalGeometry)、VillagerController.ResolveMovementHazardRadius()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。</param>
    private bool TryResolveDoor03IngressFinalSteering(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (pursuitIngressDoor == null || agent == null || agent.CurrentPathState != PathAgentState.Arrived)
        {
            return false;
        }

        if (door03TraversalRoute == null)
        {
            door03TraversalRoute = HouseThreeTraversalGeometry.Active;
        }

        if (door03TraversalRoute == null || !door03TraversalRoute.IsReady || door03TraversalRoute.entranceDoor != pursuitIngressDoor || pursuitIngressSourceSide != ResolveDoor03ExteriorSourceSide(door03TraversalRoute))
        {
            return false;
        }

        // target（目标）：使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。
Vector3 target;
        if (pursuitIngressCrossing)
        {
            target = door03TraversalRoute.InteriorGate;
        }
        else
        {
            CleanupPursuitIngressWaiters();
            // waiters（等待者集合）：保存 DoorwayTraversalOccupancy.ReadQueue(int) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。
var waiters = pursuitIngressDoor.TraversalOccupancy.ReadQueue(pursuitIngressSourceSide);
            // queueIndex（队列索引）：保存 ReadOnlyCollection<VillagerController>.IndexOf(VillagerController) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。
int queueIndex = waiters.IndexOf(this);
            if (queueIndex < 0)
            {
                return false;
            }

            // inward（向内）：使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。
Vector3 inward = door03TraversalRoute.InteriorGate - door03TraversalRoute.DoorGate;
            inward.y = 0f;
            if (inward.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            inward.Normalize();
            // queueSeparation（队列分离量）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3)。
float queueSeparation = Mathf.Max(door03TraversalRoute.entranceQueueSpacing, ResolveMovementHazardRadius() * 2f + 0.18f);
            target = door03TraversalRoute.ExteriorGate - inward * (queueIndex * queueSeparation);
        }

        direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        direction.Normalize();
        return true;
    }

    /// <summary>
    /// 动画参数和代码位移必须读取同一帧的追击方向。否则新路径在本帧
    /// 生效后，动画仍使用上一帧方向，脚步与位移会交替错位。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame()、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） PathAgent.ResetPath()、VillagerController.RefreshChaseDestination()、VillagerController.ResolveStandingPathDirection()、VillagerController.StabilizeStandingDirection(Vector3)、VillagerController.TryResolveCloseDirectPursuitDirection(out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveStandingPathDirectionForCurrentFrame()
    {
        if (standingPathDirectionFrame == Time.frameCount)
        {
            return standingPathDirectionForFrame;
        }

        standingPathDirectionFrame = Time.frameCount;
        // wasDirectPursuit（之前是否直接追击）：使用位置：VillagerController.ResolveStandingPathDirectionForCurrentFrame()。
bool wasDirectPursuit = standingDirectPursuitForFrame;
        if (IsCrowdBypassing)
        {
            // 侧向目标已完整验证踏面和身体空间，短距离直接推进，不能再追公共旧拐点。
            standingDirectPursuitForFrame = false;
            // bypassDirection（绕过方向）：使用位置：作为 VillagerController.StabilizeStandingDirection(Vector3) 的参数、VillagerController.ResolveStandingPathDirectionForCurrentFrame()。
Vector3 bypassDirection = crowdBypassDestination - transform.position;
            bypassDirection.y = 0f;
            standingPathDirectionForFrame = StabilizeStandingDirection(bypassDirection);
            return standingPathDirectionForFrame;
        }

        standingDirectPursuitForFrame = agent != null && agent.enabled && !agent.isStopped && !agent.RequiresPrecisePathFollowing && CurrentState == VillagerState.Chase && TryResolveCloseDirectPursuitDirection(out _);
        if (standingDirectPursuitForFrame)
        {
            // direction（方向）：使用位置：作为 VillagerController.StabilizeStandingDirection(Vector3) 的参数、VillagerController.ResolveStandingPathDirectionForCurrentFrame()。
Vector3 direction = Player.transform.position - transform.position;
            direction.y = 0f;
            standingPathDirectionForFrame = StabilizeStandingDirection(direction);
        }
        else
        {
            // 直追可能已经越过旧路径的拐点，退出直追时不能回头追旧点。
            // 清除旧请求并从当前位置重建路径，窗口和门洞仍按原优先级选目标。
            if (wasDirectPursuit && agent != null && agent.enabled && !agent.isStopped && CurrentState == VillagerState.Chase)
            {
                agent.ResetPath();
                RefreshChaseDestination();
            }

            standingPathDirectionForFrame = ResolveStandingPathDirection();
        }

        return standingPathDirectionForFrame;
    }

    /// <summary>StabilizeStandingDirection 的业务处理入口；本方法直接依赖 Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirection()、VillagerController.ResolveStandingPathDirectionForCurrentFrame()；下游（含内部回调体） Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="desired">中文含义：期望。使用位置：VillagerController.StabilizeStandingDirection(Vector3)。</param>
    private Vector3 StabilizeStandingDirection(Vector3 desired)
    {
        desired.y = 0f;
        if (desired.sqrMagnitude <= 0.0001f)
        {
            lastStableStandingMoveDirection = Vector3.zero;
            return Vector3.zero;
        }

        desired.Normalize();
        // 位移意图始终指向当前路径。若先平滑位移方向，再让避让结果回写
        // 这个缓存，近路点会落在转弯半径以内，形成路径有效却持续绕点的循环。
        // 身体朝向仍由统一转身函数平滑，位移限速负责等待身体对齐。
        lastStableStandingMoveDirection = desired;
        return lastStableStandingMoveDirection;
    }

    /// <summary>TryOpenDoorOnPath 的业务处理入口；本方法直接依赖 VillagerController.TryOpenDoorOnPath(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.TryOpenDoorOnPath(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryOpenDoorOnPath()
    {
        // direction（方向）：使用位置：作为 VillagerController.TryOpenDoorOnPath(Vector3) 的参数、VillagerController.TryOpenDoorOnPath()。
Vector3 direction;
        if (agent != null && agent.desiredDirection.sqrMagnitude > 0.0001f)
        {
            direction = agent.desiredDirection;
        }
        else
        {
            direction = transform.forward;
        }

        TryOpenDoorOnPath(direction);
    }

    /// <summary>TryOpenDoorOnPath 的业务处理入口；本方法直接依赖 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)、Mathf.Max(float, float)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryOpenDoorOnPath()、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)、Mathf.Max(float, float)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform) 的参数、VillagerController.TryOpenDoorOnPath(Vector3)。</param>
    private void TryOpenDoorOnPath(Vector3 direction)
    {
        if (!enableAIDoorOpening || Time.time < nextDoorOpenProbeTime || IsDead)
        {
            return;
        }

        nextDoorOpenProbeTime = Time.time + Mathf.Max(0.05f, doorOpenProbeInterval);
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        direction.Normalize();
        // probePosition（探测位置）：使用位置：作为 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform) 的参数。
Vector3 probePosition = transform.position + direction * doorOpenProbeDistance;
        DoorController.TryOpenNearestForAI(probePosition, direction, doorOpenProbeRadius, doorOpenProbeAngle, transform);
    }

    /// <summary>HasCrawlMovementDirection 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeHasCrawlMovementDirection()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasCrawlMovementDirection()
    {
        if (ctx == null || !ctx.HasCrawlMoveIntent)
        {
            return false;
        }

        if (agent != null && agent.enabled && !agent.isStopped && agent.desiredDirection.sqrMagnitude > 0.0001f)
        {
            return true;
        }

        if (ctx.Player == null)
        {
            return false;
        }

        // direction（方向）：使用位置：VillagerController.HasCrawlMovementDirection()。
Vector3 direction = ctx.Player.transform.position - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.01f;
    }

    /// <summary>RotateTowardsDirection 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Mathf.Max(float, float)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)、VillagerController.ResolveStandingPursuitFacingTurnRate()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.RotateTowardsPath()、VillagerController.UpdateCodeDrivenMovement()、VillagerController.UpdateCrawlFacingBeforeAnimator()、VillagerController.UpdateStandingFacingFromActualDisplacement(Vector3)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Mathf.Max(float, float)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)、VillagerController.ResolveStandingPursuitFacingTurnRate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、VillagerController.RotateTowardsDirection(Vector3, float)。</param>
    /// <param name="turnSpeed">中文含义：转向速度。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.RotateTowardsDirection(Vector3, float)。</param>
    private void RotateTowardsDirection(Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        if (CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion)
        {
            if (standingFacingWriteFrame == Time.frameCount)
            {
                return;
            }

            standingFacingWriteFrame = Time.frameCount;
            turnSpeed = ResolveStandingPursuitFacingTurnRate();
        }

        // targetRotation（目标旋转）：保存 Quaternion.LookRotation(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数。
Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        motionExecutor.CommitNormalOrientation(Quaternion.RotateTowards(transform.rotation, targetRotation, Mathf.Max(1f, turnSpeed) * Time.deltaTime));
    }

    /// <summary>ResolveCrawlFacingTurnRate 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()、VillagerController.UpdateCrawlFacingBeforeAnimator()；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveCrawlFacingTurnRate()
    {
        // 匍匐移动方向的平滑速率已经用于追击路径拐弯，身体转向至少
        // 使用同一速率，避免移动方向已经改变而身体仍长时间背对目标。
        return Mathf.Max(1f, Mathf.Max(crawlTurnSpeed, crawlDirectionTurnRate));
    }

    /// <summary>ResolveStandingPursuitFacingTurnRate 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.RotateTowardsDirection(Vector3, float)、VillagerController.RotateTowardsPath()、VillagerController.UpdateCodeDrivenMovement()、VillagerController.UpdateStandingFacingFromActualDisplacement(Vector3)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveStandingPursuitFacingTurnRate()
    {
        // 局部避让可以快速让开同伴，但不能直接决定身体转向速度；否则
        // 路径拐点轻微切换就会被放大成高频左右甩身。
        return Mathf.Min(180f, Mathf.Max(1f, standingDirectionTurnRate));
    }

    /// <summary>ResolveFacingLimitedStandingDisplacement 的业务处理入口；本方法直接依赖 Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3)。</param>
    private Vector3 ResolveFacingLimitedStandingDisplacement(Vector3 displacement)
    {
        displacement.y = 0f;
        if (displacement.sqrMagnitude <= 0.000001f)
        {
            return Vector3.zero;
        }

        // direction（方向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数。
Vector3 direction = displacement.normalized;
        // angle（角度）：保存 Vector3.Angle(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数。
float angle = Vector3.Angle(transform.forward, direction);
        // fullSpeedAngle（完整速度角度）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数。
float fullSpeedAngle = Mathf.Min(movementFacingFullSpeedAngle, movementFacingStopAngle);
        // stopAngle（停止角度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数。
float stopAngle = Mathf.Max(fullSpeedAngle + 0.01f, movementFacingStopAngle);
        // alignment（对齐状态）：使用位置：VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3)。
float alignment = 1f - Mathf.InverseLerp(fullSpeedAngle, stopAngle, angle);
        // 大角度换向时先转身。转向在位移之前每帧执行，不依赖成功移动；
        // 因此无需保底侧移或后退，也不会为了绕过身后路点而画圆。
        return displacement * alignment;
    }

    /// <summary>UpdateStandingFacingFromActualDisplacement 的业务处理入口；本方法直接依赖 VillagerController.ResolveStandingPursuitFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） VillagerController.ResolveStandingPursuitFacingTurnRate()、VillagerController.RotateTowardsDirection(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actualDisplacement">中文含义：实际位移。使用位置：VillagerController.UpdateStandingFacingFromActualDisplacement(Vector3)。</param>
    private void UpdateStandingFacingFromActualDisplacement(Vector3 actualDisplacement)
    {
        actualDisplacement.y = 0f;
        if (actualDisplacement.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        // actualDirection（实际方向）：使用位置：作为 VillagerController.RotateTowardsDirection(Vector3, float) 的参数。
Vector3 actualDirection = actualDisplacement.normalized;
        // 碰撞滑动和同伴避让只能影响本帧身体朝向，不能改写下一帧路径意图。
        RotateTowardsDirection(actualDirection, ResolveStandingPursuitFacingTurnRate());
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 VillagerController.SnapToGround(float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeSnapToGround()、VillagerController.EndQTEGroundLock()、VillagerController.RecoverInvalidNavigationSpawn()；下游（含内部回调体） VillagerController.SnapToGround(float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SnapToGround()
    {
        SnapToGround(groundProbeHeight, groundProbeDistance, false);
    }

    /// <summary>BeginQTEGroundLock 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.TryAcquire(int, QTEKind)、VillagerQTEParticipant.TryReserveBodyTechnique(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginQTEGroundLock()
    {
        qteGroundY = transform.position.y;
        qteGroundLocked = true;
    }

    /// <summary>EndQTEGroundLock 的业务处理入口；本方法直接依赖 VillagerController.SnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerQTEParticipant.ForceExitQTEForDeath(int)、VillagerQTEParticipant.Release(int)、VillagerQTEParticipant.ReleaseBodyTechniqueReservation()；下游（含内部回调体） VillagerController.SnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndQTEGroundLock()
    {
        qteGroundLocked = false;
        SnapToGround();
    }

    /// <summary>MaintainGroundContact 的业务处理入口；本方法直接依赖 VillagerController.SnapToGround(float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DriveExternalKnockback(Vector3, float, float)、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.OnAnimatorMove()、VillagerController.TryExecuteStairActionDisplacement(Vector3)、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） VillagerController.SnapToGround(float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void MaintainGroundContact()
    {
        if (combatSupportLostTime > 0f || externalKnockbackRoutine != null)
        {
            return;
        }
        SnapToGround(groundProbeHeight, groundProbeDistance, false);
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool)、VillagerController.MaintainGroundContact()、VillagerController.SnapToGround()、VillagerController.Start()；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probeHeight">中文含义：探测高度。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.SnapToGround(float, float, bool)。</param>
    /// <param name="probeDistance">中文含义：探测距离。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="allowLargeDrop">中文含义：是否允许较大掉落。使用位置：VillagerController.SnapToGround(float, float, bool)。</param>
    private void SnapToGround(float probeHeight, float probeDistance, bool allowLargeDrop)
    {
        // 所有动作共用实际踏面支撑，不能在受击或死亡时投影到桥下地面。
        if (PlantNearHouseThreeTread(transform.position) && TryReadStairSupportHeight(transform.position, out float /* 中文含义：楼梯纵向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stairY))
        {
            motionExecutor.CommitNormalPosition(new Vector3(transform.position.x, stairY, transform.position.z));
            return;
        }

        // 木板细缝和低台沿使用与通行预检相同的支撑范围；不能在前进时
        // 跨上台沿，帧尾又被中心射线拉回较低踏面。
        Vector3 position = transform.position;
        if (!allowLargeDrop && !IsNavigationTraversalProtected && !IsQTEControlled
            && TryReadSupportedFootprint(position, position.y, maximumGroundStepUp, maximumGroundDrop, out float floorY))
        {
            // 先采样脚下的真实踏面，避免脚边球探针先碰到较高瓦片或相邻平台。
            if (floorY <= position.y + .01f || CanRaiseRaiseMovementCapsule(floorY - position.y))
            {
                if (stableGroundFrame != Time.frameCount)
                {
                    stableGroundFrame = Time.frameCount;
                    position.y = StabilizeGroundHeight(position.y, floorY);
                    motionExecutor.CommitNormalPosition(position);
                }
                return;
            }
        }

        if (TryResolveSupportedNavigationPosition(transform.position, transform.position.y, out Vector3 /* 中文含义：连续支撑。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ continuousSupport) && (continuousSupport.y <= transform.position.y + .01f || CanRaiseRaiseMovementCapsule(continuousSupport.y - transform.position.y)))
        {
            if (allowLargeDrop || IsNavigationTraversalProtected || IsQTEControlled)
            {
                motionExecutor.CommitNormalPosition(continuousSupport);
            }
            return;
        }

        // origin（原点）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 origin = transform.position + Vector3.up * probeHeight;
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.SnapToGround(float, float, bool)。
int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, probeHeight + probeDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // nearestDistance（最近距离）：使用位置：VillagerController.SnapToGround(float, float, bool)。
float nearestDistance = float.MaxValue;
        // foundGround（是否找到的地面）：使用位置：VillagerController.SnapToGround(float, float, bool)。
bool foundGround = false;
        // groundPoint（地面点）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.SnapToGround(float, float, bool)。
Vector3 groundPoint = default;
        // i（当前遍历索引）：使用位置：VillagerController.SnapToGround(float, float, bool)。
for (int i = 0; i < hitCount; i++)
        {
            // hitCollider（命中的碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsNavigationObstacle(Collider) 的参数 等共 4 处。
Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(hitCollider) != null || NavigationCollisionCache.Player(hitCollider) != null)
            {
                continue;
            }

            if (IsNavigationObstacle(hitCollider))
            {
                continue;
            }

            if (Vector3.Angle(groundHits[i].normal, Vector3.up) > Mathf.Clamp(maximumGroundSlope, 1f, 89f))
            {
                continue;
            }

            // targetY（目标纵向）：使用位置：VillagerController.SnapToGround(float, float, bool)。
float targetY = groundHits[i].point.y + groundOffset;
            // verticalChange（竖直变化）：使用位置：VillagerController.SnapToGround(float, float, bool)。
float verticalChange = targetY - transform.position.y;
            // allowedStepUp（允许的步进向上）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.SnapToGround(float, float, bool)。
float allowedStepUp;
            if (DoorController.IsInsideOpenPassage(transform.position, 1.6f))
            {
                allowedStepUp = Mathf.Max(maximumGroundStepUp, 1.6f);
            }
            else
            {
                allowedStepUp = ResolveAllowedPathStepUp();
            }

            if (verticalChange > Mathf.Max(0.05f, allowedStepUp))
            {
                continue;
            }

            if (!allowLargeDrop && verticalChange < -Mathf.Max(0.05f, maximumGroundDrop))
            {
                continue;
            }

            if (groundHits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = groundHits[i].distance;
            groundPoint = groundHits[i].point;
            foundGround = true;
        }

        if (!foundGround)
        {
            return;
        }

        motionExecutor.CommitNormalPosition(new Vector3(transform.position.x, groundPoint.y + groundOffset, transform.position.z));
    }

    /// <summary>IsCrowdMovementStalled 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsCrowdMovementStalled()
    {
        return !enabled || CurrentMovementMode == EnemyMovementMode.FullyStopped || CurrentState == VillagerState.Staggered || (CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && actualHorizontalMovementSpeed < 0.05f && (chaseNoProgressDuration > 0.35f || movementBlockedDuration > 0.35f));
    }

    /// <summary>IsRelevantCrowdNeighbor 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)、VillagerController.IsCrowdBypassDestinationOccupied(Vector3)、VillagerController.ResolveCrowdOverlap()；下游（含内部回调体） Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="otherCollider">中文含义：另一个碰撞体。使用位置：VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)。</param>
    /// <param name="other">中文含义：另一个村民敌人控制器。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)。</param>
    private bool IsRelevantCrowdNeighbor(Collider otherCollider, out VillagerController other)
    {
        if (otherCollider != null)
        {
            other = otherCollider.GetComponentInParent<VillagerController>();
        }
        else
        {
            other = null;
        }

        return other != null && other != this && !other.IsDead && other.gameObject.activeInHierarchy && otherCollider == other.movementCollider && Mathf.Abs(other.transform.position.y - transform.position.y) <= 0.75f;
    }

    /// <summary>ConstrainCrowdDisplacement 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Sqrt(float)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Vector3.Dot(Vector3, Vector3)、VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)、VillagerController.ResolveMovementHazardRadius()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Sqrt(float)、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、Vector3.Dot(Vector3, Vector3)、VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)、VillagerController.ResolveMovementHazardRadius()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。</param>
    /// <param name="blocker">中文含义：阻挡者。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。</param>
    private Vector3 ConstrainCrowdDisplacement(Vector3 displacement, out VillagerController blocker)
    {
        blocker = null;
        if (CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion || IsNavigationTraversalProtected || displacement.sqrMagnitude <= 0.000001f)
        {
            return displacement;
        }

        // distance（距离）：使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float distance = displacement.magnitude;
        // direction（方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
Vector3 direction = displacement / distance;
        // bodyRadius（身体半径）：保存 VillagerController.ResolveMovementHazardRadius() 的返回结果，供当前步骤使用。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float bodyRadius = ResolveMovementHazardRadius();
        CombatPerformanceCounters.physicsQueryCount++;
        // count（数量）：保存 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
int count = Physics.OverlapSphereNonAlloc(transform.position, bodyRadius * 4f + distance, movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // allowed（允许的）：使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float allowed = distance;
        // i（当前遍历索引）：使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
for (int i = 0; i < count; i++)
        {
            if (!IsRelevantCrowdNeighbor(movementOverlaps[i], out VillagerController /* 中文含义：另一个村民敌人控制器。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ other))
            {
                continue;
            }

            // offset（偏移量）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;
            // separation（分离量）：使用位置：作为 Mathf.Sqrt(float) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float separation = bodyRadius + other.ResolveMovementHazardRadius() + Mathf.Max(0.01f, movementCollisionSkin);
            // toward（朝向）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float toward = Vector3.Dot(offset, direction);
            if (toward <= 0f)
            {
                continue;
            }

            // sideSquared（侧边平方）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Sqrt(float) 的参数、VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float sideSquared = Mathf.Max(0f, offset.sqrMagnitude - toward * toward);
            if (sideSquared >= separation * separation)
            {
                continue;
            }

            // contact（接触）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)。
float contact = Mathf.Max(0f, toward - Mathf.Sqrt(separation * separation - sideSquared));
            if (contact >= allowed)
            {
                continue;
            }

            allowed = contact;
            blocker = other;
        }

        return direction * allowed;
    }

    /// <summary>ResolveCrowdOverlap 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Cos(float)、Mathf.Max(int, int)、Mathf.Min(float, float)、Mathf.Min(int, int)、Mathf.Sin(float)、Object.GetInstanceID() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Cos(float)、Mathf.Max(int, int)、Mathf.Min(float, float)、Mathf.Min(int, int)、Mathf.Sin(float)、Object.GetInstanceID() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResolveCrowdOverlap()
    {
        if (CurrentState != VillagerState.Chase || IsNavigationTraversalProtected || (agent != null && agent.RequiresPrecisePathFollowing) || IsInsideTraversalBottleneck(Vector3.zero))
        {
            return;
        }

        // radius（半径）：保存 VillagerController.ResolveMovementHazardRadius() 的返回结果，供当前步骤使用。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、VillagerController.ResolveCrowdOverlap()。
float radius = ResolveMovementHazardRadius();
        CombatPerformanceCounters.physicsQueryCount++;
        // count（数量）：保存 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveCrowdOverlap()。
int count = Physics.OverlapSphereNonAlloc(transform.position, radius * 4f, movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // recovery（恢复）：使用位置：作为 VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3) 的参数、VillagerController.ResolveCrowdOverlap()。
Vector3 recovery = Vector3.zero;
        // deepest（最深）：使用位置：VillagerController.ResolveCrowdOverlap()。
float deepest = 0f;
        // i（当前遍历索引）：使用位置：VillagerController.ResolveCrowdOverlap()。
for (int i = 0; i < count; i++)
        {
            if (!IsRelevantCrowdNeighbor(movementOverlaps[i], out VillagerController /* 中文含义：另一个村民敌人控制器。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ other) || other.IsNavigationTraversalProtected || other.IsQTEControlled)
            {
                continue;
            }

            // away（远离）：使用位置：VillagerController.ResolveCrowdOverlap()。
Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            // distance（距离）：使用位置：VillagerController.ResolveCrowdOverlap()。
float distance = away.magnitude;
            // overlap（重叠）：使用位置：作为 Mathf.Min(float, float) 的参数、VillagerController.ResolveCrowdOverlap()。
float overlap = radius + other.ResolveMovementHazardRadius() - distance;
            if (overlap <= deepest)
            {
                continue;
            }

            deepest = overlap;
            if (distance < 0.001f)
            {
                // 完全同位时按固定身份确定相反的分离方向，不用随机数逐帧换边。
                // first（首个）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveCrowdOverlap()。
int first = Mathf.Min(GetInstanceID(), other.GetInstanceID());
                // second（第二）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveCrowdOverlap()。
int second = Mathf.Max(GetInstanceID(), other.GetInstanceID());
                // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = ((first ^ second) & 1023) * (Mathf.PI * 2f / 1024f);
                // leftOperand143（计算前缓存的左侧三维向量）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.ResolveCrowdOverlap()。
global::UnityEngine.Vector3 leftOperand143 = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // selectedValue142（分支选出的1f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.ResolveCrowdOverlap()。
float selectedValue142;
                if (GetInstanceID() == first)
                {
                    selectedValue142 = 1f;
                }
                else
                {
                    selectedValue142 = -1f;
                }

                away = leftOperand143 * (selectedValue142);
            }

            recovery = away.normalized * Mathf.Min(overlap * 0.5f, Mathf.Min(overlapRecoveryDistance, Time.deltaTime * 0.5f));
        }

        if (recovery.sqrMagnitude <= 0.000001f || !TryResolveSupportedNavigationPosition(transform.position + recovery, transform.position.y, out Vector3 /* 中文含义：支持的。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ supported) || !CanMoveDirectlyToNavigationPoint(supported))
        {
            return;
        }

        // 分离先验证真实踏面和墙体，只在可站立空间内渐进恢复，不能推离屋顶边缘。
        motionExecutor.CommitNormalPosition(supported);
    }

    /// <summary>IsCrowdBypassDestinationOccupied 的业务处理入口；本方法直接依赖 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、VillagerController.HorizontalDistance(Vector3, Vector3)、VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)、VillagerController.ResolveMovementHazardRadius()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryMutualBlockingYield(VillagerController)、VillagerController.TryStartCrowdBypass(VillagerController)；下游（含内部回调体） Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction)、VillagerController.HorizontalDistance(Vector3, Vector3)、VillagerController.IsRelevantCrowdNeighbor(Collider, out VillagerController)、VillagerController.ResolveMovementHazardRadius()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">中文含义：候选。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数。</param>
    private bool IsCrowdBypassDestinationOccupied(Vector3 candidate)
    {
        CombatPerformanceCounters.physicsQueryCount++;
        // count（数量）：保存 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsCrowdBypassDestinationOccupied(Vector3)。
int count = Physics.OverlapSphereNonAlloc(candidate, 3f, movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.IsCrowdBypassDestinationOccupied(Vector3)。
for (int i = 0; i < count; i++)
        {
            if (!IsRelevantCrowdNeighbor(movementOverlaps[i], out VillagerController /* 中文含义：另一个村民敌人控制器。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ other))
            {
                continue;
            }

            // separation（分离量）：使用位置：VillagerController.IsCrowdBypassDestinationOccupied(Vector3)。
float separation = ResolveMovementHazardRadius() + other.ResolveMovementHazardRadius() + 0.1f;
            if (HorizontalDistance(candidate, other.transform.position) < separation || (other.IsCrowdBypassing && HorizontalDistance(candidate, other.crowdBypassDestination) < separation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ResolveLocalAvoidanceDisplacement 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Object.GetInstanceID()、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Component.GetComponentInParent&lt;VillagerController&gt;()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Object.GetInstanceID()、Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：作为 VillagerController.IsInsideTraversalBottleneck(Vector3) 的参数、作为 VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。</param>
    private Vector3 ResolveLocalAvoidanceDisplacement(Vector3 displacement)
    {
        if (!enableLocalAvoidance || displacement.sqrMagnitude <= 0.000001f)
        {
            return displacement;
        }

        if (IsInsideTraversalBottleneck(displacement))
        {
            lastStableAvoidanceDirection = displacement.normalized;
            return displacement;
        }

        // direction（方向）：使用位置：作为 Vector3.Cross(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数 等共 5 处。
Vector3 direction = displacement.normalized;
        CombatPerformanceCounters.physicsQueryCount++;
        // overlapCount（重叠数量）：保存 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
int overlapCount = Physics.OverlapSphereNonAlloc(transform.position, enemyAvoidanceRadius, movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // side（侧边）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        // dominantNeighbor（主导邻居）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.TryStartCrowdBypass(VillagerController) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
VillagerController dominantNeighbor = null;
        // dominantWeight（主导权重）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
float dominantWeight = 0f;
        // i（当前遍历索引）：使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
for (int i = 0; i < overlapCount; i++)
        {
            // nearbyCollider（附近碰撞体）：使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Collider nearbyCollider = movementOverlaps[i];
            if (nearbyCollider == null)
            {
                continue;
            }

            // nearbyEnemy（附近敌人）：保存 Component.GetComponentInParent<VillagerController>() 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
VillagerController nearbyEnemy = nearbyCollider.GetComponentInParent<VillagerController>();
            if (nearbyEnemy == null || nearbyEnemy == this || nearbyEnemy.IsDead)
            {
                continue;
            }

            // offset（偏移量）：使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Vector3 offset = transform.position - nearbyEnemy.transform.position;
            offset.y = 0f;
            // distance（距离）：使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
float distance = offset.magnitude;
            if (distance >= enemyAvoidanceRadius || Mathf.Abs(transform.position.y - nearbyEnemy.transform.position.y) > 0.75f)
            {
                continue;
            }

            if (distance > 0.001f)
            {
                // towardEnemy（朝向敌人）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 towardEnemy = -offset / distance;
                if (Vector3.Dot(direction, towardEnemy) < -0.25f)
                {
                    continue;
                }
            }

            // weight（权重）：使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
float weight = 1f - distance / enemyAvoidanceRadius;
            if (weight > dominantWeight)
            {
                dominantWeight = weight;
                dominantNeighbor = nearbyEnemy;
            }
        }

        if (dominantNeighbor == null)
        {
            if (Time.time >= localAvoidanceSideHoldUntil)
            {
                localAvoidanceSideSign = 0;
                localAvoidancePartnerId = 0;
            }

            return StabilizeLocalAvoidanceDirection(direction, displacement.magnitude);
        }

        // towardStoppedNeighbor（朝向已停止邻居）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Vector3 towardStoppedNeighbor = dominantNeighbor.transform.position - transform.position;
        towardStoppedNeighbor.y = 0f;
        if (dominantWeight > 0.25f && towardStoppedNeighbor.sqrMagnitude > 0.0001f && Vector3.Dot(direction, towardStoppedNeighbor.normalized) > 0.7f && dominantNeighbor.IsCrowdMovementStalled())
        {
            // 前方同伴停步时先选可达侧向目标，避免后方继续压向同一条直线。
            TryStartCrowdBypass(dominantNeighbor);
        }

        // dominantId（主导编号）：保存 Object.GetInstanceID() 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
int dominantId = dominantNeighbor.GetInstanceID();
        if (localAvoidanceSideSign == 0)
        {
            // 每名敌人的通过侧在一次接触中固定，不随“最近邻”在相邻
            // 敌人之间切换。此前分离向量会在邻居从左到右越过时反向，
            // 即使里昂静止也会持续左右摆动。
            // neighborSide（邻居侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
float neighborSide = Vector3.Dot(transform.position - dominantNeighbor.transform.position, side);
            if (Mathf.Abs(neighborSide) > 0.05f)
            {
                // selectedValue144（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
int selectedValue144;
                if (neighborSide > 0f)
                {
                    selectedValue144 = 1;
                }
                else
                {
                    selectedValue144 = -1;
                }

                localAvoidanceSideSign = (selectedValue144);
            }
            else
            {
                if ((((GetInstanceID() >> 2) ^ (GetInstanceID() >> 5)) & 1) == 0)
                {
                    localAvoidanceSideSign = 1;
                }
                else
                {
                    localAvoidanceSideSign = -1;
                }
            }

            localAvoidanceSideHoldUntil = Time.time + Mathf.Max(1.25f, enemyAvoidanceSideHoldDuration);
        }

        localAvoidancePartnerId = dominantId;
        // 局部避让只能在主追击方向的一侧产生有限偏移，不能把每帧的
        // 相对位置直接当作新目标方向。这样敌人会完成一次明确的绕行，
        // 而不是在前进途中持续向左右重新选边。
        // steering（转向）：使用位置：作为 Mathf.Min(float, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Vector3 steering = side * localAvoidanceSideSign * Mathf.Max(0.05f, enemyAvoidanceSideBias + dominantWeight * enemyAvoidanceStrength);
        steering.y = 0f;
        if (steering.sqrMagnitude <= 0.0001f)
        {
            return displacement;
        }

        // 追击方向始终占主导，局部避让只以有限权重修正。
        // avoidanceWeight（避障权重）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
float avoidanceWeight = Mathf.Min(Mathf.Clamp(enemyAvoidanceMaximumWeight, 0f, 0.22f), steering.magnitude * Mathf.Clamp(enemyAvoidanceMaximumWeight, 0f, 0.22f));
        // avoidedDirection（已避开方向）：使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、作为 VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3) 的参数、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)。
Vector3 avoidedDirection = Vector3.Lerp(direction, steering.normalized, avoidanceWeight).normalized;
        avoidedDirection = Vector3.RotateTowards(direction, avoidedDirection, Mathf.Deg2Rad * ResolveLocalAvoidanceMaximumAngle(), 0f);
        return StabilizeLocalAvoidanceDirection(avoidedDirection, displacement.magnitude, direction);
    }

    /// <summary>StabilizeLocalAvoidanceDirection 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、Vector3.RotateTowards(Vector3, Vector3, float, float)、VillagerController.ResolveLocalAvoidanceMinimumForwardAlignment()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、Vector3.RotateTowards(Vector3, Vector3, float, float)、VillagerController.ResolveLocalAvoidanceMinimumForwardAlignment()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="desired">中文含义：期望。使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3)。</param>
    /// <param name="distance">中文含义：距离。使用位置：VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3)。</param>
    /// <param name="forwardConstraint">中文含义：前向约束。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3)。</param>
    private Vector3 StabilizeLocalAvoidanceDirection(Vector3 desired, float distance, Vector3 forwardConstraint = default)
    {
        desired.y = 0f;
        if (desired.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        desired.Normalize();
        if (lastStableAvoidanceDirection.sqrMagnitude <= 0.0001f)
        {
            lastStableAvoidanceDirection = desired;
        }

        // maximumRadians（最大弧度）：使用位置：作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数。
float maximumRadians = Mathf.Deg2Rad * Mathf.Min(180f, Mathf.Max(1f, localAvoidanceTurnRate)) * Time.deltaTime;
        lastStableAvoidanceDirection = Vector3.RotateTowards(lastStableAvoidanceDirection, desired, maximumRadians, 0f);
        forwardConstraint.y = 0f;
        if (forwardConstraint.sqrMagnitude > 0.0001f)
        {
            forwardConstraint.Normalize();
            if (Vector3.Dot(lastStableAvoidanceDirection, forwardConstraint) < ResolveLocalAvoidanceMinimumForwardAlignment())
            {
                lastStableAvoidanceDirection = forwardConstraint;
            }
        }

        lastStableAvoidanceDirection.Normalize();
        return lastStableAvoidanceDirection * distance;
    }

    /// <summary>
    /// 局部避让只允许在身体仍能完成转向的扇区内修正路径。追击是主线，
    /// 避让只负责让开相邻敌人，不能把位移改成侧走或后退。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.ResolveLocalAvoidanceMinimumForwardAlignment()；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveLocalAvoidanceMaximumAngle()
    {
        // facingSafeAngle（朝向安全角度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数。
float facingSafeAngle = Mathf.Max(5f, movementFacingStopAngle - 5f);
        return Mathf.Min(Mathf.Clamp(enemyAvoidanceMaximumAngle, 5f, 60f), facingSafeAngle);
    }

    /// <summary>ResolveLocalAvoidanceMinimumForwardAlignment 的业务处理入口；本方法直接依赖 Mathf.Cos(float)、VillagerController.ResolveLocalAvoidanceMaximumAngle()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)、VillagerController.ReconcileStandingPursuitFacingAfterAnimation()、VillagerController.StabilizeLocalAvoidanceDirection(Vector3, float, Vector3)；下游（含内部回调体） Mathf.Cos(float)、VillagerController.ResolveLocalAvoidanceMaximumAngle()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveLocalAvoidanceMinimumForwardAlignment()
    {
        return Mathf.Cos(ResolveLocalAvoidanceMaximumAngle() * Mathf.Deg2Rad);
    }

    /// <summary>
    /// 这是站立追击写入位移前的最后一道约束。局部避让的内部稳定缓存可能
    /// 仍保留上一段路径的侧向方向；不能让它越过本帧追击方向的安全扇区，
    /// 否则朝向限速会把有效追击路径错误地压成零位移。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、Vector3.RotateTowards(Vector3, Vector3, float, float)、VillagerController.ResolveLocalAvoidanceMaximumAngle()、VillagerController.ResolveLocalAvoidanceMinimumForwardAlignment()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="avoidanceDisplacement">中文含义：避障位移。使用位置：VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)。</param>
    /// <param name="pursuitDirection">中文含义：追击方向。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数、VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)。</param>
    private Vector3 ConstrainAvoidanceToPursuitDirection(Vector3 avoidanceDisplacement, Vector3 pursuitDirection)
    {
        avoidanceDisplacement.y = 0f;
        pursuitDirection.y = 0f;
        if (avoidanceDisplacement.sqrMagnitude <= 0.000001f || pursuitDirection.sqrMagnitude <= 0.000001f)
        {
            return avoidanceDisplacement;
        }

        // distance（距离）：使用位置：VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)。
float distance = avoidanceDisplacement.magnitude;
        // avoidanceDirection（避障方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.RotateTowards(Vector3, Vector3, float, float) 的参数。
Vector3 avoidanceDirection = avoidanceDisplacement / distance;
        pursuitDirection.Normalize();
        if (Vector3.Dot(avoidanceDirection, pursuitDirection) >= ResolveLocalAvoidanceMinimumForwardAlignment())
        {
            return avoidanceDisplacement;
        }

        // constrainedDirection（约束后的方向）：保存 Vector3.RotateTowards(Vector3, Vector3, float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ConstrainAvoidanceToPursuitDirection(Vector3, Vector3)。
Vector3 constrainedDirection = Vector3.RotateTowards(pursuitDirection, avoidanceDirection, Mathf.Deg2Rad * ResolveLocalAvoidanceMaximumAngle(), 0f);
        return constrainedDirection.normalized * distance;
    }

    /// <summary>IsInsideTraversalBottleneck 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Max(float, float)、Vector3.Normalize()、VillagerController.IsApproachingEnemyStairBridge()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveCrowdOverlap()、VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.TryStartCrowdBypass(VillagerController)、VillagerController.UpdateChaseProgressRecovery()；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Max(float, float)、Vector3.Normalize()、VillagerController.IsApproachingEnemyStairBridge()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：VillagerController.IsInsideTraversalBottleneck(Vector3)。</param>
    private bool IsInsideTraversalBottleneck(Vector3 displacement)
    {
        // direction（方向）：使用位置：VillagerController.IsInsideTraversalBottleneck(Vector3)。
Vector3 direction = displacement;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }

        // probe（探测）：使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数。
Vector3 probe = transform.position + direction * Mathf.Max(0.35f, doorOpenProbeDistance);
        return DoorController.IsInsideOpenPassage(transform.position, 2.75f) || DoorController.IsInsideOpenPassage(probe, 2.75f);
    }

    /// <summary>MoveWithCollision 的业务处理入口；本方法直接依赖 Mathf.CeilToInt(float)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Vector3.ClampMagnitude(Vector3, float)、Vector3.Dot(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.GetHorizontalMovementSince(Vector3) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DriveExternalKnockback(Vector3, float, float)、VillagerController.OnAnimatorMove()、VillagerController.TryExecuteStairActionDisplacement(Vector3)、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Mathf.CeilToInt(float)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Vector3.ClampMagnitude(Vector3, float)、Vector3.Dot(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.GetHorizontalMovementSince(Vector3) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的参数 等共 4 处。</param>
    /// <param name="strictActionCollision">中文含义：是否严格动作碰撞。使用位置：作为 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的参数、VillagerController.MoveWithCollision(Vector3, bool, bool)。</param>
    /// <param name="applyLocalAvoidance">中文含义：是否应用局部避障。使用位置：作为 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的参数。</param>
    private Vector3 MoveWithCollision(Vector3 displacement, bool strictActionCollision = false, bool applyLocalAvoidance = true)
    {
        displacement.y = 0f;
        if (!strictActionCollision && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && !standingDirectPursuitForFrame && !IsCrowdBypassing && PlantNearHouseThreeTread(transform.position) && agent != null && agent.TryGetCurrentWaypoint(out Vector3 /* 中文含义：当前路径点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ currentWaypoint))
        {
            // toWaypoint（目标路径点）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.MoveWithCollision(Vector3, bool, bool)。
Vector3 toWaypoint = Vector3.ProjectOnPlane(currentWaypoint - transform.position, Vector3.up);
            // 长帧的整步可能越过近路点，再跨数级台阶返回，永远无法满足
            // 同时到达的高度条件。先实际踩到当前路点，再由寻路代理推进。
            if (toWaypoint.sqrMagnitude > .000001f && Vector3.Dot(toWaypoint.normalized, displacement.normalized) > .8f)
            {
                displacement = Vector3.ClampMagnitude(displacement, toWaypoint.magnitude);
            }
        }

        // distance（距离）：使用位置：作为 Mathf.CeilToInt(float) 的参数、VillagerController.MoveWithCollision(Vector3, bool, bool)。
float distance = displacement.magnitude;
        if (distance <= .12f || !PlantNearHouseThreeTread(transform.position))
        {
            return ExecuteCollisionMovement(displacement, strictActionCollision, applyLocalAvoidance, Time.deltaTime);
        }

        // 一帧较长时仍沿实体踏面连续移动，避免先横跨数阶再失去高度归属。
        // 每个短步仍经过同一个碰撞执行器，不改变目标、通行顺序或位移负责方。
        // startPoint（起始点）：使用位置：作为 VillagerController.GetHorizontalMovementSince(Vector3) 的参数。
Vector3 startPoint = transform.position;
        // count（数量）：保存 Mathf.CeilToInt(float) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的参数、VillagerController.MoveWithCollision(Vector3, bool, bool)。
int count = Mathf.CeilToInt(distance / .12f);
        // shortStep（短型步进）：使用位置：作为 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的参数。
Vector3 shortStep = displacement / count;
        // i（当前遍历索引）：使用位置：VillagerController.MoveWithCollision(Vector3, bool, bool)。
for (int i = 0; i < count; i++)
        {
            // actual（实际）：保存 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.MoveWithCollision(Vector3, bool, bool)。
var actual = ExecuteCollisionMovement(shortStep, strictActionCollision, applyLocalAvoidance, Time.deltaTime / count);
            MaintainGroundContact();
            if (actual.sqrMagnitude < .00000001f)
            {
                break;
            }
        }

        return GetHorizontalMovementSince(startPoint);
    }

    /// <summary>ExecuteCollisionMovement 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.ClampMagnitude(Vector3, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、Vector3.ProjectOnPlane(Vector3, Vector3) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.MoveWithCollision(Vector3, bool, bool)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Max(float, float)、Mathf.Min(float, float)、Vector3.ClampMagnitude(Vector3, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()、Vector3.ProjectOnPlane(Vector3, Vector3) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：作为 CharacterMotionExecutor.CommitNormalPosition(Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的参数 等共 6 处。</param>
    /// <param name="strictActionCollision">中文含义：是否严格动作碰撞。使用位置：作为 VillagerController.ResolveMovementOverlap(bool) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。</param>
    /// <param name="applyLocalAvoidance">中文含义：是否应用局部避障。使用位置：VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。</param>
    /// <param name="timeSlice">中文含义：时间片段。使用位置：作为 VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float) 的参数。</param>
    private Vector3 ExecuteCollisionMovement(Vector3 displacement, bool strictActionCollision, bool applyLocalAvoidance, float timeSlice)
    {
        displacement.y = 0f;
        // requestedDistance（请求的距离）：使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3) 的参数、作为 VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float) 的参数 等共 4 处。
float requestedDistance = displacement.magnitude;
        if (requestedDistance <= 0.0001f)
        {
            return Vector3.zero;
        }

        // startPosition（起始位置）：使用位置：作为 VillagerController.GetHorizontalMovementSince(Vector3) 的参数、作为 VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float) 的参数。
Vector3 startPosition = transform.position;
        ResolveMovementOverlap(strictActionCollision);
        if (!strictActionCollision && applyLocalAvoidance)
        {
            displacement = ResolveLocalAvoidanceDisplacement(displacement);
        }

        // crowdBlocker（人群阻挡者）：使用位置：作为 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的参数、作为 VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
VillagerController crowdBlocker = null;
        if (!strictActionCollision)
        {
            displacement = ConstrainCrowdDisplacement(displacement, out crowdBlocker);
            if (crowdBlocker != null && displacement.sqrMagnitude < requestedDistance * requestedDistance * .01f && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && agent != null && agent.hasPath)
            {
                // 侧向避让可能偏向紧贴身后的同伴，再被人群碰撞截停。
                // 当前路点方向若仍有净空，就使用它；同样检测人群和墙体，不能穿过堵路者。
                // pathDisplacement（路径位移）：保存 VillagerController.ResolveFacingLimitedStandingDisplacement(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、作为 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的参数。
Vector3 pathDisplacement = ResolveFacingLimitedStandingDisplacement(agent.desiredDirection * requestedDistance);
                // canWalkDisplacement（是否可以行走位移）：保存 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的返回结果，供当前步骤使用。使用位置：VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
Vector3 canWalkDisplacement = ConstrainCrowdDisplacement(pathDisplacement, out var /* 中文含义：路径阻挡者。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pathBlocker);
                if (pathBlocker != null)
                {
                    // 紧贴安全间距时，极小的朝内分量也会令圆形人群检测截停。
                    // 沿接触切线滑动并略微分离，避免转弯时被身后角色锁死。
                    // separateDirection（分离方向）：使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数。
Vector3 separateDirection = Vector3.ProjectOnPlane(transform.position - pathBlocker.transform.position, Vector3.up).normalized;
                    // tangentialDisplacement（切向位移）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的参数 等共 4 处。
Vector3 tangentialDisplacement = Vector3.ProjectOnPlane(pathDisplacement, separateDirection);
                    if (tangentialDisplacement.sqrMagnitude > .000001f && Vector3.Dot(tangentialDisplacement.normalized, agent.desiredDirection) >= .85f)
                    {
                        tangentialDisplacement = Vector3.ClampMagnitude(tangentialDisplacement + separateDirection * requestedDistance * .05f, requestedDistance);
                        // sliding（滑移中）：保存 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的返回结果，供当前步骤使用。使用位置：VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
var sliding = ConstrainCrowdDisplacement(tangentialDisplacement, out var /* 中文含义：滑移中阻挡者。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ slidingBlocker);
                        if (sliding.sqrMagnitude > canWalkDisplacement.sqrMagnitude)
                        {
                            canWalkDisplacement = sliding;
                            pathBlocker = slidingBlocker;
                        }
                    }
                }

                if (canWalkDisplacement.sqrMagnitude > displacement.sqrMagnitude + .000004f)
                {
                    displacement = canWalkDisplacement;
                    crowdBlocker = pathBlocker;
                }
            }
        }

        // distance（距离）：使用位置：作为 Mathf.Min(float, float) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            if (strictActionCollision)
            {
                ResetBlockedMovement();
            }
            else
            {
                UpdateBlockedMovement(requestedDistance, startPosition, default, crowdBlocker, timeSlice);
            }

            return GetHorizontalMovementSince(startPosition);
        }

        // direction（方向）：使用位置：作为 CharacterMotionExecutor.CommitNormalPosition(Vector3) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
Vector3 direction = displacement / distance;
        if (!TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            if (!strictActionCollision)
            {
                motionExecutor.CommitNormalPosition(transform.position + displacement);
            }

            ResetBlockedMovement();
            return GetHorizontalMovementSince(startPosition);
        }

        if (!TryFindMovementBlocker(top, bottom, radius, direction, distance + movementCollisionSkin, out RaycastHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit, strictActionCollision))
        {
            motionExecutor.CommitNormalPosition(transform.position + displacement);
            if (strictActionCollision)
            {
                ResetBlockedMovement();
            }
            else
            {
                // 支撑校验也会在胶囊无阻挡时裁短位移，交给同一个卡路计时处理。
                UpdateBlockedMovement(requestedDistance, startPosition, default, crowdBlocker, timeSlice);
            }

            return GetHorizontalMovementSince(startPosition);
        }

        if (!strictActionCollision && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && TryStepOntoWalkableSeam(displacement, hit))
        {
            UpdateBlockedMovement(requestedDistance, startPosition, hit, null, timeSlice);
            return GetHorizontalMovementSince(startPosition);
        }

        // collisionClearance（碰撞净空）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
float collisionClearance;
        if (strictActionCollision)
        {
            collisionClearance = Mathf.Max(0.002f, movementCollisionSkin * 2f);
        }
        else
        {
            collisionClearance = Mathf.Max(0.001f, movementCollisionSkin);
        }

        // allowedDistance（允许的距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
float allowedDistance = Mathf.Max(0f, hit.distance - collisionClearance);
        if (allowedDistance > 0f)
        {
            motionExecutor.CommitNormalPosition(transform.position + direction * Mathf.Min(distance, allowedDistance));
        }

        // consumedDistance（已消耗距离）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
float consumedDistance = Mathf.Min(distance, allowedDistance);
        if (strictActionCollision)
        {
            ResetBlockedMovement();
            return GetHorizontalMovementSince(startPosition);
        }

        // remaining（剩余）：使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
Vector3 remaining = displacement - direction * consumedDistance;
        // horizontalNormal（水平常规）：使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
Vector3 horizontalNormal = hit.normal;
        horizontalNormal.y = 0f;
        if (remaining.sqrMagnitude > 0.000001f && horizontalNormal.sqrMagnitude > 0.0001f)
        {
            horizontalNormal.Normalize();
            // slide（滑移）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController) 的参数 等共 4 处。
Vector3 slide = Vector3.ProjectOnPlane(remaining, horizontalNormal);
            slide.y = 0f;
            slide = ConstrainCrowdDisplacement(slide, out _);
            if (slide.sqrMagnitude > 0.000001f && Vector3.Dot(slide, displacement) > 0f && TryGetMovementCapsule(out top, out bottom, out radius))
            {
                // slideDistance（滑移距离）：使用位置：作为 Mathf.Min(float, float) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)。
float slideDistance = slide.magnitude;
                // slideDirection（滑移方向）：使用位置：作为 CharacterMotionExecutor.CommitNormalPosition(Vector3) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数。
Vector3 slideDirection = slide / slideDistance;
                if (TryFindMovementBlocker(top, bottom, radius, slideDirection, slideDistance + movementCollisionSkin, out RaycastHit /* 中文含义：滑移命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ slideHit, strictActionCollision))
                {
                    slideDistance = Mathf.Max(0f, slideHit.distance - collisionClearance);
                }

                if (slideDistance > 0f)
                {
                    motionExecutor.CommitNormalPosition(transform.position + slideDirection * Mathf.Min(slide.magnitude, slideDistance));
                }
            }
        }

        UpdateBlockedMovement(requestedDistance, startPosition, hit, null, timeSlice);
        return GetHorizontalMovementSince(startPosition);
    }

    /// <summary>GetHorizontalMovementSince 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.MoveWithCollision(Vector3, bool, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="startPosition">中文含义：起始位置。使用位置：VillagerController.GetHorizontalMovementSince(Vector3)。</param>
    private Vector3 GetHorizontalMovementSince(Vector3 startPosition)
    {
        // result（结果）：使用位置：VillagerController.GetHorizontalMovementSince(Vector3)。
Vector3 result = transform.position - startPosition;
        result.y = 0f;
        return result;
    }

    /// <summary>CanRaiseRaiseMovementCapsule 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)、VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SnapToGround(float, float, bool)、VillagerController.TryStepOntoWalkableSeam(Vector3, RaycastHit)；下游（含内部回调体） Mathf.Max(float, float)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)、VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="heightDifference">中文含义：高度差值。使用位置：作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.CanRaiseRaiseMovementCapsule(float)。</param>
    private bool CanRaiseRaiseMovementCapsule(float heightDifference)
    {
        return heightDifference <= Mathf.Max(.1f, maximumGroundStepUp) && TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius) && // 由头顶球体上扫，避免脚底初始重叠遮住同一地图网格中的顶棚。
 !TryFindMovementBlocker(top, top, radius, Vector3.up, heightDifference + movementCollisionSkin, out _);
    }

    /// <summary>TryStepOntoWalkableSeam 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、VillagerController.CanRaiseRaiseMovementCapsule(float)、VillagerController.IsNavigationObstacle(Collider)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)、VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、VillagerController.CanRaiseRaiseMovementCapsule(float)、VillagerController.IsNavigationObstacle(Collider)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)、VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、作为 VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3) 的参数。</param>
    /// <param name="blocker">中文含义：阻挡者。使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsNavigationObstacle(Collider) 的参数 等共 4 处。</param>
    private bool TryStepOntoWalkableSeam(Vector3 displacement, RaycastHit blocker)
    {
        if (blocker.collider == null || IsNavigationObstacle(blocker.collider) ||
            NavigationCollisionCache.Player(blocker.collider) != null || NavigationCollisionCache.Enemy(blocker.collider) != null)
        {
            return false;
        }

        Vector3 position = transform.position;
        Vector3 direction = displacement.normalized;
        Vector3 landPoint;
        var navigation = HouseThreeRegionNavigation.Current;
        if (navigation != null && TryGetMovementCapsule(out _, out Vector3 footSphere, out float footRadius) &&
            navigation.TryReadOuterPlatformSeam(footSphere - Vector3.up * footRadius, direction, footRadius, blocker, out float platformRise))
        {
            landPoint = position + displacement + Vector3.up * (platformRise + 0.015f);
        }
        else if (!TryResolveSupportedNavigationPosition(position + displacement, position.y, out landPoint))
        {
            return false;
        }

        // raiseRaise（抬高抬高）：使用位置：作为 VillagerController.CanRaiseRaiseMovementCapsule(float) 的参数、VillagerController.TryStepOntoWalkableSeam(Vector3, RaycastHit)。
float raiseRaise = landPoint.y - transform.position.y;
        if (raiseRaise <= .02f || blocker.point.y > landPoint.y + movementCollisionSkin || !CanRaiseRaiseMovementCapsule(raiseRaise) || !TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            return false;
        }

        // raiseHeight（抬高高度）：使用位置：作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数。
Vector3 raiseHeight = Vector3.up * raiseRaise;
        // 抬升和前进分别检查完整身体空间；只执行本帧原有水平距离，
        // 不放宽墙体碰撞，也不把角色传送到接缝另一边。
        if (TryFindMovementBlocker(top + raiseHeight, bottom + raiseHeight, radius, displacement.normalized, displacement.magnitude + movementCollisionSkin, out _))
        {
            return false;
        }

        return motionExecutor.CommitNormalPosition(landPoint);
    }

    /// <summary>PlantNearHouseThreeTread 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsStandingAttackSurfaceStable()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)、VillagerController.SnapToGround(float, float, bool)、VillagerController.TryExecuteStairActionDisplacement(Vector3) 等共 7 处；下游（含内部回调体） HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数、作为 Mathf.Abs(float) 的参数。</param>
    internal bool PlantNearHouseThreeTread(Vector3 position)
    {
        // traversal（通行）：使用位置：VillagerController.PlantNearHouseThreeTread(Vector3)。
global::HouseThreeTraversalGeometry traversal;
        if (door03TraversalRoute)
        {
            traversal = door03TraversalRoute;
        }
        else
        {
            traversal = HouseThreeTraversalGeometry.Active;
        }

        return traversal && traversal.TryResolveStairHeight(position, out float /* 中文含义：高度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ height) && Mathf.Abs(position.y - height) < .42f;
    }

    /// <summary>TryExecuteStairActionDisplacement 的业务处理入口；本方法直接依赖 VillagerController.MaintainGroundContact()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.PlantNearHouseThreeTread(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DriveExternalKnockback(Vector3, float, float)、VillagerController.OnAnimatorMove()；下游（含内部回调体） VillagerController.MaintainGroundContact()、VillagerController.MoveWithCollision(Vector3, bool, bool)、VillagerController.PlantNearHouseThreeTread(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="displacement">中文含义：位移。使用位置：作为 VillagerController.MoveWithCollision(Vector3, bool, bool) 的参数、VillagerController.TryExecuteStairActionDisplacement(Vector3)。</param>
    private bool TryExecuteStairActionDisplacement(Vector3 displacement)
    {
        if (!PlantNearHouseThreeTread(transform.position))
        {
            return false;
        }

        displacement.y = 0f;
        bool strictCollision = CurrentState == VillagerState.Attack || CurrentState == VillagerState.CrawlAttack
            || CurrentState == VillagerState.Staggered;
        MoveWithCollision(displacement, strictCollision, false);
        MaintainGroundContact();
        return true;
    }

    /// <summary>TryReadStairSupportHeight 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.Round(float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SnapToGround(float, float, bool)；下游（含内部回调体） HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.Round(float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.TryReadStairSupportHeight(Vector3, out float)。</param>
    /// <param name="resolvedY">中文含义：解算后的纵向。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。</param>
    private bool TryReadStairSupportHeight(Vector3 position, out float resolvedY)
    {
        resolvedY = transform.position.y;
        if (door03TraversalRoute == null)
        {
            door03TraversalRoute = HouseThreeTraversalGeometry.Active;
        }

        if (door03TraversalRoute == null || !door03TraversalRoute.TryResolveStairHeight(position, out float authoredHeight))
        {
            return false;
        }

        float expectedRouteY = authoredHeight + groundOffset;

        // 优先读取实际踏面，在踏面间隙使用楼梯中心线高度。
        // probeOrigin（探测原点）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 probeOrigin = new Vector3(position.x, expectedRouteY + 1.2f, position.z);
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
int hitCount = Physics.RaycastNonAlloc(probeOrigin, Vector3.down, groundHits, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // groundDistance（地面距离）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float groundDistance = float.MaxValue;
        // foundPhysicalStep（是否找到的物理步进）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
bool foundPhysicalStep = false;
        // groundPoint（地面点）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
Vector3 groundPoint = default;
        // i（当前遍历索引）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
for (int i = 0; i < hitCount; i++)
        {
            // hit（命中）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数 等共 6 处。
RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(hit.collider) != null || NavigationCollisionCache.Player(hit.collider) != null)
            {
                continue;
            }

            if (IsNavigationObstacle(hit.collider) || hit.distance >= groundDistance || Vector3.Angle(hit.normal, Vector3.up) > Mathf.Min(28f, maximumGroundSlope))
            {
                continue;
            }

            // physicalSurfaceY（物理表面纵向）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float physicalSurfaceY = hit.point.y + groundOffset;
            // routeHeightDelta（路线高度增量）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float routeHeightDelta = physicalSurfaceY - expectedRouteY;
            // aboveRouteTolerance（上方路线容差）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float aboveRouteTolerance = Mathf.Max(stairPhysicalSurfaceAboveRouteTolerance, stairVisualStepHeight + movementCollisionSkin);
            if (routeHeightDelta > aboveRouteTolerance || routeHeightDelta < -stairPhysicalSurfaceBelowRouteTolerance)
            {
                continue;
            }

            // heightDelta（高度增量）：使用位置：VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float heightDelta = physicalSurfaceY - transform.position.y;
            if (heightDelta > Mathf.Max(0.1f, maximumGroundStepUp) || heightDelta < -Mathf.Max(0.1f, maximumGroundDrop))
            {
                continue;
            }

            groundDistance = hit.distance;
            groundPoint = hit.point;
            foundPhysicalStep = true;
        }

        if (foundPhysicalStep)
        {
            resolvedY = groundPoint.y + groundOffset;
            return true;
        }

        // stepHeight（步进高度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Round(float) 的参数、VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float stepHeight = Mathf.Max(0.05f, stairVisualStepHeight);
        float steppedHeight = Mathf.Round(expectedRouteY / stepHeight) * stepHeight;
        // maximumChange（最大变化）：使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数、VillagerController.TryReadStairSupportHeight(Vector3, out float)。
float maximumChange;
        if (steppedHeight >= transform.position.y)
        {
            maximumChange = Mathf.Max(stepHeight, maximumGroundStepUp);
        }
        else
        {
            maximumChange = Mathf.Max(stepHeight, maximumGroundDrop);
        }

        resolvedY = Mathf.MoveTowards(transform.position.y, steppedHeight, maximumChange);
        return true;
    }

    /// <summary>HorizontalDistance 的业务处理入口；本方法直接依赖 Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.CollectCombatTimelineSnapshot()、VillagerController.HasIncompletePursuitArrival.get 等共 13 处；下游（含内部回调体） Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="a">中文含义：起点（三维向量）。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、VillagerController.HorizontalDistance(Vector3, Vector3)。</param>
    /// <param name="b">中文含义：终点（三维向量）。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、VillagerController.HorizontalDistance(Vector3, Vector3)。</param>
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    /// <summary>DistanceToSegmentHorizontal 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Vector3.Distance(Vector3, Vector3)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Clamp01(float)、Vector3.Distance(Vector3, Vector3)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。</param>
    /// <param name="start">中文含义：起始。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。</param>
    /// <param name="end">中文含义：结束。使用位置：VillagerController.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。</param>
    private static float DistanceToSegmentHorizontal(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;
        // segment（线段）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。
Vector3 segment = end - start;
        // segmentLengthSquared（线段长度平方）：使用位置：作为 Mathf.Clamp01(float) 的参数、VillagerController.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。
float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= 0.0001f)
        {
            return Vector3.Distance(point, start);
        }

        // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
float progress = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSquared);
        return Vector3.Distance(point, start + segment * progress);
    }

    /// <summary>BindingTraversalGeometry 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindingTraversalGeometry()
    {
        if (door03TraversalRoute == null)
        {
            door03TraversalRoute = HouseThreeTraversalGeometry.Active;
        }
    }

    /// <summary>敌人行为、动作与通行的结构化数据 TraversalQueryEntry；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "VillagerController+通行查询项")]
    private struct TraversalQueryEntry
    {
        /// <summary>中文含义：frame 表示“帧”；collisionVersion 表示“碰撞版本”；pathVersion 表示“路径版本”。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。；使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。；使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
        internal int frame, collisionVersion, pathVersion;
        /// <summary>中文含义：startPoint 表示“起始点”；endPoint 表示“结束点”；scale 表示“缩放”。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。；使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。；使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
        internal Vector3 startPoint, endPoint, scale;
        /// <summary>中文含义：朝向。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
        internal Quaternion orientation;
        /// <summary>中文含义：当前敌人移动模式。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
        internal EnemyMovementMode mode;
        /// <summary>中文含义：是否结果。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
        internal bool result;
    }

    /// <summary>中文含义：通行查询缓存。只读字段，初始化后不由普通写入口替换。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
    private readonly TraversalQueryEntry[] traversalQueryCache = new TraversalQueryEntry[8];
    /// <summary>中文含义：通行查询光标。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</summary>
    private int traversalQueryCursor;
    /// <summary>CanMoveDirectlyToNavigationPoint 的业务处理入口；本方法直接依赖 Quaternion.Equals(Quaternion)、Vector3.Equals(Vector3)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.TraversalQueryEntry.TraversalQueryEntry()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolveDropPursuitDestination(Vector3, out Vector3)、PathAgent.AdvanceToVisibleWaypoint()、PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)、PathAgent.ResolvePhysicallyReachablePathOrigin()、VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3) 等共 9 处；下游（含内部回调体） Quaternion.Equals(Quaternion)、Vector3.Equals(Vector3)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.TraversalQueryEntry.TraversalQueryEntry()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Vector3.Equals(Vector3) 的参数、作为 VillagerController.DetectNavigationTraversal(Vector3, bool) 的参数、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</param>
    /// <param name="ignoreAlly">中文含义：是否忽略同伴。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。</param>
    public bool CanMoveDirectlyToNavigationPoint(Vector3 target, bool ignoreAlly = false)
    {
        // 图上起点和路线裁剪只检查静态可达性。同伴的暂时占位由逐帧
        // 人群碰撞处理，不能据此把接入起点改到身后，制造对向互堵。
        if (ignoreAlly)
        {
            return DetectNavigationTraversal(target, true);
        }


        // 中文含义：startPoint 为“起始点”。；scale 为“缩放”。
Vector3 startPoint = transform.position, scale = transform.lossyScale;
        // orientation（朝向）：使用位置：作为 Quaternion.Equals(Quaternion) 的参数、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
Quaternion orientation = transform.rotation;
        // version（版本）：使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
int version = NavigationCollisionCache.Version;
        // pathVersion（路径版本）：使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
int pathVersion;
        if (agent != null)
        {
            pathVersion = agent.PathRequestVersion;
        }
        else
        {
            pathVersion = 0;
        }

        // sequence（序号）：使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
for (int sequence = 0; sequence < traversalQueryCache.Length; sequence++)
        {
            // item（当前通行查询入口）：使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
ref var item = ref traversalQueryCache[sequence];
            if (item.frame == Time.frameCount && item.collisionVersion == version && item.pathVersion == pathVersion && item.startPoint.Equals(startPoint) && item.endPoint.Equals(target) && item.scale.Equals(scale) && item.orientation.Equals(orientation) && item.mode == CurrentMovementMode)
            {
                CombatPerformanceCounters.traversalCacheHit++;
                return item.result;
            }
        }

        // result（是否结果）：保存 VillagerController.DetectNavigationTraversal(Vector3, bool) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)。
bool result = DetectNavigationTraversal(target);
        traversalQueryCache[traversalQueryCursor] = new TraversalQueryEntry
        {
            frame = Time.frameCount,
            collisionVersion = version,
            pathVersion = pathVersion,
            startPoint = startPoint,
            endPoint = target,
            scale = scale,
            orientation = orientation,
            mode = CurrentMovementMode,
            result = result
        };
        traversalQueryCursor = (traversalQueryCursor + 1) % traversalQueryCache.Length;
        return result;
    }

    /// <summary>DetectNavigationTraversal 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 的参数 等共 6 处。</param>
    /// <param name="ignoreAlly">中文含义：是否忽略同伴。使用位置：作为 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数。</param>
    private bool DetectNavigationTraversal(Vector3 target, bool ignoreAlly = false)
    {
        bool requiresSteppedGround = Mathf.Abs(target.y - transform.position.y) > Mathf.Max(0.1f, maximumGroundStepUp);
        if (requiresSteppedGround && !HasContinuousSteppedNavigationGround(target))
        {
            return false;
        }

        // offset（偏移量）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.DetectNavigationTraversal(Vector3, bool)。
Vector3 offset = target - transform.position;
        offset.y = 0f;
        // distance（距离）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.DetectNavigationTraversal(Vector3, bool)。
float distance = offset.magnitude;
        if (distance <= Mathf.Max(0.05f, movementCollisionSkin))
        {
            return true;
        }

        if (requiresSteppedGround)
        {
            return HasContinuousNavigationGround(target) && CanTraverseSupportedNavigationSegment(target, ignoreAlly);
        }

        if (!TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            return false;
        }

        // 长距离胶囊检测若起点贴着整张地图网格，可能只返回地面的初始重叠。
        // 该接触被地面过滤后，后方墙体便被漏掉；用不接触地面的躯干射线补验，
        // 防止路线裁剪跨过墙角，再被逐帧碰撞挡住而反复回头。
        CombatPerformanceCounters.physicsQueryCount++;
        // bodyHitCount（身体命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.DetectNavigationTraversal(Vector3, bool)。
int bodyHitCount = Physics.RaycastNonAlloc((top + bottom) * 0.5f, offset / distance, movementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.DetectNavigationTraversal(Vector3, bool)。
for (int i = 0; i < bodyHitCount; i++)
        {
            // candidate（候选）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、VillagerController.DetectNavigationTraversal(Vector3, bool)。
Collider candidate = movementHits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(candidate) != null || NavigationCollisionCache.Player(candidate) != null)
            {
                continue;
            }

            return false;
        }

        // 平面候选已通过躯干射线，再沿路径逐点检查地面支撑。
        if (!HasContinuousNavigationGround(target))
        {
            return false;
        }

        if (!TryFindMovementBlocker(top, bottom, radius, offset / distance, distance, out RaycastHit /* 中文含义：阻挡者。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ blocker, ignorePlayer: true, ignoreAlly: ignoreAlly))
        {
            return true;
        }

        // 水平胶囊可能撞到可踩踏的低接缝。沿真实支撑高度逐段复核，
        // 不能仅因总高度差不足一级台阶，就把连续上坡误判为不可达。
        return blocker.point.y <= Mathf.Max(transform.position.y, target.y) + Mathf.Max(.05f, maximumGroundStepUp) && CanTraverseSupportedNavigationSegment(target, ignoreAlly);
    }

    /// <summary>TryResolveSupportedNavigationPosition 的业务处理入口；本方法直接依赖 Collider.Raycast(Ray, out RaycastHit, float)、float.IsNegativeInfinity(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.ResolveCrowdOverlap()、VillagerController.SnapToGround(float, float, bool)、VillagerController.TryMutualBlockingYield(VillagerController)、VillagerController.TryStepOntoWalkableSeam(Vector3, RaycastHit)；下游（含内部回调体） Collider.Raycast(Ray, out RaycastHit, float)、float.IsNegativeInfinity(float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="samplePosition">中文含义：采样位置。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 4 处。</param>
    /// <param name="previousHeight">中文含义：前一次高度。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。</param>
    /// <param name="supportedPosition">中文含义：支持的位置。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。</param>
    internal bool TryResolveSupportedNavigationPosition(Vector3 samplePosition, float previousHeight, out Vector3 supportedPosition)
    {
        supportedPosition = default;
        // allowedStep（允许的步进）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
float allowedStep = Mathf.Max(0.1f, maximumGroundStepUp);
        // maximumDrop（最大掉落）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
float maximumDrop = Mathf.Max(0.1f, maximumGroundDrop);
        // actualStair（是否实际楼梯）：保存 VillagerController.PlantNearHouseThreeTread(Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
bool actualStair = PlantNearHouseThreeTread(new Vector3(samplePosition.x, previousHeight, samplePosition.z));
        samplePosition.y = previousHeight + allowedStep + groundProbeHeight;
        CombatPerformanceCounters.physicsQueryCount++;
        // 连续台阶仍按中心踏面推进，宽足底只用于普通地表接缝；
        // 否则前方高一级台阶会过早改变支撑高度，长帧下反复重接路径。
        // count（数量）：使用位置：VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
int count;
        if (actualStair)
        {
            count = Physics.RaycastNonAlloc(samplePosition, Vector3.down, groundHits, allowedStep + maximumDrop + groundProbeHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        else
        {
            count = Physics.SphereCastNonAlloc(samplePosition, NavigationFootBottomRadius(), Vector3.down, groundHits, allowedStep + maximumDrop + groundProbeHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        // bestHeight（最佳高度）：使用位置：作为 float.IsNegativeInfinity(float) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
float bestHeight = float.NegativeInfinity;
        // i（当前遍历索引）：使用位置：VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
for (int i = 0; i < count; i++)
        {
            // hit（命中）：使用位置：作为 Ray.Ray(Vector3, Vector3) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
RaycastHit hit = groundHits[i];
            // other（另一个碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsEnemyStairBridgeCollider(Collider) 的参数 等共 5 处。
Collider other = hit.collider;
            if (other == null || other.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(other) != null || NavigationCollisionCache.Player(other) != null || IsNavigationObstacle(other))
            {
                continue;
            }

            // 球形探测在木板边缘返回的是接触法线，不能据此把水平踏面当作陡坡。
            // 与攻击支撑检查一致，在同一碰撞体的实际接触点复核踏面，不扩大跨空范围。
            if (!actualStair && other.Raycast(new Ray(hit.point + Vector3.up * .1f, Vector3.down), out RaycastHit /* 中文含义：踏面命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ treadHit, .2f))
            {
                hit = treadHit;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > maximumGroundSlope)
            {
                continue;
            }

            // height（高度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)。
float height = hit.point.y + groundOffset;
            if (height - previousHeight > allowedStep || previousHeight - height > maximumDrop || height <= bestHeight)
            {
                continue;
            }

            bestHeight = height;
            supportedPosition = new Vector3(samplePosition.x, height, samplePosition.z);
        }

        return !float.IsNegativeInfinity(bestHeight);
    }

    /// <summary>NavigationFootBottomRadius 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.HasAttackRecoveryGroundSupport()、VillagerController.HasContinuousNavigationGround(Vector3)、VillagerController.IsStandingAttackSurfaceStable()、VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float NavigationFootBottomRadius()
    {
        if (movementCollider != null)
        {
            return Mathf.Clamp(movementCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) * .8f, .05f, .28f);
        }
        else
        {
            return .1f;
        }
    }

    /// <summary>CanTraverseSupportedNavigationSegment 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.CeilToInt(float)、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DetectNavigationTraversal(Vector3, bool)；下游（含内部回调体） Mathf.Abs(float)、Mathf.CeilToInt(float)、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Player(Collider)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数。</param>
    /// <param name="ignoreAlly">中文含义：是否忽略同伴。使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。</param>
    private bool CanTraverseSupportedNavigationSegment(Vector3 target, bool ignoreAlly = false)
    {
        if (!TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            return false;
        }

        // origin（原点）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
Vector3 origin = transform.position;
        // previous（前一次）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?) 的参数 等共 5 处。
Vector3 previous = origin;
        // count（数量）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
int count = Mathf.Clamp(Mathf.CeilToInt(HorizontalDistance(origin, target) / 0.12f), 1, 64);
        // step（步进）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
for (int step = 1; step <= count; step++)
        {
            // sample（采样）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3) 的参数。
Vector3 sample = Vector3.Lerp(origin, target, step / (float)count);
            if (!TryResolveSupportedNavigationPosition(sample, previous.y, out Vector3 /* 中文含义：支持的。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ supported))
            {
                return false;
            }

            // offset（偏移量）：使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 offset = previous - origin;
            // segment（线段）：使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
Vector3 segment = supported - previous;
            // distance（距离）：使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?) 的参数 等共 4 处。
float distance = segment.magnitude;
            if (distance > 0.0001f)
            {
                // 躯干与胶囊沿真实踏面的高度前进，不能水平穿入上升屋顶后误报墙体。
                CombatPerformanceCounters.physicsQueryCount++;
                // bodyCount（身体数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
int bodyCount = Physics.RaycastNonAlloc((top + bottom) * 0.5f + offset, segment / distance, movementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                // i（当前遍历索引）：使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
for (int i = 0; i < bodyCount; i++)
                {
                    // other（另一个碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
Collider other = movementHits[i].collider;
                    if (other != null && !other.transform.IsChildOf(transform) && NavigationCollisionCache.Enemy(other) == null && NavigationCollisionCache.Player(other) == null)
                    {
                        return false;
                    }
                }

                CombatPerformanceCounters.physicsQueryCount++;
                // capsuleCount（胶囊数量）：保存 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
int capsuleCount = Physics.CapsuleCastNonAlloc(top + offset, bottom + offset, Mathf.Max(0.01f, radius - movementCollisionSkin), segment / distance, movementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                // i（当前遍历索引）：使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
for (int i = 0; i < capsuleCount; i++)
                {
                    // hit（命中）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数 等共 7 处。
RaycastHit hit = movementHits[i];
                    if (NavigationCollisionCache.Player(hit.collider) != null || (ignoreAlly && NavigationCollisionCache.Enemy(hit.collider) != null) || ShouldIgnoreMovementCollider(hit.collider, hit.point) || ShouldDeferCurrentIngressDoorBlocker(hit.collider) || IsInvalidInitialMovementHit(hit, radius, distance, previous))
                    {
                        continue;
                    }

                    // 只放行脚下可踩踏坡面，墙面及高于踏面的碰撞继续阻止裁剪。
                    if (Vector3.Angle(hit.normal, Vector3.up) <= maximumGroundSlope && hit.point.y <= Mathf.Max(previous.y, supported.y) + Mathf.Max(0.02f, movementCollisionSkin * 2f))
                    {
                        continue;
                    }

                    return false;
                }
            }

            previous = supported;
        }

        // leftOperand156（计算前缓存的左侧绝对值的返回结果）：从绝对值的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
float leftOperand156 = Mathf.Abs(previous.y - target.y);
        // argumentValue155（按原顺序缓存的实参最大地面步进向上）：从最大地面步进向上读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue155 = maximumGroundStepUp;
        // selectedValue154（分支选出的寻路代理的路径点竖直已抵达容差）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)。
float selectedValue154;
        if (agent != null)
        {
            selectedValue154 = agent.waypointVerticalReachedTolerance;
        }
        else
        {
            selectedValue154 = 0.35f;
        }

        return leftOperand156 <= Mathf.Max(argumentValue155, selectedValue154);
    }

    /// <summary>
    /// 跳过悬高的压缩路点前验证后续真实踏面及身体空间。
    /// </summary>
    /// <remarks>静态调用链：上游 PathAgent.CanAdvancePastCompressedSteppedWaypoint()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HasContinuousSteppedNavigationGround(Vector3)、VillagerController.HorizontalDistance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="compressedWaypoint">中文含义：压缩后的路径点。使用位置：作为 Mathf.Abs(float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数。</param>
    /// <param name="followingWaypoint">中文含义：跟随中路径点。使用位置：作为 VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool) 的参数、作为 VillagerController.HasContinuousSteppedNavigationGround(Vector3) 的参数、VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)。</param>
    public bool CanAdvancePastCompressedSteppedWaypoint(Vector3 compressedWaypoint, Vector3 followingWaypoint)
    {
        if (agent == null)
        {
            return false;
        }

        // horizontalDistance（水平距离）：保存 VillagerController.HorizontalDistance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)。
float horizontalDistance = HorizontalDistance(transform.position, compressedWaypoint);
        if (horizontalDistance > Mathf.Max(0.05f, agent.waypointReachedRadius) || Mathf.Abs(transform.position.y - compressedWaypoint.y) <= Mathf.Max(0.05f, agent.waypointVerticalReachedTolerance))
        {
            return false;
        }

        // followingOffset（跟随中偏移量）：使用位置：VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)。
Vector3 followingOffset = followingWaypoint - transform.position;
        followingOffset.y = 0f;
        if (followingOffset.sqrMagnitude <= 0.01f || !HasContinuousSteppedNavigationGround(followingWaypoint))
        {
            return false;
        }

        return CanMoveDirectlyToNavigationPoint(followingWaypoint);
    }

    /// <summary>
    /// 寻路图的一个拐点可以跨越数级真实台阶。不能把该拐点的总高度差
    /// 误判成一次要跨越的垂直墙；逐段验证真实踏面后才允许继续移动。
    /// 这是一条通用地形规则，不依赖任何门或建筑名称。
    /// </summary>
    /// <remarks>静态调用链：上游 PathAgent.ResolvePendingPathHandoffWaypoint(List&lt;Vector3&gt;)、VillagerController.CanAdvancePastCompressedSteppedWaypoint(Vector3, Vector3)、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.TryStartCrowdBypass(VillagerController)；下游（含内部回调体） Mathf.Abs(float)、Mathf.CeilToInt(float)、Mathf.Clamp(float, float, float)、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)、Mathf.Min(float, float)、NavigationCollisionCache.Enemy(Collider) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 5 处。</param>
    public bool HasContinuousSteppedNavigationGround(Vector3 target)
    {
        // horizontalOffset（水平偏移量）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
Vector3 horizontalOffset = target - transform.position;
        horizontalOffset.y = 0f;
        // horizontalDistance（水平距离）：使用位置：作为 Mathf.CeilToInt(float) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float horizontalDistance = horizontalOffset.magnitude;
        if (horizontalDistance <= 0.1f)
        {
            return false;
        }

        // allowedStepUp（允许的步进向上）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float allowedStepUp = Mathf.Max(0.1f, ResolveAllowedPathStepUp());
        // maximumDrop（最大掉落）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float maximumDrop = Mathf.Max(0.1f, maximumGroundDrop);
        // maximumSurfaceHeight（最大表面高度）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float maximumSurfaceHeight = Mathf.Max(transform.position.y, target.y) + allowedStepUp + groundProbeHeight;
        // sampleCount（采样数量）：保存 Mathf.Clamp(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
int sampleCount = Mathf.Clamp(Mathf.CeilToInt(horizontalDistance / 0.12f), 2, 48);
        // previousHeight（前一次高度）：使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float previousHeight = transform.position.y;
        // sampleIndex（采样索引）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
for (int sampleIndex = 1; sampleIndex <= sampleCount; sampleIndex++)
        {
            // samplePosition（采样位置）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
Vector3 samplePosition = Vector3.Lerp(transform.position, target, sampleIndex / (float)sampleCount);
            samplePosition.y = maximumSurfaceHeight;
            CombatPerformanceCounters.physicsQueryCount++;
            // hitCount（命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
int hitCount = Physics.RaycastNonAlloc(samplePosition, Vector3.down, groundHits, maximumSurfaceHeight - Mathf.Min(transform.position.y, target.y) + maximumDrop + groundProbeHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // foundStep（是否找到的步进）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
bool foundStep = false;
            // highestReachableHeight（最高可抵达高度）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float highestReachableHeight = float.NegativeInfinity;
            // hitIndex（命中索引）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                // hit（命中）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
RaycastHit hit = groundHits[hitIndex];
                // hitCollider（命中的碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsNavigationObstacle(Collider) 的参数 等共 4 处。
Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(hitCollider) != null || NavigationCollisionCache.Player(hitCollider) != null || IsNavigationObstacle(hitCollider) || Vector3.Angle(hit.normal, Vector3.up) > Mathf.Clamp(maximumGroundSlope, 1f, 89f))
                {
                    continue;
                }

                // surfaceHeight（表面高度）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float surfaceHeight = hit.point.y + groundOffset;
                // heightChange（高度变化）：使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float heightChange = surfaceHeight - previousHeight;
                if (heightChange > allowedStepUp || heightChange < -maximumDrop || surfaceHeight <= highestReachableHeight)
                {
                    continue;
                }

                highestReachableHeight = surfaceHeight;
                foundStep = true;
            }

            if (!foundStep)
            {
                return false;
            }

            previousHeight = highestReachableHeight;
        }

        // leftOperand159（计算前缓存的左侧绝对值的返回结果）：从绝对值的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float leftOperand159 = Mathf.Abs(previousHeight - target.y);
        // argumentValue158（按原顺序缓存的实参允许的步进向上）：从允许的步进向上读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue158 = allowedStepUp;
        // selectedValue157（分支选出的寻路代理的路径点竖直已抵达容差）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.HasContinuousSteppedNavigationGround(Vector3)。
float selectedValue157;
        if (agent != null)
        {
            selectedValue157 = agent.waypointVerticalReachedTolerance;
        }
        else
        {
            selectedValue157 = allowedStepUp;
        }

        return leftOperand159 <= Mathf.Max(argumentValue158, selectedValue157);
    }

    /// <summary>HasContinuousNavigationGround 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Abs(float)、Mathf.CeilToInt(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Max(int, int)、NavigationCollisionCache.Enemy(Collider) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DetectNavigationTraversal(Vector3, bool)；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Abs(float)、Mathf.CeilToInt(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Max(int, int)、NavigationCollisionCache.Enemy(Collider) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。</param>
    private bool HasContinuousNavigationGround(Vector3 target)
    {
        var house = HouseThreeRegionNavigation.Current;
        if (house != null
            && house.TreadRegion(transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform
            && house.TreadRegion(target) == VillageTraversalRegion.SecondFloorOuterPlatform)
        {
            return HasContinuousPlatformFootprint(target);
        }

        // horizontalOffset（水平偏移量）：使用位置：VillagerController.HasContinuousNavigationGround(Vector3)。
Vector3 horizontalOffset = target - transform.position;
        horizontalOffset.y = 0f;
        // horizontalDistance（水平距离）：使用位置：作为 Mathf.CeilToInt(float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
float horizontalDistance = horizontalOffset.magnitude;
        if (horizontalDistance <= 0.1f)
        {
            return true;
        }

        // sampleCount（采样数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
int sampleCount = Mathf.Max(2, Mathf.CeilToInt(horizontalDistance / 0.25f));
        // argumentValue161（按原顺序缓存的实参最大地面步进向上）：从最大地面步进向上读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue161 = maximumGroundStepUp;
        // selectedValue160（分支选出的寻路代理的路径点竖直已抵达容差）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
float selectedValue160;
        if (agent != null)
        {
            selectedValue160 = agent.waypointVerticalReachedTolerance;
        }
        else
        {
            selectedValue160 = maximumGroundStepUp;
        }

        // allowedHeightDifference（允许的高度差值）：使用位置：VillagerController.HasContinuousNavigationGround(Vector3)。
float allowedHeightDifference = Mathf.Max(argumentValue161, selectedValue160) + 0.05f;
        float supportRadius = NavigationFootBottomRadius();
        float allowedSlope = Mathf.Clamp(maximumGroundSlope, 1f, 89f);
        // i（当前遍历索引）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
for (int i = 1; i <= sampleCount; i++)
        {
            // expected（预期）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、作为 Mathf.Abs(float) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
Vector3 expected = Vector3.Lerp(transform.position, target, i / (float)sampleCount);
            // origin（原点）：使用位置：作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 origin = expected + Vector3.up * 1f;
            // 平台木板窄缝可能让中心射线漏地，但角色足底仍有真实支撑。
            // 使用小于身体半径的足底范围；坡度、高差和整段胶囊阻挡仍须通过。
            CombatPerformanceCounters.physicsQueryCount++;
            // hitCount（命中数量）：保存 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasContinuousNavigationGround(Vector3)。
int hitCount = Physics.SphereCastNonAlloc(origin, supportRadius, Vector3.down, groundHits, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // supported（是否支持的）：使用位置：VillagerController.HasContinuousNavigationGround(Vector3)。
bool supported = false;
            // hitIndex（命中索引）：使用位置：VillagerController.HasContinuousNavigationGround(Vector3)。
for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                // hit（命中）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.HasContinuousNavigationGround(Vector3)。
RaycastHit hit = groundHits[hitIndex];
                // hitCollider（命中的碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数、作为 VillagerController.IsEnemyStairBridgeCollider(Collider) 的参数 等共 5 处。
Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform) || NavigationCollisionCache.Enemy(hitCollider) != null || NavigationCollisionCache.Player(hitCollider) != null || IsNavigationObstacle(hitCollider))
                {
                    continue;
                }

                // 辅助斜桥不能代替可见台阶，避免侧面高节点被视作可直接抵达。
                if (Vector3.Angle(hit.normal, Vector3.up) > allowedSlope)
                {
                    continue;
                }

                if (Mathf.Abs(hit.point.y + groundOffset - expected.y) > allowedHeightDifference)
                {
                    continue;
                }

                supported = true;
                break;
            }

            if (!supported && DoorController.IsInsideOpenPassage(expected, 1.2f))
            {
                supported = true;
            }

            if (!supported)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ResolveMovementOverlap 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Max(float, float)、Mathf.Min(float, float)、Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float)、Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction)、Vector3.ClampMagnitude(Vector3, float)、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalPosition(Vector3)、Mathf.Max(float, float)、Mathf.Min(float, float)、Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float)、Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction)、Vector3.ClampMagnitude(Vector3, float)、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="strictActionCollision">中文含义：是否严格动作碰撞。使用位置：作为 VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool) 的参数、VillagerController.ResolveMovementOverlap(bool)。</param>
    private void ResolveMovementOverlap(bool strictActionCollision = false)
    {
        if (!TryGetMovementCapsule(out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            return;
        }

        // overlapCount（重叠数量）：保存 Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.ResolveMovementOverlap(bool)。
int overlapCount = Physics.OverlapCapsuleNonAlloc(top, bottom, Mathf.Max(0.01f, radius), movementOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // recovery（恢复）：使用位置：作为 Vector3.ClampMagnitude(Vector3, float) 的参数、VillagerController.ResolveMovementOverlap(bool)。
Vector3 recovery = Vector3.zero;
        // i（当前遍历索引）：使用位置：VillagerController.ResolveMovementOverlap(bool)。
for (int i = 0; i < overlapCount; i++)
        {
            // other（另一个碰撞体）：使用位置：作为 Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float) 的参数、作为 VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool) 的参数、VillagerController.ResolveMovementOverlap(bool)。
Collider other = movementOverlaps[i];
            if (ShouldIgnoreMovementCollider(other, transform.position, strictActionCollision))
            {
                continue;
            }

            // conditionResult165（短路判断得到的分支判断）：保存短路分支结果，未选中的分支不执行。使用位置：VillagerController.ResolveMovementOverlap(bool)。
bool conditionResult165;
            if (!strictActionCollision)
            {
                // argumentValue164（按原顺序缓存的实参另一个碰撞体）：从另一个读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 的参数。
global::UnityEngine.Collider argumentValue164 = other;
                // selectedValue163（分支选出的寻路代理的期望方向）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 的参数、VillagerController.ResolveMovementOverlap(bool)。
global::UnityEngine.Vector3 selectedValue163;
                if (agent != null)
                {
                    selectedValue163 = agent.desiredDirection;
                }
                else
                {
                    selectedValue163 = transform.forward;
                }

                conditionResult165 = CanTraverseCurrentPathGround(argumentValue164, selectedValue163, radius);
            }
            else
            {
                conditionResult165 = false;
            }

            if (conditionResult165)
            {
                continue;
            }

            if (!Physics.ComputePenetration(movementCollider, transform.position, transform.rotation, other, other.transform.position, other.transform.rotation, out Vector3 /* 中文含义：方向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ direction, out float /* 中文含义：距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ distance))
            {
                continue;
            }

            if (direction.y > 0.65f)
            {
                continue;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            recovery += direction.normalized * Mathf.Min(distance + movementCollisionSkin, overlapRecoveryDistance);
        }

        recovery.y = 0f;
        if (recovery.sqrMagnitude > 0.0001f)
        {
            motionExecutor.CommitNormalPosition(transform.position + Vector3.ClampMagnitude(recovery, overlapRecoveryDistance));
        }
    }

    /// <summary>UpdateBlockedMovement 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、object.ReferenceEquals(object, object)、PathAgent.ForceRepath()、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)；下游（含内部回调体） Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、object.ReferenceEquals(object, object)、PathAgent.ForceRepath()、PathAgent.RegisterStaticObstacleHazard(RaycastHit, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requestedDistance">中文含义：请求的距离。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="startPosition">中文含义：起始位置。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</param>
    /// <param name="blocker">中文含义：阻挡者。使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 PathAgent.RegisterStaticObstacleHazard(RaycastHit, float) 的参数、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</param>
    /// <param name="crowdBlocker">中文含义：人群阻挡者。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</param>
    /// <param name="timeSlice">中文含义：时间片段。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。</param>
    private void UpdateBlockedMovement(float requestedDistance, Vector3 startPosition, RaycastHit blocker = default, VillagerController crowdBlocker = null, float timeSlice = -1f)
    {
        // actual（实际）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
Vector3 actual = transform.position - startPosition;
        actual.y = 0f;
        // expectedDirection（预期方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
Vector3 expectedDirection;
        if (agent != null)
        {
            expectedDirection = agent.desiredDirection;
        }
        else
        {
            expectedDirection = transform.forward;
        }

        expectedDirection.y = 0f;
        if (expectedDirection.sqrMagnitude > 0.0001f)
        {
            expectedDirection.Normalize();
        }

        // forwardProgress（前向进度）：使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
float forwardProgress;
        if (expectedDirection.sqrMagnitude > 0.0001f)
        {
            forwardProgress = Vector3.Dot(actual, expectedDirection);
        }
        else
        {
            forwardProgress = actual.magnitude;
        }

        if (forwardProgress >= Mathf.Max(0.0005f, requestedDistance * 0.25f))
        {
            ResetBlockedMovement();
            return;
        }

        if (crowdBlocker != null)
        {
            lastMovementBlockerEnemy = crowdBlocker;
        }
        else
        {
            // selectedValue168（分支选出的敌人的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
global::VillagerController selectedValue168;
            if (blocker.collider != null)
            {
                selectedValue168 = NavigationCollisionCache.Enemy(blocker.collider);
            }
            else
            {
                selectedValue168 = null;
            }

            lastMovementBlockerEnemy = (selectedValue168);
        }

        // previousValue170（复合赋值前保存的移动阻挡持续时间）：从移动阻挡持续时间读取一次后保存，供后续分支使用。在右侧分支前保存复合赋值原值，保持原读取顺序。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
float previousValue170 = movementBlockedDuration;
        // selectedValue171（分支选出的时间片段）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
float selectedValue171;
        if (timeSlice >= 0f)
        {
            selectedValue171 = timeSlice;
        }
        else
        {
            selectedValue171 = Time.deltaTime;
        }

        movementBlockedDuration = (float)(previousValue170 + selectedValue171);
        if (movementBlockedDuration < blockedRepathDelay || Time.time < nextBlockedRepathTime)
        {
            return;
        }

        nextBlockedRepathTime = Time.time + blockedRepathInterval;
        if (lastMovementBlockerEnemy != null && TryStartCrowdBypass(lastMovementBlockerEnemy))
        {
            return;
        }

        // registeredHazard（是否已登记危险区）：使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
bool registeredHazard = agent != null && blocker.collider != null && agent.RegisterStaticObstacleHazard(blocker, ResolveMovementHazardRadius());
        var region = HouseThreeRegionNavigation.Current;
        bool blockedOnPlatform = region != null && region.TreadRegion(transform.position) == VillageTraversalRegion.SecondFloorOuterPlatform
            && lastMovementBlockerEnemy == null;
        if (!registeredHazard && agent != null
            && (agent.CurrentPathState == PathAgentState.Failed || blockedOnPlatform))
        {
            // receiverValue172（判空前缓存的寻路代理）：从寻路代理读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
global::PathAgent receiverValue172 = agent;
            // optionalValue173（条件访问得到的力度重新寻路的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)。
bool? optionalValue173;
            if (!global::System.Object.ReferenceEquals(receiverValue172, null))
            {
                optionalValue173 = receiverValue172.ForceRepath();
            }
            else
            {
                optionalValue173 = default;
            }
        }
    }

    /// <summary>ResetBlockedMovement 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetBlockedMovement()
    {
        lastMovementBlockerEnemy = null;
        movementBlockedDuration = 0f;
    }

    /// <summary>ResolveMovementHazardRadius 的业务处理入口；本方法直接依赖 VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ConstrainCrowdDisplacement(Vector3, out VillagerController)、VillagerController.IsCrowdBypassDestinationOccupied(Vector3)、VillagerController.ResolveCrowdOverlap()、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)、VillagerController.TryResolveDoor03IngressFinalSteering(out Vector3) 等共 7 处；下游（含内部回调体） VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveMovementHazardRadius()
    {
        if (TryGetMovementCapsule(out _, out _, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius))
        {
            return radius;
        }

        return 0.35f;
    }

    /// <summary>TryGetMovementCapsule 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanRaiseRaiseMovementCapsule(float)、VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.ResolveMovementHazardRadius() 等共 7 处；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="top">中文含义：顶部。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。</param>
    /// <param name="bottom">中文含义：底部。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。</param>
    /// <param name="radius">中文含义：半径。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。</param>
    private bool TryGetMovementCapsule(out Vector3 top, out Vector3 bottom, out float radius, bool includeDisabled = false)
    {
        top = default;
        bottom = default;
        radius = 0f;
        if (movementCollider == null || !movementCollider.enabled && !includeDisabled)
        {
            return false;
        }

        // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = transform.lossyScale;
        // horizontalScale（水平缩放）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。
float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        // verticalScale（竖直缩放）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float verticalScale = Mathf.Abs(scale.y);
        radius = movementCollider.radius * horizontalScale;
        // height（高度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float height = Mathf.Max(movementCollider.height * verticalScale, radius * 2f);
        // halfSegment（一半线段）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。
float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        // center（中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryGetMovementCapsule(out Vector3, out Vector3, out float)。
Vector3 center = transform.TransformPoint(movementCollider.center);
        top = center + Vector3.up * halfSegment;
        bottom = center - Vector3.up * halfSegment;
        return true;
    }

    /// <summary>TryFindMovementBlocker 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Name(Collider)、NavigationCollisionCache.Player(Collider)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanRaiseRaiseMovementCapsule(float)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.ExecuteCollisionMovement(Vector3, bool, bool, float)、VillagerController.TryStepOntoWalkableSeam(Vector3, RaycastHit)；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Name(Collider)、NavigationCollisionCache.Player(Collider)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="top">中文含义：顶部。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="bottom">中文含义：底部。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数、作为 VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?) 的参数。</param>
    /// <param name="nearestHit">中文含义：最近命中。输出参数，由本入口写入并交给调用方。使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。</param>
    /// <param name="strictActionCollision">中文含义：是否严格动作碰撞。使用位置：作为 VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool) 的参数、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。</param>
    /// <param name="ignorePlayer">中文含义：是否忽略玩家。使用位置：作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。</param>
    /// <param name="ignoreAlly">中文含义：是否忽略同伴。使用位置：作为 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool) 的参数、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。</param>
    internal Vector3 PreviewStandingAttackDisplacement(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= .0001f || !TryGetMovementCapsule(out Vector3 top, out Vector3 bottom, out float radius))
        {
            return Vector3.zero;
        }

        Vector3 direction = displacement / distance;
        if (!TryFindMovementBlocker(top, bottom, radius, direction, distance + movementCollisionSkin,
            out RaycastHit hit, strictActionCollision: true))
        {
            return displacement;
        }

        float clearance = Mathf.Max(.002f, movementCollisionSkin * 2f);
        return direction * Mathf.Clamp(hit.distance - clearance, 0f, distance);
    }

    private bool TryFindMovementBlocker(Vector3 top, Vector3 bottom, float radius, Vector3 direction, float distance, out RaycastHit nearestHit, bool strictActionCollision = false, bool ignorePlayer = false, bool ignoreAlly = false)
    {
        nearestHit = default;
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。
int hitCount = Physics.CapsuleCastNonAlloc(top, bottom, Mathf.Max(0.01f, radius - movementCollisionSkin), direction, movementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // nearestDistance（最近距离）：使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。
float nearestDistance = float.MaxValue;
        // allowedStepUp（允许的步进向上）：保存 VillagerController.ResolveAllowedPathStepUp() 的返回结果，供当前步骤使用。使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。
float allowedStepUp = ResolveAllowedPathStepUp();
        // found（是否已找到）：使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。
bool found = false;
        // i（当前遍历索引）：使用位置：VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)。
for (int i = 0; i < hitCount; i++)
        {
            // hitCollider（命中的碰撞体）：使用位置：作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Name(Collider) 的参数、作为 NavigationCollisionCache.Player(Collider) 的参数 等共 8 处。
Collider hitCollider = movementHits[i].collider;
            if (ignoreAlly && NavigationCollisionCache.Enemy(hitCollider) != null)
            {
                continue;
            }

            // 路线预检忽略玩家占位，实际移动仍检测玩家，避免远处旧路点无法推进。
            if (ignorePlayer && hitCollider != null && NavigationCollisionCache.Player(hitCollider) != null)
            {
                continue;
            }

            if (ShouldIgnoreMovementCollider(hitCollider, movementHits[i].point, strictActionCollision))
            {
                continue;
            }

            if (!strictActionCollision && ShouldDeferCurrentIngressDoorBlocker(hitCollider))
            {
                continue;
            }

            if (!strictActionCollision && IsInvalidInitialMovementHit(movementHits[i], radius, distance))
            {
                continue;
            }

            if (!strictActionCollision && movementHits[i].distance <= movementCollisionSkin * 2f && movementHits[i].normal.y > 0.5f && CanTraverseCurrentPathGround(hitCollider, direction, Mathf.Max(radius, distance)))
            {
                continue;
            }

            if (movementHits[i].distance <= movementCollisionSkin * 2f && IsSupportingGroundContact(hitCollider))
            {
                continue;
            }

            if (Vector3.Angle(movementHits[i].normal, Vector3.up) <= Mathf.Clamp(maximumGroundSlope, 1f, 89f) && movementHits[i].point.y <= transform.position.y + allowedStepUp)
            {
                continue;
            }

            if (movementHits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = movementHits[i].distance;
            nearestHit = movementHits[i];
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 门洞阶段已经锁定某扇门时，远处的关闭门叶不能在寻路预检中被
    /// 当成整条路线都不可走的墙，否则敌人永远到不了开门探测距离。
    /// 真正接近门时仍由门洞阶段先开门；该豁免仅用于到达开门距离前。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） Component.GetComponentInParent&lt;DoorController&gt;()、Mathf.Max(float, float)、VillagerController.HorizontalDistance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">中文含义：碰撞体。使用位置：VillagerController.ShouldDeferCurrentIngressDoorBlocker(Collider)。</param>
    private bool ShouldDeferCurrentIngressDoorBlocker(Collider collider)
    {
        if (pursuitIngressDoor == null || pursuitIngressDoor.IsOpen || collider == null)
        {
            return false;
        }

        // colliderDoor（碰撞体门）：保存 Component.GetComponentInParent<DoorController>() 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldDeferCurrentIngressDoorBlocker(Collider)。
DoorController colliderDoor = collider.GetComponentInParent<DoorController>();
        if (colliderDoor != pursuitIngressDoor)
        {
            return false;
        }

        // openingDistance（开口距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldDeferCurrentIngressDoorBlocker(Collider)。
float openingDistance = Mathf.Max(0.2f, doorOpenProbeDistance * 0.75f);
        return HorizontalDistance(transform.position, pursuitIngressDoor.PassageCenter) > openingDistance;
    }

    /// <summary>IsInvalidInitialMovementHit 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hit">中文含义：命中。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。</param>
    /// <param name="radius">中文含义：半径。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。</param>
    /// <param name="castDistance">中文含义：投射距离。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。</param>
    /// <param name="castOrigin">中文含义：投射原点。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。</param>
    private bool IsInvalidInitialMovementHit(RaycastHit hit, float radius, float castDistance, Vector3? castOrigin = null)
    {
        // contactThreshold（接触阈值）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
float contactThreshold = Mathf.Max(0.005f, movementCollisionSkin * 2f);
        if (hit.distance > contactThreshold)
        {
            return false;
        }

        // point（点）：使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数、VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
Vector3 point = hit.point;
        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
        {
            return true;
        }

        // availableValue174（空值判断前缓存的投射原点）：从投射原点读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
global::UnityEngine.Vector3? availableValue174 = castOrigin;
        // queryOrigin（查询原点）：使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
Vector3 queryOrigin;
        if (availableValue174.HasValue)
        {
            queryOrigin = availableValue174.Value;
        }
        else
        {
            queryOrigin = transform.position;
        }

        // offset（偏移量）：使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
Vector3 offset = point - queryOrigin;
        offset.y = 0f;
        // maximumLocalDistance（最大局部距离）：使用位置：VillagerController.IsInvalidInitialMovementHit(RaycastHit, float, float, Vector3?)。
float maximumLocalDistance = radius + castDistance + 0.2f;
        if (offset.sqrMagnitude > maximumLocalDistance * maximumLocalDistance)
        {
            return true;
        }

        // 胶囊起始嵌入合并网格时，物理查询会用世界原点作为无效命中点。
        // 只有敌人本身就在原点附近时，原点才可能是一个真实接触点。
        if (point.sqrMagnitude <= 0.000001f && queryOrigin.sqrMagnitude > maximumLocalDistance * maximumLocalDistance)
        {
            return true;
        }

        return false;
    }

    /// <summary>HasRaisedMovementBlocker 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Max(float, float)、Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="expectedCollider">中文含义：预期碰撞体。使用位置：VillagerController.HasRaisedMovementBlocker(Collider, Vector3, Vector3, float, Vector3, float)。</param>
    /// <param name="top">中文含义：顶部。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="bottom">中文含义：底部。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    private bool HasRaisedMovementBlocker(Collider expectedCollider, Vector3 top, Vector3 bottom, float radius, Vector3 direction, float distance)
    {
        // clearance（净空）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
float clearance = Mathf.Max(0.08f, movementCollisionSkin * 3f);
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.CapsuleCastNonAlloc(Vector3, Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.HasRaisedMovementBlocker(Collider, Vector3, Vector3, float, Vector3, float)。
int hitCount = Physics.CapsuleCastNonAlloc(top + Vector3.up * clearance, bottom + Vector3.up * clearance, Mathf.Max(0.01f, radius - movementCollisionSkin), direction, raisedMovementHits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.HasRaisedMovementBlocker(Collider, Vector3, Vector3, float, Vector3, float)。
for (int i = 0; i < hitCount; i++)
        {
            if (raisedMovementHits[i].collider == expectedCollider)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>IsSupportingGroundContact 的业务处理入口；本方法直接依赖 Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float)、VillagerController.ResolveAllowedPathStepUp()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float)、VillagerController.ResolveAllowedPathStepUp()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">中文含义：碰撞体。使用位置：作为 Physics.ComputePenetration(Collider, Vector3, Quaternion, Collider, Vector3, Quaternion, out Vector3, out float) 的参数、VillagerController.IsSupportingGroundContact(Collider)。</param>
    private bool IsSupportingGroundContact(Collider collider)
    {
        if (movementCollider == null || collider == null)
        {
            return false;
        }

        if (!Physics.ComputePenetration(movementCollider, transform.position, transform.rotation, collider, collider.transform.position, collider.transform.rotation, out Vector3 /* 中文含义：分离量方向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ separationDirection, out float /* 中文含义：分离量距离。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ separationDistance))
        {
            return false;
        }

        return separationDirection.y > 0.65f && separationDistance <= ResolveAllowedPathStepUp() + movementCollisionSkin;
    }

    /// <summary>ResolveAllowedPathStepUp 的业务处理入口；本方法直接依赖 Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、PathAgent.TryGetCurrentWaypoint(out Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)、VillagerController.HasContinuousSteppedNavigationGround(Vector3)、VillagerController.IsSupportingGroundContact(Collider)、VillagerController.SnapToGround(float, float, bool)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、PathAgent.TryGetCurrentWaypoint(out Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveAllowedPathStepUp()
    {
        // allowedStepUp（允许的步进向上）：使用位置：作为 Mathf.Max(float, float) 的参数、VillagerController.ResolveAllowedPathStepUp()。
float allowedStepUp = maximumGroundStepUp;
        if (agent == null || !agent.TryGetCurrentWaypoint(out Vector3 /* 中文含义：路径点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ waypoint))
        {
            return allowedStepUp;
        }

        // verticalChange（竖直变化）：使用位置：作为 Mathf.Atan2(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、VillagerController.ResolveAllowedPathStepUp()。
float verticalChange = waypoint.y - transform.position.y;
        if (verticalChange <= 0f)
        {
            return allowedStepUp;
        }

        // horizontalOffset（水平偏移量）：使用位置：VillagerController.ResolveAllowedPathStepUp()。
Vector3 horizontalOffset = waypoint - transform.position;
        horizontalOffset.y = 0f;
        // horizontalDistance（水平距离）：使用位置：作为 Mathf.Max(float, float) 的参数。
float horizontalDistance = horizontalOffset.magnitude;
        // routeStepLimit（路线步进限制）：使用位置：作为 Mathf.Min(float, float) 的参数。
float routeStepLimit = Mathf.Max(maximumGroundStepUp, agent.maxPathVerticalStep) + 0.15f;
        // routeSlope（路线坡度）：使用位置：VillagerController.ResolveAllowedPathStepUp()。
float routeSlope = Mathf.Atan2(verticalChange, Mathf.Max(0.001f, horizontalDistance)) * Mathf.Rad2Deg;
        if (routeSlope > Mathf.Min(Mathf.Clamp(maximumGroundSlope, 1f, 89f), Mathf.Clamp(agent.maxPathSlope, 1f, 89f)) + 2f)
        {
            return allowedStepUp;
        }

        return Mathf.Max(allowedStepUp, Mathf.Min(verticalChange + movementCollisionSkin, routeStepLimit));
    }

    /// <summary>CanTraverseCurrentPathGround 的业务处理入口；本方法直接依赖 Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、NavigationCollisionCache.Name(Collider)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveMovementOverlap(bool)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） Mathf.Atan2(float, float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、NavigationCollisionCache.Name(Collider)、PathAgent.TryGetCurrentWaypoint(out Vector3)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="expectedCollider">中文含义：预期碰撞体。使用位置：作为 NavigationCollisionCache.Name(Collider) 的参数、作为 VillagerController.IsEnemyStairBridgeCollider(Collider) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。</param>
    /// <param name="direction">中文含义：方向。使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。</param>
    /// <param name="forwardDistance">中文含义：前向距离。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private bool CanTraverseCurrentPathGround(Collider expectedCollider, Vector3 direction, float forwardDistance)
    {
        if (expectedCollider == null || agent == null)
        {
            return false;
        }

        // 辅助斜桥不代表可见踏面，跨阶只接受真实支撑。
        if (!agent.TryGetCurrentWaypoint(out Vector3 /* 中文含义：路径点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ waypoint))
        {
            return false;
        }

        // routeOffset（路线偏移量）：使用位置：作为 Mathf.Atan2(float, float) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
Vector3 routeOffset = waypoint - transform.position;
        // verticalRouteChange（竖直路线变化）：使用位置：作为 Mathf.Atan2(float, float) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float verticalRouteChange = routeOffset.y;
        routeOffset.y = 0f;
        if (verticalRouteChange <= 0.05f || routeOffset.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        // routeSlope（路线坡度）：使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float routeSlope = Mathf.Atan2(verticalRouteChange, routeOffset.magnitude) * Mathf.Rad2Deg;
        // maximumRouteSlope（最大路线坡度）：使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float maximumRouteSlope = Mathf.Min(Mathf.Clamp(maximumGroundSlope, 1f, 89f), Mathf.Clamp(agent.maxPathSlope, 1f, 89f)) + 2f;
        if (routeSlope > maximumRouteSlope && verticalRouteChange > maximumGroundStepUp)
        {
            // 低精度寻路图会把连续台阶压缩为一个高拐点。若真实地面
            // 能逐级承托角色，允许忽略该拐点造成的“陡墙”假象；若
            // 没有连续踏面，仍按障碍处理，不能穿过真正的墙体。
            return HasContinuousSteppedNavigationGround(waypoint);
        }

        // allowedStepUp（允许的步进向上）：保存 VillagerController.ResolveAllowedPathStepUp() 的返回结果，供当前步骤使用。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float allowedStepUp = ResolveAllowedPathStepUp();
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        direction.Normalize();
        // probeForward（探测前向）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float probeForward = Mathf.Max(0.2f, forwardDistance);
        // origin（原点）：使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。
Vector3 origin = transform.position + direction * probeForward + Vector3.up * (allowedStepUp + groundProbeHeight);
        CombatPerformanceCounters.physicsQueryCount++;
        // hitCount（命中数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, allowedStepUp + groundProbeHeight + maximumGroundDrop, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // i（当前遍历索引）：使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
for (int i = 0; i < hitCount; i++)
        {
            // hit（命中）：使用位置：作为 NavigationCollisionCache.Name(Collider) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
RaycastHit hit = groundHits[i];
            if (hit.collider != expectedCollider || Vector3.Angle(hit.normal, Vector3.up) > Mathf.Clamp(maximumGroundSlope, 1f, 89f))
            {
                continue;
            }

            // verticalChange（竖直变化）：使用位置：VillagerController.CanTraverseCurrentPathGround(Collider, Vector3, float)。
float verticalChange = hit.point.y + groundOffset - transform.position.y;
            if (verticalChange > allowedStepUp || verticalChange < -maximumGroundDrop)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>ShouldIgnoreMovementCollider 的业务处理入口；本方法直接依赖 DoorController.IsInsideOpenPassage(Vector3, float)、DoorController.IsOpenDoorCollider(Collider)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Name(Collider)、Transform.IsChildOf(Transform) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.ResolveMovementOverlap(bool)、VillagerController.TryFindMovementBlocker(Vector3, Vector3, float, Vector3, float, out RaycastHit, bool, bool, bool)；下游（含内部回调体） DoorController.IsInsideOpenPassage(Vector3, float)、DoorController.IsOpenDoorCollider(Collider)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)、NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.Name(Collider)、Transform.IsChildOf(Transform) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">中文含义：碰撞体。使用位置：作为 DoorController.IsOpenDoorCollider(Collider) 的参数、作为 NavigationCollisionCache.Enemy(Collider) 的参数、作为 NavigationCollisionCache.Name(Collider) 的参数 等共 5 处。</param>
    /// <param name="contactPoint">中文含义：接触点。使用位置：作为 DoorController.IsInsideOpenPassage(Vector3, float) 的参数、作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数、VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)。</param>
    /// <param name="strictActionCollision">中文含义：是否严格动作碰撞。使用位置：VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)。</param>
    private bool IgnoreWindowImpactCollider(Collider other)
    {
        var traversal = ArchitectureReadSpecialTraversal;
        return traversal != null && traversal.IgnoreInterruptedSill(other);
    }

    private bool ShouldIgnoreMovementCollider(Collider collider, Vector3 contactPoint, bool strictActionCollision = false)
    {
        if (IgnoreWindowImpactCollider(collider))
        {
            return true;
        }
        if (collider == null || collider.transform == transform || collider.transform.IsChildOf(transform))
        {
            return true;
        }

        // otherEnemy（另一个敌人）：保存 NavigationCollisionCache.Enemy(Collider) 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)。
VillagerController otherEnemy = NavigationCollisionCache.Enemy(collider);
        if (otherEnemy != null && otherEnemy != this)
        {
            return !strictActionCollision;
        }

        if (strictActionCollision)
        {
            return false;
        }

        if (DoorController.IsOpenDoorCollider(collider) && (DoorController.IsInsideOpenPassage(contactPoint, 1.6f) || DoorController.IsInsideOpenPassage(transform.position, 1.6f)))
        {
            return true;
        }

        // isFollowingStairSurface（是否跟随中楼梯表面）：保存 VillagerController.PlantNearHouseThreeTread(Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)。
bool isFollowingStairSurface = PlantNearHouseThreeTread(transform.position);
        if (isFollowingStairSurface && door03TraversalRoute != null && door03TraversalRoute.IsNativeStairTread(collider))
        {
            return true;
        }

        return false;
    }

    /// <summary>IsNavigationObstacle 的业务处理入口；本方法直接依赖 NavigationCollisionCache.IsObstacle(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.HasAttackRecoveryGroundSupport()、VillagerController.HasContinuousNavigationGround(Vector3)、VillagerController.HasContinuousSteppedNavigationGround(Vector3)、VillagerController.IsStandingAttackSurfaceStable()、VillagerController.SnapToGround(float, float, bool) 等共 8 处；下游（含内部回调体） NavigationCollisionCache.IsObstacle(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">中文含义：碰撞体。使用位置：作为 NavigationCollisionCache.IsObstacle(Collider) 的参数。</param>
    private static bool IsNavigationObstacle(Collider collider) => NavigationCollisionCache.IsObstacle(collider);
    // ================================================================
    //  移动诊断
    // ================================================================
    /// <summary>中文含义：是否人群绕过中。使用位置：VillagerController.CollectCombatTimelineSnapshot()、VillagerController.GetCurrentCombatTarget()、VillagerController.IsCrowdBypassDestinationOccupied(Vector3) 等共 9 处。</summary>
    public bool IsCrowdBypassing => crowdBypassActive && Time.time < crowdBypassExpiresAt;

    /// <summary>UpdateCrowdBypass 的业务处理入口；本方法直接依赖 EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、Mathf.Abs(float)、PathAgent.InvalidatePursuitResolutionCache()、PathAgent.ResetPath()、VillagerController.HorizontalDistance(Vector3, Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、Mathf.Abs(float)、PathAgent.InvalidatePursuitResolutionCache()、PathAgent.ResetPath()、VillagerController.HorizontalDistance(Vector3, Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateCrowdBypass()
    {
        if (!crowdBypassActive)
        {
            return;
        }

        // arrived（是否已抵达）：使用位置：VillagerController.UpdateCrowdBypass()。
bool arrived = HorizontalDistance(transform.position, crowdBypassDestination) < 0.3f;
        // interrupted（是否已中断）：使用位置：VillagerController.UpdateCrowdBypass()。
bool interrupted = IsDead || pendingHit || pendingCrawl || pendingStandUp || IsQTEControlled;
        // playerMoved（是否玩家已移动）：使用位置：VillagerController.UpdateCrowdBypass()。
bool playerMoved = Player == null || HorizontalDistance(Player.transform.position, crowdBypassPlayerAnchor) > 1.2f || Mathf.Abs(Player.transform.position.y - crowdBypassPlayerAnchor.y) > 0.4f;
        if (!arrived && !interrupted && !playerMoved && Time.time < crowdBypassExpiresAt)
        {
            return;
        }

        crowdBypassActive = false;
        if (EnemyCombatTimeline.IsRecord(this))
        {
            // argumentValue179（按原顺序缓存的实参当前）：从当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
global::VillagerController argumentValue179 = this;
            // argumentValue180（按原顺序缓存的实参文字“绕行结束”）：从文字“绕行结束”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
string argumentValue180 = "绕行结束";
            // selectedValue178（分支选出的文字“抵达侧向目标，恢复追击玩家”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数、VillagerController.UpdateCrowdBypass()。
string selectedValue178;
            if (arrived)
            {
                selectedValue178 = "抵达侧向目标，恢复追击玩家";
            }
            else
            {
                if (interrupted)
                {
                    selectedValue178 = "角色动作接管";
                }
                else
                {
                    if (playerMoved)
                    {
                        selectedValue178 = "玩家目标变化";
                    }
                    else
                    {
                        selectedValue178 = "短暂绕行到期";
                    }
                }
            }

            EnemyCombatTimeline.Record(argumentValue179, argumentValue180, selectedValue178);
        }

        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.InvalidatePursuitDestination(this);
        }

        if (!interrupted && CurrentState == VillagerState.Chase && agent != null)
        {
            agent.ResetPath();
            agent.InvalidatePursuitResolutionCache();
            RefreshChaseDestination();
        }
    }

    /// <summary>TryStartCrowdBypass 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3)、VillagerController.UpdateBlockedMovement(float, Vector3, RaycastHit, VillagerController, float)；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="blocker">中文含义：阻挡者。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 VillagerController.TryMutualBlockingYield(VillagerController) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。</param>
    private bool TryStartCrowdBypass(VillagerController blocker)
    {
        if (blocker == null || blocker.IsDead || Player == null || CurrentState != VillagerState.Chase || !ShouldChasePlayer || IsCrowdBypassing || Time.time < nextCrowdBypassTime)
        {
            return false;
        }

        nextCrowdBypassTime = Time.time + 0.8f;
        // forward（前向）：使用位置：作为 Vector3.Cross(Vector3, Vector3) 的参数、作为 VillagerController.IsInsideTraversalBottleneck(Vector3) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
Vector3 forward = Player.transform.position - transform.position;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            return false;
        }

        forward.Normalize();
        if (IsInsideTraversalBottleneck(forward) || IsNavigationTraversalProtected || (agent != null && agent.RequiresPrecisePathFollowing))
        {
            if (EnemyCombatTimeline.IsRecord(this))
            {
                // argumentValue183（按原顺序缓存的实参当前）：从当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::VillagerController argumentValue183 = this;
                // argumentValue184（按原顺序缓存的实参文字“绕行受限”）：从文字“绕行受限”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
string argumentValue184 = "绕行受限";
                // argumentValue185（按原顺序缓存的实参文字的返回结果）：从文字的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::CombatDiagnosticFields argumentValue185 = CombatDiagnosticFields.Text("原因", "保留特殊通路队列");
                // argumentValue182（按原顺序缓存的实参文字“前方”）：从文字“前方”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
string argumentValue182 = "前方";
                // selectedValue181（分支选出的阻挡者的诊断敌人数量）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
int selectedValue181;
                if (blocker != null)
                {
                    selectedValue181 = blocker.diagnosticEnemyNumber;
                }
                else
                {
                    selectedValue181 = 0;
                }

                EnemyCombatTimeline.Record(argumentValue183, argumentValue184, argumentValue185, CombatDiagnosticFields.Count(argumentValue182, selectedValue181));
            }

            return false;
        }

        // side（侧边）：保存 Vector3.Cross(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
Vector3 side = Vector3.Cross(Vector3.up, forward);
        // offset（偏移量）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
float offset = Vector3.Dot(transform.position - blocker.transform.position, side);
        // sign（符号）：使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。
int sign;
        if (Mathf.Abs(offset) > 0.05f)
        {
            // selectedValue186（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。
int selectedValue186;
            if (offset > 0f)
            {
                selectedValue186 = 1;
            }
            else
            {
                selectedValue186 = -1;
            }

            sign = (selectedValue186);
        }
        else
        {
            if ((((GetInstanceID() >> 2) ^ (GetInstanceID() >> 5)) & 1) == 0)
            {
                sign = 1;
            }
            else
            {
                sign = -1;
            }
        }

        // attempt（尝试）：使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。
for (int attempt = 0; attempt < 6; attempt++)
        {
            // trialSign（试探符号）：使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。
int trialSign;
            if (attempt % 2 == 0)
            {
                trialSign = sign;
            }
            else
            {
                trialSign = -sign;
            }

            // width（宽度）：使用位置：VillagerController.TryStartCrowdBypass(VillagerController)。
float width = Mathf.Max(0.9f, ResolveMovementHazardRadius() * 2f + 0.2f) + (attempt / 2) * 0.35f;
            // candidate（候选）：使用位置：作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、作为 VillagerController.TryResolveReachableNavigationPoint(Vector3, out Vector3) 的参数。
Vector3 candidate = transform.position + side * trialSign * width + forward * 0.3f;
            if (!TryResolveReachableNavigationPoint(candidate, out Vector3 /* 中文含义：解算后的。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ resolved) || HorizontalDistance(candidate, resolved) > 0.3f || (Mathf.Abs(resolved.y - transform.position.y) > 0.35f && !HasContinuousSteppedNavigationGround(resolved)) || IsCrowdBypassDestinationOccupied(resolved))
            {
                continue;
            }

            if (!CanMoveDirectlyToNavigationPoint(resolved))
            {
                continue;
            }

            BeginTemporaryAvoidance(resolved);
            if (EnemyCombatTimeline.IsRecord(this))
            {
                // argumentValue194（按原顺序缓存的实参当前）：从当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::VillagerController argumentValue194 = this;
                // argumentValue195（按原顺序缓存的实参文字“绕行开始”）：从文字“绕行开始”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
string argumentValue195 = "绕行开始";
                // argumentValue191（按原顺序缓存的实参文字“前方”）：从文字“前方”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
string argumentValue191 = "前方";
                // selectedValue190（分支选出的阻挡者的诊断敌人数量）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
int selectedValue190;
                if (blocker != null)
                {
                    selectedValue190 = blocker.diagnosticEnemyNumber;
                }
                else
                {
                    selectedValue190 = 0;
                }

                // argumentValue196（按原顺序缓存的实参数量的返回结果）：从数量的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::CombatDiagnosticFields argumentValue196 = CombatDiagnosticFields.Count(argumentValue191, selectedValue190);
                // argumentValue193（按原顺序缓存的实参文字“选择”）：从文字“选择”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数。
string argumentValue193 = "选择";
                // selectedValue192（分支选出的文字“右侧”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
string selectedValue192;
                if (trialSign > 0)
                {
                    selectedValue192 = "右侧";
                }
                else
                {
                    selectedValue192 = "左侧";
                }

                EnemyCombatTimeline.Record(argumentValue194, argumentValue195, argumentValue196, CombatDiagnosticFields.Text(argumentValue193, selectedValue192), CombatDiagnosticFields.Position("目标", resolved));
            }

            return true;
        }

        if (TryMutualBlockingYield(blocker))
        {
            return true;
        }

        if (EnemyCombatTimeline.IsRecord(this))
        {
            // argumentValue199（按原顺序缓存的实参当前）：从当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::VillagerController argumentValue199 = this;
            // argumentValue200（按原顺序缓存的实参文字“绕行受限”）：从文字“绕行受限”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
string argumentValue200 = "绕行受限";
            // argumentValue201（按原顺序缓存的实参文字的返回结果）：从文字的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数。
global::CombatDiagnosticFields argumentValue201 = CombatDiagnosticFields.Text("原因", "两侧没有同层可达空间");
            // argumentValue198（按原顺序缓存的实参文字“前方”）：从文字“前方”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
string argumentValue198 = "前方";
            // selectedValue197（分支选出的阻挡者的诊断敌人数量）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.TryStartCrowdBypass(VillagerController)。
int selectedValue197;
            if (blocker != null)
            {
                selectedValue197 = blocker.diagnosticEnemyNumber;
            }
            else
            {
                selectedValue197 = 0;
            }

            EnemyCombatTimeline.Record(argumentValue199, argumentValue200, argumentValue201, CombatDiagnosticFields.Count(argumentValue198, selectedValue197));
        }

        return false;
    }

    /// <summary>BeginTemporaryAvoidance 的业务处理入口；本方法直接依赖 EnemyAIManager.InvalidatePursuitDestination(VillagerController)、Mathf.Max(float, float)、PathAgent.ResetPath()、VillagerController.HorizontalDistance(Vector3, Vector3)、VillagerController.RefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryMutualBlockingYield(VillagerController)、VillagerController.TryStartCrowdBypass(VillagerController)；下游（含内部回调体） EnemyAIManager.InvalidatePursuitDestination(VillagerController)、Mathf.Max(float, float)、PathAgent.ResetPath()、VillagerController.HorizontalDistance(Vector3, Vector3)、VillagerController.RefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.BeginTemporaryAvoidance(Vector3)。</param>
    private void BeginTemporaryAvoidance(Vector3 target)
    {
        crowdBypassActive = true;
        crowdBypassDestination = target;
        crowdBypassPlayerAnchor = Player.transform.position;
        crowdBypassExpiresAt = Time.time + Mathf.Max(1.8f, HorizontalDistance(transform.position, target) / Mathf.Max(.5f, agent.speed) + 1f);
        lastStableAvoidanceDirection = Vector3.zero;
        standingPathDirectionFrame = -1;
        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.InvalidatePursuitDestination(this);
        }

        agent.ResetPath();
        agent.isStopped = false;
        RefreshChaseDestination();
    }

    /// <summary>TryMutualBlockingYield 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Object.GetInstanceID()、Vector3.Cross(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)、VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HorizontalDistance(Vector3, Vector3) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryStartCrowdBypass(VillagerController)；下游（含内部回调体） Mathf.Abs(float)、Object.GetInstanceID()、Vector3.Cross(Vector3, Vector3)、Vector3.ProjectOnPlane(Vector3, Vector3)、VillagerController.BeginTemporaryAvoidance(Vector3)、VillagerController.CanMoveDirectlyToNavigationPoint(Vector3, bool)、VillagerController.HorizontalDistance(Vector3, Vector3) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="blocker">中文含义：阻挡者。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数、VillagerController.TryMutualBlockingYield(VillagerController)。</param>
    private bool TryMutualBlockingYield(VillagerController blocker)
    {
        // 双方都有有效路线也可能相互堵住。固定由离追击目标较远的一方
        // 先腾出真实空间，不创建通路票据，也不取消身体碰撞。
        if (movementBlockedDuration < .6f || blocker.lastMovementBlockerEnemy != this || blocker.movementBlockedDuration < .6f || blocker.IsCrowdBypassing || blocker.CurrentState != VillagerState.Chase || blocker.IsNavigationTraversalProtected)
        {
            return false;
        }

        // gap（间隙）：使用位置：作为 Mathf.Abs(float) 的参数、VillagerController.TryMutualBlockingYield(VillagerController)。
float gap = HorizontalDistance(transform.position, Player.transform.position) - HorizontalDistance(blocker.transform.position, Player.transform.position);
        if (gap < -.1f || Mathf.Abs(gap) <= .1f && GetInstanceID() < blocker.GetInstanceID())
        {
            return false;
        }

        // farAway（远端远离）：使用位置：作为 Vector3.Cross(Vector3, Vector3) 的参数、VillagerController.TryMutualBlockingYield(VillagerController)。
Vector3 farAway = Vector3.ProjectOnPlane(transform.position - blocker.transform.position, Vector3.up).normalized;
        // sideDirection（侧边方向）：保存 Vector3.Cross(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
Vector3 sideDirection = Vector3.Cross(Vector3.up, farAway);
        // distanceLevel（距离级别）：使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
for (int distanceLevel = 0; distanceLevel < 3; distanceLevel++)
        {
            // sideLevel（侧边级别）：使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
for (int sideLevel = 0; sideLevel < 5; sideLevel++)
            {
                // strafe（横移）：使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
float strafe;
                if (sideLevel == 0)
                {
                    strafe = 0;
                }
                else
                {
                    // leftOperand203（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
float leftOperand203 = ((sideLevel + 1) / 2) * .45f;
                    // selectedValue202（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillagerController.TryMutualBlockingYield(VillagerController)。
int selectedValue202;
                    if (sideLevel % 2 == 0)
                    {
                        selectedValue202 = -1;
                    }
                    else
                    {
                        selectedValue202 = 1;
                    }

                    strafe = leftOperand203 * (selectedValue202);
                }

                // candidate（候选）：使用位置：作为 VillagerController.TryResolveSupportedNavigationPosition(Vector3, float, out Vector3) 的参数。
Vector3 candidate = transform.position + farAway * (.65f + distanceLevel * .35f) + sideDirection * strafe;
                if (!TryResolveSupportedNavigationPosition(candidate, transform.position.y, out Vector3 /* 中文含义：支撑。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ support) || Mathf.Abs(support.y - transform.position.y) > .35f || IsCrowdBypassDestinationOccupied(support) || !CanMoveDirectlyToNavigationPoint(support))
                {
                    continue;
                }

                BeginTemporaryAvoidance(support);
                return true;
            }
        }

        return false;
    }

    /// <summary>RecordTimelineAnimationChanges 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.Describe(bool) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.Describe(bool) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RecordTimelineAnimationChanges()
    {
        if (!EnemyCombatTimeline.IsRecord(this) || anim == null)
        {
            return;
        }

        // current（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.RecordTimelineAnimationChanges()。
var current = anim.GetCurrentAnimatorStateInfo(0);
        // transition（是否过渡）：保存 Animator.IsInTransition(int) 的返回结果，供当前步骤使用。使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.RecordTimelineAnimationChanges()。
bool transition = anim.IsInTransition(0);
        // next（下一次）：使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.RecordTimelineAnimationChanges()。
int next;
        if (transition)
        {
            next = anim.GetNextAnimatorStateInfo(0).shortNameHash;
        }
        else
        {
            next = 0;
        }

        if (timelineAnimationInitialized && timelineBaseAnimation == current.shortNameHash && timelineNextAnimation == next && timelineAnimationTransition == transition)
        {
            return;
        }

        // previous（前一次）：使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
int previous = timelineBaseAnimation;
        timelineBaseAnimation = current.shortNameHash;
        timelineNextAnimation = next;
        timelineAnimationTransition = transition;
        timelineAnimationInitialized = true;
        if (EnemyCombatTimeline.IsRecord(this))
        {
            EnemyCombatTimeline.Record(this, "动画状态切换", CombatDiagnosticFields.Count("前动画标识", previous), CombatDiagnosticFields.Count("基础标识", current.shortNameHash), CombatDiagnosticFields.Count("过渡目标标识", next), CombatDiagnosticFields.Text("处于过渡", EnemyCombatTimeline.Describe(transition)), CombatDiagnosticFields.Count("动画进度", current.normalizedTime), CombatDiagnosticFields.Count("动画移动参数", anim.GetFloat(HashMoveSpeed)), CombatDiagnosticFields.Text("角色状态", EnemyCombatTimeline.Describe(CurrentState)));
        }
    }

    /// <summary>BuildCombatTimelineSnapshot 的业务处理入口；本方法直接依赖 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyCombatTimeline.IsRecord(VillagerController)、StringBuilder.StringBuilder(int)、StringBuilder.ToString()、VillagerController.CollectCombatTimelineSnapshot()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeBuildCombatTimelineSnapshot()；下游（含内部回调体） CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyCombatTimeline.IsRecord(VillagerController)、StringBuilder.StringBuilder(int)、StringBuilder.ToString()、VillagerController.CollectCombatTimelineSnapshot()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal string BuildCombatTimelineSnapshot()
    {
        if (!EnemyCombatTimeline.IsRecord(this))
        {
            return string.Empty;
        }

        // text（文字）：使用位置：作为 CombatTimelineSnapshot.AppendValue(StringBuilder) 的参数、VillagerController.BuildCombatTimelineSnapshot()。
var text = new System.Text.StringBuilder(2048);
        CollectCombatTimelineSnapshot().AppendValue(text);
        return text.ToString();
    }

    /// <summary>CollectCombatTimelineSnapshot 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.IsInTransition(int)、CombatTimelineSnapshot.CombatTimelineSnapshot()、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)、EnemyCombatTimeline.Describe&lt;VillagerController.PursuitIngressPhase&gt;(VillagerController.PursuitIngressPhase)、Enum.ToString() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()、VillagerController.BuildCombatTimelineSnapshot()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetFloat(int)、Animator.IsInTransition(int)、CombatTimelineSnapshot.CombatTimelineSnapshot()、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)、EnemyCombatTimeline.Describe&lt;VillagerController.PursuitIngressPhase&gt;(VillagerController.PursuitIngressPhase)、Enum.ToString() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal CombatTimelineSnapshot CollectCombatTimelineSnapshot()
    {
        // reason（原因）：使用位置：VillagerController.CollectCombatTimelineSnapshot()。
string reason;
        if (!gameObject.activeInHierarchy || !enabled || !IsActive)
        {
            reason = "对象或模拟停用";
        }
        else if (!IsRuntimeInitialized)
        {
            reason = "初始化尚未完成";
        }
        else if (IsDead)
        {
            reason = "死亡";
        }
        else if (IsQTEControlled)
        {
            reason = "配对动作接管";
        }
        else if (pendingHit)
        {
            reason = "受击优先级接管";
        }
        else if (pendingCrawl || pendingStandUp)
        {
            reason = "倒地或起身接管";
        }
        else if (IsVillageEntranceCombatLocked)
        {
            reason = "尚未触发入村战斗";
        }
        else if (pursuitIngressPhase == PursuitIngressPhase.WaitingForPassage)
        {
            reason = "门洞通行排队";
        }
        else if (CurrentState == VillagerState.Attack || CurrentState == VillagerState.CrawlAttack)
        {
            reason = "攻击动作进行中";
        }
        else if (combatHoldActive)
        {
            reason = "近战或包围槽待命";
        }
        else if (IsCrowdBypassing)
        {
            reason = "绕过前方同伴";
        }
        else if (agent == null || !agent.enabled)
        {
            reason = "寻路组件未启用";
        }
        else if (agent.isStopped)
        {
            reason = "路径被停止";
        }
        else if (agent.CurrentPathState == PathAgentState.Failed)
        {
            reason = "路径计算失败";
        }
        else if (agent.CurrentPathState == PathAgentState.Calculating)
        {
            reason = "等待路径计算";
        }
        else if (agent.CurrentPathState == PathAgentState.Arrived)
        {
            reason = "抵达路径终点";
        }
        else if (movementBlockedDuration > 0.15f)
        {
            reason = "碰撞或前方同伴阻塞";
        }
        else if (lastStableStandingMoveDirection.sqrMagnitude < 0.001f)
        {
            reason = "尚无本帧路径方向";
        }
        else if (Vector3.Angle(transform.forward, lastStableStandingMoveDirection) > movementFacingStopAngle)
        {
            reason = "转身对齐后才能前进";
        }
        else if (hasStandingRootMotionSpeed && standingRootMotionSpeed < 0.02f)
        {
            reason = "动画速度近零，保留追击最低推进速度";
        }
        else
        {
            reason = "路径追击中";
        }

        // createdValue206（本次新建的快照）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：VillagerController.CollectCombatTimelineSnapshot()。
global::CombatTimelineSnapshot createdValue206 = new CombatTimelineSnapshot();
        createdValue206.state = CurrentState;
        createdValue206.movement = CurrentMovementMode;
        createdValue206.reason = reason;
        createdValue206.position = transform.position;
        createdValue206.joinCombat = combatEngaged;
        createdValue206.hitReaction = pendingHit;
        createdValue206.attackComplete = attackFinished;
        createdValue206.attackCooldown = Mathf.Max(0, lastAttackTime + attackCooldown - Time.time);
        createdValue206.hasPath = agent != null;
        createdValue206.stop = agent == null || agent.isStopped;
        createdValue206.rootSpeed = standingRootMotionSpeed;
        createdValue206.requestSpeed = lastStandingCommandedSpeed;
        if (lastMovementBlockerEnemy != null)
        {
            createdValue206.blockingPathAlly = lastMovementBlockerEnemy.diagnosticEnemyNumber;
        }
        else
        {
            createdValue206.blockingPathAlly = 0;
        }

        createdValue206.blockedTime = movementBlockedDuration;
        createdValue206.doorway = EnemyCombatTimeline.Describe(pursuitIngressPhase);
        if (HouseThreeRegionNavigation.Current != null)
        {
            createdValue206.stair = HouseThreeRegionNavigation.Current.TreadRegion(transform.position).ToString();
        }
        else
        {
            createdValue206.stair = "未配置";
        }

        // snapshot（快照）：使用位置：作为 EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot) 的参数、作为 Mathf.Abs(float) 的参数、作为 PathAgent.TryGetCurrentWaypoint(out Vector3) 的参数 等共 5 处。
var snapshot = createdValue206;
        // player（玩家）：使用位置：VillagerController.CollectCombatTimelineSnapshot()。
var player = Player;
        if (player != null)
        {
            snapshot.hasPlayer = true;
            snapshot.playerPosition = player.transform.position;
            snapshot.distance = HorizontalDistance(snapshot.position, snapshot.playerPosition);
            snapshot.heightDifference = Mathf.Abs(snapshot.position.y - snapshot.playerPosition.y);
        }

        if (agent != null)
        {
            snapshot.path = agent.CurrentPathState;
            snapshot.target = agent.Destination;
            snapshot.hasWaypoint = agent.TryGetCurrentWaypoint(out snapshot.waypoint);
            snapshot.requestVersion = agent.PathRequestVersion;
            snapshot.waypointSequence = agent.CurrentWaypointIndex;
            snapshot.pathDirection = agent.desiredDirection;
            snapshot.chaseDirection = lastStableStandingMoveDirection;
            snapshot.angle = Vector3.Angle(transform.forward, lastStableStandingMoveDirection);
            snapshot.intentDisplacement = diagStandingRequestedDisplacement;
            snapshot.avoidanceDisplacement = diagStandingAvoidanceDisplacement;
            snapshot.speedLimitDisplacement = diagStandingFacingLimitedDisplacement;
        }

        if (anim != null)
        {
            // state（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：VillagerController.CollectCombatTimelineSnapshot()。
var state = anim.GetCurrentAnimatorStateInfo(0);
            snapshot.hasAnimation = true;
            snapshot.animationIdentifier = state.shortNameHash;
            snapshot.animationProgress = state.normalizedTime;
            snapshot.animationSpeed = anim.GetFloat(HashMoveSpeed);
            snapshot.animationTransition = anim.IsInTransition(0);
        }

        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.FillCombatTimelineSnapshot(this, ref snapshot);
        }

        return snapshot;
    }

    /// <summary>中文含义：下一次移动诊断时间。使用位置：VillagerController.CanLogMovementDiagnostic.get、VillagerController.DiagLog(string, string)。</summary>
    private float nextMovementDiagnosticTime;
    /// <summary>中文含义：是否可以日志移动诊断。使用位置：VillagerController.ArchitectureReadCanLogMovementDiagnostic.get、VillagerController.DiagLog(string, string)、VillagerController.DiagLogAnimParams() 等共 10 处。</summary>
    internal bool CanLogMovementDiagnostic => enableMovementDiagnostic && Time.time >= nextMovementDiagnosticTime;

    /// <summary>DiagLog 的业务处理入口；本方法直接依赖 Debug.Log(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ArchitectureInvokeDiagLog(string, string)、VillagerController.DiagLogAnimParams()、VillagerController.DiagLogBT(string, string)、VillagerController.DiagSample()、VillagerController.DiagTrackAnimationState() 等共 8 处；下游（含内部回调体） Debug.Log(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tag">中文含义：标签。使用位置：作为 Debug.Log(object, Object) 的参数。</param>
    /// <param name="msg">中文含义：消息内容。使用位置：作为 Debug.Log(object, Object) 的参数。</param>
    private void DiagLog(string tag, string msg)
    {
        if (!CanLogMovementDiagnostic)
        {
            return;
        }

        nextMovementDiagnosticTime = Time.time + 1f;
        Debug.Log($"[移动诊断][{gameObject.name}][F{Time.frameCount}][T{Time.time:F3}] [{tag}] {msg}", this);
    }

    /// <summary>DiagLogBT 的业务处理入口；本方法直接依赖 VillagerController.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyTaskBase.DiagTaskEnd()、EnemyTaskBase.DiagTaskStart()；下游（含内部回调体） VillagerController.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="phase">中文含义：阶段。使用位置：作为 VillagerController.DiagLog(string, string) 的参数。</param>
    /// <param name="taskName">中文含义：任务名称。使用位置：作为 VillagerController.DiagLog(string, string) 的参数。</param>
    public void DiagLogBT(string phase, string taskName)
    {
        if (!CanLogMovementDiagnostic)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("行为树", $"{phase}: {taskName}, 状态={observableState}, 移动模式={CurrentMovementMode}");
        }
    }

    /// <summary>DiagLogAnimParams 的业务处理入口；本方法直接依赖 Animator.GetBool(string)、Animator.GetFloat(string)、Animator.GetInteger(string)、VillagerController.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DiagTrackAnimationState()；下游（含内部回调体） Animator.GetBool(string)、Animator.GetFloat(string)、Animator.GetInteger(string)、VillagerController.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DiagLogAnimParams()
    {
        if (!CanLogMovementDiagnostic || anim == null)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("动画参数", $"MoveSpeed={anim.GetFloat("MoveSpeed"):F3}, " + $"MoveX={anim.GetFloat("MoveX"):F3}, " + $"MoveY={anim.GetFloat("MoveY"):F3}, " + $"HIT={anim.GetBool("HIT")}, " + $"IsCrawl={anim.GetBool("IsCrawl")}, " + $"CanTripOver={anim.GetBool("CanTripOver")}, " + $"HealthRatio={anim.GetFloat("HealthRatio"):F3}, " + $"ATK={anim.GetBool("ATK")}, " + $"HitDir={anim.GetInteger("HitDir")}");
        }
    }

    /// <summary>DiagBuildSnapshot 的业务处理入口；本方法直接依赖 Animator.GetBool(string)、Animator.GetFloat(string)、HouseThreeRegionNavigation.TreadRegion(Vector3)、object.ReferenceEquals(object, object)、StringBuilder.Append(string)、StringBuilder.StringBuilder(int)、StringBuilder.ToString() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DiagSample()；下游（含内部回调体） Animator.GetBool(string)、Animator.GetFloat(string)、HouseThreeRegionNavigation.TreadRegion(Vector3)、object.ReferenceEquals(object, object)、StringBuilder.Append(string)、StringBuilder.StringBuilder(int)、StringBuilder.ToString() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private string DiagBuildSnapshot()
    {
        // sb（文本构建器）：使用位置：VillagerController.DiagBuildSnapshot()。
System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        // 位置与位移
        // pos（位置）：使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
Vector3 pos = transform.position;
        // disp（位移）：使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
Vector3 disp;
        if (diagFirstSample)
        {
            disp = Vector3.zero;
        }
        else
        {
            disp = pos - diagLastSamplePos;
        }

        sb.Append($"位置=({pos.x:F3},{pos.y:F3},{pos.z:F3}), ");
        sb.Append($"位移=({disp.x:F4},{disp.y:F4},{disp.z:F4}), 幅度={disp.magnitude:F4}, ");
        // 玩家信息
        // player（玩家）：使用位置：VillagerController.DiagBuildSnapshot()。
LeonController player = Player;
        if (player != null)
        {
            // ppos（玩家位置）：使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
Vector3 ppos = player.transform.position;
            // dist（距离）：使用位置：作为 StringBuilder.Append(string) 的参数。
float dist = PlayerDistance;
            // dirToPlayer（方向目标玩家）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、VillagerController.DiagBuildSnapshot()。
Vector3 dirToPlayer = ppos - pos;
            dirToPlayer.y = 0f;
            // facingAngle（朝向角度）：使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
float facingAngle;
            if (dirToPlayer.sqrMagnitude > 0.001f)
            {
                facingAngle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
            }
            else
            {
                facingAngle = 0f;
            }

            sb.Append($"玩家=({ppos.x:F3},{ppos.y:F3},{ppos.z:F3}), 距离={dist:F2}, 朝向角={facingAngle:F1}, ");
        }
        else
        {
            sb.Append("玩家=null, ");
        }

        // 状态与标记
        sb.Append($"状态={observableState}, 移动模式={CurrentMovementMode}, ");
        sb.Append($"pendingHit={pendingHit}, pendingCrawl={pendingCrawl}, ");
        sb.Append($"pendingStandUp={pendingStandUp}, pendingTripOver={pendingTripOver}, ");
        sb.Append($"permanentCrawl={permanentCrawl}, ");
        // 动画参数
        if (anim != null)
        {
            sb.Append($"MoveSpeed={anim.GetFloat("MoveSpeed"):F3}, ");
            sb.Append($"HIT={anim.GetBool("HIT")}, ");
            sb.Append($"IsCrawl={anim.GetBool("IsCrawl")}, ");
            sb.Append($"CanTripOver={anim.GetBool("CanTripOver")}, ");
            sb.Append($"ATK={anim.GetBool("ATK")}, ");
        }

        // 寻路
        if (agent != null)
        {
            sb.Append($"寻路状态={agent.CurrentPathState}, ");
            sb.Append($"isStopped={agent.isStopped}, ");
            sb.Append($"hasDestination={agent.HasDestination}, ");
            sb.Append($"hasPath={agent.hasPath}, ");
            sb.Append($"desiredDir=({agent.desiredDirection.x:F3},{agent.desiredDirection.y:F3},{agent.desiredDirection.z:F3}), ");
            sb.Append($"desiredVel=({agent.desiredVelocity.x:F3},{agent.desiredVelocity.y:F3},{agent.desiredVelocity.z:F3}), ");
            sb.Append($"agentSpeed={agent.speed:F3}, ");
            sb.Append($"remainingDist={agent.remainingDistance:F2}");
        }
        else
        {
            sb.Append("agent=null");
        }

        // 同时记录门洞动作与实际踏面区域，便于定位追击停滞。
        sb.Append($", 战斗={combatEngaged}, 完全警觉={fullyAwareOfPlayer}, ");
        // callReceiver212（调用前缓存的文本构建器）：从文本构建器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.DiagBuildSnapshot()。
global::System.Text.StringBuilder callReceiver212 = sb;
        // selectedValue211（分支选出的追击入口门的名称）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
string selectedValue211;
        if (pursuitIngressDoor != null)
        {
            selectedValue211 = pursuitIngressDoor.name;
        }
        else
        {
            selectedValue211 = "无";
        }

        callReceiver212.Append($"门洞={(selectedValue211)}, ");
        sb.Append($"门洞阶段={pursuitIngressPhase}, 门洞穿越={pursuitIngressCrossing}, 室内转折完成={door03InteriorTurnPassed}, ");
        // callReceiver215（调用前缓存的文本构建器）：从文本构建器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillagerController.DiagBuildSnapshot()。
global::System.Text.StringBuilder callReceiver215 = sb;
        // receiverValue213（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.DiagBuildSnapshot()。
global::HouseThreeRegionNavigation receiverValue213 = HouseThreeRegionNavigation.Current;
        // optionalValue214（条件访问得到的踏面区域的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.DiagBuildSnapshot()。
global::VillageTraversalRegion? optionalValue214;
        if (!global::System.Object.ReferenceEquals(receiverValue213, null))
        {
            optionalValue214 = receiverValue213.TreadRegion(transform.position);
        }
        else
        {
            optionalValue214 = default;
        }

        callReceiver215.Append($"踏面区域={optionalValue214}");
        return sb.ToString();
    }

    /// <summary>DiagTrackAnimationState 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorClipInfo(int)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorClipInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Array.ConvertAll&lt;AnimatorClipInfo, string&gt;(AnimatorClipInfo[], Converter&lt;AnimatorClipInfo, string&gt;)、string.Join(string, params string[]) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DiagUpdate()；下游（含内部回调体） Animator.GetCurrentAnimatorClipInfo(int)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorClipInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Array.ConvertAll&lt;AnimatorClipInfo, string&gt;(AnimatorClipInfo[], Converter&lt;AnimatorClipInfo, string&gt;)、string.Join(string, params string[]) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DiagTrackAnimationState()
    {
        if (!CanLogMovementDiagnostic || anim == null)
        {
            return;
        }

        if (!diagPrevAnimInit)
        {
            // i（当前遍历索引）：使用位置：VillagerController.DiagTrackAnimationState()。
for (int i = 0; i < diagPrevAnimHashes.Length; i++)
            {
                diagPrevAnimHashes[i] = -1;
            }

            diagPrevAnimInit = true;
        }

        // layerCount（图层数量）：使用位置：VillagerController.DiagTrackAnimationState()。
int layerCount = anim.layerCount;
        // layer（图层）：使用位置：作为 Animator.GetCurrentAnimatorClipInfo(int) 的参数、作为 Animator.GetCurrentAnimatorStateInfo(int) 的参数、作为 Animator.GetNextAnimatorClipInfo(int) 的参数 等共 7 处。
for (int layer = 0; layer < layerCount && layer < diagPrevAnimHashes.Length; layer++)
        {
            if (!CanLogMovementDiagnostic)
            {
                break;
            }

            // stateInfo（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagTrackAnimationState()。
AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.fullPathHash == diagPrevAnimHashes[layer])
            {
                continue;
            }

            // 状态变化
            // clipNames（动画片段名称列表）：使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagTrackAnimationState()。
string clipNames = "";
            // clips（动画片段集合）：保存 Animator.GetCurrentAnimatorClipInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 Array.ConvertAll<AnimatorClipInfo, string>(AnimatorClipInfo[], Converter<AnimatorClipInfo, string>) 的参数、VillagerController.DiagTrackAnimationState()。
var clips = anim.GetCurrentAnimatorClipInfo(layer);
            if (clips.Length > 0)
            {
                clipNames = string.Join(", ", System.Array.ConvertAll(clips, /* 中文含义：当前动画控制器动画片段信息。 */ c =>
                {
                    if (c.clip != null)
                    {
                        return c.clip.name;
                    }
                    else
                    {
                        return "null";
                    }
                }));
            }

            // inTransition（是否内部过渡）：保存 Animator.IsInTransition(int) 的返回结果，供当前步骤使用。使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagTrackAnimationState()。
bool inTransition = anim.IsInTransition(layer);
            // nextStateStr（下一次状态字符串）：使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagTrackAnimationState()。
string nextStateStr = "";
            if (inTransition)
            {
                // nextInfo（下一次信息）：保存 Animator.GetNextAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：VillagerController.DiagTrackAnimationState()。
AnimatorStateInfo nextInfo = anim.GetNextAnimatorStateInfo(layer);
                // nextClips（下一次动画片段集合）：使用位置：VillagerController.DiagTrackAnimationState()。
string nextClips = "";
                // nextClipArr（下一次动画片段数组）：保存 Animator.GetNextAnimatorClipInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 Array.ConvertAll<AnimatorClipInfo, string>(AnimatorClipInfo[], Converter<AnimatorClipInfo, string>) 的参数、VillagerController.DiagTrackAnimationState()。
var nextClipArr = anim.GetNextAnimatorClipInfo(layer);
                if (nextClipArr.Length > 0)
                {
                    nextClips = string.Join(", ", System.Array.ConvertAll(nextClipArr, /* 中文含义：当前动画控制器动画片段信息。 */ c =>
                    {
                        if (c.clip != null)
                        {
                            return c.clip.name;
                        }
                        else
                        {
                            return "null";
                        }
                    }));
                }

                nextStateStr = $", 下一状态Hash={nextInfo.fullPathHash}, 下一剪辑={nextClips}";
            }

            if (CanLogMovementDiagnostic)
            {
                DiagLog("动画状态", $"层{layer} 状态Hash={stateInfo.fullPathHash} " + $"剪辑=[{clipNames}] 标准化时间={stateInfo.normalizedTime:F3}" + $" 过渡中={inTransition}{nextStateStr}");
            }

            DiagLogAnimParams();
            diagPrevAnimHashes[layer] = stateInfo.fullPathHash;
        }
    }

    /// <summary>DiagUpdate 的业务处理入口；本方法直接依赖 VillagerController.DiagSample()、VillagerController.DiagTrackAnimationState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Update()；下游（含内部回调体） VillagerController.DiagSample()、VillagerController.DiagTrackAnimationState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DiagUpdate()
    {
        if (!CanLogMovementDiagnostic)
        {
            return;
        }

        DiagTrackAnimationState();
        diagSampleTimer += Time.deltaTime;
        if (diagSampleTimer >= 1f)
        {
            diagSampleTimer = 0f;
            DiagSample();
        }
    }

    /// <summary>DiagSample 的业务处理入口；本方法直接依赖 Animator.GetFloat(string)、VillagerController.DiagBuildSnapshot()、VillagerController.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.DiagUpdate()；下游（含内部回调体） Animator.GetFloat(string)、VillagerController.DiagBuildSnapshot()、VillagerController.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DiagSample()
    {
        if (!CanLogMovementDiagnostic)
        {
            return;
        }

        // currentPos（当前位置）：使用位置：VillagerController.DiagSample()。
Vector3 currentPos = transform.position;
        // displacement（位移）：使用位置：VillagerController.DiagSample()。
Vector3 displacement;
        if (diagFirstSample)
        {
            displacement = Vector3.zero;
        }
        else
        {
            displacement = currentPos - diagLastSamplePos;
        }

        // dispMag（位移弹匣）：使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagSample()。
float dispMag = displacement.magnitude;
        // moveSpeed（移动速度）：使用位置：作为 VillagerController.DiagLog(string, string) 的参数、VillagerController.DiagSample()。
float moveSpeed;
        if (anim != null)
        {
            moveSpeed = anim.GetFloat("MoveSpeed");
        }
        else
        {
            moveSpeed = 0f;
        }

        // 输出本轮采样的快照
        if (CanLogMovementDiagnostic)
        {
            DiagLog("采样", DiagBuildSnapshot());
        }

        // 输出本0.2s窗口内OnAnimatorMove缓冲
        if (diagOnAnimMoveCalled)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("OnAnimatorMove", $"模式={diagOnAnimMoveMode}, " + $"deltaPos=({diagOnAnimMoveDeltaPos.x:F4},{diagOnAnimMoveDeltaPos.y:F4},{diagOnAnimMoveDeltaPos.z:F4}), " + $"前=({diagOnAnimMovePrePos.x:F3},{diagOnAnimMovePrePos.y:F3},{diagOnAnimMovePrePos.z:F3}), " + $"后=({diagOnAnimMovePostPos.x:F3},{diagOnAnimMovePostPos.y:F3},{diagOnAnimMovePostPos.z:F3})");
            }

            diagOnAnimMoveCalled = false;
        }

        // 输出本0.2s窗口内UpdateCodeDrivenMovement缓冲
        if (diagUpdateCDTag != null)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("UpdateCodeDriven", $"{diagUpdateCDTag}: " + $"前=({diagUpdateCDPrePos.x:F3},{diagUpdateCDPrePos.y:F3},{diagUpdateCDPrePos.z:F3}), " + $"后=({diagUpdateCDPostPos.x:F3},{diagUpdateCDPostPos.y:F3},{diagUpdateCDPostPos.z:F3}), " + $"dir=({diagUpdateCDDesiredDir.x:F3},{diagUpdateCDDesiredDir.y:F3},{diagUpdateCDDesiredDir.z:F3}), " + $"speed={diagUpdateCDSpeed:F3}");
            }

            diagUpdateCDTag = null;
        }

        // 高优先级诊断：动画有速度但实际位移极小
        if (!diagFirstSample && moveSpeed > 0.1f && dispMag < 0.01f)
        {
            if (CanLogMovementDiagnostic)
            {
                DiagLog("⚠高优先级", $"MoveSpeed={moveSpeed:F3} 但位移幅度={dispMag:F4} < 0.01！疑似原地跑步！\n" + DiagBuildSnapshot());
            }
        }

        diagLastSamplePos = currentPos;
        diagFirstSample = false;
    }

    // 阶段二组装端口：只读上下文与原运动执行方法，下一阶段再迁移运动职责。
    /// <summary>中文含义：架构读取是否可以日志移动诊断。使用位置：EnemyActionCoordinator.CanLogMovementDiagnostic.get、EnemyCombatModule.CanLogMovementDiagnostic.get。</summary>
    internal bool ArchitectureReadCanLogMovementDiagnostic => CanLogMovementDiagnostic;
    /// <summary>中文含义：架构读取当前移动模式。使用位置：EnemyActionCoordinator.CurrentMovementMode.get、EnemyAnimationAdapter.CurrentMovementMode.get、EnemyCombatModule.CurrentMovementMode.get 等共 4 处。</summary>
    internal EnemyMovementMode ArchitectureReadCurrentMovementMode => CurrentMovementMode;
    /// <summary>中文含义：架构读取当前状态。使用位置：EnemyActionCoordinator.CurrentState.get、EnemyAnimationAdapter.CurrentState.get、EnemyCombatModule.CurrentState.get 等共 4 处。</summary>
    internal VillagerState ArchitectureReadCurrentState => CurrentState;
    /// <summary>中文含义：架构只读哈希编号攻击。使用位置：EnemyActionCoordinator.HashATK.get、EnemyAnimationAdapter.HashATK.get、EnemyCombatModule.HashATK.get。</summary>
    internal static int ArchitectureReadHashATK => HashATK;
    /// <summary>中文含义：架构只读哈希编号攻击01状态。使用位置：EnemyCombatModule.HashAttack01State.get。</summary>
    internal static int ArchitectureReadHashAttack01State => HashAttack01State;
    /// <summary>中文含义：架构只读哈希编号攻击02状态。使用位置：EnemyCombatModule.HashAttack02State.get。</summary>
    internal static int ArchitectureReadHashAttack02State => HashAttack02State;
    /// <summary>中文含义：架构只读哈希编号攻击03状态。使用位置：EnemyCombatModule.HashAttack03State.get。</summary>
    internal static int ArchitectureReadHashAttack03State => HashAttack03State;
    /// <summary>中文含义：架构只读哈希编号攻击索引。使用位置：EnemyCombatModule.HashAttackIndex.get。</summary>
    internal static int ArchitectureReadHashAttackIndex => HashAttackIndex;
    /// <summary>中文含义：架构只读哈希编号是否可以绊倒。使用位置：EnemyCombatModule.HashCanTripOver.get。</summary>
    internal static int ArchitectureReadHashCanTripOver => HashCanTripOver;
    /// <summary>中文含义：架构只读哈希编号命中。使用位置：EnemyActionCoordinator.HashHIT.get、EnemyAnimationAdapter.HashHIT.get、EnemyCombatModule.HashHIT.get。</summary>
    internal static int ArchitectureReadHashHIT => HashHIT;
    /// <summary>中文含义：架构只读哈希编号命中方向。使用位置：EnemyCombatModule.HashHitDir.get。</summary>
    internal static int ArchitectureReadHashHitDir => HashHitDir;
    /// <summary>中文含义：架构只读哈希编号命中触发器。使用位置：EnemyAnimationAdapter.HashHitTrigger.get、EnemyCombatModule.HashHitTrigger.get。</summary>
    internal static int ArchitectureReadHashHitTrigger => HashHitTrigger;
    /// <summary>中文含义：架构只读哈希编号是否爬行。使用位置：EnemyCombatModule.HashIsCrawl.get。</summary>
    internal static int ArchitectureReadHashIsCrawl => HashIsCrawl;
    /// <summary>中文含义：架构只读哈希编号移动速度。使用位置：EnemyAnimationAdapter.HashMoveSpeed.get、EnemyCombatModule.HashMoveSpeed.get。</summary>
    internal static int ArchitectureReadHashMoveSpeed => HashMoveSpeed;
    /// <summary>中文含义：架构只读哈希编号搜索模式。使用位置：EnemyActionCoordinator.HashSearchMode.get、EnemyCombatModule.HashSearchMode.get。</summary>
    internal static int ArchitectureReadHashSearchMode => HashSearchMode;
    /// <summary>中文含义：架构只读哈希编号硬直方向。使用位置：EnemyCombatModule.HashStaggerDirection.get。</summary>
    internal static int ArchitectureReadHashStaggerDirection => HashStaggerDirection;
    /// <summary>中文含义：架构只读哈希编号硬直级别。使用位置：EnemyCombatModule.HashStaggerLevel.get。</summary>
    internal static int ArchitectureReadHashStaggerLevel => HashStaggerLevel;
    /// <summary>中文含义：架构只读哈希编号硬直触发器。使用位置：EnemyAnimationAdapter.HashStaggerTrigger.get、EnemyCombatModule.HashStaggerTrigger.get。</summary>
    internal static int ArchitectureReadHashStaggerTrigger => HashStaggerTrigger;
    /// <summary>中文含义：架构只读哈希编号转向方向。使用位置：EnemyActionCoordinator.HashTurnDir.get、EnemyCombatModule.HashTurnDir.get。</summary>
    internal static int ArchitectureReadHashTurnDir => HashTurnDir;
    /// <summary>中文含义：架构读取寻路代理。使用位置：EnemyActionCoordinator.agent.get、EnemyAnimationAdapter.agent.get、EnemyCombatModule.agent.get 等共 4 处。</summary>
    internal PathAgent ArchitectureReadAgent => agent;
    /// <summary>中文含义：架构读取动画。使用位置：EnemyActionCoordinator.anim.get、EnemyAnimationAdapter.anim.get、EnemyCombatModule.anim.get。</summary>
    internal Animator ArchitectureReadAnim => anim;
    /// <summary>中文含义：架构读取攻击范围极小容差。使用位置：EnemyCombatModule.attackRangeEpsilon.get。</summary>
    internal static float ArchitectureReadAttackRangeEpsilon => attackRangeEpsilon;
    /// <summary>中文含义：架构读取行为树。使用位置：EnemyCombatModule.behaviorTree.get。</summary>
    internal BehaviorTree ArchitectureReadBehaviorTree => behaviorTree;
    /// <summary>中文含义：架构读取缓存玩家。使用位置：EnemyCombatModule.cachedPlayer.get。</summary>
    internal LeonController ArchitectureReadCachedPlayer => cachedPlayer;
    /// <summary>中文含义：架构读取电锯桥接。使用位置：EnemyActionCoordinator.chainsawBridge.get、EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)、EnemyCombatModule.chainsawBridge.get 等共 4 处。</summary>
    internal ChainsawManAnimationBridge ArchitectureReadChainsawBridge => chainsawBridge;
    /// <summary>中文含义：架构读取碰撞体控制器。使用位置：EnemyActionCoordinator.colliderCtrl.get、EnemyCombatModule.colliderCtrl.get。</summary>
    internal EnemyColliderController ArchitectureReadColliderCtrl => colliderCtrl;
    /// <summary>中文含义：架构读取上下文。使用位置：EnemyActionCoordinator.ctx.get、EnemyAnimationAdapter.ctx.get、EnemyCombatModule.ctx.get。</summary>
    internal EnemyContext ArchitectureReadCtx => ctx;
    /// <summary>中文含义：架构读取特殊通行。使用位置：EnemyActionCoordinator.specialTraversal.get、EnemyCombatModule.specialTraversal.get、EnemyFootAction.AllowGroundContact.get。</summary>
    internal EnemyWindowTraversal ArchitectureReadSpecialTraversal => specialTraversal;

    /// <summary>ArchitectureInvokeBecomeFullyAwareOfPlayer 的业务处理入口；本方法直接依赖 VillagerController.BecomeFullyAwareOfPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.BecomeFullyAwareOfPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeBecomeFullyAwareOfPlayer()
    {
        BecomeFullyAwareOfPlayer();
    }

    /// <summary>ArchitectureInvokeBeginChase 的业务处理入口；本方法直接依赖 VillagerController.BeginChase(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.BeginChase(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="notifyCombatEngaged">中文含义：是否通知战斗已接战。使用位置：作为 VillagerController.BeginChase(bool) 的参数。</param>
    internal void ArchitectureInvokeBeginChase(bool notifyCombatEngaged = true)
    {
        BeginChase(notifyCombatEngaged);
    }

    /// <summary>ArchitectureInvokeBeginDeath 的业务处理入口；本方法直接依赖 VillagerController.BeginDeath()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.BeginDeath()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeBeginDeath()
    {
        BeginDeath();
    }

    /// <summary>ArchitectureInvokeCanSeePlayer 的业务处理入口；本方法直接依赖 VillagerController.CanSeePlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.CanSeePlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeCanSeePlayer()
    {
        return CanSeePlayer();
    }

    /// <summary>ArchitectureInvokeDiagLog 的业务处理入口；本方法直接依赖 VillagerController.DiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.DiagLog(string, string)、EnemyCombatModule.DiagLog(string, string)；下游（含内部回调体） VillagerController.DiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tag">中文含义：标签。使用位置：作为 VillagerController.DiagLog(string, string) 的参数。</param>
    /// <param name="msg">中文含义：消息内容。使用位置：作为 VillagerController.DiagLog(string, string) 的参数。</param>
    internal void ArchitectureInvokeDiagLog(string tag, string msg)
    {
        DiagLog(tag, msg);
    }

    /// <summary>ArchitectureInvokeFacePlayerImmediately 的业务处理入口；本方法直接依赖 VillagerController.FacePlayerImmediately()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.FacePlayerImmediately()；下游（含内部回调体） VillagerController.FacePlayerImmediately()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeFacePlayerImmediately()
    {
        FacePlayerImmediately();
    }

    /// <summary>ArchitectureInvokeHasAttackRecoveryGroundSupport 的业务处理入口；本方法直接依赖 VillagerController.HasAttackRecoveryGroundSupport()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HasAttackRecoveryGroundSupport()；下游（含内部回调体） VillagerController.HasAttackRecoveryGroundSupport()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeHasAttackRecoveryGroundSupport()
    {
        return HasAttackRecoveryGroundSupport();
    }

    /// <summary>ArchitectureInvokeHasClearLineToPlayer 的业务处理入口；本方法直接依赖 VillagerController.HasClearLineToPlayer(bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.HasClearLineToPlayer(bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requireSightAngle">中文含义：是否需要视野角度。使用位置：作为 VillagerController.HasClearLineToPlayer(bool, float) 的参数。</param>
    /// <param name="maximumDistance">中文含义：最大距离。使用位置：作为 VillagerController.HasClearLineToPlayer(bool, float) 的参数。</param>
    internal bool ArchitectureInvokeHasClearLineToPlayer(bool requireSightAngle, float maximumDistance)
    {
        return HasClearLineToPlayer(requireSightAngle, maximumDistance);
    }

    /// <summary>ArchitectureInvokeIsStandingAttackSurfaceStable 的业务处理入口；本方法直接依赖 VillagerController.IsStandingAttackSurfaceStable()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsStandingAttackSurfaceStable()；下游（含内部回调体） VillagerController.IsStandingAttackSurfaceStable()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeIsStandingAttackSurfaceStable()
    {
        return IsStandingAttackSurfaceStable();
    }

    /// <summary>ArchitectureInvokePlayCrawlIdleImmediately 的业务处理入口；本方法直接依赖 VillagerController.PlayCrawlIdleImmediately()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.PlayCrawlIdleImmediately()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokePlayCrawlIdleImmediately()
    {
        PlayCrawlIdleImmediately();
    }

    /// <summary>ArchitectureInvokePlayVoice 的业务处理入口；本方法直接依赖 VillagerController.PlayVoice(ZombieVoiceType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.PlayVoice(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">中文含义：语音类型。使用位置：作为 VillagerController.PlayVoice(ZombieVoiceType) 的参数。</param>
    internal void ArchitectureInvokePlayVoice(ZombieVoiceType voiceType)
    {
        PlayVoice(voiceType);
    }

    /// <summary>ArchitectureInvokeRefreshChaseDestination 的业务处理入口；本方法直接依赖 VillagerController.RefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.RefreshChaseDestination()、EnemyPerceptionModule.RefreshChaseDestination()；下游（含内部回调体） VillagerController.RefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeRefreshChaseDestination()
    {
        return RefreshChaseDestination();
    }

    /// <summary>ArchitectureInvokeRequiresNavigationTraversalToPlayer 的业务处理入口；本方法直接依赖 VillagerController.RequiresNavigationTraversalToPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.RequiresNavigationTraversalToPlayer()；下游（含内部回调体） VillagerController.RequiresNavigationTraversalToPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeRequiresNavigationTraversalToPlayer()
    {
        return RequiresNavigationTraversalToPlayer();
    }

    /// <summary>ArchitectureInvokeSetMovementMode 的业务处理入口；本方法直接依赖 VillagerController.SetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyCombatModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyPerceptionModule.SetMovementMode(EnemyMovementMode, string, int)；下游（含内部回调体） VillagerController.SetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementMode">中文含义：移动模式。使用位置：作为 VillagerController.SetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 VillagerController.SetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 VillagerController.SetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    internal void ArchitectureInvokeSetMovementMode(EnemyMovementMode movementMode, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        SetMovementMode(movementMode, source, line);
    }

    /// <summary>ArchitectureInvokeSetObservableState 的业务处理入口；本方法直接依赖 VillagerController.SetObservableState(VillagerState, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.SetObservableState(VillagerState, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前敌人状态。使用位置：作为 VillagerController.SetObservableState(VillagerState, string, int) 的参数。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 VillagerController.SetObservableState(VillagerState, string, int) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 VillagerController.SetObservableState(VillagerState, string, int) 的参数。</param>
    internal void ArchitectureInvokeSetObservableState(VillagerState state, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        SetObservableState(state, source, line);
    }

    /// <summary>ArchitectureInvokeSnapToGround 的业务处理入口；本方法直接依赖 VillagerController.SnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SnapToGround()、EnemyCombatModule.SnapToGround()；下游（含内部回调体） VillagerController.SnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeSnapToGround()
    {
        SnapToGround();
    }

    /// <summary>ArchitectureInvokeSnapToGround 的业务处理入口；本方法直接依赖 VillagerController.SnapToGround(float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SnapToGround(float, float, bool)、EnemyCombatModule.SnapToGround(float, float, bool)；下游（含内部回调体） VillagerController.SnapToGround(float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probeHeight">中文含义：探测高度。使用位置：作为 VillagerController.SnapToGround(float, float, bool) 的参数。</param>
    /// <param name="probeDistance">中文含义：探测距离。使用位置：作为 VillagerController.SnapToGround(float, float, bool) 的参数。</param>
    /// <param name="allowLargeDrop">中文含义：是否允许较大掉落。使用位置：作为 VillagerController.SnapToGround(float, float, bool) 的参数。</param>
    internal void ArchitectureInvokeSnapToGround(float probeHeight, float probeDistance, bool allowLargeDrop)
    {
        SnapToGround(probeHeight, probeDistance, allowLargeDrop);
    }

    /// <summary>ArchitectureInvokeStopAndClearMovement 的业务处理入口；本方法直接依赖 VillagerController.StopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.StopAndClearMovement()、EnemyCombatModule.StopAndClearMovement()、EnemyPerceptionModule.StopAndClearMovement()；下游（含内部回调体） VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeStopAndClearMovement()
    {
        StopAndClearMovement();
    }

    /// <summary>ArchitectureInvokeUpdateHealthAnimationParameter 的业务处理入口；本方法直接依赖 VillagerController.UpdateHealthAnimationParameter()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.UpdateHealthAnimationParameter()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeUpdateHealthAnimationParameter()
    {
        UpdateHealthAnimationParameter();
    }

    /// <summary>中文含义：架构读取是否村庄入口战斗锁定。使用位置：EnemyActionCoordinator.IsVillageEntranceCombatLocked.get、EnemyPerceptionModule.IsVillageEntranceCombatLocked.get。</summary>
    internal bool ArchitectureReadIsVillageEntranceCombatLocked => IsVillageEntranceCombatLocked;

    /// <summary>ArchitectureInvokeFacePosition 的业务处理入口；本方法直接依赖 VillagerController.FacePosition(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.FacePosition(Vector3, float)、EnemyPerceptionModule.FacePosition(Vector3, float)；下游（含内部回调体） VillagerController.FacePosition(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">中文含义：目标位置。使用位置：作为 VillagerController.FacePosition(Vector3, float) 的参数。</param>
    /// <param name="turnSpeed">中文含义：转向速度。使用位置：作为 VillagerController.FacePosition(Vector3, float) 的参数。</param>
    internal void ArchitectureInvokeFacePosition(Vector3 targetPosition, float turnSpeed)
    {
        FacePosition(targetPosition, turnSpeed);
    }

    /// <summary>中文含义：架构只读哈希编号待机混合。使用位置：EnemyActionCoordinator.HashIdleBlend.get。</summary>
    internal static int ArchitectureReadHashIdleBlend => HashIdleBlend;
    /// <summary>中文含义：架构读取配对动作参与者。使用位置：CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、CharacterMotionExecutor.Valid(MotionControlToken)、EnemyActionCoordinator.qteParticipant.get。</summary>
    internal VillagerQTEParticipant ArchitectureReadQteParticipant => qteParticipant;

    /// <summary>ArchitectureInvokeApplyDeathAnimatorParameters 的业务处理入口；本方法直接依赖 VillagerController.ApplyDeathAnimatorParameters()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ApplyDeathAnimatorParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeApplyDeathAnimatorParameters()
    {
        ApplyDeathAnimatorParameters();
    }

    /// <summary>ArchitectureInvokeBuildCombatTimelineSnapshot 的业务处理入口；本方法直接依赖 VillagerController.BuildCombatTimelineSnapshot()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BuildCombatTimelineSnapshot()；下游（含内部回调体） VillagerController.BuildCombatTimelineSnapshot()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal string ArchitectureInvokeBuildCombatTimelineSnapshot()
    {
        return BuildCombatTimelineSnapshot();
    }

    /// <summary>ArchitectureInvokeClearCombatEngagement 的业务处理入口；本方法直接依赖 VillagerController.ClearCombatEngagement(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ClearCombatEngagement(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveInvestigation">中文含义：是否保留调查。使用位置：作为 VillagerController.ClearCombatEngagement(bool) 的参数。</param>
    internal void ArchitectureInvokeClearCombatEngagement(bool preserveInvestigation)
    {
        ClearCombatEngagement(preserveInvestigation);
    }

    /// <summary>ArchitectureInvokeGetPatrolTurnDirection 的业务处理入口；本方法直接依赖 VillagerController.GetPatrolTurnDirection(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.GetPatrolTurnDirection(float)；下游（含内部回调体） VillagerController.GetPatrolTurnDirection(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="signedTurnAngle">中文含义：有符号转向角度。使用位置：作为 VillagerController.GetPatrolTurnDirection(float) 的参数。</param>
    internal static int ArchitectureInvokeGetPatrolTurnDirection(float signedTurnAngle)
    {
        return GetPatrolTurnDirection(signedTurnAngle);
    }

    /// <summary>ArchitectureInvokeStopPathMovementWithAnimationBlend 的业务处理入口；本方法直接依赖 VillagerController.StopPathMovementWithAnimationBlend()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.StopPathMovementWithAnimationBlend()；下游（含内部回调体） VillagerController.StopPathMovementWithAnimationBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ArchitectureInvokeStopPathMovementWithAnimationBlend()
    {
        StopPathMovementWithAnimationBlend();
    }

    /// <summary>中文含义：架构只读哈希编号爬行待机路径。使用位置：EnemyAnimationAdapter.HashCrawlIdlePath.get。</summary>
    internal static int ArchitectureReadHashCrawlIdlePath => HashCrawlIdlePath;
    /// <summary>中文含义：架构只读哈希编号爬行待机状态。使用位置：EnemyAnimationAdapter.HashCrawlIdleState.get。</summary>
    internal static int ArchitectureReadHashCrawlIdleState => HashCrawlIdleState;
    /// <summary>中文含义：架构只读哈希编号爬行子级状态路径。使用位置：EnemyAnimationAdapter.HashCrawlSubStatePath.get。</summary>
    internal static int ArchitectureReadHashCrawlSubStatePath => HashCrawlSubStatePath;
    /// <summary>中文含义：架构只读哈希编号死亡。使用位置：EnemyAnimationAdapter.HashDIE.get。</summary>
    internal static int ArchitectureReadHashDIE => HashDIE;
    /// <summary>中文含义：架构只读哈希编号生命比例。使用位置：EnemyAnimationAdapter.HashHealthRatio.get。</summary>
    internal static int ArchitectureReadHashHealthRatio => HashHealthRatio;
    /// <summary>中文含义：架构只读哈希编号移动行为播放速率。使用位置：EnemyAnimationAdapter.HashLocomotionPlaybackRate.get。</summary>
    internal static int ArchitectureReadHashLocomotionPlaybackRate => HashLocomotionPlaybackRate;
    /// <summary>中文含义：架构只读哈希编号移动横向。使用位置：EnemyAnimationAdapter.HashMoveX.get。</summary>
    internal static int ArchitectureReadHashMoveX => HashMoveX;
    /// <summary>中文含义：架构只读哈希编号移动纵向。使用位置：EnemyAnimationAdapter.HashMoveY.get。</summary>
    internal static int ArchitectureReadHashMoveY => HashMoveY;
    /// <summary>中文含义：架构只读哈希编号行走。使用位置：EnemyAnimationAdapter.HashWALK.get。</summary>
    internal static int ArchitectureReadHashWALK => HashWALK;
    /// <summary>中文含义：架构读取实际水平移动速度。使用位置：EnemyAnimationAdapter.actualHorizontalMovementSpeed.get。</summary>
    internal float ArchitectureReadActualHorizontalMovementSpeed => actualHorizontalMovementSpeed;
    /// <summary>中文含义：架构读取上一次稳定爬行移动方向。使用位置：EnemyAnimationAdapter.lastStableCrawlMoveDirection.get。</summary>
    internal Vector3 ArchitectureReadLastStableCrawlMoveDirection => lastStableCrawlMoveDirection;
    /// <summary>中文含义：架构读取上一次稳定站立移动方向。使用位置：EnemyAnimationAdapter.lastStableStandingMoveDirection.get。</summary>
    internal Vector3 ArchitectureReadLastStableStandingMoveDirection => lastStableStandingMoveDirection;

    /// <summary>ArchitectureInvokeGetTargetMoveSpeed 的业务处理入口；本方法直接依赖 VillagerController.GetTargetMoveSpeed()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.GetTargetMoveSpeed()；下游（含内部回调体） VillagerController.GetTargetMoveSpeed()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal float ArchitectureInvokeGetTargetMoveSpeed()
    {
        return GetTargetMoveSpeed();
    }

    /// <summary>ArchitectureInvokeHasCrawlMovementDirection 的业务处理入口；本方法直接依赖 VillagerController.HasCrawlMovementDirection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.HasCrawlMovementDirection()；下游（含内部回调体） VillagerController.HasCrawlMovementDirection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ArchitectureInvokeHasCrawlMovementDirection()
    {
        return HasCrawlMovementDirection();
    }

    /// <summary>ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame 的业务处理入口；本方法直接依赖 VillagerController.ResolveStandingPathDirectionForCurrentFrame()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.ResolveStandingPathDirectionForCurrentFrame()；下游（含内部回调体） VillagerController.ResolveStandingPathDirectionForCurrentFrame()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal Vector3 ArchitectureInvokeResolveStandingPathDirectionForCurrentFrame()
    {
        return ResolveStandingPathDirectionForCurrentFrame();
    }

    /// <summary>ResetCrawlOrientationCache 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginCrawl()、EnemyCombatModule.CancelStandUpAndResumeCrawl()、EnemyCombatModule.ForceCrawlPoseAfterQTE()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ResetCrawlOrientationCache()
    {
        lastStableCrawlMoveDirection = Vector3.zero;
        crawlFacingInitialized = false;
    }

    /// <summary>PublishDeathFact 的业务处理入口；本方法直接依赖 Action&lt;VillagerController&gt;.Invoke(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） Action&lt;VillagerController&gt;.Invoke(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void PublishDeathFact()
    {
        // receiverValue220（判空前缓存的已死亡）：从已死亡读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillagerController.PublishDeathFact()。
global::System.Action<global::VillagerController> receiverValue220 = Died;
        if (!global::System.Object.ReferenceEquals(receiverValue220, null))
        {
            receiverValue220.Invoke(this);
        }
    }
}
