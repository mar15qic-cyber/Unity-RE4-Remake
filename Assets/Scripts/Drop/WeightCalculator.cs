/// <summary>旧掉落计算的兼容入口，单条权重查询时读取资源快照并同步转发 DropWeightRules；不保存另一份掉落状态。</summary>
public static class WeightCalculator
{
    /// <summary>旧调用方计算单条权重时进入此处，读取一次上下文后交给规则执行。</summary>
    /// <param name="entry">中文含义：当前掉落入口。待计算的掉落条目。</param>
    /// <param name="table">中文含义：表。所有动态倍率与阈值配置。</param>
    /// <param name="consecutiveNoDrops">中文含义：连续无掉落物集合。连续未掉落次数。</param>
    /// <param name="consecutiveSameCategory">中文含义：连续相同类别。当前条目类别的连续重复次数。</param>
    /// <param name="zoneConfig">中文含义：区配置。当前区域倍率配置，允许为空。</param>
    /// <returns>规则计算的最终非负权重。</returns>
    public static float CalculateWeight(DropEntry entry, DropTable table, int consecutiveNoDrops, int consecutiveSameCategory, ZoneConfig zoneConfig)
    {
        return DropWeightRules.CalculateWeight(entry, table, consecutiveNoDrops, consecutiveSameCategory, zoneConfig, DropResourceReader.Read());
    }

    /// <summary>掉落宿主查询幸运倍率时直接转发，不需要读取玩家资源快照。</summary>
    /// <param name="table">中文含义：表。幸运阈值、增长和上限配置。</param>
    /// <param name="consecutiveNoDrops">中文含义：连续无掉落物集合。当前连续未掉落次数。</param>
    /// <returns>连续未掉落对应的幸运倍率。</returns>
    public static float CalculateLuckFactor(DropTable table, int consecutiveNoDrops)
    {
        return DropWeightRules.CalculateLuckFactor(table, consecutiveNoDrops);
    }
}
