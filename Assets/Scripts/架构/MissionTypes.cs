using System;

/// <summary>第一章任务敌人分类。</summary>
public enum MissionEnemyType
{
    /// <summary>中文含义：村民敌人。普通敌人类别，生成登记和击杀统计使用此分类。</summary>
    Ganado,
    /// <summary>中文含义：电锯敌人。电锯敌人类别，单独累计生成、存活及击杀。</summary>
    Sawman
}

/// <summary>第一章通关原因。</summary>
public enum MissionCompletionReason
{
    /// <summary>中文含义：歼灭。通过两类敌人击杀目标完成任务。</summary>
    Extermination,
    /// <summary>中文含义：生存。通过累计存活时间目标完成任务。</summary>
    Survival
}

/// <summary>单局终局状态。</summary>
public enum GameEndState
{
    /// <summary>中文含义：运行中。对局尚未提交胜利或失败结果。</summary>
    Running,
    /// <summary>中文含义：胜利。对局已接受唯一胜利结果。</summary>
    Victory,
    /// <summary>中文含义：失败。对局已接受唯一失败结果。</summary>
    Defeat
}

/// <summary>MissionRulesModel.Snapshot 创建的实时进度值，任务宿主转发给显示链；构造不修改规则，也不自行广播事件。</summary>
[Serializable]
public readonly struct MissionProgressSnapshot
{
    /// <summary>中文含义：村民敌人击杀数。快照构造时已累计的普通敌人击杀数。</summary>
    public int GanadoKills { get; }
    /// <summary>中文含义：村民敌人目标。配置传入的普通敌人击杀目标。</summary>
    public int GanadoTarget { get; }
    /// <summary>中文含义：电锯敌人击杀数。快照构造时已累计的电锯敌人击杀数。</summary>
    public int SawmanKills { get; }
    /// <summary>中文含义：电锯敌人目标。配置传入的电锯敌人击杀目标。</summary>
    public int SawmanTarget { get; }
    /// <summary>中文含义：已用秒。宿主按许可推进后累计的任务秒数。</summary>
    public float ElapsedSeconds { get; }
    /// <summary>中文含义：生存持续时间。配置要求的存活目标秒数。</summary>
    public float SurvivalDuration { get; }
    /// <summary>中文含义：是否运行中。构造时任务是否仍在运行。</summary>
    public bool IsRunning { get; }
    /// <summary>中文含义：是否已完成。构造时实际完成或调用方标记的预期完成状态。</summary>
    public bool IsCompleted { get; }
    /// <summary>中文含义：剩余秒。根据目标减去累计时间计算剩余秒数，至少为零。</summary>
    public float RemainingSeconds => Math.Max(0f, SurvivalDuration - ElapsedSeconds);
    /// <summary>中文含义：总计击杀数。普通与电锯敌人击杀之和，不产生新的统计写入。</summary>
    public int TotalKills => GanadoKills + SawmanKills;

    /// <summary>任务数据链构造此值时复制全部参数，消费者通过只读属性查询。</summary>
    /// <param name="ganadoKills">中文含义：村民敌人击杀数。普通敌人击杀数。</param>
    /// <param name="ganadoTarget">中文含义：村民敌人目标。普通敌人目标。</param>
    /// <param name="sawmanKills">中文含义：电锯敌人击杀数。电锯敌人击杀数。</param>
    /// <param name="sawmanTarget">中文含义：电锯敌人目标。电锯敌人目标。</param>
    /// <param name="elapsedSeconds">中文含义：已用秒。累计任务秒数。</param>
    /// <param name="survivalDuration">中文含义：生存持续时间。存活目标秒数。</param>
    /// <param name="isRunning">中文含义：是否运行中。任务运行状态。</param>
    /// <param name="isCompleted">中文含义：是否已完成。完成状态。</param>
    public MissionProgressSnapshot(int ganadoKills, int ganadoTarget, int sawmanKills, int sawmanTarget, float elapsedSeconds, float survivalDuration, bool isRunning, bool isCompleted)
    {
        GanadoKills = ganadoKills;
        GanadoTarget = ganadoTarget;
        SawmanKills = sawmanKills;
        SawmanTarget = sawmanTarget;
        ElapsedSeconds = elapsedSeconds;
        SurvivalDuration = survivalDuration;
        IsRunning = isRunning;
        IsCompleted = isCompleted;
    }
}

/// <summary>任务宿主构造并提交给 GameManager 的通关结算值，成功后通过完成事件传给界面。</summary>
[Serializable]
public readonly struct MissionResult
{
    /// <summary>中文含义：完成原因。任务宿主确认的通关原因，供结算界面读取。</summary>
    public MissionCompletionReason CompletionReason { get; }
    /// <summary>中文含义：进度。通关时的进度值副本，之后模型变化不追随更新。</summary>
    public MissionProgressSnapshot Progress { get; }

    /// <summary>任务数据链构造此值时复制全部参数，消费者通过只读属性查询。</summary>
    /// <param name="completionReason">中文含义：完成原因。通关原因。</param>
    /// <param name="progress">中文含义：进度。通关时进度快照。</param>
    public MissionResult(MissionCompletionReason completionReason, MissionProgressSnapshot progress)
    {
        CompletionReason = completionReason;
        Progress = progress;
    }
}
