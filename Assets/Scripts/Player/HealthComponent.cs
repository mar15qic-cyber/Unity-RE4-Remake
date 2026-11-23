using UnityEngine;
using System;

/// <summary>
/// 血量组件 — 从 LeonController 提取的血量逻辑
/// 负责血量管理、伤害计算、治疗、死亡事件
/// </summary>
/// <remarks>玩家生命状态拥有者，接收伤害、治疗和上限变化，提交当前生命后同步通知生命变化与死亡；玩家外壳和 UI 读取已提交结果。</remarks>
public class HealthComponent : MonoBehaviour
{
    /// <summary>中文含义：最大生命值。使用位置：作为 Action&lt;float, float&gt;.Invoke(float, float) 的参数、作为 Mathf.Min(float, float) 的参数、HealthComponent.HealthRatio.get 等共 7 处。</summary>
    [Header("数值")]
    [HideInInspector]
    public float maxHealth = 100f;
    /// <summary>中文含义：当前生命值。使用位置：作为 Action&lt;float, float&gt;.Invoke(float, float) 的参数、作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 10 处。</summary>
    private float currentHealth;
    /// <summary>中文含义：当前生命值。当前血量</summary>
    public float CurrentHealth => currentHealth;
    /// <summary>中文含义：最大生命值。最大血量</summary>
    public float MaxHealth => maxHealth;
    public float InitialMaximum { get; private set; }

    /// <summary>中文含义：生命比例。血量比例 (0-1)</summary>
    public float HealthRatio
    {
        get
        {
            if (maxHealth > 0)
            {
                return currentHealth / maxHealth;
            }
            else
            {
                return 0f;
            }
        }
    }

    /// <summary>中文含义：是否死亡。是否死亡</summary>
    public bool IsDead => currentHealth <= 0f;

    /// <summary>中文含义：生命变化时的通知回调。血量变化事件 (current, max)</summary>
    public event Action<float, float> OnHealthChanged;
    /// <summary>中文含义：死亡时的通知回调。死亡事件</summary>
    public event Action OnDeath;
    /// <summary>初始化为满血（由 LeonController.Start 调用）</summary>
    /// <remarks>静态调用链：上游 LeonController.Start()、PerformanceSamplingEntry.Tick()；下游（含内部回调体） Action&lt;float, float&gt;.Invoke(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="max">中文含义：最大。使用位置：HealthComponent.Initialize(float)。</param>
    public void Initialize(float max)
    {
        InitialMaximum = max;
        maxHealth = max;
        currentHealth = maxHealth;
        // receiverValue2（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.Initialize(float)。
global::System.Action<float, float> receiverValue2 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>治疗</summary>
    /// <remarks>静态调用链：上游 LeonController.Heal(float)、PerformanceSamplingEntry.Tick()；下游（含内部回调体） Action&lt;float, float&gt;.Invoke(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">中文含义：大小。使用位置：作为 Mathf.Min(float, float) 的参数。</param>
    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        // receiverValue3（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.Heal(float)。
global::System.Action<float, float> receiverValue3 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>永久提高生命上限，当前生命值保持不变</summary>
    /// <remarks>静态调用链：上游 LeonController.IItemUseTarget.IncreaseHealthMaximum(float)；下游（含内部回调体） Action&lt;float, float&gt;.Invoke(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="amount">中文含义：大小。使用位置：HealthComponent.IncreaseMaxHealth(float)。</param>
    public void IncreaseMaxHealth(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        maxHealth += amount;
        // receiverValue4（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.IncreaseMaxHealth(float)。
global::System.Action<float, float> receiverValue4 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>受到伤害，返回实际造成的伤害值</summary>
    /// <remarks>静态调用链：上游 LeonController.ApplyDamage(float, Vector3, LeonHitDirection)、LeonController.TryApplyQTEFailureDamage(int, float, Vector3, Vector3)；下游（含内部回调体） Action.Invoke()、Action&lt;float, float&gt;.Invoke(float, float)、Mathf.Max(float, float)、Mathf.Min(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="damage">中文含义：伤害。使用位置：作为 Mathf.Max(float, float) 的参数、作为 Mathf.Min(float, float) 的参数。</param>
    public float TakeDamage(float damage)
    {
        if (IsDead)
        {
            return 0f;
        }

        // actual（实际）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：HealthComponent.TakeDamage(float)。
float actual = Mathf.Min(damage, currentHealth);
        currentHealth = Mathf.Max(0, currentHealth - damage);
        // receiverValue5（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.TakeDamage(float)。
global::System.Action<float, float> receiverValue5 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Invoke(currentHealth, maxHealth);
        }

        if (currentHealth <= 0)
        {
            // receiverValue6（判空前缓存的死亡时的通知回调）：从死亡时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.TakeDamage(float)。
global::System.Action receiverValue6 = OnDeath;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                receiverValue6.Invoke();
            }
        }

        return actual;
    }

    /// <summary>初始化为满血（用于重生/重置）</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action&lt;float, float&gt;.Invoke(float, float)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        // receiverValue7（判空前缓存的生命变化时的通知回调）：从生命变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、HealthComponent.ResetHealth()。
global::System.Action<float, float> receiverValue7 = OnHealthChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.Invoke(currentHealth, maxHealth);
        }
    }
}
