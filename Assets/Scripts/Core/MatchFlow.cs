/// <summary>
/// 本局终局事实的唯一拥有者。GameManager 同步提交死亡或胜利，只有首次提交成功。
/// 本类型不广播事件；宿主根据提交结果通知界面，消费者通过只读属性读取结算状态。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "对局流程")]
public sealed class MatchFlow
{
    /// <summary>中文含义：状态。当前对局状态；仅本类型可由运行中改为失败或胜利。</summary>
    public GameEndState State { get; private set; } = GameEndState.Running;
    /// <summary>中文含义：级别完成结果。提交胜利时保存的任务结算数据；其他状态下保持默认值。</summary>
    public MissionResult LevelCompleteResult { get; private set; }
    /// <summary>中文含义：是否具有对局已结束。供提交入口及宿主判断是否已有不可覆盖的终局结果。</summary>
    public bool HasMatchEnded => State != GameEndState.Running;

    /// <summary>GameManager.OnPlayerDeath 调用；首次写入失败，重复死亡不再次结算。</summary>
    /// <returns>是否接纳本次死亡；宿主仅在 true 时发送死亡通知。</returns>
    public bool CommitDeath()
    {
        if (HasMatchEnded)
        {
            return false;
        }

        State = GameEndState.Defeat;
        return true;
    }

    /// <summary>GameManager.TryCompleteMission 调用；仅运行中且玩家存活时接纳胜利。</summary>
    /// <param name="result">中文含义：当前任务结果。任务宿主生成的结算数据，成功时保存供只读查询。</param>
    /// <param name="playerHasDeath">中文含义：玩家是否具有死亡。宿主读取的玩家死亡事实，用于阻止死亡后通关。</param>
    /// <returns>是否接纳本次胜利；宿主仅在 true 时广播任务完成。</returns>
    public bool CommitLevelComplete(MissionResult result, bool playerHasDeath)
    {
        if (HasMatchEnded || playerHasDeath)
        {
            return false;
        }

        LevelCompleteResult = result;
        State = GameEndState.Victory;
        return true;
    }
}
