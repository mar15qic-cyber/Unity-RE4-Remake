using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// 从现有寻路图和全部门洞自动构建追击区域拓扑。
/// 构建时临时把门洞附近节点视为区域切口，再对剩余节点做连通分区。
/// 因此房间归属来自真实寻路连通关系，而不是门法线的无限平面或
/// 敌人与目标之间恰好穿过门洞的直线。
/// </summary>
public static class PursuitNavigationTopology
{
    /// <summary>敌人行为、动作与通行模块 PortalLink；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class PortalLink
    {
        /// <summary>门。</summary>
        public DoorController door;
        /// <summary>正向区域。</summary>
        public int positiveRegion;
        /// <summary>负值区域。</summary>
        public int negativeRegion;
        /// <summary>GetOtherRegion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
        /// <remarks>静态调用链：上游 PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="region">区域。</param>
        public int GetOtherRegion(int region)
        {
            if (region == positiveRegion)
            {
                return negativeRegion;
            }

            if (region == negativeRegion)
            {
                return positiveRegion;
            }

            return -1;
        }

        /// <summary>GetSideForRegion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
        /// <remarks>静态调用链：上游 PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="region">区域。</param>
        public int GetSideForRegion(int region)
        {
            if (region == positiveRegion)
            {
                return 1;
            }

            if (region == negativeRegion)
            {
                return -1;
            }

            return 0;
        }
    }

    /// <summary>节点集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<GraphNode> nodes = new();
    /// <summary>门集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<DoorController> doors = new();
    /// <summary>通行口节点集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly HashSet<GraphNode> gateNodes = new();
    /// <summary>通行口节点拥有者集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<GraphNode, DoorController> gateNodeOwners = new();
    /// <summary>节点区域集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<GraphNode, int> nodeRegions = new();
    /// <summary>区域连接集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<int, List<PortalLink>> regionLinks = new();
    /// <summary>门连接集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<DoorController, PortalLink> doorLinks = new();
    /// <summary>节点队列。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Queue<GraphNode> nodeQueue = new();
    /// <summary>区域队列。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Queue<int> regionQueue = new();
    /// <summary>已访问区域集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly HashSet<int> visitedRegions = new();
    /// <summary>前一次区域集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<int, int> previousRegions = new();
    /// <summary>前一次连接集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<int, PortalLink> previousLinks = new();
    /// <summary>区域支撑查询集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly HitQueryBuffer regionSupportQueries = new();
    /// <summary>已构建星形寻路。</summary>
    private static AstarPath builtAstar;
    /// <summary>拓扑是否就绪。</summary>
    private static bool topologyReady;
    /// <summary>是否建筑拓扑。</summary>
    private static bool buildingTopology;
    /// <summary>区域数量。</summary>
    private static int regionCount;
    /// <summary>中文含义：是否就绪。</summary>
    public static bool IsReady
    {
        get
        {
            EnsureBuilt();
            return topologyReady;
        }
    }

    /// <summary>中文含义：区域数量。</summary>
    public static int RegionCount
    {
        get
        {
            EnsureBuilt();
            return regionCount;
        }
    }

    /// <summary>Invalidate 的业务处理入口；本方法直接依赖 Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.Clear()、Dictionary&lt;GraphNode, DoorController&gt;.Clear()、Dictionary&lt;GraphNode, int&gt;.Clear()、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.Clear()、HashSet&lt;GraphNode&gt;.Clear()、List&lt;DoorController&gt;.Clear()、List&lt;GraphNode&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.OnDisable()、DoorController.OnEnable()、PursuitNavigationTopology.Build(AstarPath)；下游（含内部回调体） Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.Clear()、Dictionary&lt;GraphNode, DoorController&gt;.Clear()、Dictionary&lt;GraphNode, int&gt;.Clear()、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.Clear()、HashSet&lt;GraphNode&gt;.Clear()、List&lt;DoorController&gt;.Clear()、List&lt;GraphNode&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void Invalidate()
    {
        topologyReady = false;
        builtAstar = null;
        regionCount = 0;
        nodes.Clear();
        doors.Clear();
        gateNodes.Clear();
        gateNodeOwners.Clear();
        nodeRegions.Clear();
        regionLinks.Clear();
        doorLinks.Clear();
    }

    /// <summary>TryGetRegion 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.TryGetValue(DoorController, out PursuitNavigationTopology.PortalLink)、Dictionary&lt;GraphNode, DoorController&gt;.TryGetValue(GraphNode, out DoorController)、Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)、EnemyAIManager.UpdatePlayerPursuitRegion()、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)、VillagerController.TryResolveCurrentPursuitRegions(Vector3, out int, out int)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.TryGetValue(DoorController, out PursuitNavigationTopology.PortalLink)、Dictionary&lt;GraphNode, DoorController&gt;.TryGetValue(GraphNode, out DoorController)、Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="region">区域。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryGetRegion(Vector3 point, out int region)
    {
        region = -1;
        if (!EnsureBuilt() || AstarPath.active == null)
        {
            return false;
        }

        // 最近。
NNInfo nearest = AstarPath.active.GetNearest(point, NearestNodeConstraint.Walkable);
        if (nearest.node == null)
        {
            return false;
        }

        if (!nodeRegions.TryGetValue(nearest.node, out region) && gateNodeOwners.TryGetValue(nearest.node, out DoorController /* 中文含义：通行口门。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gateDoor) && doorLinks.TryGetValue(gateDoor, out PortalLink /* 中文含义：通行口连接。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gateLink))
        {
            // 侧边。
float side = Vector3.Dot(point - gateDoor.PassageCenter, gateDoor.PassageNormal);
            if (Mathf.Abs(side) > 0.03f)
            {
                if (side >= 0f)
                {
                    region = gateLink.positiveRegion;
                }
                else
                {
                    region = gateLink.negativeRegion;
                }
            }
        }

        if (region < 0 && !TryFindNearestRegion(point, out region))
        {
            return false;
        }

        // 最近节点只是位置投影，不是门两侧归属的最终事实。薄墙、门槛
        // 或门外狭窄地面会让室外点的最近节点落到门内，二号门正是因此
        // 长期不触发出屋刷新。仅在当前区域直接连接的有限门洞附近，按
        // 门平面真实侧别纠正；不会把远处无关建筑的无限平面用于分区。
        if (TryCorrectRegionByNearbyPortal(point, region, out int /* 中文含义：校正后的区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ correctedRegion))
        {
            region = correctedRegion;
        }

        return region >= 0;
    }

    /// <summary>TryResolveRegions 的业务处理入口；本方法直接依赖 PursuitNavigationTopology.TryGetRegion(Vector3, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)；下游（含内部回调体） PursuitNavigationTopology.TryGetRegion(Vector3, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="sourceRegion">来源区域。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="destinationRegion">目的地区域。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryResolveRegions(Vector3 source, Vector3 destination, out int sourceRegion, out int destinationRegion)
    {
        sourceRegion = -1;
        destinationRegion = -1;
        return TryGetRegion(source, out sourceRegion) && TryGetRegion(destination, out destinationRegion);
    }

    /// <summary>TryFindFirstPortal 的业务处理入口；本方法直接依赖 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)；下游（含内部回调体） PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="passageDoor">通道门。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="sourceSide">来源侧边。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindFirstPortal(Vector3 source, Vector3 destination, out DoorController passageDoor, out int sourceSide)
    {
        passageDoor = null;
        sourceSide = 0;
        if (!TryResolveRegions(source, destination, out int /* 中文含义：来源区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceRegion, out int /* 中文含义：目的地区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ destinationRegion))
        {
            return false;
        }

        return TryFindFirstPortal(sourceRegion, destinationRegion, out passageDoor, out sourceSide);
    }

    /// <summary>TryFindFirstPortal 的业务处理入口；本方法直接依赖 Dictionary&lt;int, int&gt;.Clear()、Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、Dictionary&lt;int, PursuitNavigationTopology.PortalLink&gt;.Clear()、HashSet&lt;int&gt;.Add(int)、HashSet&lt;int&gt;.Clear()、PursuitNavigationTopology.EnsureBuilt() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） Dictionary&lt;int, int&gt;.Clear()、Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、Dictionary&lt;int, PursuitNavigationTopology.PortalLink&gt;.Clear()、HashSet&lt;int&gt;.Add(int)、HashSet&lt;int&gt;.Clear()、PursuitNavigationTopology.EnsureBuilt() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceRegion">来源区域。</param>
    /// <param name="destinationRegion">目的地区域。</param>
    /// <param name="passageDoor">通道门。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="sourceSide">来源侧边。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindFirstPortal(int sourceRegion, int destinationRegion, out DoorController passageDoor, out int sourceSide)
    {
        passageDoor = null;
        sourceSide = 0;
        if (!EnsureBuilt() || sourceRegion < 0 || destinationRegion < 0 || sourceRegion == destinationRegion)
        {
            return false;
        }

        regionQueue.Clear();
        visitedRegions.Clear();
        previousRegions.Clear();
        previousLinks.Clear();
        regionQueue.Enqueue(sourceRegion);
        visitedRegions.Add(sourceRegion);
        while (regionQueue.Count > 0)
        {
            // 当前区域。
int currentRegion = regionQueue.Dequeue();
            if (!regionLinks.TryGetValue(currentRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
            {
                continue;
            }

            // 当前元素索引。
for (int index = 0; index < links.Count; index++)
            {
                // 连接。
PortalLink link = links[index];
                if (link == null || link.door == null || !link.door.isActiveAndEnabled)
                {
                    continue;
                }

                // 下一次区域。
int nextRegion = link.GetOtherRegion(currentRegion);
                if (nextRegion < 0 || !visitedRegions.Add(nextRegion))
                {
                    continue;
                }

                previousRegions[nextRegion] = currentRegion;
                previousLinks[nextRegion] = link;
                if (nextRegion == destinationRegion)
                {
                    // 路线区域。
int routeRegion = destinationRegion;
                    // 首个连接。
PortalLink firstLink = link;
                    while (previousRegions.TryGetValue(routeRegion, out int /* 中文含义：前一次区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ previousRegion) && previousRegion != sourceRegion)
                    {
                        firstLink = previousLinks[previousRegion];
                        routeRegion = previousRegion;
                    }

                    passageDoor = firstLink.door;
                    sourceSide = firstLink.GetSideForRegion(sourceRegion);
                    return passageDoor != null && sourceSide != 0;
                }

                regionQueue.Enqueue(nextRegion);
            }
        }

        return false;
    }

    /// <summary>AreRegionsDirectlyConnected 的业务处理入口；本方法直接依赖 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.PortalLink.GetOtherRegion(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.UpdatePlayerPursuitRegion()；下游（含内部回调体） Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.PortalLink.GetOtherRegion(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="firstRegion">首个区域。</param>
    /// <param name="secondRegion">第二区域。</param>
    public static bool AreRegionsDirectlyConnected(int firstRegion, int secondRegion)
    {
        if (!EnsureBuilt() || firstRegion < 0 || secondRegion < 0 || !regionLinks.TryGetValue(firstRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // 当前元素索引。
for (int index = 0; index < links.Count; index++)
        {
            // 连接。
PortalLink link = links[index];
            if (link != null && link.door != null && link.GetOtherRegion(firstRegion) == secondRegion)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 确认玩家已在门洞以外落到新区域，允许跳落或翻窗更新区域。
    /// 门附近、悬空、跨墙投影和跨层投影不能作为绕过穿门判定的证据。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.UpdatePlayerPursuitRegion()；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)、Mathf.Abs(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="region">区域。</param>
    public static bool IsSupportedRegionAwayFromPortals(Vector3 point, int region)
    {
        if (!EnsureBuilt() || AstarPath.active == null || region < 0)
        {
            return false;
        }

        if (regionLinks.TryGetValue(region, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            // link（连接）：每轮从连接集合取得一个元素，仅代表当前这一项。使用位置：PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
foreach (PortalLink link in links)
            {
                // 判空前缓存的连接。
global::PursuitNavigationTopology.PortalLink receiverValue2 = link;
                // 门。
DoorController door;
                if (!global::System.Object.ReferenceEquals(receiverValue2, null))
                {
                    door = receiverValue2.door;
                }
                else
                {
                    door = default;
                }

                if (door == null || !door.isActiveAndEnabled)
                {
                    continue;
                }

                // 偏移量。
Vector3 offset = point - door.PassageCenter;
                if (Mathf.Abs(offset.y) > Mathf.Max(1.5f, door.TopologyVerticalTolerance))
                {
                    continue;
                }

                offset.y = 0f;
                if (Mathf.Abs(Vector3.Dot(offset, door.PassageNormal)) <= Mathf.Max(0.55f, door.TopologyCutDepth) + 0.35f && Mathf.Abs(Vector3.Dot(offset, door.PassageTangent)) <= door.PassageHalfWidth + Mathf.Max(0.25f, door.TopologyCutPadding) + 0.35f)
                {
                    return false;
                }
            }
        }

        // 最近。
NNInfo nearest = AstarPath.active.GetNearest(point, NearestNodeConstraint.Walkable);
        if (nearest.node == null || !nodeRegions.TryGetValue(nearest.node, out int /* 中文含义：节点区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ nodeRegion) || nodeRegion != region)
        {
            return false;
        }

        // 投影后的。
Vector3 projected = nearest.position;
        // 水平。
Vector3 horizontal = projected - point;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude > 0.75f * 0.75f || Mathf.Abs(projected.y - point.y) > 0.35f)
        {
            return false;
        }

        // 命中结果。
RaycastHit[] hits = regionSupportQueries.Raycast(point + Vector3.up * 0.4f, Vector3.down, 0.75f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out int /* 中文含义：命中数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hitCount);
        // 是否支持的。
bool supported = false;
        // 当前元素索引。
for (int index = 0; index < hitCount; index++)
        {
            // 命中。
RaycastHit hit = hits[index];
            if (IsActorCollider(hit.collider))
            {
                continue;
            }

            if (hit.normal.y >= 0.45f && Mathf.Abs(hit.point.y - point.y) <= 0.3f)
            {
                supported = true;
            }
        }

        if (!supported)
        {
            return false;
        }

        // 水平距离。
float horizontalDistance = horizontal.magnitude;
        if (horizontalDistance <= 0.01f)
        {
            return true;
        }

        hits = regionSupportQueries.Raycast(point + Vector3.up * 0.65f, horizontal / horizontalDistance, horizontalDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out hitCount);
        // 当前元素索引。
for (int index = 0; index < hitCount; index++)
        {
            if (!IsActorCollider(hits[index].collider))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>IsActorCollider 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">碰撞体。</param>
    private static bool IsActorCollider(Collider collider)
    {
        return collider == null || collider.GetComponentInParent<LeonController>() != null || collider.GetComponentInParent<VillagerController>() != null;
    }

    /// <summary>
    /// 按玩家相邻两帧的真实位移检测有限门洞穿越。区域切换不能只依赖
    /// 最近节点，因为薄墙另一侧的节点可能更近；穿过门洞本身才是两侧
    /// 区域转换的权威证据。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.UpdatePlayerPursuitRegion()；下游（含内部回调体） Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="sourceRegion">来源区域。</param>
    /// <param name="destinationRegion">目的地区域。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindCrossedPortal(Vector3 source, Vector3 destination, int sourceRegion, out int destinationRegion)
    {
        destinationRegion = sourceRegion;
        if (!EnsureBuilt() || sourceRegion < 0 || !regionLinks.TryGetValue(sourceRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // 最早跨越。
float earliestCrossing = float.PositiveInfinity;
        // 选定区域。
int selectedRegion = sourceRegion;
        // 当前元素索引。
for (int index = 0; index < links.Count; index++)
        {
            // 连接。
PortalLink link = links[index];
            // 判空前缓存的连接。
global::PursuitNavigationTopology.PortalLink receiverValue4 = link;
            // 门。
DoorController door;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                door = receiverValue4.door;
            }
            else
            {
                door = default;
            }

            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // 来源侧边。
int sourceSide = link.GetSideForRegion(sourceRegion);
            // 另一个区域。
int otherRegion = link.GetOtherRegion(sourceRegion);
            if (sourceSide == 0 || otherRegion < 0)
            {
                continue;
            }

            // 来源深度。
float sourceDepth = Vector3.Dot(source - door.PassageCenter, door.PassageNormal);
            // 目的地深度。
float destinationDepth = Vector3.Dot(destination - door.PassageCenter, door.PassageNormal);
            if (sourceDepth * sourceSide < -0.05f || destinationDepth * sourceSide >= -0.05f)
            {
                continue;
            }

            // 深度增量。
float depthDelta = sourceDepth - destinationDepth;
            if (Mathf.Abs(depthDelta) <= 0.0001f)
            {
                continue;
            }

            // 跨越比例。
float crossingRatio = Mathf.Clamp01(sourceDepth / depthDelta);
            // 跨越点。
Vector3 crossingPoint = Vector3.Lerp(source, destination, crossingRatio);
            // 跨越偏移量。
Vector3 crossingOffset = crossingPoint - door.PassageCenter;
            // 高度。
float height = Mathf.Abs(crossingOffset.y);
            crossingOffset.y = 0f;
            // 侧向。
float lateral = Mathf.Abs(Vector3.Dot(crossingOffset, door.PassageTangent));
            if (height > Mathf.Max(1.5f, door.TopologyVerticalTolerance) || lateral > door.PassageHalfWidth + Mathf.Max(0.45f, door.TopologyCutPadding))
            {
                continue;
            }

            if (crossingRatio >= earliestCrossing)
            {
                continue;
            }

            earliestCrossing = crossingRatio;
            selectedRegion = otherRegion;
        }

        if (selectedRegion == sourceRegion)
        {
            return false;
        }

        destinationRegion = selectedRegion;
        return true;
    }

    /// <summary>TryGetDoorRegions 的业务处理入口；本方法直接依赖 Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.TryGetValue(DoorController, out PursuitNavigationTopology.PortalLink)、PursuitNavigationTopology.EnsureBuilt()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.TryGetValue(DoorController, out PursuitNavigationTopology.PortalLink)、PursuitNavigationTopology.EnsureBuilt()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">门。</param>
    /// <param name="positiveRegion">正向区域。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="negativeRegion">负值区域。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryGetDoorRegions(DoorController door, out int positiveRegion, out int negativeRegion)
    {
        positiveRegion = -1;
        negativeRegion = -1;
        if (!EnsureBuilt() || door == null || !doorLinks.TryGetValue(door, out PortalLink /* 中文含义：连接。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ link))
        {
            return false;
        }

        positiveRegion = link.positiveRegion;
        negativeRegion = link.negativeRegion;
        return positiveRegion >= 0 && negativeRegion >= 0;
    }

    /// <summary>EnsureBuilt 的业务处理入口；本方法直接依赖 PursuitNavigationTopology.Build(AstarPath)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)、PursuitNavigationTopology.IsReady.get、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)、PursuitNavigationTopology.RegionCount.get、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int) 等共 8 处；下游（含内部回调体） PursuitNavigationTopology.Build(AstarPath)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static bool EnsureBuilt()
    {
        // 生效星形寻路。
AstarPath activeAstar = AstarPath.active;
        if (topologyReady && builtAstar == activeAstar)
        {
            return true;
        }

        if (buildingTopology || activeAstar == null || activeAstar.graphs == null || activeAstar.graphs.Length == 0)
        {
            return false;
        }

        buildingTopology = true;
        try
        {
            Build(activeAstar);
        }
        finally
        {
            buildingTopology = false;
        }

        return topologyReady;
    }

    /// <summary>Build 的业务处理入口；本方法直接依赖 DoorController.CopyActiveSceneDoors(List&lt;DoorController&gt;)、List&lt;GraphNode&gt;.Add(GraphNode)、NavGraph.GetNodes(Action&lt;GraphNode&gt;)、PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.Invalidate()、PursuitNavigationTopology.MarkGateNodes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.EnsureBuilt()；下游（含内部回调体） DoorController.CopyActiveSceneDoors(List&lt;DoorController&gt;)、List&lt;GraphNode&gt;.Add(GraphNode)、NavGraph.GetNodes(Action&lt;GraphNode&gt;)、PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.Invalidate()、PursuitNavigationTopology.MarkGateNodes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="activeAstar">生效星形寻路。</param>
    private static void Build(AstarPath activeAstar)
    {
        Invalidate();
        builtAstar = activeAstar;
        DoorController.CopyActiveSceneDoors(doors);
        // 导航图索引。
for (int graphIndex = 0; graphIndex < activeAstar.graphs.Length; graphIndex++)
        {
            // 导航图。
NavGraph graph = activeAstar.graphs[graphIndex];
            if (graph == null)
            {
                continue;
            }

            graph.GetNodes( /* 中文含义：节点。 */node =>
            {
                if (node != null && node.Walkable)
                {
                    nodes.Add(node);
                }
            });
        }

        MarkGateNodes();
        BuildRegions();
        BuildPortalLinks();
        topologyReady = regionCount > 0;
    }

    /// <summary>MarkGateNodes 的业务处理入口；本方法直接依赖 HashSet&lt;GraphNode&gt;.Add(GraphNode)、PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.Build(AstarPath)；下游（含内部回调体） HashSet&lt;GraphNode&gt;.Add(GraphNode)、PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void MarkGateNodes()
    {
        // 节点索引。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // 节点。
GraphNode node = nodes[nodeIndex];
            // 点。
Vector3 point = (Vector3)node.position;
            // 最佳门。
DoorController bestDoor = null;
            // 最佳分值。
float bestScore = float.PositiveInfinity;
            // 门索引。
for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
            {
                // 门。
DoorController door = doors[doorIndex];
                if (!IsNodeInsideDoorCut(point, door, out float /* 中文含义：分值。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ score) || score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDoor = door;
            }

            if (bestDoor == null)
            {
                continue;
            }

            gateNodes.Add(node);
            gateNodeOwners[node] = bestDoor;
        }
    }

    /// <summary>IsNodeInsideDoorCut 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.MarkGateNodes()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="door">门。</param>
    /// <param name="score">分值。输出参数，由本入口写入并交给调用方。</param>
    private static bool IsNodeInsideDoorCut(Vector3 point, DoorController door, out float score)
    {
        score = float.PositiveInfinity;
        if (door == null || !door.isActiveAndEnabled)
        {
            return false;
        }

        // 偏移量。
Vector3 offset = point - door.PassageCenter;
        // 高度。
float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        // 深度。
float depth = Mathf.Abs(Vector3.Dot(offset, door.PassageNormal));
        // 侧向。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
        // 切口深度。
float cutDepth = Mathf.Max(0.55f, door.TopologyCutDepth);
        // 切口一半宽度。
float cutHalfWidth = door.PassageHalfWidth + Mathf.Max(0.25f, door.TopologyCutPadding);
        if (height > Mathf.Max(1.5f, door.TopologyVerticalTolerance) || depth > cutDepth || lateral > cutHalfWidth)
        {
            return false;
        }

        score = depth + lateral * 0.25f + height * 0.1f;
        return true;
    }

    /// <summary>BuildRegions 的业务处理入口；本方法直接依赖 Dictionary&lt;GraphNode, int&gt;.ContainsKey(GraphNode)、GraphNode.GetConnections(Action&lt;GraphNode&gt;, int)、HashSet&lt;GraphNode&gt;.Contains(GraphNode)、Queue&lt;GraphNode&gt;.Clear()、Queue&lt;GraphNode&gt;.Dequeue()、Queue&lt;GraphNode&gt;.Enqueue(GraphNode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.Build(AstarPath)；下游（含内部回调体） Dictionary&lt;GraphNode, int&gt;.ContainsKey(GraphNode)、GraphNode.GetConnections(Action&lt;GraphNode&gt;, int)、HashSet&lt;GraphNode&gt;.Contains(GraphNode)、Queue&lt;GraphNode&gt;.Clear()、Queue&lt;GraphNode&gt;.Dequeue()、Queue&lt;GraphNode&gt;.Enqueue(GraphNode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void BuildRegions()
    {
        regionCount = 0;
        nodeQueue.Clear();
        // 节点索引。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // 种子。
GraphNode seed = nodes[nodeIndex];
            if (gateNodes.Contains(seed) || nodeRegions.ContainsKey(seed))
            {
                continue;
            }

            // 区域。
int region = regionCount++;
            nodeRegions[seed] = region;
            nodeQueue.Enqueue(seed);
            while (nodeQueue.Count > 0)
            {
                // 当前导航图节点。
GraphNode current = nodeQueue.Dequeue();
                current.GetConnections( /* 中文含义：已连接。 */connected =>
                {
                    if (connected == null || !connected.Walkable || gateNodes.Contains(connected) || nodeRegions.ContainsKey(connected))
                    {
                        return;
                    }

                    nodeRegions[connected] = region;
                    nodeQueue.Enqueue(connected);
                });
            }
        }
    }

    /// <summary>BuildPortalLinks 的业务处理入口；本方法直接依赖 PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink)、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)、PursuitNavigationTopology.PortalLink.PortalLink()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.Build(AstarPath)；下游（含内部回调体） PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink)、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)、PursuitNavigationTopology.PortalLink.PortalLink()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void BuildPortalLinks()
    {
        // 门索引。
for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
        {
            // 门。
DoorController door = doors[doorIndex];
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // 正向区域。
int positiveRegion = FindDoorSideRegion(door, 1);
            // 负值区域。
int negativeRegion = FindDoorSideRegion(door, -1);
            if (positiveRegion < 0 || negativeRegion < 0 || positiveRegion == negativeRegion)
            {
                continue;
            }

            // 连接。
PortalLink link = new PortalLink
            {
                door = door,
                positiveRegion = positiveRegion,
                negativeRegion = negativeRegion
            };
            doorLinks[door] = link;
            AddRegionLink(positiveRegion, link);
            AddRegionLink(negativeRegion, link);
        }
    }

    /// <summary>FindDoorSideRegion 的业务处理入口；本方法直接依赖 Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.BuildPortalLinks()；下游（含内部回调体） Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="door">门。</param>
    /// <param name="side">侧边。</param>
    private static int FindDoorSideRegion(DoorController door, int side)
    {
        // 最佳分值。
float bestScore = float.PositiveInfinity;
        // 最佳区域。
int bestRegion = -1;
        // 切口深度。
float cutDepth = Mathf.Max(0.55f, door.TopologyCutDepth);
        // 探测。
Vector3 probe = door.PassageCenter + door.PassageNormal * side * (cutDepth + 0.45f);
        // 节点索引。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // 节点。
GraphNode node = nodes[nodeIndex];
            if (!nodeRegions.TryGetValue(node, out int /* 中文含义：区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ region))
            {
                continue;
            }

            // 点。
Vector3 point = (Vector3)node.position;
            // 偏移量。
Vector3 offset = point - door.PassageCenter;
            if (Mathf.Abs(offset.y) > Mathf.Max(1.5f, door.TopologyVerticalTolerance))
            {
                continue;
            }

            offset.y = 0f;
            // 有符号深度。
float signedDepth = Vector3.Dot(offset, door.PassageNormal);
            if (signedDepth * side <= cutDepth * 0.4f)
            {
                continue;
            }

            // 侧向。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
            if (lateral > door.PassageHalfWidth + 2f)
            {
                continue;
            }

            // 分值。
float score = (point - probe).sqrMagnitude;
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestRegion = region;
        }

        return bestRegion;
    }

    /// <summary>AddRegionLink 的业务处理入口；本方法直接依赖 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.Add(int, List&lt;PursuitNavigationTopology.PortalLink&gt;)、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、List&lt;PursuitNavigationTopology.PortalLink&gt;.Add(PursuitNavigationTopology.PortalLink)、List&lt;PursuitNavigationTopology.PortalLink&gt;.List()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.BuildPortalLinks()；下游（含内部回调体） Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.Add(int, List&lt;PursuitNavigationTopology.PortalLink&gt;)、Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、List&lt;PursuitNavigationTopology.PortalLink&gt;.Add(PursuitNavigationTopology.PortalLink)、List&lt;PursuitNavigationTopology.PortalLink&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="region">区域。</param>
    /// <param name="link">连接。</param>
    private static void AddRegionLink(int region, PortalLink link)
    {
        if (!regionLinks.TryGetValue(region, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            links = new List<PortalLink>();
            regionLinks.Add(region, links);
        }

        links.Add(link);
    }

    /// <summary>TryCorrectRegionByNearbyPortal 的业务处理入口；本方法直接依赖 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、Mathf.Abs(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.TryGetRegion(Vector3, out int)；下游（含内部回调体） Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;)、Mathf.Abs(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="sampledRegion">采样到的区域。</param>
    /// <param name="correctedRegion">校正后的区域。输出参数，由本入口写入并交给调用方。</param>
    private static bool TryCorrectRegionByNearbyPortal(Vector3 point, int sampledRegion, out int correctedRegion)
    {
        correctedRegion = sampledRegion;
        if (!regionLinks.TryGetValue(sampledRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // 最佳分值。
float bestScore = float.PositiveInfinity;
        // 最佳区域。
int bestRegion = sampledRegion;
        // 当前元素索引。
for (int index = 0; index < links.Count; index++)
        {
            // 连接。
PortalLink link = links[index];
            // 判空前缓存的连接。
global::PursuitNavigationTopology.PortalLink receiverValue6 = link;
            // 门。
DoorController door;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                door = receiverValue6.door;
            }
            else
            {
                door = default;
            }

            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // 偏移量。
Vector3 offset = point - door.PassageCenter;
            // 高度。
float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            // 有符号深度。
float signedDepth = Vector3.Dot(offset, door.PassageNormal);
            // 深度。
float depth = Mathf.Abs(signedDepth);
            // 侧向。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
            // 深度限制。
float depthLimit = Mathf.Max(3.5f, door.TopologyCutDepth + 2.4f);
            // 侧向限制。
float lateralLimit = Mathf.Max(2.5f, door.PassageHalfWidth + 1.5f);
            if (height > Mathf.Max(1.5f, door.TopologyVerticalTolerance) || depth > depthLimit || lateral > lateralLimit || Mathf.Abs(signedDepth) <= 0.06f)
            {
                continue;
            }

            // 侧边区域。
int sideRegion;
            if (signedDepth >= 0f)
            {
                sideRegion = link.positiveRegion;
            }
            else
            {
                sideRegion = link.negativeRegion;
            }

            if (sideRegion < 0 || sideRegion == sampledRegion)
            {
                continue;
            }

            // 分值。
float score = depth * depth / Mathf.Max(0.01f, depthLimit * depthLimit) + lateral * lateral / Mathf.Max(0.01f, lateralLimit * lateralLimit);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestRegion = sideRegion;
        }

        if (bestRegion == sampledRegion)
        {
            return false;
        }

        correctedRegion = bestRegion;
        return true;
    }

    /// <summary>TryFindNearestRegion 的业务处理入口；本方法直接依赖 Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.TryGetRegion(Vector3, out int)；下游（含内部回调体） Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">点。</param>
    /// <param name="region">区域。输出参数，由本入口写入并交给调用方。</param>
    private static bool TryFindNearestRegion(Vector3 point, out int region)
    {
        region = -1;
        // 最佳距离平方。
float bestDistanceSqr = float.PositiveInfinity;
        // 节点索引。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // 节点。
GraphNode node = nodes[nodeIndex];
            if (!nodeRegions.TryGetValue(node, out int /* 中文含义：候选区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ candidateRegion))
            {
                continue;
            }

            // 节点点。
Vector3 nodePoint = (Vector3)node.position;
            // 高度。
float height = Mathf.Abs(nodePoint.y - point.y);
            if (height > 2.5f)
            {
                continue;
            }

            // 距离平方。
float distanceSqr = (nodePoint - point).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            region = candidateRegion;
        }

        return region >= 0;
    }
}
