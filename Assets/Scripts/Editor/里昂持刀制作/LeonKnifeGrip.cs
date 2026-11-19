using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>独立候选的刀柄与包握共同标定，已确认的外拨保留原握姿。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀握姿")]
public static class LeonKnifeGrip
{
    /// <summary>中文含义：原始小刀轴。只读字段，初始化后不由普通写入口替换。使用位置：作为 Vector3.ProjectOnPlane(Vector3, Vector3) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)、LeonKnifeAnimationPreview.PreviewCharacter.PreviewCharacter() 等共 5 处。</summary>
    public static readonly Vector3 originalKnifeAxis = new Vector3(-.864971f, .364787f, -.344610f).normalized;
    /// <summary>中文含义：原始刀刃方向。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.BladeDirection.get。</summary>
    public static readonly Vector3 originalBladeDirection = Vector3.ProjectOnPlane(new Vector3(.44767f, .85428f, -.26419f), originalKnifeAxis).normalized;
    /// <summary>中文含义：原始握持中心。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。</summary>
    public static readonly Vector3 originalGripCenter = new Vector3(.012f, .078f, .027f);
    /// <summary>中文含义：校准路径。编译期常量，保持既有值与消费者映射。使用位置：作为 File.ReadAllText(string) 的参数。</summary>
    public const string calibrationPath = "Assets/Animations/左臂整体重构/握柄标定.json";
    /// <summary>中文含义：校准。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.fingerBoneRotation、LeonKnifeGrip.ReadDirection(string)、LeonKnifeGrip.ReadRotate(string)。</summary>
    static readonly Newtonsoft.Json.Linq.JObject calibration = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(calibrationPath));
    /// <summary>ReadDirection 的业务处理入口；本方法直接依赖 JToken.ToObject&lt;float[]&gt;()、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeGrip.gripCenter；下游（含内部回调体） JToken.ToObject&lt;float[]&gt;()、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 JObject.this[string] 的参数。</param>
    static Vector3 ReadDirection(string name)
    {
        // v（当前浮点数值数组）：保存 JToken.ToObject<float[]>() 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
var v = calibration[name].ToObject<float[]>();
        return new Vector3(v[0], v[1], v[2]);
    }

    /// <summary>ReadRotate 的业务处理入口；本方法直接依赖 JToken.ToObject&lt;float[]&gt;()、Quaternion.Quaternion(float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeGrip.gripRotate；下游（含内部回调体） JToken.ToObject&lt;float[]&gt;()、Quaternion.Quaternion(float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 JObject.this[string] 的参数。</param>
    static Quaternion ReadRotate(string name)
    {
        // q（当前浮点数值数组）：保存 JToken.ToObject<float[]>() 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Quaternion(float, float, float, float) 的参数。
var q = calibration[name].ToObject<float[]>();
        return new Quaternion(q[0], q[1], q[2], q[3]);
    }

    /// <summary>中文含义：握持中心。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。</summary>
    public static readonly Vector3 gripCenter = ReadDirection("握心");
    /// <summary>中文含义：握持旋转。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)、LeonKnifeGrip.BladeDirection.get、LeonKnifeGrip.KnifeAxis.get。</summary>
    public static readonly Quaternion gripRotate = ReadRotate("旋转四元数");
    /// <summary>中文含义：手指骨骼旋转。只读字段，初始化后不由普通写入口替换。使用位置：LeonKnifeGrip.ApplyFinger(Animator, AnimationClip)、LeonKnifeGrip.WriteCompensationClip(Animator)。</summary>
    static readonly Dictionary<string, float[]> fingerBoneRotation = calibration["指骨局部旋转"].ToObject<Dictionary<string, float[]>>();
    /// <summary>中文含义：小刀轴。使用位置：LeonKnifeGrip.ValidKnifeAxis(AnimationClip)。</summary>
    public static Vector3 KnifeAxis => gripRotate * originalKnifeAxis;
    /// <summary>中文含义：刀刃方向。</summary>
    public static Vector3 BladeDirection => gripRotate * originalBladeDirection;

    /// <summary>UseCombinedGripPose 的业务处理入口；本方法直接依赖 AssetDatabase.GetAssetPath(Object)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeGrip.ApplyFinger(Animator, AnimationClip)、LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)、LeonKnifeGrip.ValidKnifeAxis(AnimationClip)；下游（含内部回调体） AssetDatabase.GetAssetPath(Object)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 AssetDatabase.GetAssetPath(Object) 的参数、LeonKnifeGrip.UseCombinedGripPose(AnimationClip)。</param>
    public static bool UseCombinedGripPose(AnimationClip clip)
    {
        return clip && clip.name != "斜上外拨弹反" && AssetDatabase.GetAssetPath(clip).StartsWith("Assets/Animations/左臂整体重构/", StringComparison.Ordinal);
    }

    /// <summary>ApplyKnifeVisual 的业务处理入口；本方法直接依赖 LeonKnifeGrip.UseCombinedGripPose(AnimationClip)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)；下游（含内部回调体） LeonKnifeGrip.UseCombinedGripPose(AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="knifeVisual">中文含义：小刀视觉表现。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。</param>
    /// <param name="originalPosition">中文含义：原始位置。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。</param>
    /// <param name="originalRotation">中文含义：原始旋转。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。</param>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 LeonKnifeGrip.UseCombinedGripPose(AnimationClip) 的参数。</param>
    public static void ApplyKnifeVisual(Transform knifeVisual, Vector3 originalPosition, Quaternion originalRotation, AnimationClip clip)
    {
        // combined（是否合并后的）：保存 LeonKnifeGrip.UseCombinedGripPose(AnimationClip) 的返回结果，供当前步骤使用。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。
bool combined = UseCombinedGripPose(clip);
        // assignmentReceiver1（赋值前保存的目标小刀视觉表现）：从小刀视觉表现读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。
global::UnityEngine.Transform assignmentReceiver1 = knifeVisual;
        if (combined)
        {
            assignmentReceiver1.localPosition = gripCenter + gripRotate * (originalPosition - originalGripCenter);
        }
        else
        {
            assignmentReceiver1.localPosition = originalPosition;
        }

        // assignmentReceiver3（赋值前保存的目标小刀视觉表现）：从小刀视觉表现读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeGrip.ApplyKnifeVisual(Transform, Vector3, Quaternion, AnimationClip)。
global::UnityEngine.Transform assignmentReceiver3 = knifeVisual;
        if (combined)
        {
            assignmentReceiver3.localRotation = gripRotate * originalRotation;
        }
        else
        {
            assignmentReceiver3.localRotation = originalRotation;
        }
    }

    /// <summary>ValidKnifeAxis 的业务处理入口；本方法直接依赖 LeonKnifeGrip.UseCombinedGripPose(AnimationClip)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonKnifeGrip.UseCombinedGripPose(AnimationClip)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 LeonKnifeGrip.UseCombinedGripPose(AnimationClip) 的参数。</param>
    public static Vector3 ValidKnifeAxis(AnimationClip clip)
    {
        if (UseCombinedGripPose(clip))
        {
            return KnifeAxis;
        }
        else
        {
            return originalKnifeAxis;
        }
    }

    /// <summary>ApplyFinger 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponentsInChildren&lt;Transform&gt;(bool)、Dictionary&lt;string, float[]&gt;.TryGetValue(string, out float[])、LeonKnifeGrip.UseCombinedGripPose(AnimationClip)、Quaternion.Quaternion(float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponentsInChildren&lt;Transform&gt;(bool)、Dictionary&lt;string, float[]&gt;.TryGetValue(string, out float[])、LeonKnifeGrip.UseCombinedGripPose(AnimationClip)、Quaternion.Quaternion(float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：LeonKnifeGrip.ApplyFinger(Animator, AnimationClip)。</param>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 LeonKnifeGrip.UseCombinedGripPose(AnimationClip) 的参数。</param>
    public static void ApplyFinger(Animator animator, AnimationClip clip)
    {
        if (!UseCombinedGripPose(clip))
        {
            return;
        }

        // bone（骨骼）：每轮从取得骨骼变换的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<string, float[]>.TryGetValue(string, out float[]) 的参数、LeonKnifeGrip.ApplyFinger(Animator, AnimationClip)。
foreach (var bone in animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponentsInChildren<Transform>(true))
        {
            if (fingerBoneRotation.TryGetValue(bone.name, out var /* 中文含义：当前浮点数值数组。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ q))
            {
                bone.localRotation = new Quaternion(q[0], q[1], q[2], q[3]);
            }
        }
    }

    /// <summary>WriteCompensationClip 的业务处理入口；本方法直接依赖 AnimationClip.AnimationClip()、AnimationClip.ClearCurves()、AnimationClip.EnsureQuaternionContinuity()、AnimationCurve.Constant(float, float, float)、AnimationUtility.CalculateTransformPath(Transform, Transform)、AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve)、Animator.GetBoneTransform(HumanBodyBones) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimationClip.AnimationClip()、AnimationClip.ClearCurves()、AnimationClip.EnsureQuaternionContinuity()、AnimationCurve.Constant(float, float, float)、AnimationUtility.CalculateTransformPath(Transform, Transform)、AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve)、Animator.GetBoneTransform(HumanBodyBones) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：作为 AnimationUtility.CalculateTransformPath(Transform, Transform) 的参数、LeonKnifeGrip.WriteCompensationClip(Animator)。</param>
    public static void WriteCompensationClip(Animator animator)
    {
        // path（路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的参数。
const string path = "Assets/Animations/左臂整体重构/持刀包握补偿.anim";
        // clip（动画片段）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve) 的参数、作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 4 处。
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.ClearCurves();
        clip.name = "持刀包握补偿";
        clip.frameRate = 60;
        // bone（骨骼）：每轮从取得骨骼变换的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimationUtility.CalculateTransformPath(Transform, Transform) 的参数、作为 Dictionary<string, float[]>.TryGetValue(string, out float[]) 的参数。
foreach (var bone in animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponentsInChildren<Transform>(true))
        {
            if (!fingerBoneRotation.TryGetValue(bone.name, out var /* 中文含义：当前浮点数值数组。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ q))
            {
                continue;
            }

            // pathValue（路径值）：保存 AnimationUtility.CalculateTransformPath(Transform, Transform) 的返回结果，供当前步骤使用。使用位置：作为 EditorCurveBinding.FloatCurve(string, Type, string) 的参数。
string pathValue = AnimationUtility.CalculateTransformPath(bone, animator.transform);
            // i（当前遍历索引）：使用位置：作为 string.this[int] 的参数、LeonKnifeGrip.WriteCompensationClip(Animator)。
for (int i = 0; i < 4; i++)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pathValue, typeof(Transform), "m_LocalRotation." + "xyzw"[i]), AnimationCurve.Constant(0, 3, q[i]));
            }
        }

        clip.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(clip);
    }
}
