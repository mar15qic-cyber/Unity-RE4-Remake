using System.Collections.Generic;
using GameArchitecture;
using Pathfinding;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed partial class VillageLadderSite : MonoBehaviour, IInteractable
{
    internal enum LadderState { Down, Raising, Ready, Falling }
    internal enum Assignment { None, Raise, Climb }

    private const float TargetDwell = 0.95f;
    private const float RebuildDelay = 5f;
    private const float FallDuration = 0.8f;
    private static readonly List<VillageLadderSite> sites = new();

    private VillageLadderConfiguration configuration;
    private VillageLadderConfiguration.Site layout;
    private HouseThreeRegionNavigation navigation;
    private EnemyAIManager group;
    private WindowTraversalPoint window;
    private Transform ladderVisual;
    private bool awaitingSiege;

    internal void DeploySiegeLadder()
    {
        if (awaitingSiege)
        {
            SetLadderLift(1f);
            State = LadderState.Ready;
            awaitingSiege = false;
            ladderVisual.gameObject.SetActive(true);
        }
    }
    private MeshRenderer originalRenderer;
    private MeshCollider originalCollider;
    private bool originalRendererEnabled;
    private bool originalColliderEnabled;
    private float ladderLength;
    private Vector3 outward;
    private Quaternion downRotation;
    private float raisedAngle;
    private float liftFraction;
    private float fallStartFraction;
    private readonly List<Collider> solidRails = new();
    private readonly List<(Collider actor, Collider rail)> railIgnores = new();
    private float fallStartedAt;
    private float availableAt;
    private float targetSince = -1f;
    private float nextAssignmentAt;
    private float assignedProgressAt;
    private float assignedDistance;
    private Vector3 assignedDestination;
    private VillagerController assigned;
    private Assignment assignedRole;
    private Assignment activeRole;
    private ActionHandle activeAction;
    private VillagerController activeEnemy;
    private int routeVersion;
    private int selectionId;
    private int pendingPaths;
    private float selectionDeadline;
    private VillagerController bestPathActor;
    private ActionHandle bestPathAction;
    private float bestPathLength;
    private VillagerController recentTimeout;
    private float retryAfter;
    private bool ownsWindow;
    private readonly Collider[] exitOverlaps = new Collider[16];
    private readonly List<(VillagerController actor, Vector3 origin, Vector3 target, float distance)> candidates = new();

    private LeonController candidate;
    private bool candidateRaise;
    private bool playerRaising;
    private Vector3 playerRaiseStart;
    private float playerRaiseSettle;
    private bool playerRaiseGripPlayed;
    private Vector3 playerRaiseStance;
    private Transform raiseLeftWrist;
    private Transform raiseRightWrist;
    private AnimationPlayableOutput pushOutput;
    private LeonController pushingPlayer;
    private LeonAnimationActionCoordinator playerActions;
    private LeonActionMotionDriver playerMotion;
    private LeonWeaponVisualController playerWeapon;
    private MotionControlToken pushMotion;
    private int pushTransaction;
    private PlayableGraph pushGraph;
    private AnimationClipPlayable pushPlayable;
    private Vector3 pushPosition;
    private Quaternion pushStartRotation;
    private bool pushCommitted;
    private bool ownsPushWindow;
    private float pushContact;
    private float pushEnd;

    internal LadderState State { get; private set; }
    internal VillageLadderConfiguration Clips => configuration;
    internal Vector3 Foot => layout.foot;
    internal Vector3 Head => layout.head;
    internal Vector3 Exit => layout.exit;
    internal Vector3 Outward => outward;
    internal Quaternion Facing => Quaternion.LookRotation(-outward);
    internal bool IsWindow => window != null;
    private int LastRung
    {
        get
        {
            if (IsFixed)
            {
                return 10;
            }
            return 8;
        }
    }

    internal float RungSpacing
    {
        get
        {
            if (IsFixed)
            {
                // 塔楼原版模型的横档沿世界高度每半米一格。
                return 0.50f;
            }
            return ladderLength * 0.10f;
        }
    }

    internal float FirstRung
    {
        get
        {
            if (IsFixed)
            {
                return 0.423f;
            }
            return ladderLength * 0.095f;
        }
    }
    internal Vector3 RungOrigin => Foot + Vector3.up * 0.06f;
    internal Vector3 ClimbAxis => (Head - Foot).normalized;
    internal Vector3 Across => Facing * Vector3.right;
    internal Vector3 Approach => Foot + outward * Mathf.Max(0.53f, layout.approachDistance);
    internal float TopClimbHeight
    {
        get
        {
            if (IsWindow)
            {
                return Mathf.Min(Head.y - 1.35f, window.OpeningCenterWorld.y - window.openingHeight * .5f - .69f);
            }
            return Head.y - 1.35f;
        }
    }
    internal Vector3 PromptPosition
    {
        get
        {
            if (candidateRaise)
            {
                return Approach + Vector3.up * 0.40f;
            }

            if (candidateClimb)
            {
                return Foot + Vector3.up * 1.05f;
            }

            if (IsWindowPushCandidate)
            {
                return window.OpeningCenterWorld - outward * .12f - Vector3.up * .12f;
            }
            return Exit + outward * 0.25f + Vector3.up * 0.72f;
        }
    }
    public float InteractRange => 1.65f;
    internal bool IsWindowPushCandidate => IsWindow && candidate != null && !candidateClimb && !candidateDrop && !candidateRaise;

    internal Vector3 ReadClimbPosition(float height)
    {
        // 斜梯在胸口高度比脚下更靠墙，按胸口的可达距离安排身体。
        float along = Mathf.Clamp01((height + 1.38f - Foot.y) / (Head.y - Foot.y));
        float clearance = 0.36f;
        if (IsWindow)
        {
            clearance = 0.28f;
        }

        Vector3 position = Vector3.Lerp(Foot, Head, along) + outward * clearance;
        position.y = height;
        return position;
    }

    internal Vector3 ReadRungPosition(int rung, float across, float outwardOffset)
    {
        float distance = FirstRung + Mathf.Clamp(rung, 0, LastRung) * RungSpacing;
        return RungOrigin + ClimbAxis * distance + Across * across + outward * outwardOffset;
    }

    internal int ReadRungAtHeight(float height)
    {
        float distance = (height - RungOrigin.y) / ClimbAxis.y;
        return Mathf.Clamp(Mathf.RoundToInt((distance - FirstRung) / RungSpacing), 0, LastRung);
    }

    internal static void Install(SceneStartupEntry startup, HouseThreeRegionNavigation navigation, EnemyAIManager group)
    {
        if (startup.gameObject.scene.name != "VillageBattleLighting")
        {
            return;
        }

        var config = Resources.Load<VillageLadderConfiguration>(VillageLadderConfiguration.ResourceName);
        if (config == null || config.sites == null || config.mount == null || config.climb == null ||
            config.raise == null || config.platformExit == null || config.windowExit == null ||
            config.push == null || config.platformPush == null || config.fall == null || config.land == null ||
            config.ladderMesh == null || config.ladderMaterial == null)
        {
            Debug.LogError("村庄木梯缺少梯口或动画配置。", startup);
            return;
        }

        var windows = Object.FindObjectsByType<WindowTraversalPoint>(FindObjectsSortMode.None);
        var siege = startup.GetComponentInChildren<VillageSiegeSequence>(true);
        if (siege == null)
        {
            siege = Object.FindFirstObjectByType<VillageSiegeSequence>();
        }
        foreach (var layout in config.sites)
        {
            if (layout == null)
            {
                continue;
            }

            WindowTraversalPoint window = null;
            if (!string.IsNullOrEmpty(layout.windowName))
            {
                foreach (var point in windows)
                {
                    if (!point.isFence && point.gameObject.scene == startup.gameObject.scene && point.name == layout.windowName)
                    {
                        window = point;
                        break;
                    }
                }

                if (window == null)
                {
                    Debug.LogError("木梯找不到绑定的窗口 " + layout.windowName, startup);
                    continue;
                }
            }

            var root = new GameObject(layout.displayName);
            root.SetActive(false);
            root.transform.SetParent(startup.transform, true);
            root.transform.position = layout.exit;
            var site = root.AddComponent<VillageLadderSite>();
            if (!site.Configure(config, layout, navigation, group, window))
            {
                Debug.LogError("木梯梯脚、出口或支撑面无效 " + layout.displayName, startup);
                Destroy(root);
                continue;
            }

            if (siege != null && siege.OwnsWindow(window))
            {
                site.awaitingSiege = true;
                site.ladderVisual.gameObject.SetActive(false);
            }
            root.SetActive(true);
        }

        foreach (var site in startup.gameObject.scene.GetRootGameObjects())
        {
            foreach (var tower in site.GetComponentsInChildren<VillageTowerTraversal>(true))
            {
                tower.Install(config, navigation, group);
            }
        }
    }

    private bool Configure(VillageLadderConfiguration config, VillageLadderConfiguration.Site placement,
        HouseThreeRegionNavigation regions, EnemyAIManager enemies, WindowTraversalPoint aperture)
    {
        configuration = config;
        layout = placement;
        navigation = regions;
        group = enemies;
        window = aperture;
        Vector3 slope = Head - Foot;
        outward = Vector3.ProjectOnPlane(-slope, Vector3.up).normalized;
        ladderLength = slope.magnitude;
        if (outward.sqrMagnitude < 0.9f || slope.y < 1.6f ||
            navigation.TreadRegion(Foot) != VillageTraversalRegion.HouseOuterGround ||
            navigation.TreadRegion(Exit) != DestinationRegion || !HasSupport(Approach) || !HasSupport(Exit))
        {
            return false;
        }

        downRotation = Quaternion.LookRotation(-Vector3.up, outward);
        raisedAngle = Vector3.SignedAngle(outward, slope.normalized, downRotation * Vector3.right);
        if (!string.IsNullOrEmpty(placement.replacedPropPath))
        {
            var prop = GameObject.Find(placement.replacedPropPath);
            if (prop == null || prop.scene != gameObject.scene || !prop.TryGetComponent(out originalRenderer))
            {
                return false;
            }

            prop.TryGetComponent(out originalCollider);
        }

        BuildVisual();
        if (placement.initiallyRaised)
        {
            SetLadderLift(1f);
            State = LadderState.Ready;
        }
        return true;
    }

    private VillageTraversalRegion DestinationRegion
    {
        get
        {
            if (IsFixed)
            {
                return VillageTraversalRegion.TowerPlatform;
            }

            if (window != null)
            {
                return VillageTraversalRegion.SecondFloorIndoors;
            }

            return VillageTraversalRegion.SecondFloorOuterPlatform;
        }
    }

    private static bool HasSupport(Vector3 point)
    {
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 0.25f, Vector3.down, 0.65f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y >= 0.55f && hit.collider.GetComponentInParent<VillagerController>() == null &&
                hit.collider.GetComponentInParent<LeonController>() == null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        if (layout != null)
        {
            sites.Add(this);
            InteractionSystem.Register(this);
            if (originalRenderer != null)
            {
                originalRendererEnabled = originalRenderer.enabled;
                originalRenderer.enabled = false;
            }

            if (originalCollider != null)
            {
                originalColliderEnabled = originalCollider.enabled;
                originalCollider.enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        RestoreRails(null);
        InteractionSystem.Unregister(this);
        sites.Remove(this);
        EndPlayerTraversal(true);
        EndPush(true);
        ReleaseWindow();
        assigned = null;
        activeEnemy = null;
        activeAction = default;
        fixedClimbers.Clear();
        fixedExitOwner = null;
        siegeOwner = null;
        siegeActor = null;
        siegePriorityUntil = 0f;
        siegePathVersion++;
        siegePendingPaths = 0;
        siegeRoute = null;
        siegeBestActor = null;
        siegeCandidates.Clear();
        siegeBlockedActors.Clear();
        routeVersion++;
        CancelSelection();
        if (originalRenderer != null)
        {
            originalRenderer.enabled = originalRendererEnabled;
        }

        if (originalCollider != null)
        {
            originalCollider.enabled = originalColliderEnabled;
        }
    }

    internal static VillageLadderSite GetAssignment(VillagerController actor)
    {
        foreach (var site in sites)
        {
            if (site.assigned == actor)
            {
                return site;
            }
        }

        return null;
    }

    internal static bool IsSpawnConnectedToLadder(GraphNode origin, VillageTraversalRegion targetRegion)
    {
        if (origin == null || AstarPath.active == null || !IsUpperFloor(targetRegion))
        {
            return false;
        }

        foreach (var site in sites)
        {
            if (site == null || !site.isActiveAndEnabled || !site.CanServeTarget(targetRegion) ||
                site.IsFixed && site.tower.HasCollapsed)
            {
                continue;
            }

            var nearest = AstarPath.active.GetNearest(site.Approach, NearestNodeConstraint.Walkable);
            if (nearest.node != null && (nearest.position - site.Approach).sqrMagnitude <= .75f * .75f
                && PathUtilities.IsPathPossible(origin, nearest.node))
            {
                return true;
            }
        }
        return false;
    }

    internal static bool CanAssign(VillagerController actor)
    {
        return actor != null && actor.IsActive && actor.IsRuntimeInitialized && actor.isActiveAndEnabled &&
            actor.CurrentState == VillagerState.Chase && actor.CanEvaluateStandingCombatAction &&
            !actor.IsNavigationTraversalProtected && !actor.IsQTEControlled &&
            actor.GetComponent<ChainsawManAnimationBridge>() == null;
    }

    internal bool TryReadAssignment(VillagerController actor, out Assignment role, out Vector3 destination)
    {
        role = Assignment.None;
        destination = default;
        if (assigned != actor)
        {
            return false;
        }

        role = assignedRole;
        destination = Approach;
        if (activeEnemy != null || IsFixed && !CanMountFixedLadder())
        {
            destination += outward * 1.05f;
        }

        return true;
    }

    private bool IsTargetAbove(VillagerController actor)
    {
        return actor.Player != null && actor.Player == group.PlayerTarget && actor.perceptionModule != null &&
            actor.perceptionModule.ReadFullyAwareOfPlayer && CanServeTarget(ReadAssignmentTarget(actor.Player));
    }

    private static bool IsUpperFloor(VillageTraversalRegion region)
    {
        return region == VillageTraversalRegion.SecondFloorIndoors ||
            region == VillageTraversalRegion.SecondFloorOuterPlatform ||
            region == VillageTraversalRegion.TowerPlatform;
    }

    private bool CanServeTarget(VillageTraversalRegion targetRegion)
    {
        if (awaitingSiege)
        {
            return false;
        }
        if (IsFixed || targetRegion == VillageTraversalRegion.TowerPlatform)
        {
            return IsFixed && !tower.HasCollapsed && targetRegion == VillageTraversalRegion.TowerPlatform;
        }

        if (targetRegion == DestinationRegion)
        {
            return true;
        }
        if (!IsUpperFloor(targetRegion))
        {
            return false;
        }
        foreach (var route in EnemyWindowTraversalRoute.ActiveRoutes)
        {
            if (route != null && route.isActiveAndEnabled && route.allowBothDirections
                && route.platformRegionId == navigation.outerPlatformIdentifier)
            {
                return true;
            }
        }
        return false;
    }

    private void Update()
    {
        if (awaitingSiege)
        {
            return;
        }
        TickPush();
        if (State == LadderState.Falling)
        {
            float fraction = Mathf.Clamp01((Time.time - fallStartedAt) / FallDuration);
            SetLadderLift(Mathf.Lerp(fallStartFraction, 0f, fraction * fraction));
            if (fraction >= 1f)
            {
                State = LadderState.Down;
                availableAt = Time.time + RebuildDelay;
                PlaySound(configuration.floorSound, Foot);
            }
        }

        var player = group.PlayerTarget;
        VillageTraversalRegion targetRegion = VillageTraversalRegion.HouseOuterGround;
        if (player != null)
        {
            targetRegion = ReadAssignmentTarget(player);
        }

        if (!IsUpperFloor(targetRegion))
        {
            targetSince = -1f;
            ClearAssignment("目标换层");
            CancelSelection();
            return;
        }

        if (targetSince < 0f)
        {
            targetSince = Time.time;
        }

        if (assigned != null)
        {
            if (!CanAssign(assigned) || !IsTargetAbove(assigned))
            {
                ClearAssignment("接近中断");
            }
            else
            {
                TrackApproachProgress();
            }
        }

        if (pendingPaths > 0 && Time.time > selectionDeadline)
        {
            CancelSelection();
        }

        float assignmentDwell = TargetDwell;
        if (targetRegion != DestinationRegion)
        {
            assignmentDwell += 0.35f;
        }

        if (assigned != null || !CanServeTarget(targetRegion) || Time.time < targetSince + assignmentDwell || Time.time < nextAssignmentAt ||
            Time.time < availableAt || State == LadderState.Raising || State == LadderState.Falling ||
            activeRole == Assignment.Raise || pendingPaths > 0 || Time.deltaTime <= 0f)
        {
            return;
        }

        nextAssignmentAt = Time.time + 0.35f;
        AssignNearest();
    }

    private void TrackApproachProgress()
    {
        TryReadAssignment(assigned, out _, out Vector3 destination);
        float distance = Vector3.ProjectOnPlane(assigned.transform.position - destination, Vector3.up).magnitude;
        if ((assignedDestination - destination).sqrMagnitude > .15f * .15f || distance < assignedDistance - .15f)
        {
            assignedDestination = destination;
            assignedDistance = distance;
            assignedProgressAt = Time.time;
        }

        // 同一梯位允许一名候梯者等待正在攀爬者完成。
        if (activeEnemy == null && Time.time - assignedProgressAt >= 1.25f && !CanBegin(assigned))
        {
            recentTimeout = assigned;
            retryAfter = Time.time + 2.5f;
            ClearAssignment("梯口接近无进展");
        }
    }

    private void AssignNearest()
    {
        Assignment role = Assignment.Climb;
        Vector3 destination = Approach;
        if (State == LadderState.Down)
        {
            role = Assignment.Raise;
        }
        else if (activeEnemy != null || IsFixed && !CanMountFixedLadder())
        {
            destination += outward * 1.05f;
        }

        if (AstarPath.active == null || AstarPath.active.isScanning)
        {
            return;
        }

        candidates.Clear();
        foreach (var actor in group.StandingContactEnemies)
        {
            if (!CanAssign(actor) || actor == activeEnemy || GetAssignment(actor) != null ||
                actor.ArchitectureReadSpecialTraversal == null || !IsTargetAbove(actor) ||
                (actor == recentTimeout && Time.time < retryAfter) ||
                navigation.TreadRegion(actor.transform.position) != VillageTraversalRegion.HouseOuterGround)
            {
                continue;
            }

            var weapon = actor.GetComponent<EnemyHeldWeapon>();
            if (role == Assignment.Raise && weapon != null && weapon.IsHeld)
            {
                continue;
            }

            float distance = (actor.transform.position - destination).sqrMagnitude;
            if (distance > 22f * 22f ||
                !actor.Agent.TryResolveReachableDestination(destination, out Vector3 projected) ||
                (projected - destination).sqrMagnitude > 0.75f * 0.75f ||
                !actor.Agent.TryFindConnectedPathOrigin(projected, out Vector3 origin))
            {
                continue;
            }

            candidates.Add((actor, origin, projected, distance));
        }

        if (candidates.Count == 0)
        {
            return;
        }

        candidates.Sort((left, right) => left.distance.CompareTo(right.distance));
        int batch = ++selectionId;
        int count = Mathf.Min(4, candidates.Count);
        pendingPaths = count;
        selectionDeadline = Time.time + 1f;
        bestPathActor = null;
        bestPathAction = default;
        bestPathLength = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = candidates[i];
            var action = candidate.actor.actionCoordinator.CurrentHandle;
            var path = ABPath.Construct(candidate.origin, candidate.target,
                result => ReceivePathCost(batch, role, candidate.actor, action, result));
            AstarPath.StartPath(path);
        }
    }

    private void ReceivePathCost(int batch, Assignment role, VillagerController actor, ActionHandle action, Path result)
    {
        if (batch != selectionId || pendingPaths == 0 || !isActiveAndEnabled)
        {
            return;
        }

        if (!result.error && CanAssign(actor) && actor.actionCoordinator.CurrentHandle == action &&
            GetAssignment(actor) == null && IsTargetAbove(actor))
        {
            float length = result.GetTotalLength();
            if (length < bestPathLength)
            {
                bestPathActor = actor;
                bestPathAction = action;
                bestPathLength = length;
            }
        }

        pendingPaths--;
        if (pendingPaths != 0 || bestPathActor == null || !CanAssign(bestPathActor) ||
            bestPathActor.actionCoordinator.CurrentHandle != bestPathAction ||
            GetAssignment(bestPathActor) != null || !IsTargetAbove(bestPathActor))
        {
            return;
        }

        var player = group.PlayerTarget;
        if (player != null && CanServeTarget(ReadAssignmentTarget(player)) &&
            ((State == LadderState.Down && role == Assignment.Raise) ||
             (State == LadderState.Ready && role == Assignment.Climb)))
        {
            SetAssignment(bestPathActor, role);
        }
    }

    private void SetAssignment(VillagerController actor, Assignment role)
    {
        assigned = actor;
        assignedRole = role;
        assignedProgressAt = Time.time;
        TryReadAssignment(actor, out _, out Vector3 destination);
        assignedDestination = destination;
        assignedDistance = Vector3.ProjectOnPlane(actor.transform.position - destination, Vector3.up).magnitude;
        actor.ArchitectureReadSpecialTraversal.OnLadderApproachAssigned();
        Report(actor, "已分配" + RoleName(role));
    }

    private void CancelSelection()
    {
        if (pendingPaths != 0)
        {
            selectionId++;
            pendingPaths = 0;
        }
    }

    internal void ClearAssignment(VillagerController actor, string reason)
    {
        if (assigned == actor)
        {
            ClearAssignment(reason);
        }
    }

    private void ClearAssignment(string reason)
    {
        if (assigned != null)
        {
            Report(assigned, reason);
        }

        assigned = null;
        assignedRole = Assignment.None;
    }

    internal bool CanBegin(VillagerController actor)
    {
        if (actor == null || assigned != actor || activeEnemy != null || climbingPlayer != null ||
            pushTransaction != 0 || !IsTargetAbove(actor))
        {
            return false;
        }

        if (IsFixed && !CanMountFixedLadder())
        {
            return false;
        }

        if (IsSiegeActor(actor) && assignedRole == Assignment.Raise && State == LadderState.Down)
        {
            return siegeRoute != null;
        }
        if (IsSiegeActor(actor) && assignedRole == Assignment.Climb && !siegeMountAllowed)
        {
            return false;
        }

        Vector3 difference = actor.transform.position - Approach;
        float height = Mathf.Abs(difference.y);
        difference.y = 0f;
        float distance = difference.magnitude;
        if (height > .42f || distance > .9f)
        {
            return false;
        }
        if (distance > .68f && (actor.Agent.CurrentPathState != PathAgentState.Arrived
            || !actor.CanMoveDirectlyToNavigationPoint(Approach, true)))
        {
            return false;
        }

        return State == LadderState.Down && assignedRole == Assignment.Raise
            || State == LadderState.Ready && assignedRole == Assignment.Climb;
    }

    internal bool TryBegin(VillagerController actor, ActionHandle action, out int version, out Assignment role)
    {
        version = 0;
        role = Assignment.None;
        if (!action.Valid || !CanBegin(actor))
        {
            return false;
        }

        if (IsFixed)
        {
            version = ++routeVersion;
            role = Assignment.Climb;
            fixedClimbers.Add(actor, new FixedClimber(action, version));
            ClearAssignment("登上塔梯");
            return true;
        }

        activeEnemy = actor;
        IgnoreRails(actor.GetComponent<CapsuleCollider>());
        activeAction = action;
        version = ++routeVersion;
        role = assignedRole;
        activeRole = role;
        if (role == Assignment.Raise && !IsSiegeActor(actor))
        {
            BeginRaise();
        }
        else if (role == Assignment.Climb && siegeOwner == null)
        {
            siegePriorityUntil = 0f;
        }

        ClearAssignment("已接受" + RoleName(role));
        return true;
    }

    internal bool Owns(VillagerController actor, ActionHandle action, int version)
    {
        if (IsFixed)
        {
            return isActiveAndEnabled && actor != null && fixedClimbers.TryGetValue(actor, out var climber) &&
                climber.Action == action && climber.Version == version;
        }

        return isActiveAndEnabled && activeEnemy == actor && activeAction == action && version == routeVersion;
    }

    internal bool TransferToDeath(VillagerController actor, ActionHandle previous, int version, ActionHandle death)
    {
        if (!Owns(actor, previous, version) || !actor.IsDead || !death.Valid || death.Object != previous.Object)
        {
            return false;
        }

        if (IsFixed)
        {
            fixedClimbers[actor].Action = death;
        }
        else
        {
            activeAction = death;
        }
        return true;
    }

    internal void RaiseProgress(VillagerController actor, ActionHandle action, int version, float progress)
    {
        if (!Owns(actor, action, version) || State != LadderState.Raising)
        {
            return;
        }

        SetLadderLift(Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f)
        {
            State = LadderState.Ready;
            PlaySound(configuration.impactSound, Head);
            Report(actor, "梯体靠稳");
        }
    }

    internal bool TryReserveExit(VillagerController actor)
    {
        var capsule = actor.GetComponent<CapsuleCollider>();
        if (!HasExitSupport(Exit) || !IsExitClear(capsule, actor.transform))
        {
            return false;
        }

        if (IsFixed)
        {
            if (fixedExitOwner != null && fixedExitOwner != actor)
            {
                return false;
            }
            fixedExitOwner = actor;
            return true;
        }

        if (window == null)
        {
            return true;
        }

        ownsWindow = window.TryReservePassage(this);
        return ownsWindow;
    }

    private bool IsExitClear(CapsuleCollider capsule, Transform actor)
    {
        if (capsule == null || capsule.direction != 1)
        {
            return false;
        }

        Vector3 scale = capsule.transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(2f * radius, capsule.height * Mathf.Abs(scale.y));
        float stem = height * 0.5f - radius;
        // 为楼板表面的接触偏移留出余量，避免把脚下的支撑误判为障碍。
        Vector3 center = Exit + Facing * Vector3.Scale(capsule.center, scale) + Vector3.up * 0.06f;
        int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * stem, center + Vector3.up * stem,
            Mathf.Max(0.01f, radius - 0.015f), exitOverlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == exitOverlaps.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            var collider = exitOverlaps[i];
            if (!collider.transform.IsChildOf(actor) &&
                !solidRails.Contains(collider) && (tower == null || !tower.IsLadderCollider(collider)) &&
                (window == null || !window.ShouldIgnoreCollider(collider)) &&
                !Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, collider.gameObject.layer))
            {
                return false;
            }
        }

        return true;
    }

    internal bool HasExitSupport(Vector3 position)
    {
        if (IsFixed)
        {
            return !tower.HasCollapsed && tower.ContainsPlatform(position) && HasSupport(position);
        }

        return navigation.TreadRegion(position) == DestinationRegion && HasSupport(position);
    }

    internal void NotifyWindowCrossing()
    {
        if (ownsWindow && window != null && window.fracture != null && !window.fracture.IsBroken)
        {
            window.fracture.Fracture(window.OpeningCenterWorld, -outward);
        }
    }

    internal Vector3 RaiseHandContact(float side)
    {
        return ladderVisual.TransformPoint(new Vector3(side * 0.30f, 0.65f, 0.065f));
    }

    internal static bool HasAvailablePush(WindowTraversalPoint point, Transform interactor)
    {
        foreach (var site in sites)
        {
            if (site.window == point && site.CanInteract(interactor) && !site.candidateClimb)
            {
                return true;
            }
        }

        return false;
    }

    internal void ReleaseWindow()
    {
        if (ownsWindow && window != null)
        {
            window.ReleasePassage(this);
        }

        ownsWindow = false;
    }

    internal void Release(VillagerController actor, ActionHandle action, int version, string reason)
    {
        if (!Owns(actor, action, version))
        {
            return;
        }

        if (IsFixed)
        {
            fixedClimbers.Remove(actor);
            ReleaseEnemyExit(actor);
            Report(actor, reason);
            return;
        }

        bool offerClimb = State == LadderState.Ready && activeRole == Assignment.Raise &&
            actor.isActiveAndEnabled && actor.IsActive && !actor.IsDead && !actor.HasPendingHit && IsTargetAbove(actor);
        if (State == LadderState.Raising)
        {
            StartFalling();
        }

        ReleaseWindow();
        RestoreRails(actor.GetComponent<CapsuleCollider>());
        activeEnemy = null;
        activeAction = default;
        activeRole = Assignment.None;
        Report(actor, reason);
        if (offerClimb)
        {
            SetAssignment(actor, Assignment.Climb);
        }
    }

    private void StartFalling()
    {
        State = LadderState.Falling;
        fallStartFraction = liftFraction;
        fallStartedAt = Time.time;
        CancelSelection();
        ClearAssignment("梯体倾倒");
    }

    internal static VillageLadderSite FindAvailablePush(Transform interactor)
    {
        VillageLadderSite best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var site in sites)
        {
            if (site.CanInteract(interactor) && !site.IsWindowPushCandidate)
            {
                float distance = (site.PromptPosition - interactor.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = site;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    public bool CanInteract(Transform interactor)
    {
        if (awaitingSiege)
        {
            return false;
        }
        candidate = null;
        candidateClimb = false;
        candidateDrop = false;
        candidateRaise = false;
        if (pushTransaction != 0 ||
            climbingPlayer != null ||
            (window != null && window.IsPassageReservedByOther(this)))
        {
            return false;
        }

        if (interactor == null || !interactor.TryGetComponent<LeonController>(out var player))
        {
            return false;
        }

        if (State == LadderState.Down && !IsFixed && configuration.playerRaise != null && activeEnemy == null)
        {
            Vector3 towardLadder = Foot - player.transform.position;
            towardLadder.y = 0f;
            Vector3 offsetToApproach = player.transform.position - Approach;
            candidateRaise = player.CanAct() && player.IsGrounded && !player.IsJumping && !player.IsCrouching &&
                Mathf.Abs(offsetToApproach.y) < 0.4f && offsetToApproach.sqrMagnitude < 0.9f * 0.9f &&
                Vector3.Dot(player.transform.forward, towardLadder.normalized) > 0.35f &&
                player.TryGetComponent<LeonAnimationActionCoordinator>(out var raiseActions) &&
                raiseActions.CanRequestLadderPush();
            if (candidateRaise)
            {
                candidate = player;
            }
            return candidateRaise;
        }

        if (State != LadderState.Ready)
        {
            return false;
        }

        if (CanBeginPlayerClimb(player))
        {
            candidate = player;
            candidateClimb = true;
            return true;
        }

        if (IsFixed)
        {
            candidateDrop = CanBeginPlayerJumpDown(player);
            if (candidateDrop)
            {
                candidate = player;
            }
            return candidateDrop;
        }

        Vector3 offset = player.transform.position - Exit;
        float height = Mathf.Abs(offset.y);
        offset.y = 0f;
        if (!player.CanAct() || player.IsJumping || player.IsCrouching ||
            height > 0.56f || offset.sqrMagnitude > InteractRange * InteractRange ||
            navigation.TreadRegion(player.transform.position) != DestinationRegion ||
            (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput) ||
            (window != null && ownsWindow))
        {
            return false;
        }

        if (IsWindow)
        {
            Vector3 towardWindow = Vector3.ProjectOnPlane(window.OpeningCenterWorld - player.transform.position, Vector3.up);
            if (offset.sqrMagnitude > .35f * .35f || towardWindow.sqrMagnitude < .01f
                || Vector3.Dot(player.transform.forward, towardWindow.normalized) < .55f)
            {
                return false;
            }
        }

        if (!player.IsGrounded &&
            (!player.TryGetComponent<Rigidbody>(out var body) || Mathf.Abs(body.linearVelocity.y) > 0.75f ||
             !HasSupport(player.transform.position)))
        {
            return false;
        }

        if (!player.TryGetComponent<LeonAnimationActionCoordinator>(out var actions) || !actions.CanRequestLadderPush())
        {
            return false;
        }

        candidate = player;
        return true;
    }

    public string GetPromptText()
    {
        if (candidateRaise)
        {
            return "架起梯子";
        }

        if (candidateClimb)
        {
            return "攀爬";
        }
        if (candidateDrop)
        {
            return "跳下";
        }

        if (IsWindow)
        {
            return "推倒梯子";
        }

        return "踢倒梯子";
    }

    public void Interact()
    {
        var player = candidate;
        if (player == null || !CanInteract(player.transform))
        {
            return;
        }

        if (candidateClimb)
        {
            BeginPlayerClimb(player);
            return;
        }
        if (candidateDrop)
        {
            if (BeginPlayerTraversal(player, true))
            {
                towerJumpDeparture = true;
                towerJumpTarget = Head + Outward * .50f;
                IgnoreFixedLadderForPlayer();
                Vector3 direction = Vector3.ProjectOnPlane(towerJumpTarget - climbBody.position, Vector3.up).normalized;
                climbMotion.CommitSpeed(climbToken, direction * 2.8f + Vector3.up * 1.6f);
                StartPlayerFall(configuration.playerJumpDown);
            }
            return;
        }

        var actions = player.GetComponent<LeonAnimationActionCoordinator>();
        var motion = player.GetComponent<LeonActionMotionDriver>();
        var animator = player.GetComponent<Animator>();
        if (actions == null || motion == null || animator == null || !actions.RequestLadderPush())
        {
            return;
        }

        pushingPlayer = player;
        IgnoreRails(player.GetComponent<CapsuleCollider>());
        playerActions = actions;
        playerMotion = motion;
        pushTransaction = actions.CurrentFullBodyTransactionId;
        if (window != null)
        {
            ownsPushWindow = window.TryReservePassage(this);
            if (!ownsPushWindow)
            {
                EndPush(true);
                return;
            }
        }

        pushMotion = motion.AuthorizeMotion(MotionSource.Traversal, pushTransaction);
        if (!pushMotion.Valid)
        {
            EndPush(true);
            return;
        }

        player.ResetMovementInputForWindow();
        player.SetFreeLooking(false);
        player.ChangeState(LeonState.Traversing);
        motion.StopHorizontalSpeed(pushMotion);
        pushPosition = player.transform.position;
        pushStartRotation = player.transform.rotation;
        playerRaising = candidateRaise;
        playerWeapon = player.GetComponent<LeonWeaponVisualController>();
        if (playerWeapon != null)
        {
            playerWeapon.SetTraversalWeaponVisible(false);
        }

        pushGraph = PlayableGraph.Create("里昂活动木梯");
        pushGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationClip clip = configuration.push;
        pushContact = configuration.pushContact;
        pushEnd = 1f;
        if (!IsWindow)
        {
            clip = configuration.platformPush;
            pushContact = configuration.platformPushContact;
        }

        if (playerRaising)
        {
            ClearAssignment("里昂架梯");
            CancelSelection();
            State = LadderState.Raising;
            playerRaiseStart = pushPosition;
            Vector3 cross = Vector3.Cross(Facing * Vector3.forward, pushStartRotation * Vector3.forward);
            clip = configuration.playerRaise;
            float side = -1f;
            if (cross.y < -0.25f && configuration.playerRaiseAlternate != null)
            {
                clip = configuration.playerRaiseAlternate;
                side = 1f;
            }
            // 从梯框侧边抬梯，避免梯框转过腰和肩。保留动画自己的肘弯和腕部朝向。
            Vector3 stance = Approach + downRotation * Vector3.right * (.60f * side);
            playerRaiseStance = Approach;
            if (CanStandBesideLadder(player, stance))
            {
                playerRaiseStance = stance;
            }
            raiseLeftWrist = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.LeftHand);
            raiseRightWrist = AnimatorBoneBindings.Resolve(animator, HumanBodyBones.RightHand);
            pushContact = configuration.playerRaiseGrip;
            playerRaiseSettle = Mathf.Max(pushContact + .05f, configuration.playerRaiseSeat);
            playerRaiseGripPlayed = false;

        }

        pushPlayable = AnimationClipPlayable.Create(pushGraph, clip);
        pushPlayable.SetApplyFootIK(false);
        pushOutput = AnimationPlayableOutput.Create(pushGraph, "活动梯动作", animator);
        pushOutput.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        pushOutput.SetSourcePlayable(pushPlayable);
        pushOutput.SetWeight(0f);
        pushGraph.Play();
    }

    private bool CanStandBesideLadder(LeonController player, Vector3 stance)
    {
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null || !HasSupport(stance))
        {
            return false;
        }
        Bounds bounds = capsule.bounds;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y + radius + .04f, bounds.center.z);
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y - radius, bounds.center.z);
        Vector3 offset = Vector3.ProjectOnPlane(stance - player.transform.position, Vector3.up);
        foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, offset.normalized, offset.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(player.transform) && !solidRails.Contains(hit.collider) &&
                hit.normal.y < .55f && Vector3.Dot(hit.normal, offset) < 0f)
            {
                return false;
            }
        }
        return true;
    }

    private void TickPush()
    {
        if (pushTransaction == 0)
        {
            return;
        }

        if (pushingPlayer == null || playerActions == null || !playerActions.OwnsLadderPush(pushTransaction) ||
            !playerMotion.TokenValid(pushMotion))
        {
            EndPush(true);
            return;
        }

        float seconds = (float)pushPlayable.GetTime();
        float duration = pushPlayable.GetAnimationClip().length;
        float progress = seconds / duration;
        float entryWeight = Mathf.SmoothStep(0f, 1f, seconds / .13f);
        float exitWeight = Mathf.SmoothStep(0f, 1f, (duration - seconds) / .24f);
        pushOutput.SetWeight(entryWeight * exitWeight);
        float alignment = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, pushContact * 0.8f, progress));
        if (playerRaising)
        {
            Vector3 position = Vector3.Lerp(playerRaiseStart, playerRaiseStance, alignment);
            Quaternion facing = Quaternion.Slerp(pushStartRotation, Facing, alignment);
            playerMotion.CommitTraversalPose(pushMotion, position, facing);
            if (!playerRaiseGripPlayed && progress >= pushContact)
            {
                playerRaiseGripPlayed = true;
                PlaySound(configuration.gripSound, Foot);
            }

            if (!pushCommitted)
            {
                float phase = Mathf.InverseLerp(pushContact, playerRaiseSettle, progress);
                float lift = Mathf.SmoothStep(0f, 1f, phase);
                if (phase > 0f && raiseLeftWrist != null && raiseRightWrist != null)
                {
                    Vector3 grip = (raiseLeftWrist.position + raiseRightWrist.position) * .5f;
                    Vector3 hinge = downRotation * Vector3.right;
                    Vector3 lever = Vector3.ProjectOnPlane(grip - RungOrigin, hinge);
                    float gripLift = Mathf.Clamp01(Vector3.SignedAngle(outward, lever, hinge) / raisedAngle);
                    float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, phase));
                    lift = Mathf.Lerp(gripLift, 1f, release);
                    lift *= Mathf.SmoothStep(0f, 1f, phase / .12f);
                }
                SetLadderLift(Mathf.Max(liftFraction, lift));
                if (progress >= playerRaiseSettle)
                {
                    pushCommitted = true;
                    State = LadderState.Ready;
                    PlaySound(configuration.impactSound, Head);
                }
            }

            if (progress >= pushEnd)
            {
                EndPush(false);
            }
            return;
        }

        Quaternion rotation = Quaternion.Slerp(pushStartRotation, Quaternion.LookRotation(outward), alignment);
        playerMotion.CommitTraversalPose(pushMotion, pushPosition, rotation);
        if (!pushCommitted && progress >= pushContact)
        {
            pushCommitted = true;
            if (State == LadderState.Ready && !ownsWindow)
            {
                PlaySound(configuration.kickSound, Head);
                StartFalling();
                if (group != null && group.HasEngagedEnemy())
                {
                    VillageLeonDialogue.Report(pushingPlayer, VillageSpeechCue.LadderDown);
                }
            }
        }

        if (progress >= pushEnd)
        {
            EndPush(false);
        }
    }

    private void EndPush(bool interrupted)
    {
        if (pushTransaction == 0)
        {
            return;
        }

        int transaction = pushTransaction;
        pushTransaction = 0;
        if (playerRaising && !pushCommitted)
        {
            State = LadderState.Down;
            SetLadderLift(0f);
        }
        playerRaising = false;
        raiseLeftWrist = null;
        raiseRightWrist = null;
        pushCommitted = false;
        if (ownsPushWindow && window != null)
        {
            window.ReleasePassage(this);
        }

        ownsPushWindow = false;
        if (pushGraph.IsValid())
        {
            pushGraph.Destroy();
        }

        if (playerWeapon != null)
        {
            playerWeapon.SetTraversalWeaponVisible(true);
        }

        if (playerMotion != null)
        {
            playerMotion.ReleaseMotion(pushMotion);
        }

        if (playerActions != null)
        {
            playerActions.EndLadderPush(transaction, interrupted);
        }

        if (pushingPlayer != null && pushingPlayer.currentState == LeonState.Traversing &&
            (playerActions == null || !playerActions.HasActiveFullBodyAction))
        {
            pushingPlayer.ChangeState(LeonState.Idle);
        }

        if (playerActions != null)
        {
            playerActions.SynchronizeMotionControlAuthority();
        }

        if (pushingPlayer != null)
        {
            RestoreRails(pushingPlayer.GetComponent<CapsuleCollider>());
        }
        playerActions = null;
        playerMotion = null;
        playerWeapon = null;
        pushingPlayer = null;
    }

    internal bool IsPushPending => pushTransaction != 0 && !pushCommitted;

    internal static void PlaySound(AudioEventData sound, Vector3 point)
    {
        if (sound != null && AudioManager.HasInstance)
        {
            AudioManager.Instance.Play3D(sound, point);
        }
    }

    internal void Report(VillagerController actor, string stage)
    {
        if (EnemyCombatTimeline.IsRecord(actor))
        {
            EnemyCombatTimeline.Record(actor, "木梯通行", name + " " + stage);
        }
    }

    private static string RoleName(Assignment role)
    {
        if (role == Assignment.Raise)
        {
            return "扶梯";
        }

        return "攀爬";
    }

    private void SetLadderLift(float fraction)
    {
        liftFraction = Mathf.Clamp01(fraction);
        ladderVisual.rotation = Quaternion.AngleAxis(raisedAngle * liftFraction, downRotation * Vector3.right) * downRotation;
    }

    private void IgnoreRails(Collider actor)
    {
        if (actor == null)
        {
            return;
        }
        foreach (Collider rail in solidRails)
        {
            if (!Physics.GetIgnoreCollision(actor, rail))
            {
                Physics.IgnoreCollision(actor, rail, true);
                railIgnores.Add((actor, rail));
            }
        }
    }

    private void RestoreRails(Collider actor)
    {
        for (int i = railIgnores.Count - 1; i >= 0; i--)
        {
            var pair = railIgnores[i];
            if (actor == null || pair.actor == actor)
            {
                if (pair.actor != null && pair.rail != null)
                {
                    Physics.IgnoreCollision(pair.actor, pair.rail, false);
                }
                railIgnores.RemoveAt(i);
            }
        }
    }

    private void AddRail(Vector3 center, Vector3 size)
    {
        var rail = ladderVisual.gameObject.AddComponent<BoxCollider>();
        rail.center = center;
        rail.size = size;
        solidRails.Add(rail);
    }

    private void BuildVisual()
    {
        ladderVisual = new GameObject("木梯").transform;
        ladderVisual.SetParent(transform, true);
        ladderVisual.SetPositionAndRotation(RungOrigin, downRotation);
        AddRail(new Vector3(-.31f, ladderLength * .5f, 0f), new Vector3(.08f, ladderLength, .09f));
        AddRail(new Vector3(.31f, ladderLength * .5f, 0f), new Vector3(.08f, ladderLength, .09f));
        for (int rung = 0; rung <= LastRung; rung++)
        {
            AddRail(Vector3.up * (FirstRung + rung * RungSpacing), new Vector3(.58f, .06f, .08f));
        }
        var model = new GameObject("原版木梯模型");
        model.transform.SetParent(ladderVisual, false);
        Bounds bounds = configuration.ladderMesh.bounds;
        var scale = new Vector3(0.70f / bounds.size.x, ladderLength / bounds.size.y, 1f);
        model.transform.localScale = scale;
        model.transform.localPosition = -Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z), scale);
        model.AddComponent<MeshFilter>().sharedMesh = configuration.ladderMesh;
        model.AddComponent<MeshRenderer>().sharedMaterial = configuration.ladderMaterial;
    }
}
