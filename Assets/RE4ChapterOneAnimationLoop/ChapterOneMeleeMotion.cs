using System;
using UnityEngine;

namespace RE4.ChapterOne
{
    [PreferBinarySerialization]
    internal sealed class ChapterOneMeleeMotion : ScriptableObject
    {
        internal const string ResourceName = "ChapterOneMeleeMotion";

        [Serializable]
        internal sealed class Track
        {
            public ChapterOnePairMotion.Track root;
            public Vector3[] points;

            public Vector3 SamplePoint(float time)
            {
                float frame = Mathf.Clamp(time * root.clip.frameRate, 0f, points.Length - 1);
                int start = Mathf.FloorToInt(frame);
                int end = Mathf.Min(start + 1, points.Length - 1);
                return Vector3.Lerp(points[start], points[end], frame - start);
            }
        }

        [SerializeField] private Track[] tracks;

        internal bool TryGetTrack(AnimationClip clip, out Track track)
        {
            if (tracks == null)
            {
                track = null;
                return false;
            }
            track = Array.Find(tracks, item => item.root.clip == clip);
            return track != null;
        }

#if UNITY_EDITOR
        public static int Refresh()
        {
            const string generated = "Assets/RE4ChapterOneAnimationLoop/Generated";
            string folder = generated + "/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder(generated, "Resources");
            }
            string path = folder + "/" + ResourceName + ".asset";
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<ChapterOneMeleeMotion>(path);
            if (profile == null)
            {
                profile = CreateInstance<ChapterOneMeleeMotion>();
                UnityEditor.AssetDatabase.CreateAsset(profile, path);
            }
            var results = new System.Collections.Generic.List<Track>();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string role in new[] { "Leon", "Ganado", "GanadoMale" })
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(generated + "/" + role + ".prefab");
                    var rig = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
                    rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    rig.GetComponent<Animator>().enabled = false;
                    var actor = rig.GetComponent<ChapterOneRootActor>();
                    actor.CacheMeleeJoints(role == "Leon");
                    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(generated + "/Clips/" + role + "_Clips.asset"))
                    {
                        if (asset is AnimationClip clip && (ChapterOneRootActor.IsBodyMelee(clip.name)
                            || clip.name is "Ganado_0420" or "Ganado_0520"
                            || ChapterOneRootActor.PickupContactFrame(clip.name) >= 0))
                        {
                            results.Add(Sample(clip, actor));
                        }
                    }
                    DestroyImmediate(rig);
                }
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
            profile.tracks = results.ToArray();
            UnityEditor.EditorUtility.SetDirty(profile);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(profile);
            return results.Count;
        }

        private static Track Sample(AnimationClip clip, ChapterOneRootActor actor)
        {
            var root = ChapterOnePairMotion.ReadTrack(clip);
            var points = new Vector3[root.positions.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float time = i / clip.frameRate;
                clip.SampleAnimation(actor.gameObject, time);
                Vector3 point;
                if (ChapterOneRootActor.IsBodyMelee(clip.name))
                {
                    point = actor.MeleeStrikePoint(clip.name);
                }
                else if (ChapterOneRootActor.PickupContactFrame(clip.name) >= 0)
                {
                    point = actor.PickupHandPosition;
                }
                else
                {
                    point = actor.MeleeTargetPoint(true);
                }
                Vector3 local = actor.transform.InverseTransformPoint(point);
                points[i] = root.positions[i] + root.rotations[i] * local;
            }
            return new Track { root = root, points = points };
        }
#endif
    }
}
