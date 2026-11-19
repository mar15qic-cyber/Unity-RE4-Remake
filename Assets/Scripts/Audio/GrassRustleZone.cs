using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class GrassRustleZone : MonoBehaviour
{
    private static readonly List<GrassRustleZone> activeZones = new List<GrassRustleZone>();
    private BoxCollider volume;

    private void OnEnable()
    {
        volume = GetComponent<BoxCollider>();
        activeZones.Add(this);
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
    }

    public static bool Contains(Vector3 position)
    {
        foreach (GrassRustleZone zone in activeZones)
        {
            if (zone.volume == null || !zone.volume.enabled || !zone.volume.bounds.Contains(position))
            {
                continue;
            }

            Vector3 point = zone.transform.InverseTransformPoint(position) - zone.volume.center;
            Vector3 halfSize = zone.volume.size * 0.5f;
            if (Mathf.Abs(point.x) <= halfSize.x && Mathf.Abs(point.y) <= halfSize.y && Mathf.Abs(point.z) <= halfSize.z)
            {
                return true;
            }
        }

        return false;
    }
}
