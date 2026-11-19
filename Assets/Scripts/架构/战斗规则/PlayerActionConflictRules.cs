using GameArchitecture;

/// <summary>
/// 玩家动作的纯审批规则。LeonAnimationActionCoordinator.CheckActionConflict 传入当前占用快照，
/// Check 同步返回拒绝原因；规则不写入事务、动画或场景状态。
/// </summary>
public static class PlayerActionConflictRules
{
    /// <summary>依次执行死亡、全身动作打断、通道占用和切枪目标检查，返回首个拒绝原因。</summary>
    /// <param name="request">中文含义：请求。本次申请的动作种类。</param>
    /// <param name="deathEndEnd">中文含义：是否死亡结束结束。协调器是否已经进入死亡终止状态。</param>
    /// <param name="weaponOccupancy">中文含义：是否武器占用。武器动作通道是否被占用。</param>
    /// <param name="fullBodyOccupancy">中文含义：是否全身占用。全身动作通道是否被占用。</param>
    /// <param name="currentAsMelee">中文含义：是否当前作为近战。当前全身动作是否为可被受击或配对接管的体术。</param>
    /// <param name="waitingWeaponExit">中文含义：是否等待中武器退出。是否还在等待武器动作退出。</param>
    /// <param name="hasHasSwitchTarget">中文含义：是否具有切换目标。是否已有待完成的切枪目标。</param>
    /// <returns>None 表示允许继续审批；其他值由协调器作为拒绝结果返回。</returns>
    public static RejectionReason Check(ActionKind request, bool deathEndEnd, bool weaponOccupancy, bool fullBodyOccupancy, bool currentAsMelee, bool waitingWeaponExit, bool hasHasSwitchTarget)
    {
        if (deathEndEnd)
        {
            return RejectionReason.HasDeath;
        }

        if (request == ActionKind.Death)
        {
            return RejectionReason.None;
        }

        if (request == ActionKind.HitReaction || request == ActionKind.PairedActionAction)
        {
            if (fullBodyOccupancy && !currentAsMelee)
            {
                return RejectionReason.ActionConflict;
            }

            return RejectionReason.None;
        }

        if (weaponOccupancy || fullBodyOccupancy || waitingWeaponExit)
        {
            return RejectionReason.ActionConflict;
        }

        if (request == ActionKind.SwitchWeapon && hasHasSwitchTarget)
        {
            return RejectionReason.ActionConflict;
        }

        return RejectionReason.None;
    }
}
