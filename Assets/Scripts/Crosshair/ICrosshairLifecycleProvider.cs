using System;

/// <summary>
/// 准心生命周期接口 — 由 GameManager/InventorySceneLoader 或适配器实现
/// 提供暂停/死亡/物品栏状态及变化事件
/// </summary>
public interface ICrosshairLifecycleProvider
{
    /// <summary>中文含义：是否已暂停。游戏是否暂停</summary>
    bool IsPaused { get; }

    /// <summary>中文含义：是否已进入对局结束状态。玩家是否死亡 (GameOver)</summary>
    bool IsGameOver { get; }

    /// <summary>中文含义：背包界面是否打开。物品栏是否打开</summary>
    bool IsInventoryOpen { get; }

    /// <summary>中文含义：生命周期变化时的通知回调。生命周期变化事件</summary>
    event Action<CrosshairLifecycleEvent> OnLifecycleChanged;
}
