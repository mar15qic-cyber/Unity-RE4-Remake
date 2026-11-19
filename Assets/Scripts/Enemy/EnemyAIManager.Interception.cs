using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public partial class EnemyAIManager
{
    private const float EscapeSpeedThreshold = 0.85f;
    private const float InterceptionPlanningInterval = 0.7f;
    private const float InterceptionStopGrace = 0.55f;
    private const float InterceptionRouteClearance = 1.65f;
    private const int MaximumInterceptionProbes = 4;

    private sealed class InterceptionPlan
    {
        public Vector3 origin;
        public Vector3 direction;
        public float playerSpeed;
        public float probeDeadline;
        public int graphVersion;
        public int remaining;
        public readonly Vector3[] points = new Vector3[2];
        public readonly bool[] available = new bool[2];
        public readonly List<InterceptionOffer> offers = new();
    }

    private struct InterceptionOffer
    {
        public VillagerController enemy;
        public int side;
        public float arrival;
        public Vector3 destination;
    }

    private readonly List<DoorController> escapeDoors = new();
    private readonly List<VillagerController> interceptionCandidates = new();
    private readonly VillagerController[] interceptionEnemies = new VillagerController[2];
    private readonly Dictionary<VillagerController, float> interceptionRetryAt = new();
    private InterceptionPlan pendingInterception;
    private InterceptionPlan currentInterception;
    private float nextInterceptionPlanAt;
    private float lastPlayerMovementAt;
    private float previousEscapeSampleAt;
    private Vector3 previousEscapePosition;
    private Vector3 escapeVelocity;

    private void ResetEscapeInterception()
    {
        pendingInterception = null;
        currentInterception = null;
        nextInterceptionPlanAt = 0f;
        lastPlayerMovementAt = float.NegativeInfinity;
        previousEscapeSampleAt = float.NegativeInfinity;
        escapeVelocity = Vector3.zero;
        interceptionEnemies[0] = null;
        interceptionEnemies[1] = null;
        interceptionRetryAt.Clear();
    }

    private void SampleEscapeVelocity()
    {
        LeonController player = PlayerTarget;
        if (!IsCombatUnlocked || player == null)
        {
            return;
        }

        Vector3 position = player.transform.position;
        Vector3 velocity = player.HorizontalSpeed;
        Vector3 offset = position - previousEscapePosition;
        offset.y = 0f;
        float elapsed = Time.time - previousEscapeSampleAt;
        if (elapsed > 0.02f && elapsed < 0.8f && offset.sqrMagnitude < 9f &&
            Mathf.Abs(position.y - previousEscapePosition.y) < 0.8f)
        {
            velocity = offset / elapsed;
        }

        previousEscapePosition = position;
        previousEscapeSampleAt = Time.time;
        if (Vector3.Dot(velocity, escapeVelocity) > 0f)
        {
            escapeVelocity = Vector3.Lerp(escapeVelocity, velocity, 0.65f);
        }
        else
        {
            escapeVelocity = velocity;
        }
    }

    private void UpdateEscapeInterception()
    {
        LeonController player = PlayerTarget;
        if (!IsCombatUnlocked || player == null || NavigationQuery == null || !NavigationQuery.CanQuery)
        {
            return;
        }

        Vector3 velocity = escapeVelocity;
        float speed = velocity.magnitude;
        if (speed < EscapeSpeedThreshold)
        {
            if (Time.time - lastPlayerMovementAt > InterceptionStopGrace)
            {
                ClearInterceptionPlans();
            }

            return;
        }

        lastPlayerMovementAt = Time.time;
        Vector3 direction = velocity / speed;
        Vector3 origin = player.transform.position;
        if (currentInterception != null && !IsEscapeDirectionCurrent(currentInterception, origin, direction))
        {
            ClearInterceptionPlans();
            nextInterceptionPlanAt = Time.time;
        }

        if (pendingInterception != null &&
            (Time.time > pendingInterception.probeDeadline || !IsEscapeDirectionCurrent(pendingInterception, origin, direction)))
        {
            pendingInterception = null;
        }

        if (Time.time < nextInterceptionPlanAt || pendingInterception != null)
        {
            return;
        }

        nextInterceptionPlanAt = Time.time + InterceptionPlanningInterval;
        bool hasLeft = TryGetInterceptionDestination(interceptionEnemies[0], out _);
        bool hasRight = TryGetInterceptionDestination(interceptionEnemies[1], out _);
        if (hasLeft && hasRight)
        {
            return;
        }

        if (hasLeft || hasRight)
        {
            var reinforcement = new InterceptionPlan
            {
                origin = origin,
                direction = direction,
                playerSpeed = speed,
                graphVersion = NavigationQuery.Version
            };
            for (int side = 0; side < 2; side++)
            {
                reinforcement.points[side] = currentInterception.points[side];
                reinforcement.available[side] = currentInterception.available[side] &&
                    Vector3.Dot(reinforcement.points[side] - origin, direction) > 1.5f;
            }

            ProbeInterceptionRoutes(reinforcement);
            return;
        }

        if (TryBuildInterceptionPlan(origin, direction, speed, out var plan))
        {
            ProbeInterceptionRoutes(plan);
        }
    }

    private void ClearInterceptionPlans()
    {
        pendingInterception = null;
        currentInterception = null;
        interceptionEnemies[0] = null;
        interceptionEnemies[1] = null;
    }

    private bool IsEscapeDirectionCurrent(InterceptionPlan plan, Vector3 origin, Vector3 direction)
    {
        return plan.graphVersion == NavigationQuery.Version &&
            Mathf.Abs(origin.y - plan.origin.y) < 0.8f &&
            Vector3.Dot(direction, plan.direction) >= 0.65f;
    }

    private bool TryBuildInterceptionPlan(Vector3 origin, Vector3 direction, float speed, out InterceptionPlan plan)
    {
        plan = null;
        AstarPath astar = AstarPath.active;
        if (astar == null)
        {
            return false;
        }

        NNInfo start = astar.GetNearest(origin, NearestNodeConstraint.Walkable);
        if (start.node == null || HorizontalDistance(origin, start.position) > 1.25f ||
            Mathf.Abs(origin.y - start.position.y) > 0.8f)
        {
            return false;
        }

        float lookAhead = Mathf.Clamp(speed * 2.15f + 2f, 6f, 11f);
        var candidate = new InterceptionPlan
        {
            origin = origin,
            direction = direction,
            playerSpeed = speed,
            graphVersion = NavigationQuery.Version
        };

        if (TryFindPassageAhead(astar, start, origin, direction, lookAhead, out var passage) &&
            FindSideApproaches(astar, start.node.Area, candidate, passage))
        {
            plan = candidate;
            return true;
        }

        for (float distance = lookAhead; distance >= 4f; distance -= 2f)
        {
            Vector3 sample = origin + direction * distance;
            if (TryProjectEscapePoint(astar, start.node.Area, sample, out var anchor) &&
                !astar.Linecast(start.position, anchor) && FindSideApproaches(astar, start.node.Area, candidate, anchor))
            {
                plan = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindPassageAhead(AstarPath astar, NNInfo start, Vector3 origin, Vector3 direction,
        float lookAhead, out Vector3 passage)
    {
        passage = default;
        float nearest = lookAhead + 1.5f;
        DoorController.CopyActiveSceneDoors(escapeDoors);
        foreach (var door in escapeDoors)
        {
            ConsiderPassage(astar, start, origin, direction, door.PassageCenter, ref passage, ref nearest);
        }

        if (preciseTraversal != null)
        {
            foreach (var traversal in preciseTraversal)
            {
                if (traversal == null || traversal.link == null || !traversal.link.isActiveAndEnabled)
                {
                    continue;
                }

                var link = traversal.link;
                ConsiderPassage(astar, start, origin, direction, link.StartTransform.position, ref passage, ref nearest);
                if (link.EndTransform != null)
                {
                    ConsiderPassage(astar, start, origin, direction, link.EndTransform.position, ref passage, ref nearest);
                }
            }
        }

        return nearest < lookAhead + 1.5f;
    }

    private static void ConsiderPassage(AstarPath astar, NNInfo start, Vector3 origin, Vector3 direction,
        Vector3 point, ref Vector3 passage, ref float nearest)
    {
        Vector3 offset = point - origin;
        float forward = Vector3.Dot(offset, direction);
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        if (forward < 3.5f || forward >= nearest || Mathf.Abs(Vector3.Dot(offset, side)) > 2f ||
            Mathf.Abs(offset.y) > 0.75f || !TryProjectEscapePoint(astar, start.node.Area, point, out var approach) ||
            astar.Linecast(start.position, approach))
        {
            return;
        }

        nearest = forward;
        passage = approach;
    }

    private static bool FindSideApproaches(AstarPath astar, uint area, InterceptionPlan plan, Vector3 anchor)
    {
        Vector3 right = Vector3.Cross(Vector3.up, plan.direction);
        bool hasApproach = false;
        for (int side = 0; side < 2; side++)
        {
            Vector3 outward = right * (side * 2 - 1);
            plan.available[side] = false;
            for (float width = 3f; width >= 1.5f; width -= 0.75f)
            {
                Vector3 sample = anchor + outward * width;
                if (!TryProjectEscapePoint(astar, area, sample, out var point) ||
                    Vector3.Dot(point - anchor, outward) < 1.35f ||
                    Vector3.Dot(point - plan.origin, plan.direction) < 3f ||
                    astar.Linecast(point, anchor))
                {
                    continue;
                }

                plan.points[side] = point;
                plan.available[side] = true;
                hasApproach = true;
                break;
            }
        }

        return hasApproach;
    }

    private static bool TryProjectEscapePoint(AstarPath astar, uint area, Vector3 target, out Vector3 point)
    {
        NNInfo nearest = astar.GetNearest(target, NearestNodeConstraint.Walkable);
        point = nearest.position;
        return nearest.node != null && nearest.node.Area == area &&
            HorizontalDistance(point, target) <= 1.15f && Mathf.Abs(point.y - target.y) <= 0.7f;
    }

    private void ProbeInterceptionRoutes(InterceptionPlan plan)
    {
        interceptionCandidates.Clear();
        foreach (var enemy in enemies)
        {
            if (CanIntercept(enemy))
            {
                interceptionCandidates.Add(enemy);
            }
        }

        interceptionCandidates.Sort((first, second) =>
            EstimateDistanceToInterception(first, plan).CompareTo(EstimateDistanceToInterception(second, plan)));
        int probeCount = Mathf.Min(MaximumInterceptionProbes, interceptionCandidates.Count);
        if (probeCount == 0)
        {
            return;
        }

        pendingInterception = plan;
        plan.remaining = probeCount;
        plan.probeDeadline = Time.time + 1f;
        for (int index = 0; index < probeCount; index++)
        {
            VillagerController enemy = interceptionCandidates[index];
            var targets = new List<Vector3>(2);
            var sides = new List<int>(2);
            for (int side = 0; side < 2; side++)
            {
                if (!plan.available[side] || !enemy.TryResolveReachableNavigationPoint(plan.points[side], out var projected) ||
                    HorizontalDistance(projected, plan.points[side]) > 1f ||
                    Mathf.Abs(enemy.transform.position.y - projected.y) > enemy.standingCombatVerticalTolerance ||
                    !IsFormationSlotOnPlayerSide(enemy.transform.position, projected))
                {
                    continue;
                }

                targets.Add(projected);
                sides.Add(side);
            }

            if (targets.Count == 0)
            {
                CompleteInterceptionProbe(plan);
                continue;
            }

            Vector3 origin = enemy.transform.position;
            MultiTargetPath request = MultiTargetPath.Construct(origin, targets.ToArray(), null,
                path => ReadInterceptionProbe(plan, enemy, origin, (MultiTargetPath)path, sides));
            AstarPath.StartPath(request);
        }
    }

    private static float EstimateDistanceToInterception(VillagerController enemy, InterceptionPlan plan)
    {
        float distance = float.PositiveInfinity;
        for (int side = 0; side < 2; side++)
        {
            if (plan.available[side])
            {
                distance = Mathf.Min(distance, HorizontalDistance(enemy.transform.position, plan.points[side]));
            }
        }

        return distance;
    }

    private void ReadInterceptionProbe(InterceptionPlan plan, VillagerController enemy, Vector3 origin,
        MultiTargetPath path, List<int> sides)
    {
        if (this == null || !isActiveAndEnabled || pendingInterception != plan)
        {
            return;
        }

        if (!path.error && CanIntercept(enemy))
        {
            for (int index = 0; index < sides.Count; index++)
            {
                int side = sides[index];
                Vector3 destination = path.targetPoints[index];
                if (path.vectorPaths[index] == null ||
                    !TryMeasureWalkableRoute(path.vectorPaths[index], origin, destination, plan.origin, out float length))
                {
                    continue;
                }

                float enemyArrival = length / EnemyContext.StandingChaseAgentSpeed;
                float playerArrival = Vector3.Dot(plan.points[side] - plan.origin, plan.direction) / plan.playerSpeed;
                if (enemyArrival <= playerArrival + 0.85f)
                {
                    plan.offers.Add(new InterceptionOffer
                    {
                        enemy = enemy,
                        side = side,
                        arrival = enemyArrival,
                        destination = destination
                    });
                }
            }
        }

        CompleteInterceptionProbe(plan);
    }

    private static bool TryMeasureWalkableRoute(IReadOnlyList<Vector3> nodes, Vector3 origin,
        Vector3 destination, Vector3 playerPosition, out float length)
    {
        length = 0f;
        AstarPath astar = AstarPath.active;
        if (astar == null)
        {
            return false;
        }

        var route = new List<Vector3>(nodes.Count + 2) { origin };
        // 用可直接通过的最远路段计算距离，消除大三角形节点中心造成的折返。
        int firstCandidate = 0;
        while (firstCandidate <= nodes.Count)
        {
            Vector3 from = route[route.Count - 1];
            int selected = -1;
            for (int index = nodes.Count; index >= firstCandidate; index--)
            {
                Vector3 candidate = destination;
                if (index < nodes.Count)
                {
                    candidate = nodes[index];
                }

                if (Mathf.Abs(candidate.y - playerPosition.y) > 0.85f ||
                    !IsSafeTacticalSegment(from, candidate, playerPosition, InterceptionRouteClearance) ||
                    astar.Linecast(from, candidate))
                {
                    continue;
                }

                route.Add(candidate);
                selected = index;
                break;
            }

            if (selected < firstCandidate)
            {
                return false;
            }

            firstCandidate = selected + 1;
        }

        return TryMeasureInterceptionRoute(route, playerPosition, out length);
    }

    internal static bool TryMeasureInterceptionRoute(IReadOnlyList<Vector3> path, Vector3 playerPosition, out float length)
    {
        length = 0f;
        if (path.Count == 0 || HorizontalDistance(path[0], playerPosition) < InterceptionRouteClearance)
        {
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            Vector3 origin = path[index - 1];
            Vector3 end = path[index];
            if (!IsSafeTacticalSegment(origin, end, playerPosition, InterceptionRouteClearance) ||
                Mathf.Abs(origin.y - playerPosition.y) > 0.85f || Mathf.Abs(end.y - playerPosition.y) > 0.85f)
            {
                return false;
            }

            length += Vector3.Distance(origin, end);
        }

        return true;
    }

    private void CompleteInterceptionProbe(InterceptionPlan plan)
    {
        plan.remaining--;
        if (plan.remaining > 0 || pendingInterception != plan)
        {
            return;
        }

        pendingInterception = null;
        LeonController player = PlayerTarget;
        if (player == null || escapeVelocity.magnitude < EscapeSpeedThreshold ||
            !IsEscapeDirectionCurrent(plan, player.transform.position, escapeVelocity.normalized))
        {
            return;
        }

        InterceptionOffer left = default;
        InterceptionOffer right = default;
        float bestScore = float.PositiveInfinity;
        // 同时挑选两个方向，防止同一个敌人被分给两个入口。
        for (int leftIndex = -1; leftIndex < plan.offers.Count; leftIndex++)
        {
            InterceptionOffer leftOffer = default;
            if (leftIndex >= 0)
            {
                leftOffer = plan.offers[leftIndex];
                if (leftOffer.side != 0 || !CanIntercept(leftOffer.enemy))
                {
                    continue;
                }
            }

            for (int rightIndex = -1; rightIndex < plan.offers.Count; rightIndex++)
            {
                InterceptionOffer rightOffer = default;
                if (rightIndex >= 0)
                {
                    rightOffer = plan.offers[rightIndex];
                    if (rightOffer.side != 1 || rightOffer.enemy == leftOffer.enemy || !CanIntercept(rightOffer.enemy))
                    {
                        continue;
                    }
                }

                float score = ScoreInterceptionOffer(leftOffer, 0) + ScoreInterceptionOffer(rightOffer, 1);
                if (score < bestScore)
                {
                    bestScore = score;
                    left = leftOffer;
                    right = rightOffer;
                }
            }
        }

        if (left.enemy == null && right.enemy == null)
        {
            return;
        }

        currentInterception = plan;
        if (left.enemy != null)
        {
            plan.points[0] = left.destination;
        }

        if (right.enemy != null)
        {
            plan.points[1] = right.destination;
        }

        SetInterceptionEnemy(0, left.enemy);
        SetInterceptionEnemy(1, right.enemy);
    }

    private float ScoreInterceptionOffer(InterceptionOffer offer, int side)
    {
        if (offer.enemy == null)
        {
            return 10f;
        }

        if (interceptionEnemies[side] == offer.enemy)
        {
            return offer.arrival - 0.5f;
        }

        return offer.arrival;
    }

    private void SetInterceptionEnemy(int side, VillagerController enemy)
    {
        bool changed = interceptionEnemies[side] != enemy;
        interceptionEnemies[side] = enemy;
        if (!changed || enemy == null)
        {
            return;
        }

        combatSlots.Remove(enemy);
        pursuitTargets.Remove(enemy);
        if (side == 0)
        {
            RecordMovement(enemy, tactics[enemy], TacticalMovement.InterceptLeft);
        }
        else
        {
            RecordMovement(enemy, tactics[enemy], TacticalMovement.InterceptRight);
        }

        if (EnemyCombatTimeline.IsRecord(enemy))
        {
            EnemyCombatTimeline.Record(enemy, "分配截击点", CombatDiagnosticFields.Position("路线目标", currentInterception.points[side]));
        }
    }

    private bool CanIntercept(VillagerController enemy)
    {
        if (!IsReservationCandidateValid(enemy) || !enemy.IsRuntimeInitialized || !enemy.ShouldChasePlayer ||
            enemy.NeedsCombatTraversal() || directPressureEnemies.Contains(enemy) || ShouldPressAtCloseRange(enemy))
        {
            return false;
        }

        return !interceptionRetryAt.TryGetValue(enemy, out float retryAt) || Time.time >= retryAt;
    }

    internal bool TryGetInterceptionDestination(VillagerController enemy, out Vector3 point)
    {
        point = default;
        LeonController player = PlayerTarget;
        if (currentInterception == null || player == null || enemy == null || NavigationQuery == null ||
            Time.time - lastPlayerMovementAt > InterceptionStopGrace ||
            currentInterception.graphVersion != NavigationQuery.Version)
        {
            return false;
        }

        for (int side = 0; side < interceptionEnemies.Length; side++)
        {
            if (interceptionEnemies[side] != enemy || !CanIntercept(enemy))
            {
                continue;
            }

            point = currentInterception.points[side];
            Vector3 offset = point - player.transform.position;
            return Mathf.Abs(offset.y) <= 0.8f && Vector3.Dot(offset, currentInterception.direction) > 0.55f;
        }

        return false;
    }

    internal void RejectInterception(VillagerController enemy)
    {
        ForgetInterception(enemy);
        interceptionRetryAt[enemy] = Time.time + 1f;
    }

    private void ForgetInterception(VillagerController enemy)
    {
        for (int side = 0; side < interceptionEnemies.Length; side++)
        {
            if (interceptionEnemies[side] == enemy)
            {
                interceptionEnemies[side] = null;
            }
        }

        interceptionRetryAt.Remove(enemy);
    }
}
