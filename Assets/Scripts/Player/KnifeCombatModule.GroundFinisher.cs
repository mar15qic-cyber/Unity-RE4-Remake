using UnityEngine;

public sealed partial class KnifeCombatModule
{
    private int lastGroundFinisherSession;

    // 提示和接触分别检查实际使用的刀，耐久恰好耗尽仍可完成这一次刺击。
    internal bool HasGroundFinisherKnife => ValidKnife && SelectGroundFinisherKnife() != null;

    internal GridItem SelectGroundFinisherKnife()
    {
        if (inventory == null || configuration == null)
        {
            return null;
        }
        return inventory.SelectKnifeWithDurability(Mathf.Max(1, configuration.finisherConsumption));
    }

    private bool CanPayGroundFinisher(GridItem knife)
    {
        if (knife == null || knife.item == null || !knife.item.usesDurability || configuration == null)
        {
            return false;
        }
        int remaining = knife.currentDurability;
        if (remaining < 0)
        {
            remaining = knife.item.maxDurability;
        }
        return remaining >= Mathf.Max(1, configuration.finisherConsumption);
    }

    internal void CommitEmergencyParryCost()
    {
        if (ValidKnife && configuration != null)
        {
            DeductDurability(configuration.normalParryConsumption);
        }
    }

    internal bool CommitGroundFinisherCost(int session, GridItem knife)
    {
        if (session <= 0 || session == lastGroundFinisherSession || !CanPayGroundFinisher(knife))
        {
            return false;
        }
        // 先标记接触，避免背包和断刀通知在同一调用栈内重复提交。
        lastGroundFinisherSession = session;
        if (!TryCommitFinisherCost(knife))
        {
            lastGroundFinisherSession = 0;
            return false;
        }
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
