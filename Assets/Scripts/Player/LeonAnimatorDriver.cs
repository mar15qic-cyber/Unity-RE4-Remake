using UnityEngine;
using System;

/// <summary>
/// 受伤显示模式。两个受伤层的职责互斥：
/// 全身层（Injured Movement）负责单手武器非瞄准的完整受伤表演；
/// 下身层（Injured Lower Body）负责长枪与手枪瞄准时的下身受伤步态。
/// </summary>
public enum InjuredPresentationMode
{
    /// <summary>中文含义：普通状态。未受伤：两个受伤层均为零</summary>
    Normal = 0,
    /// <summary>中文含义：单手全身。手枪或小刀非瞄准：全身受伤层为一，下身层为零</summary>
    SingleHandFullBody = 1,
    /// <summary>中文含义：下半身仅。手枪瞄准/射击或长枪：全身层为零，下身层为一</summary>
    LowerBodyOnly = 2,
    /// <summary>中文含义：已抑制。蹲伏：两个受伤层均为零</summary>
    Suppressed = 3
}

/// <summary>
/// 里昂动画控制器运行驱动器。
/// 接收玩家动作和移动链的表现意图，写入 Animator 参数、图层权重并播放对应动画。
/// 动画事件继续交给现有动作事务校验和业务宿主处理。
/// </summary>
/// <remarks>玩家 Animator 表现适配器，接收控制器、移动和动作协调链的参数与播放意图；层索引、动画参数和事件方法保留既有绑定。</remarks>
[DefaultExecutionOrder(10000)]
public partial class LeonAnimatorDriver : MonoBehaviour
{
    /// <summary>中文含义：动画控制器。使用位置：作为 Debug.LogError(object) 的参数、作为 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 的参数、作为 Mathf.Approximately(float, float) 的参数 等共 57 处。</summary>
    [Header("引用")]
    [SerializeField]
    private Animator animator;
    /// <summary>中文含义：小刀战斗模块。使用位置：作为 KnifeCombatConfiguration.Find(KnifeAction) 的参数、作为 KnifePoseSampler.KnifePoseSampler(KnifeCombatConfiguration, Animator) 的参数、作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数 等共 6 处。</summary>
    private KnifeCombatModule knifeModule;
    /// <summary>中文含义：小刀控制器。使用位置：作为 Transform.InverseTransformDirection(Vector3) 的参数、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.Start()。</summary>
    private LeonController ownerController;
    /// <summary>中文含义：小刀视觉表现集合。使用位置：LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.Start()。</summary>
    private LeonWeaponVisualController knifeVisuals;
    /// <summary>中文含义：小刀姿态采样器。使用位置：LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.OnDisable() 等共 5 处。</summary>
    private KnifePoseSampler knifePoseSampler;
    private FootstepController knifeFootsteps;
    internal bool OwnsKnifeLocomotionFootsteps => knifePoseActive && knifeModule != null && knifeModule.Active;
    private long knifeHitBlendInstance;
    /// <summary>中文含义：小刀姿态是否生效。使用位置：LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.OnDisable() 等共 4 处。</summary>
    private bool knifePoseActive;
    /// <summary>中文含义：是否小刀姿态退出中。使用位置：LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.OnDisable() 等共 4 处。</summary>
    private bool knifePoseExiting;
    /// <summary>中文含义：小刀移动模式。使用位置：LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。</summary>
    private int knifeMovementMode;
    /// <summary>中文含义：小刀移动行为时间。使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、LeonAnimatorDriver.LateUpdate()、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。</summary>
    private float knifeLocomotionTime;
    /// <summary>中文含义：小刀姿态进入混合时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("小刀与枪械姿态交接")]
    [SerializeField, Range(.08f, .16f)]
    private float knifePoseEnterBlendDuration = .14f;
    /// <summary>中文含义：小刀姿态退出混合时长。使用位置：作为 KnifePoseSampler.BeginBlend(float, float) 的参数。</summary>
    [SerializeField, Range(.12f, .3f)]
    private float knifePoseExitBlendDuration = .18f;
    /// <summary>中文含义：武器动作空状态（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.Play(int, int, float) 的参数。</summary>
    private static readonly int weaponActionEmptyState = Animator.StringToHash("Weapon Action SM.Empty");
    /// <summary>中文含义：水平移动动画参数哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashHorizontal = Animator.StringToHash("Horizontal");
    /// <summary>中文含义：纵向移动动画参数哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashVertical = Animator.StringToHash("Vertical");
    /// <summary>中文含义：输入长度的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    /// <summary>中文含义：冲刺的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static readonly int HashSprint = Animator.StringToHash("Sprint");
    /// <summary>中文含义：瞄准的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数、LeonAnimatorDriver.SetAimState(bool)。</summary>
    private static readonly int HashAim = Animator.StringToHash("Aim");
    /// <summary>中文含义：瞄准姿态哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.HasState(int, int) 的参数、作为 Animator.Play(int, int, float) 的参数。</summary>
    private static readonly int aimPoseHash = Animator.StringToHash("AimPose");
    /// <summary>中文含义：横向瞄准角度的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashHorAimAngle = Animator.StringToHash("HorAimAngle");
    /// <summary>中文含义：纵向瞄准角度的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashVerAimAngle = Animator.StringToHash("VerAimAngle");
    /// <summary>中文含义：武器类型的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashWeaponType = Animator.StringToHash("WeaponType");
    /// <summary>中文含义：开火的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.ResetTrigger(int) 的参数、作为 Animator.SetTrigger(int) 的参数。</summary>
    private static readonly int HashFire = Animator.StringToHash("Fire");
    /// <summary>中文含义：武器动作的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetInteger(int, int) 的参数。</summary>
    private static readonly int HashWeaponAction = Animator.StringToHash("WeaponAction");
    /// <summary>中文含义：全身动作的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetInteger(int, int) 的参数。</summary>
    private static readonly int HashFullBodyAction = Animator.StringToHash("FullBodyAction");
    /// <summary>中文含义：命中方向的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashHitDir = Animator.StringToHash("HitDir");
    /// <summary>中文含义：小刀模式的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数、作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashKnifeMode = Animator.StringToHash("KnifeMode");
    /// <summary>中文含义：转向角度的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetFloat(int, float) 的参数。</summary>
    private static readonly int HashTurnAngle = Animator.StringToHash("TurnAngle");
    /// <summary>中文含义：蹲伏的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.SetBool(int, bool) 的参数。</summary>
    private static readonly int HashCrouch = Animator.StringToHash("Crouch");
    /// <summary>中文含义：步枪蹲伏空的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.UpdateLongGunCrouchLayerExit()。</summary>
    private static readonly int HashRifleCrouchEmpty = Animator.StringToHash("Empty");
    /// <summary>中文含义：基础待机的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.CrossFadeInFixedTime(int, float, int, float) 的参数、LeonAnimatorDriver.ReadStartStepDisplacementRatio()。</summary>
    private static readonly int HashBaseIdle = Animator.StringToHash("Idle");
    /// <summary>中文含义：起始步进状态（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.ReadStartStepDisplacementRatio()。</summary>
    private static readonly int startStepState = Animator.StringToHash("WalkStarts");
    /// <summary>中文含义：恢复步进状态（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.ReadStartStepDisplacementRatio()。</summary>
    private static readonly int recoverStepState = Animator.StringToHash("WalkStops");
    /// <summary>中文含义：原地转向状态（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.ReadStartStepDisplacementRatio()。</summary>
    private static readonly int inPlaceTurnState = Animator.StringToHash("TurnInPlace");
    /// <summary>中文含义：全身空的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.EnterDeath()。</summary>
    private static readonly int HashFullBodyEmpty = Animator.StringToHash("Empty");
    /// <summary>中文含义：全身命中的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyHit = Animator.StringToHash("Hit");
    /// <summary>中文含义：全身死亡的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 LeonAnimatorDriver.PrepareFullBodyLayerEntry(int) 的参数、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyDeath = Animator.StringToHash("Death");
    /// <summary>中文含义：全身配对动作进入的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyQTEEnter = Animator.StringToHash("QTEEnter");
    /// <summary>中文含义：全身右侧切口踢击的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyRightCutKick = Animator.StringToHash("QTERightCutKick");
    /// <summary>中文含义：全身旋转中挂钩踢击的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    internal const string WoodenBoxKickStateName = "NativeWoodenBoxKick";
    private static readonly int HashFullBodyWoodenBoxKick = Animator.StringToHash(WoodenBoxKickStateName);
    internal bool HasPreparedMeleeState(FullBodyActionType action)
    {
        return IsValid && TryGetPreparedFullBodyStateHash(action, out int hash) && animator.HasState(LayerFullBodyAction, hash);
    }

    internal bool TryReadMeleeProgress(FullBodyActionType action, out float progress)
    {
        progress = 0f;
        if (!IsValid || waitingForFullBodyStateEntry || activeFullBodyAction != action
            || !TryGetPreparedFullBodyStateHash(action, out int hash))
        {
            return false;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        if (state.shortNameHash != hash && animator.IsInTransition(LayerFullBodyAction))
        {
            state = animator.GetNextAnimatorStateInfo(LayerFullBodyAction);
        }
        progress = state.normalizedTime;
        return state.shortNameHash == hash;
    }
    private static readonly int HashFullBodySpinningHookKick = Animator.StringToHash("QTESpinningHookKick");
    /// <summary>中文含义：全身窗口攀爬的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyWindowClimb = Animator.StringToHash("WindowClimbThrough");
    internal static readonly string[] ItemPickupStateNames = { "NativePickupLow", "NativePickupMiddle", "NativePickupHigh" };
    internal static readonly string[] MovingItemPickupStateNames = { "NativePickupMovingLow", "NativePickupMovingMiddle", "NativePickupMovingHigh" };
    private int pickupStateHash;

    internal bool PrepareItemPickup(int height, bool moving)
    {
        string name = ItemPickupStateNames[height];
        if (moving)
        {
            name = MovingItemPickupStateNames[height];
        }
        int hash = Animator.StringToHash(name);
        if (!IsValid || !animator.HasState(LayerFullBodyAction, hash))
        {
            return false;
        }

        pickupStateHash = hash;
        return true;
    }

    internal bool TryReadItemPickupProgress(out float progress)
    {
        progress = 0f;
        if (!IsValid || waitingForFullBodyStateEntry || activeFullBodyAction != FullBodyActionType.ItemPickup)
        {
            return false;
        }

        var state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        if (state.shortNameHash != pickupStateHash)
        {
            return false;
        }

        progress = state.normalizedTime;
        return true;
    }

    private static readonly int HashFullBodyGateOpen = Animator.StringToHash("VillageGateOpen");

    internal bool TryReadGateOpeningProgress(out float progress)
    {
        progress = 0f;
        if (!IsValid || waitingForFullBodyStateEntry || activeFullBodyAction != FullBodyActionType.VillageGateOpen)
        {
            return false;
        }

        var state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        if (state.shortNameHash != HashFullBodyGateOpen)
        {
            return false;
        }

        progress = Mathf.Clamp01(state.normalizedTime);
        return true;
    }
    private static readonly int HashFullBodyHighWindowClimb = Animator.StringToHash("NativeHighWindowClimb");
    private static readonly int HashFullBodyFenceVault = Animator.StringToHash("FenceVault");
    private bool useHighWindowClimb;
    private bool useFenceVault;
    internal bool HasNativeWindowVariants => IsValid && animator.HasState(LayerFullBodyAction, HashFullBodyHighWindowClimb);
    internal bool HasFenceVault => IsValid && animator.HasState(LayerFullBodyAction, HashFullBodyFenceVault);

    internal void PrepareWindowClimb(bool high, bool fence = false)
    {
        useHighWindowClimb = high && HasNativeWindowVariants;
        useFenceVault = fence && HasFenceVault;
    }
    /// <summary>中文含义：全身窗口掉落下落的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyWindowDropFall = Animator.StringToHash("WindowDropFall");
    /// <summary>中文含义：全身窗口掉落落地的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    private static readonly int HashFullBodyWindowDropLand = Animator.StringToHash("WindowDropLand");
    /// <summary>中文含义：最小配对动作全身图层退出混合时长。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    private const float MinimumQTEFullBodyLayerExitBlendDuration = 0.12f;
    /// <summary>中文含义：最大配对动作全身图层退出混合时长。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    private const float MaximumQTEFullBodyLayerExitBlendDuration = 0.18f;
    /// <summary>中文含义：图层基础。使用位置：作为 Animator.CrossFadeInFixedTime(int, float, int, float) 的参数、作为 Animator.GetCurrentAnimatorStateInfo(int) 的参数、作为 Animator.GetNextAnimatorStateInfo(int) 的参数 等共 5 处。</summary>
    public static int LayerBase => AnimatorLayerID.Base;
    /// <summary>中文含义：图层受伤。使用位置：作为 Animator.GetLayerWeight(int) 的参数、作为 Animator.SetLayerWeight(int, float) 的参数。</summary>
    public static int LayerInjured => AnimatorLayerID.Injured;
    /// <summary>中文含义：图层受伤下半身。使用位置：作为 Animator.GetLayerWeight(int) 的参数、作为 Animator.SetLayerWeight(int, float) 的参数。</summary>
    public static int LayerInjuredLowerBody => AnimatorLayerID.InjuredLowerBody;
    /// <summary>中文含义：图层步枪蹲伏。使用位置：作为 Animator.GetCurrentAnimatorStateInfo(int) 的参数、作为 Animator.GetLayerWeight(int) 的参数、作为 Animator.IsInTransition(int) 的参数 等共 4 处。</summary>
    public static int LayerRifleCrouch => AnimatorLayerID.RifleCrouch;
    /// <summary>中文含义：图层瞄准。使用位置：作为 Animator.HasState(int, int) 的参数、作为 Animator.Play(int, int, float) 的参数、作为 Animator.SetLayerWeight(int, float) 的参数。</summary>
    public static int LayerAim => AnimatorLayerID.Aim;
    /// <summary>中文含义：图层开火。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数。</summary>
    public static int LayerFire => AnimatorLayerID.Fire;
    /// <summary>中文含义：图层武器动作。使用位置：作为 Animator.CrossFadeInFixedTime(int, float, int, float) 的参数、作为 Animator.GetLayerWeight(int) 的参数、作为 Animator.HasState(int, int) 的参数 等共 5 处。</summary>
    public static int LayerWeaponAction => AnimatorLayerID.WeaponAction;
    /// <summary>中文含义：图层全身动作。使用位置：作为 Animator.CrossFadeInFixedTime(int, float, int, float) 的参数、作为 Animator.GetCurrentAnimatorStateInfo(int) 的参数、作为 Animator.GetLayerWeight(int) 的参数 等共 6 处。</summary>
    public static int LayerFullBodyAction => AnimatorLayerID.FullBodyAction;

    /// <summary>中文含义：是否校验触发的。使用位置：LeonAnimatorDriver.ContextMenuValidate()、LeonAnimatorDriver.IsValid.get、LeonAnimatorDriver.Start() 等共 4 处。</summary>
    [NonSerialized]
    private bool validationPerformed;
    /// <summary>中文含义：是否校验结果。使用位置：LeonAnimatorDriver.IsValid.get、LeonAnimatorDriver.ValidateAnimatorContract()。</summary>
    [NonSerialized]
    private bool validationResult;
    /// <summary>中文含义：武器动作图层混合时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [SerializeField]
    private float weaponActionLayerBlendDuration = 0.1f;
    /// <summary>中文含义：小刀全身图层混合时长。使用位置：LeonAnimatorDriver.UpdateFullBodyLayerWeight()。</summary>
    [SerializeField]
    private float knifeFullBodyLayerBlendDuration = 0.08f;
    /// <summary>中文含义：全身图层入口混合时长。使用位置：LeonAnimatorDriver.UpdateFullBodyLayerWeight()。</summary>
    [SerializeField]
    private float fullBodyLayerEntryBlendDuration = 0.08f;
    /// <summary>中文含义：配对动作全身图层退出混合时长。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、LeonAnimatorDriver.Start()、LeonAnimatorDriver.UpdateFullBodyLayerWeight()。</summary>
    [SerializeField]
    private float qteFullBodyLayerExitBlendDuration = 0.16f;
    /// <summary>中文含义：小刀模式混合时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("小刀基础姿态过渡")]
    [SerializeField, Range(0.08f, 0.6f)]
    private float knifeModeBlendDuration = 0.24f;
    /// <summary>中文含义：长枪蹲伏图层混合时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("长枪蹲姿层过渡")]
    [Range(0.08f, 0.6f)]
    public float longGunCrouchLayerBlendDuration = 0.24f;
    /// <summary>中文含义：普通进入受伤状态的淡入时长。使用位置：LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonAnimatorDriver.ResetInjuredPresentation()。</summary>
    [Header("受伤层过渡")]
    [Tooltip("普通进入受伤状态的淡入时长")]
    [Range(0.05f, 1f)]
    public float injuredLayerEnterDuration = 0.20f;
    /// <summary>中文含义：受击恢复阶段交接专用的淡入时长。使用位置：LeonAnimatorDriver.BeginPendingInjuredTransition()。</summary>
    [Tooltip("受击恢复阶段交接专用的淡入时长")]
    [Range(0.05f, 1f)]
    public float injuredHitRecoveryEnterDuration = 0.14f;
    /// <summary>中文含义：恢复正常状态的淡出时长。使用位置：LeonAnimatorDriver.BeginPendingInjuredTransition()、LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonAnimatorDriver.ResetInjuredPresentation()。</summary>
    [Tooltip("恢复正常状态的淡出时长")]
    [Range(0.05f, 1f)]
    public float injuredLayerExitDuration = 0.25f;
    /// <summary>中文含义：手枪进入瞄准时全身受伤层与下身受伤层的交换时长。使用位置：LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。</summary>
    [Tooltip("手枪进入瞄准时全身受伤层与下身受伤层的交换时长")]
    [Range(0.05f, 1f)]
    public float injuredAimSwapDuration = 0.14f;
    /// <summary>中文含义：手枪退出瞄准时全身受伤层与下身受伤层的交换时长。使用位置：LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。</summary>
    [Tooltip("手枪退出瞄准时全身受伤层与下身受伤层的交换时长")]
    [Range(0.05f, 1f)]
    public float injuredAimReleaseSwapDuration = 0.18f;
    /// <summary>中文含义：单手与双手武器切换时两个受伤层的交叉淡化时长。使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数。</summary>
    [Tooltip("单手与双手武器切换时两个受伤层的交叉淡化时长")]
    [Range(0.05f, 0.4f)]
    public float injuredWeaponSwapDuration = 0.16f;
    /// <summary>中文含义：受伤方向迟滞。八方向吸附切换的点积滞回，数值越大越不容易在相邻方向间抖动。使用位置：LeonAnimatorDriver.SetMovement(float, float, float, bool)。</summary>
    [Header("受伤方向稳定")]
    [Tooltip("八方向吸附切换的点积滞回，数值越大越不容易在相邻方向间抖动")]
    [Range(0f, 0.3f)]
    public float injuredDirectionHysteresis = 0.12f;
    /// <summary>中文含义：武器动作层退出Equip后的平滑淡出时长。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("武器动作层退出Equip后的平滑淡出时长")]
    [Range(0.05f, 1f)]
    public float weaponActionLayerSmoothExitDuration = 0.14f;
    /// <summary>中文含义：受伤表现。使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数、LeonAnimatorDriver.ApplyPresentationMode(float)、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType) 等共 6 处。</summary>
    private InjuredPresentationMode injuredPresentation = InjuredPresentationMode.Normal;
    /// <summary>中文含义：待处理受伤表现。使用位置：LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType)。</summary>
    private InjuredPresentationMode pendingInjuredPresentation = InjuredPresentationMode.Normal;
    /// <summary>中文含义：是否存在待处理的受伤表现。使用位置：LeonAnimatorDriver.ClearPendingInjuredTransition()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType) 等共 4 处。</summary>
    private bool hasPendingInjuredPresentation;
    /// <summary>中文含义：受伤图层是否生效。使用位置：LeonAnimatorDriver.BeginPendingInjuredTransition()、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.InjuredLayerActive.get 等共 10 处。</summary>
    private bool injuredLayerActive;
    /// <summary>中文含义：是否存在待处理的受伤目标。使用位置：LeonAnimatorDriver.BeginPendingInjuredTransition()、LeonAnimatorDriver.ClearPendingInjuredTransition()、LeonAnimatorDriver.HasPendingInjuredTransition.get 等共 5 处。</summary>
    private bool hasPendingInjuredTarget;
    /// <summary>中文含义：是否存在待处理的受伤目标。使用位置：LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonAnimatorDriver.ResetInjuredLayerState()。</summary>
    private bool pendingInjuredTarget;
    /// <summary>中文含义：受伤动画图层权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float) 等共 6 处。</summary>
    private float injuredLayerWeight;
    /// <summary>中文含义：受伤图层混合开始时刻。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)。</summary>
    private float injuredLayerBlendStart;
    /// <summary>中文含义：受伤图层目标。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float) 等共 5 处。</summary>
    private float injuredLayerTarget;
    /// <summary>中文含义：受伤图层混合时长。使用位置：作为 Mathf.Min(float, float) 的参数、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLayerWeight()。</summary>
    private float injuredLayerBlendDuration;
    /// <summary>中文含义：受伤图层混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLayerWeight()。</summary>
    private float injuredLayerBlendElapsed;
    /// <summary>中文含义：是否混合受伤图层。使用位置：LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLayerWeight()。</summary>
    private bool blendInjuredLayer;
    /// <summary>中文含义：受伤下部动画图层权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float) 等共 6 处。</summary>
    private float injuredLowerLayerWeight;
    /// <summary>中文含义：受伤下部混合开始时刻。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)。</summary>
    private float injuredLowerBlendStart;
    /// <summary>中文含义：受伤下部目标。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float) 等共 5 处。</summary>
    private float injuredLowerTarget;
    /// <summary>中文含义：受伤下部混合时长。使用位置：作为 Mathf.Min(float, float) 的参数、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLowerLayerWeight()。</summary>
    private float injuredLowerBlendDuration;
    /// <summary>中文含义：受伤下部混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLowerLayerWeight()。</summary>
    private float injuredLowerBlendElapsed;
    /// <summary>中文含义：是否混合受伤下部图层。使用位置：LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)、LeonAnimatorDriver.UpdateInjuredLowerLayerWeight()。</summary>
    private bool blendInjuredLowerLayer;
    /// <summary>中文含义：是否当前瞄准中。使用位置：LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.ResolveModeForWeapon(WeaponType) 等共 5 处。</summary>
    private bool currentAiming;
    /// <summary>中文含义：受伤吸附后的方向。使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。</summary>
    private Vector2 injuredSnappedDirection;
    /// <summary>中文含义：是否具有受伤吸附后的方向。使用位置：LeonAnimatorDriver.PrepareTraversalLandingBasePose()、LeonAnimatorDriver.SetMovement(float, float, float, bool)。</summary>
    private bool hasInjuredSnappedDirection;
    /// <summary>中文含义：武器动作动画图层权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数 等共 11 处。</summary>
    private float weaponActionLayerWeight;
    /// <summary>中文含义：目标武器动作动画图层权重。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数 等共 10 处。</summary>
    private float targetWeaponActionLayerWeight;
    /// <summary>中文含义：是否混合武器动作动画图层权重。使用位置：LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight() 等共 7 处。</summary>
    private bool blendWeaponActionLayerWeight;
    /// <summary>中文含义：全身动画图层权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数 等共 13 处。</summary>
    private float fullBodyLayerWeight;
    /// <summary>中文含义：目标全身动画图层权重。使用位置：作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数、LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float) 等共 12 处。</summary>
    private float targetFullBodyLayerWeight;
    /// <summary>中文含义：是否混合全身动画图层权重。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.BeginQTEFullBodyExitBlend() 等共 10 处。</summary>
    private bool blendFullBodyLayerWeight;
    /// <summary>中文含义：是否混合中全身图层入口。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.BeginQTEFullBodyExitBlend() 等共 10 处。</summary>
    private bool blendingFullBodyLayerEntry;
    /// <summary>中文含义：是否混合中配对动作全身退出。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.BeginQTEFullBodyExitBlend() 等共 9 处。</summary>
    private bool blendingQTEFullBodyExit;
    /// <summary>中文含义：是否清除全身动作之后淡入淡出。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.ClearFullBodyActionAfterFadeIfReady() 等共 6 处。</summary>
    private bool clearFullBodyActionAfterFade;
    /// <summary>中文含义：是否正在等待进入全身动画状态。使用位置：LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int) 等共 4 处。</summary>
    private bool waitingForFullBodyStateEntry;
    /// <summary>中文含义：待处理全身动画状态哈希编号。使用位置：LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int) 等共 4 处。</summary>
    private int pendingFullBodyStateHash;
    /// <summary>中文含义：全身入口请求帧。使用位置：LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int) 等共 4 处。</summary>
    private int fullBodyEntryRequestFrame = -1;
    /// <summary>中文含义：通行落地基础姿态是否锁定。使用位置：LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.PrepareTraversalLandingBasePose()、LeonAnimatorDriver.ReleaseTraversalLandingBasePose() 等共 4 处。</summary>
    private bool traversalLandingBasePoseLocked;
    /// <summary>中文含义：生效全身动作。使用位置：作为 LeonAnimatorDriver.IsQTEAction(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.ShouldBlendFullBodyLayer(FullBodyActionType) 的参数、LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float) 等共 8 处。</summary>
    private FullBodyActionType activeFullBodyAction = FullBodyActionType.None;
    /// <summary>中文含义：生效近战事件武器图层。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction() 等共 7 处。</summary>
    private FullBodyActionType activeMeleeOnWeaponLayer = FullBodyActionType.None;
    /// <summary>中文含义：当前小刀模式。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数 等共 8 处。</summary>
    private float currentKnifeMode;
    /// <summary>中文含义：目标小刀模式。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.MoveTowards(float, float, float) 的参数 等共 5 处。</summary>
    private float targetKnifeMode;
    /// <summary>中文含义：是否混合小刀模式。使用位置：LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetKnifeModeImmediate(bool)、LeonAnimatorDriver.UpdateKnifeModeBlend()。</summary>
    private bool blendKnifeMode;
    /// <summary>中文含义：当前武器类型。使用位置：作为 LeonAnimatorDriver.IsLongGun(WeaponType) 的参数、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.ResolvePresentationMode() 等共 5 处。</summary>
    private WeaponType currentWeaponType = WeaponType.Pistol;
    /// <summary>中文含义：是否正在蹲伏。使用位置：LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType) 等共 9 处。</summary>
    private bool isCrouching;
    /// <summary>中文含义：长枪蹲伏图层退出是否仍待处理。使用位置：LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.SetCrouching(bool) 等共 6 处。</summary>
    private bool longGunCrouchLayerExitPending;
    /// <summary>中文含义：长枪蹲伏动画图层权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float) 等共 5 处。</summary>
    private float longGunCrouchLayerWeight;
    /// <summary>中文含义：长枪蹲伏图层混合开始时刻。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)。</summary>
    private float longGunCrouchLayerBlendStart;
    /// <summary>中文含义：长枪蹲伏图层目标。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 5 处。</summary>
    private float longGunCrouchLayerTarget;
    /// <summary>中文含义：长枪蹲伏图层混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float) 等共 5 处。</summary>
    private float longGunCrouchLayerBlendElapsed;
    /// <summary>中文含义：是否混合长枪蹲伏图层。使用位置：LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.UpdateLongGunCrouchLayerBlend()。</summary>
    private bool blendLongGunCrouchLayer;
    /// <summary>中文含义：动画控制器。使用位置：LeonAnimationActionCoordinator.ValidateDriver()、LeonCombat.ParryHitStopRoutine(VillagerController, bool)。</summary>
    public Animator Animator => animator;
    /// <summary>中文含义：是否有效。使用位置：LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.BeginHit(LeonHitDirection) 等共 15 处。</summary>
    public bool IsValid => validationPerformed && validationResult;

    /// <summary>中文含义：是否全身图层退出完成。</summary>
    public bool IsFullBodyLayerExitComplete
    {
        get
        {
            if (animator == null || !IsValid)
            {
                return true;
            }

            return !clearFullBodyActionAfterFade && !blendingQTEFullBodyExit && animator.GetLayerWeight(LayerFullBodyAction) <= 0.01f;
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetLayerWeight(int)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;KnifeCombatModule&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、LeonAnimatorDriver.ValidateAnimatorContract()、Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        knifeModule = GetComponent<KnifeCombatModule>();
        ownerController = GetComponent<LeonController>();
        knifeVisuals = GetComponent<LeonWeaponVisualController>();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        validationPerformed = false;
        ValidateAnimatorContract();
        qteFullBodyLayerExitBlendDuration = Mathf.Clamp(qteFullBodyLayerExitBlendDuration, MinimumQTEFullBodyLayerExitBlendDuration, MaximumQTEFullBodyLayerExitBlendDuration);
        if (animator != null)
        {
            weaponActionLayerWeight = animator.GetLayerWeight(LayerWeaponAction);
            fullBodyLayerWeight = animator.GetLayerWeight(LayerFullBodyAction);
            injuredLayerWeight = animator.GetLayerWeight(LayerInjured);
            injuredLowerLayerWeight = animator.GetLayerWeight(LayerInjuredLowerBody);
        }
    }

    /// <summary>
    /// 初始化动画器契约：设置图层权重和参数默认值。
    /// 在写任何参数前先验证。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.InitPipeline()；下游（含内部回调体） Animator.ResetTrigger(int)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.ResetInjuredLayerState()、LeonAnimatorDriver.SetKnifeModeImmediate(bool) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="initialWeaponType">中文含义：初始武器类型。使用位置：作为 LeonAnimatorDriver.SetKnifeModeImmediate(bool) 的参数、作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)。</param>
    public bool InitializeAnimatorContract(WeaponType initialWeaponType)
    {
        if (animator == null)
        {
            return false;
        }

        if (initialWeaponType != WeaponType.Pistol && initialWeaponType != WeaponType.Rifle && initialWeaponType != WeaponType.Shotgun && initialWeaponType != WeaponType.Knife)
        {
            return false;
        }

        if (!ValidateAnimatorContract())
        {
            return false;
        }

        animator.SetLayerWeight(LayerBase, 1f);
        currentWeaponType = initialWeaponType;
        isCrouching = false;
        currentAiming = false;
        longGunCrouchLayerExitPending = false;
        animator.SetLayerWeight(LayerRifleCrouch, 0f);
        longGunCrouchLayerWeight = 0f;
        longGunCrouchLayerBlendStart = 0f;
        longGunCrouchLayerTarget = 0f;
        longGunCrouchLayerBlendElapsed = 0f;
        blendLongGunCrouchLayer = false;
        animator.SetLayerWeight(LayerAim, 0f);
        animator.SetLayerWeight(LayerFire, 1f);
        animator.SetLayerWeight(LayerWeaponAction, 0f);
        animator.SetLayerWeight(LayerFullBodyAction, 0f);
        ResetInjuredLayerState();
        weaponActionLayerWeight = 0f;
        targetWeaponActionLayerWeight = 0f;
        blendWeaponActionLayerWeight = false;
        fullBodyLayerWeight = 0f;
        targetFullBodyLayerWeight = 0f;
        blendFullBodyLayerWeight = false;
        blendingFullBodyLayerEntry = false;
        waitingForFullBodyStateEntry = false;
        pendingFullBodyStateHash = 0;
        fullBodyEntryRequestFrame = -1;
        activeFullBodyAction = FullBodyActionType.None;
        activeMeleeOnWeaponLayer = FullBodyActionType.None;
        animator.SetInteger(HashWeaponAction, 0);
        animator.SetInteger(HashFullBodyAction, 0);
        animator.SetFloat(HashAim, 0f);
        SetKnifeModeImmediate(initialWeaponType == WeaponType.Knife);
        animator.SetFloat(HashTurnAngle, 0f);
        animator.SetBool(HashSprint, false);
        animator.SetBool(HashCrouch, false);
        animator.ResetTrigger(HashFire);
        if (initialWeaponType == WeaponType.Knife)
        {
            animator.SetFloat(HashWeaponType, 0f);
        }
        else
        {
            SetWeaponType(initialWeaponType);
        }

        return true;
    }

    /// <summary>CanWrite 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.ValidateAnimatorContract()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.SetAimAngles(float, float)、LeonAnimatorDriver.SetAimLayerWeight(float)、LeonAnimatorDriver.SetAimState(bool)、LeonAnimatorDriver.SetCrouching(bool)、LeonAnimatorDriver.SetKnifeMode(bool) 等共 9 处；下游（含内部回调体） LeonAnimatorDriver.ValidateAnimatorContract()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanWrite()
    {
        return animator != null && ValidateAnimatorContract();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifePoseSampler.RestoreUnderlyingPose()、LeonAnimatorDriver.UpdateFullBodyLayerWeight()、LeonAnimatorDriver.UpdateInjuredLayerWeight()、LeonAnimatorDriver.UpdateInjuredLowerLayerWeight()、LeonAnimatorDriver.UpdateKnifeModeBlend()、LeonAnimatorDriver.UpdateLongGunCrouchLayerBlend()、LeonAnimatorDriver.UpdateLongGunCrouchLayerExit() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (knifePoseSampler != null)
        {
            knifePoseSampler.RestoreUnderlyingPose();
        }

        if (animator == null)
        {
            return;
        }

        UpdateWeaponActionLayerWeight();
        UpdateFullBodyLayerWeight();
        UpdateInjuredLayerWeight();
        UpdateInjuredLowerLayerWeight();
        UpdateKnifeModeBlend();
        UpdateLongGunCrouchLayerExit();
        UpdateLongGunCrouchLayerBlend();
        UpdateNativeGunPose();
    }

    /// <summary>UpdateKnifeModeBlend 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、Mathf.Approximately(float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.SetFloat(int, float)、Mathf.Approximately(float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateKnifeModeBlend()
    {
        if (!blendKnifeMode)
        {
            return;
        }

        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。
float duration = Mathf.Max(0.01f, knifeModeBlendDuration);
        currentKnifeMode = Mathf.MoveTowards(currentKnifeMode, targetKnifeMode, Time.deltaTime / duration);
        animator.SetFloat(HashKnifeMode, Mathf.SmoothStep(0f, 1f, currentKnifeMode));
        if (Mathf.Approximately(currentKnifeMode, targetKnifeMode))
        {
            blendKnifeMode = false;
        }
    }

    /// <summary>中文含义：武器动作图层混合开始时刻权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)。</summary>
    private float weaponActionLayerBlendStartWeight;
    /// <summary>中文含义：武器动作图层混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimatorDriver.BlendWeaponActionLayerTo(float) 等共 4 处。</summary>
    private float weaponActionLayerBlendElapsed;
    /// <summary>中文含义：是否武器动作平滑退出。使用位置：LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight() 等共 5 处。</summary>
    private bool weaponActionSmoothExit;
    /// <summary>UpdateWeaponActionLayerWeight 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)、Mathf.Approximately(float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.SetLayerWeight(int, float)、Mathf.Approximately(float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.MoveTowards(float, float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateWeaponActionLayerWeight()
    {
        if (!blendWeaponActionLayerWeight)
        {
            return;
        }

        bool nativeAction = UsesNativeWeaponLayerFade;
        if (weaponActionSmoothExit || nativeAction)
        {
            // Equip 退出后的淡出使用保存起点、目标、经过时间的 SmoothStep 平滑曲线，
            // 保证从 Equip 到最终姿态只有一次连续交接。
            // smoothExitDuration（平滑退出持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimatorDriver.UpdateWeaponActionLayerWeight()。
            float smoothExitDuration = Mathf.Max(0.01f, weaponActionLayerSmoothExitDuration);
            if (nativeAction)
            {
                smoothExitDuration = NativeReloadEnterBlendSeconds;
                if (weaponActionSmoothExit)
                {
                    smoothExitDuration = NativeReloadExitBlendSeconds;
                }
            }
            weaponActionLayerBlendElapsed = Mathf.Min(weaponActionLayerBlendElapsed + Time.deltaTime, smoothExitDuration);
            // progress（进度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float progress = Mathf.SmoothStep(0f, 1f, weaponActionLayerBlendElapsed / smoothExitDuration);
            weaponActionLayerWeight = Mathf.Lerp(weaponActionLayerBlendStartWeight, targetWeaponActionLayerWeight, progress);
            animator.SetLayerWeight(LayerWeaponAction, weaponActionLayerWeight);
            if (weaponActionLayerBlendElapsed >= smoothExitDuration)
            {
                if (nativeAction && weaponActionSmoothExit)
                {
                    animator.SetInteger(HashWeaponAction, 0);
                }
                weaponActionLayerWeight = targetWeaponActionLayerWeight;
                blendWeaponActionLayerWeight = false;
                weaponActionSmoothExit = false;
            }

            return;
        }

        // linearDuration（线性持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。
float linearDuration = Mathf.Max(0.01f, weaponActionLayerBlendDuration);
        weaponActionLayerWeight = Mathf.MoveTowards(weaponActionLayerWeight, targetWeaponActionLayerWeight, Time.deltaTime / linearDuration);
        animator.SetLayerWeight(LayerWeaponAction, weaponActionLayerWeight);
        if (Mathf.Approximately(weaponActionLayerWeight, targetWeaponActionLayerWeight))
        {
            blendWeaponActionLayerWeight = false;
        }
    }

    // ================================================================
    // 受伤显示模式：两个受伤层职责互斥
    // ================================================================
    /// <summary>中文含义：受伤表现。当前受伤显示模式</summary>
    public InjuredPresentationMode InjuredPresentation => injuredPresentation;

    /// <summary>
    /// 根据武器、瞄准、蹲伏与逻辑受伤状态求值目标显示模式。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.ApplyPresentationMode(float)、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private InjuredPresentationMode ResolvePresentationMode()
    {
        if (!injuredLayerActive)
        {
            return InjuredPresentationMode.Normal;
        }

        if (isCrouching)
        {
            return InjuredPresentationMode.Suppressed;
        }

        return ResolveModeForWeapon(currentWeaponType);
    }

    /// <summary>
    /// 请求受伤状态切换。延迟请求只记录逻辑目标，等待受击恢复阶段消费。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.HandleDeferredTraversalHit(LeonHitDirection)、LeonController.HandleHealthChanged(float, float)；下游（含内部回调体） LeonAnimatorDriver.ApplyPresentationMode(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="injured">中文含义：是否受伤。使用位置：LeonAnimatorDriver.RequestInjuredState(bool, bool)。</param>
    /// <param name="deferUntilHitRecovery">中文含义：是否延后直到命中恢复。使用位置：LeonAnimatorDriver.RequestInjuredState(bool, bool)。</param>
    public void RequestInjuredState(bool injured, bool deferUntilHitRecovery)
    {
        injuredLayerActive = injured;
        if (deferUntilHitRecovery)
        {
            hasPendingInjuredTarget = true;
            pendingInjuredTarget = injured;
            return;
        }

        hasPendingInjuredTarget = false;
        // selectedValue1（分支选出的受伤图层进入持续时间）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimatorDriver.ApplyPresentationMode(float) 的参数、LeonAnimatorDriver.RequestInjuredState(bool, bool)。
float selectedValue1;
        if (injuredLayerActive)
        {
            selectedValue1 = injuredLayerEnterDuration;
        }
        else
        {
            selectedValue1 = injuredLayerExitDuration;
        }

        ApplyPresentationMode(selectedValue1);
    }

    /// <summary>
    /// 消费待处理的受伤目标，在受击开始退出的同一帧以受击恢复专用时长执行交接。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)、LeonAnimationActionCoordinator.HandleFullBodyInterrupted(Animator, FullBodyActionType, int)；下游（含内部回调体） LeonAnimatorDriver.ApplyPresentationMode(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginPendingInjuredTransition()
    {
        if (!hasPendingInjuredTarget)
        {
            return;
        }

        hasPendingInjuredTarget = false;
        // selectedValue2（分支选出的受伤命中恢复进入持续时间）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimatorDriver.ApplyPresentationMode(float) 的参数、LeonAnimatorDriver.BeginPendingInjuredTransition()。
float selectedValue2;
        if (injuredLayerActive)
        {
            selectedValue2 = injuredHitRecoveryEnterDuration;
        }
        else
        {
            selectedValue2 = injuredLayerExitDuration;
        }

        ApplyPresentationMode(selectedValue2);
    }

    /// <summary>
    /// 清除待处理的受伤层目标（死亡、初始化或管线失效时使用）。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.OnDisable()、LeonController.ApplyDamage(float, Vector3, LeonHitDirection)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearPendingInjuredTransition()
    {
        hasPendingInjuredTarget = false;
        hasPendingInjuredPresentation = false;
    }

    /// <summary>中文含义：是否存在待处理的受伤过渡。是否存在等待受击恢复阶段消费的受伤层目标</summary>
    public bool HasPendingInjuredTransition => hasPendingInjuredTarget;
    /// <summary>中文含义：受伤图层是否生效。受伤移动层当前逻辑目标状态（蹲伏抑制期间仍保持该目标）。</summary>
    public bool InjuredLayerActive => injuredLayerActive;

    /// <summary>
    /// 请求切枪时只记录目标受伤显示模式，不改变两个受伤层。
    /// Holster 播放期间保持旧模式。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；下游（含内部回调体） LeonAnimatorDriver.ResolveModeForWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标武器类别。使用位置：作为 LeonAnimatorDriver.ResolveModeForWeapon(WeaponType) 的参数。</param>
    public void PrepareInjuredWeaponSwitch(WeaponType target)
    {
        if (!injuredLayerActive || isCrouching)
        {
            hasPendingInjuredPresentation = false;
            return;
        }

        hasPendingInjuredPresentation = true;
        pendingInjuredPresentation = ResolveModeForWeapon(target);
    }

    /// <summary>ResolveModeForWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.PrepareInjuredWeaponSwitch(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimatorDriver.ResolveModeForWeapon(WeaponType)。</param>
    private InjuredPresentationMode ResolveModeForWeapon(WeaponType weapon)
    {
        if (weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun)
        {
            if (currentAiming && (weapon == WeaponType.Rifle && SupportsNativeCqbr
                || weapon == WeaponType.Shotgun && SupportsNativeW870Aim))
            {
                // 举枪移动的基础层提供前后及横移腿部，伤势步速由移动控制器处理。
                return InjuredPresentationMode.Suppressed;
            }
            return InjuredPresentationMode.LowerBodyOnly;
        }

        if (weapon == WeaponType.Pistol && currentAiming)
        {
            return InjuredPresentationMode.LowerBodyOnly;
        }

        return InjuredPresentationMode.SingleHandFullBody;
    }

    /// <summary>
    /// 武器提交阶段在武器动作层遮挡下交叉淡化两个受伤层。
    /// 避免单手全身姿态和双手下身姿态在同一帧硬切。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)；下游（含内部回调体） LeonAnimatorDriver.ResolveModeForWeapon(WeaponType)、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)、LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标武器类别。使用位置：作为 LeonAnimatorDriver.ResolveModeForWeapon(WeaponType) 的参数。</param>
    public void CommitInjuredWeaponSwitchPresentation(WeaponType target)
    {
        if (!injuredLayerActive)
        {
            hasPendingInjuredPresentation = false;
            injuredPresentation = InjuredPresentationMode.Normal;
            SetInjuredLayersImmediate(0f, 0f);
            return;
        }

        if (isCrouching)
        {
            injuredPresentation = InjuredPresentationMode.Suppressed;
        }
        else
        {
            if (hasPendingInjuredPresentation)
            {
                injuredPresentation = pendingInjuredPresentation;
            }
            else
            {
                injuredPresentation = ResolveModeForWeapon(target);
            }
        }

        hasPendingInjuredPresentation = false;
        StartInjuredBlendsForMode(injuredPresentation, injuredWeaponSwapDuration, injuredWeaponSwapDuration);
    }

    /// <summary>
    /// 手枪瞄准不属于切枪，保留平滑交换：
    /// 进入瞄准：全身层 1→0，下身层 0→1，时长 0.14 秒；
    /// 退出瞄准：全身层 0→1，下身层 1→0，时长 0.18 秒。
    /// 两个受伤层同步同一状态机与归一化时间，腿部保持同一姿势。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.SetAimState(bool)；下游（含内部回调体） LeonAnimatorDriver.ResolvePresentationMode()、LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="aiming">中文含义：是否瞄准中。使用位置：LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。</param>
    public void RefreshInjuredPresentationForAim(bool aiming)
    {
        if (!injuredLayerActive || isCrouching)
        {
            return;
        }

        // target（目标受伤表现模式）：保存 LeonAnimatorDriver.ResolvePresentationMode() 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。
InjuredPresentationMode target = ResolvePresentationMode();
        if (target == injuredPresentation)
        {
            return;
        }

        injuredPresentation = target;
        // swapDuration（交换持续时间）：使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。
float swapDuration;
        if (aiming)
        {
            swapDuration = injuredAimSwapDuration;
        }
        else
        {
            swapDuration = injuredAimReleaseSwapDuration;
        }

        StartInjuredBlendsForMode(target, swapDuration, swapDuration);
    }

    /// <summary>
    /// 重置受伤显示模式到当前状态的求值结果（蹲伏变化、小刀模式等使用）。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.SetCrouching(bool)；下游（含内部回调体） LeonAnimatorDriver.ApplyPresentationMode(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetInjuredPresentation()
    {
        // selectedValue6（分支选出的受伤图层进入持续时间）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimatorDriver.ApplyPresentationMode(float) 的参数、LeonAnimatorDriver.ResetInjuredPresentation()。
float selectedValue6;
        if (injuredLayerActive)
        {
            selectedValue6 = injuredLayerEnterDuration;
        }
        else
        {
            selectedValue6 = injuredLayerExitDuration;
        }

        ApplyPresentationMode(selectedValue6);
    }

    /// <summary>ApplyPresentationMode 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.ResolvePresentationMode()、LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.BeginPendingInjuredTransition()、LeonAnimatorDriver.RequestInjuredState(bool, bool)、LeonAnimatorDriver.ResetInjuredPresentation()；下游（含内部回调体） LeonAnimatorDriver.ResolvePresentationMode()、LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数。</param>
    private void ApplyPresentationMode(float duration)
    {
        if (animator == null)
        {
            return;
        }

        // target（目标受伤表现模式）：保存 LeonAnimatorDriver.ResolvePresentationMode() 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float) 的参数、LeonAnimatorDriver.ApplyPresentationMode(float)。
InjuredPresentationMode target = ResolvePresentationMode();
        injuredPresentation = target;
        StartInjuredBlendsForMode(target, duration, duration);
    }

    /// <summary>StartInjuredBlendsForMode 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float)、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.ApplyPresentationMode(float)、LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)；下游（含内部回调体） LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float)、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">中文含义：当前受伤表现模式。使用位置：作为 LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float) 的参数。</param>
    /// <param name="moveDuration">中文含义：移动持续时间。使用位置：作为 LeonAnimatorDriver.StartInjuredMoveBlend(float, float) 的参数。</param>
    /// <param name="lowerDuration">中文含义：下部持续时间。使用位置：作为 LeonAnimatorDriver.StartInjuredLowerBlend(float, float) 的参数。</param>
    private void StartInjuredBlendsForMode(InjuredPresentationMode mode, float moveDuration, float lowerDuration)
    {
        // moveTarget（移动目标）：使用位置：作为 LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float) 的参数、作为 LeonAnimatorDriver.StartInjuredMoveBlend(float, float) 的参数。
float moveTarget;
        // lowerTarget（下部目标）：使用位置：作为 LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float) 的参数、作为 LeonAnimatorDriver.StartInjuredLowerBlend(float, float) 的参数。
float lowerTarget;
        ResolveLayerWeightsForMode(mode, out moveTarget, out lowerTarget);
        if (nativeInjuryPoseActive)
        {
            moveTarget = 0f;
            lowerTarget = 0f;
        }
        StartInjuredMoveBlend(moveTarget, moveDuration);
        StartInjuredLowerBlend(lowerTarget, lowerDuration);
    }

    /// <summary>
    /// 计算指定模式下两个受伤层的目标权重。
    /// 单手全身层与双手下身层保持互斥。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">中文含义：当前受伤表现模式。使用位置：LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float)。</param>
    /// <param name="moveTarget">中文含义：移动目标。输出参数，由本入口写入并交给调用方。使用位置：LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float)。</param>
    /// <param name="lowerTarget">中文含义：下部目标。输出参数，由本入口写入并交给调用方。使用位置：LeonAnimatorDriver.ResolveLayerWeightsForMode(InjuredPresentationMode, out float, out float)。</param>
    private static void ResolveLayerWeightsForMode(InjuredPresentationMode mode, out float moveTarget, out float lowerTarget)
    {
        switch (mode)
        {
            case InjuredPresentationMode.SingleHandFullBody:
                moveTarget = 1f;
                lowerTarget = 0f;
                break;
            case InjuredPresentationMode.LowerBodyOnly:
                moveTarget = 0f;
                lowerTarget = 1f;
                break;
            case InjuredPresentationMode.Normal:
            case InjuredPresentationMode.Suppressed:
            default:
                moveTarget = 0f;
                lowerTarget = 0f;
                break;
        }
    }

    /// <summary>StartInjuredMoveBlend 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Mathf.Approximately(float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)；下游（含内部回调体） Animator.GetLayerWeight(int)、Mathf.Approximately(float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonAnimatorDriver.StartInjuredMoveBlend(float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private void StartInjuredMoveBlend(float target, float duration)
    {
        // 正在向同一目标混合时保持现有进度，避免重复受击时重置权重
        if (blendInjuredLayer && Mathf.Approximately(injuredLayerTarget, target))
        {
            return;
        }

        injuredLayerWeight = animator.GetLayerWeight(LayerInjured);
        if (!blendInjuredLayer && Mathf.Approximately(injuredLayerWeight, target))
        {
            return;
        }

        injuredLayerBlendStart = injuredLayerWeight;
        injuredLayerTarget = target;
        injuredLayerBlendDuration = Mathf.Max(0.01f, duration);
        injuredLayerBlendElapsed = 0f;
        blendInjuredLayer = true;
    }

    /// <summary>StartInjuredLowerBlend 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Mathf.Approximately(float, float)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.StartInjuredBlendsForMode(InjuredPresentationMode, float, float)；下游（含内部回调体） Animator.GetLayerWeight(int)、Mathf.Approximately(float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标。使用位置：作为 Mathf.Approximately(float, float) 的参数、LeonAnimatorDriver.StartInjuredLowerBlend(float, float)。</param>
    /// <param name="duration">中文含义：持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    private void StartInjuredLowerBlend(float target, float duration)
    {
        if (blendInjuredLowerLayer && Mathf.Approximately(injuredLowerTarget, target))
        {
            return;
        }

        injuredLowerLayerWeight = animator.GetLayerWeight(LayerInjuredLowerBody);
        if (!blendInjuredLowerLayer && Mathf.Approximately(injuredLowerLayerWeight, target))
        {
            return;
        }

        injuredLowerBlendStart = injuredLowerLayerWeight;
        injuredLowerTarget = target;
        injuredLowerBlendDuration = Mathf.Max(0.01f, duration);
        injuredLowerBlendElapsed = 0f;
        blendInjuredLowerLayer = true;
    }

    /// <summary>UpdateInjuredLayerWeight 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateInjuredLayerWeight()
    {
        if (!blendInjuredLayer || animator == null)
        {
            return;
        }

        injuredLayerBlendElapsed = Mathf.Min(injuredLayerBlendElapsed + Time.deltaTime, injuredLayerBlendDuration);
        // progress（进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float progress = injuredLayerBlendElapsed / injuredLayerBlendDuration;
        injuredLayerWeight = Mathf.Lerp(injuredLayerBlendStart, injuredLayerTarget, progress);
        animator.SetLayerWeight(LayerInjured, injuredLayerWeight);
        if (injuredLayerBlendElapsed >= injuredLayerBlendDuration)
        {
            injuredLayerWeight = injuredLayerTarget;
            blendInjuredLayer = false;
        }
    }

    /// <summary>UpdateInjuredLowerLayerWeight 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Min(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateInjuredLowerLayerWeight()
    {
        if (!blendInjuredLowerLayer || animator == null)
        {
            return;
        }

        injuredLowerBlendElapsed = Mathf.Min(injuredLowerBlendElapsed + Time.deltaTime, injuredLowerBlendDuration);
        // progress（进度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float progress = injuredLowerBlendElapsed / injuredLowerBlendDuration;
        injuredLowerLayerWeight = Mathf.Lerp(injuredLowerBlendStart, injuredLowerTarget, progress);
        animator.SetLayerWeight(LayerInjuredLowerBody, injuredLowerLayerWeight);
        if (injuredLowerBlendElapsed >= injuredLowerBlendDuration)
        {
            injuredLowerLayerWeight = injuredLowerTarget;
            blendInjuredLowerLayer = false;
        }
    }

    /// <summary>
    /// 同帧立即写入两个受伤层权重（仅切枪提交与死亡/初始化使用）。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.CommitInjuredWeaponSwitchPresentation(WeaponType)、LeonAnimatorDriver.ResetInjuredLayerState()；下游（含内部回调体） Animator.SetLayerWeight(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="moveWeight">中文含义：移动权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)。</param>
    /// <param name="lowerWeight">中文含义：下部权重。使用位置：作为 Animator.SetLayerWeight(int, float) 的参数、LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)。</param>
    private void SetInjuredLayersImmediate(float moveWeight, float lowerWeight)
    {
        blendInjuredLayer = false;
        injuredLayerWeight = moveWeight;
        injuredLayerTarget = moveWeight;
        animator.SetLayerWeight(LayerInjured, moveWeight);
        blendInjuredLowerLayer = false;
        injuredLowerLayerWeight = lowerWeight;
        injuredLowerTarget = lowerWeight;
        animator.SetLayerWeight(LayerInjuredLowerBody, lowerWeight);
    }

    /// <summary>
    /// 立即清零两个受伤层并清除待处理目标。
    /// 仅用于初始化和死亡。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)；下游（含内部回调体） LeonAnimatorDriver.SetInjuredLayersImmediate(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetInjuredLayerState()
    {
        injuredLayerActive = false;
        injuredPresentation = InjuredPresentationMode.Normal;
        hasPendingInjuredTarget = false;
        pendingInjuredTarget = false;
        hasPendingInjuredPresentation = false;
        SetInjuredLayersImmediate(0f, 0f);
    }

    /// <summary>UpdateFullBodyLayerWeight 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.ClearFullBodyActionAfterFadeIfReady()、Mathf.Approximately(float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.ClearFullBodyActionAfterFadeIfReady()、Mathf.Approximately(float, float)、Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateFullBodyLayerWeight()
    {
        if (waitingForFullBodyStateEntry)
        {
            // 至少让目标状态在零权重下完成一次动画器求值，
            // 之后才允许覆盖层开始接管骨架。
            if (Time.frameCount <= fullBodyEntryRequestFrame)
            {
                return;
            }

            // state（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.UpdateFullBodyLayerWeight()。
AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
            if (state.shortNameHash != pendingFullBodyStateHash)
            {
                return;
            }

            waitingForFullBodyStateEntry = false;
            pendingFullBodyStateHash = 0;
            fullBodyEntryRequestFrame = -1;
            fullBodyLayerWeight = 0f;
            targetFullBodyLayerWeight = 1f;
            blendFullBodyLayerWeight = true;
            blendingFullBodyLayerEntry = true;
        }

        if (!blendFullBodyLayerWeight)
        {
            ClearFullBodyActionAfterFadeIfReady();
            return;
        }

        // argumentValue9（按原顺序缓存的实参0.01f）：从0.01f读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue9 = 0.01f;
        // selectedValue8（分支选出的配对动作全身图层退出混合时长）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、LeonAnimatorDriver.UpdateFullBodyLayerWeight()。
float selectedValue8;
        if (blendingQTEFullBodyExit)
        {
            selectedValue8 = qteFullBodyLayerExitBlendDuration;
        }
        else if (blendingFullBodyLayerEntry && MeleeActionKinds.IsWoodenBox(activeFullBodyAction))
        {
            selectedValue8 = 0.12f;
        }
        else
        {
            if (blendingFullBodyLayerEntry)
            {
                selectedValue8 = fullBodyLayerEntryBlendDuration;
            }
            else
            {
                selectedValue8 = knifeFullBodyLayerBlendDuration;
            }
        }

        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.MoveTowards(float, float, float) 的参数。
float duration = Mathf.Max(argumentValue9, selectedValue8);
        fullBodyLayerWeight = Mathf.MoveTowards(fullBodyLayerWeight, targetFullBodyLayerWeight, Time.deltaTime / duration);
        animator.SetLayerWeight(LayerFullBodyAction, fullBodyLayerWeight);
        if (Mathf.Approximately(fullBodyLayerWeight, targetFullBodyLayerWeight))
        {
            blendFullBodyLayerWeight = false;
            blendingFullBodyLayerEntry = false;
            blendingQTEFullBodyExit = false;
            ClearFullBodyActionAfterFadeIfReady();
        }
    }

    /// <summary>ClearFullBodyActionAfterFadeIfReady 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Animator.SetInteger(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.EndTraversalLanding()、LeonAnimatorDriver.UpdateFullBodyLayerWeight()；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetInteger(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearFullBodyActionAfterFadeIfReady()
    {
        if (!clearFullBodyActionAfterFade || animator.GetLayerWeight(LayerFullBodyAction) > 0.01f)
        {
            return;
        }

        animator.SetInteger(HashFullBodyAction, 0);
        clearFullBodyActionAfterFade = false;
    }

    /// <summary>PrepareFullBodyLayerEntry 的业务处理入口；本方法直接依赖 Animator.Play(int, int, float)、Animator.SetLayerWeight(int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.EnterDeath()；下游（含内部回调体） Animator.Play(int, int, float)、Animator.SetLayerWeight(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateHash">中文含义：状态哈希编号。使用位置：作为 Animator.Play(int, int, float) 的参数、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int)。</param>
    private void PrepareFullBodyLayerEntry(int stateHash, float normalizedStart = 0f)
    {
        waitingForFullBodyStateEntry = true;
        pendingFullBodyStateHash = stateHash;
        fullBodyEntryRequestFrame = Time.frameCount;
        blendFullBodyLayerWeight = false;
        blendingFullBodyLayerEntry = false;
        blendingQTEFullBodyExit = false;
        fullBodyLayerWeight = 0f;
        targetFullBodyLayerWeight = 1f;
        animator.SetLayerWeight(LayerFullBodyAction, 0f);
        animator.Play(stateHash, LayerFullBodyAction, normalizedStart);
    }

    /// <summary>CancelPendingFullBodyLayerEntry 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.EndTraversalLanding()、LeonAnimatorDriver.EnterDeath() 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CancelPendingFullBodyLayerEntry()
    {
        waitingForFullBodyStateEntry = false;
        pendingFullBodyStateHash = 0;
        fullBodyEntryRequestFrame = -1;
        blendingFullBodyLayerEntry = false;
    }

    /// <summary>BlendWeaponActionLayerTo 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Animator.SetLayerWeight(int, float)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、LeonAnimatorDriver.FinalizeWeaponActionLayer() 等共 6 处；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetLayerWeight(int, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetWeight">中文含义：目标权重。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    private void BlendWeaponActionLayerTo(float targetWeight)
    {
        if (animator == null)
        {
            return;
        }

        weaponActionLayerWeight = animator.GetLayerWeight(LayerWeaponAction);
        targetWeaponActionLayerWeight = Mathf.Clamp01(targetWeight);
        weaponActionSmoothExit = targetWeaponActionLayerWeight < 0.5f;
        weaponActionLayerBlendStartWeight = weaponActionLayerWeight;
        weaponActionLayerBlendElapsed = 0f;
        blendWeaponActionLayerWeight = true;
        animator.SetLayerWeight(LayerWeaponAction, weaponActionLayerWeight);
    }

    /// <summary>中文含义：武器动作动画图层权重。武器动作层的瞬时权重（供切枪提交检查是否达到遮挡阈值）。</summary>
    public float WeaponActionLayerWeight
    {
        get
        {
            if (animator != null)
            {
                return animator.GetLayerWeight(LayerWeaponAction);
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>
    /// 切枪提交前确保武器动作层达到满权重，禁止暴露底层切换。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetLayerWeight(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EnsureWeaponActionLayerFullWeight()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        // weight（权重）：保存 Animator.GetLayerWeight(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.EnsureWeaponActionLayerFullWeight()。
float weight = animator.GetLayerWeight(LayerWeaponAction);
        if (weight >= 0.95f)
        {
            return;
        }

        blendWeaponActionLayerWeight = false;
        weaponActionSmoothExit = false;
        weaponActionLayerWeight = 1f;
        targetWeaponActionLayerWeight = 1f;
        animator.SetLayerWeight(LayerWeaponAction, 1f);
    }

    /// <summary>ReadStartStepDisplacementRatio 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Mathf.InverseLerp(float, float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.HandleMovement()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetNextAnimatorStateInfo(int)、Animator.IsInTransition(int)、Mathf.InverseLerp(float, float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float ReadStartStepDisplacementRatio()
    {
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return 1f;
        }

        // state（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、LeonAnimatorDriver.ReadStartStepDisplacementRatio()。
var state = animator.GetCurrentAnimatorStateInfo(LayerBase);
        if (animator.IsInTransition(LayerBase))
        {
            // lowerOneState（下部第一状态）：保存 Animator.GetNextAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.ReadStartStepDisplacementRatio()。
var lowerOneState = animator.GetNextAnimatorStateInfo(LayerBase);
            if (lowerOneState.shortNameHash == startStepState)
            {
                state = lowerOneState;
            }
        }

        if (state.shortNameHash == HashBaseIdle || state.shortNameHash == recoverStepState || state.shortNameHash == inPlaceTurnState)
        {
            return 0f;
        }

        if (state.shortNameHash != startStepState)
        {
            return 1f;
        }

        // 脚尚在承重时只转移重心，迈出第一步后再接上循环步速。
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.48f, state.normalizedTime));
    }

    /// <summary>SetMovement 的业务处理入口；本方法直接依赖 Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.SnapToEightDirections(Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Normalize()、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.DampToZero()、LeonLocomotion.HandleMovement()、LeonLocomotion.ResumeAfterQTESuccess()、LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.SnapToEightDirections(Vector2)、Vector2.Dot(Vector2, Vector2)、Vector2.Normalize()、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontal">中文含义：水平。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Vector2.Vector2(float, float) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。</param>
    /// <param name="vertical">中文含义：竖直。使用位置：作为 Animator.SetFloat(int, float) 的参数、作为 Vector2.Vector2(float, float) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。</param>
    /// <param name="magnitude">中文含义：长度。使用位置：作为 Animator.SetFloat(int, float) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。</param>
    /// <param name="sprint">中文含义：是否冲刺。使用位置：作为 Animator.SetBool(int, bool) 的参数。</param>
    public void SetMovement(float horizontal, float vertical, float magnitude, bool sprint)
    {
        if (!CanWrite())
        {
            return;
        }

        if (traversalLandingBasePoseLocked)
        {
            hasInjuredSnappedDirection = false;
            animator.SetFloat(HashHorizontal, 0f);
            animator.SetFloat(HashVertical, 0f);
            animator.SetFloat(HashInputMagnitude, 0f);
            animator.SetBool(HashSprint, false);
            return;
        }

        // animationDirection（动画方向）：使用位置：作为 LeonAnimatorDriver.SnapToEightDirections(Vector2) 的参数、作为 Vector2.Dot(Vector2, Vector2) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。
Vector2 animationDirection = new Vector2(horizontal, vertical);
        if (injuredLayerActive && !isCrouching && magnitude > 0.01f && animationDirection.sqrMagnitude > 0.0001f)
        {
            animationDirection.Normalize();
            // candidate（候选）：保存 LeonAnimatorDriver.SnapToEightDirections(Vector2) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Dot(Vector2, Vector2) 的参数、LeonAnimatorDriver.SetMovement(float, float, float, bool)。
Vector2 candidate = SnapToEightDirections(animationDirection);
            if (hasInjuredSnappedDirection)
            {
                // candidateDot（候选点积）：保存 Vector2.Dot(Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.SetMovement(float, float, float, bool)。
float candidateDot = Vector2.Dot(animationDirection, candidate);
                // currentDot（当前点积）：保存 Vector2.Dot(Vector2, Vector2) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.SetMovement(float, float, float, bool)。
float currentDot = Vector2.Dot(animationDirection, injuredSnappedDirection);
                if (candidateDot - currentDot < injuredDirectionHysteresis)
                {
                    candidate = injuredSnappedDirection;
                }
            }

            injuredSnappedDirection = candidate;
            hasInjuredSnappedDirection = true;
            horizontal = candidate.x;
            vertical = candidate.y;
        }
        else
        {
            hasInjuredSnappedDirection = false;
        }

        animator.SetFloat(HashHorizontal, horizontal);
        animator.SetFloat(HashVertical, vertical);
        animator.SetFloat(HashInputMagnitude, magnitude);
        animator.SetBool(HashSprint, sprint);
    }

    /// <summary>
    /// 着地动作开始前把基础移动层归入中性站立姿态。
    /// 防止全身层淡出时暴露其下方的冲刺停止状态。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)；下游（含内部回调体） Animator.CrossFadeInFixedTime(int, float, int, float)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PrepareTraversalLandingBasePose()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        traversalLandingBasePoseLocked = true;
        hasInjuredSnappedDirection = false;
        animator.SetFloat(HashHorizontal, 0f);
        animator.SetFloat(HashVertical, 0f);
        animator.SetFloat(HashInputMagnitude, 0f);
        animator.SetBool(HashSprint, false);
        animator.CrossFadeInFixedTime(HashBaseIdle, 0.08f, LayerBase, 0f);
    }

    /// <summary>
    /// 着地事务完全退出后允许基础移动层重新读取玩家输入。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)、LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReleaseTraversalLandingBasePose()
    {
        traversalLandingBasePoseLocked = false;
    }

    /// <summary>SnapToEightDirections 的业务处理入口；本方法直接依赖 Mathf.Atan2(float, float)、Mathf.Cos(float)、Mathf.Round(float)、Mathf.Sin(float)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.SetMovement(float, float, float, bool)；下游（含内部回调体） Mathf.Atan2(float, float)、Mathf.Cos(float)、Mathf.Round(float)、Mathf.Sin(float)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="direction">中文含义：方向。使用位置：作为 Mathf.Atan2(float, float) 的参数。</param>
    private static Vector2 SnapToEightDirections(Vector2 direction)
    {
        // sectorAngle（扇区角度）：使用位置：作为 Mathf.Round(float) 的参数、LeonAnimatorDriver.SnapToEightDirections(Vector2)。
float sectorAngle = Mathf.PI * 0.25f;
        // angle（角度）：保存 Mathf.Atan2(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Round(float) 的参数。
float angle = Mathf.Atan2(direction.x, direction.y);
        // snappedAngle（吸附后的角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float snappedAngle = Mathf.Round(angle / sectorAngle) * sectorAngle;
        return new Vector2(Mathf.Sin(snappedAngle), Mathf.Cos(snappedAngle));
    }

    /// <summary>SetCrouching 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Animator.SetBool(int, bool)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.IsLongGun(WeaponType)、LeonAnimatorDriver.ResetInjuredPresentation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyCrouchState(bool)、LeonController.InitPipeline()；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetBool(int, bool)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.IsLongGun(WeaponType)、LeonAnimatorDriver.ResetInjuredPresentation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="crouching">中文含义：是否蹲伏中。使用位置：作为 Animator.SetBool(int, bool) 的参数、LeonAnimatorDriver.SetCrouching(bool)。</param>
    public void SetCrouching(bool crouching)
    {
        if (!CanWrite())
        {
            return;
        }

        isCrouching = crouching;
        WriteLegacyCrouchParameter();
        // 蹲伏时两个受伤层临时淡出，站起后按当前状态恢复
        ResetInjuredPresentation();
        if (crouching)
        {
            longGunCrouchLayerExitPending = false;
            // selectedValue11（分支选出的1f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float) 的参数、LeonAnimatorDriver.SetCrouching(bool)。
float selectedValue11;
            if (IsLongGun(currentWeaponType) && !SupportsNativeLongGunCrouch)
            {
                selectedValue11 = 1f;
            }
            else
            {
                selectedValue11 = 0f;
            }

            BlendLongGunCrouchLayerTo(selectedValue11);
        }
        else if (IsLongGun(currentWeaponType) && animator.GetLayerWeight(LayerRifleCrouch) > 0f)
        {
            longGunCrouchLayerExitPending = true;
        }
        else
        {
            longGunCrouchLayerExitPending = false;
            BlendLongGunCrouchLayerTo(0f);
        }
    }

    /// <summary>SetAimAngles 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.SynchronizeEnterAimAngle()、LeonController.UpdateAnimatorParameters()；下游（含内部回调体） Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontalAngle">中文含义：水平角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    /// <param name="verticalAngle">中文含义：竖直角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    public void SetAimAngles(float horizontalAngle, float verticalAngle)
    {
        if (!CanWrite())
        {
            return;
        }

        animator.SetFloat(HashHorAimAngle, Mathf.Clamp(horizontalAngle, -60f, 60f));
        animator.SetFloat(HashVerAimAngle, Mathf.Clamp(verticalAngle, -45f, 45f));
    }

    /// <summary>SetAimState 的业务处理入口；本方法直接依赖 Animator.HasState(int, int)、Animator.Play(int, int, float)、Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ForceAimOff()、LeonCombat.HandleAim(bool)、LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.RestoreStateAfterWeaponAction()；下游（含内部回调体） Animator.HasState(int, int)、Animator.Play(int, int, float)、Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="aiming">中文含义：是否瞄准中。使用位置：作为 LeonAnimatorDriver.RefreshInjuredPresentationForAim(bool) 的参数、LeonAnimatorDriver.SetAimState(bool)。</param>
    public void SetAimState(bool aiming)
    {
        if (!CanWrite())
        {
            return;
        }

        // callReceiver14（调用前缓存的动画控制器）：从动画控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonAnimatorDriver.SetAimState(bool)。
global::UnityEngine.Animator callReceiver14 = animator;
        // argumentValue13（按原顺序缓存的实参瞄准的哈希编号（动画参数或状态路径的哈希编号））：从瞄准的哈希编号读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Animator.SetFloat(int, float) 的参数。
int argumentValue13 = HashAim;
        // selectedValue12（分支选出的1f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Animator.SetFloat(int, float) 的参数、LeonAnimatorDriver.SetAimState(bool)。
float selectedValue12;
        if (aiming)
        {
            selectedValue12 = 1f;
        }
        else
        {
            selectedValue12 = 0f;
        }

        callReceiver14.SetFloat(argumentValue13, selectedValue12);
        if (aiming && !currentAiming && !UsesNativePistolAim && animator.HasState(LayerAim, aimPoseHash))
        {
            // 角度素材包含从平视转向目标的一秒过程；偏移层直接采样末端姿态。
            // 仅定位瞄准层，不跳过基础动作、收枪、掏枪或其他动作层。
            animator.Play(aimPoseHash, LayerAim, 1f);
        }

        currentAiming = aiming;
        UpdateNativeGunPose();
        // 手枪瞄准的受伤层交换保留平滑切换（0.14 秒进入 / 0.18 秒退出）
        RefreshInjuredPresentationForAim(aiming);
    }

    /// <summary>SetAimLayerWeight 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ForceAimOff()、LeonCombat.HandleAim(bool)、LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonCombat.Update()；下游（含内部回调体） Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weight">中文含义：权重。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public void SetAimLayerWeight(float weight)
    {
        if (!CanWrite())
        {
            return;
        }

        compatibilityAimLayerWeight = Mathf.Clamp01(weight);
        float layerWeight = compatibilityAimLayerWeight;
        if (CanDriveNativeCqbrAim || NativeCqbrOwnsAimArms || CanDriveNativeW870Aim || NativeW870OwnsAimArms
            || NativePistolOwnsAimArms || currentAiming && UsesNativePistolAim)
        {
            layerWeight = 0f;
        }
        animator.SetLayerWeight(LayerAim, layerWeight);
    }

    /// <summary>
    /// 切换基础层的小刀姿态分支。
    /// 该参数只在状态内部选择动画，不参与状态转换。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)、LeonCombat.ApplyGuardPose()、LeonCombat.BeginTemporaryKnife()、LeonCombat.EndTemporaryKnife(bool) 等共 9 处；下游（含内部回调体） Animator.GetFloat(int)、Animator.SetFloat(int, float)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.CanWrite()、Mathf.Approximately(float, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="active">中文含义：是否生效。使用位置：LeonAnimatorDriver.SetKnifeMode(bool)。</param>
    public void SetKnifeMode(bool active)
    {
        if (!CanWrite())
        {
            return;
        }

        if (active && currentWeaponType != WeaponType.Knife)
        {
            currentWeaponType = WeaponType.Knife;
            longGunCrouchLayerExitPending = false;
            BlendLongGunCrouchLayerTo(0f);
        }

        currentKnifeMode = Mathf.Clamp01(animator.GetFloat(HashKnifeMode));
        if (active)
        {
            targetKnifeMode = 1f;
        }
        else
        {
            targetKnifeMode = 0f;
        }

        blendKnifeMode = !Mathf.Approximately(currentKnifeMode, targetKnifeMode);
        if (!blendKnifeMode)
        {
            animator.SetFloat(HashKnifeMode, targetKnifeMode);
        }
    }

    /// <summary>SetKnifeModeImmediate 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)；下游（含内部回调体） Animator.SetFloat(int, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="active">中文含义：是否生效。使用位置：LeonAnimatorDriver.SetKnifeModeImmediate(bool)。</param>
    private void SetKnifeModeImmediate(bool active)
    {
        if (active)
        {
            currentKnifeMode = 1f;
        }
        else
        {
            currentKnifeMode = 0f;
        }

        targetKnifeMode = currentKnifeMode;
        blendKnifeMode = false;
        animator.SetFloat(HashKnifeMode, currentKnifeMode);
    }

    /// <summary>中文含义：小刀模式混合值。</summary>
    public float KnifeModeBlendValue => currentKnifeMode;

    /// <summary>SetTurnAngle 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonLocomotion.ReleaseTurnAngle()、LeonLocomotion.ResetTurnAngle()、LeonLocomotion.UpdateTurnAngleFromCamera(Camera, bool, bool, bool, bool)；下游（含内部回调体） Animator.SetFloat(int, float)、LeonAnimatorDriver.CanWrite()、Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="angle">中文含义：角度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    public void SetTurnAngle(float angle)
    {
        if (!CanWrite())
        {
            return;
        }

        animator.SetFloat(HashTurnAngle, Mathf.Clamp(angle, -180f, 180f));
    }

    /// <summary>TriggerFire 的业务处理入口；本方法直接依赖 Animator.SetTrigger(int)、LeonAnimatorDriver.CanWrite()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()、LeonCombat.Update()；下游（含内部回调体） Animator.SetTrigger(int)、LeonAnimatorDriver.CanWrite()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TriggerFire() => TriggerFire(false);

    internal void TriggerFire(bool lastRound)
    {
        if (!CanWrite())
        {
            return;
        }

        if (!TryPlayNativePistolShot(lastRound) && !TryPlayNativeCqbrShot(lastRound) && !TryPlayNativeW870Shot(lastRound))
        {
            animator.SetTrigger(HashFire);
        }
    }

    /// <summary>SetWeaponType 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、Debug.LogWarning(object)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.IsLongGun(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimRig.ExitQTEIK(WeaponType)、LeonAnimRig.RestoreQTEWeaponTypeAfterFullBodyExit() 等共 10 处；下游（含内部回调体） Animator.SetFloat(int, float)、Debug.LogWarning(object)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.IsLongGun(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前武器类别。使用位置：作为 Debug.LogWarning(object) 的参数、LeonAnimatorDriver.SetWeaponType(WeaponType)。</param>
    public bool SetWeaponType(WeaponType type)
    {
        if (!CanWrite())
        {
            return false;
        }

        // value（值）：使用位置：作为 Animator.SetFloat(int, float) 的参数、LeonAnimatorDriver.SetWeaponType(WeaponType)。
float value;
        switch (type)
        {
            case WeaponType.Pistol:
                value = 0f;
                break;
            case WeaponType.Rifle:
                value = 1f;
                break;
            case WeaponType.Shotgun:
                value = 3f;
                break;
            default:
                Debug.LogWarning("[LeonAnimatorDriver] 不支持的武器类型: " + type + "，已拒绝写入");
                return false;
        }

        animator.SetFloat(HashWeaponType, value);
        currentWeaponType = type;
        // SetWeaponType 不自动启动受伤层混合：
        // 切枪期间的受伤层切换由 CommitInjuredWeaponSwitchPresentation 在武器提交帧同帧执行。
        if (!isCrouching && !longGunCrouchLayerExitPending)
        {
            longGunCrouchLayerExitPending = false;
            BlendLongGunCrouchLayerTo(0f);
        }
        else if (isCrouching)
        {
            longGunCrouchLayerExitPending = false;
            // selectedValue17（分支选出的1f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float) 的参数、LeonAnimatorDriver.SetWeaponType(WeaponType)。
float selectedValue17;
            if (IsLongGun(currentWeaponType) && !SupportsNativeLongGunCrouch)
            {
                selectedValue17 = 1f;
            }
            else
            {
                selectedValue17 = 0f;
            }

            BlendLongGunCrouchLayerTo(selectedValue17);
        }

        return true;
    }

    /// <summary>
    /// 长枪蹲伏层只在角色蹲下且持有步枪或霰弹枪时启用。
    /// 站立时保持零权重，避免空状态覆盖基础姿势。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EnterDeath()、LeonAnimatorDriver.SetCrouching(bool)、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimatorDriver.UpdateLongGunCrouchLayerExit()；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetLayerWeight(int, float)、Mathf.Approximately(float, float)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetWeight">中文含义：目标权重。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    private void BlendLongGunCrouchLayerTo(float targetWeight)
    {
        if (animator == null)
        {
            return;
        }

        longGunCrouchLayerWeight = animator.GetLayerWeight(LayerRifleCrouch);
        longGunCrouchLayerBlendStart = longGunCrouchLayerWeight;
        longGunCrouchLayerTarget = Mathf.Clamp01(targetWeight);
        longGunCrouchLayerBlendElapsed = 0f;
        blendLongGunCrouchLayer = !Mathf.Approximately(longGunCrouchLayerWeight, longGunCrouchLayerTarget);
        if (!blendLongGunCrouchLayer)
        {
            animator.SetLayerWeight(LayerRifleCrouch, longGunCrouchLayerTarget);
        }
    }

    /// <summary>UpdateLongGunCrouchLayerBlend 的业务处理入口；本方法直接依赖 Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.SetLayerWeight(int, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateLongGunCrouchLayerBlend()
    {
        if (!blendLongGunCrouchLayer || animator == null)
        {
            return;
        }

        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimatorDriver.UpdateLongGunCrouchLayerBlend()。
        float duration = Mathf.Max(0.01f, longGunCrouchLayerBlendDuration);
        longGunCrouchLayerBlendElapsed = Mathf.Min(longGunCrouchLayerBlendElapsed + Time.deltaTime, duration);
        // progress（进度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float progress = Mathf.SmoothStep(0f, 1f, longGunCrouchLayerBlendElapsed / duration);
        longGunCrouchLayerWeight = Mathf.Lerp(longGunCrouchLayerBlendStart, longGunCrouchLayerTarget, progress);
        animator.SetLayerWeight(LayerRifleCrouch, longGunCrouchLayerWeight);
        if (longGunCrouchLayerBlendElapsed >= duration)
        {
            blendLongGunCrouchLayer = false;
        }
    }

    /// <summary>UpdateLongGunCrouchLayerExit 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.Update()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.IsInTransition(int)、LeonAnimatorDriver.BlendLongGunCrouchLayerTo(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateLongGunCrouchLayerExit()
    {
        if (!longGunCrouchLayerExitPending || animator == null)
        {
            return;
        }

        // state（当前动画状态信息）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.UpdateLongGunCrouchLayerExit()。
AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(LayerRifleCrouch);
        if (animator.IsInTransition(LayerRifleCrouch) || state.shortNameHash != HashRifleCrouchEmpty)
        {
            return;
        }

        longGunCrouchLayerExitPending = false;
        BlendLongGunCrouchLayerTo(0f);
    }

    /// <summary>IsLongGun 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.SetCrouching(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前武器类别。使用位置：LeonAnimatorDriver.IsLongGun(WeaponType)。</param>
    private static bool IsLongGun(WeaponType type)
    {
        return type == WeaponType.Rifle || type == WeaponType.Shotgun;
    }

    /// <summary>BeginWeaponAction 的业务处理入口；本方法直接依赖 Animator.SetInteger(int, int)、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonAnimationActionCoordinator.RequestReload(WeaponType)、LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；下游（含内部回调体） Animator.SetInteger(int, int)、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 Animator.SetInteger(int, int) 的参数、LeonAnimatorDriver.BeginWeaponAction(WeaponActionType)。</param>
    public void BeginWeaponAction(WeaponActionType actionType)
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        if (actionType == WeaponActionType.None)
        {
            return;
        }

        if (actionType == WeaponActionType.Holster && SupportsNativeW870Switch)
        {
            animator.SetFloat(HolsterSourceWeaponHash, animator.GetFloat(HashWeaponType));
        }
        BlendWeaponActionLayerTo(1f);
        animator.SetInteger(HashWeaponAction, ResolveWeaponActionParameter(actionType));
    }

    /// <summary>EndWeaponAction 的业务处理入口；本方法直接依赖 Animator.SetInteger(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.CompleteReloadAfterBusinessCommit(int)、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.HandleWeaponCompleted(Animator, WeaponActionType, int)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()；下游（含内部回调体） Animator.SetInteger(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndWeaponAction()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        if (UsesNativeReloadLayerFade && (TryReadNativeCqbrReload(out _) || IsNativeW870ReloadPlaying() || TryReadNativePistolReload(out _))
            || UsesWeaponEquipLayerFade || NativePistolHolsterActive && IsNativePistolSwitchPlaying())
        {
            if (targetWeaponActionLayerWeight > 0f)
            {
                BlendWeaponActionLayerTo(0f);
            }
            return;
        }

        animator.SetInteger(HashWeaponAction, 0);
    }

    /// <summary>
    /// 状态完全退出后将武器动作层权重平滑降为零。
    /// 不得在状态退出前调用。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()；下游（含内部回调体） LeonAnimatorDriver.BlendWeaponActionLayerTo(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void FinalizeWeaponActionLayer()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        BlendWeaponActionLayerTo(0f);
    }

    /// <summary>
    /// 从 Holster 连续进入 Equip，保持 L3 权重为1。
    /// 不清零 WeaponAction，不降图层权重。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)；下游（含内部回调体） Animator.SetInteger(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TransitionToEquip()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        animator.SetInteger(HashWeaponAction, ResolveWeaponActionParameter(WeaponActionType.Equip));
    }

    /// <summary>BeginHit 的业务处理入口；本方法直接依赖 Animator.SetFloat(int, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)；下游（含内部回调体） Animator.SetFloat(int, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hitDirection">中文含义：命中方向。使用位置：作为 Animator.SetFloat(int, float) 的参数。</param>
    public void BeginHit(LeonHitDirection hitDirection)
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        animator.SetFloat(HashHitDir, (float)hitDirection);
        BeginFullBodyAction(FullBodyActionType.Hit);
    }

    /// <summary>EndHit 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.EndFullBodyAction()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimatorDriver.EndFullBodyAction()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndHit()
    {
        EndFullBodyAction();
    }

    /// <summary>中文含义：是否独立小刀暂停。开始或推进全身动作。第4层权重在整个事务期间保持为一。</summary>
    internal const string NativeKnifeCarryStateName = "NativeKnifeCarry";
    internal const int NativeKnifeHideFrames = 36;
    internal const int NativeKnifeConcealFrame = 15;
    private static readonly int NativeKnifeCarryState = Animator.StringToHash(NativeKnifeCarryStateName);
    private bool nativeKnifeCarryActive;
    internal bool SupportsNativeKnifeCarry => IsValid && animator.HasState(LayerRifleCrouch, NativeKnifeCarryState);
    internal bool IsBlendingOutKnifePose => knifePoseExiting;

    private void UpdateNativeKnifeCarry()
    {
        bool carry = currentWeaponType == WeaponType.Knife && targetKnifeMode > 0f && SupportsNativeKnifeCarry
            && !(ownsNativeGait && nativeKnifeGait);
        if (nativeKnifeCarryActive == carry)
        {
            return;
        }

        nativeKnifeCarryActive = carry;
        if (carry)
        {
            animator.Play(NativeKnifeCarryState, LayerRifleCrouch, 0f);
            BlendLongGunCrouchLayerTo(1f);
        }
        else if (currentWeaponType == WeaponType.Knife)
        {
            BlendLongGunCrouchLayerTo(0f);
        }
    }

    private bool independentKnifePause;
    private float legacyParrySpeed;
    private bool releasedThrustPose;
    private float releasedThrustElapsed;

    internal void SetLegacyParryHitStop(bool enabled)
    {
        if (animator == null)
        {
            legacyParrySpeed = 0f;
            return;
        }
        if (enabled)
        {
            if (animator.speed > 0f)
            {
                legacyParrySpeed = animator.speed;
                animator.speed = 0f;
            }
            return;
        }
        if (legacyParrySpeed > 0f && Mathf.Approximately(animator.speed, 0f))
        {
            animator.speed = legacyParrySpeed;
        }
        legacyParrySpeed = 0f;
    }
    /// <summary>中文含义：小刀暂停前方速度。使用位置：LeonAnimatorDriver.SetKnifeLocalHitStop(bool)。</summary>
    private float knifePauseFrontSpeed = 1f;
    /// <summary>PlayIndependentKnifeClip 的业务处理入口；本方法直接依赖 Animator.CrossFadeInFixedTime(int, float, int, float)、Animator.HasState(int, int)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、Animator.StringToHash(string)、KnifeCombatRules.IsReaction(KnifeAction)、KnifePoseSampler.BeginBlend(float, float) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceIndependentKnifeTransaction(int, KnifeActionConfiguration, float, float)；下游（含内部回调体） Animator.CrossFadeInFixedTime(int, float, int, float)、Animator.HasState(int, int)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、Animator.StringToHash(string)、KnifeCombatRules.IsReaction(KnifeAction)、KnifePoseSampler.BeginBlend(float, float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">中文含义：动画片段。使用位置：作为 Animator.StringToHash(string) 的参数、作为 KnifeCombatRules.IsReaction(KnifeAction) 的参数、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。</param>
    /// <param name="blend">中文含义：混合。使用位置：作为 Mathf.Max(float, float) 的参数、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。</param>
    /// <param name="offset">中文含义：偏移量。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    public bool PlayIndependentKnifeClip(KnifeActionConfiguration clip, float blend, float offset)
    {
        if (animator == null || !IsValid || clip == null || clip.clip == null)
        {
            return false;
        }

        // state（状态）：保存 Animator.StringToHash(string) 的返回结果，供当前步骤使用。使用位置：作为 Animator.CrossFadeInFixedTime(int, float, int, float) 的参数、作为 Animator.HasState(int, int) 的参数。
int state = Animator.StringToHash(clip.StatePath);
        if (!animator.HasState(LayerWeaponAction, state))
        {
            return false;
        }

        activeMeleeOnWeaponLayer = FullBodyActionType.KnifeQuickAttack;
        activeFullBodyAction = FullBodyActionType.KnifeQuickAttack;
        animator.SetInteger(HashFullBodyAction, (int)FullBodyActionType.KnifeQuickAttack);
        animator.SetInteger(HashWeaponAction, 0);
        CancelPendingFullBodyLayerEntry();
        blendFullBodyLayerWeight = false;
        fullBodyLayerWeight = targetFullBodyLayerWeight = 0f;
        animator.SetLayerWeight(LayerFullBodyAction, 0f);
        BlendWeaponActionLayerTo(1f);
        animator.CrossFadeInFixedTime(state, Mathf.Max(.001f, blend), LayerWeaponAction, Mathf.Max(0, offset));
        if (knifeModule != null && knifeModule.configuration.poseSkeleton != null)
        {
            if (knifePoseSampler == null)
            {
                knifePoseSampler = new KnifePoseSampler(knifeModule.configuration, animator);
            }

            // poseBlend（姿态混合）：使用位置：作为 KnifePoseSampler.BeginBlend(float, float) 的参数、作为 Mathf.Max(float, float) 的参数、LeonAnimatorDriver.PlayIndependentKnifeClip(KnifeActionConfiguration, float, float)。
            float poseBlend = blend;
            float elapsedBlend = 0f;
            if (GameArchitecture.KnifeCombatRules.IsReaction(clip.action))
            {
                // 弹反动作从接触标记开始取姿势，接触当帧就显示出来。
                elapsedBlend = Mathf.Max(.001f, poseBlend);
            }
            else if (!knifePoseActive)
            {
                poseBlend = Mathf.Max(poseBlend, knifePoseEnterBlendDuration);
            }

            knifePoseSampler.BeginBlend(poseBlend, elapsedBlend);
            if (!knifePoseActive)
            {
                knifeLocomotionTime = 0;
                knifeMovementMode = 0;
            }

            knifePoseActive = true;
            knifePoseExiting = false;
            // 独立采样器负责完整姿态。底层保留正常枪械求值，供退出时连续衔接。
            blendWeaponActionLayerWeight = false;
            weaponActionSmoothExit = false;
            weaponActionLayerWeight = 0f;
            targetWeaponActionLayerWeight = 0f;
            animator.SetLayerWeight(LayerWeaponAction, 0f);
        }

        return true;
    }

    /// <summary>SetKnifeLocalHitStop 的业务处理入口；本方法直接依赖 Mathf.Approximately(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.Interrupt(bool)、KnifeCombatModule.ResolveIncomingAttack(KnifeIncomingAttackContext)、KnifeCombatModule.Update()、LeonAnimatorDriver.EndIndependentKnifeClip()、LeonAnimatorDriver.OnDisable()；下游（含内部回调体） Mathf.Approximately(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pause">中文含义：是否暂停。使用位置：LeonAnimatorDriver.SetKnifeLocalHitStop(bool)。</param>
    public void SetKnifeLocalHitStop(bool pause)
    {
        if (animator == null || pause == independentKnifePause)
        {
            return;
        }

        independentKnifePause = pause;
        if (pause)
        {
            knifePauseFrontSpeed = animator.speed;
            animator.speed = 0;
        }
        else if (Mathf.Approximately(animator.speed, 0))
        {
            animator.speed = knifePauseFrontSpeed;
        }
    }

    /// <summary>EndIndependentKnifeClip 的业务处理入口；本方法直接依赖 LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)；下游（含内部回调体） LeonAnimatorDriver.BeginKnifePoseExit()、LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndIndependentKnifeClip(bool releasedThrust = false)
    {
        BeginKnifePoseExit();
        releasedThrustPose = releasedThrust && knifePoseExiting;
        releasedThrustElapsed = 0f;
        if (releasedThrustPose)
        {
            knifePoseSampler.BeginBlend(.08f, 0f);
        }
        SetKnifeLocalHitStop(false);
        EndFullBodyAction();
        FinalizeFullBodyActionLayer();
    }

    /// <summary>BeginKnifePoseExit 的业务处理入口；本方法直接依赖 Animator.Play(int, int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、KnifePoseSampler.BeginBlend(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EndIndependentKnifeClip()、LeonAnimatorDriver.LateUpdate()；下游（含内部回调体） Animator.Play(int, int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、KnifePoseSampler.BeginBlend(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginKnifePoseExit()
    {
        if (!knifePoseActive)
        {
            return;
        }

        if (knifePoseSampler != null)
        {
            knifePoseSampler.BeginBlend(knifePoseExitBlendDuration, 0);
            knifePoseExiting = true;
        }

        knifePoseActive = false;
        // 降权不会退出状态。必须回到武器空状态，后续换弹和收放枪才有合法入口。
        animator.SetInteger(HashWeaponAction, 0);
        animator.Play(weaponActionEmptyState, LayerWeaponAction, 0f);
        blendWeaponActionLayerWeight = false;
        weaponActionSmoothExit = false;
        weaponActionLayerWeight = 0f;
        targetWeaponActionLayerWeight = 0f;
        animator.SetLayerWeight(LayerWeaponAction, 0f);
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifeCombatConfiguration.Find(KnifeAction)、KnifeCombatModule.ResolveContactsAfterPose()、KnifeCombatModule.ResolveIncomingContacts()、KnifeCombatRules.IsReaction(KnifeAction)、KnifePoseSampler.Apply(float)、KnifePoseSampler.ApplyExit(float)、KnifePoseSampler.BeginBlend(float, float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LateUpdate()
    {
        if (EnemyAIManager.HasInstance)
        {
            EnemyAIManager.Instance.ResolveStandingImpacts();
        }

        if (knifeModule != null)
        {
            // 敌人已在动画求值后提交接触，先裁定反应，再提交本帧实际刀姿。
            knifeModule.ResolveIncomingContacts();
        }

        if (knifePoseActive && (knifeModule == null || !knifeModule.Active))
        {
            // 规则结束的同一帧就保住最后显示姿态，不能等接触结算后再启动过渡。
            BeginKnifePoseExit();
        }

        if (knifePoseActive && knifeModule != null && knifeModule.Active)
        {
            // action（动作）：保存 KnifeCombatConfiguration.Find(KnifeAction) 的返回结果，供当前步骤使用。使用位置：作为 KnifeCombatRules.IsReaction(KnifeAction) 的参数、作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、LeonAnimatorDriver.LateUpdate()。
KnifeActionConfiguration action = knifeModule.configuration.Find(knifeModule.CurrentAction);
            if (action != null && action.sourceClip != null && knifePoseSampler != null)
            {
                // delta（增量）：使用位置：LeonAnimatorDriver.LateUpdate()。
float delta = Time.deltaTime;
                if (knifeModule.IsHitStop)
                {
                    delta = 0;
                }

                // velocity（速度）：保存 Transform.InverseTransformDirection(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、LeonAnimatorDriver.LateUpdate()。
Vector3 velocity = transform.InverseTransformDirection(ownerController.HorizontalSpeed);
                // mode（模式）：使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数、LeonAnimatorDriver.LateUpdate()。
int mode = 0;
                // nominalSpeed（标称速度）：使用位置：LeonAnimatorDriver.LateUpdate()。
float nominalSpeed = 1.83f;
                if (!action.HasPlanarRoot && velocity.sqrMagnitude > .0025f)
                {
                    if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.z))
                    {
                        mode = 3;
                        nominalSpeed = 1.51f;
                        if (velocity.x > 0)
                        {
                            mode = 4;
                            nominalSpeed = 1.95f;
                        }
                    }
                    else
                    {
                        mode = 1;
                        if (velocity.z < 0)
                        {
                            mode = 2;
                            nominalSpeed = 1.68f;
                        }
                    }
                }

                if (mode != knifeMovementMode)
                {
                    knifePoseSampler.BeginBlend(.12f, 0);
                    knifeMovementMode = mode;
                }

                knifeLocomotionTime += delta * velocity.magnitude / nominalSpeed;
                // originalGrip（是否原始握持）：使用位置：作为 KnifePoseSampler.Sample(AnimationClip, float, int, float, float, bool) 的参数。
bool originalGrip = action.action == GameArchitecture.KnifeAction.OutwardDeflectionNormal || action.action == GameArchitecture.KnifeAction.OutwardDeflectionPerfect;
                AnimationClip source = action.sourceClip;
                if (knifeModule.HasConfirmedThrustHit && action.confirmedHitClip != null)
                {
                    source = action.confirmedHitClip;
                    if (knifeHitBlendInstance != knifeModule.CurrentInstance)
                    {
                        knifeHitBlendInstance = knifeModule.CurrentInstance;
                        knifePoseSampler.BeginBlend(knifeModule.configuration.contactBlend, 0f);
                    }
                }
                knifePoseSampler.Sample(source, action.sourceStart + (float)knifeModule.CurrentActionTime, mode, knifeLocomotionTime, 1f, originalGrip);
                float guardTurn = 0f;
                if (knifeModule.IsGuardAiming && !knifeModule.IsAttack && !knifeModule.IsHitStop && mode == 0)
                {
                    guardTurn = knifeModule.GuardTurnSpeed;
                }
                knifePoseSampler.ApplyGuardFootwork(guardTurn, delta);
                // poseDelta（姿态增量）：使用位置：作为 KnifePoseSampler.Apply(float) 的参数、LeonAnimatorDriver.LateUpdate()。
float poseDelta = delta;
                if (GameArchitecture.KnifeCombatRules.IsReaction(action.action))
                {
                    // 接触姿态需要在局部停顿中完成短混合，动作时间仍停在接触标记。
                    poseDelta = Time.deltaTime;
                }

                float lowerBodySettle = 0f;
                if (mode == 0 && !action.sourceClip.humanMotion
                    && action.action is GameArchitecture.KnifeAction.LowerGuard or GameArchitecture.KnifeAction.ThrustRecovery)
                {
                    // 在收招尾段先把站姿交回基础层，再让接地系统恢复。
                    float progress = Mathf.InverseLerp(action.timing.duration - .26f, action.timing.duration,
                        (float)knifeModule.CurrentActionTime);
                    lowerBodySettle = Mathf.SmoothStep(0f, 1f, progress);
                }
                knifePoseSampler.Apply(poseDelta, lowerBodySettle);
                if (knifeFootsteps == null)
                {
                    TryGetComponent(out knifeFootsteps);
                }

                if (knifeFootsteps != null)
                {
                    knifeFootsteps.SampleKnifeFootsteps(mode, knifePoseSampler.LocomotionClip, knifeLocomotionTime,
                        knifePoseSampler.ReadLeftFootHeightDifference(), delta > 0f);
                }
                // 原版刀姿求值之后再叠加视线俯仰，使画面和实际刀刃接触使用同一姿态。
                if (knifeModule.handConstraint != null)
                {
                    knifeModule.handConstraint.ApplyKnifeAimAfterPose();
                }
                if (knifeVisuals != null)
                {
                    knifeVisuals.ApplyKnifeGripAfterPose();
                }
            }
        }
        else if (knifePoseExiting && knifePoseSampler != null)
        {
            bool pickup = ownerController != null && ownerController.AnimCoordinator != null
                && ownerController.AnimCoordinator.IsItemPickupActive;
            // interrupted（是否已中断）：使用位置：LeonAnimatorDriver.LateUpdate()。
bool interrupted = ownerController == null || ownerController.currentState == LeonState.Dead || ownerController.currentState == LeonState.Staggered || ownerController.currentState == LeonState.QTE
                || ownerController.currentState == LeonState.Traversing && !pickup;
            if (interrupted)
            {
                knifePoseExiting = false;
                releasedThrustPose = false;
                knifePoseSampler.ReleasePresentation();
            }
            else if (releasedThrustPose)
            {
                UpdateReleasedThrustPose(pickup);
            }
            else if (pickup)
            {
                knifePoseExiting = false;
                knifePoseSampler.ReleasePresentation();
            }
            else
            {
                knifePoseExiting = knifePoseSampler.ApplyExit(Time.deltaTime);
            }
        }

        if (knifeModule != null)
        {
            knifeModule.ResolveContactsAfterPose();
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） KnifePoseSampler.Dispose()、LeonAnimatorDriver.SetKnifeLocalHitStop(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateReleasedThrustPose(bool pickup)
    {
        var lower = knifeModule.configuration.Find(GameArchitecture.KnifeAction.LowerGuard);
        const float duration = .24f;
        releasedThrustElapsed += Time.deltaTime;
        if (lower == null || lower.sourceClip == null || releasedThrustElapsed >= duration)
        {
            releasedThrustPose = false;
            knifePoseExiting = false;
            knifePoseSampler.ReleasePresentation();
            return;
        }

        float progress = Mathf.Clamp01(releasedThrustElapsed / duration);
        float sampleTime = lower.sourceStart + lower.timing.duration * progress;
        float weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.6f, 1f, progress));
        knifePoseSampler.Sample(lower.sourceClip, sampleTime, 0, 0f);
        knifePoseSampler.Apply(Time.deltaTime, 1f, weight, pickup);
    }

    private void OnDisable()
    {
        SetLegacyParryHitStop(false);
        releasedThrustPose = false;
        knifePoseActive = false;
        knifePoseExiting = false;
        SetKnifeLocalHitStop(false);
        if (knifePoseSampler != null)
        {
            knifePoseSampler.Dispose();
            knifePoseSampler = null;
        }
    }

    /// <summary>BeginFullBodyAction 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int)、LeonAnimatorDriver.ShouldBlendFullBodyLayer(FullBodyActionType) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceMeleeAction(FullBodyActionType)、LeonAnimationActionCoordinator.AdvanceQTEPhase(int, FullBodyActionType)、LeonAnimationActionCoordinator.RequestAutomaticDrop()、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)、LeonAnimationActionCoordinator.RequestQTE(int) 等共 7 处；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.PrepareFullBodyLayerEntry(int)、LeonAnimatorDriver.ShouldBlendFullBodyLayer(FullBodyActionType) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 Animator.SetInteger(int, int) 的参数、作为 LeonAnimatorDriver.ShouldBlendFullBodyLayer(FullBodyActionType) 的参数、作为 LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int) 的参数 等共 4 处。</param>
    public bool BeginFullBodyAction(FullBodyActionType actionType, float normalizedStart = 0f)
    {
        if (animator == null || !IsValid)
        {
            return false;
        }

        if (actionType == FullBodyActionType.None || actionType == FullBodyActionType.Death)
        {
            return false;
        }

        if (actionType == FullBodyActionType.VillageGateOpen && !animator.HasState(LayerFullBodyAction, HashFullBodyGateOpen))
        {
            return false;
        }

        animator.SetInteger(HashFullBodyAction, (int)actionType);
        activeFullBodyAction = actionType;
        clearFullBodyActionAfterFade = false;
        CancelPendingFullBodyLayerEntry();
        blendingQTEFullBodyExit = false;
        if (actionType == FullBodyActionType.KnifeQuickAttack || actionType == FullBodyActionType.KnifeParry)
        {
            activeMeleeOnWeaponLayer = actionType;
            BlendWeaponActionLayerTo(1f);
            blendFullBodyLayerWeight = false;
            blendingFullBodyLayerEntry = false;
            fullBodyLayerWeight = 0f;
            targetFullBodyLayerWeight = 0f;
            animator.SetLayerWeight(LayerFullBodyAction, 0f);
            return true;
        }

        if (activeMeleeOnWeaponLayer != FullBodyActionType.None)
        {
            activeMeleeOnWeaponLayer = FullBodyActionType.None;
            BlendWeaponActionLayerTo(0f);
        }

        if (actionType == FullBodyActionType.ItemPickup)
        {
            PrepareFullBodyLayerEntry(pickupStateHash, 0f);
            return true;
        }

        if (TryGetPreparedFullBodyStateHash(actionType, out int /* 中文含义：状态哈希编号。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stateHash))
        {
            if (actionType == FullBodyActionType.WindowClimbThrough)
            {
                if (useHighWindowClimb)
                {
                    stateHash = HashFullBodyHighWindowClimb;
                }
                else if (useFenceVault)
                {
                    stateHash = HashFullBodyFenceVault;
                }
                useHighWindowClimb = false;
                useFenceVault = false;
            }

            PrepareFullBodyLayerEntry(stateHash, normalizedStart);
            return true;
        }

        if (ShouldBlendFullBodyLayer(actionType))
        {
            fullBodyLayerWeight = Mathf.Max(animator.GetLayerWeight(LayerFullBodyAction), 0.01f);
            targetFullBodyLayerWeight = 1f;
            blendFullBodyLayerWeight = true;
            blendingFullBodyLayerEntry = true;
            animator.SetLayerWeight(LayerFullBodyAction, fullBodyLayerWeight);
        }
        else
        {
            blendFullBodyLayerWeight = false;
            blendingFullBodyLayerEntry = false;
            fullBodyLayerWeight = 1f;
            targetFullBodyLayerWeight = 1f;
            animator.SetLayerWeight(LayerFullBodyAction, 1f);
        }

        return true;
    }

    /// <summary>
    /// 在同一个全身事务内无缝推进到下一个窗口动作。
    /// 保持全身层满权重，并用固定时长交叉淡化避免骨架突变。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AdvanceTraversalAction(FullBodyActionType, float, float)；下游（含内部回调体） Animator.CrossFadeInFixedTime(int, float, int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)、Mathf.Clamp01(float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：作为 Animator.SetInteger(int, int) 的参数、作为 LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int) 的参数、LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)。</param>
    /// <param name="transitionDuration">中文含义：过渡时长。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="normalizedTime">中文含义：动画归一化播放时间。使用位置：作为 Mathf.Clamp01(float) 的参数。</param>
    public bool AdvanceFullBodyAction(FullBodyActionType actionType, float transitionDuration, float normalizedTime = 0f)
    {
        if (animator == null || !IsValid)
        {
            return false;
        }

        if (!TryGetPreparedFullBodyStateHash(actionType, out int /* 中文含义：状态哈希编号。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ stateHash))
        {
            return false;
        }

        animator.SetInteger(HashFullBodyAction, (int)actionType);
        activeFullBodyAction = actionType;
        clearFullBodyActionAfterFade = false;
        activeMeleeOnWeaponLayer = FullBodyActionType.None;
        CancelPendingFullBodyLayerEntry();
        blendFullBodyLayerWeight = false;
        blendingFullBodyLayerEntry = false;
        blendingQTEFullBodyExit = false;
        fullBodyLayerWeight = 1f;
        targetFullBodyLayerWeight = 1f;
        animator.SetLayerWeight(LayerFullBodyAction, 1f);
        float offset = Mathf.Clamp01(normalizedTime);
        if (actionType == FullBodyActionType.WindowDropLand && offset <= 0f)
        {
            offset = Mathf.Max(0f, LeonDropContact.RollContactTime - transitionDuration);
        }
        animator.CrossFadeInFixedTime(stateHash, Mathf.Max(0.01f, transitionDuration), LayerFullBodyAction, offset);
        return true;
    }

    /// <summary>
    /// 着地收尾与基础移动层做短暂权重交接。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyCompleted(Animator, FullBodyActionType, int)；下游（含内部回调体） LeonAnimatorDriver.BeginQTEFullBodyExitBlend()、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.ClearFullBodyActionAfterFadeIfReady()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private readonly System.Collections.Generic.List<AnimatorClipInfo> landingClips = new();

    internal bool TryReadLandingRollTime(out float time)
    {
        time = 0f;
        if (animator == null || activeFullBodyAction != FullBodyActionType.WindowDropLand)
        {
            return false;
        }

        var state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        landingClips.Clear();
        if (animator.IsInTransition(LayerFullBodyAction))
        {
            var next = animator.GetNextAnimatorStateInfo(LayerFullBodyAction);
            if (next.shortNameHash == HashFullBodyWindowDropLand)
            {
                state = next;
                animator.GetNextAnimatorClipInfo(LayerFullBodyAction, landingClips);
            }
        }
        if (landingClips.Count == 0)
        {
            animator.GetCurrentAnimatorClipInfo(LayerFullBodyAction, landingClips);
        }

        if (state.shortNameHash != HashFullBodyWindowDropLand)
        {
            return false;
        }

        foreach (var info in landingClips)
        {
            if (info.clip != null && (info.clip.name == LeonDropContact.RollLandingClipName || info.clip.name == "LeonTraverse_0374"))
            {
                time = state.normalizedTime * info.clip.length;
                return true;
            }
        }
        return false;
    }

    public void EndTraversalLanding()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        CancelPendingFullBodyLayerEntry();
        clearFullBodyActionAfterFade = true;
        BeginQTEFullBodyExitBlend();
        ClearFullBodyActionAfterFadeIfReady();
    }

    /// <summary>
    /// 请求全身动作状态返回空状态。
    /// 快速反应动作从请求退出的同一帧开始淡出，避免空状态在满权重下压住基础动作。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CancelQTE(int)、LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.CompleteMeleeAction()、LeonAnimationActionCoordinator.CompleteQTE(int)、LeonAnimationActionCoordinator.ForceFinishQTE(int) 等共 10 处；下游（含内部回调体） Animator.GetLayerWeight(int)、Animator.SetInteger(int, int)、LeonAnimatorDriver.BeginQTEFullBodyExitBlend()、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.ClearFullBodyActionAfterFadeIfReady()、LeonAnimatorDriver.IsQTEAction(FullBodyActionType) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndFullBodyAction()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        CancelPendingFullBodyLayerEntry();
        // qteAction（是否配对动作动作）：保存 LeonAnimatorDriver.IsQTEAction(FullBodyActionType) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.EndFullBodyAction()。
bool qteAction = IsQTEAction(activeFullBodyAction);
        // fullBodyLayerVisible（全身图层是否可见）：使用位置：LeonAnimatorDriver.EndFullBodyAction()。
bool fullBodyLayerVisible = animator.GetLayerWeight(LayerFullBodyAction) > 0.01f;
        if (activeMeleeOnWeaponLayer != FullBodyActionType.None)
        {
            clearFullBodyActionAfterFade = false;
            animator.SetInteger(HashFullBodyAction, 0);
            BlendWeaponActionLayerTo(0f);
            return;
        }

        if ((qteAction || MeleeActionKinds.IsWoodenBox(activeFullBodyAction) || MeleeActionKinds.IsEnemyKick(activeFullBodyAction)) && fullBodyLayerVisible)
        {
            // 先淡出当前姿态，再清空状态，避免同时向空状态和底层混合。
            clearFullBodyActionAfterFade = true;
            BeginQTEFullBodyExitBlend();
            ClearFullBodyActionAfterFadeIfReady();
            return;
        }

        clearFullBodyActionAfterFade = false;
        animator.SetInteger(HashFullBodyAction, 0);
        if (ShouldBlendFullBodyLayer(activeFullBodyAction))
        {
            targetFullBodyLayerWeight = 0f;
            blendFullBodyLayerWeight = true;
            blendingFullBodyLayerEntry = false;
            blendingQTEFullBodyExit = false;
        }
        else if (qteAction)
        {
            BeginQTEFullBodyExitBlend();
        }
    }

    /// <summary>
    /// 全身动作状态完全退出后才允许降低第4层权重。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.CancelQTE(int)、LeonAnimationActionCoordinator.CancelTraversal()、LeonAnimationActionCoordinator.ForceFinishQTE(int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.OnDisable() 等共 6 处；下游（含内部回调体） Animator.SetLayerWeight(int, float)、LeonAnimatorDriver.BeginQTEFullBodyExitBlend()、LeonAnimatorDriver.BlendWeaponActionLayerTo(float)、LeonAnimatorDriver.CancelPendingFullBodyLayerEntry()、LeonAnimatorDriver.IsQTEAction(FullBodyActionType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void FinalizeFullBodyActionLayer()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        CancelPendingFullBodyLayerEntry();
        if (activeMeleeOnWeaponLayer != FullBodyActionType.None)
        {
            activeMeleeOnWeaponLayer = FullBodyActionType.None;
            BlendWeaponActionLayerTo(0f);
            blendFullBodyLayerWeight = false;
            blendingFullBodyLayerEntry = false;
            fullBodyLayerWeight = 0f;
            targetFullBodyLayerWeight = 0f;
            activeFullBodyAction = FullBodyActionType.None;
            animator.SetLayerWeight(LayerFullBodyAction, 0f);
            return;
        }

        // shouldSmoothQTEExit（是否应当平滑配对动作退出）：使用位置：LeonAnimatorDriver.FinalizeFullBodyActionLayer()。
        bool shouldSmoothQTEExit = IsQTEAction(activeFullBodyAction) || activeFullBodyAction == FullBodyActionType.WindowDropLand ||
            activeFullBodyAction == FullBodyActionType.WindowClimbThrough || activeFullBodyAction == FullBodyActionType.VillageGateOpen ||
            activeFullBodyAction == FullBodyActionType.ItemPickup;
        activeFullBodyAction = FullBodyActionType.None;
        if (shouldSmoothQTEExit)
        {
            // EndFullBodyAction 已经开始淡出时延续当前进度，不在状态退出帧重新起步。
            BeginQTEFullBodyExitBlend();
            return;
        }

        blendFullBodyLayerWeight = false;
        blendingFullBodyLayerEntry = false;
        blendingQTEFullBodyExit = false;
        fullBodyLayerWeight = 0f;
        targetFullBodyLayerWeight = 0f;
        animator.SetLayerWeight(LayerFullBodyAction, 0f);
    }

    /// <summary>BeginQTEFullBodyExitBlend 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.EndTraversalLanding()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()；下游（含内部回调体） Animator.GetLayerWeight(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BeginQTEFullBodyExitBlend()
    {
        fullBodyLayerWeight = animator.GetLayerWeight(LayerFullBodyAction);
        targetFullBodyLayerWeight = 0f;
        blendFullBodyLayerWeight = fullBodyLayerWeight > 0f;
        blendingFullBodyLayerEntry = false;
        blendingQTEFullBodyExit = blendFullBodyLayerWeight;
    }

    /// <summary>TryGetPreparedFullBodyStateHash 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.AdvanceFullBodyAction(FullBodyActionType, float, float)、LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)。</param>
    /// <param name="stateHash">中文含义：状态哈希编号。输出参数，由本入口写入并交给调用方。使用位置：LeonAnimatorDriver.TryGetPreparedFullBodyStateHash(FullBodyActionType, out int)。</param>
    private static bool TryGetPreparedFullBodyStateHash(FullBodyActionType actionType, out int stateHash)
    {
        switch (actionType)
        {
            case FullBodyActionType.Hit:
                stateHash = HashFullBodyHit;
                return true;
            case FullBodyActionType.QTEEnter:
                stateHash = HashFullBodyQTEEnter;
                return true;
            case FullBodyActionType.QTERightCutKick:
                stateHash = HashFullBodyRightCutKick;
                return true;
            case FullBodyActionType.QTESpinningHookKick:
                stateHash = HashFullBodySpinningHookKick;
                return true;
            case FullBodyActionType.WoodenBoxKick:
                stateHash = HashFullBodyWoodenBoxKick;
                return true;
            case FullBodyActionType.WoodenBoxStomp:
            case FullBodyActionType.WoodenBoxElbow:
            case FullBodyActionType.WoodenBoxPistolElbow:
                stateHash = Animator.StringToHash("Native" + actionType);
                return true;
            case FullBodyActionType.WindowDropFall:
                stateHash = HashFullBodyWindowDropFall;
                return true;
            case FullBodyActionType.WindowDropLand:
                stateHash = HashFullBodyWindowDropLand;
                return true;
            case FullBodyActionType.WindowClimbThrough:
                stateHash = HashFullBodyWindowClimb;
                return true;
            case FullBodyActionType.VillageGateOpen:
                stateHash = HashFullBodyGateOpen;
                return true;
            default:
                stateHash = 0;
                return false;
        }
    }

    /// <summary>ShouldBlendFullBodyLayer 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.BeginFullBodyAction(FullBodyActionType)、LeonAnimatorDriver.EndFullBodyAction()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：LeonAnimatorDriver.ShouldBlendFullBodyLayer(FullBodyActionType)。</param>
    private static bool ShouldBlendFullBodyLayer(FullBodyActionType actionType)
    {
        return actionType == FullBodyActionType.KnifeTake || actionType == FullBodyActionType.KnifeHide || actionType == FullBodyActionType.QTERightCutKick || actionType == FullBodyActionType.QTESpinningHookKick
            || MeleeActionKinds.IsWoodenBox(actionType);
    }

    /// <summary>IsQTEAction 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimatorDriver.EndFullBodyAction()、LeonAnimatorDriver.FinalizeFullBodyActionLayer()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：LeonAnimatorDriver.IsQTEAction(FullBodyActionType)。</param>
    private static bool IsQTEAction(FullBodyActionType actionType)
    {
        return actionType == FullBodyActionType.QTEEnter || actionType == FullBodyActionType.QTEHold || actionType == FullBodyActionType.QTESuccess || actionType == FullBodyActionType.QTEFail;
    }

    /// <summary>EnterDeath 的业务处理入口；本方法直接依赖 Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetLayerWeight(int)、Animator.ResetTrigger(int)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestDeath()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.GetLayerWeight(int)、Animator.ResetTrigger(int)、Animator.SetBool(int, bool)、Animator.SetFloat(int, float)、Animator.SetInteger(int, int)、Animator.SetLayerWeight(int, float) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool IsDeathAnimationPlaying()
    {
        if (!IsValid || animator == null || !animator.isActiveAndEnabled)
        {
            return false;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        if (state.shortNameHash == HashFullBodyDeath)
        {
            return state.normalizedTime < 1f;
        }

        if (animator.IsInTransition(LayerFullBodyAction))
        {
            return animator.GetNextAnimatorStateInfo(LayerFullBodyAction).shortNameHash == HashFullBodyDeath;
        }

        return false;
    }

    public void EnterDeath()
    {
        if (animator == null || !IsValid)
        {
            return;
        }

        CancelPendingFullBodyLayerEntry();
        // 死亡优先级最高：清除待处理受伤切换并立即归零两个受伤层
        ResetInjuredLayerState();
        currentAiming = false;
        blendWeaponActionLayerWeight = false;
        weaponActionLayerWeight = 0f;
        targetWeaponActionLayerWeight = 0f;
        blendFullBodyLayerWeight = false;
        blendingFullBodyLayerEntry = false;
        blendingQTEFullBodyExit = false;
        traversalLandingBasePoseLocked = false;
        activeFullBodyAction = FullBodyActionType.Death;
        activeMeleeOnWeaponLayer = FullBodyActionType.None;
        animator.SetInteger(HashFullBodyAction, 2);
        // currentFullBodyState（当前全身动画状态）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.EnterDeath()。
AnimatorStateInfo currentFullBodyState = animator.GetCurrentAnimatorStateInfo(LayerFullBodyAction);
        // currentFullBodyWeight（当前全身权重）：保存 Animator.GetLayerWeight(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimatorDriver.EnterDeath()。
float currentFullBodyWeight = animator.GetLayerWeight(LayerFullBodyAction);
        if (currentFullBodyWeight > 0.01f && currentFullBodyState.shortNameHash != HashFullBodyEmpty)
        {
            // 覆盖层已有有效姿态时保留当前输出，让控制器自然混入死亡。
            fullBodyLayerWeight = currentFullBodyWeight;
            targetFullBodyLayerWeight = 1f;
            blendFullBodyLayerWeight = !Mathf.Approximately(fullBodyLayerWeight, targetFullBodyLayerWeight);
            blendingFullBodyLayerEntry = blendFullBodyLayerWeight;
            blendingQTEFullBodyExit = false;
        }
        else
        {
            // 从空覆盖层死亡时，先在零权重下进入并求值死亡状态。
            PrepareFullBodyLayerEntry(HashFullBodyDeath);
        }

        animator.SetFloat(HashAim, 0f);
        animator.SetLayerWeight(LayerAim, 0f);
        animator.SetInteger(HashWeaponAction, 0);
        animator.SetLayerWeight(LayerWeaponAction, 0f);
        animator.SetBool(HashSprint, false);
        isCrouching = false;
        longGunCrouchLayerExitPending = false;
        animator.SetBool(HashCrouch, false);
        BlendLongGunCrouchLayerTo(0f);
        animator.ResetTrigger(HashFire);
    }

    /// <summary>ValidateAnimatorContract 的业务处理入口；本方法直接依赖 Animator.GetLayerName(int)、Animator.HasState(int, int)、Debug.Log(object)、Debug.LogError(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ValidateDriver()、LeonAnimatorDriver.CanWrite()、LeonAnimatorDriver.ContextMenuValidate()、LeonAnimatorDriver.InitializeAnimatorContract(WeaponType)、LeonAnimatorDriver.Start() 等共 6 处；下游（含内部回调体） Animator.GetLayerName(int)、Animator.HasState(int, int)、Debug.Log(object)、Debug.LogError(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool ValidateAnimatorContract()
    {
        if (validationPerformed)
        {
            return validationResult;
        }

        validationPerformed = true;
        if (animator == null)
        {
            Debug.LogError("[LeonAnimatorDriver] Animator 未赋值");
            return validationResult = false;
        }

        if (animator.layerCount < 8)
        {
            Debug.LogError("[LeonAnimatorDriver] 图层数量不足: " + animator.layerCount);
            return validationResult = false;
        }

        // expectedLayers（预期图层集合）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
string[] expectedLayers =
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
        // i（当前遍历索引）：使用位置：作为 Animator.GetLayerName(int) 的参数、作为 Debug.LogError(object) 的参数、LeonAnimatorDriver.ValidateAnimatorContract()。
for (int i = 0; i < expectedLayers.Length; i++)
        {
            if (animator.GetLayerName(i) != expectedLayers[i])
            {
                Debug.LogError("[LeonAnimatorDriver] 图层" + i + "名称不匹配: " + animator.GetLayerName(i));
                return validationResult = false;
            }
        }

        // required（所需）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
var required = new (string /* 中文含义：动画参数名称。 */ name, AnimatorControllerParameterType /* 中文含义：动画参数值的类型。 */ type)[]
        {
            ("Horizontal", AnimatorControllerParameterType.Float),
            ("Vertical", AnimatorControllerParameterType.Float),
            ("InputMagnitude", AnimatorControllerParameterType.Float),
            ("Sprint", AnimatorControllerParameterType.Bool),
            ("Aim", AnimatorControllerParameterType.Float),
            ("HorAimAngle", AnimatorControllerParameterType.Float),
            ("VerAimAngle", AnimatorControllerParameterType.Float),
            ("WeaponType", AnimatorControllerParameterType.Float),
            ("Fire", AnimatorControllerParameterType.Trigger),
            ("WeaponAction", AnimatorControllerParameterType.Int),
            ("FullBodyAction", AnimatorControllerParameterType.Int),
            ("HitDir", AnimatorControllerParameterType.Float),
            ("KnifeMode", AnimatorControllerParameterType.Float),
            ("TurnAngle", AnimatorControllerParameterType.Float),
            ("Crouch", AnimatorControllerParameterType.Bool),
            ("IKSourceRight", AnimatorControllerParameterType.Float),
            ("IKSourceLeft", AnimatorControllerParameterType.Float),
            ("IKTargetRight", AnimatorControllerParameterType.Float),
            ("IKTargetLeft", AnimatorControllerParameterType.Float),
        };
        // ps（粒子系统）：使用位置：作为 Debug.LogError(object) 的参数、LeonAnimatorDriver.ValidateAnimatorContract()。
var ps = animator.parameters;
        int expectedParameterCount = required.Length;
        if (HasNativeW870SwitchStates)
        {
            expectedParameterCount++;
            if (!System.Array.Exists(ps, parameter => parameter.nameHash == HolsterSourceWeaponHash
                && parameter.type == AnimatorControllerParameterType.Float))
            {
                Debug.LogError("[LeonAnimatorDriver] 参数缺失或类型错误: " + HolsterSourceWeapon);
                return validationResult = false;
            }
        }
        if (animator.HasState(0, BasicRunLoopStates[0]))
        {
            expectedParameterCount++;
            if (!System.Array.Exists(ps, parameter => parameter.nameHash == BasicRunLeanHash
                && parameter.type == AnimatorControllerParameterType.Float))
            {
                Debug.LogError("[LeonAnimatorDriver] 参数缺失或类型错误: " + BasicRunLeanParameter);
                return validationResult = false;
            }
        }
        if (ps.Length != expectedParameterCount)
        {
            Debug.LogError("[LeonAnimatorDriver] 参数总数不匹配: " + ps.Length + " (期望" + expectedParameterCount + ")");
            return validationResult = false;
        }

        // ok（是否成功）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
bool ok = true;
        foreach (var(/* 中文含义：名称。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ name, /* 中文含义：当前动画控制器控制器参数类型。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ type)in required)
        {
            // found（是否已找到）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
bool found = false;
            // p（当前动画控制器控制器参数）：每轮从粒子系统取得一个元素，仅代表当前这一项。使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
foreach (var p in ps)
            {
                if (p.name == name && p.type == type)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogError("[LeonAnimatorDriver] 参数缺失或类型错误: " + name);
                ok = false;
            }
        }

        // forbidden（禁止）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
string[] forbidden =
        {
            "RotateAngle",
            "WeaponTypeF",
            "isRightLegUp",
            "PistolRightIKWeight",
            "PistolLeftIKWeight",
            "RifleRightIKWeight",
            "RifleLeftIKWeight"
        };
        // name（名称）：每轮从禁止取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogError(object) 的参数、LeonAnimatorDriver.ValidateAnimatorContract()。
foreach (var name in forbidden)
        {
            // p（当前动画控制器控制器参数）：每轮从粒子系统取得一个元素，仅代表当前这一项。使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
foreach (var p in ps)
            {
                if (p.name == name)
                {
                    Debug.LogError("[LeonAnimatorDriver] 存在禁止参数: " + name);
                    ok = false;
                    break;
                }
            }
        }

        // requiredFullBodyStates（所需全身状态集合）：使用位置：LeonAnimatorDriver.ValidateAnimatorContract()。
var requiredFullBodyStates = new (string /* 中文含义：动画参数名称。 */ name, int /* 中文含义：动画参数名称对应的哈希编号。 */ hash)[]
        {
            ("Hit", HashFullBodyHit),
            ("Death", HashFullBodyDeath),
            ("QTEEnter", HashFullBodyQTEEnter),
            ("QTERightCutKick", HashFullBodyRightCutKick),
            ("QTESpinningHookKick", HashFullBodySpinningHookKick),
            ("WindowClimbThrough", HashFullBodyWindowClimb),
            ("WindowDropFall", HashFullBodyWindowDropFall),
            ("WindowDropLand", HashFullBodyWindowDropLand)
        };
        foreach (var(/* 中文含义：名称。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ name, /* 中文含义：哈希编号。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hash)in requiredFullBodyStates)
        {
            if (animator.HasState(LayerFullBodyAction, hash))
            {
                continue;
            }

            Debug.LogError("[LeonAnimatorDriver] 全身动作状态缺失: " + name);
            ok = false;
        }

        if (ok)
        {
            Debug.Log("[LeonAnimatorDriver] 契约验证通过");
        }

        return validationResult = ok;
    }

    /// <summary>ContextMenuValidate 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Debug.LogError(object)、LeonAnimatorDriver.ValidateAnimatorContract()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Debug.LogError(object)、LeonAnimatorDriver.ValidateAnimatorContract()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("验证动画器契约")]
    private void ContextMenuValidate()
    {
        validationPerformed = false;
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator != null)
        {
            ValidateAnimatorContract();
        }
        else
        {
            Debug.LogError("[LeonAnimatorDriver] 无法找到 Animator");
        }
    }
}
