#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static int RefreshNativeRearGrabTracks()
        {
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(
                Generated + "/Resources/" + ChapterOnePairMotion.ResourceName + ".asset");
            int count = 0;
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
            {
                string sourcePath = Generated + "/Native/" + role + "RearQtePair.fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                string clipsPath = Generated + "/Clips/" + role + "_Clips.asset";
                var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(clipsPath);
                var clips = AssetDatabase.LoadAllAssetsAtPath(clipsPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
                string prefix = "GanadoPair_";
                if (role == "Leon")
                {
                    prefix = "LeonPair_";
                }

                foreach (string id in new[] { "0051", "0070" })
                {
                    string alias = prefix + id;
                    var source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Single(clip => clip.name == alias);
                    var clip = Bake(source, prefab, role, role, false, Array.Empty<int>(), library, clips, out Vector3 origin, alias);
                    bank.RegisterVillagerTrack(role, clip, origin);
                    count++;
                }
                AssetDatabase.SaveAssetIfDirty(library);
            }
            AssetDatabase.SaveAssetIfDirty(bank);
            return count;
        }

        public static int RefreshNativeVillagerQteTracks()
        {
            string resourcePath = Generated + "/Resources/" + ChapterOnePairMotion.ResourceName + ".asset";
            var bank = AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(resourcePath);
            int count = 0;
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                var actor = prefab.GetComponent<ChapterOneRootActor>();
                string prefix = "GanadoPair_";
                if (role == "Leon")
                {
                    prefix = "LeonPair_";
                }

                foreach (string id in new[] { "0000", "0020", "0010", "1101" })
                {
                    var take = actor.Takes.Single(item => item.id == prefix + id);
                    bank.RegisterVillagerTrack(role, take.body, take.pairOrigin);
                    count++;
                }

                string group = role + "QtePair";
                string path = Generated + "/Native/" + group + ".fbx";
                string external = Source + "/FBX/" + group + ".fbx";
                File.Copy(external, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.preserveHierarchy = true;
                importer.optimizeGameObjects = false;
                importer.SaveAndReimport();
                string alias = prefix + "1105";
                var sourceClip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(clip => clip.name == alias);
                string clipsPath = Generated + "/Clips/" + role + "_Clips.asset";
                var library = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(clipsPath);
                var clips = AssetDatabase.LoadAllAssetsAtPath(clipsPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
                var baked = Bake(sourceClip, prefab, role, role, false, new[] { 57 }, library, clips, out Vector3 origin, alias);
                bank.RegisterVillagerTrack(role, baked, origin);
                AssetDatabase.SaveAssetIfDirty(library);
                count++;
            }
            AssetDatabase.SaveAssetIfDirty(bank);
            return count;
        }
    }
}
#endif

