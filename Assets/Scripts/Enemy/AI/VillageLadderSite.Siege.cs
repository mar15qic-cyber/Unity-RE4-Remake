using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

public sealed partial class VillageLadderSite
{
    private VillageSiegeSequence siegeOwner;
    private VillagerController siegeActor;
    private float siegePriorityUntil;
    private bool siegeRaiseAllowed;
    private bool siegeMountAllowed;
    private int siegePathVersion;
    private int siegePendingPaths;
    private float nextSiegeSearch;
    private float siegeBestLength;
    private List<Vector3> siegeRoute;
    private VillagerController siegeBestActor;
    private readonly List<VillagerController> siegeCandidates = new();
    private readonly HashSet<VillagerController> siegeBlockedActors = new();

    internal static VillageLadderSite FindWindowLadder(WindowTraversalPoint window)
    {
        if (window == null)
        {
            return null;
        }
        foreach (var site in sites)
        {
            if (site.window == window && site.isActiveAndEnabled)
            {
                return site;
            }
        }
        return null;
    }

    internal bool TryReserveSiegeClimber(VillageSiegeSequence owner)
    {
        if (awaitingSiege || !IsWindow || State != LadderState.Down || assigned != null || activeEnemy != null ||
            climbingPlayer != null || pushTransaction != 0 || !HasExitSupport(Exit) || AstarPath.active == null ||
            siegeActor != null || siegePendingPaths != 0 || Time.unscaledTime < nextSiegeSearch)
        {
            return false;
        }

        var player = group.PlayerTarget;
        if (player == null || ReadTargetRegion(navigation, player.transform.position) != VillageTraversalRegion.FirstFloorIndoors)
        {
            return false;
        }

        var destination = AstarPath.active.GetNearest(Approach, NearestNodeConstraint.Walkable);
        if (destination.node == null || (destination.position - Approach).sqrMagnitude > .75f * .75f)
        {
            return false;
        }
        nextSiegeSearch = Time.unscaledTime + .4f;
        siegeCandidates.Clear();
        foreach (var candidate in group.StandingContactEnemies)
        {
            if (!CanStageSiegeApproach(candidate) || siegeBlockedActors.Contains(candidate))
            {
                continue;
            }

            if (candidate.TryGetComponent(out EnemyHeldWeapon weapon) && weapon.IsHeld)
            {
                continue;
            }
            siegeCandidates.Add(candidate);
        }

        if (siegeCandidates.Count == 0)
        {
            return false;
        }
        CancelSelection();
        siegeOwner = owner;
        siegeRaiseAllowed = false;
        siegeMountAllowed = false;
        siegeBestLength = 12f;
        siegeBestActor = null;
        siegeRoute = null;
        siegeCandidates.Sort((left, right) => (left.transform.position - Approach).sqrMagnitude
            .CompareTo((right.transform.position - Approach).sqrMagnitude));
        int version = ++siegePathVersion;
        int count = Mathf.Min(siegeCandidates.Count, 4);
        siegePendingPaths = count;
        for (int index = 0; index < count; index++)
        {
            var actor = siegeCandidates[index];
            var action = actor.actionCoordinator.CurrentHandle;
            var path = ABPath.Construct(actor.transform.position, destination.position,
                result => ReceiveSiegeRoute(owner, version, actor, action, result));
            AstarPath.StartPath(path);
        }
        return true;
    }

    private void ReceiveSiegeRoute(VillageSiegeSequence owner, int version, VillagerController actor,
        GameArchitecture.ActionHandle action, Path path)
    {
        if (siegeOwner != owner || siegePathVersion != version || siegePendingPaths <= 0 || !isActiveAndEnabled)
        {
            return;
        }
        if (!path.error && actor != null && actor.actionCoordinator.CurrentHandle == action &&
            CanStageSiegeApproach(actor) && path.vectorPath.Count > 0)
        {
            if (actor.TryGetComponent(out FunnelModifier smoothing))
            {
                smoothing.Apply(path);
            }
            float length = Vector3.Distance(actor.transform.position, path.vectorPath[0]) + path.GetTotalLength() +
                Vector3.Distance(path.vectorPath[path.vectorPath.Count - 1], Approach);
            if (length <= siegeBestLength)
            {
                siegeBestLength = length;
                siegeBestActor = actor;
                siegeRoute = new List<Vector3>(path.vectorPath);
                siegeRoute.Add(Approach);
            }
        }

        siegePendingPaths--;
        if (siegePendingPaths != 0 || siegeRaiseAllowed || State != LadderState.Down ||
            activeEnemy != null || !CanStageSiegeApproach(siegeBestActor))
        {
            return;
        }
        var player = group.PlayerTarget;
        if (player == null || ReadTargetRegion(navigation, player.transform.position) != VillageTraversalRegion.FirstFloorIndoors)
        {
            return;
        }
        group.ReleaseEngagementOnHit(siegeBestActor);
        siegeBestActor.ReceiveVillagePursuitOrder(player.transform.position);
        if (CanAssign(siegeBestActor))
        {
            siegeActor = siegeBestActor;
            SetAssignment(siegeActor, Assignment.Raise);
        }
    }

    private bool CanStageSiegeApproach(VillagerController actor)
    {
        if (actor == null || !actor.IsActive || !actor.IsRuntimeInitialized || actor.IsDead || actor.IsQTEControlled ||
            !actor.isActiveAndEnabled || actor.IsNavigationTraversalProtected || actor.HasPendingHit ||
            actor.GetComponent<ChainsawManAnimationBridge>() != null || actor.ArchitectureReadSpecialTraversal == null ||
            GetAssignment(actor) != null || actor.CurrentState is not
                (VillagerState.Idle or VillagerState.Patrol or VillagerState.Alert or VillagerState.Chase))
        {
            return false;
        }
        Vector3 difference = Approach - actor.transform.position;
        return difference.sqrMagnitude <= 12f * 12f && Mathf.Abs(difference.y) <= 1.5f &&
            navigation.TreadRegion(actor.transform.position) == VillageTraversalRegion.HouseOuterGround &&
            CanUseSiegeExit(actor);
    }

    private bool CanUseSiegeExit(VillagerController actor)
    {
        return !window.IsPassageReservedByOther(this) && HasExitSupport(Exit) &&
            IsExitClear(actor.GetComponent<CapsuleCollider>(), actor.transform);
    }

    internal IReadOnlyList<Vector3> ReadSiegeRoute(VillagerController actor)
    {
        if (IsSiegeActor(actor))
        {
            return siegeRoute;
        }
        return null;
    }

    internal void OpenSiegeAction(VillageSiegeSequence owner, bool mount)
    {
        if (siegeOwner != owner || siegeRaiseAllowed && siegeMountAllowed == mount)
        {
            return;
        }

        siegeRaiseAllowed = true;
        siegeMountAllowed = mount;
        if (siegeActor != null && !CanUseSiegeExit(siegeActor))
        {
            var blockedActor = siegeActor;
            RejectSiegeApproach(blockedActor);
            blockedActor.ArchitectureReadSpecialTraversal.CancelSiegeLadder(this);
            ClearAssignment(blockedActor, "围攻窗梯出口被挡");
        }
    }

    internal bool IsSiegeActor(VillagerController actor) => siegeOwner != null && siegeActor == actor;

    internal bool CanPerformSiegeRaise(VillagerController actor) => IsSiegeActor(actor) && siegeRaiseAllowed;

    internal void RejectSiegeApproach(VillagerController actor)
    {
        if (IsSiegeActor(actor))
        {
            siegeBlockedActors.Add(actor);
            siegeActor = null;
            siegeRoute = null;
        }
    }

    internal bool HasSiegeActor(VillageSiegeSequence owner)
    {
        return siegeOwner == owner && siegeActor != null && !siegeActor.IsDead &&
            (activeEnemy == siegeActor || assigned == siegeActor);
    }

    internal void EndSiege(VillageSiegeSequence owner, bool resume)
    {
        if (siegeOwner == owner)
        {
            var actor = siegeActor;
            siegeOwner = null;
            siegeActor = null;
            siegePathVersion++;
            siegePendingPaths = 0;
            siegeRoute = null;
            siegeBestActor = null;
            siegeCandidates.Clear();
            siegeBlockedActors.Clear();
            if (!resume && actor != null)
            {
                ClearAssignment(actor, "围攻演出取消");
                var traversal = actor.ArchitectureReadSpecialTraversal;
                if (traversal != null)
                {
                    traversal.CancelSiegeLadder(this);
                }
            }
        }

        if (resume && activeRole != Assignment.Climb)
        {
            siegePriorityUntil = Time.time + 8f;
        }
    }

    internal void BeginRaise()
    {
        State = LadderState.Raising;
        PlaySound(configuration.gripSound, Foot);
    }

    private VillageTraversalRegion ReadAssignmentTarget(LeonController player)
    {
        var actual = ReadTargetRegion(navigation, player.transform.position);
        if (IsWindow && actual == VillageTraversalRegion.FirstFloorIndoors &&
            (siegeOwner != null || Time.time < siegePriorityUntil))
        {
            return VillageTraversalRegion.SecondFloorIndoors;
        }
        if (actual == VillageTraversalRegion.HouseOuterGround)
        {
            siegePriorityUntil = 0f;
        }
        return actual;
    }
}
