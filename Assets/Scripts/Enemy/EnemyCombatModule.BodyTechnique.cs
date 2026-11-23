using GameArchitecture;
using UnityEngine;

internal enum NativeBodyReaction
{
    None,
    HeadStagger,
    LeftKneel,
    RightKneel,
    PerfectParry
}

internal struct FirearmBodyTechniqueShot
{
    internal struct Hit
    {
        internal float damage;
        internal Vector3 point;
        internal Vector3 normal;
        internal HitboxType part;
    }

    internal ActionHandle action;
    internal ObjectIdentity target;
    internal Hit strongest;
    internal Hit rightArm;
    internal Hit collapse;
    internal float head;
    internal float leftLeg;
    internal float rightLeg;
    internal bool pistol;
    internal bool shotgun;
    internal int pellets;
    internal float totalDamage;
    internal Vector3 sourcePosition;

    internal void Add(float damage, Vector3 point, Vector3 normal, HitboxType part, bool causedCollapse)
    {
        pellets++;
        totalDamage += damage;
        var hit = new Hit { damage = damage, point = point, normal = normal, part = part };
        if (damage > strongest.damage)
        {
            strongest = hit;
        }

        if (causedCollapse)
        {
            collapse = hit;
        }

        switch (part)
        {
            case HitboxType.Head:
                head += damage * 1.4f;
                break;
            case HitboxType.LeftLeg:
                leftLeg += damage * .7f;
                break;
            case HitboxType.RightLeg:
                rightLeg += damage * .7f;
                break;
            case HitboxType.RightArm:
                if (damage > rightArm.damage)
                {
                    rightArm = hit;
                }
                break;
        }
    }
}

public sealed partial class EnemyCombatModule
{
    internal const int NativeLeftKneelHitDirection = 11;
    internal const int NativeRightKneelHitDirection = 12;
    internal const int NativeLeftLegFlinchDirection = 13;
    internal const int NativeRightLegFlinchDirection = 14;

    [SerializeField, Tooltip("枪械头部命中累积到此值时进入可接体术的失衡。")]
    private float headBodyTechniqueThreshold = 30f;
    [SerializeField, Tooltip("枪械命中单条腿累积到此值时进入可接体术的跪地。")]
    private float legBodyTechniqueThreshold = 16f;

    private NativeBodyReaction pendingNativeBodyReaction;
    private NativeBodyReaction currentNativeBodyReaction;
    private ActionHandle nativeBodyReactionAction;
    private ActionHandle lastBodyTechniqueShot;
    internal const float StandingHitRecoveryBlend = .24f;
    private ActionHandle standingHitRecoveryAction;
    private float standingHitRecoveryEndTime;

    internal bool StandingHitRecoveryActive => standingHitRecoveryAction.Valid
        && ActionCoordinator.CallbackValid(standingHitRecoveryAction);
    internal bool HasUnfinishedStandingHit => CurrentState == VillagerState.Staggered
        && pendingHit && !hitFinished && !pendingCrawl;

    private bool TryBeginStandingHitRecovery()
    {
        if (!HasUnfinishedStandingHit || IsQTEControlled)
        {
            return false;
        }

        bool resumePursuit = WillPursueAfterHit() && host.healthModule.LifePhase == EnemyLifePhase.Alive
            && !host.IsNavigationTraversalProtected && host.PlayerDistance > StandingApproachRange;
        if (!AnimationPresenter.TryBlendStandingHitToLocomotion(StandingHitRecoveryBlend, resumePursuit))
        {
            return false;
        }

        standingHitRecoveryAction = ActionCoordinator.CurrentHandle;
        standingHitRecoveryEndTime = Time.time + StandingHitRecoveryBlend;
        SetMovementMode(EnemyMovementMode.FullyStopped);
        return true;
    }

    private bool UpdateNativeStandingHitRecovery()
    {
        if (!StandingHitRecoveryActive || !HasUnfinishedStandingHit || IsQTEControlled)
        {
            standingHitRecoveryAction = default;
            return false;
        }

        bool blendFinished = Time.time >= standingHitRecoveryEndTime && AnimationPresenter.StandingHitBlendCompleted;
        if (blendFinished || Time.time >= standingHitRecoveryEndTime + .5f)
        {
            standingHitRecoveryAction = default;
            AnimationPresenter.SetBoolean(HashHIT, false);
            hitFinished = true;
        }
        return true;
    }
    private Vector3 bodyTechniquePoise;
    private Vector3 bodyTechniquePoiseTime;

    internal bool UsesNativeBodyTechnique => host != null && AnimationPresenter != null && AnimationPresenter.HasNativeBodyTechnique;
    internal bool UsesArmedNativeBodyTechnique => standingWeaponAttackStartRange > 0f;

    internal bool HasNativeBodyTechniqueApproach(LeonController player, float distance)
    {
        return IsPlayerWithinCombatVerticalRange(player) && HasClearLineToPlayer(false, distance);
    }

    internal void TakeNativeFirearmDamage(float damage, Vector3 point, HitboxType part, Vector3 normal, out bool collapse)
    {
        ApplyIncomingDamage(damage, point, part, normal, true, out collapse);
    }

    internal void CompleteBodyTechniqueShot(FirearmBodyTechniqueShot shot)
    {
        if (!shot.action.Valid || shot.action == lastBodyTechniqueShot || shot.target != ActionCoordinator.Identity
            || shot.strongest.damage <= 0f || IsQTEControlled)
        {
            return;
        }

        lastBodyTechniqueShot = shot.action;
        var traversal = host.ArchitectureReadSpecialTraversal;
        if (traversal != null && traversal.DeferLadderGroundReaction)
        {
            if (!IsDead)
            {
                traversal.ReactToLadderFirearmShot(shot.pistol, shot.head > 0f);
            }
            return;
        }

        if (IsDead)
        {
            TryBeginTerminalFirearmKnockdown(shot);
            return;
        }
        if (traversal != null && traversal.IsWindowTraversalActive)
        {
            bool knockdown = shot.collapse.damage > 0f || accumulatedPoiseDamage >= ResolveHitReactionThreshold();
            if (!knockdown)
            {
                knockdown = TrySelectNativeFirearmReaction(shot, out _);
            }
            if (knockdown && traversal.TryInterruptInOpening(shot.sourcePosition, out Vector3 fallSource)
                && BeginNativeKnockdown(fallSource, false, 1f, true))
            {
                return;
            }
            if (traversal.IsWindowTraversalActive)
            {
                return;
            }
        }
        if (NativeCombatActive || TryBeginFirearmKnockdown(shot))
        {
            return;
        }
        if (shot.collapse.damage > 0f)
        {
            permanentCrawl = true;
            pendingTripOver = true;
            ScheduleDamageReaction(shot.collapse.damage, shot.collapse.point, shot.collapse.part, shot.collapse.normal, true);
            ActionCoordinator.RequestAction(ActionKind.HitReaction, "枪械命中后倒地");
            return;
        }

        bool canDisarm = shot.rightArm.damage > 0f && standingWeaponAttackStartRange > 0f && !pendingCrawl
            && accumulatedPoiseDamage >= ResolveHitReactionThreshold();
        if (canDisarm)
        {
            if (pendingHitDirection != ArmedDisarmHitDirection || !pendingHit)
            {
                hitReactionImmuneUntil = Time.time;
                ScheduleDamageReaction(shot.rightArm.damage, shot.rightArm.point, shot.rightArm.part, shot.rightArm.normal, false);
                ActionCoordinator.RequestAction(ActionKind.HitReaction, "枪械命中后缴械");
            }
            return;
        }

        if (pendingCrawl || (pendingHit && pendingHitDirection == ArmedDisarmHitDirection)
            || pendingNativeBodyReaction != NativeBodyReaction.None
            || (currentNativeBodyReaction != NativeBodyReaction.None && ActionCoordinator.CallbackValid(nativeBodyReactionAction)))
        {
            return;
        }

        if (!crawlPose && !permanentCrawl && TrySelectNativeFirearmReaction(shot, out NativeBodyReaction reaction))
        {
            pendingStandingStagger = false;
            pendingStandingStaggerBodyTechnique = false;
            pendingStaggerDirection = 0;
            pendingStaggerLevel = 0;
            pendingHitDirection = 0;
            if (reaction == NativeBodyReaction.LeftKneel)
            {
                pendingHitDirection = NativeLeftKneelHitDirection;
            }
            else if (reaction == NativeBodyReaction.RightKneel)
            {
                pendingHitDirection = NativeRightKneelHitDirection;
            }

            pendingNativeBodyReaction = reaction;
            MarkDamageReactionPending(false);
            ActionCoordinator.RequestAction(ActionKind.HitReaction, "枪械命中后失衡");
            return;
        }

        FirearmBodyTechniqueShot.Hit strongest = shot.strongest;
        ScheduleDamageReaction(strongest.damage, strongest.point, strongest.part, strongest.normal, false);
    }

    private bool TrySelectNativeFirearmReaction(FirearmBodyTechniqueShot shot, out NativeBodyReaction reaction)
    {
        var contributions = new Vector3(shot.head, shot.leftLeg, shot.rightLeg);
        if (shot.pistol && nativeCombatProfile != null)
        {
            contributions.x *= nativeCombatProfile.pistolHeadStaggerMultiplier;
        }
        float highest = 0f;
        int selected = -1;
        for (int index = 0; index < 3; index++)
        {
            float contribution = contributions[index];
            if (contribution <= 0f)
            {
                continue;
            }

            float decayTime = Mathf.Max(0f, Time.time - bodyTechniquePoiseTime[index] - host.poiseDecayDelay);
            bodyTechniquePoise[index] = Mathf.Max(0f, bodyTechniquePoise[index] - decayTime * host.poiseDecayPerSecond) + contribution;
            bodyTechniquePoiseTime[index] = Time.time;
            float threshold = legBodyTechniqueThreshold;
            if (index == 0)
            {
                threshold = headBodyTechniqueThreshold;
            }

            if (HealthModule.LifePhase == EnemyLifePhase.Revived)
            {
                threshold *= 2f;
            }
            threshold = Mathf.Max(.01f, threshold);
            float score = contribution / threshold;
            if (bodyTechniquePoise[index] >= threshold && score > highest)
            {
                highest = score;
                selected = index;
            }
        }

        reaction = NativeBodyReaction.None;
        if (selected < 0)
        {
            return false;
        }

        bodyTechniquePoise[selected] = 0f;
        reaction = (NativeBodyReaction)((int)NativeBodyReaction.HeadStagger + selected);
        return true;
    }

    internal bool TryReadNativeBodyReaction(out NativeBodyReaction reaction, out ActionHandle action)
    {
        reaction = currentNativeBodyReaction;
        action = nativeBodyReactionAction;
        return reaction != NativeBodyReaction.None && CurrentState == VillagerState.Staggered && !hitFinished
            && !pendingCrawl && ActionCoordinator.CallbackValid(action);
    }

    private void ClearNativeBodyReaction()
    {
        pendingNativeBodyReaction = NativeBodyReaction.None;
        currentNativeBodyReaction = NativeBodyReaction.None;
        nativeBodyReactionAction = default;
    }
}
