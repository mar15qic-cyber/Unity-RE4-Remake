using UnityEngine;
using RootMotion.FinalIK;

/// <summary>
/// 武器IK阶段
/// </summary>
public enum WeaponIKPhase
{
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None,
    /// <summary>中文含义：稳定。</summary>
    Stable,
    /// <summary>中文含义：换弹。</summary>
    Reload,
    /// <summary>中文含义：收起。</summary>
    Holster,
    /// <summary>中文含义：武器已提交。</summary>
    WeaponCommitted,
    /// <summary>中文含义：装备。</summary>
    Equip,
    /// <summary>中文含义：命中。</summary>
    Hit,
    /// <summary>中文含义：配对动作。</summary>
    QTE,
    /// <summary>中文含义：死亡。</summary>
    Dead
}

/// <summary>
/// LeonAnimRig FinalIK 版本。
/// 使用 LimbIK 组件，在 Update 中设置 IKPositionWeight 和 IKPosition。
/// 不依赖 PlayableGraph，不存在图冲突。
/// </summary>
public partial class LeonAnimRig : MonoBehaviour
{
    /// <summary>中文含义：手枪右侧反向动力学。使用位置：LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.PistolRightWeight.get 等共 5 处。</summary>
    [Header("LimbIK 引用")]
    public LimbIK pistolRightIK;
    /// <summary>中文含义：手枪左侧反向动力学。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.GetLeftIK(WeaponType) 等共 6 处。</summary>
    public LimbIK pistolLeftIK;
    /// <summary>中文含义：步枪右侧反向动力学。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.GetRightIK(WeaponType) 等共 7 处。</summary>
    public LimbIK rifleRightIK;
    /// <summary>中文含义：步枪左侧反向动力学。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 等共 8 处。</summary>
    public LimbIK rifleLeftIK;
    /// <summary>中文含义：霰弹枪右侧反向动力学。使用位置：LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.SetAllIKWeights(float) 等共 5 处。</summary>
    public LimbIK shotgunRightIK;
    /// <summary>中文含义：霰弹枪左侧反向动力学。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.GetLeftIK(WeaponType) 等共 6 处。</summary>
    public LimbIK shotgunLeftIK;
    /// <summary>中文含义：小刀右侧反向动力学。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.KnifeRightWeight.get、LeonAnimRig.SetAllIKWeights(float) 等共 5 处。</summary>
    public LimbIK knifeRightIK;
    /// <summary>中文含义：小刀左侧反向动力学。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数、作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.KnifeLeftWeight.get 等共 6 处。</summary>
    public LimbIK knifeLeftIK;
    /// <summary>中文含义：手枪右侧目标。使用位置：作为 LeonAnimRig.GetSafeRightHandWeight(float, Transform) 的参数、作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    [Header("外部 IK 目标")]
    public Transform pistolRightTarget;
    /// <summary>中文含义：手枪左侧目标。使用位置：作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    public Transform pistolLeftTarget;
    /// <summary>中文含义：手枪左侧提示。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数。</summary>
    public Transform pistolLeftHint;
    /// <summary>中文含义：步枪右侧目标。使用位置：作为 LeonAnimRig.GetSafeRightHandWeight(float, Transform) 的参数、作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    public Transform rifleRightTarget;
    /// <summary>中文含义：步枪左侧目标。使用位置：作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    public Transform rifleLeftTarget;
    /// <summary>中文含义：步枪左侧提示。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数。</summary>
    public Transform rifleLeftHint;
    /// <summary>中文含义：霰弹枪右侧目标。使用位置：作为 LeonAnimRig.GetSafeRightHandWeight(float, Transform) 的参数、作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    public Transform shotgunRightTarget;
    /// <summary>中文含义：霰弹枪左侧目标。使用位置：作为 LeonAnimRig.GetTargetPath(Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</summary>
    public Transform shotgunLeftTarget;
    /// <summary>中文含义：霰弹枪左侧提示。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数。</summary>
    public Transform shotgunLeftHint;
    /// <summary>中文含义：小刀右侧目标。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    public Transform knifeRightTarget;
    /// <summary>中文含义：小刀左侧目标。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    public Transform knifeLeftTarget;
    /// <summary>中文含义：小刀左侧提示。使用位置：作为 LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    public Transform knifeLeftHint;
    /// <summary>中文含义：手枪待机左手权重。使用位置：LeonAnimRig.Update()。</summary>
    [Header("手枪待机左手握持")]
    [Range(0f, 1f)]
    [UnityEngine.Serialization.FormerlySerializedAs("手枪待机左手权重")]
    [UnityEngine.InspectorName("手枪待机左手权重")]
    public float pistolIdleLeftHandWeight = 1f;
    /// <summary>中文含义：手枪左手过渡时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Range(0.05f, 0.5f)]
    [UnityEngine.Serialization.FormerlySerializedAs("手枪左手过渡时间")]
    [UnityEngine.InspectorName("手枪左手过渡时间")]
    public float pistolLeftHandTransitionTime = 0.18f;
    /// <summary>中文含义：小刀右手权重。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    [Header("小刀姿态约束")]
    [Range(0f, 1f)]
    public float knifeRightHandWeight = 0.68f;
    /// <summary>中文含义：小刀左手权重。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    [Range(0f, 1f)]
    public float knifeLeftHandWeight = 0.06f;
    /// <summary>中文含义：小刀反向动力学混合时长。使用位置：作为 Mathf.Max(float, float) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。</summary>
    [Range(0.08f, 0.6f)]
    public float knifeIKBlendDuration = 0.24f;
    /// <summary>中文含义：掏刀动作到达此比例后，才让手臂约束逐步统一到持刀姿态。使用位置：LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float)。</summary>
    [Tooltip("掏刀动作到达此比例后，才让手臂约束逐步统一到持刀姿态")]
    [Range(0.2f, 0.9f)]
    public float knifeEquipIKStartNormalizedTime = 0.48f;
    /// <summary>中文含义：瞄准距离。</summary>
    [Header("瞄准参数")]
    public float aimDistance = 10f;
    /// <summary>中文含义：掏枪或换弹状态退出后，约束恢复到稳定权重所需的时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("动作结束过渡")]
    [Tooltip("掏枪或换弹状态退出后，约束恢复到稳定权重所需的时间")]
    [Range(0.05f, 0.5f)]
    public float stableIKBlendDuration = 0.18f;
    /// <summary>中文含义：武器提交后，原武器手臂约束平滑退出所需时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("切枪约束过渡")]
    [Tooltip("武器提交后，原武器手臂约束平滑退出所需时间")]
    [Range(0.05f, 0.4f)]
    public float weaponCommitIKBlendDuration = 0.14f;
    /// <summary>中文含义：非瞄准蹲姿移动时，左手保持在霰弹枪护木上的约束权重。使用位置：LeonAnimRig.Update()。</summary>
    [Header("霰弹枪蹲姿握持")]
    [Tooltip("非瞄准蹲姿移动时，左手保持在霰弹枪护木上的约束权重")]
    [Range(0f, 1f)]
    public float shotgunCrouchLeftHandWeight = 0.82f;
    /// <summary>中文含义：霰弹枪蹲姿左手约束进入和退出的平滑时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("霰弹枪蹲姿左手约束进入和退出的平滑时间")]
    [Range(0.05f, 0.5f)]
    public float shotgunCrouchLeftHandBlendDuration = 0.2f;
    /// <summary>中文含义：是否仅当右手目标位于右手骨链外时，才允许主手约束参与解算。仅当右手目标位于右手骨链外时，才允许主手约束参与解算。使用位置：LeonAnimRig.GetSafeRightHandWeight(float, Transform)。</summary>
    [Header("主手约束保护")]
    [Tooltip("仅当右手目标位于右手骨链外时，才允许主手约束参与解算")]
    public bool allowExternalRightHandIK;
    /// <summary>中文含义：装备反向动力学淡入持续时间。EquipVisualReveal 标记后新武器约束的淡入时长。使用位置：LeonAnimRig.ApplyPhaseWeights()。</summary>
    [Header("Equip 拔枪反向动力学时机")]
    [Tooltip("EquipVisualReveal 标记后新武器约束的淡入时长")]
    [Range(0.04f, 0.5f)]
    public float equipIKFadeInDuration = 0.12f;
    /// <summary>中文含义：手枪装备视觉揭示标记未收到时，在此归一化时间触发兜底淡入。使用位置：作为 Debug.LogWarning(object) 的参数、LeonAnimRig.Update()。</summary>
    [Tooltip("手枪装备视觉揭示标记未收到时，在此归一化时间触发兜底淡入")]
    [Range(0.3f, 0.9f)]
    public float equipIKRevealFallbackNormalizedTime = 0.62f;
    /// <summary>中文含义：长枪副手握把标记后的反向动力学淡入时长。使用位置：LeonAnimRig.ApplyPhaseWeights()。</summary>
    [Tooltip("长枪副手握把标记后的反向动力学淡入时长")]
    [Range(0.04f, 0.5f)]
    public float longGunSupportHandFadeInDuration = 0.18f;
    /// <summary>中文含义：长枪支撑手部权重。长枪拔枪与低血量稳定姿态中，副手维持护木握持的最低约束权重。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("长枪拔枪与低血量稳定姿态中，副手维持护木握持的最低约束权重")]
    [Range(0f, 1f)]
    public float longGunSupportHandWeight = 0.90f;
    /// <summary>中文含义：里昂玩家控制器。使用位置：LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.ExitHitIK(WeaponType)、LeonAnimRig.InjuredSingleHandPoseSuppressionActive() 等共 6 处。</summary>
    private LeonController controller;
    /// <summary>中文含义：里昂动画控制器驱动器。使用位置：LeonAnimRig.ExitQTEIK(WeaponType)、LeonAnimRig.RestoreQTEWeaponTypeAfterFullBodyExit()、LeonAnimRig.Start()。</summary>
    private LeonAnimatorDriver driver;
    private LeonAnimationActionCoordinator actionCoordinator;
    /// <summary>中文含义：动画控制器。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonAnimRig.GetSafeRightHandWeight(float, Transform)、LeonAnimRig.IsFullBodyActionLayerInfluencingPose() 等共 8 处。</summary>
    private Animator anim;
    /// <summary>中文含义：目标手臂权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.EnterDeathIK()、LeonAnimRig.EnterHitIK() 等共 8 处。</summary>
    private float targetArmWeight;
    /// <summary>中文含义：当前手臂权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.EnterDeathIK() 等共 8 处。</summary>
    private float currentArmWeight;
    /// <summary>中文含义：装备曲线右侧权重。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float equipCurveRightWeight;
    /// <summary>中文含义：装备曲线左侧权重。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float equipCurveLeftWeight;
    /// <summary>中文含义：是否装备反向动力学显露已接收。使用位置：LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 7 处。</summary>
    private bool equipIKRevealReceived;
    /// <summary>中文含义：是否装备反向动力学显露后备已警告。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonAnimRig.ResetActionBlendState() 等共 4 处。</summary>
    private bool equipIKRevealFallbackWarned;
    /// <summary>中文含义：是否长枪支撑手部握持已接收。使用位置：LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 6 处。</summary>
    private bool longGunSupportHandGripReceived;
    private float rifleCompatibilityIkWeight = 1f;
    internal float RifleCompatibilityIkWeight => rifleCompatibilityIkWeight;
    internal float ShotgunCompatibilityIkWeight
    {
        get
        {
            if (driver == null)
            {
                return 1f;
            }
            if (driver.SupportsNativeW870Aim && driver.SupportsNativeLongGunCrouch)
            {
                return 0f;
            }
            float weight = 1f;
            if (driver.NativeW870OwnsAimArms && anim != null)
            {
                weight -= anim.GetLayerWeight(LeonAnimatorDriver.LayerRifleCrouch);
            }
            if (driver.NativeW870ReloadActive || driver.IsNativeW870SwitchPlaying())
            {
                weight *= 1f - driver.WeaponActionLayerWeight;
            }
            return weight;
        }
    }
    /// <summary>中文含义：装备反向动力学淡入淡出起始右侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 5 处。</summary>
    private float equipIKFadeStartRightWeight;
    /// <summary>中文含义：装备反向动力学淡入淡出起始左侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 5 处。</summary>
    private float equipIKFadeStartLeftWeight;
    /// <summary>中文含义：装备反向动力学淡入淡出已用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimRig.ApplyPhaseWeights() 等共 7 处。</summary>
    private float equipIKFadeElapsed;
    /// <summary>中文含义：稳定混合开始时刻右侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float stableBlendStartRightWeight;
    /// <summary>中文含义：稳定混合开始时刻左侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float stableBlendStartLeftWeight;
    /// <summary>中文含义：稳定混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType) 等共 5 处。</summary>
    private float stableBlendElapsed;
    /// <summary>中文含义：稳定混合是否生效。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.BeginReloadIK(WeaponType)、LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType) 等共 8 处。</summary>
    private bool stableBlendActive;
    /// <summary>中文含义：已提交来源武器。使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、作为 LeonAnimRig.GetLeftIK(WeaponType) 的参数、作为 LeonAnimRig.GetRightIK(WeaponType) 的参数 等共 4 处。</summary>
    private WeaponType committedSourceWeapon = WeaponType.Pistol;
    /// <summary>中文含义：已提交来源右侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float committedSourceRightWeight;
    /// <summary>中文含义：已提交来源左侧权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonAnimRig.ResetActionBlendState()。</summary>
    private float committedSourceLeftWeight;
    /// <summary>中文含义：已提交混合已用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimRig.ApplyPhaseWeights() 等共 5 处。</summary>
    private float committedBlendElapsed;
    /// <summary>中文含义：霰弹枪蹲伏左侧权重。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.Update()。</summary>
    private float shotgunCrouchLeftWeight;
    /// <summary>中文含义：霰弹枪蹲伏左侧速度。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数。</summary>
    private float shotgunCrouchLeftVelocity;
    /// <summary>中文含义：手枪左手当前权重。使用位置：作为 Mathf.Max(float, float) 的参数、LeonAnimRig.OnDisable()、LeonAnimRig.Update()。</summary>
    private float pistolLeftHandCurrentWeight;
    /// <summary>中文含义：手枪左手权重速度。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.OnDisable()。</summary>
    private float pistolLeftHandWeightSpeed;
    /// <summary>中文含义：小刀姿态权重。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.CommitWeaponSwitchIK(WeaponType) 等共 6 处。</summary>
    private float knifePoseWeight;
    /// <summary>中文含义：小刀姿态目标权重。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.BeginKnifeActionIK()、LeonAnimRig.BeginKnifeEquipIK() 等共 13 处。</summary>
    private float knifePoseTargetWeight;
    /// <summary>中文含义：小刀姿态速度。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.BeginKnifeActionIK()、LeonAnimRig.BeginKnifeEquipIK() 等共 8 处。</summary>
    private float knifePoseVelocity;
    /// <summary>中文含义：反向动力学来源右侧的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数。</summary>
    private static readonly int HashIKSourceRight = Animator.StringToHash("IKSourceRight");
    /// <summary>中文含义：反向动力学来源左侧的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数。</summary>
    private static readonly int HashIKSourceLeft = Animator.StringToHash("IKSourceLeft");
    /// <summary>中文含义：反向动力学目标右侧的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数。</summary>
    private static readonly int HashIKTargetRight = Animator.StringToHash("IKTargetRight");
    /// <summary>中文含义：反向动力学目标左侧的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetFloat(int) 的参数。</summary>
    private static readonly int HashIKTargetLeft = Animator.StringToHash("IKTargetLeft");
    /// <summary>中文含义：反向动力学阶段。使用位置：作为 Debug.Log(object) 的参数、LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.ApplyPhaseWeights() 等共 22 处。</summary>
    private WeaponIKPhase ikPhase = WeaponIKPhase.Stable;
    /// <summary>中文含义：当前武器。使用位置：作为 Debug.Log(object) 的参数、作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、作为 LeonAnimRig.IsLongGunWeapon(WeaponType) 的参数 等共 16 处。</summary>
    private WeaponType currentWeapon = WeaponType.Pistol;
    /// <summary>中文含义：来源武器。使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)。</summary>
    private WeaponType sourceWeapon = WeaponType.Pistol;
    /// <summary>中文含义：配对动作退出武器。使用位置：作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、作为 LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType) 的参数、LeonAnimRig.ExitQTEIK(WeaponType) 等共 4 处。</summary>
    private WeaponType qteExitWeapon = WeaponType.Pistol;
    /// <summary>中文含义：是否正在瞄准。使用位置：LeonAnimRig.EnterHitIK()、LeonAnimRig.EnterQTEIK()、LeonAnimRig.ExitHitIK(WeaponType) 等共 6 处。</summary>
    private bool isAiming;
    /// <summary>中文含义：是否已经死亡。使用位置：LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.BeginKnifeEquipIK() 等共 19 处。</summary>
    private bool isDead;
    /// <summary>中文含义：配对动作武器类型恢复是否仍待处理。使用位置：LeonAnimRig.EnterDeathIK()、LeonAnimRig.EnterQTEIK()、LeonAnimRig.ExitQTEIK(WeaponType) 等共 5 处。</summary>
    private bool qteWeaponTypeRestorePending;
    /// <summary>中文含义：配对动作武器类型恢复帧集合。使用位置：LeonAnimRig.EnterDeathIK()、LeonAnimRig.EnterQTEIK()、LeonAnimRig.ExitQTEIK(WeaponType) 等共 5 处。</summary>
    private int qteWeaponTypeRestoreFrames;
    /// <summary>中文含义：上一次阶段变化原因。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)、LeonAnimRig.BeginReloadIK(WeaponType)、LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType) 等共 13 处。</summary>
    private string lastPhaseChangeReason = "init";
    /// <summary>中文含义：rawSourceRight 表示“原始来源右侧”；rawSourceLeft 表示“原始来源左侧”；rawTargetRight 表示“原始目标右侧”；rawTargetLeft 表示“原始目标左侧”。使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、LeonAnimRig.RawSourceRight.get、LeonAnimRig.ReadIKCurves()。；使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、LeonAnimRig.RawSourceLeft.get、LeonAnimRig.ReadIKCurves()。；使用位置：LeonAnimRig.RawTargetRight.get、LeonAnimRig.ReadIKCurves()。；使用位置：LeonAnimRig.RawTargetLeft.get、LeonAnimRig.ReadIKCurves()。</summary>
    private float rawSourceRight, rawSourceLeft, rawTargetRight, rawTargetLeft;
    /// <summary>中文含义：上一次调试武器。使用位置：LeonAnimRig.ApplyPhaseWeights()。</summary>
    private WeaponType lastDebugWeapon = (WeaponType)(-1);
    /// <summary>中文含义：上一次调试阶段。使用位置：LeonAnimRig.ApplyPhaseWeights()。</summary>
    private WeaponIKPhase lastDebugPhase = (WeaponIKPhase)(-1);
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonController&gt;()、LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform)、LeonAnimRig.ResetActionBlendState()、LeonAnimRig.SetAllIKWeights(float)、LeonAnimRig.SyncIKPositionsToWrists()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        nativeKnifeAim = GetComponent<KnifeCombatModule>();
        PositionNativeCqbrOnBack();
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }

        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        actionCoordinator = GetComponent<LeonAnimationActionCoordinator>();
        if (driver == null)
        {
            driver = GetComponent<LeonAnimatorDriver>();
        }

        if (controller != null)
        {
            currentWeapon = controller.currentWeapon;
        }
        else
        {
            currentWeapon = WeaponType.Pistol;
        }

        if (currentWeapon == WeaponType.Knife)
        {
            knifePoseWeight = 1f;
        }
        else
        {
            knifePoseWeight = 0f;
        }

        knifePoseTargetWeight = knifePoseWeight;
        knifePoseVelocity = 0f;
        qteExitWeapon = currentWeapon;
        qteWeaponTypeRestorePending = false;
        qteWeaponTypeRestoreFrames = 0;
        ikPhase = WeaponIKPhase.Stable;
        lastPhaseChangeReason = "Start: Stable Pistol (FinalIK)";
        ResetActionBlendState();
        // 左手位置目标只约束手腕，弯肘方向必须由提示目标单独约束。
        // 否则换弹结束后会沿用动作末帧的弯曲平面，造成左手虽到达握把、手臂姿态却错误。
        ConfigureLeftArmBendGoal(pistolLeftIK, pistolLeftHint);
        ConfigureLeftArmBendGoal(rifleLeftIK, rifleLeftHint);
        ConfigureLeftArmBendGoal(shotgunLeftIK, shotgunLeftHint);
        ConfigureLeftArmBendGoal(knifeLeftIK, knifeLeftHint);
        // 初始化：IK 权重 = 0，IK 位置 = 手腕位置
        SyncIKPositionsToWrists();
        SetAllIKWeights(0f);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        RestoreNativeCqbrReloadMechanism();
        ResetActionBlendState();
        targetArmWeight = 0f;
        currentArmWeight = 0f;
        pistolLeftHandCurrentWeight = 0f;
        pistolLeftHandWeightSpeed = 0f;
        knifePoseTargetWeight = 0f;
        knifePoseVelocity = 0f;
        equipIKRevealReceived = false;
        longGunSupportHandGripReceived = false;
        stableBlendActive = false;
        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.None;
    }

    /// <summary>ConfigureLeftArmBendGoal 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ik">中文含义：反向动力学。使用位置：LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform)。</param>
    /// <param name="hint">中文含义：提示。使用位置：LeonAnimRig.ConfigureLeftArmBendGoal(LimbIK, Transform)。</param>
    private static void ConfigureLeftArmBendGoal(LimbIK ik, Transform hint)
    {
        if (ik == null || hint == null)
        {
            return;
        }

        ik.solver.bendModifier = IKSolverLimb.BendModifier.Goal;
        ik.solver.bendGoal = hint;
        ik.solver.bendModifierWeight = 1f;
    }

    /// <summary>SyncIKPositionsToWrists 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.Start()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SyncIKPositionsToWrists()
    {
        if (anim == null)
        {
            return;
        }

        // rHand（右侧手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonAnimRig.SyncIKPositionsToWrists()。
var rHand = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.RightHand);
        // lHand（左侧手部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonAnimRig.SyncIKPositionsToWrists()。
var lHand = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.LeftHand);
        // lFore（左侧前部）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。
var lFore = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.LeftLowerArm);
        if (pistolRightIK != null && rHand != null)
        {
            pistolRightIK.solver.IKPosition = rHand.position;
            pistolRightIK.solver.IKRotation = rHand.rotation;
        }

        if (pistolLeftIK != null && lHand != null)
        {
            pistolLeftIK.solver.IKPosition = lHand.position;
            pistolLeftIK.solver.IKRotation = lHand.rotation;
        }

        if (rifleRightIK != null && rHand != null)
        {
            rifleRightIK.solver.IKPosition = rHand.position;
            rifleRightIK.solver.IKRotation = rHand.rotation;
        }

        if (rifleLeftIK != null && lHand != null)
        {
            rifleLeftIK.solver.IKPosition = lHand.position;
            rifleLeftIK.solver.IKRotation = lHand.rotation;
        }

        if (shotgunRightIK != null && rHand != null)
        {
            shotgunRightIK.solver.IKPosition = rHand.position;
            shotgunRightIK.solver.IKRotation = rHand.rotation;
        }

        if (shotgunLeftIK != null && lHand != null)
        {
            shotgunLeftIK.solver.IKPosition = lHand.position;
            shotgunLeftIK.solver.IKRotation = lHand.rotation;
        }

        if (knifeRightIK != null && rHand != null)
        {
            knifeRightIK.solver.IKPosition = rHand.position;
            knifeRightIK.solver.IKRotation = rHand.rotation;
        }

        if (knifeLeftIK != null && lHand != null)
        {
            knifeLeftIK.solver.IKPosition = lHand.position;
            knifeLeftIK.solver.IKRotation = lHand.rotation;
        }
    }

    /// <summary>SetAllIKWeights 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.BeginKnifeActionIK()、LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="w">中文含义：所有反向动力学目标要使用的权重。使用位置：LeonAnimRig.SetAllIKWeights(float)。</param>
    private void SetAllIKWeights(float w)
    {
        if (pistolRightIK != null)
        {
            pistolRightIK.solver.IKPositionWeight = w;
            pistolRightIK.solver.IKRotationWeight = w;
        }

        if (pistolLeftIK != null)
        {
            pistolLeftIK.solver.IKPositionWeight = w;
            pistolLeftIK.solver.IKRotationWeight = w;
        }

        if (rifleRightIK != null)
        {
            rifleRightIK.solver.IKPositionWeight = w;
            rifleRightIK.solver.IKRotationWeight = w;
        }

        if (rifleLeftIK != null)
        {
            rifleLeftIK.solver.IKPositionWeight = w;
            rifleLeftIK.solver.IKRotationWeight = w;
        }

        if (shotgunRightIK != null)
        {
            shotgunRightIK.solver.IKPositionWeight = w;
            shotgunRightIK.solver.IKRotationWeight = w;
        }

        if (shotgunLeftIK != null)
        {
            shotgunLeftIK.solver.IKPositionWeight = w;
            shotgunLeftIK.solver.IKRotationWeight = w;
        }

        if (knifeRightIK != null)
        {
            knifeRightIK.solver.IKPositionWeight = w;
            knifeRightIK.solver.IKRotationWeight = w;
        }

        if (knifeLeftIK != null)
        {
            knifeLeftIK.solver.IKPositionWeight = w;
            knifeLeftIK.solver.IKRotationWeight = w;
        }
    }

    /// <summary>ForceAllWeaponIKOff 的业务处理入口；本方法直接依赖 LeonAnimRig.SetAllIKWeights(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.EnterDeathIK()、LeonAnimRig.EnterHitIK()、LeonAnimRig.EnterQTEIK()、LeonAnimRig.ForceIKOff()、LeonAnimRig.InterruptWeaponActionIK(WeaponType) 等共 7 处；下游（含内部回调体） LeonAnimRig.SetAllIKWeights(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceAllWeaponIKOff()
    {
        SetAllIKWeights(0f);
    }

    /// <summary>ForceIKOff 的业务处理入口；本方法直接依赖 LeonAnimRig.ForceAllWeaponIKOff()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.OnDisable()；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ForceIKOff()
    {
        targetArmWeight = 0f;
        currentArmWeight = 0f;
        knifePoseTargetWeight = 0f;
        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.None;
    }

    /// <summary>GetIKWeight 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 IKModeBehaviour.OnStateEnter(Animator, AnimatorStateInfo, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public float GetIKWeight()
    {
        return currentArmWeight;
    }

    /// <summary>SetWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonAnimRig.CompleteWeaponActionIK(WeaponType)、LeonAnimRig.ExitHitIK(WeaponType)、LeonAnimRig.ExitQTEIK(WeaponType)、LeonAnimRig.InterruptWeaponActionIK(WeaponType) 等共 10 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前武器类别。使用位置：LeonAnimRig.SetWeapon(WeaponType)。</param>
    public void SetWeapon(WeaponType type)
    {
        currentWeapon = type;
    }

    /// <summary>SetIKMode 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 IKModeBehaviour.OnStateEnter(Animator, AnimatorStateInfo, int)、IKModeBehaviour.OnStateExit(Animator, AnimatorStateInfo, int)、KnifeCombatModule.PrepareTransaction(bool)、LeonAnimRig.ExitHitIK(WeaponType)、LeonAnimRig.ExitQTEIK(WeaponType) 等共 8 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mode">中文含义：当前反向动力学模式。使用位置：LeonAnimRig.SetIKMode(IKMode)。</param>
    public void SetIKMode(IKMode mode)
    {
        if (mode == IKMode.Aim)
        {
            targetArmWeight = 1f;
            isAiming = true;
        }
        else if (mode == IKMode.None || mode == IKMode.Hit)
        {
            targetArmWeight = 0f;
            isAiming = false;
        }
        else
        {
            targetArmWeight = 0f;
            isAiming = false;
        }
    }

    // ================================================================
    // 武器IK阶段接口
    // ================================================================
    /// <summary>BeginReloadIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestReload(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.BeginReloadIK(WeaponType)。</param>
    public void BeginReloadIK(WeaponType weapon)
    {
        if (isDead)
        {
            return;
        }

        stableBlendActive = false;
        ikPhase = WeaponIKPhase.Reload;
        lastPhaseChangeReason = "BeginReloadIK: " + weapon;
    }

    /// <summary>BeginWeaponSwitchIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源武器类别。使用位置：LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType)。</param>
    /// <param name="target">中文含义：目标武器类别。使用位置：LeonAnimRig.BeginWeaponSwitchIK(WeaponType, WeaponType)。</param>
    public void BeginWeaponSwitchIK(WeaponType source, WeaponType target)
    {
        if (isDead)
        {
            return;
        }

        sourceWeapon = source;
        stableBlendActive = false;
        ikPhase = WeaponIKPhase.Holster;
        lastPhaseChangeReason = "BeginWeaponSwitchIK: " + source + " -> " + target;
    }

    /// <summary>BeginKnifeEquipIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginKnifeEquipIK()
    {
        if (isDead)
        {
            return;
        }

        // 前半段保留素材原有的掏刀轨迹，避免手腕目标过早拉扯肩肘。
        knifePoseTargetWeight = 0f;
        knifePoseVelocity = 0f;
    }

    /// <summary>UpdateKnifeTransitionIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleWeaponProgress(Animator, WeaponActionType, int, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="actionType">中文含义：动作类型。使用位置：LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float)。</param>
    /// <param name="normalizedTime">中文含义：动画归一化播放时间。使用位置：LeonAnimRig.UpdateKnifeTransitionIK(WeaponActionType, float)。</param>
    public void UpdateKnifeTransitionIK(WeaponActionType actionType, float normalizedTime)
    {
        if (isDead)
        {
            return;
        }

        if (actionType == WeaponActionType.KnifeTake && normalizedTime >= knifeEquipIKStartNormalizedTime)
        {
            knifePoseTargetWeight = 1f;
        }
        else if (actionType == WeaponActionType.KnifeHide)
        {
            knifePoseTargetWeight = 0f;
        }
    }

    /// <summary>BeginKnifeHideIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestWeaponSwitch(WeaponType, WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginKnifeHideIK()
    {
        knifePoseTargetWeight = 0f;
        knifePoseVelocity = 0f;
    }

    /// <summary>BeginKnifeActionIK 的业务处理入口；本方法直接依赖 LeonAnimRig.SetAllIKWeights(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestIndependentKnifeTransaction()、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)；下游（含内部回调体） LeonAnimRig.SetAllIKWeights(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginKnifeActionIK()
    {
        knifeActionSuppression = true;
        SetAllIKWeights(0f);
        knifePoseTargetWeight = 0f;
        knifePoseVelocity = 0f;
    }

    /// <summary>EndKnifeActionIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.EndIndependentKnifeTransaction(int)、LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)、LeonAnimationActionCoordinator.RequestMeleeAction(FullBodyActionType, int)、LeonAnimationActionCoordinator.TryInterruptMeleeForPriority(out FullBodyActionType, out int)、LeonCombat.ApplyGuardPose() 等共 7 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndKnifeActionIK()
    {
        knifeActionSuppression = false;
        if (currentWeapon == WeaponType.Knife && (driver == null || !driver.SupportsNativeKnifeCarry))
        {
            knifePoseTargetWeight = 1f;
        }
        else
        {
            knifePoseTargetWeight = 0f;
        }

        knifePoseVelocity = 0f;
    }

    /// <summary>CommitWeaponSwitchIK 的业务处理入口；本方法直接依赖 LeonAnimRig.GetLeftIK(WeaponType)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.GetSolverWeight(LimbIK)、LeonAnimRig.SetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)；下游（含内部回调体） LeonAnimRig.GetLeftIK(WeaponType)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.GetSolverWeight(LimbIK)、LeonAnimRig.SetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标武器类别。使用位置：作为 LeonAnimRig.SetWeapon(WeaponType) 的参数、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)。</param>
    public void CommitWeaponSwitchIK(WeaponType target)
    {
        if (isDead)
        {
            return;
        }

        // 提交时保存旧武器约束的真实权重，由 WeaponCommitted 阶段连续淡出。
        // 新武器约束保持为零，直到对应装备标记到达后再淡入。
        committedSourceWeapon = sourceWeapon;
        committedSourceRightWeight = GetSolverWeight(GetRightIK(committedSourceWeapon));
        committedSourceLeftWeight = GetSolverWeight(GetLeftIK(committedSourceWeapon));
        committedBlendElapsed = 0f;
        currentWeapon = target;
        knifePoseTargetWeight = 0f;
        knifePoseWeight = 0f;
        knifePoseVelocity = 0f;
        SetWeapon(target);
        equipIKRevealReceived = false;
        equipIKRevealFallbackWarned = false;
        longGunSupportHandGripReceived = false;
        equipIKFadeStartRightWeight = 0f;
        equipIKFadeStartLeftWeight = 0f;
        equipIKFadeElapsed = 0f;
        stableBlendActive = false;
        ikPhase = WeaponIKPhase.WeaponCommitted;
        lastPhaseChangeReason = "CommitWeaponSwitchIK: " + target;
    }

    /// <summary>
    /// Equip 拔枪视觉揭示标记：从此刻才开始新武器反向动力学淡入。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)、LeonAnimRig.Update()；下游（含内部回调体） LeonAnimRig.IsLongGunWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyEquipVisualRevealIK()
    {
        if (isDead || IsLongGunWeapon(currentWeapon))
        {
            return;
        }

        if (ikPhase != WeaponIKPhase.Equip && ikPhase != WeaponIKPhase.WeaponCommitted)
        {
            return;
        }

        if (equipIKRevealReceived)
        {
            return;
        }

        equipIKRevealReceived = true;
        equipIKFadeElapsed = 0f;
        equipIKFadeStartRightWeight = 0f;
        equipIKFadeStartLeftWeight = 0f;
    }

    /// <summary>
    /// 长枪副手握把标记：从副手真正到达护木后才开始长枪反向动力学淡入。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)；下游（含内部回调体） LeonAnimRig.IsLongGunWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void NotifyLongGunSupportHandGripIK()
    {
        if (isDead || !IsLongGunWeapon(currentWeapon))
        {
            return;
        }

        if (ikPhase != WeaponIKPhase.Equip && ikPhase != WeaponIKPhase.WeaponCommitted)
        {
            return;
        }

        if (longGunSupportHandGripReceived)
        {
            return;
        }

        longGunSupportHandGripReceived = true;
        equipIKFadeElapsed = 0f;
        equipIKFadeStartRightWeight = 0f;
        equipIKFadeStartLeftWeight = 0f;
    }

    /// <summary>BeginEquipIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleWeaponEntered(Animator, WeaponActionType, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标武器类别。使用位置：LeonAnimRig.BeginEquipIK(WeaponType)。</param>
    public void BeginEquipIK(WeaponType target)
    {
        if (isDead)
        {
            return;
        }

        // continuingCommittedSwitch（是否继续中已提交切换）：使用位置：LeonAnimRig.BeginEquipIK(WeaponType)。
bool continuingCommittedSwitch = ikPhase == WeaponIKPhase.WeaponCommitted && currentWeapon == target;
        currentWeapon = target;
        equipCurveRightWeight = 0f;
        equipCurveLeftWeight = 0f;
        if (!continuingCommittedSwitch)
        {
            equipIKRevealReceived = false;
            equipIKRevealFallbackWarned = false;
            longGunSupportHandGripReceived = false;
            equipIKFadeStartRightWeight = 0f;
            equipIKFadeStartLeftWeight = 0f;
            equipIKFadeElapsed = 0f;
        }

        stableBlendActive = false;
        ikPhase = WeaponIKPhase.Equip;
        lastPhaseChangeReason = "BeginEquipIK: " + target;
    }

    /// <summary>CompleteWeaponActionIK 的业务处理入口；本方法直接依赖 LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.HandleWeaponExited(Animator, WeaponActionType, int, bool)；下游（含内部回调体） LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType) 的参数、作为 LeonAnimRig.SetWeapon(WeaponType) 的参数、LeonAnimRig.CompleteWeaponActionIK(WeaponType)。</param>
    public void CompleteWeaponActionIK(WeaponType weapon)
    {
        if (isDead)
        {
            return;
        }

        currentWeapon = weapon;
        SetWeapon(weapon);
        BeginStableBlendFromCurrentPose(weapon);
        ikPhase = WeaponIKPhase.Stable;
        lastPhaseChangeReason = "CompleteWeaponActionIK: " + weapon;
    }

    /// <summary>InterruptWeaponActionIK 的业务处理入口；本方法直接依赖 LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()、LeonAnimRig.SetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.AsKnifeInterruptReload()、LeonAnimationActionCoordinator.ForceClearWeaponActionBlockAfterQTE(WeaponType)、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()、LeonAnimRig.SetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="committedWeapon">中文含义：已提交武器。使用位置：作为 LeonAnimRig.SetWeapon(WeaponType) 的参数、LeonAnimRig.InterruptWeaponActionIK(WeaponType)。</param>
    public void InterruptWeaponActionIK(WeaponType committedWeapon)
    {
        if (isDead)
        {
            return;
        }

        currentWeapon = committedWeapon;
        SetWeapon(committedWeapon);
        ResetActionBlendState();
        if (committedWeapon == WeaponType.Knife && (driver == null || !driver.SupportsNativeKnifeCarry))
        {
            knifePoseTargetWeight = 1f;
        }
        else
        {
            knifePoseTargetWeight = 0f;
        }

        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.None;
        lastPhaseChangeReason = "InterruptWeaponActionIK: " + committedWeapon;
    }

    /// <summary>EnterHitIK 的业务处理入口；本方法直接依赖 LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestHit(LeonHitDirection)；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EnterHitIK()
    {
        if (isDead)
        {
            return;
        }

        isAiming = targetArmWeight > 0.5f;
        targetArmWeight = 0f;
        ResetActionBlendState();
        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.Hit;
        lastPhaseChangeReason = "EnterHitIK aim=" + isAiming;
    }

    /// <summary>ExitHitIK 的业务处理入口；本方法直接依赖 LeonAnimRig.SetIKMode(IKMode)、LeonAnimRig.SetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.HandleFullBodyExited(Animator, FullBodyActionType, int, bool)；下游（含内部回调体） LeonAnimRig.SetIKMode(IKMode)、LeonAnimRig.SetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimRig.SetWeapon(WeaponType) 的参数、LeonAnimRig.ExitHitIK(WeaponType)。</param>
    public void ExitHitIK(WeaponType weapon)
    {
        if (isDead)
        {
            return;
        }

        currentWeapon = weapon;
        SetWeapon(weapon);
        // 优先读取正式业务瞄准状态，兜底使用受击前保存的状态
        // shouldAim（是否应当瞄准）：使用位置：LeonAnimRig.ExitHitIK(WeaponType)。
bool shouldAim = isAiming;
        if (controller != null)
        {
            shouldAim = controller.IsAiming;
        }

        if (shouldAim)
        {
            SetIKMode(IKMode.Aim);
        }
        else
        {
            SetIKMode(IKMode.None);
        }

        ikPhase = WeaponIKPhase.Stable;
        lastPhaseChangeReason = "ExitHitIK: " + weapon + " aim=" + shouldAim;
    }

    /// <summary>EnterQTEIK 的业务处理入口；本方法直接依赖 LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestQTE(int)；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EnterQTEIK()
    {
        if (isDead)
        {
            return;
        }

        qteWeaponTypeRestorePending = false;
        qteWeaponTypeRestoreFrames = 0;
        targetArmWeight = 0f;
        currentArmWeight = 0f;
        ResetActionBlendState();
        knifePoseTargetWeight = 0f;
        isAiming = false;
        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.QTE;
        lastPhaseChangeReason = "进入快速反应事件";
    }

    /// <summary>ExitQTEIK 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType)、LeonAnimRig.SetIKMode(IKMode)、LeonAnimRig.SetWeapon(WeaponType)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType)、LeonAnimRig.SetIKMode(IKMode)、LeonAnimRig.SetWeapon(WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimatorDriver.SetWeaponType(WeaponType) 的参数、作为 LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType) 的参数、作为 LeonAnimRig.SetWeapon(WeaponType) 的参数 等共 4 处。</param>
    public void ExitQTEIK(WeaponType weapon)
    {
        if (isDead)
        {
            return;
        }

        currentWeapon = weapon;
        SetWeapon(weapon);
        qteExitWeapon = weapon;
        qteWeaponTypeRestorePending = CanRestoreAnimatorWeaponType(weapon);
        if (qteWeaponTypeRestorePending)
        {
            qteWeaponTypeRestoreFrames = 12;
        }
        else
        {
            qteWeaponTypeRestoreFrames = 0;
        }

        if (driver == null)
        {
            driver = GetComponent<LeonAnimatorDriver>();
        }

        if (qteWeaponTypeRestorePending)
        {
            // receiverValue6（判空前缓存的里昂动画控制器驱动器）：从驱动组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonAnimRig.ExitQTEIK(WeaponType)。
global::LeonAnimatorDriver receiverValue6 = driver;
            // optionalValue7（条件访问得到的集合武器类型的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonAnimRig.ExitQTEIK(WeaponType)。
bool? optionalValue7;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                optionalValue7 = receiverValue6.SetWeaponType(weapon);
            }
            else
            {
                optionalValue7 = default;
            }
        }

        SetIKMode(IKMode.None);
        ikPhase = WeaponIKPhase.Stable;
        lastPhaseChangeReason = "退出快速反应事件: " + weapon;
    }

    /// <summary>EnterDeathIK 的业务处理入口；本方法直接依赖 LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimationActionCoordinator.RequestDeath()；下游（含内部回调体） LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.ResetActionBlendState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EnterDeathIK()
    {
        RestoreNativeCqbrReloadMechanism();
        isDead = true;
        qteWeaponTypeRestorePending = false;
        qteWeaponTypeRestoreFrames = 0;
        targetArmWeight = 0f;
        currentArmWeight = 0f;
        ResetActionBlendState();
        knifePoseTargetWeight = 0f;
        ForceAllWeaponIKOff();
        ikPhase = WeaponIKPhase.Dead;
        lastPhaseChangeReason = "EnterDeathIK";
    }

    // ================================================================
    // 每帧执行
    // ================================================================
    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Debug.LogWarning(object)、LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.ForceAllWeaponIKOff()、LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.IsFullBodyActionLayerInfluencingPose()、LeonAnimRig.IsLongGunWeapon(WeaponType) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (isDead)
        {
            ForceAllWeaponIKOff();
            return;
        }

        // 全身动作淡出期间保持武器约束为零，避免动作末段重新拉扯手臂。
        if (IsFullBodyActionLayerInfluencingPose() || controller != null && controller.IsHeldByTrap
            || actionCoordinator != null && actionCoordinator.IsLadderActionActive)
        {
            currentArmWeight = 0f;
            ForceAllWeaponIKOff();
            return;
        }

        // 手枪视觉揭示事件丢失时保留原有兜底；长枪兜底由装备状态进度统一处理。
        if (ikPhase == WeaponIKPhase.Equip && anim != null && !IsLongGunWeapon(currentWeapon) && !equipIKRevealReceived
            && (driver == null || !driver.NativePistolEquipActive))
        {
            // equipState（装备状态）：保存 Animator.GetCurrentAnimatorStateInfo(int) 的返回结果，供当前步骤使用。使用位置：LeonAnimRig.Update()。
AnimatorStateInfo equipState = anim.GetCurrentAnimatorStateInfo(AnimatorLayerID.WeaponAction);
            if (equipState.normalizedTime >= equipIKRevealFallbackNormalizedTime)
            {
                if (!equipIKRevealFallbackWarned)
                {
                    equipIKRevealFallbackWarned = true;
                    Debug.LogWarning("[LeonAnimRig] 手枪装备视觉揭示事件未收到，已在归一化时间 " + equipIKRevealFallbackNormalizedTime + " 触发反向动力学兜底淡入。");
                }

                NotifyEquipVisualRevealIK();
            }
        }

        knifePoseWeight = Mathf.SmoothDamp(knifePoseWeight, knifePoseTargetWeight, ref knifePoseVelocity, Mathf.Max(0.01f, knifeIKBlendDuration), Mathf.Infinity, Time.deltaTime);
        // crouchGripTarget（蹲伏握持目标）：使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.Update()。
float crouchGripTarget;
        if (controller != null && controller.IsCrouching && currentWeapon == WeaponType.Shotgun && ikPhase == WeaponIKPhase.Stable)
        {
            crouchGripTarget = shotgunCrouchLeftHandWeight;
        }
        else
        {
            crouchGripTarget = 0f;
        }

        shotgunCrouchLeftWeight = Mathf.SmoothDamp(shotgunCrouchLeftWeight, crouchGripTarget, ref shotgunCrouchLeftVelocity, Mathf.Max(0.01f, shotgunCrouchLeftHandBlendDuration), Mathf.Infinity, Time.deltaTime);
        currentArmWeight = Mathf.Lerp(currentArmWeight, targetArmWeight, Time.deltaTime * 15f);
        // 原版自由待机为单手低持。瞄准时的左手支撑继续跟随瞄准权重。
        // pistolIdle（是否手枪待机）：使用位置：LeonAnimRig.Update()。
        bool pistolIdle = currentWeapon == WeaponType.Pistol && ikPhase == WeaponIKPhase.Stable
            && (driver == null || !driver.SupportsNativePistolSwitch) && !InjuredSingleHandPoseSuppressionActive()
            && controller != null && controller.MoveInput.sqrMagnitude < 0.001f && controller.CurrentPlanarSpeed < 0.15f;
        // argumentValue10（按原顺序缓存的实参手枪左手当前权重）：从手枪左手当前权重读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数。
float argumentValue10 = pistolLeftHandCurrentWeight;
        // selectedValue9（分支选出的手枪待机左手权重）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.SmoothDamp(float, float, ref float, float, float, float) 的参数、LeonAnimRig.Update()。
float selectedValue9;
        if (pistolIdle)
        {
            selectedValue9 = pistolIdleLeftHandWeight;
        }
        else
        {
            selectedValue9 = 0f;
        }

        pistolLeftHandCurrentWeight = Mathf.SmoothDamp(argumentValue10, selectedValue9, ref pistolLeftHandWeightSpeed, Mathf.Max(0.01f, pistolLeftHandTransitionTime), Mathf.Infinity, Time.deltaTime);
        // 原版步枪动作完整记录双臂和手指，所有武器阶段直接使用动画姿态。
        rifleCompatibilityIkWeight = 1f;
        if (driver != null && driver.SupportsNativeCqbr)
        {
            rifleCompatibilityIkWeight = 0f;
        }
        ReadIKCurves();
        ApplyPhaseWeights();
        if (controller != null)
        {
            if (controller.IsFreeLooking && !controller.IsAiming && targetArmWeight > 0f)
            {
            // 自由视角不修改业务权重，只由姿态阶段控制视觉反向动力学。
            }
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimRig.RestoreQTEWeaponTypeAfterFullBodyExit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        UpdateNativeGunPresentation();
        if (controller != null)
        {
            controller.FlushWeaponMuzzleFlashes();
        }
        if (isDead)
        {
            return;
        }

        RestoreQTEWeaponTypeAfterFullBodyExit();
        // Update aim target position
        if (controller != null && controller.playerCamera != null)
        {
        // aimTarget logic can go here if needed
        }
    }

    /// <summary>RestoreQTEWeaponTypeAfterFullBodyExit 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.LateUpdate()；下游（含内部回调体） Animator.GetLayerWeight(int)、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RestoreQTEWeaponTypeAfterFullBodyExit()
    {
        if (!qteWeaponTypeRestorePending || anim == null)
        {
            return;
        }

        if (!CanRestoreAnimatorWeaponType(qteExitWeapon))
        {
            qteWeaponTypeRestorePending = false;
            qteWeaponTypeRestoreFrames = 0;
            return;
        }

        if (anim.GetLayerWeight(LeonAnimatorDriver.LayerFullBodyAction) > 0.001f)
        {
            return;
        }

        if (driver == null)
        {
            driver = GetComponent<LeonAnimatorDriver>();
        }

        if (driver == null || !driver.SetWeaponType(qteExitWeapon))
        {
            return;
        }

        qteWeaponTypeRestoreFrames--;
        if (qteWeaponTypeRestoreFrames <= 0)
        {
            qteWeaponTypeRestorePending = false;
        }
    }

    /// <summary>CanRestoreAnimatorWeaponType 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ExitQTEIK(WeaponType)、LeonAnimRig.RestoreQTEWeaponTypeAfterFullBodyExit()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.CanRestoreAnimatorWeaponType(WeaponType)。</param>
    private static bool CanRestoreAnimatorWeaponType(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>ReadIKCurves 的业务处理入口；本方法直接依赖 Animator.GetFloat(int)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.Update()；下游（含内部回调体） Animator.GetFloat(int)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ReadIKCurves()
    {
        if (anim == null)
        {
            rawSourceRight = rawSourceLeft = rawTargetRight = rawTargetLeft = 0f;
            return;
        }

        rawSourceRight = Mathf.Clamp01(anim.GetFloat(HashIKSourceRight));
        rawSourceLeft = Mathf.Clamp01(anim.GetFloat(HashIKSourceLeft));
        rawTargetRight = Mathf.Clamp01(anim.GetFloat(HashIKTargetRight));
        rawTargetLeft = Mathf.Clamp01(anim.GetFloat(HashIKTargetLeft));
    }

    /// <summary>中文含义：是否小刀动作抑制。使用位置：LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.BeginKnifeActionIK()、LeonAnimRig.EndKnifeActionIK()。</summary>
    private bool knifeActionSuppression;
    /// <summary>ApplyPhaseWeights 的业务处理入口；本方法直接依赖 Debug.Log(object)、LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float)、LeonAnimRig.GetTargetPath(Transform)、LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.IsLongGunWeapon(WeaponType) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.Update()；下游（含内部回调体） Debug.Log(object)、LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float)、LeonAnimRig.GetTargetPath(Transform)、LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.IsLongGunWeapon(WeaponType) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyPhaseWeights()
    {
        if (knifeActionSuppression)
        {
            SetAllIKWeights(0f);
            return;
        }

        // bw（身体权重）：使用位置：作为 Debug.Log(object) 的参数、作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、作为 LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float) 的参数 等共 5 处。
float bw = currentArmWeight;
        // DEBUG: 打印当前武器和目标信息（含父级路径以区分手枪/步枪）
        if (currentWeapon != lastDebugWeapon || ikPhase != lastDebugPhase)
        {
            Debug.Log($"[LeonAnimRig] Weapon={currentWeapon} Phase={ikPhase} bw={bw:F2}\n" + $"  PistolR target={GetTargetPath(pistolRightTarget)}\n" + $"  PistolL target={GetTargetPath(pistolLeftTarget)}\n" + $"  RifleR  target={GetTargetPath(rifleRightTarget)}\n" + $"  RifleL  target={GetTargetPath(rifleLeftTarget)}\n" + $"  ShotgunR target={GetTargetPath(shotgunRightTarget)}\n" + $"  ShotgunL target={GetTargetPath(shotgunLeftTarget)}");
            lastDebugWeapon = currentWeapon;
            lastDebugPhase = ikPhase;
        }

        switch (ikPhase)
        {
            case WeaponIKPhase.Stable:
                if (InjuredSingleHandPoseSuppressionActive())
                {
                    // 受伤单手非瞄准状态：完全卸除手臂位置IK，保留受伤动画原生姿态
                    SetAllIKWeights(0f);
                    break;
                }

                // stableRightWeight（稳定右侧权重）：使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、作为 LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float) 的参数。
float stableRightWeight = bw;
                // stableLeftWeight（稳定左侧权重）：使用位置：作为 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float) 的参数、作为 LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float) 的参数、作为 Mathf.Max(float, float) 的参数 等共 4 处。
float stableLeftWeight = bw;
                if (currentWeapon == WeaponType.Pistol)
                {
                    stableLeftWeight = Mathf.Max(stableLeftWeight, pistolLeftHandCurrentWeight);
                }

                EvaluateStableBlend(bw, stableLeftWeight, ref stableRightWeight, ref stableLeftWeight);
                // 长枪在非瞄准时也需要左掌承托护木，手指贴合以此为基准。
                if (IsLongGunWeapon(currentWeapon))
                {
                    stableLeftWeight = Mathf.Max(stableLeftWeight, longGunSupportHandWeight);
                }

                if (controller != null && controller.IsInjured && IsLongGunWeapon(currentWeapon))
                {
                    stableLeftWeight = Mathf.Max(stableLeftWeight, longGunSupportHandWeight);
                }

                ApplyWeaponIK(currentWeapon, stableRightWeight, stableLeftWeight);
                break;
            case WeaponIKPhase.Reload:
                ApplyWeaponIK(currentWeapon, bw * rawSourceRight, bw * rawSourceLeft);
                break;
            case WeaponIKPhase.Holster:
                ApplyWeaponIK(sourceWeapon, bw * rawSourceRight, bw * rawSourceLeft);
                break;
            case WeaponIKPhase.WeaponCommitted:
                // commitDuration（提交持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数。
float commitDuration = Mathf.Max(0.01f, weaponCommitIKBlendDuration);
                committedBlendElapsed = Mathf.Min(committedBlendElapsed + Time.deltaTime, commitDuration);
                // commitProgress（提交进度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float commitProgress = Mathf.SmoothStep(0f, 1f, committedBlendElapsed / commitDuration);
                if (InjuredSingleHandPoseSuppressionActive())
                {
                    // 受伤单手非瞄准状态：跳过旧武器约束渐出，直接归零
                    SetAllIKWeights(0f);
                    break;
                }

                ApplyWeaponIK(committedSourceWeapon, Mathf.Lerp(committedSourceRightWeight, 0f, commitProgress), Mathf.Lerp(committedSourceLeftWeight, 0f, commitProgress));
                break;
            case WeaponIKPhase.Equip:
                // 长枪必须等待独立副手握把标记，手枪仍沿用视觉揭示标记。
                // longGunEquip（是否长枪装备）：保存 LeonAnimRig.IsLongGunWeapon(WeaponType) 的返回结果，供当前步骤使用。使用位置：LeonAnimRig.ApplyPhaseWeights()。
bool longGunEquip = IsLongGunWeapon(currentWeapon);
                // selectedValue11（分支选出的装备反向动力学是否就绪）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonAnimRig.ApplyPhaseWeights()。
bool selectedValue11;
                if (longGunEquip)
                {
                    selectedValue11 = longGunSupportHandGripReceived;
                }
                else
                {
                    selectedValue11 = equipIKRevealReceived;
                }

                // equipIKReady（装备反向动力学是否就绪）：使用位置：LeonAnimRig.ApplyPhaseWeights()。
bool equipIKReady = selectedValue11;
                if (!equipIKReady)
                {
                    SetAllIKWeights(0f);
                    break;
                }

                // argumentValue13（按原顺序缓存的实参0.01f）：从0.01f读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue13 = 0.01f;
                // selectedValue12（分支选出的长枪支撑手部淡入持续时间）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、LeonAnimRig.ApplyPhaseWeights()。
float selectedValue12;
                if (longGunEquip)
                {
                    selectedValue12 = longGunSupportHandFadeInDuration;
                }
                else
                {
                    selectedValue12 = equipIKFadeInDuration;
                }

                // equipFadeDuration（装备淡入淡出时长）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数。
float equipFadeDuration = Mathf.Max(argumentValue13, selectedValue12);
                equipIKFadeElapsed = Mathf.Min(equipIKFadeElapsed + Time.deltaTime, equipFadeDuration);
                // equipFadeProgress（装备淡入淡出进度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float equipFadeProgress = Mathf.SmoothStep(0f, 1f, equipIKFadeElapsed / equipFadeDuration);
                // equipRightTarget（装备右侧目标）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float equipRightTarget = bw;
                // selectedValue14（分支选出的装备左侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonAnimRig.ApplyPhaseWeights()。
float selectedValue14;
                if (longGunEquip)
                {
                    selectedValue14 = Mathf.Max(bw, longGunSupportHandWeight);
                }
                else
                {
                    selectedValue14 = bw;
                }

                // equipLeftTarget（装备左侧目标）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float equipLeftTarget = selectedValue14;
                ApplyWeaponIK(currentWeapon, Mathf.Lerp(equipIKFadeStartRightWeight, equipRightTarget, equipFadeProgress), Mathf.Lerp(equipIKFadeStartLeftWeight, equipLeftTarget, equipFadeProgress));
                break;
            case WeaponIKPhase.Hit:
            case WeaponIKPhase.QTE:
            case WeaponIKPhase.None:
            default:
                SetAllIKWeights(0f);
                break;
        }

        ApplyKnifePoseIK();
    }

    /// <summary>ApplyKnifePoseIK 的业务处理入口；本方法直接依赖 LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.SetIK(LimbIK, float, Transform)、Mathf.Clamp01(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()；下游（含内部回调体） LeonAnimRig.InjuredSingleHandPoseSuppressionActive()、LeonAnimRig.SetIK(LimbIK, float, Transform)、Mathf.Clamp01(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ApplyKnifePoseIK()
    {
        // blocked（是否受阻）：使用位置：LeonAnimRig.ApplyKnifePoseIK()。
bool blocked = isDead || ikPhase == WeaponIKPhase.Hit || ikPhase == WeaponIKPhase.QTE || InjuredSingleHandPoseSuppressionActive();
        // poseWeight（姿态权重）：使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyKnifePoseIK()。
float poseWeight;
        if (blocked)
        {
            poseWeight = 0f;
        }
        else
        {
            poseWeight = Mathf.Clamp01(knifePoseWeight);
        }

        SetIK(knifeRightIK, poseWeight * knifeRightHandWeight, knifeRightTarget);
        SetIK(knifeLeftIK, poseWeight * knifeLeftHandWeight, knifeLeftTarget);
    }

    /// <summary>IsFullBodyActionLayerInfluencingPose 的业务处理入口；本方法直接依赖 Animator.GetLayerWeight(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.Update()；下游（含内部回调体） Animator.GetLayerWeight(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFullBodyActionLayerInfluencingPose()
    {
        return anim != null && anim.GetLayerWeight(LeonAnimatorDriver.LayerFullBodyAction) > 0.001f;
    }

    /// <summary>
    /// 受伤单手姿态抑制：
    /// 受伤且未蹲伏、持手枪或小刀、未瞄准的稳定阶段
    /// （Stable 或切枪收尾后的 WeaponCommitted），
    /// 手臂位置反向动力学不得把受伤动画的左手扶腹、右手下垂姿势拉回正常持枪姿态。
    /// 武器仍挂在手部骨骼上；进入瞄准后由现有流程恢复瞄准反向动力学。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool InjuredSingleHandPoseSuppressionActive()
    {
        // inStablePhase（是否内部稳定阶段）：使用位置：LeonAnimRig.InjuredSingleHandPoseSuppressionActive()。
bool inStablePhase = ikPhase == WeaponIKPhase.Stable || ikPhase == WeaponIKPhase.WeaponCommitted;
        return controller != null && controller.IsInjured && !controller.IsCrouching && !isAiming && inStablePhase && (currentWeapon == WeaponType.Pistol || currentWeapon == WeaponType.Knife);
    }

    /// <summary>BeginStableBlendFromCurrentPose 的业务处理入口；本方法直接依赖 LeonAnimRig.GetLeftIK(WeaponType)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.GetSolverWeight(LimbIK)、LeonAnimRig.IsIKWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.CompleteWeaponActionIK(WeaponType)；下游（含内部回调体） LeonAnimRig.GetLeftIK(WeaponType)、LeonAnimRig.GetRightIK(WeaponType)、LeonAnimRig.GetSolverWeight(LimbIK)、LeonAnimRig.IsIKWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimRig.GetLeftIK(WeaponType) 的参数、作为 LeonAnimRig.GetRightIK(WeaponType) 的参数、作为 LeonAnimRig.IsIKWeapon(WeaponType) 的参数。</param>
    private void BeginStableBlendFromCurrentPose(WeaponType weapon)
    {
        // rightIK（右侧反向动力学）：保存 LeonAnimRig.GetRightIK(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimRig.GetSolverWeight(LimbIK) 的参数。
LimbIK rightIK = GetRightIK(weapon);
        // leftIK（左侧反向动力学）：保存 LeonAnimRig.GetLeftIK(WeaponType) 的返回结果，供当前步骤使用。使用位置：作为 LeonAnimRig.GetSolverWeight(LimbIK) 的参数。
LimbIK leftIK = GetLeftIK(weapon);
        stableBlendStartRightWeight = GetSolverWeight(rightIK);
        stableBlendStartLeftWeight = GetSolverWeight(leftIK);
        stableBlendElapsed = 0f;
        stableBlendActive = IsIKWeapon(weapon);
    }

    /// <summary>ApplyWeaponIK 的业务处理入口；本方法直接依赖 LeonAnimRig.GetSafeRightHandWeight(float, Transform)、LeonAnimRig.IsIKWeapon(WeaponType)、LeonAnimRig.SetAllIKWeights(float)、LeonAnimRig.SetIK(LimbIK, float, Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()；下游（含内部回调体） LeonAnimRig.GetSafeRightHandWeight(float, Transform)、LeonAnimRig.IsIKWeapon(WeaponType)、LeonAnimRig.SetAllIKWeights(float)、LeonAnimRig.SetIK(LimbIK, float, Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：作为 LeonAnimRig.IsIKWeapon(WeaponType) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</param>
    /// <param name="rightWeight">中文含义：右侧权重。使用位置：作为 LeonAnimRig.GetSafeRightHandWeight(float, Transform) 的参数。</param>
    /// <param name="leftWeight">中文含义：左侧权重。使用位置：LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。</param>
    private void ApplyWeaponIK(WeaponType weapon, float rightWeight, float leftWeight)
    {
        if (!IsIKWeapon(weapon))
        {
            SetAllIKWeights(0f);
            return;
        }

        // argumentValue18（按原顺序缓存的实参手枪右侧反向动力学）：从手枪右侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue18 = pistolRightIK;
        // argumentValue19（按原顺序缓存的实参0f）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue19;
        if (weapon == WeaponType.Pistol)
        {
            argumentValue19 = GetSafeRightHandWeight(rightWeight, pistolRightTarget) * PistolCompatibilityIkWeight;
        }
        else
        {
            argumentValue19 = 0f;
        }

        // selectedValue17（分支选出的手枪右侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue17;
        if (weapon == WeaponType.Pistol)
        {
            selectedValue17 = pistolRightTarget;
        }
        else
        {
            selectedValue17 = null;
        }

        SetIK(argumentValue18, argumentValue19, selectedValue17);
        // argumentValue22（按原顺序缓存的实参手枪左侧反向动力学）：从手枪左侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue22 = pistolLeftIK;
        // argumentValue23（按原顺序缓存的实参左侧权重）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue23;
        if (weapon == WeaponType.Pistol)
        {
            argumentValue23 = leftWeight * PistolCompatibilityIkWeight;
        }
        else
        {
            argumentValue23 = 0f;
        }

        // selectedValue21（分支选出的手枪左侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue21;
        if (weapon == WeaponType.Pistol)
        {
            selectedValue21 = pistolLeftTarget;
        }
        else
        {
            selectedValue21 = null;
        }

        SetIK(argumentValue22, argumentValue23, selectedValue21);
        // argumentValue26（按原顺序缓存的实参步枪右侧反向动力学）：从步枪右侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue26 = rifleRightIK;
        // argumentValue27（按原顺序缓存的实参0f）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue27;
        if (weapon == WeaponType.Rifle)
        {
            argumentValue27 = GetSafeRightHandWeight(rightWeight, rifleRightTarget) * rifleCompatibilityIkWeight;
        }
        else
        {
            argumentValue27 = 0f;
        }

        // selectedValue25（分支选出的步枪右侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue25;
        if (weapon == WeaponType.Rifle)
        {
            selectedValue25 = rifleRightTarget;
        }
        else
        {
            selectedValue25 = null;
        }

        SetIK(argumentValue26, argumentValue27, selectedValue25);
        // argumentValue30（按原顺序缓存的实参步枪左侧反向动力学）：从步枪左侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue30 = rifleLeftIK;
        // argumentValue31（按原顺序缓存的实参左侧权重）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue31;
        if (weapon == WeaponType.Rifle)
        {
            argumentValue31 = leftWeight * rifleCompatibilityIkWeight;
        }
        else
        {
            argumentValue31 = 0f;
        }

        // selectedValue29（分支选出的步枪左侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue29;
        if (weapon == WeaponType.Rifle)
        {
            selectedValue29 = rifleLeftTarget;
        }
        else
        {
            selectedValue29 = null;
        }

        SetIK(argumentValue30, argumentValue31, selectedValue29);
        // argumentValue34（按原顺序缓存的实参霰弹枪右侧反向动力学）：从霰弹枪右侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue34 = shotgunRightIK;
        // argumentValue35（按原顺序缓存的实参0f）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue35;
        if (weapon == WeaponType.Shotgun)
        {
            argumentValue35 = GetSafeRightHandWeight(rightWeight, shotgunRightTarget) * ShotgunCompatibilityIkWeight;
        }
        else
        {
            argumentValue35 = 0f;
        }

        // selectedValue33（分支选出的霰弹枪右侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue33;
        if (weapon == WeaponType.Shotgun)
        {
            selectedValue33 = shotgunRightTarget;
        }
        else
        {
            selectedValue33 = null;
        }

        SetIK(argumentValue34, argumentValue35, selectedValue33);
        // argumentValue38（按原顺序缓存的实参霰弹枪左侧反向动力学）：从霰弹枪左侧反向动力学读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数。
global::RootMotion.FinalIK.LimbIK argumentValue38 = shotgunLeftIK;
        // argumentValue39（按原顺序缓存的实参左侧权重）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
float argumentValue39;
        if (weapon == WeaponType.Shotgun)
        {
            argumentValue39 = leftWeight * ShotgunCompatibilityIkWeight;
        }
        else
        {
            argumentValue39 = 0f;
        }

        // selectedValue37（分支选出的霰弹枪左侧目标）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonAnimRig.SetIK(LimbIK, float, Transform) 的参数、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)。
global::UnityEngine.Transform selectedValue37;
        if (weapon == WeaponType.Shotgun)
        {
            selectedValue37 = shotgunLeftTarget;
        }
        else
        {
            selectedValue37 = null;
        }

        SetIK(argumentValue38, argumentValue39, selectedValue37);
    }

    /// <summary>GetRightIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.GetRightIK(WeaponType)。</param>
    private LimbIK GetRightIK(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Pistol:
                return pistolRightIK;
            case WeaponType.Rifle:
                return rifleRightIK;
            case WeaponType.Shotgun:
                return shotgunRightIK;
            default:
                return null;
        }
    }

    /// <summary>GetLeftIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.GetLeftIK(WeaponType)。</param>
    private LimbIK GetLeftIK(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Pistol:
                return pistolLeftIK;
            case WeaponType.Rifle:
                return rifleLeftIK;
            case WeaponType.Shotgun:
                return shotgunLeftIK;
            default:
                return null;
        }
    }

    /// <summary>IsIKWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)、LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.IsIKWeapon(WeaponType)。</param>
    private static bool IsIKWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Pistol || weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>IsLongGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()、LeonAnimRig.NotifyEquipVisualRevealIK()、LeonAnimRig.NotifyLongGunSupportHandGripIK()、LeonAnimRig.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">中文含义：当前武器类别。使用位置：LeonAnimRig.IsLongGunWeapon(WeaponType)。</param>
    private static bool IsLongGunWeapon(WeaponType weapon)
    {
        return weapon == WeaponType.Rifle || weapon == WeaponType.Shotgun;
    }

    /// <summary>EvaluateStableBlend 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetWeight">中文含义：目标权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</param>
    /// <param name="leftHandTargetWeight">中文含义：左手目标权重。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</param>
    /// <param name="rightWeight">中文含义：右侧权重。按引用传入，读写会作用于调用方原存储位置。使用位置：LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float)。</param>
    /// <param name="leftWeight">中文含义：左侧权重。按引用传入，读写会作用于调用方原存储位置。使用位置：LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float)。</param>
    private void EvaluateStableBlend(float targetWeight, float leftHandTargetWeight, ref float rightWeight, ref float leftWeight)
    {
        if (!stableBlendActive)
        {
            return;
        }

        // duration（持续时间）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(float, float) 的参数、作为 Mathf.SmoothStep(float, float, float) 的参数、LeonAnimRig.EvaluateStableBlend(float, float, ref float, ref float)。
float duration = Mathf.Max(0.01f, stableIKBlendDuration);
        stableBlendElapsed = Mathf.Min(stableBlendElapsed + Time.deltaTime, duration);
        // progress（进度）：保存 Mathf.SmoothStep(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。
float progress = Mathf.SmoothStep(0f, 1f, stableBlendElapsed / duration);
        rightWeight = Mathf.Lerp(stableBlendStartRightWeight, targetWeight, progress);
        leftWeight = Mathf.Lerp(stableBlendStartLeftWeight, leftHandTargetWeight, progress);
        if (stableBlendElapsed >= duration)
        {
            stableBlendActive = false;
        }
    }

    /// <summary>GetSafeRightHandWeight 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Transform.IsChildOf(Transform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Transform.IsChildOf(Transform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="requestedWeight">中文含义：请求的权重。使用位置：LeonAnimRig.GetSafeRightHandWeight(float, Transform)。</param>
    /// <param name="target">中文含义：目标变换节点。使用位置：LeonAnimRig.GetSafeRightHandWeight(float, Transform)。</param>
    private float GetSafeRightHandWeight(float requestedWeight, Transform target)
    {
        if (!allowExternalRightHandIK || requestedWeight <= 0f || target == null || anim == null)
        {
            return 0f;
        }

        // rightHand（右手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.IsChildOf(Transform) 的参数、LeonAnimRig.GetSafeRightHandWeight(float, Transform)。
Transform rightHand = AnimatorBoneBindings.Resolve(anim, HumanBodyBones.RightHand);
        if (rightHand == null || target == rightHand || target.IsChildOf(rightHand))
        {
            return 0f;
        }

        return requestedWeight;
    }

    /// <summary>GetSolverWeight 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.BeginStableBlendFromCurrentPose(WeaponType)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ik">中文含义：反向动力学。使用位置：LeonAnimRig.GetSolverWeight(LimbIK)。</param>
    private static float GetSolverWeight(LimbIK ik)
    {
        if (ik != null)
        {
            return ik.solver.IKPositionWeight;
        }
        else
        {
            return 0f;
        }
    }

    /// <summary>ResetActionBlendState 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.EnterDeathIK()、LeonAnimRig.EnterHitIK()、LeonAnimRig.EnterQTEIK()、LeonAnimRig.InterruptWeaponActionIK(WeaponType)、LeonAnimRig.OnDisable() 等共 6 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResetActionBlendState()
    {
        equipCurveRightWeight = 0f;
        equipCurveLeftWeight = 0f;
        stableBlendStartRightWeight = 0f;
        stableBlendStartLeftWeight = 0f;
        stableBlendElapsed = 0f;
        stableBlendActive = false;
        committedSourceRightWeight = 0f;
        committedSourceLeftWeight = 0f;
        committedBlendElapsed = 0f;
        equipIKRevealReceived = false;
        equipIKRevealFallbackWarned = false;
        longGunSupportHandGripReceived = false;
    }

    /// <summary>SetIK 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyKnifePoseIK()、LeonAnimRig.ApplyWeaponIK(WeaponType, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ik">中文含义：反向动力学。使用位置：LeonAnimRig.SetIK(LimbIK, float, Transform)。</param>
    /// <param name="weight">中文含义：权重。使用位置：LeonAnimRig.SetIK(LimbIK, float, Transform)。</param>
    /// <param name="target">中文含义：目标变换节点。使用位置：LeonAnimRig.SetIK(LimbIK, float, Transform)。</param>
    private void SetIK(LimbIK ik, float weight, Transform target)
    {
        if (ik == null)
        {
            return;
        }

        ik.solver.IKPositionWeight = weight;
        ik.solver.IKRotationWeight = weight;
        if (target != null && weight > 0.001f)
        {
            ik.solver.IKPosition = target.position;
            ik.solver.IKRotation = target.rotation;
        }
    }

    /// <summary>GetTargetPath 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonAnimRig.ApplyPhaseWeights()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="t">中文含义：当前变换节点。使用位置：LeonAnimRig.GetTargetPath(Transform)。</param>
    private static string GetTargetPath(Transform t)
    {
        if (t == null)
        {
            return "NULL";
        }

        if (t.parent == null)
        {
            return t.name;
        }

        return t.parent.name + "/" + t.name;
    }

    // ================================================================
    // 诊断属性
    // ================================================================
    /// <summary>中文含义：反向动力学阶段。使用位置：LeonFingerIKController.CanCaptureRuntimeTargets()、LeonFingerIKController.EvaluateTargetWeight(WeaponType)。</summary>
    public WeaponIKPhase IKPhase => ikPhase;
    /// <summary>中文含义：当前武器。使用位置：作为 LeonAnimRig.ExitHitIK(WeaponType) 的参数、LeonAnimationActionCoordinator.InterruptWeaponTransactionForFullBody()、LeonFingerIKController.GetCurrentWeapon() 等共 4 处。</summary>
    public WeaponType CurrentWeapon => currentWeapon;
    /// <summary>中文含义：是否死亡。使用位置：DoorController.IsPlayerBlocked()、LeonFingerIKController.EvaluateTargetWeight(WeaponType)、LeonQTECoordinator.ReleasePlayer(int)。</summary>
    public bool IsDead => isDead;
    /// <summary>中文含义：是否正在瞄准。</summary>
    public bool IsAiming => isAiming;
    /// <summary>中文含义：上一次阶段变化原因。</summary>
    public string LastPhaseChangeReason => lastPhaseChangeReason;
    /// <summary>中文含义：原始来源右侧。</summary>
    public float RawSourceRight => rawSourceRight;
    /// <summary>中文含义：原始来源左侧。</summary>
    public float RawSourceLeft => rawSourceLeft;
    /// <summary>中文含义：原始目标右侧。</summary>
    public float RawTargetRight => rawTargetRight;
    /// <summary>中文含义：原始目标左侧。</summary>
    public float RawTargetLeft => rawTargetLeft;

    /// <summary>中文含义：手枪右侧权重。使用位置：LeonAnimRig.PistolRigWeight.get。</summary>
    internal float PistolCompatibilityIkWeight
    {
        get
        {
            if (driver == null)
            {
                return 1f;
            }
            // 进退瞄中的握持已经烘焙，禁止旧求解器在动作层淡入时提前拉回左手。
            if (driver.NativePistolOwnsAimArms)
            {
                return 0f;
            }
            if (driver.IsNativePistolSwitchPlaying() || driver.NativePistolReloadActive)
            {
                return 1f - driver.WeaponActionLayerWeight;
            }
            return 1f;
        }
    }

    public float PistolRightWeight
    {
        get
        {
            if (pistolRightIK != null)
            {
                return pistolRightIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：手枪左侧权重。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</summary>
    public float PistolLeftWeight
    {
        get
        {
            if (pistolLeftIK != null)
            {
                return pistolLeftIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：步枪右侧权重。使用位置：LeonAnimRig.RifleRigWeight.get。</summary>
    public float RifleRightWeight
    {
        get
        {
            if (rifleRightIK != null)
            {
                return rifleRightIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：步枪左侧权重。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</summary>
    public float RifleLeftWeight
    {
        get
        {
            if (rifleLeftIK != null)
            {
                return rifleLeftIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：霰弹枪右侧权重。使用位置：LeonAnimRig.ShotgunRigWeight.get。</summary>
    public float ShotgunRightWeight
    {
        get
        {
            if (shotgunRightIK != null)
            {
                return shotgunRightIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：霰弹枪左侧权重。使用位置：LeonFingerIKController.GetFingerWeight(WeaponType, bool, float)。</summary>
    public float ShotgunLeftWeight
    {
        get
        {
            if (shotgunLeftIK != null)
            {
                return shotgunLeftIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：小刀右侧权重。</summary>
    public float KnifeRightWeight
    {
        get
        {
            if (knifeRightIK != null)
            {
                return knifeRightIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：小刀左侧权重。</summary>
    public float KnifeLeftWeight
    {
        get
        {
            if (knifeLeftIK != null)
            {
                return knifeLeftIK.solver.IKPositionWeight;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：小刀姿态权重。</summary>
    public float KnifePoseWeight => knifePoseWeight;
    /// <summary>中文含义：手枪绑定权重。</summary>
    public float PistolRigWeight => PistolRightWeight;
    /// <summary>中文含义：步枪绑定权重。</summary>
    public float RifleRigWeight => RifleRightWeight;
    /// <summary>中文含义：霰弹枪绑定权重。</summary>
    public float ShotgunRigWeight => ShotgunRightWeight;
}
