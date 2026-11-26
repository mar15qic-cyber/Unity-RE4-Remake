using UnityEngine;

/// <summary>
/// 启动电锯完成通知。在 PullStart 动画接近结束时通知 ChainsawManAnimationBridge。
/// </summary>
public class ChainsawStartSMB : StateMachineBehaviour
{
    /// <summary>完成归一化时间。</summary>
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.95f;
    /// <summary>是否完成已发送。</summary>
    private bool completeSent;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        completeSent = false;
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SendMessage(string, SendMessageOptions)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!completeSent && stateInfo.normalizedTime >= completeNormalizedTime)
        {
            completeSent = true;
            animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawStartComplete, SendMessageOptions.DontRequireReceiver);
        }
    }
}
