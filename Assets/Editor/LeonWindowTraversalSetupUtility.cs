using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 窗口翻越资源配置工具。
/// 只在编辑器中修改动画导入设置、正式动画控制器、里昂预制件和当前场景测试窗口。
/// </summary>
public static class LeonWindowTraversalSetupUtility
{
    /// <summary>控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_New_RewriteTest.controller";
    /// <summary>动画片段路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ClipPath = "Assets/素材库/动画/Universal_Traversal_Anims/Art/Animations/In_Place_Versions/AnimSeq_Traversal_Window_ClimbThrough.FBX";
    /// <summary>动画片段名称。编译期常量，保持既有值与消费者映射。</summary>
    private const string ClipName = "AnimSeq_Traversal_Window_ClimbThrough";
    /// <summary>里昂预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string LeonPrefabPath = "Assets/Prefab/LeonR4R.prefab";
    /// <summary>窗口预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string WindowPrefabPath = "Assets/Prefab/可翻越窗口测试.prefab";
    /// <summary>窗口对象名称。编译期常量，保持既有值与消费者映射。</summary>
    private const string WindowObjectName = "可翻越窗口测试";
    /// <summary>全身参数。编译期常量，保持既有值与消费者映射。</summary>
    private const string FullBodyParameter = "FullBodyAction";
    /// <summary>空动画状态名称。编译期常量，保持既有值与消费者映射。</summary>
    private const string EmptyStateName = "Empty";
    /// <summary>通行动画状态名称。编译期常量，保持既有值与消费者映射。</summary>
    private const string TraversalStateName = "WindowClimbThrough";
    /// <summary>编辑器菜单调用入口 Build；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、EditorSceneManager.SaveScene(Scene)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)、LeonWindowTraversalSetupUtility.ConfigureClipImport() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/配置窗口翻越")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请先退出运行模式，再配置窗口翻越。");
        }

        ConfigureClipImport();
        // 动画片段。
AnimationClip clip = LoadTraversalClip();
        ConfigureAnimatorController(clip);
        ConfigureLeonPrefab();
        // 场景里昂。
LeonController sceneLeon = ConfigureSceneLeon();
        ConfigureTestWindow(sceneLeon);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();
        Debug.Log("[里昂窗口翻越] 动画、状态机、里昂组件和双向测试窗口已配置完成。");
    }

    /// <summary>ConfigureClipImport 的业务处理入口；本方法直接依赖 AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ConfigureClipImport()
    {
        // 导入器。
ModelImporter importer = AssetImporter.GetAtPath(ClipPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("找不到窗口翻越动画资源：" + ClipPath);
        }

        // 动画片段集合。
ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            throw new InvalidOperationException("窗口翻越动画中没有可用片段。");
        }

        // 是否发生变化。
bool changed = false;
        // 当前遍历索引。
for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].loopTime)
            {
                clips[i].loopTime = false;
                changed = true;
            }

            if (clips[i].loopPose)
            {
                clips[i].loopPose = false;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    /// <summary>LoadTraversalClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static AnimationClip LoadTraversalClip()
    {
        // 动画片段。
AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：当前动画片段。 */item => item.name == ClipName);
        if (clip == null)
        {
            throw new InvalidOperationException("找不到窗口翻越动画片段：" + ClipName);
        }

        return clip;
    }

    /// <summary>ConfigureAnimatorController 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddState(string, Vector3)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, AnimatorConditionMode, int, float)、LeonWindowTraversalSetupUtility.ConfigureTraversalBehaviour(AnimatorState) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） AnimatorStateMachine.AddState(string, Vector3)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, AnimatorConditionMode, int, float)、LeonWindowTraversalSetupUtility.ConfigureTraversalBehaviour(AnimatorState) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">动画片段。</param>
    private static void ConfigureAnimatorController(AnimationClip clip)
    {
        // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到里昂正式动画控制器。");
        }

        if (controller.layers.Length <= AnimatorLayerID.FullBodyAction)
        {
            throw new InvalidOperationException("里昂动画控制器缺少全身动作层。");
        }

        if (!controller.parameters.Any( /* 中文含义：参数。 */parameter => parameter.name == FullBodyParameter && parameter.type == AnimatorControllerParameterType.Int))
        {
            throw new InvalidOperationException("里昂动画控制器缺少全身动作整数参数。");
        }

        // 状态机。
AnimatorStateMachine machine = controller.layers[AnimatorLayerID.FullBodyAction].stateMachine;
        // 空。
AnimatorState empty = FindState(machine, EmptyStateName);
        if (empty == null)
        {
            throw new InvalidOperationException("全身动作层缺少空状态。");
        }

        // 通行。
AnimatorState traversal = FindState(machine, TraversalStateName);
        if (traversal == null)
        {
            traversal = machine.AddState(TraversalStateName, new Vector3(720f, 420f));
        }

        traversal.motion = clip;
        traversal.speed = 1f;
        traversal.writeDefaultValues = false;
        ConfigureTraversalBehaviour(traversal);
        RemoveTransitionsToState(machine, traversal);
        RemoveStateTransitions(traversal);
        AddIntegerTransition(empty, traversal, AnimatorConditionMode.Equals, (int)FullBodyActionType.WindowClimbThrough, 0.05f);
        AddIntegerTransition(traversal, empty, AnimatorConditionMode.Equals, (int)FullBodyActionType.None, 0.08f);
        EditorUtility.SetDirty(machine);
        EditorUtility.SetDirty(controller);
    }

    /// <summary>ConfigureTraversalBehaviour 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、Object.DestroyImmediate(Object, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、Object.DestroyImmediate(Object, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前动画状态。</param>
    private static void ConfigureTraversalBehaviour(AnimatorState state)
    {
        // behaviour（行为）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object, bool) 的参数、LeonWindowTraversalSetupUtility.ConfigureTraversalBehaviour(AnimatorState)。
foreach (StateMachineBehaviour behaviour in state.behaviours.ToArray())
        {
            if (behaviour is FullBodyActionSMB)
            {
                UnityEngine.Object.DestroyImmediate(behaviour, true);
            }
        }

        // 动作行为。
FullBodyActionSMB actionBehaviour = state.AddStateMachineBehaviour<FullBodyActionSMB>();
        actionBehaviour.actionType = FullBodyActionType.WindowClimbThrough;
        actionBehaviour.completeNormalizedTime = 0.93f;
    }

    /// <summary>ConfigureLeonPrefab 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、GameObject.GetComponentInChildren&lt;LeonController&gt;(bool)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)、PrefabUtility.UnloadPrefabContents(GameObject)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） Debug.LogWarning(object)、GameObject.GetComponentInChildren&lt;LeonController&gt;(bool)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)、PrefabUtility.UnloadPrefabContents(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ConfigureLeonPrefab()
    {
        // 预制件根节点。
GameObject prefabRoot = PrefabUtility.LoadPrefabContents(LeonPrefabPath);
        if (prefabRoot == null)
        {
            throw new InvalidOperationException("找不到里昂预制件。");
        }

        try
        {
            // 预制件控制器。
LeonController prefabController = prefabRoot.GetComponentInChildren<LeonController>(true);
            if (prefabController == null)
            {
                Debug.LogWarning("[里昂窗口翻越] 里昂预制件是纯模型资源，跳过组件挂载，改由场景玩家实例承载功能。");
                return;
            }

            ConfigureLeon(prefabController.gameObject);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, LeonPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>ConfigureSceneLeon 的业务处理入口；本方法直接依赖 EditorSceneManager.MarkSceneDirty(Scene)、IEnumerable&lt;LeonController&gt;.FirstOrDefault&lt;LeonController&gt;(Func&lt;LeonController, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)、Object.FindObjectsByType&lt;LeonController&gt;(FindObjectsInactive, FindObjectsSortMode)、SceneManager.GetActiveScene()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） EditorSceneManager.MarkSceneDirty(Scene)、IEnumerable&lt;LeonController&gt;.FirstOrDefault&lt;LeonController&gt;(Func&lt;LeonController, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonWindowTraversalSetupUtility.ConfigureLeon(GameObject)、Object.FindObjectsByType&lt;LeonController&gt;(FindObjectsInactive, FindObjectsSortMode)、SceneManager.GetActiveScene()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static LeonController ConfigureSceneLeon()
    {
        // 控制器集合。
LeonController[] controllers = UnityEngine.Object.FindObjectsByType<LeonController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // 里昂。
LeonController leon = controllers.FirstOrDefault( /* 中文含义：当前里昂玩家控制器。 */item => item != null && item.gameObject.scene == SceneManager.GetActiveScene());
        if (leon == null)
        {
            throw new InvalidOperationException("当前场景中找不到里昂。");
        }

        ConfigureLeon(leon.gameObject);
        EditorSceneManager.MarkSceneDirty(leon.gameObject.scene);
        return leon;
    }

    /// <summary>ConfigureLeon 的业务处理入口；本方法直接依赖 EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;LeonWindowTraversal&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;CapsuleCollider&gt;()、GameObject.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、GameObject.GetComponent&lt;LeonCombat&gt;()、GameObject.GetComponent&lt;LeonController&gt;() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureLeonPrefab()、LeonWindowTraversalSetupUtility.ConfigureSceneLeon()；下游（含内部回调体） EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;LeonWindowTraversal&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;CapsuleCollider&gt;()、GameObject.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、GameObject.GetComponent&lt;LeonCombat&gt;()、GameObject.GetComponent&lt;LeonController&gt;() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    private static void ConfigureLeon(GameObject root)
    {
        // 里昂玩家控制器。
LeonController controller = root.GetComponent<LeonController>();
        if (controller == null)
        {
            throw new InvalidOperationException("里昂根对象缺少玩家控制器。");
        }

        // 通行。
LeonWindowTraversal traversal = root.GetComponent<LeonWindowTraversal>();
        if (traversal == null)
        {
            traversal = root.AddComponent<LeonWindowTraversal>();
        }

        traversal.controller = controller;
        traversal.combat = root.GetComponent<LeonCombat>();
        traversal.coordinator = root.GetComponent<LeonAnimationActionCoordinator>();
        traversal.weaponVisualController = root.GetComponent<LeonWeaponVisualController>();
        traversal.animator = root.GetComponent<Animator>();
        traversal.body = root.GetComponent<Rigidbody>();
        traversal.bodyCollider = root.GetComponent<CapsuleCollider>();
        if (traversal.combat == null || traversal.coordinator == null || traversal.weaponVisualController == null || traversal.animator == null || traversal.body == null || traversal.bodyCollider == null)
        {
            throw new InvalidOperationException("里昂窗口翻越所需组件不完整。");
        }

        EditorUtility.SetDirty(traversal);
        EditorUtility.SetDirty(root);
    }

    /// <summary>ConfigureTestWindow 的业务处理入口；本方法直接依赖 EditorSceneManager.MarkSceneDirty(Scene)、EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.AddComponent&lt;WindowTraversalPoint&gt;()、GameObject.GameObject(string)、LeonWindowTraversalSetupUtility.CreateFramePart(Transform, string, Vector3, Vector3)、LeonWindowTraversalSetupUtility.FindGroundNear(Vector3, int, float) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.Build()；下游（含内部回调体） EditorSceneManager.MarkSceneDirty(Scene)、EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;BoxCollider&gt;()、GameObject.AddComponent&lt;WindowTraversalPoint&gt;()、GameObject.GameObject(string)、LeonWindowTraversalSetupUtility.CreateFramePart(Transform, string, Vector3, Vector3)、LeonWindowTraversalSetupUtility.FindGroundNear(Vector3, int, float) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="leon">里昂。</param>
    private static void ConfigureTestWindow(LeonController leon)
    {
        // 生效场景。
Scene activeScene = SceneManager.GetActiveScene();
        // 已有。
GameObject existing = FindSceneObject(activeScene, WindowObjectName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        // 前向。
Vector3 forward = Vector3.ProjectOnPlane(leon.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        // 开口宽度。
const float openingWidth = 0.95f;
        // 开口高度。
const float openingHeight = 1.15f;
        // 窗台高度。
const float sillHeight = 0.75f;
        // 墙体厚度。
const float wallThickness = 0.25f;
        // 柱宽度。
const float postWidth = 0.18f;
        // 横梁高度。
const float beamHeight = 0.16f;
        // 地面点。
Vector3 groundPoint = FindGroundNear(leon.transform.position + forward * 3f, leon.gameObject.layer, leon.transform.position.y);
        // 根游戏对象。
GameObject root = new GameObject(WindowObjectName);
        SceneManager.MoveGameObjectToScene(root, activeScene);
        root.transform.SetPositionAndRotation(groundPoint + Vector3.up * (sillHeight + openingHeight * 0.5f), Quaternion.LookRotation(-forward, Vector3.up));
        // 触发器。
BoxCollider trigger = root.AddComponent<BoxCollider>();
        // 窗口。
WindowTraversalPoint window = root.AddComponent<WindowTraversalPoint>();
        window.interactTrigger = trigger;
        window.openingWidth = openingWidth;
        window.openingHeight = openingHeight;
        window.wallThickness = wallThickness;
        window.approachOffset = 0.45f;
        window.landingOffset = 0.6f;
        window.interactRange = 1.75f;
        window.allowBothDirections = true;
        // 完整高度。
float fullHeight = sillHeight + openingHeight + beamHeight;
        // 根节点高度。
float rootHeight = sillHeight + openingHeight * 0.5f;
        // 帧碰撞体集合。
var frameColliders = new Collider[4];
        frameColliders[0] = CreateFramePart(root.transform, "左侧窗框", new Vector3(-(openingWidth * 0.5f + postWidth * 0.5f), fullHeight * 0.5f - rootHeight, 0f), new Vector3(postWidth, fullHeight, wallThickness));
        frameColliders[1] = CreateFramePart(root.transform, "右侧窗框", new Vector3(openingWidth * 0.5f + postWidth * 0.5f, fullHeight * 0.5f - rootHeight, 0f), new Vector3(postWidth, fullHeight, wallThickness));
        frameColliders[2] = CreateFramePart(root.transform, "窗台墙体", new Vector3(0f, sillHeight * 0.5f - rootHeight, 0f), new Vector3(openingWidth, sillHeight, wallThickness));
        frameColliders[3] = CreateFramePart(root.transform, "顶部窗框", new Vector3(0f, openingHeight * 0.5f + beamHeight * 0.5f, 0f), new Vector3(openingWidth + postWidth * 2f, beamHeight, wallThickness));
        window.ignoredTraversalColliders = frameColliders;
        EditorUtility.SetDirty(window);
        EditorUtility.SetDirty(root);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, WindowPrefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(activeScene);
    }

    /// <summary>CreateFramePart 的业务处理入口；本方法直接依赖 GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)；下游（含内部回调体） GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="objectName">对象名称。</param>
    /// <param name="localPosition">局部位置。</param>
    /// <param name="localScale">局部缩放。</param>
    private static Collider CreateFramePart(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale)
    {
        // 部件。
GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        return part.GetComponent<Collider>();
    }

    /// <summary>FindGroundNear 的业务处理入口；本方法直接依赖 Physics.Raycast(Vector3, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)；下游（含内部回调体） Physics.Raycast(Vector3, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontalPosition">水平位置。</param>
    /// <param name="playerLayer">玩家图层。</param>
    /// <param name="fallbackHeight">后备高度。</param>
    private static Vector3 FindGroundNear(Vector3 horizontalPosition, int playerLayer, float fallbackHeight)
    {
        // 遮罩。
int mask = ~(1 << playerLayer);
        // 原点。
Vector3 origin = horizontalPosition + Vector3.up * 6f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit, 20f, mask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return new Vector3(horizontalPosition.x, fallbackHeight, horizontalPosition.z);
    }

    /// <summary>FindSceneObject 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;Transform&gt;.FirstOrDefault&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、Scene.GetRootGameObjects()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureTestWindow(LeonController)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、IEnumerable&lt;Transform&gt;.FirstOrDefault&lt;Transform&gt;(Func&lt;Transform, bool&gt;)、Scene.GetRootGameObjects()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="scene">场景。</param>
    /// <param name="objectName">对象名称。</param>
    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        // sceneRoot（场景根节点）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：LeonWindowTraversalSetupUtility.FindSceneObject(Scene, string)。
foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            // 变换集合。
Transform[] transforms = sceneRoot.GetComponentsInChildren<Transform>(true);
            // 找到的。
Transform found = transforms.FirstOrDefault( /* 中文含义：当前变换节点。 */item => item.name == objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

    /// <summary>FindState 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)；下游（含内部回调体） IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    /// <param name="stateName">动画状态名称。</param>
    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == stateName);
    }

    /// <summary>RemoveTransitionsToState 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    /// <param name="target">目标动画状态。</param>
    private static void RemoveTransitionsToState(AnimatorStateMachine machine, AnimatorState target)
    {
        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：LeonWindowTraversalSetupUtility.RemoveTransitionsToState(AnimatorStateMachine, AnimatorState)。
foreach (ChildAnimatorState child in machine.states)
        {
            // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数、LeonWindowTraversalSetupUtility.RemoveTransitionsToState(AnimatorStateMachine, AnimatorState)。
foreach (AnimatorStateTransition transition in child.state.transitions.ToArray())
            {
                if (transition.destinationState == target)
                {
                    child.state.RemoveTransition(transition);
                }
            }
        }

        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数、LeonWindowTraversalSetupUtility.RemoveTransitionsToState(AnimatorStateMachine, AnimatorState)。
foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
        {
            if (transition.destinationState == target)
            {
                machine.RemoveAnyStateTransition(transition);
            }
        }
    }

    /// <summary>RemoveStateTransitions 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前动画状态。</param>
    private static void RemoveStateTransitions(AnimatorState state)
    {
        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数。
foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    /// <summary>AddIntegerTransition 的业务处理入口；本方法直接依赖 AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWindowTraversalSetupUtility.ConfigureAnimatorController(AnimationClip)；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画状态。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="mode">当前动画控制器条件模式。</param>
    /// <param name="value">值。</param>
    /// <param name="duration">持续时间。</param>
    private static void AddIntegerTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode mode, int value, float duration)
    {
        // 过渡。
AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(mode, value, FullBodyParameter);
    }
}
