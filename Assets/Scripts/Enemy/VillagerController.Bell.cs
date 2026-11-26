using UnityEngine;

public partial class VillagerController
{
    private bool bellWithdrawing;
    private bool bellRouteStarted;
    private Vector3 bellDestination;
    private float bellHitRecoveryDeadline;
    private VillageSoundscape bellSoundscape;
    private DoorController[] bellDoors;
    private DoorController bellExitDoor;
    private float bellExitDeadline;
    private float bellLookStarted = float.PositiveInfinity;
    private bool bellLooking;
    private Vector3 bellExitPoint;

    internal bool IsBellLookReady => bellLooking && !IsDead && !IsQTEControlled
        && !IsNavigationTraversalProtected && !combatModule.NativeCombatActive && !HasPendingHit
        && Time.time - bellLookStarted >= .6f;

    internal void ResumeAfterQTE(bool needsGroundRecovery)
    {
        if (IsDead || IsQTEControlled)
        {
            return;
        }

        combatModule.EndAttack();
        if (!needsGroundRecovery && combatModule.HasUnfinishedStandingHit)
        {
            combatModule.MaintainPairedSurvivorStaggerState(true);
            return;
        }
        if (needsGroundRecovery && combatModule.TryBeginPairedGroundRecovery())
        {
            return;
        }
        if (bellWithdrawing)
        {
            TryStartBellRoute();
            return;
        }

        if (Agent != null)
        {
            Agent.ResetPath();
            Agent.InvalidatePursuitResolutionCache();
        }
        if (EnemyAIManager.Instance != null)
        {
            EnemyAIManager.Instance.InvalidatePursuitDestination(this);
        }
        actionCoordinator.ResumeAfterStandUp();
    }

    internal void BeginBellWithdrawal(Vector3 destination, VillageSoundscape cover, DoorController[] doors)
    {
        if (bellWithdrawing || IsDead)
        {
            return;
        }

        bellWithdrawing = true;
        bellDestination = destination;
        bellSoundscape = cover;
        bellDoors = doors;
        bellHitRecoveryDeadline = Time.time + 1.6f;
        if (behaviorTree != null)
        {
            behaviorTree.DisableBehavior();
        }
        if (!IsQTEControlled && !IsNavigationTraversalProtected && combatModule.HasGrabWindup)
        {
            combatModule.CancelNativeCombat(false);
        }
        combatModule.StopContinuousDamage();
        animationPresenter.ReleaseVoice();
        perceptionModule.ClearCombatEngagement(false);
        TryStartBellRoute();
    }

    private void TryStartBellRoute()
    {
        if (bellRouteStarted || IsNavigationTraversalProtected || IsQTEControlled || combatModule.NativeCombatActive)
        {
            return;
        }
        if (combatModule.HasUnfinishedStandingHit && Time.time < bellHitRecoveryDeadline)
        {
            return;
        }

        bellRouteStarted = true;
        actionCoordinator.CloseAction();
        StopAndClearMovement();
        actionCoordinator.RestoreRequest();
        combatModule.ClearActionParameters();

        if (TryFindBellExit(out var destination, out bellExitDoor))
        {
            bellExitPoint = destination;
            bellExitDeadline = Time.time + 6f;
            BeginPatrol();
            SetPatrolTarget(destination);
            if (Agent.SetDestination(destination) || Agent.hasPath)
            {
                return;
            }
        }

        BeginBellLook();
    }

    private bool TryFindBellExit(out Vector3 destination, out DoorController selectedDoor)
    {
        destination = default;
        selectedDoor = null;
        if (Agent == null || bellSoundscape == null || !bellSoundscape.HasInteriorCover(transform.position + Vector3.up * 1.65f, 4f))
        {
            return false;
        }

        float bestDistance = 14f * 14f;
        foreach (var door in bellDoors)
        {
            if (door == null || !door.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 center = door.PassageCenter;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 probe = center + door.PassageNormal * (side * 1.15f);
                probe.y = door.transform.position.y;
                float distance = (probe - transform.position).sqrMagnitude;
                if (distance >= bestDistance || !Agent.TryResolveReachableDestination(probe, out Vector3 point)
                    || Vector3.Distance(point, probe) > 1.3f
                    || bellSoundscape.HasInteriorCover(point + Vector3.up * 1.65f, 4f)
                    || !PursuitNavigationTopology.TryFindFirstPortal(transform.position, point, out var firstDoor, out _)
                    || firstDoor != door)
                {
                    continue;
                }

                selectedDoor = door;
                destination = point;
                bestDistance = distance;
            }
        }
        return selectedDoor != null;
    }

    private void BeginBellLook()
    {
        bellExitDoor = null;
        ClearPatrolTarget();
        BeginIdle();
        bellLooking = true;
        bellLookStarted = float.PositiveInfinity;
    }

    private void UpdateBellReaction()
    {
        if (!bellRouteStarted || IsQTEControlled || IsNavigationTraversalProtected)
        {
            return;
        }

        if (!bellLooking)
        {
            if (bellExitDoor == null || Time.time >= bellExitDeadline || Agent.CurrentPathState == PathAgentState.Failed
                || HorizontalDistance(transform.position, bellExitPoint) <= .45f)
            {
                BeginBellLook();
            }
            else if (!bellExitDoor.IsPassageReady && Vector3.Distance(transform.position, bellExitDoor.PassageCenter) < 1.8f)
            {
                bellExitDoor.TryOpenForAI(transform);
            }
        }

        if (!bellLooking)
        {
            return;
        }

        if (HasPendingHit || combatModule.HasUnfinishedStandingHit)
        {
            bellLookStarted = float.PositiveInfinity;
            return;
        }
        if (CurrentState != VillagerState.Idle)
        {
            BeginIdle();
        }
        if (!HasPhysicalEnemySupport())
        {
            bellLookStarted = float.PositiveInfinity;
            if (specialTraversal != null)
            {
                specialTraversal.TryBeginUnsupportedDrop();
            }
            return;
        }

        Vector3 heading = Vector3.ProjectOnPlane(bellDestination - transform.position, Vector3.up);
        if (heading.sqrMagnitude > .0001f)
        {
            Quaternion target = Quaternion.LookRotation(heading);
            var turn = Quaternion.RotateTowards(transform.rotation, target, 180f * Time.deltaTime);
            motionExecutor.CommitNormalOrientation(turn);
            if (Quaternion.Angle(turn, target) > 10f || actualHorizontalMovementSpeed > .12f)
            {
                bellLookStarted = float.PositiveInfinity;
                return;
            }
        }
        if (float.IsPositiveInfinity(bellLookStarted))
        {
            bellLookStarted = Time.time;
        }
    }

}
