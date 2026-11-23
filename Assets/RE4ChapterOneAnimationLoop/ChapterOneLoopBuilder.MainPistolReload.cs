#if UNITY_EDITOR
using System;
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
        private const string MainPistolReloadBank = Generated + "/MainLeonPistolReloadMotions.asset";
        private const string PistolMechanismRoot = "wp4000_Skeleton/root/_00/";

        private static bool BindMainPistolReload(LeonController leon, AnimationClip empty, AnimationClip tactical)
        {
            var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(item => item.data != null && item.data.weaponType == WeaponType.Pistol);
            var model = weapon.VisualModel.transform;
            var rig = new SerializedObject(leon.GetComponent<LeonAnimRig>());
            rig.FindProperty("nativePistolEmptyReloadMechanism").objectReferenceValue = empty;
            rig.FindProperty("nativePistolTacticalReloadMechanism").objectReferenceValue = tactical;
            rig.FindProperty("nativePistolIncomingMagazine").objectReferenceValue = model.Find(PistolMechanismRoot + "_04");
            rig.FindProperty("nativePistolOutgoingMagazine").objectReferenceValue = model.Find(PistolMechanismRoot + "_14");
            bool changed = rig.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            }
            return changed;
        }

        private static (AnimationClip empty, AnimationClip tactical) AddMainPistolReloadMotions(AnimatorController graph)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Where(clip => clip.name.StartsWith("SG09R_", StringComparison.Ordinal)
                    || clip.name.EndsWith("_wp4000", StringComparison.Ordinal)).ToDictionary(clip => clip.name);
            var mechanisms = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4000_Mechanism.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var empty = mechanisms["SG09R_0810_Mechanism"];
            var tactical = mechanisms["SG09R_0910_Mechanism"];
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolReloadBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainPistolReloadBank);
            }
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainPistolReloadBank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var machine = graph.layers[LeonAnimatorDriver.LayerWeaponAction].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var emptyState = states["Empty"];
            var standingEmpty = source["SG09R_0810"];
            AddReload(standingEmpty, empty, true, false);
            AddReload(source["SG09R_0910"], tactical, false, false);
            AddReload(source["LeonFireExtra_1810_wp4000"], empty, true, true);
            AddReload(source["LeonFireExtra_1910_wp4000"], tactical, false, true);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(bank);
            return (empty, tactical);

            void AddReload(AnimationClip raw, AnimationClip mechanism, bool emptyMagazine, bool crouched)
            {
                int frames = LeonAnimatorDriver.PistolReloadFrames;
                if (raw.frameRate != 60f || Mathf.RoundToInt(raw.length * raw.frameRate) != frames
                    || !Mathf.Approximately(raw.length, mechanism.length))
                {
                    throw new InvalidOperationException("SG-09 R 换弹片段与枪体轨道的帧范围不匹配。");
                }
                string name = LeonAnimatorDriver.PistolReloadStateName(emptyMagazine, crouched);
                var clip = new AnimationClip { name = name, frameRate = raw.frameRate };
                if (crouched && emptyMagazine)
                {
                    BakePistolCrouchEmptyGrip(standingEmpty, raw, mechanism, clip);
                }
                else
                {
                    var bindings = PistolAimBindings(raw);
                    AnimationUtility.SetEditorCurves(clip, bindings,
                        bindings.Select(binding => AnimationUtility.GetEditorCurve(raw, binding)).ToArray());
                }
                if (saved.TryGetValue(name, out var previous))
                {
                    EditorUtility.CopySerialized(clip, previous);
                    Object.DestroyImmediate(clip);
                    clip = previous;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(clip, bank);
                }
                EditorUtility.SetDirty(clip);
                int command = LeonAnimatorDriver.PistolReloadAction(emptyMagazine, crouched);
                if (!states.TryGetValue(name, out var state))
                {
                    state = machine.AddState(name, new Vector3(510f + (command - 20) * 275f, 600f));
                    var behavior = state.AddStateMachineBehaviour<WeaponActionSMB>();
                    behavior.actionType = WeaponActionType.Reload;
                    behavior.completeNormalizedTime = 0.99f;
                }
                state.motion = clip;
                state.writeDefaultValues = false;
                Link(emptyState, state, command, 0f);
                Link(state, emptyState, 0, 0f);
                EditorUtility.SetDirty(state);
            }
        }

        private static void BakePistolCrouchEmptyGrip(AnimationClip standing, AnimationClip crouched,
            AnimationClip mechanism, AnimationClip output)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            var sampled = new AnimationClip();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var model = Node(sample, PistolModelName);
                var grip = model.Find(PistolMechanismRoot + "_100");
                var arm = new ChapterOneArmAim.Arm(Node(sample, "arm left wrist"));
                var spine = Node(sample, "spine upper");
                var tracks = ArmBindings(crouched).Select(binding => binding.path).Distinct()
                    .Where(path => path.Contains("/arm left shoulder", StringComparison.Ordinal))
                    .Select(path => new AimJointTrack(path, sample.transform.Find(path)))
                    .Where(track => track.joint != null).ToArray();
                for (int frame = 0; frame <= LeonAnimatorDriver.PistolReloadFrames; frame++)
                {
                    float time = frame / crouched.frameRate;
                    float weight = Mathf.SmoothStep(0f, 1f, (frame - 59f) / 7f)
                        * Mathf.SmoothStep(1f, 0f, (frame - 144f) / 12f);
                    if (weight > 0f)
                    {
                        standing.SampleAnimation(sample, time);
                        mechanism.SampleAnimation(model.gameObject, time);
                        var offset = new Pose(grip.InverseTransformPoint(arm.wrist.position),
                            Quaternion.Inverse(grip.rotation) * arm.wrist.rotation);
                        crouched.SampleAnimation(sample, time);
                        arm.Solve(Vector3.Lerp(arm.wrist.position, grip.TransformPoint(offset.position), weight),
                            Quaternion.Slerp(arm.wrist.rotation, grip.rotation * offset.rotation, weight), -spine.right);
                    }
                    else
                    {
                        crouched.SampleAnimation(sample, time);
                    }
                    foreach (var track in tracks)
                    {
                        track.Record(time);
                    }
                }
                WriteRifleUpperTracks(sampled, tracks, crouched.frameRate);
                var nativeBindings = PistolAimBindings(crouched);
                AnimationUtility.SetEditorCurves(output, nativeBindings,
                    nativeBindings.Select(binding => AnimationUtility.GetEditorCurve(crouched, binding)).ToArray());
                var fixedBindings = AnimationUtility.GetCurveBindings(sampled);
                AnimationUtility.SetEditorCurves(output, fixedBindings,
                    fixedBindings.Select(binding => AnimationUtility.GetEditorCurve(sampled, binding)).ToArray());
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                Object.DestroyImmediate(sampled);
            }
        }
    }
}
#endif
