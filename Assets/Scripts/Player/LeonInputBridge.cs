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
    /// <summary>中文含义：正式引用，Inspector 绑定。运行时优先使用。使用位置：LeonInputBridge.Start()。</summary>
    [Header("输入资源")]
    [Tooltip("正式引用，Inspector 绑定。运行时优先使用。")]
    [SerializeField]
    private InputActionAsset inputActionAsset;
    /// <summary>中文含义：输入资源。使用位置：作为 InputBindingPersistence.Load(InputActionAsset) 的参数、作为 Object.Destroy(Object) 的参数、LeonInputBridge.OnDestroy() 等共 4 处。</summary>
    private InputActionAsset inputAsset;
    /// <summary>中文含义：玩家映射。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int) 等共 7 处。</summary>
    private InputActionMap playerMap;
    private LeonActionMotionDriver movementDriver;
    private LeonAnimationActionCoordinator actions;
    private bool movementInputRefreshPending;
    private bool movementInputAwaitingUpdate;
    private enum PickupInput { None, Interact, Shoot, Reload }
    private int pickupInputTransaction;
    private bool pickupInputReleased;
    private bool pendingPickupAim;
    private PickupInput pendingPickupAction;
    /// <summary>中文含义：配对动作映射。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int) 等共 6 处。</summary>
    private InputActionMap qteMap;
    /// <summary>中文含义：物品检视映射。使用位置：LeonInputBridge.BeginItemInspectionInput(Action)、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.EndItemInspectionInput() 等共 5 处。</summary>
    private InputActionMap itemInspectionMap;
    /// <summary>中文含义：里昂玩家控制器。使用位置：作为 InteractionSystem.InteractNearby(Transform) 的参数、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int) 等共 19 处。</summary>
    private LeonController controller;
    /// <summary>中文含义：战斗。使用位置：LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.HandleScrollWeaponWheel()、LeonInputBridge.LockForDeath() 等共 15 处。</summary>
    private LeonCombat combat;
    /// <summary>中文含义：当前里昂配对动作协调器。使用位置：LeonInputBridge.OnEscapePress(InputAction.CallbackContext)、LeonInputBridge.OnInteract(InputAction.CallbackContext)、LeonInputBridge.Start()。</summary>
    private LeonQTECoordinator qteCoordinator;
    /// <summary>中文含义：武器轮盘。使用位置：作为 Mathf.Max(float, float) 的参数、LeonInputBridge.CanStartWeaponWheel()、LeonInputBridge.CloseWeaponWheel(bool) 等共 7 处。</summary>
    private WeaponWheelUI weaponWheel;
    /// <summary>中文含义：移动教学输入阈值。使用位置：LeonInputBridge.OnMove(InputAction.CallbackContext)。</summary>
    [Header("教学事件")]
    [SerializeField]
    private float moveTutorialInputThreshold = 0.1f;
    /// <summary>中文含义：是否滚动开放武器轮盘。使用位置：LeonInputBridge.HandleScrollWeaponWheel()。</summary>
    [Header("快速武器轮盘")]
    [SerializeField]
    private bool scrollOpensWeaponWheel = true;
    /// <summary>中文含义：滚动自动确认延迟。使用位置：作为 Mathf.Max(float, float) 的参数、LeonInputBridge.HandleScrollWeaponWheel()。</summary>
    [SerializeField, Min(0.05f)]
    private float scrollAutoConfirmDelay = 0.45f;
    /// <summary>中文含义：移动动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.OnDisable() 等共 5 处。</summary>
    private InputAction moveAction;
    /// <summary>中文含义：观察动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    private InputAction lookAction;
    /// <summary>中文含义：瞄准动作。使用位置：作为 LeonCombat.HandleAim(bool) 的参数、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable() 等共 6 处。</summary>
    private InputAction aimAction;
    /// <summary>中文含义：射击动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    private InputAction shootAction;
    /// <summary>中文含义：换弹动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.Start()。</summary>
    private InputAction reloadAction;
    /// <summary>中文含义：招架动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    private InputAction parryAction;
    private readonly InputAction[] quickSlotActions = new InputAction[WeaponWheelUI.SlotCount];
    /// <summary>中文含义：冲刺动作。使用位置：作为 LeonController.SetSprinting(bool) 的参数、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.ExitQTEInput(int) 等共 6 处。</summary>
    private InputAction sprintAction;
    /// <summary>中文含义：蹲伏动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    private InputAction crouchAction;
    /// <summary>中文含义：自由环顾动作。使用位置：LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.FreeViewKeyActualHeld.get 等共 6 处。</summary>
    private InputAction freeLookAction;
    /// <summary>中文含义：背包动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.Start()。</summary>
    private InputAction inventoryAction;
    /// <summary>中文含义：交互动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.FindKeyboardInteractPath()、LeonInputBridge.OnDisable() 等共 5 处。</summary>
    private InputAction interactAction;
    /// <summary>中文含义：排序动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()、LeonInputBridge.Start()。</summary>
    private InputAction sortAction;
    /// <summary>中文含义：逃脱按下动作。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.IsQTEInputReady.get 等共 5 处。</summary>
    private InputAction escapePressAction;
    /// <summary>中文含义：物品检视关闭动作。使用位置：LeonInputBridge.BeginItemInspectionInput(Action)、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable() 等共 4 处。</summary>
    private InputAction itemInspectionCloseAction;
    /// <summary>中文含义：物品检视关闭回调。使用位置：LeonInputBridge.BeginItemInspectionInput(Action)、LeonInputBridge.EndItemInspectionInput()、LeonInputBridge.OnDisable() 等共 4 处。</summary>
    private System.Action itemInspectionCloseCallback;
    /// <summary>中文含义：动作集合是否已绑定。使用位置：LeonInputBridge.EnableAndBindActions()、LeonInputBridge.OnDisable()。</summary>
    private bool actionsBound;
    /// <summary>中文含义：是否输入时钟已捕获。使用位置：LeonInputBridge.CaptureInputClock()、LeonInputBridge.ConvertInputTime(double)、LeonInputBridge.OnDisable() 等共 4 处。</summary>
    private bool inputClockCaptured;
    /// <summary>中文含义：inputUpdateRealtime 表示“输入更新真实时间”；inputUpdateGameTime 表示“输入更新游戏时间”。使用位置：LeonInputBridge.CaptureInputClock()、LeonInputBridge.ConvertInputTime(double)。；使用位置：LeonInputBridge.CaptureInputClock()、LeonInputBridge.ConvertInputTime(double)。</summary>
    private double inputUpdateRealtime, inputUpdateGameTime;
    /// <summary>中文含义：输入更新时间缩放。使用位置：LeonInputBridge.CaptureInputClock()、LeonInputBridge.ConvertInputTime(double)。</summary>
    private float inputUpdateTimeScale;
    /// <summary>中文含义：是否等待场景开放。使用位置：LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    [Header("本局启动")]
    [UnityEngine.Serialization.FormerlySerializedAs("等待场景开放")]
    [UnityEngine.InspectorName("等待场景开放")]
    public bool waitingSceneOpenRelease;
    /// <summary>中文含义：输入是否就绪。使用位置：SceneStartupEntry.Start()。</summary>
    internal bool InputReady => started;

    /// <summary>中文含义：是否已订阅打开输入的释放事件。使用位置：LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.EnableAndBindActions()、LeonInputBridge.SetGameplayInputEnabled(bool) 等共 4 处。</summary>
    private bool hasOpenRelease;
    /// <summary>SetSceneOpenRelease 的业务处理入口；本方法直接依赖 LeonInputBridge.SetGameplayInputEnabled(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 SceneStartupEntry.OnDisable()、SceneStartupEntry.Start()；下游（含内部回调体） LeonInputBridge.SetGameplayInputEnabled(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="openRelease">中文含义：是否打开释放。使用位置：作为 LeonInputBridge.SetGameplayInputEnabled(bool) 的参数、LeonInputBridge.SetSceneOpenRelease(bool)。</param>
    internal void SetSceneOpenRelease(bool openRelease)
    {
        hasOpenRelease = openRelease;
        SetGameplayInputEnabled(openRelease);
    }

    /// <summary>中文含义：是否已开始。使用位置：LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.InputReady.get、LeonInputBridge.IsQTEInputReady.get 等共 5 处。</summary>
    private bool started;
    /// <summary>中文含义：玩家映射之前是否启用。使用位置：LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath() 等共 4 处。</summary>
    private bool playerMapWasEnabled;
    /// <summary>中文含义：生效配对动作会话编号。使用位置：作为 LeonQTECoordinator.SubmitEscapePress(int) 的参数、LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.EnterQTEInput(int) 等共 8 处。</summary>
    private int activeQteSessionId = -1;
    /// <summary>中文含义：是否持有输入资源。使用位置：LeonInputBridge.OnDestroy()、LeonInputBridge.Start()。</summary>
    private bool ownsInputAsset;
    /// <summary>中文含义：死亡输入是否锁定。使用位置：LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.CanStartWeaponWheel()、LeonInputBridge.EnableAndBindActions() 等共 8 处。</summary>
    private bool deathInputLocked;
    /// <summary>中文含义：输入许可。使用位置：LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int)、LeonInputBridge.LockForDeath() 等共 4 处。</summary>
    private GameplayPermissionService inputPermission;
    /// <summary>中文含义：pairedActionInputPermission 表示“已配对动作输入许可”；deathInputPermission 表示“死亡输入许可”。使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数、LeonInputBridge.EnterQTEInput(int)、LeonInputBridge.ExitQTEInput(int) 等共 5 处。；使用位置：作为 GameplayPermissionService.Release(GameplayPermissionHandle) 的参数、LeonInputBridge.LockForDeath()、LeonInputBridge.OnDisable()。</summary>
    private GameArchitecture.GameplayPermissionHandle pairedActionInputPermission, deathInputPermission;
    /// <summary>中文含义：是否具有已报告移动输入。使用位置：LeonInputBridge.OnMove(InputAction.CallbackContext)。</summary>
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
    /// <summary>中文含义：武器轮盘是否打开。使用位置：LeonInputBridge.AllowFreeViewInput.get、LeonInputBridge.CanStartWeaponWheel()、LeonInputBridge.CloseWeaponWheel(bool) 等共 16 处。</summary>
    private bool weaponWheelOpen;
    /// <summary>中文含义：是否允许自由视图输入。使用位置：LeonController.AllowFreeObserve.get。</summary>
    public bool AllowFreeViewInput => isActiveAndEnabled && !deathInputLocked && activeQteSessionId <= 0 && !weaponWheelOpen && (!waitingSceneOpenRelease || hasOpenRelease) && freeLookAction != null && freeLookAction.enabled;

    /// <summary>中文含义：自由视图键实际是否保持按住。使用位置：作为 LeonController.SetFreeLooking(bool) 的参数、LeonInputBridge.OnFreeLookEnd(InputAction.CallbackContext)。</summary>
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

    /// <summary>中文含义：武器轮盘自动确认时间。使用位置：作为 Mathf.Max(float, float) 的参数、LeonInputBridge.HandleScrollWeaponWheel()、LeonInputBridge.UpdateScrollWeaponWheelAutoConfirm()。</summary>
    private float weaponWheelAutoConfirmTime;
    /// <summary>中文含义：下一次武器轮盘滚动打开时间。使用位置：LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.HandleScrollWeaponWheel()。</summary>
    private float nextWeaponWheelScrollOpenTime;
    // 瞄准模式：0=长按，1=切换
    /// <summary>中文含义：瞄准模式。使用位置：LeonInputBridge.OnAim(InputAction.CallbackContext)、LeonInputBridge.Start()、LeonInputBridge.SyncAimAfterWeaponWheel()。</summary>
    private int aimMode;
    /// <summary>中文含义：瞄准切换是否生效。使用位置：作为 LeonCombat.HandleAim(bool) 的参数、LeonInputBridge.LockForDeath()、LeonInputBridge.OnAim(InputAction.CallbackContext) 等共 5 处。</summary>
    private bool aimToggleActive;
    /// <summary>中文含义：玩家已移动时的通知回调。事件通信成员 OnPlayerMoved，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：LeonInputBridge.OnMove(InputAction.CallbackContext)、TutorialManager.BindEvents()、TutorialManager.UnbindEvents()。</summary>
    public event System.Action OnPlayerMoved;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;InputActionAsset&gt;(string)、Component.GetComponent&lt;LeonCombat&gt;()、Component.GetComponent&lt;LeonController&gt;()、Component.GetComponent&lt;LeonQTECoordinator&gt;()、Component.GetComponent&lt;WeaponWheelUI&gt;()、Debug.LogError(object)、GameObject.AddComponent&lt;WeaponWheelUI&gt;() 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        controller = GetComponent<LeonController>();
        movementDriver = GetComponent<LeonActionMotionDriver>();
        actions = GetComponent<LeonAnimationActionCoordinator>();
        combat = GetComponent<LeonCombat>();
        qteCoordinator = GetComponent<LeonQTECoordinator>();
        weaponWheel = GetComponent<WeaponWheelUI>();
        if (weaponWheel == null)
        {
            weaponWheel = gameObject.AddComponent<WeaponWheelUI>();
        }

        // 运行时使用独立副本，避免与界面输入模块共享启用状态。
        // sourceAsset（来源资源）：使用位置：作为 Object.Instantiate<InputActionAsset>(InputActionAsset) 的参数、LeonInputBridge.Start()。
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
        // uiMap（界面映射）：保存 InputActionAsset.FindActionMap(string, bool) 的返回结果，供当前步骤使用。使用位置：LeonInputBridge.Start()。
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
    /// <param name="enabled">中文含义：是否启用。使用位置：LeonInputBridge.SetGameplayInputEnabled(bool)。</param>
    public void SetGameplayInputEnabled(bool enabled)
    {
        if (!enabled)
        {
            // receiverValue1（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
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
            // receiverValue2（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
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
            // receiverValue3（判空前缓存的移动动作）：从移动动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue3 = // 只禁用会吞噬鼠标的动作，保留 Inventory/Sort/Reload(旋转)等
 moveAction;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.Disable();
            }

            // receiverValue4（判空前缓存的观察动作）：从观察动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue4 = lookAction;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.Disable();
            }

            // receiverValue5（判空前缓存的瞄准动作）：从瞄准动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue5 = aimAction;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                receiverValue5.Disable();
            }

            // receiverValue6（判空前缓存的射击动作）：从射击动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue6 = shootAction;
            if (!global::System.Object.ReferenceEquals(receiverValue6, null))
            {
                receiverValue6.Disable();
            }

            // receiverValue8（判空前缓存的招架动作）：从招架动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
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

            // receiverValue12（判空前缓存的蹲伏动作）：从蹲伏动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue12 = crouchAction;
            if (!global::System.Object.ReferenceEquals(receiverValue12, null))
            {
                receiverValue12.Disable();
            }

            // receiverValue13（判空前缓存的自由环顾动作）：从自由环顾动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
global::UnityEngine.InputSystem.InputAction receiverValue13 = freeLookAction;
            if (!global::System.Object.ReferenceEquals(receiverValue13, null))
            {
                receiverValue13.Disable();
            }

            // receiverValue14（判空前缓存的交互动作）：从交互动作读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.SetGameplayInputEnabled(bool)。
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

        // receiverValue15（判空前缓存的配对动作映射）：从配对动作映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnableAndBindActions()。
global::UnityEngine.InputSystem.InputActionMap receiverValue15 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.Disable();
        }

        // receiverValue16（判空前缓存的物品检视映射）：从物品检视映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnableAndBindActions()。
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
        movementInputAwaitingUpdate = false;
        ClearPickupInput();
        // receiverValue17（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::LeonCombat receiverValue17 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue17, null))
        {
            receiverValue17.HandleKnifeHold(false);
        }

        // receiverValue18（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::LeonController receiverValue18 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue18, null))
        {
            receiverValue18.ResetFreeView();
        }

        // receiverValue19（判空前缓存的物品检视映射）：从物品检视映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
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

        if (itemInspectionCloseAction != null)
        {
            itemInspectionCloseAction.performed -= OnItemInspectionClose;
        }

        playerMap.Disable();
        // receiverValue20（判空前缓存的配对动作映射）：从配对动作映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::UnityEngine.InputSystem.InputActionMap receiverValue20 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.Disable();
        }

        // receiverValue21（判空前缓存的物品检视映射）：从物品检视映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::UnityEngine.InputSystem.InputActionMap receiverValue21 = itemInspectionMap;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.Disable();
        }

        actionsBound = false;
        // receiverValue22（判空前缓存的输入许可）：从输入许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::GameplayPermissionService receiverValue22 = inputPermission;
        // optionalValue23（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonInputBridge.OnDisable()。
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
        // receiverValue24（判空前缓存的输入许可）：从输入许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
global::GameplayPermissionService receiverValue24 = inputPermission;
        // optionalValue25（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonInputBridge.OnDisable()。
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
            // receiverValue26（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnDisable()。
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
    /// <param name="closeCallback">中文含义：关闭回调。使用位置：LeonInputBridge.BeginItemInspectionInput(Action)。</param>
    public string BeginItemInspectionInput(System.Action closeCallback)
    {
        itemInspectionCloseCallback = closeCallback;
        if (itemInspectionMap == null || itemInspectionCloseAction == null)
        {
            return "F";
        }

        // interactPath（交互路径）：保存 LeonInputBridge.FindKeyboardInteractPath() 的返回结果，供当前步骤使用。使用位置：作为 InputAction.ApplyBindingOverride(int, string) 的参数、作为 string.IsNullOrEmpty(string) 的参数。
string interactPath = FindKeyboardInteractPath();
        itemInspectionCloseAction.RemoveBindingOverride(0);
        if (!string.IsNullOrEmpty(interactPath))
        {
            itemInspectionCloseAction.ApplyBindingOverride(0, interactPath);
        }

        itemInspectionMap.Enable();
        // displayName（显示名称）：保存 InputAction.GetBindingDisplayString(int, InputBinding.DisplayStringOptions) 的返回结果，供当前步骤使用。使用位置：作为 string.IsNullOrWhiteSpace(string) 的参数、LeonInputBridge.BeginItemInspectionInput(Action)。
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
        // receiverValue28（判空前缓存的物品检视映射）：从物品检视映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EndItemInspectionInput()。
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

        // i（当前遍历索引）：使用位置：作为 ReadOnlyArray<InputBinding>.this[int] 的参数、LeonInputBridge.FindKeyboardInteractPath()。
for (int i = 0; i < interactAction.bindings.Count; i++)
        {
            // binding（绑定）：使用位置：LeonInputBridge.FindKeyboardInteractPath()。
InputBinding binding = interactAction.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            // effectivePath（实际生效的路径）：使用位置：作为 string.IsNullOrEmpty(string) 的参数、LeonInputBridge.FindKeyboardInteractPath()。
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
    /// <param name="context">中文含义：上下文。使用位置：LeonInputBridge.OnItemInspectionClose(InputAction.CallbackContext)。</param>
    void OnItemInspectionClose(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // receiverValue29（判空前缓存的物品检视关闭回调）：从物品检视关闭回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnItemInspectionClose(InputAction.CallbackContext)。
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
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonInputBridge.EnterQTEInput(int)。</param>
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

        // receiverValue31（判空前缓存的玩家映射）：从玩家映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::UnityEngine.InputSystem.InputActionMap receiverValue31 = playerMap;
        if (!global::System.Object.ReferenceEquals(receiverValue31, null))
        {
            receiverValue31.Disable();
        }

        // receiverValue32（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::LeonController receiverValue32 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue32, null))
        {
            receiverValue32.SetMoveInput(Vector3.zero);
        }

        // receiverValue33（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::LeonController receiverValue33 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.SetLookInput(Vector2.zero);
        }

        // receiverValue34（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::LeonController receiverValue34 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.SetSprinting(false);
        }

        // receiverValue35（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::LeonController receiverValue35 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue35, null))
        {
            receiverValue35.SetFreeLooking(false);
        }

        // receiverValue36（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.EnterQTEInput(int)。
global::LeonCombat receiverValue36 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue36, null))
        {
            receiverValue36.InterruptForQTE();
        }

        qteMap.Enable();
        return true;
    }

    /// <summary>ExitQTEInput 的业务处理入口；本方法直接依赖 GameplayPermissionService.Release(GameplayPermissionHandle)、InputAction.IsPressed()、InputAction.ReadValue&lt;Vector2&gt;()、InputActionMap.Disable()、InputActionMap.Enable()、LeonController.SetMoveInput(Vector3)、LeonController.SetSprinting(bool) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonQTECoordinator.ReleasePlayer(int)；下游（含内部回调体） GameplayPermissionService.Release(GameplayPermissionHandle)、InputAction.IsPressed()、InputAction.ReadValue&lt;Vector2&gt;()、InputActionMap.Disable()、InputActionMap.Enable()、LeonController.SetMoveInput(Vector3)、LeonController.SetSprinting(bool) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sessionId">中文含义：会话编号。使用位置：LeonInputBridge.ExitQTEInput(int)。</param>
    public void ExitQTEInput(int sessionId)
    {
        if (sessionId <= 0 || activeQteSessionId != sessionId)
        {
            return;
        }

        // receiverValue37（判空前缓存的配对动作映射）：从配对动作映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.ExitQTEInput(int)。
global::UnityEngine.InputSystem.InputActionMap receiverValue37 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.Disable();
        }

        activeQteSessionId = -1;
        // receiverValue38（判空前缓存的输入许可）：从输入许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.ExitQTEInput(int)。
global::GameplayPermissionService receiverValue38 = inputPermission;
        // optionalValue39（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonInputBridge.ExitQTEInput(int)。
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
            RequestMovementInputRefresh();
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
        playerMap.Enable();
        if (movementWasDisabled || sprintWasDisabled)
        {
            movementInputAwaitingUpdate = true;
            RequestMovementInputRefresh();
        }
    }

    internal void RequestMovementInputRefresh()
    {
        movementInputRefreshPending = true;
    }

    internal void RestoreMovementInput()
    {
        if (movementInputAwaitingUpdate || !isActiveAndEnabled || weaponWheelOpen ||
            deathInputLocked || activeQteSessionId > 0 || (waitingSceneOpenRelease && !hasOpenRelease))
        {
            return;
        }

        if (controller == null || (!controller.CanAct() && !(controller.currentState == LeonState.Staggered && controller.AnimDriver != null && controller.AnimDriver.HitMovementReleased)) || moveAction == null || !moveAction.enabled ||
            sprintAction == null || !sprintAction.enabled)
        {
            return;
        }

        if (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
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

        if (GameUIManager.HasInstance && GameUIManager.Instance.IsPlayerInputBlocked)
        {
            return;
        }

        Vector2 movement = moveAction.ReadValue<Vector2>();
        Vector3 heldMovement = new Vector3(movement.x, 0f, movement.y);
        if (movementInputRefreshPending || controller.MoveInput != heldMovement)
        {
            controller.SetMoveInput(heldMovement);
        }

        movementInputRefreshPending = false;
        controller.SetSprinting(sprintAction.IsPressed());
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
        // receiverValue42（判空前缓存的输入许可）：从输入许可读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::GameplayPermissionService receiverValue42 = inputPermission;
        // optionalValue43（条件访问得到的释放的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonInputBridge.LockForDeath()。
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
        // receiverValue44（判空前缓存的玩家映射）：从玩家映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::UnityEngine.InputSystem.InputActionMap receiverValue44 = playerMap;
        if (!global::System.Object.ReferenceEquals(receiverValue44, null))
        {
            receiverValue44.Disable();
        }

        // receiverValue45（判空前缓存的配对动作映射）：从配对动作映射读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::UnityEngine.InputSystem.InputActionMap receiverValue45 = qteMap;
        if (!global::System.Object.ReferenceEquals(receiverValue45, null))
        {
            receiverValue45.Disable();
        }

        // receiverValue46（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::LeonController receiverValue46 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue46, null))
        {
            receiverValue46.SetMoveInput(Vector3.zero);
        }

        // receiverValue47（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::LeonController receiverValue47 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue47, null))
        {
            receiverValue47.SetLookInput(Vector2.zero);
        }

        // receiverValue48（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::LeonController receiverValue48 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue48, null))
        {
            receiverValue48.SetSprinting(false);
        }

        // receiverValue49（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::LeonController receiverValue49 = controller;
        if (!global::System.Object.ReferenceEquals(receiverValue49, null))
        {
            receiverValue49.SetFreeLooking(false);
        }

        aimToggleActive = false;
        // receiverValue50（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
global::LeonCombat receiverValue50 = combat;
        if (!global::System.Object.ReferenceEquals(receiverValue50, null))
        {
            receiverValue50.HandleAim(false);
        }

        // receiverValue51（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.LockForDeath()。
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
        ReplayPickupInput();
        ReplayGameplayIntents();
        // 动作映射禁用后仍读取真实按键，防止取消回调冒充松键。
        if (Application.isFocused)
        {
            // receiverValue52（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.Update()。
global::LeonController receiverValue52 = controller;
            if (!global::System.Object.ReferenceEquals(receiverValue52, null))
            {
                receiverValue52.SetFreeLooking(FreeViewKeyActualHeld);
            }
        }

        if (!CanUseQuickSelect())
        {
            ClearGameplayIntents();
            CloseWeaponWheel(false);
            return;
        }

        HandleScrollWeaponWheel();
        UpdateScrollWeaponWheelAutoConfirm();
        // receiverValue53（判空前缓存的界面光标管理器的实例）：从界面光标管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.Update()。
global::UICursorManager receiverValue53 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue53, null))
        {
            receiverValue53.HideCustomCursor();
        }
    }

    private bool CanUsePickupInput()
    {
        return controller != null && Application.isFocused && !weaponWheelOpen && !deathInputLocked
            && activeQteSessionId <= 0 && (!waitingSceneOpenRelease || hasOpenRelease)
            && (!GameManager.HasInstance || GameManager.Instance.Permission.Current.AllowInput)
            && (!GameUIManager.HasInstance || !GameUIManager.Instance.IsPlayerInputBlocked)
            && (!InventorySceneLoader.HasInstance || !InventorySceneLoader.Instance.IsVisible && !InventorySceneLoader.Instance.IsLoading)
            && (StorageBoxUI.Instance == null || !StorageBoxUI.Instance.IsVisible);
    }

    private bool BufferPickupInput(PickupInput action)
    {
        if (actions == null || !actions.IsItemPickupActive)
        {
            return false;
        }

        if (CanUsePickupInput())
        {
            int transactionId = actions.CurrentFullBodyTransactionId;
            if (pickupInputTransaction != transactionId)
            {
                ClearPickupInput();
                pickupInputTransaction = transactionId;
            }

            if (action != PickupInput.None)
            {
                pendingPickupAction = action;
            }
        }
        return true;
    }

    internal bool HasBufferedPickupInput(int transactionId)
    {
        return pickupInputTransaction == transactionId && (pendingPickupAim || pendingPickupAction != PickupInput.None);
    }

    internal void ReleaseBufferedPickupInput(int transactionId)
    {
        if (HasBufferedPickupInput(transactionId))
        {
            pickupInputReleased = true;
        }
    }

    internal void DiscardPendingPickupInput(int transactionId)
    {
        if (pickupInputTransaction == transactionId)
        {
            ClearPickupInput();
        }
    }

    private void ClearPickupInput()
    {
        pickupInputTransaction = 0;
        pickupInputReleased = false;
        pendingPickupAim = false;
        pendingPickupAction = PickupInput.None;
    }

    private void ReplayPickupInput()
    {
        if (pickupInputTransaction == 0)
        {
            return;
        }
        if (!CanUsePickupInput())
        {
            ClearPickupInput();
            return;
        }
        if (!pickupInputReleased)
        {
            return;
        }

        PickupInput action = pendingPickupAction;
        bool aim = pendingPickupAim && aimAction != null && aimAction.enabled &&
            (aimMode == 1 && aimToggleActive || aimMode != 1 && aimAction.IsPressed());
        ClearPickupInput();
        if (!controller.CanAct() || actions == null || actions.HasActiveFullBodyAction)
        {
            return;
        }

        if (aim && combat != null)
        {
            combat.HandleAim(true);
        }
        if (action == PickupInput.Interact && interactAction != null && interactAction.enabled)
        {
            TryInteract();
        }
        else if (action == PickupInput.Shoot && combat != null && shootAction != null && shootAction.enabled)
        {
            combat.CommitAttackInput(Time.timeAsDouble);
        }
        else if (action == PickupInput.Reload && combat != null && reloadAction != null && reloadAction.enabled)
        {
            combat.HandleReload();
        }
    }

    private bool BufferPickupAim(bool aim)
    {
        if (!BufferPickupInput(PickupInput.None))
        {
            return false;
        }

        if (pickupInputTransaction != 0 && !pickupInputReleased)
        {
            pendingPickupAim = aim;
        }
        return true;
    }

    /// <summary>OnMove 的业务处理入口；本方法直接依赖 Action.Invoke()、InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetMoveInput(Vector3)、object.ReferenceEquals(object, object)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Action.Invoke()、InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetMoveInput(Vector3)、object.ReferenceEquals(object, object)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：LeonInputBridge.OnMove(InputAction.CallbackContext)。</param>
    void OnMove(InputAction.CallbackContext ctx)
    {
        if (InventorySceneLoader.Instance != null && InventorySceneLoader.Instance.IsVisible)
        {
            return;
        }

        // v（读取值的返回结果）：保存 InputAction.CallbackContext.ReadValue<Vector2>() 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、LeonInputBridge.OnMove(InputAction.CallbackContext)。
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
        // receiverValue54（判空前缓存的玩家已移动时的通知回调）：从玩家已移动时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnMove(InputAction.CallbackContext)。
global::System.Action receiverValue54 = OnPlayerMoved;
        if (!global::System.Object.ReferenceEquals(receiverValue54, null))
        {
            receiverValue54.Invoke();
        }
    }

    /// <summary>OnLook 的业务处理入口；本方法直接依赖 InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetLookInput(Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InputAction.CallbackContext.ReadValue&lt;Vector2&gt;()、LeonController.SetLookInput(Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：作为 LeonController.SetLookInput(Vector2) 的参数。</param>
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
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：作为 LeonCombat.HandleAim(bool) 的参数、LeonInputBridge.OnAim(InputAction.CallbackContext)。</param>
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
        if (aimMode == 1)
        {
            // 切换模式：仅 performed 时切换状态
            if (ctx.performed)
            {
                aimToggleActive = !aimToggleActive;
                if (BufferPickupAim(aimToggleActive))
                {
                    return;
                }
                // receiverValue55（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnAim(InputAction.CallbackContext)。
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
            if (BufferPickupAim(ctx.ReadValueAsButton()))
            {
                return;
            }
            // receiverValue56（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnAim(InputAction.CallbackContext)。
global::LeonCombat receiverValue56 = // 长按模式：performed=true, canceled=false
 combat;
            if (!global::System.Object.ReferenceEquals(receiverValue56, null))
            {
                receiverValue56.HandleAim(ctx.ReadValueAsButton());
            }
        }
    }

    /// <summary>OnShoot 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackInput(double)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.CommitAttackInput(double)、LeonInputBridge.CloseWeaponWheel(bool)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：作为 LeonInputBridge.ConvertInputTime(double) 的参数。</param>
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
        if (!BufferPickupInput(PickupInput.Shoot) && CanUsePickupInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Shoot, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnShootReleased 的业务处理入口；本方法直接依赖 LeonCombat.CommitAttackReleased(double)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonCombat.CommitAttackReleased(double)、LeonInputBridge.ConvertInputTime(double)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：作为 LeonInputBridge.ConvertInputTime(double) 的参数。</param>
    void OnShootReleased(InputAction.CallbackContext ctx)
    {
        if (weaponWheelOpen)
        {
            return;
        }

        // receiverValue58（判空前缓存的战斗）：从战斗读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnShootReleased(InputAction.CallbackContext)。
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
            // receiverValue59（判空前缓存的背包界面的实例）：从背包界面的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnReload(InputAction.CallbackContext)。
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

        if (BufferPickupInput(PickupInput.Reload))
        {
            return;
        }

        if (CanUsePickupInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Reload, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>CaptureInputClock 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CaptureInputClock()
    {
        var updateType = UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType;
        if (updateType == InputUpdateType.Dynamic || updateType == InputUpdateType.Fixed ||
            updateType == InputUpdateType.Manual)
        {
            // 重新启用发生在输入回调中时，要等下一批输入完成初始状态检查。
            movementInputAwaitingUpdate = false;
        }

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
    /// <param name="originalTime">中文含义：原始时间。使用位置：LeonInputBridge.ConvertInputTime(double)。</param>
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
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：作为 LeonInputBridge.ConvertInputTime(double) 的参数、LeonInputBridge.OnParry(InputAction.CallbackContext)。</param>
    internal bool CanReadParryInput => isActiveAndEnabled && parryAction != null && parryAction.enabled && CanUsePickupInput();
    internal bool IsParryInputHeld => parryAction != null && parryAction.IsPressed();

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
        if (!ctx.performed || !CanUsePickupInput())
        {
            return;
        }
        if (controller != null && controller.IsCrouching && !controller.TrySetCrouching(false))
        {
            restoreHeldGuard = true;
            return;
        }
        if (qteCoordinator != null && qteCoordinator.TrySubmitEmergencyDefense())
        {
            return;
        }

        restoreHeldGuard = true;
        if (combat != null)
        {
            combat.CommitGuardInput(true, ConvertInputTime(ctx.time));
        }
    }

    /// <summary>OnSwitchWeapon 的业务处理入口；本方法直接依赖 Array.IndexOf&lt;WeaponType&gt;(WeaponType[], WeaponType)、Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、Mathf.Max(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Array.IndexOf&lt;WeaponType&gt;(WeaponType[], WeaponType)、Component.GetComponent&lt;LeonWeaponHandler&gt;()、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponHandler.QuickSelectSwitch(WeaponType)、Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：LeonInputBridge.OnSwitchWeapon(InputAction.CallbackContext)。</param>
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
        if (CanUsePickupInput())
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
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：LeonInputBridge.OnCrouch(InputAction.CallbackContext)。</param>
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

        // receiverValue64（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnCrouch(InputAction.CallbackContext)。
global::LeonController receiverValue64 = controller;
        // optionalValue65（条件访问得到的切换蹲伏的返回结果）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：LeonInputBridge.OnCrouch(InputAction.CallbackContext)。
bool? optionalValue65;
        if (!global::System.Object.ReferenceEquals(receiverValue64, null))
        {
            optionalValue65 = receiverValue64.ToggleCrouch();
        }
        else
        {
            optionalValue65 = default;
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
            // receiverValue66（判空前缓存的里昂玩家控制器）：从控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnFreeLookEnd(InputAction.CallbackContext)。
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

        // loader（加载器）：使用位置：LeonInputBridge.OnInventoryToggle(InputAction.CallbackContext)。
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
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：LeonInputBridge.OnInteract(InputAction.CallbackContext)。</param>
    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && !BufferPickupInput(PickupInput.Interact) && CanUsePickupInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Interact, ConvertInputTime(ctx.time));
        }
    }

    private void TryInteract()
    {
        if (CanUsePickupInput())
        {
            DispatchGameplayIntent(GameplayIntentKind.Interact, Time.timeAsDouble);
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
            // receiverValue67（判空前缓存的背包管理器的实例）：从背包管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、LeonInputBridge.OnSort(InputAction.CallbackContext)。
global::InventoryManager receiverValue67 = InventoryManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue67, null))
            {
                receiverValue67.Sort();
            }
        }
    }

    /// <summary>OnEscapePress 的业务处理入口；本方法直接依赖 LeonQTECoordinator.SubmitEscapePress(int)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonQTECoordinator.SubmitEscapePress(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ctx">中文含义：当前回调上下文。使用位置：LeonInputBridge.OnEscapePress(InputAction.CallbackContext)。</param>
    internal bool IsEscapeHeld(int session)
    {
        return session > 0 && session == activeQteSessionId && escapePressAction != null &&
            escapePressAction.enabled && escapePressAction.IsPressed();
    }

    void OnEscapePress(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && activeQteSessionId > 0 && qteCoordinator != null)
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
    /// <param name="confirm">中文含义：是否确认。使用位置：LeonInputBridge.CloseWeaponWheel(bool)。</param>
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
