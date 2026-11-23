using UnityEngine;

/// <summary>
/// 敌人受击视觉反馈管理器 — 只负责生成血液特效和发布受击音效事件
/// 不再直接保存 AudioClip，改为引用 AudioEventData 配置资产
/// 音效通过 AudioManager 对象池播放
/// </summary>
public class EnemyHitEffectManager : Singleton<EnemyHitEffectManager>
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：身体/胸部受击音效事件。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    [Header("受击音效事件")]
    [Tooltip("身体/胸部受击音效事件")]
    [SerializeField]
    private AudioEventData bodyHitEvent;
    /// <summary>中文含义：爆头受击音效事件。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    [Tooltip("爆头受击音效事件")]
    [SerializeField]
    private AudioEventData headHitEvent;
    /// <summary>中文含义：四肢受击音效事件。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    [Tooltip("四肢受击音效事件")]
    [SerializeField]
    private AudioEventData limbHitEvent;
    // 缺失警告去重
    /// <summary>中文含义：是否身体命中已警告。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    private bool bodyHitWarned;
    /// <summary>中文含义：是否头部命中已警告。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    private bool headHitWarned;
    /// <summary>中文含义：是否肢体命中已警告。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</summary>
    private bool limbHitWarned;
    private readonly System.Collections.Generic.HashSet<GameObject> presentedTargets = new System.Collections.Generic.HashSet<GameObject>();
    private int lastPresentationFrame = -1;
    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnEnable()
    {
        CombatEventDispatcher.OnHitConfirmed += PresentHasResolveHit;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDisable()
    {
        CombatEventDispatcher.OnHitConfirmed -= PresentHasResolveHit;
    }

    /// <summary>PresentHasResolveHit 的业务处理入口；本方法直接依赖 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前命中结果。使用位置：作为 EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType) 的参数。</param>
    private void PresentHasResolveHit(HitResult result)
    {
        if (result.deferBloodPresentation)
        {
            return;
        }
        if (lastPresentationFrame != Time.frameCount)
        {
            lastPresentationFrame = Time.frameCount;
            presentedTargets.Clear();
        }

        // 同一枪的多颗弹丸仍独立结算伤害，表现按目标合并。
        if (presentedTargets.Add(result.target))
        {
            PlayBloodEffect(result.hitPoint, result.bloodDirection, result.hitboxType, !result.impactSoundHandled, result.bloodEffect);
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObjectPoolManager.Prewarm(GameObject, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        ChapterOneEffects.Prewarm();
        ExecutionGeometry.Preload();
    }

    /// <summary>
    /// 在命中位置生成血液特效 + 播放受击音效
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.ApplyDeadChainsawPulse()、EnemyCombatModule.TryApplyFriendlyFire(Vector3, Vector3, float)、EnemyHitEffectManager.PresentHasResolveHit(HitResult)、VillagerQTEParticipant.ApplyBodyTechniqueSplashDamage(float, Vector3, Transform)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、Debug.LogWarning(object)、GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、object.ReferenceEquals(object, object)、Quaternion.LookRotation(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、作为 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform) 的参数。</param>
    /// <param name="normal">中文含义：表面法线。使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</param>
    /// <param name="hitPart">中文含义：命中部件。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。</param>
    public void PlayBloodEffect(Vector3 position, Vector3 normal, HitboxType hitPart = HitboxType.Chest, bool playImpactSound = true,
        ChapterOneEffect bloodEffect = ChapterOneEffect.None)
    {
        if (bloodEffect == ChapterOneEffect.None)
        {
            bloodEffect = ChapterOneEffect.SlashBlood;
        }
        ChapterOneEffects.TryPlay(bloodEffect, position, normal);

        if (!playImpactSound)
        {
            return;
        }

        // 受击音效（根据命中部位选择事件）
        // eventData（事件配置）：使用位置：作为 AudioManager.Play3D(AudioEventData, Vector3) 的参数、EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。
AudioEventData eventData = hitPart switch
        {
            HitboxType.Head => headHitEvent,
            HitboxType.LeftArm or HitboxType.RightArm or HitboxType.LeftLeg or HitboxType.RightLeg => limbHitEvent,
            _ => bodyHitEvent
        };
        if (eventData != null)
        {
            // receiverValue7（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。
global::AudioManager receiverValue7 = AudioManager.Instance;
            // optionalValue8（条件访问得到的播放3距离的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyHitEffectManager.PlayBloodEffect(Vector3, Vector3, HitboxType)。
global::AudioPlayHandle? optionalValue8;
            if (!global::System.Object.ReferenceEquals(receiverValue7, null))
            {
                optionalValue8 = receiverValue7.Play3D(eventData, position);
            }
            else
            {
                optionalValue8 = default;
            }
        }
        else
        {
            // 空配置时输出一次警告
            switch (hitPart)
            {
                case HitboxType.Head:
                    if (!headHitWarned)
                    {
                        headHitWarned = true;
                        Debug.LogWarning("[受击音效] 爆头受击音效未配置，请在检视面板中绑定 AudioEventData。");
                    }

                    break;
                case HitboxType.LeftArm:
                case HitboxType.RightArm:
                case HitboxType.LeftLeg:
                case HitboxType.RightLeg:
                    if (!limbHitWarned)
                    {
                        limbHitWarned = true;
                        Debug.LogWarning("[受击音效] 四肢受击音效未配置，请在检视面板中绑定 AudioEventData。");
                    }

                    break;
                default:
                    if (!bodyHitWarned)
                    {
                        bodyHitWarned = true;
                        Debug.LogWarning("[受击音效] 身体受击音效未配置，请在检视面板中绑定 AudioEventData。");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// 只生成血液视觉特效，不播放敌人受击音效。
    /// 用于里昂快速反应事件失败等玩家侧受伤场景。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)；下游（含内部回调体） GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform)、GameObjectPoolManager.ReturnDelayed(GameObject, float)、object.ReferenceEquals(object, object)、Quaternion.LookRotation(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 GameObjectPoolManager.Get(GameObject, Vector3, Quaternion, Transform) 的参数。</param>
    /// <param name="normal">中文含义：表面法线。使用位置：作为 Quaternion.LookRotation(Vector3) 的参数、EnemyHitEffectManager.PlayBloodVisualOnly(Vector3, Vector3)。</param>
    public void PlayBloodVisualOnly(Vector3 position, Vector3 normal)
    {
        ChapterOneEffects.TryPlay(ChapterOneEffect.StabBlood, position, normal);
    }
}
