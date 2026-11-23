using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第一章精简任务管理器。
/// 统一负责有限敌人储备、补位、计时、进度和通关判定。
/// </summary>
/// <remarks>章节任务宿主，持有 MissionRulesModel 并调用 MissionSpawnExecutor 执行生成；目标来自当前波次配置，终局通过 GameManager 提交唯一结果。</remarks>
public partial class ChapterOneMissionManager : Singleton<ChapterOneMissionManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：村民敌人预制件集合。使用位置：ChapterOneMissionManager.ValidateConfiguration()、MissionSpawnExecutor.GetNextGanadoPrefab()、SceneStartupEntry.ConfigurationError()。</summary>
    [Header("普通敌人预制件")]
    public GameObject[] ganadoPrefabs;
    /// <summary>中文含义：电锯敌人模板。绑定主场景层级中停用的 SawMan 对象，任务运行时从该对象克隆实例。使用位置：作为 ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType) 的参数、作为 SceneStartupEntry.ValidateEnemyTemplate(GameObject, bool) 的参数、ChapterOneMissionManager.ValidateConfiguration()。</summary>
    [Header("电锯敌人模板")]
    [Tooltip("绑定主场景层级中停用的 SawMan 对象，任务运行时从该对象克隆实例。")]
    public GameObject sawmanTemplate;
    /// <summary>中文含义：生成点集合。使用位置：作为 Random.Range(int, int) 的参数、ChapterOneMissionManager.ValidateConfiguration()、MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion) 等共 4 处。</summary>
    [Header("刷新点")]
    public Transform[] spawnPoints;
    /// <summary>中文含义：生成吸附距离。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</summary>
    [Min(0.1f)]
    public float spawnSnapDistance = 3f;
    /// <summary>中文含义：生成吸附高度。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</summary>
    [Min(0.1f)]
    public float spawnSnapHeight = 0.45f;
    /// <summary>中文含义：最小玩家生成距离。使用位置：MissionSpawnExecutor.TryGetValidSpawn(GameObject, out Vector3, out Quaternion)。</summary>
    [Min(0f)]
    public float minimumPlayerSpawnDistance = 10f;
    /// <summary>中文含义：占用的生成半径。使用位置：MissionSpawnExecutor.IsSpawnOccupied(Vector3)。</summary>
    [Min(0.1f)]
    public float occupiedSpawnRadius = 2f;
    /// <summary>中文含义：预制件带胶囊碰撞体时会自动采用其真实尺寸。出生点检查使用的默认角色半径；预制件带胶囊碰撞体时会自动采用其真实尺寸。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("出生实体净空")]
    [Tooltip("出生点检查使用的默认角色半径；预制件带胶囊碰撞体时会自动采用其真实尺寸。")]
    [Min(0.1f)]
    public float defaultSpawnCapsuleRadius = 0.35f;
    /// <summary>中文含义：预制件带胶囊碰撞体时会自动采用其真实尺寸。出生点检查使用的默认角色高度；预制件带胶囊碰撞体时会自动采用其真实尺寸。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("出生点检查使用的默认角色高度；预制件带胶囊碰撞体时会自动采用其真实尺寸。")]
    [Min(0.2f)]
    public float defaultSpawnCapsuleHeight = 1.7f;
    /// <summary>中文含义：胶囊从地面抬起的距离，避免把正常地面接触误判成阻挡。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("胶囊从地面抬起的距离，避免把正常地面接触误判成阻挡。")]
    [Min(0.005f)]
    public float spawnClearanceSkin = 0.05f;
    /// <summary>中文含义：待搜索集合生成排除规则中心。谷堆中心附近不允许生成敌人，避免开局就把敌人放入高风险绕行区域。使用位置：MissionSpawnExecutor.IsInsideHaystackSpawnExclusion(Vector3)。</summary>
    [Header("谷堆出生排除")]
    [Tooltip("谷堆中心附近不允许生成敌人，避免开局就把敌人放入高风险绕行区域。")]
    public Vector3 haystackSpawnExclusionCenter = new Vector3(2.4f, 0f, 14f);
    /// <summary>中文含义：待搜索集合生成排除规则半径。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.1f)]
    public float haystackSpawnExclusionRadius = 4.5f;
    /// <summary>中文含义：波次配置。使用位置：ChapterOneMissionManager.firstWaveGanado.get、ChapterOneMissionManager.firstWaveSawman.get、ChapterOneMissionManager.ganadoTarget.get 等共 14 处。</summary>
    [Header("章节规则")]
    [UnityEngine.Serialization.FormerlySerializedAs("波次配置")]
    [UnityEngine.InspectorName("波次配置")]
    public ChapterWaveConfiguration waveConfiguration;
    /// <summary>中文含义：村民敌人目标。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、作为 ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int) 的参数、作为 MissionRulesModel.Initialize(int, int, float, float) 的参数。</summary>
    public int ganadoTarget => waveConfiguration.NormalKillTarget;
    /// <summary>中文含义：电锯敌人目标。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、作为 ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int) 的参数、作为 MissionRulesModel.Initialize(int, int, float, float) 的参数。</summary>
    public int sawmanTarget => waveConfiguration.ChainsawKillTarget;
    /// <summary>中文含义：生存持续时间。使用位置：作为 ChapterOneMissionManager.MeetsSurvivalRequirement(float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 MissionRulesModel.Initialize(int, int, float, float) 的参数。</summary>
    public float survivalDuration
    {
        get
        {
            if (bellSequence != null)
            {
                return waveConfiguration.BellSurvivalSeconds;
            }

            return waveConfiguration.SurvivalSeconds;
        }
    }
    /// <summary>中文含义：首个波次村民敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredGanadoReserve.get。</summary>
    public int firstWaveGanado => waveConfiguration.FirstWaveNormalCount;
    /// <summary>中文含义：首个波次电锯敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredSawmanReserve.get。</summary>
    public int firstWaveSawman => waveConfiguration.FirstWaveChainsawCount;
    /// <summary>中文含义：第二波次村民敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredGanadoReserve.get。</summary>
    public int secondWaveGanado => waveConfiguration.OrdinalTwoWaveNormalCount;
    /// <summary>中文含义：第二波次电锯敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredSawmanReserve.get。</summary>
    public int secondWaveSawman => waveConfiguration.OrdinalTwoWaveChainsawCount;
    /// <summary>中文含义：第三波次村民敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredGanadoReserve.get。</summary>
    public int thirdWaveGanado => waveConfiguration.OrdinalThreeWaveNormalCount;
    /// <summary>中文含义：第三波次电锯敌人。使用位置：作为 ChapterOneMissionManager.SpawnFixedWave(int, int) 的参数、ChapterOneMissionManager.ConfiguredSawmanReserve.get。</summary>
    public int thirdWaveSawman => waveConfiguration.OrdinalThreeWaveChainsawCount;
    /// <summary>中文含义：下一次波次剩余比例。使用位置：作为 MissionRulesModel.Initialize(int, int, float, float) 的参数。</summary>
    public float nextWaveRemainingRatio => waveConfiguration.AdvanceRemainingRatio;
    /// <summary>中文含义：第二波次延迟。使用位置：作为 WaitForSeconds.WaitForSeconds(float) 的参数、ChapterOneMissionManager.StartNextWave()。</summary>
    public float secondWaveDelay => waveConfiguration.SubsequentWaveDelay;
    /// <summary>中文含义：波次生成间隔。使用位置：作为 WaitForSeconds.WaitForSeconds(float) 的参数、ChapterOneMissionManager.SpawnFixedWave(int, int)。</summary>
    public float waveSpawnInterval => waveConfiguration.GenerateInterval;
    /// <summary>中文含义：生成重试延迟。使用位置：作为 WaitForSeconds.WaitForSeconds(float) 的参数。</summary>
    public float spawnRetryDelay => waveConfiguration.RetryInterval;

    /// <summary>中文含义：是否日志任务事件集合。使用位置：ChapterOneMissionManager.LogMission(string)。</summary>
    [Header("诊断")]
    public bool logMissionEvents;
    /// <summary>中文含义：村民敌人击杀数。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、作为 ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int) 的参数。</summary>
    public int GanadoKills => Rules.NormalKill;
    /// <summary>中文含义：电锯敌人击杀数。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、作为 ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int) 的参数。</summary>
    public int SawmanKills => Rules.ChainsawKill;
    /// <summary>中文含义：已用玩法秒。使用位置：作为 ChapterOneMissionManager.MeetsSurvivalRequirement(float, float) 的参数、作为 Mathf.Max(float, float) 的参数。</summary>
    public float ElapsedGameplaySeconds => Rules.UsedTime;
    /// <summary>中文含义：是否运行中。使用位置：ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.Update()。</summary>
    public bool IsRunning => Rules.RuntimeMiddle;
    /// <summary>中文含义：是否已完成。使用位置：ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.Update()。</summary>
    public bool IsCompleted => Rules.Completed;
    /// <summary>中文含义：最新快照。使用位置：作为 MissionProgressHUD.UpdateView(MissionProgressSnapshot) 的参数、GameOverScreen.UpdateResultText()、PauseMenu.ShowResults() 等共 4 处。</summary>
    public MissionProgressSnapshot LatestSnapshot => BuildSnapshot();

    /// <summary>中文含义：进度变化。事件通信成员 ProgressChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：ChapterOneMissionManager.PublishProgress(bool)、MissionProgressHUD.ConnectPresentation()、MissionProgressHUD.Unbind()。</summary>
    public event Action<MissionProgressSnapshot> ProgressChanged;
    /// <summary>中文含义：跟踪的敌人集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 的参数、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.RemoveUnmanagedRuntimeEnemies() 等共 5 处。</summary>
    private readonly Dictionary<VillagerController, MissionEnemyType> trackedEnemies = new();
    /// <summary>中文含义：玩家。使用位置：作为 MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 的参数、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)。</summary>
    private LeonController player;
    /// <summary>中文含义：已生成村民敌人。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数。</summary>
    private int spawnedGanado => Rules.NormalHasGenerate;
    /// <summary>中文含义：已生成电锯敌人。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数。</summary>
    private int spawnedSawman => Rules.ChainsawHasGenerate;

    /// <summary>中文含义：上一次已显示剩余第二。使用位置：ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()。</summary>
    private int lastDisplayedRemainingSecond = -1;
    /// <summary>中文含义：启动协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OpenReleaseMission() 等共 5 处。</summary>
    private Coroutine startupRoutine;
    /// <summary>中文含义：波次协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController) 等共 5 处。</summary>
    private Coroutine waveRoutine;
    /// <summary>中文含义：当前波次。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、作为 EnemyCombatTimeline.Register(VillagerController, int) 的参数、ChapterOneMissionManager.StartNextWave()。</summary>
    private int currentWave => Rules.Wave;
    /// <summary>中文含义：规则。使用位置：ChapterOneMissionManager.BuildSnapshot(bool)、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.currentWave.get 等共 18 处。</summary>
    public MissionRulesModel Rules { get; } = new MissionRulesModel();

    /// <summary>中文含义：生成。使用位置：ChapterOneMissionManager.GetNextGanadoPrefab()、ChapterOneMissionManager.HasWalkableNavigationNodes()、ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry) 等共 4 处。</summary>
    private MissionSpawnExecutor generate;
    /// <summary>中文含义：绑定对局。使用位置：ChapterOneMissionManager.CanAdvanceGameplay()、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnDestroy() 等共 4 处。</summary>
    private GameManager bindingMatch;
    private VillageBellSequence bellSequence;
    private bool villagePatrolReady;
    private float nextReinforcementTime = -1f;
    public bool IsBellEnding { get; private set; }
    internal bool UsesBellEnding => bellSequence != null;
    internal int BellKillTarget => waveConfiguration.BellKillTarget;
    /// <summary>HasWalkableNavigationNodes 的业务处理入口；本方法直接依赖 MissionSpawnExecutor.HasWalkableNavigationNodes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.ValidateConfiguration()；下游（含内部回调体） MissionSpawnExecutor.HasWalkableNavigationNodes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool HasWalkableNavigationNodes() => generate.HasWalkableNavigationNodes();
    /// <summary>GetNextGanadoPrefab 的业务处理入口；本方法直接依赖 MissionSpawnExecutor.GetNextGanadoPrefab()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.SpawnFixedWave(int, int)；下游（含内部回调体） MissionSpawnExecutor.GetNextGanadoPrefab()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    GameObject GetNextGanadoPrefab() => generate.GetNextGanadoPrefab();
    /// <summary>Prepare 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.ValidateConfiguration()、MissionRulesModel.Initialize(int, int, float, float)、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） ChapterOneMissionManager.ValidateConfiguration()、MissionRulesModel.Initialize(int, int, float, float)、MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="match">中文含义：对局。使用位置：ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)。</param>
    /// <param name="enemyGroup">中文含义：敌人分组。使用位置：作为 MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 的参数、ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)。</param>
    /// <param name="traversal">中文含义：通行。使用位置：作为 MissionSpawnExecutor.MissionSpawnExecutor(ChapterOneMissionManager, LeonController, HouseThreeTraversalGeometry, EnemyNavigationPreciseLink[], Dictionary&lt;VillagerController, MissionEnemyType&gt;) 的参数。</param>
    internal bool Prepare(GameManager match, EnemyAIManager enemyGroup, HouseThreeTraversalGeometry traversal)
    {
        bindingMatch = match;
        player = match.player;
        currentSessionEnemyGroup = enemyGroup;
        generate = new MissionSpawnExecutor(this, player, traversal, enemyGroup.preciseTraversal, trackedEnemies);
        if (!ValidateConfiguration())
        {
            return false;
        }

        if (gameObject.scene.path == VillageBellSequence.VillageScenePath)
        {
            if (!waveConfiguration.ValidateVillage() || siegeSequence == null || !siegeSequence.HasSceneBindings)
            {
                Debug.LogError("第一章波次资产或村庄围攻场景配置无效。", this);
                return false;
            }
            bellSequence = VillageBellSequence.Load(transform);
            if (bellSequence == null)
            {
                return false;
            }
        }

        bindingMatch.OnPlayerDied += StopExecute;
        if (UsesBellEnding)
        {
            generate.PrepareEntranceActors(sawmanTemplate);
            Rules.InitializeVillage(BellKillTarget, survivalDuration, waveConfiguration.VillagePatrolPositions.Length,
                waveConfiguration.ReinforcementSize);
        }
        else
        {
            Rules.Initialize(ganadoTarget, sawmanTarget, survivalDuration, nextWaveRemainingRatio);
        }
        return true;
    }

    /// <summary>中文含义：当前会话敌人分组。使用位置：ChapterOneMissionManager.CanAdvanceGameplay()、ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)。</summary>
    private EnemyAIManager currentSessionEnemyGroup;
    internal bool TracksVillageEnemy(VillagerController enemy)
    {
        return trackedEnemies.ContainsKey(enemy);
    }

    private float nextQuietPatrolTime = -1f;
    /// <summary>OpenReleaseMission 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool OpenReleaseMission()
    {
        if (!UsesBellEnding)
        {
            startupRoutine = StartCoroutine(StartMissionWhenInitialEnemiesReady());
            return true;
        }

        RemoveUnmanagedRuntimeEnemies();
        Rules.BeginFirstWave();
        foreach (Vector3 anchor in waveConfiguration.VillagePatrolPositions)
        {
            VillagerController enemy = generate.GeneratePatrol(GetNextGanadoPrefab(), anchor);
            if (!RegisterSpawn(enemy, MissionEnemyType.Ganado))
            {
                Debug.LogError($"[第一章任务] 开场巡逻点 {anchor} 找不到可用的导航和站立空间。", this);
                foreach (VillagerController spawnedEnemy in trackedEnemies.Keys)
                {
                    spawnedEnemy.gameObject.SetActive(false);
                    Destroy(spawnedEnemy.gameObject);
                }

                UnsubscribeAllEnemies();
                return false;
            }
        }

        villagePatrolReady = true;
        LogMission("开场村民已就位，等待村民发现玩家。");
        return true;
    }
    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChapterOneMissionManager.StopExecute()、ChapterOneMissionManager.UnsubscribeAllEnemies()、Singleton&lt;ChapterOneMissionManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        if (bindingMatch != null)
        {
            bindingMatch.OnPlayerDied -= StopExecute;
        }

        StopExecute();
        UnsubscribeAllEnemies();
        base.OnDestroy();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChapterOneMissionManager.StopExecute()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable() => StopExecute();
    /// <summary>StopExecute 的业务处理入口；本方法直接依赖 MissionRulesModel.Stop()、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnDestroy()、ChapterOneMissionManager.OnDisable()、ChapterOneMissionManager.Update()、SceneStartupEntry.OnDisable()；下游（含内部回调体） MissionRulesModel.Stop()、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void StopExecute()
    {
        Rules.Stop();
        if (siegeSequence != null)
        {
            siegeSequence.Cancel();
        }
        villagePatrolReady = false;
        nextQuietPatrolTime = -1f;
        nextReinforcementTime = -1f;
        if (startupRoutine != null)
        {
            StopCoroutine(startupRoutine);
        }

        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
        }

        startupRoutine = waveRoutine = null;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChapterOneMissionManager.CanAdvanceGameplay()、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.MeetsSurvivalRequirement(float, float)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.StopExecute()、MissionRulesModel.AdvanceTime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
#if DEVELOPMENT_BUILD
        if (bellSequence != null && !IsBellEnding && VillageBellSequence.TryClaimPreview(gameObject.scene.handle))
        {
            CompleteMission(MissionCompletionReason.Survival);
            return;
        }
#endif
        bool enteredHouse = ObserveVillageApproach(out bool approachedFarm);
        StartVillageCombatIfReady();
        if (!IsRunning || IsCompleted)
        {
            UpdateQuietPatrol();
            return;
        }

        if (!IsPlayerAlive())
        {
            StopExecute();
            PublishProgress(true);
            return;
        }

        if (!CanAdvanceGameplay())
        {
            return;
        }

        Rules.AdvanceTime(Time.deltaTime);
        PublishProgress(false);
        if (Rules.ReachSurvival)
        {
            CompleteMission(MissionCompletionReason.Survival);
            return;
        }

        if (UsesBellEnding)
        {
            Rules.ObserveVillageEntrances(enteredHouse, approachedFarm, waveConfiguration.FarmEntranceKills,
                waveConfiguration.FarmEntranceSeconds);
            if (TryBeginVillageEntrance())
            {
                return;
            }

            UpdateVillageReinforcements();
        }
    }

    private void StartVillageCombatIfReady()
    {
        if (!villagePatrolReady || !currentSessionEnemyGroup.IsCombatUnlocked || !IsPlayerAlive())
        {
            return;
        }

        villagePatrolReady = false;
        nextQuietPatrolTime = -1f;
        Rules.CompleteCurrentWaveGenerate(true);
        PublishProgress(true);
        LogMission("村民发现玩家，开始战斗和生存计时。");
        VillageLeonDialogue.Report(player, VillageSpeechCue.CombatStart);
    }

    private void UpdateQuietPatrol()
    {
        if (!villagePatrolReady || currentSessionEnemyGroup == null || !currentSessionEnemyGroup.AwaitsVillageDiscovery
            || !IsPlayerAlive() || bindingMatch == null || bindingMatch.IsTerminal
            || !bindingMatch.Permission.Current.AllowMissionTimer || Time.timeScale <= 0f || Rules.AliveCount > 0)
        {
            return;
        }
        if (nextQuietPatrolTime < 0f)
        {
            nextQuietPatrolTime = Time.time + 8f;
        }
        if (Time.time < nextQuietPatrolTime)
        {
            return;
        }
        if (RegisterSpawn(generate.GenerateReinforcement(GetNextGanadoPrefab()), MissionEnemyType.Ganado))
        {
            nextQuietPatrolTime = -1f;
        }
        else
        {
            nextQuietPatrolTime = Time.time + spawnRetryDelay;
        }
    }

    private void UpdateVillageReinforcements()
    {
        bool needsMinimumCombatants = Rules.AliveCount < 4 && Rules.TotalKills < waveConfiguration.ReinforcementSize;
        if (Rules.PendingReinforcements == 0 && !needsMinimumCombatants)
        {
            nextReinforcementTime = -1f;
            return;
        }

        if (nextReinforcementTime < 0f)
        {
            nextReinforcementTime = Time.time + secondWaveDelay;
        }

        if (Time.time < nextReinforcementTime)
        {
            return;
        }

        if (RegisterSpawn(generate.GenerateReinforcement(GetNextGanadoPrefab()), MissionEnemyType.Ganado, !needsMinimumCombatants))
        {
            nextReinforcementTime = Time.time + waveSpawnInterval;
        }
        else
        {
            nextReinforcementTime = Time.time + spawnRetryDelay;
        }
    }

    /// <summary>StartMissionWhenInitialEnemiesReady 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.RemoveUnmanagedRuntimeEnemies()、ChapterOneMissionManager.SpawnFixedWave(int, int)、MissionRulesModel.BeginFirstWave()、MissionRulesModel.CompleteCurrentWaveGenerate(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OpenReleaseMission()；下游（含内部回调体） ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.RemoveUnmanagedRuntimeEnemies()、ChapterOneMissionManager.SpawnFixedWave(int, int)、MissionRulesModel.BeginFirstWave()、MissionRulesModel.CompleteCurrentWaveGenerate(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator StartMissionWhenInitialEnemiesReady()
    {
        Rules.BeginFirstWave();
        yield return SpawnFixedWave(firstWaveGanado, firstWaveSawman);
        Rules.CompleteCurrentWaveGenerate(false);
        RemoveUnmanagedRuntimeEnemies();
        startupRoutine = null;
        Rules.CompleteCurrentWaveGenerate(true);
        lastDisplayedRemainingSecond = -1;
        PublishProgress(true);
        LogMission("首批敌人已就位，第一章任务开始。");
    }

    /// <summary>SpawnFixedWave 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.GetNextGanadoPrefab()、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()、ChapterOneMissionManager.StartNextWave()；下游（含内部回调体） ChapterOneMissionManager.GetNextGanadoPrefab()、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ganadoCount">中文含义：村民敌人数量。使用位置：ChapterOneMissionManager.SpawnFixedWave(int, int)。</param>
    /// <param name="sawmanCount">中文含义：电锯敌人数量。使用位置：ChapterOneMissionManager.SpawnFixedWave(int, int)。</param>
    IEnumerator SpawnFixedWave(int ganadoCount, int sawmanCount)
    {
        // index（当前元素索引）：使用位置：ChapterOneMissionManager.SpawnFixedWave(int, int)。
for (int index = 0; index < sawmanCount; index++)
        {
            while (!TrySpawnEnemy(sawmanTemplate, MissionEnemyType.Sawman))
            {
                yield return new WaitForSeconds(spawnRetryDelay);
            }

            if (waveSpawnInterval > 0f)
            {
                yield return new WaitForSeconds(waveSpawnInterval);
            }
        }

        // index（当前元素索引）：使用位置：ChapterOneMissionManager.SpawnFixedWave(int, int)。
for (int index = 0; index < ganadoCount; index++)
        {
            // prefab（预制件）：保存 ChapterOneMissionManager.GetNextGanadoPrefab() 的返回结果，供当前步骤使用。使用位置：作为 ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType) 的参数、ChapterOneMissionManager.SpawnFixedWave(int, int)。
GameObject prefab = GetNextGanadoPrefab();
            while (!TrySpawnEnemy(prefab, MissionEnemyType.Ganado))
            {
                yield return new WaitForSeconds(spawnRetryDelay);
                prefab = GetNextGanadoPrefab();
            }

            if (waveSpawnInterval > 0f)
            {
                yield return new WaitForSeconds(waveSpawnInterval);
            }
        }
    }

    /// <summary>StartNextWave 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.SpawnFixedWave(int, int)、MissionRulesModel.BeginSubsequentWave()、MissionRulesModel.CompleteCurrentWaveGenerate(bool)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)；下游（含内部回调体） ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.SpawnFixedWave(int, int)、MissionRulesModel.BeginSubsequentWave()、MissionRulesModel.CompleteCurrentWaveGenerate(bool)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator StartNextWave()
    {
        if (secondWaveDelay > 0f)
        {
            yield return new WaitForSeconds(secondWaveDelay);
        }

        Rules.BeginSubsequentWave();
        if (currentWave == 2)
        {
            yield return SpawnFixedWave(secondWaveGanado, secondWaveSawman);
        }
        else if (currentWave == 3)
        {
            yield return SpawnFixedWave(thirdWaveGanado, thirdWaveSawman);
        }

        Rules.CompleteCurrentWaveGenerate(false);
        waveRoutine = null;
        PublishProgress(true);
        LogMission($"第 {currentWave} 波敌人已全部进入战场。");
    }

    /// <summary>TrySpawnEnemy 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType)、ChapterOneMissionManager.LogMission(string)、Dictionary&lt;VillagerController, MissionEnemyType&gt;.Add(VillagerController, MissionEnemyType)、EnemyCombatTimeline.Register(VillagerController, int)、GameObject.SetActive(bool)、MissionRulesModel.RegisterGenerate(int, MissionEnemyType)、MissionSpawnExecutor.Generate(GameObject) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.SpawnFixedWave(int, int)；下游（含内部回调体） ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType)、ChapterOneMissionManager.LogMission(string)、Dictionary&lt;VillagerController, MissionEnemyType&gt;.Add(VillagerController, MissionEnemyType)、EnemyCombatTimeline.Register(VillagerController, int)、GameObject.SetActive(bool)、MissionRulesModel.RegisterGenerate(int, MissionEnemyType)、MissionSpawnExecutor.Generate(GameObject) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">中文含义：预制件。使用位置：作为 MissionSpawnExecutor.Generate(GameObject) 的参数。</param>
    /// <param name="enemyType">中文含义：敌人类型。使用位置：作为 ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType) 的参数、作为 Dictionary&lt;VillagerController, MissionEnemyType&gt;.Add(VillagerController, MissionEnemyType) 的参数、作为 MissionRulesModel.RegisterGenerate(int, MissionEnemyType) 的参数 等共 4 处。</param>
    bool TrySpawnEnemy(GameObject prefab, MissionEnemyType enemyType)
    {
        // enemy（敌人）：保存 MissionSpawnExecutor.Generate(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<VillagerController, MissionEnemyType>.Add(VillagerController, MissionEnemyType) 的参数、作为 EnemyCombatTimeline.Register(VillagerController, int) 的参数、作为 MissionRulesModel.RegisterGenerate(int, MissionEnemyType) 的参数 等共 4 处。
        return RegisterSpawn(generate.Generate(prefab), enemyType);
    }

    private bool RegisterSpawn(VillagerController enemy, MissionEnemyType enemyType, bool reinforcement = false)
    {
        if (enemy == null)
        {
            return false;
        }

        if (!Rules.RegisterGenerate(enemy.GetInstanceID(), enemyType, reinforcement))
        {
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
            return false;
        }

        // enemyObject（敌人对象）：使用位置：ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)。
GameObject enemyObject = enemy.gameObject;
        enemy.missionEnemyType = enemyType;
        EnemyCombatTimeline.Register(enemy, currentWave);
        trackedEnemies.Add(enemy, enemyType);
        enemy.Died += OnMissionEnemyDied;
        if (!enemyObject.activeSelf)
        {
            enemyObject.SetActive(true);
        }

        LogMission($"生成{GetEnemyDisplayName(enemyType)}，普通敌人储备进度 {spawnedGanado}/{ConfiguredGanadoReserve}，电锯敌人储备进度 {spawnedSawman}/{ConfiguredSawmanReserve}。");
        return true;
    }

    /// <summary>OnMissionEnemyDied 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType)、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.ShouldStartNextWave() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType)、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.MeetsExterminationRequirement(int, int, int, int)、ChapterOneMissionManager.PublishProgress(bool)、ChapterOneMissionManager.ShouldStartNextWave() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 Dictionary&lt;VillagerController, MissionEnemyType&gt;.Remove(VillagerController, out MissionEnemyType) 的参数、作为 MissionRulesModel.RegisterDeath(int, bool) 的参数、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)。</param>
    void OnMissionEnemyDied(VillagerController enemy)
    {
        if (enemy == null || !trackedEnemies.Remove(enemy, out MissionEnemyType /* 中文含义：敌人类型。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ enemyType))
        {
            return;
        }

        enemy.Died -= OnMissionEnemyDied;
        StartVillageCombatIfReady();
        Rules.RegisterDeath(enemy.GetInstanceID(), IsPlayerAlive());
        if (villagePatrolReady && IsPlayerAlive())
        {
            if (Rules.AliveCount == 0)
            {
                nextQuietPatrolTime = Time.time + 8f;
            }
            return;
        }
        if (!IsRunning || IsCompleted || !IsPlayerAlive())
        {
            Rules.Stop();
            PublishProgress(true);
            return;
        }

        if (enemyType == MissionEnemyType.Sawman)
        {
            VillageLeonDialogue.Report(player, VillageSpeechCue.SawKilled);
        }
        LogMission($"击杀{GetEnemyDisplayName(enemyType)}，当前进度：普通敌人 {GanadoKills}/{ganadoTarget}，电锯敌人 {SawmanKills}/{sawmanTarget}。");
        PublishProgress(true);
        if (Rules.ReachElimination)
        {
            CompleteMission(MissionCompletionReason.Extermination);
            return;
        }

        if (!UsesBellEnding && ShouldStartNextWave() && waveRoutine == null)
        {
            waveRoutine = StartCoroutine(StartNextWave());
        }
    }

    /// <summary>CompleteMission 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.BuildSnapshot(bool)、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、GameManager.TryCompleteMission(MissionResult)、MissionResult.MissionResult(MissionCompletionReason, MissionProgressSnapshot)、MissionRulesModel.ConfirmComplete() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.Update()；下游（含内部回调体） ChapterOneMissionManager.BuildSnapshot(bool)、ChapterOneMissionManager.IsPlayerAlive()、ChapterOneMissionManager.LogMission(string)、ChapterOneMissionManager.PublishProgress(bool)、GameManager.TryCompleteMission(MissionResult)、MissionResult.MissionResult(MissionCompletionReason, MissionProgressSnapshot)、MissionRulesModel.ConfirmComplete() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="reason">中文含义：原因。使用位置：作为 MissionResult.MissionResult(MissionCompletionReason, MissionProgressSnapshot) 的参数、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)。</param>
    void CompleteMission(MissionCompletionReason reason)
    {
        if (IsCompleted || IsBellEnding || !IsPlayerAlive())
        {
            return;
        }

        // result（当前任务结果）：使用位置：作为 GameManager.TryCompleteMission(MissionResult) 的参数。
MissionResult result = new MissionResult(reason, BuildSnapshot(true));
        if (bellSequence != null)
        {
            IsBellEnding = true;
            StopExecute();
            PublishProgress(true);
            bellSequence.Begin(bindingMatch, currentSessionEnemyGroup, () => CommitCompletion(result));
            return;
        }

        CommitCompletion(result);
    }

    private void CommitCompletion(MissionResult result)
    {
        if (!bindingMatch.TryCompleteMission(result))
        {
            Rules.Stop();
            PublishProgress(true);
            return;
        }

        Rules.ConfirmComplete();
        Rules.Stop();
        if (startupRoutine != null)
        {
            StopCoroutine(startupRoutine);
            startupRoutine = null;
        }

        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }

        PublishProgress(true);
        // selectedValue1（分支选出的文字“已通过歼灭路线完成第一章。”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数、ChapterOneMissionManager.CompleteMission(MissionCompletionReason)。
string selectedValue1;
        if (result.CompletionReason == MissionCompletionReason.Extermination)
        {
            selectedValue1 = "已通过歼灭路线完成第一章。";
        }
        else
        {
            selectedValue1 = "已通过生存路线完成第一章。";
        }

        LogMission(selectedValue1);
    }

    /// <summary>ValidateConfiguration 的业务处理入口；本方法直接依赖 ChapterOneMissionManager.HasWalkableNavigationNodes()、Debug.LogError(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.Prepare(GameManager, EnemyAIManager, HouseThreeTraversalGeometry)；下游（含内部回调体） ChapterOneMissionManager.HasWalkableNavigationNodes()、Debug.LogError(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool ValidateConfiguration()
    {
        // valid（是否有效）：使用位置：ChapterOneMissionManager.ValidateConfiguration()。
bool valid = true;
        if (ganadoPrefabs == null || ganadoPrefabs.Length == 0)
        {
            Debug.LogError("[第一章任务] 未配置普通敌人预制件。", this);
            valid = false;
        }

        if (sawmanTemplate == null)
        {
            Debug.LogError("[第一章任务] 未配置场景层级中的电锯敌人模板。", this);
            valid = false;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[第一章任务] 未配置刷新点。", this);
            valid = false;
        }

        if (!HasWalkableNavigationNodes())
        {
            Debug.LogError("[第一章任务] 当前场景没有可用寻路图。", this);
            valid = false;
        }

        return valid;
    }

    /// <summary>中文含义：已配置村民敌人备弹。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数。</summary>
    int ConfiguredGanadoReserve => firstWaveGanado + secondWaveGanado + thirdWaveGanado;

    /// <summary>中文含义：已配置电锯敌人备弹。使用位置：作为 ChapterOneMissionManager.LogMission(string) 的参数。</summary>
    int ConfiguredSawmanReserve => firstWaveSawman + secondWaveSawman + thirdWaveSawman;

    /// <summary>ShouldStartNextWave 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool ShouldStartNextWave() => Rules.CanAdvanceWave;
    /// <summary>HasReachedWaveAdvanceThreshold 的业务处理入口；本方法直接依赖 MissionRulesModel.ReachWaveThreshold(int, int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） MissionRulesModel.ReachWaveThreshold(int, int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="activeEnemyCount">中文含义：生效敌人数量。使用位置：作为 MissionRulesModel.ReachWaveThreshold(int, int, float) 的参数。</param>
    /// <param name="baselineEnemyCount">中文含义：基准敌人数量。使用位置：作为 MissionRulesModel.ReachWaveThreshold(int, int, float) 的参数。</param>
    /// <param name="remainingRatio">中文含义：剩余比例。使用位置：作为 MissionRulesModel.ReachWaveThreshold(int, int, float) 的参数。</param>
    public static bool HasReachedWaveAdvanceThreshold(int activeEnemyCount, int baselineEnemyCount, float remainingRatio) => MissionRulesModel.ReachWaveThreshold(activeEnemyCount, baselineEnemyCount, remainingRatio);
    /// <summary>CanAdvanceGameplay 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool CanAdvanceGameplay()
    {
        if (Time.timeScale <= 0f)
        {
            return false;
        }

        if (currentSessionEnemyGroup == null || !currentSessionEnemyGroup.IsCombatUnlocked)
        {
            return false;
        }

        if (bindingMatch == null || !bindingMatch.Permission.Current.AllowMissionTimer)
        {
            return false;
        }

        return true;
    }

    /// <summary>IsPlayerAlive 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    bool IsPlayerAlive()
    {
        if (player == null)
        {
            return false;
        }

        return player.Health == null || !player.Health.IsDead;
    }

    /// <summary>PublishProgress 的业务处理入口；本方法直接依赖 Action&lt;MissionProgressSnapshot&gt;.Invoke(MissionProgressSnapshot)、ChapterOneMissionManager.BuildSnapshot(bool)、Mathf.CeilToInt(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()、ChapterOneMissionManager.StartNextWave()、ChapterOneMissionManager.Update()；下游（含内部回调体） Action&lt;MissionProgressSnapshot&gt;.Invoke(MissionProgressSnapshot)、ChapterOneMissionManager.BuildSnapshot(bool)、Mathf.CeilToInt(float)、Mathf.Max(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="force">中文含义：是否力度。使用位置：ChapterOneMissionManager.PublishProgress(bool)。</param>
    void PublishProgress(bool force)
    {
        // remainingSecond（剩余第二）：保存 Mathf.CeilToInt(float) 的返回结果，供当前步骤使用。使用位置：ChapterOneMissionManager.PublishProgress(bool)。
int remainingSecond = Mathf.CeilToInt(Mathf.Max(0f, survivalDuration - ElapsedGameplaySeconds));
        if (!force && remainingSecond == lastDisplayedRemainingSecond)
        {
            return;
        }

        lastDisplayedRemainingSecond = remainingSecond;
        // receiverValue2（判空前缓存的进度变化）：从进度变化读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ChapterOneMissionManager.PublishProgress(bool)。
global::System.Action<global::MissionProgressSnapshot> receiverValue2 = ProgressChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke(BuildSnapshot());
        }
    }

    /// <summary>BuildSnapshot 的业务处理入口；本方法直接依赖 MissionRulesModel.Snapshot(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.LatestSnapshot.get、ChapterOneMissionManager.PublishProgress(bool)；下游（含内部回调体） MissionRulesModel.Snapshot(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="completed">中文含义：是否已完成。使用位置：作为 MissionRulesModel.Snapshot(bool) 的参数。</param>
    MissionProgressSnapshot BuildSnapshot(bool completed = false) => Rules.Snapshot(completed);
    /// <summary>UnsubscribeAllEnemies 的业务处理入口；本方法直接依赖 Dictionary&lt;VillagerController, MissionEnemyType&gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnDestroy()；下游（含内部回调体） Dictionary&lt;VillagerController, MissionEnemyType&gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UnsubscribeAllEnemies()
    {
        // enemy（敌人）：每轮从跟踪的敌人集合的键集合取得一个元素，仅代表当前这一项。使用位置：ChapterOneMissionManager.UnsubscribeAllEnemies()。
foreach (VillagerController enemy in trackedEnemies.Keys)
        {
            if (enemy != null)
            {
                enemy.Died -= OnMissionEnemyDied;
            }
        }

        trackedEnemies.Clear();
    }

    /// <summary>RemoveUnmanagedRuntimeEnemies 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、Dictionary&lt;VillagerController, MissionEnemyType&gt;.ContainsKey(VillagerController)、Object.Destroy(Object)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsInactive, FindObjectsSortMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()；下游（含内部回调体） Debug.LogWarning(object, Object)、Dictionary&lt;VillagerController, MissionEnemyType&gt;.ContainsKey(VillagerController)、Object.Destroy(Object)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsInactive, FindObjectsSortMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RemoveUnmanagedRuntimeEnemies()
    {
        // activeEnemies（生效敌人集合）：保存 Object.FindObjectsByType<VillagerController>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：ChapterOneMissionManager.RemoveUnmanagedRuntimeEnemies()。
VillagerController[] activeEnemies = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        // enemy（敌人）：每轮从生效敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 Dictionary<VillagerController, MissionEnemyType>.ContainsKey(VillagerController) 的参数、作为 Object.Destroy(Object) 的参数 等共 4 处。
foreach (VillagerController enemy in activeEnemies)
        {
            if (enemy == null || enemy.assassinationShowcaseIdle || trackedEnemies.ContainsKey(enemy))
            {
                continue;
            }

            Debug.LogWarning($"[第一章任务] 已移除未登记的运行时敌人“{enemy.name}”。", enemy);
            Destroy(enemy.gameObject);
        }
    }

    /// <summary>LogMission 的业务处理入口；本方法直接依赖 Debug.Log(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.CompleteMission(MissionCompletionReason)、ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.StartMissionWhenInitialEnemiesReady()、ChapterOneMissionManager.StartNextWave()、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)；下游（含内部回调体） Debug.Log(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="message">中文含义：消息。使用位置：作为 Debug.Log(object, Object) 的参数。</param>
    void LogMission(string message)
    {
        if (logMissionEvents)
        {
            Debug.Log("[第一章任务] " + message, this);
        }
    }

    /// <summary>GetEnemyDisplayName 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)、ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemyType">中文含义：敌人类型。使用位置：ChapterOneMissionManager.GetEnemyDisplayName(MissionEnemyType)。</param>
    static string GetEnemyDisplayName(MissionEnemyType enemyType)
    {
        if (enemyType == MissionEnemyType.Sawman)
        {
            return "电锯敌人";
        }
        else
        {
            return "普通敌人";
        }
    }

    /// <summary>MeetsExterminationRequirement 的业务处理入口；本方法直接依赖 MissionRulesModel.ReachEliminationTarget(int, int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.OnMissionEnemyDied(VillagerController)；下游（含内部回调体） MissionRulesModel.ReachEliminationTarget(int, int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ganadoKills">中文含义：村民敌人击杀数。使用位置：作为 MissionRulesModel.ReachEliminationTarget(int, int, int, int) 的参数。</param>
    /// <param name="sawmanKills">中文含义：电锯敌人击杀数。使用位置：作为 MissionRulesModel.ReachEliminationTarget(int, int, int, int) 的参数。</param>
    /// <param name="requiredGanadoKills">中文含义：所需村民敌人击杀数。使用位置：作为 MissionRulesModel.ReachEliminationTarget(int, int, int, int) 的参数。</param>
    /// <param name="requiredSawmanKills">中文含义：所需电锯敌人击杀数。使用位置：作为 MissionRulesModel.ReachEliminationTarget(int, int, int, int) 的参数。</param>
    public static bool MeetsExterminationRequirement(int ganadoKills, int sawmanKills, int requiredGanadoKills, int requiredSawmanKills) => MissionRulesModel.ReachEliminationTarget(ganadoKills, sawmanKills, requiredGanadoKills, requiredSawmanKills);
    /// <summary>MeetsSurvivalRequirement 的业务处理入口；本方法直接依赖 MissionRulesModel.ReachSurvivalTarget(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.Update()；下游（含内部回调体） MissionRulesModel.ReachSurvivalTarget(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="elapsedSeconds">中文含义：已用秒。使用位置：作为 MissionRulesModel.ReachSurvivalTarget(float, float) 的参数。</param>
    /// <param name="requiredSeconds">中文含义：所需秒。使用位置：作为 MissionRulesModel.ReachSurvivalTarget(float, float) 的参数。</param>
    public static bool MeetsSurvivalRequirement(float elapsedSeconds, float requiredSeconds) => MissionRulesModel.ReachSurvivalTarget(elapsedSeconds, requiredSeconds);
}
