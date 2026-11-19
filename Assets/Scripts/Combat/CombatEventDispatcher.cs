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
    /// <summary>武器、命中与战斗的结构化数据 WeaponNoiseEvent；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    public readonly struct WeaponNoiseEvent
    {
        /// <summary>中文含义：位置。只读字段，初始化后不由普通写入口替换。使用位置：作为 VillagerController.ReceiveGunshot(Vector3) 的参数、CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float)、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。</summary>
        public readonly Vector3 Position;
        /// <summary>中文含义：半径。只读字段，初始化后不由普通写入口替换。使用位置：CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float)、EnemyAIManager.HandleWeaponFired(CombatEventDispatcher.WeaponNoiseEvent)。</summary>
        public readonly float Radius;
        /// <summary>构造并初始化 WeaponNoiseEvent 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 CombatEventDispatcher.PublishWeaponFired(Vector3, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="position">中文含义：位置。使用位置：CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float)。</param>
        /// <param name="radius">中文含义：半径。使用位置：CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float)。</param>
        public WeaponNoiseEvent(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>中文含义：命中已确认时的通知回调。命中确认事件 — 在 EnemyHitbox.OnHit 的 TakeDamage 完成之后广播。 携带完整的 HitResult，包含 isKill / isHeadshot 等最终结果。</summary>
    public static event Action<HitResult> OnHitConfirmed;
    /// <summary>中文含义：武器已开火时的通知回调。武器成功开火后广播的枪声事件。</summary>
    public static event Action<WeaponNoiseEvent> OnWeaponFired;
    /// <summary>ResetEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        OnHitConfirmed = null;
        OnWeaponFired = null;
    }

    /// <summary>
    /// 发布命中事件。
    /// </summary>
    /// <param name="result">中文含义：当前命中结果。完整的命中结果</param>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)；下游（含内部回调体） Action&lt;HitResult&gt;.Invoke(HitResult)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static void PublishHit(HitResult result)
    {
        // receiverValue1（判空前缓存的命中已确认时的通知回调）：从命中已确认时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CombatEventDispatcher.PublishHit(HitResult)。
global::System.Action<global::HitResult> receiverValue1 = OnHitConfirmed;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Invoke(result);
        }
    }

    /// <summary>
    /// 发布可被敌人感知的枪声；半径为零时不广播。
    /// </summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） Action&lt;CombatEventDispatcher.WeaponNoiseEvent&gt;.Invoke(CombatEventDispatcher.WeaponNoiseEvent)、CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：作为 CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 CombatEventDispatcher.WeaponNoiseEvent.WeaponNoiseEvent(Vector3, float) 的参数、CombatEventDispatcher.PublishWeaponFired(Vector3, float)。</param>
    public static void PublishWeaponFired(Vector3 position, float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        // receiverValue2（判空前缓存的武器已开火时的通知回调）：从武器已开火时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CombatEventDispatcher.PublishWeaponFired(Vector3, float)。
global::System.Action<global::CombatEventDispatcher.WeaponNoiseEvent> receiverValue2 = OnWeaponFired;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke(new WeaponNoiseEvent(position, radius));
        }
    }
}
