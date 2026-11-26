using System;
using UnityEngine;

/// <summary>
/// 敌人快速反应事件状态通知器，只发送带进入序号的动画通知。
/// </summary>
public class EnemyQTEStateSMB : StateMachineBehaviour
{
    /// <summary>动作。</summary>
    [Header("动作")]
    public EnemyQTEAction action = EnemyQTEAction.Enter;
    /// <summary>完成归一化时间。</summary>
    [Header("通知时刻")]
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>全局进入序号。</summary>
    private static int globalEntrySequence;
    /// <summary>进入序号。</summary>
    private int entrySequence;
    /// <summary>是否具有已完成。</summary>
    private bool hasCompleted;
    /// <summary>动作进入时的通知回调。事件通信成员 OnActionEntered，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public static event Action<Animator, EnemyQTEAction, int> OnActionEntered;
    /// <summary>动作完成时的通知回调。事件通信成员 OnActionCompleted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public static event Action<Animator, EnemyQTEAction, int> OnActionCompleted;
    /// <summary>动作中断时的通知回调。事件通信成员 OnActionInterrupted，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public static event Action<Animator, EnemyQTEAction, int> OnActionInterrupted;
    /// <summary>动作退出时的通知回调。事件通信成员 OnActionExited，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public static event Action<Animator, EnemyQTEAction, int, bool> OnActionExited;
    /// <summary>ResetEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        globalEntrySequence = 0;
        OnActionEntered = null;
        OnActionCompleted = null;
        OnActionInterrupted = null;
        OnActionExited = null;
    }

    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, EnemyQTEAction, int&gt;.Invoke(Animator, EnemyQTEAction, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasCompleted = false;
        entrySequence = ++globalEntrySequence;
        // 判空前缓存的动作进入时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::EnemyQTEAction, int> receiverValue1 = OnActionEntered;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(animator, action, entrySequence);
        }
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, EnemyQTEAction, int&gt;.Invoke(Animator, EnemyQTEAction, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (hasCompleted || stateInfo.normalizedTime < completeNormalizedTime)
        {
            return;
        }

        hasCompleted = true;
        // 判空前缓存的动作完成时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::EnemyQTEAction, int> receiverValue2 = OnActionCompleted;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke(animator, action, entrySequence);
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, EnemyQTEAction, int, bool&gt;.Invoke(Animator, EnemyQTEAction, int, bool)、Action&lt;Animator, EnemyQTEAction, int&gt;.Invoke(Animator, EnemyQTEAction, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!hasCompleted)
        {
            // 判空前缓存的动作中断时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::EnemyQTEAction, int> receiverValue3 = OnActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Invoke(animator, action, entrySequence);
            }
        }

        // 判空前缓存的动作退出时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::EnemyQTEAction, int, bool> receiverValue4 = OnActionExited;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Invoke(animator, action, entrySequence, hasCompleted);
        }
    }
}
