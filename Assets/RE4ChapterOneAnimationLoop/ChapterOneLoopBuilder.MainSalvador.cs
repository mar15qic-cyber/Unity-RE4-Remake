#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string LegacySawPrefab = "Assets/Prefab/Enemies/SawManEnemy.prefab";
        private const string LegacySawGraph = "Assets/Animations/Enemy/ChainsawMan/ChainsawMan.controller";
        private const string MainSawGraph = Generated + "/MainSalvador.controller";
        private const string MainSawPreview = Generated + "/MainSalvador.prefab";

        public static string RefreshMainSalvador(bool applyFormalPrefab = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式生成正式电锯男。");
            }

            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(LegacySawGraph);
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainSawGraph);
            if (graph == null)
            {
                if (!AssetDatabase.CopyAsset(LegacySawGraph, MainSawGraph))
                {
                    throw new InvalidOperationException("无法复制电锯男状态图。");
                }
                graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainSawGraph);
            }

            var preview = EditorSceneManager.OpenPreviewScene("Assets/GameScene.unity");
            try
            {
                var rig = new MainGanadoRig(preview, "Salvador", LegacySawPrefab);
                var actor = rig.Instance;
                var weapon = AttachMainSaw(actor, preview);
                var external = CollectMainGanadoExternalClips(preview);
                var counts = new Dictionary<string, string>();
                for (int layer = 0; layer < old.layers.Length; layer++)
                {
                    Bind(old.layers[layer].stateMachine, graph.layers[layer].stateMachine);
                }
                BindMainSawGait(graph, rig);
                var target = MainSawPreview;
                if (applyFormalPrefab)
                {
                    target = LegacySawPrefab;
                }
                rig.Save(target, graph, external, true);
                AssetDatabase.SaveAssetIfDirty(graph);
                var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(Generated + "/MainSalvadorMotions.asset");
                AssetDatabase.SaveAssetIfDirty(bank);
                return $"{target} 已绑定 {counts.Count} 个原状态、三段步态循环、九段起停交接和 {external.Length} 个正式外部片段。";

                void Bind(AnimatorStateMachine original, AnimatorStateMachine destination)
                {
                    foreach (var item in original.states)
                    {
                        var input = item.state;
                        if (input.name == "Locomotion" || input.motion is not AnimationClip legacy)
                        {
                            continue;
                        }
                        var state = destination.states.Single(child => child.state.name == input.name).state;
                        var source = ResolveMainSawSource(input.name);
                        if (source.id == null)
                        {
                            state.motion = rig.Resolve(legacy);
                        }
                        else
                        {
                            var clip = SliceMainGanadoMotion(rig.NativeClip(source.id), source.first, source.last);
                            clip.name = "MainSalvador_" + input.name + "_" + source.id;
                            if (input.name == "PullStart")
                            {
                                BindMainSawMechanism(actor.transform, weapon, clip);
                            }
                            BindMainSawNativeAttackTiming(state, source);
                            state.motion = rig.StoreNamedClip(clip);
                        }
                        counts.Add(original.name + "." + input.name, state.motion.name);
                        EditorUtility.SetDirty(state);
                    }
                    foreach (var item in original.stateMachines)
                    {
                        var stateMachine = item.stateMachine;
                        Bind(stateMachine, destination.stateMachines.Single(x => x.stateMachine.name == stateMachine.name).stateMachine);
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void BindMainSawNativeAttackTiming(AnimatorState state, (string id, int first, int last, int hit, int sign) source)
        {
            float frames = source.last - source.first;
            // 在补回的视觉尾段内保留旧片长对应的通知与体术窗口时钟。
            var (oldLength, completion) = MainSawOriginalHitClock(state.name);
            if (oldLength > 0)
            {
                var hit = state.behaviours.OfType<EnemyTimedHitStateSMB>().Single();
                hit.completeNormalizedTime = oldLength * completion / frames;
                hit.opportunityTimeScale = frames / oldLength;
                EditorUtility.SetDirty(hit);
            }
            var timed = state.behaviours.OfType<EnemyTimedAttackStateSMB>().FirstOrDefault();
            if (timed != null && source.hit > source.first)
            {
                timed.hitNormalizedTime = (source.hit - source.first) / frames;
                timed.parryWindowOpenNormalizedTime = Mathf.Max(.01f, (source.sign - source.first) / frames);
                timed.completeNormalizedTime = 1f - 4f / frames;
                EditorUtility.SetDirty(timed);
            }

            if (state.name == "Attack_DoubleSwing")
            {
                var swing = state.behaviours.OfType<ChainsawDoubleSwingAttackStateSMB>().Single();
                swing.firstHitNormalizedTime = (source.hit - source.first) / frames;
                swing.secondHitNormalizedTime = 1f;
                swing.completeNormalizedTime = 1f - 4f / frames;
                EditorUtility.SetDirty(swing);
            }
        }

        private static (int frames, float completion) MainSawOriginalHitClock(string state)
        {
            return state switch
            {
                "StaggerSmall01" => (68, .90f),
                "StaggerSmall02" => (78, .90f),
                "Hit_LeftLeg" => (62, .95f),
                "Hit_RightLeg" => (82, .95f),
                "Stagger_Heavy_Left" => (78, .90f),
                "Stagger_Heavy_Right" => (76, .90f),
                _ => default
            };
        }

        public static int RefreshMainSawNativeCombat()
        {
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainSawGraph);
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(Generated + "/MainSalvadorMotions.asset");
            var saved = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(bank)).OfType<AnimationClip>()
                .Where(clip => clip.name.StartsWith("MainSalvador_", StringComparison.Ordinal)).ToDictionary(clip => clip.name);
            var sources = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Salvador_Clips.asset").OfType<AnimationClip>().ToDictionary(clip => clip.name);
            int count = Update(graph.layers[0].stateMachine);
            EditorUtility.SetDirty(graph);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(graph);
            AssetDatabase.SaveAssetIfDirty(bank);
            return count;

            int Update(AnimatorStateMachine machine)
            {
                int changed = 0;
                foreach (var child in machine.states)
                {
                    var state = child.state;
                    var source = ResolveMainSawSource(state.name);
                    if (source.id == null || state.motion is not AnimationClip current ||
                        current.name.EndsWith("_" + source.id, StringComparison.Ordinal) &&
                        (MainSawOriginalHitClock(state.name).frames == 0 ||
                         Mathf.RoundToInt(current.length * current.frameRate) == source.last - source.first))
                    {
                        continue;
                    }

                    var clip = SliceMainGanadoMotion(sources[source.id], source.first, source.last);
                    clip.name = "MainSalvador_" + state.name + "_" + source.id;
                    if (saved.TryGetValue(clip.name, out var existing))
                    {
                        EditorUtility.CopySerialized(clip, existing);
                        UnityEngine.Object.DestroyImmediate(clip);
                        clip = existing;
                    }
                    else
                    {
                        AssetDatabase.AddObjectToAsset(clip, bank);
                    }
                    state.motion = clip;
                    BindMainSawNativeAttackTiming(state, source);
                    EditorUtility.SetDirty(state);
                    changed++;
                }
                foreach (var child in machine.stateMachines)
                {
                    changed += Update(child.stateMachine);
                }
                return changed;
            }
        }

        private static (string id, int first, int last, int hit, int sign) ResolveMainSawSource(string state)
        {
            return state switch
            {
                "PullStart" => ("Salvador_0702", 0, 182, 0, 0),
                "Death" => ("Salvador_0000_dead_2nd_wp5300", 0, 248, 0, 0),
                "Taunt_Stomp" => ("Salvador_0200", 0, 188, 0, 0),
                "Turn_Left90" => ("Salvador_3012", 0, 86, 0, 0),
                "Turn_Right90" => ("Salvador_3013", 0, 86, 0, 0),
                "Turn_180" => ("Salvador_3014", 0, 104, 0, 0),
                "Attack_DoubleSwing" => ("Salvador_0365", 0, 236, 73, 23),
                "Attack_FullSpinSwing" => ("Salvador_0356", 0, 400, 58, 15),
                // 攻击提示使用原版 ActionSign 起点，伤害时刻使用 Slash 窗口内的刀链接触帧。
                "Attack_RightSwing" => ("Salvador_0020", 72, 268, 175, 119),
                "Attack_LeftSwing" => ("Salvador_0021", 0, 148, 50, 11),
                "StaggerSmall01" => ("Salvador_0100", 0, 80, 0, 0),
                "Stagger_Body_Front" => ("Salvador_0100", 0, 68, 0, 0),
                "StaggerSmall02" => ("Salvador_0101", 0, 90, 0, 0),
                "Stagger_Body_Left" => ("Salvador_0101", 0, 78, 0, 0),
                "StaggerLarge" or "Hit_HeavyFront" or "Stagger_Heavy_Front" => ("Salvador_0110", 0, 119, 0, 0),
                "Stagger_Body_Right" or "Stagger_Head_Right" => ("Salvador_0102", 0, 76, 0, 0),
                "Stagger_Body_Back" or "Stagger_Head_Back" or "Stagger_Heavy_Back" => ("Salvador_0130", 0, 94, 0, 0),
                "Stagger_Heavy_Right" => ("Salvador_0102", 0, 80, 0, 0),
                "Stagger_Heavy_Left" => ("Salvador_0101", 0, 82, 0, 0),
                "Stagger_Head_Left" => ("Salvador_0101", 0, 78, 0, 0),
                "Stagger_Head_Front" => ("Salvador_0000_damage_2nd_wp5300", 0, 106, 0, 0),
                "Hit_LeftLeg" => ("Salvador_0400", 0, 68, 0, 0),
                "Hit_RightLeg" => ("Salvador_0500", 0, 88, 0, 0),
                _ => default
            };
        }

        private static Transform AttachMainSaw(GameObject actor, UnityEngine.SceneManagement.Scene preview)
        {
            var socket = Node(actor, "RE4_WeaponSocket");
            var weapon = socket.Find("wp5300_Model");
            if (weapon == null)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/wp5300_Model.fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                weapon = instance.transform;
                weapon.SetParent(socket, false);
                string[] mount = File.ReadLines(Source + "/GripAudit/NativeMounts.tsv").Select(row => row.Split('\t'))
                    .Single(columns => columns[0] == "5300");
                ApplyEquipmentMount(weapon, mount);
            }
            var blade = new Bounds();
            bool hasBlade = false;
            foreach (var renderer in weapon.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = ResolveMaterial("wp5300", materials[index]);
                }
                renderer.sharedMaterials = materials;

                if (!renderer.name.Contains("Chain", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Vector3 vertex in renderer.sharedMesh.vertices)
                {
                    Vector3 position = weapon.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (hasBlade)
                    {
                        blade.Encapsulate(position);
                    }
                    else
                    {
                        blade = new Bounds(position, Vector3.zero);
                        hasBlade = true;
                    }
                }
            }
            if (!hasBlade)
            {
                throw new InvalidOperationException("WP5300 缺少刀链网格。");
            }

            var marker = new GameObject("RE4_ChainsawBlade");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, preview);
            marker.transform.SetParent(weapon, false);
            marker.transform.localPosition = blade.center;
            actor.GetComponent<ChainsawManAnimationBridge>().chainsawDamageOrigin = marker.transform;
            return weapon;
        }

        private static void BindMainSawMechanism(Transform actor, Transform weapon, AnimationClip destination)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp5300_Mechanism.fbx").OfType<AnimationClip>()
                .Single(clip => clip.name == "Salvador_0702_Mechanism");
            string prefix = AnimationUtility.CalculateTransformPath(weapon, actor);
            var sourceBindings = AnimationUtility.GetCurveBindings(source);
            var targets = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            float end = destination.length;
            foreach (var sourceBinding in sourceBindings)
            {
                if (sourceBinding.type != typeof(Transform) || weapon.Find(sourceBinding.path) == null)
                {
                    continue;
                }

                var target = sourceBinding;
                target.path = prefix + "/" + sourceBinding.path;
                var curve = AnimationUtility.GetEditorCurve(source, sourceBinding);
                var keys = curve.keys.Where(key => key.time < end).Append(new Keyframe(end, curve.Evaluate(end)));
                targets.Add(target);
                curves.Add(new AnimationCurve(keys.ToArray()));
            }
            if (targets.Count == 0)
            {
                throw new InvalidOperationException("电锯启动片缺少可绑定的机构轨道。");
            }
            AnimationUtility.SetEditorCurves(destination, targets.ToArray(), curves.ToArray());
            destination.EnsureQuaternionContinuity();
        }

        private static void BindMainSawGait(AnimatorController graph, MainGanadoRig rig)
        {
            string parameter = EnemyAnimationAdapter.NativeGaitPaceParameter;
            if (!graph.parameters.Any(item => item.name == parameter))
            {
                graph.AddParameter(parameter, AnimatorControllerParameterType.Float);
            }

            var machine = graph.layers[0].stateMachine;
            var locomotion = machine.states.Single(item => item.state.name == "Locomotion").state;
            var tree = (BlendTree)locomotion.motion;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = parameter;
            tree.useAutomaticThresholds = false;
            tree.children = new[] { "0000", "1001", "2001" }.Select((id, pace) => new ChildMotion
            {
                motion = rig.NativeClip($"Salvador_{id}_GaitCycle"),
                threshold = pace,
                timeScale = 1f
            }).ToArray();
            EditorUtility.SetDirty(tree);

            string[] edges = { "1000", "2000", "1002", "1003", "2002", "0350", "0351", "0352", "0353" };
            for (int index = 0; index < edges.Length; index++)
            {
                string name = "Salvador_" + edges[index];
                var state = machine.states.FirstOrDefault(item => item.state.name == name).state;
                if (state == null)
                {
                    state = machine.AddState(name, new Vector3(650 + index % 2 * 250, 110 + index / 2 * 75));
                    foreach (var existing in locomotion.transitions)
                    {
                        AnimatorStateTransition transition;
                        if (existing.destinationStateMachine != null)
                        {
                            transition = state.AddTransition(existing.destinationStateMachine);
                        }
                        else if (existing.destinationState != null)
                        {
                            transition = state.AddTransition(existing.destinationState);
                        }
                        else
                        {
                            continue;
                        }

                        transition.hasExitTime = existing.hasExitTime;
                        transition.exitTime = existing.exitTime;
                        transition.hasFixedDuration = existing.hasFixedDuration;
                        transition.duration = existing.duration;
                        transition.offset = existing.offset;
                        transition.conditions = existing.conditions;
                        transition.interruptionSource = existing.interruptionSource;
                        transition.orderedInterruption = existing.orderedInterruption;
                    }
                }
                state.tag = EnemyAnimationAdapter.NativeGaitTag;
                state.motion = rig.NativeClip(name);
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);
        }
    }
}
#endif
