using UnityEngine;
using UnityEngine.Splines;

public sealed partial class QTECameraPresenter
{
    private int grabScreenSide;
    private Vector3 frontGrabEntryRadial;
    private Vector3 frontGrabShotRadial;
    private int recoveryCameraSession;
    private Vector3 recoveryCameraOffset;
    private Vector3 standingResultOffset;

    private bool UsesSawFailureComposition => nativePlayerClip == "LeonPair_0030_jack_pl_2nd_wp5300";

    private bool UsesGrabComposition => sessionContext.kind == QTEKind.FrontGrab ||
        sessionContext.kind == QTEKind.RearGrab || nativePlayerClip == "LeonPair_1200" ||
        nativePlayerClip == "LeonPair_1202" || UsesSawFailureComposition;

    private void ReadGrabAnchors(out Vector3 leon, out Vector3 enemy)
    {
        leon = GetBonePosition(playerHead, playerNeck, sessionContext.playerRoot, 1.66f);
        enemy = GetBonePosition(enemyHead, enemyChest, sessionContext.enemyRoot, 1.68f);
    }

    private bool TryFrameGrab(Pose reference, float referenceFov, out Pose pose, out float fov,
        float sourceFrame = 0f)
    {
        ReadGrabAnchors(out Vector3 leon, out Vector3 enemy);
        Vector3 focus = Vector3.Lerp(leon, enemy, 0.5f) - Vector3.up * 0.12f;
        Vector3 hips = GetBonePosition(playerHips, playerUpperChest, sessionContext.playerRoot, .75f);
        bool sawCollapse = UsesSawFailureComposition && sourceFrame >= 464f;
        if (sessionContext.kind == QTEKind.RearGrab)
        {
            focus = Vector3.Lerp(focus, leon + sessionContext.playerRoot.forward * 0.25f, 0.32f);
        }
        else if (UsesSawFailureComposition)
        {
            float collapse = Mathf.InverseLerp(450f, 510f, sourceFrame);
            Vector3 playerFocus = Vector3.Lerp(hips, leon, .66f);
            focus = Vector3.Lerp(focus, playerFocus, Mathf.Lerp(.23f, .83f, collapse));
        }

        Vector3 radial = reference.position - focus;
        // 原轨道的高度不能直接套到重定位后的双人骨架，否则会整段变成俯拍。
        Vector3 horizontal = Vector3.ProjectOnPlane(radial, Vector3.up);
        if (horizontal.sqrMagnitude < 0.25f)
        {
            horizontal = sessionContext.playerRoot.right * 1.6f;
        }
        if (UsesSawFailureComposition)
        {
            // 镜头沿现有轨道的一侧取景，减少电锯村民背部遮住里昂脸部的情况。
            Vector3 side = Vector3.ProjectOnPlane(sessionContext.playerRoot.right, Vector3.up).normalized;
            float lateral = Vector3.Dot(horizontal.normalized, side);
            if (Mathf.Abs(lateral) < .64f)
            {
                float sign = Mathf.Sign(lateral);
                if (Mathf.Abs(lateral) < .18f && grabScreenSide != 0)
                {
                    sign = Mathf.Sign(Vector3.Dot(stableQtePose.position - focus, side));
                }
                horizontal += side * (sign * (.64f - Mathf.Abs(lateral)) * horizontal.magnitude);
            }
        }
        if (sessionContext.kind == QTEKind.FrontGrab && frontGrabEntryRadial.sqrMagnitude > .01f)
        {
            // 开场从玩法镜头的近侧取景；第一次选中可用机位后，连续跟随双方而不绕到背面。
            float distance = horizontal.magnitude;
            if (frontGrabShotRadial.sqrMagnitude > .01f)
            {
                horizontal = frontGrabShotRadial * distance;
            }
            else
            {
                float angle = Vector3.SignedAngle(frontGrabEntryRadial, horizontal, Vector3.up);
                horizontal = Quaternion.AngleAxis(Mathf.Clamp(angle, -78f, 78f), Vector3.up) * frontGrabEntryRadial * distance;
            }
        }
        float horizontalDistance = Mathf.Clamp(horizontal.magnitude, 1.4f, 3.2f);
        radial = horizontal.normalized * horizontalDistance
            + Vector3.up * Mathf.Clamp(radial.y, -0.15f, horizontalDistance * 0.28f);
        reference.position = focus + radial;
        float aspect = cameraController.MainCamera.aspect;
        float nominalFov = Mathf.Clamp(referenceFov, 57f, 77f);
        pose = reference;
        fov = nominalFov;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector3 position = reference.position;
            if (attempt == 1)
            {
                position = focus + radial * 0.81f + Vector3.up * 0.18f;
            }
            else if (attempt == 2)
            {
                position = focus + radial * 0.68f + Vector3.up * 0.35f;
            }

            Vector3 look = focus - position;
            if (look.sqrMagnitude < 0.16f || !HasGrabSight(position, leon))
            {
                continue;
            }
            if (sawCollapse)
            {
                // 倒地阶段以里昂为取景中心。站着的对手可从画面边缘离开。
                if (!IsQteCameraMovementClear(focus, position, .13f) || !HasGrabSight(position, hips))
                {
                    continue;
                }
            }
            else if (!IsNativeCameraClear(position) || !HasGrabSight(position, enemy))
            {
                continue;
            }

            Quaternion rotation = Quaternion.LookRotation(look, Vector3.up);
            var candidate = new Pose(position, rotation);
            float requiredFov = Mathf.Max(RequiredGrabFov(candidate, leon, aspect), RequiredGrabFov(candidate, hips, aspect));
            if (!sawCollapse)
            {
                requiredFov = Mathf.Max(requiredFov, RequiredGrabFov(candidate, enemy, aspect));
                requiredFov = Mathf.Max(requiredFov, RequiredGrabFov(candidate, focus - Vector3.up * 0.28f, aspect));
            }
            if (requiredFov > 87f)
            {
                continue;
            }

            pose = candidate;
            fov = Mathf.Max(nominalFov, requiredFov);
            return true;
        }

        return false;
    }

    private bool TryFramePlayerRecovery(out Pose pose, out float horizontalFov)
    {
        Vector3 hips = GetBonePosition(playerHips, playerUpperChest, sessionContext.playerRoot, .75f);
        Vector3 focus = hips + Vector3.up * .3f;
        if (recoveryCameraSession != activeSessionId)
        {
            recoveryCameraSession = activeSessionId;
            Vector3 radial = Vector3.ProjectOnPlane(cameraController.MainCamera.transform.position - focus, Vector3.up);
            if (radial.sqrMagnitude < .1f)
            {
                radial = -sessionContext.playerRoot.forward;
            }
            recoveryCameraOffset = radial.normalized * 2.6f + Vector3.up * .45f;
        }
        Vector3 position = focus + recoveryCameraOffset;
        pose = new Pose(position, Quaternion.LookRotation(focus - position, Vector3.up));
        horizontalFov = 72f;
        return IsNativeCameraClear(position) && HasGrabSight(position, focus);
    }

    private static float RequiredGrabFov(Pose pose, Vector3 anchor, float aspect)
    {
        Vector3 local = Quaternion.Inverse(pose.rotation) * (anchor - pose.position);
        if (local.z <= 0.12f)
        {
            return float.PositiveInfinity;
        }

        float extent = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y) * aspect);
        return 2f * Mathf.Atan(extent / (local.z * 0.79f)) * Mathf.Rad2Deg;
    }

    private bool IsGrabBlendUsable(Pose pose, float horizontalFov)
    {
        if (!IsNativeCameraClear(pose.position))
        {
            return false;
        }

        ReadGrabAnchors(out Vector3 leon, out Vector3 enemy);
        float aspect = cameraController.MainCamera.aspect;
        return RequiredGrabFov(pose, leon, aspect) <= horizontalFov &&
            RequiredGrabFov(pose, enemy, aspect) <= horizontalFov &&
            HasGrabSight(pose.position, leon) && HasGrabSight(pose.position, enemy);
    }

    private bool HasGrabSight(Vector3 position, Vector3 focus)
    {
        Vector3 offset = focus - position;
        float distance = offset.magnitude;
        if (distance < 0.01f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(position, offset / distance, nativeSightHits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (count == nativeSightHits.Length)
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            if (!IsParticipantCollider(nativeSightHits[index].collider))
            {
                return false;
            }
        }

        return true;
    }

    private float ScoreGrabComposition(Pose pose, float fov, int stableSide)
    {
        Transform output = cameraController.MainCamera.transform;
        float screenX = GetLeonScreenX(pose, fov);
        int desiredSide = stableSide;
        float sideWeight = 2.4f;
        if (desiredSide == 0)
        {
            desiredSide = -1;
            if (sessionContext.kind == QTEKind.RearGrab)
            {
                desiredSide = 1;
            }
            sideWeight = 0.9f;
        }

        float continuity = Vector3.Distance(output.position, pose.position) * 0.45f;
        continuity += Quaternion.Angle(output.rotation, pose.rotation) / 140f;
        float sideCost = Mathf.Clamp01((0.055f - desiredSide * screenX) / 0.15f);
        float overlapCost = 0f;
        if (sessionContext.kind == QTEKind.RearGrab)
        {
            ReadGrabAnchors(out _, out Vector3 enemy);
            float separation = Mathf.Abs(screenX - GetGrabAnchorScreenX(pose, fov, enemy));
            overlapCost = Mathf.Clamp01((0.10f - separation) / 0.10f) * 0.8f;
        }

        return continuity + sideCost * sideWeight + overlapCost;
    }

    private float GetLeonScreenX(Pose pose, float fov)
    {
        ReadGrabAnchors(out Vector3 leon, out _);
        return GetGrabAnchorScreenX(pose, fov, leon);
    }

    private static float GetGrabAnchorScreenX(Pose pose, float fov, Vector3 anchor)
    {
        Vector3 local = Quaternion.Inverse(pose.rotation) * (anchor - pose.position);
        float halfWidth = Mathf.Max(0.1f, local.z) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        return 0.5f * local.x / halfWidth;
    }

    private void RememberGrabScreenSide(Pose pose, float fov)
    {
        float x = GetLeonScreenX(pose, fov);
        if (x < -0.04f)
        {
            grabScreenSide = -1;
        }
        else if (x > 0.04f)
        {
            grabScreenSide = 1;
        }
    }

    private bool TryFrameRearPath(SplineContainer path, float position, out Pose pose, out float fov)
    {
        QTECameraRig rig = activePreset.rig;
        Vector3 cameraPosition = path.EvaluatePosition(position);
        Vector3 splineOffset = rig.splineDolly.SplineOffset;
        cameraPosition += rig.pairRoot.TransformVector(splineOffset);
        Vector3 toward = rig.focusTarget.position - cameraPosition;
        pose = default;
        fov = 0f;
        if (toward.sqrMagnitude < 0.01f)
        {
            return false;
        }

        float referenceFov = Camera.VerticalToHorizontalFieldOfView(rig.qteCamera.Lens.FieldOfView, cameraController.MainCamera.aspect);
        Vector3 horizontal = Vector3.ProjectOnPlane(-toward, Vector3.up);
        Transform playerRoot = sessionContext.playerRoot;
        Vector3 right = Vector3.ProjectOnPlane(playerRoot.right, Vector3.up).normalized;
        float lateralRatio = Vector3.Dot(horizontal.normalized, right);
        if (horizontal.sqrMagnitude > 0.25f && Mathf.Abs(lateralRatio) < 0.78f)
        {
            float side = -1f;
            if (path == rig.fallbackPath)
            {
                side = 1f;
            }

            Vector3 foreAft = (horizontal - Vector3.Dot(horizontal, right) * right).normalized;
            Vector3 openRadial = (foreAft * 0.626f + right * side * 0.78f) * horizontal.magnitude;
            Vector3 openPosition = cameraPosition + openRadial - horizontal;
            var openReference = new Pose(openPosition, Quaternion.LookRotation(rig.focusTarget.position - openPosition, Vector3.up));
            if (TryFrameGrab(openReference, referenceFov, out pose, out fov))
            {
                return true;
            }
        }

        var reference = new Pose(cameraPosition, Quaternion.LookRotation(toward, Vector3.up));
        return TryFrameGrab(reference, referenceFov, out pose, out fov);
    }

    private float ScoreRearPath(SplineContainer path, QTECameraProfile profile, int pathScore)
    {
        float hold = profile.entrySplinePosition;
        if (profile.entryKnotIndex >= 0)
        {
            int index = Mathf.Clamp(profile.entryKnotIndex, 0, path.Spline.Count - 1);
            hold = SplineUtility.GetNormalizedInterpolation(path.Spline, index, PathIndexUnit.Knot);
        }

        if (!TryFrameRearPath(path, hold, out Pose pose, out float fov))
        {
            return 1000f + pathScore;
        }

        return ScoreGrabComposition(pose, fov, grabScreenSide) + pathScore * 0.35f;
    }

    private void UpdateRearComposition()
    {
        if (!cameraTaken || cameraController.MainCamera == null)
        {
            return;
        }

        QTECameraRig rig = activePreset.rig;
        if (coordinator.CurrentPhase is QTEPhase.SuccessResult or QTEPhase.FailureResult)
        {
            bool framed = TryFrameStandingResult(out Pose result, out float resultFov);
            nativeCameraTaken = PresentContinuousQteCamera(GetContinuousQteCamera(), framed, result, resultFov);
            return;
        }
        float along = rig.SplinePosition;
        bool valid = TryFrameRearPath(rig.SelectedPath, along, out Pose pose, out float fov);
        bool obstructedLongEnough = qteObstructedSince >= 0f && Time.time - qteObstructedSince >= .10f;
        if (!valid && (stableExternalCamera == null || obstructedLongEnough))
        {
            SplineContainer other = rig.primaryPath;
            if (rig.SelectedPath == other)
            {
                other = rig.fallbackPath;
            }
            if (other != null && TryFrameRearPath(other, along, out pose, out fov))
            {
                valid = true;
                rig.SelectPath(other);
                rig.SetSplinePosition(along);
                orbitSideSign = -1f;
                if (other == rig.fallbackPath)
                {
                    orbitSideSign = 1f;
                }
                RememberGrabScreenSide(pose, fov);
            }
        }
        else if (valid && grabScreenSide == 0)
        {
            RememberGrabScreenSide(pose, fov);
        }

        nativeCameraTaken = PresentContinuousQteCamera(GetContinuousQteCamera(), valid, pose, fov);
    }

    private bool TryFrameStandingResult(out Pose pose, out float horizontalFov)
    {
        // 摔脱和刺击收尾都会改变角色朝向。沿最后可见的观察方向平移，避免跟着角色绕行。
        var output = cameraController.MainCamera;
        pose = default;
        horizontalFov = 72f;
        if (output == null)
        {
            return false;
        }
        Vector3 center = Vector3.Lerp(sessionContext.playerRoot.position, sessionContext.enemyRoot.position, .3f);
        center.y += 1.12f;
        if (standingResultOffset.sqrMagnitude < .01f)
        {
            float distance = Mathf.Clamp(Vector3.Distance(center, output.transform.position), 1.8f, 3.2f);
            standingResultOffset = -output.transform.forward * distance;
        }
        pose = new Pose(center + standingResultOffset, Quaternion.LookRotation(-standingResultOffset, Vector3.up));
        for (int i = 0; i < 3; i++)
        {
            pose.position = center + standingResultOffset * (1f - i * .19f);
            if (IsQteCameraMovementClear(center, pose.position, .14f) && HasGrabSight(pose.position, center))
            {
                return true;
            }
        }
        return false;
    }
}
