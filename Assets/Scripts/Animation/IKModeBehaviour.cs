using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// IK模式枚举
/// </summary>
public enum IKMode
{
    /// <summary>中文含义：无（未选择或尚未发生）。关闭IK，让动画完全控制手臂</summary>
    None = 0,
    /// <summary>中文含义：瞄准。瞄准/射击，IK全开拉到枪把位置</summary>
    Aim = 1,
    /// <summary>中文含义：过渡。换弹/掏枪，IK半开（避免拉偏手部动画）</summary>
    Transition = 2,
    /// <summary>中文含义：命中。受伤硬直，IK关闭</summary>
    Hit = 3,
    /// <summary>中文含义：自由环顾。自由视角（按住左Alt非瞄准时）</summary>
    FreeLook = 4,
}

/// <summary>
/// 动画状态机行为：在进入/退出状态时自动切换IK模式和动画层权重。
/// 挂载到Animator Controller中对应状态上即可，不需要C#代码额外调用。
/// </summary>
public class IKModeBehaviour : StateMachineBehaviour
{
    /// <summary>进入此状态时切换到哪个IK模式。</summary>
    [Header("IK模式")]
    [Tooltip("进入此状态时切换到哪个IK模式")]
    public IKMode enterMode = IKMode.None;
    /// <summary>是否在进入时设置Layer权重。</summary>
    [Header("动画层权重")]
    [Tooltip("是否在进入时设置Layer权重")]
    public bool manageLayerWeight = true;
    /// <summary>目标图层。目标图层索引（Layer1=枪械, Layer2=全身动作）。</summary>
    [Tooltip("目标图层索引（Layer1=枪械, Layer2=全身动作）")]
    public int targetLayer = 2;
    /// <summary>进入状态时的图层权重。</summary>
    [Tooltip("进入状态时的图层权重")]
    [Range(0f, 1f)]
    public float enterWeight = 1f;
    /// <summary>退出状态时的图层权重。</summary>
    [Tooltip("退出状态时的图层权重")]
    [Range(0f, 1f)]
    public float exitWeight = 0f;
    /// <summary>反向动力学绑定。</summary>
    private LeonAnimRig ikRig;
    /// <summary>上一次模式反向动力学权重。</summary>
    private float lastModeIKWeight;
    /// <summary>实现基类的 OnStateEnter 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.SetLayerWeight(int, float)、Component.GetComponent&lt;LeonAnimRig&gt;()、LeonAnimRig.GetIKWeight()、LeonAnimRig.SetIKMode(IKMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (ikRig == null)
        {
            ikRig = animator.GetComponent<LeonAnimRig>();
        }

        // 切换IK模式
        if (ikRig != null)
        {
            lastModeIKWeight = ikRig.GetIKWeight();
            ikRig.SetIKMode(enterMode);
        }

        // 设置图层权重
        if (manageLayerWeight)
        {
            animator.SetLayerWeight(targetLayer, enterWeight);
        }
    }

    /// <summary>实现基类的 OnStateExit 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.SetLayerWeight(int, float)、LeonAnimRig.SetIKMode(IKMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="stateInfo">中文含义：当前动画状态信息。</param>
    /// <param name="layerIndex">中文含义：动画图层索引。</param>
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 退出时恢复默认IK模式
        if (ikRig != null)
        {
            ikRig.SetIKMode(IKMode.None);
        }

        // 恢复图层权重
        if (manageLayerWeight && targetLayer == 2)
        {
            animator.SetLayerWeight(targetLayer, exitWeight);
        }
    }
}
