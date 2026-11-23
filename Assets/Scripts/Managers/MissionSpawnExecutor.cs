using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/// <summary>任务生成适配器，只执行已批准波次的生成，保存选点缓存与预制件轮询位置。</summary>
/// <remarks>章节生成执行边界，按已绑定模板、生成点和通路执行任务宿主的生成计划，并将实际生成事实交回既有登记链。</remarks>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "任务生成执行器")]
public sealed class MissionSpawnExecutor
{
    /// <summary>中文含义：配置。只读字段，初始化后不由普通写入口替换。使用位置：作为 Debug.LogError(object, Object) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Random.Range(int, int) 的参数 等共 8 处。</summary>
    private readonly ChapterOneMissionManager configuration;
    /// <summary>中文含义：跟踪的敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</summary>
    private readonly Dictionary<VillagerController, MissionEnemyType> trackedEnemies;
    /// <summary>中文含义：玩家。只读字段，初始化后不由普通写入口替换。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 等共 4 处。</summary>
    private readonly LeonController player;
    /// <summary>中文含义：通行。只读字段，初始化后不由普通写入口替换。使用位置：MissionSpawnExecutor.Generate(GameObject)、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</summary>
    private readonly HouseThreeTraversalGeometry traversal;
    /// <summary>中文含义：精确通行。只读字段，初始化后不由普通写入口替换。使用位置：作为 PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[]) 的参数、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</summary>
    private readonly EnemyNavigationPreciseLink[] preciseTraversal;
    /// <summary>中文含义：已报告无效生成点集合。只读字段，初始化后不由普通写入口替换。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</summary>
    private readonly HashSet<Transform> reportedInvalidSpawnPoints = new HashSet<Transform>();
    /// <summary>中文含义：是否已尝试启动导航扫描。使用位置：MissionSpawnExecutor.EnsureNavigationGraphReady()。</summary>
    private bool attemptedStartupNavigationScan;
    /// <summary>中文含义：下一次村民敌人预制件索引。使用位置：MissionSpawnExecutor.GetNextGanadoPrefab()。</summary>
    private int nextGanadoPrefabIndex;
    private int nextReinforcementSite;
    private Transform entranceReserve;
    private readonly HitQueryBuffer visibilityQueries = new HitQueryBuffer();
    /// <summary>构造并初始化 MissionSpawnExecutor 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="configuration">中文含义：配置。使用位置：MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</param>
    /// <param name="playerValue">中文含义：玩家值。使用位置：MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</param>
    /// <param name="traversal">中文含义：通行。使用位置：MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</param>
    /// <param name="preciseTraversal">中文含义：精确通行。使用位置：MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</param>
    /// <param name="register">中文含义：登记。使用位置：MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。</param>
    public MissionSpawnExecutor(ChapterOneMissionManager configuration, LeonController playerValue, HouseThreeTraversalGeometry traversal, EnemyNavigationPreciseLink[] preciseTraversal, Dictionary<VillagerController, MissionEnemyType> register)
    {
        trackedEnemies = register;
        this.configuration = configuration;
        player = playerValue;
        this.traversal = traversal;
        this.preciseTraversal = preciseTraversal;
    }

    /// <summary>中文含义：生成偏移量集合。只读字段，初始化后不由普通写入口替换。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</summary>
    private static readonly Vector3[] SpawnOffsets =
    {
        Vector3.zero,
        new Vector3(2.4f, 0f, 0f),
        new Vector3(-2.4f, 0f, 0f),
        new Vector3(0f, 0f, 2.4f),
        new Vector3(0f, 0f, -2.4f)
    };
    /// <summary>GetNextGanadoPrefab 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.GetNextGanadoPrefab()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public GameObject GetNextGanadoPrefab()
    {
        if (configuration.ganadoPrefabs == null || configuration.ganadoPrefabs.Length == 0)
        {
            return null;
        }

        // offset（偏移量）：使用位置：MissionSpawnExecutor.GetNextGanadoPrefab()。
for (int offset = 0; offset < configuration.ganadoPrefabs.Length; offset++)
        {
            // index（当前元素索引）：使用位置：MissionSpawnExecutor.GetNextGanadoPrefab()。
int index = (nextGanadoPrefabIndex + offset) % configuration.ganadoPrefabs.Length;
            // prefab（预制件）：使用位置：MissionSpawnExecutor.GetNextGanadoPrefab()。
GameObject prefab = configuration.ganadoPrefabs[index];
            if (prefab == null)
            {
                continue;
            }

            nextGanadoPrefabIndex = (index + 1) % configuration.ganadoPrefabs.Length;
            return prefab;
        }

        return null;
    }

    /// <summary>TryGetValidSpawn 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Debug.LogWarning(object, Object)、HashSet&lt;Transform&gt;.Add(Transform)、Mathf.Abs(float)、MissionSpawnExecutor.EnsureNavigationGraphReady()、MissionSpawnExecutor.HasWalkableNavigationNodes()、MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.Generate(GameObject)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Debug.LogWarning(object, Object)、HashSet&lt;Transform&gt;.Add(Transform)、Mathf.Abs(float)、MissionSpawnExecutor.EnsureNavigationGraphReady()、MissionSpawnExecutor.HasWalkableNavigationNodes()、MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">中文含义：预制件。使用位置：作为 MissionSpawnExecutor.IsSpawnPhysicallyClear(Vector3, Quaternion, GameObject) 的参数。</param>
    /// <param name="position">中文含义：位置。输出参数，由本入口写入并交给调用方。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</param>
    /// <param name="rotation">中文含义：旋转。输出参数，由本入口写入并交给调用方。使用位置：作为 MissionSpawnExecutor.IsSpawnPhysicallyClear(Vector3, Quaternion, GameObject) 的参数、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</param>
    public bool TryGetValidSpawn(GameObject prefab, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
        if (configuration.spawnPoints == null || configuration.spawnPoints.Length == 0)
        {
            return false;
        }

        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return false;
        }

        GraphNode playerNode = null;
        if (player != null)
        {
            playerNode = AstarPath.active.GetNearest(player.transform.position, NearestNodeConstraint.Walkable).node;
        }

        if (playerNode == null && !HasWalkableNavigationNodes())
        {
            EnsureNavigationGraphReady();
            return false;
        }

        var targetRegion = VillageTraversalRegion.HouseOuterGround;
        var regions = HouseThreeRegionNavigation.Current;
        if (regions != null && player != null)
        {
            targetRegion = VillageLadderSite.ReadTargetRegion(regions, player.transform.position);
        }
        bool canUseLadder = prefab != null && prefab.GetComponent<ChainsawManAnimationBridge>() == null;

        // firstIndex（首个索引）：保存 Random.Range(int, int) 的返回结果，供当前步骤使用。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。
int firstIndex = UnityEngine.Random.Range(0, configuration.spawnPoints.Length);
        // offset（偏移量）：使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。
for (int offset = 0; offset < configuration.spawnPoints.Length; offset++)
        {
            // point（点）：使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 HashSet<Transform>.Add(Transform) 的参数、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。
Transform point = configuration.spawnPoints[(firstIndex + offset) % configuration.spawnPoints.Length];
            if (point == null)
            {
                continue;
            }

            // candidateIndex（候选索引）：使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。
for (int candidateIndex = 0; candidateIndex < SpawnOffsets.Length; candidateIndex++)
            {
                // candidate（候选）：使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 Mathf.Abs(float) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数。
Vector3 candidate = point.position + point.rotation * SpawnOffsets[candidateIndex];
                // nearest（最近）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、作为 MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3) 的参数、作为 MissionSpawnExecutor.IsSpawnOccupied(Vector3) 的参数 等共 7 处。
NNInfo nearest = AstarPath.active.GetNearest(candidate, NearestNodeConstraint.Walkable);
                bool closeEnough = nearest.node != null
                    && Vector3.Distance(candidate, nearest.position) <= configuration.spawnSnapDistance
                    && Mathf.Abs(candidate.y - ((Vector3)nearest.position).y) <= configuration.spawnSnapHeight;
                bool farEnoughFromPlayer = player == null || Vector3.Distance(nearest.position, player.transform.position) >= configuration.minimumPlayerSpawnDistance;
                if (!closeEnough || !farEnoughFromPlayer
                    || (playerNode != null && !PathUtilities.IsPathPossible(nearest.node, playerNode)
                        && (!canUseLadder || !VillageLadderSite.IsSpawnConnectedToLadder(nearest.node, targetRegion)))
                    || IsInsideHaystackSpawnExclusion(nearest.position)
                    || IsSpawnOccupied(nearest.position)
                    || !IsSpawnPhysicallyClear(nearest.position, point.rotation, prefab))
                {
                    continue;
                }

                position = nearest.position;
                rotation = point.rotation;
                return true;
            }

            if (reportedInvalidSpawnPoints.Add(point))
            {
                Debug.LogWarning($"[第一章任务] 刷新点“{point.name}”及其偏移位当前不可用。", point);
            }
        }

        return false;
    }

    /// <summary>IsInsideHaystackSpawnExclusion 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3)。</param>
    bool IsInsideHaystackSpawnExclusion(Vector3 position)
    {
        // offset（偏移量）：使用位置：MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3)。
Vector3 offset = position - configuration.haystackSpawnExclusionCenter;
        offset.y = 0f;
        // radius（半径）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3)。
float radius = Mathf.Max(0.1f, configuration.haystackSpawnExclusionRadius);
        return offset.sqrMagnitude < radius * radius;
    }

    /// <summary>EnsureNavigationGraphReady 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、MissionSpawnExecutor.HasWalkableNavigationNodes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)；下游（含内部回调体） Debug.LogError(object, Object)、MissionSpawnExecutor.HasWalkableNavigationNodes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EnsureNavigationGraphReady()
    {
        if (attemptedStartupNavigationScan)
        {
            return;
        }

        if (AstarPath.active == null || AstarPath.active.graphs == null || AstarPath.active.graphs.Length == 0)
        {
            return;
        }

        if (HasWalkableNavigationNodes())
        {
            return;
        }

        attemptedStartupNavigationScan = true;
        Debug.LogError("[第一章任务] 严格敌人寻路图没有可用节点，已阻止默认扫描覆盖。请先执行严格烘焙并确认候选图。", configuration);
    }

    /// <summary>HasWalkableNavigationNodes 的业务处理入口；本方法直接依赖 NavGraph.GetNodes(Action&lt;GraphNode&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.HasWalkableNavigationNodes()、MissionSpawnExecutor.EnsureNavigationGraphReady()、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)；下游（含内部回调体） NavGraph.GetNodes(Action&lt;GraphNode&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HasWalkableNavigationNodes()
    {
        if (AstarPath.active == null || AstarPath.active.data == null || AstarPath.active.data.graphs == null)
        {
            return false;
        }

        // hasNode（是否具有节点）：使用位置：作为 NavGraph.GetNodes(Action<GraphNode>) 的参数、MissionSpawnExecutor.HasWalkableNavigationNodes()。
bool hasNode = false;
        // graph（导航图）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：MissionSpawnExecutor.HasWalkableNavigationNodes()。
foreach (NavGraph graph in AstarPath.active.data.graphs)
        {
            if (graph == null)
            {
                continue;
            }

            graph.GetNodes( /* 中文含义：节点。 */node =>
            {
                if (node != null && node.Walkable)
                {
                    hasNode = true;
                }
            });
            if (hasNode)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>IsSpawnOccupied 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)。</param>
    bool IsSpawnOccupied(Vector3 position)
    {
        // radiusSqr（半径平方）：使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)。
float radiusSqr = configuration.occupiedSpawnRadius * configuration.occupiedSpawnRadius;
        // enemy（敌人）：每轮从跟踪的敌人集合的键集合取得一个元素，仅代表当前这一项。使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)。
foreach (VillagerController enemy in trackedEnemies.Keys)
        {
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }

            // offset（偏移量）：使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)。
Vector3 offset = enemy.transform.position - position;
            offset.y = 0f;
            if (offset.sqrMagnitude < radiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>IsSpawnPhysicallyClear 的业务处理入口；本方法直接依赖 MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)、Physics.CheckCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)；下游（含内部回调体） MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)、Physics.CheckCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float) 的参数。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：作为 MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float) 的参数。</param>
    /// <param name="prefab">中文含义：预制件。使用位置：作为 MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float) 的参数。</param>
    bool IsSpawnPhysicallyClear(Vector3 position, Quaternion rotation, GameObject prefab)
    {
        GetSpawnCapsule(position, rotation, prefab, out Vector3 /* 中文含义：顶部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ top, out Vector3 /* 中文含义：底部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bottom, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius);
        return !Physics.CheckCapsule(top, bottom, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>GetSpawnCapsule 的业务处理入口；本方法直接依赖 GameObject.GetComponentInChildren&lt;CapsuleCollider&gt;(bool)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Scale(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 MissionSpawnExecutor.IsSpawnPhysicallyClear(Vector3, Quaternion, GameObject)；下游（含内部回调体） GameObject.GetComponentInChildren&lt;CapsuleCollider&gt;(bool)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Scale(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    /// <param name="rotation">中文含义：旋转。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    /// <param name="prefab">中文含义：预制件。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    /// <param name="top">中文含义：顶部。输出参数，由本入口写入并交给调用方。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    /// <param name="bottom">中文含义：底部。输出参数，由本入口写入并交给调用方。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    /// <param name="radius">中文含义：半径。输出参数，由本入口写入并交给调用方。使用位置：作为 Mathf.Max(float, float) 的参数、MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。</param>
    void GetSpawnCapsule(Vector3 position, Quaternion rotation, GameObject prefab, out Vector3 top, out Vector3 bottom, out float radius)
    {
        radius = Mathf.Max(0.1f, configuration.defaultSpawnCapsuleRadius);
        // height（高度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
float height = Mathf.Max(radius * 2f, configuration.defaultSpawnCapsuleHeight);
        // center（中心）：使用位置：作为 Mathf.Max(float, float) 的参数、MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
Vector3 center = position;
        // capsule（胶囊）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、作为 Transform.TransformPoint(Vector3) 的参数 等共 4 处。
CapsuleCollider capsule;
        if (prefab == null)
        {
            capsule = null;
        }
        else
        {
            capsule = prefab.GetComponentInChildren<CapsuleCollider>(true);
        }

        if (capsule != null)
        {
            // scale（缩放）：使用位置：作为 Mathf.Abs(float) 的参数。
Vector3 scale = capsule.transform.lossyScale;
            // horizontalScale（水平缩放）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            radius = Mathf.Max(0.1f, capsule.radius * horizontalScale);
            height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
            // prefabRoot（预制件根节点）：使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
Transform prefabRoot = prefab.transform;
            // localCenter（局部中心）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Scale(Vector3, Vector3) 的参数。
Vector3 localCenter = prefabRoot.InverseTransformPoint(capsule.transform.TransformPoint(capsule.center));
            // rootScale（根节点缩放）：使用位置：作为 Vector3.Scale(Vector3, Vector3) 的参数。
Vector3 rootScale = prefabRoot.lossyScale;
            // scaledCenter（缩放后的中心）：保存 Vector3.Scale(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
Vector3 scaledCenter = Vector3.Scale(localCenter, rootScale);
            center += rotation * scaledCenter;
        }

        // skin（皮肤）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
float skin = Mathf.Max(0.005f, configuration.spawnClearanceSkin);
        // halfSegment（一半线段）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：MissionSpawnExecutor.GetSpawnCapsule(Vector3, Quaternion, GameObject, out Vector3, out Vector3, out float)。
float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        // minimumCenterY（最小中心纵向）：使用位置：作为 Mathf.Max(float, float) 的参数。
float minimumCenterY = position.y + radius + skin + halfSegment;
        center.y = Mathf.Max(center.y, minimumCenterY);
        top = center + Vector3.up * halfSegment;
        bottom = center - Vector3.up * halfSegment;
    }

    /// <summary>Generate 的业务处理入口；本方法直接依赖 Component.TryGetComponent&lt;PathAgent&gt;(out PathAgent)、Debug.LogError(object, Object)、GameObject.GetComponent&lt;VillagerController&gt;()、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)、Object.Destroy(Object)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)；下游（含内部回调体） Component.TryGetComponent&lt;PathAgent&gt;(out PathAgent)、Debug.LogError(object, Object)、GameObject.GetComponent&lt;VillagerController&gt;()、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)、Object.Destroy(Object)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、PathAgent.BindingPreciseTraversal(EnemyNavigationPreciseLink[])。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">中文含义：预制件。使用位置：作为 MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion) 的参数、MissionSpawnExecutor.Generate(GameObject)。</param>
    internal VillagerController GeneratePatrol(GameObject prefab, Vector3 anchor)
    {
        foreach (Vector3 offset in SpawnOffsets)
        {
            if (TryResolveVillagePosition(prefab, anchor + offset, out Vector3 position, out Quaternion rotation))
            {
                return CreateEnemy(prefab, position, rotation);
            }
        }

        return null;
    }

    internal VillagerController GenerateReinforcement(GameObject prefab)
    {
        Camera camera = player.playerCamera;
        if (camera == null || !camera.isActiveAndEnabled)
        {
            return null;
        }

        var sites = configuration.waveConfiguration.VillageReinforcementSites;
        for (int index = 0; index < sites.Length; index++)
        {
            int siteIndex = (nextReinforcementSite + index) % sites.Length;
            var site = sites[siteIndex];
            if (site.excludedPlayerArea.Contains(player.transform.position) ||
                !TryResolveVillagePosition(prefab, site.position, out Vector3 position, out Quaternion rotation) ||
                IsVisibleFromCamera(camera, position, prefab))
            {
                continue;
            }

            nextReinforcementSite = (siteIndex + 1) % sites.Length;
            return CreateEnemy(prefab, position, rotation);
        }

        return null;
    }

    private bool TryResolveVillagePosition(GameObject prefab, Vector3 anchor, out Vector3 position, out Quaternion rotation)
    {
        position = anchor;
        rotation = Quaternion.identity;
        if (prefab == null || AstarPath.active == null)
        {
            return false;
        }

        NNInfo nearest = AstarPath.active.GetNearest(anchor, NearestNodeConstraint.Walkable);
        if (nearest.node == null || Vector3.Distance(anchor, nearest.position) > configuration.spawnSnapDistance ||
            Mathf.Abs(anchor.y - ((Vector3)nearest.position).y) > configuration.spawnSnapHeight)
        {
            return false;
        }

        NNInfo village = AstarPath.active.GetNearest(configuration.waveConfiguration.VillagePatrolPositions[0],
            NearestNodeConstraint.Walkable);
        if (village.node == null || !PathUtilities.IsPathPossible(nearest.node, village.node))
        {
            return false;
        }

        position = nearest.position;
        Vector3 towardSquare = new Vector3(-5f, 0f, -3f) - position;
        towardSquare.y = 0f;
        if (towardSquare.sqrMagnitude > .01f)
        {
            rotation = Quaternion.LookRotation(towardSquare);
        }

        float minimumDistance = configuration.minimumPlayerSpawnDistance;
        return (player.transform.position - position).sqrMagnitude >= minimumDistance * minimumDistance &&
            !IsInsideHaystackSpawnExclusion(position) && !IsSpawnOccupied(position) &&
            IsSpawnPhysicallyClear(position, rotation, prefab);
    }

    internal void PrepareEntranceActors(GameObject prefab)
    {
        if (prefab == null || entranceReserve != null)
        {
            return;
        }
        // 同局最多有两次电锯登场。禁用父对象可在装载时复制骨架，正式登场才启动角色。
        var holder = new GameObject("村庄登场角色预备");
        holder.SetActive(false);
        holder.transform.SetParent(configuration.transform, false);
        entranceReserve = holder.transform;
        for (int index = 0; index < 2; index++)
        {
            UnityEngine.Object.Instantiate(prefab, entranceReserve, false);
        }
    }

    internal VillagerController GenerateEntrance(GameObject prefab, Transform anchor, float health)
    {
        if (prefab == null || anchor == null || AstarPath.active == null || player == null)
        {
            return null;
        }

        NNInfo village = AstarPath.active.GetNearest(configuration.waveConfiguration.VillagePatrolPositions[0],
            NearestNodeConstraint.Walkable);
        if (village.node == null)
        {
            return null;
        }

        foreach (Vector3 offset in SpawnOffsets)
        {
            Vector3 desired = anchor.position + anchor.rotation * offset;
            NNInfo nearest = AstarPath.active.GetNearest(desired, NearestNodeConstraint.Walkable);
            if (nearest.node == null || !PathUtilities.IsPathPossible(nearest.node, village.node))
            {
                continue;
            }

            Vector3 position = nearest.position;
            Vector3 separation = position - player.transform.position;
            if (Vector3.Distance(position, desired) > configuration.spawnSnapDistance ||
                Mathf.Abs(position.y - desired.y) > configuration.spawnSnapHeight || separation.sqrMagnitude < 2.25f ||
                IsSpawnOccupied(position) || !IsSpawnPhysicallyClear(position, anchor.rotation, prefab))
            {
                continue;
            }

            GameObject prepared = null;
            if (prefab == configuration.sawmanTemplate && entranceReserve != null && entranceReserve.childCount > 0)
            {
                Transform actor = entranceReserve.GetChild(0);
                actor.SetPositionAndRotation(position, anchor.rotation);
                actor.SetParent(null, true);
                prepared = actor.gameObject;
            }
            VillagerController enemy = CreateEnemy(prefab, position, anchor.rotation, prepared);
            if (enemy != null)
            {
                // 生命模块在角色首次 Start 时读取此值。
                enemy.maxHealth = health;
            }

            return enemy;
        }

        return null;
    }

    private bool IsVisibleFromCamera(Camera camera, Vector3 position, GameObject prefab)
    {
        GetSpawnCapsule(position, Quaternion.identity, prefab, out Vector3 top, out Vector3 bottom, out float radius);
        Vector3 center = (top + bottom) * .5f;
        Vector3 side = camera.transform.right * radius;
        return IsVisibleSample(camera, top + Vector3.up * radius) ||
            IsVisibleSample(camera, bottom - Vector3.up * radius * .5f) ||
            IsVisibleSample(camera, center) || IsVisibleSample(camera, center + side) ||
            IsVisibleSample(camera, center - side) || IsVisibleSample(camera, top + side) ||
            IsVisibleSample(camera, top - side);
    }

    private bool IsVisibleSample(Camera camera, Vector3 sample)
    {
        Vector3 viewport = camera.WorldToViewportPoint(sample);
        const float viewportMargin = .08f;
        if (viewport.z <= 0f || viewport.x < -viewportMargin || viewport.x > 1f + viewportMargin ||
            viewport.y < -viewportMargin || viewport.y > 1f + viewportMargin)
        {
            return false;
        }

        Vector3 origin = camera.transform.position;
        Vector3 offset = sample - origin;
        float length = offset.magnitude;
        RaycastHit[] hits = visibilityQueries.Raycast(origin, offset.normalized, length, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore, out int count);
        for (int index = 0; index < count; index++)
        {
            Collider blocker = hits[index].collider;
            if (blocker != null && blocker.GetComponentInParent<LeonController>() == null &&
                blocker.GetComponentInParent<VillagerController>() == null && hits[index].distance < length - .05f)
            {
                return false;
            }
        }

        return true;
    }

    public VillagerController Generate(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[第一章任务] 敌人预制件为空，本次刷新已延后。", configuration);
            return null;
        }

        if (!TryGetValidSpawn(prefab, out Vector3 /* 中文含义：位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ position, out Quaternion /* 中文含义：旋转。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ rotation))
        {
            return null;
        }

        return CreateEnemy(prefab, position, rotation);
    }

    private VillagerController CreateEnemy(GameObject prefab, Vector3 position, Quaternion rotation, GameObject instance = null)
    {
        if (instance == null)
        {
            instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
        var enemy = instance.GetComponent<VillagerController>();
        if (enemy == null)
        {
            Debug.LogError("[第一章任务] 敌人预制件缺少村民控制器，本次刷新不计入储备。", instance);
            UnityEngine.Object.Destroy(instance);
        }
        else
        {
            enemy.door03TraversalRoute = traversal;
            if (enemy.TryGetComponent<PathAgent>(out var /* 中文含义：寻路。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pathfinding))
            {
                pathfinding.BindingPreciseTraversal(preciseTraversal);
            }
        }

        return enemy;
    }
}
