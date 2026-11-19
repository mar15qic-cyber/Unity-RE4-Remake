using GameArchitecture;
using UnityEngine;

public partial class VillagerController
{
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
            onSupport = authoredLift <= .01f && Mathf.Abs(position.y - floorY) <= .02f;
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
                if (TryReadPhysicalGround(position, position.y, .02f, drop, out float landingY))
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
        for (int sample = 1; sample <= samples; sample++)
        {
            Vector3 point = origin + planar * (sample / (float)samples);
            if (!TryReadSupportedFootprint(point, supportHeight, maximumGroundStepUp, maximumGroundDrop, out float height))
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

    private bool TryReadSupportedFootprint(Vector3 point, float height, float rise, float drop, out float support)
    {
        if (TryReadPhysicalGround(point, height, rise, drop, out support))
        {
            return true;
        }

        var navigation = HouseThreeRegionNavigation.Current;
        point.y = height;
        if (navigation == null || navigation.TreadRegion(point) != VillageTraversalRegion.SecondFloorOuterPlatform)
        {
            return false;
        }

        // 板缝宽度按两边踏面之间的实际距离计算。中心偏向一边时，两侧所需的探测距离不同。
        return TryReadSupportedSeam(navigation, point, Vector3.right, height, out support)
            || TryReadSupportedSeam(navigation, point, Vector3.forward, height, out support);
    }

    private bool TryReadSupportedSeam(HouseThreeRegionNavigation navigation, Vector3 point, Vector3 axis,
        float height, out float support)
    {
        support = height;
        const float span = .24f;
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
