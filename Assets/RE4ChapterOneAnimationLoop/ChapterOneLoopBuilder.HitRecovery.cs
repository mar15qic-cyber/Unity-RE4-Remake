#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static void ConfigureHitRecovery(bool allDirections, bool upperBody)
        {
            var graph = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainLeonController);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Generated + "/Clips/Leon_Clips.asset")
                .OfType<AnimationClip>().ToDictionary(c => c.name);
            ApplyHitRecovery(graph, clips, allDirections, upperBody);
            AssetDatabase.SaveAssetIfDirty(graph);
        }

        private static void ApplyHitRecovery(AnimatorController graph, System.Collections.Generic.Dictionary<string, AnimationClip> clips, bool allDirections, bool upperBody)
        {
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<BlendTree>()
                .Where(t => t.name == "HitDirection_Pistol" || t.name == "HitDirection_Rifle"))
            {
                var children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    string id = children[i].motion.name.Replace("MainNative_", "");
                    if (id == "LeonDamage_0102" || allDirections && (id == "LeonDamage_0100" || id == "LeonDamage_0104"))
                        children[i].motion = clips[id];
                }
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            if (!upperBody || graph.layers.Any(l => l.name == "Native Hit Upper")) return;
            var hit = graph.layers[LeonAnimatorDriver.LayerFullBodyAction].stateMachine.states.Single(s => s.state.name == "Hit").state;
            graph.AddLayer("Native Hit Upper");
            var layers = graph.layers;
            var layer = layers[layers.Length - 1];
            layer.avatarMask = AssetDatabase.LoadAllAssetsAtPath(MainLeonController).OfType<AvatarMask>().Single(m => m.name == "上半身遮罩_Generic");
            layer.defaultWeight = 0f;
            layer.stateMachine.AddState("Empty");
            var recovery = layer.stateMachine.AddState("HitRecovery");
            recovery.motion = hit.motion;
            recovery.writeDefaultValues = hit.writeDefaultValues;
            graph.layers = layers;
            EditorUtility.SetDirty(graph);
        }

        // 仅烘焙电锯男跪姿补刀的双方轨道，单独保存以免重写已有村民和电锯格挡资源。
        public static void ImportSawKnifeFollowUp()
        {
            string bankPath = Generated + "/Resources/SawKnifeFollowUp.asset";
            string libraryPath = Generated + "/Clips/SawKnifeFollowUp.asset";
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(bankPath);
            var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(libraryPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<ChapterOnePairMotion>();
                AssetDatabase.CreateAsset(bank, bankPath);
            }
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ChapterOneBakedClips>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(libraryPath).OfType<AnimationClip>().ToDictionary(c => c.name);
            foreach (string role in new[] { "Leon", "Salvador" })
            {
                string name = role + "Pair_SawKneel1020";
                string path = Generated + "/Native/" + role + "KnifeFollowUp1122.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.preserveHierarchy = true;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
                var source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == name);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                var baked = Bake(source, prefab, role, role, false, Array.Empty<int>(), library, clips, out var origin, name);
                bank.RegisterVillagerTrack(role, baked, origin);
            }
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssetIfDirty(bank);
            AssetDatabase.SaveAssetIfDirty(library);
            ImportGroundFinisherCameras(true);
        }

        public static void ImportEnemyLegTrapTracks()
        {
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(Generated + "/Resources/" + ChapterOnePairMotion.ResourceName + ".asset");
            foreach (string role in new[] { "Ganado", "GanadoMale" })
            {
                string path = Generated + "/Native/" + role + "LegTrap.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.preserveHierarchy = true;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => c.name == "Trap2000");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                string libraryPath = Generated + "/Clips/" + role + "_Clips.asset";
                var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(libraryPath);
                var clips = AssetDatabase.LoadAllAssetsAtPath(libraryPath).OfType<AnimationClip>().ToDictionary(c => c.name);
                var baked = Bake(clip, prefab, role, role, false, Array.Empty<int>(), library, clips, out var origin, "Trap2000");
                bank.RegisterVillagerTrack(role, baked, origin);
                AssetDatabase.SaveAssetIfDirty(library);
            }
            AssetDatabase.SaveAssetIfDirty(bank);
        }
    }
}
#endif
