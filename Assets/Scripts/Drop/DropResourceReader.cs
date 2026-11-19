using System.Collections.Generic;

/// <summary>
/// 掉落执行边界的数据读取适配器。DropManager 或兼容 WeightCalculator 调用 Read，
/// 从已存在的背包、玩家和武器宿主读取数据并构造快照，再将快照交给纯权重规则；不补建业务服务。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "掉落资源读取器")]
public static class DropResourceReader
{
    /// <summary>执行一次资源汇总，查询顺序保持背包、玩家、武器、生命和各枪械备弹。</summary>
    /// <returns>独立的掉落资源快照；缺失服务通过可用性字段及原默认值表达。</returns>
    public static DropResourceSnapshot Read()
    {
        // 没有背包时保持 null，使快照能区分无服务与空背包。
        // 中文含义：countValue 为“数量值”。
Dictionary<string, int> countValue = null;
        if (InventoryManager.HasInstance)
        {
            countValue = new Dictionary<string, int>();
            // 当前背包实例，按定义标识合并多个堆叠。
            // 中文含义：itemValue 为“物品值”。每轮从取得物品列表的返回结果取得一个元素，仅代表当前这一项。
foreach (var itemValue in InventoryManager.Instance.GetItems())
            {
                if (itemValue.item == null)
                {
                    continue;
                }

                // hasHas 接收已汇总数量，首次出现时使用字典查询的默认零。
                countValue.TryGetValue(itemValue.item.itemId, out int /* 中文含义：是否具有。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hasHas);
                countValue[itemValue.item.itemId] = hasHas + itemValue.count;
            }
        }

        // 仅当玩家已注册才读取单例，保持原入口不主动创建玩家的语义。
        // 中文含义：player 为“玩家”。
LeonController player = null;
        if (LeonController.HasInstance)
        {
            player = LeonController.Instance;
        }

        // 当前玩家上的武器宿主，用于可用性和备弹查询。
        // 中文含义：weapon 为“当前装备切换组件”。
LeonWeaponHandler weapon = null;
        if (player != null)
        {
            weapon = player.GetComponent<LeonWeaponHandler>();
        }

        // 在原构造参数位置保存武器可用性，避免后续备弹查询后才重新判断。
        // 中文含义：weaponAvailable 为“是否武器可用”。
bool weaponAvailable = weapon != null;
        // 先保存原构造参数位置的生命可用性，之后比例查询仍按原顺序独立核验。
        // 中文含义：healthAvailable 为“是否生命可用”。
bool healthAvailable = player != null && player.Health != null;
        // 没有生命模块时沿用满血比例默认值，规则另通过可用性字段决定是否应用因子。
        // 中文含义：healthRatio 为“生命比例”。
float healthRatio = 1;
        if (player != null && player.Health != null)
        {
            healthRatio = player.Health.HealthRatio;
        }

        // 手枪备弹读取结果，保持原查询顺序和单次调用次数。
        // 中文含义：pistol 为“手枪”。
int pistol = 0;
        if (weapon != null)
        {
            pistol = weapon.GetReserveAmmo(WeaponType.Pistol);
        }

        // 步枪备弹读取结果，无武器宿主时保持零。
        // 中文含义：rifle 为“步枪”。
int rifle = 0;
        if (weapon != null)
        {
            rifle = weapon.GetReserveAmmo(WeaponType.Rifle);
        }

        // 霰弹枪备弹读取结果，无武器宿主时保持零。
        // 中文含义：shotgunAmmo 为“霰弹枪弹药”。
int shotgunAmmo = 0;
        if (weapon != null)
        {
            shotgunAmmo = weapon.GetReserveAmmo(WeaponType.Shotgun);
        }

        return new DropResourceSnapshot(countValue, weaponAvailable, healthAvailable, healthRatio, pistol, rifle, shotgunAmmo);
    }
}
