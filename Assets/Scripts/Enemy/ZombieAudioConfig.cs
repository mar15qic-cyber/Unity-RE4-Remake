using UnityEngine;

/// <summary>
/// Zombie 音效配置组件 — 仅保存 Inspector 可拖拽的 AudioClip 引用
/// 不自动播放，不触发 AI/FSM/Damage/Animator
/// 使用 ContextMenu 进行编辑器试听
/// </summary>
public class ZombieAudioConfig : MonoBehaviour
{
    // ═══════════════════════════════════════════════
    //  攻击类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：普通攻击音效（挥击/抓）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.AttackSounds.get。</summary>
    [Header("Attack — 攻击")]
    [Tooltip("普通攻击音效（挥击/抓）")]
    [SerializeField]
    private AudioClip[] attackSounds;
    /// <summary>中文含义：撕咬音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.BiteSounds.get。</summary>
    [Tooltip("撕咬音效")]
    [SerializeField]
    private AudioClip[] biteSounds;
    /// <summary>中文含义：蓄力攻击音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.ChargedAttackSounds.get。</summary>
    [Tooltip("蓄力攻击音效")]
    [SerializeField]
    private AudioClip[] chargedAttackSounds;
    // ═══════════════════════════════════════════════
    //  受击类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：普通受击/伤害音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.HurtSounds.get。</summary>
    [Header("Hit — 受击")]
    [Tooltip("普通受击/伤害音效")]
    [SerializeField]
    private AudioClip[] hurtSounds;
    /// <summary>中文含义：爆头受击音效（hit_head）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.HeadshotSounds.get。</summary>
    [Tooltip("爆头受击音效（hit_head）")]
    [SerializeField]
    private AudioClip[] headshotSounds;
    /// <summary>中文含义：肢体命中音效列表。四肢受击音效（hit_leg / hit_shoulder / hit_spine）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.LimbHitSounds.get。</summary>
    [Tooltip("四肢受击音效（hit_leg / hit_shoulder / hit_spine）")]
    [SerializeField]
    private AudioClip[] limbHitSounds;
    /// <summary>中文含义：痛苦叫声（pain）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.PainSounds.get。</summary>
    [Tooltip("痛苦叫声（pain）")]
    [SerializeField]
    private AudioClip[] painSounds;
    // ═══════════════════════════════════════════════
    //  死亡类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：死亡音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.DeathSounds.get。</summary>
    [Header("Death — 死亡")]
    [Tooltip("死亡音效")]
    [SerializeField]
    private AudioClip[] deathSounds;
    /// <summary>中文含义：窒息/掐喉音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.ChokeSounds.get。</summary>
    [Tooltip("窒息/掐喉音效")]
    [SerializeField]
    private AudioClip[] chokeSounds;
    /// <summary>中文含义：血腥/残暴音效（gory）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.GorySounds.get。</summary>
    [Tooltip("血腥/残暴音效（gory）")]
    [SerializeField]
    private AudioClip[] gorySounds;
    // ═══════════════════════════════════════════════
    //  声音/待机类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：待机呻吟/低吼音效。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.IdleSounds.get。</summary>
    [Header("Vocal — 待机/低吼")]
    [Tooltip("待机呻吟/低吼音效")]
    [SerializeField]
    private AudioClip[] idleSounds;
    /// <summary>中文含义：短促低吼音效（grunt）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.GruntSounds.get。</summary>
    [Tooltip("短促低吼音效（grunt）")]
    [SerializeField]
    private AudioClip[] gruntSounds;
    /// <summary>中文含义：喉音音效（throat）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.ThroatSounds.get。</summary>
    [Tooltip("喉音音效（throat）")]
    [SerializeField]
    private AudioClip[] throatSounds;
    /// <summary>中文含义：呼吸循环音效（loopable）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.BreathingSounds.get。</summary>
    [Tooltip("呼吸循环音效（loopable）")]
    [SerializeField]
    private AudioClip[] breathingSounds;
    // ═══════════════════════════════════════════════
    //  警觉/发现类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：发现玩家音效（detection）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.DetectionSounds.get。</summary>
    [Header("Alert — 警觉/发现")]
    [Tooltip("发现玩家音效（detection）")]
    [SerializeField]
    private AudioClip[] detectionSounds;
    /// <summary>中文含义：尖叫音效（scream）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.ScreamSounds.get。</summary>
    [Tooltip("尖叫音效（scream）")]
    [SerializeField]
    private AudioClip[] screamSounds;
    // ═══════════════════════════════════════════════
    //  移动类
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：移动/追逐音效（walk/run/sprint）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.LocomotionSounds.get。</summary>
    [Header("Movement — 移动")]
    [Tooltip("移动/追逐音效（walk/run/sprint）")]
    [SerializeField]
    private AudioClip[] locomotionSounds;
    /// <summary>中文含义：倒地音效（ground）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.GroundSounds.get。</summary>
    [Tooltip("倒地音效（ground）")]
    [SerializeField]
    private AudioClip[] groundSounds;
    // ═══════════════════════════════════════════════
    //  特殊/其他
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：呕吐音效（vomit）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.VomitSounds.get。</summary>
    [Header("Special — 特殊")]
    [Tooltip("呕吐音效（vomit）")]
    [SerializeField]
    private AudioClip[] vomitSounds;
    /// <summary>中文含义：笑声音效（laugh）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.LaughSounds.get。</summary>
    [Tooltip("笑声音效（laugh）")]
    [SerializeField]
    private AudioClip[] laughSounds;
    /// <summary>中文含义：反应音效（reactions）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.ReactionSounds.get。</summary>
    [Tooltip("反应音效（reactions）")]
    [SerializeField]
    private AudioClip[] reactionSounds;
    /// <summary>中文含义：进食音效（eat/eating）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.EatingSounds.get。</summary>
    [Tooltip("进食音效（eat/eating）")]
    [SerializeField]
    private AudioClip[] eatingSounds;
    /// <summary>中文含义：说话/语句音效（words_sentences）。使用位置：作为 ZombieAudioConfig.TestClip(AudioClip[], string) 的参数、ZombieAudioConfig.WordSounds.get。</summary>
    [Tooltip("说话/语句音效（words_sentences）")]
    [SerializeField]
    private AudioClip[] wordSounds;
    // ═══════════════════════════════════════════════
    //  公开只读访问（供未来接入使用，本次不自动调用）
    // ═══════════════════════════════════════════════
    /// <summary>中文含义：攻击音效列表。</summary>
    public AudioClip[] AttackSounds => attackSounds;
    /// <summary>中文含义：撕咬音效列表。</summary>
    public AudioClip[] BiteSounds => biteSounds;
    /// <summary>中文含义：已蓄力攻击音效列表。</summary>
    public AudioClip[] ChargedAttackSounds => chargedAttackSounds;
    /// <summary>中文含义：受伤音效列表。</summary>
    public AudioClip[] HurtSounds => hurtSounds;
    /// <summary>中文含义：爆头音效列表。</summary>
    public AudioClip[] HeadshotSounds => headshotSounds;
    /// <summary>中文含义：肢体命中音效列表。</summary>
    public AudioClip[] LimbHitSounds => limbHitSounds;
    /// <summary>中文含义：疼痛音效列表。</summary>
    public AudioClip[] PainSounds => painSounds;
    /// <summary>中文含义：死亡音效列表。</summary>
    public AudioClip[] DeathSounds => deathSounds;
    /// <summary>中文含义：窒息音效列表。</summary>
    public AudioClip[] ChokeSounds => chokeSounds;
    /// <summary>中文含义：血腥效果音效列表。</summary>
    public AudioClip[] GorySounds => gorySounds;
    /// <summary>中文含义：待机音效列表。</summary>
    public AudioClip[] IdleSounds => idleSounds;
    /// <summary>中文含义：低吼音效列表。</summary>
    public AudioClip[] GruntSounds => gruntSounds;
    /// <summary>中文含义：喉部音效列表。</summary>
    public AudioClip[] ThroatSounds => throatSounds;
    /// <summary>中文含义：呼吸音效列表。</summary>
    public AudioClip[] BreathingSounds => breathingSounds;
    /// <summary>中文含义：检测音效列表。</summary>
    public AudioClip[] DetectionSounds => detectionSounds;
    /// <summary>中文含义：尖叫音效列表。</summary>
    public AudioClip[] ScreamSounds => screamSounds;
    /// <summary>中文含义：移动行为音效列表。</summary>
    public AudioClip[] LocomotionSounds => locomotionSounds;
    /// <summary>中文含义：地面音效列表。</summary>
    public AudioClip[] GroundSounds => groundSounds;
    /// <summary>中文含义：呕吐音效列表。</summary>
    public AudioClip[] VomitSounds => vomitSounds;
    /// <summary>中文含义：笑声音效列表。</summary>
    public AudioClip[] LaughSounds => laughSounds;
    /// <summary>中文含义：反应音效列表。</summary>
    public AudioClip[] ReactionSounds => reactionSounds;
    /// <summary>中文含义：进食音效列表。</summary>
    public AudioClip[] EatingSounds => eatingSounds;
    /// <summary>中文含义：词条音效列表。</summary>
    public AudioClip[] WordSounds => wordSounds;

    // ═══════════════════════════════════════════════
    //  ContextMenu 试听（仅编辑器，不修改任何游戏逻辑）
    // ═══════════════════════════════════════════════
    /// <summary>TestAttack 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Attack Sound")]
    void TestAttack() => TestClip(attackSounds, "Attack");
    /// <summary>TestBite 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Bite Sound")]
    void TestBite() => TestClip(biteSounds, "Bite");
    /// <summary>TestChargedAttack 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Charged Attack Sound")]
    void TestChargedAttack() => TestClip(chargedAttackSounds, "ChargedAttack");
    /// <summary>TestHurt 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Hurt Sound")]
    void TestHurt() => TestClip(hurtSounds, "Hurt");
    /// <summary>TestHeadshot 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Headshot Sound")]
    void TestHeadshot() => TestClip(headshotSounds, "Headshot");
    /// <summary>TestLimbHit 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Limb Hit Sound")]
    void TestLimbHit() => TestClip(limbHitSounds, "LimbHit");
    /// <summary>TestPain 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Pain Sound")]
    void TestPain() => TestClip(painSounds, "Pain");
    /// <summary>TestDeath 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Death Sound")]
    void TestDeath() => TestClip(deathSounds, "Death");
    /// <summary>TestChoke 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Choke Sound")]
    void TestChoke() => TestClip(chokeSounds, "Choke");
    /// <summary>TestGory 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Gory Sound")]
    void TestGory() => TestClip(gorySounds, "Gory");
    /// <summary>TestIdle 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Idle Sound")]
    void TestIdle() => TestClip(idleSounds, "Idle");
    /// <summary>TestGrunt 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Grunt Sound")]
    void TestGrunt() => TestClip(gruntSounds, "Grunt");
    /// <summary>TestThroat 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Throat Sound")]
    void TestThroat() => TestClip(throatSounds, "Throat");
    /// <summary>TestBreathing 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Breathing Sound")]
    void TestBreathing() => TestClip(breathingSounds, "Breathing");
    /// <summary>TestDetection 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Detection Sound")]
    void TestDetection() => TestClip(detectionSounds, "Detection");
    /// <summary>TestScream 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Scream Sound")]
    void TestScream() => TestClip(screamSounds, "Scream");
    /// <summary>TestLocomotion 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Locomotion Sound")]
    void TestLocomotion() => TestClip(locomotionSounds, "Locomotion");
    /// <summary>TestGround 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Ground Sound")]
    void TestGround() => TestClip(groundSounds, "Ground");
    /// <summary>TestVomit 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Vomit Sound")]
    void TestVomit() => TestClip(vomitSounds, "Vomit");
    /// <summary>TestLaugh 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Laugh Sound")]
    void TestLaugh() => TestClip(laughSounds, "Laugh");
    /// <summary>TestReaction 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Reaction Sound")]
    void TestReaction() => TestClip(reactionSounds, "Reaction");
    /// <summary>TestEating 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Eating Sound")]
    void TestEating() => TestClip(eatingSounds, "Eating");
    /// <summary>TestWord 的业务处理入口；本方法直接依赖 ZombieAudioConfig.TestClip(AudioClip[], string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ZombieAudioConfig.TestClip(AudioClip[], string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("Test Word Sound")]
    void TestWord() => TestClip(wordSounds, "Word");
    /// <summary>随机播放一个 Clip 用于试听（仅编辑器）</summary>
    /// <remarks>静态调用链：上游 ZombieAudioConfig.TestAttack()、ZombieAudioConfig.TestBite()、ZombieAudioConfig.TestBreathing()、ZombieAudioConfig.TestChargedAttack()、ZombieAudioConfig.TestChoke() 等共 23 处；下游（含内部回调体） AudioManager.PlaySFX(AudioClip)、Debug.Log(object)、Debug.LogWarning(object)、EditorUtility.DisplayDialog(string, string, string)、object.ReferenceEquals(object, object)、Random.Range(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clips">中文含义：音频片段集合。使用位置：作为 Random.Range(int, int) 的参数、ZombieAudioConfig.TestClip(AudioClip[], string)。</param>
    /// <param name="label">中文含义：标签。使用位置：作为 Debug.Log(object) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    void TestClip(AudioClip[] clips, string label)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[ZombieAudioConfig] {label} 没有配置 AudioClip");
            return;
        }

        // clip（音频片段）：使用位置：作为 AudioManager.PlaySFX(AudioClip) 的参数、作为 Debug.Log(object) 的参数、作为 EditorUtility.DisplayDialog(string, string, string) 的参数。
var clip = clips[Random.Range(0, clips.Length)];
        Debug.Log($"[ZombieAudioConfig] 试听 {label}: {clip.name}");
#if UNITY_EDITOR
        // 编辑器直接播放，无需运行游戏
        UnityEditor.EditorUtility.DisplayDialog("试听", $"正在播放: {clip.name}\n时长: {clip.length:F1}s", "确定");
#endif
        // 如果游戏在运行，使用 AudioManager 播放
        if (Application.isPlaying)
        {
            // receiverValue1（判空前缓存的音频管理器的实例）：从音频管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ZombieAudioConfig.TestClip(AudioClip[], string)。
global::AudioManager receiverValue1 = AudioManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.PlaySFX(clip);
            }
        }
    }
}
