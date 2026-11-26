using BehaviorDesigner.Runtime.Tasks;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 IsEnemyDead，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断敌人是否已经死亡。")]
    public class IsEnemyDead : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.HasDeath &&
                (Controller.ArchitectureReadSpecialTraversal == null ||
                 !Controller.ArchitectureReadSpecialTraversal.DeferLadderGroundReaction))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HasPendingHit，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断是否存在待处理受击。")]
    public class HasPendingHit : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.PendingHitReaction &&
                (Controller.ArchitectureReadSpecialTraversal == null ||
                 !Controller.ArchitectureReadSpecialTraversal.DeferLadderGroundReaction))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HasCrawlRequest，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断是否处于爬行阶段。")]
    public class HasCrawlRequest : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.PendingCrawl &&
                (Controller.ArchitectureReadSpecialTraversal == null ||
                 !Controller.ArchitectureReadSpecialTraversal.DeferLadderGroundReaction))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HasStandUpRequest，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断是否需要执行起身。")]
    public class HasStandUpRequest : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.PendingGetUp &&
                (Controller.ArchitectureReadSpecialTraversal == null ||
                 !Controller.ArchitectureReadSpecialTraversal.DeferLadderGroundReaction))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IsPlayerValid，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断玩家目标是否有效。")]
    public class IsPlayerValid : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.Player != null)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IsCombatEngaged，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断敌人已经进入战斗主线。")]
    public class IsCombatEngaged : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.HasEnterCombat)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HasChasePathFailure，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断追击目标解析或路径计算是否失败。")]
    public class HasChasePathFailure : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.HasChasePathFailure)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanSeePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断玩家是否可见。")]
    public class CanSeePlayer : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.TargetVisible)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CannotSeePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断敌人已经没有任何玩家警觉或感知记忆。")]
    public class CannotSeePlayer : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && !Controller.StateSnapshot.HasAlert)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IsInAttackRange，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断玩家是否位于攻击范围。")]
    public class IsInAttackRange : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.IsPlayerInAttackRange)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IsAttackReady，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断攻击冷却是否结束。")]
    public class IsAttackReady : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.StateSnapshot.AttackReady)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttackNow，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("统一判断当前是否能够开始攻击，作为攻击分支的抢占条件。")]
    public class CanAttackNow : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.CanAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.CanAttack())
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanCrawlAttackNow，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断爬行敌人是否已经贴近玩家并可以开始爬行攻击。")]
    public class CanCrawlAttackNow : EnemyConditionalTaskBase
    {
        /// <summary>配对动作参与者。</summary>
        private VillagerQTEParticipant qteParticipant;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyConditionalTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                qteParticipant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)、VillagerController.CanCrawlAttack()、VillagerQTEParticipant.CanAttemptLegBite(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.CanEvaluateCrawlCombatAction && qteParticipant != null && qteParticipant.CanAttemptLegBite(Controller.Player) && EnemyAIManager.Instance != null && EnemyAIManager.Instance.CanAcquireQTEReservation(Controller, QTEKind.LegBite))
            {
                return TaskStatus.Failure;
            }

            if (ComponentsReady && Controller.CanCrawlAttack())
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttemptEnemyQTEBase，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否可以尝试向快速反应事件系统交接正面抓取。")]
    public abstract class CanAttemptEnemyQTEBase : EnemyConditionalTaskBase
    {
        /// <summary>参与者。</summary>
        private VillagerQTEParticipant participant;
        /// <summary>种类。</summary>
        protected abstract QTEKind Kind { get; }

        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyConditionalTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                participant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)、VillagerQTEParticipant.CanAttemptChainsawExecution(LeonController)、VillagerQTEParticipant.CanAttemptLegBite(LeonController)、VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            // 是否可以评估。
bool canEvaluate;
            if (Kind == QTEKind.LegBite)
            {
                canEvaluate = ComponentsReady && Controller.CanEvaluateCrawlCombatAction;
            }
            else
            {
                canEvaluate = ComponentsReady && Controller.CanEvaluateStandingQTEAction;
            }

            if (!canEvaluate || participant == null || EnemyAIManager.Instance == null)
            {
                return TaskStatus.Failure;
            }

            // 是否可以尝试。
bool canAttempt;
            if (Kind == QTEKind.RearBiteNeck)
            {
                canAttempt = participant.CanAttemptRearBiteNeck(Controller.Player);
            }
            else
            {
                if (Kind == QTEKind.ChainsawExecution)
                {
                    canAttempt = participant.CanAttemptChainsawExecution(Controller.Player);
                }
                else
                {
                    canAttempt = participant.CanAttemptLegBite(Controller.Player);
                }
            }

            if (!canAttempt)
            {
                return TaskStatus.Failure;
            }

            // 只有真正满足距离、朝向和策略概率的请求才进入协调器等待，
            // 避免无效候选反过来封住普通攻击名额。
            if (EnemyAIManager.Instance.CanAcquireQTEReservation(Controller, Kind))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttemptRearBiteNeckQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否可以尝试背后咬脖子快速反应事件。")]
    public class CanAttemptRearBiteNeckQTE : CanAttemptEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.RearBiteNeck;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttemptLegBiteQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否可以尝试倒地咬腿快速反应事件。")]
    public class CanAttemptLegBiteQTE : CanAttemptEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.LegBite;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttemptChainsawExecutionQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否可以尝试电锯处决快速反应事件。")]
    public class CanAttemptChainsawExecutionQTE : CanAttemptEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.ChainsawExecution;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 CanAttemptFrontGrabQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否可以尝试向快速反应事件系统交接正面抓取。")]
    public class CanAttemptFrontGrabQTE : EnemyConditionalTaskBase
    {
        /// <summary>参与者。</summary>
        private VillagerQTEParticipant participant;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyConditionalTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                participant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.CanAcquireFrontGrabReservation(VillagerController)、EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind)、VillagerQTEParticipant.CanAttemptFrontGrab(LeonController)、VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || !Controller.CanEvaluateStandingQTEAction || participant == null || EnemyAIManager.Instance == null)
            {
                return TaskStatus.Failure;
            }

            if (participant.CanAttemptRearBiteNeck(Controller.Player) && EnemyAIManager.Instance.CanAcquireQTEReservation(Controller, QTEKind.RearBiteNeck))
            {
                return TaskStatus.Success;
            }

            if (participant.CanAttemptFrontGrab(Controller.Player) && EnemyAIManager.Instance.CanAcquireFrontGrabReservation(Controller))
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ShouldChasePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前是否应继续追击玩家。")]
    public class ShouldChasePlayer : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.ShouldChasePlayer)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ShouldInvestigatePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断敌人是否已经察觉玩家但尚未完成警觉确认。")]
    public class ShouldInvestigatePlayer : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.ShouldInvestigatePlayer)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ShouldHoldCombatPosition，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断敌人是否应在攻击范围内保持戒备并等待攻击冷却。")]
    public class ShouldHoldCombatPosition : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Controller.ShouldHoldCombatPosition)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IsPathValid，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断当前路径是否有效。")]
    public class IsPathValid : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Agent.CurrentPathState == PathAgentState.Valid)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HasArrived，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("判断是否已经到达目的地。")]
    public class HasArrived : EnemyConditionalTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && Agent.CurrentPathState == PathAgentState.Arrived)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }
}
