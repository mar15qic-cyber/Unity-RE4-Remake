using UnityEngine;
using Unity.Cinemachine;

public sealed partial class VillageSiegeSequence
{
    private const double HouseShoulderShotStart = 1383d / 60d;
    private const float PlayerExitHeightOffset = .04f;

    // 旧占位镜头轨的兼容入口。
    internal void QueueShot(VillageSiegeShot kind)
    {
        ApplyNativeCamera();
    }

    private void ApplyNativeCamera()
    {
        if (currentStage == null || cameraController == null || cameraController.MainCamera == null)
        {
            return;
        }
        var motion = currentStage.cameraMotion;
        var source = motion.transform;
        if (activeEntrance == VillageEntrance.House && director.time >= HouseShoulderShotStart &&
            currentStage.playerRoot != null && cameraController.gameplayDriver != null)
        {
            Vector3 exitPosition = currentStage.playerRoot.position + Vector3.up * PlayerExitHeightOffset;
            bool resetCollisionState = !villageShoulderSampleInitialized;
            if (cameraController.gameplayDriver.TrySampleVillageShoulder(exitPosition, source.rotation, resetCollisionState, out var shoulder))
            {
                villageShoulderSampleInitialized = true;
                // 末镜从切入起就使用玩法镜头的焦距，借原有运镜抵达同一越肩机位。
                float lastSample = (float)(director.duration - 1d / 60d);
                float progress = Mathf.InverseLerp((float)HouseShoulderShotStart, lastSample, (float)director.time);
                float weight = Mathf.SmoothStep(0f, 1f, progress);
                SetShotPose(Vector3.Lerp(source.position, shoulder.GetFinalPosition(), weight),
                    Quaternion.Slerp(source.rotation, shoulder.GetFinalOrientation(), weight), shoulder.Lens.FieldOfView,
                    Mathf.Lerp(motion.nearPlane, shoulder.Lens.NearClipPlane, weight));
                return;
            }
        }
        float aspect = Mathf.Max(.1f, cameraController.MainCamera.aspect);
        float vertical = 2f * Mathf.Atan(Mathf.Tan(motion.horizontalFieldOfView * .5f) / aspect) * Mathf.Rad2Deg;
        SetShotPose(source.position, source.rotation, vertical, motion.nearPlane);
    }

    private void SetShotPose(Vector3 position, Quaternion rotation, float fieldOfView, float nearPlane)
    {
        shot.transform.SetPositionAndRotation(position, rotation);
        var lens = shot.Lens;
        lens.FieldOfView = fieldOfView;
        lens.NearClipPlane = nearPlane;
        shot.Lens = lens;
    }
}
