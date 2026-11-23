using UnityEngine;

public partial class LeonInputBridge
{
    private enum GameplayIntentKind { Shoot, Reload, Interact, QuickSlot, Count }
    private const double GameplayInputLifetime = .22;
    private const double ThrustFollowUpInputLifetime = .9;
    private const double ConfirmedBodyReactionLifetime = 1.6;

    private struct GameplayIntent
    {
        internal bool pending;
        internal double pressedAt;
        internal InteractionPromptSnapshot target;
        internal WeaponType weapon;
        internal int slot;
        internal bool thrustPickup;
        internal bool thrustQuickSlot;
    }

    private readonly GameplayIntent[] gameplayIntents = new GameplayIntent[(int)GameplayIntentKind.Count];
    private bool restoreHeldAim;
    private bool restoreHeldGuard;

    private void ClearGameplayIntents()
    {
        System.Array.Clear(gameplayIntents, 0, gameplayIntents.Length);
        restoreHeldAim = true;
        restoreHeldGuard = true;
    }

    private bool GameplayActionRecovering(GameplayIntentKind kind)
    {
        if (kind == GameplayIntentKind.Shoot && combat != null && combat.knifeModule != null
            && combat.knifeModule.Active)
        {
            // 连刀仍由小刀动作自己的取消窗口消费。
            return false;
        }

        if (actions == null || combat == null || !combat.CanAct() || actions.HasActiveFullBodyAction)
        {
            return true;
        }
        if (kind is GameplayIntentKind.Shoot or GameplayIntentKind.Reload)
        {
            return actions.HasPendingWeaponSwitch || actions.WaitingForWeaponExit || actions.IsWeaponSwitchInProgress;
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

        var intent = new GameplayIntent
        {
            pending = true,
            pressedAt = pressedAt,
            slot = slot,
            weapon = controller.currentWeapon
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
            gameplayIntents[channel] = intent;
        }
    }

    private void ReplayGameplayIntents()
    {
        if (playerMap == null || !playerMap.enabled || !CanUsePickupInput())
        {
            ClearGameplayIntents();
            return;
        }

        if (restoreHeldAim && !GameplayActionRecovering(GameplayIntentKind.Shoot)
            && aimAction != null && aimAction.enabled)
        {
            restoreHeldAim = false;
            if (aimMode == 0)
            {
                combat.HandleAim(aimAction.IsPressed());
            }
        }

        RestoreHeldGuardInput();
        double now = Time.timeAsDouble;
        for (int index = 0; index < gameplayIntents.Length; index++)
        {
            GameplayIntent intent = gameplayIntents[index];
            if (!intent.pending)
            {
                continue;
            }
            bool pendingBody = intent.target.Kind == InteractionPromptKind.BodyTechnique && !intent.target.CanExecute;
            double lifetime = GameplayInputLifetime;
            if (pendingBody)
            {
                // 按下时已确定硬直归属。保留至该反应首个可接帧，敌人换动作即失效。
                lifetime = ConfirmedBodyReactionLifetime;
            }
            else if (intent.thrustPickup || intent.thrustQuickSlot)
            {
                lifetime = ThrustFollowUpInputLifetime;
            }
            if (now - intent.pressedAt > lifetime || pendingBody && !CanKeepBodyIntent(intent.target)
                || TryConsumeGameplayIntent((GameplayIntentKind)index, intent, true))
            {
                gameplayIntents[index] = default;
            }
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
        if (!parryAction.IsPressed() || combat.knifeModule.GuardHold)
        {
            restoreHeldGuard = false;
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
        if (!CanUsePickupInput() || combat == null)
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
            restoreHeldAim = true;
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
                    combat.CommitAttackInput(intent.pressedAt);
                    if (replay && shootAction != null && !shootAction.IsPressed())
                    {
                        combat.CommitAttackReleased(Time.timeAsDouble);
                    }
                }
                break;
            case GameplayIntentKind.Reload:
                if (!replay || controller.currentWeapon == intent.weapon)
                {
                    combat.HandleReload();
                }
                break;
            case GameplayIntentKind.QuickSlot:
                if (CanUseQuickSelect() && weaponWheel.IsSlotAvailable(intent.slot))
                {
                    weaponWheel.SelectDirectly(intent.slot);
                }
                break;
            case GameplayIntentKind.Interact:
                if (!replay && (qteCoordinator == null || !qteCoordinator.UsesNativeInteractionPrompts)
                    && (qteCoordinator == null || !qteCoordinator.TryStartNearestBodyTechnique(out _)
                        && !qteCoordinator.TryStartNearestPrompt(out _)))
                {
                    InteractionSystem.InteractNearby(controller.transform);
                }
                break;
        }
        return true;
    }
}
