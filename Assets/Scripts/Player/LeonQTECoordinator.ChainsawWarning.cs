using GameArchitecture;
using UnityEngine;

public partial class LeonQTECoordinator
{
    private bool chainsawContactFailure;
    private VillagerQTEParticipant chainsawContactTarget;

    internal bool TryGetChainsawWarning(out VillagerQTEParticipant target, out EnemyAttackWarningSnapshot warning)
    {
        target = null;
        warning = default;
        if (PromptInputBlocked())
        {
            return false;
        }
        float best = float.MaxValue;
        foreach (var candidate in GetPromptCandidates())
        {
            if (candidate == null || candidate.controller == null || candidate.controller.combatModule == null
                || !candidate.controller.combatModule.TryReadChainsawWarning(out var current))
            {
                continue;
            }
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                target = candidate;
                warning = current;
            }
        }
        return target != null;
    }

    internal bool TrySubmitEmergencyDefense()
    {
        if (!TryGetChainsawWarning(out var target, out var warning))
        {
            return false;
        }
        return target.controller.combatModule.SubmitChainsawDefense(controller, warning.Action);
    }

    internal bool TryStartChainsawContact(VillagerQTEParticipant target, bool defended)
    {
        chainsawContactFailure = !defended;
        chainsawContactTarget = target;
        try
        {
            return TryStart(QTEKind.ChainsawExecution, target, out _);
        }
        finally
        {
            chainsawContactFailure = false;
            chainsawContactTarget = null;
        }
    }
}
