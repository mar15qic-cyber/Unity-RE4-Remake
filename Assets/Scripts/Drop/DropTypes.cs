using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>掉落候选与权重因子共享的类别，掉落宿主保存上一结果类别用于重复惩罚；保持原枚举数值。</summary>
public enum DropCategory
{
    /// <summary>中文含义：金钱。比塞塔金钱，正常掉落由独立金钱流程处理。</summary>
    Money, // 比塞塔（金钱）
    /// <summary>中文含义：弹药。枪械弹药，受武器持有和备弹数量影响。</summary>
    Ammo, // 弹药
    /// <summary>中文含义：草药。草药，受生命比例与全局草药权重倍率影响。</summary>
    Herb, // 草药
    /// <summary>中文含义：首个急救。急救喷雾，受生命比例因子影响。</summary>
    FirstAid, // 急救喷雾
    /// <summary>中文含义：手雷。手榴弹，使用对应区域倍率。</summary>
    Grenade, // 手榴弹
    /// <summary>中文含义：宝物。宝藏，使用对应区域倍率。</summary>
    Treasure, // 宝藏
    /// <summary>中文含义：无掉落。不掉落结果，宿主用于连续未掉落及类别状态记录。</summary>
    Nothing, // 不掉落
    /// <summary>中文含义：合成材质。火药和零件等材料，受快照库存数量因子影响。</summary>
    CraftingMaterial // 火药与合成零件
}

/// <summary>掉落宿主与区域配置共用的区域标识；SetZone 只改变后续配置选择，保持原资源枚举绑定。</summary>
public enum DropZone
{
    /// <summary>中文含义：村庄。村庄区域配置标识。</summary>
    Village, // 村庄
    /// <summary>中文含义：城堡。城堡区域配置标识。</summary>
    Castle, // 城堡
    /// <summary>中文含义：孤立区域。岛屿区域配置标识。</summary>
    Island // 岛屿
}

/// <summary>掉落表序列化保存的单个候选配置，宿主计算权重并随机数量，本记录不保存本局掉落计数。</summary>
[Serializable]
public class DropEntry
{
    /// <summary>中文含义：掉落物品数据。候选物品定义，缺失时权重计算和候选构建跳过。</summary>
    [Tooltip("掉落物品数据")]
    public ItemData item;
    /// <summary>中文含义：掉落类别（决定受哪些因子影响）。候选分类，决定适用因子及重复计数。</summary>
    [Tooltip("掉落类别（决定受哪些因子影响）")]
    public DropCategory category;
    /// <summary>中文含义：掉落数量最小值。成功选中后的数量随机下限，包含边界。</summary>
    [Tooltip("掉落数量最小值")]
    public int minCount = 1;
    /// <summary>中文含义：掉落数量最大值。成功选中后的数量随机上限，宿主通过加一传给整数随机保持包含边界。</summary>
    [Tooltip("掉落数量最大值")]
    public int maxCount = 1;
    /// <summary>中文含义：基础权重（与其他条目竞争）。与其他候选竞争的基础权重，规则依次乘动态因子。</summary>
    [Tooltip("基础权重（与其他条目竞争）")]
    public float baseWeight = 1f;
    /// <summary>中文含义：武器类型。弹药候选对应的武器，过滤未持有武器并选择备弹阈值。</summary>
    [Tooltip("对应的武器类型（仅弹药类有效，用于武器过滤：没有该武器则不掉）")]
    public WeaponType weaponType = WeaponType.Pistol;
}

/// <summary>弹药备弹阈值配置，DropWeightRules 按列表顺序选择首个覆盖当前数量的倍率，资产需保持既有排序。</summary>
[Serializable]
public class AmmoTier
{
    /// <summary>中文含义：弹药数量上限（含），玩家弹药 ≤ 此值时使用此倍率。覆盖的备弹数量上限，包含该值。</summary>
    [Tooltip("弹药数量上限（含），玩家弹药 ≤ 此值时使用此倍率")]
    public int maxAmmo = 20;
    /// <summary>中文含义：权重倍率。匹配本层级时乘入的权重倍率。</summary>
    [Tooltip("权重倍率")]
    public float multiplier = 5f;
}

/// <summary>生命比例阈值配置，DropWeightRules 按列表顺序选择首个覆盖当前比例的倍率。</summary>
[Serializable]
public class HealthTier
{
    /// <summary>中文含义：血量比例上限（含），玩家血量 ≤ 此值时使用此倍率。覆盖的生命比例上限，包含该值。</summary>
    [Range(0f, 1f)]
    [Tooltip("血量比例上限（含），玩家血量 ≤ 此值时使用此倍率")]
    public float maxHealthRatio = 0.3f;
    /// <summary>中文含义：权重倍率。匹配本生命层级时乘入的权重倍率。</summary>
    [Tooltip("权重倍率")]
    public float multiplier = 6f;
}

/// <summary>单区域各类别倍率配置，由 DropTable 按区域查询后交给权重规则；查询不修改配置。</summary>
[Serializable]
public class ZoneConfig
{
    /// <summary>中文含义：区。本条配置所属区域标识，DropTable 采用首个匹配项。</summary>
    public DropZone zone = DropZone.Village;
    /// <summary>中文含义：金钱掉落倍率。金钱类别的区域倍率。</summary>
    [Tooltip("金钱掉落倍率")]
    public float moneyMultiplier = 1f;
    /// <summary>中文含义：弹药掉落倍率。弹药类别的区域倍率。</summary>
    [Tooltip("弹药掉落倍率")]
    public float ammoMultiplier = 1f;
    /// <summary>中文含义：草药掉落倍率。草药类别的区域倍率。</summary>
    [Tooltip("草药掉落倍率")]
    public float herbMultiplier = 1f;
    /// <summary>中文含义：急救喷雾掉落倍率。急救喷雾类别的区域倍率。</summary>
    [Tooltip("急救喷雾掉落倍率")]
    public float firstAidMultiplier = 1f;
    /// <summary>中文含义：手榴弹掉落倍率。手榴弹类别的区域倍率。</summary>
    [Tooltip("手榴弹掉落倍率")]
    public float grenadeMultiplier = 1f;
    /// <summary>中文含义：宝藏掉落倍率。宝藏类别的区域倍率。</summary>
    [Tooltip("宝藏掉落倍率")]
    public float treasureMultiplier = 1f;
    /// <summary>中文含义：合成材料掉落倍率。合成材料类别的区域倍率。</summary>
    [Tooltip("合成材料掉落倍率")]
    public float craftingMaterialMultiplier = 1f;
    /// <summary>区域因子同步查询入口，按候选类别读取对应倍率，不掉落与未知类别沿用倍率一。</summary>
    /// <param name="category">中文含义：类别。当前候选的掉落类别。</param>
    public float GetMultiplier(DropCategory category)
    {
        if (category == DropCategory.Money)
        {
            return moneyMultiplier;
        }
        else if (category == DropCategory.Ammo)
        {
            return ammoMultiplier;
        }
        else if (category == DropCategory.Herb)
        {
            return herbMultiplier;
        }
        else if (category == DropCategory.FirstAid)
        {
            return firstAidMultiplier;
        }
        else if (category == DropCategory.Grenade)
        {
            return grenadeMultiplier;
        }
        else if (category == DropCategory.Treasure)
        {
            return treasureMultiplier;
        }
        else if (category == DropCategory.CraftingMaterial)
        {
            return craftingMaterialMultiplier;
        }

        return 1f;
    }
}
