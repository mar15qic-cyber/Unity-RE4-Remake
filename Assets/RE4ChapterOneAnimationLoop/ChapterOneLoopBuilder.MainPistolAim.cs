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
        private const float MainPistolRaiseSpeed = 1f;
        private const string MainPistolAimBank = Generated + "/MainLeonPistolAimMotions.asset";

        public static void RefreshMainLeonPistolAim()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新手枪举枪。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            AddMainPistolAimMotions(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        private static bool BindMainPistolAim(LeonController leon, Vector3 aimForward, AnimationClip ready, AnimationClip empty)
        {
            var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(item => item.data != null && item.data.weaponType == WeaponType.Pistol);
            var rig = new SerializedObject(leon.GetComponent<LeonAnimRig>());
            rig.FindProperty("nativePistolWeapon").objectReferenceValue = weapon;
            rig.FindProperty("nativePistolAimForward").vector3Value = aimForward;
            rig.FindProperty("nativePistolReadyMechanism").objectReferenceValue = ready;
            rig.FindProperty("nativePistolEmptyMechanism").objectReferenceValue = empty;
            rig.FindProperty("nativeCqbrAimSpine").objectReferenceValue = Node(leon.gameObject, "spine upper");
            bool changed = rig.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            }
            return changed;
        }

        private static (Vector3 aimForward, AnimationClip ready, AnimationClip empty) AddMainPistolAimMotions(AnimatorController graph)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Where(clip => clip.name.StartsWith("SG09R_", StringComparison.Ordinal))
                .ToDictionary(clip => clip.name);
            var mechanisms = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4000_Mechanism.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var ready = mechanisms["SG09R_0160_Mechanism"];
            var empty = mechanisms["SG09R_0160_wp4000h_Mechanism"];
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolAimBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainPistolAimBank);
            }
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainPistolAimBank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var layers = graph.layers;
            int layer = LeonAnimatorDriver.LayerRifleCrouch;
            var machine = layers[layer].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var model = Node(prefab, PistolModelName);
            string modelPath = AnimationUtility.CalculateTransformPath(model, prefab.transform);
            layers[layer].avatarMask = AddMainWeaponMaskPaths(layers[layer].avatarMask, MainW870PoseMask, prefab, model);
            graph.layers = layers;

            var idle = (AnimationClip)layers[0].stateMachine.states.Single(child => child.state.name == "NativePistolIdle").state.motion;
            var hold = source["SG09R_0160"];
            var preview = EditorSceneManager.NewPreviewScene();
            var raise = new AnimationClip();
            var lower = new AnimationClip();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var held = Node(sample, PistolModelName);
                var spine = Node(sample, "spine upper");
                var muzzle = Node(held.gameObject, "RE4_Muzzle");
                var left = new ChapterOneArmAim.Arm(Node(sample, "arm left wrist"));
                hold.SampleAnimation(sample, 0f);
                var aimForward = spine.InverseTransformDirection(muzzle.forward);
                var grip = new Pose(held.InverseTransformPoint(left.wrist.position), Quaternion.Inverse(held.rotation) * left.wrist.rotation);

                BakeStance(source["SG09R_0140"], raise, true);
                BakeStance(source["SG09R_0158"], lower, false);
                AddPose(PistolAimPose.Raise, raise, speed: MainPistolRaiseSpeed);
                AddPose(PistolAimPose.Lower, lower);
                AddPose(PistolAimPose.ReverseRaise, raise, reverse: true, speed: MainPistolRaiseSpeed);
                AddPose(PistolAimPose.ReverseLower, lower, reverse: true);
                AddPose(PistolAimPose.Hold, hold, holdPose: true);
                AddPose(PistolAimPose.Shot, source["SG09R_0500"], mechanisms["SG09R_0500_Mechanism"]);
                AddPose(PistolAimPose.LastShot, source["SG09R_0500_wp4000h"], mechanisms["SG09R_0500_wp4000h_Mechanism"]);
                AddPose(PistolAimPose.DryFire, source["SG09R_0520"], mechanisms["SG09R_0520_Mechanism"]);
                InstallStationaryPistolBreath(graph);
                EditorUtility.SetDirty(machine);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(bank);
                return (aimForward, ready, empty);

                void BakeStance(AnimationClip raw, AnimationClip output, bool raising)
                {
                    var tracks = PistolAimBindings(raw).Select(binding => binding.path).Distinct()
                        .Select(path => new AimJointTrack(path, sample.transform.Find(path)))
                        .Where(track => track.joint != null).ToArray();
                    idle.SampleAnimation(sample, 0f);
                    var idlePose = tracks.Select(track => track.LocalPose).ToArray();
                    hold.SampleAnimation(sample, 0f);
                    var holdPose = tracks.Select(track => track.LocalPose).ToArray();
                    int frames = Mathf.RoundToInt(raw.length * raw.frameRate);
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float time = frame / raw.frameRate;
                        raw.SampleAnimation(sample, time);
                        float idleWeight;
                        float holdWeight;
                        float supportWeight;
                        if (raising)
                        {
                            // 起始混合只覆盖低位交接，保留原版中段抬枪轨迹。
                            idleWeight = Mathf.SmoothStep(1f, 0f, frame / 7f);
                            holdWeight = Mathf.SmoothStep(0f, 1f, (frame - frames + 8f) / 8f);
                            supportWeight = holdWeight;
                        }
                        else
                        {
                            idleWeight = Mathf.SmoothStep(0f, 1f, (frame - 16f) / (frames - 20f));
                            holdWeight = Mathf.SmoothStep(1f, 0f, frame / 8f);
                            supportWeight = Mathf.SmoothStep(1f, 0f, (frame - 14f) / 14f);
                        }
                        for (int index = 0; index < tracks.Length; index++)
                        {
                            var joint = tracks[index].joint;
                            var position = Vector3.Lerp(joint.localPosition, idlePose[index].position, idleWeight);
                            var rotation = Quaternion.Slerp(joint.localRotation, idlePose[index].rotation, idleWeight);
                            joint.SetLocalPositionAndRotation(Vector3.Lerp(position, holdPose[index].position, holdWeight),
                                Quaternion.Slerp(rotation, holdPose[index].rotation, holdWeight));
                        }
                        if (supportWeight > 0f)
                        {
                            left.Solve(Vector3.Lerp(left.wrist.position, held.TransformPoint(grip.position), supportWeight),
                                Quaternion.Slerp(left.wrist.rotation, held.rotation * grip.rotation, supportWeight), -spine.right);
                        }
                        foreach (var track in tracks)
                        {
                            track.Record(time);
                        }
                    }
                    WriteRifleUpperTracks(output, tracks, raw.frameRate);
                }

                void AddPose(PistolAimPose pose, AnimationClip body, AnimationClip mechanism = null,
                    bool holdPose = false, bool reverse = false, float speed = 1f)
                {
                    string name = LeonAnimatorDriver.PistolAimStateName(pose);
                    var clip = new AnimationClip { name = name, frameRate = body.frameRate };
                    var bindings = PistolAimBindings(body);
                    var curves = bindings.Select(binding =>
                    {
                        var curve = AnimationUtility.GetEditorCurve(body, binding);
                        if (holdPose)
                        {
                            return AnimationCurve.Constant(0f, 1f, curve.Evaluate(0f));
                        }
                        if (reverse)
                        {
                            return new AnimationCurve(curve.keys.Reverse().Select(key =>
                                new Keyframe(body.length - key.time, key.value, -key.outTangent, -key.inTangent)).ToArray());
                        }
                        return curve;
                    }).ToArray();
                    AnimationUtility.SetEditorCurves(clip, bindings, curves);
                    if (mechanism != null)
                    {
                        if (!Mathf.Approximately(body.length, mechanism.length))
                        {
                            throw new InvalidOperationException("SG-09 R 动作与套筒轨道的帧范围不匹配。");
                        }
                        var gunBindings = AnimationUtility.GetCurveBindings(mechanism);
                        var gunCurves = gunBindings.Select(binding => AnimationUtility.GetEditorCurve(mechanism, binding)).ToArray();
                        for (int index = 0; index < gunBindings.Length; index++)
                        {
                            string path = gunBindings[index].path;
                            gunBindings[index].path = modelPath;
                            if (path.Length != 0)
                            {
                                gunBindings[index].path += "/" + path;
                            }
                        }
                        AnimationUtility.SetEditorCurves(clip, gunBindings, gunCurves);
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
                    if (!states.TryGetValue(name, out var state))
                    {
                        int index = (int)pose;
                        state = machine.AddState(name, new Vector3(780 + index % 4 * 230, 640 + index / 4 * 85));
                    }
                    state.motion = clip;
                    state.speed = speed;
                    state.writeDefaultValues = false;
                    EditorUtility.SetDirty(state);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                Object.DestroyImmediate(raise);
                Object.DestroyImmediate(lower);
            }
        }

        private static EditorCurveBinding[] PistolAimBindings(AnimationClip clip)
        {
            return ArmBindings(clip).Concat(AnimationUtility.GetCurveBindings(clip).Where(binding =>
                binding.propertyName.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                && (binding.path.EndsWith("/spine lower", StringComparison.Ordinal)
                    || binding.path.EndsWith("/spine middle", StringComparison.Ordinal)
                    || binding.path.EndsWith("/spine upper", StringComparison.Ordinal)))).ToArray();
        }
    }
}
#endif
