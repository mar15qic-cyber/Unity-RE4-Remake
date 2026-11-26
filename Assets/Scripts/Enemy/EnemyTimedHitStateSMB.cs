using UnityEngine;

/// <summary>
/// 通用定时受击状态行为。用于没有 OnHitComplete Animation Event 的受击动画。
/// 在 normalizedTime 达到 completeNormalizedTime 时调用 OnHitComplete。
/// 不修改任何 Animator 参数。
/// </summary>
public class EnemyTimedHitStateSMB : StateMachineBehaviour
{
    /// <summary>宿主村民控制器。</summary>
    private VillagerController ownerController;
    /// <summary>动作协调器。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>动作凭证。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>完成归一化时间。</summary>
    [Header("通知时刻")]
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>是否完成已发送。</summary>
    private bool completeSent;
    [HideInInspector] public float opportunityTimeScale = 1f;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 判空前缓存的取得组件的返回结果。
        global::VillagerController receiverValue1 = animator.GetComponent<VillagerController>();
        ownerController = receiverValue1;
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
        if (animator.TryGetComponent<VillagerQTEParticipant>(out var participant))
        {
            participant.SetHitWindowClock(stateInfo.fullPathHash, opportunityTimeScale);
        }
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawHitAudioEnter, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 配对存活释放阶段可能先于 QTE 结束切入受击状态，QTE 释放前不提前吞掉完成通知。
        if (!global::System.Object.ReferenceEquals(ownerController, null) && ownerController.IsQTEControlled)
        {
            return;
        }

        if (!completeSent && stateInfo.normalizedTime >= completeNormalizedTime)
        {
            global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
            bool accepted = false;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                accepted = receiverValue4.ReceiveAnimationNotification(actionToken, "OnHitComplete");
                if (!accepted
                    && !global::System.Object.ReferenceEquals(ownerController, null)
                    && ownerController.CurrentState == VillagerState.Staggered
                    && ownerController.HasPendingHit)
                {
                    // QTE 释放后行为树首次领取 Hit 动作会刷新凭证，此时同步当前有效凭证再提交一次。
                    actionToken = receiverValue4.CurrentHandle;
                    accepted = receiverValue4.ReceiveAnimationNotification(actionToken, "OnHitComplete");
                }
            }

            if (accepted || global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                completeSent = true;
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SendMessage(string, SendMessageOptions)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.gameObject.SendMessage(EnemyAnimationMessageNames.ChainsawActionAudioExit, SendMessageOptions.DontRequireReceiver);
    }
}
