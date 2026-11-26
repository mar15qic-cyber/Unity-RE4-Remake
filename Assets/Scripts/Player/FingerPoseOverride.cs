using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 手指姿态覆盖：在 Animator 更新后覆盖手指骨骼旋转，精准握枪
/// 挂在 LeonR4R 上，LateUpdate 中执行（Animator 之后）
/// </summary>
public class FingerPoseOverride : MonoBehaviour
{
    /// <summary>权重。</summary>
    [Header("权重")]
    [Range(0f, 1f)]
    public float weight = 1f;
    /// <summary>中文含义：混合速度。</summary>
    public float blendSpeed = 8f;
    /// <summary>索引旋转。</summary>
    [Header("食指（扣扳机：伸直搭在扳机护圈上）")]
    public Vector3 indexA_Rot = new Vector3(10f, 30f, 0f);
    /// <summary>索引旋转。</summary>
    public Vector3 indexB_Rot = new Vector3(15f, 0f, 0f);
    /// <summary>索引旋转。</summary>
    public Vector3 indexC_Rot = new Vector3(10f, 0f, 0f);
    /// <summary>中间旋转。</summary>
    [Header("中指（握紧枪柄）")]
    public Vector3 middleA_Rot = new Vector3(20f, 48f, 0f);
    /// <summary>中间旋转。</summary>
    public Vector3 middleB_Rot = new Vector3(50f, 0f, 0f);
    /// <summary>中间旋转。</summary>
    public Vector3 middleC_Rot = new Vector3(20f, 0f, 0f);
    /// <summary>环旋转。</summary>
    [Header("无名指（握紧枪柄）")]
    public Vector3 ringA_Rot = new Vector3(0f, 342f, 7f);
    /// <summary>环旋转。</summary>
    public Vector3 ringB_Rot = new Vector3(55f, 0f, 0f);
    /// <summary>环旋转。</summary>
    public Vector3 ringC_Rot = new Vector3(20f, 0f, 0f);
    /// <summary>小指旋转。</summary>
    [Header("小指（握紧枪柄）")]
    public Vector3 pinkyA_Rot = new Vector3(11f, 357f, 10f);
    /// <summary>小指旋转。</summary>
    public Vector3 pinkyB_Rot = new Vector3(50f, 0f, 0f);
    /// <summary>小指旋转。</summary>
    public Vector3 pinkyC_Rot = new Vector3(25f, 0f, 0f);
    /// <summary>拇指旋转。</summary>
    [Header("拇指（搭在保险/握把上）")]
    public Vector3 thumbA_Rot = new Vector3(38f, 294f, 343f);
    /// <summary>拇指旋转。</summary>
    public Vector3 thumbB_Rot = new Vector3(15f, 46f, 330f);
    /// <summary>拇指旋转。</summary>
    public Vector3 thumbC_Rot = new Vector3(10f, 0f, 0f);
    /// <summary>动画控制器。</summary>
    private Animator anim;
    private LeonAnimationActionCoordinator pickupCoordinator;
    /// <summary>indexA 表示“索引起点”；indexB 表示“索引终点”；indexC 表示“索引候选值”。</summary>
    private Transform indexA, indexB, indexC;
    /// <summary>middleA 表示“中间起点”；middleB 表示“中间终点”；middleC 表示“中间候选值”。</summary>
    private Transform middleA, middleB, middleC;
    /// <summary>ringA 表示“环起点”；ringB 表示“环终点”；ringC 表示“环候选值”。</summary>
    private Transform ringA, ringB, ringC;
    /// <summary>pinkyA 表示“小指起点”；pinkyB 表示“小指终点”；pinkyC 表示“小指候选值”。</summary>
    private Transform pinkyA, pinkyB, pinkyC;
    /// <summary>thumbA 表示“拇指起点”；thumbB 表示“拇指终点”；thumbC 表示“拇指候选值”。</summary>
    private Transform thumbA, thumbB, thumbC;
    /// <summary>手指骨骼集合。</summary>
    private List<(Transform /* 中文含义：元组中的骨骼。 */ bone, Vector3 /* 中文含义：目标骨骼的旋转值。 */ targetRot)> fingerBones = new();
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Animator&gt;()、FingerPoseOverride.Register(Transform, Vector3)、object.ReferenceEquals(object, object)、Transform.Find(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        anim = GetComponent<Animator>();
        pickupCoordinator = GetComponent<LeonAnimationActionCoordinator>();
        // 右手。
var rightHand = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.RightHand);
        if (rightHand == null)
        {
            return;
        }

        indexA = rightHand.Find("arm right finger index a");
        indexB = rightHand.Find("arm right finger index a/arm right finger index b");
        indexC = rightHand.Find("arm right finger index a/arm right finger index b/arm right finger index c");
        middleA = rightHand.Find("arm right finger middle a");
        middleB = rightHand.Find("arm right finger middle a/arm right finger middle b");
        middleC = rightHand.Find("arm right finger middle a/arm right finger middle b/arm right finger middle c");
        thumbA = rightHand.Find("arm right finger thumb a");
        thumbB = rightHand.Find("arm right finger thumb a/arm right finger thumb b");
        thumbC = rightHand.Find("arm right finger thumb a/arm right finger thumb b/arm right finger thumb c");
        // 手掌。
var palm = rightHand.Find("arm right palm");
        if (palm != null)
        {
            pinkyA = palm.Find("arm right finger pinky a");
            // 判空前缓存的小指起点。
global::UnityEngine.Transform receiverValue1 = pinkyA;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                pinkyB = receiverValue1.Find("arm right finger pinky b");
            }
            else
            {
                pinkyB = default;
            }

            // 判空前缓存的小指终点。
global::UnityEngine.Transform receiverValue3 = pinkyB;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                pinkyC = receiverValue3.Find("arm right finger pinky c");
            }
            else
            {
                pinkyC = default;
            }

            ringA = palm.Find("arm right finger ring a");
            // 判空前缓存的环起点。
global::UnityEngine.Transform receiverValue5 = ringA;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                ringB = receiverValue5.Find("arm right finger ring b");
            }
            else
            {
                ringB = default;
            }

            // 判空前缓存的环终点。
global::UnityEngine.Transform receiverValue7 = ringB;
            if (!global::System.Object.ReferenceEquals(receiverValue7, null))
            {
                ringC = receiverValue7.Find("arm right finger ring c");
            }
            else
            {
                ringC = default;
            }
        }

        // 注册需要覆盖的骨骼
        Register(indexA, indexA_Rot);
        Register(indexB, indexB_Rot);
        Register(indexC, indexC_Rot);
        Register(middleA, middleA_Rot);
        Register(middleB, middleB_Rot);
        Register(middleC, middleC_Rot);
        Register(ringA, ringA_Rot);
        Register(ringB, ringB_Rot);
        Register(ringC, ringC_Rot);
        Register(pinkyA, pinkyA_Rot);
        Register(pinkyB, pinkyB_Rot);
        Register(pinkyC, pinkyC_Rot);
        Register(thumbA, thumbA_Rot);
        Register(thumbB, thumbB_Rot);
        Register(thumbC, thumbC_Rot);
    }

    /// <summary>Register 的业务处理入口；本方法直接依赖 List&lt;(Transform bone, Vector3 targetRot)&gt;.Add((Transform bone, Vector3 targetRot))，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FingerPoseOverride.Start()；下游（含内部回调体） List&lt;(Transform bone, Vector3 targetRot)&gt;.Add((Transform bone, Vector3 targetRot))。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">骨骼。</param>
    /// <param name="targetRot">目标旋转。</param>
    void Register(Transform bone, Vector3 targetRot)
    {
        if (bone != null)
        {
            fingerBones.Add((bone, targetRot));
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Quaternion.Euler(Vector3)、Quaternion.Slerp(Quaternion, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (weight <= 0.01f || pickupCoordinator != null && pickupCoordinator.IsItemPickupActive)
        {
            return;
        }

        foreach (var(/* 中文含义：骨骼。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bone, /* 中文含义：目标旋转。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ targetRot)in fingerBones)
        {
            if (bone == null)
            {
                continue;
            }

            // 目标旋转。
Quaternion targetQ = Quaternion.Euler(targetRot);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, targetQ, weight);
        }
    }
}
