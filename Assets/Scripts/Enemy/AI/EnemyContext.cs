using UnityEngine;

/// <summary>
/// 敌人运动与动画执行上下文，由控制器统一维护，不参与智能状态转换。
/// </summary>
public class EnemyContext
{
    /// <summary>中文含义：站立行走移动参数。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、作为 Mathf.Min(float, float) 的参数、EnemyContext.MoveSpeedToAgentSpeed(float) 等共 4 处。</summary>
    public const float StandingWalkMoveParameter = 0.5f;
    /// <summary>中文含义：站立行走寻路代理速度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    public const float StandingWalkAgentSpeed = 1.2f;
    /// <summary>中文含义：站立追逐移动参数。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、EnemyContext.MaximumStandingMoveParameter、VillagerController.chaseSpeed 等共 4 处。</summary>
    public const float StandingChaseMoveParameter = 0.8f;
    /// <summary>中文含义：站立追逐寻路代理速度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Lerp(float, float, float) 的参数、VillagerController.CalculateStandingAdvanceSpeed(float)。</summary>
    public const float StandingChaseAgentSpeed = 1.65f;
    /// <summary>中文含义：行走慢速参数。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.InverseLerp(float, float, float) 的参数、EnemyContext.MoveSpeedToAgentSpeed(float)。</summary>
    private const float WalkSlowParameter = 0.2f;
    /// <summary>中文含义：行走常规参数。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、EnemyContext.MoveSpeedToAgentSpeed(float)。</summary>
    private const float WalkNormalParameter = 0.35f;
    /// <summary>中文含义：行走中等参数。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.InverseLerp(float, float, float) 的参数、EnemyContext.MoveSpeedToAgentSpeed(float)。</summary>
    private const float WalkMediumParameter = 0.43f;
    /// <summary>中文含义：行走慢速速度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    private const float WalkSlowSpeed = 0.21f;
    /// <summary>中文含义：行走常规速度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    private const float WalkNormalSpeed = 0.36f;
    /// <summary>中文含义：行走中等速度。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Lerp(float, float, float) 的参数。</summary>
    private const float WalkMediumSpeed = 0.34f;
    /// <summary>中文含义：玩家。使用位置：VillagerController.CalculateStandingAdvanceSpeed(float)、VillagerController.HasCrawlMovementDirection()、VillagerController.ResolveStableCrawlMoveDirection() 等共 6 处。</summary>
    public LeonController Player;
    /// <summary>中文含义：生命模块。只读字段，初始化后不由普通写入口替换。使用位置：EnemyContext.CurrentHealth.get、EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule)。</summary>
    private readonly CharacterHealthModule healthModule;
    /// <summary>中文含义：当前生命值。</summary>
    public float CurrentHealth
    {
        get
        {
            if (healthModule != null)
            {
                return healthModule.CurrentHealth;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：战斗模块。只读字段，初始化后不由普通写入口替换。使用位置：EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule)、EnemyContext.IsCrawl.get。</summary>
    private readonly EnemyCombatModule combatModule;
    /// <summary>构造并初始化 EnemyContext 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="healthModule">中文含义：生命模块。使用位置：EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule)。</param>
    /// <param name="combatModule">中文含义：战斗模块。使用位置：EnemyContext.EnemyContext(CharacterHealthModule, EnemyCombatModule)。</param>
    public EnemyContext(CharacterHealthModule healthModule, EnemyCombatModule combatModule = null)
    {
        this.healthModule = healthModule;
        this.combatModule = combatModule;
    }

    /// <summary>中文含义：是否爬行。使用位置：作为 EnemyCombatModule.DiagLog(string, string) 的参数。</summary>
    public bool IsCrawl => combatModule != null && combatModule.IsInCrawlPose;

    /// <summary>中文含义：是否生效。使用位置：VillagerController.Update()。</summary>
    public bool IsActive = true;
    /// <summary>中文含义：地面减速度大小。编译期常量，保持既有值与消费者映射。使用位置：作为 Mathf.Max(float, float) 的参数、EnemyContext.CrawlSpeed。</summary>
    public const float groundDecelerationAmount = .2f;
    /// <summary>中文含义：爬行速度。使用位置：作为 VillagerController.ResolveLocalAvoidanceDisplacement(Vector3) 的参数、EnemyAnimationAdapter.UpdateAnimator()、VillagerController.UpdateCodeDrivenMovement()。</summary>
    public float CrawlSpeed = .8f - groundDecelerationAmount;
    /// <summary>中文含义：最大站立移动参数。使用位置：作为 Mathf.Clamp(float, float, float) 的参数、VillagerController.Start()。</summary>
    public float MaximumStandingMoveParameter = StandingChaseMoveParameter;
    /// <summary>中文含义：是否具有爬行移动意图。爬行移动意图独立于寻路期望速度。 当寻路不可用时仍可驱动爬行动画与实际位移。</summary>
    public bool HasCrawlMoveIntent;
    /// <summary>中文含义：目标移动速度。目标移动动画参数。普通村民允许从走路过渡到匹配步幅的追击档。</summary>
    public float TargetMoveSpeed = 0f;
    /// <summary>ClampStandingMoveParameter 的业务处理入口；本方法直接依赖 Mathf.Clamp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginChase(bool)、EnemyActionCoordinator.BeginPatrol()、EnemyAnimationAdapter.UpdateAnimator()、EnemyContext.MoveSpeedToAgentSpeed(float)、VillagerController.GetTargetMoveSpeed() 等共 6 处；下游（含内部回调体） Mathf.Clamp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="moveSpeed">中文含义：移动速度。使用位置：作为 Mathf.Clamp(float, float, float) 的参数。</param>
    public float ClampStandingMoveParameter(float moveSpeed)
    {
        return Mathf.Clamp(moveSpeed, 0f, MaximumStandingMoveParameter);
    }

    /// <summary>
    /// 将站立移动动画参数映射为寻路位移速度。
    /// 走路与追击分别对应动画混合树的步幅标尺，不改变爬行和动作根位移。
    /// </summary>
    /// <remarks>静态调用链：上游 EnemyAnimationAdapter.UpdateLocomotionPlaybackRate(Vector3)、VillagerController.CalculateStandingAdvanceSpeed(float)、VillagerController.UpdateCodeDrivenMovement()；下游（含内部回调体） EnemyContext.ClampStandingMoveParameter(float)、Mathf.Clamp01(float)、Mathf.InverseLerp(float, float, float)、Mathf.Lerp(float, float, float)、Mathf.Min(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="moveSpeed">中文含义：移动速度。使用位置：作为 EnemyContext.ClampStandingMoveParameter(float) 的参数、作为 Mathf.Clamp01(float) 的参数、作为 Mathf.InverseLerp(float, float, float) 的参数 等共 5 处。</param>
    public float MoveSpeedToAgentSpeed(float moveSpeed)
    {
        if (moveSpeed <= 0f)
        {
            return 0f;
        }

        moveSpeed = ClampStandingMoveParameter(moveSpeed);
        if (moveSpeed > StandingWalkMoveParameter)
        {
            return Mathf.Lerp(StandingWalkAgentSpeed, StandingChaseAgentSpeed, Mathf.InverseLerp(StandingWalkMoveParameter, StandingChaseMoveParameter, moveSpeed));
        }

        if (moveSpeed <= WalkSlowParameter)
        {
            return Mathf.Lerp(0f, WalkSlowSpeed, Mathf.Clamp01(moveSpeed / WalkSlowParameter));
        }

        if (moveSpeed <= WalkNormalParameter)
        {
            return Mathf.Lerp(WalkSlowSpeed, WalkNormalSpeed, Mathf.InverseLerp(WalkSlowParameter, WalkNormalParameter, moveSpeed));
        }

        if (moveSpeed <= WalkMediumParameter)
        {
            return Mathf.Lerp(WalkNormalSpeed, WalkMediumSpeed, Mathf.InverseLerp(WalkNormalParameter, WalkMediumParameter, moveSpeed));
        }

        return Mathf.Lerp(WalkMediumSpeed, StandingWalkAgentSpeed, Mathf.InverseLerp(WalkMediumParameter, StandingWalkMoveParameter, Mathf.Min(moveSpeed, StandingWalkMoveParameter)));
    }
}
