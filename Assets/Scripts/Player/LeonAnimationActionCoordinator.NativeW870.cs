using UnityEngine;

public partial class LeonAnimationActionCoordinator
{
    private static readonly int[] W870LoopInsertFrames = { 33, 73, 113, 151, 186 };
    private const int W870StartFrames = 80;
    private const int W870LoopFrames = 193;
    private const int W870FirstInsertFrame = 75;
    private const float W870ContactFrameRounding = 0.5f;
    private LeonWeaponHandler nativeW870ReloadHandler;
    private bool nativeW870ReloadFromEmpty;
    private bool nativeW870StopRequested;
    private int nativeW870ShellCursor;

    private bool OwnsNativeW870Reload => weaponTx.sourceWeaponType == WeaponType.Shotgun
        && driver != null && driver.NativeW870ReloadActive;

    private void BeginNativeW870Reload()
    {
        if (!OwnsNativeW870Reload)
        {
            return;
        }

        nativeW870ReloadFromEmpty = false;
        nativeW870StopRequested = false;
        nativeW870ShellCursor = 0;
        if (TryGetComponent(out nativeW870ReloadHandler))
        {
            var weapon = nativeW870ReloadHandler.GetCurrentWeapon();
            nativeW870ReloadFromEmpty = weapon != null && weapon.currentAmmo == 0;
        }
    }

    internal void RequestNativeW870ReloadStop()
    {
        if (hasActiveWeaponTx && OwnsNativeW870Reload && !weaponTx.cancelled)
        {
            nativeW870StopRequested = true;
        }
    }

    private void HandleNativeW870ReloadProgress(float progress)
    {
        var stage = driver.W870Stage;
        if (stage is not (W870ReloadStage.Start or W870ReloadStage.Loop))
        {
            return;
        }
        if (nativeW870ReloadHandler == null)
        {
            FinishNativeW870Reload();
            return;
        }

        var weapon = nativeW870ReloadHandler.GetCurrentWeapon();
        if (weapon == null || nativeW870StopRequested && weapon.currentAmmo > 0)
        {
            FinishNativeW870Reload();
            return;
        }

        if (stage == W870ReloadStage.Start)
        {
            if (nativeW870ShellCursor == 0
                && progress * W870StartFrames + W870ContactFrameRounding >= W870FirstInsertFrame)
            {
                nativeW870ShellCursor = 1;
                nativeW870ReloadHandler.LoadOneRoundFromReserve(WeaponType.Shotgun, weaponTx.transactionId);
            }
            if (progress >= 0.99f)
            {
                if (nativeW870ReloadHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
                {
                    AdvanceNativeW870Reload(W870ReloadStage.Loop);
                }
                else
                {
                    FinishNativeW870Reload();
                }
            }
            return;
        }

        // 按最近源帧处理物理步采样，避免接触点因浮点误差晚一整步。
        float frame = progress * W870LoopFrames + W870ContactFrameRounding;
        int contactCount = W870LoopInsertFrames.Length;
        while (frame >= W870LoopInsertFrames[nativeW870ShellCursor % contactCount]
            + W870LoopFrames * (nativeW870ShellCursor / contactCount))
        {
            nativeW870ShellCursor++;
            if (!nativeW870ReloadHandler.LoadOneRoundFromReserve(WeaponType.Shotgun, weaponTx.transactionId)
                || !nativeW870ReloadHandler.CanLoadOneRoundFromReserve(WeaponType.Shotgun))
            {
                FinishNativeW870Reload();
                return;
            }
        }
    }

    private void FinishNativeW870Reload()
    {
        var finish = W870ReloadStage.Finish;
        if (nativeW870ReloadFromEmpty)
        {
            finish = W870ReloadStage.EmptyFinish;
        }
        AdvanceNativeW870Reload(finish);
    }

    private void AdvanceNativeW870Reload(W870ReloadStage stage)
    {
        // 新阶段沿用同一事务，但只接受新状态的进度和退出回调。
        weaponTx.entryBound = false;
        nativeW870ShellCursor = 0;
        driver.AdvanceNativeW870Reload(stage);
    }
}
