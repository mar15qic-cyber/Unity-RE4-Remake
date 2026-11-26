using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 三类快速反应事件的资源构建与测试接线工具。
/// 不修改行为树、音效和摄像机脚本，只生成动画副本、配置和提示界面引用。
/// </summary>
public static class LeonExpandedQTESetupUtility
{
    /// <summary>配置路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ConfigPath = "Assets/Settings/QTE/里昂正面抓取配置.asset";
    /// <summary>提示预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string PromptPrefabPath = "Assets/Prefab/通用/QTE/QTEPrompt.prefab";
    /// <summary>村民预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string VillagerPrefabPath = "Assets/Prefab/Villager.prefab";
    /// <summary>来源根节点。编译期常量，保持既有值与消费者映射。</summary>
    private const string SourceRoot = "Assets/素材库/动画/Zombie_Animations/Animations/";
    /// <summary>扩展后的根节点。编译期常量，保持既有值与消费者映射。</summary>
    private const string ExpandedRoot = "Assets/Animations/QTE/Expanded/";
    /// <summary>图片路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string PicturePath = "Assets/Resources/Pictures/cs_ui0000_iam.tex.png";
    /// <summary>编辑器菜单调用入口 Build；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、EditorUtility.SetDirty(Object)、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/构建三类快速反应事件扩展")]
    public static void Build()
    {
        EnsureFolder("Assets/Settings", "QTE");
        EnsureFolder("Assets/Animations/QTE", "Expanded");
        // 当前里昂配对动作配置。
LeonQTEConfig config = LoadOrCreateConfig();
        // 变体集合。
var variants = new List<QTEAnimationVariant>
        {
            BuildRearBiteVariant(),
            BuildLegBiteVariant(),
            BuildKnifeVariant()
        };
        config.pairingDistance = 0.68f;
        config.variants = variants.ToArray();
        EditorUtility.SetDirty(config);
        SetupVillagerPrefab();
        SetupPromptPrefab();
        SetupTestScene(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[里昂快速反应事件] 背后咬脖子、倒地咬腿、持刀暗杀及提示界面资源已接线。");
    }

    /// <summary>BuildRearBiteVariant 的业务处理入口；本方法直接依赖 LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static QTEAnimationVariant BuildRearBiteVariant()
    {
        // 根节点。
string root = ExpandedRoot + "背后咬脖子/";
        EnsureFolder(ExpandedRoot.TrimEnd('/'), "背后咬脖子");
        return new QTEAnimationVariant
        {
            kind = QTEKind.RearBiteNeck,
            playerEnter = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Start_Vic.fbx", root + "里昂_进入.anim", false, false),
            playerHold = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Loop_Vic.fbx", root + "里昂_挣扎.anim", true, false),
            playerSuccess = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_End_Vic.fbx", root + "里昂_挣脱成功.anim", false, false),
            playerFailure = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Vic.fbx", root + "里昂_咬颈失败.anim", false, false),
            enemyEnter = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Start_Att.fbx", root + "村民_进入.anim", false, false),
            enemyHold = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Loop_Att.fbx", root + "村民_保持.anim", true, false),
            enemySuccess = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_End_Att.fbx", root + "村民_被推开.anim", false, false),
            enemyFailure = CloneClip(SourceRoot + "Paired_Zombie_BiteAttempt_BiteNeck_FromBehind_Att.fbx", root + "村民_咬颈.anim", false, true),
            pairingDistance = 0.68f,
            failureDamage = 20f,
            requiresRearApproach = true
        };
    }

    /// <summary>BuildLegBiteVariant 的业务处理入口；本方法直接依赖 LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static QTEAnimationVariant BuildLegBiteVariant()
    {
        // 根节点。
string root = ExpandedRoot + "倒地咬腿/";
        EnsureFolder(ExpandedRoot.TrimEnd('/'), "倒地咬腿");
        return new QTEAnimationVariant
        {
            kind = QTEKind.LegBite,
            playerEnter = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_FromFront_Start_Vic.fbx", root + "里昂_进入.anim", false, false),
            playerHold = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_Loop_Vic.FBX", root + "里昂_挣扎.anim", true, false),
            playerSuccess = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_End_Vic.FBX", root + "里昂_挣脱成功.anim", false, false),
            playerFailure = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_Vic.FBX", root + "里昂_咬腿失败.anim", false, false),
            enemyEnter = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_FromFront_Start_Att.fbx", root + "村民_进入.anim", false, false),
            enemyHold = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_Loop_Att.FBX", root + "村民_保持.anim", true, false),
            enemySuccess = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_End_Att.FBX", root + "村民_被推开.anim", false, false),
            enemyFailure = CloneClip(SourceRoot + "Paired_Zombie_Crawl_BiteLeg_Att.FBX", root + "村民_咬腿.anim", false, true),
            pairingDistance = 0.62f,
            failureDamage = 20f,
            requiresCrawlingEnemy = true
        };
    }

    /// <summary>BuildKnifeVariant 的业务处理入口；本方法直接依赖 LeonExpandedQTESetupUtility.AddContactEvent(AnimationClip, string, float)、LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） LeonExpandedQTESetupUtility.AddContactEvent(AnimationClip, string, float)、LeonExpandedQTESetupUtility.CloneClip(string, string, bool, bool)、LeonExpandedQTESetupUtility.EnsureFolder(string, string)、QTEAnimationVariant.QTEAnimationVariant()、string.TrimEnd(char)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static QTEAnimationVariant BuildKnifeVariant()
    {
        // 根节点。
string root = ExpandedRoot + "持刀暗杀/";
        EnsureFolder(ExpandedRoot.TrimEnd('/'), "持刀暗杀");
        // 玩家。
AnimationClip player = CloneClip("Assets/素材库/动画/StealthFinishers_Knife_and_Hand/Animations/Knife_Stealth/Paired_Knife_Stealth_ClavicleStabDown_Att.FBX", root + "里昂_锁骨刺击.anim", false, false);
        // 受击者。
AnimationClip victim = CloneClip("Assets/素材库/动画/StealthFinishers_Knife_and_Hand/Animations/Knife_Stealth/Paired_Knife_Stealth_ClavicleStabDown_Vic.FBX", root + "村民_锁骨受击.anim", false, false);
        return new QTEAnimationVariant
        {
            kind = QTEKind.KnifeAssassination,
            playerEnter = player,
            playerHold = player,
            playerSuccess = player,
            playerFailure = player,
            enemyEnter = victim,
            enemyHold = victim,
            enemySuccess = AddContactEvent(victim, "OnQTEAssassinationContact", 0.58f),
            enemyFailure = victim,
            pairingDistance = 0.72f,
            failureDamage = 0f,
            playerIsAttacker = true,
            automaticSuccess = true,
            singleShot = true,
            requiresRearApproach = true
        };
    }

    /// <summary>LoadOrCreateConfig 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;LeonQTEConfig&gt;(string)、ScriptableObject.CreateInstance&lt;LeonQTEConfig&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;LeonQTEConfig&gt;(string)、ScriptableObject.CreateInstance&lt;LeonQTEConfig&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static LeonQTEConfig LoadOrCreateConfig()
    {
        // 当前里昂配对动作配置。
LeonQTEConfig config = AssetDatabase.LoadAssetAtPath<LeonQTEConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<LeonQTEConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        return config;
    }

    /// <summary>CloneClip 的业务处理入口；本方法直接依赖 AnimationEvent.AnimationEvent()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、Array.Empty&lt;AnimationEvent&gt;()、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAllAssetsAtPath(string) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant()；下游（含内部回调体） AnimationEvent.AnimationEvent()、AnimationUtility.GetAnimationClipSettings(AnimationClip)、AnimationUtility.SetAnimationClipSettings(AnimationClip, AnimationClipSettings)、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、Array.Empty&lt;AnimationEvent&gt;()、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAllAssetsAtPath(string) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourcePath">来源路径。</param>
    /// <param name="destinationPath">目的地路径。</param>
    /// <param name="loop">是否循环。</param>
    /// <param name="addBiteContact">是否添加撕咬接触。</param>
    private static AnimationClip CloneClip(string sourcePath, string destinationPath, bool loop, bool addBiteContact)
    {
        // 来源动画片段。
AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (source == null)
        {
            throw new InvalidOperationException("找不到动画片段：" + sourcePath);
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
        // 按原顺序缓存的实参目的地。
global::UnityEngine.AnimationClip argumentValue2 = destination;
        // 分支选出的空的返回结果。
global::UnityEngine.AnimationEvent[] selectedValue1;
        if (addBiteContact)
        {
            selectedValue1 = new[]
            {
                new AnimationEvent
                {
                    functionName = "OnQTEBiteContact",
                    time = destination.length * 0.42f
                }
            };
        }
        else
        {
            selectedValue1 = Array.Empty<AnimationEvent>();
        }

        AnimationUtility.SetAnimationEvents(argumentValue2, selectedValue1);
        EditorUtility.SetDirty(destination);
        return destination;
    }

    /// <summary>AddContactEvent 的业务处理入口；本方法直接依赖 AnimationEvent.AnimationEvent()、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、EditorUtility.SetDirty(Object)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.BuildKnifeVariant()；下游（含内部回调体） AnimationEvent.AnimationEvent()、AnimationUtility.SetAnimationEvents(AnimationClip, AnimationEvent[])、EditorUtility.SetDirty(Object)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">动画片段。</param>
    /// <param name="functionName">函数名称。</param>
    /// <param name="normalizedTime">动画归一化播放时间。</param>
    private static AnimationClip AddContactEvent(AnimationClip clip, string functionName, float normalizedTime)
    {
        if (clip == null)
        {
            return null;
        }

        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = functionName, time = clip.length * Mathf.Clamp01(normalizedTime) } });
        EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>SetupVillagerPrefab 的业务处理入口；本方法直接依赖 File.Exists(string)、GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） File.Exists(string)、GameObject.AddComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.AddComponent&lt;VillagerQTEParticipant&gt;()、GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;BehaviorTree&gt;()、GameObject.GetComponent&lt;EnemyQTEAnimationEventRelay&gt;()、GameObject.GetComponent&lt;PathAgent&gt;() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void SetupVillagerPrefab()
    {
        if (!System.IO.File.Exists(VillagerPrefabPath))
        {
            return;
        }

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
            participant.behaviorTree = root.GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
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

    /// <summary>SetupPromptPrefab 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;(string)、Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、EditorUtility.SetDirty(Object)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;() 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;(string)、Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、EditorUtility.SetDirty(Object)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void SetupPromptPrefab()
    {
        // 根游戏对象。
GameObject root = PrefabUtility.LoadPrefabContents(PromptPrefabPath);
        try
        {
            // 视图。
QTEPromptView view = root.GetComponent<QTEPromptView>();
            // 面板。
Transform panel = root.transform.Find("提示面板");
            if (panel == null)
            {
                panel = root.transform;
            }

            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue3 = panel.Find("快速反应按键图标");
            // 系数图标。
Image fIcon;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                fIcon = receiverValue3.GetComponent<Image>();
            }
            else
            {
                fIcon = default;
            }

            if (fIcon == null)
            {
                // 图标对象。
GameObject iconObject = new GameObject("快速反应按键图标", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(panel, false);
                // 矩形。
RectTransform rect = iconObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-150f, 25f);
                rect.sizeDelta = new Vector2(78f, 78f);
                fIcon = iconObject.GetComponent<Image>();
                fIcon.preserveAspect = true;
            }

            // 系数精灵图。
Sprite fSprite = AssetDatabase.LoadAllAssetsAtPath(PicturePath).OfType<Sprite>().FirstOrDefault( /* 中文含义：精灵图。 */sprite => sprite.name.EndsWith("_63", StringComparison.Ordinal));
            fIcon.sprite = fSprite;
            // 判空前缓存的查找的返回结果。
global::UnityEngine.Transform receiverValue5 = panel.Find("挣脱环形进度");
            // 环。
Image ring;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                ring = receiverValue5.GetComponent<Image>();
            }
            else
            {
                ring = default;
            }

            if (ring == null)
            {
                // 环对象。
GameObject ringObject = new GameObject("挣脱环形进度", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ringObject.transform.SetParent(panel, false);
                // 矩形。
RectTransform rect = ringObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-150f, 25f);
                rect.sizeDelta = new Vector2(102f, 102f);
                ring = ringObject.GetComponent<Image>();
                ring.type = Image.Type.Filled;
                ring.fillMethod = Image.FillMethod.Radial360;
                ring.fillOrigin = 2;
                ring.fillClockwise = false;
                ring.color = new Color(0.95f, 0.75f, 0.12f, 0.9f);
                ring.raycastTarget = false;
            }

            ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/素材库/界面/Grunge UI Kit/Source Files/Panels/Circle/circle1.png");
            view.keyIcon = fIcon;
            view.progressRing = ring;
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PromptPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>SetupTestScene 的业务处理入口；本方法直接依赖 EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、EditorUtility.SetDirty(Object)、Object.FindFirstObjectByType&lt;LeonQTECoordinator&gt;()、Object.FindObjectsByType&lt;LeonQTECoordinator&gt;(FindObjectsInactive, FindObjectsSortMode)、Object.FindObjectsByType&lt;QTEPromptView&gt;(FindObjectsInactive, FindObjectsSortMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()；下游（含内部回调体） EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、EditorUtility.SetDirty(Object)、Object.FindFirstObjectByType&lt;LeonQTECoordinator&gt;()、Object.FindObjectsByType&lt;LeonQTECoordinator&gt;(FindObjectsInactive, FindObjectsSortMode)、Object.FindObjectsByType&lt;QTEPromptView&gt;(FindObjectsInactive, FindObjectsSortMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="config">当前里昂配对动作配置。</param>
    private static void SetupTestScene(LeonQTEConfig config)
    {
        // 场景路径。
const string scenePath = "Assets/Scenes/QTE/LeonFrontGrabQTE_Test.unity";
        // 场景。
Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        // coordinator（当前里昂配对动作协调器）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、LeonExpandedQTESetupUtility.SetupTestScene(LeonQTEConfig)。
foreach (LeonQTECoordinator coordinator in Object.FindObjectsByType<LeonQTECoordinator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            coordinator.config = config;
            EditorUtility.SetDirty(coordinator);
        }

        // view（视图）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、LeonExpandedQTESetupUtility.SetupTestScene(LeonQTEConfig)。
foreach (QTEPromptView view in Object.FindObjectsByType<QTEPromptView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            view.coordinator = Object.FindFirstObjectByType<LeonQTECoordinator>();
            EditorUtility.SetDirty(view);
        }

        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>EnsureFolder 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonExpandedQTESetupUtility.Build()、LeonExpandedQTESetupUtility.BuildKnifeVariant()、LeonExpandedQTESetupUtility.BuildLegBiteVariant()、LeonExpandedQTESetupUtility.BuildRearBiteVariant()；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
}
