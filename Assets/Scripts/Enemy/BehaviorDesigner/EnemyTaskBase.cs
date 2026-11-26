using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 EnemyTaskBase，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    public abstract class EnemyTaskBase : Task
    {
        /// <summary>控制器。</summary>
        protected VillagerController Controller;
        /// <summary>寻路代理。</summary>
        protected PathAgent Agent;
        /// <summary>动画控制器。</summary>
        protected Animator Animator;
        /// <summary>任务游戏对象。</summary>
        protected GameObject TaskGameObject;
        /// <summary>组件集合是否就绪。</summary>
        protected bool ComponentsReady => Controller != null && Controller.isActiveAndEnabled && Agent != null && Animator != null && Controller.IsRuntimeInitialized;

        /// <summary>诊断名称。</summary>
        private string diagnosticsName;
        /// <summary>DiagTaskStart 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 ChasePlayer.OnStart()、ExecuteAttack.OnStart()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawStart.OnStart()、ExecuteChaseRecovery.OnStart() 等共 16 处；下游（含内部回调体） EnemyCombatTimeline.Describe(string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.Record(VillagerController, string, string)、object.GetType()、object.ReferenceEquals(object, object)、VillagerController.DiagLogBT(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        protected void DiagTaskStart()
        {
            if (EnemyCombatTimeline.IsRecord(Controller))
            {
                // 按原顺序缓存的实参控制器。
global::VillagerController argumentValue4 = Controller;
                // 按原顺序缓存的实参文字“行为动作开始”。
string argumentValue5 = "行为动作开始";
                // 空值判断前缓存的诊断名称。
string availableValue1 = diagnosticsName;
                // 分支选出的可用值1。
string selectedValue2;
                if (!global::System.Object.ReferenceEquals(availableValue1, null))
                {
                    selectedValue2 = availableValue1;
                }
                else
                {
                    // 待写入的文本选择。
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
                // 按原顺序缓存的实参控制器。
global::VillagerController argumentValue9 = Controller;
                // 按原顺序缓存的实参文字“行为动作结束”。
string argumentValue10 = "行为动作结束";
                // 空值判断前缓存的诊断名称。
string availableValue6 = diagnosticsName;
                // 分支选出的可用值6。
string selectedValue7;
                if (!global::System.Object.ReferenceEquals(availableValue6, null))
                {
                    selectedValue7 = availableValue6;
                }
                else
                {
                    // 待写入的文本选择。
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
        /// <summary>控制器。</summary>
        protected VillagerController Controller;
        /// <summary>寻路代理。</summary>
        protected PathAgent Agent;
        /// <summary>动画控制器。</summary>
        protected Animator Animator;
        /// <summary>任务游戏对象。</summary>
        protected GameObject TaskGameObject;
        /// <summary>组件集合是否就绪。</summary>
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
