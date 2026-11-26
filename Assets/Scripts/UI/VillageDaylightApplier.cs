using UnityEngine;

/// <summary>界面交互与显示组件 VillageDaylightApplier，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "村庄日间应用")]
public class VillageDaylightApplier : MonoBehaviour
{
    /// <summary>配置。</summary>
    [Header("共享日间配置")]
    [UnityEngine.Serialization.FormerlySerializedAs("配置")]
    [UnityEngine.InspectorName("配置")]
    public VillageDaylightConfiguration configuration;
    /// <summary>日光。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("日光")]
    [UnityEngine.InspectorName("日光")]
    public Light sunlight;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillageDaylightConfiguration.Apply(Light)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (configuration != null)
        {
            configuration.Apply(sunlight);
        }
    }
}
