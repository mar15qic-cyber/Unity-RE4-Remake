using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏内界面中枢。
/// 各面板只负责自身动画和交互；暂停、光标、输入阻断统一由本类协调。
/// </summary>
/// <remarks>本局模态界面栈与输入路由宿主，使用 GameplayPermissionService 汇总占用；关闭面板只释放其对应占用，不能覆盖其他面板的阻断。</remarks>
public class GameUIManager : Singleton<GameUIManager>, IManualSingleton, ICrosshairLifecycleProvider
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>界面交互与显示使用的 State 分类枚举；保持原成员顺序和数值，调用方据此选择业务分支。</summary>
    public enum State
    {
        /// <summary>中文含义：玩法。</summary>
        Gameplay,
        /// <summary>中文含义：已暂停。</summary>
        Paused,
        /// <summary>中文含义：选项集合。</summary>
        Options,
        /// <summary>中文含义：对局结束。</summary>
        GameOver,
        /// <summary>中文含义：任务已清除。</summary>
        MissionCleared,
        /// <summary>中文含义：波次已清除。</summary>
        WaveCleared,
        /// <summary>中文含义：背包。</summary>
        Inventory,
        /// <summary>中文含义：仓库。</summary>
        Storage,
        /// <summary>中文含义：物品检视。</summary>
        ItemInspection,
        /// <summary>中文含义：自定义。</summary>
        Custom
    }

    /// <summary>界面交互与显示模块 ModalEntry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class ModalEntry
    {
        /// <summary>中文含义：拥有者。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、GameUIManager.RemoveDestroyedOwners()、GameUIManager.UnregisterModal(Object)。</summary>
        public UnityEngine.Object owner;
        /// <summary>中文含义：当前状态。使用位置：GameUIManager.ApplyModalState()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</summary>
        public State state;
        /// <summary>中文含义：是否暂停项游戏。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</summary>
        public bool pausesGame;
        /// <summary>中文含义：是否显示时光标。使用位置：GameUIManager.ApplyModalState()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</summary>
        public bool showsCursor;
        /// <summary>中文含义：是否阻断项玩家输入。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</summary>
        public bool blocksPlayerInput;
        /// <summary>中文含义：许可句柄。使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("许可句柄")]
        [UnityEngine.InspectorName("许可句柄")]
        [Newtonsoft.Json.JsonProperty("许可句柄")]
        public GameArchitecture.GameplayPermissionHandle permissionHandle;
    }

    /// <summary>中文含义：暂停菜单。使用位置：作为 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 的参数、作为 GameUIManager.UnregisterModal(Object) 的参数、GameUIManager.AutoWireMissingReferences() 等共 10 处。</summary>
    [Header("子面板引用")]
    public PauseMenu pauseMenu;
    /// <summary>中文含义：对局结束屏幕。使用位置：作为 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 的参数、GameUIManager.AutoWireMissingReferences()、GameUIManager.OnPlayerDied() 等共 4 处。</summary>
    public GameOverScreen gameOverScreen;
    /// <summary>中文含义：任务已清除屏幕。使用位置：作为 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 的参数、GameUIManager.AutoWireMissingReferences()、GameUIManager.OnMissionCompleted(MissionResult) 等共 4 处。</summary>
    public MissionClearedScreen missionClearedScreen;
    /// <summary>中文含义：任务进度战斗显示界面。使用位置：GameUIManager.AutoWireMissingReferences()、SceneStartupEntry.ConfigurationError()、SceneStartupEntry.Start()。</summary>
    public MissionProgressHUD missionProgressHUD;
    /// <summary>中文含义：波次已清除界面。使用位置：GameUIManager.AutoWireMissingReferences()、SceneStartupEntry.ConfigurationError()。</summary>
    public WaveClearedUI waveClearedUI;
    /// <summary>中文含义：选项集合菜单。使用位置：作为 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 的参数、作为 GameUIManager.UnregisterModal(Object) 的参数、GameUIManager.AutoWireMissingReferences() 等共 8 处。</summary>
    public OptionsMenu optionsMenu;
    /// <summary>中文含义：波次结算显示时是否暂停游戏。</summary>
    [Header("界面策略")]
    [Tooltip("波次结算显示时是否暂停游戏")]
    public bool pauseOnWaveCleared;
    /// <summary>中文含义：对局结束时是否暂停游戏。死亡画面显示时是否暂停游戏。使用位置：作为 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool) 的参数。</summary>
    [Tooltip("死亡画面显示时是否暂停游戏")]
    public bool pauseOnGameOver = true;
    /// <summary>中文含义：是否状态变化时输出调试信息。状态变化时输出调试信息。使用位置：GameUIManager.ApplyModalState()。</summary>
    [Tooltip("状态变化时输出调试信息")]
    public bool logStateChanges;
    /// <summary>中文含义：绑定本局玩家的输入组件。使用位置：GameUIManager.AutoWireMissingReferences()、SceneStartupEntry.ConfigurationError()。</summary>
    [Header("玩家输入")]
    [Tooltip("绑定本局玩家的输入组件")]
    public LeonInputBridge playerInputBridge;
    /// <summary>中文含义：模态界面打开时自动隐藏。战斗信息画布；模态界面打开时自动隐藏。使用位置：GameUIManager.ApplyGameplayHudVisibility()。</summary>
    [Header("战斗界面可见性")]
    [Tooltip("战斗信息画布；模态界面打开时自动隐藏")]
    public CanvasGroup gameplayHudRoot;
    /// <summary>中文含义：模态界面打开时需要隐藏的额外对象，例如准心根对象。使用位置：GameUIManager.ApplyGameplayHudVisibility()。</summary>
    [Tooltip("模态界面打开时需要隐藏的额外对象，例如准心根对象")]
    public GameObject[] gameplayOnlyObjects;
    /// <summary>中文含义：模态界面打开时是否隐藏战斗信息。使用位置：GameUIManager.ApplyGameplayHudVisibility()。</summary>
    [Tooltip("模态界面打开时是否隐藏战斗信息")]
    public bool hideGameplayHudWhenBlocked = true;
    /// <summary>中文含义：当前状态。使用位置：作为 Action&lt;GameUIManager.State&gt;.Invoke(GameUIManager.State) 的参数、作为 Debug.Log(object) 的参数、CombatHUD.IsCombatVisualBlocked() 等共 13 处。</summary>
    public State currentState { get; private set; } = State.Gameplay;
    /// <summary>中文含义：是否游戏已暂停。使用位置：GameUIManager.ApplyModalState()、GameUIManager.IsPaused.get、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。</summary>
    public bool IsGamePaused => currentSessionPermission != null && currentSessionPermission.Current.PauseAudio;
    /// <summary>中文含义：是否玩家输入阻挡。使用位置：作为 Action&lt;bool&gt;.Invoke(bool) 的参数、GameUIManager.ApplyGameplayHudVisibility()、GameUIManager.ApplyModalState() 等共 5 处。</summary>
    public bool IsPlayerInputBlocked => currentSessionPermission != null && !currentSessionPermission.Current.AllowInput;
    /// <summary>中文含义：是否光标请求的。使用位置：GameUIManager.ApplyModalState()、LeonInputBridge.Update()。</summary>
    public bool IsCursorRequested { get; private set; }

    /// <summary>中文含义：状态变化时的通知回调。事件通信成员 OnStateChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：GameUIManager.ApplyModalState()。</summary>
    public event Action<State> OnStateChanged;
    /// <summary>中文含义：玩家输入阻断变化时的通知回调。事件通信成员 OnPlayerInputBlockChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：GameUIManager.ApplyModalState()。</summary>
    public event Action<bool> OnPlayerInputBlockChanged;
    /// <summary>中文含义：生命周期变化时的通知回调。事件通信成员 OnLifecycleChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：CrosshairLifecycleAdapter.OnDestroy()、CrosshairLifecycleAdapter.Start()、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。</summary>
    public event Action<CrosshairLifecycleEvent> OnLifecycleChanged;
    /// <summary>中文含义：是否已暂停。使用位置：CrosshairLifecycleAdapter.IsPaused.get、PerformanceSamplingEntry.Tick()、WeaponWheelUI.CanOpen()。</summary>
    public bool IsPaused => IsGamePaused;
    /// <summary>中文含义：是否已进入对局结束状态。使用位置：CrosshairLifecycleAdapter.IsGameOver.get、GameUIManager.ApplyModalState()、GameUIManager.NotifyLifecycleChanges(bool, bool, bool) 等共 4 处。</summary>
    public bool IsGameOver => currentState == State.GameOver;
    /// <summary>中文含义：是否任务已清除。使用位置：WeaponWheelUI.CanOpen()。</summary>
    public bool IsMissionCleared => currentState == State.MissionCleared;
    /// <summary>中文含义：背包界面是否打开。使用位置：CrosshairLifecycleAdapter.IsInventoryOpen.get、GameUIManager.ApplyModalState()、GameUIManager.NotifyLifecycleChanges(bool, bool, bool) 等共 4 处。</summary>
    public bool IsInventoryOpen => currentState == State.Inventory || currentState == State.Storage;

    /// <summary>中文含义：模态堆叠。只读字段，初始化后不由普通写入口替换。使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数、作为 List&lt;GameUIManager.ModalEntry&gt;.this[int] 的参数、GameUIManager.ApplyModalState() 等共 8 处。</summary>
    private readonly List<ModalEntry> modalStack = new List<ModalEntry>();
    /// <summary>中文含义：是否退出键防抖。使用位置：GameUIManager.ClearEscDebounce()、GameUIManager.Update()。</summary>
    private bool escDebounce;
    /// <summary>中文含义：模态输入帧。使用位置：GameUIManager.TryClaimModalInput()。</summary>
    private int modalInputFrame = -1;
    /// <summary>中文含义：当前会话许可。使用位置：GameUIManager.BindingCurrentSession(GameManager)、GameUIManager.ClearAllModals()、GameUIManager.IsGamePaused.get 等共 8 处。</summary>
    private GameplayPermissionService currentSessionPermission;
    /// <summary>中文含义：绑定对局。使用位置：作为 GameUIManager.OnMissionCompleted(MissionResult) 的参数、GameUIManager.BindingCurrentSession(GameManager)、GameUIManager.BreakOpenPresentation() 等共 5 处。</summary>
    private GameManager bindingMatch;
    /// <summary>中文含义：是否具有连接表现。使用位置：GameUIManager.BreakOpenPresentation()、GameUIManager.ConnectPresentation()。</summary>
    private bool hasConnectPresentation;
    /// <summary>中文含义：lastPausePresentation 表示“是否上一次暂停表现”；lastInputBlockPresentation 表示“是否上一次输入阻断表现”。使用位置：GameUIManager.ApplyModalState()。；使用位置：GameUIManager.ApplyModalState()。</summary>
    private bool lastPausePresentation, lastInputBlockPresentation;
    /// <summary>AutoWireMissingReferences 的业务处理入口；本方法直接依赖 SceneStartupEntry.QueryHasHasReference&lt;GameOverScreen&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;LeonInputBridge&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;MissionClearedScreen&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;MissionProgressHUD&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;OptionsMenu&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;PauseMenu&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;WaveClearedUI&gt;(Component)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） SceneStartupEntry.QueryHasHasReference&lt;GameOverScreen&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;LeonInputBridge&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;MissionClearedScreen&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;MissionProgressHUD&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;OptionsMenu&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;PauseMenu&gt;(Component)、SceneStartupEntry.QueryHasHasReference&lt;WaveClearedUI&gt;(Component)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [Obsolete("请在场景中显式绑定界面引用")]
    public void AutoWireMissingReferences()
    {
        if (pauseMenu == null)
        {
            pauseMenu = SceneStartupEntry.QueryHasHasReference<PauseMenu>(this);
        }

        if (optionsMenu == null)
        {
            optionsMenu = SceneStartupEntry.QueryHasHasReference<OptionsMenu>(this);
        }

        if (gameOverScreen == null)
        {
            gameOverScreen = SceneStartupEntry.QueryHasHasReference<GameOverScreen>(this);
        }

        if (missionClearedScreen == null)
        {
            missionClearedScreen = SceneStartupEntry.QueryHasHasReference<MissionClearedScreen>(this);
        }

        if (missionProgressHUD == null)
        {
            missionProgressHUD = SceneStartupEntry.QueryHasHasReference<MissionProgressHUD>(this);
        }

        if (waveClearedUI == null)
        {
            waveClearedUI = SceneStartupEntry.QueryHasHasReference<WaveClearedUI>(this);
        }

        if (playerInputBridge == null)
        {
            playerInputBridge = SceneStartupEntry.QueryHasHasReference<LeonInputBridge>(this);
        }
    }

    /// <summary>BindingCurrentSession 的业务处理入口；本方法直接依赖 GameUIManager.BreakOpenPresentation()、GameUIManager.ConnectPresentation()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.Start()；下游（含内部回调体） GameUIManager.BreakOpenPresentation()、GameUIManager.ConnectPresentation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gameManager">中文含义：游戏管理器。使用位置：GameUIManager.BindingCurrentSession(GameManager)。</param>
    internal void BindingCurrentSession(GameManager gameManager)
    {
        BreakOpenPresentation();
        bindingMatch = gameManager;
        if (gameManager != null)
        {
            currentSessionPermission = gameManager.Permission;
        }
        else
        {
            currentSessionPermission = null;
        }

        if (isActiveAndEnabled)
        {
            ConnectPresentation();
        }
    }

    /// <summary>ConnectPresentation 的业务处理入口；本方法直接依赖 GameUIManager.ApplyModalState()、GameUIManager.OnMissionCompleted(MissionResult)、GameUIManager.OnPlayerDied()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.BindingCurrentSession(GameManager)、GameUIManager.OnEnable()；下游（含内部回调体） GameUIManager.ApplyModalState()、GameUIManager.OnMissionCompleted(MissionResult)、GameUIManager.OnPlayerDied()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConnectPresentation()
    {
        if (bindingMatch == null || hasConnectPresentation)
        {
            return;
        }

        hasConnectPresentation = true;
        bindingMatch.OnPlayerDied += OnPlayerDied;
        bindingMatch.OnMissionCompleted += OnMissionCompleted;
        if (pauseMenu != null)
        {
            pauseMenu.onResume = OnPauseResume;
            pauseMenu.onOpenOptions = OpenOptionsFromPause;
        }

        if (optionsMenu != null)
        {
            optionsMenu.onClosed = OnOptionsClosed;
        }

        if (bindingMatch.IsGameOver)
        {
            OnPlayerDied();
        }
        else if (bindingMatch.IsMissionCompleted)
        {
            OnMissionCompleted(bindingMatch.Flow.LevelCompleteResult);
        }
        else
        {
            ApplyModalState();
        }
    }

    /// <summary>BreakOpenPresentation 的业务处理入口；本方法直接依赖 GameUIManager.RestoreGameplayState()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.BindingCurrentSession(GameManager)、GameUIManager.OnDestroy()、GameUIManager.OnDisable()；下游（含内部回调体） GameUIManager.RestoreGameplayState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BreakOpenPresentation()
    {
        if (hasConnectPresentation && bindingMatch != null)
        {
            bindingMatch.OnPlayerDied -= OnPlayerDied;
            bindingMatch.OnMissionCompleted -= OnMissionCompleted;
        }

        hasConnectPresentation = false;
        RestoreGameplayState();
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.BreakOpenPresentation()、Singleton&lt;GameUIManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        BreakOpenPresentation();
        base.OnDestroy();
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.BreakOpenPresentation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable() => BreakOpenPresentation();
    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.ConnectPresentation()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable() => ConnectPresentation();
    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.ClearEscDebounce()、GameUIManager.HandleEscPress()、GameUIManager.RemoveDestroyedOwners()、Input.GetKeyDown(KeyCode)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (bindingMatch == null)
        {
            return;
        }

        RemoveDestroyedOwners();
        if (currentState == State.GameOver || currentState == State.MissionCleared)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && !escDebounce)
        {
            escDebounce = true;
            StartCoroutine(ClearEscDebounce());
            HandleEscPress();
        }
    }

    /// <summary>ClearEscDebounce 的业务处理入口；本方法直接依赖 WaitForSecondsRealtime.WaitForSecondsRealtime(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.Update()；下游（含内部回调体） WaitForSecondsRealtime.WaitForSecondsRealtime(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator ClearEscDebounce()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        escDebounce = false;
    }

    /// <summary>HandleEscPress 的业务处理入口；本方法直接依赖 GameUIManager.HidePause()、GameUIManager.ShowPause()、InventorySceneLoader.Hide()、ItemInspectionUI.RequestClose()、object.ReferenceEquals(object, object)、OptionsMenu.Hide()、PauseMenu.HandleBackRequest()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.Update()；下游（含内部回调体） GameUIManager.HidePause()、GameUIManager.ShowPause()、InventorySceneLoader.Hide()、ItemInspectionUI.RequestClose()、object.ReferenceEquals(object, object)、OptionsMenu.Hide()、PauseMenu.HandleBackRequest()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleEscPress()
    {
        switch (currentState)
        {
            case State.Gameplay:
                ShowPause();
                break;
            case State.Paused:
                if (pauseMenu != null && pauseMenu.HandleBackRequest())
                {
                    break;
                }

                HidePause();
                break;
            case State.Options:
                // receiverValue2（判空前缓存的选项集合菜单）：从选项集合菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.HandleEscPress()。
global::OptionsMenu receiverValue2 = optionsMenu;
                if (!global::System.Object.ReferenceEquals(receiverValue2, null))
                {
                    receiverValue2.Hide();
                }

                break;
            case State.Inventory:
                // receiverValue3（判空前缓存的背包场景加载器的实例）：从背包场景加载器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.HandleEscPress()。
global::InventorySceneLoader receiverValue3 = InventorySceneLoader.Instance;
                if (!global::System.Object.ReferenceEquals(receiverValue3, null))
                {
                    receiverValue3.Hide();
                }

                break;
            case State.ItemInspection:
                if (ItemInspectionUI.HasInstance)
                {
                    ItemInspectionUI.Instance.RequestClose();
                }

                break;
        }
    }

    /// <summary>
    /// 登记一个模态界面。重复登记同一拥有者时会更新原记录，不会重复入栈。
    /// 新增界面可直接使用此方法接入统一暂停、光标和输入阻断。
    /// </summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Show()、GameUIManager.OnMissionCompleted(MissionResult)、GameUIManager.OnPlayerDied()、GameUIManager.OpenOptionsFromPause()、GameUIManager.ShowInventory() 等共 8 处；下游（含内部回调体） Debug.LogWarning(object)、GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float)、GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float)、GameUIManager.ApplyModalState()、GameUIManager.ModalEntry.ModalEntry()、List&lt;GameUIManager.ModalEntry&gt;.Add(GameUIManager.ModalEntry)、List&lt;GameUIManager.ModalEntry&gt;.RemoveAt(int) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="owner">中文含义：拥有者。使用位置：作为 ObjectIdentity.ObjectIdentity(int, long) 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</param>
    /// <param name="state">中文含义：当前状态。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</param>
    /// <param name="pausesGame">中文含义：是否暂停项游戏。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</param>
    /// <param name="showsCursor">中文含义：是否显示时光标。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</param>
    /// <param name="blocksPlayerInput">中文含义：是否阻断项玩家输入。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。</param>
    public void RegisterModal(UnityEngine.Object owner, State state, bool pausesGame = true, bool showsCursor = true, bool blocksPlayerInput = true)
    {
        if (owner == null)
        {
            Debug.LogWarning("[界面中枢] 登记失败：界面拥有者为空。");
            return;
        }

        // originalHandle（原始句柄）：使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
GameArchitecture.GameplayPermissionHandle originalHandle = default;
        // i（当前遍历索引）：使用位置：作为 List<GameUIManager.ModalEntry>.RemoveAt(int) 的参数、作为 List<GameUIManager.ModalEntry>.this[int] 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
for (int i = modalStack.Count - 1; i >= 0; i--)
        {
            if (modalStack[i].owner != owner)
            {
                continue;
            }

            originalHandle = modalStack[i].permissionHandle;
            modalStack.RemoveAt(i);
            break;
        }

        if (currentSessionPermission == null)
        {
            return;
        }

        // hasUpdate（是否具有更新）：使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
bool hasUpdate;
        if (currentSessionPermission != null)
        {
            // callReceiver9（调用前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
global::GameplayPermissionService callReceiver9 = currentSessionPermission;
            // argumentValue5（按原顺序缓存的实参原始句柄）：从原始句柄读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float) 的参数。
global::GameArchitecture.GameplayPermissionHandle argumentValue5 = originalHandle;
            // argumentValue6（按原顺序缓存的实参是否阻断项玩家输入）：从阻断项玩家输入读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float) 的参数。
bool argumentValue6 = blocksPlayerInput;
            // argumentValue7（按原顺序缓存的实参分支判断）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float) 的参数。
bool argumentValue7 = state != State.Gameplay;
            // argumentValue8（按原顺序缓存的实参是否暂停项游戏）：从暂停项游戏读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float) 的参数。
bool argumentValue8 = pausesGame;
            // selectedValue4（分支选出的0）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameplayPermissionService.Update(GameplayPermissionHandle, bool, bool, bool, float) 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
int selectedValue4;
            if (pausesGame)
            {
                selectedValue4 = 0;
            }
            else
            {
                selectedValue4 = 1;
            }

            hasUpdate = callReceiver9.Update(argumentValue5, argumentValue6, argumentValue7, argumentValue8, selectedValue4);
        }
        else
        {
            hasUpdate = false;
        }

        // callReceiver20（调用前缓存的模态堆叠）：从模态堆叠读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
global::System.Collections.Generic.List<global::GameUIManager.ModalEntry> callReceiver20 = modalStack;
        // createdValue11（本次新建的模态入口）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：作为 List<GameUIManager.ModalEntry>.Add(GameUIManager.ModalEntry) 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
global::GameUIManager.ModalEntry createdValue11 = new ModalEntry();
        createdValue11.owner = owner;
        createdValue11.state = state;
        createdValue11.pausesGame = pausesGame;
        createdValue11.showsCursor = showsCursor;
        createdValue11.blocksPlayerInput = blocksPlayerInput;
        if (hasUpdate)
        {
            createdValue11.permissionHandle = originalHandle;
        }
        else
        {
            if (currentSessionPermission != null)
            {
                // callReceiver17（调用前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
global::GameplayPermissionService callReceiver17 = currentSessionPermission;
                // argumentValue13（按原顺序缓存的实参对象身份标识的返回结果）：从对象身份标识的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float) 的参数。
global::GameArchitecture.ObjectIdentity argumentValue13 = new GameArchitecture.ObjectIdentity(owner.GetInstanceID(), 1);
                // argumentValue14（按原顺序缓存的实参是否阻断项玩家输入）：从阻断项玩家输入读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float) 的参数。
bool argumentValue14 = blocksPlayerInput;
                // argumentValue15（按原顺序缓存的实参分支判断）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float) 的参数。
bool argumentValue15 = state != State.Gameplay;
                // argumentValue16（按原顺序缓存的实参是否暂停项游戏）：从暂停项游戏读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float) 的参数。
bool argumentValue16 = pausesGame;
                // selectedValue12（分支选出的0）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float) 的参数、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。
int selectedValue12;
                if (pausesGame)
                {
                    selectedValue12 = 0;
                }
                else
                {
                    selectedValue12 = 1;
                }

                createdValue11.permissionHandle = callReceiver17.Request(argumentValue13, argumentValue14, argumentValue15, argumentValue16, selectedValue12);
            }
            else
            {
                createdValue11.permissionHandle = default;
            }
        }

        callReceiver20.Add(createdValue11);
        ApplyModalState();
    }

    /// <summary>注销由指定对象登记的模态界面。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.Hide()、GameUIManager.HideInventory()、GameUIManager.HidePause()、GameUIManager.OnOptionsClosed()、GameUIManager.OnPauseResume() 等共 10 处；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、GameUIManager.ApplyModalState()、List&lt;GameUIManager.ModalEntry&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="owner">中文含义：拥有者。使用位置：GameUIManager.UnregisterModal(Object)。</param>
    public void UnregisterModal(UnityEngine.Object owner)
    {
        if (owner == null)
        {
            return;
        }

        // changed（是否发生变化）：使用位置：GameUIManager.UnregisterModal(Object)。
bool changed = false;
        // i（当前遍历索引）：使用位置：作为 List<GameUIManager.ModalEntry>.RemoveAt(int) 的参数、作为 List<GameUIManager.ModalEntry>.this[int] 的参数、GameUIManager.UnregisterModal(Object)。
for (int i = modalStack.Count - 1; i >= 0; i--)
        {
            if (modalStack[i].owner != owner)
            {
                continue;
            }

            // receiverValue21（判空前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.UnregisterModal(Object)。
global::GameplayPermissionService receiverValue21 = currentSessionPermission;
            // optionalValue22（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：GameUIManager.UnregisterModal(Object)。
bool? optionalValue22;
            if (!global::System.Object.ReferenceEquals(receiverValue21, null))
            {
                optionalValue22 = receiverValue21.Release(modalStack[i].permissionHandle);
            }
            else
            {
                optionalValue22 = default;
            }

            modalStack.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            ApplyModalState();
        }
    }

    internal bool IsMissionResultShowing => missionClearedScreen != null && missionClearedScreen.IsShowing;

    /// <summary>清空全部模态状态，常用于重新加载或切换场景前。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.ExecuteOption(int)、GameUIManager.OnMissionCompleted(MissionResult)、GameUIManager.OnPlayerDied()、MissionClearedScreen.ExecuteOption(int)、PauseMenu.QuitToTitleNow()；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、GameUIManager.ApplyModalState()、List&lt;GameUIManager.ModalEntry&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ClearAllModals()
    {
        // item（当前模态入口）：每轮从模态堆叠取得一个元素，仅代表当前这一项。使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数。
foreach (var item in modalStack)
        {
            // receiverValue23（判空前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.ClearAllModals()。
global::GameplayPermissionService receiverValue23 = currentSessionPermission;
            // optionalValue24（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：GameUIManager.ClearAllModals()。
bool? optionalValue24;
            if (!global::System.Object.ReferenceEquals(receiverValue23, null))
            {
                optionalValue24 = receiverValue23.Release(item.permissionHandle);
            }
            else
            {
                optionalValue24 = default;
            }
        }

        modalStack.Clear();
        ApplyModalState();
    }

    /// <summary>ShowPause 的业务处理入口；本方法直接依赖 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、PauseMenu.Show()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()、PerformanceSamplingEntry.Drive(int, double)、PerformanceSamplingEntry.Tick()；下游（含内部回调体） GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、PauseMenu.Show()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowPause()
    {
        if (VillageBellSequence.IsPresenting || currentState == State.GameOver || currentState == State.MissionCleared || pauseMenu == null)
        {
            return;
        }

        RegisterModal(pauseMenu, State.Paused);
        pauseMenu.Show();
    }

    /// <summary>HidePause 的业务处理入口；本方法直接依赖 GameUIManager.UnregisterModal(Object)、PauseMenu.Hide()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()、PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） GameUIManager.UnregisterModal(Object)、PauseMenu.Hide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HidePause()
    {
        if (pauseMenu == null)
        {
            return;
        }

        pauseMenu.Hide();
        UnregisterModal(pauseMenu);
    }

    /// <summary>OnPauseResume 的业务处理入口；本方法直接依赖 GameUIManager.UnregisterModal(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.UnregisterModal(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnPauseResume()
    {
        UnregisterModal(pauseMenu);
    }

    /// <summary>OnPlayerDied 的业务处理入口；本方法直接依赖 GameOverScreen.Show()、GameUIManager.ClearAllModals()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ConnectPresentation()；下游（含内部回调体） GameOverScreen.Show()、GameUIManager.ClearAllModals()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnPlayerDied()
    {
        ClearAllModals();
        if (gameOverScreen == null)
        {
            return;
        }

        RegisterModal(gameOverScreen, State.GameOver, pauseOnGameOver, true, true);
        gameOverScreen.Show();
    }

    /// <summary>OnMissionCompleted 的业务处理入口；本方法直接依赖 GameUIManager.ClearAllModals()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、MissionClearedScreen.Show(MissionResult)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ConnectPresentation()；下游（含内部回调体） GameUIManager.ClearAllModals()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、MissionClearedScreen.Show(MissionResult)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">中文含义：当前任务结果。使用位置：作为 MissionClearedScreen.Show(MissionResult) 的参数。</param>
    void OnMissionCompleted(MissionResult result)
    {
        if (currentState == State.GameOver || missionClearedScreen == null)
        {
            return;
        }

        ClearAllModals();
        RegisterModal(missionClearedScreen, State.MissionCleared, true, true, true);
        var mission = ChapterOneMissionManager.Instance;
        missionClearedScreen.Show(result, mission != null && mission.IsBellEnding);
    }

    /// <summary>OpenOptionsFromPause 的业务处理入口；本方法直接依赖 Debug.LogError(object)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、object.ReferenceEquals(object, object)、OptionsMenu.Show()、PauseMenu.SetInteractionEnabled(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PauseMenu.ExecuteSelected()；下游（含内部回调体） Debug.LogError(object)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、object.ReferenceEquals(object, object)、OptionsMenu.Show()、PauseMenu.SetInteractionEnabled(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OpenOptionsFromPause()
    {
        if (optionsMenu == null)
        {
            Debug.LogError("[界面中枢] 无法打开设置页：场景中没有可用的设置菜单。");
            // receiverValue25（判空前缓存的暂停菜单）：从暂停菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.OpenOptionsFromPause()。
global::PauseMenu receiverValue25 = pauseMenu;
            if (!global::System.Object.ReferenceEquals(receiverValue25, null))
            {
                receiverValue25.SetInteractionEnabled(true);
            }

            return;
        }

        optionsMenu.onClosed = OnOptionsClosed;
        // receiverValue26（判空前缓存的暂停菜单）：从暂停菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.OpenOptionsFromPause()。
global::PauseMenu receiverValue26 = pauseMenu;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            receiverValue26.SetInteractionEnabled(false);
        }

        RegisterModal(optionsMenu, State.Options);
        optionsMenu.Show();
    }

    /// <summary>HideOptions 的业务处理入口；本方法直接依赖 object.ReferenceEquals(object, object)、OptionsMenu.Hide()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） object.ReferenceEquals(object, object)、OptionsMenu.Hide()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideOptions()
    {
        // receiverValue27（判空前缓存的选项集合菜单）：从选项集合菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.HideOptions()。
global::OptionsMenu receiverValue27 = optionsMenu;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            receiverValue27.Hide();
        }
    }

    /// <summary>OnOptionsClosed 的业务处理入口；本方法直接依赖 GameUIManager.UnregisterModal(Object)、object.ReferenceEquals(object, object)、PauseMenu.SetInteractionEnabled(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.UnregisterModal(Object)、object.ReferenceEquals(object, object)、PauseMenu.SetInteractionEnabled(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnOptionsClosed()
    {
        UnregisterModal(optionsMenu);
        // receiverValue28（判空前缓存的暂停菜单）：从暂停菜单读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.OnOptionsClosed()。
global::PauseMenu receiverValue28 = pauseMenu;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.SetInteractionEnabled(true);
        }
    }

    /// <summary>ShowInventory 的业务处理入口；本方法直接依赖 GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowInventory()
    {
        RegisterModal(this, State.Inventory);
    }

    /// <summary>HideInventory 的业务处理入口；本方法直接依赖 GameUIManager.UnregisterModal(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） GameUIManager.UnregisterModal(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideInventory()
    {
        UnregisterModal(this);
    }

    /// <summary>
    /// 同一帧只允许一个模态界面消费切换输入，避免存储箱与背包竞争同一个按键。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnInventoryToggle(InputAction.CallbackContext)、StorageBoxUI.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool TryClaimModalInput()
    {
        // frame（帧）：使用位置：GameUIManager.TryClaimModalInput()。
int frame = Time.frameCount;
        if (modalInputFrame == frame)
        {
            return false;
        }

        modalInputFrame = frame;
        return true;
    }

    /// <summary>ShowConfirmDialog 的业务处理入口；本方法直接依赖 Debug.Log(object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="message">中文含义：消息。使用位置：作为 Debug.Log(object) 的参数。</param>
    /// <param name="onYes">中文含义：确认时的通知回调。</param>
    /// <param name="onNo">中文含义：无时的通知回调。</param>
    public void ShowConfirmDialog(string message, Action onYes, Action onNo)
    {
        Debug.Log("[界面中枢] 尚未配置通用确认弹窗：" + message);
    }

    /// <summary>CanPlayerAct 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 DoorController.IsPlayerBlocked()、WorldInteractionPromptUI.IsUIBlocking()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public bool CanPlayerAct()
    {
        return !IsPlayerInputBlocked;
    }

    /// <summary>RemoveDestroyedOwners 的业务处理入口；本方法直接依赖 GameplayPermissionService.Release(GameplayPermissionHandle)、GameUIManager.ApplyModalState()、List&lt;GameUIManager.ModalEntry&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.Update()；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、GameUIManager.ApplyModalState()、List&lt;GameUIManager.ModalEntry&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RemoveDestroyedOwners()
    {
        // changed（是否发生变化）：使用位置：GameUIManager.RemoveDestroyedOwners()。
bool changed = false;
        // i（当前遍历索引）：使用位置：作为 List<GameUIManager.ModalEntry>.RemoveAt(int) 的参数、作为 List<GameUIManager.ModalEntry>.this[int] 的参数、GameUIManager.RemoveDestroyedOwners()。
for (int i = modalStack.Count - 1; i >= 0; i--)
        {
            if (modalStack[i].owner != null)
            {
                continue;
            }

            // receiverValue29（判空前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.RemoveDestroyedOwners()。
global::GameplayPermissionService receiverValue29 = currentSessionPermission;
            // optionalValue30（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：GameUIManager.RemoveDestroyedOwners()。
bool? optionalValue30;
            if (!global::System.Object.ReferenceEquals(receiverValue29, null))
            {
                optionalValue30 = receiverValue29.Release(modalStack[i].permissionHandle);
            }
            else
            {
                optionalValue30 = default;
            }

            modalStack.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            ApplyModalState();
        }
    }

    /// <summary>ApplyModalState 的业务处理入口；本方法直接依赖 Action&lt;bool&gt;.Invoke(bool)、Action&lt;GameUIManager.State&gt;.Invoke(GameUIManager.State)、Debug.Log(object)、GameUIManager.ApplyGameplayHudVisibility()、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)、object.ReferenceEquals(object, object)、UICursorManager.HideCustomCursor() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ClearAllModals()、GameUIManager.ConnectPresentation()、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、GameUIManager.RemoveDestroyedOwners()、GameUIManager.UnregisterModal(Object)；下游（含内部回调体） Action&lt;bool&gt;.Invoke(bool)、Action&lt;GameUIManager.State&gt;.Invoke(GameUIManager.State)、Debug.Log(object)、GameUIManager.ApplyGameplayHudVisibility()、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)、object.ReferenceEquals(object, object)、UICursorManager.HideCustomCursor() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyModalState()
    {
        // previousState（前一次状态）：使用位置：GameUIManager.ApplyModalState()。
State previousState = currentState;
        // previousInputBlocked（前一次输入是否受阻）：使用位置：GameUIManager.ApplyModalState()。
bool previousInputBlocked = lastInputBlockPresentation;
        // wasPaused（之前是否已暂停）：使用位置：作为 GameUIManager.NotifyLifecycleChanges(bool, bool, bool) 的参数。
bool wasPaused = lastPausePresentation;
        // wasGameOver（之前是否对局结束）：使用位置：作为 GameUIManager.NotifyLifecycleChanges(bool, bool, bool) 的参数。
bool wasGameOver = IsGameOver;
        // wasInventoryOpen（之前是否背包打开）：使用位置：作为 GameUIManager.NotifyLifecycleChanges(bool, bool, bool) 的参数。
bool wasInventoryOpen = IsInventoryOpen;
        if (modalStack.Count > 0)
        {
            currentState = modalStack[modalStack.Count - 1].state;
        }
        else
        {
            currentState = State.Gameplay;
        }

        IsCursorRequested = false;
        // i（当前遍历索引）：使用位置：作为 List<GameUIManager.ModalEntry>.this[int] 的参数、GameUIManager.ApplyModalState()。
for (int i = 0; i < modalStack.Count; i++)
        {
            IsCursorRequested |= modalStack[i].showsCursor;
        }

        if (IsCursorRequested)
        {
            // receiverValue32（判空前缓存的界面光标管理器的实例）：从界面光标管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.ApplyModalState()。
global::UICursorManager receiverValue32 = UICursorManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue32, null))
            {
                receiverValue32.ShowCustomCursor();
            }
        }
        else
        {
            // receiverValue33（判空前缓存的界面光标管理器的实例）：从界面光标管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.ApplyModalState()。
global::UICursorManager receiverValue33 = UICursorManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue33, null))
            {
                receiverValue33.HideCustomCursor();
            }
        }

        if (previousState != currentState)
        {
            if (logStateChanges)
            {
                Debug.Log("[界面中枢] 当前状态：" + currentState);
            }

            // receiverValue34（判空前缓存的状态变化时的通知回调）：从状态变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.ApplyModalState()。
global::System.Action<global::GameUIManager.State> receiverValue34 = OnStateChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue34, null))
            {
                receiverValue34.Invoke(currentState);
            }
        }

        if (previousInputBlocked != IsPlayerInputBlocked)
        {
            // receiverValue35（判空前缓存的玩家输入阻断变化时的通知回调）：从玩家输入阻断变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.ApplyModalState()。
global::System.Action<bool> receiverValue35 = OnPlayerInputBlockChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue35, null))
            {
                receiverValue35.Invoke(IsPlayerInputBlocked);
            }
        }

        ApplyGameplayHudVisibility();
        NotifyLifecycleChanges(wasPaused, wasGameOver, wasInventoryOpen);
        lastPausePresentation = IsGamePaused;
        lastInputBlockPresentation = IsPlayerInputBlocked;
    }

    /// <summary>ApplyGameplayHudVisibility 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ApplyModalState()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyGameplayHudVisibility()
    {
        if (!hideGameplayHudWhenBlocked)
        {
            return;
        }

        // visible（是否可见）：使用位置：作为 GameObject.SetActive(bool) 的参数、GameUIManager.ApplyGameplayHudVisibility()。
bool visible = !IsPlayerInputBlocked;
        if (gameplayHudRoot != null)
        {
            // assignmentReceiver36（赋值前保存的目标模态界面打开时自动隐藏）：从玩法战斗显示界面根节点读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：GameUIManager.ApplyGameplayHudVisibility()。
global::UnityEngine.CanvasGroup assignmentReceiver36 = gameplayHudRoot;
            if (visible)
            {
                assignmentReceiver36.alpha = 1f;
            }
            else
            {
                assignmentReceiver36.alpha = 0f;
            }

            gameplayHudRoot.interactable = false;
            gameplayHudRoot.blocksRaycasts = false;
        }

        if (gameplayOnlyObjects == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：GameUIManager.ApplyGameplayHudVisibility()。
for (int i = 0; i < gameplayOnlyObjects.Length; i++)
        {
            if (gameplayOnlyObjects[i] != null)
            {
                gameplayOnlyObjects[i].SetActive(visible);
            }
        }
    }

    /// <summary>NotifyLifecycleChanges 的业务处理入口；本方法直接依赖 Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.ApplyModalState()；下游（含内部回调体） Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="wasPaused">中文含义：之前是否已暂停。使用位置：GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。</param>
    /// <param name="wasGameOver">中文含义：之前是否对局结束。使用位置：GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。</param>
    /// <param name="wasInventoryOpen">中文含义：之前是否背包打开。使用位置：GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。</param>
    void NotifyLifecycleChanges(bool wasPaused, bool wasGameOver, bool wasInventoryOpen)
    {
        if (wasPaused != IsGamePaused)
        {
            // receiverValue38（判空前缓存的生命周期变化时的通知回调）：从生命周期变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue38 = OnLifecycleChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue38, null))
            {
                // selectedValue39（分支选出的准星生命周期事件的暂停已进入）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Action<CrosshairLifecycleEvent>.Invoke(CrosshairLifecycleEvent) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::CrosshairLifecycleEvent selectedValue39;
                if (IsGamePaused)
                {
                    selectedValue39 = CrosshairLifecycleEvent.PauseEntered;
                }
                else
                {
                    selectedValue39 = CrosshairLifecycleEvent.PauseExited;
                }

                receiverValue38.Invoke(selectedValue39);
            }
        }

        if (wasGameOver != IsGameOver)
        {
            // receiverValue40（判空前缓存的生命周期变化时的通知回调）：从生命周期变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue40 = OnLifecycleChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue40, null))
            {
                // selectedValue41（分支选出的准星生命周期事件的死亡）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Action<CrosshairLifecycleEvent>.Invoke(CrosshairLifecycleEvent) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::CrosshairLifecycleEvent selectedValue41;
                if (IsGameOver)
                {
                    selectedValue41 = CrosshairLifecycleEvent.Death;
                }
                else
                {
                    selectedValue41 = CrosshairLifecycleEvent.Respawn;
                }

                receiverValue40.Invoke(selectedValue41);
            }
        }

        if (wasInventoryOpen != IsInventoryOpen)
        {
            // receiverValue42（判空前缓存的生命周期变化时的通知回调）：从生命周期变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue42 = OnLifecycleChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue42, null))
            {
                // selectedValue43（分支选出的准星生命周期事件的背包已打开）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Action<CrosshairLifecycleEvent>.Invoke(CrosshairLifecycleEvent) 的参数、GameUIManager.NotifyLifecycleChanges(bool, bool, bool)。
global::CrosshairLifecycleEvent selectedValue43;
                if (IsInventoryOpen)
                {
                    selectedValue43 = CrosshairLifecycleEvent.InventoryOpened;
                }
                else
                {
                    selectedValue43 = CrosshairLifecycleEvent.InventoryClosed;
                }

                receiverValue42.Invoke(selectedValue43);
            }
        }
    }

    /// <summary>RestoreGameplayState 的业务处理入口；本方法直接依赖 GameplayPermissionService.Release(GameplayPermissionHandle)、List&lt;GameUIManager.ModalEntry&gt;.Clear()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.BreakOpenPresentation()；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、List&lt;GameUIManager.ModalEntry&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RestoreGameplayState()
    {
        // item（当前模态入口）：每轮从模态堆叠取得一个元素，仅代表当前这一项。使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数。
foreach (var item in modalStack)
        {
            // receiverValue44（判空前缓存的当前会话许可）：从当前会话许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、GameUIManager.RestoreGameplayState()。
global::GameplayPermissionService receiverValue44 = currentSessionPermission;
            // optionalValue45（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：GameUIManager.RestoreGameplayState()。
bool? optionalValue45;
            if (!global::System.Object.ReferenceEquals(receiverValue44, null))
            {
                optionalValue45 = receiverValue44.Release(item.permissionHandle);
            }
            else
            {
                optionalValue45 = default;
            }
        }

        modalStack.Clear();
    }
}
