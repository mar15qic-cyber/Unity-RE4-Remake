using Unity.Cinemachine;
using UnityEngine;

public partial class RE4RThirdPersonCamera
{
    [Tooltip("只在独立验收场景中预览待实测的原版姿态。")]
    [SerializeField] private bool previewUnverifiedNativeShoulder;

    private Transform nativeGround;
    private Transform nativeHips;
    private Transform nativeChest;
    private Transform nativeAnchorOwner;
    private Transform framingHead;
    private Transform framingSpine;
    private Transform framingLeftShoulder;
    private Transform framingRightShoulder;
    private Transform framingRightUpperArm;
    private Transform framingRightForearm;
    private Transform framingRightWrist;
    private Vector3 reticleClearanceOffset;
    private Vector3 reticleClearanceVelocity;
    private readonly Vector3[] raisedShoulderPoints = new Vector3[4];
    private Vector3 nativeAnchorDelta;
    private float nativePoseWeight;
    private bool nativePoseActive;
    private NativeShoulderCameraSettings.State nativePreviousState;
    private NativeShoulderCameraSettings.Pose nativePreviousPose;
    private float shoulderOrbitPitch;
    private float shoulderLookPitch;
    private float knifeFramingWeight;
    private float pistolFramingWeight;
    private float knifeFramingVelocity;
    private float pistolFramingVelocity;

    // 按参考录像调出的项目目标角度，录像本身未提供原作的镜头参数。
    private const float PistolUpAngle = 48f;
    private const float KnifeUpAngle = 52f;
    private const float ShoulderTransitionDamping = .15f;
    private const float ShoulderFrustumEdge = .93f;
    private const float ShoulderFitClearance = .045f;

    private bool UsesPistolShoulder => controller != null
        && (controller.currentWeapon == WeaponType.Pistol
            || controller.currentWeapon == WeaponType.Shotgun
            || controller.currentWeapon == WeaponType.Rifle)
        && (activeProfile == null || activeProfile.allowAim);

    internal float PistolFramingWeight
    {
        get { return pistolFramingWeight; }
    }

    internal Vector3 CurrentAimWorldForward
    {
        get
        {
            if (!isLocked && !inputSuppressed && followPivot != null && panTilt != null)
            {
                Quaternion orientation = followPivot.rotation
                    * Quaternion.Euler(panTilt.TiltAxis.Value, panTilt.PanAxis.Value, 0f);
                return orientation * Vector3.forward;
            }
            if (cam != null)
            {
                return cam.transform.forward;
            }
            return Vector3.forward;
        }
    }

    private float ReadPitchOrbitHeight()
    {
        if (controller == null)
        {
            return 0f;
        }

        return Mathf.Lerp(1.32f, 1.02f, crouchCameraWeight);
    }

    private bool TracksHeldKnifeInput
    {
        get
        {
            return !isLocked && !inputSuppressed && controller != null
                && (controller.IsHoldingKnifeGuard || controller.IsKnifeThrustAttack)
                && !controller.IsolateObserveCameraSteering;
        }
    }

    private void UpdateShoulderOrbit(float dt)
    {
        float targetKnifeFraming = 0f;
        float targetPistolFraming = 0f;
        if (controller != null && (controller.IsKnifeGuardAiming || controller.IsKnifeThrustAttack)
            && !controller.IsolateObserveCameraSteering)
        {
            targetKnifeFraming = 1f;
        }
        else if (controller != null && IsAiming && !controller.IsolateObserveCameraSteering
            && (UsesPistolShoulder || controller.currentWeapon == WeaponType.Rifle && (activeProfile == null || activeProfile.allowAim)))
        {
            targetPistolFraming = 1f;
        }
        if (dt <= 0f)
        {
            return;
        }

        // 输入改变时保留当前进度和速度，松开再按也沿同一条镜头曲线交接。
        knifeFramingWeight = Mathf.SmoothDamp(knifeFramingWeight, targetKnifeFraming, ref knifeFramingVelocity,
            ShoulderTransitionDamping, Mathf.Infinity, dt);
        pistolFramingWeight = Mathf.SmoothDamp(pistolFramingWeight, targetPistolFraming, ref pistolFramingVelocity,
            ShoulderTransitionDamping, Mathf.Infinity, dt);

        // 从同一输入取样。仰视时机位只小幅下降，保留原作的肩后视点。
        float requestedPitch = Mathf.Min(pitch, Mathf.Lerp(pitchRange.y, 55f, knifeFramingWeight));
        if (requestedPitch < 0f)
        {
            float inputRange = Mathf.Max(1f, -pitchRange.x);
            float upperAngle = inputRange;
            if (pistolFramingWeight + knifeFramingWeight > .001f)
            {
                upperAngle = Mathf.Lerp(PistolUpAngle, KnifeUpAngle, ReadKnifeFramingRatio());
            }
            requestedPitch *= upperAngle / inputRange;
        }
        if (TracksHeldKnifeInput)
        {
            shoulderLookPitch = requestedPitch;
        }
        else
        {
            shoulderLookPitch = Mathf.Lerp(shoulderLookPitch, requestedPitch, 1f - Mathf.Exp(-dt / 0.065f));
        }
        shoulderOrbitPitch = ResolveShoulderOrbitPitch(shoulderLookPitch);
    }

    private static float ResolveShoulderOrbitPitch(float lookPitch)
    {
        if (lookPitch < 0f)
        {
            return 24f * (Mathf.Exp(lookPitch / 48f) - 1f);
        }

        return 58f * (1f - Mathf.Exp(-lookPitch / 48f));
    }

    internal void SnapAfterVillageEntrance(Quaternion facing)
    {
        if (target == null || vcam == null || followPivot == null || panTilt == null || cam == null)
        {
            return;
        }
        SetVillageViewAngles(facing);
        // 复用平滑末镜已收敛的肩位与避障历史，不再重置碰撞状态，避免交接瞬间左右震荡。
        nativeAnchorDelta = Vector3.zero;
        basePivotPos = target.position + nativeAnchorDelta;
        pivotVel = Vector3.zero;
        unlockBoost = 0f;
        breathPhase = 0f;
        idleTimer = 0f;
        idleBreathWeight = 0f;
        idleBreathVelocity = 0f;
        currentEnemyZoom = 1f;
        enemyProximityHoldUntil = 0f;
        lastTargetYaw = GetCharacterYaw();
        turnLagTimer = 0f;
        framingPitchInitialized = false;
        smoothedFramingPitchOffset = 0f;
        framingPitchOffsetVelocity = 0f;
        externalHandoffSmoothPitchTimer = 0.45f;
        reticleClearanceOffset = Vector3.zero;
        reticleClearanceVelocity = Vector3.zero;
        AlignShoulderRig(basePivotPos);
        vcam.PreviousStateIsValid = true;
        Vector3 savedDamping = Vector3.zero;
        if (tpf != null)
        {
            savedDamping = tpf.Damping;
            tpf.Damping = Vector3.zero;
        }
        try
        {
            vcam.InternalUpdateCameraState(Vector3.up, Mathf.Max(0.001f, Time.unscaledDeltaTime));
        }
        finally
        {
            if (tpf != null)
            {
                tpf.Damping = savedDamping;
            }
        }
        var state = vcam.State;
        cam.transform.SetPositionAndRotation(state.GetFinalPosition(), state.GetFinalOrientation());
        cam.fieldOfView = state.Lens.FieldOfView;
    }

    internal bool TrySampleVillageShoulder(Vector3 exitPosition, Quaternion facing, bool resetCollisionState, out CameraState state)
    {
        state = default;
        if (vcam == null || followPivot == null || tpf == null || panTilt == null || target == null)
        {
            return false;
        }
        // 暂停期间刚体的显示位置可能仍在门口，使用源演员本帧的交还站位采样并同步避障观察原点。
        SetVillageViewAngles(facing);
        AlignShoulderRig(exitPosition);
        SetOverrideCollisionFocusRoot(true, exitPosition);
        Vector3 savedDamping = tpf.Damping;
        tpf.Damping = Vector3.zero;
        vcam.PreviousStateIsValid = !resetCollisionState;
        float sampleDeltaTime;
        if (resetCollisionState)
        {
            sampleDeltaTime = -1f;
        }
        else
        {
            sampleDeltaTime = Mathf.Max(0.001f, Time.unscaledDeltaTime);
        }
        try
        {
            vcam.InternalUpdateCameraState(Vector3.up, sampleDeltaTime);
            state = vcam.State;
        }
        finally
        {
            tpf.Damping = savedDamping;
            SetOverrideCollisionFocusRoot(false, Vector3.zero);
        }
        return true;
    }

    private void SetVillageViewAngles(Quaternion facing)
    {
        Vector3 angles = facing.eulerAngles;
        yaw = Mathf.DeltaAngle(0f, angles.y);
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), pitchRange.x, pitchRange.y);
    }

    private void AlignShoulderRig(Vector3 pivotPosition)
    {
        SnapShoulderPitch();
        followPivot.SetPositionAndRotation(pivotPosition + Vector3.up * ReadPitchOrbitHeight(),
            Quaternion.Euler(shoulderOrbitPitch, yaw, 0f));
        ApplyCameraParams();
        UpdatePanTilt(0f);
        var lens = vcam.Lens;
        lens.FieldOfView = curFOV;
        lens.Dutch = 0f;
        vcam.Lens = lens;
    }

    internal Vector2 CaptureShoulderViewAngles()
    {
        if (cam == null)
        {
            return new Vector2(yaw, pitch);
        }
        // 输入角在相机阻尼之前，转身途中要记住玩家最后实际看到的视角。
        Vector3 view = cam.transform.eulerAngles;
        return new Vector2(Mathf.DeltaAngle(0f, view.y), Mathf.DeltaAngle(0f, view.x));
    }

    // 补刀退出时使用玩家进入前的观察方向，武器和站姿仍由当前越肩配置提供镜头参数。
    internal void ResumeShoulderAtAngles(Vector2 angles)
    {
        yaw = angles.x;
        pitch = Mathf.Clamp(angles.y, pitchRange.x, pitchRange.y);
        pivotVel = Vector3.zero;
        unlockBoost = 0f;
        RefreshShoulderAfterExternalCamera();
    }

    internal void ResumeShoulderFromOutput()
    {
        SynchronizeFromCamera();
        RefreshShoulderAfterExternalCamera();
    }

    private void RefreshShoulderAfterExternalCamera()
    {
        if (cam == null || target == null || followPivot == null || vcam == null || panTilt == null)
        {
            return;
        }
        // 在 Cinemachine 计算交接之前完整对齐越肩支点并清空残余晃动/避障历史，避免交接瞬间抽动。
        basePivotPos = target.position + nativeAnchorDelta;
        pivotVel = Vector3.zero;
        unlockBoost = 0f;
        breathPhase = 0f;
        idleTimer = 0f;
        idleBreathWeight = 0f;
        idleBreathVelocity = 0f;
        currentEnemyZoom = 1f;
        enemyProximityHoldUntil = 0f;
        lastTargetYaw = GetCharacterYaw();
        turnLagTimer = 0f;
        framingPitchInitialized = false;
        smoothedFramingPitchOffset = 0f;
        framingPitchOffsetVelocity = 0f;
        externalHandoffSmoothPitchTimer = 0.45f;
        reticleClearanceOffset = Vector3.zero;
        reticleClearanceVelocity = Vector3.zero;
        SnapShoulderPitch();
        ApplyCameraParams();
        followPivot.SetPositionAndRotation(
            basePivotPos + Vector3.up * ReadPitchOrbitHeight(),
            Quaternion.Euler(shoulderOrbitPitch, yaw, 0f));
        UpdatePanTilt(0f);
        vcam.PreviousStateIsValid = false;
        vcam.InternalUpdateCameraState(Vector3.up, -1f);
    }

    private void SnapShoulderPitch()
    {
        shoulderLookPitch = pitch;
        shoulderOrbitPitch = ResolveShoulderOrbitPitch(pitch);
    }

    private float ReadShoulderModeWeight()
    {
        return Mathf.Clamp01(pistolFramingWeight + knifeFramingWeight);
    }

    private float ReadPistolCloseWeight()
    {
        return pistolFramingWeight;
    }

    private float ReadKnifeFramingRatio()
    {
        return knifeFramingWeight / Mathf.Max(.0001f, pistolFramingWeight + knifeFramingWeight);
    }

    internal Vector3 PlaceShoulderCamera(in CameraState state, Vector3 basePosition, float dt, bool previousStateValid)
    {
        float modeWeight = ReadShoulderModeWeight();
        if (modeWeight <= 0f || followPivot == null || controller == null || isLocked || inputSuppressed
            || controller.currentState == LeonState.Dead || controller.currentState == LeonState.QTE)
        {
            return basePosition;
        }

        CacheNativeShoulderAnchors();
        if (framingHead == null || framingSpine == null || framingLeftShoulder == null || framingRightShoulder == null)
        {
            return basePosition;
        }

        // 和 PanTilt 使用同一旋转。只解机位，画面中心仍是操作所给的方向。
        Quaternion orientation = followPivot.rotation * Quaternion.Euler(panTilt.TiltAxis.Value, panTilt.PanAxis.Value, 0f);
        Quaternion toCamera = Quaternion.Inverse(orientation);
        Vector3 shoulders = (framingLeftShoulder.position + framingRightShoulder.position) * .5f;
        Vector3 anchor = Vector3.Lerp(framingSpine.position, shoulders, .62f);
        float knifeRatio = ReadKnifeFramingRatio();
        Vector3 stableTorso = controller.transform.position + Vector3.up * Mathf.Lerp(1.38f, 1.10f, crouchCameraWeight);
        anchor = Vector3.Lerp(anchor, stableTorso, knifeRatio);
        float tanY = Mathf.Tan(state.Lens.FieldOfView * .5f * Mathf.Deg2Rad);
        float tanX = tanY * Mathf.Max(1f, state.Lens.Aspect);
        float nearDepth = Mathf.Max(.18f, state.Lens.NearClipPlane * 2f);
        Vector2 frustumScale = new Vector2(tanX, tanY);
        Vector3 shoulderSide = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * .22f;
        Vector3 head = Vector3.Lerp(framingHead.position + Vector3.up * .20f, stableTorso + Vector3.up * .43f, knifeRatio);
        Vector3 left = Vector3.Lerp(framingLeftShoulder.position, stableTorso - shoulderSide, knifeRatio);
        Vector3 right = Vector3.Lerp(framingRightShoulder.position, stableTorso + shoulderSide, knifeRatio);
        Vector3 spine = Vector3.Lerp(framingSpine.position, stableTorso - Vector3.up * .24f, knifeRatio);
        raisedShoulderPoints[0] = toCamera * (head - anchor);
        raisedShoulderPoints[1] = toCamera * (left - anchor);
        raisedShoulderPoints[2] = toCamera * (right - anchor);
        raisedShoulderPoints[3] = toCamera * (spine - anchor);
        Vector3 cameraPosition = basePosition;
        if (shoulderLookPitch < 0f && knifeFramingWeight > .001f)
        {
            Vector3 baseOffset = toCamera * (anchor - basePosition);
            cameraPosition = FitRaisedShoulderPosition(basePosition, baseOffset, orientation,
                frustumScale, nearDepth, knifeFramingWeight);
        }

        bool isLongGun = controller.currentWeapon == WeaponType.Shotgun || controller.currentWeapon == WeaponType.Rifle;
        Vector3 earLevelAnchor = Vector3.Lerp(shoulders, framingHead.position, 0.52f);
        Vector3 torsoAxisAnchor = new Vector3(stableTorso.x, earLevelAnchor.y, stableTorso.z);
        Vector3 gunPosition = SolveOverShoulderAimPosition(
            earLevelAnchor,
            torsoAxisAnchor,
            orientation,
            tanX,
            tanY,
            nearDepth,
            shoulderLookPitch,
            isLongGun,
            weaponRecoilDistanceOffset);

        Vector3 rightElbowPos = Vector3.zero;
        bool hasRightElbow = false;
        if (framingRightForearm != null)
        {
            rightElbowPos = framingRightForearm.position;
            hasRightElbow = true;
        }
        else if (framingRightUpperArm != null)
        {
            rightElbowPos = framingRightUpperArm.position;
            hasRightElbow = true;
        }

        Vector3 targetClearance = Vector3.zero;
        if (pistolFramingWeight > 0.001f)
        {
            targetClearance = ComputeReticleClearanceLocalOffset(
                gunPosition,
                orientation,
                tanX,
                tanY,
                shoulderLookPitch,
                framingRightShoulder.position,
                framingRightShoulder != null,
                rightElbowPos,
                hasRightElbow,
                framingHead.position,
                framingHead != null);
        }

        if (!previousStateValid || dt <= 0.0001f)
        {
            reticleClearanceOffset = targetClearance;
            reticleClearanceVelocity = Vector3.zero;
        }
        else
        {
            reticleClearanceOffset = Vector3.SmoothDamp(
                reticleClearanceOffset,
                targetClearance,
                ref reticleClearanceVelocity,
                0.10f,
                2.8f,
                dt);
        }

        gunPosition += orientation * reticleClearanceOffset;
        return Vector3.Lerp(cameraPosition, gunPosition, pistolFramingWeight);
    }

    /// <summary>
    /// 求解越肩 ADS 机位：平视高贴耳增强沉浸感，大俯角压低并外推角色锚点以避免躯干与长枪左偏遮挡视野。
    /// </summary>
    public static Vector3 SolveOverShoulderAimPosition(
        Vector3 earLevelAnchor,
        Vector3 torsoAxisAnchor,
        Quaternion orientation,
        float tanX,
        float tanY,
        float nearDepth,
        float lookPitch,
        bool isLongGun,
        float recoilDistanceOffset)
    {
        float axisBlend;
        if (isLongGun)
        {
            axisBlend = 0.38f;
        }
        else
        {
            axisBlend = 0.25f;
        }

        Vector3 stableAnchor = Vector3.Lerp(earLevelAnchor, torsoAxisAnchor, axisBlend);
        float steepDown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 68f, lookPitch));

        float baseDepth;
        float downPullback;
        float screenX;
        float screenY;
        if (isLongGun)
        {
            baseDepth = 0.76f;
            downPullback = 0.26f;
            screenX = Mathf.Lerp(-0.61f, -0.69f, steepDown);
            screenY = Mathf.Lerp(-0.26f, -0.61f, steepDown);
        }
        else
        {
            baseDepth = 0.67f;
            downPullback = 0.20f;
            screenX = Mathf.Lerp(-0.58f, -0.65f, steepDown);
            screenY = Mathf.Lerp(-0.24f, -0.55f, steepDown);
        }

        float depth = Mathf.Max(nearDepth, baseDepth) + downPullback * steepDown + recoilDistanceOffset;
        Vector3 shoulderFrame = new Vector3(screenX * tanX, screenY * tanY, 1f);
        return stableAnchor - orientation * (shoulderFrame * depth);
    }

    /// <summary>
    /// 实时监测右肩、右肘与头部外廓在视锥中的投影，若侵占中心准星区则计算相机局部让位偏移。
    /// </summary>
    public static Vector3 ComputeReticleClearanceLocalOffset(
        Vector3 baseGunPosition,
        Quaternion orientation,
        float tanX,
        float tanY,
        float lookPitch,
        Vector3 rightShoulderPos,
        bool hasRightShoulder,
        Vector3 rightElbowPos,
        bool hasRightElbow,
        Vector3 headPos,
        bool hasHead)
    {
        const float safeRightNdc = -0.15f;
        const float safeTopNdc = -0.20f;

        Quaternion toCamera = Quaternion.Inverse(orientation);
        float downWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, 60f, lookPitch));

        float maxRight = 0f;
        float maxUp = 0f;
        float maxBack = 0f;

        EvaluateClearancePoint(
            rightShoulderPos,
            hasRightShoulder,
            0.11f,
            baseGunPosition,
            toCamera,
            tanX,
            tanY,
            downWeight,
            safeRightNdc,
            safeTopNdc,
            ref maxRight,
            ref maxUp,
            ref maxBack);

        EvaluateClearancePoint(
            rightElbowPos,
            hasRightElbow,
            0.13f,
            baseGunPosition,
            toCamera,
            tanX,
            tanY,
            downWeight,
            safeRightNdc,
            safeTopNdc,
            ref maxRight,
            ref maxUp,
            ref maxBack);

        EvaluateClearancePoint(
            headPos,
            hasHead,
            0.11f,
            baseGunPosition,
            toCamera,
            tanX,
            tanY,
            downWeight,
            safeRightNdc,
            safeTopNdc,
            ref maxRight,
            ref maxUp,
            ref maxBack);

        maxRight = Mathf.Clamp(maxRight, 0f, 0.34f);
        maxUp = Mathf.Clamp(maxUp, 0f, 0.24f);
        maxBack = Mathf.Clamp(maxBack, 0f, 0.12f);

        return new Vector3(maxRight, maxUp, -maxBack);
    }

    private static void EvaluateClearancePoint(
        Vector3 worldPoint,
        bool valid,
        float radius,
        Vector3 baseGunPosition,
        Quaternion toCamera,
        float tanX,
        float tanY,
        float downWeight,
        float safeRightNdc,
        float safeTopNdc,
        ref float maxRight,
        ref float maxUp,
        ref float maxBack)
    {
        if (!valid)
        {
            return;
        }

        Vector3 local = toCamera * (worldPoint - baseGunPosition);
        if (local.z <= 0.06f)
        {
            return;
        }

        float safeTanX = Mathf.Max(0.05f, tanX);
        float safeTanY = Mathf.Max(0.05f, tanY);
        float halfW = local.z * safeTanX;
        float halfH = local.z * safeTanY;
        float ndcRight = (local.x + radius) / halfW;
        float ndcTop = (local.y + radius) / halfH;

        // 平视保持贴耳构图不被右下臂部微动反复推开，大俯角收敛至完整准星避让框并做双轴连续平滑过渡。
        float activeSafeRight = Mathf.Lerp(-0.04f, safeRightNdc, downWeight);
        float activeSafeTop = Mathf.Lerp(-0.06f, safeTopNdc, downWeight);
        const float transitionBand = 0.16f;
        float rightIntrusion = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeSafeRight - transitionBand, activeSafeRight, ndcRight));
        float topIntrusion = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(activeSafeTop - transitionBand, activeSafeTop, ndcTop));
        float intrusionWeight = rightIntrusion * topIntrusion;
        if (intrusionWeight <= 0.0001f)
        {
            return;
        }

        float rawPushRight = Mathf.Max(0f, (ndcRight - activeSafeRight) * halfW);
        float pushBack = Mathf.Min(0.10f, rawPushRight * 0.25f) * downWeight * intrusionWeight;
        float expandedHalfW = (local.z + pushBack) * safeTanX;
        float pushRight = Mathf.Max(0f, (local.x + radius) - activeSafeRight * expandedHalfW) * intrusionWeight;
        float pushUp = Mathf.Max(0f, (ndcTop - activeSafeTop) * halfH) * Mathf.Lerp(0.25f, 0.55f, downWeight) * intrusionWeight;

        if (pushRight > maxRight)
        {
            maxRight = pushRight;
        }

        if (pushUp > maxUp)
        {
            maxUp = pushUp;
        }

        if (pushBack > maxBack)
        {
            maxBack = pushBack;
        }
    }

    private Vector3 FitRaisedShoulderPosition(Vector3 basePosition, Vector3 baseOffset, Quaternion orientation,
        Vector2 frustumScale, float nearDepth, float modeWeight)
    {
        float knife = ReadKnifeFramingRatio();
        Vector2 screenCenter = new Vector2(Mathf.Lerp(-.54f, -.48f, knife), Mathf.Lerp(-.66f, -.58f, knife));
        float depth = Mathf.Lerp(.73f, .63f, knife);
        foreach (Vector3 point in raisedShoulderPoints)
        {
            FitShoulderDepth(point, screenCenter, frustumScale, nearDepth, ref depth);
        }

        Vector3 viewLine = new Vector3(screenCenter.x * frustumScale.x, screenCenter.y * frustumScale.y, 1f);
        Vector3 raisedOffset = viewLine * depth;
        float fullWeightAngle = Mathf.Lerp(34f, 25f, knife);
        float rise = Mathf.InverseLerp(3f, fullWeightAngle, -shoulderLookPitch);
        float weight = Mathf.SmoothStep(0f, 1f, rise);
        foreach (Vector3 point in raisedShoulderPoints)
        {
            float baseMargin = ReadShoulderFrustumMargin(point + baseOffset, frustumScale, nearDepth);
            if (baseMargin < 0f)
            {
                // 中途有骨骼点接近画面边缘时，提前移动机位，保持当前仰角。
                float raisedMargin = ReadShoulderFrustumMargin(point + raisedOffset, frustumScale, nearDepth);
                float needed = -baseMargin / (raisedMargin - baseMargin);
                weight = Mathf.Max(weight, needed);
            }
        }

        float activation = Mathf.SmoothStep(0f, 1f, -shoulderLookPitch / 6f);
        return basePosition + orientation * (baseOffset - raisedOffset) * (weight * modeWeight * activation);
    }

    private static float ReadShoulderFrustumMargin(Vector3 point, Vector2 scale, float nearDepth)
    {
        float horizontal = ShoulderFrustumEdge * point.z - Mathf.Abs(point.x) / scale.x;
        float vertical = ShoulderFrustumEdge * point.z - Mathf.Abs(point.y) / scale.y;
        return Mathf.Min(Mathf.Min(horizontal, vertical), point.z - nearDepth);
    }

    private static void FitShoulderDepth(Vector3 point, Vector2 center, Vector2 scale, float nearDepth, ref float depth)
    {
        // 两个机位用相同的视锥。抬升机位额外留余量，避免两端同时落在边缘外。
        float edge = ShoulderFrustumEdge;
        float clearance = ShoulderFitClearance;
        depth = Mathf.Max(depth, nearDepth - point.z + clearance);
        float horizontal = point.x / scale.x;
        float vertical = point.y / scale.y;
        depth = Mathf.Max(depth, (clearance - edge * point.z - horizontal) / (edge + center.x));
        depth = Mathf.Max(depth, (clearance + horizontal - edge * point.z) / (edge - center.x));
        depth = Mathf.Max(depth, (clearance - edge * point.z - vertical) / (edge + center.y));
        depth = Mathf.Max(depth, (clearance + vertical - edge * point.z) / (edge - center.y));
    }

    internal void KeepUpperBodyInFrame(ref CameraState state, float deltaTime)
    {
        if (controller == null || isLocked || inputSuppressed || controller.currentState == LeonState.Dead
            || controller.currentState == LeonState.QTE)
        {
            return;
        }

        CacheNativeShoulderAnchors();
        Vector3 playerPosition = controller.transform.position;
        Vector3 head = playerPosition + Vector3.up * Mathf.Lerp(1.72f, 1.32f, crouchCameraWeight);
        Vector3 hips = playerPosition + Vector3.up * Mathf.Lerp(.9f, .65f, crouchCameraWeight);
        if (framingHead != null)
        {
            head = Vector3.Lerp(framingHead.position + framingHead.up * .12f, head, knifeFramingWeight);
        }
        if (nativeHips != null)
        {
            hips = Vector3.Lerp(nativeHips.position - Vector3.up * .05f, hips, knifeFramingWeight);
        }
        if (framingSpine != null)
        {
            // 贴近右肩时以头和上背取景，腰胯可随推近自然离开下缘。
            hips = Vector3.Lerp(hips, framingSpine.position, ReadPistolCloseWeight());
        }

        // 在碰撞缩短支臂后，用当前机位和镜头视锥重新计算可用俯仰。
        Quaternion orientation = state.GetCorrectedOrientation();
        Vector3 angles = orientation.eulerAngles;
        float viewPitch = Mathf.DeltaAngle(0f, angles.x);
        Vector3 heading = Quaternion.Euler(0f, angles.y, 0f) * Vector3.forward;
        Vector3 cameraPosition = state.GetCorrectedPosition();
        Vector3 toHead = head - cameraPosition;
        Vector3 toHips = hips - cameraPosition;
        float headPitch = -Mathf.Atan2(toHead.y, Vector3.Dot(heading, toHead)) * Mathf.Rad2Deg;
        float hipsPitch = -Mathf.Atan2(toHips.y, Vector3.Dot(heading, toHips)) * Mathf.Rad2Deg;
        float halfFov = state.Lens.FieldOfView * .5f * Mathf.Deg2Rad;
        float viewportLimit = Mathf.Lerp(.94f, .86f, Mathf.SmoothStep(0f, 1f, viewPitch / 45f));
        float marginAngle = Mathf.Atan(Mathf.Tan(halfFov) * viewportLimit) * Mathf.Rad2Deg;
        float minPitch = Mathf.Max(headPitch, hipsPitch) - marginAngle;
        float maxPitch = Mathf.Min(headPitch, hipsPitch) + marginAngle;
        float correctedPitch = (minPitch + maxPitch) * .5f;
        if (minPitch <= maxPitch)
        {
            correctedPitch = Mathf.Clamp(viewPitch, minPitch, maxPitch);
        }

        // 从当前的肩后取景逐步交接到瞄准方向，水平附近也沿同一条曲线过渡。
        float modeWeight = ReadShoulderModeWeight();
        float raisedViewWeight = modeWeight * Mathf.SmoothStep(0f, 1f, -shoulderLookPitch / 6f);
        float inputPitchWeight = Mathf.Max(raisedViewWeight, ReadPistolCloseWeight());
        correctedPitch = Mathf.Lerp(correctedPitch, viewPitch, inputPitchWeight);

        // 对避障导致的构图俯仰差值做时间阻尼与角速度限制，并在强压缩时衰减俯仰扭转，防止画面垂直抖动致晕。
        float rawFramingDelta = correctedPitch - viewPitch;
        float compressionAttenuation = 1f - Mathf.SmoothStep(0.15f, 0.75f, currentCollisionCompression) * 0.7f;
        float targetFramingDelta = rawFramingDelta * compressionAttenuation;
        float frameDt = deltaTime;
        if (!framingPitchInitialized)
        {
            if (externalHandoffSmoothPitchTimer > 0f)
            {
                // 外部镜头交回越肩后从零偏移平滑过渡，避免首帧单帧硬切产生俯仰跳动。
                smoothedFramingPitchOffset = 0f;
                framingPitchOffsetVelocity = 0f;
                framingPitchInitialized = true;
            }
            else
            {
                smoothedFramingPitchOffset = targetFramingDelta;
                framingPitchOffsetVelocity = 0f;
                framingPitchInitialized = true;
            }
        }

        if (frameDt > 0.0001f)
        {
            float smoothTime = 0.09f;
            if (externalHandoffSmoothPitchTimer > 0f)
            {
                smoothTime = 0.14f;
            }
            smoothedFramingPitchOffset = Mathf.SmoothDamp(
                smoothedFramingPitchOffset,
                targetFramingDelta,
                ref framingPitchOffsetVelocity,
                smoothTime,
                140f,
                frameDt);
        }

        // 后坐力只加到最终视线，机位解算和构图限制始终使用稳定的瞄准方向。
        float finalFramingPitch = viewPitch + smoothedFramingPitchOffset + visibleWeaponRecoilPitch;

        // 碰撞或姿态变化时给取景留少量修正余量，输入仍受原俯仰范围约束。
        float correctedLimitUp = Mathf.Max(-88f, pitchRange.x - 8f);
        float correctedLimitDown = Mathf.Min(88f, pitchRange.y + 8f);
        var corrected = Quaternion.Euler(Mathf.Clamp(finalFramingPitch, correctedLimitUp, correctedLimitDown), angles.y, angles.z);
        state.OrientationCorrection = Quaternion.Inverse(state.RawOrientation) * corrected;
    }

    private float ReadShoulderOrbitPullback()
    {
        float amount = Mathf.Clamp01(Mathf.Abs(shoulderOrbitPitch) / 42f);
        return .6f * amount * amount;
    }

    private float ReadMinimumShoulderDistance()
    {
        // 视野较窄的枪械镜头多留一些距离，水平朝向也能容纳头肩与胯。
        float narrowLens = Mathf.InverseLerp(48f, 40f, curFOV);
        return Mathf.Lerp(1.25f, 1.55f, narrowLens);
    }

    private bool UpdateNativeShoulderCamera(float dt)
    {
        if (activeProfile == null || activeProfile.nativeShoulder == null || controller == null || target == null)
        {
            RestoreLegacyShoulderBlend(dt);
            return false;
        }

        var state = ResolveNativeShoulderState();
        if (!activeProfile.nativeShoulder.TryGetPose(state, previewUnverifiedNativeShoulder, out var pose))
        {
            RestoreLegacyShoulderBlend(dt);
            return false;
        }

        CacheNativeShoulderAnchors();
        Transform anchor = ResolveNativeShoulderAnchor(pose.anchor);
        if (anchor == null)
        {
            RestoreLegacyShoulderBlend(dt);
            return false;
        }

        float rate = pose.inputRate;
        float damping = pose.inputDamping;
        if (nativePoseActive && nativePreviousState == NativeShoulderCameraSettings.State.Aim
            && state != nativePreviousState && nativePreviousPose.useOutput)
        {
            nativeExitRate = nativePreviousPose.outputRate;
            nativeExitDuration = nativePreviousPose.outputDamping;
            nativeExitDamping = nativeExitDuration;
        }
        if (state == NativeShoulderCameraSettings.State.Aim)
        {
            nativeExitDamping = 0f;
        }
        if (nativeExitDamping > 0f)
        {
            rate = nativeExitRate;
            damping = nativeExitDuration;
            nativeExitDamping = Mathf.Max(0f, nativeExitDamping - dt);
        }

        float weight = 1f - Mathf.Pow(1f - Mathf.Clamp(rate, 0.01f, 0.999f), dt / Mathf.Max(0.01f, damping));
        var goal = new StateParams
        {
            fov = Camera.HorizontalToVerticalFieldOfView(pose.HorizontalFov, cam.aspect),
            cameraDistance = pose.distance - pose.offset.z,
            verticalArm = pose.offset.y,
            shoulderX = pose.offset.x,
            damping = Vector3.one * Mathf.Max(0.001f, pose.followDamping)
        };
        ApplyStateParams(LerpStateParams(CaptureCurrentStateParams(), goal, weight));
        nativeAnchorDelta = Vector3.Lerp(nativeAnchorDelta, anchor.position - target.position, weight);
        nativePoseWeight = Mathf.Lerp(nativePoseWeight, 1f, weight);
        nativePoseActive = true;
        nativePreviousState = state;
        nativePreviousPose = pose;
        return true;
    }

    private float nativeExitRate;
    private float nativeExitDamping;
    private float nativeExitDuration;

    private void RestoreLegacyShoulderBlend(float dt)
    {
        if (nativePoseActive)
        {
            nativePoseActive = false;
            nativeExitDamping = 0f;
        }

        float weight = Mathf.Clamp01(dt * paramBlendSpeed);
        nativeAnchorDelta = Vector3.Lerp(nativeAnchorDelta, Vector3.zero, weight);
        nativePoseWeight = Mathf.Lerp(nativePoseWeight, 0f, weight);
    }

    private NativeShoulderCameraSettings.State ResolveNativeShoulderState()
    {
        if (IsAiming)
        {
            return NativeShoulderCameraSettings.State.Aim;
        }
        if (controller.IsCrouching)
        {
            return NativeShoulderCameraSettings.State.Crouch;
        }
        if (IsSprinting)
        {
            return NativeShoulderCameraSettings.State.Sprint;
        }
        var gait = controller.AnimDriver;
        bool isJogging = gait != null && gait.IsNativeJogging && controller.CurrentPlanarSpeed > 1.25f;
        if (isJogging || controller.currentState == LeonState.Running && controller.CurrentPlanarSpeed > controller.walkSpeed)
        {
            return NativeShoulderCameraSettings.State.Jog;
        }

        var enemies = EnemyAIManager.Instance;
        if (enemies != null && enemies.HasEngagedEnemy())
        {
            return NativeShoulderCameraSettings.State.Battle;
        }
        return NativeShoulderCameraSettings.State.Normal;
    }

    private void CacheNativeShoulderAnchors()
    {
        if (nativeAnchorOwner == controller.transform)
        {
            return;
        }
        nativeAnchorOwner = controller.transform;
        nativeGround = nativeAnchorOwner.Find("Armature/root ground");
        nativeHips = nativeAnchorOwner.Find("Armature/root ground/root hips");
        nativeChest = nativeAnchorOwner.Find("Armature/root ground/root hips/spine lower/spine middle");
        framingHead = null;
        framingSpine = null;
        framingLeftShoulder = null;
        framingRightShoulder = null;
        framingRightUpperArm = null;
        framingRightForearm = null;
        framingRightWrist = null;
        foreach (var bone in nativeAnchorOwner.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == "head")
            {
                framingHead = bone;
            }
            else if (bone.name == "spine upper")
            {
                framingSpine = bone;
            }
            else if (bone.name == "arm left shoulder")
            {
                framingLeftShoulder = bone;
            }
            else if (bone.name == "arm right shoulder")
            {
                framingRightShoulder = bone;
            }
            else if (bone.name == "arm right upperarm")
            {
                framingRightUpperArm = bone;
            }
            else if (bone.name == "arm right forearm")
            {
                framingRightForearm = bone;
            }
            else if (bone.name == "arm right wrist")
            {
                framingRightWrist = bone;
            }
        }
    }

    private Transform ResolveNativeShoulderAnchor(NativeShoulderCameraSettings.Anchor anchor)
    {
        switch (anchor)
        {
            case NativeShoulderCameraSettings.Anchor.Hips:
                return nativeHips;
            case NativeShoulderCameraSettings.Anchor.Chest:
                return nativeChest;
            default:
                return nativeGround;
        }
    }
}

