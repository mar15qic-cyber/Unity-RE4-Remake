using UnityEngine;
using GameArchitecture;
using System;
using System.Collections.Generic;

/// <summary>
/// 视觉标记类型
/// </summary>
public enum VisualMarker
{
    /// <summary>中文含义：收起视觉表现提交。</summary>
    HolsterVisualCommit,
    /// <summary>中文含义：装备视觉表现显露。</summary>
    EquipVisualReveal,
    /// <summary>中文含义：步枪后方隐藏之前显露。</summary>
    RifleBackHideBeforeReveal,
    /// <summary>中文含义：长枪支撑手部握持。</summary>
    LongGunSupportHandGrip,
    /// <summary>中文含义：弹匣脱离。</summary>
    MagazineDetached,
    /// <summary>中文含义：弹匣附着。</summary>
    MagazineAttached,
    /// <summary>中文含义：左手已释放。</summary>
    LeftHandReleased,
    /// <summary>中文含义：左手握持。</summary>
    LeftHandGripped,
    ReloadMechanism,
    EquipSettled
}

/// <summary>
/// 武器事务阶段
/// </summary>
public enum WeaponTransactionPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：等待进入。</summary>
    WaitingForEnter,
    /// <summary>中文含义：播放中换弹。</summary>
    PlayingReload,
    /// <summary>中文含义：等待换弹提交。</summary>
    AwaitingReloadCommit,
    /// <summary>中文含义：等待换弹退出。</summary>
    WaitingForReloadExit,
    /// <summary>中文含义：播放中收起。</summary>
    PlayingHolster,
    /// <summary>中文含义：等待武器提交。</summary>
    AwaitingWeaponCommit,
    /// <summary>中文含义：等待收起退出。</summary>
    WaitingForHolsterExit,
    /// <summary>中文含义：播放中装备。</summary>
    PlayingEquip,
    /// <summary>中文含义：等待装备退出。</summary>
    WaitingForEquipExit,
    /// <summary>中文含义：播放中小刀过渡。</summary>
    PlayingKnifeTransition,
    /// <summary>中文含义：等待小刀过渡退出。</summary>
    WaitingForKnifeTransitionExit,
    /// <summary>中文含义：等待小刀隐藏提交。</summary>
    AwaitingKnifeHideCommit,
    /// <summary>中文含义：已完成。</summary>
    Completed,
    /// <summary>中文含义：已取消。</summary>
    Cancelled,
    /// <summary>中文含义：过渡中收起目标装备。</summary>
    TransitioningHolsterToEquip,
    /// <summary>中文含义：过渡中收起目标小刀取出。</summary>
    TransitioningHolsterToKnifeTake,
    /// <summary>中文含义：过渡中小刀隐藏目标装备。</summary>
    TransitioningKnifeHideToEquip
}

/// <summary>
/// 全身事务阶段。快速反应事件的四个动画状态共享同一事务编号。
/// </summary>
public enum FullBodyTransactionPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：等待进入。</summary>
    WaitingForEnter,
    /// <summary>中文含义：播放中命中。</summary>
    PlayingHit,
    /// <summary>中文含义：等待命中退出。</summary>
    WaitingForHitExit,
    /// <summary>中文含义：播放中配对动作进入。</summary>
    PlayingQTEEnter,
    /// <summary>中文含义：播放中配对动作保持。</summary>
    PlayingQTEHold,
    /// <summary>中文含义：播放中配对动作成功。</summary>
    PlayingQTESuccess,
    /// <summary>中文含义：播放中配对动作失败。</summary>
    PlayingQTEFail,
    /// <summary>中文含义：等待配对动作退出。</summary>
    WaitingForQTEExit,
    /// <summary>中文含义：播放中近战。</summary>
    PlayingMelee,
    /// <summary>中文含义：等待近战退出。</summary>
    WaitingForMeleeExit,
    /// <summary>中文含义：播放中通行。</summary>
    PlayingTraversal,
    /// <summary>中文含义：等待通行退出。</summary>
    WaitingForTraversalExit,
    /// <summary>中文含义：已完成。</summary>
    Completed,
    /// <summary>中文含义：已取消。</summary>
    Cancelled
}

/// <summary>
/// 动画动作事务信息
/// </summary>
public struct AnimationActionTransaction
{
    /// <summary>中文含义：事务编号。使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 Debug.LogError(object) 的参数、作为 LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 的参数 等共 49 处。</summary>
    public int transactionId;
    /// <summary>中文含义：武器动作类型。使用位置：作为 Debug.LogError(object) 的参数、作为 LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase) 的参数、作为 LeonAnimatorDriver.BeginWeaponAction(WeaponActionType) 的参数 等共 20 处。</summary>
    public WeaponActionType weaponActionType;
    /// <summary>中文含义：全身动作类型。使用位置：作为 Action&lt;int, FullBodyActionType&gt;.Invoke(int, FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType) 的参数 等共 33 处。</summary>
    public FullBodyActionType fullBodyActionType;
    /// <summary>中文含义：来源武器类型。使用位置：作为 LeonAnimRig.InterruptWeaponActionIK(WeaponType) 的参数、作为 LeonWeaponHandler.CancelReload(WeaponType) 的参数、LeonAnimationActionCoordinator.GetCurrentWeaponForIK() 等共 6 处。</summary>
    public WeaponType sourceWeaponType;
    /// <summary>中文含义：待处理武器类型。使用位置：作为 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的参数、作为 LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType) 的参数、作为 LeonAnimRig.BeginEquipIK(WeaponType) 的参数 等共 14 处。</summary>
    public WeaponType pendingWeaponType;
    /// <summary>中文含义：是否武器切换。使用位置：LeonAnimationActionCoordinator.GetCurrentWeaponForIK()、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody() 等共 11 处。</summary>
    public bool isWeaponSwitch;
    /// <summary>中文含义：是否已取消。使用位置：LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 等共 27 处。</summary>
    public bool cancelled;
    /// <summary>中文含义：是否已完成。使用位置：LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float) 等共 13 处。</summary>
    public bool completed;
    /// <summary>中文含义：状态进入序号。使用位置：作为 Debug.LogError(object) 的参数、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float) 等共 17 处。</summary>
    public int stateEntrySequence;
    /// <summary>中文含义：入口是否已绑定。使用位置：LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float) 等共 23 处。</summary>
    public bool entryBound;
    /// <summary>中文含义：阶段。使用位置：作为 LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 等共 18 处。</summary>
    public WeaponTransactionPhase phase;
    /// <summary>中文含义：是否视觉表现提交完成。使用位置：LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int) 等共 7 处。</summary>
    public bool visualCommitDone;
    /// <summary>中文含义：收起进入序号。使用位置：LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。</summary>
    public int holsterEntrySequence;
    /// <summary>中文含义：配对动作会话编号。使用位置：作为 Action&lt;int, FullBodyActionType&gt;.Invoke(int, FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int) 的参数、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 等共 18 处。</summary>
    public int qteSessionId;
    /// <summary>中文含义：全身阶段。使用位置：LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.CancelQTE(int) 等共 20 处。</summary>
    public FullBodyTransactionPhase fullBodyPhase;
    /// <summary>中文含义：退出中的全身动作类型。使用位置：LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool) 等共 7 处。</summary>
    public FullBodyActionType outgoingFullBodyActionType;
    /// <summary>中文含义：退出中的状态进入序号。使用位置：LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool) 等共 7 处。</summary>
    public int outgoingStateEntrySequence;
    /// <summary>中文含义：是否计划的退出。使用位置：LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int) 等共 4 处。</summary>
    public bool plannedExit;
}

/// <summary>
/// 里昂动画动作事务协调者。
/// 订阅状态行为通知，管理动作事务生命周期。
/// 不直接操作 Animator，必须通过 LeonAnimatorDriver。
/// </summary>
/// <remarks>玩家武器通道与全身通道的事务拥有者，审批动作冲突并分配事务编号；动画标记必须匹配当前事务，取消与完成沿同一资源交接链执行。</remarks>
public partial class LeonAnimationActionCoordinator : MonoBehaviour
{
    /// <summary>中文含义：里昂动画控制器驱动器。使用位置：LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType) 等共 34 处。</summary>
    [SerializeField]
    private LeonAnimatorDriver driver;
    /// <summary>中文含义：动画控制器。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 等共 7 处。</summary>
    [SerializeField]
    private Animator animator;
    /// <summary>中文含义：当前里昂动画绑定组件。使用位置：作为 LeonAnimRig.ExitHitIK(WeaponType) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int) 等共 22 处。</summary>
    [SerializeField]
    private LeonAnimRig animRig;
    /// <summary>中文含义：动作运动执行器。使用位置：作为 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion) 的参数、作为 LeonActionMotionDriver.StopHorizontalSpeed(MotionControlToken) 的参数、LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion) 等共 11 处。</summary>
    [SerializeField]
    private LeonActionMotionDriver actionMotionDriver;
    // 分离的事务存储
    /// <summary>中文含义：武器动作事务。使用位置：作为 Action&lt;AnimationActionTransaction, float&gt;.Invoke(AnimationActionTransaction, float) 的参数、作为 Action&lt;AnimationActionTransaction, VisualMarker&gt;.Invoke(AnimationActionTransaction, VisualMarker) 的参数、作为 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction) 的参数 等共 32 处。</summary>
    private AnimationActionTransaction weaponTx;
    /// <summary>中文含义：全身动作事务。使用位置：作为 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction) 的参数、作为 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType) 的参数 等共 39 处。</summary>
    private AnimationActionTransaction fullBodyTx;
    /// <summary>中文含义：是否具有生效武器动作事务。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int) 等共 21 处。</summary>
    private bool hasActiveWeaponTx;
    /// <summary>中文含义：是否具有生效全身动作事务。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数、LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType) 等共 31 处。</summary>
    private bool hasActiveFullBodyTx;
    /// <summary>中文含义：是否等待武器退出。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType) 等共 8 处。</summary>
    private bool waitingForWeaponExit;
    /// <summary>中文含义：是否存在待处理的武器切换。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int) 等共 12 处。</summary>
    private bool hasPendingWeaponSwitch;
    /// <summary>中文含义：待处理武器类型。使用位置：作为 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的参数、作为 LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType) 的参数、作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数 等共 8 处。</summary>
    private WeaponType pendingWeaponType;
    /// <summary>中文含义：下一次动作事务编号。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection) 等共 9 处。</summary>
    private int nextTxId = 1;
    /// <summary>中文含义：是否已经死亡。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数、LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType) 等共 28 处。</summary>
    private bool isDead;
    /// <summary>中文含义：是否死亡事件已发送。使用位置：LeonAnimationActionCoordinator.RequestDeath()。</summary>
    private bool deathEventSent;
    /// <summary>中文含义：是否死亡动画完成已发送。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.RequestDeath()。</summary>
    private bool deathAnimationCompletionSent;
    /// <summary>中文含义：死亡状态进入序号。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.OnDisable() 等共 4 处。</summary>
    private int deathStateEntrySequence;
    /// <summary>中文含义：配对动作武器已隐藏会话编号。使用位置：LeonAnimationActionCoordinator.RequestQTE(int)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)。</summary>
    private int qteWeaponHiddenSessionId = -1;
    /// <summary>中文含义：配对动作动画覆盖。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.TryClearQTEAnimationVariantAfterFullBodyExit()。</summary>
    private readonly QTEAnimatorOverrideScope qteAnimationOverride = new QTEAnimatorOverrideScope();
    /// <summary>中文含义：配对动作动画覆盖清除是否仍待处理。使用位置：LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)、LeonAnimationActionCoordinator.ClearQTEAnimationVariant()、LeonAnimationActionCoordinator.OnDisable() 等共 4 处。</summary>
    private bool qteAnimationOverrideClearPending;
    // 视觉标记去重（按事务）
    /// <summary>中文含义：视觉表现标记动作事务编号。使用位置：LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.ResetVisualMarkers(int)。</summary>
    private int visualMarkerTxId = -1;
    /// <summary>中文含义：已处理标记集合。使用位置：LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonAnimationActionCoordinator.OnDisable() 等共 4 处。</summary>
    private HashSet<VisualMarker> processedMarkers = new HashSet<VisualMarker>();
    // 驱动器验证缓存
    /// <summary>中文含义：是否驱动器检查完成。使用位置：LeonAnimationActionCoordinator.Start()、LeonAnimationActionCoordinator.ValidateDriver()。</summary>
    [NonSerialized]
    private bool driverCheckDone;
    /// <summary>中文含义：是否驱动器检查结果。使用位置：LeonAnimationActionCoordinator.ValidateDriver()。</summary>
    [NonSerialized]
    private bool driverCheckResult;
    // ================================================================
    // 事件
    // ================================================================
    /// <summary>中文含义：换弹动画已完成时的通知回调。事件通信成员 OnReloadAnimationCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)、LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction> OnReloadAnimationCompleted;
    /// <summary>中文含义：收起动画已完成时的通知回调。事件通信成员 OnHolsterAnimationCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.UnbindCoordinatorEvents() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnHolsterAnimationCompleted;
    /// <summary>中文含义：装备动画已完成时的通知回调。事件通信成员 OnEquipAnimationCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.UnbindCoordinatorEvents() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnEquipAnimationCompleted;
    /// <summary>中文含义：武器动作已中断时的通知回调。事件通信成员 OnWeaponActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int) 等共 12 处。</summary>
    public event Action<AnimationActionTransaction> OnWeaponActionInterrupted;
    /// <summary>中文含义：武器动作进度时的通知回调。事件通信成员 OnWeaponActionProgress，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)、LeonWeaponVisualController.BindCoordinatorEvents()、LeonWeaponVisualController.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction, float> OnWeaponActionProgress;
    /// <summary>中文含义：小刀过渡已退出时的通知回调。事件通信成员 OnKnifeTransitionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction> OnKnifeTransitionExited;

    internal bool IsKnifeEquipWaitingForReveal => hasActiveWeaponTx && weaponTx.isWeaponSwitch
        && weaponTx.sourceWeaponType == WeaponType.Knife && weaponTx.weaponActionType == WeaponActionType.Equip
        && (visualMarkerTxId != weaponTx.transactionId || !processedMarkers.Contains(VisualMarker.EquipVisualReveal));
    /// <summary>中文含义：小刀过渡已中断时的通知回调。事件通信成员 OnKnifeTransitionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int)。</summary>
    public event Action<AnimationActionTransaction> OnKnifeTransitionInterrupted;
    /// <summary>中文含义：命中动画已完成时的通知回调。事件通信成员 OnHitAnimationCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction> OnHitAnimationCompleted;
    /// <summary>中文含义：全身动作中断时的通知回调。事件通信成员 OnFullBodyActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.RequestDeath() 等共 4 处。</summary>
    public event Action<AnimationActionTransaction> OnFullBodyActionInterrupted;
    /// <summary>中文含义：配对动作动作进入时的通知回调。事件通信成员 OnQTEActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。</summary>
    public event Action<AnimationActionTransaction> OnQTEActionEntered;
    /// <summary>中文含义：配对动作动作完成时的通知回调。事件通信成员 OnQTEActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonQTECoordinator.BindEvents()、LeonQTECoordinator.UnbindEvents()。</summary>
    public event Action<AnimationActionTransaction> OnQTEActionCompleted;
    /// <summary>中文含义：配对动作动作退出时的通知回调。事件通信成员 OnQTEActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonQTECoordinator.BindEvents() 等共 4 处。</summary>
    public event Action<AnimationActionTransaction> OnQTEActionExited;
    /// <summary>中文含义：配对动作动作中断时的通知回调。事件通信成员 OnQTEActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.CancelQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.RequestDeath() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnQTEActionInterrupted;
    /// <summary>中文含义：近战动作已进入时的通知回调。事件通信成员 OnMeleeActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。</summary>
    public event Action<AnimationActionTransaction> OnMeleeActionEntered;
    /// <summary>中文含义：近战动作已完成时的通知回调。事件通信成员 OnMeleeActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction> OnMeleeActionCompleted;
    /// <summary>中文含义：近战动作已退出时的通知回调。事件通信成员 OnMeleeActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnMeleeActionExited;
    /// <summary>中文含义：近战动作已中断时的通知回调。事件通信成员 OnMeleeActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：KnifeCombatModule.BindingEvent()、KnifeCombatModule.OnDisable()、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int) 等共 8 处。</summary>
    public event Action<AnimationActionTransaction> OnMeleeActionInterrupted;
    /// <summary>中文含义：近战命中窗口时的通知回调。事件通信成员 OnMeleeHitWindow，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleMeleeHitWindow(Animator, FullBodyActionType)、LeonCombat.BindCoordinatorEvents()、LeonCombat.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction> OnMeleeHitWindow;
    /// <summary>中文含义：通行动作进入时的通知回调。事件通信成员 OnTraversalActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)、LeonWindowTraversal.BindEvents()、LeonWindowTraversal.UnbindEvents()。</summary>
    public event Action<AnimationActionTransaction> OnTraversalActionEntered;
    /// <summary>中文含义：通行动作完成时的通知回调。事件通信成员 OnTraversalActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonHighDropLanding.BindEvents()、LeonHighDropLanding.UnbindEvents() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnTraversalActionCompleted;
    /// <summary>中文含义：通行动作退出时的通知回调。事件通信成员 OnTraversalActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonHighDropLanding.BindEvents()、LeonHighDropLanding.UnbindEvents() 等共 5 处。</summary>
    public event Action<AnimationActionTransaction> OnTraversalActionExited;
    /// <summary>中文含义：通行动作中断时的通知回调。事件通信成员 OnTraversalActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.RequestDeath() 等共 7 处。</summary>
    public event Action<AnimationActionTransaction> OnTraversalActionInterrupted;
    /// <summary>中文含义：配对动作武器可见性请求的时的通知回调。事件通信成员 OnQTEWeaponVisibilityRequested，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.RequestQTE(int)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonWeaponVisualController.BindCoordinatorEvents() 等共 4 处。</summary>
    public event Action<int, bool> OnQTEWeaponVisibilityRequested;
    /// <summary>中文含义：死亡已进入时的通知回调。事件通信成员 OnDeathEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：KnifeCombatModule.BindingEvent()、KnifeCombatModule.OnDisable()、LeonAnimationActionCoordinator.RequestDeath() 等共 5 处。</summary>
    public event Action OnDeathEntered;
    /// <summary>中文含义：死亡动画已完成时的通知回调。事件通信成员 OnDeathAnimationCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonController.InitPipeline()、LeonController.OnDestroy()。</summary>
    public event Action OnDeathAnimationCompleted;
    /// <summary>中文含义：视觉表现标记时的通知回调。事件通信成员 OnVisualMarker，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonWeaponVisualController.BindCoordinatorEvents()、LeonWeaponVisualController.UnbindCoordinatorEvents()。</summary>
    public event Action<AnimationActionTransaction, VisualMarker> OnVisualMarker;
    /// <summary>中文含义：换弹已退出时的通知回调。换弹状态完全退出到Empty后触发</summary>
    public event Action<AnimationActionTransaction> OnReloadExited;
    // ================================================================
    // 生命周期
    // ================================================================
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonActionMotionDriver&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (driver == null)
        {
            driver = GetComponent<LeonAnimatorDriver>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (actionMotionDriver == null)
        {
            actionMotionDriver = GetComponent<LeonActionMotionDriver>();
        }

        driverCheckDone = false;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        playerLeaseVersion++;
        WeaponActionSMB.OnActionEntered += HandleWeaponEntered;
        WeaponActionSMB.OnActionProgress += HandleWeaponProgress;
        WeaponActionSMB.OnActionCompleted += HandleWeaponCompleted;
        WeaponActionSMB.OnActionInterrupted += HandleWeaponInterrupted;
        WeaponActionSMB.OnActionExited += HandleWeaponExited;
        FullBodyActionSMB.OnActionEntered += HandleFullBodyEntered;
        FullBodyActionSMB.OnActionCompleted += HandleFullBodyCompleted;
        FullBodyActionSMB.OnActionInterrupted += HandleFullBodyInterrupted;
        FullBodyActionSMB.OnActionExited += HandleFullBodyExited;
        MeleeDamageSMB.OnHitWindow += HandleMeleeHitWindow;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、HashSet&lt;VisualMarker&gt;.Clear()、LeonActionMotionDriver.CancelAllMotion()、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.ClearPendingInjuredTransition()、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        WeaponActionSMB.OnActionEntered -= HandleWeaponEntered;
        WeaponActionSMB.OnActionProgress -= HandleWeaponProgress;
        WeaponActionSMB.OnActionCompleted -= HandleWeaponCompleted;
        WeaponActionSMB.OnActionInterrupted -= HandleWeaponInterrupted;
        WeaponActionSMB.OnActionExited -= HandleWeaponExited;
        FullBodyActionSMB.OnActionEntered -= HandleFullBodyEntered;
        FullBodyActionSMB.OnActionCompleted -= HandleFullBodyCompleted;
        FullBodyActionSMB.OnActionInterrupted -= HandleFullBodyInterrupted;
        FullBodyActionSMB.OnActionExited -= HandleFullBodyExited;
        MeleeDamageSMB.OnHitWindow -= HandleMeleeHitWindow;
        // 完全清理，不继承旧锁
        if (hasActiveWeaponTx)
        {
            weaponTx.cancelled = true;
            weaponTx.phase = WeaponTransactionPhase.Cancelled;
            // receiverValue1（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::System.Action<global::AnimationActionTransaction> receiverValue1 = OnWeaponActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.Invoke(weaponTx);
            }
        }

        hasActiveWeaponTx = false;
        waitingForWeaponExit = false;
        hasPendingWeaponSwitch = false;
        if (hasActiveFullBodyTx)
        {
            fullBodyTx.cancelled = true;
            if (fullBodyTx.qteSessionId > 0)
            {
                RestoreQTEWeaponVisual(fullBodyTx.qteSessionId);
            }
        }

        hasActiveFullBodyTx = false;
        // receiverValue2（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonAnimatorDriver receiverValue2 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.EndFullBodyAction();
        }

        // receiverValue3（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonAnimatorDriver receiverValue3 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.FinalizeFullBodyActionLayer();
        }

        // 管线失效时清除待处理受伤目标，避免永久挂起
        // receiverValue4（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonAnimatorDriver receiverValue4 = // 管线失效时清除待处理受伤目标，避免永久挂起
 driver;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.ClearPendingInjuredTransition();
        }

        // receiverValue5（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonActionMotionDriver receiverValue5 = actionMotionDriver;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.CancelAllMotion();
        }

        // 协调器禁用时立即中断所有反向动力学事务，避免重新启用后继承旧权重。
        // receiverValue6（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonAnimRig receiverValue6 = // 协调器禁用时立即中断所有反向动力学事务，避免重新启用后继承旧权重。
 animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.ForceIKOff();
        }

        qteAnimationOverrideClearPending = false;
        qteAnimationOverride.Clear();
        deathStateEntrySequence = 0;
        deathAnimationCompletionSent = false;
        weaponTx = default;
        fullBodyTx = default;
        if (!isDead && playerFullBodyAction != PlayerFullBodyPhase.EndEnd)
        {
            playerWeaponAction = PlayerWeaponPhase.None;
            playerFullBodyAction = PlayerFullBodyPhase.None;
            // receiverValue7（判空前缓存的玩家移动）：从玩家移动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.OnDisable()。
global::LeonLocomotion receiverValue7 = playerMovement;
            if (!global::System.Object.ReferenceEquals(receiverValue7, null))
            {
                receiverValue7.CommitMovementPhase(LeonState.Idle);
            }
        }

        processedMarkers.Clear();
        visualMarkerTxId = -1;
    }

    // ================================================================
    // 验证
    // ================================================================
    /// <summary>IsFromThisAnimator 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleMeleeHitWindow(Animator, FullBodyActionType) 等共 11 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画控制器。使用位置：LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)。</param>
    private bool IsFromThisAnimator(Animator source) => source != null && source == animator;
    /// <summary>ValidateDriver 的业务处理入口；本方法直接依赖 Debug.LogError(object)、LeonAnimatorDriver.ValidateAnimatorContract()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection) 等共 12 处；下游（含内部回调体） Debug.LogError(object)、LeonAnimatorDriver.ValidateAnimatorContract()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool ValidateDriver()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (driverCheckDone)
        {
            return driverCheckResult;
        }

        driverCheckDone = true;
        if (driver == null)
        {
            Debug.LogError("[Coordinator] Driver 未赋值");
            return driverCheckResult = false;
        }

        if (animator == null)
        {
            Debug.LogError("[Coordinator] Animator 未赋值");
            return driverCheckResult = false;
        }

        if (driver.Animator != animator)
        {
            Debug.LogError("[Coordinator] Driver.Animator 与协调者 Animator 不一致");
            return driverCheckResult = false;
        }

        if (!driver.ValidateAnimatorContract())
        {
            Debug.LogError("[Coordinator] 驱动器契约无效");
            return driverCheckResult = false;
        }

        return driverCheckResult = true;
    }

    // ================================================================
    // 武器动作通知处理（只读写 weaponTx）
    // ================================================================
    /// <summary>HandleWeaponEntered 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimRig.BeginEquipIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimRig.BeginEquipIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前武器动作类型。使用位置：LeonAnimationActionCoordinator.HandleWeaponEntered(Animator, WeaponActionType, int)。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleWeaponEntered(Animator, WeaponActionType, int)。</param>
    private void HandleWeaponEntered(Animator src, WeaponActionType type, int seq)
    {
        if (!IsFromThisAnimator(src) || isDead || !hasActiveWeaponTx)
        {
            return;
        }

        // During transition from Holster to Equip, accept Equip entry
        if (weaponTx.phase == WeaponTransactionPhase.TransitioningHolsterToEquip && type == WeaponActionType.Equip)
        {
            weaponTx.stateEntrySequence = seq;
            weaponTx.entryBound = true;
            weaponTx.phase = WeaponTransactionPhase.PlayingEquip;
            // receiverValue8（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponEntered(Animator, WeaponActionType, int)。
global::LeonAnimRig receiverValue8 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                receiverValue8.BeginEquipIK(weaponTx.pendingWeaponType);
            }

            return;
        }

        if (weaponTx.phase == WeaponTransactionPhase.TransitioningHolsterToKnifeTake && type == WeaponActionType.KnifeTake)
        {
            weaponTx.stateEntrySequence = seq;
            weaponTx.entryBound = true;
            weaponTx.phase = WeaponTransactionPhase.PlayingKnifeTransition;
            return;
        }

        if (weaponTx.phase == WeaponTransactionPhase.TransitioningKnifeHideToEquip && type == WeaponActionType.Equip)
        {
            weaponTx.stateEntrySequence = seq;
            weaponTx.entryBound = true;
            weaponTx.phase = WeaponTransactionPhase.PlayingEquip;
            // receiverValue9（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponEntered(Animator, WeaponActionType, int)。
global::LeonAnimRig receiverValue9 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue9, null))
            {
                receiverValue9.BeginEquipIK(weaponTx.pendingWeaponType);
            }

            return;
        }

        if (weaponTx.entryBound || weaponTx.weaponActionType != type)
        {
            return;
        }

        weaponTx.stateEntrySequence = seq;
        weaponTx.entryBound = true;
        switch (type)
        {
            case WeaponActionType.Reload:
                weaponTx.phase = WeaponTransactionPhase.PlayingReload;
                break;
            case WeaponActionType.Holster:
                weaponTx.phase = WeaponTransactionPhase.PlayingHolster;
                break;
            case WeaponActionType.Equip:
                weaponTx.phase = WeaponTransactionPhase.PlayingEquip;
                break;
            case WeaponActionType.KnifeTake:
            case WeaponActionType.KnifeHide:
                weaponTx.phase = WeaponTransactionPhase.PlayingKnifeTransition;
                break;
        }
    }

    /// <summary>HandleWeaponProgress 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction, float&gt;.Invoke(AnimationActionTransaction, float)、HashSet&lt;VisualMarker&gt;.Contains(VisualMarker)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction, float&gt;.Invoke(AnimationActionTransaction, float)、HashSet&lt;VisualMarker&gt;.Contains(VisualMarker)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数、作为 LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker) 的参数。</param>
    /// <param name="type">中文含义：当前武器动作类型。使用位置：作为 LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float) 的参数、LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)。</param>
    /// <param name="normalizedTime">中文含义：动画归一化播放时间。使用位置：作为 Action&lt;AnimationActionTransaction, float&gt;.Invoke(AnimationActionTransaction, float) 的参数、作为 LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float) 的参数、LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)。</param>
    private void HandleWeaponProgress(Animator src, WeaponActionType type, int seq, float normalizedTime)
    {
        if (!IsFromThisAnimator(src) || isDead || !hasActiveWeaponTx)
        {
            return;
        }

        if (!weaponTx.entryBound || weaponTx.stateEntrySequence != seq)
        {
            return;
        }

        if (weaponTx.weaponActionType != type || weaponTx.cancelled)
        {
            return;
        }

        // receiverValue10（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)。
global::LeonAnimRig receiverValue10 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.UpdateKnifeTransitionIK(type, normalizedTime);
        }

        // receiverValue11（判空前缓存的武器动作进度时的通知回调）：从武器动作进度时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)。
global::System.Action<global::AnimationActionTransaction, float> receiverValue11 = OnWeaponActionProgress;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.Invoke(weaponTx, normalizedTime);
        }

        if (type == WeaponActionType.Reload && weaponTx.phase == WeaponTransactionPhase.PlayingReload)
        {
            if (OwnsNativeW870Reload)
            {
                HandleNativeW870ReloadProgress(normalizedTime);
                return;
            }

            float detach = 0.28f;
            float attach = 0.60f;
            float mechanism = 0.82f;
            if (driver != null && driver.NativeCqbrReloadActive)
            {
                detach = LeonAnimatorDriver.NativeCqbrMagazineDetachTime;
                attach = LeonAnimatorDriver.NativeCqbrMagazineCommitTime;
                mechanism = LeonAnimatorDriver.NativeCqbrBoltReleaseTime;
            }
            else if (driver != null && driver.NativePistolReloadActive)
            {
                int detachFrame = LeonAnimatorDriver.PistolTacticalMagazineDetachFrame;
                mechanism = float.PositiveInfinity;
                if (driver.NativePistolEmptyReload)
                {
                    detachFrame = LeonAnimatorDriver.PistolEmptyMagazineDetachFrame;
                    mechanism = (float)LeonAnimatorDriver.PistolSlideReleaseFrame / LeonAnimatorDriver.PistolReloadFrames;
                }
                detach = (float)detachFrame / LeonAnimatorDriver.PistolReloadFrames;
                attach = (float)LeonAnimatorDriver.PistolMagazineCommitFrame / LeonAnimatorDriver.PistolReloadFrames;
                normalizedTime += 0.501f / LeonAnimatorDriver.PistolReloadFrames;
            }
            if (normalizedTime >= detach)
            {
                NotifyVisualMarker(src, VisualMarker.MagazineDetached);
            }

            if (normalizedTime >= attach)
            {
                NotifyVisualMarker(src, VisualMarker.MagazineAttached);
            }

            if (normalizedTime >= mechanism)
            {
                NotifyVisualMarker(src, VisualMarker.ReloadMechanism);
            }

            return;
        }

        // 原版事件按最近的源帧取样，避免物理步恰好落在接触帧边界时多等一拍。
        const float cqbrFrameRounding = 0.501f / RE4.ChapterOne.ChapterOneRootActor.RifleDrawFrames;
        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.PlayingHolster
            && weaponTx.sourceWeaponType == WeaponType.Rifle && driver != null && driver.SupportsNativeCqbr
            && normalizedTime + cqbrFrameRounding >= 1f - LeonAnimatorDriver.NativeCqbrRevealTime)
        {
            NotifyVisualMarker(src, VisualMarker.HolsterVisualCommit);
        }
        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.PlayingHolster
            && weaponTx.sourceWeaponType == WeaponType.Shotgun && driver != null && driver.SupportsNativeW870Switch
            && normalizedTime * LeonAnimatorDriver.W870HolsterFrames + 0.501f >= LeonAnimatorDriver.W870StowFrame)
        {
            NotifyVisualMarker(src, VisualMarker.HolsterVisualCommit);
        }
        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.PlayingHolster
            && weaponTx.sourceWeaponType == WeaponType.Pistol && driver != null && driver.NativePistolHolsterActive
            && normalizedTime * LeonAnimatorDriver.PistolHolsterFrames + 0.501f >= LeonAnimatorDriver.PistolStowFrame)
        {
            NotifyVisualMarker(src, VisualMarker.HolsterVisualCommit);
        }
        if (type != WeaponActionType.Equip || weaponTx.phase != WeaponTransactionPhase.PlayingEquip)
        {
            return;
        }

        // 视觉事件只负责武器显隐；长枪副手握把事件才允许长枪反向动力学接入。
        float backHide = 0.08f;
        float reveal = 0.2f;
        float grip = 0.62f;
        float markerTime = normalizedTime;
        if (weaponTx.pendingWeaponType == WeaponType.Rifle && driver != null && driver.SupportsNativeCqbr)
        {
            markerTime += cqbrFrameRounding;
            backHide = LeonAnimatorDriver.NativeCqbrRevealTime;
            reveal = LeonAnimatorDriver.NativeCqbrRevealTime;
            grip = LeonAnimatorDriver.NativeCqbrGripTime;
        }
        else if (weaponTx.pendingWeaponType == WeaponType.Shotgun && driver != null && driver.SupportsNativeW870Switch)
        {
            markerTime += 0.501f / LeonAnimatorDriver.W870DrawFrames;
            reveal = (float)LeonAnimatorDriver.W870RevealFrame / LeonAnimatorDriver.W870DrawFrames;
            backHide = reveal;
            grip = (float)LeonAnimatorDriver.W870GripFrame / LeonAnimatorDriver.W870DrawFrames;
        }
        else if (weaponTx.pendingWeaponType == WeaponType.Pistol && driver != null && driver.NativePistolEquipActive)
        {
            markerTime += 0.501f / LeonAnimatorDriver.PistolDrawFrames;
            reveal = (float)LeonAnimatorDriver.PistolRevealFrame / LeonAnimatorDriver.PistolDrawFrames;
        }
        if (IsLongGunWeapon(weaponTx.pendingWeaponType) && markerTime >= backHide && !processedMarkers.Contains(VisualMarker.RifleBackHideBeforeReveal))
        {
            NotifyVisualMarker(src, VisualMarker.RifleBackHideBeforeReveal);
        }

        if (markerTime >= reveal && !processedMarkers.Contains(VisualMarker.EquipVisualReveal))
        {
            NotifyVisualMarker(src, VisualMarker.EquipVisualReveal);
        }

        // 唯一的长枪副手握把兜底，使用装备状态行为上报的进度，避免误读收枪状态。
        if (IsLongGunWeapon(weaponTx.pendingWeaponType) && markerTime >= grip && !processedMarkers.Contains(VisualMarker.LongGunSupportHandGrip))
        {
            NotifyVisualMarker(src, VisualMarker.LongGunSupportHandGrip);
        }

        if (normalizedTime >= 0.82f)
        {
            NotifyVisualMarker(src, VisualMarker.EquipSettled);
        }
    }

    /// <summary>HandleWeaponCompleted 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimatorDriver.EndWeaponAction()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimatorDriver.EndWeaponAction()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前武器动作类型。使用位置：LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)。</param>
    private void HandleWeaponCompleted(Animator src, WeaponActionType type, int seq)
    {
        if (!IsFromThisAnimator(src) || isDead || !hasActiveWeaponTx)
        {
            return;
        }

        if (!weaponTx.entryBound || weaponTx.stateEntrySequence != seq)
        {
            return;
        }

        if (weaponTx.weaponActionType != type || weaponTx.completed || weaponTx.cancelled)
        {
            return;
        }

        if (type == WeaponActionType.Reload && OwnsNativeW870Reload
            && driver.W870Stage is W870ReloadStage.Start or W870ReloadStage.Loop)
        {
            return;
        }

        weaponTx.completed = true;
        switch (type)
        {
            case WeaponActionType.Reload:
                weaponTx.phase = WeaponTransactionPhase.AwaitingReloadCommit;
                // receiverValue12（判空前缓存的换弹动画已完成时的通知回调）：从换弹动画已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue12 = OnReloadAnimationCompleted;
                if (!global::System.Object.ReferenceEquals(receiverValue12, null))
                {
                    receiverValue12.Invoke(weaponTx);
                }

                break;
            case WeaponActionType.Holster:
                NotifyVisualMarker(src, VisualMarker.HolsterVisualCommit);
                weaponTx.phase = WeaponTransactionPhase.AwaitingWeaponCommit;
                // receiverValue13（判空前缓存的收起动画已完成时的通知回调）：从收起动画已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue13 = OnHolsterAnimationCompleted;
                if (!global::System.Object.ReferenceEquals(receiverValue13, null))
                {
                    receiverValue13.Invoke(weaponTx);
                }

                break;
            case WeaponActionType.Equip:
                if (driver != null)
                {
                    driver.EndWeaponAction();
                }

                weaponTx.phase = WeaponTransactionPhase.WaitingForEquipExit;
                break;
            case WeaponActionType.KnifeTake:
            case WeaponActionType.KnifeHide:
                // receiverValue14（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)。
global::LeonAnimatorDriver receiverValue14 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue14, null))
                {
                    receiverValue14.EndWeaponAction();
                }

                weaponTx.phase = WeaponTransactionPhase.WaitingForKnifeTransitionExit;
                break;
        }
    }

    /// <summary>HandleWeaponInterrupted 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前武器动作类型。使用位置：LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int)。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int)。</param>
    private void HandleWeaponInterrupted(Animator src, WeaponActionType type, int seq)
    {
        if (!IsFromThisAnimator(src) || isDead || !hasActiveWeaponTx)
        {
            return;
        }

        if (!weaponTx.entryBound || weaponTx.stateEntrySequence != seq)
        {
            return;
        }

        if (weaponTx.weaponActionType != type || weaponTx.cancelled)
        {
            return;
        }

        weaponTx.cancelled = true;
        weaponTx.phase = WeaponTransactionPhase.Cancelled;
        hasActiveWeaponTx = false;
        // receiverValue15（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue15 = OnWeaponActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.Invoke(weaponTx);
        }

        if (type == WeaponActionType.KnifeTake || type == WeaponActionType.KnifeHide)
        {
            // receiverValue16（判空前缓存的小刀过渡已中断时的通知回调）：从小刀过渡已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponInterrupted(Animator, WeaponActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue16 = OnKnifeTransitionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.Invoke(weaponTx);
            }
        }
    }

    /// <summary>HandleWeaponExited 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.GetCurrentWeaponForIK()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.CompleteWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.GetCurrentWeaponForIK()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.CompleteWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前武器动作类型。使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。</param>
    /// <param name="wasCompleted">中文含义：之前是否已完成。</param>
    private void HandleWeaponExited(Animator src, WeaponActionType type, int seq, bool wasCompleted)
    {
        if (!IsFromThisAnimator(src) || isDead)
        {
            return;
        }

        // Handle Holster exit during TransitioningHolsterToEquip phase
        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.TransitioningHolsterToEquip)
        {
            if (weaponTx.holsterEntrySequence != seq)
            {
                return;
            }

            // Holster exited, Equip is expected. Don't clear transaction.
            // If Equip hasn't entered yet, wait for it.
            waitingForWeaponExit = false;
            return;
        }

        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.TransitioningHolsterToKnifeTake)
        {
            if (weaponTx.holsterEntrySequence != seq)
            {
                return;
            }

            waitingForWeaponExit = false;
            return;
        }

        // Handle Holster exit during WaitingForHolsterExit phase (old flow without TransitionToEquip)
        if (type == WeaponActionType.Holster && weaponTx.phase == WeaponTransactionPhase.WaitingForHolsterExit)
        {
            if (!weaponTx.entryBound || weaponTx.stateEntrySequence != seq)
            {
                return;
            }

            waitingForWeaponExit = false;
            if (hasPendingWeaponSwitch && IsGunWeapon(weaponTx.pendingWeaponType))
            {
                // visualCommitted（是否视觉表现已提交）：使用位置：LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
bool visualCommitted = weaponTx.visualCommitDone;
                weaponTx.weaponActionType = WeaponActionType.Equip;
                weaponTx.entryBound = false;
                weaponTx.stateEntrySequence = 0;
                weaponTx.completed = false;
                weaponTx.cancelled = false;
                weaponTx.visualCommitDone = visualCommitted;
                weaponTx.phase = WeaponTransactionPhase.WaitingForEnter;
            }
            else
            {
                hasActiveWeaponTx = false;
            }

            return;
        }

        if (!weaponTx.entryBound || weaponTx.stateEntrySequence != seq)
        {
            return;
        }

        if (weaponTx.weaponActionType != type)
        {
            return;
        }

        switch (weaponTx.phase)
        {
            case WeaponTransactionPhase.WaitingForReloadExit:
                hasActiveWeaponTx = false;
                weaponTx.phase = WeaponTransactionPhase.Completed;
                // receiverValue17（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimatorDriver receiverValue17 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue17, null))
                {
                    receiverValue17.FinalizeWeaponActionLayer();
                }

                // receiverValue18（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimRig receiverValue18 = animRig;
                if (!global::System.Object.ReferenceEquals(receiverValue18, null))
                {
                    receiverValue18.CompleteWeaponActionIK(GetCurrentWeaponForIK());
                }

                // receiverValue19（判空前缓存的换弹已退出时的通知回调）：从换弹已退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue19 = OnReloadExited;
                if (!global::System.Object.ReferenceEquals(receiverValue19, null))
                {
                    receiverValue19.Invoke(weaponTx);
                }

                break;
            case WeaponTransactionPhase.WaitingForEquipExit:
                hasActiveWeaponTx = false;
                hasPendingWeaponSwitch = false;
                weaponTx.phase = WeaponTransactionPhase.Completed;
                // receiverValue20（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimatorDriver receiverValue20 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue20, null))
                {
                    receiverValue20.FinalizeWeaponActionLayer();
                }

                // receiverValue21（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimRig receiverValue21 = animRig;
                if (!global::System.Object.ReferenceEquals(receiverValue21, null))
                {
                    receiverValue21.CompleteWeaponActionIK(GetCurrentWeaponForIK());
                }

                // receiverValue22（判空前缓存的装备动画已完成时的通知回调）：从装备动画已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue22 = OnEquipAnimationCompleted;
                if (!global::System.Object.ReferenceEquals(receiverValue22, null))
                {
                    receiverValue22.Invoke(weaponTx);
                }

                break;
            case WeaponTransactionPhase.WaitingForKnifeTransitionExit:
                if (weaponTx.isWeaponSwitch && weaponTx.weaponActionType == WeaponActionType.KnifeHide)
                {
                    weaponTx.phase = WeaponTransactionPhase.AwaitingKnifeHideCommit;
                    // receiverValue23（判空前缓存的小刀过渡已退出时的通知回调）：从小刀过渡已退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue23 = OnKnifeTransitionExited;
                    if (!global::System.Object.ReferenceEquals(receiverValue23, null))
                    {
                        receiverValue23.Invoke(weaponTx);
                    }

                    break;
                }

                hasActiveWeaponTx = false;
                hasPendingWeaponSwitch = false;
                weaponTx.phase = WeaponTransactionPhase.Completed;
                // receiverValue24（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimatorDriver receiverValue24 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue24, null))
                {
                    receiverValue24.FinalizeWeaponActionLayer();
                }

                // receiverValue25（判空前缓存的小刀过渡已退出时的通知回调）：从小刀过渡已退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue25 = OnKnifeTransitionExited;
                if (!global::System.Object.ReferenceEquals(receiverValue25, null))
                {
                    receiverValue25.Invoke(weaponTx);
                }

                break;
            case WeaponTransactionPhase.Cancelled:
                // 中断后退出（仅 entryBound=true 的情况会到达这里）
                hasActiveWeaponTx = false;
                waitingForWeaponExit = false;
                hasPendingWeaponSwitch = false;
                // receiverValue26（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimatorDriver receiverValue26 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue26, null))
                {
                    receiverValue26.FinalizeWeaponActionLayer();
                }

                if (type == WeaponActionType.KnifeTake || type == WeaponActionType.KnifeHide)
                {
                    // receiverValue27（判空前缓存的小刀过渡已中断时的通知回调）：从小刀过渡已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue27 = OnKnifeTransitionInterrupted;
                    if (!global::System.Object.ReferenceEquals(receiverValue27, null))
                    {
                        receiverValue27.Invoke(weaponTx);
                    }
                }

                break;
            default:
                hasActiveWeaponTx = false;
                waitingForWeaponExit = false;
                // receiverValue28（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)。
global::LeonAnimatorDriver receiverValue28 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue28, null))
                {
                    receiverValue28.FinalizeWeaponActionLayer();
                }

                break;
        }
    }

    // ================================================================
    // 全身动作通知处理（只读写 fullBodyTx）
    // ================================================================
    /// <summary>HandleFullBodyEntered 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前全身动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 的参数 等共 4 处。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。</param>
    private void HandleFullBodyEntered(Animator src, FullBodyActionType type, int seq)
    {
        if (!IsFromThisAnimator(src))
        {
            return;
        }

        if (type == FullBodyActionType.Death)
        {
            if (!isDead || deathStateEntrySequence != 0)
            {
                return;
            }

            deathStateEntrySequence = seq;
            return;
        }

        if (isDead || !hasActiveFullBodyTx)
        {
            return;
        }

        if (fullBodyTx.entryBound || fullBodyTx.fullBodyActionType != type)
        {
            return;
        }

        fullBodyTx.stateEntrySequence = seq;
        fullBodyTx.entryBound = true;
        if (IsQTEAction(type))
        {
            // receiverValue29（判空前缓存的配对动作动作进入时的通知回调）：从配对动作动作进入时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue29 = OnQTEActionEntered;
            if (!global::System.Object.ReferenceEquals(receiverValue29, null))
            {
                receiverValue29.Invoke(fullBodyTx);
            }
        }
        else if (IsMeleeAction(type))
        {
            // receiverValue30（判空前缓存的近战动作已进入时的通知回调）：从近战动作已进入时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue30 = OnMeleeActionEntered;
            if (!global::System.Object.ReferenceEquals(receiverValue30, null))
            {
                receiverValue30.Invoke(fullBodyTx);
            }
        }
        else if (IsTraversalAction(type))
        {
            // receiverValue31（判空前缓存的通行动作进入时的通知回调）：从通行动作进入时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue31 = OnTraversalActionEntered;
            if (!global::System.Object.ReferenceEquals(receiverValue31, null))
            {
                receiverValue31.Invoke(fullBodyTx);
            }
        }
    }

    /// <summary>HandleFullBodyCompleted 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.EndDeathMotion()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.EndDeathMotion()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前全身动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 的参数 等共 4 处。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。</param>
    private void HandleFullBodyCompleted(Animator src, FullBodyActionType type, int seq)
    {
        if (!IsFromThisAnimator(src))
        {
            return;
        }

        if (type == FullBodyActionType.Death)
        {
            if (!isDead || deathAnimationCompletionSent || deathStateEntrySequence == 0 || deathStateEntrySequence != seq)
            {
                return;
            }

            deathAnimationCompletionSent = true;
            // receiverValue32（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::LeonActionMotionDriver receiverValue32 = actionMotionDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue32, null))
            {
                receiverValue32.EndDeathMotion();
            }

            // receiverValue33（判空前缓存的死亡动画已完成时的通知回调）：从死亡动画已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::System.Action receiverValue33 = OnDeathAnimationCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue33, null))
            {
                receiverValue33.Invoke();
            }

            return;
        }

        if (isDead || !hasActiveFullBodyTx)
        {
            return;
        }

        if (!fullBodyTx.entryBound || fullBodyTx.stateEntrySequence != seq)
        {
            return;
        }

        if (fullBodyTx.fullBodyActionType != type || fullBodyTx.completed || fullBodyTx.cancelled)
        {
            return;
        }

        if (fullBodyTx.plannedExit && fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForQTEExit)
        {
            return;
        }

        if (type == FullBodyActionType.ItemPickup)
        {
            CompleteItemPickup(fullBodyTx.transactionId);
            return;
        }
        fullBodyTx.completed = true;
        if (type == FullBodyActionType.Hit)
        {
            fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForHitExit;
            // 固定顺序：先开始待处理受伤层过渡（与受击开始退出同一帧），
            // 再请求全身受击状态退出，实现图层权重交接。
            // receiverValue34（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::LeonAnimatorDriver receiverValue34 = // 固定顺序：先开始待处理受伤层过渡（与受击开始退出同一帧），
 // 再请求全身受击状态退出，实现图层权重交接。
            driver;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                receiverValue34.BeginPendingInjuredTransition();
            }

            // receiverValue35（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::LeonAnimatorDriver receiverValue35 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue35, null))
            {
                receiverValue35.EndFullBodyAction();
            }
        }
        else if (IsQTEAction(type))
        {
            // receiverValue36（判空前缓存的配对动作动作完成时的通知回调）：从配对动作动作完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue36 = OnQTEActionCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue36, null))
            {
                receiverValue36.Invoke(fullBodyTx);
            }
        }
        else if (IsMeleeAction(type))
        {
            // receiverValue37（判空前缓存的近战动作已完成时的通知回调）：从近战动作已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue37 = OnMeleeActionCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue37, null))
            {
                receiverValue37.Invoke(fullBodyTx);
            }
        }
        else if (IsTraversalAction(type))
        {
            fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForTraversalExit;
            if (type == FullBodyActionType.WindowDropLand || type == FullBodyActionType.WindowClimbThrough ||
                type == FullBodyActionType.VillageGateOpen)
            {
                // receiverValue38（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::LeonAnimatorDriver receiverValue38 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue38, null))
                {
                    receiverValue38.EndTraversalLanding();
                }
            }
            else
            {
                // receiverValue39（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::LeonAnimatorDriver receiverValue39 = driver;
                if (!global::System.Object.ReferenceEquals(receiverValue39, null))
                {
                    receiverValue39.EndFullBodyAction();
                }
            }

            // receiverValue40（判空前缓存的通行动作完成时的通知回调）：从通行动作完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue40 = OnTraversalActionCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue40, null))
            {
                receiverValue40.Invoke(fullBodyTx);
            }
        }
    }

    /// <summary>HandleFullBodyInterrupted 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimatorDriver.BeginPendingInjuredTransition()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimatorDriver.BeginPendingInjuredTransition()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前全身动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 的参数 等共 4 处。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。</param>
    private void HandleFullBodyInterrupted(Animator src, FullBodyActionType type, int seq)
    {
        if (!IsFromThisAnimator(src) || isDead || !hasActiveFullBodyTx)
        {
            return;
        }

        if (type == fullBodyTx.outgoingFullBodyActionType && seq == fullBodyTx.outgoingStateEntrySequence)
        {
            return;
        }

        if (!fullBodyTx.entryBound || fullBodyTx.stateEntrySequence != seq)
        {
            return;
        }

        if (fullBodyTx.fullBodyActionType != type || fullBodyTx.cancelled)
        {
            return;
        }

        // 计划内恢复退出：SMB 因未到完成阈值而发送中断通知，不应视为真实中断
        if (fullBodyTx.plannedExit && fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForQTEExit)
        {
            return;
        }

        fullBodyTx.cancelled = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
        if (type == FullBodyActionType.Hit)
        {
            // 受击被意外中断时消费待处理受伤目标，
            // 由其以受击恢复专用时长执行交接，不能永久挂起。
            // receiverValue41（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。
global::LeonAnimatorDriver receiverValue41 = // 受击被意外中断时消费待处理受伤目标，
 // 由其以受击恢复专用时长执行交接，不能永久挂起。
            driver;
            if (!global::System.Object.ReferenceEquals(receiverValue41, null))
            {
                receiverValue41.BeginPendingInjuredTransition();
            }
        }

        // receiverValue42（判空前缓存的全身动作中断时的通知回调）：从全身动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue42 = OnFullBodyActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            receiverValue42.Invoke(fullBodyTx);
        }

        if (IsQTEAction(type))
        {
            // receiverValue43（判空前缓存的配对动作动作中断时的通知回调）：从配对动作动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue43 = OnQTEActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue43, null))
            {
                receiverValue43.Invoke(fullBodyTx);
            }
        }
        else if (IsMeleeAction(type))
        {
            // receiverValue44（判空前缓存的近战动作已中断时的通知回调）：从近战动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue44 = OnMeleeActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue44, null))
            {
                receiverValue44.Invoke(fullBodyTx);
            }
        }
        else if (IsTraversalAction(type))
        {
            // receiverValue45（判空前缓存的通行动作中断时的通知回调）：从通行动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)。
global::System.Action<global::AnimationActionTransaction> receiverValue45 = OnTraversalActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue45, null))
            {
                receiverValue45.Invoke(fullBodyTx);
            }
        }
    }

    /// <summary>HandleFullBodyExited 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.EndHitMotion()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.EndHitMotion()、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="src">中文含义：当前动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="type">中文含义：当前全身动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType) 的参数 等共 5 处。</param>
    /// <param name="seq">中文含义：序号。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。</param>
    /// <param name="wasCompleted">中文含义：之前是否已完成。</param>
    private void HandleFullBodyExited(Animator src, FullBodyActionType type, int seq, bool wasCompleted)
    {
        if (!IsFromThisAnimator(src) || isDead)
        {
            return;
        }

        if (type == fullBodyTx.outgoingFullBodyActionType && seq == fullBodyTx.outgoingStateEntrySequence)
        {
            return;
        }

        if (!fullBodyTx.entryBound || fullBodyTx.stateEntrySequence != seq)
        {
            return;
        }

        if (fullBodyTx.fullBodyActionType != type)
        {
            return;
        }

        if (type == FullBodyActionType.Hit && fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForHitExit)
        {
            hasActiveFullBodyTx = false;
            fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Completed;
            // receiverValue46（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimatorDriver receiverValue46 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue46, null))
            {
                receiverValue46.FinalizeFullBodyActionLayer();
            }

            // receiverValue47（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonActionMotionDriver receiverValue47 = actionMotionDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue47, null))
            {
                receiverValue47.EndHitMotion();
            }

            // receiverValue48（判空前缓存的命中动画已完成时的通知回调）：从命中动画已完成时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue48 = OnHitAnimationCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue48, null))
            {
                receiverValue48.Invoke(fullBodyTx);
            }

            // 使用 animRig 自身记录的当前武器，不读取可能过期的 weaponTx
            if (animRig != null)
            {
                animRig.ExitHitIK(animRig.CurrentWeapon);
            }

            return;
        }

        if (IsQTEAction(type) && (fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForQTEExit || fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.Cancelled))
        {
            hasActiveFullBodyTx = false;
            // assignmentReceiver49（赋值前保存的目标动画动作事务）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
ref global::AnimationActionTransaction assignmentReceiver49 = ref fullBodyTx;
            if (fullBodyTx.cancelled)
            {
                assignmentReceiver49.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
            }
            else
            {
                assignmentReceiver49.fullBodyPhase = FullBodyTransactionPhase.Completed;
            }

            // receiverValue51（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimatorDriver receiverValue51 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue51, null))
            {
                receiverValue51.FinalizeFullBodyActionLayer();
            }

            RestoreQTEWeaponVisual(fullBodyTx.qteSessionId);
            // receiverValue52（判空前缓存的配对动作动作退出时的通知回调）：从配对动作动作退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue52 = OnQTEActionExited;
            if (!global::System.Object.ReferenceEquals(receiverValue52, null))
            {
                receiverValue52.Invoke(fullBodyTx);
            }

            return;
        }

        if (IsMeleeAction(type) && (fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForMeleeExit || fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.Cancelled))
        {
            hasActiveFullBodyTx = false;
            // assignmentReceiver53（赋值前保存的目标动画动作事务）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
ref global::AnimationActionTransaction assignmentReceiver53 = ref fullBodyTx;
            if (fullBodyTx.cancelled)
            {
                assignmentReceiver53.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
            }
            else
            {
                assignmentReceiver53.fullBodyPhase = FullBodyTransactionPhase.Completed;
            }

            // receiverValue55（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimatorDriver receiverValue55 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue55, null))
            {
                receiverValue55.FinalizeFullBodyActionLayer();
            }

            if (IsKnifeAction(type))
            {
                // receiverValue56（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimRig receiverValue56 = animRig;
                if (!global::System.Object.ReferenceEquals(receiverValue56, null))
                {
                    receiverValue56.EndKnifeActionIK();
                }
            }

            // receiverValue57（判空前缓存的近战动作已退出时的通知回调）：从近战动作已退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue57 = OnMeleeActionExited;
            if (!global::System.Object.ReferenceEquals(receiverValue57, null))
            {
                receiverValue57.Invoke(fullBodyTx);
            }

            return;
        }

        if (IsTraversalAction(type) && (fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.WaitingForTraversalExit || fullBodyTx.fullBodyPhase == FullBodyTransactionPhase.Cancelled))
        {
            hasActiveFullBodyTx = false;
            // assignmentReceiver58（赋值前保存的目标动画动作事务）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
ref global::AnimationActionTransaction assignmentReceiver58 = ref fullBodyTx;
            if (fullBodyTx.cancelled)
            {
                assignmentReceiver58.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
            }
            else
            {
                assignmentReceiver58.fullBodyPhase = FullBodyTransactionPhase.Completed;
            }

            // receiverValue60（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimatorDriver receiverValue60 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue60, null))
            {
                receiverValue60.ReleaseTraversalLandingBasePose();
            }

            // receiverValue61（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::LeonAnimatorDriver receiverValue61 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue61, null))
            {
                receiverValue61.FinalizeFullBodyActionLayer();
            }

            // receiverValue62（判空前缓存的通行动作退出时的通知回调）：从通行动作退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)。
global::System.Action<global::AnimationActionTransaction> receiverValue62 = OnTraversalActionExited;
            if (!global::System.Object.ReferenceEquals(receiverValue62, null))
            {
                receiverValue62.Invoke(fullBodyTx);
            }
        }
    }

    /// <summary>HandleMeleeHitWindow 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAttackAction(FullBodyActionType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.IsMeleeAttackAction(FullBodyActionType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsMeleeAttackAction(FullBodyActionType) 的参数、LeonAnimationActionCoordinator.HandleMeleeHitWindow(Animator, FullBodyActionType)。</param>
    private void HandleMeleeHitWindow(Animator source, FullBodyActionType actionType)
    {
        if (!IsFromThisAnimator(source) || isDead || !hasActiveFullBodyTx || fullBodyTx.cancelled || !fullBodyTx.entryBound || fullBodyTx.fullBodyActionType != actionType || !IsMeleeAttackAction(actionType))
        {
            return;
        }

        // receiverValue63（判空前缓存的近战命中窗口时的通知回调）：从近战命中窗口时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.HandleMeleeHitWindow(Animator, FullBodyActionType)。
global::System.Action<global::AnimationActionTransaction> receiverValue63 = OnMeleeHitWindow;
        if (!global::System.Object.ReferenceEquals(receiverValue63, null))
        {
            receiverValue63.Invoke(fullBodyTx);
        }
    }

    // ================================================================
    // 请求接口
    // ================================================================
    /// <summary>中文含义：玩家租用版本。使用位置：作为 ObjectIdentity.ObjectIdentity(int, long) 的参数、LeonAnimationActionCoordinator.OnEnable()。</summary>
    private long playerLeaseVersion;
    /// <summary>中文含义：玩家身份标识。使用位置：作为 ActionHandle.ActionHandle(ObjectIdentity, long) 的参数、作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)。</summary>
    public ObjectIdentity PlayerIdentity => new ObjectIdentity(GetInstanceID(), playerLeaseVersion);

    /// <summary>CreateActionRequest 的业务处理入口；本方法直接依赖 ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleReload()、LeonWeaponHandler.SwitchTo(WeaponType)；下游（含内部回调体） ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle)、RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数。</param>
    /// <param name="action">中文含义：动作。使用位置：作为 ActionRequest.ActionRequest(RequestContext, ActionKind, ActionHandle) 的参数。</param>
    public ActionRequest CreateActionRequest(string source, ActionKind action) => new ActionRequest(new RequestContext(source, PlayerIdentity, PlayerIdentity, nextTxId), action);
    /// <summary>RequestWeaponAction 的业务处理入口；本方法直接依赖 ActionHandle.ActionHandle(ObjectIdentity, long)、ActionResult.Accept(ActionHandle)、ActionResult.Reject(RejectionReason)、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.RequestReload(WeaponType)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleReload()、LeonWeaponHandler.SwitchTo(WeaponType)；下游（含内部回调体） ActionHandle.ActionHandle(ObjectIdentity, long)、ActionResult.Accept(ActionHandle)、ActionResult.Reject(RejectionReason)、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.RequestReload(WeaponType)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="request">中文含义：请求。使用位置：作为 LeonAnimationActionCoordinator.CheckActionConflict(ActionKind) 的参数、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)。</param>
    /// <param name="currentWeapon">中文含义：当前武器。使用位置：作为 LeonAnimationActionCoordinator.RequestReload(WeaponType) 的参数、作为 LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType) 的参数。</param>
    /// <param name="targetWeapon">中文含义：目标武器。使用位置：作为 LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType) 的参数。</param>
    public ActionResult RequestWeaponAction(ActionRequest request, WeaponType currentWeapon, WeaponType targetWeapon = WeaponType.Pistol)
    {
        if (!request.Context.Valid || request.Context.Target != PlayerIdentity)
        {
            return ActionResult.Reject(RejectionReason.ObjectHasInvalidate);
        }

        if (request.Action != ActionKind.Reload && request.Action != ActionKind.SwitchWeapon)
        {
            return ActionResult.Reject(RejectionReason.ParameterInvalid);
        }

        // reason（原因）：保存 LeonAnimationActionCoordinator.CheckActionConflict(ActionKind) 的返回结果，供当前步骤使用。使用位置：作为 ActionResult.Reject(RejectionReason) 的参数、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)。
var reason = CheckActionConflict(request.Action);
        if (reason != RejectionReason.None)
        {
            return ActionResult.Reject(reason);
        }

        if (!ValidateDriver())
        {
            return ActionResult.Reject(RejectionReason.NotYetReady);
        }

        // accept（是否接受）：使用位置：LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)。
bool accept;
        if (request.Action == ActionKind.Reload)
        {
            accept = RequestReload(currentWeapon);
        }
        else
        {
            accept = RequestWeaponSwitch(currentWeapon, targetWeapon);
        }

        if (accept)
        {
            return ActionResult.Accept(new ActionHandle(PlayerIdentity, weaponTx.transactionId));
        }
        else
        {
            return ActionResult.Reject(RejectionReason.ParameterInvalid);
        }
    }

    /// <summary>CheckActionConflict 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)、LeonAnimationActionCoordinator.RequestQTE(int) 等共 9 处；下游（含内部回调体） LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="request">中文含义：请求。使用位置：作为 PlayerActionConflictRules.Check(ActionKind, bool, bool, bool, bool, bool, bool) 的参数。</param>
    private RejectionReason CheckActionConflict(ActionKind request) => PlayerActionConflictRules.Check(request, isDead, hasActiveWeaponTx, hasActiveFullBodyTx, IsMeleeAction(fullBodyTx.fullBodyActionType), waitingForWeaponExit, hasPendingWeaponSwitch);
    /// <summary>RequestReload 的业务处理入口；本方法直接依赖 AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.ResetVisualMarkers(int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimRig.BeginReloadIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.ResetVisualMarkers(int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimRig.BeginReloadIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="currentWeaponType">中文含义：当前武器类型。使用位置：作为 LeonAnimRig.BeginReloadIK(WeaponType) 的参数、LeonAnimationActionCoordinator.RequestReload(WeaponType)。</param>
    public bool RequestReload(WeaponType currentWeaponType)
    {
        if (!ValidateDriver() || CheckActionConflict(ActionKind.Reload) != RejectionReason.None)
        {
            return false;
        }

        weaponTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            weaponActionType = WeaponActionType.Reload,
            sourceWeaponType = currentWeaponType,
            phase = WeaponTransactionPhase.WaitingForEnter
        };
        hasActiveWeaponTx = true;
        ResetVisualMarkers(weaponTx.transactionId);
        driver.BeginWeaponAction(WeaponActionType.Reload);
        BeginNativeW870Reload();
        // receiverValue66（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestReload(WeaponType)。
global::LeonAnimRig receiverValue66 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue66, null))
        {
            receiverValue66.BeginReloadIK(currentWeaponType);
        }

        return true;
    }

    /// <summary>CompleteReloadAfterBusinessCommit 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.EndWeaponAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.TryCompleteShotgunShellReload(int)；下游（含内部回调体） LeonAnimatorDriver.EndWeaponAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)。</param>
    public bool CompleteReloadAfterBusinessCommit(int transactionId)
    {
        if (isDead)
        {
            return false;
        }

        if (!hasActiveWeaponTx || weaponTx.transactionId != transactionId)
        {
            return false;
        }

        if (weaponTx.phase != WeaponTransactionPhase.AwaitingReloadCommit)
        {
            return false;
        }

        if (driver != null)
        {
            driver.EndWeaponAction();
        }

        weaponTx.phase = WeaponTransactionPhase.WaitingForReloadExit;
        return true;
    }

    internal bool InterruptNativeMagazineReloadForFire()
    {
        if (isDead || !hasActiveWeaponTx || weaponTx.phase != WeaponTransactionPhase.PlayingReload
            || driver == null || !driver.NativeCqbrReloadActive && !driver.NativePistolReloadActive)
        {
            return false;
        }

        InterruptWeaponTransactionForFullBody();
        return true;
    }

    /// <summary>RequestWeaponSwitch 的业务处理入口；本方法直接依赖 AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ResetVisualMarkers(int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ResetVisualMarkers(int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="currentWeapon">中文含义：当前武器。使用位置：作为 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的参数、作为 LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType) 的参数、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。</param>
    /// <param name="targetWeapon">中文含义：目标武器。使用位置：作为 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的参数、作为 LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType) 的参数、作为 LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType) 的参数 等共 4 处。</param>
    public bool RequestWeaponSwitch(WeaponType currentWeapon, WeaponType targetWeapon)
    {
        if (!ValidateDriver() || CheckActionConflict(ActionKind.SwitchWeapon) != RejectionReason.None)
        {
            return false;
        }

        // currentIsGun（当前是否枪械）：保存 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的返回结果，供当前步骤使用。使用位置：LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。
bool currentIsGun = IsGunWeapon(currentWeapon);
        // targetIsGun（目标是否枪械）：保存 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType) 的返回结果，供当前步骤使用。使用位置：LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。
bool targetIsGun = IsGunWeapon(targetWeapon);
        if (!currentIsGun && currentWeapon != WeaponType.Knife)
        {
            return false;
        }

        if (!targetIsGun && targetWeapon != WeaponType.Knife)
        {
            return false;
        }

        if (currentWeapon == targetWeapon)
        {
            return false;
        }

        pendingWeaponType = targetWeapon;
        hasPendingWeaponSwitch = true;
        // 请求切枪只记录目标受伤显示模式，Holster 期间保持旧模式
        driver.PrepareInjuredWeaponSwitch(targetWeapon);
        // createdValue67（本次新建的动画动作事务）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。
global::AnimationActionTransaction createdValue67 = new AnimationActionTransaction();
        createdValue67.transactionId = nextTxId++;
        if (currentWeapon == WeaponType.Knife)
        {
            createdValue67.weaponActionType = WeaponActionType.KnifeHide;
        }
        else
        {
            createdValue67.weaponActionType = WeaponActionType.Holster;
        }

        createdValue67.sourceWeaponType = currentWeapon;
        createdValue67.pendingWeaponType = targetWeapon;
        createdValue67.isWeaponSwitch = true;
        createdValue67.phase = WeaponTransactionPhase.WaitingForEnter;
        weaponTx = createdValue67;
        hasActiveWeaponTx = true;
        ResetVisualMarkers(weaponTx.transactionId);
        driver.BeginWeaponAction(weaponTx.weaponActionType);
        if (currentIsGun)
        {
            // receiverValue69（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。
global::LeonAnimRig receiverValue69 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue69, null))
            {
                receiverValue69.BeginWeaponSwitchIK(currentWeapon, targetWeapon);
            }
        }
        else
        {
            driver.SetKnifeMode(false);
            // receiverValue70（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)。
global::LeonAnimRig receiverValue70 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue70, null))
            {
                receiverValue70.BeginKnifeHideIK();
            }
        }

        return true;
    }

    /// <summary>ContinueKnifeTakeAfterWeaponCommit 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.BeginKnifeEquipIK()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnHolsterAnimCompleted(AnimationActionTransaction)；下游（含内部回调体） LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.BeginKnifeEquipIK()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)。</param>
    public bool ContinueKnifeTakeAfterWeaponCommit(int transactionId)
    {
        if (!ValidateDriver() || isDead)
        {
            return false;
        }

        if (!hasActiveWeaponTx || weaponTx.transactionId != transactionId)
        {
            return false;
        }

        if (weaponTx.phase != WeaponTransactionPhase.AwaitingWeaponCommit)
        {
            return false;
        }

        if (!hasPendingWeaponSwitch || pendingWeaponType != WeaponType.Knife)
        {
            return false;
        }

        weaponTx.holsterEntrySequence = weaponTx.stateEntrySequence;
        weaponTx.weaponActionType = WeaponActionType.KnifeTake;
        weaponTx.entryBound = false;
        weaponTx.stateEntrySequence = 0;
        weaponTx.completed = false;
        weaponTx.cancelled = false;
        weaponTx.visualCommitDone = true;
        weaponTx.phase = WeaponTransactionPhase.TransitioningHolsterToKnifeTake;
        // 提交前确保武器动作层满权重遮挡底层
        driver.EnsureWeaponActionLayerFullWeight();
        driver.SetKnifeMode(true);
        // 同帧立即切换两个受伤层（小刀 → 全身受伤表演）
        driver.CommitInjuredWeaponSwitchPresentation(WeaponType.Knife);
        // receiverValue71（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)。
global::LeonAnimRig receiverValue71 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue71, null))
        {
            receiverValue71.BeginKnifeEquipIK();
        }

        driver.BeginWeaponAction(WeaponActionType.KnifeTake);
        return true;
    }

    /// <summary>ContinueEquipAfterKnifeHide 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimatorDriver.TransitionToEquip()、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)；下游（含内部回调体） LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimatorDriver.TransitionToEquip()、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)。</param>
    public bool ContinueEquipAfterKnifeHide(int transactionId)
    {
        if (!ValidateDriver() || isDead)
        {
            return false;
        }

        if (!hasActiveWeaponTx || weaponTx.transactionId != transactionId)
        {
            return false;
        }

        if (weaponTx.phase != WeaponTransactionPhase.AwaitingKnifeHideCommit)
        {
            return false;
        }

        if (!hasPendingWeaponSwitch)
        {
            return false;
        }

        if (!IsGunWeapon(pendingWeaponType))
        {
            return false;
        }

        weaponTx.weaponActionType = WeaponActionType.Equip;
        weaponTx.entryBound = false;
        weaponTx.stateEntrySequence = 0;
        weaponTx.completed = false;
        weaponTx.cancelled = false;
        weaponTx.visualCommitDone = true;
        weaponTx.phase = WeaponTransactionPhase.TransitioningKnifeHideToEquip;
        // 提交前确保武器动作层满权重遮挡底层
        driver.EnsureWeaponActionLayerFullWeight();
        driver.SetWeaponType(pendingWeaponType);
        // 同帧立即切换两个受伤层
        driver.CommitInjuredWeaponSwitchPresentation(pendingWeaponType);
        // receiverValue72（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)。
global::LeonAnimRig receiverValue72 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue72, null))
        {
            receiverValue72.CommitWeaponSwitchIK(pendingWeaponType);
        }

        driver.TransitionToEquip();
        return true;
    }

    /// <summary>ContinueEquipAfterWeaponCommit 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimatorDriver.TransitionToEquip()、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnHolsterAnimCompleted(AnimationActionTransaction)；下游（含内部回调体） LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimatorDriver.TransitionToEquip()、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">中文含义：事务编号。使用位置：LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)。</param>
    public bool ContinueEquipAfterWeaponCommit(int transactionId)
    {
        if (!ValidateDriver() || isDead)
        {
            return false;
        }

        if (!hasActiveWeaponTx || weaponTx.transactionId != transactionId)
        {
            return false;
        }

        if (weaponTx.phase != WeaponTransactionPhase.AwaitingWeaponCommit)
        {
            return false;
        }

        if (!hasPendingWeaponSwitch)
        {
            return false;
        }

        if (!IsGunWeapon(pendingWeaponType))
        {
            return false;
        }

        // 1. 保存 holsterEntrySequence
        weaponTx.holsterEntrySequence = weaponTx.stateEntrySequence;
        // 2. weaponActionType 改为 Equip
        weaponTx.weaponActionType = WeaponActionType.Equip;
        // 3. entryBound=false
        weaponTx.entryBound = false;
        // 4. stateEntrySequence=0
        weaponTx.stateEntrySequence = 0;
        // 5. completed=false
        weaponTx.completed = false;
        // 6. phase=TransitioningHolsterToEquip
        weaponTx.phase = WeaponTransactionPhase.TransitioningHolsterToEquip;
        // 7. visualCommitDone=true
        weaponTx.visualCommitDone = true;
        // 8. 提交前确保武器动作层满权重遮挡底层，禁止暴露切换
        driver.EnsureWeaponActionLayerFullWeight();
        // 9. 写入新 WeaponType
        driver.SetWeaponType(pendingWeaponType);
        // 10. 同帧立即切换两个受伤层（不使用混合）
        driver.CommitInjuredWeaponSwitchPresentation(pendingWeaponType);
        // receiverValue73（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)。
global::LeonAnimRig receiverValue73 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue73, null))
        {
            receiverValue73.CommitWeaponSwitchIK(pendingWeaponType);
        }

        // 最后才能调用 TransitionToEquip（触发动画转换）
        driver.TransitionToEquip();
        return true;
    }

    /// <summary>RequestHit 的业务处理入口；本方法直接依赖 AnimationActionTransaction.AnimationActionTransaction()、LeonActionMotionDriver.BeginHitMotion()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginHit(LeonHitDirection) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleHit(LeonHitDirection)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonActionMotionDriver.BeginHitMotion()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginHit(LeonHitDirection) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 LeonAnimatorDriver.BeginHit(LeonHitDirection) 的参数。</param>
    public bool RequestHit(LeonHitDirection direction)
    {
        if (hasActiveFullBodyTx && IsEnvironmentalAction(fullBodyTx.fullBodyActionType))
        {
            EndLadderAction(fullBodyTx.transactionId, fullBodyTx.fullBodyActionType, true);
        }

        if (hasActiveFullBodyTx && fullBodyTx.fullBodyActionType == FullBodyActionType.LadderPush)
        {
            EndLadderPush(fullBodyTx.transactionId, true);
        }
        if (hasActiveFullBodyTx && fullBodyTx.fullBodyActionType == FullBodyActionType.LadderTraversal)
        {
            EndLadderAction(fullBodyTx.transactionId, FullBodyActionType.LadderTraversal, true);
        }

        if (!ValidateDriver() || CheckActionConflict(ActionKind.HitReaction) != RejectionReason.None)
        {
            return false;
        }

        if (!TryInterruptMeleeForPriority(out FullBodyActionType /* 中文含义：退出中的动作。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ outgoingAction, out int /* 中文含义：退出中的序号。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ outgoingSequence))
        {
            return false;
        }

        InterruptWeaponTransactionForFullBody();
        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            fullBodyActionType = FullBodyActionType.Hit,
            fullBodyPhase = FullBodyTransactionPhase.PlayingHit,
            outgoingFullBodyActionType = outgoingAction,
            outgoingStateEntrySequence = outgoingSequence
        };
        hasActiveFullBodyTx = true;
        driver.BeginHit(direction);
        // receiverValue74（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)。
global::LeonActionMotionDriver receiverValue74 = actionMotionDriver;
        if (!global::System.Object.ReferenceEquals(receiverValue74, null))
        {
            receiverValue74.BeginHitMotion();
        }

        // receiverValue75（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)。
global::LeonAnimRig receiverValue75 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue75, null))
        {
            receiverValue75.EnterHitIK();
        }

        return true;
    }

    /// <summary>
    /// 请求窗口翻越全身动作。翻越不抢占换弹、切枪或其他全身动作。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool RequestTraversal()
    {
        return RequestTraversal(FullBodyActionType.WindowClimbThrough);
    }

    internal bool RequestVillageGateOpening()
    {
        return RequestTraversal(FullBodyActionType.VillageGateOpen);
    }

    internal bool CanRequestLadderPush()
    {
        return ValidateDriver() && CheckActionConflict(ActionKind.Traversal) == RejectionReason.None;
    }

    internal bool RequestLadderPush()
    {
        return RequestLadderAction(FullBodyActionType.LadderPush);
    }

    internal bool RequestEnvironmentalAction(FullBodyActionType action)
    {
        return IsEnvironmentalAction(action) && RequestLadderAction(action);
    }

    internal bool RequestEnvironmentalFall()
    {
        if (!ValidateDriver() || isDead || hasActiveFullBodyTx)
        {
            return false;
        }

        InterruptWeaponTransactionForFullBody();
        return RequestEnvironmentalAction(FullBodyActionType.EnvironmentalFall);
    }

    private static bool IsEnvironmentalAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.CabinetPush || action == FullBodyActionType.TrapCatch ||
            action == FullBodyActionType.TrapEscape || action == FullBodyActionType.EnvironmentalFall ||
            action == FullBodyActionType.DoorBrace;
    }

    internal bool RequestLadderTraversal(bool interruptWeapon = false)
    {
        if (interruptWeapon && ValidateDriver() && !isDead && !hasActiveFullBodyTx)
        {
            InterruptWeaponTransactionForFullBody();
        }
        return RequestLadderAction(FullBodyActionType.LadderTraversal);
    }

    private bool RequestLadderAction(FullBodyActionType action)
    {
        if (!CanRequestLadderPush())
        {
            return false;
        }

        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            fullBodyActionType = action,
            fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal
        };
        hasActiveFullBodyTx = true;
        return true;
    }

    internal bool IsLadderActionActive => OwnsLadderAction(fullBodyTx.transactionId, FullBodyActionType.LadderTraversal)
        || OwnsLadderAction(fullBodyTx.transactionId, FullBodyActionType.LadderPush);

    internal bool OwnsLadderPush(int transaction)
    {
        return OwnsLadderAction(transaction, FullBodyActionType.LadderPush);
    }

    internal bool OwnsLadderAction(int transaction, FullBodyActionType action)
    {
        return !isDead && hasActiveFullBodyTx && !fullBodyTx.cancelled &&
            fullBodyTx.transactionId == transaction && fullBodyTx.fullBodyActionType == action;
    }

    internal void EndLadderPush(int transaction, bool interrupted)
    {
        EndLadderAction(transaction, FullBodyActionType.LadderPush, interrupted);
    }

    internal void EndLadderAction(int transaction, FullBodyActionType action, bool interrupted)
    {
        if (!OwnsLadderAction(transaction, action))
        {
            return;
        }

        var completed = fullBodyTx;
        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        if (interrupted)
        {
            completed.cancelled = true;
            completed.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
            if (OnTraversalActionInterrupted != null)
            {
                OnTraversalActionInterrupted(completed);
            }
        }
        else
        {
            completed.completed = true;
            if (OnTraversalActionCompleted != null)
            {
                OnTraversalActionCompleted(completed);
            }
        }
    }

    internal bool RequestItemPickup(int height, bool moving)
    {
        if (!ValidateDriver() || !driver.PrepareItemPickup(height, moving))
        {
            return false;
        }

        return RequestTraversal(FullBodyActionType.ItemPickup);
    }

    internal void CompleteItemPickup(int transactionId)
    {
        if (isDead || !IsItemPickupActive || fullBodyTx.transactionId != transactionId
            || !fullBodyTx.entryBound || fullBodyTx.completed || fullBodyTx.cancelled)
        {
            return;
        }

        fullBodyTx.completed = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Completed;
        var completed = fullBodyTx;
        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        driver.EndTraversalLanding();
        var pickupCompleted = OnTraversalActionCompleted;
        if (pickupCompleted != null)
        {
            pickupCompleted(completed);
        }
    }

    private bool RequestTraversal(FullBodyActionType action)
    {
        if (!ValidateDriver() || CheckActionConflict(ActionKind.Traversal) != RejectionReason.None)
        {
            return false;
        }

        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            fullBodyActionType = action,
            fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal
        };
        hasActiveFullBodyTx = true;
        if (driver.BeginFullBodyAction(action))
        {
            return true;
        }

        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        return false;
    }

    /// <summary>
    /// 玩家从可行平台边缘自然走出时建立高落差下落事务。
    /// 此入口不依赖窗口交互，也不会创建新的交互提示。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.BeginDrop()；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool RequestAutomaticDrop()
    {
        if (!ValidateDriver() || isDead || hasActiveFullBodyTx)
        {
            return false;
        }
        InterruptWeaponTransactionForFullBody();
        if (CheckActionConflict(ActionKind.Traversal) != RejectionReason.None)
        {
            return false;
        }

        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            fullBodyActionType = FullBodyActionType.WindowDropFall,
            fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal
        };
        hasActiveFullBodyTx = true;
        if (driver.BeginFullBodyAction(FullBodyActionType.WindowDropFall))
        {
            return true;
        }

        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        return false;
    }

    /// <summary>
    /// 在同一窗口事务中推进翻窗、下落与着地动作。
    /// 旧状态退出通知通过离场序号过滤，不会被误判为中断。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.UpdateActiveDrop()、LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.RequestLandingAction()；下游（含内部回调体） LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.PrepareTraversalLandingBasePose()、LeonAnimatorDriver.ReleaseTraversalLandingBasePose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nextAction">中文含义：下一次动作。使用位置：作为 LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float) 的参数、LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)。</param>
    /// <param name="transitionDuration">中文含义：过渡时长。使用位置：作为 LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float) 的参数。</param>
    /// <param name="normalizedTime">中文含义：动画归一化播放时间。使用位置：作为 LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float) 的参数。</param>
    public bool AdvanceTraversalAction(FullBodyActionType nextAction, float transitionDuration, float normalizedTime = 0f)
    {
        if (isDead || !hasActiveFullBodyTx || !IsTraversalAction(nextAction) || !IsTraversalAction(fullBodyTx.fullBodyActionType) || !fullBodyTx.entryBound || fullBodyTx.cancelled)
        {
            return false;
        }

        // preparesLanding（是否准备的落地）：使用位置：LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)。
bool preparesLanding = nextAction == FullBodyActionType.WindowDropLand;
        if (preparesLanding)
        {
            driver.PrepareTraversalLandingBasePose();
        }

        // previousAction（前一次动作）：使用位置：LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)。
FullBodyActionType previousAction = fullBodyTx.fullBodyActionType;
        // previousSequence（前一次序号）：使用位置：LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)。
int previousSequence = fullBodyTx.stateEntrySequence;
        fullBodyTx.outgoingFullBodyActionType = previousAction;
        fullBodyTx.outgoingStateEntrySequence = previousSequence;
        fullBodyTx.fullBodyActionType = nextAction;
        fullBodyTx.stateEntrySequence = 0;
        fullBodyTx.entryBound = false;
        fullBodyTx.completed = false;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.PlayingTraversal;
        if (driver.AdvanceFullBodyAction(nextAction, transitionDuration, normalizedTime))
        {
            return true;
        }

        fullBodyTx.fullBodyActionType = previousAction;
        fullBodyTx.stateEntrySequence = previousSequence;
        fullBodyTx.entryBound = previousSequence != 0;
        if (preparesLanding)
        {
            driver.ReleaseTraversalLandingBasePose();
        }

        return false;
    }

    /// <summary>
    /// 翻越状态未进入或超时后的保险取消入口。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.Update()、LeonWindowTraversal.BeginAutomaticDrop()、LeonWindowTraversal.Update()；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、LeonAnimatorDriver.ReleaseTraversalLandingBasePose()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CancelTraversal()
    {
        if (!hasActiveFullBodyTx || !IsTraversalAction(fullBodyTx.fullBodyActionType))
        {
            return false;
        }

        fullBodyTx.cancelled = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
        // receiverValue76（判空前缓存的全身动作中断时的通知回调）：从全身动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelTraversal()。
global::System.Action<global::AnimationActionTransaction> receiverValue76 = OnFullBodyActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue76, null))
        {
            receiverValue76.Invoke(fullBodyTx);
        }

        // receiverValue77（判空前缓存的通行动作中断时的通知回调）：从通行动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelTraversal()。
global::System.Action<global::AnimationActionTransaction> receiverValue77 = OnTraversalActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue77, null))
        {
            receiverValue77.Invoke(fullBodyTx);
        }

        hasActiveFullBodyTx = false;
        // receiverValue78（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelTraversal()。
global::LeonAnimatorDriver receiverValue78 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue78, null))
        {
            receiverValue78.EndFullBodyAction();
        }

        // receiverValue79（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelTraversal()。
global::LeonAnimatorDriver receiverValue79 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue79, null))
        {
            receiverValue79.ReleaseTraversalLandingBasePose();
        }

        // receiverValue80（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelTraversal()。
global::LeonAnimatorDriver receiverValue80 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue80, null))
        {
            receiverValue80.FinalizeFullBodyActionLayer();
        }

        return true;
    }

    /// <summary>
    /// 请求一个小刀或体术全身动作。
    /// 体术可携带后续硬直快速反应事件的会话编号。
    /// </summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.PrepareTransaction(bool)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimRig.BeginKnifeActionIK()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int RequestIndependentKnifeTransaction()
    {
        if (!ValidateDriver() || CheckActionConflict(ActionKind.Melee) != RejectionReason.None)
        {
            return -1;
        }

        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            fullBodyActionType = FullBodyActionType.KnifeQuickAttack,
            fullBodyPhase = FullBodyTransactionPhase.PlayingMelee
        };
        hasActiveFullBodyTx = true;
        // receiverValue81（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()。
global::LeonAnimRig receiverValue81 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue81, null))
        {
            receiverValue81.BeginKnifeActionIK();
        }

        return fullBodyTx.transactionId;
    }

    /// <summary>AdvanceIndependentKnifeTransaction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.CommitCurrentAnimation()；下游（含内部回调体） LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">中文含义：编号。使用位置：LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)。</param>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float) 的参数。</param>
    /// <param name="blend">中文含义：混合。使用位置：作为 LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float) 的参数。</param>
    /// <param name="offset">中文含义：偏移量。使用位置：作为 LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float) 的参数。</param>
    public bool AdvanceIndependentKnifeTransaction(int id, KnifeActionConfiguration clip, float blend, float offset)
    {
        return !isDead && hasActiveFullBodyTx && !fullBodyTx.cancelled && fullBodyTx.transactionId == id && IsKnifeAction(fullBodyTx.fullBodyActionType) && driver.PlayIndependentKnifeClip(clip, blend, offset);
    }

    /// <summary>EndIndependentKnifeTransaction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimatorDriver.EndIndependentKnifeClip()、LeonAnimRig.EndKnifeActionIK()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.EndTransaction(bool)；下游（含内部回调体） LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimatorDriver.EndIndependentKnifeClip()、LeonAnimRig.EndKnifeActionIK()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">中文含义：编号。使用位置：LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)。</param>
    public void EndIndependentKnifeTransaction(int id, bool releasedThrust = false)
    {
        if (!hasActiveFullBodyTx || fullBodyTx.transactionId != id || !IsKnifeAction(fullBodyTx.fullBodyActionType))
        {
            return;
        }

        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        // receiverValue82（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)。
global::LeonAnimatorDriver receiverValue82 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue82, null))
        {
            receiverValue82.EndIndependentKnifeClip(releasedThrust);
        }

        // receiverValue83（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)。
global::LeonAnimRig receiverValue83 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue83, null))
        {
            receiverValue83.EndKnifeActionIK();
        }
    }

    /// <summary>AsKnifeInterruptReload 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.PrepareTransaction(bool)；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool CanPrepareKnifeGuard()
    {
        if (!isActiveAndEnabled || isDead || driver == null || !driver.IsValid || hasPendingWeaponSwitch
            || hasActiveFullBodyTx)
        {
            return false;
        }

        if (hasActiveWeaponTx)
        {
            return weaponTx.weaponActionType == WeaponActionType.Reload;
        }

        return !waitingForWeaponExit;
    }

    public bool AsKnifeInterruptReload()
    {
        return InterruptReloadForFullBodyAction();
    }

    private bool InterruptReloadForFullBodyAction()
    {
        if (isDead || hasPendingWeaponSwitch || hasActiveWeaponTx && weaponTx.weaponActionType != WeaponActionType.Reload)
        {
            return false;
        }

        if (hasActiveWeaponTx)
        {
            // hasCancel（是否具有取消）：使用位置：作为 Action<AnimationActionTransaction>.Invoke(AnimationActionTransaction) 的参数、作为 LeonAnimRig.InterruptWeaponActionIK(WeaponType) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()。
var hasCancel = weaponTx;
            hasCancel.cancelled = true;
            hasCancel.phase = WeaponTransactionPhase.Cancelled;
            hasActiveWeaponTx = false;
            waitingForWeaponExit = false;
            weaponTx = default;
            // receiverValue84（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()。
global::System.Action<global::AnimationActionTransaction> receiverValue84 = OnWeaponActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue84, null))
            {
                receiverValue84.Invoke(hasCancel);
            }

            // receiverValue85（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()。
global::LeonAnimatorDriver receiverValue85 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue85, null))
            {
                receiverValue85.EndWeaponAction();
            }

            // receiverValue86（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()。
global::LeonAnimatorDriver receiverValue86 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue86, null))
            {
                receiverValue86.FinalizeWeaponActionLayer();
            }

            // receiverValue87（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.AsKnifeInterruptReload()。
global::LeonAnimRig receiverValue87 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue87, null))
            {
                receiverValue87.InterruptWeaponActionIK(hasCancel.sourceWeaponType);
            }
        }

        return !waitingForWeaponExit;
    }

    /// <summary>RequestMeleeAction 的业务处理入口；本方法直接依赖 AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimRig.BeginKnifeActionIK() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.StartMeleeAction(FullBodyActionType, int)；下游（含内部回调体） AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimRig.BeginKnifeActionIK() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType) 的参数 等共 4 处。</param>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)。</param>
    public bool RequestMeleeAction(FullBodyActionType actionType, int qteSessionId = 0, float normalizedStart = 0f)
    {
        if (!IsMeleeAction(actionType) || !ValidateDriver() || CheckActionConflict(ActionKind.Melee) != RejectionReason.None)
        {
            return false;
        }

        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            qteSessionId = qteSessionId,
            fullBodyActionType = actionType,
            fullBodyPhase = FullBodyTransactionPhase.PlayingMelee
        };
        hasActiveFullBodyTx = true;
        if (IsKnifeAction(actionType))
        {
            // receiverValue88（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)。
global::LeonAnimRig receiverValue88 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue88, null))
            {
                receiverValue88.BeginKnifeActionIK();
            }
        }

        if (driver.BeginFullBodyAction(actionType, normalizedStart))
        {
            return true;
        }

        if (IsKnifeAction(actionType))
        {
            // receiverValue89（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)。
global::LeonAnimRig receiverValue89 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue89, null))
            {
                receiverValue89.EndKnifeActionIK();
            }
        }

        hasActiveFullBodyTx = false;
        fullBodyTx = default;
        return false;
    }

    /// <summary>
    /// 在当前动作正常完成后，无空状态间隔地推进到下一个近战动作。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.PrepareMeleeTransition(FullBodyActionType)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nextAction">中文含义：下一次动作。使用位置：作为 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.PrepareMeleeTransition(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType) 的参数。</param>
    public bool AdvanceMeleeAction(FullBodyActionType nextAction)
    {
        if (isDead || !hasActiveFullBodyTx || !IsMeleeAction(nextAction) || !IsMeleeAction(fullBodyTx.fullBodyActionType) || !fullBodyTx.entryBound || !fullBodyTx.completed || fullBodyTx.cancelled)
        {
            return false;
        }

        PrepareMeleeTransition(nextAction);
        return driver.BeginFullBodyAction(nextAction);
    }

    /// <summary>CompleteMeleeAction 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnMeleeActionCompleted(AnimationActionTransaction)；下游（含内部回调体） LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CompleteMeleeAction()
    {
        if (isDead || !hasActiveFullBodyTx || !IsMeleeAction(fullBodyTx.fullBodyActionType) || !fullBodyTx.entryBound || !fullBodyTx.completed || fullBodyTx.cancelled)
        {
            return false;
        }

        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForMeleeExit;
        driver.EndFullBodyAction();
        return true;
    }

    /// <summary>PrepareMeleeTransition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nextAction">中文含义：下一次动作。使用位置：LeonAnimationActionCoordinator.PrepareMeleeTransition(FullBodyActionType)。</param>
    private void PrepareMeleeTransition(FullBodyActionType nextAction)
    {
        fullBodyTx.outgoingFullBodyActionType = fullBodyTx.fullBodyActionType;
        fullBodyTx.outgoingStateEntrySequence = fullBodyTx.stateEntrySequence;
        fullBodyTx.fullBodyActionType = nextAction;
        fullBodyTx.stateEntrySequence = 0;
        fullBodyTx.entryBound = false;
        fullBodyTx.completed = false;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.PlayingMelee;
    }

    /// <summary>
    /// 建立正面抓取全身事务。四个阶段共用同一事务编号和快速反应会话编号。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)；下游（含内部回调体） Action&lt;int, bool&gt;.Invoke(int, bool)、AnimationActionTransaction.AnimationActionTransaction()、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)、LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 Action&lt;int, bool&gt;.Invoke(int, bool) 的参数、LeonAnimationActionCoordinator.RequestQTE(int)。</param>
    public bool RequestQTE(int qteSessionId) => RequestQTE(qteSessionId, true);

    internal bool RequestQTE(int qteSessionId, bool useControllerPose)
    {
        if (qteSessionId <= 0 || !ValidateDriver() || CheckActionConflict(ActionKind.PairedActionAction) != RejectionReason.None)
        {
            return false;
        }

        if (!TryInterruptMeleeForPriority(out FullBodyActionType /* 中文含义：退出中的动作。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ outgoingAction, out int /* 中文含义：退出中的序号。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ outgoingSequence))
        {
            return false;
        }

        InterruptWeaponTransactionForFullBody();
        fullBodyTx = new AnimationActionTransaction
        {
            transactionId = nextTxId++,
            qteSessionId = qteSessionId,
            fullBodyActionType = FullBodyActionType.QTEEnter,
            fullBodyPhase = FullBodyTransactionPhase.PlayingQTEEnter,
            outgoingFullBodyActionType = outgoingAction,
            outgoingStateEntrySequence = outgoingSequence
        };
        hasActiveFullBodyTx = true;
        if (useControllerPose && !driver.BeginFullBodyAction(FullBodyActionType.QTEEnter))
        {
            hasActiveFullBodyTx = false;
            fullBodyTx = default;
            return false;
        }

        qteWeaponHiddenSessionId = qteSessionId;
        // receiverValue90（判空前缓存的配对动作武器可见性请求的时的通知回调）：从配对动作武器可见性请求的时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestQTE(int)。
global::System.Action<int, bool> receiverValue90 = OnQTEWeaponVisibilityRequested;
        if (!global::System.Object.ReferenceEquals(receiverValue90, null))
        {
            receiverValue90.Invoke(qteSessionId, false);
        }

        // receiverValue91（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestQTE(int)。
global::LeonAnimRig receiverValue91 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue91, null))
        {
            receiverValue91.EnterQTEIK();
        }

        return true;
    }

    /// <summary>
    /// 会话开始前覆盖里昂快速反应四个动作片段，不修改正式控制器资源。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="variant">中文含义：变体。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonAnimationActionCoordinator.ApplyQTEAnimationVariant(QTEAnimationVariant)。</param>
    public bool ApplyQTEAnimationVariant(QTEAnimationVariant variant)
    {
        if (variant == null || animator == null)
        {
            return false;
        }

        qteAnimationOverrideClearPending = false;
        return qteAnimationOverride.Apply(animator, variant.playerEnter, variant.playerHold, variant.playerSuccess, variant.playerFailure);
    }

    /// <summary>ClearQTEAnimationVariant 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.TryClearQTEAnimationVariantAfterFullBodyExit()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） LeonAnimationActionCoordinator.TryClearQTEAnimationVariantAfterFullBodyExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearQTEAnimationVariant()
    {
        // 全身层仍在淡出时恢复原控制器，会把该层当前的刺杀片段
        // 立即替换成默认片段，造成收尾阶段的姿态闪烁。
        // 保持覆盖到图层权重归零，再安全恢复正式控制器。
        qteAnimationOverrideClearPending = true;
        TryClearQTEAnimationVariantAfterFullBodyExit();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.TryClearQTEAnimationVariantAfterFullBodyExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        TryClearQTEAnimationVariantAfterFullBodyExit();
    }

    /// <summary>TryClearQTEAnimationVariantAfterFullBodyExit 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、QTEAnimatorOverrideScope.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ClearQTEAnimationVariant()、LeonAnimationActionCoordinator.LateUpdate()；下游（含内部回调体） Animator.GetLayerWeight(int)、QTEAnimatorOverrideScope.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void TryClearQTEAnimationVariantAfterFullBodyExit()
    {
        if (!qteAnimationOverrideClearPending)
        {
            return;
        }

        if (animator != null && animator.GetLayerWeight(LeonAnimatorDriver.LayerFullBodyAction) > 0.001f)
        {
            return;
        }

        qteAnimationOverrideClearPending = false;
        qteAnimationOverride.Clear();
    }

    /// <summary>AdvanceQTEPhase 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType)、LeonAnimationActionCoordinator.ToQTEPhase(FullBodyActionType)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType)、LeonAnimationActionCoordinator.ToQTEPhase(FullBodyActionType)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)。</param>
    /// <param name="nextAction">中文含义：下一次动作。使用位置：作为 LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType) 的参数、作为 LeonAnimationActionCoordinator.ToQTEPhase(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType) 的参数 等共 4 处。</param>
    public bool AdvanceQTEPhase(int qteSessionId, FullBodyActionType nextAction)
    {
        if (isDead || !hasActiveFullBodyTx)
        {
            return false;
        }

        if (fullBodyTx.qteSessionId != qteSessionId || fullBodyTx.cancelled)
        {
            return false;
        }

        if (!fullBodyTx.entryBound || !fullBodyTx.completed)
        {
            return false;
        }

        if (!IsLegalQTETransition(fullBodyTx.fullBodyActionType, nextAction))
        {
            return false;
        }

        fullBodyTx.outgoingFullBodyActionType = fullBodyTx.fullBodyActionType;
        fullBodyTx.outgoingStateEntrySequence = fullBodyTx.stateEntrySequence;
        fullBodyTx.fullBodyActionType = nextAction;
        fullBodyTx.stateEntrySequence = 0;
        fullBodyTx.entryBound = false;
        fullBodyTx.completed = false;
        fullBodyTx.fullBodyPhase = ToQTEPhase(nextAction);
        return driver.BeginFullBodyAction(nextAction);
    }

    /// <summary>CompleteQTE 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.EndFullBodyAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryAdvanceAfterPairCompletion()；下游（含内部回调体） LeonAnimatorDriver.EndFullBodyAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：LeonAnimationActionCoordinator.CompleteQTE(int)。</param>
    public bool CompleteQTE(int qteSessionId)
    {
        if (isDead || !hasActiveFullBodyTx)
        {
            return false;
        }

        if (fullBodyTx.qteSessionId != qteSessionId || fullBodyTx.cancelled)
        {
            return false;
        }

        if (!fullBodyTx.entryBound || !fullBodyTx.completed)
        {
            return false;
        }

        if (fullBodyTx.fullBodyActionType != FullBodyActionType.QTESuccess && fullBodyTx.fullBodyActionType != FullBodyActionType.QTEFail)
        {
            return false;
        }

        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForQTEExit;
        driver.EndFullBodyAction();
        return true;
    }

    /// <summary>CancelQTE 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.Cancel(int, QTECancelReason)；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int) 的参数、LeonAnimationActionCoordinator.CancelQTE(int)。</param>
    public bool CancelQTE(int qteSessionId)
    {
        if (!hasActiveFullBodyTx || fullBodyTx.qteSessionId != qteSessionId)
        {
            return false;
        }

        // wasAlreadyCancelled（之前是否已经已取消）：使用位置：LeonAnimationActionCoordinator.CancelQTE(int)。
bool wasAlreadyCancelled = fullBodyTx.cancelled;
        fullBodyTx.cancelled = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
        // receiverValue92（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelQTE(int)。
global::LeonAnimatorDriver receiverValue92 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue92, null))
        {
            receiverValue92.EndFullBodyAction();
        }

        RestoreQTEWeaponVisual(qteSessionId);
        if (!wasAlreadyCancelled)
        {
            // receiverValue93（判空前缓存的配对动作动作中断时的通知回调）：从配对动作动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelQTE(int)。
global::System.Action<global::AnimationActionTransaction> receiverValue93 = OnQTEActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue93, null))
            {
                receiverValue93.Invoke(fullBodyTx);
            }
        }

        if (!fullBodyTx.entryBound)
        {
            hasActiveFullBodyTx = false;
            // receiverValue94（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CancelQTE(int)。
global::LeonAnimatorDriver receiverValue94 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue94, null))
            {
                receiverValue94.FinalizeFullBodyActionLayer();
            }
        }

        return true;
    }

    /// <summary>中文含义：配对动作成功状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)。</summary>
    private static readonly int HashQTESuccessState = Animator.StringToHash("QTESuccess");
    /// <summary>
    /// 受事务身份保护的计划内恢复退出接口。
    /// 只允许当前会话的 QTESuccess 动作在达到恢复阈值后提前退出。
    /// 调用后 SMB 可能发送未完成中断通知，该通知会被 plannedExit 标记抑制。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.RequestPlayerRecoveryExit()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonAnimatorDriver.EndFullBodyAction()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)。</param>
    /// <param name="minimumNormalizedTime">中文含义：最小归一化时间。使用位置：LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)。</param>
    public bool TryExitQTESuccessAtRecoveryPoint(int qteSessionId, float minimumNormalizedTime)
    {
        if (isDead || !hasActiveFullBodyTx)
        {
            return false;
        }

        if (fullBodyTx.qteSessionId != qteSessionId)
        {
            return false;
        }

        if (fullBodyTx.fullBodyActionType != FullBodyActionType.QTESuccess)
        {
            return false;
        }

        if (fullBodyTx.cancelled || fullBodyTx.plannedExit)
        {
            return false;
        }

        if (!fullBodyTx.entryBound)
        {
            return false;
        }

        if (animator == null)
        {
            return false;
        }

        // stateInfo（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)。
AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        if (stateInfo.shortNameHash != HashQTESuccessState && animator.IsInTransition(AnimatorLayerID.FullBodyAction))
        {
            stateInfo = animator.GetNextAnimatorStateInfo(AnimatorLayerID.FullBodyAction);
        }

        if (stateInfo.shortNameHash != HashQTESuccessState)
        {
            return false;
        }

        if (stateInfo.normalizedTime < minimumNormalizedTime)
        {
            return false;
        }

        fullBodyTx.plannedExit = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForQTEExit;
        // receiverValue95（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.TryExitQTESuccessAtRecoveryPoint(int, float)。
global::LeonAnimatorDriver receiverValue95 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue95, null))
        {
            receiverValue95.EndFullBodyAction();
        }

        return true;
    }

    /// <summary>
    /// 强制收尾 QTE 成功动作，用于退出通知缺失时的安全兜底。
    /// 直接清零参数、降低全身层权重、恢复武器视觉并发送退出通知。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.ForceFinishQTE(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 LeonAnimationActionCoordinator.ForceFinishQTE(int) 的参数、LeonAnimationActionCoordinator.ForceFinishQTESuccess(int)。</param>
    public bool ForceFinishQTESuccess(int qteSessionId)
    {
        if (!hasActiveFullBodyTx || fullBodyTx.qteSessionId != qteSessionId)
        {
            return false;
        }

        if (fullBodyTx.fullBodyActionType != FullBodyActionType.QTESuccess)
        {
            return false;
        }

        return ForceFinishQTE(qteSessionId);
    }

    /// <summary>
    /// 强制收尾当前快速反应事务。
    /// 用于暗杀等变体已经进入结束节点，但事务仍停留在快速反应全身层时兜底释放。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ForceFinishQTESuccess(int)、LeonQTECoordinator.HandlePlayerExitTimeout()、LeonQTECoordinator.ReleasePlayer(int)、LeonQTECoordinator.TryFinalizeCommittedAssassination()；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int) 的参数、LeonAnimationActionCoordinator.ForceFinishQTE(int)。</param>
    public bool ForceFinishQTE(int qteSessionId)
    {
        if (qteSessionId <= 0)
        {
            return false;
        }

        if (!hasActiveFullBodyTx)
        {
            // 状态退出通知已经启动快速反应事件图层淡出时，事务会先被标记为结束。
            // 此处若再次强制终结，会把尚在淡出的图层直接归零，形成单帧闪烁。
            RestoreQTEWeaponVisual(qteSessionId);
            return true;
        }

        if (fullBodyTx.qteSessionId != qteSessionId)
        {
            return false;
        }

        if (!IsQTEAction(fullBodyTx.fullBodyActionType))
        {
            return false;
        }

        fullBodyTx.plannedExit = true;
        fullBodyTx.completed = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.WaitingForQTEExit;
        // receiverValue96（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceFinishQTE(int)。
global::LeonAnimatorDriver receiverValue96 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue96, null))
        {
            receiverValue96.EndFullBodyAction();
        }

        // exitedTx（已退出动作事务）：使用位置：作为 Action<AnimationActionTransaction>.Invoke(AnimationActionTransaction) 的参数、LeonAnimationActionCoordinator.ForceFinishQTE(int)。
AnimationActionTransaction exitedTx = fullBodyTx;
        exitedTx.fullBodyPhase = FullBodyTransactionPhase.Completed;
        hasActiveFullBodyTx = false;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Completed;
        // receiverValue97（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceFinishQTE(int)。
global::LeonAnimatorDriver receiverValue97 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue97, null))
        {
            receiverValue97.FinalizeFullBodyActionLayer();
        }

        RestoreQTEWeaponVisual(qteSessionId);
        // receiverValue98（判空前缓存的配对动作动作退出时的通知回调）：从配对动作动作退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceFinishQTE(int)。
global::System.Action<global::AnimationActionTransaction> receiverValue98 = OnQTEActionExited;
        if (!global::System.Object.ReferenceEquals(receiverValue98, null))
        {
            receiverValue98.Invoke(exitedTx);
        }

        return true;
    }

    /// <summary>ForceClearWeaponActionBlockAfterQTE 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.CompleteWeaponActionIK(WeaponType)、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.CompleteWeaponActionIK(WeaponType)、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="restoredWeapon">中文含义：已恢复武器。使用位置：作为 LeonAnimRig.CompleteWeaponActionIK(WeaponType) 的参数、作为 LeonAnimRig.InterruptWeaponActionIK(WeaponType) 的参数。</param>
    public void ForceClearWeaponActionBlockAfterQTE(WeaponType restoredWeapon)
    {
        if (isDead)
        {
            return;
        }

        if (!hasActiveWeaponTx && !hasPendingWeaponSwitch && !waitingForWeaponExit)
        {
            return;
        }

        if (hasActiveWeaponTx)
        {
            weaponTx.cancelled = true;
            weaponTx.phase = WeaponTransactionPhase.Cancelled;
            // receiverValue99（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)。
global::System.Action<global::AnimationActionTransaction> receiverValue99 = OnWeaponActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue99, null))
            {
                receiverValue99.Invoke(weaponTx);
            }
        }

        hasActiveWeaponTx = false;
        hasPendingWeaponSwitch = false;
        waitingForWeaponExit = false;
        pendingWeaponType = WeaponType.Pistol;
        // receiverValue100（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)。
global::LeonAnimatorDriver receiverValue100 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue100, null))
        {
            receiverValue100.EndWeaponAction();
        }

        // receiverValue101（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)。
global::LeonAnimatorDriver receiverValue101 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue101, null))
        {
            receiverValue101.FinalizeWeaponActionLayer();
        }

        if (animRig != null)
        {
            animRig.InterruptWeaponActionIK(restoredWeapon);
            animRig.CompleteWeaponActionIK(restoredWeapon);
        }
    }

    /// <summary>InterruptWeaponTransactionForFullBody 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)、LeonAnimationActionCoordinator.RequestQTE(int)；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimatorDriver.EndWeaponAction()、LeonAnimatorDriver.FinalizeWeaponActionLayer()、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InterruptWeaponTransactionForFullBody()
    {
        if (!hasActiveWeaponTx)
        {
            return;
        }

        // interruptWeapon（中断武器）：使用位置：作为 LeonAnimRig.InterruptWeaponActionIK(WeaponType) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()。
WeaponType interruptWeapon;
        if (animRig != null)
        {
            interruptWeapon = animRig.CurrentWeapon;
        }
        else
        {
            interruptWeapon = WeaponType.Pistol;
        }

        if (weaponTx.isWeaponSwitch && weaponTx.visualCommitDone)
        {
            interruptWeapon = weaponTx.pendingWeaponType;
        }

        weaponTx.cancelled = true;
        weaponTx.phase = WeaponTransactionPhase.Cancelled;
        hasActiveWeaponTx = false;
        hasPendingWeaponSwitch = false;
        // receiverValue103（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()。
global::System.Action<global::AnimationActionTransaction> receiverValue103 = OnWeaponActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue103, null))
        {
            receiverValue103.Invoke(weaponTx);
        }

        // receiverValue104（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()。
global::LeonAnimatorDriver receiverValue104 = driver;
        if (!global::System.Object.ReferenceEquals(receiverValue104, null))
        {
            receiverValue104.EndWeaponAction();
        }

        if (weaponTx.entryBound)
        {
            waitingForWeaponExit = true;
        }
        else
        {
            waitingForWeaponExit = false;
            // receiverValue105（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()。
global::LeonAnimatorDriver receiverValue105 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue105, null))
            {
                receiverValue105.FinalizeWeaponActionLayer();
            }
        }

        // receiverValue106（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()。
global::LeonAnimRig receiverValue106 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue106, null))
        {
            receiverValue106.InterruptWeaponActionIK(interruptWeapon);
        }
    }

    /// <summary>TryInterruptMeleeForPriority 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimRig.EndKnifeActionIK()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)、LeonAnimationActionCoordinator.RequestQTE(int)；下游（含内部回调体） Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)、LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)、LeonAnimRig.EndKnifeActionIK()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="outgoingAction">中文含义：退出中的动作。输出参数，由本入口写入并交给调用方。使用位置：作为 LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType) 的参数、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)。</param>
    /// <param name="outgoingSequence">中文含义：退出中的序号。输出参数，由本入口写入并交给调用方。使用位置：LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)。</param>
    private bool TryInterruptMeleeForPriority(out FullBodyActionType outgoingAction, out int outgoingSequence)
    {
        outgoingAction = FullBodyActionType.None;
        outgoingSequence = 0;
        if (!hasActiveFullBodyTx)
        {
            return true;
        }

        if (!IsMeleeAction(fullBodyTx.fullBodyActionType))
        {
            return false;
        }

        outgoingAction = fullBodyTx.fullBodyActionType;
        outgoingSequence = fullBodyTx.stateEntrySequence;
        if (IsKnifeAction(outgoingAction))
        {
            // receiverValue107（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)。
global::LeonAnimRig receiverValue107 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue107, null))
            {
                receiverValue107.EndKnifeActionIK();
            }
        }

        fullBodyTx.cancelled = true;
        fullBodyTx.fullBodyPhase = FullBodyTransactionPhase.Cancelled;
        // receiverValue108（判空前缓存的全身动作中断时的通知回调）：从全身动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)。
global::System.Action<global::AnimationActionTransaction> receiverValue108 = OnFullBodyActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue108, null))
        {
            receiverValue108.Invoke(fullBodyTx);
        }

        // receiverValue109（判空前缓存的近战动作已中断时的通知回调）：从近战动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)。
global::System.Action<global::AnimationActionTransaction> receiverValue109 = OnMeleeActionInterrupted;
        if (!global::System.Object.ReferenceEquals(receiverValue109, null))
        {
            receiverValue109.Invoke(fullBodyTx);
        }

        hasActiveFullBodyTx = false;
        return true;
    }

    /// <summary>RequestDeath 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.BeginDeathMotion()、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.EnterDeath()、LeonAnimRig.EnterDeathIK() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.HandleDeath()；下游（含内部回调体） Action.Invoke()、Action&lt;AnimationActionTransaction&gt;.Invoke(AnimationActionTransaction)、LeonActionMotionDriver.BeginDeathMotion()、LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)、LeonAnimatorDriver.EnterDeath()、LeonAnimRig.EnterDeathIK() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool completedPairDeathPose;

    internal void UseCompletedPairDeathPose()
    {
        completedPairDeathPose = true;
    }

    public void RequestDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        deathStateEntrySequence = 0;
        deathAnimationCompletionSent = false;
        if (hasActiveWeaponTx)
        {
            weaponTx.cancelled = true;
            weaponTx.phase = WeaponTransactionPhase.Cancelled;
            hasActiveWeaponTx = false;
            // receiverValue110（判空前缓存的武器动作已中断时的通知回调）：从武器动作已中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::System.Action<global::AnimationActionTransaction> receiverValue110 = OnWeaponActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue110, null))
            {
                receiverValue110.Invoke(weaponTx);
            }
        }

        if (hasActiveFullBodyTx)
        {
            fullBodyTx.cancelled = true;
            if (fullBodyTx.qteSessionId > 0)
            {
                RestoreQTEWeaponVisual(fullBodyTx.qteSessionId);
                // receiverValue111（判空前缓存的配对动作动作中断时的通知回调）：从配对动作动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::System.Action<global::AnimationActionTransaction> receiverValue111 = OnQTEActionInterrupted;
                if (!global::System.Object.ReferenceEquals(receiverValue111, null))
                {
                    receiverValue111.Invoke(fullBodyTx);
                }
            }

            if (IsTraversalAction(fullBodyTx.fullBodyActionType))
            {
                // receiverValue112（判空前缓存的全身动作中断时的通知回调）：从全身动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::System.Action<global::AnimationActionTransaction> receiverValue112 = OnFullBodyActionInterrupted;
                if (!global::System.Object.ReferenceEquals(receiverValue112, null))
                {
                    receiverValue112.Invoke(fullBodyTx);
                }

                // receiverValue113（判空前缓存的通行动作中断时的通知回调）：从通行动作中断时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::System.Action<global::AnimationActionTransaction> receiverValue113 = OnTraversalActionInterrupted;
                if (!global::System.Object.ReferenceEquals(receiverValue113, null))
                {
                    receiverValue113.Invoke(fullBodyTx);
                }
            }

            hasActiveFullBodyTx = false;
        }

        hasPendingWeaponSwitch = false;
        waitingForWeaponExit = false;
        if (driver != null && !completedPairDeathPose)
        {
            driver.EnterDeath();
        }
        completedPairDeathPose = false;

        // receiverValue114（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::LeonActionMotionDriver receiverValue114 = actionMotionDriver;
        if (!global::System.Object.ReferenceEquals(receiverValue114, null))
        {
            receiverValue114.BeginDeathMotion();
        }

        // receiverValue115（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::LeonAnimRig receiverValue115 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue115, null))
        {
            receiverValue115.EnterDeathIK();
        }

        if (!deathEventSent)
        {
            deathEventSent = true;
            // receiverValue116（判空前缓存的死亡已进入时的通知回调）：从死亡已进入时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RequestDeath()。
global::System.Action receiverValue116 = OnDeathEntered;
            if (!global::System.Object.ReferenceEquals(receiverValue116, null))
            {
                receiverValue116.Invoke();
            }
        }
    }

    /// <summary>IsQTEAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.IsQTEAction(FullBodyActionType)。</param>
    private static bool IsQTEAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.QTEEnter || action == FullBodyActionType.QTEHold || action == FullBodyActionType.QTESuccess || action == FullBodyActionType.QTEFail;
    }

    /// <summary>IsMeleeAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.CheckActionConflict(ActionKind)、LeonAnimationActionCoordinator.CompleteMeleeAction()、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int) 等共 9 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.IsMeleeAction(FullBodyActionType)。</param>
    private static bool IsMeleeAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.KnifeQuickAttack || action == FullBodyActionType.KnifeParry || action == FullBodyActionType.QTERightCutKick || action == FullBodyActionType.QTESpinningHookKick
            || MeleeActionKinds.IsWoodenBox(action);
    }

    /// <summary>IsTraversalAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyEntered(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool) 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.IsTraversalAction(FullBodyActionType)。</param>
    private static bool IsTraversalAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.WindowClimbThrough || action == FullBodyActionType.WindowDropFall ||
            action == FullBodyActionType.WindowDropLand || action == FullBodyActionType.VillageGateOpen ||
            action == FullBodyActionType.ItemPickup || action == FullBodyActionType.LadderPush ||
            action == FullBodyActionType.LadderTraversal;
    }

    /// <summary>IsKnifeAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)、LeonAnimationActionCoordinator.CommitKnifeAssistOrientation(Quaternion)、LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int) 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)。</param>
    private static bool IsKnifeAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.KnifeQuickAttack || action == FullBodyActionType.KnifeParry;
    }

    /// <summary>IsMeleeAttackAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleMeleeHitWindow(Animator, FullBodyActionType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.IsMeleeAttackAction(FullBodyActionType)。</param>
    private static bool IsMeleeAttackAction(FullBodyActionType action)
    {
        return action == FullBodyActionType.KnifeQuickAttack || action == FullBodyActionType.QTERightCutKick || action == FullBodyActionType.QTESpinningHookKick
            || MeleeActionKinds.IsWoodenBox(action);
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimationActionCoordinator.IsGunWeapon(WeaponType)。</param>
    private static bool IsGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>IsLongGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)、LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType)。</param>
    private static bool IsLongGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>IsLegalQTETransition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">中文含义：当前全身动作类型。使用位置：LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType)。</param>
    /// <param name="next">中文含义：下一个全身动作类型。使用位置：LeonAnimationActionCoordinator.IsLegalQTETransition(FullBodyActionType, FullBodyActionType)。</param>
    private static bool IsLegalQTETransition(FullBodyActionType current, FullBodyActionType next)
    {
        return (current == FullBodyActionType.QTEEnter && next == FullBodyActionType.QTEHold) || (current == FullBodyActionType.QTEEnter && next == FullBodyActionType.QTESuccess) || (current == FullBodyActionType.QTEHold && (next == FullBodyActionType.QTESuccess || next == FullBodyActionType.QTEFail));
    }

    /// <summary>ToQTEPhase 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.ToQTEPhase(FullBodyActionType)。</param>
    private static FullBodyTransactionPhase ToQTEPhase(FullBodyActionType action)
    {
        switch (action)
        {
            case FullBodyActionType.QTEEnter:
                return FullBodyTransactionPhase.PlayingQTEEnter;
            case FullBodyActionType.QTEHold:
                return FullBodyTransactionPhase.PlayingQTEHold;
            case FullBodyActionType.QTESuccess:
                return FullBodyTransactionPhase.PlayingQTESuccess;
            case FullBodyActionType.QTEFail:
                return FullBodyTransactionPhase.PlayingQTEFail;
            default:
                return FullBodyTransactionPhase.None;
        }
    }

    /// <summary>RestoreQTEWeaponVisual 的业务处理入口；本方法直接依赖 Action&lt;int, bool&gt;.Invoke(int, bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CancelQTE(int)、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.RequestDeath()；下游（含内部回调体） Action&lt;int, bool&gt;.Invoke(int, bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="qteSessionId">中文含义：配对动作会话编号。使用位置：作为 Action&lt;int, bool&gt;.Invoke(int, bool) 的参数、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)。</param>
    private void RestoreQTEWeaponVisual(int qteSessionId)
    {
        if (qteSessionId <= 0 || qteWeaponHiddenSessionId != qteSessionId)
        {
            return;
        }

        qteWeaponHiddenSessionId = -1;
        // receiverValue117（判空前缓存的配对动作武器可见性请求的时的通知回调）：从配对动作武器可见性请求的时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.RestoreQTEWeaponVisual(int)。
global::System.Action<int, bool> receiverValue117 = OnQTEWeaponVisibilityRequested;
        if (!global::System.Object.ReferenceEquals(receiverValue117, null))
        {
            receiverValue117.Invoke(qteSessionId, true);
        }
    }

    // ================================================================
    // 视觉标记
    // ================================================================
    /// <summary>ResetVisualMarkers 的业务处理入口；本方法直接依赖 HashSet&lt;VisualMarker&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestReload(WeaponType)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；下游（含内部回调体） HashSet&lt;VisualMarker&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="txId">中文含义：动作事务编号。使用位置：LeonAnimationActionCoordinator.ResetVisualMarkers(int)。</param>
    private void ResetVisualMarkers(int txId)
    {
        if (visualMarkerTxId != txId)
        {
            visualMarkerTxId = txId;
            processedMarkers.Clear();
        }
    }

    /// <summary>NotifyVisualMarker 的业务处理入口；本方法直接依赖 Action&lt;AnimationActionTransaction, VisualMarker&gt;.Invoke(AnimationActionTransaction, VisualMarker)、HashSet&lt;VisualMarker&gt;.Add(VisualMarker)、HashSet&lt;VisualMarker&gt;.Clear()、HashSet&lt;VisualMarker&gt;.Contains(VisualMarker)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)、LeonAnimRig.NotifyEquipVisualRevealIK() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)、WeaponVisualEventRelay.OnEquipVisualReveal()、WeaponVisualEventRelay.OnHolsterVisualCommit()、WeaponVisualEventRelay.OnLongGunSupportHandGrip()、WeaponVisualEventRelay.OnRifleBackHideBeforeReveal()；下游（含内部回调体） Action&lt;AnimationActionTransaction, VisualMarker&gt;.Invoke(AnimationActionTransaction, VisualMarker)、HashSet&lt;VisualMarker&gt;.Add(VisualMarker)、HashSet&lt;VisualMarker&gt;.Clear()、HashSet&lt;VisualMarker&gt;.Contains(VisualMarker)、LeonAnimationActionCoordinator.IsFromThisAnimator(Animator)、LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)、LeonAnimRig.NotifyEquipVisualRevealIK() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画控制器。使用位置：作为 LeonAnimationActionCoordinator.IsFromThisAnimator(Animator) 的参数。</param>
    /// <param name="marker">中文含义：标记。使用位置：作为 Action&lt;AnimationActionTransaction, VisualMarker&gt;.Invoke(AnimationActionTransaction, VisualMarker) 的参数、作为 HashSet&lt;VisualMarker&gt;.Add(VisualMarker) 的参数、作为 HashSet&lt;VisualMarker&gt;.Contains(VisualMarker) 的参数 等共 5 处。</param>
    public void NotifyVisualMarker(Animator source, VisualMarker marker)
    {
        if (!IsFromThisAnimator(source) || isDead)
        {
            return;
        }

        if (!hasActiveWeaponTx || !weaponTx.entryBound || weaponTx.cancelled)
        {
            return;
        }

        // 同一事务同一标记只处理一次
        if (visualMarkerTxId != weaponTx.transactionId)
        {
            visualMarkerTxId = weaponTx.transactionId;
            processedMarkers.Clear();
        }

        if (processedMarkers.Contains(marker))
        {
            return;
        }

        // 事件必须在当前装备状态有效期内到达，迟到事件直接丢弃
        if (!ValidateVisualMarker(marker, weaponTx.weaponActionType, weaponTx.phase))
        {
            return;
        }

        processedMarkers.Add(marker);
        if (marker == VisualMarker.EquipVisualReveal)
        {
            // receiverValue118（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。
global::LeonAnimRig receiverValue118 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue118, null))
            {
                receiverValue118.NotifyEquipVisualRevealIK();
            }
        }
        else if (marker == VisualMarker.LongGunSupportHandGrip)
        {
            // receiverValue119（判空前缓存的当前里昂动画绑定组件）：从动画绑定组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。
global::LeonAnimRig receiverValue119 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue119, null))
            {
                receiverValue119.NotifyLongGunSupportHandGripIK();
            }
        }

        // receiverValue120（判空前缓存的视觉表现标记时的通知回调）：从视觉表现标记时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。
global::System.Action<global::AnimationActionTransaction, global::VisualMarker> receiverValue120 = OnVisualMarker;
        if (!global::System.Object.ReferenceEquals(receiverValue120, null))
        {
            receiverValue120.Invoke(weaponTx, marker);
        }
    }

    /// <summary>ValidateVisualMarker 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)；下游（含内部回调体） LeonAnimationActionCoordinator.IsLongGunWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="marker">中文含义：标记。使用位置：LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)。</param>
    /// <param name="action">中文含义：动作。使用位置：LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)。</param>
    /// <param name="phase">中文含义：阶段。使用位置：LeonAnimationActionCoordinator.ValidateVisualMarker(VisualMarker, WeaponActionType, WeaponTransactionPhase)。</param>
    private bool ValidateVisualMarker(VisualMarker marker, WeaponActionType action, WeaponTransactionPhase phase)
    {
        switch (marker)
        {
            case VisualMarker.HolsterVisualCommit:
                return action == WeaponActionType.Holster && (phase == WeaponTransactionPhase.PlayingHolster || phase == WeaponTransactionPhase.AwaitingWeaponCommit);
            case VisualMarker.RifleBackHideBeforeReveal:
                return action == WeaponActionType.Equip && IsLongGunWeapon(weaponTx.pendingWeaponType) && phase == WeaponTransactionPhase.PlayingEquip;
            case VisualMarker.EquipVisualReveal:
            case VisualMarker.EquipSettled:
                return action == WeaponActionType.Equip && phase == WeaponTransactionPhase.PlayingEquip;
            case VisualMarker.LongGunSupportHandGrip:
                return action == WeaponActionType.Equip && IsLongGunWeapon(weaponTx.pendingWeaponType) && phase == WeaponTransactionPhase.PlayingEquip;
            case VisualMarker.MagazineDetached:
            case VisualMarker.MagazineAttached:
            case VisualMarker.ReloadMechanism:
                return action == WeaponActionType.Reload && (phase == WeaponTransactionPhase.PlayingReload || phase == WeaponTransactionPhase.AwaitingReloadCommit);
            case VisualMarker.LeftHandReleased:
            case VisualMarker.LeftHandGripped:
                return (action == WeaponActionType.Reload && phase == WeaponTransactionPhase.PlayingReload) || (action == WeaponActionType.Holster && phase == WeaponTransactionPhase.PlayingHolster) || (action == WeaponActionType.Equip && phase == WeaponTransactionPhase.PlayingEquip);
            default:
                return false;
        }
    }

    // ================================================================
    // 只读状态
    // ================================================================
    /// <summary>GetCurrentWeaponForIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType GetCurrentWeaponForIK()
    {
        if (weaponTx.visualCommitDone && weaponTx.isWeaponSwitch)
        {
            return weaponTx.pendingWeaponType;
        }

        // 普通换弹没有武器切换提交，必须恢复请求发起时记录的武器。
        // 不能固定回退到手枪，否则步枪换弹退出后会错误关闭步枪左手约束。
        return weaponTx.sourceWeaponType;
    }

    /// <summary>中文含义：玩家移动。使用位置：LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState)、LeonAnimationActionCoordinator.OnDisable() 等共 4 处。</summary>
    [Header("动作状态归属")]
    [UnityEngine.Serialization.FormerlySerializedAs("玩家移动")]
    [UnityEngine.InspectorName("玩家移动")]
    public LeonLocomotion playerMovement;
    /// <summary>中文含义：玩家武器动作。使用位置：LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.PlayerSnapshot.get。</summary>
    private PlayerWeaponPhase playerWeaponAction;
    /// <summary>中文含义：玩家全身动作。使用位置：LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、LeonAnimationActionCoordinator.OnDisable()、LeonAnimationActionCoordinator.PlayerSnapshot.get。</summary>
    private PlayerFullBodyPhase playerFullBodyAction;
    /// <summary>中文含义：玩家状态是否已初始化。使用位置：LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState)。</summary>
    private bool playerStateInitialized;
    /// <summary>中文含义：玩家快照。使用位置：作为 LeonActionMotionDriver.SynchronizeNormalMotionPermission(LeonState) 的参数、LeonController.currentState.get。</summary>
    public PlayerActionSnapshot PlayerSnapshot
    {
        get
        {
            // argumentValue124（按原顺序缓存的实参玩家移动的当前移动阶段）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PlayerActionSnapshot.PlayerActionSnapshot(LeonState, PlayerWeaponPhase, PlayerFullBodyPhase, int, int) 的参数、LeonAnimationActionCoordinator.PlayerSnapshot.get。
global::LeonState argumentValue124;
            if (playerMovement != null)
            {
                argumentValue124 = playerMovement.MovementPhase;
            }
            else
            {
                argumentValue124 = LeonState.Idle;
            }

            // argumentValue125（按原顺序缓存的实参玩家武器动作）：从玩家武器动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PlayerActionSnapshot.PlayerActionSnapshot(LeonState, PlayerWeaponPhase, PlayerFullBodyPhase, int, int) 的参数。
global::PlayerWeaponPhase argumentValue125 = playerWeaponAction;
            // argumentValue126（按原顺序缓存的实参玩家全身动作）：从玩家全身动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PlayerActionSnapshot.PlayerActionSnapshot(LeonState, PlayerWeaponPhase, PlayerFullBodyPhase, int, int) 的参数。
global::PlayerFullBodyPhase argumentValue126 = playerFullBodyAction;
            // argumentValue127（按原顺序缓存的实参武器动作事务的事务编号）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PlayerActionSnapshot.PlayerActionSnapshot(LeonState, PlayerWeaponPhase, PlayerFullBodyPhase, int, int) 的参数、LeonAnimationActionCoordinator.PlayerSnapshot.get。
int argumentValue127;
            if (hasActiveWeaponTx)
            {
                argumentValue127 = weaponTx.transactionId;
            }
            else
            {
                argumentValue127 = -1;
            }

            // selectedValue123（分支选出的全身动作事务的事务编号）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PlayerActionSnapshot.PlayerActionSnapshot(LeonState, PlayerWeaponPhase, PlayerFullBodyPhase, int, int) 的参数、LeonAnimationActionCoordinator.PlayerSnapshot.get。
int selectedValue123;
            if (hasActiveFullBodyTx)
            {
                selectedValue123 = fullBodyTx.transactionId;
            }
            else
            {
                selectedValue123 = -1;
            }

            return new PlayerActionSnapshot(argumentValue124, argumentValue125, argumentValue126, argumentValue127, selectedValue123);
        }
    }

    /// <summary>InitializePlayerState 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.InitPipeline()；下游（含内部回调体） LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="movement">中文含义：移动。使用位置：LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState)。</param>
    /// <param name="initialState">中文含义：初始状态。使用位置：作为 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState) 的参数。</param>
    public void InitializePlayerState(LeonLocomotion movement, LeonState initialState)
    {
        if (playerStateInitialized)
        {
            return;
        }

        playerMovement = movement;
        playerStateInitialized = true;
        CommitCompatibleState(initialState);
    }

    /// <summary>旧入口只转发到各状态拥有者；后续新增动作通过审批事务进入。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.InitializePlayerState(LeonLocomotion, LeonState)、LeonController.ChangeState(LeonState)；下游（含内部回调体） LeonAnimationActionCoordinator.SynchronizeMotionControlAuthority()、LeonLocomotion.CommitMovementPhase(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前里昂状态。使用位置：作为 LeonLocomotion.CommitMovementPhase(LeonState) 的参数、LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)。</param>
    public void CommitCompatibleState(LeonState state)
    {
        if (playerFullBodyAction == PlayerFullBodyPhase.EndEnd)
        {
            return;
        }

        playerWeaponAction = PlayerWeaponPhase.None;
        playerFullBodyAction = PlayerFullBodyPhase.None;
        switch (state)
        {
            case LeonState.Aiming:
                playerWeaponAction = PlayerWeaponPhase.Aim;
                break;
            case LeonState.Shooting:
                playerWeaponAction = PlayerWeaponPhase.Shoot;
                break;
            case LeonState.Reloading:
                playerWeaponAction = PlayerWeaponPhase.Reload;
                break;
            case LeonState.MeleeAttacking:
                playerFullBodyAction = PlayerFullBodyPhase.Melee;
                break;
            case LeonState.Parrying:
                playerFullBodyAction = PlayerFullBodyPhase.CellBlock;
                break;
            case LeonState.Traversing:
                playerFullBodyAction = PlayerFullBodyPhase.Traversal;
                break;
            case LeonState.Staggered:
                playerFullBodyAction = PlayerFullBodyPhase.HitReaction;
                break;
            case LeonState.QTE:
                playerFullBodyAction = PlayerFullBodyPhase.PairedAction;
                break;
            case LeonState.Dead:
                playerFullBodyAction = PlayerFullBodyPhase.EndEnd;
                break;
            default:
                // receiverValue128（判空前缓存的玩家移动）：从玩家移动读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)。
global::LeonLocomotion receiverValue128 = playerMovement;
                if (!global::System.Object.ReferenceEquals(receiverValue128, null))
                {
                    receiverValue128.CommitMovementPhase(state);
                }

                break;
        }

        SynchronizeMotionControlAuthority();
    }

    /// <summary>SynchronizeMotionControlAuthority 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.SynchronizeNormalMotionPermission(LeonState)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CommitCompatibleState(LeonState)、LeonController.FixedUpdate()；下游（含内部回调体） LeonActionMotionDriver.SynchronizeNormalMotionPermission(LeonState)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SynchronizeMotionControlAuthority()
    {
        if (isActiveAndEnabled)
        {
            // receiverValue129（判空前缓存的动作运动执行器）：从动作运动执行器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimationActionCoordinator.SynchronizeMotionControlAuthority()。
global::LeonActionMotionDriver receiverValue129 = actionMotionDriver;
            if (!global::System.Object.ReferenceEquals(receiverValue129, null))
            {
                receiverValue129.SynchronizeNormalMotionPermission(PlayerSnapshot.CompatibleState);
            }
        }
    }

    /// <summary>StopCurrentHorizontalMotion 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.StopHorizontalSpeed(MotionControlToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、LeonController.BeginDeathSequence()；下游（含内部回调体） LeonActionMotionDriver.StopHorizontalSpeed(MotionControlToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopCurrentHorizontalMotion()
    {
        if (actionMotionDriver != null)
        {
            actionMotionDriver.StopHorizontalSpeed(actionMotionDriver.CurrentMotionToken);
        }
    }

    /// <summary>CommitKnifeAssistOrientation 的业务处理入口；本方法直接依赖 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.UpdateWeakSteering()；下游（含内部回调体） LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion)、LeonAnimationActionCoordinator.IsKnifeAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="orientation">中文含义：朝向。使用位置：作为 LeonActionMotionDriver.CommitNormalOrientation(MotionControlToken, Quaternion) 的参数。</param>
    public bool CommitKnifeAssistOrientation(Quaternion orientation)
    {
        if (!hasActiveFullBodyTx || !IsKnifeAction(fullBodyTx.fullBodyActionType) || actionMotionDriver == null)
        {
            return false;
        }

        SynchronizeMotionControlAuthority();
        return actionMotionDriver.CommitNormalOrientation(actionMotionDriver.CurrentMotionToken, orientation);
    }

    /// <summary>中文含义：是否死亡。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.HandleKnifeBroken()。</summary>
    public bool IsDead => isDead;
    /// <summary>中文含义：是否具有生效武器动作。使用位置：作为 Debug.LogError(object) 的参数、LeonFingerIKController.CanCaptureRuntimeTargets()、LeonFingerIKController.EvaluateTargetWeight(WeaponType) 等共 9 处。</summary>
    public bool HasActiveWeaponAction => hasActiveWeaponTx;
    /// <summary>中文含义：是否具有生效全身动作。使用位置：LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonFootAction.AllowGroundContact.get、LeonHighDropLanding.CanBeginDrop() 等共 9 处。</summary>
    public bool HasActiveFullBodyAction => hasActiveFullBodyTx;
    internal bool IsVillageGateOpening => hasActiveFullBodyTx && fullBodyTx.fullBodyActionType == FullBodyActionType.VillageGateOpen;
    internal bool TryReadLandingRollTime(out float time)
    {
        time = 0f;
        return driver != null && driver.TryReadLandingRollTime(out time);
    }

    internal bool IsItemPickupActive => hasActiveFullBodyTx && fullBodyTx.fullBodyActionType == FullBodyActionType.ItemPickup;
    /// <summary>中文含义：是否命中事务生效。受击全身事务是否正在播放（供受击期间的再次伤害判断使用）</summary>
    public bool IsHitTransactionActive => hasActiveFullBodyTx && fullBodyTx.fullBodyActionType == FullBodyActionType.Hit && !fullBodyTx.cancelled;
    /// <summary>中文含义：是否存在待处理的武器切换。使用位置：LeonCombat.IsFireBlockedByWeaponAction()、LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.IsFireBlockedByWeaponAction.get 等共 5 处。</summary>
    public bool HasPendingWeaponSwitch => hasPendingWeaponSwitch;
    /// <summary>中文含义：是否武器切换内部进度。使用位置：LeonCombat.IsFireBlockedByWeaponAction()、LeonWeaponHandler.IsFireBlockedByWeaponAction.get。</summary>
    public bool IsWeaponSwitchInProgress => hasActiveWeaponTx && weaponTx.isWeaponSwitch;
    /// <summary>中文含义：待处理武器类型。</summary>
    public WeaponType PendingWeaponType => pendingWeaponType;
    /// <summary>中文含义：当前武器事务编号。使用位置：作为 WeaponBase.BeginReload(int) 的参数、LeonCombat.HandleReload()、LeonWeaponHandler.CompleteReload() 等共 5 处。</summary>
    public int CurrentWeaponTransactionId => weaponTx.transactionId;
    /// <summary>中文含义：当前全身事务编号。使用位置：LeonCombat.StartMeleeAction(FullBodyActionType, int)、LeonHighDropLanding.BeginDrop()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)。</summary>
    public int CurrentFullBodyTransactionId => fullBodyTx.transactionId;
    /// <summary>中文含义：当前阶段。使用位置：作为 Debug.LogError(object) 的参数、LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    public WeaponTransactionPhase CurrentPhase => weaponTx.phase;
    /// <summary>中文含义：当前全身阶段。</summary>
    public FullBodyTransactionPhase CurrentFullBodyPhase => fullBodyTx.fullBodyPhase;
    /// <summary>中文含义：当前配对动作会话编号。</summary>
    public int CurrentQTESessionId => fullBodyTx.qteSessionId;
    /// <summary>中文含义：是否等待武器退出。使用位置：LeonCombat.IsFireBlockedByWeaponAction()、LeonHighDropLanding.CanBeginDrop()、LeonWeaponHandler.AdvanceBrokenRecoverKnife() 等共 8 处。</summary>
    public bool WaitingForWeaponExit => waitingForWeaponExit;
}
