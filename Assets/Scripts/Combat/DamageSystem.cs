using UnityEngine;

/// <summary>武器、命中与战斗组件 DamageSystem，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
public class DamageSystem : MonoBehaviour
{
    /// <summary>实例。</summary>
    public static DamageSystem Instance { get; private set; }

    /// <summary>命中血迹效果。</summary>
    [Header("Hit Effects")]
    public GameObject hitBloodEffect;
    /// <summary>中文含义：命中火花效果。</summary>
    public GameObject hitSparkEffect;
    /// <summary>命中效果存活时长。</summary>
    public float hitEffectLifetime = 1f;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>SpawnHitEffect 的业务处理入口；本方法直接依赖 Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、Quaternion.LookRotation(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.PlayDamageFeedback(Vector3)；下游（含内部回调体） Object.Destroy(Object, float)、Object.Instantiate&lt;GameObject&gt;(GameObject, Vector3, Quaternion)、Quaternion.LookRotation(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="normal">表面法线。</param>
    /// <param name="isHeadshot">是否爆头。</param>
    public void SpawnHitEffect(Vector3 position, Vector3 normal, bool isHeadshot)
    {
        ChapterOneEffects.TryPlay(ChapterOneEffect.SlashBlood, position, normal);
    }

    /// <summary>ApplyDamageToPlayer 的业务处理入口；本方法直接依赖 LeonController.TakeDamage(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.TakeDamage(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseDamage">基础伤害。</param>
    public void ApplyDamageToPlayer(float baseDamage)
    {
        // 玩家。
LeonController player = LeonController.Instance;
        if (player != null)
        {
            player.TakeDamage(baseDamage);
        }
    }
}
