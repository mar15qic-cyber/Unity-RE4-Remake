using System;
using System.Collections.Generic;

namespace GameArchitecture
{
    /// <summary>小刀动作状态标识，由规则驱动切换并通过事件交给宿主播放；保持原有排列及成对反应数值。</summary>
    public enum KnifeAction
    {
        /// <summary>中文含义：无（未选择或尚未发生）。无活动小刀动作。</summary>
        None,
        /// <summary>中文含义：进入防守。抬刀进入防守。</summary>
        EnterGuard,
        /// <summary>中文含义：保持防守。持续防守循环。</summary>
        HoldGuard,
        /// <summary>中文含义：下部防守。放下防守姿态。</summary>
        LowerGuard,
        /// <summary>中文含义：主要斩击。第一段横斩。</summary>
        PrimarySlash,
        /// <summary>中文含义：反斜杠。反向续接横斩。</summary>
        Backslash,
        /// <summary>中文含义：主要斩击恢复。第一段横斩收招。</summary>
        PrimarySlashRecovery,
        /// <summary>中文含义：反斜杠恢复。反向横斩收招。</summary>
        BackslashRecovery,
        /// <summary>中文含义：突刺第一。第一段突刺。</summary>
        ThrustOne,
        /// <summary>中文含义：突刺第二。第二段突刺。</summary>
        ThrustTwo,
        /// <summary>中文含义：突刺恢复。突刺收招。</summary>
        ThrustRecovery,
        /// <summary>中文含义：关闭范围常规。近身普通招架反应。</summary>
        CloseRangeNormal,
        /// <summary>中文含义：关闭范围完美。近身完美招架反应，数值紧随普通反应。</summary>
        CloseRangePerfect,
        /// <summary>中文含义：高常规。高位普通招架反应。</summary>
        HighNormal,
        /// <summary>中文含义：高完美。高位完美招架反应，数值紧随普通反应。</summary>
        HighPerfect,
        /// <summary>中文含义：向外偏转常规。外拨普通招架反应。</summary>
        OutwardDeflectionNormal,
        /// <summary>中文含义：向外偏转完美。外拨完美招架反应，数值紧随普通反应。</summary>
        OutwardDeflectionPerfect,
        ThirdSlash,
        ThirdSlashRecovery
    }

    /// <summary>接触结算的招架结论，经规则返回给宿主以选择伤害与反应表现。</summary>
    public enum KnifeParryOutcome
    {
        /// <summary>中文含义：失败。本次接触未成功招架。</summary>
        Failure,
        /// <summary>中文含义：普通状态。有效输入命中普通招架窗口。</summary>
        Normal,
        /// <summary>中文含义：完美。有效输入命中完美招架窗口。</summary>
        Perfect
    }

    /// <summary>规则或宿主记录的招架失败原因，供接触结算诊断读取；保持原枚举数值。</summary>
    public enum KnifeParryFailure
    {
        /// <summary>中文含义：无（未选择或尚未发生）。当前检查通过，没有失败原因。</summary>
        None,
        /// <summary>中文含义：无输入。接触时间之前没有防守按下记录。</summary>
        NoInput,
        /// <summary>中文含义：输入之后接触。现有输入发生在接触之后，不能追溯保护此前接触。</summary>
        InputAfterContact,
        /// <summary>中文含义：已过期。该次按下已经超过有效时间。</summary>
        Expired,
        /// <summary>中文含义：已消耗。该次按下已经被另一接触消费。</summary>
        Consumed,
        /// <summary>中文含义：阶段阻挡。接触时所在动作阶段不允许招架。</summary>
        PhaseBlocked,
        /// <summary>中文含义：后方。接触来自宿主判定的背后方向。</summary>
        Behind,
        /// <summary>中文含义：不可招架。攻击自身不允许招架。</summary>
        Unparryable,
        /// <summary>中文含义：不可用。宿主当前不具备招架可用条件。</summary>
        Unavailable
    }

    /// <summary>单次招架输入尝试的值快照，规则供宿主诊断读取；消费状态在构造时复制。</summary>
    public readonly struct KnifeParryAttemptSnapshot
    {
        /// <summary>中文含义：序号。输入序号，关联宿主输入记录及招架消费事实。</summary>
        public readonly long sequence;
        /// <summary>中文含义：已按下所在。防守按下的规则时间。</summary>
        public readonly double pressedAt;
        /// <summary>中文含义：到期时间。该次按下的招架资格截止时间。</summary>
        public readonly double expiresAt;
        /// <summary>中文含义：是否已消耗。该次按下是否已被某次成功接触消费。</summary>
        public readonly bool consumed;
        /// <summary>Snapshot 将内部可变尝试复制给宿主，不暴露内部记录。</summary>
        /// <param name="sequence">中文含义：序号。输入序号。</param>
        /// <param name="pressedAt">中文含义：已按下所在。防守按下时间。</param>
        /// <param name="expiresAt">中文含义：到期时间。招架资格截止时间。</param>
        /// <param name="consumed">中文含义：是否已消耗。是否已经消费。</param>
        public KnifeParryAttemptSnapshot(long sequence, double pressedAt, double expiresAt, bool consumed)
        {
            this.sequence = sequence;
            this.pressedAt = pressedAt;
            this.expiresAt = expiresAt;
            this.consumed = consumed;
        }
    }

    /// <summary>宿主传入的来袭分类，用于选择对应普通或完美招架动作。</summary>
    public enum KnifeIncomingAttackCategory
    {
        /// <summary>中文含义：关闭范围。近身来袭，选择近身招架反应。</summary>
        CloseRange,
        /// <summary>中文含义：高。高位来袭，选择高位招架反应。</summary>
        High,
        /// <summary>中文含义：向外偏转。向外拨挡来袭，选择外拨招架反应。</summary>
        OutwardDeflection
    }

    /// <summary>配置资产传给规则的单段动作时序；命中、取消、接触和恢复位置均以秒表示。</summary>
    [Serializable]
    public struct KnifeActionTiming
    {
        /// <summary>中文含义：持续时间。片段总时长，推进时用作动作结束或循环边界。</summary>
        public float duration;
        /// <summary>中文含义：命中开始。有效命中区间起点；负数表示没有有效命中段。</summary>
        public float hitBegin;
        /// <summary>中文含义：命中结束。有效命中区间终点，须大于起点才构成命中段。</summary>
        public float hitEnd;
        /// <summary>中文含义：提前阶段取消结束。早期可取消阶段的结束时间，不包含边界。</summary>
        public float earlyPhaseCancelEnd;
        /// <summary>中文含义：恢复取消开始。收招可取消阶段的起始时间，包含边界。</summary>
        public float recoveryCancelBegin;
        /// <summary>中文含义：接触点。招架反应切入动画的接触位置，用作 Switch 的时间偏移。</summary>
        public float contactPoint;
        /// <summary>中文含义：恢复点。招架反应允许移交动作或消费攻击缓存的起始时间。</summary>
        public float recoveryPoint;
        /// <summary>中文含义：是否循环。配置中的循环标记；当前规则对持刀防守有专用循环逻辑。</summary>
        public bool loop;
        /// <summary>规则默认配置或资源适配器构造一段时序，参数按值保存。</summary>
        /// <param name="duration">中文含义：持续时间。总时长。</param>
        /// <param name="hitBegin">中文含义：命中开始。命中起点，默认无命中段。</param>
        /// <param name="hitEnd">中文含义：命中结束。命中终点。</param>
        /// <param name="earlyPhaseCancelEnd">中文含义：提前阶段取消结束。早期可取消阶段终点。</param>
        /// <param name="recoveryCancelBegin">中文含义：恢复取消开始。收招可取消阶段起点。</param>
        /// <param name="contactPoint">中文含义：接触点。招架接触切入时间。</param>
        /// <param name="recoveryPoint">中文含义：恢复点。反应可移交时间。</param>
        /// <param name="loop">中文含义：是否循环。配置中的循环标记。</param>
        public KnifeActionTiming(float duration, float hitBegin = -1, float hitEnd = -1, float earlyPhaseCancelEnd = 0, float recoveryCancelBegin = 0, float contactPoint = 0, float recoveryPoint = 0, bool loop = false)
        {
            this.duration = duration;
            this.hitBegin = hitBegin;
            this.hitEnd = hitEnd;
            this.earlyPhaseCancelEnd = earlyPhaseCancelEnd;
            this.recoveryCancelBegin = recoveryCancelBegin;
            this.contactPoint = contactPoint;
            this.recoveryPoint = recoveryPoint;
            this.loop = loop;
        }

        /// <summary>中文含义：是否已经命中。命中段配置有效时为 true，Advance 据此决定是否发送经过区间。</summary>
        public bool HasHit => hitBegin >= 0 && hitEnd > hitBegin;

        /// <summary>TryGuardCancel 查询当前动作是否位于早期或收招可取消阶段。</summary>
        /// <param name="time">中文含义：时间。该动作已经推进的时间。</param>
        /// <returns>处于任一可取消阶段时返回 true。</returns>
        public bool CanCancel(double time) => time < earlyPhaseCancelEnd || time >= recoveryCancelBegin;
    }

    /// <summary>一次推进确实经过的有效命中区间，经 IntervalTraversed 交给宿主做空间判定。</summary>
    public readonly struct KnifeTraversedInterval
    {
        /// <summary>中文含义：实例。区间所属动作实例编号，宿主据此拒绝旧动作命中。</summary>
        public readonly long instance;
        /// <summary>中文含义：动作。区间所属攻击动作种类。</summary>
        public readonly KnifeAction action;
        /// <summary>中文含义：开始。本次经过区间的起点，已裁到命中窗口内。</summary>
        public readonly double begin;
        /// <summary>中文含义：结束。本次经过区间的终点，已裁到命中窗口内。</summary>
        public readonly double end;
        /// <summary>Advance 通知时复制区间，避免低帧率跨过窗口而漏报。</summary>
        /// <param name="instance">中文含义：实例。所属动作实例。</param>
        /// <param name="action">中文含义：动作。所属动作种类。</param>
        /// <param name="begin">中文含义：开始。本次有效区间起点。</param>
        /// <param name="end">中文含义：结束。本次有效区间终点。</param>
        public KnifeTraversedInterval(long instance, KnifeAction action, double begin, double end)
        {
            this.instance = instance;
            this.action = action;
            this.begin = begin;
            this.end = end;
        }
    }

    /// <summary>
    /// 小刀输入消费、动作时序和招架尝试的纯模型，所有时间单位均为秒。
    /// KnifeCombatModule 传入按下、攻击、推进及接触事实；规则先写入状态，再同步发送动作切换或命中区间事件。
    /// 查询和招架结果通过返回值交给宿主；动画、空间命中和实际伤害均由宿主处理。
    /// </summary>
    public sealed class KnifeCombatRules
    {
        /// <summary>中文含义：时序。按动作保存默认或资产覆盖的时序配置。</summary>
        private readonly Dictionary<KnifeAction, KnifeActionTiming> timing = new Dictionary<KnifeAction, KnifeActionTiming>();
        /// <summary>中文含义：下部第一实例。动作实例序列，每次 Switch 都递增，包括切换到 None。</summary>
        private long lowerOneInstance;
        /// <summary>中文含义：缓存归属。当前攻击缓存所属动作实例；零表示没有缓存。</summary>
        private long cacheOwnership;
        /// <summary>中文含义：已按下序号。防守按下的内部递增序号，宿主未提供有效序号时使用。</summary>
        private long pressedSequence;
        /// <summary>中文含义：是否缓存突刺。缓存攻击时记录的防守状态，决定续接突刺还是横斩。</summary>
        private bool cacheThrust;
        /// <summary>中文含义：是否存在待处理的防守。等待动作进入可取消阶段后执行的抬刀意图。</summary>
        private bool pendingGuard;
        /// <summary>中文含义：最新已按下。最近防守按下时间，推进时用于清理已松开且过期的抬刀意图。</summary>
        private double latestPressed = double.NegativeInfinity;
        /// <summary>中文含义：模拟时间。规则当前模拟时间，动作切换时写入防守阶段历史。</summary>
        private double simulationTime;
        /// <summary>内部可消费输入记录；成功招架只消费对应的一次按下。</summary>
        private sealed class ParryAttempt
        {
            /// <summary>中文含义：序号。此按下的输入序号。</summary>
            public long sequence;
            /// <summary>中文含义：已按下所在。此按下的发生时间。</summary>
            public double pressedAt;
            /// <summary>中文含义：到期时间。此按下的招架资格截止时间。</summary>
            public double expiresAt;
            /// <summary>中文含义：是否已消耗。此按下是否已成功挡住一次接触。</summary>
            public bool consumed;
        }

        /// <summary>动作切换形成的防守阶段记录，用历史时间判断帧中途接触的资格。</summary>
        private struct GuardPhase
        {
            /// <summary>中文含义：时间。此防守阶段开始的模拟时间。</summary>
            public double time;
            /// <summary>中文含义：是否允许。此阶段是否允许招架；无动作或攻击动作不允许。</summary>
            public bool allowed;
        }

        /// <summary>中文含义：尝试次数。最近最多 64 次按下记录，按时间倒查接触发生前的最近输入。</summary>
        private readonly List<ParryAttempt> attempts = new List<ParryAttempt>();
        /// <summary>中文含义：防守阶段集合。最近最多 128 个防守阶段，防止同帧后续切换改变历史接触判断。</summary>
        private readonly List<GuardPhase> guardPhases = new List<GuardPhase>();
        /// <summary>中文含义：上一次招架失败。最近一次接触招架的失败原因，由 ResolutionParry 写入供诊断读取。</summary>
        public KnifeParryFailure LastParryFailure { get; private set; }
        /// <summary>中文含义：上一次解算后的尝试。最近结算所使用尝试的快照，成功时包含已消费状态。</summary>
        public KnifeParryAttemptSnapshot LastResolvedAttempt { get; private set; }

        /// <summary>中文含义：当前尝试。最近一次按下的快照；没有按下记录时返回默认值，不代表已有招架资格。</summary>
        public KnifeParryAttemptSnapshot CurrentAttempt
        {
            get
            {
                if (attempts.Count == 0)
                {
                    return default;
                }

                return Snapshot(attempts[attempts.Count - 1]);
            }
        }

        /// <summary>当前尝试查询与接触结算共用的复制入口，不返回内部可写对象。</summary>
        /// <param name="attempt">中文含义：尝试。已经确认非空的内部尝试记录。</param>
        /// <returns>该记录当前数值的不可变快照。</returns>
        private static KnifeParryAttemptSnapshot Snapshot(ParryAttempt attempt)
        {
            return new KnifeParryAttemptSnapshot(attempt.sequence, attempt.pressedAt, attempt.expiresAt, attempt.consumed);
        }

        /// <summary>中文含义：当前动作。当前规则动作，Switch 写入并通过事件交给动画宿主。</summary>
        public KnifeAction CurrentAction { get; private set; }
        /// <summary>中文含义：动作实例。当前动作唯一实例编号，命中区间和输入缓存均与它关联。</summary>
        public long ActionInstance { get; private set; }
        /// <summary>中文含义：动作时间。当前动作内已推进时间，Switch 可从招架接触偏移开始。</summary>
        public double ActionTime { get; private set; }
        /// <summary>中文含义：是否保持防守输入。最近收到的防守持有状态；GuardInput 更新，Interrupt 清除。</summary>
        public bool GuardHeld { get; private set; }
        /// <summary>中文含义：是否具有缓存。攻击缓存是否仍属于当前动作，旧动作缓存不能续接新动作。</summary>
        public bool HasCache => cacheOwnership != 0 && cacheOwnership == ActionInstance;
        /// <summary>中文含义：是否生效。是否存在非 None 动作，决定推进循环是否继续。</summary>
        public bool Active => CurrentAction != KnifeAction.None;
        /// <summary>中文含义：是否攻击。当前动作是否属于横斩或突刺，复用统一动作分类函数。</summary>
        public bool IsAttack => IsAttackValue(CurrentAction);
        /// <summary>中文含义：完美窗口。完美招架允许的按下到接触时间差，由配置宿主覆盖默认值。</summary>
        public double PerfectWindow { get; set; } = .11;
        /// <summary>中文含义：轻按窗口。单次防守按下的招架资格时长，由配置宿主覆盖默认值。</summary>
        public double TapWindow { get; set; } = .2;

        /// <summary>中文含义：动作是否具有切换。动作状态提交后同步广播动作、实例及切入偏移，宿主订阅并驱动动画与动作资源。</summary>
        public event Action<KnifeAction, long, double> ActionHasSwitch;
        /// <summary>中文含义：间隔已走过的。推进经过有效命中段时同步广播，宿主订阅后执行空间扫掠和命中提交。</summary>
        public event Action<KnifeTraversedInterval> IntervalTraversed;
        public event Action<KnifeTraversedInterval> ActionAdvanced;
        /// <summary>配置宿主创建规则实例时建立默认时序；随后 Configuration 可用当前资产逐项覆盖。</summary>
        public KnifeCombatRules()
        {
            Configuration(KnifeAction.EnterGuard, new KnifeActionTiming(.45f));
            Configuration(KnifeAction.HoldGuard, new KnifeActionTiming(2, loop: true));
            Configuration(KnifeAction.LowerGuard, new KnifeActionTiming(.35f));
            Configuration(KnifeAction.PrimarySlash, new KnifeActionTiming(.56f, .16f, .44f, .10f, .44f));
            Configuration(KnifeAction.Backslash, new KnifeActionTiming(1.11f, .64f, .90f, .58f, .90f));
            Configuration(KnifeAction.PrimarySlashRecovery, new KnifeActionTiming(.32f));
            Configuration(KnifeAction.BackslashRecovery, new KnifeActionTiming(.20f));
            Configuration(KnifeAction.ThrustOne, new KnifeActionTiming(.5f, .12f, .24f, .08f, .30f));
            Configuration(KnifeAction.ThrustTwo, new KnifeActionTiming(.5f, .12f, .24f, .08f, .30f));
            Configuration(KnifeAction.ThrustRecovery, new KnifeActionTiming(.10f));
            Configuration(KnifeAction.CloseRangeNormal, new KnifeActionTiming(.35f, contactPoint: .13f, recoveryPoint: .30f));
            Configuration(KnifeAction.CloseRangePerfect, new KnifeActionTiming(.90f, contactPoint: .13f, recoveryPoint: .45f));
            Configuration(KnifeAction.HighNormal, new KnifeActionTiming(.32f, contactPoint: .11f, recoveryPoint: .27f));
            Configuration(KnifeAction.HighPerfect, new KnifeActionTiming(.48f, contactPoint: .11f, recoveryPoint: .30f));
            Configuration(KnifeAction.OutwardDeflectionNormal, new KnifeActionTiming(.35f, contactPoint: .10f, recoveryPoint: .30f));
            Configuration(KnifeAction.OutwardDeflectionPerfect, new KnifeActionTiming(1, contactPoint: .10f, recoveryPoint: .50f));
        }

        /// <summary>默认构造及 KnifeCombatConfiguration.CreateRules 调用，校验时长后替换对应动作时序。</summary>
        /// <param name="action">中文含义：动作。待配置的非 None 动作。</param>
        /// <param name="data">中文含义：当前小刀动作时序。完整时序；时长无效时抛出参数异常。</param>
        public void Configuration(KnifeAction action, KnifeActionTiming data)
        {
            if (action == KnifeAction.None || data.duration <= 0 || float.IsNaN(data.duration))
            {
                throw new ArgumentException("小刀动作必须具有有效时长");
            }

            timing[action] = data;
        }

        /// <summary>推进、取消和宿主查询共用的只读配置入口。</summary>
        /// <param name="action">中文含义：动作。要查询的动作。</param>
        /// <returns>已配置时序，缺失时返回默认值。</returns>
        public KnifeActionTiming Query(KnifeAction action)
        {
            // value 接收已配置时序，缺失配置时返回默认值，不隐式创建条目。
            if (timing.TryGetValue(action, out var /* 中文含义：当前小刀动作时序。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ value))
            {
                return value;
            }

            return default;
        }

        /// <summary>连招和攻击分类共用的突刺判断。</summary>
        /// <param name="action">中文含义：动作。待分类动作。</param>
        /// <returns>第一或第二段突刺时为 true。</returns>
        public static bool IsThrust(KnifeAction action) => action == KnifeAction.ThrustOne || action == KnifeAction.ThrustTwo;
        /// <summary>规则和宿主共用攻击分类，保持横斩与突刺的一致定义。</summary>
        /// <param name="action">中文含义：动作。待分类动作。</param>
        /// <returns>横斩或突刺时为 true。</returns>
        public static bool IsAttackValue(KnifeAction action) => IsThrust(action)
            || action is KnifeAction.PrimarySlash or KnifeAction.Backslash or KnifeAction.ThirdSlash;
        /// <summary>按既有枚举排列判断招架反应；后续新增值须维护枚举的分类边界。</summary>
        /// <param name="action">中文含义：动作。待分类动作。</param>
        /// <returns>数值不小于首个招架反应时为 true。</returns>
        public static bool IsReaction(KnifeAction action) => action is >= KnifeAction.CloseRangeNormal and <= KnifeAction.OutwardDeflectionPerfect;
        /// <summary>中文含义：是否可以交接。当前非攻击且招架反应已到恢复点时允许宿主移交动作。</summary>
        public bool CanHandoff => !IsAttack && (!IsReaction(CurrentAction) || ActionTime >= Query(CurrentAction).recoveryPoint);

        /// <summary>宿主转发防守状态变化；新按下建立一次可消费尝试并申请取消，松开可切入放刀。</summary>
        /// <param name="held">中文含义：是否保持按住。当前防守是否持续按住。</param>
        /// <param name="time">中文含义：时间。此状态变化的规则时间。</param>
        /// <param name="inputSequence">中文含义：输入序号。宿主输入序号；非正数时使用内部序列。</param>
        public void GuardInput(bool held, double time, long inputSequence = 0)
        {
            if (GuardHeld == held)
            {
                return;
            }

            simulationTime = time;
            GuardHeld = held;
            ClearCache();
            if (held)
            {
                latestPressed = time;
                pressedSequence++;
                // 本次尝试实际使用的输入序号，优先采用宿主提供的正数序号。
                // 中文含义：sequence 为“序号”。
long sequence = pressedSequence;
                if (inputSequence > 0)
                {
                    sequence = inputSequence;
                }

                attempts.Add(new ParryAttempt { sequence = sequence, pressedAt = time, expiresAt = time + TapWindow });
                if (attempts.Count > 64)
                {
                    attempts.RemoveAt(0);
                }

                // 新按下允许接下一击，不强行把尚未结束的招架反应切回准备动作。
                pendingGuard = !IsReaction(CurrentAction);
                TryGuardCancel();
            }
            else if (!IsAttack && !IsReaction(CurrentAction) && Active)
            {
                Switch(KnifeAction.LowerGuard);
            }
            else if (!held)
            {
                pendingGuard = false;
                TryReleaseThrust(Query(CurrentAction));
            }
        }

        /// <summary>宿主转发一次攻击按下；冲突阶段最多缓存一次意图，空闲时按防守状态选择突刺或横斩。</summary>
        public void ResumeHeldGuard(double time)
        {
            if (GuardHeld)
            {
                return;
            }
            simulationTime = time;
            GuardHeld = true;
            ClearCache();
            pendingGuard = !IsReaction(CurrentAction);
            TryGuardCancel();
        }

        public void AttackInput()
        {
            if (IsAttack || IsReaction(CurrentAction) && !CanHandoff)
            {
                if (!HasCache)
                {
                    cacheOwnership = ActionInstance;
                    cacheThrust = GuardHeld;
                }

                return;
            }

            pendingGuard = false;
            if (GuardHeld)
            {
                Switch(KnifeAction.ThrustOne);
            }
            else
            {
                Switch(KnifeAction.PrimarySlash);
            }
        }

        /// <summary>防守输入及每次推进共用；只在攻击取消窗口或反应恢复点到达后执行待处理抬刀。</summary>
        private void TryGuardCancel()
        {
            if (!pendingGuard)
            {
                return;
            }

            if (IsAttack && !Query(CurrentAction).CanCancel(ActionTime))
            {
                return;
            }

            if (IsReaction(CurrentAction) && !CanHandoff)
            {
                return;
            }

            pendingGuard = false;
            Switch(KnifeAction.EnterGuard);
        }

        /// <summary>宿主只读查询某次接触时间的招架资格，不消费输入。</summary>
        /// <param name="time">中文含义：时间。待判断的接触规则时间。</param>
        /// <returns>输入有效且阶段允许时为 true。</returns>
        private static double ThrustReleaseTime(KnifeActionTiming data)
        {
            return Math.Min(data.duration, Math.Max(data.hitEnd, data.recoveryCancelBegin));
        }

        private bool TryReleaseThrust(KnifeActionTiming data)
        {
            if (GuardHeld || !IsThrust(CurrentAction) || ActionTime + 1e-7 < ThrustReleaseTime(data))
            {
                return false;
            }
            Switch(KnifeAction.LowerGuard);
            return true;
        }

        public bool CanAcceptGuardPress => !IsAttack || Query(CurrentAction).CanCancel(ActionTime);

        public bool HasGuardEligibility(double time)
        {
            return EvaluateAttempt(time, out _) == KnifeParryFailure.None;
        }

        /// <summary>资格查询与实际接触结算共用；倒查输入和防守阶段历史，防止更晚输入覆盖帧中途接触。</summary>
        /// <param name="time">中文含义：时间。接触发生的规则时间。</param>
        /// <param name="attempt">中文含义：尝试。接触前最近按下的记录，找不到时为 null；本方法不消费它。</param>
        /// <returns>资格失败原因，通过时为 None。</returns>
        private KnifeParryFailure EvaluateAttempt(double time, out ParryAttempt attempt)
        {
            attempt = null;
            // 接触可能位于当前帧中途，不能让同帧更晚的新输入覆盖当时有效的尝试。
            // index 从最新按下倒序查找接触之前发生的输入。
            // 中文含义：index 为“当前元素索引”。
for (int index = attempts.Count - 1; index >= 0; index--)
            {
                if (attempts[index].pressedAt <= time)
                {
                    attempt = attempts[index];
                    break;
                }
            }

            if (attempt == null)
            {
                if (attempts.Count > 0)
                {
                    return KnifeParryFailure.InputAfterContact;
                }

                return KnifeParryFailure.NoInput;
            }

            if (attempt.consumed)
            {
                return KnifeParryFailure.Consumed;
            }

            if (time > attempt.expiresAt + 1e-7)
            {
                return KnifeParryFailure.Expired;
            }

            // index 从最新阶段倒序恢复接触发生时的防守资格。
            // 中文含义：index 为“当前元素索引”。
for (int index = guardPhases.Count - 1; index >= 0; index--)
            {
                if (guardPhases[index].time <= time + 1e-9)
                {
                    if (!guardPhases[index].allowed)
                    {
                        return KnifeParryFailure.PhaseBlocked;
                    }

                    return KnifeParryFailure.None;
                }
            }

            return KnifeParryFailure.PhaseBlocked;
        }

        /// <summary>宿主提交一次实际接触时调用；资格、可招架性和正面条件通过后消费输入并区分普通与完美。</summary>
        /// <param name="time">中文含义：时间。此次接触的规则时间。</param>
        /// <param name="front">中文含义：是否前方。宿主空间判断是否来自正面。</param>
        /// <param name="canParry">中文含义：是否可以招架。来袭攻击是否允许招架。</param>
        /// <returns>失败、普通或完美结论；诊断属性同时保留本次原因和尝试快照。</returns>
        public KnifeParryOutcome ResolutionParry(double time, bool front, bool canParry)
        {
            // attempt 接收本次接触对应的历史按下，只有全部条件通过后才消费。
            LastParryFailure = EvaluateAttempt(time, out ParryAttempt /* 中文含义：尝试。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ attempt);
            LastResolvedAttempt = default;
            if (attempt != null)
            {
                LastResolvedAttempt = Snapshot(attempt);
            }

            if (!canParry)
            {
                LastParryFailure = KnifeParryFailure.Unparryable;
            }
            else if (!front)
            {
                LastParryFailure = KnifeParryFailure.Behind;
            }

            if (LastParryFailure != KnifeParryFailure.None)
            {
                return KnifeParryOutcome.Failure;
            }

            attempt.consumed = true;
            LastResolvedAttempt = Snapshot(attempt);
            // 接触相对按下的时间差，用于完美窗口判定。
            // 中文含义：age 为“已存在时间”。
double age = time - attempt.pressedAt;
            if (age <= PerfectWindow + 1e-7)
            {
                return KnifeParryOutcome.Perfect;
            }

            return KnifeParryOutcome.Normal;
        }

        /// <summary>宿主接受招架结果后调用；按来袭分类与结论切入对应反应的接触位置。</summary>
        /// <param name="result">中文含义：当前小刀招架结果。已完成的招架结论，失败时无操作。</param>
        /// <param name="category">中文含义：类别。来袭动作分类。</param>
        public void ParryReaction(KnifeParryOutcome result, KnifeIncomingAttackCategory category)
        {
            if (result == KnifeParryOutcome.Failure)
            {
                return;
            }

            // 先选择普通反应；完美结算沿用枚举相邻值映射。
            // 中文含义：action 为“动作”。
KnifeAction action;
            if (category == KnifeIncomingAttackCategory.High)
            {
                action = KnifeAction.HighNormal;
            }
            else if (category == KnifeIncomingAttackCategory.OutwardDeflection)
            {
                action = KnifeAction.OutwardDeflectionNormal;
            }
            else
            {
                action = KnifeAction.CloseRangeNormal;
            }

            if (result == KnifeParryOutcome.Perfect)
            {
                action++;
            }

            pendingGuard = false;
            Switch(action, Query(action).contactPoint);
        }

        /// <summary>按经过区间推进，低帧率跨越命中窗口时仍报告该区间。</summary>
        /// <param name="delta">中文含义：增量。本帧推进秒数；负数、NaN 或无穷大时忽略。</param>
        /// <param name="currentTime">中文含义：当前时间。本帧末规则时间，用于恢复绝对模拟时间。</param>
        public void Advance(double delta, double currentTime)
        {
            if (delta < 0 || double.IsNaN(delta) || double.IsInfinity(delta))
            {
                return;
            }

            simulationTime = currentTime - delta;
            if (pendingGuard && !GuardHeld && currentTime - latestPressed > TapWindow)
            {
                pendingGuard = false;
            }

            TryGuardCancel();
            // 单次推进最多处理 64 次边界，限制极端配置下的连续切换循环。
            // 中文含义：doorAccess 为“门访问”。
int doorAccess = 0;
            while (Active && delta > 1e-9 && doorAccess++ < 64)
            {
                // 本轮动作时序快照，跨切换后由下一轮重新读取。
                // 中文含义：data 为“当前小刀动作时序”。
var data = Query(CurrentAction);
                // 本轮最早需要处理的边界，可能被取消点或反应恢复点提前。
                // 中文含义：endPoint 为“结束点”。
double endPoint = data.duration;
                if (!GuardHeld && IsThrust(CurrentAction))
                {
                    endPoint = ThrustReleaseTime(data);
                }
                if (pendingGuard && IsAttack && ActionTime < data.recoveryCancelBegin)
                {
                    endPoint = Math.Min(endPoint, data.recoveryCancelBegin);
                }

                if (IsReaction(CurrentAction) && HasCache && ActionTime < data.recoveryPoint)
                {
                    endPoint = Math.Min(endPoint, data.recoveryPoint);
                }

                // 本轮真正推进的时间，不越过当前需要处理的动作边界。
                // 中文含义：stepSize 为“步进尺寸”。
double stepSize = Math.Min(delta, Math.Max(0, endPoint - ActionTime));
                // 推进前的动作时间，用于裁剪此次确实经过的命中区间。
                // 中文含义：begin 为“开始”。
double begin = ActionTime;
                ActionTime += stepSize;
                delta -= stepSize;
                simulationTime += stepSize;
                if (stepSize > 0 && ActionAdvanced != null)
                {
                    ActionAdvanced(new KnifeTraversedInterval(ActionInstance, CurrentAction, begin, ActionTime));
                }
                if (data.HasHit && ActionTime > data.hitBegin && begin < data.hitEnd)
                {
                    // 保存本次接收者；只有非空时才构造命中区间，保持原条件调用的求值时机。
                    // 中文含义：intervalHandler 为“间隔处理器”。
Action<KnifeTraversedInterval> intervalHandler = IntervalTraversed;
                    if (intervalHandler != null)
                    {
                        intervalHandler(new KnifeTraversedInterval(ActionInstance, CurrentAction, Math.Max(begin, data.hitBegin), Math.Min(ActionTime, data.hitEnd)));
                    }
                }

                // 取消前保存实例，若 TryGuardCancel 切换动作则从新动作重新推进。
                // 中文含义：originalInstance 为“原始实例”。
long originalInstance = ActionInstance;
                TryGuardCancel();
                if (originalInstance != ActionInstance)
                {
                    continue;
                }

                // 命中时间段先提交，再把移动和交互交回玩家，姿态由动画层渐出。
                if (TryReleaseThrust(data))
                {
                    continue;
                }

                if (IsReaction(CurrentAction) && HasCache && CanHandoff)
                {
                    // 缓存记录下一击类型，Switch 清缓存前先复制，避免丢失输入意图。
                    // 中文含义：thrust 为“是否突刺”。
bool thrust = cacheThrust;
                    if (thrust)
                    {
                        Switch(KnifeAction.ThrustOne);
                    }
                    else
                    {
                        Switch(KnifeAction.PrimarySlash);
                    }

                    continue;
                }

                if (ActionTime + 1e-7 < data.duration)
                {
                    break;
                }

                CompleteClip(currentTime);
            }

            simulationTime = currentTime;
        }

        /// <summary>Advance 到达片段结束时调用；按有效缓存续接攻击，否则收招或恢复持刀状态。</summary>
        /// <param name="currentTime">中文含义：当前时间。推进链传入的帧末时间；当前结束选择不使用此参数，保留既有入口。</param>
        private void CompleteClip(double currentTime)
        {
            // Switch 会修改当前动作，先保存结束片段种类供连招和收招选择。
            // 中文含义：original 为“原始”。
var original = CurrentAction;
            // 只有属于当前动作且攻防模式未改变的缓存才可续接下一刀。
            // 中文含义：connectKnife 为“是否连接小刀”。
bool connectKnife = HasCache && cacheThrust == GuardHeld;
            if (IsAttack)
            {
                if (connectKnife)
                {
                    if (GuardHeld)
                    {
                        if (original == KnifeAction.ThrustOne)
                        {
                            Switch(KnifeAction.ThrustTwo);
                        }
                        else
                        {
                            Switch(KnifeAction.ThrustOne);
                        }
                    }
                    else
                    {
                        if (original is KnifeAction.PrimarySlash or KnifeAction.ThirdSlash)
                        {
                            Switch(KnifeAction.Backslash);
                        }
                        else if (original == KnifeAction.Backslash && timing.ContainsKey(KnifeAction.ThirdSlash)
                            && timing.ContainsKey(KnifeAction.ThirdSlashRecovery))
                        {
                            Switch(KnifeAction.ThirdSlash);
                        }
                        else
                        {
                            Switch(KnifeAction.PrimarySlash);
                        }
                    }
                }
                else if (IsThrust(original))
                {
                    Switch(KnifeAction.ThrustRecovery);
                }
                else if (original == KnifeAction.PrimarySlash)
                {
                    Switch(KnifeAction.PrimarySlashRecovery);
                }
                else if (original == KnifeAction.ThirdSlash)
                {
                    Switch(KnifeAction.ThirdSlashRecovery);
                }
                else
                {
                    Switch(KnifeAction.BackslashRecovery);
                }

                return;
            }

            if (original == KnifeAction.HoldGuard && GuardHeld)
            {
                ActionTime %= Query(original).duration;
                return;
            }

            if (GuardHeld)
            {
                Switch(KnifeAction.HoldGuard);
            }
            else if (original == KnifeAction.EnterGuard)
            {
                Switch(KnifeAction.LowerGuard);
            }
            else
            {
                Switch(KnifeAction.None);
            }
        }

        /// <summary>输入模式变化或动作切换时清空缓存所属实例及攻击类型。</summary>
        private void ClearCache()
        {
            cacheOwnership = 0;
            cacheThrust = false;
        }

        /// <summary>所有动作切换的唯一写入口；清缓存、更新动作实例及阶段历史，最后同步通知宿主。</summary>
        /// <param name="action">中文含义：动作。目标动作，包括停止时的 None。</param>
        /// <param name="offset">中文含义：偏移量。目标片段的起始时间，招架反应可从接触点切入。</param>
        private void Switch(KnifeAction action, double offset = 0)
        {
            ClearCache();
            CurrentAction = action;
            ActionTime = offset;
            ActionInstance = ++lowerOneInstance;
            guardPhases.Add(new GuardPhase { time = simulationTime, allowed = action != KnifeAction.None && !IsAttackValue(action) });
            if (guardPhases.Count > 128)
            {
                guardPhases.RemoveAt(0);
            }

            // 保存事件委托，订阅列表只读取一次；状态已经提交后才同步通知宿主。
            // 中文含义：switchHandler 为“切换处理器”。
Action<KnifeAction, long, double> switchHandler = ActionHasSwitch;
            if (switchHandler != null)
            {
                switchHandler(action, ActionInstance, offset);
            }
        }

        /// <summary>宿主取消、受击、死亡或退出小刀流程时调用；清空输入和阶段历史，再通过 Switch 广播停止。</summary>
        public void Interrupt()
        {
            GuardHeld = false;
            pendingGuard = false;
            latestPressed = double.NegativeInfinity;
            attempts.Clear();
            guardPhases.Clear();
            LastParryFailure = KnifeParryFailure.NoInput;
            LastResolvedAttempt = default;
            Switch(KnifeAction.None);
        }
    }
}
