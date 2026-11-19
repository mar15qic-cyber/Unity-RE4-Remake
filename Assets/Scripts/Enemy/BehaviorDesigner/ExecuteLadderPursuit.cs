using BehaviorDesigner.Runtime.Tasks;
using GameArchitecture;

namespace EnemyBehaviorTasks
{
    [TaskCategory("敌人智能/条件")]
    [TaskDescription("读取当前扶梯、攀爬分配或正在执行的梯子动作。")]
    public sealed class HasLadderPursuit : EnemyConditionalTaskBase
    {
        public override TaskStatus OnUpdate()
        {
            if (ComponentsReady)
            {
                var traversal = Controller.ArchitectureReadSpecialTraversal;
                if (traversal != null &&
                    (traversal.HasActiveLadderTraversal || traversal.TryReadLadderApproach(out _)))
                {
                    return TaskStatus.Success;
                }
            }

            return TaskStatus.Failure;
        }
    }

    [TaskCategory("敌人智能/动作")]
    [TaskDescription("走向分配的梯口，到达后向动作协调器请求通行。")]
    public sealed class ExecuteLadderPursuit : EnemyTaskBase
    {
        public override TaskStatus OnUpdate()
        {
            if (!ComponentsReady)
            {
                return TaskStatus.Failure;
            }

            var traversal = Controller.ArchitectureReadSpecialTraversal;
            if (traversal == null)
            {
                return TaskStatus.Failure;
            }

            if (traversal.HasActiveLadderTraversal)
            {
                return TaskStatus.Running;
            }

            if (!traversal.TryReadLadderApproach(out _))
            {
                return TaskStatus.Success;
            }

            if (traversal.CanBeginLadderTraversal())
            {
                Controller.actionCoordinator.RequestAction(ActionKind.Traversal, nameof(ExecuteLadderPursuit));
            }

            return TaskStatus.Running;
        }

        public override void OnEnd()
        {
            if (ComponentsReady)
            {
                var site = VillageLadderSite.GetAssignment(Controller);
                if (site != null)
                {
                    site.ClearAssignment(Controller, "行为树结束接近");
                }
            }
        }
    }
}
