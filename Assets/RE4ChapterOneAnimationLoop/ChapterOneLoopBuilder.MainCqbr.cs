#if UNITY_EDITOR
using System;
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
        private const string MainCqbrBank = Generated + "/MainLeonCqbrMotions.asset";
        private const string MainCqbrHeld = "CQBR Native Held";
        private const string MainCqbrBack = "CQBR Native Back";

        public static string InstallMainLeonCqbr()
        {
            const string scenePath = "Assets/GameScene.unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式安装主场景 CQBR。");
            }
            var scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException("GameScene 有未保存修改。");
            }
            try
            {
                var leon = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
                var animator = leon.GetComponent<Animator>();
                var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
                if (animator.runtimeAnimatorController != graph)
                {
                    throw new InvalidOperationException("GameScene 里昂的正式动画控制器不匹配。");
                }
                bool mounted = MountMainCqbr(leon);
                bool aimBound = BindMainCqbrAim(leon);
                int count = AddMainCqbrMotions(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
                if ((mounted || aimBound) && !EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("无法保存 GameScene 的 CQBR 装配。");
                }
                return $"CQBR 原版模型、{count} 个持枪状态、进退瞄、站蹲换弹及枪机轨道已装配。";
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static bool BindMainCqbrAim(LeonController leon)
        {
            var rifle = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(weapon => weapon.data != null && weapon.data.weaponType == WeaponType.Rifle);
            var rig = new SerializedObject(leon.GetComponent<LeonAnimRig>());
            var mechanisms = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4402_Mechanism.fbx")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            rig.FindProperty("nativeCqbrWeapon").objectReferenceValue = rifle;
            rig.FindProperty("nativeCqbrAimSpine").objectReferenceValue = Node(leon.gameObject, "spine upper");
            rig.FindProperty("nativeCqbrCarryMechanism").objectReferenceValue = mechanisms["CQBR_0543_Mechanism"];
            rig.FindProperty("nativeCqbrShotMechanism").objectReferenceValue = mechanisms["CQBR_0500_Mechanism"];
            rig.FindProperty("nativeCqbrLastMechanism").objectReferenceValue = mechanisms["CQBR_0500_wp4202h_Mechanism"];
            rig.FindProperty("nativeCqbrDryMechanism").objectReferenceValue = mechanisms["CQBR_0520_Mechanism"];
            rig.FindProperty("nativeCqbrEmptyMechanism").objectReferenceValue = mechanisms["CQBR_0160_wp4202h_Mechanism"];
            rig.FindProperty("nativeCqbrReloadMechanism").objectReferenceValue = mechanisms["CQBR_0810_Mechanism"];
            const string boltPath = "wp4402_Skeleton/root/_00/_01";
            var bolt = rifle.VisualModel.transform.Find(boltPath);
            var magazine = rifle.VisualModel.transform.Find("wp4402_Skeleton/root/_00/_04");
            if (bolt == null || magazine == null)
            {
                throw new InvalidOperationException("CQBR 原版枪体缺少枪机或弹匣骨骼。");
            }
            var open = mechanisms["CQBR_0160_wp4202h_Mechanism"];
            rig.FindProperty("nativeCqbrBolt").objectReferenceValue = bolt;
            rig.FindProperty("nativeCqbrMagazine").objectReferenceValue = magazine;
            rig.FindProperty("nativeCqbrOpenBoltPosition").vector3Value = new Vector3(BoltAxis("x"), BoltAxis("y"), BoltAxis("z"));

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var aim = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                    .OfType<AnimationClip>().Single(clip => clip.name == "CQBR_0160");
                aim.SampleAnimation(sample, 0f);
                var spine = Node(sample, "spine upper");
                var muzzle = Node(Node(sample, "wp4402_Model").gameObject, "RE4_Muzzle");
                rig.FindProperty("nativeCqbrAimForward").vector3Value = spine.InverseTransformDirection(muzzle.forward);
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

            float BoltAxis(string axis)
            {
                var binding = EditorCurveBinding.FloatCurve(boltPath, typeof(Transform), "m_LocalPosition." + axis);
                return AnimationUtility.GetEditorCurve(open, binding).Evaluate(0f);
            }
        }

        private static bool MountMainCqbr(LeonController leon)
        {
            var wrist = Node(leon.gameObject, "arm right wrist");
            var socket = wrist.Find("RE4_Grip/RE4_WeaponSocket");
            if (socket == null)
            {
                throw new InvalidOperationException("正式里昂缺少已校准的武器挂点。");
            }
            if (socket.Find(MainCqbrHeld) != null)
            {
                return false;
            }

            var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                .Single(item => item.data != null && item.data.weaponType == WeaponType.Rifle);
            var visual = new SerializedObject(leon.GetComponent<LeonWeaponVisualController>());
            var heldRenderers = visual.FindProperty("rifleVisualRenderers");
            var backRenderers = visual.FindProperty("rifleBackRenderers");
            var oldHeld = weapon.VisualModel.transform.Find("M16_Rifle_Model");
            var oldBack = Node(leon.gameObject, "M16 Assault Rifle").Find("M16_Rifle_Model");
            var oldMuzzle = weapon.muzzlePoint;
            Vector3 backMuzzlePosition = oldBack.TransformPoint(oldHeld.InverseTransformPoint(oldMuzzle.position));
            Quaternion backMuzzleRotation = oldBack.rotation * Quaternion.Inverse(oldHeld.rotation) * oldMuzzle.rotation;

            var sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var sourceModel = Node(sourceRig, "wp4402_Model");
            var held = Object.Instantiate(sourceModel.gameObject, socket, false);
            held.name = MainCqbrHeld;
            held.SetActive(true);
            var nativeMuzzle = Node(held, "RE4_Muzzle");
            if (nativeMuzzle == null)
            {
                throw new InvalidOperationException("原版 CQBR 缺少枪口挂点。");
            }

            var back = Object.Instantiate(sourceModel.gameObject, oldBack.parent, false);
            back.name = MainCqbrBack;
            back.SetActive(true);
            var newBackMuzzle = Node(back, "RE4_Muzzle");
            Quaternion localMuzzle = Quaternion.Inverse(back.transform.rotation) * newBackMuzzle.rotation;
            back.transform.rotation = backMuzzleRotation * Quaternion.Inverse(localMuzzle);
            back.transform.position += backMuzzlePosition - newBackMuzzle.position;

            ReplaceRenderers(heldRenderers, held, false);
            ReplaceRenderers(backRenderers, back, backRenderers.arraySize > 0
                && ((Renderer)backRenderers.GetArrayElementAtIndex(0).objectReferenceValue).enabled);
            visual.ApplyModifiedPropertiesWithoutUndo();

            var legacyWeapon = new SerializedObject(weapon);
            legacyWeapon.FindProperty("visualModel").objectReferenceValue = held;
            legacyWeapon.FindProperty("muzzlePoint").objectReferenceValue = nativeMuzzle;
            legacyWeapon.ApplyModifiedPropertiesWithoutUndo();
            oldMuzzle.SetPositionAndRotation(nativeMuzzle.position, nativeMuzzle.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(oldMuzzle);

            var rig = leon.GetComponent<LeonAnimRig>();
            var aim = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Single(clip => clip.name == "CQBR_0160");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(sourceRig, preview);
                aim.SampleAnimation(sample, 0f);
                var sampledModel = Node(sample, "wp4402_Model");
                rig.rifleRightTarget = Grip("CQBR Grip Right", "arm right wrist");
                rig.rifleLeftTarget = Grip("CQBR Grip Left", "arm left wrist");

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

        private static void ReplaceRenderers(SerializedProperty references, GameObject model, bool visible)
        {
            for (int index = 0; index < references.arraySize; index++)
            {
                if (references.GetArrayElementAtIndex(index).objectReferenceValue is Renderer previous)
                {
                    previous.enabled = false;
                    EditorUtility.SetDirty(previous);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(previous);
                }
            }
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            references.arraySize = renderers.Length;
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = visible;
                references.GetArrayElementAtIndex(index).objectReferenceValue = renderers[index];
            }
        }

        public static int RefreshNativeCqbrCarry()
        {
            var demo = AssetDatabase.LoadAssetAtPath<AnimatorController>(Generated + "/Leon.controller");
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            AddFirearmLayer(demo, rig, true);
            AssetDatabase.SaveAssetIfDirty(demo);
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            int count = AddMainCqbrMotions(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
            UpdateCqbrBackMount(graph, rig);
            return count;
        }

        private static void UpdateCqbrBackMount(AnimatorController graph, GameObject prefab)
        {
            var data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Scripts/Combat/WeaponData_Rifle.asset");
            var draw = graph.animationClips.Distinct().Single(clip => clip.name == "MainCqbr_Draw");
            var carry = (AnimationClip)graph.layers[0].stateMachine.states
                .Single(child => child.state.name == "NativeCqbrIdle").state.motion;
            var mechanism = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4402_Mechanism.fbx")
                .OfType<AnimationClip>().Single(clip => clip.name == "CQBR_0543_Mechanism");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                carry.SampleAnimation(sample, 0f);
                draw.SampleAnimation(sample, ChapterOneRootActor.RifleDrawVisibleFrame / draw.frameRate);
                var model = Node(sample, "wp4402_Model");
                mechanism.SampleAnimation(model.gameObject, 0f);
                var gun = model.Find("wp4402_Skeleton/root/_00");
                var spine = Node(sample, "spine upper");
                data.stowedRootInSpine = new Pose(spine.InverseTransformPoint(gun.position),
                    Quaternion.Inverse(spine.rotation) * gun.rotation);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static int RefreshMainCqbrInjuryGait()
        {
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            int count = AddMainCqbrMotions(graph, injuredOnly: true);
            AssetDatabase.SaveAssetIfDirty(graph);
            return count;
        }

        private static int AddMainCqbrMotions(AnimatorController graph, bool injuredOnly = false, bool idleOnly = false)
        {
            var demo = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Leon.controller")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var carry = demo["CQBR_Carry"];
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainCqbrBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainCqbrBank);
            }
            var bankClips = AssetDatabase.LoadAllAssetsAtPath(MainCqbrBank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var baseLayer = graph.layers[0].stateMachine;
            var states = baseLayer.states.ToDictionary(child => child.state.name, child => child.state);
            int count = 0;
            if (!injuredOnly)
            {
                Carry(LeonAnimatorDriver.NativeIdleName, LeonAnimatorDriver.NativeCqbrIdleName);
            }
            if (!idleOnly)
            {
                Carry(LeonAnimatorDriver.NativeHurtIdleName, LeonAnimatorDriver.NativeCqbrHurtIdleName);
                var injuryGaits = LeonAnimatorDriver.NativeGaits.Where(gait => gait.name is "HurtWalk" or "HurtJog");
                foreach (var gait in injuryGaits)
                {
                    CarryGait(gait.name);
                }
            }
            if (injuredOnly || idleOnly)
            {
                EditorUtility.SetDirty(baseLayer);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(bank);
                return count;
            }

            foreach (var gait in LeonAnimatorDriver.NativeGaits.Take(3))
            {
                CarryGait(gait.name);
            }
            ReplaceAction("Equip", demo["CQBR_0543_DrawTiming"], "MainCqbr_Draw");
            ReplaceAction("Holster", demo["CQBR_0551"], "MainCqbr_Holster");
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            Reload(LeonAnimatorDriver.NativeCqbrStandingReload, LeonAnimatorDriver.NativeCqbrStandingReloadAction, source["CQBR_0810"], carry);
            Reload(LeonAnimatorDriver.NativeCqbrCrouchReload, LeonAnimatorDriver.NativeCqbrCrouchReloadAction, source["LeonFireExtra_1810_wp4402"]);
            var aimMachine = graph.layers[LeonAnimatorDriver.LayerRifleCrouch].stateMachine;
            var aimStates = aimMachine.states.ToDictionary(child => child.state.name, child => child.state);
            var raise = demo["CQBR_0140_GripCalibrated"];
            var lower = demo["CQBR_0158_GripCalibrated"];
            Aim("CarryUpper", carry, hold: true);
            Aim("Raise", raise);
            Aim("Lower", lower);
            Aim("ReverseRaise", raise, reverse: true);
            Aim("ReverseLower", lower, reverse: true);
            Aim("AimHold", source["CQBR_0160"], hold: true);
            Aim("Shot", demo["CQBR_0500_BurstCalibrated"], shot: true);
            Aim("LastShot", demo["CQBR_0500_wp4202h_BurstCalibrated"], shot: true);
            Aim("DryFire", source["CQBR_0520"]);
            EditorUtility.SetDirty(aimMachine);
            EditorUtility.SetDirty(baseLayer);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(bank);
            return count;

            void Reload(string name, int action, AnimationClip motion, AnimationClip carryPose = null)
            {
                if (motion.frameRate != 60f || Mathf.RoundToInt(motion.length * motion.frameRate) != LeonAnimatorDriver.NativeCqbrReloadFrames)
                {
                    throw new InvalidOperationException("CQBR 换弹片段与已核对的源帧范围不匹配。");
                }

                var machine = graph.layers[LeonAnimatorDriver.LayerWeaponAction].stateMachine;
                var empty = machine.states.Single(child => child.state.name == "Empty").state;
                var state = machine.states.Select(child => child.state).SingleOrDefault(item => item.name == name);
                if (state == null)
                {
                    state = machine.AddState(name);
                    var behavior = state.AddStateMachineBehaviour<WeaponActionSMB>();
                    behavior.actionType = WeaponActionType.Reload;
                    behavior.completeNormalizedTime = 0.99f;
                    var enter = empty.AddTransition(state);
                    enter.hasExitTime = false;
                    enter.hasFixedDuration = true;
                    enter.AddCondition(AnimatorConditionMode.Equals, action, "WeaponAction");
                    var exit = state.AddTransition(empty);
                    exit.hasExitTime = false;
                    exit.hasFixedDuration = true;
                    exit.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponAction");
                    EditorUtility.SetDirty(machine);
                    EditorUtility.SetDirty(empty);
                }
                // 空状态混合会把绑定姿势带入双臂。换弹只通过图层权重淡入淡出。
                var enterReload = empty.transitions.Single(transition => transition.destinationState == state);
                var exitReload = state.transitions.Single(transition => transition.destinationState == empty);
                enterReload.duration = 0f;
                exitReload.duration = 0f;
                EditorUtility.SetDirty(enterReload);
                EditorUtility.SetDirty(exitReload);
                var clip = CreateArmClip(motion);
                if (carryPose != null)
                {
                    BlendMainCqbrReloadEnds(clip, motion, carryPose);
                }
                state.motion = Store(clip, name);
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
            }

            void Aim(string pose, AnimationClip motion, bool reverse = false, bool hold = false, bool shot = false)
            {
                string name = LeonAnimatorDriver.NativeCqbrPoseName(pose);
                var clip = new AnimationClip { frameRate = motion.frameRate };
                var bindings = ArmBindings(motion);
                var curves = bindings.Select(binding =>
                {
                    var curve = AnimationUtility.GetEditorCurve(motion, binding);
                    if (hold)
                    {
                        return AnimationCurve.Constant(0f, 1f, curve.Evaluate(0f));
                    }
                    if (reverse)
                    {
                        return new AnimationCurve(curve.keys.Reverse().Select(key => new Keyframe(
                            motion.length - key.time, key.value, -key.outTangent, -key.inTangent)).ToArray());
                    }
                    return curve;
                }).ToArray();
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = hold;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                if (!aimStates.TryGetValue(name, out var state))
                {
                    state = aimMachine.AddState(name);
                }
                state.motion = Store(clip, name);
                state.speed = 1f;
                if (shot)
                {
                    state.speed = motion.length / LeonAnimatorDriver.NativeCqbrShotSeconds;
                }
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
            }

            void CarryGait(string pace)
            {
                foreach (string phase in new[] { "Start", "Loop", "Stop" })
                {
                    Carry(LeonAnimatorDriver.NativeGaitStateName(pace, phase),
                        LeonAnimatorDriver.NativeCqbrStateName(pace, phase));
                }
            }

            void Carry(string sourceName, string stateName)
            {
                var motion = (AnimationClip)states[sourceName].motion;
                var clip = Object.Instantiate(motion);
                var bindings = ArmBindings(carry);
                var curves = bindings.Select(binding =>
                {
                    var curve = AnimationUtility.GetEditorCurve(carry, binding);
                    return AnimationCurve.Constant(0f, motion.length, curve.Evaluate(0f));
                }).ToArray();
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                if (!states.TryGetValue(stateName, out var state))
                {
                    state = baseLayer.AddState(stateName, new Vector3(1990 + count % 3 * 225, 60 + count / 3 * 90));
                }
                state.motion = Store(clip, stateName);
                state.writeDefaultValues = states[sourceName].writeDefaultValues;
                EditorUtility.SetDirty(state);
                count++;
            }

            void ReplaceAction(string stateName, AnimationClip source, string clipName)
            {
                var machine = graph.layers[LeonAnimatorDriver.LayerWeaponAction].stateMachine;
                var state = machine.states.Single(child => child.state.name == stateName).state;
                if (stateName == "Holster")
                {
                    // 空状态没有蹲姿。直接进入收枪片，淡入交给武器动作层的权重。
                    var empty = machine.states.Single(child => child.state.name == "Empty").state;
                    var enter = empty.transitions.Single(transition => transition.destinationState == state);
                    enter.duration = 0f;
                    EditorUtility.SetDirty(enter);
                }
                var tree = (BlendTree)state.motion;
                if (tree.blendParameter != "WeaponType"
                    && !(stateName == "Holster" && tree.blendParameter == LeonAnimatorDriver.HolsterSourceWeapon))
                {
                    throw new InvalidOperationException("武器动作选择器契约已变更。");
                }
                var children = tree.children;
                int index = Array.FindIndex(children, child => Mathf.Approximately(child.threshold, 1f));
                if (index < 0)
                {
                    throw new InvalidOperationException("缺少 CQBR 武器动作分支。");
                }
                children[index].motion = Store(CreateArmClip(source), clipName);
                // 正式切枪动作原有 96 帧的展示时长；只给 CQBR 分支保持这个节奏。
                children[index].timeScale = ChapterOneRootActor.RifleDrawFrames / 96f;
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }

            AnimationClip Store(AnimationClip clip, string name)
            {
                clip.name = name;
                clip.EnsureQuaternionContinuity();
                if (bankClips.TryGetValue(name, out var previous))
                {
                    EditorUtility.CopySerialized(clip, previous);
                    Object.DestroyImmediate(clip);
                    EditorUtility.SetDirty(previous);
                    return previous;
                }
                AssetDatabase.AddObjectToAsset(clip, bank);
                EditorUtility.SetDirty(bank);
                return clip;
            }
        }

        private static AnimationClip CreateArmClip(AnimationClip source)
        {
            var clip = new AnimationClip { frameRate = source.frameRate };
            var bindings = ArmBindings(source);
            AnimationUtility.SetEditorCurves(clip, bindings,
                bindings.Select(binding => AnimationUtility.GetEditorCurve(source, binding)).ToArray());
            return clip;
        }

        private static void BlendMainCqbrReloadEnds(AnimationClip clip, AnimationClip source, AnimationClip carry)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var tracks = ArmBindings(source).Where(binding => binding.type == typeof(Transform))
                    .Select(binding => binding.path).Distinct().Select(path => new AimJointTrack(path, rig.transform.Find(path)))
                    .Where(track => track.joint != null).ToArray();
                carry.SampleAnimation(rig, 0f);
                var carryPose = tracks.Select(track => track.LocalPose).ToArray();
                int last = LeonAnimatorDriver.NativeCqbrReloadFrames;
                for (int frame = 0; frame <= last; frame++)
                {
                    float time = frame / source.frameRate;
                    source.SampleAnimation(rig, time);
                    // 只收顺起手和收势。插匣、枪机及左手接触区间保持原版帧序。
                    float weight = Mathf.Max(Mathf.SmoothStep(1f, 0f, frame / 21f),
                        Mathf.SmoothStep(0f, 1f, (frame - 210f) / (last - 210f)));
                    for (int i = 0; i < tracks.Length; i++)
                    {
                        var joint = tracks[i].joint;
                        joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.localPosition, carryPose[i].position, weight),
                            Quaternion.Slerp(joint.localRotation, carryPose[i].rotation, weight));
                        tracks[i].Record(time);
                    }
                }
                WriteRifleUpperTracks(clip, tracks, source.frameRate);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static EditorCurveBinding[] ArmBindings(AnimationClip clip) =>
            AnimationUtility.GetCurveBindings(clip).Where(binding =>
                binding.path.Contains("/arm left shoulder", StringComparison.Ordinal)
                || binding.path.Contains("/arm right shoulder", StringComparison.Ordinal)).ToArray();
    }
}
#endif
