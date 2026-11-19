using System;
using UnityEngine;

/// <summary>
/// 快速反应事件类型。
/// </summary>
public enum QTEKind
{
    /// <summary>中文含义：正面抓取。</summary>
    FrontGrab = 0,
    /// <summary>中文含义：后侧撕咬颈部。</summary>
    RearBiteNeck = 1,
    /// <summary>中文含义：后侧抓取。</summary>
    RearGrab = RearBiteNeck,
    /// <summary>中文含义：腿部撕咬。</summary>
    LegBite = 2,
    /// <summary>中文含义：小刀暗杀。</summary>
    KnifeAssassination = 3,
    /// <summary>中文含义：电锯执行。</summary>
    ChainsawExecution = 4,
    /// <summary>俯卧、仰卧或跪姿的小刀补刀。</summary>
    GroundFinisher = 5
}

/// <summary>
/// 一类快速反应事件的双方动画与业务约束。
/// 动画片段通过检视面板或编辑器构建器绑定，运行时只覆盖快速反应状态的动作片段。
/// </summary>
[Serializable]
public sealed class QTEAnimationVariant
{
    /// <summary>中文含义：种类。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant() 等共 4 处。</summary>
    public QTEKind kind = QTEKind.FrontGrab;
    /// <summary>中文含义：玩家进入。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip playerEnter;
    /// <summary>中文含义：玩家保持。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip playerHold;
    /// <summary>中文含义：玩家成功。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip playerSuccess;
    /// <summary>中文含义：玩家失败。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip playerFailure;
    /// <summary>中文含义：敌人进入。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip enemyEnter;
    /// <summary>中文含义：敌人保持。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip enemyHold;
    /// <summary>中文含义：敌人成功。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip enemySuccess;
    /// <summary>中文含义：敌人失败。使用位置：作为 QTEAnimatorOverrideScope.Apply(Animator, AnimationClip, AnimationClip, AnimationClip, AnimationClip) 的参数、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant() 等共 5 处。</summary>
    public AnimationClip enemyFailure;
    /// <summary>中文含义：起始音频事件。使用位置：LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。</summary>
    public AudioEventData startAudioEvent;
    /// <summary>中文含义：挣脱音频事件。使用位置：LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。</summary>
    public AudioEventData struggleAudioEvent;
    /// <summary>中文含义：失败音频事件。使用位置：LeonQTECoordinator.PlayQTEAudioForPhase(QTEPhase)。</summary>
    public AudioEventData failureAudioEvent;
    /// <summary>中文含义：配对中距离。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant() 等共 5 处。</summary>
    [Min(0.2f)]
    public float pairingDistance = 0.68f;
    /// <summary>中文含义：失败伤害。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant() 等共 5 处。</summary>
    [Min(0f)]
    public float failureDamage = 20f;
    /// <summary>中文含义：玩家是否攻击者。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()。</summary>
    public bool playerIsAttacker;
    /// <summary>中文含义：是否自动成功。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonQTECoordinator.SubmitEscapePress(int)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()。</summary>
    public bool automaticSuccess;
    /// <summary>中文含义：是否自动失败。使用位置：作为 LeonQTECoordinator.BeginResult(bool) 的参数、LeonQTECoordinator.TryAdvanceAfterPairCompletion()。</summary>
    public bool automaticFailure;
    /// <summary>中文含义：是否单一射击。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonQTECoordinator.IsSingleShotAssassination()、LeonQTECoordinator.TryAdvanceAfterPairCompletion()。</summary>
    public bool singleShot;
    /// <summary>中文含义：是否需要爬行中敌人。使用位置：LeonExpandedQTESetupUtility.BuildLegBiteVariant()。</summary>
    public bool requiresCrawlingEnemy;
    /// <summary>中文含义：是否需要后侧接近。使用位置：LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant()。</summary>
    public bool requiresRearApproach;
    /// <summary>中文含义：是否具有玩家动画片段集合。使用位置：QTEAnimationVariant.IsComplete.get。</summary>
    public bool HasPlayerClips => playerEnter != null && playerHold != null && playerSuccess != null && playerFailure != null;
    /// <summary>中文含义：是否具有敌人动画片段集合。使用位置：QTEAnimationVariant.IsComplete.get。</summary>
    public bool HasEnemyClips => enemyEnter != null && enemyHold != null && enemySuccess != null && enemyFailure != null;
    /// <summary>中文含义：是否完成。使用位置：LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.ApplyAnimationVariant(QTEAnimationVariant)。</summary>
    public bool IsComplete => HasPlayerClips && HasEnemyClips;
}

/// <summary>
/// 快速反应事件会话阶段。
/// </summary>
public enum QTEPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：请求的。</summary>
    Requested,
    /// <summary>中文含义：对齐中。</summary>
    Aligning,
    /// <summary>中文含义：进入中。</summary>
    Entering,
    /// <summary>中文含义：等待输入。</summary>
    WaitingForInput,
    /// <summary>中文含义：成功结果。</summary>
    SuccessResult,
    /// <summary>中文含义：失败结果。</summary>
    FailureResult,
    /// <summary>中文含义：退出中。</summary>
    Exiting,
    /// <summary>中文含义：已完成。</summary>
    Completed,
    /// <summary>中文含义：已取消。</summary>
    Cancelled
}

/// <summary>配对动作使用的 QTEResult 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum QTEResult
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：成功。</summary>
    Success,
    /// <summary>中文含义：失败。</summary>
    Failure,
    /// <summary>中文含义：已取消。</summary>
    Cancelled
}

/// <summary>配对动作使用的 QTECancelReason 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum QTECancelReason
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：不支持的种类。</summary>
    UnsupportedKind,
    /// <summary>中文含义：全局会话忙碌。</summary>
    GlobalSessionBusy,
    /// <summary>中文含义：无效参与者。</summary>
    InvalidParticipant,
    /// <summary>中文含义：资源缺失的。</summary>
    ResourceMissing,
    /// <summary>中文含义：玩家不可用。</summary>
    PlayerUnavailable,
    /// <summary>中文含义：敌人不可用。</summary>
    EnemyUnavailable,
    /// <summary>中文含义：距离无效。</summary>
    DistanceInvalid,
    /// <summary>中文含义：朝向无效。</summary>
    FacingInvalid,
    /// <summary>中文含义：高度无效。</summary>
    HeightInvalid,
    /// <summary>中文含义：空间阻挡。</summary>
    SpaceBlocked,
    /// <summary>中文含义：对齐状态已失败。</summary>
    AlignmentFailed,
    /// <summary>中文含义：玩家已死亡。</summary>
    PlayerDied,
    /// <summary>中文含义：敌人已死亡。</summary>
    EnemyDied,
    /// <summary>中文含义：敌人已销毁。</summary>
    EnemyDestroyed,
    /// <summary>中文含义：参与者停用。</summary>
    ParticipantDisabled,
    /// <summary>中文含义：协调器停用。</summary>
    CoordinatorDisabled,
    /// <summary>中文含义：场景已卸载。</summary>
    SceneUnloaded,
    /// <summary>中文含义：动画已中断。</summary>
    AnimationInterrupted,
    /// <summary>中文含义：安全性超时。</summary>
    SafetyTimeout
}

/// <summary>
/// 快速反应事件会话的只读取景上下文。
/// 摄像机只能读取参与者根节点与动画器，不能通过此结构修改会话状态。
/// </summary>
public readonly struct QTESessionContext
{
    /// <summary>中文含义：会话编号。只读字段，初始化后不由普通写入口替换。使用位置：QTESessionContext.IsValid.get、QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</summary>
    public readonly int sessionId;
    /// <summary>中文含义：种类。只读字段，初始化后不由普通写入口替换。使用位置：QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool) 等共 9 处。</summary>
    public readonly QTEKind kind;
    /// <summary>中文含义：玩家根节点。只读字段，初始化后不由普通写入口替换。使用位置：作为 QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float) 的参数、作为 Transform.IsChildOf(Transform) 的参数、作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数 等共 11 处。</summary>
    public readonly Transform playerRoot;
    /// <summary>中文含义：玩家动画控制器。只读字段，初始化后不由普通写入口替换。使用位置：作为 QTECameraPresenter.GetHumanBone(Animator, HumanBodyBones) 的参数、QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</summary>
    public readonly Animator playerAnimator;
    /// <summary>中文含义：敌人根节点。只读字段，初始化后不由普通写入口替换。使用位置：作为 QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float) 的参数、作为 Transform.IsChildOf(Transform) 的参数、QTECameraPresenter.CanUseRearGrabCamera(QTESessionContext) 等共 9 处。</summary>
    public readonly Transform enemyRoot;
    /// <summary>中文含义：敌人动画控制器。只读字段，初始化后不由普通写入口替换。使用位置：作为 QTECameraPresenter.GetHumanBone(Animator, HumanBodyBones) 的参数、QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</summary>
    public readonly Animator enemyAnimator;
    /// <summary>构造并初始化 QTESessionContext 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    /// <param name="kind">中文含义：种类。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    /// <param name="playerRoot">中文含义：玩家根节点。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    /// <param name="playerAnimator">中文含义：玩家动画控制器。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    /// <param name="enemyRoot">中文含义：敌人根节点。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    /// <param name="enemyAnimator">中文含义：敌人动画控制器。使用位置：QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator)。</param>
    public QTESessionContext(int sessionId, QTEKind kind, Transform playerRoot, Animator playerAnimator, Transform enemyRoot, Animator enemyAnimator)
    {
        this.sessionId = sessionId;
        this.kind = kind;
        this.playerRoot = playerRoot;
        this.playerAnimator = playerAnimator;
        this.enemyRoot = enemyRoot;
        this.enemyAnimator = enemyAnimator;
    }

    /// <summary>中文含义：是否有效。使用位置：LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)、QTECameraPresenter.LateUpdate()。</summary>
    public bool IsValid => sessionId != 0 && playerRoot != null && enemyRoot != null;
}

/// <summary>
/// 只读输入进度快照，界面只能显示，不能据此提交结果。
/// </summary>
public readonly struct QTEProgressInfo
{
    /// <summary>中文含义：会话编号。只读字段，初始化后不由普通写入口替换。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)、QTEPromptView.HandleProgressChanged(QTEProgressInfo)。</summary>
    public readonly int sessionId;
    /// <summary>中文含义：按下次数。只读字段，初始化后不由普通写入口替换。使用位置：作为 Math.Min(float, float) 的参数、作为 QTEPromptView.SetProgress(int) 的参数、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</summary>
    public readonly int presses;
    /// <summary>中文含义：所需按下次数。只读字段，初始化后不由普通写入口替换。使用位置：作为 Math.Min(float, float) 的参数、QTEProgressInfo.NormalizedProgress.get、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</summary>
    public readonly int requiredPresses;
    /// <summary>中文含义：剩余时间。只读字段，初始化后不由普通写入口替换。使用位置：作为 Math.Min(float, float) 的参数、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)、QTEPromptView.HandleProgressChanged(QTEProgressInfo)。</summary>
    public readonly float remainingTime;
    /// <summary>中文含义：持续时间。只读字段，初始化后不由普通写入口替换。使用位置：作为 Math.Min(float, float) 的参数、QTEProgressInfo.NormalizedTime.get、QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</summary>
    public readonly float duration;
    /// <summary>构造并初始化 QTEProgressInfo 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.PublishProgress()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</param>
    /// <param name="presses">中文含义：按下次数。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</param>
    /// <param name="requiredPresses">中文含义：所需按下次数。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</param>
    /// <param name="remainingTime">中文含义：剩余时间。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：QTEProgressInfo.QTEProgressInfo(int, int, int, float, float)。</param>
    public QTEProgressInfo(int sessionId, int presses, int requiredPresses, float remainingTime, float duration)
    {
        this.sessionId = sessionId;
        this.presses = presses;
        this.requiredPresses = requiredPresses;
        this.remainingTime = remainingTime;
        this.duration = duration;
    }

    /// <summary>中文含义：归一化进度。使用位置：QTEPromptView.HandleProgressChanged(QTEProgressInfo)。</summary>
    public float NormalizedProgress
    {
        get
        {
            if (requiredPresses > 0)
            {
                return Math.Min(1f, (float)presses / requiredPresses);
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：归一化时间。使用位置：QTEPromptView.HandleProgressChanged(QTEProgressInfo)。</summary>
    public float NormalizedTime
    {
        get
        {
            if (duration > 0f)
            {
                return Math.Max(0f, Math.Min(1f, remainingTime / duration));
            }
            else
            {
                return 0f;
            }
        }
    }
}

/// <summary>
/// 会话结束快照。
/// </summary>
public readonly struct QTESessionEndInfo
{
    /// <summary>中文含义：会话编号。只读字段，初始化后不由普通写入口替换。使用位置：QTECameraPresenter.HandleSessionEnded(QTESessionEndInfo)、QTEPromptView.HandleSessionEnded(QTESessionEndInfo)、QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</summary>
    public readonly int sessionId;
    /// <summary>中文含义：当前配对动作结果。只读字段，初始化后不由普通写入口替换。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</summary>
    public readonly QTEResult result;
    /// <summary>中文含义：取消原因。只读字段，初始化后不由普通写入口替换。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</summary>
    public readonly QTECancelReason cancelReason;
    /// <summary>中文含义：是否伤害已提交。只读字段，初始化后不由普通写入口替换。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</summary>
    public readonly bool damageCommitted;
    /// <summary>构造并初始化 QTESessionEndInfo 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</param>
    /// <param name="result">中文含义：当前配对动作结果。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</param>
    /// <param name="cancelReason">中文含义：取消原因。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</param>
    /// <param name="damageCommitted">中文含义：是否伤害已提交。使用位置：QTESessionEndInfo.QTESessionEndInfo(int, QTEResult, QTECancelReason, bool)。</param>
    public QTESessionEndInfo(int sessionId, QTEResult result, QTECancelReason cancelReason, bool damageCommitted)
    {
        this.sessionId = sessionId;
        this.result = result;
        this.cancelReason = cancelReason;
        this.damageCommitted = damageCommitted;
    }
}
