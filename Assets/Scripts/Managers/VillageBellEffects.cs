using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class VillageBellEffects : MonoBehaviour
{
    // 原始时间轴的末镜从教堂门前拉回广场。
    private const double ChurchPullbackStart = 55d;
    private const double ChurchOcclusionResume = 62.25d;

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
    private bool previousPuddleEnabled;
    private bool previousOcclusionCulling;
    private readonly Plane[] reflectionFrustum = new Plane[6];
    private readonly List<Renderer> sceneDoorRenderers = new List<Renderer>();
    private readonly List<Renderer> hiddenDoorRenderers = new List<Renderer>();

    private void OnEnable()
    {
        source = GetComponent<Camera>();
        previousOcclusionCulling = source.useOcclusionCulling;
        previousPuddleEnabled = puddle.enabled;
        axePlayed = false;
        stepPlayed = false;
        water = new MaterialPropertyBlock();
        var reflectionObject = new GameObject("村庄过场水面反射", typeof(Camera));
        reflectionObject.hideFlags = HideFlags.HideAndDontSave;
        mirror = reflectionObject.GetComponent<Camera>();
        mirror.enabled = false;
        PrepareReflection();
        foreach (Renderer door in sceneDoorRenderers)
        {
            if (door != null && !door.forceRenderingOff)
            {
                hiddenDoorRenderers.Add(door);
                door.forceRenderingOff = true;
            }
        }
    }

    // 村庄装配时登记会被演出替换的门扇，切镜时只切换这些渲染器。
    internal void CacheSceneDoors(Transform sequenceRoot)
    {
        sceneDoorRenderers.Clear();
        var transforms = new List<Transform>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            root.GetComponentsInChildren(true, transforms);
            foreach (Transform item in transforms)
            {
                if (item.name == doorSourceName || item.name == interactiveDoorName)
                {
                    if (!item.IsChildOf(sequenceRoot))
                    {
                        sceneDoorRenderers.AddRange(item.GetComponentsInChildren<Renderer>(true));
                    }
                }
            }
        }
    }

    internal void PrepareReflection()
    {
        if (reflection != null)
        {
            return;
        }
        // 倒影主要显示近处的斧头与靴子，按输出分辨率缩放并限制开销。
        int width = Mathf.Clamp(Screen.width / 2, 128, 768);
        int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)Screen.height / Mathf.Max(1, Screen.width)));
        reflection = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
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
        // 末镜穿过门前的烘焙遮挡单元，那里会误剔除已经入画的树冠、围栏和屋顶边缘。
        double shotTime = director.time;
        bool crossesChurchCells = shotTime >= ChurchPullbackStart && shotTime < ChurchOcclusionResume;
        bool useOcclusion = previousOcclusionCulling && !crossesChurchCells;
        if (source.useOcclusionCulling != useOcclusion)
        {
            source.useOcclusionCulling = useOcclusion;
        }
        float time = (float)shotTime;
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
        GeometryUtility.CalculateFrustumPlanes(source, reflectionFrustum);
        if (!GeometryUtility.TestPlanesAABB(reflectionFrustum, puddle.bounds))
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
            // 片中水面只反射近处角色、屋檐和天空，无需远景的延迟光照缓冲。
            mirror.renderingPath = RenderingPath.Forward;
            mirror.farClipPlane = Mathf.Min(source.farClipPlane, 64f);
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
        source.useOcclusionCulling = previousOcclusionCulling;
        puddle.enabled = previousPuddleEnabled;
        foreach (var renderer in hiddenDoorRenderers)
        {
            if (renderer != null)
            {
                renderer.forceRenderingOff = false;
            }
        }
        hiddenDoorRenderers.Clear();
        ReleaseReflection();
        if (mirror != null)
        {
            Destroy(mirror.gameObject);
        }
    }
}
