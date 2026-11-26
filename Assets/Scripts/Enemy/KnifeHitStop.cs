using UnityEngine;

/// <summary>只冻结参与本次刀刃接触的敌人，退出时恢复原播放速度。</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-100)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "小刀受击停顿")]
public sealed class KnifeHitStop : MonoBehaviour
{
    /// <summary>动画器。</summary>
    [Header("动画引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("动画器")]
    [UnityEngine.InspectorName("动画器")]
    public Animator animator;
    /// <summary>结束时间。</summary>
    private double endTime;
    /// <summary>原始速度。</summary>
    private float originalSpeed;
    /// <summary>是否具有冻结。</summary>
    private bool hasFreeze;
    /// <summary>是否命中顿帧。</summary>
    public bool IsHitStop => hasFreeze && Time.timeAsDouble < endTime;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    /// <summary>Pause 的业务处理入口；本方法直接依赖 Math.Max(double, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)；下游（含内部回调体） Math.Max(double, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="seconds">秒。</param>
    public void Pause(float seconds)
    {
        if (!isActiveAndEnabled || animator == null)
        {
            return;
        }

        if (!hasFreeze)
        {
            originalSpeed = animator.speed;
            hasFreeze = true;
        }

        endTime = System.Math.Max(endTime, Time.timeAsDouble + seconds);
        animator.speed = 0;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeHitStop.Restore()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        if (hasFreeze && !IsHitStop)
        {
            Restore();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeHitStop.Restore()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable() => Restore();
    /// <summary>Restore 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifeHitStop.OnDisable()、KnifeHitStop.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Restore()
    {
        if (hasFreeze && animator != null && animator.speed == 0)
        {
            animator.speed = originalSpeed;
        }

        hasFreeze = false;
        endTime = 0;
    }
}
