using UnityEngine;

/// <summary>限制新村庄默认层网格的太阳投影距离。</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class VillageSunShadowDistance : MonoBehaviour
{
    private const int DefaultLayerIndex = 0;
    private const float MaxShadowDistance = 52f;

    private Light sun;
    private float previousDistance;
    private bool applied;

    private void OnEnable()
    {
        sun = GetComponent<Light>();
        if (sun.type != LightType.Directional)
        {
            return;
        }

        float[] distances = sun.layerShadowCullDistances;
        previousDistance = distances[DefaultLayerIndex];
        if (previousDistance > 0f && previousDistance <= MaxShadowDistance)
        {
            return;
        }

        distances[DefaultLayerIndex] = MaxShadowDistance;
        sun.layerShadowCullDistances = distances;
        applied = true;
    }

    private void OnDisable()
    {
        if (!applied || !sun)
        {
            return;
        }

        applied = false;
        float[] distances = sun.layerShadowCullDistances;
        if (distances[DefaultLayerIndex] == MaxShadowDistance)
        {
            distances[DefaultLayerIndex] = previousDistance;
            sun.layerShadowCullDistances = distances;
        }
    }
}
