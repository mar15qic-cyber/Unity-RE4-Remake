using BehaviorDesigner.Runtime.Tasks;
using Pathfinding;
using UnityEngine;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteDeath，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("执行一次死亡结算并保持终态。")]
    public class ExecuteDeath : EnemyTaskBase
    {
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (ComponentsReady)
            {
                Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Death, GetType().Name);
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady)
            {
                return TaskStatus.Running;
            }
            else
            {
                return TaskStatus.Failure;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteHit，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("执行受击并等待动画完成。")]
    public class ExecuteHit : EnemyTaskBase
    {
        /// <summary>截止时间。</summary>
        private float deadline;
        /// <summary>是否已完成。</summary>
        private bool completed;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            completed = false;
            if (!ComponentsReady)
            {
                return;
            }

            Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.HitReaction, GetType().Name);
            deadline = Time.time + Controller.actionSafetyTimeout;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ConsumeHit()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead)
            {
                return TaskStatus.Failure;
            }

            if (Controller.combatModule != null && (Controller.combatModule.NativeCombatActive
                || Controller.combatModule.StandingHitRecoveryActive) && Time.time < deadline + 4f)
            {
                return TaskStatus.Running;
            }

            if (!Controller.HitFinished && Time.time < deadline)
            {
                return TaskStatus.Running;
            }

            Controller.ConsumeHit();
            completed = true;
            return TaskStatus.Success;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.ClearActionParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && !completed)
            {
                Controller.ClearActionParameters();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteCrawl，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("沿寻路方向爬行，超时后请求起身。")]
    public class ExecuteCrawl : EnemyTaskBase
    {
        /// <summary>结束时间。</summary>
        private float endTime;
        /// <summary>是否已完成。</summary>
        private bool completed;
        /// <summary>配对动作参与者。</summary>
        private VillagerQTEParticipant qteParticipant;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                qteParticipant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            completed = false;
            if (!ComponentsReady)
            {
                return;
            }

            Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Crawl, GetType().Name);
            endTime = Time.time + Controller.crawlDuration;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestPathTarget(Vector3)、VillagerController.CanCrawlAttack()、VillagerController.RequestStandUp()、VillagerQTEParticipant.TryStartLegBiteFromAI(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit)
            {
                return TaskStatus.Failure;
            }

            if (Controller.CanEvaluateCrawlCombatAction && qteParticipant != null && qteParticipant.TryStartLegBiteFromAI(Controller.Player))
            {
                completed = true;
                return TaskStatus.Success;
            }

            if (Controller.CanCrawlAttack())
            {
                return TaskStatus.Success;
            }

            if (Controller.Player != null)
            {
                Controller.actionCoordinator.RequestPathTarget(Controller.CurrentChaseTarget);
            }

            if (Controller.IsPermanentlyCrawling)
            {
                return TaskStatus.Running;
            }

            if (Time.time < endTime)
            {
                return TaskStatus.Running;
            }

            Controller.RequestStandUp();
            completed = true;
            return TaskStatus.Success;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、EnemyTaskBase.DiagTaskEnd()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && !completed)
            {
                Controller.actionCoordinator.RequestStopMovement();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteStandUp，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("执行起身并等待完成信号。")]
    public class ExecuteStandUp : EnemyTaskBase
    {
        /// <summary>截止时间。</summary>
        private float deadline;
        /// <summary>缓冲区截止时间。</summary>
        private float bufferDeadline;
        /// <summary>是否已完成。</summary>
        private bool completed;
        /// <summary>超时是否已经申请。</summary>
        private bool timeoutRequested;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            completed = false;
            timeoutRequested = false;
            if (!ComponentsReady)
            {
                return;
            }

            Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.GetUp, GetType().Name);
            deadline = Time.time + Controller.actionSafetyTimeout;
            bufferDeadline = deadline + 0.25f;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogWarning(object)、EnemyActionCoordinator.RequestMovementMode(EnemyMovementMode)、VillagerController.CancelStandUpAndResumeCrawl()、VillagerController.ClearActionParameters()、VillagerController.CompleteStandUp()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit)
            {
                return TaskStatus.Failure;
            }

            if (Controller.StandUpFinished)
            {
                Controller.CompleteStandUp();
                completed = true;
                return TaskStatus.Success;
            }

            if (Time.time >= deadline && !timeoutRequested)
            {
                timeoutRequested = true;
                Controller.ClearActionParameters();
                Controller.actionCoordinator.RequestMovementMode(EnemyMovementMode.FullyStopped);
            }

            if (Time.time >= bufferDeadline)
            {
                Controller.CancelStandUpAndResumeCrawl();
                completed = true;
                Debug.LogWarning("[ExecuteStandUp] 起身动画超时，已恢复为爬行状态。");
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、EnemyTaskBase.DiagTaskEnd()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && !completed)
            {
                Controller.actionCoordinator.RequestStopMovement();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteAttack，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("执行攻击并等待统一结束信号。")]
    public class ExecuteAttack : EnemyTaskBase
    {
        /// <summary>截止时间。</summary>
        private float deadline;
        /// <summary>缓冲区截止时间。</summary>
        private float bufferDeadline;
        /// <summary>是否已开始。</summary>
        private bool started;
        /// <summary>超时是否已经申请。</summary>
        private bool timeoutRequested;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            started = ComponentsReady && Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, GetType().Name).HasAccept;
            timeoutRequested = false;
            if (!started)
            {
                return;
            }

            deadline = Time.time + Controller.actionSafetyTimeout;
            bufferDeadline = deadline + 0.25f;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogWarning(object)、VillagerController.EndAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || !started)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            if (Controller.AttackFinished)
            {
                return TaskStatus.Success;
            }

            if (Time.time >= deadline && !timeoutRequested)
            {
                timeoutRequested = true;
                Controller.EndAttack();
            }

            if (Time.time >= bufferDeadline)
            {
                Debug.LogWarning("[ExecuteAttack] 攻击动画超时未退出，强制失败。");
                return TaskStatus.Failure;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.EndAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && started)
            {
                Controller.EndAttack();
                started = false;
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteCrawlAttack，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("停止爬行移动，面向玩家执行一次爬行攻击并等待动画完成。")]
    public class ExecuteCrawlAttack : EnemyTaskBase
    {
        /// <summary>截止时间。</summary>
        private float deadline;
        /// <summary>缓冲区截止时间。</summary>
        private float bufferDeadline;
        /// <summary>是否已开始。</summary>
        private bool started;
        /// <summary>超时是否已经申请。</summary>
        private bool timeoutRequested;
        /// <summary>是否已完成。</summary>
        private bool completed;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            started = ComponentsReady && Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, GetType().Name, true).HasAccept;
            completed = false;
            timeoutRequested = false;
            if (started)
            {
                deadline = Time.time + Controller.actionSafetyTimeout;
                bufferDeadline = deadline + 0.25f;
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogWarning(object)、VillagerController.EndCrawlAttack()、VillagerController.RestoreCrawlAfterAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || !started)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            if (Controller.AttackFinished)
            {
                completed = true;
                return TaskStatus.Success;
            }

            if (Time.time >= deadline && !timeoutRequested)
            {
                timeoutRequested = true;
                Controller.EndCrawlAttack();
            }

            if (Time.time >= bufferDeadline)
            {
                Debug.LogWarning("[ExecuteCrawlAttack] 爬行攻击动画超时未退出，已强制恢复爬行。");
                Controller.EndCrawlAttack();
                Controller.RestoreCrawlAfterAttack();
                completed = true;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.EndCrawlAttack()、VillagerController.RestoreCrawlAfterAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (!ComponentsReady)
            {
                return;
            }

            if (completed && !Controller.IsDead && !Controller.HasPendingHit && !Controller.HasPendingStandUp)
            {
                Controller.EndCrawlAttack();
                Controller.RestoreCrawlAfterAttack();
            }
            else
            {
                Controller.EndCrawlAttack();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 TryStartFrontGrabQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("向里昂快速反应事件协调器提交一次正面抓取请求，提交后立即结束。")]
    public class TryStartFrontGrabQTE : EnemyTaskBase
    {
        /// <summary>参与者。</summary>
        private VillagerQTEParticipant participant;
        /// <summary>是否已尝试。</summary>
        private bool attempted;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                participant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            attempted = false;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonQTECoordinator&gt;()、TryStartFrontGrabQTE.RecordStandingGrabRejection(QTECancelReason)、TryStartFrontGrabQTE.TryStartStandingGrab(LeonQTECoordinator, QTEKind)、VillagerQTEParticipant.CanAttemptFrontGrab(LeonController)、VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (attempted || !ComponentsReady || !Controller.CanEvaluateStandingQTEAction || participant == null)
            {
                return TaskStatus.Failure;
            }

            attempted = true;
            if (EnemyAIManager.Instance == null)
            {
                return TaskStatus.Failure;
            }

            // 当前里昂配对动作协调器。
            LeonQTECoordinator coordinator;
            EnemyAIManager manager = EnemyAIManager.Instance;
            if (manager != null)
            {
                coordinator = manager.ReadPlayerQte(Controller.Player);
            }
            else
            {
                coordinator = Controller.Player.GetComponent<LeonQTECoordinator>();
            }
            if (coordinator == null)
            {
                RecordStandingGrabRejection(QTECancelReason.ResourceMissing);
                return TaskStatus.Failure;
            }

            if (participant.CanAttemptRearBiteNeck(Controller.Player) && TryStartStandingGrab(coordinator, QTEKind.RearBiteNeck))
            {
                return TaskStatus.Success;
            }

            if (participant.CanAttemptFrontGrab(Controller.Player) && TryStartStandingGrab(coordinator, QTEKind.FrontGrab))
            {
                return TaskStatus.Success;
            }

            return TaskStatus.Failure;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
        }

        /// <summary>TryStartStandingGrab 的业务处理入口；本方法直接依赖 EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 TryStartFrontGrabQTE.OnUpdate()；下游（含内部回调体） EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="coordinator">当前里昂配对动作协调器。</param>
        /// <param name="kind">种类。</param>
        private bool TryStartStandingGrab(LeonQTECoordinator coordinator, QTEKind kind)
        {
            if (!EnemyAIManager.Instance.TryAcquireQTEReservation(Controller, kind))
            {
                return false;
            }

            if (Controller.combatModule != null && Controller.combatModule.NativeCombatProfile != null)
            {
                if (Controller.combatModule.TryBeginGrabAttack(Controller.Player, kind))
                {
                    return true;
                }
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, kind, false);
                return false;
            }

            if (coordinator.TryStart(kind, participant, out _))
            {
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, kind, true);
                return true;
            }

            participant.RecordQTERejection(coordinator.LastRejectionReason, kind);
            EnemyAIManager.Instance.CompleteQTEAttempt(Controller, kind, false);
            return false;
        }

        /// <summary>RecordStandingGrabRejection 的业务处理入口；本方法直接依赖 VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 TryStartFrontGrabQTE.OnUpdate()；下游（含内部回调体） VillagerQTEParticipant.CanAttemptRearBiteNeck(LeonController)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="reason">原因。</param>
        private void RecordStandingGrabRejection(QTECancelReason reason)
        {
            // 种类。
QTEKind kind;
            if (participant.CanAttemptRearBiteNeck(Controller.Player))
            {
                kind = QTEKind.RearBiteNeck;
            }
            else
            {
                kind = QTEKind.FrontGrab;
            }

            participant.RecordQTERejection(reason, kind);
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 TryStartEnemyQTEBase，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("持续更新玩家目的地并追击。")]
    public abstract class TryStartEnemyQTEBase : EnemyTaskBase
    {
        /// <summary>参与者。</summary>
        private VillagerQTEParticipant participant;
        /// <summary>是否已尝试。</summary>
        private bool attempted;
        /// <summary>种类。</summary>
        protected abstract QTEKind Kind { get; }

        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.OnAwake()、GameObject.GetComponent&lt;VillagerQTEParticipant&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                participant = TaskGameObject.GetComponent<VillagerQTEParticipant>();
            }
        }

        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            attempted = false;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonQTECoordinator&gt;()、EnemyAIManager.CompleteQTEAttempt(VillagerController, QTEKind, bool)、EnemyAIManager.TryAcquireQTEReservation(VillagerController, QTEKind)、LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)、VillagerQTEParticipant.RecordQTERejection(QTECancelReason, QTEKind)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

            if (attempted || !canEvaluate || participant == null)
            {
                return TaskStatus.Failure;
            }

            attempted = true;
            if (EnemyAIManager.Instance == null || !EnemyAIManager.Instance.TryAcquireQTEReservation(Controller, Kind))
            {
                return TaskStatus.Failure;
            }

            // 当前里昂配对动作协调器。
            LeonQTECoordinator coordinator;
            EnemyAIManager manager = EnemyAIManager.Instance;
            if (manager != null)
            {
                coordinator = manager.ReadPlayerQte(Controller.Player);
            }
            else
            {
                coordinator = Controller.Player.GetComponent<LeonQTECoordinator>();
            }
            if (coordinator == null)
            {
                participant.RecordQTERejection(QTECancelReason.ResourceMissing, Kind);
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, Kind, false);
                return TaskStatus.Failure;
            }

            if (Kind == QTEKind.ChainsawExecution && Controller.combatModule != null)
            {
                if (Controller.combatModule.TryBeginChainsawThreat())
                {
                    return TaskStatus.Success;
                }
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, Kind, false);
                return TaskStatus.Failure;
            }
            if (Kind == QTEKind.RearBiteNeck && Controller.combatModule != null && Controller.combatModule.NativeCombatProfile != null)
            {
                if (Controller.combatModule.TryBeginGrabAttack(Controller.Player, Kind))
                {
                    return TaskStatus.Success;
                }
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, Kind, false);
                return TaskStatus.Failure;
            }

            if (coordinator.TryStart(Kind, participant, out _))
            {
                EnemyAIManager.Instance.CompleteQTEAttempt(Controller, Kind, true);
                return TaskStatus.Success;
            }

            participant.RecordQTERejection(coordinator.LastRejectionReason, Kind);
            EnemyAIManager.Instance.CompleteQTEAttempt(Controller, Kind, false);
            return TaskStatus.Failure;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 TryStartRearBiteNeckQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("向里昂快速反应事件协调器提交一次背后咬脖子请求。")]
    public class TryStartRearBiteNeckQTE : TryStartEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.RearBiteNeck;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 TryStartLegBiteQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("向里昂快速反应事件协调器提交一次倒地咬腿请求。")]
    public class TryStartLegBiteQTE : TryStartEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.LegBite;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 TryStartChainsawExecutionQTE，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("向里昂快速反应事件协调器提交一次电锯处决请求。")]
    public class TryStartChainsawExecutionQTE : TryStartEnemyQTEBase
    {
        /// <summary>中文含义：种类。</summary>
        protected override QTEKind Kind => QTEKind.ChainsawExecution;
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteCombat，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("在一个持续任务内完成追击、近身转向和普通攻击。")]
    public class ExecuteCombat : EnemyTaskBase
    {
        /// <summary>攻击截止时间。</summary>
        private float attackDeadline;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (ComponentsReady)
            {
                attackDeadline = Time.time + Controller.actionSafetyTimeout;
            }
            if (ComponentsReady && Controller.IsCombatEngaged && Controller.CurrentState != VillagerState.Attack)
            {
                Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Chase, GetType().Name);
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、Mathf.Max(float, float)、object.GetType()、VillagerController.BeginCombatHold()、VillagerController.CanAttack()、VillagerController.EndAttack()、VillagerController.MaintainChasePathRecoveryFacing() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || Controller.Player == null)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            if (!Controller.IsCombatEngaged)
            {
                return TaskStatus.Failure;
            }

            if (Controller.CurrentState == VillagerState.Attack)
            {
                if (Controller.AttackFinished || Time.time >= attackDeadline)
                {
                    Controller.EndAttack();
                }

                return TaskStatus.Running;
            }

            if (Controller.CanAttack())
            {
                if (Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Attack, GetType().Name).HasAccept)
                {
                    attackDeadline = Time.time + Controller.actionSafetyTimeout;
                    return TaskStatus.Running;
                }
            }

            Controller.ContinueTacticalCombat();

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.EndAttack()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && Controller.CurrentState == VillagerState.Attack)
            {
                Controller.EndAttack();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteChaseRecovery，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("保持追击意图，在路径失败时面向玩家并按间隔恢复可达目标。")]
    public class ExecuteChaseRecovery : ExecuteCombat
    {
    }

    /*

    [TaskCategory("敌人智能/动作")]
    [TaskDescription("鎸佺画鏇存柊鐜╁鐩殑鍦板苟杩藉嚮銆?)]
    */
    /// <summary>敌人行为、动作与通行的行为树任务 ChasePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("持续更新玩家目标并追击。")]
    public class ChasePlayer : EnemyTaskBase
    {
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (ComponentsReady)
            {
                Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Chase, GetType().Name);
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || Controller.Player == null)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            if (!Controller.IsCombatEngaged)
            {
                return TaskStatus.Success;
            }

            if (!Controller.ShouldChasePlayer)
            {
                return TaskStatus.Success;
            }

            // 控制器是站立追击的唯一目的地写入者。
            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyTaskBase.DiagTaskEnd()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue4 = EnemyAIManager.Instance;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    receiverValue4.ReleaseCombatReservation(Controller);
                }
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 InvestigatePlayer，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("停止移动、观察玩家并等待警觉度确认。")]
    public class InvestigatePlayer : EnemyTaskBase
    {
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskStart()、VillagerController.BeginAlert()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (ComponentsReady)
            {
                Controller.BeginAlert();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.UpdateInvestigation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || Controller.IsDead)
            {
                return TaskStatus.Failure;
            }

            if (Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsFullyAwareOfPlayer || !Controller.HasAnyPlayerAwareness)
            {
                return TaskStatus.Success;
            }

            if (Controller.UpdateInvestigation())
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Running;
            }
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、EnemyAIManager.ReleaseCombatReservation(VillagerController)、EnemyTaskBase.DiagTaskEnd()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                Controller.actionCoordinator.RequestStopMovement();
                // 判空前缓存的敌人智能管理器的实例。
global::EnemyAIManager receiverValue6 = EnemyAIManager.Instance;
                if (!global::System.Object.ReferenceEquals(receiverValue6, null))
                {
                    receiverValue6.ReleaseCombatReservation(Controller);
                }
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 HoldCombatPosition，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("在攻击范围内面向玩家等待攻击冷却，避免移动与攻击状态反复切换。")]
    public class HoldCombatPosition : EnemyTaskBase
    {
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskStart()、VillagerController.BeginCombatHold()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (ComponentsReady)
            {
                Controller.BeginCombatHold();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.UpdateCombatFacing()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || Controller.IsDead)
            {
                return TaskStatus.Failure;
            }

            if (Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp)
            {
                return TaskStatus.Failure;
            }

            Controller.UpdateCombatFacing();
            if (Controller.ShouldHoldCombatPosition)
            {
                return TaskStatus.Running;
            }
            else
            {
                return TaskStatus.Success;
            }
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.StopPathMovementWithAnimationBlend()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                Controller.StopPathMovementWithAnimationBlend();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 SelectPatrolPoint，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("从巡逻半径内选择同一可达区域的寻路节点。")]
    public class SelectPatrolPoint : EnemyTaskBase
    {
        [BehaviorDesigner.Runtime.Tasks.Tooltip("只在正式村庄的开场发现阶段选择巡逻路线。")]
        public bool villageOpeningOnly;
        /// <summary>最大尝试次数。编译期常量，保持既有值与消费者映射。</summary>
        private const int MaxAttempts = 8;
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AstarPath.GetNearest(Vector3, NearestNodeConstraint)、Vector3.Distance(Vector3, Vector3)、Vector3.Vector3(float, float, float)、VillagerController.ClearPatrolTarget()、VillagerController.SetPatrolTarget(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || AstarPath.active == null)
            {
                return TaskStatus.Failure;
            }

            float patrolRadius = Controller.patrolRadius;
            if (villageOpeningOnly)
            {
                EnemyAIManager manager = EnemyAIManager.Instance;
                if (manager == null || !manager.AwaitsVillageDiscovery)
                {
                    return TaskStatus.Failure;
                }

                patrolRadius = Mathf.Min(patrolRadius, 4.5f);
            }

            // 原点。
NNInfo origin = AstarPath.active.GetNearest(Controller.transform.position, NearestNodeConstraint.Walkable);
            if (origin.node == null)
            {
                return TaskStatus.Failure;
            }

            // 当前遍历索引。
for (int i = 0; i < MaxAttempts; i++)
            {
                // 偏移量。
Vector2 offset = Random.insideUnitCircle * patrolRadius;
                // 候选。
Vector3 candidate = Controller.PatrolCenter + new Vector3(offset.x, 0f, offset.y);
                // 最近。
NNInfo nearest = AstarPath.active.GetNearest(candidate, NearestNodeConstraint.Walkable);
                if (nearest.node == null || nearest.node.Area != origin.node.Area || Vector3.Distance(candidate, nearest.position) > Agent.graphSnapDistance)
                {
                    continue;
                }

                Controller.SetPatrolTarget(nearest.position);
                return TaskStatus.Success;
            }

            Controller.ClearPatrolTarget();
            return TaskStatus.Failure;
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 MoveToPatrolPoint，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("移动到控制器保存的巡逻点。")]
    public class MoveToPatrolPoint : EnemyTaskBase
    {
        [BehaviorDesigner.Runtime.Tasks.Tooltip("村庄战斗开始时结束开场巡逻。")]
        public bool villageOpeningOnly;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyActionCoordinator.RequestPathTarget(Vector3)、EnemyTaskBase.DiagTaskStart()、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (!ComponentsReady || !Controller.HasPatrolTarget)
            {
                return;
            }

            Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Patrol, GetType().Name);
            Controller.actionCoordinator.RequestPathTarget(Controller.PatrolTarget);
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || !Controller.HasPatrolTarget)
            {
                return TaskStatus.Failure;
            }

            if (villageOpeningOnly)
            {
                EnemyAIManager manager = EnemyAIManager.Instance;
                if (manager == null || !manager.AwaitsVillageDiscovery)
                {
                    return TaskStatus.Failure;
                }
            }

            if (Controller.IsDead || Controller.IsQTEControlled || Controller.HasPendingHit || Controller.HasPendingCrawl ||
                Controller.HasPendingStandUp || !villageOpeningOnly && Controller.HasAnyPlayerAwareness)
            {
                return TaskStatus.Failure;
            }

            if (villageOpeningOnly && Controller.perceptionModule.HasVillageFootstepTarget)
            {
                if (Controller.UpdateInvestigation())
                {
                    return TaskStatus.Success;
                }
                return TaskStatus.Running;
            }

            if (Agent.CurrentPathState == PathAgentState.Failed)
            {
                return TaskStatus.Failure;
            }

            if (Agent.CurrentPathState == PathAgentState.Arrived)
            {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、EnemyTaskBase.DiagTaskEnd()、VillagerController.ClearPatrolTarget()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                Controller.ClearPatrolTarget();
                Controller.actionCoordinator.RequestStopMovement();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 PatrolInPlace，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("敌人停留在当前位置观察四周，并缓慢转向随机方向。")]
    public class PatrolInPlace : EnemyTaskBase
    {
        /// <summary>转向参数保持持续时间。编译期常量，保持既有值与消费者映射。</summary>
        private const float TurnParameterHoldDuration = 0.15f;
        /// <summary>结束时间。</summary>
        private float endTime;
        /// <summary>释放转向时间。</summary>
        private float releaseTurnTime;
        /// <summary>是否转向已释放。</summary>
        private bool turnReleased;
        /// <summary>目标旋转。</summary>
        private Quaternion targetRotation;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskStart()、Mathf.Clamp(float, float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Quaternion.Euler(float, float, float)、Random.Range(float, float)、VillagerController.BeginStationaryPatrol(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (!ComponentsReady)
            {
                return;
            }

            // 最大转向角度。
float maximumTurnAngle = Mathf.Clamp(Controller.stationaryPatrolMaxTurnAngle, 0f, 180f);
            // 最小转向角度。
float minimumTurnAngle = Mathf.Min(maximumTurnAngle, Mathf.Max(25f, maximumTurnAngle * 0.45f));
            // 转向长度。
float turnMagnitude = Random.Range(minimumTurnAngle, maximumTurnAngle);
            // 转向角度。
float turnAngle;
            if (Random.value < 0.5f)
            {
                turnAngle = -turnMagnitude;
            }
            else
            {
                turnAngle = turnMagnitude;
            }

            targetRotation = Controller.transform.rotation * Quaternion.Euler(0f, turnAngle, 0f);
            Controller.BeginStationaryPatrol(turnAngle);
            turnReleased = false;
            releaseTurnTime = Time.time + TurnParameterHoldDuration;
            // 最小。
float minimum = Mathf.Min(Controller.stationaryPatrolMinDuration, Controller.stationaryPatrolMaxDuration);
            // 最大。
float maximum = Mathf.Max(Controller.stationaryPatrolMinDuration, Controller.stationaryPatrolMaxDuration);
            endTime = Time.time + Random.Range(minimum, maximum);
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerController.ReleaseStationaryPatrolTurn()、VillagerController.UpdateStationaryPatrolFacing(Quaternion)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || Controller.IsDead)
            {
                return TaskStatus.Failure;
            }

            if (Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp || Controller.HasAnyPlayerAwareness)
            {
                return TaskStatus.Failure;
            }

            if (!turnReleased && Time.time >= releaseTurnTime)
            {
                turnReleased = true;
                Controller.ReleaseStationaryPatrolTurn();
            }

            Controller.UpdateStationaryPatrolFacing(targetRotation);
            if (Time.time >= endTime)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Running;
            }
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.DiagTaskEnd()、VillagerController.EndStationaryPatrol()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                Controller.EndStationaryPatrol();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 IdleWait，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("进入空闲并等待一段随机时间。")]
    public class IdleWait : EnemyTaskBase
    {
        /// <summary>结束时间。</summary>
        private float endTime;
        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestAction(ActionKind, string, bool)、EnemyTaskBase.DiagTaskStart()、object.GetType()、Random.Range(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            if (!ComponentsReady)
            {
                return;
            }

            Controller.actionCoordinator.RequestAction(GameArchitecture.ActionKind.Idle, GetType().Name);
            endTime = Time.time + Random.Range(1f, 2.5f);
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit || Controller.HasPendingCrawl || Controller.HasPendingStandUp || Controller.HasAnyPlayerAwareness)
            {
                return TaskStatus.Success;
            }

            if (Time.time >= endTime)
            {
                return TaskStatus.Success;
            }
            else
            {
                return TaskStatus.Running;
            }
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、EnemyTaskBase.DiagTaskEnd()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady)
            {
                Controller.actionCoordinator.RequestStopMovement();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 StopAndClearMovement，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("停止寻路并清理移动与动作参数。")]
    public class StopAndClearMovement : EnemyTaskBase
    {
        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyActionCoordinator.RequestStopMovement()、VillagerController.ClearActionParameters()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            Controller.actionCoordinator.RequestStopMovement();
            Controller.ClearActionParameters();
            return TaskStatus.Success;
        }
    }
}
