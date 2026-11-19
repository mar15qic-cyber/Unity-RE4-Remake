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
        /// <summary>中文含义：门。使用位置：PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)、PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int) 等共 6 处。</summary>
        public DoorController door;
        /// <summary>中文含义：正向区域。使用位置：PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.PortalLink.GetOtherRegion(int)、PursuitNavigationTopology.PortalLink.GetSideForRegion(int) 等共 6 处。</summary>
        public int positiveRegion;
        /// <summary>中文含义：负值区域。使用位置：PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.PortalLink.GetOtherRegion(int)、PursuitNavigationTopology.PortalLink.GetSideForRegion(int) 等共 6 处。</summary>
        public int negativeRegion;
        /// <summary>GetOtherRegion 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
        /// <remarks>静态调用链：上游 PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="region">中文含义：区域。使用位置：PursuitNavigationTopology.PortalLink.GetOtherRegion(int)。</param>
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
        /// <param name="region">中文含义：区域。使用位置：PursuitNavigationTopology.PortalLink.GetSideForRegion(int)。</param>
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

    /// <summary>中文含义：节点集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 NavGraph.GetNodes(Action&lt;GraphNode&gt;) 的参数、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 等共 6 处。</summary>
    private static readonly List<GraphNode> nodes = new();
    /// <summary>中文含义：门集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 DoorController.CopyActiveSceneDoors(List&lt;DoorController&gt;) 的参数、PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.Invalidate() 等共 4 处。</summary>
    private static readonly List<DoorController> doors = new();
    /// <summary>中文含义：通行口节点集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 GraphNode.GetConnections(Action&lt;GraphNode&gt;, int) 的参数、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.Invalidate() 等共 4 处。</summary>
    private static readonly HashSet<GraphNode> gateNodes = new();
    /// <summary>中文含义：通行口节点拥有者集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.Invalidate()、PursuitNavigationTopology.MarkGateNodes()、PursuitNavigationTopology.TryGetRegion(Vector3, out int)。</summary>
    private static readonly Dictionary<GraphNode, DoorController> gateNodeOwners = new();
    /// <summary>中文含义：节点区域集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 GraphNode.GetConnections(Action&lt;GraphNode&gt;, int) 的参数、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 等共 7 处。</summary>
    private static readonly Dictionary<GraphNode, int> nodeRegions = new();
    /// <summary>中文含义：区域连接集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink)、PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)、PursuitNavigationTopology.Invalidate() 等共 7 处。</summary>
    private static readonly Dictionary<int, List<PortalLink>> regionLinks = new();
    /// <summary>中文含义：门连接集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.BuildPortalLinks()、PursuitNavigationTopology.Invalidate()、PursuitNavigationTopology.TryGetDoorRegions(DoorController, out int, out int) 等共 4 处。</summary>
    private static readonly Dictionary<DoorController, PortalLink> doorLinks = new();
    /// <summary>中文含义：节点队列。只读字段，初始化后不由普通写入口替换。使用位置：作为 GraphNode.GetConnections(Action&lt;GraphNode&gt;, int) 的参数、PursuitNavigationTopology.BuildRegions()。</summary>
    private static readonly Queue<GraphNode> nodeQueue = new();
    /// <summary>中文含义：区域队列。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</summary>
    private static readonly Queue<int> regionQueue = new();
    /// <summary>中文含义：已访问区域集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</summary>
    private static readonly HashSet<int> visitedRegions = new();
    /// <summary>中文含义：前一次区域集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</summary>
    private static readonly Dictionary<int, int> previousRegions = new();
    /// <summary>中文含义：前一次连接集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</summary>
    private static readonly Dictionary<int, PortalLink> previousLinks = new();
    /// <summary>中文含义：区域支撑查询集合。只读字段，初始化后不由普通写入口替换。使用位置：PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。</summary>
    private static readonly HitQueryBuffer regionSupportQueries = new();
    /// <summary>中文含义：已构建星形寻路。使用位置：PursuitNavigationTopology.Build(AstarPath)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.Invalidate()。</summary>
    private static AstarPath builtAstar;
    /// <summary>中文含义：拓扑是否就绪。使用位置：PursuitNavigationTopology.Build(AstarPath)、PursuitNavigationTopology.EnsureBuilt()、PursuitNavigationTopology.Invalidate() 等共 4 处。</summary>
    private static bool topologyReady;
    /// <summary>中文含义：是否建筑拓扑。使用位置：PursuitNavigationTopology.EnsureBuilt()。</summary>
    private static bool buildingTopology;
    /// <summary>中文含义：区域数量。使用位置：PursuitNavigationTopology.Build(AstarPath)、PursuitNavigationTopology.BuildRegions()、PursuitNavigationTopology.Invalidate() 等共 4 处。</summary>
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
    /// <param name="point">中文含义：点。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int) 的参数、作为 PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int) 的参数 等共 4 处。</param>
    /// <param name="region">中文含义：区域。输出参数，由本入口写入并交给调用方。使用位置：作为 Dictionary&lt;GraphNode, int&gt;.TryGetValue(GraphNode, out int) 的参数、作为 PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int) 的参数、作为 PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int) 的参数 等共 4 处。</param>
    public static bool TryGetRegion(Vector3 point, out int region)
    {
        region = -1;
        if (!EnsureBuilt() || AstarPath.active == null)
        {
            return false;
        }

        // nearest（最近）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<GraphNode, DoorController>.TryGetValue(GraphNode, out DoorController) 的参数、作为 Dictionary<GraphNode, int>.TryGetValue(GraphNode, out int) 的参数、PursuitNavigationTopology.TryGetRegion(Vector3, out int)。
NNInfo nearest = AstarPath.active.GetNearest(point, NearestNodeConstraint.Walkable);
        if (nearest.node == null)
        {
            return false;
        }

        if (!nodeRegions.TryGetValue(nearest.node, out region) && gateNodeOwners.TryGetValue(nearest.node, out DoorController /* 中文含义：通行口门。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gateDoor) && doorLinks.TryGetValue(gateDoor, out PortalLink /* 中文含义：通行口连接。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gateLink))
        {
            // side（侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、PursuitNavigationTopology.TryGetRegion(Vector3, out int)。
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
    /// <param name="source">中文含义：来源。使用位置：作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数。</param>
    /// <param name="sourceRegion">中文含义：来源区域。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)。</param>
    /// <param name="destinationRegion">中文含义：目的地区域。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryGetRegion(Vector3, out int) 的参数、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)。</param>
    public static bool TryResolveRegions(Vector3 source, Vector3 destination, out int sourceRegion, out int destinationRegion)
    {
        sourceRegion = -1;
        destinationRegion = -1;
        return TryGetRegion(source, out sourceRegion) && TryGetRegion(destination, out destinationRegion);
    }

    /// <summary>TryFindFirstPortal 的业务处理入口；本方法直接依赖 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)；下游（含内部回调体） PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int) 的参数。</param>
    /// <param name="passageDoor">中文含义：通道门。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int) 的参数、PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)。</param>
    /// <param name="sourceSide">中文含义：来源侧边。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int) 的参数、PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)。</param>
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
    /// <param name="sourceRegion">中文含义：来源区域。使用位置：作为 HashSet&lt;int&gt;.Add(int) 的参数、作为 PursuitNavigationTopology.PortalLink.GetSideForRegion(int) 的参数、作为 Queue&lt;int&gt;.Enqueue(int) 的参数 等共 4 处。</param>
    /// <param name="destinationRegion">中文含义：目的地区域。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</param>
    /// <param name="passageDoor">中文含义：通道门。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</param>
    /// <param name="sourceSide">中文含义：来源侧边。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。</param>
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
            // currentRegion（当前区域）：保存 Queue<int>.Dequeue() 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<int, List<PursuitNavigationTopology.PortalLink>>.TryGetValue(int, out List<PursuitNavigationTopology.PortalLink>) 的参数、作为 PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 的参数、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。
int currentRegion = regionQueue.Dequeue();
            if (!regionLinks.TryGetValue(currentRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
            {
                continue;
            }

            // index（当前元素索引）：使用位置：作为 List<PursuitNavigationTopology.PortalLink>.this[int] 的参数、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。
for (int index = 0; index < links.Count; index++)
            {
                // link（连接）：使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。
PortalLink link = links[index];
                if (link == null || link.door == null || !link.door.isActiveAndEnabled)
                {
                    continue;
                }

                // nextRegion（下一次区域）：保存 PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<int, int>.this[int] 的参数、作为 Dictionary<int, PursuitNavigationTopology.PortalLink>.this[int] 的参数、作为 HashSet<int>.Add(int) 的参数 等共 5 处。
int nextRegion = link.GetOtherRegion(currentRegion);
                if (nextRegion < 0 || !visitedRegions.Add(nextRegion))
                {
                    continue;
                }

                previousRegions[nextRegion] = currentRegion;
                previousLinks[nextRegion] = link;
                if (nextRegion == destinationRegion)
                {
                    // routeRegion（路线区域）：使用位置：作为 Dictionary<int, int>.TryGetValue(int, out int) 的参数、PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。
int routeRegion = destinationRegion;
                    // firstLink（首个连接）：使用位置：PursuitNavigationTopology.TryFindFirstPortal(int, int, out DoorController, out int)。
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
    /// <param name="firstRegion">中文含义：首个区域。使用位置：作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数、作为 PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 的参数、PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)。</param>
    /// <param name="secondRegion">中文含义：第二区域。使用位置：PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)。</param>
    public static bool AreRegionsDirectlyConnected(int firstRegion, int secondRegion)
    {
        if (!EnsureBuilt() || firstRegion < 0 || secondRegion < 0 || !regionLinks.TryGetValue(firstRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // index（当前元素索引）：使用位置：作为 List<PursuitNavigationTopology.PortalLink>.this[int] 的参数、PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)。
for (int index = 0; index < links.Count; index++)
        {
            // link（连接）：使用位置：PursuitNavigationTopology.AreRegionsDirectlyConnected(int, int)。
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
    /// <param name="point">中文含义：点。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数、作为 Mathf.Abs(float) 的参数 等共 4 处。</param>
    /// <param name="region">中文含义：区域。使用位置：作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。</param>
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
                // receiverValue2（判空前缓存的连接）：从连接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
global::PursuitNavigationTopology.PortalLink receiverValue2 = link;
                // door（门）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
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

                // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
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

        // nearest（最近）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<GraphNode, int>.TryGetValue(GraphNode, out int) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
NNInfo nearest = AstarPath.active.GetNearest(point, NearestNodeConstraint.Walkable);
        if (nearest.node == null || !nodeRegions.TryGetValue(nearest.node, out int /* 中文含义：节点区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ nodeRegion) || nodeRegion != region)
        {
            return false;
        }

        // projected（投影后的）：使用位置：作为 Mathf.Abs(float) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
Vector3 projected = nearest.position;
        // horizontal（水平）：使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
Vector3 horizontal = projected - point;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude > 0.75f * 0.75f || Mathf.Abs(projected.y - point.y) > 0.35f)
        {
            return false;
        }

        // hits（命中结果）：保存 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的返回结果，供当前步骤使用。使用位置：作为 PursuitNavigationTopology.IsActorCollider(Collider) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
RaycastHit[] hits = regionSupportQueries.Raycast(point + Vector3.up * 0.4f, Vector3.down, 0.75f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out int /* 中文含义：命中数量。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hitCount);
        // supported（是否支持的）：使用位置：PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
bool supported = false;
        // index（当前元素索引）：使用位置：PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
for (int index = 0; index < hitCount; index++)
        {
            // hit（命中）：使用位置：作为 Mathf.Abs(float) 的参数、作为 PursuitNavigationTopology.IsActorCollider(Collider) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
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

        // horizontalDistance（水平距离）：使用位置：作为 HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int) 的参数、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
float horizontalDistance = horizontal.magnitude;
        if (horizontalDistance <= 0.01f)
        {
            return true;
        }

        hits = regionSupportQueries.Raycast(point + Vector3.up * 0.65f, horizontal / horizontalDistance, horizontalDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out hitCount);
        // index（当前元素索引）：使用位置：PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)。
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
    /// <param name="collider">中文含义：碰撞体。使用位置：PursuitNavigationTopology.IsActorCollider(Collider)。</param>
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
    /// <param name="source">中文含义：来源。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
    /// <param name="sourceRegion">中文含义：来源区域。使用位置：作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数、作为 PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 的参数、作为 PursuitNavigationTopology.PortalLink.GetSideForRegion(int) 的参数 等共 4 处。</param>
    /// <param name="destinationRegion">中文含义：目的地区域。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。</param>
    public static bool TryFindCrossedPortal(Vector3 source, Vector3 destination, int sourceRegion, out int destinationRegion)
    {
        destinationRegion = sourceRegion;
        if (!EnsureBuilt() || sourceRegion < 0 || !regionLinks.TryGetValue(sourceRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // earliestCrossing（最早跨越）：使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
float earliestCrossing = float.PositiveInfinity;
        // selectedRegion（选定区域）：使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
int selectedRegion = sourceRegion;
        // index（当前元素索引）：使用位置：作为 List<PursuitNavigationTopology.PortalLink>.this[int] 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
for (int index = 0; index < links.Count; index++)
        {
            // link（连接）：使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
PortalLink link = links[index];
            // receiverValue4（判空前缓存的连接）：从连接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
global::PursuitNavigationTopology.PortalLink receiverValue4 = link;
            // door（门）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
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

            // sourceSide（来源侧边）：保存 PursuitNavigationTopology.PortalLink.GetSideForRegion(int) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
int sourceSide = link.GetSideForRegion(sourceRegion);
            // otherRegion（另一个区域）：保存 PursuitNavigationTopology.PortalLink.GetOtherRegion(int) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
int otherRegion = link.GetOtherRegion(sourceRegion);
            if (sourceSide == 0 || otherRegion < 0)
            {
                continue;
            }

            // sourceDepth（来源深度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
float sourceDepth = Vector3.Dot(source - door.PassageCenter, door.PassageNormal);
            // destinationDepth（目的地深度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
float destinationDepth = Vector3.Dot(destination - door.PassageCenter, door.PassageNormal);
            if (sourceDepth * sourceSide < -0.05f || destinationDepth * sourceSide >= -0.05f)
            {
                continue;
            }

            // depthDelta（深度增量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Clamp01(float) 的参数。
float depthDelta = sourceDepth - destinationDepth;
            if (Mathf.Abs(depthDelta) <= 0.0001f)
            {
                continue;
            }

            // crossingRatio（跨越比例）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
float crossingRatio = Mathf.Clamp01(sourceDepth / depthDelta);
            // crossingPoint（跨越点）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
Vector3 crossingPoint = Vector3.Lerp(source, destination, crossingRatio);
            // crossingOffset（跨越偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
Vector3 crossingOffset = crossingPoint - door.PassageCenter;
            // height（高度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
float height = Mathf.Abs(crossingOffset.y);
            crossingOffset.y = 0f;
            // lateral（侧向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindCrossedPortal(Vector3, Vector3, int, out int)。
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
    /// <param name="door">中文含义：门。使用位置：作为 Dictionary&lt;DoorController, PursuitNavigationTopology.PortalLink&gt;.TryGetValue(DoorController, out PursuitNavigationTopology.PortalLink) 的参数、PursuitNavigationTopology.TryGetDoorRegions(DoorController, out int, out int)。</param>
    /// <param name="positiveRegion">中文含义：正向区域。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryGetDoorRegions(DoorController, out int, out int)。</param>
    /// <param name="negativeRegion">中文含义：负值区域。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryGetDoorRegions(DoorController, out int, out int)。</param>
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
        // activeAstar（生效星形寻路）：使用位置：作为 PursuitNavigationTopology.Build(AstarPath) 的参数、PursuitNavigationTopology.EnsureBuilt()。
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
    /// <param name="activeAstar">中文含义：生效星形寻路。使用位置：PursuitNavigationTopology.Build(AstarPath)。</param>
    private static void Build(AstarPath activeAstar)
    {
        Invalidate();
        builtAstar = activeAstar;
        DoorController.CopyActiveSceneDoors(doors);
        // graphIndex（导航图索引）：使用位置：PursuitNavigationTopology.Build(AstarPath)。
for (int graphIndex = 0; graphIndex < activeAstar.graphs.Length; graphIndex++)
        {
            // graph（导航图）：使用位置：PursuitNavigationTopology.Build(AstarPath)。
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
        // nodeIndex（节点索引）：使用位置：作为 List<GraphNode>.this[int] 的参数、PursuitNavigationTopology.MarkGateNodes()。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // node（节点）：使用位置：作为 Dictionary<GraphNode, DoorController>.this[GraphNode] 的参数、作为 HashSet<GraphNode>.Add(GraphNode) 的参数、PursuitNavigationTopology.MarkGateNodes()。
GraphNode node = nodes[nodeIndex];
            // point（点）：使用位置：作为 PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float) 的参数。
Vector3 point = (Vector3)node.position;
            // bestDoor（最佳门）：使用位置：PursuitNavigationTopology.MarkGateNodes()。
DoorController bestDoor = null;
            // bestScore（最佳分值）：使用位置：PursuitNavigationTopology.MarkGateNodes()。
float bestScore = float.PositiveInfinity;
            // doorIndex（门索引）：使用位置：作为 List<DoorController>.this[int] 的参数、PursuitNavigationTopology.MarkGateNodes()。
for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
            {
                // door（门）：使用位置：作为 PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float) 的参数、PursuitNavigationTopology.MarkGateNodes()。
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
    /// <param name="point">中文含义：点。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。</param>
    /// <param name="door">中文含义：门。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。</param>
    /// <param name="score">中文含义：分值。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。</param>
    private static bool IsNodeInsideDoorCut(Vector3 point, DoorController door, out float score)
    {
        score = float.PositiveInfinity;
        if (door == null || !door.isActiveAndEnabled)
        {
            return false;
        }

        // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
Vector3 offset = point - door.PassageCenter;
        // height（高度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        // depth（深度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
float depth = Mathf.Abs(Vector3.Dot(offset, door.PassageNormal));
        // lateral（侧向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
        // cutDepth（切口深度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
float cutDepth = Mathf.Max(0.55f, door.TopologyCutDepth);
        // cutHalfWidth（切口一半宽度）：使用位置：PursuitNavigationTopology.IsNodeInsideDoorCut(Vector3, DoorController, out float)。
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
        // nodeIndex（节点索引）：使用位置：作为 List<GraphNode>.this[int] 的参数、PursuitNavigationTopology.BuildRegions()。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // seed（种子）：使用位置：作为 Dictionary<GraphNode, int>.ContainsKey(GraphNode) 的参数、作为 Dictionary<GraphNode, int>.this[GraphNode] 的参数、作为 HashSet<GraphNode>.Contains(GraphNode) 的参数 等共 4 处。
GraphNode seed = nodes[nodeIndex];
            if (gateNodes.Contains(seed) || nodeRegions.ContainsKey(seed))
            {
                continue;
            }

            // region（区域）：使用位置：作为 GraphNode.GetConnections(Action<GraphNode>, int) 的参数、PursuitNavigationTopology.BuildRegions()。
int region = regionCount++;
            nodeRegions[seed] = region;
            nodeQueue.Enqueue(seed);
            while (nodeQueue.Count > 0)
            {
                // current（当前导航图节点）：保存 Queue<GraphNode>.Dequeue() 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.BuildRegions()。
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
        // doorIndex（门索引）：使用位置：作为 List<DoorController>.this[int] 的参数、PursuitNavigationTopology.BuildPortalLinks()。
for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
        {
            // door（门）：使用位置：作为 Dictionary<DoorController, PursuitNavigationTopology.PortalLink>.this[DoorController] 的参数、作为 PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 的参数、PursuitNavigationTopology.BuildPortalLinks()。
DoorController door = doors[doorIndex];
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // positiveRegion（正向区域）：保存 PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 的返回结果，供当前步骤使用。使用位置：作为 PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink) 的参数、PursuitNavigationTopology.BuildPortalLinks()。
int positiveRegion = FindDoorSideRegion(door, 1);
            // negativeRegion（负值区域）：保存 PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 的返回结果，供当前步骤使用。使用位置：作为 PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink) 的参数、PursuitNavigationTopology.BuildPortalLinks()。
int negativeRegion = FindDoorSideRegion(door, -1);
            if (positiveRegion < 0 || negativeRegion < 0 || positiveRegion == negativeRegion)
            {
                continue;
            }

            // link（连接）：使用位置：作为 PursuitNavigationTopology.AddRegionLink(int, PursuitNavigationTopology.PortalLink) 的参数、PursuitNavigationTopology.BuildPortalLinks()。
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
    /// <param name="door">中文含义：门。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。</param>
    /// <param name="side">中文含义：侧边。使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。</param>
    private static int FindDoorSideRegion(DoorController door, int side)
    {
        // bestScore（最佳分值）：使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
float bestScore = float.PositiveInfinity;
        // bestRegion（最佳区域）：使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
int bestRegion = -1;
        // cutDepth（切口深度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
float cutDepth = Mathf.Max(0.55f, door.TopologyCutDepth);
        // probe（探测）：使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
Vector3 probe = door.PassageCenter + door.PassageNormal * side * (cutDepth + 0.45f);
        // nodeIndex（节点索引）：使用位置：作为 List<GraphNode>.this[int] 的参数、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // node（节点）：使用位置：作为 Dictionary<GraphNode, int>.TryGetValue(GraphNode, out int) 的参数、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
GraphNode node = nodes[nodeIndex];
            if (!nodeRegions.TryGetValue(node, out int /* 中文含义：区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ region))
            {
                continue;
            }

            // point（点）：使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
Vector3 point = (Vector3)node.position;
            // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
Vector3 offset = point - door.PassageCenter;
            if (Mathf.Abs(offset.y) > Mathf.Max(1.5f, door.TopologyVerticalTolerance))
            {
                continue;
            }

            offset.y = 0f;
            // signedDepth（有符号深度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
float signedDepth = Vector3.Dot(offset, door.PassageNormal);
            if (signedDepth * side <= cutDepth * 0.4f)
            {
                continue;
            }

            // lateral（侧向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
            if (lateral > door.PassageHalfWidth + 2f)
            {
                continue;
            }

            // score（分值）：使用位置：PursuitNavigationTopology.FindDoorSideRegion(DoorController, int)。
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
    /// <param name="region">中文含义：区域。使用位置：作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.Add(int, List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数、作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数。</param>
    /// <param name="link">中文含义：连接。使用位置：作为 List&lt;PursuitNavigationTopology.PortalLink&gt;.Add(PursuitNavigationTopology.PortalLink) 的参数。</param>
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
    /// <param name="point">中文含义：点。使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。</param>
    /// <param name="sampledRegion">中文含义：采样到的区域。使用位置：作为 Dictionary&lt;int, List&lt;PursuitNavigationTopology.PortalLink&gt; &gt;.TryGetValue(int, out List&lt;PursuitNavigationTopology.PortalLink&gt;) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。</param>
    /// <param name="correctedRegion">中文含义：校正后的区域。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。</param>
    private static bool TryCorrectRegionByNearbyPortal(Vector3 point, int sampledRegion, out int correctedRegion)
    {
        correctedRegion = sampledRegion;
        if (!regionLinks.TryGetValue(sampledRegion, out List<PortalLink> /* 中文含义：连接集合。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ links))
        {
            return false;
        }

        // bestScore（最佳分值）：使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float bestScore = float.PositiveInfinity;
        // bestRegion（最佳区域）：使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
int bestRegion = sampledRegion;
        // index（当前元素索引）：使用位置：作为 List<PursuitNavigationTopology.PortalLink>.this[int] 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
for (int index = 0; index < links.Count; index++)
        {
            // link（连接）：使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
PortalLink link = links[index];
            // receiverValue6（判空前缓存的连接）：从连接读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
global::PursuitNavigationTopology.PortalLink receiverValue6 = link;
            // door（门）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
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

            // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
Vector3 offset = point - door.PassageCenter;
            // height（高度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float height = Mathf.Abs(offset.y);
            offset.y = 0f;
            // signedDepth（有符号深度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float signedDepth = Vector3.Dot(offset, door.PassageNormal);
            // depth（深度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float depth = Mathf.Abs(signedDepth);
            // lateral（侧向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float lateral = Mathf.Abs(Vector3.Dot(offset, door.PassageTangent));
            // depthLimit（深度限制）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float depthLimit = Mathf.Max(3.5f, door.TopologyCutDepth + 2.4f);
            // lateralLimit（侧向限制）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
float lateralLimit = Mathf.Max(2.5f, door.PassageHalfWidth + 1.5f);
            if (height > Mathf.Max(1.5f, door.TopologyVerticalTolerance) || depth > depthLimit || lateral > lateralLimit || Mathf.Abs(signedDepth) <= 0.06f)
            {
                continue;
            }

            // sideRegion（侧边区域）：使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
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

            // score（分值）：使用位置：PursuitNavigationTopology.TryCorrectRegionByNearbyPortal(Vector3, int, out int)。
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
    /// <param name="point">中文含义：点。使用位置：作为 Mathf.Abs(float) 的参数、PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。</param>
    /// <param name="region">中文含义：区域。输出参数，由本入口写入并交给调用方。使用位置：PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。</param>
    private static bool TryFindNearestRegion(Vector3 point, out int region)
    {
        region = -1;
        // bestDistanceSqr（最佳距离平方）：使用位置：PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
float bestDistanceSqr = float.PositiveInfinity;
        // nodeIndex（节点索引）：使用位置：作为 List<GraphNode>.this[int] 的参数、PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            // node（节点）：使用位置：作为 Dictionary<GraphNode, int>.TryGetValue(GraphNode, out int) 的参数、PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
GraphNode node = nodes[nodeIndex];
            if (!nodeRegions.TryGetValue(node, out int /* 中文含义：候选区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ candidateRegion))
            {
                continue;
            }

            // nodePoint（节点点）：使用位置：作为 Mathf.Abs(float) 的参数、PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
Vector3 nodePoint = (Vector3)node.position;
            // height（高度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
float height = Mathf.Abs(nodePoint.y - point.y);
            if (height > 2.5f)
            {
                continue;
            }

            // distanceSqr（距离平方）：使用位置：PursuitNavigationTopology.TryFindNearestRegion(Vector3, out int)。
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
