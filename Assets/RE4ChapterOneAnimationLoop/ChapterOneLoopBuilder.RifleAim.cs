#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private static void BuildNativeCqbrDraw(GameObject rig, AnimationClip carry, AnimationClip result)
        {
            var native = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Native/CQBRNativeDraw.fbx")
                .OfType<AnimationClip>().Single(clip => clip.name == "cha0_wp4402H_slot543_put_out");
            var source = new AnimationClip { frameRate = native.frameRate };
            try
            {
                const string sourceRoot = "Leon_Skeleton/";
                var bindings = AnimationUtility.GetCurveBindings(native).Where(binding =>
                    binding.type == typeof(Transform) && binding.path.StartsWith(sourceRoot, StringComparison.Ordinal)).ToArray();
                var curves = new AnimationCurve[bindings.Length];
                for (int index = 0; index < bindings.Length; index++)
                {
                    var binding = bindings[index];
                    var curve = AnimationUtility.GetEditorCurve(native, binding);
                    if (binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                    {
                        var keys = curve.keys;
                        for (int key = 0; key < keys.Length; key++)
                        {
                            keys[key].value *= 100f;
                            keys[key].inTangent *= 100f;
                            keys[key].outTangent *= 100f;
                        }
                        curve.keys = keys;
                    }
                    curves[index] = curve;
                    bindings[index].path = "Armature/" + binding.path.Substring(sourceRoot.Length);
                }
                AnimationUtility.SetEditorCurves(source, bindings, curves);
                var tracks = RifleUpperTracks(rig, source);
                carry.SampleAnimation(rig, 0f);
                var held = tracks.Select(track => track.LocalPose).ToArray();
                var leftArm = tracks.Select(track => track.path.Contains("/arm left shoulder")).ToArray();
                var leftFingers = tracks.Select(track => track.path.Contains("/arm left finger")).ToArray();
                var rightFingers = tracks.Select(track => track.path.Contains("/arm right finger")).ToArray();
                var rightArm = tracks.Select(track => track.path.Contains("/arm right shoulder")).ToArray();
                var rightWrist = Node(rig, "arm right wrist");
                var forearm = rightWrist.parent;
                var upperArm = forearm.parent;
                var rightShoulder = Node(rig, "arm right shoulder");
                var leftShoulder = Node(rig, "arm left shoulder");
                int frames = Mathf.RoundToInt(source.length * source.frameRate);
                if (frames != ChapterOneRootActor.RifleDrawFrames)
                {
                    throw new InvalidOperationException("CQBR 拔枪片段与槽位 543 的源帧数不符。");
                }
                for (int frame = 0; frame <= frames; frame++)
                {
                    const int sourceGripReady = 30;
                    float sourceFrame = frame;
                    int pickup = ChapterOneRootActor.RifleDrawVisibleFrame;
                    int ready = ChapterOneRootActor.RifleGripReadyFrame;
                    if (frame > pickup && frame < ready)
                    {
                        // 让枪离背和回背时减速，已握稳的静置帧留给持枪状态。
                        sourceFrame = Mathf.SmoothStep(pickup, sourceGripReady, Mathf.InverseLerp(pickup, ready, frame));
                    }
                    else if (frame >= ready)
                    {
                        sourceFrame = Mathf.Lerp(sourceGripReady, frames, Mathf.InverseLerp(ready, frames, frame));
                    }
                    source.SampleAnimation(rig, sourceFrame / source.frameRate);
                    // 先由原作右臂取枪，再一起过渡到双手持枪；枪始终使用持枪素材中的掌内挂点。
                    float rightWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(19f, 30f, sourceFrame));
                    float leftWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 28f, sourceFrame));
                    float rightFingerWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 11f, sourceFrame));
                    float fingerWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 21f, sourceFrame));
                    for (int index = 0; index < tracks.Length; index++)
                    {
                        float weight = 0f;
                        if (tracks[index].joint.name == "RE4_Grip")
                        {
                            weight = 1f;
                        }
                        else if (rightFingers[index])
                        {
                            weight = rightFingerWeight;
                        }
                        else if (leftFingers[index])
                        {
                            weight = fingerWeight;
                        }
                        else if (leftArm[index])
                        {
                            weight = leftWeight;
                        }
                        else if (rightArm[index])
                        {
                            weight = rightWeight;
                        }
                        var joint = tracks[index].joint;
                        joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.localPosition, held[index].position, weight),
                            Quaternion.Slerp(joint.localRotation, held[index].rotation, weight));
                    }

                    // 从背中部取枪时抬起上臂并屈肘。腕部和各段骨长完全沿用原片。
                    float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, pickup, sourceFrame));
                    reach *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pickup, 24f, sourceFrame));
                    if (reach > 0f)
                    {
                        Vector3 bend = Vector3.Cross(forearm.position - upperArm.position, rightWrist.position - forearm.position);
                        Vector3 across = (rightShoulder.position - leftShoulder.position).normalized;
                        Vector3 forward = Vector3.Cross(across, rig.transform.up).normalized;
                        Vector3 liftAxis = (0.763f * across - 0.635f * rig.transform.up + 0.121f * forward).normalized;
                        // 右肩胛随取枪轻抬并回转，枪托收进右肩轮廓；前臂旋转时手与枪一起运动。
                        Vector3 forearmAxis = (rightWrist.position - forearm.position).normalized;
                        Quaternion turnForearm = Quaternion.AngleAxis(15f * reach, forearmAxis);
                        Quaternion bendElbow = Quaternion.AngleAxis(44.75f * reach, bend.normalized);
                        forearm.rotation = bendElbow * turnForearm * forearm.rotation;
                        upperArm.rotation = Quaternion.AngleAxis(48f * reach, liftAxis) * upperArm.rotation;
                        Quaternion liftShoulder = Quaternion.AngleAxis(22f * reach, forward);
                        Quaternion turnShoulder = Quaternion.AngleAxis(22f * reach, rig.transform.up);
                        rightShoulder.rotation = turnShoulder * liftShoulder * rightShoulder.rotation;
                    }
                    foreach (var track in tracks)
                    {
                        track.Record(frame / source.frameRate);
                    }
                }
                WriteRifleUpperTracks(result, tracks, source.frameRate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static AnimationCurve ResampleTiming(AnimationCurve original, float[] sourceTimes, float fps)
        {
            if (original.length == 2 && original[0].value == original[1].value
                && original[0].outTangent == 0f && original[1].inTangent == 0f)
            {
                return original;
            }
            int last = sourceTimes.Length - 1;
            var values = new float[last + 1];
            var keys = new Keyframe[last + 1];
            for (int frame = 0; frame <= last; frame++)
            {
                values[frame] = original.Evaluate(sourceTimes[frame]);
            }
            for (int frame = 0; frame <= last; frame++)
            {
                int before = Mathf.Max(0, frame - 1);
                int after = Mathf.Min(last, frame + 1);
                float tangent = (values[after] - values[before]) * fps / (after - before);
                keys[frame] = new Keyframe(frame / fps, values[frame], tangent, tangent);
            }
            return CollapseConstant(new AnimationCurve(keys));
        }

        private sealed class AimJointTrack
        {
            public readonly string path;
            public readonly Transform joint;
            public readonly List<Keyframe>[] position = CreateKeys(3);
            public readonly List<Keyframe>[] rotation = CreateKeys(4);
            private Quaternion previous = Quaternion.identity;

            public AimJointTrack(string path, Transform joint)
            {
                this.path = path;
                this.joint = joint;
            }

            public Pose LocalPose => new(joint.localPosition, joint.localRotation);

            public void Record(float time)
            {
                AddVector(position, time, joint.localPosition);
                previous = AlignSign(previous, joint.localRotation);
                AddQuaternion(rotation, time, previous);
            }
        }

        private static Dictionary<string, AnimationClip> BakeCqbrAimTransitions(AnimatorController controller,
            GameObject sourceRig, Dictionary<string, AnimationClip> bank, AnimationClip carry)
        {
            var saved = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject rig;
                if (PrefabUtility.IsPartOfPrefabAsset(sourceRig))
                {
                    rig = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig, scene);
                }
                else
                {
                    rig = UnityEngine.Object.Instantiate(sourceRig);
                    SceneManager.MoveGameObjectToScene(rig, scene);
                }
                rig.GetComponent<Animator>().enabled = false;
                rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var wristBones = rig.GetComponentsInChildren<Transform>(true);
                var left = Array.Find(wristBones, joint => joint.name == "arm left wrist");
                var right = Array.Find(wristBones, joint => joint.name == "arm right wrist");
                if (left == null || right == null)
                {
                    throw new InvalidOperationException("CQBR 进退瞄校准缺少 LeonR4R 手腕骨骼。");
                }
                var arm = new ChapterOneArmAim.Arm(left);
                var aim = bank["CQBR_0160"];
                var result = new Dictionary<string, AnimationClip>();
                Bake("CQBR_0140", carry, aim, true);
                Bake("CQBR_0158", aim, carry, false);
                var rifle = Array.Find(wristBones, joint => joint.name == "wp4402_Model");
                if (rifle == null)
                {
                    throw new InvalidOperationException("CQBR 校准缺少步枪模型。");
                }
                var draw = GetClip("CQBR_0543_DrawTiming");
                BuildNativeCqbrDraw(rig, carry, draw);
                result.Add("CQBR_0543", draw);
                foreach (string id in new[] { "CQBR_0500", "CQBR_0500_wp4202h" })
                {
                    var source = bank[id];
                    var clip = GetClip(id + "_BurstCalibrated");
                    int frames = Mathf.RoundToInt(source.length * source.frameRate);
                    BakeRifleShot(rig, source, aim, clip, arm, rifle, frames);
                    result.Add(id, clip);
                }
                return result;

                void Bake(string id, AnimationClip entry, AnimationClip exit, bool smoothRaise)
                {
                    AnimationClip source = bank[id];
                    var calibrated = GetClip(id + "_GripCalibrated");
                    BakeRifleAimClip(rig, source, entry, exit, calibrated, arm, right, smoothRaise);
                    result.Add(id, calibrated);
                }

                AnimationClip GetClip(string clipName)
                {
                    if (!saved.TryGetValue(clipName, out var calibrated))
                    {
                        calibrated = new AnimationClip { name = clipName };
                        AssetDatabase.AddObjectToAsset(calibrated, controller);
                    }
                    return calibrated;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void BakeRifleShot(GameObject rig, AnimationClip source, AnimationClip reference,
            AnimationClip result, ChapterOneArmAim.Arm left, Transform rifle, int last)
        {
            var tracks = RifleUpperTracks(rig, source);
            reference.SampleAnimation(rig, 0f);
            var heldPose = tracks.Select(track => track.LocalPose).ToArray();
            Vector3 gripPoint = rifle.InverseTransformPoint(left.wrist.position);
            Quaternion gripRotation = Quaternion.Inverse(rifle.rotation) * left.wrist.rotation;
            float fps = source.frameRate;
            if (fps != 60f || last != 30)
            {
                throw new InvalidOperationException("CQBR 开火片段与后坐采样的源帧范围不匹配。");
            }
            for (int frame = 0; frame <= last; frame++)
            {
                float sample = ChapterOneRootActor.RifleRecoilSourceFrame(frame);
                float rise = Mathf.SmoothStep(0f, 1f, (frame - 1f) / 5f);
                float settle = Mathf.SmoothStep(0f, 1f, (last - frame) / 12f);
                float hold = 1f - 0.55f * rise * settle;
                source.SampleAnimation(rig, sample / fps);
                if (hold > 0f)
                {
                    for (int i = 0; i < tracks.Length; i++)
                    {
                        var joint = tracks[i].joint;
                        joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.localPosition, heldPose[i].position, hold),
                            Quaternion.Slerp(joint.localRotation, heldPose[i].rotation, hold));
                    }
                    if (hold < 1f)
                    {
                        left.Solve(rifle.TransformPoint(gripPoint), rifle.rotation * gripRotation,
                            -rig.transform.right - rig.transform.up);
                    }
                }
                foreach (var track in tracks)
                {
                    track.Record(frame / fps);
                }
            }
            WriteRifleUpperTracks(result, tracks, fps);
        }

        private static AimJointTrack[] RifleUpperTracks(GameObject rig, AnimationClip source) =>
            AnimationUtility.GetCurveBindings(source)
                .Where(binding => binding.type == typeof(Transform) && IsUpperBodyPath(binding.path))
                .Select(binding => binding.path).Distinct()
                .Select(path => new AimJointTrack(path, rig.transform.Find(path)))
                .Where(track => track.joint != null).ToArray();

        private static void BakeRifleAimClip(GameObject rig, AnimationClip source, AnimationClip entry,
            AnimationClip exit, AnimationClip result, ChapterOneArmAim.Arm left, Transform right, bool smoothRaise)
        {
            var tracks = RifleUpperTracks(rig, source);
            Transform hips = rig.transform.Find("Armature/root ground/root hips");

            source.SampleAnimation(rig, 0f);
            var rawStart = tracks.Select(track => track.LocalPose).ToArray();
            entry.SampleAnimation(rig, 0f);
            var start = tracks.Select(track => track.LocalPose).ToArray();
            Pose startGrip = ReadGrip();
            Quaternion startWeapon = Quaternion.Inverse(hips.rotation) * right.rotation;
            source.SampleAnimation(rig, source.length);
            var rawEnd = tracks.Select(track => track.LocalPose).ToArray();
            exit.SampleAnimation(rig, 0f);
            var end = tracks.Select(track => track.LocalPose).ToArray();
            Pose endGrip = ReadGrip();
            Quaternion endWeapon = Quaternion.Inverse(hips.rotation) * right.rotation;

            int frames = Mathf.RoundToInt(source.length * source.frameRate);
            for (int frame = 0; frame <= frames; frame++)
            {
                float progress = (float)frame / frames;
                float time = source.length * progress;
                float ease = Mathf.SmoothStep(0f, 1f, progress);
                source.SampleAnimation(rig, time);
                for (int i = 0; i < tracks.Length; i++)
                {
                    var joint = tracks[i].joint;
                    Vector3 positionOffset = Vector3.Lerp(start[i].position - rawStart[i].position,
                        end[i].position - rawEnd[i].position, ease);
                    Quaternion rotationOffset = Quaternion.Slerp(start[i].rotation * Quaternion.Inverse(rawStart[i].rotation),
                        end[i].rotation * Quaternion.Inverse(rawEnd[i].rotation), ease);
                    joint.SetLocalPositionAndRotation(joint.localPosition + positionOffset, rotationOffset * joint.localRotation);
                }
                if (frame > 0 && frame < frames)
                {
                    if (smoothRaise)
                    {
                        // 旧持枪姿态下枪口更低，举枪转角从当前姿势连续走向瞄准姿势。
                        right.rotation = hips.rotation * Quaternion.Slerp(startWeapon, endWeapon, ease);
                    }
                    // 保留原版肘部弯曲方向，把托握接触烘焙到左臂与手腕轨道。
                    Vector3 grip = Vector3.Lerp(startGrip.position, endGrip.position, ease);
                    Quaternion gripRotation = Quaternion.Slerp(startGrip.rotation, endGrip.rotation, ease);
                    left.Solve(right.position + right.rotation * grip, right.rotation * gripRotation,
                        -rig.transform.right - rig.transform.up);
                }
                foreach (var track in tracks)
                {
                    track.Record(time);
                }
            }

            WriteRifleUpperTracks(result, tracks, source.frameRate);

            Pose ReadGrip()
            {
                Quaternion toRight = Quaternion.Inverse(right.rotation);
                return new Pose(toRight * (left.wrist.position - right.position), toRight * left.wrist.rotation);
            }
        }

        private static void WriteRifleUpperTracks(AnimationClip result, AimJointTrack[] tracks, float fps, bool replaceCurves = true)
        {
            var bindings = new List<EditorCurveBinding>(tracks.Length * 7);
            var curves = new List<AnimationCurve>(bindings.Capacity);
            foreach (var track in tracks)
            {
                AddTrack(track.path, "m_LocalPosition", track.position);
                AddTrack(track.path, "m_LocalRotation", track.rotation);
            }
            if (replaceCurves)
            {
                result.ClearCurves();
            }
            result.frameRate = fps;
            AnimationUtility.SetEditorCurves(result, bindings.ToArray(), curves.ToArray());
            result.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(result);

            void AddTrack(string path, string property, List<Keyframe>[] channels)
            {
                const string components = "xyzw";
                for (int i = 0; i < channels.Length; i++)
                {
                    bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property + "." + components[i]));
                    curves.Add(CollapseConstant(new AnimationCurve(channels[i].ToArray())));
                }
            }
        }
    }
}
#endif
