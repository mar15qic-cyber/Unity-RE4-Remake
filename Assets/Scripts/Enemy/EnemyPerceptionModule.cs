using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyPerceptionModule，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人感知模块")]
public sealed class EnemyPerceptionModule : MonoBehaviour
{
    /// <summary>宿主。</summary>
    [Header("角色组装引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("宿主")]
    [UnityEngine.InspectorName("宿主")]
    public VillagerController host;
    /// <summary>中文含义：感知模块。</summary>
    private EnemyPerceptionModule PerceptionModule => host.perceptionModule;
    /// <summary>动作协调器。</summary>
    private EnemyActionCoordinator ActionCoordinator => host.actionCoordinator;
    /// <summary>动画显示器。</summary>
    private EnemyAnimationAdapter AnimationPresenter => host.animationPresenter;

    /// <summary>警觉。</summary>
    private float awareness;
    /// <summary>读取警觉。</summary>
    internal float ReadAwareness => awareness;

    /// <summary>是否完全察觉所属玩家。</summary>
    private bool fullyAwareOfPlayer;
    /// <summary>是否读取完全察觉所属玩家。</summary>
    internal bool ReadFullyAwareOfPlayer => fullyAwareOfPlayer;

    /// <summary>是否战斗已接战。</summary>
    private bool combatEngaged;
    /// <summary>是否读取战斗已接战。</summary>
    internal bool ReadCombatEngaged => combatEngaged;

    /// <summary>当前敌人本人是否已经真正发现过玩家（目击、近距正面惊动、受击或发起进攻），用于与全村集结信号解耦以对齐 RE4R 潜行暗杀资格。</summary>
    private bool hasPersonallyDetectedPlayer;
    /// <summary>只读访问当前敌人本人是否已经真正发现过玩家。</summary>
    internal bool ReadHasPersonallyDetectedPlayer
    {
        get
        {
            return hasPersonallyDetectedPlayer;
        }
    }

    /// <summary>标记当前敌人本人已经真实发现玩家。</summary>
    internal void MarkPersonallyDetectedPlayer()
    {
        hasPersonallyDetectedPlayer = true;
    }

    /// <summary>外部解除接触自从。</summary>
    private float outsideDisengageSince = float.NegativeInfinity;
    /// <summary>读取外部解除接触自从。</summary>
    internal float ReadOutsideDisengageSince => outsideDisengageSince;

    /// <summary>玩家是否可见。</summary>
    private bool playerVisible;
    /// <summary>读取玩家是否可见。</summary>
    internal bool ReadPlayerVisible => playerVisible;

    /// <summary>上一次已见时间。</summary>
    private float lastSeenTime = float.NegativeInfinity;
    /// <summary>读取上一次已见时间。</summary>
    internal float ReadLastSeenTime => lastSeenTime;

    /// <summary>上一次已知玩家位置。</summary>
    private Vector3 lastKnownPlayerPosition;
    /// <summary>读取上一次已知玩家位置。</summary>
    internal Vector3 ReadLastKnownPlayerPosition => lastKnownPlayerPosition;

    /// <summary>是否具有调查目标。</summary>
    private bool hasInvestigationTarget;
    /// <summary>读取是否具有调查目标。</summary>
    internal bool ReadHasInvestigationTarget => hasInvestigationTarget;

    /// <summary>是否调查搜索中。</summary>
    private bool investigationSearching;
    /// <summary>是否读取调查搜索中。</summary>
    internal bool ReadInvestigationSearching => investigationSearching;

    /// <summary>调查搜索结束时间。</summary>
    private float investigationSearchEndTime;
    /// <summary>读取调查搜索结束时间。</summary>
    internal float ReadInvestigationSearchEndTime => investigationSearchEndTime;

    /// <summary>下一次调查转向时间。</summary>
    private float nextInvestigationTurnTime;
    /// <summary>读取下一次调查转向时间。</summary>
    internal float ReadNextInvestigationTurnTime => nextInvestigationTurnTime;

    /// <summary>调查观察目标。</summary>
    private Vector3 investigationLookTarget;
    /// <summary>读取调查观察目标。</summary>
    internal Vector3 ReadInvestigationLookTarget => investigationLookTarget;

    /// <summary>可见性评估帧。</summary>
    private int visibilityEvaluationFrame = -1;
    /// <summary>读取可见性评估帧。</summary>
    internal int ReadVisibilityEvaluationFrame => visibilityEvaluationFrame;
    /// <summary>当前移动模式。</summary>
    private EnemyMovementMode CurrentMovementMode => host.ArchitectureReadCurrentMovementMode;
    /// <summary>当前状态。</summary>
    private VillagerState CurrentState => host.ArchitectureReadCurrentState;
    /// <summary>是否生效。</summary>
    private bool IsActive => host.IsActive;
    /// <summary>是否死亡。</summary>
    private bool IsDead => host.IsDead;
    /// <summary>是否配对动作受控。</summary>
    private bool IsQTEControlled => host.IsQTEControlled;
    /// <summary>是否村庄入口战斗锁定。</summary>
    private bool IsVillageEntranceCombatLocked => host.ArchitectureReadIsVillageEntranceCombatLocked;
    /// <summary>玩家。</summary>
    private LeonController Player => host.Player;
    /// <summary>玩家距离。</summary>
    private float PlayerDistance => host.PlayerDistance;
    /// <summary>寻路代理。</summary>
    private PathAgent agent => host.ArchitectureReadAgent;
    /// <summary>警戒转向速度。</summary>
    private float alertTurnSpeed => host.alertTurnSpeed;
    /// <summary>警觉衰减持续时间。</summary>
    private float awarenessDecayDuration => host.awarenessDecayDuration;
    /// <summary>关闭警觉范围。</summary>
    private float closeAwarenessRange => host.closeAwarenessRange;
    /// <summary>枪声警觉。</summary>
    private float gunshotAwareness => host.gunshotAwareness;
    /// <summary>调查抵达距离。</summary>
    private float investigationArrivalDistance => host.investigationArrivalDistance;
    /// <summary>调查搜索持续时间。</summary>
    private float investigationSearchDuration => host.investigationSearchDuration;
    /// <summary>视野角度。</summary>
    private float sightAngle => host.sightAngle;
    /// <summary>视野范围。</summary>
    private float sightRange => host.sightRange;

    /// <summary>BeginChase 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.BeginChase(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)；下游（含内部回调体） EnemyActionCoordinator.BeginChase(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="notifyCombatEngaged">是否通知战斗已接战。</param>
    private void BeginChase(bool notifyCombatEngaged = true)
    {
        ActionCoordinator.BeginChase(notifyCombatEngaged);
    }

    /// <summary>FacePosition 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeFacePosition(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.UpdateAlertFacing()；下游（含内部回调体） VillagerController.ArchitectureInvokeFacePosition(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">目标位置。</param>
    /// <param name="turnSpeed">转向速度。</param>
    private void FacePosition(Vector3 targetPosition, float turnSpeed)
    {
        host.ArchitectureInvokeFacePosition(targetPosition, turnSpeed);
    }

    /// <summary>PlayVoice 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.BecomeFullyAwareOfPlayer()；下游（含内部回调体） EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">语音类型。</param>
    private void PlayVoice(ZombieVoiceType voiceType)
    {
        AnimationPresenter.PlayVoice(voiceType);
    }

    /// <summary>RefreshChaseDestination 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeRefreshChaseDestination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)；下游（含内部回调体） VillagerController.ArchitectureInvokeRefreshChaseDestination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool RefreshChaseDestination()
    {
        return host.ArchitectureInvokeRefreshChaseDestination();
    }

    /// <summary>SetMovementMode 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.UpdateInvestigation()；下游（含内部回调体） VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementMode">移动模式。</param>
    /// <param name="source">来源。</param>
    /// <param name="line">线。</param>
    private void SetMovementMode(EnemyMovementMode movementMode, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        host.ArchitectureInvokeSetMovementMode(movementMode, source, line);
    }

    /// <summary>StopAndClearMovement 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeStopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.UpdateInvestigation()；下游（含内部回调体） VillagerController.ArchitectureInvokeStopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopAndClearMovement()
    {
        host.ArchitectureInvokeStopAndClearMovement();
    }

    /// <summary>CanSeePlayer 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.CanSeePlayer()、EnemyPerceptionModule.UpdateAwareness()、EnemyStateSnapshot.EnemyStateSnapshot(VillagerController)、VillagerController.CanSeePlayer()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool CanSeePlayer() => playerVisible;
    /// <summary>SampleLineOfSight 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.EvaluatePlayerVisibility()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()、VillagerController.Update()；下游（含内部回调体） EnemyPerceptionModule.EvaluatePlayerVisibility()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void SampleLineOfSight()
    {
        if (visibilityEvaluationFrame == Time.frameCount)
        {
            return;
        }

        visibilityEvaluationFrame = Time.frameCount;
        if (IsVillageEntranceCombatLocked)
        {
            playerVisible = false;
            EnemyAIManager manager = EnemyAIManager.Instance;
            if (manager != null && manager.AwaitsVillageDiscovery && IsActive && !IsDead && !IsQTEControlled)
            {
                SampleVillageDiscovery(manager);
            }

            return;
        }

        playerVisible = EvaluatePlayerVisibility();
        return;
    }

    private void SampleVillageDiscovery(EnemyAIManager manager)
    {
        LeonController player = Player;
        if (player == null || !manager.CanObserveVillagePlayer)
        {
            awareness = 0f;
            return;
        }

        float range = 14f;
        if (player.IsCrouching)
        {
            range = 8f;
        }
        if (!HasClearLineToPlayer(true, range, 55f))
        {
            awareness = Mathf.MoveTowards(awareness, 0f, Time.deltaTime / 1.5f);
            return;
        }

        float confirmation = .8f;
        if (PlayerDistance <= 2.5f)
        {
            confirmation = .25f;
        }
        awareness = Mathf.MoveTowards(awareness, 1f, Time.deltaTime / confirmation);
        if (awareness >= 1f)
        {
            hasPersonallyDetectedPlayer = true;
            manager.DiscoverVillageCombat(host);
            playerVisible = manager.IsCombatUnlocked;
            if (playerVisible)
            {
                PlayVoice(ZombieVoiceType.Detection);
            }
        }
    }

    internal bool HasVillageFootstepTarget => hasInvestigationTarget && !fullyAwareOfPlayer;

    internal void NoticeVillageFootstep(Vector3 position)
    {
        if (hasInvestigationTarget && (lastKnownPlayerPosition - position).sqrMagnitude < .36f)
        {
            return;
        }
        lastKnownPlayerPosition = position;
        hasInvestigationTarget = true;
        investigationSearching = false;
        investigationSearchEndTime = 0f;
    }

    /// <summary>EvaluatePlayerVisibility 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.HasClearLineToPlayer(bool, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.SampleLineOfSight()、VillagerController.EvaluatePlayerVisibility()；下游（含内部回调体） EnemyPerceptionModule.HasClearLineToPlayer(bool, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool EvaluatePlayerVisibility()
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        if (PlayerDistance <= closeAwarenessRange && HasClearLineToPlayer(false, closeAwarenessRange))
        {
            return true;
        }

        return HasClearLineToPlayer(true, sightRange);
    }

    /// <summary>HasClearLineToPlayer 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)、Mathf.Min(float, float)、Transform.IsChildOf(Transform)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.HasClearLineToPlayer(bool, float)、EnemyPerceptionModule.EvaluatePlayerVisibility()、VillagerController.HasClearLineToPlayer(bool, float)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)、Mathf.Min(float, float)、Transform.IsChildOf(Transform)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requireSightAngle">是否需要视野角度。</param>
    /// <param name="maximumDistance">最大距离。</param>
    internal bool HasClearLineToPlayer(bool requireSightAngle, float maximumDistance, float halfAngle = -1f)
    {
        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        // 原点。
Vector3 origin = transform.position + Vector3.up * 1.4f;
        // target（目标）：使用位置：EnemyPerceptionModule.HasClearLineToPlayer(bool, float)。
float targetHeight = 1f;
        if (halfAngle >= 0f && player.IsCrouching)
        {
            targetHeight = .68f;
        }
        Vector3 target = player.transform.position + Vector3.up * targetHeight;
        if (halfAngle < 0f)
        {
            halfAngle = sightAngle * .5f;
        }
        // 方向。
Vector3 direction = target - origin;
        // 距离。
float distance = direction.magnitude;
        if (distance > maximumDistance || distance <= 0.001f)
        {
            return false;
        }

        // 水平方向。
Vector3 horizontalDirection = direction;
        horizontalDirection.y = 0f;
        if (requireSightAngle && horizontalDirection.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, horizontalDirection) > halfAngle)
        {
            return false;
        }

        // 命中结果。
RaycastHit[] hits = sightQueries.Raycast(origin, direction.normalized, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out int /* 中文含义：命中数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hitCount);
        if (hitCount == 0)
        {
            return false;
        }

        // 最近玩家命中。
float nearestPlayerHit = float.MaxValue;
        // 最近阻挡者命中。
float nearestBlockerHit = float.MaxValue;
        // 命中索引。
for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            // 命中。
RaycastHit hit = hits[hitIndex];
            // 命中的碰撞体。
Collider hitCollider = hit.collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            // 同伴不应遮挡敌人对里昂的近距离感知。若把它们当作实体
            // 墙面，攻击名额被其他敌人占用时，等待者会在“看不见目标”
            // 与“继续追击”之间来回切换，并和局部避让叠加成绕圈。
            if (hitCollider.GetComponentInParent<VillagerController>() != null)
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<LeonController>() != null)
            {
                nearestPlayerHit = Mathf.Min(nearestPlayerHit, hit.distance);
                continue;
            }

            nearestBlockerHit = Mathf.Min(nearestBlockerHit, hit.distance);
        }

        return nearestPlayerHit < float.MaxValue && nearestPlayerHit <= nearestBlockerHit + 0.02f;
    }

    /// <summary>ClearCombatEngagement 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ClearCombatEngagement(bool)、VillagerController.ClearCombatEngagement(bool)；下游（含内部回调体） EnemyActionCoordinator.ExitCombatOccupancy()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveInvestigation">是否保留调查。</param>
    internal void ClearCombatEngagement(bool preserveInvestigation)
    {
        combatEngaged = false;
        fullyAwareOfPlayer = false;
        hasPersonallyDetectedPlayer = false;
        outsideDisengageSince = float.NegativeInfinity;
        ActionCoordinator.ExitCombatOccupancy();
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue1 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.ReleaseCombatReservation(host);
        }

        if (preserveInvestigation && Player != null)
        {
            awareness = Mathf.Max(awareness, gunshotAwareness);
            hasInvestigationTarget = true;
            investigationSearching = false;
            investigationSearchEndTime = 0f;
            return;
        }

        awareness = 0f;
        hasInvestigationTarget = false;
        investigationSearching = false;
        investigationSearchEndTime = 0f;
    }

    /// <summary>UpdateAwareness 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.CanSeePlayer()、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateAwareness()；下游（含内部回调体） EnemyPerceptionModule.CanSeePlayer()、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateAwareness()
    {
        if (IsVillageEntranceCombatLocked)
        {
            fullyAwareOfPlayer = false;
            combatEngaged = false;
            outsideDisengageSince = float.NegativeInfinity;
            playerVisible = false;
            return;
        }

        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            awareness = 0f;
            fullyAwareOfPlayer = false;
            combatEngaged = false;
            outsideDisengageSince = float.NegativeInfinity;
            playerVisible = false;
            hasInvestigationTarget = false;
            return;
        }

        // 是否可见。
bool visible = CanSeePlayer();
        if (visible)
        {
            hasPersonallyDetectedPlayer = true;
        }

        if (combatEngaged)
        {
            awareness = 1f;
            fullyAwareOfPlayer = true;
            outsideDisengageSince = float.NegativeInfinity;
            if (visible)
            {
                lastKnownPlayerPosition = player.transform.position;
                lastSeenTime = Time.time;
            }

            return;
        }

        // 当前敌人智能管理器。
EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager != null && manager.CanEngage(host))
        {
            ReceiveVillagePursuitOrder(player.transform.position);
            return;
        }

        // 视觉只服务于近战视线和最后可见位置，不再决定是否进入追击。
        // 未收到入村追击命令时始终保持非战斗状态。
        fullyAwareOfPlayer = false;
        awareness = Mathf.MoveTowards(awareness, 0f, Time.deltaTime / awarenessDecayDuration);
    }

    /// <summary>提交受击或枪声引发的接战，并更新玩家位置记忆。</summary>
    internal void BecomeFullyAwareOfPlayer(bool directDamage = false)
    {
        if (directDamage || CanSeePlayer())
        {
            hasPersonallyDetectedPlayer = true;
        }

        EnemyAIManager manager = EnemyAIManager.Instance;
        if (IsVillageEntranceCombatLocked)
        {
            return;
        }

        if (manager != null)
        {
            if (directDamage)
            {
                manager.ReleaseEngagementOnHit(host);
            }

            if (!manager.CanEngage(host))
            {
                return;
            }
        }

        // 玩家。
LeonController player = Player;
        if (player == null)
        {
            return;
        }

        awareness = 1f;
        fullyAwareOfPlayer = true;
        combatEngaged = true;
        outsideDisengageSince = float.NegativeInfinity;
        lastSeenTime = Time.time;
        lastKnownPlayerPosition = player.transform.position;
        hasInvestigationTarget = true;
        // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue2 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.NotifyCombatEngaged(host);
        }

    }

    /// <summary>ReceiveSharedCombatAlert 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ReceiveSharedCombatAlert(Vector3)；下游（含内部回调体） EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">玩家位置。</param>
    internal void ReceiveSharedCombatAlert(Vector3 playerPosition)
    {
        // 已经进入持续追击的敌人不应被门声或移动声重新启动追击状态。
        // 路线刷新只由玩家区域版本负责；环境警报仅用于唤醒尚未参战者。
        if (combatEngaged && fullyAwareOfPlayer)
        {
            lastKnownPlayerPosition = playerPosition;
            lastSeenTime = Time.time;
            return;
        }

        ReceiveVillagePursuitOrder(playerPosition);
    }

    /// <summary>ReceiveVillagePursuitOrder 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ExitCombatOccupancy()、EnemyPerceptionModule.BeginChase(bool)、EnemyPerceptionModule.RefreshChaseDestination()、PathAgent.ForceRepath()、PathAgent.InvalidatePursuitResolutionCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.ReceiveSharedCombatAlert(Vector3)、EnemyPerceptionModule.UpdateAwareness()、VillagerController.ReceiveVillagePursuitOrder(Vector3)；下游（含内部回调体） EnemyActionCoordinator.ExitCombatOccupancy()、EnemyPerceptionModule.BeginChase(bool)、EnemyPerceptionModule.RefreshChaseDestination()、PathAgent.ForceRepath()、PathAgent.InvalidatePursuitResolutionCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerPosition">玩家位置。</param>
    internal void ReceiveVillagePursuitOrder(Vector3 playerPosition)
    {
        // 停用后的警报只能丢弃，不能绕过动作审批重新登记路径与门队列。
        if (!isActiveAndEnabled || host == null || !host.isActiveAndEnabled || ActionCoordinator == null || !ActionCoordinator.CanReceiveRequest || IsVillageEntranceCombatLocked || IsDead || IsQTEControlled)
        {
            return;
        }

        EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager != null && !manager.CanEngage(host))
        {
            return;
        }

        awareness = 1f;
        fullyAwareOfPlayer = true;
        combatEngaged = true;
        outsideDisengageSince = float.NegativeInfinity;
        lastSeenTime = Time.time;
        lastKnownPlayerPosition = playerPosition;
        hasInvestigationTarget = true;
        investigationSearching = false;
        investigationSearchEndTime = 0f;
        // 入村警报是强制追击指令，而不是单纯的意识标记。此前已停在
        // 占位、巡逻收尾或取消路径状态的敌人会保留旧移动模式，直到玩家
        // 再次移动才被行为树重新激活，造成局部敌人原地等待。
        if (CurrentState == VillagerState.Attack || CurrentState == VillagerState.CrawlAttack || CurrentState == VillagerState.Staggered || CurrentState == VillagerState.StandUp)
        {
            return;
        }

        ActionCoordinator.ExitCombatOccupancy();
        // 是否需要追逐激活。
bool requiresChaseActivation = CurrentState != VillagerState.Chase || CurrentMovementMode != EnemyMovementMode.StandingPathRootMotion;
        if (requiresChaseActivation)
        {
            BeginChase(false);
        }

        if (agent == null || !agent.enabled)
        {
            return;
        }

        agent.isStopped = false;
        // 是否需要路径激活。
bool requiresPathActivation = requiresChaseActivation || !agent.HasDestination || agent.CurrentPathState == PathAgentState.Cancelled || agent.CurrentPathState == PathAgentState.Failed;
        if (!requiresPathActivation)
        {
            return;
        }

        agent.InvalidatePursuitResolutionCache();
        if (!RefreshChaseDestination())
        {
            agent.ForceRepath();
        }
    }

    /// <summary>ReceiveGunshot 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.BecomeFullyAwareOfPlayer()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ReceiveGunshot(Vector3)；下游（含内部回调体） EnemyPerceptionModule.BecomeFullyAwareOfPlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    internal void ReceiveGunshot(Vector3 position)
    {
        if (IsVillageEntranceCombatLocked || IsDead || !IsActive || Player == null)
        {
            return;
        }

        lastKnownPlayerPosition = position;
        if (PlayerDistance <= closeAwarenessRange && HasClearLineToPlayer(false, closeAwarenessRange))
        {
            hasPersonallyDetectedPlayer = true;
        }
        BecomeFullyAwareOfPlayer();
    }

    /// <summary>UpdateInvestigation 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyPerceptionModule.StopAndClearMovement()、EnemyPerceptionModule.UpdateAlertFacing()、PathAgent.SetDestination(Vector3)、Quaternion.Euler(float, float, float)、Random.Range(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateInvestigation()；下游（含内部回调体） EnemyPerceptionModule.SetMovementMode(EnemyMovementMode, string, int)、EnemyPerceptionModule.StopAndClearMovement()、EnemyPerceptionModule.UpdateAlertFacing()、PathAgent.SetDestination(Vector3)、Quaternion.Euler(float, float, float)、Random.Range(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool UpdateInvestigation()
    {
        if (IsDead || fullyAwareOfPlayer || !hasInvestigationTarget)
        {
            return true;
        }

        // 水平偏移量。
Vector3 horizontalOffset = lastKnownPlayerPosition - transform.position;
        horizontalOffset.y = 0f;
        if (!investigationSearching && agent != null && agent.CurrentPathState == PathAgentState.Failed)
        {
            horizontalOffset = Vector3.zero;
        }

        if (!investigationSearching && horizontalOffset.magnitude > investigationArrivalDistance)
        {
            SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
            if (agent != null)
            {
                agent.isStopped = false;
                agent.SetDestination(lastKnownPlayerPosition);
            }

            return false;
        }

        if (!investigationSearching)
        {
            investigationSearching = true;
            investigationSearchEndTime = Time.time + investigationSearchDuration;
            nextInvestigationTurnTime = Time.time;
            StopAndClearMovement();
            SetMovementMode(EnemyMovementMode.FullyStopped);
        }

        if (Time.time >= nextInvestigationTurnTime)
        {
            nextInvestigationTurnTime = Time.time + 0.75f;
            // 角度。
float angle = UnityEngine.Random.Range(-120f, 120f);
            investigationLookTarget = transform.position + Quaternion.Euler(0f, angle, 0f) * transform.forward * 3f;
        }

        UpdateAlertFacing();
        if (Time.time < investigationSearchEndTime)
        {
            return false;
        }

        investigationSearching = false;
        hasInvestigationTarget = false;
        awareness = 0f;
        return true;
    }

    /// <summary>UpdateAlertFacing 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.FacePosition(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyPerceptionModule.UpdateInvestigation()、VillagerController.UpdateAlertFacing()；下游（含内部回调体） EnemyPerceptionModule.FacePosition(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateAlertFacing()
    {
        if (investigationSearching)
        {
            FacePosition(investigationLookTarget, alertTurnSpeed);
        }
        else
        {
            FacePosition(lastKnownPlayerPosition, alertTurnSpeed);
        }
    }

    /// <summary>视野查询集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HitQueryBuffer sightQueries = new HitQueryBuffer();
    /// <summary>ConfirmChaseAlert 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginChase(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ConfirmChaseAlert()
    {
        combatEngaged = true;
        fullyAwareOfPlayer = true;
        awareness = 1f;
        outsideDisengageSince = float.NegativeInfinity;
    }

    /// <summary>EndInvestigateWaiting 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginAlert()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void EndInvestigateWaiting()
    {
        investigationSearching = false;
        investigationSearchEndTime = 0f;
    }

    /// <summary>UpdateRegionMemory 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ApplyPendingPursuitRegionChange()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    internal void UpdateRegionMemory(Vector3 position)
    {
        lastKnownPlayerPosition = position;
        hasInvestigationTarget = true;
    }
}
