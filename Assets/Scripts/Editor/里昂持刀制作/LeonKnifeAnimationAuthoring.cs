using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>仅制作隔离候选资源，不修改场景、正式控制器或伤害时序。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀动画制作器")]
public static class LeonKnifeAnimationAuthoring
{
    /// <summary>输出目录。编译期常量，保持既有值与消费者映射。</summary>
    public const string outputDirectory = "Assets/Animations/里昂持刀候选";
    /// <summary>模型路径。编译期常量，保持既有值与消费者映射。</summary>
    const string modelPath = "Assets/Models/角色/里昂/LeonR4R.fbx";
    /// <summary>待机来源。编译期常量，保持既有值与消费者映射。</summary>
    const string idleSource = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/Knife_St_IdleSet/Knife_St_Idle_00.fbx";
    /// <summary>动画片段名称。只读字段，初始化后不由普通写入口替换。</summary>
    public static readonly string[] clipName =
    {
        "低位待机",
        "高位防御",
        "拔刀",
        "收刀",
        "右向左主挥砍",
        "左向右回切",
        "主挥砍回收",
        "回切回收",
        "抬刀",
        "放下",
        "普通格挡反应",
        "弹反成功",
        "移动主挥砍",
        "移动回切"
    };
    /// <summary>动画片段持续时间。只读字段，初始化后不由普通写入口替换。</summary>
    public static readonly float[] clipDuration =
    {
        3f,
        2f,
        .5f,
        .5f,
        .6f,
        .6f,
        .2f,
        .2f,
        .2f,
        .25f,
        .35f,
        .45f,
        .6f,
        .6f
    };
    /// <summary>仅对调用方提供的临时模型做离线静态标定，不生成或覆盖候选动画。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="id">编号。</param>
    public static string CalibrationNaturalAction(Animator animator, int id) => CalibrationHorizontalGrip(animator, id);
    /// <summary>CalibrationHorizontalGrip 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAllAssetsAtPath(string)、float.ToString(string)、HumanPose.HumanPose()、HumanPoseHandler.GetHumanPose(ref HumanPose)、HumanPoseHandler.HumanPoseHandler(Avatar, Transform) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.CalibrationNaturalAction(Animator, int)；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAllAssetsAtPath(string)、float.ToString(string)、HumanPose.HumanPose()、HumanPoseHandler.GetHumanPose(ref HumanPose)、HumanPoseHandler.HumanPoseHandler(Avatar, Transform) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="id">编号。</param>
    public static string CalibrationHorizontalGrip(Animator animator, int id)
    {
        if (!animator || !animator.avatar || !animator.avatar.isHuman)
        {
            throw new InvalidOperationException("需要具有有效人形映射的临时里昂模型。");
        }

        // 来源动画片段。
var source = AssetDatabase.LoadAllAssetsAtPath(idleSource).OfType<AnimationClip>().First( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__"));
        source.SampleAnimation(animator.gameObject, Mathf.Min(.2f, source.length * .25f));
        // footRig（脚部绑定）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var footRig = new AuthoringSkeleton(animator))
        // handler（处理器）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
        {
            // 姿态。
var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            // 髋部骨骼。
var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            // 原始髋部。
Vector3 originalHips = hipsBone.position;
            // 胸部骨骼。
var chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            // 左侧肩部。
var leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            // 右侧肩部。
var rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            // 原始肩部偏移量。
Vector3 originalShoulderOffset = (leftShoulder.position + rightShoulder.position) * .5f - originalHips;
            // 原始胸部偏移量。
Vector3 originalChestOffset = chestBone.position - originalHips;
            // 计算前缓存的左侧原始髋部。
global::UnityEngine.Vector3 leftOperand3 = originalHips;
            // 计算前缓存的左侧动画控制器的变换的向上。
global::UnityEngine.Vector3 leftOperand2 = animator.transform.up;
            // 分支选出的浮点计算。
float selectedValue1;
            if (id == 8)
            {
                selectedValue1 = -.008f;
            }
            else
            {
                selectedValue1 = -.035f;
            }

            // 髋部。
Vector3 hips = leftOperand3 + leftOperand2 * (selectedValue1);
            // 分支选出的2.5f。
float selectedValue4;
            if (id == 8)
            {
                selectedValue4 = 2.5f;
            }
            else
            {
                selectedValue4 = 8f;
            }

            pose.bodyRotation = Quaternion.AngleAxis(selectedValue4, Vector3.right) * pose.bodyRotation;
            Muscle(ref pose, "Spine Front-Back", .12f);
            Muscle(ref pose, "Chest Front-Back", .045f);
            Muscle(ref pose, "Spine Twist Left-Right", 0f);
            Muscle(ref pose, "Chest Twist Left-Right", 0f);
            LeftHandNaturalFingerShape(ref pose);
            RightHandGripFineTune(ref pose);
            // 左侧报告。
string leftReport = HorizontalGripFit(animator, handler, ref pose, hips, false, id);
            // 右侧报告。
string rightReport = HorizontalGripFit(animator, handler, ref pose, hips, true, id);
            footRig.FootLock(ref pose, hips);
            // 肩部偏移量。
Vector3 shoulderOffset = (leftShoulder.position + rightShoulder.position) * .5f - hipsBone.position;
            // 胸部偏移量。
Vector3 chestOffset = chestBone.position - hipsBone.position;
            // 肩部前方移动。
float shoulderFrontMove = Vector3.Dot(shoulderOffset - originalShoulderOffset, animator.transform.forward);
            // 胸部前方移动。
float chestFrontMove = Vector3.Dot(chestOffset - originalChestOffset, animator.transform.forward);
            return new[]
            {
                "准备",
                "高架",
                "蓄势",
                "主挥末",
                "弹反展开",
                "鞘位",
                "回切末",
                "格挡接触",
                "自然待机",
                "左臂蓄力",
                "左臂主挥外摆",
                "左臂回切借力",
                "左臂弹反外摆"
            }[id] + "；骨盆降低=" + Vector3.Dot(originalHips - hipsBone.position, animator.transform.up).ToString("F4") + "米；双肩相对髋前移=" + shoulderFrontMove.ToString("F4") + "米；胸骨相对髋前移=" + chestFrontMove.ToString("F4") + "米；锁脚误差=" + footRig.footError.ToString("F4") + "；" + leftReport + "；" + rightReport;
        }
    }

    /// <summary>HorizontalGripFit 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Apply(ref HumanPose)、Array.IndexOf&lt;string&gt;(string[], string)、Cost(bool)、float.ToString(string)、IEnumerable&lt;int&gt;.Select&lt;int, string&gt;(Func&lt;int, string&gt;)、IEnumerable&lt;int&gt;.ToArray&lt;int&gt;() 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Apply(ref HumanPose)、Array.IndexOf&lt;string&gt;(string[], string)、Cost(bool)、float.ToString(string)、IEnumerable&lt;int&gt;.Select&lt;int, string&gt;(Func&lt;int, string&gt;)、IEnumerable&lt;int&gt;.ToArray&lt;int&gt;() 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="handler">处理器。</param>
    /// <param name="output">输出。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="hipsTarget">髋部目标。</param>
    /// <param name="right">是否右侧。</param>
    /// <param name="id">编号。</param>
    static string HorizontalGripFit(Animator animator, HumanPoseHandler handler, ref HumanPose output, Vector3 hipsTarget, bool right, int id)
    {
        // 侧边。
string side;
        if (right)
        {
            side = "Right ";
        }
        else
        {
            side = "Left ";
        }

        // 名称。
string[] name =
        {
            "Shoulder Down-Up",
            "Shoulder Front-Back",
            "Arm Down-Up",
            "Arm Front-Back",
            "Arm Twist In-Out",
            "Forearm Stretch",
            "Forearm Twist In-Out",
            "Hand Down-Up",
            "Hand In-Out"
        };
        // 当前元素索引。
int[] index = name.Select( /* 中文含义：物品。 */item => Array.IndexOf(HumanTrait.MuscleName, side + item)).ToArray();
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver7 = animator;
        // 分支选出的人形身体骨骼集合的右侧上部手臂。
global::UnityEngine.HumanBodyBones selectedValue6;
        if (right)
        {
            selectedValue6 = HumanBodyBones.RightUpperArm;
        }
        else
        {
            selectedValue6 = HumanBodyBones.LeftUpperArm;
        }

        // 上部手臂。
var upperArm = callReceiver7.GetBoneTransform(selectedValue6);
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver9 = animator;
        // 分支选出的人形身体骨骼集合的右侧下部手臂。
global::UnityEngine.HumanBodyBones selectedValue8;
        if (right)
        {
            selectedValue8 = HumanBodyBones.RightLowerArm;
        }
        else
        {
            selectedValue8 = HumanBodyBones.LeftLowerArm;
        }

        // 肘部骨骼。
var elbowBone = callReceiver9.GetBoneTransform(selectedValue8);
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver11 = animator;
        // 分支选出的人形身体骨骼集合的右手。
global::UnityEngine.HumanBodyBones selectedValue10;
        if (right)
        {
            selectedValue10 = HumanBodyBones.RightHand;
        }
        else
        {
            selectedValue10 = HumanBodyBones.LeftHand;
        }

        // 腕部骨骼。
var wristBone = callReceiver11.GetBoneTransform(selectedValue10);
        // 髋部骨骼。
var hipsBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver13 = animator;
        // 分支选出的人形骨骼编号。
global::UnityEngine.HumanBodyBones selectedValue12;
        if (right)
        {
            selectedValue12 = HumanBodyBones.RightIndexProximal;
        }
        else
        {
            selectedValue12 = HumanBodyBones.LeftIndexProximal;
        }

        // 索引手指根节点。
var indexFingerRoot = callReceiver13.GetBoneTransform(selectedValue12);
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver15 = animator;
        // 分支选出的人形骨骼编号。
global::UnityEngine.HumanBodyBones selectedValue14;
        if (right)
        {
            selectedValue14 = HumanBodyBones.RightMiddleProximal;
        }
        else
        {
            selectedValue14 = HumanBodyBones.LeftMiddleProximal;
        }

        // 中间手指根节点。
var middleFingerRoot = callReceiver15.GetBoneTransform(selectedValue14);
        // 调用前缓存的动画控制器。
global::UnityEngine.Animator callReceiver17 = animator;
        // 分支选出的人形骨骼编号。
global::UnityEngine.HumanBodyBones selectedValue16;
        if (right)
        {
            selectedValue16 = HumanBodyBones.RightLittleProximal;
        }
        else
        {
            selectedValue16 = HumanBodyBones.LeftLittleProximal;
        }

        // 较小手指根节点。
var smallFingerRoot = callReceiver17.GetBoneTransform(selectedValue16);
        /// <summary>PalmSurface 的业务处理入口；本方法直接依赖 Transform.InverseTransformDirection(Vector3)、Vector3.Cross(Vector3, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 Cost(bool)、LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)；下游（含内部回调体） Transform.InverseTransformDirection(Vector3)、Vector3.Cross(Vector3, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        Vector3 PalmSurface()
        {
            return animator.transform.InverseTransformDirection(Vector3.Cross(indexFingerRoot.position - smallFingerRoot.position, middleFingerRoot.position - wristBone.position).normalized);
        }

        // 肘部目标分组。
Vector3[] elbowTargetGroup =
        {
            new Vector3(.28f, 1.15f, .15f),
            new Vector3(.32f, 1.23f, .18f),
            new Vector3(.39f, 1.19f, .10f),
            new Vector3(.17f, 1.18f, .23f),
            new Vector3(.42f, 1.36f, .12f),
            new Vector3(.26f, 1.07f, .03f),
            new Vector3(.36f, 1.16f, .14f),
            new Vector3(.34f, 1.25f, .15f)
        };
        // 腕部目标分组。
Vector3[] wristTargetGroup =
        {
            new Vector3(.17f, 1.23f, .375f),
            new Vector3(.19f, 1.32f, .39f),
            new Vector3(.47f, 1.37f, .27f),
            new Vector3(-.06f, 1.22f, .35f),
            new Vector3(.62f, 1.45f, .26f),
            new Vector3(.30f, .87f, .19f),
            new Vector3(.54f, 1.20f, .32f),
            new Vector3(.19f, 1.39f, .31f)
        };
        // 小刀目标分组。
Vector3[] knifeTargetGroup =
        {
            new Vector3(-.90f, .32f, .20f),
            new Vector3(-.90f, .42f, .20f),
            new Vector3(-.80f, .35f, .35f),
            new Vector3(-.80f, -.10f, .55f),
            new Vector3(-.65f, .60f, .45f),
            new Vector3(-.75f, .10f, .65f),
            new Vector3(-.75f, .05f, .65f),
            new Vector3(-.85f, .50f, .20f)
        };
        // 右侧姿态编号。
int rightPoseId;
        if (id == 9)
        {
            rightPoseId = 2;
        }
        else
        {
            if (id == 10)
            {
                rightPoseId = 3;
            }
            else
            {
                if (id == 11)
                {
                    rightPoseId = 6;
                }
                else
                {
                    if (id == 12)
                    {
                        rightPoseId = 4;
                    }
                    else
                    {
                        rightPoseId = Mathf.Min(id, 7);
                    }
                }
            }
        }

        // 肘部目标。
Vector3 elbowTarget;
        if (right)
        {
            elbowTarget = elbowTargetGroup[rightPoseId];
        }
        else
        {
            elbowTarget = new Vector3(-.23f, 1.05f, .13f);
        }

        // 腕部目标。
Vector3 wristTarget;
        if (right)
        {
            wristTarget = wristTargetGroup[rightPoseId];
        }
        else
        {
            wristTarget = new Vector3(-.03f, 1.06f, .30f);
        }

        if (id == 8)
        {
            // 按原顺序缓存的实参浮点计算。
float argumentValue26;
            if (right)
            {
                argumentValue26 = .23f;
            }
            else
            {
                argumentValue26 = -.22f;
            }

            // 按原顺序缓存的实参1.10f。
float argumentValue27 = 1.10f;
            // 分支选出的浮点计算。
float selectedValue25;
            if (right)
            {
                selectedValue25 = .02f;
            }
            else
            {
                selectedValue25 = .06f;
            }

            elbowTarget = new Vector3(argumentValue26, argumentValue27, selectedValue25);
            // 按原顺序缓存的实参浮点计算。
float argumentValue30;
            if (right)
            {
                argumentValue30 = .25f;
            }
            else
            {
                argumentValue30 = -.24f;
            }

            // 按原顺序缓存的实参浮点计算。
float argumentValue31 = .85f;
            // 分支选出的浮点计算。
float selectedValue29;
            if (right)
            {
                selectedValue29 = .09f;
            }
            else
            {
                selectedValue29 = .12f;
            }

            wristTarget = new Vector3(argumentValue30, argumentValue31, selectedValue29);
        }

        if (!right && id >= 9)
        {
            // 左侧肘部。
Vector3[] leftElbow =
            {
                new Vector3(-.28f, 1.12f, .08f),
                new Vector3(-.36f, 1.16f, .04f),
                new Vector3(-.30f, 1.10f, .15f),
                new Vector3(-.38f, 1.16f, .07f)
            };
            // 左侧腕部。
Vector3[] leftWrist =
            {
                new Vector3(-.10f, 1.13f, .28f),
                new Vector3(-.48f, 1.12f, .27f),
                new Vector3(-.08f, 1.11f, .29f),
                new Vector3(-.53f, 1.12f, .28f)
            };
            elbowTarget = leftElbow[id - 9];
            wristTarget = leftWrist[id - 9];
        }

        // 小刀局部。
Vector3 knifeLocal = new Vector3(-.864971f, .364787f, -.344610f).normalized;
        // 小刀目标。
Vector3 knifeTarget;
        if (id == 8)
        {
            knifeTarget = new Vector3(0f, .05f, 1f).normalized;
        }
        else
        {
            knifeTarget = knifeTargetGroup[rightPoseId].normalized;
        }

        // 下部已绑定。
float[] lowerBound =
        {
            -.12f,
            -.18f,
            -.90f,
            -.90f,
            -.85f,
            -.65f,
            -.90f,
            -.55f,
            -.45f
        };
        // 上部已绑定。
float[] upperBound =
        {
            .18f,
            .28f,
            .55f,
            .55f,
            .85f,
            .60f,
            .90f,
            .55f,
            .45f
        };
        if (id == 8)
        {
            upperBound[5] = .95f;
        }

        if (right && id != 8 && (rightPoseId == 4 || rightPoseId == 6))
        {
            upperBound[6] = .05f;
        }

        // 基础值。
var baseValue = Copy(output);
        // 最小。
float minimum = float.PositiveInfinity;
        // 最佳。
HumanPose best = baseValue;
        /// <summary>Apply 的业务处理入口；本方法直接依赖 HumanPoseHandler.SetHumanPose(ref HumanPose)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)；下游（含内部回调体） HumanPoseHandler.SetHumanPose(ref HumanPose)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
        void Apply(ref HumanPose pose)
        {
            handler.SetHumanPose(ref pose);
            pose.bodyPosition += (hipsTarget - hipsBone.position) / animator.humanScale;
            handler.SetHumanPose(ref pose);
        }

        /// <summary>Cost 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.Pow(float, float)、Mathf.Sign(float)、PalmSurface()、Transform.InverseTransformDirection(Vector3)、Transform.InverseTransformPoint(Vector3)、Transform.TransformDirection(Vector3) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Pow(float, float)、Mathf.Sign(float)、PalmSurface()、Transform.InverseTransformDirection(Vector3)、Transform.InverseTransformPoint(Vector3)、Transform.TransformDirection(Vector3) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="knifeDirectionPhase">是否小刀方向阶段。</param>
        float Cost(bool knifeDirectionPhase)
        {

            // 中文含义：elbow 为“肘部”。；wrist 为“腕部”。
Vector3 elbow = animator.transform.InverseTransformPoint(elbowBone.position), wrist = animator.transform.InverseTransformPoint(wristBone.position);

            // 中文含义：elbowDifference 为“肘部差值”。；wristDifference 为“腕部差值”。
float elbowDifference = Vector3.Distance(elbow, elbowTarget), wristDifference = Vector3.Distance(wrist, wristTarget);
            // 肘部弯曲。
float elbowBend = Vector3.Angle(upperArm.position - elbowBone.position, wristBone.position - elbowBone.position);
            // 输出所属包围盒。
float outOfBounds = Mathf.Pow(Mathf.Max(0, elbowDifference - .025f), 2) + Mathf.Pow(Mathf.Max(0, wristDifference - .035f), 2);
            // 分支选出的最低角度。
int selectedValue33;
            if (id == 8)
            {
                selectedValue33 = 140;
            }
            else
            {
                selectedValue33 = 70;
            }

            // 最低角度。
float lowestAngle = selectedValue33;
            // 分支选出的最多高度角度。
int selectedValue34;
            if (id == 8)
            {
                selectedValue34 = 178;
            }
            else
            {
                selectedValue34 = 125;
            }

            // 最多高度角度。
float mostHeightAngle = selectedValue34;
            outOfBounds += Mathf.Pow(Mathf.Max(0, (lowestAngle - elbowBend) * .003f), 2) + Mathf.Pow(Mathf.Max(0, elbowBend - mostHeightAngle) * .003f, 2);
            // 计算前缓存的左侧浮点计算。
float leftOperand36 = (elbowDifference * elbowDifference * 2 + wristDifference * wristDifference);
            // 分支选出的100。
int selectedValue35;
            if (knifeDirectionPhase)
            {
                selectedValue35 = 100;
            }
            else
            {
                selectedValue35 = 1500;
            }

            // 位置值。
float positionValue = leftOperand36 * (selectedValue35);
            if (!knifeDirectionPhase)
            {
                return positionValue;
            }

            // 方向差值。
float directionDifference = 0;
            if (right)
            {
                // 小刀方向。
Vector3 knifeDirection = animator.transform.InverseTransformDirection(wristBone.TransformDirection(knifeLocal));
                directionDifference = (1 - Vector3.Dot(knifeDirection, knifeTarget)) * 5;
                // 复合赋值前保存的方向差值。
float previousValue37 = directionDifference;
                // 按原顺序缓存的实参0。
float argumentValue40 = 0;
                // 计算前缓存的左侧浮点计算。
float leftOperand39 = .65f;
                // 分支选出的小刀方向的深度。
float selectedValue38;
                if (id == 8)
                {
                    selectedValue38 = knifeDirection.z;
                }
                else
                {
                    selectedValue38 = Mathf.Sign(knifeTarget.x) * knifeDirection.x;
                }

                directionDifference = (float)(previousValue37 + Mathf.Pow(Mathf.Max(argumentValue40, leftOperand39 - (selectedValue38)), 2) * 20);
            }
            else
            {
                // 计算前缓存的左侧1。
float leftOperand44 = 1;
                // 按原顺序缓存的实参三维向量。
global::UnityEngine.Vector3 argumentValue43 = PalmSurface();
                // 分支选出的三维向量。
global::UnityEngine.Vector3 selectedValue42;
                if (id == 8)
                {
                    selectedValue42 = new Vector3(.85f, 0, -.25f);
                }
                else
                {
                    if (id == 12)
                    {
                        selectedValue42 = new Vector3(-.20f, -.50f, .70f);
                    }
                    else
                    {
                        selectedValue42 = new Vector3(0, -.85f, -.40f);
                    }
                }

                directionDifference = (leftOperand44 - Vector3.Dot(argumentValue43, (selectedValue42).normalized)) * 3;
            }

            return positionValue + outOfBounds * 200000 + directionDifference;
        }

        // upperRotate（上部旋转）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)。
foreach (float upperRotate in new[]
        {
            -.60f,
            -.15f,
            .30f,
            .65f
        }

        )
        {
            // 分支选出的浮点数值数组。
float[] selectedValue45;
            if (right)
            {
                selectedValue45 = new[]
                {
                    -.75f,
                    0f,
                    .75f
                };
            }
            else
            {
                selectedValue45 = new[]
                {
                    .10f
                };
            }

            // lowerRotate（下部旋转）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)。
foreach (float lowerRotate in (selectedValue45))
            {
                // 姿态。
var pose = Copy(baseValue);
                // 起始值。
float[] startValue;
                if (right)
                {
                    startValue = new[]
                    {
                        .02f,
                        .02f,
                        -.35f,
                        -.35f,
                        upperRotate,
                        -.10f,
                        lowerRotate,
                        0f,
                        0f
                    };
                }
                else
                {
                    startValue = new[]
                    {
                        0f,
                        .05f,
                        -.65f,
                        .08f,
                        upperRotate,
                        -.05f,
                        .10f,
                        0f,
                        0f
                    };
                }

                // 物品。
for (int item = 0; item < index.Length; item++)
                {
                    pose.muscles[index[item]] = Mathf.Clamp(startValue[item], lowerBound[item], upperBound[item]);
                }

                // 阶段。
for (int phase = 0; phase < 2; phase++)
                {
                    // 轮次。
for (int round = 0; round < 48; round++)
                    {
                        // 步进。
float step;
                        if (round < 15)
                        {
                            step = .12f;
                        }
                        else
                        {
                            if (round < 30)
                            {
                                step = .035f;
                            }
                            else
                            {
                                step = .008f;
                            }
                        }

                        // 物品。
for (int item = 0;; item++)
                        {
                            // 计算前缓存的左侧物品。
int leftOperand50 = item;
                            // 分支选出的6。
int selectedValue49;
                            if (phase == 0)
                            {
                                selectedValue49 = 6;
                            }
                            else
                            {
                                selectedValue49 = 9;
                            }

                            if (!(leftOperand50 < (selectedValue49)))
                            {
                                break;
                            }

                            {
                                Apply(ref pose);

                                // 中文含义：original 为“原始”。；selected 为“选定”。；bestValue 为“最佳值”。
float original = pose.muscles[index[item]], selected = original, bestValue = Cost(phase == 1);
                                // 符号。
for (int symbol = -1; symbol <= 1; symbol += 2)
                                {
                                    pose.muscles[index[item]] = Mathf.Clamp(original + symbol * step, lowerBound[item], upperBound[item]);
                                    Apply(ref pose);
                                    // 当前。
float current = Cost(phase == 1);
                                    if (current < bestValue)
                                    {
                                        bestValue = current;
                                        selected = pose.muscles[index[item]];
                                    }
                                }

                                pose.muscles[index[item]] = selected;
                            }
                        }
                    }
                }

                Apply(ref pose);
                // 上一次。
float last = Cost(true);
                if (last < minimum)
                {
                    minimum = last;
                    best = Copy(pose);
                }
            }
        }

        output = best;
        Apply(ref output);

        // 中文含义：elbowEnd 为“肘部结束”。；wristEnd 为“腕部结束”。
Vector3 elbowEnd = animator.transform.InverseTransformPoint(elbowBone.position), wristEnd = animator.transform.InverseTransformPoint(wristBone.position);
        // 角度。
float angle = Vector3.Angle(upperArm.position - elbowBone.position, wristBone.position - elbowBone.position);
        // 小刀结束。
Vector3 knifeEnd = animator.transform.InverseTransformDirection(wristBone.TransformDirection(knifeLocal));
        // 标签。
string label;
        if (right)
        {
            label = "右臂";
        }
        else
        {
            label = "左臂";
        }

        // 计算前缓存的左侧加入的返回结果。
string leftOperand53 = label + "九关节=" + string.Join(",", index.Select( /* 中文含义：物品。 */item => best.muscles[item].ToString("F4"))) + "，肘=" + elbowEnd.ToString("F4") + "，腕=" + wristEnd.ToString("F4") + "，肘误差=" + Vector3.Distance(elbowEnd, elbowTarget).ToString("F4") + "，腕误差=" + Vector3.Distance(wristEnd, wristTarget).ToString("F4") + "，内肘角=" + angle.ToString("F1");
        // 分支选出的转换后的文字。
string selectedValue52;
        if (right)
        {
            selectedValue52 = "，刀轴=" + knifeEnd.ToString("F4") + "，刀向误差=" + Vector3.Angle(knifeEnd, knifeTarget).ToString("F1");
        }
        else
        {
            selectedValue52 = "，左掌参考法向=" + PalmSurface().ToString("F4");
        }

        return leftOperand53 + (selectedValue52);
    }

    /// <summary>LeftArmBase 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.LeftArmAction(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static float[] LeftArmBase() => new[]
    {
        .2250f,
        -.0980f,
        -.6212f,
        .1301f,
        .0637f,
        -.3591f,
        -.4137f,
        -.4297f,
        .0267f
    };
    /// <summary>LeftArmAction 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.LeftArmBase()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.ReturnEnd()、LeonKnifeAnimationAuthoring.SheathPosition()、LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） LeonKnifeAnimationAuthoring.LeftArmBase()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    static float[] LeftArmAction(int id)
    {
        switch (id)
        {
            case 8:
                return new[]
                {
                    0.1720f,
                    -0.1800f,
                    -0.8410f,
                    0.2850f,
                    0.2480f,
                    0.6440f,
                    0.3050f,
                    -0.2400f,
                    -0.0350f
                };
            case 9:
                return new[]
                {
                    .16f,
                    .18f,
                    -.33f,
                    .10f,
                    -.26f,
                    -.57f,
                    -.30f,
                    -.12f,
                    -.06f
                };
            case 10:
                return new[]
                {
                    -.06f,
                    -.12f,
                    -.08f,
                    .56f,
                    .64f,
                    .28f,
                    -.10f,
                    -.10f,
                    .12f
                };
            case 11:
                return new[]
                {
                    .13f,
                    .24f,
                    -.51f,
                    .46f,
                    -.12f,
                    -.52f,
                    -.39f,
                    -.20f,
                    .03f
                };
            case 12:
                return new[]
                {
                    -.10f,
                    .12f,
                    .08f,
                    .39f,
                    .47f,
                    .25f,
                    .17f,
                    .21f,
                    -.19f
                };
            default:
                return LeftArmBase();
        }
    }

    /// <summary>RightArmPose 的业务处理入口；本方法直接依赖 Array.Clone()、Mathf.Clamp(int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)；下游（含内部回调体） Array.Clone()、Mathf.Clamp(int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    static float[] RightArmPose(int id)
    {
        // 当前浮点数值数组数组。
float[][] value =
        {
            new[]
            {
                0.1800f,
                0.2530f,
                -0.3900f,
                -0.3920f,
                0.0080f,
                -0.0390f,
                -0.4800f,
                0.3170f,
                0.4500f
            },
            new[]
            {
                .0640f,
                .4670f,
                -.0632f,
                -.4700f,
                -.0218f,
                .0271f,
                -.1727f,
                -.4138f,
                .6678f
            },
            new[]
            {
                -0.0430f,
                -0.1450f,
                0.0600f,
                -0.1880f,
                0.5470f,
                -0.0300f,
                -0.3900f,
                0.3870f,
                0.0160f
            },
            new[]
            {
                -0.1200f,
                0.1380f,
                -0.6250f,
                -0.6140f,
                -0.3390f,
                0.1250f,
                -0.9000f,
                0.1040f,
                0.4500f
            },
            new[]
            {
                0.1800f,
                0.0320f,
                0.2880f,
                -0.2220f,
                0.6600f,
                0.4050f,
                -0.2270f,
                0.5500f,
                0.0520f
            },
            new[]
            {
                -0.1200f,
                0.1110f,
                -0.4750f,
                -0.0060f,
                -0.1320f,
                0.3680f,
                -0.3500f,
                0.3060f,
                0.2640f
            },
            new[]
            {
                -0.1200f,
                -0.1800f,
                -0.0640f,
                -0.2410f,
                0.5970f,
                0.3200f,
                -0.7150f,
                -0.2330f,
                -0.2160f
            },
            new[]
            {
                0.1800f,
                0.2800f,
                0.0610f,
                -0.4110f,
                0.1860f,
                -0.2860f,
                -0.2190f,
                -.245f,
                .58f
            },
            new[]
            {
                -0.0770f,
                -0.1800f,
                -0.6070f,
                -0.0490f,
                -0.7100f,
                0.8940f,
                0.8220f,
                0.2800f,
                0.0720f
            }
        };
        return (float[])value[Mathf.Clamp(id, 0, value.Length - 1)].Clone();
    }

    /// <summary>ArmParameter 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="right">是否右侧。</param>
    /// <param name="value">当前浮点数值数组。</param>
    static void ArmParameter(ref HumanPose pose, bool right, float[] value)
    {
        // 侧边。
string side;
        if (right)
        {
            side = "Right ";
        }
        else
        {
            side = "Left ";
        }

        // 名称。
string[] name =
        {
            "Shoulder Down-Up",
            "Shoulder Front-Back",
            "Arm Down-Up",
            "Arm Front-Back",
            "Arm Twist In-Out",
            "Forearm Stretch",
            "Forearm Twist In-Out",
            "Hand Down-Up",
            "Hand In-Out"
        };
        // 物品。
for (int item = 0; item < name.Length; item++)
        {
            Muscle(ref pose, side + name[item], value[item]);
        }
    }

    /// <summary>LeftHandNaturalFingerShape 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)；下游（含内部回调体） LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
    static void LeftHandNaturalFingerShape(ref HumanPose pose)
    {
        // finger（手指）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float) 的参数。
foreach (string finger in new[]
        {
            "Index",
            "Middle",
            "Ring",
            "Little"
        }

        )
        {
            Muscle(ref pose, "Left " + finger + " 1 Stretched", .30f);
            Muscle(ref pose, "Left " + finger + " 2 Stretched", .45f);
            Muscle(ref pose, "Left " + finger + " 3 Stretched", .55f);
            Muscle(ref pose, "Left " + finger + " Spread", 0f);
        }

        Muscle(ref pose, "Left Thumb 1 Stretched", -.25f);
        Muscle(ref pose, "Left Thumb 2 Stretched", .35f);
        Muscle(ref pose, "Left Thumb 3 Stretched", .45f);
        Muscle(ref pose, "Left Thumb Spread", .10f);
    }

    /// <summary>AdjustAlertFingerShape 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="configuration">配置。</param>
    static void AdjustAlertFingerShape(ref HumanPose pose, ActionPose configuration)
    {
        // 警戒。
float alert = Mathf.InverseLerp(2.5f, 8f, configuration.bodyTiltAngle);
        // 散布手掌。
float spreadPalm = Mathf.InverseLerp(-.36f, .65f, configuration.leftArm[5]);
        // 手指名称。
string[] fingerName =
        {
            "Index",
            "Middle",
            "Ring",
            "Little"
        };
        // 手指。
for (int finger = 0; finger < fingerName.Length; finger++)
        {
            // 前缀。
string prefix = "Left " + fingerName[finger];
            // 内部扣除。
float innerDeduct = .095f * finger;
            Muscle(ref pose, prefix + " 1 Stretched", Mathf.Lerp(.30f, .19f - innerDeduct + .28f * spreadPalm, alert));
            Muscle(ref pose, prefix + " 2 Stretched", Mathf.Lerp(.45f, .06f - innerDeduct + .29f * spreadPalm, alert));
            Muscle(ref pose, prefix + " 3 Stretched", Mathf.Lerp(.55f, .25f - innerDeduct + .17f * spreadPalm, alert));
            Muscle(ref pose, prefix + " Spread", alert * (.05f + .12f * spreadPalm));
        }

        Muscle(ref pose, "Left Thumb Spread", .10f + alert * (.12f + .12f * spreadPalm));
    }

    /// <summary>RightHandGripFineTune 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)；下游（含内部回调体） LeonKnifeAnimationAuthoring.Muscle(ref HumanPose, string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
    static void RightHandGripFineTune(ref HumanPose pose)
    {
        // 保留已贴合柄身的掌指关节和各指横展，只收拢伸出的末节。
        Muscle(ref pose, "Right Index 3 Stretched", -.45f);
        Muscle(ref pose, "Right Middle 3 Stretched", -.50f);
        Muscle(ref pose, "Right Ring 3 Stretched", -.50f);
        Muscle(ref pose, "Right Little 3 Stretched", -.40f);
        Muscle(ref pose, "Right Thumb 1 Stretched", -.50f);
        Muscle(ref pose, "Right Thumb Spread", .35f);
        Muscle(ref pose, "Right Thumb 2 Stretched", -.50f);
        Muscle(ref pose, "Right Thumb 3 Stretched", -.50f);
    }

    /// <summary>编辑器菜单调用入口 MenuGenerate；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、LeonKnifeAnimationAuthoring.Generate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/生成候选动作")]
    public static void MenuGenerate()
    {
        Debug.Log(Generate());
    }

    /// <summary>Generate 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.SaveAssetIfDirty(Object)、EditorSceneManager.ClosePreviewScene(Scene)、EditorSceneManager.NewPreviewScene() 等共 22 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.MenuGenerate()；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.SaveAssetIfDirty(Object)、EditorSceneManager.ClosePreviewScene(Scene)、EditorSceneManager.NewPreviewScene() 等共 22 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Generate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请在编辑模式生成候选动作。");
        }

        CreateDirectory(outputDirectory);
        // 场景。
var scene = EditorSceneManager.NewPreviewScene();
        // 模型。
GameObject model = null;
        try
        {
            // 资源。
var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!asset)
            {
                throw new InvalidOperationException("找不到里昂人形模型。");
            }

            model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 动画控制器。
var animator = model.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException("里昂模型的人形映射无效。");
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // 来源动画片段。
var source = AssetDatabase.LoadAllAssetsAtPath(idleSource).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__"));
            if (!source || !source.humanMotion)
            {
                throw new InvalidOperationException("小刀待机素材缺失或未导入为人形动画。");
            }

            source.SampleAnimation(model, Mathf.Min(.2f, source.length * .25f));
            // skeleton（骨架）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var skeleton = new AuthoringSkeleton(animator))
            {
                // 编号。
for (int id = 0; id < clipName.Length; id++)
                {
                    EditorUtility.DisplayProgressBar("制作里昂持刀候选", clipName[id], id / (float)clipName.Length);
                    Bake(skeleton, id);
                }

                // nameValue（名称值）：每轮从动画片段名称取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的参数。
foreach (string nameValue in clipName)
                {
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<AnimationClip>(outputDirectory + "/" + nameValue + ".anim"));
                }

                return "已完成自然持刀与左臂返工十四段候选；每秒六十次采样；脚底最大求解偏差：" + skeleton.footError.ToString("F4") + "米。需逐段检查多视角刀路与肩肘腕链，数值检查不代替视觉验收。";
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (model)
            {
                Object.DestroyImmediate(model);
            }

            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>CreateDirectory 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、LeonKnifeAnimationAuthoring.CreateDirectory(string)、Path.GetDirectoryName(string)、Path.GetFileName(string)、string.Replace(char, char)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.CreateDirectory(string)、LeonKnifeAnimationAuthoring.Generate()；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、LeonKnifeAnimationAuthoring.CreateDirectory(string)、Path.GetDirectoryName(string)、Path.GetFileName(string)、string.Replace(char, char)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    static void CreateDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        // 父级。
string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        CreateDirectory(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    /// <summary>Copy 的业务处理入口；本方法直接依赖 Array.Clone()、HumanPose.HumanPose()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.Bake(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int)、LeonKnifeAnimationAuthoring.HorizontalGripFit(Animator, HumanPoseHandler, ref HumanPose, Vector3, bool, int)；下游（含内部回调体） Array.Clone()、HumanPose.HumanPose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。</param>
    static HumanPose Copy(HumanPose pose) => new HumanPose
    {
        bodyPosition = pose.bodyPosition,
        bodyRotation = pose.bodyRotation,
        muscles = (float[])pose.muscles.Clone()
    };
    /// <summary>Muscle 的业务处理入口；本方法直接依赖 Array.IndexOf&lt;string&gt;(string[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AdjustAlertFingerShape(ref HumanPose, LeonKnifeAnimationAuthoring.ActionPose)、LeonKnifeAnimationAuthoring.ArmParameter(ref HumanPose, bool, float[])、LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)、LeonKnifeAnimationAuthoring.LeftHandNaturalFingerShape(ref HumanPose) 等共 6 处；下游（含内部回调体） Array.IndexOf&lt;string&gt;(string[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
    /// <param name="name">名称。</param>
    /// <param name="value">值。</param>
    static void Muscle(ref HumanPose pose, string name, float value)
    {
        // 当前元素索引。
int index = Array.IndexOf(HumanTrait.MuscleName, name);
        if (index >= 0)
        {
            pose.muscles[index] = value;
        }
    }

    /// <summary>Ease 的业务处理入口；本方法直接依赖 Mathf.InverseLerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.LeftArmStaggeredTiming(LeonKnifeAnimationAuthoring.ActionPose, float, float[], float[], float[], float[], float, float)；下游（含内部回调体） Mathf.InverseLerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="start">起始。</param>
    /// <param name="end">结束。</param>
    /// <param name="time">时间。</param>
    static float Ease(float start, float end, float time)
    {
        // 值。
float value = Mathf.InverseLerp(start, end, time);
        return value * value * value * (value * (value * 6 - 15) + 10);
    }

    /// <summary>编辑器配置与校验模块 ActionPose；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀动画制作器+动作姿态")]
    // 肩肘腕直接以人形关节角创作，禁止以追逐手腕点的方式改写肩轴分支。
    sealed class ActionPose
    {
        /// <summary>右臂。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("右臂")]
        [UnityEngine.InspectorName("右臂")]
        public float[] rightArm;
        /// <summary>左臂。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("左臂")]
        [UnityEngine.InspectorName("左臂")]
        public float[] leftArm;
        /// <summary>胸扭。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("胸扭")]
        [UnityEngine.InspectorName("胸扭")]
        public float chestTwist;
        /// <summary>髋扭。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("髋扭")]
        [UnityEngine.InspectorName("髋扭")]
        public float hipsTwist;
        /// <summary>前倾。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("前倾")]
        [UnityEngine.InspectorName("前倾")]
        public float forwardLean;
        /// <summary>下沉。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("下沉")]
        [UnityEngine.InspectorName("下沉")]
        public float sink;
        /// <summary>身体倾角。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("身体倾角")]
        [UnityEngine.InspectorName("身体倾角")]
        public float bodyTiltAngle;
        /// <summary>构造并初始化 ActionPose 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ActionPose.Blend(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float)、LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.ContinuousPassThrough(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float, float)、LeonKnifeAnimationAuthoring.High()、LeonKnifeAnimationAuthoring.Low() 等共 9 处；下游（含内部回调体） LeonKnifeAnimationAuthoring.LeftArmAction(int)、LeonKnifeAnimationAuthoring.RightArmPose(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="rightId">右侧编号。</param>
        public ActionPose(int rightId)
        {
            rightArm = RightArmPose(rightId);
            // 分支选出的8。
int selectedValue55;
            if (rightId == 8)
            {
                selectedValue55 = 8;
            }
            else
            {
                selectedValue55 = 0;
            }

            leftArm = LeftArmAction(selectedValue55);
            forwardLean = .12f;
            if (rightId == 8)
            {
                sink = .008f;
            }
            else
            {
                sink = .035f;
            }

            if (rightId == 8)
            {
                bodyTiltAngle = 2.5f;
            }
            else
            {
                bodyTiltAngle = 8f;
            }
        }

        /// <summary>Blend 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="start">起始。</param>
        /// <param name="end">结束。</param>
        /// <param name="ratio">比例。</param>
        public static ActionPose Blend(ActionPose start, ActionPose end, float ratio)
        {
            // 当前动作姿态。
var result = new ActionPose(0);
            // 物品。
for (int item = 0; item < 9; item++)
            {
                result.rightArm[item] = Mathf.Lerp(start.rightArm[item], end.rightArm[item], ratio);
                result.leftArm[item] = Mathf.Lerp(start.leftArm[item], end.leftArm[item], ratio);
            }

            result.chestTwist = Mathf.Lerp(start.chestTwist, end.chestTwist, ratio);
            result.hipsTwist = Mathf.Lerp(start.hipsTwist, end.hipsTwist, ratio);
            result.forwardLean = Mathf.Lerp(start.forwardLean, end.forwardLean, ratio);
            result.sink = Mathf.Lerp(start.sink, end.sink, ratio);
            result.bodyTiltAngle = Mathf.Lerp(start.bodyTiltAngle, end.bodyTiltAngle, ratio);
            return result;
        }
    }

    /// <summary>Low 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static ActionPose Low()
    {
        // 姿态。
var pose = new ActionPose(8);
        pose.rightArm[0] -= .045f;
        pose.leftArm[0] -= .055f;
        pose.rightArm[5] -= .045f;
        pose.leftArm[5] -= .035f;
        return pose;
    }

    /// <summary>High 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static ActionPose High()
    {
        // 绑定。
var rig = new ActionPose(1)
        {
            chestTwist = .055f,
            hipsTwist = 2.5f,
            bodyTiltAngle = 8.5f
        };
        // 用肩肘把刀架在胸前，保留原握柄与刀刃截挡角。
        rig.rightArm[2] += .045f;
        rig.rightArm[5] -= .055f;
        return rig;
    }

    /// <summary>SheathPosition 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.LeftArmAction(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.LeftArmAction(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static ActionPose SheathPosition() => new ActionPose(5)
    {
        chestTwist = -.035f,
        leftArm = LeftArmAction(8),
        bodyTiltAngle = 2.5f,
        sink = .008f
    };
    /// <summary>MainEnd 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static ActionPose MainEnd()
    {
        // 当前动作姿态。
var result = new ActionPose(3)
        {
            leftArm = new[]
            {
                .10f,
                .22f,
                .14f,
                .41f,
                -.32f,
                -.55f,
                -.26f,
                -.12f,
                .05f
            },
            chestTwist = .34f,
            hipsTwist = 14f,
            forwardLean = .12f,
            sink = .035f
        };
        return result;
    }

    /// <summary>ReturnEnd 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.LeftArmAction(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.LeftArmAction(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static ActionPose ReturnEnd()
    {
        // 当前动作姿态。
var result = new ActionPose(6)
        {
            leftArm = LeftArmAction(11),
            chestTwist = -.29f,
            hipsTwist = -12f,
            forwardLean = .12f,
            sink = .035f
        };
        return result;
    }

    /// <summary>ContinuousPassThrough 的业务处理入口；本方法直接依赖 Evaluate(float, float, float)、LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Evaluate(float, float, float)、LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="start">起始。</param>
    /// <param name="middle">中间。</param>
    /// <param name="end">结束。</param>
    /// <param name="time">时间。</param>
    /// <param name="middleRatio">中间比例。</param>
    static ActionPose ContinuousPassThrough(ActionPose start, ActionPose middle, ActionPose end, float time, float middleRatio)
    {
        // 是否前方线段。
bool frontSegment = time < middleRatio;
        // 宽度。
float width;
        if (frontSegment)
        {
            width = middleRatio;
        }
        else
        {
            width = 1 - middleRatio;
        }

        // 分支选出的浮点计算。
float selectedValue59;
        if (frontSegment)
        {
            selectedValue59 = time / width;
        }
        else
        {
            selectedValue59 = (time - middleRatio) / width;
        }

        // 比例。
float ratio = Mathf.Clamp01(selectedValue59);

        // 中文含义：twoIteration 为“第二迭代”。；threeIteration 为“第三迭代”。
float twoIteration = ratio * ratio, threeIteration = twoIteration * ratio;
        /// <summary>Evaluate 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ContinuousPassThrough(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float, float)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="first">本段曲线的起点值。</param>
        /// <param name="between">两者之间。</param>
        /// <param name="tail">尾部。</param>
        float Evaluate(float first, float between, float tail)
        {
            // 起始值。
float startValue;
            if (frontSegment)
            {
                startValue = first;
            }
            else
            {
                startValue = between;
            }

            // 结束值。
float endValue;
            if (frontSegment)
            {
                endValue = between;
            }
            else
            {
                endValue = tail;
            }

            // 初始速度。
float initialSpeed;
            if (frontSegment)
            {
                initialSpeed = 0;
            }
            else
            {
                initialSpeed = tail - first;
            }

            // 最终速度。
float finalSpeed;
            if (frontSegment)
            {
                finalSpeed = tail - first;
            }
            else
            {
                finalSpeed = 0;
            }

            // 值。
float value = (2 * threeIteration - 3 * twoIteration + 1) * startValue + (threeIteration - 2 * twoIteration + ratio) * initialSpeed * width + (-2 * threeIteration + 3 * twoIteration) * endValue + (threeIteration - twoIteration) * finalSpeed * width;
            return Mathf.Clamp(value, Mathf.Min(first, Mathf.Min(between, tail)), Mathf.Max(first, Mathf.Max(between, tail)));
        }

        // 当前动作姿态。
var result = new ActionPose(0);
        // 物品。
for (int item = 0; item < 9; item++)
        {
            result.rightArm[item] = Evaluate(start.rightArm[item], middle.rightArm[item], end.rightArm[item]);
            result.leftArm[item] = Evaluate(start.leftArm[item], middle.leftArm[item], end.leftArm[item]);
        }

        result.chestTwist = Evaluate(start.chestTwist, middle.chestTwist, end.chestTwist);
        result.hipsTwist = Evaluate(start.hipsTwist, middle.hipsTwist, end.hipsTwist);
        result.forwardLean = Evaluate(start.forwardLean, middle.forwardLean, end.forwardLean);
        result.sink = Evaluate(start.sink, middle.sink, end.sink);
        result.bodyTiltAngle = Evaluate(start.bodyTiltAngle, middle.bodyTiltAngle, end.bodyTiltAngle);
        return result;
    }

    // 两刀共用一条时间轴。一处只在方向反转时减速，普通中间点沿割线继续运动。
    /// <summary>BladePathInterpolation 的业务处理入口；本方法直接依赖 Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;.Invoke(LeonKnifeAnimationAuthoring.ActionPose)、Mathf.LerpUnclamped(float, float, float)、Tangent(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)；下游（含内部回调体） Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;.Invoke(LeonKnifeAnimationAuthoring.ActionPose)、Mathf.LerpUnclamped(float, float, float)、Tangent(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="timestamp">时间戳。</param>
    /// <param name="pose">姿态。</param>
    /// <param name="time">时间。</param>
    /// <param name="get">取得。</param>
    static float BladePathInterpolation(float[] timestamp, ActionPose[] pose, float time, Func<ActionPose, float> get)
    {
        // 尾部。
int tail = timestamp.Length - 1;
        if (time <= timestamp[0])
        {
            return get(pose[0]);
        }

        if (time >= timestamp[tail])
        {
            return get(pose[tail]);
        }

        // 段落。
int section = 0;
        while (time > timestamp[section + 1])
        {
            section++;
        }

        /// <summary>Slope 的业务处理入口；本方法直接依赖 Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;.Invoke(LeonKnifeAnimationAuthoring.ActionPose)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 Tangent(int)；下游（含内部回调体） Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;.Invoke(LeonKnifeAnimationAuthoring.ActionPose)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="item">物品。</param>
        float Slope(int item)
        {
            return (get(pose[item + 1]) - get(pose[item])) / (timestamp[item + 1] - timestamp[item]);
        }

        /// <summary>Tangent 的业务处理入口；本方法直接依赖 Slope(int)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.BladePathInterpolation(float[], LeonKnifeAnimationAuthoring.ActionPose[], float, Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;)；下游（含内部回调体） Slope(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="item">物品。</param>
        float Tangent(int item)
        {
            if (item == 0 || item == tail)
            {
                return 0;
            }


            // 中文含义：front 为“前方”。；back 为“后方”。
float front = Slope(item - 1), back = Slope(item);
            if (front * back <= 0)
            {
                return 0;
            }
            else
            {
                return 2 * front * back / (front + back);
            }
        }

        // 宽度。
float width = timestamp[section + 1] - timestamp[section];

        // 中文含义：ratio 为“比例”。；direction 为“方向”。
float ratio = (time - timestamp[section]) / width, direction = ratio * ratio;
        return Mathf.LerpUnclamped(get(pose[section]), get(pose[section + 1]), direction * (3 - 2 * ratio)) + width * (ratio * (1 - ratio) * (1 - ratio) * Tangent(section) - direction * (1 - ratio) * Tangent(section + 1));
    }

    /// <summary>TwoAttacksMomentumTransfer 的业务处理入口；本方法直接依赖 Array.Clone()、LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.ActionPose.Blend(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float)、LeonKnifeAnimationAuthoring.BladePathInterpolation(float[], LeonKnifeAnimationAuthoring.ActionPose[], float, Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;)、LeonKnifeAnimationAuthoring.LeftArmAction(int)、LeonKnifeAnimationAuthoring.Low()、LeonKnifeAnimationAuthoring.MainEnd() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)；下游（含内部回调体） Array.Clone()、LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.ActionPose.Blend(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float)、LeonKnifeAnimationAuthoring.BladePathInterpolation(float[], LeonKnifeAnimationAuthoring.ActionPose[], float, Func&lt;LeonKnifeAnimationAuthoring.ActionPose, float&gt;)、LeonKnifeAnimationAuthoring.LeftArmAction(int)、LeonKnifeAnimationAuthoring.Low()、LeonKnifeAnimationAuthoring.MainEnd() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="time">时间。</param>
    /// <param name="backslash">是否反斜杠。</param>
    static ActionPose TwoAttacksMomentumTransfer(float time, bool backslash)
    {
        // 分支选出的1。
int selectedValue65;
        if (backslash)
        {
            selectedValue65 = 1;
        }
        else
        {
            selectedValue65 = 0;
        }

        // 时间值。
float timeValue = (selectedValue65) + time;
        // 起始。
var start = Low();
        // 蓄力向上。
var chargeUp = new ActionPose(2)
        {
            chestTwist = -.24f,
            hipsTwist = -11,
            leftArm = LeftArmAction(9),
            sink = .043f
        };
        // 由肘向右外拉开刀手，蓄势压在肩下，前臂沿用同一握刀分支。
        chargeUp.rightArm[2] -= .14f;
        chargeUp.rightArm[3] += .20f;
        chargeUp.rightArm[5] += .08f;
        // 小刀提前量。
var knifeLead = ActionPose.Blend(start, chargeUp, .35f);
        // 片段进入。
var sliceInto = ActionPose.Blend(chargeUp, MainEnd(), .29f);
        // 主要穿透。
var mainPenetration = ActionPose.Blend(chargeUp, MainEnd(), .72f);
        mainPenetration.rightArm[5] += .19f;
        mainPenetration.sink = .05f;
        // 左侧边缘。
var leftEdge = MainEnd();
        leftEdge.rightArm[3] -= .09f;
        leftEdge.rightArm[5] += .11f;
        leftEdge.chestTwist += .08f;
        leftEdge.hipsTwist += 3;
        leftEdge.leftArm[2] += .11f;
        leftEdge.leftArm[5] = -.48f;
        leftEdge.leftArm[4] = -.28f;
        // 连接。
var connect = MainEnd();
        // 返回穿透。
var returnPenetration = ActionPose.Blend(connect, ReturnEnd(), .58f);
        returnPenetration.rightArm[2] += .14f;
        returnPenetration.rightArm[5] += .18f;
        returnPenetration.sink = .047f;
        // 右侧边缘。
var rightEdge = ReturnEnd();
        rightEdge.rightArm[3] += .10f;
        rightEdge.rightArm[2] += .14f;
        rightEdge.chestTwist -= .08f;
        rightEdge.hipsTwist -= 3;
        // 结束。
var end = ReturnEnd();
        // 返回值。
var returnValue = Low();
        // 时间戳。
float[] timestamp =
        {
            0,
            .24f,
            .46f,
            .555f,
            .73f,
            1,
            1.39f,
            1.76f,
            2,
            2.3333333f
        };
        // 姿态。
ActionPose[] pose =
        {
            start,
            knifeLead,
            sliceInto,
            mainPenetration,
            leftEdge,
            connect,
            returnPenetration,
            rightEdge,
            end,
            returnValue
        };
        // 当前动作姿态。
var result = new ActionPose(0);
        // 每个关节拥有独立的中间关键时刻，共享两刀边界及回收终点。
        // 左侧时间。
float[] leftTime = (float[])timestamp.Clone();
        // 物品。
for (int item = 0; item < 9; item++)
        {
            // 差值。
float difference;
            if (item < 2)
            {
                difference = -.035f;
            }
            else
            {
                if (item < 5)
                {
                    difference = 0;
                }
                else
                {
                    if (item < 7)
                    {
                        difference = .042f;
                    }
                    else
                    {
                        difference = .085f;
                    }
                }
            }

            // 键。
for (int key = 1; key < timestamp.Length - 1; key++)
            {
                // 赋值前保存的目标左侧时间。
float[] assignmentReceiver69 = leftTime;
                // 赋值前保存的索引键。
int assignmentIndex70 = key;
                // 计算前缓存的左侧时间戳。
float leftOperand72 = timestamp[key];
                // 分支选出的0。
float selectedValue71;
                if (key == 5 || key == 8)
                {
                    selectedValue71 = 0;
                }
                else
                {
                    selectedValue71 = difference;
                }

                assignmentReceiver69[assignmentIndex70] = leftOperand72 + (selectedValue71);
            }

            result.leftArm[item] = BladePathInterpolation(leftTime, pose, timeValue, /* 中文含义：段落。 */ section => section.leftArm[item]);
            result.rightArm[item] = BladePathInterpolation(timestamp, pose, timeValue, /* 中文含义：段落。 */ section => section.rightArm[item]);
        }

        result.chestTwist = -BladePathInterpolation(timestamp, pose, timeValue, /* 中文含义：段落。 */ section => section.chestTwist);
        // 髋部时间。
float[] hipsTime = (float[])timestamp.Clone();
        // key（键）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationAuthoring.TwoAttacksMomentumTransfer(float, bool)。
foreach (int key in new[]
        {
            1,
            2,
            3,
            4,
            6,
            7
        }

        )
        {
            hipsTime[key] -= .055f;
        }

        result.hipsTwist = -BladePathInterpolation(hipsTime, pose, timeValue, /* 中文含义：段落。 */ section => section.hipsTwist);
        result.bodyTiltAngle = BladePathInterpolation(timestamp, pose, timeValue, /* 中文含义：段落。 */ section => section.bodyTiltAngle);
        result.forwardLean = BladePathInterpolation(timestamp, pose, timeValue, /* 中文含义：段落。 */ section => section.forwardLean);
        result.sink = BladePathInterpolation(timestamp, pose, timeValue, /* 中文含义：段落。 */ section => section.sink);
        return result;
    }

    // 锁骨先起，上臂再带肘，前臂最后带掌。到达与折返分别错峰，端点仍取共享姿态。
    /// <summary>LeftArmStaggeredTiming 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.Ease(float, float, float)、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.ComputePose(int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.Ease(float, float, float)、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pose">姿态。</param>
    /// <param name="time">时间。</param>
    /// <param name="start">起始。</param>
    /// <param name="chargeUp">蓄力向上。</param>
    /// <param name="spread">散布。</param>
    /// <param name="end">结束。</param>
    /// <param name="chargeUpTime">蓄力向上时间。</param>
    /// <param name="spreadTime">散布时间。</param>
    static void LeftArmStaggeredTiming(ActionPose pose, float time, float[] start, float[] chargeUp, float[] spread, float[] end, float chargeUpTime, float spreadTime)
    {
        // 物品。
for (int item = 0; item < 9; item++)
        {
            // 延后。
float late;
            if (item < 2)
            {
                late = 0f;
            }
            else
            {
                if (item < 5)
                {
                    late = .035f;
                }
                else
                {
                    if (item < 7)
                    {
                        late = .072f;
                    }
                    else
                    {
                        late = .108f;
                    }
                }
            }

            // 恢复结束。
float recoverEnd = chargeUpTime + late * .65f;
            // 散布结束。
float spreadEnd = spreadTime + late;
            if (time < recoverEnd)
            {
                pose.leftArm[item] = Mathf.Lerp(start[item], chargeUp[item], Ease(0, recoverEnd, time));
            }
            else if (time < spreadEnd)
            {
                pose.leftArm[item] = Mathf.Lerp(chargeUp[item], spread[item], Ease(recoverEnd, spreadEnd, time));
            }
            else
            {
                pose.leftArm[item] = Mathf.Lerp(spread[item], end[item], Ease(spreadEnd, 1, time));
            }
        }
    }

    /// <summary>ComputePose 的业务处理入口；本方法直接依赖 LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.ActionPose.Blend(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float)、LeonKnifeAnimationAuthoring.Ease(float, float, float)、LeonKnifeAnimationAuthoring.High()、LeonKnifeAnimationAuthoring.LeftArmAction(int)、LeonKnifeAnimationAuthoring.LeftArmStaggeredTiming(LeonKnifeAnimationAuthoring.ActionPose, float, float[], float[], float[], float[], float, float)、LeonKnifeAnimationAuthoring.Low() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)；下游（含内部回调体） LeonKnifeAnimationAuthoring.ActionPose.ActionPose(int)、LeonKnifeAnimationAuthoring.ActionPose.Blend(LeonKnifeAnimationAuthoring.ActionPose, LeonKnifeAnimationAuthoring.ActionPose, float)、LeonKnifeAnimationAuthoring.Ease(float, float, float)、LeonKnifeAnimationAuthoring.High()、LeonKnifeAnimationAuthoring.LeftArmAction(int)、LeonKnifeAnimationAuthoring.LeftArmStaggeredTiming(LeonKnifeAnimationAuthoring.ActionPose, float, float[], float[], float[], float[], float, float)、LeonKnifeAnimationAuthoring.Low() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    /// <param name="time">时间。</param>
    static ActionPose ComputePose(int id, float time)
    {
        if (id == 4 || id == 12)
        {
            return TwoAttacksMomentumTransfer(time, false);
        }

        if (id == 5 || id == 13)
        {
            return TwoAttacksMomentumTransfer(time, true);
        }

        if (id == 6)
        {
            // 远离小刀。
var awayKnife = TwoAttacksMomentumTransfer(time / 3f, true);
            return ActionPose.Blend(awayKnife, Low(), Ease(0, 1, time));
        }

        if (id == 7)
        {
            return TwoAttacksMomentumTransfer(1 + time / 3f, true);
        }

        if (id == 8)
        {
            return ActionPose.Blend(Low(), High(), Ease(0, 1, 1 - Mathf.Pow(1 - time, 1.32f)));
        }

        if (id == 9)
        {
            // 防守。
var guard = High();
            // 释放。
var release = Low();
            // 释放值。
var releaseValue = ActionPose.Blend(guard, release, Ease(0, 1, time));
            // 物品。
for (int item = 0; item < 9; item++)
            {
                // 延后。
float late;
                if (item < 2)
                {
                    late = 0;
                }
                else
                {
                    if (item < 5)
                    {
                        late = .04f;
                    }
                    else
                    {
                        late = .10f;
                    }
                }

                releaseValue.rightArm[item] = Mathf.Lerp(guard.rightArm[item], release.rightArm[item], Ease(late, 1, time));
                releaseValue.leftArm[item] = Mathf.Lerp(guard.leftArm[item], release.leftArm[item], Ease(late + .045f, 1, time));
            }

            return releaseValue;
        }

        if (id == 2)
        {
            // 抬高小刀。
var raiseKnife = ActionPose.Blend(SheathPosition(), High(), .70f);
            raiseKnife.rightArm[2] += .08f;
            if (time < .42f)
            {
                return ActionPose.Blend(SheathPosition(), raiseKnife, Ease(.075f, .42f, time));
            }
            else
            {
                return ActionPose.Blend(raiseKnife, High(), Ease(.42f, 1, time));
            }
        }

        if (id == 3)
        {
            // 刀鞘对齐状态。
var sheathAlignment = ActionPose.Blend(Low(), SheathPosition(), .50f);
            sheathAlignment.rightArm[2] += .04f;
            if (time < .58f)
            {
                return ActionPose.Blend(Low(), sheathAlignment, Ease(0, .58f, time));
            }
            else
            {
                return ActionPose.Blend(sheathAlignment, SheathPosition(), Ease(.58f, .92f, time));
            }
        }

        // 当前动作姿态。
global::LeonKnifeAnimationAuthoring.ActionPose result;
        if (id == 0)
        {
            result = Low();
        }
        else
        {
            result = High();
        }

        if (id < 2)
        {
            // 呼吸。
float breathing = Mathf.Sin(time * Mathf.PI * 2);
            result.forwardLean += .007f * breathing;
            result.sink -= .0012f * breathing;
        }

        if (id == 10)
        {
            // 接触。
var contact = new ActionPose(7)
            {
                forwardLean = .10f,
                bodyTiltAngle = 9f,
                sink = .047f,
                chestTwist = .09f,
                hipsTwist = 2.5f
            };
            contact.rightArm[0] += .08f;
            contact.rightArm[2] += .22f;
            contact.rightArm[5] -= .07f;
            if (time < .22f)
            {
                return ActionPose.Blend(High(), contact, Ease(0, .22f, time));
            }
            else
            {
                return ActionPose.Blend(contact, High(), Ease(.22f, 1, time));
            }
        }

        if (id == 11)
        {
            // 恢复。
var recover = new ActionPose(7)
            {
                forwardLean = .09f,
                sink = .045f
            };
            // 散布。
var spread = ActionPose.Blend(High(), new ActionPose(4), .86f);
            spread.rightArm[5] -= .20f;
            spread.leftArm = ActionPose.Blend(High(), new ActionPose(4) { leftArm = LeftArmAction(12) }, .64f).leftArm;
            spread.chestTwist = -.37f;
            spread.hipsTwist = 12;
            spread.sink = .043f;
            if (time < .16f)
            {
                result = ActionPose.Blend(High(), recover, Ease(0, .16f, time));
            }
            else if (time < .60f)
            {
                result = ActionPose.Blend(recover, spread, Ease(.16f, .60f, time));
            }
            else
            {
                result = ActionPose.Blend(spread, High(), Ease(.60f, 1, time));
            }

            result.sink = Mathf.Lerp(result.sink, High().sink, Ease(.59f, .80f, time));
            // 防守。
var guard = High().leftArm;
            LeftArmStaggeredTiming(result, time, guard, LeftArmAction(9), spread.leftArm, guard, .11f, .53f);
            return result;
        }

        return result;
    }

    /// <summary>编辑器配置与校验模块 AuthoringSkeleton；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀动画制作器+制作骨架")]
    sealed class AuthoringSkeleton : IDisposable
    {
        /// <summary>动画控制器。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly Animator animator;
        /// <summary>处理器。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly HumanPoseHandler handler;
        /// <summary>基础。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("基础")]
        [UnityEngine.InspectorName("基础")]
        public HumanPose baseValue;
        /// <summary>骨盆。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly Vector3 pelvis;
        /// <summary>脚部位置。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly Vector3[] footPosition = new Vector3[2];
        /// <summary>脚部旋转。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly Quaternion[] footRotate = new Quaternion[2];
        /// <summary>脚误差。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("脚误差")]
        [UnityEngine.InspectorName("脚误差")]
        public float footError;
        /// <summary>构造并初始化 AuthoringSkeleton 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)、LeonKnifeAnimationAuthoring.Generate()；下游（含内部回调体） HumanPose.HumanPose()、HumanPoseHandler.GetHumanPose(ref HumanPose)、HumanPoseHandler.HumanPoseHandler(Avatar, Transform)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Bone(HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="source">来源动画控制器。</param>
        public AuthoringSkeleton(Animator source)
        {
            animator = source;
            handler = new HumanPoseHandler(source.avatar, source.transform);
            baseValue = new HumanPose();
            handler.GetHumanPose(ref baseValue);
            pelvis = Bone(HumanBodyBones.Hips).position;
            // 边缘。
for (int edge = 0; edge < 2; edge++)
            {
                // 分支选出的人形身体骨骼集合的左脚。
global::UnityEngine.HumanBodyBones selectedValue82;
                if (edge == 0)
                {
                    selectedValue82 = HumanBodyBones.LeftFoot;
                }
                else
                {
                    selectedValue82 = HumanBodyBones.RightFoot;
                }

                // 脚部。
var foot = Bone(selectedValue82);
                footPosition[edge] = foot.position;
                footRotate[edge] = foot.rotation;
            }
        }

        /// <summary>Bone 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringSkeleton.Apply(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.AuthoringSkeleton(Animator)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.FootLock(ref HumanPose, Vector3)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="name">名称。</param>
        public Transform Bone(HumanBodyBones name)
        {
            // 骨骼。
var bone = animator.GetBoneTransform(name);
            if (!bone)
            {
                throw new InvalidOperationException("里昂缺少制作所需的人形骨骼。");
            }

            return bone;
        }

        /// <summary>Apply 的业务处理入口；本方法直接依赖 HumanPoseHandler.SetHumanPose(ref HumanPose)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Bone(HumanBodyBones)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.FootLock(ref HumanPose, Vector3)；下游（含内部回调体） HumanPoseHandler.SetHumanPose(ref HumanPose)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Bone(HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="hips">髋部。</param>
        public void Apply(ref HumanPose pose, Vector3 hips)
        {
            handler.SetHumanPose(ref pose);
            pose.bodyPosition += (hips - Bone(HumanBodyBones.Hips).position) / animator.humanScale;
            handler.SetHumanPose(ref pose);
        }

        /// <summary>FootLock 的业务处理入口；本方法直接依赖 HumanPoseHandler.GetHumanPose(ref HumanPose)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Apply(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Bone(HumanBodyBones)、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Sqrt(float) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.AuthoringFrame(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int, float)、LeonKnifeAnimationAuthoring.CalibrationHorizontalGrip(Animator, int)；下游（含内部回调体） HumanPoseHandler.GetHumanPose(ref HumanPose)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Apply(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Bone(HumanBodyBones)、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Sqrt(float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="pose">姿态。按引用传入，读写会作用于调用方原存储位置。</param>
        /// <param name="hips">髋部。</param>
        public void FootLock(ref HumanPose pose, Vector3 hips)
        {
            Apply(ref pose, hips);
            // 边缘。
for (int edge = 0; edge < 2; edge++)
            {
                // 是否右侧。
bool right = edge == 1;
                // 分支选出的人形身体骨骼集合的右侧大腿。
global::UnityEngine.HumanBodyBones selectedValue83;
                if (right)
                {
                    selectedValue83 = HumanBodyBones.RightUpperLeg;
                }
                else
                {
                    selectedValue83 = HumanBodyBones.LeftUpperLeg;
                }

                // 上部。
var upper = Bone(selectedValue83);
                // 分支选出的人形身体骨骼集合的右侧小腿。
global::UnityEngine.HumanBodyBones selectedValue84;
                if (right)
                {
                    selectedValue84 = HumanBodyBones.RightLowerLeg;
                }
                else
                {
                    selectedValue84 = HumanBodyBones.LeftLowerLeg;
                }

                // 下部。
var lower = Bone(selectedValue84);
                // 分支选出的人形身体骨骼集合的右脚。
global::UnityEngine.HumanBodyBones selectedValue85;
                if (right)
                {
                    selectedValue85 = HumanBodyBones.RightFoot;
                }
                else
                {
                    selectedValue85 = HumanBodyBones.LeftFoot;
                }

                // 脚部。
var foot = Bone(selectedValue85);

                // 中文含义：upperLong 为“上部长型”。；lowerLong 为“下部长型”。
float upperLong = Vector3.Distance(upper.position, lower.position), lowerLong = Vector3.Distance(lower.position, foot.position);

                // 中文含义：offset 为“偏移量”。；direction 为“方向”。
Vector3 offset = footPosition[edge] - upper.position, direction = offset.normalized;
                // 距离。
float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(upperLong - lowerLong) + .001f, (upperLong + lowerLong) * .9999f);
                // 膝盖方向。
Vector3 kneeDirection = Vector3.ProjectOnPlane(footPosition[edge] + new Vector3(0, .38f, .30f) * animator.humanScale - upper.position, direction).normalized;
                // 沿线。
float along = (upperLong * upperLong - lowerLong * lowerLong + distance * distance) / (2 * distance);
                // 膝盖。
Vector3 knee = upper.position + direction * along + kneeDirection * Mathf.Sqrt(Mathf.Max(0, upperLong * upperLong - along * along));
                upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, knee - upper.position) * upper.rotation;
                lower.rotation = Quaternion.FromToRotation(foot.position - lower.position, footPosition[edge] - lower.position) * lower.rotation;
                foot.rotation = footRotate[edge];
                handler.GetHumanPose(ref pose);
                Apply(ref pose, hips);
                footError = Mathf.Max(footError, Vector3.Distance(foot.position, footPosition[edge]));
            }
        }

        /// <summary>Dispose 的业务处理入口；本方法直接依赖 HumanPoseHandler.Dispose()，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） HumanPoseHandler.Dispose()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public void Dispose()
        {
            handler.Dispose();
        }
    }

    /// <summary>AuthoringFrame 的业务处理入口；本方法直接依赖 Array.IndexOf&lt;string&gt;(string[], string)、LeonKnifeAnimationAuthoring.AdjustAlertFingerShape(ref HumanPose, LeonKnifeAnimationAuthoring.ActionPose)、LeonKnifeAnimationAuthoring.ArmParameter(ref HumanPose, bool, float[])、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Apply(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.FootLock(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.Copy(HumanPose) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.Bake(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int)；下游（含内部回调体） Array.IndexOf&lt;string&gt;(string[], string)、LeonKnifeAnimationAuthoring.AdjustAlertFingerShape(ref HumanPose, LeonKnifeAnimationAuthoring.ActionPose)、LeonKnifeAnimationAuthoring.ArmParameter(ref HumanPose, bool, float[])、LeonKnifeAnimationAuthoring.AuthoringSkeleton.Apply(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.AuthoringSkeleton.FootLock(ref HumanPose, Vector3)、LeonKnifeAnimationAuthoring.ComputePose(int, float)、LeonKnifeAnimationAuthoring.Copy(HumanPose) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rig">绑定。</param>
    /// <param name="id">编号。</param>
    /// <param name="time">时间。</param>
    static HumanPose AuthoringFrame(AuthoringSkeleton rig, int id, float time)
    {
        // 配置。
var configuration = ComputePose(id, time);
        // 姿态。
var pose = Copy(rig.baseValue);
        ArmParameter(ref pose, true, configuration.rightArm);
        ArmParameter(ref pose, false, configuration.leftArm);
        LeftHandNaturalFingerShape(ref pose);
        AdjustAlertFingerShape(ref pose, configuration);
        RightHandGripFineTune(ref pose);
        if (id == 10 || id == 11)
        {
            // 恢复头部。
float recoverHead = Ease(0, .17f, time) * (1 - Ease(.32f, .85f, time));
            // 颈部。
int neck = Array.IndexOf(HumanTrait.MuscleName, "Neck Nod Down-Up");
            if (neck >= 0)
            {
                pose.muscles[neck] -= .11f * recoverHead;
            }
        }

        Muscle(ref pose, "Spine Twist Left-Right", configuration.chestTwist * .55f);
        Muscle(ref pose, "Chest Twist Left-Right", configuration.chestTwist * .45f);
        Muscle(ref pose, "Spine Front-Back", configuration.forwardLean);
        Muscle(ref pose, "Chest Front-Back", .045f);
        Muscle(ref pose, "Head Turn Left-Right", -configuration.chestTwist * .25f);
        // 按原顺序缓存的实参0。
float argumentValue88;
        if (id >= 12)
        {
            argumentValue88 = 0;
        }
        else
        {
            argumentValue88 = configuration.bodyTiltAngle;
        }

        // 分支选出的0。
float selectedValue87;
        if (id >= 12)
        {
            selectedValue87 = 0;
        }
        else
        {
            selectedValue87 = configuration.hipsTwist;
        }

        pose.bodyRotation = Quaternion.Euler(argumentValue88, selectedValue87, 0) * rig.baseValue.bodyRotation;
        // 髋部。
Vector3 hips = rig.pelvis + Vector3.down * configuration.sink;
        if (id < 12)
        {
            rig.FootLock(ref pose, hips);
        }
        else
        {
            rig.Apply(ref pose, hips);
        }

        return pose;
    }

    /// <summary>BindingName 的业务处理入口；本方法直接依赖 string.StartsWith(string)、string.Substring(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.Bake(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int)；下游（含内部回调体） string.StartsWith(string)、string.Substring(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    static string BindingName(string name)
    {
        // side（侧边）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationAuthoring.BindingName(string)。
foreach (string side in new[]
        {
            "Left",
            "Right"
        }

        )
        {
            // finger（手指）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonKnifeAnimationAuthoring.BindingName(string)。
foreach (string finger in new[]
            {
                "Thumb",
                "Index",
                "Middle",
                "Ring",
                "Little"
            }

            )
            {
                // 前缀。
string prefix = side + " " + finger + " ";
                if (name.StartsWith(prefix))
                {
                    return side + "Hand." + finger + "." + name.Substring(prefix.Length);
                }
            }
        }

        return name;
    }

    /// <summary>LowerBodyMuscle 的业务处理入口；本方法直接依赖 string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.Bake(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int)；下游（含内部回调体） string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    static bool LowerBodyMuscle(string name) => name.Contains("Leg") || name.Contains("Foot") || name.Contains("Toes");
    /// <summary>Bake 的业务处理入口；本方法直接依赖 AnimationClip.AnimationClip()、AnimationCurve.AddKey(float, float)、AnimationCurve.AnimationCurve()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.Generate()；下游（含内部回调体） AnimationClip.AnimationClip()、AnimationCurve.AddKey(float, float)、AnimationCurve.AnimationCurve()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rig">绑定。</param>
    /// <param name="id">编号。</param>
    static void Bake(AuthoringSkeleton rig, int id)
    {
        // 持续时间。
float duration = clipDuration[id];
        // 帧数量。
int frameCount = Mathf.CeilToInt(duration * 60);
        // 曲线。
var curve = Enumerable.Range(0, HumanTrait.MuscleCount + 7).Select( /* 中文含义：物品。 */item => new AnimationCurve()).ToArray();
        // 首个帧。
HumanPose firstFrame = default;
        // 帧。
for (int frame = 0; frame <= frameCount; frame++)
        {
            // 姿态。
global::UnityEngine.HumanPose pose;
            if (id < 2 && frame == frameCount)
            {
                pose = firstFrame;
            }
            else
            {
                pose = AuthoringFrame(rig, id, frame / (float)frameCount);
            }

            if (frame == 0)
            {
                firstFrame = Copy(pose);
            }

            // 时间戳。
float timestamp = duration * frame / frameCount;
            // 物品。
for (int item = 0; item < HumanTrait.MuscleCount; item++)
            {
                curve[item].AddKey(timestamp, pose.muscles[item]);
            }

            // 身体。
float[] body =
            {
                pose.bodyPosition.x,
                pose.bodyPosition.y,
                pose.bodyPosition.z,
                pose.bodyRotation.x,
                pose.bodyRotation.y,
                pose.bodyRotation.z,
                pose.bodyRotation.w
            };
            // 物品。
for (int item = 0; item < 7; item++)
            {
                curve[HumanTrait.MuscleCount + item].AddKey(timestamp, body[item]);
            }
        }

        // 动画。
var animation = new AnimationClip
        {
            name = clipName[id],
            frameRate = 60
        };
        // 物品。
for (int item = 0; item < HumanTrait.MuscleCount; item++)
        {
            if (id >= 12 && LowerBodyMuscle(HumanTrait.MuscleName[item]))
            {
                continue;
            }

            WriteCurve(animation, BindingName(HumanTrait.MuscleName[item]), curve[item]);
        }

        if (id < 12)
        {
            // 根文本数组。
string[] root =
            {
                "RootT.x",
                "RootT.y",
                "RootT.z",
                "RootQ.x",
                "RootQ.y",
                "RootQ.z",
                "RootQ.w"
            };
            // 物品。
for (int item = 0; item < 7; item++)
            {
                WriteCurve(animation, root[item], curve[HumanTrait.MuscleCount + item]);
            }
        }

        // 集合。
var set = AnimationUtility.GetAnimationClipSettings(animation);
        set.loopTime = id < 2;
        set.keepOriginalPositionXZ = true;
        set.keepOriginalPositionY = true;
        set.keepOriginalOrientation = true;
        set.loopBlendOrientation = true;
        set.loopBlendPositionXZ = true;
        set.loopBlendPositionY = true;
        AnimationUtility.SetAnimationClipSettings(animation, set);
        // 路径。
string path = outputDirectory + "/" + animation.name + ".anim";
        // 原始是否具有。
var originalHas = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (originalHas)
        {
            EditorUtility.CopySerialized(animation, originalHas);
            EditorUtility.SetDirty(originalHas);
            Object.DestroyImmediate(animation);
        }
        else
        {
            AssetDatabase.CreateAsset(animation, path);
        }
    }

    /// <summary>WriteCurve 的业务处理入口；本方法直接依赖 AnimationCurve.Constant(float, float, float)、AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve)、AnimationUtility.SetKeyLeftTangentMode(AnimationCurve, int, AnimationUtility.TangentMode)、AnimationUtility.SetKeyRightTangentMode(AnimationCurve, int, AnimationUtility.TangentMode)、EditorCurveBinding.FloatCurve(string, Type, string)、IEnumerable&lt;Keyframe&gt;.All&lt;Keyframe&gt;(Func&lt;Keyframe, bool&gt;)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationAuthoring.Bake(LeonKnifeAnimationAuthoring.AuthoringSkeleton, int)；下游（含内部回调体） AnimationCurve.Constant(float, float, float)、AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve)、AnimationUtility.SetKeyLeftTangentMode(AnimationCurve, int, AnimationUtility.TangentMode)、AnimationUtility.SetKeyRightTangentMode(AnimationCurve, int, AnimationUtility.TangentMode)、EditorCurveBinding.FloatCurve(string, Type, string)、IEnumerable&lt;Keyframe&gt;.All&lt;Keyframe&gt;(Func&lt;Keyframe, bool&gt;)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animation">动画。</param>
    /// <param name="name">名称。</param>
    /// <param name="curve">曲线。</param>
    static void WriteCurve(AnimationClip animation, string name, AnimationCurve curve)
    {
        if (curve.length > 2)
        {
            // 键。
var key = curve.keys;
            // 首个值。
float firstValue = key[0].value;
            if (key.All( /* 中文含义：当前关键帧。 */item => Mathf.Abs(item.value - firstValue) <= .000001f))
            {
                curve = AnimationCurve.Constant(key[0].time, key[key.Length - 1].time, firstValue);
            }
        }

        // 密集线性切线消除自动切线的关节过冲，分段速度由创作时序决定。
        // 物品。
for (int item = 0; item < curve.length; item++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, item, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, item, AnimationUtility.TangentMode.Linear);
        }

        AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve("", typeof(Animator), name), curve);
    }
}
