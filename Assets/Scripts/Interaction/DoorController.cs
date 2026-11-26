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
    /// <summary>场景门集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly List<DoorController> sceneDoors = new();
    /// <summary>门叶铰链轴节点，旋转此节点来开关门。</summary>
    [Header("引用")]
    [Tooltip("门叶铰链轴节点，旋转此节点来开关门")]
    [SerializeField]
    private Transform hingePivot;
    /// <summary>门叶物理碰撞体（随门旋转，关闭时阻挡通行）。</summary>
    [Tooltip("门叶物理碰撞体（随门旋转，关闭时阻挡通行）")]
    [SerializeField]
    private Collider physicalCollider;
    /// <summary>中文含义：交互触发器。交互触发碰撞体（供 InteractionSystem 计算距离）。</summary>
    [Tooltip("交互触发碰撞体（供 InteractionSystem 计算距离）")]
    [SerializeField]
    private Collider interactTrigger;
    /// <summary>开门角度（度）。</summary>
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
    /// <summary>交互有效距离（米）。</summary>
    [Header("交互")]
    [Tooltip("交互有效距离（米）")]
    [SerializeField, Min(0.25f)]
    private float interactRange = 1.2f;
    [SerializeField, Tooltip("关闭时允许里昂向门叶走动并推开，开启后仍可手动关门。")]
    private bool openOnPlayerContact;
    /// <summary>智能路线侧向留白。直线路线穿过门洞时允许偏离门洞中心的额外宽度，避免把整扇门的无限平面当作通道。</summary>
    [Header("敌人门洞路由")]
    [Tooltip("直线路线穿过门洞时允许偏离门洞中心的额外宽度，避免把整扇门的无限平面当作通道。")]
    [SerializeField, Min(0f)]
    private float aiRouteLateralPadding = 0.35f;
    /// <summary>验证门两侧导航区域时，取样点离门洞中心的距离。</summary>
    [Tooltip("验证门两侧导航区域时，取样点离门洞中心的距离。")]
    [SerializeField, Min(0.1f)]
    private float aiRouteSideProbeDistance = 0.85f;
    /// <summary>构建追击区域时，从寻路图中切除门洞节点的法线深度。</summary>
    [Tooltip("构建追击区域时，从寻路图中切除门洞节点的法线深度。")]
    [SerializeField, Min(0.25f)]
    private float topologyCutDepth = 1.1f;
    /// <summary>构建追击区域时，在门洞有效宽度外增加的切口宽度。</summary>
    [Tooltip("构建追击区域时，在门洞有效宽度外增加的切口宽度。")]
    [SerializeField, Min(0.1f)]
    private float topologyCutPadding = 0.45f;
    /// <summary>门洞两侧区域节点相对门洞中心允许的高度差。</summary>
    [Tooltip("门洞两侧区域节点相对门洞中心允许的高度差。")]
    [SerializeField, Min(0.5f)]
    private float topologyVerticalTolerance = 2.2f;
    /// <summary>是否仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。</summary>
    [Header("开门方向")]
    [Tooltip("仅用于兼容旧门逻辑。默认关闭时，门叶始终摆向室内侧。")]
    [SerializeField]
    private bool useAutomaticOpenDirection;
    /// <summary>打开村庄建筑木门时播放的音效事件。</summary>
    [Header("音效")]
    [Tooltip("打开村庄建筑木门时播放的音效事件")]
    [SerializeField]
    private AudioEventData openAudioEvent;
    [SerializeField, Tooltip("门实际关闭时播放的空间音效")]
    private AudioEventData closeAudioEvent;
    /// <summary>门开合进度，0=关闭 1=完全打开。</summary>
    [Header("运行时（由 AnimationClip 驱动，请勿手动修改）")]
    [Tooltip("门开合进度，0=关闭 1=完全打开")]
    [SerializeField]
    private float doorOpenProgress = 0f;
    /// <summary>占用。</summary>
    private DoorwayTraversalOccupancy occupancy;
    /// <summary>通行占用。</summary>
    internal DoorwayTraversalOccupancy TraversalOccupancy => occupancy;

    /// <summary>TraversalValid 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.TraversalValid(TraversalToken)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） DoorwayTraversalOccupancy.TraversalValid(TraversalToken)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    public bool TraversalValid(GameArchitecture.TraversalToken token) => isActiveAndEnabled && occupancy != null && occupancy.TraversalValid(token);
    /// <summary>ReleaseTraversal 的业务处理入口；本方法直接依赖 DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.UnregisterPursuitIngressWaiter()；下游（含内部回调体） DoorwayTraversalOccupancy.ReleaseTraversal(TraversalToken, ReleaseReason)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="token">凭证。</param>
    /// <param name="reason">原因。</param>
    public bool ReleaseTraversal(GameArchitecture.TraversalToken token, GameArchitecture.ReleaseReason reason) => occupancy != null && occupancy.ReleaseTraversal(token, reason);
    /// <summary>动画控制器。</summary>
    private Animator animator;
    /// <summary>初始铰链世界旋转。</summary>
    private Quaternion initialHingeWorldRotation;
    /// <summary>打开方向。</summary>
    private int openDirection = 1;
    /// <summary>是否打开。</summary>
    private bool isOpen;
    /// <summary>是否动画播放。</summary>
    private bool isAnimating;
    /// <summary>打开音频句柄。</summary>
    private AudioPlayHandle openAudioHandle;
    /// <summary>通道中心。</summary>
    private Vector3 passageCenter;
    /// <summary>通道常规。</summary>
    private Vector3 passageNormal;
    /// <summary>通道切线。</summary>
    private Vector3 passageTangent;
    /// <summary>通道一半宽度。</summary>
    private float passageHalfWidth;
    /// <summary>通道几何是否就绪。</summary>
    private bool passageGeometryReady;
    /// <summary>是否打开哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int IsOpenHash = Animator.StringToHash("isOpen");
    /// <summary>打开方向哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int OpenDirHash = Animator.StringToHash("openDirection");
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => interactRange;
    /// <summary>是否打开。</summary>
    public bool IsOpen => isOpen || siegeBroken;
    /// <summary>是否通道就绪。</summary>
    public bool IsPassageReady => siegeBroken || !siegeLatched && isOpen && !isAnimating;

    /// <summary>通道中心。</summary>
    public Vector3 PassageCenter
    {
        get
        {
            EnsurePassageGeometry();
            return passageCenter;
        }
    }

    /// <summary>通道常规。</summary>
    public Vector3 PassageNormal
    {
        get
        {
            EnsurePassageGeometry();
            return passageNormal;
        }
    }

    /// <summary>通道切线。</summary>
    public Vector3 PassageTangent
    {
        get
        {
            EnsurePassageGeometry();
            return passageTangent;
        }
    }

    /// <summary>通道一半宽度。</summary>
    public float PassageHalfWidth
    {
        get
        {
            EnsurePassageGeometry();
            return passageHalfWidth;
        }
    }

    /// <summary>拓扑切口深度。</summary>
    public float TopologyCutDepth => Mathf.Max(1.1f, topologyCutDepth);
    /// <summary>拓扑切口留白。</summary>
    public float TopologyCutPadding => topologyCutPadding;
    /// <summary>拓扑竖直容差。</summary>
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
        // 判空前缓存的占用。
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
    /// <param name="results">结果集合。</param>
    public static void CopyActiveSceneDoors(List<DoorController> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        // 当前元素索引。
for (int index = 0; index < sceneDoors.Count; index++)
        {
            // 门。
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

        // 当前动画状态信息。
var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // 之前是否动画播放。
bool wasAnimating = isAnimating;
        isAnimating = stateInfo.IsName("Opening") || stateInfo.IsName("Closing");
        if (wasAnimating && !isAnimating)
        {
            HandleAnimationEnd();
        }

        if (isAnimating)
        {
            // 角度。
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
            // 角度。
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
            // 是否室内清除。
bool interiorClear = CheckDirectionClear(openDirection);
            if (!interiorClear)
            {
                Debug.LogWarning($"[DoorController] {gameObject.name} 室内开门方向存在静态碰撞，仍保持向内开门以保护门外追击通道。", this);
            }

            Debug.Log($"[DoorController] {gameObject.name} 开门方向: {openDirection}（统一向室内）", this);
            return;
        }

        // 是否正向清除。
bool positiveClear = CheckDirectionClear(1);
        // 是否负值清除。
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

        // 室内侧边。
int interiorSide = ResolveInteriorPassageSide();
        // 正向分值。
float positiveScore = GetOpenLeafInteriorScore(boxCollider, 1, interiorSide);
        // 负值分值。
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
        // 室内方向。
Vector3 interiorDirection = transform.up;
        // 路线。
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
    /// <param name="boxCollider">盒形碰撞体。</param>
    /// <param name="direction">方向。</param>
    /// <param name="interiorSide">室内侧边。</param>
    private float GetOpenLeafInteriorScore(BoxCollider boxCollider, int direction, int interiorSide)
    {
        // 已关闭中心。
Vector3 closedCenter = boxCollider.transform.TransformPoint(boxCollider.center);
        // 中心内部铰链。
Vector3 centerInHinge = hingePivot.InverseTransformPoint(closedCenter);
        // 打开旋转。
Quaternion openRotation = Quaternion.AngleAxis(openAngle * direction, Vector3.up) * initialHingeWorldRotation;
        // 打开中心。
Vector3 openCenter = hingePivot.position + openRotation * centerInHinge;
        // 偏移量。
Vector3 offset = openCenter - PassageCenter;
        offset.y = 0f;
        return Vector3.Dot(offset, PassageNormal) * interiorSide;
    }

    /// <summary>CheckDirectionClear 的业务处理入口；本方法直接依赖 DoorController.AbsVector(Vector3)、Physics.OverlapBox(Vector3, Vector3, Quaternion)、Quaternion.AngleAxis(float, Vector3)、Quaternion.Inverse(Quaternion)、Transform.InverseTransformPoint(Vector3)、Transform.IsChildOf(Transform)、Transform.TransformPoint(Vector3) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.DetermineOpenDirection()；下游（含内部回调体） DoorController.AbsVector(Vector3)、Physics.OverlapBox(Vector3, Vector3, Quaternion)、Quaternion.AngleAxis(float, Vector3)、Quaternion.Inverse(Quaternion)、Transform.InverseTransformPoint(Vector3)、Transform.IsChildOf(Transform)、Transform.TransformPoint(Vector3) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">方向。</param>
    bool CheckDirectionClear(int direction)
    {
        if (physicalCollider == null || hingePivot == null)
        {
            return true;
        }

        // 盒形碰撞体。
BoxCollider boxCollider = physicalCollider as BoxCollider;
        if (boxCollider == null)
        {
            return true;
        }

        // 已关闭中心。
Vector3 closedCenter = boxCollider.transform.TransformPoint(boxCollider.center);
        // 中心内部铰链。
Vector3 centerInHinge = hingePivot.InverseTransformPoint(closedCenter);
        // 碰撞体旋转内部铰链。
Quaternion colliderRotationInHinge = Quaternion.Inverse(hingePivot.rotation) * boxCollider.transform.rotation;
        // 验证铰链旋转。
Quaternion testHingeRotation = Quaternion.AngleAxis(openAngle * direction, Vector3.up) * initialHingeWorldRotation;
        // 检查中心。
Vector3 checkCenter = hingePivot.position + testHingeRotation * centerInHinge;
        // 检查旋转。
Quaternion checkRotation = testHingeRotation * colliderRotationInHinge;
        // 一半半尺寸。
Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, AbsVector(boxCollider.transform.lossyScale));
        // 列之前是否启用。
bool colWasEnabled = physicalCollider.enabled;
        physicalCollider.enabled = false;
        // 重叠结果。
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
    /// <param name="value">值。</param>
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

        // 玩家。
var player = LeonController.Instance;
        if (player == null)
        {
            return true;
        }

        // 当前里昂动画绑定组件。
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
    /// <param name="position">位置。</param>
    /// <param name="forward">前向。</param>
    /// <param name="radius">半径。</param>
    /// <param name="facingAngle">朝向角度。</param>
    /// <param name="opener">开门者。</param>
    public static bool TryOpenNearestForAI(Vector3 position, Vector3 forward, float radius, float facingAngle, Transform opener)
    {
        if (radius <= 0f)
        {
            return false;
        }

        // 最佳门。
DoorController bestDoor = null;
        // 最佳距离平方。
float bestDistanceSqr = radius * radius;
        // 平面前向。
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

            // 偏移量。
Vector3 offset = door.PassageCenter - position;
            offset.y = 0f;
            // 距离平方。
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
    /// <param name="point">点。</param>
    /// <param name="radius">半径。</param>
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
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="passageDestination">通道目的地。输出参数，由本入口写入并交给调用方。</param>
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
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="passageDoor">通道门。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindPassageTowardDestination(Vector3 source, Vector3 destination, out DoorController passageDoor)
    {
        return TryFindPassageTowardDestination(source, destination, out passageDoor, out _);
    }

    /// <summary>
    /// 返回区域拓扑路线上的第一扇门，并同时返回来源区域位于门法线的
    /// 哪一侧。只要区域拓扑能够识别两端，就不再退回直线穿门猜测。
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController)、DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)、VillagerController.TryGetSameLevelPursuitIngressDestination(Vector3, out Vector3)；下游（含内部回调体） DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float)、Mathf.Abs(float)、Mathf.Sign(float)、PursuitNavigationTopology.TryFindFirstPortal(Vector3, Vector3, out DoorController, out int)、PursuitNavigationTopology.TryResolveRegions(Vector3, Vector3, out int, out int)、Vector3.Dot(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="passageDoor">通道门。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="sourceSideSign">来源侧边符号。输出参数，由本入口写入并交给调用方。</param>
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
        // 最佳门。
DoorController bestDoor = null;
        // 最佳目标距离平方。
float bestTargetDistanceSqr = float.PositiveInfinity;
        // 最佳来源距离平方。
float bestSourceDistanceSqr = float.PositiveInfinity;
        // 侧边容差。
const float sideTolerance = 0.12f;
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // 中心。
Vector3 center = door.PassageCenter;
            // 表面法线。
Vector3 normal = door.PassageNormal;
            // 来源侧边。
float sourceSide = Vector3.Dot(source - center, normal);
            // 目的地侧边。
float destinationSide = Vector3.Dot(destination - center, normal);
            if (Mathf.Abs(sourceSide) <= sideTolerance || Mathf.Abs(destinationSide) <= sideTolerance || Mathf.Sign(sourceSide) == Mathf.Sign(destinationSide))
            {
                continue;
            }

            if (!door.IsPassageRequiredForRoute(source, destination, sourceSide, destinationSide))
            {
                continue;
            }

            // 目的地偏移量。
Vector3 destinationOffset = destination - center;
            destinationOffset.y = 0f;
            // 目标距离平方。
float targetDistanceSqr = destinationOffset.sqrMagnitude;
            // 来源偏移量。
Vector3 sourceOffset = source - center;
            sourceOffset.y = 0f;
            // 来源距离平方。
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
            // 侧边。
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
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="sourceSide">来源侧边。</param>
    /// <param name="destinationSide">目的地侧边。</param>
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
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="sourceSide">来源侧边。</param>
    /// <param name="destinationSide">目的地侧边。</param>
    private bool DoesRouteCrossFinitePassage(Vector3 source, Vector3 destination, float sourceSide, float destinationSide)
    {
        // 侧边增量。
float sideDelta = sourceSide - destinationSide;
        if (Mathf.Abs(sideDelta) <= 0.0001f)
        {
            return false;
        }

        // 跨越比例。
float crossingRatio = Mathf.Clamp01(sourceSide / sideDelta);
        // 跨越点。
Vector3 crossingPoint = Vector3.Lerp(source, destination, crossingRatio);
        // 偏移量。
Vector3 offset = crossingPoint - PassageCenter;
        offset.y = 0f;
        // 侧向距离。
float lateralDistance = Mathf.Abs(Vector3.Dot(offset, PassageTangent));
        return lateralDistance <= PassageHalfWidth + Mathf.Max(0f, aiRouteLateralPadding);
    }

    /// <summary>ConnectsSeparateNavigationAreas 的业务处理入口；本方法直接依赖 AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Max(float, float)、Mathf.Sign(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DoorController.IsPassageRequiredForRoute(Vector3, Vector3, float, float)；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Mathf.Max(float, float)、Mathf.Sign(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="sourceSide">来源侧边。</param>
    private bool ConnectsSeparateNavigationAreas(Vector3 source, Vector3 destination, float sourceSide)
    {
        if (AstarPath.active == null)
        {
            return false;
        }

        // 来源信息。
NNInfo sourceInfo = AstarPath.active.GetNearest(source, NearestNodeConstraint.Walkable);
        // 目的地信息。
NNInfo destinationInfo = AstarPath.active.GetNearest(destination, NearestNodeConstraint.Walkable);
        if (sourceInfo.node == null || destinationInfo.node == null || sourceInfo.node.Area == destinationInfo.node.Area)
        {
            return false;
        }

        // 探测距离。
float probeDistance = Mathf.Max(0.1f, aiRouteSideProbeDistance);
        // 来源方向。
float sourceDirection = Mathf.Sign(sourceSide);
        // 来源探测。
Vector3 sourceProbe = PassageCenter + PassageNormal * sourceDirection * probeDistance;
        // 目的地探测。
Vector3 destinationProbe = PassageCenter - PassageNormal * sourceDirection * probeDistance;
        // 来源探测信息。
NNInfo sourceProbeInfo = AstarPath.active.GetNearest(sourceProbe, NearestNodeConstraint.Walkable);
        // 目的地探测信息。
NNInfo destinationProbeInfo = AstarPath.active.GetNearest(destinationProbe, NearestNodeConstraint.Walkable);
        return sourceProbeInfo.node != null && destinationProbeInfo.node != null && sourceProbeInfo.node.Area == sourceInfo.node.Area && destinationProbeInfo.node.Area == destinationInfo.node.Area;
    }

    /// <summary>
    /// 返回距指定目标最近的门洞。跨层路线先锁定与楼梯入口同属建筑的门，
    /// 再由敌人判断自己是否位于该门的另一侧，避免远处敌人被分派到别的门。
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerController.TryGetPursuitIngressDestination(Vector3, out Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="destination">目的地。</param>
    /// <param name="passageDoor">通道门。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryFindNearestPassageToDestination(Vector3 destination, out DoorController passageDoor)
    {
        passageDoor = null;
        // 最佳距离平方。
float bestDistanceSqr = float.PositiveInfinity;
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryFindNearestPassageToDestination(Vector3, out DoorController)。
foreach (DoorController door in sceneDoors)
        {
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            // 偏移量。
Vector3 offset = destination - door.PassageCenter;
            offset.y = 0f;
            // 距离平方。
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
    /// <param name="collider">碰撞体。</param>
    public static bool IsOpenDoorCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        // 门。
DoorController door = collider.GetComponentInParent<DoorController>();
        return door != null && door.isOpen;
    }

    /// <summary>TryResolveOpenPassageDirection 的业务处理入口；本方法直接依赖 DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、Mathf.Max(float, float)、Vector3.Normalize()、Vector3.Project(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.ResolveStandingPathDirection()；下游（含内部回调体） DoorController.ContainsPassageGeometry(Vector3, float)、DoorController.TryFindPassageTowardDestination(Vector3, Vector3, out DoorController, out int)、Mathf.Max(float, float)、Vector3.Normalize()、Vector3.Project(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="captureDistance">捕获距离。</param>
    /// <param name="direction">方向。输出参数，由本入口写入并交给调用方。</param>
    public static bool TryResolveOpenPassageDirection(Vector3 position, Vector3 destination, float captureDistance, out Vector3 direction)
    {
        direction = Vector3.zero;
        // 门边转向也必须遵守真实通道路线。屋外追击屋后目标时，不能
        // 因目标落在门的无限平面另一侧，就把刚出门的敌人再次引回屋内。
        if (!TryFindPassageTowardDestination(position, destination, out DoorController /* 中文含义：所需门。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ requiredDoor, out int /* 中文含义：来源侧边。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sourceSide))
        {
            return false;
        }

        // 最近门。
DoorController nearestDoor = null;
        // 最近距离平方。
float nearestDistanceSqr = Mathf.Max(0.25f, captureDistance * captureDistance);
        // door（门）：每轮从场景门集合取得一个元素，仅代表当前这一项。使用位置：DoorController.TryResolveOpenPassageDirection(Vector3, Vector3, float, out Vector3)。
foreach (DoorController door in sceneDoors)
        {
            if (door != requiredDoor || door == null || !door.isActiveAndEnabled || !door.isOpen)
            {
                continue;
            }

            // 偏移量。
Vector3 offset = door.PassageCenter - position;
            offset.y = 0f;
            // 距离平方。
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

        // 表面法线。
Vector3 normal = nearestDoor.PassageNormal * -sourceSide;
        // 目标中心。
Vector3 toCenter = nearestDoor.PassageCenter - position;
        toCenter.y = 0f;
        // 侧向校正量。
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
    /// <param name="point">点。</param>
    /// <param name="halfDepth">一半深度。</param>
    public bool ContainsPassageGeometry(Vector3 point, float halfDepth)
    {
        EnsurePassageGeometry();
        // 偏移量。
Vector3 offset = point - passageCenter;
        // 门洞只约束本高度层，不能把屋顶上的角色当作楼下门洞的通行者。
        // 中心高度。
float centerHeight = passageCenter.y + 1f;
        // 一半高度。
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
        // 侧向距离。
float lateralDistance = Mathf.Abs(Vector3.Dot(offset, passageTangent));
        // 深度距离。
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
        // 坐标轴。
Vector3[] axes =
        {
            boxCollider.transform.TransformVector(Vector3.right * boxCollider.size.x),
            boxCollider.transform.TransformVector(Vector3.up * boxCollider.size.y),
            boxCollider.transform.TransformVector(Vector3.forward * boxCollider.size.z)
        };
        // 竖直轴。
int verticalAxis = 0;
        // 最强竖直。
float strongestVertical = -1f;
        // 当前遍历索引。
for (int i = 0; i < axes.Length; i++)
        {
            // 竖直强度。
float verticalStrength = Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up));
            if (verticalStrength <= strongestVertical)
            {
                continue;
            }

            strongestVertical = verticalStrength;
            verticalAxis = i;
        }

        // 首个水平。
Vector3 firstHorizontal = axes[(verticalAxis + 1) % axes.Length];
        // 第二水平。
Vector3 secondHorizontal = axes[(verticalAxis + 2) % axes.Length];
        firstHorizontal.y = 0f;
        secondHorizontal.y = 0f;
        // 宽度轴。
Vector3 widthAxis;
        if (firstHorizontal.sqrMagnitude >= secondHorizontal.sqrMagnitude)
        {
            widthAxis = firstHorizontal;
        }
        else
        {
            widthAxis = secondHorizontal;
        }

        // 深度轴。
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
    /// <param name="direction">方向。</param>
    /// <param name="fallback">后备。</param>
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
    /// <param name="open">是否打开。</param>
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
            // 当前敌人智能管理器。
EnemyAIManager manager = EnemyAIManager.Instance;
            if (manager != null)
            {
                manager.NotifyEnvironmentalDisturbance(transform.position, manager.doorDisturbanceRadius);
            }
        }
    }
}
