#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string Home = "Assets/RE4ChapterOneAnimationLoop";
        private const string Art = "Assets/RE4ChapterOneAnimationReview/Art";
        private const string Generated = Home + "/Generated";
        private const string ScenePath = Home + "/ChapterOneAnimationLoop.unity";
        private const string Source = @"D:\RE4素材\Chapter1Animation";
        private const string LeonPath = "Assets/Prefab/LeonR4R.prefab";
        private static readonly Dictionary<string, AnimationClip[]> Clips = new();
        private static readonly List<string> Audit = new();
        private static readonly Dictionary<string, string[]> PreviousRows = new();

        [MenuItem("工具/RE4/生成第一章动画闭环")]
        public static string Build()
        {
            if (EditorApplication.isPlaying || SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("生成前请退出播放并关闭已有的闭环场景。");

            Clips.Clear();
            Audit.Clear();
            PreviousRows.Clear();
            string auditPath = Source + "/Unity闭环接线.tsv";
            if (File.Exists(auditPath))
            {
                foreach (string line in File.ReadLines(auditPath).Skip(1))
                {
                    string[] row = line.Split('\t');
                    if (row.Length == 10)
                        PreviousRows[row[0] + "/" + row[1]] = row;
                }
            }
            Audit.Add("角色\t动作状态\t来源动作\t时长\t帧率\t绑定轨道\t缺失轨道\t平面根位移（米）\t接触帧\t接线结果");
            Directory.CreateDirectory(Generated + "/Clips");
            Directory.CreateDirectory(Generated + "/Native");
            Directory.CreateDirectory(Generated + "/Textures");
            AssetDatabase.Refresh();
            ImportAdditionalArt();

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                string[][] rows = File.ReadLines(Source + "/ReviewTakes.tsv")
                    .Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Split('\t')).ToArray();
                var actors = new Dictionary<string, ChapterOneRootActor>();
                GameObject malePrefab = null;
                foreach (string role in new[] { "Leon", "Ganado", "GanadoMale", "Salvador" })
                {
                    GameObject rig = CreateRig(role);
                    string clipPath = Generated + "/Clips/" + role + "_Clips.asset";
                    var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(clipPath);
                    if (library == null)
                    {
                        library = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                        AssetDatabase.CreateAsset(library, clipPath);
                    }
                    var existingClips = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                        .ToDictionary(c => c.name);
                    List<ChapterOneRootActor.Take> takes;
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        takes = BuildTakes(role, rig, rows, library, existingClips);
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                    AssetDatabase.SaveAssetIfDirty(library);
                    BuildAvatarAndController(role, rig, takes);
                    actors[role] = ConfigureActor(role, rig, takes);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(rig, Generated + "/" + role + ".prefab");
                    if (role == "GanadoMale")
                        malePrefab = prefab;
                }

                var torchRig = (GameObject)PrefabUtility.InstantiatePrefab(malePrefab, scene);
                PrefabUtility.UnpackPrefabInstance(torchRig, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                torchRig.name = "持火把村民";
                var torch = AttachTorch(torchRig);
                var torchActor = torchRig.GetComponent<ChapterOneRootActor>();
                torchActor.Configure("持火把村民", actors["GanadoMale"].Takes.ToArray(), new[] { torch },
                    Node(torchRig, "L_Foot"), Node(torchRig, "R_Foot"), Node(torchRig, "R_Hand"), 1, null);
                PrefabUtility.SaveAsPrefabAsset(torchRig, Generated + "/GanadoTorch.prefab");

                Place(actors["Leon"], new Vector3(0, 0.04f, -5.1f), 0);
                Place(actors["Ganado"], new Vector3(-4.3f, 0.04f, 1.8f), 155);
                Place(actors["GanadoMale"], new Vector3(4.2f, 0.04f, 3.1f), 195);
                Place(actors["Salvador"], new Vector3(0, 0.04f, 9.7f), 180);
                Place(torchActor, new Vector3(5.5f, 0.04f, 7.1f), 210);
                Transform terrain = MakeGround();

                var camera = new GameObject("越肩相机").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.fieldOfView = 57f;
                camera.nearClipPlane = 0.07f;
                camera.gameObject.AddComponent<AudioListener>();
                var sun = new GameObject("午后光线").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1, 0.94f, 0.82f);
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(44f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.39f, 0.42f, 0.45f);

                Transform pickup = Box("弹药拾取", new Vector3(-2f, 0.16f, -3.3f), new Vector3(0.3f, 0.23f, 0.46f),
                    new Color(0.75f, 0.32f, 0.15f), false).transform;
                Transform crate = Box("踹箱验证", new Vector3(2.1f, 0.46f, -3.5f), new Vector3(0.9f, 0.9f, 0.9f),
                    new Color(0.42f, 0.30f, 0.18f)).transform;
                var windows = MakeTraversalFixtures(terrain, null);
                var door = CreateModel("sm80_500");
                door.name = "原版木门";
                door.transform.SetPositionAndRotation(new Vector3(-6.5f, 0, 0), Quaternion.Euler(0, 90, 0));

                var flow = new GameObject("第一章动画闭环").AddComponent<ChapterOneLoop>();
                flow.Configure(actors["Leon"], new[]
                {
                    Threat(actors["Ganado"], "Ganado", "Ganado_0003", "Ganado_0200"),
                    Threat(actors["GanadoMale"], "Ganado", "Ganado_0001", "Ganado_0200"),
                    Threat(torchActor, "Ganado", "GanadoTorch_1010", "Ganado_0200", "wp5805"),
                    Threat(actors["Salvador"], "Salvador", "Salvador_0020", "Salvador_0200", "wp5300", 180),
                }, camera, pickup, crate, windows.level, door, Find("sm80_500_DoorMotion", "sm80_500_0010_double_door_open_F"),
                    Find("sm80_500_DoorMotion", "sm80_500_0012_double_door_close_F"),
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RE4CombatSample/MuzzleFlash.prefab"), BuildNavigation(terrain), 1);
                flow.ConfigureTraversal(windows.level, windows.elevated);

                ChapterOneGaitCycles.Apply();
                RefreshLegacyLegBite();
                RefreshLegacyCrawl();
                RefreshLegacySlotReactions();
                ChapterOneMeleeMotion.Refresh();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("无法保存闭环场景。");
                File.WriteAllLines(auditPath, Audit);
                WriteSlotReport();
                return $"{ScenePath}\n{Audit.Count - 1} 条动作接线记录";
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void WriteSlotReport()
        {
            var connected = new HashSet<string>(Audit.Skip(1).Select(r => r.Split('\t'))
                .Where(r => r[9].StartsWith("原版", StringComparison.Ordinal))
                .Select(r => r[0] + "|" + r[1]));
            var legacyBites = new HashSet<string>(Audit.Skip(1).Select(r => r.Split('\t'))
                .Where(r => r[9] == LegBiteConnected).Select(r => r[0] + "|" + r[1]));
            var lines = new List<string> { "旧控制器或入口\t层\t旧槽位\t旧片段\t候选\t新样例覆盖" };
            foreach (string line in File.ReadLines(Source + "/工程逐槽对照.tsv").Skip(1))
            {
                string[] row = line.Split('\t');
                if (row.Length < 10)
                    continue;
                if (row.Length > 16 && row[15] == LegBiteConnected)
                {
                    string legacy = row[16].Split(' ').Last();
                    string role = "Leon";
                    if (legacy.StartsWith("Ganado", StringComparison.Ordinal))
                    {
                        role = "Ganado";
                    }
                    if (legacyBites.Contains(role + "|" + legacy))
                    {
                        string asset = Generated + "/Clips/" + role + "_Clips.asset/" + legacy;
                        lines.Add(string.Join("\t", row[0], row[1], row[2], row[3], asset, LegBiteConnected));
                        continue;
                    }
                }
                string alias = row[9].Split('/').Last();
                string target = alias.StartsWith("Ganado", StringComparison.Ordinal) ? "Ganado"
                    : alias.StartsWith("Salvador", StringComparison.Ordinal) ? "Salvador" : "Leon";
                string status = connected.Contains(target + "|" + alias)
                    ? "同候选已生成新样例状态"
                    : "原槽位待映射或补片";
                lines.Add(string.Join("\t", row[0], row[1], row[2], row[3], row[9], status));
            }
            ApplySlotResolutions(lines);
            File.WriteAllLines(Source + "/Unity工程逐槽接线.tsv", lines);
        }

        public static int RefreshSlotResolutions()
        {
            string path = Source + "/Unity工程逐槽接线.tsv";
            var lines = File.ReadAllLines(path);
            int count = ApplySlotResolutions(lines);
            File.WriteAllLines(path, lines);
            return count;
        }

        private static int ApplySlotResolutions(IList<string> lines)
        {
            string path = Source + "/逐槽核销.tsv";
            if (!File.Exists(path))
            {
                return 0;
            }

            var resolutions = File.ReadLines(path).Skip(1)
                .Select(line => line.Split('\t')).ToDictionary(SlotKey, StringComparer.Ordinal);
            int count = 0;
            for (int index = 1; index < lines.Count; index++)
            {
                string[] row = lines[index].Split('\t');
                if (resolutions.TryGetValue(SlotKey(row), out var resolution))
                {
                    lines[index] = string.Join("\t", row[0], row[1], row[2], row[3], resolution[4], resolution[5]);
                    count++;
                }
            }
            if (count != resolutions.Count)
            {
                throw new InvalidOperationException("逐槽核销记录与当前控制器快照不匹配。");
            }
            return count;
        }

        private static string SlotKey(string[] columns) => string.Join("\t", columns.Take(4));

        private static void ImportAdditionalArt(string only = null)
        {
            foreach (string name in new[] { "GanadoMale_Model", "wp5805_Model", "GanadoTorch", "wp5801_Model", "GanadoArmed1st",
                         "GanadoArmedReactions", "GanadoArmedStandingLeg",
                         "GanadoMale", "GanadoMalePair", "LeonDamage", "CQBRRifleDraw", "CQBRRifleDrawCalibrated",
                         "LeonWeaponContexts", "LeonLocomotionTurns", "LeonDirectionalEdges",
                         "GanadoLocomotion", "GanadoMaleLocomotion", "SalvadorLocomotion", "GanadoMelee", "GanadoMaleMelee",
                         "GanadoWallReactions", "GanadoMaleWallReactions", "GanadoGround", "GanadoMaleGround",
                         "LeonGroundFinish", "GanadoGroundFinish", "GanadoMaleGroundFinish", "LeonPickup", "LeonIdleTurn",
                         "LeonHurtEdges", "LeonFatalKicks" })
            {
                if (only != null && name != only)
                {
                    continue;
                }
                string path = Generated + "/Native/" + name + ".fbx";
                string input = Source + "/FBX/" + name + ".fbx";
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) == File.GetLastWriteTimeUtc(input)
                    && AssetImporter.GetAtPath(path) is ModelImporter configured
                    && configured.animationType == ModelImporterAnimationType.Generic)
                    continue;
                File.Copy(input, path, true);
                File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(input));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = !name.EndsWith("_Model", StringComparison.Ordinal);
                importer.preserveHierarchy = true;
                importer.optimizeGameObjects = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
        }

        private static GameObject CreateRig(string role)
        {
            if (role != "Leon")
                return CreateModel(role);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPath);
            var rig = Object.Instantiate(prefab);
            rig.name = "LeonR4R";
            foreach (var component in rig.GetComponentsInChildren<Component>(true).Reverse())
            {
                if (component != null && component is not Transform && component is not Renderer && component is not Animator)
                    Object.DestroyImmediate(component);
            }

            foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.Contains("pistol", StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;

            var wrist = Node(rig, "arm right wrist");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Leon_Model.fbx");
            var sourceGrip = Node(source, "RE4_Grip");
            var sourceSocket = Node(source, "RE4_WeaponSocket");
            Transform grip = CopySocket(sourceGrip, wrist, 100);
            CopySocket(sourceSocket, grip, 100);
            return rig;
        }

        private static Transform CopySocket(Transform from, Transform to, float scale)
        {
            var socket = new GameObject(from.name).transform;
            socket.SetParent(to, false);
            socket.SetLocalPositionAndRotation(from.localPosition * scale, from.localRotation);
            socket.localScale = from.localScale;
            return socket;
        }

        private static GameObject CreateModel(string id)
        {
            string directory = id is "GanadoMale" or "wp5805" ? Generated + "/Native" : Art;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(directory + "/" + id + "_Model.fbx");
            if (source == null)
                throw new FileNotFoundException("找不到模型", id);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, SceneManager.GetActiveScene());
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null)
                        materials[i] = ResolveMaterial(id, materials[i]);
                renderer.sharedMaterials = materials;
            }
            return model;
        }

        private static Material ResolveMaterial(string id, Material source)
        {
            string safe = Regex.Replace(source.name, @"[^\w.-]", "_");
            var shared = AssetDatabase.LoadAssetAtPath<Material>($"{Art}/Materials/{id}_{safe}.mat");
            if (shared != null)
            {
                return shared;
            }
            if (id != "GanadoMale" && id != "wp5805" && id != "wp5801")
            {
                return source;
            }

            string json = File.ReadAllText(Source + "/RigData/" + id + "Materials.json");
            var match = Regex.Match(json, "\"" + Regex.Escape(source.name) + "\"\\s*:\\s*\"([^\"]+\\.png)\"");
            if (!match.Success)
                return source;
            string textureName = match.Groups[1].Value;
            string texturePath = Generated + "/Textures/" + textureName;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/" + textureName);
            if (texture == null)
            {
                if (!File.Exists(texturePath))
                {
                    File.Copy(Source + "/Textures/" + textureName, texturePath);
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                }
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            var material = new Material(Shader.Find("Standard")) { mainTexture = texture };
            material.SetFloat("_Glossiness", 0.24f);
            if (source.name.Contains("hair", StringComparison.OrdinalIgnoreCase))
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", 0.31f);
                material.renderQueue = 2450;
            }
            return SaveAsset(material, Generated + "/Native/" + id + "_" + safe + ".mat");
        }

        private static ChapterOneRootActor ConfigureActor(string role, GameObject rig, List<ChapterOneRootActor.Take> takes)
        {
            rig.layer = role == "Leon" ? 9 : 10;
            var body = rig.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var capsule = rig.AddComponent<CapsuleCollider>();
            capsule.radius = 0.29f;
            capsule.height = role == "Salvador" ? 1.92f : 1.72f;
            capsule.center = Vector3.up * (capsule.height * 0.5f + 0.012f);
            var actor = rig.AddComponent<ChapterOneRootActor>();
            var motor = role == "Leon" ? rig.AddComponent<LeonActionMotionDriver>() : null;
            if (motor != null)
            {
                motor.body = body;
                motor.animator = rig.GetComponent<Animator>();
            }
            var items = new List<ChapterOneRootActor.Equipment>();
            var mounts = File.ReadLines(Source + "/GripAudit/NativeMounts.tsv").Select(l => l.Split('\t'))
                .ToDictionary(x => "wp" + x[0]);
            string[] ids = role == "Leon" ? new[] { "wp4000", "wp4100", "wp4402", "wp5000" }
                : role == "Salvador" ? new[] { "wp5300" } : Array.Empty<string>();
            foreach (string id in ids)
            {
                var socket = Node(rig, "RE4_WeaponSocket");
                var model = CreateModel(id);
                model.transform.SetParent(socket, false);
                ApplyEquipmentMount(model.transform, mounts[id]);
                var item = new ChapterOneRootActor.Equipment { id = id, model = model.transform, muzzle = Node(model, "RE4_Muzzle") };
                item.FitKnifeGrip();
                items.Add(item);
            }

            actor.Configure(role switch { "Leon" => "里昂", "Ganado" => "女村民", "GanadoMale" => "男村民", _ => "电锯男" },
                takes.ToArray(), items.ToArray(), Node(rig, role == "Leon" ? "leg left ankle" : "L_Foot"),
                Node(rig, role == "Leon" ? "leg right ankle" : "R_Foot"),
                Node(rig, role == "Leon" ? "arm right wrist" : "R_Hand"), 1, motor);
            return actor;
        }

        private static void ApplyEquipmentMount(Transform model, string[] mount)
        {
            float At(int index) => float.Parse(mount[index], CultureInfo.InvariantCulture);
            Vector3 parentScale = model.parent.lossyScale;
            var position = new Vector3(-At(2) / parentScale.x, -At(4) / parentScale.y, At(3) / parentScale.z);
            var rotation = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(At(5), -At(6), -At(7), At(8));
            var scale = new Vector3(At(9) / parentScale.x, At(10) / parentScale.y, At(11) / parentScale.z);
            model.SetLocalPositionAndRotation(position, rotation);
            model.localScale = scale;
        }

        public static string RefreshKnifeMount()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("校准刀柄前请退出播放。");
            }
            string path = Generated + "/Leon.prefab";
            var mount = File.ReadLines(Source + "/GripAudit/NativeMounts.tsv").Select(line => line.Split('\t'))
                .Single(columns => columns[0] == "5000");
            var rig = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var item = rig.GetComponent<ChapterOneRootActor>().FindEquipment("wp5000");
                if (item == null)
                {
                    throw new InvalidOperationException("找不到验收角色的战斗刀装备绑定。");
                }
                var knife = item.model;
                if (knife == null || knife.parent == null || knife.parent.name != "RE4_WeaponSocket")
                {
                    throw new InvalidOperationException("找不到验收角色右手上的原版战斗刀。");
                }
                ApplyEquipmentMount(knife, mount);
                item.FitKnifeGrip(true);
                if (PrefabUtility.SaveAsPrefabAsset(rig, path) == null)
                {
                    throw new InvalidOperationException("未保存校准后的里昂预制件。");
                }
                return knife.localPosition.ToString("F6");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }

        private static ChapterOneRootActor.Equipment AttachTorch(GameObject rig)
        {
            var node = Node(rig, "R_Wep");
            var model = CreateModel("wp5805");
            model.transform.SetParent(node, false);
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale = Vector3.one / node.lossyScale.x;
            return new ChapterOneRootActor.Equipment { id = "wp5805", model = model.transform };
        }

        private static ChapterOneLoop.Threat Threat(ChapterOneRootActor actor, string prefix, string attack,
            string alert, string equipment = null, int health = 100)
            => new() { actor = actor, prefix = prefix, attack = attack, alert = alert, equipment = equipment, health = health };

        private static void Place(ChapterOneRootActor actor, Vector3 position, float heading)
            => actor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0));

        private static Transform Node(GameObject rig, string name) => rig.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        private static AnimationClip Find(string file, string id)
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(id))
                return null;
            if (!Clips.TryGetValue(file, out var clips))
            {
                string root = file is "GanadoTorch" or "GanadoMale" or "GanadoMalePair" or "LeonDamage"
                    or "CQBRRifleDraw" or "CQBRRifleDrawCalibrated" or "LeonWeaponContexts" or "LeonLocomotionTurns"
                    or "LeonDirectionalEdges" or "GanadoLocomotion" or "GanadoMaleLocomotion" or "SalvadorLocomotion"
                    or "GanadoMelee" or "GanadoMaleMelee" or "GanadoWallReactions" or "GanadoMaleWallReactions"
                    or "GanadoGround" or "GanadoMaleGround"
                    or "LeonGroundFinish" or "GanadoGroundFinish" or "GanadoMaleGroundFinish" or "LeonPickup" or "LeonIdleTurn"
                    or "LeonHurtEdges" or "LeonFatalKicks"
                    ? Generated + "/Native" : Art;
                clips = AssetDatabase.LoadAllAssetsAtPath(root + "/" + file + ".fbx").OfType<AnimationClip>().ToArray();
                Clips[file] = clips;
            }
            return Array.Find(clips, c => c.name == id || c.name.EndsWith("|" + id, StringComparison.Ordinal));
        }

        private static T SaveAsset<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(value, path);
                return value;
            }
            EditorUtility.CopySerialized(value, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(value);
            return existing;
        }

        private static Transform MakeGround()
        {
            var root = new GameObject("验收地形").transform;
            var ground = new Color(0.34f, 0.35f, 0.28f);
            var timber = new Color(0.44f, 0.39f, 0.32f);
            Surface("村庄练习地面", new Vector3(0, -0.22f, 3), new Vector3(21, 0.44f, 23), ground);
            Surface("围栏西", new Vector3(-10, 1, 3), new Vector3(0.4f, 2, 23), timber);
            Surface("围栏东", new Vector3(10, 1, 3), new Vector3(0.4f, 2, 23), timber);
            Surface("围栏北", new Vector3(0, 1, 14.2f), new Vector3(20, 2, 0.4f), timber);
            Surface("围栏南", new Vector3(0, 1, -8.2f), new Vector3(20, 2, 0.4f), timber);
            Surface("掩体墙", new Vector3(-2.6f, 0.95f, 3.2f), new Vector3(0.48f, 1.9f, 3.2f), timber);
            Surface("挡墙", new Vector3(3.1f, 0.93f, 6.3f), new Vector3(2.6f, 1.86f, 0.38f), timber);
            Surface("斜坡", new Vector3(-7.5f, 0.18f, 6.5f), new Vector3(2.1f, 0.38f, 4f), timber)
                .rotation = Quaternion.Euler(-12, 0, 0);
            for (int i = 1; i <= 5; i++)
                Surface("台阶 " + i, new Vector3(7f, i * 0.06f, 8f + i * 0.4f),
                    new Vector3(2.2f, i * 0.12f, 0.4f), timber);
            return root;

            Transform Surface(string label, Vector3 at, Vector3 size, Color tint)
            {
                var item = Box(label, at, size, tint).transform;
                item.SetParent(root, true);
                return item;
            }
        }

        private static GameObject Box(string label, Vector3 at, Vector3 size, Color color, bool collider = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = label;
            obj.transform.position = at;
            obj.transform.localScale = size;
            var material = new Material(Shader.Find("Standard")) { color = color };
            string path = Generated + "/Surface_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            obj.GetComponent<Renderer>().sharedMaterial = SaveAsset(material, path);
            if (!collider)
                Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static NavMeshData BuildNavigation(Transform terrain)
        {
            var bounds = new Bounds(new Vector3(0, 1, 3), new Vector3(24, 6, 27));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(terrain, 1, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            var nav = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, bounds, Vector3.zero, Quaternion.identity);
            if (nav == null)
                throw new InvalidOperationException("无法生成独立验收场景的寻路网格。");
            return SaveAsset(nav, Generated + "/ReviewNavigation.asset");
        }
    }
}
#endif

