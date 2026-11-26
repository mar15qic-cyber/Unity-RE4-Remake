using UnityEngine;

/// <summary>界面交互与显示使用的共享配置资产 VillageDaylightConfiguration；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(fileName = "村庄日间配置", menuName = "游戏界面/村庄日间氛围")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "村庄日间配置")]
public class VillageDaylightConfiguration : ScriptableObject
{
    /// <summary>日光颜色。</summary>
    [Header("日光与补光")]
    [UnityEngine.Serialization.FormerlySerializedAs("日光颜色")]
    [UnityEngine.InspectorName("日光颜色")]
    public Color sunlightColor = new Color(1f, .95f, .84f);
    /// <summary>日光强度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("日光强度")]
    [UnityEngine.InspectorName("日光强度")]
    public float sunlightIntensity = 1.5f;
    /// <summary>日光角度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("日光角度")]
    [UnityEngine.InspectorName("日光角度")]
    public Vector3 sunlightAngle = new Vector3(44f, -38f, 0f);
    /// <summary>阴影强度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("阴影强度")]
    [UnityEngine.InspectorName("阴影强度")]
    public float shadowIntensity = .82f;
    /// <summary>阴影偏移。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("阴影偏移")]
    [UnityEngine.InspectorName("阴影偏移")]
    public float shadowOffset = .025f;
    /// <summary>法线偏移。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("法线偏移")]
    [UnityEngine.InspectorName("法线偏移")]
    public float normalOffset = .25f;
    /// <summary>天顶补光的颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("天顶补光")]
    [UnityEngine.InspectorName("天顶补光")]
    public Color zenithFillLight = new Color(.60f, .61f, .62f);
    /// <summary>水平补光的颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("水平补光")]
    [UnityEngine.InspectorName("水平补光")]
    public Color horizontalFillLight = new Color(.46f, .47f, .47f);
    /// <summary>地面补光的颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("地面补光")]
    [UnityEngine.InspectorName("地面补光")]
    public Color groundFillLight = new Color(.25f, .25f, .24f);
    /// <summary>雾色。</summary>
    [Header("远景空气")]
    [UnityEngine.Serialization.FormerlySerializedAs("雾色")]
    [UnityEngine.InspectorName("雾色")]
    public Color fogColorValue = new Color(.60f, .63f, .64f);
    /// <summary>雾浓度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("雾浓度")]
    [UnityEngine.InspectorName("雾浓度")]
    public float fogDensityValue = .012f;
    /// <summary>天空材质。</summary>
    [Header("云层")]
    [UnityEngine.Serialization.FormerlySerializedAs("天空材质")]
    [UnityEngine.InspectorName("天空材质")]
    public Material skyMaterial;
    /// <summary>云层亮部的颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("云层亮部")]
    [UnityEngine.InspectorName("云层亮部")]
    public Color cloudLayerHighlights = new Color(.83f, .84f, .83f);
    /// <summary>云层暗部的颜色。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("云层暗部")]
    [UnityEngine.InspectorName("云层暗部")]
    public Color cloudLayerShadows = new Color(.40f, .44f, .47f);
    /// <summary>云层速度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("云层速度")]
    [UnityEngine.InspectorName("云层速度")]
    public float cloudLayerSpeed = .00065f;
    /// <summary>Apply 的业务处理入口；本方法直接依赖 Material.SetColor(string, Color)、Material.SetFloat(string, float)、Quaternion.Euler(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationTitle()、VillageDaylightApplier.Start()、VillageInterfaceRefinementConfigurator.ApplyScene()；下游（含内部回调体） Material.SetColor(string, Color)、Material.SetFloat(string, float)、Quaternion.Euler(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sunlight">日光。</param>
    public void Apply(Light sunlight)
    {
        if (sunlight != null)
        {
            sunlight.color = sunlightColor;
            sunlight.intensity = sunlightIntensity;
            sunlight.transform.rotation = Quaternion.Euler(sunlightAngle);
            sunlight.shadowStrength = shadowIntensity;
            sunlight.shadowBias = shadowOffset;
            sunlight.shadowNormalBias = normalOffset;
            sunlight.shadows = LightShadows.Soft;
            RenderSettings.sun = sunlight;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = zenithFillLight;
        RenderSettings.ambientEquatorColor = horizontalFillLight;
        RenderSettings.ambientGroundColor = groundFillLight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColorValue;
        RenderSettings.fogDensity = fogDensityValue;
        if (skyMaterial != null)
        {
            skyMaterial.SetColor("_CloudLight", cloudLayerHighlights);
            skyMaterial.SetColor("_CloudShadow", cloudLayerShadows);
            skyMaterial.SetFloat("_CloudSpeed", cloudLayerSpeed);
        }
    }
}
