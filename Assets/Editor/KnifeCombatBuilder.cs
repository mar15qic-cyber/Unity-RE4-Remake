using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameArchitecture;
using Object = UnityEngine.Object;

/// <summary>制作源只读；通过编辑器资源接口建立战斗派生片段、标记与正式引用。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "小刀战斗构建器")]
public static class KnifeCombatBuilder
{
    /// <summary>中文含义：输出目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.IsValidFolder(string) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)、KnifeCombatBuilder.configurationPath 等共 5 处。</summary>
    public const string outputDirectory = "Assets/Animations/小刀战斗";
    /// <summary>中文含义：配置路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string) 的参数。</summary>
    public const string configurationPath = outputDirectory + "/小刀战斗配置.asset";
    /// <summary>中文含义：控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CopyAsset(string, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数。</summary>
    public const string controllerPath = outputDirectory + "/里昂小刀战斗.controller";
    /// <summary>中文含义：来源目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数、作为 File.ReadAllText(string) 的参数。</summary>
    private const string sourceDirectory = "Assets/Animations/左臂整体重构/";
    public static string RefreshMainLeonNativeKnife()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请退出播放后更新小刀动作。");
        }

        var scene = SceneManager.GetSceneByPath(SceneManagerEx.MainScenePath);
        if (!scene.isLoaded)
        {
            throw new InvalidOperationException("请先打开正式村庄。");
        }

        var player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<LeonController>(true)).Single();
        var graph = player.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
        if (AssetDatabase.GetAssetPath(graph) != RE4.ChapterOne.ChapterOneLoopBuilder.MainLeonController)
        {
            throw new InvalidOperationException("正式场景当前使用的并非 MainLeon 控制器。");
        }

        var destination = player.GetComponent<KnifeCombatModule>().configuration;
        var configuration = Object.Instantiate(destination);
        try
        {
            const string libraryPath = "Assets/RE4ChapterOneAnimationLoop/Generated/Clips/Leon_Clips.asset";
            var clips = AssetDatabase.LoadAllAssetsAtPath(libraryPath).OfType<AnimationClip>()
                .ToDictionary(clip => clip.name);
            configuration.previewIdle = clips["Knife_0160"];
            configuration.previewLocomotionIdle = configuration.previewIdle;
            configuration.previewLocomotion = new[]
            {
                clips["Knife_0403"], clips["Knife_0443"], clips["Knife_0413"],
                clips["Knife_0453"], clips["Knife_0403"], clips["LeonMove_0175"]
            };
            // 原版手指保留在片段中，刀模沿用正式场景的手部挂点。
            configuration.fingerBoneName = Array.Empty<string>();
            configuration.fingerBoneRotation = Array.Empty<Quaternion>();
            configuration.combinedGripCenter = configuration.originalGripCenter;
            configuration.combinedRotation = Quaternion.identity;

            Set(KnifeAction.EnterGuard, "Knife_0140");
            Set(KnifeAction.HoldGuard, "Knife_0160", loop: true);
            Set(KnifeAction.LowerGuard, "Knife_0158");
            Set(KnifeAction.PrimarySlash, "Knife_0630", end: 46, firstHit: 13, lastHit: 23);
            Set(KnifeAction.PrimarySlashRecovery, "Knife_0630", start: 46);
            Set(KnifeAction.Backslash, "Knife_0640", end: 46, firstHit: 15, lastHit: 26);
            Set(KnifeAction.BackslashRecovery, "Knife_0640", start: 46);
            Set(KnifeAction.ThirdSlash, "Knife_0650", end: 46, firstHit: 21, lastHit: 32);
            Set(KnifeAction.ThirdSlashRecovery, "Knife_0650", start: 46);
            Set(KnifeAction.ThrustOne, "Knife_0600", end: 31, firstHit: 12, lastHit: 20);
            Set(KnifeAction.ThrustTwo, "Knife_0610", end: 38, firstHit: 14, lastHit: 24);
            configuration.Find(KnifeAction.ThrustOne).confirmedHitClip = clips["Knife_0601"];
            Set(KnifeAction.ThrustRecovery, "Knife_0600", start: 31);
            Set(KnifeAction.CloseRangeNormal, "Knife_0146", reaction: true);
            Set(KnifeAction.CloseRangePerfect, "Knife_0800", reaction: true);
            Set(KnifeAction.HighNormal, "Knife_0146", reaction: true);
            Set(KnifeAction.HighPerfect, "Knife_0800", reaction: true);
            Set(KnifeAction.OutwardDeflectionNormal, "Knife_0146", reaction: true);
            Set(KnifeAction.OutwardDeflectionPerfect, "Knife_0800", reaction: true);

            BakeTrajectory(player, configuration);
            var knifeMachine = graph.layers[AnimatorLayerID.WeaponAction].stateMachine.stateMachines
                .Select(child => child.stateMachine).Single(machine => machine.name == "小刀重构");
            BindThirdSlashState(KnifeAction.ThrustTwo);
            BindThirdSlashState(KnifeAction.ThirdSlash);
            BindThirdSlashState(KnifeAction.ThirdSlashRecovery);
            EditorUtility.SetDirty(knifeMachine);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssetIfDirty(graph);
            configuration.name = destination.name;
            EditorUtility.CopySerialized(configuration, destination);
            EditorUtility.SetDirty(destination);
            AssetDatabase.SaveAssetIfDirty(destination);
            return $"已接入 {configuration.actionClip.Length} 个正式小刀槽，含 {configuration.actionClip.Count(item => item.timing.HasHit)} 条刀刃轨迹。";

            void BindThirdSlashState(KnifeAction action)
            {
                var item = configuration.Find(action);
                var state = knifeMachine.states.Select(child => child.state).SingleOrDefault(candidate => candidate.name == item.StateName);
                if (state == null)
                {
                    state = knifeMachine.AddState(item.StateName);
                }

                state.motion = item.clip;
                state.writeDefaultValues = false;
                EditorUtility.SetDirty(state);
            }

            void Set(KnifeAction action, string id, int start = 0, int end = -1,
                int firstHit = -1, int lastHit = -1, bool loop = false, bool reaction = false)
            {
                var item = configuration.Find(action);
                if (item == null)
                {
                    item = new KnifeActionConfiguration { action = action };
                    configuration.actionClip = configuration.actionClip.Append(item).ToArray();
                }
                var source = clips[id];
                float frameRate = source.frameRate;
                float begin = start / frameRate;
                float finish = source.length;
                if (end >= 0)
                {
                    finish = end / frameRate;
                }
                var timing = new KnifeActionTiming(finish - begin, loop: loop);
                if (firstHit >= 0)
                {
                    timing.hitBegin = firstHit / frameRate - begin;
                    timing.hitEnd = lastHit / frameRate - begin;
                    timing.earlyPhaseCancelEnd = Mathf.Min(item.timing.earlyPhaseCancelEnd, timing.hitBegin);
                    timing.recoveryCancelBegin = timing.hitEnd;
                }
                else if (reaction)
                {
                    timing.contactPoint = item.timing.contactPoint;
                    timing.recoveryPoint = 41f / frameRate;
                }

                item.clip = source;
                item.sourceClip = source;
                item.sourceStart = begin;
                item.timing = timing;
                item.planarRootTrajectory = null;
                if (action is KnifeAction.PrimarySlash or KnifeAction.Backslash or KnifeAction.ThirdSlash
                    or KnifeAction.PrimarySlashRecovery or KnifeAction.BackslashRecovery or KnifeAction.ThirdSlashRecovery)
                {
                    var x = AnimationUtility.GetEditorCurve(source, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
                    var z = AnimationUtility.GetEditorCurve(source, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"));
                    if (x == null || z == null)
                    {
                        throw new InvalidOperationException("小刀挥砍缺少原版平面根轨：" + id);
                    }

                    int count = Mathf.CeilToInt(timing.duration / item.sampleInterval) + 1;
                    var origin = new Vector3(x.Evaluate(begin), 0f, z.Evaluate(begin));
                    item.planarRootTrajectory = new Vector3[count];
                    for (int index = 0; index < count; index++)
                    {
                        float time = Mathf.Min(finish, begin + index * item.sampleInterval);
                        item.planarRootTrajectory[index] = new Vector3(x.Evaluate(time), 0f, z.Evaluate(time)) - origin;
                    }
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(configuration);
        }
    }

    /// <summary>Read 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AddValue(KnifeAction, string, float, bool)、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数。</param>

    private static AnimationClip Read(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(sourceDirectory + name + ".anim");
    /// <summary>编辑器菜单调用入口 Build；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AddValue(KnifeAction, string, float, bool)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.CreateFolder(string, string)、AssetDatabase.FindAssets(string, string[])、AssetDatabase.IsValidFolder(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string) 等共 38 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/小刀战斗/生成战斗资源并接入当前场景")]
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请退出播放后生成资源。");
        }

        // player（玩家）：保存 Object.FindFirstObjectByType<LeonController>() 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数、作为 KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration) 的参数 等共 6 处。
var player = Object.FindFirstObjectByType<LeonController>();
        if (player == null)
        {
            throw new InvalidOperationException("当前场景没有正式玩家。");
        }

        if (!AssetDatabase.IsValidFolder(outputDirectory))
        {
            AssetDatabase.CreateFolder("Assets/Animations", "小刀战斗");
        }

        // configuration（配置）：保存 AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、作为 KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration) 的参数 等共 7 处。
var configuration = AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(configurationPath);
        if (configuration == null)
        {
            configuration = ScriptableObject.CreateInstance<KnifeCombatConfiguration>();
            AssetDatabase.CreateAsset(configuration, configurationPath);
        }

        // rules（规则）：使用位置：AddValue(KnifeAction, string, float, bool)。
var rules = new KnifeCombatRules();
        // cacheEntry（缓存入口）：使用位置：AddValue(KnifeAction, string, float, bool)、KnifeCombatBuilder.Build()。
var cacheEntry = new List<KnifeActionConfiguration>();
        /// <summary>AddValue 的业务处理入口；本方法直接依赖 Enum.ToString()、KnifeActionConfiguration.KnifeActionConfiguration()、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)、KnifeCombatBuilder.Read(string)、KnifeCombatRules.Query(KnifeAction)、List&lt;KnifeActionConfiguration&gt;.Add(KnifeActionConfiguration)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） Enum.ToString()、KnifeActionConfiguration.KnifeActionConfiguration()、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)、KnifeCombatBuilder.Read(string)、KnifeCombatRules.Query(KnifeAction)、List&lt;KnifeActionConfiguration&gt;.Add(KnifeActionConfiguration)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="action">中文含义：动作。使用位置：作为 KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool) 的参数、作为 KnifeCombatRules.Query(KnifeAction) 的参数、作为 List&lt;KnifeActionConfiguration&gt;.Add(KnifeActionConfiguration) 的参数。</param>
        /// <param name="source">中文含义：来源。使用位置：作为 KnifeCombatBuilder.Read(string) 的参数。</param>
        /// <param name="startPoint">中文含义：起始点。使用位置：作为 KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool) 的参数。</param>
        /// <param name="shortRestore">中文含义：是否短型恢复。使用位置：作为 AddValue(KnifeAction, string, float, bool) 的参数、作为 KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool) 的参数。</param>
        void AddValue(KnifeAction action, string source, float startPoint = 0, bool shortRestore = false)
        {
            // timing（时序）：保存 KnifeCombatRules.Query(KnifeAction) 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool) 的参数、作为 List<KnifeActionConfiguration>.Add(KnifeActionConfiguration) 的参数。
var timing = rules.Query(action);
            // clip（动画片段）：保存 KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool) 的返回结果，供当前步骤使用。使用位置：作为 List<KnifeActionConfiguration>.Add(KnifeActionConfiguration) 的参数。
var clip = Derived(action.ToString(), Read(source), startPoint, timing.duration, timing.loop, shortRestore);
            cacheEntry.Add(new KnifeActionConfiguration { action = action, timing = timing, clip = clip });
        }

        AddValue(KnifeAction.EnterGuard, "进入防守");
        AddValue(KnifeAction.HoldGuard, "防守保持");
        AddValue(KnifeAction.LowerGuard, "放下防守");
        AddValue(KnifeAction.PrimarySlash, "右向左主挥砍");
        AddValue(KnifeAction.Backslash, "左向右回切");
        AddValue(KnifeAction.PrimarySlashRecovery, "主挥砍回收");
        AddValue(KnifeAction.BackslashRecovery, "回切回收");
        AddValue(KnifeAction.ThrustOne, "快速前刺循环");
        AddValue(KnifeAction.ThrustTwo, "快速前刺循环", .5f);
        AddValue(KnifeAction.ThrustRecovery, "防守保持");
        AddValue(KnifeAction.CloseRangeNormal, "近身完美弹反", shortRestore: true);
        AddValue(KnifeAction.CloseRangePerfect, "近身完美弹反");
        AddValue(KnifeAction.HighNormal, "高位截挡", shortRestore: true);
        AddValue(KnifeAction.HighPerfect, "高位截挡");
        AddValue(KnifeAction.OutwardDeflectionNormal, "斜上外拨弹反", shortRestore: true);
        AddValue(KnifeAction.OutwardDeflectionPerfect, "斜上外拨弹反");
        configuration.actionClip = cacheEntry.ToArray();
        // calibration（校准）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.Build()。
var calibration = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(sourceDirectory + "握柄标定.json"));
        // fingerBone（手指骨骼）：保存 JToken.ToObject<Dictionary<string, float[]>>() 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.Build()。
var fingerBone = calibration["指骨局部旋转"].ToObject<Dictionary<string, float[]>>();
        configuration.fingerBoneName = fingerBone.Keys.ToArray();
        configuration.fingerBoneRotation = fingerBone.Values.Select( /* 中文含义：当前浮点数值数组。 */q => new Quaternion(q[0], q[1], q[2], q[3])).ToArray();
        BakeTrajectory(player, configuration);
        KnifePreviewIntegrationBuilder.Configure(configuration);
        configuration.whiffSound = AuthoringSound("挥空", 0);
        configuration.sliceIntoSound = AuthoringSound("切入", 1);
        configuration.hardSurfaceSound = AuthoringSound("硬物", 2);
        configuration.normalParrySound = AuthoringSound("普通招架", 3);
        configuration.perfectParrySound = AuthoringSound("完美弹反", 4);
        configuration.knifeBreakSound = AuthoringSound("断刀", 5);
        EditorUtility.SetDirty(configuration);
        // controller（动画控制器资源）：保存 KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration) 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController) 的参数。
var controller = CreateController(player.GetComponent<Animator>(), configuration);
        // preControlPath（前置控制路径）：使用位置：作为 PrefabUtility.LoadPrefabContents(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数。
var preControlPath = "Assets/Prefab/LeonR4R.prefab";
        // content（内容）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、KnifeCombatBuilder.Build()。
var content = PrefabUtility.LoadPrefabContents(preControlPath);
        try
        {
            // 此项目的同名预制件只是展示模型，正式业务组件挂在场景模型实例上。
            // preControlPlayer（前置控制玩家）：保存 GameObject.GetComponent<LeonController>() 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController) 的参数、KnifeCombatBuilder.Build()。
var preControlPlayer = content.GetComponent<LeonController>();
            if (preControlPlayer != null)
            {
                BindingPlayer(preControlPlayer, configuration, controller);
                PrefabUtility.SaveAsPrefabAsset(content, preControlPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(content);
        }

        BindingPlayer(player, configuration, controller);
        // count（数量）：使用位置：KnifeCombatBuilder.Build()。
int count = 0;
        // path（路径）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数、作为 PrefabUtility.LoadPrefabContents(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数。
foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            // asset（资源）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.Build()。
var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<VillagerController>() == null)
            {
                continue;
            }

            // root（根游戏对象）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatBuilder.BindingEnemy(VillagerController) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数。
var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                BindingEnemy(root.GetComponent<VillagerController>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // enemy（敌人）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 KnifeCombatBuilder.BindingEnemy(VillagerController) 的参数。
foreach (var enemy in Object.FindObjectsByType<VillagerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            BindingEnemy(enemy);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveScene(player.gameObject.scene);
        return "已生成十六个派生动作，完成玩家与" + count + "个敌人预制件绑定。";
    }

    /// <summary>Derived 的业务处理入口；本方法直接依赖 AnimationClip.AnimationClip()、AnimationClip.ClearCurves()、AnimationClip.EnsureQuaternionContinuity()、AnimationCurve.AnimationCurve(params Keyframe[])、AnimationCurve.Evaluate(float)、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.GetCurveBindings(AnimationClip) 等共 28 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AddValue(KnifeAction, string, float, bool)；下游（含内部回调体） AnimationClip.AnimationClip()、AnimationClip.ClearCurves()、AnimationClip.EnsureQuaternionContinuity()、AnimationCurve.AnimationCurve(params Keyframe[])、AnimationCurve.Evaluate(float)、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.GetCurveBindings(AnimationClip) 等共 28 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nameValue">中文含义：名称值。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。</param>
    /// <param name="source">中文含义：来源动画片段。使用位置：作为 AnimationUtility.GetAnimationClipSettings(AnimationClip) 的参数、作为 AnimationUtility.GetCurveBindings(AnimationClip) 的参数、作为 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的参数 等共 5 处。</param>
    /// <param name="startPoint">中文含义：起始点。使用位置：作为 Mathf.Min(float, float) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.CeilToInt(float) 的参数、作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 4 处。</param>
    /// <param name="loop">中文含义：是否循环。使用位置：KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。</param>
    /// <param name="shortRestore">中文含义：是否短型恢复。使用位置：KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。</param>
    private static AnimationClip Derived(string nameValue, AnimationClip source, float startPoint, float duration, bool loop, bool shortRestore)
    {
        if (source == null)
        {
            throw new InvalidOperationException("缺少制作源：" + nameValue);
        }

        // path（路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的参数。
string path = outputDirectory + "/" + nameValue + ".anim";
        // target（目标动画片段）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings) 的参数、作为 AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[]) 的参数、作为 AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve) 的参数 等共 6 处。
var target = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (target == null)
        {
            target = new AnimationClip();
            AssetDatabase.CreateAsset(target, path);
        }

        target.ClearCurves();
        target.name = nameValue;
        target.frameRate = 120;
        // guard（防守）：保存 KnifeCombatBuilder.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的参数。
var guard = Read("防守保持");
        // binding（绑定）：每轮从取得曲线绑定集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的参数、作为 AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve) 的参数。
foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            // original（原始）：保存 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
var original = AnimationUtility.GetEditorCurve(source, binding);
            // recover（恢复）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
global::UnityEngine.AnimationCurve recover;
            if (shortRestore)
            {
                recover = AnimationUtility.GetEditorCurve(guard, binding);
            }
            else
            {
                recover = null;
            }

            // key（键）：使用位置：作为 AnimationCurve.AnimationCurve(params Keyframe[]) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
var key = new List<Keyframe>();
            // count（数量）：保存 Mathf.CeilToInt(float) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
int count = Mathf.CeilToInt(duration * 120);
            // index（当前元素索引）：使用位置：作为 Mathf.Min(float, float) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
for (int index = 0; index <= count; index++)
            {
                // time（时间）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Keyframe.Keyframe(float, float) 的参数、作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Mathf.Min(float, float) 的参数。
float time = Mathf.Min(duration, index / 120f);
                // value（值）：保存 AnimationCurve.Evaluate(float) 的返回结果，供当前步骤使用。使用位置：作为 Keyframe.Keyframe(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
float value = original.Evaluate(Mathf.Min(source.length, startPoint + time));
                if (recover != null)
                {
                    value = Mathf.Lerp(value, recover.Evaluate(0), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(duration * .62f, duration, time)));
                }

                key.Add(new Keyframe(time, value));
            }

            // curve（曲线）：使用位置：作为 AnimationUtility.SetEditorCurve(AnimationClip, EditorCurveBinding, AnimationCurve) 的参数、作为 AnimationUtility.SetKeyLeftTangentMode(AnimationCurve, int, AnimationUtility.TangentMode) 的参数、作为 AnimationUtility.SetKeyRightTangentMode(AnimationCurve, int, AnimationUtility.TangentMode) 的参数 等共 4 处。
var curve = new AnimationCurve(key.ToArray());
            // index（当前元素索引）：使用位置：作为 AnimationUtility.SetKeyLeftTangentMode(AnimationCurve, int, AnimationUtility.TangentMode) 的参数、作为 AnimationUtility.SetKeyRightTangentMode(AnimationCurve, int, AnimationUtility.TangentMode) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
for (int index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            }

            AnimationUtility.SetEditorCurve(target, binding, curve);
        }

        // set（集合）：保存 AnimationUtility.GetAnimationClipSettings(AnimationClip) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings) 的参数、KnifeCombatBuilder.Derived(string, AnimationClip, float, float, bool, bool)。
var set = AnimationUtility.GetAnimationClipSettings(source);
        set.startTime = 0;
        set.stopTime = duration;
        set.loopTime = loop;
        set.keepOriginalPositionXZ = true;
        set.keepOriginalPositionY = true;
        set.keepOriginalOrientation = true;
        set.loopBlendPositionXZ = true;
        set.loopBlendPositionY = true;
        set.loopBlendOrientation = true;
        AnimationUtility.SetAnimationClipSettings(target, set);
        AnimationUtility.SetAnimationEvents(target, Array.Empty<AnimationEvent>());
        target.EnsureQuaternionContinuity();
        EditorUtility.SetDirty(target);
        return target;
    }

    /// <summary>BakeTrajectory 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponentsInChildren&lt;MeshFilter&gt;(bool)、EditorSceneManager.ClosePreviewScene(Scene) 等共 25 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponentsInChildren&lt;MeshFilter&gt;(bool)、EditorSceneManager.ClosePreviewScene(Scene) 等共 25 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。</param>
    /// <param name="configuration">中文含义：配置。使用位置：作为 Transform.TransformPoint(Vector3) 的参数、作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。</param>
    private static void BakeTrajectory(LeonController player, KnifeCombatConfiguration configuration)
    {
        // scene（场景）：保存 EditorSceneManager.NewPreviewScene() 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.ClosePreviewScene(Scene) 的参数、作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数。
var scene = EditorSceneManager.NewPreviewScene();
        // model（模型）：使用位置：作为 AnimationClip.SampleAnimation(GameObject, float) 的参数、作为 Object.DestroyImmediate(Object) 的参数、作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数 等共 4 处。
GameObject model = null;
        try
        {
            model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/角色/里昂/LeonR4R.fbx"));
            SceneManager.MoveGameObjectToScene(model, scene);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            model.transform.localScale = Vector3.one;
            // animator（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
var animator = model.GetComponent<Animator>();
            animator.applyRootMotion = false;
            // sourceAnimator（来源动画控制器）：保存 Component.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
var sourceAnimator = player.GetComponent<Animator>();
            // sourceHand（来源手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Inverse(Quaternion) 的参数、作为 Vector3.Vector3(float, float, float) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
var sourceHand = AnimatorBoneBindings.Resolve(sourceAnimator, HumanBodyBones.RightHand);
            // hand（手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数。
var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            // sourceKnife（来源小刀）：保存 LeonWeaponVisualController.GetWeaponVisualRoot(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 Object.Instantiate<GameObject>(GameObject, Transform, bool) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 4 处。
var sourceKnife = player.GetComponent<LeonWeaponVisualController>().GetWeaponVisualRoot(WeaponType.Knife);
            // knife（小刀）：使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
var knife = Object.Instantiate(sourceKnife.gameObject, hand, false).transform;
            // originalPosition（原始位置）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
Vector3 originalPosition = sourceHand.InverseTransformPoint(sourceKnife.position);
            // originalRotation（原始旋转）：使用位置：作为 Quaternion.Inverse(Quaternion) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
Quaternion originalRotation = Quaternion.Inverse(sourceHand.rotation) * sourceKnife.rotation;
            knife.localPosition = originalPosition;
            knife.localRotation = originalRotation;
            knife.localScale = new Vector3(sourceKnife.lossyScale.x / sourceHand.lossyScale.x, sourceKnife.lossyScale.y / sourceHand.lossyScale.y, sourceKnife.lossyScale.z / sourceHand.lossyScale.z);
            // axis（轴）：使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数。
Vector3 axis = Quaternion.Inverse(originalRotation) * LeonKnifeGrip.originalKnifeAxis;
            // farthest（最远）：使用位置：作为 float.IsNegativeInfinity(float) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
float farthest = float.NegativeInfinity;
            // mesh（网格）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
foreach (var mesh in knife.GetComponentsInChildren<MeshFilter>(true))
            {
                // top（顶部）：每轮从网格的共享网格的顶点集合取得一个元素，仅代表当前这一项。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。
foreach (var top in mesh.sharedMesh.vertices)
                {
                    // point（点）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Dot(Vector3, Vector3) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
Vector3 point = knife.InverseTransformPoint(mesh.transform.TransformPoint(top));
                    if (Vector3.Dot(point, axis) <= farthest)
                    {
                        continue;
                    }

                    farthest = Vector3.Dot(point, axis);
                    configuration.bladeTipLocal = point;
                }
            }

            if (float.IsNegativeInfinity(farthest))
            {
                throw new InvalidOperationException("未能读取刀具网格。");
            }

            // grip（握持）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。
Vector3 grip = knife.InverseTransformPoint(hand.TransformPoint(configuration.originalGripCenter));
            configuration.bladeBaseLocal = Vector3.Lerp(grip, configuration.bladeTipLocal, .35f);
            // item（当前小刀动作配置）：每轮从配置的动作动画片段取得一个元素，仅代表当前这一项。使用位置：作为 Mathf.CeilToInt(float) 的参数、作为 Mathf.Min(float, float) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
foreach (var item in configuration.actionClip)
            {
                if (!item.timing.HasHit)
                {
                    continue;
                }

                // count（数量）：使用位置：KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
int count = Mathf.CeilToInt(item.timing.duration * 120) + 1;
                item.bladeBaseTrajectory = new Vector3[count];
                item.bladeTipTrajectory = new Vector3[count];
                item.sampleInterval = 1f / 120;
                // index（当前元素索引）：使用位置：作为 Mathf.Min(float, float) 的参数、KnifeCombatBuilder.BakeTrajectory(LeonController, KnifeCombatConfiguration)。
for (int index = 0; index < count; index++)
                {
                    float time = Mathf.Min(item.timing.duration, index / 120f);
                    if (item.sourceClip != null && !item.sourceClip.humanMotion)
                    {
                        item.sourceClip.SampleAnimation(model, item.sourceStart + time);
                    }
                    else
                    {
                        item.clip.SampleAnimation(model, time);
                    }
                    model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    knife.localPosition = configuration.combinedGripCenter + configuration.combinedRotation * (originalPosition - configuration.originalGripCenter);
                    knife.localRotation = configuration.combinedRotation * originalRotation;
                    item.bladeBaseTrajectory[index] = model.transform.InverseTransformPoint(knife.TransformPoint(configuration.bladeBaseLocal));
                    item.bladeTipTrajectory[index] = model.transform.InverseTransformPoint(knife.TransformPoint(configuration.bladeTipLocal));
                }
            }
        }
        finally
        {
            if (model != null)
            {
                Object.DestroyImmediate(model);
            }

            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>CreateController 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddState(string)、AnimatorStateMachine.AddStateMachine(string)、AssetDatabase.CopyAsset(string, string)、AssetDatabase.GetAssetPath(Object)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） AnimatorStateMachine.AddState(string)、AnimatorStateMachine.AddStateMachine(string)、AssetDatabase.CopyAsset(string, string)、AssetDatabase.GetAssetPath(Object)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">中文含义：动画控制器。使用位置：作为 AssetDatabase.GetAssetPath(Object) 的参数。</param>
    /// <param name="configuration">中文含义：配置。使用位置：KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。</param>
    private static AnimatorController CreateController(Animator animator, KnifeCombatConfiguration configuration)
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), controllerPath))
            {
                throw new InvalidOperationException("无法保留并派生正式控制器。");
            }

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        }

        /// <summary>RemovePreviousEntryPoint 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()、IEnumerable&lt;AnimatorStateTransition&gt;.Where&lt;AnimatorStateTransition&gt;(Func&lt;AnimatorStateTransition, bool&gt;)、RemovePreviousEntryPoint(AnimatorStateMachine)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)、RemovePreviousEntryPoint(AnimatorStateMachine)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()、IEnumerable&lt;AnimatorStateTransition&gt;.Where&lt;AnimatorStateTransition&gt;(Func&lt;AnimatorStateTransition, bool&gt;)、RemovePreviousEntryPoint(AnimatorStateMachine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="machine">中文含义：状态机。使用位置：RemovePreviousEntryPoint(AnimatorStateMachine)。</param>
        void RemovePreviousEntryPoint(AnimatorStateMachine machine)
        {
            if (machine == null)
            {
                return;
            }

            /// <summary>PreviousCondition 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorCondition&gt;.Any&lt;AnimatorCondition&gt;(Func&lt;AnimatorCondition, bool&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
            /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） IEnumerable&lt;AnimatorCondition&gt;.Any&lt;AnimatorCondition&gt;(Func&lt;AnimatorCondition, bool&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
            /// <param name="rotate">中文含义：旋转。使用位置：PreviousCondition(AnimatorStateTransition)。</param>
            bool PreviousCondition(AnimatorStateTransition rotate)
            {
                return rotate.conditions.Any( /* 中文含义：当前动画控制器条件。 */c => c.parameter == "FullBodyAction" && c.mode == AnimatorConditionMode.Equals && (c.threshold == (int)FullBodyActionType.KnifeQuickAttack || c.threshold == (int)FullBodyActionType.KnifeParry));
            }

            // rotate（旋转）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数。
foreach (var rotate in machine.anyStateTransitions.Where(PreviousCondition).ToArray())
            {
                machine.RemoveAnyStateTransition(rotate);
            }

            // stateValue（状态值）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：RemovePreviousEntryPoint(AnimatorStateMachine)。
foreach (var stateValue in machine.states)
            {
                // rotate（旋转）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数。
foreach (var rotate in stateValue.state.transitions.Where(PreviousCondition).ToArray())
                {
                    stateValue.state.RemoveTransition(rotate);
                }
            }

            // child（子级）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 RemovePreviousEntryPoint(AnimatorStateMachine) 的参数。
foreach (var child in machine.stateMachines)
            {
                RemovePreviousEntryPoint(child.stateMachine);
            }
        }

        // layer（图层）：每轮从控制器的图层集合取得一个元素，仅代表当前这一项。使用位置：作为 RemovePreviousEntryPoint(AnimatorStateMachine) 的参数。
foreach (var layer in controller.layers)
        {
            RemovePreviousEntryPoint(layer.stateMachine);
        }

        // root（根动画状态机）：使用位置：KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
var root = controller.layers[AnimatorLayerID.WeaponAction].stateMachine;
        // availableValue2（空值判断前缓存的选择的返回结果）：从选择的返回结果读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
global::UnityEditor.Animations.AnimatorStateMachine availableValue2 = root.stateMachines.Select( /* 中文含义：横向。 */x => x.stateMachine).FirstOrDefault( /* 中文含义：横向。 */x => x.name == "小刀重构");
        // subStateMachine（子级状态机）：使用位置：KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
global::UnityEditor.Animations.AnimatorStateMachine subStateMachine;
        if (!global::System.Object.ReferenceEquals(availableValue2, null))
        {
            subStateMachine = availableValue2;
        }
        else
        {
            subStateMachine = root.AddStateMachine("小刀重构");
        }

        // itemValue（物品值）：每轮从配置的动作动画片段取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.AddState(string) 的参数、作为 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的参数、KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
foreach (var itemValue in configuration.actionClip)
        {
            // state（当前动画状态）：保存 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的返回结果，供当前步骤使用。使用位置：KnifeCombatBuilder.CreateController(Animator, KnifeCombatConfiguration)。
var state = subStateMachine.states.Select( /* 中文含义：当前子级动画控制器状态。 */item => item.state).FirstOrDefault( /* 中文含义：当前动画状态。 */item => item.name == itemValue.StateName);
            if (state == null)
            {
                state = subStateMachine.AddState(itemValue.StateName);
            }

            state.motion = itemValue.clip;
            state.writeDefaultValues = false;
            state.speed = 1;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    /// <summary>BindingPlayer 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;KnifeCombatModule&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;KnifeCombatModule&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)。</param>
    /// <param name="configuration">中文含义：配置。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)。</param>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)。</param>
    private static void BindingPlayer(LeonController player, KnifeCombatConfiguration configuration, AnimatorController controller)
    {
        // availableValue4（空值判断前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)。
global::KnifeCombatModule availableValue4 = player.GetComponent<KnifeCombatModule>();
        // module（模块）：使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、KnifeCombatBuilder.BindingPlayer(LeonController, KnifeCombatConfiguration, AnimatorController)。
global::KnifeCombatModule module;
        if (!global::System.Object.ReferenceEquals(availableValue4, null))
        {
            module = availableValue4;
        }
        else
        {
            module = player.gameObject.AddComponent<KnifeCombatModule>();
        }

        module.player = player;
        module.combat = player.GetComponent<LeonCombat>();
        module.coordinator = player.GetComponent<LeonAnimationActionCoordinator>();
        module.animationDrive = player.GetComponent<LeonAnimatorDriver>();
        module.weapon = player.GetComponent<LeonWeaponHandler>();
        module.weaponVisual = player.GetComponent<LeonWeaponVisualController>();
        module.handConstraint = player.GetComponent<LeonAnimRig>();
        module.configuration = configuration;
        module.knifeVisual = module.weaponVisual.GetWeaponVisualRoot(WeaponType.Knife);
        module.combat.knifeModule = module;
        module.weapon.knifeModule = module;
        player.GetComponent<Animator>().runtimeAnimatorController = controller;
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(module);
        EditorUtility.SetDirty(module.combat);
        EditorUtility.SetDirty(module.weapon);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.GetComponent<Animator>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(module.combat);
        PrefabUtility.RecordPrefabInstancePropertyModifications(module.weapon);
        PrefabUtility.RecordPrefabInstancePropertyModifications(module);
    }

    /// <summary>BindingEnemy 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;ChainsawManAnimationBridge&gt;()、Component.GetComponent&lt;KnifeHitStop&gt;()、EditorUtility.SetDirty(Object)、EnemyCombatModule.ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[])、Enumerable.Range(int, int)、GameObject.AddComponent&lt;KnifeHitStop&gt;() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;ChainsawManAnimationBridge&gt;()、Component.GetComponent&lt;KnifeHitStop&gt;()、EditorUtility.SetDirty(Object)、EnemyCombatModule.ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[])、Enumerable.Range(int, int)、GameObject.AddComponent&lt;KnifeHitStop&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">中文含义：敌人。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 IEnumerable&lt;int&gt;.Select&lt;int, KnifeEnemyAttackProfile&gt;(Func&lt;int, KnifeEnemyAttackProfile&gt;) 的参数、作为 Mathf.Min(int, int) 的参数 等共 5 处。</param>
    private static void BindingEnemy(VillagerController enemy)
    {
        // availableValue6（空值判断前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、KnifeCombatBuilder.BindingEnemy(VillagerController)。
global::KnifeHitStop availableValue6 = enemy.GetComponent<KnifeHitStop>();
        // pause（暂停）：使用位置：作为 EditorUtility.SetDirty(Object) 的参数、KnifeCombatBuilder.BindingEnemy(VillagerController)。
global::KnifeHitStop pause;
        if (!global::System.Object.ReferenceEquals(availableValue6, null))
        {
            pause = availableValue6;
        }
        else
        {
            pause = enemy.gameObject.AddComponent<KnifeHitStop>();
        }

        pause.animator = enemy.GetComponent<Animator>();
        enemy.knifeLocalHitStop = pause;
        if (enemy.combatModule != null && !enemy.combatModule.HasKnifeAttackProfiles)
        {
            enemy.combatModule.ConfigureKnifeAttackProfiles(Enumerable.Range(0, Mathf.Min(3, enemy.standingAttackAnimationCount)).Select( /* 中文含义：索引。 */index => new KnifeEnemyAttackProfile { category = (KnifeIncomingAttackCategory)index, canParry = enemy.enableKnifeParry && enemy.GetComponent<ChainsawManAnimationBridge>() == null, contactDistance = enemy.attackHitRange, contactAngle = enemy.attackHitAngle, contactNormalizedPoint = enemy.standingAttackFallbackHitNormalizedTime }).ToArray());
            EditorUtility.SetDirty(enemy.combatModule);
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.combatModule);
        }

        EditorUtility.SetDirty(pause);
        EditorUtility.SetDirty(enemy);
        PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
    }

    // 临时合成的六类反馈音可独立替换，不冒充原作录音。
    /// <summary>AuthoringSound 的业务处理入口；本方法直接依赖 AssetDatabase.ImportAsset(string)、AssetDatabase.LoadAssetAtPath&lt;AudioClip&gt;(string)、BinaryWriter.BinaryWriter(Stream)、BinaryWriter.Write(byte[])、BinaryWriter.Write(int)、BinaryWriter.Write(short)、Encoding.GetBytes(string) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()；下游（含内部回调体） AssetDatabase.ImportAsset(string)、AssetDatabase.LoadAssetAtPath&lt;AudioClip&gt;(string)、BinaryWriter.BinaryWriter(Stream)、BinaryWriter.Write(byte[])、BinaryWriter.Write(int)、BinaryWriter.Write(short)、Encoding.GetBytes(string) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。</param>
    /// <param name="category">中文含义：类别。使用位置：作为 Random.Random(int) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。</param>
    private static AudioClip AuthoringSound(string name, int category)
    {
        // path（路径）：使用位置：作为 AssetDatabase.ImportAsset(string) 的参数、作为 AssetDatabase.LoadAssetAtPath<AudioClip>(string) 的参数、作为 File.Create(string) 的参数 等共 4 处。
string path = outputDirectory + "/" + name + ".wav";
        if (!File.Exists(path))
        {
            // sampleRate（采样速率）：使用位置：作为 BinaryWriter.Write(int) 的参数、作为 Mathf.RoundToInt(float) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
const int sampleRate = 44100;
            // duration（持续时间）：使用位置：作为 Mathf.RoundToInt(float) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
float duration;
            if (category == 4)
            {
                duration = .28f;
            }
            else
            {
                if (category == 5)
                {
                    duration = .30f;
                }
                else
                {
                    duration = .15f;
                }
            }

            // totalCount（总计数量）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：作为 BinaryWriter.Write(int) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
int totalCount = Mathf.RoundToInt(sampleRate * duration);
            // random（随机）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
var random = new System.Random(173 + category);
            // previousNoise（前一次噪声）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
float previousNoise = 0;
            // stream（数据流）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var stream = new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                stream.Write(36 + totalCount * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                stream.Write(16);
                stream.Write((short)1);
                stream.Write((short)1);
                stream.Write(sampleRate);
                stream.Write(sampleRate * 2);
                stream.Write((short)2);
                stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                stream.Write(totalCount * 2);
                // index（当前元素索引）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
for (int index = 0; index < totalCount; index++)
                {

                    // 中文含义：time 为“时间”。；ratio 为“比例”。
float time = index / (float)sampleRate, ratio = time / duration;
                    // noise（噪声）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
float noise = (float)random.NextDouble() * 2 - 1;
                    // argumentValue11（按原顺序缓存的实参前一次噪声）：从前一次噪声读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float argumentValue11 = previousNoise;
                    // argumentValue12（按原顺序缓存的实参噪声）：从噪声读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float argumentValue12 = noise;
                    // selectedValue10（分支选出的浮点计算）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
float selectedValue10;
                    if (category == 1)
                    {
                        selectedValue10 = .15f;
                    }
                    else
                    {
                        selectedValue10 = .6f;
                    }

                    previousNoise = Mathf.Lerp(argumentValue11, argumentValue12, selectedValue10);
                    // envelope（包络）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
float envelope;
                    if (category == 0)
                    {
                        envelope = Mathf.Sin(ratio * Mathf.PI) * (1 - ratio);
                    }
                    else
                    {
                        // leftOperand14（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Exp(float) 的参数。
float leftOperand14 = -time;
                        // selectedValue13（分支选出的15）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Exp(float) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
int selectedValue13;
                        if (category == 4)
                        {
                            selectedValue13 = 15;
                        }
                        else
                        {
                            selectedValue13 = 28;
                        }

                        envelope = Mathf.Exp(leftOperand14 * (selectedValue13)) * Mathf.Min(1, time * 1500);
                    }

                    // leftOperand17（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Sin(float) 的参数。
float leftOperand17 = time * Mathf.PI * 2;
                    // selectedValue16（分支选出的2150）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Sin(float) 的参数、KnifeCombatBuilder.AuthoringSound(string, int)。
int selectedValue16;
                    if (category == 4)
                    {
                        selectedValue16 = 2150;
                    }
                    else
                    {
                        selectedValue16 = 1550;
                    }

                    // metal（金属）：使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
float metal = Mathf.Sin(leftOperand17 * (selectedValue16)) * .35f + Mathf.Sin(time * Mathf.PI * 2 * 3871) * .20f;
                    // selectedValue18（分支选出的前一次噪声）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：KnifeCombatBuilder.AuthoringSound(string, int)。
float selectedValue18;
                    if (category < 2)
                    {
                        selectedValue18 = previousNoise;
                    }
                    else
                    {
                        selectedValue18 = metal + previousNoise * .30f;
                    }

                    // sample（采样）：使用位置：作为 Mathf.Clamp(float, float, float) 的参数。
float sample = (selectedValue18) * envelope * .35f;
                    stream.Write((short)(Mathf.Clamp(sample, -1, 1) * short.MaxValue));
                }
            }

            AssetDatabase.ImportAsset(path);
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
