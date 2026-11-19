/// <summary>
/// 敌人当前可观察状态。
/// 状态值由 VillagerController 统一写入；行为设计器外部行为树只读取状态并选择行为意图。
/// 动画状态行为只报告动画事实，不直接切换可观察状态。
/// Dead 是终态；门洞、楼梯、翻窗和外平台属于受保护导航事务。
/// </summary>
public enum VillagerState
{
    /// <summary>中文含义：待机。</summary>
    Idle,
    /// <summary>中文含义：巡逻。</summary>
    Patrol,
    /// <summary>中文含义：追逐。</summary>
    Chase,
    /// <summary>中文含义：攻击。</summary>
    Attack,
    /// <summary>中文含义：硬直中。</summary>
    Staggered,
    /// <summary>中文含义：爬行。</summary>
    Crawl,
    /// <summary>中文含义：起身。</summary>
    StandUp,
    /// <summary>中文含义：死亡。</summary>
    Dead,
    /// <summary>中文含义：警戒。</summary>
    Alert,
    /// <summary>中文含义：爬行攻击。</summary>
    CrawlAttack
}
