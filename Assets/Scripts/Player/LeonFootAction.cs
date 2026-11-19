using UnityEngine;

/// <summary>玩家控制、动作与运动组件 LeonFootAction，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "里昂足部动作")]
public sealed class LeonFootAction : MonoBehaviour, IFootActionState
{
    [Tooltip("为本角色启用地面足部修正，攀梯和全身动作自行接管双腿。")]
    [SerializeField] private bool enableTerrainIK;
    internal bool UseTerrainIK => enableTerrainIK;
    /// <summary>中文含义：里昂。使用位置：LeonFootAction.AllowGroundContact.get、LeonFootAction.FixedUpdate()、LeonFootAction.IsStepping.get。</summary>
    [Header("里昂动作与接地状态")]
    [UnityEngine.Serialization.FormerlySerializedAs("里昂")]
    [UnityEngine.InspectorName("里昂")]
    public LeonController leon;
    /// <summary>中文含义：运动。使用位置：LeonFootAction.AllowGroundContact.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("运动")]
    [UnityEngine.InspectorName("运动")]
    public LeonActionMotionDriver motion;
    /// <summary>中文含义：动作。使用位置：LeonFootAction.AllowGroundContact.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动作")]
    [UnityEngine.InspectorName("动作")]
    public LeonAnimationActionCoordinator action;
    /// <summary>中文含义：高处落地。使用位置：LeonFootAction.AllowGroundContact.get。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("高处落地")]
    [UnityEngine.InspectorName("高处落地")]
    public LeonHighDropLanding elevatedLanding;
    /// <summary>中文含义：转向（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonFootAction.IsRecoverFootOrTurn(int)。</summary>
    private static readonly int turn = Animator.StringToHash("TurnInPlace");
    /// <summary>中文含义：恢复步进（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonFootAction.IsRecoverFootOrTurn(int)。</summary>
    private static readonly int recoverStep = Animator.StringToHash("WalkStops");
    /// <summary>中文含义：快速停止（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonFootAction.IsRecoverFootOrTurn(int)。</summary>
    private static readonly int quickStop = Animator.StringToHash("SprintStop");
    /// <summary>中文含义：已准备快速停止（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonFootAction.IsRecoverFootOrTurn(int)。</summary>
    private static readonly int armedQuickStop = Animator.StringToHash("SprintStop_WS");
    /// <summary>中文含义：移动。使用位置：LeonFootAction.IsStepping.get、LeonFootAction.Start()。</summary>
    private LeonLocomotion movement;
    /// <summary>中文含义：动画。使用位置：LeonFootAction.IsStepping.get、LeonFootAction.Start()。</summary>
    private Animator animation;
    private LeonAnimatorDriver animatorDriver;
    /// <summary>中文含义：刚体值。使用位置：LeonFootAction.FixedUpdate()、LeonFootAction.Start()。</summary>
    private Rigidbody rigidbodyValue;
    /// <summary>中文含义：胶囊。使用位置：LeonFootAction.FixedUpdate()、LeonFootAction.Start()。</summary>
    private CapsuleCollider capsule;
    /// <summary>中文含义：是否等待中接触。使用位置：LeonFootAction.AllowGroundContact.get、LeonFootAction.FixedUpdate()。</summary>
    private bool waitingContact = true;
    private KnifeCombatModule knife;
    private readonly RaycastHit[] contactHits = new RaycastHit[12];
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;LeonLocomotion&gt;()、Component.GetComponent&lt;Rigidbody&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        movement = GetComponent<LeonLocomotion>();
        animation = GetComponent<Animator>();
        animatorDriver = GetComponent<LeonAnimatorDriver>();
        rigidbodyValue = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        knife = GetComponent<KnifeCombatModule>();
    }

    /// <summary>中文含义：是否迈步中。</summary>
    public bool IsStepping
    {
        get
        {
            if (leon.MoveInput.sqrMagnitude > .001f || movement.IsQuickStopDeceleration)
            {
                return true;
            }

            // current（当前）：使用位置：作为 LeonFootAction.IsRecoverFootOrTurn(int) 的参数。
int current = animation.GetCurrentAnimatorStateInfo(0).shortNameHash;
            // lowerOneElement（下部第一元素）：使用位置：作为 LeonFootAction.IsRecoverFootOrTurn(int) 的参数、LeonFootAction.IsStepping.get。
int lowerOneElement;
            if (animation.IsInTransition(0))
            {
                lowerOneElement = animation.GetNextAnimatorStateInfo(0).shortNameHash;
            }
            else
            {
                lowerOneElement = 0;
            }

            return IsRecoverFootOrTurn(current) || IsRecoverFootOrTurn(lowerOneElement);
        }
    }

    /// <summary>IsRecoverFootOrTurn 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonFootAction.IsStepping.get；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：状态。使用位置：LeonFootAction.IsRecoverFootOrTurn(int)。</param>
    private static bool IsRecoverFootOrTurn(int state) => state == turn || state == recoverStep || state == quickStop || state == armedQuickStop;
    /// <summary>中文含义：是否允许地面接触。</summary>
    public bool AllowGroundContact => enableTerrainIK && isActiveAndEnabled && leon != null &&
        leon.isActiveAndEnabled && leon.NewPipelineValid && leon.IsGrounded && !waitingContact && !leon.IsJumping &&
        action != null && !action.HasActiveFullBodyAction && !action.IsHitTransactionActive &&
        (animatorDriver == null || !animatorDriver.IsBlendingOutKnifePose) && (knife == null || !knife.Active) &&
        (elevatedLanding == null || !elevatedLanding.IsActive) && motion != null &&
        motion.CurrentMotionToken.Source == GameArchitecture.MotionSource.NormalMovement &&
        leon.currentState is LeonState.Idle or LeonState.Walking or LeonState.Running or LeonState.Sprinting
            or LeonState.Aiming or LeonState.Shooting or LeonState.Reloading;

    /// <summary>Unity 固定时间步更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Min(float, float)、Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FixedUpdate()
    {
        if (!enableTerrainIK)
        {
            return;
        }

        // 普通接地射线长于小腿，快速下落后只在胶囊重新接触支撑面时恢复双脚。
        waitingContact = true;
        if (leon == null || capsule == null || rigidbodyValue == null || motion == null ||
            motion.CurrentMotionToken.Source != GameArchitecture.MotionSource.NormalMovement ||
            !leon.IsGrounded || leon.IsJumping || rigidbodyValue.linearVelocity.y < -3f)
        {
            return;
        }

        // boundary（边界）：使用位置：作为 Mathf.Min(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
var boundary = capsule.bounds;
        // radius（半径）：使用位置：作为 Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
var radius = Mathf.Min(boundary.extents.x, boundary.extents.z) * .8f;
        // startPoint（起始点）：使用位置：作为 Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction) 的参数。
var startPoint = new Vector3(boundary.center.x, boundary.min.y + radius + .12f, boundary.center.z);
        int count = Physics.SphereCastNonAlloc(startPoint, radius, Vector3.down, contactHits,
            0.23f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = contactHits[i];
            if (hit.collider != null && !motion.IsWalkThroughDetail(hit.collider) && hit.rigidbody != rigidbodyValue && hit.normal.y >= 0.6f &&
                hit.collider.GetComponentInParent<VillagerController>() == null)
            {
                waitingContact = false;
                return;
            }
        }
    }
}
