using GameArchitecture;

/// <summary>按当前拥有者组合的值快照，不保存第二份可写角色状态。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人状态快照")]
public readonly struct EnemyStateSnapshot
{
    /// <summary>动作。</summary>
    public ActionHandle Action { get; }
    /// <summary>是否可以观察状态。</summary>
    public VillagerState CanObserveState { get; }
    /// <summary>生命。</summary>
    public float Health { get; }
    /// <summary>是否具有死亡。</summary>
    public bool HasDeath { get; }
    /// <summary>是否存在待处理的受击反应。</summary>
    public bool PendingHitReaction { get; }
    /// <summary>是否存在待处理的爬行。</summary>
    public bool PendingCrawl { get; }
    /// <summary>是否存在待处理的取得向上。</summary>
    public bool PendingGetUp { get; }
    /// <summary>是否具有进入战斗。</summary>
    public bool HasEnterCombat { get; }
    /// <summary>是否具有警戒。</summary>
    public bool HasAlert { get; }
    /// <summary>目标是否可见。</summary>
    public bool TargetVisible { get; }
    /// <summary>攻击是否就绪。</summary>
    public bool AttackReady { get; }
    /// <summary>是否攻击结束。</summary>
    public bool AttackEnd { get; }
    /// <summary>是否受击反应结束。</summary>
    public bool HitReactionEnd { get; }
    /// <summary>是否取得向上结束。</summary>
    public bool GetUpEnd { get; }

    /// <summary>构造并初始化 EnemyStateSnapshot 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 VillagerController.StateSnapshot.get；下游（含内部回调体） EnemyPerceptionModule.CanSeePlayer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">角色。</param>
    internal EnemyStateSnapshot(VillagerController character)
    {
        if (character.actionCoordinator != null)
        {
            Action = character.actionCoordinator.CurrentHandle;
        }
        else
        {
            Action = default;
        }

        CanObserveState = character.CurrentState;
        Health = character.currentHealth;
        HasDeath = character.IsDead;
        PendingHitReaction = character.HasPendingHit;
        PendingCrawl = character.HasPendingCrawl;
        PendingGetUp = character.HasPendingStandUp;
        HasEnterCombat = character.IsCombatEngaged;
        HasAlert = character.HasAnyPlayerAwareness;
        TargetVisible = character.perceptionModule != null && character.perceptionModule.CanSeePlayer();
        AttackReady = character.IsAttackReady;
        AttackEnd = character.AttackFinished;
        HitReactionEnd = character.HitFinished;
        GetUpEnd = character.StandUpFinished;
    }
}
