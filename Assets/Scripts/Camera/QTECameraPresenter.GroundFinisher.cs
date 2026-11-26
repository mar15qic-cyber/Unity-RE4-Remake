using Unity.Cinemachine;
using UnityEngine;

public sealed partial class QTECameraPresenter
{
    private Transform nativeGroundSpine;
    private Vector2? groundReturnAngles;
    private float groundCameraEntryTime;
    private Quaternion nativeGroundView;
    private Vector3 nativeGroundFocus;
    private Vector3 nativeGroundFocusVelocity;
    private bool nativeGroundFacingBound;

    private void BeginGroundFinisherCamera()
    {
        groundReturnAngles = null;
        if ((sessionContext.kind == QTEKind.GroundFinisher || sessionContext.kind == QTEKind.FrontGrab)
            && cameraController.gameplayDriver != null)
        {
            groundReturnAngles = cameraController.gameplayDriver.CaptureShoulderViewAngles();
        }
        nativeGroundSpine = null;
        nativeGroundFacingBound = false;
        foreach (Transform bone in sessionContext.playerRoot.GetComponentsInChildren<Transform>())
        {
            // 原版 Spine_1 在本项目里昂重定向骨架中对应 spine middle。
            if (bone.name == "spine_1" || bone.name == "Spine_1" || bone.name == "spine middle")
            {
                nativeGroundSpine = bone;
                break;
            }
        }
        nativeGroundView = sessionContext.playerRoot.rotation;
        if (cameraController.MainCamera != null)
        {
            nativeGroundView = cameraController.MainCamera.transform.rotation;
        }
        nativeGroundFocusVelocity = Vector3.zero;
    }

    private bool TrySampleGroundFinisherCamera(out Pose pose, out float horizontalFov)
    {
        pose = default;
        horizontalFov = 64f;
        if (nativeGroundSpine == null)
        {
            return false;
        }
        if (!nativeGroundFacingBound)
        {
            // 接住玩家入镜前的观察方向。用双方真实骨点确定俯角，整次刺击固定观察轴。
            float pitch = 35f;
            if (enemyHead != null && cameraController.MainCamera != null)
            {
                Vector3 midpoint = Vector3.Lerp(nativeGroundSpine.position, enemyHead.position, .5f);
                Vector3 sight = midpoint - cameraController.MainCamera.transform.position;
                float planar = Vector3.ProjectOnPlane(sight, Vector3.up).magnitude;
                pitch = Mathf.Atan2(-sight.y, planar) * Mathf.Rad2Deg;
            }
            nativeGroundView = Quaternion.Euler(Mathf.Clamp(pitch, 30f, 60f), nativeGroundView.eulerAngles.y, 0f);
        }
        // 保留 KnifeFatalCamera 的脊柱锚点和偏移，同时把受击方头颈纳入构图。
        Vector3 anchor = nativeGroundSpine.position;
        if (enemyHead != null)
        {
            anchor = Vector3.Lerp(anchor, enemyHead.position, .4f);
        }
        Vector3 desiredFocus = anchor + nativeGroundView * new Vector3(.25f, .2f, 0f);
        if (!nativeGroundFacingBound)
        {
            nativeGroundFocus = desiredFocus;
            nativeGroundFacingBound = true;
        }
        nativeGroundFocus = Vector3.SmoothDamp(nativeGroundFocus, desiredFocus, ref nativeGroundFocusVelocity,
            .05f, Mathf.Infinity, Time.deltaTime);
        Vector3 direction = nativeGroundView * Vector3.forward;
        Vector3 position = nativeGroundFocus - direction * 1.6f;
        if (!IsNativeCameraClear(position))
        {
            position = nativeGroundFocus + nativeGroundView * Vector3.right * .1f - direction * 1.45f;
            if (!IsNativeCameraClear(position))
            {
                return false;
            }
        }
        pose = new Pose(position, nativeGroundView);
        return true;
    }
}
