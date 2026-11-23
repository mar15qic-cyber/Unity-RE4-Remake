using UnityEngine;

public sealed partial class EnemyCombatModule
{
    private DoorController siegeAttackDoor;
    private bool siegeAttackLanded;

    private DoorController ResolveSiegeDoor()
    {
        HouseThreeTraversalGeometry geometry = host.door03TraversalRoute;
        if (chainsawBridge == null || geometry == null || !geometry.IsReady || Player == null ||
            !geometry.entranceDoor.IsSiegeLatched || !geometry.IsOnExteriorSide(transform.position))
        {
            return null;
        }

        Vector3 toDoor = geometry.DoorGate - transform.position;
        if (Mathf.Abs(toDoor.y) > 0.5f)
        {
            return null;
        }

        toDoor.y = 0f;
        if (toDoor.sqrMagnitude > 2.56f || Vector3.Angle(transform.forward, toDoor) > 65f)
        {
            return null;
        }

        return geometry.entranceDoor;
    }
}
