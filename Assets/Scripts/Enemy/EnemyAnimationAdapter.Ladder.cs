using UnityEngine;

public partial class EnemyAnimationAdapter
{
    internal static void FitLadderBoneContact(Transform upper, Transform lower, Transform tip, Vector3 contact)
    {
        FitLadderBoneContact(upper, lower, tip, contact, Vector3.zero, tip.rotation);
    }

    internal static void FitLadderBoneContact(Transform upper, Transform lower, Transform tip, Vector3 contact,
        Vector3 bendHint, Quaternion tipRotation)
    {
        Vector3 upperSegment = lower.position - upper.position;
        Vector3 lowerSegment = tip.position - lower.position;
        float upperLength = upperSegment.magnitude;
        float lowerLength = lowerSegment.magnitude;
        Vector3 reach = contact - upper.position;
        Vector3 direction = reach.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(upperSegment, direction).normalized;
        Vector3 guidedBend = Vector3.ProjectOnPlane(bendHint, direction).normalized;
        if (guidedBend.sqrMagnitude > 0.9f)
        {
            bend = guidedBend;
        }

        if (upperLength < 0.01f || lowerLength < 0.01f || bend.sqrMagnitude < 0.9f || reach.sqrMagnitude < 0.001f)
        {
            return;
        }

        float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
        float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        upper.rotation = Quaternion.FromToRotation(upperSegment, direction * along + bend * height) * upper.rotation;
        Vector3 target = upper.position + direction * distance;
        lower.rotation = Quaternion.FromToRotation(tip.position - lower.position, target - lower.position) * lower.rotation;
        tip.rotation = tipRotation;
    }
}
