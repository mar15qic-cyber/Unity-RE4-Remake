using UnityEngine;

/// <summary>
/// Rigidbody 池化重置组件。
/// 自动添加到含 Rigidbody 的池化实例上。
/// 在取出和回收时清零速度和角速度，防止物理状态残留。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RigidbodyReset : MonoBehaviour, IPoolable
{
    /// <summary>运动刚体。</summary>
    private Rigidbody rb;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Rigidbody&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>OnPoolGet 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPoolGet()
    {
        if (rb == null)
        {
            return;
        }

        // 清零速度和角速度，确保不携带上一次租用的物理状态
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>OnPoolReturn 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPoolReturn()
    {
        if (rb == null)
        {
            return;
        }

        // 回收时清零速度和角速度
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
