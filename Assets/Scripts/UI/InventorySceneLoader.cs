using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 背包场景加载器 — 在游戏场景中常驻，管理背包附加场景的首次加载与复用。
/// 打开背包：首次加载背包场景，后续直接重新显示。
/// 关闭背包：隐藏背包界面和相机，立即恢复游戏画面，不在游玩过程中卸载场景。
/// </summary>
public class InventorySceneLoader : Singleton<InventorySceneLoader>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：背包场景名称。使用位置：作为 Application.CanStreamedLevelBeLoaded(string) 的参数、作为 Debug.LogError(object) 的参数、作为 SceneManager.GetSceneByName(string) 的参数 等共 5 处。</summary>
    [Header("场景")]
    public string inventorySceneName = "InventoryScene";
    /// <summary>中文含义：游戏画布。使用位置：InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()、InventorySceneLoader.Start() 等共 5 处。</summary>
    [Header("游戏场景引用")]
    public Canvas gameCanvas;
    /// <summary>中文含义：主摄像机。使用位置：InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()、InventorySceneLoader.Start() 等共 4 处。</summary>
    public Camera mainCamera;
    /// <summary>中文含义：是否加载。使用位置：InventorySceneLoader.Hide()、InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.IsLoading.get 等共 6 处。</summary>
    private bool isLoading;
    /// <summary>中文含义：关闭是否已经申请。使用位置：InventorySceneLoader.Hide()、InventorySceneLoader.LoadInventoryScene()、InventorySceneLoader.Show() 等共 4 处。</summary>
    private bool closeRequested;
    /// <summary>中文含义：是否关闭中。使用位置：InventorySceneLoader.Hide()、InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene() 等共 4 处。</summary>
    private bool isClosing;
    // === Crosshair 生命周期事件（additive）===
    /// <summary>中文含义：生命周期变化时的通知回调。事件通信成员 OnLifecycleChanged，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()。</summary>
    public event System.Action<CrosshairLifecycleEvent> OnLifecycleChanged;
    /// <summary>中文含义：是否可见。使用位置：CombatHUD.IsCombatVisualBlocked()、DoorController.IsPlayerBlocked()、InventorySceneLoader.Hide() 等共 22 处。</summary>
    public bool IsVisible { get; private set; }
    /// <summary>中文含义：是否加载。使用位置：CombatHUD.IsCombatVisualBlocked()、DoorController.IsPlayerBlocked()、LeonInputBridge.CanStartWeaponWheel() 等共 9 处。</summary>
    public bool IsLoading => isLoading;

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (mainCamera == null)
        {
            Debug.LogError("背包加载器缺少主相机引用", this);
        }

        if (gameCanvas == null)
        {
            Debug.LogError("[背包加载器] 未设置游戏主画布引用，请在检查器中指定。", this);
        }
    }

    /// <summary>Toggle 的业务处理入口；本方法直接依赖 InventorySceneLoader.Hide()、InventorySceneLoader.Show()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventorySceneLoader.Hide()、InventorySceneLoader.Show()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Toggle()
    {
        if (isLoading)
        {
            if (!isClosing)
            {
                closeRequested = true;
            }

            return;
        }

        if (StorageBoxController.IsAnyBoxBusy)
        {
            return;
        }

        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>Show 的业务处理入口；本方法直接依赖 InventorySceneLoader.LoadInventoryScene()、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.Toggle()、LeonInputBridge.OnInventoryToggle(InputAction.CallbackContext)、PerformanceSamplingEntry.Tick()；下游（含内部回调体） InventorySceneLoader.LoadInventoryScene()、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Show()
    {
        if (VillageBellSequence.IsPresenting || IsVisible || isLoading || StorageBoxController.IsAnyBoxBusy)
        {
            return;
        }

        isLoading = true;
        closeRequested = false;
        StartCoroutine(LoadInventoryScene());
    }

    /// <summary>Hide 的业务处理入口；本方法直接依赖 InventorySceneLoader.HideInventoryScene()、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameUIManager.HandleEscPress()、InventorySceneLoader.LoadInventoryScene()、InventorySceneLoader.Toggle()、LeonInputBridge.OnInventoryToggle(InputAction.CallbackContext)；下游（含内部回调体） InventorySceneLoader.HideInventoryScene()、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private WeaponType? equipOnClose;

    internal void EquipAndClose(WeaponType weapon)
    {
        if (!IsVisible || isLoading || VillageBellSequence.IsPresenting)
        {
            return;
        }
        equipOnClose = weapon;
        Hide();
    }

    public void Hide()
    {
        if (isLoading)
        {
            if (!isClosing)
            {
                closeRequested = true;
            }

            return;
        }

        if (!IsVisible)
        {
            return;
        }

        isClosing = true;
        isLoading = true;
        AudioManager.PlayInterface(AudioEventNames.UICaseClose);
        StartCoroutine(HideInventoryScene());
    }

    /// <summary>LoadInventoryScene 的业务处理入口；本方法直接依赖 Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、Application.CanStreamedLevelBeLoaded(string)、AttacheCasePresentationController.ImmediateHide()、Debug.LogError(object, Object)、Debug.LogError(object)、Debug.LogException(Exception, Object)、GameObject.SetActive(bool) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.Show()；下游（含内部回调体） Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、Application.CanStreamedLevelBeLoaded(string)、AttacheCasePresentationController.ImmediateHide()、Debug.LogError(object, Object)、Debug.LogError(object)、Debug.LogException(Exception, Object)、GameObject.SetActive(bool) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator LoadInventoryScene()
    {
        // 先登记状态，暂停、光标和输入阻断由统一中枢处理。
        // receiverValue1（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::GameUIManager receiverValue1 = // 先登记状态，暂停、光标和输入阻断由统一中枢处理。
 GameUIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.ShowInventory();
        }

        // inventoryScene（背包场景）：保存 SceneManager.GetSceneByName(string) 的返回结果，供当前步骤使用。使用位置：InventorySceneLoader.LoadInventoryScene()。
Scene inventoryScene = SceneManager.GetSceneByName(inventorySceneName);
        // sceneAlreadyLoaded（是否场景已经已加载）：使用位置：InventorySceneLoader.LoadInventoryScene()。
bool sceneAlreadyLoaded = inventoryScene.IsValid() && inventoryScene.isLoaded;
        if (!sceneAlreadyLoaded && !Application.CanStreamedLevelBeLoaded(inventorySceneName))
        {
            Debug.LogError("[背包界面] 场景未加入构建设置：" + inventorySceneName);
            // receiverValue2（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::GameUIManager receiverValue2 = GameUIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.HideInventory();
            }

            isLoading = false;
            yield break;
        }

        // 只在本局第一次打开时加载；关闭后保留场景，后续打开不会产生加载延迟。
        if (!sceneAlreadyLoaded)
        {
            // op（操作）：保存 SceneManager.LoadSceneAsync(string, LoadSceneMode) 的返回结果，供当前步骤使用。使用位置：InventorySceneLoader.LoadInventoryScene()。
var op = SceneManager.LoadSceneAsync(inventorySceneName, LoadSceneMode.Additive);
            if (op != null)
            {
                yield return op;
            }
        }

        // 连接 InventoryUI 的相机引用
        // invUi（背包界面）：使用位置：InventorySceneLoader.LoadInventoryScene()。
        inventoryScene = SceneManager.GetSceneByName(inventorySceneName);
        InventoryUI invUi = null;
        if (inventoryScene.IsValid() && inventoryScene.isLoaded)
        {
            foreach (GameObject root in inventoryScene.GetRootGameObjects())
            {
                invUi = root.GetComponentInChildren<InventoryUI>(true);
                if (invUi != null)
                {
                    break;
                }
            }
        }
        if (invUi == null)
        {
            Debug.LogError("[背包界面] 背包场景中未找到界面控制器。", this);
            // receiverValue3（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::GameUIManager receiverValue3 = GameUIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue3, null))
            {
                receiverValue3.HideInventory();
            }

            isLoading = false;
            yield break;
        }

        invUi.mainCamera = mainCamera;
        invUi.inventoryCamera = FindInventoryCamera();
        DisableInventoryEventSystems();
        // presentationFailure（是否表现失败）：使用位置：InventorySceneLoader.LoadInventoryScene()。
bool presentationFailure = false;
        try
        {
            invUi.OnSceneLoaded();
        }
        catch (System.Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
        {
            presentationFailure = true;
            Debug.LogException(exception, this);
        }

        if (presentationFailure)
        {
            // receiverValue4（判空前缓存的背包界面的公文包与物品的三维展示组件）：从背包界面的公文包与物品的三维展示组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::AttacheCasePresentationController receiverValue4 = invUi.threeDimensionalPresentation;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.ImmediateHide();
            }

            if (invUi.inventoryPanel != null)
            {
                invUi.inventoryPanel.gameObject.SetActive(false);
            }

            if (invUi.inventoryCamera != null)
            {
                invUi.inventoryCamera.enabled = false;
            }

            if (mainCamera != null)
            {
                mainCamera.enabled = true;
            }

            if (gameCanvas != null)
            {
                gameCanvas.enabled = true;
            }

            isLoading = false;
            isClosing = false;
            closeRequested = false;
            IsVisible = false;
            // receiverValue5（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::GameUIManager receiverValue5 = GameUIManager.Instance;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                receiverValue5.HideInventory();
            }

            yield break;
        }

        // 背包相机已激活，现在关闭游戏 HUD 和主相机
        if (gameCanvas != null)
        {
            gameCanvas.enabled = false;
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = false;
        }

        IsVisible = true;
        isLoading = false;
        AudioManager.PlayInterface(AudioEventNames.UICaseOpen);
        // receiverValue6（判空前缓存的生命周期变化时的通知回调）：从生命周期变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.LoadInventoryScene()。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue6 = OnLifecycleChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Invoke(CrosshairLifecycleEvent.InventoryOpened);
        }

        if (closeRequested)
        {
            closeRequested = false;
            Hide();
        }
    }

    /// <summary>HideInventoryScene 的业务处理入口；本方法直接依赖 Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、AttacheCasePresentationController.ImmediateHide()、Debug.LogWarning(object, Object)、GameUIManager.HideInventory()、InventorySceneLoader.FindInventoryCamera()、InventoryUI.PlayExitAnimation(Action)、Mathf.Max(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.Hide()；下游（含内部回调体） Action&lt;CrosshairLifecycleEvent&gt;.Invoke(CrosshairLifecycleEvent)、AttacheCasePresentationController.ImmediateHide()、Debug.LogWarning(object, Object)、GameUIManager.HideInventory()、InventorySceneLoader.FindInventoryCamera()、InventoryUI.PlayExitAnimation(Action)、Mathf.Max(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator HideInventoryScene()
    {
        // 先播放淡出动画，动画完成后卸载场景
        // invUi（背包界面）：使用位置：InventorySceneLoader.HideInventoryScene()。
var invUi = InventoryUI.Instance;
        if (invUi != null)
        {
            // animDone（是否动画完成）：使用位置：作为 InventoryUI.PlayExitAnimation(Action) 的参数、InventorySceneLoader.HideInventoryScene()。
bool animDone = false;
            invUi.PlayExitAnimation(() => animDone = true);
            // 使用未缩放时间设置保护期限，避免动画回调异常时永久卡在黑屏。
            // leftOperand10（计算前缓存的左侧引擎启动后经过的真实时间）：从引擎启动后经过的真实时间读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventorySceneLoader.HideInventoryScene()。
float leftOperand10 = Time.realtimeSinceStartup;
            // argumentValue9（按原顺序缓存的实参1.5f）：从1.5f读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float argumentValue9 = 1.5f;
            // leftOperand8（计算前缓存的左侧背包界面的界面淡入淡出的持续时间）：从背包界面的界面淡入淡出的持续时间读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Mathf.Max(float, float) 的参数。
float leftOperand8 = invUi.fadeDuration;
            // selectedValue7（分支选出的0f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Mathf.Max(float, float) 的参数、InventorySceneLoader.HideInventoryScene()。
float selectedValue7;
            if (invUi.threeDimensionalPresentation != null)
            {
                selectedValue7 = invUi.threeDimensionalPresentation.openDuration + invUi.threeDimensionalPresentation.closeDuration;
            }
            else
            {
                selectedValue7 = 0f;
            }

            // 使用未缩放时间设置保护期限，避免动画回调异常时永久卡在黑屏。
            // exitDeadline（退出截止时间）：使用位置：InventorySceneLoader.HideInventoryScene()。
float exitDeadline = leftOperand10 + Mathf.Max(argumentValue9, leftOperand8 + (selectedValue7) + 0.75f);
            while (!animDone && Time.realtimeSinceStartup < exitDeadline)
            {
                yield return null;
            }

            if (!animDone)
            {
                Debug.LogWarning("[背包界面] 淡出动画未按时结束，已强制恢复游戏画面。", this);
            }
        }

        if (invUi != null)
        {
            // receiverValue11（判空前缓存的背包界面的公文包与物品的三维展示组件）：从背包界面的公文包与物品的三维展示组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.HideInventoryScene()。
global::AttacheCasePresentationController receiverValue11 = invUi.threeDimensionalPresentation;
            if (!global::System.Object.ReferenceEquals(receiverValue11, null))
            {
                receiverValue11.ImmediateHide();
            }
        }

        isClosing = false;
        // 背包相机的深度高于主相机，并会清屏为黑色，必须先停用。
        // inventoryCamera（背包摄像机）：使用位置：InventorySceneLoader.HideInventoryScene()。
Camera inventoryCamera;
        if (invUi != null)
        {
            inventoryCamera = invUi.inventoryCamera;
        }
        else
        {
            inventoryCamera = FindInventoryCamera();
        }

        if (inventoryCamera != null)
        {
            inventoryCamera.enabled = false;
        }

        // 立即恢复游戏画面、时间和输入。
        if (gameCanvas != null)
        {
            gameCanvas.enabled = true;
        }

        if (mainCamera != null)
        {
            mainCamera.enabled = true;
        }

        IsVisible = false;
        isLoading = false;
        // receiverValue13（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.HideInventoryScene()。
global::GameUIManager receiverValue13 = GameUIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            receiverValue13.HideInventory();
        }

        WeaponType? selectedWeapon = equipOnClose;
        equipOnClose = null;
        if (selectedWeapon.HasValue && !VillageBellSequence.IsPresenting && LeonController.Instance != null)
        {
            LeonController.Instance.TrySwitchWeapon(selectedWeapon.Value);
        }

        // receiverValue14（判空前缓存的生命周期变化时的通知回调）：从生命周期变化时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventorySceneLoader.HideInventoryScene()。
global::System.Action<global::CrosshairLifecycleEvent> receiverValue14 = OnLifecycleChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            receiverValue14.Invoke(CrosshairLifecycleEvent.InventoryClosed);
        }
    }

    /// <summary>FindInventoryCamera 的业务处理入口；本方法直接依赖 Object.FindObjectsByType&lt;Camera&gt;(FindObjectsSortMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.HideInventoryScene()、InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） Object.FindObjectsByType&lt;Camera&gt;(FindObjectsSortMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    Camera FindInventoryCamera()
    {
        // cams（摄像机集合）：保存 Object.FindObjectsByType<Camera>(FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：InventorySceneLoader.FindInventoryCamera()。
var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        // c（当前摄像机）：每轮从摄像机集合取得一个元素，仅代表当前这一项。使用位置：InventorySceneLoader.FindInventoryCamera()。
foreach (var c in cams)
        {
            if (c.gameObject.name == "InventoryCamera")
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>DisableInventoryEventSystems 的业务处理入口；本方法直接依赖 Object.FindObjectsByType&lt;EventSystem&gt;(FindObjectsInactive, FindObjectsSortMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） Object.FindObjectsByType&lt;EventSystem&gt;(FindObjectsInactive, FindObjectsSortMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void DisableInventoryEventSystems()
    {
        // eventSystems（事件系统集合）：保存 Object.FindObjectsByType<EventSystem>(FindObjectsInactive, FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：InventorySceneLoader.DisableInventoryEventSystems()。
var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        // eventSystem（事件系统）：每轮从事件系统集合取得一个元素，仅代表当前这一项。使用位置：InventorySceneLoader.DisableInventoryEventSystems()。
foreach (var eventSystem in eventSystems)
        {
            if (eventSystem.gameObject.scene.name == inventorySceneName)
            {
                eventSystem.enabled = false;
            }
        }
    }
}
