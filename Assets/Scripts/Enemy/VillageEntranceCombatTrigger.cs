using UnityEngine;

/// <summary>敌人行为、动作与通行组件 VillageEntranceCombatTrigger，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[RequireComponent(typeof(BoxCollider))]
public class VillageEntranceCombatTrigger : MonoBehaviour
{
    /// <summary>中文含义：当前敌人智能管理器。使用位置：EnemyAIManager.ValidateSceneConfiguration()、VillageEntranceCombatTrigger.Configure(EnemyAIManager, Vector3)、VillageEntranceCombatTrigger.OnTriggerEnter(Collider)。</summary>
    [Header("战斗管理器")]
    public EnemyAIManager manager;
    /// <summary>Configure 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;BoxCollider&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;BoxCollider&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="owner">中文含义：拥有者。使用位置：VillageEntranceCombatTrigger.Configure(EnemyAIManager, Vector3)。</param>
    /// <param name="triggerSize">中文含义：触发器尺寸。使用位置：VillageEntranceCombatTrigger.Configure(EnemyAIManager, Vector3)。</param>
    public void Configure(EnemyAIManager owner, Vector3 triggerSize)
    {
        manager = owner;
        // boxCollider（盒形碰撞体）：保存 Component.GetComponent<BoxCollider>() 的返回结果，供当前步骤使用。使用位置：VillageEntranceCombatTrigger.Configure(EnemyAIManager, Vector3)。
BoxCollider boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.center = Vector3.zero;
        boxCollider.size = triggerSize;
    }

    /// <summary>Unity 触发区域进入回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponentInParent&lt;LeonController&gt;()、EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="other">中文含义：另一个碰撞体。使用位置：VillageEntranceCombatTrigger.OnTriggerEnter(Collider)。</param>
    void OnTriggerEnter(Collider other)
    {
        if (manager == null || manager.IsCombatUnlocked || manager.AwaitsVillageDiscovery)
        {
            return;
        }

        // player（玩家）：保存 Component.GetComponentInParent<LeonController>() 的返回结果，供当前步骤使用。使用位置：作为 EnemyAIManager.UnlockCombatAtVillageEntrance(LeonController) 的参数、VillageEntranceCombatTrigger.OnTriggerEnter(Collider)。
        LeonController player = other.GetComponentInParent<LeonController>();
        if (player == null)
        {
            return;
        }

        manager.UnlockCombatAtVillageEntrance(player);
    }
}
