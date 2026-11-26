using UnityEngine;
using System;

/// <summary>
/// 武器动作类型枚举，用于序列化配置。
/// </summary>
public enum WeaponActionType
{
    /// <summary>中文含义：无（未选择或尚未发生）。无动作</summary>
    None = 0,
    /// <summary>中文含义：换弹。换弹</summary>
    Reload = 1,
    /// <summary>中文含义：收起。收起</summary>
    Holster = 2,
    /// <summary>中文含义：装备。拔出</summary>
    Equip = 3,
    /// <summary>中文含义：小刀取出。小刀拔出</summary>
    KnifeTake = 4,
    /// <summary>中文含义：小刀隐藏。小刀收回</summary>
    KnifeHide = 5
}

/// <summary>
/// 武器动作状态行为。
/// 只负责发送动作进入、完成、中断和退出通知，不修改任何参数、图层权重或业务状态。
/// 挂载到 Weapon Action 层的 Reload/Holster/Equip 状态。
/// </summary>
public class WeaponActionSMB : StateMachineBehaviour
{
    /// <summary>当前状态对应的武器动作类型，不通过状态名判断。</summary>
    [Header("动作类型")]
    [Tooltip("当前状态对应的武器动作类型，不通过状态名判断")]
    public WeaponActionType actionType = WeaponActionType.Reload;
    /// <summary>标准化时间达到此值时发送正常完成通知。</summary>
    [Header("通知时刻")]
    [Tooltip("标准化时间达到此值时发送正常完成通知")]
    [Range(0.1f, 0.99f)]
    public float completeNormalizedTime = 0.9f;
    [Tooltip("逐发装填循环保留累计圈数，供跨循环的插弹标记去重")]
    public bool reportUnwrappedProgress;
    /// <summary>是否具有已完成。</summary>
    [SerializeField]
    private bool hasCompleted;
    /// <summary>中文含义：全局进入序号。全局递增的状态进入序号</summary>
    private static int globalEntrySequence;
    /// <summary>中文含义：进入序号。本次进入的序号</summary>
    private int entrySequence;
    // ================================================================
    // 通知事件（全部携带 Animator、动作类型、进入序号）
    // ================================================================
    /// <summary>中文含义：动作进入时的通知回调。状态进入通知</summary>
    public static event Action<Animator, WeaponActionType, int> OnActionEntered;
    /// <summary>中文含义：动作完成时的通知回调。动作完成通知（标准化时间达到阈值）</summary>
    public static event Action<Animator, WeaponActionType, int> OnActionCompleted;
    /// <summary>中文含义：动作进度时的通知回调。动作进度通知，用于不依赖固定秒数的视觉时序</summary>
    public static event Action<Animator, WeaponActionType, int, float> OnActionProgress;
    /// <summary>中文含义：动作中断时的通知回调。动作中断通知（未正常完成就退出）</summary>
    public static event Action<Animator, WeaponActionType, int> OnActionInterrupted;
    /// <summary>中文含义：动作退出时的通知回调。状态退出通知（无论正常完成还是中断都发送）</summary>
    public static event Action<Animator, WeaponActionType, int, bool> OnActionExited;
    /// <summary>ResetEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        globalEntrySequence = 0;
        OnActionEntered = null;
        OnActionCompleted = null;
        OnActionProgress = null;
        OnActionInterrupted = null;
        OnActionExited = null;
    }

    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, WeaponActionType, int&gt;.Invoke(Animator, WeaponActionType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        hasCompleted = false;
        entrySequence = ++globalEntrySequence;
        // 判空前缓存的动作进入时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::WeaponActionType, int> receiverValue1 = OnActionEntered;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(animator, actionType, entrySequence);
        }
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, WeaponActionType, int, float&gt;.Invoke(Animator, WeaponActionType, int, float)、Action&lt;Animator, WeaponActionType, int&gt;.Invoke(Animator, WeaponActionType, int)、Mathf.Clamp01(float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 判空前缓存的动作进度时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::WeaponActionType, int, float> receiverValue2 = OnActionProgress;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            float progress = stateInfo.normalizedTime;
            if (!reportUnwrappedProgress)
            {
                progress = Mathf.Clamp01(progress);
            }
            receiverValue2.Invoke(animator, actionType, entrySequence, progress);
        }

        if (hasCompleted)
        {
            return;
        }

        if (stateInfo.normalizedTime >= completeNormalizedTime)
        {
            hasCompleted = true;
            // 判空前缓存的动作完成时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::WeaponActionType, int> receiverValue3 = OnActionCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Invoke(animator, actionType, entrySequence);
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, WeaponActionType, int, bool&gt;.Invoke(Animator, WeaponActionType, int, bool)、Action&lt;Animator, WeaponActionType, int&gt;.Invoke(Animator, WeaponActionType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!hasCompleted)
        {
            // 判空前缓存的动作中断时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::WeaponActionType, int> receiverValue4 = OnActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.Invoke(animator, actionType, entrySequence);
            }
        }

        // 判空前缓存的动作退出时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::WeaponActionType, int, bool> receiverValue5 = OnActionExited;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Invoke(animator, actionType, entrySequence, hasCompleted);
        }
    }
}
