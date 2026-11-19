using UnityEngine;

public sealed partial class KnifeCombatModule
{
    private int lastGroundFinisherSession;

    internal void CommitEmergencyParryCost()
    {
        if (ValidKnife && configuration != null)
        {
            DeductDurability(configuration.normalParryConsumption);
        }
    }

    internal bool CommitGroundFinisherCost(int session, GridItem knife)
    {
        if (session <= 0 || session == lastGroundFinisherSession || knife == null || configuration == null)
        {
            return false;
        }
        if (!TryCommitFinisherCost(knife))
        {
            return false;
        }
        lastGroundFinisherSession = session;
        return true;
    }

    internal bool TryCommitFinisherCost(GridItem knife)
    {
        if (configuration == null || inventory == null)
        {
            return false;
        }

        if (!inventory.TryConsumeKnife(knife, configuration.finisherConsumption, out bool broken))
        {
            return false;
        }

        if (broken)
        {
            PlayKnifeBreakFeedback();
            if (weapon != null && inventory.GetPreferredKnife() == null)
            {
                weapon.HandleKnifeBroken();
            }
        }
        return true;
    }
}
