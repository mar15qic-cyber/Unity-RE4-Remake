using System.Collections.Generic;
using RootMotion.FinalIK;
using UnityEngine;

/// <summary>敌人行为、动作与通行组件 EnemyFootAction，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(8900)]
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人足部动作")]
public sealed class EnemyFootAction : MonoBehaviour, IFootActionState, IFootActionHandoff
{
    /// <summary>中文含义：敌人。使用位置：EnemyFootAction.AllowGroundContact.get、EnemyFootAction.AttackStartupMiddle.get、EnemyFootAction.LateUpdate() 等共 4 处。</summary>
    private VillagerController enemy;
    /// <summary>中文含义：双重脚部。使用位置：作为 EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3) 的参数、EnemyFootAction.LateUpdate()、EnemyFootAction.Start()。</summary>
    private BipedFootPlanting dualFoot;
    /// <summary>中文含义：是否需要权重绑定。使用位置：EnemyFootAction.LateUpdate()、EnemyFootAction.OnEnable()。</summary>
    private bool needsGroundContactReset = true;
    /// <summary>中文含义：前一次帧位置。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、EnemyFootAction.LateUpdate()、EnemyFootAction.Start()。</summary>
    private Vector3 previousFramePosition;
    /// <summary>中文含义：是否迈步中。使用位置：EnemyFootAction.LateUpdate()。</summary>
    public bool IsStepping { get; private set; }
    /// <summary>中文含义：是否攻击启动中间。</summary>
    public bool AttackStartupMiddle => enemy && enemy.IsActive && !enemy.IsDead && !enemy.HasPendingHit && !enemy.IsPermanentlyCrawling && !enemy.IsQTEControlled && enemy.CurrentState == VillagerState.Attack;
    /// <summary>中文含义：是否允许地面接触。</summary>
    public bool AllowGroundContact => BipedFootPlanting.EnemyGroundIKEnabled && enemy && enemy.isActiveAndEnabled && enemy.IsRuntimeInitialized && enemy.IsActive && !enemy.IsDead && !enemy.IsPermanentlyCrawling && !enemy.HasPendingHit && !enemy.IsQTEControlled && !(enemy.ArchitectureReadSpecialTraversal && enemy.ArchitectureReadSpecialTraversal.IsMovementProtected) && enemy.CurrentMovementMode is EnemyMovementMode.StandingPathRootMotion or EnemyMovementMode.FullyStopped && enemy.CurrentState is VillagerState.Idle or VillagerState.Alert or VillagerState.Patrol or VillagerState.Chase;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;BipedFootPlanting&gt;()、Component.GetComponent&lt;VillagerController&gt;()、Dictionary&lt;string, Quaternion&gt;.Dictionary()、EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)、GameObject.AddComponent&lt;BipedFootPlanting&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void Start()
    {
        enemy = GetComponent<VillagerController>();
        // animation（动画）：保存 Component.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3) 的参数、EnemyFootAction.Start()。
var animation = GetComponent<Animator>();
        dualFoot = GetComponent<BipedFootPlanting>();
        if (!BipedFootPlanting.EnemyGroundIKEnabled || !animation || !animation.isHuman || dualFoot)
        {
            return;
        }

        // staticRotation（静态旋转）：使用位置：作为 EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3) 的参数、EnemyFootAction.Start()。
var staticRotation = new Dictionary<string, Quaternion>();
        // bone（骨骼）：每轮从动画的人形骨架的人形描述的骨架取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<string, Quaternion>.this[string] 的参数、EnemyFootAction.Start()。
foreach (var bone in animation.avatar.humanDescription.skeleton)
        {
            staticRotation[bone.name] = bone.rotation;
        }

        dualFoot = gameObject.AddComponent<BipedFootPlanting>();
        dualFoot.movingStableFeet = true;
        dualFoot.actionSource = this;
        dualFoot.pelvis = animation.GetBoneTransform(HumanBodyBones.Hips);
        dualFoot.leftToes = animation.GetBoneTransform(HumanBodyBones.LeftToes);
        dualFoot.rightToes = animation.GetBoneTransform(HumanBodyBones.RightToes);
        dualFoot.leftLeg = BindingLeg(animation, staticRotation, true, out dualFoot.leftSole, out dualFoot.leftSoleFacingUp);
        dualFoot.rightLeg = BindingLeg(animation, staticRotation, false, out dualFoot.rightSole, out dualFoot.rightSoleFacingUp);
        previousFramePosition = transform.position;
    }

    /// <summary>BindingLeg 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Dictionary&lt;string, Quaternion&gt;.TryGetValue(string, out Quaternion)、GameObject.AddComponent&lt;LimbIK&gt;()、Mathf.Max(float, float)、Quaternion.Inverse(Quaternion)、Transform.TransformVector(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyFootAction.Start()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Dictionary&lt;string, Quaternion&gt;.TryGetValue(string, out Quaternion)、GameObject.AddComponent&lt;LimbIK&gt;()、Mathf.Max(float, float)、Quaternion.Inverse(Quaternion)、Transform.TransformVector(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animation">中文含义：动画。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)。</param>
    /// <param name="staticRotation">中文含义：静态旋转。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)。</param>
    /// <param name="left">中文含义：是否左侧。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)。</param>
    /// <param name="sole">中文含义：脚底。输出参数，由本入口写入并交给调用方。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)。</param>
    /// <param name="facingUp">中文含义：朝向向上。输出参数，由本入口写入并交给调用方。使用位置：作为 Transform.TransformVector(Vector3) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary&lt;string, Quaternion&gt;, bool, out Vector3, out Vector3)。</param>
    private LimbIK BindingLeg(Animator animation, Dictionary<string, Quaternion> staticRotation, bool left, out Vector3 sole, out Vector3 facingUp)
    {
        // callReceiver2（调用前缓存的动画）：从动画读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.Animator callReceiver2 = animation;
        // selectedValue1（分支选出的人形身体骨骼集合的左侧大腿）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.HumanBodyBones selectedValue1;
        if (left)
        {
            selectedValue1 = HumanBodyBones.LeftUpperLeg;
        }
        else
        {
            selectedValue1 = HumanBodyBones.RightUpperLeg;
        }

        // largeLeg（较大腿部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var largeLeg = callReceiver2.GetBoneTransform(selectedValue1);
        // callReceiver4（调用前缓存的动画）：从动画读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.Animator callReceiver4 = animation;
        // selectedValue3（分支选出的人形身体骨骼集合的左侧小腿）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.HumanBodyBones selectedValue3;
        if (left)
        {
            selectedValue3 = HumanBodyBones.LeftLowerLeg;
        }
        else
        {
            selectedValue3 = HumanBodyBones.RightLowerLeg;
        }

        // smallLeg（较小腿部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var smallLeg = callReceiver4.GetBoneTransform(selectedValue3);
        // callReceiver6（调用前缓存的动画）：从动画读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.Animator callReceiver6 = animation;
        // selectedValue5（分支选出的人形身体骨骼集合的左脚）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.HumanBodyBones selectedValue5;
        if (left)
        {
            selectedValue5 = HumanBodyBones.LeftFoot;
        }
        else
        {
            selectedValue5 = HumanBodyBones.RightFoot;
        }

        // footPosition（脚部位置）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var footPosition = callReceiver6.GetBoneTransform(selectedValue5);
        // 从人形骨架的参考姿势标定鞋底，不把生成时正在播放的动画姿势当作站姿。
        // orientation（朝向）：使用位置：作为 Quaternion.Inverse(Quaternion) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var orientation = Quaternion.identity;
        // bone（骨骼）：使用位置：作为 Dictionary<string, Quaternion>.TryGetValue(string, out Quaternion) 的参数、EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
for (var bone = footPosition; bone && bone != animation.transform; bone = bone.parent)
        {
            // selectedValue7（分支选出的旋转角度）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::UnityEngine.Quaternion selectedValue7;
            if (staticRotation.TryGetValue(bone.name, out var /* 中文含义：旋转角度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ rotateAngle))
            {
                selectedValue7 = rotateAngle;
            }
            else
            {
                selectedValue7 = bone.localRotation;
            }

            orientation = (selectedValue7) * orientation;
        }

        facingUp = Quaternion.Inverse(orientation) * Vector3.up;
        // thickness（厚度）：使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
float thickness;
        if (left)
        {
            thickness = animation.leftFeetBottomHeight;
        }
        else
        {
            thickness = animation.rightFeetBottomHeight;
        }

        sole = -facingUp * (thickness / Mathf.Max(.001f, footPosition.TransformVector(facingUp).magnitude));
        // chain（链条）：保存 GameObject.AddComponent<LimbIK>() 的返回结果，供当前步骤使用。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var chain = largeLeg.gameObject.AddComponent<LimbIK>();
        chain.fixTransforms = false;
        // solve（求解）：使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
var solve = chain.solver;
        // assignmentReceiver9（赋值前保存的目标求解）：从求解读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyFootAction.BindingLeg(Animator, Dictionary<string, Quaternion>, bool, out Vector3, out Vector3)。
global::RootMotion.FinalIK.IKSolverLimb assignmentReceiver9 = solve;
        if (left)
        {
            assignmentReceiver9.goal = AvatarIKGoal.LeftFoot;
        }
        else
        {
            assignmentReceiver9.goal = AvatarIKGoal.RightFoot;
        }

        solve.bendModifier = IKSolverLimb.BendModifier.Animation;
        solve.bendNormal = transform.right;
        solve.bone1.transform = largeLeg;
        solve.bone2.transform = smallLeg;
        solve.bone3.transform = footPosition;
        solve.IKPositionWeight = solve.IKRotationWeight = 0f;
        return chain;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable() => needsGroundContactReset = true;
    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BipedFootPlanting.ResetGroundContact()、Vector3.ProjectOnPlane(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LateUpdate()
    {
        if (!enemy || !dualFoot || Time.deltaTime <= 0f)
        {
            return;
        }

        if (needsGroundContactReset)
        {
            dualFoot.ResetGroundContact();
            previousFramePosition = transform.position;
            IsStepping = false;
            needsGroundContactReset = false;
        }

        // translation（平移）：保存 Vector3.ProjectOnPlane(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：EnemyFootAction.LateUpdate()。
var translation = Vector3.ProjectOnPlane(transform.position - previousFramePosition, Vector3.up);
        // 原地跺脚仍有摆动脚，只在真正待机时锁定双脚。
        // chainsaw（电锯）：使用位置：EnemyFootAction.LateUpdate()。
var chainsaw = enemy.ArchitectureReadChainsawBridge;
        IsStepping = translation.magnitude > .025f * Time.deltaTime || (chainsaw && chainsaw.InPlaceIntimidationMiddle);
        previousFramePosition = transform.position;
    }
}
