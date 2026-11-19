using UnityEngine;

/// <summary>
/// 通用定时受击状态行为。用于没有 OnHitComplete Animation Event 的受击动画。
/// 在 normalizedTime 达到 completeNormalizedTime 时调用 OnHitComplete。
/// 不修改任何 Animator 参数。
/// </summary>
public class EnemyTimedHitStateSMB : StateMachineBehaviour
{
    /// <summary>中文含义：动作协调器。使用位置：EnemyTimedHitStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>中文含义：动作凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数、EnemyTimedHitStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>中文含义：完成归一化时间。使用位置：EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)、ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)。</summary>
    [Header("通知时刻")]
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>中文含义：是否完成已发送。使用位置：EnemyTimedHitStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool completeSent;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyTimedHitStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // receiverValue1（判空前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedHitStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。
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

        completeSent = false;
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawHitAudioEnter, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。使用位置：EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!completeSent && stateInfo.normalizedTime >= completeNormalizedTime)
        {
            completeSent = true;
            // receiverValue4（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
            // optionalValue5（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedHitStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
bool? optionalValue5;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                optionalValue5 = receiverValue4.ReceiveAnimationNotification(actionToken, "OnHitComplete");
            }
            else
            {
                optionalValue5 = default;
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SendMessage(string, SendMessageOptions)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyTimedHitStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawActionAudioExit, SendMessageOptions.DontRequireReceiver);
    }
}
