using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 正面抓取快速反应事件资源构建器。
/// 只生成专用副本、扩展控制器、接线村民预制件并创建独立测试场景。
/// </summary>
public static class LeonQTESetupUtility
{
    /// <summary>里昂玩家控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string LeonControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_New_RewriteTest.controller";
    /// <summary>敌人控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string EnemyControllerPath = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>村民预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string VillagerPrefabPath = "Assets/Prefab/Villager.prefab";
    /// <summary>输入资源路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string InputAssetPath = "Assets/Scripts/Input/LeonInputActions.inputactions";
    /// <summary>配置路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ConfigPath = "Assets/Settings/QTE/里昂正面抓取配置.asset";
    /// <summary>提示预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string PromptPrefabPath = "Assets/Prefab/通用/QTE/QTEPrompt.prefab";
    /// <summary>验证场景路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string TestScenePath = "Assets/Scenes/QTE/LeonFrontGrabQTE_Test.unity";
    /// <summary>来源根节点。编译期常量，保持既有值与消费者映射。</summary>
    private const string SourceRoot = "Assets/素材库/动画/Zombie_Animations/Animations/";
    /// <summary>里昂动画片段根节点。编译期常量，保持既有值与消费者映射。</summary>
    private const string LeonClipRoot = "Assets/Animations/QTE/Leon/";
    /// <summary>敌人动画片段根节点。编译期常量，保持既有值与消费者映射。</summary>
    private const string EnemyClipRoot = "Assets/Animations/QTE/Enemy/";
    /// <summary>编辑器菜单调用入口 BuildAll；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.ImportAsset(string, ImportAssetOptions)、AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、LeonQTESetupUtility.BuildConfig()、LeonQTESetupUtility.BuildDedicatedClips()、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/构建正面抓取快速反应事件")]
    public static void BuildAll()
    {
        EnsureFolders();
        // 动画片段集合。
Dictionary<string, AnimationClip> clips = BuildDedicatedClips();
        BuildLeonController(clips);
        BuildEnemyController(clips);
        SetupVillagerPrefab();
        // 当前里昂配对动作配置。
LeonQTEConfig config = BuildConfig();
        // 提示预制件。
GameObject promptPrefab = BuildPromptPrefab();
        BuildTestScene(config, promptPrefab);
        AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateControllerMatrices();
        Debug.Log("[里昂快速反应事件] 专用素材、双方控制器、提示预制件和独立测试场景已构建。");
    }

    /// <summary>EnsureFolders 的业务处理入口；本方法直接依赖 LeonQTESetupUtility.EnsureFolder(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） LeonQTESetupUtility.EnsureFolder(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Animations", "QTE");
        EnsureFolder("Assets/Animations/QTE", "Leon");
        EnsureFolder("Assets/Animations/QTE", "Enemy");
        EnsureFolder("Assets", "Settings");
        EnsureFolder("Assets/Settings", "QTE");
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefab/通用", "QTE");
        EnsureFolder("Assets", "Scenes");
        EnsureFolder("Assets/Scenes", "QTE");
    }

    /// <summary>EnsureFolder 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.EnsureFolders()；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="child">子级。</param>
    private static void EnsureFolder(string parent, string child)
    {
        // 路径。
string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    /// <summary>BuildDedicatedClips 的业务处理入口；本方法直接依赖 Dictionary&lt;string, AnimationClip&gt;.Dictionary()、LeonQTESetupUtility.CloneClip(string, string, bool, bool)、LeonQTESetupUtility.ValidatePairedClip(AnimationClip, AnimationClip, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） Dictionary&lt;string, AnimationClip&gt;.Dictionary()、LeonQTESetupUtility.CloneClip(string, string, bool, bool)、LeonQTESetupUtility.ValidatePairedClip(AnimationClip, AnimationClip, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static Dictionary<string, AnimationClip> BuildDedicatedClips()
    {
        // 动画片段集合。
var clips = new Dictionary<string, AnimationClip>
        {
            ["里昂进入"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_Enter_Vic.fbx", LeonClipRoot + "里昂_正面抓取_进入.anim", false, false),
            ["里昂保持"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_Loop_Vic.fbx", LeonClipRoot + "里昂_正面抓取_挣扎.anim", true, false),
            ["里昂成功"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_PushOff_Vic.fbx", LeonClipRoot + "里昂_正面抓取_推开.anim", false, false),
            ["里昂失败"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_Vic.fbx", LeonClipRoot + "里昂_正面抓取_咬颈.anim", false, false),
            ["敌人进入"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_Enter_Att.fbx", EnemyClipRoot + "村民_正面抓取_进入.anim", false, false),
            ["敌人保持"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_Loop_Att.fbx", EnemyClipRoot + "村民_正面抓取_挣扎.anim", true, false),
            ["敌人成功"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_PushOff_Att.fbx", EnemyClipRoot + "村民_正面抓取_被推开.anim", false, false),
            ["敌人失败"] = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_Att.fbx", EnemyClipRoot + "村民_正面抓取_咬颈.anim", false, true)
        };
        ValidatePairedClip(clips["里昂进入"], clips["敌人进入"], "进入");
        ValidatePairedClip(clips["里昂保持"], clips["敌人保持"], "挣扎");
        ValidatePairedClip(clips["里昂成功"], clips["敌人成功"], "推开");
        ValidatePairedClip(clips["里昂失败"], clips["敌人失败"], "咬颈");
        return clips;
    }

    /// <summary>CloneClip 的业务处理入口；本方法直接依赖 AnimationEvent.AnimationEvent()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、Array.Empty&lt;AnimationEvent&gt;()、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAllAssetsAtPath(string) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildDedicatedClips()；下游（含内部回调体） AnimationEvent.AnimationEvent()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、Array.Empty&lt;AnimationEvent&gt;()、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAllAssetsAtPath(string) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourcePath">来源路径。</param>
    /// <param name="destinationPath">目的地路径。</param>
    /// <param name="loop">是否循环。</param>
    /// <param name="addBiteContactEvent">是否添加撕咬接触事件。</param>
    private static AnimationClip CloneClip(string sourcePath, string destinationPath, bool loop, bool addBiteContactEvent)
    {
        // 来源动画片段。
AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (source == null)
        {
            throw new InvalidOperationException("找不到动画片段: " + sourcePath);
        }

        // 目的地。
AnimationClip destination = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinationPath);
        if (destination == null)
        {
            destination = Object.Instantiate(source);
            destination.name = System.IO.Path.GetFileNameWithoutExtension(destinationPath);
            destination.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(destination, destinationPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, destination);
            destination.name = System.IO.Path.GetFileNameWithoutExtension(destinationPath);
        }

        // 设置。
AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(destination);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(destination, settings);
        if (addBiteContactEvent)
        {
            // 撕咬接触。
AnimationEvent biteContact = new AnimationEvent
            {
                functionName = "OnQTEBiteContact",
                time = destination.length * 0.42f
            };
            AnimationUtility.SetAnimationEvents(destination, new[] { biteContact });
        }
        else
        {
            AnimationUtility.SetAnimationEvents(destination, Array.Empty<AnimationEvent>());
        }

        EditorUtility.SetDirty(destination);
        return destination;
    }

    /// <summary>ValidatePairedClip 的业务处理入口；本方法直接依赖 InvalidOperationException.InvalidOperationException(string)、Mathf.Abs(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildDedicatedClips()；下游（含内部回调体） InvalidOperationException.InvalidOperationException(string)、Mathf.Abs(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="victim">受击者。</param>
    /// <param name="attacker">攻击者。</param>
    /// <param name="phaseName">阶段名称。</param>
    private static void ValidatePairedClip(AnimationClip victim, AnimationClip attacker, string phaseName)
    {
        if (victim == null || attacker == null)
        {
            throw new InvalidOperationException(phaseName + "配对片段缺失。");
        }

        if (Mathf.Abs(victim.length - attacker.length) > 1f / 60f || Mathf.Abs(victim.frameRate - attacker.frameRate) > 0.01f)
        {
            throw new InvalidOperationException(phaseName + "配对片段的时长或帧率不一致。");
        }
    }

    /// <summary>BuildLeonController 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、InvalidOperationException.InvalidOperationException(string)、LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int)、LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)、LeonQTESetupUtility.FindState(AnimatorStateMachine, string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、InvalidOperationException.InvalidOperationException(string)、LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int)、LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)、LeonQTESetupUtility.FindState(AnimatorStateMachine, string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clips">动画片段集合。</param>
    private static void BuildLeonController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LeonControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到里昂正式控制器。");
        }

        if (controller.layers.Length != 5 || controller.parameters.Length != 17)
        {
            throw new InvalidOperationException("里昂控制器不满足五层、十七参数门禁。");
        }

        // 状态机。
AnimatorStateMachine machine = controller.layers[4].stateMachine;
        RemoveQTEStates(machine);
        // 空。
AnimatorState empty = FindState(machine, "Empty");
        if (empty == null)
        {
            throw new InvalidOperationException("第4层缺少空状态。");
        }

        // 进入。
AnimatorState enter = AddLeonQTEState(machine, "QTEEnter", clips["里昂进入"], FullBodyActionType.QTEEnter, 0.9f, new Vector3(330f, 0f));
        // 保持。
AnimatorState hold = AddLeonQTEState(machine, "QTEHold", clips["里昂保持"], FullBodyActionType.QTEHold, 0.08f, new Vector3(540f, 0f));
        // 原地成功。
AnimationClip inPlaceSuccess = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/里昂_正面抓取_推开_原地驱动.anim");
        // 原地失败。
AnimationClip inPlaceFail = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/里昂_正面抓取_咬颈_原地驱动.anim");
        // 按原顺序缓存的实参状态机。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue2 = machine;
        // 按原顺序缓存的实参文字“QTESuccess”。
string argumentValue3 = "QTESuccess";
        // 分支选出的原地成功。
global::UnityEngine.AnimationClip selectedValue1;
        if (inPlaceSuccess != null)
        {
            selectedValue1 = inPlaceSuccess;
        }
        else
        {
            selectedValue1 = clips["里昂成功"];
        }

        // 成功。
AnimatorState success = AddLeonQTEState(argumentValue2, argumentValue3, selectedValue1, FullBodyActionType.QTESuccess, 0.9f, new Vector3(750f, -70f));
        // 按原顺序缓存的实参状态机。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue5 = machine;
        // 按原顺序缓存的实参文字“QTEFail”。
string argumentValue6 = "QTEFail";
        // 分支选出的原地失败。
global::UnityEngine.AnimationClip selectedValue4;
        if (inPlaceFail != null)
        {
            selectedValue4 = inPlaceFail;
        }
        else
        {
            selectedValue4 = clips["里昂失败"];
        }

        // 失败。
AnimatorState fail = AddLeonQTEState(argumentValue5, argumentValue6, selectedValue4, FullBodyActionType.QTEFail, 0.9f, new Vector3(750f, 70f));
        AddIntegerTransition(empty, enter, "FullBodyAction", 3);
        AddIntegerTransition(enter, hold, "FullBodyAction", 4);
        AddIntegerTransition(enter, empty, "FullBodyAction", 0);
        AddIntegerTransition(hold, success, "FullBodyAction", 5);
        AddIntegerTransition(success, empty, "FullBodyAction", 0);
        AddIntegerTransition(hold, fail, "FullBodyAction", 6);
        AddIntegerTransition(fail, empty, "FullBodyAction", 0);
        AddIntegerTransition(hold, empty, "FullBodyAction", 0);
        EditorUtility.SetDirty(controller);
    }

    /// <summary>AddLeonQTEState 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、AnimatorStateMachine.AddState(string, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、AnimatorStateMachine.AddState(string, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    /// <param name="stateName">动画状态名称。</param>
    /// <param name="motion">运动。</param>
    /// <param name="action">动作。</param>
    /// <param name="completionTime">完成时间。</param>
    /// <param name="position">位置。</param>
    private static AnimatorState AddLeonQTEState(AnimatorStateMachine machine, string stateName, Motion motion, FullBodyActionType action, float completionTime, Vector3 position)
    {
        // 当前动画状态。
AnimatorState state = machine.AddState(stateName, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        // 行为。
FullBodyActionSMB behaviour = state.AddStateMachineBehaviour<FullBodyActionSMB>();
        behaviour.actionType = action;
        behaviour.completeNormalizedTime = completionTime;
        return state;
    }

    /// <summary>BuildEnemyController 的业务处理入口；本方法直接依赖 AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.AddParameter(string, AnimatorControllerParameterType)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorStateMachine.AnimatorStateMachine()、AssetDatabase.AddObjectToAsset(Object, Object) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.AddParameter(string, AnimatorControllerParameterType)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorStateMachine.AnimatorStateMachine()、AssetDatabase.AddObjectToAsset(Object, Object) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clips">动画片段集合。</param>
    private static void BuildEnemyController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到村民动画控制器。");
        }

        if (!controller.parameters.Any( /* 中文含义：参数。 */parameter => parameter.name == "QTEAction" && parameter.type == AnimatorControllerParameterType.Int))
        {
            controller.AddParameter("QTEAction", AnimatorControllerParameterType.Int);
        }

        // 当前遍历索引。
for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            if (controller.layers[i].name == "QTE Override")
            {
                controller.RemoveLayer(i);
            }
        }

        // 状态机。
AnimatorStateMachine machine = new AnimatorStateMachine
        {
            name = "QTE Override",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(machine, controller);
        // 图层。
AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "QTE Override",
            defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = machine
        };
        controller.AddLayer(layer);
        // 空。
AnimatorState empty = machine.AddState("Empty", new Vector3(100f, 0f));
        empty.writeDefaultValues = false;
        machine.defaultState = empty;
        // 进入。
AnimatorState enter = AddEnemyQTEState(machine, "QTEEnter", clips["敌人进入"], EnemyQTEAction.Enter, 0.9f, new Vector3(330f, 0f));
        // 保持。
AnimatorState hold = AddEnemyQTEState(machine, "QTEHold", clips["敌人保持"], EnemyQTEAction.Hold, 0.08f, new Vector3(540f, 0f));
        // 原地成功。
AnimationClip inPlaceSuccess = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/村民_正面抓取_被推开_原地驱动.anim");
        // 原地失败。
AnimationClip inPlaceFail = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/村民_正面抓取_咬颈_原地驱动.anim");
        // 按原顺序缓存的实参状态机。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue8 = machine;
        // 按原顺序缓存的实参文字“QTESuccess”。
string argumentValue9 = "QTESuccess";
        // 分支选出的原地成功。
global::UnityEngine.AnimationClip selectedValue7;
        if (inPlaceSuccess != null)
        {
            selectedValue7 = inPlaceSuccess;
        }
        else
        {
            selectedValue7 = clips["敌人成功"];
        }

        // 成功。
AnimatorState success = AddEnemyQTEState(argumentValue8, argumentValue9, selectedValue7, EnemyQTEAction.Success, 0.9f, new Vector3(750f, -70f));
        // 按原顺序缓存的实参状态机。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue11 = machine;
        // 按原顺序缓存的实参文字“QTEFail”。
string argumentValue12 = "QTEFail";
        // 分支选出的原地失败。
global::UnityEngine.AnimationClip selectedValue10;
        if (inPlaceFail != null)
        {
            selectedValue10 = inPlaceFail;
        }
        else
        {
            selectedValue10 = clips["敌人失败"];
        }

        // 失败。
AnimatorState fail = AddEnemyQTEState(argumentValue11, argumentValue12, selectedValue10, EnemyQTEAction.Failure, 0.9f, new Vector3(750f, 70f));
        AddIntegerTransition(empty, enter, "QTEAction", 1);
        AddIntegerTransition(enter, hold, "QTEAction", 2);
        AddIntegerTransition(enter, empty, "QTEAction", 0);
        AddIntegerTransition(hold, success, "QTEAction", 3);
        AddIntegerTransition(success, empty, "QTEAction", 0);
        AddIntegerTransition(hold, fail, "QTEAction", 4);
        AddIntegerTransition(fail, empty, "QTEAction", 0);
        AddIntegerTransition(hold, empty, "QTEAction", 0);
        EditorUtility.SetDirty(controller);
    }

    /// <summary>AddEnemyQTEState 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;EnemyQTEStateSMB&gt;()、AnimatorStateMachine.AddState(string, Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;EnemyQTEStateSMB&gt;()、AnimatorStateMachine.AddState(string, Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    /// <param name="stateName">动画状态名称。</param>
    /// <param name="motion">运动。</param>
    /// <param name="action">动作。</param>
    /// <param name="completionTime">完成时间。</param>
    /// <param name="position">位置。</param>
    private static AnimatorState AddEnemyQTEState(AnimatorStateMachine machine, string stateName, Motion motion, EnemyQTEAction action, float completionTime, Vector3 position)
    {
        // 当前动画状态。
AnimatorState state = machine.AddState(stateName, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        // 行为。
EnemyQTEStateSMB behaviour = state.AddStateMachineBehaviour<EnemyQTEStateSMB>();
        behaviour.action = action;
        behaviour.completeNormalizedTime = completionTime;
        return state;
    }

    /// <summary>AddIntegerTransition 的业务处理入口；本方法直接依赖 AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源动画状态。</param>
    /// <param name="destination">目的地。</param>
    /// <param name="parameter">参数。</param>
    /// <param name="value">值。</param>
    private static void AddIntegerTransition(AnimatorState source, AnimatorState destination, string parameter, int value)
    {
        // 过渡。
AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
    }

    /// <summary>RemoveQTEStates 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveState(AnimatorState)、HashSet&lt;string&gt;.Contains(string)、HashSet&lt;string&gt;.HashSet()、IEnumerable&lt;ChildAnimatorState&gt;.ToArray&lt;ChildAnimatorState&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorStateMachine.RemoveState(AnimatorState)、HashSet&lt;string&gt;.Contains(string)、HashSet&lt;string&gt;.HashSet()、IEnumerable&lt;ChildAnimatorState&gt;.ToArray&lt;ChildAnimatorState&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    private static void RemoveQTEStates(AnimatorStateMachine machine)
    {
        // 配对动作名称列表。
var qteNames = new HashSet<string>
        {
            "QTEEnter",
            "QTEHold",
            "QTESuccess",
            "QTEFail"
        };
        // child（子级）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveState(AnimatorState) 的参数、作为 HashSet<string>.Contains(string) 的参数。
foreach (ChildAnimatorState child in machine.states.ToArray())
        {
            if (qteNames.Contains(child.state.name))
            {
                machine.RemoveState(child.state);
            }
        }
    }

    /// <summary>FindState 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">状态机。</param>
    /// <param name="stateName">动画状态名称。</param>
    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == stateName);
    }

    /// <summary>SetupVillagerPrefab 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void SetupVillagerPrefab()
    {
        // 根游戏对象。
GameObject root = PrefabUtility.LoadPrefabContents(VillagerPrefabPath);
        try
        {
            // 参与者。
VillagerQTEParticipant participant = root.GetComponent<VillagerQTEParticipant>();
            if (participant == null)
            {
                participant = root.AddComponent<VillagerQTEParticipant>();
            }

            // 转发器。
EnemyQTEAnimationEventRelay relay = root.GetComponent<EnemyQTEAnimationEventRelay>();
            if (relay == null)
            {
                relay = root.AddComponent<EnemyQTEAnimationEventRelay>();
            }

            participant.controller = root.GetComponent<VillagerController>();
            participant.animator = root.GetComponent<Animator>();
            participant.pathAgent = root.GetComponent<PathAgent>();
            participant.behaviorTree = root.GetComponent<BehaviorTree>();
            participant.body = root.GetComponent<Rigidbody>();
            participant.participantColliders = root.GetComponentsInChildren<Collider>(true);
            relay.participant = participant;
            PrefabUtility.SaveAsPrefabAsset(root, VillagerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>BuildConfig 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;LeonQTEConfig&gt;(string)、EditorUtility.SetDirty(Object)、ScriptableObject.CreateInstance&lt;LeonQTEConfig&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;LeonQTEConfig&gt;(string)、EditorUtility.SetDirty(Object)、ScriptableObject.CreateInstance&lt;LeonQTEConfig&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static LeonQTEConfig BuildConfig()
    {
        // 当前里昂配对动作配置。
LeonQTEConfig config = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LeonQTEConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        EditorUtility.SetDirty(config);
        return config;
    }

    /// <summary>BuildPromptPrefab 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;Image&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Canvas&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasScaler&gt;() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;Image&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Canvas&gt;()、GameObject.GetComponent&lt;CanvasGroup&gt;()、GameObject.GetComponent&lt;CanvasScaler&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static GameObject BuildPromptPrefab()
    {
        // 根游戏对象。
GameObject root = new GameObject("快速反应提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(QTEPromptView));
        try
        {
            // 画布。
Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            // 缩放器。
CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // 面板。
GameObject panel = CreateUIObject("提示面板", root.transform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0f, 0f), new Vector2(430f, 150f));
            // 面板图像。
Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            // 键文字。
Text keyText = CreateText("按键", panel.transform, "F", 52, new Vector2(-150f, 25f), new Vector2(80f, 80f));
            keyText.color = new Color(0.95f, 0.75f, 0.12f, 1f);
            // 时间文字。
Text timeText = CreateText("剩余时间", panel.transform, "3.0", 28, new Vector2(150f, 28f), new Vector2(90f, 50f));
            // 时间条。
GameObject timeBar = CreateUIObject("时间条", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(35f, 5f), new Vector2(250f, 14f));
            // 时间填充。
Image timeFill = timeBar.AddComponent<Image>();
            timeFill.type = Image.Type.Filled;
            timeFill.fillMethod = Image.FillMethod.Horizontal;
            timeFill.fillOrigin = 0;
            timeFill.color = new Color(0.85f, 0.18f, 0.12f, 1f);
            // 线段集合。
Image[] segments = new Image[8];
            // 当前遍历索引。
for (int i = 0; i < segments.Length; i++)
            {
                // 线段。
GameObject segment = CreateUIObject("进度段" + (i + 1), panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-87.5f + i * 25f, -38f), new Vector2(18f, 32f));
                segments[i] = segment.AddComponent<Image>();
                segments[i].color = new Color(1f, 1f, 1f, 0.2f);
            }

            // 视图。
QTEPromptView view = root.GetComponent<QTEPromptView>();
            view.rootGroup = root.GetComponent<CanvasGroup>();
            view.keyLabel = keyText;
            view.timeLabel = timeText;
            view.timeFill = timeFill;
            view.progressSegments = segments;
            PrefabUtility.SaveAsPrefabAsset(root, PromptPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PromptPrefabPath);
    }

    /// <summary>CreateUIObject 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildPromptPrefab()、LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">对象名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="anchorMin">锚点最小。</param>
    /// <param name="anchorMax">锚点最大。</param>
    /// <param name="anchoredPosition">锚定位置。</param>
    /// <param name="size">尺寸。</param>
    private static GameObject CreateUIObject(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        // 子级。
GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        // 矩形。
RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return child;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;Text&gt;()、LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildPromptPrefab()；下游（含内部回调体） GameObject.AddComponent&lt;Text&gt;()、LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">对象名称。</param>
    /// <param name="parent">父级。</param>
    /// <param name="content">内容。</param>
    /// <param name="fontSize">字体尺寸。</param>
    /// <param name="position">位置。</param>
    /// <param name="size">尺寸。</param>
    private static Text CreateText(string objectName, Transform parent, string content, int fontSize, Vector2 position, Vector2 size)
    {
        // 文字对象。
GameObject textObject = CreateUIObject(objectName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        // 文字。
Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return text;
    }

    /// <summary>BuildTestScene 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Object&gt;(string)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.NewScene(NewSceneSetup, NewSceneMode)、EditorSceneManager.SaveScene(Scene, string)、EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;LeonQTECoordinator&gt;() 等共 41 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Object&gt;(string)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.NewScene(NewSceneSetup, NewSceneMode)、EditorSceneManager.SaveScene(Scene, string)、EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;LeonQTECoordinator&gt;() 等共 41 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="config">当前里昂配对动作配置。</param>
    /// <param name="promptPrefab">提示预制件。</param>
    private static void BuildTestScene(LeonQTEConfig config, GameObject promptPrefab)
    {
        // 前一次生效。
Scene previousActive = SceneManager.GetActiveScene();
        // 来源里昂。
LeonController sourceLeon = Object.FindObjectsByType<LeonController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault( /* 中文含义：里昂。 */leon => leon.gameObject.scene.path == "Assets/GameScene.unity");
        if (sourceLeon == null)
        {
            throw new InvalidOperationException("正式场景中找不到里昂，无法建立隔离测试副本。");
        }

        // 场景。
Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            // 玩家。
GameObject player = Object.Instantiate(sourceLeon.gameObject);
            player.name = "里昂_快速反应测试";
            // 玩家控制器。
LeonController playerController = player.GetComponent<LeonController>();
            playerController.playerCamera = null;
            playerController.aimTarget = null;
            SceneManager.MoveGameObjectToScene(player, scene);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // 旧版摄像机。
var legacyCamera = player.GetComponent<RE4RThirdPersonCamera>();
            if (legacyCamera != null)
            {
                legacyCamera.enabled = false;
            }

            // 摄像机对象。
GameObject cameraObject = new GameObject("主摄像机", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(2.6f, 2.1f, -4.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.2f, 0.6f));
            // 摄像机。
Camera camera = cameraObject.GetComponent<Camera>();
            // 瞄准目标对象。
GameObject aimTargetObject = new GameObject("本地瞄准目标");
            SceneManager.MoveGameObjectToScene(aimTargetObject, scene);
            aimTargetObject.transform.position = new Vector3(0f, 1.4f, 8f);
            // 光源对象。
GameObject lightObject = new GameObject("主方向光", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            // 光源。
Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            // 地面。
GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "测试地面";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            // 村民预制件。
GameObject villagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerPrefabPath);
            // 敌人。
GameObject enemy = PrefabUtility.InstantiatePrefab(villagerPrefab, scene) as GameObject;
            if (enemy == null)
            {
                throw new InvalidOperationException("村民预制件实例化失败。");
            }

            enemy.name = "村民_快速反应测试";
            enemy.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.Euler(0f, 180f, 0f));
            // 参与者。
VillagerQTEParticipant participant = enemy.GetComponent<VillagerQTEParticipant>();
            participant.keepAIDisabledForIsolatedTest = true;
            EditorUtility.SetDirty(participant);
            PrefabUtility.RecordPrefabInstancePropertyModifications(participant);
            // 当前里昂配对动作协调器。
LeonQTECoordinator coordinator = player.GetComponent<LeonQTECoordinator>();
            if (coordinator == null)
            {
                coordinator = player.AddComponent<LeonQTECoordinator>();
            }

            coordinator.config = config;
            coordinator.controller = playerController;
            coordinator.animationCoordinator = player.GetComponent<LeonAnimationActionCoordinator>();
            coordinator.inputBridge = player.GetComponent<LeonInputBridge>();
            coordinator.animRig = player.GetComponent<LeonAnimRig>();
            coordinator.playerBody = player.GetComponent<Rigidbody>();
            coordinator.playerAnimator = player.GetComponent<Animator>();
            coordinator.playerColliders = player.GetComponentsInChildren<Collider>(true);
            playerController.playerCamera = camera;
            playerController.aimTarget = aimTargetObject.transform;
            // 输入桥接。
LeonInputBridge inputBridge = player.GetComponent<LeonInputBridge>();
            if (inputBridge != null)
            {
                // 序列化的桥接。
SerializedObject serializedBridge = new SerializedObject(inputBridge);
                // 输入属性。
SerializedProperty inputProperty = serializedBridge.FindProperty("inputActionAsset");
                inputProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(InputAssetPath);
                serializedBridge.ApplyModifiedPropertiesWithoutUndo();
            }

            // 提示。
GameObject prompt = PrefabUtility.InstantiatePrefab(promptPrefab, scene) as GameObject;
            if (prompt == null)
            {
                throw new InvalidOperationException("提示预制件实例化失败。");
            }

            prompt.GetComponent<QTEPromptView>().coordinator = coordinator;
            // 触发器对象。
GameObject triggerObject = new GameObject("编辑器测试触发器", typeof(QTEEditorTestTrigger));
            SceneManager.MoveGameObjectToScene(triggerObject, scene);
            // 触发器。
QTEEditorTestTrigger trigger = triggerObject.GetComponent<QTEEditorTestTrigger>();
            trigger.coordinator = coordinator;
            trigger.candidates = new[]
            {
                participant
            };
            EditorSceneManager.SaveScene(scene, TestScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    /// <summary>ValidateControllerMatrices 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、IEnumerable&lt;AnimatorControllerLayer&gt;.FirstOrDefault&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, bool&gt;)、IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonQTESetupUtility.CountBehaviours(AnimatorController)、LeonQTESetupUtility.CountStates(AnimatorController) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、IEnumerable&lt;AnimatorControllerLayer&gt;.FirstOrDefault&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, bool&gt;)、IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)、InvalidOperationException.InvalidOperationException(string)、LeonQTESetupUtility.CountBehaviours(AnimatorController)、LeonQTESetupUtility.CountStates(AnimatorController) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ValidateControllerMatrices()
    {
        // 里昂。
AnimatorController leon = AssetDatabase.LoadAssetAtPath<AnimatorController>(LeonControllerPath);
        // 里昂状态集合。
int leonStates = CountStates(leon);
        // 里昂过渡集合。
int leonTransitions = CountTransitions(leon);
        // 里昂行为集合。
int leonBehaviours = CountBehaviours(leon);
        if (leon.layers.Length != 5 || leon.parameters.Length != 16 || leonStates != 22 || leonTransitions != 35 || leonBehaviours != 8)
        {
            throw new InvalidOperationException("里昂控制器矩阵不匹配: 层=" + leon.layers.Length + " 参数=" + leon.parameters.Length + " 状态=" + leonStates + " 转换=" + leonTransitions + " 行为=" + leonBehaviours);
        }

        // 任意状态过渡集合。
int anyStateTransitions = leon.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.anyStateTransitions.Length);
        if (anyStateTransitions != 1)
        {
            throw new InvalidOperationException("里昂控制器任意状态转换数量不是一。");
        }

        // 敌人。
AnimatorController enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
        // 配对动作图层。
AnimatorControllerLayer qteLayer = enemy.layers.FirstOrDefault( /* 中文含义：图层。 */layer => layer.name == "QTE Override");
        if (qteLayer == null || qteLayer.stateMachine.states.Length != 5 || qteLayer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.transitions.Length) != 8)
        {
            throw new InvalidOperationException("村民快速反应覆盖层矩阵不匹配。");
        }
    }

    /// <summary>CountStates 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">动画控制器资源。</param>
    private static int CountStates(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.states.Length);
    }

    /// <summary>CountTransitions 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">动画控制器资源。</param>
    private static int CountTransitions(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.anyStateTransitions.Length + layer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.transitions.Length));
    }

    /// <summary>CountBehaviours 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">动画控制器资源。</param>
    private static int CountBehaviours(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.behaviours.Length));
    }
}
