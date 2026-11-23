#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string LegBiteSourceFolder = "Assets/Animations/QTE/Expanded/倒地咬腿/";
        private const string LegBiteConnected = "旧动作过渡，已实战接入";
        private static readonly (string phase, string leon, string enemy)[] LegBiteSources =
        {
            ("Enter", "里昂_进入", "村民_进入"),
            ("Hold", "里昂_挣扎", "村民_保持"),
            ("Escape", "里昂_挣脱成功", "村民_被推开"),
            ("Failure", "里昂_咬腿失败", "村民_咬腿")
        };

        public static int RefreshLegacyLegBite()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式烘焙咬腿动画。");
            }
            var legacyLeon = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPath).GetComponentInChildren<Animator>().avatar;
            int count = 0;
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
            {
                bool player = role == "Leon";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                var instance = Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                Avatar temporary = null;
                try
                {
                    animator.avatar = legacyLeon;
                    if (!player)
                    {
                        temporary = BuildLegBiteAvatar(instance);
                        animator.avatar = temporary;
                    }
                    if (!animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                    {
                        throw new InvalidOperationException(role + " 缺少可采样的人形映射。");
                    }
                    animator.Rebind();
                    string rigName = "Armature";
                    string prefix = "LeonPair_LegBite";
                    if (!player)
                    {
                        rigName = role + "_Skeleton";
                        prefix = "GanadoPair_LegBite";
                    }
                    Transform rig = instance.transform.Find(rigName);
                    if (rig == null)
                    {
                        throw new InvalidOperationException(role + " 缺少目标骨架。");
                    }
                    string bankPath = Generated + "/Clips/" + role + "_Clips.asset";
                    var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
                    var existing = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
                    foreach (var phase in LegBiteSources)
                    {
                        string name = phase.leon;
                        if (!player)
                        {
                            name = phase.enemy;
                        }
                        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(LegBiteSourceFolder + name + ".anim");
                        if (!source || !source.humanMotion)
                        {
                            throw new InvalidOperationException("缺少过渡片段 " + name);
                        }
                        string id = prefix + phase.phase;
                        bool calibratedEnemy = !player && phase.phase == "Failure";
                        Store(BakeLegacyHumanoid(source, animator, rig, id, phase.phase == "Hold"), source, !calibratedEnemy);
                        if (calibratedEnemy)
                        {
                            using var fit = new LegBiteContactFit(instance);
                            Store(BakeLegacyHumanoid(source, animator, rig, id + "Fit", false, fit.Apply), source, true);
                        }
                    }
                    EditorUtility.SetDirty(bank);
                    AssetDatabase.SaveAssetIfDirty(bank);

                    void Store(AnimationClip clip, AnimationClip source, bool connected)
                    {
                        var root = ChapterOnePairMotion.ReadTrack(clip);
                        Vector3 delta = root.positions[^1] - root.positions[0];
                        string status = "旧动作原始重绑";
                        if (connected)
                        {
                            status = LegBiteConnected;
                        }
                        string contact = "";
                        if (connected && clip.name.Contains("Failure", StringComparison.Ordinal))
                        {
                            contact = "160";
                        }
                        Audit.Add(FormattableString.Invariant(
                            $"{role}\t{clip.name}\t{AssetDatabase.GetAssetPath(source)}\t{clip.length:F4}\t{clip.frameRate}\t{AnimationUtility.GetCurveBindings(clip).Length}\t0\t{delta.x:F3},{delta.z:F3}\t{contact}\t{status}"));
                        if (existing.TryGetValue(clip.name, out var previous))
                        {
                            EditorUtility.CopySerialized(clip, previous);
                            EditorUtility.SetDirty(previous);
                            Object.DestroyImmediate(clip);
                        }
                        else
                        {
                            AssetDatabase.AddObjectToAsset(clip, bank);
                        }
                        count++;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                    if (temporary)
                    {
                        Object.DestroyImmediate(temporary);
                    }
                }
            }
            WriteLegBiteStates();
            ChapterOnePairMotion.Refresh();
            return count;
        }

        private static void WriteLegBiteStates()
        {
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
            {
                var ids = new List<string>();
                foreach (var item in ChapterOneRootActor.LegBiteTakes)
                {
                    string id = item.player;
                    if (role != "Leon")
                    {
                        id = item.enemy;
                    }
                    ids.Add(id);
                }
                WriteLegacyStates(role, ids);
            }
        }

        private static void WriteLegacyStates(string role, IEnumerable<string> ids)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/" + role + "_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            if (role == "GanadoMale")
            {
                var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Generated + "/GanadoMale.overrideController");
                foreach (string id in ids)
                {
                    overrides[id] = clips[id];
                }
                EditorUtility.SetDirty(overrides);
                AssetDatabase.SaveAssetIfDirty(overrides);
                return;
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/" + role + ".controller");
            var machine = controller.layers[0].stateMachine;
            foreach (string id in ids)
            {
                var state = machine.states.Select(child => child.state).FirstOrDefault(existing => existing.name == id);
                if (state == null)
                {
                    int index = machine.states.Length;
                    state = machine.AddState(id, new Vector3(280 + index % 8 * 215, 145 + index / 8 * 62));
                }
                state.motion = clips[id];
                state.speedParameter = "DurationScale";
                state.speedParameterActive = true;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(machine);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        private static Avatar BuildLegBiteAvatar(GameObject instance)
        {
            var map = new Dictionary<string, string>
            {
                { "Hips", "Hip" }, { "Spine", "Spine_0" }, { "Chest", "Spine_1" },
                { "UpperChest", "Spine_2" }, { "Neck", "Neck_0" }, { "Head", "Head" }
            };
            foreach (var (side, prefix) in new[] { ("Left", "L_"), ("Right", "R_") })
            {
                foreach (var (human, native) in new[]
                {
                    ("UpperLeg", "Thigh"), ("LowerLeg", "Shin"), ("Foot", "Foot"), ("Toes", "Toe"),
                    ("Shoulder", "Shoulder"), ("UpperArm", "UpperArm"), ("LowerArm", "Forearm"), ("Hand", "Hand")
                })
                {
                    map.Add(side + human, prefix + native);
                }
                foreach (var (human, native) in new[]
                {
                    ("Thumb", "Thumb"), ("Index", "IndexF"), ("Middle", "MiddleF"), ("Ring", "RingF"), ("Little", "PinkyF")
                })
                {
                    map.Add(side + " " + human + " Proximal", prefix + native + "1");
                    map.Add(side + " " + human + " Intermediate", prefix + native + "2");
                    map.Add(side + " " + human + " Distal", prefix + native + "3");
                }
            }
            var skeleton = instance.GetComponentsInChildren<Transform>(true);
            return AvatarBuilder.BuildHumanAvatar(instance, new HumanDescription
            {
                human = map.Select(item => new HumanBone
                {
                    humanName = item.Key, boneName = item.Value, limit = new HumanLimit { useDefaultValues = true }
                }).ToArray(),
                skeleton = skeleton.Select(joint => new SkeletonBone
                {
                    name = joint.name, position = joint.localPosition, rotation = joint.localRotation, scale = joint.localScale
                }).ToArray(),
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f
            });
        }

        private sealed class LegBiteBoneTrack
        {
            public readonly Transform joint;
            public readonly string path;
            public readonly List<Keyframe>[] translation = CreateKeys(3);
            public readonly List<Keyframe>[] rotation = CreateKeys(4);
            private Quaternion previous = Quaternion.identity;

            public LegBiteBoneTrack(Transform root, Transform joint)
            {
                this.joint = joint;
                path = AnimationUtility.CalculateTransformPath(joint, root);
            }

            public void Sample(float time)
            {
                previous = AlignSign(previous, joint.localRotation);
                AddVector(translation, time, joint.localPosition);
                AddQuaternion(rotation, time, previous);
            }
        }

        private static AnimationClip BakeLegacyHumanoid(AnimationClip source, Animator animator, Transform rig,
            string id, bool loop, Action<int> fit = null, bool sharedStage = true, bool inPlace = false)
        {
            const float fps = 60f;
            Transform root = animator.transform;
            animator.Rebind();
            Transform leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Vector3 reference = Vector3.ProjectOnPlane(rightHip.position - leftHip.position, Vector3.up);
            var joints = new HashSet<Transform> { rig };
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                Transform joint = animator.GetBoneTransform((HumanBodyBones)i);
                while (joint != null && joint != root && joint.IsChildOf(rig))
                {
                    joints.Add(joint);
                    joint = joint.parent;
                }
            }
            var tracks = joints.Select(joint => new LegBiteBoneTrack(root, joint)).OrderBy(track => track.path).ToArray();
            Vector3 rigPosition = rig.localPosition;
            Quaternion rigRotation = rig.localRotation;
            var travel = CreateKeys(3);
            var turn = CreateKeys(4);
            Quaternion previous = Quaternion.identity;
            Pose first = default;
            int last = Mathf.RoundToInt(source.length * fps);
            for (int frame = 0; frame <= last; frame++)
            {
                float time = 0f;
                if (last > 0)
                {
                    // 均匀采样并保留准确片尾，避免浮点误差在同一源帧生成两个键。
                    time = source.length * (frame / (float)last);
                }
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rig.SetLocalPositionAndRotation(rigPosition, rigRotation);
                source.SampleAnimation(animator.gameObject, time);
                if (fit != null)
                {
                    fit(frame);
                }
                if (inPlace)
                {
                    root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
                Vector3 position = Vector3.ProjectOnPlane(root.position, Vector3.up);
                Quaternion rotation = root.rotation;
                if (sharedStage)
                {
                    Vector3 hips = Vector3.ProjectOnPlane(rightHip.position - leftHip.position, Vector3.up);
                    rotation = previous;
                    if (hips.sqrMagnitude > 0.0004f)
                    {
                        // 配对素材共用身体朝向。视觉姿势仍用采样时的世界骨架。
                        rotation = Quaternion.AngleAxis(Vector3.SignedAngle(reference, hips, Vector3.up), Vector3.up);
                    }
                    if (frame > 0)
                    {
                        rotation = Quaternion.RotateTowards(previous, rotation, 12f);
                    }
                }
                rotation = AlignSign(previous, rotation);
                previous = rotation;
                var rigWorld = new Pose(rig.position, rig.rotation);
                root.SetPositionAndRotation(position, rotation);
                rig.SetPositionAndRotation(rigWorld.position, rigWorld.rotation);
                if (!sharedStage)
                {
                    if (frame == 0)
                    {
                        first = new Pose(position, rotation);
                    }
                    position = Quaternion.Inverse(first.rotation) * (position - first.position);
                    rotation = Quaternion.Inverse(first.rotation) * rotation;
                }
                AddVector(travel, time, position);
                AddQuaternion(turn, time, rotation);
                foreach (var track in tracks)
                {
                    track.Sample(time);
                }
            }
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.SetLocalPositionAndRotation(rigPosition, rigRotation);

            var clip = new AnimationClip { name = id, frameRate = fps };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            void Add(string path, Type type, string property, List<Keyframe>[] keys)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    bindings.Add(EditorCurveBinding.FloatCurve(path, type, property + "." + "xyzw"[i]));
                    curves.Add(CollapseConstant(new AnimationCurve(keys[i].ToArray())));
                }
            }
            Add("", typeof(Animator), "RootT", travel);
            Add("", typeof(Animator), "RootQ", turn);
            foreach (var track in tracks)
            {
                Add(track.path, typeof(Transform), "m_LocalPosition", track.translation);
                Add(track.path, typeof(Transform), "m_LocalRotation", track.rotation);
            }
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private sealed class LegBiteContactFit : IDisposable
        {
            private readonly GameObject player;
            private readonly AnimationClip clip;
            private readonly ChapterOnePairMotion.Track root;
            private readonly Transform ankle;
            private readonly Transform knee;
            private readonly Transform enemy;
            private readonly Transform spine;
            private readonly Transform neck;
            private readonly Transform[] mouth;

            internal LegBiteContactFit(GameObject target)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
                player = Object.Instantiate(prefab);
                player.hideFlags = HideFlags.HideAndDontSave;
                player.GetComponent<Animator>().runtimeAnimatorController = null;
                clip = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset").OfType<AnimationClip>()
                    .Single(item => item.name == "LeonPair_LegBiteFailure");
                root = ChapterOnePairMotion.ReadTrack(clip);
                ankle = Node(player, "leg left ankle");
                knee = Node(player, "leg left knee");
                enemy = target.transform;
                spine = Node(target, "Spine_2");
                neck = Node(target, "Neck_0");
                mouth = new[] { Node(target, "L_U_MouthCorner"), Node(target, "R_U_MouthCorner"),
                    Node(target, "L_L_MouthCorner"), Node(target, "R_L_MouthCorner") };
            }

            internal void Apply(int frame)
            {
                float window = Mathf.Min(Mathf.InverseLerp(136f, 159f, frame), Mathf.InverseLerp(202f, 171f, frame));
                float weight = Mathf.SmoothStep(0f, 1f, window);
                if (weight <= 0f)
                {
                    return;
                }
                float time = frame / 60f;
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clip.SampleAnimation(player, time);
                Pose stage = root.Sample(time);
                player.transform.SetPositionAndRotation(stage.position, stage.rotation);
                Vector3 jaw = Mouth();
                Vector3 leg = knee.position - ankle.position;
                Vector3 shin = ankle.position + leg * Mathf.Clamp(Vector3.Dot(jaw - ankle.position, leg) / leg.sqrMagnitude, 0.12f, 0.88f);
                // 保留小腿表面的间隙，根轨只向接触方向补一次短扑。
                Vector3 surface = shin + (jaw - shin).normalized * 0.065f;
                Vector3 shift = Vector3.ProjectOnPlane(shin - jaw, Vector3.up);
                enemy.position += Vector3.ClampMagnitude(shift, 0.31f) * weight;
                Aim(spine, surface, 42f * weight);
                Aim(neck, surface, 23f * weight);
            }

            private void Aim(Transform joint, Vector3 target, float degrees)
            {
                var turn = Quaternion.FromToRotation(Mouth() - joint.position, target - joint.position);
                joint.rotation = Quaternion.RotateTowards(Quaternion.identity, turn, degrees) * joint.rotation;
            }

            private Vector3 Mouth() =>
                (mouth[0].position + mouth[1].position + mouth[2].position + mouth[3].position) * 0.25f;

            public void Dispose()
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
#endif
