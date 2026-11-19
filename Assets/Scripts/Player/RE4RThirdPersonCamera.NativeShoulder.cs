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
    private float pistolClearanceDistance;
    private float pistolClearanceVelocity;

    // 按参考录像调出的项目目标角度，录像本身未提供原作的镜头参数。
    private const float PistolUpAngle = 48f;
    private const float KnifeUpAngle = 52f;
    private const float ShoulderTransitionDamping = .15f;
    private const float ShoulderFrustumEdge = .93f;
    private const float ShoulderFitClearance = .045f;

    private bool UsesPistolShoulder => controller != null
        && (controller.currentWeapon == WeaponType.Pistol || controller.currentWeapon == WeaponType.Shotgun)
        && (activeProfile == null || activeProfile.allowAim);

    private float ReadPitchOrbitHeight()
    {
        if (controller == null)
        {
            return 0f;
        }

        return Mathf.Lerp(1.32f, 1.02f, crouchCameraWeight);
    }

    private void UpdateShoulderOrbit(float dt)
    {
        float targetKnifeFraming = 0f;
        float targetPistolFraming = 0f;
        if (controller != null && controller.IsKnifeGuardAiming && !controller.IsolateObserveCameraSteering)
        {
            targetKnifeFraming = 1f;
        }
        else if (UsesPistolShoulder && IsAiming && !controller.IsolateObserveCameraSteering)
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
        shoulderLookPitch = Mathf.Lerp(shoulderLookPitch, requestedPitch, 1f - Mathf.Exp(-dt / 0.065f));
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
        // 大仰角交给视锥构图解算，避免近肩推进和仰视机位同时拉动镜头。
        float raised = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 34f, -shoulderLookPitch));
        return pistolFramingWeight * (1f - raised);
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
            pistolClearanceDistance = 0f;
            pistolClearanceVelocity = 0f;
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
        if (shoulderLookPitch < 0f)
        {
            Vector3 baseOffset = toCamera * (anchor - basePosition);
            cameraPosition = FitRaisedShoulderPosition(basePosition, baseOffset, orientation,
                frustumScale, nearDepth, modeWeight);
        }

        float closeWeight = ReadPistolCloseWeight();
        if (closeWeight <= 0f)
        {
            pistolClearanceDistance = 0f;
            pistolClearanceVelocity = 0f;
            return cameraPosition;
        }

        // 平移瞄准时给肩背留出步态摆动的余量，视线继续跟随准星输入。
        Vector3 anchorOffset = toCamera * (anchor - cameraPosition);
        float retreat = 0f;
        foreach (Vector3 point in raisedShoulderPoints)
        {
            float margin = ReadShoulderFrustumMargin(point + anchorOffset, frustumScale, nearDepth);
            retreat = Mathf.Max(retreat, (.06f - margin) / ShoulderFrustumEdge);
        }

        if (!previousStateValid || dt < 0f)
        {
            pistolClearanceDistance = retreat;
            pistolClearanceVelocity = 0f;
        }
        else if (dt > 0f)
        {
            float damping = .14f;
            if (retreat > pistolClearanceDistance)
            {
                damping = .055f;
            }
            pistolClearanceDistance = Mathf.SmoothDamp(pistolClearanceDistance, retreat,
                ref pistolClearanceVelocity, damping, Mathf.Infinity, dt);
        }
        return cameraPosition - orientation * Vector3.forward * (pistolClearanceDistance * closeWeight);
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

    internal void KeepUpperBodyInFrame(ref CameraState state)
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
        // 后坐力只加到最终视线，机位解算和构图限制始终使用稳定的瞄准方向。
        correctedPitch += visibleWeaponRecoilPitch;

        // 碰撞或姿态变化时给取景留少量修正余量，输入仍受原俯仰范围约束。
        float correctedLimitUp = Mathf.Max(-88f, pitchRange.x - 8f);
        float correctedLimitDown = Mathf.Min(88f, pitchRange.y + 8f);
        var corrected = Quaternion.Euler(Mathf.Clamp(correctedPitch, correctedLimitUp, correctedLimitDown), angles.y, angles.z);
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

