using UnityEngine;

/// <summary>摄像机与瞄准使用的 QTECameraFocusMode 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
public enum QTECameraFocusMode
{
    /// <summary>中文含义：双重角色上部一半身体。分类成员“双人上半身”，对应既有数值 0；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("双人上半身")]
    DualCharacterUpperHalfBody,
    /// <summary>中文含义：腿部撕咬交互中心。分类成员“咬腿互动中心”，对应既有数值 1；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("咬腿互动中心")]
    LegBiteInteractionCenter,
    /// <summary>中文含义：高腿部撕咬交互中心。分类成员“高位咬腿互动中心”，对应既有数值 2；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("高位咬腿互动中心")]
    HighLegBiteInteractionCenter,
    /// <summary>中文含义：颈部撕咬交互中心。分类成员“咬颈互动中心”，对应既有数值 3；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("咬颈互动中心")]
    NeckBiteInteractionCenter,
    /// <summary>中文含义：暗杀交互中心。分类成员“刺杀互动中心”，对应既有数值 4；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("刺杀互动中心")]
    AssassinationInteractionCenter,
    /// <summary>中文含义：前方破坏自由交互中心。分类成员“正面挣脱互动中心”，对应既有数值 5；供所属枚举的业务分支读取，不在可读性调整中重排。</summary>
    [UnityEngine.InspectorName("正面挣脱互动中心")]
    FrontBreakFreeInteractionCenter
}

/// <summary>摄像机与瞄准使用的共享配置资产 QTECameraProfile；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(fileName = "快速反应事件摄像机配置", menuName = "RE4/摄像机/快速反应事件摄像机配置")]
public sealed class QTECameraProfile : ScriptableObject
{
    /// <summary>入口持续时间。</summary>
    [Header("进入运镜")]
    [Min(0.01f)]
    public float entryDuration = 0.75f;
    /// <summary>入口曲线。</summary>
    public AnimationCurve entryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>入口样条位置。</summary>
    [Range(0f, 1f)]
    public float entrySplinePosition = 1f;
    /// <summary>入口样条结点索引。</summary>
    [Min(-1)]
    public int entryKnotIndex = -1;
    /// <summary>持有漂移持续时间。</summary>
    [Min(0.01f)]
    public float holdingDriftDuration = 0.01f;
    /// <summary>持有视野角度。</summary>
    [Range(1f, 120f)]
    public float holdingFieldOfView = 42f;
    /// <summary>焦点模式。</summary>
    [Header("双人构图")]
    public QTECameraFocusMode focusMode = QTECameraFocusMode.DualCharacterUpperHalfBody;
    /// <summary>玩家颈部混合。</summary>
    [Range(0f, 1f)]
    public float playerNeckBlend = 0.65f;
    /// <summary>敌人头部混合。</summary>
    [Range(0f, 1f)]
    public float enemyHeadBlend = 0.75f;
    /// <summary>敌人焦点权重。</summary>
    [Range(0f, 1f)]
    public float enemyFocusWeight = 0.46f;
    /// <summary>焦点偏移量。</summary>
    public Vector3 focusOffset = new Vector3(0f, -0.08f, 0f);
    /// <summary>焦点平滑时间。</summary>
    [Min(0f)]
    public float focusSmoothTime = 0.06f;
    /// <summary>腿部撕咬敌人头部权重。</summary>
    [Header("咬腿构图")]
    [Range(0f, 1f)]
    public float legBiteEnemyHeadWeight = 0.52f;
    /// <summary>小腿后备高度。</summary>
    [Min(0f)]
    public float lowerLegFallbackHeight = 0.55f;
    /// <summary>是否优先选择最近小腿。优先选择距离敌人头部更近的里昂小腿，并让镜头停留在该腿一侧。</summary>
    [Tooltip("优先选择距离敌人头部更近的里昂小腿，并让镜头停留在该腿一侧。")]
    public bool preferClosestLowerLeg = true;
    /// <summary>高角度大腿混合。</summary>
    [Header("高位咬腿构图")]
    [Range(0f, 1f)]
    public float highAngleThighBlend = 0.38f;
    /// <summary>高角度敌人上半身权重。</summary>
    [Range(0f, 1f)]
    public float highAngleEnemyUpperBodyWeight = 0.42f;
    /// <summary>高角度撕咬腿部侧边偏移量。</summary>
    [Min(0f)]
    public float highAngleBiteLegSideOffset = 0.12f;
    /// <summary>高角度成功玩家权重。咬腿成功时把观察点重新偏向玩家，避免被推开的敌人把镜头带离主体。</summary>
    [Tooltip("咬腿成功时把观察点重新偏向玩家，避免被推开的敌人把镜头带离主体")]
    [Range(0f, 1f)]
    public float highAngleSuccessPlayerWeight = 0.68f;
    /// <summary>后侧撕咬敌人头部权重。</summary>
    [Header("背后咬颈构图")]
    [Range(0f, 1f)]
    public float rearBiteEnemyHeadWeight = 0.56f;
    /// <summary>后侧撕咬失败焦点偏移量。</summary>
    public Vector3 rearBiteFailureFocusOffset = new Vector3(0f, -0.08f, -0.04f);
    /// <summary>失败预备持续时间。</summary>
    [Min(0.01f)]
    public float failureAnticipationDuration = 0.35f;
    /// <summary>失败环绕轨道持续时间。</summary>
    [Min(0.01f)]
    public float failureOrbitDuration = 0.7f;
    /// <summary>失败环绕轨道曲线。</summary>
    public AnimationCurve failureOrbitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>失败预备行进。</summary>
    [Range(0f, 1f)]
    public float failureAnticipationTravel = 0.08f;
    /// <summary>成功侧边偏移量。</summary>
    [Min(0f)]
    public float successSideOffset = 0.12f;
    /// <summary>动态入口切线缩放。</summary>
    [Min(0f)]
    public float dynamicEntryTangentScale = 0.32f;
    /// <summary>侧边偏好分值容差。</summary>
    [Min(0)]
    public int sidePreferenceScoreTolerance = 2;
    /// <summary>阻挡路径分值。</summary>
    [Min(1)]
    public int blockedPathScore = 12;
    /// <summary>正面抓取敌人面权重。</summary>
    [Header("正面挣脱构图")]
    [Range(0f, 1f)]
    public float frontGrabEnemyFaceWeight = 0.56f;
    /// <summary>正面抓取失败敌人权重。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailureEnemyWeight = 0.72f;
    /// <summary>正面抓取失败焦点偏移量。</summary>
    public Vector3 frontGrabFailureFocusOffset = new Vector3(0f, -0.07f, 0.04f);
    /// <summary>正面抓取失败样条结点索引。</summary>
    [Min(0)]
    public int frontGrabFailureKnotIndex = 3;
    /// <summary>正面抓取失败压力持续时间。</summary>
    [Min(0.01f)]
    public float frontGrabFailurePressureDuration = 0.28f;
    /// <summary>正面抓取失败压力行进。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailurePressureTravel = 0.24f;
    /// <summary>正面抓取失败环绕轨道持续时间。</summary>
    [Min(0.01f)]
    public float frontGrabFailureOrbitDuration = 0.52f;
    /// <summary>正面抓取失败前置接触行进。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailurePreContactTravel = 0.84f;
    /// <summary>正面抓取失败接触持续时间。</summary>
    [Min(0.01f)]
    public float frontGrabFailureContactDuration = 0.14f;
    /// <summary>正面抓取失败视野角度。</summary>
    [Range(1f, 120f)]
    public float frontGrabFailureFieldOfView = 40f;
    /// <summary>暗杀敌人颈部权重。</summary>
    [Header("刺杀构图")]
    [Range(0f, 1f)]
    public float assassinationEnemyNeckWeight = 0.64f;
    /// <summary>暗杀之后接触玩家权重。</summary>
    [Range(0f, 1f)]
    public float assassinationAfterContactPlayerWeight = 0.28f;
    /// <summary>暗杀最小焦点高度。</summary>
    [Min(0f)]
    public float assassinationMinimumFocusHeight = 0.92f;
    /// <summary>暗杀之后接触焦点偏移量。</summary>
    public Vector3 assassinationAfterContactFocusOffset = new Vector3(0f, -0.08f, 0.04f);
    /// <summary>暗杀接触样条结点索引。</summary>
    [Min(0)]
    public int assassinationContactKnotIndex = 3;
    /// <summary>暗杀后续反馈样条结点索引。</summary>
    [Min(0)]
    public int assassinationAftermathKnotIndex = 4;
    /// <summary>暗杀接触持续时间。</summary>
    [Min(0.01f)]
    public float assassinationContactDuration = 0.18f;
    /// <summary>暗杀后续反馈持续时间。</summary>
    [Min(0.01f)]
    public float assassinationAftermathDuration = 0.72f;
    /// <summary>暗杀接触视野角度。</summary>
    [Range(1f, 120f)]
    public float assassinationContactFieldOfView = 40f;
    /// <summary>暗杀后续反馈视野角度。</summary>
    [Range(1f, 120f)]
    public float assassinationAftermathFieldOfView = 46f;
    /// <summary>环境遮罩。</summary>
    [Header("轨迹预检")]
    public LayerMask environmentMask = 1;
    /// <summary>路径探测半径。</summary>
    [Min(0.01f)]
    public float pathProbeRadius = 0.18f;
    /// <summary>路径探测采样集合。</summary>
    [Range(4, 64)]
    public int pathProbeSamples = 16;
    /// <summary>成功持续时间。</summary>
    [Header("成功表现")]
    [Min(0.01f)]
    public float successDuration = 0.45f;
    /// <summary>成功拉近后方距离。</summary>
    [Min(0f)]
    public float successPullBackDistance = 0.22f;
    /// <summary>成功竖直偏移量。</summary>
    public float successVerticalOffset = 0.05f;
    /// <summary>成功视野角度。</summary>
    [Range(1f, 120f)]
    public float successFieldOfView = 48f;
    /// <summary>失败持续时间。</summary>
    [Header("失败表现")]
    [Min(0.01f)]
    public float failureDuration = 0.25f;
    /// <summary>失败推开内部距离。</summary>
    [Min(0f)]
    public float failurePushInDistance = 0.12f;
    /// <summary>失败竖直偏移量。</summary>
    public float failureVerticalOffset = -0.03f;
    /// <summary>失败视野角度。</summary>
    [Range(1f, 120f)]
    public float failureFieldOfView = 38f;
    /// <summary>是否启用摄像机反馈。</summary>
    [Header("镜头反馈")]
    public bool enableCameraFeedback;
    /// <summary>抓取冲击延迟。</summary>
    [Min(0f)]
    public float grabImpactDelay = 0.12f;
    /// <summary>抓取冲击侧边偏移量。</summary>
    [Min(0f)]
    public float grabImpactSideOffset = 0.035f;
    /// <summary>抓取冲击竖直偏移量。</summary>
    [Min(0f)]
    public float grabImpactVerticalOffset = 0.018f;
    /// <summary>抓取冲击镜头侧倾。</summary>
    [Range(0f, 5f)]
    public float grabImpactDutch = 1.5f;
    /// <summary>抓取冲击持续时间。</summary>
    [Min(0.01f)]
    public float grabImpactDuration = 0.15f;
    /// <summary>撕咬冲击侧边偏移量。</summary>
    [Min(0f)]
    public float biteImpactSideOffset = 0.045f;
    /// <summary>撕咬冲击竖直偏移量。</summary>
    [Min(0f)]
    public float biteImpactVerticalOffset = 0.025f;
    /// <summary>撕咬冲击镜头侧倾。</summary>
    [Range(0f, 5f)]
    public float biteImpactDutch = 1.35f;
    /// <summary>撕咬冲击持续时间。</summary>
    [Min(0.01f)]
    public float biteImpactDuration = 0.14f;
    /// <summary>撕咬接触稳定持续时间。</summary>
    [Min(0.01f)]
    public float biteContactSettleDuration = 0.08f;
    /// <summary>暗杀冲击侧边偏移量。</summary>
    [Min(0f)]
    public float assassinationImpactSideOffset = 0.055f;
    /// <summary>暗杀冲击竖直偏移量。</summary>
    [Min(0f)]
    public float assassinationImpactVerticalOffset = 0.028f;
    /// <summary>暗杀冲击镜头侧倾。</summary>
    [Range(0f, 5f)]
    public float assassinationImpactDutch = 1.8f;
    /// <summary>暗杀冲击持续时间。</summary>
    [Min(0.01f)]
    public float assassinationImpactDuration = 0.16f;
    /// <summary>保持噪声位置。</summary>
    [Min(0f)]
    public float holdNoisePosition = 0.008f;
    /// <summary>保持噪声镜头侧倾。</summary>
    [Range(0f, 5f)]
    public float holdNoiseDutch = 0.25f;
    /// <summary>保持噪声频率。</summary>
    [Min(0f)]
    public float holdNoiseFrequency = 1.7f;
    /// <summary>踢击冲击侧边偏移量。</summary>
    [Min(0f)]
    public float kickImpactSideOffset = 0.065f;
    /// <summary>踢击冲击镜头侧倾。</summary>
    [Range(0f, 6f)]
    public float kickImpactDutch = 2.4f;
    /// <summary>踢击冲击延迟。</summary>
    [Min(0f)]
    public float kickImpactDelay = 0.1f;
    /// <summary>踢击冲击持续时间。</summary>
    [Min(0.01f)]
    public float kickImpactDuration = 0.12f;
    /// <summary>反馈恢复持续时间。</summary>
    [Min(0.01f)]
    public float feedbackRecoveryDuration = 0.32f;
}
