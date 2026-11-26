using System;
using System.Collections.Generic;

/// <summary>
/// 任务波次、生成、击杀和计时的唯一状态拥有者。ChapterOneMissionManager 同步提交生成与死亡事实并推进时间，
/// 再读取条件与 Snapshot 决定波次和结算；模型不广播事件，不访问场景或引擎时间。
/// </summary>
public sealed partial class MissionRulesModel
{
    /// <summary>中文含义：存活登记。按敌人身份保存仍存活的登记，用于拒绝重复生成和重复死亡事实。</summary>
    private readonly Dictionary<int, MissionEnemyType> aliveRegister = new Dictionary<int, MissionEnemyType>();
    /// <summary>中文含义：常规击杀。任务运行期间且玩家存活时记入的普通敌人击杀数。</summary>
    public int NormalKill { get; private set; }
    /// <summary>中文含义：电锯击杀。任务运行期间且玩家存活时记入的电锯敌人击杀数。</summary>
    public int ChainsawKill { get; private set; }
    /// <summary>中文含义：常规是否具有生成。已接纳的普通敌人生成总数，不随死亡减少。</summary>
    public int NormalHasGenerate { get; private set; }
    /// <summary>中文含义：电锯是否具有生成。已接纳的电锯敌人生成总数，不随死亡减少。</summary>
    public int ChainsawHasGenerate { get; private set; }
    /// <summary>中文含义：常规存活。名册中仍存活的普通敌人数。</summary>
    public int NormalAlive { get; private set; }
    /// <summary>中文含义：电锯存活。名册中仍存活的电锯敌人数。</summary>
    public int ChainsawAlive { get; private set; }
    /// <summary>中文含义：波次。当前波次编号；初始化为零，首波开始时设为一。</summary>
    public int Wave { get; private set; }
    /// <summary>中文含义：波次开始的存活。本波生成完成时的全部存活数，用作下一波阈值的分母。</summary>
    public int WaveStartingAlive { get; private set; }
    /// <summary>中文含义：已使用的时间。宿主按许可推进的累计任务时间，上限为存活目标时长。</summary>
    public float UsedTime { get; private set; }
    /// <summary>中文含义：是否运行时中间。首波生成完成后进入运行，停止、完成或无效死亡结算时退出。</summary>
    public bool RuntimeMiddle { get; private set; }
    /// <summary>中文含义：是否已完成。宿主已确认任务完成的事实；仅 Initialize 清除。</summary>
    public bool Completed { get; private set; }

    /// <summary>中文含义：常规目标。普通敌人击杀目标；正式运行由宿主读取配置后传入 Initialize。</summary>
    private int normalTarget = 15;
    /// <summary>中文含义：电锯目标。电锯敌人击杀目标；正式运行由 Initialize 覆盖默认值。</summary>
    private int chainsawTarget = 2;
    /// <summary>中文含义：生存持续时间。存活目标秒数；同时作为累计时间上限。</summary>
    private float survivalDuration = 360;
    /// <summary>中文含义：推进比例。剩余存活数相对波初基线的推进比例。</summary>
    private float advanceRatio = .3f;
    /// <summary>中文含义：存活数量。当前名册存活总数，供波次阈值及宿主读取。</summary>
    public int AliveCount => NormalAlive + ChainsawAlive;

    private int reinforcementStep;
    private int reinforcementCapacity;
    private int reinforcementsGenerated;
    private int bellKillTarget;

    public int TotalKills => NormalKill + ChainsawKill;

    public int PendingReinforcements
    {
        get
        {
            if (!RuntimeMiddle || Completed || reinforcementStep == 0 || ReachElimination)
            {
                return 0;
            }

            int earned = TotalKills / reinforcementStep * reinforcementStep;
            return Math.Max(0, Math.Min(earned - reinforcementsGenerated, reinforcementCapacity - AliveCount));
        }
    }
    /// <summary>中文含义：是否可以推进波次。前三波约束下是否达到推进比例；宿主仍负责实际生成下一波。</summary>
    public bool CanAdvanceWave => reinforcementStep == 0 && Wave < 3 &&
        ReachWaveThreshold(AliveCount, WaveStartingAlive, advanceRatio);
    /// <summary>中文含义：是否抵达歼灭。两类敌人的击杀是否都达到各自目标。</summary>
    public bool ReachElimination
    {
        get
        {
            if (bellKillTarget > 0)
            {
                return TotalKills >= bellKillTarget;
            }

            return ReachEliminationTarget(NormalKill, ChainsawKill, normalTarget, chainsawTarget);
        }
    }
    /// <summary>中文含义：是否抵达生存。累计运行时间是否达到存活目标。</summary>
    public bool ReachSurvival => ReachSurvivalTarget(UsedTime, survivalDuration);

    /// <summary>任务宿主准备本局时调用，复制配置并清空上局所有名册、计数和运行状态。</summary>
    /// <param name="normalTarget">中文含义：常规目标。配置中的普通敌人击杀目标。</param>
    /// <param name="chainsawTarget">中文含义：电锯目标。配置中的电锯敌人击杀目标。</param>
    /// <param name="survivalDuration">中文含义：生存持续时间。配置中的存活目标秒数。</param>
    /// <param name="advanceRatio">中文含义：推进比例。配置中的下一波剩余敌人比例。</param>
    public void Initialize(int normalTarget, int chainsawTarget, float survivalDuration, float advanceRatio)
    {
        ResetVillageEntrance();
        this.normalTarget = normalTarget;
        this.chainsawTarget = chainsawTarget;
        this.survivalDuration = survivalDuration;
        this.advanceRatio = advanceRatio;
        reinforcementStep = 0;
        reinforcementCapacity = 0;
        reinforcementsGenerated = 0;
        bellKillTarget = 0;
        aliveRegister.Clear();
        NormalKill = 0;
        ChainsawKill = 0;
        NormalHasGenerate = 0;
        ChainsawHasGenerate = 0;
        NormalAlive = 0;
        ChainsawAlive = 0;
        Wave = 0;
        WaveStartingAlive = 0;
        UsedTime = 0;
        RuntimeMiddle = false;
        Completed = false;
    }

    public void InitializeVillage(int killTarget, float duration, int capacity, int replacementCount)
    {
        Initialize(killTarget, 0, duration, .3f);
        bellKillTarget = killTarget;
        reinforcementCapacity = capacity;
        reinforcementStep = replacementCount;
    }

    /// <summary>宿主开放任务时调用，标记首波；计时仍等待首波生成完成后才开放。</summary>
    public void BeginFirstWave()
    {
        Wave = 1;
    }

    /// <summary>宿主判断推进条件通过后调用，只递增编号，不自行生成敌人。</summary>
    public void BeginSubsequentWave()
    {
        Wave++;
    }

    /// <summary>宿主收到本波生成结束后提交，记录全部存活基线，首波同时开放运行状态。</summary>
    /// <param name="firstWave">中文含义：是否首个波次。是否为首波；后续波次不重新启动已停止的任务。</param>
    public void CompleteCurrentWaveGenerate(bool firstWave)
    {
        WaveStartingAlive = AliveCount;
        if (firstWave)
        {
            RuntimeMiddle = true;
        }
    }

    /// <summary>宿主将实际生成成功的角色登记到模型；同一身份只累计一次。</summary>
    /// <param name="identity">中文含义：身份标识。角色身份编号，在当前名册内必须唯一。</param>
    /// <param name="type">中文含义：当前任务敌人类型。生成角色类别，决定更新哪一组计数。</param>
    /// <returns>首次登记成功返回 true；重复身份返回 false。</returns>
    public bool RegisterGenerate(int identity, MissionEnemyType type, bool reinforcement = false)
    {
        if (aliveRegister.ContainsKey(identity) || (reinforcement && PendingReinforcements == 0))
        {
            return false;
        }

        aliveRegister.Add(identity, type);
        if (reinforcement)
        {
            reinforcementsGenerated++;
        }
        if (type == MissionEnemyType.Sawman)
        {
            ChainsawHasGenerate++;
            ChainsawAlive++;
        }
        else
        {
            NormalHasGenerate++;
            NormalAlive++;
        }

        return true;
    }

    /// <summary>宿主提交角色死亡事实；先从名册移除并减少存活数，再按任务与玩家状态决定是否记入击杀。</summary>
    /// <param name="identity">中文含义：身份标识。死亡角色的已登记身份。</param>
    /// <param name="playerAlive">中文含义：玩家是否存活。宿主在提交时读取的玩家存活事实。</param>
    /// <returns>是否接纳死亡；未登记或重复死亡返回 false。</returns>
    public bool RegisterDeath(int identity, bool playerAlive)
    {
        // type 接收生成时保存的敌人类别，避免死亡回调自行更改统计分类。
        if (!aliveRegister.TryGetValue(identity, out var /* 中文含义：当前任务敌人类型。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ type))
        {
            return false;
        }

        aliveRegister.Remove(identity);
        if (type == MissionEnemyType.Sawman)
        {
            ChainsawAlive = Math.Max(0, ChainsawAlive - 1);
        }
        else
        {
            NormalAlive = Math.Max(0, NormalAlive - 1);
        }

        if (!RuntimeMiddle || Completed || !playerAlive)
        {
            RuntimeMiddle = false;
            return true;
        }

        if (type == MissionEnemyType.Sawman)
        {
            ChainsawKill++;
        }
        else
        {
            NormalKill++;
        }

        return true;
    }

    /// <summary>任务宿主在计时许可允许时传入时间增量；模型只在运行且未完成时累计并限制上限。</summary>
    /// <param name="duration">中文含义：持续时间。宿主提供的本次任务时间增量，模型不自行读取引擎时钟。</param>
    public void AdvanceTime(float duration)
    {
        if (RuntimeMiddle && !Completed)
        {
            UsedTime = Math.Min(survivalDuration, UsedTime + duration);
        }
    }

    /// <summary>宿主停止任务时调用，只关闭推进状态，保留进度供界面和诊断读取。</summary>
    public void Stop()
    {
        RuntimeMiddle = false;
        pendingFarmEntrance = false;
        pendingHouseEntrance = false;
    }

    /// <summary>宿主确认终局提交成功后调用，写入完成事实并停止计时与后续击杀累计。</summary>
    public void ConfirmComplete()
    {
        Completed = true;
        Stop();
    }

    /// <summary>宿主构造进度通知或结算数据时读取；通过值快照向显示链传递，不暴露可写模型。</summary>
    /// <param name="expectedComplete">中文含义：是否预期完成。构造预期完成结果时使用，不改变模型实际完成状态。</param>
    /// <returns>当前击杀、目标、计时及运行状态的快照。</returns>
    public MissionProgressSnapshot Snapshot(bool expectedComplete = false)
    {
        return new MissionProgressSnapshot(NormalKill, normalTarget, ChainsawKill, chainsawTarget, UsedTime, survivalDuration, RuntimeMiddle, expectedComplete || Completed);
    }

    /// <summary>CanAdvanceWave 共用的纯阈值判断；基线为零时拒绝推进，比例限制在既有范围内。</summary>
    /// <param name="alive">中文含义：存活。当前存活总数。</param>
    /// <param name="baseline">中文含义：基准。本波生成结束时的存活基线。</param>
    /// <param name="ratio">中文含义：比例。配置中的推进比例，计算时限制到 0.05 至 0.95。</param>
    /// <returns>存活比例达到推进条件时返回 true。</returns>
    public static bool ReachWaveThreshold(int alive, int baseline, float ratio)
    {
        return baseline > 0 && alive / (float)baseline <= Math.Max(.05f, Math.Min(.95f, ratio));
    }

    /// <summary>ReachElimination 共用的纯判断，要求两类敌人的目标同时满足。</summary>
    /// <param name="normal">中文含义：普通状态值。普通敌人击杀数。</param>
    /// <param name="chainsaw">中文含义：电锯。电锯敌人击杀数。</param>
    /// <param name="normalTarget">中文含义：常规目标。普通敌人目标。</param>
    /// <param name="chainsawTarget">中文含义：电锯目标。电锯敌人目标。</param>
    /// <returns>两个目标均满足时返回 true。</returns>
    public static bool ReachEliminationTarget(int normal, int chainsaw, int normalTarget, int chainsawTarget)
    {
        return normal >= normalTarget && chainsaw >= chainsawTarget;
    }

    /// <summary>ReachSurvival 共用的纯判断，将宿主累计时间与配置目标比较。</summary>
    /// <param name="duration">中文含义：持续时间。累计任务时间。</param>
    /// <param name="target">中文含义：目标。存活目标秒数。</param>
    /// <returns>累计时间达到目标时返回 true。</returns>
    public static bool ReachSurvivalTarget(float duration, float target)
    {
        return duration >= target;
    }
}
