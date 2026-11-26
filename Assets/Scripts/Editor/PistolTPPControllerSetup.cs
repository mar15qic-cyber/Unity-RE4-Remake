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
    /// <summary>路径。编译期常量，保持既有值与消费者映射。</summary>
    const string CTRL_PATH = "Assets/PistolAnimsetPro/Mecanim/PistolTPPController.controller";
    /// <summary>路径。编译期常量，保持既有值与消费者映射。</summary>
    const string MASK_PATH = "Assets/PistolAnimsetPro/Mecanim/UpperBodyMaskPistol.mask";
    /// <summary>路径。编译期常量，保持既有值与消费者映射。</summary>
    const string FBX_PATH = "Assets/PistolAnimsetPro/Animations/PistolAnimsetPro.fbx";
    /// <summary>模型文件。编译期常量，保持既有值与消费者映射。</summary>
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
        // 角色控制器。
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
        // 动画片段换弹。
AnimationClip clipReload = FindClip(FBX_PATH, "Pistol_Reload_2");
        // 动画片段近战。
AnimationClip clipMelee = FindClip(FBX_PATH, "Pistol_Melee_Hard");
        // 动画片段命中候选值。
AnimationClip clipHitC = FindClip(FBX_PATH, "Pistol_Hit_C_1");
        // 动画片段命中左侧。
AnimationClip clipHitL = FindClip(FBX_PATH, "Pistol_Hit_L_1");
        // 动画片段命中右侧。
AnimationClip clipHitR = FindClip(FBX_PATH, "Pistol_Hit_R_2");
        // 动画片段冲刺。
AnimationClip clipSprint = FindClip(FBX_PATH, "Pistol_SprintLoop");
        // 动画片段冲刺起始。
AnimationClip clipSprintStart = FindClip(FBX_PATH, "Pistol_SprintStart");
        // 动画片段冲刺停止。
AnimationClip clipSprintStop = FindClip(FBX_PATH, "Pistol_SprintStop_LU");
        // 动画片段死亡。
AnimationClip clipDeath = FindClip(DEATH_FBX, null);
        // ========== 3. L0 Base Layer ==========
        // 图层0。
var layer0 = ctrl.layers[0];
        // 状态机0。
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
            // 命中状态机。
var hitSM = sm0.AddStateMachine("HitReact", new Vector3(600, 120, 0));
            // 命中候选值动画状态的哈希编号。
var stHitC = hitSM.AddState("Hit_C", new Vector3(0, 0, 0));
            stHitC.motion = clipHitC;
            // 命中左侧动画状态的哈希编号。
var stHitL = hitSM.AddState("Hit_L", new Vector3(0, 60, 0));
            stHitL.motion = clipHitL;
            // 命中右侧动画状态的哈希编号。
var stHitR = hitSM.AddState("Hit_R", new Vector3(0, 120, 0));
            stHitR.motion = clipHitR;
            // 命中后方动画状态的哈希编号。
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
                // 进度退出。
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
            // 动画状态死亡。
var stDeath = sm0.AddState("Death", new Vector3(800, 120, 0));
            stDeath.motion = clipDeath;
            stDeath.writeDefaultValues = false;
            AddAnyStateTransition(sm0, stDeath, 0.1f, false, ("Dead", AnimatorConditionMode.If, 0f));
            Debug.Log("已添加 Death 状态");
        }

        // ---- 3.3 Sprint 子状态机 ----
        if (clipSprint != null)
        {
            // 冲刺状态机。
var sprintSM = sm0.AddStateMachine("Sprint", new Vector3(400, 120, 0));
            // 动画状态起始。
var stStart = sprintSM.AddState("SprintStart", new Vector3(0, 0, 0));
            stStart.motion = clipSprintStart;
            // 动画状态循环。
var stLoop = sprintSM.AddState("SprintLoop", new Vector3(0, 60, 0));
            stLoop.motion = clipSprint;
            // 动画状态停止。
var stStop = sprintSM.AddState("SprintStop", new Vector3(0, 120, 0));
            stStop.motion = clipSprintStop;
            // 进度起始。
var tStart = stStart.AddTransition(stLoop);
            tStart.hasExitTime = true;
            tStart.exitTime = 0.85f;
            tStart.duration = 0.1f;
            // 进度停止。
var tStop = stLoop.AddTransition(stStop);
            tStop.AddCondition(AnimatorConditionMode.IfNot, 0, "Sprint");
            tStop.hasExitTime = false;
            tStop.duration = 0.15f;
            // 进度停止退出。
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
            // 状态机2。
var sm2 = ctrl.layers[2].stateMachine;
            RemoveChildState(sm2, "Melee");
            RemoveAnyStateTransitionTo(sm2, "Melee");
            if (clipMelee != null)
            {
                // 动画状态近战。
var stMelee = sm2.AddState("Melee", new Vector3(300, 60, 0));
                stMelee.motion = clipMelee;
                AddAnyStateTransition(sm2, stMelee, 0.05f, false, ("Melee", AnimatorConditionMode.If, 0f)).canTransitionToSelf = false;
                // 进度退出。
var tExit = stMelee.AddTransition(sm2.defaultState);
                tExit.hasExitTime = true;
                tExit.exitTime = 0.9f;
                tExit.duration = 0.1f;
                Debug.Log("已添加 Melee 状态到 L2");
            }
        }

        // ========== 5. 重建 L3 ReloadOverride ==========
        // 已有图层集合。
var existingLayers = ctrl.layers.ToList();
        // 换弹索引。
int reloadIdx = existingLayers.FindIndex( /* 中文含义：当前动画控制器控制器图层。 */l => l.name == "ReloadOverride");
        if (reloadIdx >= 0)
        {
            ctrl.RemoveLayer(reloadIdx);
            Debug.Log($"已删除旧的 ReloadOverride 层 (索引 {reloadIdx})");
        }

        // 遮罩。
var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MASK_PATH);
        if (mask == null)
        {
            Debug.LogWarning("找不到 AvatarMask: " + MASK_PATH);
        }

        // 状态机换弹。
var smReload = new AnimatorStateMachine();
        smReload.name = "ReloadOverride";
        smReload.hideFlags = HideFlags.HideInHierarchy;
        if (AssetDatabase.GetAssetPath(ctrl) != "")
        {
            AssetDatabase.AddObjectToAsset(smReload, ctrl);
        }

        // 空状态。
var emptyState = smReload.AddState("Empty", new Vector3(300, 120, 0));
        emptyState.writeDefaultValues = false;
        smReload.defaultState = emptyState;
        // 动画状态换弹。
var stReload = smReload.AddState("Reload", new Vector3(300, 0, 0));
        stReload.motion = clipReload;
        stReload.writeDefaultValues = false;
        // 任意目标换弹。
var anyToReload = smReload.AddAnyStateTransition(stReload);
        anyToReload.AddCondition(AnimatorConditionMode.If, 0, "Reload");
        anyToReload.duration = 0.05f;
        anyToReload.hasExitTime = false;
        anyToReload.canTransitionToSelf = false;
        // 换弹目标空。
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
    /// <param name="sm0">状态机0。</param>
    /// <param name="sprintStart">冲刺起始。</param>
    static void AddSprintEntryTransitions(AnimatorStateMachine sm0, AnimatorState sprintStart)
    {
        // subSMRef（子级状态机引用）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
foreach (var subSMRef in sm0.stateMachines)
        {
            // 子级状态机名称。
var subSMName = subSMRef.stateMachine.name;
            if (subSMName != "Walking" && subSMName != "Standing")
            {
                continue;
            }

            // 子级状态机。
var subSM = subSMRef.stateMachine;
            // stateRef（状态引用）：每轮从子级状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：PistolTPPControllerSetup.AddSprintEntryTransitions(AnimatorStateMachine, AnimatorState)。
foreach (var stateRef in subSM.states)
            {
                // 当前动画状态。
var st = stateRef.state;
                // 跳过 WalkStop_* 和 RotateLeft/Right（那些只是过渡状态），只从主要状态进入 Sprint
                if (st.name.Contains("Stop"))
                {
                    continue;
                }

                // 当前动画控制器状态过渡。
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="dest">目的地。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="hasExitTime">是否具有退出时间。</param>
    /// <param name="conds">条件集合。</param>
    static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState dest, float duration, bool hasExitTime, params (string /* 中文含义：过渡条件使用的动画参数名称。 */ param, AnimatorConditionMode /* 中文含义：过渡条件使用的比较模式。 */ mode, float /* 中文含义：过渡条件的比较阈值。 */ threshold)[] conds)
    {
        // 当前动画控制器状态过渡。
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
    /// <param name="ctrl">角色控制器。</param>
    /// <param name="name">名称。</param>
    /// <param name="type">当前动画控制器控制器参数类型。</param>
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
    /// <param name="fbxPath">模型文件路径。</param>
    /// <param name="clipName">动画片段名称。</param>
    static AnimationClip FindClip(string fbxPath, string clipName)
    {
        if (string.IsNullOrEmpty(fbxPath))
        {
            return null;
        }

        // 对象集合。
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="name">名称。</param>
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="name">名称。</param>
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="name">名称。</param>
    static void RemoveChildStateMachine(AnimatorStateMachine sm, string name)
    {
        // 目标移除。
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="name">名称。</param>
    static void RemoveChildState(AnimatorStateMachine sm, string name)
    {
        // 目标移除。
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="destName">目的地名称。</param>
    static void RemoveAnyStateTransitionTo(AnimatorStateMachine sm, string destName)
    {
        // 目标移除。
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
    /// <param name="sm">动画状态机。</param>
    /// <param name="smName">状态机名称。</param>
    static void RemoveAnyStateTransitionToSM(AnimatorStateMachine sm, string smName)
    {
        // 目标移除。
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
