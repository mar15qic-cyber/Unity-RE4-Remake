using System;
using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    [PreferBinarySerialization]
    internal sealed class ChapterOnePairMotion : ScriptableObject
    {
        internal const string ResourceName = "ChapterOnePairMotion";

        [Serializable]
        internal sealed class Track
        {
            public AnimationClip clip;
            public Vector3[] positions;
            public Quaternion[] rotations;
            public Vector3 sourceOrigin;
            public Vector3 sourceRight;
            public string villagerRole;

            internal bool Matches(AnimationClip animation) =>
                animation != null && clip == animation && positions != null && positions.Length > 0
                && rotations != null && rotations.Length == positions.Length;

            public Pose Sample(float time)
            {
                float frame = Mathf.Clamp(time * clip.frameRate, 0f, positions.Length - 1);
                int start = Mathf.FloorToInt(frame);
                int end = Mathf.Min(start + 1, positions.Length - 1);
                float weight = frame - start;
                return new Pose(Vector3.Lerp(positions[start], positions[end], weight),
                    Quaternion.Slerp(rotations[start], rotations[end], weight));
            }
        }

        [SerializeField] private Track[] tracks = Array.Empty<Track>();
        private Dictionary<AnimationClip, Track> byClip;

        internal bool TryGetTrack(AnimationClip clip, out Track track)
        {
            if (clip == null)
            {
                track = null;
                return false;
            }
            if (byClip == null)
            {
                byClip = new Dictionary<AnimationClip, Track>();
                foreach (var item in tracks)
                {
                    if (item.clip != null)
                    {
                        byClip.Add(item.clip, item);
                    }
                }
            }
            return byClip.TryGetValue(clip, out track) && track.Matches(clip);
        }

        internal bool TryGetNamedTrack(string name, out Track track)
        {
            track = Array.Find(tracks, item => item.clip != null && item.clip.name == name);
            return track != null && track.Matches(track.clip);
        }

        internal bool TryGetVillagerTrack(string role, string name, out Track track)
        {
            track = Array.Find(tracks, item => item.clip != null && item.clip.name == name && item.villagerRole == role);
            return track != null && track.Matches(track.clip);
        }

#if UNITY_EDITOR
        internal void RegisterVillagerTrack(string role, AnimationClip clip, Vector3 origin)
        {
            var next = new List<Track>(tracks);
            next.RemoveAll(item => item.clip == clip);
            Track track = ReadTrack(clip);
            track.sourceOrigin = origin;
            track.villagerRole = role;
            next.Add(track);
            tracks = next.ToArray();
            byClip = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public static int RefreshSawPairOrigins()
        {
            const string directory = "Assets/RE4ChapterOneAnimationLoop/Generated";
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(
                directory + "/Resources/" + ResourceName + ".asset");
            int count = WriteSawPairOrigins(profile, directory);
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
            return count;
        }

        private static int WriteSawPairOrigins(ChapterOnePairMotion profile, string directory)
        {
            string[] ids = { "LeonPair_1200", "LeonPair_1202", "LeonPair_0030_jack_pl_2nd_wp5300",
                "SalvadorPair_1200", "SalvadorPair_1202", "SalvadorPair_0030" };
            int count = 0;
            foreach (string role in new[] { "Leon", "Salvador" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(directory + "/" + role + ".prefab");
                foreach (var take in prefab.GetComponent<ChapterOneRootActor>().Takes)
                {
                    if (Array.IndexOf(ids, take.id) >= 0 && profile.TryGetNamedTrack(take.id, out var track))
                    {
                        track.sourceOrigin = take.pairOrigin;
                        count++;
                    }
                }
            }
            if (count != ids.Length)
            {
                throw new InvalidOperationException("电锯配对缺少源起点。");
            }
            return count;
        }

        public static int Refresh()
        {
            const string generated = "Assets/RE4ChapterOneAnimationLoop/Generated";
            string resourceFolder = generated + "/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(resourceFolder))
            {
                UnityEditor.AssetDatabase.CreateFolder(generated, "Resources");
            }
            string path = resourceFolder + "/" + ResourceName + ".asset";
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChapterOnePairMotion>(path);
            if (profile == null)
            {
                profile = CreateInstance<ChapterOnePairMotion>();
                UnityEditor.AssetDatabase.CreateAsset(profile, path);
            }
            var villagerTracks = new Dictionary<AnimationClip, Track>();
            if (profile.tracks != null)
            {
                foreach (Track track in profile.tracks)
                {
                    if (track.clip != null && !string.IsNullOrEmpty(track.villagerRole))
                    {
                        villagerTracks[track.clip] = track;
                    }
                }
            }
            var result = new List<Track>();
            foreach (string role in new[] { "Leon", "Ganado", "GanadoMale", "Salvador" })
            {
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(generated + "/Clips/" + role + "_Clips.asset");
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip
                        && (clip.name.StartsWith("LeonPair_", StringComparison.Ordinal)
                            || clip.name.StartsWith("GanadoPair_", StringComparison.Ordinal)
                            || clip.name.StartsWith("SalvadorPair_", StringComparison.Ordinal)
                            || clip.name.StartsWith("LeonTraverse_", StringComparison.Ordinal)
                            || clip.name.StartsWith("GanadoWindow_", StringComparison.Ordinal)
                            || clip.name.StartsWith("SalvadorWindow_", StringComparison.Ordinal)
                            || clip.name == ChapterOneRootActor.CrawlBackClip
                            || clip.name == "LeonDoors_0007"))
                    {
                        var track = ReadTrack(clip);
                        if (villagerTracks.TryGetValue(clip, out var existing))
                        {
                            track.villagerRole = existing.villagerRole;
                            track.sourceOrigin = existing.sourceOrigin;
                        }
                        else if (ChapterOneRootActor.IsGroundFinishClip(clip.name))
                        {
                            track.sourceOrigin = ChapterOneLoopBuilder.GroundFinishOrigin(role, clip.name);
                            if (role != "Leon")
                            {
                                track.sourceRight = ReadGroundFinishRight(generated, role, clip);
                            }
                        }
                        else if (clip.name.Contains("Pair_LegBite", StringComparison.Ordinal))
                        {
                            // 过渡资源保留双方共享的舞台坐标，运行轨道仍以各自首帧为零点。
                            track.sourceOrigin = track.positions[0];
                            for (int frame = 0; frame < track.positions.Length; frame++)
                            {
                                track.positions[frame] -= track.sourceOrigin;
                            }
                        }
                        result.Add(track);
                    }
                }
            }
            profile.tracks = result.ToArray();
            profile.byClip = null;
            WriteSawPairOrigins(profile, generated);
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
            return result.Count;
        }

        private static Vector3 ReadGroundFinishRight(string directory, string role, AnimationClip clip)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(directory + "/" + role + ".prefab");
            var instance = Instantiate(prefab);
            try
            {
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clip.SampleAnimation(instance, 0f);
                return ChapterOneRootActor.ReadGroundBodyRight(instance.transform);
            }
            finally
            {
                DestroyImmediate(instance);
            }
        }

        internal static Track ReadTrack(AnimationClip clip)
        {
            string[] properties = { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            var curves = new AnimationCurve[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var binding = UnityEditor.EditorCurveBinding.FloatCurve("", typeof(Animator), properties[i]);
                curves[i] = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
                if (curves[i] == null)
                {
                    throw new InvalidOperationException(clip.name + " 缺少原版配对根轨道 " + properties[i]);
                }
            }
            int count = Mathf.RoundToInt(clip.length * clip.frameRate) + 1;
            var track = new Track { clip = clip, positions = new Vector3[count], rotations = new Quaternion[count] };
            for (int i = 0; i < count; i++)
            {
                float time = Mathf.Min(i / clip.frameRate, clip.length);
                track.positions[i] = new Vector3(curves[0].Evaluate(time), curves[1].Evaluate(time), curves[2].Evaluate(time));
                track.rotations[i] = new Quaternion(curves[3].Evaluate(time), curves[4].Evaluate(time),
                    curves[5].Evaluate(time), curves[6].Evaluate(time)).normalized;
            }
            return track;
        }
#endif
    }
}
