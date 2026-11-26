using UnityEngine;

/// <summary>菜单环境仅运行展示动态，计时不受暂停影响。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "村庄菜单氛围")]
public class VillageMenuAtmosphere : MonoBehaviour
{
    /// <summary>火光。</summary>
    [Header("环境动态")]
    [UnityEngine.Serialization.FormerlySerializedAs("火光")]
    [UnityEngine.InspectorName("火光")]
    public Light[] firelight;
    /// <summary>基础亮度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("基础亮度")]
    [UnityEngine.InspectorName("基础亮度")]
    public float baseBrightness = .65f;
    /// <summary>波动幅度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("波动幅度")]
    [UnityEngine.InspectorName("波动幅度")]
    public float oscillationAmplitude = .16f;
    /// <summary>展示相机。</summary>
    [Header("街道横移")]
    [UnityEngine.Serialization.FormerlySerializedAs("展示相机")]
    [UnityEngine.InspectorName("展示相机")]
    public Camera presentationCamera;
    /// <summary>横移幅度。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("横移幅度")]
    [UnityEngine.InspectorName("横移幅度")]
    public float strafeAmplitude = 1.1f;
    /// <summary>效果往返变化一轮的持续时间。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("往返周期")]
    [UnityEngine.InspectorName("往返周期")]
    public float roundTripCycle = 100f;
    /// <summary>开始的位置。</summary>
    Vector3 startingPosition;
    /// <summary>计时器。</summary>
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

        // 当前遍历索引。
for (int i = 0; i < firelight.Length; i++)
        {
            if (firelight[i] != null)
            {
                firelight[i].intensity = baseBrightness + Mathf.Sin(timer * 3.1f + i) * oscillationAmplitude + Mathf.Sin(timer * 7.7f + i * 2f) * oscillationAmplitude * .35f;
            }
        }
    }
}
