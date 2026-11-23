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
        private const string MainW870Bank = Generated + "/MainLeonW870Motions.asset";
        private const string MainW870Mask = Generated + "/MainLeonWeaponAction.mask";
        private const string MainW870PoseMask = Generated + "/MainLeonNativeLongGunPose.mask";
        private const string W870ModelName = "wp4100_Model";
        private static readonly string[] W870ReloadClips = { "0805", "0810", "0817", "0815" };

        public static string InstallMainLeonW870Reload()
        {
            const string path = "Assets/GameScene.unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式安装主场景 W-870。");
            }
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException("GameScene 有未保存修改。");
            }

            try
            {
                var leon = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
                var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
                if (leon.GetComponent<Animator>().runtimeAnimatorController != graph)
                {
                    throw new InvalidOperationException("GameScene 里昂的正式动画控制器不匹配。");
                }
                bool mounted = MountMainW870(leon);
                bool boundAim = BindMainW870Aim(leon);
                bool boundBack = BindMainW870Back(leon, AddMainW870ReloadMotions(graph));
                AssetDatabase.SaveAssetIfDirty(graph);
                if ((mounted || boundAim || boundBack) && !EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("无法保存 GameScene 的 W-870 装配。");
                }
                return "W-870 原版拔收、持枪、进退瞄、开火、泵动及站蹲逐发装填已装配。";
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool BindMainW870Back(LeonController leon, Pose grip)
        {
            var back = Node(leon.gameObject, "W870 Native Back");
            var spine = Node(leon.gameObject, "spine upper");
            if (back.parent == spine && Vector3.Distance(back.localPosition, grip.position) < 0.0001f
                && Quaternion.Angle(back.localRotation, grip.rotation) < 0.01f)
            {
                return false;
            }
            back.SetParent(spine, false);
            back.SetLocalPositionAndRotation(grip.position, grip.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(back);
            EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            return true;
        }

        private static bool BindMainW870Aim(LeonController leon)
        {
            var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(item => item.data != null && item.data.weaponType == WeaponType.Shotgun);
            var rig = new SerializedObject(leon.GetComponent<LeonAnimRig>());
            rig.FindProperty("nativeW870Weapon").objectReferenceValue = weapon;
            rig.FindProperty("nativeCqbrAimSpine").objectReferenceValue = Node(leon.gameObject, "spine upper");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var aim = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Single(clip => clip.name == "W870_0160");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                aim.SampleAnimation(sample, 0f);
                var spine = Node(sample, "spine upper");
                var muzzle = Node(Node(sample, W870ModelName).gameObject, "RE4_Muzzle");
                rig.FindProperty("nativeW870AimForward").vector3Value = spine.InverseTransformDirection(muzzle.forward);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            bool changed = rig.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            }
            return changed;
        }

        private static bool MountMainW870(LeonController leon)
        {
            var wrist = Node(leon.gameObject, "arm right wrist");
            var socket = wrist.Find("RE4_Grip/RE4_WeaponSocket");
            if (socket == null)
            {
                throw new InvalidOperationException("正式里昂缺少已校准的武器挂点。");
            }
            if (socket.Find(W870ModelName) != null)
            {
                return false;
            }

            var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(item => item.data != null && item.data.weaponType == WeaponType.Shotgun);
            var visual = new SerializedObject(leon.GetComponent<LeonWeaponVisualController>());
            var heldRenderers = visual.FindProperty("shotgunVisualRenderers");
            var backRenderers = visual.FindProperty("shotgunBackRenderers");
            var oldHeld = weapon.VisualModel.transform.Find("Mossberg_590_Model");
            var oldBack = Node(leon.gameObject, "Mossberg 590 Shotgun_Back").Find("Mossberg_590_Model");
            var oldMuzzle = weapon.muzzlePoint;
            Vector3 backMuzzlePosition = oldBack.TransformPoint(oldHeld.InverseTransformPoint(oldMuzzle.position));
            Quaternion backMuzzleRotation = oldBack.rotation * Quaternion.Inverse(oldHeld.rotation) * oldMuzzle.rotation;

            var sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var sourceModel = Node(sourceRig, W870ModelName);
            var held = Object.Instantiate(sourceModel.gameObject, socket, false);
            held.name = W870ModelName;
            held.SetActive(true);
            var nativeMuzzle = Node(held, "RE4_Muzzle");
            var back = Object.Instantiate(sourceModel.gameObject, oldBack.parent, false);
            back.name = "W870 Native Back";
            back.SetActive(true);
            var newBackMuzzle = Node(back, "RE4_Muzzle");
            Quaternion localMuzzle = Quaternion.Inverse(back.transform.rotation) * newBackMuzzle.rotation;
            back.transform.rotation = backMuzzleRotation * Quaternion.Inverse(localMuzzle);
            back.transform.position += backMuzzlePosition - newBackMuzzle.position;

            ReplaceRenderers(heldRenderers, held, false);
            ReplaceRenderers(backRenderers, back, backRenderers.arraySize > 0
                && ((Renderer)backRenderers.GetArrayElementAtIndex(0).objectReferenceValue).enabled);
            var visualObjects = visual.FindProperty("shotgunVisualObjects");
            visualObjects.arraySize = 1;
            visualObjects.GetArrayElementAtIndex(0).objectReferenceValue = held;
            visual.ApplyModifiedPropertiesWithoutUndo();

            var weaponProperties = new SerializedObject(weapon);
            weaponProperties.FindProperty("visualModel").objectReferenceValue = held;
            weaponProperties.FindProperty("muzzlePoint").objectReferenceValue = nativeMuzzle;
            weaponProperties.ApplyModifiedPropertiesWithoutUndo();
            oldMuzzle.SetPositionAndRotation(nativeMuzzle.position, nativeMuzzle.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldMuzzle);

            var rig = leon.GetComponent<LeonAnimRig>();
            var aim = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Single(clip => clip.name == "W870_0160");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig, preview);
                aim.SampleAnimation(sample, 0f);
                var sampledModel = Node(sample, W870ModelName);
                rig.shotgunRightTarget = Grip("W870 Grip Right", "arm right wrist");
                rig.shotgunLeftTarget = Grip("W870 Grip Left", "arm left wrist");

                Transform Grip(string name, string boneName)
                {
                    var bone = Node(sample, boneName);
                    var point = new GameObject(name).transform;
                    point.SetParent(held.transform, false);
                    point.SetLocalPositionAndRotation(sampledModel.InverseTransformPoint(bone.position),
                        Quaternion.Inverse(sampledModel.rotation) * bone.rotation);
                    return point;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            EditorUtility.SetDirty(rig);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            return true;
        }

        private static Pose AddMainW870ReloadMotions(AnimatorController graph)
        {
            var sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var model = Node(sourceRig, W870ModelName);
            string weaponPath = AnimationUtility.CalculateTransformPath(model, sourceRig.transform);
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var mechanisms = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4100_Mechanism.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainW870Bank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainW870Bank);
            }
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainW870Bank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            int layer = LeonAnimatorDriver.LayerWeaponAction;
            var layers = graph.layers;
            var machine = layers[layer].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var empty = states["Empty"];
            int poseLayer = LeonAnimatorDriver.LayerRifleCrouch;
            layers[layer].avatarMask = AddMainWeaponMaskPaths(layers[layer].avatarMask, MainW870Mask, sourceRig, model);
            layers[poseLayer].avatarMask = AddMainWeaponMaskPaths(layers[poseLayer].avatarMask, MainW870PoseMask, sourceRig, model);
            graph.layers = layers;

            foreach (bool crouched in new[] { false, true })
            {
                var reload = new Dictionary<W870ReloadStage, AnimatorState>();
                for (int i = 0; i < W870ReloadClips.Length; i++)
                {
                    var stage = (W870ReloadStage)(i + 1);
                    string suffix = W870ReloadClips[i];
                    string bodyName = "W870_" + suffix;
                    if (crouched)
                    {
                        bodyName = "LeonFireExtra_1" + suffix.Substring(1) + "_wp4100";
                    }
                    var body = source[bodyName];
                    var mechanism = mechanisms["W870_" + suffix + "_Mechanism"];
                    if (body.frameRate != 60f || !Mathf.Approximately(body.length, mechanism.length))
                    {
                        throw new InvalidOperationException("W-870 身体与枪体轨道的帧范围不匹配。");
                    }
                    string name = LeonAnimatorDriver.W870ReloadStateName(stage, crouched);
                    var clip = SaveMotion(name, body, mechanism, loop: stage == W870ReloadStage.Loop);

                    if (!states.TryGetValue(name, out var state))
                    {
                        float y = 350f;
                        if (crouched)
                        {
                            y += 115f;
                        }
                        state = machine.AddState(name, new Vector3(450 + 260 * i, y));
                        var behavior = state.AddStateMachineBehaviour<WeaponActionSMB>();
                        behavior.actionType = WeaponActionType.Reload;
                        behavior.completeNormalizedTime = 0.99f;
                        behavior.reportUnwrappedProgress = stage == W870ReloadStage.Loop;
                    }
                    state.motion = clip;
                    state.writeDefaultValues = false;
                    EditorUtility.SetDirty(state);
                    reload.Add(stage, state);
                }

                var start = reload[W870ReloadStage.Start];
                var loop = reload[W870ReloadStage.Loop];
                var finish = reload[W870ReloadStage.Finish];
                var emptyFinish = reload[W870ReloadStage.EmptyFinish];
                Link(empty, start, LeonAnimatorDriver.W870ReloadAction(W870ReloadStage.Start, crouched), 0f);
                Link(start, loop, LeonAnimatorDriver.W870ReloadAction(W870ReloadStage.Loop, crouched), 0.10f);
                foreach (var phase in new[] { start, loop })
                {
                    Link(phase, finish, LeonAnimatorDriver.W870ReloadAction(W870ReloadStage.Finish, crouched), 0.18f);
                    Link(phase, emptyFinish, LeonAnimatorDriver.W870ReloadAction(W870ReloadStage.EmptyFinish, crouched), 0.18f);
                }
                foreach (var state in reload.Values)
                {
                    Link(state, empty, 0, 0f);
                }
            }

            var backGrip = AddSwitchMotions();
            AddStandingPoseMotions();
            InstallStationaryW870Breath(graph);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(graph);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
            return backGrip;

            AnimationClip SaveMotion(string name, AnimationClip body, AnimationClip mechanism,
                bool loop = false, bool hold = false, bool reverse = false)
            {
                var clip = new AnimationClip { name = name, frameRate = body.frameRate };
                WriteCurves(body, ArmBindings(body), null);
                WriteCurves(mechanism, AnimationUtility.GetCurveBindings(mechanism), weaponPath);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                // 直接保留源曲线切线，避免重算后手和护木的接触姿势改变。
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
                return clip;

                void WriteCurves(AnimationClip motion, EditorCurveBinding[] bindings, string prefix)
                {
                    var curves = new AnimationCurve[bindings.Length];
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        var curve = AnimationUtility.GetEditorCurve(motion, bindings[i]);
                        if (hold)
                        {
                            curve = AnimationCurve.Constant(0f, 1f, curve.Evaluate(0f));
                        }
                        else if (reverse)
                        {
                            curve = new AnimationCurve(curve.keys.Reverse().Select(key => new Keyframe(
                                motion.length - key.time, key.value, -key.outTangent, -key.inTangent)).ToArray());
                        }
                        curves[i] = curve;
                        if (prefix != null)
                        {
                            if (bindings[i].path.Length == 0)
                            {
                                bindings[i].path = prefix;
                            }
                            else
                            {
                                bindings[i].path = prefix + "/" + bindings[i].path;
                            }
                        }
                    }
                    AnimationUtility.SetEditorCurves(clip, bindings, curves);
                }
            }

            Pose AddSwitchMotions()
            {
                var rawDraw = source["W870_0543"];
                var rawStow = source["W870_0551"];
                var draw = new AnimationClip();
                var stow = new AnimationClip();
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var sample = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig, preview);
                    var spine = Node(sample, "spine upper");
                    var held = Node(sample, W870ModelName);
                    var left = new ChapterOneArmAim.Arm(Node(sample, "arm left wrist"));
                    var right = new ChapterOneArmAim.Arm(Node(sample, "arm right wrist"));
                    var carry = source["W870_0140"];
                    rawDraw.SampleAnimation(sample, LeonAnimatorDriver.W870RevealFrame / rawDraw.frameRate);
                    var grip = new Pose(spine.InverseTransformPoint(held.position), Quaternion.Inverse(spine.rotation) * held.rotation);

                    BakeSwitch(rawDraw, draw, LeonAnimatorDriver.W870DrawFrames, false);
                    BakeSwitch(rawStow, stow, LeonAnimatorDriver.W870HolsterFrames, true);
                    var holster = SwitchState(LeonAnimatorDriver.W870HolsterName, WeaponActionType.Holster, stow, "0551", 450f);
                    var equip = SwitchState(LeonAnimatorDriver.W870EquipName, WeaponActionType.Equip, draw, "0543", 730f);
                    var sourceSelector = (BlendTree)states["Holster"].motion;
                    if (!graph.parameters.Any(parameter => parameter.name == LeonAnimatorDriver.HolsterSourceWeapon))
                    {
                        graph.AddParameter(LeonAnimatorDriver.HolsterSourceWeapon, AnimatorControllerParameterType.Float);
                    }
                    // 提交目标枪时，正在混出的收枪仍按来源枪选片。
                    sourceSelector.blendParameter = LeonAnimatorDriver.HolsterSourceWeapon;
                    EditorUtility.SetDirty(sourceSelector);
                    Link(empty, holster, LeonAnimatorDriver.W870HolsterAction, 0f);
                    Link(empty, equip, LeonAnimatorDriver.W870EquipAction, 0f);
                    Link(states["Holster"], equip, LeonAnimatorDriver.W870EquipAction, 0.15f);
                    Link(holster, states["Equip"], (int)WeaponActionType.Equip, 0.15f);
                    Link(holster, states["KnifeTake"], (int)WeaponActionType.KnifeTake, 0.15f);
                    Link(holster, empty, 0, 0f);
                    Link(equip, empty, 0, 0f);
                    Link(states["Equip"], empty, 0, 0f);
                    return grip;

                    void BakeSwitch(AnimationClip raw, AnimationClip result, int frames, bool holstering)
                    {
                        if (raw.frameRate != 60f || Mathf.RoundToInt(raw.length * raw.frameRate) != frames)
                        {
                            throw new InvalidOperationException("W-870 拔收枪源片的帧范围已变化。");
                        }
                        var tracks = ArmBindings(raw).Where(binding => binding.type == typeof(Transform))
                            .Select(binding => binding.path).Distinct().Select(path => new AimJointTrack(path, sample.transform.Find(path)))
                            .Where(track => track.joint != null).ToArray();
                        carry.SampleAnimation(sample, 0f);
                        var carryPose = tracks.Select(track => track.LocalPose).ToArray();
                        var carrySupport = new Pose(held.InverseTransformPoint(left.wrist.position),
                            Quaternion.Inverse(held.rotation) * left.wrist.rotation);
                        for (int frame = 0; frame <= frames; frame++)
                        {
                            float time = frame / raw.frameRate;
                            raw.SampleAnimation(sample, time);
                            var support = new Pose(held.InverseTransformPoint(left.wrist.position),
                                Quaternion.Inverse(held.rotation) * left.wrist.rotation);
                            float carryWeight = Mathf.SmoothStep(0f, 1f, (frame - 94f) / (frames - 94f));
                            if (holstering)
                            {
                                carryWeight = Mathf.SmoothStep(1f, 0f, frame / 12f);
                            }
                            if (carryWeight > 0f)
                            {
                                for (int i = 0; i < tracks.Length; i++)
                                {
                                    var joint = tracks[i].joint;
                                    joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.localPosition, carryPose[i].position, carryWeight),
                                        Quaternion.Slerp(joint.localRotation, carryPose[i].rotation, carryWeight));
                                }
                                support.position = Vector3.Lerp(support.position, carrySupport.position, carryWeight);
                                support.rotation = Quaternion.Slerp(support.rotation, carrySupport.rotation, carryWeight);
                            }

                            // 收枪接触附近用同一背挂姿势。保留其余原片轨迹和两只手的骨长。
                            float backWeight = 0f;
                            if (holstering)
                            {
                                int contact = LeonAnimatorDriver.W870StowFrame;
                                backWeight = Mathf.SmoothStep(0f, 1f, (frame - (contact - 8)) / 8f)
                                    * Mathf.SmoothStep(1f, 0f, (frame - (contact + 1)) / 8f);
                            }
                            if (backWeight > 0f)
                            {
                                var anchor = Vector3.Lerp(held.position, spine.TransformPoint(grip.position), backWeight);
                                var orientation = Quaternion.Slerp(held.rotation, spine.rotation * grip.rotation, backWeight);
                                var correction = orientation * Quaternion.Inverse(held.rotation);
                                right.Solve(anchor + correction * (right.wrist.position - held.position),
                                    correction * right.wrist.rotation, spine.right);
                            }
                            if (carryWeight > 0f || backWeight > 0f)
                            {
                                left.Solve(held.TransformPoint(support.position), held.rotation * support.rotation, -spine.right);
                            }
                            foreach (var track in tracks)
                            {
                                track.Record(time);
                            }
                        }
                        WriteRifleUpperTracks(result, tracks, raw.frameRate);
                    }

                    AnimatorState SwitchState(string name, WeaponActionType action, AnimationClip body, string suffix, float x)
                    {
                        if (!states.TryGetValue(name, out var state))
                        {
                            state = machine.AddState(name, new Vector3(x, 235f));
                            var behavior = state.AddStateMachineBehaviour<WeaponActionSMB>();
                            behavior.actionType = action;
                            behavior.completeNormalizedTime = 0.99f;
                        }
                        var mechanism = mechanisms["W870_" + suffix + "_Mechanism"];
                        if (!Mathf.Approximately(body.length, mechanism.length))
                        {
                            throw new InvalidOperationException("W-870 拔收枪与枪体轨道的帧范围不匹配。");
                        }
                        state.motion = SaveMotion(name, body, mechanism);
                        state.writeDefaultValues = false;
                        EditorUtility.SetDirty(state);
                        return state;
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                    Object.DestroyImmediate(draw);
                    Object.DestroyImmediate(stow);
                }
            }

            void AddStandingPoseMotions()
            {
                var poseMachine = graph.layers[poseLayer].stateMachine;
                var existing = poseMachine.states.ToDictionary(child => child.state.name, child => child.state);
                var rawRaise = source["W870_0140"];
                var rawLower = source["W870_0158"];
                var aim = source["W870_0160"];
                var raise = new AnimationClip();
                var lower = new AnimationClip();
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var sample = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig, preview);
                    var left = new ChapterOneArmAim.Arm(Node(sample, "arm left wrist"));
                    var right = Node(sample, "arm right wrist");
                    BakeRifleAimClip(sample, rawRaise, rawRaise, aim, raise, left, right, false);
                    BakeRifleAimClip(sample, rawLower, aim, rawRaise, lower, left, right, false);

                    var interval = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Scripts/Combat/WeaponData_Shotgun.asset").fireRate;
                    float fireSpeed = (source["W870_0500"].length + source["W870_0510"].length) / Mathf.Max(0.1f, interval);
                    AddPose(W870Pose.Carry, rawRaise, "0140", hold: true);
                    AddPose(W870Pose.EmptyCarry, rawRaise, "0160_wp4100h", hold: true);
                    AddPose(W870Pose.Raise, raise, "0140");
                    AddPose(W870Pose.Lower, lower, "0158");
                    AddPose(W870Pose.ReverseRaise, raise, "0140", reverse: true);
                    AddPose(W870Pose.ReverseLower, lower, "0158", reverse: true);
                    AddPose(W870Pose.Aim, aim, "0160", hold: true);
                    AddPose(W870Pose.EmptyAim, aim, "0160_wp4100h", hold: true);
                    AddPose(W870Pose.Shot, source["W870_0500"], "0500", speed: fireSpeed);
                    AddPose(W870Pose.LastShot, source["W870_0500_wp4100h"], "0500_wp4100h", speed: fireSpeed);
                    AddPose(W870Pose.Pump, source["W870_0510"], "0510", speed: fireSpeed);
                    AddPose(W870Pose.DryFire, source["W870_0520"], "0520");
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                    Object.DestroyImmediate(raise);
                    Object.DestroyImmediate(lower);
                }
                EditorUtility.SetDirty(poseMachine);

                void AddPose(W870Pose pose, AnimationClip body, string mechanism,
                    bool hold = false, bool reverse = false, float speed = 1f)
                {
                    string name = LeonAnimatorDriver.W870PoseName(pose);
                    if (!existing.TryGetValue(name, out var state))
                    {
                        state = poseMachine.AddState(name, new Vector3(780 + ((int)pose % 4) * 220, 260 + ((int)pose / 4) * 85));
                    }
                    state.motion = SaveMotion(name, body, mechanisms["W870_" + mechanism + "_Mechanism"], hold, hold, reverse);
                    state.speed = speed;
                    state.writeDefaultValues = false;
                    EditorUtility.SetDirty(state);
                }
            }
        }

        private static AvatarMask AddMainWeaponMaskPaths(AvatarMask source, string assetPath, GameObject sourceRig, Transform model)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(assetPath);
            if (mask == null)
            {
                mask = Object.Instantiate(source);
                AssetDatabase.CreateAsset(mask, assetPath);
            }
            else if (source != mask)
            {
                EditorUtility.CopySerialized(source, mask);
            }
            var maskPaths = Enumerable.Range(0, mask.transformCount).Select(mask.GetTransformPath).ToHashSet();
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(bone, sourceRig.transform);
                if (maskPaths.Add(path))
                {
                    int index = mask.transformCount;
                    mask.transformCount = index + 1;
                    mask.SetTransformPath(index, path);
                    mask.SetTransformActive(index, true);
                }
            }
            EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            return mask;
        }

        private static void Link(AnimatorState from, AnimatorState to, int command, float duration)
        {
            var transition = from.transitions.SingleOrDefault(item => item.destinationState == to);
            if (transition == null)
            {
                transition = from.AddTransition(to);
                transition.AddCondition(AnimatorConditionMode.Equals, command, "WeaponAction");
            }
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            EditorUtility.SetDirty(transition);
            EditorUtility.SetDirty(from);
        }
    }
}
#endif
