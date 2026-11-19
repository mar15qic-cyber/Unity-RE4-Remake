using System.Collections.Generic;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyAIManager，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>本局敌群服务，负责具名登记、攻击名额和站位协作；个体行为通过快照和审批入口使用敌群状态，不能绕过动作及租用有效期。</remarks>
public partial class EnemyAIManager : Singleton<EnemyAIManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：最大生效距离。使用位置：EnemyAIManager.UpdateEnemyStates()、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Header("远距离启停")]
    public float maxActiveDistance = 80f;
    /// <summary>中文含义：更新间隔。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.Update()。</summary>
    public float updateInterval = 0.2f;
    /// <summary>中文含义：包围距离。使用位置：作为 EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int) 的参数、作为 EnemyAIManager.DrawSurroundSlots(Vector3, float, Color) 的参数、作为 EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool) 的参数 等共 6 处。</summary>
    [Header("敌群战斗协调")]
    [Min(0.1f)]
    public float surroundDistance = 1.75f;
    /// <summary>中文含义：包围阵型起始距离。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float surroundFormationStartDistance = 3f;
    /// <summary>中文含义：最小槽位分离量。使用位置：EnemyAIManager.SelectSurroundSlot(VillagerController, Vector3, float, int, bool, out int)。</summary>
    [Min(0.1f)]
    public float minimumSlotSeparation = 0.85f;
    /// <summary>中文含义：攻击恢复延迟。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float attackRecoveryDelay = 0.8f;
    /// <summary>中文含义：最大并发攻击者集合。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.Min(int, int) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Range(1, 4)]
    public int maximumConcurrentAttackers = 2;
    /// <summary>中文含义：最小并发攻击角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Range(0f, 180f)]
    public float minimumConcurrentAttackAngle = 75f;
    /// <summary>中文含义：正面抓取全局冷却。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。</summary>
    [Min(0f)]
    public float frontGrabGlobalCooldown = 9f;
    /// <summary>中文含义：腿部撕咬全局冷却。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。</summary>
    [Min(0f)]
    public float legBiteGlobalCooldown = 6f;
    /// <summary>中文含义：电锯执行全局冷却。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。</summary>
    [Min(0f)]
    public float chainsawExecutionGlobalCooldown = 8f;
    /// <summary>中文含义：配对动作优先级预约窗口。近身交互资格成立后，为它短暂封住新的普通攻击名额，避免现有攻击者结束后又被下一名普通攻击者抢占。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("近身交互资格成立后，为它短暂封住新的普通攻击名额，避免现有攻击者结束后又被下一名普通攻击者抢占。")]
    [Min(0.1f)]
    public float qtePriorityReservationWindow = 1.25f;
    /// <summary>中文含义：包围槽位数量。使用位置：作为 Mathf.Max(int, int) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Range(4, 16)]
    public int surroundSlotCount = 16;
    /// <summary>中文含义：包围槽位保持持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.25f)]
    public float surroundSlotHoldDuration = 2f;
    /// <summary>中文含义：包围槽位抵达距离。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(0.1f)]
    public float surroundSlotArrivalDistance = 0.65f;
    /// <summary>中文含义：包围槽位玩家移动阈值。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)。</summary>
    [Min(0.1f)]
    public float surroundSlotPlayerMoveThreshold = 0.75f;
    /// <summary>中文含义：包围槽位切换惩罚。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float surroundSlotSwitchPenalty = 8f;
    /// <summary>中文含义：共享战斗警戒半径。使用位置：EnemyAIManager.UpdateSharedCombatAlert()、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Header("共享战斗警报")]
    [Min(1f)]
    public float sharedCombatAlertRadius = 24f;
    /// <summary>中文含义：共享战斗警戒间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.05f)]
    public float sharedCombatAlertInterval = 0.25f;
    /// <summary>中文含义：共享战斗警戒持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float sharedCombatAlertDuration = 2.5f;
    /// <summary>中文含义：移动听觉半径。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(1f)]
    public float movementHearingRadius = 7f;
    /// <summary>中文含义：移动听觉速度阈值。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(0.1f)]
    public float movementHearingSpeedThreshold = 3.8f;
    /// <summary>中文含义：门干扰半径。使用位置：作为 EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(1f)]
    public float doorDisturbanceRadius = 30f;
    /// <summary>中文含义：阵型距离迟滞。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("追击目标稳定")]
    [Min(0f)]
    public float formationDistanceHysteresis = 0.6f;
    /// <summary>中文含义：追击目标保持持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float pursuitTargetHoldDuration = 0.8f;
    /// <summary>中文含义：追击目标玩家移动阈值。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float pursuitTargetPlayerMoveThreshold = 0.9f;
    /// <summary>中文含义：追击目标玩家高度阈值。玩家高度变化超过该值时立即废弃追击缓存，防止跨楼层沿用旧目标。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("玩家高度变化超过该值时立即废弃追击缓存，防止跨楼层沿用旧目标")]
    [Min(0.1f)]
    public float pursuitTargetPlayerHeightThreshold = 0.5f;
    /// <summary>中文含义：战术侧翼半径。使用位置：作为 EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool) 的参数、作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Header("战术包围")]
    [Min(2f)]
    public float tacticalFlankRadius = 6f;
    /// <summary>中文含义：战术侧翼激活距离。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(4f)]
    public float tacticalFlankActivationDistance = 6f;
    /// <summary>中文含义：最大战术侧翼激活距离。侧翼预备位的启用距离上限。过大的旧场景数值会让敌人过早放弃包围并排成直线。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("侧翼预备位的启用距离上限。过大的旧场景数值会让敌人过早放弃包围并排成直线。")]
    [Min(4f)]
    public float maximumTacticalFlankActivationDistance = 12f;
    /// <summary>中文含义：战术侧翼抵达距离。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Min(0.1f)]
    public float tacticalFlankArrivalDistance = 1.15f;
    /// <summary>中文含义：战术侧翼玩家移动阈值。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)。</summary>
    [Min(0.1f)]
    public float tacticalFlankPlayerMoveThreshold = 2f;
    /// <summary>中文含义：直接压力敌人数量。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Range(0, 3)]
    public int directPressureEnemyCount = 2;
    /// <summary>中文含义：阵型目标权重。追击目标占零点七八，包围位置最多占零点二二，确保追击玩家始终是主目标。</summary>
    [Tooltip("追击目标占零点七八，包围位置最多占零点二二，确保追击玩家始终是主目标。")]
    [Range(0f, 0.45f)]
    public float formationObjectiveWeight = 0.22f;
    /// <summary>中文含义：是否需要村庄入口触发器。使用位置：EnemyAIManager.BindingCurrentSession()、EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [Header("入村追击触发")]
    public bool requireVillageEntranceTrigger = true;
    /// <summary>中文含义：是否显示村庄入口触发器辅助线。使用位置：EnemyAIManager.OnDrawGizmosSelected()。</summary>
    public bool showVillageEntranceTriggerGizmo = true;
    /// <summary>中文含义：是否优先使用场景中名为 start 的物体作为追击触发中心。优先使用场景中名为 start 的物体作为追击触发中心。</summary>
    [Tooltip("优先使用场景中名为 start 的物体作为追击触发中心。")]
    public bool useNamedVillageEntranceAnchor = true;
    /// <summary>中文含义：敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.ActiveEnemyCount.get、EnemyAIManager.EngageAllMissionEnemies(Vector3)、EnemyAIManager.GetNearestEnemy(Vector3) 等共 13 处。</summary>
    private readonly List<VillagerController> enemies = new();
    /// <summary>中文含义：提示候选集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.PromptCandidates.get、EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)、EnemyAIManager.UnregisterPromptCandidate(VillagerQTEParticipant)。</summary>
    private readonly List<VillagerQTEParticipant> promptCandidates = new();
    /// <summary>中文含义：提示候选集合。使用位置：LeonQTECoordinator.GetPromptCandidates()。</summary>
    internal IReadOnlyList<VillagerQTEParticipant> PromptCandidates => promptCandidates;

    /// <summary>RegisterPromptCandidate 的业务处理入口；本方法直接依赖 List&lt;VillagerQTEParticipant&gt;.Add(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Contains(VillagerQTEParticipant)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.RegisterEnemy(VillagerController)、VillagerQTEParticipant.RegisterPromptCandidate()；下游（含内部回调体） List&lt;VillagerQTEParticipant&gt;.Add(VillagerQTEParticipant)、List&lt;VillagerQTEParticipant&gt;.Contains(VillagerQTEParticipant)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">中文含义：候选。使用位置：作为 List&lt;VillagerQTEParticipant&gt;.Add(VillagerQTEParticipant) 的参数、作为 List&lt;VillagerQTEParticipant&gt;.Contains(VillagerQTEParticipant) 的参数、EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)。</param>
    internal void RegisterPromptCandidate(VillagerQTEParticipant candidate)
    {
        if (candidate != null && candidate.isActiveAndEnabled && !promptCandidates.Contains(candidate))
        {
            promptCandidates.Add(candidate);
        }
    }

    /// <summary>UnregisterPromptCandidate 的业务处理入口；本方法直接依赖 List&lt;VillagerQTEParticipant&gt;.Remove(VillagerQTEParticipant)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.UnregisterEnemy(VillagerController)、VillagerQTEParticipant.OnDisable()；下游（含内部回调体） List&lt;VillagerQTEParticipant&gt;.Remove(VillagerQTEParticipant)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">中文含义：候选。使用位置：作为 List&lt;VillagerQTEParticipant&gt;.Remove(VillagerQTEParticipant) 的参数。</param>
    internal void UnregisterPromptCandidate(VillagerQTEParticipant candidate) => promptCandidates.Remove(candidate);
    /// <summary>中文含义：战斗槽位集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot) 等共 12 处。</summary>
    private readonly Dictionary<VillagerController, CombatSlotAssignment> combatSlots = new();
    /// <summary>中文含义：追击目标列表。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3) 等共 5 处。</summary>
    private readonly Dictionary<VillagerController, PursuitTargetAssignment> pursuitTargets = new();
    /// <summary>中文含义：直接追击敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyAIManager.ShouldUseDirectPursuit(VillagerController, float)、EnemyAIManager.UnregisterEnemy(VillagerController) 等共 4 处。</summary>
    /// <summary>中文含义：直接压力敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)、EnemyAIManager.HasDirectPressureDirectionSpace(VillagerController, Vector3, VillagerController) 等共 7 处。</summary>
    private readonly HashSet<VillagerController> directPressureEnemies = new();
    /// <summary>中文含义：攻击中敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.CanAcquireAttackReservation(VillagerController)、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind) 等共 13 处。</summary>
    private readonly HashSet<VillagerController> attackingEnemies = new();
    /// <summary>中文含义：配对动作已预留敌人。使用位置：作为 EnemyAIManager.IsReservationCandidateValid(VillagerController) 的参数、作为 EnemyCombatTimeline.Number(VillagerController) 的参数、作为 Gizmos.DrawLine(Vector3, Vector3) 的参数 等共 16 处。</summary>
    private VillagerController qteReservedEnemy;
    /// <summary>中文含义：待处理配对动作敌人。使用位置：作为 EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind) 的参数、作为 EnemyCombatTimeline.Number(VillagerController) 的参数、EnemyAIManager.BuildCombatTimelineSummary(VillagerController) 等共 11 处。</summary>
    private VillagerController pendingQTEEnemy;
    /// <summary>中文含义：待处理配对动作种类。使用位置：作为 EnemyAIManager.GetQTERequestPriority(QTEKind) 的参数、作为 EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind) 的参数、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind) 等共 6 处。</summary>
    private QTEKind pendingQTEKind;
    /// <summary>中文含义：待处理配对动作到期时间。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.ClearPendingQTERequest() 等共 6 处。</summary>
    private float pendingQTEExpiresAt;
    /// <summary>中文含义：下一次配对动作优先级允许的时间。使用位置：EnemyAIManager.HasPendingQTERequest()、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)。</summary>
    private float nextQTEPriorityAllowedTime;
    /// <summary>中文含义：配对动作预约已取得所在。使用位置：EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、EnemyAIManager.ValidateCombatReservations()。</summary>
    private float qteReservationAcquiredAt;
    /// <summary>中文含义：直接压力评估帧。使用位置：EnemyAIManager.InvalidatePursuitDestination(VillagerController)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)。</summary>
    /// <summary>中文含义：下一次攻击允许的时间。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.CanAcquireAttackReservation(VillagerController) 等共 9 处。</summary>
    private float nextAttackAllowedTime;
    /// <summary>中文含义：下一次正面抓取允许的时间。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.GetNextQTEAllowedTime(QTEKind)。</summary>
    private float nextFrontGrabAllowedTime;
    /// <summary>中文含义：下一次腿部撕咬允许的时间。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.GetNextQTEAllowedTime(QTEKind)。</summary>
    private float nextLegBiteAllowedTime;
    /// <summary>中文含义：下一次电锯执行允许的时间。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.GetNextQTEAllowedTime(QTEKind)。</summary>
    private float nextChainsawExecutionAllowedTime;
    /// <summary>中文含义：更新计时器。使用位置：EnemyAIManager.Update()。</summary>
    private float updateTimer;
    /// <summary>中文含义：共享战斗警戒是否生效。使用位置：EnemyAIManager.BindingCurrentSession()、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)、EnemyAIManager.NotifyCombatEngaged(VillagerController) 等共 7 处。</summary>
    private bool sharedCombatAlertActive;
    /// <summary>中文含义：下一次共享战斗警戒时间。使用位置：EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)、EnemyAIManager.NotifyCombatEngaged(VillagerController)、EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float) 等共 6 处。</summary>
    private float nextSharedCombatAlertTime;
    /// <summary>中文含义：共享战斗警戒到期时间。使用位置：EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)、EnemyAIManager.NotifyCombatEngaged(VillagerController)、EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float) 等共 6 处。</summary>
    private float sharedCombatAlertExpiresAt;
    /// <summary>中文含义：前一次玩家位置。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController)、EnemyAIManager.UpdateMovementNoiseAwareness()。</summary>
    private Vector3 previousPlayerPosition;
    /// <summary>中文含义：是否具有前一次玩家位置。使用位置：EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController)、EnemyAIManager.UpdateMovementNoiseAwareness()。</summary>
    private bool hasPreviousPlayerPosition;
    /// <summary>中文含义：是否已开始。使用位置：EnemyAIManager.BindingCurrentSession()、EnemyAIManager.LateUpdate()、EnemyAIManager.OnEnable() 等共 4 处。</summary>
    private bool started;
    /// <summary>中文含义：是否具有玩家追击区域。使用位置：EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int)、EnemyAIManager.UpdatePlayerPursuitRegion()。</summary>
    private bool hasPlayerPursuitRegion;
    /// <summary>中文含义：玩家追击区域。使用位置：作为 PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int) 的参数、作为 PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int) 的参数、EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int) 等共 4 处。</summary>
    private int playerPursuitRegion = -1;
    /// <summary>中文含义：追击区域版本。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、作为 VillagerController.ReceivePursuitRegionChange(int, Vector3) 的参数、EnemyAIManager.PursuitRegionVersion.get 等共 4 处。</summary>
    private int pursuitRegionVersion;
    /// <summary>中文含义：是否具有前一次追击区域玩家位置。使用位置：EnemyAIManager.UpdatePlayerPursuitRegion()。</summary>
    private bool hasPreviousPursuitRegionPlayerPosition;
    /// <summary>中文含义：前一次追击区域玩家位置。使用位置：作为 PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int) 的参数、EnemyAIManager.UpdatePlayerPursuitRegion()。</summary>
    private Vector3 previousPursuitRegionPlayerPosition;
    /// <summary>中文含义：待处理支持的玩家区域。使用位置：EnemyAIManager.UpdatePlayerPursuitRegion()。</summary>
    private int pendingSupportedPlayerRegion = -1;
    /// <summary>中文含义：待处理支持的玩家区域自从。使用位置：EnemyAIManager.UpdatePlayerPursuitRegion()。</summary>
    private float pendingSupportedPlayerRegionSince;
    /// <summary>中文含义：生效敌人数量。使用位置：GameManager.totalEnemiesAlive.get。</summary>
    public int ActiveEnemyCount => enemies.Count;

    internal bool HasLocalCombatThreat(LeonController player)
    {
        if (!IsCombatUnlocked || player == null)
        {
            return false;
        }

        const float combatMusicDistance = 16f;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsActive || enemy.IsDead ||
                !enemy.IsCombatEngaged || enemy.Player != player ||
                (enemy.transform.position - player.transform.position).sqrMagnitude > combatMusicDistance * combatMusicDistance)
            {
                continue;
            }

            if (enemy.CurrentState == VillagerState.Attack ||
                enemy.perceptionModule != null && enemy.perceptionModule.ReadPlayerVisible)
            {
                return true;
            }
        }

        return false;
    }

    internal bool HasEngagedEnemy()
    {
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.isActiveAndEnabled && enemy.IsActive && !enemy.IsDead && enemy.IsCombatEngaged)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>中文含义：玩家目标。使用位置：作为 EnemyAIManager.EngageAllMissionEnemies(Vector3) 的参数、作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController) 的参数 等共 31 处。</summary>
    public LeonController PlayerTarget
    {
        get
        {
            if (chasePlayer != null)
            {
                return chasePlayer;
            }
            else
            {
                return scenePlayer;
            }
        }

        private set => chasePlayer = value;
    }

    /// <summary>中文含义：追逐玩家。使用位置：EnemyAIManager.PlayerTarget.get、EnemyAIManager.PlayerTarget.set。</summary>
    private LeonController chasePlayer;
    /// <summary>中文含义：攻击中敌人。</summary>
    public VillagerController AttackingEnemy => GetFirstReservedAttacker();
    /// <summary>中文含义：准备中敌人。</summary>
    public VillagerController PreparingEnemy => null;
    /// <summary>中文含义：是否战斗已解锁。使用位置：ChapterOneMissionManager.CanAdvanceGameplay()、EnemyAIManager.BindingCurrentSession()、EnemyAIManager.BuildCombatTimelineSummary(VillagerController) 等共 18 处。</summary>
    public bool IsCombatUnlocked { get; private set; }
    internal bool AwaitsVillageDiscovery => requireVillageDiscovery && !IsCombatUnlocked && !IsBellWithdrawal;
    private bool requireVillageDiscovery;
    private float nextVillageFootstep;
    internal bool CanObserveVillagePlayer => PlayerTarget != null && PlayerTarget.currentState != LeonState.Traversing
        && PlayerTarget.currentState != LeonState.Dead && Time.deltaTime > 0f;
    internal bool IsBellWithdrawal { get; private set; }
    /// <summary>中文含义：追击区域版本。</summary>
    public int PursuitRegionVersion => pursuitRegionVersion;

    /// <summary>TryGetCurrentPlayerPursuitRegion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)、VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="region">中文含义：区域。输出参数，由本入口写入并交给调用方。使用位置：EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int)。</param>
    public bool TryGetCurrentPlayerPursuitRegion(out int region)
    {
        region = playerPursuitRegion;
        return hasPlayerPursuitRegion && region >= 0;
    }

    /// <summary>中文含义：场景玩家。使用位置：EnemyAIManager.BindingCurrentSession()、EnemyAIManager.PlayerTarget.get、SceneStartupEntry.ConfigurationError()。</summary>
    [Header("本局玩家与入村触发器")]
    [UnityEngine.Serialization.FormerlySerializedAs("场景玩家")]
    [UnityEngine.InspectorName("场景玩家")]
    public LeonController scenePlayer;
    /// <summary>中文含义：入村触发器。使用位置：EnemyAIManager.ValidateSceneConfiguration()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("入村触发器")]
    [UnityEngine.InspectorName("入村触发器")]
    public VillageEntranceCombatTrigger enterVillageTrigger;
    /// <summary>中文含义：精确通路。使用位置：作为 MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 的参数、作为 PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[]) 的参数。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("精确通路")]
    [UnityEngine.InspectorName("精确通路")]
    public EnemyNavigationPreciseLink[] preciseTraversal;
    /// <summary>ValidateSceneConfiguration 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;BoxCollider&gt;()、Mathf.Max(float, float)、Mathf.Min(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.ConfigurationError()；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()、Mathf.Max(float, float)、Mathf.Min(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal string ValidateSceneConfiguration()
    {
        if (!(maxActiveDistance >= 80f))
        {
            return nameof(maxActiveDistance);
        }

        if (!(sharedCombatAlertRadius >= 8f && sharedCombatAlertRadius <= 35f))
        {
            return nameof(sharedCombatAlertRadius);
        }

        if (!(movementHearingRadius >= 3f && movementHearingRadius <= 10f))
        {
            return nameof(movementHearingRadius);
        }

        if (!(movementHearingSpeedThreshold >= 3.5f))
        {
            return nameof(movementHearingSpeedThreshold);
        }

        if (!(doorDisturbanceRadius >= 30f))
        {
            return nameof(doorDisturbanceRadius);
        }

        if (maximumConcurrentAttackers < 1 || maximumConcurrentAttackers > 4)
        {
            return nameof(maximumConcurrentAttackers);
        }

        if (!(minimumConcurrentAttackAngle >= 0f && minimumConcurrentAttackAngle <= 180f))
        {
            return nameof(minimumConcurrentAttackAngle);
        }

        if (!(surroundSlotArrivalDistance >= .1f))
        {
            return nameof(surroundSlotArrivalDistance);
        }

        if (directPressureEnemyCount < Mathf.Min(maximumConcurrentAttackers, 3) || directPressureEnemyCount > 3)
        {
            return nameof(directPressureEnemyCount);
        }

        if (surroundSlotCount < 16)
        {
            return nameof(surroundSlotCount);
        }

        if (!(tacticalFlankRadius >= surroundDistance + 3.5f))
        {
            return nameof(tacticalFlankRadius);
        }

        // minimumDistance（最小距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.ValidateSceneConfiguration()。
float minimumDistance = Mathf.Max(4f, surroundFormationStartDistance + 1f);
        if (!(tacticalFlankActivationDistance >= minimumDistance && tacticalFlankActivationDistance <= Mathf.Max(minimumDistance, maximumTacticalFlankActivationDistance)))
        {
            return nameof(tacticalFlankActivationDistance);
        }

        if (!(tacticalFlankArrivalDistance >= .75f))
        {
            return nameof(tacticalFlankArrivalDistance);
        }

        if (requireVillageEntranceTrigger)
        {
            if (enterVillageTrigger == null || !enterVillageTrigger.isActiveAndEnabled || enterVillageTrigger.manager != this || enterVillageTrigger.gameObject.scene != gameObject.scene)
            {
                return nameof(enterVillageTrigger);
            }

            // collision（碰撞）：保存 Component.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.ValidateSceneConfiguration()。
var collision = enterVillageTrigger.GetComponent<BoxCollider>();
            if (collision == null || !collision.enabled || !collision.isTrigger || collision.size.x <= 0f || collision.size.y <= 0f || collision.size.z <= 0f)
            {
                return "入村触发器.碰撞范围";
            }
        }

        return null;
    }

    /// <summary>中文含义：导航查询。使用位置：EnemyAIManager.BindingCurrentSession()、EnemyAIManager.LateUpdate()、EnemyAIManager.OnDestroy() 等共 7 处。</summary>
    internal NavigationQueryService NavigationQuery { get; private set; }

    /// <summary>BindingCurrentSession 的业务处理入口；本方法直接依赖 EnemyAIManager.BindEvents()、EnemyAIManager.EngageAllMissionEnemies(Vector3)、NavigationQueryService.Dispose()、NavigationQueryService.NavigationQueryService()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） EnemyAIManager.BindEvents()、EnemyAIManager.EngageAllMissionEnemies(Vector3)、NavigationQueryService.Dispose()、NavigationQueryService.NavigationQueryService()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BindingCurrentSession(bool discoverVillageCombat = false)
    {
        // receiverValue2（判空前缓存的导航查询）：从导航查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.BindingCurrentSession()。
global::NavigationQueryService receiverValue2 = NavigationQuery;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Dispose();
        }

        NavigationQuery = new NavigationQueryService();
        ResetTacticalCombat();
        requireVillageDiscovery = discoverVillageCombat;
        IsCombatUnlocked = !requireVillageDiscovery && !requireVillageEntranceTrigger;
        sharedCombatAlertActive = false;
        PlayerTarget = scenePlayer;
        if (IsCombatUnlocked)
        {
            BeginVillageEngagement();
        }

        started = true;
        BindEvents();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (started)
        {
            BindEvents();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        standingImpacts.Clear();
        UnbindEvents();
    }

    private readonly List<EnemyCombatModule.StandingImpact> standingImpacts = new();
    internal IReadOnlyList<VillagerController> StandingContactEnemies => enemies;

    internal void QueueStandingImpact(EnemyCombatModule.StandingImpact impact)
    {
        standingImpacts.Add(impact);
    }

    internal void ResolveStandingImpacts()
    {
        standingImpacts.Sort(CompareStandingImpact);
        foreach (EnemyCombatModule.StandingImpact impact in standingImpacts)
        {
            if (impact.attacker != null)
            {
                impact.attacker.ResolveStandingImpact(impact);
            }
        }

        standingImpacts.Clear();
    }

    private static int CompareStandingImpact(EnemyCombatModule.StandingImpact first, EnemyCombatModule.StandingImpact second)
    {
        int order = first.time.CompareTo(second.time);
        if (order != 0)
        {
            return order;
        }

        return first.action.Object.InstanceId.CompareTo(second.action.Object.InstanceId);
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.UnbindEvents()、NavigationQueryService.Dispose()、object.ReferenceEquals(object, object)、Singleton&lt;EnemyAIManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        // receiverValue3（判空前缓存的导航查询）：从导航查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.OnDestroy()。
global::NavigationQueryService receiverValue3 = NavigationQuery;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Dispose();
        }

        UnbindEvents();
        base.OnDestroy();
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.BindingCurrentSession()、EnemyAIManager.OnEnable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BindEvents()
    {
        CombatEventDispatcher.OnWeaponFired -= HandleWeaponFired;
        CombatEventDispatcher.OnWeaponFired += HandleWeaponFired;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.OnDestroy()、EnemyAIManager.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UnbindEvents()
    {
        CombatEventDispatcher.OnWeaponFired -= HandleWeaponFired;
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） NavigationQueryService.CommitRequest()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (started)
        {
            // receiverValue4（判空前缓存的导航查询）：从导航查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.LateUpdate()。
global::NavigationQueryService receiverValue4 = NavigationQuery;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.CommitRequest();
            }
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.UpdateEnemyStates()、EnemyAIManager.UpdateMovementNoiseAwareness()、EnemyAIManager.UpdatePlayerPursuitRegion()、EnemyAIManager.UpdateSharedCombatAlert()、EnemyAIManager.ValidateCombatReservations()、NavigationQueryService.Update() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!started)
        {
            return;
        }

        // receiverValue5（判空前缓存的导航查询）：从导航查询读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.Update()。
global::NavigationQueryService receiverValue5 = NavigationQuery;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Update();
        }

        if (IsBellWithdrawal)
        {
            return;
        }

        UpdatePlayerPursuitRegion();
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval)
        {
            return;
        }

        updateTimer = 0f;
        UpdateEnemyStates();
        ValidateCombatReservations();
        ReleaseQueuedEngagements(2);
        SampleEscapeVelocity();
        SelectAttackApproach();
        UpdateEscapeInterception();
        UpdateMovementNoiseAwareness();
        UpdateSharedCombatAlert();
    }

    /// <summary>
    /// 玩家跨越门洞后立即废弃整群敌人的旧包围站位和稳定目标。
    /// 区域来自寻路图切分结果，不依赖开门音效，也不依赖玩家继续
    /// 走进另一间房屋。敌人控制器会在安全阶段应用本次版本变化。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.Update()；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3)、CombatDiagnosticFields.Text(string, string)、Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Clear()、Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Clear()、EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdatePlayerPursuitRegion()
    {
        if (PlayerTarget == null)
        {
            return;
        }

        // playerPosition（玩家位置）：使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、作为 PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int) 的参数、作为 PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int) 的参数 等共 6 处。
Vector3 playerPosition = PlayerTarget.transform.position;
        if (!hasPreviousPursuitRegionPlayerPosition)
        {
            hasPreviousPursuitRegionPlayerPosition = true;
            previousPursuitRegionPlayerPosition = playerPosition;
        }

        if (!hasPlayerPursuitRegion)
        {
            if (!PursuitNavigationTopology.TryGetRegion(playerPosition, out int /* 中文含义：初始区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ initialRegion))
            {
                return;
            }

            hasPlayerPursuitRegion = true;
            playerPursuitRegion = initialRegion;
            previousPursuitRegionPlayerPosition = playerPosition;
            return;
        }

        // currentRegion（当前区域）：使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.UpdatePlayerPursuitRegion()。
int currentRegion = playerPursuitRegion;
        // regionChangeReason（区域变化原因）：使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、EnemyAIManager.UpdatePlayerPursuitRegion()。
string regionChangeReason = "穿过门洞";
        if (PursuitNavigationTopology.TryFindCrossedPortal(previousPursuitRegionPlayerPosition, playerPosition, playerPursuitRegion, out int /* 中文含义：已越过区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ crossedRegion))
        {
            currentRegion = crossedRegion;
            pendingSupportedPlayerRegion = -1;
        }
        else if (PursuitNavigationTopology.TryGetRegion(playerPosition, out int /* 中文含义：采样到的区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sampledRegion) && sampledRegion != playerPursuitRegion)
        {
            if (!PursuitNavigationTopology.AreRegionsDirectlyConnected(playerPursuitRegion, sampledRegion))
            {
                currentRegion = sampledRegion;
                regionChangeReason = "非门洞区域变化";
                pendingSupportedPlayerRegion = -1;
            }
            else if (PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(playerPosition, sampledRegion))
            {
                // 屋顶和一楼可能共用区域，跳落、翻窗也能到达门外。
                // 不能把真实穿门当成唯一出口。远离门洞且有同区域踏面
                // 支撑时，连续确认落地后更新区域；门边仍保留穿门判定。
                if (pendingSupportedPlayerRegion != sampledRegion)
                {
                    pendingSupportedPlayerRegion = sampledRegion;
                    pendingSupportedPlayerRegionSince = Time.time;
                }

                if (Time.time - pendingSupportedPlayerRegionSince >= 0.2f)
                {
                    currentRegion = sampledRegion;
                    regionChangeReason = "门洞外落地确认";
                    pendingSupportedPlayerRegion = -1;
                }
            }
            else
            {
                pendingSupportedPlayerRegion = -1;
            }
        }
        else
        {
            pendingSupportedPlayerRegion = -1;
        }

        previousPursuitRegionPlayerPosition = playerPosition;
        if (playerPursuitRegion == currentRegion)
        {
            return;
        }

        // previousRegion（前一次区域）：使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。
int previousRegion = playerPursuitRegion;
        playerPursuitRegion = currentRegion;
        pursuitRegionVersion++;
        pursuitTargets.Clear();
        combatSlots.Clear();
        directPressureEnemies.Clear();
        // index（当前元素索引）：使用位置：作为 List<VillagerController>.this[int] 的参数、EnemyAIManager.UpdatePlayerPursuitRegion()。
for (int index = 0; index < enemies.Count; index++)
        {
            // enemy（敌人）：使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、作为 EnemyCombatTimeline.IsRecord(VillagerController) 的参数、作为 EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 的参数 等共 4 处。
VillagerController enemy = enemies[index];
            if (!IsEnemyUsable(enemy) || enemy.IsQTEControlled)
            {
                continue;
            }

            enemy.ReceivePursuitRegionChange(pursuitRegionVersion, playerPosition);
            if (EnemyCombatTimeline.IsRecord(enemy))
            {
                EnemyCombatTimeline.Record(enemy, "玩家追击区域切换", CombatDiagnosticFields.Count("来源区域", previousRegion), CombatDiagnosticFields.Count("目标区域", currentRegion), CombatDiagnosticFields.Count("区域版本", pursuitRegionVersion), CombatDiagnosticFields.Text("依据", regionChangeReason), CombatDiagnosticFields.Position("玩家位置", playerPosition));
            }
        }
    }

    /// <summary>UnlockCombatAtVillageEntrance 的业务处理入口；本方法直接依赖 EnemyAIManager.EngageAllMissionEnemies(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()、VillageEntranceCombatTrigger.OnTriggerEnter(Collider)；下游（含内部回调体） EnemyAIManager.EngageAllMissionEnemies(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enteringPlayer">中文含义：进入中玩家。使用位置：EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController)。</param>
    public void UnlockCombatAtVillageEntrance(LeonController enteringPlayer)
    {
        UnlockVillageCombat(enteringPlayer, null);
    }

    internal void DiscoverVillageCombat(VillagerController observer)
    {
        if (AwaitsVillageDiscovery && CanObserveVillagePlayer && IsEnemyUsable(observer) && !observer.IsQTEControlled)
        {
            UnlockVillageCombat(PlayerTarget, observer);
        }
    }

    private void UnlockVillageCombat(LeonController enteringPlayer, VillagerController observer)
    {
        if (IsCombatUnlocked || IsBellWithdrawal)
        {
            return;
        }

        if (enteringPlayer != null)
        {
            PlayerTarget = enteringPlayer;
        }

        if (PlayerTarget == null)
        {
            return;
        }

        IsCombatUnlocked = true;
        previousPlayerPosition = PlayerTarget.transform.position;
        hasPreviousPlayerPosition = true;
        sharedCombatAlertActive = false;
        nextSharedCombatAlertTime = 0f;
        sharedCombatAlertExpiresAt = 0f;
        if (observer != null)
        {
            ReleaseEngagementOnHit(observer);
            observer.ReceiveVillagePursuitOrder(PlayerTarget.transform.position);
            releasedFirstEngagement = true;
            nextEngagementBatch = Time.time + EngagementBatchInterval;
        }

        BeginVillageEngagement();
    }

    /// <summary>村口解锁后开始分批接战。</summary>
    private void BeginVillageEngagement()
    {
        ReleaseQueuedEngagements(1);
        SelectAttackApproach();
    }

    internal void BeginBellWithdrawal(Vector3 destination, VillageSoundscape cover)
    {
        if (IsBellWithdrawal)
        {
            return;
        }

        IsBellWithdrawal = true;
        IsCombatUnlocked = false;
        sharedCombatAlertActive = false;
        var doors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        var actors = new List<VillagerController>(enemies);
        foreach (var actor in actors)
        {
            if (actor != null && actor.isActiveAndEnabled && actor.IsRuntimeInitialized && !actor.IsDead)
            {
                actor.BeginBellWithdrawal(destination, cover, doors);
            }
        }
    }

    /// <summary>RegisterEnemy 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;VillagerQTEParticipant&gt;()、EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)、EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.Contains(VillagerController)、VillagerController.ReceiveVillagePursuitOrder(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RecoverManagerRegistration()、VillagerController.Start()；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()、EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant)、EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、List&lt;VillagerController&gt;.Add(VillagerController)、List&lt;VillagerController&gt;.Contains(VillagerController)、VillagerController.ReceiveVillagePursuitOrder(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.RegisterPromptCandidate(VillagerQTEParticipant) 的参数、作为 EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController) 的参数、作为 List&lt;VillagerController&gt;.Add(VillagerController) 的参数 等共 5 处。</param>
    internal bool HasNearbyBellReaction(LeonController player)
    {
        if (player == null || player.playerCamera == null)
        {
            return false;
        }

        foreach (var actor in enemies)
        {
            if (actor == null || !actor.isActiveAndEnabled || !actor.IsBellLookReady
                || (actor.transform.position - player.transform.position).sqrMagnitude > 12f * 12f)
            {
                continue;
            }

            if (LeonQTECoordinator.IsPromptVisible(player.playerCamera, actor, actor.transform.position + Vector3.up * 1.6f))
            {
                return true;
            }
        }
        return false;
    }

    public void RegisterEnemy(VillagerController enemy)
    {
        if (enemy != null)
        {
            RegisterPromptCandidate(enemy.GetComponent<VillagerQTEParticipant>());
        }

        if (enemy == null || enemy.assassinationShowcaseIdle || enemies.Contains(enemy))
        {
            return;
        }

        enemies.Add(enemy);
        tactics[enemy] = new EnemyTactics();
    }

    /// <summary>UnregisterEnemy 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;VillagerQTEParticipant&gt;()、Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController)、EnemyAIManager.ClearPendingQTERequest()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAIManager.UnregisterPromptCandidate(VillagerQTEParticipant)、HashSet&lt;VillagerController&gt;.Remove(VillagerController) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnDestroy()；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()、Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController)、EnemyAIManager.ClearPendingQTERequest()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAIManager.UnregisterPromptCandidate(VillagerQTEParticipant)、HashSet&lt;VillagerController&gt;.Remove(VillagerController) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController) 的参数、作为 Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController) 的参数、作为 EnemyAIManager.ReleaseCombatReservation(VillagerController) 的参数 等共 7 处。</param>
    public void UnregisterEnemy(VillagerController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        UnregisterPromptCandidate(enemy.GetComponent<VillagerQTEParticipant>());
        ReleaseCombatReservation(enemy);
        combatSlots.Remove(enemy);
        pursuitTargets.Remove(enemy);
        directPressureEnemies.Remove(enemy);
        if (pendingQTEEnemy == enemy)
        {
            FinishQTEWait();
        }

        enemies.Remove(enemy);
        tactics.Remove(enemy);
        ForgetInterception(enemy);
    }

    /// <summary>为即将启动的动作临时占用攻击名额。</summary>
    public bool TryAcquireAttackReservation(VillagerController enemy)
    {
        if (!CanAcquireAttackReservation(enemy))
        {
            return false;
        }

        return attackingEnemies.Add(enemy);
    }

    /// <summary>核对近身资格和当前起手条件。</summary>
    public bool CanAcquireAttackReservation(VillagerController enemy)
    {
        if (!IsReservationCandidateValid(enemy) || attackingEnemies.Contains(enemy) || !CanOpenAttackSlot(enemy))
        {
            return false;
        }

        if (selectedAttackCandidate != enemy && lastAttackReevaluationFrame != Time.frameCount &&
            (!IsReadyAttackCandidate(selectedAttackCandidate) || !CanOpenAttackSlot(selectedAttackCandidate)))
        {
            lastAttackReevaluationFrame = Time.frameCount;
            SelectAttackCandidate();
        }

        return selectedAttackCandidate == enemy;
    }

    /// <summary>TryAcquirePreparationReservation 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。</param>
    public bool TryAcquirePreparationReservation(VillagerController enemy)
    {
        return false;
    }

    /// <summary>NotifyCombatEngaged 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyAIManager.UpdateSharedCombatAlert()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginChase(bool)、EnemyPerceptionModule.BecomeFullyAwareOfPlayer()；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyAIManager.UpdateSharedCombatAlert()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源村民敌人控制器。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数。</param>
    public void NotifyCombatEngaged(VillagerController source)
    {
        if (!IsCombatUnlocked || !IsEnemyUsable(source) || PlayerTarget == null)
        {
            return;
        }

        sharedCombatAlertActive = true;
        nextSharedCombatAlertTime = 0f;
        sharedCombatAlertExpiresAt = Time.time + Mathf.Max(0.1f, sharedCombatAlertDuration);
        UpdateSharedCombatAlert();
    }

    /// <summary>NotifyEnvironmentalDisturbance 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.SetOpen(bool)；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public void NotifyEnvironmentalDisturbance(Vector3 position, float radius)
    {
        if (!IsCombatUnlocked)
        {
            return;
        }

        if (PlayerTarget == null)
        {
            return;
        }

        // alertedAnyEnemy（是否已警觉任意敌人）：使用位置：EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。
bool alertedAnyEnemy = false;
        // radiusSqr（半径平方）：使用位置：EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。
float radiusSqr = Mathf.Max(1f, radius) * Mathf.Max(1f, radius);
        // playerPosition（玩家位置）：使用位置：作为 VillagerController.ReceiveSharedCombatAlert(Vector3) 的参数。
Vector3 playerPosition = PlayerTarget.transform.position;
        // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。
foreach (VillagerController enemy in enemies)
        {
            if (!IsEnemyUsable(enemy) || enemy.IsQTEControlled)
            {
                continue;
            }

            // offset（偏移量）：使用位置：EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。
Vector3 offset = enemy.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            enemy.ReceiveSharedCombatAlert(playerPosition);
            alertedAnyEnemy = true;
        }

        if (!alertedAnyEnemy)
        {
            return;
        }

        sharedCombatAlertActive = true;
        sharedCombatAlertExpiresAt = Time.time + Mathf.Max(0.1f, sharedCombatAlertDuration);
        nextSharedCombatAlertTime = Time.time + Mathf.Max(0.05f, sharedCombatAlertInterval);
    }

    /// <summary>TryAcquireFrontGrabReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind) 的参数。</param>
    public bool TryAcquireFrontGrabReservation(VillagerController enemy)
    {
        return TryAcquireQTEReservation(enemy, QTEKind.FrontGrab);
    }

    /// <summary>CanAcquireFrontGrabReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptFrontGrabQTE.OnUpdate()；下游（含内部回调体） EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind) 的参数。</param>
    public bool CanAcquireFrontGrabReservation(VillagerController enemy)
    {
        return CanAcquireQTEReservation(enemy, QTEKind.FrontGrab);
    }

    /// <summary>CompleteFrontGrabAttempt 的业务处理入口；本方法直接依赖 EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool) 的参数。</param>
    /// <param name="sessionStarted">中文含义：会话是否已开始。使用位置：作为 EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool) 的参数。</param>
    public void CompleteFrontGrabAttempt(VillagerController enemy, bool sessionStarted)
    {
        CompleteQTEAttempt(enemy, QTEKind.FrontGrab, sessionStarted);
    }

    /// <summary>TryAcquireQTEReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.ClearPendingQTERequest()、EnemyAIManager.GetNextQTEAllowedTime(QTEKind)、EnemyAIManager.IsReservationCandidateValid(VillagerController)、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)、EnemyAIManager.ValidateCombatReservations()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.TryAcquireFrontGrabReservation(VillagerController)、TryStartEnemyQTEBase.OnUpdate()、TryStartFrontGrabQTE.TryStartStandingGrab(LeonQTECoordinator, QTEKind)、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)；下游（含内部回调体） EnemyAIManager.ClearPendingQTERequest()、EnemyAIManager.GetNextQTEAllowedTime(QTEKind)、EnemyAIManager.IsReservationCandidateValid(VillagerController)、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)、EnemyAIManager.ValidateCombatReservations()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.IsReservationCandidateValid(VillagerController) 的参数、作为 EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind) 的参数、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)。</param>
    /// <param name="kind">中文含义：种类。使用位置：作为 EnemyAIManager.GetNextQTEAllowedTime(QTEKind) 的参数、作为 EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind) 的参数、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)。</param>
    public bool TryAcquireQTEReservation(VillagerController enemy, QTEKind kind)
    {
        if (kind == QTEKind.LegBite || !IsReservationCandidateValid(enemy) || Time.time < GetNextQTEAllowedTime(kind))
        {
            return false;
        }

        ValidateCombatReservations();
        if (qteReservedEnemy == enemy)
        {
            return true;
        }

        RegisterPendingQTERequest(enemy, kind);
        if (qteReservedEnemy != null || pendingQTEEnemy != enemy || pendingQTEKind != kind || attackingEnemies.Count > 0 || Time.time < nextAttackAllowedTime)
        {
            return false;
        }

        qteReservedEnemy = enemy;
        qteReservationAcquiredAt = Time.time;
        ClearPendingQTERequest();
        return true;
    }

    /// <summary>CanAcquireQTEReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.GetNextQTEAllowedTime(QTEKind)、EnemyAIManager.IsReservationCandidateValid(VillagerController)、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)、EnemyAIManager.ValidateCombatReservations()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CanAttemptEnemyQTEBase.OnUpdate()、CanAttemptFrontGrabQTE.OnUpdate()、CanCrawlAttackNow.OnUpdate()、EnemyAIManager.CanAcquireFrontGrabReservation(VillagerController)；下游（含内部回调体） EnemyAIManager.GetNextQTEAllowedTime(QTEKind)、EnemyAIManager.IsReservationCandidateValid(VillagerController)、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)、EnemyAIManager.ValidateCombatReservations()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.IsReservationCandidateValid(VillagerController) 的参数、作为 EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind) 的参数、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)。</param>
    /// <param name="kind">中文含义：种类。使用位置：作为 EnemyAIManager.GetNextQTEAllowedTime(QTEKind) 的参数、作为 EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind) 的参数、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)。</param>
    public bool CanAcquireQTEReservation(VillagerController enemy, QTEKind kind)
    {
        if (kind == QTEKind.LegBite || !IsReservationCandidateValid(enemy))
        {
            return false;
        }

        ValidateCombatReservations();
        if (Time.time < GetNextQTEAllowedTime(kind))
        {
            return false;
        }

        if (qteReservedEnemy == enemy)
        {
            return true;
        }

        RegisterPendingQTERequest(enemy, kind);
        return qteReservedEnemy == null && pendingQTEEnemy == enemy && pendingQTEKind == kind && attackingEnemies.Count == 0 && Time.time >= nextAttackAllowedTime;
    }

    /// <summary>CompleteQTEAttempt 的业务处理入口；本方法直接依赖 EnemyAIManager.ReleaseQTEReservation(VillagerController, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.CompleteFrontGrabAttempt(VillagerController, bool)、TryStartEnemyQTEBase.OnUpdate()、TryStartFrontGrabQTE.TryStartStandingGrab(LeonQTECoordinator, QTEKind)、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)；下游（含内部回调体） EnemyAIManager.ReleaseQTEReservation(VillagerController, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.ReleaseQTEReservation(VillagerController, bool) 的参数。</param>
    /// <param name="kind">中文含义：种类。使用位置：EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。</param>
    /// <param name="sessionStarted">中文含义：会话是否已开始。使用位置：作为 EnemyAIManager.ReleaseQTEReservation(VillagerController, bool) 的参数、EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)。</param>
    public void CompleteQTEAttempt(VillagerController enemy, QTEKind kind, bool sessionStarted)
    {
        if (sessionStarted)
        {
            if (kind == QTEKind.LegBite)
            {
                nextLegBiteAllowedTime = Time.time + legBiteGlobalCooldown;
            }
            else if (kind == QTEKind.ChainsawExecution)
            {
                nextChainsawExecutionAllowedTime = Time.time + chainsawExecutionGlobalCooldown;
            }
            else
            {
                nextFrontGrabAllowedTime = Time.time + frontGrabGlobalCooldown;
            }
        }

        ReleaseQTEReservation(enemy, sessionStarted);
        if (!sessionStarted)
        {
            FinishQTEWait();
        }
    }

    /// <summary>GetNextQTEAllowedTime 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：EnemyAIManager.GetNextQTEAllowedTime(QTEKind)。</param>
    private float GetNextQTEAllowedTime(QTEKind kind)
    {
        if (kind == QTEKind.LegBite)
        {
            return nextLegBiteAllowedTime;
        }

        if (kind == QTEKind.ChainsawExecution)
        {
            return nextChainsawExecutionAllowedTime;
        }

        return nextFrontGrabAllowedTime;
    }

    /// <summary>ReleaseQTEReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.ClearPendingQTERequest()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)；下游（含内部回调体） EnemyAIManager.ClearPendingQTERequest()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：EnemyAIManager.ReleaseQTEReservation(VillagerController, bool)。</param>
    /// <param name="applyAttackRecovery">中文含义：是否应用攻击恢复。使用位置：EnemyAIManager.ReleaseQTEReservation(VillagerController, bool)。</param>
    private void ReleaseQTEReservation(VillagerController enemy, bool applyAttackRecovery)
    {
        if (qteReservedEnemy == enemy)
        {
            qteReservedEnemy = null;
            if (applyAttackRecovery)
            {
                nextAttackAllowedTime = Mathf.Max(nextAttackAllowedTime, Time.time + attackRecoveryDelay);
            }
        }

        if (pendingQTEEnemy == enemy)
        {
            ClearPendingQTERequest();
        }
    }

    /// <summary>HasAttackReservation 的业务处理入口；本方法直接依赖 EnemyAIManager.ValidateCombatReservations()、HashSet&lt;VillagerController&gt;.Contains(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ShouldHoldCombatPosition.get；下游（含内部回调体） EnemyAIManager.ValidateCombatReservations()、HashSet&lt;VillagerController&gt;.Contains(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 HashSet&lt;VillagerController&gt;.Contains(VillagerController) 的参数、EnemyAIManager.HasAttackReservation(VillagerController)。</param>
    public bool HasAttackReservation(VillagerController enemy)
    {
        ValidateCombatReservations();
        return attackingEnemies.Contains(enemy) || qteReservedEnemy == enemy;
    }

    /// <summary>HasPreparationReservation 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。</param>
    public bool HasPreparationReservation(VillagerController enemy)
    {
        return false;
    }

    /// <summary>在受击、停用等生命周期切换时释放当前战斗占用。</summary>
    public void ReleaseCombatReservation(VillagerController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (committedAttacks.TryGetValue(enemy, out var handle))
        {
            ReleaseAttack(enemy, handle);
        }
        else
        {
            attackingEnemies.Remove(enemy);
        }

        if (qteReservedEnemy == enemy)
        {
            ReleaseQTEReservation(enemy, true);
        }
    }

    /// <summary>FillCombatTimelineSnapshot 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)、HashSet&lt;VillagerController&gt;.Contains(VillagerController)、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CollectCombatTimelineSnapshot()；下游（含内部回调体） Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)、HashSet&lt;VillagerController&gt;.Contains(VillagerController)、Mathf.Clamp(int, int, int)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment) 的参数、作为 EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController) 的参数、作为 HashSet&lt;VillagerController&gt;.Contains(VillagerController) 的参数。</param>
    /// <param name="snapshot">中文含义：快照。按引用传入，读写会作用于调用方原存储位置。使用位置：EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。</param>
    internal void FillCombatTimelineSnapshot(VillagerController enemy, ref CombatTimelineSnapshot snapshot)
    {
        snapshot.permission = DescribeAttackPermission(enemy);
        snapshot.attackCharacterCount = attackingEnemies.Count;
        // sequence（序号）：使用位置：EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。
int sequence = 0;
        // attacker（攻击者）：每轮从攻击中敌人集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。
foreach (var attacker in attackingEnemies)
        {
            // id（编号）：使用位置：EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。
int id;
            if (attacker != null)
            {
                id = attacker.diagnosticEnemyNumber;
            }
            else
            {
                id = 0;
            }

            switch (sequence++)
            {
                case 0:
                    snapshot.attackerOne = id;
                    break;
                case 1:
                    snapshot.attackerTwo = id;
                    break;
                case 2:
                    snapshot.attackerThree = id;
                    break;
                case 3:
                    snapshot.attackerFour = id;
                    break;
            }
        }

        if (qteReservedEnemy != null)
        {
            snapshot.grabHolder = qteReservedEnemy.diagnosticEnemyNumber;
        }
        else
        {
            snapshot.grabHolder = 0;
        }

        if (pendingQTEEnemy != null)
        {
            snapshot.grabWaitingActor = pendingQTEEnemy.diagnosticEnemyNumber;
        }
        else
        {
            snapshot.grabWaitingActor = 0;
        }

        snapshot.grabWaiting = Mathf.Max(0, pendingQTEExpiresAt - Time.time);
        snapshot.attackInterval = Mathf.Max(0, nextAttackAllowedTime - Time.time);
        snapshot.directChase = directPressureEnemies.Contains(enemy);
        snapshot.hasSurroundSlot = combatSlots.TryGetValue(enemy, out var /* 中文含义：槽位。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ slot);
        if (snapshot.hasSurroundSlot)
        {
            snapshot.surroundSlot = slot.slotIndex;
            snapshot.surroundTarget = slot.destination;
        }
    }

    /// <summary>BuildCombatTimelineSummary 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Number(VillagerController)、float.ToString(string)、HashSet&lt;VillagerController&gt;.Contains(VillagerController) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Number(VillagerController)、float.ToString(string)、HashSet&lt;VillagerController&gt;.Contains(VillagerController) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment) 的参数、作为 EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController) 的参数、作为 EnemyCombatTimeline.IsRecord(VillagerController) 的参数 等共 4 处。</param>
    internal string BuildCombatTimelineSummary(VillagerController enemy)
    {
        if (!EnemyCombatTimeline.IsRecord(enemy))
        {
            return string.Empty;
        }

        // 诊断不能验证或清理许可，否则记录器本身会改变待测行为。
        string wait = DescribeAttackPermission(enemy);
        // result（结果）：使用位置：EnemyAIManager.BuildCombatTimelineSummary(VillagerController)。
string result = "许可情况=" + wait + "，当前攻击人数=" + attackingEnemies.Count + "，攻击者序号=";
        // attacker（攻击者）：每轮从攻击中敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyCombatTimeline.Number(VillagerController) 的参数。
foreach (VillagerController attacker in attackingEnemies)
        {
            result += EnemyCombatTimeline.Number(attacker) + " ";
        }

        result += "，抓取持有者=" + EnemyCombatTimeline.Number(qteReservedEnemy) + "，抓取等待者=" + EnemyCombatTimeline.Number(pendingQTEEnemy) + "，等待剩余=" + Mathf.Max(0f, pendingQTEExpiresAt - Time.time).ToString("F3") + "，全局攻击间隔剩余=" + Mathf.Max(0f, nextAttackAllowedTime - Time.time).ToString("F3") + "，直接追击名额=" + EnemyCombatTimeline.Describe(directPressureEnemies.Contains(enemy));
        if (combatSlots.TryGetValue(enemy, out CombatSlotAssignment /* 中文含义：槽位。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ slot))
        {
            result += "，包围槽=" + slot.slotIndex + "，包围目标=" + slot.destination.ToString("F3");
        }

        return result;
    }

    /// <summary>路径失效时清理旧目标并安排重新选点。</summary>
    public void InvalidatePursuitDestination(VillagerController enemy)
    {
        if (enemy == null)
        {
            return;
        }

        pursuitTargets.Remove(enemy);
        InvalidateTacticalDestination(enemy);
        if (interceptionEnemies[0] == enemy || interceptionEnemies[1] == enemy)
        {
            RejectInterception(enemy);
        }
    }

    /// <summary>根据通行、近身进攻或外围走位选择当前目标。</summary>
    public Vector3 GetCombatDestination(VillagerController enemy, Vector3 playerPosition)
    {
        if (enemy == null)
        {
            return playerPosition;
        }

        if (!CanEngage(enemy))
        {
            return enemy.transform.position;
        }

        if (ShouldPressAtCloseRange(enemy) || directPressureEnemies.Contains(enemy))
        {
            return ResolvePursuitDestination(enemy, playerPosition);
        }

        if (TryGetInterceptionDestination(enemy, out var interceptionPoint))
        {
            return interceptionPoint;
        }

        if (enemy.NeedsCombatTraversal())
        {
            return ResolvePursuitDestination(enemy, playerPosition);
        }

        return GetTacticalDestination(enemy, playerPosition);
    }

    /// <summary>
    /// 判断敌人当前抵达的寻路终点是否就是管理器分配的近身包围槽。
    /// 包围槽本来就位于普通攻击距离之外，不能再被控制器当成“错误投影点”
    /// 触发停滞恢复，否则槽位会被周期清除并重新分配，敌人便会围着静止的
    /// 里昂反复换边打转。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.HasIncompletePursuitArrival.get；下游（含内部回调体） EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment) 的参数。</param>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment) 的参数。</param>
    /// <param name="navigationDestination">中文含义：导航目的地。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数。</param>
    /// <param name="destinationTolerance">中文含义：目的地容差。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public bool IsStableSurroundArrival(VillagerController enemy, Vector3 playerPosition, Vector3 navigationDestination, float destinationTolerance)
    {
        if (!TryGetStableSurroundAssignment(enemy, playerPosition, out CombatSlotAssignment /* 中文含义：赋值。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ assignment))
        {
            return false;
        }

        // tolerance（容差）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.IsStableSurroundArrival(VillagerController, Vector3, Vector3, float)。
float tolerance = Mathf.Max(0.1f, destinationTolerance);
        return HorizontalDistance(assignment.destination, navigationDestination) <= tolerance && Mathf.Abs(assignment.destination.y - navigationDestination.y) <= Mathf.Max(0.35f, tolerance);
    }

    /// <summary>
    /// 判断敌人是否已经进入自己的稳定包围槽。该判定使用敌人的实际位置，
    /// 因而即使拥挤碰撞使寻路组件没有切换为到达状态，也能结束无意义的
    /// 局部避让并稳定面向里昂等待攻击机会。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.ShouldHoldCombatPosition.get；下游（含内部回调体） EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment) 的参数、作为 Mathf.Abs(float) 的参数。</param>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment) 的参数。</param>
    public bool IsAtStableSurroundPosition(VillagerController enemy, Vector3 playerPosition)
    {
        if (!TryGetStableSurroundAssignment(enemy, playerPosition, out CombatSlotAssignment /* 中文含义：赋值。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ assignment))
        {
            return false;
        }

        // arrivalDistance（抵达距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyAIManager.IsAtStableSurroundPosition(VillagerController, Vector3)。
float arrivalDistance = Mathf.Max(0.1f, surroundSlotArrivalDistance);
        return HorizontalDistance(enemy.transform.position, assignment.destination) <= arrivalDistance && Mathf.Abs(enemy.transform.position.y - assignment.destination.y) <= Mathf.Max(0.35f, arrivalDistance);
    }

    /// <summary>TryGetStableSurroundAssignment 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.IsAtStableSurroundPosition(VillagerController, Vector3)、EnemyAIManager.IsStableSurroundArrival(VillagerController, Vector3, Vector3, float)；下游（含内部回调体） Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment)、EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.Remove(VillagerController) 的参数、作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment) 的参数、作为 EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3) 的参数 等共 4 处。</param>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3) 的参数、作为 EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3) 的参数 等共 4 处。</param>
    /// <param name="assignment">中文含义：赋值。输出参数，由本入口写入并交给调用方。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.CombatSlotAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.CombatSlotAssignment) 的参数、作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3) 的参数 等共 5 处。</param>
    private bool TryGetStableSurroundAssignment(VillagerController enemy, Vector3 playerPosition, out CombatSlotAssignment assignment)
    {
        assignment = default;
        if (enemy == null)
        {
            return false;
        }

        // 直接追击名额变更后，旧包围槽不能继续让新追击者原地等待。
        if (directPressureEnemies.Contains(enemy))
        {
            return false;
        }

        if (!combatSlots.TryGetValue(enemy, out assignment) || assignment.isFlankStaging || assignment.slotIndex < 0 || !IsFormationSlotOnPlayerSide(playerPosition, assignment.destination))
        {
            return false;
        }

        return HorizontalDistance(assignment.playerAnchor, playerPosition) < Mathf.Max(0.1f, surroundSlotPlayerMoveThreshold) && Mathf.Abs(assignment.playerAnchor.y - playerPosition.y) < Mathf.Max(0.1f, pursuitTargetPlayerHeightThreshold);
    }

    /// <summary>读取当前敌人的近身进攻资格。</summary>
    public bool ShouldUseDirectPressurePursuit(VillagerController enemy, Vector3 playerPosition)
    {
        return CanEngage(enemy) && ShouldFollowPlayerDirectly(enemy);
    }

    /// <summary>ResolvePursuitDestination 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.PursuitTargetAssignment)、EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.PursuitTargetAssignment.PursuitTargetAssignment()、Mathf.Abs(float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.GetCombatDestination(VillagerController, Vector3)；下游（含内部回调体） Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController)、Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.PursuitTargetAssignment)、EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.PursuitTargetAssignment.PursuitTargetAssignment()、Mathf.Abs(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.Remove(VillagerController) 的参数、作为 Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.this[VillagerController] 的参数、作为 Dictionary&lt;VillagerController, EnemyAIManager.PursuitTargetAssignment&gt;.TryGetValue(VillagerController, out EnemyAIManager.PursuitTargetAssignment) 的参数。</param>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3)。</param>
    private Vector3 ResolvePursuitDestination(VillagerController enemy, Vector3 playerPosition)
    {
        // hasAssignment（是否具有赋值）：保存 Dictionary<VillagerController, EnemyAIManager.PursuitTargetAssignment>.TryGetValue(VillagerController, out EnemyAIManager.PursuitTargetAssignment) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3)。
bool hasAssignment = pursuitTargets.TryGetValue(enemy, out PursuitTargetAssignment /* 中文含义：赋值。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ assignment);
        if (hasAssignment && Mathf.Abs(assignment.playerAnchor.y - playerPosition.y) >= Mathf.Max(0.1f, pursuitTargetPlayerHeightThreshold))
        {
            pursuitTargets.Remove(enemy);
            hasAssignment = false;
        }

        if (hasAssignment && Time.time < assignment.expiresAt && HorizontalDistance(assignment.playerAnchor, playerPosition) < Mathf.Max(0.1f, pursuitTargetPlayerMoveThreshold))
        {
            return assignment.destination;
        }

        // 管理器只负责稳定追击意图，目标投影和路径可达性由敌人的
        // 路径组件统一处理。这里再次投影会让跨层失败被错误地降级为原地。
        pursuitTargets[enemy] = new PursuitTargetAssignment
        {
            playerAnchor = playerPosition,
            destination = playerPosition,
            expiresAt = Time.time + Mathf.Max(0.1f, pursuitTargetHoldDuration)
        };
        return playerPosition;
    }

    /// <summary>GetNearestEnemy 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)、object.ReferenceEquals(object, object)、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)、object.ReferenceEquals(object, object)、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。</param>
    public Transform GetNearestEnemy(Vector3 position)
    {
        // nearest（最近）：使用位置：EnemyAIManager.GetNearestEnemy(Vector3)。
float nearest = float.MaxValue;
        // nearestEnemy（最近敌人）：使用位置：EnemyAIManager.GetNearestEnemy(Vector3)。
VillagerController nearestEnemy = null;
        // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、EnemyAIManager.GetNearestEnemy(Vector3)。
foreach (VillagerController enemy in enemies)
        {
            if (!IsEnemyUsable(enemy))
            {
                continue;
            }

            // distance（距离）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.GetNearestEnemy(Vector3)。
float distance = Vector3.Distance(position, enemy.transform.position);
            if (distance < nearest)
            {
                nearest = distance;
                nearestEnemy = enemy;
            }
        }

        // receiverValue29（判空前缓存的最近敌人）：从最近敌人读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.GetNearestEnemy(Vector3)。
global::VillagerController receiverValue29 = nearestEnemy;
        if (!global::System.Object.ReferenceEquals(receiverValue29, null))
        {
            return receiverValue29.transform;
        }
        else
        {
            return default;
        }
    }

    private bool CanHearVillageShot(VillagerController enemy, Vector3 origin, float radius, float squareDistance)
    {
        float blockedRadius = radius * .6f;
        if (squareDistance <= blockedRadius * blockedRadius)
        {
            return true;
        }

        Vector3 direction = enemy.transform.position + Vector3.up * 1.4f - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            bool onPlayer = PlayerTarget != null && hit.transform.IsChildOf(PlayerTarget.transform);
            if (!hit.transform.IsChildOf(enemy.transform) && !onPlayer
                && hit.collider.GetComponentInParent<VillagerController>() == null)
            {
                return false;
            }
        }
        return true;
    }

    private void UpdateVillageFootsteps(Vector3 position)
    {
        if (!AwaitsVillageDiscovery || !CanObserveVillagePlayer || !PlayerTarget.IsGrounded || PlayerTarget.IsCrouching
            || PlayerTarget.MoveInput.sqrMagnitude < .025f || Time.time < nextVillageFootstep
            || !hasPreviousPlayerPosition || HorizontalDistance(previousPlayerPosition, position) < .035f)
        {
            return;
        }

        nextVillageFootstep = Time.time + .32f;
        float radius = 2f;
        if (PlayerTarget.IsEffectiveSprinting)
        {
            radius = 5f;
        }
        foreach (VillagerController enemy in enemies)
        {
            if (IsEnemyUsable(enemy) && !enemy.IsQTEControlled &&
                (enemy.transform.position - position).sqrMagnitude <= radius * radius
                && CanHearVillageShot(enemy, position + Vector3.up * .2f, radius, (enemy.transform.position - position).sqrMagnitude))
            {
                enemy.perceptionModule.NoticeVillageFootstep(position);
            }
        }
    }

    /// <summary>HandleWeaponFired 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyAIManager.UpdateSharedCombatAlert()、Mathf.Max(float, float)、VillagerController.ReceiveGunshot(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)、EnemyAIManager.UpdateSharedCombatAlert()、Mathf.Max(float, float)、VillagerController.ReceiveGunshot(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="noise">中文含义：噪声。使用位置：作为 VillagerController.ReceiveGunshot(Vector3) 的参数、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。</param>
    private void HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent noise)
    {
        if (AwaitsVillageDiscovery)
        {
            VillagerController listener = null;
            float closestDistance = noise.Radius * noise.Radius;
            foreach (VillagerController enemy in enemies)
            {
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                if (enemy.IsQTEControlled)
                {
                    continue;
                }

                float distance = (enemy.transform.position - noise.Position).sqrMagnitude;
                if (distance <= closestDistance && CanHearVillageShot(enemy, noise.Position, noise.Radius, distance))
                {
                    listener = enemy;
                    closestDistance = distance;
                }
            }

            DiscoverVillageCombat(listener);
        }

        if (!IsCombatUnlocked)
        {
            return;
        }

        // radiusSqr（半径平方）：使用位置：EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。
float radiusSqr = noise.Radius * noise.Radius;
        // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。
foreach (VillagerController enemy in enemies)
        {
            if (!IsEnemyUsable(enemy))
            {
                continue;
            }

            // offset（偏移量）：使用位置：EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。
Vector3 offset = enemy.transform.position - noise.Position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr)
            {
                enemy.ReceiveGunshot(noise.Position);
            }
        }

        if (PlayerTarget != null)
        {
            sharedCombatAlertActive = true;
            nextSharedCombatAlertTime = 0f;
            sharedCombatAlertExpiresAt = Time.time + Mathf.Max(0.1f, sharedCombatAlertDuration);
            UpdateSharedCombatAlert();
        }
    }

    /// <summary>UpdateSharedCombatAlert 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)、EnemyAIManager.NotifyCombatEngaged(VillagerController)、EnemyAIManager.Update()；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateSharedCombatAlert()
    {
        if (sharedCombatAlertActive && Time.time > sharedCombatAlertExpiresAt)
        {
            sharedCombatAlertActive = false;
            return;
        }

        if (!IsCombatUnlocked || !sharedCombatAlertActive || PlayerTarget == null || Time.time < nextSharedCombatAlertTime)
        {
            return;
        }

        nextSharedCombatAlertTime = Time.time + Mathf.Max(0.05f, sharedCombatAlertInterval);
        // playerPosition（玩家位置）：使用位置：作为 VillagerController.ReceiveSharedCombatAlert(Vector3) 的参数、EnemyAIManager.UpdateSharedCombatAlert()。
Vector3 playerPosition = PlayerTarget.transform.position;
        // radiusSqr（半径平方）：使用位置：EnemyAIManager.UpdateSharedCombatAlert()。
float radiusSqr = sharedCombatAlertRadius * sharedCombatAlertRadius;
        // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、EnemyAIManager.UpdateSharedCombatAlert()。
foreach (VillagerController enemy in enemies)
        {
            if (!IsEnemyUsable(enemy) || enemy.IsQTEControlled)
            {
                continue;
            }

            // offset（偏移量）：使用位置：EnemyAIManager.UpdateSharedCombatAlert()。
Vector3 offset = enemy.transform.position - playerPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr)
            {
                enemy.ReceiveSharedCombatAlert(playerPosition);
            }
        }
    }

    /// <summary>UpdateMovementNoiseAwareness 的业务处理入口；本方法直接依赖 EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.Update()；下游（含内部回调体） EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.IsEnemyUsable(VillagerController)、Mathf.Max(float, float)、VillagerController.ReceiveSharedCombatAlert(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateMovementNoiseAwareness()
    {
        if (PlayerTarget == null)
        {
            return;
        }

        // playerPosition（玩家位置）：使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 VillagerController.ReceiveSharedCombatAlert(Vector3) 的参数、EnemyAIManager.UpdateMovementNoiseAwareness()。
Vector3 playerPosition = PlayerTarget.transform.position;
        if (!IsCombatUnlocked)
        {
            UpdateVillageFootsteps(playerPosition);
            previousPlayerPosition = playerPosition;
            hasPreviousPlayerPosition = true;
            return;
        }

        if (!hasPreviousPlayerPosition)
        {
            previousPlayerPosition = playerPosition;
            hasPreviousPlayerPosition = true;
            return;
        }

        // elapsed（已用）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
float elapsed = Mathf.Max(0.01f, updateInterval);
        // movementSpeed（移动速度）：使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
float movementSpeed = HorizontalDistance(previousPlayerPosition, playerPosition) / elapsed;
        previousPlayerPosition = playerPosition;
        if (movementSpeed < Mathf.Max(0.1f, movementHearingSpeedThreshold))
        {
            return;
        }

        // radius（半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
float radius = Mathf.Max(1f, movementHearingRadius);
        // radiusSqr（半径平方）：使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
float radiusSqr = radius * radius;
        // alertedAnyEnemy（是否已警觉任意敌人）：使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
bool alertedAnyEnemy = false;
        // enemy（敌人）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、EnemyAIManager.UpdateMovementNoiseAwareness()。
foreach (VillagerController enemy in enemies)
        {
            if (!IsEnemyUsable(enemy) || enemy.IsQTEControlled)
            {
                continue;
            }

            // offset（偏移量）：使用位置：EnemyAIManager.UpdateMovementNoiseAwareness()。
Vector3 offset = enemy.transform.position - playerPosition;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            enemy.ReceiveSharedCombatAlert(playerPosition);
            alertedAnyEnemy = true;
        }

        if (!alertedAnyEnemy)
        {
            return;
        }

        sharedCombatAlertActive = true;
        sharedCombatAlertExpiresAt = Time.time + Mathf.Max(0.1f, sharedCombatAlertDuration);
        nextSharedCombatAlertTime = Time.time + Mathf.Max(0.05f, sharedCombatAlertInterval);
    }

    /// <summary>UpdateEnemyStates 的业务处理入口；本方法直接依赖 EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、List&lt;VillagerController&gt;.RemoveAt(int)、Vector3.Distance(Vector3, Vector3)、VillagerController.ReceiveVillagePursuitOrder(Vector3)、VillagerController.SetLODActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.Update()；下游（含内部回调体） EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)、List&lt;VillagerController&gt;.RemoveAt(int)、Vector3.Distance(Vector3, Vector3)、VillagerController.ReceiveVillagePursuitOrder(Vector3)、VillagerController.SetLODActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateEnemyStates()
    {
        if (PlayerTarget == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<VillagerController>.RemoveAt(int) 的参数、作为 List<VillagerController>.this[int] 的参数、EnemyAIManager.UpdateEnemyStates()。
for (int i = enemies.Count - 1; i >= 0; i--)
        {
            // enemy（敌人）：使用位置：作为 EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、EnemyAIManager.UpdateEnemyStates()。
VillagerController enemy = enemies[i];
            if (enemy == null)
            {
                if (!ReferenceEquals(enemy, null))
                {
                    tactics.Remove(enemy);
                    directPressureEnemies.Remove(enemy);
                    ForgetInterception(enemy);
                    combatSlots.Remove(enemy);
                    pursuitTargets.Remove(enemy);
                }

                enemies.RemoveAt(i);
                continue;
            }

            if (enemy.IsDead)
            {
                continue;
            }

            // distance（距离）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.UpdateEnemyStates()。
float distance = Vector3.Distance(enemy.transform.position, PlayerTarget.transform.position);
            // 入村战斗开始后，已经接到追击命令的敌人必须持续模拟。
            // 远距离停用会取消路径并清除战斗状态，玩家再次靠近时才恢复，
            // 视觉上就像敌人主动返回出生守位。
            // shouldRemainActiveForPursuit（是否应当保持生效用于追击）：使用位置：EnemyAIManager.UpdateEnemyStates()。
bool shouldRemainActiveForPursuit = IsCombatUnlocked && enemy.IsCombatEngaged;
            // shouldBeActive（是否应当保持生效）：使用位置：作为 VillagerController.SetLODActive(bool) 的参数、EnemyAIManager.UpdateEnemyStates()。
bool shouldBeActive = shouldRemainActiveForPursuit || distance < maxActiveDistance;
            enemy.SetLODActive(shouldBeActive);
            // 区域切换广播可能发生在敌人被快速反应事件接管、暂时停用
            // 或刚完成注册的帧。追击中的敌人必须持续对齐当前版本，不能
            // 依赖那一次广播；控制器内部会忽略已经接收过的版本。
            SynchronizePursuitRegionVersion(enemy);
        }
    }

    /// <summary>SynchronizePursuitRegionVersion 的业务处理入口；本方法直接依赖 VillagerController.ReceivePursuitRegionChange(int, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.RegisterEnemy(VillagerController)、EnemyAIManager.UpdateEnemyStates()；下游（含内部回调体） VillagerController.ReceivePursuitRegionChange(int, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：EnemyAIManager.SynchronizePursuitRegionVersion(VillagerController)。</param>
    private void SynchronizePursuitRegionVersion(VillagerController enemy)
    {
        if (!hasPlayerPursuitRegion || PlayerTarget == null || enemy == null || enemy.IsDead || enemy.IsQTEControlled || !enemy.IsCombatEngaged)
        {
            return;
        }

        enemy.ReceivePursuitRegionChange(pursuitRegionVersion, PlayerTarget.transform.position);
    }

    /// <summary>
    /// 战术包围是围绕里昂当前活动区域的局部站位，不是跨门导航目标。
    /// 两点之间若必须通过一扇有限门洞，则候选点属于另一房间，必须
    /// 排除；敌人跨房间的追击由敌人控制器的门洞阶段单独负责。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)、EnemyAIManager.SelectSurroundSlot(VillagerController, Vector3, float, int, bool, out int)、EnemyAIManager.TryGetStableSurroundAssignment(VillagerController, Vector3, out EnemyAIManager.CombatSlotAssignment)；下游（含内部回调体） DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController)、EnemyAIManager.HorizontalDistance(Vector3, Vector3)、EnemyAIManager.TryGetCurrentPlayerPursuitRegion(out int)、PursuitNavigationTopology.TryGetRegion(Vector3, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">中文含义：玩家位置。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数、作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数。</param>
    /// <param name="candidate">中文含义：候选。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数、作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数。</param>
    private bool IsFormationSlotOnPlayerSide(Vector3 playerPosition, Vector3 candidate)
    {
        // hasAuthoritativePlayerRegion（是否具有权威玩家区域）：使用位置：EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)。
bool hasAuthoritativePlayerRegion = TryGetCurrentPlayerPursuitRegion(out int /* 中文含义：玩家区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ playerRegion) && PlayerTarget != null && HorizontalDistance(PlayerTarget.transform.position, playerPosition) <= 0.1f;
        if (!hasAuthoritativePlayerRegion && !PursuitNavigationTopology.TryGetRegion(playerPosition, out playerRegion))
        {
            return !DoorController.TryFindPassageTowardDestination(playerPosition, candidate, out _);
        }

        if (PursuitNavigationTopology.TryGetRegion(candidate, out int /* 中文含义：候选区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ candidateRegion))
        {
            return playerRegion == candidateRegion;
        }

        return !DoorController.TryFindPassageTowardDestination(playerPosition, candidate, out _);
    }

    /// <summary>HorizontalDistance 的业务处理入口；本方法直接依赖 Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.GetCombatDestination(VillagerController, Vector3)、EnemyAIManager.GetDirectPressureScore(VillagerController, Vector3)、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)、EnemyAIManager.IsAtStableSurroundPosition(VillagerController, Vector3)、EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3) 等共 10 处；下游（含内部回调体） Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">中文含义：首个。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、EnemyAIManager.HorizontalDistance(Vector3, Vector3)。</param>
    /// <param name="second">中文含义：第二。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、EnemyAIManager.HorizontalDistance(Vector3, Vector3)。</param>
    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    /// <summary>清理已结束、失效或被打断的战斗占用。</summary>
    private void ValidateCombatReservations()
    {
        ValidateAttackHandles();
        // 交接留出短暂窗口，超过窗口仍未启动会话的孤立许可必须释放。
        if (!IsReservationCandidateValid(qteReservedEnemy) || (Time.time > qteReservationAcquiredAt + 0.25f && !(qteReservedEnemy.combatModule != null && qteReservedEnemy.combatModule.HasGrabWindup) && (qteReservedEnemy.GetComponent<VillagerQTEParticipant>() == null || qteReservedEnemy.GetComponent<VillagerQTEParticipant>().ActiveSessionId == 0)))
        {
            qteReservedEnemy = null;
        }

        HasPendingQTERequest();
    }

    /// <summary>记录本轮快速反应事件候选及等待截止时间。</summary>
    private void RegisterPendingQTERequest(VillagerController enemy, QTEKind kind)
    {
        if (!IsQTERequestEligible(enemy, kind))
        {
            return;
        }

        // hasPendingRequest（是否存在待处理的请求）：保存 EnemyAIManager.HasPendingQTERequest() 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)。
bool hasPendingRequest = HasPendingQTERequest();
        if (hasPendingRequest && pendingQTEEnemy == enemy && pendingQTEKind == kind)
        {
            return;
        }

        if (!hasPendingRequest && Time.time < nextQTEPriorityAllowedTime)
        {
            return;
        }

        if (hasPendingRequest && GetQTERequestPriority(kind) <= GetQTERequestPriority(pendingQTEKind))
        {
            return;
        }

        pendingQTEEnemy = enemy;
        pendingQTEKind = kind;
        if (!hasPendingRequest)
        {
            pendingQTEExpiresAt = Time.time + Mathf.Clamp(qtePriorityReservationWindow, 0.1f, 1.25f);
        }
    }

    /// <summary>读取有效的快速反应事件等待请求。</summary>
    private bool HasPendingQTERequest()
    {
        if (pendingQTEEnemy == null)
        {
            return false;
        }

        if (Time.time <= pendingQTEExpiresAt && IsQTERequestEligible(pendingQTEEnemy, pendingQTEKind))
        {
            return true;
        }

        FinishQTEWait();
        return false;
    }

    /// <summary>IsQTERequestEligible 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;VillagerQTEParticipant&gt;()、EnemyAIManager.IsReservationCandidateValid(VillagerController)、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、VillagerQTEParticipant.CanAttemptChainsawExecution(LeonController)、VillagerQTEParticipant.CanAttemptFrontGrab(LeonController)、VillagerQTEParticipant.CanAttemptLegBite(LeonController) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.HasPendingQTERequest()、EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)；下游（含内部回调体） Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;VillagerQTEParticipant&gt;()、EnemyAIManager.IsReservationCandidateValid(VillagerController)、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、VillagerQTEParticipant.CanAttemptChainsawExecution(LeonController)、VillagerQTEParticipant.CanAttemptFrontGrab(LeonController)、VillagerQTEParticipant.CanAttemptLegBite(LeonController) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.IsReservationCandidateValid(VillagerController) 的参数、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)。</param>
    /// <param name="kind">中文含义：种类。使用位置：作为 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 的参数、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)。</param>
    private bool IsQTERequestEligible(VillagerController enemy, QTEKind kind)
    {
        if (!IsReservationCandidateValid(enemy) || PlayerTarget == null)
        {
            return false;
        }

        // canEvaluate（是否可以评估）：使用位置：EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)。
bool canEvaluate;
        if (kind == QTEKind.LegBite)
        {
            canEvaluate = enemy.CanEvaluateCrawlCombatAction;
        }
        else
        {
            canEvaluate = enemy.CanEvaluateStandingQTEAction;
        }

        if (!canEvaluate)
        {
            return false;
        }

        // participant（参与者）：保存 Component.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 的参数、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)。
VillagerQTEParticipant participant = enemy.GetComponent<VillagerQTEParticipant>();
        // coordinator（当前里昂配对动作协调器）：保存 Component.GetComponent<LeonQTECoordinator>() 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)。
LeonQTECoordinator coordinator = PlayerTarget.GetComponent<LeonQTECoordinator>();
        if (participant == null || coordinator == null || coordinator.IsActive)
        {
            return false;
        }
        bool nativeGrab = enemy.combatModule != null && enemy.combatModule.NativeCombatProfile != null
            && (kind == QTEKind.FrontGrab || kind == QTEKind.RearBiteNeck);
        if (!nativeGrab && !coordinator.CanStart(kind, participant, out _))
        {
            return false;
        }

        switch (kind)
        {
            case QTEKind.FrontGrab:
                return participant.CanAttemptFrontGrab(PlayerTarget);
            case QTEKind.RearBiteNeck:
                return participant.CanAttemptRearBiteNeck(PlayerTarget);
            case QTEKind.LegBite:
                return participant.CanAttemptLegBite(PlayerTarget);
            case QTEKind.ChainsawExecution:
                return participant.CanAttemptChainsawExecution(PlayerTarget);
            default:
                return false;
        }
    }

    /// <summary>ClearPendingQTERequest 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.HasPendingQTERequest()、EnemyAIManager.ReleaseQTEReservation(VillagerController, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、EnemyAIManager.UnregisterEnemy(VillagerController)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearPendingQTERequest()
    {
        pendingQTEEnemy = null;
        pendingQTEKind = QTEKind.FrontGrab;
        pendingQTEExpiresAt = 0f;
    }

    /// <summary>GetQTERequestPriority 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.RegisterPendingQTERequest(VillagerController, QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：EnemyAIManager.GetQTERequestPriority(QTEKind)。</param>
    private static int GetQTERequestPriority(QTEKind kind)
    {
        if (kind == QTEKind.ChainsawExecution)
        {
            return 3;
        }

        if (kind == QTEKind.RearBiteNeck)
        {
            return 2;
        }

        return 1;
    }

    /// <summary>HasConcurrentAttackDirectionSpace 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.CanAcquireAttackReservation(VillagerController)、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)、EnemyAIManager.TryAcquireAttackReservation(VillagerController)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requester">中文含义：请求方。使用位置：EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)。</param>
    private bool HasConcurrentAttackDirectionSpace(VillagerController requester)
    {
        if (requester == null || PlayerTarget == null)
        {
            return true;
        }

        // requesterDirection（请求方方向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)。
Vector3 requesterDirection = requester.transform.position - PlayerTarget.transform.position;
        requesterDirection.y = 0f;
        if (requesterDirection.sqrMagnitude <= 0.0001f)
        {
            return attackingEnemies.Count == 0;
        }

        requesterDirection.Normalize();
        // minimumAngle（最小角度）：保存 Mathf.Clamp(float, float, float) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)。
float minimumAngle = Mathf.Clamp(minimumConcurrentAttackAngle, 0f, 180f);
        // attacker（攻击者）：每轮从攻击中敌人集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)。
foreach (VillagerController attacker in attackingEnemies)
        {
            if (attacker == null || attacker == requester)
            {
                continue;
            }

            // attackerDirection（攻击者方向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、EnemyAIManager.HasConcurrentAttackDirectionSpace(VillagerController)。
Vector3 attackerDirection = attacker.transform.position - PlayerTarget.transform.position;
            attackerDirection.y = 0f;
            if (attackerDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            attackerDirection.Normalize();
            if (Vector3.Angle(requesterDirection, attackerDirection) < minimumAngle)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>GetFirstReservedAttacker 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.AttackingEnemy.get；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private VillagerController GetFirstReservedAttacker()
    {
        if (qteReservedEnemy != null)
        {
            return qteReservedEnemy;
        }

        // attacker（攻击者）：每轮从攻击中敌人集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIManager.GetFirstReservedAttacker()。
foreach (VillagerController attacker in attackingEnemies)
        {
            if (attacker != null)
            {
                return attacker;
            }
        }

        return null;
    }

    /// <summary>IsReservationCandidateValid 的业务处理入口；本方法直接依赖 EnemyAIManager.IsEnemyUsable(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.CanAcquireAttackReservation(VillagerController)、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)、EnemyAIManager.TryAcquireAttackReservation(VillagerController) 等共 7 处；下游（含内部回调体） EnemyAIManager.IsEnemyUsable(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EnemyAIManager.IsEnemyUsable(VillagerController) 的参数、EnemyAIManager.IsReservationCandidateValid(VillagerController)。</param>
    private bool IsReservationCandidateValid(VillagerController enemy)
    {
        return IsEnemyUsable(enemy) && CanEngage(enemy) && enemy.IsCombatEngaged;
    }

    /// <summary>IsEnemyUsable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.GetNearestEnemy(Vector3)、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)、EnemyAIManager.IsDirectPressureEnemy(VillagerController, Vector3)、EnemyAIManager.IsReservationCandidateValid(VillagerController)、EnemyAIManager.NotifyCombatEngaged(VillagerController) 等共 10 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：EnemyAIManager.IsEnemyUsable(VillagerController)。</param>
    private static bool IsEnemyUsable(VillagerController enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && enemy.gameObject.activeInHierarchy && enemy.IsActive && !enemy.IsDead;
    }

    /// <summary>敌人行为、动作与通行的结构化数据 CombatSlotAssignment；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct CombatSlotAssignment
    {
        /// <summary>中文含义：玩家锚点。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)。</summary>
        public Vector3 playerAnchor;
        /// <summary>中文含义：目的地。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数 等共 7 处。</summary>
        public Vector3 destination;
        /// <summary>中文含义：槽位索引。使用位置：作为 EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int) 的参数、EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.BuildCombatTimelineSummary(VillagerController) 等共 7 处。</summary>
        public int slotIndex;
        /// <summary>中文含义：是否侧翼等待区。使用位置：EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)、EnemyAIManager.ShouldUseFlankStaging(VillagerController, float) 等共 4 处。</summary>
        public bool isFlankStaging;
        /// <summary>中文含义：到期时间。使用位置：EnemyAIManager.AssignSurroundSlot(VillagerController, Vector3, float, bool, int)、EnemyAIManager.GetStableSurroundSlot(VillagerController, Vector3, float, bool)。</summary>
    }

    /// <summary>敌人行为、动作与通行的结构化数据 PursuitTargetAssignment；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct PursuitTargetAssignment
    {
        /// <summary>中文含义：玩家锚点。使用位置：作为 EnemyAIManager.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3)。</summary>
        public Vector3 playerAnchor;
        /// <summary>中文含义：目的地。使用位置：EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3)。</summary>
        public Vector3 destination;
        /// <summary>中文含义：到期时间。使用位置：EnemyAIManager.ResolvePursuitDestination(VillagerController, Vector3)。</summary>
        public float expiresAt;
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)、Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireCube(Vector3, Vector3)、Object.FindFirstObjectByType&lt;LeonController&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        if (showVillageEntranceTriggerGizmo && enterVillageTrigger != null &&
            enterVillageTrigger.TryGetComponent(out BoxCollider trigger))
        {
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.75f);
            if (IsCombatUnlocked)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.75f);
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = trigger.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(trigger.center, trigger.size);
            Gizmos.matrix = previousMatrix;
        }

        // playerTransform（玩家变换）：使用位置：作为 EnemyAIManager.DrawSurroundSlots(Vector3, float, Color) 的参数、作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、EnemyAIManager.OnDrawGizmosSelected()。
Transform playerTransform;
        if (PlayerTarget != null)
        {
            playerTransform = PlayerTarget.transform;
        }
        else
        {
            // receiverValue35（判空前缓存的里昂玩家控制器）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIManager.OnDrawGizmosSelected()。
global::LeonController receiverValue35 = FindFirstObjectByType<LeonController>();
            if (!global::System.Object.ReferenceEquals(receiverValue35, null))
            {
                playerTransform = receiverValue35.transform;
            }
            else
            {
                playerTransform = default;
            }
        }

        if (playerTransform == null)
        {
            return;
        }

        DrawSurroundSlots(playerTransform.position, surroundDistance, Color.cyan);
        // attacker（攻击者）：每轮从攻击中敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、EnemyAIManager.OnDrawGizmosSelected()。
foreach (VillagerController attacker in attackingEnemies)
        {
            if (attacker == null)
            {
                continue;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(attacker.transform.position, playerTransform.position);
        }

        if (qteReservedEnemy != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(qteReservedEnemy.transform.position, playerTransform.position);
        }
    }

    /// <summary>DrawSurroundSlots 的业务处理入口；本方法直接依赖 Gizmos.DrawWireSphere(Vector3, float)、Mathf.Max(int, int)、Quaternion.Euler(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.OnDrawGizmosSelected()；下游（含内部回调体） Gizmos.DrawWireSphere(Vector3, float)、Mathf.Max(int, int)、Quaternion.Euler(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="center">中文含义：中心。使用位置：EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)。</param>
    /// <param name="radius">中文含义：半径。使用位置：EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)。</param>
    /// <param name="color">中文含义：颜色。使用位置：EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)。</param>
    private void DrawSurroundSlots(Vector3 center, float radius, Color color)
    {
        // slotCount（槽位数量）：保存 Mathf.Max(int, int) 的返回结果，供当前步骤使用。使用位置：EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)。
int slotCount = Mathf.Max(4, surroundSlotCount);
        Gizmos.color = color;
        // index（当前元素索引）：使用位置：EnemyAIManager.DrawSurroundSlots(Vector3, float, Color)。
for (int index = 0; index < slotCount; index++)
        {
            // angle（角度）：使用位置：作为 Quaternion.Euler(float, float, float) 的参数。
float angle = 360f * index / slotCount;
            // point（点）：使用位置：作为 Gizmos.DrawWireSphere(Vector3, float) 的参数。
Vector3 point = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            Gizmos.DrawWireSphere(point, 0.12f);
        }
    }
}
