using UnityEngine;

[DisallowMultipleComponent]
public sealed class WindowFracture : MonoBehaviour, IDamageable
{
    [Header("原版窗户")]
    [Tooltip("完好窗框和完整玻璃的渲染器。")]
    public Renderer[] intactRenderers = System.Array.Empty<Renderer>();
    public GameObject brokenVisual;
    [Tooltip("仅覆盖玻璃所在的开口，窗台和墙体仍由地图碰撞支撑。")]
    public BoxCollider paneCollider;

    [Header("碎裂表现")]
    public AudioEventData breakSound;
    public GameObject pooledDebris;
    [Min(0.1f)] public float debrisLifetime = 2.5f;

    public bool IsBroken { get; private set; }

    internal bool OwnsCollider(Collider candidate) => candidate != null && candidate == paneCollider;

    internal bool TryFractureTraversalContact(Vector3 from, Vector3 to)
    {
        if (IsBroken || paneCollider == null || !paneCollider.enabled)
        {
            return false;
        }

        Vector3 direction = to - from;
        if (direction.sqrMagnitude <= .000001f)
        {
            return false;
        }

        var ray = new Ray(from, direction.normalized);
        if (paneCollider.Raycast(ray, out var hit, direction.magnitude))
        {
            return Fracture(hit.point, direction);
        }

        if ((paneCollider.ClosestPoint(to) - to).sqrMagnitude <= .000001f)
        {
            return Fracture(to, direction);
        }
        return false;
    }

    internal bool Fracture(Vector3 contact, Vector3 direction)
    {
        if (IsBroken || !isActiveAndEnabled || paneCollider == null || !paneCollider.enabled)
        {
            return false;
        }

        Vector3 point = paneCollider.ClosestPoint(contact);
        IsBroken = true;
        paneCollider.enabled = false;
        foreach (Renderer intact in intactRenderers)
        {
            if (intact != null)
            {
                intact.enabled = false;
            }
        }

        if (brokenVisual != null)
        {
            brokenVisual.SetActive(true);
        }

        if (breakSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play3D(breakSound, point);
        }

        if (pooledDebris != null && GameObjectPoolManager.Instance != null)
        {
            Vector3 outward = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (outward.sqrMagnitude < 0.001f)
            {
                outward = paneCollider.transform.forward;
            }

            GameObject debris = GameObjectPoolManager.Instance.Get(pooledDebris, point, Quaternion.LookRotation(outward));
            if (debris != null)
            {
                GameObjectPoolManager.Instance.ReturnDelayed(debris, debrisLifetime);
            }
        }

        return true;
    }

    public void TakeDamage(float damage)
    {
        if (paneCollider != null)
        {
            TakeDamage(damage, paneCollider.bounds.center);
        }
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (damage <= 0f || paneCollider == null)
        {
            return;
        }

        Vector3 direction = paneCollider.transform.forward;
        if (LeonController.Instance != null)
        {
            direction = hitPoint - LeonController.Instance.transform.position;
        }

        Fracture(hitPoint, direction);
    }
}
