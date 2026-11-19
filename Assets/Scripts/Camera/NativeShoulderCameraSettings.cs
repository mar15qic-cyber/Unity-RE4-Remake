using System;
using UnityEngine;

public sealed class NativeShoulderCameraSettings : ScriptableObject
{
    public enum State
    {
        Normal,
        Aim,
        Sprint,
        Crouch,
        Jog,
        Battle
    }

    public enum Anchor
    {
        Root,
        Hips,
        Chest
    }

    [Serializable]
    public struct Pose
    {
        public Anchor anchor;
        public Vector3 offset;
        public float distance;
        public float sourceFov;
        [Tooltip("原版运行中采到的水平视角。未采到时用源配置值预览。")]
        public float measuredHorizontalFov;
        internal float HorizontalFov
        {
            get
            {
                if (measuredHorizontalFov > 0f && measuredHorizontalFov < 179f)
                {
                    return measuredHorizontalFov;
                }
                return sourceFov;
            }
        }
        public float inputRate;
        public float inputDamping;
        public float outputRate;
        public float outputDamping;
        public bool useOutput;
        public float followDamping;
        public bool verified;
    }

    [SerializeField] private Pose[] poses = Array.Empty<Pose>();

    internal bool TryGetPose(State state, bool allowPreview, out Pose pose)
    {
        int index = (int)state;
        pose = default;
        if (index < 0 || index >= poses.Length)
        {
            return false;
        }

        pose = poses[index];
        return (pose.verified || allowPreview) && pose.distance > 0f
            && pose.HorizontalFov > 0f && pose.HorizontalFov < 179f;
    }
}

