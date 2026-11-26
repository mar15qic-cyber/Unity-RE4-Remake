using RootMotion.FinalIK;
using UnityEngine;

/// <summary>玩家控制、动作与运动的 IFootActionState 通信契约；消费者通过成员调用实现方，状态及资源仍由实现宿主负责。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "I足部动作状态")]
public interface IFootActionState
{
    /// <summary>是否允许地面接触。</summary>
    bool AllowGroundContact { get; }

    /// <summary>是否迈步中。</summary>
    bool IsStepping { get; }
}

/// <summary>玩家控制、动作与运动的 IFootActionHandoff 通信契约；消费者通过成员调用实现方，状态及资源仍由实现宿主负责。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "I足部动作交接")]
public interface IFootActionHandoff
{
    /// <summary>是否攻击启动中间。</summary>
    bool AttackStartupMiddle { get; }
}

// 在动画完成后读取两脚原姿态，先移动骨盆，再交给腿和持枪手臂解算。
/// <summary>玩家控制、动作与运动组件 BipedFootPlanting，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(9000)]
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "双足贴地")]
public sealed partial class BipedFootPlanting : MonoBehaviour
{
    internal static bool EnemyGroundIKEnabled => false;

    /// <summary>骨盆。</summary>
    [Header("角色骨骼与动作来源")]
    [UnityEngine.Serialization.FormerlySerializedAs("骨盆")]
    [UnityEngine.InspectorName("骨盆")]
    public Transform pelvis;
    /// <summary>动作来源。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("动作来源")]
    [UnityEngine.InspectorName("动作来源")]
    public MonoBehaviour actionSource;
    /// <summary>左腿。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("左腿")]
    [UnityEngine.InspectorName("左腿")]
    public LimbIK leftLeg;
    /// <summary>右腿。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("右腿")]
    [UnityEngine.InspectorName("右腿")]
    public LimbIK rightLeg;
    /// <summary>左脚趾。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("左脚趾")]
    [UnityEngine.InspectorName("左脚趾")]
    public Transform leftToes;
    /// <summary>右脚趾。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("右脚趾")]
    [UnityEngine.InspectorName("右脚趾")]
    public Transform rightToes;
    [Header("脚骨局部空间中的鞋底承重点")]
    [UnityEngine.Serialization.FormerlySerializedAs("左鞋底")]
    [UnityEngine.InspectorName("左鞋底")]
    public Vector3 leftSole;
    /// <summary>右鞋底。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("右鞋底")]
    [UnityEngine.InspectorName("右鞋底")]
    public Vector3 rightSole;
    /// <summary>左鞋底朝上。</summary>
    [Header("脚骨局部空间中的鞋底向上方向")]
    [UnityEngine.Serialization.FormerlySerializedAs("左鞋底朝上")]
    [UnityEngine.InspectorName("左鞋底朝上")]
    public Vector3 leftSoleFacingUp;
    /// <summary>右鞋底朝上。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("右鞋底朝上")]
    [UnityEngine.InspectorName("右鞋底朝上")]
    public Vector3 rightSoleFacingUp;
    /// <summary>地面层。</summary>
    [Header("地面探测")]
    [UnityEngine.Serialization.FormerlySerializedAs("地面层")]
    [UnityEngine.InspectorName("地面层")]
    public LayerMask groundLayer = Physics.DefaultRaycastLayers;
    /// <summary>最大落差。</summary>
    [Min(.05f)]
    [UnityEngine.Serialization.FormerlySerializedAs("最大落差")]
    [UnityEngine.InspectorName("最大落差")]
    public float maximumLandDifference = .42f;
    /// <summary>是否行进稳脚。</summary>
    [Header("行走时约束支撑脚")]
    [UnityEngine.Serialization.FormerlySerializedAs("行进稳脚")]
    [UnityEngine.InspectorName("行进稳脚")]
    public bool movingStableFeet;
    /// <summary>玩家控制、动作与运动模块 FootState；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "双足贴地+足")]
    private sealed class FootState
    {
        /// <summary>链条。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("链")]
        [UnityEngine.InspectorName("链")]
        public LimbIK chain;
        /// <summary>脚趾。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("趾")]
        [UnityEngine.InspectorName("趾")]
        public Transform toe;
        /// <summary>脚部位置。</summary>
        public Transform FootPosition => chain.solver.bone3.transform;

        /// <summary>鞋底。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("鞋底")]
        [UnityEngine.InspectorName("鞋底")]
        public Vector3 sole;
        /// <summary>朝上。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("朝上")]
        [UnityEngine.InspectorName("朝上")]
        public Vector3 facingUp;
        /// <summary>踝目标。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("踝目标")]
        [UnityEngine.InspectorName("踝目标")]
        public Vector3 ankleTarget;
        /// <summary>锚点。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("锚点")]
        [UnityEngine.InspectorName("锚点")]
        public Vector3 anchor;
        /// <summary>位移修正。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("位移修正")]
        [UnityEngine.InspectorName("位移修正")]
        public Vector3 displacementCorrection;
        /// <summary>修正速度。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("修正速度")]
        [UnityEngine.InspectorName("修正速度")]
        public Vector3 correctionSpeed;
        /// <summary>锚旋转。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("锚旋转")]
        [UnityEngine.InspectorName("锚旋转")]
        public Quaternion anchorRotation;
        /// <summary>目标旋转。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("目标旋转")]
        [UnityEngine.InspectorName("目标旋转")]
        public Quaternion targetRotation;
        /// <summary>旋转修正。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("旋转修正")]
        [UnityEngine.InspectorName("旋转修正")]
        public Quaternion rotationCorrection = Quaternion.identity;
        /// <summary>腿长。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("腿长")]
        [UnityEngine.InspectorName("腿长")]
        public float legLong;
        /// <summary>锁定权重。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("锁定权重")]
        [UnityEngine.InspectorName("锁定权重")]
        public float lockWeight;
        public bool nativeContact;
        public float nativeContactWeight;
        public bool hasPlayerSupport;
        public bool levelPlayerSupport;
        public float playerSurfaceHeight;
        public float playerSoleDelta;
        public Vector3 nativeToeAnchor;
        public Vector3 nativeToeCorrection;
        /// <summary>上帧鞋底。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("上帧鞋底")]
        [UnityEngine.InspectorName("上帧鞋底")]
        public Vector3 previousFrameSole;
        /// <summary>已解鞋底。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("已解鞋底")]
        [UnityEngine.InspectorName("已解鞋底")]
        public Vector3 hasSolveSole;
        /// <summary>阶沿退让。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("阶沿退让")]
        [UnityEngine.InspectorName("阶沿退让")]
        public Vector3 stepEdgeYield;
        /// <summary>是否有已解鞋底。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("有已解鞋底")]
        [UnityEngine.InspectorName("有已解鞋底")]
        public bool hasHasSolveSole;
        /// <summary>上次地高。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("上次地高")]
        [UnityEngine.InspectorName("上次地高")]
        public float lastGroundHeight;
        /// <summary>离面余时。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("离面余时")]
        [UnityEngine.InspectorName("离面余时")]
        public float awaySurfaceRemainingTime;
        /// <summary>地面权重。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("地面权重")]
        [UnityEngine.InspectorName("地面权重")]
        public float groundWeight;
        /// <summary>承重抬升。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("承重抬升")]
        [UnityEngine.InspectorName("承重抬升")]
        public float supportWeightRaiseRaise;
        /// <summary>是否锁定。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("锁定")]
        [UnityEngine.InspectorName("锁定")]
        public bool lockValue;
        /// <summary>是否有鞋底历史。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("有鞋底历史")]
        [UnityEngine.InspectorName("有鞋底历史")]
        public bool hasSoleHistory;
        /// <summary>是否踏实。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("踏实")]
        [UnityEngine.InspectorName("踏实")]
        public bool firmFooting;
        /// <summary>是否等待摆动。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("等待摆动")]
        [UnityEngine.InspectorName("等待摆动")]
        public bool waitingSwing;
        /// <summary>支撑点。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("支撑点")]
        [UnityEngine.InspectorName("支撑点")]
        public Vector3 supportPoint;
        /// <summary>上次局部鞋。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("上次局部鞋")]
        [UnityEngine.InspectorName("上次局部鞋")]
        public Vector3 lastLocalShoe;
        /// <summary>是否有步相。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("有步相")]
        [UnityEngine.InspectorName("有步相")]
        public bool hasStepRelative;
        /// <summary>动画保持时间。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("动画保持时间")]
        [UnityEngine.InspectorName("动画保持时间")]
        public float animationHoldTime;
        /// <summary>上次后移速度。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("上次后移速度")]
        [UnityEngine.InspectorName("上次后移速度")]
        public float lastBackMoveSpeed;
        /// <summary>支撑混合。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("支撑混合")]
        [UnityEngine.InspectorName("支撑混合")]
        public float supportBlend;
        /// <summary>上帧屈膝。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("上帧屈膝")]
        [UnityEngine.InspectorName("上帧屈膝")]
        public float previousFrameKneeBend;
        /// <summary>屈膝速度。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("屈膝速度")]
        [UnityEngine.InspectorName("屈膝速度")]
        public float kneeBendSpeed;
        /// <summary>大腿。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("大腿")]
        [UnityEngine.InspectorName("大腿")]
        public JointCorrection largeLeg;
        /// <summary>小腿。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("小腿")]
        [UnityEngine.InspectorName("小腿")]
        public JointCorrection smallLeg;
        /// <summary>脚掌。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("脚掌")]
        [UnityEngine.InspectorName("脚掌")]
        public JointCorrection soleValue;
    }

    /// <summary>玩家控制、动作与运动的结构化数据 JointCorrection；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "双足贴地+关节修正")]
    private struct JointCorrection
    {
        /// <summary>originalRotate 表示“原始旋转”；hasWrite 表示“是否具有写入”；additional 表示“额外”。</summary>
        private Quaternion originalRotate, hasWrite, additional;
        /// <summary>是否已记录。</summary>
        private bool recorded;
        /// <summary>Restore 的业务处理入口；本方法直接依赖 Quaternion.Equals(Quaternion)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 BipedFootPlanting.RestoreCloseSection(BipedFootPlanting.FootState)；下游（含内部回调体） Quaternion.Equals(Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="bone">骨骼。</param>
        public void Restore(Transform bone)
        {
            // 固定步动画可能没有在本帧重采样，只撤掉上次确实留下的写入。
            if (recorded && bone.localRotation.Equals(hasWrite))
            {
                bone.localRotation = originalRotate;
            }

            recorded = false;
        }

        /// <summary>Read 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
        /// <remarks>静态调用链：上游 BipedFootPlanting.Commit(BipedFootPlanting.FootState)、BipedFootPlanting.JointCorrection.FadeExit(Transform, float, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="bone">骨骼。</param>
        public void Read(Transform bone) => originalRotate = bone.localRotation;
        /// <summary>Record 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
        /// <remarks>静态调用链：上游 BipedFootPlanting.Commit(BipedFootPlanting.FootState)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="bone">骨骼。</param>
        public void Record(Transform bone)
        {
            hasWrite = bone.localRotation;
            recorded = true;
        }

        /// <summary>FadeExit 的业务处理入口；本方法直接依赖 BipedFootPlanting.JointCorrection.Read(Transform)、Quaternion.Inverse(Quaternion)、Quaternion.Slerp(Quaternion, Quaternion, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 BipedFootPlanting.FadeExitCorrection(BipedFootPlanting.FootState, float, bool)；下游（含内部回调体） BipedFootPlanting.JointCorrection.Read(Transform)、Quaternion.Inverse(Quaternion)、Quaternion.Slerp(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="bone">骨骼。</param>
        /// <param name="preserve">保留。</param>
        /// <param name="startup">是否启动。</param>
        public void FadeExit(Transform bone, float preserve, bool startup)
        {
            Read(bone);
            // 起手可能同时切换基础动画，按上一帧已解姿势与当前动作首帧的差计算。
            if (startup)
            {
                additional = Quaternion.Inverse(originalRotate) * hasWrite;
            }

            bone.localRotation = originalRotate * Quaternion.Slerp(Quaternion.identity, additional, preserve);
            hasWrite = bone.localRotation;
            recorded = true;
        }
    }

    /// <summary>left 表示“左侧”；right 表示“右侧”。</summary>
    private FootState left, right;
    /// <summary>当前索引脚部动作状态。</summary>
    private IFootActionState state;
    private LeonAnimatorDriver nativeGait;
    /// <summary>交接。</summary>
    private IFootActionHandoff handoff;
    /// <summary>handoffMargin 表示“交接余量”；rootHeightBuffer 表示“根节点高度缓冲区”；rootHeightSettleSpeed 表示“根节点高度稳定速度”。</summary>
    private float handoffMargin, rootHeightBuffer, rootHeightSettleSpeed;
    /// <summary>命中。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly RaycastHit[] hit = new RaycastHit[24];
    /// <summary>pelvisOffset 表示“骨盆偏移量”；pelvisSpeed 表示“骨盆速度”；stationaryTime 表示“静止时间”；previousFrameOrientation 表示“前一次帧朝向”；stationaryOrientation 表示“静止朝向”。</summary>
    private float pelvisOffset, pelvisSpeed, stationaryTime, previousFrameOrientation, stationaryOrientation;
    /// <summary>前一次帧根节点位置。</summary>
    private Vector3 previousFrameRootPosition;
    /// <summary>是否具有根节点历史。</summary>
    private bool hasRootHistory;
    /// <summary>originalPelvisPosition 表示“原始骨盆位置”；writePosition 表示“写入位置”。</summary>
    private Vector3 originalPelvisPosition, writePosition;
    /// <summary>是否具有写入骨盆。</summary>
    private bool hasWritePelvis;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BipedFootPlanting.BuildFoot(LimbIK, Transform, Vector3, Vector3)、BipedFootPlanting.ResetPose()、Debug.LogError(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        playerFeet = actionSource as LeonFootAction;
        if ((playerFeet != null && !playerFeet.UseTerrainIK) || (actionSource is EnemyFootAction && !EnemyGroundIKEnabled))
        {
            DisableLegIK(leftLeg);
            DisableLegIK(rightLeg);
            enabled = false;
            return;
        }

        state = actionSource as IFootActionState;
        handoff = actionSource as IFootActionHandoff;
        var animator = GetComponent<Animator>();
        if (animator != null && animator.HasState(0, LeonAnimatorDriver.NativeIdleState))
        {
            nativeGait = GetComponent<LeonAnimatorDriver>();
        }
        if (state == null || !pelvis || !leftLeg || !rightLeg)
        {
            Debug.LogError("双足贴地缺少骨盆、腿链或动作来源。", this);
            enabled = false;
            return;
        }

        left = BuildFoot(leftLeg, leftToes, leftSole, leftSoleFacingUp);
        right = BuildFoot(rightLeg, rightToes, rightSole, rightSoleFacingUp);
        if (playerFeet != null)
        {
            PreparePlayerLeg(leftLeg);
            PreparePlayerLeg(rightLeg);
        }
        ResetPose();
    }

    private static void DisableLegIK(LimbIK leg)
    {
        if (leg == null)
        {
            return;
        }

        leg.solver.IKPositionWeight = 0f;
        leg.solver.IKRotationWeight = 0f;
        leg.enabled = false;
    }

    /// <summary>BuildFoot 的业务处理入口；本方法直接依赖 BipedFootPlanting.FootState.FootState()、Vector3.Distance(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.Start()；下游（含内部回调体） BipedFootPlanting.FootState.FootState()、Vector3.Distance(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="chain">链条。</param>
    /// <param name="toe">脚趾。</param>
    /// <param name="sole">脚底。</param>
    /// <param name="facingUp">朝向向上。</param>
    private static FootState BuildFoot(LimbIK chain, Transform toe, Vector3 sole, Vector3 facingUp)
    {
        // 求解。
var solve = chain.solver;
        // 腿部长型。
var legLong = Vector3.Distance(solve.bone1.transform.position, solve.bone2.transform.position) + Vector3.Distance(solve.bone2.transform.position, solve.bone3.transform.position);
        return new FootState
        {
            chain = chain,
            toe = toe,
            sole = sole,
            facingUp = facingUp.normalized,
            legLong = legLong
        };
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Update()
    {
        if (playerFeet != null)
        {
            RestoreCloseSection(left);
            RestoreCloseSection(right);
        }
        // 动画暂停或未重采样时也不能叠加上一帧的骨盆位移。
        if (hasWritePelvis && (pelvis.localPosition - writePosition).sqrMagnitude < .00000001f)
        {
            pelvis.localPosition = originalPelvisPosition;
        }

        hasWritePelvis = false;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.WeightBindStair(MonoBehaviour)；下游（含内部回调体） BipedFootPlanting.ResetPose()、BipedFootPlanting.RestoreCloseSection(BipedFootPlanting.FootState)、BipedFootPlanting.Update()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        if (hasWritePelvis && pelvis)
        {
            Update();
        }

        if (playerFeet != null || handoff != null || nativeGait != null)
        {
            RestoreCloseSection(left);
            RestoreCloseSection(right);
        }

        ResetPose();
    }

    internal void ResetGroundContact()
    {
        OnDisable();
    }

    /// <summary>Clear 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.ResetPose()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private static void Clear(FootState footPosition)
    {
        if (footPosition == null || !footPosition.chain)
        {
            return;
        }

        footPosition.lockValue = footPosition.hasSoleHistory = footPosition.hasHasSolveSole = false;
        footPosition.firmFooting = footPosition.waitingSwing = footPosition.hasStepRelative = false;
        footPosition.animationHoldTime = footPosition.lastBackMoveSpeed = 0f;
        footPosition.supportBlend = 0f;
        footPosition.nativeContact = false;
        footPosition.nativeContactWeight = 0f;
        footPosition.nativeToeCorrection = Vector3.zero;
        footPosition.stepEdgeYield = Vector3.zero;
        footPosition.supportWeightRaiseRaise = 0f;
        footPosition.groundWeight = footPosition.awaySurfaceRemainingTime = footPosition.lockWeight = 0f;
        footPosition.displacementCorrection = footPosition.correctionSpeed = Vector3.zero;
        footPosition.rotationCorrection = Quaternion.identity;
        footPosition.chain.solver.IKPositionWeight = footPosition.chain.solver.IKRotationWeight = 0f;
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BipedFootPlanting.Commit(BipedFootPlanting.FootState)、BipedFootPlanting.ComputeSink(BipedFootPlanting.FootState)、BipedFootPlanting.FadeExitCorrection(BipedFootPlanting.FootState, float, bool)、BipedFootPlanting.ResetPose()、BipedFootPlanting.RestoreCloseSection(BipedFootPlanting.FootState)、BipedFootPlanting.UpdateLandPoint(BipedFootPlanting.FootState, Vector3)、BipedFootPlanting.WritePelvis(float) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LateUpdate()
    {
        if (left == null || Time.deltaTime <= 0f)
        {
            return;
        }

        if (this.playerFeet != null)
        {
            UpdatePlayerFeet();
            return;
        }

        if (handoff != null || nativeGait != null)
        {
            RestoreCloseSection(left);
            RestoreCloseSection(right);
        }

        if (!state.AllowGroundContact)
        {
            if (handoffMargin > 0f && handoff.AttackStartupMiddle)
            {
                handoffMargin = Mathf.MoveTowards(handoffMargin, 0f, Time.deltaTime / .24f);
                // 保留。
float preserve = Mathf.SmoothStep(0f, 1f, handoffMargin);
                WritePelvis((pelvisOffset + rootHeightBuffer) * preserve);
                FadeExitCorrection(left, preserve, hasRootHistory);
                FadeExitCorrection(right, preserve, hasRootHistory);
                hasRootHistory = false;
                if (handoffMargin == 0f)
                {
                    ResetPose();
                }
            }
            else if (hasRootHistory || handoffMargin > 0f)
            {
                ResetPose();
            }

            return;
        }

        if (!hasRootHistory && handoffMargin > 0f)
        {
            ResetPose();
        }

        // 朝向。
float orientation = transform.eulerAngles.y;
        if (hasRootHistory && Vector3.Distance(transform.position, previousFrameRootPosition) > .7f)
        {
            ResetPose();
        }

        if (!hasRootHistory)
        {
            stationaryOrientation = previousFrameOrientation = orientation;
        }

        // 是否旋转运动。
bool rotateMotion = Mathf.Abs(Mathf.DeltaAngle(previousFrameOrientation, orientation)) > 5f * Time.deltaTime || Mathf.Abs(Mathf.DeltaAngle(stationaryOrientation, orientation)) > 3f;
        if (state.IsStepping || rotateMotion)
        {
            stationaryOrientation = orientation;
            stationaryTime = 0f;
        }
        else
        {
            stationaryTime += Time.deltaTime;
        }

        previousFrameOrientation = orientation;
        // 根节点位移。
global::UnityEngine.Vector3 rootDisplacement;
        if (hasRootHistory)
        {
            rootDisplacement = Vector3.ProjectOnPlane(transform.position - previousFrameRootPosition, Vector3.up);
        }
        else
        {
            rootDisplacement = Vector3.zero;
        }

        if (movingStableFeet && hasRootHistory)
        {
            // 寻路胶囊可能一帧跳过整级。只在视觉骨盆抵消突变，让它随后平顺落回。
            // 高度移动。
float heightMove = transform.position.y - previousFrameRootPosition.y;
            // 连续高度移动。
float continuousHeightMove = 1.2f * Time.deltaTime;
            rootHeightBuffer = Mathf.SmoothDamp(rootHeightBuffer, 0f, ref rootHeightSettleSpeed, .105f);
            rootHeightBuffer = Mathf.Clamp(rootHeightBuffer - heightMove + Mathf.Clamp(heightMove, -continuousHeightMove, continuousHeightMove), -.32f, .32f);
        }

        previousFrameRootPosition = transform.position;
        hasRootHistory = true;
        bool leftNativeContact = false;
        bool rightNativeContact = false;
        if (nativeGait != null)
        {
            nativeGait.ReadNativeFootContacts(out leftNativeContact, out rightNativeContact);
        }
        bool injuredMovement = state is LeonFootAction playerFeet && playerFeet.leon.IsInjured
            && !playerFeet.leon.IsCrouching && (state.IsStepping || nativeGait != null && nativeGait.IsNativeGaitMoving);
        UpdateLandPoint(left, rootDisplacement, leftNativeContact, injuredMovement);
        UpdateLandPoint(right, rootDisplacement, rightNativeContact, injuredMovement);
        // 下沉。
float sink = Mathf.Clamp(Mathf.Min(ComputeSink(left), ComputeSink(right)), -StairProbeDepth, 0f);
        pelvisOffset = Mathf.SmoothDamp(pelvisOffset, sink, ref pelvisSpeed, .09f, 2.5f);
        WritePelvis(pelvisOffset + rootHeightBuffer);
        Commit(left, injuredMovement);
        Commit(right, injuredMovement);
        if (handoff != null)
        {
            handoffMargin = 1f;
        }
    }

    /// <summary>WritePelvis 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="offset">偏移量。</param>
    private void WritePelvis(float offset)
    {
        originalPelvisPosition = pelvis.localPosition;
        pelvis.position += Vector3.up * offset;
        writePosition = pelvis.localPosition;
        hasWritePelvis = true;
    }

    /// <summary>FadeExitCorrection 的业务处理入口；本方法直接依赖 BipedFootPlanting.JointCorrection.FadeExit(Transform, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()；下游（含内部回调体） BipedFootPlanting.JointCorrection.FadeExit(Transform, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    /// <param name="preserve">保留。</param>
    /// <param name="startup">是否启动。</param>
    private static void FadeExitCorrection(FootState footPosition, float preserve, bool startup)
    {
        // 松开旧踏面，攻击每帧仍采用当前动画，只逐渐消除上一姿势的关节附加量。
        // 求解。
var solve = footPosition.chain.solver;
        solve.IKPositionWeight = solve.IKRotationWeight = 0f;
        footPosition.largeLeg.FadeExit(solve.bone1.transform, preserve, startup);
        footPosition.smallLeg.FadeExit(solve.bone2.transform, preserve, startup);
        footPosition.soleValue.FadeExit(footPosition.FootPosition, preserve, startup);
    }

    /// <summary>RestoreCloseSection 的业务处理入口；本方法直接依赖 BipedFootPlanting.JointCorrection.Restore(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()、BipedFootPlanting.OnDisable()；下游（含内部回调体） BipedFootPlanting.JointCorrection.Restore(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private static void RestoreCloseSection(FootState footPosition)
    {
        if (footPosition == null || !footPosition.chain)
        {
            return;
        }

        footPosition.largeLeg.Restore(footPosition.chain.solver.bone1.transform);
        footPosition.smallLeg.Restore(footPosition.chain.solver.bone2.transform);
        footPosition.soleValue.Restore(footPosition.FootPosition);
    }

    // 通行桥面会临时托高胶囊，尤其是蹲走时。额外深度只查询已经绑定的可见楼梯。
    /// <summary>楼梯探测深度。</summary>
    private float StairProbeDepth => maximumLandDifference * 2f + .35f;

    private bool FindTread(Vector3 point, out RaycastHit ground)
    {
        // 射线。
var ray = new Ray(point + Vector3.up * maximumLandDifference, Vector3.down);
        ground = default;
        // 数量。
int count = Physics.RaycastNonAlloc(ray, hit, maximumLandDifference * 2f, groundLayer, QueryTriggerInteraction.Ignore);
        // 最新。
float latest = float.PositiveInfinity;
        // 当前遍历索引。
for (int i = 0; i < count; i++)
        {
            // 当前射线命中结果。
var item = hit[i];
            if (item.distance >= latest || item.normal.y < .63f || item.rigidbody || item.transform.IsChildOf(transform) || item.collider.GetComponentInParent<Animator>())
            {
                continue;
            }

            latest = item.distance;
            ground = item;
        }

        return latest < float.PositiveInfinity;
    }

    /// <summary>ResetPose 的业务处理入口；本方法直接依赖 BipedFootPlanting.Clear(BipedFootPlanting.FootState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()、BipedFootPlanting.OnDisable()、BipedFootPlanting.Start()；下游（含内部回调体） BipedFootPlanting.Clear(BipedFootPlanting.FootState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetPose()
    {
        Clear(left);
        Clear(right);
        rootHeightBuffer = rootHeightSettleSpeed = 0f;
        handoffMargin = pelvisOffset = pelvisSpeed = stationaryTime = 0f;
        hasRootHistory = false;
    }

    /// <summary>UpdateLandPoint 的业务处理入口；本方法直接依赖 BipedFootPlanting.FindTread(Vector3, out RaycastHit, out bool)、BipedFootPlanting.FindTread(Vector3, out RaycastHit)、BipedFootPlanting.UpdateMovingSupport(BipedFootPlanting.FootState, Vector3, Vector3, Vector3, float, ref Vector3)、float.IsNegativeInfinity(float)、Mathf.Exp(float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()；下游（含内部回调体） BipedFootPlanting.FindTread(Vector3, out RaycastHit, out bool)、BipedFootPlanting.FindTread(Vector3, out RaycastHit)、BipedFootPlanting.UpdateMovingSupport(BipedFootPlanting.FootState, Vector3, Vector3, Vector3, float, ref Vector3)、float.IsNegativeInfinity(float)、Mathf.Exp(float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    /// <param name="rootDisplacement">根节点位移。</param>
    private void UpdateLandPoint(FootState footPosition, Vector3 rootDisplacement, bool nativeContact, bool injuredMovement)
    {
        // 脚部骨骼。
var footBone = footPosition.FootPosition;
        // 鞋。
var shoe = footBone.TransformPoint(footPosition.sole);
        // 脚部位移。
global::UnityEngine.Vector3 footDisplacement;
        if (footPosition.hasSoleHistory)
        {
            footDisplacement = shoe - footPosition.previousFrameSole;
        }
        else
        {
            footDisplacement = Vector3.zero;
        }

        // 探测前方。
global::UnityEngine.Vector3 probeFront;
        if (footPosition.hasSoleHistory)
        {
            probeFront = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(shoe - footPosition.previousFrameSole, Vector3.up) * (.13f / Time.deltaTime), footPosition.legLong * .72f);
        }
        else
        {
            probeFront = Vector3.zero;
        }

        footPosition.previousFrameSole = shoe;
        footPosition.hasSoleHistory = true;
        // 位移。
var displacement = Vector3.zero;
        // 接触最小。
float contactMinimum = float.NegativeInfinity;
        // 旋转值。
var rotationValue = Quaternion.identity;
        if (stationaryTime < .16f)
        {
            footPosition.lockValue = false;
        }

        // 站稳后保留承重点，呼吸、环顾和脚尖靠近阶沿都不会触发重新选级。
        if (!footPosition.lockValue && FindTread(shoe, out RaycastHit ground))
        {
            footPosition.lastGroundHeight = ground.point.y;
            footPosition.awaySurfaceRemainingTime = .12f;

            if (stationaryTime >= .16f)
            {
                footPosition.lockValue = true;
                footPosition.anchor = ground.point;
                // 常规值。
var normalValue = Vector3.RotateTowards(Vector3.up, ground.normal, 25f * Mathf.Deg2Rad, 0f);
                footPosition.anchorRotation = Quaternion.FromToRotation(footBone.TransformDirection(footPosition.facingUp), normalValue) * footBone.rotation;
            }
            else
            {
                // 保留动画的抬脚高度、前后步幅和脚尖滚动，只叠加落脚处相对角色根部的高差。
                // 高度差值。
float heightDifference = ground.point.y - transform.position.y;
                // 净空。
float clearance = shoe.y - transform.position.y;
                if (footPosition.toe && clearance > .035f)
                {
                    // 脚趾底部。
var toeBottom = shoe + Vector3.ProjectOnPlane(footPosition.toe.position - footBone.position, Vector3.up);
                    if (FindTread(toeBottom, out var /* 中文含义：步进边缘。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stepEdge))
                    {
                        heightDifference = Mathf.Max(heightDifference, stepEdge.point.y + .012f - shoe.y);
                    }
                }

                contactMinimum = ground.point.y - shoe.y;
                displacement.y = Mathf.Max(contactMinimum, heightDifference);
                // 坡度方向。
var slopeDirection = Vector3.RotateTowards(Vector3.up, ground.normal, 18f * Mathf.Deg2Rad, 0f);
                rotationValue = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, slopeDirection), 1f - Mathf.InverseLerp(.025f, .14f, clearance));
            }
        }

        if (!footPosition.lockValue && float.IsNegativeInfinity(contactMinimum) && footPosition.awaySurfaceRemainingTime > 0f)
        {
            // 射线短暂穿过楼板缝隙时沿用上一踏面高度，避免把脚突然抬回胶囊底部。
            footPosition.awaySurfaceRemainingTime -= Time.deltaTime;
            displacement.y = footPosition.lastGroundHeight - transform.position.y;
        }

        // 脚掌在级边或板缝上方也要看到下一踏面。沿摆动轨迹提前留出抬膝时间。
        if (!footPosition.lockValue && probeFront.sqrMagnitude > .000025f)
        {
            // 当前遍历索引。
for (int i = 1; i <= 3; i++)
            {
                if (FindTread(shoe + probeFront * (i / 3f), out var /* 中文含义：前方地面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ frontGround))
                {
                    displacement.y = Mathf.Max(displacement.y, frontGround.point.y - shoe.y + .035f);
                }
            }
        }

        if (movingStableFeet)
        {
            UpdateMovingSupport(footPosition, shoe, footDisplacement, rootDisplacement, contactMinimum, ref displacement);
        }

        if (injuredMovement && !footPosition.lockValue)
        {
            // 伤势步态的摆动脚沿原片抬起，逐渐跟随下降的坡面。
            float nearSurface = Mathf.InverseLerp(0.12f, 0.045f, -contactMinimum);
            float nearAuthoredPlant = Mathf.InverseLerp(0.08f, 0.005f, shoe.y - transform.position.y);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.Max(nearSurface, nearAuthoredPlant));
            displacement.y = Mathf.Lerp(Mathf.Max(0f, displacement.y), displacement.y, settle);
        }

        if (footPosition.lockValue)
        {
            rotationValue = footPosition.anchorRotation * Quaternion.Inverse(footBone.rotation);
            displacement = footPosition.anchor - (footBone.position + rotationValue * footBone.TransformVector(footPosition.sole));
        }

        // 只平滑附加量，动画本身始终在当前帧生效。站稳后精确抵消待机摆动。
        // 赋值前保存的目标脚部位置。
global::BipedFootPlanting.FootState assignmentReceiver6 = // 只平滑附加量，动画本身始终在当前帧生效。站稳后精确抵消待机摆动。
 footPosition;
        if (footPosition.lockValue)
        {
            assignmentReceiver6.lockWeight = Mathf.MoveTowards(footPosition.lockWeight, 1f, Time.deltaTime * 4f);
        }
        else
        {
            assignmentReceiver6.lockWeight = 0f;
        }

        // 是否具有支撑。
bool hasSupport = footPosition.lockValue || footPosition.awaySurfaceRemainingTime > 0f;
        // 赋值前保存的目标脚部位置。
global::BipedFootPlanting.FootState assignmentReceiver8 = footPosition;
        // 按原顺序缓存的实参脚部位置的地面权重。
float argumentValue10 = footPosition.groundWeight;
        // 分支选出的1f。
float selectedValue9;
        if (hasSupport)
        {
            selectedValue9 = 1f;
        }
        else
        {
            selectedValue9 = 0f;
        }

        assignmentReceiver8.groundWeight = Mathf.MoveTowards(argumentValue10, selectedValue9, 8f * Time.deltaTime);
        // 预期穿透进入。
float expectedPenetrationEnter = contactMinimum - footPosition.displacementCorrection.y;
        // 缓冲区。
float buffer = Mathf.Lerp(.075f, .028f, Mathf.InverseLerp(0f, .045f, expectedPenetrationEnter));
        footPosition.displacementCorrection = Vector3.SmoothDamp(footPosition.displacementCorrection, displacement, ref footPosition.correctionSpeed, buffer, 5f);
        footPosition.displacementCorrection = Vector3.Lerp(footPosition.displacementCorrection, displacement, Mathf.Max(footPosition.lockWeight, footPosition.supportBlend));
        footPosition.rotationCorrection = Quaternion.Slerp(footPosition.rotationCorrection, rotationValue, Mathf.Lerp(1f - Mathf.Exp(-16f * Time.deltaTime), 1f, footPosition.lockWeight));
        footPosition.ankleTarget = footBone.position + footPosition.displacementCorrection;
        footPosition.targetRotation = footPosition.rotationCorrection * footBone.rotation;
        bool canAnchorToe = nativeContact && !footPosition.lockValue
            && !float.IsNegativeInfinity(contactMinimum) && footPosition.groundWeight > .9f;
        if (injuredMovement)
        {
            float clearanceLimit = 0.045f;
            if (footPosition.nativeContact)
            {
                clearanceLimit = 0.075f;
            }
            float correctedClearance = footPosition.displacementCorrection.y - contactMinimum;
            canAnchorToe &= correctedClearance <= clearanceLimit;
        }
        ApplyNativeToeSupport(footPosition, canAnchorToe, injuredMovement);
        if (state is LeonFootAction playerFeet && playerFeet.leon.IsInjured && footPosition.toe != null)
        {
            // 伤势步态拖脚时，鞋底中点可能还在地面上，但鞋尖已经碰到坡面。
            Vector3 toeTarget = footPosition.ankleTarget + footPosition.rotationCorrection * (footPosition.toe.position - footBone.position);
            if (FindTread(toeTarget, out RaycastHit toeGround))
            {
                float lift = Mathf.Clamp(toeGround.point.y + 0.008f - toeTarget.y, 0f, 0.09f);
                footPosition.ankleTarget += Vector3.up * lift;
            }
        }
    }

    private static void ApplyNativeToeSupport(FootState foot, bool contact, bool injuredMovement)
    {
        if (contact && foot.toe != null)
        {
            var toeFromAnkle = foot.toe.position - foot.FootPosition.position;
            var toeTarget = foot.ankleTarget + foot.rotationCorrection * toeFromAnkle;
            if (!foot.nativeContact)
            {
                foot.nativeToeAnchor = toeTarget + foot.nativeToeCorrection * foot.nativeContactWeight;
            }
            foot.nativeContact = true;
            float maximumCorrection = 0.18f;
            if (injuredMovement)
            {
                foot.nativeContactWeight = Mathf.MoveTowards(foot.nativeContactWeight, 1f, Time.deltaTime / 0.065f);
                maximumCorrection = 0.11f;
            }
            else
            {
                foot.nativeContactWeight = 1f;
            }
            foot.nativeToeCorrection = Vector3.ClampMagnitude(
                Vector3.ProjectOnPlane(foot.nativeToeAnchor - toeTarget, Vector3.up), maximumCorrection);
        }
        else
        {
            foot.nativeContact = false;
            foot.nativeContactWeight = Mathf.MoveTowards(foot.nativeContactWeight, 0f, Time.deltaTime / .09f);
        }
        foot.ankleTarget += foot.nativeToeCorrection * foot.nativeContactWeight;
    }

    /// <summary>UpdateMovingSupport 的业务处理入口；本方法直接依赖 float.IsNegativeInfinity(float)、Mathf.Abs(float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)、Transform.InverseTransformPoint(Vector3)、Vector3.Dot(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.UpdateLandPoint(BipedFootPlanting.FootState, Vector3)；下游（含内部回调体） float.IsNegativeInfinity(float)、Mathf.Abs(float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)、Transform.InverseTransformPoint(Vector3)、Vector3.Dot(Vector3, Vector3)、Vector3.Lerp(Vector3, Vector3, float) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    /// <param name="shoe">鞋。</param>
    /// <param name="footDisplacement">脚部位移。</param>
    /// <param name="rootDisplacement">根节点位移。</param>
    /// <param name="contactMinimum">接触最小。</param>
    /// <param name="displacement">位移。按引用传入，读写会作用于调用方原存储位置。</param>
    private void UpdateMovingSupport(FootState footPosition, Vector3 shoe, Vector3 footDisplacement, Vector3 rootDisplacement, float contactMinimum, ref Vector3 displacement)
    {
        // 行走速度。
float walkSpeed = rootDisplacement.magnitude / Time.deltaTime;
        // 后方移动。
float backMove = Vector3.Dot(rootDisplacement - footDisplacement, rootDisplacement.normalized) / Time.deltaTime;
        // 远处角色可能隔帧写骨骼。沿用两个真实动画采样间的步相，
        // 避免把重复的姿势误认作每隔一帧就抬起了支撑脚。
        // 局部鞋。
var localShoe = transform.InverseTransformPoint(shoe);
        if (footPosition.hasStepRelative && (localShoe - footPosition.lastLocalShoe).sqrMagnitude < .00000004f)
        {
            footPosition.animationHoldTime += Time.deltaTime;
            if (footPosition.animationHoldTime < .075f)
            {
                backMove = footPosition.lastBackMoveSpeed;
            }
            else
            {
                backMove = 0f;
            }
        }
        else
        {
            backMove *= Time.deltaTime / (Time.deltaTime + footPosition.animationHoldTime);
            footPosition.lastBackMoveSpeed = backMove;
            footPosition.animationHoldTime = 0f;
            footPosition.lastLocalShoe = localShoe;
            footPosition.hasStepRelative = true;
        }

        // 抬高高度。
float raiseHeight = shoe.y - transform.position.y;
        // 是否可以支撑权重。
bool canSupportWeight = !footPosition.lockValue && walkSpeed > .12f && walkSpeed < 2.8f && !float.IsNegativeInfinity(contactMinimum);
        if (!canSupportWeight || backMove < .08f * walkSpeed || raiseHeight > footPosition.legLong * .19f)
        {
            footPosition.firmFooting = false;
            footPosition.waitingSwing = false;
        }
        else if (!footPosition.firmFooting && !footPosition.waitingSwing && backMove > .30f * walkSpeed && raiseHeight < footPosition.legLong * .13f)
        {
            // 入口点。
var entryPoint = shoe;
            if (footPosition.hasHasSolveSole && Mathf.Abs(footPosition.hasSolveSole.y - footPosition.lastGroundHeight) < .035f && Vector3.ProjectOnPlane(footPosition.hasSolveSole - shoe, Vector3.up).magnitude < footPosition.legLong * .22f)
            {
                entryPoint = footPosition.hasSolveSole;
            }

            footPosition.supportPoint = new Vector3(entryPoint.x, footPosition.lastGroundHeight, entryPoint.z);
            footPosition.firmFooting = true;
        }

        if (footPosition.firmFooting && (Vector3.ProjectOnPlane(footPosition.supportPoint - shoe, Vector3.up).magnitude > footPosition.legLong * .25f || Mathf.Abs(footPosition.lastGroundHeight - footPosition.supportPoint.y) > .08f))
        {
            footPosition.firmFooting = false;
            footPosition.waitingSwing = true;
        }

        // 赋值前保存的目标脚部位置。
global::BipedFootPlanting.FootState assignmentReceiver12 = footPosition;
        // 按原顺序缓存的实参脚部位置的支撑混合。
float argumentValue14 = footPosition.supportBlend;
        // 分支选出的1f。
float selectedValue13;
        if (footPosition.firmFooting)
        {
            selectedValue13 = 1f;
        }
        else
        {
            selectedValue13 = 0f;
        }

        assignmentReceiver12.supportBlend = Mathf.MoveTowards(argumentValue14, selectedValue13, Time.deltaTime * 12f);
        if (footPosition.supportBlend > 0f)
        {
            displacement = Vector3.Lerp(displacement, footPosition.supportPoint - shoe, Mathf.SmoothStep(0f, 1f, footPosition.supportBlend));
        }
    }

    /// <summary>ComputeSink 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.Sqrt(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.Sqrt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private static float ComputeSink(FootState footPosition)
    {
        // 跨越距离。
var crossDistance = footPosition.chain.solver.bone1.transform.position - footPosition.ankleTarget;
        // 可抵达。
float reachable = footPosition.legLong * .967f;
        // 垂直于距离。
float perpendicularDistance = Mathf.Sqrt(Mathf.Max(0f, reachable * reachable - crossDistance.x * crossDistance.x - crossDistance.z * crossDistance.z));
        return Mathf.Min(0f, perpendicularDistance - crossDistance.y) * footPosition.groundWeight;
    }

    /// <summary>Commit 的业务处理入口；本方法直接依赖 BipedFootPlanting.FindTread(Vector3, out RaycastHit)、BipedFootPlanting.JointCorrection.Read(Transform)、BipedFootPlanting.JointCorrection.Record(Transform)、BipedFootPlanting.LimitKneeBendDiscontinuity(BipedFootPlanting.FootState)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.LateUpdate()；下游（含内部回调体） BipedFootPlanting.FindTread(Vector3, out RaycastHit)、BipedFootPlanting.JointCorrection.Read(Transform)、BipedFootPlanting.JointCorrection.Record(Transform)、BipedFootPlanting.LimitKneeBendDiscontinuity(BipedFootPlanting.FootState)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private void Commit(FootState footPosition, bool injuredMovement)
    {
        // 求解。
var solve = footPosition.chain.solver;
        // 骨盆尚在追随下一阶时，给膝盖留少许弯曲，防止伸直附近一帧翻折。
        // 髋部。
var hips = solve.bone1.transform.position;
        float reach = 0.992f;
        if (injuredMovement)
        {
            reach = 0.98f;
        }
        solve.IKPosition = hips + Vector3.ClampMagnitude(footPosition.ankleTarget - hips, footPosition.legLong * reach);
        // 摆脚仍低于立边时，缩短这一拍的水平步幅，给脚抬过边缘的时间。
        if (!footPosition.lockValue && footPosition.hasHasSolveSole && footPosition.groundWeight > .99f)
        {
            // 鞋偏移量。
var shoeOffset = footPosition.targetRotation * Quaternion.Inverse(footPosition.FootPosition.rotation) * footPosition.FootPosition.TransformVector(footPosition.sole);
            // 动画点。
var animationPoint = solve.IKPosition + shoeOffset;
            // 稳定。
var settle = Vector3.MoveTowards(footPosition.stepEdgeYield, Vector3.zero, 2.6f * Time.deltaTime);
            // 鞋点。
var shoePoint = animationPoint + settle;
            footPosition.supportWeightRaiseRaise = Mathf.MoveTowards(footPosition.supportWeightRaiseRaise, 0f, Time.deltaTime * .6f);
            if (FindTread(shoePoint, out var /* 中文含义：障碍。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ obstacle) && obstacle.point.y > shoePoint.y + .002f)
            {
                // 起始点。
var startPoint = new Vector3(footPosition.hasSolveSole.x, shoePoint.y, footPosition.hasSolveSole.z);
                // 是否具有前一次表面。
bool hasPreviousSurface = FindTread(startPoint, out var /* 中文含义：前一次表面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ previousSurface);
                if (hasPreviousSurface && Mathf.Abs(previousSurface.point.y - obstacle.point.y) < .035f)
                {
                    // 同一踏面上的骨盆下降直接由支撑脚承接，抬升量只补偿微小穿入。
                    footPosition.supportWeightRaiseRaise = Mathf.Max(footPosition.supportWeightRaiseRaise, Mathf.Min(.035f, obstacle.point.y - shoePoint.y + .001f));
                }
                else if (hasPreviousSurface && previousSurface.point.y <= shoePoint.y + .002f)
                {
                    // 高度侧边。
var heightSide = shoePoint;
                    // 当前遍历索引。
for (int i = 0; i < 5; i++)
                    {
                        // 中点。
var midpoint = (heightSide + startPoint) * .5f;
                        if (FindTread(midpoint, out var /* 中文含义：启用路线。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enRoute) && enRoute.point.y > shoePoint.y + .002f)
                        {
                            heightSide = midpoint;
                        }
                        else
                        {
                            startPoint = midpoint;
                        }
                    }

                    settle = Vector3.ClampMagnitude(startPoint - animationPoint, footPosition.legLong * .25f);
                }
            }

            footPosition.stepEdgeYield = settle;
            solve.IKPosition += settle + Vector3.up * footPosition.supportWeightRaiseRaise;
        }
        else
        {
            footPosition.stepEdgeYield = Vector3.zero;
            footPosition.supportWeightRaiseRaise = 0f;
        }

        if ((movingStableFeet || injuredMovement) && footPosition.hasHasSolveSole && footPosition.groundWeight > .99f && !footPosition.lockValue)
        {
            LimitKneeBendDiscontinuity(footPosition);
        }

        solve.IKRotation = footPosition.targetRotation;
        solve.IKPositionWeight = solve.IKRotationWeight = footPosition.groundWeight;
        if (handoff != null || nativeGait != null)
        {
            footPosition.largeLeg.Read(solve.bone1.transform);
            footPosition.smallLeg.Read(solve.bone2.transform);
            footPosition.soleValue.Read(footPosition.FootPosition);
            footPosition.chain.UpdateSolverExternal();
            footPosition.largeLeg.Record(solve.bone1.transform);
            footPosition.smallLeg.Record(solve.bone2.transform);
            footPosition.soleValue.Record(footPosition.FootPosition);
        }
        else
        {
            footPosition.chain.UpdateSolverExternal();
        }

        if (movingStableFeet || injuredMovement)
        {
            // 膝盖。
var knee = solve.bone2.transform.position;
            // 当前膝盖弯曲。
float currentKneeBend = Vector3.Angle(knee - hips, footPosition.FootPosition.position - knee);
            // 赋值前保存的目标脚部位置。
global::BipedFootPlanting.FootState assignmentReceiver15 = footPosition;
            if (footPosition.hasHasSolveSole)
            {
                assignmentReceiver15.kneeBendSpeed = (currentKneeBend - footPosition.previousFrameKneeBend) / Time.deltaTime;
            }
            else
            {
                assignmentReceiver15.kneeBendSpeed = 0f;
            }

            footPosition.previousFrameKneeBend = currentKneeBend;
        }

        footPosition.hasSolveSole = footPosition.FootPosition.TransformPoint(footPosition.sole);
        footPosition.hasHasSolveSole = true;
    }

    /// <summary>LimitKneeBendDiscontinuity 的业务处理入口；本方法直接依赖 BipedFootPlanting.FindTread(Vector3, out RaycastHit)、Mathf.Abs(float)、Mathf.Acos(float)、Mathf.Clamp(float, float, float)、Mathf.Cos(float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.Commit(BipedFootPlanting.FootState)；下游（含内部回调体） BipedFootPlanting.FindTread(Vector3, out RaycastHit)、Mathf.Abs(float)、Mathf.Acos(float)、Mathf.Clamp(float, float, float)、Mathf.Cos(float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private void LimitKneeBendDiscontinuity(FootState footPosition)
    {
        // 在最终踝目标上约束突然折膝的角速度，正常步幅仍可直接到达踏面。
        // 求解。
var solve = footPosition.chain.solver;
        // 髋部。
var hips = solve.bone1.transform.position;
        // 膝盖。
var knee = solve.bone2.transform.position;
        // 开火中方向。
var firingDirection = solve.IKPosition - hips;
        // 较大腿部长型。
float largeLegLong = Vector3.Distance(hips, knee);
        // 较小腿部长型。
float smallLegLong = Vector3.Distance(knee, footPosition.FootPosition.position);
        // 总和所属平方值集合。
float sumOfSquares = largeLegLong * largeLegLong + smallLegLong * smallLegLong;
        // 双重乘积。
float doubleProduct = 2f * largeLegLong * smallLegLong;
        if (doubleProduct < .0001f || firingDirection.sqrMagnitude < .0001f)
        {
            return;
        }

        // 预期。
float expected = Mathf.Acos(Mathf.Clamp((firingDirection.sqrMagnitude - sumOfSquares) / doubleProduct, -1f, 1f)) * Mathf.Rad2Deg;
        // 步进尺寸。
float stepSize = Time.deltaTime;
        // 预期速度。
float expectedSpeed = (expected - footPosition.previousFrameKneeBend) / stepSize;
        // 速度限制。
float speedLimit = Mathf.Clamp(Mathf.MoveTowards(footPosition.kneeBendSpeed, expectedSpeed, 18500f * stepSize), -520f, 520f);
        // 即使膝角没有超速，前一段防穿的退让或腿长限制仍可能让鞋点落在阶沿下。
        // 是否常规屈曲扩展名。
bool normalFlexionExtension = Mathf.Abs(speedLimit - expectedSpeed) < .01f;
        if (normalFlexionExtension)
        {
            return;
        }

        // 膝盖弯曲。
float kneeBend = Mathf.Clamp(footPosition.previousFrameKneeBend + speedLimit * stepSize, 8f, 155f) * Mathf.Deg2Rad;
        // 半径。
float radius = Mathf.Sqrt(Mathf.Max(0f, sumOfSquares + doubleProduct * Mathf.Cos(kneeBend)));
        // 缓冲区目标。
global::UnityEngine.Vector3 bufferTarget;
        if (normalFlexionExtension)
        {
            bufferTarget = solve.IKPosition;
        }
        else
        {
            bufferTarget = hips + firingDirection.normalized * radius;
        }

        // 鞋偏移量。
var shoeOffset = footPosition.targetRotation * Quaternion.Inverse(footPosition.FootPosition.rotation) * footPosition.FootPosition.TransformVector(footPosition.sole);
        // 鞋点。
var shoePoint = bufferTarget + shoeOffset;
        if (FindTread(shoePoint, out var /* 中文含义：踏面。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ tread) && shoePoint.y < tread.point.y)
        {
            if (solve.IKPosition.y + shoeOffset.y >= tread.point.y - .004f)
            {
                bufferTarget.y += tread.point.y - shoePoint.y;
                solve.IKPosition = bufferTarget;
            }

            return;
        }

        solve.IKPosition = bufferTarget;
    }

    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BipedFootPlanting.DrawLandPoint(BipedFootPlanting.FootState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDrawGizmosSelected()
    {
        DrawLandPoint(left);
        DrawLandPoint(right);
    }

    /// <summary>DrawLandPoint 的业务处理入口；本方法直接依赖 Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireSphere(Vector3, float)、Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BipedFootPlanting.OnDrawGizmosSelected()；下游（含内部回调体） Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireSphere(Vector3, float)、Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="footPosition">脚部位置。</param>
    private static void DrawLandPoint(FootState footPosition)
    {
        if (footPosition == null || !footPosition.chain)
        {
            return;
        }

        // 实际。
Vector3 actual = footPosition.FootPosition.TransformPoint(footPosition.sole);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(actual, .018f);
        if (!footPosition.lockValue)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(footPosition.anchor, .025f);
        Gizmos.DrawLine(actual, footPosition.anchor);
    }
}
