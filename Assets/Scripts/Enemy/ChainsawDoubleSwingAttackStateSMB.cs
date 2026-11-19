using UnityEngine;

/// <summary>
/// 电锯双挥攻击的双命中窗口通知器。
/// 每一挥仅发送一次独立命中通知，避免第一挥被第二挥覆盖。
/// </summary>
public class ChainsawDoubleSwingAttackStateSMB : StateMachineBehaviour
{
    /// <summary>中文含义：动作协调器。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>中文含义：动作凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数、ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>中文含义：首个命中归一化时间。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Header("命中时刻")]
    [Range(0.01f, 0.99f)]
    public float firstHitNormalizedTime = 0.3f;
    /// <summary>中文含义：第二命中归一化时间。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Range(0.01f, 0.99f)]
    public float secondHitNormalizedTime = 0.7f;
    /// <summary>中文含义：完成归一化时间。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.93f;
    /// <summary>中文含义：是否首个命中已发送。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool firstHitSent;
    /// <summary>中文含义：是否第二命中已发送。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool secondHitSent;
    /// <summary>中文含义：是否完成已发送。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool completeSent;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // receiverValue1（判空前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ChainsawDoubleSwingAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。
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

        firstHitSent = false;
        secondHitSent = false;
        completeSent = false;
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawAttackAudioEnter, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!firstHitSent && stateInfo.normalizedTime >= firstHitNormalizedTime)
        {
            firstHitSent = true;
            // receiverValue4（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
            // optionalValue5（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
bool? optionalValue5;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                optionalValue5 = receiverValue4.ReceiveAnimationNotification(actionToken, "OnAttackFirstHit");
            }
            else
            {
                optionalValue5 = default;
            }
        }

        if (!secondHitSent && stateInfo.normalizedTime >= secondHitNormalizedTime)
        {
            secondHitSent = true;
            // receiverValue6（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue6 = actionCoordinator;
            // optionalValue7（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
bool? optionalValue7;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                optionalValue7 = receiverValue6.ReceiveAnimationNotification(actionToken, "OnAttackSecondHit");
            }
            else
            {
                optionalValue7 = default;
            }
        }

        if (!completeSent && stateInfo.normalizedTime >= completeNormalizedTime)
        {
            completeSent = true;
            // receiverValue8（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue8 = actionCoordinator;
            // optionalValue9（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
bool? optionalValue9;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                optionalValue9 = receiverValue8.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.AttackComplete);
            }
            else
            {
                optionalValue9 = default;
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SendMessage(string, SendMessageOptions)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：ChainsawDoubleSwingAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawActionAudioExit, SendMessageOptions.DontRequireReceiver);
    }
}
