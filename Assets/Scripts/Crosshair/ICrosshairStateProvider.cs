using System;

/// <summary>
/// 准心状态接口 — 由 LeonCombat 或适配器实现
/// 提供瞄准/换弹状态及变化事件
/// </summary>
public interface ICrosshairStateProvider
{
    /// <summary>中文含义：是否正在瞄准。是否正在瞄准</summary>
    bool IsAiming { get; }

    /// <summary>中文含义：是否正在换弹。是否正在换弹</summary>
    bool IsReloading { get; }

    /// <summary>中文含义：瞄准状态变化时发出的通知。瞄准状态变化事件 (true=进入瞄准, false=退出瞄准)</summary>
    event Action<bool> OnAimStateChanged;
    /// <summary>中文含义：换弹状态变化时发出的通知。换弹状态变化事件 (true=开始换弹, false=换弹完成)</summary>
    event Action<bool> OnReloadingChanged;
}
