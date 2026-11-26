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
    /// <summary>种类。</summary>
    public QTEKind kind = QTEKind.FrontGrab;
    /// <summary>玩家进入。</summary>
    public AnimationClip playerEnter;
    /// <summary>玩家保持。</summary>
    public AnimationClip playerHold;
    /// <summary>玩家成功。</summary>
    public AnimationClip playerSuccess;
    /// <summary>玩家失败。</summary>
    public AnimationClip playerFailure;
    /// <summary>敌人进入。</summary>
    public AnimationClip enemyEnter;
    /// <summary>敌人保持。</summary>
    public AnimationClip enemyHold;
    /// <summary>敌人成功。</summary>
    public AnimationClip enemySuccess;
    /// <summary>敌人失败。</summary>
    public AnimationClip enemyFailure;
    /// <summary>起始音频事件。</summary>
    public AudioEventData startAudioEvent;
    /// <summary>挣脱音频事件。</summary>
    public AudioEventData struggleAudioEvent;
    /// <summary>失败音频事件。</summary>
    public AudioEventData failureAudioEvent;
    /// <summary>配对中距离。</summary>
    [Min(0.2f)]
    public float pairingDistance = 0.68f;
    /// <summary>失败伤害。</summary>
    [Min(0f)]
    public float failureDamage = 20f;
    /// <summary>玩家是否攻击者。</summary>
    public bool playerIsAttacker;
    /// <summary>是否自动成功。</summary>
    public bool automaticSuccess;
    /// <summary>是否自动失败。</summary>
    public bool automaticFailure;
    /// <summary>是否单一射击。</summary>
    public bool singleShot;
    /// <summary>是否需要爬行中敌人。</summary>
    public bool requiresCrawlingEnemy;
    /// <summary>是否需要后侧接近。</summary>
    public bool requiresRearApproach;
    /// <summary>是否具有玩家动画片段集合。</summary>
    public bool HasPlayerClips => playerEnter != null && playerHold != null && playerSuccess != null && playerFailure != null;
    /// <summary>是否具有敌人动画片段集合。</summary>
    public bool HasEnemyClips => enemyEnter != null && enemyHold != null && enemySuccess != null && enemyFailure != null;
    /// <summary>是否完成。</summary>
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
    /// <summary>会话编号。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int sessionId;
    /// <summary>种类。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly QTEKind kind;
    /// <summary>玩家根节点。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly Transform playerRoot;
    /// <summary>玩家动画控制器。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly Animator playerAnimator;
    /// <summary>敌人根节点。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly Transform enemyRoot;
    /// <summary>敌人动画控制器。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly Animator enemyAnimator;
    public readonly bool startedCrouching;
    /// <summary>构造并初始化 QTESessionContext 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryGetSessionContext(int, out QTESessionContext)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="kind">种类。</param>
    /// <param name="playerRoot">玩家根节点。</param>
    /// <param name="playerAnimator">玩家动画控制器。</param>
    /// <param name="enemyRoot">敌人根节点。</param>
    /// <param name="enemyAnimator">敌人动画控制器。</param>
    public QTESessionContext(int sessionId, QTEKind kind, Transform playerRoot, Animator playerAnimator, Transform enemyRoot, Animator enemyAnimator, bool startedCrouching = false)
    {
        this.sessionId = sessionId;
        this.kind = kind;
        this.playerRoot = playerRoot;
        this.playerAnimator = playerAnimator;
        this.enemyRoot = enemyRoot;
        this.enemyAnimator = enemyAnimator;
        this.startedCrouching = startedCrouching;
    }

    /// <summary>是否有效。</summary>
    public bool IsValid => sessionId != 0 && playerRoot != null && enemyRoot != null;
}

/// <summary>
/// 只读输入进度快照，界面只能显示，不能据此提交结果。
/// </summary>
public readonly struct QTEProgressInfo
{
    /// <summary>会话编号。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int sessionId;
    /// <summary>按下次数。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int presses;
    /// <summary>所需按下次数。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int requiredPresses;
    /// <summary>剩余时间。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly float remainingTime;
    /// <summary>持续时间。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly float duration;
    /// <summary>构造并初始化 QTEProgressInfo 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.PublishProgress()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="presses">按下次数。</param>
    /// <param name="requiredPresses">所需按下次数。</param>
    /// <param name="remainingTime">剩余时间。</param>
    /// <param name="duration">持续时间。</param>
    public QTEProgressInfo(int sessionId, int presses, int requiredPresses, float remainingTime, float duration)
    {
        this.sessionId = sessionId;
        this.presses = presses;
        this.requiredPresses = requiredPresses;
        this.remainingTime = remainingTime;
        this.duration = duration;
    }

    /// <summary>归一化进度。</summary>
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

    /// <summary>归一化时间。</summary>
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
    /// <summary>会话编号。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly int sessionId;
    /// <summary>当前配对动作结果。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly QTEResult result;
    /// <summary>取消原因。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly QTECancelReason cancelReason;
    /// <summary>是否伤害已提交。只读字段，初始化后不由普通写入口替换。</summary>
    public readonly bool damageCommitted;
    /// <summary>构造并初始化 QTESessionEndInfo 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    /// <param name="result">当前配对动作结果。</param>
    /// <param name="cancelReason">取消原因。</param>
    /// <param name="damageCommitted">是否伤害已提交。</param>
    public QTESessionEndInfo(int sessionId, QTEResult result, QTECancelReason cancelReason, bool damageCommitted)
    {
        this.sessionId = sessionId;
        this.result = result;
        this.cancelReason = cancelReason;
        this.damageCommitted = damageCommitted;
    }
}
