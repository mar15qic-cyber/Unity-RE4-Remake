using GameArchitecture;
using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private const float StandingContactPause = .03f;
    private const float StandingContactRecovery = .11f;
    private const float StandingForecastStep = 1f / 60f;

    internal struct StandingImpact
    {
        internal EnemyCombatModule attacker;
        internal ActionHandle action;
        internal long attackId;
        internal ObjectIdentity targetIdentity;
        internal LeonController leon;
        internal VillagerController ally;
        internal int segment;
        internal HitboxType part;
        internal Vector3 point;
        internal Vector3 normal;
        internal Vector3 origin;
        internal Vector3 playerPosition;
        internal Vector3 playerForward;
        internal Vector3 wristOffset;
        internal float axisFraction;
        internal float clipTime;
        internal float fraction;
        internal double time;
        internal bool wall;
        internal bool Found => wall || leon != null || ally != null;
    }

    private readonly RaycastHit[] standingWallHits = new RaycastHit[24];
    private readonly Collider[] standingWallOverlaps = new Collider[24];
    private StandingMeleeBody standingAllyBody;
    private StandingMeleeBody standingLeonBody;
    private LeonController standingLeon;
    private Transform[] standingBones;
    private MeleeCapsule[] standingPrevious;
    private MeleeCapsule[] standingCurrent;
    private KnifeEnemyAttackProfile standingPoseProfile;
    private bool standingImpactPending;
    private int standingReachFrame = -1;
    private int standingReachMask;
    private ActionHandle standingReboundAction;
    private double standingReboundStart;
    private Vector3 standingWristTarget;
    private Vector3 standingReboundNormal;

    private StandingMeleeBody GetStandingLeonBody(LeonController player, bool refresh)
    {
        if (standingLeon != player || standingLeonBody == null)
        {
            standingLeon = player;
            standingLeonBody = StandingMeleeBody.ForLeon(player);
        }

        standingLeonBody.Capture(player.transform.position, refresh);
        return standingLeonBody;
    }

    private StandingMeleeBody GetStandingAllyBody()
    {
        if (standingAllyBody == null)
        {
            standingAllyBody = StandingMeleeBody.ForVillager(host);
        }

        standingAllyBody.Capture(transform.position);
        return standingAllyBody;
    }

    internal bool CanReachWithStandingPose()
    {
        KnifeEnemyAttackProfile[] profiles = ActiveKnifeAttackProfiles;
        if (chainsawBridge != null || profiles == null || profiles.Length == 0
            || profiles[0] == null || !profiles[0].usePoseContact)
        {
            return true;
        }

        LeonController player = Player;
        if (player == null)
        {
            return false;
        }

        for (int index = 0; index < Mathf.Min(profiles.Length, standingAttackAnimationCount); index++)
        {
            if (CanReachWithStandingPose(index, player))
            {
                return true;
            }
        }

        return false;
    }

    internal float StandingApproachRange
    {
        get
        {
            float range = StandingAttackStartRange;
            if (Player != null && HorizontalSwingDistance(Player) <= range && !CanReachWithStandingPose())
            {
                return Mathf.Min(range, .65f);
            }

            return range;
        }
    }

    private bool CanReachWithStandingPose(int index, LeonController player)
    {
        KnifeEnemyAttackProfile[] profiles = ActiveKnifeAttackProfiles;
        if (profiles[index] == null)
        {
            return false;
        }

        if (!profiles[index].usePoseContact)
        {
            return true;
        }

        if (standingReachFrame != Time.frameCount)
        {
            standingReachFrame = Time.frameCount;
            standingReachMask = 0;
            Vector3 offset = player.transform.position - transform.position;
            Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (planar.sqrMagnitude > .0001f)
            {
                Quaternion facing = Quaternion.LookRotation(planar, Vector3.up);
                StandingMeleeBody body = GetStandingLeonBody(player, false);
                int count = Mathf.Min(Mathf.Min(profiles.Length, standingAttackAnimationCount), 30);
                for (int move = 0; move < count; move++)
                {
                    KnifeEnemyAttackProfile profile = profiles[move];
                    if (profile != null && profile.usePoseContact && planar.magnitude <= profile.maximumStartDistance
                        && PredictStandingImpact(profile, player, body, facing, 0f, float.PositiveInfinity, false).leon == player)
                    {
                        standingReachMask |= 1 << move;
                    }
                }
            }
        }

        return (standingReachMask & (1 << index)) != 0;
    }

    private void UpdateStandingMelee(KnifeEnemyAttackProfile profile, AnimatorStateInfo state, LeonController player, KnifeCombatModule knife)
    {
        if (standingImpactPending || normalSwingHitHasRecoverWindow)
        {
            return;
        }

        float clipTime = Mathf.Clamp01(state.normalizedTime) * profile.sourceClip.length;
        double now = Time.timeAsDouble;
        Vector3 currentPlayerPosition = player.transform.position;
        StandingMeleeBody body = GetStandingLeonBody(player, true);
        EnsureStandingPoseBindings(profile);
        CaptureStandingArms(profile);

        float rate = Mathf.Abs(anim.speed * state.speed * state.speedMultiplier);
        bool continuous = knifeTimelineSampled && clipTime >= previousKnifeClipTime && now > previousKnifeWorldTime;
        if (continuous && clipTime > previousKnifeClipTime)
        {
            rate = (clipTime - previousKnifeClipTime) / (float)(now - previousKnifeWorldTime);
        }

        var earliest = new StandingImpact { clipTime = float.PositiveInfinity };
        float lastEnd = 0f;
        bool canHitPlayer = IsPlayerWithinCombatVerticalRange(player);
        for (int index = 0; index < standingBones.Length; index++)
        {
            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            lastEnd = Mathf.Max(lastEnd, segment.end);
            if ((knifeContactMask & (1 << index)) != 0 || standingCurrent[index].radius <= 0f)
            {
                continue;
            }

            bool canSweep = continuous && clipTime > previousKnifeClipTime && standingPrevious[index].radius > 0f;
            if (!canSweep && (knifeTimelineSampled || clipTime < segment.begin || clipTime > segment.end))
            {
                continue;
            }

            float begin = clipTime;
            if (canSweep)
            {
                begin = Mathf.Max(previousKnifeClipTime, segment.begin);
            }

            float end = Mathf.Min(clipTime, segment.end);
            if (end < begin)
            {
                continue;
            }

            float from = 1f;
            float to = 1f;
            MeleeCapsule first = standingCurrent[index];
            MeleeCapsule last = first;
            if (canSweep)
            {
                from = Mathf.InverseLerp(previousKnifeClipTime, clipTime, begin);
                to = Mathf.InverseLerp(previousKnifeClipTime, clipTime, end);
                first = MeleeCapsule.Interpolate(standingPrevious[index], standingCurrent[index], from);
                last = MeleeCapsule.Interpolate(standingPrevious[index], standingCurrent[index], to);
            }

            StandingImpact impact = QueryStandingImpact(first, last, player, body, from, to, canHitPlayer, true);
            if (impact.wall)
            {
                knifeContactMask |= 1 << index;
                continue;
            }

            if (impact.Found)
            {
                impact.clipTime = Mathf.Lerp(begin, end, impact.fraction);
                if (impact.clipTime < earliest.clipTime)
                {
                    float frameFraction = Mathf.Lerp(from, to, impact.fraction);
                    impact.segment = index;
                    impact.time = now;
                    impact.origin = transform.position;
                    impact.playerPosition = currentPlayerPosition;
                    impact.playerForward = player.transform.forward;
                    if (canSweep)
                    {
                        impact.time = previousKnifeWorldTime + (now - previousKnifeWorldTime) * frameFraction;
                        impact.origin = Vector3.Lerp(previousKnifeFrame.origin, impact.origin, frameFraction);
                        impact.playerPosition = Vector3.Lerp(previousKnifeFrame.playerPosition, impact.playerPosition, frameFraction);
                        impact.playerForward = Quaternion.Slerp(previousKnifeFrame.playerRotation, player.transform.rotation, frameFraction) * Vector3.forward;
                    }

                    MeleeCapsule attack = standingCurrent[index];
                    impact.wristOffset = impact.point + impact.normal * attack.radius
                        - Vector3.Lerp(attack.start, attack.end, impact.axisFraction);
                    earliest = impact;
                }
            }
        }

        if (earliest.Found)
        {
            earliest.attacker = this;
            earliest.action = ActionCoordinator.CurrentHandle;
            earliest.attackId = KnifeIncomingAttackId;
            standingImpactPending = true;
            if (EnemyAIManager.HasInstance)
            {
                EnemyAIManager.Instance.QueueStandingImpact(earliest);
            }
            else
            {
                ResolveStandingImpact(earliest);
            }
        }
        else if (knife != null && knife.Ready && enableKnifeParry && profile.canParry && rate > .0001f && canHitPlayer)
        {
            float lead = profile.promptEarly;
            if (!host.TryGetComponent<EnemyHeldWeapon>(out var heldWeapon) || !heldWeapon.IsHeld)
            {
                lead = Mathf.Max(lead, .34f);
            }

            float window = Mathf.Min(knife.configuration.EffectiveTapWindow, lead);
            StandingImpact forecast = PredictStandingImpact(profile, player, body, transform.rotation, clipTime, clipTime + rate * window, true);
            if (forecast.leon == player && forecast.clipTime > clipTime)
            {
                KnifeIncomingAttackContext prompt = CreateStandingContext(forecast);
                prompt.expectedContactTime = now + (forecast.clipTime - clipTime) / rate;
                knife.RegisterIncomingAttackPrompt(prompt);
            }
        }

        if (clipTime >= lastEnd && !standingImpactPending)
        {
            normalSwingHitHasRecoverWindow = true;
        }

        knifeTimelineSampled = true;
        previousKnifeClipTime = clipTime;
        previousKnifeWorldTime = now;
        previousKnifeFrame.origin = transform.position;
        previousKnifeFrame.playerPosition = currentPlayerPosition;
        previousKnifeFrame.playerRotation = player.transform.rotation;
        var swap = standingPrevious;
        standingPrevious = standingCurrent;
        standingCurrent = swap;
    }

    private void EnsureStandingPoseBindings(KnifeEnemyAttackProfile profile)
    {
        if (standingPoseProfile == profile)
        {
            return;
        }

        standingPoseProfile = profile;
        int count = Mathf.Min(30, profile.contactSegments.Length);
        standingBones = new Transform[count];
        standingPrevious = new MeleeCapsule[count];
        standingCurrent = new MeleeCapsule[count];
        for (int index = 0; index < count; index++)
        {
            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            if (!string.IsNullOrEmpty(segment.contactBonePath))
            {
                standingBones[index] = anim.transform.Find(segment.contactBonePath);
            }
        }
    }

    private void CaptureStandingArms(KnifeEnemyAttackProfile profile)
    {
        for (int index = 0; index < standingBones.Length; index++)
        {
            Transform bone = standingBones[index];
            standingCurrent[index] = default;
            if (bone == null)
            {
                continue;
            }

            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            float radius = segment.radius + Mathf.Clamp(profile.contactPadding, 0f, .05f);
            standingCurrent[index] = new MeleeCapsule(bone.TransformPoint(segment.contactLocalStart),
                bone.TransformPoint(segment.contactLocalEnd), radius);
        }
    }

    private StandingImpact PredictStandingImpact(KnifeEnemyAttackProfile profile, LeonController player, StandingMeleeBody body,
        Quaternion facing, float startTime, float endTime, bool includeAllies)
    {
        var result = new StandingImpact { clipTime = float.PositiveInfinity };
        Vector3 rootAtStart = SampleKnifeRoot(profile, startTime);
        Vector3 separation = player.transform.position - transform.position;
        float horizontal = Vector3.ProjectOnPlane(separation, Vector3.up).magnitude;
        float slope = Mathf.Clamp(separation.y / Mathf.Max(.35f, horizontal), -.55f, .55f);
        for (int index = 0; index < profile.contactSegments.Length && index < 30; index++)
        {
            KnifeEnemyAttackSegment segment = profile.contactSegments[index];
            if (segment.localEndPoints.Length != segment.localPoints.Length || segment.localPoints.Length < 2
                || (includeAllies && (knifeContactMask & (1 << index)) != 0))
            {
                continue;
            }

            float begin = Mathf.Max(segment.begin, startTime);
            float end = Mathf.Min(segment.end, endTime);
            if (end <= begin || begin >= result.clipTime)
            {
                continue;
            }

            Vector3 travel = facing * (SampleKnifeRoot(profile, end) - rootAtStart);
            // 预判使用根位移已有的碰撞预算，实际命中直接读取骨骼世界位置。
            float allowed = host.PreviewStandingAttackDisplacement(travel).magnitude;
            float cursor = begin;
            MeleeCapsule first = PredictStandingCapsule(profile, segment, facing, rootAtStart, allowed, slope, cursor);
            while (cursor < end && cursor < result.clipTime)
            {
                float next = Mathf.Min(end, cursor + StandingForecastStep);
                MeleeCapsule last = PredictStandingCapsule(profile, segment, facing, rootAtStart, allowed, slope, next);
                StandingImpact impact = QueryStandingImpact(first, last, player, body, 1f, 1f, true, includeAllies, includeAllies);
                if (impact.Found)
                {
                    if (!impact.wall)
                    {
                        float contactTime = Mathf.Lerp(cursor, next, impact.fraction);
                        if (contactTime < result.clipTime)
                        {
                            impact.clipTime = contactTime;
                            impact.segment = index;
                            impact.origin = transform.position;
                            impact.playerPosition = player.transform.position;
                            impact.playerForward = player.transform.forward;
                            result = impact;
                        }
                    }

                    break;
                }

                cursor = next;
                first = last;
            }
        }

        return result;
    }

    private MeleeCapsule PredictStandingCapsule(KnifeEnemyAttackProfile profile, KnifeEnemyAttackSegment segment,
        Quaternion facing, Vector3 rootAtStart, float allowed, float slope, float time)
    {
        Vector3 offset = Vector3.ClampMagnitude(SampleKnifeRoot(profile, time) - rootAtStart, allowed);
        Vector3 origin = transform.position + facing * offset + Vector3.up * (offset.z * slope);
        Vector3 scale = transform.lossyScale;
        float sample = Mathf.InverseLerp(segment.begin, segment.end, time) * (segment.localEndPoints.Length - 1);
        int left = Mathf.Min(Mathf.FloorToInt(sample), segment.localEndPoints.Length - 2);
        Vector3 end = Vector3.Lerp(segment.localEndPoints[left], segment.localEndPoints[left + 1], sample - left);
        return new MeleeCapsule(origin + facing * Vector3.Scale(segment.Sample(time), scale),
            origin + facing * Vector3.Scale(end, scale), segment.radius + Mathf.Clamp(profile.contactPadding, 0f, .05f));
    }

    private StandingImpact QueryStandingImpact(MeleeCapsule first, MeleeCapsule last, LeonController player, StandingMeleeBody body,
        float bodyFrom, float bodyTo, bool canHitPlayer, bool includeAllies, bool checkEmptySweep = true)
    {
        var result = new StandingImpact { fraction = 1.00001f };
        float time = result.fraction;
        if (canHitPlayer && body.Sweep(first, last, bodyFrom, bodyTo, ref time, out Vector3 point, out Vector3 normal,
            out float axis, out HitboxType part))
        {
            result.leon = player;
            result.fraction = time;
            result.point = point;
            result.normal = normal;
            result.part = part;
            result.axisFraction = axis;
        }

        if (includeAllies && EnemyAIManager.HasInstance)
        {
            foreach (VillagerController ally in EnemyAIManager.Instance.StandingContactEnemies)
            {
                if (ally == null || ally == host || !ally.isActiveAndEnabled || ally.IsDead || ally.IsQTEControlled
                    || ally.combatModule == null || Mathf.Abs(ally.transform.position.y - transform.position.y) > enemyFriendlyFireHeightTolerance
                    || (ally.transform.position - first.start).sqrMagnitude > 9f)
                {
                    continue;
                }

                time = result.fraction;
                StandingMeleeBody allyBody = ally.combatModule.GetStandingAllyBody();
                if (allyBody.Sweep(first, last, bodyFrom, bodyTo, ref time, out point, out normal, out axis, out part))
                {
                    result.leon = null;
                    result.ally = ally;
                    result.targetIdentity = ally.actionCoordinator.Identity;
                    result.fraction = time;
                    result.point = point;
                    result.normal = normal;
                    result.part = part;
                    result.axisFraction = axis;
                }
            }
        }

        bool wallInSweep = (checkEmptySweep || result.Found)
            && StandingWallBlocks(first, last, Mathf.Min(1f, result.fraction));
        if (wallInSweep || (result.Found && StandingContactIsOccluded(result.point)))
        {
            return new StandingImpact { wall = true };
        }

        return result;
    }

    private bool StandingWallBlocks(MeleeCapsule first, MeleeCapsule last, float until)
    {
        int overlaps = Physics.OverlapCapsuleNonAlloc(first.start, first.end, first.radius, standingWallOverlaps,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Collider[] colliders = standingWallOverlaps;
        if (overlaps == colliders.Length)
        {
            colliders = Physics.OverlapCapsule(first.start, first.end, first.radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            overlaps = colliders.Length;
        }

        for (int index = 0; index < overlaps; index++)
        {
            if (IsStandingWall(colliders[index]))
            {
                return true;
            }
        }

        for (int index = 0; index <= 2; index++)
        {
            float axis = index * .5f;
            Vector3 start = Vector3.Lerp(first.start, first.end, axis);
            Vector3 delta = (Vector3.Lerp(last.start, last.end, axis) - start) * until;
            float distance = delta.magnitude;
            if (distance <= .0001f)
            {
                continue;
            }

            RaycastHit[] hits = standingWallHits;
            int count = Physics.SphereCastNonAlloc(start, first.radius, delta / distance, hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(start, first.radius, delta / distance, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            for (int hit = 0; hit < count; hit++)
            {
                if (IsStandingWall(hits[hit].collider))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsStandingWall(Collider collider)
    {
        return collider != null && collider.GetComponentInParent<VillagerController>() == null
            && collider.GetComponentInParent<LeonController>() == null;
    }

    private bool StandingContactIsOccluded(Vector3 point)
    {
        // 起手时斧刃可能已绕过薄墙。接触侧还需要与攻击者躯干连通。
        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 delta = point - origin;
        float distance = delta.magnitude;
        if (distance <= .001f)
        {
            return false;
        }

        Vector3 direction = delta / distance;
        RaycastHit[] hits = standingWallHits;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == hits.Length)
        {
            hits = Physics.RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int index = 0; index < count; index++)
        {
            if (IsStandingWall(hits[index].collider))
            {
                return true;
            }
        }

        return false;
    }

    internal void ResolveStandingImpact(StandingImpact impact)
    {
        if (impact.attackId != KnifeIncomingAttackId)
        {
            return;
        }

        standingImpactPending = false;
        if (!ActionCoordinator.CallbackValid(impact.action) || attackFinished || CurrentState != VillagerState.Attack)
        {
            return;
        }

        knifeContactMask |= 1 << impact.segment;
        attackHitMask |= 1 << impact.segment;
        if (impact.leon != null && impact.leon.currentState != LeonState.Dead)
        {
            if (rearAssistSession > 0 || impact.leon.currentState == LeonState.QTE)
            {
                if (impact.leon.TryGetComponent<LeonQTECoordinator>(out var pair))
                {
                    pair.CommitRearHelperHit(host, rearAssistSession, attackDamage, impact.point, impact.normal);
                }
                BeginStandingRebound(impact);
                return;
            }
            var knife = impact.leon.GetComponent<KnifeCombatModule>();
            if (knife != null && knife.Ready)
            {
                KnifeIncomingAttackContext contact = CreateStandingContext(impact);
                contact.standingImpact = impact;
                knife.ResolveIncomingAttack(contact);
                return;
            }

            BeginStandingRebound(impact);
            impact.leon.TakeMeleeContactDamage(attackDamage, impact.origin, impact.point, impact.normal);
        }
        else if (impact.ally != null && impact.ally.actionCoordinator.Identity == impact.targetIdentity)
        {
            BeginStandingRebound(impact);
            if (!impact.ally.IsDead && !impact.ally.IsQTEControlled && enableEnemyFriendlyFire
                && enemyFriendlyFireMaxTargets != 0 && enemyFriendlyFireDamageMultiplier > 0f)
            {
                impact.ally.combatModule.TakeDamage(attackDamage * enemyFriendlyFireDamageMultiplier,
                    impact.point, impact.part, impact.normal);
                if (EnemyHitEffectManager.Instance != null)
                {
                    // 吃到友方武器命中时，命中声由进攻方的武器表现播放。
                    EnemyHitEffectManager.Instance.PlayBloodEffect(impact.point, impact.normal, impact.part, playImpactSound: false);
                }
            }
        }
    }

    private KnifeIncomingAttackContext CreateStandingContext(StandingImpact impact)
    {
        var context = CreateKnifeIncomingAttack(impact.segment, attackDamage, impact.point, transform.forward);
        context.contactTime = impact.time;
        context.attackOrigin = impact.origin;
        context.hasAttackOrigin = true;
        context.playerPositionAtContact = impact.playerPosition;
        context.hasPlayerPositionAtContact = true;
        context.playerForwardAtContact = impact.playerForward;
        context.hasPlayerFacingAtContact = true;
        KnifeEnemyAttackSegment segment = CurrentKnifeConfiguration.contactSegments[impact.segment];
        context.segmentBegin = segment.begin;
        context.segmentEnd = segment.end;
        context.animationTime = impact.clipTime;
        context.contactEvidence = "实际姿态胶囊";
        return context;
    }

    internal void BeginStandingRebound(StandingImpact impact)
    {
        if (!ActionCoordinator.CallbackValid(impact.action) || impact.attackId != KnifeIncomingAttackId || normalSwingHitHasRecoverWindow)
        {
            return;
        }

        HandleOnAttackComplete();
        StopAndClearMovement();
        if (host.TryGetComponent(out EnemyHeldWeapon weapon))
        {
            weapon.PlayHitAudio(impact.point);
        }

        KnifeEnemyAttackSegment segment = CurrentKnifeConfiguration.contactSegments[impact.segment];
        Vector3 offset = impact.wristOffset + impact.normal * .012f;
        if (!AnimationPresenter.TryPrepareMeleeReboundArm(segment.hand, offset, out standingWristTarget))
        {
            return;
        }

        standingReboundAction = impact.action;
        standingReboundStart = Time.timeAsDouble;
        standingReboundNormal = impact.normal;
        if (host.knifeLocalHitStop != null)
        {
            host.knifeLocalHitStop.Pause(StandingContactPause);
        }

        ApplyStandingRebound(0f);
    }

    private void UpdateStandingRebound()
    {
        if (!standingReboundAction.Valid)
        {
            return;
        }

        float time = (float)(Time.timeAsDouble - standingReboundStart);
        if (!ActionCoordinator.CallbackValid(standingReboundAction) || time >= StandingContactRecovery)
        {
            ClearStandingRebound();
            return;
        }

        ApplyStandingRebound(time);
    }

    private void ApplyStandingRebound(float time)
    {
        float progress = Mathf.InverseLerp(StandingContactPause, StandingContactRecovery, time);
        AnimationPresenter.ApplyMeleeReboundArm(standingWristTarget + standingReboundNormal * (.08f * progress), 1f - progress);
    }

    private void ClearStandingRebound()
    {
        if (standingReboundAction.Valid && host != null)
        {
            if (AnimationPresenter != null)
            {
                AnimationPresenter.ClearMeleeReboundArm();
            }

            if (host.knifeLocalHitStop != null && Time.timeAsDouble - standingReboundStart < StandingContactPause)
            {
                host.knifeLocalHitStop.Restore();
            }
        }

        standingReboundAction = default;
    }

    private void ClearStandingMelee()
    {
        ClearStandingRebound();
        standingPoseProfile = null;
        standingImpactPending = false;
        standingReachFrame = -1;
    }
}
