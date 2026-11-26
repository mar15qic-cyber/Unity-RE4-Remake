using UnityEngine;

/// <summary>
/// 通用定时攻击状态行为。用于没有 Animation Event 的攻击动画。
/// 在 normalizedTime 达到 hitNormalizedTime 时调用 OnAttackHit，
/// 达到 completeNormalizedTime 时调用 OnAttackComplete。
/// 不修改任何 Animator 参数，只通过 SendMessage 通知。
/// </summary>
public class EnemyTimedAttackStateSMB : StateMachineBehaviour
{
    /// <summary>动作协调器。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>动作凭证。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>招架窗口打开归一化时间。</summary>
    [Header("通知时刻")]
    [Range(0.01f, 0.99f)]
    public float parryWindowOpenNormalizedTime = 0.24f;
    /// <summary>命中归一化时间。</summary>
    [Range(0.01f, 0.99f)]
    public float hitNormalizedTime = 0.4f;
    /// <summary>完成归一化时间。</summary>
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>是否招架窗口已发送。</summary>
    private bool parryWindowSent;
    /// <summary>是否命中已发送。</summary>
    private bool hitSent;
    /// <summary>是否完成已发送。</summary>
    private bool completeSent;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

        parryWindowSent = false;
        hitSent = false;
        completeSent = false;
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawAttackAudioEnter, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 解算后的招架打开时间。
float resolvedParryOpenTime = Mathf.Min(parryWindowOpenNormalizedTime, Mathf.Max(0.01f, hitNormalizedTime - 0.01f));
        if (!parryWindowSent && stateInfo.normalizedTime >= resolvedParryOpenTime)
        {
            parryWindowSent = true;
            // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
            // 条件访问得到的接收动画通知的返回结果。
bool? optionalValue5;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                optionalValue5 = receiverValue4.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.ParryWindowOpen);
            }
            else
            {
                optionalValue5 = default;
            }
        }

        if (!hitSent && stateInfo.normalizedTime >= hitNormalizedTime)
        {
            hitSent = true;
            // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue6 = actionCoordinator;
            // 条件访问得到的接收动画通知的返回结果。
bool? optionalValue7;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                optionalValue7 = receiverValue6.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.ParryWindowClose);
            }
            else
            {
                optionalValue7 = default;
            }

            // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue8 = actionCoordinator;
            // 条件访问得到的接收动画通知的返回结果。
bool? optionalValue9;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                optionalValue9 = receiverValue8.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.AttackHit);
            }
            else
            {
                optionalValue9 = default;
            }
        }

        if (!completeSent && stateInfo.normalizedTime >= completeNormalizedTime)
        {
            completeSent = true;
            // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue10 = actionCoordinator;
            // 条件访问得到的接收动画通知的返回结果。
bool? optionalValue11;
            if (!global::System.Object.ReferenceEquals(receiverValue10, null))
            {
                optionalValue11 = receiverValue10.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.AttackComplete);
            }
            else
            {
                optionalValue11 = default;
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 判空前缓存的动作协调器。
global::EnemyActionCoordinator receiverValue12 = actionCoordinator;
        // 条件访问得到的接收动画通知的返回结果。
bool? optionalValue13;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            optionalValue13 = receiverValue12.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.ParryWindowClose);
        }
        else
        {
            optionalValue13 = default;
        }

        // 如果被中断（未完成），不补发伤害。
        // VillagerController.EndAttack / BeginDeath 会处理 ATK=false。
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawActionAudioExit, SendMessageOptions.DontRequireReceiver);
    }
}
