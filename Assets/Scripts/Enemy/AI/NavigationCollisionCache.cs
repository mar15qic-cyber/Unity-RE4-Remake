using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>敌人行为、动作与通行模块 NavigationCollisionCache；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "导航碰撞缓存")]
// 只缓存对象身份和静态分类，门的开启状态与动作通行资格仍由调用方判断。
internal static class NavigationCollisionCache
{
    /// <summary>敌人行为、动作与通行的结构化数据 CacheEntry；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "导航碰撞缓存+条目")]
    private struct CacheEntry
    {
        /// <summary>父节点。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("父节点")]
        [UnityEngine.InspectorName("父节点")]
        public Transform parentNode;
        /// <summary>根节点。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("根节点")]
        [UnityEngine.InspectorName("根节点")]
        public Transform rootNode;
        /// <summary>敌人。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("敌人")]
        [UnityEngine.InspectorName("敌人")]
        public VillagerController enemy;
        /// <summary>玩家。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("玩家")]
        [UnityEngine.InspectorName("玩家")]
        public LeonController player;
        /// <summary>修饰。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("修饰")]
        [UnityEngine.InspectorName("修饰")]
        public RecastNavmeshModifier modifier;
        /// <summary>是否名称障碍。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("名称障碍")]
        [UnityEngine.InspectorName("名称障碍")]
        public bool nameObstacle;
    }

    /// <summary>缓存。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<Collider, CacheEntry> cache = new(256);
    /// <summary>待处理移除。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<Collider> pendingRemove = new(128);
    /// <summary>图层障碍。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly bool[] layerObstacle = new bool[32];
    /// <summary>是否已初始化。</summary>
    private static bool initialized;
    /// <summary>版本。</summary>
    internal static int Version { get; private set; }

    /// <summary>ClearValue 的业务处理入口；本方法直接依赖 Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Clear()、List&lt;Collider&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationQueryService.Dispose()；下游（含内部回调体） Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Clear()、List&lt;Collider&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ClearValue()
    {
        cache.Clear();
        pendingRemove.Clear();
        initialized = false;
        Version++;
    }

    /// <summary>InvalidateHierarchy 的业务处理入口；本方法直接依赖 Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Remove(Collider)、List&lt;Collider&gt;.Add(Collider)、List&lt;Collider&gt;.Clear()、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationQueryService.Update()、PrefabPool.CreateInstance()、PrefabPool.Get(Vector3, Quaternion, Transform)、PrefabPool.ReturnInstance(GameObject)、VillagerController.OnDisable()；下游（含内部回调体） Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Remove(Collider)、List&lt;Collider&gt;.Add(Collider)、List&lt;Collider&gt;.Clear()、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根变换节点。</param>
    internal static void InvalidateHierarchy(Transform root)
    {
        pendingRemove.Clear();
        // pair（当前键值配对）：每轮从缓存取得一个元素，仅代表当前这一项。使用位置：作为 List<Collider>.Add(Collider) 的参数、NavigationCollisionCache.InvalidateHierarchy(Transform)。
foreach (var pair in cache)
        {
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy || (root != null && pair.Key.transform.IsChildOf(root)))
            {
                pendingRemove.Add(pair.Key);
            }
        }

        // key（键）：每轮从待处理移除取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<Collider, NavigationCollisionCache.CacheEntry>.Remove(Collider) 的参数。
foreach (var key in pendingRemove)
        {
            cache.Remove(key);
        }

        pendingRemove.Clear();
        Version++;
    }

    // 动态改名、改变祖先关系或增删导航修饰组件的入口应调用此方法。
    /// <summary>Invalidate 的业务处理入口；本方法直接依赖 Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Remove(Collider)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.Remove(Collider)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collision">碰撞。</param>
    internal static void Invalidate(Collider collision)
    {
        if (!ReferenceEquals(collision, null))
        {
            cache.Remove(collision);
        }

        Version++;
    }

    /// <summary>Read 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;RecastNavmeshModifier&gt;()、Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.TryGetValue(Collider, out NavigationCollisionCache.CacheEntry)、LayerMask.LayerToName(int)、NavigationCollisionCache.CacheEntry.CacheEntry()、NavigationCollisionCache.NameMatch(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationCollisionCache.Enemy(Collider)、NavigationCollisionCache.IsObstacle(Collider)、NavigationCollisionCache.Name(Collider)、NavigationCollisionCache.Player(Collider)；下游（含内部回调体） Component.GetComponent&lt;RecastNavmeshModifier&gt;()、Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、Dictionary&lt;Collider, NavigationCollisionCache.CacheEntry&gt;.TryGetValue(Collider, out NavigationCollisionCache.CacheEntry)、LayerMask.LayerToName(int)、NavigationCollisionCache.CacheEntry.CacheEntry()、NavigationCollisionCache.NameMatch(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collision">碰撞。</param>
    private static CacheEntry Read(Collider collision)
    {
        if (!initialized)
        {
            // 序号。
for (int sequence = 0; sequence < 32; sequence++)
            {
                layerObstacle[sequence] = NameMatch(LayerMask.LayerToName(sequence));
            }

            initialized = true;
        }

        // 位置。
Transform position = collision.transform;
        if (cache.TryGetValue(collision, out var /* 中文含义：当前缓存入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ value) && value.parentNode == position.parent && value.rootNode == position.root)
        {
            CombatPerformanceCounters.categoryHit++;
            return value;
        }

        CombatPerformanceCounters.categoryMissed++;
        // 名称值。
string nameValue = collision.name;
        value = new CacheEntry
        {
            parentNode = position.parent,
            rootNode = position.root,
            enemy = collision.GetComponentInParent<VillagerController>(),
            player = collision.GetComponentInParent<LeonController>(),
            modifier = collision.GetComponent<RecastNavmeshModifier>(),
            nameObstacle = NameMatch(nameValue)
        };
        cache[collision] = value;
        return value;
    }

    /// <summary>MarkerPoseChange 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CharacterMotionExecutor.CommitPose(MotionControlToken, Vector3, Quaternion, bool)、DoorController.SetPhysicalColliderBlocking()、DoorController.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal static void MarkerPoseChange() => Version++;
    /// <summary>IsObstacle 的业务处理入口；本方法直接依赖 NavigationCollisionCache.Read(Collider)、ProfilerMarker.Auto()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.IsNavigationObstacle(Collider)；下游（含内部回调体） NavigationCollisionCache.Read(Collider)、ProfilerMarker.Auto()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collision">碰撞。</param>
    internal static bool IsObstacle(Collider collision)
    {
        if (collision == null)
        {
            return false;
        }

        using (CombatPerformanceCounters.obstacleMarker.Auto())
        {
            // 当前缓存入口。
var value = Read(collision);
            return (value.modifier != null && value.modifier.enabled && value.modifier.mode == RecastNavmeshModifier.Mode.UnwalkableSurface) || value.nameObstacle || layerObstacle[collision.gameObject.layer];
        }
    }

    /// <summary>Enemy 的业务处理入口；本方法直接依赖 NavigationCollisionCache.Read(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.HasAttackRecoveryGroundSupport()、VillagerController.HasContinuousNavigationGround(Vector3)、VillagerController.HasContinuousSteppedNavigationGround(Vector3) 等共 14 处；下游（含内部回调体） NavigationCollisionCache.Read(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collision">碰撞。</param>
    internal static VillagerController Enemy(Collider collision)
    {
        if (collision != null)
        {
            return Read(collision).enemy;
        }
        else
        {
            return null;
        }
    }

    /// <summary>Player 的业务处理入口；本方法直接依赖 NavigationCollisionCache.Read(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.CanTraverseSupportedNavigationSegment(Vector3, bool)、VillagerController.DetectNavigationTraversal(Vector3, bool)、VillagerController.HasAttackRecoveryGroundSupport()、VillagerController.HasContinuousNavigationGround(Vector3)、VillagerController.HasContinuousSteppedNavigationGround(Vector3) 等共 12 处；下游（含内部回调体） NavigationCollisionCache.Read(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collision">碰撞。</param>
    internal static LeonController Player(Collider collision)
    {
        if (collision != null)
        {
            return Read(collision).player;
        }
        else
        {
            return null;
        }
    }

    /// <summary>NameMatch 的业务处理入口；本方法直接依赖 string.IndexOf(string, StringComparison)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 NavigationCollisionCache.Read(Collider)；下游（含内部回调体） string.IndexOf(string, StringComparison)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    private static bool NameMatch(string name) => !string.IsNullOrEmpty(name) && (name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Hedge", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Vegetation", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("植被", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("灌木", StringComparison.OrdinalIgnoreCase) >= 0);
}
