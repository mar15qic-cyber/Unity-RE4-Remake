using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 预热配置条目（Inspector 可配置）
/// </summary>
[System.Serializable]
public struct PrewarmConfig
{
    /// <summary>要预热的预制体。</summary>
    [Tooltip("要预热的预制体")]
    public GameObject prefab;
    /// <summary>预热数量。</summary>
    [Tooltip("预热数量")]
    [Min(0)]
    public int prewarmCount;
    /// <summary>该预制体池的最大容量。</summary>
    [Tooltip("该预制体池的最大容量")]
    [Min(1)]
    public int maxSize;
    /// <summary>达到上限后是否允许扩容。</summary>
    [Tooltip("达到上限后是否允许扩容")]
    public bool expandable;
}

/// <summary>
/// 通用 GameObject 对象池管理器。
/// 场景级单例（不跨场景存活），按预制体区分独立子池。
/// 只负责实例的获取、回收、延迟回收和预热，不负责任何业务逻辑。
/// </summary>
public class GameObjectPoolManager : Singleton<GameObjectPoolManager>
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>未在预热列表中配置的预制体的默认最大容量。</summary>
    [Header("默认池配置")]
    [Tooltip("未在预热列表中配置的预制体的默认最大容量")]
    [Min(1)]
    [SerializeField]
    private int defaultMaxSize = 50;
    /// <summary>未在预热列表中配置的预制体是否默认允许扩容。</summary>
    [Tooltip("未在预热列表中配置的预制体是否默认允许扩容")]
    [SerializeField]
    private bool defaultExpandable = true;
    /// <summary>场景启动时预热的预制体列表。</summary>
    [Header("预热配置")]
    [Tooltip("场景启动时预热的预制体列表")]
    [SerializeField]
    private PrewarmConfig[] prewarmConfigs = new PrewarmConfig[0];
    // ═══════════════════ 内部状态 ═══════════════════
    /// <summary>对象池集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<int, PrefabPool> pools = new Dictionary<int, PrefabPool>();
    /// <summary>对象池根节点。</summary>
    private Transform poolRoot;
    /// <summary>延迟的归还列表。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<int, PooledObject> pooledInstances = new();

    internal void RegisterInstance(GameObject instance, PooledObject pooled)
    {
        pooledInstances[instance.GetInstanceID()] = pooled;
    }

    internal void ForgetInstance(int instanceId)
    {
        pooledInstances.Remove(instanceId);
    }

    internal PooledObject ReadInstance(GameObject instance)
    {
        if (pooledInstances.TryGetValue(instance.GetInstanceID(), out var pooled) && pooled != null)
        {
            return pooled;
        }
        return null;
    }

    private readonly List<DelayedReturn> delayedReturns = new List<DelayedReturn>(64);
    /// <summary>对象池与重置的结构化数据 DelayedReturn；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct DelayedReturn
    {
        /// <summary>池化的。</summary>
        internal PooledObject pooled;
        /// <summary>version 表示“版本”；earliestFrame 表示“最早帧”。</summary>
        internal int version, earliestFrame;
        /// <summary>到期。</summary>
        internal double due;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObjectPoolManager.Return(GameObject)、List&lt;GameObjectPoolManager.DelayedReturn&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        // 缩放时间在暂停时不会推进；即使零延时，也最早在登记后的下一帧回收。
        if (Time.timeScale <= 0f)
        {
            return;
        }

        // 当前时间。
double now = Time.timeAsDouble;
        // 当前遍历索引。
for (int i = delayedReturns.Count - 1; i >= 0; i--)
        {
            // 当前延迟的返回。
DelayedReturn record = delayedReturns[i];
            // 是否已过期。
bool expired = record.pooled == null || record.pooled.Manager != this || !record.pooled.IsActive || record.pooled.RentalVersion != record.version;
            if (!expired && (Time.frameCount < record.earliestFrame || now < record.due))
            {
                continue;
            }

            delayedReturns.RemoveAt(i);
            if (!expired)
            {
                Return(record.pooled.gameObject);
            }
        }
    }

    // ═══════════════════ 生命周期 ═══════════════════
    /// <summary>实现基类的 Awake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.GameObject(string)、Singleton&lt;GameObjectPoolManager&gt;.Awake()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void Awake()
    {
        base.Awake();
        poolRoot = new GameObject("GameObjectPool_Root").transform;
        poolRoot.SetParent(transform, false);
        poolRoot.localPosition = Vector3.zero;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;int, PrefabPool&gt;.Add(int, PrefabPool)、Dictionary&lt;int, PrefabPool&gt;.TryGetValue(int, out PrefabPool)、Mathf.Min(int, int)、Object.GetInstanceID()、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)、PrefabPool.Prewarm(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        // 执行预热配置
        // config（当前预热配置）：每轮从预热配置集合取得一个元素，仅代表当前这一项。使用位置：作为 Mathf.Min(int, int) 的参数、作为 PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool) 的参数、作为 PrefabPool.Prewarm(int) 的参数 等共 4 处。
foreach (var config in prewarmConfigs)
        {
            if (config.prefab == null)
            {
                continue;
            }

            // 键。
int key = config.prefab.GetInstanceID();
            if (pools.TryGetValue(key, out var /* 中文含义：已有。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ existing))
            {
                existing.Prewarm(config.prewarmCount);
            }
            else
            {
                // 初始。
int initial = Mathf.Min(config.prewarmCount, config.maxSize);
                // 对象池。
var pool = new PrefabPool(config.prefab, poolRoot, this, initial, config.maxSize, config.expandable);
                pools.Add(key, pool);
            }
        }
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;int, PrefabPool&gt;.Clear()、List&lt;GameObjectPoolManager.DelayedReturn&gt;.Clear()、PrefabPool.ReturnAll()、Singleton&lt;GameObjectPoolManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        delayedReturns.Clear();
        // 强制回收所有活跃对象，避免协程访问已销毁对象
        // kvp（键值对）：每轮从对象池集合取得一个元素，仅代表当前这一项。使用位置：GameObjectPoolManager.OnDestroy()。
foreach (var kvp in pools)
        {
            kvp.Value.ReturnAll();
        }

        pools.Clear();
        pooledInstances.Clear();
        base.OnDestroy();
    }

    // ═══════════════════ 公开接口 ═══════════════════
    /// <summary>
    /// 从池中获取一个实例。
    /// </summary>
    /// <param name="prefab">中文含义：预制件。源预制体</param>
    /// <param name="position">中文含义：位置。世界坐标位置</param>
    /// <param name="rotation">中文含义：旋转。旋转</param>
    /// <param name="parent">中文含义：父级。父节点（null 则放在池根节点下）</param>
    /// <remarks>静态调用链：上游 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3)、GameObjectPoolManager.Get&lt;T&gt;(GameObject, Vector3, Quaternion, Transform)、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)；下游（含内部回调体） Debug.LogWarning(object)、GameObjectPoolManager.GetOrCreatePool(GameObject)、PrefabPool.Get(Vector3, Quaternion, Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[Pool] Get 失败: prefab 为 null。");
            return null;
        }

        // 对象池。
var pool = GetOrCreatePool(prefab);
        return pool.Get(position, rotation, parent);
    }

    /// <summary>
    /// 从池中获取一个实例并返回指定组件类型。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.GetComponent&lt;T&gt;()、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">预制件。</param>
    /// <param name="position">位置。</param>
    /// <param name="rotation">旋转。</param>
    /// <param name="parent">父级。</param>
    public T Get</* 中文含义：由调用方指定的通用类型参数。 */ T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        where T : Component
    {
        // 实例。
var instance = Get(prefab, position, rotation, parent);
        if (instance == null)
        {
            return null;
        }

        return instance.GetComponent<T>();
    }

    /// <summary>
    /// 回收一个实例到其所属的池。
    /// 调用者无需传入 prefab，实例通过 PooledObject 组件自行识别所属池。
    /// </summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.Update()；下游（含内部回调体） Debug.LogWarning(object)、GameObject.GetComponent&lt;PooledObject&gt;()、PrefabPool.ReturnInstance(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">实例。</param>
    public bool Return(GameObject instance)
    {
        if (instance == null)
        {
            Debug.LogWarning("[Pool] Return 失败: instance 为 null。");
            return false;
        }

        // 池化的。
        PooledObject pooled = ReadInstance(instance);
        if (pooled == null)
        {
            Debug.LogWarning($"[Pool] Return 被拒绝: '{instance.name}' 不是池化对象。");
            return false;
        }

        if (!pooled.IsActive || pooled.Manager != this || pooled.Pool == null)
        {
            // 已回收，安全拒绝（不输出警告避免噪音，这是合法的幂等调用场景）
            return false;
        }

        return pooled.Pool.ReturnInstance(instance);
    }

    /// <summary>
    /// 延迟回收一个实例。
    /// 使用租用版本号机制确保旧延迟任务不会回收已重新租用的对象。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)、EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3)、LeonCombat.SpawnParrySparks(Vector3, Vector3, bool)；下游（含内部回调体） Debug.LogWarning(object)、GameObject.GetComponent&lt;PooledObject&gt;()、GameObjectPoolManager.DelayedReturn.DelayedReturn()、List&lt;GameObjectPoolManager.DelayedReturn&gt;.Add(GameObjectPoolManager.DelayedReturn)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">实例。</param>
    /// <param name="delay">延迟。</param>
    public bool ReturnDelayed(GameObject instance, float delay)
    {
        if (instance == null)
        {
            Debug.LogWarning("[Pool] ReturnDelayed 失败: instance 为 null。");
            return false;
        }

        // 池化的。
        PooledObject pooled = ReadInstance(instance);
        if (pooled == null)
        {
            Debug.LogWarning($"[Pool] ReturnDelayed 被拒绝: '{instance.name}' 不是池化对象。");
            return false;
        }

        if (!pooled.IsActive || pooled.Manager != this || pooled.Pool == null)
        {
            return false;
        }

        delayedReturns.Add(new DelayedReturn { pooled = pooled, version = pooled.RentalVersion, earliestFrame = Time.frameCount + 1, due = Time.timeAsDouble + Mathf.Max(0f, delay) });
        return true;
    }

    /// <summary>
    /// 预热指定预制体的池（幂等，多次调用只增不减）。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyHitEffectManager.Start()；下游（含内部回调体） GameObjectPoolManager.GetOrCreatePool(GameObject)、PrefabPool.Prewarm(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">预制件。</param>
    /// <param name="count">数量。</param>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
        {
            return;
        }

        // 对象池。
var pool = GetOrCreatePool(prefab);
        pool.Prewarm(count);
    }

    /// <summary>
    /// 获取指定预制体的池统计信息。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;int, PrefabPool&gt;.TryGetValue(int, out PrefabPool)、Object.GetInstanceID()、PrefabPool.GetStats()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">预制件。</param>
    public PoolStats GetStats(GameObject prefab)
    {
        if (prefab == null)
        {
            return default;
        }

        // 键。
int key = prefab.GetInstanceID();
        if (pools.TryGetValue(key, out var /* 中文含义：对象池。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pool))
        {
            return pool.GetStats();
        }

        return default;
    }

    // ═══════════════════ 内部方法 ═══════════════════
    /// <summary>GetOrCreatePool 的业务处理入口；本方法直接依赖 Dictionary&lt;int, PrefabPool&gt;.Add(int, PrefabPool)、Dictionary&lt;int, PrefabPool&gt;.TryGetValue(int, out PrefabPool)、Object.GetInstanceID()、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.Prewarm(GameObject, int)；下游（含内部回调体） Dictionary&lt;int, PrefabPool&gt;.Add(int, PrefabPool)、Dictionary&lt;int, PrefabPool&gt;.TryGetValue(int, out PrefabPool)、Object.GetInstanceID()、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">预制件。</param>
    internal PrefabPool GetVisualPool(GameObject prefab) => GetOrCreatePool(prefab, false);

    private PrefabPool GetOrCreatePool(GameObject prefab, bool affectsNavigation = true)
    {
        // 键。
int key = prefab.GetInstanceID();
        if (!pools.TryGetValue(key, out var /* 中文含义：对象池。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pool))
        {
            pool = new PrefabPool(prefab, poolRoot, this, 0, defaultMaxSize, defaultExpandable, affectsNavigation);
            pools.Add(key, pool);
        }

        return pool;
    }
}
