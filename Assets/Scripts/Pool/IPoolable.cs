/// <summary>
/// 池化对象生命周期回调接口。
/// 实现此接口的组件会在对象从池中取出和回收时被调用，
/// 用于重置特定组件状态（如 Rigidbody 速度、ParticleSystem 粒子等）。
/// </summary>
public interface IPoolable
{
    /// <summary>对象从池中取出时调用，在此重置为初始状态。</summary>
    /// <remarks>静态调用链：上游 PrefabPool.Get(Vector3, Quaternion, Transform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnPoolGet();
    /// <summary>对象回收入池时调用，在此清理运行时状态。</summary>
    /// <remarks>静态调用链：上游 PrefabPool.ReturnInstance(GameObject)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnPoolReturn();
}
