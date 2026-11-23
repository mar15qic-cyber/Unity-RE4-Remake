using UnityEngine;

public partial class ChapterOneMissionManager
{
    [SerializeField, Tooltip("正式村庄的农场门和三号屋登场演出。")]
    private VillageSiegeSequence siegeSequence;

    private float nextEntranceAttempt;

    private bool ObserveVillageApproach(out bool approachedFarm)
    {
        approachedFarm = false;
        if (!UsesBellEnding || siegeSequence == null || bindingMatch == null || bindingMatch.IsTerminal ||
            !bindingMatch.Permission.Current.AllowInput || !IsPlayerAlive() || (!villagePatrolReady && !IsRunning))
        {
            return false;
        }

        bool enteredHouse = siegeSequence.ObserveHouseEntrance(player);
        approachedFarm = siegeSequence.IsApproachingFarm(player, waveConfiguration.FarmEntranceRadius);
        if (villagePatrolReady && (enteredHouse || approachedFarm))
        {
            currentSessionEnemyGroup.UnlockCombatAtVillageEntrance(player);
        }

        return enteredHouse;
    }

    private bool TryBeginVillageEntrance()
    {
        VillageEntrance entrance = Rules.PendingEntrance;
        if (entrance == VillageEntrance.None || Time.time < nextEntranceAttempt || !siegeSequence.CanStart(bindingMatch))
        {
            return false;
        }

        VillagerController actor = null;
        if (Rules.CanSpawnAtEntrance(entrance))
        {
            Transform anchor = siegeSequence.GetSpawnAnchor(entrance);
            actor = generate.GenerateEntrance(sawmanTemplate, anchor, waveConfiguration.EntranceSawmanHealth);
            if (!RegisterSpawn(actor, MissionEnemyType.Sawman))
            {
                nextEntranceAttempt = Time.time + spawnRetryDelay;
                return false;
            }
        }
        else
        {
            // 不新增敌人时沿用本局已有的电锯敌人，保持演出与战斗身份一致。
            foreach (var tracked in trackedEnemies)
            {
                if (tracked.Value == MissionEnemyType.Sawman && tracked.Key != null &&
                    !tracked.Key.IsDead && tracked.Key.gameObject.activeInHierarchy)
                {
                    actor = tracked.Key;
                    break;
                }
            }
        }

        Rules.CompleteEntrance(entrance);
        bool nearFarm = entrance == VillageEntrance.Farm && siegeSequence.IsApproachingFarm(player, waveConfiguration.FarmEntranceRadius);
        siegeSequence.Play(bindingMatch, entrance, actor, nearFarm);
        return true;
    }
}
