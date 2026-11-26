using UnityEngine;

/// <summary>本局服务与流程使用的共享配置资产 ChapterWaveConfiguration；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(menuName = "游戏配置/章节波次配置")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "章节波次配置")]
public sealed class ChapterWaveConfiguration : ScriptableObject
{
    /// <summary>村民敌人目标。</summary>
    [Header("任务目标")]
    [Min(1)]
    [SerializeField]
    private int ganadoTarget = 15;
    /// <summary>电锯敌人目标。</summary>
    [Min(1)]
    [SerializeField]
    private int sawmanTarget = 2;
    /// <summary>生存持续时间。</summary>
    [Min(1f)]
    [SerializeField]
    private float survivalDuration = 360f;
    [Min(1)]
    [SerializeField, Tooltip("入村战斗达到此总击杀数时鸣钟，普通敌人与电锯敌人合计。")]
    private int bellKillTarget = 15;
    [Min(1f)]
    [SerializeField, Tooltip("入村战斗达到此生存秒数时鸣钟。")]
    private float bellSurvivalDuration = 600f;

    [Header("村庄巡逻和增援")]
    [SerializeField, Min(1), Tooltip("每累计击杀这么多名敌人，就获得等量增援。")]
    private int reinforcementSize = 4;
    [SerializeField, Tooltip("开场村民的世界坐标，每个位置对应一名村民。")]
    private Vector3[] villagePatrolPositions;
    [SerializeField, Tooltip("房屋内或墙后可出来追击的位置。检查净空、导航、玩家所在区域和镜头可见性后才会使用。")]
    private ReinforcementSite[] villageReinforcementSites;

    [System.Serializable]
    internal struct ReinforcementSite
    {
        public Vector3 position;
        public Bounds excludedPlayerArea;
    }
    /// <summary>首个波次村民敌人。</summary>
    [Header("固定三波")]
    [Min(1)]
    [SerializeField]
    private int firstWaveGanado = 7;
    /// <summary>首个波次电锯敌人。</summary>
    [Min(0)]
    [SerializeField]
    private int firstWaveSawman = 0;
    /// <summary>第二波次村民敌人。</summary>
    [Min(0)]
    [SerializeField]
    private int secondWaveGanado = 5;
    /// <summary>第二波次电锯敌人。</summary>
    [Min(0)]
    [SerializeField]
    private int secondWaveSawman = 1;
    /// <summary>第三波次村民敌人。</summary>
    [Min(0)]
    [SerializeField]
    private int thirdWaveGanado = 5;
    /// <summary>第三波次电锯敌人。</summary>
    [Min(0)]
    [SerializeField]
    private int thirdWaveSawman = 1;
    /// <summary>下一次波次剩余比例。</summary>
    [Range(0.05f, 0.95f)]
    [SerializeField]
    private float nextWaveRemainingRatio = 0.3f;
    /// <summary>第二波次延迟。</summary>
    [Min(0f)]
    [SerializeField]
    private float secondWaveDelay = 2f;
    /// <summary>波次生成间隔。</summary>
    [Min(0f)]
    [SerializeField]
    private float waveSpawnInterval = 0.12f;
    /// <summary>生成重试延迟。</summary>
    [Min(0.1f)]
    [SerializeField]
    private float spawnRetryDelay = 1f;
    /// <summary>常规击杀目标。</summary>
    internal int NormalKillTarget => ganadoTarget;
    /// <summary>电锯击杀目标。</summary>
    internal int ChainsawKillTarget => sawmanTarget;
    /// <summary>生存秒。</summary>
    internal float SurvivalSeconds => survivalDuration;
    internal int BellKillTarget => bellKillTarget;
    internal float BellSurvivalSeconds => bellSurvivalDuration;
    internal int ReinforcementSize => reinforcementSize;

    [SerializeField, Min(1f), Tooltip("农场门村庄侧的电锯人触发距离。")]
    private float farmEntranceRadius = 6f;
    [SerializeField, Min(1), Tooltip("普通村民击杀达到该数值时触发农场登场。")]
    private int farmEntranceKills = 6;
    [SerializeField, Min(1f), Tooltip("战斗计时达到该秒数时触发农场登场。")]
    private float farmEntranceSeconds = 120f;
    [SerializeField, Min(1f), Tooltip("本章登场的电锯人最大生命值。")]
    private float entranceSawmanHealth = 360f;

    internal float FarmEntranceRadius => farmEntranceRadius;
    internal int FarmEntranceKills => farmEntranceKills;
    internal float FarmEntranceSeconds => farmEntranceSeconds;
    internal float EntranceSawmanHealth => entranceSawmanHealth;
    internal Vector3[] VillagePatrolPositions => villagePatrolPositions;
    internal ReinforcementSite[] VillageReinforcementSites => villageReinforcementSites;

    internal bool ValidateVillage()
    {
        return bellKillTarget >= 1 && bellSurvivalDuration >= 1f && !float.IsInfinity(bellSurvivalDuration) &&
            villagePatrolPositions != null && reinforcementSize > 0 && villagePatrolPositions.Length >= reinforcementSize &&
            villageReinforcementSites != null && villageReinforcementSites.Length > 0 &&
            farmEntranceKills > 0 && farmEntranceRadius >= 1f && float.IsFinite(farmEntranceRadius) &&
            farmEntranceSeconds >= 1f && float.IsFinite(farmEntranceSeconds) &&
            entranceSawmanHealth >= 1f && float.IsFinite(entranceSawmanHealth);
    }
    /// <summary>首个波次常规数量。</summary>
    internal int FirstWaveNormalCount => firstWaveGanado;
    /// <summary>首个波次电锯数量。</summary>
    internal int FirstWaveChainsawCount => firstWaveSawman;
    /// <summary>序数第二波次常规数量。</summary>
    internal int OrdinalTwoWaveNormalCount => secondWaveGanado;
    /// <summary>序数第二波次电锯数量。</summary>
    internal int OrdinalTwoWaveChainsawCount => secondWaveSawman;
    /// <summary>序数第三波次常规数量。</summary>
    internal int OrdinalThreeWaveNormalCount => thirdWaveGanado;
    /// <summary>序数第三波次电锯数量。</summary>
    internal int OrdinalThreeWaveChainsawCount => thirdWaveSawman;
    /// <summary>推进剩余比例。</summary>
    internal float AdvanceRemainingRatio => nextWaveRemainingRatio;
    /// <summary>后续波次延迟。</summary>
    internal float SubsequentWaveDelay => secondWaveDelay;
    /// <summary>生成间隔。</summary>
    internal float GenerateInterval => waveSpawnInterval;
    /// <summary>重试间隔。</summary>
    internal float RetryInterval => spawnRetryDelay;

    /// <summary>Validate 的业务处理入口；本方法直接依赖 float.IsInfinity(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.ConfigurationError()；下游（含内部回调体） float.IsInfinity(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="field">字段。输出参数，由本入口写入并交给调用方。</param>
    public bool Validate(out string field)
    {
        field = null;
        if (ganadoTarget < 1 || ganadoTarget > firstWaveGanado + secondWaveGanado + thirdWaveGanado)
        {
            field = nameof(ganadoTarget);
        }
        else if (sawmanTarget < 1 || sawmanTarget > firstWaveSawman + secondWaveSawman + thirdWaveSawman)
        {
            field = nameof(sawmanTarget);
        }
        else if (firstWaveGanado < 1 || firstWaveSawman < 0 || secondWaveGanado < 0 || secondWaveSawman < 0 || thirdWaveGanado < 0 || thirdWaveSawman < 0)
        {
            field = "三波生成数量";
        }
        else if (!(survivalDuration >= 1f) || float.IsInfinity(survivalDuration))
        {
            field = nameof(survivalDuration);
        }
        else if (!(nextWaveRemainingRatio >= .05f && nextWaveRemainingRatio <= .95f))
        {
            field = nameof(nextWaveRemainingRatio);
        }
        else if (!(secondWaveDelay >= 0f && waveSpawnInterval >= 0f && spawnRetryDelay >= .1f) || float.IsInfinity(secondWaveDelay) || float.IsInfinity(waveSpawnInterval) || float.IsInfinity(spawnRetryDelay))
        {
            field = "生成间隔";
        }

        return field == null;
    }
}
