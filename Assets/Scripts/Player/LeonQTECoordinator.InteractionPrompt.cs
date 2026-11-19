using GameArchitecture;
using UnityEngine;

public partial class LeonQTECoordinator
{
    private int promptSnapshotFrame = -1;
    private InteractionPromptSnapshot interactSnapshot;
    private InteractionPromptSnapshot finisherSnapshot;
    private InteractionPromptSnapshot distantSnapshot;
    private InteractionPromptSnapshot pendingBodySnapshot;
    private InteractionPromptSnapshot pendingFinisherSnapshot;
    private readonly System.Collections.Generic.List<IInteractable> pickupInputCandidates = new();

    internal bool UsesNativeInteractionPrompts => config != null && config.useNativeGroundFinisher;

    internal bool TryReadInteractionPrompts(out InteractionPromptSnapshot interact, out InteractionPromptSnapshot finisher,
        out InteractionPromptSnapshot distant)
    {
        RefreshInteractionSnapshots();
        interact = interactSnapshot;
        finisher = finisherSnapshot;
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
        Camera camera = Camera.main;
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
            && combat.knifeModule != null && combat.knifeModule.ValidKnife;
        foreach (var target in GetPromptCandidates())
        {
            if (target == null || !target.IsAlive || target.controller == null || target.controller.actionCoordinator == null)
            {
                continue;
            }
            float distance = Vector3.Distance(transform.position, target.Root.position);
            Vector3 position = target.Root.position + Vector3.up * 1.8f;
            bool ground = hasFinisherKnife && target.TryReadGroundFinisher(out _);
            if (ground)
            {
                position = target.Root.position + Vector3.up * .8f;
            }
            if (distance > 8f)
            {
                continue;
            }
            ActionHandle action = target.controller.actionCoordinator.CurrentHandle;
            QTECancelReason finisherReason = QTECancelReason.None;
            bool canFinish = ground && canKnife && CanStart(QTEKind.GroundFinisher, target, out finisherReason);
            if (canFinish)
            {
                if (distance < finisherDistance)
                {
                    finisherDistance = distance;
                    finisherSnapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.GroundFinisher,
                        true, "Shoot", "补刀", position);
                }
            }
            if (!IsPromptVisible(camera, target, position))
            {
                continue;
            }
            if (ground && !canFinish)
            {
                float planarDistance = Vector3.ProjectOnPlane(transform.position - target.Root.position, Vector3.up).magnitude;
                bool withinReach = planarDistance <= config.groundFinisherDistance;
                if (withinReach && !canKnife && distance < pendingFinisherDistance
                    && ValidatePairing(target, QTEKind.GroundFinisher, out _))
                {
                    pendingFinisherDistance = distance;
                    pendingFinisherSnapshot = new InteractionPromptSnapshot(target, action,
                        InteractionPromptKind.GroundFinisher, true, "Shoot", "补刀", position);
                }
                if (distance < distantDistance && !withinReach && CanApproachGroundFinisher(target))
                {
                    distantDistance = distance;
                    distantSnapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.GroundFinisher,
                        false, "Shoot", "补刀", position);
                }
            }
            if (distance < pendingBodyDistance && target.TryReadPendingBodyTechnique(controller, out int pendingTechnique))
            {
                pendingBodyDistance = distance;
                pendingBodySnapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.BodyTechnique,
                    false, "Interact", "体术", position, pendingTechnique);
            }
            if (canBody && target.TryReadBodyTechniqueOpportunity(out int technique))
            {
                bool near = target.CanStartBodyTechnique(controller, out technique, out _);
                var snapshot = new InteractionPromptSnapshot(target, action, InteractionPromptKind.BodyTechnique, near, "Interact", "体术", position, technique);
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
        if (interactSnapshot.IsValid)
        {
            return;
        }
        if (canKnife)
        {
            float closest = config.assassinationStartDistance;
            foreach (var target in GetPromptCandidates())
            {
                if (target == null || !target.CanStart(controller, QTEKind.KnifeAssassination, out _)
                    || !ValidatePairing(target, QTEKind.KnifeAssassination, out _))
                {
                    continue;
                }
                float distance = Vector3.Distance(transform.position, target.Root.position);
                Vector3 position = target.Root.position + Vector3.up * 1.5f;
                if (distance < closest && IsPromptVisible(camera, target, position))
                {
                    closest = distance;
                    interactSnapshot = new InteractionPromptSnapshot(target, target.controller.actionCoordinator.CurrentHandle,
                        InteractionPromptKind.Assassination, true, "Interact", "暗杀", position);
                }
            }
        }
        if (!interactSnapshot.IsValid)
        {
            var nearby = InteractionSystem.FindNearby(transform, item =>
            {
                return item is Component component && component is not VillageLadderSite
                    && ((component is not VillageEntranceGate && IsPassageInteraction(component))
                        || IsPromptVisible(camera, component, ReadWorldPromptPosition(component)));
            });
            if (nearby is Component component)
            {
                interactSnapshot = new InteractionPromptSnapshot(component, default, InteractionPromptKind.Interact,
                    true, "Interact", nearby.GetPromptText(), ReadWorldPromptPosition(component));
            }

            SelectWindowLadderPush(camera);
        }
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

    internal InteractionPromptSnapshot ReadInteractionInput(bool forMelee)
    {
        promptSnapshotFrame = -1;
        RefreshInteractionSnapshots();
        if (interactSnapshot.IsValid && (!forMelee
            || interactSnapshot.Kind != InteractionPromptKind.Interact && interactSnapshot.Kind != InteractionPromptKind.WindowLadderPush))
        {
            return interactSnapshot;
        }
        if (!forMelee && !pendingBodySnapshot.IsValid && !PromptInputBlocked()
            && combat != null && combat.knifeModule != null
            && KnifeCombatRules.IsThrust(combat.knifeModule.CurrentAction))
        {
            return ReadPickupInputDuringThrust();
        }
        return pendingBodySnapshot;
    }

    private InteractionPromptSnapshot ReadPickupInputDuringThrust()
    {
        Camera camera = Camera.main;
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
            float distance = (pickup.ContactPoint - transform.position).sqrMagnitude;
            if (distance < closestDistance && IsPromptVisible(camera, pickup, ReadWorldPromptPosition(pickup)))
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
        return TryExecuteInputInteraction(ReadInteractionInput(false));
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
            return true;
        }
        return false;
    }

    internal static bool IsPassageInteraction(Component target)
    {
        return target is DoorController or VillageEntranceGate or WindowTraversalPoint;
    }

    internal static bool IsPromptVisible(Camera camera, Component target, Vector3 position)
    {
        Vector3 viewport = camera.WorldToViewportPoint(position);
        if (viewport.z <= 0f || viewport.x < .02f || viewport.x > .98f || viewport.y < .02f || viewport.y > .98f)
        {
            return false;
        }
        Vector3 direction = position - camera.transform.position;
        foreach (RaycastHit hit in Physics.RaycastAll(camera.transform.position, direction.normalized, direction.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(target.transform) || target.transform.IsChildOf(hit.transform)
                || hit.collider.GetComponentInParent<LeonController>() != null)
            {
                continue;
            }
            return false;
        }
        return true;
    }
}
