using System;
using System.Collections.Generic;
using UnityEngine;
using GameArchitecture;

internal enum KnifeParryPromptKind
{
    None,
    Perfect,
    PressAgain
}

/// <summary>玩家控制、动作与运动模块 KnifeIncomingAttackContext；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀来袭上下文")]
public sealed class KnifeIncomingAttackContext
{
    /// <summary>攻击者。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击者")]
    [UnityEngine.InspectorName("攻击者")]
    public VillagerController attacker;
    /// <summary>攻击编号。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击编号")]
    [UnityEngine.InspectorName("攻击编号")]
    public long attackId;
    /// <summary>攻击段。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击段")]
    [UnityEngine.InspectorName("攻击段")]
    public int attackSegment;
    /// <summary>接触时间。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("接触时间")]
    [UnityEngine.InspectorName("接触时间")]
    public double contactTime;
    /// <summary>预计接触时间。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("预计接触时间")]
    [UnityEngine.InspectorName("预计接触时间")]
    public double expectedContactTime;
    /// <summary>接触位置。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("接触位置")]
    [UnityEngine.InspectorName("接触位置")]
    public Vector3 contactPosition;
    /// <summary>方向。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("方向")]
    [UnityEngine.InspectorName("方向")]
    public Vector3 direction;
    /// <summary>伤害。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("伤害")]
    [UnityEngine.InspectorName("伤害")]
    public float damage;
    /// <summary>是否可招架。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("可招架")]
    [UnityEngine.InspectorName("可招架")]
    public bool canParry;
    /// <summary>类别。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("类别")]
    [UnityEngine.InspectorName("类别")]
    public KnifeIncomingAttackCategory category;
    /// <summary>攻击原点。</summary>
    public Vector3 attackOrigin;
    /// <summary>是否具有攻击原点。</summary>
    public bool hasAttackOrigin;
    /// <summary>玩家位置所在接触。</summary>
    public Vector3 playerPositionAtContact;
    /// <summary>是否具有玩家位置所在接触。</summary>
    public bool hasPlayerPositionAtContact;
    /// <summary>玩家前向所在接触。</summary>
    public Vector3 playerForwardAtContact;
    /// <summary>是否具有玩家朝向所在接触。</summary>
    public bool hasPlayerFacingAtContact;
    /// <summary>segmentBegin 表示“线段开始”；segmentEnd 表示“线段结束”；animationTime 表示“动画时间”。</summary>
    public float segmentBegin, segmentEnd, animationTime;
    /// <summary>接触判定证据。</summary>
    public string contactEvidence;
    internal EnemyCombatModule.StandingImpact standingImpact;
    /// <summary>是否有效。</summary>
    public bool Valid => attacker != null && attacker.gameObject.activeInHierarchy && !attacker.IsDead
        && attacker.combatModule != null && attacker.combatModule.isActiveAndEnabled
        && attacker.combatModule.KnifeIncomingAttackId == attackId && !attacker.combatModule.ReadAttackFinished
        && (attacker.CurrentState == VillagerState.Attack || attacker.CurrentState == VillagerState.CrawlAttack);
}

/// <summary>玩家控制、动作与运动的结构化数据 KnifeMeleeHitNotification；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀近战命中通知")]
public readonly struct KnifeMeleeHitNotification
{
    /// <summary>动作实例。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly long actionInstance;
    /// <summary>攻击线段。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int attackSegment;
    /// <summary>目标村民敌人控制器。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly VillagerController target;
    /// <summary>身体部位。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly HitboxType bodyPart;
    /// <summary>位置。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly Vector3 position;
    /// <summary>构造并初始化 KnifeMeleeHitNotification 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">实例。</param>
    /// <param name="target">目标村民敌人控制器。</param>
    /// <param name="bodyPart">身体部位。</param>
    /// <param name="position">位置。</param>
    public KnifeMeleeHitNotification(long instance, VillagerController target, HitboxType bodyPart, Vector3 position)
    {
        actionInstance = instance;
        attackSegment = 0;
        this.target = target;
        this.bodyPart = bodyPart;
        this.position = position;
    }
}

/// <summary>输入、动作时间、刀刃扫掠和来袭裁定的唯一小刀业务入口。</summary>
/// <remarks>小刀引擎宿主，把输入和时间事实交给 KnifeCombatRules，再将经过的命中区间转为空间检测及伤害请求；配合当前动作、输入和反馈协议完成或取消。</remarks>
[DisallowMultipleComponent, DefaultExecutionOrder(-50)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗模块")]
public sealed partial class KnifeCombatModule : MonoBehaviour
{
    private Collider[] bladeOverlaps = new Collider[32];
    private Collider[] assistOverlaps = new Collider[32];
    private readonly HitQueryBuffer knifeOcclusionHits = new();

    /// <summary>玩家。</summary>
    [Header("组件引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("玩家")]
    [UnityEngine.InspectorName("玩家")]
    public LeonController player;
    /// <summary>战斗。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("战斗")]
    [UnityEngine.InspectorName("战斗")]
    public LeonCombat combat;
    /// <summary>当前玩家动画动作协调器。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("协调器")]
    [UnityEngine.InspectorName("协调器")]
    public LeonAnimationActionCoordinator coordinator;
    /// <summary>动画驱动。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动画驱动")]
    [UnityEngine.InspectorName("动画驱动")]
    public LeonAnimatorDriver animationDrive;
    /// <summary>武器。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("武器")]
    [UnityEngine.InspectorName("武器")]
    public LeonWeaponHandler weapon;
    /// <summary>武器视觉。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("武器视觉")]
    [UnityEngine.InspectorName("武器视觉")]
    public LeonWeaponVisualController weaponVisual;
    /// <summary>手部约束。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("手部约束")]
    [UnityEngine.InspectorName("手部约束")]
    public LeonAnimRig handConstraint;
    /// <summary>背包。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("背包")]
    [UnityEngine.InspectorName("背包")]
    public InventoryManager inventory;
    /// <summary>刀具。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("刀具")]
    [UnityEngine.InspectorName("刀具")]
    public Transform knifeVisual;
    /// <summary>配置。</summary>
    [Header("战斗配置")]
    [UnityEngine.Serialization.FormerlySerializedAs("配置")]
    [UnityEngine.InspectorName("配置")]
    public KnifeCombatConfiguration configuration;
    /// <summary>是否记录诊断。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("记录诊断")]
    [UnityEngine.InspectorName("记录诊断")]
    public bool recordDiagnostics = true;
    /// <summary>规则。</summary>
    private KnifeCombatRules rules;
    /// <summary>事务编号值。</summary>
    private int transactionIdValue = -1;
    private GridItem transactionKnife;
    /// <summary>isEnd 表示“是否结束”；bound 表示“是否已绑定”；attackKeyHold 表示“是否攻击键保持”。</summary>
    private bool isEnd, bound, attackKeyHold;
    /// <summary>是否等待中提交动画。</summary>
    private bool waitingCommitAnimation;
    private bool lastKnifeAttackWasThrust;
    /// <summary>advancedTime 表示“推进后的时间”；hitStopBegin 表示“命中顿帧开始”；hitStopEnd 表示“命中顿帧结束”。</summary>
    private double advancedTime, hitStopBegin, hitStopEnd;
    /// <summary>latestChargeInstance 表示“最新蓄力实例”；latestWhiffInstance 表示“最新挥空实例”。</summary>
    private long latestChargeInstance = -1, latestWhiffInstance = -1;
    private bool pendingSwingSound;
    /// <summary>结束突刺。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<long> finishedThrust = new HashSet<long>();
    /// <summary>前一次帧小刀实例。</summary>
    private long previousFrameKnifeInstance;
    /// <summary>前一次帧小刀时间。</summary>
    private double previousFrameKnifeTime;
    /// <summary>previousFrameBladeBase 表示“前一次帧刀刃基础”；previousFrameBladeTip 表示“前一次帧刀刃尖端”。</summary>
    private Vector3 previousFrameBladeBase, previousFrameBladeTip;
    /// <summary>startupOrientation 表示“启动朝向”；assistTargetOrientation 表示“辅助目标朝向”。</summary>
    private Quaternion startupOrientation, assistTargetOrientation;
    /// <summary>是否具有校正量角度。</summary>
    private float hasCorrectionAngle;
    /// <summary>输入队列。只读字段，初始化后不由普通写入口替换。</summary>
    private List<InputRecord> inputQueue = new List<InputRecord>();
    private List<InputRecord> inputBatch = new List<InputRecord>();
    /// <summary>命中间隔。只读字段，初始化后不由普通写入口替换。</summary>
    private struct RootSlice
    {
        public Vector3 velocity;
        public float duration;
    }

    private readonly Queue<RootSlice> rootSlices = new();
    private RootSlice currentRootSlice;

    private List<KnifeTraversedInterval> hitInterval = new List<KnifeTraversedInterval>();
    private List<KnifeTraversedInterval> hitIntervalBatch = new List<KnifeTraversedInterval>();
    /// <summary>传入攻击队列。只读字段，初始化后不由普通写入口替换。</summary>
    private List<KnifeIncomingAttackContext> incomingAttackQueue = new List<KnifeIncomingAttackContext>();
    private List<KnifeIncomingAttackContext> incomingAttackBatch = new List<KnifeIncomingAttackContext>();
    /// <summary>提示候选。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<int, KnifeIncomingAttackContext> promptCandidate = new Dictionary<int, KnifeIncomingAttackContext>();
    private (KnifeParryPromptKind kind, int enemyId, long attackId, int segment) lastParryPrompt;
    /// <summary>命中目标列表。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<(long, int)> hitTargets = new HashSet<(long, int)>();
    /// <summary>解算后的传入攻击。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<(int, long, int)> resolvedIncomingAttack = new HashSet<(int, long, int)>();
    /// <summary>日志。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Queue<string> log = new Queue<string>();
    /// <summary>接触列表。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<ContactCandidate> contactList = new List<ContactCandidate>();
    /// <summary>玩家控制、动作与运动的结构化数据 InputRecord；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗模块+输入记录")]
    private struct InputRecord
    {
        /// <summary>种类。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("种类")]
        [UnityEngine.InspectorName("种类")]
        public int kind;
        /// <summary>是否按住。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("按住")]
        [UnityEngine.InspectorName("按住")]
        public bool held;
        /// <summary>时间。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("时间")]
        [UnityEngine.InspectorName("时间")]
        public double time;
        /// <summary>序号。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("序号")]
        [UnityEngine.InspectorName("序号")]
        public long sequence;
    }

    /// <summary>玩家控制、动作与运动的结构化数据 ContactCandidate；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗模块+接触候选")]
    private struct ContactCandidate
    {
        /// <summary>碰撞体。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("碰撞体")]
        [UnityEngine.InspectorName("碰撞体")]
        public Collider collider;
        /// <summary>位置。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("位置")]
        [UnityEngine.InspectorName("位置")]
        public Vector3 position;
        /// <summary>距离。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("距离")]
        [UnityEngine.InspectorName("距离")]
        public float distance;
        /// <summary>时间。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("时间")]
        [UnityEngine.InspectorName("时间")]
        public double time;
    }

    /// <summary>输入序号。</summary>
    private long inputSequence;
    /// <summary>诊断序号。</summary>
    private long diagnosticSequence;
    /// <summary>是否就绪。</summary>
    public bool Ready => rules != null && configuration != null && coordinator != null && player != null;
    /// <summary>是否生效。</summary>
    public bool Active => Ready && rules.Active;
    /// <summary>是否攻击。</summary>
    public bool IsAttack => Active && rules.IsAttack;
    /// <summary>是否命中顿帧。</summary>
    public bool IsHitStop => Time.timeAsDouble < hitStopEnd;
    /// <summary>中文含义：是否防守保持。</summary>
    public bool GuardHold => Ready && rules.GuardHeld;
    internal bool IsHoldingGuardPose => IsGuardAiming && CurrentAction is (KnifeAction.EnterGuard or KnifeAction.HoldGuard);
    internal float GuardViewPitch { get; private set; }
    internal float GuardTurnDelta { get; private set; }
    internal float GuardEntryHeadingError { get; private set; }
    internal int GuardViewFrame { get; private set; } = -1;
    private float cameraHeading;
    private bool hasCameraHeading;
    private bool guardEntryTurning;
    private const float GuardEntryTurnSpeed = 480f;
    private readonly RaycastHit[] viewFallbackHits = new RaycastHit[8];
    private bool IsReleasedThrust => lastKnifeAttackWasThrust && !GuardHold && CurrentAction == KnifeAction.LowerGuard;
    internal bool IsFinishingThrust => Active && lastKnifeAttackWasThrust
        && (KnifeCombatRules.IsThrust(CurrentAction) || CurrentAction == KnifeAction.ThrustRecovery || IsReleasedThrust);
    public bool IsGuardAiming => Active && GuardHold && CanExecute
        && (CurrentAction == KnifeAction.EnterGuard || CurrentAction == KnifeAction.HoldGuard
            || CurrentAction == KnifeAction.ThrustOne || CurrentAction == KnifeAction.ThrustTwo
            || CurrentAction == KnifeAction.ThrustRecovery);
    internal bool IsThrustAttack
    {
        get
        {
            return Active && CanExecute && KnifeCombatRules.IsThrust(CurrentAction);
        }
    }

    internal bool ShouldTrackKnifeAimPitch
    {
        get
        {
            if (!Active || !CanExecute)
            {
                return false;
            }

            if (IsGuardAiming)
            {
                return true;
            }

            return KnifeCombatRules.IsThrust(CurrentAction)
                || CurrentAction == KnifeAction.ThrustRecovery
                || IsReleasedThrust;
        }
    }

    /// <summary>是否可以招架当前时刻。</summary>
    public bool CanParryNow
    {
        get
        {
            return Ready && CanExecute && ValidKnife && rules.HasGuardEligibility(Time.timeAsDouble);
        }
    }

    /// <summary>是否可以连接近战终结技。</summary>
    public bool CanConnectMeleeFinisher => Active && rules.CanHandoff;

    /// <summary>当前动作。</summary>
    public KnifeAction CurrentAction
    {
        get
        {
            if (rules == null)
            {
                return KnifeAction.None;
            }
            else
            {
                return rules.CurrentAction;
            }
        }
    }

    /// <summary>当前实例。</summary>
    public long CurrentInstance
    {
        get
        {
            if (rules == null)
            {
                return 0;
            }
            else
            {
                return rules.ActionInstance;
            }
        }
    }

    /// <summary>当前动作时间。</summary>
    public double CurrentActionTime
    {
        get
        {
            if (rules == null)
            {
                return 0;
            }
            else
            {
                return rules.ActionTime;
            }
        }
    }

    /// <summary>移动比例。</summary>
    public float MovementRatio
    {
        get
        {
            if (!Active)
            {
                return 1;
            }
            else
            {
                if (IsHitStop)
                {
                    return 0;
                }
                else
                {
                    if (!IsAttack)
                    {
                        return configuration.guardWalkSpeedRatio;
                    }
                    else
                    {
                        if (CurrentActionTime >= rules.Query(CurrentAction).hitBegin && CurrentActionTime <= rules.Query(CurrentAction).hitEnd)
                        {
                            return configuration.activePhaseWalkSpeedRatio;
                        }
                        else
                        {
                            return configuration.attackWalkSpeedRatio;
                        }
                    }
                }
            }
        }
    }

    /// <summary>ReadDiagnostics 的业务处理入口；本方法直接依赖 Queue&lt;string&gt;.ToArray()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Queue&lt;string&gt;.ToArray()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string[] ReadDiagnostics() => log.ToArray();
    /// <summary>中文含义：诊断序号。</summary>
    public long DiagnosticSequence => diagnosticSequence;

    /// <summary>ReadDiagnosticsAfter 的业务处理入口；本方法直接依赖 Array.Copy(Array, int, Array, int, int)、Math.Max(long, long)、Math.Min(long, long)、Queue&lt;string&gt;.ToArray()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Array.Copy(Array, int, Array, int, int)、Math.Max(long, long)、Math.Min(long, long)、Queue&lt;string&gt;.ToArray()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sequence">序号。</param>
    public string[] ReadDiagnosticsAfter(long sequence)
    {
        // 快照。
string[] snapshot = log.ToArray();
        // 跳过。
int skip = (int)Math.Min(snapshot.Length, Math.Max(0L, sequence - diagnosticSequence + snapshot.Length));
        // 当前文本数组。
string[] result = new string[snapshot.Length - skip];
        Array.Copy(snapshot, skip, result, 0, result.Length);
        return result;
    }

    /// <summary>已提交命中。事件通信成员 CommittedHit，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<KnifeMeleeHitNotification> CommittedHit;
    /// <summary>招架解算后的。事件通信成员 ParryResolved，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event Action<KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot> ParryResolved;
    /// <summary>中文含义：招架尝试。</summary>
    public KnifeParryAttemptSnapshot ParryAttempt
    {
        get
        {
            if (rules != null)
            {
                return rules.CurrentAttempt;
            }

            return default;
        }
    }

    /// <summary>是否有效小刀。</summary>
    public bool ValidKnife
    {
        get
        {
            GridItem knife = CurrentKnife();
            return weapon != null && weapon.knife != null && knife != null && knife.currentDurability != 0 &&
                knife.item != null && knife.item.usesDurability;
        }
    }

    private GridItem CurrentKnife()
    {
        if (transactionIdValue >= 0)
        {
            return transactionKnife;
        }
        if (inventory != null)
        {
            return inventory.GetPreferredKnife();
        }
        return null;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        if (player == null)
        {
            player = GetComponent<LeonController>();
        }

        if (combat == null)
        {
            combat = GetComponent<LeonCombat>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (animationDrive == null)
        {
            animationDrive = GetComponent<LeonAnimatorDriver>();
        }

        if (weapon == null)
        {
            weapon = GetComponent<LeonWeaponHandler>();
        }

        if (weaponVisual == null)
        {
            weaponVisual = GetComponent<LeonWeaponVisualController>();
        }

        if (handConstraint == null)
        {
            handConstraint = GetComponent<LeonAnimRig>();
        }

        if (inventory == null)
        {
            inventory = InventoryManager.Instance;
        }

        if (knifeVisual == null && weaponVisual != null)
        {
            knifeVisual = weaponVisual.GetWeaponVisualRoot(WeaponType.Knife);
        }

        if (configuration == null)
        {
            return;
        }

        rules = configuration.CreateRules();
        rules.ActionHasSwitch += ActionSwitch;
        rules.ActionAdvanced += AccumulateRootMotion;
        rules.IntervalTraversed += /* 中文含义：间隔。 */ interval => hitInterval.Add(interval);
        advancedTime = Time.timeAsDouble;
        BindingEvent();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeCombatModule.BindingEvent()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        advancedTime = Time.timeAsDouble;
        BindingEvent();
    }

    /// <summary>BindingEvent 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.OnEnable()、KnifeCombatModule.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindingEvent()
    {
        if (bound || coordinator == null)
        {
            return;
        }

        coordinator.OnMeleeActionInterrupted += ActionInterrupted;
        coordinator.OnDeathEntered += DeathInterrupt;
        bound = true;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;int, KnifeIncomingAttackContext&gt;.Clear()、HashSet&lt;(int, long, int)&gt;.Clear()、KnifeCombatModule.Interrupt(bool)、List&lt;KnifeIncomingAttackContext&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        Interrupt(false);
        incomingAttackQueue.Clear();
        promptCandidate.Clear();
        resolvedIncomingAttack.Clear();
        previousFrameKnifeInstance = 0;
        if (bound && coordinator != null)
        {
            coordinator.OnMeleeActionInterrupted -= ActionInterrupted;
            coordinator.OnDeathEntered -= DeathInterrupt;
        }

        bound = false;
    }

    /// <summary>ActionInterrupted 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">动作。</param>
    private void ActionInterrupted(AnimationActionTransaction action)
    {
        if (action.transactionId == transactionIdValue)
        {
            Interrupt(false);
        }
    }

    /// <summary>DeathInterrupt 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DeathInterrupt() => Interrupt(false);
    /// <summary>CommitGuard 的业务处理入口；本方法直接依赖 KnifeCombatModule.EnqueueValue(int, bool, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonCombat.CommitGuardInput(bool, double)；下游（含内部回调体） KnifeCombatModule.EnqueueValue(int, bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="held">是否保持按住。</param>
    /// <param name="time">时间。</param>
    public void CommitGuard(bool held, double time) => EnqueueValue(0, held, time);

    internal bool CanResumeHeldGuard => Ready && CanExecute && ValidKnife && !GuardHold
        && (transactionIdValue >= 0 || coordinator.CanPrepareKnifeGuard());

    internal void CommitHeldGuard(double time) => EnqueueValue(3, true, time);
    /// <summary>CommitAttack 的业务处理入口；本方法直接依赖 KnifeCombatModule.EnqueueValue(int, bool, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonCombat.CommitAttackInput(double)；下游（含内部回调体） KnifeCombatModule.EnqueueValue(int, bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    public void CommitAttack(double time) => EnqueueValue(1, true, time);
    /// <summary>CommitAttackReleased 的业务处理入口；本方法直接依赖 KnifeCombatModule.EnqueueValue(int, bool, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonCombat.CommitAttackReleased(double)；下游（含内部回调体） KnifeCombatModule.EnqueueValue(int, bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    public void CommitAttackReleased(double time) => EnqueueValue(2, false, time);
    /// <summary>EnqueueValue 的业务处理入口；本方法直接依赖 KnifeCombatModule.InputRecord.InputRecord()、List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CommitAttack(double)、KnifeCombatModule.CommitAttackReleased(double)、KnifeCombatModule.CommitGuard(bool, double)；下游（含内部回调体） KnifeCombatModule.InputRecord.InputRecord()、List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">种类。</param>
    /// <param name="held">是否保持按住。</param>
    /// <param name="time">时间。</param>
    private void EnqueueValue(int kind, bool held, double time)
    {
        if (!Ready || !isActiveAndEnabled || Time.timeScale <= 0 && (kind == 1 || held))
        {
            return;
        }

        inputQueue.Add(new InputRecord { kind = kind, held = held, time = time, sequence = ++inputSequence });
    }

    /// <summary>是否可以执行。</summary>
    private bool CanExecute => player != null && player.NewPipelineValid && player.currentState != LeonState.Dead && player.currentState != LeonState.Staggered && player.currentState != LeonState.QTE && player.currentState != LeonState.Traversing;

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） double.CompareTo(double)、double.ToString(string)、KnifeCombatModule.AdvanceTo(double)、KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.PrepareTransaction(bool)、KnifeCombatModule.Record(string, string) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        if (!Ready || Time.timeScale <= 0)
        {
            return;
        }

        if (!CanExecute)
        {
            if (Active || inputQueue.Count > 0)
            {
                Interrupt(false);
            }

            rootSlices.Clear();
            currentRootSlice = default;
            advancedTime = Time.timeAsDouble;
            return;
        }

        if (Active && !ValidKnife)
        {
            Interrupt(true);
        }

        animationDrive.SetKnifeLocalHitStop(IsHitStop);
        // 先按真实时间排序，仅完全同刻时防守优先，避免稍晚输入倒置历史接触。
        inputQueue.Sort(( /* 中文含义：第一个输入记录。 */first, /* 中文含义：第二个输入记录。 */ second) =>
        {
            // 顺序。
int order = first.time.CompareTo(second.time);
            if (order != 0)
            {
                return order;
            }

            if (first.kind == 0 && second.kind != 0)
            {
                return -1;
            }

            if (second.kind == 0 && first.kind != 0)
            {
                return 1;
            }

            return first.sequence.CompareTo(second.sequence);
        });
        List<InputRecord> inputSnapshot = inputQueue;
        inputQueue = inputBatch;
        inputBatch = inputSnapshot;
        foreach (var input in inputSnapshot)
        {
            // 时间戳。
double timestamp = Math.Min(Time.timeAsDouble, Math.Max(advancedTime, input.time));
            AdvanceTo(timestamp);
            if (input.kind == 0)
            {
                if (input.held && (!ValidKnife || !PrepareTransaction(true)))
                {
                    if (recordDiagnostics)
                    {
                        Record("防守拒绝", "无可用刀或动作事务不允许；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6"));
                    }
                    continue;
                }

                rules.GuardInput(input.held, input.time, input.sequence);
                // 边缘。
string edge = "松开";
                if (input.held)
                {
                    edge = "按下";
                }

                if (recordDiagnostics)
                {
                    Record("防守输入", edge + "；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6") + "；到期=" + rules.CurrentAttempt.expiresAt.ToString("F6") + "；阶段=" + rules.CurrentAction + "；此刻资格=" + rules.HasGuardEligibility(input.time));
                }
            }
            else if (input.kind == 3)
            {
                if (!rules.GuardHeld && ValidKnife && PrepareTransaction(true))
                {
                    rules.ResumeHeldGuard(timestamp);
                }
            }
            else if (input.kind == 2)
            {
                attackKeyHold = false;
                combat.ExecuteFirearmAttackReleased();
            }
            else
            {
                if (attackKeyHold)
                {
                    continue;
                }

                attackKeyHold = true;
                if (!Active && combat.IsAiming)
                {
                    combat.ExecuteFirearmAttack();
                    continue;
                }

                if (!ValidKnife || !PrepareTransaction(false))
                {
                    continue;
                }

                rules.AttackInput();
                // 按原顺序缓存的实参文字“攻击输入”。
string argumentValue9 = "攻击输入";
                // 分支选出的文字“缓存下一刀”。
string selectedValue8;
                if (rules.HasCache)
                {
                    selectedValue8 = "缓存下一刀";
                }
                else
                {
                    selectedValue8 = "执行单刀";
                }

                if (recordDiagnostics)
                {
                    Record(argumentValue9, (selectedValue8) + "；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6"));
                }
            }
        }

        inputSnapshot.Clear();
        AdvanceTo(Time.timeAsDouble);
        CommitCurrentAnimation();
        UpdateWeakSteering();
    }

    /// <summary>AdvanceTo 的业务处理入口；本方法直接依赖 KnifeCombatRules.Advance(double, double)、Math.Max(double, double)、Math.Min(double, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()；下游（含内部回调体） KnifeCombatRules.Advance(double, double)、Math.Max(double, double)、Math.Min(double, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    private void AdvanceTo(double time)
    {
        // 增量。
double delta = Math.Max(0, time - advancedTime);
        // 冻结。
double freeze = Math.Max(0, Math.Min(time, hitStopEnd) - Math.Max(advancedTime, hitStopBegin));
        advancedTime = time;
        rules.Advance(Math.Max(0, delta - freeze), time);
    }

    /// <summary>PrepareTransaction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.BeginTemporaryKnife()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()；下游（含内部回调体） LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.BeginTemporaryKnife()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="guard">是否防守。</param>
    internal void ApplyCameraHeading(float heading, float pitch)
    {
        cameraHeading = heading;
        hasCameraHeading = true;
        if (ShouldTrackKnifeAimPitch && !IsHitStop && !player.IsolateObserveCameraSteering && Time.deltaTime > 0f)
        {
            GuardViewPitch = pitch;
            GuardViewFrame = Time.frameCount;

            if (IsHoldingGuardPose)
            {
                float previousHeading = transform.eulerAngles.y;
                float bodyHeading = heading;
                GuardEntryHeadingError = 0f;
                if (CurrentAction == KnifeAction.EnterGuard || guardEntryTurning)
                {
                    // 侧面或背面入防时，让真实转角逐帧驱动换脚，保持阶段继续跟随镜头。
                    bodyHeading = Mathf.MoveTowardsAngle(previousHeading, heading, GuardEntryTurnSpeed * Mathf.Min(Time.deltaTime, .05f));
                    GuardEntryHeadingError = Mathf.DeltaAngle(bodyHeading, heading);
                    guardEntryTurning = Mathf.Abs(GuardEntryHeadingError) > .1f;
                }
                bool committed = coordinator.CommitKnifeAssistOrientation(Quaternion.Euler(0f, bodyHeading, 0f));
                GuardTurnDelta = 0f;
                if (committed)
                {
                    GuardTurnDelta = Mathf.DeltaAngle(previousHeading, bodyHeading);
                }
            }
            else if (KnifeCombatRules.IsThrust(CurrentAction) || CurrentAction == KnifeAction.ThrustRecovery || IsReleasedThrust)
            {
                // 前刺途中保持实时响应准心水平朝向，避免身体继续沿着起手方向挥刺。
                float currentHeading = transform.eulerAngles.y;
                float bodyHeading = Mathf.MoveTowardsAngle(currentHeading, heading, 720f * Mathf.Min(Time.deltaTime, .05f));
                coordinator.CommitKnifeAssistOrientation(Quaternion.Euler(0f, bodyHeading, 0f));
            }
        }
    }

    private void AccumulateRootMotion(KnifeTraversedInterval interval)
    {
        var clip = configuration.Find(interval.action);
        if (clip == null || !clip.HasPlanarRoot)
        {
            return;
        }

        float duration = (float)(interval.end - interval.begin);
        if (duration <= 0.00001f)
        {
            return;
        }

        Vector3 displacement = clip.PlanarRootAt(interval.end) - clip.PlanarRootAt(interval.begin);
        rootSlices.Enqueue(new RootSlice
        {
            velocity = displacement / duration,
            duration = duration
        });
    }

    internal bool ConsumeRootMotion(float stepTime, out Vector3 displacement)
    {
        displacement = Vector3.zero;
        if (configuration == null)
        {
            return false;
        }

        var activeClip = configuration.Find(CurrentAction);
        bool ownsMotion = currentRootSlice.duration > 0f || rootSlices.Count > 0
            || Active && activeClip != null && activeClip.HasPlanarRoot;
        if (IsHitStop)
        {
            return ownsMotion;
        }

        while (stepTime > 0f)
        {
            if (currentRootSlice.duration <= 0f)
            {
                if (rootSlices.Count == 0)
                {
                    break;
                }
                currentRootSlice = rootSlices.Dequeue();
            }

            float step = Mathf.Min(stepTime, currentRootSlice.duration);
            displacement += transform.TransformDirection(currentRootSlice.velocity) * step;
            currentRootSlice.duration -= step;
            stepTime -= step;
        }

        return ownsMotion;
    }

    private bool PrepareTransaction(bool guard)
    {
        if (!CanExecute)
        {
            return false;
        }

        if (transactionIdValue >= 0)
        {
            return true;
        }

        if (guard && !coordinator.AsKnifeInterruptReload())
        {
            return false;
        }

        // 新事务。
int newTransaction = coordinator.RequestIndependentKnifeTransaction();
        if (newTransaction < 0)
        {
            return false;
        }

        transactionKnife = inventory.GetPreferredKnife();
        transactionIdValue = newTransaction;
        lastKnifeAttackWasThrust = false;
        combat.BeginTemporaryKnife();
        weaponVisual.SetKnifeAppearance(transactionKnife);
        weaponVisual.SetKnifeModeVisible(true);
        // 判空前缓存的手部约束。
global::LeonAnimRig receiverValue10 = handConstraint;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.SetIKMode(IKMode.None);
        }

        return true;
    }

    /// <summary>ActionSwitch 的业务处理入口；本方法直接依赖 Enum.ToString()、HashSet&lt;(long, int)&gt;.Clear()、HashSet&lt;long&gt;.Clear()、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)、KnifeCombatModule.SelectAssistTarget()、KnifeCombatRules.IsAttackValue(KnifeAction) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Enum.ToString()、HashSet&lt;(long, int)&gt;.Clear()、HashSet&lt;long&gt;.Clear()、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)、KnifeCombatModule.SelectAssistTarget()、KnifeCombatRules.IsAttackValue(KnifeAction) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">动作。</param>
    /// <param name="instance">实例。</param>
    /// <param name="offset">中文含义：偏移量。</param>
    private void ActionSwitch(KnifeAction action, long instance, double offset)
    {
        if (action != KnifeAction.HoldGuard)
        {
            guardEntryTurning = false;
        }
        if (recordDiagnostics)
        {
            Record("动作切换", action.ToString());
        }
        pendingSwingSound = false;
        waitingCommitAnimation = action != KnifeAction.None;
        if (action == KnifeAction.None)
        {
            return;
        }

        // 调用前缓存的玩家。
global::LeonController callReceiver12 = player;
        // 分支选出的里昂状态的近战攻击中。
global::LeonState selectedValue11;
        if (KnifeCombatRules.IsAttackValue(action))
        {
            selectedValue11 = LeonState.MeleeAttacking;
        }
        else
        {
            selectedValue11 = LeonState.Parrying;
        }

        callReceiver12.ChangeState(selectedValue11);
        if (KnifeCombatRules.IsAttackValue(action))
        {
            lastKnifeAttackWasThrust = KnifeCombatRules.IsThrust(action);
            if (hitTargets.Count > 512)
            {
                hitTargets.Clear();
            }

            if (finishedThrust.Count > 256)
            {
                finishedThrust.Clear();
            }

            SelectAssistTarget();
            if (latestWhiffInstance != instance)
            {
                latestWhiffInstance = instance;
                pendingSwingSound = true;
            }
        }
    }

    /// <summary>CommitCurrentAnimation 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、KnifeCombatConfiguration.Find(KnifeAction)、KnifeCombatModule.Interrupt(bool)、KnifeCombatRules.IsReaction(KnifeAction)、LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonWeaponVisualController.SetCombatKnifeGripPose(KnifeCombatConfiguration, bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、KnifeCombatModule.Update()；下游（含内部回调体） Debug.LogError(object, Object)、KnifeCombatConfiguration.Find(KnifeAction)、KnifeCombatModule.Interrupt(bool)、KnifeCombatRules.IsReaction(KnifeAction)、LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonWeaponVisualController.SetCombatKnifeGripPose(KnifeCombatConfiguration, bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CommitCurrentAnimation()
    {
        if (!waitingCommitAnimation || !Active)
        {
            return;
        }

        waitingCommitAnimation = false;
        // 动作。
var action = CurrentAction;
        // 动画片段。
var clip = configuration.Find(action);
        // 混合。
float blend;
        if (KnifeCombatRules.IsReaction(action))
        {
            blend = configuration.contactBlend;
        }
        else
        {
            blend = configuration.normalBlend;
            if (action == KnifeAction.Backslash && configuration.Find(KnifeAction.ThirdSlash) != null)
            {
                // 回切的起姿与两种挥出末姿有差距，在命中窗口前完成过渡。
                blend = Mathf.Max(blend, 0.12f);
            }
        }

        // 输入或接招发生在这一帧中途时，动画从业务已经推进到的同一时刻进入。
        if (!coordinator.AdvanceIndependentKnifeTransaction(transactionIdValue, clip, blend, (float)CurrentActionTime))
        {
            Debug.LogError("小刀动作配置或正式控制器状态缺失：" + action, this);
            Interrupt(true);
            return;
        }

        if (pendingSwingSound && KnifeCombatRules.IsAttackValue(action))
        {
            pendingSwingSound = false;
            AudioEventData swing = configuration.slashSwingEvent;
            if (KnifeCombatRules.IsThrust(action))
            {
                swing = configuration.thrustSwingEvent;
            }

            if (!TryGetComponent<NativeActionAudio>(out var nativeAudio) || !nativeAudio.HasWeaponCue(clip.sourceClip))
            {
                PlayConfiguredSound(swing, configuration.whiffSound, transform.position);
            }
        }

        weaponVisual.SetCombatKnifeGripPose(configuration, action == KnifeAction.OutwardDeflectionNormal || action == KnifeAction.OutwardDeflectionPerfect, blend);
    }

    /// <summary>EndTransaction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)、LeonCombat.EndTemporaryKnife(bool)、LeonWeaponVisualController.EndCombatKnifeGripPose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.ResolveContactsAfterPose()；下游（含内部回调体） LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)、LeonCombat.EndTemporaryKnife(bool)、LeonWeaponVisualController.EndCombatKnifeGripPose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreAim">是否恢复瞄准。</param>
    private void EndTransaction(bool restoreAim, bool releasedThrust = false)
    {
        if (transactionIdValue < 0)
        {
            return;
        }

        // 编号。
int id = transactionIdValue;
        transactionIdValue = -1;
        transactionKnife = null;
        // 判空前缓存的当前玩家动画动作协调器。
global::LeonAnimationActionCoordinator receiverValue14 = coordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.EndIndependentKnifeTransaction(id, releasedThrust);
        }

        // 判空前缓存的武器视觉。
global::LeonWeaponVisualController receiverValue15 = weaponVisual;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.EndCombatKnifeGripPose();
        }

        // 判空前缓存的武器视觉。
global::LeonWeaponVisualController receiverValue16 = weaponVisual;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            receiverValue16.SetKnifeAppearance(CurrentKnife());
            receiverValue16.SetKnifeModeVisible(combat != null && combat.IsKnifeMode);
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue17 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.EndTemporaryKnife(restoreAim && CanExecute);
        }
    }

    /// <summary>Interrupt 的业务处理入口；本方法直接依赖 KnifeCombatModule.EndTransaction(bool)、KnifeCombatRules.Interrupt()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)、List&lt;KnifeCombatModule.InputRecord&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.Clear()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionInterrupted(AnimationActionTransaction)、KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.DeathInterrupt()、KnifeCombatModule.OnDisable()、KnifeCombatModule.ResolveContactsAfterPose() 等共 11 处；下游（含内部回调体） KnifeCombatModule.EndTransaction(bool)、KnifeCombatRules.Interrupt()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)、List&lt;KnifeCombatModule.InputRecord&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreAim">是否恢复瞄准。</param>
    public void Interrupt(bool restoreAim)
    {
        if (isEnd)
        {
            return;
        }

        isEnd = true;
        inputQueue.Clear();
        hitInterval.Clear();
        rootSlices.Clear();
        currentRootSlice = default;
        attackKeyHold = false;
        hitStopEnd = 0;
        // 判空前缓存的动画驱动。
global::LeonAnimatorDriver receiverValue18 = animationDrive;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.SetKnifeLocalHitStop(false);
        }

        // 判空前缓存的规则。
global::GameArchitecture.KnifeCombatRules receiverValue19 = rules;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            receiverValue19.Interrupt();
        }

        EndTransaction(restoreAim);
        isEnd = false;
    }

    /// <summary>YieldToMeleeFinisher 的业务处理入口；本方法直接依赖 KnifeCombatModule.Interrupt(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.TryStartQTEBodyTechnique(int, int)；下游（含内部回调体） KnifeCombatModule.Interrupt(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool YieldToMeleeFinisher()
    {
        if (!Active)
        {
            return true;
        }

        if (!rules.CanHandoff)
        {
            return false;
        }

        Interrupt(false);
        return true;
    }

    /// <summary>SelectAssistTarget 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;VillagerController&gt;()、KnifeCombatModule.NoneOcclusion(Vector3, Transform)、Physics.OverlapSphere(Vector3, float, int, QueryTriggerInteraction)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)；下游（含内部回调体） Component.GetComponentInParent&lt;VillagerController&gt;()、KnifeCombatModule.NoneOcclusion(Vector3, Transform)、Physics.OverlapSphere(Vector3, float, int, QueryTriggerInteraction)、Quaternion.LookRotation(Vector3)、Quaternion.RotateTowards(Quaternion, Quaternion, float)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SelectAssistTarget()
    {
        startupOrientation = transform.rotation;
        assistTargetOrientation = startupOrientation;
        hasCorrectionAngle = 0;
        // 最小角度。
float minimumAngle = configuration.assistTotalAngle * .5f;
        // collision（碰撞）：每轮从重叠球体的返回结果取得一个元素，仅代表当前这一项。使用位置：KnifeCombatModule.SelectAssistTarget()。
        Vector3 assistOrigin = transform.position + Vector3.up;
        int assistCount = Physics.OverlapSphereNonAlloc(assistOrigin, configuration.assistDistance,
            assistOverlaps, configuration.hitMask, QueryTriggerInteraction.Collide);
        while (assistCount == assistOverlaps.Length)
        {
            System.Array.Resize(ref assistOverlaps, assistOverlaps.Length * 2);
            assistCount = Physics.OverlapSphereNonAlloc(assistOrigin, configuration.assistDistance,
                assistOverlaps, configuration.hitMask, QueryTriggerInteraction.Collide);
        }
        for (int index = 0; index < assistCount; index++)
        {
            Collider collision = assistOverlaps[index];
            // 敌人。
var enemy = collision.GetComponentInParent<VillagerController>();
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            // 方向。
var direction = enemy.transform.position - transform.position;
            direction.y = 0;
            // 角度。
float angle = Vector3.Angle(transform.forward, direction);
            if (direction.sqrMagnitude < .0001f || angle >= minimumAngle || !NoneOcclusion(enemy.transform.position + Vector3.up, enemy.transform))
            {
                continue;
            }

            minimumAngle = angle;
            assistTargetOrientation = Quaternion.RotateTowards(startupOrientation, Quaternion.LookRotation(direction), configuration.maximumCorrectionAngle);
        }
    }

    /// <summary>UpdateWeakSteering 的业务处理入口；本方法直接依赖 KnifeCombatRules.Query(KnifeAction)、LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion)、Mathf.Min(float, float)、Quaternion.Angle(Quaternion, Quaternion)、Quaternion.RotateTowards(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()；下游（含内部回调体） KnifeCombatRules.Query(KnifeAction)、LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion)、Mathf.Min(float, float)、Quaternion.Angle(Quaternion, Quaternion)、Quaternion.RotateTowards(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateWeakSteering()
    {
        if (IsHoldingGuardPose && hasCameraHeading)
        {
            return;
        }
        if ((IsGuardAiming || KnifeCombatRules.IsThrust(CurrentAction) || IsReleasedThrust)
            && !IsHitStop && !player.IsolateObserveCameraSteering && Time.deltaTime > 0f)
        {
            float heading = transform.eulerAngles.y;
            if (hasCameraHeading)
            {
                heading = cameraHeading;
            }
            else if (player.playerCamera != null)
            {
                heading = player.playerCamera.transform.eulerAngles.y;
            }

            Quaternion target = Quaternion.Euler(0f, heading, 0f);
            Quaternion facing = Quaternion.RotateTowards(transform.rotation, target, 540f * Time.deltaTime);
            coordinator.CommitKnifeAssistOrientation(facing);
            return;
        }

        if (KnifeCombatRules.IsThrust(CurrentAction) || !IsAttack || IsHitStop || CurrentActionTime > rules.Query(CurrentAction).earlyPhaseCancelEnd || hasCorrectionAngle >= configuration.maximumCorrectionAngle)
        {
            return;
        }

        // 步进尺寸。
float stepSize = Mathf.Min(120f * Time.deltaTime, configuration.maximumCorrectionAngle - hasCorrectionAngle);
        // 新朝向。
var newOrientation = Quaternion.RotateTowards(transform.rotation, assistTargetOrientation, stepSize);
        hasCorrectionAngle += Quaternion.Angle(transform.rotation, newOrientation);
        coordinator.CommitKnifeAssistOrientation(newOrientation);
    }

    /// <summary>RegisterIncomingAttackPrompt 的业务处理入口；本方法直接依赖 Object.GetInstanceID()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.LateUpdate()；下游（含内部回调体） Object.GetInstanceID()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">传入攻击。</param>
    public void RegisterIncomingAttackPrompt(KnifeIncomingAttackContext incomingAttack)
    {
        // 判空前缓存的传入攻击。
global::KnifeIncomingAttackContext receiverValue20 = incomingAttack;
        // 条件访问得到的接收者值20的攻击者。
global::VillagerController optionalValue21;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            optionalValue21 = receiverValue20.attacker;
        }
        else
        {
            optionalValue21 = default;
        }

        if (optionalValue21 != null)
        {
            promptCandidate[incomingAttack.attacker.GetInstanceID()] = incomingAttack;
        }
    }

    /// <summary>CloseIncomingAttackPrompt 的业务处理入口；本方法直接依赖 Dictionary&lt;int, KnifeIncomingAttackContext&gt;.Remove(int)、Object.GetInstanceID()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowClose()、EnemyCombatModule.LateUpdate()；下游（含内部回调体） Dictionary&lt;int, KnifeIncomingAttackContext&gt;.Remove(int)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源村民敌人控制器。</param>
    public void CloseIncomingAttackPrompt(VillagerController source)
    {
        if (source != null)
        {
            promptCandidate.Remove(source.GetInstanceID());
        }
    }

    /// <summary>是否显示招架提示。</summary>
    public bool ShowParryPrompt { get => CurrentPromptIncomingAttack != null; }

    internal KnifeParryPromptKind ReadParryPrompt(bool canReadInput, bool held)
    {
        KnifeIncomingAttackContext candidate = null;
        if (canReadInput)
        {
            candidate = CurrentPromptIncomingAttack;
        }

        KnifeParryPromptKind kind = KnifeParryPromptKind.None;
        if (candidate != null)
        {
            double contactTime = candidate.expectedContactTime;
            if (held)
            {
                if (!rules.HasGuardEligibility(contactTime))
                {
                    kind = KnifeParryPromptKind.PressAgain;
                }
            }
            else if (contactTime - Time.timeAsDouble <= rules.PerfectWindow + 1e-7)
            {
                kind = KnifeParryPromptKind.Perfect;
            }
        }

        if (kind == KnifeParryPromptKind.None)
        {
            lastParryPrompt = default;
            return kind;
        }

        var prompt = (kind, candidate.attacker.GetInstanceID(), candidate.attackId, candidate.attackSegment);
        if (lastParryPrompt != prompt)
        {
            lastParryPrompt = prompt;
            if (recordDiagnostics)
            {
                Record("弹反提示", kind + "；敌人=" + candidate.attacker.name + "；攻击=" + candidate.attackId
                    + "；段=" + candidate.attackSegment + "；预计接触=" + candidate.expectedContactTime.ToString("F6"));
            }
        }

        return kind;
    }

    /// <summary>当前提示传入攻击。</summary>
    public KnifeIncomingAttackContext CurrentPromptIncomingAttack
    {
        get
        {
            if (promptCandidate.Count == 0 || !Ready || !isActiveAndEnabled || !ValidKnife || !CanExecute || Time.timeScale <= 0f
                || !rules.CanAcceptGuardPress || (transactionIdValue < 0 && !coordinator.CanPrepareKnifeGuard())
                || (player.IsCrouching && !player.CanStandUp()))
            {
                return null;
            }

            double now = Time.timeAsDouble;
            KnifeIncomingAttackContext mostUrgent = null;
            int mostUrgentEnemyId = 0;
            foreach (var entry in promptCandidate)
            {
                KnifeIncomingAttackContext candidate = entry.Value;
                double lead = candidate.expectedContactTime - now;
                bool isUpcoming = lead > 0 && lead <= rules.TapWindow + .000001;
                if (!isUpcoming || !candidate.Valid || !candidate.canParry || !IsFront(candidate)
                    || resolvedIncomingAttack.Contains((entry.Key, candidate.attackId, candidate.attackSegment)))
                {
                    continue;
                }

                if (mostUrgent == null || candidate.expectedContactTime < mostUrgent.expectedContactTime
                    || candidate.expectedContactTime == mostUrgent.expectedContactTime && entry.Key < mostUrgentEnemyId)
                {
                    mostUrgent = candidate;
                    mostUrgentEnemyId = entry.Key;
                }
            }

            return mostUrgent;
        }
    }

    /// <summary>CommitIncomingAttack 的业务处理入口；本方法直接依赖 List&lt;KnifeIncomingAttackContext&gt;.Add(KnifeIncomingAttackContext)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.LateUpdate()、EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） List&lt;KnifeIncomingAttackContext&gt;.Add(KnifeIncomingAttackContext)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">传入攻击。</param>
    public void CommitIncomingAttack(KnifeIncomingAttackContext incomingAttack)
    {
        if (incomingAttack != null)
        {
            incomingAttackQueue.Add(incomingAttack);
        }
    }

    /// <summary>IsFront 的业务处理入口；本方法直接依赖 Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CurrentPromptIncomingAttack.get、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">传入攻击。</param>
    private bool IsFront(KnifeIncomingAttackContext incomingAttack)
    {
        // 原点。
Vector3 origin = incomingAttack.attacker.transform.position;
        // 玩家位置。
Vector3 playerPosition = transform.position;
        if (incomingAttack.hasAttackOrigin)
        {
            origin = incomingAttack.attackOrigin;
        }

        if (incomingAttack.hasPlayerPositionAtContact)
        {
            playerPosition = incomingAttack.playerPositionAtContact;
        }

        // 方向。
Vector3 direction = origin - playerPosition;
        direction.y = 0;
        if (direction.sqrMagnitude < .0001f)
        {
            direction = -incomingAttack.direction;
        }

        // 前向。
Vector3 forward = transform.forward;
        if (incomingAttack.hasPlayerFacingAtContact)
        {
            forward = incomingAttack.playerForwardAtContact;
        }

        return Vector3.Angle(forward, direction) <= configuration.guardTotalAngle * .5f;
    }

    /// <summary>ResolveIncomingContacts 的业务处理入口；本方法直接依赖 double.CompareTo(double)、int.CompareTo(int)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、List&lt;KnifeIncomingAttackContext&gt;.Clear()、List&lt;KnifeIncomingAttackContext&gt;.Sort(Comparison&lt;KnifeIncomingAttackContext&gt;)、List&lt;KnifeIncomingAttackContext&gt;.ToArray()、Object.GetInstanceID()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） double.CompareTo(double)、int.CompareTo(int)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、List&lt;KnifeIncomingAttackContext&gt;.Clear()、List&lt;KnifeIncomingAttackContext&gt;.Sort(Comparison&lt;KnifeIncomingAttackContext&gt;)、List&lt;KnifeIncomingAttackContext&gt;.ToArray()、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResolveIncomingContacts()
    {
        if (!Ready || Time.timeScale <= 0)
        {
            return;
        }

        incomingAttackQueue.Sort(( /* 中文含义：第一个小刀传入攻击上下文。 */first, /* 中文含义：第二个小刀传入攻击上下文。 */ second) =>
        {
            // 顺序。
int order = first.contactTime.CompareTo(second.contactTime);
            if (order != 0)
            {
                return order;
            }

            if (first.attacker != null && second.attacker != null)
            {
                order = first.attacker.GetInstanceID().CompareTo(second.attacker.GetInstanceID());
            }

            if (order != 0)
            {
                return order;
            }

            return first.attackSegment.CompareTo(second.attackSegment);
        });
        List<KnifeIncomingAttackContext> contacts = incomingAttackQueue;
        incomingAttackQueue = incomingAttackBatch;
        incomingAttackBatch = contacts;
        foreach (KnifeIncomingAttackContext incomingAttack in contacts)
        {
            ResolveIncomingAttack(incomingAttack);
        }
        contacts.Clear();
    }

    /// <summary>ResolveContactsAfterPose 的业务处理入口；本方法直接依赖 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.EndTransaction(bool)、KnifeCombatModule.Interrupt(bool)、List&lt;KnifeTraversedInterval&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.ToArray()、Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.EndTransaction(bool)、KnifeCombatModule.Interrupt(bool)、List&lt;KnifeTraversedInterval&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.ToArray()、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResolveContactsAfterPose()
    {
        if (!Ready || Time.timeScale <= 0)
        {
            return;
        }

        // 回调可在扣血或断刀时清空原列表，因此在局部快照上完成当前帧。
        List<KnifeTraversedInterval> intervalSnapshot = hitInterval;
        hitInterval = hitIntervalBatch;
        hitIntervalBatch = intervalSnapshot;
        if (intervalSnapshot.Count > 0)
        {
            Physics.SyncTransforms();
        }
        // 小刀破坏时间实例。
        long knifeBreakTimeInstance = -1;
        // interval（间隔）：每轮从间隔快照取得一个元素，仅代表当前这一项。使用位置：作为 KnifeCombatModule.DetectBlade(KnifeTraversedInterval) 的参数、KnifeCombatModule.ResolveContactsAfterPose()。
        foreach (var interval in intervalSnapshot)
        {
            if (!CanExecute || transactionIdValue < 0 || knifeBreakTimeInstance >= 0 && knifeBreakTimeInstance != interval.instance)
            {
                break;
            }

            DetectBlade(interval);
            if (!ValidKnife)
            {
                knifeBreakTimeInstance = interval.instance;
            }
        }

        intervalSnapshot.Clear();

        if (Active && !ValidKnife)
        {
            Interrupt(true);
        }

        if (transactionIdValue >= 0 && (!Active || IsReleasedThrust))
        {
            bool releasedThrust = IsReleasedThrust;
            if (releasedThrust)
            {
                // 先处理已完成的命中区间，再把余下的收肘交给姿态渐出。
                rules.Interrupt();
                rootSlices.Clear();
                currentRootSlice = default;
            }
            EndTransaction(true, releasedThrust);
        }

        if (IsAttack && HasVisibleBlade())
        {
            previousFrameKnifeInstance = CurrentInstance;
            previousFrameKnifeTime = CurrentActionTime;
            previousFrameBladeBase = knifeVisual.TransformPoint(configuration.bladeBaseLocal);
            previousFrameBladeTip = knifeVisual.TransformPoint(configuration.bladeTipLocal);
        }
        else
        {
            previousFrameKnifeInstance = 0;
        }
    }

    /// <summary>ResolveIncomingAttack 的业务处理入口；本方法直接依赖 Action&lt;KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot&gt;.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot)、double.ToString(string)、EnemyCombatModule.ApplyKnifeParryStagger(bool)、float.ToString(string)、HashSet&lt;(int, long, int)&gt;.Add((int, long, int))、HashSet&lt;(int, long, int)&gt;.Clear()、KnifeCombatModule.CommitCurrentAnimation() 等共 22 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingContacts()；下游（含内部回调体） Action&lt;KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot&gt;.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot)、double.ToString(string)、EnemyCombatModule.ApplyKnifeParryStagger(bool)、float.ToString(string)、HashSet&lt;(int, long, int)&gt;.Add((int, long, int))、HashSet&lt;(int, long, int)&gt;.Clear()、KnifeCombatModule.CommitCurrentAnimation() 等共 22 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">传入攻击。</param>
    internal void ResolveIncomingAttack(KnifeIncomingAttackContext incomingAttack)
    {
        if (!incomingAttack.Valid)
        {
            return;
        }

        // 键。
var key = (incomingAttack.attacker.GetInstanceID(), incomingAttack.attackId, incomingAttack.attackSegment);
        if (!resolvedIncomingAttack.Add(key))
        {
            return;
        }

        if (promptCandidate.TryGetValue(key.Item1, out KnifeIncomingAttackContext prompt)
            && prompt.attackId == incomingAttack.attackId && prompt.attackSegment == incomingAttack.attackSegment)
        {
            promptCandidate.Remove(key.Item1);
        }

        if (resolvedIncomingAttack.Count > 512)
        {
            resolvedIncomingAttack.Clear();
            resolvedIncomingAttack.Add(key);
        }

        // 当前小刀招架结果。
KnifeParryOutcome result = KnifeParryOutcome.Failure;
        // 失败。
KnifeParryFailure failure = KnifeParryFailure.Unavailable;
        // 尝试。
KnifeParryAttemptSnapshot attempt = rules.CurrentAttempt;
        if (CanExecute && ValidKnife)
        {
            result = rules.ResolutionParry(incomingAttack.contactTime, IsFront(incomingAttack), incomingAttack.canParry);
            failure = rules.LastParryFailure;
            attempt = rules.LastResolvedAttempt;
        }

        if (recordDiagnostics)
        {
            Record("来袭裁定", result + "；原因=" + failure + "；敌人=" + incomingAttack.attacker.name + "；攻击=" + incomingAttack.attackId + "；段=" + incomingAttack.attackSegment + "；输入序号=" + attempt.sequence + "；按下=" + attempt.pressedAt.ToString("F6") + "；接触=" + incomingAttack.contactTime.ToString("F6") + "；年龄=" + (incomingAttack.contactTime - attempt.pressedAt).ToString("F6") + "；动画秒=" + incomingAttack.animationTime.ToString("F4") + "；依据=" + incomingAttack.contactEvidence);
        }
        if (ParryResolved != null)
        {
            ParryResolved(incomingAttack, result, failure, attempt);
        }

        if (result == KnifeParryOutcome.Failure)
        {
            if (incomingAttack.standingImpact.attacker != null)
            {
                var impact = incomingAttack.standingImpact;
                impact.attacker.BeginStandingRebound(impact);
                player.TakeMeleeContactDamage(incomingAttack.damage, impact.origin, impact.point, impact.normal);
            }
            else
            {
                player.TakeDamageFrom(incomingAttack.damage, incomingAttack.contactPosition, player.transform.position + Vector3.up);
            }

            return;
        }

        // 是否完美。
bool perfect = result == KnifeParryOutcome.Perfect;
        rules.ParryReaction(result, incomingAttack.category);
        CommitCurrentAnimation();
        incomingAttack.attacker.combatModule.ApplyKnifeParryStagger(perfect);
        // 分支选出的配置的完美招架消耗。
int selectedValue22;
        if (perfect)
        {
            selectedValue22 = configuration.perfectParryConsumption;
        }
        else
        {
            selectedValue22 = configuration.normalParryConsumption;
        }

        DeductDurability(selectedValue22);
        // 接触。
Vector3 contact = incomingAttack.contactPosition;
        if (recordDiagnostics)
        {
            Record("弹反反馈", "输入序号=" + attempt.sequence + "；攻击=" + incomingAttack.attackId + "；段=" + incomingAttack.attackSegment + "；等级=" + result + "；开始=" + Time.timeAsDouble.ToString("F6"));
        }
        // 分支选出的配置的完美招架声音。
global::UnityEngine.AudioClip selectedValue23;
        if (perfect)
        {
            selectedValue23 = configuration.perfectParrySound;
        }
        else
        {
            selectedValue23 = configuration.normalParrySound;
        }

        AudioEventData parry = configuration.normalParryEvent;
        if (perfect)
        {
            parry = configuration.perfectParryEvent;
        }

        PlayConfiguredSound(parry, selectedValue23, contact);
        combat.PresentKnifeSparks(contact, incomingAttack.direction, perfect);
        // 命中顿帧。
float hitStop;
        if (perfect)
        {
            hitStop = configuration.perfectHitStop;
        }
        else
        {
            hitStop = configuration.normalHitStop;
        }

        hitStopBegin = Time.timeAsDouble;
        hitStopEnd = Math.Max(hitStopEnd, hitStopBegin + hitStop);
        animationDrive.SetKnifeLocalHitStop(true);
        coordinator.StopCurrentHorizontalMotion();
        // 判空前缓存的传入攻击的攻击者的小刀局部命中顿帧。
global::KnifeHitStop receiverValue25 = incomingAttack.attacker.knifeLocalHitStop;
        if (!global::System.Object.ReferenceEquals(receiverValue25, null))
        {
            receiverValue25.Pause(hitStop);
        }

        // 判空前缓存的玩家的摄像机控制器。
global::RE4CameraController receiverValue26 = player.CameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            // 按原顺序缓存的实参浮点计算。
float argumentValue29;
            if (perfect)
            {
                argumentValue29 = .065f;
            }
            else
            {
                argumentValue29 = .035f;
            }

            // 分支选出的浮点计算。
float selectedValue28;
            if (perfect)
            {
                selectedValue28 = .7f;
            }
            else
            {
                selectedValue28 = .4f;
            }

            receiverValue26.Shake(argumentValue29, selectedValue28);
        }
    }

    /// <summary>DetectBlade 的业务处理入口；本方法直接依赖 Action&lt;KnifeMeleeHitNotification&gt;.Invoke(KnifeMeleeHitNotification)、Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponentInParent&lt;DestructibleWoodenBox&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)、DestructibleWoodenBox.TakeDamage(float, Vector3) 等共 38 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveContactsAfterPose()；下游（含内部回调体） Action&lt;KnifeMeleeHitNotification&gt;.Invoke(KnifeMeleeHitNotification)、Collider.ClosestPoint(Vector3)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponentInParent&lt;DestructibleWoodenBox&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)、DestructibleWoodenBox.TakeDamage(float, Vector3) 等共 38 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interval">间隔。</param>
    internal bool TryReadBladeContact(out Vector3 point)
    {
        point = default;
        if (configuration == null || !HasVisibleBlade())
        {
            return false;
        }
        point = knifeVisual.TransformPoint(Vector3.Lerp(configuration.bladeBaseLocal, configuration.bladeTipLocal, 0.7f));
        return true;
    }

    private bool HasVisibleBlade()
    {
        return knifeVisual != null && knifeVisual.gameObject.activeInHierarchy
            && (weaponVisual == null || weaponVisual.IsCombatKnifeVisible);
    }

    private long thrustDamageInstance;
    internal bool HasConfirmedThrustHit => Active && KnifeCombatRules.IsThrust(CurrentAction)
        && CurrentInstance > 0 && CurrentInstance == thrustDamageInstance;

    private void DetectBlade(KnifeTraversedInterval interval)
    {
        if (interval.instance != CurrentInstance || finishedThrust.Contains(interval.instance) || !HasVisibleBlade())
        {
            return;
        }

        // 只在实际显示的刀刃上采样，混合切换时沿用同一动作的上一帧姿势。
        Vector3 currentBase = knifeVisual.TransformPoint(configuration.bladeBaseLocal);
        Vector3 currentTip = knifeVisual.TransformPoint(configuration.bladeTipLocal);
        Vector3 startBase = currentBase;
        Vector3 startTip = currentTip;
        Vector3 endBase = currentBase;
        Vector3 endTip = currentTip;
        if (previousFrameKnifeInstance == interval.instance && CurrentActionTime > previousFrameKnifeTime)
        {
            double elapsed = CurrentActionTime - previousFrameKnifeTime;
            float from = Mathf.Clamp01((float)((interval.begin - previousFrameKnifeTime) / elapsed));
            float to = Mathf.Clamp01((float)((interval.end - previousFrameKnifeTime) / elapsed));
            startBase = Vector3.Lerp(previousFrameBladeBase, currentBase, from);
            startTip = Vector3.Lerp(previousFrameBladeTip, currentTip, from);
            endBase = Vector3.Lerp(previousFrameBladeBase, currentBase, to);
            endTip = Vector3.Lerp(previousFrameBladeTip, currentTip, to);
        }

        contactList.Clear();
        float travel = Mathf.Max(Vector3.Distance(startBase, endBase), Vector3.Distance(startTip, endTip));
        int segments = Mathf.CeilToInt(travel / 0.025f);
        for (int segment = 0; segment <= segments; segment++)
        {
            float ratio = segment / (float)Mathf.Max(1, segments);
            Vector3 bladeBase = Vector3.Lerp(startBase, endBase, ratio);
            Vector3 bladeTip = Vector3.Lerp(startTip, endTip, ratio);
            int hitCount = Physics.OverlapCapsuleNonAlloc(bladeBase, bladeTip, configuration.bladeDetectRadius,
                bladeOverlaps, configuration.hitMask, QueryTriggerInteraction.Collide);
            while (hitCount == bladeOverlaps.Length)
            {
                System.Array.Resize(ref bladeOverlaps, bladeOverlaps.Length * 2);
                hitCount = Physics.OverlapCapsuleNonAlloc(bladeBase, bladeTip, configuration.bladeDetectRadius,
                    bladeOverlaps, configuration.hitMask, QueryTriggerInteraction.Collide);
            }
            for (int index = 0; index < hitCount; index++)
            {
                Collider collision = bladeOverlaps[index];
                if (collision == null || collision.transform.IsChildOf(transform))
                {
                    continue;
                }

                var middle = (bladeBase + bladeTip) * .5f;
                Vector3 point;
                if (collision is MeshCollider mesh && !mesh.convex && mesh.GetComponentInParent<VillageLegTrap>() != null)
                {
                    var blade = bladeTip - bladeBase;
                    if (mesh.Raycast(new Ray(bladeBase, blade.normalized), out var bladeHit, blade.magnitude))
                    {
                        point = bladeHit.point;
                    }
                    else
                    {
                        point = mesh.bounds.ClosestPoint(middle);
                    }
                }
                else
                {
                    point = collision.ClosestPoint(middle);
                }
                contactList.Add(new ContactCandidate
                {
                    collider = collision,
                    position = point,
                    distance = Vector3.Distance(bladeBase, point),
                    time = interval.begin + (interval.end - interval.begin) * ratio
                });
            }
        }

        // 水平挥刀的烘焙轨迹够不到地面，且快速前刺途中压低准心时也需要由准心射线补充脚边夹子与近距木箱判定。
        if (player.playerCamera != null)
        {
            Transform view = player.playerCamera.transform;
            bool isThrust = KnifeCombatRules.IsThrust(interval.action);
            if (view.forward.y < 0f || isThrust)
            {
                int hitCount = Physics.RaycastNonAlloc(view.position, view.forward, viewFallbackHits, 8f,
                    configuration.hitMask, QueryTriggerInteraction.Ignore);
                RaycastHit closestFallbackHit = default;
                float closestFallbackDistance = float.PositiveInfinity;
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit candidate = viewFallbackHits[hitIndex];
                    if (candidate.collider == null || candidate.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (candidate.distance < closestFallbackDistance)
                    {
                        closestFallbackDistance = candidate.distance;
                        closestFallbackHit = candidate;
                    }
                }

                if (closestFallbackHit.collider != null)
                {
                    bool isTrap = view.forward.y < 0f && closestFallbackHit.collider.GetComponentInParent<VillageLegTrap>() != null;
                    bool isWoodenBox = isThrust && closestFallbackHit.collider.GetComponentInParent<DestructibleWoodenBox>() != null;
                    if (isTrap || isWoodenBox)
                    {
                        Vector3 reach = closestFallbackHit.point - transform.position;
                        Vector3 planarReach = Vector3.ProjectOnPlane(reach, Vector3.up);
                        float maxPlanarRadius = 0.95f;
                        float minVerticalOffset = -0.60f;
                        float maxVerticalOffset = 0.60f;
                        if (isWoodenBox)
                        {
                            maxPlanarRadius = 1.55f;
                            minVerticalOffset = -0.65f;
                            maxVerticalOffset = 1.30f;
                        }

                        if (reach.y >= minVerticalOffset && reach.y <= maxVerticalOffset
                            && planarReach.sqrMagnitude <= maxPlanarRadius * maxPlanarRadius
                            && Vector3.Dot(transform.forward, planarReach) >= 0f)
                        {
                            contactList.Add(new ContactCandidate
                            {
                                collider = closestFallbackHit.collider,
                                position = closestFallbackHit.point,
                                distance = reach.magnitude,
                                time = interval.end
                            });
                        }
                    }
                }
            }
        }
        contactList.Sort(( /* 中文含义：第一个接触候选。 */first, /* 中文含义：第二个接触候选。 */ second) =>
        {
            if (first.time != second.time)
            {
                return first.time.CompareTo(second.time);
            }
            else
            {
                return first.distance.CompareTo(second.distance);
            }
        });
        // contact（接触）：每轮从接触列表取得一个元素，仅代表当前这一项。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DestructibleWoodenBox.TakeDamage(float, Vector3) 的参数、作为 KnifeCombatModule.NoneOcclusion(Vector3, Transform) 的参数 等共 7 处。
foreach (var contact in contactList)
        {
            // 碰撞。
var collision = contact.collider;
            if (collision == null)
            {
                continue;
            }

            // 身体部位。
var bodyPart = collision.GetComponent<EnemyHitbox>();
            // 敌人。
global::VillagerController enemy;
            if (bodyPart != null)
            {
                enemy = bodyPart.Owner;
            }
            else
            {
                enemy = collision.GetComponentInParent<VillagerController>();
            }

            if (bodyPart != null && enemy == null
                || bodyPart == null && enemy != null && enemy.GetComponentInChildren<EnemyHitbox>() != null)
            {
                continue;
            }

            // 公文包值。
var caseValue = collision.GetComponentInParent<DestructibleWoodenBox>();
            var trap = collision.GetComponentInParent<VillageLegTrap>();
            if (trap != null && collision.isTrigger)
                continue;
            // 目标变换节点。
global::UnityEngine.Transform target;
            if (enemy != null)
            {
                target = enemy.transform;
            }
            else
            {
                if (caseValue != null)
                {
                    target = caseValue.transform;
                }
                else
                {
                    target = trap != null ? trap.transform : collision.transform;
                }
            }

            // 目标编号。
int targetId = target.GetInstanceID();
            if (hitTargets.Contains((interval.instance, targetId)) || !NoneOcclusion(contact.position, target))
            {
                continue;
            }

            if (enemy != null)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                hitTargets.Add((interval.instance, targetId));
                // 命中身体部位。
global::HitboxType hitBodyPart;
                if (bodyPart != null)
                {
                    hitBodyPart = bodyPart.hitboxType;
                }
                else
                {
                    hitBodyPart = enemy.ResolveFallbackHitboxType(contact.position);
                }

                AudioEventData impact = ResolveBladeImpact(interval.action, hitBodyPart);
                float previousHealth = enemy.currentHealth;
                var bloodEffect = ChapterOneEffect.SlashBlood;
                if (KnifeCombatRules.IsThrust(interval.action))
                {
                    bloodEffect = ChapterOneEffect.StabBlood;
                }
                float damage = weapon.knife.data.baseDamage * InventoryManager.GetKnifeDamageScale(CurrentKnife());
                DamageResolutionModule.CommitEnemyHit(enemy, hitBodyPart, damage,
                    contact.position, -transform.forward, impact != null, bloodEffect);
                // 判空前缓存的已提交命中。
global::System.Action<global::KnifeMeleeHitNotification> receiverValue37 = CommittedHit;
                if (!global::System.Object.ReferenceEquals(receiverValue37, null))
                {
                    receiverValue37.Invoke(new KnifeMeleeHitNotification(interval.instance, enemy, hitBodyPart, contact.position));
                }

                if (enemy.currentHealth < previousHealth)
                {
                    PlayConfiguredSound(impact, configuration.sliceIntoSound, contact.position);
                }
                if (latestChargeInstance != interval.instance)
                {
                    latestChargeInstance = interval.instance;
                    DeductDurability(configuration.attackConsumption);
                }

                if (recordDiagnostics)
                {
                    Record("刀刃命中", enemy.name + "；部位=" + hitBodyPart + "；刀=" + interval.instance);
                }
                if (KnifeCombatRules.IsThrust(interval.action))
                {
                    if (enemy.currentHealth < previousHealth)
                    {
                        thrustDamageInstance = interval.instance;
                    }
                    finishedThrust.Add(interval.instance);
                    break;
                }
            }
            else if (trap != null)
            {
                hitTargets.Add((interval.instance, targetId));
                if (trap.TryDisarm() && KnifeCombatRules.IsThrust(interval.action))
                {
                    finishedThrust.Add(interval.instance);
                    break;
                }
            }
            else if (caseValue != null)
            {
                hitTargets.Add((interval.instance, targetId));
                float damage = weapon.knife.data.baseDamage * InventoryManager.GetKnifeDamageScale(CurrentKnife());
                caseValue.TakeDamage(damage, contact.position);
                if (!caseValue.IsBroken)
                {
                    ChapterOneEffects.TryPlay(ChapterOneEffect.KnifeWood, contact.position, -transform.forward);
                }
                if (!caseValue.HasDamageAudio)
                {
                    PlaySound(configuration.hardSurfaceSound, contact.position);
                }
                if (KnifeCombatRules.IsThrust(interval.action))
                {
                    finishedThrust.Add(interval.instance);
                    break;
                }
            }
            else if (!collision.isTrigger)
            {
                hitTargets.Add((interval.instance, targetId));
                PlaySound(configuration.hardSurfaceSound, contact.position);
                var surface = FootstepSurface.ResolveCollider(collision);
                if (surface == SurfaceType.Wood)
                {
                    ChapterOneEffects.TryPlay(ChapterOneEffect.KnifeWood, contact.position, -transform.forward);
                }
                else if (surface == SurfaceType.Concrete || surface == SurfaceType.Default)
                {
                    ChapterOneEffects.TryPlay(ChapterOneEffect.KnifeHardSurface, contact.position, -transform.forward);
                }
                if (KnifeCombatRules.IsThrust(interval.action))
                {
                    finishedThrust.Add(interval.instance);
                    break;
                }
            }
        }
    }

    /// <summary>NoneOcclusion 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.SelectAssistTarget()；下游（含内部回调体） Component.GetComponentInParent&lt;VillagerController&gt;()、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="target">目标变换节点。</param>
    private bool NoneOcclusion(Vector3 point, Transform target)
    {
        // 起始点。
Vector3 startPoint = transform.position + Vector3.up * 1.15f;
        // 方向。
Vector3 direction = point - startPoint;
        // hit（命中）：每轮从射线检测全部的返回结果取得一个元素，仅代表当前这一项。使用位置：KnifeCombatModule.NoneOcclusion(Vector3, Transform)。
        RaycastHit[] hits = knifeOcclusionHits.Raycast(startPoint, direction.normalized, direction.magnitude,
            configuration.hitMask, QueryTriggerInteraction.Ignore, out int hitCount);
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = hits[index];
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target))
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<VillagerController>() != null)
            {
                continue;
            }

            if (hit.distance < direction.magnitude - .025f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>DeductDurability 的业务处理入口；本方法直接依赖 InventoryManager.TryConsumeDurability(string, int, out bool)、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） InventoryManager.TryConsumeDurability(string, int, out bool)、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="count">数量。</param>
    private void DeductDurability(int count)
    {
        if (inventory != null && inventory.TryConsumeKnife(CurrentKnife(), count, out bool breakage) && breakage)
        {
            PlayKnifeBreakFeedback();
        }
    }

    private void PlayKnifeBreakFeedback()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null && configuration != null)
        {
            AudioClip fracture = Resources.Load<AudioClip>("UI/KnifeFracture");
            if (fracture == null)
            {
                fracture = configuration.knifeBreakSound;
            }
            audio.PlaySFX(fracture);
        }
        if (recordDiagnostics)
        {
            Record("断刀", "耐久已归零");
        }
    }

    /// <summary>PlaySound 的业务处理入口；本方法直接依赖 AudioManager.PlaySFX3D(AudioClip, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DeductDurability(int)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） AudioManager.PlaySFX3D(AudioClip, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sound">声音。</param>
    /// <param name="position">位置。</param>
    private AudioEventData ResolveBladeImpact(KnifeAction action, HitboxType bodyPart)
    {
        bool head = bodyPart == HitboxType.Head;
        if (KnifeCombatRules.IsThrust(action))
        {
            if (head)
            {
                return configuration.thrustHeadImpactEvent;
            }

            return configuration.thrustBodyImpactEvent;
        }

        if (head)
        {
            return configuration.slashHeadImpactEvent;
        }

        return configuration.slashBodyImpactEvent;
    }

    private static void PlayConfiguredSound(AudioEventData sound, AudioClip fallback, Vector3 position)
    {
        if (sound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3D(sound, position);
        }
        else
        {
            PlaySound(fallback, position);
        }
    }

    private static void PlaySound(AudioClip sound, Vector3 position)
    {
        if (sound != null)
        {
            // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue38 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue38, null))
            {
                receiverValue38.PlaySFX3D(sound, position);
            }
        }
    }

    /// <summary>Record 的业务处理入口；本方法直接依赖 double.ToString(string, IFormatProvider)、Queue&lt;string&gt;.Dequeue()、Queue&lt;string&gt;.Enqueue(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DeductDurability(int)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、KnifeCombatModule.Update()；下游（含内部回调体） double.ToString(string, IFormatProvider)、Queue&lt;string&gt;.Dequeue()、Queue&lt;string&gt;.Enqueue(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    /// <param name="content">内容。</param>
    private void Record(string category, string content)
    {
        if (!recordDiagnostics)
        {
            return;
        }

        diagnosticSequence++;
        log.Enqueue(Time.timeAsDouble.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "｜" + category + "｜" + content);
        while (log.Count > 512)
        {
            log.Dequeue();
        }
    }
}
