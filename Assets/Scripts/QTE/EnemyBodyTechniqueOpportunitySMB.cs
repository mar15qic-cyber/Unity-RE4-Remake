using UnityEngine;

/// <summary>
/// 敌人硬直体术窗口通知器。
/// 当前体术窗口由参与器运行时读取腿部受击状态开放，本行为保留给后续需要直接挂状态时使用。
/// </summary>
public class EnemyBodyTechniqueOpportunitySMB : StateMachineBehaviour
{
    /// <summary>零为右侧切入踢，一为旋转钩踢。</summary>
    [Header("体术配置")]
    [Tooltip("零为右侧切入踢，一为旋转钩踢。")]
    public int techniqueIndex;
    /// <summary>打开归一化时间。</summary>
    [Header("窗口时间")]
    [Range(0.05f, 0.95f)]
    public float openNormalizedTime = 0.42f;
    /// <summary>关闭归一化时间。</summary>
    [Range(0.05f, 1f)]
    public float closeNormalizedTime = 0.78f;
    /// <summary>最小窗口持续时间。</summary>
    [Min(0.05f)]
    public float minimumWindowDuration = 0.35f;
    /// <summary>是否已打开。</summary>
    private bool opened;
    /// <summary>是否已经关闭。</summary>
    private bool closed;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        opened = false;
        closed = false;
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()、VillagerQTEParticipant.OpenBodyTechniqueOpportunity(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null)
        {
            return;
        }

        if (!opened && stateInfo.normalizedTime >= openNormalizedTime)
        {
            opened = true;
            // 参与者。
VillagerQTEParticipant participant = animator.GetComponent<VillagerQTEParticipant>();
            if (participant != null)
            {
                participant.OpenBodyTechniqueOpportunity(techniqueIndex, minimumWindowDuration);
            }
        }

        if (!closed && stateInfo.normalizedTime >= closeNormalizedTime)
        {
            closed = true;
            // 参与者。
VillagerQTEParticipant participant = animator.GetComponent<VillagerQTEParticipant>();
            if (participant != null)
            {
                participant.CloseBodyTechniqueOpportunity();
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 参与者。
VillagerQTEParticipant participant;
        if (animator != null)
        {
            participant = animator.GetComponent<VillagerQTEParticipant>();
        }
        else
        {
            participant = null;
        }

        if (participant != null)
        {
            participant.CloseBodyTechniqueOpportunity();
        }
    }
}
