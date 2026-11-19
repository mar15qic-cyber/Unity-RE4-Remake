using GameArchitecture;
using UnityEngine;

public sealed partial class CharacterHealthModule
{
    private readonly EnemyRevivalRules revivalRules = new EnemyRevivalRules();

    public EnemyLifePhase LifePhase => revivalRules.Phase;
    public long LifeRevision => revivalRules.Revision;
    public double RevivalDeadline => revivalRules.Deadline;

    /// <summary>必须在扣除致命伤害前调用，禁止已经发布死亡的对象恢复生命。</summary>
    internal bool TryBeginDying(float incomingDamage, float continuationHealth, double deadline)
    {
        if (!initialized || deathFact || incomingDamage < currentHealthValue || incomingDamage <= 0f
            || float.IsNaN(incomingDamage) || float.IsInfinity(incomingDamage)
            || continuationHealth <= 0f || float.IsNaN(continuationHealth) || float.IsInfinity(continuationHealth))
        {
            return false;
        }
        if (!revivalRules.BeginDying(deadline))
        {
            return false;
        }
        currentHealthValue = continuationHealth;
        return true;
    }

    internal bool TryBeginReviving(long revision, double now)
    {
        return !deathFact && revivalRules.BeginReviving(revision, now);
    }

    internal bool ScheduleRevivalWait(long revision, double deadline)
    {
        return !deathFact && revivalRules.ScheduleWait(revision, deadline);
    }

    internal bool CompleteRevival(long revision, float revivedHealth)
    {
        if (deathFact || !float.IsFinite(revivedHealth) || revivedHealth <= 0f || !revivalRules.CompleteRevival(revision))
        {
            return false;
        }
        currentHealthValue = revivedHealth;
        return true;
    }

    private void OnDisable()
    {
        revivalRules.InvalidatePendingTransition();
    }
}
