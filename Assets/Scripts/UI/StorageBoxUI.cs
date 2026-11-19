using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 寄存箱 UI — 左侧箱子列表 + 右侧共享网格视图
/// 支持从背包拖入箱子和从箱子拖回背包
/// </summary>
/// <remarks>仓库与背包的存取界面，把选择和转移意图交给已有宿主及规则服务，完成后读取双方当前结果刷新显示。</remarks>
public class StorageBoxUI : MonoBehaviour
{
    /// <summary>中文含义：界面视觉主题配置。使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数、InterfaceReworkBuilder.ConfigurationOtherInterface()、StorageBoxUI.CreateStorageEntry(GridItem) 等共 4 处。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>中文含义：物品预览。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.Close()、StorageBoxUI.OnInventoryItemHoverEnter(GridItem) 等共 7 处。</summary>
    [Header("物品三维预览")]
    [UnityEngine.Serialization.FormerlySerializedAs("物品预览")]
    [UnityEngine.InspectorName("物品预览")]
    public StoredItemPreview itemPreview;
    /// <summary>中文含义：实例。使用位置：CombatHUD.IsCombatVisualBlocked()、DoorController.IsPlayerBlocked()、LeonInputBridge.CanStartWeaponWheel() 等共 13 处。</summary>
    public static StorageBoxUI Instance { get; private set; }

    /// <summary>中文含义：面板。使用位置：StorageBoxUI.FadeIn()、StorageBoxUI.FadeOut()、StorageBoxUI.Show(StorageBoxController) 等共 5 处。</summary>
    [Header("面板引用")]
    public CanvasGroup panel;
    /// <summary>中文含义：仓库列表容器。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、StorageBoxUI.CacheReusableStorageEntries()、StorageBoxUI.IsPointerOverStorageList(Vector2) 等共 4 处。</summary>
    public RectTransform storageListContainer;
    /// <summary>中文含义：背包网格容器。使用位置：StorageBoxUI.ResolveGridView()。</summary>
    public RectTransform inventoryGridContainer;
    /// <summary>中文含义：留空时自动使用箱子滚动区域。背包物品拖入箱子时使用的投放区域；留空时自动使用箱子滚动区域。使用位置：StorageBoxUI.IsPointerOverStorageList(Vector2)、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    [Tooltip("背包物品拖入箱子时使用的投放区域；留空时自动使用箱子滚动区域。")]
    public RectTransform storageDropArea;
    /// <summary>中文含义：标题文字。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    [Header("标题")]
    public Text titleText;
    /// <summary>中文含义：仓库标题文字。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    public Text storageTitleText;
    /// <summary>中文含义：背包标题文字。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    public Text inventoryTitleText;
    /// <summary>中文含义：操作提示栏文字。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、StorageBoxUI.Start()、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    [Header("提示")]
    public Text hintText;
    /// <summary>中文含义：界面淡入淡出的持续时间。使用位置：StorageBoxUI.FadeIn()、StorageBoxUI.FadeOut()。</summary>
    [Header("动画")]
    public float fadeDuration = 0.25f;
    /// <summary>中文含义：背包网格视图组件。右侧背包网格视图组件。使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数、作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、StorageBoxUI.ConfigureGridCallbacks() 等共 13 处。</summary>
    [Header("共享网格视图")]
    [Tooltip("右侧背包网格视图组件")]
    public InventoryGridView gridView;
    /// <summary>中文含义：左侧箱子区域投放高亮 Image。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、StorageBoxUI.ClearDragContext()、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2) 等共 6 处。</summary>
    [Header("投放高亮")]
    [Tooltip("左侧箱子区域投放高亮 Image")]
    public Image dropZoneHighlight;
    /// <summary>中文含义：投放高亮颜色。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2)、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    [Tooltip("投放高亮颜色")]
    public Color dropZoneColor = new Color(0.15f, 1f, 0.35f, 0.3f);
    /// <summary>中文含义：右侧背包区域投放高亮 Image。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、StorageBoxUI.ClearDragContext() 等共 4 处。</summary>
    [Tooltip("右侧背包区域投放高亮 Image")]
    public Image inventoryDropZoneHighlight;
    /// <summary>中文含义：右侧投放高亮颜色。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、InterfaceReworkBuilder.ConfigurationOtherInterface()、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</summary>
    [Tooltip("右侧投放高亮颜色")]
    public Color inventoryDropZoneColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    /// <summary>中文含义：操作失败提示文本。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、StorageBoxUI.FailHintRoutine(string)、StorageBoxUI.ShowFailHint(string) 等共 5 处。</summary>
    [Header("失败提示")]
    [Tooltip("操作失败提示文本")]
    public Text failHintText;
    /// <summary>中文含义：失败提示持续时间。使用位置：StorageBoxUI.FailHintRoutine(string)。</summary>
    public float failHintDuration = 1.5f;
    /// <summary>中文含义：拖拽时跟随光标的图标（留空则自动创建）。使用位置：StorageBoxUI.EnsureDragGhost()、StorageBoxUI.HideDragGhost()、StorageBoxUI.ShowDragGhost(GridItem)。</summary>
    [Header("拖拽幽灵图标")]
    [Tooltip("拖拽时跟随光标的图标（留空则自动创建）")]
    public Image dragGhost;
    /// <summary>中文含义：拖拽幽灵预览矩形变换。使用位置：StorageBoxUI.EnsureDragGhost()、StorageBoxUI.UpdateDragGhostPos(Vector2)。</summary>
    private RectTransform dragGhostRt;
    /// <summary>中文含义：是否可见。使用位置：StorageBoxUI.Close()、StorageBoxUI.IsVisible.get、StorageBoxUI.Show(StorageBoxController) 等共 4 处。</summary>
    private bool isVisible;
    /// <summary>中文含义：是否可见。使用位置：CombatHUD.IsCombatVisualBlocked()、DoorController.IsPlayerBlocked()、LeonInputBridge.CanStartWeaponWheel() 等共 10 处。</summary>
    public bool IsVisible => isVisible;

    /// <summary>中文含义：淡入淡出协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、StorageBoxUI.Close()、StorageBoxUI.Show(StorageBoxController)。</summary>
    private Coroutine fadeRoutine;
    /// <summary>中文含义：失败提示协程。使用位置：作为 MonoBehaviour.StopCoroutine(Coroutine) 的参数、StorageBoxUI.ShowFailHint(string)。</summary>
    private Coroutine failHintRoutine;
    /// <summary>中文含义：生效控制器。使用位置：StorageBoxUI.Close()、StorageBoxUI.Show(StorageBoxController)。</summary>
    private StorageBoxController activeController;
    /// <summary>中文含义：是否网格警告已显示。使用位置：StorageBoxUI.ResolveGridView()。</summary>
    private bool gridWarningShown;
    /// <summary>中文含义：已订阅仓库管理器。使用位置：StorageBoxUI.BindManagerEvents()、StorageBoxUI.UnbindManagerEvents()。</summary>
    private StorageBoxManager subscribedStorageManager;
    // 复用寄存箱行，隐藏时只保留一次待刷新标记。
    /// <summary>中文含义：仓库条目集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 List&lt;GridItem&gt;.List(IEnumerable&lt;GridItem&gt;) 的参数、StorageBoxUI.CacheReusableStorageEntries()、StorageBoxUI.RefreshStorageList()。</summary>
    private readonly System.Collections.Generic.Dictionary<GridItem, GameObject> storageEntries = new System.Collections.Generic.Dictionary<GridItem, GameObject>();
    /// <summary>中文含义：池化的仓库条目集合。只读字段，初始化后不由普通写入口替换。使用位置：StorageBoxUI.CacheReusableStorageEntries()、StorageBoxUI.RefreshStorageList()、StorageBoxUI.ReleaseStorageEntry(GameObject)。</summary>
    private readonly System.Collections.Generic.List<GameObject> pooledStorageEntries = new System.Collections.Generic.List<GameObject>();
    /// <summary>中文含义：仓库刷新是否仍待处理。使用位置：StorageBoxUI.RequestStorageRefresh()、StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Update()。</summary>
    private bool storageRefreshPending;
    /// <summary>中文含义：背包刷新是否仍待处理。使用位置：StorageBoxUI.RequestInventoryRefresh()、StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Update()。</summary>
    private bool inventoryRefreshPending;
    /// <summary>中文含义：是否可复用的仓库条目集合缓存。使用位置：StorageBoxUI.CacheReusableStorageEntries()。</summary>
    private bool reusableStorageEntriesCached;
    /// <summary>中文含义：已订阅背包管理器。使用位置：StorageBoxUI.BindManagerEvents()、StorageBoxUI.UnbindManagerEvents()。</summary>
    private InventoryManager subscribedInventoryManager;
    // 拖拽上下文
    /// <summary>中文含义：拖拽上下文。使用位置：作为 Dictionary&lt;GridItem, GameObject&gt;.ContainsKey(GridItem) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.ClearDragContext() 等共 9 处。</summary>
    private DragContext dragContext;
    /// <summary>界面交互与显示的结构化数据 DragContext；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    struct DragContext
    {
        /// <summary>中文含义：当前背包网格物品。使用位置：作为 Dictionary&lt;GridItem, GameObject&gt;.ContainsKey(GridItem) 的参数、作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2) 等共 7 处。</summary>
        public GridItem item;
        /// <summary>中文含义：来源网格来源。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)、StorageBoxUI.RefreshInventoryGridInternal() 等共 4 处。</summary>
        public InventoryGridView.GridSource source;
        /// <summary>中文含义：原始位置。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)。</summary>
        public Vector2 originalPos;
        /// <summary>中文含义：是否已完成。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.OnInventoryItemClicked(GridItem, PointerEventData.InputButton, Vector2)、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2) 等共 6 处。</summary>
        public bool completed;
    }

    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、StorageBoxUI.BindManagerEvents()、StorageBoxUI.ConfigureGridCallbacks()、StorageBoxUI.EnsureDragGhost()、StorageBoxUI.ResolveGridView()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool viewInitialized;

    void Start()
    {
        InitializeView();
    }

    private void InitializeView()
    {
        if (viewInitialized)
        {
            return;
        }

        ResolveGridView();
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        if (hintText != null)
        {
            hintText.text = "左键拖拽存取  |  右键背包物品快速存入  |  左键箱子物品取回  |  E/Esc/Tab 关闭";
        }

        if (failHintText != null)
        {
            failHintText.gameObject.SetActive(false);
        }

        if (dropZoneHighlight != null)
        {
            dropZoneHighlight.gameObject.SetActive(false);
        }

        // 初始化拖拽幽灵图标
        EnsureDragGhost();
        BindManagerEvents();
        ConfigureGridCallbacks();
        viewInitialized = true;
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.UnregisterModal(Object)、StorageBoxUI.UnbindManagerEvents()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        if (GameUIManager.HasInstance)
        {
            GameUIManager.Instance.UnregisterModal(this);
        }

        UnbindManagerEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameUIManager.TryClaimModalInput()、Input.GetKeyDown(KeyCode)、StorageBoxUI.Close()、StorageBoxUI.RefreshInventoryGridInternal()、StorageBoxUI.RefreshStorageList()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isVisible)
        {
            return;
        }

        if (storageRefreshPending)
        {
            storageRefreshPending = false;
            RefreshStorageList();
        }

        if (inventoryRefreshPending)
        {
            inventoryRefreshPending = false;
            RefreshInventoryGridInternal();
        }

        if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Tab) && !Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        if (GameUIManager.HasInstance && !GameUIManager.Instance.TryClaimModalInput())
        {
            return;
        }

        Close();
    }

    /// <summary>Show 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、MenuThemeApplier.Apply(Transform, UIVisualTheme)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、StorageBoxManager.Open() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxController.OpenUIWhenAnimationDone()；下游（含内部回调体） GameObject.SetActive(bool)、GameUIManager.RegisterModal(Object, GameUIManager.State, bool, bool, bool)、MenuThemeApplier.Apply(Transform, UIVisualTheme)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、StorageBoxManager.Open() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="controller">中文含义：当前仓库箱体控制器。使用位置：StorageBoxUI.Show(StorageBoxController)。</param>
    public void Show(StorageBoxController controller = null)
    {
        TryShow(controller);
    }

    internal bool TryShow(StorageBoxController controller)
    {
        if (isVisible || !isActiveAndEnabled || panel == null)
        {
            return false;
        }

        // loader（加载器）：使用位置：StorageBoxUI.Show(StorageBoxController)。
InventorySceneLoader loader = InventorySceneLoader.Instance;
        if (loader != null && (loader.IsVisible || loader.IsLoading))
        {
            return false;
        }

        // Additive 加载的调用方可能早于本组件的 Start 收到完成通知。
        InitializeView();
        MenuThemeApplier.Apply(transform, visualTheme);
        if (visualTheme != null && LeonController.Instance != null)
        {
            OriginalMenuBackdrop.Show(panel.transform, visualTheme.originalBackdropMaterial,
                LeonController.Instance.playerCamera);
        }

        ConfigureGridCallbacks();
        BindManagerEvents();
        activeController = controller;
        isVisible = true;
        // receiverValue1（判空前缓存的仓库箱体管理器的实例）：从仓库箱体管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxUI.Show(StorageBoxController)。
global::StorageBoxManager receiverValue1 = StorageBoxManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Open();
        }

        // receiverValue2（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxUI.Show(StorageBoxController)。
global::GameUIManager receiverValue2 = GameUIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.RegisterModal(this, GameUIManager.State.Storage);
        }

        if (panel != null)
        {
            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(true);
        }

        RefreshStorageList();
        storageRefreshPending = false;
        inventoryRefreshPending = false;
        RefreshInventoryGridInternal();
        if (gridView != null && gridView.threeDimensionalPresentation != null)
        {
            gridView.threeDimensionalPresentation.Show();
            RefreshInventoryGridInternal();
        }
        if (itemPreview != null)
        {
            itemPreview.Clear();
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeIn());
        return true;
    }

    /// <summary>ResolveGridView 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponentInChildren&lt;InventoryGridView&gt;(bool)、Debug.LogError(object, Object)、GameObject.AddComponent&lt;InventoryGridView&gt;()、StorageBoxUI.FindRectTransform(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Start()；下游（含内部回调体） Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponentInChildren&lt;InventoryGridView&gt;(bool)、Debug.LogError(object, Object)、GameObject.AddComponent&lt;InventoryGridView&gt;()、StorageBoxUI.FindRectTransform(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ResolveGridView()
    {
        if (gridView == null)
        {
            gridView = GetComponent<InventoryGridView>();
        }

        if (gridView == null)
        {
            gridView = GetComponentInChildren<InventoryGridView>(true);
        }

        if (gridView == null)
        {
            gridView = gameObject.AddComponent<InventoryGridView>();
        }

        if (gridView.gridContainer == null)
        {
            // assignmentReceiver3（赋值前保存的目标背包网格视图组件）：从背包网格视图组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：StorageBoxUI.ResolveGridView()。
global::InventoryGridView assignmentReceiver3 = gridView;
            if (inventoryGridContainer != null)
            {
                assignmentReceiver3.gridContainer = inventoryGridContainer;
            }
            else
            {
                assignmentReceiver3.gridContainer = FindRectTransform("GridContainer");
            }
        }

        if (gridView.gridArea == null)
        {
            gridView.gridArea = FindRectTransform("GridArea");
        }

        if ((gridView.gridContainer == null || gridView.gridArea == null) && !gridWarningShown)
        {
            gridWarningShown = true;
            Debug.LogError("[寄存箱界面] 无法恢复右侧背包网格引用。", this);
        }
    }

    /// <summary>ConfigureGridCallbacks 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConfigureGridCallbacks()
    {
        if (gridView == null)
        {
            return;
        }

        gridView.sourceType = InventoryGridView.GridSource.Inventory;
        gridView.onItemPointerDown = OnInventoryItemPointerDown;
        gridView.onItemDrag = OnInventoryItemDrag;
        gridView.onItemPointerUp = OnInventoryItemPointerUp;
        gridView.onItemClicked = OnInventoryItemClicked;
        gridView.onCellClicked = OnInventoryCellClicked;
        gridView.onItemHoverEnter = OnInventoryItemHoverEnter;
        gridView.onItemHoverExit = OnInventoryItemHoverExit;
    }

    /// <summary>BindManagerEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BindManagerEvents()
    {
        // storageManager（仓库数据与操作宿主）：使用位置：StorageBoxUI.BindManagerEvents()。
StorageBoxManager storageManager = StorageBoxManager.Instance;
        if (subscribedStorageManager != storageManager)
        {
            if (subscribedStorageManager != null)
            {
                subscribedStorageManager.OnStorageChanged -= RequestStorageRefresh;
                subscribedStorageManager.OnTransferFailed -= ShowFailHint;
            }

            subscribedStorageManager = storageManager;
            if (subscribedStorageManager != null)
            {
                subscribedStorageManager.OnStorageChanged += RequestStorageRefresh;
                subscribedStorageManager.OnTransferFailed += ShowFailHint;
            }
        }

        // inventoryManager（背包数据与操作宿主）：使用位置：StorageBoxUI.BindManagerEvents()。
InventoryManager inventoryManager = InventoryManager.Instance;
        if (subscribedInventoryManager == inventoryManager)
        {
            return;
        }

        if (subscribedInventoryManager != null)
        {
            subscribedInventoryManager.OnInventoryChanged -= RequestInventoryRefresh;
        }

        subscribedInventoryManager = inventoryManager;
        if (subscribedInventoryManager != null)
        {
            subscribedInventoryManager.OnInventoryChanged += RequestInventoryRefresh;
        }
    }

    /// <summary>UnbindManagerEvents 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.OnDestroy()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UnbindManagerEvents()
    {
        if (subscribedStorageManager != null)
        {
            subscribedStorageManager.OnStorageChanged -= RequestStorageRefresh;
            subscribedStorageManager.OnTransferFailed -= ShowFailHint;
        }

        if (subscribedInventoryManager != null)
        {
            subscribedInventoryManager.OnInventoryChanged -= RequestInventoryRefresh;
        }

        subscribedStorageManager = null;
        subscribedInventoryManager = null;
    }

    /// <summary>FindRectTransform 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;RectTransform&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ResolveGridView()；下游（含内部回调体） Component.GetComponentsInChildren&lt;RectTransform&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="objectName">中文含义：对象名称。使用位置：StorageBoxUI.FindRectTransform(string)。</param>
    RectTransform FindRectTransform(string objectName)
    {
        // rects（矩形集合）：保存 Component.GetComponentsInChildren<RectTransform>(bool) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.FindRectTransform(string)。
RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        // i（当前遍历索引）：使用位置：StorageBoxUI.FindRectTransform(string)。
for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == objectName)
            {
                return rects[i];
            }
        }

        return null;
    }

    /// <summary>Close 的业务处理入口；本方法直接依赖 GameUIManager.UnregisterModal(Object)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、StorageBoxController.CloseBox()、StorageBoxManager.Close()、StorageBoxUI.ClearDragContext() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Update()；下游（含内部回调体） GameUIManager.UnregisterModal(Object)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、object.ReferenceEquals(object, object)、StorageBoxController.CloseBox()、StorageBoxManager.Close()、StorageBoxUI.ClearDragContext() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Close()
    {
        if (gridView != null && gridView.threeDimensionalPresentation != null)
        {
            gridView.threeDimensionalPresentation.ImmediateHide();
        }
        if (!isVisible)
        {
            return;
        }

        isVisible = false;
        ClearDragContext();
        if (itemPreview != null)
        {
            itemPreview.Close();
        }

        // receiverValue5（判空前缓存的仓库箱体管理器的实例）：从仓库箱体管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxUI.Close()。
global::StorageBoxManager receiverValue5 = StorageBoxManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Close();
        }

        // receiverValue6（判空前缓存的游戏界面管理器的实例）：从游戏界面管理器的实例读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxUI.Close()。
global::GameUIManager receiverValue6 = GameUIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.UnregisterModal(this);
        }

        // receiverValue7（判空前缓存的生效控制器）：从生效控制器读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxUI.Close()。
global::StorageBoxController receiverValue7 = activeController;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.CloseBox();
        }

        activeController = null;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeOut());
    }

    /// <summary>FadeIn 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeIn()
    {
        if (panel == null)
        {
            yield break;
        }

        // elapsed（已用）：使用位置：StorageBoxUI.FadeIn()。
float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // t（当前界面动画的归一化进度）：使用位置：StorageBoxUI.FadeIn()。
float t = elapsed / fadeDuration;
            // eased（缓动后的）：使用位置：StorageBoxUI.FadeIn()。
float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            panel.alpha = eased;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        panel.alpha = 1f;
        panel.interactable = true;
        panel.blocksRaycasts = true;
    }

    /// <summary>FadeOut 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Close()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOut()
    {
        if (panel == null)
        {
            yield break;
        }

        // elapsed（已用）：使用位置：StorageBoxUI.FadeOut()。
float elapsed = 0f;
        // startAlpha（起始透明度）：使用位置：StorageBoxUI.FadeOut()。
float startAlpha = panel.alpha;
        while (elapsed < fadeDuration)
        {
            // t（当前界面动画的归一化进度）：使用位置：StorageBoxUI.FadeOut()。
float t = elapsed / fadeDuration;
            // eased（缓动后的）：使用位置：StorageBoxUI.FadeOut()。
float eased = 1f - t * t * t;
            panel.alpha = startAlpha * eased;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
    }

    // ===== 左侧：箱子物品列表 =====
    /// <summary>RefreshStorageList 的业务处理入口；本方法直接依赖 Dictionary&lt;GridItem, GameObject&gt;.ContainsKey(GridItem)、Dictionary&lt;GridItem, GameObject&gt;.Remove(GridItem)、Dictionary&lt;GridItem, GameObject&gt;.TryGetValue(GridItem, out GameObject)、GameObject.SetActive(bool)、List&lt;GameObject&gt;.RemoveAt(int)、List&lt;GridItem&gt;.Add(GridItem)、List&lt;GridItem&gt;.Contains(GridItem) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Update()；下游（含内部回调体） Dictionary&lt;GridItem, GameObject&gt;.ContainsKey(GridItem)、Dictionary&lt;GridItem, GameObject&gt;.Remove(GridItem)、Dictionary&lt;GridItem, GameObject&gt;.TryGetValue(GridItem, out GameObject)、GameObject.SetActive(bool)、List&lt;GameObject&gt;.RemoveAt(int)、List&lt;GridItem&gt;.Add(GridItem)、List&lt;GridItem&gt;.Contains(GridItem) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RefreshStorageList()
    {
        if (storageListContainer == null)
        {
            return;
        }

        CacheReusableStorageEntries();
        // desiredItems（期望物品列表）：使用位置：StorageBoxUI.RefreshStorageList()。
var desiredItems = new System.Collections.Generic.List<GridItem>();
        // mgr（当前仓库管理器）：使用位置：StorageBoxUI.RefreshStorageList()。
StorageBoxManager mgr = StorageBoxManager.Instance;
        if (mgr != null)
        {
            // gi（背包网格物品）：每轮从取得仓库物品列表的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<GridItem>.Add(GridItem) 的参数、作为 List<GridItem>.Contains(GridItem) 的参数、StorageBoxUI.RefreshStorageList()。
foreach (GridItem gi in mgr.GetStorageItems())
            {
                if (gi != null && !desiredItems.Contains(gi))
                {
                    desiredItems.Add(gi);
                }
            }
        }

        // 先回收失效行，再申请新行，保证同容量换物品时不会额外增长。
        // activeItems（生效物品列表）：使用位置：StorageBoxUI.RefreshStorageList()。
var activeItems = new System.Collections.Generic.List<GridItem>(storageEntries.Keys);
        // i（当前遍历索引）：使用位置：作为 List<GridItem>.this[int] 的参数、StorageBoxUI.RefreshStorageList()。
for (int i = 0; i < activeItems.Count; i++)
        {
            // activeItem（生效物品）：使用位置：作为 Dictionary<GridItem, GameObject>.Remove(GridItem) 的参数、作为 Dictionary<GridItem, GameObject>.TryGetValue(GridItem, out GameObject) 的参数、作为 List<GridItem>.Contains(GridItem) 的参数 等共 4 处。
GridItem activeItem = activeItems[i];
            if (desiredItems.Contains(activeItem))
            {
                continue;
            }

            // staleEntry（过期的入口）：使用位置：作为 Dictionary<GridItem, GameObject>.TryGetValue(GridItem, out GameObject) 的参数、作为 StorageBoxUI.ReleaseStorageEntry(GameObject) 的参数。
GameObject staleEntry;
            if (storageEntries.TryGetValue(activeItem, out staleEntry))
            {
                storageEntries.Remove(activeItem);
                ReleaseStorageEntry(staleEntry);
            }

            if (dragContext.item == activeItem)
            {
                ClearDragContext();
            }
        }

        // i（当前遍历索引）：使用位置：作为 List<GridItem>.this[int] 的参数、作为 Transform.SetSiblingIndex(int) 的参数、StorageBoxUI.RefreshStorageList()。
for (int i = 0; i < desiredItems.Count; i++)
        {
            // gi（背包网格物品）：使用位置：作为 Dictionary<GridItem, GameObject>.this[GridItem] 的参数、作为 Dictionary<GridItem, GameObject>.TryGetValue(GridItem, out GameObject) 的参数、作为 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool) 的参数 等共 4 处。
GridItem gi = desiredItems[i];
            // entry（当前游戏对象）：使用位置：作为 Dictionary<GridItem, GameObject>.TryGetValue(GridItem, out GameObject) 的参数、作为 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool) 的参数、StorageBoxUI.RefreshStorageList()。
GameObject entry;
            if (storageEntries.TryGetValue(gi, out entry))
            {
                ConfigureStorageEntry(entry, gi, false);
            }
            else if (pooledStorageEntries.Count > 0)
            {
                // lastIndex（上一次索引）：使用位置：作为 List<GameObject>.RemoveAt(int) 的参数、作为 List<GameObject>.this[int] 的参数。
int lastIndex = pooledStorageEntries.Count - 1;
                entry = pooledStorageEntries[lastIndex];
                pooledStorageEntries.RemoveAt(lastIndex);
                ConfigureStorageEntry(entry, gi, true);
            }
            else
            {
                entry = CreateStorageEntry(gi);
            }

            entry.transform.SetSiblingIndex(i);
            entry.SetActive(true);
            storageEntries[gi] = entry;
        }

        if (dragContext.item != null && dragContext.source == InventoryGridView.GridSource.Storage && !storageEntries.ContainsKey(dragContext.item))
        {
            ClearDragContext();
        }
    }

    /// <summary>CreateStorageEntry 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;LayoutElement&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;HorizontalLayoutGroup&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.RefreshStorageList()；下游（含内部回调体） GameObject.AddComponent&lt;LayoutElement&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;HorizontalLayoutGroup&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool) 的参数。</param>
    GameObject CreateStorageEntry(GridItem gi)
    {
        // go（当前游戏对象）：使用位置：作为 RefinedMenuStyle.AddRowLine(Transform) 的参数、作为 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 4 处。
GameObject go = new GameObject("StorageEntry", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(Image), typeof(EventTrigger));
        go.transform.SetParent(storageListContainer, false);
        // layoutGroup（布局分组）：保存 GameObject.GetComponent<HorizontalLayoutGroup>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
HorizontalLayoutGroup layoutGroup = go.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 12f;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = true;
        layoutGroup.padding = new RectOffset(10, 10, 4, 4);
        // layoutElement（布局元素）：保存 GameObject.GetComponent<LayoutElement>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 44f;
        layoutElement.flexibleWidth = 1f;
        // background（背景）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
Image background = go.GetComponent<Image>();
        background.raycastTarget = true;
        // rect（矩形）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 44f);
        // iconObject（图标对象）：使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(go.transform, false);
        // iconLayout（图标布局）：保存 GameObject.AddComponent<LayoutElement>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 64f;
        iconLayout.preferredHeight = 36f;
        iconLayout.flexibleWidth = 0f;
        iconObject.GetComponent<Image>().raycastTarget = false;
        // labelObject（标签对象）：使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(go.transform, false);
        // labelLayout（标签布局）：保存 GameObject.AddComponent<LayoutElement>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.preferredHeight = 44f;
        // label（标签）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
Text label = labelObject.GetComponent<Text>();
        // assignmentReceiver8（赋值前保存的目标标签）：从标签读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：StorageBoxUI.CreateStorageEntry(GridItem)。
global::UnityEngine.UI.Text assignmentReceiver8 = label;
        if (visualTheme != null)
        {
            assignmentReceiver8.font = visualTheme.interfaceFont;
        }
        else
        {
            assignmentReceiver8.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        RefinedMenuStyle.AddRowLine(go.transform);
        ConfigureStorageEntry(go, gi, true);
        go.SetActive(false);
        return go;
    }

    /// <summary>ConfigureStorageEntry 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;Text&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.CreateStorageEntry(GridItem)、StorageBoxUI.RefreshStorageList()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;Text&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.GetComponent&lt;Image&gt;() 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="go">中文含义：当前游戏对象。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。</param>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 StorageBoxManager.TransferToInventory(GridItem) 的参数、作为 StorageBoxUI.ShowDragGhost(GridItem) 的参数、作为 StoredItemPreview.Select(GridItem) 的参数 等共 5 处。</param>
    /// <param name="rebindCallbacks">中文含义：是否重新绑定回调集合。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。</param>
    void ConfigureStorageEntry(GameObject go, GridItem gi, bool rebindCallbacks)
    {
        if (go == null || gi == null || gi.item == null)
        {
            return;
        }

        go.name = "Storage_" + gi.item.displayName;
        // normalColor（普通状态颜色）：使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Color normalColor = Color.clear;
        // hoverColor（悬停颜色）：使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
Color hoverColor = new Color(.75f, .77f, .73f, .52f);
        // background（背景）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Image background = go.GetComponent<Image>();
        if (background != null)
        {
            if (visualTheme != null)
            {
                background.sprite = visualTheme.originalSelectionBand;
                background.material = visualTheme.originalSelectionMaterial;
                background.type = Image.Type.Simple;
            }
            if (rebindCallbacks)
            {
                background.color = normalColor;
            }

            background.raycastTarget = true;
        }

        // iconTransform（图标变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Transform iconTransform = go.transform.Find("Icon");
        // icon（图标）：使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Image icon;
        if (iconTransform != null)
        {
            icon = iconTransform.GetComponent<Image>();
        }
        else
        {
            icon = null;
        }

        if (icon != null)
        {
            icon.sprite = gi.item.icon;
            icon.preserveAspect = true;
            // assignmentReceiver11（赋值前保存的目标图标）：从图标读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
global::UnityEngine.UI.Image assignmentReceiver11 = icon;
            if (gi.item.icon != null)
            {
                assignmentReceiver11.color = Color.white;
            }
            else
            {
                assignmentReceiver11.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            }

            icon.raycastTarget = false;
        }

        // labelTransform（标签变换）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Transform labelTransform = go.transform.Find("Label");
        // label（标签）：使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
Text label;
        if (labelTransform != null)
        {
            label = labelTransform.GetComponent<Text>();
        }
        else
        {
            label = null;
        }

        if (label != null)
        {
            label.text = gi.item.displayName + "　× " + gi.count;
            label.color = Color.white;
        }

        if (!rebindCallbacks)
        {
            return;
        }

        // trigger（触发器）：保存 GameObject.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = go.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();
        // enterEntry（进入入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry enterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseHover);
            if (itemPreview != null)
            {
                itemPreview.Select(gi);
            }

            // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
Image image = go.GetComponent<Image>();
            if (image != null)
            {
                image.color = hoverColor;
            }
        });
        trigger.triggers.Add(enterEntry);
        // exitEntry（退出入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry exitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
Image image = go.GetComponent<Image>();
            if (image != null)
            {
                image.color = normalColor;
            }
        });
        trigger.triggers.Add(exitEntry);
        // downEntry（向下入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry downEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        downEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
        {
            // pointer（指针）：使用位置：作为 StorageBoxUI.UpdateDragGhostPos(Vector2) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
PointerEventData pointer = (PointerEventData)e;
            if (pointer.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            dragContext = new DragContext
            {
                item = gi,
                source = InventoryGridView.GridSource.Storage,
                originalPos = Vector2.zero,
                completed = false
            };
            ShowDragGhost(gi);
            UpdateDragGhostPos(pointer.position);
        });
        trigger.triggers.Add(downEntry);
        // dragEntry（拖拽入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry dragEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Drag
        };
        dragEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
        {
            if (dragContext.item == null || dragContext.completed || dragContext.source != InventoryGridView.GridSource.Storage)
            {
                return;
            }

            // pointer（指针）：使用位置：作为 StorageBoxUI.IsPointerOverInventoryGrid(Vector2) 的参数、作为 StorageBoxUI.UpdateDragGhostPos(Vector2) 的参数。
PointerEventData pointer = (PointerEventData)e;
            UpdateDragGhostPos(pointer.position);
            // overGrid（是否上方网格）：保存 StorageBoxUI.IsPointerOverInventoryGrid(Vector2) 的返回结果，供当前步骤使用。使用位置：作为 GameObject.SetActive(bool) 的参数。
bool overGrid = IsPointerOverInventoryGrid(pointer.position);
            var presentation = gridView.threeDimensionalPresentation;
            if (presentation != null && presentation.Interactive)
            {
                presentation.ShowTransferTarget(overGrid, inventoryDropZoneColor);
            }
            else if (inventoryDropZoneHighlight != null)
            {
                inventoryDropZoneHighlight.gameObject.SetActive(overGrid);
                inventoryDropZoneHighlight.color = inventoryDropZoneColor;
            }
        });
        trigger.triggers.Add(dragEntry);
        // upEntry（向上入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry upEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerUp
        };
        upEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
        {
            if (dragContext.item == null || dragContext.completed || dragContext.source != InventoryGridView.GridSource.Storage)
            {
                return;
            }

            dragContext.completed = true;
            HideDragGhost();
            if (gridView.threeDimensionalPresentation != null)
            {
                gridView.threeDimensionalPresentation.EndDrag();
            }
            // pointer（指针）：使用位置：作为 StorageBoxUI.IsPointerOverInventoryGrid(Vector2) 的参数。
PointerEventData pointer = (PointerEventData)e;
            if (inventoryDropZoneHighlight != null)
            {
                inventoryDropZoneHighlight.gameObject.SetActive(false);
            }

            // storageManager（仓库数据与操作宿主）：使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
StorageBoxManager storageManager = StorageBoxManager.Instance;
            if (IsPointerOverInventoryGrid(pointer.position) && storageManager != null && !storageManager.TransferToInventory(gi))
            {
                ShowFailHint("背包空间不足");
            }

            dragContext.item = null;
        });
        trigger.triggers.Add(upEntry);
        // clickEntry（点击入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)。
EventTrigger.Entry clickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        clickEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
        {
            if (dragContext.completed)
            {
                dragContext.completed = false;
                return;
            }

            // pointer（指针）：使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
PointerEventData pointer = (PointerEventData)e;
            // storageManager（仓库数据与操作宿主）：使用位置：作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
StorageBoxManager storageManager = StorageBoxManager.Instance;
            if (pointer.button == PointerEventData.InputButton.Left && storageManager != null && !storageManager.TransferToInventory(gi))
            {
                ShowFailHint("背包空间不足");
            }
        });
        trigger.triggers.Add(clickEntry);
    }

    /// <summary>ClearDragContext 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、StorageBoxUI.HideDragGhost()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Close()、StorageBoxUI.RefreshInventoryGridInternal()、StorageBoxUI.RefreshStorageList()；下游（含内部回调体） GameObject.SetActive(bool)、StorageBoxUI.HideDragGhost()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ClearDragContext()
    {
        HideDragGhost();
        if (dropZoneHighlight != null)
        {
            dropZoneHighlight.gameObject.SetActive(false);
        }

        if (inventoryDropZoneHighlight != null)
        {
            inventoryDropZoneHighlight.gameObject.SetActive(false);
        }

        dragContext = default(DragContext);
    }

    /// <summary>CacheReusableStorageEntries 的业务处理入口；本方法直接依赖 Dictionary&lt;GridItem, GameObject&gt;.ContainsValue(GameObject)、GameObject.SetActive(bool)、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)、string.StartsWith(string, StringComparison)、Transform.GetChild(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.RefreshStorageList()；下游（含内部回调体） Dictionary&lt;GridItem, GameObject&gt;.ContainsValue(GameObject)、GameObject.SetActive(bool)、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)、string.StartsWith(string, StringComparison)、Transform.GetChild(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CacheReusableStorageEntries()
    {
        if (reusableStorageEntriesCached || storageListContainer == null)
        {
            return;
        }

        reusableStorageEntriesCached = true;
        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、StorageBoxUI.CacheReusableStorageEntries()。
for (int i = storageListContainer.childCount - 1; i >= 0; i--)
        {
            // child（子级）：保存 Transform.GetChild(int) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.CacheReusableStorageEntries()。
Transform child = storageListContainer.GetChild(i);
            if (!child.name.StartsWith("Storage_", System.StringComparison.Ordinal))
            {
                continue;
            }

            // entry（当前游戏对象）：使用位置：作为 Dictionary<GridItem, GameObject>.ContainsValue(GameObject) 的参数、作为 List<GameObject>.Add(GameObject) 的参数、作为 List<GameObject>.Contains(GameObject) 的参数 等共 4 处。
GameObject entry = child.gameObject;
            if (storageEntries.ContainsValue(entry) || pooledStorageEntries.Contains(entry))
            {
                continue;
            }

            pooledStorageEntries.Add(entry);
            entry.SetActive(false);
        }
    }

    /// <summary>ReleaseStorageEntry 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.SetActive(bool)、List&lt;EventTrigger.Entry&gt;.Clear()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.RefreshStorageList()；下游（含内部回调体） GameObject.GetComponent&lt;EventTrigger&gt;()、GameObject.SetActive(bool)、List&lt;EventTrigger.Entry&gt;.Clear()、List&lt;GameObject&gt;.Add(GameObject)、List&lt;GameObject&gt;.Contains(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="entry">中文含义：当前游戏对象。使用位置：作为 List&lt;GameObject&gt;.Add(GameObject) 的参数、作为 List&lt;GameObject&gt;.Contains(GameObject) 的参数、StorageBoxUI.ReleaseStorageEntry(GameObject)。</param>
    void ReleaseStorageEntry(GameObject entry)
    {
        if (entry == null)
        {
            return;
        }

        // trigger（触发器）：保存 GameObject.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.ReleaseStorageEntry(GameObject)。
EventTrigger trigger = entry.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            trigger.triggers.Clear();
        }

        entry.SetActive(false);
        if (!pooledStorageEntries.Contains(entry))
        {
            pooledStorageEntries.Add(entry);
        }
    }

    // ===== 右侧：背包网格（委托给 gridView）=====
    /// <summary>RefreshInventoryGridInternal 的业务处理入口；本方法直接依赖 InventoryGridView.GetItemWidget(GridItem)、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryManager.GetItems()、StorageBoxUI.ClearDragContext()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)、StorageBoxUI.Update()；下游（含内部回调体） InventoryGridView.GetItemWidget(GridItem)、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryManager.GetItems()、StorageBoxUI.ClearDragContext()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RefreshInventoryGridInternal()
    {
        // mgr（当前背包管理器）：使用位置：作为 InventoryGridView.Rebuild(int, int, IReadOnlyList<GridItem>) 的参数、StorageBoxUI.RefreshInventoryGridInternal()。
var mgr = InventoryManager.Instance;
        if (mgr == null || gridView == null)
        {
            return;
        }

        // activeDragItem（生效拖拽物品）：使用位置：作为 InventoryGridView.GetItemWidget(GridItem) 的参数、作为 InventoryGridView.SetItemDragAlpha(GridItem, bool) 的参数、作为 InventoryGridView.SetItemWidgetPosition(GridItem, Vector2) 的参数 等共 4 处。
GridItem activeDragItem;
        if (dragContext.source == InventoryGridView.GridSource.Inventory)
        {
            activeDragItem = dragContext.item;
        }
        else
        {
            activeDragItem = null;
        }

        // dragVisualPosition（拖拽视觉表现位置）：使用位置：作为 InventoryGridView.SetItemWidgetPosition(GridItem, Vector2) 的参数、StorageBoxUI.RefreshInventoryGridInternal()。
Vector2 dragVisualPosition = Vector2.zero;
        // hadDragVisual（是否此前具有拖拽视觉表现）：使用位置：StorageBoxUI.RefreshInventoryGridInternal()。
bool hadDragVisual = false;
        if (activeDragItem != null)
        {
            // dragWidget（拖拽界面控件）：保存 InventoryGridView.GetItemWidget(GridItem) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.RefreshInventoryGridInternal()。
RectTransform dragWidget = gridView.GetItemWidget(activeDragItem);
            if (dragWidget != null)
            {
                dragVisualPosition = dragWidget.anchoredPosition;
                hadDragVisual = true;
            }
        }

        gridView.Rebuild(mgr.CaseWidth, mgr.CaseHeight, mgr.GetItems());
        if (activeDragItem == null)
        {
            return;
        }

        // refreshedDragWidget（刷新后的拖拽界面控件）：保存 InventoryGridView.GetItemWidget(GridItem) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.RefreshInventoryGridInternal()。
RectTransform refreshedDragWidget = gridView.GetItemWidget(activeDragItem);
        if (refreshedDragWidget == null)
        {
            ClearDragContext();
        }
        else if (hadDragVisual && !dragContext.completed)
        {
            // 刷新没有替换拖拽物品时，保留右侧网格拖拽位置和透明度。
            gridView.SetItemWidgetPosition(activeDragItem, dragVisualPosition);
            gridView.SetItemDragAlpha(activeDragItem, true);
        }
    }

    // ===== 拖拽：从背包拖入箱子 =====
    /// <summary>OnInventoryItemPointerDown 的业务处理入口；本方法直接依赖 InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.ShowSelected(GridItem)、StorageBoxUI.DragContext.DragContext()、StorageBoxUI.GetItemAnchoredPos(GridItem)、StorageBoxUI.HideDragGhost()、StoredItemPreview.Select(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.ShowSelected(GridItem)、StorageBoxUI.DragContext.DragContext()、StorageBoxUI.GetItemAnchoredPos(GridItem)、StorageBoxUI.HideDragGhost()、StoredItemPreview.Select(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.SetItemDragAlpha(GridItem, bool) 的参数、作为 InventoryGridView.ShowSelected(GridItem) 的参数、作为 StorageBoxUI.GetItemAnchoredPos(GridItem) 的参数 等共 5 处。</param>
    /// <param name="screenPos">中文含义：屏幕位置。</param>
    void OnInventoryItemPointerDown(GridItem gi, Vector2 screenPos)
    {
        if (gi == null)
        {
            return;
        }

        HideDragGhost();
        dragContext = new DragContext
        {
            item = gi,
            source = InventoryGridView.GridSource.Inventory,
            originalPos = GetItemAnchoredPos(gi),
            completed = false
        };
        gridView.ShowSelected(gi);
        if (itemPreview != null)
        {
            itemPreview.Select(gi);
        }

        gridView.SetItemDragAlpha(gi, true);
    }

    /// <summary>OnInventoryItemDrag 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、StorageBoxUI.IsPointerOverStorageList(Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、StorageBoxUI.IsPointerOverStorageList(Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.SetItemWidgetPosition(GridItem, Vector2) 的参数。</param>
    /// <param name="screenPos">中文含义：屏幕位置。使用位置：作为 InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2) 的参数、作为 StorageBoxUI.IsPointerOverStorageList(Vector2) 的参数。</param>
    void OnInventoryItemDrag(GridItem gi, Vector2 screenPos)
    {
        if (dragContext.item == null || dragContext.completed)
        {
            return;
        }

        // 移动物品控件
        if (gridView.TryScreenPointToLocal(screenPos, out Vector2 /* 中文含义：局部坐标点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ localPoint))
        {
            gridView.SetItemWidgetPosition(gi, localPoint);
        }

        // 检测是否进入左侧投放区
        // overDropZone（是否上方掉落区）：保存 StorageBoxUI.IsPointerOverStorageList(Vector2) 的返回结果，供当前步骤使用。使用位置：作为 GameObject.SetActive(bool) 的参数。
bool overDropZone = IsPointerOverStorageList(screenPos);
        if (dropZoneHighlight != null)
        {
            dropZoneHighlight.gameObject.SetActive(overDropZone);
            dropZoneHighlight.color = dropZoneColor;
        }
    }

    /// <summary>OnInventoryItemPointerUp 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.IsPointerOverStorageList(Vector2)、StorageBoxUI.RequestInventoryRefresh()、StorageBoxUI.ShowFailHint(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.IsPointerOverStorageList(Vector2)、StorageBoxUI.RequestInventoryRefresh()、StorageBoxUI.ShowFailHint(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.SetItemDragAlpha(GridItem, bool) 的参数、作为 StorageBoxManager.TransferToStorage(GridItem) 的参数。</param>
    /// <param name="screenPos">中文含义：屏幕位置。使用位置：作为 StorageBoxUI.IsPointerOverStorageList(Vector2) 的参数。</param>
    void OnInventoryItemPointerUp(GridItem gi, Vector2 screenPos)
    {
        if (dragContext.item == null || dragContext.completed)
        {
            return;
        }

        dragContext.completed = true;
        gridView.SetItemDragAlpha(gi, false);
        if (dropZoneHighlight != null)
        {
            dropZoneHighlight.gameObject.SetActive(false);
        }

        // overDropZone（是否上方掉落区）：保存 StorageBoxUI.IsPointerOverStorageList(Vector2) 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2)。
bool overDropZone = IsPointerOverStorageList(screenPos);
        if (overDropZone)
        {
            // 执行转移
            if (!StorageBoxManager.Instance.TransferToStorage(gi))
            {
                ShowFailHint("无法存入该物品");
                RequestInventoryRefresh();
            }
        // 转移成功后两侧各自通过事件刷新
        }
        else
        {
            // 未在投放区松开，恢复原位
            RequestInventoryRefresh();
        }

        dragContext.item = null;
    }

    /// <summary>OnInventoryItemClicked 的业务处理入口；本方法直接依赖 StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.ShowFailHint(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.ShowFailHint(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 StorageBoxManager.TransferToStorage(GridItem) 的参数。</param>
    /// <param name="button">中文含义：按钮。使用位置：StorageBoxUI.OnInventoryItemClicked(GridItem, PointerEventData.InputButton, Vector2)。</param>
    /// <param name="screenPos">中文含义：屏幕位置。</param>
    void OnInventoryItemClicked(GridItem gi, PointerEventData.InputButton button, Vector2 screenPos)
    {
        if (button == PointerEventData.InputButton.Right)
        {
            dragContext.completed = false;
            // 右键快速存入
            if (!StorageBoxManager.Instance.TransferToStorage(gi))
            {
                ShowFailHint("无法存入该物品");
            }

            return;
        }

        if (dragContext.completed)
        {
            dragContext.completed = false;
        }
    }

    /// <summary>OnInventoryCellClicked 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">中文含义：横向。</param>
    /// <param name="y">中文含义：纵向。</param>
    void OnInventoryCellClicked(int x, int y)
    {
    }

    /// <summary>OnInventoryItemHoverEnter 的业务处理入口；本方法直接依赖 InventoryGridView.ShowHover(GridItem)、StoredItemPreview.Select(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.ShowHover(GridItem)、StoredItemPreview.Select(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.ShowHover(GridItem) 的参数、作为 StoredItemPreview.Select(GridItem) 的参数。</param>
    void OnInventoryItemHoverEnter(GridItem gi)
    {
        AudioManager.PlayInterface(AudioEventNames.UICaseHover);
        gridView.ShowHover(gi);
        if (itemPreview != null)
        {
            itemPreview.Select(gi);
        }
    }

    /// <summary>OnInventoryItemHoverExit 的业务处理入口；本方法直接依赖 InventoryGridView.HideHover()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.HideHover()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnInventoryItemHoverExit()
    {
        gridView.HideHover();
    }

    // ===== 辅助 =====
    /// <summary>EnsureDragGhost 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Component.GetComponentInParent&lt;Canvas&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ShowDragGhost(GridItem)、StorageBoxUI.Start()；下游（含内部回调体） Color.Color(float, float, float, float)、Component.GetComponentInParent&lt;Canvas&gt;()、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void EnsureDragGhost()
    {
        if (dragGhost != null)
        {
            dragGhostRt = dragGhost.rectTransform;
            dragGhost.gameObject.SetActive(false);
            dragGhost.raycastTarget = false;
            return;
        }

        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、StorageBoxUI.EnsureDragGhost()。
var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // go（当前游戏对象）：使用位置：StorageBoxUI.EnsureDragGhost()。
var go = new GameObject("DragGhost", typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        dragGhost = go.GetComponent<Image>();
        dragGhost.raycastTarget = false;
        dragGhost.color = new Color(1, 1, 1, 0.7f);
        dragGhost.preserveAspect = true;
        dragGhostRt = dragGhost.rectTransform;
        dragGhostRt.sizeDelta = new Vector2(64, 64);
        go.SetActive(false);
    }

    /// <summary>ShowDragGhost 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、StorageBoxUI.EnsureDragGhost()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、StorageBoxUI.EnsureDragGhost()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：StorageBoxUI.ShowDragGhost(GridItem)。</param>
    void ShowDragGhost(GridItem gi)
    {
        if (dragGhost == null)
        {
            EnsureDragGhost();
        }

        if (dragGhost == null)
        {
            return;
        }

        dragGhost.sprite = gi.item.icon;
        // assignmentReceiver15（赋值前保存的目标拖拽时跟随光标的图标（留空则自动创建））：从拖拽幽灵预览读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：StorageBoxUI.ShowDragGhost(GridItem)。
global::UnityEngine.UI.Image assignmentReceiver15 = dragGhost;
        if (gi.item.icon != null)
        {
            assignmentReceiver15.color = new Color(1, 1, 1, 0.7f);
        }
        else
        {
            assignmentReceiver15.color = new Color(0.6f, 0.5f, 0.2f, 0.7f);
        }

        dragGhost.gameObject.SetActive(true);
    }

    /// <summary>UpdateDragGhostPos 的业务处理入口；本方法直接依赖 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)；下游（含内部回调体） RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="screenPos">中文含义：屏幕位置。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数。</param>
    void UpdateDragGhostPos(Vector2 screenPos)
    {
        if (dragGhostRt == null)
        {
            return;
        }

        // parent（父级）：使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、StorageBoxUI.UpdateDragGhostPos(Vector2)。
var parent = dragGhostRt.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        // localPos（局部位置）：使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、StorageBoxUI.UpdateDragGhostPos(Vector2)。
Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, null, out localPos);
        dragGhostRt.anchoredPosition = localPos;
    }

    /// <summary>HideDragGhost 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ClearDragContext()、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HideDragGhost()
    {
        if (dragGhost != null)
        {
            dragGhost.gameObject.SetActive(false);
        }
    }

    /// <summary>IsPointerOverStorageList 的业务处理入口；本方法直接依赖 Component.GetComponentInParent&lt;Canvas&gt;()、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2)、StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2)；下游（含内部回调体） Component.GetComponentInParent&lt;Canvas&gt;()、RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="screenPos">中文含义：屏幕位置。使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera) 的参数。</param>
    bool IsPointerOverStorageList(Vector2 screenPos)
    {
        // target（目标界面矩形变换）：使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera) 的参数、StorageBoxUI.IsPointerOverStorageList(Vector2)。
RectTransform target = storageDropArea;
        if (target == null && storageListContainer != null)
        {
            target = storageListContainer.parent as RectTransform;
        }

        if (target == null)
        {
            target = storageListContainer;
        }

        if (target == null)
        {
            return false;
        }

        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：StorageBoxUI.IsPointerOverStorageList(Vector2)。
Canvas canvas = target.GetComponentInParent<Canvas>();
        // eventCamera（事件摄像机）：使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera) 的参数、StorageBoxUI.IsPointerOverStorageList(Vector2)。
Camera eventCamera;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }
        else
        {
            eventCamera = null;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(target, screenPos, eventCamera);
    }

    /// <summary>IsPointerOverInventoryGrid 的业务处理入口；本方法直接依赖 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)；下游（含内部回调体） RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="screenPos">中文含义：屏幕位置。使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数。</param>
    bool IsPointerOverInventoryGrid(Vector2 screenPos)
    {
        if (gridView == null || gridView.gridArea == null)
        {
            return false;
        }

        var presentation = gridView.threeDimensionalPresentation;
        if (presentation != null)
        {
            if (!presentation.Interactive || !presentation.ScreenRotateMesh(screenPos, out var localPoint))
            {
                return false;
            }

            return Mathf.Abs(localPoint.x) <= gridView.TotalWidth * 0.5f
                && Mathf.Abs(localPoint.y) <= gridView.TotalHeight * 0.5f;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(gridView.gridArea, screenPos);
    }

    /// <summary>GetItemAnchoredPos 的业务处理入口；本方法直接依赖 InventoryGridView.GetItemWidget(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)；下游（含内部回调体） InventoryGridView.GetItemWidget(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.GetItemWidget(GridItem) 的参数。</param>
    Vector2 GetItemAnchoredPos(GridItem gi)
    {
        // widget（界面控件）：使用位置：StorageBoxUI.GetItemAnchoredPos(GridItem)。
RectTransform widget;
        if (gridView != null)
        {
            widget = gridView.GetItemWidget(gi);
        }
        else
        {
            widget = null;
        }

        if (widget != null)
        {
            return widget.anchoredPosition;
        }
        else
        {
            return Vector2.zero;
        }
    }

    /// <summary>ShowFailHint 的业务处理入口；本方法直接依赖 MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、StorageBoxUI.FailHintRoutine(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)、StorageBoxUI.OnInventoryItemClicked(GridItem, PointerEventData.InputButton, Vector2)、StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2)；下游（含内部回调体） MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)、StorageBoxUI.FailHintRoutine(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="message">中文含义：消息。使用位置：作为 StorageBoxUI.FailHintRoutine(string) 的参数。</param>
    void ShowFailHint(string message)
    {
        AudioManager.PlayInterface(AudioEventNames.UICaseError);
        if (failHintText == null)
        {
            return;
        }

        if (failHintRoutine != null)
        {
            StopCoroutine(failHintRoutine);
        }

        failHintRoutine = StartCoroutine(FailHintRoutine(message));
    }

    /// <summary>FailHintRoutine 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.ShowFailHint(string)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="message">中文含义：消息。使用位置：StorageBoxUI.FailHintRoutine(string)。</param>
    IEnumerator FailHintRoutine(string message)
    {
        failHintText.gameObject.SetActive(true);
        failHintText.text = message;
        failHintText.color = new Color(1f, 0.4f, 0.4f, 1f);
        // elapsed（已用）：使用位置：StorageBoxUI.FailHintRoutine(string)。
float elapsed = 0f;
        while (elapsed < failHintDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // t（当前界面动画的归一化进度）：使用位置：StorageBoxUI.FailHintRoutine(string)。
float t = elapsed / failHintDuration;
            // alpha（透明度）：使用位置：作为 Color.Color(float, float, float, float) 的参数、StorageBoxUI.FailHintRoutine(string)。
float alpha;
            if (t < 0.2f)
            {
                alpha = t / 0.2f;
            }
            else
            {
                // selectedValue20（分支选出的1f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：StorageBoxUI.FailHintRoutine(string)。
float selectedValue20;
                if (t > 0.7f)
                {
                    selectedValue20 = (1f - t) / 0.3f;
                }
                else
                {
                    selectedValue20 = 1f;
                }

                alpha = (selectedValue20);
            }

            failHintText.color = new Color(1f, 0.4f, 0.4f, alpha);
            yield return null;
        }

        failHintText.gameObject.SetActive(false);
    }

    /// <summary>RequestStorageRefresh 的业务处理入口；本方法直接依赖 StoredItemPreview.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StoredItemPreview.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RequestStorageRefresh()
    {
        storageRefreshPending = true;
        if (itemPreview != null)
        {
            itemPreview.Clear();
        }
    }

    /// <summary>RequestInventoryRefresh 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RequestInventoryRefresh()
    {
        inventoryRefreshPending = true;
    }
}
