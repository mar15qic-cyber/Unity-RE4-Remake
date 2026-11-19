using Unity.Cinemachine;
using UnityEngine;

public sealed partial class QTECameraPresenter
{
    private Transform nativeGroundSpine;
    private Quaternion nativeGroundView;
    private Vector3 nativeGroundFocus;
    private Vector3 nativeGroundFocusVelocity;
    private bool nativeGroundFacingBound;

    private void BeginGroundFinisherCamera()
    {
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
        // via.Range(-60, 30) 表示起点与范围，即原版俯角 -60 到 -30 度。
        // Unity 的正 X 欧拉角朝下，转换后是 30 到 60 度。
        Vector3 viewAngles = nativeGroundView.eulerAngles;
        float pitch = 35f;
        nativeGroundView = Quaternion.Euler(pitch, viewAngles.y, 0f);
        nativeGroundFocus = sessionContext.playerRoot.position + Vector3.up;
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
        if (!nativeGroundFacingBound && coordinator.TryGetNativePairCameraFrame(activeSessionId,
            out _, out _, out _, out Pose stage))
        {
            nativeGroundView = Quaternion.Euler(35f, stage.rotation.eulerAngles.y, 0f);
            nativeGroundFacingBound = true;
        }
        // KnifeKill_FaceDown 状态机引用 KnifeFatalCamera / Camera2；源 mcam 同号轨道并非该分支的实际镜头。
        // 原版配置：Spine_1、偏移 (0.25, 0.2, 0)、跟随 0.05 秒、距离 1.6 / 1.45。
        Vector3 desiredFocus = nativeGroundSpine.position + nativeGroundView * new Vector3(.25f, .2f, 0f);
        nativeGroundFocus = Vector3.SmoothDamp(nativeGroundFocus, desiredFocus, ref nativeGroundFocusVelocity,
            .05f, Mathf.Infinity, Time.deltaTime);
        Vector3 direction = nativeGroundView * Vector3.forward;
        Vector3 position = nativeGroundFocus - direction * 1.6f;
        if (!IsNativeCameraClear(position))
        {
            position = nativeGroundFocus - direction * 1.45f;
            if (!IsNativeCameraClear(position))
            {
                return false;
            }
        }
        pose = new Pose(position, nativeGroundView);
        return true;
    }
}
