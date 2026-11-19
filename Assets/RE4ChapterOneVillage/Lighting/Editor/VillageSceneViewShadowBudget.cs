using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>降低新村庄着色场景视图的绘制开销。</summary>
[InitializeOnLoad]
internal static class VillageSceneViewShadowBudget
{
    private const string ScenePath = "Assets/RE4ChapterOneVillage/Lighting/VillageBattleLighting.unity";
    private const int OccluderLayerMask = 1;
    // 入村道路和弯道的实测范围。广场及室内继续使用 Scene 视图原有设置。
    private static readonly Bounds EntryOcclusionBounds = new(new Vector3(41f, 2.5f, 13.5f), new Vector3(12f, 6f, 10f));
    private static VillageCameraShadowBudget.RenderScope shadowScope;
    private static Camera scopedCamera;
    private static Camera occlusionCamera;
    private static bool opaqueSortApplied;

    static VillageSceneViewShadowBudget()
    {
        Camera.onPreCull += BeginSceneViewRender;
        Camera.onPostRender += EndSceneViewRender;
        SceneView.beforeSceneGui += BeginSceneViewOcclusion;
        SceneView.duringSceneGui += EndSceneViewOcclusion;
        EditorApplication.update += RestoreRenderingState;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreRenderingState;
    }

    internal static bool TryGetVillageScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        return SceneManager.sceneCount == 1 && scene.path == ScenePath
            && StageUtility.GetCurrentStageHandle().Equals(StageUtility.GetMainStageHandle());
    }

    private static bool IsLitPerspectiveView(SceneView view, Camera camera)
    {
        return view && camera && view.camera == camera && camera.cameraType == CameraType.SceneView
            && view.sceneLighting && !view.orthographic && !view.isUsingSceneFiltering
            && view.cameraMode.drawMode == DrawCameraMode.Textured && camera.actualRenderingPath == RenderingPath.DeferredShading;
    }

    private static void BeginSceneViewRender(Camera camera)
    {
        if (scopedCamera || !IsLitPerspectiveView(SceneView.currentDrawingSceneView, camera) || !TryGetVillageScene(out _))
        {
            return;
        }

        bool shadowApplied = shadowScope.TryApply();
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 && camera.opaqueSortMode == OpaqueSortMode.Default)
        {
            camera.opaqueSortMode = OpaqueSortMode.NoDistanceSort;
            opaqueSortApplied = true;
        }

        if (shadowApplied || opaqueSortApplied)
        {
            scopedCamera = camera;
        }
    }

    private static void EndSceneViewRender(Camera camera)
    {
        if (camera == scopedCamera)
        {
            RestoreShadowAndSorting();
        }
    }

    private static void BeginSceneViewOcclusion(SceneView view)
    {
        if (occlusionCamera || Event.current.type != EventType.Repaint || !TryGetVillageScene(out Scene scene))
        {
            return;
        }

        Camera camera = view.camera;
        if (!IsLitPerspectiveView(view, camera) || camera.useOcclusionCulling
            || !EntryOcclusionBounds.Contains(camera.transform.position) || (Tools.visibleLayers & OccluderLayerMask) == 0)
        {
            return;
        }

        SceneVisibilityManager visibility = SceneVisibilityManager.instance;
        if (visibility.IsCurrentStageIsolated() || visibility.AreAnyDescendantsHidden(scene))
        {
            return;
        }

        // 在编辑器开始绘制相机前设置，原有相机偏好由 Unity 保管。
        occlusionCamera = camera;
        camera.useOcclusionCulling = true;
    }

    private static void EndSceneViewOcclusion(SceneView view)
    {
        if (view.camera == occlusionCamera)
        {
            RestoreOcclusion();
        }
    }

    private static void RestoreRenderingState()
    {
        RestoreShadowAndSorting();
        RestoreOcclusion();
    }

    private static void RestoreOcclusion()
    {
        if (occlusionCamera)
        {
            occlusionCamera.useOcclusionCulling = false;
        }

        occlusionCamera = null;
    }

    private static void RestoreShadowAndSorting()
    {
        if (opaqueSortApplied && scopedCamera && scopedCamera.opaqueSortMode == OpaqueSortMode.NoDistanceSort)
        {
            scopedCamera.opaqueSortMode = OpaqueSortMode.Default;
        }

        opaqueSortApplied = false;
        shadowScope.Restore();
        scopedCamera = null;
    }
}
