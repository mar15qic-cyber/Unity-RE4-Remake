using UnityEngine;

/// <summary>
/// 起身状态行为：仅报告 CrawlToStand 状态的退出事实。
/// 不修改任何动画参数或业务状态。
/// 挂载到 CrawlToStand 状态上，通过 OnStateExit 通知 VillagerController
/// 起身动画状态已经真正退出。
/// </summary>
public class EnemyStandUpStateSMB : StateMachineBehaviour
{
    /// <summary>动作协调器。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>动作凭证。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 判空前缓存的取得组件的返回结果。
global::VillagerController receiverValue1 = animator.GetComponent<VillagerController>();
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            actionCoordinator = receiverValue1.actionCoordinator;
        }
        else
        {
            actionCoordinator = default;
        }

        if (actionCoordinator != null)
        {
            actionToken = actionCoordinator.CurrentHandle;
        }
        else
        {
            actionToken = default;
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.NotifyStandUpStateExited(ActionHandle)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
        // 条件访问得到的通知起身状态已退出的返回结果。
bool? optionalValue5;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            optionalValue5 = receiverValue4.NotifyStandUpStateExited(actionToken);
        }
        else
        {
            optionalValue5 = default;
        }
    }
}
