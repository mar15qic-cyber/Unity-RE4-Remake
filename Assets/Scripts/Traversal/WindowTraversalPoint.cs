using UnityEngine;

/// <summary>
/// 可翻越窗口的主动标记。
/// 只有挂载该组件并通过里昂几何校验的窗口才会显示翻越提示。
/// 窗口本地前方向代表正面，本地右方向代表开口宽度方向。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class WindowTraversalPoint : MonoBehaviour, IInteractable
{
    /// <summary>中文含义：交互触发器。使用位置：LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)、WindowTraversalPoint.ConfigureInteractionTrigger()、WindowTraversalPoint.OnValidate() 等共 5 处。</summary>
    [Header("交互碰撞体")]
    public BoxCollider interactTrigger;
    /// <summary>中文含义：开口中心。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、WindowTraversalPoint.ConfigureInteractionTrigger()。</summary>
    [Header("窗口开口")]
    public Vector3 openingCenter = Vector3.zero;
    /// <summary>中文含义：开口宽度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 6 处。</summary>
    [Min(0.1f)]
    public float openingWidth = 0.9f;
    /// <summary>中文含义：开口高度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 10 处。</summary>
    [Min(0.1f)]
    public float openingHeight = 1.1f;
    /// <summary>中文含义：墙体厚度。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、HouseThreeRegionNavigation.ReadPlatformOwnership(Vector3, bool) 等共 7 处。</summary>
    [Min(0.05f)]
    public float wallThickness = 0.3f;
    [Tooltip("低栅栏使用跨栏动作，且仅提供玩家翻越。")]
    public bool isFence;
    /// <summary>中文含义：接近偏移量。使用位置：LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)、WindowTraversalPoint.GetApproachProbePosition(int)。</summary>
    [Header("站位距离")]
    [Min(0.1f)]
    public float approachOffset = 0.45f;
    /// <summary>中文含义：落地偏移量。使用位置：LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)、WindowTraversalPoint.GetLandingProbePosition(int)。</summary>
    [Min(0.1f)]
    public float landingOffset = 0.6f;
    /// <summary>中文含义：交互范围。使用位置：作为 Mathf.Max(float, float) 的参数、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController) 等共 4 处。</summary>
    [Min(0.1f)]
    public float interactRange = 1.75f;
    /// <summary>中文含义：是否允许双方方向集合。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)、WindowTraversalPoint.OnDrawGizmosSelected()。</summary>
    [Header("通行方向")]
    public bool allowBothDirections = true;
    /// <summary>中文含义：大于0时覆盖里昂翻越控制器的最大面对角度。使用位置：WindowTraversalPoint.ResolveMaximumFacingAngle(float)。</summary>
    [Header("单点覆盖")]
    [Tooltip("大于0时覆盖里昂翻越控制器的最大面对角度")]
    [Range(0f, 120f)]
    public float maximumFacingAngleOverride = 0f;
    /// <summary>中文含义：是否大于等于0时覆盖里昂翻越控制器允许的最大落点高差。大于等于0时覆盖里昂翻越控制器允许的最大落点高差。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Tooltip("大于等于0时覆盖里昂翻越控制器允许的最大落点高差")]
    [Header("高落差自动下落")]
    public bool enableAutomaticDropLanding;
    /// <summary>中文含义：最小自动掉落高度。使用位置：LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)。</summary>
    [Min(0.5f)]
    public float minimumAutomaticDropHeight = 2f;
    /// <summary>中文含义：自动掉落释放偏移量。使用位置：WindowTraversalPoint.GetAutomaticDropReleasePosition(int)。</summary>
    [Min(0.1f)]
    public float automaticDropReleaseOffset = 0.85f;
    /// <summary>中文含义：自动掉落释放归一化时间。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(0.5f, 0.95f)]
    public float automaticDropReleaseNormalizedTime = 0.72f;
    /// <summary>中文含义：自动掉落水平速度。使用位置：作为 LeonActionMotionDriver.CommitSpeed(MotionControlToken, Vector3) 的参数、LeonWindowTraversal.UpdateAutomaticFall()。</summary>
    [Min(0f)]
    public float automaticDropHorizontalSpeed = 1.15f;
    /// <summary>中文含义：最大落地高度差值覆盖。使用位置：WindowTraversalPoint.ResolveMaximumLandingHeightDifference(float)。</summary>
    [Min(-1f)]
    public float maximumLandingHeightDifferenceOverride = -1f;
    /// <summary>中文含义：地面遮罩。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    [Header("物理检测")]
    public LayerMask groundMask = ~0;
    /// <summary>中文含义：障碍遮罩。使用位置：作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    public LayerMask obstacleMask = ~0;
    /// <summary>中文含义：地面探测高度。使用位置：EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)、StrictAstarGraphBaker.ValidateElevatedPlatformNodes(RecastGraph, out bool)。</summary>
    [Min(0.1f)]
    public float groundProbeHeight = 1.5f;
    /// <summary>中文含义：地面探测距离。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    [Min(0.5f)]
    public float groundProbeDistance = 4f;
    /// <summary>中文含义：翻越校验和执行期间允许忽略的窗框或墙体碰撞体。使用位置：LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)、WindowTraversalPoint.ShouldIgnoreCollider(Collider)。</summary>
    [Tooltip("翻越校验和执行期间允许忽略的窗框或墙体碰撞体")]
    public Collider[] ignoredTraversalColliders;
    /// <summary>中文含义：预先验证的静态路径碰撞体集合。已在编辑器中确认轨迹安全、运行时只需忽略其路径阻挡的静态地图碰撞体。使用位置：WindowTraversalPoint.ShouldIgnorePathCollider(Collider)。</summary>
    [Header("地图静态预审")]
    [Tooltip("已在编辑器中确认轨迹安全、运行时只需忽略其路径阻挡的静态地图碰撞体")]
    public Collider[] prevalidatedStaticPathColliders;
    /// <summary>中文含义：交互范围。</summary>
    public float InteractRange => interactRange;
    /// <summary>中文含义：开口中心世界空间。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、EnemyWindowTraversal.BeginWindowTraversal(EnemyWindowTraversalPlan)、EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3) 等共 11 处。</summary>
    public Vector3 OpeningCenterWorld => transform.TransformPoint(openingCenter);
    /// <summary>中文含义：常规世界空间。使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、作为 Vector3.Dot(Vector3, Vector3) 的参数、作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数 等共 8 处。</summary>
    public Vector3 NormalWorld => transform.forward.normalized;

    [Header("玻璃或板材")]
    [Tooltip("选填。开口有完整玻璃时绑定可碎窗对象。")]
    public WindowFracture fracture;

    /// <summary>ResolveMaximumFacingAngle 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fallback">中文含义：后备。使用位置：WindowTraversalPoint.ResolveMaximumFacingAngle(float)。</param>
    public float ResolveMaximumFacingAngle(float fallback)
    {
        if (maximumFacingAngleOverride > 0f)
        {
            return maximumFacingAngleOverride;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>ResolveMaximumLandingHeightDifference 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fallback">中文含义：后备。使用位置：WindowTraversalPoint.ResolveMaximumLandingHeightDifference(float)。</param>
    public float ResolveMaximumLandingHeightDifference(float fallback)
    {
        if (maximumLandingHeightDifferenceOverride >= 0f)
        {
            return maximumLandingHeightDifferenceOverride;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()、WindowTraversalPoint.ConfigureInteractionTrigger()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (interactTrigger == null)
        {
            interactTrigger = GetComponent<BoxCollider>();
        }

        ConfigureInteractionTrigger();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Register(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        InteractionSystem.Register(this);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.Unregister(IInteractable)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        InteractionSystem.Unregister(this);
    }

    /// <summary>CanInteract 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWindowTraversal&gt;()、LeonWindowTraversal.CanTraverse(WindowTraversalPoint)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonWindowTraversal&gt;()、LeonWindowTraversal.CanTraverse(WindowTraversalPoint)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="interactor">中文含义：交互者。使用位置：WindowTraversalPoint.CanInteract(Transform)。</param>
    public bool CanInteract(Transform interactor)
    {
        if (!isActiveAndEnabled || interactor == null)
        {
            return false;
        }

        if (!isFence && VillageLadderSite.HasAvailablePush(this, interactor))
        {
            return false;
        }

        // traversal（通行）：保存 Component.GetComponent<LeonWindowTraversal>() 的返回结果，供当前步骤使用。使用位置：WindowTraversalPoint.CanInteract(Transform)。
LeonWindowTraversal traversal = interactor.GetComponent<LeonWindowTraversal>();
        return traversal != null && traversal.CanTraverse(this);
    }

    private Object passageOwner;

    private EnemyWindowTraversal fallingActor;

    internal void HoldForFallingActor(EnemyWindowTraversal actor)
    {
        fallingActor = actor;
    }

    internal bool FallingBodyInOpening
    {
        get
        {
            if (fallingActor != null && fallingActor.IsClearingOpening(this))
            {
                return true;
            }
            fallingActor = null;
            return false;
        }
    }

    internal bool IsPassageReservedByOther(Object actor)
    {
        return FallingBodyInOpening || passageOwner != null && passageOwner != actor;
    }

    internal bool TryReservePassage(Object actor)
    {
        if (actor == null || !isActiveAndEnabled || IsPassageReservedByOther(actor))
        {
            return false;
        }

        passageOwner = actor;
        return true;
    }

    internal void ReleasePassage(Object actor)
    {
        if (passageOwner == actor)
        {
            passageOwner = null;
        }
    }

    /// <summary>Interact 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWindowTraversal&gt;()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonWindowTraversal&gt;()、LeonWindowTraversal.TryStartTraversal(WindowTraversalPoint)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Interact()
    {
        if (LeonController.Instance == null)
        {
            return;
        }

        // traversal（通行）：保存 Component.GetComponent<LeonWindowTraversal>() 的返回结果，供当前步骤使用。使用位置：WindowTraversalPoint.Interact()。
LeonWindowTraversal traversal = LeonController.Instance.GetComponent<LeonWindowTraversal>();
        // receiverValue3（判空前缓存的通行）：从通行读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WindowTraversalPoint.Interact()。
global::LeonWindowTraversal receiverValue3 = traversal;
        // optionalValue4（条件访问得到的尝试起始通行的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：WindowTraversalPoint.Interact()。
bool? optionalValue4;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            optionalValue4 = receiverValue3.TryStartTraversal(this);
        }
        else
        {
            optionalValue4 = default;
        }
    }

    /// <summary>GetPromptText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string GetPromptText()
    {
        if (isFence)
        {
            return "翻越栅栏";
        }

        return "翻越";
    }

    /// <summary>ShouldIgnoreCollider 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.TryFindGround(Vector3, out Vector3)、LeonWindowTraversal.TryFindGround(WindowTraversalPoint, Vector3, out Vector3)、LeonWindowTraversal.TryGetDropGroundDistance(out float)、StrictAstarGraphBaker.ValidateElevatedPlatformNodes(RecastGraph, out bool)、WindowTraversalPoint.ShouldIgnorePathCollider(Collider)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">中文含义：候选。使用位置：WindowTraversalPoint.ShouldIgnoreCollider(Collider)。</param>
    public bool ShouldIgnoreCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate == interactTrigger || fracture != null && fracture.OwnsCollider(candidate))
        {
            return true;
        }

        if (ignoredTraversalColliders == null)
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：WindowTraversalPoint.ShouldIgnoreCollider(Collider)。
for (int i = 0; i < ignoredTraversalColliders.Length; i++)
        {
            if (ignoredTraversalColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ShouldIgnorePathCollider 的业务处理入口；本方法直接依赖 WindowTraversalPoint.ShouldIgnoreCollider(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.ContainsBlockingCollider(Collider[], WindowTraversalPoint)；下游（含内部回调体） WindowTraversalPoint.ShouldIgnoreCollider(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="candidate">中文含义：候选。使用位置：作为 WindowTraversalPoint.ShouldIgnoreCollider(Collider) 的参数、WindowTraversalPoint.ShouldIgnorePathCollider(Collider)。</param>
    public bool ShouldIgnorePathCollider(Collider candidate)
    {
        if (ShouldIgnoreCollider(candidate))
        {
            return true;
        }

        if (prevalidatedStaticPathColliders == null)
        {
            return false;
        }

        // i（当前遍历索引）：使用位置：WindowTraversalPoint.ShouldIgnorePathCollider(Collider)。
for (int i = 0; i < prevalidatedStaticPathColliders.Length; i++)
        {
            if (prevalidatedStaticPathColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>GetApproachProbePosition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.TryAcquireWindowSideTread(bool, out Vector3)、EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、StrictAstarGraphBaker.ValidateElevatedPlatformNodes(RecastGraph, out bool)、WindowTraversalPoint.OnDrawGizmosSelected()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sideSign">中文含义：侧边符号。使用位置：WindowTraversalPoint.GetApproachProbePosition(int)。</param>
    public Vector3 GetApproachProbePosition(int sideSign)
    {
        return OpeningCenterWorld + NormalWorld * sideSign * (wallThickness * 0.5f + approachOffset);
    }

    /// <summary>GetLandingProbePosition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyWindowTraversalRoute.TryBuildPlanFromSide(VillagerController, bool, out EnemyWindowTraversalPlan, bool)、LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)、WindowTraversalPoint.OnDrawGizmosSelected()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sideSign">中文含义：侧边符号。使用位置：WindowTraversalPoint.GetLandingProbePosition(int)。</param>
    public Vector3 GetLandingProbePosition(int sideSign)
    {
        return OpeningCenterWorld - NormalWorld * sideSign * (wallThickness * 0.5f + landingOffset);
    }

    /// <summary>GetAutomaticDropReleasePosition 的业务处理入口；本方法直接依赖 Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversal.TryBuildPlan(WindowTraversalPoint, out LeonWindowTraversal.TraversalPlan)；下游（含内部回调体） Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sideSign">中文含义：侧边符号。使用位置：WindowTraversalPoint.GetAutomaticDropReleasePosition(int)。</param>
    public Vector3 GetAutomaticDropReleasePosition(int sideSign)
    {
        // travelDirection（行进方向）：使用位置：WindowTraversalPoint.GetAutomaticDropReleasePosition(int)。
Vector3 travelDirection = -NormalWorld * sideSign;
        travelDirection.y = 0f;
        if (travelDirection.sqrMagnitude < 0.001f)
        {
            travelDirection = transform.forward;
        }

        travelDirection.Normalize();
        return OpeningCenterWorld + travelDirection * automaticDropReleaseOffset;
    }

    /// <summary>Unity 编辑器字段验证回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()、WindowTraversalPoint.ConfigureInteractionTrigger()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnValidate()
    {
        if (interactTrigger == null)
        {
            interactTrigger = GetComponent<BoxCollider>();
        }

        ConfigureInteractionTrigger();
    }

    /// <summary>ConfigureInteractionTrigger 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WindowTraversalPoint.OnValidate()、WindowTraversalPoint.Start()；下游（含内部回调体） Mathf.Max(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ConfigureInteractionTrigger()
    {
        if (interactTrigger == null)
        {
            return;
        }

        interactTrigger.isTrigger = true;
        interactTrigger.center = openingCenter;
        interactTrigger.size = new Vector3(Mathf.Max(0.1f, openingWidth + 0.8f), Mathf.Max(0.1f, openingHeight + 0.8f), Mathf.Max(0.2f, wallThickness + interactRange * 2f));
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawSphere(Vector3, float)、Gizmos.DrawWireCube(Vector3, Vector3)、Matrix4x4.TRS(Vector3, Quaternion, Vector3)、Vector3.Vector3(float, float, float)、WindowTraversalPoint.GetApproachProbePosition(int) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        // center（中心）：使用位置：作为 Gizmos.DrawLine(Vector3, Vector3) 的参数、作为 Matrix4x4.TRS(Vector3, Quaternion, Vector3) 的参数。
Vector3 center = OpeningCenterWorld;
        // previous（前一次）：使用位置：WindowTraversalPoint.OnDrawGizmosSelected()。
Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.15f, 0.9f, 0.35f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(openingWidth, openingHeight, wallThickness));
        Gizmos.matrix = previous;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center, center + NormalWorld * 0.75f);
        Gizmos.DrawSphere(GetApproachProbePosition(1), 0.08f);
        Gizmos.DrawSphere(GetLandingProbePosition(1), 0.08f);
        if (allowBothDirections)
        {
            Gizmos.DrawSphere(GetApproachProbePosition(-1), 0.08f);
            Gizmos.DrawSphere(GetLandingProbePosition(-1), 0.08f);
        }
    }
}
