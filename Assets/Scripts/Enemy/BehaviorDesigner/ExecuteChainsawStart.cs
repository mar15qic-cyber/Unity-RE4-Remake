using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteChainsawStart，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("ChainsawMan 专属：执行启动电锯动画并等待完成。")]
    public class ExecuteChainsawStart : EnemyTaskBase
    {
        /// <summary>中文含义：桥接。使用位置：ExecuteChainsawStart.OnAwake()、ExecuteChainsawStart.OnEnd()、ExecuteChainsawStart.OnStart() 等共 4 处。</summary>
        private ChainsawManAnimationBridge bridge;
        /// <summary>中文含义：截止时间。使用位置：ExecuteChainsawStart.OnStart()、ExecuteChainsawStart.OnUpdate()。</summary>
        private float deadline;
        /// <summary>中文含义：是否已开始。使用位置：ExecuteChainsawStart.OnEnd()、ExecuteChainsawStart.OnStart()、ExecuteChainsawStart.OnUpdate()。</summary>
        private bool started;
        /// <summary>中文含义：是否已完成。使用位置：ExecuteChainsawStart.OnEnd()、ExecuteChainsawStart.OnStart()、ExecuteChainsawStart.OnUpdate()。</summary>
        private bool completed;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.OnAwake()、GameObject.GetComponent&lt;ChainsawManAnimationBridge&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                bridge = TaskGameObject.GetComponent<ChainsawManAnimationBridge>();
            }
        }

        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.BeginChainsawStart()、EnemyTaskBase.DiagTaskStart()、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            started = false;
            completed = false;
            if (!ComponentsReady || bridge == null || Controller.IsNavigationTraversalProtected)
            {
                return;
            }

            Controller.StopAndClearMovement();
            bridge.BeginChainsawStart();
            started = true;
            deadline = Time.time + Controller.actionSafetyTimeout;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.OnChainsawStartComplete()、Debug.LogWarning(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || bridge == null)
            {
                return TaskStatus.Failure;
            }

            if (!started)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit)
            {
                return TaskStatus.Failure;
            }

            if (bridge.StartActionFinished)
            {
                completed = true;
                return TaskStatus.Success;
            }

            if (Time.time >= deadline)
            {
                Debug.LogWarning("[ExecuteChainsawStart] 启动电锯超时，强制标记完成。", TaskGameObject);
                bridge.OnChainsawStartComplete();
                completed = true;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.CancelChainsawStart()、EnemyTaskBase.DiagTaskEnd()、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && started && !completed && bridge != null)
            {
                bridge.CancelChainsawStart();
                Controller.StopAndClearMovement();
            }
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 ExecuteChainsawIntroTaunt，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/动作")]
    [TaskDescription("电锯村民专属：在安全距离播放一次入战威慑并等待完成。")]
    public class ExecuteChainsawIntroTaunt : EnemyTaskBase
    {
        /// <summary>中文含义：桥接。使用位置：ExecuteChainsawIntroTaunt.OnAwake()、ExecuteChainsawIntroTaunt.OnEnd()、ExecuteChainsawIntroTaunt.OnStart() 等共 4 处。</summary>
        private ChainsawManAnimationBridge bridge;
        /// <summary>中文含义：截止时间。使用位置：ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawIntroTaunt.OnUpdate()。</summary>
        private float deadline;
        /// <summary>中文含义：是否已开始。使用位置：ExecuteChainsawIntroTaunt.OnEnd()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawIntroTaunt.OnUpdate()。</summary>
        private bool started;
        /// <summary>中文含义：是否已完成。使用位置：ExecuteChainsawIntroTaunt.OnEnd()、ExecuteChainsawIntroTaunt.OnStart()、ExecuteChainsawIntroTaunt.OnUpdate()。</summary>
        private bool completed;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyTaskBase.OnAwake()、GameObject.GetComponent&lt;ChainsawManAnimationBridge&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                bridge = TaskGameObject.GetComponent<ChainsawManAnimationBridge>();
            }
        }

        /// <summary>实现基类的 OnStart 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.BeginIntroTaunt(float)、EnemyTaskBase.DiagTaskStart()、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnStart()
        {
            DiagTaskStart();
            started = false;
            completed = false;
            if (!ComponentsReady || bridge == null || Controller.IsNavigationTraversalProtected)
            {
                return;
            }

            Controller.StopAndClearMovement();
            bridge.BeginIntroTaunt(Controller.PlayerDistance);
            started = true;
            deadline = Time.time + Controller.actionSafetyTimeout;
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.CompleteIntroTaunt()、ChainsawManAnimationBridge.UpdateIntroTaunt()、Debug.LogWarning(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || bridge == null)
            {
                return TaskStatus.Failure;
            }

            if (!started)
            {
                return TaskStatus.Failure;
            }

            if (Controller.IsDead || Controller.HasPendingHit)
            {
                return TaskStatus.Failure;
            }

            if (bridge.UpdateIntroTaunt())
            {
                completed = true;
                return TaskStatus.Success;
            }

            if (Time.time >= deadline)
            {
                Debug.LogWarning("电锯村民入战威慑超时，已安全结束动作。", TaskGameObject);
                bridge.CompleteIntroTaunt();
                completed = true;
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>实现基类的 OnEnd 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ChainsawManAnimationBridge.CancelIntroTaunt()、EnemyTaskBase.DiagTaskEnd()、VillagerController.StopAndClearMovement()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnEnd()
        {
            DiagTaskEnd();
            if (ComponentsReady && started && !completed && bridge != null)
            {
                if (Controller.HasPendingHit)
                {
                    bridge.CompleteIntroTaunt();
                }
                else
                {
                    bridge.CancelIntroTaunt();
                }
                Controller.StopAndClearMovement();
            }
        }
    }
}
