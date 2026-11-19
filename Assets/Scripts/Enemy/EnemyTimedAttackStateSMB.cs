using UnityEngine;

/// <summary>
/// 通用定时攻击状态行为。用于没有 Animation Event 的攻击动画。
/// 在 normalizedTime 达到 hitNormalizedTime 时调用 OnAttackHit，
/// 达到 completeNormalizedTime 时调用 OnAttackComplete。
/// 不修改任何 Animator 参数，只通过 SendMessage 通知。
/// </summary>
public class EnemyTimedAttackStateSMB : StateMachineBehaviour
{
    /// <summary>中文含义：动作协调器。使用位置：EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private EnemyActionCoordinator actionCoordinator;
    /// <summary>中文含义：动作凭证。使用位置：作为 EnemyActionCoordinator.ReceiveAnimationNotification(ActionHandle, string) 的参数、EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</summary>
    private GameArchitecture.ActionHandle actionToken;
    /// <summary>中文含义：招架窗口打开归一化时间。使用位置：作为 Mathf.Min(float, float) 的参数。</summary>
    [Header("通知时刻")]
    [Range(0.01f, 0.99f)]
    public float parryWindowOpenNormalizedTime = 0.24f;
    /// <summary>中文含义：命中归一化时间。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Range(0.01f, 0.99f)]
    public float hitNormalizedTime = 0.4f;
    /// <summary>中文含义：完成归一化时间。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    [Range(0.01f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>中文含义：是否招架窗口已发送。使用位置：EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool parryWindowSent;
    /// <summary>中文含义：是否命中已发送。使用位置：EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool hitSent;
    /// <summary>中文含义：是否完成已发送。使用位置：EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</summary>
    private bool completeSent;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerController&gt;()、GameObject.SendMessage(string, SendMessageOptions)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // receiverValue1（判空前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateEnter(Animator, AnimatorStateInfo, int)。
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
    /// <param name="stateInfo">中文含义：当前动画状态信息。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // resolvedParryOpenTime（解算后的招架打开时间）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
float resolvedParryOpenTime = Mathf.Min(parryWindowOpenNormalizedTime, Mathf.Max(0.01f, hitNormalizedTime - 0.01f));
        if (!parryWindowSent && stateInfo.normalizedTime >= resolvedParryOpenTime)
        {
            parryWindowSent = true;
            // receiverValue4（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue4 = actionCoordinator;
            // optionalValue5（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
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
            // receiverValue6（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue6 = actionCoordinator;
            // optionalValue7（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
bool? optionalValue7;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                optionalValue7 = receiverValue6.ReceiveAnimationNotification(actionToken, EnemyAnimationMessageNames.ParryWindowClose);
            }
            else
            {
                optionalValue7 = default;
            }

            // receiverValue8（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue8 = actionCoordinator;
            // optionalValue9（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
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
            // receiverValue10（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue10 = actionCoordinator;
            // optionalValue11（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedAttackStateSMB.OnStateUpdate(Animator, AnimatorStateInfo, int)。
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
    /// <param name="animator">中文含义：动画控制器。使用位置：EnemyTimedAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // receiverValue12（判空前缓存的动作协调器）：从动作协调器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTimedAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)。
global::EnemyActionCoordinator receiverValue12 = actionCoordinator;
        // optionalValue13（条件访问得到的接收动画通知的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyTimedAttackStateSMB.OnStateExit(Animator, AnimatorStateInfo, int)。
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
