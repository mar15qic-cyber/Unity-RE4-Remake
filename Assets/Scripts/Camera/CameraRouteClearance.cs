using UnityEngine;

internal static class CameraRouteClearance
{
    internal static float ReadNearPlaneRadius(float nearClip, float verticalFov, float aspect)
    {
        float halfHeight = Mathf.Tan(Mathf.Clamp(verticalFov, 1f, 150f) * .5f * Mathf.Deg2Rad) * nearClip;
        return Mathf.Max(.1f, Mathf.Sqrt(halfHeight * halfHeight * (1f + aspect * aspect) + nearClip * nearClip));
    }

    internal static bool Sweep(Vector3 from, Vector3 to, float radius, int mask, RaycastHit[] hits,
        Transform ignoreFirst, Transform ignoreSecond, out float distance)
    {
        Vector3 delta = to - from;
        distance = delta.magnitude;
        if (distance < .001f)
        {
            return false;
        }

        int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, hits, distance, mask, QueryTriggerInteraction.Ignore);
        if (count >= hits.Length)
        {
            distance = 0f;
            return true;
        }
        bool blocked = false;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = hits[index];
            if (!Ignore(hit.collider, ignoreFirst, ignoreSecond) && hit.distance <= distance)
            {
                distance = hit.distance;
                blocked = true;
            }
        }
        return blocked;
    }

    internal static bool HasOverlap(Vector3 position, float radius, int mask, Collider[] hits,
        Transform ignoreFirst, Transform ignoreSecond)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, hits, mask, QueryTriggerInteraction.Ignore);
        if (count >= hits.Length)
        {
            return true;
        }
        for (int index = 0; index < count; index++)
        {
            if (!Ignore(hits[index], ignoreFirst, ignoreSecond))
            {
                return true;
            }
        }
        return false;
    }

    private static bool Ignore(Collider collider, Transform first, Transform second)
    {
        return collider == null || first != null && collider.transform.IsChildOf(first)
            || second != null && collider.transform.IsChildOf(second);
    }
}
