#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GameArchitecture;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public const string MainGanadoArmedPrefab = Generated + "/MainGanadoArmed.prefab";
        private const string MainArmedMotionBank = Generated + "/MainGanadoArmedMotions.asset";
        private const string MainArmedOverride = Generated + "/MainGanadoArmed.overrideController";
        private const string ArmedSourcePrefix = "GanadoArmed1st_";

        public static string RefreshMainGanadoArmed()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式生成正式持械村民。");
            }

            ImportAdditionalArt("wp5801_Model");
            ImportAdditionalArt("GanadoArmed1st");
            ImportAdditionalArt("GanadoArmedReactions");
            ImportAdditionalArt("GanadoArmedStandingLeg");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var male = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/MainGanadoMale.prefab");
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(male, preview);
                PrefabUtility.UnpackPrefabInstance(actor, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                var animator = actor.GetComponent<Animator>();
                animator.enabled = false;
                var maleGraph = (AnimatorOverrideController)animator.runtimeAnimatorController;
                var graph = (AnimatorController)maleGraph.runtimeAnimatorController;
                var sourceClips = GetMainArmedSourceClips(actor, "GanadoArmed1st", ArmedSourcePrefix);
                var weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Native/wp5801_Model.fbx");
                var weapon = (GameObject)PrefabUtility.InstantiatePrefab(weaponPrefab, preview);
                PrefabUtility.UnpackPrefabInstance(weapon, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var skin = weapon.GetComponentInChildren<SkinnedMeshRenderer>();
                var vertices = skin.sharedMesh.vertices.Select(vertex =>
                    weapon.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex))).ToArray();
                var head = ArmedWeaponBounds(vertices.Where(vertex => vertex.x > 0.055f && vertex.z > 0.18f));
                var weaponBounds = ArmedWeaponBounds(vertices);
                var socket = Node(actor, "R_Wep");
                if (socket == null || socket.parent.name != "R_Hand")
                {
                    throw new InvalidOperationException("正式男村民缺少右手的原版武器骨。");
                }
                weapon.name = "WP5801";
                weapon.transform.SetParent(socket, false);
                // 斧柄跨过食指到无名指。导出的模型前轴已与 R_Wep 一致。
                weapon.transform.localRotation = Quaternion.identity;
                weapon.transform.localPosition = new Vector3(0.018f, 0.014f, -0.022f) / socket.lossyScale.x;
                weapon.transform.localScale = Vector3.one / socket.lossyScale.x;
                skin.sharedMaterials = skin.sharedMaterials.Select(material => ResolveMaterial("wp5801", material)).ToArray();
                var droppedCollider = weapon.AddComponent<BoxCollider>();
                droppedCollider.center = weaponBounds.center;
                droppedCollider.size = weaponBounds.size;
                droppedCollider.enabled = false;

                var bank = GetArmedClipBank(MainArmedMotionBank);
                var existing = AssetDatabase.LoadAllAssetsAtPath(MainArmedMotionBank).OfType<AnimationClip>()
                    .ToDictionary(clip => clip.name);
                var byState = new Dictionary<AnimationClip, AnimationClip>();
                var machine = graph.layers[0].stateMachine;
                var gait = (BlendTree)FindArmedStateClipOwner(machine, "Locomotion").motion;
                var cycles = new[] { ("0000", 0, 60), ("1001", 207, 291), ("2001", 153, 198) };
                for (int index = 0; index < cycles.Length; index++)
                {
                    var (id, first, end) = cycles[index];
                    string name = ArmedSourcePrefix + id + "_GaitCycle";
                    byState.Add((AnimationClip)gait.children[index].motion,
                        Store(ChapterOneGaitCycles.BakeCycle(sourceClips[id], name, first, end)));
                }
                foreach (string id in new[] { "1000", "2000", "1002", "1003", "2002", "2003", "0350", "0351", "0352", "0353" })
                {
                    byState.Add((AnimationClip)FindArmedStateClipOwner(machine, "Ganado_" + id).motion, sourceClips[id]);
                }

                var attacks = new[]
                {
                    ("Attack0001", 39, 43, 45, 188, KnifeIncomingAttackCategory.OutwardDeflection),
                    ("Attack0003", 41, 48, 49, 150, KnifeIncomingAttackCategory.High),
                    ("Attack0101", 50, 55, 59, 196, KnifeIncomingAttackCategory.CloseRange)
                };
                var profiles = new KnifeEnemyAttackProfile[attacks.Length];
                for (int index = 0; index < attacks.Length; index++)
                {
                    var (id, parry, begin, end, recovery, category) = attacks[index];
                    var clip = SliceMainGanadoMotion(sourceClips[id], 0, recovery);
                    clip.name = ArmedSourcePrefix + id + "_Combat";
                    AnimationUtility.SetAnimationEvents(clip, new[]
                    {
                        new AnimationEvent { time = (recovery - 3) / clip.frameRate, functionName = "OnAttackComplete" }
                    });
                    clip = Store(clip);
                    var state = FindArmedStateClipOwner(machine, $"Attack{index + 1:00}");
                    byState.Add((AnimationClip)state.motion, clip);
                    profiles[index] = SampleMainArmedAttack(actor, weapon.transform, head, clip, parry, begin, end, category);
                }

                var disarm = BindMainArmedReactions(actor, machine, sourceClips["0000"], byState, Store);
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                maleGraph.GetOverrides(overrides);

                int bound = 0;
                for (int index = 0; index < overrides.Count; index++)
                {
                    var original = overrides[index].Key;
                    if (byState.TryGetValue(original, out var replacement))
                    {
                        overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                        bound++;
                    }
                }
                if (bound != byState.Count)
                {
                    throw new InvalidOperationException($"持械村民应覆盖 {byState.Count} 个正式槽位，实际 {bound} 个。");
                }

                var armedGraph = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(MainArmedOverride);
                if (armedGraph == null)
                {
                    armedGraph = new AnimatorOverrideController(graph);
                    AssetDatabase.CreateAsset(armedGraph, MainArmedOverride);
                }
                armedGraph.ApplyOverrides(overrides);
                EditorUtility.SetDirty(armedGraph);

                var heldWeapon = actor.AddComponent<EnemyHeldWeapon>();
                heldWeapon.Configure(droppedCollider, maleGraph, disarm);
                var windowBank = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/GanadoArmedReactions_Clips.asset")
                    .OfType<AnimationClip>().ToDictionary(clip => clip.name);
                var crossing = Store(BuildMainArmedWindowClip(windowBank["GanadoArmedReaction_WindowFallStart"], actor, socket.parent));
                actor.GetComponent<EnemyWindowTraversal>().ConfigureNativeWindow(heldWeapon,
                    ChapterOnePairMotion.ReadTrack(crossing),
                    ChapterOnePairMotion.ReadTrack(windowBank["GanadoArmedReaction_WindowFallEnd"]));
                animator.runtimeAnimatorController = armedGraph;
                animator.enabled = true;
                animator.Rebind();
                var controller = actor.GetComponent<VillagerController>();
                controller.animationPresenter.ConfigureNativeArmedGait();
                float standingRange = profiles.Max(profile => profile.swingStartDistance) + 0.07f;
                controller.combatModule.ConfigureStandingWeaponAttacks(profiles, standingRange);
                actor.name = "MainGanadoArmed";
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (PrefabUtility.SaveAsPrefabAsset(actor, MainGanadoArmedPrefab) == null)
                {
                    throw new InvalidOperationException("无法保存正式持械村民。");
                }
                AssetDatabase.SaveAssetIfDirty(bank);
                AssetDatabase.SaveAssetIfDirty(armedGraph);
                AssetDatabase.SaveAssetIfDirty(graph);
                RefreshMainGanadoBodyTechniques();
                return $"WP5801 覆盖 {bound} 个槽位，增加缴械、站立腿部受击及左右倒地五态，刃部半径 {head.extents.magnitude:F3} m，起手距离 "
                    + string.Join("、", profiles.Select(profile => $"{profile.swingStartDistance:F2} m")) + "。";

                AnimationClip Store(AnimationClip clip)
                {
                    if (existing.TryGetValue(clip.name, out var stored))
                    {
                        EditorUtility.CopySerialized(clip, stored);
                        EditorUtility.SetDirty(stored);
                        Object.DestroyImmediate(clip);
                        return stored;
                    }
                    AssetDatabase.AddObjectToAsset(clip, bank);
                    EditorUtility.SetDirty(bank);
                    return clip;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static AnimatorState FindArmedStateClipOwner(AnimatorStateMachine machine, string name)
        {
            var direct = machine.states.FirstOrDefault(item => item.state.name == name).state;
            if (direct != null)
            {
                return direct;
            }
            foreach (var nested in machine.stateMachines)
            {
                var found = FindArmedStateClipOwner(nested.stateMachine, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static Dictionary<string, AnimationClip> GetMainArmedSourceClips(GameObject actor, string group, string prefix)
        {
            string bankPath = Generated + "/Clips/" + group + "_Clips.asset";
            var bank = GetArmedClipBank(bankPath);
            var existing = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>()
                .ToDictionary(clip => clip.name);
            var sources = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Native/" + group + ".fbx")
                .OfType<AnimationClip>().Where(clip => clip.name.StartsWith(prefix, StringComparison.Ordinal));
            var result = new Dictionary<string, AnimationClip>();
            foreach (var source in sources)
            {
                string id = source.name.Substring(prefix.Length);
                var clip = Bake(source, actor, "GanadoMale", "GanadoMale", false, Array.Empty<int>(),
                    bank, existing, out _);
                result.Add(id, clip);
            }
            AssetDatabase.SaveAssetIfDirty(bank);
            return result;
        }

        private static ChapterOneBakedClips GetArmedClipBank(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static Bounds ArmedWeaponBounds(IEnumerable<Vector3> points)
        {
            using var vertices = points.GetEnumerator();
            if (!vertices.MoveNext())
            {
                throw new InvalidOperationException("WP5801 网格缺少刃部顶点。");
            }
            var bounds = new Bounds(vertices.Current, Vector3.zero);
            while (vertices.MoveNext())
            {
                bounds.Encapsulate(vertices.Current);
            }
            return bounds;
        }

        private static KnifeEnemyAttackProfile SampleMainArmedAttack(GameObject actor, Transform weapon, Bounds head,
            AnimationClip clip, int parry, int begin, int end, KnifeIncomingAttackCategory category)
        {
            float rate = clip.frameRate;
            var segment = new KnifeEnemyAttackSegment
            {
                hand = HumanBodyBones.RightHand,
                begin = begin / rate,
                end = end / rate,
                radius = head.extents.magnitude,
                localPoints = new Vector3[(end - begin) * 2 + 1]
            };
            float maxReach = 0f;
            for (int sample = 0; sample < segment.localPoints.Length; sample++)
            {
                float time = (begin + sample * 0.5f) / rate;
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clip.SampleAnimation(actor, time);
                Vector3 point = actor.transform.InverseTransformPoint(weapon.TransformPoint(head.center));
                segment.localPoints[sample] = point;
                maxReach = Mathf.Max(maxReach, new Vector2(point.x, point.z).magnitude);
            }
            var firstPoint = segment.localPoints[0];
            Vector2 entryToContact = new Vector2(firstPoint.x + RootAtContact("x"), firstPoint.z + RootAtContact("z"));
            return new KnifeEnemyAttackProfile
            {
                sourceClip = clip,
                category = category,
                contactNormalizedPoint = segment.begin / clip.length,
                contactDistance = maxReach + segment.radius + 0.4f,
                contactAngle = 120f,
                promptEarly = (begin - parry) / rate,
                swingStartDistance = entryToContact.magnitude,
                contactSegments = new[] { segment }
            };

            float RootAtContact(string axis)
            {
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + axis);
                return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(segment.begin);
            }
        }
    }
}
#endif
