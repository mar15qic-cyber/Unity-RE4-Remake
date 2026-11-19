using UnityEngine;

/// <summary>
/// 准心 Presenter — 薄控制器
///
/// 职责（仅限）：
///   1. Update() — 读 DataProvider.Spread → Lerp → 计算 Model → 推送 View（每帧仅 1 次 float 读取）
///   2. 事件回调 — 更新 Model 状态（Phase / HitType / 可见性）
///   3. 武器切换 — 切换 DataProvider + 通知 View 切换 Renderer
///
/// 禁止：
///   - 操作 Image / RectTransform / CanvasGroup
///   - 访问 LeonController / Animator / WeaponBase 等具体类
///   - 在 Update() 中轮询业务状态（IsAiming / currentState 等）
///   - 包含任何渲染逻辑
/// </summary>
/// <remarks>兼容旧准星资源的表现控制器；当前正式 CrosshairUI 会停用本组件，保留接口与字段用于既有资源兼容，不能据此视为正式入口。</remarks>
public class CrosshairPresenter : MonoBehaviour
{
    // === 接口源（Inspector 中拖入实现了接口的 MonoBehaviour）===
    /// <summary>中文含义：数据提供者来源。实现 ICrosshairDataProvider 的组件 (Stage 4: WeaponBase)。使用位置：CrosshairPresenter.ResolveInterfaces()。</summary>
    [Header("接口引用 (Inspector 拖入)")]
    [Tooltip("实现 ICrosshairDataProvider 的组件 (Stage 4: WeaponBase)")]
    [SerializeField]
    private MonoBehaviour dataProviderSource;
    /// <summary>中文含义：状态提供者来源。实现 ICrosshairStateProvider 的组件 (Stage 4: 适配器)。使用位置：CrosshairPresenter.ResolveInterfaces()。</summary>
    [Tooltip("实现 ICrosshairStateProvider 的组件 (Stage 4: 适配器)")]
    [SerializeField]
    private MonoBehaviour stateProviderSource;
    /// <summary>中文含义：生命周期提供者来源。实现 ICrosshairLifecycleProvider 的组件 (Stage 4: 适配器)。使用位置：CrosshairPresenter.ResolveInterfaces()。</summary>
    [Tooltip("实现 ICrosshairLifecycleProvider 的组件 (Stage 4: 适配器)")]
    [SerializeField]
    private MonoBehaviour lifecycleProviderSource;
    /// <summary>中文含义：武器提供者来源。实现 ICrosshairWeaponProvider 的组件 (Stage 4: LeonWeaponHandler)。使用位置：CrosshairPresenter.ResolveInterfaces()。</summary>
    [Tooltip("实现 ICrosshairWeaponProvider 的组件 (Stage 4: LeonWeaponHandler)")]
    [SerializeField]
    private MonoBehaviour weaponProviderSource;
    /// <summary>中文含义：视图。使用位置：CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.Update()。</summary>
    [Header("View 引用")]
    [SerializeField]
    private CrosshairView view;
    // === 运行时状态 ===
    /// <summary>中文含义：模型。使用位置：CrosshairPresenter.Awake()、CrosshairPresenter.Model.get。</summary>
    private CrosshairModel _model;
    /// <summary>中文含义：数据提供者。使用位置：作为 Mathf.Clamp01(float) 的参数、CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)、CrosshairPresenter.InitializeFromProviders() 等共 5 处。</summary>
    private ICrosshairDataProvider _dataProvider;
    /// <summary>中文含义：状态提供者。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)、CrosshairPresenter.ResolveInterfaces()、CrosshairPresenter.SubscribeEvents() 等共 4 处。</summary>
    private ICrosshairStateProvider _stateProvider;
    /// <summary>中文含义：生命周期提供者。使用位置：CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.ResolveInterfaces()、CrosshairPresenter.SubscribeEvents() 等共 4 处。</summary>
    private ICrosshairLifecycleProvider _lifecycleProvider;
    /// <summary>中文含义：武器提供者。使用位置：CrosshairPresenter.ResolveInterfaces()、CrosshairPresenter.SubscribeEvents()、CrosshairPresenter.UnsubscribeEvents()。</summary>
    private ICrosshairWeaponProvider _weaponProvider;
    // 生命周期标志（仅由事件更新，不在 Update 中轮询）
    /// <summary>中文含义：是否已暂停。使用位置：CrosshairPresenter.HandleLifecycleChanged(CrosshairLifecycleEvent)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.UpdateVisibility()。</summary>
    private bool _isPaused;
    /// <summary>中文含义：是否对局已经结束。使用位置：CrosshairPresenter.HandleLifecycleChanged(CrosshairLifecycleEvent)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.UpdateVisibility()。</summary>
    private bool _isGameOver;
    /// <summary>中文含义：是否已打开背包。使用位置：CrosshairPresenter.HandleLifecycleChanged(CrosshairLifecycleEvent)、CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.UpdateVisibility()。</summary>
    private bool _isInventoryOpen;
    /// <summary>中文含义：模型。Lazy Init 兜底 — Awake 中的初始化仍然存在，此属性仅在 Awake 未执行时自动创建。 场景：Domain Reload / Scene Reload 后 stale delegate 在 Awake 之前触发事件回调。</summary>
    private CrosshairModel Model
    {
        get
        {
            // availableValue1（空值判断前缓存的模型）：从模型读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CrosshairPresenter.Model.get。
global::CrosshairModel availableValue1 = _model;
            if (!global::System.Object.ReferenceEquals(availableValue1, null))
            {
                return availableValue1;
            }
            else
            {
                return (_model = new CrosshairModel());
            }
        }
    }

    // === 生命周期 ===
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairModel.CrosshairModel()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        _model = new CrosshairModel();
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.InitializeFromProviders()、CrosshairPresenter.ResolveInterfaces()、CrosshairPresenter.SubscribeEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        ResolveInterfaces();
        SubscribeEvents();
        InitializeFromProviders();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.UnsubscribeEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        UnsubscribeEvents();
    }

    // === 接口解析 ===
    /// <summary>ResolveInterfaces 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.OnEnable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ResolveInterfaces()
    {
        if (dataProviderSource is ICrosshairDataProvider /* 中文含义：目标位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ dp)
        {
            _dataProvider = dp;
        }

        if (stateProviderSource is ICrosshairStateProvider /* 中文含义：屏幕位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ sp)
        {
            _stateProvider = sp;
        }

        if (lifecycleProviderSource is ICrosshairLifecycleProvider /* 中文含义：局部位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ lp)
        {
            _lifecycleProvider = lp;
        }

        if (weaponProviderSource is ICrosshairWeaponProvider /* 中文含义：世界位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ wp)
        {
            _weaponProvider = wp;
        }
    }

    /// <summary>SubscribeEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.OnEnable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void SubscribeEvents()
    {
        if (_stateProvider != null)
        {
            _stateProvider.OnAimStateChanged += HandleAimChanged;
            _stateProvider.OnReloadingChanged += HandleReloadingChanged;
        }

        // Combat Event — 命中反馈通过统一事件总线，不再订阅 WeaponBase.OnHit
        CombatEventDispatcher.OnHitConfirmed += HandleHitConfirmed;
        if (_lifecycleProvider != null)
        {
            _lifecycleProvider.OnLifecycleChanged += HandleLifecycleChanged;
        }

        if (_weaponProvider != null)
        {
            _weaponProvider.OnDataProviderSwitched += HandleWeaponSwitched;
        }
    }

    /// <summary>UnsubscribeEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.OnDisable()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UnsubscribeEvents()
    {
        if (_stateProvider != null)
        {
            _stateProvider.OnAimStateChanged -= HandleAimChanged;
            _stateProvider.OnReloadingChanged -= HandleReloadingChanged;
        }

        CombatEventDispatcher.OnHitConfirmed -= HandleHitConfirmed;
        if (_lifecycleProvider != null)
        {
            _lifecycleProvider.OnLifecycleChanged -= HandleLifecycleChanged;
        }

        if (_weaponProvider != null)
        {
            _weaponProvider.OnDataProviderSwitched -= HandleWeaponSwitched;
        }
    }

    /// <summary>InitializeFromProviders 的业务处理入口；本方法直接依赖 CrosshairPresenter.UpdateVisibility()、CrosshairView.SwitchRenderer(CrosshairConfig)、ICrosshairDataProvider.GetCrosshairConfig()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.OnEnable()；下游（含内部回调体） CrosshairPresenter.UpdateVisibility()、CrosshairView.SwitchRenderer(CrosshairConfig)、ICrosshairDataProvider.GetCrosshairConfig()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializeFromProviders()
    {
        // 从 DataProvider 获取初始配置
        if (_dataProvider != null)
        {
            Model.Config = _dataProvider.GetCrosshairConfig();
            // receiverValue3（判空前缓存的视图）：从视图读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CrosshairPresenter.InitializeFromProviders()。
global::CrosshairView receiverValue3 = view;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.SwitchRenderer(Model.Config);
            }
        }

        // 从 LifecycleProvider 读取初始生命周期状态
        if (_lifecycleProvider != null)
        {
            _isPaused = _lifecycleProvider.IsPaused;
            _isGameOver = _lifecycleProvider.IsGameOver;
            _isInventoryOpen = _lifecycleProvider.IsInventoryOpen;
        }

        UpdateVisibility();
    }

    // === Update — 薄 Presenter：仅 Spread 读取 + Model 更新 + View 推送 ===
    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimationCurve.Evaluate(float)、CrosshairView.ApplyModel(CrosshairModel)、CrosshairView.SwitchRenderer(CrosshairConfig)、ICrosshairDataProvider.GetCrosshairConfig()、ICrosshairDataProvider.GetSpreadNormalized()、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (_dataProvider == null)
        {
            return;
        }

        // 延迟获取 Config — WeaponBase.Initialize() 可能在 OnEnable 之后才执行
        if (Model.Config == null)
        {
            Model.Config = _dataProvider.GetCrosshairConfig();
            if (Model.Config == null)
            {
                return;
            }

            // receiverValue4（判空前缓存的视图）：从视图读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CrosshairPresenter.Update()。
global::CrosshairView receiverValue4 = view;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.SwitchRenderer(Model.Config);
            }
        }

        // 1. 读取真实 Spread（每帧仅 1 次 float）
        Model.CurrentSpread = Mathf.Clamp01(_dataProvider.GetSpreadNormalized());
        // 2. Lerp 平滑
        // lerpSpeed（插值速度）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、CrosshairPresenter.Update()。
float lerpSpeed;
        if (Model.Config.dynamicSpread)
        {
            lerpSpeed = Model.Config.dynamicSpreadSpeed;
        }
        else
        {
            lerpSpeed = 100f;
        }

        Model.DisplayedSpread = Mathf.Lerp(Model.DisplayedSpread, Model.CurrentSpread, Time.deltaTime * lerpSpeed);
        // 3. 通过 spreadCurve 计算最终 UI 尺寸
        // curveValue（曲线值）：保存 AnimationCurve.Evaluate(float) 的返回结果，供当前步骤使用。使用位置：CrosshairPresenter.Update()。
float curveValue = Model.Config.spreadCurve.Evaluate(Model.DisplayedSpread);
        Model.ComputedSize = Model.Config.minSize + (Model.Config.maxSize - Model.Config.minSize) * curveValue;
        // 4. 命中反馈计时器衰减
        if (Model.HitTimer > 0f)
        {
            Model.HitTimer -= Time.deltaTime * Model.Config.hitFeedbackFadeSpeed;
            if (Model.HitTimer <= 0f)
            {
                Model.HitTimer = 0f;
                Model.HitBlend = 0f;
            }
            else
            {
                Model.HitBlend = Mathf.Clamp01(Model.HitTimer / Model.Config.hitFeedbackDuration);
            }
        }

        // 5. 推送到 View（纯数据推送，不含任何渲染逻辑）
        // receiverValue6（判空前缓存的视图）：从视图读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CrosshairPresenter.Update()。
global::CrosshairView receiverValue6 = // 5. 推送到 View（纯数据推送，不含任何渲染逻辑）
 view;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.ApplyModel(Model);
        }
    }

    // === 事件处理（不在 Update 中轮询）===
    /// <summary>HandleAimChanged 的业务处理入口；本方法直接依赖 CrosshairPresenter.UpdateVisibility()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.UpdateVisibility()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="isAiming">中文含义：是否正在瞄准。使用位置：CrosshairPresenter.HandleAimChanged(bool)。</param>
    private void HandleAimChanged(bool isAiming)
    {
        if (Model.Config == null)
        {
            return;
        }

        if (isAiming)
        {
            Model.Phase = CrosshairPhase.Aim;
        }
        else
        {
            // assignmentReceiver7（赋值前保存的目标模型）：从模型读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：CrosshairPresenter.HandleAimChanged(bool)。
global::CrosshairModel assignmentReceiver7 = Model;
            if (Model.Config.showInHipFire)
            {
                assignmentReceiver7.Phase = CrosshairPhase.HipFire;
            }
            else
            {
                assignmentReceiver7.Phase = CrosshairPhase.Hidden;
            }
        }

        UpdateVisibility();
    }

    /// <summary>HandleReloadingChanged 的业务处理入口；本方法直接依赖 CrosshairPresenter.UpdateVisibility()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.UpdateVisibility()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="isReloading">中文含义：是否正在换弹。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)。</param>
    private void HandleReloadingChanged(bool isReloading)
    {
        if (Model.Config == null)
        {
            return;
        }

        if (isReloading)
        {
            // assignmentReceiver9（赋值前保存的目标模型）：从模型读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)。
global::CrosshairModel assignmentReceiver9 = Model;
            if (Model.Config.showInReload)
            {
                assignmentReceiver9.Phase = CrosshairPhase.Reload;
            }
            else
            {
                assignmentReceiver9.Phase = CrosshairPhase.Hidden;
            }
        }
        else
        {
            // 换弹完成 — 根据当前瞄准状态恢复
            // aiming（是否瞄准中）：使用位置：CrosshairPresenter.HandleReloadingChanged(bool)。
bool aiming = _stateProvider != null && _stateProvider.IsAiming;
            // assignmentReceiver11（赋值前保存的目标模型）：从模型读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)。
global::CrosshairModel assignmentReceiver11 = Model;
            if (aiming)
            {
                assignmentReceiver11.Phase = CrosshairPhase.Aim;
            }
            else
            {
                // selectedValue12（分支选出的准星阶段的髋部开火）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：CrosshairPresenter.HandleReloadingChanged(bool)。
global::CrosshairPhase selectedValue12;
                if (Model.Config.showInHipFire)
                {
                    selectedValue12 = CrosshairPhase.HipFire;
                }
                else
                {
                    selectedValue12 = CrosshairPhase.Hidden;
                }

                assignmentReceiver11.Phase = (selectedValue12);
            }
        }

        UpdateVisibility();
    }

    /// <summary>
    /// 命中确认 — 由 CombatEventDispatcher.OnHitConfirmed 事件触发。
    /// 直接读取 HitResult 中的 isHeadshot / isKill / hitboxType，映射为 HitType。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前命中结果。使用位置：CrosshairPresenter.HandleHitConfirmed(HitResult)。</param>
    private void HandleHitConfirmed(HitResult result)
    {
        if (Model.Config == null)
        {
            return;
        }

        // HitResult → HitType 映射
        // hitType（命中类型）：使用位置：CrosshairPresenter.HandleHitConfirmed(HitResult)。
HitType hitType = HitType.Normal;
        if (result.isKill)
        {
            hitType = HitType.Kill;
        }
        else if (result.isHeadshot)
        {
            hitType = HitType.Headshot;
        }

        Model.CurrentHitType = hitType;
        Model.HitTimer = Model.Config.hitFeedbackDuration;
        Model.HitBlend = 1f;
    }

    /// <summary>HandleLifecycleChanged 的业务处理入口；本方法直接依赖 CrosshairPresenter.UpdateVisibility()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.UpdateVisibility()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="evt">中文含义：当前准星生命周期事件。使用位置：CrosshairPresenter.HandleLifecycleChanged(CrosshairLifecycleEvent)。</param>
    private void HandleLifecycleChanged(CrosshairLifecycleEvent evt)
    {
        switch (evt)
        {
            case CrosshairLifecycleEvent.PauseEntered:
                _isPaused = true;
                break;
            case CrosshairLifecycleEvent.PauseExited:
                _isPaused = false;
                break;
            case CrosshairLifecycleEvent.Death:
                _isGameOver = true;
                break;
            case CrosshairLifecycleEvent.Respawn:
                _isGameOver = false;
                break;
            case CrosshairLifecycleEvent.InventoryOpened:
                _isInventoryOpen = true;
                break;
            case CrosshairLifecycleEvent.InventoryClosed:
                _isInventoryOpen = false;
                break;
            case CrosshairLifecycleEvent.CutsceneStarted:
                _isPaused = true;
                break;
            case CrosshairLifecycleEvent.CutsceneEnded:
                _isPaused = false;
                break;
        }

        UpdateVisibility();
    }

    // === 武器切换（由 Stage 4 适配器调用）===
    /// <summary>
    /// 武器切换 — 由 ICrosshairWeaponProvider.OnDataProviderSwitched 事件触发。
    /// 切换 DataProvider 引用，加载新配置。
    /// 命中反馈已迁移至 CombatEventDispatcher，不再需要订阅/取消 OnHit。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CrosshairPresenter.UpdateVisibility()、CrosshairView.SwitchRenderer(CrosshairConfig)、Debug.LogWarning(object)、ICrosshairDataProvider.GetCrosshairConfig()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="newProvider">中文含义：新提供者。使用位置：CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)。</param>
    public void HandleWeaponSwitched(ICrosshairDataProvider newProvider)
    {
        // newProvider 为 null — 说明 GetCurrentWeapon() 返回了 null
        // 这通常意味着 WeaponBase 字段未正确初始化
        if (newProvider == null)
        {
            Debug.LogWarning("[CrosshairPresenter] HandleWeaponSwitched: newProvider is null — WeaponBase 可能未初始化");
            return;
        }

        _dataProvider = newProvider;
        // 加载新武器配置 + 切换渲染策略
        // config（当前准星配置）：保存 ICrosshairDataProvider.GetCrosshairConfig() 的返回结果，供当前步骤使用。使用位置：CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)。
var config = _dataProvider.GetCrosshairConfig();
        if (config == null)
        {
            Debug.LogWarning("[CrosshairPresenter] GetCrosshairConfig() returned null — WeaponData 可能未赋值");
            return;
        }

        Model.Config = config;
        // receiverValue14（判空前缓存的视图）：从视图读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)。
global::CrosshairView receiverValue14 = view;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.SwitchRenderer(Model.Config);
        }

        // 切换期间标记为 Switching，后续 Aim 事件会恢复
        Model.Phase = CrosshairPhase.Switching;
        UpdateVisibility();
    }

    // === 可见性计算（仅由事件驱动的状态决定，不在 Update 中轮询）===
    /// <summary>UpdateVisibility 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CrosshairPresenter.HandleAimChanged(bool)、CrosshairPresenter.HandleLifecycleChanged(CrosshairLifecycleEvent)、CrosshairPresenter.HandleReloadingChanged(bool)、CrosshairPresenter.HandleWeaponSwitched(ICrosshairDataProvider)、CrosshairPresenter.InitializeFromProviders()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void UpdateVisibility()
    {
        if (Model.Config == null)
        {
            Model.IsVisible = false;
            return;
        }

        // None 类型 — 永远隐藏（Knife）
        if (Model.Config.crosshairType == CrosshairType.None)
        {
            Model.IsVisible = false;
            return;
        }

        // 生命周期阻断
        if (_isPaused || _isGameOver || _isInventoryOpen)
        {
            Model.IsVisible = false;
            return;
        }

        // 阶段决定
        switch (Model.Phase)
        {
            case CrosshairPhase.Aim:
                Model.IsVisible = Model.Config.showInAim;
                break;
            case CrosshairPhase.HipFire:
                Model.IsVisible = Model.Config.showInHipFire;
                break;
            case CrosshairPhase.Reload:
                Model.IsVisible = Model.Config.showInReload;
                break;
            default:
                Model.IsVisible = false;
                break;
        }
    }
}
