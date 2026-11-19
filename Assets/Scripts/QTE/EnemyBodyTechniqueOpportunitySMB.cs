using UnityEngine;

/// <summary>
/// 敌人硬直体术窗口通知器。
/// 当前体术窗口由参与器运行时读取腿部受击状态开放，本行为保留给后续需要直接挂状态时使用。
/// </summary>
public class EnemyBodyTechniqueOpportunitySMB : StateMachineBehaviour
{
    /// <summary>中文含义：零为右侧切入踢，一为旋转钩踢。使用位置：作为 VillagerQTEParticipant.OpenBodyTechniqueOpportunity(int, float) 的参数。</summary>
    [Header("体术配置")]
    [Tooltip("零为右侧切入踢，一为旋转钩踢。")]
    public int techniqueIndex;
    /// <summary>中文含义：打开归一化时间。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Header("窗口时间")]
    [Range(0.05f, 0.95f)]
    public float openNormalizedTime = 0.42f;
    /// <summary>中文含义：关闭归一化时间。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Range(0.05f, 1f)]
    public float closeNormalizedTime = 0.78f;
    /// <summary>中文含义：最小窗口持续时间。使用位置：作为 VillagerQTEParticipant.OpenBodyTechniqueOpportunity(int, float) 的参数。</summary>
    [Min(0.05f)]
    public float minimumWindowDuration = 0.35f;
    /// <summary>中文含义：是否已打开。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool opened;
    /// <summary>中文含义：是否已经关闭。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
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
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</param>
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
            // participant（参与者）：保存 Component.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
VillagerQTEParticipant participant = animator.GetComponent<VillagerQTEParticipant>();
            if (participant != null)
            {
                participant.OpenBodyTechniqueOpportunity(techniqueIndex, minimumWindowDuration);
            }
        }

        if (!closed && stateInfo.normalizedTime >= closeNormalizedTime)
        {
            closed = true;
            // participant（参与者）：保存 Component.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
VillagerQTEParticipant participant = animator.GetComponent<VillagerQTEParticipant>();
            if (participant != null)
            {
                participant.CloseBodyTechniqueOpportunity();
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()、VillagerQTEParticipant.CloseBodyTechniqueOpportunity()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateExit(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // participant（参与者）：使用位置：EnemyBodyTechniqueOpportunitySMB.OnStateExit(Animator, AnimatorStateInfo, int)。
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
