using System;
using UnityEngine;
using GameArchitecture;

/// <summary>拥有敌人当前生命与死亡事实；不执行动画、掉落、行为树或任务结算。</summary>
[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "角色生命模块")]
public sealed partial class CharacterHealthModule : MonoBehaviour
{
    /// <summary>当前生命值值。</summary>
    private float currentHealthValue;
    /// <summary>是否已初始化。</summary>
    private bool initialized;
    /// <summary>是否死亡事实。</summary>
    private bool deathFact;
    /// <summary>当前生命值。</summary>
    public float CurrentHealth => currentHealthValue;
    /// <summary>是否具有死亡。</summary>
    public bool HasDeath => !initialized || deathFact;
    /// <summary>中文含义：是否就绪。</summary>
    public bool Ready => initialized;

    /// <summary>由角色启动入口显式调用一次，不依赖组件启动回调的先后顺序。</summary>
    /// <remarks>静态调用链：上游 VillagerController.Start()；下游（含内部回调体） ArgumentException.ArgumentException(string)、float.IsInfinity(float)、float.IsNaN(float)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="initialHealth">初始生命。</param>
    internal void Initialize(float initialHealth)
    {
        if (initialized)
        {
            throw new InvalidOperationException("生命模块已经初始化，禁止通过重复启动恢复角色");
        }

        if (float.IsNaN(initialHealth) || float.IsInfinity(initialHealth))
        {
            throw new ArgumentException("初始生命必须是有限数值");
        }

        currentHealthValue = initialHealth;
        deathFact = initialHealth <= 0f;
        initialized = true;
        if (deathFact)
        {
            revivalRules.ConfirmDeath();
        }
    }

    /// <summary>旧受伤入口的内部适配，保持存活角色原先的扣血与下限规则。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatModule.TakeDamage(float, Vector3, HitboxType, Vector3)；下游（含内部回调体） ArgumentException.ArgumentException(string)、float.IsInfinity(float)、float.IsNaN(float)、InvalidOperationException.InvalidOperationException(string)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">伤害。</param>
    internal float CommitCompatibleDamage(float damage)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("生命模块尚未初始化");
        }

        if (deathFact)
        {
            return 0f;
        }

        if (float.IsNaN(damage) || float.IsInfinity(damage))
        {
            throw new ArgumentException("伤害必须是有限数值");
        }

        // 原始生命。
float originalHealth = currentHealthValue;
        currentHealthValue = Mathf.Max(0f, currentHealthValue - damage);
        if (currentHealthValue <= 0f)
        {
            deathFact = true;
            revivalRules.ConfirmDeath();
        }

        return originalHealth - currentHealthValue;
    }

    /// <summary>兼容直接请求死亡的入口；重复确认不产生新结果。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.BeginDeath()；下游（含内部回调体） InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal bool ConfirmDeath()
    {
        if (!initialized)
        {
            throw new InvalidOperationException("生命模块尚未初始化");
        }

        if (deathFact)
        {
            return false;
        }

        currentHealthValue = 0f;
        deathFact = true;
        revivalRules.ConfirmDeath();
        return true;
    }

    /// <summary>保留既有性能采样布置能力；诊断不能复活已经死亡的角色。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Tick()；下游（含内部回调体） ArgumentException.ArgumentException(string)、float.IsInfinity(float)、float.IsNaN(float)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="health">生命。</param>
    internal void SetAliveCharacterValidateHealth(float health)
    {
        if (!initialized || deathFact)
        {
            throw new InvalidOperationException("只能设置已初始化且存活的验证对象");
        }

        if (health <= 0f || float.IsNaN(health) || float.IsInfinity(health))
        {
            throw new ArgumentException("验证生命必须为有限正值");
        }

        currentHealthValue = health;
    }
}
