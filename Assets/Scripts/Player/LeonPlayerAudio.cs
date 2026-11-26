using UnityEngine;

/// <summary>
/// 里昂角色音效：负责持续呼吸、跑步呼吸、死亡和切枪等玩家专属声音。
/// </summary>
[DisallowMultipleComponent]
public class LeonPlayerAudio : MonoBehaviour
{
    /// <summary>呼吸事件名称。</summary>
    [Header("事件名")]
    public string breathingEventName = AudioEventNames.PlayerBreathing;
    /// <summary>运行呼吸事件名称。</summary>
    public string runBreathingEventName = AudioEventNames.PlayerRunBreathing;
    [Tooltip("原地、战斗与受伤时使用的独立喘息事件")]
    public string idleBreathingEventName = "player_breathing_idle";
    public string combatBreathingEventName = "player_breathing_combat";
    public string injuredBreathingEventName = "player_breathing_injured";
    /// <summary>死亡事件名称。</summary>
    public string deathEventName = AudioEventNames.PlayerDeath;
    /// <summary>武器切换事件名称。</summary>
    public string weaponSwitchEventName = AudioEventNames.PlayerWeaponSwitch;
    /// <summary>移动中阈值。</summary>
    [Header("呼吸切换")]
    [Min(0f)]
    public float movingThreshold = 0.1f;
    /// <summary>里昂玩家控制器。</summary>
    private LeonController controller;
    /// <summary>管理当前装备与武器切换的组件。</summary>
    private LeonWeaponHandler weaponHandler;
    /// <summary>呼吸句柄。</summary>
    private AudioPlayHandle breathingHandle;
    private AudioPlayHandle hurtHandle;
    /// <summary>当前循环事件名称。</summary>
    private string currentLoopEventName;
    private float nextBreathTime;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonPlayerAudio.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        controller = GetComponent<LeonController>();
        weaponHandler = GetComponent<LeonWeaponHandler>();
        BindEvents();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerAudio.BindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        BindEvents();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerAudio.StopBreathingLoop()、LeonPlayerAudio.UnbindEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        StopBreathingLoop();
        StopHurt();
        UnbindEvents();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonPlayerAudio.UpdateBreathingLoop()、LeonPlayerAudio.UpdateLoopPosition()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        UpdateBreathingLoop();
        UpdateLoopPosition();
    }

    /// <summary>PlayDeath 的业务处理入口；本方法直接依赖 AudioManager.Play3D(string, Vector3)、LeonPlayerAudio.StopBreathingLoop()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.BeginDeathSequence()；下游（含内部回调体） AudioManager.Play3D(string, Vector3)、LeonPlayerAudio.StopBreathingLoop()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayHurt(float healthFractionLost)
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        string key = AudioEventNames.PlayerHit;
        if (audio.library != null)
        {
            if (healthFractionLost < 0.10f && audio.library.HasEvent(AudioEventNames.PlayerHitLight))
            {
                key = AudioEventNames.PlayerHitLight;
            }
            else if (healthFractionLost >= 0.25f && audio.library.HasEvent(AudioEventNames.PlayerHitHeavy))
            {
                key = AudioEventNames.PlayerHitHeavy;
            }
        }

        StopHurt();
        hurtHandle = audio.Play3D(key, transform.position);
    }

    private void StopHurt()
    {
        if (hurtHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(hurtHandle);
        }

        hurtHandle = default;
    }

    public void PlayDeath()
    {
        StopBreathingLoop();
        StopHurt();
        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue1 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue2;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            optionalValue2 = receiverValue1.Play3D(deathEventName, transform.position);
        }
        else
        {
            optionalValue2 = default;
        }
    }

    /// <summary>BindEvents 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;LeonWeaponHandler&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerAudio.OnEnable()、LeonPlayerAudio.Start()；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindEvents()
    {
        if (eventsBound)
        {
            return;
        }

        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }

        if (weaponHandler == null)
        {
            return;
        }

        weaponHandler.OnWeaponSwitched += HandleWeaponSwitched;
        eventsBound = true;
    }

    /// <summary>UnbindEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerAudio.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (weaponHandler != null)
        {
            weaponHandler.OnWeaponSwitched -= HandleWeaponSwitched;
        }

        eventsBound = false;
    }

    /// <summary>HandleWeaponSwitched 的业务处理入口；本方法直接依赖 AudioManager.Play3D(string, Vector3)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.Play3D(string, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">中文含义：武器类型。</param>
    private void HandleWeaponSwitched(WeaponType weaponType)
    {
        // 正式角色由原版动作音轨播放拔刀、收刀和换枪，装备事实不再叠加旧切换声。
        if (TryGetComponent<NativeActionAudio>(out var nativeAudio)
            && nativeAudio.isActiveAndEnabled && nativeAudio.nativeWeaponMarkers)
        {
            return;
        }
        WeaponBase weapon = weaponHandler.GetWeapon(weaponType);
        if (weapon != null && weapon.WeaponAudioRef != null && weapon.WeaponAudioRef.UsesStagedReload)
        {
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue3 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue4;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            optionalValue4 = receiverValue3.Play3D(weaponSwitchEventName, transform.position);
        }
        else
        {
            optionalValue4 = default;
        }
    }

    /// <summary>UpdateBreathingLoop 的业务处理入口；本方法直接依赖 AudioManager.Play3D(string, Vector3)、Component.GetComponent&lt;LeonController&gt;()、LeonPlayerAudio.StopBreathingLoop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerAudio.Update()；下游（含内部回调体） AudioManager.Play3D(string, Vector3)、Component.GetComponent&lt;LeonController&gt;()、LeonPlayerAudio.StopBreathingLoop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateBreathingLoop()
    {
        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        if (controller == null || controller.currentState == LeonState.Dead)
        {
            StopBreathingLoop();
            return;
        }

        // 是否运行中。
bool isRunning = controller.IsEffectiveSprinting;
        // 是否移动中。
bool isMoving = controller.MoveInput.magnitude > movingThreshold;
        var audio = AudioManager.Instance;
        if (audio == null || audio.IsAudioPaused || hurtHandle.IsValid)
        {
            StopBreathingLoop();
            return;
        }

        // 目标事件名称。
string targetEventName;
        if (controller.IsInjured)
        {
            targetEventName = injuredBreathingEventName;
        }
        else if (controller.IsAiming || controller.currentState is LeonState.Shooting or LeonState.MeleeAttacking)
        {
            targetEventName = combatBreathingEventName;
        }
        else if (isRunning && isMoving)
        {
            targetEventName = runBreathingEventName;
        }
        else if (isMoving)
        {
            targetEventName = breathingEventName;
        }
        else
        {
            targetEventName = idleBreathingEventName;
        }

        if (audio.library == null || !audio.library.HasEvent(targetEventName))
        {
            if (!isMoving)
            {
                StopBreathingLoop();
                return;
            }
            targetEventName = breathingEventName;
        }

        if (currentLoopEventName == targetEventName && (breathingHandle.IsValid || Time.time < nextBreathTime))
        {
            return;
        }

        StopBreathingLoop();
        currentLoopEventName = targetEventName;
        breathingHandle = audio.Play3D(targetEventName, transform.position);
        nextBreathTime = Time.time + 0.75f;
        if (breathingHandle.IsValid && breathingHandle.source.clip != null)
        {
            nextBreathTime += breathingHandle.source.clip.length / Mathf.Max(0.01f, Mathf.Abs(breathingHandle.source.pitch));
        }
    }

    /// <summary>UpdateLoopPosition 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerAudio.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateLoopPosition()
    {
        if (breathingHandle.IsValid && breathingHandle.source != null)
        {
            breathingHandle.source.transform.position = transform.position;
        }
    }

    /// <summary>StopBreathingLoop 的业务处理入口；本方法直接依赖 AudioManager.Stop(AudioPlayHandle)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonPlayerAudio.OnDisable()、LeonPlayerAudio.PlayDeath()、LeonPlayerAudio.UpdateBreathingLoop()；下游（含内部回调体） AudioManager.Stop(AudioPlayHandle)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopBreathingLoop()
    {
        if (breathingHandle.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.Stop(breathingHandle);
        }

        breathingHandle = default;
        currentLoopEventName = null;
    }
}
