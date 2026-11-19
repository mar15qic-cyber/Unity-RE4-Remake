using UnityEngine;

/// <summary>
/// 将武器业务开火事件转发给枪模自身 Animator，并把枪模动画总时长提供给射击锁。
/// </summary>
[DisallowMultipleComponent]
public class WeaponAnimatorFireBridge : MonoBehaviour, IWeaponFireAnimation, IWeaponFireCadenceProvider
{
    /// <summary>中文含义：枪械动画控制器。使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()、WeaponAnimatorFireBridge.Start()、WeaponAnimatorFireBridge.TriggerFireAnimation()。</summary>
    [Header("枪模动画器")]
    public Animator gunAnimator;
    /// <summary>中文含义：开火触发器名称。使用位置：作为 Animator.StringToHash(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数。</summary>
    [Header("开火触发")]
    public string fireTriggerName = "Fire";
    /// <summary>中文含义：是否重置触发器之前集合。使用位置：WeaponAnimatorFireBridge.TriggerFireAnimation()。</summary>
    public bool resetTriggerBeforeSet = true;
    /// <summary>中文含义：是否自动解算动画片段集合。使用位置：WeaponAnimatorFireBridge.Start()。</summary>
    [Header("动画片段")]
    public bool autoResolveClips = true;
    /// <summary>中文含义：开火动画片段名称。使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()。</summary>
    public string fireClipName = "Mossberg@Fire";
    /// <summary>中文含义：拉动动画片段名称。使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()。</summary>
    public string pumpClipName = "Mossberg@LoadNext";
    /// <summary>中文含义：开火动画片段。使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()、WeaponAnimatorFireBridge.ResolveFireInterval()。</summary>
    public AnimationClip fireClip;
    /// <summary>中文含义：拉动动画片段。使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()、WeaponAnimatorFireBridge.ResolveFireInterval()。</summary>
    public AnimationClip pumpClip;
    /// <summary>中文含义：手动开火间隔。使用位置：WeaponAnimatorFireBridge.ResolveFireInterval()。</summary>
    [Header("射击节奏")]
    [Min(0f)]
    public float manualFireInterval = 0f;
    /// <summary>中文含义：额外锁定时间。使用位置：WeaponAnimatorFireBridge.ResolveFireInterval()。</summary>
    [Min(0f)]
    public float extraLockTime = 0.05f;
    /// <summary>中文含义：后备开火间隔。使用位置：WeaponAnimatorFireBridge.ResolveFireInterval()。</summary>
    [Min(0.01f)]
    public float fallbackFireInterval = 0.8f;
    /// <summary>中文含义：开火触发器哈希编号。使用位置：作为 Animator.ResetTrigger(int) 的参数、作为 Animator.SetTrigger(int) 的参数、WeaponAnimatorFireBridge.Start() 等共 4 处。</summary>
    private int fireTriggerHash;
    /// <summary>中文含义：缓存开火间隔。使用位置：WeaponAnimatorFireBridge.FireInterval.get、WeaponAnimatorFireBridge.Start()。</summary>
    private float cachedFireInterval;
    /// <summary>中文含义：开火间隔。</summary>
    public float FireInterval
    {
        get
        {
            if (cachedFireInterval <= 0f)
            {
                cachedFireInterval = ResolveFireInterval();
            }

            return cachedFireInterval;
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.StringToHash(string)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponentInChildren&lt;Animator&gt;()、WeaponAnimatorFireBridge.ResolveClipsFromAnimator()、WeaponAnimatorFireBridge.ResolveFireInterval()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (gunAnimator == null)
        {
            gunAnimator = GetComponent<Animator>();
        }

        if (gunAnimator == null)
        {
            gunAnimator = GetComponentInChildren<Animator>();
        }

        fireTriggerHash = Animator.StringToHash(fireTriggerName);
        if (autoResolveClips)
        {
            ResolveClipsFromAnimator();
        }

        cachedFireInterval = ResolveFireInterval();
    }

    /// <summary>TriggerFireAnimation 的业务处理入口；本方法直接依赖 Animator.ResetTrigger(int)、Animator.SetTrigger(int)、Animator.StringToHash(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.ResetTrigger(int)、Animator.SetTrigger(int)、Animator.StringToHash(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TriggerFireAnimation()
    {
        if (gunAnimator == null || !gunAnimator.isActiveAndEnabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(fireTriggerName))
        {
            return;
        }

        if (fireTriggerHash == 0)
        {
            fireTriggerHash = Animator.StringToHash(fireTriggerName);
        }

        if (resetTriggerBeforeSet)
        {
            gunAnimator.ResetTrigger(fireTriggerHash);
        }

        gunAnimator.SetTrigger(fireTriggerHash);
    }

    /// <summary>ResolveClipsFromAnimator 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 WeaponAnimatorFireBridge.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResolveClipsFromAnimator()
    {
        if (gunAnimator == null || gunAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        // clips（动画片段集合）：使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()。
AnimationClip[] clips = gunAnimator.runtimeAnimatorController.animationClips;
        // i（当前遍历索引）：使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()。
for (int i = 0; i < clips.Length; i++)
        {
            // clip（动画片段）：使用位置：WeaponAnimatorFireBridge.ResolveClipsFromAnimator()。
AnimationClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            if (fireClip == null && clip.name == fireClipName)
            {
                fireClip = clip;
            }

            if (pumpClip == null && clip.name == pumpClipName)
            {
                pumpClip = clip;
            }
        }
    }

    /// <summary>ResolveFireInterval 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 WeaponAnimatorFireBridge.FireInterval.get、WeaponAnimatorFireBridge.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private float ResolveFireInterval()
    {
        if (manualFireInterval > 0f)
        {
            return manualFireInterval;
        }

        // interval（间隔）：使用位置：WeaponAnimatorFireBridge.ResolveFireInterval()。
float interval = 0f;
        if (fireClip != null)
        {
            interval += fireClip.length;
        }

        if (pumpClip != null && pumpClip != fireClip)
        {
            interval += pumpClip.length;
        }

        if (interval > 0f)
        {
            return interval + extraLockTime;
        }

        return fallbackFireInterval;
    }
}
