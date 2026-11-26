using UnityEngine;
using System;

/// <summary>
/// 全身动作类型枚举，用于序列化配置。
/// </summary>
public enum FullBodyActionType
{
    /// <summary>中文含义：无（未选择或尚未发生）。无动作</summary>
    None = 0,
    /// <summary>中文含义：命中。受击</summary>
    Hit = 1,
    /// <summary>中文含义：死亡。死亡</summary>
    Death = 2,
    /// <summary>中文含义：配对动作进入。正面抓取进入</summary>
    QTEEnter = 3,
    /// <summary>中文含义：配对动作保持。正面抓取挣扎保持</summary>
    QTEHold = 4,
    /// <summary>中文含义：配对动作成功。成功推开</summary>
    QTESuccess = 5,
    /// <summary>中文含义：配对动作失败。失败咬颈</summary>
    QTEFail = 6,
    /// <summary>中文含义：小刀取出。小刀拔出起手</summary>
    KnifeTake = 7,
    /// <summary>中文含义：已预留小刀装备。保留编号：旧小刀拔出衔接，正式状态机不再使用</summary>
    ReservedKnifeEquip = 8,
    /// <summary>中文含义：小刀隐藏。小刀收回</summary>
    KnifeHide = 9,
    /// <summary>中文含义：小刀快速突刺。小刀快速攻击</summary>
    KnifeQuickAttack = 10,
    /// <summary>中文含义：小刀攻击04。小刀普通攻击第一段</summary>
    KnifeAttack04 = 11,
    /// <summary>中文含义：小刀攻击02。小刀普通攻击第二段</summary>
    KnifeAttack02 = 12,
    /// <summary>中文含义：配对动作右侧切口踢击。快速反应事件右侧切入踢</summary>
    QTERightCutKick = 13,
    /// <summary>中文含义：配对动作旋转中挂钩踢击。快速反应事件旋转钩踢</summary>
    QTESpinningHookKick = 14,
    /// <summary>中文含义：小刀招架。小刀瞬时弹反</summary>
    KnifeParry = 15,
    /// <summary>中文含义：窗口攀爬穿过。从窗口普通爬越</summary>
    WindowClimbThrough = 16,
    /// <summary>中文含义：窗口掉落下落。窗口自动下落循环</summary>
    WindowDropFall = 17,
    /// <summary>中文含义：窗口掉落落地。窗口自动下落落地</summary>
    WindowDropLand = 18,
    WoodenBoxKick = 19,
    WoodenBoxStomp = 20,
    WoodenBoxElbow = 21,
    WoodenBoxPistolElbow = 22,
    VillageGateOpen = 23,
    ItemPickup = 24,
    LadderPush = 25,
    LadderTraversal = 26,
    CabinetPush = 27,
    TrapCatch = 28,
    TrapEscape = 29,
    EnvironmentalFall = 30,
    DoorBrace = 31
}

internal static class MeleeActionKinds
{
    internal static bool IsWoodenBox(FullBodyActionType action)
    {
        return action is FullBodyActionType.WoodenBoxKick or FullBodyActionType.WoodenBoxStomp
            or FullBodyActionType.WoodenBoxElbow or FullBodyActionType.WoodenBoxPistolElbow;
    }

    internal static bool IsEnemyKick(FullBodyActionType action)
    {
        return action is FullBodyActionType.QTERightCutKick or FullBodyActionType.QTESpinningHookKick;
    }

    internal static int EnemyKickFrames(FullBodyActionType action)
    {
        if (action == FullBodyActionType.QTESpinningHookKick)
        {
            return 137;
        }
        return 135;
    }

    internal static float EnemyKickContactTime(FullBodyActionType action)
    {
        if (action == FullBodyActionType.QTESpinningHookKick)
        {
            return 58f / EnemyKickFrames(action);
        }
        return 57f / EnemyKickFrames(action);
    }
}

/// <summary>
/// 全身动作状态行为。
/// 只负责发送动作进入、完成、中断和退出通知，不修改任何参数、图层权重或业务状态。
/// 挂载到全身动作层需要完成握手的动作状态。
/// 死亡状态用完成通知结束死亡流程，但不会恢复或退出该状态。
/// </summary>
public class FullBodyActionSMB : StateMachineBehaviour
{
    /// <summary>当前状态对应的全身动作类型，不通过状态名判断。</summary>
    [Header("动作类型")]
    [Tooltip("当前状态对应的全身动作类型，不通过状态名判断")]
    public FullBodyActionType actionType = FullBodyActionType.Hit;
    /// <summary>标准化时间达到此值时发送正常完成通知。</summary>
    [Header("通知时刻")]
    [Tooltip("标准化时间达到此值时发送正常完成通知")]
    [Range(0.1f, 1f)]
    public float completeNormalizedTime = 0.9f;
    /// <summary>是否关闭时由业务代码主动推进（下落/落地等循环或预测状态）。开启后达到完成阈值时自动发送完成通知；关闭时由业务代码主动推进（下落/落地等循环或预测状态）。</summary>
    [Header("自动完成")]
    [Tooltip("开启后达到完成阈值时自动发送完成通知；关闭时由业务代码主动推进（下落/落地等循环或预测状态）")]
    public bool autoComplete = true;
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
    public static event Action<Animator, FullBodyActionType, int> OnActionEntered;
    /// <summary>中文含义：动作完成时的通知回调。动作完成通知（标准化时间达到阈值）</summary>
    public static event Action<Animator, FullBodyActionType, int> OnActionCompleted;
    /// <summary>中文含义：动作中断时的通知回调。动作中断通知（未正常完成就退出）</summary>
    public static event Action<Animator, FullBodyActionType, int> OnActionInterrupted;
    /// <summary>中文含义：动作退出时的通知回调。状态退出通知（无论正常完成还是中断都发送）</summary>
    public static event Action<Animator, FullBodyActionType, int, bool> OnActionExited;
    /// <summary>ResetEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        globalEntrySequence = 0;
        OnActionEntered = null;
        OnActionCompleted = null;
        OnActionInterrupted = null;
        OnActionExited = null;
    }

    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, FullBodyActionType, int&gt;.Invoke(Animator, FullBodyActionType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    private float nativeHitExitSeconds;
    private readonly System.Collections.Generic.List<AnimatorClipInfo> hitClips = new();

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        nativeHitExitSeconds = 0f;
        if (actionType == FullBodyActionType.Hit)
        {
            animator.GetCurrentAnimatorClipInfo(layerIndex, hitClips);
            foreach (var item in hitClips)
            {
                if (item.weight < .5f) continue;
                int endFrame = item.clip.name switch
                {
                    "LeonDamage_0100" => 68,
                    "LeonDamage_0102" => 77,
                    "LeonDamage_0104" => 85,
                    _ => 0
                };
                nativeHitExitSeconds = endFrame * completeNormalizedTime / 60f;
            }
        }
        hasCompleted = false;
        entrySequence = ++globalEntrySequence;
        // 判空前缓存的动作进入时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::FullBodyActionType, int> receiverValue1 = OnActionEntered;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(animator, actionType, entrySequence);
        }
    }

    /// <summary>实现基类的 OnStateUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, FullBodyActionType, int&gt;.Invoke(Animator, FullBodyActionType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (hasCompleted || !autoComplete)
        {
            return;
        }

        float completionTime = completeNormalizedTime;
        if (actionType == FullBodyActionType.Hit && nativeHitExitSeconds > 0f && stateInfo.length > 0f)
        {
            completionTime = nativeHitExitSeconds / stateInfo.length;
        }
        if (actionType == FullBodyActionType.Death)
        {
            completionTime = Mathf.Max(completionTime, 1f);
        }

        if (stateInfo.normalizedTime >= completionTime)
        {
            hasCompleted = true;
            // 判空前缓存的动作完成时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::FullBodyActionType, int> receiverValue2 = OnActionCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.Invoke(animator, actionType, entrySequence);
            }
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;Animator, FullBodyActionType, int, bool&gt;.Invoke(Animator, FullBodyActionType, int, bool)、Action&lt;Animator, FullBodyActionType, int&gt;.Invoke(Animator, FullBodyActionType, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!hasCompleted)
        {
            // 判空前缓存的动作中断时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::FullBodyActionType, int> receiverValue3 = OnActionInterrupted;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Invoke(animator, actionType, entrySequence);
            }
        }

        // 判空前缓存的动作退出时的通知回调。
global::System.Action<global::UnityEngine.Animator, global::FullBodyActionType, int, bool> receiverValue4 = OnActionExited;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Invoke(animator, actionType, entrySequence, hasCompleted);
        }
    }
}
