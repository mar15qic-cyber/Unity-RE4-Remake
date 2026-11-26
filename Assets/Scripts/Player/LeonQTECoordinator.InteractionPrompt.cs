using GameArchitecture;
using UnityEngine;

public partial class LeonQTECoordinator
{
    private Camera promptCamera;
    private System.Predicate<IInteractable> acceptVisiblePrompt;
    private Transform promptPlayerRoot;
    private static readonly HitQueryBuffer promptVisibilityHits = new();
    private readonly HitQueryBuffer groundFinisherHits = new();

    internal Camera ReadPromptCamera()
    {
        if (promptCamera == null || !promptCamera.isActiveAndEnabled)
        {
            promptCamera = Camera.main;
        }
        return promptCamera;
    }

    private bool IsPromptVisible(Camera camera, Component target, Vector3 position)
    {
        if (promptPlayerRoot == null && controller != null)
        {
            promptPlayerRoot = controller.transform;
        }
        return IsPromptVisible(camera, target, position, promptPlayerRoot);
    }

    private int promptSnapshotFrame = -1;
    private InteractionPromptSnapshot interactSnapshot;
    private InteractionPromptSnapshot finisherSnapshot;
    private InteractionPromptSnapshot displayedFinisher;
    private int displayedFinisherFrame = -2;
    private InteractionPromptSnapshot distantSnapshot;
    private InteractionPromptSnapshot pendingBodySnapshot;
    private InteractionPromptSnapshot pendingFinisherSnapshot;
    private WindowBarricade heldCabinet;
    private readonly System.Collections.Generic.List<IInteractable> pickupInputCandidates = new();

    internal bool UsesNativeInteractionPrompts => config != null && config.useNativeGroundFinisher;

    internal bool TryReadInteractionPrompts(out InteractionPromptSnapshot interact, out InteractionPromptSnapshot finisher,
        out InteractionPromptSnapshot distant)
    {
        RefreshInteractionSnapshots();
        interact = interactSnapshot;
        finisher = finisherSnapshot;
        displayedFinisher = finisher;
        displayedFinisherFrame = Time.frameCount;
        distant = distantSnapshot;
        return interact.IsValid || finisher.IsValid || distant.IsValid;
    }

    private bool PromptInputBlocked()
    {
        return !started || IsActive || controller == null || controller.currentState == LeonState.Dead
            || GameManager.HasInstance && (GameManager.Instance.IsTerminal || !GameManager.Instance.Permission.Current.AllowInput)
            || Time.timeScale <= 0f || GameUIManager.HasInstance && !GameUIManager.Instance.CanPlayerAct()
            || InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading)
            || StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible;
    }

    private void RefreshInteractionSnapshots()
    {
        if (promptSnapshotFrame == Time.frameCount)
        {
            return;
        }
        promptSnapshotFrame = Time.frameCount;
        interactSnapshot = default;
        finisherSnapshot = default;
        distantSnapshot = default;
        pendingBodySnapshot = default;
        pendingFinisherSnapshot = default;
        if (PromptInputBlocked())
        {
            return;
        }
        if (heldCabinet != null && heldCabinet.HasActivePush)
        {
            if (heldCabinet.ShowsHoldPrompt)
            {
                interactSnapshot = new InteractionPromptSnapshot(heldCabinet, default, InteractionPromptKind.Interact,
                    true, "Interact", heldCabinet.GetPromptText(), heldCabinet.PromptPosition);
            }
            return;
        }
        heldCabinet = null;
        Camera camera = ReadPromptCamera();
        if (camera == null)
        {
            return;
        }
        float bodyDistance = float.MaxValue;
        float finisherDistance = float.MaxValue;
        float distantDistance = 8f;
        float pendingBodyDistance = float.PositiveInfinity;
        float pendingFinisherDistance = float.PositiveInfinity;
        bool canBody = CanQueryBodyTechniquePrompt();
        bool canKnife = CanQueryAssassinationPrompt() && combat != null && combat.knifeModule != null && combat.knifeModule.ValidKnife;
        bool hasFinisherKnife = UsesNativeInteractionPrompts && combat != null && !combat.IsAiming
            && combat.knifeModule != null && combat.knifeModule.HasGroundFinisherKnife;
        foreach (var target in GetPromptCandidates())
        {
            if (target == null || !target.IsAlive || target.controller == null || target.controller.actionCoordinator == null)
            {
                continue;
            }
            float distance = Vector3.Distance(transform.position, target.Root.position);
            if (distance > 8f)
            {
                continue;
            }
            bool isSawman = target.controller.missionEnemyType == MissionEnemyType.Sawman;
            Vector3 bodyPosition = target.Root.position + Vector3.up * 1.85f;
            Vector3 finisherPosition;
            if (isSawman)
            {
                finisherPosition = target.Root.position + Vector3.up * 1.25f;
            }
            else
            {
                finisherPosition = target.Root.position + Vector3.up * .8f;
            }
            bool ground = hasFinisherKnife && target.TryReadGroundFinisher(out _);
            bool finisherVisible = ground && IsPromptVisible(camera, target, finisherPosition);
            bool bodyVisible = IsPromptVisible(camera, target, bodyPosition);
            if (!finisherVisible && !bodyVisible)
            {
                continue;
            }
            ActionHandle action = target.controller.actionCoordinator.CurrentHandle;
            QTECancelReason finisherReason = QTECancelReason.None;
            bool canFinish = finisherVisible && canKnife && CanStart(QTEKind.GroundFinisher, target, out finisherReason);
            if (canFinish)
            {
                if (distance < finisherDistance || distance == finisherDistance
                    && finisherSnapshot.Target != null && target.GetInstanceID() < finisherSnapshot.Target.GetInstanceID())
                {
                    finisherDistance = distance;
                    finisherSnapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.GroundFinisher,
                        true, "Shoot", "补刀", finisherPosition);
                }
            }
            if (finisherVisible && !canFinish)
            {
                float planarDistance = Vector3.ProjectOnPlane(transform.position - target.Root.position, Vector3.up).magnitude;
                float reachDistance = config.groundFinisherDistance;
                if (isSawman)
                {
                    reachDistance = Mathf.Max(reachDistance, 2.25f);
                }
                bool withinReach = planarDistance <= reachDistance;
                if (withinReach && !canKnife && distance < pendingFinisherDistance
                    && ValidatePairing(target, QTEKind.GroundFinisher, out _))
                {
                    pendingFinisherDistance = distance;
                    pendingFinisherSnapshot = new InteractionPromptSnapshot(target, action,
                        InteractionPromptKind.GroundFinisher, true, "Shoot", "补刀", finisherPosition);
                }
            }
            if (bodyVisible && distance < pendingBodyDistance && target.TryReadPendingBodyTechnique(controller, out int pendingTechnique))
            {
                pendingBodyDistance = distance;
                pendingBodySnapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.BodyTechnique,
                    false, "Interact", "体术", bodyPosition, pendingTechnique);
            }
            bool suppressBodyForSawmanFinisher = isSawman && hasFinisherKnife && canKnife && ground;
            if (!suppressBodyForSawmanFinisher && bodyVisible && canBody && target.TryReadBodyTechniqueOpportunity(out int technique))
            {
                bool near = target.CanStartBodyTechnique(controller, out technique, out _);
                var snapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.BodyTechnique, near, "Interact", "体术", bodyPosition, technique);
                if (near && distance < bodyDistance)
                {
                    bodyDistance = distance;
                    interactSnapshot = snapshot;
                }
                else if (!near && distance < distantDistance)
                {
                    distantDistance = distance;
                    distantSnapshot = snapshot;
                }
            }
        }
        var ladder = VillageLadderSite.FindAvailablePush(transform);
        if (ladder != null)
        {
            interactSnapshot = new InteractionPromptSnapshot(ladder, default, InteractionPromptKind.Interact, true,
                "Interact", ladder.GetPromptText(), ladder.PromptPosition);
            return;
        }
        if (canKnife)
        {
            float closest = bodyDistance;
            float maxScanDistance = config.assassinationStartDistance + Mathf.Max(0f, config.assassinationTurnGraceDistanceBonus);
            foreach (var target in GetPromptCandidates())
            {
                if (target == null || !target.CanStart(controller, QTEKind.KnifeAssassination, out _)
                    || !ValidatePairing(target, QTEKind.KnifeAssassination, out _))
                {
                    continue;
                }
                float distance = Vector3.Distance(transform.position, target.Root.position);
                if (distance > maxScanDistance)
                {
                    continue;
                }
                Vector3 position = target.Root.position + Vector3.up * 1.5f;
                if (distance < closest && IsPromptVisible(camera, target, position))
                {
                    closest = distance;
                    string label = "暗杀";
                    if (target.controller != null && target.controller.CanBeStaggerBackstabbedBy(controller))
                    {
                        label = "背刺";
                    }
                    interactSnapshot = new InteractionPromptSnapshot(target, target.controller.actionCoordinator.CurrentHandle,
                        InteractionPromptKind.Assassination, true, "Interact", label, position);
                }
            }
        }
        if (!interactSnapshot.IsValid)
        {
            if (acceptVisiblePrompt == null)
            {
                acceptVisiblePrompt = IsVisiblePromptCandidate;
            }
            var nearby = InteractionSystem.FindNearby(transform, acceptVisiblePrompt);
            if (nearby is Component component)
            {
                interactSnapshot = new InteractionPromptSnapshot(component, default, InteractionPromptKind.Interact,
                    true, "Interact", nearby.GetPromptText(), ReadWorldPromptPosition(component));
            }

            SelectWindowLadderPush(camera);
        }
    }

    private bool IsVisiblePromptCandidate(IInteractable item)
    {
        return item is Component component && component is not VillageLadderSite
            && ((component is not VillageEntranceGate && IsPassageInteraction(component))
                || IsPromptVisible(promptCamera, component, ReadWorldPromptPosition(component)));
    }

    private void SelectWindowLadderPush(Camera camera)
    {
        InteractionSystem.CopyRegistered(pickupInputCandidates);
        float currentScore = float.PositiveInfinity;
        if (interactSnapshot.IsValid)
        {
            currentScore = ViewportCenterDistance(camera, interactSnapshot.Position);
        }

        foreach (var candidate in pickupInputCandidates)
        {
            if (candidate is not VillageLadderSite site || !site.CanInteract(transform) || !site.IsWindowPushCandidate)
            {
                continue;
            }

            Vector3 position = site.PromptPosition;
            float score = ViewportCenterDistance(camera, position);
            if (score < currentScore && IsPromptVisible(camera, site, position))
            {
                currentScore = score;
                interactSnapshot = new InteractionPromptSnapshot(site, default, InteractionPromptKind.WindowLadderPush,
                    true, "Interact", site.GetPromptText(), position);
            }
        }
        pickupInputCandidates.Clear();
    }

    private static float ViewportCenterDistance(Camera camera, Vector3 position)
    {
        Vector3 point = camera.WorldToViewportPoint(position);
        if (point.z <= 0f)
        {
            return float.PositiveInfinity;
        }
        return (new Vector2(point.x, point.y) - new Vector2(.5f, .5f)).sqrMagnitude;
    }

    private static Vector3 ReadWorldPromptPosition(Component component)
    {
        if (component is WindowBarricade cabinet)
        {
            return cabinet.PromptPosition;
        }
        if (component is VillageEntranceGate gate)
        {
            return gate.PromptPosition;
        }
        if (component is DoorController door)
        {
            return door.PassageCenter;
        }
        if (component is WindowTraversalPoint window)
        {
            return window.OpeningCenterWorld;
        }
        if (component is StorageBoxController storage)
        {
            return storage.PromptPosition;
        }
        return component.transform.position + Vector3.up * .35f;
    }

    internal InteractionPromptSnapshot ReadInteractionInput()
    {
        promptSnapshotFrame = -1;
        RefreshInteractionSnapshots();
        if (interactSnapshot.IsValid)
        {
            return interactSnapshot;
        }
        if (!pendingBodySnapshot.IsValid && !PromptInputBlocked()
            && combat != null && combat.knifeModule != null
            && combat.knifeModule.IsFinishingThrust)
        {
            return ReadThrustFollowUpPickup();
        }
        return pendingBodySnapshot;
    }

    internal InteractionPromptSnapshot ReadThrustFollowUpPickup()
    {
        Camera camera = ReadPromptCamera();
        if (camera == null || !TryGetComponent<LeonItemPickup>(out var pickupAction))
        {
            return default;
        }
        InteractionSystem.CopyRegistered(pickupInputCandidates);
        ItemPickup closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (var candidate in pickupInputCandidates)
        {
            if (candidate is not ItemPickup pickup || !pickupAction.CanReachAfterAction(pickup))
            {
                continue;
            }
            Vector3 offset = pickup.ContactPoint - transform.position;
            float distance = offset.sqrMagnitude;
            Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            // 箱子的掉落可能正好落到越肩画面下沿外，伸手净空仍由拾取检查确认。
            bool nearFront = distance <= 1.35f * 1.35f && Vector3.Dot(transform.forward, horizontal.normalized) >= .5f;
            if (distance < closestDistance && (nearFront || IsPromptVisible(camera, pickup, ReadWorldPromptPosition(pickup))))
            {
                closest = pickup;
                closestDistance = distance;
            }
        }
        pickupInputCandidates.Clear();
        if (closest == null)
        {
            return default;
        }
        // 只将已经存在且可触及的物品交给按键缓冲，回放时仍由拾取入口复核。
        return new InteractionPromptSnapshot(closest, default, InteractionPromptKind.Interact, false,
            "Interact", closest.GetPromptText(), ReadWorldPromptPosition(closest));
    }

    internal InteractionPromptSnapshot ReadFinisherInput(bool includeRecovering = false)
    {
        // 玩家按下的是上一张可见卡片；目标刚恢复或离开时，不能把这一击移给旁边的敌人。
        if (displayedFinisherFrame >= Time.frameCount - 1 && displayedFinisher.Kind == InteractionPromptKind.GroundFinisher)
        {
            return displayedFinisher;
        }
        promptSnapshotFrame = -1;
        RefreshInteractionSnapshots();
        if (includeRecovering && !finisherSnapshot.IsValid)
        {
            return pendingFinisherSnapshot;
        }
        return finisherSnapshot;
    }

    internal bool TryExecuteSnapshotInteraction()
    {
        return TryExecuteInputInteraction(ReadInteractionInput());
    }

    internal bool TryExecuteInputInteraction(InteractionPromptSnapshot request)
    {
        if (PromptInputBlocked() || !request.IsValid)
        {
            return false;
        }
        if (request.Kind == InteractionPromptKind.BodyTechnique && request.Target is VillagerQTEParticipant body)
        {
            return TryStartBodyTechnique(body, request.Technique, out _);
        }
        if (request.Kind == InteractionPromptKind.Assassination && request.Target is VillagerQTEParticipant assassin)
        {
            return TryStart(QTEKind.KnifeAssassination, assassin, out _);
        }
        if (request.Kind == InteractionPromptKind.WindowLadderPush && request.Target is VillageLadderSite ladder)
        {
            Camera camera = controller.playerCamera;
            if (camera == null || !ladder.CanInteract(transform) || !ladder.IsWindowPushCandidate
                || !IsPromptVisible(camera, ladder, ladder.PromptPosition))
            {
                return false;
            }
            ladder.Interact();
            return true;
        }
        if (request.Kind == InteractionPromptKind.Interact && request.Target is IInteractable interactable
            && interactable.CanInteract(transform))
        {
            interactable.Interact();
            if (interactable is WindowBarricade cabinet && cabinet.HasActivePush)
            {
                heldCabinet = cabinet;
                promptSnapshotFrame = -1;
            }
            return true;
        }
        return false;
    }

    internal static bool IsPassageInteraction(Component target)
    {
        return target is DoorController or VillageEntranceGate or WindowTraversalPoint;
    }

    internal static bool IsPromptVisible(Camera camera, Component target, Vector3 position, Transform playerRoot)
    {
        Vector3 viewport = camera.WorldToViewportPoint(position);
        if (viewport.z <= 0f || viewport.x < .02f || viewport.x > .98f || viewport.y < .02f || viewport.y > .98f)
        {
            return false;
        }
        Vector3 direction = position - camera.transform.position;
        RaycastHit[] hits = promptVisibilityHits.Raycast(camera.transform.position, direction.normalized, direction.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore, out int hitCount);
        Transform targetRoot = target.transform;
        for (int index = 0; index < hitCount; index++)
        {
            Transform hitTransform = hits[index].transform;
            if (hitTransform.IsChildOf(targetRoot) || targetRoot.IsChildOf(hitTransform)
                || (playerRoot != null && hitTransform.IsChildOf(playerRoot)))
            {
                continue;
            }
            return false;
        }
        return true;
    }
}
