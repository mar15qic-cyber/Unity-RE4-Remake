using UnityEngine;

/// <summary>敌人行为、动作与通行组件 HouseThreeTraversalGeometry，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(-12000)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "三号屋通行几何")]
public class HouseThreeTraversalGeometry : MonoBehaviour
{
    /// <summary>中文含义：生效路线。使用位置：HouseThreeTraversalGeometry.Active.get、HouseThreeTraversalGeometry.OnDisable()、HouseThreeTraversalGeometry.OnEnable()。</summary>
    private static HouseThreeTraversalGeometry activeRoute;
    [Header("入口")]
    public DoorController entranceDoor;
    /// <summary>中文含义：室外通行口。使用位置：HouseThreeTraversalGeometry.ExteriorGate.get、HouseThreeTraversalGeometry.IsReady.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform exteriorGate;
    /// <summary>中文含义：门通行口。使用位置：HouseThreeTraversalGeometry.DoorGate.get、HouseThreeTraversalGeometry.IsReady.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform doorGate;
    /// <summary>中文含义：室内通行口。使用位置：HouseThreeTraversalGeometry.InteriorGate.get、HouseThreeTraversalGeometry.IsReady.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform interiorGate;
    /// <summary>中文含义：入口一半宽度。使用位置：HouseThreeTraversalGeometry.IsNearEntrance(Vector3, float)、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</summary>
    [Min(0.2f)]
    public float entranceHalfWidth = 0.48f;
    /// <summary>中文含义：入口队列间距。使用位置：作为 Mathf.Max(float, float) 的参数、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    [Min(0.3f)]
    public float entranceQueueSpacing = 0.8f;
    /// <summary>中文含义：房间转向。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.RoomTurn.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    [Header("室内通道")]
    public Transform roomTurn;
    /// <summary>中文含义：下部入口。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.LowerEntry.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform lowerEntry;
    /// <summary>中文含义：室内一半宽度。使用位置：HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    [Min(0.2f)]
    public float interiorHalfWidth = 0.5f;
    /// <summary>中文含义：下部交汇点。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.LowerJunction.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    [Header("楼梯中心线")]
    public Transform lowerJunction;
    /// <summary>中文含义：上部交汇点。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.UpperJunction.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform upperJunction;
    /// <summary>中文含义：上部退出。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.UpperExit.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform upperExit;
    /// <summary>中文含义：上部落地转向。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.UpperLandingTurn.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform upperLandingTurn;
    /// <summary>中文含义：上部平台释放。使用位置：HouseThreeTraversalGeometry.IsReady.get、HouseThreeTraversalGeometry.UpperPlatformRelease.get、StrictAstarGraphBaker.EnsureDoor03TraversalRoute(Scene)。</summary>
    public Transform upperPlatformRelease;
    /// <summary>中文含义：上段桥面。使用位置：HouseThreeTraversalGeometry.IsHasRegisterBridgeDeck(Collider)。</summary>
    [Header("楼梯与门槛碰撞")]
    [Tooltip("村庄门口实际承重的原版地面碰撞。")]
    public Collider nativeDoorThreshold;

    public bool HasEntranceSupport => nativeDoorThreshold != null && nativeDoorThreshold.enabled &&
        !nativeDoorThreshold.isTrigger && nativeDoorThreshold.gameObject.activeInHierarchy &&
        nativeDoorThreshold.gameObject.scene == gameObject.scene;

    [Tooltip("原版楼梯的踏板和立板。敌人步行时沿已验证的楼梯走向越过立板，脚底仍采样原碰撞。")]
    [SerializeField] private Collider[] nativeStairTreads = System.Array.Empty<Collider>();

    [Tooltip("玩家胶囊由脚底已达到的踏板承重，立面及尚未迈上的高阶交给楼梯辅助。")]
    [SerializeField] private CapsuleCollider nativeStairPlayer;
    [SerializeField] private Collider nativeStairLandingSupport;
    private readonly System.Collections.Generic.HashSet<Collider> nativePlayerIgnoredTreads = new();

    private void FixedUpdate()
    {
        if (nativeStairPlayer == null || !nativeStairPlayer.enabled || !nativeStairPlayer.gameObject.activeInHierarchy)
        {
            return;
        }

        if (lowerEntry != null && (nativeStairPlayer.transform.position - lowerEntry.position).sqrMagnitude > 100f)
        {
            return;
        }

        float soleHeight = nativeStairPlayer.bounds.min.y;
        foreach (Collider tread in nativeStairTreads)
        {
            if (tread == null || tread == nativeStairLandingSupport || !tread.enabled ||
                !tread.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = tread.bounds;
            bool horizontalBoard = bounds.size.y < Mathf.Min(bounds.size.x, bounds.size.z);
            bool owned = nativePlayerIgnoredTreads.Contains(tread);
            float clearance = 0.075f;
            if (owned)
            {
                clearance = 0.04f;
            }
            if (horizontalBoard && bounds.max.y <= soleHeight + clearance)
            {
                if (owned)
                {
                    Physics.IgnoreCollision(nativeStairPlayer, tread, false);
                    nativePlayerIgnoredTreads.Remove(tread);
                }
            }
            else if (!Physics.GetIgnoreCollision(nativeStairPlayer, tread))
            {
                Physics.IgnoreCollision(nativeStairPlayer, tread);
                nativePlayerIgnoredTreads.Add(tread);
            }
        }
    }

    internal bool IsNativeStairTread(Collider collider)
    {
        return System.Array.IndexOf(nativeStairTreads, collider) >= 0;
    }

    internal bool RaycastNativeStairTread(Ray ray, out RaycastHit hit, float distance)
    {
        hit = default;
        bool found = false;
        foreach (Collider tread in nativeStairTreads)
        {
            if (tread != null && tread.enabled && tread.gameObject.activeInHierarchy &&
                tread.Raycast(ray, out RaycastHit candidate, distance) &&
                candidate.normal.y >= .63f)
            {
                hit = candidate;
                distance = candidate.distance;
                found = true;
            }
        }

        return found;
    }

    /// <summary>中文含义：生效。使用位置：DoorController.ResolveInteriorPassageSide()、StrictAstarGraphBaker.ValidateDoor03LocalRoute(out bool, bool)、VillagerController.BindingTraversalGeometry() 等共 8 处。</summary>
    public static HouseThreeTraversalGeometry Active
    {
        get
        {
            if (activeRoute == null && !Application.isPlaying)
            {
                activeRoute = FindFirstObjectByType<HouseThreeTraversalGeometry>(FindObjectsInactive.Include);
            }

            return activeRoute;
        }
    }

    /// <summary>中文含义：是否就绪。使用位置：DoorController.ResolveInteriorPassageSide()、DoorwayTraversalOccupancy.Valid(VillagerController, int)、HouseThreeRegionNavigation.ConfigurationError() 等共 13 处。</summary>
    public bool IsReady => isActiveAndEnabled && entranceDoor != null && exteriorGate != null && doorGate != null && interiorGate != null && roomTurn != null && lowerEntry != null && lowerJunction != null && upperJunction != null && upperExit != null && upperLandingTurn != null && upperPlatformRelease != null;
    /// <summary>中文含义：室外通行口。使用位置：作为 HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3) 的参数、作为 StrictAstarGraphBaker.EnsureDoor03StairBridgeCollider(Scene, string, Vector3, Vector3, float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数 等共 7 处。</summary>
    public Vector3 ExteriorGate => exteriorGate.position;
    /// <summary>中文含义：门通行口。使用位置：DoorController.ResolveInteriorPassageSide()、HouseThreeTraversalGeometry.IsOnExteriorSide(Vector3, float)、HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float) 等共 6 处。</summary>
    public Vector3 DoorGate => doorGate.position;
    /// <summary>中文含义：室内通行口。使用位置：作为 HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3) 的参数、作为 StrictAstarGraphBaker.EnsureDoor03StairBridgeCollider(Scene, string, Vector3, Vector3, float) 的参数、DoorController.ResolveInteriorPassageSide() 等共 8 处。</summary>
    public Vector3 InteriorGate => interiorGate.position;
    /// <summary>中文含义：房间转向。使用位置：作为 StrictAstarGraphBaker.EnsureDoor03StairBridgeCollider(Scene, string, Vector3, Vector3, float) 的参数、StrictAstarGraphBaker.ApplyHouseThreeRegionRules(HouseThreeRegionNavigation, AstarPath)。</summary>
    public Vector3 RoomTurn => roomTurn.position;
    /// <summary>中文含义：下部入口。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 GraphNode.GetConnections(Action&lt;GraphNode&gt;, int) 的参数、作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数 等共 12 处。</summary>
    public Vector3 LowerEntry => lowerEntry.position;
    /// <summary>中文含义：下部交汇点。使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、作为 Mathf.Abs(float) 的参数、作为 StrictAstarGraphBaker.EnsureDoor03StairBridgeCollider(Scene, string, Vector3, Vector3, float) 的参数 等共 5 处。</summary>
    public Vector3 LowerJunction => lowerJunction.position;
    /// <summary>中文含义：上部交汇点。使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、作为 StrictAstarGraphBaker.EnsureDoor03StairBridgeCollider(Scene, string, Vector3, Vector3, float) 的参数、作为 StrictAstarGraphBaker.HasContinuousGroundSupport(Vector3, Vector3, float) 的参数 等共 4 处。</summary>
    public Vector3 UpperJunction => upperJunction.position;
    /// <summary>中文含义：上部退出。使用位置：作为 GraphNode.GetConnections(Action&lt;GraphNode&gt;, int) 的参数、作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数、作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数 等共 10 处。</summary>
    public Vector3 UpperExit => upperExit.position;
    /// <summary>中文含义：上部落地转向。使用位置：作为 StrictAstarGraphBaker.HasContinuousGroundSupport(Vector3, Vector3, float) 的参数、StrictAstarGraphBaker.ApplyHouseThreeRegionRules(HouseThreeRegionNavigation, AstarPath)。</summary>
    public Vector3 UpperLandingTurn => upperLandingTurn.position;
    /// <summary>中文含义：上部平台释放。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 StrictAstarGraphBaker.HasContinuousGroundSupport(Vector3, Vector3, float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数 等共 5 处。</summary>
    public Vector3 UpperPlatformRelease => upperPlatformRelease.position;

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;HouseThreeTraversalGeometry&gt;.Add(HouseThreeTraversalGeometry)、List&lt;HouseThreeTraversalGeometry&gt;.Contains(HouseThreeTraversalGeometry)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        activeRoute = this;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;HouseThreeTraversalGeometry&gt;.Remove(HouseThreeTraversalGeometry)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        if (nativeStairPlayer != null && nativeStairPlayer.enabled && nativeStairPlayer.gameObject.activeInHierarchy)
        {
            foreach (Collider tread in nativePlayerIgnoredTreads)
            {
                if (tread != null && tread.enabled && tread.gameObject.activeInHierarchy)
                {
                    Physics.IgnoreCollision(nativeStairPlayer, tread, false);
                }
            }
        }

        nativePlayerIgnoredTreads.Clear();
        if (activeRoute == this)
        {
            activeRoute = null;
        }
    }

    /// <summary>IsOnInteriorSide 的业务处理入口；本方法直接依赖 Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorwayTraversalOccupancy.Valid(VillagerController, int)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)。</param>
    /// <param name="margin">中文含义：余量。使用位置：HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)。</param>
    public bool IsOnInteriorSide(Vector3 position, float margin = 0.12f)
    {
        if (!IsReady)
        {
            return false;
        }

        // inward（向内）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)。
Vector3 inward = InteriorGate - DoorGate;
        inward.y = 0f;
        if (inward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        inward.Normalize();
        // offset（偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeTraversalGeometry.IsOnInteriorSide(Vector3, float)。
Vector3 offset = position - DoorGate;
        offset.y = 0f;
        return Vector3.Dot(offset, inward) >= margin;
    }

    /// <summary>IsOnExteriorSide 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Abs(float)、Vector3.Dot(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：HouseThreeTraversalGeometry.IsOnExteriorSide(Vector3, float)。</param>
    /// <param name="margin">中文含义：余量。使用位置：作为 Mathf.Abs(float) 的参数。</param>
    public bool IsOnExteriorSide(Vector3 position, float margin = 0.12f)
    {
        if (!IsReady)
        {
            return false;
        }

        // inward（向内）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeTraversalGeometry.IsOnExteriorSide(Vector3, float)。
Vector3 inward = InteriorGate - DoorGate;
        inward.y = 0f;
        if (inward.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        inward.Normalize();
        // offset（偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeTraversalGeometry.IsOnExteriorSide(Vector3, float)。
Vector3 offset = position - DoorGate;
        offset.y = 0f;
        return Vector3.Dot(offset, inward) <= -Mathf.Abs(margin);
    }

    /// <summary>IsNearEntrance 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3) 的参数。</param>
    /// <param name="padding">中文含义：留白。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public bool IsNearEntrance(Vector3 position, float padding = 0f)
    {
        if (!IsReady)
        {
            return false;
        }

        return DistanceToSegmentHorizontal(position, ExteriorGate, InteriorGate) <= entranceHalfWidth + Mathf.Max(0f, padding);
    }

    /// <summary>TryResolveStairHeight 的业务处理入口；本方法直接依赖 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)、PathAgent.OnPathComplete(Path, PathAgent.RequestContext)、VillagerController.PlantNearHouseThreeTread(Vector3)、VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool) 等共 6 处；下游（含内部回调体） HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)。</param>
    /// <param name="height">中文含义：高度。输出参数，由本入口写入并交给调用方。使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)。</param>
    public bool TryResolveStairHeight(Vector3 position, out float height)
    {
        height = position.y;
        if (!IsReady)
        {
            return false;
        }

        // nearestDistance（最近距离）：使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)。
float nearestDistance = float.MaxValue;
        // found（是否已找到）：使用位置：作为 HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool) 的参数、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)。
bool found = false;
        ResolveSegmentHeight(position, LowerEntry, LowerJunction, ref nearestDistance, ref height, ref found);
        ResolveSegmentHeight(position, LowerJunction, UpperJunction, ref nearestDistance, ref height, ref found);
        ResolveSegmentHeight(position, UpperJunction, UpperExit, ref nearestDistance, ref height, ref found);
        return found && nearestDistance <= interiorHalfWidth + 0.2f;
    }

    /// <summary>ResolveSegmentHeight 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Vector2.Distance(Vector2, Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Lerp(Vector2, Vector2, float)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Vector2.Distance(Vector2, Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Lerp(Vector2, Vector2, float)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="start">中文含义：起始。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="end">中文含义：结束。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="nearestDistance">中文含义：最近距离。按引用传入，读写会作用于调用方原存储位置。使用位置：HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。</param>
    /// <param name="height">中文含义：高度。按引用传入，读写会作用于调用方原存储位置。使用位置：HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。</param>
    /// <param name="found">中文含义：是否已找到。按引用传入，读写会作用于调用方原存储位置。使用位置：HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。</param>
    private static void ResolveSegmentHeight(Vector3 position, Vector3 start, Vector3 end, ref float nearestDistance, ref float height, ref bool found)
    {
        // point2D（点2距离）：使用位置：作为 Vector2.Distance(Vector2, Vector2) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数。
Vector2 point2D = new Vector2(position.x, position.z);
        // start2D（起始2距离）：使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
Vector2 start2D = new Vector2(start.x, start.z);
        // end2D（结束2距离）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
Vector2 end2D = new Vector2(end.x, end.z);
        // segment（线段）：使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
Vector2 segment = end2D - start2D;
        // lengthSquared（长度平方）：使用位置：作为 Mathf.Clamp01(float) 的参数、HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
float lengthSquared = segment.sqrMagnitude;
        // progress（进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
float progress;
        if (lengthSquared > 0.0001f)
        {
            progress = Mathf.Clamp01(Vector2.Dot(point2D - start2D, segment) / lengthSquared);
        }
        else
        {
            progress = 0f;
        }

        // nearest（最近）：保存 Vector2.Lerp(Vector2, Vector2, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Distance(Vector2, Vector2) 的参数。
Vector2 nearest = Vector2.Lerp(start2D, end2D, progress);
        // distance（距离）：保存 Vector2.Distance(Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：HouseThreeTraversalGeometry.ResolveSegmentHeight(Vector3, Vector3, Vector3, ref float, ref float, ref bool)。
float distance = Vector2.Distance(point2D, nearest);
        if (distance >= nearestDistance)
        {
            return;
        }

        nearestDistance = distance;
        height = Mathf.Lerp(start.y, end.y, progress);
        found = true;
    }

    /// <summary>DistanceToSegmentHorizontal 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Vector2.Distance(Vector2, Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Lerp(Vector2, Vector2, float)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeTraversalGeometry.IsNearEntrance(Vector3, float)；下游（含内部回调体） Mathf.Clamp01(float)、Vector2.Distance(Vector2, Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Lerp(Vector2, Vector2, float)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="start">中文含义：起始。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="end">中文含义：结束。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    private static float DistanceToSegmentHorizontal(Vector3 point, Vector3 start, Vector3 end)
    {
        // point2D（点2距离）：使用位置：作为 Vector2.Distance(Vector2, Vector2) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数。
Vector2 point2D = new Vector2(point.x, point.z);
        // start2D（起始2距离）：使用位置：作为 Vector2.Distance(Vector2, Vector2) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数、作为 Vector2.Lerp(Vector2, Vector2, float) 的参数 等共 4 处。
Vector2 start2D = new Vector2(start.x, start.z);
        // end2D（结束2距离）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。
Vector2 end2D = new Vector2(end.x, end.z);
        // segment（线段）：使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数、HouseThreeTraversalGeometry.DistanceToSegmentHorizontal(Vector3, Vector3, Vector3)。
Vector2 segment = end2D - start2D;
        if (segment.sqrMagnitude <= 0.0001f)
        {
            return Vector2.Distance(point2D, start2D);
        }

        // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
float progress = Mathf.Clamp01(Vector2.Dot(point2D - start2D, segment) / segment.sqrMagnitude);
        return Vector2.Distance(point2D, Vector2.Lerp(start2D, end2D, progress));
    }
}
