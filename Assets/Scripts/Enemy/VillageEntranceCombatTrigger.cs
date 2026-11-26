using UnityEngine;

/// <summary>敌人行为、动作与通行组件 VillageEntranceCombatTrigger，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[RequireComponent(typeof(BoxCollider))]
public class VillageEntranceCombatTrigger : MonoBehaviour
{
    /// <summary>当前敌人智能管理器。</summary>
    [Header("战斗管理器")]
    public EnemyAIManager manager;
    /// <summary>Configure 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;BoxCollider&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="owner">拥有者。</param>
    /// <param name="triggerSize">触发器尺寸。</param>
    public void Configure(EnemyAIManager owner, Vector3 triggerSize)
    {
        manager = owner;
        // 盒形碰撞体。
BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.center = Vector3.zero;
        boxCollider.size = triggerSize;
    }

    /// <summary>Unity 触发区域进入回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">另一个碰撞体。</param>
    void OnTriggerEnter(Collider other)
    {
        if (manager == null || manager.IsCombatUnlocked || manager.AwaitsVillageDiscovery)
        {
            return;
        }

        // 玩家。
        LeonController player = other.GetComponentInParent<LeonController>();
        if (player == null)
        {
            return;
        }

        manager.UnlockCombatAtVillageEntrance(player);
    }
}
