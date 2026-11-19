using GameArchitecture;

public sealed partial class EnemyActionCoordinator
{
    internal ActionHandle BeginNativeCombat(bool attacking)
    {
        if (!allowRequest || host == null || !host.IsRuntimeInitialized || IsDead || host.IsQTEControlled
            || host.IsNavigationTraversalProtected)
        {
            return default;
        }
        EnemyAIManager manager = EnemyAIManager.Instance;
        if (attacking)
        {
            // AI 已取得的抓取名额必须保留到接触或扑空，不能先释放并触发恢复冷却。
            if (manager == null || !manager.HasAttackReservation(host) && !manager.TryAcquireAttackReservation(host))
            {
                return default;
            }
        }
        else
        {
            CombatModule.EndAttack();
        }
        StopAndClearMovement();
        if (attacking)
        {
            SetObservableState(VillagerState.Attack);
        }
        else
        {
            SetObservableState(VillagerState.Staggered);
        }
        SetMovementMode(EnemyMovementMode.ActionRootMotion);
        RegisterAction();
        if (attacking)
        {
            manager.ConfirmAttack(host, CurrentHandle);
        }
        return CurrentHandle;
    }

    internal void EndNativeCombat(ActionHandle handle, bool recoverMovement)
    {
        if (handle != CurrentHandle || IsDead || host.IsQTEControlled)
        {
            return;
        }
        CombatModule.EndAttack();
        if (recoverMovement)
        {
            ResumeAfterStandUp();
            return;
        }

        RegisterAction();
        BeginIdle();
    }

    internal void ResumeAfterStandUp()
    {
        RegisterAction();
        BeginIdle();
        if (host.ShouldChasePlayer)
        {
            BeginChase(false);
            host.TryRecoverChasePath();
        }
    }
}
