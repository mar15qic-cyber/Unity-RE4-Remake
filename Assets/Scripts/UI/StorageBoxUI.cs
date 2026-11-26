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
    /// <summary>界面视觉主题配置。</summary>
    [Header("统一视觉主题")]
    public UIVisualTheme visualTheme;
    /// <summary>物品预览。</summary>
    [Header("物品三维预览")]
    [UnityEngine.Serialization.FormerlySerializedAs("物品预览")]
    [UnityEngine.InspectorName("物品预览")]
    public StoredItemPreview itemPreview;
    /// <summary>实例。</summary>
    public static StorageBoxUI Instance { get; private set; }

    /// <summary>面板。</summary>
    [Header("面板引用")]
    public CanvasGroup panel;
    /// <summary>仓库列表容器。</summary>
    public RectTransform storageListContainer;
    /// <summary>背包网格容器。</summary>
    public RectTransform inventoryGridContainer;
    /// <summary>留空时自动使用箱子滚动区域。背包物品拖入箱子时使用的投放区域；留空时自动使用箱子滚动区域。</summary>
    [Tooltip("背包物品拖入箱子时使用的投放区域；留空时自动使用箱子滚动区域。")]
    public RectTransform storageDropArea;
    /// <summary>标题文字。</summary>
    [Header("标题")]
    public Text titleText;
    /// <summary>仓库标题文字。</summary>
    public Text storageTitleText;
    /// <summary>背包标题文字。</summary>
    public Text inventoryTitleText;
    /// <summary>操作提示栏文字。</summary>
    [Header("提示")]
    public Text hintText;
    /// <summary>界面淡入淡出的持续时间。</summary>
    [Header("动画")]
    public float fadeDuration = 0.25f;
    /// <summary>背包网格视图组件。右侧背包网格视图组件。</summary>
    [Header("共享网格视图")]
    [Tooltip("右侧背包网格视图组件")]
    public InventoryGridView gridView;
    /// <summary>左侧箱子区域投放高亮 Image。</summary>
    [Header("投放高亮")]
    [Tooltip("左侧箱子区域投放高亮 Image")]
    public Image dropZoneHighlight;
    /// <summary>投放高亮颜色。</summary>
    [Tooltip("投放高亮颜色")]
    public Color dropZoneColor = new Color(0.15f, 1f, 0.35f, 0.3f);
    /// <summary>右侧背包区域投放高亮 Image。</summary>
    [Tooltip("右侧背包区域投放高亮 Image")]
    public Image inventoryDropZoneHighlight;
    /// <summary>右侧投放高亮颜色。</summary>
    [Tooltip("右侧投放高亮颜色")]
    public Color inventoryDropZoneColor = new Color(0.3f, 0.6f, 1f, 0.3f);
    /// <summary>操作失败提示文本。</summary>
    [Header("失败提示")]
    [Tooltip("操作失败提示文本")]
    public Text failHintText;
    /// <summary>失败提示持续时间。</summary>
    public float failHintDuration = 1.5f;
    /// <summary>拖拽时跟随光标的图标（留空则自动创建）。</summary>
    [Header("拖拽幽灵图标")]
    [Tooltip("拖拽时跟随光标的图标（留空则自动创建）")]
    public Image dragGhost;
    /// <summary>拖拽幽灵预览矩形变换。</summary>
    private RectTransform dragGhostRt;
    /// <summary>是否可见。</summary>
    private bool isVisible;
    /// <summary>是否可见。</summary>
    public bool IsVisible => isVisible;

    /// <summary>淡入淡出协程。</summary>
    private Coroutine fadeRoutine;
    /// <summary>失败提示协程。</summary>
    private Coroutine failHintRoutine;
    /// <summary>生效控制器。</summary>
    private StorageBoxController activeController;
    /// <summary>是否网格警告已显示。</summary>
    private bool gridWarningShown;
    /// <summary>已订阅仓库管理器。</summary>
    private StorageBoxManager subscribedStorageManager;
    // 复用寄存箱行，隐藏时只保留一次待刷新标记。
    /// <summary>仓库条目集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly System.Collections.Generic.Dictionary<GridItem, GameObject> storageEntries = new System.Collections.Generic.Dictionary<GridItem, GameObject>();
    /// <summary>池化的仓库条目集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly System.Collections.Generic.List<GameObject> pooledStorageEntries = new System.Collections.Generic.List<GameObject>();
    /// <summary>仓库刷新是否仍待处理。</summary>
    private bool storageRefreshPending;
    /// <summary>背包刷新是否仍待处理。</summary>
    private bool inventoryRefreshPending;
    /// <summary>是否可复用的仓库条目集合缓存。</summary>
    private bool reusableStorageEntriesCached;
    /// <summary>已订阅背包管理器。</summary>
    private InventoryManager subscribedInventoryManager;
    // 拖拽上下文
    /// <summary>拖拽上下文。</summary>
    private DragContext dragContext;
    /// <summary>界面交互与显示的结构化数据 DragContext；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    struct DragContext
    {
        /// <summary>当前背包网格物品。</summary>
        public GridItem item;
        /// <summary>来源网格来源。</summary>
        public InventoryGridView.GridSource source;
        /// <summary>原始位置。</summary>
        public Vector2 originalPos;
        /// <summary>是否已完成。</summary>
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
    /// <param name="controller">当前仓库箱体控制器。</param>
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

        // 加载器。
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
        // 判空前缓存的仓库箱体管理器的实例。
global::StorageBoxManager receiverValue1 = StorageBoxManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Open();
        }

        // 判空前缓存的游戏界面管理器的实例。
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
            // 赋值前保存的目标背包网格视图组件。
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
        // 仓库数据与操作宿主。
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

        // 背包数据与操作宿主。
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
    /// <param name="objectName">对象名称。</param>
    RectTransform FindRectTransform(string objectName)
    {
        // 矩形集合。
RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        // 当前遍历索引。
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

        // 判空前缓存的仓库箱体管理器的实例。
global::StorageBoxManager receiverValue5 = StorageBoxManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.Close();
        }

        // 判空前缓存的游戏界面管理器的实例。
global::GameUIManager receiverValue6 = GameUIManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.UnregisterModal(this);
        }

        // 判空前缓存的生效控制器。
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

        // 已用。
float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // 当前界面动画的归一化进度。
float t = elapsed / fadeDuration;
            // 缓动后的。
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

        // 已用。
float elapsed = 0f;
        // 起始透明度。
float startAlpha = panel.alpha;
        while (elapsed < fadeDuration)
        {
            // 当前界面动画的归一化进度。
float t = elapsed / fadeDuration;
            // 缓动后的。
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
        // 期望物品列表。
var desiredItems = new System.Collections.Generic.List<GridItem>();
        // 当前仓库管理器。
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
        // 生效物品列表。
var activeItems = new System.Collections.Generic.List<GridItem>(storageEntries.Keys);
        // 当前遍历索引。
for (int i = 0; i < activeItems.Count; i++)
        {
            // 生效物品。
GridItem activeItem = activeItems[i];
            if (desiredItems.Contains(activeItem))
            {
                continue;
            }

            // 过期的入口。
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

        // 当前遍历索引。
for (int i = 0; i < desiredItems.Count; i++)
        {
            // 背包网格物品。
GridItem gi = desiredItems[i];
            // 当前游戏对象。
GameObject entry;
            if (storageEntries.TryGetValue(gi, out entry))
            {
                ConfigureStorageEntry(entry, gi, false);
            }
            else if (pooledStorageEntries.Count > 0)
            {
                // 上一次索引。
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
    /// <param name="gi">背包网格物品。</param>
    GameObject CreateStorageEntry(GridItem gi)
    {
        // 当前游戏对象。
GameObject go = new GameObject("StorageEntry", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(Image), typeof(EventTrigger));
        go.transform.SetParent(storageListContainer, false);
        // 布局分组。
HorizontalLayoutGroup layoutGroup = go.GetComponent<HorizontalLayoutGroup>();
        layoutGroup.spacing = 12f;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = true;
        layoutGroup.padding = new RectOffset(10, 10, 4, 4);
        // 布局元素。
LayoutElement layoutElement = go.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 44f;
        layoutElement.flexibleWidth = 1f;
        // 背景。
Image background = go.GetComponent<Image>();
        background.raycastTarget = true;
        // 矩形。
RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 44f);
        // 图标对象。
GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(go.transform, false);
        // 图标布局。
LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 64f;
        iconLayout.preferredHeight = 36f;
        iconLayout.flexibleWidth = 0f;
        iconObject.GetComponent<Image>().raycastTarget = false;
        // 标签对象。
GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(go.transform, false);
        // 标签布局。
LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.preferredHeight = 44f;
        // 标签。
Text label = labelObject.GetComponent<Text>();
        // 赋值前保存的目标标签。
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
    /// <param name="go">当前游戏对象。</param>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="rebindCallbacks">是否重新绑定回调集合。</param>
    void ConfigureStorageEntry(GameObject go, GridItem gi, bool rebindCallbacks)
    {
        if (go == null || gi == null || gi.item == null)
        {
            return;
        }

        go.name = "Storage_" + gi.item.displayName;
        // 普通状态颜色。
Color normalColor = Color.clear;
        // 悬停颜色。
Color hoverColor = new Color(.75f, .77f, .73f, .52f);
        // 背景。
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

        // 图标变换。
Transform iconTransform = go.transform.Find("Icon");
        // 图标。
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
            // 赋值前保存的目标图标。
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

        // 标签变换。
Transform labelTransform = go.transform.Find("Label");
        // 标签。
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

        // 触发器。
EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = go.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();
        // 进入入口。
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

            // 图像。
Image image = go.GetComponent<Image>();
            if (image != null)
            {
                image.color = hoverColor;
            }
        });
        trigger.triggers.Add(enterEntry);
        // 退出入口。
EventTrigger.Entry exitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        exitEntry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            // 图像。
Image image = go.GetComponent<Image>();
            if (image != null)
            {
                image.color = normalColor;
            }
        });
        trigger.triggers.Add(exitEntry);
        // 向下入口。
EventTrigger.Entry downEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerDown
        };
        downEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
        {
            // 指针。
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
        // 拖拽入口。
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

            // 指针。
PointerEventData pointer = (PointerEventData)e;
            UpdateDragGhostPos(pointer.position);
            // 是否上方网格。
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
        // 向上入口。
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
            // 指针。
PointerEventData pointer = (PointerEventData)e;
            if (inventoryDropZoneHighlight != null)
            {
                inventoryDropZoneHighlight.gameObject.SetActive(false);
            }

            // 仓库数据与操作宿主。
StorageBoxManager storageManager = StorageBoxManager.Instance;
            if (IsPointerOverInventoryGrid(pointer.position) && storageManager != null && !storageManager.TransferToInventory(gi))
            {
                ShowFailHint("背包空间不足");
            }

            dragContext.item = null;
        });
        trigger.triggers.Add(upEntry);
        // 点击入口。
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

            // 指针。
PointerEventData pointer = (PointerEventData)e;
            // 仓库数据与操作宿主。
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
        // 当前遍历索引。
for (int i = storageListContainer.childCount - 1; i >= 0; i--)
        {
            // 子级。
Transform child = storageListContainer.GetChild(i);
            if (!child.name.StartsWith("Storage_", System.StringComparison.Ordinal))
            {
                continue;
            }

            // 当前游戏对象。
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
    /// <param name="entry">当前游戏对象。</param>
    void ReleaseStorageEntry(GameObject entry)
    {
        if (entry == null)
        {
            return;
        }

        // 触发器。
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
        // 当前背包管理器。
var mgr = InventoryManager.Instance;
        if (mgr == null || gridView == null)
        {
            return;
        }

        // 生效拖拽物品。
GridItem activeDragItem;
        if (dragContext.source == InventoryGridView.GridSource.Inventory)
        {
            activeDragItem = dragContext.item;
        }
        else
        {
            activeDragItem = null;
        }

        // 拖拽视觉表现位置。
Vector2 dragVisualPosition = Vector2.zero;
        // 是否此前具有拖拽视觉表现。
bool hadDragVisual = false;
        if (activeDragItem != null)
        {
            // 拖拽界面控件。
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

        // 刷新后的拖拽界面控件。
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
    /// <param name="gi">背包网格物品。</param>
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
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
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
        // 是否上方掉落区。
bool overDropZone = IsPointerOverStorageList(screenPos);
        if (dropZoneHighlight != null)
        {
            dropZoneHighlight.gameObject.SetActive(overDropZone);
            dropZoneHighlight.color = dropZoneColor;
        }
    }

    /// <summary>OnInventoryItemPointerUp 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.IsPointerOverStorageList(Vector2)、StorageBoxUI.RequestInventoryRefresh()、StorageBoxUI.ShowFailHint(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、StorageBoxManager.TransferToStorage(GridItem)、StorageBoxUI.IsPointerOverStorageList(Vector2)、StorageBoxUI.RequestInventoryRefresh()、StorageBoxUI.ShowFailHint(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
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

        // 是否上方掉落区。
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
    /// <param name="gi">背包网格物品。</param>
    /// <param name="button">按钮。</param>
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
    /// <param name="gi">背包网格物品。</param>
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

        // 画布。
var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        // 当前游戏对象。
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
    /// <param name="gi">背包网格物品。</param>
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
    /// <param name="screenPos">屏幕位置。</param>
    void UpdateDragGhostPos(Vector2 screenPos)
    {
        if (dragGhostRt == null)
        {
            return;
        }

        // 父级。
var parent = dragGhostRt.parent as RectTransform;
        if (parent == null)
        {
            return;
        }

        // 局部位置。
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
    /// <param name="screenPos">屏幕位置。</param>
    bool IsPointerOverStorageList(Vector2 screenPos)
    {
        // 目标界面矩形变换。
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

        // 画布。
Canvas canvas = target.GetComponentInParent<Canvas>();
        // 事件摄像机。
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
    /// <param name="screenPos">屏幕位置。</param>
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
    /// <param name="gi">背包网格物品。</param>
    Vector2 GetItemAnchoredPos(GridItem gi)
    {
        // 界面控件。
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
    /// <param name="message">消息。</param>
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
    /// <param name="message">消息。</param>
    IEnumerator FailHintRoutine(string message)
    {
        failHintText.gameObject.SetActive(true);
        failHintText.text = message;
        failHintText.color = new Color(1f, 0.4f, 0.4f, 1f);
        // 已用。
float elapsed = 0f;
        while (elapsed < failHintDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            // 当前界面动画的归一化进度。
float t = elapsed / failHintDuration;
            // 透明度。
float alpha;
            if (t < 0.2f)
            {
                alpha = t / 0.2f;
            }
            else
            {
                // 分支选出的1f。
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
