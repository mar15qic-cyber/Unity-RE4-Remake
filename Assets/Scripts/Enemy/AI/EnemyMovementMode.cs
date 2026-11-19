/// <summary>敌人行为、动作与通行使用的 EnemyMovementMode 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum EnemyMovementMode
{
    /// <summary>中文含义：完全已停止。</summary>
    FullyStopped,
    /// <summary>中文含义：站立路径根运动。</summary>
    StandingPathRootMotion,
    /// <summary>中文含义：动作根运动。</summary>
    ActionRootMotion,
    /// <summary>中文含义：爬行代码驱动。</summary>
    CrawlCodeDriven,
    /// <summary>中文含义：通行代码驱动。</summary>
    TraversalCodeDriven,
    /// <summary>中文含义：完整根运动。</summary>
    FullRootMotion
}
