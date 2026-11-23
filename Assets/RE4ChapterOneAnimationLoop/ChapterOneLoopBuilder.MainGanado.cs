#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string LegacyGanadoController = "Assets/Animations/Enemy/Enemy.controller";
        private const string MainGanadoController = Generated + "/MainGanado.controller";
        private const string MainGanadoMaleOverride = Generated + "/MainGanadoMale.overrideController";

        public static string RefreshMainGanado()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式生成正式村民。");
            }

            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacyGanadoController);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(MainGanadoController) == null
                && !AssetDatabase.CopyAsset(LegacyGanadoController, MainGanadoController))
            {
                throw new InvalidOperationException("无法复制正式村民状态机。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainGanadoController);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/GameScene.unity");
            try
            {
                var external = CollectMainGanadoExternalClips(scene);
                var female = new MainGanadoRig(scene, "Ganado", "Assets/Prefab/Enemies/FemaleGanardEnemy.prefab");
                var male = new MainGanadoRig(scene, "GanadoMale", "Assets/Prefab/Villager.prefab");
                var maleOverrides = new Dictionary<AnimationClip, AnimationClip>();

                for (int index = 0; index < source.layers.Length; index++)
                {
                    BindMachine(source.layers[index].stateMachine, graph.layers[index].stateMachine);
                }
                BindStandingGait();

                var maleGraph = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(MainGanadoMaleOverride);
                if (maleGraph == null)
                {
                    maleGraph = new AnimatorOverrideController(graph);
                    AssetDatabase.CreateAsset(maleGraph, MainGanadoMaleOverride);
                }
                maleGraph.ApplyOverrides(maleOverrides.ToList());
                EditorUtility.SetDirty(maleGraph);
                female.Save(Generated + "/MainGanadoFemale.prefab", graph, external, false);
                male.Save(Generated + "/MainGanadoMale.prefab", maleGraph, external, true);
                AssetDatabase.SaveAssets();
                RefreshMainGanadoBodyTechniques();
                return $"正式村民共用 {graph.layers.Length} 层状态图，男村民覆盖 {maleOverrides.Count} 段，外部片段 {external.Length} 段。";

                void BindMachine(AnimatorStateMachine original, AnimatorStateMachine destination)
                {
                    foreach (var child in original.states)
                    {
                        var oldState = child.state;
                        var newState = destination.states.Single(item => item.state.name == oldState.name).state;
                        if (oldState.name == "Locomotion")
                        {
                            continue;
                        }
                        newState.motion = BindMotion(oldState.motion, newState.motion);
                        EditorUtility.SetDirty(newState);
                    }
                    foreach (var child in original.stateMachines)
                    {
                        var stateMachine = child.stateMachine;
                        BindMachine(stateMachine, destination.stateMachines
                            .Single(item => item.stateMachine.name == stateMachine.name).stateMachine);
                    }
                }

                Motion BindMotion(Motion original, Motion destination)
                {
                    if (original is BlendTree sourceTree && destination is BlendTree targetTree)
                    {
                        var children = sourceTree.children;
                        var result = targetTree.children;
                        for (int index = 0; index < children.Length; index++)
                        {
                            result[index].motion = BindMotion(children[index].motion, result[index].motion);
                        }
                        targetTree.children = result;
                        EditorUtility.SetDirty(targetTree);
                        return targetTree;
                    }
                    if (original is AnimationClip clip)
                    {
                        string take = MainGanadoNativeTake(clip.name);
                        var femaleClip = female.Resolve(clip, take);
                        maleOverrides[femaleClip] = male.Resolve(clip, take);
                        return femaleClip;
                    }
                    return original;
                }

                void BindStandingGait()
                {
                    string paceParameter = EnemyAnimationAdapter.NativeGaitPaceParameter;
                    if (!graph.parameters.Any(parameter => parameter.name == paceParameter))
                    {
                        graph.AddParameter(paceParameter, AnimatorControllerParameterType.Float);
                    }

                    var machine = graph.layers[0].stateMachine;
                    var locomotion = machine.states.Single(node => node.state.name == "Locomotion").state;
                    var tree = (BlendTree)locomotion.motion;
                    tree.blendParameter = paceParameter;
                    tree.useAutomaticThresholds = false;
                    tree.children = new[] { "0000", "1001", "2001" }
                        .Select((id, pace) => new ChildMotion
                        {
                            motion = BindNativeGaitClip($"Ganado_{id}_GaitCycle"),
                            threshold = pace,
                            timeScale = 1f
                        }).ToArray();
                    EditorUtility.SetDirty(tree);

                    string[] edges = { "1000", "2000", "1002", "1003", "2002", "2003", "0350", "0351", "0352", "0353" };
                    for (int index = 0; index < edges.Length; index++)
                    {
                        string name = "Ganado_" + edges[index];
                        var edge = machine.states.FirstOrDefault(node => node.state.name == name).state;
                        if (edge == null)
                        {
                            edge = machine.AddState(name, new Vector3(550 + index % 2 * 250, 110 + index / 2 * 75));
                            // 攻击与倒地可从任一步态边进入，受击等全局过渡仍由正式状态图负责。
                            foreach (var original in locomotion.transitions.Where(transition => transition.destinationStateMachine != null))
                            {
                                var exit = edge.AddTransition(original.destinationStateMachine);
                                exit.hasExitTime = original.hasExitTime;
                                exit.hasFixedDuration = original.hasFixedDuration;
                                exit.duration = original.duration;
                                exit.conditions = original.conditions;
                                exit.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                            }
                        }
                        edge.tag = EnemyAnimationAdapter.NativeGaitTag;
                        edge.motion = BindNativeGaitClip(name);
                        EditorUtility.SetDirty(edge);
                    }
                    EditorUtility.SetDirty(machine);
                    EditorUtility.SetDirty(graph);

                    AnimationClip BindNativeGaitClip(string id)
                    {
                        var clip = female.NativeClip(id);
                        maleOverrides[clip] = male.NativeClip(id);
                        return clip;
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static AnimationClip[] CollectMainGanadoExternalClips(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var qte = roots.SelectMany(root => root.GetComponentsInChildren<LeonQTECoordinator>(true)).Single();
            var clips = new HashSet<AnimationClip>();
            foreach (var variant in qte.config.variants)
            {
                if (variant == null)
                {
                    continue;
                }
                clips.UnionWith(new[] { variant.enemyEnter, variant.enemyHold, variant.enemySuccess, variant.enemyFailure });
            }
            foreach (var traversal in roots.SelectMany(root => root.GetComponentsInChildren<EnemyTraversalAnimationLibrary>(true)))
            {
                clips.UnionWith(new[] { traversal.windowClimbThrough, traversal.dropJumpStart, traversal.dropFallLoop, traversal.dropLanding });
            }
            return clips.Where(clip => clip != null && clip.humanMotion).ToArray();
        }

        private static string MainGanadoNativeTake(string source)
        {
            return source switch
            {
                "Zombie_Turn_Left90" => "Ganado_3012",
                "Zombie_Turn_Right90" => "Ganado_3013",
                "Zombie_Turn_180" => "Ganado_3014",
                "Zombie_Idle_Search" => "Ganado_0050",
                "Zombie_Idle_Death" => "Ganado_0000_dead_0th_com",
                "Zombie_Attack01" => "Ganado_0000_attack_0th_com",
                "Zombie_Attack02" => "Ganado_0001",
                "Zombie_Attack03" => "Ganado_0003",
                "zombie_hit_body_front" => "Ganado_0100",
                "zombie_hit_body_back" or "zombie_hit_heavy_back" => "Ganado_0130",
                "zombie_hit_body_left" => "Ganado_0101_damage_0th_com",
                "zombie_hit_body_right" => "Ganado_0102",
                "zombie_hit_heavy_front" => "Ganado_0110",
                "zombie_hit_heavy_left" => "Ganado_0111",
                "zombie_hit_heavy_right" => "Ganado_0112",
                "zombie_hit_head_front" or "zombie_hit_head_back" or "zombie_hit_head_left"
                    or "zombie_hit_head_right" or "Zombie_HitReact_Head" => "Ganado_0000_damage_0th_com",
                "Zombie_HitReact_LeftArm" => "Ganado_0200_damage_0th_com",
                "Zombie_HitReact_RightArm" => "Ganado_0300",
                "Zombie_HitReact_LeftLeg" or "Zombie_Idle_TripOverToCrawlIdle_KneeHeight"
                    or "Zombie_Idle_TripOverToCrawlIdle_MidHeight" => "GanadoGround_0421",
                "Zombie_HitReact_RightLeg" => "GanadoGround_0521",
                "Zombie_Crawl_Idle" => "GanadoGround_3001",
                "Zombie_Crawl_Forward" => "GanadoGround_2121",
                "Zombie_Crawl_CrawlIdle_To_Stand" => "GanadoGround_3500",
                _ => null
            };
        }

        private static AnimationClip BuildMainGanadoHeadReaction(AnimationClip source)
        {
            // 第 12 帧进入受击动作，第 156 帧双脚重新接地。片尾的持续前行交还步态与寻路。
            return SliceMainGanadoMotion(source, 12, 156);
        }

        private static AnimationClip SliceMainGanadoMotion(AnimationClip source, int firstFrame, int lastFrame)
        {
            float first = firstFrame / source.frameRate;
            float last = lastFrame / source.frameRate;
            var result = new AnimationClip { frameRate = source.frameRate };
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = new AnimationCurve[bindings.Length];
            for (int index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                float origin = 0f;
                if (binding.type == typeof(Animator) && binding.propertyName.StartsWith("RootT.", StringComparison.Ordinal))
                {
                    origin = curve.Evaluate(first);
                }

                var keys = new List<Keyframe> { Boundary(first) };
                foreach (var sourceKey in curve.keys)
                {
                    if (sourceKey.time <= first + 0.00001f || sourceKey.time >= last - 0.00001f)
                    {
                        continue;
                    }
                    var key = sourceKey;
                    key.time -= first;
                    key.value -= origin;
                    keys.Add(key);
                }
                keys.Add(Boundary(last));
                curves[index] = new AnimationCurve(keys.ToArray());

                Keyframe Boundary(float time)
                {
                    const float delta = 0.001f;
                    float slope = (curve.Evaluate(time + delta) - curve.Evaluate(time - delta)) / (2f * delta);
                    return new Keyframe(time - first, curve.Evaluate(time) - origin, slope, slope);
                }
            }
            AnimationUtility.SetEditorCurves(result, bindings, curves);
            result.EnsureQuaternionContinuity();
            return result;
        }

        private sealed class MainGanadoRig
        {
            private readonly GameObject instance;
            private readonly Animator animator;
            private readonly Avatar nativeAvatar;
            private readonly Avatar samplerAvatar;
            private readonly Transform rig;
            private readonly string role;
            private readonly ChapterOneBakedClips bank;
            private readonly Dictionary<string, AnimationClip> native;
            private readonly Dictionary<string, AnimationClip> stored;
            private readonly Dictionary<AnimationClip, AnimationClip> fallback;
            private readonly HashSet<AnimationClip> compiled = new();
            private readonly (Transform bone, Vector3 position, Quaternion rotation)[] restPose;

            public GameObject Instance => instance;

            public MainGanadoRig(Scene scene, string role, string gameplayPrefabPath)
            {
                this.role = role;
                instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPrefabPath), scene);
                var art = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{Generated}/{role}.prefab"), scene);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                foreach (var child in instance.transform.Cast<Transform>().ToArray())
                {
                    bool legacySawRig = role == "Salvador" && (child.name == "IK RIG" || child.name == "Salvador_Skeleton");
                    if (child.name != "root" && !legacySawRig && child.GetComponent<SkinnedMeshRenderer>() == null)
                    {
                        throw new InvalidOperationException($"正式村民有待迁移的子对象 {child.name}。");
                    }
                    Object.DestroyImmediate(child.gameObject);
                }
                nativeAvatar = art.GetComponent<Animator>().avatar;
                foreach (var child in art.transform.Cast<Transform>().ToArray())
                {
                    child.SetParent(instance.transform, false);
                }
                Object.DestroyImmediate(art);

                animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig = instance.transform.Find(role + "_Skeleton");
                samplerAvatar = BuildLegBiteAvatar(instance);
                if (rig == null || nativeAvatar == null || !nativeAvatar.isValid || !samplerAvatar.isValid)
                {
                    throw new InvalidOperationException($"无法绑定 {role} 正式村民骨架。");
                }
                animator.avatar = samplerAvatar;
                animator.Rebind();
                var bindings = instance.GetComponent<AnimatorBoneBindings>();
                if (bindings == null)
                {
                    bindings = instance.AddComponent<AnimatorBoneBindings>();
                }
                bindings.CaptureHumanoid();
                restPose = instance.GetComponentsInChildren<Transform>(true)
                    .Select(bone => (bone, bone.localPosition, bone.localRotation)).ToArray();

                string bankPath = $"{Generated}/Main{role}Motions.asset";
                bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
                if (bank == null)
                {
                    bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                    AssetDatabase.CreateAsset(bank, bankPath);
                }
                native = AssetDatabase.LoadAllAssetsAtPath($"{Generated}/Clips/{role}_Clips.asset").OfType<AnimationClip>()
                    .Concat(AssetDatabase.LoadAllAssetsAtPath($"{Generated}/GaitCycles/{role}.asset").OfType<AnimationClip>())
                    .ToDictionary(clip => clip.name);
                stored = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>()
                    .Where(clip => clip.name.StartsWith("Main", StringComparison.Ordinal)).ToDictionary(clip => clip.name);
                fallback = bank.ReadSourceLinks();
            }

            public AnimationClip Resolve(AnimationClip source, string nativeTake = null)
            {
                if (nativeTake == null)
                {
                    if (!source.humanMotion)
                    {
                        return source;
                    }
                    if (compiled.Add(source))
                    {
                        var baked = BakeLegacyHumanoid(source, animator, rig, source.name, source.isLooping, sharedStage: false);
                        foreach (var joint in restPose)
                        {
                            joint.bone.SetLocalPositionAndRotation(joint.position, joint.rotation);
                        }
                        AnimationUtility.SetAnimationEvents(baked, AnimationUtility.GetAnimationEvents(source));
                        fallback.TryGetValue(source, out var existing);
                        fallback[source] = Store(baked, existing);
                    }
                    return fallback[source];
                }

                string name = "MainGanado_" + source.name + "_" + nativeTake;
                if (stored.TryGetValue(name, out var cached) && compiled.Contains(cached))
                {
                    return cached;
                }
                var replacement = native[nativeTake];
                if (nativeTake == "Ganado_0000_damage_0th_com")
                {
                    replacement = BuildMainGanadoHeadReaction(replacement);
                }
                else
                {
                    replacement = Object.Instantiate(replacement);
                }
                replacement.name = name;
                var events = AnimationUtility.GetAnimationEvents(source);
                foreach (var marker in events)
                {
                    marker.time = marker.time / source.length * replacement.length;
                    if (marker.functionName == "OnAttackHit")
                    {
                        marker.time = Mathf.Round(0.38f * replacement.length * replacement.frameRate) / replacement.frameRate;
                    }
                }
                AnimationUtility.SetAnimationEvents(replacement, events);
                var saved = Store(replacement, cached);
                compiled.Add(saved);
                stored[name] = saved;
                return saved;
            }

            public AnimationClip NativeClip(string name) => native[name];

            public AnimationClip StoreNamedClip(AnimationClip clip)
            {
                stored.TryGetValue(clip.name, out var previous);
                var result = Store(clip, previous);
                stored[result.name] = result;
                return result;
            }

            public void Save(string path, RuntimeAnimatorController controller, AnimationClip[] external, bool maleContacts)
            {
                var presenter = instance.GetComponent<EnemyAnimationAdapter>();
                if (role == "Salvador")
                {
                    presenter.ConfigureNativeSawGait();
                }
                else
                {
                    presenter.ConfigureNativeFootContacts(maleContacts);
                }
                var adapter = new SerializedObject(presenter);
                var sources = adapter.FindProperty("legacyClipSources");
                var targets = adapter.FindProperty("legacyClipRebindings");
                sources.arraySize = external.Length;
                targets.arraySize = external.Length;
                for (int index = 0; index < external.Length; index++)
                {
                    sources.GetArrayElementAtIndex(index).objectReferenceValue = external[index];
                    targets.GetArrayElementAtIndex(index).objectReferenceValue = Resolve(external[index]);
                }
                adapter.ApplyModifiedPropertiesWithoutUndo();
                bank.WriteSourceLinks(fallback);
                EditorUtility.SetDirty(bank);

                var host = instance.GetComponent<VillagerController>();
                host.useRootMotion = true;
                host.useStandingAnimationSpeedForPath = true;
                animator.avatar = nativeAvatar;
                animator.applyRootMotion = true;
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                host.EnsureRuntimeHitboxCoverage();
                instance.GetComponent<VillagerQTEParticipant>().participantColliders = instance.GetComponentsInChildren<Collider>(true);
                Object.DestroyImmediate(samplerAvatar);
                instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (PrefabUtility.SaveAsPrefabAsset(instance, path) == null)
                {
                    throw new InvalidOperationException($"无法保存 {path}。");
                }
            }

            private AnimationClip Store(AnimationClip clip, AnimationClip existing)
            {
                if (existing == null)
                {
                    AssetDatabase.AddObjectToAsset(clip, bank);
                    return clip;
                }
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(clip);
                return existing;
            }
        }
    }
}
#endif
