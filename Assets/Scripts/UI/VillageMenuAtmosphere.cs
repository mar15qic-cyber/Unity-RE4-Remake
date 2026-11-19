using UnityEngine;

/// <summary>菜单环境仅运行展示动态，计时不受暂停影响。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "村庄菜单氛围")]
public class VillageMenuAtmosphere : MonoBehaviour
{
    /// <summary>中文含义：火光。使用位置：InterfaceReworkBuilder.ConfigurationTitle()、VillageMenuAtmosphere.Update()。</summary>
    [Header("环境动态")]
    [UnityEngine.Serialization.FormerlySerializedAs("火光")]
    [UnityEngine.InspectorName("火光")]
    public Light[] firelight;
    /// <summary>中文含义：基础亮度。使用位置：VillageMenuAtmosphere.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("基础亮度")]
    [UnityEngine.InspectorName("基础亮度")]
    public float baseBrightness = .65f;
    /// <summary>中文含义：波动幅度。使用位置：VillageMenuAtmosphere.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("波动幅度")]
    [UnityEngine.InspectorName("波动幅度")]
    public float oscillationAmplitude = .16f;
    /// <summary>中文含义：展示相机。使用位置：InterfaceReworkBuilder.ConfigurationTitle()、VillageMenuAtmosphere.Start()、VillageMenuAtmosphere.Update()。</summary>
    [Header("街道横移")]
    [UnityEngine.Serialization.FormerlySerializedAs("展示相机")]
    [UnityEngine.InspectorName("展示相机")]
    public Camera presentationCamera;
    /// <summary>中文含义：横移幅度。使用位置：VillageMenuAtmosphere.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("横移幅度")]
    [UnityEngine.InspectorName("横移幅度")]
    public float strafeAmplitude = 1.1f;
    /// <summary>中文含义：效果往返变化一轮的持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("往返周期")]
    [UnityEngine.InspectorName("往返周期")]
    public float roundTripCycle = 100f;
    /// <summary>中文含义：开始的位置。使用位置：VillageMenuAtmosphere.Start()、VillageMenuAtmosphere.Update()。</summary>
    Vector3 startingPosition;
    /// <summary>中文含义：计时器。使用位置：作为 Mathf.Sin(float) 的参数、VillageMenuAtmosphere.Update()。</summary>
    float timer;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (presentationCamera != null)
        {
            startingPosition = presentationCamera.transform.position;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Max(float, float)、Mathf.Sin(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (presentationCamera != null)
        {
            presentationCamera.transform.position = startingPosition + presentationCamera.transform.right * (Mathf.Sin(timer * Mathf.PI * 2 / Mathf.Max(10, roundTripCycle)) * strafeAmplitude);
        }

        if (firelight == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 Mathf.Sin(float) 的参数、VillageMenuAtmosphere.Update()。
for (int i = 0; i < firelight.Length; i++)
        {
            if (firelight[i] != null)
            {
                firelight[i].intensity = baseBrightness + Mathf.Sin(timer * 3.1f + i) * oscillationAmplitude + Mathf.Sin(timer * 7.7f + i * 2f) * oscillationAmplitude * .35f;
            }
        }
    }
}
