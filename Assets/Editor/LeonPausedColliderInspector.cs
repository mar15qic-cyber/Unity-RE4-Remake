using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>编辑器配置与校验模块 LeonPausedColliderInspector；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[InitializeOnLoad]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂暂停碰撞体查看器")]
internal static class LeonPausedColliderInspector
{
    /// <summary>场景视图原始辅助辅助线状态。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly Dictionary<int, bool> sceneViewOriginalAssistGizmoState = new Dictionary<int, bool>();
    /// <summary>当前胶囊。</summary>
    private static CapsuleCollider currentCapsule;
    /// <summary>是否具有接管场景视图。</summary>
    private static bool hasTakeOverSceneView;
    /// <summary>构造并初始化 LeonPausedColliderInspector 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPausedColliderInspector.RefreshTakeOverState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static LeonPausedColliderInspector()
    {
        Selection.selectionChanged += RefreshTakeOverState;
        EditorApplication.pauseStateChanged += /* 中文含义：当前流程中不使用的传入结果。 */ _ => RefreshTakeOverState();
        EditorApplication.playModeStateChanged += HandleRuntimeModeChange;
        EditorApplication.update += RefreshTakeOverState;
        SceneView.duringSceneGui += DrawSafeCapsuleWireframe;
        EditorApplication.delayCall += CloseGameWindowAssistGizmo;
    }

    /// <summary>HandleRuntimeModeChange 的业务处理入口；本方法直接依赖 LeonPausedColliderInspector.CloseGameWindowAssistGizmo()、LeonPausedColliderInspector.RefreshTakeOverState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPausedColliderInspector.CloseGameWindowAssistGizmo()、LeonPausedColliderInspector.RefreshTakeOverState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前播放模式状态变化。</param>
    private static void HandleRuntimeModeChange(PlayModeStateChange state)
    {
        RefreshTakeOverState();
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            CloseGameWindowAssistGizmo();
        }
    }

    /// <summary>CloseGameWindowAssistGizmo 的业务处理入口；本方法直接依赖 EditorWindow.Repaint()、PropertyInfo.SetValue(object, object)、Resources.FindObjectsOfTypeAll(Type)、Type.GetProperty(string, BindingFlags)、Type.GetType(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.HandleRuntimeModeChange(PlayModeStateChange)；下游（含内部回调体） EditorWindow.Repaint()、PropertyInfo.SetValue(object, object)、Resources.FindObjectsOfTypeAll(Type)、Type.GetProperty(string, BindingFlags)、Type.GetType(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void CloseGameWindowAssistGizmo()
    {
        // 游戏窗口类型。
Type gameWindowType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameWindowType == null)
        {
            return;
        }

        // 辅助辅助线属性。
PropertyInfo assistGizmoProperty = gameWindowType.GetProperty("drawGizmos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (assistGizmoProperty == null)
        {
            return;
        }

        // 游戏窗口列表。
UnityEngine.Object[] gameWindowList = Resources.FindObjectsOfTypeAll(gameWindowType);
        // gameWindowObject（游戏窗口对象）：每轮从游戏窗口列表取得一个元素，仅代表当前这一项。使用位置：LeonPausedColliderInspector.CloseGameWindowAssistGizmo()。
foreach (UnityEngine.Object gameWindowObject in gameWindowList)
        {
            if (gameWindowObject is not EditorWindow /* 中文含义：游戏窗口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gameWindow)
            {
                continue;
            }

            assistGizmoProperty.SetValue(gameWindow, false);
            gameWindow.Repaint();
        }
    }

    /// <summary>RefreshTakeOverState 的业务处理入口；本方法直接依赖 LeonPausedColliderInspector.RestoreSceneViewAssistGizmo()、LeonPausedColliderInspector.TakeOverSceneViewAssistGizmo()、LeonPausedColliderInspector.TryGetSelectedLeonCapsule(out CapsuleCollider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.HandleRuntimeModeChange(PlayModeStateChange)、LeonPausedColliderInspector.LeonPausedColliderInspector()；下游（含内部回调体） LeonPausedColliderInspector.RestoreSceneViewAssistGizmo()、LeonPausedColliderInspector.TakeOverSceneViewAssistGizmo()、LeonPausedColliderInspector.TryGetSelectedLeonCapsule(out CapsuleCollider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void RefreshTakeOverState()
    {
        // 胶囊。
CapsuleCollider capsule = null;
        // 是否应当接管。
bool shouldTakeOver = EditorApplication.isPlaying && EditorApplication.isPaused && TryGetSelectedLeonCapsule(out capsule);
        if (shouldTakeOver)
        {
            currentCapsule = capsule;
        }
        else
        {
            currentCapsule = null;
        }

        if (shouldTakeOver)
        {
            TakeOverSceneViewAssistGizmo();
            return;
        }

        RestoreSceneViewAssistGizmo();
    }

    /// <summary>TryGetSelectedLeonCapsule 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponentInParent&lt;LeonController&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.RefreshTakeOverState()；下游（含内部回调体） Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponentInParent&lt;LeonController&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="capsule">胶囊。输出参数，由本入口写入并交给调用方。</param>
    private static bool TryGetSelectedLeonCapsule(out CapsuleCollider capsule)
    {
        capsule = null;
        // 选定对象。
Transform selectedObject = Selection.activeTransform;
        if (selectedObject == null)
        {
            return false;
        }

        // 里昂。
LeonController leon = selectedObject.GetComponentInParent<LeonController>();
        if (leon == null)
        {
            return false;
        }

        capsule = leon.GetComponent<CapsuleCollider>();
        return capsule != null && capsule.enabled;
    }

    /// <summary>TakeOverSceneViewAssistGizmo 的业务处理入口；本方法直接依赖 Dictionary&lt;int, bool&gt;.Add(int, bool)、Dictionary&lt;int, bool&gt;.ContainsKey(int)、Object.GetInstanceID()、SceneView.RepaintAll()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.RefreshTakeOverState()；下游（含内部回调体） Dictionary&lt;int, bool&gt;.Add(int, bool)、Dictionary&lt;int, bool&gt;.ContainsKey(int)、Object.GetInstanceID()、SceneView.RepaintAll()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void TakeOverSceneViewAssistGizmo()
    {
        // 状态是否具有变化。
bool stateHasChange = false;
        // sceneView（场景视图）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<int, bool>.Add(int, bool) 的参数、LeonPausedColliderInspector.TakeOverSceneViewAssistGizmo()。
foreach (SceneView sceneView in SceneView.sceneViews)
        {
            if (sceneView == null)
            {
                continue;
            }

            // 标识符。
int identifier = sceneView.GetInstanceID();
            if (!sceneViewOriginalAssistGizmoState.ContainsKey(identifier))
            {
                sceneViewOriginalAssistGizmoState.Add(identifier, sceneView.drawGizmos);
            }

            if (!sceneView.drawGizmos)
            {
                continue;
            }

            sceneView.drawGizmos = false;
            stateHasChange = true;
        }

        if (!hasTakeOverSceneView || stateHasChange)
        {
            SceneView.RepaintAll();
        }

        hasTakeOverSceneView = true;
    }

    /// <summary>RestoreSceneViewAssistGizmo 的业务处理入口；本方法直接依赖 Dictionary&lt;int, bool&gt;.Clear()、Dictionary&lt;int, bool&gt;.TryGetValue(int, out bool)、Object.GetInstanceID()、SceneView.RepaintAll()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.RefreshTakeOverState()；下游（含内部回调体） Dictionary&lt;int, bool&gt;.Clear()、Dictionary&lt;int, bool&gt;.TryGetValue(int, out bool)、Object.GetInstanceID()、SceneView.RepaintAll()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void RestoreSceneViewAssistGizmo()
    {
        if (!hasTakeOverSceneView && sceneViewOriginalAssistGizmoState.Count == 0)
        {
            return;
        }

        // sceneView（场景视图）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：LeonPausedColliderInspector.RestoreSceneViewAssistGizmo()。
foreach (SceneView sceneView in SceneView.sceneViews)
        {
            if (sceneView == null)
            {
                continue;
            }

            // 标识符。
int identifier = sceneView.GetInstanceID();
            if (sceneViewOriginalAssistGizmoState.TryGetValue(identifier, out bool /* 中文含义：是否原始状态。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ originalState))
            {
                sceneView.drawGizmos = originalState;
            }
        }

        sceneViewOriginalAssistGizmoState.Clear();
        hasTakeOverSceneView = false;
        SceneView.RepaintAll();
    }

    /// <summary>DrawSafeCapsuleWireframe 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Handles.DrawLine(Vector3, Vector3)、Handles.DrawWireArc(Vector3, Vector3, Vector3, float, float)、Handles.DrawWireDisc(Vector3, Vector3, float)、LeonPausedColliderInspector.TryCalculateWorldCapsule(CapsuleCollider, out Vector3, out Vector3, out float, out float)、Vector3.Cross(Vector3, Vector3)、Vector3.Normalize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、Handles.DrawLine(Vector3, Vector3)、Handles.DrawWireArc(Vector3, Vector3, Vector3, float, float)、Handles.DrawWireDisc(Vector3, Vector3, float)、LeonPausedColliderInspector.TryCalculateWorldCapsule(CapsuleCollider, out Vector3, out Vector3, out float, out float)、Vector3.Cross(Vector3, Vector3)、Vector3.Normalize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sceneView">中文含义：场景视图。</param>
    private static void DrawSafeCapsuleWireframe(SceneView sceneView)
    {
        if (!hasTakeOverSceneView || currentCapsule == null)
        {
            return;
        }

        if (!TryCalculateWorldCapsule(currentCapsule, out Vector3 /* 中文含义：中心。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ center, out Vector3 /* 中文含义：轴向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ axial, out float /* 中文含义：半径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ radius, out float /* 中文含义：圆柱一半长度。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ cylinderHalfLength))
        {
            return;
        }

        // 切向第一。
Vector3 tangentialOne = Vector3.Cross(axial, Vector3.forward);
        if (tangentialOne.sqrMagnitude < 0.0001f)
        {
            tangentialOne = Vector3.Cross(axial, Vector3.right);
        }

        tangentialOne.Normalize();
        // 切向第二。
Vector3 tangentialTwo = Vector3.Cross(axial, tangentialOne).normalized;
        // 上部圆形中心。
Vector3 upperCircleCenter = center + axial * cylinderHalfLength;
        // 下部圆形中心。
Vector3 lowerCircleCenter = center - axial * cylinderHalfLength;
        // 原始颜色。
Color originalColor = Handles.color;
        Handles.color = new Color(0.15f, 1f, 0.35f, 1f);
        Handles.DrawWireDisc(upperCircleCenter, axial, radius);
        Handles.DrawWireDisc(lowerCircleCenter, axial, radius);
        Handles.DrawLine(upperCircleCenter + tangentialOne * radius, lowerCircleCenter + tangentialOne * radius);
        Handles.DrawLine(upperCircleCenter - tangentialOne * radius, lowerCircleCenter - tangentialOne * radius);
        Handles.DrawLine(upperCircleCenter + tangentialTwo * radius, lowerCircleCenter + tangentialTwo * radius);
        Handles.DrawLine(upperCircleCenter - tangentialTwo * radius, lowerCircleCenter - tangentialTwo * radius);
        Handles.DrawWireArc(upperCircleCenter, tangentialTwo, tangentialOne, 180f, radius);
        Handles.DrawWireArc(upperCircleCenter, tangentialOne, -tangentialTwo, 180f, radius);
        Handles.DrawWireArc(lowerCircleCenter, tangentialTwo, -tangentialOne, 180f, radius);
        Handles.DrawWireArc(lowerCircleCenter, tangentialOne, tangentialTwo, 180f, radius);
        Handles.color = originalColor;
    }

    /// <summary>TryCalculateWorldCapsule 的业务处理入口；本方法直接依赖 LeonPausedColliderInspector.IsFiniteCount(float)、LeonPausedColliderInspector.IsFiniteVector(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformDirection(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Normalize() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.DrawSafeCapsuleWireframe(SceneView)；下游（含内部回调体） LeonPausedColliderInspector.IsFiniteCount(float)、LeonPausedColliderInspector.IsFiniteVector(Vector3)、Mathf.Abs(float)、Mathf.Max(float, float)、Transform.TransformDirection(Vector3)、Transform.TransformPoint(Vector3)、Vector3.Normalize() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="capsule">胶囊。</param>
    /// <param name="centerValue">中心值。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="axial">轴向。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="radiusValue">半径值。输出参数，由本入口写入并交给调用方。</param>
    /// <param name="cylinderHalfLength">圆柱一半长度。输出参数，由本入口写入并交给调用方。</param>
    private static bool TryCalculateWorldCapsule(CapsuleCollider capsule, out Vector3 centerValue, out Vector3 axial, out float radiusValue, out float cylinderHalfLength)
    {
        // 变换值。
Transform transformValue = capsule.transform;
        // 局部轴向。
Vector3 localAxial = capsule.direction switch
        {
            0 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up
        };
        // 绝对缩放。
Vector3 absoluteScale = new Vector3(Mathf.Abs(transformValue.lossyScale.x), Mathf.Abs(transformValue.lossyScale.y), Mathf.Abs(transformValue.lossyScale.z));
        // 轴向缩放。
float axialScale;
        // 半径缩放。
float radiusScale;
        switch (capsule.direction)
        {
            case 0:
                axialScale = absoluteScale.x;
                radiusScale = Mathf.Max(absoluteScale.y, absoluteScale.z);
                break;
            case 2:
                axialScale = absoluteScale.z;
                radiusScale = Mathf.Max(absoluteScale.x, absoluteScale.y);
                break;
            default:
                axialScale = absoluteScale.y;
                radiusScale = Mathf.Max(absoluteScale.x, absoluteScale.z);
                break;
        }

        centerValue = transformValue.TransformPoint(capsule.center);
        axial = transformValue.TransformDirection(localAxial);
        radiusValue = capsule.radius * radiusScale;
        // 世界空间一半高度。
float worldHalfHeight = Mathf.Max(capsule.height * axialScale * 0.5f, radiusValue);
        cylinderHalfLength = worldHalfHeight - radiusValue;
        if (!IsFiniteVector(centerValue) || !IsFiniteVector(axial) || axial.sqrMagnitude < 0.0001f || !IsFiniteCount(radiusValue) || !IsFiniteCount(cylinderHalfLength) || radiusValue <= 0f || cylinderHalfLength < 0f)
        {
            return false;
        }

        axial.Normalize();
        return true;
    }

    /// <summary>IsFiniteVector 的业务处理入口；本方法直接依赖 LeonPausedColliderInspector.IsFiniteCount(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.TryCalculateWorldCapsule(CapsuleCollider, out Vector3, out Vector3, out float, out float)；下游（含内部回调体） LeonPausedColliderInspector.IsFiniteCount(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    private static bool IsFiniteVector(Vector3 value)
    {
        return IsFiniteCount(value.x) && IsFiniteCount(value.y) && IsFiniteCount(value.z);
    }

    /// <summary>IsFiniteCount 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPausedColliderInspector.IsFiniteVector(Vector3)、LeonPausedColliderInspector.TryCalculateWorldCapsule(CapsuleCollider, out Vector3, out Vector3, out float, out float)；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    private static bool IsFiniteCount(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
