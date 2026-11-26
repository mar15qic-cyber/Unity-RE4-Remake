using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// RE4 公文包式背包 UI — 使用 InventoryGridView 共享网格视图
/// </summary>
/// <remarks>背包交互宿主，接收网格、拖放、使用和配方意图，通过已有服务提交；显示与模态占用分别读取规则结果并释放自身许可。</remarks>
public partial class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    /// <summary>公文包与物品的三维展示组件。</summary>
    [Header("三维公文包")]
    [UnityEngine.Serialization.FormerlySerializedAs("三维展示")]
    [UnityEngine.InspectorName("三维展示")]
    public AttacheCasePresentationController threeDimensionalPresentation;
    /// <summary>当前展示是否允许交互。</summary>
    public bool Interactive => isVisible && inventoryPanel != null && inventoryPanel.interactable
        && (threeDimensionalPresentation == null || threeDimensionalPresentation.Interactive);

    /// <summary>主摄像机。</summary>
    [Header("相机")]
    public Camera mainCamera;
    /// <summary>背包摄像机。</summary>
    public Camera inventoryCamera;
    /// <summary>背包皮革外壳图片。</summary>
    [Header("分层贴图素材")]
    public Sprite skinShell;
    /// <summary>背包泡棉网格图片。</summary>
    public Sprite foamGrid;
    /// <summary>背包金属边框图片。</summary>
    public Sprite metalFrame;
    /// <summary>背包格子的高亮图片。</summary>
    public Sprite cellHighlight;
    /// <summary>背包分页页签图片。</summary>
    public Sprite tabPage;
    /// <summary>显示背包皮革外壳的图像组件。</summary>
    [Header("贴图引用")]
    public Image skinShellImage;
    /// <summary>显示背包泡棉网格的图像组件。</summary>
    public Image foamGridImage;
    /// <summary>显示背包金属边框的图像组件。</summary>
    public Image metalFrameImage;
    /// <summary>显示背包格子高亮的图像组件。</summary>
    public Image cellHighlightImage;
    /// <summary>显示背包分页页签的图像组件。</summary>
    public Image tabPageImage;
    /// <summary>背包面板的画布组。</summary>
    [Header("面板引用")]
    public CanvasGroup inventoryPanel;
    /// <summary>背包网格视图组件。</summary>
    [Header("共享网格视图")]
    [Tooltip("背包网格视图组件")]
    public InventoryGridView gridView;
    /// <summary>物品合成分页组件。</summary>
    [Header("合成分页")]
    public InventoryCraftingPanel craftingPanel;
    /// <summary>物品说明面板。</summary>
    [Header("描述面板")]
    public RectTransform descriptionPanel;
    /// <summary>物品说明面板中的物品图标。</summary>
    public Image descIcon;
    /// <summary>物品说明面板中的名称文字。</summary>
    public Text descName;
    /// <summary>物品说明面板中的详细描述文字。</summary>
    public Text descDescription;
    /// <summary>物品说明面板中的数量文字。</summary>
    public Text descCount;
    /// <summary>使用物品按钮。</summary>
    public Button useButton;
    /// <summary>未选中物品时的提示文字。未选中时的提示文本。</summary>
    [Tooltip("未选中时的提示文本")]
    public Text emptyHintText;
    /// <summary>操作提示栏文字。</summary>
    [Header("提示栏")]
    public Text hintText;
    /// <summary>物品上下文菜单。</summary>
    [Header("右键上下文菜单")]
    public RectTransform contextMenu;
    /// <summary>上下文菜单中的使用按钮。</summary>
    public Button ctxUseButton;
    /// <summary>上下文菜单中的丢弃按钮。</summary>
    public Button ctxDiscardButton;
    /// <summary>上下文菜单中的寄存按钮。</summary>
    public Button ctxStoreButton;
    /// <summary>上下文菜单使用按钮上的文字。</summary>
    public Text ctxUseButtonText;
    /// <summary>自定义光标图片。</summary>
    [Header("自定义光标")]
    public Sprite cursorSprite;
    /// <summary>自定义光标的图像组件。</summary>
    public Image cursorImage;
    /// <summary>界面视觉主题配置。可选。赋值后，两个使用按钮会读取主题中的按钮图片状态。</summary>
    [Header("界面主题")]
    [Tooltip("可选。赋值后，两个使用按钮会读取主题中的按钮图片状态")]
    public UIVisualTheme visualTheme;
    /// <summary>使用按钮未悬停时显示的美术素材。</summary>
    [Header("使用按钮样式")]
    [Tooltip("使用按钮未悬停时显示的美术素材")]
    public Sprite actionButtonNormalSprite;
    /// <summary>使用按钮悬停时显示的美术素材。</summary>
    [Tooltip("使用按钮悬停时显示的美术素材")]
    public Sprite actionButtonHoverSprite;
    /// <summary>动作普通状态颜色。</summary>
    public Color actionNormalColor = new Color(0.15f, 0.4f, 0.15f, 1f);
    /// <summary>动作高亮状态颜色。</summary>
    public Color actionHighlightedColor = new Color(0.28f, 0.65f, 0.24f, 1f);
    /// <summary>动作按下状态颜色。</summary>
    public Color actionPressedColor = new Color(0.07f, 0.2f, 0.07f, 1f);
    /// <summary>动作禁用状态颜色。</summary>
    public Color actionDisabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
    /// <summary>拾取通知文字。</summary>
    [Header("拾取通知")]
    public Text toastText;
    /// <summary>拾取通知的显示时长。</summary>
    public float toastDuration = 2f;
    /// <summary>界面淡入淡出的持续时间。</summary>
    [Header("动画")]
    public float fadeDuration = 0.25f;
    /// <summary>高亮颜色。</summary>
    [Header("选中高亮颜色（传给 gridView）")]
    public Color highlightColor = new Color(0.15f, 1f, 0.35f, 0.85f);
    /// <summary>中文含义：选中高亮边缘的外扩留白。</summary>
    public float highlightPadding = 2f;
    /// <summary>是否可见。</summary>
    private bool isVisible;
    /// <summary>中文含义：是否可见。</summary>
    public bool IsVisible => isVisible;

    /// <summary>选定背包网格物品。</summary>
    private GridItem selectedGridItem;
    /// <summary>被悬停的背包网格物品。</summary>
    private GridItem hoveredGridItem;
    /// <summary>淡入淡出协程。</summary>
    private Coroutine fadeRoutine;
    /// <summary>短时通知协程。</summary>
    private Coroutine toastRoutine;
    /// <summary>是否网格警告已显示。</summary>
    private bool gridWarningShown;
    // 拖拽状态
    /// <summary>拖拽中物品。</summary>
    private GridItem draggingItem;
    /// <summary>拖拽偏移量。</summary>
    private Vector2 dragOffset;
    /// <summary>拖拽是否已完成。</summary>
    private bool dragCompleted;
    private bool dragMoved;
    // 隐藏期间只记录脏标记，同一帧的多次事件合并一次。
    /// <summary>刷新是否仍待处理。</summary>
    private bool refreshPending;
    /// <summary>上一次网格区域。</summary>
    private Vector2 lastMeshRegion;
    /// <summary>上下文菜单物品。</summary>
    private GridItem contextMenuItem;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponentInChildren&lt;InventoryCraftingPanel&gt;(bool)、GameObject.SetActive(bool)、InventoryCraftingPanel.Initialize(InventoryUI)、InventoryUI.ApplyActionButtonStyle()、InventoryUI.ApplySkinTextures()、InventoryUI.ConfigureActionButtons()、InventoryUI.ConfigureCursorImage() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        // 背包的全屏底色只负责绘制，关闭背包时同画布的拾取提示仍会保留。
        Transform backdrop = transform.Find("DarkOverlay");
        if (backdrop != null && backdrop.TryGetComponent<Graphic>(out var backdropGraphic))
        {
            backdropGraphic.raycastTarget = false;
        }

        if (!isVisible && inventoryPanel != null)
        {
            inventoryPanel.alpha = 0f;
            inventoryPanel.blocksRaycasts = false;
            inventoryPanel.interactable = false;
        }
        if (gridView == null && (FindRectTransform("GridContainer") == null || FindRectTransform("GridArea") == null))
        {
            return;
        }
        ApplySkinTextures();
        ResolveGridView();
        if (toastText != null)
        {
            toastText.gameObject.SetActive(false);
        }

        if (emptyHintText != null)
        {
            emptyHintText.gameObject.SetActive(true);
        }

        if (contextMenu != null)
        {
            contextMenu.gameObject.SetActive(false);
        }

        ConfigureActionButtons();
        ApplyActionButtonStyle();
        ConfigureCursorImage();
        if (craftingPanel == null)
        {
            craftingPanel = GetComponentInChildren<InventoryCraftingPanel>(true);
        }

        // 判空前缓存的物品合成分页组件。
global::InventoryCraftingPanel receiverValue1 = craftingPanel;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            receiverValue1.Initialize(this);
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        ConfigureGridCallbacks();
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RequestRefreshGrid;
        }
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.ClearToast()、Singleton&lt;InventoryUI&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void OnDestroy()
    {
        ClearToast();
        if (InventoryManager.HasInstance)
        {
            InventoryManager.Instance.OnInventoryChanged -= RequestRefreshGrid;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.ClearToast()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        ClearToast();
    }

    /// <summary>ApplySkinTextures 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.Start()；下游（含内部回调体） GameObject.SetActive(bool)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplySkinTextures()
    {
        if (skinShellImage != null)
        {
            skinShellImage.raycastTarget = false;
            if (skinShell != null)
            {
                skinShellImage.sprite = skinShell;
                skinShellImage.color = Color.white;
            }
        }

        if (foamGridImage != null)
        {
            foamGridImage.raycastTarget = false;
            if (foamGrid != null)
            {
                foamGridImage.sprite = foamGrid;
                foamGridImage.color = Color.white;
            }
        }

        if (metalFrameImage != null)
        {
            metalFrameImage.raycastTarget = false;
            if (metalFrame != null)
            {
                metalFrameImage.sprite = metalFrame;
                metalFrameImage.color = Color.white;
            }
        }

        if (cellHighlightImage != null)
        {
            cellHighlightImage.raycastTarget = false;
            if (cellHighlight != null)
            {
                cellHighlightImage.sprite = cellHighlight;
                cellHighlightImage.color = Color.white;
            }

            // 判空前缓存的显示背包格子高亮的图像组件。
global::UnityEngine.UI.Image receiverValue2 = cellHighlightImage;
            if (!global::System.Object.ReferenceEquals(receiverValue2, null))
            {
                receiverValue2.gameObject.SetActive(false);
            }
        }

        if (tabPageImage != null)
        {
            tabPageImage.raycastTarget = false;
            if (tabPage != null)
            {
                tabPageImage.sprite = tabPage;
                tabPageImage.color = Color.white;
            }
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.UpdateCursor()、InventoryUI.UpdateHighlightPulse()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!isVisible)
        {
            return;
        }

        UpdateCursor();
        UpdateItemShortcuts();
        UpdateHighlightPulse();
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.RefreshGridInternal()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (!isVisible)
        {
            return;
        }

        if (gridView != null && gridView.gridArea != null && gridView.gridArea.rect.size != lastMeshRegion)
        {
            lastMeshRegion = gridView.gridArea.rect.size;
            refreshPending = true;
        }

        if (!refreshPending)
        {
            return;
        }

        refreshPending = false;
        RefreshGridInternal();
    }

    /// <summary>UpdateHighlightPulse 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateHighlightPulse()
    {
    // gridView 内部管理 overlay 显隐，此处仅做脉动
    }

    /// <summary>UpdateCursor 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InputControl&lt;Vector2&gt;.ReadValue()、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.Update()；下游（含内部回调体） GameObject.SetActive(bool)、InputControl&lt;Vector2&gt;.ReadValue()、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateCursor()
    {
        if (cursorImage == null || cursorSprite == null)
        {
            return;
        }

        // 光标图层只负责显示，不能挡住按钮和网格的射线检测。
        cursorImage.raycastTarget = false;
        // 鼠标位置。
Vector2 mousePos;
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }
        else
        {
            mousePos = new Vector2(UnityEngine.Input.mousePosition.x, UnityEngine.Input.mousePosition.y);
        }

        cursorImage.gameObject.SetActive(true);
        // 界面矩形变换。
var rt = (RectTransform)cursorImage.transform;
        // 父级矩形变换。
var parentRt = rt.parent as RectTransform;
        // 局部位置。
Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, mousePos, null, out localPos);
        rt.anchoredPosition = localPos;
    }

    /// <summary>OnSceneLoaded 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.Show()、GameObject.SetActive(bool)、InventoryCraftingPanel.Refresh()、InventoryCraftingPanel.ShowItemsPage()、InventoryUI.ApplyActionButtonStyle()、InventoryUI.ClearToast()、InventoryUI.ConfigureActionButtons() 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.LoadInventoryScene()；下游（含内部回调体） AttacheCasePresentationController.Show()、GameObject.SetActive(bool)、InventoryCraftingPanel.Refresh()、InventoryCraftingPanel.ShowItemsPage()、InventoryUI.ApplyActionButtonStyle()、InventoryUI.ClearToast()、InventoryUI.ConfigureActionButtons() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnSceneLoaded()
    {
        Instance = this;
        isVisible = true;
        if (craftingPanel != null)
        {
            craftingPanel.Initialize(this);
        }
        refreshPending = false;
        ClearToast();
        ResolveGridView();
        ConfigureGridCallbacks();
        ConfigureActionButtons();
        ApplyActionButtonStyle();
        ConfigureCursorImage();
        // 判空前缓存的物品合成分页组件。
global::InventoryCraftingPanel receiverValue3 = craftingPanel;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.ShowItemsPage();
        }

        // 判空前缓存的物品合成分页组件。
global::InventoryCraftingPanel receiverValue4 = craftingPanel;
        if (!global::System.Object.ReferenceEquals(receiverValue4, null))
        {
            receiverValue4.Refresh();
        }

        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue5 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            receiverValue5.ShowCustomCursor();
        }

        if (inventoryCamera != null)
        {
            inventoryCamera.enabled = true;
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.alpha = 0f;
            inventoryPanel.interactable = false;
            inventoryPanel.blocksRaycasts = false;
            inventoryPanel.gameObject.SetActive(true);
        }

        // 物品操作菜单自行设置各行的底色、选中和禁用状态。
        MenuThemeApplier.Apply(transform.root, visualTheme, contextMenu);
        BuildControlHints();
        // 判空前缓存的公文包与物品的三维展示组件。
global::AttacheCasePresentationController receiverValue6 = threeDimensionalPresentation;
        if (!global::System.Object.ReferenceEquals(receiverValue6, null))
        {
            receiverValue6.Show();
        }

        // 首次打开需要立即同步当前数据，后续事件统一在本帧末合并刷新。
        RefreshGridInternal();
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeIn());
    }

    /// <summary>ResolveGridView 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponentInChildren&lt;InventoryGridView&gt;(bool)、Debug.LogError(object, Object)、GameObject.AddComponent&lt;InventoryGridView&gt;()、InventoryUI.FindRectTransform(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()、InventoryUI.Start()；下游（含内部回调体） Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponentInChildren&lt;InventoryGridView&gt;(bool)、Debug.LogError(object, Object)、GameObject.AddComponent&lt;InventoryGridView&gt;()、InventoryUI.FindRectTransform(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
            gridView.gridContainer = FindRectTransform("GridContainer");
        }

        if (gridView.gridArea == null)
        {
            gridView.gridArea = FindRectTransform("GridArea");
        }

        if ((gridView.gridContainer == null || gridView.gridArea == null) && !gridWarningShown)
        {
            gridWarningShown = true;
            Debug.LogError("[背包界面] 无法恢复网格区域引用，请检查 GridContainer 和 GridArea。", this);
        }
    }

    /// <summary>ConfigureGridCallbacks 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()、InventoryUI.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConfigureGridCallbacks()
    {
        if (gridView == null)
        {
            return;
        }

        gridView.sourceType = InventoryGridView.GridSource.Inventory;
        gridView.onItemPointerDown = OnItemPointerDown;
        gridView.onItemDrag = OnItemDrag;
        gridView.onItemPointerUp = OnItemPointerUp;
        gridView.onItemClicked = OnItemClicked;
        gridView.onCellClicked = OnCellClicked;
        gridView.onItemHoverEnter = OnItemHoverEnter;
        gridView.onItemHoverExit = OnItemHoverExit;
    }

    /// <summary>FindRectTransform 的业务处理入口；本方法直接依赖 Component.GetComponentsInChildren&lt;RectTransform&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ResolveGridView()；下游（含内部回调体） Component.GetComponentsInChildren&lt;RectTransform&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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

    /// <summary>PlayExitAnimation 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.EndDrag()、InventoryGridView.HideHover()、InventoryGridView.HideSelected()、InventoryUI.ClearDragState()、InventoryUI.ClearToast()、InventoryUI.FadeOutAndCallback(Action)、InventoryUI.HideContextMenu() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventorySceneLoader.HideInventoryScene()；下游（含内部回调体） AttacheCasePresentationController.EndDrag()、InventoryGridView.HideHover()、InventoryGridView.HideSelected()、InventoryUI.ClearDragState()、InventoryUI.ClearToast()、InventoryUI.FadeOutAndCallback(Action)、InventoryUI.HideContextMenu() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="onComplete">完成回调。</param>
    public void PlayExitAnimation(System.Action onComplete)
    {
        isVisible = false;
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (inventoryPanel != null)
        {
            inventoryPanel.interactable = false;
            inventoryPanel.blocksRaycasts = false;
        }

        ClearDragState();
        hoveredGridItem = null;
        // 判空前缓存的公文包与物品的三维展示组件。
global::AttacheCasePresentationController receiverValue7 = threeDimensionalPresentation;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            receiverValue7.EndDrag();
        }

        RefreshGridInternal();
        ClearToast();
        selectedGridItem = null;
        HideContextMenu();
        // 判空前缓存的背包网格视图组件。
global::InventoryGridView receiverValue8 = gridView;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.HideSelected();
        }

        // 判空前缓存的背包网格视图组件。
global::InventoryGridView receiverValue9 = gridView;
        if (!global::System.Object.ReferenceEquals(receiverValue9, null))
        {
            receiverValue9.HideHover();
        }

        // 判空前缓存的界面光标管理器的实例。
global::UICursorManager receiverValue10 = UICursorManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue10, null))
        {
            receiverValue10.HideCustomCursor();
        }

        StartCoroutine(FadeOutAndCallback(onComplete));
    }

    /// <summary>FadeOutAndCallback 的业务处理入口；本方法直接依赖 Action.Invoke()、AttacheCasePresentationController.CloseLid()、GameObject.SetActive(bool)、InventoryUI.FadeOut()、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.PlayExitAnimation(Action)；下游（含内部回调体） Action.Invoke()、AttacheCasePresentationController.CloseLid()、GameObject.SetActive(bool)、InventoryUI.FadeOut()、MonoBehaviour.StartCoroutine(IEnumerator)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="onComplete">完成回调。</param>
    IEnumerator FadeOutAndCallback(System.Action onComplete)
    {
        if (threeDimensionalPresentation != null)
        {
            yield return threeDimensionalPresentation.CloseLid();
        }

        yield return StartCoroutine(FadeOut());
        // 判空前缓存的完成回调。
global::System.Action receiverValue11 = onComplete;
        if (!global::System.Object.ReferenceEquals(receiverValue11, null))
        {
            receiverValue11.Invoke();
        }

        // 完成回调必须先于对象停用，否则承载该协程的对象会让回调永远无法执行。
        if (inventoryPanel != null)
        {
            inventoryPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>FadeIn 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeIn()
    {
        if (inventoryPanel == null)
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
            inventoryPanel.alpha = eased;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        inventoryPanel.alpha = 1f;
        while (threeDimensionalPresentation != null && !threeDimensionalPresentation.Interactive)
        {
            yield return null;
        }

        inventoryPanel.interactable = isVisible;
        inventoryPanel.blocksRaycasts = isVisible;
    }

    /// <summary>FadeOut 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.FadeOutAndCallback(Action)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    IEnumerator FadeOut()
    {
        if (inventoryPanel == null)
        {
            yield break;
        }

        // 已用。
float elapsed = 0f;
        // 起始透明度。
float startAlpha = inventoryPanel.alpha;
        while (elapsed < fadeDuration)
        {
            // 当前界面动画的归一化进度。
float t = elapsed / fadeDuration;
            // 缓动后的。
float eased = 1f - t * t * t;
            inventoryPanel.alpha = startAlpha * eased;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        inventoryPanel.alpha = 0f;
        inventoryPanel.interactable = false;
        inventoryPanel.blocksRaycasts = false;
    }

    // ========================== 网格刷新 ==========================
    /// <summary>RefreshGrid 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemPointerUp(GridItem, Vector2)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RefreshGrid()
    {
        refreshPending = true;
    }

    /// <summary>RefreshGridInternal 的业务处理入口；本方法直接依赖 InventoryCraftingPanel.Refresh()、InventoryGridView.GetItemWidget(GridItem)、InventoryGridView.HideSelected()、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.ShowSelected(GridItem) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.LateUpdate()、InventoryUI.OnSceneLoaded()、InventoryUI.PlayExitAnimation(Action)；下游（含内部回调体） InventoryCraftingPanel.Refresh()、InventoryGridView.GetItemWidget(GridItem)、InventoryGridView.HideSelected()、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.ShowSelected(GridItem) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RefreshGridInternal()
    {
        // 当前背包管理器。
var mgr = InventoryManager.Instance;
        if (mgr == null || gridView == null)
        {
            return;
        }

        // 生效拖拽物品。
GridItem activeDragItem = draggingItem;
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
        // 判空前缓存的物品合成分页组件。
global::InventoryCraftingPanel receiverValue12 = craftingPanel;
        if (!global::System.Object.ReferenceEquals(receiverValue12, null))
        {
            receiverValue12.Refresh();
        }

        if (activeDragItem != null)
        {
            // 刷新后的拖拽界面控件。
RectTransform refreshedDragWidget = gridView.GetItemWidget(activeDragItem);
            if (refreshedDragWidget == null)
            {
                ClearDragState();
            }
            else if (hadDragVisual && !dragCompleted)
            {
                // 刷新没有替换拖拽物品时，保留拖拽中的位置和透明度。
                gridView.SetItemWidgetPosition(activeDragItem, dragVisualPosition);
                gridView.SetItemDragAlpha(activeDragItem, true);
            }
        }

        if (selectedGridItem != null)
        {
            // 使用或整理后，原网格对象可能已经被移除，必须重新取得当前格子对象。
            // 当前物品。
GridItem currentItem = mgr.GetItemAt(selectedGridItem.gridX, selectedGridItem.gridY);
            if (currentItem != null)
            {
                selectedGridItem = currentItem;
                ShowDescription(currentItem);
                gridView.ShowSelected(currentItem);
            }
            else
            {
                selectedGridItem = null;
                ShowEmptyDescription();
                gridView.HideSelected();
            }
        }
        else
        {
            ShowEmptyDescription();
        }
    }

    // ========================== 拖拽 ==========================
    /// <summary>OnItemPointerDown 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.BeginDrag(GridItem)、InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.ShowSelected(GridItem)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、InventoryUI.HideContextMenu()、InventoryUI.ShowDescription(GridItem) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.BeginDrag(GridItem)、InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.ShowSelected(GridItem)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、InventoryUI.HideContextMenu()、InventoryUI.ShowDescription(GridItem) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
    void OnItemPointerDown(GridItem gi, Vector2 screenPos)
    {
        if (!keyboardItemMove)
        {
            BeginItemMove(gi, screenPos);
        }
    }

    void BeginItemMove(GridItem gi, Vector2 screenPos)
    {
        if (gi == null || !Interactive)
        {
            return;
        }

        // 判空前缓存的公文包与物品的三维展示组件。
global::AttacheCasePresentationController receiverValue13 = threeDimensionalPresentation;
        if (!global::System.Object.ReferenceEquals(receiverValue13, null))
        {
            receiverValue13.BeginDrag(gi);
        }

        if (selectedGridItem != gi)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        }

        selectedGridItem = gi;
        ShowDescription(gi);
        HideContextMenu();
        gridView.ShowSelected(gi);
        draggingItem = gi;
        dragCompleted = false;
        dragMoved = false;
        // 记录拖拽偏移
        if (gridView.TryScreenPointToLocal(screenPos, out Vector2 /* 中文含义：指针局部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pointerLocal))
        {
            // 物品位置。
Vector2 itemPos;
            gridView.GetItemRect(gi, gridView.CurrentCell, out itemPos, out _);
            dragOffset = pointerLocal - itemPos;
        }

        gridView.SetItemDragAlpha(gi, true);
    }

    /// <summary>OnItemDrag 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
    void OnItemDrag(GridItem gi, Vector2 screenPos)
    {
        if (!keyboardItemMove)
        {
            PreviewItemMove(gi, screenPos);
        }
    }

    void PreviewItemMove(GridItem gi, Vector2 screenPos)
    {
        if (draggingItem != gi || dragCompleted)
        {
            return;
        }

        // 跟随光标移动物品控件
        if (gridView.TryScreenPointToLocal(screenPos, out Vector2 /* 中文含义：局部坐标点。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ localPoint))
        {
            dragMoved = true;
            gridView.SetItemWidgetPosition(gi, localPoint - dragOffset);
            // 判空前缓存的公文包与物品的三维展示组件。
global::AttacheCasePresentationController receiverValue14 = threeDimensionalPresentation;
            if (!global::System.Object.ReferenceEquals(receiverValue14, null))
            {
                receiverValue14.PreviewLandPoint(gi, screenPos, dragOffset);
            }
        }
    }

    /// <summary>OnItemPointerUp 的业务处理入口；本方法直接依赖 AttacheCasePresentationController.EndDrag()、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)、InventoryManager.GetItemAt(int, int)、InventoryManager.MoveItemNear(GridItem, int, int, int)、InventoryManager.TryCombine(GridItem, GridItem, out ItemData)、InventoryUI.RefreshGrid() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AttacheCasePresentationController.EndDrag()、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)、InventoryManager.GetItemAt(int, int)、InventoryManager.MoveItemNear(GridItem, int, int, int)、InventoryManager.TryCombine(GridItem, GridItem, out ItemData)、InventoryUI.RefreshGrid() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
    void OnItemPointerUp(GridItem gi, Vector2 screenPos)
    {
        if (!keyboardItemMove)
        {
            CommitItemMove(gi, screenPos);
        }
    }

    void CommitItemMove(GridItem gi, Vector2 screenPos)
    {
        if (draggingItem != gi || dragCompleted)
        {
            return;
        }

        dragCompleted = true;
        // 判空前缓存的公文包与物品的三维展示组件。
global::AttacheCasePresentationController receiverValue15 = threeDimensionalPresentation;
        if (!global::System.Object.ReferenceEquals(receiverValue15, null))
        {
            receiverValue15.EndDrag();
        }

        gridView.SetItemDragAlpha(gi, false);
        if (!dragMoved && !keyboardItemMove)
        {
            draggingItem = null;
            dragCompleted = false;
            return;
        }

        // 计算落点格子
        if (gridView.TryGetDropGrid(gi, screenPos, dragOffset, out int /* 中文含义：目标横向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ targetX, out int /* 中文含义：目标纵向。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ targetY))
        {
            // 当前背包管理器。
var mgr = InventoryManager.Instance;
            if (mgr != null)
            {
                // 目标物品。
GridItem targetItem = mgr.GetItemAt(targetX, targetY);
                if (targetItem != null && mgr.TryCombine(gi, targetItem, out ItemData /* 中文含义：当前物品数据配置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ result))
                {
                    draggingItem = null;
                    dragMoved = false;
                    selectedGridItem = null;
                    ShowPickupToast("合成完成：" + result.displayName);
                    RefreshGrid();
                    return;
                }

                int originalX = gi.gridX;
                int originalY = gi.gridY;
                if (!mgr.MoveItemNear(gi, targetX, targetY, 1))
                {
                    if (dragMoved)
                    {
                        AudioManager.PlayInterface(AudioEventNames.UICaseError);
                    }

                    RefreshGrid();
                }
                else if (gi.gridX != originalX || gi.gridY != originalY)
                {
                    AudioManager.PlayInterface(AudioEventNames.UICaseManipulate);
                }
            }
        }
        else
        {
            if (dragMoved)
            {
                AudioManager.PlayInterface(AudioEventNames.UICaseError);
            }

            RefreshGrid();
        }

        draggingItem = null;
        dragMoved = false;
    }

    // ========================== 点击/悬停 ==========================
    /// <summary>OnItemClicked 的业务处理入口；本方法直接依赖 InventoryGridView.ShowSelected(GridItem)、InventoryUI.HideContextMenu()、InventoryUI.ShowContextMenu(GridItem, Vector2)、InventoryUI.ShowDescription(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.ShowSelected(GridItem)、InventoryUI.HideContextMenu()、InventoryUI.ShowContextMenu(GridItem, Vector2)、InventoryUI.ShowDescription(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="button">按钮。</param>
    /// <param name="screenPos">屏幕位置。</param>
    void OnItemClicked(GridItem gi, PointerEventData.InputButton button, Vector2 screenPos)
    {
        if (!Interactive || keyboardItemMove)
        {
            return;
        }

        if (dragCompleted)
        {
            dragCompleted = false;
            if (button == PointerEventData.InputButton.Left)
            {
                return;
            }
        }

        selectedGridItem = gi;
        ShowDescription(gi);
        gridView.ShowSelected(gi);
        bool hasDetailsPanel = descriptionPanel != null && descriptionPanel.gameObject.activeSelf;
        bool openItemCommands = button == PointerEventData.InputButton.Right
            || (button == PointerEventData.InputButton.Left && !hasDetailsPanel);
        if (openItemCommands)
        {
            ShowContextMenu(gi, screenPos);
        }
        else
        {
            HideContextMenu();
        }
    }

    /// <summary>OnCellClicked 的业务处理入口；本方法直接依赖 InventoryGridView.HideSelected()、InventoryManager.GetItemAt(int, int)、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.HideSelected()、InventoryManager.GetItemAt(int, int)、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">横向。</param>
    /// <param name="y">纵向。</param>
    void OnCellClicked(int x, int y)
    {
        if (keyboardItemMove)
        {
            return;
        }

        // 判空前缓存的背包管理器的实例。
global::InventoryManager receiverValue16 = InventoryManager.Instance;
        // 背包网格物品。
global::GridItem gi;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            gi = receiverValue16.GetItemAt(x, y);
        }
        else
        {
            gi = default;
        }

        if (gi == null)
        {
            selectedGridItem = null;
            ShowEmptyDescription();
            gridView.HideSelected();
        }

        if (contextMenu != null && contextMenu.gameObject.activeSelf)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseCancel);
        }

        HideContextMenu();
    }

    /// <summary>OnItemHoverEnter 的业务处理入口；本方法直接依赖 InventoryGridView.ShowHover(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.ShowHover(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    void OnItemHoverEnter(GridItem gi)
    {
        if (Interactive && gi != null && hoveredGridItem != gi && draggingItem == null)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseHover);
        }

        hoveredGridItem = gi;
        gridView.ShowHover(gi);
    }

    /// <summary>OnItemHoverExit 的业务处理入口；本方法直接依赖 InventoryGridView.HideHover()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.HideHover()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnItemHoverExit()
    {
        hoveredGridItem = null;
        gridView.HideHover();
    }

    /// <summary>旋转当前选中的物品</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnReload(InputAction.CallbackContext)；下游（含内部回调体） InventoryManager.RotateItem(GridItem)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RotateSelectedItem()
    {
        if (selectedGridItem == null || !Interactive)
        {
            return;
        }

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null && inventory.RotateItem(selectedGridItem))
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseManipulate);
        }
        else
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseError);
        }
    }

    // ========================== 描述面板 ==========================
    /// <summary>ShowDescription 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryManager.CanUseItem(ItemData)、InventoryManager.GetCraftingHint(ItemData)、InventoryUI.ResolveUseText(ItemData)、InventoryUI.SetButtonText(Button, string)、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemClicked(GridItem, PointerEventData.InputButton, Vector2)、InventoryUI.OnItemPointerDown(GridItem, Vector2)、InventoryUI.RefreshGridInternal()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryManager.CanUseItem(ItemData)、InventoryManager.GetCraftingHint(ItemData)、InventoryUI.ResolveUseText(ItemData)、InventoryUI.SetButtonText(Button, string)、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    void ShowDescription(GridItem gi)
    {
        if (descriptionPanel != null && !descriptionPanel.gameObject.activeSelf)
        {
            return;
        }

        if (emptyHintText != null)
        {
            emptyHintText.gameObject.SetActive(false);
        }

        if (descIcon != null)
        {
            descIcon.sprite = gi.item.icon;
            // 赋值前保存的目标物品说明面板中的物品图标。
global::UnityEngine.UI.Image assignmentReceiver20 = descIcon;
            if (gi.item.icon != null)
            {
                assignmentReceiver20.color = Color.white;
            }
            else
            {
                assignmentReceiver20.color = new Color(0.6f, 0.5f, 0.2f, 0.8f);
            }
        }

        if (descName != null)
        {
            descName.text = gi.item.displayName;
        }

        if (descDescription != null)
        {
            // 判空前缓存的背包管理器的实例。
global::InventoryManager receiverValue22 = InventoryManager.Instance;
            // 合成提示。
string craftingHint;
            if (!global::System.Object.ReferenceEquals(receiverValue22, null))
            {
                craftingHint = receiverValue22.GetCraftingHint(gi.item);
            }
            else
            {
                craftingHint = default;
            }

            // 赋值前保存的目标物品说明面板中的详细描述文字。
global::UnityEngine.UI.Text assignmentReceiver24 = descDescription;
            if (string.IsNullOrEmpty(craftingHint))
            {
                assignmentReceiver24.text = gi.item.description;
            }
            else
            {
                assignmentReceiver24.text = gi.item.description + "\n\n" + craftingHint;
            }
        }

        if (descCount != null)
        {
            descCount.text = $"x{gi.count}";
        }

        if (useButton != null)
        {
            // 是否可以使用。
bool canUse = InventoryManager.Instance != null && InventoryManager.Instance.CanUseItem(gi.item) && !(gi.item.itemType == ItemType.Weapon && gi.item.weaponType == WeaponType.Knife);
            useButton.gameObject.SetActive(canUse);
            SetButtonText(useButton, ResolveUseText(gi.item));
        }
    }

    /// <summary>ShowEmptyDescription 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ClearDragState()、InventoryUI.HandleContextDiscardButtonClicked()、InventoryUI.HandleContextStoreButtonClicked()、InventoryUI.OnCellClicked(int, int)、InventoryUI.RefreshGridInternal()；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ShowEmptyDescription()
    {
        if (descriptionPanel != null && !descriptionPanel.gameObject.activeSelf)
        {
            return;
        }

        if (emptyHintText != null)
        {
            emptyHintText.gameObject.SetActive(true);
        }

        if (descIcon != null)
        {
            descIcon.color = new Color(1, 1, 1, 0);
        }

        if (descName != null)
        {
            descName.text = "";
        }

        if (descDescription != null)
        {
            descDescription.text = "";
        }

        if (descCount != null)
        {
            descCount.text = "";
        }

        if (useButton != null)
        {
            useButton.gameObject.SetActive(false);
        }
    }

    // ========================== 右键上下文菜单 ==========================
    /// <summary>ShowContextMenu 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InventoryManager.CanUseItem(ItemData)、InventoryUI.ResolveUseText(ItemData)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemClicked(GridItem, PointerEventData.InputButton, Vector2)；下游（含内部回调体） GameObject.SetActive(bool)、InventoryManager.CanUseItem(ItemData)、InventoryUI.ResolveUseText(ItemData)、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">背包网格物品。</param>
    /// <param name="screenPos">屏幕位置。</param>
    void ShowContextMenu(GridItem gi, Vector2 screenPos)
    {
        if (contextMenu == null)
        {
            return;
        }

        if (contextMenuItem != gi || !contextMenu.gameObject.activeSelf)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        }

        if (gi == null || gi.item == null)
        {
            return;
        }

        contextMenuItem = gi;
        contextMenu.gameObject.SetActive(true);
        PresentItemCommands(gi, screenPos);
    }

    /// <summary>HideContextMenu 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.HandleContextDiscardButtonClicked()、InventoryUI.HandleContextStoreButtonClicked()、InventoryUI.HandleContextUseButtonClicked()、InventoryUI.OnCellClicked(int, int)、InventoryUI.OnItemClicked(GridItem, PointerEventData.InputButton, Vector2) 等共 7 处；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    internal void SetCraftingBackdrop(bool visible)
    {
        if (visible)
        {
            HideContextMenu();
        }
        craftingBackdropVisible = visible;
        if (controlHints != null)
        {
            controlHints.gameObject.SetActive(!visible);
        }
        if (threeDimensionalPresentation != null)
        {
            threeDimensionalPresentation.SetCraftingBackdrop(visible);
        }
    }

    void HideContextMenu()
    {
        if (contextMenu != null)
        {
            contextMenu.gameObject.SetActive(false);
        }

        contextMenuItem = null;
    }

    /// <summary>ConfigureActionButtons 的业务处理入口；本方法直接依赖 UnityEvent.AddListener(UnityAction)、UnityEvent.RemoveListener(UnityAction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()、InventoryUI.Start()；下游（含内部回调体） UnityEvent.AddListener(UnityAction)、UnityEvent.RemoveListener(UnityAction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConfigureActionButtons()
    {
        if (useButton != null)
        {
            useButton.onClick.RemoveListener(HandleUseButtonClicked);
            useButton.onClick.AddListener(HandleUseButtonClicked);
        }

        if (ctxUseButton != null)
        {
            ctxUseButton.onClick.RemoveListener(HandleContextUseButtonClicked);
            ctxUseButton.onClick.AddListener(HandleContextUseButtonClicked);
        }

        if (ctxDiscardButton != null)
        {
            ctxDiscardButton.onClick.RemoveListener(HandleContextDiscardButtonClicked);
            ctxDiscardButton.onClick.AddListener(HandleContextDiscardButtonClicked);
        }

        if (ctxStoreButton != null)
        {
            ctxStoreButton.onClick.RemoveListener(HandleContextStoreButtonClicked);
            ctxStoreButton.onClick.AddListener(HandleContextStoreButtonClicked);
        }
    }

    /// <summary>HandleUseButtonClicked 的业务处理入口；本方法直接依赖 InventoryUI.UseItem(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.UseItem(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleUseButtonClicked()
    {
        UseItem(selectedGridItem);
    }

    /// <summary>HandleContextUseButtonClicked 的业务处理入口；本方法直接依赖 InventoryUI.HideContextMenu()、InventoryUI.UseItem(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryUI.HideContextMenu()、InventoryUI.UseItem(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleContextUseButtonClicked()
    {
        UseItem(contextMenuItem);
        HideContextMenu();
    }

    /// <summary>HandleContextDiscardButtonClicked 的业务处理入口；本方法直接依赖 InventoryGridView.HideSelected()、InventoryManager.DiscardItemAt(int, int)、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.HideSelected()、InventoryManager.DiscardItemAt(int, int)、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleContextDiscardButtonClicked()
    {
        if (contextMenuItem == null || !Interactive)
        {
            return;
        }

        // 判空前缓存的背包管理器的实例。
global::InventoryManager receiverValue26 = InventoryManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue26, null))
        {
            receiverValue26.DiscardItemAt(contextMenuItem.gridX, contextMenuItem.gridY);
            AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        }

        HideContextMenu();
        selectedGridItem = null;
        ShowEmptyDescription();
        gridView.HideSelected();
    }

    /// <summary>HandleContextStoreButtonClicked 的业务处理入口；本方法直接依赖 InventoryGridView.HideSelected()、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)、StorageBoxManager.TransferToStorage(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.HideSelected()、InventoryUI.HideContextMenu()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)、StorageBoxManager.TransferToStorage(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void HandleContextStoreButtonClicked()
    {
        if (contextMenuItem == null || !Interactive)
        {
            return;
        }

        // 判空前缓存的仓库箱体管理器的实例。
global::StorageBoxManager receiverValue27 = StorageBoxManager.Instance;
        // 条件访问得到的转移目标仓库的返回结果。
bool? optionalValue28;
        if (!global::System.Object.ReferenceEquals(receiverValue27, null))
        {
            optionalValue28 = receiverValue27.TransferToStorage(contextMenuItem);
            if (optionalValue28 == false)
            {
                AudioManager.PlayInterface(AudioEventNames.UICaseError);
            }
        }
        else
        {
            optionalValue28 = default;
        }

        HideContextMenu();
        selectedGridItem = null;
        ShowEmptyDescription();
        gridView.HideSelected();
    }

    /// <summary>UseItem 的业务处理入口；本方法直接依赖 InventoryManager.UseItemAt(int, int)、InventoryUI.RegisterWeaponToWheel(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.HandleContextUseButtonClicked()、InventoryUI.HandleUseButtonClicked()；下游（含内部回调体） InventoryManager.UseItemAt(int, int)、InventoryUI.RegisterWeaponToWheel(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    void UseItem(GridItem item)
    {
        if (!Interactive || item == null || InventoryManager.Instance == null)
        {
            return;
        }

        if (item.item != null && item.item.itemType == ItemType.Weapon)
        {
            RegisterWeaponToWheel(item);
            return;
        }

        InventoryManager.Instance.UseItemAt(item.gridX, item.gridY);
    }

    /// <summary>RegisterWeaponToWheel 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;WeaponWheelUI&gt;()、InventoryUI.ShowPickupToast(string)、Object.FindObjectOfType&lt;WeaponWheelUI&gt;(bool)、WeaponWheelUI.RegisterWeaponFromInventory(ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.UseItem(GridItem)；下游（含内部回调体） Component.GetComponent&lt;WeaponWheelUI&gt;()、InventoryUI.ShowPickupToast(string)、Object.FindObjectOfType&lt;WeaponWheelUI&gt;(bool)、WeaponWheelUI.RegisterWeaponFromInventory(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    void RegisterWeaponToWheel(GridItem item)
    {
        // 轮盘。
WeaponWheelUI wheel = null;
        if (LeonController.Instance != null)
        {
            wheel = LeonController.Instance.GetComponent<WeaponWheelUI>();
        }

        if (wheel == null)
        {
            wheel = Object.FindObjectOfType<WeaponWheelUI>(true);
        }

        // 是否已登记。
bool registered = wheel != null && wheel.RegisterWeaponFromInventory(item.item);
        if (registered)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
            ShowPickupToast(item.item.displayName + " 已登记到快捷栏");
        }
        else
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseError);
            ShowPickupToast("该武器暂不能登记到快捷栏");
        }
    }

    /// <summary>ResolveUseText 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ShowContextMenu(GridItem, Vector2)、InventoryUI.ShowDescription(GridItem)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前物品数据配置。</param>
    string ResolveUseText(ItemData item)
    {
        if (item == null)
        {
            return "使用";
        }

        if (item.itemType == ItemType.Ammo)
        {
            return "装填";
        }

        if (item.itemType == ItemType.Weapon)
        {
            return "登记快捷栏";
        }

        return "使用";
    }

    /// <summary>SetButtonText 的业务处理入口；本方法直接依赖 Component.GetComponentInChildren&lt;Text&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ShowDescription(GridItem)；下游（含内部回调体） Component.GetComponentInChildren&lt;Text&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="button">按钮。</param>
    /// <param name="text">文字。</param>
    void SetButtonText(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        // 标签。
Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }

    /// <summary>ConfigureCursorImage 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()、InventoryUI.Start()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ConfigureCursorImage()
    {
        if (cursorImage == null)
        {
            return;
        }

        cursorImage.raycastTarget = false;
    }

    /// <summary>ApplyActionButtonStyle 的业务处理入口；本方法直接依赖 InventoryUI.ApplyActionButtonStyle(Button)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnSceneLoaded()、InventoryUI.Start()；下游（含内部回调体） InventoryUI.ApplyActionButtonStyle(Button)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyActionButtonStyle()
    {
        ApplyActionButtonStyle(useButton);
        ApplyActionButtonStyle(ctxUseButton);
    }

    /// <summary>ApplyActionButtonStyle 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()、Component.GetComponentInChildren&lt;Text&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ApplyActionButtonStyle()；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、Component.GetComponentInChildren&lt;Text&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="button">按钮。</param>
    void ApplyActionButtonStyle(Button button)
    {
        if (button == null)
        {
            return;
        }

        // 图像。
Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        if (image != null)
        {
            image.raycastTarget = true;
            if (visualTheme != null && visualTheme.buttonNormal != null)
            {
                image.sprite = visualTheme.buttonNormal;
                image.color = Color.white;
                // 精灵图状态。
SpriteState spriteState = button.spriteState;
                if (actionButtonHoverSprite != null)
                {
                    spriteState.highlightedSprite = actionButtonHoverSprite;
                }
                else
                {
                    spriteState.highlightedSprite = visualTheme.buttonHighlighted;
                }

                if (actionButtonHoverSprite != null)
                {
                    spriteState.pressedSprite = actionButtonHoverSprite;
                }
                else
                {
                    spriteState.pressedSprite = visualTheme.buttonPressed;
                }

                if (actionButtonHoverSprite != null)
                {
                    spriteState.selectedSprite = actionButtonHoverSprite;
                }
                else
                {
                    spriteState.selectedSprite = visualTheme.buttonHighlighted;
                }

                spriteState.disabledSprite = visualTheme.buttonDisabled;
                button.spriteState = spriteState;
                button.transition = Selectable.Transition.SpriteSwap;
            }
            else if (actionButtonNormalSprite != null || actionButtonHoverSprite != null)
            {
                image.sprite = actionButtonNormalSprite;
                image.color = Color.white;
                // 精灵图状态。
SpriteState spriteState = button.spriteState;
                spriteState.highlightedSprite = actionButtonHoverSprite;
                spriteState.pressedSprite = actionButtonHoverSprite;
                spriteState.selectedSprite = actionButtonHoverSprite;
                button.spriteState = spriteState;
                button.transition = Selectable.Transition.SpriteSwap;
            }
            else
            {
                image.sprite = null;
                // 颜色集合。
ColorBlock colors = button.colors;
                colors.normalColor = actionNormalColor;
                colors.highlightedColor = actionHighlightedColor;
                colors.pressedColor = actionPressedColor;
                colors.selectedColor = actionHighlightedColor;
                colors.disabledColor = actionDisabledColor;
                colors.colorMultiplier = 1f;
                button.colors = colors;
                button.transition = Selectable.Transition.ColorTint;
                image.color = actionNormalColor;
            }
        }

        // 标签。
Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.color = Color.white;
        }
    }

    // ========================== 拾取通知 ==========================
    /// <summary>
    /// 统一清理临时提示：停止协程、清空文本、重置透明度、隐藏对象、清空引用。
    /// 可重复调用，不会报错。
    /// </summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnDestroy()、InventoryUI.OnDisable()、InventoryUI.OnSceneLoaded()、InventoryUI.PlayExitAnimation(Action)、InventoryUI.ShowPickupToast(string) 等共 6 处；下游（含内部回调体） GameObject.SetActive(bool)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void ClearToast()
    {
        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }

        if (toastText != null)
        {
            toastText.text = string.Empty;
            toastText.color = Color.white;
            toastText.gameObject.SetActive(false);
        }
    }

    /// <summary>ShowPickupToast 的业务处理入口；本方法直接依赖 InventoryUI.ClearToast()、InventoryUI.ToastRoutine(string)、MonoBehaviour.StartCoroutine(IEnumerator)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CraftSelectedRecipe()、InventoryUI.OnItemPointerUp(GridItem, Vector2)、InventoryUI.RegisterWeaponToWheel(GridItem)；下游（含内部回调体） InventoryUI.ClearToast()、InventoryUI.ToastRoutine(string)、MonoBehaviour.StartCoroutine(IEnumerator)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">文字。</param>
    public void ShowPickupToast(string text)
    {
        if (toastText == null)
        {
            return;
        }

        ClearToast();
        toastRoutine = StartCoroutine(ToastRoutine(text));
    }

    /// <summary>ToastRoutine 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryUI.ClearToast()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ShowPickupToast(string)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.SetActive(bool)、InventoryUI.ClearToast()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">文字。</param>
    IEnumerator ToastRoutine(string text)
    {
        toastText.gameObject.SetActive(true);
        toastText.text = text;
        // 已用。
float elapsed = 0f;
        while (elapsed < toastDuration)
        {
            // 当前界面动画的归一化进度。
float t = elapsed / toastDuration;
            // 透明度。
float alpha;
            if (t < 0.1f)
            {
                alpha = t / 0.1f;
            }
            else
            {
                // 分支选出的1f。
float selectedValue32;
                if (t > 0.7f)
                {
                    selectedValue32 = (1f - t) / 0.3f;
                }
                else
                {
                    selectedValue32 = 1f;
                }

                alpha = (selectedValue32);
            }

            toastText.color = new Color(1, 1, 1, alpha);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        toastRoutine = null;
        ClearToast();
    }

    /// <summary>RequestRefreshGrid 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void RequestRefreshGrid()
    {
        refreshPending = true;
    }

    /// <summary>ClearDragState 的业务处理入口；本方法直接依赖 InventoryGridView.HideSelected()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.PlayExitAnimation(Action)、InventoryUI.RefreshGridInternal()；下游（含内部回调体） InventoryGridView.HideSelected()、InventoryUI.ShowEmptyDescription()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ClearDragState()
    {
        keyboardItemMove = false;
        draggingItem = null;
        dragOffset = Vector2.zero;
        dragCompleted = false;
        dragMoved = false;
        selectedGridItem = null;
        ShowEmptyDescription();
        // 判空前缓存的背包网格视图组件。
global::InventoryGridView receiverValue34 = gridView;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.HideSelected();
        }
    }
}
