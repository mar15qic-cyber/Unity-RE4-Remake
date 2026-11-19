using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 里昂快速反应事件业务协调器。
/// 负责会话、输入窗口、配对对齐、结果提交和统一恢复，不直接写动画器。
/// </summary>
[DisallowMultipleComponent]
public partial class LeonQTECoordinator : MonoBehaviour
{
    /// <summary>玩家控制、动作与运动的结构化数据 CollisionPair；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct CollisionPair
    {
        /// <summary>中文含义：第一个碰撞体。使用位置：作为 List&lt;LeonQTECoordinator.CollisionPair&gt;.Add(LeonQTECoordinator.CollisionPair) 的参数、作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数、LeonQTECoordinator.RestorePairCollisions()。</summary>
        public Collider first;
        /// <summary>中文含义：第二个碰撞体。使用位置：作为 List&lt;LeonQTECoordinator.CollisionPair&gt;.Add(LeonQTECoordinator.CollisionPair) 的参数、作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数、LeonQTECoordinator.RestorePairCollisions()。</summary>
        public Collider second;
        /// <summary>中文含义：之前是否已忽略。使用位置：作为 List&lt;LeonQTECoordinator.CollisionPair&gt;.Add(LeonQTECoordinator.CollisionPair) 的参数、LeonQTECoordinator.RestorePairCollisions()。</summary>
        public bool wasIgnored;
    }

    /// <summary>中文含义：全局拥有者。使用位置：LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 等共 9 处。</summary>
    private static LeonQTECoordinator globalOwner;
    /// <summary>中文含义：全局会话序号。使用位置：LeonQTECoordinator.ResetStatics()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)。</summary>
    private static int globalSessionSequence;
    /// <summary>中文含义：当前里昂配对动作配置。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Clamp01(float) 的参数 等共 31 处。</summary>
    [Header("配置")]
    public LeonQTEConfig config;
    /// <summary>中文含义：提示扫描间隔。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("提示检测")]
    [Min(0.02f)]
    public float promptScanInterval = 0.05f;
    /// <summary>中文含义：里昂玩家控制器。使用位置：作为 IEnemyQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason) 的参数、作为 VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason) 的参数、作为 VillagerQTEParticipant.CanStartBodyTechnique(LeonController, out int, out QTECancelReason) 的参数 等共 16 处。</summary>
    [Header("组件引用")]
    public LeonController controller;
    /// <summary>中文含义：战斗。使用位置：LeonQTECoordinator.BindEvents()、LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 等共 6 处。</summary>
    public LeonCombat combat;
    /// <summary>中文含义：动画协调器。使用位置：LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BindEvents() 等共 17 处。</summary>
    public LeonAnimationActionCoordinator animationCoordinator;
    /// <summary>中文含义：输入桥接。使用位置：LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.Start() 等共 5 处。</summary>
    public LeonInputBridge inputBridge;
    /// <summary>中文含义：当前里昂动画绑定组件。使用位置：LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.ResolveCurrentSessionWeapon()、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool) 等共 5 处。</summary>
    public LeonAnimRig animRig;
    /// <summary>中文含义：动画控制器驱动器。使用位置：LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.RestoreWeaponTypeAfterQTE()、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool) 等共 5 处。</summary>
    public LeonAnimatorDriver animatorDriver;
    /// <summary>中文含义：武器视觉表现控制器。使用位置：LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()、LeonQTECoordinator.LateUpdate()、LeonQTECoordinator.ResolveCurrentSessionWeapon() 等共 6 处。</summary>
    public LeonWeaponVisualController weaponVisualController;
    /// <summary>中文含义：管理当前装备与武器切换的组件。使用位置：LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.ResolveWeaponFromHandler()、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool) 等共 4 处。</summary>
    public LeonWeaponHandler weaponHandler;
    /// <summary>中文含义：玩家身体。使用位置：LeonQTECoordinator.Start()、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。</summary>
    public Rigidbody playerBody;
    /// <summary>中文含义：玩家动画控制器。使用位置：作为 QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator) 的参数、LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 等共 11 处。</summary>
    public Animator playerAnimator;
    /// <summary>中文含义：玩家碰撞体集合。使用位置：作为 LeonQTECoordinator.ContainsCollider(Collider[], Collider) 的参数、LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant)、LeonQTECoordinator.Start() 等共 4 处。</summary>
    [Header("玩家碰撞体")]
    public Collider[] playerColliders;
    /// <summary>中文含义：腿部撕咬血迹后备高度。使用位置：LeonQTECoordinator.ResolveLegBiteHitPoint()。</summary>
    [Header("失败伤口定位")]
    [Min(0f)]
    public float legBiteBloodFallbackHeight = 0.42f;
    /// <summary>中文含义：腿部撕咬血迹后备侧边偏移量。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)、LeonQTECoordinator.ResolveLegBiteHitPoint()。</summary>
    [Min(0f)]
    public float legBiteBloodFallbackSideOffset = 0.18f;
    /// <summary>中文含义：颈部撕咬血迹前向偏移量。使用位置：LeonQTECoordinator.ResolveNeckBiteHitPoint()。</summary>
    [Min(0f)]
    public float neckBiteBloodForwardOffset = 0.05f;
    /// <summary>中文含义：参与者。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数 等共 26 处。</summary>
    private IEnemyQTEParticipant participant;
    /// <summary>中文含义：生效协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.Cancel(int, QTECancelReason) 等共 7 处。</summary>
    private Coroutine activeRoutine;
    /// <summary>中文含义：生效种类。使用位置：作为 QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator) 的参数、LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool) 等共 16 处。</summary>
    private QTEKind activeKind;
    /// <summary>中文含义：生效变体。使用位置：作为 LeonQTECoordinator.BeginResult(bool) 的参数、LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.CommitBiteDamage(int) 等共 9 处。</summary>
    private QTEAnimationVariant activeVariant;
    /// <summary>中文含义：阶段。使用位置：作为 Debug.LogWarning(object) 的参数、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.Cancel(int, QTECancelReason) 等共 19 处。</summary>
    private QTEPhase phase;
    /// <summary>中文含义：待处理结果。使用位置：作为 Debug.LogWarning(object) 的参数、LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.BeginResult(bool) 等共 22 处。</summary>
    private QTEResult pendingResult;
    /// <summary>中文含义：上一次拒绝原因。使用位置：LeonQTECoordinator.LastRejectionReason.get、LeonQTECoordinator.Reject(QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private QTECancelReason lastRejectionReason;
    /// <summary>中文含义：活动会话编号。使用位置：作为 Action&lt;int, QTEPhase&gt;.Invoke(int, QTEPhase) 的参数、作为 Debug.LogWarning(object) 的参数、作为 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的参数 等共 43 处。</summary>
    private int activeSessionId;
    /// <summary>中文含义：按下数量。使用位置：作为 Mathf.Min(int, int) 的参数、作为 QTEProgressInfo.QTEProgressInfo(int, int, int, float, float) 的参数、LeonQTECoordinator.ChangePhase(QTEPhase) 等共 5 处。</summary>
    private int pressCount;
    private float rearHoldElapsed;
    private VillagerController rearHelper;
    private GameArchitecture.ObjectIdentity rearHelperIdentity;
    private bool rearHelperHit;

    internal bool IsRearHelper(VillagerController enemy)
    {
        return activeSessionId > 0 && activeKind == QTEKind.RearGrab && phase == QTEPhase.WaitingForInput &&
            !rearHelperHit && rearHelper != null && rearHelper == enemy && !enemy.IsDead &&
            enemy.actionCoordinator.Identity == rearHelperIdentity;
    }
    /// <summary>中文含义：上一次输入帧。使用位置：LeonQTECoordinator.SubmitEscapePress(int)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private int lastInputFrame = -1;
    private const double EarlyEscapeWindow = .12;
    private double earlyEscapePressedAt = double.NegativeInfinity;
    /// <summary>中文含义：剩余时间。使用位置：作为 Mathf.Max(float, float) 的参数、作为 QTEProgressInfo.QTEProgressInfo(int, int, int, float, float) 的参数、LeonQTECoordinator.ChangePhase(QTEPhase) 等共 5 处。</summary>
    private float remainingTime;
    /// <summary>中文含义：阶段已用。使用位置：LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CheckAssassinationCommitFallback()、LeonQTECoordinator.CheckFailureExitFailsafe() 等共 6 处。</summary>
    private float phaseElapsed;
    /// <summary>中文含义：玩家动作是否已完成。使用位置：LeonQTECoordinator.HandlePlayerActionCompleted(AnimationActionTransaction)、LeonQTECoordinator.ResetPairFlags()、LeonQTECoordinator.TryAdvanceAfterPairCompletion() 等共 5 处。</summary>
    private bool playerActionCompleted;
    /// <summary>中文含义：敌人动作是否已完成。使用位置：LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.ForceReleasePlayerFromResult(int) 等共 11 处。</summary>
    private bool enemyActionCompleted;
    /// <summary>中文含义：是否玩家已退出。使用位置：LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction) 等共 6 处。</summary>
    private bool playerExited;
    /// <summary>中文含义：是否敌人已退出。使用位置：LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.HandleParticipantActionExited(int, EnemyQTEAction, int, bool)、LeonQTECoordinator.ResetPairFlags() 等共 5 处。</summary>
    private bool enemyExited;
    /// <summary>中文含义：是否伤害已提交。使用位置：作为 QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ForceReleasePlayerFromResult(int) 等共 6 处。</summary>
    private bool damageCommitted;
    /// <summary>中文含义：是否暗杀已提交。使用位置：LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.CheckAssassinationCommitFallback() 等共 9 处。</summary>
    private bool assassinationCommitted;
    /// <summary>中文含义：暗杀已提交时间。使用位置：LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.TryFinalizeCommittedAssassination() 等共 4 处。</summary>
    private float assassinationCommittedTime = float.NegativeInfinity;
    /// <summary>中文含义：是否清理运行中。使用位置：LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.CheckFailureExitFailsafe() 等共 12 处。</summary>
    private bool cleanupRunning;
    /// <summary>中文含义：是否已绑定事件。使用位置：LeonQTECoordinator.BindEvents()、LeonQTECoordinator.UnbindEvents()。</summary>
    private bool eventsBound;
    /// <summary>中文含义：是否已开始。使用位置：LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 等共 5 处。</summary>
    private bool started;
    /// <summary>中文含义：是否提示可用。使用位置：LeonQTECoordinator.IsPromptAvailable.get、LeonQTECoordinator.SetPromptAvailability(bool)。</summary>
    private bool promptAvailable;
    /// <summary>中文含义：下一次提示扫描时间。使用位置：LeonQTECoordinator.ClearBodyTechniqueSession()、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.OnEnable() 等共 5 处。</summary>
    private float nextPromptScanTime;
    private bool bodyTechniquePromptAvailable;
    /// <summary>中文含义：是否玩家锁定缓存。使用位置：LeonQTECoordinator.CacheAndLockPlayer()、LeonQTECoordinator.RestorePlayerLock()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private bool playerLockCached;
    /// <summary>中文含义：玩家运动。使用位置：LeonQTECoordinator.CacheAndLockPlayer()、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、LeonQTECoordinator.RestorePlayerLock() 等共 6 处。</summary>
    [Header("玩家运动执行引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("玩家运动")]
    [UnityEngine.InspectorName("玩家运动")]
    public LeonActionMotionDriver playerMotion;
    /// <summary>中文含义：上一次结束信息。使用位置：作为 Action&lt;QTESessionEndInfo&gt;.Invoke(QTESessionEndInfo) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.LastEndInfo.get。</summary>
    private QTESessionEndInfo lastEndInfo;
    /// <summary>中文含义：预期玩家动作。使用位置：作为 Debug.LogWarning(object) 的参数、LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool) 等共 6 处。</summary>
    private FullBodyActionType expectedPlayerAction;
    /// <summary>中文含义：预期敌人动作。使用位置：LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason) 等共 5 处。</summary>
    private EnemyQTEAction expectedEnemyAction;
    /// <summary>中文含义：结果运动是否生效。使用位置：LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.BeginResultMotion(bool)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason) 等共 5 处。</summary>
    private bool resultMotionActive;
    /// <summary>中文含义：结果玩家起始。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.BeginResultMotion(bool)。</summary>
    private Vector3 resultPlayerStart;
    /// <summary>中文含义：结果敌人起始。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.BeginResultMotion(bool)。</summary>
    private Vector3 resultEnemyStart;
    /// <summary>中文含义：结果玩家目标。使用位置：作为 LeonQTECoordinator.SetPlayerWorldPosition(Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.BeginResultMotion(bool)。</summary>
    private Vector3 resultPlayerTarget;
    /// <summary>中文含义：结果敌人目标。使用位置：作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.BeginResultMotion(bool)。</summary>
    private Vector3 resultEnemyTarget;
    /// <summary>中文含义：是否玩家已释放。使用位置：LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.BlocksOrdinaryDamage.get、LeonQTECoordinator.CheckFailureExitFailsafe() 等共 17 处。</summary>
    private bool playerReleased;
    /// <summary>中文含义：是否计划的恢复退出。使用位置：LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.RequestPlayerRecoveryExit() 等共 5 处。</summary>
    private bool plannedRecoveryExit;
    /// <summary>中文含义：玩家退出请求的时间。使用位置：LeonQTECoordinator.RequestPlayerRecoveryExit()、LeonQTECoordinator.Update()。</summary>
    private float playerExitRequestedTime;
    /// <summary>中文含义：敌人结束是否已经申请。使用位置：LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int) 等共 8 处。</summary>
    private bool enemyEndRequested;
    /// <summary>中文含义：敌人结束请求的时间。使用位置：LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.Update()。</summary>
    private float enemyEndRequestedTime;
    /// <summary>中文含义：清理会话编号。使用位置：LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private int cleanupSessionId;
    /// <summary>中文含义：生效体术目标。使用位置：LeonQTECoordinator.CancelBodyTechniqueIfActive()、LeonQTECoordinator.ClearBodyTechniqueSession()、LeonQTECoordinator.HandleBodyTechniqueHit(int, FullBodyActionType) 等共 4 处。</summary>
    private VillagerQTEParticipant activeBodyTechniqueTarget;
    /// <summary>中文含义：生效体术会话编号。使用位置：作为 VillagerQTEParticipant.CancelBodyTechnique(int) 的参数、LeonQTECoordinator.CancelBodyTechniqueIfActive()、LeonQTECoordinator.ClearBodyTechniqueSession() 等共 8 处。</summary>
    private int activeBodyTechniqueSessionId;
    /// <summary>中文含义：活动会话武器。使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveQTEExitWeapon()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private WeaponType activeSessionWeapon = WeaponType.Pistol;
    /// <summary>中文含义：待处理武器恢复。使用位置：作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private WeaponType pendingWeaponRestore = WeaponType.Pistol;
    /// <summary>中文含义：武器恢复是否仍待处理。使用位置：LeonQTECoordinator.RestoreWeaponTypeAfterQTE()、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private bool weaponRestorePending;
    /// <summary>中文含义：武器恢复帧集合。使用位置：LeonQTECoordinator.RestoreWeaponTypeAfterQTE()、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    private int weaponRestoreFrames;
    /// <summary>中文含义：暗杀小刀视觉表现是否生效。使用位置：LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()、LeonQTECoordinator.LateUpdate()、LeonQTECoordinator.SetAssassinationKnifeVisualIfNeeded(QTEKind)。</summary>
    private bool assassinationKnifeVisualActive;
    /// <summary>中文含义：配对动作挣脱循环句柄。使用位置：作为 AudioManager.Stop(AudioPlayHandle) 的参数、LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData)、LeonQTECoordinator.StopQTEStruggleLoop() 等共 4 处。</summary>
    private AudioPlayHandle qteStruggleLoopHandle;
    /// <summary>中文含义：配对动作成功状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.CheckPlayerRecoveryThreshold()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。</summary>
    private static readonly int HashQTESuccessState = Animator.StringToHash("QTESuccess");
    /// <summary>中文含义：配对动作失败状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。</summary>
    private static readonly int HashQTEFailState = Animator.StringToHash("QTEFail");
    /// <summary>中文含义：武器类型的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数、LeonQTECoordinator.EnsureWeaponParameterCache()。</summary>
    private static readonly int HashWeaponType = Animator.StringToHash("WeaponType");
    /// <summary>中文含义：小刀模式的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数、LeonQTECoordinator.EnsureWeaponParameterCache()。</summary>
    private static readonly int HashKnifeMode = Animator.StringToHash("KnifeMode");
    /// <summary>中文含义：碰撞配对集合。只读字段，初始化后不由普通写入口替换。使用位置：LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant)、LeonQTECoordinator.RestorePairCollisions()。</summary>
    private readonly List<CollisionPair> collisionPairs = new List<CollisionPair>();
    /// <summary>中文含义：配对动作会话已开始时的通知回调。事件通信成员 OnQTESessionStarted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、QTECameraPresenter.BindEvents()、QTECameraPresenter.UnbindEvents() 等共 5 处。</summary>
    public event Action<int, QTEKind> OnQTESessionStarted;
    /// <summary>中文含义：配对动作阶段变化时的通知回调。事件通信成员 OnQTEPhaseChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.ChangePhase(QTEPhase)、QTECameraPresenter.BindEvents()、QTECameraPresenter.UnbindEvents() 等共 5 处。</summary>
    public event Action<int, QTEPhase> OnQTEPhaseChanged;
    /// <summary>中文含义：配对动作撕咬接触时的通知回调。事件通信成员 OnQTEBiteContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.HandleBiteContact(int)、QTECameraPresenter.BindEvents()、QTECameraPresenter.UnbindEvents()。</summary>
    public event Action<int> OnQTEBiteContact;
    /// <summary>中文含义：配对动作暗杀接触时的通知回调。事件通信成员 OnQTEAssassinationContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.HandleAssassinationContact(int)、QTECameraPresenter.BindEvents()、QTECameraPresenter.UnbindEvents()。</summary>
    public event Action<int> OnQTEAssassinationContact;
    /// <summary>中文含义：配对动作进度变化时的通知回调。事件通信成员 OnQTEProgressChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.PublishProgress()、QTEPromptView.BindEvents()、QTEPromptView.UnbindEvents()。</summary>
    public event Action<QTEProgressInfo> OnQTEProgressChanged;
    /// <summary>中文含义：配对动作会话已结束时的通知回调。事件通信成员 OnQTESessionEnded，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、QTECameraPresenter.BindEvents()、QTECameraPresenter.UnbindEvents() 等共 5 处。</summary>
    public event Action<QTESessionEndInfo> OnQTESessionEnded;
    /// <summary>中文含义：配对动作提示可用性变化时的通知回调。事件通信成员 OnQTEPromptAvailabilityChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SetPromptAvailability(bool)、QTEPromptView.BindEvents()、QTEPromptView.UnbindEvents()。</summary>
    public event Action<bool> OnQTEPromptAvailabilityChanged;
    /// <summary>中文含义：当前会话编号。</summary>
    public int CurrentSessionId => activeSessionId;
    /// <summary>中文含义：当前阶段。</summary>
    public QTEPhase CurrentPhase => phase;
    /// <summary>中文含义：上一次拒绝原因。使用位置：作为 VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind) 的参数。</summary>
    public QTECancelReason LastRejectionReason => lastRejectionReason;
    /// <summary>中文含义：是否生效。使用位置：LeonQTECoordinator.BlocksOrdinaryDamage.get、LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.CanQueryBodyTechniquePrompt() 等共 15 处。</summary>
    public bool IsActive => activeSessionId != 0;
    /// <summary>中文含义：上一次结束信息。</summary>
    public QTESessionEndInfo LastEndInfo => lastEndInfo;
    /// <summary>中文含义：玩家操作是否被锁定。</summary>
    public bool IsPlayerControlLocked => IsActive && !playerReleased;
    /// <summary>中文含义：是否阻断项普通伤害。使用位置：LeonController.ApplyDamage(float, Vector3, LeonHitDirection)。</summary>
    public bool BlocksOrdinaryDamage => IsActive && !playerReleased;
    /// <summary>中文含义：是否提示可用。使用位置：作为 QTEPromptView.HandlePromptAvailabilityChanged(bool) 的参数。</summary>
    public bool IsPromptAvailable => promptAvailable;

    /// <summary>TryGetPromptPresentation 的业务处理入口；本方法直接依赖 LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 QTEPromptView.HandlePromptAvailabilityChanged(bool)、QTEPromptView.HandleSessionStarted(int, QTEKind)、QTEPromptView.LateUpdate()；下游（含内部回调体） LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetRoot">中文含义：目标根节点。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)。</param>
    /// <param name="actionText">中文含义：动作文字。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)。</param>
    public bool TryGetPromptPresentation(out Transform targetRoot, out string actionText)
    {
        targetRoot = null;
        actionText = string.Empty;
        if (IsActive && participant != null && participant.Root != null)
        {
            targetRoot = participant.Root;
            if (activeKind == QTEKind.KnifeAssassination)
            {
                actionText = "暗杀";
            }
            else
            {
                actionText = "挣脱";
            }

            return true;
        }

        if (TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant /* 中文含义：体术目标。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bodyTechniqueTarget, out _))
        {
            targetRoot = bodyTechniqueTarget.transform;
            actionText = "体术";
            return true;
        }

        if (TryGetPromptCandidate(out IEnemyQTEParticipant /* 中文含义：暗杀目标。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ assassinationTarget, out _))
        {
            targetRoot = assassinationTarget.Root;
            actionText = "暗杀";
            return true;
        }

        return false;
    }

    /// <summary>ResetStatics 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        globalOwner = null;
        globalSessionSequence = 0;
    }

    /// <summary>
    /// 向只读表现模块提供当前会话的参与者上下文。
    /// 会话结束或编号不匹配时不会返回已经释放的敌人引用。
    /// </summary>
    /// <remarks>静态调用链：上游 QTECameraPresenter.HandleSessionStarted(int, QTEKind)；下游（含内部回调体） QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)。</param>
    /// <param name="context">中文含义：上下文。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)。</param>
    public bool TryGetSessionContext(int sessionId, out QTESessionContext context)
    {
        if (sessionId == 0 || sessionId != activeSessionId || participant == null || participant.Root == null)
        {
            context = default;
            return false;
        }

        context = new QTESessionContext(activeSessionId, activeKind, transform, playerAnimator, participant.Root, participant.Animator);
        return context.IsValid;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (playerMotion == null)
        {
            playerMotion = GetComponent<LeonActionMotionDriver>();
        }

        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        if (combat == null)
        {
            combat = GetComponent<LeonCombat>();
        }

        if (animationCoordinator == null)
        {
            animationCoordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (inputBridge == null)
        {
            inputBridge = GetComponent<LeonInputBridge>();
        }

        if (animRig == null)
        {
            animRig = GetComponent<LeonAnimRig>();
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<LeonAnimatorDriver>();
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }


        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }

        if (playerBody == null)
        {
            playerBody = GetComponent<Rigidbody>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }

        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponentsInChildren<Collider>(true);
        }

        EnsureWeaponParameterCache();
        started = true;
        nextPromptScanTime = 0f;
        BindEvents();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (started)
        {
            BindEvents();
            nextPromptScanTime = 0f;
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.CancelBodyTechniqueIfActive()、LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()、LeonQTECoordinator.SetPromptAvailability(bool)、LeonQTECoordinator.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        SetPromptAvailability(false);
        CancelBodyTechniqueIfActive();
        if (IsActive)
        {
            Cancel(activeSessionId, QTECancelReason.CoordinatorDisabled);
        }

        DisposeNativePairFade();
        DisposeNativeDeathPose();
        EndAssassinationKnifeVisualIfNeeded();
        UnbindEvents();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.CheckAssassinationCommitFallback()、LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.CheckPlayerRecoveryThreshold()、LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()、LeonQTECoordinator.DecideResult(bool) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        UpdateNativePairFade();
        if (!IsActive)
        {
            return;
        }

        if (controller == null || controller.currentState == LeonState.Dead)
        {
            Cancel(activeSessionId, QTECancelReason.PlayerDied);
            return;
        }

        if (participant == null || (!participant.IsAlive && !((activeKind == QTEKind.KnifeAssassination || activeKind == QTEKind.GroundFinisher) && pendingResult == QTEResult.Success)))
        {
            Cancel(activeSessionId, QTECancelReason.EnemyDied);
            return;
        }

        if (cleanupRunning)
        {
            return;
        }

        if (nativePair != null)
        {
            UpdateNativePair();
            return;
        }

        phaseElapsed += Time.deltaTime;
        ApplyResultMotion();
        UpdateQTEStruggleLoopPosition();
        if (phase == QTEPhase.WaitingForInput)
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            PublishProgress();
            if (remainingTime <= 0f)
            {
                DecideResult(false);
            }

            return;
        }

        if (phase == QTEPhase.SuccessResult && !plannedRecoveryExit)
        {
            CheckPlayerRecoveryThreshold();
            if (plannedRecoveryExit)
            {
                return;
            }
        }

        CheckAssassinationCommitFallback();
        CheckResultPlayerReleaseFailsafe();
        if (IsCommittedAssassinationSuccess())
        {
            TryFinalizeCommittedAssassination();
            return;
        }

        if (plannedRecoveryExit && !playerReleased && Time.time - playerExitRequestedTime >= config.playerExitSafetyTimeout)
        {
            HandlePlayerExitTimeout();
            return;
        }

        if (enemyEndRequested && pendingResult == QTEResult.Success && Time.time - enemyEndRequestedTime >= config.enemyExitSafetyTimeout)
        {
            HandleEnemyExitTimeout();
            return;
        }

        CheckFailureExitFailsafe();
        if (!IsActive || cleanupRunning)
        {
            return;
        }

        if (config != null && phase != QTEPhase.Requested && phase != QTEPhase.Aligning && phase != QTEPhase.Completed && phase != QTEPhase.Cancelled && phaseElapsed >= config.animationSafetyTimeout)
        {
            Cancel(activeSessionId, QTECancelReason.SafetyTimeout);
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonQTECoordinator.RestoreWeaponTypeAfterQTE()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        DispatchNativePairContact();
        RestoreWeaponTypeAfterQTE();
        UpdatePromptAvailability();
        // 配对动画可能重写渲染器开关，因此在每帧动画求值后重申小刀可见。
        if (assassinationKnifeVisualActive && IsActive)
        {
            if (weaponVisualController == null)
            {
                weaponVisualController = GetComponent<LeonWeaponVisualController>();
            }

            // receiverValue2（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.LateUpdate()。
global::LeonWeaponVisualController receiverValue2 = weaponVisualController;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.SetKnifeModeVisible(true);
            }
        }
    }

    /// <summary>BindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.OnEnable()、LeonQTECoordinator.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound || animationCoordinator == null)
        {
            return;
        }

        animationCoordinator.OnQTEActionCompleted += HandlePlayerActionCompleted;
        animationCoordinator.OnQTEActionExited += HandlePlayerActionExited;
        animationCoordinator.OnQTEActionInterrupted += HandlePlayerActionInterrupted;
        animationCoordinator.OnMeleeActionExited += HandleMeleeActionExited;
        animationCoordinator.OnMeleeActionInterrupted += HandleMeleeActionInterrupted;
        animationCoordinator.OnDeathEntered += HandlePlayerDeath;
        if (combat != null)
        {
            combat.OnQTEBodyTechniqueHit += HandleBodyTechniqueHit;
        }

        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (animationCoordinator != null)
        {
            animationCoordinator.OnQTEActionCompleted -= HandlePlayerActionCompleted;
            animationCoordinator.OnQTEActionExited -= HandlePlayerActionExited;
            animationCoordinator.OnQTEActionInterrupted -= HandlePlayerActionInterrupted;
            animationCoordinator.OnMeleeActionExited -= HandleMeleeActionExited;
            animationCoordinator.OnMeleeActionInterrupted -= HandleMeleeActionInterrupted;
            animationCoordinator.OnDeathEntered -= HandlePlayerDeath;
        }

        if (combat != null)
        {
            combat.OnQTEBodyTechniqueHit -= HandleBodyTechniqueHit;
        }

        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        eventsBound = false;
    }

    /// <summary>
    /// 检查交接资格，不登记会话，也不改变角色或动画状态。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.IsQTERequestEligible(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） IEnemyQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、LeonQTEConfig.GetVariant(QTEKind)、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：作为 IEnemyQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason) 的参数、作为 LeonQTEConfig.GetVariant(QTEKind) 的参数、作为 LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason) 的参数 等共 4 处。</param>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：作为 LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason) 的参数、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)。</param>
    /// <param name="reason">中文含义：原因。输出参数，由本入口写入并交给调用方。使用位置：作为 IEnemyQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason) 的参数、作为 LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason) 的参数、LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)。</param>
    public bool CanStart(QTEKind kind, IEnemyQTEParticipant targetParticipant, out QTECancelReason reason)
    {
        if (kind == QTEKind.LegBite)
        {
            reason = QTECancelReason.UnsupportedKind;
            return false;
        }

        reason = QTECancelReason.None;
        if (!started || config == null || animationCoordinator == null || !animationCoordinator.isActiveAndEnabled || inputBridge == null || controller == null || playerAnimator == null || playerMotion == null)
        {
            reason = QTECancelReason.ResourceMissing;
            return false;
        }

        if (IsActive || globalOwner != null)
        {
            reason = QTECancelReason.GlobalSessionBusy;
            return false;
        }

        if (targetParticipant == null || !targetParticipant.IsAvailable)
        {
            reason = QTECancelReason.InvalidParticipant;
            return false;
        }

        if (controller.currentState == LeonState.Dead || controller.currentState == LeonState.Staggered || animationCoordinator.HasActiveFullBodyAction && !(kind == QTEKind.FrontGrab && combat != null && combat.knifeModule != null && combat.knifeModule.Active))
        {
            reason = QTECancelReason.PlayerUnavailable;
            return false;
        }

        // variant（变体）：保存 LeonQTEConfig.GetVariant(QTEKind) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)。
QTEAnimationVariant variant = config.GetVariant(kind);
        if (variant == null || (kind != QTEKind.FrontGrab && kind != QTEKind.GroundFinisher && !variant.IsComplete))
        {
            reason = QTECancelReason.ResourceMissing;
            return false;
        }

        return targetParticipant.CanStart(controller, kind, out reason) && ValidatePairing(targetParticipant, kind, out reason);
    }

    /// <summary>
    /// 发起快速反应事件。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStartNearestPrompt(out int)、TryStartEnemyQTEBase.OnUpdate()、TryStartFrontGrabQTE.TryStartStandingGrab(LeonQTECoordinator, QTEKind)、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)；下游（含内部回调体） Action&lt;int, QTEKind&gt;.Invoke(int, QTEKind)、IEnemyQTEParticipant.ApplyAnimationVariant(QTEAnimationVariant)、IEnemyQTEParticipant.TryAcquire(int, QTEKind)、LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)、LeonAnimationActionCoordinator.ClearQTEAnimationVariant()、LeonController.PrepareForQTE()、LeonInputBridge.EnterQTEInput(int) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：作为 Action&lt;int, QTEKind&gt;.Invoke(int, QTEKind) 的参数、作为 IEnemyQTEParticipant.TryAcquire(int, QTEKind) 的参数、作为 LeonQTEConfig.GetVariant(QTEKind) 的参数 等共 6 处。</param>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：作为 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason) 的参数、作为 LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant) 的参数 等共 5 处。</param>
    /// <param name="sessionId">中文含义：会话编号。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</param>
    public bool TryStart(QTEKind kind, IEnemyQTEParticipant targetParticipant, out int sessionId)
    {
        sessionId = 0;
        lastRejectionReason = QTECancelReason.None;
        if (!CanStart(kind, targetParticipant, out QTECancelReason /* 中文含义：原因。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ reason))
        {
            return Reject(reason);
        }

        // variant（变体）：保存 LeonQTEConfig.GetVariant(QTEKind) 的返回结果，供当前步骤使用。使用位置：作为 IEnemyQTEParticipant.ApplyAnimationVariant(QTEAnimationVariant) 的参数、作为 LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant) 的参数、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。
QTEAnimationVariant variant = config.GetVariant(kind);
        // newSessionId（新会话编号）：使用位置：作为 Action<int, QTEKind>.Invoke(int, QTEKind) 的参数、作为 IEnemyQTEParticipant.TryAcquire(int, QTEKind) 的参数、作为 LeonInputBridge.EnterQTEInput(int) 的参数 等共 6 处。
int newSessionId = ++globalSessionSequence;
        if (newSessionId <= 0)
        {
            globalSessionSequence = 1;
            newSessionId = 1;
        }

        globalOwner = this;
        activeSessionId = newSessionId;
        DisposeNativePairFade();
        activeKind = kind;
        activeVariant = variant;
        participant = targetParticipant;
        if (UsesNativeSawPair(kind, targetParticipant) && !TryLoadNativeSawPair(out nativePair))
        {
            participant = null;
            activeVariant = null;
            activeSessionId = 0;
            globalOwner = null;
            return Reject(QTECancelReason.ResourceMissing);
        }
        if (nativePair == null && (useNativeVillagerPairs || config.nativeCameras != null
            && (kind == QTEKind.FrontGrab && config.useNativeFrontGrab
                || kind == QTEKind.RearBiteNeck && config.useNativeRearGrab
                || kind == QTEKind.KnifeAssassination && config.useNativeAssassination)))
        {
            TryLoadNativeVillagerPair(kind, targetParticipant, out nativePair);
        }
        bool missingGroundFinisher = kind == QTEKind.GroundFinisher
            && !TryLoadGroundFinisher(targetParticipant, out nativePair, out groundFinisherMotion);
        bool missingRearGrab = kind == QTEKind.RearBiteNeck && config.useNativeRearGrab && nativePair == null;
        if (missingGroundFinisher || missingRearGrab)
        {
            participant = null;
            activeVariant = null;
            activeSessionId = 0;
            globalOwner = null;
            return Reject(QTECancelReason.ResourceMissing);
        }
        activeSessionWeapon = ResolveCurrentSessionWeapon();
        pendingWeaponRestore = activeSessionWeapon;
        weaponRestorePending = false;
        weaponRestoreFrames = 0;
        pressCount = 0;
        rearHoldElapsed = 0f;
        rearHelper = null;
        rearHelperHit = false;
        lastInputFrame = -1;
        earlyEscapePressedAt = double.NegativeInfinity;
        remainingTime = config.inputDuration;
        phaseElapsed = 0f;
        pendingResult = QTEResult.None;
        damageCommitted = false;
        assassinationCommitted = false;
        assassinationCommittedTime = float.NegativeInfinity;
        cleanupRunning = false;
        playerReleased = false;
        plannedRecoveryExit = false;
        enemyEndRequested = false;
        cleanupSessionId = 0;
        ResetPairFlags();
        SubscribeParticipant(targetParticipant);
        // variantApplied（是否变体已应用）：使用位置：LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。
bool variantApplied;
        if (variant.IsComplete && nativePair == null)
        {
            variantApplied = animationCoordinator.ApplyQTEAnimationVariant(variant) && targetParticipant.ApplyAnimationVariant(variant);
        }
        else
        {
            variantApplied = true;
        }

        if (!variantApplied)
        {
            UnsubscribeParticipant(targetParticipant);
            animationCoordinator.ClearQTEAnimationVariant();
            participant = null;
            activeSessionId = 0;
            if (globalOwner == this)
            {
                globalOwner = null;
            }

            return Reject(QTECancelReason.ResourceMissing);
        }

        if (!targetParticipant.TryAcquire(newSessionId, kind))
        {
            StopNativePair();
            UnsubscribeParticipant(targetParticipant);
            participant = null;
            animationCoordinator.ClearQTEAnimationVariant();
            activeSessionId = 0;
            if (globalOwner == this)
            {
                globalOwner = null;
            }

            return Reject(QTECancelReason.EnemyUnavailable);
        }

        pairedKnife = null;
        if ((kind == QTEKind.GroundFinisher || kind == QTEKind.KnifeAssassination) && InventoryManager.Instance != null)
        {
            pairedKnife = InventoryManager.Instance.GetPreferredKnife();
        }

        CacheAndLockPlayer();
        if (!playerLockCached)
        {
            Cancel(newSessionId, QTECancelReason.ResourceMissing);
            return Reject(QTECancelReason.ResourceMissing);
        }

        IgnorePairCollisions(targetParticipant);
        if (!controller.PrepareForQTE())
        {
            Cancel(newSessionId, QTECancelReason.SpaceBlocked);
            sessionId = 0;
            return Reject(QTECancelReason.SpaceBlocked);
        }

        SetAssassinationKnifeVisualIfNeeded(kind);
        if (!inputBridge.EnterQTEInput(newSessionId))
        {
            Cancel(newSessionId, QTECancelReason.ResourceMissing);
            sessionId = 0;
            return Reject(QTECancelReason.ResourceMissing);
        }

        SetPromptAvailability(false);
        ChangePhase(QTEPhase.Requested);
        // receiverValue4（判空前缓存的配对动作会话已开始时的通知回调）：从配对动作会话已开始时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。
global::System.Action<int, global::QTEKind> receiverValue4 = OnQTESessionStarted;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Invoke(newSessionId, kind);
        }

        sessionId = newSessionId;
        if (nativePair != null)
        {
            if (!BeginNativePair(newSessionId))
            {
                sessionId = 0;
                return false;
            }
        }
        else
        {
            activeRoutine = StartCoroutine(AlignAndEnter(newSessionId));
        }
        return true;
    }

    /// <summary>SubmitEscapePress 的业务处理入口；本方法直接依赖 LeonQTECoordinator.DecideResult(bool)、LeonQTECoordinator.PublishProgress()、Mathf.Min(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnEscapePress(InputAction.CallbackContext)；下游（含内部回调体） LeonQTECoordinator.DecideResult(bool)、LeonQTECoordinator.PublishProgress()、Mathf.Min(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonQTECoordinator.SubmitEscapePress(int)。</param>
    public bool SubmitEscapePress(int sessionId)
    {
        return SubmitEscapePress(sessionId, Time.timeAsDouble);
    }

    internal bool SubmitEscapePress(int sessionId, double pressedAt)
    {
        if (sessionId <= 0 || sessionId != activeSessionId || cleanupRunning
            || activeVariant != null && (activeVariant.automaticSuccess || activeVariant.automaticFailure))
        {
            return false;
        }
        if (activeKind == QTEKind.RearGrab)
        {
            return false;
        }
        if (phase == QTEPhase.Entering)
        {
            if (nativePair != null && (nativePair.InputFrame - nativePair.SourceFrame) / nativePair.FrameRate > EarlyEscapeWindow)
            {
                return false;
            }
            earlyEscapePressedAt = pressedAt;
            return true;
        }

        if (phase != QTEPhase.WaitingForInput)
        {
            return false;
        }

        if (Time.frameCount == lastInputFrame)
        {
            return false;
        }

        lastInputFrame = Time.frameCount;
        pressCount = Mathf.Min(config.requiredPresses, pressCount + 1);
        PublishProgress();
        if (pressCount >= config.requiredPresses)
        {
            if (nativePair != null)
            {
                nativePair.QueueEscape();
            }
            else
            {
                DecideResult(true);
            }
        }

        return true;
    }

    /// <summary>Cancel 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.CancelQTE(int)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)、LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.HandleParticipantInvalidated(int, QTECancelReason)、LeonQTECoordinator.HandlePlayerActionInterrupted(AnimationActionTransaction) 等共 11 处；下游（含内部回调体） LeonAnimationActionCoordinator.CancelQTE(int)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonAnimationActionCoordinator.CancelQTE(int) 的参数、LeonQTECoordinator.Cancel(int, QTECancelReason)。</param>
    /// <param name="reason">中文含义：原因。使用位置：作为 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason) 的参数。</param>
    public void Cancel(int sessionId, QTECancelReason reason)
    {
        if (sessionId == 0 || sessionId != activeSessionId)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (phase != QTEPhase.Cancelled)
        {
            ChangePhase(QTEPhase.Cancelled);
        }

        // receiverValue5（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.Cancel(int, QTECancelReason)。
global::LeonAnimationActionCoordinator receiverValue5 = animationCoordinator;
        // optionalValue6（条件访问得到的取消配对动作的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.Cancel(int, QTECancelReason)。
bool? optionalValue6;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            optionalValue6 = receiverValue5.CancelQTE(sessionId);
        }
        else
        {
            optionalValue6 = default;
        }

        FinishCleanup(QTEResult.Cancelled, reason);
    }

    /// <summary>
    /// 由里昂生命值入口调用并一次性消费失败伤害许可。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonQTECoordinator.TryConsumeFailureDamageAuthorization(int)。</param>
    public bool TryConsumeFailureDamageAuthorization(int sessionId)
    {
        if (sessionId != activeSessionId || damageCommitted)
        {
            return false;
        }

        if (phase != QTEPhase.FailureResult)
        {
            return false;
        }

        damageCommitted = true;
        return true;
    }

    /// <summary>AlignAndEnter 的业务处理入口；本方法直接依赖 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、LeonAnimationActionCoordinator.RequestQTE(int)、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant)、LeonQTECoordinator.ResetPairFlags()、LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、LeonAnimationActionCoordinator.RequestQTE(int)、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant)、LeonQTECoordinator.ResetPairFlags()、LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的参数、作为 LeonAnimationActionCoordinator.RequestQTE(int) 的参数、作为 LeonQTECoordinator.Cancel(int, QTECancelReason) 的参数 等共 4 处。</param>
    private IEnumerator AlignAndEnter(int sessionId)
    {
        ChangePhase(QTEPhase.Aligning);
        // enemyRoot（敌人根节点）：使用位置：作为 LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion) 的参数、LeonQTECoordinator.AlignAndEnter(int)。
Transform enemyRoot = participant.Root;
        // enemyStartPosition（敌人起始位置）：使用位置：作为 LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion) 的参数、LeonQTECoordinator.AlignAndEnter(int)。
Vector3 enemyStartPosition = enemyRoot.position;
        // enemyStartRotation（敌人起始旋转）：使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。
Quaternion enemyStartRotation = enemyRoot.rotation;
        // playerStartPosition（玩家起始位置）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 playerStartPosition = transform.position;
        // playerStartRotation（玩家起始旋转）：使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。
Quaternion playerStartRotation = transform.rotation;
        // enemyToPlayer（从敌人指向玩家的方向向量）：使用位置：LeonQTECoordinator.AlignAndEnter(int)。
Vector3 enemyToPlayer = transform.position - enemyRoot.position;
        enemyToPlayer.y = 0f;
        if (enemyToPlayer.sqrMagnitude <= 0.0001f)
        {
            Cancel(sessionId, QTECancelReason.AlignmentFailed);
            yield break;
        }

        enemyToPlayer.Normalize();
        // sameFacing（是否相同朝向）：使用位置：LeonQTECoordinator.AlignAndEnter(int)。
bool sameFacing = activeKind == QTEKind.RearBiteNeck || activeKind == QTEKind.KnifeAssassination;
        // facing（朝向）：使用位置：LeonQTECoordinator.AlignAndEnter(int)。
Vector3 facing;
        if (sameFacing)
        {
            // selectedValue7（分支选出的从敌人指向玩家的方向向量）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonQTECoordinator.AlignAndEnter(int)。
global::UnityEngine.Vector3 selectedValue7;
            if (activeKind == QTEKind.KnifeAssassination)
            {
                selectedValue7 = -enemyToPlayer;
            }
            else
            {
                selectedValue7 = enemyToPlayer;
            }

            facing = (selectedValue7);
        }
        else
        {
            facing = enemyToPlayer;
        }

        // selectedValue9（分支选出的朝向）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Quaternion.LookRotation(Vector3, Vector3) 的参数、LeonQTECoordinator.AlignAndEnter(int)。
global::UnityEngine.Vector3 selectedValue9;
        if (activeKind == QTEKind.KnifeAssassination)
        {
            selectedValue9 = facing;
        }
        else
        {
            selectedValue9 = enemyToPlayer;
        }

        // enemyTargetRotation（敌人目标旋转）：保存 Quaternion.LookRotation(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion) 的参数、作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。
Quaternion enemyTargetRotation = Quaternion.LookRotation(selectedValue9, Vector3.up);
        // selectedValue10（分支选出的朝向）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Quaternion.LookRotation(Vector3, Vector3) 的参数、LeonQTECoordinator.AlignAndEnter(int)。
global::UnityEngine.Vector3 selectedValue10;
        if (sameFacing)
        {
            selectedValue10 = facing;
        }
        else
        {
            selectedValue10 = -enemyToPlayer;
        }

        // playerTargetRotation（玩家目标旋转）：保存 Quaternion.LookRotation(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.SetPlayerWorldPose(Vector3, Quaternion) 的参数、作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数。
Quaternion playerTargetRotation = Quaternion.LookRotation(selectedValue10, Vector3.up);
        // pairingDistance（配对中距离）：使用位置：LeonQTECoordinator.AlignAndEnter(int)。
float pairingDistance;
        if (activeVariant != null && activeVariant.pairingDistance > 0f)
        {
            pairingDistance = activeVariant.pairingDistance;
        }
        else
        {
            pairingDistance = config.pairingDistance;
        }

        // playerTargetPosition（玩家目标位置）：使用位置：作为 LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.SetPlayerWorldPose(Vector3, Quaternion) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数 等共 4 处。
Vector3 playerTargetPosition;
        if (activeKind == QTEKind.KnifeAssassination)
        {
            playerTargetPosition = enemyStartPosition - facing * pairingDistance;
        }
        else
        {
            playerTargetPosition = enemyStartPosition + enemyToPlayer * pairingDistance;
        }

        playerTargetPosition.y = enemyStartPosition.y;
        if (IsPairingSpaceBlocked(playerTargetPosition, participant))
        {
            Cancel(sessionId, QTECancelReason.SpaceBlocked);
            yield break;
        }

        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.AlignAndEnter(int)。
float elapsed = 0f;
        while (elapsed < config.alignmentDuration)
        {
            if (sessionId != activeSessionId)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            // t（配对双方对齐阶段的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.AlignAndEnter(int)。
float t = Mathf.Clamp01(elapsed / config.alignmentDuration);
            // smooth（平滑）：使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
float smooth = t * t * (3f - 2f * t);
            SetPlayerWorldPose(Vector3.Lerp(playerStartPosition, playerTargetPosition, smooth), Quaternion.Slerp(playerStartRotation, playerTargetRotation, smooth));
            SetParticipantWorldPose(enemyRoot, enemyStartPosition, Quaternion.Slerp(enemyStartRotation, enemyTargetRotation, smooth));
            yield return null;
        }

        SetPlayerWorldPose(playerTargetPosition, playerTargetRotation);
        SetParticipantWorldPose(enemyRoot, enemyStartPosition, enemyTargetRotation);
        if (sessionId != activeSessionId)
        {
            yield break;
        }

        if (!animationCoordinator.RequestQTE(sessionId) || !participant.BeginAction(sessionId, EnemyQTEAction.Enter))
        {
            Cancel(sessionId, QTECancelReason.AnimationInterrupted);
            yield break;
        }

        activeRoutine = null;
        ResetPairFlags();
        expectedPlayerAction = FullBodyActionType.QTEEnter;
        expectedEnemyAction = EnemyQTEAction.Enter;
        ChangePhase(QTEPhase.Entering);
    }

    /// <summary>HandlePlayerActionCompleted 的业务处理入口；本方法直接依赖 LeonQTECoordinator.MatchesCurrentPlayerAction(FullBodyActionType)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.MatchesCurrentPlayerAction(FullBodyActionType)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：作为 LeonQTECoordinator.MatchesCurrentPlayerAction(FullBodyActionType) 的参数、LeonQTECoordinator.HandlePlayerActionCompleted(AnimationActionTransaction)。</param>
    private void HandlePlayerActionCompleted(AnimationActionTransaction transaction)
    {
        if (!IsActive || transaction.qteSessionId != activeSessionId)
        {
            return;
        }

        if (!MatchesCurrentPlayerAction(transaction.fullBodyActionType))
        {
            return;
        }

        playerActionCompleted = true;
        TryAdvanceAfterPairCompletion();
    }

    /// <summary>HandleParticipantActionCompleted 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.MatchesCurrentEnemyAction(EnemyQTEAction)、LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.TryAdvanceAfterPairCompletion()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.MatchesCurrentEnemyAction(EnemyQTEAction)、LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.TryAdvanceAfterPairCompletion()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonQTECoordinator.CommitAssassination(int) 的参数、LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int)。</param>
    /// <param name="action">中文含义：动作。使用位置：作为 LeonQTECoordinator.MatchesCurrentEnemyAction(EnemyQTEAction) 的参数、LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int)。</param>
    /// <param name="sequence">中文含义：序号。</param>
    private void HandleParticipantActionCompleted(int sessionId, EnemyQTEAction action, int sequence)
    {
        if (sessionId != activeSessionId || !MatchesCurrentEnemyAction(action))
        {
            return;
        }

        enemyActionCompleted = true;
        if (activeKind == QTEKind.KnifeAssassination && action == EnemyQTEAction.Success)
        {
            CommitAssassination(sessionId);
        }

        if (pendingResult == QTEResult.Success)
        {
            if (playerReleased && !enemyEndRequested)
            {
                RequestEnemyEnd();
            }

            return;
        }

        TryAdvanceAfterPairCompletion();
    }

    /// <summary>TryAdvanceAfterPairCompletion 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、IEnemyQTEParticipant.EndAction(int)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.CompleteQTE(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult() 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int)、LeonQTECoordinator.HandlePlayerActionCompleted(AnimationActionTransaction)；下游（含内部回调体） Debug.LogWarning(object)、IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、IEnemyQTEParticipant.EndAction(int)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.CompleteQTE(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryAdvanceAfterPairCompletion()
    {
        if (!playerActionCompleted || !enemyActionCompleted || cleanupRunning)
        {
            return;
        }

        if (pendingResult == QTEResult.Success)
        {
            if (IsSingleShotAssassination())
            {
                TryFinishSingleShotAssassination();
            }

            return;
        }

        // sessionId（会话编号）：使用位置：作为 Debug.LogWarning(object) 的参数、作为 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的参数、作为 IEnemyQTEParticipant.EndAction(int) 的参数 等共 9 处。
int sessionId = activeSessionId;
        if (phase == QTEPhase.Entering && expectedPlayerAction == FullBodyActionType.QTEEnter)
        {
            if (IsSingleShotAssassination())
            {
                BeginSingleShotAssassinationResult();
                return;
            }

            ResetPairFlags();
            if (activeVariant != null && activeVariant.singleShot)
            {
                BeginResult(!activeVariant.automaticFailure);
                return;
            }

            // playerAdvanced（是否玩家推进后的）：保存 LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryAdvanceAfterPairCompletion()。
bool playerAdvanced = animationCoordinator.AdvanceQTEPhase(sessionId, FullBodyActionType.QTEHold);
            // enemyAdvanced（是否敌人推进后的）：保存 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryAdvanceAfterPairCompletion()。
bool enemyAdvanced = participant.BeginAction(sessionId, EnemyQTEAction.Hold);
            if (!playerAdvanced || !enemyAdvanced)
            {
                Cancel(sessionId, QTECancelReason.AnimationInterrupted);
                return;
            }

            expectedPlayerAction = FullBodyActionType.QTEHold;
            expectedEnemyAction = EnemyQTEAction.Hold;
            return;
        }

        if (phase == QTEPhase.Entering && expectedPlayerAction == FullBodyActionType.QTEHold)
        {
            ResetPairFlags();
            ChangePhase(QTEPhase.WaitingForInput);
            if (activeVariant != null && (activeVariant.automaticSuccess || activeVariant.automaticFailure))
            {
                BeginResult(!activeVariant.automaticFailure);
            }

            return;
        }

        if (phase == QTEPhase.SuccessResult || phase == QTEPhase.FailureResult)
        {
            FinishResultMotion();
            if (phase == QTEPhase.FailureResult && !damageCommitted)
            {
                Debug.LogWarning("[里昂快速反应事件] 咬颈动画未发送接触标记，使用结果完成兜底提交一次伤害。");
                CommitBiteDamage(sessionId);
                if (sessionId != activeSessionId)
                {
                    return;
                }
            }

            ResetPairFlags();
            ChangePhase(QTEPhase.Exiting);
            // playerEnding（是否玩家结束中）：保存 LeonAnimationActionCoordinator.CompleteQTE(int) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryAdvanceAfterPairCompletion()。
bool playerEnding = animationCoordinator.CompleteQTE(sessionId);
            // enemyEnding（是否敌人结束中）：保存 IEnemyQTEParticipant.EndAction(int) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryAdvanceAfterPairCompletion()。
bool enemyEnding = participant.EndAction(sessionId);
            if (!playerEnding)
            {
                Debug.LogWarning($"[里昂快速反应事件] 会话 {sessionId} 玩家失败退出请求未被动画事务接受，立即执行玩家释放兜底。");
                playerExited = true;
                ReleasePlayer(sessionId);
            }

            if (!enemyEnding)
            {
                Debug.LogWarning($"[里昂快速反应事件] 会话 {sessionId} 敌人失败退出请求未被动画事务接受，交由统一释放兜底收尾。");
                enemyExited = true;
            }

            TryFinishAfterPairExit();
        }
    }

    /// <summary>HandlePlayerActionExited 的业务处理入口；本方法直接依赖 LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.TryFinishAfterPairExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.TryFinishAfterPairExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)。</param>
    private void HandlePlayerActionExited(AnimationActionTransaction transaction)
    {
        if (transaction.qteSessionId != activeSessionId)
        {
            return;
        }

        if (pendingResult == QTEResult.Success && plannedRecoveryExit)
        {
            if (!playerReleased)
            {
                ReleasePlayer(activeSessionId);
            }

            if (IsCommittedAssassinationSuccess())
            {
                HandleEnemySuccessExit(activeSessionId);
                return;
            }

            if (enemyActionCompleted && !enemyEndRequested)
            {
                RequestEnemyEnd();
            }

            return;
        }

        if (phase != QTEPhase.Exiting)
        {
            return;
        }

        playerExited = true;
        TryFinishAfterPairExit();
    }

    /// <summary>IsCommittedAssassinationSuccess 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.HandlePlayerExitTimeout()、LeonQTECoordinator.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsCommittedAssassinationSuccess()
    {
        return (activeKind == QTEKind.KnifeAssassination || activeKind == QTEKind.GroundFinisher) && pendingResult == QTEResult.Success && assassinationCommitted;
    }

    /// <summary>HandleParticipantActionExited 的业务处理入口；本方法直接依赖 LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.TryFinishAfterPairExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.TryFinishAfterPairExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonQTECoordinator.HandleEnemySuccessExit(int) 的参数、LeonQTECoordinator.HandleParticipantActionExited(int, EnemyQTEAction, int, bool)。</param>
    /// <param name="action">中文含义：动作。使用位置：LeonQTECoordinator.HandleParticipantActionExited(int, EnemyQTEAction, int, bool)。</param>
    /// <param name="sequence">中文含义：序号。</param>
    /// <param name="wasCompleted">中文含义：之前是否已完成。</param>
    private void HandleParticipantActionExited(int sessionId, EnemyQTEAction action, int sequence, bool wasCompleted)
    {
        if (sessionId != activeSessionId)
        {
            return;
        }

        if (action != EnemyQTEAction.Success && action != EnemyQTEAction.Failure)
        {
            return;
        }

        if (pendingResult == QTEResult.Success)
        {
            if (activeKind == QTEKind.KnifeAssassination && !playerReleased)
            {
                return;
            }

            HandleEnemySuccessExit(sessionId);
            return;
        }

        if (phase != QTEPhase.Exiting)
        {
            return;
        }

        enemyExited = true;
        TryFinishAfterPairExit();
    }

    /// <summary>TryFinishAfterPairExit 的业务处理入口；本方法直接依赖 LeonQTECoordinator.SeparateAndFinish(int)、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.HandleParticipantActionExited(int, EnemyQTEAction, int, bool)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonQTECoordinator.SeparateAndFinish(int)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryFinishAfterPairExit()
    {
        if (!playerExited || !enemyExited || cleanupRunning)
        {
            return;
        }

        cleanupRunning = true;
        groundFinisherPlans.Clear();
        activeRoutine = StartCoroutine(SeparateAndFinish(activeSessionId));
    }

    /// <summary>SeparateAndFinish 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)、Mathf.Clamp01(float)、Vector3.Lerp(Vector3, Vector3, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryFinishAfterPairExit()；下游（含内部回调体） LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)、Mathf.Clamp01(float)、Vector3.Lerp(Vector3, Vector3, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonQTECoordinator.SeparateAndFinish(int)。</param>
    private IEnumerator SeparateAndFinish(int sessionId)
    {
        // enemyRoot（敌人根节点）：使用位置：作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。
Transform enemyRoot = participant.Root;
        // direction（方向）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。
Vector3 direction = transform.position - enemyRoot.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = -enemyRoot.forward;
        }

        direction.Normalize();
        // playerStart（玩家起始）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。
Vector3 playerStart = transform.position;
        // enemyStart（敌人起始）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。
Vector3 enemyStart = enemyRoot.position;
        // playerDistance（玩家距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.SeparateAndFinish(int)。
float playerDistance = GetSafeSeparationDistance(playerStart, direction, config.playerSeparationDistance, participant);
        // enemyDistance（敌人距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.SeparateAndFinish(int)。
float enemyDistance = GetSafeSeparationDistance(enemyStart, -direction, config.enemySeparationDistance, participant);
        // playerTarget（玩家目标）：使用位置：作为 LeonQTECoordinator.SetPlayerWorldPosition(Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 playerTarget = playerStart + direction * playerDistance;
        // enemyTarget（敌人目标）：使用位置：作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 enemyTarget = enemyStart - direction * enemyDistance;
        // elapsed（已用）：使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。
float elapsed = 0f;
        while (elapsed < config.separationDuration)
        {
            if (sessionId != activeSessionId)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            // t（配对结束后双方分离阶段的归一化进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.SeparateAndFinish(int)。
float t = Mathf.Clamp01(elapsed / config.separationDuration);
            // smooth（平滑）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
float smooth = t * t * (3f - 2f * t);
            SetPlayerWorldPosition(Vector3.Lerp(playerStart, playerTarget, smooth));
            SetParticipantWorldPosition(enemyRoot, Vector3.Lerp(enemyStart, enemyTarget, smooth));
            yield return null;
        }

        SetPlayerWorldPosition(playerTarget);
        SetParticipantWorldPosition(enemyRoot, enemyTarget);
        activeRoutine = null;
        // result（当前配对动作结果）：使用位置：作为 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason) 的参数。
QTEResult result = pendingResult;
        ChangePhase(QTEPhase.Completed);
        FinishCleanup(result, QTECancelReason.None);
    }

    /// <summary>DecideResult 的业务处理入口；本方法直接依赖 LeonQTECoordinator.BeginResult(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.SubmitEscapePress(int)、LeonQTECoordinator.Update()；下游（含内部回调体） LeonQTECoordinator.BeginResult(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">中文含义：是否成功。使用位置：作为 LeonQTECoordinator.BeginResult(bool) 的参数。</param>
    private void DecideResult(bool success)
    {
        if (phase != QTEPhase.WaitingForInput || cleanupRunning)
        {
            return;
        }

        BeginResult(success);
    }

    /// <summary>BeginResult 的业务处理入口；本方法直接依赖 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonQTECoordinator.BeginResultMotion(bool)、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.ResetPairFlags() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.DecideResult(bool)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonQTECoordinator.BeginResultMotion(bool)、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.ResetPairFlags() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">中文含义：是否成功。使用位置：LeonQTECoordinator.BeginResult(bool)。</param>
    private void BeginResult(bool success)
    {
        if (cleanupRunning || participant == null || (phase != QTEPhase.WaitingForInput && phase != QTEPhase.Entering))
        {
            return;
        }

        if (success)
        {
            pendingResult = QTEResult.Success;
        }
        else
        {
            pendingResult = QTEResult.Failure;
        }

        StopQTEStruggleLoop();
        ResetPairFlags();
        if (success)
        {
            ChangePhase(QTEPhase.SuccessResult);
            BeginResultMotion(true);
            expectedPlayerAction = FullBodyActionType.QTESuccess;
            expectedEnemyAction = EnemyQTEAction.Success;
            // playerAdvanced（是否玩家推进后的）：保存 LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResult(bool)。
bool playerAdvanced = animationCoordinator.AdvanceQTEPhase(activeSessionId, FullBodyActionType.QTESuccess);
            // enemyAdvanced（是否敌人推进后的）：保存 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResult(bool)。
bool enemyAdvanced = participant.BeginAction(activeSessionId, EnemyQTEAction.Success);
            if (!playerAdvanced || !enemyAdvanced)
            {
                Cancel(activeSessionId, QTECancelReason.AnimationInterrupted);
            }

            if (activeKind == QTEKind.KnifeAssassination && enemyActionCompleted && !assassinationCommitted)
            {
                CommitAssassination(activeSessionId);
            }
        }
        else
        {
            ChangePhase(QTEPhase.FailureResult);
            BeginResultMotion(false);
            expectedPlayerAction = FullBodyActionType.QTEFail;
            expectedEnemyAction = EnemyQTEAction.Failure;
            // playerAdvanced（是否玩家推进后的）：保存 LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResult(bool)。
bool playerAdvanced = animationCoordinator.AdvanceQTEPhase(activeSessionId, FullBodyActionType.QTEFail);
            // enemyAdvanced（是否敌人推进后的）：保存 IEnemyQTEParticipant.BeginAction(int, EnemyQTEAction) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResult(bool)。
bool enemyAdvanced = participant.BeginAction(activeSessionId, EnemyQTEAction.Failure);
            if (!playerAdvanced || !enemyAdvanced)
            {
                Cancel(activeSessionId, QTECancelReason.AnimationInterrupted);
            }
        }
    }

    /// <summary>BeginResultMotion 的业务处理入口；本方法直接依赖 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、Mathf.Min(float, float)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginResult(bool)；下游（含内部回调体） LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、Mathf.Min(float, float)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">中文含义：是否成功。使用位置：LeonQTECoordinator.BeginResultMotion(bool)。</param>
    private void BeginResultMotion(bool success)
    {
        if (participant == null || participant.Root == null || config == null)
        {
            resultMotionActive = false;
            return;
        }

        // enemyRoot（敌人根节点）：使用位置：LeonQTECoordinator.BeginResultMotion(bool)。
Transform enemyRoot = participant.Root;
        // awayFromEnemy（远离来源敌人）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、LeonQTECoordinator.BeginResultMotion(bool)。
Vector3 awayFromEnemy = transform.position - enemyRoot.position;
        awayFromEnemy.y = 0f;
        if (awayFromEnemy.sqrMagnitude <= 0.0001f)
        {
            awayFromEnemy = -enemyRoot.forward;
        }

        awayFromEnemy.Normalize();
        resultPlayerStart = transform.position;
        resultEnemyStart = enemyRoot.position;
        if (success)
        {
            // playerDistance（玩家距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResultMotion(bool)。
float playerDistance = GetSafeSeparationDistance(resultPlayerStart, awayFromEnemy, config.successPlayerMotionDistance, participant);
            // enemyDistance（敌人距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResultMotion(bool)。
float enemyDistance = GetSafeSeparationDistance(resultEnemyStart, -awayFromEnemy, config.successEnemyMotionDistance, participant);
            resultPlayerTarget = resultPlayerStart + awayFromEnemy * playerDistance;
            resultEnemyTarget = resultEnemyStart - awayFromEnemy * enemyDistance;
        }
        else
        {
            // playerDistance（玩家距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数。
float playerDistance = GetSafeSeparationDistance(resultPlayerStart, awayFromEnemy, config.failurePairMotionDistance, participant);
            // enemyDistance（敌人距离）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数。
float enemyDistance = GetSafeSeparationDistance(resultEnemyStart, awayFromEnemy, config.failurePairMotionDistance, participant);
            // pairDistance（配对距离）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.BeginResultMotion(bool)。
float pairDistance = Mathf.Min(playerDistance, enemyDistance);
            resultPlayerTarget = resultPlayerStart + awayFromEnemy * pairDistance;
            resultEnemyTarget = resultEnemyStart + awayFromEnemy * pairDistance;
        }

        resultMotionActive = true;
    }

    /// <summary>ApplyResultMotion 的业务处理入口；本方法直接依赖 AnimationCurve.Evaluate(float)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetLayerIndex(string)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） AnimationCurve.Evaluate(float)、Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetLayerIndex(string)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyResultMotion()
    {
        if (!resultMotionActive || participant == null || participant.Root == null || playerAnimator == null || config == null)
        {
            return;
        }

        // isSuccess（是否成功）：使用位置：LeonQTECoordinator.ApplyResultMotion()。
bool isSuccess = pendingResult == QTEResult.Success;
        // expectedHash（预期哈希编号）：使用位置：LeonQTECoordinator.ApplyResultMotion()。
int expectedHash;
        if (isSuccess)
        {
            expectedHash = HashQTESuccessState;
        }
        else
        {
            expectedHash = HashQTEFailState;
        }

        // playerStateInfo（玩家状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.ApplyResultMotion()。
AnimatorStateInfo playerStateInfo = playerAnimator.GetCurrentAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        if (playerStateInfo.shortNameHash != expectedHash && playerAnimator.IsInTransition(AnimatorLayerID.FullBodyAction))
        {
            playerStateInfo = playerAnimator.GetNextAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        }

        if (playerStateInfo.shortNameHash != expectedHash)
        {
            return;
        }

        if (!playerReleased)
        {
            // playerProgress（玩家进度）：使用位置：作为 AnimationCurve.Evaluate(float) 的参数、LeonQTECoordinator.ApplyResultMotion()。
float playerProgress;
            if (isSuccess)
            {
                playerProgress = Mathf.Clamp01(playerStateInfo.normalizedTime / ResolveSuccessPlayerReleaseNormalizedTime());
            }
            else
            {
                playerProgress = Mathf.Clamp01(playerStateInfo.normalizedTime);
            }

            // playerCurved（玩家弯曲）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ApplyResultMotion()。
float playerCurved;
            if (config.resultMotionCurve != null)
            {
                playerCurved = Mathf.Clamp01(config.resultMotionCurve.Evaluate(playerProgress));
            }
            else
            {
                playerCurved = playerProgress * playerProgress * (3f - 2f * playerProgress);
            }

            SetPlayerWorldPosition(Vector3.Lerp(resultPlayerStart, resultPlayerTarget, playerCurved));
        }

        // enemyAnimator（敌人动画控制器）：使用位置：LeonQTECoordinator.ApplyResultMotion()。
Animator enemyAnimator = participant.Animator;
        if (enemyAnimator != null)
        {
            // enemyQteLayer（敌人配对动作图层）：保存 Animator.GetLayerIndex(string) 的返回结果，供当前步骤使用。使用位置：作为 Animator.GetCurrentAnimatorStateInfo(int) 的参数、LeonQTECoordinator.ApplyResultMotion()。
int enemyQteLayer = enemyAnimator.GetLayerIndex("QTE Override");
            if (enemyQteLayer >= 0)
            {
                // enemyStateInfo（敌人状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数。
AnimatorStateInfo enemyStateInfo = enemyAnimator.GetCurrentAnimatorStateInfo(enemyQteLayer);
                // enemyProgress（敌人进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 AnimationCurve.Evaluate(float) 的参数、LeonQTECoordinator.ApplyResultMotion()。
float enemyProgress = Mathf.Clamp01(enemyStateInfo.normalizedTime);
                // enemyCurved（敌人弯曲）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ApplyResultMotion()。
float enemyCurved;
                if (config.resultMotionCurve != null)
                {
                    enemyCurved = Mathf.Clamp01(config.resultMotionCurve.Evaluate(enemyProgress));
                }
                else
                {
                    enemyCurved = enemyProgress * enemyProgress * (3f - 2f * enemyProgress);
                }

                SetParticipantWorldPosition(participant.Root, Vector3.Lerp(resultEnemyStart, resultEnemyTarget, enemyCurved));
            }
        }
    }

    /// <summary>FinishResultMotion 的业务处理入口；本方法直接依赖 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()、LeonQTECoordinator.TryFinishSingleShotAssassination()；下游（含内部回调体） LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FinishResultMotion()
    {
        if (!resultMotionActive)
        {
            return;
        }

        if (!playerReleased)
        {
            SetPlayerWorldPosition(resultPlayerTarget);
        }

        if (participant != null && participant.Root != null)
        {
            SetParticipantWorldPosition(participant.Root, resultEnemyTarget);
        }

        resultMotionActive = false;
    }

    /// <summary>SetPlayerWorldPosition 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.RequestPlayerRecoveryExit()、LeonQTECoordinator.SeparateAndFinish(int)；下游（含内部回调体） LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool) 的参数。</param>
    private void SetPlayerWorldPosition(Vector3 position)
    {
        // receiverValue17（判空前缓存的玩家运动）：从玩家运动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)。
global::LeonActionMotionDriver receiverValue17 = playerMotion;
        // optionalValue18（条件访问得到的提交已配对动作姿态的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.SetPlayerWorldPosition(Vector3)。
bool? optionalValue18;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            optionalValue18 = receiverValue17.CommitPairedActionPose(activeSessionId, position, transform.rotation, false);
        }
        else
        {
            optionalValue18 = default;
        }
    }

    /// <summary>SetParticipantWorldPosition 的业务处理入口；本方法直接依赖 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.SeparateAndFinish(int)；下游（含内部回调体） IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="participantRoot">中文含义：参与者根节点。使用位置：作为 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion) 的参数、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion) 的参数。</param>
    private void SetParticipantWorldPosition(Transform participantRoot, Vector3 position)
    {
        if (participant == null || participant.Root != participantRoot)
        {
            return;
        }

        participant.CommitPairedActionPose(activeSessionId, position, participantRoot.rotation);
    }

    /// <summary>SetPlayerWorldPose 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)；下游（含内部回调体） LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool) 的参数。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：作为 LeonActionMotionDriver.CommitPairedActionPose(int, Vector3, Quaternion, bool) 的参数。</param>
    private void SetPlayerWorldPose(Vector3 position, Quaternion rotation)
    {
        // receiverValue19（判空前缓存的玩家运动）：从玩家运动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.SetPlayerWorldPose(Vector3, Quaternion)。
global::LeonActionMotionDriver receiverValue19 = playerMotion;
        // optionalValue20（条件访问得到的提交已配对动作姿态的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.SetPlayerWorldPose(Vector3, Quaternion)。
bool? optionalValue20;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            optionalValue20 = receiverValue19.CommitPairedActionPose(activeSessionId, position, rotation, true);
        }
        else
        {
            optionalValue20 = default;
        }
    }

    /// <summary>SetParticipantWorldPose 的业务处理入口；本方法直接依赖 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)；下游（含内部回调体） IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="participantRoot">中文含义：参与者根节点。使用位置：LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion) 的参数。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：作为 IEnemyQTEParticipant.CommitPairedActionPose(int, Vector3, Quaternion) 的参数。</param>
    private void SetParticipantWorldPose(Transform participantRoot, Vector3 position, Quaternion rotation)
    {
        if (participant == null || participant.Root != participantRoot)
        {
            return;
        }

        participant.CommitPairedActionPose(activeSessionId, position, rotation);
    }

    /// <summary>HandleBiteContact 的业务处理入口；本方法直接依赖 Action&lt;int&gt;.Invoke(int)、LeonQTECoordinator.CommitBiteDamage(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、LeonQTECoordinator.CommitBiteDamage(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 Action&lt;int&gt;.Invoke(int) 的参数、作为 LeonQTECoordinator.CommitBiteDamage(int) 的参数、LeonQTECoordinator.HandleBiteContact(int)。</param>
    private void HandleBiteContact(int sessionId)
    {
        if (sessionId != activeSessionId || phase != QTEPhase.FailureResult)
        {
            return;
        }

        // receiverValue21（判空前缓存的配对动作撕咬接触时的通知回调）：从配对动作撕咬接触时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.HandleBiteContact(int)。
global::System.Action<int> receiverValue21 = OnQTEBiteContact;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.Invoke(sessionId);
        }

        CommitBiteDamage(sessionId);
    }

    /// <summary>HandleAssassinationContact 的业务处理入口；本方法直接依赖 Action&lt;int&gt;.Invoke(int)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.StopQTEStruggleLoop()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.StopQTEStruggleLoop()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 Action&lt;int&gt;.Invoke(int) 的参数、作为 LeonQTECoordinator.CommitAssassination(int) 的参数、LeonQTECoordinator.HandleAssassinationContact(int)。</param>
    private void HandleAssassinationContact(int sessionId)
    {
        if (sessionId != activeSessionId || activeKind != QTEKind.KnifeAssassination)
        {
            return;
        }

        // receiverValue22（判空前缓存的配对动作暗杀接触时的通知回调）：从配对动作暗杀接触时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.HandleAssassinationContact(int)。
global::System.Action<int> receiverValue22 = OnQTEAssassinationContact;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.Invoke(sessionId);
        }

        if (IsSingleShotAssassination() && pendingResult == QTEResult.None && phase == QTEPhase.Entering)
        {
            pendingResult = QTEResult.Success;
            StopQTEStruggleLoop();
            ChangePhase(QTEPhase.SuccessResult);
        }

        if (pendingResult != QTEResult.Success)
        {
            return;
        }

        CommitAssassination(sessionId);
    }

    /// <summary>CommitBiteDamage 的业务处理入口；本方法直接依赖 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)、LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3)、LeonQTECoordinator.ResolveFailureBiteHitPoint()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandleBiteContact(int)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)、LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3)、LeonQTECoordinator.ResolveFailureBiteHitPoint()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3) 的参数。</param>
    private void CommitBiteDamage(int sessionId)
    {
        if (activeKind == QTEKind.RearGrab)
        {
            return;
        }
        if (controller == null || config == null)
        {
            return;
        }

        // hitPoint（命中点位置）：保存 LeonQTECoordinator.ResolveFailureBiteHitPoint() 的返回结果，供当前步骤使用。使用位置：作为 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3) 的参数、作为 LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3) 的参数。
Vector3 hitPoint = ResolveFailureBiteHitPoint();
        // hitNormal（命中表面法线）：保存 LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3) 的参数。
Vector3 hitNormal = ResolveFailureBiteHitNormal(hitPoint);
        // damage（伤害）：使用位置：作为 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3) 的参数、LeonQTECoordinator.CommitBiteDamage(int)。
float damage;
        if (activeVariant != null && activeVariant.failureDamage > 0f)
        {
            damage = activeVariant.failureDamage;
        }
        else
        {
            damage = config.biteDamage;
        }

        if (activeKind == QTEKind.ChainsawExecution && nativePair != null)
        {
            // 电锯接触标记提交终局伤害，生命上限变化也不能跳过死亡。
            damage = Mathf.Max(damage, controller.currentHealth);
        }
        bool playBlood = nativePair == null || !nativePair.ContactVfxPlayed;
        controller.TryApplyQTEFailureDamage(sessionId, damage, hitPoint, hitNormal, playBlood);
    }

    /// <summary>ResolveFailureBiteHitPoint 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ResolveLegBiteHitPoint()、LeonQTECoordinator.ResolveNeckBiteHitPoint()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CommitBiteDamage(int)；下游（含内部回调体） LeonQTECoordinator.ResolveLegBiteHitPoint()、LeonQTECoordinator.ResolveNeckBiteHitPoint()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveFailureBiteHitPoint()
    {
        if (activeKind == QTEKind.LegBite)
        {
            return ResolveLegBiteHitPoint();
        }

        if (activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearBiteNeck || activeKind == QTEKind.ChainsawExecution)
        {
            return ResolveNeckBiteHitPoint();
        }

        return transform.position + Vector3.up * 1.2f;
    }

    /// <summary>ResolveLegBiteHitPoint 的业务处理入口；本方法直接依赖 LeonQTECoordinator.GetPlayerBone(HumanBodyBones)、LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveFailureBiteHitPoint()；下游（含内部回调体） LeonQTECoordinator.GetPlayerBone(HumanBodyBones)、LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveLegBiteHitPoint()
    {
        // leftLowerLeg（左侧小腿）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform leftLowerLeg = GetPlayerBone(HumanBodyBones.LeftLowerLeg);
        // rightLowerLeg（右侧小腿）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform rightLowerLeg = GetPlayerBone(HumanBodyBones.RightLowerLeg);
        // leftFoot（左脚）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform leftFoot = GetPlayerBone(HumanBodyBones.LeftFoot);
        // rightFoot（右脚）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform rightFoot = GetPlayerBone(HumanBodyBones.RightFoot);
        // enemyPosition（敌人位置）：使用位置：作为 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Vector3 enemyPosition;
        if (participant != null && participant.Root != null)
        {
            enemyPosition = participant.Root.position;
        }
        else
        {
            enemyPosition = transform.position + transform.forward;
        }

        // useLeft（是否使用左侧）：保存 LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ResolveLegBiteHitPoint()。
bool useLeft = IsLeftLegCloserToEnemy(enemyPosition, leftLowerLeg, rightLowerLeg, leftFoot, rightFoot);
        // lowerLeg（小腿）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform lowerLeg;
        if (useLeft)
        {
            lowerLeg = leftLowerLeg;
        }
        else
        {
            lowerLeg = rightLowerLeg;
        }

        // foot（脚部）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ResolveLegBiteHitPoint()。
Transform foot;
        if (useLeft)
        {
            foot = leftFoot;
        }
        else
        {
            foot = rightFoot;
        }

        if (lowerLeg != null && foot != null)
        {
            return Vector3.Lerp(lowerLeg.position, foot.position, 0.55f);
        }

        if (lowerLeg != null)
        {
            return lowerLeg.position;
        }

        if (foot != null)
        {
            return foot.position + Vector3.up * 0.12f;
        }

        // side（侧边）：使用位置：LeonQTECoordinator.ResolveLegBiteHitPoint()。
float side;
        if (useLeft)
        {
            side = -1f;
        }
        else
        {
            side = 1f;
        }

        return transform.position + transform.right * legBiteBloodFallbackSideOffset * side + Vector3.up * legBiteBloodFallbackHeight;
    }

    /// <summary>ResolveNeckBiteHitPoint 的业务处理入口；本方法直接依赖 LeonQTECoordinator.GetPlayerBone(HumanBodyBones)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveFailureBiteHitPoint()；下游（含内部回调体） LeonQTECoordinator.GetPlayerBone(HumanBodyBones)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveNeckBiteHitPoint()
    {
        // neck（颈部）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ResolveNeckBiteHitPoint()。
Transform neck = GetPlayerBone(HumanBodyBones.Neck);
        // head（头部）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ResolveNeckBiteHitPoint()。
Transform head = GetPlayerBone(HumanBodyBones.Head);
        // upperChest（上部胸部）：保存 LeonQTECoordinator.GetPlayerBone(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、LeonQTECoordinator.ResolveNeckBiteHitPoint()。
Transform upperChest = GetPlayerBone(HumanBodyBones.UpperChest);
        if (upperChest == null)
        {
            upperChest = GetPlayerBone(HumanBodyBones.Chest);
        }

        // point（点）：使用位置：LeonQTECoordinator.ResolveNeckBiteHitPoint()。
Vector3 point;
        if (neck != null)
        {
            point = neck.position;
        }
        else if (head != null && upperChest != null)
        {
            point = Vector3.Lerp(upperChest.position, head.position, 0.62f);
        }
        else if (head != null)
        {
            point = head.position + Vector3.down * 0.18f;
        }
        else if (upperChest != null)
        {
            point = upperChest.position + Vector3.up * 0.18f;
        }
        else
        {
            point = transform.position + Vector3.up * 1.45f;
        }

        if (participant != null && participant.Root != null)
        {
            // toEnemy（目标敌人）：使用位置：LeonQTECoordinator.ResolveNeckBiteHitPoint()。
Vector3 toEnemy = participant.Root.position - point;
            if (toEnemy.sqrMagnitude > 0.0001f)
            {
                point += toEnemy.normalized * neckBiteBloodForwardOffset;
            }
        }

        return point;
    }

    /// <summary>ResolveFailureBiteHitNormal 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CommitBiteDamage(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3)。</param>
    private Vector3 ResolveFailureBiteHitNormal(Vector3 hitPoint)
    {
        if (participant == null || participant.Root == null)
        {
            return transform.forward;
        }

        // normal（表面法线）：使用位置：LeonQTECoordinator.ResolveFailureBiteHitNormal(Vector3)。
Vector3 normal = hitPoint - participant.Root.position;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            return transform.forward;
        }

        return normal.normalized;
    }

    /// <summary>GetPlayerBone 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveLegBiteHitPoint()、LeonQTECoordinator.ResolveNeckBiteHitPoint()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数。</param>
    private Transform GetPlayerBone(HumanBodyBones bone)
    {
        return AnimatorBoneBindings.Resolve(playerAnimator, bone);
    }

    /// <summary>IsLeftLegCloserToEnemy 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveLegBiteHitPoint()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemyPosition">中文含义：敌人位置。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。</param>
    /// <param name="leftLowerLeg">中文含义：左侧小腿。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。</param>
    /// <param name="rightLowerLeg">中文含义：右侧小腿。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。</param>
    /// <param name="leftFoot">中文含义：左脚。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。</param>
    /// <param name="rightFoot">中文含义：右脚。使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。</param>
    private bool IsLeftLegCloserToEnemy(Vector3 enemyPosition, Transform leftLowerLeg, Transform rightLowerLeg, Transform leftFoot, Transform rightFoot)
    {
        // leftPoint（左侧点）：使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。
Vector3 leftPoint;
        if (leftLowerLeg != null)
        {
            leftPoint = leftLowerLeg.position;
        }
        else
        {
            if (leftFoot != null)
            {
                leftPoint = leftFoot.position;
            }
            else
            {
                leftPoint = transform.position - transform.right * legBiteBloodFallbackSideOffset;
            }
        }

        // rightPoint（右侧点）：使用位置：LeonQTECoordinator.IsLeftLegCloserToEnemy(Vector3, Transform, Transform, Transform, Transform)。
Vector3 rightPoint;
        if (rightLowerLeg != null)
        {
            rightPoint = rightLowerLeg.position;
        }
        else
        {
            if (rightFoot != null)
            {
                rightPoint = rightFoot.position;
            }
            else
            {
                rightPoint = transform.position + transform.right * legBiteBloodFallbackSideOffset;
            }
        }

        return (enemyPosition - leftPoint).sqrMagnitude <= (enemyPosition - rightPoint).sqrMagnitude;
    }

    private GridItem pairedKnife;

    /// <summary>CommitAssassination 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;VillagerController&gt;()、IEnemyQTEParticipant.CommitAssassinationResult(int, float)、LeonQTECoordinator.ConsumeAssassinationKnifeDurability()、LeonQTECoordinator.ResolveAssassinationDamage(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.CheckAssassinationCommitFallback()、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandleAssassinationContact(int) 等共 7 处；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、IEnemyQTEParticipant.CommitAssassinationResult(int, float)、LeonQTECoordinator.ConsumeAssassinationKnifeDurability()、LeonQTECoordinator.ResolveAssassinationDamage(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 IEnemyQTEParticipant.CommitAssassinationResult(int, float) 的参数、LeonQTECoordinator.CommitAssassination(int)。</param>
    private void CommitAssassination(int sessionId)
    {
        if (assassinationCommitted || sessionId != activeSessionId || participant == null || participant.Root == null)
        {
            return;
        }

        // villager（村民）：保存 Component.GetComponent<VillagerController>() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.ResolveAssassinationDamage(VillagerController) 的参数、LeonQTECoordinator.CommitAssassination(int)。
VillagerController villager = participant.Root.GetComponent<VillagerController>();
        if (villager == null || villager.IsDead)
        {
            return;
        }

        assassinationCommitted = true;
        assassinationCommittedTime = Time.time;
        enemyActionCompleted = true;
        if (!participant.CommitAssassinationResult(sessionId, ResolveAssassinationDamage(villager)))
        {
            return;
        }

        if (TryGetComponent<KnifeCombatModule>(out var knife))
        {
            knife.TryCommitFinisherCost(pairedKnife);
            if (nativePair != null && knife.TryReadBladeContact(out var bladeContact))
            {
                ChapterOneEffects.TryPlay(ChapterOneEffect.Assassination, bladeContact, -transform.forward);
                PlayKnifePairContactAudio(bladeContact);
            }
        }
    }

    /// <summary>HandlePlayerActionInterrupted 的业务处理入口；本方法直接依赖 LeonQTECoordinator.Cancel(int, QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.Cancel(int, QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonQTECoordinator.HandlePlayerActionInterrupted(AnimationActionTransaction)。</param>
    private void HandlePlayerActionInterrupted(AnimationActionTransaction transaction)
    {
        if (transaction.qteSessionId == activeSessionId && IsActive && phase != QTEPhase.Cancelled)
        {
            Cancel(activeSessionId, QTECancelReason.AnimationInterrupted);
        }
    }

    /// <summary>HandleParticipantInvalidated 的业务处理入口；本方法直接依赖 LeonQTECoordinator.Cancel(int, QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.Cancel(int, QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonQTECoordinator.Cancel(int, QTECancelReason) 的参数、LeonQTECoordinator.HandleParticipantInvalidated(int, QTECancelReason)。</param>
    /// <param name="reason">中文含义：原因。使用位置：作为 LeonQTECoordinator.Cancel(int, QTECancelReason) 的参数、LeonQTECoordinator.HandleParticipantInvalidated(int, QTECancelReason)。</param>
    private void HandleParticipantInvalidated(int sessionId, QTECancelReason reason)
    {
        if (sessionId == activeSessionId && (activeKind == QTEKind.KnifeAssassination || activeKind == QTEKind.GroundFinisher) && pendingResult == QTEResult.Success && reason == QTECancelReason.EnemyDied)
        {
            enemyActionCompleted = true;
            return;
        }

        if (sessionId == activeSessionId)
        {
            Cancel(sessionId, reason);
        }
    }

    /// <summary>HandlePlayerDeath 的业务处理入口；本方法直接依赖 LeonQTECoordinator.Cancel(int, QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.Cancel(int, QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandlePlayerDeath()
    {
        if (IsActive)
        {
            Cancel(activeSessionId, QTECancelReason.PlayerDied);
        }
    }

    /// <summary>HandleSceneUnloaded 的业务处理入口；本方法直接依赖 LeonQTECoordinator.Cancel(int, QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.Cancel(int, QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="scene">中文含义：场景。</param>
    private void HandleSceneUnloaded(Scene scene)
    {
        if (IsActive)
        {
            Cancel(activeSessionId, QTECancelReason.SceneUnloaded);
        }
    }

    /// <summary>CacheAndLockPlayer 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.RequestPairedActionMotion(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） LeonActionMotionDriver.RequestPairedActionMotion(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheAndLockPlayer()
    {
        playerLockCached = playerMotion != null && playerMotion.RequestPairedActionMotion(activeSessionId);
    }

    /// <summary>RestorePlayerLock 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.ReleasePairedActionMotion(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） LeonActionMotionDriver.ReleasePairedActionMotion(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestorePlayerLock()
    {
        if (!playerLockCached)
        {
            return;
        }

        // receiverValue33（判空前缓存的玩家运动）：从玩家运动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.RestorePlayerLock()。
global::LeonActionMotionDriver receiverValue33 = playerMotion;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.ReleasePairedActionMotion(activeSessionId);
        }

        playerLockCached = false;
    }

    /// <summary>IgnorePairCollisions 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CollisionPair.CollisionPair()、List&lt;LeonQTECoordinator.CollisionPair&gt;.Add(LeonQTECoordinator.CollisionPair)、List&lt;LeonQTECoordinator.CollisionPair&gt;.Clear()、Physics.GetIgnoreCollision(Collider, Collider)、Physics.IgnoreCollision(Collider, Collider, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） LeonQTECoordinator.CollisionPair.CollisionPair()、List&lt;LeonQTECoordinator.CollisionPair&gt;.Add(LeonQTECoordinator.CollisionPair)、List&lt;LeonQTECoordinator.CollisionPair&gt;.Clear()、Physics.GetIgnoreCollision(Collider, Collider)、Physics.IgnoreCollision(Collider, Collider, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant)。</param>
    private void IgnorePairCollisions(IEnemyQTEParticipant targetParticipant)
    {
        collisionPairs.Clear();
        if (playerColliders == null || targetParticipant.Colliders == null)
        {
            return;
        }

        // playerCollider（玩家碰撞体）：每轮从玩家碰撞体集合取得一个元素，仅代表当前这一项。使用位置：作为 List<LeonQTECoordinator.CollisionPair>.Add(LeonQTECoordinator.CollisionPair) 的参数、作为 Physics.GetIgnoreCollision(Collider, Collider) 的参数、作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数 等共 4 处。
foreach (var playerCollider in playerColliders)
        {
            if (playerCollider == null)
            {
                continue;
            }

            // enemyCollider（敌人碰撞体）：每轮从目标参与者的碰撞体集合取得一个元素，仅代表当前这一项。使用位置：作为 List<LeonQTECoordinator.CollisionPair>.Add(LeonQTECoordinator.CollisionPair) 的参数、作为 Physics.GetIgnoreCollision(Collider, Collider) 的参数、作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数 等共 4 处。
foreach (var enemyCollider in targetParticipant.Colliders)
            {
                if (enemyCollider == null || playerCollider == enemyCollider)
                {
                    continue;
                }

                // wasIgnored（之前是否已忽略）：保存 Physics.GetIgnoreCollision(Collider, Collider) 的返回结果，供当前步骤使用。使用位置：作为 List<LeonQTECoordinator.CollisionPair>.Add(LeonQTECoordinator.CollisionPair) 的参数、LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant)。
bool wasIgnored = Physics.GetIgnoreCollision(playerCollider, enemyCollider);
                collisionPairs.Add(new CollisionPair { first = playerCollider, second = enemyCollider, wasIgnored = wasIgnored });
                if (!wasIgnored)
                {
                    Physics.IgnoreCollision(playerCollider, enemyCollider, true);
                }
            }
        }
    }

    /// <summary>RestorePairCollisions 的业务处理入口；本方法直接依赖 List&lt;LeonQTECoordinator.CollisionPair&gt;.Clear()、Physics.IgnoreCollision(Collider, Collider, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)；下游（含内部回调体） List&lt;LeonQTECoordinator.CollisionPair&gt;.Clear()、Physics.IgnoreCollision(Collider, Collider, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestorePairCollisions()
    {
        // pair（当前碰撞配对）：每轮从碰撞配对集合取得一个元素，仅代表当前这一项。使用位置：作为 Physics.IgnoreCollision(Collider, Collider, bool) 的参数、LeonQTECoordinator.RestorePairCollisions()。
foreach (var pair in collisionPairs)
        {
            if (pair.first != null && pair.second != null && !pair.wasIgnored)
            {
                Physics.IgnoreCollision(pair.first, pair.second, false);
            }
        }

        collisionPairs.Clear();
    }

    /// <summary>ValidatePairing 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)；下游（含内部回调体） Mathf.Abs(float)、Vector3.Angle(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</param>
    /// <param name="kind">中文含义：种类。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</param>
    /// <param name="reason">中文含义：原因。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</param>
    private bool ValidatePairing(IEnemyQTEParticipant targetParticipant, QTEKind kind, out QTECancelReason reason)
    {
        // enemyToPlayer（从敌人指向玩家的方向向量）：使用位置：作为 Mathf.Abs(float) 的参数、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
Vector3 enemyToPlayer = transform.position - targetParticipant.Root.position;
        // height（高度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float height = Mathf.Abs(enemyToPlayer.y);
        enemyToPlayer.y = 0f;
        // distance（距离）：使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float distance = enemyToPlayer.magnitude;
        // maximumDistance（最大距离）：使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float maximumDistance;
        if (kind == QTEKind.GroundFinisher)
        {
            maximumDistance = config.groundFinisherDistance;
        }
        else if (kind == QTEKind.KnifeAssassination)
        {
            maximumDistance = config.assassinationStartDistance;
        }
        else
        {
            maximumDistance = config.maximumStartDistance;
        }

        if (distance <= 0.001f || distance > maximumDistance)
        {
            reason = QTECancelReason.DistanceInvalid;
            return false;
        }

        if (height > config.maximumHeightDifference)
        {
            reason = QTECancelReason.HeightInvalid;
            return false;
        }

        // direction（方向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数。
Vector3 direction = enemyToPlayer / distance;
        // enemyAngle（敌人角度）：保存 Vector3.Angle(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float enemyAngle = Vector3.Angle(targetParticipant.Root.forward, direction);
        // playerAngle（玩家角度）：保存 Vector3.Angle(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float playerAngle = Vector3.Angle(transform.forward, -direction);
        bool confirmedContact = targetParticipant is VillagerQTEParticipant contactTarget && contactTarget.HasConfirmedGrabContact(controller, kind);
        if (confirmedContact)
        {
            reason = QTECancelReason.None;
            return true;
        }
        if (kind == QTEKind.ChainsawExecution && chainsawContactTarget == targetParticipant)
        {
            // 仅真实刀链接触入口可绕过面向限制，距离与楼层检查仍已通过。
            reason = QTECancelReason.None;
            return true;
        }
        if (kind == QTEKind.RearBiteNeck)
        {
            playerAngle = Vector3.Angle(transform.forward, direction);
            if (enemyAngle > config.maximumEnemyFacingAngle || playerAngle > config.maximumPlayerFacingAngle)
            {
                reason = QTECancelReason.FacingInvalid;
                return false;
            }
        }
        else if (kind == QTEKind.KnifeAssassination)
        {
            // enemyBackAngle（敌人后方角度）：保存 Vector3.Angle(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。
float enemyBackAngle = Vector3.Angle(targetParticipant.Root.forward, -direction);
            if (enemyBackAngle > config.assassinationRearAngle || playerAngle > config.assassinationPlayerFacingAngle)
            {
                reason = QTECancelReason.FacingInvalid;
                return false;
            }
        }
        else if (kind == QTEKind.GroundFinisher)
        {
            if (!config.useNativeGroundFinisher || combat == null || combat.knifeModule == null || !combat.knifeModule.ValidKnife)
            {
                reason = QTECancelReason.PlayerUnavailable;
                return false;
            }
            if (playerAngle > Mathf.Max(config.maximumPlayerFacingAngle, 100f))
            {
                reason = QTECancelReason.FacingInvalid;
                return false;
            }
            if (targetParticipant is not VillagerQTEParticipant target || !CanAlignGroundFinisher(target))
            {
                reason = QTECancelReason.SpaceBlocked;
                return false;
            }
        }
        else if (enemyAngle > config.maximumEnemyFacingAngle || playerAngle > config.maximumPlayerFacingAngle)
        {
            reason = QTECancelReason.FacingInvalid;
            return false;
        }

        reason = QTECancelReason.None;
        return true;
    }

    /// <summary>IsPairingSpaceBlocked 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ContainsCollider(Collider[], Collider)、Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)；下游（含内部回调体） LeonQTECoordinator.ContainsCollider(Collider[], Collider)、Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerTargetPosition">中文含义：玩家目标位置。使用位置：LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant)。</param>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：作为 LeonQTECoordinator.ContainsCollider(Collider[], Collider) 的参数。</param>
    private Vector3 ResolvePairingClearanceBase(Vector3 position, IEnemyQTEParticipant targetParticipant)
    {
        // 配对根轨允许数厘米的上下起伏。净空胶囊按附近支撑面抬高，仍检查同一地形网格上的墙体。
        float maximumSupportHeight = position.y + .22f;
        Vector3 origin = position + Vector3.up * .45f;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, .7f, config.clearanceMask, QueryTriggerInteraction.Ignore))
        {
            if (ContainsCollider(playerColliders, hit.collider) || ContainsCollider(targetParticipant.Colliders, hit.collider)
                || hit.normal.y < .65f || hit.point.y > maximumSupportHeight)
            {
                continue;
            }
            position.y = Mathf.Max(position.y, hit.point.y);
        }
        return position;
    }

    private bool IsPairingSpaceBlocked(Vector3 playerTargetPosition, IEnemyQTEParticipant targetParticipant)
    {
        Vector3 clearanceBase = ResolvePairingClearanceBase(playerTargetPosition, targetParticipant);
        Vector3 bottom = clearanceBase + Vector3.up * (config.clearanceRadius + .08f);
        Vector3 top = clearanceBase + Vector3.up * 1.6f;
        Collider[] overlaps = Physics.OverlapCapsule(bottom, top, config.clearanceRadius, config.clearanceMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in overlaps)
        {
            if (ContainsCollider(playerColliders, hit) || ContainsCollider(targetParticipant.Colliders, hit))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private float GetSafeSeparationDistance(Vector3 origin, Vector3 direction, float desiredDistance, IEnemyQTEParticipant targetParticipant)
    {
        if (desiredDistance <= 0f)
        {
            return 0f;
        }

        // hits（命中结果）：保存 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)。
RaycastHit[] hits = Physics.SphereCastAll(origin + Vector3.up * 0.9f, config.clearanceRadius, direction, desiredDistance, config.clearanceMask, QueryTriggerInteraction.Ignore);
        // safeDistance（安全距离）：使用位置：作为 Mathf.Min(float, float) 的参数、LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)。
float safeDistance = desiredDistance;
        // hit（命中）：每轮从命中结果取得一个元素，仅代表当前这一项。使用位置：作为 LeonQTECoordinator.ContainsCollider(Collider[], Collider) 的参数、作为 Mathf.Max(float, float) 的参数。
foreach (var hit in hits)
        {
            if (ContainsCollider(playerColliders, hit.collider) || ContainsCollider(targetParticipant.Colliders, hit.collider))
            {
                continue;
            }

            safeDistance = Mathf.Min(safeDistance, Mathf.Max(0f, hit.distance - 0.05f));
        }

        return safeDistance;
    }

    /// <summary>ResolveSuccessPlayerReleaseNormalizedTime 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ApplyResultMotion()、LeonQTECoordinator.CheckPlayerRecoveryThreshold()、LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()、LeonQTECoordinator.RequestPlayerRecoveryExit()、LeonQTECoordinator.TryFinalizeCommittedAssassination()；下游（含内部回调体） Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveSuccessPlayerReleaseNormalizedTime()
    {
        if (config == null)
        {
            return 0.55f;
        }

        if (activeKind == QTEKind.KnifeAssassination)
        {
            return Mathf.Clamp(config.assassinationPlayerReleaseNormalizedTime, 0.5f, 1f);
        }

        return config.successPlayerRecoveryNormalizedTime;
    }

    /// <summary>ResolveAssassinationDamage 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CommitAssassination(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="villager">中文含义：村民。使用位置：LeonQTECoordinator.ResolveAssassinationDamage(VillagerController)。</param>
    private float ResolveAssassinationDamage(VillagerController villager)
    {
        if (config == null)
        {
            if (villager != null && villager.missionEnemyType == MissionEnemyType.Sawman)
            {
                return 85f;
            }
            else
            {
                return 9999f;
            }
        }

        if (villager != null && villager.missionEnemyType == MissionEnemyType.Sawman)
        {
            return config.sawmanAssassinationDamage;
        }

        return config.ganadoAssassinationDamage;
    }

    /// <summary>CheckPlayerRecoveryThreshold 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonQTECoordinator.RequestPlayerRecoveryExit()、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonQTECoordinator.RequestPlayerRecoveryExit()、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CheckPlayerRecoveryThreshold()
    {
        if (playerAnimator == null || config == null)
        {
            return;
        }

        // stateInfo（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.CheckPlayerRecoveryThreshold()。
AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        if (stateInfo.shortNameHash != HashQTESuccessState && playerAnimator.IsInTransition(AnimatorLayerID.FullBodyAction))
        {
            stateInfo = playerAnimator.GetNextAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        }

        if (stateInfo.shortNameHash != HashQTESuccessState)
        {
            return;
        }

        if (stateInfo.normalizedTime >= ResolveSuccessPlayerReleaseNormalizedTime())
        {
            RequestPlayerRecoveryExit();
        }
    }

    /// <summary>CheckAssassinationCommitFallback 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CommitAssassination(int)、Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） LeonQTECoordinator.CommitAssassination(int)、Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CheckAssassinationCommitFallback()
    {
        if (activeKind != QTEKind.KnifeAssassination || pendingResult != QTEResult.Success || assassinationCommitted || phase != QTEPhase.SuccessResult)
        {
            return;
        }

        // fallbackDelay（后备延迟）：使用位置：LeonQTECoordinator.CheckAssassinationCommitFallback()。
float fallbackDelay;
        if (config != null)
        {
            fallbackDelay = Mathf.Clamp(config.assassinationSuccessForceExitDelay * 0.5f, 0.08f, 0.2f);
        }
        else
        {
            fallbackDelay = 0.16f;
        }

        if (phaseElapsed < fallbackDelay)
        {
            return;
        }

        CommitAssassination(activeSessionId);
    }

    /// <summary>CheckResultPlayerReleaseFailsafe 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） Debug.LogWarning(object)、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CheckResultPlayerReleaseFailsafe()
    {
        if (playerReleased || cleanupRunning || (phase != QTEPhase.SuccessResult && phase != QTEPhase.FailureResult))
        {
            return;
        }

        // success（是否成功）：使用位置：作为 LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float) 的参数、LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
bool success = pendingResult == QTEResult.Success;
        // normalizedTime（动画归一化播放时间）：使用位置：作为 Debug.LogWarning(object) 的参数、作为 LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float) 的参数、LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
float normalizedTime;
        // hasResultState（是否具有结果状态）：保存 LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
bool hasResultState = TryGetPlayerResultNormalizedTime(success, out normalizedTime);
        // releaseThreshold（释放阈值）：使用位置：LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
float releaseThreshold;
        if (success)
        {
            releaseThreshold = ResolveSuccessPlayerReleaseNormalizedTime();
        }
        else
        {
            releaseThreshold = 0.82f;
        }

        // timeout（超时）：使用位置：LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
float timeout;
        if (success)
        {
            // selectedValue38（分支选出的最大的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()。
float selectedValue38;
            if (config != null)
            {
                selectedValue38 = Mathf.Max(1.6f, config.assassinationSuccessForceExitDelay + 1.2f);
            }
            else
            {
                selectedValue38 = 1.2f;
            }

            timeout = (selectedValue38);
        }
        else
        {
            timeout = 1.15f;
        }

        if (hasResultState && normalizedTime >= releaseThreshold)
        {
            ForceReleasePlayerFromResult(activeSessionId);
            return;
        }

        if (!hasResultState && phaseElapsed < timeout)
        {
            return;
        }

        if (hasResultState && normalizedTime < releaseThreshold && phaseElapsed < timeout)
        {
            return;
        }

        Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 玩家结果动作释放兜底触发。" + $"结果={pendingResult}，阶段={phase}，动画进度={normalizedTime}。");
        ForceReleasePlayerFromResult(activeSessionId);
    }

    /// <summary>CheckFailureExitFailsafe 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.TryFinishAfterPairExit()、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） Debug.LogWarning(object)、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.TryFinishAfterPairExit()、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CheckFailureExitFailsafe()
    {
        if (phase != QTEPhase.Exiting || pendingResult != QTEResult.Failure || cleanupRunning || activeSessionId == 0)
        {
            return;
        }

        // playerTimeout（玩家超时）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonQTECoordinator.CheckFailureExitFailsafe()。
float playerTimeout;
        if (config != null)
        {
            playerTimeout = Mathf.Max(0.1f, config.playerExitSafetyTimeout);
        }
        else
        {
            playerTimeout = 0.35f;
        }

        // enemyTimeout（敌人超时）：使用位置：作为 Mathf.Max(float, float) 的参数、LeonQTECoordinator.CheckFailureExitFailsafe()。
float enemyTimeout;
        if (config != null)
        {
            enemyTimeout = Mathf.Max(0.1f, config.enemyExitSafetyTimeout);
        }
        else
        {
            enemyTimeout = 0.5f;
        }

        if (!playerExited && phaseElapsed >= playerTimeout)
        {
            Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 玩家失败退出回调超时，强制释放玩家控制权。");
            playerExited = true;
            if (!playerReleased)
            {
                ReleasePlayer(activeSessionId);
            }
        }

        if (!enemyExited && phaseElapsed >= enemyTimeout)
        {
            Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 敌人失败退出回调超时，强制进入统一清理。");
            enemyExited = true;
        }

        TryFinishAfterPairExit();
        // cleanupTimeout（清理超时）：使用位置：LeonQTECoordinator.CheckFailureExitFailsafe()。
float cleanupTimeout = Mathf.Max(playerTimeout, enemyTimeout) + 0.25f;
        if (!cleanupRunning && phaseElapsed >= cleanupTimeout)
        {
            Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 失败退出整体清理超时，直接恢复双方状态。");
            FinishResultMotion();
            FinishCleanup(QTEResult.Failure, QTECancelReason.None);
        }
    }

    /// <summary>TryGetPlayerResultNormalizedTime 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()、LeonQTECoordinator.TryFinalizeCommittedAssassination()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">中文含义：是否成功。使用位置：LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。</param>
    /// <param name="normalizedTime">中文含义：动画归一化播放时间。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。</param>
    private bool TryGetPlayerResultNormalizedTime(bool success, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (playerAnimator == null)
        {
            return false;
        }

        // expectedHash（预期哈希编号）：使用位置：LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。
int expectedHash;
        if (success)
        {
            expectedHash = HashQTESuccessState;
        }
        else
        {
            expectedHash = HashQTEFailState;
        }

        // stateInfo（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)。
AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        if (stateInfo.shortNameHash != expectedHash && playerAnimator.IsInTransition(AnimatorLayerID.FullBodyAction))
        {
            stateInfo = playerAnimator.GetNextAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        }

        if (stateInfo.shortNameHash != expectedHash)
        {
            return false;
        }

        normalizedTime = stateInfo.normalizedTime;
        return true;
    }

    /// <summary>ForceReleasePlayerFromResult 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.CommitBiteDamage(int)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckResultPlayerReleaseFailsafe()；下游（含内部回调体） LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.CommitBiteDamage(int)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonQTECoordinator.CommitAssassination(int) 的参数、作为 LeonQTECoordinator.CommitBiteDamage(int) 的参数、作为 LeonQTECoordinator.HandleEnemySuccessExit(int) 的参数 等共 5 处。</param>
    private void ForceReleasePlayerFromResult(int sessionId)
    {
        if (sessionId == 0 || sessionId != activeSessionId || playerReleased)
        {
            return;
        }

        // success（是否成功）：使用位置：LeonQTECoordinator.ForceReleasePlayerFromResult(int)。
bool success = pendingResult == QTEResult.Success;
        FinishResultMotion();
        if (!success && !damageCommitted)
        {
            CommitBiteDamage(sessionId);
            if (sessionId != activeSessionId)
            {
                return;
            }
        }

        if (success)
        {
            if (activeKind == QTEKind.KnifeAssassination && !assassinationCommitted)
            {
                CommitAssassination(sessionId);
            }

            if (IsCommittedAssassinationSuccess())
            {
                ReleasePlayer(sessionId);
                HandleEnemySuccessExit(sessionId);
                return;
            }

            RequestPlayerRecoveryExit();
            if (!playerReleased)
            {
                ReleasePlayer(sessionId);
            }

            if (enemyActionCompleted && !enemyEndRequested)
            {
                RequestEnemyEnd();
            }

            return;
        }

        ChangePhase(QTEPhase.Exiting);
        playerExited = true;
        ReleasePlayer(sessionId);
        if (!enemyEndRequested)
        {
            RequestEnemyEnd();
        }
    }

    /// <summary>TryFinalizeCommittedAssassination 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)、Mathf.Max(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.TryGetPlayerResultNormalizedTime(bool, out float)、Mathf.Max(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryFinalizeCommittedAssassination()
    {
        if (IsSingleShotAssassination() && (!playerActionCompleted || !enemyActionCompleted))
        {
            return;
        }

        if (playerReleased)
        {
            HandleEnemySuccessExit(activeSessionId);
            return;
        }

        // delay（延迟）：使用位置：LeonQTECoordinator.TryFinalizeCommittedAssassination()。
float delay;
        if (config != null)
        {
            delay = Mathf.Max(0.05f, config.assassinationSuccessForceExitDelay);
        }
        else
        {
            delay = 0.35f;
        }

        if (Time.time - assassinationCommittedTime < delay)
        {
            return;
        }

        if (TryGetPlayerResultNormalizedTime(true, out float /* 中文含义：动画归一化播放时间。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ normalizedTime) && normalizedTime < ResolveSuccessPlayerReleaseNormalizedTime())
        {
            return;
        }

        // receiverValue44（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.TryFinalizeCommittedAssassination()。
global::LeonAnimationActionCoordinator receiverValue44 = animationCoordinator;
        // optionalValue45（条件访问得到的力度结束配对动作的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.TryFinalizeCommittedAssassination()。
bool? optionalValue45;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            optionalValue45 = receiverValue44.ForceFinishQTE(activeSessionId);
        }
        else
        {
            optionalValue45 = default;
        }

        ReleasePlayer(activeSessionId);
        HandleEnemySuccessExit(activeSessionId);
    }

    /// <summary>IsSingleShotAssassination 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.HandleAssassinationContact(int)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()、LeonQTECoordinator.TryFinalizeCommittedAssassination()、LeonQTECoordinator.TryFinishSingleShotAssassination()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsSingleShotAssassination()
    {
        return activeKind == QTEKind.KnifeAssassination && activeVariant != null && activeVariant.singleShot;
    }

    /// <summary>BeginSingleShotAssassinationResult 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.StopQTEStruggleLoop()、LeonQTECoordinator.TryFinishSingleShotAssassination()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.StopQTEStruggleLoop()、LeonQTECoordinator.TryFinishSingleShotAssassination()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginSingleShotAssassinationResult()
    {
        if (!IsSingleShotAssassination() || cleanupRunning)
        {
            return;
        }

        if (pendingResult == QTEResult.None)
        {
            pendingResult = QTEResult.Success;
            StopQTEStruggleLoop();
            ChangePhase(QTEPhase.SuccessResult);
        }

        if (!assassinationCommitted)
        {
            CommitAssassination(activeSessionId);
        }

        TryFinishSingleShotAssassination();
    }

    /// <summary>TryFinishSingleShotAssassination 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.ReleasePlayer(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonQTECoordinator.CommitAssassination(int)、LeonQTECoordinator.FinishResultMotion()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.ReleasePlayer(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryFinishSingleShotAssassination()
    {
        if (!IsSingleShotAssassination() || cleanupRunning || pendingResult != QTEResult.Success || !playerActionCompleted || !enemyActionCompleted)
        {
            return;
        }

        // sessionId（会话编号）：使用位置：作为 LeonQTECoordinator.CommitAssassination(int) 的参数、作为 LeonQTECoordinator.HandleEnemySuccessExit(int) 的参数、作为 LeonQTECoordinator.ReleasePlayer(int) 的参数 等共 4 处。
int sessionId = activeSessionId;
        if (!assassinationCommitted)
        {
            CommitAssassination(sessionId);
        }

        if (sessionId == 0 || sessionId != activeSessionId || !assassinationCommitted)
        {
            return;
        }

        FinishResultMotion();
        ReleasePlayer(sessionId);
        HandleEnemySuccessExit(sessionId);
    }

    /// <summary>RequestPlayerRecoveryExit 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckPlayerRecoveryThreshold()、LeonQTECoordinator.ForceReleasePlayerFromResult(int)；下游（含内部回调体） Debug.LogWarning(object)、LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()、LeonQTECoordinator.SetPlayerWorldPosition(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RequestPlayerRecoveryExit()
    {
        plannedRecoveryExit = true;
        playerExitRequestedTime = Time.time;
        if (!playerReleased)
        {
            SetPlayerWorldPosition(resultPlayerTarget);
        }

        ChangePhase(QTEPhase.Exiting);
        if (!animationCoordinator.TryExitQTESuccessAtRecoveryPoint(activeSessionId, ResolveSuccessPlayerReleaseNormalizedTime()))
        {
            Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 请求恢复点退出失败。");
        }
    }

    /// <summary>ReleasePlayer 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimatorDriver.SetMovement(float, float, float, bool)、LeonAnimRig.ExitQTEIK(WeaponType)、LeonController.FinishQTEState(bool)、LeonInputBridge.ExitQTEInput(int)、LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.HandlePlayerExitTimeout() 等共 8 处；下游（含内部回调体） LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimatorDriver.SetMovement(float, float, float, bool)、LeonAnimRig.ExitQTEIK(WeaponType)、LeonController.FinishQTEState(bool)、LeonInputBridge.ExitQTEInput(int)、LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 LeonAnimationActionCoordinator.ForceFinishQTE(int) 的参数、作为 LeonInputBridge.ExitQTEInput(int) 的参数。</param>
    private void ReleasePlayer(int sessionId)
    {
        if (playerReleased)
        {
            return;
        }

        playerReleased = true;
        // restoreWeapon（恢复武器）：保存 LeonQTECoordinator.ResolveQTEExitWeapon() 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType) 的参数、作为 LeonAnimRig.ExitQTEIK(WeaponType) 的参数、作为 LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool) 的参数 等共 4 处。
WeaponType restoreWeapon = ResolveQTEExitWeapon();
        // resumeMovement（是否恢复执行移动）：使用位置：作为 LeonController.FinishQTEState(bool) 的参数。
bool resumeMovement = pendingResult == QTEResult.Success;
        // 先把基础移动参数恢复为稳定的静止值，再让全身层开始淡出。
        // receiverValue46（判空前缓存的动画控制器驱动器）：从动画控制器驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ReleasePlayer(int)。
global::LeonAnimatorDriver receiverValue46 = // 先把基础移动参数恢复为稳定的静止值，再让全身层开始淡出。
 animatorDriver;
        if (!global::System.Object.ReferenceEquals(receiverValue46, null))
        {
            receiverValue46.SetMovement(0f, 0f, 0f, false);
        }

        // receiverValue47（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ReleasePlayer(int)。
global::LeonAnimationActionCoordinator receiverValue47 = animationCoordinator;
        // optionalValue48（条件访问得到的力度结束配对动作的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.ReleasePlayer(int)。
bool? optionalValue48;
        if (!global::System.Object.ReferenceEquals(receiverValue47, null))
        {
            optionalValue48 = receiverValue47.ForceFinishQTE(sessionId);
        }
        else
        {
            optionalValue48 = default;
        }

        // receiverValue49（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ReleasePlayer(int)。
global::LeonAnimationActionCoordinator receiverValue49 = animationCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.ForceClearWeaponActionBlockAfterQTE(restoreWeapon);
        }

        EndAssassinationKnifeVisualIfNeeded();
        RestoreWeaponVisualAfterQTE(restoreWeapon);
        RestorePlayerLock();
        if (animRig != null && !animRig.IsDead)
        {
            animRig.ExitQTEIK(restoreWeapon);
        }

        ScheduleWeaponTypeRestore(restoreWeapon);
        // receiverValue50（判空前缓存的输入桥接）：从输入桥接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ReleasePlayer(int)。
global::LeonInputBridge receiverValue50 = inputBridge;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.ExitQTEInput(sessionId);
        }

        if (controller != null && controller.currentState == LeonState.QTE)
        {
            controller.FinishQTEState(resumeMovement);
        }
    }

    /// <summary>RestoreWeaponVisualAfterQTE 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)、LeonQTECoordinator.CanRestoreWeapon(WeaponType) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)、LeonQTECoordinator.CanRestoreWeapon(WeaponType) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、作为 LeonAnimRig.SetWeapon(WeaponType) 的参数、作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数 等共 7 处。</param>
    /// <param name="notify">中文含义：是否通知。使用位置：作为 LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool) 的参数。</param>
    private void RestoreWeaponVisualAfterQTE(WeaponType weapon, bool notify = true)
    {
        if (!IsSupportedQTEWeapon(weapon) || !CanRestoreWeapon(weapon))
        {
            return;
        }

        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }

        if (weaponHandler != null)
        {
            weaponHandler.ForceSyncCurrentWeapon(weapon, notify);
            return;
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        // receiverValue51（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)。
global::LeonWeaponVisualController receiverValue51 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue51, null))
        {
            receiverValue51.SetEquippedWeapon(weapon);
        }

        // receiverValue52（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)。
global::LeonAnimRig receiverValue52 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue52, null))
        {
            receiverValue52.SetWeapon(weapon);
        }

        // 正式角色必须绑定武器持有者；缺失时只恢复兼容表现，不另存装备状态。
        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<LeonAnimatorDriver>();
        }

        if (animatorDriver != null)
        {
            if (weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun)
            {
                animatorDriver.SetKnifeMode(false);
                animatorDriver.SetWeaponType(weapon);
            }
            else if (weapon == WeaponType.Knife)
            {
                animatorDriver.SetKnifeMode(true);
            }
        }
    }

    /// <summary>ResolveCurrentSessionWeapon 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.ResolveOwnedFallbackWeapon()、LeonQTECoordinator.ResolveWeaponFromAnimator()、LeonQTECoordinator.ResolveWeaponFromHandler()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveQTEExitWeapon()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.ResolveOwnedFallbackWeapon()、LeonQTECoordinator.ResolveWeaponFromAnimator()、LeonQTECoordinator.ResolveWeaponFromHandler()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType ResolveCurrentSessionWeapon()
    {
        // controllerWeapon（控制器武器）：使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveCurrentSessionWeapon()。
WeaponType controllerWeapon;
        if (controller != null)
        {
            controllerWeapon = controller.currentWeapon;
        }
        else
        {
            controllerWeapon = WeaponType.Pistol;
        }

        if (CanRestoreWeapon(controllerWeapon))
        {
            return controllerWeapon;
        }

        // handlerWeapon（处理器武器）：保存 LeonQTECoordinator.ResolveWeaponFromHandler() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveCurrentSessionWeapon()。
WeaponType handlerWeapon = ResolveWeaponFromHandler();
        if (CanRestoreWeapon(handlerWeapon))
        {
            return handlerWeapon;
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        // visualWeapon（视觉表现武器）：使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveCurrentSessionWeapon()。
WeaponType visualWeapon;
        if (weaponVisualController != null)
        {
            visualWeapon = weaponVisualController.CurrentEquippedWeapon;
        }
        else
        {
            visualWeapon = WeaponType.Pistol;
        }

        // rigWeapon（绑定武器）：使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveCurrentSessionWeapon()。
WeaponType rigWeapon;
        if (animRig != null)
        {
            rigWeapon = animRig.CurrentWeapon;
        }
        else
        {
            rigWeapon = WeaponType.Pistol;
        }

        // animatorWeapon（动画控制器武器）：保存 LeonQTECoordinator.ResolveWeaponFromAnimator() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveCurrentSessionWeapon()。
WeaponType animatorWeapon = ResolveWeaponFromAnimator();
        if (CanRestoreWeapon(visualWeapon))
        {
            return visualWeapon;
        }

        if (CanRestoreWeapon(rigWeapon))
        {
            return rigWeapon;
        }

        if (CanRestoreWeapon(animatorWeapon))
        {
            return animatorWeapon;
        }

        return ResolveOwnedFallbackWeapon();
    }

    /// <summary>ResolveWeaponFromAnimator 的业务处理入口；本方法直接依赖 Animator.GetFloat(int)、LeonQTECoordinator.EnsureWeaponParameterCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveCurrentSessionWeapon()；下游（含内部回调体） Animator.GetFloat(int)、LeonQTECoordinator.EnsureWeaponParameterCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType ResolveWeaponFromAnimator()
    {
        if (playerAnimator == null)
        {
            return WeaponType.Pistol;
        }

        EnsureWeaponParameterCache();
        if (cachedKnifeMode && playerAnimator.GetFloat(HashKnifeMode) > 0.5f)
        {
            return WeaponType.Knife;
        }

        if (cachedWeaponType)
        {
            // weaponTypeValue（武器类型值）：保存 Animator.GetFloat(int) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ResolveWeaponFromAnimator()。
float weaponTypeValue = playerAnimator.GetFloat(HashWeaponType);
            if (weaponTypeValue > 2f)
            {
                return WeaponType.Shotgun;
            }

            if (weaponTypeValue > 0.5f)
            {
                return WeaponType.Rifle;
            }
        }

        return WeaponType.Pistol;
    }

    /// <summary>中文含义：缓存武器控制器。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.EnsureWeaponParameterCache()。</summary>
    private RuntimeAnimatorController cachedWeaponController;
    /// <summary>中文含义：weaponParameterCacheInitialized 表示“武器参数缓存是否已初始化”；cachedKnifeMode 表示“是否缓存小刀模式”；cachedWeaponType 表示“是否缓存武器类型”。使用位置：LeonQTECoordinator.EnsureWeaponParameterCache()。；使用位置：LeonQTECoordinator.EnsureWeaponParameterCache()、LeonQTECoordinator.ResolveWeaponFromAnimator()。；使用位置：LeonQTECoordinator.EnsureWeaponParameterCache()、LeonQTECoordinator.ResolveWeaponFromAnimator()。</summary>
    private bool weaponParameterCacheInitialized, cachedKnifeMode, cachedWeaponType;
    /// <summary>GetPromptCandidates 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private System.Collections.Generic.IReadOnlyList<VillagerQTEParticipant> GetPromptCandidates()
    {
        if (EnemyAIManager.HasInstance)
        {
            return EnemyAIManager.Instance.PromptCandidates;
        }
        else
        {
            return VillagerQTEParticipant.ActiveParticipants;
        }
    }

    /// <summary>EnsureWeaponParameterCache 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveWeaponFromAnimator()、LeonQTECoordinator.Start()；下游（含内部回调体） object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureWeaponParameterCache()
    {
        // current（当前运行时动画控制器控制器）：使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.EnsureWeaponParameterCache()。
RuntimeAnimatorController current;
        if (playerAnimator != null)
        {
            current = playerAnimator.runtimeAnimatorController;
        }
        else
        {
            current = null;
        }

        if (weaponParameterCacheInitialized && object.ReferenceEquals(current, cachedWeaponController))
        {
            return;
        }

        weaponParameterCacheInitialized = true;
        cachedWeaponController = current;
        cachedKnifeMode = false;
        cachedWeaponType = false;
        if (playerAnimator == null || current == null)
        {
            return;
        }

        // parameter（参数）：每轮从玩家动画控制器的参数集合取得一个元素，仅代表当前这一项。使用位置：LeonQTECoordinator.EnsureWeaponParameterCache()。
foreach (var parameter in playerAnimator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float)
            {
                continue;
            }

            if (parameter.nameHash == HashWeaponType)
            {
                cachedWeaponType = true;
            }

            if (parameter.nameHash == HashKnifeMode)
            {
                cachedKnifeMode = true;
            }
        }
    }

    /// <summary>ResolveWeaponFromHandler 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.GetCurrentWeapon()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveCurrentSessionWeapon()；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.GetCurrentWeapon()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType ResolveWeaponFromHandler()
    {
        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }

        if (weaponHandler == null)
        {
            return WeaponType.Pistol;
        }

        // currentWeaponBase（当前武器基础）：保存 LeonWeaponHandler.GetCurrentWeapon() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.ResolveWeaponFromHandler()。
WeaponBase currentWeaponBase = weaponHandler.GetCurrentWeapon();
        if (currentWeaponBase != null && currentWeaponBase.data != null)
        {
            return currentWeaponBase.data.weaponType;
        }

        if (controller != null)
        {
            return controller.currentWeapon;
        }
        else
        {
            return WeaponType.Pistol;
        }
    }

    /// <summary>ResolveQTEExitWeapon 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.ResolveCurrentSessionWeapon()、LeonQTECoordinator.ResolveOwnedFallbackWeapon()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.ResolveCurrentSessionWeapon()、LeonQTECoordinator.ResolveOwnedFallbackWeapon()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType ResolveQTEExitWeapon()
    {
        if (CanRestoreWeapon(activeSessionWeapon))
        {
            return activeSessionWeapon;
        }

        // currentResolvedWeapon（当前解算后的武器）：保存 LeonQTECoordinator.ResolveCurrentSessionWeapon() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveQTEExitWeapon()。
WeaponType currentResolvedWeapon = ResolveCurrentSessionWeapon();
        if (CanRestoreWeapon(currentResolvedWeapon))
        {
            return currentResolvedWeapon;
        }

        return ResolveOwnedFallbackWeapon();
    }

    /// <summary>ScheduleWeaponTypeRestore 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.IsGunWeapon(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.IsGunWeapon(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、作为 LeonQTECoordinator.IsGunWeapon(WeaponType) 的参数 等共 4 处。</param>
    private void ScheduleWeaponTypeRestore(WeaponType weapon)
    {
        if (!IsGunWeapon(weapon) || !CanRestoreWeapon(weapon))
        {
            weaponRestorePending = false;
            weaponRestoreFrames = 0;
            return;
        }

        pendingWeaponRestore = weapon;
        weaponRestorePending = true;
        weaponRestoreFrames = 12;
        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<LeonAnimatorDriver>();
        }

        // receiverValue59（判空前缓存的动画控制器驱动器）：从动画控制器驱动器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)。
global::LeonAnimatorDriver receiverValue59 = animatorDriver;
        // optionalValue60（条件访问得到的集合武器类型的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)。
bool? optionalValue60;
        if (!global::System.Object.ReferenceEquals(receiverValue59, null))
        {
            optionalValue60 = receiverValue59.SetWeaponType(weapon);
        }
        else
        {
            optionalValue60 = default;
        }
    }

    /// <summary>RestoreWeaponTypeAfterQTE 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.LateUpdate()；下游（含内部回调体） Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestoreWeaponTypeAfterQTE()
    {
        if (!weaponRestorePending)
        {
            return;
        }

        if (animatorDriver == null)
        {
            animatorDriver = GetComponent<LeonAnimatorDriver>();
        }

        if (animatorDriver == null)
        {
            return;
        }

        animatorDriver.SetWeaponType(pendingWeaponRestore);
        weaponRestoreFrames--;
        if (weaponRestoreFrames <= 0)
        {
            weaponRestorePending = false;
        }
    }

    /// <summary>CanRestoreWeapon 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonQTECoordinator.IsSupportedQTEWeapon(WeaponType)、LeonWeaponHandler.HasWeaponInInventory(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveCurrentSessionWeapon()、LeonQTECoordinator.ResolveOwnedFallbackWeapon()、LeonQTECoordinator.ResolveQTEExitWeapon()、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonQTECoordinator.IsSupportedQTEWeapon(WeaponType)、LeonWeaponHandler.HasWeaponInInventory(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonQTECoordinator.IsSupportedQTEWeapon(WeaponType) 的参数、作为 LeonWeaponHandler.HasWeaponInInventory(WeaponType) 的参数。</param>
    private bool CanRestoreWeapon(WeaponType weapon)
    {
        if (!IsSupportedQTEWeapon(weapon))
        {
            return false;
        }

        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }

        return weaponHandler == null || weaponHandler.HasWeaponInInventory(weapon);
    }

    /// <summary>ResolveOwnedFallbackWeapon 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CanRestoreWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ResolveCurrentSessionWeapon()、LeonQTECoordinator.ResolveQTEExitWeapon()；下游（含内部回调体） LeonQTECoordinator.CanRestoreWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType ResolveOwnedFallbackWeapon()
    {
        // fallbackOrder（后备顺序）：使用位置：作为 LeonQTECoordinator.CanRestoreWeapon(WeaponType) 的参数、LeonQTECoordinator.ResolveOwnedFallbackWeapon()。
WeaponType[] fallbackOrder =
        {
            WeaponType.Pistol,
            WeaponType.Shotgun,
            WeaponType.Rifle,
            WeaponType.Knife
        };
        // i（当前遍历索引）：使用位置：LeonQTECoordinator.ResolveOwnedFallbackWeapon()。
for (int i = 0; i < fallbackOrder.Length; i++)
        {
            if (CanRestoreWeapon(fallbackOrder[i]))
            {
                return fallbackOrder[i];
            }
        }

        return WeaponType.Pistol;
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.IsSupportedQTEWeapon(WeaponType)、LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonQTECoordinator.IsGunWeapon(WeaponType)。</param>
    private static bool IsGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>IsSupportedQTEWeapon 的业务处理入口；本方法直接依赖 LeonQTECoordinator.IsGunWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)；下游（含内部回调体） LeonQTECoordinator.IsGunWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonQTECoordinator.IsGunWeapon(WeaponType) 的参数、LeonQTECoordinator.IsSupportedQTEWeapon(WeaponType)。</param>
    private static bool IsSupportedQTEWeapon(WeaponType weapon)
    {
        return IsGunWeapon(weapon) || weapon == WeaponType.Knife;
    }

    /// <summary>RequestEnemyEnd 的业务处理入口；本方法直接依赖 IEnemyQTEParticipant.EndAction(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.HandlePlayerExitTimeout()；下游（含内部回调体） IEnemyQTEParticipant.EndAction(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RequestEnemyEnd()
    {
        if (enemyEndRequested || participant == null)
        {
            return;
        }

        enemyEndRequested = true;
        enemyEndRequestedTime = Time.time;
        participant.EndAction(activeSessionId);
    }

    /// <summary>HandleEnemySuccessExit 的业务处理入口；本方法直接依赖 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ForceReleasePlayerFromResult(int)、LeonQTECoordinator.HandleEnemyExitTimeout()、LeonQTECoordinator.HandleParticipantActionExited(int, EnemyQTEAction, int, bool)、LeonQTECoordinator.HandlePlayerActionExited(AnimationActionTransaction)、LeonQTECoordinator.HandlePlayerExitTimeout() 等共 7 处；下游（含内部回调体） LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonQTECoordinator.HandleEnemySuccessExit(int)。</param>
    private void HandleEnemySuccessExit(int sessionId)
    {
        if (sessionId != activeSessionId || pendingResult != QTEResult.Success)
        {
            return;
        }

        resultMotionActive = false;
        if (participant != null && participant.Root != null && config != null)
        {
            // toPlayer（目标玩家）：使用位置：LeonQTECoordinator.HandleEnemySuccessExit(int)。
Vector3 toPlayer = transform.position - participant.Root.position;
            toPlayer.y = 0f;
            // currentDistance（当前距离）：使用位置：LeonQTECoordinator.HandleEnemySuccessExit(int)。
float currentDistance = toPlayer.magnitude;
            if (currentDistance < config.minimumSuccessFinalDistance)
            {
                // direction（方向）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、LeonQTECoordinator.HandleEnemySuccessExit(int)。
Vector3 direction;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    direction = toPlayer.normalized;
                }
                else
                {
                    direction = -participant.Root.forward;
                }

                // needed（需要）：使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。
float needed = config.minimumSuccessFinalDistance - currentDistance;
                // safeNeeded（安全需要）：保存 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、LeonQTECoordinator.HandleEnemySuccessExit(int)。
float safeNeeded = GetSafeSeparationDistance(participant.Root.position, -direction, needed, participant);
                if (safeNeeded > 0f)
                {
                    SetParticipantWorldPosition(participant.Root, participant.Root.position - direction * safeNeeded);
                }
            }
        }

        FinishCleanup(QTEResult.Success, QTECancelReason.None);
    }

    /// <summary>HandlePlayerExitTimeout 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Debug.LogWarning(object)、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.RequestEnemyEnd() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Debug.LogWarning(object)、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.IsCommittedAssassinationSuccess()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.RequestEnemyEnd() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandlePlayerExitTimeout()
    {
        // stateInfo（当前动画状态信息）：使用位置：作为 Debug.LogWarning(object) 的参数、LeonQTECoordinator.HandlePlayerExitTimeout()。
AnimatorStateInfo stateInfo;
        if (playerAnimator != null)
        {
            stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        }
        else
        {
            stateInfo = default;
        }

        Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 玩家恢复点退出超时。" + $"动作类型={expectedPlayerAction}，当前阶段={phase}，" + $"标准化进度={stateInfo.normalizedTime}。执行强制收尾。");
        // receiverValue63（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.HandlePlayerExitTimeout()。
global::LeonAnimationActionCoordinator receiverValue63 = animationCoordinator;
        // optionalValue64（条件访问得到的力度结束配对动作的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonQTECoordinator.HandlePlayerExitTimeout()。
bool? optionalValue64;
        if (!global::System.Object.ReferenceEquals(receiverValue63, null))
        {
            optionalValue64 = receiverValue63.ForceFinishQTE(activeSessionId);
        }
        else
        {
            optionalValue64 = default;
        }

        if (!playerReleased)
        {
            ReleasePlayer(activeSessionId);
        }

        if (IsCommittedAssassinationSuccess())
        {
            HandleEnemySuccessExit(activeSessionId);
            return;
        }

        if (enemyActionCompleted && !enemyEndRequested)
        {
            RequestEnemyEnd();
        }
    }

    /// <summary>HandleEnemyExitTimeout 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonQTECoordinator.HandleEnemySuccessExit(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） Debug.LogWarning(object)、LeonQTECoordinator.HandleEnemySuccessExit(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void HandleEnemyExitTimeout()
    {
        Debug.LogWarning($"[里昂快速反应事件] 会话 {activeSessionId} 敌人退出超时。强制释放参与者并完成会话。");
        HandleEnemySuccessExit(activeSessionId);
    }

    /// <summary>FinishCleanup 的业务处理入口；本方法直接依赖 Action&lt;QTESessionEndInfo&gt;.Invoke(QTESessionEndInfo)、IEnemyQTEParticipant.Release(int)、LeonAnimationActionCoordinator.ClearQTEAnimationVariant()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.ResolveQTEExitWeapon()、LeonQTECoordinator.RestorePairCollisions() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.CheckFailureExitFailsafe()、LeonQTECoordinator.HandleEnemySuccessExit(int)、LeonQTECoordinator.SeparateAndFinish(int)；下游（含内部回调体） Action&lt;QTESessionEndInfo&gt;.Invoke(QTESessionEndInfo)、IEnemyQTEParticipant.Release(int)、LeonAnimationActionCoordinator.ClearQTEAnimationVariant()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.ResolveQTEExitWeapon()、LeonQTECoordinator.RestorePairCollisions() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前配对动作结果。使用位置：作为 QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)。</param>
    /// <param name="reason">中文含义：原因。使用位置：作为 QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool) 的参数。</param>
    private void FinishCleanup(QTEResult result, QTECancelReason reason)
    {
        // finishedSessionId（结束会话编号）：使用位置：作为 IEnemyQTEParticipant.Release(int) 的参数、作为 LeonQTECoordinator.ReleasePlayer(int) 的参数、作为 QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool) 的参数 等共 4 处。
int finishedSessionId = activeSessionId;
        if (finishedSessionId == 0 || cleanupSessionId == finishedSessionId)
        {
            return;
        }

        if (activeKind == QTEKind.RearGrab || activeKind == QTEKind.FrontGrab)
        {
            float sourceFrame = -1f;
            if (nativePair != null)
            {
                sourceFrame = nativePair.SourceFrame;
            }
            Debug.Log($"[村庄抓取] 会话={finishedSessionId} 动作={activeKind} 阶段={phase} 源帧={sourceFrame:F1} 输入计数={pressCount} 长按={rearHoldElapsed:F2} 协攻命中={rearHelperHit} 结果={result} 原因={reason}");
        }
        rearHelper = null;
        StopQTEStruggleLoop();
        cleanupSessionId = finishedSessionId;
        cleanupRunning = true;
        bool lethalNativeFailure = nativePair != null && damageCommitted && controller != null &&
            controller.Health != null && controller.Health.IsDead;
        bool blendGrabExit = controller != null && controller.currentState != LeonState.Dead &&
            (activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearGrab);
        StopNativePair(blendGrabExit || result == QTEResult.Cancelled &&
            reason is QTECancelReason.SpaceBlocked or QTECancelReason.AnimationInterrupted or QTECancelReason.SafetyTimeout,
            lethalNativeFailure);
        // finishedParticipant（结束参与者）：使用位置：作为 LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)。
IEnemyQTEParticipant finishedParticipant = participant;
        if (!playerReleased)
        {
            ReleasePlayer(finishedSessionId);
        }

        resultMotionActive = false;
        RestorePairCollisions();
        if (finishedParticipant != null)
        {
            UnsubscribeParticipant(finishedParticipant);
            finishedParticipant.Release(finishedSessionId);
        }

        // receiverValue65（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)。
global::LeonAnimationActionCoordinator receiverValue65 = animationCoordinator;
        if (!global::System.Object.ReferenceEquals(receiverValue65, null))
        {
            receiverValue65.ClearQTEAnimationVariant();
        }

        if (!lethalNativeFailure && (controller == null || controller.currentState != LeonState.Dead))
        {
            // restoreWeapon（恢复武器）：保存 LeonQTECoordinator.ResolveQTEExitWeapon() 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType) 的参数、作为 LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool) 的参数、作为 LeonQTECoordinator.ScheduleWeaponTypeRestore(WeaponType) 的参数。
WeaponType restoreWeapon = ResolveQTEExitWeapon();
            RestoreWeaponVisualAfterQTE(restoreWeapon, false);
            // receiverValue66（判空前缓存的动画协调器）：从动画协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)。
global::LeonAnimationActionCoordinator receiverValue66 = animationCoordinator;
            if (!global::System.Object.ReferenceEquals(receiverValue66, null))
            {
                receiverValue66.ForceClearWeaponActionBlockAfterQTE(restoreWeapon);
            }

            ScheduleWeaponTypeRestore(restoreWeapon);
        }

        if (globalOwner == this)
        {
            globalOwner = null;
        }

        participant = null;
        activeSessionId = 0;
        pairedKnife = null;
        activeVariant = null;
        if (result == QTEResult.Cancelled)
        {
            phase = QTEPhase.Cancelled;
        }
        else
        {
            phase = QTEPhase.Completed;
        }

        pendingResult = QTEResult.None;
        expectedPlayerAction = FullBodyActionType.None;
        expectedEnemyAction = EnemyQTEAction.None;
        plannedRecoveryExit = false;
        playerReleased = false;
        enemyEndRequested = false;
        assassinationCommitted = false;
        assassinationCommittedTime = float.NegativeInfinity;
        cleanupRunning = false;
        activeRoutine = null;
        lastEndInfo = new QTESessionEndInfo(finishedSessionId, result, reason, damageCommitted);
        // receiverValue68（判空前缓存的配对动作会话已结束时的通知回调）：从配对动作会话已结束时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)。
global::System.Action<global::QTESessionEndInfo> receiverValue68 = OnQTESessionEnded;
        if (!global::System.Object.ReferenceEquals(receiverValue68, null))
        {
            receiverValue68.Invoke(lastEndInfo);
        }

        nextPromptScanTime = 0f;
        damageCommitted = false;
        if (lethalNativeFailure)
        {
            controller.CompleteNativeQTEDeath();
        }
    }

    /// <summary>SubscribeParticipant 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)。</param>
    private void SubscribeParticipant(IEnemyQTEParticipant targetParticipant)
    {
        targetParticipant.ActionCompleted += HandleParticipantActionCompleted;
        targetParticipant.ActionExited += HandleParticipantActionExited;
        targetParticipant.BiteContact += HandleBiteContact;
        targetParticipant.AssassinationContact += HandleAssassinationContact;
        targetParticipant.Invalidated += HandleParticipantInvalidated;
    }

    /// <summary>UnsubscribeParticipant 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetParticipant">中文含义：目标参与者。使用位置：LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</param>
    private void UnsubscribeParticipant(IEnemyQTEParticipant targetParticipant)
    {
        targetParticipant.ActionCompleted -= HandleParticipantActionCompleted;
        targetParticipant.ActionExited -= HandleParticipantActionExited;
        targetParticipant.BiteContact -= HandleBiteContact;
        targetParticipant.AssassinationContact -= HandleAssassinationContact;
        targetParticipant.Invalidated -= HandleParticipantInvalidated;
    }

    /// <summary>ChangePhase 的业务处理入口；本方法直接依赖 Action&lt;int, QTEPhase&gt;.Invoke(int, QTEPhase)、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)、LeonQTECoordinator.PublishProgress()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.Cancel(int, QTECancelReason)、LeonQTECoordinator.ForceReleasePlayerFromResult(int) 等共 10 处；下游（含内部回调体） Action&lt;int, QTEPhase&gt;.Invoke(int, QTEPhase)、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)、LeonQTECoordinator.PublishProgress()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nextPhase">中文含义：下一次阶段。使用位置：作为 Action&lt;int, QTEPhase&gt;.Invoke(int, QTEPhase) 的参数、作为 LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase) 的参数、LeonQTECoordinator.ChangePhase(QTEPhase)。</param>
    private void ChangePhase(QTEPhase nextPhase)
    {
        phase = nextPhase;
        phaseElapsed = 0f;
        // receiverValue69（判空前缓存的配对动作阶段变化时的通知回调）：从配对动作阶段变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.ChangePhase(QTEPhase)。
global::System.Action<int, global::QTEPhase> receiverValue69 = OnQTEPhaseChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue69, null))
        {
            receiverValue69.Invoke(activeSessionId, nextPhase);
        }

        PlayQTEAudioForPhase(nextPhase);
        if (nextPhase == QTEPhase.WaitingForInput)
        {
            remainingTime = ReadQteInputDuration();
            pressCount = 0;
            PublishProgress();
            double earlyPress = earlyEscapePressedAt;
            earlyEscapePressedAt = double.NegativeInfinity;
            double age = Time.timeAsDouble - earlyPress;
            if (age >= 0 && age <= EarlyEscapeWindow)
            {
                SubmitEscapePress(activeSessionId, earlyPress);
            }
        }
        else if (nextPhase != QTEPhase.Entering)
        {
            earlyEscapePressedAt = double.NegativeInfinity;
        }
    }

    /// <summary>PlayQTEAudioForPhase 的业务处理入口；本方法直接依赖 LeonQTECoordinator.PlayQTEOneShot(AudioEventData)、LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData)、LeonQTECoordinator.StopQTEStruggleLoop()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ChangePhase(QTEPhase)；下游（含内部回调体） LeonQTECoordinator.PlayQTEOneShot(AudioEventData)、LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData)、LeonQTECoordinator.StopQTEStruggleLoop()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nextPhase">中文含义：下一次阶段。使用位置：LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。</param>
    private void PlayQTEAudioForPhase(QTEPhase nextPhase)
    {
        if ((nextPhase == QTEPhase.Entering || nextPhase == QTEPhase.WaitingForInput || nextPhase == QTEPhase.SuccessResult)
            && activeKind == QTEKind.FrontGrab && nativePair != null && TryGetComponent<NativeActionAudio>(out var actionAudio)
            && actionAudio.HasPlayerPairTrack(nativePair.PlayerClipName))
        {
            return;
        }

        switch (nextPhase)
        {
            case QTEPhase.Entering:
                // receiverValue70（判空前缓存的生效变体）：从生效变体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::QTEAnimationVariant receiverValue70 = activeVariant;
                // optionalValue71（条件访问得到的接收者值70的起始音频事件）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：作为 LeonQTECoordinator.PlayQTEOneShot(AudioEventData) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::AudioEventData optionalValue71;
                if (!global::System.Object.ReferenceEquals(receiverValue70, null))
                {
                    optionalValue71 = receiverValue70.startAudioEvent;
                }
                else
                {
                    optionalValue71 = default;
                }

                PlayQTEOneShot(optionalValue71);
                break;
            case QTEPhase.WaitingForInput:
                // receiverValue72（判空前缓存的生效变体）：从生效变体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::QTEAnimationVariant receiverValue72 = activeVariant;
                // optionalValue73（条件访问得到的接收者值72的挣脱音频事件）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：作为 LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::AudioEventData optionalValue73;
                if (!global::System.Object.ReferenceEquals(receiverValue72, null))
                {
                    optionalValue73 = receiverValue72.struggleAudioEvent;
                }
                else
                {
                    optionalValue73 = default;
                }

                if (optionalValue73 != null && !optionalValue73.loop)
                {
                    PlayQTEOneShot(optionalValue73);
                }
                else
                {
                    StartQTEStruggleLoop(optionalValue73);
                }
                break;
            case QTEPhase.SuccessResult:
                if (config != null && (activeKind == QTEKind.FrontGrab || activeKind == QTEKind.RearGrab))
                {
                    PlayQTEOneShot(config.grabEscapeAudio);
                }
                break;
            case QTEPhase.FailureResult:
                // receiverValue74（判空前缓存的生效变体）：从生效变体读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::QTEAnimationVariant receiverValue74 = activeVariant;
                // optionalValue75（条件访问得到的接收者值74的失败音频事件）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：作为 LeonQTECoordinator.PlayQTEOneShot(AudioEventData) 的参数、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。
global::AudioEventData optionalValue75;
                if (!global::System.Object.ReferenceEquals(receiverValue74, null))
                {
                    optionalValue75 = receiverValue74.failureAudioEvent;
                }
                else
                {
                    optionalValue75 = default;
                }

                PlayQTEOneShot(optionalValue75);
                break;
            case QTEPhase.Exiting:
            case QTEPhase.Completed:
            case QTEPhase.Cancelled:
                StopQTEStruggleLoop();
                break;
        }
    }

    /// <summary>PlayQTEOneShot 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、LeonQTECoordinator.ResolveQTEAudioPosition()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、LeonQTECoordinator.ResolveQTEAudioPosition()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">中文含义：事件配置。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、LeonQTECoordinator.PlayQTEOneShot(AudioEventData)。</param>
    private void PlayQTEOneShot(AudioEventData eventData)
    {
        if (eventData == null || AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.Play3D(eventData, ResolveQTEAudioPosition());
    }

    /// <summary>StartQTEStruggleLoop 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、LeonQTECoordinator.ResolveQTEAudioPosition()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、LeonQTECoordinator.ResolveQTEAudioPosition()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">中文含义：事件配置。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData)。</param>
    private void StartQTEStruggleLoop(AudioEventData eventData)
    {
        if (eventData == null || AudioManager.Instance == null || qteStruggleLoopHandle.IsValid)
        {
            return;
        }

        qteStruggleLoopHandle = AudioManager.Instance.Play3D(eventData, ResolveQTEAudioPosition());
    }

    /// <summary>StopQTEStruggleLoop 的业务处理入口；本方法直接依赖 AudioManager.Stop(AudioPlayHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.BeginSingleShotAssassinationResult()、LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.HandleAssassinationContact(int)、LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)；下游（含内部回调体） AudioManager.Stop(AudioPlayHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopQTEStruggleLoop()
    {
        if (qteStruggleLoopHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(qteStruggleLoopHandle);
        }

        qteStruggleLoopHandle = default;
    }

    /// <summary>UpdateQTEStruggleLoopPosition 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ResolveQTEAudioPosition()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） LeonQTECoordinator.ResolveQTEAudioPosition()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateQTEStruggleLoopPosition()
    {
        if (qteStruggleLoopHandle.IsValid && qteStruggleLoopHandle.source != null)
        {
            qteStruggleLoopHandle.source.transform.position = ResolveQTEAudioPosition();
        }
    }

    /// <summary>ResolveQTEAudioPosition 的业务处理入口；本方法直接依赖 Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.PlayQTEOneShot(AudioEventData)、LeonQTECoordinator.StartQTEStruggleLoop(AudioEventData)、LeonQTECoordinator.UpdateQTEStruggleLoopPosition()；下游（含内部回调体） Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector3 ResolveQTEAudioPosition()
    {
        if (participant == null || participant.Root == null)
        {
            return transform.position;
        }

        return Vector3.Lerp(transform.position, participant.Root.position, 0.5f);
    }

    /// <summary>PublishProgress 的业务处理入口；本方法直接依赖 Action&lt;QTEProgressInfo&gt;.Invoke(QTEProgressInfo)、object.ReferenceEquals(object, object)、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.SubmitEscapePress(int)、LeonQTECoordinator.Update()；下游（含内部回调体） Action&lt;QTEProgressInfo&gt;.Invoke(QTEProgressInfo)、object.ReferenceEquals(object, object)、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ReadQteInputDuration()
    {
        if (activeKind == QTEKind.RearGrab || activeKind == QTEKind.FrontGrab)
        {
            return config.inputDuration;
        }
        if (nativePair != null)
        {
            return (nativePair.Guard.finalFrame - nativePair.InputFrame) / nativePair.FrameRate;
        }
        return config.inputDuration;
    }

    private void PublishProgress()
    {
        // receiverValue76（判空前缓存的配对动作进度变化时的通知回调）：从配对动作进度变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.PublishProgress()。
global::System.Action<global::QTEProgressInfo> receiverValue76 = OnQTEProgressChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue76, null))
        {
            receiverValue76.Invoke(new QTEProgressInfo(activeSessionId, pressCount, config.requiredPresses, remainingTime, ReadQteInputDuration()));
        }
    }

    /// <summary>ResetPairFlags 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetPairFlags()
    {
        playerActionCompleted = false;
        enemyActionCompleted = false;
        playerExited = false;
        enemyExited = false;
    }

    /// <summary>MatchesCurrentPlayerAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HandlePlayerActionCompleted(AnimationActionTransaction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonQTECoordinator.MatchesCurrentPlayerAction(FullBodyActionType)。</param>
    private bool MatchesCurrentPlayerAction(FullBodyActionType action)
    {
        return action == expectedPlayerAction;
    }

    /// <summary>MatchesCurrentEnemyAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HandleParticipantActionCompleted(int, EnemyQTEAction, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonQTECoordinator.MatchesCurrentEnemyAction(EnemyQTEAction)。</param>
    private bool MatchesCurrentEnemyAction(EnemyQTEAction action)
    {
        return action == expectedEnemyAction;
    }

    /// <summary>ContainsCollider 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant)、LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="colliders">中文含义：碰撞体集合。使用位置：LeonQTECoordinator.ContainsCollider(Collider[], Collider)。</param>
    /// <param name="target">中文含义：目标碰撞体。使用位置：LeonQTECoordinator.ContainsCollider(Collider[], Collider)。</param>
    private static bool ContainsCollider(Collider[] colliders, Collider target)
    {
        if (colliders == null || target == null)
        {
            return false;
        }

        // collider（碰撞体）：每轮从碰撞体集合取得一个元素，仅代表当前这一项。使用位置：LeonQTECoordinator.ContainsCollider(Collider[], Collider)。
foreach (var collider in colliders)
        {
            if (collider == target)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 查询里昂身后或侧后方可执行的持刀暗杀目标。
    /// 该查询只读取敌人状态，不修改行为树状态。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、LeonQTECoordinator.TryStartNearestPrompt(out int)；下游（含内部回调体） LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.GetPromptCandidates()、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)、Vector3.Distance(Vector3, Vector3)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标索引敌人配对动作参与者。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。</param>
    /// <param name="kind">中文含义：种类。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。</param>
    public bool TryGetPromptCandidate(out IEnemyQTEParticipant target, out QTEKind kind)
    {
        if (UsesNativeInteractionPrompts)
        {
            RefreshInteractionSnapshots();
            kind = QTEKind.KnifeAssassination;
            target = null;
            if (interactSnapshot.Kind == InteractionPromptKind.Assassination && interactSnapshot.IsValid && interactSnapshot.CanExecute)
            {
                target = interactSnapshot.Target as IEnemyQTEParticipant;
            }
            return target != null;
        }
        target = null;
        kind = QTEKind.KnifeAssassination;
        if (!CanQueryAssassinationPrompt())
        {
            return false;
        }

        // candidates（候选集合）：保存 LeonQTECoordinator.GetPromptCandidates() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。
var candidates = GetPromptCandidates();
        // nearestDistance（最近距离）：使用位置：LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。
float nearestDistance = float.MaxValue;
        // candidateIndex（候选索引）：使用位置：作为 IReadOnlyList<VillagerQTEParticipant>.this[int] 的参数、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。
for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            // candidate（候选）：使用位置：作为 LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。
var candidate = candidates[candidateIndex];
            if (candidate == null || !candidate.IsAvailable)
            {
                continue;
            }

            if (!candidate.CanStart(controller, QTEKind.KnifeAssassination, out _))
            {
                continue;
            }

            if (!ValidatePairing(candidate, QTEKind.KnifeAssassination, out _))
            {
                continue;
            }

            // distance（距离）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)。
float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            target = candidate;
        }

        return target != null;
    }

    /// <summary>SetAssassinationKnifeVisualIfNeeded 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonWeaponVisualController.BeginAssassinationKnifePose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonWeaponVisualController.BeginAssassinationKnifePose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：LeonQTECoordinator.SetAssassinationKnifeVisualIfNeeded(QTEKind)。</param>
    private void SetAssassinationKnifeVisualIfNeeded(QTEKind kind)
    {
        if (kind != QTEKind.KnifeAssassination && kind != QTEKind.GroundFinisher)
        {
            return;
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        if (weaponVisualController != null)
        {
            weaponVisualController.SetKnifeAppearance(pairedKnife);
        }

        if (kind == QTEKind.GroundFinisher && nativePair != null && weaponVisualController != null
            && weaponVisualController.BeginNativePairKnife())
        {
            assassinationKnifeVisualActive = true;
            return;
        }

        // receiverValue77（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.SetAssassinationKnifeVisualIfNeeded(QTEKind)。
global::LeonWeaponVisualController receiverValue77 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue77, null))
        {
            receiverValue77.SetKnifeModeVisible(true);
        }

        // receiverValue78（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.SetAssassinationKnifeVisualIfNeeded(QTEKind)。
global::LeonWeaponVisualController receiverValue78 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue78, null))
        {
            receiverValue78.BeginAssassinationKnifePose();
        }

        assassinationKnifeVisualActive = true;
    }

    /// <summary>EndAssassinationKnifeVisualIfNeeded 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonWeaponVisualController.EndAssassinationKnifePose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.OnDisable()、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonWeaponVisualController.EndAssassinationKnifePose()、LeonWeaponVisualController.SetKnifeModeVisible(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EndAssassinationKnifeVisualIfNeeded()
    {
        if (weaponVisualController != null)
        {
            weaponVisualController.EndNativePairKnife();
        }
        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        // 无论是否仍标记为活动，都执行一次幂等恢复，覆盖中断、死亡和禁用路径。
        // receiverValue79（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()。
global::LeonWeaponVisualController receiverValue79 = // 无论是否仍标记为活动，都执行一次幂等恢复，覆盖中断、死亡和禁用路径。
 weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue79, null))
        {
            receiverValue79.EndAssassinationKnifePose();
        }

        if (!assassinationKnifeVisualActive)
        {
            return;
        }

        assassinationKnifeVisualActive = false;
        // receiverValue80（判空前缓存的武器视觉表现控制器）：从武器视觉表现控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.EndAssassinationKnifeVisualIfNeeded()。
global::LeonWeaponVisualController receiverValue80 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue80, null))
        {
            receiverValue80.SetKnifeModeVisible(false);
        }
    }

    /// <summary>TryGetBodyTechniquePromptCandidate 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.GetPromptCandidates()、Vector3.Distance(Vector3, Vector3)、VillagerQTEParticipant.CanStartBodyTechnique(LeonController, out int, out QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryGetPromptPresentation(out Transform, out string)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)；下游（含内部回调体） LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.GetPromptCandidates()、Vector3.Distance(Vector3, Vector3)、VillagerQTEParticipant.CanStartBodyTechnique(LeonController, out int, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标村民配对动作参与者。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。</param>
    /// <param name="techniqueIndex">中文含义：体术索引。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。</param>
    public bool TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant target, out int techniqueIndex)
    {
        if (UsesNativeInteractionPrompts)
        {
            RefreshInteractionSnapshots();
            target = null;
            techniqueIndex = 0;
            if (interactSnapshot.Kind == InteractionPromptKind.BodyTechnique && interactSnapshot.IsValid && interactSnapshot.CanExecute)
            {
                target = interactSnapshot.Target as VillagerQTEParticipant;
                techniqueIndex = interactSnapshot.Technique;
            }
            return target != null;
        }
        target = null;
        techniqueIndex = 0;
        if (!CanQueryBodyTechniquePrompt())
        {
            return false;
        }

        // candidates（候选集合）：保存 LeonQTECoordinator.GetPromptCandidates() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。
var candidates = GetPromptCandidates();
        // nearestDistance（最近距离）：使用位置：LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。
float nearestDistance = float.MaxValue;
        // candidateIndex（候选索引）：使用位置：作为 IReadOnlyList<VillagerQTEParticipant>.this[int] 的参数、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。
for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            // candidate（候选）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。
var candidate = candidates[candidateIndex];
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.CanStartBodyTechnique(controller, out int /* 中文含义：候选体术。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ candidateTechnique, out _))
            {
                continue;
            }

            // distance（距离）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)。
float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            target = candidate;
            techniqueIndex = candidateTechnique;
        }

        return target != null;
    }

    /// <summary>TryStartNearestBodyTechnique 的业务处理入口；本方法直接依赖 LeonCombat.TryStartQTEBodyTechnique(int, int)、LeonQTECoordinator.ClearBodyTechniqueSession()、LeonQTECoordinator.SetPromptAvailability(bool)、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)、VillagerQTEParticipant.CancelBodyTechnique(int)、VillagerQTEParticipant.TryReserveBodyTechnique(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleMelee()、LeonInputBridge.OnInteract(InputAction.CallbackContext)；下游（含内部回调体） LeonCombat.TryStartQTEBodyTechnique(int, int)、LeonQTECoordinator.ClearBodyTechniqueSession()、LeonQTECoordinator.SetPromptAvailability(bool)、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)、VillagerQTEParticipant.CancelBodyTechnique(int)、VillagerQTEParticipant.TryReserveBodyTechnique(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。输出参数，由本入口写入并交给调用方。使用位置：LeonQTECoordinator.TryStartNearestBodyTechnique(out int)。</param>
    public bool TryStartNearestBodyTechnique(out int sessionId)
    {
        sessionId = 0;
        if (!TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant /* 中文含义：目标村民配对动作参与者。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target, out int /* 中文含义：体术索引。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ techniqueIndex))
        {
            return false;
        }

        // newSessionId（新会话编号）：使用位置：作为 LeonCombat.TryStartQTEBodyTechnique(int, int) 的参数、作为 VillagerQTEParticipant.CancelBodyTechnique(int) 的参数、作为 VillagerQTEParticipant.TryReserveBodyTechnique(int) 的参数 等共 4 处。
        return TryStartBodyTechnique(target, techniqueIndex, out sessionId);
    }

    private bool TryStartBodyTechnique(VillagerQTEParticipant target, int techniqueIndex, out int sessionId)
    {
        sessionId = 0;
        if (!CanQueryBodyTechniquePrompt()
            || !target.CanStartBodyTechnique(controller, out int availableTechnique, out _)
            || techniqueIndex != availableTechnique)
        {
            return false;
        }

        int newSessionId = ++globalSessionSequence;
        if (newSessionId <= 0)
        {
            globalSessionSequence = 1;
            newSessionId = 1;
        }

        if (!target.TryReserveBodyTechnique(newSessionId))
        {
            return false;
        }

        globalOwner = this;
        activeBodyTechniqueTarget = target;
        activeBodyTechniqueSessionId = newSessionId;
        if (!combat.TryStartQTEBodyTechnique(techniqueIndex, newSessionId, target))
        {
            target.CancelBodyTechnique(newSessionId);
            ClearBodyTechniqueSession();
            return false;
        }

        SetPromptAvailability(false);
        sessionId = newSessionId;
        return true;
    }

    /// <summary>TryStartNearestPrompt 的业务处理入口；本方法直接依赖 LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleMelee()、LeonInputBridge.OnInteract(InputAction.CallbackContext)；下游（含内部回调体） LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。输出参数，由本入口写入并交给调用方。使用位置：作为 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int) 的参数、LeonQTECoordinator.TryStartNearestPrompt(out int)。</param>
    public bool TryStartNearestPrompt(out int sessionId)
    {
        sessionId = 0;
        if (!TryGetPromptCandidate(out IEnemyQTEParticipant /* 中文含义：目标索引敌人配对动作参与者。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ target, out QTEKind /* 中文含义：种类。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ kind))
        {
            return false;
        }

        return TryStart(kind, target, out sessionId);
    }

    /// <summary>HandleBodyTechniqueHit 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ClearBodyTechniqueSession()、VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.ClearBodyTechniqueSession()、VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：作为 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform) 的参数、LeonQTECoordinator.HandleBodyTechniqueHit(int, FullBodyActionType)。</param>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 VillagerQTEParticipant.ApplyBodyTechniqueHit(int, FullBodyActionType, Transform) 的参数。</param>
    private void HandleBodyTechniqueHit(int sessionId, FullBodyActionType actionType)
    {
        if (sessionId == 0 || sessionId != activeBodyTechniqueSessionId || activeBodyTechniqueTarget == null)
        {
            return;
        }

        if (activeBodyTechniqueTarget.ApplyBodyTechniqueHit(sessionId, actionType, transform, out Vector3 contact))
        {
            VillageLeonDialogue.Report(controller, VillageSpeechCue.Kick);
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.Play3D(AudioEventNames.MeleeHit, contact);
                // 以主目标的致死结算触发原版重击音层，每个会话只播一次。
                if (!activeBodyTechniqueTarget.IsAlive && audio.library != null
                    && audio.library.HasEvent(AudioEventNames.MeleeHitCritical))
                {
                    audio.Play3D(AudioEventNames.MeleeHitCritical, contact);
                    audio.Play3D(AudioEventNames.MeleeHitCriticalCrack, contact);
                    audio.Play3D(AudioEventNames.MeleeHitCriticalTail, contact);
                }
                else if (audio.library != null && audio.library.HasEvent(AudioEventNames.MeleeHitNormal))
                {
                    audio.Play3D(AudioEventNames.MeleeHitNormal, contact);
                }
            }
        }
        else
        {
            activeBodyTechniqueTarget.CancelBodyTechnique(sessionId);
        }
        ClearBodyTechniqueSession();
    }

    /// <summary>HandleMeleeActionExited 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CancelBodyTechniqueIfActive()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.CancelBodyTechniqueIfActive()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonQTECoordinator.HandleMeleeActionExited(AnimationActionTransaction)。</param>
    private void HandleMeleeActionExited(AnimationActionTransaction transaction)
    {
        if (transaction.qteSessionId == activeBodyTechniqueSessionId)
        {
            CancelBodyTechniqueIfActive();
        }
    }

    /// <summary>HandleMeleeActionInterrupted 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CancelBodyTechniqueIfActive()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.CancelBodyTechniqueIfActive()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transaction">中文含义：事务。使用位置：LeonQTECoordinator.HandleMeleeActionInterrupted(AnimationActionTransaction)。</param>
    private void HandleMeleeActionInterrupted(AnimationActionTransaction transaction)
    {
        if (transaction.qteSessionId == activeBodyTechniqueSessionId)
        {
            CancelBodyTechniqueIfActive();
        }
    }

    /// <summary>CancelBodyTechniqueIfActive 的业务处理入口；本方法直接依赖 LeonQTECoordinator.ClearBodyTechniqueSession()、object.ReferenceEquals(object, object)、VillagerQTEParticipant.CancelBodyTechnique(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HandleMeleeActionExited(AnimationActionTransaction)、LeonQTECoordinator.HandleMeleeActionInterrupted(AnimationActionTransaction)、LeonQTECoordinator.OnDisable()；下游（含内部回调体） LeonQTECoordinator.ClearBodyTechniqueSession()、object.ReferenceEquals(object, object)、VillagerQTEParticipant.CancelBodyTechnique(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CancelBodyTechniqueIfActive()
    {
        if (activeBodyTechniqueSessionId == 0)
        {
            return;
        }

        // receiverValue81（判空前缓存的生效体术目标）：从生效体术目标读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.CancelBodyTechniqueIfActive()。
global::VillagerQTEParticipant receiverValue81 = activeBodyTechniqueTarget;
        if (!global::System.Object.ReferenceEquals(receiverValue81, null))
        {
            receiverValue81.CancelBodyTechnique(activeBodyTechniqueSessionId);
        }

        ClearBodyTechniqueSession();
    }

    /// <summary>ClearBodyTechniqueSession 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CancelBodyTechniqueIfActive()、LeonQTECoordinator.HandleBodyTechniqueHit(int, FullBodyActionType)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearBodyTechniqueSession()
    {
        activeBodyTechniqueTarget = null;
        activeBodyTechniqueSessionId = 0;
        if (globalOwner == this && !IsActive)
        {
            globalOwner = null;
        }

        nextPromptScanTime = 0f;
    }

    /// <summary>UpdatePromptAvailability 的业务处理入口；本方法直接依赖 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.SetPromptAvailability(bool)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Update()；下游（含内部回调体） LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.SetPromptAvailability(bool)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdatePromptAvailability()
    {
        if (IsActive || activeBodyTechniqueSessionId != 0 || globalOwner != null || Time.timeScale <= 0f)
        {
            bodyTechniquePromptAvailable = false;
            nextPromptScanTime = 0f;
            SetPromptAvailability(false);
            return;
        }

        if (TryGetBodyTechniquePromptCandidate(out _, out _))
        {
            bodyTechniquePromptAvailable = true;
            SetPromptAvailability(true);
            return;
        }

        if (bodyTechniquePromptAvailable)
        {
            bodyTechniquePromptAvailable = false;
            nextPromptScanTime = 0f;
        }

        if (Time.time < nextPromptScanTime)
        {
            return;
        }

        nextPromptScanTime = Time.time + Mathf.Max(0.02f, promptScanInterval);
        SetPromptAvailability(HasAnyPromptCandidate());
    }

    /// <summary>HasAnyPromptCandidate 的业务处理入口；本方法直接依赖 LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.GetPromptCandidates()、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanStartBodyTechnique(LeonController, out int, out QTECancelReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.UpdatePromptAvailability()；下游（含内部回调体） LeonQTECoordinator.CanQueryAssassinationPrompt()、LeonQTECoordinator.CanQueryBodyTechniquePrompt()、LeonQTECoordinator.GetPromptCandidates()、LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanStart(LeonController, QTEKind, out QTECancelReason)、VillagerQTEParticipant.CanStartBodyTechnique(LeonController, out int, out QTECancelReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool HasAnyPromptCandidate()
    {
        // canQueryBodyTechnique（是否可以查询体术）：保存 LeonQTECoordinator.CanQueryBodyTechniquePrompt() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.HasAnyPromptCandidate()。
bool canQueryBodyTechnique = CanQueryBodyTechniquePrompt();
        // canQueryAssassination（是否可以查询暗杀）：保存 LeonQTECoordinator.CanQueryAssassinationPrompt() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.HasAnyPromptCandidate()。
bool canQueryAssassination = CanQueryAssassinationPrompt();
        if (!canQueryBodyTechnique && !canQueryAssassination)
        {
            return false;
        }

        // candidates（候选集合）：保存 LeonQTECoordinator.GetPromptCandidates() 的返回结果，供当前步骤使用。使用位置：LeonQTECoordinator.HasAnyPromptCandidate()。
var candidates = GetPromptCandidates();
        // candidateIndex（候选索引）：使用位置：作为 IReadOnlyList<VillagerQTEParticipant>.this[int] 的参数、LeonQTECoordinator.HasAnyPromptCandidate()。
for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            // candidate（候选）：使用位置：作为 LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason) 的参数、LeonQTECoordinator.HasAnyPromptCandidate()。
var candidate = candidates[candidateIndex];
            if (candidate == null)
            {
                continue;
            }

            if (canQueryBodyTechnique && candidate.CanStartBodyTechnique(controller, out _, out _))
            {
                return true;
            }

            if (canQueryAssassination && candidate.IsAvailable && candidate.CanStart(controller, QTEKind.KnifeAssassination, out _) && ValidatePairing(candidate, QTEKind.KnifeAssassination, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>CanQueryAssassinationPrompt 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasUsableKnife()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetPromptCandidate(out IEnemyQTEParticipant, out QTEKind)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasUsableKnife()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanQueryAssassinationPrompt()
    {
        // weaponHandler（管理当前装备与武器切换的组件）：使用位置：LeonQTECoordinator.CanQueryAssassinationPrompt()。
LeonWeaponHandler weaponHandler;
        if (controller != null)
        {
            weaponHandler = controller.GetComponent<LeonWeaponHandler>();
        }
        else
        {
            weaponHandler = null;
        }

        return started && !IsActive && globalOwner == null && config != null && controller != null && animationCoordinator != null && !animationCoordinator.HasActiveFullBodyAction && controller.currentState != LeonState.Dead && controller.currentState != LeonState.Staggered && controller.currentState != LeonState.QTE && controller.currentState != LeonState.MeleeAttacking && weaponHandler != null && weaponHandler.HasUsableKnife();
    }

    /// <summary>ConsumeAssassinationKnifeDurability 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()、InventoryManager.TryConsumeDurability(string, int, out bool)、LeonWeaponHandler.HandleKnifeBroken()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <summary>CanQueryBodyTechniquePrompt 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.HasAnyPromptCandidate()、LeonQTECoordinator.TryGetBodyTechniquePromptCandidate(out VillagerQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanQueryBodyTechniquePrompt()
    {
        return started && !IsActive && globalOwner == null && combat != null && (!combat.IsKnifeMode || combat.IsHoldingTemporaryKnife) && animationCoordinator != null && (!animationCoordinator.HasActiveFullBodyAction || combat.knifeModule != null && combat.knifeModule.CanConnectMeleeFinisher) && controller != null && controller.currentState != LeonState.Dead && controller.currentState != LeonState.Staggered && controller.currentState != LeonState.QTE && controller.currentState != LeonState.MeleeAttacking;
    }

    /// <summary>SetPromptAvailability 的业务处理入口；本方法直接依赖 Action&lt;bool&gt;.Invoke(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.OnDisable()、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)、LeonQTECoordinator.UpdatePromptAvailability()；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="available">中文含义：是否可用。使用位置：作为 Action&lt;bool&gt;.Invoke(bool) 的参数、LeonQTECoordinator.SetPromptAvailability(bool)。</param>
    private void SetPromptAvailability(bool available)
    {
        if (promptAvailable == available)
        {
            return;
        }

        promptAvailable = available;
        // receiverValue85（判空前缓存的配对动作提示可用性变化时的通知回调）：从配对动作提示可用性变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonQTECoordinator.SetPromptAvailability(bool)。
global::System.Action<bool> receiverValue85 = OnQTEPromptAvailabilityChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue85, null))
        {
            receiverValue85.Invoke(available);
        }
    }

    /// <summary>Reject 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reason">中文含义：原因。使用位置：LeonQTECoordinator.Reject(QTECancelReason)。</param>
    private bool Reject(QTECancelReason reason)
    {
        lastRejectionReason = reason;
        return false;
    }
}
