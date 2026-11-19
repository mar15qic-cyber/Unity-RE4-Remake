using UnityEngine;
using System.Collections.Generic;

/// <summary>掉落宿主绑定或按既有资源路径加载的共享配置资产，提供概率、候选、阈值与区域倍率；本局连续未掉落和类别计数保存在 DropManager。</summary>
[CreateAssetMenu(fileName = "DropTable", menuName = "RE4/Drop Table")]
public class DropTable : ScriptableObject
{
    // ═══════════════════ 金钱掉落（独立摇奖） ═══════════════════
    /// <summary>中文含义：金钱物品数据（为空则不掉落金钱）。独立金钱随机成功后生成的物品定义，缺失时不进行金钱随机。</summary>
    [Header("金钱掉落（独立摇奖）")]
    [Tooltip("金钱物品数据（为空则不掉落金钱）")]
    public ItemData moneyItem;
    /// <summary>中文含义：金钱基础掉落概率 (0-1)，受幸运因子影响。金钱基础概率，宿主乘幸运因子并限制不超过一。</summary>
    [Tooltip("金钱基础掉落概率 (0-1)，受幸运因子影响")]
    [Range(0f, 1f)]
    public float moneyDropChance = 0.5f;
    /// <summary>中文含义：金钱最小数量。金钱成功后的整数数量下限。</summary>
    [Tooltip("金钱最小数量")]
    public int moneyMin = 100;
    /// <summary>中文含义：金钱最大数量。金钱成功后的整数数量上限，宿主保持包含边界。</summary>
    [Tooltip("金钱最大数量")]
    public int moneyMax = 500;
    // ═══════════════════ 物品掉落（加权随机） ═══════════════════
    /// <summary>中文含义：所有可掉落物品（不含金钱，金钱独立处理）。普通物品加权候选，按现有列表顺序构建和累计，金钱单独处理。</summary>
    [Header("物品掉落条目")]
    [Tooltip("所有可掉落物品（不含金钱，金钱独立处理）")]
    public List<DropEntry> entries = new List<DropEntry>();
    /// <summary>中文含义：不掉落权重（与所有物品竞争，受幸运因子反向影响）。普通流程中的不掉落基础权重，除以幸运因子后参与竞争。</summary>
    [Tooltip("不掉落权重（与所有物品竞争，受幸运因子反向影响）")]
    public float nothingWeight = 1f;
    /// <summary>中文含义：容器保底掉落不受此项影响。非保底物品流程进入候选随机前的基础总概率。</summary>
    [Tooltip("敌人普通物品掉落概率；容器保底掉落不受此项影响")]
    [Range(0f, 1f)]
    public float itemDropChance = 0.55f;
    // ═══════════════════ 弹药资源阈值 ═══════════════════
    /// <summary>中文含义：手枪弹药权重倍率（从低到高排列，弹药越少倍率越高）。手枪备弹数量阈值列表，按配置顺序选择首个覆盖数量的倍率。</summary>
    [Header("弹药资源阈值（缺什么掉什么）")]
    [Tooltip("手枪弹药权重倍率（从低到高排列，弹药越少倍率越高）")]
    public List<AmmoTier> pistolAmmoTiers = new List<AmmoTier>();
    /// <summary>中文含义：步枪弹药权重倍率（从低到高排列）。步枪备弹数量阈值列表。</summary>
    [Tooltip("步枪弹药权重倍率（从低到高排列）")]
    public List<AmmoTier> rifleAmmoTiers = new List<AmmoTier>();
    /// <summary>中文含义：霰弹枪弹药权重倍率（从低到高排列）。霰弹枪备弹数量阈值列表。</summary>
    [Tooltip("霰弹枪弹药权重倍率（从低到高排列）")]
    public List<AmmoTier> shotgunAmmoTiers = new List<AmmoTier>();
    // ═══════════════════ 血量阈值 ═══════════════════
    /// <summary>中文含义：草药权重倍率（从低到高排列，血量越低倍率越高）。草药生命比例阈值列表。</summary>
    [Header("血量阈值（治疗品）")]
    [Tooltip("草药权重倍率（从低到高排列，血量越低倍率越高）")]
    public List<HealthTier> herbHealthTiers = new List<HealthTier>();
    /// <summary>中文含义：草药全局权重倍率，用于压低连续出现草药的概率。草药候选额外全局权重倍率，规则限制到零至一后乘入。</summary>
    [Tooltip("草药全局权重倍率，用于压低连续出现草药的概率")]
    [Range(0f, 1f)]
    public float herbDropWeightMultiplier = 0.45f;
    /// <summary>中文含义：急救喷雾权重倍率（从低到高排列）。急救喷雾生命比例阈值列表。</summary>
    [Tooltip("急救喷雾权重倍率（从低到高排列）")]
    public List<HealthTier> firstAidHealthTiers = new List<HealthTier>();
    // ═══════════════════ 幸运保护 ═══════════════════
    /// <summary>中文含义：连续不掉落多少次后开始提高掉率。开始幸运增长的连续未掉落阈值，恰好达到时增量为零。</summary>
    [Header("连续幸运保护")]
    [Tooltip("连续不掉落多少次后开始提高掉率")]
    public int luckThreshold = 3;
    /// <summary>中文含义：每次额外不掉落的全局权重倍率增量。超过幸运阈值后每次额外未掉落增加的倍率。</summary>
    [Tooltip("每次额外不掉落的全局权重倍率增量")]
    public float luckBonusPerMiss = 0.15f;
    /// <summary>中文含义：幸运权重倍率上限。幸运增长的倍率上限。</summary>
    [Tooltip("幸运权重倍率上限")]
    public float luckBonusCap = 2f;
    // ═══════════════════ 重复衰减 ═══════════════════
    /// <summary>中文含义：连续掉落同一类别多少次后开始衰减。同类别开始重复衰减的连续次数阈值。</summary>
    [Header("重复掉落衰减")]
    [Tooltip("连续掉落同一类别多少次后开始衰减")]
    public int repeatThreshold = 2;
    /// <summary>中文含义：每次额外重复掉落的衰减倍率（指数衰减）。每次超过重复阈值后的指数衰减底数。</summary>
    [Tooltip("每次额外重复掉落的衰减倍率（指数衰减）")]
    [Range(0f, 1f)]
    public float repeatDecayFactor = 0.5f;
    /// <summary>中文含义：衰减下限倍率（避免权重降到 0）。重复衰减的最低倍率，避免无限逼近零。</summary>
    [Tooltip("衰减下限倍率（避免权重降到 0）")]
    public float repeatDecayFloor = 0.1f;
    // ═══════════════════ 区域配置 ═══════════════════
    /// <summary>中文含义：不同区域各类别掉落的权重倍率。区域配置列表，GetZoneConfig 使用首个标识匹配项。</summary>
    [Header("区域配置")]
    [Tooltip("不同区域各类别掉落的权重倍率")]
    public List<ZoneConfig> zoneConfigs = new List<ZoneConfig>();
    /// <summary>掉落宿主每轮物品随机前同步查询，按配置列表顺序返回首个区域匹配项，缺失时返回 null。</summary>
    /// <param name="zone">中文含义：区。本轮使用的区域标识。</param>
    public ZoneConfig GetZoneConfig(DropZone zone)
    {
        // 当前区域候选配置，标识匹配时直接返回。
        // 中文含义：zc 为“资源类别”。每轮从区配置集合取得一个元素，仅代表当前这一项。
foreach (var zc in zoneConfigs)
        {
            if (zc.zone == zone)
            {
                return zc;
            }
        }

        return null;
    }
}
