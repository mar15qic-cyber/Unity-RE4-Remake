/// <summary>玩家武器动作通道的查询阶段，由动作协调器投影到快照，供旧状态消费者读取；独立于移动与全身通道。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩家武器阶段")]
public enum PlayerWeaponPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。武器通道没有需要覆盖移动状态的阶段。</summary>
    [UnityEngine.InspectorName("无")]
    None,
    /// <summary>中文含义：瞄准。武器通道处于瞄准。</summary>
    [UnityEngine.InspectorName("瞄准")]
    Aim,
    /// <summary>中文含义：射击。武器通道处于射击。</summary>
    [UnityEngine.InspectorName("射击")]
    Shoot,
    /// <summary>中文含义：换弹。武器通道处于装填事务。</summary>
    [UnityEngine.InspectorName("装填")]
    Reload
}

/// <summary>玩家全身动作通道的查询阶段，在兼容状态投影中优先于武器和移动；事务仍由动作协调器拥有。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩家全身阶段")]
public enum PlayerFullBodyPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。全身通道没有需要覆盖其他通道的阶段。</summary>
    [UnityEngine.InspectorName("无")]
    None,
    /// <summary>中文含义：近战。体术或近战动作阶段。</summary>
    [UnityEngine.InspectorName("近战")]
    Melee,
    /// <summary>中文含义：单元格阻断。格挡或弹反兼容阶段，保留既有成员名称。</summary>
    [UnityEngine.InspectorName("格挡")]
    CellBlock,
    /// <summary>中文含义：通行。翻越等通行动作阶段。</summary>
    [UnityEngine.InspectorName("通行")]
    Traversal,
    /// <summary>中文含义：受击反应。受击动作阶段。</summary>
    [UnityEngine.InspectorName("受击")]
    HitReaction,
    /// <summary>中文含义：已配对动作。配对会话动作阶段。</summary>
    [UnityEngine.InspectorName("配对")]
    PairedAction,
    /// <summary>中文含义：结束结束。死亡终止阶段。</summary>
    [UnityEngine.InspectorName("终止")]
    EndEnd
}

/// <summary>
/// LeonAnimationActionCoordinator.PlayerSnapshot 构造的玩家动作值快照，分别保留移动、武器和全身通道及事务编号。
/// LeonController.currentState 等旧消费者读取 CompatibleState；投影只查询，不绕过协调器审批或重新拥有状态。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩家动作快照")]
public readonly struct PlayerActionSnapshot
{
    /// <summary>中文含义：移动。构造时的移动阶段，全身与武器均无覆盖阶段时作为兼容查询结果。</summary>
    public readonly LeonState movement;
    /// <summary>中文含义：当前玩家武器阶段。构造时武器通道的阶段。</summary>
    public readonly PlayerWeaponPhase weapon;
    /// <summary>中文含义：全身。构造时全身通道的阶段，在兼容查询中优先处理。</summary>
    public readonly PlayerFullBodyPhase fullBody;
    /// <summary>中文含义：武器事务。武器通道事务编号，供消费者关联动作标记或诊断。</summary>
    public readonly int weaponTransaction;
    /// <summary>中文含义：全身事务。全身通道事务编号，供消费者关联动作标记或诊断。</summary>
    public readonly int fullBodyTransaction;
    /// <summary>动作协调器按当前通道状态构造值副本，不在构造时执行动画、取消或资源交接。</summary>
    /// <param name="movement">中文含义：移动。移动阶段。</param>
    /// <param name="weapon">中文含义：当前玩家武器阶段。武器阶段。</param>
    /// <param name="fullBody">中文含义：全身。全身阶段。</param>
    /// <param name="weaponTransaction">中文含义：武器事务。武器事务编号。</param>
    /// <param name="fullBodyTransaction">中文含义：全身事务。全身事务编号。</param>
    public PlayerActionSnapshot(LeonState movement, PlayerWeaponPhase weapon, PlayerFullBodyPhase fullBody, int weaponTransaction, int fullBodyTransaction)
    {
        this.movement = movement;
        this.weapon = weapon;
        this.fullBody = fullBody;
        this.weaponTransaction = weaponTransaction;
        this.fullBodyTransaction = fullBodyTransaction;
    }

    /// <summary>中文含义：兼容状态。旧消费者查询的单枚举投影：已识别全身阶段优先，其次武器阶段，最后返回移动；未知阶段保留原回退行为。</summary>
    public LeonState CompatibleState
    {
        get
        {
            if (fullBody == PlayerFullBodyPhase.Melee)
            {
                return LeonState.MeleeAttacking;
            }
            else if (fullBody == PlayerFullBodyPhase.CellBlock)
            {
                return LeonState.Parrying;
            }
            else if (fullBody == PlayerFullBodyPhase.Traversal)
            {
                return LeonState.Traversing;
            }
            else if (fullBody == PlayerFullBodyPhase.HitReaction)
            {
                return LeonState.Staggered;
            }
            else if (fullBody == PlayerFullBodyPhase.PairedAction)
            {
                return LeonState.QTE;
            }
            else if (fullBody == PlayerFullBodyPhase.EndEnd)
            {
                return LeonState.Dead;
            }

            if (weapon == PlayerWeaponPhase.Aim)
            {
                return LeonState.Aiming;
            }
            else if (weapon == PlayerWeaponPhase.Shoot)
            {
                return LeonState.Shooting;
            }
            else if (weapon == PlayerWeaponPhase.Reload)
            {
                return LeonState.Reloading;
            }

            return movement;
        }
    }
}
