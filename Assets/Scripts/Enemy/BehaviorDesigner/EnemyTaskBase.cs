using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 EnemyTaskBase，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    public abstract class EnemyTaskBase : Task
    {
        /// <summary>中文含义：控制器。使用位置：作为 AstarPath.GetNearest(Vector3, NearestNodeConstraint) 的参数、作为 ChainsawManAnimationBridge.BeginIntroTaunt(float) 的参数、作为 EnemyActionCoordinator.RequestPathTarget(Vector3) 的参数 等共 65 处。</summary>
        protected VillagerController Controller;
        /// <summary>中文含义：寻路代理。使用位置：EnemyTaskBase.ComponentsReady.get、EnemyTaskBase.OnAwake()、MoveToPatrolPoint.OnUpdate() 等共 4 处。</summary>
        protected PathAgent Agent;
        /// <summary>中文含义：动画控制器。使用位置：EnemyTaskBase.ComponentsReady.get、EnemyTaskBase.OnAwake()。</summary>
        protected Animator Animator;
        /// <summary>中文含义：任务游戏对象。使用位置：作为 Debug.LogWarning(object, Object) 的参数、EnemyTaskBase.OnAwake()、ExecuteChainsawIntroTaunt.OnAwake() 等共 7 处。</summary>
        protected GameObject TaskGameObject;
        /// <summary>中文含义：组件集合是否就绪。使用位置：ChasePlayer.OnEnd()、ChasePlayer.OnStart()、ChasePlayer.OnUpdate() 等共 52 处。</summary>
        protected bool ComponentsReady => Controller != null && Controller.isActiveAndEnabled && Agent != null && Animator != null && Controller.IsRuntimeInitialized;

        /// <summary>中文含义：诊断名称。使用位置：EnemyTaskBase.DiagTaskEnd()、EnemyTaskBase.DiagTaskStart()。</summary>
        private string diagnosticsName;
        /// <summary>DiagTaskStart 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 ChasePlayer.OnStart()、ExecuteAttack.OnStart()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawStart.OnStart()、ExecuteChaseRecovery.OnStart() 等共 16 处；下游（含内部回调体） EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        protected void DiagTaskStart()
        {
            if (EnemyCombatTimeline.IsRecord(Controller))
            {
                // argumentValue4（按原顺序缓存的实参控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
global::VillagerController argumentValue4 = Controller;
                // argumentValue5（按原顺序缓存的实参文字“行为动作开始”）：从文字“行为动作开始”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
string argumentValue5 = "行为动作开始";
                // availableValue1（空值判断前缓存的诊断名称）：从诊断名称读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTaskBase.DiagTaskStart()。
string availableValue1 = diagnosticsName;
                // selectedValue2（分支选出的可用值1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数、EnemyTaskBase.DiagTaskStart()。
string selectedValue2;
                if (!global::System.Object.ReferenceEquals(availableValue1, null))
                {
                    selectedValue2 = availableValue1;
                }
                else
                {
                    // assignedValue3（待写入的文本选择）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyTaskBase.DiagTaskStart()。
string assignedValue3 = EnemyCombatTimeline.Describe(GetType().Name);
                    diagnosticsName = assignedValue3;
                    selectedValue2 = assignedValue3;
                }

                EnemyCombatTimeline.Record(argumentValue4, argumentValue5, selectedValue2);
            }

            if (ComponentsReady && Controller.enableMovementDiagnostic)
            {
                Controller.DiagLogBT("动作开始", GetType().Name);
            }
        }

        /// <summary>DiagTaskEnd 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 ChasePlayer.OnEnd()、ExecuteAttack.OnEnd()、ExecuteChainsawIntroTaunt.OnEnd()、ExecuteChainsawStart.OnEnd()、ExecuteChaseRecovery.OnEnd() 等共 15 处；下游（含内部回调体） EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        protected void DiagTaskEnd()
        {
            if (EnemyCombatTimeline.IsRecord(Controller))
            {
                // argumentValue9（按原顺序缓存的实参控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
global::VillagerController argumentValue9 = Controller;
                // argumentValue10（按原顺序缓存的实参文字“行为动作结束”）：从文字“行为动作结束”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数。
string argumentValue10 = "行为动作结束";
                // availableValue6（空值判断前缓存的诊断名称）：从诊断名称读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyTaskBase.DiagTaskEnd()。
string availableValue6 = diagnosticsName;
                // selectedValue7（分支选出的可用值6）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 EnemyCombatTimeline.Record(VillagerController, string, string) 的参数、EnemyTaskBase.DiagTaskEnd()。
string selectedValue7;
                if (!global::System.Object.ReferenceEquals(availableValue6, null))
                {
                    selectedValue7 = availableValue6;
                }
                else
                {
                    // assignedValue8（待写入的文本选择）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyTaskBase.DiagTaskEnd()。
string assignedValue8 = EnemyCombatTimeline.Describe(GetType().Name);
                    diagnosticsName = assignedValue8;
                    selectedValue7 = assignedValue8;
                }

                EnemyCombatTimeline.Record(argumentValue9, argumentValue10, selectedValue7);
            }

            if (ComponentsReady && Controller.enableMovementDiagnostic)
            {
                Controller.DiagLogBT("动作结束", GetType().Name);
            }
        }

        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：上游 ExecuteChainsawIntroTaunt.OnAwake()、ExecuteChainsawStart.OnAwake()、ExecuteCrawl.OnAwake()、TryStartEnemyQTEBase.OnAwake()、TryStartFrontGrabQTE.OnAwake()；下游（含内部回调体） GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;VillagerController&gt;()、Task.GetDefaultGameObject(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            TaskGameObject = GetDefaultGameObject(null);
            if (TaskGameObject == null)
            {
                return;
            }

            Controller = TaskGameObject.GetComponent<VillagerController>();
            Agent = TaskGameObject.GetComponent<PathAgent>();
            Animator = TaskGameObject.GetComponent<Animator>();
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 EnemyConditionalTaskBase，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    public abstract class EnemyConditionalTaskBase : Conditional
    {
        /// <summary>中文含义：控制器。使用位置：作为 EnemyAIManager.CanAcquireFrontGrabReservation(VillagerController) 的参数、作为 EnemyAIManager.CanAcquireQTEReservation(VillagerController, QTEKind) 的参数、作为 VillagerQTEParticipant.CanAttemptChainsawExecution(LeonController) 的参数 等共 28 处。</summary>
        protected VillagerController Controller;
        /// <summary>中文含义：寻路代理。使用位置：EnemyConditionalTaskBase.ComponentsReady.get、EnemyConditionalTaskBase.OnAwake()、HasArrived.OnUpdate() 等共 4 处。</summary>
        protected PathAgent Agent;
        /// <summary>中文含义：动画控制器。使用位置：EnemyConditionalTaskBase.ComponentsReady.get、EnemyConditionalTaskBase.OnAwake()。</summary>
        protected Animator Animator;
        /// <summary>中文含义：任务游戏对象。使用位置：CanAttemptEnemyQTEBase.OnAwake()、CanAttemptFrontGrabQTE.OnAwake()、CanCrawlAttackNow.OnAwake() 等共 6 处。</summary>
        protected GameObject TaskGameObject;
        /// <summary>中文含义：组件集合是否就绪。使用位置：CanAttackNow.OnUpdate()、CanAttemptEnemyQTEBase.OnUpdate()、CanAttemptFrontGrabQTE.OnUpdate() 等共 22 处。</summary>
        protected bool ComponentsReady => Controller != null && Controller.isActiveAndEnabled && Agent != null && Animator != null && Controller.IsRuntimeInitialized;

        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：上游 CanAttemptEnemyQTEBase.OnAwake()、CanAttemptFrontGrabQTE.OnAwake()、CanCrawlAttackNow.OnAwake()、NeedsChainsawIntroTaunt.OnAwake()、NeedsChainsawStart.OnAwake()；下游（含内部回调体） GameObject.GetComponent&lt;Animator&gt;()、GameObject.GetComponent&lt;PathAgent&gt;()、GameObject.GetComponent&lt;VillagerController&gt;()、Task.GetDefaultGameObject(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            TaskGameObject = GetDefaultGameObject(null);
            if (TaskGameObject == null)
            {
                return;
            }

            Controller = TaskGameObject.GetComponent<VillagerController>();
            Agent = TaskGameObject.GetComponent<PathAgent>();
            Animator = TaskGameObject.GetComponent<Animator>();
        }
    }
}
