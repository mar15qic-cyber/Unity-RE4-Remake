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
    /// <summary>中文含义：里昂玩家控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数。</summary>
    private const string LeonControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_New_RewriteTest.controller";
    /// <summary>中文含义：敌人控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数。</summary>
    private const string EnemyControllerPath = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>中文含义：村民预制件路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string) 的参数、作为 PrefabUtility.LoadPrefabContents(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数。</summary>
    private const string VillagerPrefabPath = "Assets/Prefab/Villager.prefab";
    /// <summary>中文含义：输入资源路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.ImportAsset(string, ImportAssetOptions) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;Object&gt;(string) 的参数。</summary>
    private const string InputAssetPath = "Assets/Scripts/Input/LeonInputActions.inputactions";
    /// <summary>中文含义：配置路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;LeonQTEConfig&gt;(string) 的参数。</summary>
    private const string ConfigPath = "Assets/Settings/QTE/里昂正面抓取配置.asset";
    /// <summary>中文含义：提示预制件路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数。</summary>
    private const string PromptPrefabPath = "Assets/Prefab/通用/QTE/QTEPrompt.prefab";
    /// <summary>中文含义：验证场景路径。编译期常量，保持既有值与消费者映射。使用位置：作为 EditorSceneManager.SaveScene(Scene, string) 的参数。</summary>
    private const string TestScenePath = "Assets/Scenes/QTE/LeonFrontGrabQTE_Test.unity";
    /// <summary>中文含义：来源根节点。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonQTESetupUtility.CloneClip(string, string, bool, bool) 的参数。</summary>
    private const string SourceRoot = "Assets/素材库/动画/Zombie_Animations/Animations/";
    /// <summary>中文含义：里昂动画片段根节点。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonQTESetupUtility.CloneClip(string, string, bool, bool) 的参数。</summary>
    private const string LeonClipRoot = "Assets/Animations/QTE/Leon/";
    /// <summary>中文含义：敌人动画片段根节点。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonQTESetupUtility.CloneClip(string, string, bool, bool) 的参数。</summary>
    private const string EnemyClipRoot = "Assets/Animations/QTE/Enemy/";
    /// <summary>编辑器菜单调用入口 BuildAll；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.ImportAsset(string, ImportAssetOptions)、AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、LeonQTESetupUtility.BuildConfig()、LeonQTESetupUtility.BuildDedicatedClips()、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/构建正面抓取快速反应事件")]
    public static void BuildAll()
    {
        EnsureFolders();
        // clips（动画片段集合）：保存 LeonQTESetupUtility.BuildDedicatedClips() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>) 的参数、作为 LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>) 的参数。
Dictionary<string, AnimationClip> clips = BuildDedicatedClips();
        BuildLeonController(clips);
        BuildEnemyController(clips);
        SetupVillagerPrefab();
        // config（当前里昂配对动作配置）：保存 LeonQTESetupUtility.BuildConfig() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject) 的参数。
LeonQTEConfig config = BuildConfig();
        // promptPrefab（提示预制件）：保存 LeonQTESetupUtility.BuildPromptPrefab() 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject) 的参数。
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
    /// <param name="parent">中文含义：父级。使用位置：作为 AssetDatabase.CreateFolder(string, string) 的参数、LeonQTESetupUtility.EnsureFolder(string, string)。</param>
    /// <param name="child">中文含义：子级。使用位置：作为 AssetDatabase.CreateFolder(string, string) 的参数、LeonQTESetupUtility.EnsureFolder(string, string)。</param>
    private static void EnsureFolder(string parent, string child)
    {
        // path（路径）：使用位置：作为 AssetDatabase.IsValidFolder(string) 的参数。
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
        // clips（动画片段集合）：使用位置：作为 LeonQTESetupUtility.ValidatePairedClip(AnimationClip, AnimationClip, string) 的参数、LeonQTESetupUtility.BuildDedicatedClips()。
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
    /// <param name="sourcePath">中文含义：来源路径。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</param>
    /// <param name="destinationPath">中文含义：目的地路径。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数、作为 Path.GetFileNameWithoutExtension(string) 的参数。</param>
    /// <param name="loop">中文含义：是否循环。使用位置：LeonQTESetupUtility.CloneClip(string, string, bool, bool)。</param>
    /// <param name="addBiteContactEvent">中文含义：是否添加撕咬接触事件。使用位置：LeonQTESetupUtility.CloneClip(string, string, bool, bool)。</param>
    private static AnimationClip CloneClip(string sourcePath, string destinationPath, bool loop, bool addBiteContactEvent)
    {
        // source（来源动画片段）：保存 IEnumerable<AnimationClip>.FirstOrDefault<AnimationClip>(Func<AnimationClip, bool>) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、作为 Object.Instantiate<AnimationClip>(AnimationClip) 的参数、LeonQTESetupUtility.CloneClip(string, string, bool, bool)。
AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (source == null)
        {
            throw new InvalidOperationException("找不到动画片段: " + sourcePath);
        }

        // destination（目的地）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.GetAnimationClipSettings(AnimationClip) 的参数、作为 AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings) 的参数、作为 AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[]) 的参数 等共 7 处。
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

        // settings（设置）：保存 AnimationUtility.GetAnimationClipSettings(AnimationClip) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings) 的参数、LeonQTESetupUtility.CloneClip(string, string, bool, bool)。
AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(destination);
        settings.loopTime = loop;
        settings.loopBlend = loop;
        AnimationUtility.SetAnimationClipSettings(destination, settings);
        if (addBiteContactEvent)
        {
            // biteContact（撕咬接触）：使用位置：作为 AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[]) 的参数。
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
    /// <param name="victim">中文含义：受击者。使用位置：作为 Mathf.Abs(float) 的参数、LeonQTESetupUtility.ValidatePairedClip(AnimationClip, AnimationClip, string)。</param>
    /// <param name="attacker">中文含义：攻击者。使用位置：作为 Mathf.Abs(float) 的参数、LeonQTESetupUtility.ValidatePairedClip(AnimationClip, AnimationClip, string)。</param>
    /// <param name="phaseName">中文含义：阶段名称。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数。</param>
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
    /// <param name="clips">中文含义：动画片段集合。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)。</param>
    private static void BuildLeonController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LeonControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到里昂正式控制器。");
        }

        if (controller.layers.Length != 5 || controller.parameters.Length != 17)
        {
            throw new InvalidOperationException("里昂控制器不满足五层、十七参数门禁。");
        }

        // machine（状态机）：使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数、作为 LeonQTESetupUtility.FindState(AnimatorStateMachine, string) 的参数、作为 LeonQTESetupUtility.RemoveQTEStates(AnimatorStateMachine) 的参数 等共 4 处。
AnimatorStateMachine machine = controller.layers[4].stateMachine;
        RemoveQTEStates(machine);
        // empty（空）：保存 LeonQTESetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
AnimatorState empty = FindState(machine, "Empty");
        if (empty == null)
        {
            throw new InvalidOperationException("第4层缺少空状态。");
        }

        // enter（进入）：保存 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState enter = AddLeonQTEState(machine, "QTEEnter", clips["里昂进入"], FullBodyActionType.QTEEnter, 0.9f, new Vector3(330f, 0f));
        // hold（保持）：保存 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState hold = AddLeonQTEState(machine, "QTEHold", clips["里昂保持"], FullBodyActionType.QTEHold, 0.08f, new Vector3(540f, 0f));
        // inPlaceSuccess（原地成功）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
AnimationClip inPlaceSuccess = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/里昂_正面抓取_推开_原地驱动.anim");
        // inPlaceFail（原地失败）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
AnimationClip inPlaceFail = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/里昂_正面抓取_咬颈_原地驱动.anim");
        // argumentValue2（按原顺序缓存的实参状态机）：从状态机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue2 = machine;
        // argumentValue3（按原顺序缓存的实参文字“QTESuccess”）：从文字“QTESuccess”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数。
string argumentValue3 = "QTESuccess";
        // selectedValue1（分支选出的原地成功）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
global::UnityEngine.AnimationClip selectedValue1;
        if (inPlaceSuccess != null)
        {
            selectedValue1 = inPlaceSuccess;
        }
        else
        {
            selectedValue1 = clips["里昂成功"];
        }

        // success（成功）：保存 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState success = AddLeonQTEState(argumentValue2, argumentValue3, selectedValue1, FullBodyActionType.QTESuccess, 0.9f, new Vector3(750f, -70f));
        // argumentValue5（按原顺序缓存的实参状态机）：从状态机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue5 = machine;
        // argumentValue6（按原顺序缓存的实参文字“QTEFail”）：从文字“QTEFail”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数。
string argumentValue6 = "QTEFail";
        // selectedValue4（分支选出的原地失败）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的参数、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary<string, AnimationClip>)。
global::UnityEngine.AnimationClip selectedValue4;
        if (inPlaceFail != null)
        {
            selectedValue4 = inPlaceFail;
        }
        else
        {
            selectedValue4 = clips["里昂失败"];
        }

        // fail（失败）：保存 LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
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
    /// <param name="machine">中文含义：状态机。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    /// <param name="motion">中文含义：运动。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。</param>
    /// <param name="action">中文含义：动作。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。</param>
    /// <param name="completionTime">中文含义：完成时间。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    private static AnimatorState AddLeonQTEState(AnimatorStateMachine machine, string stateName, Motion motion, FullBodyActionType action, float completionTime, Vector3 position)
    {
        // state（当前动画状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。
AnimatorState state = machine.AddState(stateName, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        // behaviour（行为）：保存 AnimatorState.AddStateMachineBehaviour<FullBodyActionSMB>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.AddLeonQTEState(AnimatorStateMachine, string, Motion, FullBodyActionType, float, Vector3)。
FullBodyActionSMB behaviour = state.AddStateMachineBehaviour<FullBodyActionSMB>();
        behaviour.actionType = action;
        behaviour.completeNormalizedTime = completionTime;
        return state;
    }

    /// <summary>BuildEnemyController 的业务处理入口；本方法直接依赖 AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.AddParameter(string, AnimatorControllerParameterType)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorStateMachine.AnimatorStateMachine()、AssetDatabase.AddObjectToAsset(Object, Object) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.AddParameter(string, AnimatorControllerParameterType)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorStateMachine.AnimatorStateMachine()、AssetDatabase.AddObjectToAsset(Object, Object) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clips">中文含义：动画片段集合。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)。</param>
    private static void BuildEnemyController(IReadOnlyDictionary<string, AnimationClip> clips)
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到村民动画控制器。");
        }

        if (!controller.parameters.Any( /* 中文含义：参数。 */parameter => parameter.name == "QTEAction" && parameter.type == AnimatorControllerParameterType.Int))
        {
            controller.AddParameter("QTEAction", AnimatorControllerParameterType.Int);
        }

        // i（当前遍历索引）：使用位置：作为 AnimatorController.RemoveLayer(int) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            if (controller.layers[i].name == "QTE Override")
            {
                controller.RemoveLayer(i);
            }
        }

        // machine（状态机）：使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
AnimatorStateMachine machine = new AnimatorStateMachine
        {
            name = "QTE Override",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(machine, controller);
        // layer（图层）：使用位置：作为 AnimatorController.AddLayer(AnimatorControllerLayer) 的参数。
AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "QTE Override",
            defaultWeight = 0f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = machine
        };
        controller.AddLayer(layer);
        // empty（空）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
AnimatorState empty = machine.AddState("Empty", new Vector3(100f, 0f));
        empty.writeDefaultValues = false;
        machine.defaultState = empty;
        // enter（进入）：保存 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState enter = AddEnemyQTEState(machine, "QTEEnter", clips["敌人进入"], EnemyQTEAction.Enter, 0.9f, new Vector3(330f, 0f));
        // hold（保持）：保存 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState hold = AddEnemyQTEState(machine, "QTEHold", clips["敌人保持"], EnemyQTEAction.Hold, 0.08f, new Vector3(540f, 0f));
        // inPlaceSuccess（原地成功）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
AnimationClip inPlaceSuccess = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/村民_正面抓取_被推开_原地驱动.anim");
        // inPlaceFail（原地失败）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
AnimationClip inPlaceFail = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/QTE/InPlace/村民_正面抓取_咬颈_原地驱动.anim");
        // argumentValue8（按原顺序缓存的实参状态机）：从状态机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue8 = machine;
        // argumentValue9（按原顺序缓存的实参文字“QTESuccess”）：从文字“QTESuccess”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数。
string argumentValue9 = "QTESuccess";
        // selectedValue7（分支选出的原地成功）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
global::UnityEngine.AnimationClip selectedValue7;
        if (inPlaceSuccess != null)
        {
            selectedValue7 = inPlaceSuccess;
        }
        else
        {
            selectedValue7 = clips["敌人成功"];
        }

        // success（成功）：保存 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
AnimatorState success = AddEnemyQTEState(argumentValue8, argumentValue9, selectedValue7, EnemyQTEAction.Success, 0.9f, new Vector3(750f, -70f));
        // argumentValue11（按原顺序缓存的实参状态机）：从状态机读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数。
global::UnityEditor.Animations.AnimatorStateMachine argumentValue11 = machine;
        // argumentValue12（按原顺序缓存的实参文字“QTEFail”）：从文字“QTEFail”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数。
string argumentValue12 = "QTEFail";
        // selectedValue10（分支选出的原地失败）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的参数、LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary<string, AnimationClip>)。
global::UnityEngine.AnimationClip selectedValue10;
        if (inPlaceFail != null)
        {
            selectedValue10 = inPlaceFail;
        }
        else
        {
            selectedValue10 = clips["敌人失败"];
        }

        // fail（失败）：保存 LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int) 的参数。
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
    /// <param name="machine">中文含义：状态机。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    /// <param name="motion">中文含义：运动。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。</param>
    /// <param name="action">中文含义：动作。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。</param>
    /// <param name="completionTime">中文含义：完成时间。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    private static AnimatorState AddEnemyQTEState(AnimatorStateMachine machine, string stateName, Motion motion, EnemyQTEAction action, float completionTime, Vector3 position)
    {
        // state（当前动画状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。
AnimatorState state = machine.AddState(stateName, position);
        state.motion = motion;
        state.writeDefaultValues = false;
        // behaviour（行为）：保存 AnimatorState.AddStateMachineBehaviour<EnemyQTEStateSMB>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.AddEnemyQTEState(AnimatorStateMachine, string, Motion, EnemyQTEAction, float, Vector3)。
EnemyQTEStateSMB behaviour = state.AddStateMachineBehaviour<EnemyQTEStateSMB>();
        behaviour.action = action;
        behaviour.completeNormalizedTime = completionTime;
        return state;
    }

    /// <summary>AddIntegerTransition 的业务处理入口；本方法直接依赖 AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildEnemyController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)、LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画状态。使用位置：LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int)。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数。</param>
    /// <param name="parameter">中文含义：参数。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    private static void AddIntegerTransition(AnimatorState source, AnimatorState destination, string parameter, int value)
    {
        // transition（过渡）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, int)。
AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.05f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
    }

    /// <summary>RemoveQTEStates 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveState(AnimatorState)、HashSet&lt;string&gt;.Contains(string)、HashSet&lt;string&gt;.HashSet()、IEnumerable&lt;ChildAnimatorState&gt;.ToArray&lt;ChildAnimatorState&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildLeonController(IReadOnlyDictionary&lt;string, AnimationClip&gt;)；下游（含内部回调体） AnimatorStateMachine.RemoveState(AnimatorState)、HashSet&lt;string&gt;.Contains(string)、HashSet&lt;string&gt;.HashSet()、IEnumerable&lt;ChildAnimatorState&gt;.ToArray&lt;ChildAnimatorState&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：LeonQTESetupUtility.RemoveQTEStates(AnimatorStateMachine)。</param>
    private static void RemoveQTEStates(AnimatorStateMachine machine)
    {
        // qteNames（配对动作名称列表）：使用位置：LeonQTESetupUtility.RemoveQTEStates(AnimatorStateMachine)。
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
    /// <param name="machine">中文含义：状态机。使用位置：LeonQTESetupUtility.FindState(AnimatorStateMachine, string)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;) 的参数。</param>
    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == stateName);
    }

    /// <summary>SetupVillagerPrefab 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.BuildAll()；下游（含内部回调体） GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;Rigidbody&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void SetupVillagerPrefab()
    {
        // root（根游戏对象）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、LeonQTESetupUtility.SetupVillagerPrefab()。
GameObject root = PrefabUtility.LoadPrefabContents(VillagerPrefabPath);
        try
        {
            // participant（参与者）：保存 GameObject.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.SetupVillagerPrefab()。
VillagerQTEParticipant participant = root.GetComponent<VillagerQTEParticipant>();
            if (participant == null)
            {
                participant = root.AddComponent<VillagerQTEParticipant>();
            }

            // relay（转发器）：保存 GameObject.GetComponent<EnemyQTEAnimationEventRelay>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.SetupVillagerPrefab()。
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
        // config（当前里昂配对动作配置）：保存 AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、LeonQTESetupUtility.BuildConfig()。
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
        // root（根游戏对象）：使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数、作为 Object.DestroyImmediate(Object) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数 等共 4 处。
GameObject root = new GameObject("快速反应提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(QTEPromptView));
        try
        {
            // canvas（画布）：保存 GameObject.GetComponent<Canvas>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            // scaler（缩放器）：保存 GameObject.GetComponent<CanvasScaler>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // panel（面板）：保存 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：作为 LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2) 的参数、作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数、LeonQTESetupUtility.BuildPromptPrefab()。
GameObject panel = CreateUIObject("提示面板", root.transform, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.2f), new Vector2(0f, 0f), new Vector2(430f, 150f));
            // panelImage（面板图像）：保存 GameObject.AddComponent<Image>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);
            // keyText（键文字）：保存 LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Text keyText = CreateText("按键", panel.transform, "F", 52, new Vector2(-150f, 25f), new Vector2(80f, 80f));
            keyText.color = new Color(0.95f, 0.75f, 0.12f, 1f);
            // timeText（时间文字）：保存 LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Text timeText = CreateText("剩余时间", panel.transform, "3.0", 28, new Vector2(150f, 28f), new Vector2(90f, 50f));
            // timeBar（时间条）：保存 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
GameObject timeBar = CreateUIObject("时间条", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(35f, 5f), new Vector2(250f, 14f));
            // timeFill（时间填充）：保存 GameObject.AddComponent<Image>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Image timeFill = timeBar.AddComponent<Image>();
            timeFill.type = Image.Type.Filled;
            timeFill.fillMethod = Image.FillMethod.Horizontal;
            timeFill.fillOrigin = 0;
            timeFill.color = new Color(0.85f, 0.18f, 0.12f, 1f);
            // segments（线段集合）：使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
Image[] segments = new Image[8];
            // i（当前遍历索引）：使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数、作为 Vector2.Vector2(float, float) 的参数、LeonQTESetupUtility.BuildPromptPrefab()。
for (int i = 0; i < segments.Length; i++)
            {
                // segment（线段）：保存 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
GameObject segment = CreateUIObject("进度段" + (i + 1), panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-87.5f + i * 25f, -38f), new Vector2(18f, 32f));
                segments[i] = segment.AddComponent<Image>();
                segments[i].color = new Color(1f, 1f, 1f, 0.2f);
            }

            // view（视图）：保存 GameObject.GetComponent<QTEPromptView>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildPromptPrefab()。
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
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="anchorMin">中文含义：锚点最小。使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。</param>
    /// <param name="anchorMax">中文含义：锚点最大。使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。</param>
    /// <param name="anchoredPosition">中文含义：锚定位置。使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。</param>
    private static GameObject CreateUIObject(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        // child（子级）：使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。
GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2)。
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
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数。</param>
    /// <param name="parent">中文含义：父级。使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数。</param>
    /// <param name="content">中文含义：内容。使用位置：LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2)。</param>
    /// <param name="fontSize">中文含义：字体尺寸。使用位置：LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数。</param>
    /// <param name="size">中文含义：尺寸。使用位置：作为 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的参数。</param>
    private static Text CreateText(string objectName, Transform parent, string content, int fontSize, Vector2 position, Vector2 size)
    {
        // textObject（文字对象）：保存 LeonQTESetupUtility.CreateUIObject(string, Transform, Vector2, Vector2, Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2)。
GameObject textObject = CreateUIObject(objectName, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        // text（文字）：保存 GameObject.AddComponent<Text>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.CreateText(string, Transform, string, int, Vector2, Vector2)。
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
    /// <param name="config">中文含义：当前里昂配对动作配置。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。</param>
    /// <param name="promptPrefab">中文含义：提示预制件。使用位置：作为 PrefabUtility.InstantiatePrefab(Object, Scene) 的参数。</param>
    private static void BuildTestScene(LeonQTEConfig config, GameObject promptPrefab)
    {
        // previousActive（前一次生效）：保存 SceneManager.GetActiveScene() 的返回结果，供当前步骤使用。使用位置：作为 SceneManager.SetActiveScene(Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
Scene previousActive = SceneManager.GetActiveScene();
        // sourceLeon（来源里昂）：保存 IEnumerable<LeonController>.FirstOrDefault<LeonController>(Func<LeonController, bool>) 的返回结果，供当前步骤使用。使用位置：作为 Object.Instantiate<GameObject>(GameObject) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
LeonController sourceLeon = Object.FindObjectsByType<LeonController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault( /* 中文含义：里昂。 */leon => leon.gameObject.scene.path == "Assets/GameScene.unity");
        if (sourceLeon == null)
        {
            throw new InvalidOperationException("正式场景中找不到里昂，无法建立隔离测试副本。");
        }

        // scene（场景）：保存 EditorSceneManager.NewScene(NewSceneSetup, NewSceneMode) 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.CloseScene(Scene, bool) 的参数、作为 EditorSceneManager.SaveScene(Scene, string) 的参数、作为 PrefabUtility.InstantiatePrefab(Object, Scene) 的参数 等共 5 处。
Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            // player（玩家）：保存 Object.Instantiate<GameObject>(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject player = Object.Instantiate(sourceLeon.gameObject);
            player.name = "里昂_快速反应测试";
            // playerController（玩家控制器）：保存 GameObject.GetComponent<LeonController>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
LeonController playerController = player.GetComponent<LeonController>();
            playerController.playerCamera = null;
            playerController.aimTarget = null;
            SceneManager.MoveGameObjectToScene(player, scene);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // legacyCamera（旧版摄像机）：保存 GameObject.GetComponent<RE4RThirdPersonCamera>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
var legacyCamera = player.GetComponent<RE4RThirdPersonCamera>();
            if (legacyCamera != null)
            {
                legacyCamera.enabled = false;
            }

            // cameraObject（摄像机对象）：使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject cameraObject = new GameObject("主摄像机", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(2.6f, 2.1f, -4.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 1.2f, 0.6f));
            // camera（摄像机）：保存 GameObject.GetComponent<Camera>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
Camera camera = cameraObject.GetComponent<Camera>();
            // aimTargetObject（瞄准目标对象）：使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject aimTargetObject = new GameObject("本地瞄准目标");
            SceneManager.MoveGameObjectToScene(aimTargetObject, scene);
            aimTargetObject.transform.position = new Vector3(0f, 1.4f, 8f);
            // lightObject（光源对象）：使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject lightObject = new GameObject("主方向光", typeof(Light));
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            // light（光源）：保存 GameObject.GetComponent<Light>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            // ground（地面）：保存 GameObject.CreatePrimitive(PrimitiveType) 的返回结果，供当前步骤使用。使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "测试地面";
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            // villagerPrefab（村民预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.InstantiatePrefab(Object, Scene) 的参数。
GameObject villagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillagerPrefabPath);
            // enemy（敌人）：使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject enemy = PrefabUtility.InstantiatePrefab(villagerPrefab, scene) as GameObject;
            if (enemy == null)
            {
                throw new InvalidOperationException("村民预制件实例化失败。");
            }

            enemy.name = "村民_快速反应测试";
            enemy.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.25f), Quaternion.Euler(0f, 180f, 0f));
            // participant（参与者）：保存 GameObject.GetComponent<VillagerQTEParticipant>() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
VillagerQTEParticipant participant = enemy.GetComponent<VillagerQTEParticipant>();
            participant.keepAIDisabledForIsolatedTest = true;
            EditorUtility.SetDirty(participant);
            PrefabUtility.RecordPrefabInstancePropertyModifications(participant);
            // coordinator（当前里昂配对动作协调器）：保存 GameObject.GetComponent<LeonQTECoordinator>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
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
            // inputBridge（输入桥接）：保存 GameObject.GetComponent<LeonInputBridge>() 的返回结果，供当前步骤使用。使用位置：作为 SerializedObject.SerializedObject(Object) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
LeonInputBridge inputBridge = player.GetComponent<LeonInputBridge>();
            if (inputBridge != null)
            {
                // serializedBridge（序列化的桥接）：使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
SerializedObject serializedBridge = new SerializedObject(inputBridge);
                // inputProperty（输入属性）：保存 SerializedObject.FindProperty(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
SerializedProperty inputProperty = serializedBridge.FindProperty("inputActionAsset");
                inputProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(InputAssetPath);
                serializedBridge.ApplyModifiedPropertiesWithoutUndo();
            }

            // prompt（提示）：使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject prompt = PrefabUtility.InstantiatePrefab(promptPrefab, scene) as GameObject;
            if (prompt == null)
            {
                throw new InvalidOperationException("提示预制件实例化失败。");
            }

            prompt.GetComponent<QTEPromptView>().coordinator = coordinator;
            // triggerObject（触发器对象）：使用位置：作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
GameObject triggerObject = new GameObject("编辑器测试触发器", typeof(QTEEditorTestTrigger));
            SceneManager.MoveGameObjectToScene(triggerObject, scene);
            // trigger（触发器）：保存 GameObject.GetComponent<QTEEditorTestTrigger>() 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.BuildTestScene(LeonQTEConfig, GameObject)。
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
        // leon（里昂）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 LeonQTESetupUtility.CountBehaviours(AnimatorController) 的参数、作为 LeonQTESetupUtility.CountStates(AnimatorController) 的参数 等共 5 处。
AnimatorController leon = AssetDatabase.LoadAssetAtPath<AnimatorController>(LeonControllerPath);
        // leonStates（里昂状态集合）：保存 LeonQTESetupUtility.CountStates(AnimatorController) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonQTESetupUtility.ValidateControllerMatrices()。
int leonStates = CountStates(leon);
        // leonTransitions（里昂过渡集合）：保存 LeonQTESetupUtility.CountTransitions(AnimatorController) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonQTESetupUtility.ValidateControllerMatrices()。
int leonTransitions = CountTransitions(leon);
        // leonBehaviours（里昂行为集合）：保存 LeonQTESetupUtility.CountBehaviours(AnimatorController) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonQTESetupUtility.ValidateControllerMatrices()。
int leonBehaviours = CountBehaviours(leon);
        if (leon.layers.Length != 5 || leon.parameters.Length != 16 || leonStates != 22 || leonTransitions != 35 || leonBehaviours != 8)
        {
            throw new InvalidOperationException("里昂控制器矩阵不匹配: 层=" + leon.layers.Length + " 参数=" + leon.parameters.Length + " 状态=" + leonStates + " 转换=" + leonTransitions + " 行为=" + leonBehaviours);
        }

        // anyStateTransitions（任意状态过渡集合）：保存 IEnumerable<AnimatorControllerLayer>.Sum<AnimatorControllerLayer>(Func<AnimatorControllerLayer, int>) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.ValidateControllerMatrices()。
int anyStateTransitions = leon.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.anyStateTransitions.Length);
        if (anyStateTransitions != 1)
        {
            throw new InvalidOperationException("里昂控制器任意状态转换数量不是一。");
        }

        // enemy（敌人）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.ValidateControllerMatrices()。
AnimatorController enemy = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyControllerPath);
        // qteLayer（配对动作图层）：保存 IEnumerable<AnimatorControllerLayer>.FirstOrDefault<AnimatorControllerLayer>(Func<AnimatorControllerLayer, bool>) 的返回结果，供当前步骤使用。使用位置：LeonQTESetupUtility.ValidateControllerMatrices()。
AnimatorControllerLayer qteLayer = enemy.layers.FirstOrDefault( /* 中文含义：图层。 */layer => layer.name == "QTE Override");
        if (qteLayer == null || qteLayer.stateMachine.states.Length != 5 || qteLayer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.transitions.Length) != 8)
        {
            throw new InvalidOperationException("村民快速反应覆盖层矩阵不匹配。");
        }
    }

    /// <summary>CountStates 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonQTESetupUtility.CountStates(AnimatorController)。</param>
    private static int CountStates(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.states.Length);
    }

    /// <summary>CountTransitions 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonQTESetupUtility.CountTransitions(AnimatorController)。</param>
    private static int CountTransitions(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.anyStateTransitions.Length + layer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.transitions.Length));
    }

    /// <summary>CountBehaviours 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTESetupUtility.ValidateControllerMatrices()；下游（含内部回调体） IEnumerable&lt;AnimatorControllerLayer&gt;.Sum&lt;AnimatorControllerLayer&gt;(Func&lt;AnimatorControllerLayer, int&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Sum&lt;ChildAnimatorState&gt;(Func&lt;ChildAnimatorState, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonQTESetupUtility.CountBehaviours(AnimatorController)。</param>
    private static int CountBehaviours(AnimatorController controller)
    {
        return controller.layers.Sum( /* 中文含义：图层。 */layer => layer.stateMachine.states.Sum( /* 中文含义：子级。 */child => child.state.behaviours.Length));
    }
}
