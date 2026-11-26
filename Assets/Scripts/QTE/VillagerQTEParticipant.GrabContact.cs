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
        if (kind == QTEKind.RearBiteNeck)
        {
            return IsValidRearGrabGeometry(
                transform.position,
                transform.forward,
                player.transform.position,
                player.transform.forward,
                player.IsCrouching);
        }

        bool behind = Vector3.Dot(player.transform.forward, transform.position - player.transform.position) < 0f;
        return !behind;
    }

    /// <summary>校验背后环抱几何条件：玩家未蹲伏、敌人在玩家背后 ±65° 扇区内且正对玩家。</summary>
    internal static bool IsValidRearGrabGeometry(Vector3 enemyPosition, Vector3 enemyForward,
        Vector3 playerPosition, Vector3 playerForward, bool playerIsCrouching)
    {
        if (playerIsCrouching)
        {
            return false;
        }

        Vector3 toEnemy = enemyPosition - playerPosition;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 toEnemyNormalized = toEnemy.normalized;
        Vector3 toPlayerNormalized = -toEnemyNormalized;

        Vector3 playerForwardXZ = playerForward;
        playerForwardXZ.y = 0f;
        if (playerForwardXZ.sqrMagnitude <= 0.0001f)
        {
            return false;
        }
        playerForwardXZ.Normalize();

        Vector3 enemyForwardXZ = enemyForward;
        enemyForwardXZ.y = 0f;
        if (enemyForwardXZ.sqrMagnitude <= 0.0001f)
        {
            return false;
        }
        enemyForwardXZ.Normalize();

        // cos(180° - 65°) = cos(115°) ≈ -0.422618；敌人在玩家背后 ±65° 扇区内。
        float playerBehindDot = Vector3.Dot(playerForwardXZ, toEnemyNormalized);
        if (playerBehindDot > -0.422618f)
        {
            return false;
        }

        // 敌人正面朝向玩家（±60° 以内）。
        float enemyFacingDot = Vector3.Dot(enemyForwardXZ, toPlayerNormalized);
        return enemyFacingDot >= 0.5f;
    }
}
