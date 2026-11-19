using System;
using UnityEngine;

[PreferBinarySerialization]
public sealed class NativeQTECameraTracks : ScriptableObject
{
    [Serializable]
    public sealed class Track
    {
        public string playerClip;
        public int bankId;
        public int motionId;
        public int frameRate;
        public Vector3[] positions;
        public Quaternion[] rotations;
        public float[] horizontalFov;

        public bool IsValid => frameRate > 0 && positions != null && positions.Length > 1
            && rotations != null && rotations.Length == positions.Length
            && horizontalFov != null && horizontalFov.Length == positions.Length;

        internal void Sample(float sourceFrame, out Pose pose, out float fov)
        {
            float frame = Mathf.Clamp(sourceFrame, 0f, positions.Length - 1);
            int first = Mathf.FloorToInt(frame);
            int next = Mathf.Min(first + 1, positions.Length - 1);
            float weight = frame - first;
            pose = new Pose(Vector3.Lerp(positions[first], positions[next], weight),
                Quaternion.Slerp(rotations[first], rotations[next], weight));
            fov = Mathf.Lerp(horizontalFov[first], horizontalFov[next], weight);
        }
    }

    [SerializeField] private Track[] tracks = Array.Empty<Track>();

#if UNITY_EDITOR
    public void ReplaceGroundFinisherTracks(Track[] replacement)
    {
        var next = new System.Collections.Generic.List<Track>();
        foreach (Track track in tracks)
        {
            if (track != null && !track.playerClip.StartsWith("LeonPair_Ground", StringComparison.Ordinal))
            {
                next.Add(track);
            }
        }
        foreach (Track track in replacement)
        {
            if (track == null || !track.IsValid || !track.playerClip.StartsWith("LeonPair_Ground", StringComparison.Ordinal))
            {
                throw new ArgumentException("补刀镜头轨道不完整。");
            }
            next.Add(track);
        }
        tracks = next.ToArray();
    }
#endif

    internal Track Find(string playerClip, int frameRate, int start = 0)
    {
        for (int index = start; index < tracks.Length; index++)
        {
            Track candidate = tracks[index];
            if (candidate != null && candidate.IsValid && candidate.playerClip == playerClip
                && candidate.frameRate == frameRate)
            {
                return candidate;
            }
        }
        return null;
    }

    internal Track FindAlternative(Track primary)
    {
        int next = Array.IndexOf(tracks, primary) + 1;
        return Find(primary.playerClip, primary.frameRate, next);
    }
}

