using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Input System -> LeonController
/// 鼠标右键 = 瞄准  鼠标左键 = 射击  F = 近战  R = 换弹  C = 蹲伏  Tab = 物品栏  E = 交互
/// </summary>
/// <remarks>输入动作资产的运行桥接，读取有效重绑定并按玩法、模态界面及配对上下文路由意图；通过控制器和已有动作宿主入口提交，不硬编码替代可改键动作。</remarks>
public partial class LeonInputBridge : MonoBehaviour
{
    /// <summary>正式引用，Inspector 绑定。运行时优先使用。</summary>
    [Header("输入资源")]
    [Tooltip("正式引用，Inspector 绑定。运行时优先使用。")]
    [SerializeField]
    private InputActionAsset inputActionAsset;
    /// <summary>输入资源。</summary>
    private InputActionAsset inputAsset;
    /// <summary>玩家映射。</summary>
    private InputActionMap playerMap;
    private LeonActionMotionDriver movementDriver;
    private LeonAnimationActionCoordinator actions;
    private LeonAnimatorDriver driver;
    private bool movementInputRefreshPending;
    /// <summary>配对动作映射。</summary>
    private InputActionMap qteMap;
    /// <summary>物品检视映射。</summary>
    private InputActionMap itemInspectionMap;
    /// <summary>里昂玩家控制器。</summary>
    private LeonController controller;
    /// <summary>战斗。</summary>
    private LeonCombat combat;
    /// <summary>当前里昂配对动作协调器。</summary>
    private LeonQTECoordinator qteCoordinator;
    /// <summary>武器轮盘。</summary>
    private WeaponWheelUI weaponWheel;
    /// <summary>移动教学输入阈值。</summary>
    [Header("教学事件")]
    [SerializeField]
    private float moveTutorialInputThreshold = 0.1f;
    /// <summary>是否滚动开放武器轮盘。</summary>
    [Header("快速武器轮盘")]
    [SerializeField]
    private bool scrollOpensWeaponWheel = true;
    /// <summary>滚动自动确认延迟。</summary>
    [SerializeField, Min(0.05f)]
    private float scrollAutoConfirmDelay = 0.45f;
    /// <summary>移动动作。</summary>
    private InputAction moveAction;
    /// <summary>观察动作。</summary>
    private InputAction lookAction;
    /// <summary>瞄准动作。</summary>
    private InputAction aimAction;
    /// <summary>射击动作。</summary>
    private InputAction shootAction;
    /// <summary>换弹动作。</summary>
    private InputAction reloadAction;
    /// <summary>招架动作。</summary>
    private InputAction parryAction;
    private readonly InputAction[] quickSlotActions = new InputAction[WeaponWheelUI.SlotCount];
    /// <summary>冲刺动作。</summary>
    private InputAction sprintAction;
    /// <summary>蹲伏动作。</summary>
    private InputAction crouchAction;
    /// <summary>自由环顾动作。</summary>
    private InputAction freeLookAction;
    /// <summary>背包动作。</summary>
    private InputAction inventoryAction;
    /// <summary>交互动作。</summary>
    private InputAction interactAction;
    /// <summary>排序动作。</summary>
    private InputAction sortAction;
    /// <summary>逃脱按下动作。</summary>
    private InputAction escapePressAction;
    private InputAction knifeEscapeAction;
    private bool knifeEscapeAwaitingRelease;
    /// <summary>物品检视关闭动作。</summary>
    private InputAction itemInspectionCloseAction;
    /// <summary>物品检视关闭回调。</summary>
    private System.Action itemInspectionCloseCallback;
    /// <summary>动作集合是否已绑定。</summary>
    private bool actionsBound;
    /// <summary>是否输入时钟已捕获。</summary>
    private bool inputClockCaptured;
    /// <summary>inputUpdateRealtime 表示“输入更新真实时间”；inputUpdateGameTime 表示“输入更新游戏时间”。</summary>
    private double inputUpdateRealtime, inputUpdateGameTime;
    /// <summary>输入更新时间缩放。</summary>
    private float inputUpdateTimeScale;
    /// <summary>是否等待场景开放。</summary>
    [Header("本局启动")]
    [UnityEngine.Serialization.FormerlySerializedAs("等待场景开放")]
    [UnityEngine.InspectorName("等待场景开放")]
    public bool waitingSceneOpenRelease;
    /// <summary>输入是否就绪。</summary>
    internal bool InputReady => started;

    /// <summary>是否已订阅打开输入的释放事件。</summary>
    private bool hasOpenRelease;
    /// <summary>SetSceneOpenRelease 的业务处理入口；本方法直接依赖 LeonInputBridge.SetGameplayInputEnabled(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.OnDisable()、SceneStartupEntry.Start()；下游（含内部回调体） LeonInputBridge.SetGameplayInputEnabled(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="openRelease">是否打开释放。</param>
    internal void SetSceneOpenRelease(bool openRelease)
    {
        hasOpenRelease = openRelease;
        SetGameplayInputEnabled(openRelease);
    }

    /// <summary>是否已开始。</summary>
    private bool started;
    /// <summary>玩家映射之前是否启用。</summary>
    private bool playerMapWasEnabled;
    /// <summary>生效配对动作会话编号。</summary>
    private int activeQteSessionId = -1;
    /// <summary>是否持有输入资源。</summary>
    private bool ownsInputAsset;
    /// <summary>死亡输入是否锁定。</summary>
    private bool deathInputLocked;
    /// <summary>输入许可。</summary>
    private GameplayPermissionService inputPermission;
    /// <summary>pairedActionInputPermission 表示“已配对动作输入许可”；deathInputPermission 表示“死亡输入许可”。</summary>
    private GameArchitecture.GameplayPermissionHandle pairedActionInputPermission, deathInputPermission;
    /// <summary>是否具有已报告移动输入。</summary>
    private bool hasReportedMoveInput;

    internal float ReadLadderVerticalInput()
    {
        if (moveAction == null || !moveAction.enabled)
        {
            return 0f;
        }

        if (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return 0f;
        }

        return moveAction.ReadValue<Vector2>().y;
    }
    /// <summary>武器轮盘是否打开。</summary>
    private bool weaponWheelOpen;
    /// <summary>是否允许自由视图输入。</summary>
    public bool AllowFreeViewInput => isActiveAndEnabled && !deathInputLocked && activeQteSessionId <= 0 && !weaponWheelOpen && (!waitingSceneOpenRelease || hasOpenRelease) && freeLookAction != null && freeLookAction.enabled;

    /// <summary>自由视图键实际是否保持按住。</summary>
    private bool FreeViewKeyActualHeld
    {
        get
        {
            if (freeLookAction == null)
            {
                return false;
            }

            // control（控制）：每轮从自由环顾动作的控件集合取得一个元素，仅代表当前这一项。使用位置：LeonInputBridge.FreeViewKeyActualHeld.get。
foreach (var control in freeLookAction.controls)
            {
                if (control is UnityEngine.InputSystem.Controls.ButtonControl /* 中文含义：键绑定。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ keyBinding && keyBinding.isPressed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>武器轮盘自动确认时间。</summary>
    private float weaponWheelAutoConfirmTime;
    /// <summary>下一次武器轮盘滚动打开时间。</summary>
    private float nextWeaponWheelScrollOpenTime;
    // 瞄准模式：0=长按，1=切换
    /// <summary>瞄准模式。</summary>
    private int aimMode;
    /// <summary>瞄准切换是否生效。</summary>
    private bool aimToggleActive;
    /// <summary>玩家已移动时的通知回调。事件通信成员 OnPlayerMoved，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。</summary>
    public event System.Action OnPlayerMoved;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;InputActionAsset&gt;(string)、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;WeaponWheelUI&gt;()、Debug.LogError(object)、GameObject.AddComponent&lt;WeaponWheelUI&gt;() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        controller = GetComponent<LeonController>();
        movementDriver = GetComponent<LeonActionMotionDriver>();
        actions = GetComponent<LeonAnimationActionCoordinator>();
        driver = GetComponent<LeonAnimatorDriver>();
        combat = GetComponent<LeonCombat>();
        qteCoordinator = GetComponent<LeonQTECoordinator>();
        weaponWheel = GetComponent<WeaponWheelUI>();
        if (weaponWheel == null)
        {
            weaponWheel = gameObject.AddComponent<WeaponWheelUI>();
        }

        // 运行时使用独立副本，避免与界面输入模块共享启用状态。
        // 来源资源。
InputActionAsset sourceAsset = inputActionAsset;
        if (sourceAsset == null)
        {
            // Resources 回退
            sourceAsset = Resources.Load<InputActionAsset>("Input/LeonInputActions");
        }

        if (sourceAsset == null)
        {
#if UNITY_EDITOR
            // 编辑器资源查找兜底，仅用于编辑器环境。
            sourceAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/Input/LeonInputActions.inputactions");
#endif
        }

        if (sourceAsset == null)
        {
            Debug.LogError("[LeonInputBridge] 找不到 InputActions，请在 Inspector 绑定 inputActionAsset");
            return;
        }

        inputAsset = Instantiate(sourceAsset);
        ownsInputAsset = true;
        // 应用标题选项中保存的自定义按键
        InputBindingPersistence.Load(inputAsset);
        // 读取瞄准模式
        aimMode = PlayerPrefs.GetInt("AimMode", 0);
        aimToggleActive = false;
        playerMap = inputAsset.FindActionMap("Player");
        if (playerMap == null)
        {
            Debug.LogError("[LeonInputBridge] ActionMap Player 未找到");
            return;
        }

        qteMap = inputAsset.FindActionMap("QTE");
        if (qteMap == null)
        {
            Debug.LogError("[LeonInputBridge] ActionMap QTE 未找到");
            return;
        }

        itemInspectionMap = inputAsset.FindActionMap("ItemInspection");
        if (itemInspectionMap == null)
        {
            Debug.LogError("[输入] 未找到物品检视输入映射。");
            return;
        }

        // 启用 UI ActionMap（InputSystemUIInputModule 需要）
        // 界面映射。
var uiMap = inputAsset.FindActionMap("UI");
        if (uiMap != null)
        {
            uiMap.Enable();
        }

        moveAction = playerMap.FindAction("Move");
        lookAction = playerMap.FindAction("Look");
        aimAction = playerMap.FindAction("Aim");
        shootAction = playerMap.FindAction("Shoot");
        reloadAction = playerMap.FindAction("Reload");
        parryAction = playerMap.FindAction("Parry");
        for (int index = 0; index < quickSlotActions.Length; index++)
        {
            quickSlotActions[index] = playerMap.FindAction(WeaponWheelUI.GetSlotActionName(index));
        }
        sprintAction = playerMap.FindAction("Sprint");
        if (sprintAction != null)
        {
            // 恢复映射时，按住的冲刺键也需要参与初始状态检查。
            sprintAction.wantsInitialStateCheck = true;
        }
        crouchAction = playerMap.FindAction("Crouch");
        freeLookAction = playerMap.FindAction("FreeLook");
        inventoryAction = playerMap.FindAction("Inventory");
        interactAction = playerMap.FindAction("Interact");
        sortAction = playerMap.FindAction("Sort");
        escapePressAction = qteMap.FindAction("EscapePress");
        knifeEscapeAction = qteMap.FindAction("KnifeEscape");
        // 兼容仍引用旧输入资源的场景实例。
        if (knifeEscapeAction == null)
        {
            knifeEscapeAction = qteMap.AddAction("KnifeEscape", InputActionType.Button, "<Mouse>/leftButton");
        }
        itemInspectionCloseAction = itemInspectionMap.FindAction("Close");
        if (escapePressAction == null)
        {
            Debug.LogError("[LeonInputBridge] QTE/EscapePress 动作未找到");
            return;
        }

        if (itemInspectionCloseAction == null)
        {
            Debug.LogError("[输入] 未找到物品检视关闭动作。");
            return;
        }

        started = true;
        EnableAndBindActions();
    }

    /// <summary>
    /// 背包打开时禁用 Player ActionMap（避免吞噬鼠标事件），保留 Inventory + Sort 动作
    /// </summary>
    /// <remarks>静态调用链：上游 GameManager.ApplyPermission(GameplayPermissionSnapshot)、LeonInputBridge.OnInventoryToggle(InputAction.CallbackContext)、LeonInputBridge.SetSceneOpenRelease(bool)；下游（含内部回调体） InputAction.Disable()、InputActionMap.Disable()、InputActionMap.Enable()、LeonCombat.HandleKnifeHold(bool)、LeonController.ResetFreeView()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enabled">是否启用。</param>
    public void SetGameplayInputEnabled(bool enabled)
    {
        if (!enabled)
        {
            // 判空前缓存的战斗。
global::LeonCombat receiverValue1 = combat;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                receiverValue1.HandleKnifeHold(false);
            }
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (GameManager.HasInstance)
        {
            enabled &= GameManager.Instance.Permission.Current.AllowInput;
        }

        if (!enabled)
        {
            CloseWeaponWheel(false, false);
        }

        if (!enabled || deathInputLocked || activeQteSessionId > 0 || (waitingSceneOpenRelease && !hasOpenRelease))
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue2 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.ResetFreeView();
                receiverValue2.SetLookInput(Vector2.zero);
            }
        }

        if (playerMap == null)
        {
            return;
        }

        if ((waitingSceneOpenRelease && !hasOpenRelease) || deathInputLocked)
        {
            playerMap.Disable();
            return;
        }

        if (activeQteSessionId > 0)
        {
            playerMap.Disable();
            return;
        }

        if (enabled)
        {
            EnableGameplayMap();
        }
        else
        {
            // 只禁用会吞噬鼠标的动作，保留 Inventory/Sort/Reload(旋转)等
            // 判空前缓存的移动动作。
global::UnityEngine.InputSystem.InputAction receiverValue3 = // 只禁用会吞噬鼠标的动作，保留 Inventory/Sort/Reload(旋转)等
 moveAction;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Disable();
            }

            // 判空前缓存的观察动作。
global::UnityEngine.InputSystem.InputAction receiverValue4 = lookAction;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.Disable();
            }

            // 判空前缓存的瞄准动作。
global::UnityEngine.InputSystem.InputAction receiverValue5 = aimAction;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                receiverValue5.Disable();
            }

            // 判空前缓存的射击动作。
global::UnityEngine.InputSystem.InputAction receiverValue6 = shootAction;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                receiverValue6.Disable();
            }

            // 判空前缓存的招架动作。
global::UnityEngine.InputSystem.InputAction receiverValue8 = parryAction;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                receiverValue8.Disable();
            }

            // receiverValue9（判空前缓存的切换武器动作）：从切换武器动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
            foreach (InputAction action in quickSlotActions)
            {
                if (action != null)
                {
                    action.Disable();
                }
            }

global::UnityEngine.InputSystem.InputAction receiverValue11 = sprintAction;
            if (!global::System.Object.ReferenceEquals(receiverValue11, null))
            {
                receiverValue11.Disable();
            }

            // 判空前缓存的蹲伏动作。
global::UnityEngine.InputSystem.InputAction receiverValue12 = crouchAction;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                receiverValue12.Disable();
            }

            // 判空前缓存的自由环顾动作。
global::UnityEngine.InputSystem.InputAction receiverValue13 = freeLookAction;
            if (!global::System.Object.ReferenceEquals(receiverValue13, null))
            {
                receiverValue13.Disable();
            }

            // 判空前缓存的交互动作。
global::UnityEngine.InputSystem.InputAction receiverValue14 = interactAction;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                receiverValue14.Disable();
            }
        // Inventory / Sort / Reload 保持 enabled
        }
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonInputBridge.EnableAndBindActions()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        InputSystem.onBeforeUpdate += CaptureInputClock;
        InputSystem.onAfterUpdate += ReleaseInputClock;
        if (!started)
        {
            return;
        }

        EnableAndBindActions();
    }

    /// <summary>EnableAndBindActions 的业务处理入口；本方法直接依赖 InputActionMap.Disable()、InputActionMap.Enable()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnEnable()、LeonInputBridge.Start()；下游（含内部回调体） InputActionMap.Disable()、InputActionMap.Enable()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnableAndBindActions()
    {
        if (actionsBound || playerMap == null)
        {
            return;
        }

        if (!deathInputLocked && (!waitingSceneOpenRelease || hasOpenRelease))
        {
            EnableGameplayMap();
        }

        // 判空前缓存的配对动作映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue15 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.Disable();
        }

        // 判空前缓存的物品检视映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue16 = itemInspectionMap;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            receiverValue16.Disable();
        }

        if (moveAction != null)
        {
            moveAction.performed += OnMove;
            moveAction.canceled += OnMove;
        }

        if (lookAction != null)
        {
            lookAction.performed += OnLook;
            lookAction.canceled += OnLook;
        }

        if (aimAction != null)
        {
            aimAction.performed += OnAim;
            aimAction.canceled += OnAim;
        }

        if (shootAction != null)
        {
            shootAction.performed += OnShoot;
            shootAction.canceled += OnShootReleased;
        }

        if (reloadAction != null)
        {
            reloadAction.performed += OnReload;
        }

        if (parryAction != null)
        {
            parryAction.performed += OnParry;
            parryAction.canceled += OnParry;
        }

        foreach (InputAction action in quickSlotActions)
        {
            if (action != null)
            {
                action.performed += OnQuickSlot;
            }
        }

        if (sprintAction != null)
        {
            sprintAction.performed += OnSprintStart;
            sprintAction.canceled += OnSprintEnd;
        }

        if (crouchAction != null)
        {
            crouchAction.performed += OnCrouch;
        }

        if (freeLookAction != null)
        {
            freeLookAction.performed += OnFreeLookStart;
            freeLookAction.canceled += OnFreeLookEnd;
        }

        if (inventoryAction != null)
        {
            inventoryAction.performed += OnInventoryToggle;
        }

        if (interactAction != null)
        {
            interactAction.performed += OnInteract;
        }

        if (sortAction != null)
        {
            sortAction.performed += OnSort;
        }

        escapePressAction.performed += OnEscapePress;
        knifeEscapeAction.performed += OnKnifeEscape;
        itemInspectionCloseAction.performed += OnItemInspectionClose;
        actionsBound = true;
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、InputActionMap.Disable()、LeonCombat.HandleAim(bool)、LeonCombat.HandleKnifeHold(bool)、LeonController.ResetFreeView()、LeonInputBridge.CloseWeaponWheel(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        ClearGameplayIntents();
        InputSystem.onBeforeUpdate -= CaptureInputClock;
        InputSystem.onAfterUpdate -= ReleaseInputClock;
        inputClockCaptured = false;
        movementInputRefreshPending = false;
        ClearPickupBufferState();
        // 判空前缓存的战斗。
global::LeonCombat receiverValue17 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.HandleKnifeHold(false);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue18 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.ResetFreeView();
        }

        // 判空前缓存的物品检视映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue19 = itemInspectionMap;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            receiverValue19.Disable();
        }

        itemInspectionCloseCallback = null;
        if (!actionsBound)
        {
            return;
        }

        if (moveAction != null)
        {
            moveAction.performed -= OnMove;
            moveAction.canceled -= OnMove;
        }

        if (lookAction != null)
        {
            lookAction.performed -= OnLook;
            lookAction.canceled -= OnLook;
        }

        if (aimAction != null)
        {
            aimAction.performed -= OnAim;
            aimAction.canceled -= OnAim;
        }

        if (shootAction != null)
        {
            shootAction.performed -= OnShoot;
            shootAction.canceled -= OnShootReleased;
        }

        if (reloadAction != null)
        {
            reloadAction.performed -= OnReload;
        }

        if (parryAction != null)
        {
            parryAction.performed -= OnParry;
            parryAction.canceled -= OnParry;
        }

        foreach (InputAction action in quickSlotActions)
        {
            if (action != null)
            {
                action.performed -= OnQuickSlot;
            }
        }

        if (sprintAction != null)
        {
            sprintAction.performed -= OnSprintStart;
            sprintAction.canceled -= OnSprintEnd;
        }

        if (crouchAction != null)
        {
            crouchAction.performed -= OnCrouch;
        }

        if (freeLookAction != null)
        {
            freeLookAction.performed -= OnFreeLookStart;
            freeLookAction.canceled -= OnFreeLookEnd;
        }

        if (inventoryAction != null)
        {
            inventoryAction.performed -= OnInventoryToggle;
        }

        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
        }

        if (sortAction != null)
        {
            sortAction.performed -= OnSort;
        }

        if (escapePressAction != null)
        {
            escapePressAction.performed -= OnEscapePress;
        }
        if (knifeEscapeAction != null)
        {
            knifeEscapeAction.performed -= OnKnifeEscape;
        }

        if (itemInspectionCloseAction != null)
        {
            itemInspectionCloseAction.performed -= OnItemInspectionClose;
        }

        playerMap.Disable();
        // 判空前缓存的配对动作映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue20 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.Disable();
        }

        // 判空前缓存的物品检视映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue21 = itemInspectionMap;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.Disable();
        }

        actionsBound = false;
        // 判空前缓存的输入许可。
global::GameplayPermissionService receiverValue22 = inputPermission;
        // 条件访问得到的释放的返回结果。
bool? optionalValue23;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            optionalValue23 = receiverValue22.Release(deathInputPermission);
        }
        else
        {
            optionalValue23 = default;
        }

        deathInputPermission = default;
        activeQteSessionId = -1;
        // 判空前缓存的输入许可。
global::GameplayPermissionService receiverValue24 = inputPermission;
        // 条件访问得到的释放的返回结果。
bool? optionalValue25;
        if (!global::System.Object.ReferenceEquals(receiverValue24, null))
        {
            optionalValue25 = receiverValue24.Release(pairedActionInputPermission);
        }
        else
        {
            optionalValue25 = default;
        }

        pairedActionInputPermission = default;
        CloseWeaponWheel(false);
        // 组件禁用或场景切换时强制退出瞄准，防止切换模式状态卡住
        if (aimToggleActive)
        {
            aimToggleActive = false;
            // 判空前缓存的战斗。
global::LeonCombat receiverValue26 = combat;
            if (!global::System.Object.ReferenceEquals(receiverValue26, null))
            {
                receiverValue26.HandleAim(false);
            }
        }
    }

    /// <summary>
    /// 启用首次获得物品检视的独立关闭输入，并同步玩家当前互动键。
    /// 返回适合显示在关闭提示中的互动键名称。
    /// </summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.ShowRoutine(ItemData, GameObject)；下游（含内部回调体） InputAction.ApplyBindingOverride(int, string)、InputAction.GetBindingDisplayString(int, InputBinding.DisplayStringOptions)、InputAction.RemoveBindingOverride(int)、InputActionMap.Enable()、LeonInputBridge.FindKeyboardInteractPath()、string.IsNullOrEmpty(string)、string.IsNullOrWhiteSpace(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="closeCallback">关闭回调。</param>
    public string BeginItemInspectionInput(System.Action closeCallback)
    {
        itemInspectionCloseCallback = closeCallback;
        if (itemInspectionMap == null || itemInspectionCloseAction == null)
        {
            return "F";
        }

        // 交互路径。
string interactPath = FindKeyboardInteractPath();
        itemInspectionCloseAction.RemoveBindingOverride(0);
        if (!string.IsNullOrEmpty(interactPath))
        {
            itemInspectionCloseAction.ApplyBindingOverride(0, interactPath);
        }

        itemInspectionMap.Enable();
        // 显示名称。
string displayName = itemInspectionCloseAction.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "F";
        }
        else
        {
            return displayName;
        }
    }

    /// <summary>停用首次获得物品检视的独立关闭输入。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionUI.CleanupImmediately()、ItemInspectionUI.HideRoutine()、LeonInputBridge.OnDestroy()；下游（含内部回调体） InputActionMap.Disable()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void EndItemInspectionInput()
    {
        // 判空前缓存的物品检视映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue28 = itemInspectionMap;
        if (!global::System.Object.ReferenceEquals(receiverValue28, null))
        {
            receiverValue28.Disable();
        }

        itemInspectionCloseCallback = null;
    }

    /// <summary>FindKeyboardInteractPath 的业务处理入口；本方法直接依赖 string.IndexOf(string, StringComparison)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.BeginItemInspectionInput(Action)；下游（含内部回调体） string.IndexOf(string, StringComparison)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    string FindKeyboardInteractPath()
    {
        if (interactAction == null)
        {
            return null;
        }

        // 当前遍历索引。
for (int i = 0; i < interactAction.bindings.Count; i++)
        {
            // 绑定。
InputBinding binding = interactAction.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            // 实际生效的路径。
string effectivePath = binding.effectivePath;
            if (!string.IsNullOrEmpty(effectivePath) && effectivePath.IndexOf("<Keyboard>", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return effectivePath;
            }
        }

        return null;
    }

    /// <summary>OnItemInspectionClose 的业务处理入口；本方法直接依赖 Action.Invoke()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="context">上下文。</param>
    void OnItemInspectionClose(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // 判空前缓存的物品检视关闭回调。
global::System.Action receiverValue29 = itemInspectionCloseCallback;
            if (!global::System.Object.ReferenceEquals(receiverValue29, null))
            {
                receiverValue29.Invoke();
            }
        }
    }

    internal bool IsInteractionHeld => interactAction != null && interactAction.enabled && interactAction.IsPressed();

    internal string ReadInteractBindingLabel()
    {
        if (interactAction == null)
        {
            return string.Empty;
        }

        string path = FindKeyboardInteractPath();
        if (!string.IsNullOrEmpty(path))
        {
            for (int i = 0; i < interactAction.bindings.Count; i++)
            {
                if (interactAction.bindings[i].effectivePath == path)
                {
                    return interactAction.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                }
            }
        }

        return interactAction.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
    }

    /// <summary>EnterQTEInput 的业务处理入口；本方法直接依赖 GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float)、InputActionMap.Disable()、InputActionMap.Enable()、LeonCombat.InterruptForQTE()、LeonController.SetFreeLooking(bool)、LeonController.SetLookInput(Vector2)、LeonController.SetMoveInput(Vector3) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.TryStart(QTEKind, IEnemyQTEParticipant, out int)；下游（含内部回调体） GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float)、InputActionMap.Disable()、InputActionMap.Enable()、LeonCombat.InterruptForQTE()、LeonController.SetFreeLooking(bool)、LeonController.SetLookInput(Vector2)、LeonController.SetMoveInput(Vector3) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public bool EnterQTEInput(int sessionId)
    {
        if (deathInputLocked)
        {
            return false;
        }

        if (sessionId <= 0 || !started || qteMap == null || escapePressAction == null)
        {
            return false;
        }

        if (activeQteSessionId > 0)
        {
            return activeQteSessionId == sessionId;
        }

        if (controller != null && controller.IsCrouching && !controller.TrySetCrouching(false))
        {
            return false;
        }

        ClearGameplayIntents();
        activeQteSessionId = sessionId;
        playerMapWasEnabled = playerMap != null && playerMap.enabled;
        if (GameManager.HasInstance)
        {
            inputPermission = GameManager.Instance.Permission;
        }
        else
        {
            inputPermission = null;
        }

        if (inputPermission != null)
        {
            pairedActionInputPermission = inputPermission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, false, false);
        }

        // 判空前缓存的玩家映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue31 = playerMap;
        if (!global::System.Object.ReferenceEquals(receiverValue31, null))
        {
            receiverValue31.Disable();
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue32 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue32, null))
        {
            receiverValue32.SetMoveInput(Vector3.zero);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue33 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.SetLookInput(Vector2.zero);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue34 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.SetSprinting(false);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue35 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue35, null))
        {
            receiverValue35.SetFreeLooking(false);
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue36 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue36, null))
        {
            receiverValue36.InterruptForQTE();
        }

        knifeEscapeAwaitingRelease = IsKnifeEscapeControlPressed();
        qteMap.Enable();
        return true;
    }

    /// <summary>ExitQTEInput 的业务处理入口；本方法直接依赖 GameplayPermissionService.Release(GameplayPermissionHandle)、InputAction.IsPressed()、InputAction.ReadValue&lt;Vector2&gt;()、InputActionMap.Disable()、InputActionMap.Enable()、LeonController.SetMoveInput(Vector3)、LeonController.SetSprinting(bool) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、InputAction.IsPressed()、InputAction.ReadValue&lt;Vector2&gt;()、InputActionMap.Disable()、InputActionMap.Enable()、LeonController.SetMoveInput(Vector3)、LeonController.SetSprinting(bool) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">会话编号。</param>
    public void ExitQTEInput(int sessionId)
    {
        if (sessionId <= 0 || activeQteSessionId != sessionId)
        {
            return;
        }

        // 判空前缓存的配对动作映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue37 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.Disable();
        }

        activeQteSessionId = -1;
        // 判空前缓存的输入许可。
global::GameplayPermissionService receiverValue38 = inputPermission;
        // 条件访问得到的释放的返回结果。
bool? optionalValue39;
        if (!global::System.Object.ReferenceEquals(receiverValue38, null))
        {
            optionalValue39 = receiverValue38.Release(pairedActionInputPermission);
        }
        else
        {
            optionalValue39 = default;
        }

        pairedActionInputPermission = default;
        if (!deathInputLocked && isActiveAndEnabled && ShouldRestoreGameplayMapAfterQTE())
        {
            EnableGameplayMap();
            SyncHeldMovementAfterQTE();
        }

        playerMapWasEnabled = false;
    }

    private void EnableGameplayMap()
    {
        if (playerMap == null)
        {
            return;
        }

        bool movementWasDisabled = moveAction != null && !moveAction.enabled;
        bool sprintWasDisabled = sprintAction != null && !sprintAction.enabled;
        bool combatWasDisabled = (aimAction != null && !aimAction.enabled)
            || (parryAction != null && !parryAction.enabled);
        playerMap.Enable();
        if (movementWasDisabled || sprintWasDisabled)
        {
            RequestMovementInputRefresh();
        }

        if (movementWasDisabled || sprintWasDisabled || combatWasDisabled)
        {
            restoreHeldAim = true;
            restoreHeldGuard = true;
        }

        if (CanUseGameplayMovementSync())
        {
            SyncHeldMovementImmediately();
            NotifyActionWindowOpened();
        }
    }

    internal void RequestMovementInputRefresh()
    {
        movementInputRefreshPending = true;
    }

    private bool CanUseGameplayMovementSync()
    {
        if (!isActiveAndEnabled || controller == null || weaponWheelOpen || deathInputLocked)
        {
            return false;
        }

        if (activeQteSessionId > 0 || (waitingSceneOpenRelease && !hasOpenRelease))
        {
            return false;
        }

        if (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        if (GameUIManager.HasInstance && GameUIManager.Instance.IsPlayerInputBlocked)
        {
            return false;
        }

        if (InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
        {
            return false;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return false;
        }

        return true;
    }

    internal Vector2 ReadEffectiveMoveInput()
    {
        if (moveAction == null)
        {
            return Vector2.zero;
        }

        Vector2 actionValue = Vector2.zero;
        if (moveAction.enabled)
        {
            actionValue = moveAction.ReadValue<Vector2>();
        }

        if (actionValue.sqrMagnitude > 0.0001f)
        {
            return actionValue;
        }

        Vector2 rawValue = ReadRawMoveInput();
        if (rawValue.sqrMagnitude > 0.0001f)
        {
            return rawValue;
        }

        return Vector2.zero;
    }

    internal bool IsEffectiveSprintPressed()
    {
        if (sprintAction != null && sprintAction.enabled && sprintAction.IsPressed())
        {
            return true;
        }

        return IsRawSprintPressed();
    }

    internal bool IsEffectiveAimPressed()
    {
        if (aimAction != null && aimAction.enabled && aimAction.IsPressed())
        {
            return true;
        }

        return IsRawAimPressed();
    }

    internal bool IsEffectiveGuardPressed()
    {
        if (parryAction != null && parryAction.enabled && parryAction.IsPressed())
        {
            return true;
        }

        return IsActionControlPressed(parryAction);
    }

    internal void RestoreMovementInput()
    {
        if (!CanUseGameplayMovementSync())
        {
            return;
        }

        bool canMoveWhileHitReleased = controller.currentState == LeonState.Staggered
            && controller.AnimDriver != null
            && controller.AnimDriver.HitMovementReleased;
        if (!controller.CanAct() && !canMoveWhileHitReleased)
        {
            return;
        }

        if (moveAction == null || !moveAction.enabled || sprintAction == null || !sprintAction.enabled)
        {
            return;
        }

        if (movementDriver == null)
        {
            return;
        }

        var token = movementDriver.CurrentMotionToken;
        if (!token.Valid || token.Source != GameArchitecture.MotionSource.NormalMovement)
        {
            return;
        }

        Vector2 movement = ReadEffectiveMoveInput();
        Vector3 heldMovement = new Vector3(movement.x, 0f, movement.y);
        if (movementInputRefreshPending || controller.MoveInput != heldMovement)
        {
            controller.SetMoveInput(heldMovement);
        }

        movementInputRefreshPending = false;
        controller.SetSprinting(IsEffectiveSprintPressed());
    }

    /// <summary>ShouldRestoreGameplayMapAfterQTE 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.ExitQTEInput(int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool ShouldRestoreGameplayMapAfterQTE()
    {
        if (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        if (playerMapWasEnabled)
        {
            return true;
        }

        if (InventorySceneLoader.Instance != null && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
        {
            return false;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return false;
        }

        if (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 死亡流程的永久输入锁。界面切换或快速反应事件退出都不能重新启用玩家输入。
    /// </summary>
    /// <remarks>静态调用链：上游 LeonController.BeginDeathSequence()；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、GameplayPermissionService.Request(ObjectIdentity, bool, bool, bool, float)、InputActionMap.Disable()、LeonCombat.ForceStopShoot()、LeonCombat.HandleAim(bool)、LeonController.SetFreeLooking(bool)、LeonController.SetLookInput(Vector2) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void LockForDeath()
    {
        if (deathInputLocked)
        {
            return;
        }

        deathInputLocked = true;
        if (GameManager.HasInstance)
        {
            inputPermission = GameManager.Instance.Permission;
        }
        else
        {
            inputPermission = null;
        }

        if (inputPermission != null)
        {
            deathInputPermission = inputPermission.Request(new GameArchitecture.ObjectIdentity(GetInstanceID(), 1), true, false, false);
        }

        playerMapWasEnabled = false;
        activeQteSessionId = -1;
        // 判空前缓存的输入许可。
global::GameplayPermissionService receiverValue42 = inputPermission;
        // 条件访问得到的释放的返回结果。
bool? optionalValue43;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            optionalValue43 = receiverValue42.Release(pairedActionInputPermission);
        }
        else
        {
            optionalValue43 = default;
        }

        pairedActionInputPermission = default;
        CloseWeaponWheel(false);
        // 判空前缓存的玩家映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue44 = playerMap;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.Disable();
        }

        // 判空前缓存的配对动作映射。
global::UnityEngine.InputSystem.InputActionMap receiverValue45 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.Disable();
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue46 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue46, null))
        {
            receiverValue46.SetMoveInput(Vector3.zero);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue47 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue47, null))
        {
            receiverValue47.SetLookInput(Vector2.zero);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue48 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue48, null))
        {
            receiverValue48.SetSprinting(false);
        }

        // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue49 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.SetFreeLooking(false);
        }

        aimToggleActive = false;
        // 判空前缓存的战斗。
global::LeonCombat receiverValue50 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.HandleAim(false);
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue51 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue51, null))
        {
            receiverValue51.ForceStopShoot();
        }
    }

    /// <summary>中文含义：是否死亡输入锁定。</summary>
    public bool IsDeathInputLocked => deathInputLocked;

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.SetFreeLooking(bool)、LeonInputBridge.HandleScrollWeaponWheel()、LeonInputBridge.UpdateScrollWeaponWheelAutoConfirm()、object.ReferenceEquals(object, object)、UICursorManager.HideCustomCursor()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        ReplayGameplayIntents();
        // 动作映射禁用后仍读取真实按键，防止取消回调冒充松键。
        if (Application.isFocused)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue52 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue52, null))
            {
                receiverValue52.SetFreeLooking(FreeViewKeyActualHeld);
            }
        }

        if (!CanUseQuickSelect())
        {
            CloseWeaponWheel(false);
            return;
        }

        HandleScrollWeaponWheel();
        UpdateScrollWeaponWheelAutoConfirm();
        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue53 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue53, null))
        {
            receiverValue53.HideCustomCursor();
        }
    }

    private bool CanUseGameplayInput()
    {
        return controller != null && Application.isFocused && !weaponWheelOpen && !deathInputLocked
            && activeQteSessionId <= 0 && (!waitingSceneOpenRelease || hasOpenRelease)
            && (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput)
            && (!GameUIManager.HasInstance || !GameUIManager.Instance.IsPlayerInputBlocked)
            && (!InventorySceneLoader.HasInstance || !InventorySceneLoader.Instance.IsVisible && !InventorySceneLoader.Instance.IsLoading)
            && (StorageBoxUI.Instance == null || !StorageBoxUI.Instance.IsVisible);
    }

    /// <summary>OnMove 的业务处理入口；本方法直接依赖 Action.Invoke()、InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetMoveInput(Vector3)、object.ReferenceEquals(object, object)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetMoveInput(Vector3)、object.ReferenceEquals(object, object)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnMove(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            return;
        }

        // 读取值的返回结果。
var v = ctx.ReadValue<Vector2>();
        if (controller)
        {
            controller.SetMoveInput(new Vector3(v.x, 0, v.y));
        }

        if (hasReportedMoveInput)
        {
            return;
        }

        if (v.sqrMagnitude < moveTutorialInputThreshold * moveTutorialInputThreshold)
        {
            return;
        }

        hasReportedMoveInput = true;
        // 判空前缓存的玩家已移动时的通知回调。
global::System.Action receiverValue54 = OnPlayerMoved;
        if (!global::System.Object.ReferenceEquals(receiverValue54, null))
        {
            receiverValue54.Invoke();
        }
    }

    /// <summary>OnLook 的业务处理入口；本方法直接依赖 InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetLookInput(Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetLookInput(Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnLook(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            if (controller != null)
            {
                controller.SetLookInput(Vector2.zero);
            }
            return;
        }

        if (controller)
        {
            controller.SetLookInput(ctx.ReadValue<Vector2>());
        }
    }

    /// <summary>OnAim 的业务处理入口；本方法直接依赖 InputAction.CallbackContext.ReadValueAsButton()、LeonCombat.HandleAim(bool)、LeonInputBridge.CloseWeaponWheel(bool)、object.ReferenceEquals(object, object)、PlayerPrefs.GetInt(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InputAction.CallbackContext.ReadValueAsButton()、LeonCombat.HandleAim(bool)、LeonInputBridge.CloseWeaponWheel(bool)、object.ReferenceEquals(object, object)、PlayerPrefs.GetInt(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnAim(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            return;
        }

        if (weaponWheelOpen)
        {
            CloseWeaponWheel(false);
        }

        // 重新读取设置，确保运行时修改立即生效
        aimMode = PlayerPrefs.GetInt("AimMode", 0);
        bool pickupActive = actions != null && actions.IsItemPickupActive;
        if (aimMode == 1)
        {
            // 切换模式：仅 performed 时切换状态
            if (ctx.performed)
            {
                aimToggleActive = !aimToggleActive;
                if (pickupActive)
                {
                    BufferPickupAim(aimToggleActive);
                    return;
                }

                restoreHeldAim = aimToggleActive;
                if (GameplayActionRecovering(GameplayIntentKind.Shoot))
                {
                    return;
                }

                // 判空前缓存的战斗。
global::LeonCombat receiverValue55 = combat;
                if (!global::System.Object.ReferenceEquals(receiverValue55, null))
                {
                    receiverValue55.HandleAim(aimToggleActive);
                }
            }
        }
        else
        {
            // 长按模式：performed=true, canceled=false
            bool aimHeld = ctx.ReadValueAsButton();
            if (pickupActive)
            {
                BufferPickupAim(aimHeld);
                return;
            }

            restoreHeldAim = aimHeld;
            if (aimHeld && GameplayActionRecovering(GameplayIntentKind.Shoot))
            {
                return;
            }

            // 判空前缓存的战斗。
global::LeonCombat receiverValue56 = // 长按模式：performed=true, canceled=false
 combat;
            if (!global::System.Object.ReferenceEquals(receiverValue56, null))
            {
                receiverValue56.HandleAim(aimHeld);
            }
        }
    }

    /// <summary>OnShoot 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackInput(double)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.CommitAttackInput(double)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnShoot(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            return;
        }
        if (weaponWheelOpen)
        {
            CloseWeaponWheel(false);
        }
        if (CanUseGameplayInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Shoot, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnShootReleased 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackReleased(double)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.CommitAttackReleased(double)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnShootReleased(InputAction.CallbackContext ctx)
    {
        if (weaponWheelOpen)
        {
            return;
        }

        // 判空前缓存的战斗。
global::LeonCombat receiverValue58 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue58, null))
        {
            receiverValue58.CommitAttackReleased(ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnReload 的业务处理入口；本方法直接依赖 InventoryUI.RotateSelectedItem()、LeonCombat.HandleReload()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.RotateSelectedItem()、LeonCombat.HandleReload()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnReload(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            // 判空前缓存的背包界面的实例。
global::InventoryUI receiverValue59 = InventoryUI.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue59, null))
            {
                receiverValue59.RotateSelectedItem();
            }

            return;
        }

        if (weaponWheelOpen)
        {
            return;
        }

        if (CanUseGameplayInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Reload, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>CaptureInputClock 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CaptureInputClock()
    {
        // 同一批输入共享时间基准，不让前一个回调的处理耗时改变后一个事件的年龄。
        inputUpdateRealtime = UnityEngine.InputSystem.LowLevel.InputState.currentTime;
        inputUpdateGameTime = Time.timeAsDouble;
        inputUpdateTimeScale = Time.timeScale;
        inputClockCaptured = true;
    }

    /// <summary>ReleaseInputClock 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ReleaseInputClock()
    {
        inputClockCaptured = false;
    }

    /// <summary>ConvertInputTime 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnParry(InputAction.CallbackContext)、LeonInputBridge.OnShoot(InputAction.CallbackContext)、LeonInputBridge.OnShootReleased(InputAction.CallbackContext)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="originalTime">原始时间。</param>
    private double ConvertInputTime(double originalTime)
    {
        if (inputClockCaptured)
        {
            return inputUpdateGameTime + (originalTime - inputUpdateRealtime) * inputUpdateTimeScale;
        }

        return Time.timeAsDouble + (originalTime - UnityEngine.InputSystem.LowLevel.InputState.currentTime) * Time.timeScale;
    }

    /// <summary>OnParry 的业务处理入口；本方法直接依赖 LeonCombat.CommitGuardInput(bool, double)、LeonController.TrySetCrouching(bool)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.CommitGuardInput(bool, double)、LeonController.TrySetCrouching(bool)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    internal bool CanReadParryInput => isActiveAndEnabled && parryAction != null && parryAction.enabled && CanUseGameplayInput();
    internal bool IsParryInputHeld => parryAction != null && IsEffectiveGuardPressed();

    internal string GetParryBindingDisplayName()
    {
        if (parryAction != null)
        {
            foreach (InputBinding binding in parryAction.bindings)
            {
                if (string.Equals(binding.effectivePath, "<Keyboard>/space", System.StringComparison.OrdinalIgnoreCase))
                {
                    return "空格";
                }
            }

            string displayName = parryAction.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }

        return "空格";
    }

    void OnParry(InputAction.CallbackContext ctx)
    {
        if (ctx.canceled)
        {
            restoreHeldGuard = false;
            DiscardGameplayIntent(GameplayIntentKind.Parry);
            if (combat != null)
            {
                combat.CommitGuardInput(false, ConvertInputTime(ctx.time));
            }
            return;
        }

        if (weaponWheelOpen)
        {
            CloseWeaponWheel(false);
        }
        if (!ctx.performed || !CanUseGameplayInput())
        {
            return;
        }
        if (qteCoordinator != null && qteCoordinator.TrySubmitEmergencyDefense())
        {
            return;
        }

        restoreHeldGuard = true;
        DispatchGameplayIntent(GameplayIntentKind.Parry, ConvertInputTime(ctx.time));
    }

    /// <summary>OnSwitchWeapon 的业务处理入口；本方法直接依赖 Array.IndexOf&lt;WeaponType&gt;(WeaponType[], WeaponType)、Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、Mathf.Max(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Array.IndexOf&lt;WeaponType&gt;(WeaponType[], WeaponType)、Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    public string GetQuickSlotBindingDisplay(int slotIndex)
    {
        InputAction action = quickSlotActions[slotIndex];
        if (action != null && action.bindings.Count > 0)
        {
            return action.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        return string.Empty;
    }

    private void OnQuickSlot(InputAction.CallbackContext context)
    {
        if (!context.performed || !CanUseQuickSelect())
        {
            return;
        }

        int slotIndex = System.Array.IndexOf(quickSlotActions, context.action);
        if (slotIndex < 0 || !weaponWheel.IsSlotAvailable(slotIndex))
        {
            return;
        }

        CloseWeaponWheel(false);
        if (CanUseGameplayInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.QuickSlot, ConvertInputTime(context.time), slotIndex);
        }
    }

    /// <summary>OnSprintStart 的业务处理入口；本方法直接依赖 LeonController.SetSprinting(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.SetSprinting(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnSprintStart(InputAction.CallbackContext ctx)
    {
        if (controller && !controller.IsCrouching)
        {
            controller.SetSprinting(true);
        }
    }

    /// <summary>OnSprintEnd 的业务处理入口；本方法直接依赖 LeonController.SetSprinting(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.SetSprinting(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnSprintEnd(InputAction.CallbackContext ctx)
    {
        if (controller)
        {
            controller.SetSprinting(false);
        }
    }

    /// <summary>OnCrouch 的业务处理入口；本方法直接依赖 LeonController.ToggleCrouch()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ToggleCrouch()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnCrouch(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || weaponWheelOpen)
        {
            return;
        }

        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            return;
        }

        if (CanUseGameplayInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Crouch, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnFreeLookStart 的业务处理入口；本方法直接依赖 LeonController.SetFreeLooking(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.SetFreeLooking(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnFreeLookStart(InputAction.CallbackContext ctx)
    {
        if (controller)
        {
            controller.SetFreeLooking(true);
        }
    }

    /// <summary>OnFreeLookEnd 的业务处理入口；本方法直接依赖 LeonController.ResetFreeView()、LeonController.SetFreeLooking(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonController.ResetFreeView()、LeonController.SetFreeLooking(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnFreeLookEnd(InputAction.CallbackContext ctx)
    {
        if (!Application.isFocused || FreeViewKeyActualHeld)
        {
            // 判空前缓存的里昂玩家控制器。
global::LeonController receiverValue66 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue66, null))
            {
                receiverValue66.ResetFreeView();
            }

            return;
        }

        if (controller)
        {
            controller.SetFreeLooking(false);
        }
    }

    /// <summary>OnInventoryToggle 的业务处理入口；本方法直接依赖 GameUIManager.TryClaimModalInput()、InventorySceneLoader.Hide()、InventorySceneLoader.Show()、LeonInputBridge.SetGameplayInputEnabled(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.TryClaimModalInput()、InventorySceneLoader.Hide()、InventorySceneLoader.Show()、LeonInputBridge.SetGameplayInputEnabled(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    void OnInventoryToggle(InputAction.CallbackContext ctx)
    {
        if (VillageBellSequence.IsPresenting || weaponWheelOpen)
        {
            return;
        }

        // 存储箱打开动画、加载场景或已经显示时，背包不能抢占同一帧的切换输入。
        if (StorageBoxController.IsAnyBoxBusy)
        {
            return;
        }

        if (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible)
        {
            return;
        }

        if (GameUIManager.HasInstance && !GameUIManager.Instance.TryClaimModalInput())
        {
            return;
        }

        // 加载器。
var loader = InventorySceneLoader.Instance;
        if (loader == null || loader.IsLoading)
        {
            return;
        }

        if (loader.IsVisible)
        {
            SetGameplayInputEnabled(true);
            loader.Hide();
        }
        else
        {
            SetGameplayInputEnabled(false);
            loader.Show();
        }
    }

    /// <summary>OnInteract 的业务处理入口；本方法直接依赖 InteractionSystem.InteractNearby(Transform)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)、LeonQTECoordinator.TryStartNearestPrompt(out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InteractionSystem.InteractNearby(Transform)、LeonQTECoordinator.TryStartNearestBodyTechnique(out int)、LeonQTECoordinator.TryStartNearestPrompt(out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && CanUseGameplayInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Interact, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnSort 的业务处理入口；本方法直接依赖 InventoryManager.Sort()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryManager.Sort()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>
    internal InputAction FindInventoryAction(string actionName)
    {
        if (playerMap == null)
        {
            return null;
        }

        return playerMap.FindAction(actionName);
    }

    void OnSort(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible && InventoryUI.Instance != null && InventoryUI.Instance.Interactive)
        {
            // 判空前缓存的背包管理器的实例。
global::InventoryManager receiverValue67 = InventoryManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue67, null))
            {
                receiverValue67.Sort();
            }
        }
    }

    /// <summary>OnEscapePress 的业务处理入口；本方法直接依赖 LeonQTECoordinator.SubmitEscapePress(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.SubmitEscapePress(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">当前回调上下文。</param>
    internal bool IsEscapeHeld(int session)
    {
        return session > 0 && session == activeQteSessionId && escapePressAction != null &&
            escapePressAction.enabled && escapePressAction.IsPressed();
    }

    internal bool HasKnifeEscapeBinding => knifeEscapeAction != null && knifeEscapeAction.controls.Count > 0;

    private static bool IsActionControlPressed(InputAction action)
    {
        if (action == null)
        {
            return false;
        }
        foreach (InputControl control in action.controls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed)
            {
                return true;
            }
        }
        foreach (InputBinding binding in action.bindings)
        {
            if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath))
            {
                continue;
            }
            InputControl resolved = InputSystem.FindControl(binding.effectivePath);
            if (resolved is UnityEngine.InputSystem.Controls.ButtonControl resolvedButton && resolvedButton.isPressed)
            {
                return true;
            }
        }
        return false;
    }

    internal Vector2 ReadRawMoveInput()
    {
        if (moveAction == null)
        {
            return Vector2.zero;
        }
        Vector2 result = Vector2.zero;
        bool readAnyControl = false;
        foreach (InputControl control in moveAction.controls)
        {
            readAnyControl = true;
            if (control is UnityEngine.InputSystem.Controls.StickControl stick)
            {
                result += stick.ReadValue();
            }
            else if (control is UnityEngine.InputSystem.Controls.Vector2Control vectorControl)
            {
                result += vectorControl.ReadValue();
            }
            else if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed)
            {
                InputBinding? matchedBinding = moveAction.GetBindingForControl(control);
                if (matchedBinding.HasValue)
                {
                    result += ReadMovePartDirection(matchedBinding.Value.name);
                }
            }
        }
        if (!readAnyControl)
        {
            foreach (InputBinding binding in moveAction.bindings)
            {
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath))
                {
                    continue;
                }
                InputControl control = InputSystem.FindControl(binding.effectivePath);
                if (control is UnityEngine.InputSystem.Controls.StickControl stick)
                {
                    result += stick.ReadValue();
                }
                else if (control is UnityEngine.InputSystem.Controls.Vector2Control vectorControl)
                {
                    result += vectorControl.ReadValue();
                }
                else if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.isPressed)
                {
                    result += ReadMovePartDirection(binding.name);
                }
            }
        }
        if (result.sqrMagnitude > 1f)
        {
            result.Normalize();
        }
        return result;
    }

    private static Vector2 ReadMovePartDirection(string partName)
    {
        if (string.Equals(partName, "up", System.StringComparison.OrdinalIgnoreCase))
        {
            return Vector2.up;
        }
        if (string.Equals(partName, "down", System.StringComparison.OrdinalIgnoreCase))
        {
            return Vector2.down;
        }
        if (string.Equals(partName, "right", System.StringComparison.OrdinalIgnoreCase))
        {
            return Vector2.right;
        }
        if (string.Equals(partName, "left", System.StringComparison.OrdinalIgnoreCase))
        {
            return Vector2.left;
        }
        return Vector2.zero;
    }

    internal bool IsRawSprintPressed()
    {
        return IsActionControlPressed(sprintAction);
    }

    internal bool IsRawAimPressed()
    {
        return IsActionControlPressed(aimAction);
    }

    internal void SyncHeldMovementImmediately()
    {
        if (!CanUseGameplayMovementSync())
        {
            return;
        }

        Vector2 rawMove = ReadEffectiveMoveInput();
        bool rawSprint = IsEffectiveSprintPressed();
        movementInputRefreshPending = false;
        controller.SetMoveInput(new Vector3(rawMove.x, 0f, rawMove.y));
        controller.SetSprinting(rawSprint);
    }

    private void SyncHeldMovementAfterQTE()
    {
        RequestMovementInputRefresh();
        if (controller == null)
        {
            return;
        }

        Vector2 rawMove = ReadEffectiveMoveInput();
        bool rawSprint = IsEffectiveSprintPressed();
        if (rawMove.sqrMagnitude > 0.0001f || rawSprint)
        {
            movementInputRefreshPending = false;
            controller.SetMoveInput(new Vector3(rawMove.x, 0f, rawMove.y));
            controller.SetSprinting(rawSprint);
        }
    }

    private bool IsKnifeEscapeControlPressed()
    {
        return IsActionControlPressed(knifeEscapeAction);
    }

    internal void ObserveKnifeEscapeRelease()
    {
        if (knifeEscapeAwaitingRelease && !IsKnifeEscapeControlPressed())
        {
            knifeEscapeAwaitingRelease = false;
        }
    }

    private bool CanSubmitQteInput()
    {
        return activeQteSessionId > 0 && qteCoordinator != null && !deathInputLocked
            && Time.timeScale > 0f && Application.isFocused
            && (!GameUIManager.HasInstance || !GameUIManager.Instance.HasBlockingModal);
    }

    private void OnKnifeEscape(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && !knifeEscapeAwaitingRelease && CanSubmitQteInput())
        {
            qteCoordinator.SubmitFrontGrabKnife(activeQteSessionId);
        }
    }

    void OnEscapePress(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && CanSubmitQteInput())
        {
            qteCoordinator.SubmitEscapePress(activeQteSessionId, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnWeaponWheelStart 的业务处理入口；本方法直接依赖 LeonCombat.ForceStopShoot()、LeonInputBridge.CanStartWeaponWheel()、object.ReferenceEquals(object, object)、WeaponWheelUI.BeginSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.ForceStopShoot()、LeonInputBridge.CanStartWeaponWheel()、object.ReferenceEquals(object, object)、WeaponWheelUI.BeginSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>


    /// <summary>OnWeaponWheelEnd 的业务处理入口；本方法直接依赖 LeonInputBridge.CloseWeaponWheel(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonInputBridge.CloseWeaponWheel(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。</param>


    /// <summary>CloseWeaponWheel 的业务处理入口；本方法直接依赖 LeonInputBridge.SyncAimAfterWeaponWheel()、object.ReferenceEquals(object, object)、WeaponWheelUI.CancelSelection()、WeaponWheelUI.ConfirmSelection()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.LockForDeath()、LeonInputBridge.OnAim(InputAction.CallbackContext)、LeonInputBridge.OnDisable()、LeonInputBridge.OnParry(InputAction.CallbackContext)、LeonInputBridge.OnShoot(InputAction.CallbackContext) 等共 7 处；下游（含内部回调体） LeonInputBridge.SyncAimAfterWeaponWheel()、object.ReferenceEquals(object, object)、WeaponWheelUI.CancelSelection()、WeaponWheelUI.ConfirmSelection()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="confirm">是否确认。</param>
    void CloseWeaponWheel(bool confirm, bool restoreAim = true)
    {
        if (!weaponWheelOpen)
        {
            return;
        }

        weaponWheelOpen = false;
        nextWeaponWheelScrollOpenTime = Time.unscaledTime + 0.06f;
        if (weaponWheel != null)
        {
            if (confirm && CanUseQuickSelect())
            {
                weaponWheel.ConfirmSelection();
            }
            else
            {
                weaponWheel.CancelSelection();
            }
        }

        if (restoreAim && CanUseQuickSelect())
        {
            SyncAimAfterWeaponWheel();
        }
    }

    /// <summary>CanStartWeaponWheel 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.HandleScrollWeaponWheel()、LeonInputBridge.OnWeaponWheelStart(InputAction.CallbackContext)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool CanUseQuickSelect()
    {
        return weaponWheel != null && Application.isFocused && !deathInputLocked
            && activeQteSessionId <= 0 && (!waitingSceneOpenRelease || hasOpenRelease)
            && controller != null && controller.NewPipelineValid
            && controller.currentState != LeonState.QTE && controller.currentState != LeonState.Dead
            && !WeaponWheelUI.IsPresentationBlocked();
    }

    /// <summary>HandleScrollWeaponWheel 的业务处理入口；本方法直接依赖 InputControl&lt;Vector2&gt;.ReadValue()、LeonCombat.ForceStopShoot()、LeonInputBridge.CanStartWeaponWheel()、Mathf.Abs(float)、object.ReferenceEquals(object, object)、WeaponWheelUI.BeginSelectionFromScroll(float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.Update()；下游（含内部回调体） InputControl&lt;Vector2&gt;.ReadValue()、LeonCombat.ForceStopShoot()、LeonInputBridge.CanStartWeaponWheel()、Mathf.Abs(float)、object.ReferenceEquals(object, object)、WeaponWheelUI.BeginSelectionFromScroll(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleScrollWeaponWheel()
    {
        if (!scrollOpensWeaponWheel || Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < weaponWheel.scrollDeadZone)
        {
            return;
        }

        if (weaponWheelOpen)
        {
            weaponWheel.MovePreview(scroll);
        }
        else if (Time.unscaledTime >= nextWeaponWheelScrollOpenTime)
        {
            weaponWheelOpen = weaponWheel.BeginSelectionFromScroll(scroll);
            if (weaponWheelOpen && combat != null)
            {
                combat.ForceStopShoot();
            }
        }

        if (weaponWheelOpen)
        {
            weaponWheelAutoConfirmTime = Time.unscaledTime + scrollAutoConfirmDelay;
        }
    }

    /// <summary>UpdateScrollWeaponWheelAutoConfirm 的业务处理入口；本方法直接依赖 LeonInputBridge.CloseWeaponWheel(bool)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.Update()；下游（含内部回调体） LeonInputBridge.CloseWeaponWheel(bool)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateScrollWeaponWheelAutoConfirm()
    {
        if (weaponWheelOpen && Time.unscaledTime >= weaponWheelAutoConfirmTime)
        {
            CloseWeaponWheel(true);
        }
    }

    /// <summary>SyncAimAfterWeaponWheel 的业务处理入口；本方法直接依赖 InputAction.ReadValue&lt;float&gt;()、LeonCombat.HandleAim(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.CloseWeaponWheel(bool)；下游（含内部回调体） InputAction.ReadValue&lt;float&gt;()、LeonCombat.HandleAim(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void SyncAimAfterWeaponWheel()
    {
        if (combat == null || aimMode != 0 || aimAction == null)
        {
            return;
        }

        combat.HandleAim(aimAction.ReadValue<float>() > 0.5f);
    }

    /// <summary>中文含义：是否配对动作输入就绪。</summary>
    public bool IsQTEInputReady => started && escapePressAction != null;

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonInputBridge.EndItemInspectionInput()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        EndItemInspectionInput();
        if (ownsInputAsset && inputAsset != null)
        {
            Destroy(inputAsset);
        }

        inputAsset = null;
        ownsInputAsset = false;
    }
}
