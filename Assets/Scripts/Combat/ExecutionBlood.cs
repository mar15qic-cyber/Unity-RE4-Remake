using UnityEngine;

internal static class ExecutionBlood
{
    private static readonly RaycastHit[] surfaces = new RaycastHit[32];

    internal static bool FindSurface(Vector3 start, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.RaycastNonAlloc(start, direction, surfaces, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            var hit = surfaces[index];
            if (hit.distance >= best || hit.rigidbody != null ||
                hit.collider.GetComponentInParent<LeonController>() != null ||
                hit.collider.GetComponentInParent<VillagerController>() != null)
            {
                continue;
            }
            closest = hit;
            best = hit.distance;
        }
        return best < float.PositiveInfinity;
    }

    internal static void Mark(Vector3 start, Vector3 direction, float distance, float size)
    {
        var bank = ExecutionGeometry.Load();
        var pools = GameObjectPoolManager.Instance;
        if (bank == null || bank.bloodMark == null || pools == null ||
            !FindSurface(start, direction, distance, out var hit))
        {
            return;
        }
        var pool = pools.GetVisualPool(bank.bloodMark);
        if (pool.ActiveCount >= 28)
        {
            return;
        }
        var orientation = Quaternion.LookRotation(-hit.normal) *
            Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
        // 在台阶和门框边缘收小印迹，避免把贴花悬在空中。
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = orientation * (Quaternion.AngleAxis(i * 90f, Vector3.forward) * Vector3.right);
            Vector3 probe = hit.point + corner * size * .46f + hit.normal * .045f;
            if (!FindSurface(probe, -hit.normal, .09f, out var edge) || Vector3.Dot(edge.normal, hit.normal) < .92f)
            {
                size *= .5f;
                break;
            }
        }
        GameObject mark = pool.Get(hit.point + hit.normal * .004f, orientation, null);
        if (mark != null)
        {
            mark.transform.localScale = new Vector3(size, size * .83f, 1f);
            pools.ReturnDelayed(mark, 22f);
        }
    }
}
