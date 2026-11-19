using System;
using UnityEngine;

/// <summary>配对动作使用的 EnemyQTEAction 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum EnemyQTEAction
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None = 0,
    /// <summary>中文含义：进入。</summary>
    Enter = 1,
    /// <summary>中文含义：保持。</summary>
    Hold = 2,
    /// <summary>中文含义：成功。</summary>
    Success = 3,
    /// <summary>中文含义：失败。</summary>
    Failure = 4
}

/// <summary>
/// 敌人配对动作参与者契约。行为树只需持有该接口并调用里昂协调器。
/// </summary>
public interface IEnemyQTEParticipant
{
    /// <summary>中文含义：根节点。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数、作为 LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3) 的参数、作为 QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator) 的参数 等共 21 处。</summary>
    Transform Root { get; }

    /// <summary>中文含义：动画控制器。使用位置：作为 QTESessionContext.QTESessionContext(int, QTEKind, Transform, Animator, Transform, Animator) 的参数、LeonQTECoordinator.ApplyResultMotion()。</summary>
    Animator Animator { get; }

    /// <summary>中文含义：碰撞体集合。使用位置：作为 LeonQTECoordinator.ContainsCollider(Collider[], Collider) 的参数、LeonQTECoordinator.IgnorePairCollisions(IEnemyQTEParticipant)。</summary>
    Collider[] Colliders { get; }

    /// <summary>中文含义：是否可用。使用位置：LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)。</summary>
    bool IsAvailable { get; }

    /// <summary>中文含义：是否存活。使用位置：LeonQTECoordinator.Update()。</summary>
    bool IsAlive { get; }

    /// <summary>中文含义：活动会话编号。</summary>
    int ActiveSessionId { get; }

    /// <summary>中文含义：动作进入。事件通信成员 ActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    event Action<int, EnemyQTEAction, int> ActionEntered;
    /// <summary>中文含义：动作完成。事件通信成员 ActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)、LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</summary>
    event Action<int, EnemyQTEAction, int> ActionCompleted;
    /// <summary>中文含义：动作退出。事件通信成员 ActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)、LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</summary>
    event Action<int, EnemyQTEAction, int, bool> ActionExited;
    /// <summary>中文含义：撕咬接触。事件通信成员 BiteContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)、LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</summary>
    event Action<int> BiteContact;
    /// <summary>中文含义：暗杀接触。事件通信成员 AssassinationContact，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)、LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</summary>
    event Action<int> AssassinationContact;
    /// <summary>中文含义：已失效。事件通信成员 Invalidated，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonQTECoordinator.SubscribeParticipant(IEnemyQTEParticipant)、LeonQTECoordinator.UnsubscribeParticipant(IEnemyQTEParticipant)。</summary>
    event Action<int, QTECancelReason> Invalidated;
    /// <summary>CanStart 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">中文含义：里昂。</param>
    /// <param name="kind">中文含义：种类。</param>
    /// <param name="reason">中文含义：原因。输出参数，由本入口写入并交给调用方。</param>
    bool CanStart(LeonController leon, QTEKind kind, out QTECancelReason reason);
    /// <summary>ApplyAnimationVariant 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="variant">中文含义：变体。</param>
    bool ApplyAnimationVariant(QTEAnimationVariant variant);
    /// <summary>ClearAnimationVariant 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ClearAnimationVariant();
    /// <summary>TryAcquire 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。</param>
    /// <param name="kind">中文含义：种类。</param>
    bool TryAcquire(int sessionId, QTEKind kind);
    /// <summary>CommitPairedActionPose 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.SetParticipantWorldPose(Transform, Vector3, Quaternion)、LeonQTECoordinator.SetParticipantWorldPosition(Transform, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">中文含义：会话。</param>
    /// <param name="position">中文含义：位置。</param>
    /// <param name="orientation">中文含义：朝向。</param>
    bool CommitPairedActionPose(int session, Vector3 position, Quaternion orientation);
    /// <summary>CommitAssassinationResult 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CommitAssassination(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="session">中文含义：会话。</param>
    /// <param name="damage">中文含义：伤害。</param>
    bool CommitAssassinationResult(int session, float damage);
    /// <summary>BeginAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.AlignAndEnter(int)、LeonQTECoordinator.BeginResult(bool)、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。</param>
    /// <param name="action">中文含义：动作。</param>
    bool BeginAction(int sessionId, EnemyQTEAction action);
    /// <summary>EndAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.RequestEnemyEnd()、LeonQTECoordinator.TryAdvanceAfterPairCompletion()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。</param>
    bool EndAction(int sessionId);
    /// <summary>Release 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.FinishCleanup(QTEResult, QTECancelReason)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。</param>
    void Release(int sessionId);
}
