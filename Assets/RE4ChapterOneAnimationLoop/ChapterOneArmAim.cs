using UnityEngine;

namespace RE4.ChapterOne
{
    internal static class ChapterOneArmAim
    {
        internal sealed class Arm
        {
            private readonly Transform upper;
            private readonly Transform lower;
            public readonly Transform wrist;

            public Arm(Transform wrist)
            {
                this.wrist = wrist;
                lower = wrist.parent;
                upper = lower.parent;
            }

            public void Solve(Vector3 target, Quaternion orientation, Vector3 fallbackBend)
            {
                Vector3 first = lower.position - upper.position;
                Vector3 second = wrist.position - lower.position;
                float upperLength = first.magnitude;
                float lowerLength = second.magnitude;
                Vector3 reach = target - upper.position;
                if (reach.sqrMagnitude < 0.000001f)
                {
                    return;
                }
                Vector3 direction = reach.normalized;
                float length = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                    upperLength + lowerLength - 0.001f);
                Vector3 bend = Vector3.ProjectOnPlane(first, direction);
                if (bend.sqrMagnitude < 0.0001f)
                {
                    bend = Vector3.ProjectOnPlane(fallbackBend, direction);
                }
                float along = (upperLength * upperLength + length * length - lowerLength * lowerLength) / (2f * length);
                Vector3 elbow = direction * along
                    + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
                upper.rotation = Quaternion.FromToRotation(first, elbow) * upper.rotation;
                Vector3 wristTarget = upper.position + direction * length;
                lower.rotation = Quaternion.FromToRotation(wrist.position - lower.position, wristTarget - lower.position) * lower.rotation;
                wrist.rotation = orientation;
            }
        }
    }
}
