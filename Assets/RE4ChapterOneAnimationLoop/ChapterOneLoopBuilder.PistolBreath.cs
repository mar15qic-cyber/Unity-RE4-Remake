#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static void RefreshStationaryPistolBreath()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式更新手枪瞄准呼吸。");
            InstallStationaryPistolBreath(AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController));
        }

        private static void InstallStationaryPistolBreath(AnimatorController graph)
        {
            InstallStationaryGunBreath(graph, "PistolAimBreath", "SG09R_0163", LeonAnimatorDriver.PistolAimStateName(PistolAimPose.Hold),
                LeonAnimatorDriver.PistolBreathStateName, LeonAnimatorDriver.PistolBreathLayerName, "MainLeonStationaryPistolBreath");
            BakeStandingPistolCameraBreath();
        }

        public static void BakeStandingPistolCameraBreath()
            => BakeStandingGunCameraBreath("SG09R", "PistolAimBreath");

        public static void BakeStandingW870CameraBreath()
            => BakeStandingGunCameraBreath("W870", "W870AimBreath");

        private static void BakeStandingGunCameraBreath(string weapon, string source)
        {
            var settings = AssetDatabase.LoadAssetAtPath<NativeShoulderCameraSettings>($"Assets/Settings/Camera/NativeShoulder_{weapon}.asset");
            string path = $"{Art}/{source}.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == weapon + "_0163");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), preview);
                var chest = Node(model, "spine upper");
                var elbow = Node(model, "arm right forearm");
                var wrist = Node(model, "arm right wrist");
                Vector3 Forearm() => chest.InverseTransformDirection((wrist.position - elbow.position).normalized);
                clip.SampleAnimation(model, 0f);
                var forward = Forearm();
                var right = Vector3.Cross(chest.InverseTransformDirection(Vector3.up), forward).normalized;
                var up = Vector3.Cross(forward, right);
                int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
                var samples = new Vector2[frames + 1];
                var mean = Vector2.zero;
                for (int i = 0; i < frames; i++)
                {
                    clip.SampleAnimation(model, i / clip.frameRate);
                    var d = Forearm();
                    float depth = Vector3.Dot(d, forward);
                    samples[i] = new Vector2(Mathf.Atan2(Vector3.Dot(d, right), depth), Mathf.Atan2(Vector3.Dot(d, up), depth)) * Mathf.Rad2Deg;
                    mean += samples[i];
                }
                mean /= frames;
                for (int i = 0; i < frames; i++) samples[i] -= mean;
                samples[frames] = samples[0];
                settings.SetForearmBreathSamples(samples);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static void RefreshStationaryW870Breath()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式更新霰弹枪瞄准呼吸。");
            InstallStationaryW870Breath(AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController));
        }

        private static void InstallStationaryW870Breath(AnimatorController graph)
        {
            InstallStationaryGunBreath(graph, "W870AimBreath", "W870_0163", LeonAnimatorDriver.W870PoseName(W870Pose.Aim),
                LeonAnimatorDriver.W870BreathStateName, LeonAnimatorDriver.W870BreathLayerName, "MainLeonStationaryW870Breath");
            BakeStandingW870CameraBreath();
        }

        private static void InstallStationaryGunBreath(AnimatorController graph, string sourceFile, string take, string holdState,
            string stateName, string layerName, string assetName)
        {
            string sourcePath = Art + "/" + sourceFile + ".fbx";
            string targetPath = Generated + "/" + assetName + ".anim";
            var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Single(c => c.name == take);
            var idle = (AnimationClip)graph.layers[0].stateMachine.states.Single(s => s.state.name == LeonAnimatorDriver.NativeIdleName).state.motion;
            var hold = (AnimationClip)graph.layers[LeonAnimatorDriver.LayerRifleCrouch].stateMachine.states
                .Single(s => s.state.name == holdState).state.motion;
            var output = new AnimationClip { name = stateName };
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var raw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath), preview);
                var target = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab"), preview);
                var names = new List<string> { "root hips", "spine lower", "spine middle", "spine upper", "head neck lower", "head neck upper", "head" };
                foreach (string side in new[] { "left", "right" })
                {
                    names.AddRange(new[] { "shoulder", "upperarm", "forearm", "wrist" }.Select(j => "arm " + side + " " + j));
                    names.AddRange(new[] { "thigh", "knee", "ankle", "toes" }.Select(j => "leg " + side + " " + j));
                }
                source.SampleAnimation(raw, 0f);
                idle.SampleAnimation(target, 0f);
                hold.SampleAnimation(target, 0f);
                var bones = names.Select(name =>
                {
                    var joint = Node(target, name);
                    var native = Node(raw, name);
                    return (track: new AimJointTrack(AnimationUtility.CalculateTransformPath(joint, target.transform), joint),
                        native, reference: new Pose(native.localPosition, native.localRotation),
                        calibrated: new Pose(joint.localPosition, joint.localRotation));
                }).ToArray();
                var spine = Node(target, "spine upper");
                var right = Node(target, "arm right wrist");
                var left = new ChapterOneArmAim.Arm(Node(target, "arm left wrist"));
                var wristAim = Quaternion.Inverse(spine.rotation) * right.rotation;
                var support = new Pose(right.InverseTransformPoint(left.wrist.position), Quaternion.Inverse(right.rotation) * left.wrist.rotation);
                var soles = new[] { "leg left ankle", "leg right ankle" }.Select(name =>
                {
                    var foot = Node(target, name);
                    return (leg: new ChapterOneArmAim.Arm(foot), planted: new Pose(foot.position, foot.rotation));
                }).ToArray();

                int count = Mathf.RoundToInt(source.length * source.frameRate);
                // 与 Leon 的 Generic 重绑相同，FBX 的骨骼局部位移在 Armature 下使用厘米。
                const float leonPositionScale = 100f;
                for (int frame = 0; frame <= count; frame++)
                {
                    float time = frame / source.frameRate;
                    source.SampleAnimation(raw, time);
                    foreach (var b in bones)
                    {
                        b.track.joint.SetLocalPositionAndRotation(b.calibrated.position + (b.native.localPosition - b.reference.position) * leonPositionScale,
                            b.calibrated.rotation * (Quaternion.Inverse(b.reference.rotation) * b.native.localRotation));
                    }
                    // 右手维持已校准的枪口方向，左手跟随握点，骨盆呼吸时两脚仍留在支撑点。
                    right.rotation = spine.rotation * wristAim;
                    left.Solve(right.TransformPoint(support.position), right.rotation * support.rotation, -spine.right);
                    foreach (var sole in soles)
                        sole.leg.Solve(sole.planted.position, sole.planted.rotation, target.transform.forward);
                    foreach (var b in bones)
                        b.track.Record(time);
                }
                WriteRifleUpperTracks(output, bones.Select(b => b.track).ToArray(), source.frameRate);
                var settings = AnimationUtility.GetAnimationClipSettings(output);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(output, settings);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            if (saved == null)
            {
                saved = output;
                AssetDatabase.CreateAsset(saved, targetPath);
            }
            else
            {
                EditorUtility.CopySerialized(output, saved);
                UnityEngine.Object.DestroyImmediate(output);
                EditorUtility.SetDirty(saved);
            }
            if (graph.layers.All(l => l.name != layerName))
                graph.AddLayer(layerName);
            var layers = graph.layers;
            var layer = layers.Single(l => l.name == layerName);
            layer.defaultWeight = 0f;
            var machine = layer.stateMachine;
            var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == stateName) ?? machine.AddState(stateName);
            state.motion = saved;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            graph.layers = layers;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(saved);
            AssetDatabase.SaveAssetIfDirty(graph);
        }
    }
}
#endif
