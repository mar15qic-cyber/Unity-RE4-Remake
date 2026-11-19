using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 里昂低血量受伤移动动画配置工具。
/// 从正式控制器复制生成受伤版八层控制器：
/// 索引1为受伤移动层（全身受伤动画），
/// 索引2为受伤下身层（同步受伤移动层，长枪或手枪瞄准时仅覆盖双腿）。
/// 只使用原地受伤动画，不修改第三方动画源文件。
/// 可重复运行，不会产生重复图层、状态或混合树。
/// </summary>
public static class LeonInjuredAnimationSetup
{
    /// <summary>中文含义：来源控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CopyAsset(string, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</summary>
    private const string SourceControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_New_RewriteTest.controller";
    /// <summary>中文含义：受伤控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CopyAsset(string, string) 的参数、作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数 等共 4 处。</summary>
    private const string InjuredControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_Injured.controller";
    /// <summary>中文含义：受伤遮罩路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AvatarMask&gt;(string) 的参数。</summary>
    private const string InjuredMaskPath = "Assets/Animations/遮罩/受伤移动遮罩.mask";
    /// <summary>中文含义：受伤下半身遮罩路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AvatarMask&gt;(string) 的参数。</summary>
    private const string InjuredLowerBodyMaskPath = "Assets/Animations/遮罩/受伤下身遮罩.mask";
    /// <summary>中文含义：受伤动画片段目录。编译期常量，保持既有值与消费者映射。使用位置：LeonInjuredAnimationSetup.LoadHurtClip(string)。</summary>
    private const string HurtClipDirectory = "Assets/素材库/动画/YummyGames/Art/Animations/INPLACE/Locomotion/Hurt/";
    /// <summary>中文含义：长枪装备动画片段路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</summary>
    private const string LongGunEquipClipPath = "Assets/Animations/LeonWeaponActions/EquipRifle_Anim.anim";
    /// <summary>中文含义：里昂预制件路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string) 的参数、作为 Debug.Log(object) 的参数、作为 Debug.LogWarning(object) 的参数 等共 5 处。</summary>
    private const string LeonPrefabPath = "Assets/Prefab/LeonR4R.prefab";
    /// <summary>中文含义：受伤图层名称。编译期常量，保持既有值与消费者映射。使用位置：作为 List&lt;AnimatorControllerLayer&gt;.FindIndex(Predicate&lt;AnimatorControllerLayer&gt;) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。</summary>
    private const string InjuredLayerName = "Injured Movement";
    /// <summary>中文含义：受伤下半身图层名称。编译期常量，保持既有值与消费者映射。使用位置：作为 List&lt;AnimatorControllerLayer&gt;.FindIndex(Predicate&lt;AnimatorControllerLayer&gt;) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。</summary>
    private const string InjuredLowerBodyLayerName = "Injured Lower Body";
    /// <summary>中文含义：旧版受伤上部恢复图层名称。编译期常量，保持既有值与消费者映射。使用位置：作为 List&lt;AnimatorControllerLayer&gt;.FindIndex(Predicate&lt;AnimatorControllerLayer&gt;) 的参数。</summary>
    private const string LegacyInjuredUpperRestoreLayerName = "Injured Upper Restore";
    // 八层契约（顺序即索引）
    /// <summary>中文含义：预期图层名称列表。只读字段，初始化后不由普通写入口替换。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。</summary>
    private static readonly string[] ExpectedLayerNames =
    {
        "Base Movement",
        "Injured Movement",
        "Injured Lower Body",
        "Rifle Crouch Upper",
        "Aim Offset",
        "Fire Additive",
        "Weapon Action",
        "Full Body Action"
    };
    /// <summary>中文含义：待机动画状态名称。编译期常量，保持既有值与消费者映射。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController) 等共 5 处。</summary>
    private const string IdleStateName = "HurtIdle";
    /// <summary>中文含义：行走动画状态名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)、LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。</summary>
    private const string WalkStateName = "HurtWalk";
    /// <summary>中文含义：慢跑动画状态名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)、LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。</summary>
    private const string JogStateName = "HurtJog";
    /// <summary>中文含义：转向动画状态名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)、LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。</summary>
    private const string TurnStateName = "HurtTurnInPlace";
    /// <summary>中文含义：行走行为树名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的参数、LeonInjuredAnimationSetup.ConfigureWalkTree(BlendTree)。</summary>
    private const string WalkTreeName = "HurtWalkDirections";
    /// <summary>中文含义：慢跑行为树名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的参数、LeonInjuredAnimationSetup.ConfigureJogTree(BlendTree)。</summary>
    private const string JogTreeName = "HurtJogDirections";
    /// <summary>中文含义：转向行为树名称。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的参数、LeonInjuredAnimationSetup.ConfigureTurnTree(BlendTree)。</summary>
    private const string TurnTreeName = "HurtTurnSelector";
    // 过渡时长：待机与移动 0.18，待机与转向 0.12
    /// <summary>中文含义：待机移动过渡时长。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</summary>
    private const float IdleMovementTransitionDuration = 0.18f;
    // 受伤步行到慢跑的过渡时长
    /// <summary>中文含义：行走目标慢跑过渡时长。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</summary>
    private const float WalkToJogTransitionDuration = 0.18f;
    // 受伤慢跑到步行的过渡时长
    /// <summary>中文含义：慢跑目标行走过渡时长。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</summary>
    private const float JogToWalkTransitionDuration = 0.20f;
    /// <summary>中文含义：待机转向过渡时长。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</summary>
    private const float IdleTurnTransitionDuration = 0.12f;
    // 受伤移动速度（写入 LeonLocomotion）
    /// <summary>中文含义：受伤行走前向速度。编译期常量，保持既有值与消费者映射。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。</summary>
    private const float InjuredWalkForwardSpeed = 1.35f;
    /// <summary>中文含义：受伤行走后退速度。编译期常量，保持既有值与消费者映射。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。</summary>
    private const float InjuredWalkBackwardSpeed = 1.10f;
    /// <summary>中文含义：受伤横移速度。编译期常量，保持既有值与消费者映射。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。</summary>
    private const float InjuredStrafeSpeed = 0.95f;
    /// <summary>中文含义：受伤慢跑速度。编译期常量，保持既有值与消费者映射。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。</summary>
    private const float InjuredJogSpeed = 2.35f;
    // 方向位置（单位圆，斜向为归一化分量）
    /// <summary>中文含义：对角方向。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonInjuredAnimationSetup.CreateDirectionalChild(string, float, float, float) 的参数。</summary>
    private const float Diagonal = 0.707f;
    /// <summary>编辑器菜单调用入口 Build；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、Debug.LogError(object)、EditorUtility.SetDirty(Object)、LeonInjuredAnimationSetup.ApplyControllerToPrefabAndScene(AnimatorController)、LeonInjuredAnimationSetup.ApplyInjuredSpeeds() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/配置低血量受伤动画")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[里昂受伤动画] 运行模式下禁止执行配置工具。");
            return;
        }

        // injuredMask（受伤遮罩）：保存 LeonInjuredAnimationSetup.EnsureInjuredMask() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask) 的参数。
AvatarMask injuredMask = EnsureInjuredMask();
        // lowerBodyMask（下半身遮罩）：保存 LeonInjuredAnimationSetup.EnsureInjuredLowerBodyMask() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask) 的参数。
AvatarMask lowerBodyMask = EnsureInjuredLowerBodyMask();
        // controller（动画控制器资源）：保存 LeonInjuredAnimationSetup.EnsureController() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonInjuredAnimationSetup.ApplyControllerToPrefabAndScene(AnimatorController) 的参数、作为 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController) 的参数 等共 9 处。
AnimatorController controller = EnsureController();
        EnsureInjuredLayers(controller, injuredMask, lowerBodyMask);
        ConfigureLongGunEquipMotion(controller);
        ConfigureInjuredLowerBodyOverrides(controller);
        BuildInjuredStateMachine(controller);
        CalibrateDirectionalPhaseOffsets(controller);
        ValidateLongGunEquipMotion(controller);
        ValidateResult(controller);
        EditorUtility.SetDirty(injuredMask);
        EditorUtility.SetDirty(lowerBodyMask);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ApplyInjuredSpeeds();
        ApplyControllerToPrefabAndScene(controller);
        Debug.Log("[里昂受伤动画] 配置完成：八层受伤控制器（含下身同步层）已生成并应用。");
    }

    /// <summary>
    /// 创建或刷新受伤移动遮罩：全身受伤表演。
    /// 启用躯干、头部、双腿、双臂和双脚反向动力学；
    /// 关闭根、手指和双手反向动力学（手指继承基础层握持形状，武器仍附着在手骨）。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;AvatarMask&gt;(string)、AvatarMask.AvatarMask()、AvatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static AvatarMask EnsureInjuredMask()
    {
        // mask（遮罩）：保存 AssetDatabase.LoadAssetAtPath<AvatarMask>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、LeonInjuredAnimationSetup.EnsureInjuredMask()。
AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(InjuredMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, InjuredMaskPath);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        return mask;
    }

    /// <summary>
    /// 创建或刷新受伤下身遮罩：只开启双腿和双脚反向动力学。
    /// Root、Body、Head、双臂、手指和双手反向动力学全部关闭。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;AvatarMask&gt;(string)、AvatarMask.AvatarMask()、AvatarMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static AvatarMask EnsureInjuredLowerBodyMask()
    {
        // mask（遮罩）：保存 AssetDatabase.LoadAssetAtPath<AvatarMask>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLowerBodyMask()。
AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(InjuredLowerBodyMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, InjuredLowerBodyMaskPath);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        return mask;
    }

    /// <summary>
    /// 确保受伤控制器存在：不存在时从正式控制器复制生成。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AssetDatabase.CopyAsset(string, string)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.Refresh()、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static AnimatorController EnsureController()
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.EnsureController()。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(InjuredControllerPath);
        if (controller != null)
        {
            return controller;
        }

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath) == null)
        {
            throw new InvalidOperationException("找不到源控制器：" + SourceControllerPath);
        }

        if (!AssetDatabase.CopyAsset(SourceControllerPath, InjuredControllerPath))
        {
            throw new InvalidOperationException("复制控制器失败：" + InjuredControllerPath);
        }

        AssetDatabase.Refresh();
        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(InjuredControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("复制后无法加载控制器：" + InjuredControllerPath);
        }

        return controller;
    }

    /// <summary>
    /// 确保两个受伤层存在并按八层契约重排全部图层。
    /// 受伤移动层：覆盖模式，默认权重零，携带独立状态机。
    /// 受伤下身层：覆盖模式，默认权重零，同步受伤移动层（syncedLayerIndex=1），
    /// 只开启双腿与双脚反向动力学，不创建独立状态机。
    /// 重复执行时复用现有层，处理旧方案 Injured Upper Restore 的迁移。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorStateMachine.AnimatorStateMachine()、AssetDatabase.AddObjectToAsset(Object, Object)、Dictionary&lt;string, AnimatorControllerLayer&gt;.ContainsKey(string)、Dictionary&lt;string, AnimatorControllerLayer&gt;.Dictionary()、Dictionary&lt;string, AnimatorControllerLayer&gt;.Remove(string)、IEnumerable&lt;AnimatorControllerLayer&gt;.ToList&lt;AnimatorControllerLayer&gt;() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。</param>
    /// <param name="injuredMask">中文含义：受伤遮罩。使用位置：LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。</param>
    /// <param name="lowerBodyMask">中文含义：下半身遮罩。使用位置：LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。</param>
    private static void EnsureInjuredLayers(AnimatorController controller, AvatarMask injuredMask, AvatarMask lowerBodyMask)
    {
        // layerList（图层列表）：保存 IEnumerable<AnimatorControllerLayer>.ToList<AnimatorControllerLayer>() 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
List<AnimatorControllerLayer> layerList = controller.layers.ToList();
        // 迁移：旧方案的 Injured Upper Restore 重命名为 Injured Lower Body
        // legacyIndex（旧版索引）：保存 List<AnimatorControllerLayer>.FindIndex(Predicate<AnimatorControllerLayer>) 的返回结果，供当前步骤使用。使用位置：作为 List<AnimatorControllerLayer>.this[int] 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
int legacyIndex = layerList.FindIndex( /* 中文含义：图层。 */layer => layer.name == LegacyInjuredUpperRestoreLayerName);
        if (legacyIndex >= 0)
        {
            layerList[legacyIndex] = new AnimatorControllerLayer
            {
                name = InjuredLowerBodyLayerName,
                defaultWeight = 0f,
                avatarMask = lowerBodyMask,
                iKPass = false,
                blendingMode = AnimatorLayerBlendingMode.Override,
                syncedLayerIndex = AnimatorLayerID.Injured,
                stateMachine = null
            };
        }

        // 去重：Injured Lower Body 只允许存在一个
        // firstLowerIndex（首个下部索引）：保存 List<AnimatorControllerLayer>.FindIndex(Predicate<AnimatorControllerLayer>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
int firstLowerIndex = layerList.FindIndex( /* 中文含义：图层。 */layer => layer.name == InjuredLowerBodyLayerName);
        if (firstLowerIndex >= 0)
        {
            // i（当前遍历索引）：使用位置：作为 List<AnimatorControllerLayer>.RemoveAt(int) 的参数、作为 List<AnimatorControllerLayer>.this[int] 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
for (int i = layerList.Count - 1; i > firstLowerIndex; i--)
            {
                if (layerList[i].name == InjuredLowerBodyLayerName)
                {
                    layerList.RemoveAt(i);
                }
            }
        }

        // 受伤移动层：沿用或新建
        // injuredIndex（受伤索引）：保存 List<AnimatorControllerLayer>.FindIndex(Predicate<AnimatorControllerLayer>) 的返回结果，供当前步骤使用。使用位置：作为 List<AnimatorControllerLayer>.RemoveAt(int) 的参数、作为 List<AnimatorControllerLayer>.this[int] 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
int injuredIndex = layerList.FindIndex( /* 中文含义：图层。 */layer => layer.name == InjuredLayerName);
        // injuredLayer（受伤图层）：使用位置：作为 List<AnimatorControllerLayer>.Insert(int, AnimatorControllerLayer) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
AnimatorControllerLayer injuredLayer;
        if (injuredIndex < 0)
        {
            // machine（状态机）：使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
AnimatorStateMachine machine = new AnimatorStateMachine
            {
                name = InjuredLayerName
            };
            AssetDatabase.AddObjectToAsset(machine, controller);
            injuredLayer = new AnimatorControllerLayer
            {
                name = InjuredLayerName,
                defaultWeight = 0f,
                avatarMask = injuredMask,
                iKPass = false,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = machine
            };
        }
        else
        {
            injuredLayer = layerList[injuredIndex];
            layerList.RemoveAt(injuredIndex);
        }

        injuredLayer.defaultWeight = 0f;
        injuredLayer.avatarMask = injuredMask;
        injuredLayer.iKPass = false;
        injuredLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        if (injuredLayer.stateMachine == null)
        {
            throw new InvalidOperationException("受伤移动层缺少状态机，请删除控制器后重新运行。");
        }

        // 受伤下身层：沿用或新建（同步受伤移动层，无独立状态机）
        // lowerIndex（下部索引）：保存 List<AnimatorControllerLayer>.FindIndex(Predicate<AnimatorControllerLayer>) 的返回结果，供当前步骤使用。使用位置：作为 List<AnimatorControllerLayer>.RemoveAt(int) 的参数、作为 List<AnimatorControllerLayer>.this[int] 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
int lowerIndex = layerList.FindIndex( /* 中文含义：图层。 */layer => layer.name == InjuredLowerBodyLayerName);
        // lowerLayer（下部图层）：使用位置：作为 List<AnimatorControllerLayer>.Insert(int, AnimatorControllerLayer) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
AnimatorControllerLayer lowerLayer;
        if (lowerIndex >= 0)
        {
            lowerLayer = layerList[lowerIndex];
            layerList.RemoveAt(lowerIndex);
        }
        else
        {
            lowerLayer = new AnimatorControllerLayer
            {
                name = InjuredLowerBodyLayerName,
                defaultWeight = 0f,
                avatarMask = lowerBodyMask,
                iKPass = false,
                blendingMode = AnimatorLayerBlendingMode.Override,
                syncedLayerIndex = AnimatorLayerID.Injured,
                stateMachine = null
            };
        }

        lowerLayer.defaultWeight = 0f;
        lowerLayer.avatarMask = lowerBodyMask;
        lowerLayer.iKPass = false;
        lowerLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        lowerLayer.syncedLayerIndex = AnimatorLayerID.Injured;
        lowerLayer.stateMachine = null;
        // 按八层契约重排
        layerList.Insert(AnimatorLayerID.Injured, injuredLayer);
        layerList.Insert(AnimatorLayerID.InjuredLowerBody, lowerLayer);
        // byName（依据名称）：使用位置：作为 List<AnimatorControllerLayer>.Add(AnimatorControllerLayer) 的参数、作为 string.Join(string, IEnumerable<string>) 的参数、LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
var byName = new Dictionary<string, AnimatorControllerLayer>();
        // layer（图层）：每轮从图层列表取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<string, AnimatorControllerLayer>.ContainsKey(string) 的参数、作为 Dictionary<string, AnimatorControllerLayer>.this[string] 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数 等共 4 处。
foreach (AnimatorControllerLayer layer in layerList)
        {
            if (byName.ContainsKey(layer.name))
            {
                throw new InvalidOperationException("存在重名图层：" + layer.name);
            }

            byName[layer.name] = layer;
        }

        // ordered（已排序）：使用位置：LeonInjuredAnimationSetup.EnsureInjuredLayers(AnimatorController, AvatarMask, AvatarMask)。
var ordered = new List<AnimatorControllerLayer>();
        // layerName（图层名称）：每轮从预期图层名称列表取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<string, AnimatorControllerLayer>.ContainsKey(string) 的参数、作为 Dictionary<string, AnimatorControllerLayer>.Remove(string) 的参数、作为 Dictionary<string, AnimatorControllerLayer>.this[string] 的参数 等共 4 处。
foreach (string layerName in ExpectedLayerNames)
        {
            if (!byName.ContainsKey(layerName))
            {
                throw new InvalidOperationException("控制器缺少图层：" + layerName);
            }

            ordered.Add(byName[layerName]);
            byName.Remove(layerName);
        }

        if (byName.Count > 0)
        {
            throw new InvalidOperationException("控制器存在未预期的图层：" + string.Join(",", byName.Keys));
        }

        controller.layers = ordered.ToArray();
    }

    /// <summary>ConfigureInjuredLowerBodyOverrides 的业务处理入口；本方法直接依赖 AnimatorController.SetStateEffectiveMotion(AnimatorState, Motion, int)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AnimatorController.SetStateEffectiveMotion(AnimatorState, Motion, int)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。</param>
    private static void ConfigureInjuredLowerBodyOverrides(AnimatorController controller)
    {
        // stateNames（状态名称列表）：使用位置：LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。
string[] stateNames =
        {
            IdleStateName,
            WalkStateName,
            JogStateName,
            TurnStateName
        };
        // stateName（动画状态名称）：每轮从状态名称列表取得一个元素，仅代表当前这一项。使用位置：作为 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。
foreach (string stateName in stateNames)
        {
            // sourceMachine（来源状态机）：使用位置：LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。
AnimatorStateMachine sourceMachine = controller.layers[AnimatorLayerID.Injured].stateMachine;
            if (sourceMachine == null)
            {
                throw new InvalidOperationException("受伤移动层缺少状态机，无法配置下身动作。");
            }

            // state（当前动画状态）：保存 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorController.SetStateEffectiveMotion(AnimatorState, Motion, int) 的参数、LeonInjuredAnimationSetup.ConfigureInjuredLowerBodyOverrides(AnimatorController)。
AnimatorState state = sourceMachine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：候选。 */candidate => candidate.name == stateName);
            if (state == null || state.motion == null)
            {
                throw new InvalidOperationException("受伤移动层存在空动作状态：" + stateName);
            }

            // sourceMotion（来源运动）：使用位置：作为 AnimatorController.SetStateEffectiveMotion(AnimatorState, Motion, int) 的参数。
Motion sourceMotion = state.motion;
            controller.SetStateEffectiveMotion(state, sourceMotion, AnimatorLayerID.InjuredLowerBody);
        }
    }

    /// <summary>ConfigureLongGunEquipMotion 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、InvalidOperationException.InvalidOperationException(string)、Mathf.Approximately(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、EditorUtility.SetDirty(Object)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、InvalidOperationException.InvalidOperationException(string)、Mathf.Approximately(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。</param>
    private static void ConfigureLongGunEquipMotion(AnimatorController controller)
    {
        // equipClip（装备动画片段）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
AnimationClip equipClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongGunEquipClipPath);
        if (equipClip == null)
        {
            throw new InvalidOperationException("找不到真正的长枪掏枪动作：" + LongGunEquipClipPath);
        }

        // actionLayer（动作图层）：使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
AnimatorControllerLayer actionLayer = controller.layers[AnimatorLayerID.WeaponAction];
        // equipState（装备状态）：保存 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
AnimatorState equipState = actionLayer.stateMachine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == "Equip");
        if (equipState == null)
        {
            throw new InvalidOperationException("武器动作层缺少装备状态。");
        }

        // selector（选择器）：使用位置：作为 EditorUtility.SetDirty(Object) 的参数、LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
BlendTree selector = equipState.motion as BlendTree;
        if (selector == null || selector.blendParameter != "WeaponType")
        {
            throw new InvalidOperationException("装备状态缺少武器类型混合树。");
        }

        // children（子对象集合）：使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
ChildMotion[] children = selector.children;
        // rifleMapped（是否步枪映射后的）：使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
bool rifleMapped = false;
        // shotgunMapped（是否霰弹枪映射后的）：使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
bool shotgunMapped = false;
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.ConfigureLongGunEquipMotion(AnimatorController)。
for (int i = 0; i < children.Length; i++)
        {
            if (Mathf.Approximately(children[i].threshold, 1f))
            {
                children[i].motion = equipClip;
                rifleMapped = true;
            }
            else if (Mathf.Approximately(children[i].threshold, 3f))
            {
                children[i].motion = equipClip;
                shotgunMapped = true;
            }
        }

        if (!rifleMapped || !shotgunMapped)
        {
            throw new InvalidOperationException("装备混合树缺少步枪或霰弹枪分支。");
        }

        selector.children = children;
        EditorUtility.SetDirty(selector);
    }

    /// <summary>ValidateLongGunEquipMotion 的业务处理入口；本方法直接依赖 AnimationUtility.GetAnimationEvents(AnimationClip)、AnimationUtility.GetCurveBindings(AnimationClip)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、IEnumerable&lt;AnimationEvent&gt;.FirstOrDefault&lt;AnimationEvent&gt;(Func&lt;AnimationEvent, bool&gt;)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、IEnumerable&lt;ChildMotion&gt;.FirstOrDefault&lt;ChildMotion&gt;(Func&lt;ChildMotion, bool&gt;) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AnimationUtility.GetAnimationEvents(AnimationClip)、AnimationUtility.GetCurveBindings(AnimationClip)、AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、IEnumerable&lt;AnimationEvent&gt;.FirstOrDefault&lt;AnimationEvent&gt;(Func&lt;AnimationEvent, bool&gt;)、IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)、IEnumerable&lt;ChildMotion&gt;.FirstOrDefault&lt;ChildMotion&gt;(Func&lt;ChildMotion, bool&gt;) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。</param>
    private static void ValidateLongGunEquipMotion(AnimatorController controller)
    {
        // expected（预期）：保存 AssetDatabase.LoadAssetAtPath<AnimationClip>(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.GetAnimationEvents(AnimationClip) 的参数、作为 AnimationUtility.GetCurveBindings(AnimationClip) 的参数、LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimationClip expected = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongGunEquipClipPath);
        if (expected == null)
        {
            throw new InvalidOperationException("长枪装备过渡动作未生成。");
        }

        if (AnimationUtility.GetCurveBindings(expected).Length == 0)
        {
            throw new InvalidOperationException("长枪装备过渡动作没有动画曲线。");
        }

        // events（事件集合）：保存 AnimationUtility.GetAnimationEvents(AnimationClip) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimationEvent[] events = AnimationUtility.GetAnimationEvents(expected);
        // backHide（后方隐藏）：保存 IEnumerable<AnimationEvent>.FirstOrDefault<AnimationEvent>(Func<AnimationEvent, bool>) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimationEvent backHide = events.FirstOrDefault( /* 中文含义：动画事件。 */animationEvent => animationEvent.functionName == "OnRifleBackHideBeforeReveal");
        // visualReveal（视觉表现显露）：保存 IEnumerable<AnimationEvent>.FirstOrDefault<AnimationEvent>(Func<AnimationEvent, bool>) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimationEvent visualReveal = events.FirstOrDefault( /* 中文含义：动画事件。 */animationEvent => animationEvent.functionName == "OnEquipVisualReveal");
        // supportHandGrip（支撑手部握持）：保存 IEnumerable<AnimationEvent>.FirstOrDefault<AnimationEvent>(Func<AnimationEvent, bool>) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimationEvent supportHandGrip = events.FirstOrDefault( /* 中文含义：动画事件。 */animationEvent => animationEvent.functionName == "OnLongGunSupportHandGrip");
        if (backHide == null || visualReveal == null || supportHandGrip == null)
        {
            throw new InvalidOperationException("长枪装备过渡必须包含背部隐藏、视觉显示和副手握把三个事件。");
        }

        if (!(backHide.time < visualReveal.time && visualReveal.time < supportHandGrip.time))
        {
            throw new InvalidOperationException("长枪装备事件顺序必须为背部隐藏、视觉显示、副手握把。");
        }

        if (!Mathf.Approximately(backHide.time, 0.05f) || !Mathf.Approximately(visualReveal.time, 0.3166667f) || !Mathf.Approximately(supportHandGrip.time, 1.05f))
        {
            throw new InvalidOperationException("长枪装备事件时间不匹配，期望为0.05、0.3166667和1.05秒。");
        }

        // equipState（装备状态）：保存 IEnumerable<AnimatorState>.FirstOrDefault<AnimatorState>(Func<AnimatorState, bool>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
AnimatorState equipState = controller.layers[AnimatorLayerID.WeaponAction].stateMachine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == "Equip");
        // selector（选择器）：使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
BlendTree selector;
        if (equipState != null)
        {
            selector = equipState.motion as BlendTree;
        }
        else
        {
            selector = null;
        }

        if (selector == null)
        {
            throw new InvalidOperationException("装备状态缺少有效混合树。");
        }

        // rifle（步枪）：保存 IEnumerable<ChildMotion>.FirstOrDefault<ChildMotion>(Func<ChildMotion, bool>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
ChildMotion rifle = selector.children.FirstOrDefault( /* 中文含义：子级。 */child => Mathf.Approximately(child.threshold, 1f));
        // shotgun（霰弹枪）：保存 IEnumerable<ChildMotion>.FirstOrDefault<ChildMotion>(Func<ChildMotion, bool>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ValidateLongGunEquipMotion(AnimatorController)。
ChildMotion shotgun = selector.children.FirstOrDefault( /* 中文含义：子级。 */child => Mathf.Approximately(child.threshold, 3f));
        if (rifle.motion != expected || shotgun.motion != expected)
        {
            throw new InvalidOperationException("步枪或霰弹枪仍引用了无效的旧装备动作。");
        }
    }

    /// <summary>
    /// 构建受伤层状态机：复用已有状态与混合树，清空并重建全部转换。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AnimatorStateMachine.RemoveState(AnimatorState)、IEnumerable&lt;ChildAnimatorState&gt;.ToArray&lt;ChildAnimatorState&gt;()、IEnumerable&lt;string&gt;.Contains&lt;string&gt;(string)、LeonInjuredAnimationSetup.ConfigureJogTree(BlendTree)、LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool)、LeonInjuredAnimationSetup.ConfigureTurnTree(BlendTree)、LeonInjuredAnimationSetup.ConfigureWalkTree(BlendTree) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。</param>
    private static void BuildInjuredStateMachine(AnimatorController controller)
    {
        // machine（状态机）：使用位置：作为 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的参数、作为 LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
AnimatorStateMachine machine = controller.layers[AnimatorLayerID.Injured].stateMachine;
        // 移除不属于本工具的历史残留状态
        // expectedStateNames（预期状态名称列表）：使用位置：LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
string[] expectedStateNames =
        {
            IdleStateName,
            WalkStateName,
            JogStateName,
            TurnStateName
        };
        // child（子级）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveState(AnimatorState) 的参数、作为 IEnumerable<string>.Contains<string>(string) 的参数、作为 LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState) 的参数。
foreach (ChildAnimatorState child in machine.states.ToArray())
        {
            if (expectedStateNames.Contains(child.state.name))
            {
                continue;
            }

            RemoveIncomingTransitions(machine, child.state);
            machine.RemoveState(child.state);
        }

        // walkTree（行走行为树）：保存 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.ConfigureWalkTree(BlendTree) 的参数。
BlendTree walkTree = FindOrCreateTree(controller, WalkTreeName);
        ConfigureWalkTree(walkTree);
        // jogTree（慢跑行为树）：保存 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureJogTree(BlendTree) 的参数、作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数。
BlendTree jogTree = FindOrCreateTree(controller, JogTreeName);
        ConfigureJogTree(jogTree);
        // turnTree（转向行为树）：保存 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.ConfigureTurnTree(BlendTree) 的参数。
BlendTree turnTree = FindOrCreateTree(controller, TurnTreeName);
        ConfigureTurnTree(turnTree);
        // idle（待机）：保存 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
AnimatorState idle = EnsureState(machine, IdleStateName, new Vector3(260f, 0f));
        ConfigureState(idle, LoadHurtClip("IP_Hurt_Idle"), true);
        // walk（行走）：保存 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
AnimatorState walk = EnsureState(machine, WalkStateName, new Vector3(560f, 130f));
        ConfigureState(walk, walkTree, true);
        // jog（慢跑）：保存 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
AnimatorState jog = EnsureState(machine, JogStateName, new Vector3(560f, 270f));
        // 慢跑同样启用脚部反向动力学，减轻人形重定向后的脚底漂移与骨盆摇摆
        ConfigureState(jog, jogTree, true);
        // turn（转向）：保存 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool) 的参数、作为 LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState) 的参数、LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)。
AnimatorState turn = EnsureState(machine, TurnStateName, new Vector3(260f, 300f));
        ConfigureState(turn, turnTree, true);
        machine.defaultState = idle;
        // state（当前动画状态）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonInjuredAnimationSetup.RemoveStateTransitions(AnimatorState) 的参数。
foreach (AnimatorState state in new[]
        {
            idle,
            walk,
            jog,
            turn
        }

        )
        {
            RemoveStateTransitions(state);
        }

        RebuildTransitions(idle, walk, jog, turn);
    }

    /// <summary>
    /// 重建受伤层全部转换：所有过渡关闭退出时间，使用固定时长。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="idle">中文含义：待机。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</param>
    /// <param name="walk">中文含义：行走。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</param>
    /// <param name="jog">中文含义：慢跑。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</param>
    /// <param name="turn">中文含义：转向。使用位置：作为 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的参数。</param>
    private static void RebuildTransitions(AnimatorState idle, AnimatorState walk, AnimatorState jog, AnimatorState turn)
    {
        // 待机 → 步行：有移动输入且未按跑步
        // idleToWalk（待机目标行走）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition idleToWalk = CreateTransition(idle, walk, IdleMovementTransitionDuration);
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.5f, "InputMagnitude");
        idleToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprint");
        // 待机 → 慢跑：按住跑步且有移动输入
        // idleToJog（待机目标慢跑）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition idleToJog = CreateTransition(idle, jog, IdleMovementTransitionDuration);
        idleToJog.AddCondition(AnimatorConditionMode.Greater, 0.1f, "InputMagnitude");
        idleToJog.AddCondition(AnimatorConditionMode.If, 0f, "Sprint");
        // 待机 → 原地转向（左右各一条）
        // idleToTurnRight（待机目标转向右侧）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition idleToTurnRight = CreateTransition(idle, turn, IdleTurnTransitionDuration);
        idleToTurnRight.AddCondition(AnimatorConditionMode.Greater, 6f, "TurnAngle");
        // idleToTurnLeft（待机目标转向左侧）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition idleToTurnLeft = CreateTransition(idle, turn, IdleTurnTransitionDuration);
        idleToTurnLeft.AddCondition(AnimatorConditionMode.Less, -6f, "TurnAngle");
        // 步行 ↔ 慢跑（步行→慢跑 0.18，慢跑→步行 0.20）
        // walkToJog（行走目标慢跑）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition walkToJog = CreateTransition(walk, jog, WalkToJogTransitionDuration);
        walkToJog.AddCondition(AnimatorConditionMode.If, 0f, "Sprint");
        // jogToWalk（慢跑目标行走）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition jogToWalk = CreateTransition(jog, walk, JogToWalkTransitionDuration);
        jogToWalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprint");
        // 移动 → 待机
        // walkToIdle（行走目标待机）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition walkToIdle = CreateTransition(walk, idle, IdleMovementTransitionDuration);
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "InputMagnitude");
        // jogToIdle（慢跑目标待机）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition jogToIdle = CreateTransition(jog, idle, IdleMovementTransitionDuration);
        jogToIdle.AddCondition(AnimatorConditionMode.Less, 0.5f, "InputMagnitude");
        jogToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sprint");
        // 转向 → 待机：转向角回到小角度
        // turnToIdle（转向目标待机）：保存 LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)。
AnimatorStateTransition turnToIdle = CreateTransition(turn, idle, IdleTurnTransitionDuration);
        turnToIdle.AddCondition(AnimatorConditionMode.Less, 2f, "TurnAngle");
        turnToIdle.AddCondition(AnimatorConditionMode.Greater, -2f, "TurnAngle");
    }

    /// <summary>CreateTransition 的业务处理入口；本方法直接依赖 AnimatorState.AddTransition(AnimatorState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.RebuildTransitions(AnimatorState, AnimatorState, AnimatorState, AnimatorState)；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画状态。使用位置：LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float)。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float)。</param>
    private static AnimatorStateTransition CreateTransition(AnimatorState source, AnimatorState destination, float duration)
    {
        // transition（过渡）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.CreateTransition(AnimatorState, AnimatorState, float)。
AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.offset = 0f;
        transition.canTransitionToSelf = false;
        return transition;
    }

    /// <summary>ConfigureState 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool)。</param>
    /// <param name="motion">中文含义：运动。使用位置：LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool)。</param>
    /// <param name="footIk">中文含义：是否脚部反向动力学。使用位置：LeonInjuredAnimationSetup.ConfigureState(AnimatorState, Motion, bool)。</param>
    private static void ConfigureState(AnimatorState state, Motion motion, bool footIk)
    {
        state.motion = motion;
        state.speed = 1f;
        state.writeDefaultValues = false;
        state.iKOnFeet = footIk;
    }

    /// <summary>
    /// 八方向受伤步行混合树。前进、后退、横移和斜向分别匹配目标速度，
    /// 避免共用倍率导致横移过慢或斜向步频失真。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） LeonInjuredAnimationSetup.CreateDirectionalChild(string, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tree">中文含义：行为树。使用位置：LeonInjuredAnimationSetup.ConfigureWalkTree(BlendTree)。</param>
    private static void ConfigureWalkTree(BlendTree tree)
    {
        tree.name = WalkTreeName;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "Horizontal";
        tree.blendParameterY = "Vertical";
        tree.children = new[]
        {
            CreateDirectionalChild("IP_Walk_Hurt_Front_Loop", 0f, 1f, 3.277f),
            CreateDirectionalChild("IP_Walk_Hurt_Back_Loop", 0f, -1f, 2.670f),
            CreateDirectionalChild("IP_Walk_Hurt_Left", -1f, 0f, 3.532f),
            CreateDirectionalChild("IP_Walk_Hurt_Right", 1f, 0f, 3.532f),
            CreateDirectionalChild("IP_Walk_Hurt_Front_Left", -Diagonal, Diagonal, 4.016f),
            CreateDirectionalChild("IP_Walk_Hurt_Front_Right", Diagonal, Diagonal, 4.016f),
            CreateDirectionalChild("IP_Walk_Hurt_Back_Left", -Diagonal, -Diagonal, 3.536f),
            CreateDirectionalChild("IP_Walk_Hurt_Back_Right", Diagonal, -Diagonal, 3.536f)
        };
    }

    /// <summary>
    /// 八方向受伤慢跑混合树。所有方向统一匹配每秒 2.35 米，
    /// 按各原始片段的实际步幅分别计算播放倍率。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） LeonInjuredAnimationSetup.CreateDirectionalChild(string, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tree">中文含义：行为树。使用位置：LeonInjuredAnimationSetup.ConfigureJogTree(BlendTree)。</param>
    private static void ConfigureJogTree(BlendTree tree)
    {
        tree.name = JogTreeName;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "Horizontal";
        tree.blendParameterY = "Vertical";
        tree.children = new[]
        {
            CreateDirectionalChild("IP_Jog_Hurt_Front_Loop", 0f, 1f, 2.113f),
            CreateDirectionalChild("IP_Jog_Hurt_Back_Loop", 0f, -1f, 2.113f),
            CreateDirectionalChild("IP_Jog_Hurt_Left", -1f, 0f, 2.119f),
            CreateDirectionalChild("IP_Jog_Hurt_Right", 1f, 0f, 2.119f),
            CreateDirectionalChild("IP_Jog_Hurt_Front_Left", -Diagonal, Diagonal, 1.858f),
            CreateDirectionalChild("IP_Jog_Hurt_Front_Right", Diagonal, Diagonal, 1.858f),
            CreateDirectionalChild("IP_Jog_Hurt_Back_Left", -Diagonal, -Diagonal, 1.858f),
            CreateDirectionalChild("IP_Jog_Hurt_Back_Right", Diagonal, -Diagonal, 1.858f)
        };
    }

    /// <summary>
    /// 原地转向混合树：九十度转向倍率 2.35，一百八十度转向倍率 1.55，
    /// 中心用受伤待机占位避免左右转向互相混合。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） LeonInjuredAnimationSetup.CreateThresholdChild(string, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tree">中文含义：行为树。使用位置：LeonInjuredAnimationSetup.ConfigureTurnTree(BlendTree)。</param>
    private static void ConfigureTurnTree(BlendTree tree)
    {
        tree.name = TurnTreeName;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "TurnAngle";
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            CreateThresholdChild("IP_Turn_In_Place_180_Left", -180f, 1.55f),
            CreateThresholdChild("IP_Hurt_Turn_In_Place_90_Left", -90f, 2.35f),
            CreateThresholdChild("IP_Hurt_Idle", 0f, 1f),
            CreateThresholdChild("IP_Hurt_Turn_In_Place_90_Right", 90f, 2.35f),
            CreateThresholdChild("IP_Turn_In_Place_180_Right", 180f, 1.55f)
        };
    }

    /// <summary>CreateDirectionalChild 的业务处理入口；本方法直接依赖 ChildMotion.ChildMotion()、LeonInjuredAnimationSetup.LoadHurtClip(string)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ConfigureJogTree(BlendTree)、LeonInjuredAnimationSetup.ConfigureWalkTree(BlendTree)；下游（含内部回调体） ChildMotion.ChildMotion()、LeonInjuredAnimationSetup.LoadHurtClip(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 LeonInjuredAnimationSetup.LoadHurtClip(string) 的参数。</param>
    /// <param name="x">中文含义：横向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="timeScale">中文含义：时间缩放。使用位置：LeonInjuredAnimationSetup.CreateDirectionalChild(string, float, float, float)。</param>
    private static ChildMotion CreateDirectionalChild(string clipName, float x, float y, float timeScale)
    {
        return new ChildMotion
        {
            motion = LoadHurtClip(clipName),
            position = new Vector2(x, y),
            timeScale = timeScale
        };
    }

    // ================================================================
    // 八方向脚步相位校准
    // ================================================================
    /// <summary>
    /// 校准八方向受伤步行与慢跑的脚步相位：
    /// 用里昂骨架采样左右脚相对髋部的高度差信号，
    /// 以前进动画为参考做循环互相关，把最小误差相位偏移写入 cycleOffset。
    /// 重复运行时基于原始信号重新计算，结果一致，不会累加偏移。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、GameObject.GetComponent&lt;Animator&gt;()、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)、LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的参数。</param>
    private static void CalibrateDirectionalPhaseOffsets(AnimatorController controller)
    {
        // rigPrefab（绑定预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.CalibrateDirectionalPhaseOffsets(AnimatorController)。
GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPrefabPath);
        // sourceAnimator（来源动画控制器）：使用位置：作为 LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string) 的参数、LeonInjuredAnimationSetup.CalibrateDirectionalPhaseOffsets(AnimatorController)。
Animator sourceAnimator;
        if (rigPrefab != null)
        {
            sourceAnimator = rigPrefab.GetComponent<Animator>();
        }
        else
        {
            sourceAnimator = null;
        }

        // walkTreeMask（行走行为树遮罩）：
AvatarMask walkTreeMask = null;
        // jogTreeMask（慢跑行为树遮罩）：
AvatarMask jogTreeMask = null;
        // walkTree（行走行为树）：保存 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string) 的参数。
var walkTree = FindOrCreateTree(controller, WalkTreeName);
        // jogTree（慢跑行为树）：保存 LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string) 的参数。
var jogTree = FindOrCreateTree(controller, JogTreeName);
        CalibrateTreePhase(walkTree, sourceAnimator, "步行");
        CalibrateTreePhase(jogTree, sourceAnimator, "慢跑");
    }

    /// <summary>CalibrateTreePhase 的业务处理入口；本方法直接依赖 Debug.Log(object)、Debug.LogWarning(object)、EditorUtility.SetDirty(Object)、LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)、LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)、Mathf.Approximately(float, float)、StringBuilder.AppendLine(string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.CalibrateDirectionalPhaseOffsets(AnimatorController)；下游（含内部回调体） Debug.Log(object)、Debug.LogWarning(object)、EditorUtility.SetDirty(Object)、LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)、LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)、Mathf.Approximately(float, float)、StringBuilder.AppendLine(string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tree">中文含义：行为树。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。</param>
    /// <param name="animator">中文含义：动画控制器。使用位置：作为 LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。</param>
    /// <param name="label">中文含义：标签。使用位置：作为 Debug.LogWarning(object) 的参数、作为 StringBuilder.AppendLine(string) 的参数。</param>
    private static void CalibrateTreePhase(BlendTree tree, Animator animator, string label)
    {
        if (tree == null || animator == null || animator.avatar == null)
        {
            Debug.LogWarning("[里昂受伤动画] 缺少骨架或 Avatar，跳过" + label + "相位校准。");
            return;
        }

        // sampleCount（采样数量）：使用位置：作为 LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
const int sampleCount = 128;
        // signals（信号集合）：使用位置：作为 LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
float[][] signals = new float[tree.children.Length][];
        // clipNames（动画片段名称列表）：使用位置：作为 StringBuilder.AppendLine(string) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
string[] clipNames = new string[tree.children.Length];
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
for (int i = 0; i < tree.children.Length; i++)
        {
            // clip（动画片段）：使用位置：作为 LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
AnimationClip clip = tree.children[i].motion as AnimationClip;
            if (clip == null)
            {
                signals[i] = null;
                continue;
            }

            clipNames[i] = clip.name;
            signals[i] = SampleFootPhaseSignal(clip, animator, sampleCount);
        }

        // 参考：前进动画（位置 (0,1) 的第一个子项）
        // referenceIndex（引用索引）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
int referenceIndex = -1;
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
for (int i = 0; i < tree.children.Length; i++)
        {
            if (Mathf.Approximately(tree.children[i].position.x, 0f) && Mathf.Approximately(tree.children[i].position.y, 1f))
            {
                referenceIndex = i;
                break;
            }
        }

        if (referenceIndex < 0 || signals[referenceIndex] == null)
        {
            Debug.LogWarning("[里昂受伤动画] 未找到前进参考动画，跳过" + label + "相位校准。");
            return;
        }

        // reference（引用）：使用位置：作为 LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float) 的参数。
float[] reference = signals[referenceIndex];
        // sb（文本构建器）：使用位置：作为 Debug.Log(object) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[里昂受伤动画] {label}相位校准（参考: {clipNames[referenceIndex]}）:");
        // children（子对象集合）：使用位置：作为 StringBuilder.AppendLine(string) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
ChildMotion[] children = tree.children;
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
for (int i = 0; i < children.Length; i++)
        {
            if (signals[i] == null)
            {
                sb.AppendLine($"  {clipNames[i]}: 无法采样，保持 cycleOffset={children[i].cycleOffset:F3}");
                continue;
            }

            if (i == referenceIndex)
            {
                children[i].cycleOffset = 0f;
                sb.AppendLine($"  {clipNames[i]}: 参考，偏移=0.000，误差={CircuarSignalError(signals[i], reference, 0f):F4}");
                continue;
            }

            // bestOffset（最佳偏移量）：使用位置：作为 StringBuilder.AppendLine(string) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
float bestOffset = 0f;
            // bestError（最佳错误）：使用位置：作为 StringBuilder.AppendLine(string) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
float bestError = float.MaxValue;
            // errorAtZero（错误所在零值）：保存 LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float) 的返回结果，供当前步骤使用。使用位置：作为 StringBuilder.AppendLine(string) 的参数。
float errorAtZero = CircuarSignalError(signals[i], reference, 0f);
            // steps（步数）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
int steps = sampleCount;
            // s（当前步态相位的采样索引）：使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
for (int s = 0; s < steps; s++)
            {
                // offset（偏移量）：使用位置：作为 LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float) 的参数、LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
float offset = (float)s / steps;
                // error（错误）：保存 LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)。
float error = CircuarSignalError(signals[i], reference, offset);
                if (error < bestError)
                {
                    bestError = error;
                    bestOffset = offset;
                }
            }

            children[i].cycleOffset = bestOffset;
            sb.AppendLine($"  {clipNames[i]}: 偏移={bestOffset:F3}，误差 {errorAtZero:F4}->{bestError:F4}");
        }

        tree.children = children;
        EditorUtility.SetDirty(tree);
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 用人形骨架采样左右脚相对髋部的高度差（leftFootLocalY - rightFootLocalY）。
    /// 通过 AnimationMode 逐帧采样曲线，不依赖运行时。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)；下游（含内部回调体） AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float)、AnimationMode.StartAnimationMode()、AnimationMode.StopAnimationMode()、Animator.GetBoneTransform(HumanBodyBones)、Transform.InverseTransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float) 的参数、LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。</param>
    /// <param name="animator">中文含义：动画控制器。使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。</param>
    /// <param name="sampleCount">中文含义：采样数量。使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。</param>
    private static float[] SampleFootPhaseSignal(AnimationClip clip, Animator animator, int sampleCount)
    {
        try
        {
            AnimationMode.StartAnimationMode();
            // proxy（代理）：使用位置：作为 AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float) 的参数。
GameObject proxy = animator.gameObject;
            AnimationMode.SampleAnimationClip(proxy, clip, 0f);
            // hips（髋部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            // leftFoot（左脚）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            // rightFoot（右脚）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || leftFoot == null || rightFoot == null)
            {
                AnimationMode.StopAnimationMode();
                return null;
            }

            // signal（信号）：使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
float[] signal = new float[sampleCount];
            // s（当前脚部相位的采样索引）：使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
for (int s = 0; s < sampleCount; s++)
            {
                // time（时间）：使用位置：作为 AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float) 的参数。
float time = clip.length * s / sampleCount;
                AnimationMode.SampleAnimationClip(proxy, clip, time);
                // leftLocal（左侧局部）：使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
float leftLocal = hips.InverseTransformPoint(leftFoot.position).y;
                // rightLocal（右侧局部）：使用位置：LeonInjuredAnimationSetup.SampleFootPhaseSignal(AnimationClip, Animator, int)。
float rightLocal = hips.InverseTransformPoint(rightFoot.position).y;
                signal[s] = leftLocal - rightLocal;
            }

            AnimationMode.StopAnimationMode();
            return signal;
        }
        catch
        {
            try
            {
                AnimationMode.StopAnimationMode();
            }
            catch
            {
            }

            return null;
        }
    }

    /// <summary>
    /// 循环互相关误差：信号 a 偏移 offset 后与参考 b 的均方差。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.CalibrateTreePhase(BlendTree, Animator, string)；下游（含内部回调体） Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="a">中文含义：当前浮点数值数组。使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。</param>
    /// <param name="reference">中文含义：引用。使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。</param>
    /// <param name="offset">中文含义：偏移量。使用位置：作为 Mathf.RoundToInt(float) 的参数。</param>
    private static float CircuarSignalError(float[] a, float[] reference, float offset)
    {
        // n（要比较的步态信号采样数量）：使用位置：作为 Mathf.RoundToInt(float) 的参数、LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
int n = a.Length;
        // shift（偏移）：使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
int shift = Mathf.RoundToInt(offset * n) % n;
        // error（错误）：使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
double error = 0.0;
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
for (int i = 0; i < n; i++)
        {
            // j（内层遍历索引）：使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
int j = (i + shift) % n;
            // d（当前采样点与参考信号的差值）：使用位置：LeonInjuredAnimationSetup.CircuarSignalError(float[], float[], float)。
double d = a[j] - reference[i];
            error += d * d;
        }

        return (float)(error / n);
    }

    /// <summary>CreateThresholdChild 的业务处理入口；本方法直接依赖 ChildMotion.ChildMotion()、LeonInjuredAnimationSetup.LoadHurtClip(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ConfigureTurnTree(BlendTree)；下游（含内部回调体） ChildMotion.ChildMotion()、LeonInjuredAnimationSetup.LoadHurtClip(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 LeonInjuredAnimationSetup.LoadHurtClip(string) 的参数。</param>
    /// <param name="threshold">中文含义：阈值。使用位置：LeonInjuredAnimationSetup.CreateThresholdChild(string, float, float)。</param>
    /// <param name="timeScale">中文含义：时间缩放。使用位置：LeonInjuredAnimationSetup.CreateThresholdChild(string, float, float)。</param>
    private static ChildMotion CreateThresholdChild(string clipName, float threshold, float timeScale)
    {
        return new ChildMotion
        {
            motion = LoadHurtClip(clipName),
            threshold = threshold,
            timeScale = timeScale
        };
    }

    /// <summary>
    /// 复用控制器内同名混合树子资产，避免重复运行产生冗余资产。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)、LeonInjuredAnimationSetup.CalibrateDirectionalPhaseOffsets(AnimatorController)；下游（含内部回调体） AssetDatabase.AddObjectToAsset(Object, Object)、AssetDatabase.LoadAllAssetsAtPath(string)、BlendTree.BlendTree()、IEnumerable.OfType&lt;BlendTree&gt;()、IEnumerable&lt;BlendTree&gt;.FirstOrDefault&lt;BlendTree&gt;(Func&lt;BlendTree, bool&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数。</param>
    /// <param name="treeName">中文含义：行为树名称。使用位置：作为 IEnumerable&lt;BlendTree&gt;.FirstOrDefault&lt;BlendTree&gt;(Func&lt;BlendTree, bool&gt;) 的参数、LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string)。</param>
    private static BlendTree FindOrCreateTree(AnimatorController controller, string treeName)
    {
        // tree（行为树）：保存 IEnumerable<BlendTree>.FirstOrDefault<BlendTree>(Func<BlendTree, bool>) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、LeonInjuredAnimationSetup.FindOrCreateTree(AnimatorController, string)。
BlendTree tree = AssetDatabase.LoadAllAssetsAtPath(InjuredControllerPath).OfType<BlendTree>().FirstOrDefault( /* 中文含义：当前混合行为树。 */item => item.name == treeName);
        if (tree != null)
        {
            return tree;
        }

        tree = new BlendTree
        {
            name = treeName
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    /// <summary>LoadHurtClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)、LeonInjuredAnimationSetup.CreateDirectionalChild(string, float, float, float)、LeonInjuredAnimationSetup.CreateThresholdChild(string, float, float)；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;) 的参数、LeonInjuredAnimationSetup.LoadHurtClip(string)。</param>
    private static AnimationClip LoadHurtClip(string clipName)
    {
        // assetPath（资源路径）：使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。
string assetPath = HurtClipDirectory + clipName + ".fbx";
        // clip（动画片段）：保存 IEnumerable<AnimationClip>.FirstOrDefault<AnimationClip>(Func<AnimationClip, bool>) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.LoadHurtClip(string)。
AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：当前动画片段。 */item => item.name == clipName);
        if (clip == null)
        {
            throw new InvalidOperationException("找不到受伤动画片段：" + assetPath);
        }

        return clip;
    }

    /// <summary>EnsureState 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddState(string, Vector3)、LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） AnimatorStateMachine.AddState(string, Vector3)、LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：作为 LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string) 的参数、LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数、作为 LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    private static AnimatorState EnsureState(AnimatorStateMachine machine, string stateName, Vector3 position)
    {
        // state（当前动画状态）：保存 LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3)。
AnimatorState state = FindState(machine, stateName);
        if (state != null)
        {
            return state;
        }

        return machine.AddState(stateName, position);
    }

    /// <summary>FindState 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.EnsureState(AnimatorStateMachine, string, Vector3)；下游（含内部回调体） IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：LeonInjuredAnimationSetup.FindState(AnimatorStateMachine, string)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;) 的参数。</param>
    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == stateName);
    }

    /// <summary>RemoveStateTransitions 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：LeonInjuredAnimationSetup.RemoveStateTransitions(AnimatorState)。</param>
    private static void RemoveStateTransitions(AnimatorState state)
    {
        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数。
foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    /// <summary>RemoveIncomingTransitions 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.BuildInjuredStateMachine(AnimatorController)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。</param>
    /// <param name="target">中文含义：目标动画状态。使用位置：LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。</param>
    private static void RemoveIncomingTransitions(AnimatorStateMachine machine, AnimatorState target)
    {
        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。
foreach (ChildAnimatorState child in machine.states)
        {
            // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数、LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。
foreach (AnimatorStateTransition transition in child.state.transitions.ToArray())
            {
                if (transition.destinationState == target)
                {
                    child.state.RemoveTransition(transition);
                }
            }
        }

        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数、LeonInjuredAnimationSetup.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。
foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
        {
            if (transition.destinationState == target)
            {
                machine.RemoveAnyStateTransition(transition);
            }
        }
    }

    /// <summary>
    /// 校验最终结构：八层、十九参数、受伤层遮罩与默认状态、恢复层同步配置。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） AnimatorController.GetStateEffectiveMotion(AnimatorState, int)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、Mathf.Approximately(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。</param>
    private static void ValidateResult(AnimatorController controller)
    {
        if (controller.layers.Length != 8)
        {
            throw new InvalidOperationException("受伤控制器必须为八层，当前：" + controller.layers.Length);
        }

        // i（当前遍历索引）：使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
for (int i = 0; i < ExpectedLayerNames.Length; i++)
        {
            if (controller.layers[i].name != ExpectedLayerNames[i])
            {
                throw new InvalidOperationException("图层" + i + "名称不匹配：" + controller.layers[i].name + "（期望：" + ExpectedLayerNames[i] + "）");
            }
        }

        if (controller.parameters.Length != 19)
        {
            throw new InvalidOperationException("参数数量必须保持19，当前：" + controller.parameters.Length);
        }

        // requiredParameters（所需参数集合）：使用位置：LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
string[] requiredParameters =
        {
            "Horizontal",
            "Vertical",
            "InputMagnitude",
            "Sprint",
            "TurnAngle"
        };
        // parameterName（参数名称）：每轮从所需参数集合取得一个元素，仅代表当前这一项。使用位置：作为 IEnumerable<AnimatorControllerParameter>.Any<AnimatorControllerParameter>(Func<AnimatorControllerParameter, bool>) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。
foreach (string parameterName in requiredParameters)
        {
            if (!controller.parameters.Any( /* 中文含义：参数。 */parameter => parameter.name == parameterName))
            {
                throw new InvalidOperationException("控制器缺少参数：" + parameterName);
            }
        }

        // injured（受伤）：使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
AnimatorControllerLayer injured = controller.layers[AnimatorLayerID.Injured];
        if (injured.avatarMask == null)
        {
            throw new InvalidOperationException("受伤移动层缺少遮罩。");
        }

        if (!Mathf.Approximately(injured.defaultWeight, 0f))
        {
            throw new InvalidOperationException("受伤移动层默认权重必须为零。");
        }

        if (injured.stateMachine == null)
        {
            throw new InvalidOperationException("受伤移动层缺少状态机。");
        }

        if (injured.stateMachine.defaultState == null || injured.stateMachine.defaultState.name != IdleStateName)
        {
            throw new InvalidOperationException("受伤移动层默认状态必须为 " + IdleStateName);
        }

        if (injured.stateMachine.states.Length != 4)
        {
            throw new InvalidOperationException("受伤移动层状态数量异常：" + injured.stateMachine.states.Length);
        }

        if (injured.stateMachine.anyStateTransitions.Length != 0)
        {
            throw new InvalidOperationException("受伤移动层不允许存在任意状态转换。");
        }

        // lowerBody（下半身）：使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
AnimatorControllerLayer lowerBody = controller.layers[AnimatorLayerID.InjuredLowerBody];
        if (lowerBody.avatarMask == null)
        {
            throw new InvalidOperationException("受伤下身层缺少遮罩。");
        }

        if (!Mathf.Approximately(lowerBody.defaultWeight, 0f))
        {
            throw new InvalidOperationException("受伤下身层默认权重必须为零。");
        }

        if (lowerBody.syncedLayerIndex != AnimatorLayerID.Injured)
        {
            throw new InvalidOperationException("受伤下身层必须同步受伤移动层，当前同步索引：" + lowerBody.syncedLayerIndex);
        }

        if (lowerBody.stateMachine != null)
        {
            throw new InvalidOperationException("受伤下身层不允许携带独立状态机。");
        }

        // child（子级）：每轮从受伤的状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
foreach (ChildAnimatorState child in injured.stateMachine.states)
        {
            // state（当前动画状态）：使用位置：作为 AnimatorController.GetStateEffectiveMotion(AnimatorState, int) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
AnimatorState state = child.state;
            // overrideMotion（覆盖运动）：保存 AnimatorController.GetStateEffectiveMotion(AnimatorState, int) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
Motion overrideMotion = controller.GetStateEffectiveMotion(state, AnimatorLayerID.InjuredLowerBody);
            if (state == null || state.motion == null || overrideMotion == null)
            {
                // leftOperand4（计算前缓存的左侧文字“受伤下身层存在未解析的空动作：”）：从文字“受伤下身层存在未解析的空动作：”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数。
string leftOperand4 = "受伤下身层存在未解析的空动作：";
                // selectedValue3（分支选出的文字“空状态”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、LeonInjuredAnimationSetup.ValidateResult(AnimatorController)。
string selectedValue3;
                if (state == null)
                {
                    selectedValue3 = "空状态";
                }
                else
                {
                    selectedValue3 = state.name;
                }

                throw new InvalidOperationException(leftOperand4 + (selectedValue3));
            }

            if (overrideMotion != state.motion)
            {
                throw new InvalidOperationException("受伤下身层动作与源层不一致：" + state.name);
            }
        }

        if (lowerBody.blendingMode != AnimatorLayerBlendingMode.Override)
        {
            throw new InvalidOperationException("受伤下身层必须为覆盖模式。");
        }
    }

    /// <summary>
    /// 通过 SerializedObject 将受伤移动速度写入预制体与所有已加载场景中的
    /// LeonLocomotion，清除旧的序列化值（如 2.25、3.00 等）。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） LeonInjuredAnimationSetup.ApplyInjuredSpeedsToLoadedScenes()、LeonInjuredAnimationSetup.ApplyInjuredSpeedsToPrefab()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ApplyInjuredSpeeds()
    {
        ApplyInjuredSpeedsToPrefab();
        ApplyInjuredSpeedsToLoadedScenes();
    }

    /// <summary>WriteInjuredSpeeds 的业务处理入口；本方法直接依赖 InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ApplyInjuredSpeedsToLoadedScenes()、LeonInjuredAnimationSetup.ApplyInjuredSpeedsToPrefab()；下游（含内部回调体） InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="serialized">中文含义：序列化的。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。</param>
    private static void WriteInjuredSpeeds(SerializedObject serialized)
    {
        // fieldNames（字段名称列表）：使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 SerializedObject.FindProperty(string) 的参数、LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。
string[] fieldNames =
        {
            "injuredWalkForwardSpeed",
            "injuredWalkBackwardSpeed",
            "injuredStrafeSpeed",
            "injuredJogSpeed"
        };
        // values（数值集合）：使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。
float[] values =
        {
            InjuredWalkForwardSpeed,
            InjuredWalkBackwardSpeed,
            InjuredStrafeSpeed,
            InjuredJogSpeed
        };
        // i（当前遍历索引）：使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。
for (int i = 0; i < fieldNames.Length; i++)
        {
            // property（属性）：保存 SerializedObject.FindProperty(string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)。
SerializedProperty property = serialized.FindProperty(fieldNames[i]);
            if (property == null)
            {
                throw new InvalidOperationException("LeonLocomotion 缺少字段：" + fieldNames[i]);
            }

            property.floatValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>ApplyInjuredSpeedsToPrefab 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogWarning(object)、GameObject.GetComponentInChildren&lt;LeonLocomotion&gt;(bool)、LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ApplyInjuredSpeeds()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogWarning(object)、GameObject.GetComponentInChildren&lt;LeonLocomotion&gt;(bool)、LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ApplyInjuredSpeedsToPrefab()
    {
        // prefabRoot（预制件根节点）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ApplyInjuredSpeedsToPrefab()。
GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning("[里昂受伤动画] 找不到预制体：" + LeonPrefabPath);
            return;
        }

        // contents（内容集合）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、LeonInjuredAnimationSetup.ApplyInjuredSpeedsToPrefab()。
GameObject contents = PrefabUtility.LoadPrefabContents(LeonPrefabPath);
        try
        {
            // locomotion（移动行为）：保存 GameObject.GetComponentInChildren<LeonLocomotion>(bool) 的返回结果，供当前步骤使用。使用位置：作为 SerializedObject.SerializedObject(Object) 的参数、LeonInjuredAnimationSetup.ApplyInjuredSpeedsToPrefab()。
LeonLocomotion locomotion = contents.GetComponentInChildren<LeonLocomotion>(true);
            if (locomotion == null)
            {
                Debug.Log("[里昂受伤动画] 预制体不含 LeonLocomotion，速度仅写入场景实例。");
                return;
            }

            WriteInjuredSpeeds(new SerializedObject(locomotion));
            PrefabUtility.SaveAsPrefabAsset(contents, LeonPrefabPath);
            Debug.Log("[里昂受伤动画] 已将受伤移动速度写入预制体：" + LeonPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>ApplyInjuredSpeedsToLoadedScenes 的业务处理入口；本方法直接依赖 Debug.Log(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene)、HashSet&lt;Scene&gt;.Add(Scene)、HashSet&lt;Scene&gt;.HashSet()、LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)、Object.FindObjectsByType&lt;LeonLocomotion&gt;(FindObjectsInactive, FindObjectsSortMode) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ApplyInjuredSpeeds()；下游（含内部回调体） Debug.Log(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene)、HashSet&lt;Scene&gt;.Add(Scene)、HashSet&lt;Scene&gt;.HashSet()、LeonInjuredAnimationSetup.WriteInjuredSpeeds(SerializedObject)、Object.FindObjectsByType&lt;LeonLocomotion&gt;(FindObjectsInactive, FindObjectsSortMode) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void ApplyInjuredSpeedsToLoadedScenes()
    {
        // dirtyScenes（未保存改动场景集合）：使用位置：LeonInjuredAnimationSetup.ApplyInjuredSpeedsToLoadedScenes()。
HashSet<Scene> dirtyScenes = new HashSet<Scene>();
        // locomotions（移动行为集合）：保存 Object.FindObjectsByType<LeonLocomotion>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ApplyInjuredSpeedsToLoadedScenes()。
LeonLocomotion[] locomotions = Object.FindObjectsByType<LeonLocomotion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // locomotion（移动行为）：每轮从移动行为集合取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<Scene>.Add(Scene) 的参数、作为 SerializedObject.SerializedObject(Object) 的参数。
foreach (LeonLocomotion locomotion in locomotions)
        {
            WriteInjuredSpeeds(new SerializedObject(locomotion));
            dirtyScenes.Add(locomotion.gameObject.scene);
        }

        // scene（场景）：每轮从未保存改动场景集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数。
foreach (Scene scene in dirtyScenes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("[里昂受伤动画] 已将受伤移动速度写入场景：" + scene.path);
            }
        }
    }

    /// <summary>
    /// 将受伤控制器赋给里昂预制体和已加载场景中的里昂实例。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.Build()；下游（含内部回调体） LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)、LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController) 的参数、作为 LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController) 的参数。</param>
    private static void ApplyControllerToPrefabAndScene(AnimatorController controller)
    {
        ApplyControllerToPrefab(controller);
        ApplyControllerToLoadedScenes(controller);
    }

    /// <summary>ApplyControllerToPrefab 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogWarning(object)、GameObject.GetComponent&lt;Animator&gt;()、PrefabUtility.GetPrefabAssetType(Object)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ApplyControllerToPrefabAndScene(AnimatorController)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogWarning(object)、GameObject.GetComponent&lt;Animator&gt;()、PrefabUtility.GetPrefabAssetType(Object)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController)。</param>
    private static void ApplyControllerToPrefab(AnimatorController controller)
    {
        // prefabRoot（预制件根节点）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.GetPrefabAssetType(Object) 的参数、LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController)。
GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(LeonPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogWarning("[里昂受伤动画] 找不到预制体：" + LeonPrefabPath);
            return;
        }

        if (PrefabUtility.GetPrefabAssetType(prefabRoot) != PrefabAssetType.Regular)
        {
            Debug.LogWarning("[里昂受伤动画] 预制体不是常规类型，跳过控制器赋值：" + LeonPrefabPath);
            return;
        }

        // contents（内容集合）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController)。
GameObject contents = PrefabUtility.LoadPrefabContents(LeonPrefabPath);
        try
        {
            // animator（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ApplyControllerToPrefab(AnimatorController)。
Animator animator = contents.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[里昂受伤动画] 预制体缺少 Animator 组件：" + LeonPrefabPath);
                return;
            }

            if (animator.runtimeAnimatorController == controller)
            {
                return;
            }

            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(contents, LeonPrefabPath);
            Debug.Log("[里昂受伤动画] 已将受伤控制器赋给预制体：" + LeonPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    /// <summary>ApplyControllerToLoadedScenes 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Debug.Log(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene)、HashSet&lt;Scene&gt;.Add(Scene)、HashSet&lt;Scene&gt;.HashSet()、Object.FindObjectsByType&lt;LeonAnimatorDriver&gt;(FindObjectsInactive, FindObjectsSortMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInjuredAnimationSetup.ApplyControllerToPrefabAndScene(AnimatorController)；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Debug.Log(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene)、HashSet&lt;Scene&gt;.Add(Scene)、HashSet&lt;Scene&gt;.HashSet()、Object.FindObjectsByType&lt;LeonAnimatorDriver&gt;(FindObjectsInactive, FindObjectsSortMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)。</param>
    private static void ApplyControllerToLoadedScenes(AnimatorController controller)
    {
        // dirtyScenes（未保存改动场景集合）：使用位置：LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)。
HashSet<Scene> dirtyScenes = new HashSet<Scene>();
        // drivers（驱动器集合）：保存 Object.FindObjectsByType<LeonAnimatorDriver>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)。
LeonAnimatorDriver[] drivers = Object.FindObjectsByType<LeonAnimatorDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // driver（里昂动画控制器驱动器）：每轮从驱动器集合取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<Scene>.Add(Scene) 的参数、LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)。
foreach (LeonAnimatorDriver driver in drivers)
        {
            // animator（动画控制器）：保存 Component.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：LeonInjuredAnimationSetup.ApplyControllerToLoadedScenes(AnimatorController)。
Animator animator = driver.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == controller)
            {
                continue;
            }

            animator.runtimeAnimatorController = controller;
            dirtyScenes.Add(driver.gameObject.scene);
        }

        // scene（场景）：每轮从未保存改动场景集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数。
foreach (Scene scene in dirtyScenes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log("[里昂受伤动画] 已更新场景中的里昂实例：" + scene.path);
            }
        }
    }
}
