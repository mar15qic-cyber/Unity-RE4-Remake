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
    /// <summary>中文含义：入口持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("进入运镜")]
    [Min(0.01f)]
    public float entryDuration = 0.75f;
    /// <summary>中文含义：入口曲线。使用位置：QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)、QTECameraPresenter.UpdateMotion()。</summary>
    public AnimationCurve entryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>中文含义：入口样条位置。使用位置：QTECameraPresenter.BeginEntry()、QTECameraPresenter.HandleSessionStarted(int, QTEKind)。</summary>
    [Range(0f, 1f)]
    public float entrySplinePosition = 1f;
    /// <summary>中文含义：入口样条结点索引。使用位置：作为 QTECameraRig.GetNormalizedPositionAtKnot(int) 的参数、QTECameraPresenter.BeginEntry()。</summary>
    [Min(-1)]
    public int entryKnotIndex = -1;
    /// <summary>中文含义：持有漂移持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float holdingDriftDuration = 0.01f;
    /// <summary>中文含义：持有视野角度。使用位置：作为 QTECameraRig.SetFieldOfView(float) 的参数、QTECameraPresenter.BeginEntry()、QTECameraPresenter.HandleSessionStarted(int, QTEKind)。</summary>
    [Range(1f, 120f)]
    public float holdingFieldOfView = 42f;
    /// <summary>中文含义：焦点模式。使用位置：QTECameraPresenter.GetPreferredPath(QTECameraRig, QTECameraProfile)、QTECameraPresenter.UpdateFocusTarget(bool)。</summary>
    [Header("双人构图")]
    public QTECameraFocusMode focusMode = QTECameraFocusMode.DualCharacterUpperHalfBody;
    /// <summary>中文含义：玩家颈部混合。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float playerNeckBlend = 0.65f;
    /// <summary>中文含义：敌人头部混合。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float enemyHeadBlend = 0.75f;
    /// <summary>中文含义：敌人焦点权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float enemyFocusWeight = 0.46f;
    /// <summary>中文含义：焦点偏移量。使用位置：作为 Transform.TransformVector(Vector3) 的参数。</summary>
    public Vector3 focusOffset = new Vector3(0f, -0.08f, 0f);
    /// <summary>中文含义：焦点平滑时间。使用位置：作为 Vector3.SmoothDamp(Vector3, Vector3, ref Vector3, float, float, float) 的参数、QTECameraPresenter.ApplyFocusPosition(Vector3, QTECameraProfile, bool)。</summary>
    [Min(0f)]
    public float focusSmoothTime = 0.06f;
    /// <summary>中文含义：腿部撕咬敌人头部权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Header("咬腿构图")]
    [Range(0f, 1f)]
    public float legBiteEnemyHeadWeight = 0.52f;
    /// <summary>中文含义：小腿后备高度。使用位置：作为 QTECameraPresenter.GetBonePosition(Transform, Transform, Transform, float) 的参数。</summary>
    [Min(0f)]
    public float lowerLegFallbackHeight = 0.55f;
    /// <summary>中文含义：是否优先选择最近小腿。优先选择距离敌人头部更近的里昂小腿，并让镜头停留在该腿一侧。使用位置：QTECameraPresenter.SelectLowerLegIfNeeded(QTECameraProfile)。</summary>
    [Tooltip("优先选择距离敌人头部更近的里昂小腿，并让镜头停留在该腿一侧。")]
    public bool preferClosestLowerLeg = true;
    /// <summary>中文含义：高角度大腿混合。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Header("高位咬腿构图")]
    [Range(0f, 1f)]
    public float highAngleThighBlend = 0.38f;
    /// <summary>中文含义：高角度敌人上半身权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float highAngleEnemyUpperBodyWeight = 0.42f;
    /// <summary>中文含义：高角度撕咬腿部侧边偏移量。使用位置：QTECameraPresenter.UpdateHighAngleLegBiteFocusTarget(QTECameraProfile, bool)。</summary>
    [Min(0f)]
    public float highAngleBiteLegSideOffset = 0.12f;
    /// <summary>中文含义：高角度成功玩家权重。咬腿成功时把观察点重新偏向玩家，避免被推开的敌人把镜头带离主体。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Tooltip("咬腿成功时把观察点重新偏向玩家，避免被推开的敌人把镜头带离主体")]
    [Range(0f, 1f)]
    public float highAngleSuccessPlayerWeight = 0.68f;
    /// <summary>中文含义：后侧撕咬敌人头部权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Header("背后咬颈构图")]
    [Range(0f, 1f)]
    public float rearBiteEnemyHeadWeight = 0.56f;
    /// <summary>中文含义：后侧撕咬失败焦点偏移量。使用位置：作为 Transform.TransformVector(Vector3) 的参数。</summary>
    public Vector3 rearBiteFailureFocusOffset = new Vector3(0f, -0.08f, -0.04f);
    /// <summary>中文含义：失败预备持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float failureAnticipationDuration = 0.35f;
    /// <summary>中文含义：失败环绕轨道持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float failureOrbitDuration = 0.7f;
    /// <summary>中文含义：失败环绕轨道曲线。使用位置：QTECameraPresenter.UpdateMotion()。</summary>
    public AnimationCurve failureOrbitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>中文含义：失败预备行进。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float failureAnticipationTravel = 0.08f;
    /// <summary>中文含义：成功侧边偏移量。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    [Min(0f)]
    public float successSideOffset = 0.12f;
    /// <summary>中文含义：动态入口切线缩放。使用位置：作为 QTECameraRig.ConfigureDynamicEntry(Vector3, float) 的参数。</summary>
    [Min(0f)]
    public float dynamicEntryTangentScale = 0.32f;
    /// <summary>中文含义：侧边偏好分值容差。使用位置：QTECameraPresenter.BeginEntry()。</summary>
    [Min(0)]
    public int sidePreferenceScoreTolerance = 2;
    /// <summary>中文含义：阻挡路径分值。使用位置：QTECameraPresenter.BeginEntry()。</summary>
    [Min(1)]
    public int blockedPathScore = 12;
    /// <summary>中文含义：正面抓取敌人面权重。使用位置：QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)。</summary>
    [Header("正面挣脱构图")]
    [Range(0f, 1f)]
    public float frontGrabEnemyFaceWeight = 0.56f;
    /// <summary>中文含义：正面抓取失败敌人权重。使用位置：QTECameraPresenter.UpdateFrontGrabFocusTarget(QTECameraProfile, bool)。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailureEnemyWeight = 0.72f;
    /// <summary>中文含义：正面抓取失败焦点偏移量。使用位置：作为 Transform.TransformVector(Vector3) 的参数。</summary>
    public Vector3 frontGrabFailureFocusOffset = new Vector3(0f, -0.07f, 0.04f);
    /// <summary>中文含义：正面抓取失败样条结点索引。使用位置：作为 QTECameraRig.GetNormalizedPositionAtKnot(int) 的参数。</summary>
    [Min(0)]
    public int frontGrabFailureKnotIndex = 3;
    /// <summary>中文含义：正面抓取失败压力持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float frontGrabFailurePressureDuration = 0.28f;
    /// <summary>中文含义：正面抓取失败压力行进。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailurePressureTravel = 0.24f;
    /// <summary>中文含义：正面抓取失败环绕轨道持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float frontGrabFailureOrbitDuration = 0.52f;
    /// <summary>中文含义：正面抓取失败前置接触行进。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float frontGrabFailurePreContactTravel = 0.84f;
    /// <summary>中文含义：正面抓取失败接触持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float frontGrabFailureContactDuration = 0.14f;
    /// <summary>中文含义：正面抓取失败视野角度。使用位置：QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HandleBiteContact(int)。</summary>
    [Range(1f, 120f)]
    public float frontGrabFailureFieldOfView = 40f;
    /// <summary>中文含义：暗杀敌人颈部权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Header("刺杀构图")]
    [Range(0f, 1f)]
    public float assassinationEnemyNeckWeight = 0.64f;
    /// <summary>中文含义：暗杀之后接触玩家权重。使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数。</summary>
    [Range(0f, 1f)]
    public float assassinationAfterContactPlayerWeight = 0.28f;
    /// <summary>中文含义：暗杀最小焦点高度。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0f)]
    public float assassinationMinimumFocusHeight = 0.92f;
    /// <summary>中文含义：暗杀之后接触焦点偏移量。使用位置：作为 Transform.TransformVector(Vector3) 的参数。</summary>
    public Vector3 assassinationAfterContactFocusOffset = new Vector3(0f, -0.08f, 0.04f);
    /// <summary>中文含义：暗杀接触样条结点索引。使用位置：作为 QTECameraRig.GetNormalizedPositionAtKnot(int) 的参数。</summary>
    [Min(0)]
    public int assassinationContactKnotIndex = 3;
    /// <summary>中文含义：暗杀后续反馈样条结点索引。使用位置：作为 QTECameraRig.GetNormalizedPositionAtKnot(int) 的参数。</summary>
    [Min(0)]
    public int assassinationAftermathKnotIndex = 4;
    /// <summary>中文含义：暗杀接触持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float assassinationContactDuration = 0.18f;
    /// <summary>中文含义：暗杀后续反馈持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float assassinationAftermathDuration = 0.72f;
    /// <summary>中文含义：暗杀接触视野角度。使用位置：QTECameraPresenter.HandleAssassinationContact(int)。</summary>
    [Range(1f, 120f)]
    public float assassinationContactFieldOfView = 40f;
    /// <summary>中文含义：暗杀后续反馈视野角度。使用位置：QTECameraPresenter.UpdateMotion()。</summary>
    [Range(1f, 120f)]
    public float assassinationAftermathFieldOfView = 46f;
    /// <summary>中文含义：环境遮罩。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</summary>
    [Header("轨迹预检")]
    public LayerMask environmentMask = 1;
    /// <summary>中文含义：路径探测半径。使用位置：作为 Physics.OverlapSphereNonAlloc(Vector3, float, Collider[], int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</summary>
    [Min(0.01f)]
    public float pathProbeRadius = 0.18f;
    /// <summary>中文含义：路径探测采样集合。使用位置：作为 Mathf.Max(int, int) 的参数。</summary>
    [Range(4, 64)]
    public int pathProbeSamples = 16;
    /// <summary>中文含义：成功持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("成功表现")]
    [Min(0.01f)]
    public float successDuration = 0.45f;
    /// <summary>中文含义：成功拉近后方距离。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    [Min(0f)]
    public float successPullBackDistance = 0.22f;
    /// <summary>中文含义：成功竖直偏移量。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    public float successVerticalOffset = 0.05f;
    /// <summary>中文含义：成功视野角度。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    [Range(1f, 120f)]
    public float successFieldOfView = 48f;
    /// <summary>中文含义：失败持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Header("失败表现")]
    [Min(0.01f)]
    public float failureDuration = 0.25f;
    /// <summary>中文含义：失败推开内部距离。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    [Min(0f)]
    public float failurePushInDistance = 0.12f;
    /// <summary>中文含义：失败竖直偏移量。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    public float failureVerticalOffset = -0.03f;
    /// <summary>中文含义：失败视野角度。使用位置：QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.HandleBiteContact(int)。</summary>
    [Range(1f, 120f)]
    public float failureFieldOfView = 38f;
    /// <summary>中文含义：是否启用摄像机反馈。使用位置：QTECameraPresenter.BeginEntry()、QTECameraPresenter.BeginResult(bool)、QTECameraPresenter.UpdateCameraFeedback()。</summary>
    [Header("镜头反馈")]
    public bool enableCameraFeedback;
    /// <summary>中文含义：抓取冲击延迟。使用位置：QTECameraPresenter.BeginEntry()。</summary>
    [Min(0f)]
    public float grabImpactDelay = 0.12f;
    /// <summary>中文含义：抓取冲击侧边偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float grabImpactSideOffset = 0.035f;
    /// <summary>中文含义：抓取冲击竖直偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float grabImpactVerticalOffset = 0.018f;
    /// <summary>中文含义：抓取冲击镜头侧倾。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Range(0f, 5f)]
    public float grabImpactDutch = 1.5f;
    /// <summary>中文含义：抓取冲击持续时间。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Min(0.01f)]
    public float grabImpactDuration = 0.15f;
    /// <summary>中文含义：撕咬冲击侧边偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float biteImpactSideOffset = 0.045f;
    /// <summary>中文含义：撕咬冲击竖直偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float biteImpactVerticalOffset = 0.025f;
    /// <summary>中文含义：撕咬冲击镜头侧倾。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Range(0f, 5f)]
    public float biteImpactDutch = 1.35f;
    /// <summary>中文含义：撕咬冲击持续时间。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Min(0.01f)]
    public float biteImpactDuration = 0.14f;
    /// <summary>中文含义：撕咬接触稳定持续时间。使用位置：作为 Mathf.Max(float, float) 的参数。</summary>
    [Min(0.01f)]
    public float biteContactSettleDuration = 0.08f;
    /// <summary>中文含义：暗杀冲击侧边偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float assassinationImpactSideOffset = 0.055f;
    /// <summary>中文含义：暗杀冲击竖直偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float assassinationImpactVerticalOffset = 0.028f;
    /// <summary>中文含义：暗杀冲击镜头侧倾。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Range(0f, 5f)]
    public float assassinationImpactDutch = 1.8f;
    /// <summary>中文含义：暗杀冲击持续时间。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Min(0.01f)]
    public float assassinationImpactDuration = 0.16f;
    /// <summary>中文含义：保持噪声位置。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float holdNoisePosition = 0.008f;
    /// <summary>中文含义：保持噪声镜头侧倾。使用位置：QTECameraPresenter.UpdateCameraFeedback()。</summary>
    [Range(0f, 5f)]
    public float holdNoiseDutch = 0.25f;
    /// <summary>中文含义：保持噪声频率。使用位置：QTECameraPresenter.UpdateCameraFeedback()。</summary>
    [Min(0f)]
    public float holdNoiseFrequency = 1.7f;
    /// <summary>中文含义：踢击冲击侧边偏移量。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</summary>
    [Min(0f)]
    public float kickImpactSideOffset = 0.065f;
    /// <summary>中文含义：踢击冲击镜头侧倾。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Range(0f, 6f)]
    public float kickImpactDutch = 2.4f;
    /// <summary>中文含义：踢击冲击延迟。使用位置：QTECameraPresenter.BeginResult(bool)。</summary>
    [Min(0f)]
    public float kickImpactDelay = 0.1f;
    /// <summary>中文含义：踢击冲击持续时间。使用位置：作为 QTECameraPresenter.AddImpactFeedback(ref Vector3, ref float, float, float, Vector3, float, float) 的参数。</summary>
    [Min(0.01f)]
    public float kickImpactDuration = 0.12f;
    /// <summary>中文含义：反馈恢复持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数。</summary>
    [Min(0.01f)]
    public float feedbackRecoveryDuration = 0.32f;
}
