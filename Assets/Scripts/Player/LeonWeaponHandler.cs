using UnityEngine;

/// <summary>玩家控制、动作与运动组件 LeonWeaponHandler，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>玩家装备映射与切换宿主，核对持有、动作事务和武器实例；运行数据沿 WeaponRuntimeModule 查询，模型、准星和镜头通过既有通知同步。</remarks>
public class LeonWeaponHandler : MonoBehaviour, ICrosshairWeaponProvider
{
    /// <summary>里昂玩家控制器。</summary>
    private LeonController controller;
    /// <summary>当前玩家动画动作协调器。</summary>
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>动画控制器。</summary>
    private Animator animator;
    /// <summary>里昂动画控制器驱动器。</summary>
    private LeonAnimatorDriver driver;
    /// <summary>当前里昂动画绑定组件。</summary>
    private LeonAnimRig animRig;
    /// <summary>武器视觉表现控制器。</summary>
    private LeonWeaponVisualController weaponVisualController;
    private LeonInputBridge inputBridge;
    /// <summary>小刀战斗模块。</summary>
    [Header("临时小刀入口")]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀模块")]
    [UnityEngine.InspectorName("小刀模块")]
    public KnifeCombatModule knifeModule;
    /// <summary>手枪。</summary>
    [Header("武器引用")]
    public WeaponBase pistol;
    /// <summary>步枪。</summary>
    public WeaponBase rifle;
    /// <summary>霰弹枪。</summary>
    public WeaponBase shotgun;
    /// <summary>小刀。</summary>
    public WeaponBase knife;
    /// <summary>初始手枪备弹。</summary>
    [Header("初始兼容备弹")]
    [UnityEngine.Serialization.FormerlySerializedAs("pistolReserveAmmo")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始手枪备弹")]
    [UnityEngine.InspectorName("初始手枪备弹")]
    private int initialPistolReserveAmmo = 12;
    /// <summary>手枪背包备弹。</summary>
    public int pistolReserveAmmo => RuntimeModule.QueryCompatibleReserveAmmo(WeaponType.Pistol);

    /// <summary>初始步枪备弹。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("rifleReserveAmmo")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始步枪备弹")]
    [UnityEngine.InspectorName("初始步枪备弹")]
    private int initialRifleReserveAmmo = 5;
    /// <summary>步枪背包备弹。</summary>
    public int rifleReserveAmmo => RuntimeModule.QueryCompatibleReserveAmmo(WeaponType.Rifle);

    /// <summary>初始霰弹备弹。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("shotgunReserveAmmo")]
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("初始霰弹备弹")]
    [UnityEngine.InspectorName("初始霰弹备弹")]
    private int initialShotgunAmmoReserveAmmo = 6;
    /// <summary>霰弹枪背包备弹。</summary>
    public int shotgunReserveAmmo => RuntimeModule.QueryCompatibleReserveAmmo(WeaponType.Shotgun);

    /// <summary>换弹警告弹药。</summary>
    [Header("教学提示")]
    [Min(0)]
    public int reloadWarningAmmo = 2;
    /// <summary>运行时模块。</summary>
    public WeaponRuntimeModule RuntimeModule { get; } = new WeaponRuntimeModule();
    /// <summary>当前装备。</summary>
    public WeaponType CurrentEquipment => RuntimeModule.Equipment;

    /// <summary>InitializeRuntimeModule 的业务处理入口；本方法直接依赖 WeaponRuntimeModule.InitializeEquipment(WeaponType, int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.InitPipeline()、LeonWeaponHandler.Start()；下游（含内部回调体） WeaponRuntimeModule.InitializeEquipment(WeaponType, int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="initialEquipment">初始装备。</param>
    public void InitializeRuntimeModule(WeaponType initialEquipment)
    {
        RuntimeModule.InitializeEquipment(initialEquipment, initialPistolReserveAmmo, initialRifleReserveAmmo, initialShotgunAmmoReserveAmmo);
    }

    /// <summary>待处理武器类型。</summary>
    private WeaponType pendingWeaponType;
    /// <summary>是否正在切换武器。</summary>
    private bool isSwitching;
    /// <summary>是否存在待处理的斩击快速选择武器。</summary>
    private bool hasPendingSlashQuickSelectWeapon;
    /// <summary>待处理斩击快速选择武器。</summary>
    private WeaponType pendingSlashQuickSelectWeapon;
    /// <summary>当前切换动作事务编号。</summary>
    private int currentSwitchTxId = -1;
    /// <summary>是否已经发出动作管线未就绪的警告。</summary>
    private bool newPipelineWarned;
    /// <summary>是否已绑定事件。</summary>
    private bool eventsBound;
    /// <summary>待处理仓库切换完成。</summary>
    private System.Action<bool> pendingStorageSwitchCompletion;
    /// <summary>已保存动画控制器更新模式。</summary>
    private AnimatorUpdateMode savedAnimatorUpdateMode;
    /// <summary>是否动画控制器更新模式被覆盖的。</summary>
    private bool animatorUpdateModeOverridden;
    /// <summary>是否等待中损坏恢复小刀。</summary>
    private bool waitingBrokenRecoverKnife;
    /// <summary>损坏后方候选武器。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly WeaponType[] brokenBackCandidateWeapon =
    {
        WeaponType.Pistol,
        WeaponType.Rifle,
        WeaponType.Shotgun
    };
    /// <summary>武器已切换时的通知回调。事件通信成员 OnWeaponSwitched，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<WeaponType> OnWeaponSwitched;
    /// <summary>数据提供者已切换时的通知回调。事件通信成员 OnDataProviderSwitched，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<ICrosshairDataProvider> OnDataProviderSwitched;
    /// <summary>当前弹匣弹量变化时的通知回调。事件通信成员 OnCurrentAmmoChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<WeaponType, int, int> OnCurrentAmmoChanged;
    /// <summary>当前武器低弹药时的通知回调。事件通信成员 OnCurrentWeaponLowAmmo，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action<WeaponType, int, int> OnCurrentWeaponLowAmmo;
    /// <summary>是否正在切换武器。</summary>
    public bool IsSwitching => isSwitching;

    /// <summary>快速选择选择目标。</summary>
    public WeaponType QuickSelectSelectTarget
    {
        get
        {
            if (hasPendingSlashQuickSelectWeapon)
            {
                return pendingSlashQuickSelectWeapon;
            }
            else
            {
                if (isSwitching)
                {
                    return pendingWeaponType;
                }
                else
                {
                    return CurrentEquipment;
                }
            }
        }
    }

    /// <summary>是否开火阻挡依据武器动作。</summary>
    public bool IsFireBlockedByWeaponAction
    {
        get
        {
            if (isSwitching)
            {
                return true;
            }

            return coordinator != null && (coordinator.HasPendingWeaponSwitch || coordinator.WaitingForWeaponExit || coordinator.IsWeaponSwitchInProgress);
        }
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;KnifeCombatModule&gt;()、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.InitializeRuntimeModule(WeaponType)、object.ReferenceEquals(object, object)、WeaponBase.Initialize(LeonController)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (knifeModule == null)
        {
            knifeModule = GetComponent<KnifeCombatModule>();
        }

        EnsureReferences();
        // 分支选出的控制器的初始装备。
global::WeaponType selectedValue3;
        if (controller != null)
        {
            selectedValue3 = controller.InitialEquipment;
        }
        else
        {
            selectedValue3 = WeaponType.Pistol;
        }

        InitializeRuntimeModule(selectedValue3);
        // 判空前缓存的手枪。
global::WeaponBase receiverValue4 = pistol;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Initialize(controller);
        }

        // 判空前缓存的步枪。
global::WeaponBase receiverValue5 = rifle;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Initialize(controller);
        }

        // 判空前缓存的霰弹枪。
global::WeaponBase receiverValue6 = shotgun;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Initialize(controller);
        }

        // 判空前缓存的小刀。
global::WeaponBase receiverValue7 = knife;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.Initialize(controller);
        }

        BindCoordinatorEvents();
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.AdvanceQuickSelectSwitch()、LeonWeaponHandler.RecoverStaleSwitchLock()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        RecoverStaleSwitchLock();
        AdvanceBrokenRecoverKnife();
        AdvanceQuickSelectSwitch();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.BindCoordinatorEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        BindCoordinatorEvents();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.UnbindCoordinatorEvents()、object.ReferenceEquals(object, object)、WeaponBase.CancelReload()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        waitingBrokenRecoverKnife = false;
        hasPendingSlashQuickSelectWeapon = false;
        UnbindCoordinatorEvents();
        // 判空前缓存的手枪。
global::WeaponBase receiverValue8 = pistol;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.CancelReload();
        }

        // 判空前缓存的步枪。
global::WeaponBase receiverValue9 = rifle;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.CancelReload();
        }

        // 判空前缓存的霰弹枪。
global::WeaponBase receiverValue10 = shotgun;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.CancelReload();
        }

        // 判空前缓存的小刀。
global::WeaponBase receiverValue11 = knife;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.CancelReload();
        }

        CompletePendingStorageSwitch(false);
    }

    /// <summary>EnsureReferences 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponent&lt;WeaponBase&gt;() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.Fire()、LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、LeonWeaponHandler.Start()、LeonWeaponHandler.SwitchTo(WeaponType) 等共 6 处；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()、Component.GetComponent&lt;LeonAnimatorDriver&gt;()、Component.GetComponent&lt;LeonAnimRig&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonWeaponVisualController&gt;()、Component.GetComponent&lt;WeaponBase&gt;() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void EnsureReferences()
    {
        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (driver == null)
        {
            driver = GetComponent<LeonAnimatorDriver>();
        }

        if (animRig == null)
        {
            animRig = GetComponent<LeonAnimRig>();
        }

        if (weaponVisualController == null)
        {
            weaponVisualController = GetComponent<LeonWeaponVisualController>();
        }

        if (inputBridge == null)
        {
            inputBridge = GetComponent<LeonInputBridge>();
        }

        if (pistol == null)
        {
            // 手枪变换。
Transform pistolTransform = transform.Find("手枪");
            if (pistolTransform != null)
            {
                pistol = pistolTransform.GetComponent<WeaponBase>();
            }
        }

        if (rifle == null)
        {
            // 步枪变换。
Transform rifleTransform = transform.Find("Rifle");
            if (rifleTransform != null)
            {
                rifle = rifleTransform.GetComponent<WeaponBase>();
            }
        }

        if (shotgun == null)
        {
            // 霰弹枪变换。
Transform shotgunTransform = transform.Find("Shotgun");
            if (shotgunTransform == null)
            {
                shotgunTransform = FindChildByName(transform, "Mossberg 590 Shotgun (Shooting)");
            }

            if (shotgunTransform == null)
            {
                shotgunTransform = FindChildByName(transform, "霰弹枪");
            }

            if (shotgunTransform != null)
            {
                shotgun = shotgunTransform.GetComponent<WeaponBase>();
            }
        }

        if (knife == null)
        {
            // 小刀变换。
Transform knifeTransform = transform.Find("小刀");
            if (knifeTransform != null)
            {
                knife = knifeTransform.GetComponent<WeaponBase>();
            }
        }
    }

    /// <summary>BindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnEnable()、LeonWeaponHandler.Start()、LeonWeaponHandler.SwitchTo(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void BindCoordinatorEvents()
    {
        if (eventsBound || coordinator == null)
        {
            return;
        }

        coordinator.OnVisualMarker += OnWeaponAudioMarker;
        coordinator.OnHolsterAnimationCompleted += OnHolsterAnimCompleted;
        coordinator.OnEquipAnimationCompleted += OnEquipAnimCompleted;
        coordinator.OnKnifeTransitionExited += OnKnifeTransitionExited;
        coordinator.OnWeaponActionInterrupted += OnWeaponActionInterrupted;
        eventsBound = true;
    }

    /// <summary>UnbindCoordinatorEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnWeaponAudioMarker(AnimationActionTransaction tx, VisualMarker marker)
    {
        WeaponType type = tx.sourceWeaponType;
        if (tx.weaponActionType == WeaponActionType.Equip || tx.weaponActionType == WeaponActionType.KnifeTake)
        {
            type = tx.pendingWeaponType;
        }

        if (marker == VisualMarker.MagazineAttached && type == WeaponType.Shotgun && controller != null
            && controller.TryGetComponent(out LeonCombat combat) && combat.IsShotgunReloadTransaction(tx))
        {
            return;
        }

        if (marker == VisualMarker.MagazineAttached && driver != null
            && (type == WeaponType.Rifle && driver.NativeCqbrReloadActive
                || type == WeaponType.Pistol && driver.NativePistolReloadActive))
        {
            CompleteReload(tx.transactionId);
        }

        if (TryGetComponent<NativeActionAudio>(out var nativeAudio) && nativeAudio.isActiveAndEnabled
            && nativeAudio.nativeWeaponMarkers && UsesSourceWeaponAudio(type, marker))
        {
            return;
        }

        WeaponBase weapon = GetWeapon(type);
        if (weapon != null && weapon.WeaponAudioRef != null)
        {
            weapon.WeaponAudioRef.PlayActionMarker(marker);
        }
    }

    private bool UsesSourceWeaponAudio(WeaponType type, VisualMarker marker)
    {
        if (driver == null)
        {
            return false;
        }
        if (type == WeaponType.Rifle && driver.NativeCqbrReloadActive)
        {
            return marker == VisualMarker.MagazineAttached || marker == VisualMarker.ReloadMechanism;
        }
        if (type == WeaponType.Pistol)
        {
            if (marker == VisualMarker.EquipVisualReveal && driver.NativePistolEquipActive)
            {
                return true;
            }
            if (driver.NativePistolReloadActive)
            {
                return marker == VisualMarker.MagazineDetached || marker == VisualMarker.MagazineAttached
                    || marker == VisualMarker.ReloadMechanism;
            }
        }
        if (type == WeaponType.Shotgun)
        {
            return (driver.NativeW870ReloadActive && marker == VisualMarker.ReloadMechanism)
                || (driver.IsNativeW870SwitchPlaying() && (marker == VisualMarker.EquipVisualReveal || marker == VisualMarker.HolsterVisualCommit));
        }
        return false;
    }

    private void UnbindCoordinatorEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (coordinator != null)
        {
            coordinator.OnVisualMarker -= OnWeaponAudioMarker;
            coordinator.OnHolsterAnimationCompleted -= OnHolsterAnimCompleted;
            coordinator.OnEquipAnimationCompleted -= OnEquipAnimCompleted;
            coordinator.OnKnifeTransitionExited -= OnKnifeTransitionExited;
            coordinator.OnWeaponActionInterrupted -= OnWeaponActionInterrupted;
        }

        eventsBound = false;
    }

    /// <summary>QuickSelectSwitch 的业务处理入口；本方法直接依赖 LeonController.CanAct()、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetWeapon(WeaponType)、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.IsGunWeapon(WeaponType)、LeonWeaponHandler.SwitchTo(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnSwitchWeapon(InputAction.CallbackContext)、LeonWeaponHandler.AdvanceQuickSelectSwitch()、WeaponWheelUI.Close(bool)、WeaponWheelUI.SelectRelative(int)；下游（含内部回调体） LeonController.CanAct()、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetWeapon(WeaponType)、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.IsGunWeapon(WeaponType)、LeonWeaponHandler.SwitchTo(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    private float quickSelectExpiresAt;

    public bool QuickSelectSwitch(WeaponType type)
    {
        EnsureReferences();
        if (!CanKeepQuickSelectInput() || !IsGunWeapon(type) || !HasWeaponInInventory(type) || GetWeapon(type) == null)
        {
            return false;
        }

        if (isSwitching || !CanExecuteQuickSelect())
        {
            quickSelectExpiresAt = Time.time + 2.5f;
            // 连续滚动只保留最后选择，正在播放的收枪、掏枪动作正常完成。
            pendingSlashQuickSelectWeapon = type;
            hasPendingSlashQuickSelectWeapon = !isSwitching || type != pendingWeaponType;
            return true;
        }

        hasPendingSlashQuickSelectWeapon = false;
        return controller.currentWeapon == type || SwitchTo(type);
    }

    /// <summary>AdvanceQuickSelectSwitch 的业务处理入口；本方法直接依赖 LeonController.CanAct()、LeonWeaponHandler.QuickSelectSwitch(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.Update()；下游（含内部回调体） LeonController.CanAct()、LeonWeaponHandler.QuickSelectSwitch(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanKeepQuickSelectInput()
    {
        return controller != null && coordinator != null && controller.NewPipelineValid &&
            controller.currentState != LeonState.Dead && controller.currentState != LeonState.QTE &&
            (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput) &&
            (!GameUIManager.HasInstance || !GameUIManager.Instance.IsPlayerInputBlocked);
    }

    private bool CanExecuteQuickSelect()
    {
        return controller.CanAct() && controller.currentState != LeonState.Reloading &&
            controller.currentState != LeonState.Shooting && !coordinator.HasActiveFullBodyAction &&
            !coordinator.HasActiveWeaponAction && !coordinator.WaitingForWeaponExit && !coordinator.HasPendingWeaponSwitch;
    }

    private void AdvanceQuickSelectSwitch()
    {
        if (!hasPendingSlashQuickSelectWeapon)
        {
            return;
        }

        if (!CanKeepQuickSelectInput() || Time.time > quickSelectExpiresAt)
        {
            hasPendingSlashQuickSelectWeapon = false;
            return;
        }

        if (isSwitching || !CanExecuteQuickSelect())
        {
            return;
        }

        // 目标武器类别。
WeaponType target = pendingSlashQuickSelectWeapon;
        hasPendingSlashQuickSelectWeapon = false;
        QuickSelectSwitch(target);
    }

    /// <summary>SwitchTo 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、LeonAnimationActionCoordinator.CreateActionRequest(string, ActionKind)、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)、LeonController.TrySetCrouching(bool)、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.HasUsableKnife() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.TryToggleKnifeMode()、LeonController.TrySwitchWeapon(WeaponType)、LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、LeonWeaponHandler.TrySwitchAwayForStorage(WeaponType, Action&lt;bool&gt;)；下游（含内部回调体） Debug.LogWarning(object)、LeonAnimationActionCoordinator.CreateActionRequest(string, ActionKind)、LeonAnimationActionCoordinator.RequestWeaponAction(ActionRequest, WeaponType, WeaponType)、LeonController.TrySetCrouching(bool)、LeonWeaponHandler.BindCoordinatorEvents()、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.HasUsableKnife() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    public bool SwitchTo(WeaponType type)
    {
        EnsureReferences();
        BindCoordinatorEvents();
        if (controller == null || coordinator == null)
        {
            return false;
        }

        if (isSwitching)
        {
            RecoverStaleSwitchLock();
            if (isSwitching)
            {
                return false;
            }
        }

        // 旧类型。
var oldType = controller.currentWeapon;
        if (oldType == type)
        {
            return false;
        }

        if (!controller.NewPipelineValid)
        {
            return false;
        }

        if (!IsSupportedWeapon(type))
        {
            if (!newPipelineWarned)
            {
                Debug.LogWarning("[LeonWeaponHandler] 新管线不支持该武器");
                newPipelineWarned = true;
            }

            return false;
        }

        if (type == WeaponType.Knife && knifeModule != null)
        {
            return false;
        }

        if (type == WeaponType.Knife && (knife == null || !HasWeaponInInventory(WeaponType.Knife) || !HasUsableKnife()))
        {
            return false;
        }

        if (type == WeaponType.Shotgun && shotgun == null)
        {
            return false;
        }

        if (type != WeaponType.Knife && !HasWeaponInInventory(type))
        {
            return false;
        }

        // 枪械之间切换时保留蹲伏；切到小刀前必须安全站起。
        if (type == WeaponType.Knife && controller.IsCrouching && !controller.TrySetCrouching(false))
        {
            return false;
        }

        isSwitching = true;
        pendingWeaponType = type;
        // 是否成功。
bool ok = coordinator.RequestWeaponAction(coordinator.CreateActionRequest("玩家武器入口", GameArchitecture.ActionKind.SwitchWeapon), oldType, type).HasAccept;
        if (!ok)
        {
            isSwitching = false;
            return false;
        }

        currentSwitchTxId = coordinator.CurrentWeaponTransactionId;
        return true;
    }

    // 新管线收枪完成事件
    /// <summary>OnHolsterAnimCompleted 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、WeaponRuntimeModule.CommitEquipment(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.ContinueEquipAfterWeaponCommit(int)、LeonAnimationActionCoordinator.ContinueKnifeTakeAfterWeaponCommit(int)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、WeaponRuntimeModule.CommitEquipment(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnHolsterAnimCompleted(AnimationActionTransaction tx)
    {
        if (!tx.isWeaponSwitch)
        {
            return;
        }

        if (tx.transactionId != currentSwitchTxId)
        {
            return;
        }

        if (coordinator == null)
        {
            return;
        }

        // 是否继续。
bool continued;
        if (tx.pendingWeaponType == WeaponType.Knife)
        {
            continued = coordinator.ContinueKnifeTakeAfterWeaponCommit(tx.transactionId);
        }
        else
        {
            RuntimeModule.CommitEquipment(tx.pendingWeaponType);
            continued = coordinator.ContinueEquipAfterWeaponCommit(tx.transactionId);
        }

        if (!continued)
        {
            isSwitching = false;
            currentSwitchTxId = -1;
            CompletePendingStorageSwitch(false);
        }
    }

    /// <summary>OnKnifeTransitionExited 的业务处理入口；本方法直接依赖 LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.FinishWeaponSwitch()、LeonWeaponHandler.IsGunWeapon(WeaponType)、LeonWeaponVisualController.FinishKnifeEquip() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonAnimationActionCoordinator.ContinueEquipAfterKnifeHide(int)、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimRig.CommitWeaponSwitchIK(WeaponType)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.FinishWeaponSwitch()、LeonWeaponHandler.IsGunWeapon(WeaponType)、LeonWeaponVisualController.FinishKnifeEquip() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnKnifeTransitionExited(AnimationActionTransaction tx)
    {
        if (!tx.isWeaponSwitch)
        {
            return;
        }

        if (tx.transactionId != currentSwitchTxId)
        {
            return;
        }

        if (tx.weaponActionType == WeaponActionType.KnifeTake && tx.pendingWeaponType == WeaponType.Knife)
        {
            RuntimeModule.CommitEquipment(WeaponType.Knife);
            // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue12 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                receiverValue12.SetKnifeMode(true);
            }

            // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue13 = animRig;
            if (!global::System.Object.ReferenceEquals(receiverValue13, null))
            {
                receiverValue13.CommitWeaponSwitchIK(WeaponType.Knife);
            }

            // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue14 = weaponVisualController;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                receiverValue14.FinishKnifeEquip();
            }

            FinishWeaponSwitch();
            return;
        }

        if (tx.weaponActionType == WeaponActionType.KnifeHide && IsGunWeapon(tx.pendingWeaponType))
        {
            RuntimeModule.CommitEquipment(tx.pendingWeaponType);
            // 判空前缓存的里昂动画控制器驱动器。
global::LeonAnimatorDriver receiverValue15 = driver;
            if (!global::System.Object.ReferenceEquals(receiverValue15, null))
            {
                receiverValue15.SetKnifeMode(false);
            }

            // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue16 = weaponVisualController;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                receiverValue16.PrepareGunEquipAfterKnifeHide(tx.pendingWeaponType);
            }

            if (!coordinator.ContinueEquipAfterKnifeHide(tx.transactionId))
            {
                isSwitching = false;
                currentSwitchTxId = -1;
                CompletePendingStorageSwitch(false);
            }
        }
    }

    // 新管线拔枪完成事件
    /// <summary>OnEquipAnimCompleted 的业务处理入口；本方法直接依赖 LeonWeaponHandler.FinishWeaponSwitch()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.FinishWeaponSwitch()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnEquipAnimCompleted(AnimationActionTransaction tx)
    {
        if (!tx.isWeaponSwitch)
        {
            return;
        }

        if (tx.transactionId != currentSwitchTxId)
        {
            return;
        }

        FinishWeaponSwitch();
    }

    /// <summary>FinishWeaponSwitch 的业务处理入口；本方法直接依赖 Action&lt;ICrosshairDataProvider&gt;.Invoke(ICrosshairDataProvider)、Action&lt;WeaponType&gt;.Invoke(WeaponType)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.OnEquipAnimCompleted(AnimationActionTransaction)、LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)；下游（含内部回调体） Action&lt;ICrosshairDataProvider&gt;.Invoke(ICrosshairDataProvider)、Action&lt;WeaponType&gt;.Invoke(WeaponType)、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void FinishWeaponSwitch()
    {
        isSwitching = false;
        currentSwitchTxId = -1;
        // 判空前缓存的武器已切换时的通知回调。
global::System.Action<global::WeaponType> receiverValue17 = OnWeaponSwitched;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.Invoke(controller.currentWeapon);
        }

        // 判空前缓存的数据提供者已切换时的通知回调。
global::System.Action<global::ICrosshairDataProvider> receiverValue18 = OnDataProviderSwitched;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.Invoke(GetCurrentWeapon() as ICrosshairDataProvider);
        }

        CompletePendingStorageSwitch(true);
        if (inputBridge != null)
        {
            inputBridge.NotifyActionWindowOpened();
        }
    }

    /// <summary>RecoverStaleSwitchLock 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CompletePendingStorageSwitch(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.SwitchTo(WeaponType)、LeonWeaponHandler.Update()；下游（含内部回调体） LeonWeaponHandler.CompletePendingStorageSwitch(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void RecoverStaleSwitchLock()
    {
        if (!isSwitching)
        {
            return;
        }

        if (coordinator == null)
        {
            isSwitching = false;
            currentSwitchTxId = -1;
            CompletePendingStorageSwitch(false);
            return;
        }

        if (coordinator.HasActiveWeaponAction || coordinator.HasPendingWeaponSwitch || coordinator.WaitingForWeaponExit)
        {
            return;
        }

        isSwitching = false;
        currentSwitchTxId = -1;
        CompletePendingStorageSwitch(false);
    }

    // 新管线武器动作中断
    /// <summary>OnWeaponActionInterrupted 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CompletePendingStorageSwitch(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.CompletePendingStorageSwitch(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="tx">动画动作事务。</param>
    private void OnWeaponActionInterrupted(AnimationActionTransaction tx)
    {
        if (!controller.NewPipelineValid)
        {
            return;
        }

        if (!tx.isWeaponSwitch)
        {
            return;
        }

        if (tx.transactionId != currentSwitchTxId)
        {
            return;
        }

        hasPendingSlashQuickSelectWeapon = false;
        isSwitching = false;
        currentSwitchTxId = -1;
        if (tx.weaponActionType == WeaponActionType.KnifeHide && controller.currentWeapon == WeaponType.Knife && driver != null)
        {
            driver.SetKnifeMode(true);
        }
        CompletePendingStorageSwitch(false);
    }

    /// <summary>
    /// 为寄存当前武器请求一次完整的收枪、业务提交和拔枪事务。
    /// 游戏暂停时仅让角色动画器使用非缩放时间，事务结束后恢复原模式。
    /// </summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.TransferToStorage(GridItem)；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、LeonWeaponHandler.BeginUnscaledAnimatorForPausedMenu()、LeonWeaponHandler.CompletePendingStorageSwitch(bool)、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.SwitchTo(WeaponType)、LeonWeaponHandler.TryGetOwnedFallbackWeapon(WeaponType, out WeaponType)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponToStore">武器目标寄存。</param>
    /// <param name="onCompleted">完成通知回调。</param>
    public bool TrySwitchAwayForStorage(WeaponType weaponToStore, System.Action<bool> onCompleted)
    {
        EnsureReferences();
        if (controller == null || pendingStorageSwitchCompletion != null)
        {
            return false;
        }

        if (controller.currentWeapon != weaponToStore)
        {
            // 判空前缓存的完成通知回调。
global::System.Action<bool> receiverValue19 = onCompleted;
            if (!global::System.Object.ReferenceEquals(receiverValue19, null))
            {
                receiverValue19.Invoke(true);
            }

            return true;
        }

        if (!TryGetOwnedFallbackWeapon(weaponToStore, out WeaponType /* 中文含义：后备。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ fallback))
        {
            return false;
        }

        pendingStorageSwitchCompletion = onCompleted;
        BeginUnscaledAnimatorForPausedMenu();
        if (SwitchTo(fallback))
        {
            return true;
        }

        CompletePendingStorageSwitch(false);
        return false;
    }

    /// <summary>当前背包中是否仍然拥有指定的正式枪械。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnSwitchWeapon(InputAction.CallbackContext)、LeonQTECoordinator.CanRestoreWeapon(WeaponType)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、LeonWeaponHandler.SwitchTo(WeaponType)、LeonWeaponHandler.TryGetOwnedFallbackWeapon(WeaponType, out WeaponType) 等共 7 处；下游（含内部回调体） InventoryManager.HasItem(string, int)、LeonWeaponHandler.GetWeapon(WeaponType)、LeonWeaponHandler.GetWeaponItemId(WeaponType)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    public bool HasWeaponInInventory(WeaponType type)
    {
        // 物品编号。
string itemId = GetWeaponItemId(type);
        if (string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        // 背包数据与操作宿主。
InventoryManager inventoryManager;
        if (InventoryManager.HasInstance)
        {
            inventoryManager = InventoryManager.Instance;
        }
        else
        {
            inventoryManager = null;
        }

        if (inventoryManager != null)
        {
            return inventoryManager.HasItem(itemId);
        }

        return GetWeapon(type) != null;
    }

    /// <summary>HasUsableKnife 的业务处理入口；本方法直接依赖 InventoryManager.HasUsableDurability(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.CanBeginParryAction.get、LeonCombat.HandleKnifeAttack()、LeonCombat.OnMeleeActionExited(AnimationActionTransaction)、LeonCombat.Update()、LeonQTECoordinator.CanQueryAssassinationPrompt() 等共 8 处；下游（含内部回调体） InventoryManager.HasUsableDurability(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool HasUsableKnife()
    {
        // 背包数据与操作宿主。
InventoryManager inventoryManager;
        if (InventoryManager.HasInstance)
        {
            inventoryManager = InventoryManager.Instance;
        }
        else
        {
            inventoryManager = null;
        }

        return knife != null && (inventoryManager == null || inventoryManager.GetPreferredKnife() != null);
    }

    /// <summary>HandleKnifeBroken 的业务处理入口；本方法直接依赖 LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.HasUsableKnife()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ConsumeKnifeDurability(int)、LeonQTECoordinator.ConsumeAssassinationKnifeDurability()；下游（含内部回调体） LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.HasUsableKnife()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HandleKnifeBroken()
    {
        if (!isActiveAndEnabled || controller == null || coordinator == null || !coordinator.isActiveAndEnabled || coordinator.IsDead || controller.currentWeapon != WeaponType.Knife || HasUsableKnife())
        {
            return;
        }

        // 损坏通常发生在挥刀或格挡事务中；保留请求，等原事务真正释放后再审批。
        waitingBrokenRecoverKnife = true;
        AdvanceBrokenRecoverKnife();
    }

    /// <summary>AdvanceBrokenRecoverKnife 的业务处理入口；本方法直接依赖 LeonWeaponHandler.HasUsableKnife()、LeonWeaponHandler.SwitchTo(WeaponType)、LeonWeaponHandler.TryGetOwnedFallbackWeapon(WeaponType, out WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.HandleKnifeBroken()、LeonWeaponHandler.Update()；下游（含内部回调体） LeonWeaponHandler.HasUsableKnife()、LeonWeaponHandler.SwitchTo(WeaponType)、LeonWeaponHandler.TryGetOwnedFallbackWeapon(WeaponType, out WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void AdvanceBrokenRecoverKnife()
    {
        if (!waitingBrokenRecoverKnife)
        {
            return;
        }

        if (controller == null || coordinator == null || !coordinator.isActiveAndEnabled || coordinator.IsDead || (controller.Health != null && controller.Health.IsDead) || controller.currentWeapon != WeaponType.Knife || HasUsableKnife())
        {
            waitingBrokenRecoverKnife = false;
            return;
        }

        if (isSwitching || coordinator.HasActiveWeaponAction || coordinator.HasActiveFullBodyAction || coordinator.WaitingForWeaponExit || coordinator.HasPendingWeaponSwitch)
        {
            return;
        }

        // 切换获准后仍保留请求，直到装备提交；受击打断收刀时可以在恢复后继续。
        if (TryGetOwnedFallbackWeapon(WeaponType.Knife, out WeaponType /* 中文含义：后备。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ fallback))
        {
            SwitchTo(fallback);
        }
    }

    /// <summary>TryGetOwnedFallbackWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.HasWeaponInInventory(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.AdvanceBrokenRecoverKnife()、LeonWeaponHandler.TrySwitchAwayForStorage(WeaponType, Action&lt;bool&gt;)；下游（含内部回调体） LeonWeaponHandler.HasWeaponInInventory(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="excluded">已排除。</param>
    /// <param name="fallback">后备。输出参数，由本入口写入并交给调用方。</param>
    bool TryGetOwnedFallbackWeapon(WeaponType excluded, out WeaponType fallback)
    {
        // 支持的武器集合。
WeaponType[] supportedWeapons = brokenBackCandidateWeapon;
        // 当前遍历索引。
for (int i = 0; i < supportedWeapons.Length; i++)
        {
            // 候选。
WeaponType candidate = supportedWeapons[i];
            if (candidate == excluded || !HasWeaponInInventory(candidate))
            {
                continue;
            }

            fallback = candidate;
            return true;
        }

        fallback = WeaponType.Pistol;
        return false;
    }

    /// <summary>GetWeaponItemId 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.HasWeaponInInventory(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    string GetWeaponItemId(WeaponType type)
    {
        return type switch
        {
            WeaponType.Pistol => "pistol",
            WeaponType.Rifle => "rifle",
            WeaponType.Shotgun => "shotgun",
            WeaponType.Knife => "knife",
            _ => null
        };
    }

    /// <summary>BeginUnscaledAnimatorForPausedMenu 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.TrySwitchAwayForStorage(WeaponType, Action&lt;bool&gt;)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BeginUnscaledAnimatorForPausedMenu()
    {
        if (animator == null || Time.timeScale > 0f || animatorUpdateModeOverridden)
        {
            return;
        }

        savedAnimatorUpdateMode = animator.updateMode;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animatorUpdateModeOverridden = true;
    }

    /// <summary>CompletePendingStorageSwitch 的业务处理入口；本方法直接依赖 Action&lt;bool&gt;.Invoke(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.FinishWeaponSwitch()、LeonWeaponHandler.OnDisable()、LeonWeaponHandler.OnHolsterAnimCompleted(AnimationActionTransaction)、LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)、LeonWeaponHandler.OnWeaponActionInterrupted(AnimationActionTransaction) 等共 7 处；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">是否成功。</param>
    void CompletePendingStorageSwitch(bool success)
    {
        if (animatorUpdateModeOverridden && animator != null)
        {
            animator.updateMode = savedAnimatorUpdateMode;
            animatorUpdateModeOverridden = false;
        }

        // 完成。
System.Action<bool> completion = pendingStorageSwitchCompletion;
        pendingStorageSwitchCompletion = null;
        // 判空前缓存的完成。
global::System.Action<bool> receiverValue22 = completion;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.Invoke(success);
        }
    }

    /// <summary>Fire 的业务处理入口；本方法直接依赖 LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、RE4CameraController.ResolveAimPoint(float)、WeaponBase.FireAt(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.FireWeapon()；下游（含内部回调体） LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、RE4CameraController.ResolveAimPoint(float)、WeaponBase.FireAt(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool Fire()
    {
        EnsureReferences();
        if (coordinator == null || !coordinator.isActiveAndEnabled || controller == null || controller.Health == null || controller.Health.IsDead)
        {
            return false;
        }

        if (IsFireBlockedByWeaponAction || controller.IsFirearmStanceBlocked
            || driver != null && (driver.NativeCqbrBlocksFire || driver.NativeW870BlocksFire || driver.NativeW870ReloadActive
                || driver.IsNativeW870ReloadPlaying()))
        {
            return false;
        }

        // 当前武器组件。
WeaponBase weapon = GetCurrentWeapon();
        if (weapon == null)
        {
            return false;
        }

        // 摄像机。
var cam = controller.playerCamera;
        if (cam == null)
        {
            return false;
        }

        // 范围。
float range;
        if (weapon.data != null)
        {
            range = weapon.data.range;
        }
        else
        {
            range = 100f;
        }

        // 让准星和枪口射线使用同一帧的敌人部位碰撞体。
        Physics.SyncTransforms();
        // 瞄准点。
Vector3 aimPoint;
        if (controller.CameraController != null)
        {
            aimPoint = controller.CameraController.ResolveAimPoint(range);
        }
        else
        {
            aimPoint = cam.transform.position + cam.transform.forward * range;
        }

        // 是否已开火。
bool fired = weapon.FireAt(aimPoint);
        if (fired)
        {
            NotifyCurrentAmmoChanged(weapon);
        }

        return fired;
    }

    /// <summary>GetCurrentWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()、LeonCombat.Update()、LeonCombat.UpdateShootStateLock()、LeonQTECoordinator.ResolveWeaponFromHandler()、LeonWeaponHandler.CanReload() 等共 13 处；下游（含内部回调体） LeonWeaponHandler.GetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public WeaponBase GetCurrentWeapon()
    {
        return GetWeapon(CurrentEquipment);
    }

    /// <summary>GetWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.RefreshAmmoText()、CrosshairUI.RefreshWeaponConfig(bool)、CrosshairUI.UpdateSpread()、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonWeaponHandler.CancelReload(WeaponType) 等共 14 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    public WeaponBase GetWeapon(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Pistol => pistol,
            WeaponType.Rifle => rifle,
            WeaponType.Shotgun => shotgun,
            WeaponType.Knife => knife,
            _ => pistol
        };
    }

    /// <summary>ForceSyncCurrentWeapon 的业务处理入口；本方法直接依赖 Action&lt;ICrosshairDataProvider&gt;.Invoke(ICrosshairDataProvider)、Action&lt;WeaponType&gt;.Invoke(WeaponType)、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetCurrentWeapon() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.InitPipeline()、LeonQTECoordinator.RestoreWeaponVisualAfterQTE(WeaponType, bool)、PerformanceSamplingEntry.Tick()；下游（含内部回调体） Action&lt;ICrosshairDataProvider&gt;.Invoke(ICrosshairDataProvider)、Action&lt;WeaponType&gt;.Invoke(WeaponType)、LeonAnimatorDriver.SetKnifeMode(bool)、LeonAnimatorDriver.SetWeaponType(WeaponType)、LeonAnimRig.SetWeapon(WeaponType)、LeonWeaponHandler.EnsureReferences()、LeonWeaponHandler.GetCurrentWeapon() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    /// <param name="notify">是否通知。</param>
    public void ForceSyncCurrentWeapon(WeaponType type, bool notify)
    {
        if (!IsSupportedWeapon(type))
        {
            return;
        }

        EnsureReferences();
        // 武器是否发生变化。
bool weaponChanged = controller != null && controller.currentWeapon != type;
        if (controller != null)
        {
            RuntimeModule.CommitEquipment(type);
        }

        // 判空前缓存的武器视觉表现控制器。
global::LeonWeaponVisualController receiverValue25 = weaponVisualController;
        if (!global::System.Object.ReferenceEquals(receiverValue25, null))
        {
            receiverValue25.SetEquippedWeapon(type);
        }

        // 判空前缓存的当前里昂动画绑定组件。
global::LeonAnimRig receiverValue26 = animRig;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            receiverValue26.SetWeapon(type);
        }

        if (driver != null)
        {
            driver.SetKnifeMode(type == WeaponType.Knife);
            if (IsGunWeapon(type))
            {
                driver.SetWeaponType(type);
            }
        }

        if (!notify || !weaponChanged)
        {
            return;
        }

        // 判空前缓存的武器已切换时的通知回调。
global::System.Action<global::WeaponType> receiverValue27 = OnWeaponSwitched;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.Invoke(type);
        }

        // 判空前缓存的数据提供者已切换时的通知回调。
global::System.Action<global::ICrosshairDataProvider> receiverValue28 = OnDataProviderSwitched;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.Invoke(GetCurrentWeapon() as ICrosshairDataProvider);
        }

        NotifyCurrentAmmoChanged(GetCurrentWeapon());
    }

    /// <summary>CompleteReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CompleteReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.CompleteReload()；下游（含内部回调体） LeonWeaponHandler.CompleteReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void CompleteReload()
    {
        // 分支选出的动作协调组件的当前武器事务编号。
int selectedValue29;
        if (coordinator != null)
        {
            selectedValue29 = coordinator.CurrentWeaponTransactionId;
        }
        else
        {
            selectedValue29 = -1;
        }

        CompleteReload(selectedValue29);
    }

    /// <summary>CompleteReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.ConsumeReserveAmmo(WeaponType, int)、LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnReloadAnimCompleted(AnimationActionTransaction)、LeonWeaponHandler.CompleteReload()；下游（含内部回调体） LeonWeaponHandler.ConsumeReserveAmmo(WeaponType, int)、LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="transactionId">事务编号。</param>
    public void CompleteReload(int transactionId)
    {
        // 当前武器组件。
var weapon = GetCurrentWeapon();
        if (weapon == null)
        {
            return;
        }

        // 武器类型。
WeaponType wt = controller.currentWeapon;
        // 目标加载。
int toLoad = weapon.CommitReload(transactionId, GetReserveAmmo(wt), false, /* 中文含义：大小。 */ amount => ConsumeReserveAmmo(wt, amount));
        if (toLoad > 0)
        {
            NotifyCurrentAmmoChanged(weapon);
        }
    }

    /// <summary>CanLoadOneRoundFromReserve 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.GetWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ShotgunShellReloadRoutine(int)、LeonCombat.TryCompleteShotgunShellReload(int)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)；下游（含内部回调体） LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.GetWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    public bool CanLoadOneRoundFromReserve(WeaponType weaponType)
    {
        // 当前武器组件。
WeaponBase weapon = GetWeapon(weaponType);
        if (weapon == null || weapon.data == null)
        {
            return false;
        }

        return weapon.currentAmmo < weapon.data.magazineSize && GetReserveAmmo(weaponType) > 0;
    }

    /// <summary>LoadOneRoundFromReserve 的业务处理入口；本方法直接依赖 LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    public bool LoadOneRoundFromReserve(WeaponType weaponType)
    {
        // 按原顺序缓存的实参武器类型。
global::WeaponType argumentValue31 = weaponType;
        // 分支选出的动作协调组件的当前武器事务编号。
int selectedValue30;
        if (coordinator != null)
        {
            selectedValue30 = coordinator.CurrentWeaponTransactionId;
        }
        else
        {
            selectedValue30 = -1;
        }

        return LoadOneRoundFromReserve(argumentValue31, selectedValue30);
    }

    /// <summary>LoadOneRoundFromReserve 的业务处理入口；本方法直接依赖 LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.ConsumeReserveAmmo(WeaponType, int)、LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.GetWeapon(WeaponType)、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ShotgunShellReloadRoutine(int)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType)；下游（含内部回调体） LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.ConsumeReserveAmmo(WeaponType, int)、LeonWeaponHandler.GetReserveAmmo(WeaponType)、LeonWeaponHandler.GetWeapon(WeaponType)、LeonWeaponHandler.NotifyCurrentAmmoChanged(WeaponBase)、WeaponBase.CommitReload(int, int, bool, Action&lt;int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    /// <param name="transactionId">事务编号。</param>
    public bool LoadOneRoundFromReserve(WeaponType weaponType, int transactionId)
    {
        if (!CanLoadOneRoundFromReserve(weaponType))
        {
            return false;
        }

        // 当前武器组件。
WeaponBase weapon = GetWeapon(weaponType);
        // 已加载。
int loaded = weapon.CommitReload(transactionId, GetReserveAmmo(weaponType), true, /* 中文含义：大小。 */ amount => ConsumeReserveAmmo(weaponType, amount));
        if (loaded == 0)
        {
            return false;
        }

        NotifyCurrentAmmoChanged(weapon);
        if (weaponType == WeaponType.Shotgun && weapon.WeaponAudioRef != null)
        {
            bool hasSourceCue = driver != null && driver.NativeW870ReloadActive
                && TryGetComponent<NativeActionAudio>(out var nativeAudio) && nativeAudio.isActiveAndEnabled && nativeAudio.w870InsertMarkers;
            if (!hasSourceCue)
            {
                weapon.WeaponAudioRef.PlayActionMarker(VisualMarker.MagazineAttached);
            }
        }

        return true;
    }

    /// <summary>CancelReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetWeapon(WeaponType)、WeaponBase.CancelReload()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.OnWeaponActionInterrupted(AnimationActionTransaction)、LeonCombat.StopShotgunShellReload(bool)、LeonCombat.TryCompleteShotgunShellReload(int)；下游（含内部回调体） LeonWeaponHandler.GetWeapon(WeaponType)、WeaponBase.CancelReload()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weaponType">武器类型。</param>
    public void CancelReload(WeaponType weaponType)
    {
        // 当前武器组件。
WeaponBase weapon = GetWeapon(weaponType);
        if (weapon != null)
        {
            weapon.CancelReload();
        }
    }

    /// <summary>NotifyCurrentAmmoChanged 的业务处理入口；本方法直接依赖 Action&lt;WeaponType, int, int&gt;.Invoke(WeaponType, int, int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.CompleteReload(int)、LeonWeaponHandler.Fire()、LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)；下游（含内部回调体） Action&lt;WeaponType, int, int&gt;.Invoke(WeaponType, int, int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器组件。</param>
    private void NotifyCurrentAmmoChanged(WeaponBase weapon)
    {
        if (weapon == null || weapon.data == null || controller == null)
        {
            return;
        }

        // 武器类型。
WeaponType weaponType = weapon.data.weaponType;
        // 弹匣尺寸。
int magazineSize = weapon.data.magazineSize;
        // 判空前缓存的当前弹匣弹量变化时的通知回调。
global::System.Action<global::WeaponType, int, int> receiverValue32 = OnCurrentAmmoChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue32, null))
        {
            receiverValue32.Invoke(weaponType, weapon.currentAmmo, magazineSize);
        }

        if (weaponType == WeaponType.Knife)
        {
            return;
        }

        if (controller.currentWeapon != weaponType)
        {
            return;
        }

        if (weapon.currentAmmo > reloadWarningAmmo)
        {
            return;
        }

        // 判空前缓存的当前武器低弹药时的通知回调。
global::System.Action<global::WeaponType, int, int> receiverValue33 = OnCurrentWeaponLowAmmo;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.Invoke(weaponType, weapon.currentAmmo, magazineSize);
        }
    }

    /// <summary>StartReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)、object.ReferenceEquals(object, object)、WeaponAudio.PlayReload()、WeaponBase.BeginReload(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.StartReload()；下游（含内部回调体） LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)、object.ReferenceEquals(object, object)、WeaponAudio.PlayReload()、WeaponBase.BeginReload(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StartReload()
    {
        // 当前武器组件。
var weapon = GetCurrentWeapon();
        if (weapon == null)
        {
            return;
        }

        if (weapon.isReloading || weapon.currentAmmo >= weapon.data.magazineSize)
        {
            return;
        }

        if (GetReserveAmmo(controller.currentWeapon) <= 0)
        {
            return;
        }

        if (weapon.BeginReload(coordinator.CurrentWeaponTransactionId))
        {
            // 判空前缓存的武器的武器音频引用。
global::WeaponAudio receiverValue34 = weapon.WeaponAudioRef;
            if (receiverValue34 != null && !receiverValue34.UsesStagedReload)
            {
                receiverValue34.PlayReload();
            }
        }
    }

    /// <summary>CanReload 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.CanReload()；下游（含内部回调体） LeonWeaponHandler.GetCurrentWeapon()、LeonWeaponHandler.GetReserveAmmo(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanReload()
    {
        // 当前武器组件。
var weapon = GetCurrentWeapon();
        if (weapon == null)
        {
            return false;
        }

        return weapon.currentAmmo < weapon.data.magazineSize && GetReserveAmmo(controller.currentWeapon) > 0;
    }

    /// <summary>GetReserveAmmo 的业务处理入口；本方法直接依赖 InventoryManager.GetItemCount(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.RefreshAmmoText()、DropResourceReader.Read()、LeonWeaponHandler.CanLoadOneRoundFromReserve(WeaponType)、LeonWeaponHandler.CanReload()、LeonWeaponHandler.CompleteReload(int) 等共 8 处；下游（含内部回调体） InventoryManager.GetItemCount(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    public int GetReserveAmmo(WeaponType weapon)
    {
        if (InventoryManager.HasInstance)
        {
            // 物品编号。
string itemId = weapon switch
            {
                WeaponType.Pistol => "pistol_ammo",
                WeaponType.Rifle => "rifle_ammo",
                WeaponType.Shotgun => "shotgun_ammo",
                _ => null
            };
            if (itemId != null)
            {
                return InventoryManager.Instance.GetItemCount(itemId);
            }
        }

        return weapon switch
        {
            WeaponType.Pistol => pistolReserveAmmo,
            WeaponType.Rifle => rifleReserveAmmo,
            WeaponType.Shotgun => shotgunReserveAmmo,
            _ => 0
        };
    }

    /// <summary>AddReserveAmmo 的业务处理入口；本方法直接依赖 WeaponRuntimeModule.IncreaseCompatibleReserveAmmo(WeaponType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.IItemUseTarget.IncreaseCompatibleReserveAmmo(WeaponType, int)；下游（含内部回调体） WeaponRuntimeModule.IncreaseCompatibleReserveAmmo(WeaponType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    /// <param name="amount">大小。</param>
    public void AddReserveAmmo(WeaponType weapon, int amount)
    {
        if (weapon != WeaponType.Knife)
        {
            RuntimeModule.IncreaseCompatibleReserveAmmo(weapon, amount);
        }
    }

    /// <summary>ConsumeReserveAmmo 的业务处理入口；本方法直接依赖 InventoryManager.RemoveItem(string, int)、WeaponRuntimeModule.ConsumptionCompatibleReserveAmmo(WeaponType, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.CompleteReload(int)、LeonWeaponHandler.LoadOneRoundFromReserve(WeaponType, int)；下游（含内部回调体） InventoryManager.RemoveItem(string, int)、WeaponRuntimeModule.ConsumptionCompatibleReserveAmmo(WeaponType, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="weapon">当前武器类别。</param>
    /// <param name="amount">大小。</param>
    public void ConsumeReserveAmmo(WeaponType weapon, int amount)
    {
        if (InventoryManager.HasInstance)
        {
            // 物品编号。
string itemId = weapon switch
            {
                WeaponType.Pistol => "pistol_ammo",
                WeaponType.Rifle => "rifle_ammo",
                WeaponType.Shotgun => "shotgun_ammo",
                _ => null
            };
            if (itemId != null)
            {
                InventoryManager.Instance.RemoveItem(itemId, amount);
                return;
            }
        }

        if (weapon != WeaponType.Knife)
        {
            RuntimeModule.ConsumptionCompatibleReserveAmmo(weapon, amount);
        }
    }

    /// <summary>IsGunWeapon 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool)、LeonWeaponHandler.IsSupportedWeapon(WeaponType)、LeonWeaponHandler.OnKnifeTransitionExited(AnimationActionTransaction)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    private static bool IsGunWeapon(WeaponType type)
    {
        return type == WeaponType.Pistol || type == WeaponType.Rifle || type == WeaponType.Shotgun;
    }

    /// <summary>IsSupportedWeapon 的业务处理入口；本方法直接依赖 LeonWeaponHandler.IsGunWeapon(WeaponType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.ForceSyncCurrentWeapon(WeaponType, bool)、LeonWeaponHandler.SwitchTo(WeaponType)；下游（含内部回调体） LeonWeaponHandler.IsGunWeapon(WeaponType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前武器类别。</param>
    private static bool IsSupportedWeapon(WeaponType type)
    {
        return IsGunWeapon(type) || type == WeaponType.Knife;
    }

    /// <summary>FindChildByName 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;Transform&gt;(bool)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.EnsureReferences()；下游（含内部回调体） Component.GetComponentsInChildren&lt;Transform&gt;(bool)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根变换节点。</param>
    /// <param name="childName">子级名称。</param>
    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        // 子对象集合。
Transform[] children = root.GetComponentsInChildren<Transform>(true);
        // 当前遍历索引。
for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    /// <summary>GetCurrentAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.RefreshAmmoText()、LeonController.GetCurrentAmmo()；下游（含内部回调体） LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int GetCurrentAmmo()
    {
        // 判空前缓存的武器组件。
global::WeaponBase receiverValue35 = GetCurrentWeapon();
        // 空值判断前缓存的接收者值35的当前弹匣弹量。
int? availableValue37;
        if (!global::System.Object.ReferenceEquals(receiverValue35, null))
        {
            availableValue37 = receiverValue35.currentAmmo;
        }
        else
        {
            availableValue37 = default;
        }

        if (availableValue37.HasValue)
        {
            return availableValue37.Value;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>GetMaxAmmo 的业务处理入口；本方法直接依赖 LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonController.GetMaxAmmo()；下游（含内部回调体） LeonWeaponHandler.GetCurrentWeapon()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public int GetMaxAmmo()
    {
        // 判空前缓存的武器组件。
global::WeaponBase receiverValue39 = GetCurrentWeapon();
        // 空值判断前缓存的接收者值40的弹匣尺寸。
int? availableValue43;
        if (!global::System.Object.ReferenceEquals(receiverValue39, null))
        {
            // 判空前缓存的接收者值39的数据。
global::WeaponData receiverValue40 = receiverValue39.data;
            if (!global::System.Object.ReferenceEquals(receiverValue40, null))
            {
                availableValue43 = receiverValue40.magazineSize;
            }
            else
            {
                availableValue43 = default;
            }
        }
        else
        {
            availableValue43 = default;
        }

        if (availableValue43.HasValue)
        {
            return availableValue43.Value;
        }
        else
        {
            return 0;
        }
    }
}
