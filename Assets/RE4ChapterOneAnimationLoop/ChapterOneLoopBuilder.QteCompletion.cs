#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        private const string QteSource = "D:/RE4素材/复跑/QTE-20261115";

        public static string BindNativeGroundKnife()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式绑定原版配对小刀。");
            }
            var player = UnityEngine.Object.FindFirstObjectByType<LeonWeaponVisualController>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/Leon.prefab")
                .GetComponent<ChapterOneRootActor>().FindEquipment("wp5000").model;
            if (player.nativePairKnife == null)
            {
                var socket = Node(player.gameObject, "RE4_WeaponSocket");
                if (socket == null)
                {
                    throw new InvalidOperationException("正式里昂缺少原版武器挂点。");
                }
                var knife = UnityEngine.Object.Instantiate(source.gameObject, socket, false);
                knife.name = "NativePairKnife";
                Undo.RegisterCreatedObjectUndo(knife, "绑定原版配对小刀");
                knife.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
                knife.transform.localScale = source.localScale;
                Undo.RecordObject(player, "绑定原版配对小刀");
                player.nativePairKnife = knife.transform;
                knife.SetActive(false);
            }
            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            return "已复用原版 wp5000 模型、材质及提取的握持变换。";
        }

        public static string ConfigureQteCompletion()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式绑定正式资源。");
            }
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(Generated + "/Resources/ChapterOnePairMotion.asset");
            foreach (string role in new[] { "Ganado", "GanadoMale" })
            {
                foreach (string motion in new[] { "Native_2100", "Native_1015", "Native_3510", "Native_3597", "GanadoPair_Ground1000", "GanadoPair_Ground1010", "GanadoPair_Ground1035" })
                {
                    if (!bank.TryGetVillagerTrack(role, motion, out _))
                    {
                        throw new InvalidOperationException("缺少动作轨道：" + role + "/" + motion);
                    }
                }
            }
            const string profilePath = "Assets/Configs/NativeChapterOneCombat.asset";
            var profile = AssetDatabase.LoadAssetAtPath<NativeEnemyCombatProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NativeEnemyCombatProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            // 绑定正式配置，保留其中已保存的复起调参。
            foreach (string name in new[] { "MainGanadoMale", "MainGanadoFemale", "MainGanadoArmed" })
            {
                string path = Generated + "/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var combat = new SerializedObject(root.GetComponent<EnemyCombatModule>());
                    combat.FindProperty("nativeCombatProfile").objectReferenceValue = profile;
                    combat.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            foreach (string path in Directory.GetFiles("Assets/Resources/NativeInteractionArt", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var config = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(Generated + "/MainLeonQTE.asset");
            config.useNativeGroundFinisher = true;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            var player = UnityEngine.Object.FindFirstObjectByType<LeonQTECoordinator>();
            if (player == null || player.config != config)
            {
                throw new InvalidOperationException("当前场景没有绑定正式 QTE 配置的里昂。");
            }
            var view = player.GetComponent<NativeInteractionPromptView>();
            if (view == null)
            {
                view = Undo.AddComponent<NativeInteractionPromptView>(player.gameObject);
            }
            Undo.RecordObject(view, "绑定原版提示");
            view.coordinator = player;
            view.input = player.GetComponent<LeonInputBridge>();
            view.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/重制界面/中文正文字体.otf");
            EditorUtility.SetDirty(view);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            return "已绑定正式村民抓取、击倒、复起配置、补刀与原版提示。";
        }

        public static string ImportQteCompletionMotions(bool revivedOnly = false)
        {
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(Generated + "/Resources/ChapterOnePairMotion.asset");
            int count = 0;
            foreach (string role in new[] { "Ganado", "GanadoMale" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                string libraryPath = Generated + "/Clips/" + role + "_Clips.asset";
                var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(libraryPath);
                var clips = AssetDatabase.LoadAllAssetsAtPath(libraryPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
                string[] suffixes = { "Revival", "Down" };
                if (revivedOnly)
                {
                    suffixes = new[] { "RevivedGait" };
                }
                foreach (string suffix in suffixes)
                {
                    string group = role + suffix;
                    string path = Generated + "/Native/" + group + ".fbx";
                    File.Copy(QteSource + "/FBX/" + group + ".fbx", path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.preserveHierarchy = true;
                    importer.optimizeGameObjects = false;
                    importer.SaveAndReimport();
                    foreach (AnimationClip source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    {
                        if (!source.name.StartsWith("Native_", StringComparison.Ordinal)
                            && !source.name.StartsWith("Revived_", StringComparison.Ordinal))
                        {
                            continue;
                        }
                        AnimationClip baked = Bake(source, prefab, role, role, false, Array.Empty<int>(), library, clips,
                            out Vector3 origin, source.name);
                        bank.RegisterVillagerTrack(role, baked, origin);
                        count++;
                    }
                }
                AssetDatabase.SaveAssetIfDirty(library);
            }
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
            {
                string prefix = "GanadoPair_";
                if (role == "Leon")
                {
                    prefix = "LeonPair_";
                }
                foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/" + role + "_Clips.asset").OfType<AnimationClip>())
                {
                    if (clip.name == prefix + "Ground1000" || clip.name == prefix + "Ground1010" || clip.name == prefix + "Ground1035")
                    {
                        if (!bank.TryGetTrack(clip, out var track))
                        {
                            throw new InvalidOperationException("补刀缺少已烘焙根轨：" + role + "/" + clip.name);
                        }
                        track.villagerRole = role;
                        count++;
                    }
                }
            }
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
            return "已追加原版动作与补刀角色轨道：" + count;
        }

        public static string ImportGroundFinisherCameras(bool sawOnly = false)
        {
            string source = "D:/RE4素材/Chapter1Animation/QTECamera/Decoded/natives/stm/_chainsaw/animation/ch/chc0/camlist/0th/";
            var result = new List<NativeQTECameraTracks.Track>();
            foreach (string file in new[] { "chc0_cam_0th_com.mcamlist.22.json", "chc0_cam2_0th_com.mcamlist.22.json" })
            {
                JObject data = JObject.Parse(File.ReadAllText(source + file));
                foreach (JToken take in data["takes"])
                {
                    int id = (int)take["motionId"];
                    if (sawOnly && id != 1020 || !sawOnly && id != 1000 && id != 1010 && id != 1035)
                    {
                        continue;
                    }
                    int frames = (int)take["frames"] + 1;
                    var track = new NativeQTECameraTracks.Track
                    {
                        playerClip = "LeonPair_Ground" + id,
                        bankId = 100,
                        motionId = id,
                        frameRate = (int)take["fps"],
                        positions = new Vector3[frames],
                        rotations = new Quaternion[frames],
                        horizontalFov = new float[frames]
                    };
                    if (sawOnly)
                    {
                        track.playerClip = "LeonPair_SawKneel1020";
                    }
                    if (file.Contains("cam2"))
                    {
                        track.bankId = 150;
                    }
                    for (int frame = 0; frame < frames; frame++)
                    {
                        Vector4 position = ReadNativeCameraChannel(take["clip1"], "positionFrames", "positions", frame, false);
                        Vector4 rotation = ReadNativeCameraChannel(take["clip1"], "rotationFrames", "rotations", frame, true);
                        Vector4 lens = ReadNativeCameraChannel(take["clip2"], "positionFrames", "positions", frame, false);
                        // 沿用现有原版镜头坐标转换，FOV 的源 X 通道是水平弧度。
                        track.positions[frame] = new Vector3(-position.x, position.y, position.z);
                        track.rotations[frame] = new Quaternion(rotation.z, rotation.w, rotation.x, rotation.y).normalized;
                        track.horizontalFov[frame] = lens.x * Mathf.Rad2Deg;
                    }
                    result.Add(track);
                }
            }
            string prefix = "LeonPair_Ground";
            string path = "Assets/Settings/Camera/QTE/NativeQTECameraTracks.asset";
            if (sawOnly)
            {
                prefix = "LeonPair_SawKneel1020";
                path = Generated + "/Resources/SawKnifeCamera.asset";
            }
            var asset = AssetDatabase.LoadAssetAtPath<NativeQTECameraTracks>(path);
            if (asset == null && sawOnly)
            {
                asset = ScriptableObject.CreateInstance<NativeQTECameraTracks>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.ReplaceGroundFinisherTracks(result.ToArray(), prefix);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return "已追加原版补刀镜头：" + result.Count;
        }

        private static Vector4 ReadNativeCameraChannel(JToken clip, string frameKey, string valueKey, int frame, bool rotation)
        {
            var values = (JArray)clip[valueKey];
            if (values == null || values.Count == 0)
            {
                throw new InvalidDataException("原版镜头缺少通道：" + valueKey);
            }
            JArray keys = clip[frameKey] as JArray;
            int first = 0;
            int next = 0;
            float weight = 0f;
            if (keys != null && keys.Count > 1)
            {
                while (first + 1 < keys.Count && (int)keys[first + 1] <= frame)
                {
                    first++;
                }
                next = Math.Min(first + 1, keys.Count - 1);
                if (first != next)
                {
                    weight = Mathf.InverseLerp((int)keys[first], (int)keys[next], frame);
                }
            }
            Vector4 Read(JToken value)
            {
                float w = 0f;
                if (rotation)
                {
                    w = (float)value["W"];
                }
                return new Vector4((float)value["X"], (float)value["Y"], (float)value["Z"], w);
            }
            Vector4 a = Read(values[first]);
            Vector4 b = Read(values[next]);
            if (rotation)
            {
                Quaternion blended = Quaternion.Slerp(new Quaternion(a.x, a.y, a.z, a.w), new Quaternion(b.x, b.y, b.z, b.w), weight);
                return new Vector4(blended.x, blended.y, blended.z, blended.w);
            }
            return Vector4.Lerp(a, b, weight);
        }
    }
}
#endif
