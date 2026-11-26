using UnityEngine;
using System;

/// <summary>
/// 近战命中窗口通知器。
/// 只报告动画已经到达命中帧，不处理伤害、目标选择或动画参数。
/// </summary>
public class MeleeDamageSMB : StateMachineBehaviour
{
    /// <summary>必须与同一状态上的全身动作类型一致。</summary>
    [Header("动作")]
    [Tooltip("必须与同一状态上的全身动作类型一致")]
    public FullBodyActionType actionType = FullBodyActionType.KnifeQuickAttack;
    /// <summary>标准化时间达到此值时发送一次命中窗口通知。</summary>
    [Header("命中时刻")]
    [Tooltip("标准化时间达到此值时发送一次命中窗口通知")]
    [Range(0.05f, 0.95f)]
    public float hitNormalizedTime = 0.4f;
    /// <summary>是否命中已发送。</summary>
    private bool hitSent;
    /// <summary>命中窗口时的通知回调。事件通信成员 OnHitWindow，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public static event Action<Animator, FullBodyActionType> OnHitWindow;
    /// <summary>ResetEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        OnHitWindow = null;
    }

    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hitSent = false;
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, FullBodyActionType&gt;.Invoke(Animator, FullBodyActionType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (hitSent || stateInfo.normalizedTime < hitNormalizedTime)
        {
            return;
        }

        hitSent = true;
        // 判空前缓存的命中窗口时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::FullBodyActionType> receiverValue1 = OnHitWindow;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(animator, actionType);
        }
    }
}
