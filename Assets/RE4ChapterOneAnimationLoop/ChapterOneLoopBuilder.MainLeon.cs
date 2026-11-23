#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public const string MainLeonController = Generated + "/MainLeon.controller";
        public const string MainLeonQte = Generated + "/MainLeonQTE.asset";
        private const string LegacyMainLeonController = "Assets/Animations/小刀战斗/里昂小刀战斗.controller";
        private const string LegacyMainLeonQte = "Assets/Settings/QTE/里昂正面抓取配置.asset";

        public static string RefreshMainLeonController()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式生成主场景动画控制器。");
            }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/GameScene.unity");
            try
            {
                var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
                var animator = player.GetComponent<Animator>();
                var original = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyMainLeonController);
                string current = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                if (current != LegacyMainLeonController && current != MainLeonController)
                {
                    throw new InvalidOperationException("主场景里昂的控制器已变更。");
                }
                var qte = player.GetComponent<LeonQTECoordinator>().config;
                if (AssetDatabase.GetAssetPath(qte) == MainLeonQte)
                {
                    qte = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(LegacyMainLeonQte);
                }
                if (!animator.isHuman)
                {
                    animator.avatar = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPath).GetComponent<Animator>().avatar;
                }
                if (!animator.isHuman)
                {
                    throw new InvalidOperationException("无法恢复人形采样 Avatar。");
                }
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var compiler = new MainLeonGraph(original, animator);
                return compiler.Build(qte);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }


        public static string RefreshMainLeonAcceptanceMotions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请退出播放后更新正式动作。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            ApplyMainLeonAcceptanceMotions(graph, clips);
            AssetDatabase.SaveAssetIfDirty(graph);
            return "已更新主场景转身、原版踹击和手枪举枪。";
        }

        private static void ApplyMainLeonAcceptanceMotions(AnimatorController graph, Dictionary<string, AnimationClip> clips)
        {
            var baseMachine = graph.layers[0].stateMachine;
            var baseStates = baseMachine.states.ToDictionary(child => child.state.name, child => child.state);
            var turnTrees = AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<BlendTree>();
            foreach (var tree in turnTrees)
            {
                string prefix;
                Motion idle;
                switch (tree.name)
                {
                    case "PistolTurnSelector":
                    case "KnifeTurnSelector":
                        prefix = "LeonMove_";
                        idle = baseStates[LeonAnimatorDriver.NativeIdleName].motion;
                        break;
                    case "RifleTurnSelector":
                        prefix = "LeonMove_";
                        idle = baseStates[LeonAnimatorDriver.NativeIdleName].motion;
                        if (baseStates.TryGetValue(LeonAnimatorDriver.NativeCqbrIdleName, out var rifleIdle))
                        {
                            idle = rifleIdle.motion;
                        }
                        break;
                    case "HurtTurnSelector":
                        prefix = "LeonHurt_";
                        idle = baseStates[LeonAnimatorDriver.NativeHurtIdleName].motion;
                        break;
                    default:
                        continue;
                }

                string turn180Prefix = prefix;
                if (prefix == "LeonMove_")
                {
                    turn180Prefix = "LeonIdleTurn_";
                }
                var children = tree.children.ToList();
                if (!children.Any(child => child.threshold == 0f))
                {
                    children.Add(new ChildMotion { threshold = 0f, timeScale = 1f, directBlendParameter = "Blend" });
                }

                for (int index = 0; index < children.Count; index++)
                {
                    var child = children[index];
                    switch (child.threshold)
                    {
                        case -180f:
                            child.motion = clips[turn180Prefix + "0170"];
                            break;
                        case -90f:
                            child.motion = clips[prefix + "0174"];
                            break;
                        case 0f:
                            child.motion = idle;
                            break;
                        case 90f:
                            child.motion = clips[prefix + "0175"];
                            break;
                        case 180f:
                            child.motion = clips[turn180Prefix + "0171"];
                            break;
                    }
                    children[index] = child;
                }
                tree.children = children.OrderBy(child => child.threshold).ToArray();
                EditorUtility.SetDirty(tree);
            }

            ApplyMainLeonMelee(graph, clips);

            var pistolAim = graph.layers[LeonAnimatorDriver.LayerRifleCrouch].stateMachine.states;
            foreach (var child in pistolAim)
            {
                if (child.state.name == LeonAnimatorDriver.PistolAimStateName(PistolAimPose.Raise)
                    || child.state.name == LeonAnimatorDriver.PistolAimStateName(PistolAimPose.ReverseRaise))
                {
                    child.state.speed = MainPistolRaiseSpeed;
                    EditorUtility.SetDirty(child.state);
                }
            }

            EditorUtility.SetDirty(graph);
        }

        public static void RefreshMainLeonWoodenBoxKick()
        {
            RefreshMainLeonMelee();
        }

        public static string RefreshMainLeonKnifePresentation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请退出播放后更新正式持刀姿态。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            ApplyMainLeonKnifePresentation(graph, clips);
            AssetDatabase.SaveAssetIfDirty(graph);
            return "已接入原版低位持刀和收刀动作。";
        }

        private static void ApplyMainLeonKnifePresentation(AnimatorController graph, Dictionary<string, AnimationClip> clips)
        {
            var lower = clips["Knife_0158"];
            var sheath = clips["Knife_0551"];
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<AnimationClip>()
                .ToDictionary(clip => clip.name);
            var layers = graph.layers;
            var carryMachine = layers[LeonAnimatorDriver.LayerRifleCrouch].stateMachine;
            var carry = carryMachine.states.Select(child => child.state)
                .SingleOrDefault(state => state.name == LeonAnimatorDriver.NativeKnifeCarryStateName);
            if (carry == null)
            {
                carry = carryMachine.AddState(LeonAnimatorDriver.NativeKnifeCarryStateName);
                carry.writeDefaultValues = false;
            }

            carry.motion = StoreUpperMotion(LeonAnimatorDriver.NativeKnifeCarryStateName, lower, true);
            var hide = layers[LeonAnimatorDriver.LayerWeaponAction].stateMachine.states.Select(child => child.state)
                .Single(state => state.name == "KnifeHide");
            hide.motion = StoreUpperMotion("MainNativeKnifeHide", sheath, false);
            var hideTiming = hide.behaviours.OfType<WeaponActionSMB>().Single();
            hideTiming.completeNormalizedTime = 0.999f;
            EditorUtility.SetDirty(hideTiming);
            ApplyMainLeonKnifeGait(graph, lower);
            EditorUtility.SetDirty(carry);
            EditorUtility.SetDirty(carryMachine);
            EditorUtility.SetDirty(hide);
            EditorUtility.SetDirty(graph);

            AnimationClip StoreUpperMotion(string name, AnimationClip source, bool stationary)
            {
                var bindings = PistolAimBindings(source);
                var curves = new AnimationCurve[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    var curve = AnimationUtility.GetEditorCurve(source, binding);
                    if (stationary)
                    {
                        curves[i] = AnimationCurve.Constant(0f, 1f, curve.Evaluate(source.length));
                        continue;
                    }

                    var carryCurve = AnimationUtility.GetEditorCurve(lower, binding);
                    if (carryCurve == null)
                    {
                        curves[i] = curve;
                        continue;
                    }

                    float held = carryCurve.Evaluate(lower.length);
                    if (binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    {
                        var component = binding;
                        float dot = 0f;
                        foreach (char axis in "xyzw")
                        {
                            component.propertyName = "m_LocalRotation." + axis;
                            dot += AnimationUtility.GetEditorCurve(lower, component).Evaluate(lower.length)
                                * AnimationUtility.GetEditorCurve(source, component).Evaluate(0f);
                        }
                        if (dot < 0f)
                        {
                            held = -held;
                        }
                    }
                    int sourceFrames = Mathf.RoundToInt(source.length * source.frameRate);
                    int frames = LeonAnimatorDriver.NativeKnifeHideFrames;
                    var keys = new Keyframe[frames + 1];
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float time = frame / source.frameRate;
                        float weight = Mathf.SmoothStep(0f, 1f, frame / 6f);
                        // 第 36 帧解除持械姿态锁，随后用现有 0.08 秒过渡接到换武器解锁点。
                        weight *= Mathf.SmoothStep(1f, 0f, (frame - sourceFrames) / (frames - (float)sourceFrames));
                        keys[frame] = new Keyframe(time, Mathf.Lerp(held, curve.Evaluate(Mathf.Min(time, source.length)), weight));
                    }
                    curves[i] = new AnimationCurve(keys);
                }

                var clip = new AnimationClip { name = name, frameRate = source.frameRate };
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                clip.EnsureQuaternionContinuity();
                if (saved.TryGetValue(name, out var existing))
                {
                    EditorUtility.CopySerialized(clip, existing);
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(clip);
                    return existing;
                }

                AssetDatabase.AddObjectToAsset(clip, graph);
                return clip;
            }
        }

        private const string MainLeonKnifeGaitBank = Generated + "/MainLeonKnifeGaitMotions.asset";

        private static void ApplyMainLeonKnifeGait(AnimatorController graph, AnimationClip lowKnife, bool turnsOnly = false,
            bool idleOnly = false)
        {
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainLeonKnifeGaitBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainLeonKnifeGaitBank);
            }
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainLeonKnifeGaitBank).OfType<AnimationClip>()
                .ToDictionary(clip => clip.name);
            var machine = graph.layers[0].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var idle = (AnimationClip)states[LeonAnimatorDriver.NativeIdleName].motion;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var paths = PistolAimBindings(lowKnife).Select(binding => binding.path).Distinct()
                    .Where(path => !path.Contains("/RE4_", StringComparison.Ordinal)).ToArray();
                var joints = paths.Select(path => rig.transform.Find(path)).ToArray();
                idle.SampleAnimation(rig, 0f);
                var idleRotations = joints.Select(joint => joint.localRotation).ToArray();
                lowKnife.SampleAnimation(rig, lowKnife.length);
                var knifeRotations = joints.Select(joint => joint.localRotation).ToArray();
                var offsets = knifeRotations.Select((rotation, index) => Quaternion.Inverse(idleRotations[index]) * rotation).ToArray();
                var gripJoints = paths.Select(path => path.Contains("/arm right wrist/", StringComparison.Ordinal)).ToArray();

                if (!turnsOnly)
                {
                    Bake(LeonAnimatorDriver.NativeIdleName, LeonAnimatorDriver.NativeKnifeIdleName);
                }
                if (!turnsOnly && !idleOnly)
                {
                    foreach (var gait in LeonAnimatorDriver.NativeGaits.Take(3))
                    {
                        foreach (string phase in new[] { "Start", "Loop", "Stop" })
                        {
                            Bake(LeonAnimatorDriver.NativeGaitStateName(gait.name, phase),
                                LeonAnimatorDriver.NativeKnifeStateName(gait.name, phase));
                        }
                    }
                }
                if (!idleOnly)
                {
                    foreach (var turn in LeonAnimatorDriver.BasicRunTurns)
                    {
                        string sourceName = LeonAnimatorDriver.BasicRunTurnStateName("NativePistol", turn.clip);
                        if (states.ContainsKey(sourceName))
                        {
                            Bake(sourceName, LeonAnimatorDriver.BasicRunTurnStateName("NativeKnife", turn.clip));
                        }
                    }
                }
                EditorUtility.SetDirty(machine);
                EditorUtility.SetDirty(bank);
                AssetDatabase.SaveAssetIfDirty(bank);

                void Bake(string sourceState, string name)
                {
                    var original = states[sourceState];
                    var source = (AnimationClip)original.motion;
                    if (turnsOnly && states.ContainsKey(name) && saved.ContainsKey(name))
                    {
                        return;
                    }
                    var clip = Object.Instantiate(source);
                    clip.name = name;
                    var tracks = paths.Select((path, index) => new AimJointTrack(path, joints[index])).ToArray();
                    int frames = Mathf.RoundToInt(source.length * source.frameRate);
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float time = frame / source.frameRate;
                        source.SampleAnimation(rig, time);
                        for (int index = 0; index < tracks.Length; index++)
                        {
                            // 保留原版步态的肩、脊柱和摆臂相位，只转移低位刀姿的偏移及右手握姿。
                            var joint = joints[index];
                            if (gripJoints[index])
                            {
                                joint.localRotation = knifeRotations[index];
                            }
                            else
                            {
                                joint.localRotation *= offsets[index];
                            }
                            tracks[index].Record(time);
                        }
                    }
                    WriteRifleUpperTracks(clip, tracks, source.frameRate, replaceCurves: false);
                    if (source.hasRootCurves && !clip.hasRootCurves)
                    {
                        Object.DestroyImmediate(clip);
                        throw new InvalidOperationException("低位持刀步态缺少原版根轨：" + name);
                    }

                    if (saved.TryGetValue(name, out var existing))
                    {
                        EditorUtility.CopySerialized(clip, existing);
                        Object.DestroyImmediate(clip);
                        clip = existing;
                        EditorUtility.SetDirty(clip);
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(clip, bank);
                    }

                    if (!states.TryGetValue(name, out var state))
                    {
                        var position = machine.states.Single(child => child.state == original).position;
                        state = machine.AddState(name, position + new Vector3(0f, 390f));
                    }
                    state.motion = clip;
                    state.speed = original.speed;
                    state.writeDefaultValues = original.writeDefaultValues;
                    EditorUtility.SetDirty(state);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private const string MainLeonFallbackBank = Generated + "/MainLeonFallback.asset";
        private static readonly HashSet<string> MainLeonLocomotionStates = new(StringComparer.Ordinal)
        {
            "Idle", "WalkStarts", "MovementLoop", "WalkStops", "SprintStart", "SprintLoop",
            "SprintStop", "CrouchMovement", "HurtIdle", "HurtWalk", "HurtJog"
        };

        public static string RefreshMainLeonLocomotionTravel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请退出播放后更新移动片段。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainLeonFallbackBank);
            var locomotion = CollectMainLeonLocomotion(graph);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPath);
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                var animator = sample.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!animator.isHuman)
                {
                    throw new InvalidOperationException("正式里昂缺少人形采样 Avatar。");
                }

                int updated = 0;
                foreach (var pair in bank.ReadSourceLinks())
                {
                    if (locomotion.Contains(pair.Value) && BakeMainLeonLocomotionPelvis(pair.Key, pair.Value, animator))
                    {
                        EditorUtility.SetDirty(pair.Value);
                        updated++;
                    }
                }
                AssetDatabase.SaveAssetIfDirty(bank);
                return $"已分离 {updated} 个兼容步态的骨盆与移动位移。";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static HashSet<AnimationClip> CollectMainLeonLocomotion(AnimatorController graph)
        {
            var clips = new HashSet<AnimationClip>();
            foreach (var layer in graph.layers)
            {
                if (layer.syncedLayerIndex < 0 && layer.stateMachine != null)
                {
                    VisitMachine(layer.stateMachine);
                }
            }
            return clips;

            void VisitMachine(AnimatorStateMachine machine)
            {
                foreach (var child in machine.states)
                {
                    if (MainLeonLocomotionStates.Contains(child.state.name))
                    {
                        VisitMotion(child.state.motion);
                    }
                }
                foreach (var child in machine.stateMachines)
                {
                    VisitMachine(child.stateMachine);
                }
            }

            void VisitMotion(Motion motion)
            {
                if (motion is AnimationClip clip)
                {
                    clips.Add(clip);
                }
                else if (motion is BlendTree tree)
                {
                    foreach (var child in tree.children)
                    {
                        VisitMotion(child.motion);
                    }
                }
            }
        }

        private static bool BakeMainLeonLocomotionPelvis(AnimationClip source, AnimationClip baked, Animator sampler)
        {
            if (!HasTravel("MotionT.x") && !HasTravel("MotionT.z"))
            {
                return false;
            }

            var root = sampler.transform;
            var rig = root.Find("Armature");
            var rootPose = new Pose(root.position, root.rotation);
            var rigPose = new Pose(rig.localPosition, rig.localRotation);
            bool restoreRootMotion = sampler.applyRootMotion;
            try
            {
                // 人形采样必须先提取原始根位移，否则 MotionT 会进入骨盆曲线。
                sampler.applyRootMotion = true;
                sampler.Rebind();
                var hips = sampler.GetBoneTransform(HumanBodyBones.Hips);
                string path = AnimationUtility.CalculateTransformPath(hips, root);
                var translation = CreateKeys(3);
                int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * baked.frameRate));
                for (int frame = 0; frame <= frames; frame++)
                {
                    float time = source.length * (frame / (float)frames);
                    root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    rig.SetLocalPositionAndRotation(rigPose.position, rigPose.rotation);
                    source.SampleAnimation(root.gameObject, time);
                    AddVector(translation, time, hips.localPosition);
                }

                var bindings = new EditorCurveBinding[3];
                var curves = new AnimationCurve[3];
                for (int axis = 0; axis < bindings.Length; axis++)
                {
                    bindings[axis] = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[axis]);
                    curves[axis] = CollapseConstant(new AnimationCurve(translation[axis].ToArray()));
                }
                // 兼容步态的实际移动仍由运动执行器负责。这里只替换表现用的骨盆位置。
                AnimationUtility.SetEditorCurves(baked, bindings, curves);
                return true;
            }
            finally
            {
                sampler.applyRootMotion = restoreRootMotion;
                root.SetPositionAndRotation(rootPose.position, rootPose.rotation);
                rig.SetLocalPositionAndRotation(rigPose.position, rigPose.rotation);
            }

            bool HasTravel(string property)
            {
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                return curve != null && curve.length > 1
                    && curve.keys.Any(key => Mathf.Abs(key.value - curve[0].value) > 0.0001f);
            }
        }

        private sealed class MainLeonGraph
        {
            private readonly AnimatorController source;
            private readonly AnimatorController target;
            private readonly Animator sampler;
            private readonly Transform rig;
            private readonly ChapterOneBakedClips bank;
            private readonly Dictionary<AnimationClip, AnimationClip> links;
            private readonly HashSet<AnimationClip> compiled = new();
            private readonly HashSet<AnimationClip> locomotionSources;
            private readonly HashSet<BlendTree> visited = new();
            private readonly Dictionary<AvatarMask, AvatarMask> masks = new();
            private readonly Dictionary<string, AnimationClip> bankClips;
            private readonly Dictionary<string, AnimationClip> native;
            private readonly Dictionary<string, AnimationClip> migrated = new();

            public MainLeonGraph(AnimatorController source, Animator sampler)
            {
                this.source = source;
                this.sampler = sampler;
                locomotionSources = CollectMainLeonLocomotion(source);
                rig = sampler.transform.Find("Armature");
                if (rig == null)
                {
                    throw new InvalidOperationException("正式里昂缺少 Armature。");
                }

                target = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
                if (target == null)
                {
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), MainLeonController))
                    {
                        throw new InvalidOperationException("无法复制里昂状态机。");
                    }
                    target = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
                }
                bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainLeonFallbackBank);
                if (bank == null)
                {
                    bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                    AssetDatabase.CreateAsset(bank, MainLeonFallbackBank);
                }
                links = bank.ReadSourceLinks();
                bankClips = AssetDatabase.LoadAllAssetsAtPath(MainLeonFallbackBank).OfType<AnimationClip>()
                    .Where(clip => clip.name.StartsWith("MainNative_", StringComparison.Ordinal))
                    .ToDictionary(clip => clip.name);
                native = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                    .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            }

            public string Build(LeonQTEConfig sourceQte)
            {
                var layers = source.layers;
                var output = target.layers;
                if (layers.Length != output.Length && !(output.Length == layers.Length + 1 && output[output.Length - 1].name == "Native Hit Upper"))
                {
                    throw new InvalidOperationException("正式图层数量已变更。");
                }
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int index = 0; index < layers.Length; index++)
                    {
                        var layer = layers[index];
                        var destination = output[index];
                        if (layer.name != destination.name)
                        {
                            throw new InvalidOperationException("正式图层顺序已变更。");
                        }
                        destination.avatarMask = ConvertMask(layer.avatarMask);
                        if (layer.syncedLayerIndex < 0)
                        {
                            CopyMachine(layer.stateMachine, destination.stateMachine);
                            continue;
                        }

                        var originalMachine = layers[layer.syncedLayerIndex].stateMachine;
                        var targetMachine = output[layer.syncedLayerIndex].stateMachine;
                        foreach (var entry in originalMachine.states)
                        {
                            var state = targetMachine.states.Single(child => child.state.name == entry.state.name).state;
                            var motion = layer.GetOverrideMotion(entry.state);
                            if (motion != null)
                            {
                                destination.SetOverrideMotion(state, ConvertMotion(motion, destination.GetOverrideMotion(state)));
                            }
                        }
                    }
                    target.layers = output;
                    AddMainLeonGait(target);
                    if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainCqbrBank) != null)
                    {
                        AddMainCqbrMotions(target);
                    }
                    if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainW870Bank) != null)
                    {
                        AddMainW870ReloadMotions(target);
                    }
                    if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolSwitchBank) != null)
                    {
                        AddMainPistolSwitchMotions(target);
                    }
                    if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolAimBank) != null)
                    {
                        AddMainPistolAimMotions(target);
                    }
                    if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolReloadBank) != null)
                    {
                        AddMainPistolReloadMotions(target);
                    }
                    ApplyMainLeonAcceptanceMotions(target, native);
                    ApplyHitRecovery(target, native, true, true);
                    ApplyMainLeonKnifePresentation(target, native);
                    AddMainLeonBasicRun(target, native);
                    AddMovingPickupStates(target, native);
                    SaveQte(sourceQte);
                    bank.WriteSourceLinks(links);
                    EditorUtility.SetDirty(bank);
                    EditorUtility.SetDirty(target);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
                AssetDatabase.SaveAssetIfDirty(bank);
                AssetDatabase.SaveAssetIfDirty(target);
                var qte = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(MainLeonQte);
                AssetDatabase.SaveAssetIfDirty(qte);
                if (target.animationClips.Any(clip => clip.humanMotion))
                {
                    throw new InvalidOperationException("Generic 图中仍有人形片段。");
                }
                return $"Generic 图 {target.animationClips.Distinct().Count()} 片段，重绑 {compiled.Count}，原版替换 {migrated.Count}。";
            }

            private void CopyMachine(AnimatorStateMachine original, AnimatorStateMachine destination)
            {
                foreach (var child in original.states)
                {
                    var state = destination.states.Single(item => item.state.name == child.state.name).state;
                    state.motion = ConvertMotion(child.state.motion, state.motion);
                    EditorUtility.SetDirty(state);
                }
                foreach (var child in original.stateMachines)
                {
                    var machine = destination.stateMachines.Single(item => item.stateMachine.name == child.stateMachine.name);
                    CopyMachine(child.stateMachine, machine.stateMachine);
                }
            }

            private Motion ConvertMotion(Motion motion, Motion destination)
            {
                if (motion is AnimationClip clip)
                {
                    return ConvertClip(clip);
                }
                if (motion == null)
                {
                    return null;
                }
                if (!(motion is BlendTree sourceTree) || !(destination is BlendTree targetTree)
                    || AssetDatabase.GetAssetPath(targetTree) != MainLeonController)
                {
                    throw new InvalidOperationException("找不到可独立修改的 Blend Tree " + motion.name);
                }
                if (!visited.Add(targetTree))
                {
                    return targetTree;
                }
                var originals = sourceTree.children;
                var previous = targetTree.children;
                var children = new ChildMotion[originals.Length];
                for (int index = 0; index < originals.Length; index++)
                {
                    var child = originals[index];
                    child.motion = ConvertMotion(child.motion, previous[index].motion);
                    children[index] = child;
                }
                targetTree.children = children;
                EditorUtility.SetDirty(targetTree);
                return targetTree;
            }

            private AnimationClip ConvertClip(AnimationClip sourceClip)
            {
                switch (sourceClip.name)
                {
                    case "Pistol_Hit_L_1":
                    case "Rifle_Hit_L_1":
                        return NativeMotion("LeonDamage_0100");
                    case "Pistol_Hit_R_2":
                    case "Rifle_Hit_R_2":
                        return NativeMotion("LeonDamage_0102");
                    case "Pistol_Hit_C_1":
                    case "Rifle_Hit_C_1":
                        return NativeMotion("LeonDamage_0104");
                    case "Rifle_Death_L":
                        return NativeMotion("LeonDamage_0900");
                    default:
                        return ConvertLegacy(sourceClip);
                }
            }

            private AnimationClip NativeMotion(string id)
            {
                var clip = native[id];
                migrated[id] = clip;
                return clip;
            }

            private AnimationClip ConvertLegacy(AnimationClip clip)
            {
                if (clip == null || !clip.humanMotion)
                {
                    return clip;
                }
                if (!compiled.Add(clip))
                {
                    return links[clip];
                }
                var baked = BakeLegacyHumanoid(clip, sampler, rig, clip.name, clip.isLooping, sharedStage: false);
                if (locomotionSources.Contains(clip))
                {
                    BakeMainLeonLocomotionPelvis(clip, baked, sampler);
                }
                CopyAuxiliaryCurves(clip, baked);
                AnimationUtility.SetAnimationEvents(baked, AnimationUtility.GetAnimationEvents(clip));
                links.TryGetValue(clip, out var old);
                var stored = StoreClip(baked, old);
                links[clip] = stored;
                var originalSettings = AnimationUtility.GetAnimationClipSettings(clip);
                var settings = AnimationUtility.GetAnimationClipSettings(stored);
                settings.loopTime = originalSettings.loopTime;
                settings.hasAdditiveReferencePose = originalSettings.hasAdditiveReferencePose;
                settings.additiveReferencePoseTime = originalSettings.additiveReferencePoseTime;
                if (settings.hasAdditiveReferencePose)
                {
                    settings.additiveReferencePoseClip = ConvertLegacy(originalSettings.additiveReferencePoseClip);
                }
                AnimationUtility.SetAnimationClipSettings(stored, settings);
                EditorUtility.SetDirty(stored);
                return stored;
            }

            private static void CopyAuxiliaryCurves(AnimationClip sourceClip, AnimationClip targetClip)
            {
                var existing = AnimationUtility.GetCurveBindings(targetClip).ToHashSet();
                foreach (var binding in AnimationUtility.GetCurveBindings(sourceClip))
                {
                    if (binding.type == typeof(Animator))
                    {
                        if (binding.propertyName is not ("IKSourceRight" or "IKSourceLeft" or "IKTargetRight" or "IKTargetLeft"))
                        {
                            continue;
                        }
                    }
                    if (!existing.Contains(binding))
                    {
                        AnimationUtility.SetEditorCurve(targetClip, binding, AnimationUtility.GetEditorCurve(sourceClip, binding));
                    }
                }
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(sourceClip))
                {
                    AnimationUtility.SetObjectReferenceCurve(targetClip, binding, AnimationUtility.GetObjectReferenceCurve(sourceClip, binding));
                }
            }

            private AnimationClip StoreClip(AnimationClip clip, AnimationClip previous)
            {
                if (previous == null)
                {
                    AssetDatabase.AddObjectToAsset(clip, bank);
                    return clip;
                }
                EditorUtility.CopySerialized(clip, previous);
                EditorUtility.SetDirty(previous);
                Object.DestroyImmediate(clip);
                return previous;
            }

            private AvatarMask ConvertMask(AvatarMask original)
            {
                if (original == null || original.transformCount > 0)
                {
                    return original;
                }
                if (masks.TryGetValue(original, out var saved))
                {
                    return saved;
                }
                var mask = new AvatarMask { name = original.name + "_Generic" };
                mask.AddTransformPath(sampler.transform, true);
                for (int index = 0; index < mask.transformCount; index++)
                {
                    string path = mask.GetTransformPath(index);
                    mask.SetTransformActive(index, original.GetHumanoidBodyPartActive(BodyRegion(path)));
                }
                saved = AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<AvatarMask>()
                    .SingleOrDefault(item => item.name == mask.name);
                if (saved == null)
                {
                    AssetDatabase.AddObjectToAsset(mask, target);
                    saved = mask;
                }
                else
                {
                    EditorUtility.CopySerialized(mask, saved);
                    EditorUtility.SetDirty(saved);
                    Object.DestroyImmediate(mask);
                }
                masks.Add(original, saved);
                return saved;
            }

            private static AvatarMaskBodyPart BodyRegion(string path)
            {
                if (path.Contains("/arm left", StringComparison.Ordinal))
                {
                    if (path.Contains("/arm left finger", StringComparison.Ordinal))
                    {
                        return AvatarMaskBodyPart.LeftFingers;
                    }
                    return AvatarMaskBodyPart.LeftArm;
                }
                if (path.Contains("/arm right", StringComparison.Ordinal))
                {
                    if (path.Contains("/arm right finger", StringComparison.Ordinal))
                    {
                        return AvatarMaskBodyPart.RightFingers;
                    }
                    return AvatarMaskBodyPart.RightArm;
                }
                if (path.Contains("/leg left", StringComparison.Ordinal))
                {
                    return AvatarMaskBodyPart.LeftLeg;
                }
                if (path.Contains("/leg right", StringComparison.Ordinal))
                {
                    return AvatarMaskBodyPart.RightLeg;
                }
                if (path.Contains("/head", StringComparison.Ordinal))
                {
                    return AvatarMaskBodyPart.Head;
                }
                if (path.StartsWith("Armature/root ground/root hips", StringComparison.Ordinal))
                {
                    return AvatarMaskBodyPart.Body;
                }
                return AvatarMaskBodyPart.Root;
            }

            private void SaveQte(LeonQTEConfig original)
            {
                var config = Object.Instantiate(original);
                config.name = "MainLeonQTE";
                foreach (var variant in config.variants)
                {
                    if (variant == null)
                    {
                        continue;
                    }
                    variant.playerEnter = ConvertLegacy(variant.playerEnter);
                    variant.playerHold = ConvertLegacy(variant.playerHold);
                    variant.playerSuccess = ConvertLegacy(variant.playerSuccess);
                    variant.playerFailure = ConvertLegacy(variant.playerFailure);
                }
                var saved = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(MainLeonQte);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(config, MainLeonQte);
                }
                else
                {
                    config.nativeCameras = saved.nativeCameras;
                    config.useNativeFrontGrab = saved.useNativeFrontGrab;
                    config.useNativeAssassination = saved.useNativeAssassination;
                    EditorUtility.CopySerialized(config, saved);
                    EditorUtility.SetDirty(saved);
                    Object.DestroyImmediate(config);
                }
            }
        }

        private const string MainLeonHurtEdgeBank = Generated + "/Clips/LeonHurtEdges_Clips.asset";

        public static int RefreshMainLeonHurtEdges()
        {
            const string file = "LeonHurtEdges";
            ImportAdditionalArt(file);
            Clips.Remove(file);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainLeonHurtEdgeBank);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(library, MainLeonHurtEdgeBank);
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(MainLeonHurtEdgeBank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            int count = 0;
            foreach (string id in new[] { "LeonHurt_0400", "LeonHurt_0406" })
            {
                var source = Find(file, id);
                if (source == null)
                {
                    throw new InvalidOperationException("找不到原版伤势动作 " + id);
                }
                Bake(source, rig, "Leon", "Leon", false, Array.Empty<int>(), library, clips, out _, id);
                count++;
            }
            AssetDatabase.SaveAssetIfDirty(library);
            return count;
        }

        public static int RefreshMainLeonGait()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新主场景步态。");
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            int count = AddMainLeonGait(controller);
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            ApplyMainLeonKnifeGait(controller, source["Knife_0158"], turnsOnly: true);
            AddMainLeonBasicRun(controller, source);
            AssetDatabase.SaveAssetIfDirty(controller);
            return count;
        }

        public static string RefreshMainLeonStandingIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新站姿待机。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var idle = graph.layers[0].stateMachine.states.Single(entry => entry.state.name == LeonAnimatorDriver.NativeIdleName).state;
            idle.motion = ChapterOneGaitCycles.RefreshLeonStandingIdle();
            EditorUtility.SetDirty(idle);

            var lowKnife = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Single(clip => clip.name == "Knife_0158");
            ApplyMainLeonKnifeGait(graph, lowKnife, idleOnly: true);
            AddMainCqbrMotions(graph, idleOnly: true);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
            return $"已更新普通站姿待机及持刀、CQBR 的派生片段，循环时长 {idle.motion.averageDuration:F2} 秒。";
        }

        private static int AddMainLeonGait(AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainLeonHurtEdgeBank) == null)
            {
                RefreshMainLeonHurtEdges();
            }
            var machine = controller.layers[0].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Concat(AssetDatabase.LoadAllAssetsAtPath(Generated + "/GaitCycles/Leon.asset")
                    .OfType<AnimationClip>()).Concat(AssetDatabase.LoadAllAssetsAtPath(MainLeonHurtEdgeBank)
                    .OfType<AnimationClip>()).ToDictionary(clip => clip.name);
            bool writeDefaults = states["Idle"].writeDefaultValues;
            int count = 0;
            Set(LeonAnimatorDriver.NativeIdleName, "LeonMove_0160_GaitCycle");
            Set(LeonAnimatorDriver.NativeCrouchIdleName, "LeonMove_1160_GaitCycle");
            Set(LeonAnimatorDriver.NativeHurtIdleName, "LeonHurt_0160_GaitCycle");
            foreach (var gait in LeonAnimatorDriver.NativeGaits)
            {
                Set(LeonAnimatorDriver.NativeGaitStateName(gait.name, "Start"), gait.start);
                Set(LeonAnimatorDriver.NativeGaitStateName(gait.name, "Loop"), gait.loop);
                Set(LeonAnimatorDriver.NativeGaitStateName(gait.name, "Stop"), gait.stop);
            }
            foreach (var stance in LeonAnimatorDriver.NativeStances)
            {
                Set(stance.state, stance.clip);
            }
            foreach (var turn in LeonAnimatorDriver.BasicRunTurns)
            {
                Set(LeonAnimatorDriver.BasicRunTurnStateName("NativePistol", turn.clip), turn.clip);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return count;

            void Set(string name, string id)
            {
                if (!states.TryGetValue(name, out var state))
                {
                    state = machine.AddState(name, new Vector3(1200 + count % 3 * 230, 60 + count / 3 * 90));
                }
                state.motion = clips[id];
                state.writeDefaultValues = writeDefaults;
                EditorUtility.SetDirty(state);
                count++;
            }
        }

        public static int RefreshMainLeonMovingPickup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新移动拾取。");
            }
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            int count = AddMovingPickupStates(graph, source);
            AssetDatabase.SaveAssetIfDirty(graph);
            return count;
        }

        private static int AddMovingPickupStates(AnimatorController graph, Dictionary<string, AnimationClip> source)
        {
            var machine = graph.layers[LeonAnimatorDriver.LayerFullBodyAction].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var motions = AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<AnimationClip>()
                .ToDictionary(clip => clip.name);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var leftArm = Node(rig, "arm left shoulder");
            var rightArm = Node(rig, "arm right shoulder");
            string leftPath = AnimationUtility.CalculateTransformPath(leftArm, rig.transform);
            string rightPath = AnimationUtility.CalculateTransformPath(rightArm, rig.transform);
            var leftPaths = leftArm.GetComponentsInChildren<Transform>(true).ToDictionary(bone => bone.name,
                bone => AnimationUtility.CalculateTransformPath(bone, rig.transform));
            string[] stationarySources = { ChapterOneRootActor.LowPickup, "LeonPickup_0410", "LeonPickup_0400" };
            for (int index = 0; index < stationarySources.Length; index++)
            {
                UpdatePickup(LeonAnimatorDriver.ItemPickupStateNames[index], stationarySources[index], false);
                string movingSource = stationarySources[index];
                if (index == 0)
                {
                    movingSource = "LeonPickup_0406";
                }
                UpdatePickup(LeonAnimatorDriver.MovingItemPickupStateNames[index], movingSource, true);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);
            return stationarySources.Length * 2;

            void UpdatePickup(string name, string sourceName, bool moving)
            {
                string motionName = sourceName + "_QuickReach";
                if (moving)
                {
                    motionName = sourceName + "_MovingReach";
                }
                var sourceClip = source[sourceName];
                int endFrame = Mathf.RoundToInt(sourceClip.length * sourceClip.frameRate);
                AnimationClip motion = SliceMainGanadoMotion(sourceClip, 0, endFrame);
                motion.name = motionName;
                var pickupCurves = AnimationUtility.GetCurveBindings(motion);
                var retained = new Dictionary<EditorCurveBinding, AnimationCurve>();
                foreach (var binding in pickupCurves)
                {
                    if (!binding.path.Contains("/RE4_", StringComparison.Ordinal) &&
                        !binding.path.Contains("/arm right shoulder", StringComparison.Ordinal) &&
                        !binding.path.Contains("/arm right scapula", StringComparison.Ordinal) &&
                        (!binding.path.StartsWith(leftPath, StringComparison.Ordinal) ||
                            binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) &&
                        (!moving || binding.path.Contains("/spine lower/", StringComparison.Ordinal)))
                    {
                        retained.TryAdd(binding, AnimationUtility.GetEditorCurve(motion, binding));
                    }
                    if (!binding.path.StartsWith(rightPath, StringComparison.Ordinal) || binding.type != typeof(Transform) ||
                        !binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string joint = binding.path.Substring(binding.path.LastIndexOf('/') + 1);
                    if (!leftPaths.TryGetValue(joint.Replace("arm right ", "arm left "), out string mirroredPath))
                    {
                        continue;
                    }

                    var curve = AnimationUtility.GetEditorCurve(motion, binding);
                    var keys = curve.keys;
                    if (binding.propertyName.EndsWith(".y", StringComparison.Ordinal) ||
                        binding.propertyName.EndsWith(".z", StringComparison.Ordinal))
                    {
                        for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                        {
                            keys[keyIndex].value = -keys[keyIndex].value;
                            keys[keyIndex].inTangent = -keys[keyIndex].inTangent;
                            keys[keyIndex].outTangent = -keys[keyIndex].outTangent;
                        }
                    }
                    var mirroredBinding = binding;
                    mirroredBinding.path = mirroredPath;
                    retained[mirroredBinding] = new AnimationCurve(keys);
                }
                motion.ClearCurves();
                AnimationUtility.SetEditorCurves(motion, retained.Keys.ToArray(), retained.Values.ToArray());
                motion.EnsureQuaternionContinuity();
                if (motions.TryGetValue(motionName, out var existing))
                {
                    EditorUtility.CopySerialized(motion, existing);
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(motion);
                    motion = existing;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(motion, graph);
                }
                if (!states.TryGetValue(name, out var state))
                {
                    state = machine.AddState(name);
                }
                var completion = state.behaviours.OfType<FullBodyActionSMB>().FirstOrDefault();
                if (completion == null)
                {
                    completion = state.AddStateMachineBehaviour<FullBodyActionSMB>();
                }
                completion.actionType = FullBodyActionType.ItemPickup;
                // 拾取事务按动画接触后的收手时机结束，片段可继续在淡出期间采样。
                completion.autoComplete = false;
                EditorUtility.SetDirty(completion);
                state.motion = motion;
                state.writeDefaultValues = false;
                state.speed = 1f;
                EditorUtility.SetDirty(state);
            }
        }

        private static void AddMainLeonBasicRun(AnimatorController controller, Dictionary<string, AnimationClip> source)
        {
            var machine = controller.layers[0].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var assets = AssetDatabase.LoadAllAssetsAtPath(MainLeonController);
            var clips = assets.OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var trees = assets.OfType<BlendTree>().ToDictionary(tree => tree.name);
            string parameter = LeonAnimatorDriver.BasicRunLeanParameter;
            if (!controller.parameters.Any(item => item.name == parameter))
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            }

            var leftSource = BakeLean("LeonMove_0526");
            var rightSource = BakeLean("LeonMove_0527");
            var rifleCarry = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Leon.controller")
                .OfType<AnimationClip>().Single(clip => clip.name == "CQBR_Carry");
            var rifleArmBindings = ArmBindings(rifleCarry);
            foreach (var turn in LeonAnimatorDriver.BasicRunTurns)
            {
                string name = LeonAnimatorDriver.BasicRunTurnStateName("NativeCqbr", turn.clip);
                if (!clips.TryGetValue(name, out var motion))
                {
                    motion = Object.Instantiate(source[turn.clip]);
                    motion.name = name;
                    var curves = rifleArmBindings.Select(binding => AnimationCurve.Constant(0f, motion.length,
                        AnimationUtility.GetEditorCurve(rifleCarry, binding).Evaluate(0f))).ToArray();
                    AnimationUtility.SetEditorCurves(motion, rifleArmBindings, curves);
                    AssetDatabase.AddObjectToAsset(motion, controller);
                }
                SetMotion(name, motion, states["NativeCqbrJogLoop"]);
            }

            foreach (string prefix in new[] { "NativePistol", "NativeCqbr", "NativeKnife" })
            {
                var centerState = states[prefix + "JogLoop"];
                var center = (AnimationClip)centerState.motion;
                string name = prefix + "BasicRunLoop";
                var left = StoreLean(name + "Left", center, leftSource);
                var right = StoreLean(name + "Right", center, rightSource);
                if (!trees.TryGetValue(name, out var tree))
                {
                    tree = new BlendTree { name = name };
                    AssetDatabase.AddObjectToAsset(tree, controller);
                }
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = parameter;
                tree.useAutomaticThresholds = false;
                tree.children = new[]
                {
                    new ChildMotion { motion = left, threshold = -1f, timeScale = 1f },
                    new ChildMotion { motion = center, threshold = 0f, timeScale = 1f },
                    new ChildMotion { motion = right, threshold = 1f, timeScale = 1f }
                };
                EditorUtility.SetDirty(tree);
                SetMotion(name, tree, centerState);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);

            AnimationClip BakeLean(string id)
            {
                string name = id + "_GaitCycle";
                if (clips.TryGetValue(name, out var existing))
                {
                    return existing;
                }
                var motion = ChapterOneGaitCycles.BakeCycle(source[id], name, 177, 224);
                AssetDatabase.AddObjectToAsset(motion, controller);
                return motion;
            }

            AnimationClip StoreLean(string name, AnimationClip center, AnimationClip lean)
            {
                if (clips.TryGetValue(name, out var existing))
                {
                    return existing;
                }
                // 同源侧倾只改变骨盆和横向根轨，手部维持各武器已校准的跑步握姿。
                var motion = Object.Instantiate(center);
                motion.name = name;
                var bindings = AnimationUtility.GetCurveBindings(lean).Where(binding =>
                    binding.path == "Armature/root ground/root hips"
                    || binding.path.Length == 0 && binding.propertyName == "RootT.x").ToArray();
                var curves = bindings.Select(binding => AnimationUtility.GetEditorCurve(lean, binding)).ToArray();
                AnimationUtility.SetEditorCurves(motion, bindings, curves);
                AssetDatabase.AddObjectToAsset(motion, controller);
                return motion;
            }

            void SetMotion(string name, Motion motion, AnimatorState reference)
            {
                if (!states.TryGetValue(name, out var state))
                {
                    state = machine.AddState(name);
                }
                state.motion = motion;
                state.speed = reference.speed;
                state.writeDefaultValues = reference.writeDefaultValues;
                EditorUtility.SetDirty(state);
            }
        }
    }
}
#endif
