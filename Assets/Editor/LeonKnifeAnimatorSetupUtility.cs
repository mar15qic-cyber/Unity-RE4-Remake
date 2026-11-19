using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 里昂小刀近战动画状态机重建工具。
/// 保持五层和十八参数契约，只调整小刀轻攻击所在图层。
/// </summary>
public static class LeonKnifeAnimatorSetupUtility
{
    /// <summary>中文含义：控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数。</summary>
    private const string ControllerPath = "Assets/Animations/玩家/控制器/LeonCombatController_New_RewriteTest.controller";
    /// <summary>中文含义：小刀攻击动画片段路径。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string) 的参数。</summary>
    private const string KnifeAttackClipPath = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/Knife_MeleeSet/Knife_Light_Melee06.fbx";
    /// <summary>中文含义：小刀招架动画片段路径。编译期常量，保持既有值与消费者映射。使用位置：作为 LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string) 的参数。</summary>
    private const string KnifeParryClipPath = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/Knife_MeleeSet/Knife_Light_Melee05.fbx";
    /// <summary>编辑器菜单调用入口 Build；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、EditorUtility.SetDirty(Object)、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/重建小刀近战状态机")]
    public static void Build()
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonKnifeAnimatorSetupUtility.ValidateContract(AnimatorController) 的参数、LeonKnifeAnimatorSetupUtility.Build()。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            throw new InvalidOperationException("找不到里昂正式动画控制器。");
        }

        ValidateContract(controller);
        // weaponMachine（武器状态机）：使用位置：作为 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine) 的参数、作为 LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine) 的参数。
AnimatorStateMachine weaponMachine = controller.layers[AnimatorLayerID.WeaponAction].stateMachine;
        // fullBodyMachine（全身状态机）：使用位置：作为 LeonKnifeAnimatorSetupUtility.RemoveKnifeAttackState(AnimatorStateMachine) 的参数。
AnimatorStateMachine fullBodyMachine = controller.layers[AnimatorLayerID.FullBodyAction].stateMachine;
        RemoveKnifeAttackState(fullBodyMachine);
        BuildKnifeAttackOnWeaponLayer(weaponMachine);
        BuildKnifeParryOnWeaponLayer(weaponMachine);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[里昂小刀] 小刀近战状态机已重建到第3层上半身遮罩。");
    }

    /// <summary>ValidateContract 的业务处理入口；本方法直接依赖 InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.EnsureParameter(AnimatorController, string, AnimatorControllerParameterType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.Build()；下游（含内部回调体） InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.EnsureParameter(AnimatorController, string, AnimatorControllerParameterType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：作为 LeonKnifeAnimatorSetupUtility.EnsureParameter(AnimatorController, string, AnimatorControllerParameterType) 的参数、LeonKnifeAnimatorSetupUtility.ValidateContract(AnimatorController)。</param>
    private static void ValidateContract(AnimatorController controller)
    {
        if (controller.layers.Length != 5)
        {
            throw new InvalidOperationException("里昂控制器必须保持五层。");
        }

        if (controller.parameters.Length != 18)
        {
            throw new InvalidOperationException("里昂控制器必须保持十八个参数。");
        }

        // expectedLayers（预期图层集合）：使用位置：LeonKnifeAnimatorSetupUtility.ValidateContract(AnimatorController)。
string[] expectedLayers =
        {
            "Base Movement",
            "Aim Offset",
            "Fire Additive",
            "Weapon Action",
            "Full Body Action"
        };
        // i（当前遍历索引）：使用位置：LeonKnifeAnimatorSetupUtility.ValidateContract(AnimatorController)。
for (int i = 0; i < expectedLayers.Length; i++)
        {
            if (controller.layers[i].name != expectedLayers[i])
            {
                throw new InvalidOperationException("里昂控制器图层顺序不匹配。");
            }
        }

        EnsureParameter(controller, "WeaponAction", AnimatorControllerParameterType.Int);
        EnsureParameter(controller, "FullBodyAction", AnimatorControllerParameterType.Int);
    }

    /// <summary>EnsureParameter 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.ValidateContract(AnimatorController)；下游（含内部回调体） IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：动画控制器资源。使用位置：LeonKnifeAnimatorSetupUtility.EnsureParameter(AnimatorController, string, AnimatorControllerParameterType)。</param>
    /// <param name="parameterName">中文含义：参数名称。使用位置：作为 IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</param>
    /// <param name="parameterType">中文含义：参数类型。使用位置：作为 IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;) 的参数。</param>
    private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
    {
        // found（是否已找到）：保存 IEnumerable<AnimatorControllerParameter>.Any<AnimatorControllerParameter>(Func<AnimatorControllerParameter, bool>) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.EnsureParameter(AnimatorController, string, AnimatorControllerParameterType)。
bool found = controller.parameters.Any( /* 中文含义：参数。 */parameter => parameter.name == parameterName && parameter.type == parameterType);
        if (!found)
        {
            throw new InvalidOperationException("里昂控制器缺少参数：" + parameterName);
        }
    }

    /// <summary>RemoveKnifeAttackState 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.Build()；下游（含内部回调体） AnimatorStateMachine.RemoveState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：作为 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState) 的参数、LeonKnifeAnimatorSetupUtility.RemoveKnifeAttackState(AnimatorStateMachine)。</param>
    private static void RemoveKnifeAttackState(AnimatorStateMachine machine)
    {
        // state（当前动画状态）：保存 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.RemoveState(AnimatorState) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState) 的参数 等共 4 处。
AnimatorState state = FindState(machine, "KnifeAttack");
        if (state == null)
        {
            return;
        }

        RemoveIncomingTransitions(machine, state);
        RemoveStateTransitions(state);
        machine.RemoveState(state);
    }

    /// <summary>BuildKnifeAttackOnWeaponLayer 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddState(string, Vector3)、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)、LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.Build()；下游（含内部回调体） AnimatorStateMachine.AddState(string, Vector3)、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)、LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：作为 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的参数、LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)。</param>
    private static void BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine machine)
    {
        // empty（空）：保存 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 的参数、LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)。
AnimatorState empty = FindState(machine, "Empty");
        if (empty == null)
        {
            throw new InvalidOperationException("第3层缺少空状态。");
        }

        empty.writeDefaultValues = false;
        // attack（攻击）：保存 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float) 的参数、作为 LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState) 的参数 等共 5 处。
AnimatorState attack = FindState(machine, "KnifeAttack");
        if (attack == null)
        {
            attack = machine.AddState("KnifeAttack", new Vector3(540f, 260f));
        }

        ConfigureKnifeAttackState(attack);
        RemoveTransitionsToState(empty, attack);
        RemoveStateTransitions(attack);
        AddIntegerTransition(empty, attack, "FullBodyAction", AnimatorConditionMode.Equals, (int)FullBodyActionType.KnifeQuickAttack, 0.035f);
        AddIntegerTransition(attack, empty, "FullBodyAction", AnimatorConditionMode.NotEqual, (int)FullBodyActionType.KnifeQuickAttack, 0.055f);
    }

    /// <summary>ConfigureKnifeAttackState 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、AnimatorState.AddStateMachineBehaviour&lt;MeleeDamageSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、LeonKnifeAnimatorSetupUtility.LoadKnifeAttackClip()、Object.DestroyImmediate(Object, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、AnimatorState.AddStateMachineBehaviour&lt;MeleeDamageSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、LeonKnifeAnimatorSetupUtility.LoadKnifeAttackClip()、Object.DestroyImmediate(Object, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)。</param>
    private static void ConfigureKnifeAttackState(AnimatorState state)
    {
        // clip（动画片段）：保存 LeonKnifeAnimatorSetupUtility.LoadKnifeAttackClip() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)。
AnimationClip clip = LoadKnifeAttackClip();
        state.motion = clip;
        state.speed = 1.65f;
        state.writeDefaultValues = false;
        // behaviour（行为）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object, bool) 的参数、LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)。
foreach (StateMachineBehaviour behaviour in state.behaviours.ToArray())
        {
            if (behaviour is FullBodyActionSMB || behaviour is MeleeDamageSMB)
            {
                Object.DestroyImmediate(behaviour, true);
            }
        }

        // actionBehaviour（动作行为）：保存 AnimatorState.AddStateMachineBehaviour<FullBodyActionSMB>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)。
FullBodyActionSMB actionBehaviour = state.AddStateMachineBehaviour<FullBodyActionSMB>();
        actionBehaviour.actionType = FullBodyActionType.KnifeQuickAttack;
        actionBehaviour.completeNormalizedTime = 0.72f;
        // damageBehaviour（伤害行为）：保存 AnimatorState.AddStateMachineBehaviour<MeleeDamageSMB>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)。
MeleeDamageSMB damageBehaviour = state.AddStateMachineBehaviour<MeleeDamageSMB>();
        damageBehaviour.actionType = FullBodyActionType.KnifeQuickAttack;
        damageBehaviour.hitNormalizedTime = 0.24f;
    }

    /// <summary>BuildKnifeParryOnWeaponLayer 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddState(string, Vector3)、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)、LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.Build()；下游（含内部回调体） AnimatorStateMachine.AddState(string, Vector3)、InvalidOperationException.InvalidOperationException(string)、LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)、LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)、LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)、LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：作为 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的参数、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)。</param>
    private static void BuildKnifeParryOnWeaponLayer(AnimatorStateMachine machine)
    {
        // empty（空）：保存 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 的参数、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)。
AnimatorState empty = FindState(machine, "Empty");
        if (empty == null)
        {
            throw new InvalidOperationException("武器动作层缺少空状态。");
        }

        // parry（招架）：保存 LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float) 的参数、作为 LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState) 的参数、作为 LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState) 的参数 等共 5 处。
AnimatorState parry = FindState(machine, "KnifeParry");
        if (parry == null)
        {
            parry = machine.AddState("KnifeParry", new Vector3(540f, 360f));
        }

        ConfigureKnifeParryState(parry);
        RemoveTransitionsToState(empty, parry);
        RemoveStateTransitions(parry);
        AddIntegerTransition(empty, parry, "FullBodyAction", AnimatorConditionMode.Equals, (int)FullBodyActionType.KnifeParry, 0.035f);
        AddIntegerTransition(parry, empty, "FullBodyAction", AnimatorConditionMode.NotEqual, (int)FullBodyActionType.KnifeParry, 0.055f);
    }

    /// <summary>ConfigureKnifeParryState 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string)、Object.DestroyImmediate(Object, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;FullBodyActionSMB&gt;()、IEnumerable&lt;StateMachineBehaviour&gt;.ToArray&lt;StateMachineBehaviour&gt;()、LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string)、Object.DestroyImmediate(Object, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)。</param>
    private static void ConfigureKnifeParryState(AnimatorState state)
    {
        state.motion = LoadAnimationClip(KnifeParryClipPath, "Knife_Light_Melee05");
        state.speed = 1.65f;
        state.writeDefaultValues = false;
        // behaviour（行为）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object, bool) 的参数、LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)。
foreach (StateMachineBehaviour behaviour in state.behaviours.ToArray())
        {
            if (behaviour is FullBodyActionSMB || behaviour is MeleeDamageSMB)
            {
                Object.DestroyImmediate(behaviour, true);
            }
        }

        // actionBehaviour（动作行为）：保存 AnimatorState.AddStateMachineBehaviour<FullBodyActionSMB>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)。
FullBodyActionSMB actionBehaviour = state.AddStateMachineBehaviour<FullBodyActionSMB>();
        actionBehaviour.actionType = FullBodyActionType.KnifeParry;
        actionBehaviour.completeNormalizedTime = 0.52f;
    }

    /// <summary>LoadKnifeAttackClip 的业务处理入口；本方法直接依赖 LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.ConfigureKnifeAttackState(AnimatorState)；下游（含内部回调体） LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static AnimationClip LoadKnifeAttackClip()
    {
        return LoadAnimationClip(KnifeAttackClipPath, "Knife_Light_Melee06");
    }

    /// <summary>LoadAnimationClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.ConfigureKnifeParryState(AnimatorState)、LeonKnifeAnimatorSetupUtility.LoadKnifeAttackClip()；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、IEnumerable.OfType&lt;AnimationClip&gt;()、IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;)、InvalidOperationException.InvalidOperationException(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="assetPath">中文含义：资源路径。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数。</param>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 IEnumerable&lt;AnimationClip&gt;.FirstOrDefault&lt;AnimationClip&gt;(Func&lt;AnimationClip, bool&gt;) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</param>
    private static AnimationClip LoadAnimationClip(string assetPath, string clipName)
    {
        // clip（动画片段）：保存 IEnumerable<AnimationClip>.FirstOrDefault<AnimationClip>(Func<AnimationClip, bool>) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.LoadAnimationClip(string, string)。
AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：当前动画片段。 */item => item.name == clipName);
        if (clip != null)
        {
            return clip;
        }

        clip = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：当前动画片段。 */item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip == null)
        {
            throw new InvalidOperationException("找不到小刀动画片段：" + clipName);
        }

        return clip;
    }

    /// <summary>AddIntegerTransition 的业务处理入口；本方法直接依赖 AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画状态。使用位置：LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)。</param>
    /// <param name="destination">中文含义：目的地。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数。</param>
    /// <param name="parameter">中文含义：参数。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    /// <param name="mode">中文含义：当前动画控制器条件模式。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)。</param>
    private static void AddIntegerTransition(AnimatorState source, AnimatorState destination, string parameter, AnimatorConditionMode mode, int value, float duration)
    {
        // transition（过渡）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：LeonKnifeAnimatorSetupUtility.AddIntegerTransition(AnimatorState, AnimatorState, string, AnimatorConditionMode, int, float)。
AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(mode, value, parameter);
    }

    /// <summary>RemoveIncomingTransitions 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.RemoveKnifeAttackState(AnimatorStateMachine)；下游（含内部回调体） AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。</param>
    /// <param name="target">中文含义：目标动画状态。使用位置：作为 LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 的参数、LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。</param>
    private static void RemoveIncomingTransitions(AnimatorStateMachine machine, AnimatorState target)
    {
        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState) 的参数。
foreach (ChildAnimatorState child in machine.states)
        {
            RemoveTransitionsToState(child.state, target);
        }

        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数、LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)。
foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
        {
            if (transition.destinationState == target)
            {
                machine.RemoveAnyStateTransition(transition);
            }
        }
    }

    /// <summary>RemoveTransitionsToState 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.RemoveIncomingTransitions(AnimatorStateMachine, AnimatorState)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源动画状态。使用位置：LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState)。</param>
    /// <param name="target">中文含义：目标动画状态。使用位置：LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState)。</param>
    private static void RemoveTransitionsToState(AnimatorState source, AnimatorState target)
    {
        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数、LeonKnifeAnimatorSetupUtility.RemoveTransitionsToState(AnimatorState, AnimatorState)。
foreach (AnimatorStateTransition transition in source.transitions.ToArray())
        {
            if (transition.destinationState == target)
            {
                source.RemoveTransition(transition);
            }
        }
    }

    /// <summary>RemoveStateTransitions 的业务处理入口；本方法直接依赖 AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.RemoveKnifeAttackState(AnimatorStateMachine)；下游（含内部回调体） AnimatorState.RemoveTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorStateTransition&gt;.ToArray&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：LeonKnifeAnimatorSetupUtility.RemoveStateTransitions(AnimatorState)。</param>
    private static void RemoveStateTransitions(AnimatorState state)
    {
        // transition（过渡）：每轮从目标数组的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorState.RemoveTransition(AnimatorStateTransition) 的参数。
foreach (AnimatorStateTransition transition in state.transitions.ToArray())
        {
            state.RemoveTransition(transition);
        }
    }

    /// <summary>FindState 的业务处理入口；本方法直接依赖 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeAnimatorSetupUtility.BuildKnifeAttackOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.BuildKnifeParryOnWeaponLayer(AnimatorStateMachine)、LeonKnifeAnimatorSetupUtility.RemoveKnifeAttackState(AnimatorStateMachine)；下游（含内部回调体） IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;)、IEnumerable&lt;ChildAnimatorState&gt;.Select&lt;ChildAnimatorState, AnimatorState&gt;(Func&lt;ChildAnimatorState, AnimatorState&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="machine">中文含义：状态机。使用位置：LeonKnifeAnimatorSetupUtility.FindState(AnimatorStateMachine, string)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 IEnumerable&lt;AnimatorState&gt;.FirstOrDefault&lt;AnimatorState&gt;(Func&lt;AnimatorState, bool&gt;) 的参数。</param>
    private static AnimatorState FindState(AnimatorStateMachine machine, string stateName)
    {
        return machine.states.Select( /* 中文含义：子级。 */child => child.state).FirstOrDefault( /* 中文含义：当前动画状态。 */state => state.name == stateName);
    }
}
