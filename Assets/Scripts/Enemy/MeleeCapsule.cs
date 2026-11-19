using UnityEngine;

internal readonly struct MeleeCapsule
{
    internal readonly Vector3 start;
    internal readonly Vector3 end;
    internal readonly float radius;

    internal MeleeCapsule(Vector3 start, Vector3 end, float radius)
    {
        this.start = start;
        this.end = end;
        this.radius = radius;
    }

    internal static MeleeCapsule Interpolate(MeleeCapsule first, MeleeCapsule last, float time)
    {
        return new MeleeCapsule(Vector3.Lerp(first.start, last.start, time), Vector3.Lerp(first.end, last.end, time),
            Mathf.Lerp(first.radius, last.radius, time));
    }

    internal bool TrySweep(MeleeCapsule endPose, MeleeCapsule target, MeleeCapsule targetEnd, out float fraction,
        out Vector3 surface, out Vector3 normal, out float axisFraction)
    {
        fraction = 0f;
        surface = default;
        normal = default;
        axisFraction = 0f;
        float maximumTravel = Mathf.Max((endPose.start - start).magnitude, (endPose.end - end).magnitude)
            + Mathf.Max((targetEnd.start - target.start).magnitude, (targetEnd.end - target.end).magnitude)
            + Mathf.Abs(endPose.radius - radius) + Mathf.Abs(targetEnd.radius - target.radius);
        const float tolerance = .001f;
        // 以两胶囊端点的最大相对速度推进，防止快速挥击越过采样间隙。
        for (int step = 0; step < 48; step++)
        {
            MeleeCapsule weapon = Interpolate(this, endPose, fraction);
            MeleeCapsule body = Interpolate(target, targetEnd, fraction);
            weapon.ClosestAxes(body, out Vector3 attackPoint, out Vector3 bodyPoint, out axisFraction);
            Vector3 delta = attackPoint - bodyPoint;
            float distance = delta.magnitude;
            float gap = distance - weapon.radius - body.radius;
            if (gap <= tolerance)
            {
                if (distance > tolerance)
                {
                    normal = delta / distance;
                }
                else
                {
                    normal = (weapon.start + weapon.end - body.start - body.end).normalized;
                    if (normal.sqrMagnitude < .5f)
                    {
                        normal = Vector3.up;
                    }
                }

                surface = bodyPoint + normal * body.radius;
                return true;
            }

            if (maximumTravel <= tolerance || fraction >= 1f || gap > maximumTravel * (1f - fraction) + tolerance)
            {
                break;
            }

            fraction = Mathf.Min(1f, fraction + gap / maximumTravel);
        }

        return false;
    }

    private void ClosestAxes(MeleeCapsule other, out Vector3 firstPoint, out Vector3 secondPoint, out float first)
    {
        const float epsilon = .00000001f;
        Vector3 axis = end - start;
        Vector3 otherAxis = other.end - other.start;
        Vector3 separation = start - other.start;
        float length = axis.sqrMagnitude;
        float otherLength = otherAxis.sqrMagnitude;
        float alignment = Vector3.Dot(axis, otherAxis);
        float projected = Vector3.Dot(axis, separation);
        float otherProjected = Vector3.Dot(otherAxis, separation);
        first = 0f;
        float second = 0f;
        if (otherLength > epsilon)
        {
            float denominator = length * otherLength - alignment * alignment;
            if (denominator > epsilon * Mathf.Max(length, otherLength))
            {
                first = Mathf.Clamp01((alignment * otherProjected - projected * otherLength) / denominator);
            }

            second = Mathf.Clamp01((alignment * first + otherProjected) / otherLength);
        }

        if (length > epsilon)
        {
            first = Mathf.Clamp01((alignment * second - projected) / length);
            if (otherLength > epsilon)
            {
                second = Mathf.Clamp01((alignment * first + otherProjected) / otherLength);
            }
        }

        firstPoint = start + axis * first;
        secondPoint = other.start + otherAxis * second;
    }
}
