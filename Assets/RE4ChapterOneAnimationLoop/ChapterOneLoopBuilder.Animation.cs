#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private static readonly string[] ContextualWeaponClips =
        {
            "SG09R_0541", "SG09R_1541", "SG09R_1551",
            "W870_0541", "W870_1541", "W870_1551", "Knife_1551"
        };
        private static readonly string[] MovementTurns =
        {
            "LeonMove_0530", "LeonMove_0532", "LeonMove_0534", "LeonMove_0536"
        };
        private static readonly string[] GanadoMeleeClips =
        {
            "Ganado_1015", "Ganado_1016", "Ganado_1100", "Ganado_1101", "Ganado_1105", "Ganado_1106", "Ganado_3011"
        };
        private static readonly string[] GanadoWallClips =
        {
            "Ganado_0620", "Ganado_0621", "Ganado_1410", "Ganado_1411"
        };

        private static List<ChapterOneRootActor.Take> BuildTakes(string role, GameObject rig, string[][] rows,
            ChapterOneBakedClips library, Dictionary<string, AnimationClip> existingClips)
        {
            string group = role == "GanadoMale" ? "Ganado" : role;
            var results = new List<ChapterOneRootActor.Take>();
            foreach (string[] row in rows)
            {
                if (group == "Ganado" && row[0] == "Ganado_1100")
                {
                    continue;
                }
                if (row[4] == group)
                    Add(row[0], row[1], FileForRig(row[3]), row[7] == "True", row[5], row[9], row[16], row[10], row[8]);
                if (row[12] == group && row[4] != group)
                    Add(row[10], row[1], FileForRig(row[11]), false, row[13], "", "", "", "");
            }
            if (group == "Ganado")
            {
                Add("GanadoTorch_1010", "火把挥击", "GanadoTorch", false, "wp5805", "", "", "", "115");
                foreach (string id in GanadoMeleeClips)
                {
                    Add(id, id, role + "Melee", id == "Ganado_3011", "", "", "", "", "");
                }
                foreach (string id in GanadoWallClips)
                {
                    Add(id, id, role + "WallReactions", false, "", "", "", "", "");
                }
                foreach (var (id, label) in ChapterOneRootActor.EnemyGroundClips)
                {
                    Add(id, label, role + "Ground", ChapterOneRootActor.IsEnemyGroundLoop(id), "", "", "", "", "");
                }
            }
            if (group is "Leon" or "Ganado")
            {
                foreach (var finish in ChapterOneRootActor.GroundFinishes)
                {
                    if (group == "Leon")
                    {
                        Add(finish.player, finish.label, role + "GroundFinish", false, "wp5000", "", "", finish.enemy, "");
                    }
                    else
                    {
                        Add(finish.enemy, finish.label, role + "GroundFinish", false, "", "", "", "", "");
                    }
                }
            }
            if (group is "Ganado" or "Salvador")
            {
                foreach (string id in EnemyWindowIds(group))
                {
                    string label = "翻窗落地";
                    if (id.EndsWith("1100", StringComparison.Ordinal))
                    {
                        label = "翻窗下落";
                    }
                    Add(id, label, role, false, "", "", "", "", "", EnemyWindowSourceId(id));
                }
                foreach (string id in EnemyLocomotionIds(role))
                {
                    Add(id, id, role + "Locomotion", false, "", "", "", "", "");
                }
            }
            if (role == "Leon")
            {
                foreach (int id in new[] { 100, 102, 104, 106, 110, 112, 114, 116, 140, 141, 900, 902, 904 })
                    Add($"LeonDamage_{id:D4}", id >= 900 ? "里昂死亡" : "里昂受击", "LeonDamage", false, "", "", "", "", "");
                foreach (string id in ContextualWeaponClips)
                {
                    string weapon = "wp5000";
                    if (id.StartsWith("SG09R_", StringComparison.Ordinal))
                    {
                        weapon = "wp4000";
                    }
                    else if (id.StartsWith("W870_", StringComparison.Ordinal))
                    {
                        weapon = "wp4100";
                    }
                    Add(id, id, "LeonWeaponContexts", false, weapon, "", "", "", "");
                }

                foreach (string id in MovementTurns)
                {
                    Add(id, id, "LeonLocomotionTurns", false, "", "", "", "", "");
                }
                foreach (string id in ChapterOneRootActor.DirectionalEdgeIds())
                {
                    Add(id, id, "LeonDirectionalEdges", false, "", "", "", "", "");
                }
                Add("LeonIdleTurn_0170", "站姿左转 180°", "LeonIdleTurn", false, "", "", "", "", "");
                Add("LeonIdleTurn_0171", "站姿右转 180°", "LeonIdleTurn", false, "", "", "", "", "");
                foreach (var pickup in ChapterOneRootActor.PickupTakes)
                {
                    Add(pickup.id, pickup.label, "LeonPickup", false, "", "", "", "", pickup.frame.ToString());
                }
                Add("LeonPickup_0406", "跑动拾取", "LeonPickup", false, "", "", "", "", "");
            }
            return results;

            string FileForRig(string file) => role == "GanadoMale" && file.StartsWith("Ganado", StringComparison.Ordinal)
                ? "GanadoMale" + file.Substring("Ganado".Length) : file;

            void Add(string id, string label, string file, bool loop, string weapon, string mechanismFile,
                string mechanismName, string partner, string markers, string sourceId = null)
            {
                if (string.IsNullOrEmpty(id) || results.Any(r => r.id == id))
                    return;
                if (sourceId == null)
                {
                    sourceId = id;
                }
                var clip = id == "CQBR_0543" ? Find("CQBRRifleDrawCalibrated", "CQBR_0543_RifleFit") : Find(file, sourceId);
                if (clip == null)
                {
                    Audit.Add($"{role}\t{id}\t{file}\t\t\t\t\t\t\t待补");
                    return;
                }

                int[] frames = markers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse).Distinct().OrderBy(x => x).ToArray();
                if (id == "LeonFireExtra_1810_wp4100")
                    frames = new[] { 33, 73, 113, 151, 186 };
                if (frames.Length == 0)
                {
                    int fallback = FallbackContact(id, clip);
                    if (fallback >= 0)
                        frames = new[] { fallback };
                }

                string sourceRole = file == "GanadoTorch" || role == "GanadoMale" ? "GanadoMale" : group;
                bool bakeLoop = loop && !id.Contains("0810") && !id.StartsWith("LeonFireExtra_1810", StringComparison.Ordinal);
                string alias = null;
                if (id != sourceId || id is "CQBR_0543" or "LeonDoors_0007"
                    || file is "LeonWeaponContexts" or "LeonLocomotionTurns" or "LeonDirectionalEdges" or "LeonPickup" or "LeonIdleTurn")
                {
                    alias = id;
                }
                var item = new ChapterOneRootActor.Take
                {
                    id = id,
                    label = label,
                    body = Bake(clip, rig, sourceRole, role, bakeLoop, frames, library, existingClips, out var origin, alias),
                    weapon = weapon,
                    mechanism = Find(mechanismFile, mechanismName),
                    partnerId = partner,
                    contactFrames = frames,
                    pairOrigin = origin,
                    loop = bakeLoop,
                    verticalTravel = id.StartsWith("LeonTraverse_", StringComparison.Ordinal) || id != sourceId
                };
                results.Add(item);
            }
        }

        private static int FallbackContact(string id, AnimationClip clip)
        {
            int pickup = ChapterOneRootActor.PickupContactFrame(id);
            if (pickup >= 0)
            {
                return pickup;
            }
            int melee = ChapterOneRootActor.MeleeContactFrame(id);
            if (melee >= 0)
                return melee;
            if (id is "LeonInteract_0109" or "Ganado_0003" or "Ganado_0001" or "Salvador_0020")
                return Mathf.RoundToInt(clip.length * clip.frameRate * 0.38f);
            if (id.StartsWith("LeonFireExtra_1810_wp", StringComparison.Ordinal) ||
                id.StartsWith("LeonFireExtra_1910_wp", StringComparison.Ordinal))
                return Mathf.RoundToInt(clip.length * clip.frameRate * 0.54f);
            if (id.Contains("_0500", StringComparison.Ordinal) &&
                (id.StartsWith("SG09R", StringComparison.Ordinal) || id.StartsWith("W870", StringComparison.Ordinal)
                    || id.StartsWith("CQBR", StringComparison.Ordinal)))
                return 1;
            return -1;
        }

        private static AnimationClip Bake(AnimationClip source, GameObject target, string sourceRole, string targetRole,
            bool loop, int[] frames, ChapterOneBakedClips library, Dictionary<string, AnimationClip> existingClips,
            out Vector3 origin, string alias = null)
        {
            string inputRig = sourceRole + "_Skeleton";
            string outputRig = targetRole == "Leon" ? "Armature" : targetRole + "_Skeleton";
            string root = inputRig + (sourceRole == "Leon" ? "/root ground" : "/root");
            string outputRoot = outputRig + (targetRole == "Leon" ? "/root ground" : "/root");
            var rig = target.transform.Find(outputRig);
            if (rig == null)
                throw new InvalidOperationException($"{targetRole} 缺骨架 {outputRig}");
            var joint = target.transform.Find(outputRoot);
            if (joint == null)
                throw new InvalidOperationException($"{targetRole} 缺根骨 {outputRoot}");

            string assetPath = AssetDatabase.GetAssetPath(source);
            string id = alias ?? source.name;
            string contactReport = string.Join(",", frames);
            var knifeWindow = ChapterOneRootActor.KnifeHitWindow(id);
            if (knifeWindow.first >= 0)
            {
                contactReport = $"{knifeWindow.first}–{knifeWindow.last}（刀刃扫掠）";
            }
            bool doorStage = id == "LeonDoors_0007";
            bool reuse = existingClips.TryGetValue(id, out var cached)
                && cached.hasRootCurves && cached.isLooping == loop && Mathf.Abs(cached.length - source.length) < 0.0001f
                && PreviousRows.TryGetValue(targetRole + "/" + id, out var row)
                && row[2] == assetPath
                && System.IO.File.GetLastWriteTimeUtc(assetPath) <= System.IO.File.GetLastWriteTimeUtc(AssetDatabase.GetAssetPath(library));
            if (reuse && doorStage)
            {
                var travelBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z");
                reuse = AnimationUtility.GetEditorCurve(cached, travelBinding).Evaluate(cached.length) > 0f;
            }
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            var rootCurves = new Dictionary<string, AnimationCurve>();
            int missing = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (binding.type != typeof(Transform) || !binding.path.StartsWith(inputRig + "/", StringComparison.Ordinal))
                    continue;

                if (binding.path == root)
                {
                    rootCurves.Add(binding.propertyName, AnimationUtility.GetEditorCurve(source, binding));
                    continue;
                }
                if (reuse)
                    continue;

                var data = AnimationUtility.GetEditorCurve(source, binding);
                string path = outputRig + binding.path.Substring(inputRig.Length);
                if (target.transform.Find(path) == null)
                {
                    missing++;
                    continue;
                }
                var destination = binding;
                destination.path = path;
                if (targetRole == "Leon" && binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                    data = Scale(data, 100f);
                bindings.Add(destination);
                curves.Add(CollapseConstant(data));
            }

            if (!rootCurves.ContainsKey("m_LocalPosition.x"))
                throw new InvalidOperationException(source.name + " 找不到原版根位移轨道。");
            const float sourceScale = 100f;
            float outputScale = targetRole == "Leon" ? 1f : 100f;
            Quaternion axis = rig.localRotation;
            Quaternion stageFacing = Quaternion.identity;
            if (doorStage)
            {
                // 双开门的原版坐标面向门外。角色控制器使用面向动作前方的坐标。
                stageFacing = Quaternion.Euler(0f, 180f, 0f);
            }
            Vector3 sourcePosition = Position(0);
            Quaternion sourceRotation = Rotation(0);
            origin = axis * (sourcePosition * sourceScale);
            Vector3 poseOrigin = Quaternion.Inverse(axis) * Vector3.Project(origin, Vector3.up) / outputScale;
            if (reuse)
            {
                CenterCachedPose(cached, outputRoot, poseOrigin, library);
                var report = (string[])PreviousRows[targetRole + "/" + id].Clone();
                report[8] = contactReport;
                Audit.Add(string.Join("\t", report));
                return cached;
            }

            var copy = new AnimationClip { name = id, frameRate = source.frameRate };
            Vector3 delta = Vector3.zero;
            int samples = Mathf.Max(1, Mathf.RoundToInt(source.length * source.frameRate));
            var travel = CreateKeys(3);
            var turn = CreateKeys(4);
            var posePosition = CreateKeys(3);
            var poseRotation = CreateKeys(4);
            Quaternion lastTurn = Quaternion.identity;
            Quaternion lastPose = sourceRotation;
            for (int i = 0; i <= samples; i++)
            {
                float time = source.length * i / samples;
                delta = stageFacing * axis * ((Position(time) - sourcePosition) * sourceScale);
                Quaternion full = axis * Rotation(time) * Quaternion.Inverse(sourceRotation) * Quaternion.Inverse(axis);
                Quaternion yaw = Quaternion.Euler(0, full.eulerAngles.y, 0);
                Quaternion visual = Quaternion.Inverse(axis) * stageFacing * Quaternion.Inverse(yaw) * axis * Rotation(time);
                yaw = AlignSign(lastTurn, yaw);
                visual = AlignSign(lastPose, visual);
                lastTurn = yaw;
                lastPose = visual;
                AddVector(travel, time, delta);
                AddQuaternion(turn, time, yaw);
                AddVector(posePosition, time, poseOrigin);
                AddQuaternion(poseRotation, time, visual);
            }

            AddCurves("", typeof(Animator), "RootT", travel);
            AddCurves("", typeof(Animator), "RootQ", turn);
            AddCurves(outputRoot, typeof(Transform), "m_LocalPosition", posePosition);
            AddCurves(outputRoot, typeof(Transform), "m_LocalRotation", poseRotation);
            if (id == "LeonPair_Ground1010")
            {
                // 将刺入段提前两帧，对齐敌方第 35 帧接触，保留首尾和完整根轨道。
                var sourceTimes = new float[samples + 1];
                for (int frame = 0; frame <= samples; frame++)
                {
                    float window = Mathf.Min(Mathf.InverseLerp(26f, 35f, frame), Mathf.InverseLerp(50f, 35f, frame));
                    sourceTimes[frame] = (frame + 2.2f * Mathf.SmoothStep(0f, 1f, window)) / source.frameRate;
                }
                for (int channel = 0; channel < curves.Count; channel++)
                {
                    curves[channel] = ResampleTiming(curves[channel], sourceTimes, source.frameRate);
                }
            }
            AnimationUtility.SetEditorCurves(copy, bindings.ToArray(), curves.ToArray());
            copy.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AnimationClip saved = copy;
            if (existingClips.TryGetValue(copy.name, out var existing))
            {
                EditorUtility.CopySerialized(copy, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(copy);
                saved = existing;
            }
            else
            {
                AssetDatabase.AddObjectToAsset(copy, library);
            }
            EditorUtility.SetDirty(library);
            Audit.Add($"{targetRole}\t{id}\t{assetPath}\t{saved.length:F4}\t{saved.frameRate}\t{bindings.Count}\t{missing}\t{delta.x:F3},{delta.z:F3}\t{contactReport}\t{(missing == 0 ? "原版已接线" : "原版部分骨骼已接线")}");
            return saved;

            float Evaluate(string name, float time, float fallback)
                => rootCurves.TryGetValue(name, out var c) ? c.Evaluate(time) : fallback;
            Vector3 Position(float time) => new(Evaluate("m_LocalPosition.x", time, 0),
                Evaluate("m_LocalPosition.y", time, 0), Evaluate("m_LocalPosition.z", time, 0));
            Quaternion Rotation(float time) => new Quaternion(Evaluate("m_LocalRotation.x", time, joint.localRotation.x),
                Evaluate("m_LocalRotation.y", time, joint.localRotation.y), Evaluate("m_LocalRotation.z", time, joint.localRotation.z),
                Evaluate("m_LocalRotation.w", time, joint.localRotation.w)).normalized;
            void AddCurves(string path, Type type, string property, List<Keyframe>[] keys)
            {
                const string channels = "xyzw";
                for (int i = 0; i < keys.Length; i++)
                {
                    bindings.Add(EditorCurveBinding.FloatCurve(path, type, property + "." + channels[i]));
                    curves.Add(new AnimationCurve(keys[i].ToArray()));
                }
            }
        }

        private static void CenterCachedPose(AnimationClip clip, string path, Vector3 position, ChapterOneBakedClips library)
        {
            var bindings = new EditorCurveBinding[3];
            var curves = new AnimationCurve[3];
            bool changed = false;
            const string axes = "xyz";
            for (int i = 0; i < axes.Length; i++)
            {
                bindings[i] = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition." + axes[i]);
                float old = AnimationUtility.GetEditorCurve(clip, bindings[i])?.Evaluate(0f) ?? 0f;
                changed |= Mathf.Abs(old - position[i]) > 0.000001f;
                curves[i] = AnimationCurve.Constant(0, clip.length, position[i]);
            }
            if (!changed)
                return;
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(library);
        }

        private static AnimationCurve Scale(AnimationCurve original, float ratio)
        {
            var keys = original.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                key.value *= ratio;
                key.inTangent *= ratio;
                key.outTangent *= ratio;
                keys[i] = key;
            }
            return new AnimationCurve(keys) { preWrapMode = original.preWrapMode, postWrapMode = original.postWrapMode };
        }

        private static AnimationCurve CollapseConstant(AnimationCurve curve)
        {
            if (curve.length < 3)
                return curve;
            var keys = curve.keys;
            float start = keys[0].value;
            for (int i = 1; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].value - start) > 0.000001f)
                    return curve;
            return AnimationCurve.Constant(keys[0].time, keys[^1].time, start);
        }

        private static List<Keyframe>[] CreateKeys(int count) => Enumerable.Range(0, count).Select(_ => new List<Keyframe>()).ToArray();
        private static void AddVector(List<Keyframe>[] keys, float time, Vector3 value)
        {
            for (int i = 0; i < 3; i++)
                keys[i].Add(new Keyframe(time, value[i]));
        }
        private static void AddQuaternion(List<Keyframe>[] keys, float time, Quaternion value)
        {
            for (int i = 0; i < 4; i++)
                keys[i].Add(new Keyframe(time, value[i]));
        }
        private static Quaternion AlignSign(Quaternion previous, Quaternion value) => Quaternion.Dot(previous, value) < 0f
            ? new Quaternion(-value.x, -value.y, -value.z, -value.w) : value;

        private static void BuildAvatarAndController(string role, GameObject rig, List<ChapterOneRootActor.Take> takes)
        {
            // 位移已在 Animator 的 RootT 和 RootQ 轨道中，骨架不再指定第二个位移节点。
            var avatar = AvatarBuilder.BuildGenericAvatar(rig, string.Empty);
            if (avatar == null || !avatar.isValid || avatar.isHuman)
                throw new InvalidOperationException(role + " Generic Avatar 构建失败");
            avatar.name = role + " Generic";
            avatar = SaveAsset(avatar, Generated + "/" + role + ".asset");
            var animator = rig.GetComponent<Animator>();
            if (animator == null)
                animator = rig.AddComponent<Animator>();
            animator.enabled = true;
            animator.avatar = avatar;
            animator.applyRootMotion = true;
            animator.updateMode = AnimatorUpdateMode.Fixed;
            animator.animatePhysics = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (role == "GanadoMale")
            {
                var shared = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/Ganado.controller");
                var overrides = new AnimatorOverrideController(shared);
                var byName = takes.ToDictionary(take => take.id, take => take.body);
                var replacements = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrides.GetOverrides(replacements);
                for (int i = 0; i < replacements.Count; i++)
                {
                    var original = replacements[i].Key;
                    if (!byName.TryGetValue(original.name, out var replacement))
                        throw new InvalidOperationException("男村民共用状态缺片段 " + original.name);
                    replacements[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                }
                overrides.ApplyOverrides(replacements);
                animator.runtimeAnimatorController = SaveAsset(overrides, Generated + "/GanadoMale.overrideController");
                return;
            }

            string path = Generated + "/" + role + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
                AssetDatabase.DeleteAsset(path);
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Pace", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "DurationScale", type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });
            var machine = controller.layers[0].stateMachine;
            var map = takes.ToDictionary(t => t.id);
            AnimationClip Get(string id) => map.TryGetValue(id, out var take) ? take.body : null;
            var move = machine.AddState("Move0", new Vector3(280, 40));
            string group = role == "GanadoMale" ? "Ganado" : role;
            if (role == "Leon")
            {
                move.motion = PaceTree("正常步态", "LeonMove_0160", "LeonMove_0403", "LeonMove_0520", "LeonMove_0720");
                AddDirection(1, "蹲姿", "LeonMove_1160", "LeonMove_1403", "LeonMove_1433", "LeonMove_1443", "LeonMove_1473");
                AddDirection(2, "SG-09 R 瞄准", "SG09R_0160", "SG09R_0403", "SG09R_0433", "SG09R_0443", "SG09R_0473");
                AddDirection(3, "W-870 瞄准", "W870_0160", "W870_0403", "W870_0433", "W870_0443", "W870_0473");
                AddDirection(4, "CQBR 瞄准", "CQBR_0160", "CQBR_0403", "CQBR_0433", "CQBR_0443", "CQBR_0473");
                ConfigureRifleAimGait(controller);
                AddDirection(5, "小刀防守", "Knife_0160", "Knife_0403", "Knife_0413", "Knife_0443", "Knife_0453");
                var hurt = machine.AddState("Move6", new Vector3(1540, 40));
                hurt.motion = PaceTree("伤势步态", "LeonHurt_0160", "LeonHurt_0403", "LeonHurt_0520", "LeonHurt_0520");
            }
            else
            {
                string run = group == "Salvador" && Get("Salvador_2001") == null ? "Salvador_1001" : group + "_2001";
                move.motion = PaceTree(group + " 步态", group + "_0000", group + "_1001", run);
            }
            machine.defaultState = move;

            for (int i = 0; i < takes.Count; i++)
            {
                var item = takes[i];
                var state = machine.AddState(item.id, new Vector3(280 + i % 8 * 215, 145 + i / 8 * 62));
                state.motion = item.body;
                state.speedParameter = "DurationScale";
                state.speedParameterActive = true;
                state.writeDefaultValues = true;
                if (!item.loop && !item.id.Contains("_dead_", StringComparison.Ordinal)
                    && !item.id.StartsWith("LeonPair_", StringComparison.Ordinal)
                    && !item.id.StartsWith("GanadoPair_", StringComparison.Ordinal)
                    && !item.id.StartsWith("SalvadorPair_", StringComparison.Ordinal))
                {
                    var transition = state.AddTransition(move);
                    transition.hasExitTime = true;
                    transition.exitTime = 1.1f;
                    transition.duration = 0.06f;
                }
            }
            if (role == "Leon")
            {
                AddFirearmLayer(controller, rig);
                AddWeaponArmsLayer(controller);
            }
            animator.runtimeAnimatorController = controller;

            BlendTree PaceTree(string title, params string[] ids)
            {
                var tree = new BlendTree { name = title, blendType = BlendTreeType.Simple1D, blendParameter = "Pace",
                    useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller);
                for (int i = 0; i < ids.Length; i++)
                {
                    var clip = Get(ids[i]);
                    if (clip != null)
                        tree.AddChild(clip, i);
                }
                return tree;
            }

            void AddDirection(int index, string title, params string[] ids)
            {
                var tree = new BlendTree { name = title, blendType = BlendTreeType.FreeformCartesian2D,
                    blendParameter = "MoveX", blendParameterY = "MoveY" };
                AssetDatabase.AddObjectToAsset(tree, controller);
                Vector2[] positions = { Vector2.zero, Vector2.up, Vector2.left, Vector2.down, Vector2.right };
                for (int i = 0; i < ids.Length; i++)
                {
                    var clip = Get(ids[i]);
                    if (clip != null)
                        tree.AddChild(clip, positions[i]);
                }
                var state = machine.AddState("Move" + index, new Vector3(280 + index * 210, 40));
                state.motion = tree;
            }
        }

        public static bool RefreshRifleAimGait()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/Leon.controller");
            bool changed = ConfigureRifleAimGait(controller);
            if (changed)
            {
                AssetDatabase.SaveAssetIfDirty(controller);
            }
            return changed;
        }

        private static bool ConfigureRifleAimGait(AnimatorController controller)
        {
            var states = controller.layers[0].stateMachine.states;
            var aim = states.Single(item => item.state.name == "Move4").state;
            if (aim.motion is BlendTree current && current.blendType == BlendTreeType.Simple1D)
            {
                return false;
            }
            var normal = (BlendTree)states.Single(item => item.state.name == "Move0").state.motion;
            var jog = normal.children.Single(child => child.threshold == 2f);
            var sprint = normal.children.Single(child => child.threshold == 3f);
            // 慢跑原片的双脚相位比瞄准步行和冲刺晚约六分之一个周期。
            jog.cycleOffset = 0.84f;
            var tree = new BlendTree
            {
                name = "CQBR 瞄准步速",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Pace",
                useAutomaticThresholds = false,
                children = new[]
                {
                    new ChildMotion { motion = aim.motion, threshold = 1f, timeScale = 1f },
                    jog,
                    sprint
                }
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            aim.motion = tree;
            EditorUtility.SetDirty(aim);
            EditorUtility.SetDirty(controller);
            return true;
        }

        public static int ConfigureFirearmLayer()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/Leon.controller");
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            int count = AddFirearmLayer(controller, rig);
            AddWeaponArmsLayer(controller);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return count;
        }

        public static string RefreshRifleDraw()
        {
            ImportAdditionalArt();
            Clips.Remove("CQBRRifleDrawCalibrated");
            var source = Find("CQBRRifleDrawCalibrated", "CQBR_0543_RifleFit");
            if (source == null)
                throw new InvalidOperationException("找不到步枪组原版拔枪片段。");
            string bankPath = Generated + "/Clips/Leon_Clips.asset";
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            Bake(source, rig, "Leon", "Leon", false, Array.Empty<int>(), library, clips, out _, "CQBR_0543");
            AssetDatabase.SaveAssetIfDirty(library);
            ConfigureFirearmLayer();
            return Audit.Last();
        }

        public static int RefreshWeaponContexts()
        {
            ImportAdditionalArt();
            Clips.Remove("LeonWeaponContexts");
            string bankPath = Generated + "/Clips/Leon_Clips.asset";
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            foreach (string id in ContextualWeaponClips)
            {
                var source = Find("LeonWeaponContexts", id);
                if (source == null)
                {
                    throw new InvalidOperationException("找不到武器姿态片段 " + id);
                }
                Bake(source, rig, "Leon", "Leon", false, Array.Empty<int>(), library, clips, out _, id);
            }

            AssetDatabase.SaveAssetIfDirty(library);
            return ConfigureFirearmLayer();
        }

        public static int RefreshLocomotionTurns() => RefreshClipStates("LeonLocomotionTurns", MovementTurns);

        public static int RefreshIdleTurns() =>
            RefreshClipStates("LeonIdleTurn", new[] { "LeonIdleTurn_0170", "LeonIdleTurn_0171" });

        public static int RefreshPickups() => RefreshClipStates("LeonPickup", new[]
        {
            "LeonPickup_0400", "LeonPickup_0402", "LeonPickup_0404", "LeonPickup_0406",
            "LeonPickup_0410", "LeonPickup_0412", "LeonPickup_0414"
        });

        public static int RefreshLeonDamage()
        {
            int count = RefreshClipStates("LeonDamage", new[] { "LeonDamage_0110", "LeonDamage_0112" });
            ConfigureFirearmLayer();
            return count;
        }

        public static int RefreshDirectionalEdges() =>
            RefreshClipStates("LeonDirectionalEdges", ChapterOneRootActor.DirectionalEdgeIds().ToArray());

        public static int RefreshEnemyLocomotion()
        {
            int count = 0;
            foreach (string role in new[] { "Ganado", "GanadoMale", "Salvador" })
            {
                count += RefreshClipStates(role + "Locomotion", EnemyLocomotionIds(role), role);
            }
            return count;
        }

        public static int RefreshGanadoMelee()
        {
            int count = RefreshClipStates("GanadoMelee", GanadoMeleeClips, "Ganado", "Ganado_3011");
            return count + RefreshClipStates("GanadoMaleMelee", GanadoMeleeClips, "GanadoMale", "Ganado_3011");
        }

        public static int RefreshGanadoWallReactions()
        {
            int count = RefreshClipStates("GanadoWallReactions", GanadoWallClips, "Ganado");
            return count + RefreshClipStates("GanadoMaleWallReactions", GanadoWallClips, "GanadoMale");
        }

        public static int RefreshGanadoGround()
        {
            var ids = ChapterOneRootActor.EnemyGroundClips.Select(clip => clip.id).ToArray();
            int count = RefreshClipStates("GanadoGround", ids, "Ganado");
            return count + RefreshClipStates("GanadoMaleGround", ids, "GanadoMale");
        }

        public static int RefreshGroundFinishes()
        {
            int count = RefreshClipStates("LeonGroundFinish", ChapterOneRootActor.GroundFinishes.Select(x => x.player).ToArray());
            var enemies = ChapterOneRootActor.GroundFinishes.Select(x => x.enemy).ToArray();
            count += RefreshClipStates("GanadoGroundFinish", enemies, "Ganado");
            count += RefreshClipStates("GanadoMaleGroundFinish", enemies, "GanadoMale");
            ChapterOnePairMotion.Refresh();
            return count;
        }

        internal static Vector3 GroundFinishOrigin(string role, string id)
        {
            var clip = Find(role + "GroundFinish", id);
            if (clip == null)
            {
                throw new InvalidOperationException("缺少处决源片 " + id);
            }
            string root = role + "_Skeleton/root";
            string rig = role + "_Skeleton";
            if (role == "Leon")
            {
                root = "Leon_Skeleton/root ground";
                rig = "Armature";
            }
            float Value(string axis)
            {
                var key = EditorCurveBinding.FloatCurve(root, typeof(Transform), "m_LocalPosition." + axis);
                return AnimationUtility.GetEditorCurve(clip, key).Evaluate(0f);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
            return prefab.transform.Find(rig).localRotation * (100f * new Vector3(Value("x"), Value("y"), Value("z")));
        }

        private static string[] EnemyLocomotionIds(string role)
        {
            var ids = new List<string> { "0350", "0351", "0352", "0353", "1003", "2002" };
            string prefix = "Ganado";
            if (role == "Salvador")
            {
                prefix = "Salvador";
            }
            else
            {
                ids.Add("2003");
            }
            return ids.Select(id => prefix + "_" + id).ToArray();
        }

        private static int RefreshClipStates(string file, string[] ids, string role = "Leon", string loopingId = null,
            Func<string, string> sourceName = null)
        {
            ImportAdditionalArt();
            Clips.Remove(file);
            string bankPath = Generated + "/Clips/" + role + "_Clips.asset";
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
            string controllerRole = role;
            if (role == "GanadoMale")
            {
                controllerRole = "Ganado";
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/" + controllerRole + ".controller");
            var machine = controller.layers[0].stateMachine;
            var bakedClips = new Dictionary<string, AnimationClip>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string id in ids)
                {
                    string sourceId = id;
                    if (sourceName != null)
                    {
                        sourceId = sourceName(id);
                    }
                    var source = Find(file, sourceId);
                    if (source == null)
                    {
                        throw new InvalidOperationException("找不到原版移动片段 " + id);
                    }
                    bool loop = id == loopingId || ChapterOneRootActor.IsEnemyGroundLoop(id);
                    int pickupFrame = ChapterOneRootActor.PickupContactFrame(id);
                    int[] frames = Array.Empty<int>();
                    if (pickupFrame >= 0)
                    {
                        frames = new[] { pickupFrame };
                    }
                    var baked = Bake(source, rig, role, role, loop, frames, library, clips, out _, id);
                    bakedClips.Add(id, baked);
                    if (role == "GanadoMale")
                    {
                        continue;
                    }
                    var state = machine.states.Select(child => child.state).FirstOrDefault(item => item.name == id);
                    if (state == null)
                    {
                        int index = machine.states.Length;
                        state = machine.AddState(id, new Vector3(280 + index % 8 * 215, 145 + index / 8 * 62));
                    }
                    state.motion = baked;
                    state.speedParameter = "DurationScale";
                    state.speedParameterActive = true;
                    state.writeDefaultValues = true;
                    EditorUtility.SetDirty(state);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssetIfDirty(library);
            if (role == "GanadoMale")
            {
                var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Generated + "/GanadoMale.overrideController");
                var bindings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrides.GetOverrides(bindings);
                for (int i = 0; i < bindings.Count; i++)
                {
                    AnimationClip original = bindings[i].Key;
                    if (bakedClips.TryGetValue(original.name, out var replacement))
                    {
                        bindings[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
                    }
                }
                overrides.ApplyOverrides(bindings);
                EditorUtility.SetDirty(overrides);
                AssetDatabase.SaveAssetIfDirty(overrides);
            }
            else
            {
                EditorUtility.SetDirty(machine);
                AssetDatabase.SaveAssetIfDirty(controller);
            }
            return ids.Length;
        }

        private static int AddFirearmLayer(AnimatorController controller, GameObject rig, bool rifleOnly = false)
        {
            string name = ChapterOneRootActor.WeaponLayerName;
            var source = controller.layers[0].stateMachine.states.Select(child => child.state)
                .ToDictionary(state => state.name);
            var machine = controller.layers.FirstOrDefault(layer => layer.name == name)?.stateMachine;
            if (machine == null)
            {
                var paths = AnimationUtility.GetCurveBindings((AnimationClip)source["SG09R_0500"].motion)
                    .Select(binding => binding.path).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
                var mask = new AvatarMask { name = "Leon Firearm Mask", transformCount = paths.Length };
                for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
                for (int i = 0; i < paths.Length; i++)
                {
                    mask.SetTransformPath(i, paths[i]);
                    mask.SetTransformActive(i, IsUpperBodyPath(paths[i]));
                }
                AssetDatabase.AddObjectToAsset(mask, controller);

                machine = new AnimatorStateMachine { name = name };
                AssetDatabase.AddObjectToAsset(machine, controller);
                machine.defaultState = machine.AddState("Inactive", new Vector3(280, 40));
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = ChapterOneRootActor.WeaponRateName,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f
                });
                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = name,
                    stateMachine = machine,
                    avatarMask = mask,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 0f
                });
            }

            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var bank = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            if (!rifleOnly)
            {
                foreach (var state in source.Values)
                {
                    bool action = ChapterOneRootActor.IsWeaponLayerAction(state.name) || ChapterOneRootActor.IsLeonStagger(state.name);
                    if (action || ChapterOneRootActor.IsRunningAimPose(state.name))
                        SetState(state.name, state.motion, action);
                    if (ChapterOneRootActor.KnifeMoveFrame(state.name) >= 0)
                    {
                        SetState(ChapterOneRootActor.KnifeTailId(state.name), state.motion, true);
                    }
                }
                foreach (string id in ContextualWeaponClips)
                {
                    if (bank.TryGetValue(id, out var clip))
                    {
                        SetState(id, clip, true);
                    }
                }
                SetUpperClip("W870_Carry", bank["W870_0158"], false);
                SetUpperClip("Knife_Draw", bank["Knife_0551"], true);
                SetUpperClip("Knife_CrouchDraw", bank["Knife_1551"], true);
            }
            var nativeHold = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Native/CQBRNativeHold.fbx")
                .OfType<AnimationClip>().Single(clip => clip.name == "cha0_wp4402M_090_hand_VerA");
            SetUpperClip("CQBR_Carry", nativeHold, false, true);
            var carry = (AnimationClip)states["CQBR_Carry"].motion;
            var rifleTransitions = BakeCqbrAimTransitions(controller, rig, bank, carry);
            foreach (var clip in rifleTransitions)
            {
                SetState(clip.Key, clip.Value, true);
            }
            SetUpperClip("CQBR_0551", rifleTransitions["CQBR_0543"], true);
            EditorUtility.SetDirty(machine);
            return states.Count - 1;

            void SetState(string id, Motion motion, bool action)
            {
                if (!states.TryGetValue(id, out var state))
                {
                    int i = states.Count - 1;
                    state = machine.AddState(id, new Vector3(280 + i % 5 * 215, 115 + i / 5 * 62));
                    states.Add(id, state);
                }
                state.motion = motion;
                state.speedParameter = ChapterOneRootActor.WeaponRateName;
                state.speedParameterActive = action;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
            }

            void SetUpperClip(string id, AnimationClip clip, bool reverse, bool nativeLeonSkeleton = false)
            {
                var pose = states.TryGetValue(id, out var state) ? state.motion as AnimationClip : null;
                if (pose == null)
                {
                    pose = new AnimationClip { name = id, frameRate = clip.frameRate };
                    AssetDatabase.AddObjectToAsset(pose, controller);
                }
                // 原版专用持枪片只转换骨架路径和导入单位，双臂、手腕与手指一起取样。
                string TargetPath(string path)
                {
                    const string inputRoot = "Leon_Skeleton/";
                    if (nativeLeonSkeleton && path.StartsWith(inputRoot, StringComparison.Ordinal))
                    {
                        return "Armature/" + path.Substring(inputRoot.Length);
                    }
                    return path;
                }
                var bindings = AnimationUtility.GetCurveBindings(clip).Where(binding => IsUpperBodyPath(TargetPath(binding.path))).ToArray();
                var curves = bindings.Select(binding =>
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (!reverse)
                    {
                        float value = curve.Evaluate(clip.length);
                        if (nativeLeonSkeleton && binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                        {
                            value *= 100f;
                        }
                        return AnimationCurve.Constant(0f, 1f, value);
                    }
                    return new AnimationCurve(curve.keys.Reverse().Select(key => new Keyframe(
                        clip.length - key.time, key.value, -key.outTangent, -key.inTangent, key.outWeight, key.inWeight)).ToArray());
                }).ToArray();
                for (int index = 0; index < bindings.Length; index++)
                {
                    bindings[index].path = TargetPath(bindings[index].path);
                }
                pose.ClearCurves();
                pose.frameRate = clip.frameRate;
                AnimationUtility.SetEditorCurves(pose, bindings, curves);
                pose.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(pose);
                settings.loopTime = !reverse;
                AnimationUtility.SetAnimationClipSettings(pose, settings);
                EditorUtility.SetDirty(pose);
                SetState(id, pose, reverse);
            }
        }

        private static void AddWeaponArmsLayer(AnimatorController controller)
        {
            var upper = controller.layers.First(layer => layer.name == ChapterOneRootActor.WeaponLayerName);
            var arms = controller.layers.FirstOrDefault(layer => layer.name == ChapterOneRootActor.WeaponArmsLayerName);
            if (arms == null)
            {
                var upperMask = upper.avatarMask;
                var mask = new AvatarMask { name = "Leon Weapon Arms Mask", transformCount = upperMask.transformCount };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                }
                for (int i = 0; i < upperMask.transformCount; i++)
                {
                    string path = upperMask.GetTransformPath(i);
                    mask.SetTransformPath(i, path);
                    mask.SetTransformActive(i, path.Contains("/arm left shoulder", StringComparison.Ordinal)
                        || path.Contains("/arm right shoulder", StringComparison.Ordinal));
                }
                AssetDatabase.AddObjectToAsset(mask, controller);
                var machine = new AnimatorStateMachine { name = ChapterOneRootActor.WeaponArmsLayerName };
                AssetDatabase.AddObjectToAsset(machine, controller);
                arms = new AnimatorControllerLayer
                {
                    name = ChapterOneRootActor.WeaponArmsLayerName,
                    stateMachine = machine,
                    avatarMask = mask,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    defaultWeight = 0f
                };
                controller.AddLayer(arms);
            }
            var states = arms.stateMachine.states.ToDictionary(child => child.state.name, child => child.state);
            foreach (var child in upper.stateMachine.states)
            {
                var reference = child.state;
                if (!states.TryGetValue(reference.name, out var state))
                {
                    state = arms.stateMachine.AddState(reference.name, child.position);
                }
                state.motion = reference.motion;
                state.speedParameter = reference.speedParameter;
                state.speedParameterActive = reference.speedParameterActive;
                state.writeDefaultValues = false;
                if (reference == upper.stateMachine.defaultState)
                {
                    arms.stateMachine.defaultState = state;
                }
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(arms.stateMachine);
        }

        private static bool IsUpperBodyPath(string path)
        {
            const string spine = "Armature/root ground/root hips/spine lower";
            return path == spine || path.StartsWith(spine + "/", StringComparison.Ordinal);
        }
    }
}
#endif

