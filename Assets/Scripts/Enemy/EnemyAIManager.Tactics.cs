using System.Collections.Generic;
using GameArchitecture;
using UnityEngine;

public partial class EnemyAIManager
{
    internal enum TacticalMovement
    {
        Approach,
        Flank,
        Circle,
        Yield,
        Blocked,
        InterceptLeft,
        InterceptRight
    }

    private sealed class EnemyTactics
    {
        public bool released;
        public float waitingSince;
        public float nextAttackCandidateTime;
        public float nextRouteCheck;
        public float yieldUntil;
        public float failedUntil;
        public float nextOrbitReversal;
        public Vector3 failedTarget;
        public Vector3 retreatPoint;
        public Vector3 progressPosition;
        public float progressAt;
        public int orbitDirection = 1;
        public bool flankCompleted;
        public bool hasRetreatPoint;
        public TacticalMovement movement;
    }

    private const float EngagementBatchInterval = 0.6f;
    private const float MinimumAttackStartInterval = 0.8f;
    private const float ObstructedRetryInterval = 0.4f;
    private const float FailedTargetCooldown = 1f;
    private const float YieldDuration = 0.45f;

    private readonly Dictionary<VillagerController, EnemyTactics> tactics = new();
    private readonly Dictionary<VillagerController, ActionHandle> committedAttacks = new();
    private readonly List<VillagerController> invalidAttackOwners = new();
    private float nextEngagementBatch;
    private VillagerController selectedAttackCandidate;
    private int lastAttackReevaluationFrame = -1;
    private bool releasedFirstEngagement;

    private void ResetTacticalCombat()
    {
        nextEngagementBatch = Time.time;
        releasedFirstEngagement = false;
        selectedAttackCandidate = null;
        lastAttackReevaluationFrame = -1;
        ResetEscapeInterception();
        nextAttackAllowedTime = 0f;
        nextQTEPriorityAllowedTime = 0f;
        nextFrontGrabAllowedTime = 0f;
        nextLegBiteAllowedTime = 0f;
        nextChainsawExecutionAllowedTime = 0f;
        qteReservedEnemy = null;
        ClearPendingQTERequest();
        attackingEnemies.Clear();
        committedAttacks.Clear();
        directPressureEnemies.Clear();
        combatSlots.Clear();
        pursuitTargets.Clear();

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                tactics[enemy] = new EnemyTactics();
            }
        }
    }

    internal bool CanEngage(VillagerController enemy)
    {
        return IsCombatUnlocked && enemy != null && tactics.TryGetValue(enemy, out var state) && state.released;
    }

    internal void ReleaseEngagementOnHit(VillagerController enemy)
    {
        if (!IsCombatUnlocked || !IsEnemyUsable(enemy) || !tactics.TryGetValue(enemy, out var state) || state.released)
        {
            return;
        }

        state.released = true;
        state.waitingSince = Time.time;
        SynchronizePursuitRegionVersion(enemy);
    }

    private void ReleaseQueuedEngagements(int limit)
    {
        if (!IsCombatUnlocked || PlayerTarget == null || Time.time + 0.001f < nextEngagementBatch)
        {
            return;
        }

        Vector3 playerPosition = PlayerTarget.transform.position;
        if (!releasedFirstEngagement)
        {
            limit = 1;
        }

        for (int released = 0; released < limit; released++)
        {
            VillagerController nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var enemy in enemies)
            {
                if (!IsEnemyUsable(enemy) || !enemy.IsRuntimeInitialized || enemy.IsQTEControlled ||
                    !tactics.TryGetValue(enemy, out var state) || state.released)
                {
                    continue;
                }

                float distance = (enemy.transform.position - playerPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                return;
            }

            nextEngagementBatch = Time.time + EngagementBatchInterval;
            releasedFirstEngagement = true;
            ReleaseEngagementOnHit(nearest);
            nearest.ReceiveVillagePursuitOrder(playerPosition);
        }
    }

    internal bool ShouldFollowPlayerDirectly(VillagerController enemy)
    {
        return directPressureEnemies.Contains(enemy) || ShouldPressAtCloseRange(enemy);
    }

    private void SelectAttackApproach()
    {
        SelectRearPursuer();
        SelectAttackCandidate();
    }

    private void SelectRearPursuer()
    {
        if (!IsCombatUnlocked || PlayerTarget == null)
        {
            return;
        }

        Vector3 position = PlayerTarget.transform.position;
        Vector3 heading = escapeVelocity;
        bool moving = heading.sqrMagnitude > 0.64f;
        heading.Normalize();
        VillagerController selected = null;
        float bestScore = float.PositiveInfinity;
        foreach (var enemy in enemies)
        {
            if (!IsReservationCandidateValid(enemy) || !enemy.IsRuntimeInitialized || enemy.IsQTEControlled ||
                enemy.IsNavigationTraversalProtected || enemy.HasPendingHit)
            {
                continue;
            }

            EnemyTactics state = tactics[enemy];
            bool pursuing = directPressureEnemies.Contains(enemy);
            if (pursuing && enemy.ShouldChasePlayer && !ShouldPressAtCloseRange(enemy) &&
                !ObserveApproachProgress(enemy, state))
            {
                YieldAttackApproach(enemy);
                continue;
            }

            if (Time.time < state.yieldUntil)
            {
                continue;
            }

            Vector3 offset = enemy.transform.position - position;
            offset.y = 0f;
            float score = offset.magnitude;
            if (moving && Vector3.Dot(offset, heading) > 0.4f)
            {
                score += 12f;
            }

            if (moving && (interceptionEnemies[0] == enemy || interceptionEnemies[1] == enemy))
            {
                score += 4f;
            }

            if (enemy.NeedsCombatTraversal())
            {
                score += 8f;
            }

            if (pursuing)
            {
                score -= 2.5f;
            }

            if (score < bestScore)
            {
                selected = enemy;
                bestScore = score;
            }
        }

        if (selected == null)
        {
            directPressureEnemies.Clear();
            return;
        }

        if (directPressureEnemies.Contains(selected))
        {
            return;
        }

        directPressureEnemies.Clear();
        directPressureEnemies.Add(selected);
        EnemyTactics selectedState = tactics[selected];
        selectedState.progressPosition = selected.transform.position;
        selectedState.progressAt = Time.time;
        combatSlots.Remove(selected);
        pursuitTargets.Remove(selected);
        RecordMovement(selected, selectedState, TacticalMovement.Approach);
    }

    private void SelectAttackCandidate()
    {
        selectedAttackCandidate = null;
        if (!IsCombatUnlocked || PlayerTarget == null)
        {
            return;
        }

        Vector3 playerPosition = PlayerTarget.transform.position;
        Vector3 playerForward = PlayerTarget.transform.forward;
        playerForward.y = 0f;
        float bestScore = float.PositiveInfinity;
        foreach (var enemy in enemies)
        {
            if (!IsReadyAttackCandidate(enemy) || !CanOpenAttackSlot(enemy))
            {
                continue;
            }

            Vector3 offset = enemy.transform.position - playerPosition;
            offset.y = 0f;
            EnemyTactics state = tactics[enemy];
            float score = offset.magnitude - Mathf.Clamp((Time.time - state.waitingSince) * 0.18f, 0f, 2f);
            if (attackingEnemies.Count == 0 && Vector3.Angle(playerForward, offset) <= 60f)
            {
                score -= 2.5f;
            }

            if (score < bestScore)
            {
                selectedAttackCandidate = enemy;
                bestScore = score;
            }
        }
    }

    private bool IsReadyAttackCandidate(VillagerController enemy)
    {
        if (!IsReservationCandidateValid(enemy) || !enemy.IsRuntimeInitialized || enemy.IsQTEControlled ||
            enemy.IsNavigationTraversalProtected || attackingEnemies.Contains(enemy) ||
            Time.time < tactics[enemy].nextAttackCandidateTime)
        {
            return false;
        }

        return enemy.combatModule.CanStartStandingAttack() || enemy.combatModule.CanStartCrawlAttack();
    }

    internal bool ShouldPressAtCloseRange(VillagerController enemy)
    {
        LeonController player = PlayerTarget;
        if (!CanEngage(enemy) || player == null || enemy.combatModule == null ||
            Mathf.Abs(enemy.transform.position.y - player.transform.position.y) > enemy.standingCombatVerticalTolerance)
        {
            return false;
        }

        float pressRange = GetMovementExclusionRadius(enemy) + 1.05f;
        if (HorizontalDistance(enemy.transform.position, player.transform.position) > pressRange)
        {
            return false;
        }

        return enemy.combatModule.CanEngagePlayerAtCloseRange() ||
            (enemy.perceptionModule != null && enemy.perceptionModule.HasClearLineToPlayer(false, pressRange + 0.5f));
    }

    private static bool ObserveApproachProgress(VillagerController enemy, EnemyTactics state)
    {
        if (HorizontalDistance(enemy.transform.position, state.progressPosition) >= enemy.chaseProgressDistance)
        {
            state.progressPosition = enemy.transform.position;
            state.progressAt = Time.time;
        }

        return Time.time - state.progressAt < 1f;
    }

    internal void YieldAttackApproach(VillagerController enemy)
    {
        if (enemy == null || !tactics.TryGetValue(enemy, out var state))
        {
            return;
        }

        directPressureEnemies.Remove(enemy);
        pursuitTargets.Remove(enemy);
        combatSlots.Remove(enemy);
        state.nextRouteCheck = Time.time;
        state.yieldUntil = Time.time + YieldDuration;
        state.flankCompleted = true;
        RecordMovement(enemy, state, TacticalMovement.Yield);
    }

    private bool CanOpenAttackSlot(VillagerController enemy)
    {
        return GetAttackRestriction(enemy, true) == null;
    }

    private string GetAttackRestriction(VillagerController enemy, bool validateQTE)
    {
        bool assisting = PlayerTarget != null && PlayerTarget.TryGetComponent<LeonQTECoordinator>(out var activePair) &&
            activePair.IsRearHelper(enemy);
        if (!assisting && (qteReservedEnemy != null ||
            (PlayerTarget != null && PlayerTarget.TryGetComponent<LeonQTECoordinator>(out var qte) && qte.IsActive)))
        {
            return "配对动作独占许可";
        }

        bool pending = pendingQTEEnemy != null && Time.time <= pendingQTEExpiresAt;
        if (validateQTE)
        {
            pending = HasPendingQTERequest();
        }

        if (pending && !assisting)
        {
            return "抓取优先等待窗口";
        }

        if (Time.time < nextAttackAllowedTime)
        {
            return "全局攻击起手间隔";
        }

        if (PlayerTarget != null && PlayerTarget.currentState == LeonState.Staggered &&
            enemy.GetComponent<ChainsawManAnimationBridge>() == null)
        {
            return "等待玩家受击恢复";
        }

        if (attackingEnemies.Count >= Mathf.Clamp(maximumConcurrentAttackers, 1, 2))
        {
            return "攻击名额已满";
        }

        foreach (var attacker in attackingEnemies)
        {
            if (attacker != null && attacker.combatModule != null && attacker.combatModule.ReadStandingParryWindowOpen)
            {
                return "已有弹反窗口";
            }
        }

        if (!HasConcurrentAttackDirectionSpace(enemy))
        {
            return "攻击方向与现有攻击者重合";
        }

        return null;
    }

    private string DescribeAttackPermission(VillagerController enemy)
    {
        if (!IsCombatUnlocked)
        {
            return "入村战斗尚未解锁";
        }

        if (!CanEngage(enemy))
        {
            return "等待本批接战";
        }

        if (attackingEnemies.Contains(enemy))
        {
            return "本人正在攻击";
        }

        string restriction = GetAttackRestriction(enemy, false);
        if (restriction != null)
        {
            return restriction;
        }

        if (selectedAttackCandidate == enemy)
        {
            return "本次出招候选";
        }

        if (tactics[enemy].movement == TacticalMovement.Blocked)
        {
            return "外围走位缺少安全落脚点";
        }

        return "等待下次出招评估";
    }

    internal void ConfirmAttack(VillagerController enemy, ActionHandle handle)
    {
        if (!handle.Valid || !attackingEnemies.Contains(enemy))
        {
            return;
        }

        committedAttacks[enemy] = handle;
        float startInterval = MinimumAttackStartInterval;
        if (enemy.GetComponent<ChainsawManAnimationBridge>() == null)
        {
            startInterval = 1.25f;
        }

        nextAttackAllowedTime = Time.time + Mathf.Max(startInterval, attackRecoveryDelay);
        tactics[enemy].waitingSince = Time.time;
        selectedAttackCandidate = null;
        if (EnemyCombatTimeline.IsRecord(enemy))
        {
            EnemyCombatTimeline.Record(enemy, "错峰攻击起手", CombatDiagnosticFields.Count("当前攻击数", attackingEnemies.Count));
        }
    }

    internal void CancelAttackStart(VillagerController enemy)
    {
        attackingEnemies.Remove(enemy);
        selectedAttackCandidate = null;
        if (tactics.TryGetValue(enemy, out var state))
        {
            state.nextAttackCandidateTime = Time.time + 0.3f;
        }
    }

    internal void ReleaseAttack(VillagerController enemy, ActionHandle handle)
    {
        if (!committedAttacks.TryGetValue(enemy, out var current) || current != handle)
        {
            return;
        }

        committedAttacks.Remove(enemy);
        attackingEnemies.Remove(enemy);
    }

    private void ValidateAttackHandles()
    {
        invalidAttackOwners.Clear();
        foreach (var entry in committedAttacks)
        {
            VillagerController enemy = entry.Key;
            if (!IsReservationCandidateValid(enemy) || enemy.IsQTEControlled || enemy.AttackFinished ||
                enemy.actionCoordinator == null || enemy.actionCoordinator.CurrentHandle != entry.Value)
            {
                invalidAttackOwners.Add(enemy);
            }
        }

        foreach (var enemy in invalidAttackOwners)
        {
            ReleaseAttack(enemy, committedAttacks[enemy]);
        }
    }

    private void FinishQTEWait()
    {
        ClearPendingQTERequest();
        nextQTEPriorityAllowedTime = Time.time + MinimumAttackStartInterval;
    }

    internal bool TryReadTacticalMovement(VillagerController enemy, out TacticalMovement movement)
    {
        movement = TacticalMovement.Approach;
        if (!CanEngage(enemy) || ShouldPressAtCloseRange(enemy) || directPressureEnemies.Contains(enemy) ||
            TryGetInterceptionDestination(enemy, out _) || enemy.NeedsCombatTraversal())
        {
            return false;
        }

        movement = tactics[enemy].movement;
        return true;
    }

    private void InvalidateTacticalDestination(VillagerController enemy)
    {
        if (combatSlots.TryGetValue(enemy, out var assignment) && tactics.TryGetValue(enemy, out var state))
        {
            RejectTacticalPoint(state, assignment.destination);
        }

        combatSlots.Remove(enemy);
        if (directPressureEnemies.Contains(enemy))
        {
            YieldAttackApproach(enemy);
        }
    }

    internal void RejectCurrentTacticalRoute(VillagerController enemy)
    {
        InvalidateTacticalDestination(enemy);
    }

    internal static float GetMovementExclusionRadius(VillagerController enemy)
    {
        return Mathf.Max(enemy.attackStartRange, enemy.combatModule.StandingAttackStartRange);
    }

    private static void RejectTacticalPoint(EnemyTactics state, Vector3 point)
    {
        state.failedTarget = point;
        state.failedUntil = Time.time + FailedTargetCooldown;
        state.nextRouteCheck = Time.time;
    }

    private Vector3 GetTacticalDestination(VillagerController enemy, Vector3 playerPosition)
    {
        EnemyTactics state = tactics[enemy];
        PathAgent path = enemy.Agent;
        Vector3 origin = enemy.transform.position;
        bool hasAssignment = combatSlots.TryGetValue(enemy, out var assignment);
        float exclusionRadius = GetMovementExclusionRadius(enemy);
        float arrival = Mathf.Max(path.waypointReachedRadius, path.arrivedDestinationChangeThreshold);
        if (hasAssignment && path.HasDestination && enemy.HasChasePathFailure &&
            HorizontalDistance(path.Destination, assignment.destination) <= arrival)
        {
            RejectTacticalPoint(state, assignment.destination);
            combatSlots.Remove(enemy);
            hasAssignment = false;
        }

        bool anchorValid = hasAssignment &&
            HorizontalDistance(assignment.playerAnchor, playerPosition) < surroundSlotPlayerMoveThreshold &&
            Mathf.Abs(assignment.playerAnchor.y - playerPosition.y) < pursuitTargetPlayerHeightThreshold;
        if (!anchorValid && hasAssignment)
        {
            state.nextRouteCheck = Time.time;
            combatSlots.Remove(enemy);
        }

        if (anchorValid && HorizontalDistance(origin, assignment.destination) > arrival + 0.15f &&
            IsSafeTacticalSegment(origin, assignment.destination, playerPosition, exclusionRadius))
        {
            return assignment.destination;
        }

        if (anchorValid && assignment.isFlankStaging && assignment.slotIndex >= 0)
        {
            state.flankCompleted = true;
        }

        if (Time.time < state.nextRouteCheck)
        {
            if (anchorValid)
            {
                return assignment.destination;
            }

            return origin;
        }

        state.nextRouteCheck = Time.time + Mathf.Min(0.2f, updateInterval);
        float radius = Mathf.Max(surroundDistance, exclusionRadius + arrival + 0.35f);
        float distance = HorizontalDistance(origin, playerPosition);
        if (distance > tacticalFlankRadius + formationDistanceHysteresis + 1f)
        {
            state.flankCompleted = false;
        }

        int currentSlot = GetAngleSlot(origin - playerPosition);
        TacticalMovement movement = TacticalMovement.Circle;
        if (Time.time < state.yieldUntil || distance < exclusionRadius + arrival)
        {
            movement = TacticalMovement.Yield;
        }
        else if (!state.flankCompleted && distance > tacticalFlankActivationDistance)
        {
            movement = TacticalMovement.Flank;
        }

        if (TrySelectOrbitPoint(enemy, state, playerPosition, radius, currentSlot, movement, out var point, out int slot) ||
            TrySelectLocalStep(enemy, state, playerPosition, out point))
        {
            combatSlots[enemy] = new CombatSlotAssignment
            {
                playerAnchor = playerPosition,
                destination = point,
                slotIndex = slot,
                isFlankStaging = movement == TacticalMovement.Flank
            };
            state.retreatPoint = origin;
            state.hasRetreatPoint = true;
            RecordMovement(enemy, state, movement);
            return point;
        }

        if (anchorValid && HorizontalDistance(origin, assignment.destination) > arrival &&
            IsSafeTacticalSegment(origin, assignment.destination, playerPosition, exclusionRadius))
        {
            return assignment.destination;
        }

        combatSlots.Remove(enemy);
        state.nextRouteCheck = Time.time + ObstructedRetryInterval;
        RecordMovement(enemy, state, TacticalMovement.Blocked);
        return origin;
    }

    private bool TrySelectOrbitPoint(VillagerController enemy, EnemyTactics state, Vector3 playerPosition,
        float radius, int currentSlot, TacticalMovement movement, out Vector3 point, out int slot)
    {
        point = default;
        slot = -1;
        if (movement == TacticalMovement.Flank)
        {
            float flankRadius = Mathf.Clamp(tacticalFlankRadius, radius, maximumTacticalFlankActivationDistance);
            int bestCoverage = int.MaxValue;
            for (int step = 1; step <= 4; step++)
            {
                for (int side = 0; side < 2; side++)
                {
                    int direction = state.orbitDirection;
                    if (side == 1)
                    {
                        direction = -direction;
                    }

                    int index = WrapSlot(currentSlot + direction * step);
                    int coverage = CountSectorAssignments(enemy, index, playerPosition);
                    if (coverage >= bestCoverage ||
                        !TryResolveSlot(enemy, state, playerPosition, flankRadius, index, out var candidate, true))
                    {
                        continue;
                    }

                    slot = index;
                    point = candidate;
                    bestCoverage = coverage;
                }
            }

            return slot >= 0;
        }

        int next = WrapSlot(currentSlot + state.orbitDirection);
        if (TryResolveSlot(enemy, state, playerPosition, radius, next, out point) ||
            TryResolveSlot(enemy, state, playerPosition, radius + 1.2f, next, out point))
        {
            slot = next;
            return true;
        }

        int reverse = WrapSlot(currentSlot - state.orbitDirection);
        if (Time.time >= state.nextOrbitReversal && TryResolveSlot(enemy, state, playerPosition, radius, reverse, out point))
        {
            state.orbitDirection = -state.orbitDirection;
            state.nextOrbitReversal = Time.time + FailedTargetCooldown;
            slot = reverse;
            return true;
        }

        return false;
    }

    private bool TrySelectLocalStep(VillagerController enemy, EnemyTactics state, Vector3 playerPosition, out Vector3 point)
    {
        Vector3 origin = enemy.transform.position;
        Vector3 outward = origin - playerPosition;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f)
        {
            outward = -enemy.transform.forward;
            outward.y = 0f;
        }

        outward.Normalize();
        Vector3 tangent = Vector3.Cross(Vector3.up, outward) * state.orbitDirection;
        if (TryResolveTacticalPoint(enemy, state, playerPosition, origin + tangent * 0.95f, out point) ||
            TryResolveTacticalPoint(enemy, state, playerPosition, origin + outward * 0.9f + tangent * 0.55f, out point) ||
            TryResolveTacticalPoint(enemy, state, playerPosition, origin + outward * 0.9f - tangent * 0.55f, out point))
        {
            return true;
        }

        if (outward.sqrMagnitude > 0.001f &&
            HorizontalDistance(origin, playerPosition) > tacticalFlankActivationDistance &&
            TryResolveTacticalPoint(enemy, state, playerPosition, origin - outward * 1.2f + tangent * 0.55f, out point))
        {
            return true;
        }

        return state.hasRetreatPoint && TryResolveTacticalPoint(enemy, state, playerPosition, state.retreatPoint, out point);
    }

    private bool TryResolveSlot(VillagerController enemy, EnemyTactics state, Vector3 playerPosition,
        float radius, int index, out Vector3 point, bool allowLongRoute = false)
    {
        Vector3 direction = Quaternion.Euler(0f, 360f * index / surroundSlotCount, 0f) * Vector3.forward;
        Vector3 rawPoint = playerPosition + direction * radius;
        return TryResolveTacticalPoint(enemy, state, playerPosition, rawPoint, out point, allowLongRoute);
    }

    private bool TryResolveTacticalPoint(VillagerController enemy, EnemyTactics state, Vector3 playerPosition,
        Vector3 rawPoint, out Vector3 point, bool allowLongRoute = false)
    {
        point = default;
        Vector3 origin = enemy.transform.position;
        if (!enemy.TryResolveReachableNavigationPoint(rawPoint, out var resolved) ||
            HorizontalDistance(rawPoint, resolved) > 0.45f ||
            Mathf.Abs(resolved.y - origin.y) > pursuitTargetPlayerHeightThreshold ||
            !IsFormationSlotOnPlayerSide(playerPosition, resolved) ||
            HorizontalDistance(origin, resolved) < Mathf.Max(0.35f, enemy.Agent.waypointReachedRadius + 0.15f) ||
            (Time.time < state.failedUntil && HorizontalDistance(state.failedTarget, resolved) < 0.5f) ||
            !IsSafeTacticalSegment(origin, resolved, playerPosition, GetMovementExclusionRadius(enemy)))
        {
            return false;
        }

        foreach (var entry in combatSlots)
        {
            if (entry.Key != enemy && IsEnemyUsable(entry.Key) &&
                Mathf.Abs(entry.Value.destination.y - resolved.y) < pursuitTargetPlayerHeightThreshold &&
                HorizontalDistance(entry.Value.destination, resolved) < minimumSlotSeparation)
            {
                return false;
            }
        }

        bool needsLocalClearance = !allowLongRoute || HorizontalDistance(origin, playerPosition) <= maximumTacticalFlankActivationDistance;
        if (needsLocalClearance && !enemy.CanMoveDirectlyToNavigationPoint(resolved))
        {
            return false;
        }

        point = resolved;
        return true;
    }

    internal static bool IsSafeTacticalSegment(Vector3 origin, Vector3 end, Vector3 playerPosition, float attackRange)
    {
        Vector3 startOffset = origin - playerPosition;
        Vector3 segment = end - origin;
        startOffset.y = 0f;
        segment.y = 0f;
        float startRadius = startOffset.magnitude;
        if (startRadius < attackRange)
        {
            return Vector3.Dot(startOffset, segment) >= 0f &&
                (startOffset + segment).magnitude > startRadius + 0.1f;
        }

        float along = Mathf.Clamp01(-Vector3.Dot(startOffset, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
        return (startOffset + segment * along).sqrMagnitude >= attackRange * attackRange;
    }

    private int CountSectorAssignments(VillagerController enemy, int slot, Vector3 playerPosition)
    {
        int sector = slot * 4 / surroundSlotCount;
        int count = 0;
        foreach (var entry in combatSlots)
        {
            if (entry.Key == enemy || !IsEnemyUsable(entry.Key) || entry.Value.slotIndex < 0 ||
                HorizontalDistance(entry.Value.playerAnchor, playerPosition) > tacticalFlankPlayerMoveThreshold)
            {
                continue;
            }

            if (entry.Value.slotIndex * 4 / surroundSlotCount == sector)
            {
                count++;
            }
        }

        return count;
    }

    private int GetAngleSlot(Vector3 offset)
    {
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        return WrapSlot(Mathf.RoundToInt(angle * surroundSlotCount / 360f));
    }

    private int WrapSlot(int slot)
    {
        return (slot % surroundSlotCount + surroundSlotCount) % surroundSlotCount;
    }

    private static void RecordMovement(VillagerController enemy, EnemyTactics state, TacticalMovement movement)
    {
        if (state.movement == movement)
        {
            return;
        }

        state.movement = movement;
        if (EnemyCombatTimeline.IsRecord(enemy))
        {
            string description;
            switch (movement)
            {
                case TacticalMovement.Approach:
                    description = "近身进攻";
                    break;
                case TacticalMovement.Flank:
                    description = "侧翼接近";
                    break;
                case TacticalMovement.Circle:
                    description = "外围绕行";
                    break;
                case TacticalMovement.Yield:
                    description = "侧移让位";
                    break;
                case TacticalMovement.InterceptLeft:
                    description = "左路截击";
                    break;
                case TacticalMovement.InterceptRight:
                    description = "右路截击";
                    break;
                default:
                    description = "无安全落脚点";
                    break;
            }

            EnemyCombatTimeline.Record(enemy, "战术走位切换", CombatDiagnosticFields.Text("走位", description));
        }
    }
}
