using UnityEngine;

/// <summary>配对动作使用的共享配置资产 LeonQTEConfig；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
[CreateAssetMenu(fileName = "里昂正面抓取配置", menuName = "战斗/快速反应事件/里昂正面抓取配置")]
public class LeonQTEConfig : ScriptableObject
{
    [Tooltip("正式场景通过验收的原版配对镜头。")]
    public NativeQTECameraTracks nativeCameras;
    [Tooltip("启用原版正面抓取双人动画与镜头。")]
    public bool useNativeFrontGrab;
    [Tooltip("背抱使用原版的双人进入、抓持和挣脱动作。")]
    public bool useNativeRearGrab;
    [Tooltip("启用原版站姿与蹲姿暗杀双人动画与镜头。")]
    public bool useNativeAssassination;
    [Tooltip("补刀使用原版双人动作、根轨和镜头；缺少资源时禁止进入。")]
    public bool useNativeGroundFinisher;
    [Tooltip("补刀开始距离，以当前项目的米制角色空间检查。")]
    public float groundFinisherDistance = 1.65f;
    /// <summary>中文含义：所需按下次数。使用位置：作为 Mathf.Min(int, int) 的参数、作为 QTEProgressInfo.QTEProgressInfo(int, int, int, float, float) 的参数、LeonQTECoordinator.SubmitEscapePress(int)。</summary>
    [Header("输入")]
    [Min(1)]
    public int requiredPresses = 8;

    [Header("背后环抱")]
    [Min(.1f)] public float rearHoldSeconds = 1.5f;
    [Range(0f, .1f)] public float rearDrainPerSecond = .02f;
    public AnimationClip rearPushStagger;
    /// <summary>中文含义：输入持续时间。使用位置：作为 QTEProgressInfo.QTEProgressInfo(int, int, int, float, float) 的参数、LeonQTECoordinator.ChangePhase(QTEPhase)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)。</summary>
    [Min(0.1f)]
    public float inputDuration = 3f;
    /// <summary>中文含义：撕咬伤害。使用位置：LeonQTEConfig.GetVariant(QTEKind)、LeonQTECoordinator.CommitBiteDamage(int)。</summary>
    [Header("失败伤害")]
    [Min(0f)]
    public float biteDamage = 20f;
    /// <summary>中文含义：最大起始距离。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Header("发起验证")]
    [Min(0.1f)]
    public float maximumStartDistance = 2.1f;
    /// <summary>中文含义：最大敌人朝向角度。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Range(0f, 180f)]
    public float maximumEnemyFacingAngle = 55f;
    /// <summary>中文含义：最大玩家朝向角度。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Range(0f, 180f)]
    public float maximumPlayerFacingAngle = 80f;
    /// <summary>中文含义：最大高度差值。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Min(0f)]
    public float maximumHeightDifference = 0.35f;
    /// <summary>中文含义：暗杀起始距离。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Header("背刺刺杀")]
    [Min(0.1f)]
    public float assassinationStartDistance = 2.1f;
    /// <summary>中文含义：暗杀后侧角度。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Range(0f, 180f)]
    public float assassinationRearAngle = 45f;
    /// <summary>中文含义：暗杀玩家朝向角度。使用位置：LeonQTECoordinator.ValidatePairing(IEnemyQTEParticipant, QTEKind, out QTECancelReason)。</summary>
    [Range(0f, 180f)]
    public float assassinationPlayerFacingAngle = 100f;
    /// <summary>中文含义：村民敌人暗杀伤害。使用位置：LeonQTECoordinator.ResolveAssassinationDamage(VillagerController)。</summary>
    [Min(0f)]
    public float ganadoAssassinationDamage = 9999f;
    /// <summary>中文含义：电锯敌人暗杀伤害。使用位置：LeonQTECoordinator.ResolveAssassinationDamage(VillagerController)。</summary>
    [Min(0f)]
    public float sawmanAssassinationDamage = 85f;
    /// <summary>中文含义：暗杀玩家释放归一化时间。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</summary>
    [Range(0.5f, 1f)]
    public float assassinationPlayerReleaseNormalizedTime = 0.98f;
    /// <summary>中文含义：配对中距离。使用位置：LeonExpandedQTESetupUtility.Build()、LeonQTEConfig.GetVariant(QTEKind)、LeonQTECoordinator.AlignAndEnter(int)。</summary>
    [Header("配对对齐")]
    [Min(0.2f)]
    public float pairingDistance = 0.68f;
    /// <summary>中文含义：对齐状态持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.AlignAndEnter(int)。</summary>
    [Min(0.01f)]
    public float alignmentDuration = 0.12f;
    /// <summary>中文含义：净空半径。使用位置：作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、LeonQTECoordinator.IsPairingSpaceBlocked(Vector3, IEnemyQTEParticipant)。</summary>
    [Min(0.1f)]
    public float clearanceRadius = 0.3f;
    /// <summary>中文含义：净空遮罩。使用位置：作为 Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数。</summary>
    public LayerMask clearanceMask = ~0;
    /// <summary>中文含义：玩家分离量距离。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。</summary>
    [Header("结果分离")]
    [Min(0f)]
    public float playerSeparationDistance = 0.55f;
    /// <summary>中文含义：敌人分离量距离。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。</summary>
    [Min(0f)]
    public float enemySeparationDistance = 0.45f;
    /// <summary>中文含义：分离量持续时间。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.SeparateAndFinish(int)。</summary>
    [Min(0.01f)]
    public float separationDuration = 0.18f;
    /// <summary>中文含义：成功推开期间里昂向后移动的距离。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。</summary>
    [Header("结果动画实体位移")]
    [Tooltip("成功推开期间里昂向后移动的距离")]
    [Min(0f)]
    public float successPlayerMotionDistance = 0.18f;
    /// <summary>中文含义：成功推开期间敌人被推离的距离。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。</summary>
    [Tooltip("成功推开期间敌人被推离的距离")]
    [Min(0f)]
    public float successEnemyMotionDistance = 0.65f;
    /// <summary>中文含义：失败咬颈期间双方共同后移的距离。使用位置：作为 LeonQTECoordinator.GetSafeSeparationDistance(Vector3, Vector3, float, IEnemyQTEParticipant) 的参数。</summary>
    [Tooltip("失败咬颈期间双方共同后移的距离")]
    [Min(0f)]
    public float failurePairMotionDistance = 0.45f;
    /// <summary>中文含义：结果位移随动画进度的平滑曲线。使用位置：作为 Mathf.Clamp01(float) 的参数、LeonQTECoordinator.ApplyResultMotion()。</summary>
    [Tooltip("结果位移随动画进度的平滑曲线")]
    public AnimationCurve resultMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    /// <summary>中文含义：成功玩家恢复归一化时间。成功动画中里昂提前退出的标准化时间点（约对应3秒动画的1.65秒处）。使用位置：LeonQTECoordinator.ResolveSuccessPlayerReleaseNormalizedTime()。</summary>
    [Header("成功恢复与退出保护")]
    [Tooltip("成功动画中里昂提前退出的标准化时间点（约对应3秒动画的1.65秒处）")]
    [Range(0.45f, 0.75f)]
    public float successPlayerRecoveryNormalizedTime = 0.55f;
    /// <summary>中文含义：里昂恢复点退出请求后等待实际退出的安全超时（秒）。使用位置：作为 Mathf.Max(float, float) 的参数、LeonQTECoordinator.Update()。</summary>
    [Tooltip("里昂恢复点退出请求后等待实际退出的安全超时（秒）")]
    [Min(0.1f)]
    public float playerExitSafetyTimeout = 0.35f;
    /// <summary>中文含义：暗杀成功提交后等待自动释放里昂的最短时间（秒）。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、作为 Mathf.Max(float, float) 的参数。</summary>
    [Tooltip("暗杀成功提交后等待自动释放里昂的最短时间（秒）")]
    [Min(0.05f)]
    public float assassinationSuccessForceExitDelay = 0.35f;
    /// <summary>中文含义：敌人收到结束请求后等待实际退出的安全超时（秒）。使用位置：作为 Mathf.Max(float, float) 的参数、LeonQTECoordinator.Update()。</summary>
    [Tooltip("敌人收到结束请求后等待实际退出的安全超时（秒）")]
    [Min(0.1f)]
    public float enemyExitSafetyTimeout = 0.5f;
    /// <summary>中文含义：成功后双方最小水平距离，不足时只补足敌人一侧。使用位置：LeonQTECoordinator.HandleEnemySuccessExit(int)。</summary>
    [Tooltip("成功后双方最小水平距离，不足时只补足敌人一侧")]
    [Min(0f)]
    public float minimumSuccessFinalDistance = 1.2f;
    /// <summary>中文含义：动画安全性超时。使用位置：LeonQTECoordinator.Update()。</summary>
    [Header("故障保护")]
    [Min(2f)]
    public float animationSafetyTimeout = 12f;
    /// <summary>中文含义：变体集合。使用位置：LeonExpandedQTESetupUtility.Build()、LeonQTEConfig.GetVariant(QTEKind)。</summary>
    [Header("快速反应事件变体")]
    public QTEAnimationVariant[] variants;

    [Tooltip("地面补刀或暗杀实际刺中时播放的小刀命中声。")]
    public AudioEventData knifeContactAudio;

    [Tooltip("正面擒抱和背后抱挣脱成功时的身体动作声。")]
    public AudioEventData grabEscapeAudio;
    /// <summary>GetVariant 的业务处理入口；本方法直接依赖 QTEAnimationVariant.QTEAnimationVariant()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.CanStart(QTEKind, IEnemyQTEParticipant, out QTECancelReason)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） QTEAnimationVariant.QTEAnimationVariant()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="kind">中文含义：种类。使用位置：LeonQTEConfig.GetVariant(QTEKind)。</param>
    public QTEAnimationVariant GetVariant(QTEKind kind)
    {
        if (variants != null)
        {
            // variant（变体）：每轮从变体集合取得一个元素，仅代表当前这一项。使用位置：LeonQTEConfig.GetVariant(QTEKind)。
foreach (var variant in variants)
            {
                if (variant != null && variant.kind == kind)
                {
                    return variant;
                }
            }
        }

        if (kind == QTEKind.GroundFinisher && useNativeGroundFinisher)
        {
            return new QTEAnimationVariant
            {
                kind = kind,
                playerIsAttacker = true,
                automaticSuccess = true,
                singleShot = true,
                failureDamage = 0f
            };
        }

        if (kind != QTEKind.FrontGrab)
        {
            return null;
        }

        return new QTEAnimationVariant
        {
            kind = QTEKind.FrontGrab,
            pairingDistance = pairingDistance,
            failureDamage = biteDamage
        };
    }
}
