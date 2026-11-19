using GameArchitecture;
using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private void ResumePendingRevival()
    {
        if (HealthModule == null || !HealthModule.Ready || IsDead || IsQTEControlled || nativeCombatProfile == null
            || !nativeCombatProfile.enableRevival)
        {
            return;
        }
        EnemyLifePhase life = HealthModule.LifePhase;
        if (life != EnemyLifePhase.Dying && life != EnemyLifePhase.Reviving)
        {
            return;
        }
        string motion = "Native_3591";
        NativeCombatPhase phase = NativeCombatPhase.DyingLoop;
        if (nativeDownFaceUp)
        {
            motion = "Native_3596";
        }
        if (life == EnemyLifePhase.Reviving)
        {
            phase = NativeCombatPhase.Reviving;
            motion = "Native_3592";
            if (nativeDownFaceUp)
            {
                motion = "Native_3597";
            }
        }
        if (!TryReadNativeCombatTrack(motion, out var track))
        {
            return;
        }
        nativeCombatAction = ActionCoordinator.BeginNativeCombat(false);
        if (!nativeCombatAction.Valid)
        {
            return;
        }
        nativeLifeRevision = HealthModule.LifeRevision;
        nativeCombatOrientation = transform.rotation;
        SetNativeCombatTrack(track, phase);
    }

    private bool TryInterceptLethalRevival(float damage, Vector3 point)
    {
        if (nativeCombatProfile == null || !nativeCombatProfile.enableRevival
            || nativeCombatProfile.revivalContinuationHealth <= 0f || damage < currentHealth || damage <= 0f
            || missionEnemyType != MissionEnemyType.Ganado || IsQTEControlled || host.IsNavigationTraversalProtected
            || HealthModule.LifePhase != EnemyLifePhase.Alive || permanentCrawl)
        {
            return false;
        }
        if (!EnemyRevivalRules.ShouldRevive(true, nativeCombatProfile.revivalProbability, Random.value))
        {
            return false;
        }
        // 等待从原版倒地起身前置动作结束后开始，不能在落地过程中提前耗尽。
        double deadline = double.MaxValue;
        if (!BeginNativeKnockdown(point, true))
        {
            return false;
        }
        if (!HealthModule.TryBeginDying(damage, nativeCombatProfile.revivalContinuationHealth, deadline))
        {
            CancelNativeCombat(true);
            return false;
        }
        nativeLifeRevision = HealthModule.LifeRevision;
        return true;
    }

    private void BeginNativeRevivalWait()
    {
        if (nativeCombatProfile == null || HealthModule.RevivalDeadline != double.MaxValue)
        {
            return;
        }
        double deadline = Time.timeAsDouble + nativeCombatProfile.revivalWaitBase
            + Random.value * nativeCombatProfile.revivalWaitRandom;
        HealthModule.ScheduleRevivalWait(nativeLifeRevision, deadline);
    }
}
