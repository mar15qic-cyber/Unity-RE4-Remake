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
        private const string MainPistolSwitchBank = Generated + "/MainLeonPistolSwitchMotions.asset";
        private const string PistolModelName = "wp4000_Model";
        private const string PistolHolsteredModelName = "SG09R Native Holster";
        private const string PistolHolsteredMeshName = "24_pistol_0.45_0_0";
        private const string PistolSupportName = "SG09R Grip Left";

        public static string InstallMainLeonPistolSwitch()
        {
            const string path = "Assets/GameScene.unity";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式安装主场景 SG-09 R。");
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
                var leon = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
                var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
                if (leon.GetComponent<Animator>().runtimeAnimatorController != graph)
                {
                    throw new InvalidOperationException("GameScene 里昂的正式动画控制器不匹配。");
                }
                var (holsterPose, supportPose) = AddMainPistolSwitchMotions(graph);
                bool mounted = BindMainPistol(leon, holsterPose, supportPose);
                var (aimForward, ready, empty) = AddMainPistolAimMotions(graph);
                mounted |= BindMainPistolAim(leon, aimForward, ready, empty);
                var (emptyReload, tacticalReload) = AddMainPistolReloadMotions(graph);
                mounted |= BindMainPistolReload(leon, emptyReload, tacticalReload);
                AssetDatabase.SaveAssetIfDirty(graph);
                if (mounted && !EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("无法保存 GameScene 的 SG-09 R 装配。");
                }
                return "SG-09 R 原版枪体、短拔收、站姿进退瞄、射击、套筒及站蹲换弹已装配到主场景。";
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        public static void RefreshMainLeonPistolSwitch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新手枪拔收动作。");
            }

            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            AddMainPistolSwitchMotions(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        public static string RefreshVillagePistolHolster()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请在编辑模式更新正式枪套。");
            }
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/RE4ChapterOneVillage/Lighting/VillageBattleLighting.unity" || scene.isDirty)
            {
                throw new InvalidOperationException("正式村庄必须是当前已保存的场景。");
            }
            var leon = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
            var (holster, support) = AddMainPistolSwitchMotions(graph);
            if (BindMainPistol(leon, holster, support) && !EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("未能保存正式枪套绑定。");
            }
            AssetDatabase.SaveAssetIfDirty(graph);
            return "已校准正式手枪枪套和拔收交接";
        }

        private static bool BindMainPistol(LeonController leon, Pose holsterPose, Pose supportPose)
        {
            var thigh = Node(leon.gameObject, "leg right thigh twist");
            var socket = Node(leon.gameObject, "arm right wrist").Find("RE4_Grip/RE4_WeaponSocket");
            if (socket == null)
            {
                throw new InvalidOperationException("正式里昂缺少已校准的武器挂点。");
            }
            var held = socket.Find(PistolModelName);
            bool changed = held == null;
            if (changed)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
                var source = Node(prefab, PistolModelName);
                held = Object.Instantiate(source.gameObject, socket, false).transform;
                held.name = PistolModelName;
                held.gameObject.SetActive(true);
                var holstered = Object.Instantiate(source.gameObject, thigh, false);
                holstered.name = PistolHolsteredModelName;
                holstered.SetActive(true);
                new GameObject(PistolSupportName).transform.SetParent(held, false);

                var weapon = leon.GetComponentsInChildren<WeaponBase>(true)
                    .Single(item => item.data != null && item.data.weaponType == WeaponType.Pistol);
                var visual = new SerializedObject(leon.GetComponent<LeonWeaponVisualController>());
                var renderers = visual.FindProperty("pistolVisualRenderers");
                bool wasVisible = Enumerable.Range(0, renderers.arraySize)
                    .Any(i => renderers.GetArrayElementAtIndex(i).objectReferenceValue is Renderer renderer && renderer.enabled);
                ReplaceRenderers(renderers, held.gameObject, wasVisible);
                ReplaceRenderers(visual.FindProperty("holsteredPistolRenderers"), holstered, !wasVisible);
                var objects = visual.FindProperty("pistolVisualObjects");
                objects.arraySize = 1;
                objects.GetArrayElementAtIndex(0).objectReferenceValue = held.gameObject;
                visual.ApplyModifiedPropertiesWithoutUndo();

                var properties = new SerializedObject(weapon);
                properties.FindProperty("visualModel").objectReferenceValue = held.gameObject;
                properties.FindProperty("muzzlePoint").objectReferenceValue = Node(held.gameObject, "RE4_Muzzle");
                properties.ApplyModifiedPropertiesWithoutUndo();
            }

            var rig = leon.GetComponent<LeonAnimRig>();
            var support = held.Find(PistolSupportName);
            if (rig.pistolLeftTarget != support)
            {
                rig.pistolLeftTarget = support;
                EditorUtility.SetDirty(rig);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                changed = true;
            }
            // Final IK 会在求解时读取自己的 target，它必须与动画驱动使用同一个握点。
            if (rig.pistolLeftIK != null && rig.pistolLeftIK.solver.target != support)
            {
                rig.pistolLeftIK.solver.target = support;
                EditorUtility.SetDirty(rig.pistolLeftIK);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig.pistolLeftIK);
                changed = true;
            }
            var holsteredModel = Node(leon.gameObject, PistolHolsteredModelName);
            if (holsteredModel.parent != thigh)
            {
                Undo.SetTransformParent(holsteredModel, thigh, "手枪归入右腿枪套");
                changed = true;
            }
            changed |= BindPose(holsteredModel, holsterPose);
            changed |= BindPose(support, supportPose);
            // 角色自带的枪套内手枪已经按枪套形状蒙皮，跟随腿部扭转。
            var fittedPistol = Node(leon.gameObject, PistolHolsteredMeshName).GetComponent<SkinnedMeshRenderer>();
            var visuals = new SerializedObject(leon.GetComponent<LeonWeaponVisualController>());
            var stowedRenderers = visuals.FindProperty("holsteredPistolRenderers");
            if (stowedRenderers.arraySize != 1 || stowedRenderers.GetArrayElementAtIndex(0).objectReferenceValue != fittedPistol)
            {
                foreach (var renderer in holsteredModel.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                fittedPistol.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(fittedPistol);
                stowedRenderers.arraySize = 1;
                stowedRenderers.GetArrayElementAtIndex(0).objectReferenceValue = fittedPistol;
                visuals.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
            }
            return changed;

            static bool BindPose(Transform target, Pose pose)
            {
                if ((target.localPosition - pose.position).sqrMagnitude < 0.00000001f
                    && Mathf.Abs(Quaternion.Dot(target.localRotation, pose.rotation)) > 0.999999f)
                {
                    return false;
                }
                target.SetLocalPositionAndRotation(pose.position, pose.rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                return true;
            }
        }

        private static (Pose holster, Pose support) AddMainPistolSwitchMotions(AnimatorController graph)
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab");
            var source = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().Where(clip => clip.name.StartsWith("SG09R_", StringComparison.Ordinal))
                .ToDictionary(clip => clip.name);
            var restMechanism = AssetDatabase.LoadAllAssetsAtPath(Art + "/wp4000_Mechanism.fbx")
                .OfType<AnimationClip>().Single(clip => clip.name == "SG09R_0551_Mechanism");
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(MainPistolSwitchBank);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(bank, MainPistolSwitchBank);
            }
            var saved = AssetDatabase.LoadAllAssetsAtPath(MainPistolSwitchBank)
                .OfType<AnimationClip>().ToDictionary(clip => clip.name);
            var layers = graph.layers;
            int layer = LeonAnimatorDriver.LayerWeaponAction;
            var machine = layers[layer].stateMachine;
            var states = machine.states.ToDictionary(child => child.state.name, child => child.state);
            var model = Node(rig, PistolModelName);
            string modelPath = AnimationUtility.CalculateTransformPath(model, rig.transform);
            layers[layer].avatarMask = AddMainWeaponMaskPaths(layers[layer].avatarMask, MainW870Mask, rig, model);
            graph.layers = layers;

            var idle = (AnimationClip)layers[0].stateMachine.states.Single(child => child.state.name == "NativePistolIdle").state.motion;
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var sample = (GameObject)PrefabUtility.InstantiatePrefab(rig, preview);
                var held = Node(sample, PistolModelName);
                var spine = Node(sample, "spine upper");
                var thigh = Node(sample, "leg right thigh twist");
                var left = Node(sample, "arm left wrist");
                var right = new ChapterOneArmAim.Arm(Node(sample, "arm right wrist"));
                var draw = source["SG09R_0541"];
                draw.SampleAnimation(sample, LeonAnimatorDriver.PistolDrawContactFrame / 60f);
                var fittedPistol = Node(sample, PistolHolsteredMeshName).GetComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh();
                fittedPistol.BakeMesh(mesh);
                Vector3 fittedCenter = fittedPistol.transform.TransformPoint(mesh.bounds.center);
                Object.DestroyImmediate(mesh);
                var bounds = held.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.bounds).ToArray();
                var heldBounds = bounds[0];
                for (int index = 1; index < bounds.Length; index++)
                {
                    heldBounds.Encapsulate(bounds[index]);
                }
                var anchor = new Pose(thigh.InverseTransformPoint(fittedCenter + held.position - heldBounds.center),
                    Quaternion.Inverse(thigh.rotation) * held.rotation);
                source["SG09R_0160"].SampleAnimation(sample, 0f);
                var support = new Pose(held.InverseTransformPoint(left.position), Quaternion.Inverse(held.rotation) * left.rotation);

                var equip = AddState(LeonAnimatorDriver.PistolEquipState, WeaponActionType.Equip,
                    draw, LeonAnimatorDriver.PistolDrawFrames, LeonAnimatorDriver.PistolDrawContactFrame, 995f);
                var holster = AddState(LeonAnimatorDriver.PistolHolsterState, WeaponActionType.Holster,
                    source["SG09R_0551"], LeonAnimatorDriver.PistolHolsterFrames, LeonAnimatorDriver.PistolStowFrame, 1240f);
                var empty = states["Empty"];
                Link(empty, holster, LeonAnimatorDriver.PistolHolsterAction, 0f);
                Link(empty, equip, LeonAnimatorDriver.PistolEquipAction, 0f);
                Link(states["Holster"], equip, LeonAnimatorDriver.PistolEquipAction, 0.18f);
                Link(holster, states["Equip"], (int)WeaponActionType.Equip, 0.16f);
                Link(holster, states["KnifeTake"], (int)WeaponActionType.KnifeTake, 0.16f);
                Link(holster, empty, 0, 0f);
                Link(equip, empty, 0, 0f);
                if (states.TryGetValue(LeonAnimatorDriver.W870HolsterName, out var w870Holster))
                {
                    Link(w870Holster, equip, LeonAnimatorDriver.PistolEquipAction, 0.18f);
                }
                if (states.TryGetValue(LeonAnimatorDriver.W870EquipName, out var w870Equip))
                {
                    Link(holster, w870Equip, LeonAnimatorDriver.W870EquipAction, 0.15f);
                }
                EditorUtility.SetDirty(machine);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(bank);
                return (anchor, support);

                AnimatorState AddState(string name, WeaponActionType action, AnimationClip raw, int frames, int contact, float x)
                {
                    if (raw.frameRate != 60f || Mathf.RoundToInt(raw.length * raw.frameRate) != frames)
                    {
                        throw new InvalidOperationException("SG-09 R 拔收枪源片的帧范围已变化。");
                    }
                    var tracks = ArmBindings(raw).Where(binding => binding.type == typeof(Transform))
                        .Select(binding => binding.path).Distinct()
                        .Select(path => new AimJointTrack(path, sample.transform.Find(path)))
                        .Where(track => track.joint != null).ToArray();
                    idle.SampleAnimation(sample, 0f);
                    var idlePose = tracks.Select(track => track.LocalPose).ToArray();
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float time = frame / raw.frameRate;
                        raw.SampleAnimation(sample, time);
                        float idleWeight;
                        if (action == WeaponActionType.Holster)
                        {
                            idleWeight = Mathf.SmoothStep(1f, 0f, frame / 11f);
                        }
                        else
                        {
                            float entering = Mathf.SmoothStep(1f, 0f, frame / 6f);
                            float settling = Mathf.SmoothStep(0f, 1f, (frame - 37f) / (frames - 37f));
                            idleWeight = Mathf.Max(entering, settling);
                        }
                        if (idleWeight > 0f)
                        {
                            for (int index = 0; index < tracks.Length; index++)
                            {
                                var joint = tracks[index].joint;
                                joint.SetLocalPositionAndRotation(Vector3.Lerp(joint.localPosition, idlePose[index].position, idleWeight),
                                    Quaternion.Slerp(joint.localRotation, idlePose[index].rotation, idleWeight));
                            }
                        }

                        // 接触帧两边保留一个源帧的握点，避免渲染采样跨帧时枪体跳动。
                        float anchorWeight = Mathf.SmoothStep(0f, 1f, (frame - contact + 7f) / 6f)
                            * Mathf.SmoothStep(1f, 0f, (frame - contact - 1f) / 6f);
                        if (anchorWeight > 0f)
                        {
                            var position = Vector3.Lerp(held.position, thigh.TransformPoint(anchor.position), anchorWeight);
                            var rotation = Quaternion.Slerp(held.rotation, thigh.rotation * anchor.rotation, anchorWeight);
                            var correction = rotation * Quaternion.Inverse(held.rotation);
                            right.Solve(position + correction * (right.wrist.position - held.position),
                                correction * right.wrist.rotation, spine.right);
                        }
                        foreach (var track in tracks)
                        {
                            track.Record(time);
                        }
                    }
                    var clip = new AnimationClip { name = name };
                    WriteRifleUpperTracks(clip, tracks, raw.frameRate);
                    var bindings = AnimationUtility.GetCurveBindings(restMechanism);
                    var curves = bindings.Select(binding =>
                        AnimationCurve.Constant(0f, raw.length, AnimationUtility.GetEditorCurve(restMechanism, binding).Evaluate(0f))).ToArray();
                    for (int index = 0; index < bindings.Length; index++)
                    {
                        if (bindings[index].path.Length == 0)
                        {
                            bindings[index].path = modelPath;
                        }
                        else
                        {
                            bindings[index].path = modelPath + "/" + bindings[index].path;
                        }
                    }
                    AnimationUtility.SetEditorCurves(clip, bindings, curves);
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
                        state = machine.AddState(name, new Vector3(x, 235f));
                        var behavior = state.AddStateMachineBehaviour<WeaponActionSMB>();
                        behavior.actionType = action;
                        behavior.completeNormalizedTime = 0.99f;
                    }
                    state.motion = clip;
                    state.writeDefaultValues = false;
                    EditorUtility.SetDirty(state);
                    return state;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
#endif
