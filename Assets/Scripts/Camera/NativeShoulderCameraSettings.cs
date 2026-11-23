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
    [SerializeField, HideInInspector] private Vector2[] forearmBreathAngles = Array.Empty<Vector2>();
    [SerializeField, Range(0f, 0.5f), Tooltip("站定瞄准时，小臂水平和垂直摆角传给镜头的比例。")]
    private float forearmBreathGain = 0.12f;
    [SerializeField, Tooltip("射击与泵动的归一化进度对应的画面上扬量，以画面高度为单位。")]
    private AnimationCurve shotPumpRise;

    internal bool HasShotPumpRise => shotPumpRise != null && shotPumpRise.length >= 2;

    internal float ShotPumpPitch(float progress, float verticalFov)
    {
        float halfHeight = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
        return Mathf.Atan(2f * halfHeight * shotPumpRise.Evaluate(progress)) * Mathf.Rad2Deg;
    }

    internal bool TrySampleBreath(float normalizedTime, out Vector2 angles)
    {
        angles = Vector2.zero;
        if (forearmBreathAngles == null || forearmBreathAngles.Length < 2)
            return false;
        float frame = Mathf.Repeat(normalizedTime, 1f) * (forearmBreathAngles.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(frame), forearmBreathAngles.Length - 2);
        angles = Vector2.Lerp(forearmBreathAngles[index], forearmBreathAngles[index + 1], frame - index) * forearmBreathGain;
        return true;
    }

#if UNITY_EDITOR
    public void SetForearmBreathSamples(Vector2[] samples) => forearmBreathAngles = samples;
#endif

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

