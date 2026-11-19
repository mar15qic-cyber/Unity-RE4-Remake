using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个预制体的对象池。
/// 维护空闲列表和活跃列表，负责实例的创建、获取、回收和统计。
/// 不负责业务逻辑（掉落概率、特效选择等）。
/// </summary>
/// <remarks>预制件对象池宿主，负责既有实例租用、返还和重置协议；消费方必须遵守当前租用生命周期，不能让旧回调修改新一轮对象。</remarks>
public class PrefabPool
{
    // ═══════════════════ 配置 ═══════════════════
    /// <summary>中文含义：预制件。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.LogWarning(object) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform, bool) 的参数、PrefabPool.CreateInstance() 等共 5 处。</summary>
    private readonly GameObject prefab;
    /// <summary>中文含义：对象池根节点。只读字段，初始化后不由普通写入口替换。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Transform, bool) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。</summary>
    private readonly Transform poolRoot;
    /// <summary>中文含义：最大尺寸。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.LogWarning(object) 的参数、作为 Mathf.Clamp(int, int, int) 的参数、PrefabPool.Get(Vector3, Quaternion, Transform) 等共 4 处。</summary>
    private readonly int maxSize;
    /// <summary>中文含义：是否可扩容。只读字段，初始化后不由普通写入口替换。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)、PrefabPool.Prewarm(int)。</summary>
    private readonly bool expandable;
    /// <summary>中文含义：当前游戏对象对象池管理器。只读字段，初始化后不由普通写入口替换。使用位置：PrefabPool.CreateInstance()、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)、PrefabPool.ReturnInstance(GameObject)。</summary>
    private readonly GameObjectPoolManager manager;
    // ═══════════════════ 实例集合 ═══════════════════
    /// <summary>中文含义：自由。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.LogWarning(object) 的参数、作为 List&lt;GameObject&gt;.RemoveAt(int) 的参数、作为 List&lt;GameObject&gt;.this[int] 的参数 等共 10 处。</summary>
    private readonly List<GameObject> free = new List<GameObject>();
    /// <summary>中文含义：生效。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.LogWarning(object) 的参数、PrefabPool.ActiveCount.get、PrefabPool.CleanupNullReferences() 等共 8 处。</summary>
    private readonly List<GameObject> active = new List<GameObject>();
    // ═══════════════════ 统计 ═══════════════════
    /// <summary>中文含义：总计已创建。使用位置：PrefabPool.CreateInstance()、PrefabPool.GetStats()、PrefabPool.TotalCreated.get。</summary>
    private readonly bool affectsNavigation;
    private int totalCreated;
    /// <summary>中文含义：峰值生效。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.GetStats()、PrefabPool.PeakActive.get。</summary>
    private int peakActive;
    /// <summary>中文含义：展开数量。使用位置：PrefabPool.ExpandCount.get、PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.GetStats()。</summary>
    private int expandCount;
    /// <summary>中文含义：被拒绝的取得数量。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.GetStats()、PrefabPool.RejectedGetCount.get。</summary>
    private int rejectedGetCount;
    /// <summary>中文含义：非法返回数量。使用位置：PrefabPool.GetStats()、PrefabPool.IllegalReturnCount.get、PrefabPool.ReturnInstance(GameObject)。</summary>
    private int illegalReturnCount;
    // ═══════════════════ 属性 ═══════════════════
    /// <summary>中文含义：预制件。</summary>
    public GameObject Prefab => prefab;
    /// <summary>中文含义：总计已创建。</summary>
    public int TotalCreated => totalCreated;
    /// <summary>中文含义：生效数量。</summary>
    public int ActiveCount => active.Count;
    /// <summary>中文含义：自由数量。</summary>
    public int FreeCount => free.Count;
    /// <summary>中文含义：峰值生效。</summary>
    public int PeakActive => peakActive;
    /// <summary>中文含义：展开数量。</summary>
    public int ExpandCount => expandCount;
    /// <summary>中文含义：被拒绝的取得数量。</summary>
    public int RejectedGetCount => rejectedGetCount;
    /// <summary>中文含义：非法返回数量。</summary>
    public int IllegalReturnCount => illegalReturnCount;

    // ═══════════════════ 构造 ═══════════════════
    /// <summary>构造并初始化 PrefabPool 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.GetOrCreatePool(GameObject)、GameObjectPoolManager.Start()；下游（含内部回调体） List&lt;GameObject&gt;.Add(GameObject)、Mathf.Clamp(int, int, int)、Mathf.Max(int, int)、PrefabPool.CreateInstance()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">中文含义：预制件。使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。</param>
    /// <param name="poolRoot">中文含义：对象池根节点。使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。</param>
    /// <param name="manager">中文含义：当前游戏对象对象池管理器。使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。</param>
    /// <param name="initialSize">中文含义：初始尺寸。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.Max(int, int) 的参数。</param>
    /// <param name="maxSize">中文含义：最大尺寸。使用位置：作为 Mathf.Max(int, int) 的参数。</param>
    /// <param name="expandable">中文含义：是否可扩容。使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。</param>
    public PrefabPool(GameObject prefab, Transform poolRoot, GameObjectPoolManager manager, int initialSize, int maxSize, bool expandable, bool affectsNavigation = true)
    {
        this.prefab = prefab;
        this.poolRoot = poolRoot;
        this.manager = manager;
        this.maxSize = Mathf.Max(1, maxSize);
        this.expandable = expandable;
        this.affectsNavigation = affectsNavigation;
        // initialCount（初始数量）：使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。
int initialCount;
        if (expandable)
        {
            initialCount = Mathf.Max(0, initialSize);
        }
        else
        {
            initialCount = Mathf.Clamp(initialSize, 0, this.maxSize);
        }

        // i（当前遍历索引）：使用位置：PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)。
for (int i = 0; i < initialCount; i++)
        {
            free.Add(CreateInstance());
        }
    }

    // ═══════════════════ 获取 ═══════════════════
    /// <summary>Get 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、GameObject.GetComponent&lt;PooledObject&gt;()、GameObject.SetActive(bool)、IPoolable.OnPoolGet()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.RemoveAt(int)、NavigationCollisionCache.InvalidateHierarchy(Transform) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)；下游（含内部回调体） Debug.LogWarning(object)、GameObject.GetComponent&lt;PooledObject&gt;()、GameObject.SetActive(bool)、IPoolable.OnPoolGet()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.RemoveAt(int)、NavigationCollisionCache.InvalidateHierarchy(Transform) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、PrefabPool.Get(Vector3, Quaternion, Transform)。</param>
    public GameObject Get(Vector3 position, Quaternion rotation, Transform parent)
    {
        // instance（实例）：使用位置：作为 List<GameObject>.Add(GameObject) 的参数、作为 NavigationCollisionCache.InvalidateHierarchy(Transform) 的参数、PrefabPool.Get(Vector3, Quaternion, Transform)。
GameObject instance = null;
        // 优先复用空闲对象
        while (free.Count > 0)
        {
            instance = free[free.Count - 1];
            free.RemoveAt(free.Count - 1);
            // 安全清理已被外部 Destroy 的空引用
            if (instance == null)
            {
                instance = null;
                continue;
            }

            break;
        }

        // 空闲池为空，尝试扩容
        if (instance == null)
        {
            // currentTotal（当前总计）：使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。
int currentTotal = active.Count + free.Count;
            // canExpand（是否可以展开）：使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。
bool canExpand = expandable || currentTotal < maxSize;
            if (canExpand)
            {
                instance = CreateInstance();
                expandCount++;
            }
            else
            {
                rejectedGetCount++;
                Debug.LogWarning($"[Pool] 池已满且不可扩容: '{prefab.name}' " + $"(active={active.Count}, free={free.Count}, max={maxSize})");
                return null;
            }
        }

        // 标记为租用
        // pooled（池化的）：保存 GameObject.GetComponent<PooledObject>() 的返回结果，供当前步骤使用。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。
var pooled = instance.GetComponent<PooledObject>();
        pooled.MarkRented();
        // 设置父节点
        if (parent != null)
        {
            instance.transform.SetParent(parent, false);
        }
        else
        {
            instance.transform.SetParent(poolRoot, false);
        }

        // 设置位置和旋转
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        // 激活 GameObject
        if (affectsNavigation)
        {
            NavigationCollisionCache.InvalidateHierarchy(instance.transform);
        }
        instance.SetActive(true);
        // 调用所有 IPoolable.OnPoolGet()
        // poolables（可池化对象集合）：保存 PooledObject.GetPoolables() 的返回结果，供当前步骤使用。使用位置：PrefabPool.Get(Vector3, Quaternion, Transform)。
var poolables = pooled.GetPoolables();
        // i（当前遍历索引）：使用位置：作为 List<IPoolable>.this[int] 的参数、PrefabPool.Get(Vector3, Quaternion, Transform)。
for (int i = 0; i < poolables.Count; i++)
        {
            poolables[i].OnPoolGet();
        }

        active.Add(instance);
        if (active.Count > peakActive)
        {
            peakActive = active.Count;
        }

        return instance;
    }

    // ═══════════════════ 回收 ═══════════════════
    /// <summary>ReturnInstance 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;PooledObject&gt;()、GameObject.SetActive(bool)、IPoolable.OnPoolReturn()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)、List&lt;GameObject&gt;.Remove(GameObject)、NavigationCollisionCache.InvalidateHierarchy(Transform) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.Return(GameObject)、PrefabPool.ReturnAll()；下游（含内部回调体） GameObject.GetComponent&lt;PooledObject&gt;()、GameObject.SetActive(bool)、IPoolable.OnPoolReturn()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)、List&lt;GameObject&gt;.Remove(GameObject)、NavigationCollisionCache.InvalidateHierarchy(Transform) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">中文含义：实例。使用位置：作为 List&lt;GameObject&gt;.Add(GameObject) 的参数、作为 List&lt;GameObject&gt;.Contains(GameObject) 的参数、作为 List&lt;GameObject&gt;.Remove(GameObject) 的参数 等共 5 处。</param>
    public bool ReturnInstance(GameObject instance)
    {
        if (instance == null)
        {
            return false;
        }

        // pooled（池化的）：保存 GameObject.GetComponent<PooledObject>() 的返回结果，供当前步骤使用。使用位置：PrefabPool.ReturnInstance(GameObject)。
var pooled = instance.GetComponent<PooledObject>();
        if (pooled == null || pooled.Pool != this || pooled.Manager != manager || !pooled.IsActive || !active.Contains(instance))
        {
            illegalReturnCount++;
            return false;
        }

        // 标记为已回收
        pooled.MarkReturned();
        // 调用所有 IPoolable.OnPoolReturn()
        // poolables（可池化对象集合）：保存 PooledObject.GetPoolables() 的返回结果，供当前步骤使用。使用位置：PrefabPool.ReturnInstance(GameObject)。
var poolables = pooled.GetPoolables();
        // i（当前遍历索引）：使用位置：作为 List<IPoolable>.this[int] 的参数、PrefabPool.ReturnInstance(GameObject)。
for (int i = 0; i < poolables.Count; i++)
        {
            poolables[i].OnPoolReturn();
        }

        // 停用对象
        if (affectsNavigation)
        {
            NavigationCollisionCache.InvalidateHierarchy(instance.transform);
        }
        instance.SetActive(false);
        // 移回池根节点并重置局部变换
        instance.transform.SetParent(poolRoot, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        // 从活跃列表移除，加入空闲列表
        active.Remove(instance);
        free.Add(instance);
        return true;
    }

    // ═══════════════════ 预热 ═══════════════════
    /// <summary>Prewarm 的业务处理入口；本方法直接依赖 List&lt;GameObject&gt;.Add(GameObject)、Mathf.Clamp(int, int, int)、Mathf.Max(int, int)、PrefabPool.CreateInstance()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.Prewarm(GameObject, int)、GameObjectPoolManager.Start()；下游（含内部回调体） List&lt;GameObject&gt;.Add(GameObject)、Mathf.Clamp(int, int, int)、Mathf.Max(int, int)、PrefabPool.CreateInstance()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="count">中文含义：数量。使用位置：作为 Mathf.Clamp(int, int, int) 的参数、作为 Mathf.Max(int, int) 的参数。</param>
    public void Prewarm(int count)
    {
        // target（目标）：使用位置：PrefabPool.Prewarm(int)。
int target;
        if (expandable)
        {
            target = Mathf.Max(count, 0);
        }
        else
        {
            target = Mathf.Clamp(count, 0, maxSize);
        }

        // toCreate（目标创建）：使用位置：PrefabPool.Prewarm(int)。
int toCreate = target - (free.Count + active.Count);
        // i（当前遍历索引）：使用位置：PrefabPool.Prewarm(int)。
for (int i = 0; i < toCreate; i++)
        {
            free.Add(CreateInstance());
        }
    }

    // ═══════════════════ 强制回收所有 ═══════════════════
    /// <summary>ReturnAll 的业务处理入口；本方法直接依赖 List&lt;GameObject&gt;.RemoveAt(int)、PrefabPool.ReturnInstance(GameObject)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.OnDestroy()；下游（含内部回调体） List&lt;GameObject&gt;.RemoveAt(int)、PrefabPool.ReturnInstance(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReturnAll()
    {
        // i（当前遍历索引）：使用位置：作为 List<GameObject>.RemoveAt(int) 的参数、作为 List<GameObject>.this[int] 的参数、PrefabPool.ReturnAll()。
for (int i = active.Count - 1; i >= 0; i--)
        {
            // instance（实例）：使用位置：作为 PrefabPool.ReturnInstance(GameObject) 的参数、PrefabPool.ReturnAll()。
var instance = active[i];
            if (instance == null)
            {
                active.RemoveAt(i);
                continue;
            }

            ReturnInstance(instance);
        }
    }

    // ═══════════════════ 统计 ═══════════════════
    /// <summary>GetStats 的业务处理入口；本方法直接依赖 PoolStats.PoolStats()、PrefabPool.CleanupNullReferences()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameObjectPoolManager.GetStats(GameObject)；下游（含内部回调体） PoolStats.PoolStats()、PrefabPool.CleanupNullReferences()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public PoolStats GetStats()
    {
        // 清理空引用以获得准确计数
        CleanupNullReferences();
        return new PoolStats
        {
            totalCreated = totalCreated,
            activeCount = active.Count,
            freeCount = free.Count,
            peakActive = peakActive,
            expandCount = expandCount,
            rejectedGetCount = rejectedGetCount,
            illegalReturnCount = illegalReturnCount
        };
    }

    // ═══════════════════ 内部方法 ═══════════════════
    /// <summary>CreateInstance 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;ParticleSystemReset&gt;()、GameObject.AddComponent&lt;PooledObject&gt;()、GameObject.AddComponent&lt;RigidbodyReset&gt;()、GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;()、GameObject.GetComponentInChildren&lt;ParticleSystem&gt;(bool)、GameObject.SetActive(bool) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.PrefabPool(GameObject, Transform, GameObjectPoolManager, int, int, bool)、PrefabPool.Prewarm(int)；下游（含内部回调体） GameObject.AddComponent&lt;ParticleSystemReset&gt;()、GameObject.AddComponent&lt;PooledObject&gt;()、GameObject.AddComponent&lt;RigidbodyReset&gt;()、GameObject.GetComponent&lt;ParticleSystem&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;()、GameObject.GetComponentInChildren&lt;ParticleSystem&gt;(bool)、GameObject.SetActive(bool) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private GameObject CreateInstance()
    {
        // instance（实例）：保存 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的返回结果，供当前步骤使用。使用位置：作为 NavigationCollisionCache.InvalidateHierarchy(Transform) 的参数、PrefabPool.CreateInstance()。
var instance = Object.Instantiate(prefab, poolRoot, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.name = prefab.name + "_Pooled";
        if (affectsNavigation)
        {
            NavigationCollisionCache.InvalidateHierarchy(instance.transform);
        }
        instance.SetActive(false);
        // 添加身份组件
        // pooled（池化的）：保存 GameObject.AddComponent<PooledObject>() 的返回结果，供当前步骤使用。使用位置：PrefabPool.CreateInstance()。
var pooled = instance.AddComponent<PooledObject>();
        pooled.SourcePrefab = prefab;
        pooled.Manager = manager;
        pooled.Pool = this;
        // 自动检测并添加重置组件
        if (instance.GetComponent<Rigidbody>() != null)
        {
            instance.AddComponent<RigidbodyReset>();
        }

        if (instance.GetComponent<ParticleSystem>() != null || instance.GetComponentInChildren<ParticleSystem>(true) != null)
        {
            instance.AddComponent<ParticleSystemReset>();
        }

        totalCreated++;
        return instance;
    }

    /// <summary>CleanupNullReferences 的业务处理入口；本方法直接依赖 List&lt;GameObject&gt;.RemoveAt(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PrefabPool.GetStats()；下游（含内部回调体） List&lt;GameObject&gt;.RemoveAt(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CleanupNullReferences()
    {
        // i（当前遍历索引）：使用位置：作为 List<GameObject>.RemoveAt(int) 的参数、作为 List<GameObject>.this[int] 的参数、PrefabPool.CleanupNullReferences()。
for (int i = free.Count - 1; i >= 0; i--)
        {
            if (free[i] == null)
            {
                free.RemoveAt(i);
            }
        }

        // i（当前遍历索引）：使用位置：作为 List<GameObject>.RemoveAt(int) 的参数、作为 List<GameObject>.this[int] 的参数、PrefabPool.CleanupNullReferences()。
for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i] == null)
            {
                active.RemoveAt(i);
            }
        }
    }
}

/// <summary>
/// 对象池统计信息
/// </summary>
public struct PoolStats
{
    /// <summary>中文含义：总计已创建。使用位置：PrefabPool.GetStats()。</summary>
    public int totalCreated;
    /// <summary>中文含义：生效数量。使用位置：PrefabPool.GetStats()。</summary>
    public int activeCount;
    /// <summary>中文含义：自由数量。使用位置：PrefabPool.GetStats()。</summary>
    public int freeCount;
    /// <summary>中文含义：峰值生效。使用位置：PrefabPool.GetStats()。</summary>
    public int peakActive;
    /// <summary>中文含义：展开数量。使用位置：PrefabPool.GetStats()。</summary>
    public int expandCount;
    /// <summary>中文含义：被拒绝的取得数量。使用位置：PrefabPool.GetStats()。</summary>
    public int rejectedGetCount;
    /// <summary>中文含义：非法返回数量。使用位置：PrefabPool.GetStats()。</summary>
    public int illegalReturnCount;
}
