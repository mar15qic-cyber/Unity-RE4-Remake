using System;
using UnityEngine;

/// <summary>
/// 战斗事件分发器 — 静态事件出口，零业务依赖。
///
/// 职责：仅负责广播战斗事件，不保存状态、不缓存数据、不管理对象。
/// 这是事件出口，不是 Manager。
///
/// 当前事件：
///   OnHitConfirmed — 命中确认后广播（TakeDamage 已完成，携带完整 HitResult）
///   OnWeaponFired — 武器成功开火后广播可感知枪声
///
/// 运行子系统初始化时会清空监听，避免关闭脚本域重载后残留旧对象委托。
/// </summary>
public static class CombatEventDispatcher
{
    /// <summary>可供敌人等订阅者感知的枪声位置和半径。</summary>
    public readonly struct WeaponNoiseEvent
    {
        /// <summary>枪声世界坐标。</summary>
        public readonly Vector3 Position;
        /// <summary>听觉半径。</summary>
        public readonly float Radius;
        /// <summary>由 PublishWeaponFired 创建枪声事件，交给事件订阅者。</summary>
        /// <param name="position">位置。</param>
        /// <param name="radius">半径。</param>
        public WeaponNoiseEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>伤害提交后的命中通知，携带命中结果。</summary>
    public static event Action<HitResult> OnHitConfirmed;
    /// <summary>武器成功开火后的枪声通知。</summary>
    public static event Action<WeaponNoiseEvent> OnWeaponFired;
    /// <summary>由 Unity 在子系统注册时调用，清除残留的命中和枪声事件订阅。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        OnHitConfirmed = null;
        OnWeaponFired = null;
    }

    /// <summary>由伤害结算调用，将已提交的命中事实广播给命中事件订阅者。</summary>
    /// <param name="result">已提交的命中结果。</param>
    public static void PublishHit(HitResult result)
    {
        // 判空前缓存的命中已确认时的通知回调。
global::System.Action<global::HitResult> receiverValue1 = OnHitConfirmed;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(result);
        }
    }

    /// <summary>由武器开火调用，将正半径枪声广播给事件订阅者；半径非正时不广播。</summary>
    /// <param name="position">位置。</param>
    /// <param name="radius">半径。</param>
    public static void PublishWeaponFired(Vector3 position, float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        // 判空前缓存的武器已开火时的通知回调。
global::System.Action<global::CombatEventDispatcher.WeaponNoiseEvent> receiverValue2 = OnWeaponFired;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke(new WeaponNoiseEvent(position, radius));
        }
    }
}
