using GameArchitecture;
using System;
using System.Collections;
using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyActionCoordinator，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>单个敌人的动作审批与有效期边界，行为树提交意图后统一核对冲突、冷却和关联资源，结束或取消时释放本动作持有的授权。</remarks>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人动作协调器")]
public sealed partial class EnemyActionCoordinator : MonoBehaviour
{
    /// <summary>中文含义：宿主。使用位置：作为 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle) 的参数、作为 Debug.LogWarning(object, Object) 的参数、作为 EnemyAIManager.NotifyCombatEngaged(VillagerController) 的参数 等共 56 处。</summary>
    [Header("角色组装引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("宿主")]
    [UnityEngine.InspectorName("宿主")]
    public VillagerController host;
    /// <summary>中文含义：战斗模块。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.CloseAction() 等共 4 处。</summary>
    private EnemyCombatModule CombatModule => host.combatModule;
    /// <summary>中文含义：感知模块。使用位置：EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.ClearCombatEngagement(bool)。</summary>
    private EnemyPerceptionModule PerceptionModule => host.perceptionModule;
    /// <summary>中文含义：动作协调器。</summary>
    private EnemyActionCoordinator ActionCoordinator => host.actionCoordinator;
    /// <summary>中文含义：动画显示器。使用位置：EnemyActionCoordinator.ApplyDeathAnimatorParameters()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold() 等共 7 处。</summary>
    private EnemyAnimationAdapter AnimationPresenter => host.animationPresenter;

    /// <summary>中文含义：当前动作。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 EnemyActionCoordinator.DiagLog(string, string) 的参数、EnemyActionCoordinator.InitializeAction() 等共 5 处。</summary>
    private VillagerState currentAction = VillagerState.Idle;
    /// <summary>中文含义：读取可感知状态。使用位置：EnemyCombatModule.observableState.get、VillagerController.observableState.get。</summary>
    internal VillagerState ReadObservableState => currentAction;

    /// <summary>中文含义：战斗保持是否生效。使用位置：EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold() 等共 8 处。</summary>
    private bool combatHoldActive;
    /// <summary>中文含义：读取战斗保持是否生效。使用位置：VillagerController.combatHoldActive.get。</summary>
    internal bool ReadCombatHoldActive => combatHoldActive;

    /// <summary>中文含义：追逐开始时间。使用位置：EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.ReadChaseStartedTime.get。</summary>
    private float chaseStartedTime;
    /// <summary>中文含义：读取追逐开始时间。使用位置：VillagerController.chaseStartedTime.get。</summary>
    internal float ReadChaseStartedTime => chaseStartedTime;

    /// <summary>中文含义：巡逻中心。使用位置：EnemyActionCoordinator.InitializeAction()、EnemyActionCoordinator.ReadPatrolCenter.get。</summary>
    private Vector3 patrolCenter;
    /// <summary>中文含义：读取巡逻中心。使用位置：VillagerController.patrolCenter.get。</summary>
    internal Vector3 ReadPatrolCenter => patrolCenter;

    /// <summary>中文含义：巡逻目标。使用位置：EnemyActionCoordinator.ReadPatrolTarget.get、EnemyActionCoordinator.SetPatrolTarget(Vector3)。</summary>
    private Vector3 patrolTarget;
    /// <summary>中文含义：读取巡逻目标。使用位置：VillagerController.patrolTarget.get。</summary>
    internal Vector3 ReadPatrolTarget => patrolTarget;

    /// <summary>中文含义：是否具有巡逻目标。使用位置：EnemyActionCoordinator.ClearPatrolTarget()、EnemyActionCoordinator.ReadHasPatrolTarget.get、EnemyActionCoordinator.SetPatrolTarget(Vector3)。</summary>
    private bool hasPatrolTarget;
    /// <summary>中文含义：读取是否具有巡逻目标。使用位置：VillagerController.hasPatrolTarget.get。</summary>
    internal bool ReadHasPatrolTarget => hasPatrolTarget;

    /// <summary>中文含义：是否具有已掉落战利品。使用位置：EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.ReadHasDroppedLoot.get。</summary>
    private bool hasDroppedLoot;
    /// <summary>中文含义：读取是否具有已掉落战利品。使用位置：VillagerController.hasDroppedLoot.get。</summary>
    internal bool ReadHasDroppedLoot => hasDroppedLoot;

    /// <summary>中文含义：死亡执行是否具有开始。使用位置：EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.ReadDeathExecuteHasBegin.get。</summary>
    private bool deathExecuteHasBegin;
    /// <summary>中文含义：读取死亡执行是否具有开始。使用位置：VillagerController.DeathExecuteHasBegin.get。</summary>
    internal bool ReadDeathExecuteHasBegin => deathExecuteHasBegin;
    /// <summary>中文含义：是否可以日志移动诊断。使用位置：EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()。</summary>
    private bool CanLogMovementDiagnostic => host.ArchitectureReadCanLogMovementDiagnostic;
    /// <summary>中文含义：当前移动模式。使用位置：作为 EnemyActionCoordinator.DiagLog(string, string) 的参数、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()。</summary>
    private EnemyMovementMode CurrentMovementMode => host.ArchitectureReadCurrentMovementMode;
    /// <summary>中文含义：当前状态。使用位置：EnemyActionCoordinator.BeginChase(bool)。</summary>
    private VillagerState CurrentState => host.ArchitectureReadCurrentState;
    /// <summary>中文含义：攻击的哈希编号。使用位置：作为 EnemyAnimationAdapter.SetBoolean(int, bool) 的参数。</summary>
    private static int HashATK => VillagerController.ArchitectureReadHashATK;
    /// <summary>中文含义：命中的哈希编号。使用位置：作为 EnemyAnimationAdapter.SetBoolean(int, bool) 的参数。</summary>
    private static int HashHIT => VillagerController.ArchitectureReadHashHIT;
    /// <summary>中文含义：待机混合的哈希编号。使用位置：作为 EnemyAnimationAdapter.SetFloatValue(int, float) 的参数。</summary>
    private static int HashIdleBlend => VillagerController.ArchitectureReadHashIdleBlend;
    /// <summary>中文含义：搜索模式的哈希编号。使用位置：作为 EnemyAnimationAdapter.SetBoolean(int, bool) 的参数。</summary>
    private static int HashSearchMode => VillagerController.ArchitectureReadHashSearchMode;
    /// <summary>中文含义：转向方向的哈希编号。使用位置：作为 EnemyAnimationAdapter.SetIntegerValue(int, int) 的参数。</summary>
    private static int HashTurnDir => VillagerController.ArchitectureReadHashTurnDir;
    /// <summary>中文含义：是否死亡。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool) 等共 8 处。</summary>
    private bool IsDead => host.IsDead;
    /// <summary>中文含义：是否村庄入口战斗锁定。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.BeginChase(bool)。</summary>
    private bool IsVillageEntranceCombatLocked => host.ArchitectureReadIsVillageEntranceCombatLocked;
    /// <summary>中文含义：玩家。使用位置：作为 EnemyActionCoordinator.FacePosition(Vector3, float) 的参数、EnemyActionCoordinator.UpdateCombatFacing()。</summary>
    private LeonController Player => host.Player;
    /// <summary>中文含义：寻路代理。使用位置：作为 EnemyActionCoordinator.DiagLog(string, string) 的参数、EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool) 等共 5 处。</summary>
    private PathAgent agent => host.ArchitectureReadAgent;
    /// <summary>中文含义：警戒转向速度。使用位置：作为 EnemyActionCoordinator.FacePosition(Vector3, float) 的参数。</summary>
    private float alertTurnSpeed => host.alertTurnSpeed;
    /// <summary>中文含义：动画控制器。</summary>
    private Animator anim => host.ArchitectureReadAnim;
    /// <summary>中文含义：电锯桥接。使用位置：EnemyActionCoordinator.BeginDeath()。</summary>
    private ChainsawManAnimationBridge chainsawBridge => host.ArchitectureReadChainsawBridge;
    /// <summary>中文含义：追逐起始移动速度。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数。</summary>
    private float chaseStartMoveSpeed => host.chaseStartMoveSpeed;
    /// <summary>中文含义：碰撞体控制器。使用位置：EnemyActionCoordinator.BeginDeath()。</summary>
    private EnemyColliderController colliderCtrl => host.ArchitectureReadColliderCtrl;
    /// <summary>中文含义：当前敌人上下文。使用位置：EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginPatrol()。</summary>
    private EnemyContext ctx => host.ArchitectureReadCtx;
    /// <summary>中文含义：死亡电锯危险持续时间。使用位置：作为 ChainsawManAnimationBridge.BeginDeathShutdown(float) 的参数、EnemyActionCoordinator.BeginDeath()。</summary>
    private float deadChainsawDangerDuration => host.deadChainsawDangerDuration;
    /// <summary>中文含义：任务敌人类型。使用位置：EnemyActionCoordinator.BeginDeath()。</summary>
    private MissionEnemyType missionEnemyType => host.missionEnemyType;
    /// <summary>中文含义：巡逻速度。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数。</summary>
    private float patrolSpeed => host.patrolSpeed;
    /// <summary>中文含义：配对动作参与者。</summary>
    private VillagerQTEParticipant qteParticipant => host.ArchitectureReadQteParticipant;
    /// <summary>中文含义：特殊通行。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.BeginChase(bool)。</summary>
    private EnemyWindowTraversal specialTraversal => host.ArchitectureReadSpecialTraversal;
    /// <summary>中文含义：静止巡逻转向速度。使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数。</summary>
    private float stationaryPatrolTurnSpeed => host.stationaryPatrolTurnSpeed;
    /// <summary>中文含义：生命模块。使用位置：EnemyActionCoordinator.BeginDeath()。</summary>
    private CharacterHealthModule HealthModule => host.healthModule;

    /// <summary>ApplyDeathAnimatorParameters 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.ApplyDeathAnimatorParameters()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） EnemyAnimationAdapter.ApplyDeathAnimatorParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyDeathAnimatorParameters()
    {
        AnimationPresenter.ApplyDeathAnimatorParameters();
    }

    /// <summary>BuildCombatTimelineSnapshot 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeBuildCombatTimelineSnapshot()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ArchitectureInvokeBuildCombatTimelineSnapshot()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private string BuildCombatTimelineSnapshot()
    {
        return host.ArchitectureInvokeBuildCombatTimelineSnapshot();
    }

    /// <summary>ClearCombatEngagement 的业务处理入口；本方法直接依赖 EnemyPerceptionModule.ClearCombatEngagement(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） EnemyPerceptionModule.ClearCombatEngagement(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="preserveInvestigation">中文含义：是否保留调查。使用位置：作为 EnemyPerceptionModule.ClearCombatEngagement(bool) 的参数。</param>
    private void ClearCombatEngagement(bool preserveInvestigation)
    {
        PerceptionModule.ClearCombatEngagement(preserveInvestigation);
    }

    /// <summary>DiagLog 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeDiagLog(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()；下游（含内部回调体） VillagerController.ArchitectureInvokeDiagLog(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tag">中文含义：标签。使用位置：作为 VillagerController.ArchitectureInvokeDiagLog(string, string) 的参数。</param>
    /// <param name="msg">中文含义：消息内容。使用位置：作为 VillagerController.ArchitectureInvokeDiagLog(string, string) 的参数。</param>
    private void DiagLog(string tag, string msg)
    {
        host.ArchitectureInvokeDiagLog(tag, msg);
    }

    /// <summary>FacePosition 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeFacePosition(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.UpdateCombatFacing()；下游（含内部回调体） VillagerController.ArchitectureInvokeFacePosition(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetPosition">中文含义：目标位置。使用位置：作为 VillagerController.ArchitectureInvokeFacePosition(Vector3, float) 的参数。</param>
    /// <param name="turnSpeed">中文含义：转向速度。使用位置：作为 VillagerController.ArchitectureInvokeFacePosition(Vector3, float) 的参数。</param>
    private void FacePosition(Vector3 targetPosition, float turnSpeed)
    {
        host.ArchitectureInvokeFacePosition(targetPosition, turnSpeed);
    }

    /// <summary>GetPatrolTurnDirection 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeGetPatrolTurnDirection(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginStationaryPatrol(float)；下游（含内部回调体） VillagerController.ArchitectureInvokeGetPatrolTurnDirection(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="signedTurnAngle">中文含义：有符号转向角度。使用位置：作为 VillagerController.ArchitectureInvokeGetPatrolTurnDirection(float) 的参数。</param>
    private static int GetPatrolTurnDirection(float signedTurnAngle)
    {
        return VillagerController.ArchitectureInvokeGetPatrolTurnDirection(signedTurnAngle);
    }

    /// <summary>PlayVoice 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="voiceType">中文含义：语音类型。使用位置：作为 EnemyAnimationAdapter.PlayVoice(ZombieVoiceType) 的参数。</param>
    private void PlayVoice(ZombieVoiceType voiceType)
    {
        AnimationPresenter.PlayVoice(voiceType);
    }

    /// <summary>SetMovementMode 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()、EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle() 等共 7 处；下游（含内部回调体） VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movementMode">中文含义：移动模式。使用位置：作为 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 VillagerController.ArchitectureInvokeSetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    private void SetMovementMode(EnemyMovementMode movementMode, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        host.ArchitectureInvokeSetMovementMode(movementMode, source, line);
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSnapToGround()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） VillagerController.ArchitectureInvokeSnapToGround()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SnapToGround()
    {
        host.ArchitectureInvokeSnapToGround();
    }

    /// <summary>SnapToGround 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ArchitectureInvokeSnapToGround(float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="probeHeight">中文含义：探测高度。使用位置：作为 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool) 的参数。</param>
    /// <param name="probeDistance">中文含义：探测距离。使用位置：作为 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool) 的参数。</param>
    /// <param name="allowLargeDrop">中文含义：是否允许较大掉落。使用位置：作为 VillagerController.ArchitectureInvokeSnapToGround(float, float, bool) 的参数。</param>
    private void SnapToGround(float probeHeight, float probeDistance, bool allowLargeDrop)
    {
        host.ArchitectureInvokeSnapToGround(probeHeight, probeDistance, allowLargeDrop);
    }

    /// <summary>StopAndClearMovement 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeStopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginStationaryPatrol(float)、EnemyActionCoordinator.EndStationaryPatrol()；下游（含内部回调体） VillagerController.ArchitectureInvokeStopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopAndClearMovement()
    {
        host.ArchitectureInvokeStopAndClearMovement();
    }

    /// <summary>StopPathMovementWithAnimationBlend 的业务处理入口；本方法直接依赖 VillagerController.ArchitectureInvokeStopPathMovementWithAnimationBlend()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginCombatHold()；下游（含内部回调体） VillagerController.ArchitectureInvokeStopPathMovementWithAnimationBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopPathMovementWithAnimationBlend()
    {
        host.ArchitectureInvokeStopPathMovementWithAnimationBlend();
    }

    /// <summary>SetObservableState 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、Debug.LogWarning(object, Object)、EnemyActionCoordinator.RegisterAction()、EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginAlert()、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginCombatHold()、EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle() 等共 9 处；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Text(string, string)、Debug.LogWarning(object, Object)、EnemyActionCoordinator.RegisterAction()、EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState)、EnemyCombatTimeline.IsRecord(VillagerController) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前敌人状态。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState) 的参数、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 EnemyCombatTimeline.Describe(string) 的参数。</param>
    /// <param name="line">中文含义：线。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数。</param>
    internal void SetObservableState(VillagerState state, [System.Runtime.CompilerServices.CallerMemberName] string source = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (currentAction == state)
        {
            return;
        }

        if (currentAction == VillagerState.Dead && state != VillagerState.Dead)
        {
            Debug.LogWarning($"[{name}] 拒绝从终态 Dead 恢复到 {state}。当前状态={currentAction}，目标状态={state}。", host);
            return;
        }

        // previous（前一次）：使用位置：作为 EnemyCombatTimeline.Describe<VillagerState>(VillagerState) 的参数。
VillagerState previous = currentAction;
        currentAction = state;
        RegisterAction();
        if (EnemyCombatTimeline.IsRecord(host))
        {
            EnemyCombatTimeline.Record(host, "状态切换", CombatDiagnosticFields.Text("原状态", EnemyCombatTimeline.Describe(previous)), CombatDiagnosticFields.Text("新状态", EnemyCombatTimeline.Describe(state)), CombatDiagnosticFields.Text("入口", EnemyCombatTimeline.Describe(source)), CombatDiagnosticFields.Count("调用行号", line));
        }
    }

    /// <summary>BeginChase 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.NotifyCombatEngaged(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyContext.ClampStandingMoveParameter(float)、EnemyPerceptionModule.ConfirmChaseAlert() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyCombatModule.BeginChase(bool)、EnemyPerceptionModule.BeginChase(bool)、VillagerController.BeginChase(bool)；下游（含内部回调体） EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.NotifyCombatEngaged(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyContext.ClampStandingMoveParameter(float)、EnemyPerceptionModule.ConfirmChaseAlert() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="notifyCombatEngaged">中文含义：是否通知战斗已接战。使用位置：EnemyActionCoordinator.BeginChase(bool)。</param>
    internal void BeginChase(bool notifyCombatEngaged = true)
    {
        if (IsDead || IsVillageEntranceCombatLocked)
        {
            return;
        }

        // 追击请求可以在受击过程中到达，须等当前动作交还控制权。
        if (CombatModule.NativeCombatActive || CombatModule.HasUnfinishedStandingHit)
        {
            return;
        }

        if (specialTraversal != null && specialTraversal.IsMovementProtected)
        {
            return;
        }

        if (CurrentState == VillagerState.Chase && CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && !combatHoldActive)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginChase", "进入");
        }

        // manager（当前敌人智能管理器）：使用位置：EnemyActionCoordinator.BeginChase(bool)。
EnemyAIManager manager = EnemyAIManager.Instance;
        PerceptionModule.ConfirmChaseAlert();
        combatHoldActive = false;
        if (notifyCombatEngaged)
        {
            // receiverValue1（判空前缓存的当前敌人智能管理器）：从管理器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginChase(bool)。
global::EnemyAIManager receiverValue1 = manager;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.NotifyCombatEngaged(host);
            }
        }

        SetObservableState(VillagerState.Chase);
        SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        chaseStartedTime = Time.time;
        ctx.TargetMoveSpeed = ctx.ClampStandingMoveParameter(chaseStartMoveSpeed);
        if (agent != null)
        {
            agent.isStopped = false;
        }

        AnimationPresenter.SetBoolean(HashATK, false);
        AnimationPresenter.SetBoolean(HashHIT, false);
        AnimationPresenter.SetBoolean(HashSearchMode, false);
        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginChase", $"退出: 状态={currentAction}, 移动模式={CurrentMovementMode}, " + $"agent.isStopped={agent != null && agent.isStopped}");
        }
    }

    /// <summary>BeginAlert 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyPerceptionModule.EndInvestigateWaiting()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.BeginAlert()；下游（含内部回调体） EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyPerceptionModule.EndInvestigateWaiting()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginAlert()
    {
        if (IsDead)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginAlert", "进入");
        }

        // receiverValue2（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginAlert()。
global::EnemyAIManager receiverValue2 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.ReleaseCombatReservation(host);
        }

        combatHoldActive = false;
        SetObservableState(VillagerState.Alert);
        SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        if (agent != null)
        {
            agent.isStopped = false;
        }

        PerceptionModule.EndInvestigateWaiting();
        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginAlert", $"退出: 状态={currentAction}, 移动模式={CurrentMovementMode}");
        }
    }

    /// <summary>BeginCombatHold 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopPathMovementWithAnimationBlend()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.BeginCombatHold()；下游（含内部回调体） EnemyActionCoordinator.DiagLog(string, string)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopPathMovementWithAnimationBlend()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyAnimationAdapter.SetBoolean(int, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginCombatHold()
    {
        if (IsDead || CombatModule.NativeCombatActive || CombatModule.HasUnfinishedStandingHit)
        {
            return;
        }

        if (combatHoldActive && CurrentMovementMode == EnemyMovementMode.FullyStopped)
        {
            return;
        }

        if (CanLogMovementDiagnostic)
        {
            DiagLog("BeginCombatHold", "进入");
        }

        // receiverValue3（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginCombatHold()。
global::EnemyAIManager receiverValue3 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.ReleaseCombatReservation(host);
        }

        combatHoldActive = true;
        SetObservableState(VillagerState.Alert);
        SetMovementMode(EnemyMovementMode.FullyStopped);
        StopPathMovementWithAnimationBlend();
        AnimationPresenter.SetBoolean(HashSearchMode, false);
    }

    /// <summary>UpdateCombatFacing 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.FacePosition(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCombatFacing()；下游（含内部回调体） EnemyActionCoordinator.FacePosition(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void UpdateCombatFacing()
    {
        if (host.HasSiegeBreachOrder)
        {
            FacePosition(host.door03TraversalRoute.DoorGate, alertTurnSpeed * 1.5f);
            return;
        }
        if (Player != null)
        {
            FacePosition(Player.transform.position, alertTurnSpeed * 1.5f);
        }
    }

    /// <summary>BeginPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyContext.ClampStandingMoveParameter(float)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、VillagerController.BeginPatrol()；下游（含内部回调体） EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyContext.ClampStandingMoveParameter(float)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginPatrol()
    {
        if (IsDead)
        {
            return;
        }

        // receiverValue4（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginPatrol()。
global::HouseThreeRegionNavigation receiverValue4 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.ReleaseCharacter(host);
        }

        // receiverValue5（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginPatrol()。
global::EnemyAIManager receiverValue5 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.ReleaseCombatReservation(host);
        }

        combatHoldActive = false;
        SetObservableState(VillagerState.Patrol);
        SetMovementMode(EnemyMovementMode.StandingPathRootMotion);
        ctx.TargetMoveSpeed = ctx.ClampStandingMoveParameter(patrolSpeed);
        if (agent != null)
        {
            agent.isStopped = false;
        }
    }

    /// <summary>BeginStationaryPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.GetPatrolTurnDirection(float)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopAndClearMovement()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.BeginStationaryPatrol(float)；下游（含内部回调体） EnemyActionCoordinator.GetPatrolTurnDirection(float)、EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopAndClearMovement()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="signedTurnAngle">中文含义：有符号转向角度。使用位置：作为 EnemyActionCoordinator.GetPatrolTurnDirection(float) 的参数。</param>
    internal void BeginStationaryPatrol(float signedTurnAngle)
    {
        if (IsDead)
        {
            return;
        }

        // receiverValue6（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginStationaryPatrol(float)。
global::HouseThreeRegionNavigation receiverValue6 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.ReleaseCharacter(host);
        }

        combatHoldActive = false;
        SetObservableState(VillagerState.Patrol);
        SetMovementMode(EnemyMovementMode.FullyStopped);
        StopAndClearMovement();
        AnimationPresenter.SetBoolean(HashSearchMode, true);
        AnimationPresenter.SetFloatValue(HashIdleBlend, UnityEngine.Random.Range(0f, 1f));
        AnimationPresenter.SetIntegerValue(HashTurnDir, GetPatrolTurnDirection(signedTurnAngle));
    }

    /// <summary>ReleaseStationaryPatrolTurn 的业务处理入口；本方法直接依赖 EnemyAnimationAdapter.SetIntegerValue(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ReleaseStationaryPatrolTurn()；下游（含内部回调体） EnemyAnimationAdapter.SetIntegerValue(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ReleaseStationaryPatrolTurn()
    {
        AnimationPresenter.SetIntegerValue(HashTurnDir, 0);
    }

    /// <summary>UpdateStationaryPatrolFacing 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.RotateTowards(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateStationaryPatrolFacing(Quaternion)；下游（含内部回调体） CharacterMotionExecutor.CommitNormalOrientation(Quaternion)、Quaternion.RotateTowards(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRotation">中文含义：目标旋转。使用位置：作为 Quaternion.RotateTowards(Quaternion, Quaternion, float) 的参数。</param>
    internal void UpdateStationaryPatrolFacing(Quaternion targetRotation)
    {
        host.motionExecutor.CommitNormalOrientation(Quaternion.RotateTowards(transform.rotation, targetRotation, stationaryPatrolTurnSpeed * Time.deltaTime));
    }

    /// <summary>EndStationaryPatrol 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.StopAndClearMovement()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.EndStationaryPatrol()；下游（含内部回调体） EnemyActionCoordinator.StopAndClearMovement()、EnemyAnimationAdapter.SetBoolean(int, bool)、EnemyAnimationAdapter.SetFloatValue(int, float)、EnemyAnimationAdapter.SetIntegerValue(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void EndStationaryPatrol()
    {
        AnimationPresenter.SetIntegerValue(HashTurnDir, 0);
        AnimationPresenter.SetBoolean(HashSearchMode, false);
        AnimationPresenter.SetFloatValue(HashIdleBlend, 0f);
        StopAndClearMovement();
    }

    /// <summary>BeginIdle 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopAndClearMovement()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、VillagerController.BeginIdle()；下游（含内部回调体） EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyActionCoordinator.StopAndClearMovement()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginIdle()
    {
        if (CombatModule.HasUnfinishedStandingHit)
        {
            return;
        }

        // receiverValue7（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginIdle()。
global::HouseThreeRegionNavigation receiverValue7 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.ReleaseCharacter(host);
        }

        // receiverValue8（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginIdle()。
global::EnemyAIManager receiverValue8 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.ReleaseCombatReservation(host);
        }

        combatHoldActive = false;
        SetObservableState(VillagerState.Idle);
        SetMovementMode(EnemyMovementMode.FullyStopped);
        ctx.TargetMoveSpeed = 0f;
        StopAndClearMovement();
    }

    /// <summary>SetPatrolTarget 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SetPatrolTarget(Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：EnemyActionCoordinator.SetPatrolTarget(Vector3)。</param>
    internal void SetPatrolTarget(Vector3 target)
    {
        patrolTarget = target;
        hasPatrolTarget = true;
    }

    /// <summary>ClearPatrolTarget 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ClearPatrolTarget()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ClearPatrolTarget()
    {
        hasPatrolTarget = false;
    }

    /// <summary>BeginDeath 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.BeginDeathShutdown(float)、ChainsawManAnimationBridge.StopChainsawLoop(bool)、CharacterHealthModule.ConfirmDeath()、Component.GetComponent&lt;VillagerQTEParticipant&gt;()、DropManager.OnEnemyDeath(Vector3)、EnemyActionCoordinator.ApplyDeathAnimatorParameters()、EnemyActionCoordinator.ClearCombatEngagement(bool) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyCombatModule.BeginDeath()、VillagerController.BeginDeath()；下游（含内部回调体） ChainsawManAnimationBridge.BeginDeathShutdown(float)、ChainsawManAnimationBridge.StopChainsawLoop(bool)、CharacterHealthModule.ConfirmDeath()、Component.GetComponent&lt;VillagerQTEParticipant&gt;()、DropManager.OnEnemyDeath(Vector3)、EnemyActionCoordinator.ApplyDeathAnimatorParameters()、EnemyActionCoordinator.ClearCombatEngagement(bool) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void BeginDeath()
    {
        if (deathExecuteHasBegin)
        {
            return;
        }

        // 生命已归零，梯上角色先持死亡凭证完成坠落，再执行地面死亡和掉落。
        if (specialTraversal != null && specialTraversal.TryBeginTerminalLadderFall())
        {
            return;
        }

        CombatModule.PrepareNativeDeathPresentation();
        CombatModule.CancelNativeCombat(false);
        // 复起待机和走跑也有独立输出，即使没有正在执行的倒地事务也要移交给死亡动画。
        AnimationPresenter.StopNativeCombat();
        AnimationPresenter.SetEnable(true);
        if (host.knifeLocalHitStop != null)
        {
            host.knifeLocalHitStop.Restore();
        }
        if (anim != null && anim.speed <= 0f)
        {
            AnimationPresenter.SetPlaySpeed(1f);
        }

        // qteParticipant（配对动作参与者）：保存 Component.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：EnemyActionCoordinator.BeginDeath()。
VillagerQTEParticipant qteParticipant = GetComponent<VillagerQTEParticipant>();
        // keepAssassinationPose（是否保持暗杀姿态）：使用位置：EnemyActionCoordinator.BeginDeath()。
bool keepAssassinationPose = qteParticipant != null && qteParticipant.IsPreservingAssassinationDeathPose;
        // receiverValue9（判空前缓存的房屋第三区域导航的当前）：从房屋第三区域导航的当前读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::HouseThreeRegionNavigation receiverValue9 = HouseThreeRegionNavigation.Current;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.ReleaseCharacter(host);
        }

        deathExecuteHasBegin = true;
        HealthModule.ConfirmDeath();
        CombatModule.ClearDeathCombatRequest();
        if (!keepAssassinationPose)
        {
            ApplyDeathAnimatorParameters();
        }

        if (!keepAssassinationPose)
        {
            // receiverValue10（判空前缓存的配对动作参与者）：从配对动作参与者读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::VillagerQTEParticipant receiverValue10 = qteParticipant;
            if (!global::System.Object.ReferenceEquals(receiverValue10, null))
            {
                receiverValue10.ForceExitQTEForDeath(0);
            }
        }

        ClearCombatEngagement(false);
        // receiverValue11（判空前缓存的敌人智能管理器的实例）：从敌人智能管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::EnemyAIManager receiverValue11 = EnemyAIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.ReleaseCombatReservation(host);
        }

        SetObservableState(VillagerState.Dead);
        // selectedValue12（分支选出的敌人移动模式的完全已停止）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 EnemyActionCoordinator.SetMovementMode(EnemyMovementMode, string, int) 的参数、EnemyActionCoordinator.BeginDeath()。
global::EnemyMovementMode selectedValue12;
        if (keepAssassinationPose)
        {
            selectedValue12 = EnemyMovementMode.FullyStopped;
        }
        else
        {
            selectedValue12 = EnemyMovementMode.FullRootMotion;
        }

        SetMovementMode(selectedValue12);
        StopAndClearMovement();
        if (!hasDroppedLoot)
        {
            hasDroppedLoot = true;
            // receiverValue13（判空前缓存的掉落管理器的实例）：从掉落管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::DropManager receiverValue13 = DropManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue13, null))
            {
                var mission = ChapterOneMissionManager.Instance;
                bool allowReserveKnife = mission != null && mission.UsesBellEnding &&
                    host.missionEnemyType == MissionEnemyType.Ganado && mission.TracksVillageEnemy(host);
                receiverValue13.OnEnemyDeath(transform.position, allowReserveKnife);
            }
        }

        if (!keepAssassinationPose)
        {
            ApplyDeathAnimatorParameters();
        }

        // receiverValue14（判空前缓存的碰撞体控制器）：从碰撞体控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::EnemyColliderController receiverValue14 = colliderCtrl;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.SetDisabled();
        }

        if (specialTraversal == null || !specialTraversal.ClearingImpactOpening)
        {
            SnapToGround();
        }
        if (missionEnemyType == MissionEnemyType.Sawman && chainsawBridge != null && deadChainsawDangerDuration > 0f)
        {
            chainsawBridge.BeginDeathShutdown(deadChainsawDangerDuration);
            CombatModule.BeginDeathContinuousDamage();
        }
        else
        {
            // receiverValue15（判空前缓存的电锯桥接）：从电锯桥接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.BeginDeath()。
global::ChainsawManAnimationBridge receiverValue15 = chainsawBridge;
            if (!global::System.Object.ReferenceEquals(receiverValue15, null))
            {
                receiverValue15.StopChainsawLoop(true);
            }
        }

        PlayVoice(ZombieVoiceType.Death);
        CombatModule.BindNativeDeathAction();
        host.PublishDeathFact();
    }

    /// <summary>InitializeAction 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RegisterAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；下游（含内部回调体） EnemyActionCoordinator.RegisterAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void InitializeAction()
    {
        currentAction = VillagerState.Idle;
        patrolCenter = transform.position;
        RegisterAction();
    }

    /// <summary>ExitCombatOccupancy 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()、EnemyCombatModule.BeginCrawlAttack()、EnemyPerceptionModule.ClearCombatEngagement(bool)、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、VillagerController.MaintainChasePathRecoveryFacing() 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void ExitCombatOccupancy()
    {
        combatHoldActive = false;
    }

    /// <summary>中文含义：租用版本。使用位置：作为 ObjectIdentity.ObjectIdentity(int, long) 的参数、EnemyActionCoordinator.RestoreRequest()。</summary>
    private long leaseVersion;
    /// <summary>中文含义：动作编号。使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。</summary>
    private long actionId;
    /// <summary>中文含义：是否允许请求。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyActionCoordinator.CallbackValid(ActionHandle) 等共 7 处。</summary>
    private bool allowRequest;
    /// <summary>中文含义：诊断。只读字段，初始化后不由普通写入口替换。使用位置：EnemyActionCoordinator.Commit(ActionRequest, bool)、EnemyActionCoordinator.ReadDiagnostics()、EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。</summary>
    private readonly ArchitectureDiagnosticLog diagnostics = new ArchitectureDiagnosticLog(64);
    /// <summary>中文含义：是否可以接收请求。使用位置：EnemyCombatModule.EndAttack()、EnemyPerceptionModule.ReceiveVillagePursuitOrder(Vector3)、VillagerController.RefreshChaseDestination()。</summary>
    internal bool CanReceiveRequest => allowRequest;
    /// <summary>中文含义：当前句柄。使用位置：作为 ActionResult.Accept(ActionHandle) 的参数、作为 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数 等共 29 处。</summary>
    public ActionHandle CurrentHandle { get; private set; }
    /// <summary>中文含义：身份标识。使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、DamageResolutionModule.Commit(DamageRequest, VillagerController, HitboxType, Vector3) 等共 7 处。</summary>
    public ObjectIdentity Identity => new ObjectIdentity(GetInstanceID(), leaseVersion);

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RestoreRequest()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        RestoreRequest();
    }

    /// <summary>RestoreRequest 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RegisterAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.OnEnable()、VillagerController.OnEnable()；下游（含内部回调体） EnemyActionCoordinator.RegisterAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RestoreRequest()
    {
        if (allowRequest)
        {
            return;
        }

        leaseVersion++;
        allowRequest = true;
        CurrentHandle = default;
        if (host != null && host.IsRuntimeInitialized)
        {
            RegisterAction();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.CloseAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        CloseAction();
    }

    /// <summary>CloseAction 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.Revoke()、EnemyCombatModule.EndAttack()、EnemyCombatModule.StopContinuousDamage()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.OnDisable()、VillagerController.OnDisable()；下游（含内部回调体） CharacterMotionExecutor.Revoke()、EnemyCombatModule.EndAttack()、EnemyCombatModule.StopContinuousDamage()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void CloseAction()
    {
        if (!allowRequest)
        {
            return;
        }

        allowRequest = false;
        CurrentHandle = default;
        if (host != null)
        {
            // receiverValue16（判空前缓存的宿主的角色运动执行器）：从宿主的角色运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyActionCoordinator.CloseAction()。
global::CharacterMotionExecutor receiverValue16 = host.motionExecutor;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.Revoke();
            }
        }

        if (host != null && CombatModule != null)
        {
            CombatModule.StopContinuousDamage();
        }

        if (host != null && host.IsRuntimeInitialized && CombatModule != null)
        {
            CombatModule.EndAttack();
        }
    }

    /// <summary>RegisterAction 的业务处理入口；本方法直接依赖 ActionHandle.ActionHandle(ObjectIdentity, long)、CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)、EnemyActionCoordinator.ApproveMotionMode(EnemyMovementMode)、EnemyActionCoordinator.InitializeAction()、EnemyActionCoordinator.RestoreRequest()、EnemyActionCoordinator.RevokePreviousAnimationCallback() 等共 6 处；下游（含内部回调体） ActionHandle.ActionHandle(ObjectIdentity, long)、CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RegisterAction()
    {
        CurrentHandle = new ActionHandle(Identity, ++actionId);
        if (host != null && host.motionExecutor != null)
        {
            host.motionExecutor.Authorize(host.CurrentMovementMode, CurrentHandle);
        }
    }

    /// <summary>CallbackValid 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handle">中文含义：句柄。使用位置：EnemyActionCoordinator.CallbackValid(ActionHandle)。</param>
    public bool CallbackValid(ActionHandle handle)
    {
        return allowRequest && isActiveAndEnabled && host != null && host.isActiveAndEnabled && !IsDead && !host.IsQTEControlled && handle.Valid && handle == CurrentHandle;
    }

    /// <summary>RequestAction 的业务处理入口；本方法直接依赖 ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle)、EnemyActionCoordinator.Commit(ActionRequest, bool)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChasePlayer.OnStart()、EnemyCombatModule.ApplyKnifeParryStagger(bool)、ExecuteAttack.OnStart()、ExecuteChaseRecovery.OnStart()、ExecuteCombat.OnStart() 等共 18 处；下游（含内部回调体） ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle)、EnemyActionCoordinator.Commit(ActionRequest, bool)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：作为 ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle) 的参数。</param>
    /// <param name="source">中文含义：来源。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。</param>
    /// <param name="crawlAttack">中文含义：是否爬行攻击。使用位置：作为 EnemyActionCoordinator.Commit(ActionRequest, bool) 的参数。</param>
    public ActionResult RequestAction(ActionKind action, string source, bool crawlAttack = false)
    {
        return Commit(new ActionRequest(new RequestContext(source, Identity, Identity, actionId + 1), action), crawlAttack);
    }

    /// <summary>Commit 的业务处理入口；本方法直接依赖 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double)、ArchitectureDiagnosticLog.Record(in ArchitectureDiagnosticEntry)、EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.RequestAction(ActionKind, string, bool)；下游（含内部回调体） ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double)、ArchitectureDiagnosticLog.Record(in ArchitectureDiagnosticEntry)、EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="request">中文含义：请求。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数、作为 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool) 的参数、EnemyActionCoordinator.Commit(ActionRequest, bool)。</param>
    /// <param name="crawlAttack">中文含义：是否爬行攻击。使用位置：作为 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool) 的参数。</param>
    public ActionResult Commit(ActionRequest request, bool crawlAttack = false)
    {
        // result（当前动作结果）：保存 EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool) 的返回结果，供当前步骤使用。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数、EnemyActionCoordinator.Commit(ActionRequest, bool)。
var result = ApprovalAndExecute(request, crawlAttack);
        diagnostics.Enable = host != null && host.enableMovementDiagnostic;
        // callReceiver20（调用前缓存的诊断）：从诊断读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyActionCoordinator.Commit(ActionRequest, bool)。
global::GameArchitecture.ArchitectureDiagnosticLog callReceiver20 = diagnostics;
        // argumentValue18（按原顺序缓存的实参请求的上下文）：从请求的上下文读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数。
global::GameArchitecture.RequestContext argumentValue18 = request.Context;
        // argumentValue19（按原顺序缓存的实参结果的句柄）：从结果的句柄读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数。
global::GameArchitecture.ActionHandle argumentValue19 = result.Handle;
        // selectedValue17（分支选出的诊断事件的请求接受）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数、EnemyActionCoordinator.Commit(ActionRequest, bool)。
global::GameArchitecture.DiagnosticEvent selectedValue17;
        if (result.HasAccept)
        {
            selectedValue17 = DiagnosticEvent.RequestAccept;
        }
        else
        {
            selectedValue17 = DiagnosticEvent.RequestReject;
        }

        callReceiver20.Record(new ArchitectureDiagnosticEntry(argumentValue18, argumentValue19, selectedValue17, result.Reason, ActionKind.None, request.Action, Time.timeAsDouble));
        return result;
    }

    /// <summary>ReadDiagnostics 的业务处理入口；本方法直接依赖 ArchitectureDiagnosticLog.ReadSnapshot()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ArchitectureDiagnosticLog.ReadSnapshot()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public System.Collections.Generic.IReadOnlyList<ArchitectureDiagnosticEntry> ReadDiagnostics() => diagnostics.ReadSnapshot();
    /// <summary>ApprovalAndExecute 的业务处理入口；本方法直接依赖 ActionResult.Accept(ActionHandle)、ActionResult.Reject(RejectionReason)、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginPatrol()、EnemyActionCoordinator.RegisterAction() 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.Commit(ActionRequest, bool)；下游（含内部回调体） ActionResult.Accept(ActionHandle)、ActionResult.Reject(RejectionReason)、EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginPatrol()、EnemyActionCoordinator.RegisterAction() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="request">中文含义：请求。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)。</param>
    /// <param name="crawlAttack">中文含义：是否爬行攻击。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)。</param>
    private ActionResult ApprovalAndExecute(ActionRequest request, bool crawlAttack)
    {
        if (!request.Context.Valid || request.Context.Target != Identity)
        {
            return ActionResult.Reject(RejectionReason.ParameterInvalid);
        }

        if (!allowRequest || !isActiveAndEnabled || host == null || !host.isActiveAndEnabled || !host.IsRuntimeInitialized)
        {
            return ActionResult.Reject(RejectionReason.NotYetReady);
        }

        if (request.ExpectedCurrentAction.Valid && request.ExpectedCurrentAction != CurrentHandle)
        {
            return ActionResult.Reject(RejectionReason.CallbackHasExpired);
        }

        if (IsDead && request.Action != ActionKind.Death)
        {
            return ActionResult.Reject(RejectionReason.HasDeath);
        }

        if (CombatModule.NativeCombatActive && request.Action != ActionKind.Death)
        {
            return ActionResult.Reject(RejectionReason.PermissionBeingOccupancy);
        }

        switch (request.Action)
        {
            case ActionKind.Attack:
                // selectedValue21（分支选出的是否可以爬行攻击的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)。
bool selectedValue21;
                if (crawlAttack)
                {
                    selectedValue21 = CombatModule.CanCrawlAttack();
                }
                else
                {
                    selectedValue21 = CombatModule.CanAttack();
                }

                if (!(selectedValue21))
                {
                    // selectedValue22（分支选出的拒绝原因的动作冲突）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 ActionResult.Reject(RejectionReason) 的参数、EnemyActionCoordinator.ApprovalAndExecute(ActionRequest, bool)。
global::GameArchitecture.RejectionReason selectedValue22;
                    if (host.IsAttackReady)
                    {
                        selectedValue22 = RejectionReason.ActionConflict;
                    }
                    else
                    {
                        selectedValue22 = RejectionReason.CooldownUnfinished;
                    }

                    return ActionResult.Reject(selectedValue22);
                }

                EnemyAIManager manager = EnemyAIManager.Instance;
                if (manager == null || !manager.TryAcquireAttackReservation(host))
                {
                    return ActionResult.Reject(RejectionReason.PermissionBeingOccupancy);
                }

                bool started;
                if (crawlAttack)
                {
                    started = CombatModule.BeginCrawlAttack();
                }
                else
                {
                    started = CombatModule.BeginAttack();
                }

                if (!started)
                {
                    manager.CancelAttackStart(host);
                    return ActionResult.Reject(RejectionReason.ActionConflict);
                }

                manager.ConfirmAttack(host, CurrentHandle);

                break;
            case ActionKind.HitReaction:
                if (!CombatModule.ReadPendingHit)
                {
                    return ActionResult.Reject(RejectionReason.ActionConflict);
                }

                if (CombatModule.TryReadNativeBodyReaction(out _, out ActionHandle activeReaction))
                {
                    return ActionResult.Accept(activeReaction);
                }

                // 连续受击即使姿态相同，也属于新的动作事务。
                RegisterAction();
                CombatModule.BeginHit();
                break;
            case ActionKind.Crawl:
                CombatModule.BeginCrawl();
                break;
            case ActionKind.GetUp:
                CombatModule.BeginStandUp();
                break;
            case ActionKind.Chase:
                if (IsVillageEntranceCombatLocked || (specialTraversal != null && specialTraversal.IsMovementProtected))
                {
                    return ActionResult.Reject(RejectionReason.PermissionBeingOccupancy);
                }

                BeginChase();
                break;
            case ActionKind.Traversal:
                if (IsVillageEntranceCombatLocked || specialTraversal == null || !specialTraversal.CanBeginLadderTraversal())
                {
                    return ActionResult.Reject(RejectionReason.ActionConflict);
                }

                RegisterAction();
                if (!specialTraversal.BeginLadderTraversal(CurrentHandle))
                {
                    return ActionResult.Reject(RejectionReason.ActionConflict);
                }

                break;
            case ActionKind.Patrol:
                BeginPatrol();
                break;
            case ActionKind.Idle:
                BeginIdle();
                break;
            case ActionKind.Death:
                if (specialTraversal != null && specialTraversal.DeferLadderGroundReaction)
                {
                    return ActionResult.Reject(RejectionReason.PermissionBeingOccupancy);
                }

                BeginDeath();
                break;
            default:
                return ActionResult.Reject(RejectionReason.ParameterInvalid);
        }

        if (!CurrentHandle.Valid)
        {
            RegisterAction();
        }

        return ActionResult.Accept(CurrentHandle);
    }

    internal MotionControlToken AuthorizeLadderDeath(ActionHandle traversal)
    {
        if (!allowRequest || !IsDead || deathExecuteHasBegin || !traversal.Valid ||
            traversal != CurrentHandle || specialTraversal == null || !specialTraversal.DeferLadderGroundReaction ||
            host.CurrentMovementMode != EnemyMovementMode.TraversalCodeDriven)
        {
            return default;
        }

        SetObservableState(VillagerState.Dead);
        return host.motionExecutor.CurrentToken;
    }

    /// <summary>RequestPathTarget 的业务处理入口；本方法直接依赖 PathAgent.SetDestination(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawl.OnUpdate()、MoveToPatrolPoint.OnStart()；下游（含内部回调体） PathAgent.SetDestination(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 PathAgent.SetDestination(Vector3) 的参数。</param>
    internal void RequestPathTarget(Vector3 target)
    {
        if (!IsDead && !host.IsQTEControlled && agent != null)
        {
            agent.SetDestination(target);
        }
    }

    /// <summary>RequestStopMovement 的业务处理入口；本方法直接依赖 VillagerController.StopAndClearMovement()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteCrawl.OnEnd()、ExecuteStandUp.OnEnd()、IdleWait.OnEnd()、InvestigatePlayer.OnEnd()、MoveToPatrolPoint.OnEnd() 等共 6 处；下游（含内部回调体） VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RequestStopMovement()
    {
        host.StopAndClearMovement();
    }

    /// <summary>ApproveMotionMode 的业务处理入口；本方法直接依赖 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、EnemyActionCoordinator.RegisterAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.SetMovementMode(EnemyMovementMode, string, int)、VillagerController.Start()、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle)、EnemyActionCoordinator.RegisterAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">中文含义：当前敌人移动模式。使用位置：作为 CharacterMotionExecutor.Authorize(EnemyMovementMode, ActionHandle) 的参数。</param>
    internal void ApproveMotionMode(EnemyMovementMode mode)
    {
        if (!allowRequest || host == null || host.motionExecutor == null)
        {
            return;
        }

        if (!CurrentHandle.Valid)
        {
            RegisterAction();
        }

        host.motionExecutor.Authorize(mode, CurrentHandle);
    }

    /// <summary>RequestMovementMode 的业务处理入口；本方法直接依赖 VillagerController.SetMovementMode(EnemyMovementMode, string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteStandUp.OnUpdate()；下游（含内部回调体） VillagerController.SetMovementMode(EnemyMovementMode, string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">中文含义：当前敌人移动模式。使用位置：作为 VillagerController.SetMovementMode(EnemyMovementMode, string, int) 的参数。</param>
    internal void RequestMovementMode(EnemyMovementMode mode)
    {
        host.SetMovementMode(mode);
    }

    /// <summary>RevokePreviousAnimationCallback 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.RegisterAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)、VillagerQTEParticipant.TryAcquire(int, QTEKind)；下游（含内部回调体） EnemyActionCoordinator.RegisterAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void RevokePreviousAnimationCallback()
    {
        // 攀梯事务自行读取待处理受击，保留同一个通行凭证直到完成或中断。
        if (allowRequest && (specialTraversal == null || !specialTraversal.DeferLadderGroundReaction))
        {
            RegisterAction();
        }
    }

    /// <summary>ReceiveAnimationNotification 的业务处理入口；本方法直接依赖 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double)、ArchitectureDiagnosticLog.Record(in ArchitectureDiagnosticEntry)、EnemyActionCoordinator.CallbackValid(ActionHandle)、EnemyCombatModule.HandleOnAttackComplete()、EnemyCombatModule.HandleOnAttackFirstHit()、EnemyCombatModule.HandleOnAttackHit()、EnemyCombatModule.HandleOnAttackSecondHit() 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)、EnemyActionCoordinator.NotifyAttackStateMachineExited(ActionHandle)、EnemyActionCoordinator.NotifyCrawlAttackStateExited(ActionHandle)、EnemyActionCoordinator.NotifyHitStateMachineExited(ActionHandle)、EnemyActionCoordinator.NotifyStandUpStateExited(ActionHandle) 等共 9 处；下游（含内部回调体） ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double)、ArchitectureDiagnosticLog.Record(in ArchitectureDiagnosticEntry)、EnemyActionCoordinator.CallbackValid(ActionHandle)、EnemyCombatModule.HandleOnAttackComplete()、EnemyCombatModule.HandleOnAttackFirstHit()、EnemyCombatModule.HandleOnAttackHit()、EnemyCombatModule.HandleOnAttackSecondHit() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handle">中文含义：句柄。使用位置：作为 EnemyActionCoordinator.CallbackValid(ActionHandle) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。</param>
    /// <param name="notification">中文含义：通知。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。</param>
    public bool ReceiveAnimationNotification(ActionHandle handle, string notification)
    {
        // valid（是否有效）：保存 EnemyActionCoordinator.CallbackValid(ActionHandle) 的返回结果，供当前步骤使用。使用位置：EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。
bool valid = CallbackValid(handle);
        diagnostics.Enable = host != null && host.enableMovementDiagnostic;
        // callReceiver28（调用前缓存的诊断）：从诊断读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。
global::GameArchitecture.ArchitectureDiagnosticLog callReceiver28 = diagnostics;
        // argumentValue25（按原顺序缓存的实参请求上下文）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数。
global::GameArchitecture.RequestContext argumentValue25 = new RequestContext(notification, handle.Object, Identity, handle.Id);
        // argumentValue26（按原顺序缓存的实参当前句柄）：从当前句柄读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数。
global::GameArchitecture.ActionHandle argumentValue26 = CurrentHandle;
        // argumentValue27（按原顺序缓存的实参诊断事件的请求接受）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数、EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。
global::GameArchitecture.DiagnosticEvent argumentValue27;
        if (valid)
        {
            argumentValue27 = DiagnosticEvent.RequestAccept;
        }
        else
        {
            argumentValue27 = DiagnosticEvent.RequestReject;
        }

        // selectedValue24（分支选出的拒绝原因的无）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 ArchitectureDiagnosticEntry.ArchitectureDiagnosticEntry(RequestContext, ActionHandle, DiagnosticEvent, RejectionReason, ActionKind, ActionKind, double) 的参数、EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。
global::GameArchitecture.RejectionReason selectedValue24;
        if (valid)
        {
            selectedValue24 = RejectionReason.None;
        }
        else
        {
            selectedValue24 = RejectionReason.CallbackHasExpired;
        }

        callReceiver28.Record(new ArchitectureDiagnosticEntry(argumentValue25, argumentValue26, argumentValue27, selectedValue24, ActionKind.None, ActionKind.None, Time.timeAsDouble));
        if (!valid)
        {
            return false;
        }

        switch (notification)
        {
            case "OnAttackHit":
                CombatModule.HandleOnAttackHit();
                break;
            case "OnAttackFirstHit":
                CombatModule.HandleOnAttackFirstHit();
                break;
            case "OnAttackSecondHit":
                CombatModule.HandleOnAttackSecondHit();
                break;
            case "OnAttackComplete":
                CombatModule.HandleOnAttackComplete();
                break;
            case "OnParryWindowOpen":
                CombatModule.HandleOnParryWindowOpen();
                break;
            case "OnParryWindowClose":
                CombatModule.HandleOnParryWindowClose();
                break;
            case "OnAttackStateMachineExit":
                CombatModule.NotifyAttackStateMachineExited();
                break;
            case "OnCrawlAttackExit":
                CombatModule.NotifyCrawlAttackStateExited();
                break;
            case "OnHitComplete":
                CombatModule.HandleOnHitComplete();
                break;
            case "OnHitStateMachineExit":
                CombatModule.NotifyHitStateMachineExited();
                break;
            case "OnStandUpComplete":
                CombatModule.HandleOnStandUpComplete();
                break;
            case "OnStandUpStateExit":
                CombatModule.NotifyStandUpStateExited();
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>NotifyAttackStateMachineExited 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttackStateMachineSMB.OnStateMachineExit(Animator, int, AnimatorControllerPlayable)；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数。</param>
    public bool NotifyAttackStateMachineExited(ActionHandle token) => ReceiveAnimationNotification(token, "OnAttackStateMachineExit");
    /// <summary>NotifyHitStateMachineExited 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitReactStateMachineSMB.OnStateMachineExit(Animator, int, AnimatorControllerPlayable)；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数。</param>
    public bool NotifyHitStateMachineExited(ActionHandle token) => ReceiveAnimationNotification(token, "OnHitStateMachineExit");
    /// <summary>NotifyCrawlAttackStateExited 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCrawlAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数。</param>
    public bool NotifyCrawlAttackStateExited(ActionHandle token) => ReceiveAnimationNotification(token, "OnCrawlAttackExit");
    /// <summary>NotifyStandUpStateExited 的业务处理入口；本方法直接依赖 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyStandUpStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数。</param>
    public bool NotifyStandUpStateExited(ActionHandle token) => ReceiveAnimationNotification(token, "OnStandUpStateExit");
}
