using UnityEngine;

public partial class LeonInputBridge
{
    private enum GameplayIntentKind
    {
        Shoot,
        Reload,
        Interact,
        QuickSlot,
        Crouch,
        Parry,
        Count
    }

    private const double GameplayInputLifetime = 0.28;
    private const double PickupBufferedInputLifetime = 1.5;
    private const double ThrustFollowUpInputLifetime = 0.9;
    private const double ConfirmedBodyReactionLifetime = 1.6;

    private struct GameplayIntent
    {
        internal bool pending;
        internal double pressedAt;
        internal long sequence;
        internal InteractionPromptSnapshot target;
        internal WeaponType weapon;
        internal int slot;
        internal int pickupTransactionId;
        internal bool thrustPickup;
        internal bool thrustQuickSlot;
    }

    private readonly GameplayIntent[] gameplayIntents = new GameplayIntent[(int)GameplayIntentKind.Count];
    private long nextIntentSequence;
    private bool restoreHeldAim;
    private bool restoreHeldGuard;
    private int pickupInputTransaction;
    private bool pickupInputReleased;
    private bool pendingPickupAim;
    private bool isReplayingIntents;

    internal bool HasAimIntentForReplay
    {
        get
        {
            if (restoreHeldAim)
            {
                return true;
            }

            if (aimMode == 1)
            {
                return aimToggleActive;
            }

            return IsEffectiveAimPressed();
        }
    }

    private void ClearPickupBufferState()
    {
        pickupInputTransaction = 0;
        pickupInputReleased = false;
        pendingPickupAim = false;
    }

    private void ClearGameplayIntents()
    {
        System.Array.Clear(gameplayIntents, 0, gameplayIntents.Length);
        ClearPickupBufferState();
        restoreHeldAim = true;
        restoreHeldGuard = true;
    }

    private void DiscardGameplayIntent(GameplayIntentKind kind)
    {
        int channel = (int)kind;
        if (channel >= 0 && channel < gameplayIntents.Length)
        {
            gameplayIntents[channel] = default;
        }
    }

    private bool BufferPickupAim(bool aim)
    {
        if (actions == null || !actions.IsItemPickupActive)
        {
            return false;
        }

        if (CanUseGameplayInput())
        {
            int transactionId = actions.CurrentFullBodyTransactionId;
            if (pickupInputTransaction != transactionId)
            {
                ClearPickupBufferState();
                pickupInputTransaction = transactionId;
            }

            if (!pickupInputReleased)
            {
                pendingPickupAim = aim;
                restoreHeldAim = aim;
            }
        }

        return true;
    }

    internal bool HasBufferedPickupInput(int transactionId)
    {
        if (transactionId == 0)
        {
            return false;
        }

        if (pickupInputTransaction == transactionId && pendingPickupAim)
        {
            return true;
        }

        for (int index = 0; index < gameplayIntents.Length; index++)
        {
            if (gameplayIntents[index].pending && gameplayIntents[index].pickupTransactionId == transactionId)
            {
                return true;
            }
        }

        if (aimMode == 0 && IsEffectiveAimPressed())
        {
            return true;
        }

        if (IsEffectiveGuardPressed())
        {
            return true;
        }

        return false;
    }

    internal void ReleaseBufferedPickupInput(int transactionId)
    {
        if (transactionId == 0)
        {
            return;
        }

        if (pickupInputTransaction == transactionId)
        {
            pickupInputReleased = true;
        }

        for (int index = 0; index < gameplayIntents.Length; index++)
        {
            if (gameplayIntents[index].pending && gameplayIntents[index].pickupTransactionId == transactionId)
            {
                gameplayIntents[index].pickupTransactionId = 0;
            }
        }

        NotifyActionWindowOpened();
    }

    internal void DiscardPendingPickupInput(int transactionId)
    {
        if (transactionId == 0)
        {
            return;
        }

        if (pickupInputTransaction == transactionId)
        {
            ClearPickupBufferState();
            restoreHeldAim = false;
            restoreHeldGuard = false;
        }

        for (int index = 0; index < gameplayIntents.Length; index++)
        {
            if (gameplayIntents[index].pending && gameplayIntents[index].pickupTransactionId == transactionId)
            {
                gameplayIntents[index] = default;
            }
        }
    }

    internal void NotifyActionWindowOpened()
    {
        if (!isActiveAndEnabled || isReplayingIntents)
        {
            return;
        }

        RestoreMovementInput();
        ReplayGameplayIntents();
    }

    private bool GameplayActionRecovering(GameplayIntentKind kind)
    {
        if (actions != null && actions.IsItemPickupActive)
        {
            int activePickupTx = actions.CurrentFullBodyTransactionId;
            if (pickupInputTransaction == 0 || pickupInputTransaction != activePickupTx || !pickupInputReleased)
            {
                return true;
            }
        }

        if (kind == GameplayIntentKind.Shoot && combat != null && combat.knifeModule != null
            && combat.knifeModule.Active)
        {
            // 连刀仍由小刀动作自己的取消窗口消费。
            return false;
        }

        if (kind == GameplayIntentKind.Parry && combat != null && combat.knifeModule != null
            && combat.knifeModule.Active)
        {
            // 小刀连段期间按住格挡由小刀模块自己的取消窗口处理。
            return false;
        }

        if (actions == null || combat == null || !combat.CanAct() || actions.HasActiveFullBodyAction)
        {
            return true;
        }

        if (kind == GameplayIntentKind.Shoot || kind == GameplayIntentKind.Reload || kind == GameplayIntentKind.QuickSlot)
        {
            if (actions.HasPendingWeaponSwitch || actions.WaitingForWeaponExit || actions.IsWeaponSwitchInProgress)
            {
                return true;
            }
        }

        if (kind == GameplayIntentKind.Reload)
        {
            if (driver != null && driver.WeaponActionLayerWeight > 0.05f)
            {
                return true;
            }
        }

        if (kind == GameplayIntentKind.QuickSlot)
        {
            if (controller != null && (controller.currentState == LeonState.Shooting || controller.currentState == LeonState.Reloading))
            {
                return true;
            }

            if (driver != null && (driver.NativeW870FireCycleActive || driver.WeaponActionLayerWeight > 0.05f))
            {
                return true;
            }
        }

        if (kind == GameplayIntentKind.Parry)
        {
            if (combat.IsFireBlockedByWeaponAction())
            {
                return true;
            }

            if (controller != null && controller.currentState == LeonState.Reloading)
            {
                return true;
            }
        }

        return false;
    }

    private void DispatchGameplayIntent(GameplayIntentKind kind, double pressedAt, int slot = -1)
    {
        int channel = (int)kind;
        gameplayIntents[channel] = default;
        if (controller == null || actions == null || combat == null)
        {
            return;
        }

        int activePickupTx = 0;
        if (actions.IsItemPickupActive)
        {
            activePickupTx = actions.CurrentFullBodyTransactionId;
            if (pickupInputTransaction != activePickupTx)
            {
                ClearPickupBufferState();
                pickupInputTransaction = activePickupTx;
            }
        }

        var intent = new GameplayIntent
        {
            pending = true,
            pressedAt = pressedAt,
            sequence = ++nextIntentSequence,
            slot = slot,
            weapon = controller.currentWeapon,
            pickupTransactionId = activePickupTx
        };

        if (actions.HasPendingWeaponSwitch)
        {
            intent.weapon = actions.PendingWeaponType;
        }

        if (qteCoordinator != null && qteCoordinator.UsesNativeInteractionPrompts)
        {
            bool recovering = GameplayActionRecovering(kind);
            if (kind == GameplayIntentKind.Shoot)
            {
                intent.target = qteCoordinator.ReadFinisherInput(recovering);
            }
            else if (kind == GameplayIntentKind.Interact)
            {
                intent.target = qteCoordinator.ReadInteractionInput();
                if (!intent.target.IsValid)
                {
                    intent.target = qteCoordinator.ReadFinisherInput(recovering);
                }
            }
        }

        if (combat.knifeModule != null && combat.knifeModule.IsFinishingThrust)
        {
            intent.thrustPickup = kind == GameplayIntentKind.Interact
                && (intent.target.Target is ItemPickup || intent.target.Kind == InteractionPromptKind.None);
            intent.thrustQuickSlot = kind == GameplayIntentKind.QuickSlot;
        }

        if (!TryConsumeGameplayIntent(kind, intent, false)
            && intent.target.Kind != InteractionPromptKind.WindowLadderPush)
        {
            ClearMutuallyExclusiveIntents(kind);
            gameplayIntents[channel] = intent;
        }
    }

    private void ClearMutuallyExclusiveIntents(GameplayIntentKind incomingKind)
    {
        for (int index = 0; index < gameplayIntents.Length; index++)
        {
            if (index == (int)incomingKind)
            {
                continue;
            }

            if (!gameplayIntents[index].pending)
            {
                continue;
            }

            // 互斥主动动作只保留玩家最新有效意图，避免恢复帧先后触发两个冲突动作。
            gameplayIntents[index] = default;
        }
    }

    private void ReplayGameplayIntents()
    {
        if (isReplayingIntents)
        {
            return;
        }

        if (playerMap == null || !playerMap.enabled || !CanUseGameplayInput())
        {
            ClearGameplayIntents();
            return;
        }

        isReplayingIntents = true;
        try
        {
            bool pickupHolding = actions != null && actions.IsItemPickupActive
                && (pickupInputTransaction == 0
                    || pickupInputTransaction != actions.CurrentFullBodyTransactionId
                    || !pickupInputReleased);

            if (!pickupHolding && restoreHeldAim && !GameplayActionRecovering(GameplayIntentKind.Shoot))
            {
                restoreHeldAim = false;
                if (aimMode == 1)
                {
                    if (pendingPickupAim || aimToggleActive)
                    {
                        pendingPickupAim = false;
                        combat.HandleAim(aimToggleActive);
                    }
                }
                else
                {
                    bool aimHeld = IsEffectiveAimPressed();
                    pendingPickupAim = false;
                    combat.HandleAim(aimHeld);
                }
            }

            if (!pickupHolding)
            {
                RestoreHeldGuardInput();
            }

            double now = Time.timeAsDouble;
            int bestIndex = -1;
            long bestSequence = -1;
            for (int index = 0; index < gameplayIntents.Length; index++)
            {
                GameplayIntent intent = gameplayIntents[index];
                if (!intent.pending)
                {
                    continue;
                }

                bool pendingBody = intent.target.Kind == InteractionPromptKind.BodyTechnique && !intent.target.CanExecute;
                bool activePickupBound = intent.pickupTransactionId != 0
                    && actions != null
                    && actions.IsItemPickupActive
                    && actions.CurrentFullBodyTransactionId == intent.pickupTransactionId;
                double lifetime = GameplayInputLifetime;
                if (pendingBody)
                {
                    // 按下时已确定硬直归属。保留至该反应首个可接帧，敌人换动作即失效。
                    lifetime = ConfirmedBodyReactionLifetime;
                }
                else if (activePickupBound)
                {
                    lifetime = PickupBufferedInputLifetime;
                }
                else if (intent.thrustPickup || intent.thrustQuickSlot)
                {
                    lifetime = ThrustFollowUpInputLifetime;
                }

                if (now - intent.pressedAt > lifetime || (pendingBody && !CanKeepBodyIntent(intent.target)))
                {
                    gameplayIntents[index] = default;
                    continue;
                }

                if (intent.sequence > bestSequence)
                {
                    bestSequence = intent.sequence;
                    bestIndex = index;
                }
            }

            if (bestIndex >= 0)
            {
                GameplayIntent chosen = gameplayIntents[bestIndex];
                if (TryConsumeGameplayIntent((GameplayIntentKind)bestIndex, chosen, true))
                {
                    gameplayIntents[bestIndex] = default;
                }
            }

            if (pickupInputTransaction != 0 && pickupInputReleased
                && (actions == null || !actions.IsItemPickupActive))
            {
                ClearPickupBufferState();
            }
        }
        finally
        {
            isReplayingIntents = false;
        }
    }

    private bool CanKeepBodyIntent(InteractionPromptSnapshot target)
    {
        return target.IsValid && target.Target is VillagerQTEParticipant participant
            && participant.TryReadPendingBodyTechnique(controller, out int currentTechnique)
            && currentTechnique == target.Technique;
    }

    private void RestoreHeldGuardInput()
    {
        if (!restoreHeldGuard || parryAction == null || !parryAction.enabled || combat == null || combat.knifeModule == null)
        {
            return;
        }

        if (!IsEffectiveGuardPressed() || combat.knifeModule.GuardHold)
        {
            restoreHeldGuard = false;
            return;
        }

        if (actions != null && actions.HasActiveFullBodyAction)
        {
            return;
        }

        if (combat.knifeModule.CanResumeHeldGuard && (!controller.IsCrouching || controller.TrySetCrouching(false)))
        {
            restoreHeldGuard = false;
            combat.knifeModule.CommitHeldGuard(Time.timeAsDouble);
        }
    }

    private bool TryConsumeGameplayIntent(GameplayIntentKind kind, GameplayIntent intent, bool replay)
    {
        if (!CanUseGameplayInput() || combat == null)
        {
            return true;
        }

        if (controller.IsHeldByTrap)
        {
            if (kind == GameplayIntentKind.Interact && !replay)
            {
                controller.TrapAvatar.TryEscape();
            }
            return true;
        }

        if (intent.thrustPickup && (actions.IsHitTransactionActive || controller.currentState == LeonState.Staggered
            || controller.currentState == LeonState.Traversing || controller.currentState == LeonState.QTE))
        {
            return true;
        }

        if (intent.target.Kind != InteractionPromptKind.None && !intent.target.IsValid)
        {
            return true;
        }

        if (GameplayActionRecovering(kind))
        {
            if (kind == GameplayIntentKind.Shoot)
            {
                restoreHeldAim = true;
            }

            if (intent.target.Kind != InteractionPromptKind.BodyTechnique)
            {
                return false;
            }
        }

        if (intent.thrustPickup && intent.target.Kind == InteractionPromptKind.None && qteCoordinator != null)
        {
            // 掉落可能在按下交互键后的刀刃接触帧才生成。
            intent.target = qteCoordinator.ReadThrustFollowUpPickup();
            if (!intent.target.IsValid)
            {
                return false;
            }
        }

        // 已有目标时，重放沿用按下时的目标和敌人动作编号。
        if (intent.target.IsValid)
        {
            if (intent.target.Kind == InteractionPromptKind.GroundFinisher)
            {
                return qteCoordinator.TryStartGroundFinisherInput(intent.target);
            }
            return qteCoordinator.TryExecuteInputInteraction(intent.target);
        }

        switch (kind)
        {
            case GameplayIntentKind.Shoot:
                if (!replay || controller.currentWeapon == intent.weapon)
                {
                    if (!combat.TryCommitAttackInput(intent.pressedAt))
                    {
                        return false;
                    }

                    if (replay && shootAction != null && !shootAction.IsPressed())
                    {
                        combat.CommitAttackReleased(Time.timeAsDouble);
                    }
                }
                break;
            case GameplayIntentKind.Reload:
                if (!replay || controller.currentWeapon == intent.weapon)
                {
                    if (!combat.TryHandleReload())
                    {
                        return false;
                    }
                }
                break;
            case GameplayIntentKind.QuickSlot:
                if (CanUseQuickSelect() && weaponWheel.IsSlotAvailable(intent.slot))
                {
                    weaponWheel.SelectDirectly(intent.slot);
                }
                break;
            case GameplayIntentKind.Crouch:
                if (!controller.ToggleCrouch())
                {
                    return false;
                }
                break;
            case GameplayIntentKind.Parry:
                if (controller.IsCrouching && !controller.TrySetCrouching(false))
                {
                    restoreHeldGuard = true;
                    return false;
                }

                restoreHeldGuard = true;
                combat.CommitGuardInput(true, intent.pressedAt);
                break;
            case GameplayIntentKind.Interact:
                if (qteCoordinator != null && qteCoordinator.UsesNativeInteractionPrompts)
                {
                    qteCoordinator.TryExecuteSnapshotInteraction();
                }
                else if (qteCoordinator == null || (!qteCoordinator.TryStartNearestBodyTechnique(out _)
                    && !qteCoordinator.TryStartNearestPrompt(out _)))
                {
                    InteractionSystem.InteractNearby(controller.transform);
                }
                break;
        }

        return true;
    }
}
