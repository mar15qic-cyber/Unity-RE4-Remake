using Unity.Cinemachine;
using UnityEngine;

public sealed partial class QTECameraPresenter
{
    [Tooltip("按配对动画源帧采样的原版镜头。通过验收的场景可绑定此资源。")]
    [SerializeField] private NativeQTECameraTracks nativeTracks;

    private readonly RaycastHit[] nativeSightHits = new RaycastHit[24];
    private readonly Collider[] nativeCameraOverlaps = new Collider[16];
    private CinemachineCamera nativeCamera;
    private NativeQTECameraTracks activeNativeTracks;
    private NativeQTECameraTracks.Track nativeTrack;
    private string nativePlayerClip;
    private bool nativeCameraTaken;
    private float nextNativeTrackSearch;
    private CinemachineCamera stableExternalCamera;
    private Pose stableQtePose;
    private float stableQteFov;
    private float qteObstructedSince = -1f;
    private float qteClearSince = -1f;
    private bool qteNeedsStableClearance;
    private bool nativeMirrored;
    private CinemachineCore.GetBlendOverrideDelegate previousNativeBlendOverride;
    private bool nativeBlendOverrideRegistered;
    private float nativeExitBlendSeconds = .18f;
    private bool resultCameraReleased;
    private CinemachineBlendDefinition.Styles nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseOut;

    private void RegisterNativeBlendOverride()
    {
        if (nativeBlendOverrideRegistered)
        {
            return;
        }
        previousNativeBlendOverride = CinemachineCore.GetBlendOverride;
        CinemachineCore.GetBlendOverride = ResolveNativeBlend;
        nativeBlendOverrideRegistered = true;
    }

    private void UnregisterNativeBlendOverride()
    {
        if (!nativeBlendOverrideRegistered)
        {
            return;
        }
        if (CinemachineCore.GetBlendOverride == ResolveNativeBlend)
        {
            CinemachineCore.GetBlendOverride = previousNativeBlendOverride;
        }
        else
        {
            CinemachineCore.GetBlendOverride -= ResolveNativeBlend;
        }
        nativeBlendOverrideRegistered = false;
        previousNativeBlendOverride = null;
    }

    private CinemachineBlendDefinition ResolveNativeBlend(ICinemachineCamera from, ICinemachineCamera to,
        CinemachineBlendDefinition fallback, Object owner)
    {
        if (previousNativeBlendOverride != null)
        {
            fallback = previousNativeBlendOverride(from, to, fallback, owner);
        }
        if (IsContinuousQteCamera(to))
        {
            // 进入已按动作源帧混合，不能再叠加主相机默认的两秒过渡。
            return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }
        if (cameraController != null && to == cameraController.gameplayCamera && from is CinemachineCamera outgoing &&
            runtimeRoot != null && outgoing.transform.IsChildOf(runtimeRoot))
        {
            float duration = nativeExitBlendSeconds;
            if (to != null)
            {
                CameraState next = to.State;
                CameraState last = from.State;
                if (!IsQteCameraMovementClear(last.GetFinalPosition(), next.GetFinalPosition(), .15f))
                {
                    return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                }
                // 近景转回越肩时，按实际角度和位移保证足够的回收时间。
                float turn = Quaternion.Angle(last.GetFinalOrientation(), next.GetFinalOrientation());
                float travel = Vector3.Distance(last.GetFinalPosition(), next.GetFinalPosition());
                duration = Mathf.Max(duration, turn / 150f, travel / 5f);
                if (groundReturnAngles.HasValue)
                {
                    duration = Mathf.Max(duration, Mathf.Abs(last.Lens.FieldOfView - next.Lens.FieldOfView) / 60f);
                }
            }
            return new CinemachineBlendDefinition(nativeExitBlendStyle, duration);
        }
        return fallback;
    }

    private bool IsContinuousQteCamera(ICinemachineCamera camera)
    {
        return camera != null && (ReferenceEquals(camera, nativeCamera) || ReferenceEquals(camera, stableExternalCamera));
    }

    private bool TryStartNativeCameraSession(int sessionId)
    {
        var tracks = nativeTracks;
        if (tracks == null && coordinator.config != null)
        {
            tracks = coordinator.config.nativeCameras;
        }
        if (tracks == null || cameraController == null || !coordinator.HasNativePairCameraClock(sessionId)
            || !coordinator.TryGetSessionContext(sessionId, out var context)
            || context.kind == QTEKind.RearBiteNeck)
        {
            return false;
        }

        nativeExitBlendSeconds = .18f;
        nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseOut;
        activeNativeTracks = tracks;
        if (context.kind == QTEKind.GroundFinisher && context.enemyRoot.TryGetComponent<VillagerController>(out var enemy)
            && enemy.missionEnemyType == MissionEnemyType.Sawman)
        {
            activeNativeTracks = Resources.Load<NativeQTECameraTracks>("SawKnifeCamera");
            if (activeNativeTracks == null)
            {
                return false;
            }
        }
        activeSessionId = sessionId;
        sessionContext = context;
        if (context.kind == QTEKind.FrontGrab && cameraController.MainCamera != null)
        {
            Vector3 focus = (context.playerRoot.position + context.enemyRoot.position) * .5f;
            frontGrabEntryRadial = Vector3.ProjectOnPlane(cameraController.MainCamera.transform.position - focus, Vector3.up).normalized;
        }
        BeginGroundFinisherCamera();
        CacheFocusBones();
        return true;
    }

    private void UpdateNativeCamera()
    {
        if (!sessionContext.IsValid)
        {
            ReleaseCamera();
            return;
        }

        if (!coordinator.TryGetNativePairCameraFrame(activeSessionId, out string playerClip,
            out float sourceFrame, out int frameRate, out Pose stage))
        {
            return;
        }

        if (NativeGroundFinisherTiming.TryGet(playerClip, out var timing) && sourceFrame >= timing.cameraSwitchFrame)
        {
            nativeExitBlendSeconds = timing.cameraBlendFrames / (float)frameRate;
            nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseIn;
            if (timing.smoothCameraBlend)
            {
                nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
            }
            ReleaseNativeCamera();
            return;
        }

        Pose pose = default;
        float horizontalFov = 64f;
        bool hasCandidate;
        if (coordinator.IsNativePairPlayerRecovering)
        {
            hasCandidate = TryFramePlayerRecovery(out pose, out horizontalFov);
        }
        else if (NativeGroundFinisherTiming.TryGet(playerClip, out _) && playerClip != "LeonPair_SawKneel1020")
        {
            hasCandidate = TrySampleGroundFinisherCamera(out pose, out horizontalFov);
        }
        else
        {
            bool clipChanged = nativePlayerClip != playerClip;
            nativePlayerClip = playerClip;
            if (clipChanged)
            {
                nativeTrack = null;
                nextNativeTrackSearch = 0f;
            }

            hasCandidate = nativeTrack != null
                && TrySampleNativePose(nativeTrack, sourceFrame, stage, nativeMirrored, out pose, out horizontalFov);
            if (!hasCandidate && Time.time >= nextNativeTrackSearch
                && (clipChanged || nativeTrack == null || qteObstructedSince >= 0f && Time.time - qteObstructedSince >= .10f))
            {
                nativeTrack = SelectNativeTrack(playerClip, frameRate, sourceFrame, stage);
                nextNativeTrackSearch = Time.time + .16f;
                hasCandidate = nativeTrack != null
                    && TrySampleNativePose(nativeTrack, sourceFrame, stage, nativeMirrored, out pose, out horizontalFov);
            }
        }

        nativeCameraTaken = PresentContinuousQteCamera(GetContinuousQteCamera(), hasCandidate, pose, horizontalFov);
        if (nativeCameraTaken && hasCandidate && sessionContext.kind == QTEKind.FrontGrab && frontGrabShotRadial.sqrMagnitude < .01f)
        {
            ReadGrabAnchors(out var leon, out var enemy);
            frontGrabShotRadial = Vector3.ProjectOnPlane(pose.position - (leon + enemy) * .5f, Vector3.up).normalized;
        }
    }

    private CinemachineCamera GetContinuousQteCamera()
    {
        if (nativeCamera == null)
        {
            var cameraObject = new GameObject("NativePairQTECamera");
            cameraObject.transform.SetParent(runtimeRoot, false);
            nativeCamera = cameraObject.AddComponent<CinemachineCamera>();
            nativeCamera.Priority = 0;
            nativeCamera.enabled = false;
        }
        float nearClipPlane = LensSettings.Default.NearClipPlane;
        if (sessionContext.kind == QTEKind.KnifeAssassination)
        {
            nearClipPlane = AssassinationNearClipPlane;
        }
        // 交接直接插值镜头朝向和镜头参数，避免 Cinemachine 另按两个注视点重算旋转和 FOV。
        nativeCamera.BlendHint = default;
        if (groundReturnAngles.HasValue)
        {
            nativeCamera.BlendHint = CinemachineCore.BlendHints.IgnoreTarget
                | CinemachineCore.BlendHints.FreezeWhenBlendingOut;
        }
        var lens = nativeCamera.Lens;
        if (lens.NearClipPlane != nearClipPlane)
        {
            lens.NearClipPlane = nearClipPlane;
            nativeCamera.Lens = lens;
        }
        return nativeCamera;
    }

    private bool PresentContinuousQteCamera(CinemachineCamera camera, bool hasCandidate, Pose destination, float horizontalFov)
    {
        Camera output = cameraController.MainCamera;
        if (output == null || resultCameraReleased)
        {
            return false;
        }

        float now = Time.time;
        bool active = stableExternalCamera == camera && camera.enabled;
        if (!active && qteNeedsStableClearance && stableExternalCamera == camera &&
            coordinator.CurrentPhase is QTEPhase.SuccessResult or QTEPhase.FailureResult)
        {
            // 抓取阶段已经因遮挡交回越肩时，收尾也沿用玩家当前看到的机位。
            resultCameraReleased = true;
            return false;
        }
        float verticalFov = Camera.HorizontalToVerticalFieldOfView(horizontalFov, output.aspect);
        Pose previous = new Pose(output.transform.position, output.transform.rotation);
        float previousFov = output.fieldOfView;
        if (active)
        {
            previous = stableQtePose;
            previousFov = stableQteFov;
        }

        float dt = Mathf.Min(.05f, Time.deltaTime);
        if (groundReturnAngles.HasValue)
        {
            // 接管首帧沿用当前画面，随后逐步跟上源镜头，避免一帧内抬头或缩放。
            if (!active)
            {
                groundCameraEntryTime = now;
            }
            float entryWeight = Mathf.SmoothStep(0f, 1f, (now - groundCameraEntryTime) / .12f);
            dt *= entryWeight;
        }
        float nextFov = previousFov;
        Pose next = previous;
        if (hasCandidate)
        {
            float weight = 1f - Mathf.Exp(-16f * dt);
            next = new Pose(
                Vector3.MoveTowards(previous.position, Vector3.Lerp(previous.position, destination.position, weight), 9f * dt),
                Quaternion.RotateTowards(previous.rotation, Quaternion.Slerp(previous.rotation, destination.rotation, weight), 285f * dt));
            nextFov = Mathf.MoveTowards(previousFov, Mathf.Lerp(previousFov, verticalFov, weight), 88f * dt);
        }

        float nearClipPlane = output.nearClipPlane;
        if (sessionContext.kind == QTEKind.KnifeAssassination)
        {
            nearClipPlane = camera.Lens.NearClipPlane;
        }
        float radius = CameraRouteClearance.ReadNearPlaneRadius(nearClipPlane, Mathf.Max(previousFov, nextFov), output.aspect);
        bool movementClear = hasCandidate && IsQteCameraMovementClear(previous.position, next.position, radius);
        if (!movementClear)
        {
            qteClearSince = -1f;
            if (qteObstructedSince < 0f)
            {
                qteObstructedSince = now;
            }
            if (active && now - qteObstructedSince < .15f && !HasQteCameraOverlap(previous.position, radius))
            {
                // 样条组件也会更新机位。短遮挡期间送回最后一个安全输出。
                WriteContinuousQtePose(camera, previous, previousFov);
                return true;
            }
            if (active)
            {
                cameraController.ReleaseExternalCamera(this, true, groundReturnAngles);
                if (coordinator.CurrentPhase is QTEPhase.SuccessResult or QTEPhase.FailureResult)
                {
                    resultCameraReleased = true;
                }
            }
            qteNeedsStableClearance = true;
            return false;
        }

        qteObstructedSince = -1f;
        if (!active && qteNeedsStableClearance)
        {
            if (qteClearSince < 0f)
            {
                qteClearSince = now;
            }
            if (now - qteClearSince < .20f)
            {
                return false;
            }
        }

        WriteContinuousQtePose(camera, next, nextFov);
        stableExternalCamera = camera;
        if (!active && !cameraController.RequestExternalCamera(this, camera, true))
        {
            return false;
        }
        qteNeedsStableClearance = false;
        return true;
    }

    private void WriteContinuousQtePose(CinemachineCamera camera, Pose pose, float fov)
    {
        stableQtePose = pose;
        stableQteFov = fov;
        camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        var lens = camera.Lens;
        lens.FieldOfView = fov;
        camera.Lens = lens;
    }

    private bool HasQteCameraOverlap(Vector3 position, float radius)
    {
        return CameraRouteClearance.HasOverlap(position, radius, Physics.DefaultRaycastLayers, nativeCameraOverlaps,
            sessionContext.playerRoot, sessionContext.enemyRoot);
    }

    private bool IsQteCameraMovementClear(Vector3 from, Vector3 to, float radius)
    {
        return !HasQteCameraOverlap(to, radius) && !CameraRouteClearance.Sweep(from, to, radius,
            Physics.DefaultRaycastLayers, nativeSightHits, sessionContext.playerRoot, sessionContext.enemyRoot, out _);
    }

    private NativeQTECameraTracks.Track SelectNativeTrack(string playerClip, int frameRate, float frame, Pose stage)
    {
        NativeQTECameraTracks.Track best = null;
        bool bestMirror = false;
        float bestScore = float.PositiveInfinity;
        var candidate = activeNativeTracks.Find(playerClip, frameRate);
        while (candidate != null)
        {
            int count = 1;
            if (UsesGrabComposition)
            {
                count = 2;
            }

            for (int index = 0; index < count; index++)
            {
                bool mirrored = index == 1;
                if (!TrySampleNativePose(candidate, frame, stage, mirrored, out Pose pose, out float fov))
                {
                    continue;
                }

                float score = 0f;
                if (UsesGrabComposition)
                {
                    score = ScoreGrabComposition(pose, fov, grabScreenSide);
                }
                if (score < bestScore)
                {
                    best = candidate;
                    bestMirror = mirrored;
                    bestScore = score;
                }
            }

            if (best != null && !UsesGrabComposition)
            {
                break;
            }

            candidate = activeNativeTracks.FindAlternative(candidate);
        }

        nativeMirrored = bestMirror;
        if (best != null && UsesGrabComposition &&
            TrySampleNativePose(best, frame, stage, bestMirror, out Pose selectedPose, out float selectedFov))
        {
            RememberGrabScreenSide(selectedPose, selectedFov);
        }

        return best;
    }

    private bool TrySampleNativePose(NativeQTECameraTracks.Track track, float frame, Pose stage,
        bool mirrored, out Pose pose, out float fov)
    {
        track.Sample(frame, out Pose local, out fov);
        if (mirrored)
        {
            local.position.x = -local.position.x;
            Vector3 forward = local.rotation * Vector3.forward;
            Vector3 up = local.rotation * Vector3.up;
            forward.x = -forward.x;
            up.x = -up.x;
            local.rotation = Quaternion.LookRotation(forward, up);
        }

        pose = new Pose(stage.position + stage.rotation * local.position, stage.rotation * local.rotation);
        if (UsesGrabComposition)
        {
            return TryFrameGrab(pose, fov, out pose, out fov, frame);
        }

        if (IsNativeCameraClear(pose.position))
        {
            return true;
        }

        Vector3 focus = ReadQteClearanceFocus();
        Vector3 radial = pose.position - focus;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            Vector3 position = focus + radial * (1f - attempt * .19f) + Vector3.up * (.16f * attempt);
            Vector3 direction = focus - position;
            if (direction.sqrMagnitude > .16f && IsNativeCameraClear(position))
            {
                pose = new Pose(position, Quaternion.LookRotation(direction, Vector3.up));
                return true;
            }
        }
        return false;
    }

    private Vector3 ReadQteClearanceFocus()
    {
        float height = 1.15f;
        if (sessionContext.kind == QTEKind.GroundFinisher)
        {
            height = .65f;
        }
        return (sessionContext.playerRoot.position + sessionContext.enemyRoot.position) * .5f + Vector3.up * height;
    }

    private bool IsNativeCameraClear(Vector3 cameraPosition)
    {
        Vector3 focus = ReadQteClearanceFocus();
        return (focus - cameraPosition).sqrMagnitude > .05f * .05f
            && IsQteCameraMovementClear(focus, cameraPosition, .13f);
    }

    private void ReleaseNativeCamera()
    {
        if (nativeCameraTaken && cameraController != null)
        {
            cameraController.ReleaseExternalCamera(this, true, groundReturnAngles);
        }
        nativeCameraTaken = false;
    }

    private void ResetNativeCameraSession()
    {
        ReleaseNativeCamera();
        groundReturnAngles = null;
        activeNativeTracks = null;
        nativePlayerClip = null;
        nativeTrack = null;
        nextNativeTrackSearch = 0f;
        nativeMirrored = false;
        frontGrabEntryRadial = Vector3.zero;
        frontGrabShotRadial = Vector3.zero;
        standingResultOffset = Vector3.zero;
        resultCameraReleased = false;
        stableExternalCamera = null;
        qteObstructedSince = -1f;
        qteClearSince = -1f;
        qteNeedsStableClearance = false;
    }
}

