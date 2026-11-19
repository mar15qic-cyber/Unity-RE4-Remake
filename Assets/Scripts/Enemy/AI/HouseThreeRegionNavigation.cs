using Pathfinding;
using UnityEngine;
using System.Collections.Generic;

/// <summary>敌人行为、动作与通行使用的 VillageTraversalRegion 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "村庄通行区域")]
public enum VillageTraversalRegion
{
    /// <summary>中文含义：房屋外侧地面。分类成员“屋外地面”，对应既有数值 0；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("屋外地面")]
    HouseOuterGround,
    /// <summary>中文含义：首个楼层室内。分类成员“一楼室内”，对应既有数值 1；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("一楼室内")]
    FirstFloorIndoors,
    /// <summary>中文含义：楼梯。分类成员“楼梯”，对应既有数值 2；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("楼梯")]
    Stair,
    /// <summary>中文含义：第二楼层室内。分类成员“二楼室内”，对应既有数值 3；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("二楼室内")]
    SecondFloorIndoors,
    /// <summary>中文含义：第二楼层外侧平台。分类成员“二楼外平台”，对应既有数值 4；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("二楼外平台")]
    SecondFloorOuterPlatform,
    [UnityEngine.InspectorName("塔楼梯顶")]
    TowerPlatform
}

/// <summary>敌人行为、动作与通行组件 HouseThreeRegionNavigation，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(-11000)]
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "三号屋分区导航")]
public sealed class HouseThreeRegionNavigation : MonoBehaviour
{
    /// <summary>中文含义：正式缓存。使用位置：HouseThreeRegionNavigation.ConfigurationError()、HouseThreeRegionNavigation.LoadProductionImage()、StrictAstarGraphBaker.PublishCandidate()。</summary>
    [Header("正式分区导航数据")]
    [UnityEngine.Serialization.FormerlySerializedAs("试验缓存")]
    [UnityEngine.Serialization.FormerlySerializedAs("正式缓存")]
    [UnityEngine.InspectorName("正式缓存")]
    public TextAsset productionCache;
    [Tooltip("布景调整后需要重建的局部导航范围。")]
    public Bounds[] refreshedSceneryBounds;
    /// <summary>中文含义：通路。使用位置：作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数 等共 11 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("通路")]
    [UnityEngine.InspectorName("通路")]
    public HouseThreeTraversalGeometry traversal;
    /// <summary>中文含义：外平台标识。使用位置：作为 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3) 的参数、作为 EnemyWindowTraversal.TryGetPlatformRegionState(string, out bool) 的参数、作为 LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool) 的参数 等共 8 处。</summary>
    [Header("沿用窗口和外平台边界的区域标识")]
    [UnityEngine.Serialization.FormerlySerializedAs("外平台标识")]
    [UnityEngine.InspectorName("外平台标识")]
    public string outerPlatformIdentifier;
    /// <summary>中文含义：一楼范围。使用位置：HouseThreeRegionNavigation.TreadRegion(Vector3)。</summary>
    [Header("室内地板的世界边界")]
    [UnityEngine.Serialization.FormerlySerializedAs("一楼范围")]
    [UnityEngine.InspectorName("一楼范围")]
    public Bounds firstFloorRange;
    /// <summary>中文含义：二楼范围。使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)、HouseThreeRegionNavigation.TreadRegion(Vector3)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("二楼范围")]
    [UnityEngine.InspectorName("二楼范围")]
    public Bounds secondFloorRange;
    /// <summary>中文含义：当前。使用位置：EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginPatrol() 等共 27 处。</summary>
    public static HouseThreeRegionNavigation Current { get; private set; }

    /// <summary>中文含义：楼梯上部成员。只读字段，初始化后不由普通写入口替换。使用位置：HouseThreeRegionNavigation.OnDisable()、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。</summary>
    private readonly List<VillagerController> stairUpperMember = new List<VillagerController>();
    /// <summary>中文含义：窗口专用连接。使用位置：HouseThreeRegionNavigation.ConfigurationError()、HouseThreeRegionNavigation.OnEnable()、StrictAstarGraphBaker.ApplyHouseThreeRegionRules(HouseThreeRegionNavigation, AstarPath) 等共 4 处。</summary>
    [HideInInspector]
    [UnityEngine.Serialization.FormerlySerializedAs("窗口专用连接")]
    [UnityEngine.InspectorName("窗口专用连接")]
    public NodeLink2[] windowDedicatedConnect;
    /// <summary>中文含义：脚部下部命中。只读字段，初始化后不由普通写入口替换。使用位置：作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。</summary>
    private readonly RaycastHit[] footLowerHit = new RaycastHit[16];
    /// <summary>中文含义：跟随间距。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。</summary>
    [Header("楼梯入口让行与跟随")]
    [Range(.3f, 1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("跟随间距")]
    [UnityEngine.InspectorName("跟随间距")]
    public float followSpacing = .62f;
    /// <summary>中文含义：让出行帧。只读字段，初始化后不由普通写入口替换。使用位置：HouseThreeRegionNavigation.IsEntryPointYieldRow(VillagerController)、HouseThreeRegionNavigation.OnDisable()、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController) 等共 4 处。</summary>
    private readonly Dictionary<VillagerController, int> yieldRowFrame = new();
    /// <summary>ConfigurationError 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.OnEnable()、SceneStartupEntry.ConfigurationError()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string ConfigurationError()
    {
        if (!productionCache || productionCache.bytes.Length == 0)
        {
            return "正式导航缓存缺失";
        }

        if (!traversal || !traversal.IsReady)
        {
            return "通行几何缺失";
        }

        if (firstFloorRange.size.x <= 0f || firstFloorRange.size.y <= 0f || firstFloorRange.size.z <= 0f ||
            secondFloorRange.size.x <= 0f || secondFloorRange.size.y <= 0f || secondFloorRange.size.z <= 0f)
        {
            return "室内楼层范围缺失";
        }

        if (string.IsNullOrWhiteSpace(outerPlatformIdentifier))
        {
            return "外平台标识缺失";
        }

        if (windowDedicatedConnect == null || windowDedicatedConnect.Length == 0)
        {
            return "窗口隔离配置缺失";
        }

        // connect（连接）：每轮从窗口专用连接取得一个元素，仅代表当前这一项。使用位置：HouseThreeRegionNavigation.ConfigurationError()。
foreach (var connect in windowDedicatedConnect)
        {
            if (!connect || !connect.end)
            {
                return "窗口隔离连接失效";
            }
        }

        return null;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object, Object)、HouseThreeRegionNavigation.ConfigurationError()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // error（错误）：保存 HouseThreeRegionNavigation.ConfigurationError() 的返回结果，供当前步骤使用。使用位置：作为 Debug.LogError(object, Object) 的参数、HouseThreeRegionNavigation.OnEnable()。
string error = ConfigurationError();
        if (error != null)
        {
            Debug.LogError("三号屋分区导航无法启动：" + error, this);
            return;
        }

        // connect（连接）：每轮从窗口专用连接取得一个元素，仅代表当前这一项。使用位置：HouseThreeRegionNavigation.OnEnable()。
foreach (var connect in windowDedicatedConnect)
        {
            connect.enabled = false;
        }

        Current = this;
        AstarPath.OnAwakeSettings += LoadProductionImage;
    }

    internal bool RefreshSceneryNavigation()
    {
        if (refreshedSceneryBounds == null || refreshedSceneryBounds.Length == 0)
        {
            return true;
        }

        AstarPath graph = AstarPath.active;
        if (graph == null || graph.data.recastGraph == null)
        {
            return false;
        }

        Physics.SyncTransforms();
        foreach (Bounds area in refreshedSceneryBounds)
        {
            graph.UpdateGraphs(new GraphUpdateObject(area) { updatePhysics = true });
        }
        graph.FlushGraphUpdates();
        return true;
    }

    /// <summary>LoadProductionImage 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadProductionImage()
    {
        AstarPath.active.data.file_cachedStartup = productionCache;
        AstarPath.active.data.cacheStartup = true;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;VillagerController, int&gt;.Clear()、List&lt;VillagerController&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        AstarPath.OnAwakeSettings -= LoadProductionImage;
        if (Current == this)
        {
            Current = null;
        }

        stairUpperMember.Clear();
        yieldRowFrame.Clear();
    }

    /// <summary>ReleaseCharacter 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()、EnemyActionCoordinator.BeginIdle()、EnemyActionCoordinator.BeginPatrol()、EnemyActionCoordinator.BeginStationaryPatrol(float)、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3) 等共 8 处；下游（含内部回调体） Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController)、List&lt;VillagerController&gt;.Remove(VillagerController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：作为 Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController) 的参数、作为 List&lt;VillagerController&gt;.Remove(VillagerController) 的参数。</param>
    public void ReleaseCharacter(VillagerController character)
    {
        stairUpperMember.Remove(character);
        yieldRowFrame.Remove(character);
    }

    /// <summary>IsEntryPointYieldRow 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, int&gt;.TryGetValue(VillagerController, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Dictionary&lt;VillagerController, int&gt;.TryGetValue(VillagerController, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：作为 Dictionary&lt;VillagerController, int&gt;.TryGetValue(VillagerController, out int) 的参数。</param>
    public bool IsEntryPointYieldRow(VillagerController character) => yieldRowFrame.TryGetValue(character, out int /* 中文含义：帧。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ frame) && Time.frameCount - frame <= 1;
    /// <summary>TreadRegion 的业务处理入口；本方法直接依赖 Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.IsPlayerWithinCombatVerticalRange(LeonController)、HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3)、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float) 等共 12 处；下游（含内部回调体） Bounds.Contains(Vector3)、EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 Bounds.Contains(Vector3) 的参数、作为 EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(string, Vector3) 的参数、作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数 等共 4 处。</param>
    internal bool TryReadOuterPlatformSeam(Vector3 foot, Vector3 direction, float radius, RaycastHit blocker, out float rise)
    {
        rise = 0f;
        if (blocker.collider == null || blocker.collider.attachedRigidbody != null || TreadRegion(foot) != VillageTraversalRegion.SecondFloorOuterPlatform)
        {
            return false;
        }

        const float maximumSeamHeight = 0.22f;
        for (int sample = 0; sample < 3; sample++)
        {
            Vector3 ahead = foot + direction * (radius + .06f + sample * .08f);
            if (TreadRegion(ahead) != VillageTraversalRegion.SecondFloorOuterPlatform)
            {
                continue;
            }

            var probe = new Ray(ahead + Vector3.up * (maximumSeamHeight + .06f), Vector3.down);
            if (!blocker.collider.Raycast(probe, out RaycastHit tread, maximumSeamHeight + .08f) ||
                tread.normal.y < .72f || blocker.point.y > tread.point.y + .04f)
            {
                continue;
            }

            float candidate = tread.point.y - foot.y;
            if (candidate > .015f && candidate <= maximumSeamHeight)
            {
                rise = candidate;
                return true;
            }
        }

        return false;
    }

    public VillageTraversalRegion TreadRegion(Vector3 position)
    {
        if (traversal.TryResolveStairHeight(position, out float /* 中文含义：楼梯高度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stairHeight) && Mathf.Abs(position.y - stairHeight) < 0.38f)
        {
            return VillageTraversalRegion.Stair;
        }

        if (secondFloorRange.Contains(position))
        {
            return VillageTraversalRegion.SecondFloorIndoors;
        }

        if (firstFloorRange.Contains(position))
        {
            return VillageTraversalRegion.FirstFloorIndoors;
        }

        if (EnemyNavigationElevatedWalkableVolume.ContainsBoundaryRegionPoint(outerPlatformIdentifier, position))
        {
            return VillageTraversalRegion.SecondFloorOuterPlatform;
        }
        else
        {
            return VillageTraversalRegion.HouseOuterGround;
        }
    }

    /// <summary>ReadPlatformOwnership 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、float.IsInfinity(float)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Abs(float)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.IsCurrentActorOnPlatform()、LeonWindowTraversal.TryGetEnemyPlatformRegionState(string, out bool)；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、Component.GetComponentInParent&lt;VillagerController&gt;()、float.IsInfinity(float)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Abs(float)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footBottom">中文含义：脚部底部。使用位置：作为 Mathf.Abs(float) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数 等共 4 处。</param>
    /// <param name="originalOwnership">中文含义：是否原始归属。使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。</param>
    public bool ReadPlatformOwnership(Vector3 footBottom, bool originalOwnership)
    {
        // 只接受实体踏面，窗台交接沿用上一侧。
        if (!originalOwnership && footBottom.y < secondFloorRange.min.y)
        {
            return false;
        }

        // passage（通道）：每轮从敌人翻窗通行路线的生效路线集合取得一个元素，仅代表当前这一项。使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
foreach (var passage in EnemyWindowTraversalRoute.ActiveRoutes)
        {
            // window（窗口）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
var window = passage.windowPoint;
            if (!window || passage.platformRegionId != outerPlatformIdentifier)
            {
                continue;
            }

            // offset（偏移量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数。
var offset = footBottom - window.OpeningCenterWorld;
            if (Mathf.Abs(offset.y) <= window.openingHeight && Mathf.Abs(Vector3.Dot(offset, window.NormalWorld)) < window.wallThickness * .5f && Mathf.Abs(Vector3.Dot(offset, window.transform.right)) < window.openingWidth * .5f)
            {
                return originalOwnership;
            }
        }

        // count（数量）：保存 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
int count = Physics.RaycastNonAlloc(footBottom + Vector3.up * .18f, Vector3.down, footLowerHit, .48f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        // latest（最新）：使用位置：作为 float.IsInfinity(float) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
float latest = float.PositiveInfinity;
        // tread（踏面）：使用位置：作为 HouseThreeRegionNavigation.TreadRegion(Vector3) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
Vector3 tread = default;
        // i（当前遍历索引）：使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
for (int i = 0; i < count; i++)
        {
            // hit（命中）：使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
var hit = footLowerHit[i];
            if (hit.normal.y < .45f || hit.distance >= latest || hit.collider.GetComponentInParent<VillagerController>() || hit.collider.GetComponentInParent<LeonController>())
            {
                continue;
            }

            latest = hit.distance;
            tread = hit.point;
        }

        if (float.IsInfinity(latest) && TreadRegion(footBottom) == VillageTraversalRegion.SecondFloorOuterPlatform)
        {
            // 平台边缘可能只在浅探针以下有实际踏面。先确认脚下，再考虑球体探针扫到的侧边表面。
            count = Physics.RaycastNonAlloc(footBottom + Vector3.up * .18f, Vector3.down, footLowerHit, .93f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = footLowerHit[i];
                if (hit.normal.y < .45f || hit.distance >= latest || hit.collider.GetComponentInParent<VillagerController>() || hit.collider.GetComponentInParent<LeonController>())
                {
                    continue;
                }

                latest = hit.distance;
                tread = hit.point;
            }
        }

        if (float.IsInfinity(latest))
        {
            // 木板窄缝可能漏过中心射线，但脚底周围仍有同层踏面。
            // 仅用贴近脚底的实体补采样，不能借楼下地面判断窗内外。
            count = Physics.SphereCastNonAlloc(footBottom + Vector3.up * .5f, .25f, Vector3.down, footLowerHit, .7f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // i（当前遍历索引）：使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
for (int i = 0; i < count; i++)
            {
                // hit（命中）：使用位置：作为 Mathf.Abs(float) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
var hit = footLowerHit[i];
                if (hit.normal.y < .45f || hit.distance >= latest || Mathf.Abs(hit.point.y - footBottom.y) > .3f || hit.collider.GetComponentInParent<VillagerController>() || hit.collider.GetComponentInParent<LeonController>())
                {
                    continue;
                }

                latest = hit.distance;
                tread = hit.point;
            }
        }

        if (float.IsInfinity(latest))
        {
            // 尸体会抬高脚底；补查同层原有踏面，不把临时角色碰撞当成楼层。
            // 查询深度小于上下楼层间距，翻越过程仍由调用方保留来源区域。
            count = Physics.RaycastNonAlloc(footBottom + Vector3.up * .18f, Vector3.down, footLowerHit, 1.38f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // i（当前遍历索引）：使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
for (int i = 0; i < count; i++)
            {
                // hit（命中）：使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
var hit = footLowerHit[i];
                if (hit.normal.y < .45f || hit.distance >= latest || hit.collider.GetComponentInParent<VillagerController>() || hit.collider.GetComponentInParent<LeonController>())
                {
                    continue;
                }

                latest = hit.distance;
                tread = hit.point;
            }
        }

        if (float.IsInfinity(latest))
        {
            return originalOwnership;
        }

        // region（区域）：保存 HouseThreeRegionNavigation.TreadRegion(Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool)。
var region = TreadRegion(tread);
        if (originalOwnership && TreadRegion(footBottom) == VillageTraversalRegion.SecondFloorOuterPlatform
            && footBottom.y - tread.y > .3f)
        {
            // 离边后的远距离探地只预测落点，不能在人物仍处于平台高度时提前换层。
            return true;
        }
        if (region != VillageTraversalRegion.SecondFloorOuterPlatform && region != VillageTraversalRegion.SecondFloorIndoors)
        {
            return false;
        }

        // 区域属于实际踏面，导航节点内缩或边界离散误差不能改变窗内外归属。
        return region == VillageTraversalRegion.SecondFloorOuterPlatform;
    }

    /// <summary>ReadSameSideNavigationPoint 的业务处理入口；本方法直接依赖 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)、Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、PathAgent.ReadNormalNavigationPoint(Vector3, float, float)、RegionWindowRouting.Projection(Vector3, bool)、StrictAstarGraphBaker.ValidatePlatformSeamWalk(HouseThreeRegionNavigation, AstarPath)；下游（含内部回调体） HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)、Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float) 的参数。</param>
    /// <param name="outside">中文含义：是否外部。使用位置：作为 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float) 的参数。</param>
    /// <param name="maximumDistance">中文含义：最大距离。使用位置：作为 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float) 的参数。</param>
    /// <param name="maximumHeightDifference">中文含义：最大高度差值。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    public NNInfo ReadSameSideNavigationPoint(Vector3 position, bool outside, float maximumDistance, float maximumHeightDifference) => QuerySameSideSurface(position, outside, maximumDistance, Mathf.Clamp(maximumHeightDifference, .1f, .6f));
    /// <summary>ReadChaseApproachPoint 的业务处理入口；本方法直接依赖 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)、HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversal.TryResolvePursuitDestination(Vector3, out Vector3)、EnemyWindowTraversal.TryResolveSamePlatformPursuitDestination(Vector3, out Vector3)；下游（含内部回调体） HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)、HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float) 的参数、作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    /// <param name="outside">中文含义：是否外部。使用位置：作为 HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float) 的参数、作为 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的参数。</param>
    public NNInfo ReadChaseApproachPoint(Vector3 position, bool outside)
    {
        // strictPoint（严格点）：保存 HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)。
var strictPoint = ReadSameSideNavigationPoint(position, outside, 1.25f, .6f);
        // 高处目标可以靠近到同侧合法表面，不能扩大敌人自身接入或攻击范围。
        if (strictPoint.node != null)
        {
            return strictPoint;
        }
        else
        {
            return QuerySameSideSurface(position, outside, 2.4f, 1.2f);
        }
    }

    /// <summary>QuerySameSideSurface 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、GraphNode.ClosestPointOnNode(Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Sqrt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.ReadChaseApproachPoint(Vector3, bool)、HouseThreeRegionNavigation.ReadSameSideNavigationPoint(Vector3, bool, float, float)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、GraphNode.ClosestPointOnNode(Vector3)、HouseThreeRegionNavigation.TreadRegion(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Sqrt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="positionValue">中文含义：位置值。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 GraphNode.ClosestPointOnNode(Vector3) 的参数、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。</param>
    /// <param name="outside">中文含义：是否外部。使用位置：HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。</param>
    /// <param name="maximumDistance">中文含义：最大距离。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Sqrt(float) 的参数、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。</param>
    /// <param name="maximumHeightDifference">中文含义：最大高度差值。使用位置：作为 Mathf.Sqrt(float) 的参数、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。</param>
    private NNInfo QuerySameSideSurface(Vector3 positionValue, bool outside, float maximumDistance, float maximumHeightDifference)
    {
        if (AstarPath.active == null)
        {
            return default;
        }

        // constraint（约束）：使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。
var constraint = NearestNodeConstraint.Walkable;
        maximumDistance = Mathf.Max(.1f, maximumDistance);
        constraint.maxDistance = Mathf.Sqrt(maximumDistance * maximumDistance + maximumHeightDifference * maximumHeightDifference);
        // 木板边沿的节点会内缩；可以投影到同侧踏面，不能穿过薄墙选中室内节点。
        constraint.filter = /* 中文含义：节点。 */ node =>
        {
            // point（点）：保存 GraphNode.ClosestPointOnNode(Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。
var point = node.ClosestPointOnNode(positionValue);
            // difference（差值）：使用位置：作为 Mathf.Abs(float) 的参数、HouseThreeRegionNavigation.QuerySameSideSurface(Vector3, bool, float, float)。
var difference = point - positionValue;
            return Mathf.Abs(difference.y) <= maximumHeightDifference && difference.x * difference.x + difference.z * difference.z <= maximumDistance * maximumDistance && (TreadRegion((Vector3)node.position) == VillageTraversalRegion.SecondFloorOuterPlatform) == outside;
        };
        return AstarPath.active.GetNearest(positionValue, constraint);
    }

    /// <summary>CrossLayerOrIndoorsChase 的业务处理入口；本方法直接依赖 HouseThreeRegionNavigation.TreadRegion(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.RefreshChaseDestination()；下游（含内部回调体） HouseThreeRegionNavigation.TreadRegion(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 HouseThreeRegionNavigation.TreadRegion(Vector3) 的参数。</param>
    /// <param name="player">中文含义：玩家。使用位置：作为 HouseThreeRegionNavigation.TreadRegion(Vector3) 的参数。</param>
    public bool CrossLayerOrIndoorsChase(Vector3 enemy, Vector3 player)
    {
        // startPoint（起始点）：保存 HouseThreeRegionNavigation.TreadRegion(Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3)。
var startPoint = TreadRegion(enemy);
        // endPoint（结束点）：保存 HouseThreeRegionNavigation.TreadRegion(Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.CrossLayerOrIndoorsChase(Vector3, Vector3)。
var endPoint = TreadRegion(player);
        return startPoint != VillageTraversalRegion.HouseOuterGround || endPoint != VillageTraversalRegion.HouseOuterGround;
    }

    /// <summary>CanNormalWalk 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：HouseThreeRegionNavigation.CanNormalWalk(VillagerController)。</param>
    private bool CanNormalWalk(VillagerController character)
    {
        return character && character.isActiveAndEnabled && character.IsRuntimeInitialized && character.IsActive && !character.IsDead && !character.IsPermanentlyCrawling && !character.HasPendingHit && !character.IsQTEControlled && character.CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && (character.CurrentState == VillagerState.Chase || character.CurrentState == VillagerState.Patrol);
    }

    /// <summary>ApproachEndPoint 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.ProjectOnPlane(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)；下游（含内部回调体） Mathf.Abs(float)、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数。</param>
    /// <param name="endPoint">中文含义：结束点。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float)。</param>
    private bool ApproachEndPoint(Vector3 position, Vector3 endPoint, float radius) => Mathf.Abs(position.y - endPoint.y) < .45f && Vector3.ProjectOnPlane(position - endPoint, Vector3.up).sqrMagnitude < radius * radius;
    /// <summary>WalkDirection 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)；下游（含内部回调体） Mathf.Abs(float)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：作为 Mathf.Abs(float) 的参数、HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3)。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。</param>
    /// <param name="axis">中文含义：轴。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。</param>
    private int WalkDirection(VillagerController character, Vector3 direction, Vector3 axis)
    {
        // 接缝处的路径可能横向绕柱，以当前终点楼层判断进出，目标反向时立即更新。
        if (character.Agent != null && character.Agent.HasDestination)
        {
            // endHeight（结束高度）：使用位置：作为 Mathf.Abs(float) 的参数、HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3)。
float endHeight = character.Agent.Destination.y;
            if (endHeight >= traversal.UpperExit.y - .4f)
            {
                return 1;
            }

            if (endHeight <= traversal.LowerEntry.y + .4f)
            {
                return -1;
            }

            if (Mathf.Abs(endHeight - character.transform.position.y) > .3f)
            {
                if (endHeight > character.transform.position.y)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
        }

        if (Vector3.Dot(direction, axis) >= 0)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>StairAllowFrontEnter 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController)、HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float)、HouseThreeRegionNavigation.CanNormalWalk(VillagerController)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、List&lt;VillagerController&gt;.Add(VillagerController) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController)、HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float)、HouseThreeRegionNavigation.CanNormalWalk(VillagerController)、HouseThreeRegionNavigation.ReleaseCharacter(VillagerController)、HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3)、HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float)、List&lt;VillagerController&gt;.Add(VillagerController) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, int&gt;.Remove(VillagerController) 的参数、作为 Dictionary&lt;VillagerController, int&gt;.this[VillagerController] 的参数、作为 HouseThreeRegionNavigation.CanNormalWalk(VillagerController) 的参数 等共 8 处。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的参数、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。</param>
    public bool StairAllowFrontEnter(VillagerController enemy, Vector3 direction)
    {
        // i（当前遍历索引）：使用位置：作为 List<VillagerController>.this[int] 的参数、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
for (int i = stairUpperMember.Count - 1; i >= 0; i--)
        {
            // other（另一个村民敌人控制器）：使用位置：作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数、作为 HouseThreeRegionNavigation.CanNormalWalk(VillagerController) 的参数、作为 HouseThreeRegionNavigation.ReleaseCharacter(VillagerController) 的参数 等共 6 处。
var other = stairUpperMember[i];
            // atTread（是否所在踏面）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool atTread = other && traversal.TryResolveStairHeight(other.transform.position, out float /* 中文含义：高度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ height) && Mathf.Abs(other.transform.position.y - height) <= .45f;
            // atSeam（是否所在接缝）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool atSeam = other && (ApproachEndPoint(other.transform.position, traversal.UpperExit, 1.35f) || ApproachEndPoint(other.transform.position, traversal.LowerEntry, 1.15f));
            if (!CanNormalWalk(other) || (!atTread && !atSeam))
            {
                ReleaseCharacter(other);
            }
        }

        // positionValue（位置值）：使用位置：作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数、作为 HouseThreeTraversalGeometry.TryResolveStairHeight(Vector3, out float) 的参数、作为 Mathf.Abs(float) 的参数 等共 4 处。
var positionValue = enemy.transform.position;
        if (!CanNormalWalk(enemy))
        {
            ReleaseCharacter(enemy);
            return true;
        }

        if (direction.sqrMagnitude < .000001f)
        {
            return true;
        }

        // axis（轴）：使用位置：作为 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数。
var axis = Vector3.ProjectOnPlane(traversal.UpperExit - traversal.LowerEntry, Vector3.up).normalized;
        // progress（进度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
float progress = Vector3.Dot(positionValue - traversal.LowerEntry, axis);
        // length（长度）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
float length = Vector3.Dot(traversal.UpperExit - traversal.LowerEntry, axis);
        // directionIndex（方向索引）：保存 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
int directionIndex = WalkDirection(enemy, direction, axis);
        // hasEnter（是否具有进入）：保存 List<VillagerController>.Contains(VillagerController) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool hasEnter = stairUpperMember.Contains(enemy);
        // upperEndWaitingRange（上部结束等待中范围）：使用位置：作为 HouseThreeRegionNavigation.ApproachEndPoint(Vector3, Vector3, float) 的参数。
float upperEndWaitingRange = Vector3.ProjectOnPlane(traversal.UpperPlatformRelease - traversal.UpperExit, Vector3.up).magnitude + followSpacing;
        // entryPoint（是否入口点）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool entryPoint;
        if (!hasEnter)
        {
            // selectedValue5（分支选出的分支判断）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool selectedValue5;
            if (directionIndex > 0)
            {
                selectedValue5 = ApproachEndPoint(positionValue, traversal.LowerEntry, 1.5f);
            }
            else
            {
                selectedValue5 = ApproachEndPoint(positionValue, traversal.UpperExit, upperEndWaitingRange);
            }

            entryPoint = (selectedValue5);
        }
        else
        {
            entryPoint = false;
        }

        // treadValid（踏面是否有效）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool treadValid = traversal.TryResolveStairHeight(positionValue, out float /* 中文含义：楼梯高度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stairHeight) && Mathf.Abs(positionValue.y - stairHeight) <= .45f;
        if (!entryPoint && !hasEnter && !treadValid)
        {
            ReleaseCharacter(enemy);
            return true;
        }

        // canWalk（是否可以行走）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
bool canWalk = true;
        // other（另一个村民敌人控制器）：每轮从楼梯上部成员取得一个元素，仅代表当前这一项。使用位置：作为 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
foreach (var other in stairUpperMember)
        {
            if (other == enemy)
            {
                continue;
            }

            // 每帧读取实际路径方向，玩家反向移动后不能遗留旧的放行方向。
            // pairDirection（配对方向）：使用位置：作为 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的参数、HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
global::UnityEngine.Vector3 pairDirection;
            if (other.Agent != null)
            {
                pairDirection = other.Agent.desiredDirection;
            }
            else
            {
                pairDirection = Vector3.zero;
            }

            // otherDirection（另一个方向）：保存 HouseThreeRegionNavigation.WalkDirection(VillagerController, Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
int otherDirection = WalkDirection(other, pairDirection, axis);
            if (entryPoint && pairDirection.sqrMagnitude > .0001f && otherDirection != directionIndex)
            {
                canWalk = false;
            }

            // frontDistance（前方距离）：使用位置：HouseThreeRegionNavigation.StairAllowFrontEnter(VillagerController, Vector3)。
float frontDistance = Vector3.Dot(other.transform.position - positionValue, axis) * directionIndex;
            if (otherDirection == directionIndex && frontDistance > .01f && frontDistance < followSpacing)
            {
                canWalk = false;
            }
        }

        if (!canWalk)
        {
            if (entryPoint)
            {
                yieldRowFrame[enemy] = Time.frameCount;
            }

            return false;
        }

        yieldRowFrame.Remove(enemy);
        if (treadValid && !hasEnter && progress > .35f && progress < length - .35f)
        {
            stairUpperMember.Add(enemy);
        }

        return true;
    }
}
