using UnityEngine;

/// <summary>
/// ChainsawMan 专属动画桥接组件。
/// 只管理 ChainsawStart 和 TauntTrigger 专属参数。
/// 不写 VillagerController 管理的通用参数（ATK/HIT/DIE/MoveSpeed 等）。
/// 电锯音效以“启动后持续运转”为核心，攻击、受击等状态只临时切走，状态结束后回到待机循环。
/// </summary>
[DisallowMultipleComponent]
public class ChainsawManAnimationBridge : MonoBehaviour
{
    /// <summary>电锯起始的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashChainsawStart = Animator.StringToHash("ChainsawStart");
    /// <summary>挑衅触发器的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashTauntTrigger = Animator.StringToHash("TauntTrigger");
    /// <summary>挑衅状态的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashTauntState = Animator.StringToHash("Base Layer.Taunt_Stomp");
    /// <summary>挣脱状态（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int struggleState = Animator.StringToHash("Base Layer.PullStart");
    /// <summary>是否原地威吓中间。</summary>
    private float chainsawStartedTime;
    internal bool InPlaceIntimidationMiddle
    {
        get
        {
            if (!anim)
            {
                return false;
            }

            // 正式原版步态在启动与入战威慑之间保持待机，等行为树交还追击；超过安全时间窗则自动解除避免永久停步。
            if (!anim.isHuman && HasStartedChainsaw && !HasResolvedIntroTaunt && Time.time - chainsawStartedTime <= 3.0f)
            {
                return true;
            }

            // 当前。
var current = anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
            // 后续动作。
int successor;
            if (anim.IsInTransition(0))
            {
                successor = anim.GetNextAnimatorStateInfo(0).fullPathHash;
            }
            else
            {
                successor = 0;
            }

            return current == struggleState || current == HashTauntState || successor == struggleState || successor == HashTauntState;
        }
    }

    /// <summary>攻击索引的哈希编号（动画参数或状态路径的哈希编号）。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly int HashAttackIndex = Animator.StringToHash("AttackIndex");
    /// <summary>最小介绍挑衅距离。</summary>
    [Header("入战威慑")]
    [Min(0f)]
    public float minimumIntroTauntDistance = 3.5f;
    /// <summary>挑衅完成归一化时间。</summary>
    [Range(0.5f, 0.99f)]
    public float tauntCompleteNormalizedTime = 0.9f;
    /// <summary>远端追逐移动速度。</summary>
    [Header("追猎节奏")]
    [Range(0.5f, 1.5f)]
    public float farChaseMoveSpeed = 1.25f;
    /// <summary>远端追逐距离。</summary>
    [Min(0f)]
    public float farChaseDistance = 7f;
    /// <summary>狂暴生命比例。</summary>
    [Range(0f, 1f)]
    public float enragedHealthRatio = 0.45f;
    /// <summary>狂暴移动速度额外加成。</summary>
    [Range(0f, 0.5f)]
    public float enragedMoveSpeedBonus = 0.1f;
    /// <summary>关闭攻击距离。</summary>
    [Header("攻击选择")]
    [Min(0f)]
    public float closeAttackDistance = 1f;
    /// <summary>关闭完整旋转概率。</summary>
    [Range(0f, 1f)]
    public float closeFullSpinChance = 0.45f;
    /// <summary>双重挥击概率。</summary>
    [Range(0f, 1f)]
    public float doubleSwingChance = 0.35f;
    /// <summary>双重挥击伤害倍率。</summary>
    [Header("攻击伤害")]
    [Min(0f)]
    public float doubleSwingDamageMultiplier = 0.55f;
    /// <summary>完整旋转伤害倍率。</summary>
    [Min(0f)]
    public float fullSpinDamageMultiplier = 1.15f;
    /// <summary>电锯起始事件。</summary>
    [Header("电锯音效")]
    public AudioEventData chainsawStartEvent;
    /// <summary>电锯待机循环事件。</summary>
    public AudioEventData chainsawIdleLoopEvent;
    /// <summary>电锯反向事件。</summary>
    public AudioEventData chainsawRevEvent;
    /// <summary>电锯攻击事件。</summary>
    public AudioEventData chainsawAttackEvent;
    /// <summary>电锯命中事件。</summary>
    public AudioEventData chainsawHitEvent;
    [Tooltip("锯齿接触的原版音效，与武器受击分开")]
    public AudioEventData chainsawContactEvent;
    /// <summary>电锯停止事件。</summary>
    public AudioEventData chainsawStopEvent;
    /// <summary>电锯伤害原点。</summary>
    [Header("电锯伤害原点")]
    public Transform chainsawDamageOrigin;
    /// <summary>后备伤害原点偏移量。</summary>
    public Vector3 fallbackDamageOriginOffset = new Vector3(0f, 1f, 0f);
    /// <summary>动画表现。</summary>
    [Header("动画表现引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("动画表现")]
    [UnityEngine.InspectorName("动画表现")]
    public EnemyAnimationAdapter animationPresenter;
    /// <summary>动画控制器。</summary>
    private Animator anim;
    /// <summary>是否挑衅状态已进入。</summary>
    private bool tauntStateEntered;
    /// <summary>上一次攻击索引。</summary>
    private int lastAttackIndex = -1;
    /// <summary>电锯循环来源。</summary>
    private AudioSource chainsawLoopSource;
    /// <summary>电锯状态来源。</summary>
    private AudioSource chainsawStateSource;
    /// <summary>电锯循环句柄。</summary>
    private AudioPlayHandle chainsawLoopHandle;
    /// <summary>电锯状态句柄。</summary>
    private AudioPlayHandle chainsawStateHandle;
    /// <summary>死亡关闭协程。</summary>
    private Coroutine deathShutdownRoutine;
    /// <summary>是否电锯已停止。</summary>
    private bool chainsawStopped = true;
    /// <summary>临时电锯音频是否生效。</summary>
    private bool temporaryChainsawAudioActive;
    /// <summary>中文含义：是否具有已开始电锯。电锯是否已经启动完成。</summary>
    public bool HasStartedChainsaw { get; private set; }
    /// <summary>中文含义：是否起始动作结束。本次启动电锯动画是否已经完成。</summary>
    public bool StartActionFinished { get; private set; }
    /// <summary>中文含义：是否具有解算后的介绍挑衅。入战威慑是否已经播放或因距离过近而跳过。</summary>
    public bool HasResolvedIntroTaunt { get; private set; }
    /// <summary>中文含义：是否挑衅动作结束。本次威慑动作是否已经完成。</summary>
    public bool TauntActionFinished { get; private set; }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.EnsureChainsawLoopSource()、ChainsawManAnimationBridge.ResetRuntimeState()、ChainsawManAnimationBridge.ResolveChainsawDamageOrigin()、Component.GetComponent&lt;Animator&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        anim = GetComponent<Animator>();
        ResolveChainsawDamageOrigin();
        EnsureChainsawLoopSource();
        ResetRuntimeState();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.ResetRuntimeState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        // 如果是池化复用场景，重置状态
        ResetRuntimeState();
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.RegisterExternalSource(chainsawLoopSource);
            audioManager.RegisterExternalSource(chainsawStateSource);
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.UnregisterExternalSource(AudioSource)、ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        if (deathShutdownRoutine != null)
        {
            StopCoroutine(deathShutdownRoutine);
            deathShutdownRoutine = null;
        }

        StopTemporaryChainsawAudio(false);
        StopIdleLoop(false, true);
        if (chainsawLoopSource != null)
        {
            // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue2 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.UnregisterExternalSource(chainsawLoopSource);
            }
        }

        if (chainsawStateSource != null)
        {
            // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue3 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.UnregisterExternalSource(chainsawStateSource);
            }
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.UpdateChainsawAudioPosition()、ChainsawManAnimationBridge.UpdateTemporaryChainsawAudio()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.IsAudioPaused)
        {
            return;
        }

        UpdateChainsawAudioPosition();
        UpdateTemporaryChainsawAudio();
    }

    /// <summary>
    /// 由 ExecuteChainsawStart BT Task 调用。
    /// 停止移动并设置 Trigger 播放 PullStart 动画。
    /// </summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawStart.OnStart()；下游（含内部回调体） ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)、ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、EnemyAnimationAdapter.TriggerPresentation(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void BeginChainsawStart()
    {
        if (anim == null)
        {
            return;
        }

        if (HasStartedChainsaw && !chainsawStopped)
        {
            StartActionFinished = true;
            StartIdleLoop();
            return;
        }

        chainsawStopped = false;
        StopTemporaryChainsawAudio(false);
        StartActionFinished = false;
        var nativeAudio = GetComponentInParent<NativeActionAudio>();
        if (nativeAudio == null || !nativeAudio.isActiveAndEnabled || !nativeAudio.nativeChainsawIgnition)
        {
            PlayOneShot(chainsawStartEvent, AudioEventNames.ChainsawStart);
        }
        animationPresenter.TriggerPresentation(HashChainsawStart);
    }

    /// <summary>
    /// 由 BT Task 在被中断时调用。
    /// 清除 Trigger，不标记为已完成。
    /// </summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawStart.OnEnd()；下游（含内部回调体） EnemyAnimationAdapter.ClearTrigger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelChainsawStart()
    {
        if (anim == null)
        {
            return;
        }

        animationPresenter.ClearTrigger(HashChainsawStart);
        StartActionFinished = false;
    }

    /// <summary>
    /// 由 ChainsawStartSMB 在动画接近完成时通过 SendMessage 调用。
    /// </summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawStart.OnUpdate()；下游（含内部回调体） ChainsawManAnimationBridge.StartIdleLoop(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnChainsawStartComplete()
    {
        if (StartActionFinished)
        {
            return;
        }

        StartActionFinished = true;
        HasStartedChainsaw = true;
        chainsawStartedTime = Time.time;
        chainsawStopped = false;
        StartIdleLoop();
    }

    /// <summary>
    /// 池化复用时重置全部状态。
    /// </summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.OnEnable()、ChainsawManAnimationBridge.Start()；下游（含内部回调体） ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、EnemyAnimationAdapter.ClearTrigger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetRuntimeState()
    {
        StopTemporaryChainsawAudio(false);
        StopIdleLoop(false, true);
        HasStartedChainsaw = false;
        StartActionFinished = false;
        HasResolvedIntroTaunt = false;
        TauntActionFinished = false;
        tauntStateEntered = false;
        lastAttackIndex = -1;
        if (anim != null)
        {
            animationPresenter.ClearTrigger(HashChainsawStart);
            animationPresenter.ClearTrigger(HashTauntTrigger);
        }
    }

    /// <summary>
    /// 开始一次入战威慑。距离过近时直接标记为已处理，避免敌人在玩家脸前白送输出窗口。
    /// </summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawIntroTaunt.OnStart()；下游（含内部回调体） ChainsawManAnimationBridge.CompleteIntroTaunt()、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、EnemyAnimationAdapter.ClearTrigger(int)、EnemyAnimationAdapter.TriggerPresentation(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerDistance">玩家距离。</param>
    public void BeginIntroTaunt(float playerDistance)
    {
        TauntActionFinished = false;
        tauntStateEntered = false;
        if (anim == null || playerDistance < minimumIntroTauntDistance)
        {
            CompleteIntroTaunt();
            return;
        }

        PlayTemporaryChainsawAudio(chainsawRevEvent, AudioEventNames.ChainsawRev);
        animationPresenter.ClearTrigger(HashTauntTrigger);
        animationPresenter.TriggerPresentation(HashTauntTrigger);
    }

    /// <summary>轮询威慑动画状态，兼容没有动画事件的现有动作资源。</summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawIntroTaunt.OnUpdate()；下游（含内部回调体） Animator.GetCurrentAnimatorStateInfo(int)、Animator.IsInTransition(int)、ChainsawManAnimationBridge.CompleteIntroTaunt()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool UpdateIntroTaunt()
    {
        if (TauntActionFinished || anim == null)
        {
            return true;
        }

        // 当前动画状态信息。
AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        // 是否内部挑衅。
bool isInTaunt = stateInfo.fullPathHash == HashTauntState;
        if (isInTaunt)
        {
            tauntStateEntered = true;
            if (stateInfo.normalizedTime >= tauntCompleteNormalizedTime)
            {
                CompleteIntroTaunt();
            }
        }
        else if (tauntStateEntered && !anim.IsInTransition(0))
        {
            CompleteIntroTaunt();
        }

        return TauntActionFinished;
    }

    /// <summary>CompleteIntroTaunt 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResumeIdleLoop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginIntroTaunt(float)、ChainsawManAnimationBridge.UpdateIntroTaunt()、ExecuteChainsawIntroTaunt.OnUpdate()；下游（含内部回调体） ChainsawManAnimationBridge.ResumeIdleLoop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteIntroTaunt()
    {
        if (TauntActionFinished)
        {
            return;
        }

        if (anim != null)
        {
            animationPresenter.ClearTrigger(HashTauntTrigger);
        }
        TauntActionFinished = true;
        HasResolvedIntroTaunt = true;
        ResumeIdleLoop();
    }

    internal void MarkIntroTauntResolvedAfterTraversal()
    {
        if (anim == null || anim.isHuman || !HasStartedChainsaw || HasResolvedIntroTaunt)
        {
            return;
        }

        if (animationPresenter != null)
        {
            animationPresenter.ClearTrigger(HashTauntTrigger);
        }

        TauntActionFinished = true;
        HasResolvedIntroTaunt = true;
        ResumeIdleLoop();
    }

    /// <summary>CancelIntroTaunt 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResumeIdleLoop()、EnemyAnimationAdapter.ClearTrigger(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ExecuteChainsawIntroTaunt.OnEnd()；下游（含内部回调体） ChainsawManAnimationBridge.ResumeIdleLoop()、EnemyAnimationAdapter.ClearTrigger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CancelIntroTaunt()
    {
        if (anim != null)
        {
            animationPresenter.ClearTrigger(HashTauntTrigger);
        }

        TauntActionFinished = false;
        tauntStateEntered = false;
        ResumeIdleLoop();
    }

    /// <summary>根据距离挑选四种电锯招式，并尽量避免连续重复。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.BeginAttack()；下游（含内部回调体） Mathf.Max(int, int)、Random.Range(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playerDistance">玩家距离。</param>
    /// <param name="attackCount">攻击数量。</param>
    public int SelectAttackIndex(float playerDistance, int attackCount)
    {
        // 安全数量。
int safeCount = Mathf.Max(1, attackCount);
        if (safeCount == 1)
        {
            return 0;
        }

        // 选定索引。
int selectedIndex;
        if (safeCount >= 4)
        {
            // 随机值。
float randomValue = Random.value;
            if (playerDistance <= closeAttackDistance)
            {
                if (randomValue < closeFullSpinChance)
                {
                    selectedIndex = 3;
                }
                else if (randomValue < closeFullSpinChance + doubleSwingChance)
                {
                    selectedIndex = 2;
                }
                else
                {
                    selectedIndex = Random.Range(0, 2);
                }
            }
            else if (randomValue < doubleSwingChance)
            {
                selectedIndex = 2;
            }
            else
            {
                selectedIndex = Random.Range(0, 2);
            }
        }
        else
        {
            selectedIndex = Random.Range(0, safeCount);
        }

        if (selectedIndex == lastAttackIndex)
        {
            selectedIndex = (selectedIndex + Random.Range(1, safeCount)) % safeCount;
        }

        lastAttackIndex = selectedIndex;
        return selectedIndex;
    }

    /// <summary>双挥采用两次较轻伤害，回旋挥击采用一次重伤害。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） Animator.GetInteger(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseDamage">基础伤害。</param>
    public float ResolveAttackDamage(float baseDamage)
    {
        if (anim == null)
        {
            return baseDamage;
        }

        // 攻击索引。
int attackIndex = anim.GetInteger(HashAttackIndex);
        if (attackIndex == 2)
        {
            return baseDamage * doubleSwingDamageMultiplier;
        }

        if (attackIndex == 3)
        {
            return baseDamage * fullSpinDamageMultiplier;
        }

        return baseDamage;
    }

    /// <summary>远距离加速追猎，低生命阶段进一步提高压迫速度。</summary>
    /// <remarks>静态调用链：上游 VillagerController.GetTargetMoveSpeed()；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="baseMoveSpeed">基础移动速度。</param>
    /// <param name="playerDistance">玩家距离。</param>
    /// <param name="healthRatio">生命比例。</param>
    public float ResolveChaseMoveSpeed(float baseMoveSpeed, float playerDistance, float healthRatio)
    {
        // 解算后的速度。
float resolvedSpeed;
        if (playerDistance >= farChaseDistance)
        {
            resolvedSpeed = Mathf.Max(baseMoveSpeed, farChaseMoveSpeed);
        }
        else
        {
            resolvedSpeed = baseMoveSpeed;
        }

        if (healthRatio <= enragedHealthRatio)
        {
            resolvedSpeed += enragedMoveSpeedBonus;
        }

        return Mathf.Clamp(resolvedSpeed, 0.5f, 1.5f);
    }

    /// <summary>PlayChainsawHit 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayChainsawHit()
    {
        var sound = chainsawContactEvent;
        if (sound == null)
        {
            sound = chainsawHitEvent;
        }
        PlayOneShot(sound, AudioEventNames.ChainsawHit);
    }

    /// <summary>OnChainsawAttackAudioEnter 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnChainsawAttackAudioEnter()
    {
        PlayTemporaryChainsawAudio(chainsawAttackEvent, AudioEventNames.ChainsawAttack);
    }

    /// <summary>OnChainsawHitAudioEnter 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnChainsawHitAudioEnter()
    {
        PlayTemporaryChainsawAudio(chainsawHitEvent, AudioEventNames.ChainsawHit);
    }

    /// <summary>OnChainsawActionAudioExit 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResumeIdleLoop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.ResumeIdleLoop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnChainsawActionAudioExit()
    {
        ResumeIdleLoop();
    }

    /// <summary>StopChainsawLoop 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playStop">是否播放停止。</param>
    public void StopChainsawLoop(bool playStop)
    {
        StopTemporaryChainsawAudio(false);
        StopIdleLoop(playStop, true);
        HasStartedChainsaw = false;
    }

    /// <summary>BeginDeathShutdown 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.DeathShutdownRoutine(float)、ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） ChainsawManAnimationBridge.DeathShutdownRoutine(float)、ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="duration">持续时间。</param>
    public void BeginDeathShutdown(float duration)
    {
        if (deathShutdownRoutine != null)
        {
            StopCoroutine(deathShutdownRoutine);
        }

        StopTemporaryChainsawAudio(false);
        HasStartedChainsaw = true;
        chainsawStopped = false;
        StartIdleLoop(true);
        deathShutdownRoutine = StartCoroutine(DeathShutdownRoutine(duration));
    }

    /// <summary>GetChainsawDamageOrigin 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResolveChainsawDamageOrigin()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.UpdateChainsawAudioPosition()、EnemyCombatModule.ApplyDeadChainsawPulse() 等共 6 处；下游（含内部回调体） ChainsawManAnimationBridge.ResolveChainsawDamageOrigin()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public Vector3 GetChainsawDamageOrigin()
    {
        if (chainsawDamageOrigin == null)
        {
            ResolveChainsawDamageOrigin();
        }

        if (chainsawDamageOrigin != null)
        {
            return chainsawDamageOrigin.position;
        }
        else
        {
            return transform.position + fallbackDamageOriginOffset;
        }
    }

    /// <summary>GetChainsawDamageForward 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResolveChainsawDamageOrigin()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TryApplyAttackHit(int)；下游（含内部回调体） ChainsawManAnimationBridge.ResolveChainsawDamageOrigin()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public Vector3 GetChainsawDamageForward()
    {
        if (anim != null && !anim.isHuman)
        {
            return transform.forward;
        }
        if (chainsawDamageOrigin == null)
        {
            ResolveChainsawDamageOrigin();
        }

        // 前向。
Vector3 forward;
        if (chainsawDamageOrigin != null)
        {
            forward = chainsawDamageOrigin.forward;
        }
        else
        {
            forward = transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            return forward.normalized;
        }
        else
        {
            return Vector3.forward;
        }
    }

    /// <summary>PlayOneShot 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、ChainsawManAnimationBridge.GetChainsawDamageOrigin()、ChainsawManAnimationBridge.ResolveAudioEvent(AudioEventData, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginChainsawStart()、ChainsawManAnimationBridge.PlayChainsawHit()、ChainsawManAnimationBridge.StopIdleLoop(bool, bool)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、ChainsawManAnimationBridge.GetChainsawDamageOrigin()、ChainsawManAnimationBridge.ResolveAudioEvent(AudioEventData, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    /// <param name="eventName">事件名称。</param>
    private void PlayOneShot(AudioEventData eventData, string eventName)
    {
        eventData = ResolveAudioEvent(eventData, eventName);
        if (eventData == null || AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.Play3D(eventData, GetChainsawDamageOrigin());
    }

    /// <summary>StartIdleLoop 的业务处理入口；本方法直接依赖 AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip()、AudioManager.Play3D(AudioEventData, Vector3)、AudioSource.Play()、ChainsawManAnimationBridge.EnsureChainsawLoopSource()、ChainsawManAnimationBridge.GetChainsawDamageOrigin() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginChainsawStart()、ChainsawManAnimationBridge.BeginDeathShutdown(float)、ChainsawManAnimationBridge.OnChainsawStartComplete()、ChainsawManAnimationBridge.ResumeIdleLoop()；下游（含内部回调体） AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip()、AudioManager.Play3D(AudioEventData, Vector3)、AudioSource.Play()、ChainsawManAnimationBridge.EnsureChainsawLoopSource()、ChainsawManAnimationBridge.GetChainsawDamageOrigin() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="force">是否力度。</param>
    private void StartIdleLoop(bool force = false)
    {
        if (!force && (!HasStartedChainsaw || chainsawStopped || temporaryChainsawAudioActive))
        {
            return;
        }

        EnsureChainsawLoopSource();
        // 事件配置。
AudioEventData eventData = ResolveAudioEvent(chainsawIdleLoopEvent, AudioEventNames.ChainsawIdleLoop);
        if (eventData == null || chainsawLoopHandle.IsValid || (chainsawLoopSource != null && chainsawLoopSource.isPlaying))
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            chainsawLoopHandle = AudioManager.Instance.Play3D(eventData, GetChainsawDamageOrigin());
            if (chainsawLoopHandle.IsValid)
            {
                return;
            }
        }

        if (AudioManager.Instance != null && AudioManager.Instance.IsAudioPaused)
        {
            return;
        }

        if (chainsawLoopSource == null)
        {
            return;
        }

        // 音频片段。
AudioClip clip = eventData.SelectClip();
        if (clip == null)
        {
            return;
        }

        chainsawLoopSource.clip = clip;
        chainsawLoopSource.volume = eventData.GetVolume();
        chainsawLoopSource.pitch = eventData.GetPitch();
        chainsawLoopSource.priority = eventData.priority;
        chainsawLoopSource.minDistance = eventData.minDistance;
        chainsawLoopSource.maxDistance = eventData.maxDistance;
        chainsawLoopSource.rolloffMode = eventData.rolloffMode;
        chainsawLoopSource.loop = true;
        chainsawLoopSource.spatialBlend = 1f;
        chainsawLoopSource.dopplerLevel = 0f;
        chainsawLoopSource.Play();
    }

    /// <summary>StopIdleLoop 的业务处理入口；本方法直接依赖 AudioManager.Stop(AudioPlayHandle)、AudioSource.Stop()、ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.DeathShutdownRoutine(float)、ChainsawManAnimationBridge.OnDisable()、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.ResetRuntimeState()、ChainsawManAnimationBridge.StopChainsawLoop(bool)；下游（含内部回调体） AudioManager.Stop(AudioPlayHandle)、AudioSource.Stop()、ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="playStop">是否播放停止。</param>
    /// <param name="markStopped">是否标记已停止。</param>
    private void StopIdleLoop(bool playStop, bool markStopped = false)
    {
        if (chainsawLoopHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(chainsawLoopHandle);
        }

        chainsawLoopHandle = default;
        if (chainsawLoopSource != null)
        {
            chainsawLoopSource.Stop();
        }

        if (playStop)
        {
            PlayOneShot(chainsawStopEvent, AudioEventNames.ChainsawStop);
        }

        if (markStopped)
        {
            chainsawStopped = true;
        }
    }

    /// <summary>PlayTemporaryChainsawAudio 的业务处理入口；本方法直接依赖 AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip()、AudioManager.Play3D(AudioEventData, Vector3)、AudioSource.Play()、ChainsawManAnimationBridge.EnsureChainsawStateSource()、ChainsawManAnimationBridge.GetChainsawDamageOrigin() 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginIntroTaunt(float)、ChainsawManAnimationBridge.OnChainsawAttackAudioEnter()、ChainsawManAnimationBridge.OnChainsawHitAudioEnter()；下游（含内部回调体） AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip()、AudioManager.Play3D(AudioEventData, Vector3)、AudioSource.Play()、ChainsawManAnimationBridge.EnsureChainsawStateSource()、ChainsawManAnimationBridge.GetChainsawDamageOrigin() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    /// <param name="eventName">事件名称。</param>
    private void PlayTemporaryChainsawAudio(AudioEventData eventData, string eventName)
    {
        if (!HasStartedChainsaw || chainsawStopped)
        {
            return;
        }

        eventData = ResolveAudioEvent(eventData, eventName);
        if (eventData == null)
        {
            return;
        }

        StopTemporaryChainsawAudio(false);
        if (chainsawContactEvent == null)
        {
            StopIdleLoop(false);
        }
        temporaryChainsawAudioActive = true;
        if (AudioManager.Instance != null)
        {
            chainsawStateHandle = AudioManager.Instance.Play3D(eventData, GetChainsawDamageOrigin());
            if (chainsawStateHandle.IsValid)
            {
                return;
            }
        }

        if (AudioManager.Instance != null && AudioManager.Instance.IsAudioPaused)
        {
            temporaryChainsawAudioActive = false;
            ResumeIdleLoop();
            return;
        }

        EnsureChainsawStateSource();
        // 音频片段。
AudioClip clip = eventData.SelectClip();
        if (clip == null)
        {
            temporaryChainsawAudioActive = false;
            ResumeIdleLoop();
            return;
        }

        chainsawStateSource.clip = clip;
        chainsawStateSource.volume = eventData.GetVolume();
        chainsawStateSource.pitch = eventData.GetPitch();
        chainsawStateSource.priority = eventData.priority;
        chainsawStateSource.minDistance = eventData.minDistance;
        chainsawStateSource.maxDistance = eventData.maxDistance;
        chainsawStateSource.rolloffMode = eventData.rolloffMode;
        chainsawStateSource.loop = eventData.loop;
        chainsawStateSource.spatialBlend = 1f;
        chainsawStateSource.dopplerLevel = 0f;
        chainsawStateSource.Play();
    }

    /// <summary>StopTemporaryChainsawAudio 的业务处理入口；本方法直接依赖 AudioManager.Stop(AudioPlayHandle)、AudioSource.Stop()、ChainsawManAnimationBridge.ResumeIdleLoop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginChainsawStart()、ChainsawManAnimationBridge.BeginDeathShutdown(float)、ChainsawManAnimationBridge.OnDisable()、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.ResetRuntimeState() 等共 7 处；下游（含内部回调体） AudioManager.Stop(AudioPlayHandle)、AudioSource.Stop()、ChainsawManAnimationBridge.ResumeIdleLoop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="resumeIdle">是否恢复执行待机。</param>
    private void StopTemporaryChainsawAudio(bool resumeIdle)
    {
        if (chainsawStateHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(chainsawStateHandle);
        }

        chainsawStateHandle = default;
        if (chainsawStateSource != null)
        {
            chainsawStateSource.Stop();
        }

        temporaryChainsawAudioActive = false;
        if (resumeIdle)
        {
            ResumeIdleLoop();
        }
    }

    /// <summary>ResumeIdleLoop 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.CancelIntroTaunt()、ChainsawManAnimationBridge.CompleteIntroTaunt()、ChainsawManAnimationBridge.OnChainsawActionAudioExit()、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool) 等共 6 处；下游（含内部回调体） ChainsawManAnimationBridge.StartIdleLoop(bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResumeIdleLoop()
    {
        if (!HasStartedChainsaw || chainsawStopped)
        {
            return;
        }

        StopTemporaryChainsawAudio(false);
        StartIdleLoop();
    }

    /// <summary>EnsureChainsawStateSource 的业务处理入口；本方法直接依赖 AudioManager.RegisterExternalSource(AudioSource)、GameObject.AddComponent&lt;AudioSource&gt;()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)；下游（含内部回调体） AudioManager.RegisterExternalSource(AudioSource)、GameObject.AddComponent&lt;AudioSource&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureChainsawStateSource()
    {
        if (chainsawStateSource != null)
        {
            return;
        }

        EnsureChainsawLoopSource();
        chainsawStateSource = chainsawLoopSource.gameObject.AddComponent<AudioSource>();
        chainsawStateSource.playOnAwake = false;
        chainsawStateSource.loop = false;
        chainsawStateSource.spatialBlend = 1f;
        chainsawStateSource.dopplerLevel = 0f;
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue8 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.RegisterExternalSource(chainsawStateSource);
        }
    }

    /// <summary>EnsureChainsawLoopSource 的业务处理入口；本方法直接依赖 AudioManager.RegisterExternalSource(AudioSource)、GameObject.AddComponent&lt;AudioSource&gt;()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.Start()、ChainsawManAnimationBridge.StartIdleLoop(bool)；下游（含内部回调体） AudioManager.RegisterExternalSource(AudioSource)、GameObject.AddComponent&lt;AudioSource&gt;()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsureChainsawLoopSource()
    {
        if (chainsawLoopSource != null)
        {
            return;
        }

        // 备用音源跟随刀链。独立的音源节点让角色根位姿继续由运动执行器控制。
        var audioEmitter = new GameObject("ChainsawAudio");
        audioEmitter.transform.SetParent(transform, false);
        audioEmitter.transform.position = GetChainsawDamageOrigin();
        chainsawLoopSource = audioEmitter.AddComponent<AudioSource>();
        chainsawLoopSource.playOnAwake = false;
        chainsawLoopSource.loop = true;
        chainsawLoopSource.spatialBlend = 1f;
        chainsawLoopSource.dopplerLevel = 0f;
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue9 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.RegisterExternalSource(chainsawLoopSource);
        }
    }

    /// <summary>UpdateChainsawAudioPosition 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.GetChainsawDamageOrigin()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.LateUpdate()；下游（含内部回调体） ChainsawManAnimationBridge.GetChainsawDamageOrigin()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateChainsawAudioPosition()
    {
        Vector3 origin = GetChainsawDamageOrigin();
        if (chainsawLoopHandle.IsValid && chainsawLoopHandle.source != null)
        {
            chainsawLoopHandle.source.transform.position = origin;
        }

        if (chainsawStateHandle.IsValid && chainsawStateHandle.source != null)
        {
            chainsawStateHandle.source.transform.position = origin;
        }

        if (chainsawLoopSource != null)
        {
            chainsawLoopSource.transform.position = origin;
        }
    }

    /// <summary>UpdateTemporaryChainsawAudio 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.ResumeIdleLoop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.Update()；下游（含内部回调体） ChainsawManAnimationBridge.ResumeIdleLoop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateTemporaryChainsawAudio()
    {
        if (!temporaryChainsawAudioActive)
        {
            return;
        }

        // 是否状态句柄播放中。
bool stateHandlePlaying = chainsawStateHandle.IsValid;
        // 是否状态来源播放中。
bool stateSourcePlaying = chainsawStateSource != null && chainsawStateSource.isPlaying;
        if (stateHandlePlaying || stateSourcePlaying)
        {
            return;
        }

        temporaryChainsawAudioActive = false;
        ResumeIdleLoop();
    }

    /// <summary>ResolveAudioEvent 的业务处理入口；本方法直接依赖 AudioLibrary.GetEvent(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.StartIdleLoop(bool)；下游（含内部回调体） AudioLibrary.GetEvent(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    /// <param name="eventName">事件名称。</param>
    private AudioEventData ResolveAudioEvent(AudioEventData eventData, string eventName)
    {
        if (eventData != null)
        {
            return eventData;
        }

        if (AudioManager.Instance == null || AudioManager.Instance.library == null || string.IsNullOrEmpty(eventName))
        {
            return null;
        }

        return AudioManager.Instance.library.GetEvent(eventName);
    }

    /// <summary>ResolveChainsawDamageOrigin 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Transform&gt;(bool)、string.Contains(string)、string.ToLowerInvariant()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.GetChainsawDamageForward()、ChainsawManAnimationBridge.GetChainsawDamageOrigin()、ChainsawManAnimationBridge.Start()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Transform&gt;(bool)、string.Contains(string)、string.ToLowerInvariant()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResolveChainsawDamageOrigin()
    {
        if (chainsawDamageOrigin != null)
        {
            return;
        }

        // 子对象集合。
Transform[] children = GetComponentsInChildren<Transform>(true);
        // 当前遍历索引。
for (int i = 0; i < children.Length; i++)
        {
            // 子级。
Transform child = children[i];
            if (child == null)
            {
                continue;
            }

            // 子级名称。
string childName = child.name.ToLowerInvariant();
            if (childName.Contains("chainsaw") || childName.Contains("saw") || childName.Contains("电锯"))
            {
                chainsawDamageOrigin = child;
                return;
            }
        }
    }

    /// <summary>DeathShutdownRoutine 的业务处理入口；本方法直接依赖 ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.BeginDeathShutdown(float)；下游（含内部回调体） ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、Mathf.Max(float, float)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="duration">持续时间。</param>
    private System.Collections.IEnumerator DeathShutdownRoutine(float duration)
    {
        // 等待时间。
float waitTime = Mathf.Max(0f, duration);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }

        StopIdleLoop(true, true);
        HasStartedChainsaw = false;
        deathShutdownRoutine = null;
    }
}
