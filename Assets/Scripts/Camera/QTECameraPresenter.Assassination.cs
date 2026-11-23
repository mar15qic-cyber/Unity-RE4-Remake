using Unity.Cinemachine;
using UnityEngine;

public sealed partial class QTECameraPresenter
{
    private const float AssassinationNearClipPlane = .06f;

    private QTECameraProfile assassinationProfile;
    private float assassinationEntryTime = -1f;
    private float assassinationContactTime = -1f;
    private Vector3 assassinationForward;
    private float assassinationSide;
    private bool assassinationSideLocked;

    private void StartAssassinationCamera(int sessionId)
    {
        if (assassinationProfile == null)
        {
            Debug.LogWarning("[快速反应事件摄像机] 缺少暗杀镜头配置，会话继续使用游戏摄像机。", this);
            return;
        }
        if (cameraController == null || !coordinator.TryGetSessionContext(sessionId, out var context))
        {
            return;
        }

        sessionContext = context;
        activeSessionId = sessionId;
        assassinationSideLocked = false;
        nativeExitBlendSeconds = .32f;
        nativeExitBlendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
        CacheFocusBones();
    }

    private void HandleAssassinationContact(int sessionId)
    {
        if (sessionId != 0 && sessionId == activeSessionId && sessionContext.kind == QTEKind.KnifeAssassination
            && assassinationContactTime < 0f)
        {
            assassinationContactTime = Time.time;
        }
    }

    private void UpdateAssassinationCamera()
    {
        if (!sessionContext.IsValid)
        {
            ReleaseCamera();
            return;
        }

        Camera output = cameraController.MainCamera;
        if (output == null || coordinator.CurrentPhase is not (QTEPhase.Entering or QTEPhase.WaitingForInput
            or QTEPhase.SuccessResult or QTEPhase.FailureResult))
        {
            return;
        }

        if (assassinationEntryTime < 0f)
        {
            assassinationEntryTime = Time.time;
            // 对位结束后记录双人朝向，避免受击者转身或倒地时把镜头带到正面。
            Transform player = sessionContext.playerRoot;
            Vector3 separation = sessionContext.enemyRoot.position - player.position;
            assassinationForward = Vector3.ProjectOnPlane(separation, Vector3.up);
            if (assassinationForward.sqrMagnitude < .04f)
            {
                assassinationForward = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            }
            assassinationForward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, assassinationForward);
            float outputSide = Vector3.Dot(output.transform.position - player.position, right);
            assassinationSide = 1f;
            if (outputSide < -.12f)
            {
                assassinationSide = -1f;
            }
        }

        bool framed = TryFrameAssassination(output, out var pose, out float fov, out float side);
        nativeCameraTaken = PresentContinuousQteCamera(GetContinuousQteCamera(), framed, pose, fov);
        if (nativeCameraTaken && framed)
        {
            assassinationSide = side;
            assassinationSideLocked = true;
        }
    }

    private bool TryFrameAssassination(Camera output, out Pose pose, out float fov, out float side)
    {
        pose = default;
        side = assassinationSide;
        QTECameraProfile profile = assassinationProfile;

        float entryElapsed = Time.time - assassinationEntryTime;
        float entryTime = Mathf.Clamp01(entryElapsed / Mathf.Max(.01f, profile.entryDuration));
        float entryProgress = Mathf.SmoothStep(0f, 1f, entryTime);
        if (profile.entryCurve != null)
        {
            entryProgress = Mathf.Clamp01(profile.entryCurve.Evaluate(entryTime));
        }
        float contactProgress = 0f;
        float aftermathProgress = 0f;
        if (assassinationContactTime >= 0f)
        {
            float contactElapsed = Time.time - assassinationContactTime;
            contactProgress = Mathf.SmoothStep(0f, 1f, contactElapsed / Mathf.Max(.01f, profile.assassinationContactDuration));
            float aftermathElapsed = contactElapsed - profile.assassinationContactDuration;
            aftermathProgress = Mathf.SmoothStep(0f, 1f, aftermathElapsed / Mathf.Max(.01f, profile.assassinationAftermathDuration));
        }

        float entryAngle = 36f;
        float entryHeight = .18f;
        if (sessionContext.startedCrouching)
        {
            entryAngle = 24f;
            entryHeight = -.16f;
        }
        float angle = Mathf.Lerp(entryAngle, 72f, entryProgress);
        angle = Mathf.Lerp(angle, 80f, contactProgress);
        angle = Mathf.Lerp(angle, 58f, aftermathProgress);
        float height = Mathf.Lerp(entryHeight, -.12f, entryProgress);
        height = Mathf.Lerp(height, -.03f, contactProgress);
        height = Mathf.Lerp(height, .65f, aftermathProgress);
        float distance = Mathf.Lerp(2.55f, 1.98f, entryProgress);
        distance = Mathf.Lerp(distance, 1.86f, contactProgress);
        distance = Mathf.Lerp(distance, 2.60f, aftermathProgress);

        Transform player = sessionContext.playerRoot;
        Transform enemy = sessionContext.enemyRoot;
        Vector3 leon = GetBonePosition(playerHead, playerNeck, player, 1.68f);
        Vector3 victim = GetBonePosition(enemyHead, enemyChest, enemy, 1.68f);
        Vector3 hips = GetBonePosition(playerHips, null, player, .93f);
        Vector3 leonTorso = Vector3.Lerp(hips, leon, .28f);
        Vector3 victimTorso = GetBonePosition(enemyChest, enemyHead, enemy, 1.37f);
        Vector3 leonTop = leon + Vector3.up * .14f;
        Vector3 victimTop = victim + Vector3.up * .12f;
        Vector3 victimBottom = victimTorso - Vector3.up * .08f;
        if (victim.y < victimBottom.y)
        {
            victimBottom = victim;
        }

        Vector3 upperBodyCenter = Vector3.Lerp(leon, victim, .5f) - Vector3.up * .28f;
        Vector3 aftermathCenter = (leonTorso + victimTorso) * .5f;
        Vector3 focus = Vector3.Lerp(upperBodyCenter, aftermathCenter, aftermathProgress);
        Vector3 right = Vector3.Cross(Vector3.up, assassinationForward);
        float floor = Mathf.Max(player.position.y, enemy.position.y);
        float minimumHeight = Mathf.Lerp(profile.assassinationMinimumFocusHeight, .42f, aftermathProgress);
        focus.y = Mathf.Max(focus.y, floor + minimumHeight);
        focus += right * profile.focusOffset.x + Vector3.up * profile.focusOffset.y
            + assassinationForward * profile.focusOffset.z;

        float verticalFov = Mathf.Lerp(profile.holdingFieldOfView, profile.assassinationContactFieldOfView, contactProgress);
        verticalFov = Mathf.Lerp(verticalFov, profile.assassinationAftermathFieldOfView, aftermathProgress);
        fov = Camera.VerticalToHorizontalFieldOfView(verticalFov, output.aspect);
        float radians = angle * Mathf.Deg2Rad;
        Vector3 rearOffset = -Mathf.Cos(radians) * assassinationForward;
        Vector3 sideOffset = Mathf.Sin(radians) * right;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float shotDistance = distance;
            float shotHeight = height;
            bool holdPosition = attempt == 2;
            if (holdPosition && !nativeCameraTaken)
            {
                continue;
            }
            if (attempt == 1)
            {
                // 狭窄处先在同一侧收短机位，再检查双方是否仍在画幅内。
                shotDistance *= .76f;
                shotHeight += .22f;
            }
            else if (attempt == 3)
            {
                if (assassinationSideLocked)
                {
                    break;
                }
                side = -assassinationSide;
            }

            Vector3 radial = side * sideOffset + rearOffset;
            Vector3 position = focus + shotDistance * radial + shotHeight * Vector3.up;
            if (holdPosition)
            {
                // 首选落点被墙挡住时，在上一个安全位置继续跟拍，避免倒地过程中提前切回越肩。
                position = stableQtePose.position;
            }

            // 以两人的最高点和最低点在镜头中的仰角定俯仰，目标倒地时仍留在画幅内。
            Vector3 horizontalForward = Vector3.ProjectOnPlane(focus - position, Vector3.up);
            if (horizontalForward.sqrMagnitude < .04f)
            {
                continue;
            }
            horizontalForward.Normalize();
            float upperPitch = Mathf.Max(GetAssassinationElevation(leonTop, position, horizontalForward),
                GetAssassinationElevation(victimTop, position, horizontalForward));
            float lowerPitch = Mathf.Min(GetAssassinationElevation(leonTorso, position, horizontalForward),
                GetAssassinationElevation(victimBottom, position, horizontalForward));
            Vector3 look = horizontalForward + Vector3.up * Mathf.Tan((upperPitch + lowerPitch) * .5f);
            Pose candidate = new Pose(position, Quaternion.LookRotation(look, Vector3.up));

            float requiredFov = Mathf.Max(RequiredGrabFov(candidate, leonTop, output.aspect),
                RequiredGrabFov(candidate, victimTop, output.aspect));
            requiredFov = Mathf.Max(requiredFov, RequiredGrabFov(candidate, leonTorso, output.aspect));
            requiredFov = Mathf.Max(requiredFov, RequiredGrabFov(candidate, victimBottom, output.aspect));
            float maximumFov = 88f;
            if (holdPosition && aftermathProgress > 0f)
            {
                // 狭窄处无法后移时，允许用稍广的画幅同时保留站起与倒地的角色。
                maximumFov = 112f;
            }
            if (requiredFov > Mathf.Max(maximumFov, fov))
            {
                continue;
            }

            float candidateFov = Mathf.Max(fov, requiredFov);
            float candidateVerticalFov = Camera.HorizontalToVerticalFieldOfView(candidateFov, output.aspect);
            float cameraRadius = CameraRouteClearance.ReadNearPlaneRadius(AssassinationNearClipPlane, candidateVerticalFov, output.aspect);
            if (HasQteCameraOverlap(position, cameraRadius) || !HasGrabSight(position, leon))
            {
                continue;
            }
            if (!holdPosition && !IsQteCameraMovementClear(focus, position, cameraRadius))
            {
                continue;
            }
            if (nativeCameraTaken && !IsQteCameraMovementClear(stableQtePose.position, position, cameraRadius))
            {
                continue;
            }
            bool victimVisible = HasGrabSight(position, victim) || HasGrabSight(position, victimTorso + Vector3.up * .10f);
            if (!victimVisible)
            {
                continue;
            }

            pose = candidate;
            fov = candidateFov;
            return true;
        }
        return false;
    }

    private static float GetAssassinationElevation(Vector3 anchor, Vector3 cameraPosition, Vector3 horizontalForward)
    {
        Vector3 offset = anchor - cameraPosition;
        float depth = Mathf.Max(.01f, Vector3.Dot(offset, horizontalForward));
        return Mathf.Atan2(offset.y, depth);
    }
}
