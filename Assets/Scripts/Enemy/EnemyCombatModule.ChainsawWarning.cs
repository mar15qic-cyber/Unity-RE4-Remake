using GameArchitecture;
using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private ActionHandle chainsawThreatAction;
    private EnemyAttackWarningSnapshot chainsawWarning;
    private bool chainsawDefenseAccepted;
    private bool chainsawThreatConsumed;

    internal bool TryBeginChainsawThreat()
    {
        if (IsDead || IsQTEControlled || chainsawBridge == null || anim == null || anim.isHuman)
        {
            return false;
        }
        EnemyAIManager manager = EnemyAIManager.Instance;
        if (manager == null)
        {
            return false;
        }
        // 从配对候选名额交接到正式攻击名额，起手期间不锁定玩家。
        manager.CompleteQTEAttempt(host, QTEKind.ChainsawExecution, false);
        ActionResult result = ActionCoordinator.RequestAction(ActionKind.Attack, "电锯紧急格挡起手");
        if (!result.HasAccept)
        {
            return false;
        }
        chainsawThreatAction = result.Handle;
        chainsawWarning = default;
        chainsawDefenseAccepted = false;
        chainsawThreatConsumed = false;
        return true;
    }

    internal bool TryReadChainsawWarning(out EnemyAttackWarningSnapshot warning)
    {
        warning = chainsawWarning;
        return !IsDead && !IsQTEControlled && !attackFinished && !chainsawThreatConsumed
            && warning.IsCurrent(ActionCoordinator.CurrentHandle, Time.timeAsDouble);
    }

    private void UpdateChainsawWarning(float frame, int contactFrame)
    {
        if (!chainsawThreatAction.Valid || chainsawThreatAction != ActionCoordinator.CurrentHandle
            || chainsawThreatConsumed || frame > contactFrame)
        {
            chainsawWarning = default;
            return;
        }
        double remaining = (contactFrame - frame) / 60d;
        if (remaining > 0.75d)
        {
            chainsawWarning = default;
            return;
        }
        bool hasKnife = Player != null && Player.TryGetComponent<KnifeCombatModule>(out var knife) && knife.ValidKnife;
        chainsawWarning = new EnemyAttackWarningSnapshot(chainsawThreatAction, Time.timeAsDouble,
            Time.timeAsDouble + remaining + .05d, hasKnife);
    }

    internal bool SubmitChainsawDefense(LeonController player, ActionHandle action)
    {
        if (player == null || player != Player || action != chainsawThreatAction || chainsawDefenseAccepted
            || !TryReadChainsawWarning(out var warning) || !warning.CanDefend)
        {
            return false;
        }
        chainsawDefenseAccepted = true;
        return true;
    }

    private bool TryCommitChainsawThreatContact(LeonController player, Vector3 bladeCenter, Vector3 contactPoint)
    {
        if (!chainsawThreatAction.Valid || chainsawThreatAction != ActionCoordinator.CurrentHandle || chainsawThreatConsumed)
        {
            return false;
        }
        chainsawThreatConsumed = true;
        chainsawWarning = default;
        var target = host.GetComponent<VillagerQTEParticipant>();
        var coordinator = player.GetComponent<LeonQTECoordinator>();
        if (target == null || coordinator == null)
        {
            return false;
        }
        player.TryGetComponent<KnifeCombatModule>(out var knife);
        bool defended = chainsawDefenseAccepted && knife != null && knife.ValidKnife;
        if (!coordinator.TryStartChainsawContact(target, defended))
        {
            if (coordinator.LastRejectionReason != QTECancelReason.SpaceBlocked)
            {
                return false;
            }

            if (defended)
            {
                // 贴墙无法展开双人动作时，已确认的格挡仍按一次接触结算。
                knife.CommitEmergencyParryCost();
                if (player.TryGetComponent<LeonCombat>(out var combat))
                {
                    combat.PresentChainsawDefense(host, contactPoint, contactPoint - bladeCenter);
                }
            }
            else
            {
                player.TakeDamageFrom(player.currentHealth, bladeCenter, contactPoint);
                chainsawBridge.PlayChainsawHit();
            }
            return true;
        }
        if (defended)
        {
            knife.CommitEmergencyParryCost();
        }
        return true;
    }
}
