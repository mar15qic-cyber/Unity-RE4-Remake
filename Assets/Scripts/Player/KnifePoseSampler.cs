using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>按已验收预览的流程求值完整姿态，由正式动画驱动器提交到角色。</summary>
public sealed class KnifePoseSampler : IDisposable
{
    /// <summary>模型。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly GameObject model;
    /// <summary>动画控制器。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Animator animator;
    /// <summary>配置。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly KnifeCombatConfiguration configuration;
    /// <summary>骨骼集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Transform[] bones;
    /// <summary>目标列表。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Transform[] targets;
    /// <summary>上部骨骼集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Transform[] upperBones;
    private readonly bool[] lowerBody;
    private readonly bool[] rightArm;
    private readonly bool[] guardStance;
    /// <summary>脊柱。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Transform spine;
    private readonly Transform motionRoot;
    private readonly Transform leftFoot;
    private readonly Transform rightFoot;
    private readonly Transform targetRoot;
    private readonly Transform targetLeftFoot;
    private readonly Transform targetRightFoot;
    internal const float GuardTurnSettleDuration = .32f;
    private Pose guardTurnLeftPose;
    private Pose guardTurnRightPose;
    private bool guardTurnLeftFirst;
    private float guardTurnSettleTime = GuardTurnSettleDuration;
    /// <summary>上部位置集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Vector3[] upperPositions;
    /// <summary>上部旋转集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Quaternion[] upperRotations;
    /// <summary>混合位置集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Vector3[] blendPositions;
    /// <summary>混合旋转集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Quaternion[] blendRotations;
    /// <summary>已显示位置集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Vector3[] presentedPositions;
    /// <summary>已显示旋转集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Quaternion[] presentedRotations;
    /// <summary>底层位置集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Vector3[] underlyingPositions;
    /// <summary>底层旋转集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Quaternion[] underlyingRotations;
    /// <summary>手指骨骼集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<string, Transform> fingerBones = new Dictionary<string, Transform>();
    /// <summary>导航图。</summary>
    private PlayableGraph graph;
    /// <summary>图层集合。</summary>
    private AnimationLayerMixerPlayable layers;
    /// <summary>主要可播放。</summary>
    private AnimationClipPlayable mainPlayable;
    /// <summary>行走可播放。</summary>
    private AnimationClipPlayable walkPlayable;
    /// <summary>待机可播放。</summary>
    private AnimationClipPlayable idlePlayable;
    /// <summary>移动行为混音器。</summary>
    private AnimationMixerPlayable locomotionMixer;
    /// <summary>当前动画片段。</summary>
    private AnimationClip currentClip;
    /// <summary>当前行走。</summary>
    private AnimationClip currentWalk;
    /// <summary>混合时长。</summary>
    private float blendDuration;
    /// <summary>混合已用。</summary>
    private float blendElapsed;
    /// <summary>上一次混合时间。</summary>
    private double lastBlendTime;
    /// <summary>是否具有混合。</summary>
    private bool hasBlend;
    /// <summary>是否具有已显示姿态。</summary>
    private bool hasPresentedPose;
    /// <summary>是否具有底层姿态。</summary>
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
    /// <param name="configuration">配置。</param>
    /// <param name="target">目标动画控制器。</param>
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
        // 髋部。
Transform hips = RequireBone(HumanBodyBones.Hips);
        bones = hips.GetComponentsInChildren<Transform>(true);
        targets = new Transform[bones.Length];
        blendPositions = new Vector3[bones.Length];
        blendRotations = new Quaternion[bones.Length];
        presentedPositions = new Vector3[bones.Length];
        presentedRotations = new Quaternion[bones.Length];
        underlyingPositions = new Vector3[bones.Length];
        underlyingRotations = new Quaternion[bones.Length];
        // 当前元素索引。
for (int index = 0; index < bones.Length; index++)
        {
            // 路径。
string path = GetPath(bones[index], model.transform);
            targets[index] = target.transform.Find(path);
            if (targets[index] == null)
            {
                throw new InvalidOperationException("正式角色缺少预览骨骼：" + path);
            }
        }

        spine = RequireBone(HumanBodyBones.Spine);
        motionRoot = hips.parent;
        leftFoot = RequireBone(HumanBodyBones.LeftFoot);
        rightFoot = RequireBone(HumanBodyBones.RightFoot);
        targetRoot = target.transform;
        targetLeftFoot = targets[Array.IndexOf(bones, leftFoot)];
        targetRightFoot = targets[Array.IndexOf(bones, rightFoot)];
        upperBones = spine.GetComponentsInChildren<Transform>(true);
        lowerBody = new bool[bones.Length];
        rightArm = new bool[bones.Length];
        guardStance = new bool[bones.Length];
        Transform leftArm = RequireBone(HumanBodyBones.LeftUpperArm);
        Transform upperArm = RequireBone(HumanBodyBones.RightUpperArm);
        for (int index = 0; index < bones.Length; index++)
        {
            lowerBody[index] = bones[index] != spine && !bones[index].IsChildOf(spine);
            bool sharedSpine = leftArm.IsChildOf(bones[index]) && upperArm.IsChildOf(bones[index]);
            guardStance[index] = lowerBody[index] || sharedSpine || bones[index] == leftArm.parent || bones[index] == upperArm.parent;
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
        // 输出。
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
    /// <param name="bone">骨骼。</param>
    /// <param name="root">根变换节点。</param>
    private static string GetPath(Transform bone, Transform root)
    {
        // 路径。
string path = bone.name;
        // 父级。
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
    /// <param name="clip">动画片段。</param>
    private AnimationClipPlayable CreateClip(AnimationClip clip)
    {
        // 可播放。
AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetSpeed(0);
        return playable;
    }

    /// <summary>Sample 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationMixerPlayable.Create(PlayableGraph, int)、AnimationMixerPlayable.IsValid&lt;AnimationMixerPlayable&gt;()、AnimationMixerPlayable.SetInputWeight&lt;AnimationMixerPlayable&gt;(int, float) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewReferenceValidator.CompareSourcePreview(bool)、LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、AnimationClipPlayable.IsValid&lt;AnimationClipPlayable&gt;()、AnimationClipPlayable.SetTime&lt;AnimationClipPlayable&gt;(double)、AnimationLayerMixerPlayable.SetInputWeight&lt;AnimationLayerMixerPlayable&gt;(int, float)、AnimationMixerPlayable.Create(PlayableGraph, int)、AnimationMixerPlayable.IsValid&lt;AnimationMixerPlayable&gt;()、AnimationMixerPlayable.SetInputWeight&lt;AnimationMixerPlayable&gt;(int, float) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">动画片段。</param>
    /// <param name="actionTime">动作时间。</param>
    /// <param name="movementMode">移动模式。</param>
    /// <param name="locomotionTime">移动行为时间。</param>
    /// <param name="movementWeight">移动权重。</param>
    /// <param name="originalGrip">是否原始握持。</param>
    public void Sample(AnimationClip clip, float actionTime, int movementMode, float locomotionTime,
        float movementWeight = 1f, bool originalGrip = false, AnimationClip guardTurn = null, float guardTorsoLead = 0f)
    {
        // 行走。
 AnimationClip walk = guardTurn;
        if (walk == null && movementMode > 0 && movementMode <= configuration.previewLocomotion.Length)
        {
            walk = configuration.previewLocomotion[movementMode - 1];
        }

        if (currentClip != clip || currentWalk != walk)
        {
            if (mainPlayable.IsValid())
            {
                // 输入数据。
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
            // 权重。
float weight = Mathf.Clamp01(movementWeight);
            if (movementMode == 5)
            {
                // 阶段。
float phase = Mathf.Repeat(locomotionTime, 3f);
                weight *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .35f));
                weight *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - 1.8f) / .35f));
            }

            locomotionMixer.SetInputWeight(0, 1f - weight);
            locomotionMixer.SetInputWeight(1, weight);
            // 保留完整源动作的上身朝向，步态只决定骨盆和下身。
            clip.SampleAnimation(model, Mathf.Clamp(actionTime, 0f, clip.length));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Vector3 guardRootPosition = motionRoot.localPosition;
            Quaternion guardRootRotation = motionRoot.localRotation;
            // 脊柱旋转。
Quaternion spineRotation = spine.rotation;
            // 当前元素索引。
for (int index = 0; index < upperBones.Length; index++)
            {
                upperPositions[index] = upperBones[index].localPosition;
                upperRotations[index] = upperBones[index].localRotation;
            }

            graph.Evaluate(0f);
            graph.Evaluate(0f);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (guardTurn != null)
            {
                // 刚体已跟随镜头转向。保持采样根骨中立，避免转身片段再转一次。
                motionRoot.SetLocalPositionAndRotation(guardRootPosition, guardRootRotation);
            }
            // 当前元素索引。
for (int index = 0; index < upperBones.Length; index++)
            {
                upperBones[index].localPosition = upperPositions[index];
                upperBones[index].localRotation = upperRotations[index];
            }

            spine.rotation = Quaternion.AngleAxis(guardTorsoLead, Vector3.up) * spineRotation;
            if (clip == configuration.previewIdle && clip.humanMotion)
            {
                // 左脚。
Transform leftFoot = RequireBone(HumanBodyBones.LeftFoot);
                // 右脚。
Transform rightFoot = RequireBone(HumanBodyBones.RightFoot);
                // 步幅。
float stride = Mathf.Clamp((leftFoot.position.z - rightFoot.position.z) / .65f, -1f, 1f) * weight;
                // 左侧手臂。
Transform leftArm = RequireBone(HumanBodyBones.LeftUpperArm);
                // 右侧手臂。
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
            // 当前元素索引。
for (int index = 0; index < configuration.fingerBoneName.Length; index++)
            {
                // 手指。
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
    /// <param name="duration">持续时间。</param>
    /// <param name="elapsed">已用。</param>
    internal AnimationClip LocomotionClip => currentWalk;

    internal float ReadLeftFootHeightDifference()
    {
        return leftFoot.position.y - rightFoot.position.y;
    }

    public void BeginBlend(float duration, float elapsed)
    {
        // 当前元素索引。
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
    /// <param name="deltaTime">本帧时间间隔。</param>
    public void Apply(float deltaTime, float lowerBodySettle = 0f, float poseWeight = 1f, bool rightArmOnly = false,
        float standingGuardRelease = -1f, bool guardTurning = false, bool guardStanding = false)
    {
        CaptureUnderlyingPose();
        // 权重。
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

        // 当前元素索引。
for (int index = 0; index < targets.Length; index++)
        {
            if (rightArmOnly && !rightArm[index])
            {
                continue;
            }
            if (standingGuardRelease >= 0f && guardStance[index])
            {
                // 原地松键时逐帧追随正在播放的待机，骨盆和双腿不会再回弹到按键前的旧姿态。
                targets[index].SetLocalPositionAndRotation(
                    Vector3.Lerp(blendPositions[index], underlyingPositions[index], standingGuardRelease),
                    Quaternion.Slerp(blendRotations[index], underlyingRotations[index], standingGuardRelease));
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

        if (guardTurning)
        {
            guardTurnLeftPose = new Pose(targetLeftFoot.position, targetLeftFoot.rotation);
            guardTurnRightPose = new Pose(targetRightFoot.position, targetRightFoot.rotation);
            guardTurnSettleTime = GuardTurnSettleDuration;
        }
        else if (guardStanding && guardTurnSettleTime < GuardTurnSettleDuration)
        {
            guardTurnSettleTime = Mathf.Min(guardTurnSettleTime + deltaTime, GuardTurnSettleDuration);
            float phase = guardTurnSettleTime * 2f / GuardTurnSettleDuration;
            float delay = 0f;
            if (!guardTurnLeftFirst)
            {
                delay = 1f;
            }
            SettleGuardFoot(leftFoot, targetLeftFoot, guardTurnLeftPose, Mathf.Clamp01(phase - delay));
            SettleGuardFoot(rightFoot, targetRightFoot, guardTurnRightPose, Mathf.Clamp01(phase - (1f - delay)));
        }
        else if (!guardStanding)
        {
            guardTurnSettleTime = GuardTurnSettleDuration;
        }

        CapturePresentedPose();
    }

    internal void BeginGuardTurnSettle()
    {
        // 较低的脚先留在原地承重，另一脚先迈回站姿。
        guardTurnLeftFirst = guardTurnLeftPose.position.y > guardTurnRightPose.position.y;
        guardTurnSettleTime = 0f;
        BeginBlend(GuardTurnSettleDuration, 0f);
    }

    private void SettleGuardFoot(Transform source, Transform foot, Pose from, float phase)
    {
        Vector3 end = targetRoot.TransformPoint(model.transform.InverseTransformPoint(source.position));
        Quaternion endRotation = targetRoot.rotation * Quaternion.Inverse(model.transform.rotation) * source.rotation;
        float travel = Mathf.SmoothStep(0f, 1f, phase);
        Vector3 goal = Vector3.Lerp(from.position, end, travel);
        float distance = Vector3.ProjectOnPlane(end - from.position, Vector3.up).magnitude;
        float lift = Mathf.Min(.075f, distance * .29f);
        goal += Vector3.up * (4f * travel * (1f - travel) * lift);

        Transform knee = foot.parent;
        Transform thigh = knee.parent;
        Vector3 upper = knee.position - thigh.position;
        Vector3 lower = foot.position - knee.position;
        Vector3 bendNormal = Vector3.Cross(upper, lower);
        if (bendNormal.sqrMagnitude < .000001f)
        {
            bendNormal = targetRoot.right;
        }
        float reach = upper.magnitude + lower.magnitude - .002f;
        goal = thigh.position + Vector3.ClampMagnitude(goal - thigh.position, reach);
        RootMotion.FinalIK.IKSolverTrigonometric.Solve(thigh, knee, foot, goal, bendNormal.normalized, 1f);
        foot.rotation = Quaternion.Slerp(from.rotation, endRotation, travel);
    }

    /// <summary>ApplyExit 的业务处理入口；本方法直接依赖 KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.ReleasePresentation()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） KnifePoseSampler.AdvanceBlend(float)、KnifePoseSampler.CapturePresentedPose()、KnifePoseSampler.CaptureUnderlyingPose()、KnifePoseSampler.ReleasePresentation()、Mathf.Clamp01(float)、Mathf.SmoothStep(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="deltaTime">本帧时间间隔。</param>
    public bool ApplyExit(float deltaTime)
    {
        CaptureUnderlyingPose();
        AdvanceBlend(deltaTime);
        // 权重。
float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendElapsed / blendDuration));
        // 当前元素索引。
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
        // 当前元素索引。
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
        // 当前元素索引。
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
        // 当前元素索引。
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
    /// <param name="deltaTime">本帧时间间隔。</param>
    private void AdvanceBlend(float deltaTime)
    {
        // 时间。
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
