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
    /// <summary>中文含义：攻击者。使用位置：作为 Dictionary&lt;int, KnifeIncomingAttackContext&gt;.this[int] 的参数、作为 int.CompareTo(int) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数 等共 10 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击者")]
    [UnityEngine.InspectorName("攻击者")]
    public VillagerController attacker;
    /// <summary>中文含义：攻击编号。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext) 等共 4 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击编号")]
    [UnityEngine.InspectorName("攻击编号")]
    public long attackId;
    /// <summary>中文含义：攻击段。使用位置：作为 int.CompareTo(int) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 List&lt;KnifeIncomingAttackContext&gt;.Sort(Comparison&lt;KnifeIncomingAttackContext&gt;) 的参数 等共 5 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("攻击段")]
    [UnityEngine.InspectorName("攻击段")]
    public int attackSegment;
    /// <summary>中文含义：接触时间。使用位置：作为 double.CompareTo(double) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.ResolutionParry(double, bool, bool) 的参数 等共 6 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("接触时间")]
    [UnityEngine.InspectorName("接触时间")]
    public double contactTime;
    /// <summary>中文含义：预计接触时间。使用位置：EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.LateUpdate()、KnifeCombatModule.CurrentPromptIncomingAttack.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("预计接触时间")]
    [UnityEngine.InspectorName("预计接触时间")]
    public double expectedContactTime;
    /// <summary>中文含义：接触位置。使用位置：作为 LeonController.TakeDamageFrom(float, Vector3, Vector3) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("接触位置")]
    [UnityEngine.InspectorName("接触位置")]
    public Vector3 contactPosition;
    /// <summary>中文含义：方向。使用位置：作为 LeonCombat.PresentKnifeSparks(Vector3, Vector3, bool) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("方向")]
    [UnityEngine.InspectorName("方向")]
    public Vector3 direction;
    /// <summary>中文含义：伤害。使用位置：作为 LeonController.TakeDamageFrom(float, Vector3, Vector3) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("伤害")]
    [UnityEngine.InspectorName("伤害")]
    public float damage;
    /// <summary>中文含义：是否可招架。使用位置：作为 KnifeCombatRules.ResolutionParry(double, bool, bool) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)、KnifeCombatModule.CurrentPromptIncomingAttack.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("可招架")]
    [UnityEngine.InspectorName("可招架")]
    public bool canParry;
    /// <summary>中文含义：类别。使用位置：作为 KnifeCombatRules.ParryReaction(KnifeParryOutcome, KnifeIncomingAttackCategory) 的参数、EnemyCombatModule.CreateKnifeIncomingAttack(int, float, Vector3, Vector3)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("类别")]
    [UnityEngine.InspectorName("类别")]
    public KnifeIncomingAttackCategory category;
    /// <summary>中文含义：攻击原点。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public Vector3 attackOrigin;
    /// <summary>中文含义：是否具有攻击原点。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public bool hasAttackOrigin;
    /// <summary>中文含义：玩家位置所在接触。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public Vector3 playerPositionAtContact;
    /// <summary>中文含义：是否具有玩家位置所在接触。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public bool hasPlayerPositionAtContact;
    /// <summary>中文含义：玩家前向所在接触。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public Vector3 playerForwardAtContact;
    /// <summary>中文含义：是否具有玩家朝向所在接触。使用位置：EnemyCombatModule.LateUpdate()、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</summary>
    public bool hasPlayerFacingAtContact;
    /// <summary>中文含义：segmentBegin 表示“线段开始”；segmentEnd 表示“线段结束”；animationTime 表示“动画时间”。使用位置：EnemyCombatModule.LateUpdate()。；使用位置：EnemyCombatModule.LateUpdate()。；使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、EnemyCombatModule.LateUpdate()。</summary>
    public float segmentBegin, segmentEnd, animationTime;
    /// <summary>中文含义：接触判定证据。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、EnemyCombatModule.LateUpdate()。</summary>
    public string contactEvidence;
    internal EnemyCombatModule.StandingImpact standingImpact;
    /// <summary>中文含义：是否有效。使用位置：KnifeCombatModule.CurrentPromptIncomingAttack.get、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。</summary>
    public bool Valid => attacker != null && attacker.gameObject.activeInHierarchy && !attacker.IsDead
        && attacker.combatModule != null && attacker.combatModule.isActiveAndEnabled
        && attacker.combatModule.KnifeIncomingAttackId == attackId && !attacker.combatModule.ReadAttackFinished
        && (attacker.CurrentState == VillagerState.Attack || attacker.CurrentState == VillagerState.CrawlAttack);
}

/// <summary>玩家控制、动作与运动的结构化数据 KnifeMeleeHitNotification；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀近战命中通知")]
public readonly struct KnifeMeleeHitNotification
{
    /// <summary>中文含义：动作实例。只读字段，初始化后不由普通写入口替换。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</summary>
    public readonly long actionInstance;
    /// <summary>中文含义：攻击线段。只读字段，初始化后不由普通写入口替换。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</summary>
    public readonly int attackSegment;
    /// <summary>中文含义：目标村民敌人控制器。只读字段，初始化后不由普通写入口替换。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</summary>
    public readonly VillagerController target;
    /// <summary>中文含义：身体部位。只读字段，初始化后不由普通写入口替换。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</summary>
    public readonly HitboxType bodyPart;
    /// <summary>中文含义：位置。只读字段，初始化后不由普通写入口替换。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</summary>
    public readonly Vector3 position;
    /// <summary>构造并初始化 KnifeMeleeHitNotification 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DetectBlade(KnifeTraversedInterval)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">中文含义：实例。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</param>
    /// <param name="target">中文含义：目标村民敌人控制器。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</param>
    /// <param name="bodyPart">中文含义：身体部位。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3)。</param>
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
    /// <summary>中文含义：玩家。使用位置：作为 LeonController.TakeDamageFrom(float, Vector3, Vector3) 的参数、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.ActionSwitch(KnifeAction, long, double) 等共 8 处。</summary>
    [Header("组件引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("玩家")]
    [UnityEngine.InspectorName("玩家")]
    public LeonController player;
    /// <summary>中文含义：战斗。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController) 等共 8 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("战斗")]
    [UnityEngine.InspectorName("战斗")]
    public LeonCombat combat;
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.BindingEvent()、KnifeCombatModule.CommitCurrentAnimation() 等共 10 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("协调器")]
    [UnityEngine.InspectorName("协调器")]
    public LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：动画驱动。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext) 等共 5 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动画驱动")]
    [UnityEngine.InspectorName("动画驱动")]
    public LeonAnimatorDriver animationDrive;
    /// <summary>中文含义：武器。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DestructibleWoodenBox.TakeDamage(float, Vector3) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 7 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("武器")]
    [UnityEngine.InspectorName("武器")]
    public LeonWeaponHandler weapon;
    /// <summary>中文含义：武器视觉。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.EndTransaction(bool) 等共 5 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("武器视觉")]
    [UnityEngine.InspectorName("武器视觉")]
    public LeonWeaponVisualController weaponVisual;
    /// <summary>中文含义：手部约束。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.PrepareTransaction(bool)、KnifeCombatModule.Start()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("手部约束")]
    [UnityEngine.InspectorName("手部约束")]
    public LeonAnimRig handConstraint;
    /// <summary>中文含义：背包。使用位置：KnifeCombatModule.DeductDurability(int)、KnifeCombatModule.Start()、KnifeCombatModule.ValidKnife.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("背包")]
    [UnityEngine.InspectorName("背包")]
    public InventoryManager inventory;
    /// <summary>中文含义：刀具。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveContactsAfterPose() 等共 4 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("刀具")]
    [UnityEngine.InspectorName("刀具")]
    public Transform knifeVisual;
    /// <summary>中文含义：配置。使用位置：作为 KnifeCombatModule.DeductDurability(int) 的参数、作为 KnifeCombatModule.PlaySound(AudioClip, Vector3) 的参数、作为 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 的参数 等共 23 处。</summary>
    [Header("战斗配置")]
    [UnityEngine.Serialization.FormerlySerializedAs("配置")]
    [UnityEngine.InspectorName("配置")]
    public KnifeCombatConfiguration configuration;
    /// <summary>中文含义：是否记录诊断。使用位置：KnifeCombatModule.Record(string, string)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("记录诊断")]
    [UnityEngine.InspectorName("记录诊断")]
    public bool recordDiagnostics = true;
    /// <summary>中文含义：规则。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、KnifeCombatModule.Active.get、KnifeCombatModule.AdvanceTo(double) 等共 19 处。</summary>
    private KnifeCombatRules rules;
    /// <summary>中文含义：事务编号值。使用位置：作为 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float) 的参数、KnifeCombatModule.ActionInterrupted(AnimationActionTransaction)、KnifeCombatModule.EndTransaction(bool) 等共 5 处。</summary>
    private int transactionIdValue = -1;
    private GridItem transactionKnife;
    /// <summary>中文含义：isEnd 表示“是否结束”；bound 表示“是否已绑定”；attackKeyHold 表示“是否攻击键保持”。使用位置：KnifeCombatModule.Interrupt(bool)。；使用位置：KnifeCombatModule.BindingEvent()、KnifeCombatModule.OnDisable()。；使用位置：KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.Update()。</summary>
    private bool isEnd, bound, attackKeyHold;
    /// <summary>中文含义：是否等待中提交动画。使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.CommitCurrentAnimation()。</summary>
    private bool waitingCommitAnimation;
    private bool lastKnifeAttackWasThrust;
    /// <summary>中文含义：advancedTime 表示“推进后的时间”；hitStopBegin 表示“命中顿帧开始”；hitStopEnd 表示“命中顿帧结束”。使用位置：作为 Math.Max(double, double) 的参数、KnifeCombatModule.AdvanceTo(double)、KnifeCombatModule.OnEnable() 等共 5 处。；使用位置：作为 Math.Max(double, double) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。；使用位置：作为 Math.Max(double, double) 的参数、作为 Math.Min(double, double) 的参数、KnifeCombatModule.Interrupt(bool) 等共 5 处。</summary>
    private double advancedTime, hitStopBegin, hitStopEnd;
    /// <summary>中文含义：latestChargeInstance 表示“最新蓄力实例”；latestWhiffInstance 表示“最新挥空实例”。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。；使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)。</summary>
    private long latestChargeInstance = -1, latestWhiffInstance = -1;
    private bool pendingSwingSound;
    /// <summary>中文含义：结束突刺。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。</summary>
    private readonly HashSet<long> finishedThrust = new HashSet<long>();
    /// <summary>中文含义：前一次帧小刀实例。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.OnDisable()、KnifeCombatModule.ResolveContactsAfterPose()。</summary>
    private long previousFrameKnifeInstance;
    /// <summary>中文含义：前一次帧小刀时间。使用位置：作为 Mathf.Clamp01(float) 的参数、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveContactsAfterPose()。</summary>
    private double previousFrameKnifeTime;
    /// <summary>中文含义：previousFrameBladeBase 表示“前一次帧刀刃基础”；previousFrameBladeTip 表示“前一次帧刀刃尖端”。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifeCombatModule.ResolveContactsAfterPose()。；使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifeCombatModule.ResolveContactsAfterPose()。</summary>
    private Vector3 previousFrameBladeBase, previousFrameBladeTip;
    /// <summary>中文含义：startupOrientation 表示“启动朝向”；assistTargetOrientation 表示“辅助目标朝向”。使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数、KnifeCombatModule.SelectAssistTarget()。；使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数、KnifeCombatModule.SelectAssistTarget()。</summary>
    private Quaternion startupOrientation, assistTargetOrientation;
    /// <summary>中文含义：是否具有校正量角度。使用位置：作为 Mathf.Min(float, float) 的参数、KnifeCombatModule.SelectAssistTarget()、KnifeCombatModule.UpdateWeakSteering()。</summary>
    private float hasCorrectionAngle;
    /// <summary>中文含义：输入队列。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.EnqueueValue(int, bool, double)、KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.Update()。</summary>
    private readonly List<InputRecord> inputQueue = new List<InputRecord>();
    /// <summary>中文含义：命中间隔。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.ResolveContactsAfterPose()、KnifeCombatModule.Start()。</summary>
    private struct RootSlice
    {
        public Vector3 velocity;
        public float duration;
    }

    private readonly Queue<RootSlice> rootSlices = new();
    private RootSlice currentRootSlice;

    private readonly List<KnifeTraversedInterval> hitInterval = new List<KnifeTraversedInterval>();
    /// <summary>中文含义：传入攻击队列。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.CommitIncomingAttack(KnifeIncomingAttackContext)、KnifeCombatModule.OnDisable()、KnifeCombatModule.ResolveIncomingContacts()。</summary>
    private readonly List<KnifeIncomingAttackContext> incomingAttackQueue = new List<KnifeIncomingAttackContext>();
    /// <summary>中文含义：提示候选。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.CloseIncomingAttackPrompt(VillagerController)、KnifeCombatModule.CurrentPromptIncomingAttack.get、KnifeCombatModule.OnDisable() 等共 4 处。</summary>
    private readonly Dictionary<int, KnifeIncomingAttackContext> promptCandidate = new Dictionary<int, KnifeIncomingAttackContext>();
    private (KnifeParryPromptKind kind, int enemyId, long attackId, int segment) lastParryPrompt;
    /// <summary>中文含义：命中目标列表。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。</summary>
    private readonly HashSet<(long, int)> hitTargets = new HashSet<(long, int)>();
    /// <summary>中文含义：解算后的传入攻击。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.OnDisable()、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。</summary>
    private readonly HashSet<(int, long, int)> resolvedIncomingAttack = new HashSet<(int, long, int)>();
    /// <summary>中文含义：日志。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.ReadDiagnostics()、KnifeCombatModule.ReadDiagnosticsAfter(long)、KnifeCombatModule.Record(string, string)。</summary>
    private readonly Queue<string> log = new Queue<string>();
    /// <summary>中文含义：接触列表。只读字段，初始化后不由普通写入口替换。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。</summary>
    private readonly List<ContactCandidate> contactList = new List<ContactCandidate>();
    /// <summary>玩家控制、动作与运动的结构化数据 InputRecord；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗模块+输入记录")]
    private struct InputRecord
    {
        /// <summary>中文含义：种类。使用位置：作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数、作为 List&lt;KnifeCombatModule.InputRecord&gt;.Sort(Comparison&lt;KnifeCombatModule.InputRecord&gt;) 的参数、KnifeCombatModule.Update()。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("种类")]
        [UnityEngine.InspectorName("种类")]
        public int kind;
        /// <summary>中文含义：是否按住。使用位置：作为 KnifeCombatRules.GuardInput(bool, double, long) 的参数、作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数、KnifeCombatModule.Update()。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("按住")]
        [UnityEngine.InspectorName("按住")]
        public bool held;
        /// <summary>中文含义：时间。使用位置：作为 double.CompareTo(double) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.GuardInput(bool, double, long) 的参数 等共 7 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("时间")]
        [UnityEngine.InspectorName("时间")]
        public double time;
        /// <summary>中文含义：序号。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.GuardInput(bool, double, long) 的参数、作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("序号")]
        [UnityEngine.InspectorName("序号")]
        public long sequence;
    }

    /// <summary>玩家控制、动作与运动的结构化数据 ContactCandidate；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀战斗模块+接触候选")]
    private struct ContactCandidate
    {
        /// <summary>中文含义：碰撞体。使用位置：作为 List&lt;KnifeCombatModule.ContactCandidate&gt;.Add(KnifeCombatModule.ContactCandidate) 的参数、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("碰撞体")]
        [UnityEngine.InspectorName("碰撞体")]
        public Collider collider;
        /// <summary>中文含义：位置。使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 DestructibleWoodenBox.TakeDamage(float, Vector3) 的参数、作为 KnifeCombatModule.NoneOcclusion(Vector3, Transform) 的参数 等共 7 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("位置")]
        [UnityEngine.InspectorName("位置")]
        public Vector3 position;
        /// <summary>中文含义：距离。使用位置：作为 float.CompareTo(float) 的参数、作为 List&lt;KnifeCombatModule.ContactCandidate&gt;.Add(KnifeCombatModule.ContactCandidate) 的参数、作为 List&lt;KnifeCombatModule.ContactCandidate&gt;.Sort(Comparison&lt;KnifeCombatModule.ContactCandidate&gt;) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("距离")]
        [UnityEngine.InspectorName("距离")]
        public float distance;
        /// <summary>中文含义：时间。使用位置：作为 double.CompareTo(double) 的参数、作为 List&lt;KnifeCombatModule.ContactCandidate&gt;.Add(KnifeCombatModule.ContactCandidate) 的参数、作为 List&lt;KnifeCombatModule.ContactCandidate&gt;.Sort(Comparison&lt;KnifeCombatModule.ContactCandidate&gt;) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("时间")]
        [UnityEngine.InspectorName("时间")]
        public double time;
    }

    /// <summary>中文含义：输入序号。使用位置：作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数。</summary>
    private long inputSequence;
    /// <summary>中文含义：诊断序号。使用位置：作为 Math.Max(long, long) 的参数、KnifeCombatModule.DiagnosticSequence.get、KnifeCombatModule.Record(string, string)。</summary>
    private long diagnosticSequence;
    /// <summary>中文含义：是否就绪。使用位置：EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.LateUpdate()、EnemyCombatModule.TryApplyAttackHit(int) 等共 13 处。</summary>
    public bool Ready => rules != null && configuration != null && coordinator != null && player != null;
    /// <summary>中文含义：是否生效。使用位置：KnifeCombatModule.CanConnectMeleeFinisher.get、KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.IsAttack.get 等共 14 处。</summary>
    public bool Active => Ready && rules.Active;
    /// <summary>中文含义：是否攻击。使用位置：KnifeCombatModule.MovementRatio.get、KnifeCombatModule.ResolveContactsAfterPose()、KnifeCombatModule.UpdateWeakSteering() 等共 6 处。</summary>
    public bool IsAttack => Active && rules.IsAttack;
    /// <summary>中文含义：是否命中顿帧。使用位置：作为 LeonAnimatorDriver.SetKnifeLocalHitStop(bool) 的参数、KnifeCombatModule.MovementRatio.get、KnifeCombatModule.UpdateWeakSteering() 等共 5 处。</summary>
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
    private bool IsReleasedThrust => lastKnifeAttackWasThrust && !GuardHold && CurrentAction == KnifeAction.LowerGuard;
    internal bool IsFinishingThrust => Active && lastKnifeAttackWasThrust
        && (KnifeCombatRules.IsThrust(CurrentAction) || CurrentAction == KnifeAction.ThrustRecovery || IsReleasedThrust);
    public bool IsGuardAiming => Active && GuardHold && CanExecute
        && (CurrentAction == KnifeAction.EnterGuard || CurrentAction == KnifeAction.HoldGuard
            || CurrentAction == KnifeAction.ThrustOne || CurrentAction == KnifeAction.ThrustTwo
            || CurrentAction == KnifeAction.ThrustRecovery);

    /// <summary>中文含义：是否可以招架当前时刻。使用位置：LeonCombat.IsParrying.get。</summary>
    public bool CanParryNow
    {
        get
        {
            return Ready && CanExecute && ValidKnife && rules.HasGuardEligibility(Time.timeAsDouble);
        }
    }

    /// <summary>中文含义：是否可以连接近战终结技。使用位置：LeonQTECoordinator.CanQueryBodyTechniquePrompt()。</summary>
    public bool CanConnectMeleeFinisher => Active && rules.CanHandoff;

    /// <summary>中文含义：当前动作。使用位置：作为 KnifeCombatConfiguration.Find(KnifeAction) 的参数、作为 KnifeCombatRules.Query(KnifeAction) 的参数、KnifeCombatModule.CommitCurrentAnimation()。</summary>
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

    /// <summary>中文含义：当前实例。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveContactsAfterPose()。</summary>
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

    /// <summary>中文含义：当前动作时间。使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、作为 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float) 的参数、作为 Mathf.Clamp01(float) 的参数 等共 8 处。</summary>
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

    /// <summary>中文含义：移动比例。使用位置：LeonLocomotion.HandleMovement()。</summary>
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
    /// <param name="sequence">中文含义：序号。使用位置：作为 Math.Max(long, long) 的参数。</param>
    public string[] ReadDiagnosticsAfter(long sequence)
    {
        // snapshot（快照）：保存 Queue<string>.ToArray() 的返回结果，供当前步骤使用。使用位置：作为 Array.Copy(Array, int, Array, int, int) 的参数、作为 Math.Max(long, long) 的参数、作为 Math.Min(long, long) 的参数 等共 4 处。
string[] snapshot = log.ToArray();
        // skip（跳过）：使用位置：作为 Array.Copy(Array, int, Array, int, int) 的参数、KnifeCombatModule.ReadDiagnosticsAfter(long)。
int skip = (int)Math.Min(snapshot.Length, Math.Max(0L, sequence - diagnosticSequence + snapshot.Length));
        // result（当前文本数组）：使用位置：作为 Array.Copy(Array, int, Array, int, int) 的参数、KnifeCombatModule.ReadDiagnosticsAfter(long)。
string[] result = new string[snapshot.Length - skip];
        Array.Copy(snapshot, skip, result, 0, result.Length);
        return result;
    }

    /// <summary>中文含义：已提交命中。事件通信成员 CommittedHit，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。</summary>
    public event Action<KnifeMeleeHitNotification> CommittedHit;
    /// <summary>中文含义：招架解算后的。事件通信成员 ParryResolved，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。</summary>
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

    /// <summary>中文含义：是否有效小刀。使用位置：KnifeCombatModule.CanParryNow.get、KnifeCombatModule.CurrentPromptIncomingAttack.get、KnifeCombatModule.ResolveContactsAfterPose() 等共 5 处。</summary>
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
    /// <param name="action">中文含义：动作。使用位置：KnifeCombatModule.ActionInterrupted(AnimationActionTransaction)。</param>
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
    /// <param name="held">中文含义：是否保持按住。使用位置：作为 KnifeCombatModule.EnqueueValue(int, bool, double) 的参数。</param>
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatModule.EnqueueValue(int, bool, double) 的参数。</param>
    public void CommitGuard(bool held, double time) => EnqueueValue(0, held, time);

    internal bool CanResumeHeldGuard => Ready && CanExecute && ValidKnife && !GuardHold
        && (transactionIdValue >= 0 || coordinator.CanPrepareKnifeGuard());

    internal void CommitHeldGuard(double time) => EnqueueValue(3, true, time);
    /// <summary>CommitAttack 的业务处理入口；本方法直接依赖 KnifeCombatModule.EnqueueValue(int, bool, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonCombat.CommitAttackInput(double)；下游（含内部回调体） KnifeCombatModule.EnqueueValue(int, bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatModule.EnqueueValue(int, bool, double) 的参数。</param>
    public void CommitAttack(double time) => EnqueueValue(1, true, time);
    /// <summary>CommitAttackReleased 的业务处理入口；本方法直接依赖 KnifeCombatModule.EnqueueValue(int, bool, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CaptureRuntime(KnifeCombatModule)、LeonCombat.CommitAttackReleased(double)；下游（含内部回调体） KnifeCombatModule.EnqueueValue(int, bool, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatModule.EnqueueValue(int, bool, double) 的参数。</param>
    public void CommitAttackReleased(double time) => EnqueueValue(2, false, time);
    /// <summary>EnqueueValue 的业务处理入口；本方法直接依赖 KnifeCombatModule.InputRecord.InputRecord()、List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CommitAttack(double)、KnifeCombatModule.CommitAttackReleased(double)、KnifeCombatModule.CommitGuard(bool, double)；下游（含内部回调体） KnifeCombatModule.InputRecord.InputRecord()、List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数、KnifeCombatModule.EnqueueValue(int, bool, double)。</param>
    /// <param name="held">中文含义：是否保持按住。使用位置：作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数、KnifeCombatModule.EnqueueValue(int, bool, double)。</param>
    /// <param name="time">中文含义：时间。使用位置：作为 List&lt;KnifeCombatModule.InputRecord&gt;.Add(KnifeCombatModule.InputRecord) 的参数。</param>
    private void EnqueueValue(int kind, bool held, double time)
    {
        if (!Ready || !isActiveAndEnabled || Time.timeScale <= 0 && (kind == 1 || held))
        {
            return;
        }

        inputQueue.Add(new InputRecord { kind = kind, held = held, time = time, sequence = ++inputSequence });
    }

    /// <summary>中文含义：是否可以执行。使用位置：作为 LeonCombat.EndTemporaryKnife(bool) 的参数、KnifeCombatModule.CanParryNow.get、KnifeCombatModule.CurrentPromptIncomingAttack.get 等共 7 处。</summary>
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
            // order（顺序）：保存 double.CompareTo(double) 的返回结果，供当前步骤使用。使用位置：作为 List<KnifeCombatModule.InputRecord>.Sort(Comparison<KnifeCombatModule.InputRecord>) 的参数。
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
        // inputSnapshot（输入快照）：保存 List<KnifeCombatModule.InputRecord>.ToArray() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.Update()。
var inputSnapshot = inputQueue.ToArray();
        inputQueue.Clear();
        // input（输入数据）：每轮从输入快照取得一个元素，仅代表当前这一项。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.GuardInput(bool, double, long) 的参数、作为 KnifeCombatRules.HasGuardEligibility(double) 的参数 等共 5 处。
foreach (var input in inputSnapshot)
        {
            // timestamp（时间戳）：保存 Math.Min(double, double) 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatModule.AdvanceTo(double) 的参数。
double timestamp = Math.Min(Time.timeAsDouble, Math.Max(advancedTime, input.time));
            AdvanceTo(timestamp);
            if (input.kind == 0)
            {
                if (input.held && (!ValidKnife || !PrepareTransaction(true)))
                {
                    Record("防守拒绝", "无可用刀或动作事务不允许；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6"));
                    continue;
                }

                rules.GuardInput(input.held, input.time, input.sequence);
                // edge（边缘）：使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、KnifeCombatModule.Update()。
string edge = "松开";
                if (input.held)
                {
                    edge = "按下";
                }

                Record("防守输入", edge + "；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6") + "；到期=" + rules.CurrentAttempt.expiresAt.ToString("F6") + "；阶段=" + rules.CurrentAction + "；此刻资格=" + rules.HasGuardEligibility(input.time));
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
                // argumentValue9（按原顺序缓存的实参文字“攻击输入”）：从文字“攻击输入”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数。
string argumentValue9 = "攻击输入";
                // selectedValue8（分支选出的文字“缓存下一刀”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、KnifeCombatModule.Update()。
string selectedValue8;
                if (rules.HasCache)
                {
                    selectedValue8 = "缓存下一刀";
                }
                else
                {
                    selectedValue8 = "执行单刀";
                }

                Record(argumentValue9, (selectedValue8) + "；序号=" + input.sequence + "；输入时刻=" + input.time.ToString("F6"));
            }
        }

        AdvanceTo(Time.timeAsDouble);
        CommitCurrentAnimation();
        UpdateWeakSteering();
    }

    /// <summary>AdvanceTo 的业务处理入口；本方法直接依赖 KnifeCombatRules.Advance(double, double)、Math.Max(double, double)、Math.Min(double, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()；下游（含内部回调体） KnifeCombatRules.Advance(double, double)、Math.Max(double, double)、Math.Min(double, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">中文含义：时间。使用位置：作为 KnifeCombatRules.Advance(double, double) 的参数、作为 Math.Max(double, double) 的参数、作为 Math.Min(double, double) 的参数 等共 4 处。</param>
    private void AdvanceTo(double time)
    {
        // delta（增量）：保存 Math.Max(double, double) 的返回结果，供当前步骤使用。使用位置：作为 Math.Max(double, double) 的参数。
double delta = Math.Max(0, time - advancedTime);
        // freeze（冻结）：保存 Math.Max(double, double) 的返回结果，供当前步骤使用。使用位置：作为 Math.Max(double, double) 的参数。
double freeze = Math.Max(0, Math.Min(time, hitStopEnd) - Math.Max(advancedTime, hitStopBegin));
        advancedTime = time;
        rules.Advance(Math.Max(0, delta - freeze), time);
    }

    /// <summary>PrepareTransaction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.BeginTemporaryKnife()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Update()；下游（含内部回调体） LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimRig.SetIKMode(IKMode)、LeonCombat.BeginTemporaryKnife()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="guard">中文含义：是否防守。使用位置：KnifeCombatModule.PrepareTransaction(bool)。</param>
    internal void ApplyCameraHeading(float heading, float pitch)
    {
        cameraHeading = heading;
        hasCameraHeading = true;
        if (IsHoldingGuardPose && !IsHitStop && !player.IsolateObserveCameraSteering && Time.deltaTime > 0f)
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
            GuardViewPitch = pitch;
            GuardViewFrame = Time.frameCount;
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

        // newTransaction（新事务）：保存 LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.PrepareTransaction(bool)。
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
        // receiverValue10（判空前缓存的手部约束）：从手部约束读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.PrepareTransaction(bool)。
global::LeonAnimRig receiverValue10 = handConstraint;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.SetIKMode(IKMode.None);
        }

        return true;
    }

    /// <summary>ActionSwitch 的业务处理入口；本方法直接依赖 Enum.ToString()、HashSet&lt;(long, int)&gt;.Clear()、HashSet&lt;long&gt;.Clear()、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)、KnifeCombatModule.SelectAssistTarget()、KnifeCombatRules.IsAttackValue(KnifeAction) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Enum.ToString()、HashSet&lt;(long, int)&gt;.Clear()、HashSet&lt;long&gt;.Clear()、KnifeCombatModule.PlaySound(AudioClip, Vector3)、KnifeCombatModule.Record(string, string)、KnifeCombatModule.SelectAssistTarget()、KnifeCombatRules.IsAttackValue(KnifeAction) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.IsAttackValue(KnifeAction) 的参数、KnifeCombatModule.ActionSwitch(KnifeAction, long, double)。</param>
    /// <param name="instance">中文含义：实例。使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)。</param>
    /// <param name="offset">中文含义：偏移量。</param>
    private void ActionSwitch(KnifeAction action, long instance, double offset)
    {
        if (action != KnifeAction.HoldGuard)
        {
            guardEntryTurning = false;
        }
        Record("动作切换", action.ToString());
        pendingSwingSound = false;
        waitingCommitAnimation = action != KnifeAction.None;
        if (action == KnifeAction.None)
        {
            return;
        }

        // callReceiver12（调用前缓存的玩家）：从玩家读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：KnifeCombatModule.ActionSwitch(KnifeAction, long, double)。
global::LeonController callReceiver12 = player;
        // selectedValue11（分支选出的里昂状态的近战攻击中）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonController.ChangeState(LeonState) 的参数、KnifeCombatModule.ActionSwitch(KnifeAction, long, double)。
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
        // action（动作）：使用位置：作为 Debug.LogError(object, Object) 的参数、作为 KnifeCombatConfiguration.Find(KnifeAction) 的参数、作为 KnifeCombatRules.IsReaction(KnifeAction) 的参数 等共 4 处。
var action = CurrentAction;
        // clip（动画片段）：保存 KnifeCombatConfiguration.Find(KnifeAction) 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float) 的参数。
var clip = configuration.Find(action);
        // blend（混合）：使用位置：作为 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float) 的参数、作为 LeonWeaponVisualController.SetCombatKnifeGripPose(KnifeCombatConfiguration, bool, float) 的参数、KnifeCombatModule.CommitCurrentAnimation()。
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
    /// <param name="restoreAim">中文含义：是否恢复瞄准。使用位置：作为 LeonCombat.EndTemporaryKnife(bool) 的参数。</param>
    private void EndTransaction(bool restoreAim, bool releasedThrust = false)
    {
        if (transactionIdValue < 0)
        {
            return;
        }

        // id（编号）：使用位置：作为 LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int) 的参数。
int id = transactionIdValue;
        transactionIdValue = -1;
        transactionKnife = null;
        // receiverValue14（判空前缓存的当前玩家动画动作协调器）：从动作协调组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.EndTransaction(bool)。
global::LeonAnimationActionCoordinator receiverValue14 = coordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.EndIndependentKnifeTransaction(id, releasedThrust);
        }

        // receiverValue15（判空前缓存的武器视觉）：从武器视觉表现读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.EndTransaction(bool)。
global::LeonWeaponVisualController receiverValue15 = weaponVisual;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.EndCombatKnifeGripPose();
        }

        // receiverValue16（判空前缓存的武器视觉）：从武器视觉表现读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.EndTransaction(bool)。
global::LeonWeaponVisualController receiverValue16 = weaponVisual;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            receiverValue16.SetKnifeAppearance(CurrentKnife());
            receiverValue16.SetKnifeModeVisible(combat != null && combat.IsKnifeMode);
        }

        // receiverValue17（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.EndTransaction(bool)。
global::LeonCombat receiverValue17 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.EndTemporaryKnife(restoreAim && CanExecute);
        }
    }

    /// <summary>Interrupt 的业务处理入口；本方法直接依赖 KnifeCombatModule.EndTransaction(bool)、KnifeCombatRules.Interrupt()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)、List&lt;KnifeCombatModule.InputRecord&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.Clear()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionInterrupted(AnimationActionTransaction)、KnifeCombatModule.CommitCurrentAnimation()、KnifeCombatModule.DeathInterrupt()、KnifeCombatModule.OnDisable()、KnifeCombatModule.ResolveContactsAfterPose() 等共 11 处；下游（含内部回调体） KnifeCombatModule.EndTransaction(bool)、KnifeCombatRules.Interrupt()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)、List&lt;KnifeCombatModule.InputRecord&gt;.Clear()、List&lt;KnifeTraversedInterval&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoreAim">中文含义：是否恢复瞄准。使用位置：作为 KnifeCombatModule.EndTransaction(bool) 的参数。</param>
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
        // receiverValue18（判空前缓存的动画驱动）：从动画驱动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.Interrupt(bool)。
global::LeonAnimatorDriver receiverValue18 = animationDrive;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.SetKnifeLocalHitStop(false);
        }

        // receiverValue19（判空前缓存的规则）：从规则读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.Interrupt(bool)。
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
        // minimumAngle（最小角度）：使用位置：KnifeCombatModule.SelectAssistTarget()。
float minimumAngle = configuration.assistTotalAngle * .5f;
        // collision（碰撞）：每轮从重叠球体的返回结果取得一个元素，仅代表当前这一项。使用位置：KnifeCombatModule.SelectAssistTarget()。
foreach (var collision in Physics.OverlapSphere(transform.position + Vector3.up, configuration.assistDistance, configuration.hitMask, QueryTriggerInteraction.Collide))
        {
            // enemy（敌人）：保存 Component.GetComponentInParent<VillagerController>() 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatModule.NoneOcclusion(Vector3, Transform) 的参数、KnifeCombatModule.SelectAssistTarget()。
var enemy = collision.GetComponentInParent<VillagerController>();
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            // direction（方向）：使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、作为 Vector3.Angle(Vector3, Vector3) 的参数、KnifeCombatModule.SelectAssistTarget()。
var direction = enemy.transform.position - transform.position;
            direction.y = 0;
            // angle（角度）：保存 Vector3.Angle(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.SelectAssistTarget()。
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

        // stepSize（步进尺寸）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数。
float stepSize = Mathf.Min(120f * Time.deltaTime, configuration.maximumCorrectionAngle - hasCorrectionAngle);
        // newOrientation（新朝向）：保存 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion) 的参数、作为 Quaternion.Angle(Quaternion, Quaternion) 的参数。
var newOrientation = Quaternion.RotateTowards(transform.rotation, assistTargetOrientation, stepSize);
        hasCorrectionAngle += Quaternion.Angle(transform.rotation, newOrientation);
        coordinator.CommitKnifeAssistOrientation(newOrientation);
    }

    /// <summary>RegisterIncomingAttackPrompt 的业务处理入口；本方法直接依赖 Object.GetInstanceID()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HandleOnParryWindowOpen()、EnemyCombatModule.LateUpdate()；下游（含内部回调体） Object.GetInstanceID()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">中文含义：传入攻击。使用位置：作为 Dictionary&lt;int, KnifeIncomingAttackContext&gt;.this[int] 的参数、KnifeCombatModule.RegisterIncomingAttackPrompt(KnifeIncomingAttackContext)。</param>
    public void RegisterIncomingAttackPrompt(KnifeIncomingAttackContext incomingAttack)
    {
        // receiverValue20（判空前缓存的传入攻击）：从传入攻击读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.RegisterIncomingAttackPrompt(KnifeIncomingAttackContext)。
global::KnifeIncomingAttackContext receiverValue20 = incomingAttack;
        // optionalValue21（条件访问得到的接收者值20的攻击者）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：KnifeCombatModule.RegisterIncomingAttackPrompt(KnifeIncomingAttackContext)。
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
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：作为 Dictionary&lt;int, KnifeIncomingAttackContext&gt;.Remove(int) 的参数、KnifeCombatModule.CloseIncomingAttackPrompt(VillagerController)。</param>
    public void CloseIncomingAttackPrompt(VillagerController source)
    {
        if (source != null)
        {
            promptCandidate.Remove(source.GetInstanceID());
        }
    }

    /// <summary>中文含义：是否显示招架提示。使用位置：LeonCombat.IsParryPromptVisible.get。</summary>
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
            Record("弹反提示", kind + "；敌人=" + candidate.attacker.name + "；攻击=" + candidate.attackId
                + "；段=" + candidate.attackSegment + "；预计接触=" + candidate.expectedContactTime.ToString("F6"));
        }

        return kind;
    }

    /// <summary>中文含义：当前提示传入攻击。使用位置：KnifeCombatModule.ShowParryPrompt.get。</summary>
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
    /// <param name="incomingAttack">中文含义：传入攻击。使用位置：作为 List&lt;KnifeIncomingAttackContext&gt;.Add(KnifeIncomingAttackContext) 的参数、KnifeCombatModule.CommitIncomingAttack(KnifeIncomingAttackContext)。</param>
    public void CommitIncomingAttack(KnifeIncomingAttackContext incomingAttack)
    {
        if (incomingAttack != null)
        {
            incomingAttackQueue.Add(incomingAttack);
        }
    }

    /// <summary>IsFront 的业务处理入口；本方法直接依赖 Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CurrentPromptIncomingAttack.get、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="incomingAttack">中文含义：传入攻击。使用位置：KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。</param>
    private bool IsFront(KnifeIncomingAttackContext incomingAttack)
    {
        // origin（原点）：使用位置：KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。
Vector3 origin = incomingAttack.attacker.transform.position;
        // playerPosition（玩家位置）：使用位置：KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。
Vector3 playerPosition = transform.position;
        if (incomingAttack.hasAttackOrigin)
        {
            origin = incomingAttack.attackOrigin;
        }

        if (incomingAttack.hasPlayerPositionAtContact)
        {
            playerPosition = incomingAttack.playerPositionAtContact;
        }

        // direction（方向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。
Vector3 direction = origin - playerPosition;
        direction.y = 0;
        if (direction.sqrMagnitude < .0001f)
        {
            direction = -incomingAttack.direction;
        }

        // forward（前向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、KnifeCombatModule.IsFront(KnifeIncomingAttackContext)。
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
            // order（顺序）：保存 double.CompareTo(double) 的返回结果，供当前步骤使用。使用位置：作为 List<KnifeIncomingAttackContext>.Sort(Comparison<KnifeIncomingAttackContext>) 的参数。
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
        // contacts（接触集合）：保存 List<KnifeIncomingAttackContext>.ToArray() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.ResolveIncomingContacts()。
KnifeIncomingAttackContext[] contacts = incomingAttackQueue.ToArray();
        incomingAttackQueue.Clear();
        // incomingAttack（传入攻击）：每轮从接触集合取得一个元素，仅代表当前这一项。使用位置：作为 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext) 的参数。
foreach (KnifeIncomingAttackContext incomingAttack in contacts)
        {
            ResolveIncomingAttack(incomingAttack);
        }
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
        // intervalSnapshot（间隔快照）：保存 List<KnifeTraversedInterval>.ToArray() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.ResolveContactsAfterPose()。
var intervalSnapshot = hitInterval.ToArray();
        hitInterval.Clear();
        if (intervalSnapshot.Length > 0)
        {
            Physics.SyncTransforms();
        }
        // knifeBreakTimeInstance（小刀破坏时间实例）：使用位置：KnifeCombatModule.ResolveContactsAfterPose()。
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
    /// <param name="incomingAttack">中文含义：传入攻击。使用位置：作为 Action&lt;KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot&gt;.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot) 的参数、作为 KnifeCombatModule.IsFront(KnifeIncomingAttackContext) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数 等共 8 处。</param>
    internal void ResolveIncomingAttack(KnifeIncomingAttackContext incomingAttack)
    {
        if (!incomingAttack.Valid)
        {
            return;
        }

        // key（键）：使用位置：作为 HashSet<(int, long, int)>.Add((int, long, int)) 的参数。
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

        // result（当前小刀招架结果）：使用位置：作为 Action<KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot>.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeCombatRules.ParryReaction(KnifeParryOutcome, KnifeIncomingAttackCategory) 的参数 等共 4 处。
KnifeParryOutcome result = KnifeParryOutcome.Failure;
        // failure（失败）：使用位置：作为 Action<KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot>.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
KnifeParryFailure failure = KnifeParryFailure.Unavailable;
        // attempt（尝试）：使用位置：作为 Action<KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot>.Invoke(KnifeIncomingAttackContext, KnifeParryOutcome, KnifeParryFailure, KnifeParryAttemptSnapshot) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
KnifeParryAttemptSnapshot attempt = rules.CurrentAttempt;
        if (CanExecute && ValidKnife)
        {
            result = rules.ResolutionParry(incomingAttack.contactTime, IsFront(incomingAttack), incomingAttack.canParry);
            failure = rules.LastParryFailure;
            attempt = rules.LastResolvedAttempt;
        }

        Record("来袭裁定", result + "；原因=" + failure + "；敌人=" + incomingAttack.attacker.name + "；攻击=" + incomingAttack.attackId + "；段=" + incomingAttack.attackSegment + "；输入序号=" + attempt.sequence + "；按下=" + attempt.pressedAt.ToString("F6") + "；接触=" + incomingAttack.contactTime.ToString("F6") + "；年龄=" + (incomingAttack.contactTime - attempt.pressedAt).ToString("F6") + "；动画秒=" + incomingAttack.animationTime.ToString("F4") + "；依据=" + incomingAttack.contactEvidence);
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

        // perfect（是否完美）：使用位置：作为 EnemyCombatModule.ApplyKnifeParryStagger(bool) 的参数、作为 LeonCombat.PresentKnifeSparks(Vector3, Vector3, bool) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
bool perfect = result == KnifeParryOutcome.Perfect;
        rules.ParryReaction(result, incomingAttack.category);
        CommitCurrentAnimation();
        incomingAttack.attacker.combatModule.ApplyKnifeParryStagger(perfect);
        // selectedValue22（分支选出的配置的完美招架消耗）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 KnifeCombatModule.DeductDurability(int) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
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
        // contact（接触）：使用位置：作为 KnifeCombatModule.PlaySound(AudioClip, Vector3) 的参数、作为 LeonCombat.PresentKnifeSparks(Vector3, Vector3, bool) 的参数。
Vector3 contact = incomingAttack.contactPosition;
        Record("弹反反馈", "输入序号=" + attempt.sequence + "；攻击=" + incomingAttack.attackId + "；段=" + incomingAttack.attackSegment + "；等级=" + result + "；开始=" + Time.timeAsDouble.ToString("F6"));
        // selectedValue23（分支选出的配置的完美招架声音）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 KnifeCombatModule.PlaySound(AudioClip, Vector3) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
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
        // hitStop（命中顿帧）：使用位置：作为 KnifeHitStop.Pause(float) 的参数、作为 Math.Max(double, double) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
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
        // receiverValue25（判空前缓存的传入攻击的攻击者的小刀局部命中顿帧）：从传入攻击的攻击者的小刀局部命中顿帧读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
global::KnifeHitStop receiverValue25 = incomingAttack.attacker.knifeLocalHitStop;
        if (!global::System.Object.ReferenceEquals(receiverValue25, null))
        {
            receiverValue25.Pause(hitStop);
        }

        // receiverValue26（判空前缓存的玩家的摄像机控制器）：从玩家的摄像机控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
global::RE4CameraController receiverValue26 = player.CameraController;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            // argumentValue29（按原顺序缓存的实参浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 RE4CameraController.Shake(float, float) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
float argumentValue29;
            if (perfect)
            {
                argumentValue29 = .065f;
            }
            else
            {
                argumentValue29 = .035f;
            }

            // selectedValue28（分支选出的浮点计算）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 RE4CameraController.Shake(float, float) 的参数、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)。
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
    /// <param name="interval">中文含义：间隔。使用位置：作为 HashSet&lt;long&gt;.Add(long) 的参数、作为 HashSet&lt;long&gt;.Contains(long) 的参数、作为 KnifeActionConfiguration.GetTrajectory(double, out Vector3, out Vector3) 的参数 等共 9 处。</param>
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
            foreach (Collider collision in Physics.OverlapCapsule(bladeBase, bladeTip, configuration.bladeDetectRadius,
                         configuration.hitMask, QueryTriggerInteraction.Collide))
            {
                if (collision == null || collision.transform.IsChildOf(transform))
                {
                    continue;
                }

                var middle = (bladeBase + bladeTip) * .5f;
                Vector3 point;
                if (collision is MeshCollider mesh && !mesh.convex && mesh.GetComponentInParent<VillageLegTrap>() != null)
                {
                    var blade = bladeTip - bladeBase;
                    point = mesh.Raycast(new Ray(bladeBase, blade.normalized), out var bladeHit, blade.magnitude)
                        ? bladeHit.point : mesh.bounds.ClosestPoint(middle);
                }
                else point = collision.ClosestPoint(middle);
                contactList.Add(new ContactCandidate
                {
                    collider = collision,
                    position = point,
                    distance = Vector3.Distance(bladeBase, point),
                    time = interval.begin + (interval.end - interval.begin) * ratio
                });
            }
        }

        // 水平挥刀的烘焙轨迹够不到地面。朝脚边夹子下刀仍须在攻击有效帧内，
        // 准心命中夹体，且人物到接触点之间无遮挡。
        if (player.playerCamera != null)
        {
            var view = player.playerCamera.transform;
            if (view.forward.y < 0f && Physics.Raycast(view.position, view.forward, out var groundHit, 8f,
                configuration.hitMask, QueryTriggerInteraction.Ignore)
                && groundHit.collider.GetComponentInParent<VillageLegTrap>() != null)
            {
                var reach = groundHit.point - transform.position;
                if (Mathf.Abs(reach.y) <= .6f && Vector3.ProjectOnPlane(reach, Vector3.up).sqrMagnitude <= .95f * .95f
                    && Vector3.Dot(transform.forward, reach) >= 0f)
                    contactList.Add(new ContactCandidate { collider = groundHit.collider, position = groundHit.point,
                        distance = reach.magnitude, time = interval.end });
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
            // collision（碰撞）：使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
var collision = contact.collider;
            if (collision == null)
            {
                continue;
            }

            // bodyPart（身体部位）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
var bodyPart = collision.GetComponent<EnemyHitbox>();
            // enemy（敌人）：使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3) 的参数 等共 4 处。
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

            // caseValue（公文包值）：保存 Component.GetComponentInParent<DestructibleWoodenBox>() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
var caseValue = collision.GetComponentInParent<DestructibleWoodenBox>();
            var trap = collision.GetComponentInParent<VillageLegTrap>();
            if (trap != null && collision.isTrigger)
                continue;
            // target（目标变换节点）：使用位置：作为 KnifeCombatModule.NoneOcclusion(Vector3, Transform) 的参数、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
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

            // targetId（目标编号）：保存 Object.GetInstanceID() 的返回结果，供当前步骤使用。使用位置：KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
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
                // hitBodyPart（命中身体部位）：使用位置：作为 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3) 的参数、作为 KnifeCombatModule.Record(string, string) 的参数、作为 KnifeMeleeHitNotification.KnifeMeleeHitNotification(long, VillagerController, HitboxType, Vector3) 的参数 等共 4 处。
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
                // receiverValue37（判空前缓存的已提交命中）：从已提交命中读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)。
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

                Record("刀刃命中", enemy.name + "；部位=" + hitBodyPart + "；刀=" + interval.instance);
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
    /// <param name="point">中文含义：点。使用位置：KnifeCombatModule.NoneOcclusion(Vector3, Transform)。</param>
    /// <param name="target">中文含义：目标变换节点。使用位置：作为 Transform.IsChildOf(Transform) 的参数。</param>
    private bool NoneOcclusion(Vector3 point, Transform target)
    {
        // startPoint（起始点）：使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、KnifeCombatModule.NoneOcclusion(Vector3, Transform)。
Vector3 startPoint = transform.position + Vector3.up * 1.15f;
        // direction（方向）：使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、KnifeCombatModule.NoneOcclusion(Vector3, Transform)。
Vector3 direction = point - startPoint;
        // hit（命中）：每轮从射线检测全部的返回结果取得一个元素，仅代表当前这一项。使用位置：KnifeCombatModule.NoneOcclusion(Vector3, Transform)。
foreach (var hit in Physics.RaycastAll(startPoint, direction.normalized, direction.magnitude, configuration.hitMask, QueryTriggerInteraction.Ignore))
        {
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
    /// <param name="count">中文含义：数量。使用位置：作为 InventoryManager.TryConsumeDurability(string, int, out bool) 的参数。</param>
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
        Record("断刀", "耐久已归零");
    }

    /// <summary>PlaySound 的业务处理入口；本方法直接依赖 AudioManager.PlaySFX3D(AudioClip, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DeductDurability(int)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） AudioManager.PlaySFX3D(AudioClip, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sound">中文含义：声音。使用位置：作为 AudioManager.PlaySFX3D(AudioClip, Vector3) 的参数、KnifeCombatModule.PlaySound(AudioClip, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 AudioManager.PlaySFX3D(AudioClip, Vector3) 的参数。</param>
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
            // receiverValue38（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatModule.PlaySound(AudioClip, Vector3)。
global::AudioManager receiverValue38 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue38, null))
            {
                receiverValue38.PlaySFX3D(sound, position);
            }
        }
    }

    /// <summary>Record 的业务处理入口；本方法直接依赖 double.ToString(string, IFormatProvider)、Queue&lt;string&gt;.Dequeue()、Queue&lt;string&gt;.Enqueue(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ActionSwitch(KnifeAction, long, double)、KnifeCombatModule.DeductDurability(int)、KnifeCombatModule.DetectBlade(KnifeTraversedInterval)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、KnifeCombatModule.Update()；下游（含内部回调体） double.ToString(string, IFormatProvider)、Queue&lt;string&gt;.Dequeue()、Queue&lt;string&gt;.Enqueue(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">中文含义：类别。使用位置：作为 Queue&lt;string&gt;.Enqueue(string) 的参数。</param>
    /// <param name="content">中文含义：内容。使用位置：作为 Queue&lt;string&gt;.Enqueue(string) 的参数。</param>
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
