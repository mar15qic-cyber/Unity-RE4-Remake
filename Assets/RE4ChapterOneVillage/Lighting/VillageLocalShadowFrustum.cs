using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>在新村庄主相机绘制时跳过完全位于视锥外的局部阴影。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class VillageLocalShadowFrustum : MonoBehaviour
{
    private const float FrustumMargin = 0.20f;

    [SerializeField] private Light[] spotlights = Array.Empty<Light>();

    [Tooltip("高画质时将这些聚光灯的阴影分辨率限制为中等。")]
    [SerializeField] private bool capSpotlightShadowResolution;

    private readonly Plane[] frustum = new Plane[6];
    private readonly List<(Light light, LightShadows shadows)> suppressed = new(5);
    private readonly List<(Light light, LightShadowResolution resolution)> cappedResolutions = new(5);
    private Camera renderCamera;
    private ShadowResolution lastQualityResolution;
    private bool hasQualityResolution;

    private void Awake()
    {
        renderCamera = GetComponent<Camera>();
    }

    private void OnPreCull()
    {
        RestoreShadows();
        if (!renderCamera || spotlights.Length == 0 || renderCamera.stereoEnabled || renderCamera.actualRenderingPath != RenderingPath.DeferredShading)
        {
            return;
        }

        if (capSpotlightShadowResolution)
        {
            UpdateSpotlightShadowResolution();
        }

        GeometryUtility.CalculateFrustumPlanes(renderCamera.projectionMatrix * renderCamera.worldToCameraMatrix, frustum);
        foreach (Light light in spotlights)
        {
            if (!light || !light.isActiveAndEnabled || light.type != LightType.Spot || light.cookie || light.shadows == LightShadows.None)
            {
                continue;
            }

            if (IsOutsideFrustum(light))
            {
                suppressed.Add((light, light.shadows));
                light.shadows = LightShadows.None;
            }
        }
    }

    private bool IsOutsideFrustum(Light light)
    {
        Transform lightTransform = light.transform;
        Vector3 apex = lightTransform.position;
        Vector3 axis = lightTransform.forward;
        float halfAngle = light.spotAngle * 0.5f * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(halfAngle);
        float sine = Mathf.Sin(halfAngle);
        float range = light.range;

        // 无自定义遮片的聚光灯覆盖球形扇区。仅在整个扇区都位于某个视锥面外时跳过阴影。
        foreach (Plane plane in frustum)
        {
            float alignment = Mathf.Clamp(Vector3.Dot(plane.normal, axis), -1f, 1f);
            float projection = 1f;
            if (alignment < cosine)
            {
                projection = alignment * cosine + Mathf.Sqrt(Mathf.Max(0f, 1f - alignment * alignment)) * sine;
            }

            if (plane.GetDistanceToPoint(apex) + range * Mathf.Max(0f, projection) < -FrustumMargin)
            {
                return true;
            }
        }

        return false;
    }

    private void OnPostRender()
    {
        RestoreShadows();
    }

    private void OnDisable()
    {
        RestoreShadows();
        RestoreSpotlightShadowResolution();
        hasQualityResolution = false;
    }

    private void UpdateSpotlightShadowResolution()
    {
        ShadowResolution qualityResolution = QualitySettings.shadowResolution;
        if (hasQualityResolution && lastQualityResolution == qualityResolution)
        {
            return;
        }

        RestoreSpotlightShadowResolution();
        lastQualityResolution = qualityResolution;
        hasQualityResolution = true;
        if (qualityResolution <= ShadowResolution.Medium)
        {
            return;
        }

        foreach (Light light in spotlights)
        {
            if (!light || light.type != LightType.Spot)
            {
                continue;
            }

            LightShadowResolution previousResolution = light.shadowResolution;
            if (previousResolution == LightShadowResolution.Low || previousResolution == LightShadowResolution.Medium)
            {
                continue;
            }

            cappedResolutions.Add((light, previousResolution));
            light.shadowResolution = LightShadowResolution.Medium;
        }
    }

    private void RestoreSpotlightShadowResolution()
    {
        foreach (var entry in cappedResolutions)
        {
            if (entry.light && entry.light.shadowResolution == LightShadowResolution.Medium)
            {
                entry.light.shadowResolution = entry.resolution;
            }
        }

        cappedResolutions.Clear();
    }

    private void RestoreShadows()
    {
        foreach (var entry in suppressed)
        {
            if (entry.light && entry.light.shadows == LightShadows.None)
            {
                entry.light.shadows = entry.shadows;
            }
        }

        suppressed.Clear();
    }
}
