using GameArchitecture;
using UnityEngine;

public partial class VillagerQTEParticipant
{
    internal bool TryReadBodyTechniqueOpportunity(out int technique)
    {
        technique = bodyTechniqueIndex;
        if (!IsAvailable || !IsAlive || activeSessionId != 0 || activeBodyTechniqueSessionId != 0
            || !enableBodyTechniqueOpportunity || Time.time < nextBodyTechniqueAllowedTime)
        {
            return false;
        }
        if (UsesNativeBodyTechnique)
        {
            return TryReadNativeBodyTechnique(out _, out technique);
        }
        return bodyTechniqueOpportunityOpen && Time.time <= bodyTechniqueOpportunityExpiresAt;
    }
    internal bool TryReadPendingBodyTechnique(LeonController leon, out int technique)
    {
        technique = 0;
        if (leon == null || !IsAvailable || !IsAlive || !UsesNativeBodyTechnique
            || !enableBodyTechniqueOpportunity || activeSessionId != 0 || activeBodyTechniqueSessionId != 0
            || Time.time < nextBodyTechniqueAllowedTime
            || !controller.combatModule.TryReadNativeBodyReaction(out NativeBodyReaction reaction, out ActionHandle action)
            || action == consumedNativeBodyReaction || !HasBodyTechniqueApproach(leon, out _))
        {
            return false;
        }

        if (reaction is NativeBodyReaction.HeadStagger or NativeBodyReaction.PerfectParry)
        {
            technique = 1;
        }
        return true;
    }

    internal bool TryReadGroundFinisher(out string motion)
    {
        motion = null;
        if (!IsAvailable || !IsAlive || controller.missionEnemyType != MissionEnemyType.Ganado
            || controller.combatModule == null || controller.animationPresenter == null
            || activeSessionId != 0 || activeBodyTechniqueSessionId != 0)
        {
            return false;
        }

        if (controller.combatModule.HasGroundFinisherPose)
        {
            motion = controller.combatModule.GroundFinisherMotion;
            return !string.IsNullOrEmpty(motion);
        }

        if (controller.combatModule.TryReadNativeBodyReaction(out NativeBodyReaction reaction, out _)
            && (reaction == NativeBodyReaction.LeftKneel || reaction == NativeBodyReaction.RightKneel)
            && controller.animationPresenter.IsNativeBodyTechniquePoseReady(reaction))
        {
            motion = "Ground1035";
            return true;
        }
        return false;
    }

}
