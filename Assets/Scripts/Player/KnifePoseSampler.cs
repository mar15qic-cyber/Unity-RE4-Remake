using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>按已验收预览的流程求值完整姿态，由正式动画驱动器提交到角色。</summary>
public sealed class KnifePoseSampler : IDisposable
{
    /// <summary>中文含义：模型。只读字段，初始化后不由普通写入口替换。使用位置：作为 AnimationClip.SampleAnimation(GameObject, float) 的参数、作为 KnifePoseSampler.GetPath(Transform, Transform) 的参数、作为 Object.Destroy(Object) 的参数 等共 8 处。</summary>
    private readonly GameObject model;
    /// <summary>中文含义：动画控制器。只读字段，初始化后不由普通写入口替换。使用位置：作为 AnimationPlayableOutput.Create(PlayableGraph, string, Animator) 的参数、KnifePoseSampler.Animator.get、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 等共 4 处。</summary>
    private readonly Animator animator;
    /// <summary>中文含义：配置。只读字段，初始化后不由普通写入口替换。使用位置：作为 Dictionary&lt;string, Transform&gt;.TryGetValue(string, out Transform) 的参数、作为 KnifePoseSampler.CreateClip(AnimationClip) 的参数、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 等共 4 处。</summary>
    private readonly KnifeCombatConfiguration configuration;
    /// <summary>中文含义：骨骼集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 KnifePoseSampler.GetPath(Transform, Transform) 的参数、作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数 等共 4 处。</summary>
    private readonly Transform[] bones;
    /// <summary>中文含义：目标列表。只读字段，初始化后不由普通写入口替换。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifePoseSampler.Apply(float) 等共 9 处。</summary>
    private readonly Transform[] targets;
    /// <summary>中文含义：上部骨骼集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private readonly Transform[] upperBones;
    private readonly bool[] lowerBody;
    private readonly bool[] rightArm;
    /// <summary>中文含义：脊柱。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private readonly Transform spine;
    private readonly Transform guardHips;
    private readonly Transform guardLeftThigh;
    private readonly Transform guardRightThigh;
    private readonly Transform guardLeftKnee;
    private readonly Transform guardRightKnee;
    private readonly Transform leftFoot;
    private readonly Transform rightFoot;
    private float guardTurnWeight;
    private float guardStepPhase;
    /// <summary>中文含义：上部位置集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private readonly Vector3[] upperPositions;
    /// <summary>中文含义：上部旋转集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private readonly Quaternion[] upperRotations;
    /// <summary>中文含义：混合位置集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</summary>
    private readonly Vector3[] blendPositions;
    /// <summary>中文含义：混合旋转集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</summary>
    private readonly Quaternion[] blendRotations;
    /// <summary>中文含义：已显示位置集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</summary>
    private readonly Vector3[] presentedPositions;
    /// <summary>中文含义：已显示旋转集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</summary>
    private readonly Quaternion[] presentedRotations;
    /// <summary>中文含义：底层位置集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.RestoreUnderlyingPose()。</summary>
    private readonly Vector3[] underlyingPositions;
    /// <summary>中文含义：底层旋转集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.RestoreUnderlyingPose()。</summary>
    private readonly Quaternion[] underlyingRotations;
    /// <summary>中文含义：手指骨骼集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private readonly Dictionary<string, Transform> fingerBones = new Dictionary<string, Transform>();
    /// <summary>中文含义：导航图。使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数、作为 AnimationLayerMixerPlayable.Create(PlayableGraph, int) 的参数、作为 AnimationMixerPlayable.Create(PlayableGraph, int) 的参数 等共 7 处。</summary>
    private PlayableGraph graph;
    /// <summary>中文含义：图层集合。使用位置：作为 AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationLayerMixerPlayable&gt;(AnimationLayerMixerPlayable) 的参数、作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationLayerMixerPlayable&gt;(AnimationClipPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.Connect&lt;AnimationMixerPlayable, AnimationLayerMixerPlayable&gt;(AnimationMixerPlayable, int, AnimationLayerMixerPlayable, int) 的参数 等共 6 处。</summary>
    private AnimationLayerMixerPlayable layers;
    /// <summary>中文含义：主要可播放。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationLayerMixerPlayable&gt;(AnimationClipPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.DestroyPlayable&lt;AnimationClipPlayable&gt;(AnimationClipPlayable) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private AnimationClipPlayable mainPlayable;
    /// <summary>中文含义：行走可播放。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private AnimationClipPlayable walkPlayable;
    /// <summary>中文含义：待机可播放。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private AnimationClipPlayable idlePlayable;
    /// <summary>中文含义：移动行为混音器。使用位置：作为 PlayableGraph.Connect&lt;AnimationClipPlayable, AnimationMixerPlayable&gt;(AnimationClipPlayable, int, AnimationMixerPlayable, int) 的参数、作为 PlayableGraph.Connect&lt;AnimationMixerPlayable, AnimationLayerMixerPlayable&gt;(AnimationMixerPlayable, int, AnimationLayerMixerPlayable, int) 的参数、作为 PlayableGraph.DestroySubgraph&lt;AnimationMixerPlayable&gt;(AnimationMixerPlayable) 的参数 等共 4 处。</summary>
    private AnimationMixerPlayable locomotionMixer;
    /// <summary>中文含义：当前动画片段。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private AnimationClip currentClip;
    /// <summary>中文含义：当前行走。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</summary>
    private AnimationClip currentWalk;
    /// <summary>中文含义：混合时长。使用位置：作为 Mathf.Clamp01(float) 的参数、KnifePoseSampler.BeginBlend(float, float)。</summary>
    private float blendDuration;
    /// <summary>中文含义：混合已用。使用位置：作为 Mathf.Clamp01(float) 的参数、KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.BeginBlend(float, float)。</summary>
    private float blendElapsed;
    /// <summary>中文含义：上一次混合时间。使用位置：作为 Mathf.Max(float, float) 的参数、KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.BeginBlend(float, float)。</summary>
    private double lastBlendTime;
    /// <summary>中文含义：是否具有混合。使用位置：KnifePoseSampler.Apply(float)、KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.ReleasePresentation()。</summary>
    private bool hasBlend;
    /// <summary>中文含义：是否具有已显示姿态。使用位置：KnifePoseSampler.BeginBlend(float, float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.ReleasePresentation()。</summary>
    private bool hasPresentedPose;
    /// <summary>中文含义：是否具有底层姿态。使用位置：KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.ReleasePresentation()、KnifePoseSampler.RestoreUnderlyingPose()。</summary>
    private bool hasUnderlyingPose;
    /// <summary>中文含义：模型。</summary>
    public GameObject Model
    {
        get
        {
            return model;
        }
    }

    /// <summary>中文含义：动画控制器。</summary>
    public Animator Animator
    {
        get
        {
            return animator;
        }
    }

    /// <summary>构造并初始化 KnifePoseSampler 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)；下游（含内部回调体） AnimationLayerMixerPlayable.Create(PlayableGraph, int)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationPlayableOutput.Create(PlayableGraph, string, Animator)、AnimationPlayableOutput.SetSourcePlayable&lt;AnimationPlayableOutput, AnimationLayerMixerPlayable&gt;(AnimationLayerMixerPlayable)、Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponentsInChildren&lt;Transform&gt;(bool)、GameObject.GetComponent&lt;Animator&gt;() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="configuration">中文含义：配置。使用位置：作为 Object.Instantiate&lt;GameObject&gt;(GameObject) 的参数、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</param>
    /// <param name="target">中文含义：目标动画控制器。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。</param>
    public KnifePoseSampler(KnifeCombatConfiguration configuration, Animator target)
    {
        this.configuration = configuration;
        if (configuration.poseSkeleton == null)
        {
            throw new InvalidOperationException("缺少已经验收的小刀采样骨架。");
        }

        model = UnityEngine.Object.Instantiate(configuration.poseSkeleton);
        model.name = "小刀姿态隔离求值";
        model.hideFlags = HideFlags.HideAndDontSave;
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.fireEvents = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // hips（髋部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。
Transform hips = RequireBone(HumanBodyBones.Hips);
        bones = hips.GetComponentsInChildren<Transform>(true);
        targets = new Transform[bones.Length];
        blendPositions = new Vector3[bones.Length];
        blendRotations = new Quaternion[bones.Length];
        presentedPositions = new Vector3[bones.Length];
        presentedRotations = new Quaternion[bones.Length];
        underlyingPositions = new Vector3[bones.Length];
        underlyingRotations = new Quaternion[bones.Length];
        // index（当前元素索引）：使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。
for (int index = 0; index < bones.Length; index++)
        {
            // path（路径）：保存 KnifePoseSampler.GetPath(Transform, Transform) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 Transform.Find(string) 的参数。
string path = GetPath(bones[index], model.transform);
            targets[index] = target.transform.Find(path);
            if (targets[index] == null)
            {
                throw new InvalidOperationException("正式角色缺少预览骨骼：" + path);
            }
        }

        spine = RequireBone(HumanBodyBones.Spine);
        guardHips = hips;
        guardLeftThigh = RequireBone(HumanBodyBones.LeftUpperLeg);
        guardRightThigh = RequireBone(HumanBodyBones.RightUpperLeg);
        guardLeftKnee = RequireBone(HumanBodyBones.LeftLowerLeg);
        guardRightKnee = RequireBone(HumanBodyBones.RightLowerLeg);
        leftFoot = RequireBone(HumanBodyBones.LeftFoot);
        rightFoot = RequireBone(HumanBodyBones.RightFoot);
        upperBones = spine.GetComponentsInChildren<Transform>(true);
        lowerBody = new bool[bones.Length];
        rightArm = new bool[bones.Length];
        Transform upperArm = RequireBone(HumanBodyBones.RightUpperArm);
        for (int index = 0; index < bones.Length; index++)
        {
            lowerBody[index] = bones[index] != spine && !bones[index].IsChildOf(spine);
            rightArm[index] = bones[index] == upperArm || bones[index].IsChildOf(upperArm);
        }
        upperPositions = new Vector3[upperBones.Length];
        upperRotations = new Quaternion[upperBones.Length];
        // bone（骨骼）：每轮从取得骨骼变换的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<string, Transform>.this[string] 的参数、KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。
foreach (Transform bone in RequireBone(HumanBodyBones.RightHand).GetComponentsInChildren<Transform>(true))
        {
            fingerBones[bone.name] = bone;
        }

        if (configuration.previewIdle != null && !configuration.previewIdle.humanMotion && !target.isHuman)
        {
            animator.avatar = target.avatar;
        }

        graph = PlayableGraph.Create("小刀完整姿态求值");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        layers = AnimationLayerMixerPlayable.Create(graph, 2);
        layers.SetInputWeight(0, 1f);
        layers.SetInputWeight(1, 0f);
        // output（输出）：保存 AnimationPlayableOutput.Create(PlayableGraph, string, Animator) 的返回结果，供当前步骤使用。使用位置：KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)。
AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "小刀采样骨架", animator);
        output.SetSourcePlayable(layers);
        graph.Play();
    }

    private Transform RequireBone(HumanBodyBones id)
    {
        Transform bone = AnimatorBoneBindings.Resolve(animator, id);
        if (bone == null)
        {
            throw new InvalidOperationException("小刀采样骨架缺少骨骼 " + id);
        }
        return bone;
    }

    /// <summary>GetPath 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="bone">中文含义：骨骼。使用位置：KnifePoseSampler.GetPath(Transform, Transform)。</param>
    /// <param name="root">中文含义：根变换节点。使用位置：KnifePoseSampler.GetPath(Transform, Transform)。</param>
    private static string GetPath(Transform bone, Transform root)
    {
        // path（路径）：使用位置：KnifePoseSampler.GetPath(Transform, Transform)。
string path = bone.name;
        // parent（父级）：使用位置：KnifePoseSampler.GetPath(Transform, Transform)。
Transform parent = bone.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    /// <summary>CreateClip 的业务处理入口；本方法直接依赖 AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)；下游（含内部回调体） AnimationClipPlayable.Create(PlayableGraph, AnimationClip)、AnimationClipPlayable.SetApplyFootIK(bool)、AnimationClipPlayable.SetApplyPlayableIK(bool)、AnimationClipPlayable.SetSpeed&lt;AnimationClipPlayable&gt;(double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的参数。</param>
    private AnimationClipPlayable CreateClip(AnimationClip clip)
    {
        // playable（可播放）：保存 AnimationClipPlayable.Create(PlayableGraph, AnimationClip) 的返回结果，供当前步骤使用。使用位置：KnifePoseSampler.CreateClip(AnimationClip)。
AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(0);
        return playable;
    }

    /// <summary>Sample 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationMixerPlayable.Create(PlayableGraph, int)、AnimationMixerPlayable.IsValid&lt;AnimationMixerPlayable&gt;()、AnimationMixerPlayable.SetInputWeight&lt;AnimationMixerPlayable&gt;(int, float) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationMixerPlayable.Create(PlayableGraph, int)、AnimationMixerPlayable.IsValid&lt;AnimationMixerPlayable&gt;()、AnimationMixerPlayable.SetInputWeight&lt;AnimationMixerPlayable&gt;(int, float) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 KnifePoseSampler.CreateClip(AnimationClip) 的参数、作为 Mathf.Clamp(float, float, float) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</param>
    /// <param name="actionTime">中文含义：动作时间。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    /// <param name="movementMode">中文含义：移动模式。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</param>
    /// <param name="locomotionTime">中文含义：移动行为时间。使用位置：作为 Mathf.Repeat(float, float) 的参数。</param>
    /// <param name="movementWeight">中文含义：移动权重。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    /// <param name="originalGrip">中文含义：是否原始握持。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。</param>
    public void Sample(AnimationClip clip, float actionTime, int movementMode, float locomotionTime, float movementWeight = 1f, bool originalGrip = false)
    {
        // walk（行走）：使用位置：作为 KnifePoseSampler.CreateClip(AnimationClip) 的参数、作为 Mathf.Repeat(float, float) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
AnimationClip walk = null;
        if (movementMode > 0 && movementMode <= configuration.previewLocomotion.Length)
        {
            walk = configuration.previewLocomotion[movementMode - 1];
        }

        if (currentClip != clip || currentWalk != walk)
        {
            if (mainPlayable.IsValid())
            {
                // input（输入数据）：使用位置：作为 PlayableGraph.Disconnect<AnimationLayerMixerPlayable>(AnimationLayerMixerPlayable, int) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
int input = 0;
                if (currentWalk != null)
                {
                    input = 1;
                }

                graph.Disconnect(layers, input);
                graph.DestroyPlayable(mainPlayable);
            }

            if (locomotionMixer.IsValid())
            {
                graph.Disconnect(layers, 0);
                graph.DestroySubgraph(locomotionMixer);
                walkPlayable = default;
                idlePlayable = default;
                locomotionMixer = default;
            }

            currentClip = clip;
            currentWalk = walk;
            mainPlayable = CreateClip(clip);
            if (walk != null)
            {
                walkPlayable = CreateClip(walk);
                idlePlayable = CreateClip(configuration.previewLocomotionIdle);
                locomotionMixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(idlePlayable, 0, locomotionMixer, 0);
                graph.Connect(walkPlayable, 0, locomotionMixer, 1);
                graph.Connect(locomotionMixer, 0, layers, 0);
                graph.Connect(mainPlayable, 0, layers, 1);
                layers.SetInputWeight(1, 0f);
            }
            else
            {
                graph.Connect(mainPlayable, 0, layers, 0);
                layers.SetInputWeight(1, 0f);
            }
        }

        mainPlayable.SetTime(Mathf.Clamp(actionTime, 0f, clip.length));
        if (walk != null)
        {
            walkPlayable.SetTime(Mathf.Repeat(locomotionTime, walk.length));
            idlePlayable.SetTime(0f);
            // weight（权重）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：作为 AnimationMixerPlayable.SetInputWeight<AnimationMixerPlayable>(int, float) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
float weight = Mathf.Clamp01(movementWeight);
            if (movementMode == 5)
            {
                // phase（阶段）：保存 Mathf.Repeat(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp01(float) 的参数。
float phase = Mathf.Repeat(locomotionTime, 3f);
                weight *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .35f));
                weight *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 1.8f) / .35f));
            }

            locomotionMixer.SetInputWeight(0, 1f - weight);
            locomotionMixer.SetInputWeight(1, weight);
            // 保留完整源动作的上身朝向，步态只决定骨盆和下身。
            clip.SampleAnimation(model, Mathf.Clamp(actionTime, 0f, clip.length));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // spineRotation（脊柱旋转）：使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
Quaternion spineRotation = spine.rotation;
            // index（当前元素索引）：使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
for (int index = 0; index < upperBones.Length; index++)
            {
                upperPositions[index] = upperBones[index].localPosition;
                upperRotations[index] = upperBones[index].localRotation;
            }

            graph.Evaluate(0f);
            graph.Evaluate(0f);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // index（当前元素索引）：使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
for (int index = 0; index < upperBones.Length; index++)
            {
                upperBones[index].localPosition = upperPositions[index];
                upperBones[index].localRotation = upperRotations[index];
            }

            spine.rotation = spineRotation;
            if (clip == configuration.previewIdle && clip.humanMotion)
            {
                // leftFoot（左脚）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
Transform leftFoot = RequireBone(HumanBodyBones.LeftFoot);
                // rightFoot（右脚）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
Transform rightFoot = RequireBone(HumanBodyBones.RightFoot);
                // stride（步幅）：使用位置：作为 Quaternion.AngleAxis(float, Vector3) 的参数。
float stride = Mathf.Clamp((leftFoot.position.z - rightFoot.position.z) / .65f, -1f, 1f) * weight;
                // leftArm（左侧手臂）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
Transform leftArm = RequireBone(HumanBodyBones.LeftUpperArm);
                // rightArm（右侧手臂）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
Transform rightArm = RequireBone(HumanBodyBones.RightUpperArm);
                leftArm.rotation = Quaternion.AngleAxis(stride * 7f, Vector3.right) * leftArm.rotation;
                rightArm.rotation = Quaternion.AngleAxis(-stride * 4f, Vector3.right) * rightArm.rotation;
            }
        }
        else
        {
            graph.Evaluate(0f);
            graph.Evaluate(0f);
        }

        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (!originalGrip)
        {
            // index（当前元素索引）：使用位置：KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
for (int index = 0; index < configuration.fingerBoneName.Length; index++)
            {
                // finger（手指）：使用位置：作为 Dictionary<string, Transform>.TryGetValue(string, out Transform) 的参数、KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool)。
Transform finger;
                if (fingerBones.TryGetValue(configuration.fingerBoneName[index], out finger))
                {
                    finger.localRotation = configuration.fingerBoneRotation[index];
                }
            }
        }
    }

    /// <summary>BeginBlend 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="elapsed">中文含义：已用。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    internal AnimationClip LocomotionClip => currentWalk;

    internal float ReadLeftFootHeightDifference()
    {
        return leftFoot.position.y - rightFoot.position.y;
    }

    internal void ApplyGuardFootwork(float turnSpeed, float dt)
    {
        float targetWeight = Mathf.Clamp(turnSpeed / 170f, -1f, 1f);
        guardTurnWeight = Mathf.MoveTowards(guardTurnWeight, targetWeight, dt * 7f);
        float weight = Mathf.Abs(guardTurnWeight);
        if (weight <= .001f)
        {
            guardStepPhase = 0f;
            return;
        }

        guardStepPhase = Mathf.Repeat(guardStepPhase + dt * Mathf.Lerp(1.3f, 2.5f, weight), 1f);
        float stride = Mathf.Sin(guardStepPhase * 2f * Mathf.PI);
        float step = stride * weight;
        Vector3 hipsPosition = guardHips.position;
        hipsPosition.y -= .028f * weight * Mathf.Abs(stride);
        guardHips.position = hipsPosition;
        guardHips.rotation = Quaternion.Euler(0f, -11f * guardTurnWeight, -4f * step) * guardHips.rotation;
        spine.rotation = Quaternion.AngleAxis(18f * guardTurnWeight, Vector3.up) * spine.rotation;
        guardLeftThigh.rotation = Quaternion.Euler(15f * step, -6f * guardTurnWeight, 5f * step) * guardLeftThigh.rotation;
        guardRightThigh.rotation = Quaternion.Euler(-15f * step, 6f * guardTurnWeight, 5f * step) * guardRightThigh.rotation;
        guardLeftKnee.rotation = Quaternion.AngleAxis(17f * Mathf.Max(0f, step), Vector3.right) * guardLeftKnee.rotation;
        guardRightKnee.rotation = Quaternion.AngleAxis(17f * Mathf.Max(0f, -step), Vector3.right) * guardRightKnee.rotation;
    }

    public void BeginBlend(float duration, float elapsed)
    {
        // index（当前元素索引）：使用位置：KnifePoseSampler.BeginBlend(float, float)。
for (int index = 0; index < targets.Length; index++)
        {
            // 起点必须是上一帧真正显示的姿态，不能读取本帧已被动画器覆盖的骨架。
            if (hasPresentedPose)
            {
                blendPositions[index] = presentedPositions[index];
                blendRotations[index] = presentedRotations[index];
            }
            else
            {
                blendPositions[index] = targets[index].localPosition;
                blendRotations[index] = targets[index].localRotation;
            }
        }

        blendDuration = Mathf.Max(.001f, duration);
        blendElapsed = Mathf.Max(0, elapsed);
        lastBlendTime = Time.timeAsDouble;
        hasBlend = true;
    }

    /// <summary>Apply 的业务处理入口；本方法直接依赖 KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="deltaTime">中文含义：本帧时间间隔。使用位置：作为 KnifePoseSampler.AdvanceBlend(float) 的参数。</param>
    public void Apply(float deltaTime, float lowerBodySettle = 0f, float poseWeight = 1f, bool rightArmOnly = false)
    {
        CaptureUnderlyingPose();
        // weight（权重）：使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifePoseSampler.Apply(float)。
float weight = 1f;
        if (hasBlend)
        {
            AdvanceBlend(deltaTime);
            weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendElapsed / blendDuration));
            if (weight >= 1f)
            {
                hasBlend = false;
            }
        }

        // index（当前元素索引）：使用位置：KnifePoseSampler.Apply(float)。
for (int index = 0; index < targets.Length; index++)
        {
            if (rightArmOnly && !rightArm[index])
            {
                continue;
            }
            Vector3 position = bones[index].localPosition;
            Quaternion rotation = bones[index].localRotation;
            if (lowerBodySettle > 0f && lowerBody[index])
            {
                position = Vector3.Lerp(position, underlyingPositions[index], lowerBodySettle);
                rotation = Quaternion.Slerp(rotation, underlyingRotations[index], lowerBodySettle);
            }

            Vector3 blendedPosition = Vector3.Lerp(blendPositions[index], position, weight);
            Quaternion blendedRotation = Quaternion.Slerp(blendRotations[index], rotation, weight);
            targets[index].localPosition = Vector3.Lerp(underlyingPositions[index], blendedPosition, poseWeight);
            targets[index].localRotation = Quaternion.Slerp(underlyingRotations[index], blendedRotation, poseWeight);
        }

        CapturePresentedPose();
    }

    /// <summary>ApplyExit 的业务处理入口；本方法直接依赖 KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.ReleasePresentation()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.ReleasePresentation()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="deltaTime">中文含义：本帧时间间隔。使用位置：作为 KnifePoseSampler.AdvanceBlend(float) 的参数。</param>
    public bool ApplyExit(float deltaTime)
    {
        CaptureUnderlyingPose();
        AdvanceBlend(deltaTime);
        // weight（权重）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifePoseSampler.ApplyExit(float)。
float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendElapsed / blendDuration));
        // index（当前元素索引）：使用位置：KnifePoseSampler.ApplyExit(float)。
for (int index = 0; index < targets.Length; index++)
        {
            targets[index].localPosition = Vector3.Lerp(blendPositions[index], targets[index].localPosition, weight);
            targets[index].localRotation = Quaternion.Slerp(blendRotations[index], targets[index].localRotation, weight);
        }

        CapturePresentedPose();
        if (weight >= 1f)
        {
            ReleasePresentation();
        }

        return weight < 1f;
    }

    /// <summary>CaptureUnderlyingPose 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.Apply(float)、KnifePoseSampler.ApplyExit(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CaptureUnderlyingPose()
    {
        // index（当前元素索引）：使用位置：KnifePoseSampler.CaptureUnderlyingPose()。
for (int index = 0; index < targets.Length; index++)
        {
            underlyingPositions[index] = targets[index].localPosition;
            underlyingRotations[index] = targets[index].localRotation;
        }

        hasUnderlyingPose = true;
    }

    /// <summary>CapturePresentedPose 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.Apply(float)、KnifePoseSampler.ApplyExit(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CapturePresentedPose()
    {
        // index（当前元素索引）：使用位置：KnifePoseSampler.CapturePresentedPose()。
for (int index = 0; index < targets.Length; index++)
        {
            presentedPositions[index] = targets[index].localPosition;
            presentedRotations[index] = targets[index].localRotation;
        }

        hasPresentedPose = true;
    }

    /// <summary>RestoreUnderlyingPose 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.Dispose()、LeonAnimatorDriver.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RestoreUnderlyingPose()
    {
        if (!hasUnderlyingPose)
        {
            return;
        }

        // 在下一次动画求值之前撤回直接写骨，防止没有曲线的骨骼留下小刀姿态。
        // index（当前元素索引）：使用位置：KnifePoseSampler.RestoreUnderlyingPose()。
for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index] != null)
            {
                // 固定步 Animator 已经写入新姿态时，保留它的结果。
                if (targets[index].localPosition.Equals(presentedPositions[index]))
                {
                    targets[index].localPosition = underlyingPositions[index];
                }
                if (targets[index].localRotation.Equals(presentedRotations[index]))
                {
                    targets[index].localRotation = underlyingRotations[index];
                }
            }
        }

        hasUnderlyingPose = false;
    }

    /// <summary>ReleasePresentation 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.ApplyExit(float)、LeonAnimatorDriver.LateUpdate()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReleasePresentation()
    {
        hasPresentedPose = false;
        hasUnderlyingPose = false;
        hasBlend = false;
    }

    /// <summary>AdvanceBlend 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePoseSampler.Apply(float)、KnifePoseSampler.ApplyExit(float)；下游（含内部回调体） Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="deltaTime">中文含义：本帧时间间隔。使用位置：KnifePoseSampler.AdvanceBlend(float)。</param>
    private void AdvanceBlend(float deltaTime)
    {
        // time（时间）：使用位置：作为 Mathf.Max(float, float) 的参数、KnifePoseSampler.AdvanceBlend(float)。
double time = Time.timeAsDouble;
        if (deltaTime > 0)
        {
            blendElapsed += Mathf.Max(0, (float)(time - lastBlendTime));
        }

        lastBlendTime = time;
    }

    /// <summary>Dispose 的业务处理入口；本方法直接依赖 KnifePoseSampler.RestoreUnderlyingPose()、Object.Destroy(Object)、Object.DestroyImmediate(Object)、PlayableGraph.Destroy()、PlayableGraph.IsValid()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonAnimatorDriver.OnDisable()；下游（含内部回调体） KnifePoseSampler.RestoreUnderlyingPose()、Object.Destroy(Object)、Object.DestroyImmediate(Object)、PlayableGraph.Destroy()、PlayableGraph.IsValid()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Dispose()
    {
        RestoreUnderlyingPose();
        if (graph.IsValid())
        {
            graph.Destroy();
        }

        if (model != null)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(model);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(model);
            }
        }
    }
}
