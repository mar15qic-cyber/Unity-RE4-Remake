using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 一键构建 Zombie 敌人动画状态机
/// 菜单路径：Tools / Enemy / 构建 Zombie 状态机
///
/// 状态机结构：
/// Layer 0 (Base Layer) — 全身 locomotion + 状态
///   ├── Locomotion (BlendTree: Idle→Walk)
///   ├── Attack (Sub-SM: Attack01/02/03 随机)
///   ├── HitReact (Sub-SM: 5方向受击，腿部→Crawl)
///   ├── Crawl (Sub-SM: 爬行移动/攻击/起身)
///   └── Death (独立状态)
///
/// 参数：
///   MoveSpeed(float), MoveX(float), MoveY(float)
///   WALK(bool), ATK(bool), AttackIndex(int)
///   HIT(bool), HitDir(int), DIE(bool)
///   IsCrawl(bool), SearchMode(bool)
/// </summary>
public class ZombieAnimatorSetup : EditorWindow
{
    /// <summary>中文含义：方向。编译期常量，保持既有值与消费者映射。使用位置：ZombieAnimatorSetup.FindClip(string, string)。</summary>
    const string ANIM_DIR = "Assets/素材库/动画/Zombie_Animations/Animations";
    /// <summary>中文含义：方向。编译期常量，保持既有值与消费者映射。使用位置：ZombieAnimatorSetup.FindStaggerClip(string, string)。</summary>
    const string STAGGER_ANIM_DIR = "Assets/素材库/动画/Gruesome_Zombie_AnimSet/Animation/Humanoid/Inplace";
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AnimatorController.CreateAnimatorControllerAtPath(string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 Debug.Log(object) 的参数。</summary>
    const string CTRL_PATH = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>中文含义：路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 Debug.LogError(object) 的参数。</summary>
    const string CHAINSAW_CTRL_PATH = "Assets/Animations/Enemy/ChainsawMan/ChainsawMan.controller";
    /// <summary>中文含义：速度。编译期常量，保持既有值与消费者映射。使用位置：ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)。</summary>
    const float STAGGER_STATE_SPEED = 0.65f;
    /// <summary>编辑器菜单调用入口 ShowWindow；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorWindow.GetWindow&lt;ZombieAnimatorSetup&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/构建 Zombie 状态机")]
    static void ShowWindow()
    {
        GetWindow<ZombieAnimatorSetup>("Zombie 状态机构建");
    }

    /// <summary>编辑器菜单调用入口 RunChainsawSetupMenu；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAnimatorSetup.RunChainsawSetup()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/配置 ChainsawMan 硬直状态机")]
    static void RunChainsawSetupMenu()
    {
        RunChainsawSetup();
    }

    /// <summary>OnGUI 的业务处理入口；本方法直接依赖 GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])、ZombieAnimatorSetup.RunSetup()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Label(string, GUIStyle, params GUILayoutOption[])、GUILayout.Label(string, params GUILayoutOption[])、ZombieAnimatorSetup.RunSetup()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnGUI()
    {
        GUILayout.Label("构建 Zombie 敌人动画状态机", EditorStyles.boldLabel);
        GUILayout.Label("重建 Base Layer 全部状态、子状态机、BlendTree、过渡。");
        if (GUILayout.Button("执行构建", GUILayout.Height(40)))
        {
            RunSetup();
        }
    }

    /// <summary>RunSetup 的业务处理入口；本方法直接依赖 AnimatorController.CreateAnimatorControllerAtPath(string)、AnimatorState.AddExitTransition()、AnimatorState.AddStateMachineBehaviour&lt;EnemyCrawlAttackStateSMB&gt;()、AnimatorState.AddStateMachineBehaviour&lt;EnemyStandUpStateSMB&gt;()、AnimatorState.AddTransition(AnimatorState)、AnimatorState.AddTransition(AnimatorStateMachine)、AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 等共 33 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.OnGUI()；下游（含内部回调体） AnimatorController.CreateAnimatorControllerAtPath(string)、AnimatorState.AddExitTransition()、AnimatorState.AddStateMachineBehaviour&lt;EnemyCrawlAttackStateSMB&gt;()、AnimatorState.AddStateMachineBehaviour&lt;EnemyStandUpStateSMB&gt;()、AnimatorState.AddTransition(AnimatorState)、AnimatorState.AddTransition(AnimatorStateMachine)、AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 等共 33 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static void RunSetup()
    {
        // ctrl（角色控制器）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、作为 Debug.Log(object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 5 处。
var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL_PATH);
        if (ctrl == null)
        {
            // 创建新 Controller
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL_PATH);
            Debug.Log("[ZombieSetup] 创建新 Controller: " + CTRL_PATH);
        }

        // ========== 1. 添加参数 ==========
        AddParam(ctrl, "MoveSpeed", AnimatorControllerParameterType.Float);
        AddParam(ctrl, "MoveX", AnimatorControllerParameterType.Float);
        AddParam(ctrl, "MoveY", AnimatorControllerParameterType.Float);
        AddParam(ctrl, "WALK", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "ATK", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "AttackIndex", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "HIT", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "HitDir", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "DIE", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "IsCrawl", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "SearchMode", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "IdleBlend", AnimatorControllerParameterType.Float);
        AddParam(ctrl, "TurnDir", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "Stunned", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "HitTrigger", AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, "StaggerTrigger", AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, "StaggerDirection", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "StaggerLevel", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "CanTripOver", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "HealthRatio", AnimatorControllerParameterType.Float);
        // ========== 2. 获取 Base Layer ==========
        // 清理旧状态机，重建
        // layer0（图层0）：使用位置：ZombieAnimatorSetup.RunSetup()。
var layer0 = ctrl.layers[0];
        // sm（动画状态机）：使用位置：作为 Debug.Log(object) 的参数、作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数 等共 5 处。
var sm = layer0.stateMachine;
        // 删除所有旧状态和子状态机
        ClearStateMachine(sm);
        // ========== 3. 加载动画剪辑 ==========
        // Idle variants
        // clipIdle01（动画片段待机01）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var clipIdle01 = FindClip("Zombie_Idle_01.FBX", "Zombie_Idle_01");
        // clipIdle02（动画片段待机02）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var clipIdle02 = FindClip("Zombie_Idle_02.FBX", "Zombie_Idle_02");
        // clipIdle03（动画片段待机03）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var clipIdle03 = FindClip("Zombie_Idle_03.FBX", "Zombie_Idle_03");
        // clipIdleSearch（动画片段待机搜索）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var clipIdleSearch = FindClip("Zombie_Idle_Search.FBX", "Zombie_Idle_Search");
        // Walk variants (非 InPlace, 有根运动)
        // walk01（行走01）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var walk01 = FindClip("Zombie_Walk_01_Forward.FBX", "Zombie_Walk_01_Forward");
        // walk02（行走02）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var walk02 = FindClip("Zombie_Walk_02_Forward.FBX", "Zombie_Walk_02_Forward");
        // walk03（行走03）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var walk03 = FindClip("Zombie_Walk_03_Forward.FBX", "Zombie_Walk_03_Forward");
        // walk04（行走04）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var walk04 = FindClip("Zombie_Walk_04_Forward.FBX", "Zombie_Walk_04_Forward");
        // walkFast（行走快速）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var walkFast = FindClip("Zombie_Walk_Fast01_Forward.FBX", "Zombie_Walk_Fast01_Forward");
        // Attack
        // attack01（攻击01）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var attack01 = FindClip("Zombie_Attack01.FBX", "Zombie_Attack01");
        // attack02（攻击02）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var attack02 = FindClip("Zombie_Attack02.FBX", "Zombie_Attack02");
        // attack03（攻击03）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var attack03 = FindClip("Zombie_Attack03.FBX", "Zombie_Attack03");
        // attackFwd（攻击前向）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var attackFwd = FindClip("Zombie_Attack_Forward01.fbx", "Take 001");
        // attackTurn180（攻击转向180）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var attackTurn180 = FindClip("Zombie_Attack_180Turn.FBX", "Zombie_Attack_180Turn");
        // attackTurnL（攻击转向左侧）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var attackTurnL = FindClip("Zombie_Attack_90TurnLeft.FBX", "Zombie_Attack_90TurnLeft");
        // attackTurnR（攻击转向右侧）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var attackTurnR = FindClip("Zombie_Attack_90TurnRight.FBX", "Zombie_Attack_90TurnRight");
        // Hit React
        // hitHead（命中头部）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var hitHead = FindClip("Zombie_HitReact_Head.fbx", "Zombie_HitReact_Head");
        // hitLArm（命中左侧手臂）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var hitLArm = FindClip("Zombie_HitReact_LeftArm.fbx", "Zombie_HitReact_LeftArm");
        // hitRArm（命中右侧手臂）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var hitRArm = FindClip("Zombie_HitReact_RightArm.fbx", "Zombie_HitReact_RightArm");
        // hitLLeg（命中左侧腿部）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var hitLLeg = FindClip("Zombie_HitReact_LeftLeg.fbx", "Zombie_HitReact_LeftLeg");
        // hitRLeg（命中右侧腿部）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var hitRLeg = FindClip("Zombie_HitReact_RightLeg.fbx", "Zombie_HitReact_RightLeg");
        // staggerBodyFront（身体硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyFront = FindStaggerClip("zombie_hit_body_front_inplace.fbx", "zombie_hit_body_front_inplace");
        // staggerBodyBack（身体硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyBack = FindStaggerClip("zombie_hit_body_back_inplace.fbx", "zombie_hit_body_back_inplace");
        // staggerBodyLeft（身体硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyLeft = FindStaggerClip("zombie_hit_body_left_inplace.fbx", "zombie_hit_body_left_inplace");
        // staggerBodyRight（身体硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyRight = FindStaggerClip("zombie_hit_body_right_inplace.fbx", "zombie_hit_body_right_inplace");
        // staggerHeadFront（头部硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadFront = FindStaggerClip("zombie_hit_head_front_inplace.fbx", "zombie_hit_head_front_inplace");
        // staggerHeadBack（头部硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadBack = FindStaggerClip("zombie_hit_head_back_inplace.fbx", "zombie_hit_head_back_inplace");
        // staggerHeadLeft（头部硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadLeft = FindStaggerClip("zombie_hit_head_left_inplace.fbx", "zombie_hit_head_left_inplace");
        // staggerHeadRight（头部硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadRight = FindStaggerClip("zombie_hit_head_right_inplace.fbx", "zombie_hit_head_right_inplace");
        // staggerHeavyFront（重度硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyFront = FindStaggerClip("zombie_hit_heavy_front_inplace.fbx", "zombie_hit_heavy_front_inplace");
        // staggerHeavyBack（重度硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyBack = FindStaggerClip("zombie_hit_heavy_back_inplace.fbx", "zombie_hit_heavy_back_inplace");
        // staggerHeavyLeft（重度硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyLeft = FindStaggerClip("zombie_hit_heavy_left_inplace.fbx", "zombie_hit_heavy_left_inplace");
        // staggerHeavyRight（重度硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyRight = FindStaggerClip("zombie_hit_heavy_right_inplace.fbx", "zombie_hit_heavy_right_inplace");
        // TripOver → Crawl
        // tripKnee（绊倒膝盖）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tripKnee = FindClip("Zombie_Idle_TripOverToCrawlIdle_KneeHeight.fbx", "Zombie_Idle_TripOverToCrawlIdle_KneeHeight");
        // tripMid（绊倒中段）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tripMid = FindClip("Zombie_Idle_TripOverToCrawlIdle_MidHeight.fbx", "Zombie_Idle_TripOverToCrawlIdle_MidHeight");
        // Crawl
        // crawlIdle（爬行待机）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlIdle = FindClip("Zombie_Crawl_Idle.FBX", "Zombie_Crawl_Idle");
        // crawlForward（爬行前向）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlForward = FindClip("Zombie_Crawl_Forward.FBX", "Zombie_Crawl_Forward");
        // crawlBack（爬行后方）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlBack = FindClip("Zombie_Crawl_Back.FBX", "Zombie_Crawl_Back");
        // crawlIdleSearch（爬行待机搜索）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。
var crawlIdleSearch = FindClip("Zombie_Crawl_Idle_Search.FBX", "Zombie_Crawl_Idle_Search");
        // crawlAttack（爬行攻击）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlAttack = FindClip("Zombie_Crawl_Attack01.FBX", "Zombie_Crawl_Attack01");
        // crawlToStand（爬行目标站立）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlToStand = FindClip("Zombie_Crawl_CrawlIdle_To_Stand.FBX", "Zombie_Crawl_CrawlIdle_To_Stand");
        // Turn
        // turn180（转向180）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var turn180 = FindClip("Zombie_Turn_180.fbx", "Zombie_Turn_180");
        // turnL90（转向左侧90）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var turnL90 = FindClip("Zombie_Turn_Left90.fbx", "Zombie_Turn_Left90");
        // turnR90（转向右侧90）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var turnR90 = FindClip("Zombie_Turn_Right90.fbx", "Zombie_Turn_Right90");
        // Special
        // electricuted（电击倒地）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var electricuted = FindClip("Zombie_Idle_Electricuted.fbx", "Zombie_Idle_Electricuted");
        // Death
        // clipDeath（动画片段死亡）：保存 ZombieAnimatorSetup.FindClip(string, string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var clipDeath = FindClip("Zombie_Idle_Death.fbx", "Zombie_Idle_Death");
        // ========== 4. 构建 Locomotion BlendTree ==========
        // locoState（移动行为状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var locoState = sm.AddState("Locomotion", new Vector3(0, 0, 0));
        locoState.writeDefaultValues = false;
        // locoTree（移动行为行为树）：使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、ZombieAnimatorSetup.RunSetup()。
var locoTree = new BlendTree
        {
            name = "LocomotionTree"
        };
        locoTree.blendType = BlendTreeType.Simple1D;
        locoTree.blendParameter = "MoveSpeed";
        locoTree.useAutomaticThresholds = false;
        // Idle: 根据 IdleBlend 参数(0~1)在 Idle_01/02/03 之间混合
        // idleTree（待机行为树）：使用位置：作为 AssetDatabase.AddObjectToAsset(Object, Object) 的参数、作为 BlendTree.AddChild(Motion, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var idleTree = new BlendTree
        {
            name = "IdleBlend"
        };
        idleTree.blendType = BlendTreeType.Simple1D;
        idleTree.blendParameter = "IdleBlend";
        idleTree.useAutomaticThresholds = false;
        if (clipIdle01 != null)
        {
            idleTree.AddChild(clipIdle01, 0f);
        }

        if (clipIdle02 != null)
        {
            idleTree.AddChild(clipIdle02, 0.5f);
        }

        if (clipIdle03 != null)
        {
            idleTree.AddChild(clipIdle03, 1f);
        }

        locoTree.AddChild(idleTree, 0f);
        // Walk: 只保留走路区间，快走作为追击上限，彻底移除跑步与冲刺
        if (walk04 != null)
        {
            locoTree.AddChild(walk04, 0.2f);
        }

        if (walk01 != null)
        {
            locoTree.AddChild(walk01, 0.35f);
        }

        if (walk03 != null)
        {
            locoTree.AddChild(walk03, 0.43f);
        }

        if (walkFast != null)
        {
            locoTree.AddChild(walkFast, 0.5f);
        }

        locoState.motion = locoTree;
        AssetDatabase.AddObjectToAsset(locoTree, ctrl);
        AssetDatabase.AddObjectToAsset(idleTree, ctrl);
        sm.defaultState = locoState;
        // ========== 4b. Turn 状态 ==========
        // stTurn180（转向180动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、ZombieAnimatorSetup.RunSetup()。
var stTurn180 = sm.AddState("Turn_180", new Vector3(-200, 0, 0));
        stTurn180.motion = turn180;
        stTurn180.writeDefaultValues = false;
        // stTurnL（转向左侧动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、ZombieAnimatorSetup.RunSetup()。
var stTurnL = sm.AddState("Turn_Left90", new Vector3(-200, 60, 0));
        stTurnL.motion = turnL90;
        stTurnL.writeDefaultValues = false;
        // stTurnR（转向右侧动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、ZombieAnimatorSetup.RunSetup()。
var stTurnR = sm.AddState("Turn_Right90", new Vector3(-200, 120, 0));
        stTurnR.motion = turnR90;
        stTurnR.writeDefaultValues = false;
        // Turn → Locomotion (播完自动回去)
        // tTurn180（进度转向180）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tTurn180 = stTurn180.AddTransition(locoState);
        tTurn180.hasExitTime = true;
        tTurn180.exitTime = 0.9f;
        tTurn180.duration = 0.1f;
        // tTurnL（进度转向左侧）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tTurnL = stTurnL.AddTransition(locoState);
        tTurnL.hasExitTime = true;
        tTurnL.exitTime = 0.9f;
        tTurnL.duration = 0.1f;
        // tTurnR（进度转向右侧）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tTurnR = stTurnR.AddTransition(locoState);
        tTurnR.hasExitTime = true;
        tTurnR.exitTime = 0.9f;
        tTurnR.duration = 0.1f;
        // AnyState → Turn (用 TurnDir 参数: 0=none, 1=left90, 2=right90, 3=180)
        AddAnyStateTransition(sm, stTurnL, 0.05f, false, ("TurnDir", AnimatorConditionMode.Equals, 1f));
        AddAnyStateTransition(sm, stTurnR, 0.05f, false, ("TurnDir", AnimatorConditionMode.Equals, 2f));
        AddAnyStateTransition(sm, stTurn180, 0.05f, false, ("TurnDir", AnimatorConditionMode.Equals, 3f));
        // ========== 4c. Search Idle 状态 ==========
        // stSearch（动画状态搜索）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stSearch = sm.AddState("SearchIdle", new Vector3(200, 0, 0));
        stSearch.motion = clipIdleSearch;
        stSearch.writeDefaultValues = false;
        // SearchIdle ← Locomotion (SearchMode=true 且 MoveSpeed<0.1)
        // tToSearch（进度目标搜索）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tToSearch = locoState.AddTransition(stSearch);
        tToSearch.AddCondition(AnimatorConditionMode.If, 0, "SearchMode");
        tToSearch.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
        tToSearch.hasExitTime = false;
        tToSearch.duration = 0.2f;
        // Locomotion ← SearchIdle (SearchMode=false 或 MoveSpeed>0.1)
        // tFromSearch（进度来源搜索）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tFromSearch = stSearch.AddTransition(locoState);
        tFromSearch.AddCondition(AnimatorConditionMode.IfNot, 0, "SearchMode");
        tFromSearch.hasExitTime = false;
        tFromSearch.duration = 0.2f;
        // tFromSearch2（进度来源搜索2）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tFromSearch2 = stSearch.AddTransition(locoState);
        tFromSearch2.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
        tFromSearch2.hasExitTime = false;
        tFromSearch2.duration = 0.2f;
        // ========== 6. Attack 子状态机 ==========
        // attackSM（攻击状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorStateMachine) 的参数、ZombieAnimatorSetup.RunSetup()。
var attackSM = sm.AddStateMachine("Attack", new Vector3(0, 120, 0));
        attackSM.entryPosition = new Vector3(0, 0, 0);
        attackSM.anyStatePosition = new Vector3(300, 0, 0);
        attackSM.exitPosition = new Vector3(600, 0, 0);
        // 挂载攻击状态机退出通知行为
        attackSM.AddStateMachineBehaviour<AttackStateMachineSMB>();
        // stAtk01（攻击01动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stAtk01 = attackSM.AddState("Attack01", new Vector3(50, 50, 0));
        stAtk01.motion = attack01;
        stAtk01.writeDefaultValues = false;
        // stAtk02（攻击02动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stAtk02 = attackSM.AddState("Attack02", new Vector3(50, 100, 0));
        stAtk02.motion = attack02;
        stAtk02.writeDefaultValues = false;
        // stAtk03（攻击03动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stAtk03 = attackSM.AddState("Attack03", new Vector3(50, 150, 0));
        stAtk03.motion = attack03;
        stAtk03.writeDefaultValues = false;
        // Entry → Attack 状态（按 AttackIndex 选择）
        // Attack 不需要内部 AnyState，因为攻击不支持连击打断
        attackSM.defaultState = stAtk01;
        // entryAtk01（入口攻击01）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryAtk01 = attackSM.AddEntryTransition(stAtk01);
        entryAtk01.AddCondition(AnimatorConditionMode.Equals, 0f, "AttackIndex");
        // entryAtk02（入口攻击02）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryAtk02 = attackSM.AddEntryTransition(stAtk02);
        entryAtk02.AddCondition(AnimatorConditionMode.Equals, 1f, "AttackIndex");
        // entryAtk03（入口攻击03）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryAtk03 = attackSM.AddEntryTransition(stAtk03);
        entryAtk03.AddCondition(AnimatorConditionMode.Equals, 2f, "AttackIndex");
        // Attack → Exit（ATK=false 直接驱动退出，不使用固定退出时间窗口）
        // st（当前动画状态）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.RunSetup()。
foreach (var st in new[]
        {
            stAtk01,
            stAtk02,
            stAtk03
        }

        )
        {
            // tExit（进度退出）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tExit = st.AddExitTransition();
            tExit.hasExitTime = false;
            tExit.duration = 0.08f;
            tExit.AddCondition(AnimatorConditionMode.IfNot, 0, "ATK");
        }

        // ========== 7. HitReact 子状态机 ==========
        // staggerSM（硬直状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的参数、作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数 等共 5 处。
var staggerSM = sm.AddStateMachine("Stagger", new Vector3(-300, 240, 0));
        staggerSM.entryPosition = new Vector3(0, 0, 0);
        staggerSM.anyStatePosition = new Vector3(300, 0, 0);
        staggerSM.exitPosition = new Vector3(700, 0, 0);
        staggerSM.AddStateMachineBehaviour<HitReactStateMachineSMB>();
        // stStaggerBodyFront（身体硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数、ZombieAnimatorSetup.RunSetup()。
var stStaggerBodyFront = CreateStaggerState(staggerSM, "Stagger_Body_Front", staggerBodyFront, new Vector3(50, 0, 0), 0.88f);
        // stStaggerBodyBack（身体硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyBack = CreateStaggerState(staggerSM, "Stagger_Body_Back", staggerBodyBack, new Vector3(50, 50, 0), 0.88f);
        // stStaggerBodyLeft（身体硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyLeft = CreateStaggerState(staggerSM, "Stagger_Body_Left", staggerBodyLeft, new Vector3(50, 100, 0), 0.88f);
        // stStaggerBodyRight（身体硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyRight = CreateStaggerState(staggerSM, "Stagger_Body_Right", staggerBodyRight, new Vector3(50, 150, 0), 0.88f);
        // stStaggerHeadFront（头部硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadFront = CreateStaggerState(staggerSM, "Stagger_Head_Front", staggerHeadFront, new Vector3(250, 0, 0), 0.85f);
        // stStaggerHeadBack（头部硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadBack = CreateStaggerState(staggerSM, "Stagger_Head_Back", staggerHeadBack, new Vector3(250, 50, 0), 0.85f);
        // stStaggerHeadLeft（头部硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadLeft = CreateStaggerState(staggerSM, "Stagger_Head_Left", staggerHeadLeft, new Vector3(250, 100, 0), 0.85f);
        // stStaggerHeadRight（头部硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadRight = CreateStaggerState(staggerSM, "Stagger_Head_Right", staggerHeadRight, new Vector3(250, 150, 0), 0.85f);
        // stStaggerHeavyFront（重度硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyFront = CreateStaggerState(staggerSM, "Stagger_Heavy_Front", staggerHeavyFront, new Vector3(450, 0, 0), 0.9f);
        // stStaggerHeavyBack（重度硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyBack = CreateStaggerState(staggerSM, "Stagger_Heavy_Back", staggerHeavyBack, new Vector3(450, 50, 0), 0.9f);
        // stStaggerHeavyLeft（重度硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyLeft = CreateStaggerState(staggerSM, "Stagger_Heavy_Left", staggerHeavyLeft, new Vector3(450, 100, 0), 0.9f);
        // stStaggerHeavyRight（重度硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyRight = CreateStaggerState(staggerSM, "Stagger_Heavy_Right", staggerHeavyRight, new Vector3(450, 150, 0), 0.9f);
        AddStaggerEntry(staggerSM, stStaggerBodyFront, 0, 0);
        AddStaggerEntry(staggerSM, stStaggerBodyBack, 0, 1);
        AddStaggerEntry(staggerSM, stStaggerBodyLeft, 0, 2);
        AddStaggerEntry(staggerSM, stStaggerBodyRight, 0, 3);
        AddStaggerEntry(staggerSM, stStaggerHeadFront, 1, 0);
        AddStaggerEntry(staggerSM, stStaggerHeadBack, 1, 1);
        AddStaggerEntry(staggerSM, stStaggerHeadLeft, 1, 2);
        AddStaggerEntry(staggerSM, stStaggerHeadRight, 1, 3);
        AddStaggerEntry(staggerSM, stStaggerHeavyFront, 2, 0);
        AddStaggerEntry(staggerSM, stStaggerHeavyBack, 2, 1);
        AddStaggerEntry(staggerSM, stStaggerHeavyLeft, 2, 2);
        AddStaggerEntry(staggerSM, stStaggerHeavyRight, 2, 3);
        staggerSM.defaultState = stStaggerBodyFront;
        AddAnyStateTransition(sm, staggerSM, 0.03f, false, ("StaggerTrigger", AnimatorConditionMode.If, 0f));
        // hitSM（命中状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的参数、作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorStateMachine) 的参数、作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数 等共 4 处。
var hitSM = sm.AddStateMachine("HitReact", new Vector3(0, 240, 0));
        hitSM.entryPosition = new Vector3(0, 0, 0);
        hitSM.anyStatePosition = new Vector3(300, 0, 0);
        hitSM.exitPosition = new Vector3(600, 0, 0);
        // 挂载受击状态机退出通知行为
        hitSM.AddStateMachineBehaviour<HitReactStateMachineSMB>();
        // stHitHead（命中头部动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var stHitHead = hitSM.AddState("Hit_Head", new Vector3(50, 0, 0));
        stHitHead.motion = hitHead;
        stHitHead.writeDefaultValues = false;
        SetTimedHitComplete(stHitHead, 0.82f);
        // stHitLArm（命中左侧手臂动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var stHitLArm = hitSM.AddState("Hit_LeftArm", new Vector3(50, 50, 0));
        stHitLArm.motion = hitLArm;
        stHitLArm.writeDefaultValues = false;
        SetTimedHitComplete(stHitLArm, 0.82f);
        // stHitRArm（命中右侧手臂动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var stHitRArm = hitSM.AddState("Hit_RightArm", new Vector3(50, 100, 0));
        stHitRArm.motion = hitRArm;
        stHitRArm.writeDefaultValues = false;
        SetTimedHitComplete(stHitRArm, 0.82f);
        // stHitLLeg（命中左侧腿部动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var stHitLLeg = hitSM.AddState("Hit_LeftLeg", new Vector3(50, 150, 0));
        stHitLLeg.motion = hitLLeg;
        stHitLLeg.writeDefaultValues = false;
        SetTimedHitComplete(stHitLLeg, 0.95f);
        // stHitRLeg（命中右侧腿部动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数、作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.RunSetup()。
var stHitRLeg = hitSM.AddState("Hit_RightLeg", new Vector3(50, 200, 0));
        stHitRLeg.motion = hitRLeg;
        stHitRLeg.writeDefaultValues = false;
        SetTimedHitComplete(stHitRLeg, 0.95f);
        // TripOver 过渡状态（腿部受击后 → 爬行）
        // stTripKnee（绊倒膝盖动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stTripKnee = hitSM.AddState("TripOver_Knee", new Vector3(250, 150, 0));
        stTripKnee.motion = tripKnee;
        stTripKnee.writeDefaultValues = false;
        // stTripMid（绊倒中段动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stTripMid = hitSM.AddState("TripOver_Mid", new Vector3(250, 200, 0));
        stTripMid.motion = tripMid;
        stTripMid.writeDefaultValues = false;
        // Base Layer AnyState → HitReact SM（模块级入口，不穿透到内部状态）
        AddAnyStateTransition(sm, hitSM, 0.05f, false, ("HitTrigger", AnimatorConditionMode.If, 0f));
        // HitReact SM 内部：Entry 首次进入路由（按 HitDir 分发）
        hitSM.defaultState = stHitHead;
        // entryHitHead（入口命中头部）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryHitHead = hitSM.AddEntryTransition(stHitHead);
        entryHitHead.AddCondition(AnimatorConditionMode.Equals, 0f, "HitDir");
        // entryHitLArm（入口命中左侧手臂）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryHitLArm = hitSM.AddEntryTransition(stHitLArm);
        entryHitLArm.AddCondition(AnimatorConditionMode.Equals, 1f, "HitDir");
        // entryHitRArm（入口命中右侧手臂）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryHitRArm = hitSM.AddEntryTransition(stHitRArm);
        entryHitRArm.AddCondition(AnimatorConditionMode.Equals, 2f, "HitDir");
        // entryHitLLeg（入口命中左侧腿部）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryHitLLeg = hitSM.AddEntryTransition(stHitLLeg);
        entryHitLLeg.AddCondition(AnimatorConditionMode.Equals, 3f, "HitDir");
        // entryHitRLeg（入口命中右侧腿部）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var entryHitRLeg = hitSM.AddEntryTransition(stHitRLeg);
        entryHitRLeg.AddCondition(AnimatorConditionMode.Equals, 4f, "HitDir");
        // 受击动画 → Exit（HIT=false 直接驱动退出，不使用固定退出时间窗口）
        // st（当前动画状态）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.RunSetup()。
foreach (var st in new[]
        {
            stHitHead,
            stHitLArm,
            stHitRArm
        }

        )
        {
            // tExit（进度退出）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tExit = st.AddExitTransition();
            tExit.hasExitTime = false;
            tExit.duration = 0.08f;
            tExit.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
        }

        // 腿部受击 → TripOver → Exit（→ Crawl）
        // tLLegTrip（进度左侧腿部绊倒）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tLLegTrip = stHitLLeg.AddTransition(stTripKnee);
        tLLegTrip.hasExitTime = true;
        tLLegTrip.exitTime = 0.8f;
        tLLegTrip.duration = 0.15f;
        tLLegTrip.AddCondition(AnimatorConditionMode.If, 0, "CanTripOver");
        // tRLegTrip（进度右侧腿部绊倒）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tRLegTrip = stHitRLeg.AddTransition(stTripMid);
        tRLegTrip.hasExitTime = true;
        tRLegTrip.exitTime = 0.8f;
        tRLegTrip.duration = 0.15f;
        tRLegTrip.AddCondition(AnimatorConditionMode.If, 0, "CanTripOver");
        // st（当前动画状态）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.RunSetup()。
foreach (var st in new[]
        {
            stHitLLeg,
            stHitRLeg
        }

        )
        {
            // tLegExit（进度腿部退出）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tLegExit = st.AddExitTransition();
            tLegExit.hasExitTime = false;
            tLegExit.duration = 0.08f;
            tLegExit.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
            tLegExit.AddCondition(AnimatorConditionMode.IfNot, 0, "CanTripOver");
        }

        // TripOver → Exit（进入 Crawl SM）
        // tTripExit1（进度绊倒退出1）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tTripExit1 = stTripKnee.AddExitTransition();
        tTripExit1.hasExitTime = true;
        tTripExit1.exitTime = 0.9f;
        tTripExit1.duration = 0.15f;
        // tTripExit2（进度绊倒退出2）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tTripExit2 = stTripMid.AddExitTransition();
        tTripExit2.hasExitTime = true;
        tTripExit2.exitTime = 0.9f;
        tTripExit2.duration = 0.15f;
        // ========== 8. Crawl 子状态机 ==========
        // crawlSM（爬行状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorStateMachine) 的参数、作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorStateMachine) 的参数、ZombieAnimatorSetup.RunSetup()。
var crawlSM = sm.AddStateMachine("Crawl", new Vector3(0, 360, 0));
        crawlSM.entryPosition = new Vector3(0, 0, 0);
        crawlSM.anyStatePosition = new Vector3(300, 0, 0);
        crawlSM.exitPosition = new Vector3(600, 0, 0);
        // stCrawlIdle（爬行待机动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stCrawlIdle = crawlSM.AddState("CrawlIdle", new Vector3(50, 50, 0));
        stCrawlIdle.motion = crawlIdle;
        stCrawlIdle.writeDefaultValues = false;
        crawlSM.defaultState = stCrawlIdle;
        // stCrawlFwd（爬行前向动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stCrawlFwd = crawlSM.AddState("CrawlForward", new Vector3(50, 100, 0));
        stCrawlFwd.motion = crawlForward;
        stCrawlFwd.writeDefaultValues = false;
        // stCrawlBack（爬行后方动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stCrawlBack = crawlSM.AddState("CrawlBack", new Vector3(50, 150, 0));
        stCrawlBack.motion = crawlBack;
        stCrawlBack.writeDefaultValues = false;
        // stCrawlAtk（爬行攻击动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、作为 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stCrawlAtk = crawlSM.AddState("CrawlAttack", new Vector3(250, 50, 0));
        stCrawlAtk.motion = crawlAttack;
        stCrawlAtk.writeDefaultValues = false;
        // crawlAttackBehaviour（爬行攻击行为）：保存 AnimatorState.AddStateMachineBehaviour<EnemyCrawlAttackStateSMB>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var crawlAttackBehaviour = stCrawlAtk.AddStateMachineBehaviour<EnemyCrawlAttackStateSMB>();
        crawlAttackBehaviour.hitNormalizedTime = 0.55f;
        crawlAttackBehaviour.completeNormalizedTime = 0.95f;
        // stCrawlToStand（爬行目标站立动画状态的哈希编号）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorState.AddTransition(AnimatorState) 的参数、ZombieAnimatorSetup.RunSetup()。
var stCrawlToStand = crawlSM.AddState("CrawlToStand", new Vector3(250, 150, 0));
        stCrawlToStand.motion = crawlToStand;
        stCrawlToStand.writeDefaultValues = false;
        stCrawlToStand.AddStateMachineBehaviour<EnemyStandUpStateSMB>();
        // CrawlIdle → CrawlForward (MoveSpeed > 0.1)
        // RE4 风格：敌人始终面向玩家爬行，不使用 CrawlBack
        // tIdleFwd（进度待机前向）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tIdleFwd = stCrawlIdle.AddTransition(stCrawlFwd);
        tIdleFwd.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
        tIdleFwd.hasExitTime = false;
        tIdleFwd.duration = 0.15f;
        // CrawlIdle → CrawlBack (MoveSpeed > 0.1 且 MoveY < 0)
        // 保留但当前 AI 不会触发（敌人始终面向移动方向）
        // tIdleBack（进度待机后方）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tIdleBack = stCrawlIdle.AddTransition(stCrawlBack);
        tIdleBack.AddCondition(AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
        tIdleBack.AddCondition(AnimatorConditionMode.Less, -0.01f, "MoveY");
        tIdleBack.hasExitTime = false;
        tIdleBack.duration = 0.15f;
        // CrawlForward → CrawlIdle (MoveSpeed < 0.1)
        // tFwdIdle（进度前向待机）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tFwdIdle = stCrawlFwd.AddTransition(stCrawlIdle);
        tFwdIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
        tFwdIdle.hasExitTime = false;
        tFwdIdle.duration = 0.15f;
        // CrawlBack → CrawlIdle (MoveSpeed < 0.1)
        // tBackIdle（进度后方待机）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tBackIdle = stCrawlBack.AddTransition(stCrawlIdle);
        tBackIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
        tBackIdle.hasExitTime = false;
        tBackIdle.duration = 0.15f;
        // CrawlIdle → CrawlAttack (ATK == true)
        // tIdleAtk（进度待机攻击）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tIdleAtk = stCrawlIdle.AddTransition(stCrawlAtk);
        tIdleAtk.AddCondition(AnimatorConditionMode.If, 0, "ATK");
        tIdleAtk.hasExitTime = false;
        tIdleAtk.duration = 0.1f;
        // 爬行移动中也允许直接进入攻击，避免必须先经过爬行静止状态
        // tAnyCrawlAtk（进度任意爬行攻击）：保存 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tAnyCrawlAtk = crawlSM.AddAnyStateTransition(stCrawlAtk);
        tAnyCrawlAtk.AddCondition(AnimatorConditionMode.If, 0, "ATK");
        tAnyCrawlAtk.hasExitTime = false;
        tAnyCrawlAtk.duration = 0.1f;
        tAnyCrawlAtk.canTransitionToSelf = false;
        // CrawlAttack → CrawlIdle (ATK == false, 不使用固定退出时间窗口)
        // tAtkIdle（进度攻击待机）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tAtkIdle = stCrawlAtk.AddTransition(stCrawlIdle);
        tAtkIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "ATK");
        tAtkIdle.hasExitTime = false;
        tAtkIdle.duration = 0.08f;
        // CrawlIdle → CrawlToStand (IsCrawl == false)
        // tIdleStand（进度待机站立）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tIdleStand = stCrawlIdle.AddTransition(stCrawlToStand);
        tIdleStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawl");
        tIdleStand.hasExitTime = false;
        tIdleStand.duration = 0.2f;
        // CrawlToStand → Exit（回 Base Layer Locomotion），97% 开始退出
        // tStandExit（进度站立退出）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tStandExit = stCrawlToStand.AddExitTransition();
        tStandExit.hasExitTime = true;
        tStandExit.exitTime = 0.97f;
        tStandExit.duration = 0.08f;
        // ========== 9. Base Layer 主状态过渡 ==========
        // Locomotion → Attack SM (ATK == true)
        // tLocoAtk（进度移动行为攻击）：保存 AnimatorState.AddTransition(AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tLocoAtk = locoState.AddTransition(attackSM);
        tLocoAtk.AddCondition(AnimatorConditionMode.If, 0, "ATK");
        tLocoAtk.hasExitTime = false;
        tLocoAtk.duration = 0.1f;
        // Locomotion → Crawl SM (IsCrawl == true)
        // tLocoCrawl（进度移动行为爬行）：保存 AnimatorState.AddTransition(AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tLocoCrawl = locoState.AddTransition(crawlSM);
        tLocoCrawl.AddCondition(AnimatorConditionMode.If, 0, "IsCrawl");
        tLocoCrawl.hasExitTime = false;
        tLocoCrawl.duration = 0.2f;
        // HitReact SM → Locomotion (HIT == false)
        // 受击动画播完 Exit 后，如果不再僵直，回到 Locomotion
        // 注意：AddStateMachineTransition 返回 AnimatorTransition（非 AnimatorStateTransition），
        // 不支持 hasExitTime / duration。退出时序已由 SM 内部 Hit 动画的 hasExitTime=0.85 控制。
        // 加 IsCrawl=false 条件，使两条 SM Exit Transition 互斥：
        // IsCrawl=true → Crawl SM；IsCrawl=false → Locomotion
        // tHitLoco（进度命中移动行为）：保存 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tHitLoco = sm.AddStateMachineTransition(hitSM, locoState);
        tHitLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
        tHitLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawl");
        // tStaggerLoco（进度硬直移动行为）：保存 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tStaggerLoco = sm.AddStateMachineTransition(staggerSM, locoState);
        tStaggerLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
        tStaggerLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrawl");
        // HitReact SM → Crawl SM (IsCrawl == true)
        // 腿部受击倒地后，Exit 直接进入 Crawl，不经过 Locomotion
        // tHitCrawl（进度命中爬行）：保存 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tHitCrawl = sm.AddStateMachineTransition(hitSM, crawlSM);
        tHitCrawl.AddCondition(AnimatorConditionMode.If, 0, "IsCrawl");
        // AnyState → Death (DIE == true)
        // deathState（死亡状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、ZombieAnimatorSetup.RunSetup()。
var deathState = sm.AddState("Death", new Vector3(400, 60, 0));
        deathState.motion = clipDeath;
        deathState.writeDefaultValues = false;
        // Electricuted
        // stElec（动画状态电击）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、ZombieAnimatorSetup.RunSetup()。
var stElec = sm.AddState("Electricuted", new Vector3(400, 120, 0));
        stElec.motion = electricuted;
        stElec.writeDefaultValues = false;
        // tElecExit（进度电击退出）：保存 AnimatorState.AddTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunSetup()。
var tElecExit = stElec.AddTransition(locoState);
        tElecExit.hasExitTime = true;
        tElecExit.exitTime = 0.9f;
        tElecExit.duration = 0.1f;
        AddAnyStateTransition(sm, stElec, 0.05f, false, ("Stunned", AnimatorConditionMode.If, 0f));
        AddAnyStateTransition(sm, deathState, 0.1f, false, ("DIE", AnimatorConditionMode.If, 0f));
        // ========== 10. 保存 ==========
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ZombieSetup] 状态机构建完成！");
        Debug.Log($"[ZombieSetup] 参数: {ctrl.parameters.Length}个");
        Debug.Log($"[ZombieSetup] Base Layer 状态: {sm.states.Length}个, 子状态机: {sm.stateMachines.Length}个");
    }

    /// <summary>RunChainsawSetup 的业务处理入口；本方法直接依赖 AnimatorController.AddParameter(AnimatorControllerParameter)、AnimatorControllerParameter.AnimatorControllerParameter()、AnimatorStateMachine.AddStateMachine(string, Vector3)、AnimatorStateMachine.AddStateMachineBehaviour&lt;HitReactStateMachineSMB&gt;()、AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetupMenu()；下游（含内部回调体） AnimatorController.AddParameter(AnimatorControllerParameter)、AnimatorControllerParameter.AnimatorControllerParameter()、AnimatorStateMachine.AddStateMachine(string, Vector3)、AnimatorStateMachine.AddStateMachineBehaviour&lt;HitReactStateMachineSMB&gt;()、AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void RunChainsawSetup()
    {
        // ctrl（角色控制器）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType) 的参数、ZombieAnimatorSetup.RunChainsawSetup()。
var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CHAINSAW_CTRL_PATH);
        if (ctrl == null)
        {
            Debug.LogError("[ChainsawManSetup] 找不到控制器: " + CHAINSAW_CTRL_PATH);
            return;
        }

        AddParam(ctrl, "HIT", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "DIE", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "HitTrigger", AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, "HitDir", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "StaggerTrigger", AnimatorControllerParameterType.Trigger);
        AddParam(ctrl, "StaggerDirection", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "StaggerLevel", AnimatorControllerParameterType.Int);
        AddParam(ctrl, "CanTripOver", AnimatorControllerParameterType.Bool);
        AddParam(ctrl, "HealthRatio", AnimatorControllerParameterType.Float);
        // sm（动画状态机）：使用位置：作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 ZombieAnimatorSetup.FindStateByName(AnimatorStateMachine, string) 的参数、作为 ZombieAnimatorSetup.RemoveAnyStateTransitionsUsingParameter(AnimatorStateMachine, string) 的参数 等共 5 处。
var sm = ctrl.layers[0].stateMachine;
        // cadence（射击节奏）：使用位置：作为 AnimatorController.AddParameter(AnimatorControllerParameter) 的参数、作为 IEnumerable<AnimatorControllerParameter>.Any<AnimatorControllerParameter>(Func<AnimatorControllerParameter, bool>) 的参数、ZombieAnimatorSetup.RunChainsawSetup()。
const string cadence = "LocomotionPlaybackRate";
        // movement（移动）：使用位置：作为 EditorUtility.SetDirty(Object) 的参数、ZombieAnimatorSetup.RunChainsawSetup()。
var movement = sm.states.FirstOrDefault( /* 中文含义：当前子级动画控制器状态。 */item => item.state.name == "Locomotion").state;
        if (movement != null)
        {
            if (!ctrl.parameters.Any( /* 中文含义：当前动画控制器控制器参数。 */item => item.name == cadence))
            {
                ctrl.AddParameter(new AnimatorControllerParameter { name = cadence, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            }

            movement.speedParameter = cadence;
            movement.speedParameterActive = true;
            EditorUtility.SetDirty(movement);
        }

        RemoveChildStateMachineByName(sm, "Stagger");
        RemoveAnyStateTransitionsUsingParameter(sm, "StaggerTrigger");
        // staggerBodyFront（身体硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyFront = FindStaggerClip("zombie_hit_body_front_inplace.fbx", "zombie_hit_body_front_inplace");
        // staggerBodyBack（身体硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyBack = FindStaggerClip("zombie_hit_body_back_inplace.fbx", "zombie_hit_body_back_inplace");
        // staggerBodyLeft（身体硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyLeft = FindStaggerClip("zombie_hit_body_left_inplace.fbx", "zombie_hit_body_left_inplace");
        // staggerBodyRight（身体硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerBodyRight = FindStaggerClip("zombie_hit_body_right_inplace.fbx", "zombie_hit_body_right_inplace");
        // staggerHeadFront（头部硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadFront = FindStaggerClip("zombie_hit_head_front_inplace.fbx", "zombie_hit_head_front_inplace");
        // staggerHeadBack（头部硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadBack = FindStaggerClip("zombie_hit_head_back_inplace.fbx", "zombie_hit_head_back_inplace");
        // staggerHeadLeft（头部硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadLeft = FindStaggerClip("zombie_hit_head_left_inplace.fbx", "zombie_hit_head_left_inplace");
        // staggerHeadRight（头部硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeadRight = FindStaggerClip("zombie_hit_head_right_inplace.fbx", "zombie_hit_head_right_inplace");
        // staggerHeavyFront（重度硬直前方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyFront = FindStaggerClip("zombie_hit_heavy_front_inplace.fbx", "zombie_hit_heavy_front_inplace");
        // staggerHeavyBack（重度硬直后方）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyBack = FindStaggerClip("zombie_hit_heavy_back_inplace.fbx", "zombie_hit_heavy_back_inplace");
        // staggerHeavyLeft（重度硬直左侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyLeft = FindStaggerClip("zombie_hit_heavy_left_inplace.fbx", "zombie_hit_heavy_left_inplace");
        // staggerHeavyRight（重度硬直右侧）：保存 ZombieAnimatorSetup.FindStaggerClip(string, string) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的参数。
var staggerHeavyRight = FindStaggerClip("zombie_hit_heavy_right_inplace.fbx", "zombie_hit_heavy_right_inplace");
        // staggerSM（硬直状态机）：保存 AnimatorStateMachine.AddStateMachine(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的参数、作为 ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[]) 的参数、作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数 等共 5 处。
var staggerSM = sm.AddStateMachine("Stagger", new Vector3(-300, 300, 0));
        staggerSM.entryPosition = new Vector3(0, 0, 0);
        staggerSM.anyStatePosition = new Vector3(300, 0, 0);
        staggerSM.exitPosition = new Vector3(600, 0, 0);
        staggerSM.AddStateMachineBehaviour<HitReactStateMachineSMB>();
        // stStaggerBodyFront（身体硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数、ZombieAnimatorSetup.RunChainsawSetup()。
var stStaggerBodyFront = CreateStaggerState(staggerSM, "Stagger_Body_Front", staggerBodyFront, new Vector3(50, 0, 0), 0.82f);
        // stStaggerBodyBack（身体硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyBack = CreateStaggerState(staggerSM, "Stagger_Body_Back", staggerBodyBack, new Vector3(50, 50, 0), 0.82f);
        // stStaggerBodyLeft（身体硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyLeft = CreateStaggerState(staggerSM, "Stagger_Body_Left", staggerBodyLeft, new Vector3(50, 100, 0), 0.82f);
        // stStaggerBodyRight（身体硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerBodyRight = CreateStaggerState(staggerSM, "Stagger_Body_Right", staggerBodyRight, new Vector3(50, 150, 0), 0.82f);
        // stStaggerHeadFront（头部硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadFront = CreateStaggerState(staggerSM, "Stagger_Head_Front", staggerHeadFront, new Vector3(250, 0, 0), 0.85f);
        // stStaggerHeadBack（头部硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadBack = CreateStaggerState(staggerSM, "Stagger_Head_Back", staggerHeadBack, new Vector3(250, 50, 0), 0.85f);
        // stStaggerHeadLeft（头部硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadLeft = CreateStaggerState(staggerSM, "Stagger_Head_Left", staggerHeadLeft, new Vector3(250, 100, 0), 0.85f);
        // stStaggerHeadRight（头部硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeadRight = CreateStaggerState(staggerSM, "Stagger_Head_Right", staggerHeadRight, new Vector3(250, 150, 0), 0.85f);
        // stStaggerHeavyFront（重度硬直前方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyFront = CreateStaggerState(staggerSM, "Stagger_Heavy_Front", staggerHeavyFront, new Vector3(450, 0, 0), 0.9f);
        // stStaggerHeavyBack（重度硬直后方动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyBack = CreateStaggerState(staggerSM, "Stagger_Heavy_Back", staggerHeavyBack, new Vector3(450, 50, 0), 0.9f);
        // stStaggerHeavyLeft（重度硬直左侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyLeft = CreateStaggerState(staggerSM, "Stagger_Heavy_Left", staggerHeavyLeft, new Vector3(450, 100, 0), 0.9f);
        // stStaggerHeavyRight（重度硬直右侧动画状态的哈希编号）：保存 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int) 的参数。
var stStaggerHeavyRight = CreateStaggerState(staggerSM, "Stagger_Heavy_Right", staggerHeavyRight, new Vector3(450, 150, 0), 0.9f);
        AddStaggerEntry(staggerSM, stStaggerBodyFront, 0, 0);
        AddStaggerEntry(staggerSM, stStaggerBodyBack, 0, 1);
        AddStaggerEntry(staggerSM, stStaggerBodyLeft, 0, 2);
        AddStaggerEntry(staggerSM, stStaggerBodyRight, 0, 3);
        AddStaggerEntry(staggerSM, stStaggerHeadFront, 1, 0);
        AddStaggerEntry(staggerSM, stStaggerHeadBack, 1, 1);
        AddStaggerEntry(staggerSM, stStaggerHeadLeft, 1, 2);
        AddStaggerEntry(staggerSM, stStaggerHeadRight, 1, 3);
        AddStaggerEntry(staggerSM, stStaggerHeavyFront, 2, 0);
        AddStaggerEntry(staggerSM, stStaggerHeavyBack, 2, 1);
        AddStaggerEntry(staggerSM, stStaggerHeavyLeft, 2, 2);
        AddStaggerEntry(staggerSM, stStaggerHeavyRight, 2, 3);
        staggerSM.defaultState = stStaggerBodyFront;
        AddAnyStateTransition(sm, staggerSM, 0.03f, false, ("StaggerTrigger", AnimatorConditionMode.If, 0f));
        // locomotionState（移动行为状态）：保存 ZombieAnimatorSetup.FindStateByName(AnimatorStateMachine, string) 的返回结果，供当前步骤使用。使用位置：作为 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的参数、ZombieAnimatorSetup.RunChainsawSetup()。
var locomotionState = FindStateByName(sm, "Locomotion");
        if (locomotionState != null)
        {
            // tStaggerLoco（进度硬直移动行为）：保存 AnimatorStateMachine.AddStateMachineTransition(AnimatorStateMachine, AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RunChainsawSetup()。
var tStaggerLoco = sm.AddStateMachineTransition(staggerSM, locomotionState);
            tStaggerLoco.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ChainsawManSetup] 硬直状态机配置完成");
    }

    /// <summary>
    /// 创建 2D 方向 BlendTree（8方向）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BlendTree.AddChild(Motion, Vector2)、BlendTree.BlendTree()、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="paramX">中文含义：参数横向。使用位置：ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="paramY">中文含义：参数纵向。使用位置：ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="forward">中文含义：前向。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="back">中文含义：后方。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="left">中文含义：左侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="right">中文含义：右侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="forwardLeft">中文含义：前向左侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="forwardRight">中文含义：前向右侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="backLeft">中文含义：后方左侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    /// <param name="backRight">中文含义：后方右侧。使用位置：作为 BlendTree.AddChild(Motion, Vector2) 的参数、ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。</param>
    static BlendTree CreateDirectionalBlendTree(string name, string paramX, string paramY, AnimationClip forward, AnimationClip back, AnimationClip left, AnimationClip right, AnimationClip forwardLeft, AnimationClip forwardRight, AnimationClip backLeft, AnimationClip backRight)
    {
        // tree（行为树）：使用位置：ZombieAnimatorSetup.CreateDirectionalBlendTree(string, string, string, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip, AnimationClip)。
var tree = new BlendTree
        {
            name = name
        };
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = paramX;
        tree.blendParameterY = paramY;
        tree.useAutomaticThresholds = false;
        // 8方向 + 中心
        //          Forward (0, 1)
        //    FL(-0.7, 0.7)    FR(0.7, 0.7)
        // Left(-1, 0)    Center(0,0)   Right(1, 0)
        //    BL(-0.7, -0.7)   BR(0.7, -0.7)
        //          Back (0, -1)
        if (forward != null)
        {
            tree.AddChild(forward, new Vector2(0, 1));
        }

        if (back != null)
        {
            tree.AddChild(back, new Vector2(0, -1));
        }

        if (left != null)
        {
            tree.AddChild(left, new Vector2(-1, 0));
        }

        if (right != null)
        {
            tree.AddChild(right, new Vector2(1, 0));
        }

        if (forwardLeft != null)
        {
            tree.AddChild(forwardLeft, new Vector2(-0.7f, 0.7f));
        }

        if (forwardRight != null)
        {
            tree.AddChild(forwardRight, new Vector2(0.7f, 0.7f));
        }

        if (backLeft != null)
        {
            tree.AddChild(backLeft, new Vector2(-0.7f, -0.7f));
        }

        if (backRight != null)
        {
            tree.AddChild(backRight, new Vector2(0.7f, -0.7f));
        }

        return tree;
    }

    // ========== 辅助方法 ==========
    /// <summary>AddParam 的业务处理入口；本方法直接依赖 AnimatorController.AddParameter(string, AnimatorControllerParameterType)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorController.AddParameter(string, AnimatorControllerParameterType)、IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctrl">中文含义：角色控制器。使用位置：ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType)。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 AnimatorController.AddParameter(string, AnimatorControllerParameterType) 的参数、作为 IEnumerable&lt;AnimatorControllerParameter&gt;.Any&lt;AnimatorControllerParameter&gt;(Func&lt;AnimatorControllerParameter, bool&gt;) 的参数、ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType)。</param>
    /// <param name="type">中文含义：当前动画控制器控制器参数类型。使用位置：作为 AnimatorController.AddParameter(string, AnimatorControllerParameterType) 的参数、ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType)。</param>
    static void AddParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        if (ctrl.parameters.Any( /* 中文含义：当前动画控制器控制器参数。 */p => p.name == name))
        {
            // 更新类型
            // arr（数组）：使用位置：ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType)。
var arr = ctrl.parameters;
            // i（当前遍历索引）：使用位置：ZombieAnimatorSetup.AddParam(AnimatorController, string, AnimatorControllerParameterType)。
for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].name == name)
                {
                    arr[i].type = type;
                    break;
                }
            }

            ctrl.parameters = arr;
            return;
        }

        ctrl.AddParameter(name, type);
    }

    /// <summary>FindStaggerClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)、string.StartsWith(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AssetDatabase.LoadAllAssetsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)、string.StartsWith(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fbxFileName">中文含义：模型文件文件名称。使用位置：作为 Debug.LogWarning(object) 的参数、ZombieAnimatorSetup.FindStaggerClip(string, string)。</param>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 Debug.LogWarning(object) 的参数、作为 string.IsNullOrEmpty(string) 的参数、ZombieAnimatorSetup.FindStaggerClip(string, string)。</param>
    static AnimationClip FindStaggerClip(string fbxFileName, string clipName)
    {
        // path（路径）：使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数。
string path = STAGGER_ANIM_DIR + "/" + fbxFileName;
        // objs（对象集合）：保存 AssetDatabase.LoadAllAssetsAtPath(string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.FindStaggerClip(string, string)。
var objs = AssetDatabase.LoadAllAssetsAtPath(path);
        // firstClip（首个动画片段）：使用位置：ZombieAnimatorSetup.FindStaggerClip(string, string)。
AnimationClip firstClip = null;
        // o（当前对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.FindStaggerClip(string, string)。
foreach (var o in objs)
        {
            if (o is AnimationClip /* 中文含义：动画片段。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ clip && !clip.name.StartsWith("__preview__"))
            {
                if (firstClip == null)
                {
                    firstClip = clip;
                }

                if (string.IsNullOrEmpty(clipName) || clip.name == clipName)
                {
                    return clip;
                }
            }
        }

        if (firstClip != null)
        {
            return firstClip;
        }

        Debug.LogWarning($"[ZombieSetup] 找不到硬直动画剪辑: {fbxFileName} -> {clipName}");
        return null;
    }

    /// <summary>CreateStaggerState 的业务处理入口；本方法直接依赖 AnimatorState.AddExitTransition()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorState.AddExitTransition()、AnimatorStateMachine.AddState(string, Vector3)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)、ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    /// <param name="clip">中文含义：动画片段。使用位置：ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 AnimatorStateMachine.AddState(string, Vector3) 的参数。</param>
    /// <param name="completeNormalizedTime">中文含义：完成归一化时间。使用位置：作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数。</param>
    static AnimatorState CreateStaggerState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip, Vector3 position, float completeNormalizedTime)
    {
        // state（当前动画状态）：保存 AnimatorStateMachine.AddState(string, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float) 的参数、ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)。
var state = stateMachine.AddState(stateName, position);
        state.motion = clip;
        state.writeDefaultValues = false;
        state.speed = STAGGER_STATE_SPEED;
        state.tag = "BodyTechniqueEntry";
        SetTimedHitComplete(state, completeNormalizedTime);
        // exit（退出）：保存 AnimatorState.AddExitTransition() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)。
var exit = state.AddExitTransition();
        exit.hasExitTime = false;
        exit.duration = 0.18f;
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, "HIT");
        return state;
    }

    /// <summary>AddStaggerEntry 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddEntryTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.AddEntryTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int)。</param>
    /// <param name="state">中文含义：当前动画状态。使用位置：作为 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的参数。</param>
    /// <param name="level">中文含义：级别。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。</param>
    static void AddStaggerEntry(AnimatorStateMachine stateMachine, AnimatorState state, int level, int direction)
    {
        // transition（过渡）：保存 AnimatorStateMachine.AddEntryTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.AddStaggerEntry(AnimatorStateMachine, AnimatorState, int, int)。
var transition = stateMachine.AddEntryTransition(state);
        transition.AddCondition(AnimatorConditionMode.Equals, level, "StaggerLevel");
        transition.AddCondition(AnimatorConditionMode.Equals, direction, "StaggerDirection");
    }

    /// <summary>SetTimedHitComplete 的业务处理入口；本方法直接依赖 AnimatorState.AddStateMachineBehaviour&lt;EnemyTimedHitStateSMB&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.CreateStaggerState(AnimatorStateMachine, string, AnimationClip, Vector3, float)、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorState.AddStateMachineBehaviour&lt;EnemyTimedHitStateSMB&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">中文含义：当前动画状态。使用位置：ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)。</param>
    /// <param name="completeNormalizedTime">中文含义：完成归一化时间。使用位置：ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)。</param>
    static void SetTimedHitComplete(AnimatorState state, float completeNormalizedTime)
    {
        // behaviour（行为）：保存 AnimatorState.AddStateMachineBehaviour<EnemyTimedHitStateSMB>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.SetTimedHitComplete(AnimatorState, float)。
var behaviour = state.AddStateMachineBehaviour<EnemyTimedHitStateSMB>();
        behaviour.completeNormalizedTime = completeNormalizedTime;
    }

    /// <summary>FindClip 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAllAssetRepresentationsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AssetDatabase.LoadAllAssetRepresentationsAtPath(string)、Debug.LogWarning(object)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fbxFileName">中文含义：模型文件文件名称。使用位置：作为 Debug.LogWarning(object) 的参数、ZombieAnimatorSetup.FindClip(string, string)。</param>
    /// <param name="clipName">中文含义：动画片段名称。使用位置：作为 Debug.LogWarning(object) 的参数、作为 string.IsNullOrEmpty(string) 的参数、ZombieAnimatorSetup.FindClip(string, string)。</param>
    static AnimationClip FindClip(string fbxFileName, string clipName)
    {
        // path（路径）：使用位置：作为 AssetDatabase.LoadAllAssetRepresentationsAtPath(string) 的参数。
string path = ANIM_DIR + "/" + fbxFileName;
        // objs（对象集合）：保存 AssetDatabase.LoadAllAssetRepresentationsAtPath(string) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.FindClip(string, string)。
var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        // o（当前对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.FindClip(string, string)。
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

        Debug.LogWarning($"[ZombieSetup] 找不到动画剪辑: {fbxFileName} -> {clipName}");
        return null;
    }

    /// <summary>AddAnyStateTransition 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.AddAnyStateTransition(AnimatorState)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="dest">中文含义：目的地。使用位置：作为 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="hasExitTime">中文含义：是否具有退出时间。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="conds">中文含义：条件集合。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState dest, float duration, bool hasExitTime, params (string /* 中文含义：过渡条件使用的动画参数名称。 */ param, AnimatorConditionMode /* 中文含义：过渡条件使用的比较模式。 */ mode, float /* 中文含义：过渡条件的比较阈值。 */ threshold)[] conds)
    {
        // t（当前动画控制器状态过渡）：保存 AnimatorStateMachine.AddAnyStateTransition(AnimatorState) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorState, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。
var t = sm.AddAnyStateTransition(dest);
        t.duration = duration;
        t.hasExitTime = hasExitTime;
        // c（当前组合结果）：每轮从条件集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。
foreach (var c in conds)
        {
            t.AddCondition(c.mode, c.threshold, c.param);
        }

        t.canTransitionToSelf = false;
        return t;
    }

    /// <summary>AddAnyStateTransition 的业务处理入口；本方法直接依赖 AnimatorStateMachine.AddAnyStateTransition(AnimatorStateMachine)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()、ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.AddAnyStateTransition(AnimatorStateMachine)、AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="dest">中文含义：目的地。使用位置：作为 AnimatorStateMachine.AddAnyStateTransition(AnimatorStateMachine) 的参数。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="hasExitTime">中文含义：是否具有退出时间。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    /// <param name="conds">中文含义：条件集合。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。</param>
    static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine sm, AnimatorStateMachine dest, float duration, bool hasExitTime, params (string /* 中文含义：过渡条件使用的动画参数名称。 */ param, AnimatorConditionMode /* 中文含义：过渡条件使用的比较模式。 */ mode, float /* 中文含义：过渡条件的比较阈值。 */ threshold)[] conds)
    {
        // t（当前动画控制器状态过渡）：保存 AnimatorStateMachine.AddAnyStateTransition(AnimatorStateMachine) 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.AddAnyStateTransition(AnimatorStateMachine, AnimatorStateMachine, float, bool, params (string param, AnimatorConditionMode mode, float threshold)[])。
var t = sm.AddAnyStateTransition(dest);
        t.duration = duration;
        t.hasExitTime = hasExitTime;
        // c（当前组合结果）：每轮从条件集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorTransitionBase.AddCondition(AnimatorConditionMode, float, string) 的参数。
foreach (var c in conds)
        {
            t.AddCondition(c.mode, c.threshold, c.param);
        }

        t.canTransitionToSelf = false;
        return t;
    }

    /// <summary>FindStateByName 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：ZombieAnimatorSetup.FindStateByName(AnimatorStateMachine, string)。</param>
    /// <param name="stateName">中文含义：动画状态名称。使用位置：ZombieAnimatorSetup.FindStateByName(AnimatorStateMachine, string)。</param>
    static AnimatorState FindStateByName(AnimatorStateMachine stateMachine, string stateName)
    {
        // childState（子级状态）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：ZombieAnimatorSetup.FindStateByName(AnimatorStateMachine, string)。
foreach (var childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        return null;
    }

    /// <summary>RemoveChildStateMachineByName 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、IEnumerable&lt;ChildAnimatorStateMachine&gt;.ToList&lt;ChildAnimatorStateMachine&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、IEnumerable&lt;ChildAnimatorStateMachine&gt;.ToList&lt;ChildAnimatorStateMachine&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：ZombieAnimatorSetup.RemoveChildStateMachineByName(AnimatorStateMachine, string)。</param>
    /// <param name="childName">中文含义：子级名称。使用位置：ZombieAnimatorSetup.RemoveChildStateMachineByName(AnimatorStateMachine, string)。</param>
    static void RemoveChildStateMachineByName(AnimatorStateMachine stateMachine, string childName)
    {
        // children（子对象集合）：保存 IEnumerable<ChildAnimatorStateMachine>.ToList<ChildAnimatorStateMachine>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RemoveChildStateMachineByName(AnimatorStateMachine, string)。
var children = stateMachine.stateMachines.ToList();
        // child（子级）：每轮从子对象集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine) 的参数、ZombieAnimatorSetup.RemoveChildStateMachineByName(AnimatorStateMachine, string)。
foreach (var child in children)
        {
            if (child.stateMachine != null && child.stateMachine.name == childName)
            {
                stateMachine.RemoveStateMachine(child.stateMachine);
            }
        }
    }

    /// <summary>RemoveAnyStateTransitionsUsingParameter 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorCondition&gt;.Any&lt;AnimatorCondition&gt;(Func&lt;AnimatorCondition, bool&gt;)、IEnumerable&lt;AnimatorStateTransition&gt;.ToList&lt;AnimatorStateTransition&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunChainsawSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、IEnumerable&lt;AnimatorCondition&gt;.Any&lt;AnimatorCondition&gt;(Func&lt;AnimatorCondition, bool&gt;)、IEnumerable&lt;AnimatorStateTransition&gt;.ToList&lt;AnimatorStateTransition&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：ZombieAnimatorSetup.RemoveAnyStateTransitionsUsingParameter(AnimatorStateMachine, string)。</param>
    /// <param name="parameterName">中文含义：参数名称。使用位置：作为 IEnumerable&lt;AnimatorCondition&gt;.Any&lt;AnimatorCondition&gt;(Func&lt;AnimatorCondition, bool&gt;) 的参数。</param>
    static void RemoveAnyStateTransitionsUsingParameter(AnimatorStateMachine stateMachine, string parameterName)
    {
        // transitions（过渡集合）：保存 IEnumerable<AnimatorStateTransition>.ToList<AnimatorStateTransition>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.RemoveAnyStateTransitionsUsingParameter(AnimatorStateMachine, string)。
var transitions = stateMachine.anyStateTransitions.ToList();
        // transition（过渡）：每轮从过渡集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数、ZombieAnimatorSetup.RemoveAnyStateTransitionsUsingParameter(AnimatorStateMachine, string)。
foreach (var transition in transitions)
        {
            if (transition.conditions.Any( /* 中文含义：当前动画控制器条件。 */c => c.parameter == parameterName))
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }
    }

    /// <summary>ClearStateMachine 的业务处理入口；本方法直接依赖 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveEntryTransition(AnimatorTransition)、AnimatorStateMachine.RemoveState(AnimatorState)、AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、IEnumerable&lt;AnimatorStateTransition&gt;.ToList&lt;AnimatorStateTransition&gt;()、IEnumerable&lt;AnimatorTransition&gt;.ToList&lt;AnimatorTransition&gt;()、IEnumerable&lt;ChildAnimatorState&gt;.ToList&lt;ChildAnimatorState&gt;() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAnimatorSetup.RunSetup()；下游（含内部回调体） AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition)、AnimatorStateMachine.RemoveEntryTransition(AnimatorTransition)、AnimatorStateMachine.RemoveState(AnimatorState)、AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine)、IEnumerable&lt;AnimatorStateTransition&gt;.ToList&lt;AnimatorStateTransition&gt;()、IEnumerable&lt;AnimatorTransition&gt;.ToList&lt;AnimatorTransition&gt;()、IEnumerable&lt;ChildAnimatorState&gt;.ToList&lt;ChildAnimatorState&gt;() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sm">中文含义：动画状态机。使用位置：ZombieAnimatorSetup.ClearStateMachine(AnimatorStateMachine)。</param>
    static void ClearStateMachine(AnimatorStateMachine sm)
    {
        // 删除所有子状态
        // states（状态集合）：保存 IEnumerable<ChildAnimatorState>.ToList<ChildAnimatorState>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.ClearStateMachine(AnimatorStateMachine)。
var states = sm.states.ToList();
        // s（当前子级动画控制器状态）：每轮从状态集合取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveState(AnimatorState) 的参数。
foreach (var s in states)
        {
            sm.RemoveState(s.state);
        }

        // 删除所有子状态机
        // subSMs（子级缩放毫秒）：保存 IEnumerable<ChildAnimatorStateMachine>.ToList<ChildAnimatorStateMachine>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.ClearStateMachine(AnimatorStateMachine)。
var subSMs = sm.stateMachines.ToList();
        // s（当前子级动画控制器状态机）：每轮从子级缩放毫秒取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveStateMachine(AnimatorStateMachine) 的参数。
foreach (var s in subSMs)
        {
            sm.RemoveStateMachine(s.stateMachine);
        }

        // 删除 AnyState 过渡
        // anyTrans（任意变换）：保存 IEnumerable<AnimatorStateTransition>.ToList<AnimatorStateTransition>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.ClearStateMachine(AnimatorStateMachine)。
var anyTrans = sm.anyStateTransitions.ToList();
        // t（当前动画控制器状态过渡）：每轮从任意变换取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveAnyStateTransition(AnimatorStateTransition) 的参数。
foreach (var t in anyTrans)
        {
            sm.RemoveAnyStateTransition(t);
        }

        // 删除 Entry 过渡
        // entryTrans（入口变换）：保存 IEnumerable<AnimatorTransition>.ToList<AnimatorTransition>() 的返回结果，供当前步骤使用。使用位置：ZombieAnimatorSetup.ClearStateMachine(AnimatorStateMachine)。
var entryTrans = sm.entryTransitions.ToList();
        // t（当前动画控制器过渡）：每轮从入口变换取得一个元素，仅代表当前这一项。使用位置：作为 AnimatorStateMachine.RemoveEntryTransition(AnimatorTransition) 的参数。
foreach (var t in entryTrans)
        {
            sm.RemoveEntryTransition(t);
        }
    }
}
