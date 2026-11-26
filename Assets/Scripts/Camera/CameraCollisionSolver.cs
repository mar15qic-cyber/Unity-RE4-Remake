using UnityEngine;

/// <summary>
/// 摄像机避障碰撞体过滤器。
/// 排除玩家自身、敌人及命中盒、非运动学动态刚体与当前穿越窗口的可忽略碰撞体。
/// </summary>
public static class CameraCollisionFilter
{
    private static int cachedEnemyLayer = -2;
    private static int cachedEnemyHitboxLayer = -2;

    public static void ReadEnemyLayers(out int enemyLayer, out int enemyHitboxLayer)
    {
        if (cachedEnemyLayer == -2)
        {
            cachedEnemyLayer = LayerMask.NameToLayer("Enemy");
        }

        if (cachedEnemyHitboxLayer == -2)
        {
            cachedEnemyHitboxLayer = LayerMask.NameToLayer("EnemyHitbox");
        }

        enemyLayer = cachedEnemyLayer;
        enemyHitboxLayer = cachedEnemyHitboxLayer;
    }

    public static bool ShouldIgnoreCollider(
        Collider collider,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        if (collider == null || collider.isTrigger)
        {
            return true;
        }

        if (playerRoot != null && collider.transform.IsChildOf(playerRoot))
        {
            return true;
        }

        int layer = collider.gameObject.layer;
        if (enemyLayer >= 0 && layer == enemyLayer)
        {
            return true;
        }

        if (enemyHitboxLayer >= 0 && layer == enemyHitboxLayer)
        {
            return true;
        }

        if (collider.GetComponentInParent<VillagerController>() != null)
        {
            return true;
        }

        if (collider.GetComponentInParent<EnemyHitbox>() != null)
        {
            return true;
        }

        Rigidbody attachedBody = collider.attachedRigidbody;
        if (attachedBody != null && !attachedBody.isKinematic)
        {
            return true;
        }

        if (activeWindow != null)
        {
            if (activeWindow.ShouldIgnorePathCollider(collider))
            {
                return true;
            }

            if (activeWindow.fracture != null && collider.transform.IsChildOf(activeWindow.fracture.transform))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 三自由度连续摄像机避障求解器。
/// 顺序执行头顶净空下压、肩位横向滑移、主臂安全距离压缩与非对称防晕平滑恢复。
/// </summary>
public sealed class CameraCollisionSolver
{
    public struct SolveSettings
    {
        public float releaseDelay;
        public float normalRecoveryDamping;
        public float exitEaseRecoveryDamping;
        public float maxNormalRecoverySpeed;
        public float maxExitRecoverySpeed;
        public float ceilingDropLimit;
        public float heightSmoothTime;
        public float shoulderSlideSmoothTime;
        public float relativeOffsetSmoothSpeed;

        public static SolveSettings CreateDefault()
        {
            SolveSettings settings = new SolveSettings();
            settings.releaseDelay = 0.08f;
            settings.normalRecoveryDamping = 0.16f;
            settings.exitEaseRecoveryDamping = 0.24f;
            settings.maxNormalRecoverySpeed = 7.5f;
            settings.maxExitRecoverySpeed = 4.2f;
            settings.ceilingDropLimit = 0.52f;
            settings.heightSmoothTime = 0.10f;
            settings.shoulderSlideSmoothTime = 0.10f;
            settings.relativeOffsetSmoothSpeed = 9.5f;
            return settings;
        }
    }

    public struct SolveRequest
    {
        public Vector3 focusPosition;
        public Vector3 desiredPosition;
        public Vector3 referenceUp;
        public Vector3 cameraRight;
        public float desiredShoulderOffset;
        public float cameraRadius;
        public float safetySkin;
        public float nearPlaneRadius;
        public float minimumDistance;
        public int collisionMask;
        public Transform playerRoot;
        public WindowTraversalPoint activeWindow;
        public bool inPassage;
        public float exitEaseWeight;
        public float deltaTime;
        public bool resetState;
        public float aimShoulderLockWeight;
    }

    public struct DebugSnapshot
    {
        public bool valid;
        public Vector3 focusPosition;
        public Vector3 desiredPosition;
        public Vector3 ceilingAdjustedPosition;
        public Vector3 shoulderAdjustedPosition;
        public Vector3 finalPosition;
        public float compressionRatio;
        public float ceilingDrop;
        public float shoulderScale;
        public bool blocked;
    }

    private const int HitBufferCapacity = 32;
    private const int CollisionProbeSteps = 12;
    private const int CollisionRefinementSteps = 5;
    private const int ShoulderSampleCount = 5;
    private const float Epsilon = 0.0001f;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferCapacity];
    private readonly Collider[] overlapBuffer = new Collider[HitBufferCapacity];

    private bool stateInitialized;
    private float currentDistance;
    private float distanceVelocity;
    private float heldBlockedDistance;
    private float previousDesiredDistance;
    private float unblockedElapsed = float.PositiveInfinity;

    private float currentCeilingDrop;
    private float ceilingDropVelocity;
    private float currentShoulderScale = 1f;
    private float lastChosenShoulderScale = 1f;
    private float shoulderScaleVelocity;

    private bool hasLastRelativeOffset;
    private Vector3 lastRelativeOffset;

    private float compressionRatio;
    private DebugSnapshot lastSnapshot;

    public float CompressionRatio
    {
        get { return compressionRatio; }
    }

    public DebugSnapshot LastSnapshot
    {
        get { return lastSnapshot; }
    }

    public void Reset()
    {
        stateInitialized = false;
        currentDistance = 0f;
        distanceVelocity = 0f;
        heldBlockedDistance = 0f;
        previousDesiredDistance = 0f;
        unblockedElapsed = float.PositiveInfinity;
        currentCeilingDrop = 0f;
        ceilingDropVelocity = 0f;
        currentShoulderScale = 1f;
        lastChosenShoulderScale = 1f;
        shoulderScaleVelocity = 0f;
        hasLastRelativeOffset = false;
        lastRelativeOffset = Vector3.zero;
        compressionRatio = 0f;
        lastSnapshot = default(DebugSnapshot);
    }

    public Vector3 Solve(in SolveRequest request, in SolveSettings settings)
    {
        if (!IsFinite(request.focusPosition) || !IsFinite(request.desiredPosition))
        {
            Reset();
            return request.desiredPosition;
        }

        if (request.collisionMask == 0)
        {
            compressionRatio = 0f;
            lastSnapshot = new DebugSnapshot
            {
                valid = true,
                focusPosition = request.focusPosition,
                desiredPosition = request.desiredPosition,
                ceilingAdjustedPosition = request.desiredPosition,
                shoulderAdjustedPosition = request.desiredPosition,
                finalPosition = request.desiredPosition,
                compressionRatio = 0f,
                ceilingDrop = 0f,
                shoulderScale = 1f,
                blocked = false
            };
            return request.desiredPosition;
        }

        Vector3 up;
        if (request.referenceUp.sqrMagnitude > 0.001f)
        {
            up = request.referenceUp.normalized;
        }
        else
        {
            up = Vector3.up;
        }

        Vector3 right = Vector3.ProjectOnPlane(request.cameraRight, up);
        if (right.sqrMagnitude > 0.001f)
        {
            right.Normalize();
        }
        else
        {
            right = Vector3.right;
        }

        CameraCollisionFilter.ReadEnemyLayers(out int enemyLayer, out int enemyHitboxLayer);
        float safeRadius = Mathf.Max(0.05f, request.cameraRadius);
        float safeSkin = Mathf.Max(0.01f, request.safetySkin);
        float safeNearRadius = Mathf.Max(safeRadius, request.nearPlaneRadius);
        float exitEase = Mathf.Clamp01(request.exitEaseWeight);
        bool mustReset = !stateInitialized || request.resetState || request.deltaTime < 0f;

        // 1. 头顶净空探测：低矮天花板或房梁下优先平滑下压高度，避免向上顶头。
        float targetCeilingDrop = ComputeTargetCeilingDrop(
            request.focusPosition,
            request.desiredPosition,
            up,
            safeRadius,
            safeSkin,
            settings.ceilingDropLimit,
            request.collisionMask,
            request.playerRoot,
            request.activeWindow,
            enemyLayer,
            enemyHitboxLayer);

        if (mustReset)
        {
            currentCeilingDrop = targetCeilingDrop;
            ceilingDropVelocity = 0f;
        }
        else if (IsFinite(request.deltaTime) && request.deltaTime > Epsilon)
        {
            float heightSmooth;
            if (targetCeilingDrop < currentCeilingDrop)
            {
                heightSmooth = 0.045f;
            }
            else
            {
                heightSmooth = Mathf.Lerp(Mathf.Max(0.02f, settings.heightSmoothTime), 0.18f, exitEase);
            }

            currentCeilingDrop = Mathf.SmoothDamp(
                currentCeilingDrop,
                targetCeilingDrop,
                ref ceilingDropVelocity,
                heightSmooth,
                Mathf.Infinity,
                request.deltaTime);
        }

        Vector3 ceilingAdjustedDesired = request.desiredPosition + up * currentCeilingDrop;
        float activeDesiredShoulderOffset = request.desiredShoulderOffset;
        if (request.aimShoulderLockWeight > 0.5f && currentCeilingDrop < -0.05f)
        {
            float lateralRelief = Mathf.Clamp(-currentCeilingDrop * 0.35f, 0f, 0.16f) * Mathf.Clamp01((request.aimShoulderLockWeight - 0.5f) * 2f);
            if (lateralRelief > 0.001f)
            {
                Vector3 reliefDir = right;
                if (activeDesiredShoulderOffset < 0f)
                {
                    reliefDir = -right;
                }
                ceilingAdjustedDesired += reliefDir * lateralRelief;
                if (activeDesiredShoulderOffset >= 0f)
                {
                    activeDesiredShoulderOffset += lateralRelief;
                }
                else
                {
                    activeDesiredShoulderOffset -= lateralRelief;
                }
            }
        }

        // 2. 肩位横向滑移：右肩被侧墙或门柱挡住而中轴畅通时，先向中心平滑内收肩位；ADS 时限制最小内收比例防止人物遮挡准星。
        float targetShoulderScale = ComputeTargetShoulderScale(
            request.focusPosition,
            ceilingAdjustedDesired,
            right,
            activeDesiredShoulderOffset,
            safeRadius,
            safeSkin,
            request.minimumDistance,
            request.collisionMask,
            request.playerRoot,
            request.activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            request.aimShoulderLockWeight);

        if (mustReset)
        {
            currentShoulderScale = targetShoulderScale;
            shoulderScaleVelocity = 0f;
        }
        else if (IsFinite(request.deltaTime) && request.deltaTime > Epsilon)
        {
            float slideSmooth;
            if (targetShoulderScale < currentShoulderScale)
            {
                slideSmooth = 0.06f;
            }
            else
            {
                slideSmooth = Mathf.Lerp(Mathf.Max(0.02f, settings.shoulderSlideSmoothTime), 0.18f, exitEase);
            }

            currentShoulderScale = Mathf.SmoothDamp(
                currentShoulderScale,
                targetShoulderScale,
                ref shoulderScaleVelocity,
                slideSmooth,
                Mathf.Infinity,
                request.deltaTime);
        }

        Vector3 shoulderAdjustedDesired = ceilingAdjustedDesired - right * (activeDesiredShoulderOffset * (1f - currentShoulderScale));

        // 3. 主臂安全距离求解与非对称防晕平滑恢复。
        Vector3 boom = shoulderAdjustedDesired - request.focusPosition;
        float desiredDistance = boom.magnitude;
        if (!IsFinite(desiredDistance) || desiredDistance <= Epsilon)
        {
            return shoulderAdjustedDesired;
        }

        Vector3 boomDirection = boom / desiredDistance;
        float minDist = Mathf.Min(request.minimumDistance, desiredDistance);
        bool blocked = TryGetSafeDistance(
            request.focusPosition,
            boomDirection,
            desiredDistance,
            minDist,
            safeRadius,
            safeSkin,
            request.collisionMask,
            request.playerRoot,
            request.activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            out float safeDistance);

        float desiredDistanceChange = desiredDistance - previousDesiredDistance;
        previousDesiredDistance = desiredDistance;

        if (mustReset)
        {
            currentDistance = safeDistance;
            heldBlockedDistance = safeDistance;
            distanceVelocity = 0f;
            lastChosenShoulderScale = 1f;
            hasLastRelativeOffset = false;
            stateInitialized = true;
            if (blocked)
            {
                unblockedElapsed = 0f;
            }
            else
            {
                unblockedElapsed = float.PositiveInfinity;
            }
        }
        else if (!IsFinite(request.deltaTime) || request.deltaTime <= Epsilon)
        {
            distanceVelocity = 0f;
            if (IsFinite(currentDistance))
            {
                currentDistance = Mathf.Clamp(currentDistance, minDist, desiredDistance);
            }
            else
            {
                currentDistance = safeDistance;
            }
        }
        else
        {
            float targetDistance = desiredDistance;
            if (blocked)
            {
                unblockedElapsed = 0f;
                heldBlockedDistance = safeDistance;
                if (safeDistance < currentDistance)
                {
                    currentDistance = safeDistance;
                    distanceVelocity = 0f;
                }

                targetDistance = safeDistance;
            }
            else
            {
                unblockedElapsed += request.deltaTime;
                if (unblockedElapsed < settings.releaseDelay)
                {
                    targetDistance = Mathf.Min(desiredDistance, heldBlockedDistance);
                }
                else
                {
                    currentDistance = Mathf.Clamp(currentDistance + desiredDistanceChange, minDist, desiredDistance);
                }
            }

            if (targetDistance >= currentDistance)
            {
                float activeDamping = Mathf.Lerp(settings.normalRecoveryDamping, settings.exitEaseRecoveryDamping, exitEase);
                float maxSpeed = Mathf.Lerp(settings.maxNormalRecoverySpeed, settings.maxExitRecoverySpeed, exitEase);
                float nextVelocity = distanceVelocity;
                float nextDistance = Mathf.SmoothDamp(
                    currentDistance,
                    targetDistance,
                    ref nextVelocity,
                    Mathf.Max(0.01f, activeDamping),
                    Mathf.Max(0.1f, maxSpeed),
                    request.deltaTime);

                if (nextDistance > currentDistance + Epsilon)
                {
                    // 用与近裁面校验一致的半径预检恢复后的候选机位，避免推出后立即被硬拉回导致锯齿振荡。
                    Vector3 recoveryPreview = request.focusPosition + boomDirection * nextDistance;
                    if (IsClearCameraPoint(
                            request.focusPosition,
                            recoveryPreview,
                            safeNearRadius,
                            request.collisionMask,
                            request.playerRoot,
                            request.activeWindow,
                            enemyLayer,
                            enemyHitboxLayer))
                    {
                        currentDistance = nextDistance;
                        distanceVelocity = nextVelocity;
                    }
                    else
                    {
                        distanceVelocity = 0f;
                    }
                }
                else
                {
                    currentDistance = nextDistance;
                    distanceVelocity = nextVelocity;
                }
            }

            if (!IsFinite(currentDistance) || !IsFinite(distanceVelocity))
            {
                currentDistance = safeDistance;
                heldBlockedDistance = safeDistance;
                distanceVelocity = 0f;
            }

            currentDistance = Mathf.Clamp(currentDistance, minDist, desiredDistance);
        }

        Vector3 candidate = request.focusPosition + boomDirection * currentDistance;

        // 4. 近裁面安全校验与通道/出窗期焦点相对坐标平滑（不再对世界空间历史点做易卡窗框的直线投射）。
        if (!IsClearCameraPoint(
                request.focusPosition,
                candidate,
                safeNearRadius,
                request.collisionMask,
                request.playerRoot,
                request.activeWindow,
                enemyLayer,
                enemyHitboxLayer))
        {
            candidate = PullToClearDistance(
                request.focusPosition,
                boomDirection,
                currentDistance,
                minDist,
                safeNearRadius,
                safeSkin,
                request.collisionMask,
                request.playerRoot,
                request.activeWindow,
                enemyLayer,
                enemyHitboxLayer);

            float pulledDistance = Vector3.Distance(request.focusPosition, candidate);
            if (IsFinite(pulledDistance) && pulledDistance < currentDistance - Epsilon)
            {
                currentDistance = Mathf.Max(minDist, pulledDistance);
                heldBlockedDistance = Mathf.Min(heldBlockedDistance, currentDistance);
                distanceVelocity = 0f;
                unblockedElapsed = 0f;
            }
        }

        if ((request.inPassage || exitEase > 0.01f) && hasLastRelativeOffset && !mustReset && IsFinite(request.deltaTime) && request.deltaTime > Epsilon)
        {
            Vector3 targetRelativeOffset = candidate - request.focusPosition;
            float passageFactor;
            if (request.inPassage)
            {
                passageFactor = 1f;
            }
            else
            {
                passageFactor = exitEase;
            }

            float relativeSpeed = Mathf.Lerp(settings.relativeOffsetSmoothSpeed, settings.maxExitRecoverySpeed * 1.35f, passageFactor);
            Vector3 blendedRelative = Vector3.MoveTowards(lastRelativeOffset, targetRelativeOffset, relativeSpeed * request.deltaTime);
            Vector3 blendedCandidate = request.focusPosition + blendedRelative;
            candidate = ClampSegmentAgainstHardWalls(
                request.focusPosition,
                blendedCandidate,
                safeNearRadius,
                safeSkin,
                request.collisionMask,
                request.playerRoot,
                request.activeWindow,
                enemyLayer,
                enemyHitboxLayer);
        }

        lastRelativeOffset = candidate - request.focusPosition;
        hasLastRelativeOffset = true;

        float rawDesiredDistance = Vector3.Distance(request.focusPosition, request.desiredPosition);
        float rawRange = Mathf.Max(0.15f, rawDesiredDistance - minDist);
        float actualDistance = Vector3.Distance(request.focusPosition, candidate);
        compressionRatio = Mathf.Clamp01(1f - (actualDistance - minDist) / rawRange);

        lastSnapshot = new DebugSnapshot
        {
            valid = true,
            focusPosition = request.focusPosition,
            desiredPosition = request.desiredPosition,
            ceilingAdjustedPosition = ceilingAdjustedDesired,
            shoulderAdjustedPosition = shoulderAdjustedDesired,
            finalPosition = candidate,
            compressionRatio = compressionRatio,
            ceilingDrop = currentCeilingDrop,
            shoulderScale = currentShoulderScale,
            blocked = blocked || compressionRatio > 0.05f
        };

        return candidate;
    }

    private float ComputeTargetCeilingDrop(
        Vector3 focusPosition,
        Vector3 desiredPosition,
        Vector3 up,
        float radius,
        float skin,
        float dropLimit,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        if (dropLimit <= 0.01f)
        {
            return 0f;
        }

        float desiredVerticalDelta = Vector3.Dot(desiredPosition - focusPosition, up);
        if (desiredVerticalDelta <= 0.02f)
        {
            return 0f;
        }

        float targetDrop = 0f;

        // 从观察原点向上检测低矮顶棚。
        if (TryFilteredSphereCastCeiling(
                focusPosition,
                radius,
                up,
                desiredVerticalDelta + skin + 0.04f,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer,
                out float focusCeilingDistance))
        {
            float safeAboveFocus = Mathf.Max(0f, focusCeilingDistance - skin);
            float requiredDrop = safeAboveFocus - desiredVerticalDelta;
            if (requiredDrop < targetDrop)
            {
                targetDrop = requiredDrop;
            }
        }

        // 在相机所处水平列与观察原点同高的位置向上检测屋檐与门楣，防止角色在屋外而机位仍在低矮门檐下顶头。
        Vector3 columnBase = desiredPosition - up * desiredVerticalDelta;
        if (!HasBlockingOverlapFiltered(
                columnBase,
                radius,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer))
        {
            if (TryFilteredSphereCastCeiling(
                    columnBase,
                    radius,
                    up,
                    desiredVerticalDelta + skin + 0.06f,
                    mask,
                    playerRoot,
                    activeWindow,
                    enemyLayer,
                    enemyHitboxLayer,
                    out float columnCeilingDistance))
            {
                float safeSpan = Mathf.Max(0f, columnCeilingDistance - skin);
                float requiredColumnDrop = safeSpan - desiredVerticalDelta;
                if (requiredColumnDrop < targetDrop)
                {
                    targetDrop = requiredColumnDrop;
                }
            }
        }

        return Mathf.Clamp(targetDrop, -dropLimit, 0f);
    }

    private float ComputeTargetShoulderScale(
        Vector3 focusPosition,
        Vector3 ceilingAdjustedDesired,
        Vector3 right,
        float desiredShoulderOffset,
        float radius,
        float skin,
        float minDist,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer,
        float aimShoulderLockWeight)
    {
        float absShoulder = Mathf.Abs(desiredShoulderOffset);
        if (absShoulder <= 0.05f)
        {
            return 1f;
        }

        float minAllowedScale = Mathf.Lerp(0f, 0.78f, Mathf.Clamp01(aimShoulderLockWeight));
        float maxLateralScale = 1f;
        Vector3 shoulderDir = right * Mathf.Sign(desiredShoulderOffset);
        if (TryFilteredSphereCastNearest(
                focusPosition,
                radius * 0.8f,
                shoulderDir,
                absShoulder + skin,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer,
                out RaycastHit lateralHit))
        {
            float safeLateral = Mathf.Max(0f, lateralHit.distance - skin);
            maxLateralScale = Mathf.Clamp01(safeLateral / absShoulder);
        }

        maxLateralScale = Mathf.Max(minAllowedScale, maxLateralScale);

        float fullSafeDist = EvaluatePointSafeDistance(
            focusPosition,
            ceilingAdjustedDesired,
            radius,
            skin,
            minDist,
            mask,
            playerRoot,
            activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            out float fullDesiredDist);

        bool fullObstructed = fullSafeDist < fullDesiredDist - 0.16f;
        if (!fullObstructed && maxLateralScale >= 0.99f)
        {
            lastChosenShoulderScale = 1f;
            return 1f;
        }

        float clampedPreviousScale = Mathf.Clamp(lastChosenShoulderScale, minAllowedScale, maxLateralScale);
        Vector3 previousSampleDesired = ceilingAdjustedDesired - right * (desiredShoulderOffset * (1f - clampedPreviousScale));
        float previousSampleSafeDist = EvaluatePointSafeDistance(
            focusPosition,
            previousSampleDesired,
            radius,
            skin,
            minDist,
            mask,
            playerRoot,
            activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            out _);
        float previousScaleScore = previousSampleSafeDist + 0.15f * clampedPreviousScale;

        float bestScale = clampedPreviousScale;
        float bestScore = previousScaleScore;
        for (int i = 0; i < ShoulderSampleCount; i++)
        {
            float sampleScale = i / (float)(ShoulderSampleCount - 1);
            if (sampleScale < minAllowedScale - 0.01f || sampleScale > maxLateralScale + 0.01f)
            {
                continue;
            }

            float clampedSample = Mathf.Clamp(sampleScale, minAllowedScale, 1f);
            Vector3 sampleDesired = ceilingAdjustedDesired - right * (desiredShoulderOffset * (1f - clampedSample));
            float sampleSafeDist = EvaluatePointSafeDistance(
                focusPosition,
                sampleDesired,
                radius,
                skin,
                minDist,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer,
                out _);

            // 只要内收肩位能明显换取更充裕的视距，就优先向中心靠拢，同时保留尽可能多的越肩构图。
            float score = sampleSafeDist + 0.15f * clampedSample;
            float requiredMargin = 0.01f;
            if (Mathf.Abs(clampedSample - clampedPreviousScale) > 0.05f)
            {
                requiredMargin = 0.08f;
            }
            if (score > bestScore + requiredMargin)
            {
                bestScore = score;
                bestScale = clampedSample;
            }
        }

        lastChosenShoulderScale = Mathf.Clamp(bestScale, minAllowedScale, 1f);
        return lastChosenShoulderScale;
    }

    private float EvaluatePointSafeDistance(
        Vector3 focusPosition,
        Vector3 desiredPoint,
        float radius,
        float skin,
        float minDist,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer,
        out float desiredDistance)
    {
        Vector3 delta = desiredPoint - focusPosition;
        desiredDistance = delta.magnitude;
        if (desiredDistance <= Epsilon)
        {
            return 0f;
        }

        float clampedMin = Mathf.Min(minDist, desiredDistance);
        TryGetSafeDistance(
            focusPosition,
            delta / desiredDistance,
            desiredDistance,
            clampedMin,
            radius,
            skin,
            mask,
            playerRoot,
            activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            out float safeDistance);
        return safeDistance;
    }

    private bool TryGetSafeDistance(
        Vector3 origin,
        Vector3 direction,
        float desiredDistance,
        float minDist,
        float radius,
        float skin,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer,
        out float safeDistance)
    {
        safeDistance = desiredDistance;
        bool blocked = false;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            hitBuffer,
            desiredDistance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (CameraCollisionFilter.ShouldIgnoreCollider(hit.collider, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
            {
                continue;
            }

            float candidateDistance = Mathf.Max(minDist, hit.distance - skin);
            if (candidateDistance < safeDistance)
            {
                safeDistance = candidateDistance;
                blocked = true;
            }
        }

        float overlapSafeDistance = FindFirstOverlapSafeDistance(
            origin,
            direction,
            desiredDistance,
            minDist,
            radius,
            skin,
            mask,
            playerRoot,
            activeWindow,
            enemyLayer,
            enemyHitboxLayer);

        if (overlapSafeDistance < safeDistance)
        {
            safeDistance = overlapSafeDistance;
            blocked = true;
        }

        return blocked;
    }

    private float FindFirstOverlapSafeDistance(
        Vector3 origin,
        Vector3 direction,
        float desiredDistance,
        float minDist,
        float radius,
        float skin,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        float previousDistance = minDist;
        if (HasBlockingOverlapFiltered(
                origin + direction * previousDistance,
                radius,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer))
        {
            return minDist;
        }

        for (int i = 1; i <= CollisionProbeSteps; i++)
        {
            float probeDistance = Mathf.Lerp(minDist, desiredDistance, i / (float)CollisionProbeSteps);
            if (!HasBlockingOverlapFiltered(
                    origin + direction * probeDistance,
                    radius,
                    mask,
                    playerRoot,
                    activeWindow,
                    enemyLayer,
                    enemyHitboxLayer))
            {
                previousDistance = probeDistance;
                continue;
            }

            float lower = previousDistance;
            float upper = probeDistance;
            for (int refinement = 0; refinement < CollisionRefinementSteps; refinement++)
            {
                float middle = (lower + upper) * 0.5f;
                if (HasBlockingOverlapFiltered(
                        origin + direction * middle,
                        radius,
                        mask,
                        playerRoot,
                        activeWindow,
                        enemyLayer,
                        enemyHitboxLayer))
                {
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }

            return Mathf.Max(minDist, lower - skin);
        }

        return desiredDistance;
    }

    private Vector3 PullToClearDistance(
        Vector3 focus,
        Vector3 direction,
        float startDistance,
        float minDist,
        float nearRadius,
        float skin,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        TryGetSafeDistance(
            focus,
            direction,
            startDistance,
            minDist,
            nearRadius,
            skin,
            mask,
            playerRoot,
            activeWindow,
            enemyLayer,
            enemyHitboxLayer,
            out float nearSafeDist);

        Vector3 candidate = focus + direction * nearSafeDist;
        if (IsClearCameraPoint(focus, candidate, nearRadius, mask, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
        {
            return candidate;
        }

        for (int step = 1; step <= 8; step++)
        {
            float t = 1f - step / 8f;
            float dist = Mathf.Lerp(minDist, nearSafeDist, t);
            Vector3 probe = focus + direction * dist;
            if (IsClearCameraPoint(focus, probe, nearRadius, mask, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
            {
                return probe;
            }
        }

        return focus + direction * minDist;
    }

    private Vector3 ClampSegmentAgainstHardWalls(
        Vector3 focus,
        Vector3 targetPoint,
        float nearRadius,
        float skin,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        Vector3 delta = targetPoint - focus;
        float distance = delta.magnitude;
        if (distance <= Epsilon)
        {
            return targetPoint;
        }

        Vector3 dir = delta / distance;
        if (TryFilteredSphereCastNearest(
                focus,
                nearRadius,
                dir,
                distance,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer,
                out RaycastHit hit))
        {
            float safeDist = Mathf.Max(0.15f, hit.distance - skin);
            if (safeDist < distance)
            {
                return focus + dir * safeDist;
            }
        }

        if (HasBlockingOverlapFiltered(
                targetPoint,
                nearRadius,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer))
        {
            return PullToClearDistance(
                focus,
                dir,
                distance,
                Mathf.Min(0.25f, distance),
                nearRadius,
                skin,
                mask,
                playerRoot,
                activeWindow,
                enemyLayer,
                enemyHitboxLayer);
        }

        return targetPoint;
    }

    private bool IsClearCameraPoint(
        Vector3 focus,
        Vector3 position,
        float radius,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        if (HasBlockingOverlapFiltered(position, radius, mask, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
        {
            return false;
        }

        Vector3 delta = position - focus;
        float distance = delta.magnitude;
        if (distance <= Epsilon)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            focus,
            delta / distance,
            hitBuffer,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            if (!CameraCollisionFilter.ShouldIgnoreCollider(
                    hitBuffer[i].collider,
                    playerRoot,
                    activeWindow,
                    enemyLayer,
                    enemyHitboxLayer))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryFilteredSphereCastCeiling(
        Vector3 origin,
        float radius,
        Vector3 up,
        float distance,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer,
        out float nearestDistance)
    {
        nearestDistance = distance;
        bool found = false;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            up,
            hitBuffer,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (CameraCollisionFilter.ShouldIgnoreCollider(hit.collider, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
            {
                continue;
            }

            // 仅将法线明显朝下的表面视为顶棚/房梁/门楣，避免垂直墙面触发误压低。
            if (Vector3.Dot(hit.normal, up) >= -0.35f)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                found = true;
            }
        }

        return found;
    }

    private bool TryFilteredSphereCastNearest(
        Vector3 origin,
        float radius,
        Vector3 direction,
        float distance,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer,
        out RaycastHit nearestHit)
    {
        nearestHit = default(RaycastHit);
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            hitBuffer,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (CameraCollisionFilter.ShouldIgnoreCollider(hit.collider, playerRoot, activeWindow, enemyLayer, enemyHitboxLayer))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                nearestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool HasBlockingOverlapFiltered(
        Vector3 position,
        float radius,
        int mask,
        Transform playerRoot,
        WindowTraversalPoint activeWindow,
        int enemyLayer,
        int enemyHitboxLayer)
    {
        int count = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (!CameraCollisionFilter.ShouldIgnoreCollider(
                    overlapBuffer[i],
                    playerRoot,
                    activeWindow,
                    enemyLayer,
                    enemyHitboxLayer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
