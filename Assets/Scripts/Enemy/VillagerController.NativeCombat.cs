using GameArchitecture;
using UnityEngine;

public partial class VillagerController
{
    private bool orderedToBreach;
    internal bool HasSiegeBreachOrder => orderedToBreach && door03TraversalRoute != null
        && door03TraversalRoute.IsReady && door03TraversalRoute.entranceDoor.IsSiegeLatched;

    internal void OrderSiegeBreach(HouseThreeTraversalGeometry route)
    {
        if (route == null || !route.IsReady || chainsawBridge == null || IsDead)
        {
            return;
        }
        ClearPursuitIngress();
        if (specialTraversal != null)
        {
            specialTraversal.CancelPendingSiegeDetour();
        }
        door03TraversalRoute = route;
        orderedToBreach = true;
    }

    private bool ApproachSiegeDoor()
    {
        if (CurrentState == VillagerState.Attack)
        {
            return false;
        }
        // 破门演员始终从门外就位。玩家在屋内移动时，包围站位不能把他带到其他入口。
        Vector3 gate = door03TraversalRoute.DoorGate;
        Vector3 stance = Vector3.Lerp(gate, door03TraversalRoute.ExteriorGate, .7f);
        if (HorizontalDistance(transform.position, stance) <= .45f && Mathf.Abs(transform.position.y - stance.y) <= .4f)
        {
            agent.isStopped = true;
            FacePosition(gate, alertTurnSpeed * 1.5f);
            if (combatModule.CanAttack())
            {
                actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, "电锯破门");
            }
            return false;
        }
        agent.isStopped = false;
        return agent.SetDestination(stance);
    }

    internal void CommitVillageEntrancePose(VillageSiegeSequence owner, Vector3 position, Quaternion rotation)
    {
        if (owner == null || !owner.ControlsActor(this) || IsDead || IsQTEControlled ||
            IsNavigationTraversalProtected || motionExecutor == null)
        {
            return;
        }
        var token = motionExecutor.CurrentToken;
        if (motionExecutor.Valid(token) && TryResolveSupportedNavigationPosition(position, position.y, out var support))
        {
            motionExecutor.CommitPose(token, support, rotation, true);
        }
    }

    private const float CombatSupportRise = .12f;
    private const float CombatSupportDrop = .22f;
    private const float CombatSupportGrace = .055f;
    private float combatSupportLostTime;
    private float combatFallSpeed;
    private float combatAppliedLift;
    private bool combatOnSupport;
    private int combatFallFrame = -1;
    private int stableGroundFrame = -1;

    internal bool IsCombatFalling => combatFallSpeed > 0f;
    internal bool OwnsImpactGroundMotion => externalKnockbackRoutine != null || combatSupportLostTime > 0f
        || (combatModule != null && (combatModule.NativeCombatActive || combatModule.NativeDeathActive));

    internal bool TryAdvanceFarmEntrance(MotionControlToken token, Vector3 step, Quaternion facing)
    {
        if (IsDead || IsQTEControlled || IsNavigationTraversalProtected || OwnsImpactGroundMotion ||
            Time.timeScale > 0f || motionExecutor == null || !motionExecutor.Valid(token) ||
            !TryResolveSupportedNavigationPosition(transform.position + step, transform.position.y, out var supported) ||
            !CanMoveDirectlyToNavigationPoint(supported))
        {
            return false;
        }
        if (Player != null && (supported - Player.transform.position).sqrMagnitude < 1.1f * 1.1f)
        {
            return false;
        }
        return motionExecutor.CommitPose(token, supported, facing, true);
    }

    internal bool CommitNativeCombatMotion(ActionHandle handle, Vector3 displacement, Quaternion rotation,
        float lift, bool findLanding, out bool onSupport)
    {
        onSupport = false;
        if (actionCoordinator == null || actionCoordinator.CurrentHandle != handle || motionExecutor == null
            || !motionExecutor.Valid(motionExecutor.CurrentToken) || IsNavigationTraversalProtected || IsQTEControlled)
        {
            return false;
        }
        var traversal = ArchitectureReadSpecialTraversal;
        displacement.y = 0f;
        Vector3 before = transform.position;
        if (traversal != null && traversal.ClearingImpactOpening)
        {
            displacement = traversal.ReadWindowImpactStep(displacement);
            motionExecutor.CommitPose(motionExecutor.CurrentToken, before + displacement, rotation, true);
        }
        else
        {
            MoveWithCollision(displacement, true, false);
        }
        if (findLanding || combatSupportLostTime > 0f || IsCombatFalling)
        {
            return CommitCombatGroundMotion(rotation, lift, out onSupport) &&
                (displacement.sqrMagnitude < .000001f || GetHorizontalMovementSince(before).sqrMagnitude >= displacement.sqrMagnitude * .05f);
        }

        MaintainGroundContact();
        motionExecutor.CommitPose(motionExecutor.CurrentToken, transform.position, rotation, true);
        return displacement.sqrMagnitude < .000001f || GetHorizontalMovementSince(before).sqrMagnitude >= displacement.sqrMagnitude * .05f;
    }

    private bool CommitCombatGroundMotion(Quaternion rotation, float authoredLift, out bool onSupport)
    {
        onSupport = false;
        if (motionExecutor == null || !motionExecutor.Valid(motionExecutor.CurrentToken) || Time.deltaTime <= 0f
            || IsNavigationTraversalProtected || IsQTEControlled || (knifeLocalHitStop != null && knifeLocalHitStop.IsHitStop))
        {
            return false;
        }
        if (combatFallFrame == Time.frameCount)
        {
            onSupport = combatOnSupport;
            return true;
        }

        combatFallFrame = Time.frameCount;
        Vector3 position = transform.position;
        float baseY = position.y - combatAppliedLift;
        if (!IsCombatFalling && TryReadSupportedFootprint(position, baseY, CombatSupportRise, CombatSupportDrop, out float floorY))
        {
            combatSupportLostTime = 0f;
            float supportY = StabilizeGroundHeight(baseY, floorY);
            float targetY = supportY + authoredLift;
            if (targetY <= position.y + .01f || CanRaiseRaiseMovementCapsule(targetY - position.y))
            {
                position.y = targetY;
            }
            combatAppliedLift = Mathf.Max(0f, position.y - supportY);
            onSupport = authoredLift <= .01f && Mathf.Abs(position.y - floorY) <= .12f;
        }
        else
        {
            combatSupportLostTime += Time.deltaTime;
            if (combatSupportLostTime >= CombatSupportGrace)
            {
                // 离开楼板后以当前真实高度接续重力，不能吸回身后的屋檐。
                combatAppliedLift = 0f;
                float previousSpeed = combatFallSpeed;
                combatFallSpeed += Mathf.Abs(Physics.gravity.y) * Time.deltaTime;
                float drop = (previousSpeed + combatFallSpeed) * .5f * Time.deltaTime;
                if (TryReadSupportedFootprint(position, position.y, .02f, drop, out float landingY))
                {
                    position.y = landingY;
                    combatFallSpeed = 0f;
                    combatSupportLostTime = 0f;
                    onSupport = true;
                }
                else
                {
                    position.y -= drop;
                }
            }
        }

        bool committed = motionExecutor.CommitPose(motionExecutor.CurrentToken, position, rotation, true);
        onSupport = committed && onSupport;
        combatOnSupport = onSupport;
        return committed;
    }

    private static float StabilizeGroundHeight(float currentY, float targetY)
    {
        if (Mathf.Abs(currentY - targetY) <= .012f)
        {
            return currentY;
        }
        return Mathf.MoveTowards(currentY, targetY, 3.5f * Time.deltaTime);
    }

    internal Vector3 ConstrainSupportedEnemyMotion(Vector3 destination)
    {
        Vector3 origin = transform.position;
        Vector3 planar = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
        if (planar.sqrMagnitude < .000001f || IsDead || IsQTEControlled || IsNavigationTraversalProtected
            || OwnsImpactGroundMotion || movementCollider == null)
        {
            return destination;
        }

        return ConstrainFootprintPath(origin, destination, planar);
    }

    private Vector3 ConstrainFootprintPath(Vector3 origin, Vector3 destination, Vector3 planar)
    {
        const float sampleSpacing = .075f;
        int samples = Mathf.Max(1, Mathf.CeilToInt(planar.magnitude / sampleSpacing));
        float supportHeight = origin.y;
        Vector3 supported = origin;
        Vector3 preferredDirection = planar.normalized;
        for (int sample = 1; sample <= samples; sample++)
        {
            Vector3 point = origin + planar * (sample / (float)samples);
            if (!TryReadSupportedFootprint(point, supportHeight, maximumGroundStepUp, maximumGroundDrop, out float height, preferredDirection))
            {
                if (DoorController.IsInsideOpenPassage(point, .20f) || PlantNearHouseThreeTread(point)
                    && TryResolveSupportedNavigationPosition(point, supportHeight, out _))
                {
                    supported = point;
                    continue;
                }

                return supported;
            }
            supportHeight = height;
            supported = point;
        }
        return destination;
    }

    private bool HasContinuousPlatformFootprint(Vector3 destination)
    {
        Vector3 origin = transform.position;
        Vector3 planar = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
        return ConstrainFootprintPath(origin, destination, planar).Equals(destination);
    }

    internal bool HasPhysicalEnemySupport()
    {
        Vector3 position = transform.position;
        return TryReadSupportedFootprint(position, position.y, maximumGroundStepUp, maximumGroundDrop, out _);
    }

    private static readonly Vector3 DiagonalSeamAxisA = new Vector3(0.70710677f, 0f, 0.70710677f);
    private static readonly Vector3 DiagonalSeamAxisB = new Vector3(0.70710677f, 0f, -0.70710677f);

    private bool TryReadSupportedFootprint(Vector3 point, float height, float rise, float drop, out float support,
        Vector3 preferredAxis = default)
    {
        var navigation = HouseThreeRegionNavigation.Current;
        Vector3 regionProbe = new Vector3(point.x, height, point.z);
        bool onSecondFloorOuterPlatform = navigation != null
            && navigation.TreadRegion(regionProbe) == VillageTraversalRegion.SecondFloorOuterPlatform;
        float effectiveRise = rise;
        if (onSecondFloorOuterPlatform && effectiveRise < .22f)
        {
            effectiveRise = .22f;
        }

        bool centerHit = TryReadPhysicalGround(point, height, effectiveRise, drop, out float centerHeight);
        support = centerHeight;
        if (centerHit && centerHeight >= height - .12f)
        {
            return true;
        }

        if (!onSecondFloorOuterPlatform)
        {
            return centerHit;
        }

        point.y = height;
        float seamHeight = 0f;
        bool foundSeam = false;
        Vector3 planarPreferred = Vector3.ProjectOnPlane(preferredAxis, Vector3.up);
        if (planarPreferred.sqrMagnitude > 0.0001f)
        {
            foundSeam = TryReadSupportedSeam(navigation, point, planarPreferred.normalized, height, out seamHeight);
        }

        // 板缝宽度按两边踏面之间的实际距离计算；同步探测世界正交与 45° 对角轴，避免斜向木板接缝卡脚。
        if (!foundSeam)
        {
            foundSeam = TryReadSupportedSeam(navigation, point, Vector3.right, height, out seamHeight)
                || TryReadSupportedSeam(navigation, point, Vector3.forward, height, out seamHeight)
                || TryReadSupportedSeam(navigation, point, DiagonalSeamAxisA, height, out seamHeight)
                || TryReadSupportedSeam(navigation, point, DiagonalSeamAxisB, height, out seamHeight);
        }

        if (foundSeam && seamHeight >= height - drop && seamHeight <= height + effectiveRise
            && (!centerHit || seamHeight > centerHeight))
        {
            support = seamHeight;
            return true;
        }
        return centerHit;
    }

    private bool TryReadSupportedSeam(HouseThreeRegionNavigation navigation, Vector3 point, Vector3 axis,
        float height, out float support)
    {
        support = height;
        float span = .24f;
        if (movementCollider != null)
        {
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            span = Mathf.Clamp(movementCollider.radius * scale * 2f, .24f, .55f);
        }
        const float maximumSeamRise = .22f;
        const int sampleCount = 12;
        Vector3 step = axis * (span / sampleCount);
        float beforeHeight = height;
        float afterHeight = height;
        int before = 0;
        int after = 0;
        for (int sample = 1; sample < sampleCount; sample++)
        {
            Vector3 beforePoint = point - step * sample;
            if (before == 0 && TryReadPhysicalGround(beforePoint, height, maximumSeamRise, maximumSeamRise, out beforeHeight))
            {
                beforePoint.y = beforeHeight;
                if (navigation.TreadRegion(beforePoint) == VillageTraversalRegion.SecondFloorOuterPlatform)
                {
                    before = sample;
                }
            }
            Vector3 afterPoint = point + step * sample;
            if (after == 0 && TryReadPhysicalGround(afterPoint, height, maximumSeamRise, maximumSeamRise, out afterHeight))
            {
                afterPoint.y = afterHeight;
                if (navigation.TreadRegion(afterPoint) == VillageTraversalRegion.SecondFloorOuterPlatform)
                {
                    after = sample;
                }
            }
            if (before != 0 && after != 0)
            {
                if (before + after > sampleCount || Mathf.Abs(beforeHeight - afterHeight) > maximumSeamRise)
                {
                    return false;
                }
                support = Mathf.Lerp(beforeHeight, afterHeight, before / (float)(before + after));
                return true;
            }
        }
        return false;
    }

    private bool TryReadPhysicalGround(Vector3 position, float referenceY, float rise, float drop, out float groundY)
    {
        groundY = referenceY;
        const float clearance = .025f;
        position.y = referenceY - groundOffset + rise + clearance;
        CombatPerformanceCounters.physicsQueryCount++;
        int count = Physics.RaycastNonAlloc(position, Vector3.down, groundHits, rise + drop + clearance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float minimumNormal = Mathf.Cos(Mathf.Clamp(maximumGroundSlope, 1f, 89f) * Mathf.Deg2Rad);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            Collider other = hit.collider;
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform) || hit.normal.y < minimumNormal
                || NavigationCollisionCache.Enemy(other) != null || NavigationCollisionCache.Player(other) != null
                || IsNavigationObstacle(other) || IgnoreWindowImpactCollider(other))
            {
                continue;
            }
            float height = hit.point.y + groundOffset;
            if (height <= referenceY + rise && height > bestY)
            {
                bestY = height;
            }
        }
        if (float.IsNegativeInfinity(bestY))
        {
            return false;
        }
        groundY = bestY;
        return true;
    }

    private void ClearCombatGroundMotion()
    {
        combatSupportLostTime = 0f;
        combatFallSpeed = 0f;
        combatAppliedLift = 0f;
        combatOnSupport = false;
        combatFallFrame = -1;
        stableGroundFrame = -1;
    }
}
