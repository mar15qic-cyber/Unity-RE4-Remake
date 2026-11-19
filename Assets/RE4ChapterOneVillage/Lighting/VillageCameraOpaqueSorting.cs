using UnityEngine;
using UnityEngine.Rendering;

/// <summary>新村庄延迟相机按材质状态提交不透明物件。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class VillageCameraOpaqueSorting : MonoBehaviour
{
    private Camera configuredCamera;

    private void OnEnable()
    {
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
        {
            return;
        }

        Camera camera = GetComponent<Camera>();
        if (camera.actualRenderingPath != RenderingPath.DeferredShading || camera.opaqueSortMode != OpaqueSortMode.Default)
        {
            return;
        }

        camera.opaqueSortMode = OpaqueSortMode.NoDistanceSort;
        configuredCamera = camera;
    }

    private void OnDisable()
    {
        if (configuredCamera && configuredCamera.opaqueSortMode == OpaqueSortMode.NoDistanceSort)
        {
            configuredCamera.opaqueSortMode = OpaqueSortMode.Default;
        }

        configuredCamera = null;
    }
}
