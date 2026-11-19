using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 动态掉落权重的只读规则。DropManager 或 WeightCalculator 传入资源快照和配置，
/// CalculateWeight 按固定顺序相乘各因子后同步返回；不查找服务、不修改库存、不决定随机结果。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "掉落权重规则")]
public static class DropWeightRules
{
    /// <summary>计算单条候选的最终权重，顺序保持武器、资源、材料、生命、草药抑制、幸运、区域、重复衰减。</summary>
    /// <param name="entry">中文含义：当前掉落入口。候选条目，条目或物品为空时返回零。</param>
    /// <param name="table">中文含义：表。动态阈值和倍率配置；沿用调用方在需要其字段的分支保证有效的契约。</param>
    /// <param name="consecutiveNoDrops">中文含义：连续无掉落物集合。物品连续未掉落次数。</param>
    /// <param name="consecutiveSameCategory">中文含义：连续相同类别。当前候选类别连续出现次数。</param>
    /// <param name="zoneConfig">中文含义：区配置。当前区域配置，允许为空。</param>
    /// <param name="asset">中文含义：资源。读取边界提供的资源值快照。</param>
    /// <returns>最终非负权重；无对应武器的弹药候选为零。</returns>
    public static float CalculateWeight(DropEntry entry, DropTable table, int consecutiveNoDrops, int consecutiveSameCategory, ZoneConfig zoneConfig, DropResourceSnapshot asset)
    {
        if (entry == null || entry.item == null)
        {
            return 0f;
        }

        // 最终权重累乘值，起点为配置中的基础权重。
        // 中文含义：weight 为“权重”。
float weight = entry.baseWeight;
        // 武器过滤单独提前返回，避免无资格弹药继续访问后续配置。
        // 中文含义：weaponFactor 为“武器系数”。
float weaponFactor = CalculateWeaponFactor(entry, asset);
        if (weaponFactor <= 0f)
        {
            return 0f;
        }

        weight *= weaponFactor;
        weight *= CalculateResourceFactor(entry, table, asset);
        weight *= CalculateCraftingMaterialFactor(entry, asset);
        weight *= CalculateHealthFactor(entry, table, asset);
        if (entry.category == DropCategory.Herb && table != null)
        {
            weight *= Mathf.Clamp01(table.herbDropWeightMultiplier);
        }

        weight *= CalculateLuckFactor(table, consecutiveNoDrops);
        weight *= CalculateAreaFactor(entry.category, zoneConfig, asset);
        weight *= CalculateRepeatPenalty(table, consecutiveSameCategory);
        return Mathf.Max(0f, weight);
    }

    /// <summary>主计算入口首先调用，只允许已持有对应武器的弹药进入后续竞争。</summary>
    /// <param name="entry">中文含义：当前掉落入口。当前候选条目。</param>
    /// <param name="asset">中文含义：资源。包含武器持有数量的快照。</param>
    /// <returns>非弹药或持有对应武器时为一，否则为零。</returns>
    static float CalculateWeaponFactor(DropEntry entry, DropResourceSnapshot asset)
    {
        if (entry.category != DropCategory.Ammo)
        {
            return 1f;
        }

        if (asset.HasWeapon(entry.weaponType))
        {
            return 1f;
        }

        return 0f;
    }

    /// <summary>主计算入口的弹药数量因子，按配置顺序返回首个满足数量上限的层级，超出全部层级使用末项。</summary>
    /// <param name="entry">中文含义：当前掉落入口。弹药候选及对应武器类型。</param>
    /// <param name="table">中文含义：表。按数量递增配置的各武器阈值。</param>
    /// <param name="asset">中文含义：资源。包含备弹与武器模块可用性的快照。</param>
    /// <returns>选中的层级倍率；不适用或无层级时为一。</returns>
    static float CalculateResourceFactor(DropEntry entry, DropTable table, DropResourceSnapshot asset)
    {
        if (entry.category != DropCategory.Ammo)
        {
            return 1f;
        }

        if (!asset.HasWeaponModule)
        {
            return 1f;
        }

        // 此候选对应武器的备弹数量，只从快照读取。
        // 中文含义：currentAmmo 为“当前弹匣弹量”。
int currentAmmo = asset.ReserveAmmo(entry.weaponType);
        // 当前武器的阈值列表，未知武器沿用不适用的空列表语义。
        // 中文含义：tiers 为“品质级别集合”。
List<AmmoTier> tiers = null;
        if (entry.weaponType == WeaponType.Pistol)
        {
            tiers = table.pistolAmmoTiers;
        }
        else if (entry.weaponType == WeaponType.Rifle)
        {
            tiers = table.rifleAmmoTiers;
        }
        else if (entry.weaponType == WeaponType.Shotgun)
        {
            tiers = table.shotgunAmmoTiers;
        }

        if (tiers == null || tiers.Count == 0)
        {
            return 1f;
        }

        // 按资产现有顺序检查阈值，不在运行时重排或更改配置。
        // 中文含义：tier 为“品质级别”。每轮从品质级别集合取得一个元素，仅代表当前这一项。
foreach (var tier in tiers)
        {
            if (currentAmmo <= tier.maxAmmo)
            {
                return tier.multiplier;
            }
        }

        return tiers[tiers.Count - 1].multiplier;
    }

    /// <summary>主计算入口的材料数量因子，库存少时提高权重，数量多时按既有分段降低。</summary>
    /// <param name="entry">中文含义：当前掉落入口。候选材料条目。</param>
    /// <param name="asset">中文含义：资源。包含物品数量的快照。</param>
    /// <returns>对应库存段的倍率；非材料或无背包时为一。</returns>
    static float CalculateCraftingMaterialFactor(DropEntry entry, DropResourceSnapshot asset)
    {
        if (entry.category != DropCategory.CraftingMaterial || entry.item == null)
        {
            return 1f;
        }

        if (!asset.HasInventory)
        {
            return 1f;
        }

        // 当前材料所有堆叠的合计数量。
        // 中文含义：count 为“数量”。
int count = asset.Count(entry.item.itemId);
        if (count <= 2)
        {
            return 2.25f;
        }
        else if (count <= 6)
        {
            return 1.35f;
        }
        else if (count <= 12)
        {
            return 0.85f;
        }

        return 0.45f;
    }

    /// <summary>主计算入口的治疗品生命因子，按现有阈值顺序选择首个生命比例上限。</summary>
    /// <param name="entry">中文含义：当前掉落入口。当前候选，用类别选择草药或喷雾配置。</param>
    /// <param name="table">中文含义：表。治疗品生命阈值配置。</param>
    /// <param name="asset">中文含义：资源。生命模块可用性和生命比例快照。</param>
    /// <returns>命中层级倍率，超过全部阈值用末项，不适用时为一。</returns>
    static float CalculateHealthFactor(DropEntry entry, DropTable table, DropResourceSnapshot asset)
    {
        if (!asset.HasHealth)
        {
            return 1f;
        }

        // 已读取的玩家生命比例，不在规则内访问生命组件。
        // 中文含义：healthRatio 为“生命比例”。
float healthRatio = asset.HealthRatio;
        // 当前治疗品类别的阈值列表，其他类别不应用生命因子。
        // 中文含义：tiers 为“品质级别集合”。
List<HealthTier> tiers = null;
        if (entry.category == DropCategory.Herb)
        {
            tiers = table.herbHealthTiers;
        }
        else if (entry.category == DropCategory.FirstAid)
        {
            tiers = table.firstAidHealthTiers;
        }

        if (tiers == null || tiers.Count == 0)
        {
            return 1f;
        }

        // 当前生命阈值候选，按资产既有顺序查询。
        // 中文含义：tier 为“品质级别”。每轮从品质级别集合取得一个元素，仅代表当前这一项。
foreach (var tier in tiers)
        {
            if (healthRatio <= tier.maxHealthRatio)
            {
                return tier.multiplier;
            }
        }

        return tiers[tiers.Count - 1].multiplier;
    }

    /// <summary>主计算和金钱或总掉落资格链共用的幸运因子，达到连续未掉落阈值后线性增长并限制上限。</summary>
    /// <param name="table">中文含义：表。幸运阈值、增长量与上限配置。</param>
    /// <param name="consecutiveNoDrops">中文含义：连续无掉落物集合。连续未掉落次数。</param>
    /// <returns>尚未触发时为一，否则返回受上限限制的增长倍率。</returns>
    public static float CalculateLuckFactor(DropTable table, int consecutiveNoDrops)
    {
        if (table == null || consecutiveNoDrops < table.luckThreshold)
        {
            return 1f;
        }

        // 超过阈值的次数，阈值当次不额外增长。
        // 中文含义：misses 为“未命中次数”。
int misses = consecutiveNoDrops - table.luckThreshold;
        // 超阈次数对应的累计倍率增量。
        // 中文含义：bonus 为“额外加成”。
float bonus = misses * table.luckBonusPerMiss;
        return Mathf.Min(table.luckBonusCap, 1f + bonus);
    }

    /// <summary>主计算读取区域倍率的适配入口，实际分类查询交给 ZoneConfig。</summary>
    /// <param name="category">中文含义：类别。当前候选的掉落类别。</param>
    /// <param name="zoneConfig">中文含义：区配置。区域配置，允许为空。</param>
    /// <param name="asset">中文含义：资源。保留既有规则入口参数，当前区域倍率不使用资源快照。</param>
    /// <returns>配置中的类别倍率，无区域配置时为一。</returns>
    static float CalculateAreaFactor(DropCategory category, ZoneConfig zoneConfig, DropResourceSnapshot asset)
    {
        if (zoneConfig == null)
        {
            return 1f;
        }

        return zoneConfig.GetMultiplier(category);
    }

    /// <summary>主计算最后应用的重复衰减，达到类别连续出现阈值后指数降低，并限制到配置下限。</summary>
    /// <param name="table">中文含义：表。重复阈值、衰减倍率与下限配置。</param>
    /// <param name="consecutiveSameCategory">中文含义：连续相同类别。当前候选类别的连续出现次数。</param>
    /// <returns>未触发时为一，否则为受下限限制的衰减倍率。</returns>
    static float CalculateRepeatPenalty(DropTable table, int consecutiveSameCategory)
    {
        if (table == null || consecutiveSameCategory < table.repeatThreshold)
        {
            return 1f;
        }

        // 超过重复阈值的次数，阈值当次指数为零。
        // 中文含义：repeats 为“重复次数”。
int repeats = consecutiveSameCategory - table.repeatThreshold;
        // 按配置底数计算的原始衰减结果。
        // 中文含义：penalty 为“惩罚”。
float penalty = Mathf.Pow(table.repeatDecayFactor, repeats);
        return Mathf.Max(table.repeatDecayFloor, penalty);
    }
}
