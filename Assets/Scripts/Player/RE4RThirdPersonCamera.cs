using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// RE4 Remake 第三人称相机控制器 — Cinemachine 驱动版
///
/// 架构：
///   VCam_RE4 [CinemachineCamera]
///     ├── CinemachineThirdPersonFollow → 位置（肩偏移 + 距离 + 高度 + 阻尼）
///     ├── CinemachinePanTilt           → 旋转（pan=yaw基准, tilt=pitch）
///     ├── CinemachineDeoccluder        → 仅保留遮挡质量评估，不参与位置修正
///     └── RE4StableCameraCollision     → 从上身观察点到期望镜头做单段碰撞修正
///
/// 核心原理（位置与旋转完全独立）：
///   FollowPivot 旋转 = yaw only → 相机位置在水平面内，pitch 不影响高度
///   PanTilt tilt = pitch        → 相机朝向 = (yaw, pitch)，准心 = 摄像机 forward
///
/// 十大模块：
///   1. 跟随       — SmoothDamp 位置阻尼 + Slerp 旋转阻尼 + 角色转向延迟跟随
///   2. 鼠标旋转   — X=yaw, Y=pitch, 限制俯仰, 支持反转
///   3. 碰撞检测   — 单段球形检测 + 离开遮挡迟滞，排除玩家层
///   4. 瞄准模式   — 三状态（常态/瞄准/冲刺）参数实时插值
///   5. 镜头震动   — Perlin noise Dutch angle, 公共 CameraShake API
///   6. 剧情锁定   — 外部锁定/解锁, 平滑过渡回弹
///   7. 奔跑效果   — 低强度 Perlin 位置抖动, 模拟 RE4 跑动震动
///   8. 待机呼吸   — 缓慢水平摆动 + 垂直浮动, 提升沉浸感
///   9. 近战推近   — 近战攻击时镜头前推 + 震动, 自动回弹
///  10. 敌人拉远   — 敌人靠近时自动拉远镜头, 离开后回弹
/// </summary>
/// <remarks>正式第三人称镜头手感驱动，承接 RE4CameraController 的场景与武器配置，处理自由环顾、回正、碰撞和开火反馈，并配合外部镜头同步与接管。</remarks>
[DefaultExecutionOrder(-100)]
public partial class RE4RThirdPersonCamera : MonoBehaviour
{
    private static readonly int AimParameterHash = Animator.StringToHash("Aim");

    // ========================================================================
    // 模块 1：跟随设置
    // ========================================================================
    /// <summary>目标角色 Transform，为空则使用本物体。</summary>
    [Header("【1】跟随设置")]
    [Tooltip("目标角色 Transform，为空则使用本物体")]
    [SerializeField]
    private Transform target;
    /// <summary>常态相机偏移（X=左右肩, Y=高度, Z=-距离）。</summary>
    [Tooltip("常态相机偏移（X=左右肩, Y=高度, Z=-距离）")]
    [SerializeField]
    private Vector3 normalOffset = new Vector3(0.5f, 1.6f, -1.8f);
    /// <summary>瞄准时相机偏移（拉近 + 专属肩位）。</summary>
    [Tooltip("瞄准时相机偏移（拉近 + 专属肩位）")]
    [SerializeField]
    private Vector3 aimOffset = new Vector3(0.62f, 1.56f, -0.82f);
    /// <summary>奔跑时相机偏移（轻微拉远）。</summary>
    [Tooltip("奔跑时相机偏移（轻微拉远）")]
    [SerializeField]
    private Vector3 sprintOffset = new Vector3(0.5f, 1.65f, -2.0f);
    /// <summary>非瞄准蹲伏时的垂直臂偏移。</summary>
    [Header("蹲伏镜头")]
    [Tooltip("非瞄准蹲伏时的垂直臂偏移")]
    [SerializeField]
    private float crouchExploreVerticalOffset = -0.42f;
    /// <summary>瞄准蹲伏时的垂直臂偏移。</summary>
    [Tooltip("瞄准蹲伏时的垂直臂偏移")]
    [SerializeField]
    private float crouchAimVerticalOffset = -0.36f;
    /// <summary>蹲伏时额外拉远的距离。</summary>
    [Tooltip("蹲伏时额外拉远的距离")]
    [SerializeField]
    private float crouchDistanceOffset = 0.1f;
    /// <summary>蹲伏镜头过渡速度。</summary>
    [Tooltip("蹲伏镜头过渡速度")]
    [SerializeField, Min(0.01f)]
    private float crouchCameraBlendSpeed = 8f;
    /// <summary>位置阻尼时间（秒，越小越跟手）。</summary>
    [Tooltip("位置阻尼时间（秒，越小越跟手）")]
    [SerializeField]
    private float positionDamping = 0.12f;
    /// <summary>瞄准时位置阻尼时间（更紧密）。</summary>
    [Tooltip("瞄准时位置阻尼时间（更紧密）")]
    [SerializeField]
    private float aimPositionDamping = 0.05f;
    /// <summary>旋转平滑速度（越大越快收敛）。</summary>
    [Tooltip("旋转平滑速度（越大越快收敛）")]
    [SerializeField]
    private float rotationSmoothing = 10f;
    /// <summary>角色转向时相机自动对齐速度（0=禁用）。</summary>
    [Tooltip("角色转向时相机自动对齐速度（0=禁用）")]
    [Range(0f, 5f)]
    [SerializeField]
    private float autoAlignSpeed = 0.5f;
    /// <summary>转向延迟时间（秒，RE4 经典滞后跟随手感）。</summary>
    [Tooltip("转向延迟时间（秒，RE4 经典滞后跟随手感）")]
    [Range(0f, 1f)]
    [SerializeField]
    private float turnLagDelay = 0.15f;
    /// <summary>自由视角回正时长。松开自由视角键后，水平回到角色背后的时长；移动鼠标会立即打断。</summary>
    [Header("自由视角回正")]
    [Tooltip("松开自由视角键后，水平回到角色背后的时长；移动鼠标会立即打断")]
    [Min(0.01f)]
    [UnityEngine.Serialization.FormerlySerializedAs("自由视角回正时长")]
    [UnityEngine.InspectorName("自由视角回正时长")]
    public float freeViewRecenteringDuration = 0.3f;
    /// <summary>当前帧是否回正。</summary>
    private bool currentFrameIsRecentering;
    /// <summary>瞄准接管帧。</summary>
    private int aimTakeOverFrame = -1;
    // ========================================================================
    // 模块 2：鼠标旋转
    // ========================================================================
    /// <summary>是否反转纵向。</summary>
    [Header("【2】鼠标旋转")]
    [SerializeField]
    private bool invertY = false;
    /// <summary>随机音高变化范围。</summary>
    [SerializeField]
    private Vector2 pitchRange = new Vector2(-65f, 70f);
    /// <summary>抬头达到上限时，相机相对基础高度下降的距离。</summary>
    [Header("瞄准俯仰高度联动")]
    [Tooltip("抬头达到上限时，相机相对基础高度下降的距离")]
    [SerializeField]
    private float aimLookUpHeightOffset = -0.22f;
    /// <summary>低头达到下限时，相机相对基础高度升高的距离。</summary>
    [Tooltip("低头达到下限时，相机相对基础高度升高的距离")]
    [SerializeField]
    private float aimLookDownHeightOffset = 0.32f;
    /// <summary>水平附近不触发高度联动的角度范围。</summary>
    [Tooltip("水平附近不触发高度联动的角度范围")]
    [SerializeField, Min(0f)]
    private float pitchHeightDeadZone = 2.5f;
    /// <summary>俯仰高度联动的平滑时间。</summary>
    [Tooltip("俯仰高度联动的平滑时间")]
    [SerializeField, Min(0.01f)]
    private float pitchHeightSmoothTime = 0.07f;
    /// <summary>是否使用相机碰撞层约束俯仰产生的垂直位移。使用相机碰撞层约束俯仰产生的垂直位移。</summary>
    [Tooltip("使用相机碰撞层约束俯仰产生的垂直位移")]
    [SerializeField]
    private bool constrainPitchHeightByCollision = true;
    /// <summary>水平灵敏度偏好键。编译期常量，保持既有值与消费者映射。</summary>
    public const string HorizontalSensitivityPreferenceKey = "Sensitivity_X";
    /// <summary>竖直灵敏度偏好键。编译期常量，保持既有值与消费者映射。</summary>
    public const string VerticalSensitivityPreferenceKey = "Sensitivity_Y";
    /// <summary>瞄准灵敏度偏好键。编译期常量，保持既有值与消费者映射。</summary>
    public const string AimSensitivityPreferenceKey = "AimSensitivity";
    /// <summary>最小观察灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float MinimumLookSensitivity = 0.1f;
    /// <summary>最大观察灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float MaximumLookSensitivity = 3f;
    /// <summary>最大瞄准灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float MaximumAimSensitivity = 1f;
    /// <summary>默认水平灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float DefaultHorizontalSensitivity = 1f;
    /// <summary>默认竖直灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float DefaultVerticalSensitivity = 1f;
    /// <summary>默认瞄准灵敏度。编译期常量，保持既有值与消费者映射。</summary>
    public const float DefaultAimSensitivity = 0.5f;
    /// <summary>水平度每次像素。编译期常量，保持既有值与消费者映射。</summary>
    private const float HorizontalDegreesPerPixel = 0.08f;
    /// <summary>竖直度每次像素。编译期常量，保持既有值与消费者映射。</summary>
    private const float VerticalDegreesPerPixel = 0.06f;
    /// <summary>水平灵敏度。</summary>
    private float horizontalSensitivity = DefaultHorizontalSensitivity;
    /// <summary>竖直灵敏度。</summary>
    private float verticalSensitivity = DefaultVerticalSensitivity;
    /// <summary>瞄准灵敏度。</summary>
    private float aimSensitivity = DefaultAimSensitivity;
    // ========================================================================
    // 模块 3：碰撞检测
    // ========================================================================
    /// <summary>碰撞检测层（Start 中自动排除玩家自身层）。</summary>
    [Header("【3】碰撞检测")]
    [Tooltip("碰撞检测层（Start 中自动排除玩家自身层）")]
    [SerializeField]
    private LayerMask collisionMask = ~0;
    /// <summary>球形碰撞检测半径。</summary>
    [Tooltip("球形碰撞检测半径")]
    [SerializeField]
    private float collisionRadius = 0.25f;
    /// <summary>碰撞安全边距，防止镜头贴住墙面。</summary>
    [Tooltip("碰撞安全边距，防止镜头贴住墙面")]
    [SerializeField]
    private float collisionSkin = 0.06f;
    /// <summary>上身观察点占镜头臂高度的比例。</summary>
    [Tooltip("上身观察点占镜头臂高度的比例")]
    [Range(0.5f, 1f)]
    [SerializeField]
    private float collisionFocusHeightRatio = 0.85f;
    /// <summary>战斗镜头距离下限，防止近战或遮挡把镜头压得过近。</summary>
    [Tooltip("战斗镜头距离下限，防止近战或遮挡把镜头压得过近")]
    [SerializeField]
    private float cameraDistanceFloor = 0.72f;
    /// <summary>遭遇墙体遮挡时镜头仍须保持在角色体积外的最短距离。</summary>
    [Tooltip("遭遇墙体遮挡时镜头仍须保持在角色体积外的最短距离")]
    [SerializeField, Min(0.35f)]
    private float minimumExternalCameraDistance = 0.72f;
    /// <summary>是否保护玩家蒙皮渲染器集合。</summary>
    [Header("角色渲染保护")]
    [SerializeField]
    private bool protectPlayerSkinnedRenderers = true;
    /// <summary>最小玩家渲染器包围盒半尺寸。</summary>
    [SerializeField]
    private Vector3 minimumPlayerRendererBoundsExtents = new Vector3(2f, 2.5f, 2f);
    /// <summary>玩法近裁剪面。</summary>
    [SerializeField]
    private float gameplayNearClipPlane = 0.01f;
    /// <summary>是否对齐后方玩家事件起始。</summary>
    [Header("开局镜头")]
    [SerializeField]
    private bool alignBehindPlayerOnStart = true;
    /// <summary>起始俯仰。</summary>
    [SerializeField]
    private float startPitch = 0f;
    /// <summary>离开遮挡后恢复正常距离的阻尼时间。</summary>
    [Header("遮挡平滑")]
    [Tooltip("离开遮挡后恢复正常距离的阻尼时间")]
    [SerializeField]
    private float collisionDamping = 0.18f;
    /// <summary>失去碰撞命中后保持当前距离的时间，防止门框边缘反复切换。</summary>
    [Tooltip("失去碰撞命中后保持当前距离的时间，防止门框边缘反复切换")]
    [SerializeField]
    private float collisionReleaseDelay = 0.12f;
    [Tooltip("离开窗洞或狭窄通道后的出口舒展平滑时长")]
    [SerializeField, Range(0.15f, 1.0f)]
    private float exitEaseDuration = 0.42f;
    [Tooltip("出口舒展期允许的最大相机拉远速度")]
    [SerializeField, Range(1.5f, 10.0f)]
    private float exitMaxRecoverySpeed = 4.2f;
    [Tooltip("头顶遇到低矮天花板或房梁时允许向下压低的最大高度")]
    [SerializeField, Range(0.1f, 0.8f)]
    private float ceilingDropLimit = 0.52f;
    [Tooltip("是否在 Scene 视图绘制三级避障求解器调试线")]
    [SerializeField]
    private bool debugDrawCollisionSolver = false;
    // ========================================================================
    // 模块 4：瞄准模式 / 状态 FOV
    // ========================================================================
    /// <summary>常规视野角度。</summary>
    [Header("【4】瞄准模式")]
    [SerializeField]
    private float normalFOV = 48f;
    /// <summary>瞄准视野角度。</summary>
    [SerializeField]
    private float aimFOV = 40f;
    /// <summary>冲刺视野角度。</summary>
    [SerializeField]
    private float sprintFOV = 55f;
    /// <summary>参数混合速度。</summary>
    [SerializeField]
    private float paramBlendSpeed = 8f;
    // ========================================================================
    // 模块 5：镜头震动
    // ========================================================================
    /// <summary>默认晃动持续时间。</summary>
    [Header("【5】镜头震动")]
    [SerializeField]
    private float defaultShakeDuration = 0.3f;
    // ========================================================================
    // 模块 6：剧情锁定
    // ========================================================================
    /// <summary>解锁恢复速度。</summary>
    [Header("【6】剧情锁定")]
    [SerializeField]
    private float unlockRecoverSpeed = 3f;
    // ========================================================================
    // 模块 7：奔跑效果
    // ========================================================================
    /// <summary>冲刺晃动强度。</summary>
    [Header("【7】奔跑效果")]
    [SerializeField]
    private float sprintShakeIntensity = 0.03f;
    /// <summary>冲刺晃动频率。</summary>
    [SerializeField]
    private float sprintShakeFrequency = 8f;
    /// <summary>实际水平速度达到此值后才进入奔跑镜头。</summary>
    [Header("有效奔跑镜头")]
    [Tooltip("实际水平速度达到此值后才进入奔跑镜头")]
    [SerializeField, Min(0.1f)]
    private float sprintCameraEnterSpeed = 2.25f;
    /// <summary>实际水平速度低于此值后退出奔跑镜头，避免临界速度反复切换。</summary>
    [Tooltip("实际水平速度低于此值后退出奔跑镜头，避免临界速度反复切换")]
    [SerializeField, Min(0.05f)]
    private float sprintCameraExitSpeed = 1.8f;
    // ========================================================================
    // 模块 8：待机呼吸
    // ========================================================================
    /// <summary>待机呼吸幅度。</summary>
    [Header("【8】待机呼吸")]
    [SerializeField]
    private float idleBreathAmplitude = 1.5f;
    /// <summary>待机呼吸速度。</summary>
    [SerializeField]
    private float idleBreathSpeed = 0.3f;
    /// <summary>待机上下起伏幅度。</summary>
    [SerializeField]
    private float idleBobAmplitude = 0.03f;
    /// <summary>瞄准呼吸偏航幅度。</summary>
    [SerializeField]
    private float aimBreathYawAmplitude = 0.35f;
    /// <summary>瞄准呼吸俯仰幅度。</summary>
    [SerializeField]
    private float aimBreathPitchAmplitude = 0.22f;
    /// <summary>瞄准呼吸上下起伏幅度。</summary>
    [SerializeField]
    private float aimBreathBobAmplitude = 0.012f;
    /// <summary>瞄准呼吸速度。</summary>
    [SerializeField]
    private float aimBreathSpeed = 1.15f;
    /// <summary>瞄准呼吸混合速度。</summary>
    [SerializeField]
    private float aimBreathBlendSpeed = 4f;
    /// <summary>武器后坐力俯仰缩放。</summary>
    [Header("开火反冲")]
    [SerializeField]
    private float weaponRecoilPitchScale = 0.95f;
    /// <summary>武器后坐力偏航缩放。</summary>
    [SerializeField]
    private float weaponRecoilYawScale = 0.02f;
    /// <summary>武器后坐力最小俯仰。</summary>
    [SerializeField]
    private float weaponRecoilMinimumPitch = 1.1f;
    /// <summary>武器后坐力最大俯仰。</summary>
    [SerializeField]
    private float weaponRecoilMaximumPitch = 4.8f;
    /// <summary>武器后坐力返回时间。</summary>
    [SerializeField]
    private float weaponRecoilReturnTime = 0.17f;
    /// <summary>武器后坐力晃动缩放。</summary>
    [SerializeField]
    private float weaponRecoilShakeScale = 0.25f;
    /// <summary>武器后坐力最大晃动。</summary>
    [SerializeField]
    private float weaponRecoilMaximumShake = 0.45f;
    /// <summary>武器后坐力晃动持续时间。</summary>
    [SerializeField]
    private float weaponRecoilShakeDuration = 0.075f;
    /// <summary>重型后坐力阈值。</summary>
    [SerializeField]
    private float heavyRecoilThreshold = 3f;
    /// <summary>重型后坐力最大俯仰。</summary>
    [SerializeField]
    private float heavyRecoilMaximumPitch = 5.6f;
    /// <summary>重型后坐力返回时间。</summary>
    [SerializeField]
    private float heavyRecoilReturnTime = 0.2f;
    /// <summary>重型后坐力视野角度缩放。</summary>
    [SerializeField]
    private float heavyRecoilFOVScale = 0.03f;
    /// <summary>重型后坐力最大视野角度。</summary>
    [SerializeField]
    private float heavyRecoilMaximumFOV = 0.12f;
    /// <summary>重型后坐力距离缩放。</summary>
    [SerializeField]
    private float heavyRecoilDistanceScale = 0f;
    /// <summary>重型后坐力最大距离。</summary>
    [SerializeField]
    private float heavyRecoilMaximumDistance = 0f;
    /// <summary>重型后坐力肩部缩放。</summary>
    [SerializeField]
    private float heavyRecoilShoulderScale = 0f;
    /// <summary>重型后坐力最大肩部。</summary>
    [SerializeField]
    private float heavyRecoilMaximumShoulder = 0f;
    // ========================================================================
    // 模块 9：近战镜头微推
    // ========================================================================
    /// <summary>近战推开距离。</summary>
    [Header("【9】近战镜头微推")]
    [SerializeField]
    private float meleePushDistance = 0.3f;
    /// <summary>近战推开持续时间。</summary>
    [SerializeField]
    private float meleePushDuration = 0.1f;
    /// <summary>近战恢复持续时间。</summary>
    [SerializeField]
    private float meleeRecoverDuration = 0.3f;
    /// <summary>近战晃动强度。</summary>
    [SerializeField]
    private float meleeShakeIntensity = 0.5f;
    /// <summary>是否自动检测近战。</summary>
    [SerializeField]
    private bool autoDetectMelee = true;
    // ========================================================================
    // 模块 10：敌人近距离自动拉远
    // ========================================================================
    /// <summary>掉落水平阻尼。</summary>
    [Header("【11】窗口高落差镜头")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float dropHorizontalDamping = 0.12f;
    /// <summary>掉落竖直阻尼。</summary>
    [SerializeField, Range(0.1f, 0.8f)]
    private float dropVerticalDamping = 0.36f;
    /// <summary>掉落摄像机回拉。</summary>
    [SerializeField, Range(0f, 0.8f)]
    private float dropCameraPullback = 0.2f;
    /// <summary>掉落观察向下角度。</summary>
    [SerializeField, Range(0f, 10f)]
    private float dropLookDownAngle = 4f;
    /// <summary>掉落冲击压缩。</summary>
    [SerializeField, Range(0f, 0.4f)]
    private float dropImpactCompression = 0.12f;
    /// <summary>掉落冲击俯仰。</summary>
    [SerializeField, Range(0f, 8f)]
    private float dropImpactPitch = 2f;
    /// <summary>掉落冲击持续时间。</summary>
    [SerializeField, Range(0.05f, 0.5f)]
    private float dropImpactDuration = 0.22f;
    /// <summary>掉落恢复持续时间。</summary>
    [SerializeField, Range(0.1f, 1f)]
    private float dropRecoveryDuration = 0.42f;
    /// <summary>最小掉落冲击速度。</summary>
    [SerializeField, Range(2f, 10f)]
    private float minimumDropImpactSpeed = 4f;
    /// <summary>最大掉落冲击速度。</summary>
    [SerializeField, Range(4f, 14f)]
    private float maximumDropImpactSpeed = 8f;
    /// <summary>敌人图层遮罩。</summary>
    [Header("【10】敌人近距离自动拉远")]
    [SerializeField]
    private LayerMask enemyLayerMask = 0;
    /// <summary>敌人临近程度范围。</summary>
    [SerializeField]
    private float enemyProximityRange = 3f;
    /// <summary>敌人缩放视野输出系数。</summary>
    [SerializeField]
    private float enemyZoomOutFactor = 1.3f;
    /// <summary>敌人缩放视野速度。</summary>
    [SerializeField]
    private float enemyZoomSpeed = 3f;
    /// <summary>敌人检查帧间隔。</summary>
    [SerializeField]
    private int enemyCheckFrameInterval = 5;
    // ========================================================================
    // 内部状态
    // ========================================================================
    /// <summary>里昂玩家控制器。</summary>
    private LeonController controller;
    /// <summary>动画控制器。</summary>
    private Animator anim;
    /// <summary>摄像机。</summary>
    private Camera cam;
    // Cinemachine 组件
    /// <summary>虚拟摄像机。</summary>
    private CinemachineCamera vcam;
    /// <summary>第三人称跟随。</summary>
    private CinemachineThirdPersonFollow tpf;
    /// <summary>水平转动竖直转动。</summary>
    private CinemachinePanTilt panTilt;
    /// <summary>遮挡解除器。</summary>
    private CinemachineDeoccluder deoccluder;
    /// <summary>稳定碰撞。</summary>
    private RE4StableCameraCollision stableCollision;
    /// <summary>跟随轴心。</summary>
    private Transform followPivot;
    // 位置
    /// <summary>基础轴心位置。</summary>
    private Vector3 basePivotPos; // SmoothDamp 后的基础位置
    /// <summary>掉落摄像机阶段。</summary>
    private DropCameraPhase dropCameraPhase;
    /// <summary>掉落摄像机权重。</summary>
    private float dropCameraWeight;
    /// <summary>掉落冲击已用。</summary>
    private float dropImpactElapsed;
    /// <summary>掉落冲击强度。</summary>
    private float dropImpactStrength;
    /// <summary>掉落冲击脉冲。</summary>
    private float dropImpactPulse;
    /// <summary>轴心速度。</summary>
    private Vector3 pivotVel; // SmoothDamp 速度
    // 旋转
    /// <summary>偏航。</summary>
    private float yaw;
    /// <summary>俯仰。</summary>
    private float pitch;
    private float passageCameraWeight;
    private float aimPushWeight;
    private Vector3 gateFollowOffset;
    private bool gateFollowActive;
    private WindowTraversalPoint activeTraversalWindow;
    private float exitEaseRemaining;
    private float currentCollisionCompression;
    private bool wasInPassage;
    private float enemyProximityHoldUntil;
    private float smoothedFramingPitchOffset;
    private float framingPitchOffsetVelocity;
    private bool framingPitchInitialized;
    private float externalHandoffSmoothPitchTimer;
    private bool hasOverrideCollisionFocus;
    private Vector3 overrideCollisionFocusRoot;
    private bool externalVillageCameraSampling;

    internal void SetExternalVillageCameraSampling(bool active)
    {
        externalVillageCameraSampling = active;
    }
    internal float PassageCameraWeight => passageCameraWeight;
    internal WindowTraversalPoint ActiveTraversalWindow => activeTraversalWindow;
    internal float ExitEaseWeight
    {
        get
        {
            if (exitEaseDuration <= 0.001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(exitEaseRemaining / exitEaseDuration);
        }
    }
    internal float CollisionCompressionRatio => currentCollisionCompression;
    internal float AimShoulderLockWeight
    {
        get { return ReadShoulderModeWeight(); }
    }
    internal Transform ControllerTransform
    {
        get
        {
            if (controller != null)
            {
                return controller.transform;
            }

            return null;
        }
    }
    // 转向延迟
    /// <summary>转向滞后计时器。</summary>
    private float turnLagTimer;
    /// <summary>上一次目标偏航。</summary>
    private float lastTargetYaw;
    /// <summary>玩家控制、动作与运动使用的 DropCameraPhase 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    private enum DropCameraPhase
    {
        /// <summary>中文含义：无（未选择或尚未发生）。</summary>
        None,
        /// <summary>中文含义：下落中。</summary>
        Falling,
        /// <summary>中文含义：冲击。</summary>
        Impact,
        /// <summary>中文含义：恢复中。</summary>
        Recovering
    }

    /// <summary>玩家控制、动作与运动的结构化数据 StateParams；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    private struct StateParams
    {
        /// <summary>视野角度。</summary>
        public float fov;
        /// <summary>摄像机距离。</summary>
        public float cameraDistance;
        /// <summary>竖直手臂。</summary>
        public float verticalArm;
        /// <summary>肩部横向。</summary>
        public float shoulderX;
        /// <summary>阻尼。</summary>
        public Vector3 damping;
    }

    /// <summary>状态集合。</summary>
    private StateParams[] states;
    // 当前插值后的参数（供其他模块使用）
    /// <summary>当前距离。</summary>
    private float curDistance;
    /// <summary>当前竖直手臂。</summary>
    private float curVerticalArm;
    /// <summary>当前肩部横向。</summary>
    private float curShoulderX;
    /// <summary>当前视野角度。</summary>
    private float curFOV;
    /// <summary>当前阻尼。</summary>
    private Vector3 curDamping = new Vector3(0.1f, 0.1f, 0.1f);
    // 场景级摄像机系统
    /// <summary>是否场景绑定已配置。</summary>
    private bool sceneRigConfigured;
    /// <summary>是否输入已抑制。</summary>
    private bool inputSuppressed;
    /// <summary>生效配置档。</summary>
    private CameraProfile activeProfile;
    // 震动
    /// <summary>晃动计时器。</summary>
    private float shakeTimer;
    /// <summary>晃动起始持续时间。</summary>
    private float shakeStartDuration;
    /// <summary>当前晃动弹匣。</summary>
    private float currentShakeMag;
    // 剧情锁定
    /// <summary>是否锁定。</summary>
    private bool isLocked;
    /// <summary>锁定位置。</summary>
    private Vector3 lockedPosition;
    /// <summary>锁定旋转。</summary>
    private Quaternion lockedRotation;
    /// <summary>锁定视野角度。</summary>
    private float lockedFOV;
    // 待机
    /// <summary>待机计时器。</summary>
    private float idleTimer;
    /// <summary>呼吸阶段。</summary>
    private float breathPhase;
    /// <summary>之前是否待机。</summary>
    private float idleBreathWeight;
    private float idleBreathVelocity;
    /// <summary>瞄准呼吸阶段。</summary>
    private float aimBreathPhase;
    /// <summary>瞄准呼吸权重。</summary>
    private float aimBreathWeight;
    // 开火后坐会临时叠加到 PanTilt，随后快速回中。
    /// <summary>武器后坐力俯仰偏移量。</summary>
    private float weaponRecoilPitchOffset;
    private float visibleWeaponRecoilPitch;
    private float pistolRetainedPitch;
    private float pistolRecoilHoldUntil;
    private bool w870RecoilActive;
    [SerializeField, Range(0f, 1f), Tooltip("站定手枪点射时，每发上扬保留至下一发的比例。")]
    private float pistolRecoilRetention = .55f;
    [SerializeField, Min(0f), Tooltip("最后一发后保留上扬角的时间（秒）。")]
    private float pistolRecoilHoldTime = .65f;
    /// <summary>武器后坐力偏航偏移量。</summary>
    private float weaponRecoilYawOffset;
    /// <summary>武器后坐力视野角度偏移量。</summary>
    private float weaponRecoilFOVOffset;
    /// <summary>武器后坐力距离偏移量。</summary>
    private float weaponRecoilDistanceOffset;
    /// <summary>武器后坐力肩部偏移量。</summary>
    private float weaponRecoilShoulderOffset;
    /// <summary>蹲伏摄像机权重。</summary>
    private float crouchCameraWeight;
    /// <summary>俯仰高度偏移量。</summary>
    private float pitchHeightOffset;
    /// <summary>俯仰高度速度。</summary>
    private float pitchHeightVelocity;
    /// <summary>武器后坐力俯仰速度。</summary>
    private float weaponRecoilPitchVelocity;
    /// <summary>武器后坐力偏航速度。</summary>
    private float weaponRecoilYawVelocity;
    /// <summary>武器后坐力视野角度速度。</summary>
    private float weaponRecoilFOVVelocity;
    /// <summary>武器后坐力距离速度。</summary>
    private float weaponRecoilDistanceVelocity;
    /// <summary>武器后坐力肩部速度。</summary>
    private float weaponRecoilShoulderVelocity;
    /// <summary>当前武器后坐力返回时间。</summary>
    private float currentWeaponRecoilReturnTime;
    /// <summary>上一次有限失败帧。</summary>
    private int lastFiniteFailureFrame = -1;
    /// <summary>下一次有限警告时间。</summary>
    private float nextFiniteWarningTime;
    // 解锁回弹
    /// <summary>解锁增强。</summary>
    private float unlockBoost;
    // 近战推近
    /// <summary>近战推开计时器。</summary>
    private float meleePushTimer;
    /// <summary>近战推开比例。</summary>
    private float meleePushRatio;
    /// <summary>之前是否近战。</summary>
    private bool wasMelee;
    // 敌人拉远
    /// <summary>当前敌人缩放视野。</summary>
    private float currentEnemyZoom = 1f;
    /// <summary>敌人帧计数器。</summary>
    private int enemyFrameCounter;
    /// <summary>是否具有附近敌人。</summary>
    private bool hasNearbyEnemy;
    /// <summary>摄像机冲刺是否生效。</summary>
    private bool cameraSprintActive;
    // ========================== 公共 API ==========================
    /// <summary>震屏（兼容旧接口：duration, magnitude）</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.BindPlayer(LeonController, Transform)、RE4CameraController.Start()；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerController">玩家控制器。</param>
    /// <param name="cameraAnchor">摄像机锚点。</param>
    /// <param name="mainCamera">主摄像机。</param>
    /// <param name="gameplayCamera">玩法摄像机。</param>
    /// <param name="thirdPersonFollow">第三人称跟随。</param>
    /// <param name="cameraPanTilt">摄像机水平转动竖直转动。</param>
    /// <param name="cameraDeoccluder">摄像机遮挡解除器。</param>
    /// <param name="cameraFollowTarget">摄像机跟随目标。</param>
    public void ConfigureSceneRig(LeonController playerController, Transform cameraAnchor, Camera mainCamera, CinemachineCamera gameplayCamera, CinemachineThirdPersonFollow thirdPersonFollow, CinemachinePanTilt cameraPanTilt, CinemachineDeoccluder cameraDeoccluder, Transform cameraFollowTarget)
    {
        controller = playerController;
        if (playerController != null)
        {
            anim = playerController.GetComponent<Animator>();
        }
        else
        {
            anim = null;
        }

        target = cameraAnchor;
        cam = mainCamera;
        vcam = gameplayCamera;
        tpf = thirdPersonFollow;
        panTilt = cameraPanTilt;
        deoccluder = cameraDeoccluder;
        followPivot = cameraFollowTarget;
        sceneRigConfigured = true;
    }

    /// <summary>ApplyProfile 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、RE4RThirdPersonCamera.BuildStates()、RE4RThirdPersonCamera.CaptureCurrentStateParams()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.ApplyWeaponProfile(WeaponType)；下游（含内部回调体） Mathf.Max(float, float)、RE4RThirdPersonCamera.BuildStates()、RE4RThirdPersonCamera.CaptureCurrentStateParams()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="profile">配置档。</param>
    public void ApplyProfile(CameraProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        // 是否应当混合来源当前。
bool shouldBlendFromCurrent = states != null && Application.isPlaying && sceneRigConfigured;
        activeProfile = profile;
        normalOffset = profile.explorePose.offset;
        aimOffset = profile.aimPose.offset;
        sprintOffset = profile.sprintPose.offset;
        normalFOV = profile.explorePose.fieldOfView;
        aimFOV = profile.aimPose.fieldOfView;
        sprintFOV = profile.sprintPose.fieldOfView;
        paramBlendSpeed = Mathf.Max(0.01f, profile.blendSpeed);
        if (states != null)
        {
            BuildStates();
            if (!shouldBlendFromCurrent)
            {
                ApplyStateParams(states[ResolveCameraStateIndex()]);
            }
        }
    }

    /// <summary>SetInputSuppressed 的业务处理入口；本方法直接依赖 LeonController.ResetFreeView()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.ClearDropCameraImmediate()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.ReleaseExternalCamera(Object)、RE4CameraController.RequestExternalCamera(Object, CinemachineCamera, bool)；下游（含内部回调体） LeonController.ResetFreeView()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.ClearDropCameraImmediate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">是否值。</param>
    public void SetInputSuppressed(bool value)
    {
        inputSuppressed = value;
        if (value)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue3 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.ResetFreeView();
            }

            ClearDropCameraImmediate();
        }
    }

    /// <summary>BeginDropCamera 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.BeginDrop()、LeonWindowTraversal.BeginAutomaticDrop()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginDropCamera()
    {
        if (isLocked || inputSuppressed)
        {
            return;
        }

        dropCameraPhase = DropCameraPhase.Falling;
        dropImpactElapsed = 0f;
        dropImpactStrength = 0f;
        dropImpactPulse = 0f;
    }

    /// <summary>NotifyDropImpact 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.UpdateActiveDrop()、LeonWindowTraversal.CommitDropImpact()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.InverseLerp(float, float, float)、Mathf.Max(float, float)、RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="downwardSpeed">向下速度。</param>
    public void NotifyDropImpact(float downwardSpeed)
    {
        if (dropCameraPhase == DropCameraPhase.None)
        {
            return;
        }

        if (!IsFinite(downwardSpeed))
        {
            LogRecoveredFiniteWarning("落地冲击输入", "下落速度");
            downwardSpeed = 0f;
        }

        downwardSpeed = Mathf.Max(0f, downwardSpeed);
        dropImpactStrength = Mathf.Clamp01(Mathf.InverseLerp(minimumDropImpactSpeed, Mathf.Max(minimumDropImpactSpeed + 0.1f, maximumDropImpactSpeed), downwardSpeed));
        dropImpactElapsed = 0f;
        dropImpactPulse = 0f;
        dropCameraPhase = DropCameraPhase.Impact;
    }

    /// <summary>EndDropCamera 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonHighDropLanding.FinishDrop()、LeonHighDropLanding.HandleTraversalCompleted(AnimationActionTransaction)、LeonWindowTraversal.FinishTraversal(bool)、LeonWindowTraversal.HandleTraversalCompleted(AnimationActionTransaction)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndDropCamera()
    {
        if (dropCameraPhase == DropCameraPhase.None)
        {
            return;
        }

        if (dropCameraPhase != DropCameraPhase.Impact)
        {
            dropCameraPhase = DropCameraPhase.Recovering;
        }
    }

    /// <summary>ClearDropCameraImmediate 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LockCamera(Vector3, Quaternion, float)、RE4RThirdPersonCamera.RecoverFiniteCameraState()、RE4RThirdPersonCamera.SetInputSuppressed(bool)、RE4RThirdPersonCamera.UpdateDropCamera(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearDropCameraImmediate()
    {
        dropCameraPhase = DropCameraPhase.None;
        dropCameraWeight = 0f;
        dropImpactElapsed = 0f;
        dropImpactStrength = 0f;
        dropImpactPulse = 0f;
    }

    /// <summary>翻窗或通道动作进入时记录当前穿越窗口，避免窗框与碎玻璃触发近距避障闪烁。</summary>
    /// <param name="window">中文含义：当前穿越的窗口点。</param>
    public void BeginWindowTraversalCamera(WindowTraversalPoint window)
    {
        activeTraversalWindow = window;
    }

    /// <summary>翻窗或通道动作退出时释放窗口上下文，并触发窄到宽的出口舒展期。</summary>
    /// <param name="window">中文含义：结束穿越的窗口点。</param>
    /// <param name="triggerExitEase">中文含义：是否开启出口舒展过渡。</param>
    public void EndWindowTraversalCamera(WindowTraversalPoint window, bool triggerExitEase = true)
    {
        if (window == null || activeTraversalWindow == window)
        {
            activeTraversalWindow = null;
        }

        if (triggerExitEase && exitEaseDuration > 0.001f)
        {
            exitEaseRemaining = Mathf.Max(exitEaseRemaining, exitEaseDuration);
        }
    }

    internal void SetOverrideCollisionFocusRoot(bool enabled, Vector3 rootPosition)
    {
        hasOverrideCollisionFocus = enabled;
        overrideCollisionFocusRoot = rootPosition;
    }

    internal Vector3 ReadStableCollisionFocusPosition(Vector3 up)
    {
        Vector3 safeUp;
        if (up.sqrMagnitude > 0.001f)
        {
            safeUp = up.normalized;
        }
        else
        {
            safeUp = Vector3.up;
        }

        float torsoHeight = Mathf.Lerp(1.36f, 1.04f, crouchCameraWeight);
        if (hasOverrideCollisionFocus)
        {
            return overrideCollisionFocusRoot + safeUp * torsoHeight;
        }

        if ((inputSuppressed || isLocked || gateFollowActive) && followPivot != null)
        {
            return followPivot.position;
        }

        if (controller != null)
        {
            return controller.transform.position + nativeAnchorDelta + safeUp * torsoHeight;
        }

        if (target != null)
        {
            return target.position + safeUp * torsoHeight;
        }

        if (followPivot != null)
        {
            return followPivot.position;
        }

        return transform.position + safeUp * torsoHeight;
    }

    internal void SetCollisionCompressionRatio(float ratio)
    {
        if (IsFinite(ratio))
        {
            currentCollisionCompression = Mathf.Clamp01(ratio);
        }
    }

    internal CameraCollisionSolver.SolveSettings ReadCollisionSolverSettings()
    {
        CameraCollisionSolver.SolveSettings settings = CameraCollisionSolver.SolveSettings.CreateDefault();
        settings.releaseDelay = Mathf.Clamp(collisionReleaseDelay, 0f, 0.25f);
        settings.normalRecoveryDamping = Mathf.Max(0.05f, collisionDamping);
        settings.exitEaseRecoveryDamping = Mathf.Max(settings.normalRecoveryDamping, exitEaseDuration * 0.58f);
        settings.maxExitRecoverySpeed = Mathf.Max(1.0f, exitMaxRecoverySpeed);
        settings.ceilingDropLimit = Mathf.Max(0.05f, ceilingDropLimit);
        return settings;
    }

    /// <summary>UpdateDropCamera 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.MoveTowards(float, float, float)、Mathf.Sin(float)、RE4RThirdPersonCamera.ClearDropCameraImmediate()、RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)、RE4RThirdPersonCamera.SanitizePositive(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.MoveTowards(float, float, float)、Mathf.Sin(float)、RE4RThirdPersonCamera.ClearDropCameraImmediate()、RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)、RE4RThirdPersonCamera.SanitizePositive(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    private void UpdateDropCamera(float dt)
    {
        if (!IsFinite(dropCameraWeight) || !IsFinite(dropImpactElapsed) || !IsFinite(dropImpactStrength) || !IsFinite(dropImpactPulse))
        {
            LogRecoveredFiniteWarning("坠落镜头更新前", "坠落镜头运行状态");
            ClearDropCameraImmediate();
            return;
        }

        switch (dropCameraPhase)
        {
            case DropCameraPhase.Falling:
                dropCameraWeight = Mathf.MoveTowards(dropCameraWeight, 1f, dt / 0.18f);
                dropImpactPulse = 0f;
                break;
            case DropCameraPhase.Impact:
                dropCameraWeight = 1f;
                dropImpactElapsed += dt;
                // 安全冲击持续时间。
float safeImpactDuration = SanitizePositive(dropImpactDuration, 0.12f);
                // 冲击进度。
float impactProgress = Mathf.Clamp01(dropImpactElapsed / safeImpactDuration);
                dropImpactPulse = Mathf.Sin(impactProgress * Mathf.PI);
                if (impactProgress >= 1f)
                {
                    dropImpactPulse = 0f;
                    dropCameraPhase = DropCameraPhase.Recovering;
                }

                break;
            case DropCameraPhase.Recovering:
                dropImpactPulse = 0f;
                // 安全恢复持续时间。
float safeRecoveryDuration = SanitizePositive(dropRecoveryDuration, 0.35f);
                dropCameraWeight = Mathf.MoveTowards(dropCameraWeight, 0f, dt / safeRecoveryDuration);
                if (dropCameraWeight <= 0.001f)
                {
                    ClearDropCameraImmediate();
                }

                break;
            default:
                dropCameraWeight = 0f;
                dropImpactPulse = 0f;
                break;
        }
    }

    /// <summary>从玩家正在看到的方向接管，保留跟随位置和碰撞状态。</summary>
    /// <remarks>静态调用链：上游 LeonController.InterruptFreeViewRecentering()、LeonController.SetFreeLooking(bool)、LeonController.TakeOverFreeViewAim()；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.DeltaAngle(float, float)、Quaternion.Euler(float, float, float)、RE4RThirdPersonCamera.GetCharacterYaw()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="aimTakeOver">是否瞄准接管。</param>
    public void SynchronizeObserveDirection(bool aimTakeOver = false)
    {
        if (cam == null)
        {
            return;
        }

        // 角度。
Vector3 angle = cam.transform.eulerAngles;
        yaw = Mathf.DeltaAngle(0f, angle.y);
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angle.x), pitchRange.x, pitchRange.y);
        if (followPivot != null)
        {
            followPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        lastTargetYaw = GetCharacterYaw();
        turnLagTimer = 0f;
        if (aimTakeOver)
        {
            aimTakeOverFrame = Time.frameCount;
        }
    }

    /// <summary>SynchronizeFromCamera 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.Repeat(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.ReleaseExternalCamera(Object)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Repeat(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SynchronizeFromCamera()
    {
        if (cam == null)
        {
            return;
        }

        // 欧拉。
Vector3 euler = cam.transform.eulerAngles;
        yaw = Mathf.Repeat(euler.y + 180f, 360f) - 180f;
        // 摄像机俯仰。
float cameraPitch = euler.x;
        if (cameraPitch > 180f)
        {
            cameraPitch -= 360f;
        }

        pitch = Mathf.Clamp(cameraPitch, pitchRange.x, pitchRange.y);
        pivotVel = Vector3.zero;
        unlockBoost = 0f;
    }

    /// <summary>SetCollisionMask 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.ApplyCollisionSettings()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.Start()；下游（含内部回调体） RE4RThirdPersonCamera.ApplyCollisionSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="mask">遮罩。</param>
    public void SetCollisionMask(LayerMask mask)
    {
        collisionMask = mask;
        ApplyCollisionSettings();
    }

    /// <summary>Shake 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.CameraShake(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.Shake(float, float)；下游（含内部回调体） RE4RThirdPersonCamera.CameraShake(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="duration">持续时间。</param>
    /// <param name="magnitude">长度。</param>
    public void Shake(float duration, float magnitude) => CameraShake(magnitude, duration);
    /// <summary>震屏公共方法 — 外部调用</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.CameraShake(float, float)、RE4RThirdPersonCamera.Shake(float, float)、RE4RThirdPersonCamera.TriggerMeleePush()、RE4RThirdPersonCamera.TriggerWeaponRecoil(float, float, float, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="intensity">强度。</param>
    /// <param name="duration">持续时间。</param>
    public void CameraShake(float intensity, float duration)
    {
        if (duration <= 0f)
        {
            duration = defaultShakeDuration;
        }

        if (shakeTimer <= 0f || intensity >= currentShakeMag)
        {
            shakeTimer = duration;
            shakeStartDuration = duration;
            currentShakeMag = intensity;
        }
    }

    /// <summary>触发武器开火后坐反馈</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.AddWeaponRecoil(float)；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Random.Range(float, float)、RE4RThirdPersonCamera.CameraShake(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="recoilAmount">后坐力大小。</param>
    /// <param name="shakeMultiplier">晃动倍率。</param>
    /// <param name="accumulatedAngleMaximum">累计角度最大。</param>
    /// <param name="recenteringTime">回正时间。</param>
    public void TriggerWeaponRecoil(float recoilAmount, float shakeMultiplier = 1f, float accumulatedAngleMaximum = 0f, float recenteringTime = 0f)
    {
        float amount = Mathf.Max(0f, recoilAmount);
        if (amount <= 0f)
        {
            return;
        }

        float heavyWeight = Mathf.InverseLerp(heavyRecoilThreshold, heavyRecoilThreshold * 1.8f, amount);
        float maxPitch = Mathf.Lerp(weaponRecoilMaximumPitch, heavyRecoilMaximumPitch, heavyWeight);
        float pitchScale = Mathf.Lerp(weaponRecoilPitchScale, weaponRecoilPitchScale * 1.12f, heavyWeight);
        float pitchKick = Mathf.Clamp(amount * pitchScale, weaponRecoilMinimumPitch, maxPitch);
        float accumulatedLimit = accumulatedAngleMaximum;
        if (accumulatedLimit <= 0f)
        {
            accumulatedLimit = maxPitch * 1.5f;
        }

        bool aiming = IsAiming;
        w870RecoilActive = aiming && controller != null && controller.currentWeapon == WeaponType.Shotgun
            && controller.IsGrounded && !controller.IsCrouching && controller.MoveInput.sqrMagnitude <= .0025f
            && controller.AnimDriver != null && controller.AnimDriver.SupportsNativeW870Aim
            && activeProfile != null && activeProfile.nativeShoulder != null && activeProfile.nativeShoulder.HasShotPumpRise;
        if (w870RecoilActive) return;
        bool stationaryPistol = aiming && controller != null && controller.currentWeapon == WeaponType.Pistol
            && controller.IsGrounded && !controller.IsCrouching && controller.MoveInput.sqrMagnitude <= .0025f;
        if (stationaryPistol)
        {
            // 连点在同一个俯仰偏移上累积，射停后再回中；实际射线取渲染相机的瞄点。
            pistolRetainedPitch = Mathf.Clamp(weaponRecoilPitchOffset - pitchKick * pistolRecoilRetention, -accumulatedLimit, 0f);
            pistolRecoilHoldUntil = Time.time + pistolRecoilHoldTime;
        }
        else if (aiming)
        {
            // 移动中保留较小的抬镜幅度，枪械动作仍反馈每一发射击。
            float movement = GetMoveMagnitude();
            if (controller != null)
            {
                movement = Mathf.Max(movement, controller.CurrentPlanarSpeed / 1.2f);
            }
            float kickScale = Mathf.Lerp(.52f, .34f, Mathf.Clamp01(movement));
            pitchKick *= kickScale;
            accumulatedLimit = Mathf.Min(1.45f, accumulatedLimit * kickScale);
        }
        else
        {
            float yawKick = amount * Mathf.Lerp(weaponRecoilYawScale, weaponRecoilYawScale * 1.1f, heavyWeight);
            weaponRecoilYawOffset = Mathf.Clamp(weaponRecoilYawOffset + Random.Range(-yawKick, yawKick), -maxPitch, maxPitch);
            if (heavyWeight > 0f)
            {
                float fovKick = Mathf.Min(heavyRecoilMaximumFOV, amount * heavyRecoilFOVScale * heavyWeight);
                float distanceKick = Mathf.Min(heavyRecoilMaximumDistance, amount * heavyRecoilDistanceScale * heavyWeight);
                float shoulderKick = Mathf.Min(heavyRecoilMaximumShoulder, amount * heavyRecoilShoulderScale * heavyWeight);
                if (curShoulderX < 0f)
                {
                    shoulderKick = -shoulderKick;
                }

                weaponRecoilFOVOffset = Mathf.Max(weaponRecoilFOVOffset, fovKick);
                weaponRecoilDistanceOffset = Mathf.Max(weaponRecoilDistanceOffset, distanceKick);
                weaponRecoilShoulderOffset = Mathf.Clamp(weaponRecoilShoulderOffset + shoulderKick,
                    -heavyRecoilMaximumShoulder, heavyRecoilMaximumShoulder);
            }

            float shakeScale = Mathf.Lerp(weaponRecoilShakeScale, weaponRecoilShakeScale * 1.1f, heavyWeight);
            float shakeIntensity = Mathf.Min(weaponRecoilMaximumShake, amount * shakeScale);
            CameraShake(shakeIntensity * Mathf.Max(0f, shakeMultiplier), weaponRecoilShakeDuration + heavyWeight * .08f);
        }

        weaponRecoilPitchOffset = Mathf.Clamp(weaponRecoilPitchOffset - pitchKick, -accumulatedLimit, maxPitch);
        weaponRecoilPitchVelocity = 0f;
        weaponRecoilYawVelocity = 0f;
        weaponRecoilFOVVelocity = 0f;
        weaponRecoilDistanceVelocity = 0f;
        weaponRecoilShoulderVelocity = 0f;
        currentWeaponRecoilReturnTime = Mathf.Lerp(weaponRecoilReturnTime, heavyRecoilReturnTime, heavyWeight);
        if (recenteringTime > 0f)
        {
            currentWeaponRecoilReturnTime = recenteringTime;
        }
    }

    /// <summary>触发近战镜头微推</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.TriggerMeleePush()、RE4RThirdPersonCamera.UpdateMeleePush(float)；下游（含内部回调体） RE4RThirdPersonCamera.CameraShake(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TriggerMeleePush()
    {
        meleePushTimer = meleePushDuration + meleeRecoverDuration;
        CameraShake(meleeShakeIntensity, meleePushDuration);
    }

    /// <summary>锁定相机到指定位置/旋转（剧情用）</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LockCamera(Transform, float)；下游（含内部回调体） Component.GetComponent&lt;CinemachineBrain&gt;()、Component.GetComponent&lt;RE4CameraController&gt;()、LeonController.ResetFreeView()、object.ReferenceEquals(object, object)、RE4CameraController.LockCamera(Vector3, Quaternion, float)、RE4RThirdPersonCamera.ClearDropCameraImmediate()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">位置。</param>
    /// <param name="rotation">旋转。</param>
    /// <param name="fov">视野角度。</param>
    public void LockCamera(Vector3 position, Quaternion rotation, float fov = 55f)
    {
        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue7 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.ResetFreeView();
        }

        ClearDropCameraImmediate();
        // 场景控制器。
var sceneController = GetComponent<RE4CameraController>();
        if (sceneController != null)
        {
            sceneController.LockCamera(position, rotation, fov);
            return;
        }

        isLocked = true;
        lockedPosition = position;
        lockedRotation = rotation;
        lockedFOV = fov;
        if (vcam != null)
        {
            vcam.enabled = false;
        }

        // 镜头脑。
global::Unity.Cinemachine.CinemachineBrain brain;
        if (cam != null)
        {
            brain = cam.GetComponent<CinemachineBrain>();
        }
        else
        {
            brain = null;
        }

        if (brain != null)
        {
            brain.enabled = false;
        }
    }

    /// <summary>锁定相机到指定 Transform</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） RE4RThirdPersonCamera.LockCamera(Vector3, Quaternion, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="lockTransform">锁定变换。</param>
    /// <param name="fov">视野角度。</param>
    public void LockCamera(Transform lockTransform, float fov = 55f) => LockCamera(lockTransform.position, lockTransform.rotation, fov);
    /// <summary>解除锁定，平滑回弹至跟随状态</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;CinemachineBrain&gt;()、Component.GetComponent&lt;RE4CameraController&gt;()、Mathf.Clamp(float, float, float)、Mathf.Repeat(float, float)、Quaternion.Euler(float, float, float)、RE4CameraController.UnlockCamera()、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void UnlockCamera()
    {
        // 场景控制器。
var sceneController = GetComponent<RE4CameraController>();
        if (sceneController != null)
        {
            sceneController.UnlockCamera();
            return;
        }

        if (!isLocked)
        {
            return;
        }

        isLocked = false;
        unlockBoost = 1f;
        if (vcam != null)
        {
            vcam.enabled = true;
        }

        // 镜头脑。
global::Unity.Cinemachine.CinemachineBrain brain;
        if (cam != null)
        {
            brain = cam.GetComponent<CinemachineBrain>();
        }
        else
        {
            brain = null;
        }

        if (brain != null)
        {
            brain.enabled = true;
        }

        // 从当前相机位置恢复，避免跳变
        // 摄像机的变换的欧拉角度集合。
Vector3 e = cam.transform.eulerAngles;
        yaw = Mathf.Repeat(e.y + 180f, 360f) - 180f;
        // 镜头的俯仰角。
float p = e.x;
        if (p > 180f)
        {
            p -= 360f;
        }

        pitch = Mathf.Clamp(p, pitchRange.x, pitchRange.y);
        // 近似还原 FollowPivot 位置，让 VCam 从当前相机位置开始过渡
        // 偏航旋转。
Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
        // 偏移量。
Vector3 offset = yawRot * new Vector3(curShoulderX, curVerticalArm, -curDistance);
        basePivotPos = cam.transform.position - offset;
        SnapShoulderPitch();
        pivotVel = Vector3.zero;
    }

    /// <summary>中文含义：是否锁定。</summary>
    public bool IsLocked => isLocked;
    /// <summary>中文含义：距离缩放。</summary>
    public float DistanceScale => 1f;
    /// <summary>水平观察灵敏度。</summary>
    public float HorizontalLookSensitivity => horizontalSensitivity;
    /// <summary>竖直观察灵敏度。</summary>
    public float VerticalLookSensitivity => verticalSensitivity;
    /// <summary>瞄准观察灵敏度。</summary>
    public float AimLookSensitivity => aimSensitivity;
    /// <summary>中文含义：是否竖直观察反转后的。</summary>
    public bool IsVerticalLookInverted => invertY;

    /// <summary>
    /// 立即应用纵轴反转设置，避免设置界面只保存选项却不更新当前镜头。
    /// </summary>
    /// <remarks>静态调用链：上游 RE4CameraController.SetInvertY(bool, bool)、RE4RThirdPersonCamera.ReloadInvertY()；下游（含内部回调体） PlayerPrefs.Save()、PlayerPrefs.SetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="inverted">是否反转后的。</param>
    /// <param name="saveToPreferences">是否保存到用户偏好。</param>
    public void SetInvertY(bool inverted, bool saveToPreferences = true)
    {
        invertY = inverted;
        if (!saveToPreferences)
        {
            return;
        }

        // 按原顺序缓存的实参文字“InvertY”。
string argumentValue11 = "InvertY";
        // 分支选出的1。
int selectedValue10;
        if (inverted)
        {
            selectedValue10 = 1;
        }
        else
        {
            selectedValue10 = 0;
        }

        PlayerPrefs.SetInt(argumentValue11, selectedValue10);
        PlayerPrefs.Save();
    }

    /// <summary>ReloadInvertY 的业务处理入口；本方法直接依赖 PlayerPrefs.GetInt(string, int)、RE4RThirdPersonCamera.SetInvertY(bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.ReloadInvertY()、RE4RThirdPersonCamera.Start()；下游（含内部回调体） PlayerPrefs.GetInt(string, int)、RE4RThirdPersonCamera.SetInvertY(bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReloadInvertY()
    {
        // 按原顺序缓存的实参文字“InvertY”。
string argumentValue13 = "InvertY";
        // 分支选出的1。
int selectedValue12;
        if (invertY)
        {
            selectedValue12 = 1;
        }
        else
        {
            selectedValue12 = 0;
        }

        SetInvertY(PlayerPrefs.GetInt(argumentValue13, selectedValue12) == 1, false);
    }

    /// <summary>
    /// 同时设置横向、纵向与瞄准灵敏度，供设置界面统一提交。
    /// </summary>
    /// <remarks>静态调用链：上游 RE4CameraController.SetLookSensitivity(float, float, float, bool)、RE4RThirdPersonCamera.ReloadLookSensitivity()、RE4RThirdPersonCamera.SetAimLookSensitivity(float, bool)、RE4RThirdPersonCamera.SetHorizontalLookSensitivity(float, bool)、RE4RThirdPersonCamera.SetVerticalLookSensitivity(float, bool)；下游（含内部回调体） Mathf.Clamp(float, float, float)、PlayerPrefs.Save()、PlayerPrefs.SetFloat(string, float)、RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="horizontal">水平。</param>
    /// <param name="vertical">竖直。</param>
    /// <param name="aim">瞄准。</param>
    /// <param name="saveToPreferences">是否保存到用户偏好。</param>
    public void SetLookSensitivity(float horizontal, float vertical, float aim, bool saveToPreferences = true)
    {
        if (!IsFinite(horizontal))
        {
            horizontal = DefaultHorizontalSensitivity;
        }

        if (!IsFinite(vertical))
        {
            vertical = DefaultVerticalSensitivity;
        }

        if (!IsFinite(aim))
        {
            aim = DefaultAimSensitivity;
        }

        horizontalSensitivity = Mathf.Clamp(horizontal, MinimumLookSensitivity, MaximumLookSensitivity);
        verticalSensitivity = Mathf.Clamp(vertical, MinimumLookSensitivity, MaximumLookSensitivity);
        aimSensitivity = Mathf.Clamp(aim, MinimumLookSensitivity, MaximumAimSensitivity);
        if (!saveToPreferences)
        {
            return;
        }

        PlayerPrefs.SetFloat(HorizontalSensitivityPreferenceKey, horizontalSensitivity);
        PlayerPrefs.SetFloat(VerticalSensitivityPreferenceKey, verticalSensitivity);
        PlayerPrefs.SetFloat(AimSensitivityPreferenceKey, aimSensitivity);
        PlayerPrefs.Save();
    }

    /// <summary>SetHorizontalLookSensitivity 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.SetHorizontalLookSensitivity(float, bool)；下游（含内部回调体） RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="saveToPreferences">是否保存到用户偏好。</param>
    public void SetHorizontalLookSensitivity(float value, bool saveToPreferences = true)
    {
        SetLookSensitivity(value, verticalSensitivity, aimSensitivity, saveToPreferences);
    }

    /// <summary>SetVerticalLookSensitivity 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.SetVerticalLookSensitivity(float, bool)；下游（含内部回调体） RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="saveToPreferences">是否保存到用户偏好。</param>
    public void SetVerticalLookSensitivity(float value, bool saveToPreferences = true)
    {
        SetLookSensitivity(horizontalSensitivity, value, aimSensitivity, saveToPreferences);
    }

    /// <summary>SetAimLookSensitivity 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.SetAimLookSensitivity(float, bool)；下游（含内部回调体） RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="saveToPreferences">是否保存到用户偏好。</param>
    public void SetAimLookSensitivity(float value, bool saveToPreferences = true)
    {
        SetLookSensitivity(horizontalSensitivity, verticalSensitivity, value, saveToPreferences);
    }

    /// <summary>ReloadLookSensitivity 的业务处理入口；本方法直接依赖 PlayerPrefs.GetFloat(string, float)、RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4CameraController.ReloadLookSensitivity()、RE4RThirdPersonCamera.Start()；下游（含内部回调体） PlayerPrefs.GetFloat(string, float)、RE4RThirdPersonCamera.SetLookSensitivity(float, float, float, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReloadLookSensitivity()
    {
        SetLookSensitivity(PlayerPrefs.GetFloat(HorizontalSensitivityPreferenceKey, DefaultHorizontalSensitivity), PlayerPrefs.GetFloat(VerticalSensitivityPreferenceKey, DefaultVerticalSensitivity), PlayerPrefs.GetFloat(AimSensitivityPreferenceKey, DefaultAimSensitivity), false);
    }

    // ========================== 生命周期 ==========================
    /// <summary>Unity 编辑器字段验证回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnValidate()
    {
        enemyCheckFrameInterval = Mathf.Max(1, enemyCheckFrameInterval);
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object, Object)、Mathf.Clamp(float, float, float)、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.ApplyCollisionSettings()、RE4RThirdPersonCamera.ApplyLensSafetySettings()、RE4RThirdPersonCamera.ApplyPlayerRendererVisibilityGuard()、RE4RThirdPersonCamera.BuildStates() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        // 读取标题选项中的纵轴方向设置
        ReloadInvertY();
        ReloadLookSensitivity();
        if (!sceneRigConfigured)
        {
            Debug.LogError("[RE4RThirdPersonCamera] 未由场景级 RE4CameraController 配置，停止运行。", this);
            enabled = false;
            return;
        }

        if (controller == null || target == null || cam == null || vcam == null || tpf == null || panTilt == null || deoccluder == null || followPivot == null)
        {
            Debug.LogError("[RE4RThirdPersonCamera] 场景级摄像机引用不完整，停止运行。", this);
            enabled = false;
            return;
        }

        BuildStates();
        curDistance = states[0].cameraDistance;
        curVerticalArm = states[0].verticalArm;
        curShoulderX = states[0].shoulderX;
        curFOV = states[0].fov;
        curDamping = states[0].damping;
        basePivotPos = target.position;
        yaw = ResolveInitialYaw();
        pitch = Mathf.Clamp(startPitch, pitchRange.x, pitchRange.y);
        lastTargetYaw = yaw;
        if (!ValidateFiniteCameraState("初始化"))
        {
            return;
        }

        vcam.Follow = followPivot;
        vcam.LookAt = null;
        if (cam.TryGetComponent(out CinemachineBrain gameplayBrain))
        {
            // 跟随轴在 LateUpdate 写入，镜头也在同一渲染帧读取。
            gameplayBrain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
        }
        panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.TrackingTarget;
        panTilt.PanAxis.Range = new Vector2(-180f, 180f);
        panTilt.PanAxis.Wrap = true;
        panTilt.TiltAxis.Range = new Vector2(pitchRange.x, pitchRange.y);
        panTilt.TiltAxis.Wrap = false;
        ApplyCollisionSettings();
        ApplyLensSafetySettings();
        ApplyPlayerRendererVisibilityGuard();
        SnapToInitialCameraPose();
        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue14 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.HideCustomCursor();
        }
    }

    /// <summary>BuildStates 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.StateParams.StateParams()、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.ApplyProfile(CameraProfile)、RE4RThirdPersonCamera.Start()；下游（含内部回调体） RE4RThirdPersonCamera.StateParams.StateParams()、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BuildStates()
    {
        // 默认阻尼。
Vector3 defaultDamping = new Vector3(0.1f, 0.1f, 0.1f);
        states = new StateParams[3];
        // 赋值前保存的目标状态集合。
global::RE4RThirdPersonCamera.StateParams[] assignmentReceiver15 = states;
        // 赋值前保存的索引0。
int assignmentIndex16 = 0;
        // 本次新建的状态参数集合。
global::RE4RThirdPersonCamera.StateParams createdValue17 = new StateParams();
        createdValue17.fov = normalFOV;
        createdValue17.cameraDistance = -normalOffset.z;
        createdValue17.verticalArm = normalOffset.y;
        createdValue17.shoulderX = normalOffset.x;
        if (activeProfile != null)
        {
            createdValue17.damping = activeProfile.explorePose.damping;
        }
        else
        {
            createdValue17.damping = defaultDamping;
        }

        assignmentReceiver15[assignmentIndex16] = createdValue17;
        // 赋值前保存的目标状态集合。
global::RE4RThirdPersonCamera.StateParams[] assignmentReceiver19 = states;
        // 赋值前保存的索引1。
int assignmentIndex20 = 1;
        // 本次新建的状态参数集合。
global::RE4RThirdPersonCamera.StateParams createdValue21 = new StateParams();
        createdValue21.fov = aimFOV;
        createdValue21.cameraDistance = -aimOffset.z;
        createdValue21.verticalArm = aimOffset.y;
        createdValue21.shoulderX = aimOffset.x;
        if (activeProfile != null)
        {
            createdValue21.damping = activeProfile.aimPose.damping;
        }
        else
        {
            createdValue21.damping = defaultDamping;
        }

        assignmentReceiver19[assignmentIndex20] = createdValue21;
        // 赋值前保存的目标状态集合。
global::RE4RThirdPersonCamera.StateParams[] assignmentReceiver23 = states;
        // 赋值前保存的索引2。
int assignmentIndex24 = 2;
        // 本次新建的状态参数集合。
global::RE4RThirdPersonCamera.StateParams createdValue25 = new StateParams();
        createdValue25.fov = sprintFOV;
        createdValue25.cameraDistance = -sprintOffset.z;
        createdValue25.verticalArm = sprintOffset.y;
        createdValue25.shoulderX = sprintOffset.x;
        if (activeProfile != null)
        {
            createdValue25.damping = activeProfile.sprintPose.damping;
        }
        else
        {
            createdValue25.damping = defaultDamping;
        }

        assignmentReceiver23[assignmentIndex24] = createdValue25;
    }

    /// <summary>ResolveInitialYaw 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.GetCharacterYaw()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.Start()；下游（含内部回调体） RE4RThirdPersonCamera.GetCharacterYaw()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    float ResolveInitialYaw()
    {
        if (alignBehindPlayerOnStart)
        {
            return GetCharacterYaw();
        }

        if (target != null)
        {
            return target.eulerAngles.y;
        }
        else
        {
            return transform.eulerAngles.y;
        }
    }

    /// <summary>GetCharacterYaw 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)、RE4RThirdPersonCamera.ResolveInitialYaw()、RE4RThirdPersonCamera.SynchronizeObserveDirection(bool)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    float GetCharacterYaw()
    {
        if (controller != null)
        {
            return controller.transform.eulerAngles.y;
        }

        if (target != null)
        {
            return target.eulerAngles.y;
        }
        else
        {
            return transform.eulerAngles.y;
        }
    }

    /// <summary>SnapToInitialCameraPose 的业务处理入口；本方法直接依赖 Quaternion.Euler(float, float, float)、RE4RThirdPersonCamera.ApplyCameraParams()、RE4RThirdPersonCamera.UpdatePanTilt()、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.Start()；下游（含内部回调体） Quaternion.Euler(float, float, float)、RE4RThirdPersonCamera.ApplyCameraParams()、RE4RThirdPersonCamera.UpdatePanTilt()、Transform.SetPositionAndRotation(Vector3, Quaternion)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void SnapToInitialCameraPose()
    {
        if (followPivot == null || tpf == null || panTilt == null || vcam == null)
        {
            return;
        }

        basePivotPos = target.position;
        pivotVel = Vector3.zero;
        unlockBoost = 0f;
        breathPhase = 0f;
        idleTimer = 0f;
        idleBreathWeight = 0f;
        idleBreathVelocity = 0f;
        aimBreathPhase = 0f;
        aimBreathWeight = 0f;
        proceduralAimBreathWeight = 0f;
        nativeAimBreathAngles = Vector2.zero;
        weaponRecoilPitchOffset = 0f;
        visibleWeaponRecoilPitch = 0f;
        w870RecoilActive = false;
        pistolRecoilHoldUntil = 0f;
        weaponRecoilYawOffset = 0f;
        weaponRecoilFOVOffset = 0f;
        weaponRecoilDistanceOffset = 0f;
        weaponRecoilShoulderOffset = 0f;
        pitchHeightOffset = 0f;
        pitchHeightVelocity = 0f;
        weaponRecoilPitchVelocity = 0f;
        weaponRecoilYawVelocity = 0f;
        weaponRecoilFOVVelocity = 0f;
        weaponRecoilDistanceVelocity = 0f;
        weaponRecoilShoulderVelocity = 0f;
        currentWeaponRecoilReturnTime = weaponRecoilReturnTime;
        activeTraversalWindow = null;
        exitEaseRemaining = 0f;
        currentCollisionCompression = 0f;
        wasInPassage = false;
        hasOverrideCollisionFocus = false;
        overrideCollisionFocusRoot = Vector3.zero;
        framingPitchInitialized = false;
        externalHandoffSmoothPitchTimer = 0f;
        smoothedFramingPitchOffset = 0f;
        framingPitchOffsetVelocity = 0f;
        reticleClearanceOffset = Vector3.zero;
        reticleClearanceVelocity = Vector3.zero;
        AlignShoulderRig(basePivotPos);
        if (cam == null)
        {
            return;
        }

        // 摄像机偏移量。
Vector3 cameraOffset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(curShoulderX, curVerticalArm, -curDistance);
        // 摄像机旋转。
Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.SetPositionAndRotation(basePivotPos + cameraOffset, cameraRotation);
        cam.fieldOfView = curFOV;
    }

    /// <summary>ApplyCollisionSettings 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;RE4StableCameraCollision&gt;()、GameObject.AddComponent&lt;RE4StableCameraCollision&gt;()、LayerMask.NameToLayer(string)、Mathf.Max(float, float)、RE4StableCameraCollision.Configure(Transform, CinemachineThirdPersonFollow, LayerMask, float, float, float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.SetCollisionMask(LayerMask)、RE4RThirdPersonCamera.Start()；下游（含内部回调体） Component.GetComponent&lt;RE4StableCameraCollision&gt;()、GameObject.AddComponent&lt;RE4StableCameraCollision&gt;()、LayerMask.NameToLayer(string)、Mathf.Max(float, float)、RE4StableCameraCollision.Configure(Transform, CinemachineThirdPersonFollow, LayerMask, float, float, float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyCollisionSettings()
    {
        if (tpf == null || deoccluder == null)
        {
            return;
        }

        // 实际生效的遮罩。
LayerMask effectiveMask = collisionMask;
        // 玩家图层。
int playerLayer = LayerMask.NameToLayer("player");
        if (playerLayer >= 0)
        {
            effectiveMask &= ~(1 << playerLayer);
        }

        // 内置检测会先把肩部支点压回角色根点，再检测镜头臂。
        // 门框或复杂网格会让第一段命中逐帧切换，导致支点高度和横向位置突变。
        // 跟随障碍设置。
var followObstacleSettings = tpf.AvoidObstacles;
        followObstacleSettings.Enabled = false;
        followObstacleSettings.CollisionFilter = effectiveMask;
        followObstacleSettings.CameraRadius = Mathf.Max(0.01f, collisionRadius);
        tpf.AvoidObstacles = followObstacleSettings;
        // 遮挡扩展的多方向寻路在门框、墙角和复杂网格上会反复切换解。
        // 关闭它的位置修正，仅由上方的镜头臂碰撞负责避障。
        // 遮挡解除器设置。
var deoccluderSettings = deoccluder.AvoidObstacles;
        deoccluderSettings.Enabled = false;
        deoccluder.AvoidObstacles = deoccluderSettings;
        // 改为从稳定的上身观察点到期望镜头位置只检测一次。
        // 进入遮挡立即收回保证不穿墙，离开遮挡延迟后平滑恢复。
        if (stableCollision == null)
        {
            stableCollision = vcam.GetComponent<RE4StableCameraCollision>();
        }

        if (stableCollision == null)
        {
            stableCollision = vcam.gameObject.AddComponent<RE4StableCameraCollision>();
        }

        float configuredRadius = Mathf.Max(0.14f, collisionRadius);
        float configuredSkin = Mathf.Max(0.03f, collisionSkin);
        stableCollision.Configure(this, followPivot, tpf, effectiveMask, configuredRadius, configuredSkin, collisionFocusHeightRatio, collisionReleaseDelay, collisionDamping, Mathf.Max(cameraDistanceFloor, minimumExternalCameraDistance));
    }

    /// <summary>ApplyLensSafetySettings 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.Start()；下游（含内部回调体） Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyLensSafetySettings()
    {
        // 近端动画片段。
float nearClip = Mathf.Clamp(gameplayNearClipPlane, 0.001f, 0.1f);
        if (cam != null)
        {
            cam.nearClipPlane = nearClip;
        }

        if (vcam == null)
        {
            return;
        }

        // 镜头。
var lens = vcam.Lens;
        lens.NearClipPlane = nearClip;
        vcam.Lens = lens;
    }

    /// <summary>ApplyPlayerRendererVisibilityGuard 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;SkinnedMeshRenderer&gt;(bool)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyPlayerRendererVisibilityGuard()
    {
        if (!protectPlayerSkinnedRenderers || controller == null)
        {
            return;
        }

        // 渲染器集合。
SkinnedMeshRenderer[] renderers = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        // 当前遍历索引。
for (int i = 0; i < renderers.Length; i++)
        {
            // 渲染器。
SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.updateWhenOffscreen = true;
            // 包围盒。
Bounds bounds = renderer.localBounds;
            // 半尺寸。
Vector3 extents = bounds.extents;
            extents.x = Mathf.Max(extents.x, minimumPlayerRendererBoundsExtents.x);
            extents.y = Mathf.Max(extents.y, minimumPlayerRendererBoundsExtents.y);
            extents.z = Mathf.Max(extents.z, minimumPlayerRendererBoundsExtents.z);
            bounds.extents = extents;
            renderer.localBounds = bounds;
        }
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ResetFreeView()、LeonController.UpdateFreeLookState()、object.ReferenceEquals(object, object)、RE4RThirdPersonCamera.ApplyCameraParams()、RE4RThirdPersonCamera.ApplyFOV(float)、RE4RThirdPersonCamera.ApplyLockedCamera(float)、RE4RThirdPersonCamera.ApplyShake(float) 等共 22 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (controller == null && target == null)
        {
            return;
        }

        if (cam == null || vcam == null)
        {
            return;
        }

        if (!ValidateFiniteCameraState("帧开始"))
        {
            return;
        }

        if (states == null)
        {
            BuildStates();
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue29 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue29, null))
        {
            receiverValue29.UpdateFreeLookState();
        }

        currentFrameIsRecentering = false;
        if (isLocked || inputSuppressed)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue30 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue30, null))
            {
                receiverValue30.ResetFreeView();
            }
        }

        // 本次计算的时间步长。
float dt = Time.deltaTime;
        if (!IsFinite(dt) || dt < 0f)
        {
            dt = 0f;
        }

        // 剧情锁定最高优先级
        if (isLocked)
        {
            ApplyLockedCamera(dt);
            return;
        }

        bool openingGate = controller != null && controller.AnimCoordinator != null
            && controller.AnimCoordinator.IsVillageGateOpening;
        bool traversing = controller != null && controller.currentState == LeonState.Traversing;
        bool inPassage = openingGate || traversing;
        if (wasInPassage && !inPassage && exitEaseDuration > 0.001f)
        {
            exitEaseRemaining = Mathf.Max(exitEaseRemaining, exitEaseDuration);
        }

        wasInPassage = inPassage;
        if (exitEaseRemaining > 0f)
        {
            exitEaseRemaining = Mathf.Max(0f, exitEaseRemaining - dt);
        }

        bool inQteCameraFreeze = false;
        if (controller != null && controller.currentState == LeonState.QTE)
        {
            inQteCameraFreeze = true;
        }

        if (externalHandoffSmoothPitchTimer > 0f && !inQteCameraFreeze)
        {
            externalHandoffSmoothPitchTimer = Mathf.Max(0f, externalHandoffSmoothPitchTimer - dt);
        }

        float passageTarget = 0f;
        if (inPassage)
        {
            passageTarget = 1f;
        }
        passageCameraWeight = Mathf.MoveTowards(passageCameraWeight, passageTarget, dt / .18f);
        if (openingGate && !gateFollowActive && target != null)
        {
            gateFollowOffset = basePivotPos - controller.transform.position;
        }
        gateFollowActive = openingGate;
        float aimTarget = 0f;
        if (IsAiming && !traversing && controller != null && controller.currentWeapon != WeaponType.Knife)
        {
            aimTarget = 1f;
        }
        aimPushWeight = Mathf.MoveTowards(aimPushWeight, aimTarget, dt / .15f);
        if (!inputSuppressed && !openingGate)
        {
            DriveInput(dt);
        }

        if (!ValidateFiniteCameraState("输入更新后"))
        {
            return;
        }

        if (externalVillageCameraSampling)
        {
            return;
        }

        UpdateCameraSprintState();
        UpdateShoulderOrbit(dt);
        UpdateStateBlend(dt);
        UpdateCrouchCamera(dt);
        UpdatePitchHeight(dt);
        UpdateMeleePush(dt);
        UpdateDropCamera(dt);
        UpdateEnemyProximity(dt);
        ApplyCameraParams();
        if (!ValidateFiniteCameraState("镜头参数应用后"))
        {
            return;
        }

        UpdateIdleBreathing(dt);
        UpdateAimBreathing(dt);
        if (controller != null && !inputSuppressed && !openingGate)
        {
            controller.ApplyKnifeCameraHeading(yaw, shoulderLookPitch);
        }
        UpdateFollowPivot(dt);
        if (!ValidateFiniteCameraState("跟随点更新后"))
        {
            return;
        }

        UpdateWeaponRecoil(dt);
        UpdatePanTilt(dt);
        ApplyShake(dt);
        ApplyFOV(dt);
        ValidateFiniteCameraState("帧结束");
    }

    // ========================== 模块 1：跟随 ==========================
    /// <summary>UpdateFollowPivot 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.PerlinNoise(float, float)、Mathf.Sin(float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)、Quaternion.Euler(float, float, float) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.PerlinNoise(float, float)、Mathf.Sin(float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)、Quaternion.Euler(float, float, float) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateFollowPivot(float dt)
    {
        // 是否正在瞄准。
bool isAiming = IsAiming;
        // 常规阻尼。
float normalDamping;
        if (isAiming)
        {
            normalDamping = aimPositionDamping;
        }
        else
        {
            normalDamping = positionDamping;
        }

        // 水平阻尼。
float horizontalDamping = Mathf.Lerp(normalDamping, dropHorizontalDamping, dropCameraWeight);
        // 倒退瞄准跨过阶沿时，只放缓根节点高度传递到越肩镜头的速度。
        // 是否后退瞄准。
bool backwardAim = isAiming && controller && controller.IsGrounded && controller.MoveInput.z < -.1f;
        // 分支选出的最大的返回结果。
float selectedValue32;
        if (backwardAim)
        {
            selectedValue32 = Mathf.Max(normalDamping, positionDamping);
        }
        else
        {
            selectedValue32 = normalDamping;
        }

        // 竖直阻尼。
float verticalDamping = Mathf.Lerp(selectedValue32, dropVerticalDamping, dropCameraWeight);
        if (UsesPistolShoulder && !nativePoseActive)
        {
            float followDamping = Mathf.Lerp(positionDamping, aimPositionDamping, pistolFramingWeight);
            horizontalDamping = Mathf.Lerp(followDamping, dropHorizontalDamping, dropCameraWeight);
            if (backwardAim)
            {
                followDamping = Mathf.Max(followDamping, positionDamping);
            }
            verticalDamping = Mathf.Lerp(followDamping, dropVerticalDamping, dropCameraWeight);
        }
        if (unlockBoost > 0f)
        {
            unlockBoost -= dt * unlockRecoverSpeed;
            horizontalDamping = Mathf.Min(horizontalDamping, 0.02f);
            verticalDamping = Mathf.Min(verticalDamping, 0.02f);
        }

        // horizontalTarget（水平目标）：使用位置：作为 Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float, float, float) 的参数。
        Vector3 anchorPosition = target.position + nativeAnchorDelta;
        if (gateFollowActive)
        {
            anchorPosition = controller.transform.position + gateFollowOffset;
        }
        Vector3 horizontalTarget = new Vector3(anchorPosition.x, basePivotPos.y, anchorPosition.z);
        // 水平速度。
Vector3 horizontalVelocity = new Vector3(pivotVel.x, 0f, pivotVel.z);
        basePivotPos = Vector3.SmoothDamp(basePivotPos, horizontalTarget, ref horizontalVelocity, horizontalDamping, Mathf.Infinity, dt);
        pivotVel.x = horizontalVelocity.x;
        pivotVel.z = horizontalVelocity.z;
        // 竖直速度。
float verticalVelocity = pivotVel.y;
        basePivotPos.y = Mathf.SmoothDamp(basePivotPos.y, anchorPosition.y, ref verticalVelocity, verticalDamping, Mathf.Infinity, dt);
        if (dropCameraWeight > 0.001f && dt > 0.0001f)
        {
            // 高差过渡时软性收敛超出最大滞后阈值的垂直偏差，避免硬截断造成镜头顿挫。
            float maximumLag = Mathf.Lerp(0.80f, 0.34f, dropCameraWeight);
            float verticalOffset = basePivotPos.y - anchorPosition.y;
            float absOffset = Mathf.Abs(verticalOffset);
            if (absOffset > maximumLag)
            {
                float excess = absOffset - maximumLag;
                float softPull = excess * (1f - Mathf.Exp(-dt * 18f));
                basePivotPos.y -= Mathf.Sign(verticalOffset) * softPull;
            }
        }
        pivotVel.y = verticalVelocity;
        // 近距避障压缩时按比例衰减微幅抖动，减轻贴墙与狭小空间下的眩晕感。
        float shakeAttenuation = 1f - Mathf.Clamp01(currentCollisionCompression * 0.85f);
        // 最终位置。
Vector3 finalPos = basePivotPos;
        finalPos.y -= dropImpactCompression * dropImpactPulse * dropImpactStrength;
        finalPos.y += Mathf.Sin(breathPhase) * idleBobAmplitude * idleBreathWeight * shakeAttenuation;

        if (proceduralAimBreathWeight > 0f)
        {
            // 瞄准上下起伏。
float aimBob = Mathf.Sin(aimBreathPhase) * aimBreathBobAmplitude * proceduralAimBreathWeight * shakeAttenuation;
            // 瞄准侧边。
float aimSide = Mathf.Sin(aimBreathPhase + Mathf.PI * 0.4f) * aimBreathBobAmplitude * 0.45f * proceduralAimBreathWeight * shakeAttenuation;
            finalPos += Vector3.up * aimBob + followPivot.right * aimSide;
        }

        if (IsSprinting && !isAiming)
        {
            // 横向缩放。
float sx = (Mathf.PerlinNoise(Time.time * sprintShakeFrequency, 0f) - 0.5f) * sprintShakeIntensity * shakeAttenuation;
            // 纵向缩放。
float sy = (Mathf.PerlinNoise(0f, Time.time * sprintShakeFrequency) - 0.5f) * sprintShakeIntensity * shakeAttenuation;
            finalPos += followPivot.right * sx + Vector3.up * sy;
        }

        followPivot.position = finalPos + Vector3.up * ReadPitchOrbitHeight();
        // 呼吸偏航。
float breathYaw;
        if (!currentFrameIsRecentering)
        {
            breathYaw = Mathf.Sin(breathPhase) * idleBreathAmplitude * idleBreathWeight * shakeAttenuation;
        }
        else
        {
            breathYaw = 0f;
        }

        breathYaw += Mathf.Sin(aimBreathPhase + Mathf.PI * 0.25f) * aimBreathYawAmplitude * proceduralAimBreathWeight * shakeAttenuation;
        // yawRotation（偏航旋转）：保存 Quaternion.Euler(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Slerp(Quaternion, Quaternion, float) 的参数、RE4RThirdPersonCamera.UpdateFollowPivot(float)。
        if (TracksHeldKnifeInput)
        {
            followPivot.rotation = Quaternion.Euler(shoulderOrbitPitch, yaw, 0f);
            return;
        }
        Quaternion yawRotation = Quaternion.Euler(shoulderOrbitPitch, yaw + breathYaw, 0f);
        // 回正自身已有定时平滑，避免再次阻尼使实际画面迟于移动参考交接。
        // 赋值前保存的目标跟随轴心。
global::UnityEngine.Transform assignmentReceiver34 = // 回正自身已有定时平滑，避免再次阻尼使实际画面迟于移动参考交接。
 followPivot;
        if (currentFrameIsRecentering)
        {
            assignmentReceiver34.rotation = yawRotation;
        }
        else
        {
            float currentYaw = followPivot.eulerAngles.y;
            float nextYaw = Mathf.LerpAngle(currentYaw, yaw + breathYaw, dt * rotationSmoothing);
            assignmentReceiver34.rotation = Quaternion.Euler(shoulderOrbitPitch, nextYaw, 0f);
        }
    }

    // ========================== 模块 2：鼠标旋转 ==========================
    /// <summary>DriveInput 的业务处理入口；本方法直接依赖 LeonController.AdvanceFreeViewRecentering(float, float)、LeonController.InterruptFreeViewRecentering()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.DeltaAngle(float, float)、Mathf.LerpAngle(float, float, float)、Mathf.Repeat(float, float) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） LeonController.AdvanceFreeViewRecentering(float, float)、LeonController.InterruptFreeViewRecentering()、Mathf.Abs(float)、Mathf.Clamp(float, float, float)、Mathf.DeltaAngle(float, float)、Mathf.LerpAngle(float, float, float)、Mathf.Repeat(float, float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void DriveInput(float dt)
    {
        // 观察。
Vector2 look = GetLookInput();
        if (!IsFinite(look.x) || !IsFinite(look.y))
        {
            look = Vector2.zero;
        }

        // 接管帧的画面已经包含本帧观察结果，不再重复消费同一份鼠标位移。
        if (aimTakeOverFrame == Time.frameCount)
        {
            look = Vector2.zero;
        }

        if (controller != null && controller.CurrentFreeLookPhase == FreeLookPhase.Recentering)
        {
            if (look.sqrMagnitude > 0.0001f)
            {
                controller.InterruptFreeViewRecentering();
            }
            else
            {
                currentFrameIsRecentering = true;
                yaw = controller.AdvanceFreeViewRecentering(dt, freeViewRecenteringDuration);
                return;
            }
        }

        // 输入缩放。
float inputScale;
        if (controller != null)
        {
            inputScale = 1f;
        }
        else
        {
            inputScale = dt;
        }

        // 瞄准倍率。
float aimMultiplier;
        if (IsAiming)
        {
            aimMultiplier = aimSensitivity;
        }
        else
        {
            aimMultiplier = 1f;
        }

        yaw += look.x * HorizontalDegreesPerPixel * horizontalSensitivity * aimMultiplier * inputScale;
        // 俯仰输入。
float pitchInput;
        if (invertY)
        {
            pitchInput = look.y;
        }
        else
        {
            pitchInput = -look.y;
        }

        pitch += pitchInput * VerticalDegreesPerPixel * verticalSensitivity * aimMultiplier * inputScale;
        pitch = Mathf.Clamp(pitch, pitchRange.x, pitchRange.y);
        yaw = Mathf.Repeat(yaw + 180f, 360f) - 180f;
        // 角色转向延迟跟随（RE4 经典滞后手感）
        bool allowAutoAlign = controller == null || !controller.IsolateObserveCameraSteering
            && (controller.AnimDriver == null || !controller.AnimDriver.IsBasicRunning);
        float alignWeight = 0f;
        if (UsesPistolShoulder && !nativePoseActive)
        {
            alignWeight = 1f - pistolFramingWeight;
        }
        else if (!IsAiming)
        {
            alignWeight = 1f;
        }
        if (!TracksHeldKnifeInput && autoAlignSpeed > 0f && alignWeight > .001f && GetMoveMagnitude() > 0.1f && allowAutoAlign)
        {
            // 目标偏航。
float targetYaw = GetCharacterYaw();
            // 目标增量。
float targetDelta = Mathf.DeltaAngle(lastTargetYaw, targetYaw);
            if (Mathf.Abs(targetDelta) > 1f)
            {
                turnLagTimer = turnLagDelay;
            }

            lastTargetYaw = targetYaw;
            if (turnLagTimer > 0f)
            {
                turnLagTimer -= dt;
            }
            else
            {
                // 偏航差值。
float yawDiff = Mathf.DeltaAngle(yaw, targetYaw);
                if (Mathf.Abs(yawDiff) > 45f)
                {
                    yaw = Mathf.LerpAngle(yaw, targetYaw, autoAlignSpeed * alignWeight * dt);
                }
            }
        }
    }

    // ========================== 模块 3：碰撞检测 ==========================
    // 由虚拟摄像机上的稳定碰撞扩展在位置阶段统一处理
    // ========================== 模块 4：瞄准模式 / 状态插值 ==========================
    /// <summary>UpdateStateBlend 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.SmoothStep(float, float, float)、RE4RThirdPersonCamera.ApplyStateParams(RE4RThirdPersonCamera.StateParams)、RE4RThirdPersonCamera.LerpStateParams(RE4RThirdPersonCamera.StateParams, RE4RThirdPersonCamera.StateParams, float)、RE4RThirdPersonCamera.ResolveCameraStateIndex()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.SmoothStep(float, float, float)、RE4RThirdPersonCamera.ApplyStateParams(RE4RThirdPersonCamera.StateParams)、RE4RThirdPersonCamera.LerpStateParams(RE4RThirdPersonCamera.StateParams, RE4RThirdPersonCamera.StateParams, float)、RE4RThirdPersonCamera.ResolveCameraStateIndex()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateStateBlend(float dt)
    {
        if (UpdateNativeShoulderCamera(dt))
        {
            return;
        }
        int targetIndex = ResolveCameraStateIndex();
        if (UsesPistolShoulder && targetIndex == 1)
        {
            // 手枪的基础机位继续跟随探索，瞄准的机位和镜头由同一权重驱动。
            targetIndex = 0;
        }
        // 状态或武器在过渡中再次变化时，从当前画面继续接向新参数。
        float weight = 1f - Mathf.Exp(-dt * paramBlendSpeed);
        ApplyStateParams(LerpStateParams(CaptureCurrentStateParams(), states[targetIndex], weight));
    }

    /// <summary>ResolveCameraStateIndex 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.BeginProfileBlend(RE4RThirdPersonCamera.StateParams)、RE4RThirdPersonCamera.UpdateStateBlend(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    int ResolveCameraStateIndex()
    {
        // 是否正在瞄准。
bool isAiming = IsAiming;
        // 是否正在冲刺。
bool isSprinting = IsSprinting && !isAiming;
        if (isAiming)
        {
            return 1;
        }
        else
        {
            // 分支选出的2。
int selectedValue39;
            if (isSprinting)
            {
                selectedValue39 = 2;
            }
            else
            {
                selectedValue39 = 0;
            }

            return (selectedValue39);
        }
    }

    /// <summary>
    /// 奔跑镜头必须由实际移动结果驱动。按住冲刺键、受击、贴墙无法前进等情况
    /// 都不应触发拉远、视野角变化与镜头抖动。
    /// </summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Input.GetKey(KeyCode)、Mathf.Max(float, float)、Mathf.Min(float, float)、RE4RThirdPersonCamera.GetMoveMagnitude()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateCameraSprintState()
    {
        if (controller == null)
        {
            cameraSprintActive = Input.GetKey(KeyCode.LeftShift) && GetMoveMagnitude() > 0.1f;
            return;
        }

        // 是否可以冲刺。
bool canSprint = controller.IsEffectiveSprinting && !IsAiming;
        // 阈值。
float threshold;
        if (cameraSprintActive)
        {
            threshold = Mathf.Min(sprintCameraEnterSpeed, sprintCameraExitSpeed);
        }
        else
        {
            threshold = Mathf.Max(sprintCameraEnterSpeed, sprintCameraExitSpeed);
        }

        cameraSprintActive = canSprint && controller.CurrentPlanarSpeed >= threshold;
    }

    /// <summary>CaptureCurrentStateParams 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.StateParams.StateParams()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.UpdateStateBlend(float)、RE4RThirdPersonCamera.UpdateNativeShoulderCamera(float)；下游（含内部回调体） RE4RThirdPersonCamera.StateParams.StateParams()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    StateParams CaptureCurrentStateParams()
    {
        return new StateParams
        {
            fov = curFOV,
            cameraDistance = curDistance,
            verticalArm = curVerticalArm,
            shoulderX = curShoulderX,
            damping = curDamping
        };
    }

    /// <summary>LerpStateParams 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、RE4RThirdPersonCamera.StateParams.StateParams()、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.UpdateStateBlend(float)；下游（含内部回调体） Mathf.Lerp(float, float, float)、RE4RThirdPersonCamera.StateParams.StateParams()、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="from">来源。</param>
    /// <param name="to">目标。</param>
    /// <param name="t">镜头状态参数的插值比例。</param>
    static StateParams LerpStateParams(StateParams from, StateParams to, float t)
    {
        return new StateParams
        {
            fov = Mathf.Lerp(from.fov, to.fov, t),
            cameraDistance = Mathf.Lerp(from.cameraDistance, to.cameraDistance, t),
            verticalArm = Mathf.Lerp(from.verticalArm, to.verticalArm, t),
            shoulderX = Mathf.Lerp(from.shoulderX, to.shoulderX, t),
            damping = Vector3.Lerp(from.damping, to.damping, t)
        };
    }

    /// <summary>ApplyStateParams 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.UpdateStateBlend(float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameters">参数集合。</param>
    void ApplyStateParams(StateParams parameters)
    {
        curDistance = parameters.cameraDistance;
        curVerticalArm = parameters.verticalArm;
        curShoulderX = parameters.shoulderX;
        curFOV = parameters.fov;
        curDamping = parameters.damping;
    }

    /// <summary>将插值后的参数 + 近战/敌人修正应用到 Cinemachine 组件</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()、RE4RThirdPersonCamera.SnapToInitialCameraPose()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Max(float, float)、RE4RThirdPersonCamera.ResolveSafePitchHeightOffset(float, float, float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyCameraParams()
    {
        // 近战推近：缩短距离
        // 最终距离。
        float finalDist = Mathf.Max(curDistance, ReadMinimumShoulderDistance());
        float verticalArm = curVerticalArm;
        float shoulderX = curShoulderX;
        float enemyZoom = currentEnemyZoom;
        Vector3 damping = curDamping;
        if (UsesPistolShoulder && !nativePoseActive)
        {
            // 过渡期间基础机位直接从探索构图线性交接给 PlaceShoulderCamera 的新越肩构图，不再绕道旧 aimPose。
            StateParams aim = states[1];
            damping = Vector3.Lerp(damping, aim.damping, pistolFramingWeight);
            enemyZoom = Mathf.Lerp(enemyZoom, 1f, pistolFramingWeight);
        }
        float meleeAttenuation = 1f - Mathf.Clamp01(currentCollisionCompression * 0.85f);
        finalDist -= meleePushDistance * meleePushRatio * meleeAttenuation;
        // 敌人拉远：放大距离
        finalDist *= enemyZoom;
        finalDist += weaponRecoilDistanceOffset;
        finalDist += dropCameraPullback * dropCameraWeight;
        finalDist += crouchDistanceOffset * crouchCameraWeight;
        finalDist += ReadShoulderOrbitPullback();
        float lookingUp = Mathf.SmoothStep(0f, 1f, -shoulderLookPitch / 65f);
        finalDist -= .18f * lookingUp;
        // 瞄准推进仍交给近裁面与墙体净空约束。
        finalDist -= .16f * Mathf.SmoothStep(0f, 1f, aimPushWeight);
        finalDist = Mathf.Max(cameraDistanceFloor, finalDist);
        tpf.CameraDistance = finalDist;
        // 蹲伏竖直偏移量。
        float crouchVerticalOffset;
        if (UsesPistolShoulder && !nativePoseActive)
        {
            crouchVerticalOffset = Mathf.Lerp(crouchExploreVerticalOffset, crouchAimVerticalOffset, pistolFramingWeight);
        }
        else if (IsAiming)
        {
            crouchVerticalOffset = crouchAimVerticalOffset;
        }
        else
        {
            crouchVerticalOffset = crouchExploreVerticalOffset;
        }

        // 基础竖直手臂。
        float baseVerticalArm = verticalArm + crouchVerticalOffset * crouchCameraWeight * (1f - nativePoseWeight) - ReadPitchOrbitHeight();
        float shoulderOffset = shoulderX + weaponRecoilShoulderOffset;
        shoulderOffset += .04f * lookingUp;
        // 安全俯仰高度偏移量。
        float safePitchHeightOffset = ResolveSafePitchHeightOffset(baseVerticalArm, finalDist, shoulderOffset, pitchHeightOffset);
        tpf.VerticalArmLength = baseVerticalArm + safePitchHeightOffset;
        tpf.ShoulderOffset = new Vector3(shoulderOffset, 0f, 0f);
        tpf.Damping = damping;
    }

    /// <summary>ValidateFiniteCameraState 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFiniteCameraState()、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)、RE4RThirdPersonCamera.RecoverFiniteCameraState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()、RE4RThirdPersonCamera.Start()；下游（含内部回调体） Debug.LogError(object, Object)、RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFiniteCameraState()、RE4RThirdPersonCamera.LogRecoveredFiniteWarning(string, string)、RE4RThirdPersonCamera.RecoverFiniteCameraState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stage">阶段。</param>
    private bool ValidateFiniteCameraState(string stage)
    {
        if (IsFiniteCameraState())
        {
            return true;
        }

        // 无效字段。
string invalidField = GetFirstInvalidCameraStateName();
        RecoverFiniteCameraState();
        if (!IsFiniteCameraState())
        {
            if (lastFiniteFailureFrame != Time.frameCount)
            {
                lastFiniteFailureFrame = Time.frameCount;
                Debug.LogError("[RE4RThirdPersonCamera] 在“" + stage + "”检测到非数值且恢复失败，异常字段：" + GetFirstInvalidCameraStateName() + "。", this);
            }

            return false;
        }

        LogRecoveredFiniteWarning(stage, invalidField);
        return false;
    }

    /// <summary>RecoverFiniteCameraState 的业务处理入口；本方法直接依赖 Camera.ResetCullingMatrix()、Camera.ResetProjectionMatrix()、Camera.ResetWorldToCameraMatrix()、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Quaternion.Euler(float, float, float)、RE4RThirdPersonCamera.ClearDropCameraImmediate() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.ValidateFiniteCameraState(string)；下游（含内部回调体） Camera.ResetCullingMatrix()、Camera.ResetProjectionMatrix()、Camera.ResetWorldToCameraMatrix()、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Quaternion.Euler(float, float, float)、RE4RThirdPersonCamera.ClearDropCameraImmediate() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RecoverFiniteCameraState()
    {
        ClearDropCameraImmediate();
        // 安全目标位置。
Vector3 safeTargetPosition;
        if (target != null && IsFinite(target.position))
        {
            safeTargetPosition = target.position;
        }
        else
        {
            if (controller != null && IsFinite(controller.transform.position))
            {
                safeTargetPosition = controller.transform.position;
            }
            else
            {
                safeTargetPosition = Vector3.zero;
            }
        }

        // 安全偏航。
float safeYaw;
        if (controller != null && IsFinite(controller.transform.eulerAngles.y))
        {
            safeYaw = controller.transform.eulerAngles.y;
        }
        else
        {
            safeYaw = 0f;
        }

        // 安全俯仰。
float safePitch;
        if (IsFinite(startPitch))
        {
            safePitch = Mathf.Clamp(startPitch, pitchRange.x, pitchRange.y);
        }
        else
        {
            safePitch = 0f;
        }

        yaw = safeYaw;
        pitch = safePitch;
        basePivotPos = safeTargetPosition;
        pivotVel = Vector3.zero;
        if (controller != null && controller.IsCrouching)
        {
            crouchCameraWeight = 1f;
        }
        else
        {
            crouchCameraWeight = 0f;
        }

        pitchHeightOffset = 0f;
        pitchHeightVelocity = 0f;
        currentEnemyZoom = 1f;
        meleePushTimer = 0f;
        meleePushRatio = 0f;
        aimBreathPhase = 0f;
        aimBreathWeight = 0f;
        proceduralAimBreathWeight = 0f;
        nativeAimBreathAngles = Vector2.zero;
        breathPhase = 0f;
        idleTimer = 0f;
        weaponRecoilPitchOffset = 0f;
        visibleWeaponRecoilPitch = 0f;
        w870RecoilActive = false;
        pistolRecoilHoldUntil = 0f;
        weaponRecoilYawOffset = 0f;
        weaponRecoilFOVOffset = 0f;
        weaponRecoilDistanceOffset = 0f;
        weaponRecoilShoulderOffset = 0f;
        weaponRecoilPitchVelocity = 0f;
        weaponRecoilYawVelocity = 0f;
        weaponRecoilFOVVelocity = 0f;
        weaponRecoilDistanceVelocity = 0f;
        weaponRecoilShoulderVelocity = 0f;
        curDistance = SanitizePositive(-normalOffset.z, 1.8f);
        curVerticalArm = Sanitize(normalOffset.y, 1.6f);
        curShoulderX = Sanitize(normalOffset.x, 0.5f);
        curFOV = Mathf.Clamp(Sanitize(normalFOV, 48f), 1f, 179f);
        curDamping = SanitizeVector(curDamping, new Vector3(0.1f, 0.1f, 0.1f));
        if (followPivot != null)
        {
            followPivot.SetPositionAndRotation(safeTargetPosition, Quaternion.Euler(0f, safeYaw, 0f));
        }

        if (tpf != null)
        {
            tpf.CameraDistance = Mathf.Max(cameraDistanceFloor, curDistance);
            tpf.VerticalArmLength = curVerticalArm;
            tpf.ShoulderOffset = new Vector3(curShoulderX, 0f, 0f);
            tpf.Damping = curDamping;
        }

        if (panTilt != null)
        {
            panTilt.PanAxis.Value = 0f;
            panTilt.TiltAxis.Value = safePitch;
        }

        // 安全旋转。
Quaternion safeRotation = Quaternion.Euler(safePitch, safeYaw, 0f);
        // 安全偏移量。
Vector3 safeOffset = Quaternion.Euler(0f, safeYaw, 0f) * new Vector3(curShoulderX, curVerticalArm, -curDistance);
        if (cam != null)
        {
            cam.ResetWorldToCameraMatrix();
            cam.ResetProjectionMatrix();
            cam.ResetCullingMatrix();
            cam.transform.SetPositionAndRotation(safeTargetPosition + safeOffset, safeRotation);
            cam.fieldOfView = curFOV;
        }

        if (vcam != null)
        {
            // 镜头。
var lens = vcam.Lens;
            lens.FieldOfView = curFOV;
            lens.NearClipPlane = Mathf.Clamp(gameplayNearClipPlane, 0.001f, 0.1f);
            vcam.Lens = lens;
        }
    }

    /// <summary>Sanitize 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.RecoverFiniteCameraState()；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="fallback">后备。</param>
    private static float Sanitize(float value, float fallback)
    {
        if (IsFinite(value))
        {
            return value;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>SanitizePositive 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.RecoverFiniteCameraState()、RE4RThirdPersonCamera.UpdateDropCamera(float)；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="fallback">后备。</param>
    private static float SanitizePositive(float value, float fallback)
    {
        if (IsFinite(value) && value > 0f)
        {
            return value;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>SanitizeVector 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.RecoverFiniteCameraState()；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    /// <param name="fallback">后备。</param>
    private static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
    {
        if (IsFinite(value))
        {
            return value;
        }
        else
        {
            return fallback;
        }
    }

    /// <summary>IsFinite 的业务处理入口；本方法直接依赖 float.IsInfinity(float)、float.IsNaN(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)、RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFinite(Matrix4x4)、RE4RThirdPersonCamera.IsFinite(Quaternion)、RE4RThirdPersonCamera.IsFinite(Vector3) 等共 13 处；下游（含内部回调体） float.IsInfinity(float)、float.IsNaN(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>IsFinite 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFiniteCameraState()、RE4RThirdPersonCamera.RecoverFiniteCameraState()、RE4RThirdPersonCamera.SanitizeVector(Vector3, Vector3)；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// <summary>IsFinite 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFiniteCameraState()；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    /// <summary>IsFinite 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.GetFirstInvalidCameraStateName()、RE4RThirdPersonCamera.IsFiniteCameraState()；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">当前矩阵4横向4。</param>
    private static bool IsFinite(Matrix4x4 value)
    {
        // 行。
for (int row = 0; row < 4; row++)
        {
            // 列。
for (int column = 0; column < 4; column++)
            {
                if (!IsFinite(value[row, column]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>UpdateCrouchCamera 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Max(float, float)、Mathf.MoveTowards(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateCrouchCamera(float dt)
    {
        // 目标权重。
float targetWeight;
        if (controller != null && controller.IsCrouching)
        {
            targetWeight = 1f;
        }
        else
        {
            targetWeight = 0f;
        }

        crouchCameraWeight = Mathf.MoveTowards(crouchCameraWeight, targetWeight, Mathf.Max(0.01f, crouchCameraBlendSpeed) * dt);
    }

    /// <summary>UpdatePitchHeight 的业务处理入口；本方法直接依赖 Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)、Mathf.SmoothStep(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)、Mathf.SmoothStep(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdatePitchHeight(float dt)
    {
        float aimWeight = 0f;
        if (UsesPistolShoulder && !nativePoseActive)
        {
            aimWeight = pistolFramingWeight;
        }
        else if (IsAiming)
        {
            aimWeight = 1f;
        }
        float targetOffset = 0f;
        if (aimWeight > 0f)
        {
            // 死亡区。
float deadZone = Mathf.Max(0f, pitchHeightDeadZone);
            if (pitch > deadZone)
            {
                // 最大向下俯仰。
float maximumDownPitch = Mathf.Max(deadZone + 0.01f, pitchRange.y);
                // 权重。
float weight = Mathf.InverseLerp(deadZone, maximumDownPitch, pitch);
                targetOffset = Mathf.Lerp(0f, Mathf.Max(0f, aimLookDownHeightOffset), Mathf.SmoothStep(0f, 1f, weight));
            }
            else if (pitch < -deadZone)
            {
                // 最大向上俯仰。
float maximumUpPitch = Mathf.Max(deadZone + 0.01f, -pitchRange.x);
                // 权重。
float weight = Mathf.InverseLerp(deadZone, maximumUpPitch, -pitch);
                targetOffset = Mathf.Lerp(0f, Mathf.Min(0f, aimLookUpHeightOffset), Mathf.SmoothStep(0f, 1f, weight));
            }
        }

        if (dt <= 0f)
        {
            pitchHeightVelocity = 0f;
            return;
        }

        pitchHeightOffset = Mathf.SmoothDamp(pitchHeightOffset, targetOffset * aimWeight, ref pitchHeightVelocity,
            Mathf.Max(0.01f, pitchHeightSmoothTime), Mathf.Infinity, dt);
    }

    /// <summary>ResolveSafePitchHeightOffset 的业务处理入口；本方法直接依赖 LayerMask.NameToLayer(string)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.Sign(float)、Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.ApplyCameraParams()；下游（含内部回调体） LayerMask.NameToLayer(string)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Mathf.Sign(float)、Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)、Transform.IsChildOf(Transform) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseVerticalArm">基础竖直手臂。</param>
    /// <param name="cameraDistance">摄像机距离。</param>
    /// <param name="shoulderOffset">肩部偏移量。</param>
    /// <param name="desiredOffset">期望偏移量。</param>
    float ResolveSafePitchHeightOffset(float baseVerticalArm, float cameraDistance, float shoulderOffset, float desiredOffset)
    {
        if (!constrainPitchHeightByCollision || Mathf.Abs(desiredOffset) <= 0.0001f || followPivot == null || collisionMask.value == 0)
        {
            return desiredOffset;
        }

        // 轴心旋转。
Quaternion pivotRotation = followPivot.rotation;
        // 基础摄像机位置。
Vector3 baseCameraPosition = followPivot.position + pivotRotation * new Vector3(shoulderOffset, baseVerticalArm, -cameraDistance);
        // 移动。
Vector3 movement = pivotRotation * Vector3.up * desiredOffset;
        // 距离。
float distance = movement.magnitude;
        if (distance <= 0.0001f)
        {
            return desiredOffset;
        }

        // 实际生效的遮罩。
int effectiveMask = collisionMask.value;
        // 玩家图层。
int playerLayer = LayerMask.NameToLayer("player");
        if (playerLayer >= 0)
        {
            effectiveMask &= ~(1 << playerLayer);
        }

        if (!Physics.SphereCast(baseCameraPosition, Mathf.Max(0.01f, collisionRadius), movement.normalized, out RaycastHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit, distance, effectiveMask, QueryTriggerInteraction.Ignore))
        {
            return desiredOffset;
        }

        if (controller != null && hit.collider != null && hit.collider.transform.IsChildOf(controller.transform))
        {
            return desiredOffset;
        }

        // 安全距离。
float safeDistance = Mathf.Max(0f, hit.distance - collisionSkin);
        return Mathf.Sign(desiredOffset) * Mathf.Min(distance, safeDistance);
    }

    /// <summary>ApplyFOV 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void ApplyFOV(float dt)
    {
        if (UsesPistolShoulder && !nativePoseActive)
        {
            vcam.Lens.FieldOfView = Mathf.Lerp(curFOV, aimFOV, pistolFramingWeight) + weaponRecoilFOVOffset;
            return;
        }

        vcam.Lens.FieldOfView = Mathf.Lerp(vcam.Lens.FieldOfView,
            curFOV + weaponRecoilFOVOffset, 1f - Mathf.Exp(-dt * paramBlendSpeed));
    }

    // ========================== 模块 5：镜头震动 ==========================
    /// <summary>ApplyShake 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.PerlinNoise(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Max(float, float)、Mathf.PerlinNoise(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void ApplyShake(float dt)
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            // 比例。
float ratio;
            if (shakeStartDuration > 0f)
            {
                ratio = Mathf.Max(0f, shakeTimer / shakeStartDuration);
            }
            else
            {
                ratio = 0f;
            }

            // 晃动。
float shake = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * currentShakeMag * ratio;
            // 镜头。
var lens = vcam.Lens;
            lens.Dutch = shake * 2f;
            vcam.Lens = lens;
        }
        else if (vcam.Lens.Dutch != 0f)
        {
            // 镜头。
var lens = vcam.Lens;
            lens.Dutch = 0f;
            vcam.Lens = lens;
        }
    }

    // ========================== 模块 6：剧情锁定 ==========================
    /// <summary>ApplyLockedCamera 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Quaternion.Slerp(Quaternion, Quaternion, float)、Vector3.Lerp(Vector3, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void ApplyLockedCamera(float dt)
    {
        // 候选遍历索引。
float k = dt * 5f;
        cam.transform.position = Vector3.Lerp(cam.transform.position, lockedPosition, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, lockedRotation, k);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, lockedFOV, k);
    }

    // ========================== 模块 7：奔跑晃动 ==========================
    // 在 UpdateFollowPivot 中叠加到 followPivot.position
    // ========================== 模块 8：待机呼吸 ==========================
    /// <summary>UpdateIdleBreathing 的业务处理入口；本方法直接依赖 Mathf.Abs(float)、Mathf.Lerp(float, float, float)、RE4RThirdPersonCamera.GetMoveMagnitude()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Abs(float)、Mathf.Lerp(float, float, float)、RE4RThirdPersonCamera.GetMoveMagnitude()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateIdleBreathing(float dt)
    {
        float targetWeight = 0f;
        if (GetMoveMagnitude() < 0.1f && !IsSprinting)
        {
            idleTimer += dt;
            if (idleTimer > .5f)
            {
                if (UsesPistolShoulder && !nativePoseActive)
                {
                    targetWeight = 1f - pistolFramingWeight;
                }
                else if (!IsAiming)
                {
                    targetWeight = 1f;
                }
            }
        }
        else
        {
            idleTimer = 0f;
        }

        if (dt > 0f)
        {
            idleBreathWeight = Mathf.SmoothDamp(idleBreathWeight, targetWeight, ref idleBreathVelocity,
                ShoulderTransitionDamping, Mathf.Infinity, dt);
        }
        if (idleBreathWeight > .001f)
        {
            breathPhase = Mathf.Repeat(breathPhase + dt * idleBreathSpeed, 2f * Mathf.PI);
        }
    }

    /// <summary>UpdateAimBreathing 的业务处理入口；本方法直接依赖 Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.MoveTowards(float, float, float)、RE4RThirdPersonCamera.GetMoveMagnitude()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.MoveTowards(float, float, float)、RE4RThirdPersonCamera.GetMoveMagnitude()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateAimBreathing(float dt)
    {
        // 目标权重。
float targetWeight;
        if (UsesPistolShoulder && !nativePoseActive)
        {
            targetWeight = pistolFramingWeight;
        }
        else if (IsAiming && !IsSprinting)
        {
            targetWeight = 1f;
        }
        else
        {
            targetWeight = 0f;
        }

        if (controller != null && controller.currentState == LeonState.Shooting)
        {
            targetWeight *= 0.25f;
        }

        // 移动淡入淡出。
float moveFade = Mathf.Clamp01(GetMoveMagnitude());
        targetWeight *= Mathf.Lerp(1f, 0.45f, moveFade);
        aimBreathWeight = Mathf.MoveTowards(aimBreathWeight, targetWeight, dt * aimBreathBlendSpeed);
        nativeAimBreathAngles = Vector2.zero;
        proceduralAimBreathWeight = aimBreathWeight;
        if (controller != null && controller.AnimDriver != null
            && activeProfile != null && activeProfile.nativeShoulder != null
            && controller.AnimDriver.TryReadStandingBreath(out var phase, out var weight)
            && activeProfile.nativeShoulder.TrySampleBreath(phase, out var angles))
        {
            nativeAimBreathAngles = angles * weight;
            proceduralAimBreathWeight *= 1f - weight;
        }
        if (aimBreathWeight > 0.001f)
        {
            aimBreathPhase = Mathf.Repeat(aimBreathPhase + dt * aimBreathSpeed, 2f * Mathf.PI);
        }
    }

    /// <summary>UpdateWeaponRecoil 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Max(float, float)、Mathf.SmoothDamp(float, float, ref float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateWeaponRecoil(float dt)
    {
        // 按原顺序缓存的实参0.01f。
float argumentValue55 = 0.01f;
        // 分支选出的当前武器后坐力返回时间。
float selectedValue54;
        if (currentWeaponRecoilReturnTime > 0f)
        {
            selectedValue54 = currentWeaponRecoilReturnTime;
        }
        else
        {
            selectedValue54 = weaponRecoilReturnTime;
        }

        // 返回时间。
float returnTime = Mathf.Max(argumentValue55, selectedValue54);
        bool pistolAiming = IsAiming && controller != null && controller.currentWeapon == WeaponType.Pistol;
        if (!pistolAiming) pistolRecoilHoldUntil = 0f;
        float targetPitch = Time.time < pistolRecoilHoldUntil ? pistolRetainedPitch : 0f;
        weaponRecoilPitchOffset = Mathf.SmoothDamp(weaponRecoilPitchOffset, targetPitch, ref weaponRecoilPitchVelocity, returnTime, Mathf.Infinity, dt);
        visibleWeaponRecoilPitch = Mathf.Lerp(visibleWeaponRecoilPitch, weaponRecoilPitchOffset, 1f - Mathf.Exp(-dt / .032f));
        if (w870RecoilActive)
        {
            if (IsAiming && controller != null && controller.currentWeapon == WeaponType.Shotgun
                && controller.AnimDriver != null && controller.AnimDriver.TryReadW870ShotPumpPhase(out float phase)
                && activeProfile != null && activeProfile.nativeShoulder != null && cam != null)
            {
                visibleWeaponRecoilPitch = -activeProfile.nativeShoulder.ShotPumpPitch(phase, cam.fieldOfView);
                weaponRecoilPitchOffset = visibleWeaponRecoilPitch;
                weaponRecoilPitchVelocity = 0f;
            }
            else w870RecoilActive = false;
        }
        weaponRecoilYawOffset = Mathf.SmoothDamp(weaponRecoilYawOffset, 0f, ref weaponRecoilYawVelocity, returnTime, Mathf.Infinity, dt);
        weaponRecoilFOVOffset = Mathf.SmoothDamp(weaponRecoilFOVOffset, 0f, ref weaponRecoilFOVVelocity, returnTime, Mathf.Infinity, dt);
        weaponRecoilDistanceOffset = Mathf.SmoothDamp(weaponRecoilDistanceOffset, 0f, ref weaponRecoilDistanceVelocity, returnTime, Mathf.Infinity, dt);
        weaponRecoilShoulderOffset = Mathf.SmoothDamp(weaponRecoilShoulderOffset, 0f, ref weaponRecoilShoulderVelocity, returnTime, Mathf.Infinity, dt);
    }

    // ========================== 模块 9：近战镜头微推 ==========================
    /// <summary>UpdateMeleePush 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.TriggerMeleePush()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） RE4RThirdPersonCamera.TriggerMeleePush()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateMeleePush(float dt)
    {
        if (autoDetectMelee && controller != null)
        {
            // 是否近战。
bool isMelee = controller.currentState == LeonState.MeleeAttacking;
            if (isMelee && !wasMelee && !controller.IsKnifeAttackActive)
            {
                TriggerMeleePush();
            }

            wasMelee = isMelee;
        }

        if (meleePushTimer > 0f)
        {
            meleePushTimer -= dt;
            // 总计持续时间。
float totalDuration = meleePushDuration + meleeRecoverDuration;
            // 已用。
float elapsed = totalDuration - meleePushTimer;
            if (elapsed <= meleePushDuration)
            {
                // 近战镜头前推阶段的进度。
float t = elapsed / meleePushDuration;
                meleePushRatio = t * t * (3f - 2f * t);
            }
            else
            {
                // 近战镜头回退阶段的进度。
float t = (elapsed - meleePushDuration) / meleeRecoverDuration;
                meleePushRatio = 1f - (t * t * (3f - 2f * t));
            }

            if (meleePushTimer <= 0f)
            {
                meleePushTimer = 0f;
                meleePushRatio = 0f;
            }
        }
        else
        {
            meleePushRatio = 0f;
        }
    }

    // ========================== 模块 10：敌人近距离自动拉远 ==========================
    /// <summary>UpdateEnemyProximity 的业务处理入口；本方法直接依赖 Mathf.Lerp(float, float, float)、Mathf.Max(int, int)、Physics.CheckSphere(Vector3, float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()；下游（含内部回调体） Mathf.Lerp(float, float, float)、Mathf.Max(int, int)、Physics.CheckSphere(Vector3, float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="dt">本次计算的时间步长。</param>
    void UpdateEnemyProximity(float dt)
    {
        if (enemyLayerMask == 0)
        {
            return;
        }

        enemyCheckFrameInterval = Mathf.Max(1, enemyCheckFrameInterval);
        enemyFrameCounter++;
        if (enemyFrameCounter % enemyCheckFrameInterval == 0)
        {
            // 中心。
Vector3 center = target.position + Vector3.up * 1f;
            if (Physics.CheckSphere(center, enemyProximityRange, enemyLayerMask, QueryTriggerInteraction.UseGlobal))
            {
                hasNearbyEnemy = true;
                enemyProximityHoldUntil = Time.time + 0.35f;
            }
            else if (Time.time >= enemyProximityHoldUntil)
            {
                hasNearbyEnemy = false;
            }
        }

        // 目标缩放视野。
float targetZoom;
        // 瞄准镜头保留原有肩距，近敌拉远仅用于一般观察；室内或贴墙受阻时自动衰减，防止与避障互相拉扯。
        bool tracksExploreZoom = !IsAiming || UsesPistolShoulder && !nativePoseActive;
        if (hasNearbyEnemy && tracksExploreZoom && knifeFramingWeight <= 0.01f)
        {
            float openSpaceWeight = 1f - Mathf.SmoothStep(0.10f, 0.45f, currentCollisionCompression);
            targetZoom = Mathf.Lerp(1f, enemyZoomOutFactor, openSpaceWeight);
        }
        else
        {
            targetZoom = 1f;
        }

        currentEnemyZoom = Mathf.Lerp(currentEnemyZoom, targetZoom, dt * enemyZoomSpeed);
    }

    // ========================== PanTilt 更新 ==========================
    /// <summary>UpdatePanTilt 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)、Mathf.Sin(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.LateUpdate()、RE4RThirdPersonCamera.SnapToInitialCameraPose()；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Sin(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private Vector2 nativeAimBreathAngles;
    private float proceduralAimBreathWeight;

    void UpdatePanTilt(float dt)
    {
        // 瞄准呼吸俯仰。
float aimBreathPitch = Mathf.Sin(aimBreathPhase) * aimBreathPitchAmplitude * proceduralAimBreathWeight - nativeAimBreathAngles.y;
        // 掉落俯仰。
float dropPitch = dropLookDownAngle * dropCameraWeight + dropImpactPitch * dropImpactPulse * dropImpactStrength;
        // 最终俯仰。
        float finalPitch = Mathf.Clamp(shoulderLookPitch + aimBreathPitch
            + dropPitch, pitchRange.x, pitchRange.y);
        panTilt.PanAxis.Value = weaponRecoilYawOffset + nativeAimBreathAngles.x;
        float pivotPitch = Mathf.DeltaAngle(0f, followPivot.eulerAngles.x);
        panTilt.TiltAxis.Value = Mathf.Clamp(finalPitch - pivotPitch, pitchRange.x, pitchRange.y);
    }

    // ========================== 辅助方法 ==========================
    /// <summary>是否正在瞄准。</summary>
    bool IsAiming
    {
        get
        {
            if (UsesPistolShoulder)
            {
                return controller.IsAiming;
            }

            if ((activeProfile == null || activeProfile.allowAim))
            {
                // 分支选出的取得浮点的返回结果。
bool selectedValue57;
                if (anim != null)
                {
                    selectedValue57 = anim.GetFloat(AimParameterHash) > 0.5f;
                }
                else
                {
                    selectedValue57 = Input.GetMouseButton(1);
                }

                return (selectedValue57);
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>是否正在冲刺。</summary>
    bool IsSprinting => cameraSprintActive;

    /// <summary>GetLookInput 的业务处理入口；本方法直接依赖 Input.GetAxis(string)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)；下游（含内部回调体） Input.GetAxis(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    Vector2 GetLookInput()
    {
        if (controller != null)
        {
            return controller.LookInput;
        }

        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    /// <summary>GetMoveMagnitude 的业务处理入口；本方法直接依赖 Input.GetAxis(string)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.DriveInput(float)、RE4RThirdPersonCamera.UpdateAimBreathing(float)、RE4RThirdPersonCamera.UpdateCameraSprintState()、RE4RThirdPersonCamera.UpdateIdleBreathing(float)；下游（含内部回调体） Input.GetAxis(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    float GetMoveMagnitude()
    {
        if (controller != null)
        {
            return controller.MoveInput.magnitude;
        }

        // 水平移动输入。
float h = Input.GetAxis("Horizontal");
        // 纵向移动输入。
float v = Input.GetAxis("Vertical");
        return new Vector2(h, v).magnitude;
    }

    // ========================== 调试可视化 ==========================
    /// <summary>Unity 选中对象辅助绘制回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Gizmos.DrawLine(Vector3, Vector3)、Gizmos.DrawWireSphere(Vector3, float)、Quaternion.Euler(float, float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDrawGizmosSelected()
    {
        if (target == null)
        {
            return;
        }

        // 按原顺序缓存的实参0f。
float argumentValue60 = 0f;
        // 分支选出的偏航。
float selectedValue59;
        if (Application.isPlaying)
        {
            selectedValue59 = yaw;
        }
        else
        {
            selectedValue59 = transform.eulerAngles.y;
        }

        // 偏航旋转。
Quaternion yawRot = Quaternion.Euler(argumentValue60, selectedValue59, 0f);
        // 偏移量。
Vector3 offset;
        if (Application.isPlaying)
        {
            offset = new Vector3(curShoulderX, curVerticalArm, -curDistance);
        }
        else
        {
            offset = normalOffset;
        }

        // 手臂位置。
Vector3 armPos = target.position + Vector3.up * offset.y;
        // 摄像机位置。
Vector3 camPos = armPos + yawRot * new Vector3(offset.x, 0f, offset.z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(armPos, camPos);
        Gizmos.DrawWireSphere(camPos, collisionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(armPos, 0.1f);
        if (enemyLayerMask != 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position + Vector3.up, enemyProximityRange);
        }

        if (debugDrawCollisionSolver && Application.isPlaying && stableCollision != null)
        {
            CameraCollisionSolver.DebugSnapshot snapshot = stableCollision.LastSnapshot;
            if (snapshot.valid)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(snapshot.focusPosition, 0.08f);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(snapshot.focusPosition, snapshot.ceilingAdjustedPosition);
                Gizmos.DrawLine(snapshot.ceilingAdjustedPosition, snapshot.shoulderAdjustedPosition);
                if (snapshot.blocked)
                {
                    Gizmos.color = new Color(1f, 0.45f, 0f, 1f);
                }
                else
                {
                    Gizmos.color = Color.white;
                }
                Gizmos.DrawLine(snapshot.focusPosition, snapshot.finalPosition);
                Gizmos.DrawWireSphere(snapshot.finalPosition, collisionRadius);
            }
        }
    }

    /// <summary>LogRecoveredFiniteWarning 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.NotifyDropImpact(float)、RE4RThirdPersonCamera.UpdateDropCamera(float)、RE4RThirdPersonCamera.ValidateFiniteCameraState(string)；下游（含内部回调体） Debug.LogWarning(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stage">阶段。</param>
    /// <param name="invalidField">无效字段。</param>
    private void LogRecoveredFiniteWarning(string stage, string invalidField)
    {
        if (lastFiniteFailureFrame == Time.frameCount)
        {
            return;
        }

        lastFiniteFailureFrame = Time.frameCount;
        // 当前时间。
float now = Time.realtimeSinceStartup;
        if (now < nextFiniteWarningTime)
        {
            return;
        }

        nextFiniteWarningTime = now + 2f;
        Debug.LogWarning("[RE4RThirdPersonCamera] 在“" + stage + "”检测到非数值（" + invalidField + "），已完成局部恢复并继续运行。", this);
    }

    /// <summary>GetFirstInvalidCameraStateName 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.IsFinite(Matrix4x4)、RE4RThirdPersonCamera.IsFinite(Quaternion)、RE4RThirdPersonCamera.IsFinite(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.ValidateFiniteCameraState(string)；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.IsFinite(Matrix4x4)、RE4RThirdPersonCamera.IsFinite(Quaternion)、RE4RThirdPersonCamera.IsFinite(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private string GetFirstInvalidCameraStateName()
    {
        if (!IsFinite(yaw))
        {
            return "水平旋转";
        }

        if (!IsFinite(pitch))
        {
            return "垂直旋转";
        }

        if (!IsFinite(basePivotPos))
        {
            return "跟随点基础位置";
        }

        if (!IsFinite(pivotVel))
        {
            return "跟随点平滑速度";
        }

        if (!IsFinite(dropCameraWeight))
        {
            return "坠落镜头权重";
        }

        if (!IsFinite(dropImpactElapsed))
        {
            return "落地冲击计时";
        }

        if (!IsFinite(dropImpactStrength))
        {
            return "落地冲击强度";
        }

        if (!IsFinite(dropImpactPulse))
        {
            return "落地冲击脉冲";
        }

        if (target == null)
        {
            return "跟随目标引用";
        }

        if (!IsFinite(target.position))
        {
            return "跟随目标位置";
        }

        if (!IsFinite(target.rotation))
        {
            return "跟随目标旋转";
        }

        if (followPivot == null)
        {
            return "跟随点引用";
        }

        if (!IsFinite(followPivot.position))
        {
            return "跟随点位置";
        }

        if (!IsFinite(followPivot.rotation))
        {
            return "跟随点旋转";
        }

        if (cam == null)
        {
            return "主摄像机引用";
        }

        if (!IsFinite(cam.transform.position))
        {
            return "主摄像机位置";
        }

        if (!IsFinite(cam.transform.rotation))
        {
            return "主摄像机旋转";
        }

        if (!IsFinite(cam.projectionMatrix))
        {
            return "投影矩阵";
        }

        if (!IsFinite(cam.worldToCameraMatrix))
        {
            return "观察矩阵";
        }

        if (tpf == null)
        {
            return "第三人称跟随组件";
        }

        return "其他镜头运行字段";
    }

    /// <summary>IsFiniteCameraState 的业务处理入口；本方法直接依赖 RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.IsFinite(Matrix4x4)、RE4RThirdPersonCamera.IsFinite(Quaternion)、RE4RThirdPersonCamera.IsFinite(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RE4RThirdPersonCamera.ValidateFiniteCameraState(string)；下游（含内部回调体） RE4RThirdPersonCamera.IsFinite(float)、RE4RThirdPersonCamera.IsFinite(Matrix4x4)、RE4RThirdPersonCamera.IsFinite(Quaternion)、RE4RThirdPersonCamera.IsFinite(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsFiniteCameraState()
    {
        return IsFinite(yaw) && IsFinite(pitch) && IsFinite(curDistance) && IsFinite(curVerticalArm) && IsFinite(curShoulderX) && IsFinite(curFOV) && IsFinite(curDamping) && IsFinite(basePivotPos) && IsFinite(pivotVel) && IsFinite(crouchCameraWeight) && IsFinite(pitchHeightOffset) && IsFinite(pitchHeightVelocity) && IsFinite(currentEnemyZoom) && IsFinite(meleePushRatio) && IsFinite(dropCameraWeight) && IsFinite(dropImpactElapsed) && IsFinite(dropImpactStrength) && IsFinite(dropImpactPulse) && IsFinite(weaponRecoilPitchOffset) && IsFinite(visibleWeaponRecoilPitch) && IsFinite(weaponRecoilYawOffset) && IsFinite(weaponRecoilFOVOffset) && IsFinite(weaponRecoilDistanceOffset) && IsFinite(weaponRecoilShoulderOffset) && target != null && IsFinite(target.position) && IsFinite(target.rotation) && followPivot != null && IsFinite(followPivot.position) && IsFinite(followPivot.rotation) && cam != null && IsFinite(cam.transform.position) && IsFinite(cam.transform.rotation) && IsFinite(cam.fieldOfView) && IsFinite(cam.nearClipPlane) && IsFinite(cam.farClipPlane) && IsFinite(cam.projectionMatrix) && IsFinite(cam.worldToCameraMatrix) && tpf != null && IsFinite(tpf.CameraDistance) && IsFinite(tpf.VerticalArmLength) && IsFinite(tpf.ShoulderOffset) && IsFinite(tpf.Damping);
    }
}

/// <summary>
/// 从稳定上身观察点到期望镜头位置执行三自由度连续避障求解。
/// 优先忽略敌人与动态刚体，顶头时下压高度、贴墙时内收肩位并平滑舒展恢复。
/// </summary>
[DisallowMultipleComponent]
sealed class RE4StableCameraCollision : CinemachineExtension
{
    private const float AbsoluteMinimumCameraDistance = 0.12f;
    private readonly CameraCollisionSolver solver = new CameraCollisionSolver();
    private RE4RThirdPersonCamera gameplayDriver;
    private Transform followTarget;
    private CinemachineThirdPersonFollow thirdPersonFollow;
    private LayerMask collisionMask;
    private float cameraRadius;
    private float safetySkin;
    private float focusHeightRatio;
    private float releaseDelay;
    private float recoveryDamping;
    private float minimumExternalDistance;

    internal CameraCollisionSolver.DebugSnapshot LastSnapshot
    {
        get { return solver.LastSnapshot; }
    }

    public void Configure(
        RE4RThirdPersonCamera driver,
        Transform target,
        CinemachineThirdPersonFollow follow,
        LayerMask mask,
        float radius,
        float skin,
        float heightRatio,
        float obstructionReleaseDelay,
        float obstructionRecoveryDamping,
        float externalDistance)
    {
        gameplayDriver = driver;
        followTarget = target;
        thirdPersonFollow = follow;
        collisionMask = mask;
        cameraRadius = Mathf.Max(0.05f, radius);
        safetySkin = Mathf.Max(0f, skin);
        focusHeightRatio = Mathf.Clamp(heightRatio, 0.5f, 1f);
        releaseDelay = Mathf.Max(0f, obstructionReleaseDelay);
        recoveryDamping = Mathf.Max(0.01f, obstructionRecoveryDamping);
        minimumExternalDistance = Mathf.Max(AbsoluteMinimumCameraDistance, externalDistance);
        ResetCollisionState();
        enabled = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetCollisionState();
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage == CinemachineCore.Stage.Finalize && gameplayDriver != null)
        {
            gameplayDriver.KeepUpperBodyInFrame(ref state, deltaTime);
            return;
        }

        if (stage != CinemachineCore.Stage.Body || followTarget == null || thirdPersonFollow == null)
        {
            return;
        }

        if (gameplayDriver != null)
        {
            Vector3 basePosition = state.GetCorrectedPosition();
            Vector3 raisedPosition = gameplayDriver.PlaceShoulderCamera(in state, basePosition, deltaTime, vcam.PreviousStateIsValid);
            state.RawPosition += raisedPosition - basePosition;
        }

        if (collisionMask.value == 0)
        {
            if (gameplayDriver != null)
            {
                gameplayDriver.SetCollisionCompressionRatio(0f);
            }
            return;
        }

        Vector3 up;
        if (state.ReferenceUp.sqrMagnitude > 0.001f)
        {
            up = state.ReferenceUp.normalized;
        }
        else
        {
            up = Vector3.up;
        }

        Vector3 focusPosition;
        if (gameplayDriver != null)
        {
            focusPosition = gameplayDriver.ReadStableCollisionFocusPosition(up);
        }
        else
        {
            float focusHeight = Mathf.Max(0f, thirdPersonFollow.VerticalArmLength) * focusHeightRatio;
            focusPosition = followTarget.position + up * focusHeight;
        }

        Vector3 desiredPosition = state.GetCorrectedPosition();
        float near = Mathf.Max(0.01f, state.Lens.NearClipPlane);
        float halfHeight = near * Mathf.Tan(state.Lens.FieldOfView * Mathf.Deg2Rad * 0.5f);
        float nearPlaneRadius = Mathf.Max(
            cameraRadius,
            Mathf.Sqrt(near * near + halfHeight * halfHeight * (1f + state.Lens.Aspect * state.Lens.Aspect)));

        CameraCollisionSolver.SolveSettings settings;
        Transform playerRoot = null;
        WindowTraversalPoint activeWindow = null;
        bool inPassage = false;
        float exitEaseWeight = 0f;
        float aimShoulderLockWeight = 0f;

        if (gameplayDriver != null)
        {
            settings = gameplayDriver.ReadCollisionSolverSettings();
            playerRoot = gameplayDriver.ControllerTransform;
            activeWindow = gameplayDriver.ActiveTraversalWindow;
            inPassage = gameplayDriver.PassageCameraWeight > 0.01f;
            exitEaseWeight = gameplayDriver.ExitEaseWeight;
            aimShoulderLockWeight = gameplayDriver.AimShoulderLockWeight;
        }
        else
        {
            settings = CameraCollisionSolver.SolveSettings.CreateDefault();
            settings.releaseDelay = releaseDelay;
            settings.normalRecoveryDamping = recoveryDamping;
        }

        float actualShoulderOffset = Mathf.Max(0f, Vector3.Dot(desiredPosition - focusPosition, followTarget.right));
        float effectiveDesiredShoulderOffset = thirdPersonFollow.ShoulderOffset.x;
        if (gameplayDriver != null && gameplayDriver.PistolFramingWeight > 0.001f)
        {
            effectiveDesiredShoulderOffset = Mathf.Lerp(
                effectiveDesiredShoulderOffset,
                actualShoulderOffset,
                gameplayDriver.PistolFramingWeight);
        }

        CameraCollisionSolver.SolveRequest request = new CameraCollisionSolver.SolveRequest
        {
            focusPosition = focusPosition,
            desiredPosition = desiredPosition,
            referenceUp = up,
            cameraRight = followTarget.right,
            desiredShoulderOffset = effectiveDesiredShoulderOffset,
            cameraRadius = cameraRadius,
            safetySkin = safetySkin,
            nearPlaneRadius = nearPlaneRadius,
            minimumDistance = MinimumDistance,
            collisionMask = collisionMask.value,
            playerRoot = playerRoot,
            activeWindow = activeWindow,
            inPassage = inPassage,
            exitEaseWeight = exitEaseWeight,
            deltaTime = deltaTime,
            resetState = !vcam.PreviousStateIsValid,
            aimShoulderLockWeight = aimShoulderLockWeight
        };

        Vector3 correctedPosition = solver.Solve(in request, in settings);
        if (gameplayDriver != null)
        {
            gameplayDriver.SetCollisionCompressionRatio(solver.CompressionRatio);
        }

        state.RawPosition += correctedPosition - desiredPosition;
    }

    private float MinimumDistance
    {
        get { return Mathf.Max(AbsoluteMinimumCameraDistance, minimumExternalDistance); }
    }

    private void ResetCollisionState()
    {
        solver.Reset();
        if (gameplayDriver != null)
        {
            gameplayDriver.SetCollisionCompressionRatio(0f);
        }
    }
}
