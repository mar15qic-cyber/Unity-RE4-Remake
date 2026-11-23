using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

/// <summary>
/// 门交互控制器 — 实现 IInteractable 接口
/// 挂载到 DoorRoot，通过 Animator 状态机驱动门叶围绕世界竖直轴旋转
/// 动画片段驱动 doorOpenProgress（0=关闭，1=完全打开），脚本读取该值并应用到 DoorHingePivot 旋转
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider))]
public partial class DoorController : MonoBehaviour, IInteractable
{
    /// <summary>中文含义：场景门集合。只读字段，初始化后不由普通写入口替换。使用位置：DoorController.CopyActiveSceneDoors(List&lt;DoorController&gt;)、DoorController.IsInsideOpenPassage(Vector3, float)、DoorController.OnDisable() 等共 8 处。</summary>
    private static readonly List<DoorController> sceneDoors = new();
    /// <summary>中文含义：门叶铰链轴节点，旋转此节点来开关门。使用位置：作为 Quaternion.Inverse(Quaternion) 的参数、DoorController.CheckDirectionClear(int)、DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int) 等共 7 处。</summary>
    [Header("引用")]
    [Tooltip("门叶铰链轴节点，旋转此节点来开关门")]
    [SerializeField]
    private Transform hingePivot;
    /// <summary>中文含义：门叶物理碰撞体（随门旋转，关闭时阻挡通行）。使用位置：DoorController.CheckDirectionClear(int)、DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.EnsurePassageGeometry() 等共 5 处。</summary>
    [Tooltip("门叶物理碰撞体（随门旋转，关闭时阻挡通行）")]
    [SerializeField]
    private Collider physicalCollider;
    /// <summary>中文含义：交互触发器。交互触发碰撞体（供 InteractionSystem 计算距离）。</summary>
    [Tooltip("交互触发碰撞体（供 InteractionSystem 计算距离）")]
    [SerializeField]
    private Collider interactTrigger;
    /// <summary>中文含义：开门角度（度）。使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数、DoorController.HandleAnimationEnd()、DoorController.Update()。</summary>
    [Header("动画参数")]
    [Tooltip("开门角度（度）")]
    [SerializeField]
    private float openAngle = 95f;
    /// <summary>中文含义：开门动画时长（秒）。</summary>
    [Tooltip("开门动画时长（秒）")]
    [SerializeField]
    private float openDuration = 1.0f;
    /// <summary>中文含义：关门动画时长（秒）。</summary>
    [Tooltip("关门动画时长（秒）")]
    [SerializeField]
    private float closeDuration = 0.85f;
    /// <summary>中文含义：打开速度倍率。开门速度倍率（1=正常，ChapelDoor 用 0.83 实现更慢动作）。</summary>
    [Tooltip("开门速度倍率（1=正常，ChapelDoor 用 0.83 实现更慢动作）")]
    [SerializeField]
    private float openSpeedMultiplier = 1f;
    /// <summary>中文含义：交互有效距离（米）。使用位置：DoorController.InteractRange.get。</summary>
    [Header("交互")]
    [Tooltip("交互有效距离（米）")]
    [SerializeField, Min(0.25f)]
    private float interactRange = 1.2f;
    [SerializeField, Tooltip("关闭时允许里昂向门叶走动并推开，开启后仍可手动关门。")]
    private bool openOnPlayerContact;
    /// <summary>中文含义：智能路线侧向留白。直线路线穿过门洞时允许偏离门洞中心的额外宽度，避免把整扇门的无限平面当作通道。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("敌人门洞路由")]
    [Tooltip("直线路线穿过门洞时允许偏离门洞中心的额外宽度，避免把整扇门的无限平面当作通道。")]
    [SerializeField, Min(0f)]
    private float aiRouteLateralPadding = 0.35f;
    /// <summary>中文含义：验证门两侧导航区域时，取样点离门洞中心的距离。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("验证门两侧导航区域时，取样点离门洞中心的距离。")]
    [SerializeField, Min(0.1f)]
    private float aiRouteSideProbeDistance = 0.85f;
    /// <summary>中文含义：构建追击区域时，从寻路图中切除门洞节点的法线深度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("构建追击区域时，从寻路图中切除门洞节点的法线深度。")]
    [SerializeField, Min(0.25f)]
    private float topologyCutDepth = 1.1f;
    /// <summary>中文含义：构建追击区域时，在门洞有效宽度外增加的切口宽度。使用位置：DoorController.TopologyCutPadding.get。</summary>
    [Tooltip("构建追击区域时，在门洞有效宽度外增加的切口宽度。")]
    [SerializeField, Min(0.1f)]
    private float topologyCutPadding = 0.45f;
    /// <summary>中文含义：门洞两侧区域节点相对门洞中心允许的高度差。使用位置：DoorController.TopologyVerticalTolerance.get。</summary>
    [Tooltip("门洞两侧区域节点相对门洞中心允许的高度差。")]
    [SerializeField, Min(0.5f)]
    private float topologyVerticalTolerance = 2.2f;
    /// <summary>中文含义：是否仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。使用位置：DoorController.DetermineOpenDirection()。</summary>
    [Header("开门方向")]
    [Tooltip("仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。")]
    [SerializeField]
    private bool useAutomaticOpenDirection;
    /// <summary>中文含义：打开村庄建筑木门时播放的音效事件。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、DoorController.PlayOpenAudio()。</summary>
    [Header("音效")]
    [Tooltip("打开村庄建筑木门时播放的音效事件")]
    [SerializeField]
    private AudioEventData openAudioEvent;
    [SerializeField, Tooltip("门实际关闭时播放的空间音效")]
    private AudioEventData closeAudioEvent;
    /// <summary>中文含义：门开合进度，0=关闭 1=完全打开。使用位置：DoorController.HandleAnimationEnd()、DoorController.Update()。</summary>
    [Header("运行时（由 AnimationClip 驱动，请勿手动修改）")]
    [Tooltip("门开合进度，0=关闭 1=完全打开")]
    [SerializeField]
    private float doorOpenProgress = 0f;
    /// <summary>中文含义：占用。使用位置：DoorController.OnDisable()、DoorController.OnEnable()、DoorController.ReleaseTraversal(TraversalToken, ReleaseReason) 等共 5 处。</summary>
    private DoorwayTraversalOccupancy occupancy;
    /// <summary>中文含义：通行占用。使用位置：作为 RequestContext.RequestContext(string, ObjectIdentity, ObjectIdentity, long) 的参数、VillagerController.CleanupPursuitIngressWaiters()、VillagerController.IsPursuitIngressDirectionAvailable(DoorController, int) 等共 10 处。</summary>
    internal DoorwayTraversalOccupancy TraversalOccupancy => occupancy;

    /// <summary>TraversalValid 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.TraversalValid(TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorwayTraversalOccupancy.TraversalValid(TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 DoorwayTraversalOccupancy.TraversalValid(TraversalToken) 的参数。</param>
    public bool TraversalValid(GameArchitecture.TraversalToken token) => isActiveAndEnabled && occupancy != null && occupancy.TraversalValid(token);
    /// <summary>ReleaseTraversal 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UnregisterPursuitIngressWaiter()；下游（含内部回调体） DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">中文含义：凭证。使用位置：作为 DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason) 的参数。</param>
    /// <param name="reason">中文含义：原因。使用位置：作为 DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason) 的参数。</param>
    public bool ReleaseTraversal(GameArchitecture.TraversalToken token, GameArchitecture.ReleaseReason reason) => occupancy != null && occupancy.ReleaseTraversal(token, reason);
    /// <summary>中文含义：动画控制器。使用位置：DoorController.Awake()、DoorController.Interact()、DoorController.SetOpen(bool) 等共 6 处。</summary>
    private Animator animator;
    /// <summary>中文含义：初始铰链世界旋转。使用位置：DoorController.CheckDirectionClear(int)、DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)、DoorController.HandleAnimationEnd() 等共 5 处。</summary>
    private Quaternion initialHingeWorldRotation;
    /// <summary>中文含义：打开方向。使用位置：作为 Animator.SetInteger(int, int) 的参数、作为 Debug.Log(object, Object) 的参数、作为 DoorController.CheckDirectionClear(int) 的参数 等共 6 处。</summary>
    private int openDirection = 1;
    /// <summary>中文含义：是否打开。使用位置：作为 Animator.SetBool(int, bool) 的参数、作为 DoorController.SetOpen(bool) 的参数、DoorController.GetPromptText() 等共 12 处。</summary>
    private bool isOpen;
    /// <summary>中文含义：是否动画播放。使用位置：DoorController.CanInteract(Transform)、DoorController.Interact()、DoorController.IsPassageReady.get 等共 6 处。</summary>
    private bool isAnimating;
    /// <summary>中文含义：打开音频句柄。使用位置：作为 AudioManager.Stop(AudioPlayHandle) 的参数、DoorController.PlayOpenAudio()。</summary>
    private AudioPlayHandle openAudioHandle;
    /// <summary>中文含义：通道中心。使用位置：DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.EnsurePassageGeometry()、DoorController.PassageCenter.get。</summary>
    private Vector3 passageCenter;
    /// <summary>中文含义：通道常规。使用位置：作为 DoorController.FlattenDirection(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 5 处。</summary>
    private Vector3 passageNormal;
    /// <summary>中文含义：通道切线。使用位置：作为 DoorController.FlattenDirection(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.Project(Vector3, Vector3) 的参数 等共 5 处。</summary>
    private Vector3 passageTangent;
    /// <summary>中文含义：通道一半宽度。使用位置：DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.EnsurePassageGeometry()、DoorController.PassageHalfWidth.get。</summary>
    private float passageHalfWidth;
    /// <summary>中文含义：通道几何是否就绪。使用位置：DoorController.EnsurePassageGeometry()。</summary>
    private bool passageGeometryReady;
    /// <summary>中文含义：是否打开哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static readonly int IsOpenHash = Animator.StringToHash("isOpen");
    /// <summary>中文含义：打开方向哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetInteger(int, int) 的参数。</summary>
    private static readonly int OpenDirHash = Animator.StringToHash("openDirection");
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => interactRange;
    /// <summary>中文含义：是否打开。使用位置：VillagerController.ShouldDeferCurrentIngressDoorBlocker(Collider)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</summary>
    public bool IsOpen => isOpen || siegeBroken;
    /// <summary>中文含义：是否通道就绪。使用位置：VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</summary>
    public bool IsPassageReady => siegeBroken || !siegeLatched && isOpen && !isAnimating;

    /// <summary>中文含义：通道中心。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 DoorwayTraversalOccupancy.HorizontalDistance(Vector3, Vector3) 的参数、作为 Mathf.Abs(float) 的参数 等共 27 处。</summary>
    public Vector3 PassageCenter
    {
        get
        {
            EnsurePassageGeometry();
            return passageCenter;
        }
    }

    /// <summary>中文含义：通道常规。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float) 等共 11 处。</summary>
    public Vector3 PassageNormal
    {
        get
        {
            EnsurePassageGeometry();
            return passageNormal;
        }
    }

    /// <summary>中文含义：通道切线。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)。</summary>
    public Vector3 PassageTangent
    {
        get
        {
            EnsurePassageGeometry();
            return passageTangent;
        }
    }

    /// <summary>中文含义：通道一半宽度。使用位置：作为 Mathf.Max(float, float) 的参数、DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)、PursuitNavigationTopology.FindDoorSideRegion(DoorController, int) 等共 7 处。</summary>
    public float PassageHalfWidth
    {
        get
        {
            EnsurePassageGeometry();
            return passageHalfWidth;
        }
    }

    /// <summary>中文含义：拓扑切口深度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float TopologyCutDepth => Mathf.Max(1.1f, topologyCutDepth);
    /// <summary>中文含义：拓扑切口留白。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float TopologyCutPadding => topologyCutPadding;
    /// <summary>中文含义：拓扑竖直容差。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    public float TopologyVerticalTolerance => topologyVerticalTolerance;

    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorwayTraversalOccupancy.DoorwayTraversalOccupancy(DoorController)、DoorwayTraversalOccupancy.OpenRelease()、InteractionSystem.Register(IInteractable)、List&lt;DoorController&gt;.Add(DoorController)、List&lt;DoorController&gt;.Contains(DoorController)、PursuitNavigationTopology.Invalidate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (occupancy == null)
        {
            occupancy = new DoorwayTraversalOccupancy(this);
        }

        occupancy.OpenRelease();
        if (!sceneDoors.Contains(this))
        {
            sceneDoors.Add(this);
        }

        PursuitNavigationTopology.Invalidate();
        InteractionSystem.Register(this);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorwayTraversalOccupancy.ClearValue()、InteractionSystem.Unregister(IInteractable)、List&lt;DoorController&gt;.Remove(DoorController)、object.ReferenceEquals(object, object)、PursuitNavigationTopology.Invalidate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        // receiverValue1（判空前缓存的占用）：从占用读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、DoorController.OnDisable()。
global::DoorwayTraversalOccupancy receiverValue1 = occupancy;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.ClearValue();
        }

        sceneDoors.Remove(this);
        PursuitNavigationTopology.Invalidate();
        InteractionSystem.Unregister(this);
    }

    /// <summary>CopyActiveSceneDoors 的业务处理入口；本方法直接依赖 List&lt;DoorController&gt;.Add(DoorController)、List&lt;DoorController&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PursuitNavigationTopology.Build(AstarPath)；下游（含内部回调体） List&lt;DoorController&gt;.Add(DoorController)、List&lt;DoorController&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="results">中文含义：结果集合。使用位置：DoorController.CopyActiveSceneDoors(List&lt;DoorController&gt;)。</param>
    public static void CopyActiveSceneDoors(List<DoorController> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        // index（当前元素索引）：使用位置：作为 List<DoorController>.this[int] 的参数、DoorController.CopyActiveSceneDoors(List<DoorController>)。
for (int index = 0; index < sceneDoors.Count; index++)
        {
            // door（门）：使用位置：作为 List<DoorController>.Add(DoorController) 的参数、DoorController.CopyActiveSceneDoors(List<DoorController>)。
DoorController door = sceneDoors[index];
            if (door != null && door.isActiveAndEnabled)
            {
                results.Add(door);
            }
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.SetBool(int, bool)、Animator.SetInteger(int, int)、Debug.LogError(object, Object)、DoorController.DetermineOpenDirection()、DoorController.EnsurePassageGeometry()、DoorController.SetPhysicalColliderBlocking()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (hingePivot == null)
        {
            Debug.LogError($"[DoorController] {gameObject.name} 未绑定 hingePivot", this);
            return;
        }

        initialHingeWorldRotation = hingePivot.rotation;
        SetPhysicalColliderBlocking();
        EnsurePassageGeometry();
        DetermineOpenDirection();
        if (animator != null)
        {
            animator.SetBool(IsOpenHash, false);
            animator.SetInteger(OpenDirHash, openDirection);
        }
    }

    /// <summary>CanInteract 的业务处理入口；本方法直接依赖 DoorController.IsPlayerBlocked()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorController.IsPlayerBlocked()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。</param>
    public bool CanInteract(Transform interactor)
    {
        if (siegeLatched || siegeBroken || isAnimating || (openOnPlayerContact && !isOpen))
        {
            return false;
        }

        if (IsPlayerBlocked())
        {
            return false;
        }

        return true;
    }

    /// <summary>Interact 的业务处理入口；本方法直接依赖 DoorController.SetOpen(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorController.SetOpen(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Interact()
    {
        if (siegeLatched || siegeBroken || isAnimating)
        {
            return;
        }

        if (animator == null)
        {
            return;
        }

        SetOpen(!isOpen);
    }

    /// <summary>GetPromptText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetPromptText()
    {
        if (isOpen)
        {
            return "关门";
        }
        else
        {
            return "开门";
        }
    }

    private void FixedUpdate()
    {
        if (siegeLatched || siegeBroken || !openOnPlayerContact || isOpen || isAnimating || animator == null
            || physicalCollider == null || !physicalCollider.enabled || physicalCollider.isTrigger)
        {
            return;
        }
        var leon = LeonController.Instance;
        if (leon == null || (leon.transform.position - PassageCenter).sqrMagnitude > 4f
            || IsPlayerBlocked() || !leon.CanAct() || !leon.IsGrounded
            || !VillageEntranceGate.TryReadPlayerPush(leon, out Vector3 movement, out CapsuleCollider capsule))
        {
            return;
        }
        Vector3 center = capsule.bounds.center;
        Vector3 leafPoint = physicalCollider.ClosestPoint(center);
        float distance = Mathf.Min(capsule.bounds.extents.x, capsule.bounds.extents.z) + .1f;
        float side = Vector3.Dot(center - PassageCenter, PassageNormal);
        float intoDoor = Vector3.Dot(movement, PassageNormal);
        if ((leafPoint - center).sqrMagnitude <= distance * distance && intoDoor * Mathf.Sign(side) < -.3f)
        {
            SetOpen(true);
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、AnimatorStateInfo.IsName(string)、DoorController.HandleAnimationEnd()、DoorController.SetPhysicalColliderBlocking()、NavigationCollisionCache.MarkerPoseChange()、Quaternion.AngleAxis(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (siegeLatchPending)
        {
            TryLatchForSiege();
        }
        if (isAnimating)
        {
            NavigationCollisionCache.MarkerPoseChange();
        }

        if (siegeBroken || animator == null || hingePivot == null)
        {
            return;
        }

        // stateInfo（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：DoorController.Update()。
var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // wasAnimating（之前是否动画播放）：使用位置：DoorController.Update()。
bool wasAnimating = isAnimating;
        isAnimating = stateInfo.IsName("Opening") || stateInfo.IsName("Closing");
        if (wasAnimating && !isAnimating)
        {
            HandleAnimationEnd();
        }

        if (isAnimating)
        {
            // angle（角度）：使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。
float angle = doorOpenProgress * openAngle * openDirection;
            hingePivot.rotation = Quaternion.AngleAxis(angle, Vector3.up) * initialHingeWorldRotation;
        }

        SetPhysicalColliderBlocking();
    }

    /// <summary>动画结束时的最终姿态校正</summary>
    /// <remarks>静态调用链：上游 DoorController.Update()；下游（含内部回调体） DoorController.SetPhysicalColliderBlocking()、Quaternion.AngleAxis(float, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleAnimationEnd()
    {
        if (isOpen)
        {
            // angle（角度）：使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。
float angle = openAngle * openDirection;
            hingePivot.rotation = Quaternion.AngleAxis(angle, Vector3.up) * initialHingeWorldRotation;
            doorOpenProgress = 1f;
            SetPhysicalColliderBlocking();
        }
        else
        {
            hingePivot.rotation = initialHingeWorldRotation;
            doorOpenProgress = 0f;
            SetPhysicalColliderBlocking();
        }
    }

    /// <summary>SetPhysicalColliderBlocking 的业务处理入口；本方法直接依赖 NavigationCollisionCache.MarkerPoseChange()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.HandleAnimationEnd()、DoorController.Start()、DoorController.Update()；下游（含内部回调体） NavigationCollisionCache.MarkerPoseChange()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SetPhysicalColliderBlocking()
    {
        NavigationCollisionCache.MarkerPoseChange();
        if (physicalCollider == null)
        {
            return;
        }

        if (!physicalCollider.enabled)
        {
            physicalCollider.enabled = true;
        }

        if (physicalCollider.isTrigger)
        {
            physicalCollider.isTrigger = false;
        }
    }

    /// <summary>
    /// 默认始终向室内开门。旧实现会根据哪一侧碰撞空隙更大而翻转门叶，
    /// 导致部分门向外扫到正在接近的敌群。只有明确关闭室内优先时，才
    /// 使用旧的碰撞空隙选择作为兼容回退。
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.Start()；下游（含内部回调体） Debug.Log(object, Object)、Debug.LogWarning(object, Object)、DoorController.CheckDirectionClear(int)、DoorController.ResolveInteriorOpenDirection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void DetermineOpenDirection()
    {
        if (!useAutomaticOpenDirection)
        {
            openDirection = ResolveInteriorOpenDirection();
            // interiorClear（是否室内清除）：保存 DoorController.CheckDirectionClear(int) 的返回结果，供当前步骤使用。使用位置：DoorController.DetermineOpenDirection()。
bool interiorClear = CheckDirectionClear(openDirection);
            if (!interiorClear)
            {
                Debug.LogWarning($"[DoorController] {gameObject.name} 室内开门方向存在静态碰撞，仍保持向内开门以保护门外追击通道。", this);
            }

            Debug.Log($"[DoorController] {gameObject.name} 开门方向: {openDirection}（统一向室内）", this);
            return;
        }

        // positiveClear（是否正向清除）：保存 DoorController.CheckDirectionClear(int) 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object, Object) 的参数、DoorController.DetermineOpenDirection()。
bool positiveClear = CheckDirectionClear(1);
        // negativeClear（是否负值清除）：保存 DoorController.CheckDirectionClear(int) 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object, Object) 的参数、DoorController.DetermineOpenDirection()。
bool negativeClear = CheckDirectionClear(-1);
        if (positiveClear)
        {
            openDirection = 1;
        }
        else if (negativeClear)
        {
            openDirection = -1;
        }
        else
        {
            openDirection = 1;
        }

        Debug.Log($"[DoorController] {gameObject.name} 开门方向: {openDirection} (正:{positiveClear} 负:{negativeClear})", this);
    }

    /// <summary>ResolveInteriorOpenDirection 的业务处理入口；本方法直接依赖 DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)、DoorController.ResolveInteriorPassageSide()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.DetermineOpenDirection()；下游（含内部回调体） DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)、DoorController.ResolveInteriorPassageSide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private int ResolveInteriorOpenDirection()
    {
        if (physicalCollider is not BoxCollider /* 中文含义：盒形碰撞体。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ boxCollider || hingePivot == null)
        {
            return 1;
        }

        // interiorSide（室内侧边）：保存 DoorController.ResolveInteriorPassageSide() 的返回结果，供当前步骤使用。使用位置：作为 DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int) 的参数。
int interiorSide = ResolveInteriorPassageSide();
        // positiveScore（正向分值）：保存 DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int) 的返回结果，供当前步骤使用。使用位置：DoorController.ResolveInteriorOpenDirection()。
float positiveScore = GetOpenLeafInteriorScore(boxCollider, 1, interiorSide);
        // negativeScore（负值分值）：保存 DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int) 的返回结果，供当前步骤使用。使用位置：DoorController.ResolveInteriorOpenDirection()。
float negativeScore = GetOpenLeafInteriorScore(boxCollider, -1, interiorSide);
        // 两种旋转在特殊模型上可能投影相同，此时保持模型默认正方向。
        if (positiveScore >= negativeScore)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>ResolveInteriorPassageSide 的业务处理入口；本方法直接依赖 Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.ResolveInteriorOpenDirection()；下游（含内部回调体） Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private int ResolveInteriorPassageSide()
    {
        // interiorDirection（室内方向）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.ResolveInteriorPassageSide()。
Vector3 interiorDirection = transform.up;
        // route（路线）：使用位置：DoorController.ResolveInteriorPassageSide()。
HouseThreeTraversalGeometry route = HouseThreeTraversalGeometry.Active;
        if (route != null && route.IsReady && route.entranceDoor == this)
        {
            interiorDirection = route.InteriorGate - route.DoorGate;
        }

        interiorDirection.y = 0f;
        if (interiorDirection.sqrMagnitude <= 0.0001f)
        {
            return 1;
        }

        if (Vector3.Dot(interiorDirection.normalized, PassageNormal) >= 0f)
        {
            return 1;
        }
        else
        {
            return -1;
        }
    }

    /// <summary>GetOpenLeafInteriorScore 的业务处理入口；本方法直接依赖 Quaternion.AngleAxis(float, Vector3)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.ResolveInteriorOpenDirection()；下游（含内部回调体） Quaternion.AngleAxis(float, Vector3)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="boxCollider">中文含义：盒形碰撞体。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。</param>
    /// <param name="interiorSide">中文含义：室内侧边。使用位置：DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。</param>
    private float GetOpenLeafInteriorScore(BoxCollider boxCollider, int direction, int interiorSide)
    {
        // closedCenter（已关闭中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。
Vector3 closedCenter = boxCollider.transform.TransformPoint(boxCollider.center);
        // centerInHinge（中心内部铰链）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。
Vector3 centerInHinge = hingePivot.InverseTransformPoint(closedCenter);
        // openRotation（打开旋转）：使用位置：DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。
Quaternion openRotation = Quaternion.AngleAxis(openAngle * direction, Vector3.up) * initialHingeWorldRotation;
        // openCenter（打开中心）：使用位置：DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。
Vector3 openCenter = hingePivot.position + openRotation * centerInHinge;
        // offset（偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.GetOpenLeafInteriorScore(BoxCollider, int, int)。
Vector3 offset = openCenter - PassageCenter;
        offset.y = 0f;
        return Vector3.Dot(offset, PassageNormal) * interiorSide;
    }

    /// <summary>CheckDirectionClear 的业务处理入口；本方法直接依赖 DoorController.AbsVector(Vector3)、Physics.OverlapBox(Vector3, Vector3, Quaternion)、Quaternion.AngleAxis(float, Vector3)、Quaternion.Inverse(Quaternion)、Transform.InverseTransformPoint(Vector3)、Transform.IsChildOf(Transform)、Transform.TransformPoint(Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.DetermineOpenDirection()；下游（含内部回调体） DoorController.AbsVector(Vector3)、Physics.OverlapBox(Vector3, Vector3, Quaternion)、Quaternion.AngleAxis(float, Vector3)、Quaternion.Inverse(Quaternion)、Transform.InverseTransformPoint(Vector3)、Transform.IsChildOf(Transform)、Transform.TransformPoint(Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。</param>
    bool CheckDirectionClear(int direction)
    {
        if (physicalCollider == null || hingePivot == null)
        {
            return true;
        }

        // boxCollider（盒形碰撞体）：使用位置：作为 DoorController.AbsVector(Vector3) 的参数、作为 Transform.TransformPoint(Vector3) 的参数、作为 Vector3.Scale(Vector3, Vector3) 的参数 等共 4 处。
BoxCollider boxCollider = physicalCollider as BoxCollider;
        if (boxCollider == null)
        {
            return true;
        }

        // closedCenter（已关闭中心）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。
Vector3 closedCenter = boxCollider.transform.TransformPoint(boxCollider.center);
        // centerInHinge（中心内部铰链）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：DoorController.CheckDirectionClear(int)。
Vector3 centerInHinge = hingePivot.InverseTransformPoint(closedCenter);
        // colliderRotationInHinge（碰撞体旋转内部铰链）：使用位置：DoorController.CheckDirectionClear(int)。
Quaternion colliderRotationInHinge = Quaternion.Inverse(hingePivot.rotation) * boxCollider.transform.rotation;
        // testHingeRotation（验证铰链旋转）：使用位置：DoorController.CheckDirectionClear(int)。
Quaternion testHingeRotation = Quaternion.AngleAxis(openAngle * direction, Vector3.up) * initialHingeWorldRotation;
        // checkCenter（检查中心）：使用位置：作为 Physics.OverlapBox(Vector3, Vector3, Quaternion) 的参数。
Vector3 checkCenter = hingePivot.position + testHingeRotation * centerInHinge;
        // checkRotation（检查旋转）：使用位置：作为 Physics.OverlapBox(Vector3, Vector3, Quaternion) 的参数。
Quaternion checkRotation = testHingeRotation * colliderRotationInHinge;
        // halfExtents（一半半尺寸）：保存 Vector3.Scale(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Physics.OverlapBox(Vector3, Vector3, Quaternion) 的参数。
Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, AbsVector(boxCollider.transform.lossyScale));
        // colWasEnabled（列之前是否启用）：使用位置：DoorController.CheckDirectionClear(int)。
bool colWasEnabled = physicalCollider.enabled;
        physicalCollider.enabled = false;
        // overlaps（重叠结果）：保存 Physics.OverlapBox(Vector3, Vector3, Quaternion) 的返回结果，供当前步骤使用。使用位置：DoorController.CheckDirectionClear(int)。
Collider[] overlaps = Physics.OverlapBox(checkCenter, halfExtents, checkRotation);
        physicalCollider.enabled = colWasEnabled;
        // col（列）：每轮从重叠结果取得一个元素，仅代表当前这一项。使用位置：DoorController.CheckDirectionClear(int)。
foreach (var col in overlaps)
        {
            if (col.isTrigger)
            {
                continue;
            }

            if (col.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>AbsVector 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.CheckDirectionClear(int)；下游（含内部回调体） Mathf.Abs(float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">中文含义：值。使用位置：作为 Mathf.Abs(float) 的参数。</param>
    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    /// <summary>检查玩家是否处于无法交互的状态</summary>
    /// <remarks>静态调用链：上游 DoorController.CanInteract(Transform)；下游（含内部回调体） Component.GetComponent&lt;LeonAnimRig&gt;()、GameUIManager.CanPlayerAct()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool IsPlayerBlocked()
    {
        if (GameUIManager.HasInstance && !GameUIManager.Instance.CanPlayerAct())
        {
            return true;
        }

        if (InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
        {
            return true;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return true;
        }

        // player（玩家）：使用位置：DoorController.IsPlayerBlocked()。
var player = LeonController.Instance;
        if (player == null)
        {
            return true;
        }

        // animRig（当前里昂动画绑定组件）：保存 Component.GetComponent<LeonAnimRig>() 的返回结果，供当前步骤使用。使用位置：DoorController.IsPlayerBlocked()。
var animRig = player.GetComponent<LeonAnimRig>();
        if (animRig != null && animRig.IsDead)
        {
            return true;
        }

        return false;
    }

    /// <summary>PlayOpenAudio 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、AudioManager.Play3D(string, Vector3)、AudioManager.Stop(AudioPlayHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.SetOpen(bool)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、AudioManager.Play3D(string, Vector3)、AudioManager.Stop(AudioPlayHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayDoorAudio(bool opening)
    {
        AudioEventData sound = closeAudioEvent;
        if (opening)
        {
            sound = openAudioEvent;
        }

        if (AudioManager.Instance == null || (!opening && sound == null))
        {
            return;
        }

        if (openAudioHandle.IsValid)
        {
            AudioManager.Instance.Stop(openAudioHandle);
        }

        if (sound != null)
        {
            openAudioHandle = AudioManager.Instance.Play3D(sound, transform.position);
            return;
        }

        openAudioHandle = AudioManager.Instance.Play3D(AudioEventNames.VillageDoorOpen, transform.position);
    }

    /// <summary>TryOpenForAI 的业务处理入口；本方法直接依赖 DoorController.SetOpen(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)、VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorController.SetOpen(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="opener">中文含义：开门者。</param>
    public bool TryOpenForAI(Transform opener)
    {
        if (siegeLatched || siegeBroken)
        {
            return false;
        }

        if (isOpen || isAnimating || animator == null)
        {
            return false;
        }

        SetOpen(true);
        return true;
    }

    /// <summary>TryOpenNearestForAI 的业务处理入口；本方法直接依赖 DoorController.TryOpenForAI(Transform)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.TryOpenDoorOnPath(Vector3)；下游（含内部回调体） DoorController.TryOpenForAI(Transform)、Vector3.Angle(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。</param>
    /// <param name="forward">中文含义：前向。使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。</param>
    /// <param name="radius">中文含义：半径。使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。</param>
    /// <param name="facingAngle">中文含义：朝向角度。使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。</param>
    /// <param name="opener">中文含义：开门者。使用位置：作为 DoorController.TryOpenForAI(Transform) 的参数、DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。</param>
    public static bool TryOpenNearestForAI(Vector3 position, Vector3 forward, float radius, float facingAngle, Transform opener)
    {
        if (radius <= 0f)
        {
            return false;
        }

        // bestDoor（最佳门）：使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
DoorController bestDoor = null;
        // bestDistanceSqr（最佳距离平方）：使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
float bestDistanceSqr = radius * radius;
        // flatForward（平面前向）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude <= 0.0001f && opener != null)
        {
            flatForward = opener.forward;
        }

        flatForward.y = 0f;
        if (flatForward.sqrMagnitude > 0.0001f)
        {
            flatForward.Normalize();
        }

        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled || door.isOpen || door.isAnimating)
            {
                continue;
            }

            // offset（偏移量）：使用位置：作为 Vector3.Angle(Vector3, Vector3) 的参数、DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
Vector3 offset = door.PassageCenter - position;
            offset.y = 0f;
            // distanceSqr（距离平方）：使用位置：DoorController.TryOpenNearestForAI(Vector3, Vector3, float, float, Transform)。
float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > bestDistanceSqr)
            {
                continue;
            }

            if (flatForward.sqrMagnitude > 0.0001f && offset.sqrMagnitude > 0.0001f && Vector3.Angle(flatForward, offset.normalized) > facingAngle)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestDoor = door;
        }

        return bestDoor != null && bestDoor.TryOpenForAI(opener);
    }

    /// <summary>IsInsideOpenPassage 的业务处理入口；本方法直接依赖 DoorController.ContainsPassageGeometry(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.HasContinuousNavigationGround(Vector3)、VillagerController.IsInsideTraversalBottleneck(Vector3)、VillagerController.IsNavigationTorsoClear(Vector3)、VillagerController.RequiresDirectNavigationTraversalToPlayer()、VillagerController.RequiresNavigationTraversalToPlayer() 等共 8 处；下游（含内部回调体） DoorController.ContainsPassageGeometry(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 DoorController.ContainsPassageGeometry(Vector3, float) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 DoorController.ContainsPassageGeometry(Vector3, float) 的参数。</param>
    public static bool IsInsideOpenPassage(Vector3 point, float radius)
    {
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.IsInsideOpenPassage(Vector3, float)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled || !door.isOpen)
            {
                continue;
            }

            if (door.ContainsPassageGeometry(point, radius))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 目标位于门的另一侧时，返回必须先经过的门洞中心。该方法只负责
    /// 追击阶段的路由，不改变门的开关状态；实际靠近后仍由敌人开门逻辑处理。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController) 的参数、DoorController.TryGetPassageTowardDestination(Vector3, Vector3, out Vector3)。</param>
    /// <param name="passageDestination">中文含义：通道目的地。输出参数，由本入口写入并交给调用方。使用位置：DoorController.TryGetPassageTowardDestination(Vector3, Vector3, out Vector3)。</param>
    public static bool TryGetPassageTowardDestination(Vector3 source, Vector3 destination, out Vector3 passageDestination)
    {
        passageDestination = destination;
        if (!TryFindPassageTowardDestination(source, destination, out DoorController /* 中文含义：通道门。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ passageDoor))
        {
            return false;
        }

        passageDestination = passageDoor.PassageCenter;
        return true;
    }

    /// <summary>
    /// 查找真正连接敌人与目标的门洞。门的法线只用于判断两侧；它本身
    /// 不是无限长的分界面。只有直线路线穿过有限门洞，或门两侧分别与
    /// 敌人、目标所在的不同导航区域相连，才允许将追击改为穿门阶段。
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.TryGetPassageTowardDestination(Vector3, Vector3, out Vector3)、EnemyAIManager.IsFormationSlotOnPlayerSide(Vector3, Vector3)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数。</param>
    /// <param name="passageDoor">中文含义：通道门。输出参数，由本入口写入并交给调用方。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数。</param>
    public static bool TryFindPassageTowardDestination(Vector3 source, Vector3 destination, out DoorController passageDoor)
    {
        return TryFindPassageTowardDestination(source, destination, out passageDoor, out _);
    }

    /// <summary>
    /// 返回区域拓扑路线上的第一扇门，并同时返回来源区域位于门法线的
    /// 哪一侧。只要区域拓扑能够识别两端，就不再退回直线穿门猜测。
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController)、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float)、Mathf.Abs(float)、Mathf.Sign(float)、PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float) 的参数、作为 PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int) 的参数、作为 PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int) 的参数 等共 5 处。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float) 的参数、作为 PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int) 的参数、作为 PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int) 的参数 等共 5 处。</param>
    /// <param name="passageDoor">中文含义：通道门。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。</param>
    /// <param name="sourceSideSign">中文含义：来源侧边符号。输出参数，由本入口写入并交给调用方。使用位置：作为 PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int) 的参数、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。</param>
    public static bool TryFindPassageTowardDestination(Vector3 source, Vector3 destination, out DoorController passageDoor, out int sourceSideSign)
    {
        passageDoor = null;
        sourceSideSign = 0;
        if (PursuitNavigationTopology.TryResolveRegions(source, destination, out int /* 中文含义：来源区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceRegion, out int /* 中文含义：目的地区域。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ destinationRegion))
        {
            if (sourceRegion == destinationRegion)
            {
                return false;
            }

            return PursuitNavigationTopology.TryFindFirstPortal(source, destination, out passageDoor, out sourceSideSign);
        }

        // 寻路图尚未完成扫描时保留旧判定作为启动阶段兼容回退。
        // 一旦区域拓扑可用，这段逻辑不再参与普通追击决策。
        // bestDoor（最佳门）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
DoorController bestDoor = null;
        // bestTargetDistanceSqr（最佳目标距离平方）：使用位置：作为 Mathf.Abs(float) 的参数、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
float bestTargetDistanceSqr = float.PositiveInfinity;
        // bestSourceDistanceSqr（最佳来源距离平方）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
float bestSourceDistanceSqr = float.PositiveInfinity;
        // sideTolerance（侧边容差）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
const float sideTolerance = 0.12f;
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // center（中心）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
Vector3 center = door.PassageCenter;
            // normal（表面法线）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 normal = door.PassageNormal;
            // sourceSide（来源侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数。
float sourceSide = Vector3.Dot(source - center, normal);
            // destinationSide（目的地侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float) 的参数、作为 Mathf.Abs(float) 的参数、作为 Mathf.Sign(float) 的参数。
float destinationSide = Vector3.Dot(destination - center, normal);
            if (Mathf.Abs(sourceSide) <= sideTolerance || Mathf.Abs(destinationSide) <= sideTolerance || Mathf.Sign(sourceSide) == Mathf.Sign(destinationSide))
            {
                continue;
            }

            if (!door.IsPassageRequiredForRoute(source, destination, sourceSide, destinationSide))
            {
                continue;
            }

            // destinationOffset（目的地偏移量）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
Vector3 destinationOffset = destination - center;
            destinationOffset.y = 0f;
            // targetDistanceSqr（目标距离平方）：使用位置：作为 Mathf.Abs(float) 的参数、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
float targetDistanceSqr = destinationOffset.sqrMagnitude;
            // sourceOffset（来源偏移量）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
Vector3 sourceOffset = source - center;
            sourceOffset.y = 0f;
            // sourceDistanceSqr（来源距离平方）：使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
float sourceDistanceSqr = sourceOffset.sqrMagnitude;
            if (targetDistanceSqr > bestTargetDistanceSqr + 0.01f || (Mathf.Abs(targetDistanceSqr - bestTargetDistanceSqr) <= 0.01f && sourceDistanceSqr >= bestSourceDistanceSqr))
            {
                continue;
            }

            // 跨层路线的目标是固定的楼梯入口。门的选择必须优先贴近
            // 该入口，不能因为敌人离另一扇门较近就把它带到错误建筑。
            bestTargetDistanceSqr = targetDistanceSqr;
            bestSourceDistanceSqr = sourceDistanceSqr;
            bestDoor = door;
        }

        passageDoor = bestDoor;
        if (passageDoor != null)
        {
            // side（侧边）：保存 Vector3.Dot(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
float side = Vector3.Dot(source - passageDoor.PassageCenter, passageDoor.PassageNormal);
            if (side >= 0f)
            {
                sourceSideSign = 1;
            }
            else
            {
                sourceSideSign = -1;
            }
        }

        return passageDoor != null;
    }

    /// <summary>
    /// 验证这扇门是否真是当前追击路线上的有限通道。这样屋外两个点即使
    /// 恰好落在门法线两侧，也不会被错误吸附到无关房屋的门前。
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)；下游（含内部回调体） DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)、DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float) 的参数、作为 DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float) 的参数、作为 DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float) 的参数。</param>
    /// <param name="sourceSide">中文含义：来源侧边。使用位置：作为 DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float) 的参数、作为 DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float) 的参数。</param>
    /// <param name="destinationSide">中文含义：目的地侧边。使用位置：作为 DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float) 的参数。</param>
    private bool IsPassageRequiredForRoute(Vector3 source, Vector3 destination, float sourceSide, float destinationSide)
    {
        if (DoesRouteCrossFinitePassage(source, destination, sourceSide, destinationSide))
        {
            return true;
        }

        return ConnectsSeparateNavigationAreas(source, destination, sourceSide);
    }

    /// <summary>DoesRouteCrossFinitePassage 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float)；下游（含内部回调体） Mathf.Abs(float)、Mathf.Clamp01(float)、Mathf.Max(float, float)、Vector3.Dot(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</param>
    /// <param name="sourceSide">中文含义：来源侧边。使用位置：作为 Mathf.Clamp01(float) 的参数、DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。</param>
    /// <param name="destinationSide">中文含义：目的地侧边。使用位置：DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。</param>
    private bool DoesRouteCrossFinitePassage(Vector3 source, Vector3 destination, float sourceSide, float destinationSide)
    {
        // sideDelta（侧边增量）：使用位置：作为 Mathf.Abs(float) 的参数、作为 Mathf.Clamp01(float) 的参数。
float sideDelta = sourceSide - destinationSide;
        if (Mathf.Abs(sideDelta) <= 0.0001f)
        {
            return false;
        }

        // crossingRatio（跨越比例）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
float crossingRatio = Mathf.Clamp01(sourceSide / sideDelta);
        // crossingPoint（跨越点）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。
Vector3 crossingPoint = Vector3.Lerp(source, destination, crossingRatio);
        // offset（偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。
Vector3 offset = crossingPoint - PassageCenter;
        offset.y = 0f;
        // lateralDistance（侧向距离）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：DoorController.DoesRouteCrossFinitePassage(Vector3, Vector3, float, float)。
float lateralDistance = Mathf.Abs(Vector3.Dot(offset, PassageTangent));
        return lateralDistance <= PassageHalfWidth + Mathf.Max(0f, aiRouteLateralPadding);
    }

    /// <summary>ConnectsSeparateNavigationAreas 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Max(float, float)、Mathf.Sign(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Max(float, float)、Mathf.Sign(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数。</param>
    /// <param name="sourceSide">中文含义：来源侧边。使用位置：作为 Mathf.Sign(float) 的参数。</param>
    private bool ConnectsSeparateNavigationAreas(Vector3 source, Vector3 destination, float sourceSide)
    {
        if (AstarPath.active == null)
        {
            return false;
        }

        // sourceInfo（来源信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
NNInfo sourceInfo = AstarPath.active.GetNearest(source, NearestNodeConstraint.Walkable);
        // destinationInfo（目的地信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
NNInfo destinationInfo = AstarPath.active.GetNearest(destination, NearestNodeConstraint.Walkable);
        if (sourceInfo.node == null || destinationInfo.node == null || sourceInfo.node.Area == destinationInfo.node.Area)
        {
            return false;
        }

        // probeDistance（探测距离）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
float probeDistance = Mathf.Max(0.1f, aiRouteSideProbeDistance);
        // sourceDirection（来源方向）：保存 Mathf.Sign(float) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
float sourceDirection = Mathf.Sign(sourceSide);
        // sourceProbe（来源探测）：使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数。
Vector3 sourceProbe = PassageCenter + PassageNormal * sourceDirection * probeDistance;
        // destinationProbe（目的地探测）：使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数。
Vector3 destinationProbe = PassageCenter - PassageNormal * sourceDirection * probeDistance;
        // sourceProbeInfo（来源探测信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
NNInfo sourceProbeInfo = AstarPath.active.GetNearest(sourceProbe, NearestNodeConstraint.Walkable);
        // destinationProbeInfo（目的地探测信息）：保存 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的返回结果，供当前步骤使用。使用位置：DoorController.ConnectsSeparateNavigationAreas(Vector3, Vector3, float)。
NNInfo destinationProbeInfo = AstarPath.active.GetNearest(destinationProbe, NearestNodeConstraint.Walkable);
        return sourceProbeInfo.node != null && destinationProbeInfo.node != null && sourceProbeInfo.node.Area == sourceInfo.node.Area && destinationProbeInfo.node.Area == destinationInfo.node.Area;
    }

    /// <summary>
    /// 返回距指定目标最近的门洞。跨层路线先锁定与楼梯入口同属建筑的门，
    /// 再由敌人判断自己是否位于该门的另一侧，避免远处敌人被分派到别的门。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="destination">中文含义：目的地。使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。</param>
    /// <param name="passageDoor">中文含义：通道门。输出参数，由本入口写入并交给调用方。使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。</param>
    public static bool TryFindNearestPassageToDestination(Vector3 destination, out DoorController passageDoor)
    {
        passageDoor = null;
        // bestDistanceSqr（最佳距离平方）：使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。
float bestDistanceSqr = float.PositiveInfinity;
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // offset（偏移量）：使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。
Vector3 offset = destination - door.PassageCenter;
            offset.y = 0f;
            // distanceSqr（距离平方）：使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。
float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            passageDoor = door;
        }

        return passageDoor != null;
    }

    /// <summary>IsOpenDoorCollider 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;DoorController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ShouldIgnoreMovementCollider(Collider, Vector3, bool)；下游（含内部回调体） Component.GetComponentInParent&lt;DoorController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="collider">中文含义：碰撞体。使用位置：DoorController.IsOpenDoorCollider(Collider)。</param>
    public static bool IsOpenDoorCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        // door（门）：保存 Component.GetComponentInParent<DoorController>() 的返回结果，供当前步骤使用。使用位置：DoorController.IsOpenDoorCollider(Collider)。
DoorController door = collider.GetComponentInParent<DoorController>();
        return door != null && door.isOpen;
    }

    /// <summary>TryResolveOpenPassageDirection 的业务处理入口；本方法直接依赖 DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、Mathf.Max(float, float)、Vector3.Normalize()、Vector3.Project(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirection()；下游（含内部回调体） DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、Mathf.Max(float, float)、Vector3.Normalize()、Vector3.Project(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 DoorController.ContainsPassageGeometry(Vector3, float) 的参数、作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int) 的参数。</param>
    /// <param name="captureDistance">中文含义：捕获距离。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="direction">中文含义：方向。输出参数，由本入口写入并交给调用方。使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。</param>
    public static bool TryResolveOpenPassageDirection(Vector3 position, Vector3 destination, float captureDistance, out Vector3 direction)
    {
        direction = Vector3.zero;
        // 门边转向也必须遵守真实通道路线。屋外追击屋后目标时，不能
        // 因目标落在门的无限平面另一侧，就把刚出门的敌人再次引回屋内。
        if (!TryFindPassageTowardDestination(position, destination, out DoorController /* 中文含义：所需门。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ requiredDoor, out int /* 中文含义：来源侧边。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceSide))
        {
            return false;
        }

        // nearestDoor（最近门）：使用位置：作为 Vector3.Project(Vector3, Vector3) 的参数、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
DoorController nearestDoor = null;
        // nearestDistanceSqr（最近距离平方）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
float nearestDistanceSqr = Mathf.Max(0.25f, captureDistance * captureDistance);
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
foreach (DoorController door in sceneDoors)
        {
            if (door != requiredDoor || door == null || !door.isActiveAndEnabled || !door.isOpen)
            {
                continue;
            }

            // offset（偏移量）：使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
Vector3 offset = door.PassageCenter - position;
            offset.y = 0f;
            // distanceSqr（距离平方）：使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr || !door.ContainsPassageGeometry(position, Mathf.Max(0.8f, captureDistance)))
            {
                continue;
            }

            nearestDistanceSqr = distanceSqr;
            nearestDoor = door;
        }

        if (nearestDoor == null)
        {
            return false;
        }

        // normal（表面法线）：使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
Vector3 normal = nearestDoor.PassageNormal * -sourceSide;
        // toCenter（目标中心）：使用位置：作为 Vector3.Project(Vector3, Vector3) 的参数、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
Vector3 toCenter = nearestDoor.PassageCenter - position;
        toCenter.y = 0f;
        // lateralCorrection（侧向校正量）：保存 Vector3.Project(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
Vector3 lateralCorrection = Vector3.Project(toCenter, nearestDoor.passageTangent);
        direction = normal + lateralCorrection * 1.5f;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = normal;
        }

        direction.Normalize();
        return true;
    }

    /// <summary>ContainsPassageGeometry 的业务处理入口；本方法直接依赖 DoorController.EnsurePassageGeometry()、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)、Vector3.Dot(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.IsInsideOpenPassage(Vector3, float)、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)、StrictAstarGraphBaker.IsNearDoorPassage(Vector3, float)；下游（含内部回调体） DoorController.EnsurePassageGeometry()、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="point">中文含义：点。使用位置：作为 Mathf.Abs(float) 的参数、DoorController.ContainsPassageGeometry(Vector3, float)。</param>
    /// <param name="halfDepth">中文含义：一半深度。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public bool ContainsPassageGeometry(Vector3 point, float halfDepth)
    {
        EnsurePassageGeometry();
        // offset（偏移量）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.ContainsPassageGeometry(Vector3, float)。
Vector3 offset = point - passageCenter;
        // 门洞只约束本高度层，不能把屋顶上的角色当作楼下门洞的通行者。
        // centerHeight（中心高度）：使用位置：作为 Mathf.Abs(float) 的参数、DoorController.ContainsPassageGeometry(Vector3, float)。
float centerHeight = passageCenter.y + 1f;
        // halfHeight（一半高度）：使用位置：DoorController.ContainsPassageGeometry(Vector3, float)。
float halfHeight = 1.25f;
        if (physicalCollider is BoxCollider /* 中文含义：箱体。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ box)
        {
            centerHeight = box.transform.TransformPoint(box.center).y;
            halfHeight = (Mathf.Abs(box.transform.TransformVector(Vector3.right * box.size.x).y) + Mathf.Abs(box.transform.TransformVector(Vector3.up * box.size.y).y) + Mathf.Abs(box.transform.TransformVector(Vector3.forward * box.size.z).y)) * 0.5f;
        }

        if (Mathf.Abs(point.y - centerHeight) > halfHeight + 0.35f)
        {
            return false;
        }

        offset.y = 0f;
        // lateralDistance（侧向距离）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：DoorController.ContainsPassageGeometry(Vector3, float)。
float lateralDistance = Mathf.Abs(Vector3.Dot(offset, passageTangent));
        // depthDistance（深度距离）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：DoorController.ContainsPassageGeometry(Vector3, float)。
float depthDistance = Mathf.Abs(Vector3.Dot(offset, passageNormal));
        return lateralDistance <= passageHalfWidth && depthDistance <= Mathf.Max(0.1f, halfDepth);
    }

    /// <summary>EnsurePassageGeometry 的业务处理入口；本方法直接依赖 DoorController.FlattenDirection(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)、Vector3.Dot(Vector3, Vector3)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.PassageCenter.get、DoorController.PassageHalfWidth.get、DoorController.PassageNormal.get、DoorController.PassageTangent.get 等共 6 处；下游（含内部回调体） DoorController.FlattenDirection(Vector3, Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformPoint(Vector3)、Transform.TransformVector(Vector3)、Vector3.Dot(Vector3, Vector3)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsurePassageGeometry()
    {
        if (passageGeometryReady)
        {
            return;
        }

        passageGeometryReady = true;
        passageCenter = transform.position;
        passageNormal = FlattenDirection(transform.up, Vector3.forward);
        passageTangent = new Vector3(-passageNormal.z, 0f, passageNormal.x);
        passageHalfWidth = 0.55f;
        if (physicalCollider is not BoxCollider /* 中文含义：盒形碰撞体。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ boxCollider)
        {
            return;
        }

        passageCenter = boxCollider.transform.TransformPoint(boxCollider.center);
        passageCenter.y = transform.position.y;
        // axes（坐标轴）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、DoorController.EnsurePassageGeometry()。
Vector3[] axes =
        {
            boxCollider.transform.TransformVector(Vector3.right * boxCollider.size.x),
            boxCollider.transform.TransformVector(Vector3.up * boxCollider.size.y),
            boxCollider.transform.TransformVector(Vector3.forward * boxCollider.size.z)
        };
        // verticalAxis（竖直轴）：使用位置：DoorController.EnsurePassageGeometry()。
int verticalAxis = 0;
        // strongestVertical（最强竖直）：使用位置：DoorController.EnsurePassageGeometry()。
float strongestVertical = -1f;
        // i（当前遍历索引）：使用位置：DoorController.EnsurePassageGeometry()。
for (int i = 0; i < axes.Length; i++)
        {
            // verticalStrength（竖直强度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：DoorController.EnsurePassageGeometry()。
float verticalStrength = Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up));
            if (verticalStrength <= strongestVertical)
            {
                continue;
            }

            strongestVertical = verticalStrength;
            verticalAxis = i;
        }

        // firstHorizontal（首个水平）：使用位置：DoorController.EnsurePassageGeometry()。
Vector3 firstHorizontal = axes[(verticalAxis + 1) % axes.Length];
        // secondHorizontal（第二水平）：使用位置：DoorController.EnsurePassageGeometry()。
Vector3 secondHorizontal = axes[(verticalAxis + 2) % axes.Length];
        firstHorizontal.y = 0f;
        secondHorizontal.y = 0f;
        // widthAxis（宽度轴）：使用位置：作为 DoorController.FlattenDirection(Vector3, Vector3) 的参数、作为 Mathf.Max(float, float) 的参数、DoorController.EnsurePassageGeometry()。
Vector3 widthAxis;
        if (firstHorizontal.sqrMagnitude >= secondHorizontal.sqrMagnitude)
        {
            widthAxis = firstHorizontal;
        }
        else
        {
            widthAxis = secondHorizontal;
        }

        // depthAxis（深度轴）：使用位置：作为 DoorController.FlattenDirection(Vector3, Vector3) 的参数、DoorController.EnsurePassageGeometry()。
Vector3 depthAxis;
        if (firstHorizontal.sqrMagnitude < secondHorizontal.sqrMagnitude)
        {
            depthAxis = firstHorizontal;
        }
        else
        {
            depthAxis = secondHorizontal;
        }

        passageTangent = FlattenDirection(widthAxis, passageTangent);
        passageNormal = FlattenDirection(depthAxis, passageNormal);
        passageHalfWidth = Mathf.Max(0.45f, widthAxis.magnitude * 0.5f + 0.12f);
    }

    /// <summary>FlattenDirection 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DoorController.EnsurePassageGeometry()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：DoorController.FlattenDirection(Vector3, Vector3)。</param>
    /// <param name="fallback">中文含义：后备。使用位置：DoorController.FlattenDirection(Vector3, Vector3)。</param>
    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            fallback.y = 0f;
            direction = fallback;
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }
        else
        {
            return Vector3.forward;
        }
    }

    /// <summary>SetOpen 的业务处理入口；本方法直接依赖 Animator.SetBool(int, bool)、DoorController.PlayOpenAudio()、EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.Interact()、DoorController.TryOpenForAI(Transform)；下游（含内部回调体） Animator.SetBool(int, bool)、DoorController.PlayOpenAudio()、EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="open">中文含义：是否打开。使用位置：DoorController.SetOpen(bool)。</param>
    private void SetOpen(bool open)
    {
        if (siegeLatched || siegeBroken || isOpen == open)
        {
            return;
        }

        isOpen = open;
        animator.SetBool(IsOpenHash, isOpen);
        PlayDoorAudio(open);
        if (isOpen)
        {
            // manager（当前敌人智能管理器）：使用位置：作为 EnemyAIManager.NotifyEnvironmentalDisturbance(Vector3, float) 的参数、DoorController.SetOpen(bool)。
EnemyAIManager manager = EnemyAIManager.Instance;
            if (manager != null)
            {
                manager.NotifyEnvironmentalDisturbance(transform.position, manager.doorDisturbanceRadius);
            }
        }
    }
}
