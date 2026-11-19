using BehaviorDesigner.Runtime.Tasks;

namespace EnemyBehaviorTasks
{
    /// <summary>敌人行为、动作与通行的行为树任务 NeedsChainsawStart，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("ChainsawMan 专属：判断是否需要启动电锯。")]
    public class NeedsChainsawStart : EnemyConditionalTaskBase
    {
        /// <summary>中文含义：桥接。使用位置：NeedsChainsawStart.OnAwake()、NeedsChainsawStart.OnUpdate()。</summary>
        private ChainsawManAnimationBridge bridge;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyConditionalTaskBase.OnAwake()、GameObject.GetComponent&lt;ChainsawManAnimationBridge&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                bridge = TaskGameObject.GetComponent<ChainsawManAnimationBridge>();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady || bridge == null || Controller.IsDead || Controller.IsNavigationTraversalProtected)
            {
                return TaskStatus.Failure;
            }

            if (bridge.HasStartedChainsaw)
            {
                return TaskStatus.Failure;
            }

            // 仅在进入战斗时启动电锯。
            if (!Controller.IsCombatEngaged && !Controller.IsFullyAwareOfPlayer)
            {
                return TaskStatus.Failure;
            }

            return TaskStatus.Success;
        }
    }

    /// <summary>敌人行为、动作与通行的行为树任务 NeedsChainsawIntroTaunt，由框架重写入口调度；具体读取和写入沿成员调用链执行。</summary>
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("电锯村民专属：判断是否需要处理一次入战威慑。")]
    public class NeedsChainsawIntroTaunt : EnemyConditionalTaskBase
    {
        /// <summary>中文含义：桥接。使用位置：NeedsChainsawIntroTaunt.OnAwake()、NeedsChainsawIntroTaunt.OnUpdate()。</summary>
        private ChainsawManAnimationBridge bridge;
        /// <summary>实现基类的 OnAwake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyConditionalTaskBase.OnAwake()、GameObject.GetComponent&lt;ChainsawManAnimationBridge&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override void OnAwake()
        {
            base.OnAwake();
            if (TaskGameObject != null)
            {
                bridge = TaskGameObject.GetComponent<ChainsawManAnimationBridge>();
            }
        }

        /// <summary>实现基类的 OnUpdate 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady && bridge != null && !Controller.IsDead && !Controller.IsNavigationTraversalProtected && bridge.HasStartedChainsaw && !bridge.HasResolvedIntroTaunt)
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
