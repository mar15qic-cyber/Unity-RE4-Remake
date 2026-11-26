using UnityEngine;

/// <summary>
/// 敌人配对动画事件中继。动画事件只报告接触标记，不直接造成伤害。
/// </summary>
public class EnemyQTEAnimationEventRelay : MonoBehaviour
{
    /// <summary>参与者。</summary>
    [Header("参与者")]
    public VillagerQTEParticipant participant;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (participant == null)
        {
            participant = GetComponent<VillagerQTEParticipant>();
        }
    }

    /// <summary>OnQTEBiteContact 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、VillagerQTEParticipant.NotifyBiteContactFromAnimation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、VillagerQTEParticipant.NotifyBiteContactFromAnimation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnQTEBiteContact()
    {
        // 判空前缓存的参与者。
global::VillagerQTEParticipant receiverValue1 = participant;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.NotifyBiteContactFromAnimation();
        }
    }

    /// <summary>OnQTEAssassinationContact 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、VillagerQTEParticipant.NotifyAssassinationContactFromAnimation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、VillagerQTEParticipant.NotifyAssassinationContactFromAnimation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnQTEAssassinationContact()
    {
        // 判空前缓存的参与者。
global::VillagerQTEParticipant receiverValue2 = participant;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.NotifyAssassinationContactFromAnimation();
        }
    }
}
