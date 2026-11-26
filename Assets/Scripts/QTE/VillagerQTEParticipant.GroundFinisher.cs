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

    // 电锯男仅在真实的单膝跪地硬直姿势下开放跪姿小刀补刀（SawKneel1020）；站立硬直仅开放体术。
    private int sawKnifeHitSession;
    private static readonly int SawKneelLeft = Animator.StringToHash("Base Layer.HitReact.Hit_NativeKneelLeft");
    private static readonly int SawKneelRight = Animator.StringToHash("Base Layer.HitReact.Hit_NativeKneelRight");

    internal bool TryReadGroundFinisher(out string motion)
    {
        motion = null;
        if (!IsAvailable || !IsAlive || controller == null
            || controller.combatModule == null || controller.animationPresenter == null
            || activeSessionId != 0 || activeBodyTechniqueSessionId != 0)
        {
            return false;
        }

        if (controller.missionEnemyType == MissionEnemyType.Sawman)
        {
            if (controller.CurrentState != VillagerState.Staggered || animator == null || animator.IsInTransition(0))
            {
                return false;
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            int hash = state.fullPathHash;
            bool isLeftKneel = hash == SawKneelLeft;
            bool isRightKneel = hash == SawKneelRight;
            if (!isLeftKneel && !isRightKneel)
            {
                return false;
            }

            float maxNormalizedTime = 0.74f;
            if (isRightKneel)
            {
                maxNormalizedTime = 0.76f;
            }
            if (state.normalizedTime < 0.09f || state.normalizedTime > maxNormalizedTime)
            {
                return false;
            }
            motion = "SawKneel1020";
            return true;
        }
        if (controller.missionEnemyType != MissionEnemyType.Ganado)
        {
            return false;
        }
        if (controller.combatModule.TryReadTrappedOpportunity(out _))
        {
            motion = "Ground1035";
            return true;
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
