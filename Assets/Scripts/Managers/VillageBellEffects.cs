using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class VillageBellEffects : MonoBehaviour
{
    [Header("水洼、落斧与靴子水花")]
    public PlayableDirector director;
    public Renderer puddle;
    public ParticleSystem axeSplash;
    public ParticleSystem stepSplash;
    public Light fireLight;
    [Header("演出角度的门扇")]
    public string doorSourceName;
    [Tooltip("正式场景中控制教堂交互门扇的根物体。")]
    public string interactiveDoorName;

    private Camera source;
    private Camera mirror;
    private RenderTexture reflection;
    private MaterialPropertyBlock water;
    private bool rendering;
    private bool axePlayed;
    private bool stepPlayed;
    private float previousFogDensity;
    private readonly List<Renderer> hiddenDoorRenderers = new List<Renderer>();
    private readonly List<LODGroup> sceneryLods = new List<LODGroup>();

    private void OnEnable()
    {
        source = GetComponent<Camera>();
        source.useOcclusionCulling = false;
        source.farClipPlane = Mathf.Max(source.farClipPlane, 650f);
        axePlayed = false;
        stepPlayed = false;
        water = new MaterialPropertyBlock();
        previousFogDensity = RenderSettings.fogDensity;
        if (RenderSettings.fog && RenderSettings.fogMode != FogMode.Linear)
        {
            RenderSettings.fogDensity = Mathf.Max(previousFogDensity, 0.022f);
        }
        var reflectionObject = new GameObject("村庄过场水面反射", typeof(Camera));
        reflectionObject.hideFlags = HideFlags.HideAndDontSave;
        mirror = reflectionObject.GetComponent<Camera>();
        mirror.enabled = false;
        PrepareReflection();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (transform.IsChildOf(root.transform))
            {
                continue;
            }

            sceneryLods.AddRange(root.GetComponentsInChildren<LODGroup>(true));
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name != doorSourceName && item.name != interactiveDoorName)
                {
                    continue;
                }
                foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.forceRenderingOff)
                    {
                        hiddenDoorRenderers.Add(renderer);
                        renderer.forceRenderingOff = true;
                    }
                }
            }
        }
        foreach (var lod in sceneryLods)
        {
            lod.ForceLOD(0);
        }
    }

    internal void PrepareReflection()
    {
        if (reflection != null)
        {
            return;
        }
        reflection = new RenderTexture(1024, 576, 24, RenderTextureFormat.DefaultHDR);
        reflection.name = "村庄过场水面反射";
        reflection.Create();
    }

    internal void ReleaseReflection()
    {
        if (reflection == null)
        {
            return;
        }
        reflection.Release();
        Destroy(reflection);
        reflection = null;
    }

    private void LateUpdate()
    {
        float time = (float)director.time;
        bool waterShot = time >= 10.30f && time <= 19.45f;
        puddle.enabled = waterShot;
        water.SetFloat("_ShotTime", time);
        water.SetTexture("_ReflectionTex", reflection);
        puddle.SetPropertyBlock(water);
        if (!axePlayed && time >= 10.51f)
        {
            axePlayed = true;
            axeSplash.Play();
        }
        if (!stepPlayed && time >= 17.11f)
        {
            stepPlayed = true;
            stepSplash.Play();
        }
        if (fireLight != null)
        {
            float slowFlicker = Mathf.PerlinNoise(time * 3.8f, 0.42f);
            float fastFlicker = Mathf.PerlinNoise(time * 11f, 0.78f);
            fireLight.intensity = 0.95f + 1.35f * slowFlicker + 0.34f * fastFlicker;
        }
    }

    private void OnPreCull()
    {
        if (rendering || mirror == null || !puddle.enabled || !source.enabled)
        {
            return;
        }
        rendering = true;
        bool invert = GL.invertCulling;
        try
        {
            mirror.CopyFrom(source);
            mirror.enabled = false;
            mirror.targetTexture = reflection;
            mirror.depthTextureMode = DepthTextureMode.None;
            mirror.useOcclusionCulling = false;
            float level = puddle.transform.position.y;
            var reflected = Matrix4x4.identity;
            reflected.m11 = -1f;
            reflected.m13 = 2f * level;
            mirror.transform.SetPositionAndRotation(reflected.MultiplyPoint(source.transform.position),
                Quaternion.LookRotation(reflected.MultiplyVector(source.transform.forward), Vector3.up));
            mirror.worldToCameraMatrix = source.worldToCameraMatrix * reflected;
            Vector3 clipPoint = mirror.worldToCameraMatrix.MultiplyPoint(new Vector3(0f, level + 0.005f, 0f));
            Vector3 clipNormal = mirror.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;
            var clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
            mirror.projectionMatrix = mirror.CalculateObliqueMatrix(clipPlane);
            puddle.enabled = false;
            GL.invertCulling = !invert;
            mirror.Render();
        }
        finally
        {
            GL.invertCulling = invert;
            puddle.enabled = true;
            rendering = false;
        }
    }

    private void OnDisable()
    {
        RenderSettings.fogDensity = previousFogDensity;
        foreach (var renderer in hiddenDoorRenderers)
        {
            if (renderer != null)
            {
                renderer.forceRenderingOff = false;
            }
        }
        hiddenDoorRenderers.Clear();
        foreach (var lod in sceneryLods)
        {
            if (lod != null)
            {
                lod.ForceLOD(-1);
            }
        }
        sceneryLods.Clear();
        ReleaseReflection();
        if (mirror != null)
        {
            Destroy(mirror.gameObject);
        }
    }
}
