using GameArchitecture;
using UnityEngine;

public partial class VillagerQTEParticipant
{
    private ActionHandle confirmedGrabAction;
    private LeonController confirmedGrabPlayer;
    private QTEKind confirmedGrabKind;
    private int confirmedGrabFrame = -1;

    internal void BeginConfirmedGrabContact(ActionHandle action, LeonController player, QTEKind kind)
    {
        confirmedGrabAction = action;
        confirmedGrabPlayer = player;
        confirmedGrabKind = kind;
        confirmedGrabFrame = Time.frameCount;
    }

    internal void EndConfirmedGrabContact()
    {
        confirmedGrabAction = default;
        confirmedGrabPlayer = null;
        confirmedGrabFrame = -1;
    }

    internal bool HasConfirmedGrabContact(LeonController player, QTEKind kind)
    {
        return controller != null && controller.actionCoordinator != null
            && confirmedGrabAction.Valid && confirmedGrabAction == controller.actionCoordinator.CurrentHandle
            && confirmedGrabFrame == Time.frameCount && confirmedGrabPlayer == player && confirmedGrabKind == kind;
    }

    internal bool CanWindupGrab(LeonController player, QTEKind kind)
    {
        if (controller == null || controller.combatModule == null || controller.combatModule.NativeCombatProfile == null)
        {
            return CanStart(player, kind, out _);
        }
        if (!IsAvailable || !IsAlive || player == null || player.currentState == LeonState.Dead
            || activeSessionId != 0 || activeBodyTechniqueSessionId != 0 || controller.combatModule.NativeCombatActive
            || !controller.CanEvaluateStandingQTEAction)
        {
            return false;
        }
        Vector3 delta = player.transform.position - transform.position;
        if (Mathf.Abs(delta.y) > .28f || delta.sqrMagnitude > 2.4f * 2.4f
            || controller.PlantNearHouseThreeTread(transform.position) || controller.PlantNearHouseThreeTread(player.transform.position))
        {
            return false;
        }
        bool behind = Vector3.Dot(player.transform.forward, transform.position - player.transform.position) < 0f;
        if (kind == QTEKind.RearBiteNeck)
        {
            return behind;
        }
        return !behind;
    }
}
