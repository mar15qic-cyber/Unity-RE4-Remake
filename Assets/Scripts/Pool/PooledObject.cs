using UnityEngine;

/// <summary>
/// 池化对象身份组件。
/// 由对象池在创建实例时自动添加，业务模块无需手动添加。
/// 通过此组件识别对象所属的池、原始预制体、租用状态和版本号。
/// </summary>
public class PooledObject : MonoBehaviour
{
    /// <summary>中文含义：来源预制件。原始预制体引用</summary>
    [System.NonSerialized]
    public GameObject SourcePrefab;
    /// <summary>中文含义：管理器。所属对象池管理器</summary>
    [System.NonSerialized]
    public GameObjectPoolManager Manager;
    /// <summary>中文含义：对象池。所属预制体池</summary>
    [System.NonSerialized]
    public PrefabPool Pool;
    /// <summary>租用版本。</summary>
    private int rentalVersion;
    /// <summary>是否生效。</summary>
    private bool isActive;
    /// <summary>可池化对象集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly System.Collections.Generic.List<IPoolable> poolables = new System.Collections.Generic.List<IPoolable>(4);
    /// <summary>poolableCacheReady 表示“可池化对象缓存是否就绪”；started 表示“是否已开始”。</summary>
    private bool poolableCacheReady, started;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） PooledObject.RebuildPoolableCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        started = true;
        RebuildPoolableCache();
    }

    /// <summary>运行中新增或删除池化组件后显式调用，下次租用或回收前重建。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void InvalidatePoolableCache() => poolableCacheReady = false;
    /// <summary>RebuildPoolableCache 的业务处理入口；本方法直接依赖 Component.GetComponents&lt;IPoolable&gt;(List&lt;IPoolable&gt;)、List&lt;IPoolable&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PooledObject.GetPoolables()、PooledObject.Start()；下游（含内部回调体） Component.GetComponents&lt;IPoolable&gt;(List&lt;IPoolable&gt;)、List&lt;IPoolable&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RebuildPoolableCache()
    {
        poolables.Clear();
        GetComponents(poolables);
        poolableCacheReady = started;
    }

    /// <summary>GetPoolables 的业务处理入口；本方法直接依赖 PooledObject.RebuildPoolableCache()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.ReturnInstance(GameObject)；下游（含内部回调体） PooledObject.RebuildPoolableCache()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal System.Collections.Generic.List<IPoolable> GetPoolables()
    {
        // 首次激活可能早于启动回调，使用列表查询仍不生成组件数组。
        if (!poolableCacheReady)
        {
            RebuildPoolableCache();
        }

        return poolables;
    }

    /// <summary>中文含义：租用版本。当前租用版本号，每次 Get 递增</summary>
    public int RentalVersion => rentalVersion;
    /// <summary>中文含义：是否生效。当前是否处于租用（活跃）状态</summary>
    public bool IsActive => isActive;

    /// <summary>标记为已租用，递增版本号</summary>
    /// <remarks>静态调用链：上游 PrefabPool.Get(Vector3, Quaternion, Transform)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void MarkRented()
    {
        rentalVersion++;
        isActive = true;
    }

    /// <summary>标记为已回收</summary>
    /// <remarks>静态调用链：上游 PrefabPool.ReturnInstance(GameObject)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void MarkReturned()
    {
        isActive = false;
    }
}
