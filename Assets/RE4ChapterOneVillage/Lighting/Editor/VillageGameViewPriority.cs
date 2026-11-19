using UnityEditor;
using UnityEngine;

/// <summary>在新村庄实机测试期间，将后台 Scene 视图的自动刷新限制在约 12 Hz。</summary>
[InitializeOnLoad]
internal static class VillageGameViewPriority
{
    private const string MenuPath = "工具/新村庄/优先 Game 视图帧率";
    private const double RefreshInterval = 1.0 / 12.0;
    private static readonly string PreferenceKey = "RE4.VillageGameViewPriority." + Application.dataPath;
    private static readonly System.Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

    private static bool enabled = EditorPrefs.GetBool(PreferenceKey, true);
    private static SceneView backgroundView;
    private static bool originalAutoRepaint;
    private static bool originalAlwaysRefresh;
    private static double nextRefresh;

    static VillageGameViewPriority()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += _ => Restore();
        EditorApplication.pauseStateChanged += _ => Restore();
        EditorWindow.windowFocusChanged += Restore;
        SceneView.beforeSceneGui += DeferNextRefreshAfterRepaint;
        AssemblyReloadEvents.beforeAssemblyReload += Restore;
        EditorApplication.quitting += Restore;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        enabled = !enabled;
        EditorPrefs.SetBool(PreferenceKey, enabled);
        Menu.SetChecked(MenuPath, enabled);
        if (!enabled)
        {
            Restore();
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, enabled);
        return true;
    }

    private static void Update()
    {
        EditorWindow focus = EditorWindow.focusedWindow;
        SceneView view = SceneView.lastActiveSceneView;
        if (!enabled || !EditorApplication.isPlaying || EditorApplication.isPaused || !focus
            || focus.GetType() != GameViewType || focus.maximized || !view
            || !VillageSceneViewShadowBudget.TryGetVillageScene(out _))
        {
            Restore();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (view != backgroundView)
        {
            Restore();
            if (!view.autoRepaintOnSceneChange && !view.sceneViewState.alwaysRefresh)
            {
                return;
            }

            backgroundView = view;
            originalAutoRepaint = view.autoRepaintOnSceneChange;
            originalAlwaysRefresh = view.sceneViewState.alwaysRefresh;
            view.autoRepaintOnSceneChange = false;
            view.sceneViewState.alwaysRefresh = false;
            nextRefresh = now;
        }

        if (now >= nextRefresh)
        {
            // 每次只请求一帧。编辑器忙过预定时刻时，略过已错过的刷新。
            nextRefresh = System.Math.Max(nextRefresh + RefreshInterval, now + RefreshInterval * 0.5);
            view.Repaint();
        }
    }

    private static void DeferNextRefreshAfterRepaint(SceneView view)
    {
        if (view == backgroundView && Event.current.type == EventType.Repaint)
        {
            // Unity 也会自行刷新 Scene 视图。以实际重绘时刻重新计时，避免紧接着再请求一帧。
            nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
        }
    }

    private static void Restore()
    {
        if (backgroundView)
        {
            backgroundView.autoRepaintOnSceneChange = originalAutoRepaint;
            backgroundView.sceneViewState.alwaysRefresh = originalAlwaysRefresh;
            backgroundView.Repaint();
        }

        backgroundView = null;
    }
}
