using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

/// <summary>
/// 一键重建 PistolTPPController 状态机，补齐 Sprint/HitReact/Death/Melee/L3 ReloadOverride
/// 菜单路径：Tools / Leon / 修复 PistolTPP 状态机
///
/// 关键设计（纯状态机解决动画问题）：
/// - Sprint 不走 AnyState 进入。改为从 Walking/Standing 子状态机的具体状态直接过渡到 SprintStart；
/// - 一旦进入 Sprint 子状态机，Sprint loop 不会被 AnyState 打断反反复复涌回 Start；
/// - 松开 Sprint 后 SprintStop -> Exit 回到默认状态，再由代码层把状态走回 Idle/Walking。
/// </summary>
public class PistolTPPControllerSetup : EditorWindow
{
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 Debug.LogError(object) 的参数。</summary>
    const string CTRL_PATH = "Assets/PistolAnimsetPro/Mecanim/PistolTPPController.controller";
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AvatarMask&gt;(string) 的参数、作为 Debug.LogWarning(object) 的参数。</summary>
    const string MASK_PATH = "Assets/PistolAnimsetPro/Mecanim/UpperBodyMaskPistol.mask";
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 PistolTPPControllerSetup.FindClip(string, string) 的参数。</summary>
    const string FBX_PATH = "Assets/PistolAnimsetPro/Animations/PistolAnimsetPro.fbx";
    /// <summary>中文含义：模型文件。编译期常量，保持既有值与消费者映射。使用位置：作为 PistolTPPControllerSetup.FindClip(string, string) 的参数。</summary>
    const string DEATH_FBX = "Assets/PistolAnimsetPro/Animations/PistolAnimsetPro_Death.fbx";
    /// <summary>编辑器菜单调用入口 ShowWindow；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.GetWindow&lt;PistolTPPControllerSetup&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Leon/修复 PistolTPP 状态机")]
    static void ShowWindow()
    {
        GetWindow<PistolTPPControllerSetup>("状态机修复");
    }

    /// <summary>OnGUI 的业务处理入口；本方法直接依赖 GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])、PistolTPPControllerSetup.RunSetup()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])、PistolTPPControllerSetup.RunSetup()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnGUI()
    {
        GUILayout.Label("修复 PistolTPPController 状态机", EditorStyles.boldLabel);
        GUILayout.Label("重新创建 Sprint/HitReact/Death/Melee/L3 ReloadOverride 等。");
        if (GUILayout.Button("执行修复", GUILayout.Height(40)))
        {
            RunSetup();
        }
    }

    /// <summary>RunSetup 的业务处理入口；本方法直接依赖 AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorState.AddTransition(AnimatorState)、AnimatorState.AddTransition(AnimatorStateMachine)、AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorStateMachine.AddState(string, Vector3) 等共 28 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.OnGUI()；下游（含内部回调体） AnimatorController.AddLayer(AnimatorControllerLayer)、AnimatorController.RemoveLayer(int)、AnimatorControllerLayer.AnimatorControllerLayer()、AnimatorState.AddTransition(AnimatorState)、AnimatorState.AddTransition(AnimatorStateMachine)、AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorStateMachine.AddState(string, Vector3) 等共 28 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static void RunSetup()
    {
        // ctrl（角色控制器）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、作为 AssetDatabase.GetAssetPath(Object) 的参数、作为 PistolTPPControllerSetup.AddParamIfMissing(AnimatorController, string, AnimatorControllerParameterType) 的参数 等共 4 处。
var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null)
        {
            Debug.LogError("找不到 Controller: " + CTRL_PATH);
            return;
        }

        // ========== 1. 添加参数 ==========
        AddParamIfMissing(ctrl, "Sprint", AnimatorControllerParameterType.Bool);
        AddParamIfMissing(ctrl, "Melee", AnimatorControllerParameterType.Trigger);
        AddParamIfMissing(ctrl, "Hit", AnimatorControllerParameterType.Trigger);
        AddParamIfMissing(ctrl, "HitDir", AnimatorControllerParameterType.Int);
        AddParamIfMissing(ctrl, "Dead", AnimatorControllerParameterType.Bool);
        // ========== 2. 加载动画剪辑 ==========
        // clipReload（动画片段换弹）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipReload = FindClip(FBX_PATH, "Pistol_Reload_2");
        // clipMelee（动画片段近战）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipMelee = FindClip(FBX_PATH, "Pistol_Melee_Hard");
        // clipHitC（动画片段命中候选值）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipHitC = FindClip(FBX_PATH, "Pistol_Hit_C_1");
        // clipHitL（动画片段命中左侧）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipHitL = FindClip(FBX_PATH, "Pistol_Hit_L_1");
        // clipHitR（动画片段命中右侧）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipHitR = FindClip(FBX_PATH, "Pistol_Hit_R_2");
        // clipSprint（动画片段冲刺）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipSprint = FindClip(FBX_PATH, "Pistol_SprintLoop");
        // clipSprintStart（动画片段冲刺起始）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipSprintStart = FindClip(FBX_PATH, "Pistol_SprintStart");
        // clipSprintStop（动画片段冲刺停止）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipSprintStop = FindClip(FBX_PATH, "Pistol_SprintStop_LU");
        // clipDeath（动画片段死亡）：保存 PistolTPPControllerSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
AnimationClip clipDeath = FindClip(DEATH_FBX, null);
        // ========== 3. L0 Base Layer ==========
        // layer0（图层0）：使用位置：PistolTPPControllerSetup.RunSetup()。
var layer0 = ctrl.layers[0];
        // sm0（状态机0）：使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState) 的参数、作为 PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string) 的参数 等共 6 处。
var sm0 = layer0.stateMachine;
        // 先删除旧的 Sprint/HitReact 子状态机（避免残留损坏的过渡）
        RemoveChildStateMachine(sm0, "Sprint");
        RemoveChildStateMachine(sm0, "HitReact");
        RemoveChildState(sm0, "Death");
        RemoveAnyStateTransitionTo(sm0, "SprintStart");
        RemoveAnyStateTransitionTo(sm0, "Hit_C");
        RemoveAnyStateTransitionTo(sm0, "Hit_L");
        RemoveAnyStateTransitionTo(sm0, "Hit_R");
        RemoveAnyStateTransitionTo(sm0, "Hit_Back");
        RemoveAnyStateTransitionTo(sm0, "Death");
        // ---- 3.1 HitReact 子状态机 ----
        {
            // hitSM（命中状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorStateMachine) 的参数、PistolTPPControllerSetup.RunSetup()。
var hitSM = sm0.AddStateMachine("HitReact", new Vector3(600, 120, 0));
            // stHitC（命中候选值动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stHitC = hitSM.AddState("Hit_C", new Vector3(0, 0, 0));
            stHitC.motion = clipHitC;
            // stHitL（命中左侧动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stHitL = hitSM.AddState("Hit_L", new Vector3(0, 60, 0));
            stHitL.motion = clipHitL;
            // stHitR（命中右侧动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stHitR = hitSM.AddState("Hit_R", new Vector3(0, 120, 0));
            stHitR.motion = clipHitR;
            // stHitBack（命中后方动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stHitBack = hitSM.AddState("Hit_Back", new Vector3(0, 180, 0));
            // 每个 Hit 状态播完回 Exit（用子状态机作为 Exit 目标的代理）
            // st（当前动画状态）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.RunSetup()。
foreach (var st in new[]
            {
                stHitC,
                stHitL,
                stHitR,
                stHitBack
            }

            )
            {
                // tExit（进度退出）：保存 AnimatorState.AddTransition(AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var tExit = st.AddTransition(hitSM);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.85f;
                tExit.duration = 0.1f;
            }

            // AnyState -> Hit 系列用 Trigger 触发
            AddAnyStateTransition(sm0, stHitC, 0.05f, false, ("HitDir", AnimatorConditionMode.Equals, 0f), ("Hit", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm0, stHitL, 0.05f, false, ("HitDir", AnimatorConditionMode.Equals, 1f), ("Hit", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm0, stHitR, 0.05f, false, ("HitDir", AnimatorConditionMode.Equals, 2f), ("Hit", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm0, stHitBack, 0.05f, false, ("HitDir", AnimatorConditionMode.Equals, 3f), ("Hit", AnimatorConditionMode.If, 0f));
            Debug.Log("已添加 HitReact 子状态机");
        }

        // ---- 3.2 Death 状态 ----
        if (clipDeath != null)
        {
            // stDeath（动画状态死亡）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stDeath = sm0.AddState("Death", new Vector3(800, 120, 0));
            stDeath.motion = clipDeath;
            stDeath.writeDefaultValues = false;
            AddAnyStateTransition(sm0, stDeath, 0.1f, false, ("Dead", AnimatorConditionMode.If, 0f));
            Debug.Log("已添加 Death 状态");
        }

        // ---- 3.3 Sprint 子状态机 ----
        if (clipSprint != null)
        {
            // sprintSM（冲刺状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorStateMachine) 的参数、PistolTPPControllerSetup.RunSetup()。
var sprintSM = sm0.AddStateMachine("Sprint", new Vector3(400, 120, 0));
            // stStart（动画状态起始）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState) 的参数、PistolTPPControllerSetup.RunSetup()。
var stStart = sprintSM.AddState("SprintStart", new Vector3(0, 0, 0));
            stStart.motion = clipSprintStart;
            // stLoop（动画状态循环）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、PistolTPPControllerSetup.RunSetup()。
var stLoop = sprintSM.AddState("SprintLoop", new Vector3(0, 60, 0));
            stLoop.motion = clipSprint;
            // stStop（动画状态停止）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、PistolTPPControllerSetup.RunSetup()。
var stStop = sprintSM.AddState("SprintStop", new Vector3(0, 120, 0));
            stStop.motion = clipSprintStop;
            // tStart（进度起始）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var tStart = stStart.AddTransition(stLoop);
            tStart.hasExitTime = true;
            tStart.exitTime = 0.85f;
            tStart.duration = 0.1f;
            // tStop（进度停止）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var tStop = stLoop.AddTransition(stStop);
            tStop.AddCondition(AnimatorConditionMode.IfNot, 0, "Sprint");
            tStop.hasExitTime = false;
            tStop.duration = 0.15f;
            // tStopExit（进度停止退出）：保存 AnimatorState.AddTransition(AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var tStopExit = stStop.AddTransition(sprintSM); // Exit
            tStopExit.hasExitTime = true;
            tStopExit.exitTime = 0.9f;
            tStopExit.duration = 0.1f;
            Debug.Log("已添加 Sprint 子状态机");
            // 关键：不使用 AnyState -> SprintStart；改为从 Walking/Standing 各状态直接进入 SprintStart
            AddSprintEntryTransitions(sm0, stStart);
        }

        // ========== 4. L2 Firing 层：添加 Melee ==========
        if (ctrl.layers.Length > 2)
        {
            // sm2（状态机2）：使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string) 的参数 等共 5 处。
var sm2 = ctrl.layers[2].stateMachine;
            RemoveChildState(sm2, "Melee");
            RemoveAnyStateTransitionTo(sm2, "Melee");
            if (clipMelee != null)
            {
                // stMelee（动画状态近战）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、PistolTPPControllerSetup.RunSetup()。
var stMelee = sm2.AddState("Melee", new Vector3(300, 60, 0));
                stMelee.motion = clipMelee;
                AddAnyStateTransition(sm2, stMelee, 0.05f, false, ("Melee", AnimatorConditionMode.If, 0f)).canTransitionToSelf = false;
                // tExit（进度退出）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var tExit = stMelee.AddTransition(sm2.defaultState);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.9f;
                tExit.duration = 0.1f;
                Debug.Log("已添加 Melee 状态到 L2");
            }
        }

        // ========== 5. 重建 L3 ReloadOverride ==========
        // existingLayers（已有图层集合）：保存 IEnumerable<AnimatorControllerLayer>.ToList<AnimatorControllerLayer>() 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var existingLayers = ctrl.layers.ToList();
        // reloadIdx（换弹索引）：保存 List<AnimatorControllerLayer>.FindIndex(Predicate<AnimatorControllerLayer>) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorController.RemoveLayer(int) 的参数、作为 Debug.Log(object) 的参数、PistolTPPControllerSetup.RunSetup()。
int reloadIdx = existingLayers.FindIndex( /* 中文含义：当前动画控制器控制器图层。 */l => l.name == "ReloadOverride");
        if (reloadIdx >= 0)
        {
            ctrl.RemoveLayer(reloadIdx);
            Debug.Log($"已删除旧的 ReloadOverride 层 (索引 {reloadIdx})");
        }

        // mask（遮罩）：保存 AssetDatabase.LoadAssetAtPath<AvatarMask>(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorController.AddLayer(AnimatorControllerLayer) 的参数、PistolTPPControllerSetup.RunSetup()。
var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
        if (mask == null)
        {
            Debug.LogWarning("找不到 AvatarMask: " + MASK_PATH);
        }

        // smReload（状态机换弹）：使用位置：作为 AnimatorController.AddLayer(AnimatorControllerLayer) 的参数、作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、PistolTPPControllerSetup.RunSetup()。
var smReload = new AnimatorStateMachine();
        smReload.name = "ReloadOverride";
        smReload.hideFlags = HideFlags.HideInHierarchy;
        if (AssetDatabase.GetAssetPath(ctrl) != "")
        {
            AssetDatabase.AddObjectToAsset(smReload, ctrl);
        }

        // emptyState（空状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、PistolTPPControllerSetup.RunSetup()。
var emptyState = smReload.AddState("Empty", new Vector3(300, 120, 0));
        emptyState.writeDefaultValues = false;
        smReload.defaultState = emptyState;
        // stReload（动画状态换弹）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的参数、PistolTPPControllerSetup.RunSetup()。
var stReload = smReload.AddState("Reload", new Vector3(300, 0, 0));
        stReload.motion = clipReload;
        stReload.writeDefaultValues = false;
        // anyToReload（任意目标换弹）：保存 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var anyToReload = smReload.AddAnyStateTransition(stReload);
        anyToReload.AddCondition(AnimatorConditionMode.If, 0, "Reload");
        anyToReload.duration = 0.05f;
        anyToReload.hasExitTime = false;
        anyToReload.canTransitionToSelf = false;
        // reloadToEmpty（换弹目标空）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.RunSetup()。
var reloadToEmpty = stReload.AddTransition(emptyState);
        reloadToEmpty.hasExitTime = true;
        reloadToEmpty.exitTime = 0.95f;
        reloadToEmpty.duration = 0.1f;
        ctrl.AddLayer(new AnimatorControllerLayer { name = "ReloadOverride", stateMachine = smReload, defaultWeight = 0f, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override });
        AssetDatabase.SaveAssets();
        Debug.Log("PistolTPPController 状态机修复完成！");
    }

    /// <summary>
    /// 从 Walking、Standing 子状态机的各状态向 SprintStart 添加进入 Sprint 的过渡
    /// 条件：Sprint==true 且 InputMagnitude>0.1
    /// </summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorState.AddTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、Debug.Log(object)、string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm0">中文含义：状态机0。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。</param>
    /// <param name="sprintStart">中文含义：冲刺起始。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数。</param>
    static void AddSprintEntryTransitions(AnimatorStateMachine sm0, AnimatorState sprintStart)
    {
        // subSMRef（子级状态机引用）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
foreach (var subSMRef in sm0.stateMachines)
        {
            // subSMName（子级状态机名称）：使用位置：作为 Debug.Log(object) 的参数、PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
var subSMName = subSMRef.stateMachine.name;
            if (subSMName != "Walking" && subSMName != "Standing")
            {
                continue;
            }

            // subSM（子级状态机）：使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
var subSM = subSMRef.stateMachine;
            // stateRef（状态引用）：每轮从子级状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
foreach (var stateRef in subSM.states)
            {
                // st（当前动画状态）：使用位置：作为 Debug.Log(object) 的参数、PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
var st = stateRef.state;
                // 跳过 WalkStop_* 和 RotateLeft/Right（那些只是过渡状态），只从主要状态进入 Sprint
                if (st.name.Contains("Stop"))
                {
                    continue;
                }

                // t（当前动画控制器状态过渡）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
var t = st.AddTransition(sprintStart);
                t.AddCondition(AnimatorConditionMode.If, 0, "Sprint");
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "InputMagnitude");
                t.hasExitTime = false;
                t.duration = 0.1f;
                t.canTransitionToSelf = false;
                Debug.Log("添加 SprintEntry: " + subSMName + "." + st.name + " -> SprintStart");
            }
        }
    }

    /// <summary>AddAnyStateTransition 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="dest">中文含义：目的地。使用位置：作为 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="hasExitTime">中文含义：是否具有退出时间。使用位置：PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="conds">中文含义：条件集合。使用位置：PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState dest, float duration, bool hasExitTime, params (string /* 中文含义：过渡条件使用的动画参数名称。 */ param, AnimatorConditionMode /* 中文含义：过渡条件使用的比较模式。 */ mode, float /* 中文含义：过渡条件的比较阈值。 */ threshold)[] conds)
    {
        // t（当前动画控制器状态过渡）：保存 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。
var t = sm.AddAnyStateTransition(dest);
        t.duration = duration;
        t.hasExitTime = hasExitTime;
        // c（当前组合结果）：每轮从条件集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。
foreach (var c in conds)
        {
            t.AddCondition(c.mode, c.threshold, c.param);
        }

        return t;
    }

    /// <summary>AddParamIfMissing 的业务处理入口；本方法直接依赖 AnimatorController.AddParameter(string, AnimatorControllerParameterType)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorController.AddParameter(string, AnimatorControllerParameterType)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctrl">中文含义：角色控制器。使用位置：PistolTPPControllerSetup.AddParamIfMissing(AnimatorController, string, AnimatorControllerParameterType)。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 AnimatorController.AddParameter(string, AnimatorControllerParameterType) 的参数、作为 IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;) 的参数。</param>
    /// <param name="type">中文含义：当前动画控制器控制器参数类型。使用位置：作为 AnimatorController.AddParameter(string, AnimatorControllerParameterType) 的参数。</param>
    static void AddParamIfMissing(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        if (ctrl.parameters.Any( /* 中文含义：当前动画控制器控制器参数。 */p => p.name == name))
        {
            return;
        }

        ctrl.AddParameter(name, type);
    }

    /// <summary>FindClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetRepresentationsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AssetDatabase.LoadAllAssetRepresentationsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fbxPath">中文含义：模型文件路径。使用位置：作为 AssetDatabase.LoadAllAssetRepresentationsAtPath(string) 的参数、作为 Debug.LogWarning(object) 的参数、作为 string.IsNullOrEmpty(string) 的参数。</param>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 Debug.LogWarning(object) 的参数、作为 string.IsNullOrEmpty(string) 的参数、PistolTPPControllerSetup.FindClip(string, string)。</param>
    static AnimationClip FindClip(string fbxPath, string clipName)
    {
        if (string.IsNullOrEmpty(fbxPath))
        {
            return null;
        }

        // objs（对象集合）：保存 AssetDatabase.LoadAllAssetRepresentationsAtPath(string) 的返回结果，供当前步骤使用。使用位置：PistolTPPControllerSetup.FindClip(string, string)。
Object[] objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
        // o（当前对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.FindClip(string, string)。
foreach (var o in objs)
        {
            if (o is AnimationClip /* 中文含义：动画片段。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ clip)
            {
                if (string.IsNullOrEmpty(clipName))
                {
                    return clip;
                }

                if (clip.name == clipName)
                {
                    return clip;
                }
            }
        }

        Debug.LogWarning($"找不到动画剪辑: {fbxPath} -> {clipName}");
        return null;
    }

    /// <summary>HasChildStateMachine 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.HasChildStateMachine(AnimatorStateMachine, string)。</param>
    /// <param name="name">中文含义：名称。使用位置：PistolTPPControllerSetup.HasChildStateMachine(AnimatorStateMachine, string)。</param>
    static bool HasChildStateMachine(AnimatorStateMachine sm, string name)
    {
        // child（子级）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.HasChildStateMachine(AnimatorStateMachine, string)。
foreach (var child in sm.stateMachines)
        {
            if (child.stateMachine.name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>HasChildState 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.HasChildState(AnimatorStateMachine, string)。</param>
    /// <param name="name">中文含义：名称。使用位置：PistolTPPControllerSetup.HasChildState(AnimatorStateMachine, string)。</param>
    static bool HasChildState(AnimatorStateMachine sm, string name)
    {
        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.HasChildState(AnimatorStateMachine, string)。
foreach (var child in sm.states)
        {
            if (child.state.name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>RemoveChildStateMachine 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、List&lt;ChildAnimatorStateMachine&gt;.Add(ChildAnimatorStateMachine)、List&lt;ChildAnimatorStateMachine&gt;.List()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、List&lt;ChildAnimatorStateMachine&gt;.Add(ChildAnimatorStateMachine)、List&lt;ChildAnimatorStateMachine&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.RemoveChildStateMachine(AnimatorStateMachine, string)。</param>
    /// <param name="name">中文含义：名称。使用位置：PistolTPPControllerSetup.RemoveChildStateMachine(AnimatorStateMachine, string)。</param>
    static void RemoveChildStateMachine(AnimatorStateMachine sm, string name)
    {
        // toRemove（目标移除）：使用位置：PistolTPPControllerSetup.RemoveChildStateMachine(AnimatorStateMachine, string)。
var toRemove = new System.Collections.Generic.List<ChildAnimatorStateMachine>();
        // child（子级）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 List<ChildAnimatorStateMachine>.Add(ChildAnimatorStateMachine) 的参数、PistolTPPControllerSetup.RemoveChildStateMachine(AnimatorStateMachine, string)。
foreach (var child in sm.stateMachines)
        {
            if (child.stateMachine.name == name)
            {
                toRemove.Add(child);
            }
        }

        // c（当前子级动画控制器状态机）：每轮从目标移除取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine) 的参数。
foreach (var c in toRemove)
        {
            sm.RemoveStateMachine(c.stateMachine);
        }
    }

    /// <summary>RemoveChildState 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveState(AnimatorState)、List&lt;AnimatorState&gt;.Add(AnimatorState)、List&lt;AnimatorState&gt;.List()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveState(AnimatorState)、List&lt;AnimatorState&gt;.Add(AnimatorState)、List&lt;AnimatorState&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.RemoveChildState(AnimatorStateMachine, string)。</param>
    /// <param name="name">中文含义：名称。使用位置：PistolTPPControllerSetup.RemoveChildState(AnimatorStateMachine, string)。</param>
    static void RemoveChildState(AnimatorStateMachine sm, string name)
    {
        // toRemove（目标移除）：使用位置：PistolTPPControllerSetup.RemoveChildState(AnimatorStateMachine, string)。
var toRemove = new System.Collections.Generic.List<AnimatorState>();
        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：作为 List<AnimatorState>.Add(AnimatorState) 的参数、PistolTPPControllerSetup.RemoveChildState(AnimatorStateMachine, string)。
foreach (var child in sm.states)
        {
            if (child.state.name == name)
            {
                toRemove.Add(child.state);
            }
        }

        // s（当前动画状态）：每轮从目标移除取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveState(AnimatorState) 的参数。
foreach (var s in toRemove)
        {
            sm.RemoveState(s);
        }
    }

    /// <summary>RemoveAnyStateTransitionTo 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.Add(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.List()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PistolTPPControllerSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.Add(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string)。</param>
    /// <param name="destName">中文含义：目的地名称。使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string)。</param>
    static void RemoveAnyStateTransitionTo(AnimatorStateMachine sm, string destName)
    {
        // toRemove（目标移除）：使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string)。
var toRemove = new System.Collections.Generic.List<AnimatorStateTransition>();
        // t（当前动画控制器状态过渡）：每轮从状态机的任意状态过渡集合取得一个元素，仅代表当前这一项。使用位置：作为 List<AnimatorStateTransition>.Add(AnimatorStateTransition) 的参数、PistolTPPControllerSetup.RemoveAnyStateTransitionTo(AnimatorStateMachine, string)。
foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState != null && t.destinationState.name == destName)
            {
                toRemove.Add(t);
            }
        }

        // t（当前动画控制器状态过渡）：每轮从目标移除取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数。
foreach (var t in toRemove)
        {
            sm.RemoveAnyStateTransition(t);
        }
    }

    /// <summary>RemoveAnyStateTransitionToSM 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.Add(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.List()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.Add(AnimatorStateTransition)、List&lt;AnimatorStateTransition&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionToSM(AnimatorStateMachine, string)。</param>
    /// <param name="smName">中文含义：状态机名称。使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionToSM(AnimatorStateMachine, string)。</param>
    static void RemoveAnyStateTransitionToSM(AnimatorStateMachine sm, string smName)
    {
        // toRemove（目标移除）：使用位置：PistolTPPControllerSetup.RemoveAnyStateTransitionToSM(AnimatorStateMachine, string)。
var toRemove = new System.Collections.Generic.List<AnimatorStateTransition>();
        // t（当前动画控制器状态过渡）：每轮从状态机的任意状态过渡集合取得一个元素，仅代表当前这一项。使用位置：作为 List<AnimatorStateTransition>.Add(AnimatorStateTransition) 的参数、PistolTPPControllerSetup.RemoveAnyStateTransitionToSM(AnimatorStateMachine, string)。
foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationStateMachine != null && t.destinationStateMachine.name == smName)
            {
                toRemove.Add(t);
            }
        }

        // t（当前动画控制器状态过渡）：每轮从目标移除取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数。
foreach (var t in toRemove)
        {
            sm.RemoveAnyStateTransition(t);
        }
    }
}
