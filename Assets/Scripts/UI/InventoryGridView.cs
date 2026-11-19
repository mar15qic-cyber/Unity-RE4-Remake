using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

/// <summary>
/// 可复用的背包网格视图组件。
/// InventoryUI 和 StorageBoxUI 共同使用，统一格子创建、物品控件创建、坐标换算、高亮管理。
/// </summary>
public class InventoryGridView : MonoBehaviour
{
    /// <summary>中文含义：公文包与物品的三维展示组件。使用位置：InterfaceReworkBuilder.ConfigurationInventory()、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;) 等共 4 处。</summary>
    [Header("可选三维表现")]
    [UnityEngine.Serialization.FormerlySerializedAs("三维展示")]
    [UnityEngine.InspectorName("三维展示")]
    public AttacheCasePresentationController threeDimensionalPresentation;
    /// <summary>中文含义：界面视觉主题配置。使用位置：AttacheCasePresentationController.CreateCountText(GridItem)、InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface() 等共 5 处。</summary>
    public UIVisualTheme visualTheme;
    /// <summary>中文含义：网格内容容器（格子+物品的父节点）。使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、AttacheCasePresentationController.CreateStage() 等共 8 处。</summary>
    [Header("网格区域")]
    [Tooltip("网格内容容器（格子+物品的父节点）")]
    public RectTransform gridContainer;
    /// <summary>中文含义：网格区域。网格可用区域的 RectTransform，格子尺寸根据此区域计算。使用位置：作为 RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数 等共 8 处。</summary>
    [Tooltip("网格可用区域的 RectTransform，格子尺寸根据此区域计算")]
    public RectTransform gridArea;
    /// <summary>中文含义：格子底图 Sprite（留空则使用纯色方块）。使用位置：InterfaceReworkBuilder.ConfigurationInventory()、InventoryGridView.ConfigureCell(RectTransform, int, int, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;) 等共 4 处。</summary>
    [Header("格子外观")]
    [Tooltip("格子底图 Sprite（留空则使用纯色方块）")]
    public Sprite cellBackgroundSprite;
    /// <summary>中文含义：格子间距（像素）。使用位置：作为 Mathf.RoundToInt(float) 的参数、作为 Vector2.Vector2(float, float) 的参数、AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2) 等共 9 处。</summary>
    [Tooltip("格子间距（像素）")]
    public float cellSpacing = 1f;
    /// <summary>中文含义：格子底色（无 Sprite 时使用）。使用位置：InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface()、InventoryGridView.ConfigureCell(RectTransform, int, int, float) 等共 5 处。</summary>
    [Tooltip("格子底色（无 Sprite 时使用）")]
    public Color cellFallbackColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
    /// <summary>中文含义：选中高亮颜色。使用位置：作为 InventoryGridView.EnsureOverlay(string, Color, float) 的参数、InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface() 等共 4 处。</summary>
    [Header("选中高亮")]
    [Tooltip("选中高亮颜色")]
    public Color selectedColor = new Color(0.15f, 1f, 0.35f, 0.85f);
    /// <summary>中文含义：选中高亮边框外扩像素。使用位置：作为 InventoryGridView.EnsureOverlay(string, Color, float) 的参数、作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数。</summary>
    [Tooltip("选中高亮边框外扩像素")]
    public float selectedPadding = 2f;
    /// <summary>中文含义：悬停高亮颜色。使用位置：作为 InventoryGridView.EnsureOverlay(string, Color, float) 的参数、InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface() 等共 4 处。</summary>
    [Header("悬停高亮")]
    [Tooltip("悬停高亮颜色")]
    public Color hoverColor = new Color(1f, 0.85f, 0.3f, 0.6f);
    /// <summary>中文含义：悬停高亮边框外扩像素。使用位置：作为 InventoryGridView.EnsureOverlay(string, Color, float) 的参数、作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数。</summary>
    [Tooltip("悬停高亮边框外扩像素")]
    public float hoverPadding = 1f;
    /// <summary>中文含义：拖拽中物品的透明度。使用位置：InventoryGridView.SetItemDragAlpha(GridItem, bool)。</summary>
    [Header("拖拽高亮")]
    [Tooltip("拖拽中物品的透明度")]
    public float dragAlpha = 0.5f;
    /// <summary>拖拽来源标识</summary>
    public enum GridSource
    {
        /// <summary>中文含义：背包。</summary>
        Inventory,
        /// <summary>中文含义：仓库。</summary>
        Storage
    }

    /// <summary>中文含义：来源类型。当前网格代表的数据源类型</summary>
    public GridSource sourceType = GridSource.Inventory;
    /// <summary>中文含义：当前单元格。当前单元格像素尺寸</summary>
    public float CurrentCell { get; private set; }
    /// <summary>中文含义：总计宽度。网格总宽（像素）</summary>
    public float TotalWidth { get; private set; }
    /// <summary>中文含义：总计高度。网格总高（像素）</summary>
    public float TotalHeight { get; private set; }

    // 高亮覆盖层
    /// <summary>中文含义：选定叠加层。使用位置：作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数、作为 Transform.SetSiblingIndex(int) 的参数、InventoryGridView.CacheReusableChildren() 等共 8 处。</summary>
    private Image selectedOverlay;
    /// <summary>中文含义：悬停叠加层。使用位置：作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数、InventoryGridView.CacheReusableChildren()、InventoryGridView.HideHover() 等共 6 处。</summary>
    private Image hoverOverlay;
    /// <summary>中文含义：选定覆盖。使用位置：作为 AttacheCasePresentationController.UpdateSpaceOverride(Image) 的参数。</summary>
    public Image SelectedOverride => selectedOverlay;
    /// <summary>中文含义：悬停覆盖。使用位置：作为 AttacheCasePresentationController.UpdateSpaceOverride(Image) 的参数。</summary>
    public Image HoverOverride => hoverOverlay;

    /// <summary>中文含义：发光精灵图。使用位置：InventoryGridView.EnsureGlowSprite()。</summary>
    private Sprite glowSprite;
    /// <summary>中文含义：物品控件集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.CacheReusableChildren()、InventoryGridView.CreateItemWidget(GridItem, float)、InventoryGridView.GetItemWidget(GridItem) 等共 4 处。</summary>
    private readonly Dictionary<GridItem, RectTransform> itemWidgets = new Dictionary<GridItem, RectTransform>();
    /// <summary>中文含义：可复用的单元格集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.CacheReusableChildren()、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private readonly List<RectTransform> reusableCells = new List<RectTransform>();
    /// <summary>中文含义：池化的物品控件集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.CacheReusableChildren()、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.ReleaseItemWidget(RectTransform)。</summary>
    private readonly List<RectTransform> pooledItemWidgets = new List<RectTransform>();
    /// <summary>中文含义：界面控件绑定集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.CreateItemWidget(GridItem, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.ReleaseItemWidget(RectTransform)。</summary>
    private readonly Dictionary<RectTransform, GridItem> widgetBindings = new Dictionary<RectTransform, GridItem>();
    /// <summary>中文含义：前一次控件集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private readonly Dictionary<GridItem, RectTransform> previousWidgets = new Dictionary<GridItem, RectTransform>();
    /// <summary>中文含义：期望物品列表。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private readonly List<GridItem> desiredItems = new List<GridItem>();
    /// <summary>中文含义：期望集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private readonly HashSet<GridItem> desiredSet = new HashSet<GridItem>();
    /// <summary>中文含义：前一次条目集合。只读字段，初始化后不由普通写入口替换。使用位置：InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private readonly List<KeyValuePair<GridItem, RectTransform>> previousEntries = new List<KeyValuePair<GridItem, RectTransform>>();
    /// <summary>中文含义：rebuilding 表示“是否重建中”；rebuildPending 表示“重建是否仍待处理”。使用位置：InventoryGridView.LateUpdate()、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。；使用位置：InventoryGridView.LateUpdate()、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private bool rebuilding, rebuildPending;
    /// <summary>中文含义：pendingWidth 表示“待处理宽度”；pendingHeight 表示“待处理高度”。使用位置：作为 InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。；使用位置：作为 InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private int pendingWidth, pendingHeight;
    /// <summary>中文含义：待处理物品列表。使用位置：InventoryGridView.LateUpdate()、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</summary>
    private IReadOnlyList<GridItem> pendingItems;
    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LateUpdate()
    {
        if (!rebuildPending || rebuilding)
        {
            return;
        }

        rebuildPending = false;
        // nextItems（下一次物品列表）：使用位置：作为 InventoryGridView.Rebuild(int, int, IReadOnlyList<GridItem>) 的参数。
IReadOnlyList<GridItem> nextItems = pendingItems;
        pendingItems = null;
        Rebuild(pendingWidth, pendingHeight, nextItems);
    }

    /// <summary>Rebuild 的业务处理入口；本方法直接依赖 Dictionary&lt;GridItem, RectTransform&gt;.Clear()、HashSet&lt;GridItem&gt;.Clear()、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、List&lt;GridItem&gt;.Clear()、List&lt;KeyValuePair&lt;GridItem, RectTransform&gt; &gt;.Clear()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.LateUpdate()、InventoryUI.RefreshGridInternal()、StorageBoxUI.RefreshInventoryGridInternal()；下游（含内部回调体） Dictionary&lt;GridItem, RectTransform&gt;.Clear()、HashSet&lt;GridItem&gt;.Clear()、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、List&lt;GridItem&gt;.Clear()、List&lt;KeyValuePair&lt;GridItem, RectTransform&gt; &gt;.Clear()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="caseWidth">中文含义：公文包宽度。使用位置：作为 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    /// <param name="caseHeight">中文含义：公文包高度。使用位置：作为 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    /// <param name="items">中文含义：物品列表。使用位置：作为 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    public void Rebuild(int caseWidth, int caseHeight, IReadOnlyList<GridItem> items)
    {
        if (rebuilding)
        {
            pendingWidth = caseWidth;
            pendingHeight = caseHeight;
            pendingItems = items;
            rebuildPending = true;
            return;
        }

        rebuildPending = false;
        pendingItems = null;
        rebuilding = true;
        try
        {
            RebuildInternal(caseWidth, caseHeight, items);
        }
        finally
        {
            previousWidgets.Clear();
            desiredItems.Clear();
            desiredSet.Clear();
            previousEntries.Clear();
            rebuilding = false;
        }
    }

    // 回调
    /// <summary>中文含义：物品指针向下时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.OnPointerDown(PointerEventData)、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<GridItem, Vector2> onItemPointerDown;
    /// <summary>中文含义：物品拖拽时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.OnDrag(PointerEventData)、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<GridItem, Vector2> onItemDrag;
    /// <summary>中文含义：物品指针向上时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.OnPointerUp(PointerEventData)、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<GridItem, Vector2> onItemPointerUp;
    /// <summary>中文含义：物品已点击时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.OnPointerClick(PointerEventData)、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<GridItem, PointerEventData.InputButton, Vector2> onItemClicked;
    /// <summary>中文含义：单元格已点击时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.OnPointerClick(PointerEventData)、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<int, int> onCellClicked;
    /// <summary>中文含义：物品悬停进入时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.Update()、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action<GridItem> onItemHoverEnter;
    /// <summary>中文含义：物品悬停退出时的通知回调。使用位置：作为 UnityEvent&lt;BaseEventData&gt;.AddListener(UnityAction&lt;BaseEventData&gt;) 的参数、AttacheCaseProjectionInput.Update()、InventoryUI.ConfigureGridCallbacks() 等共 4 处。</summary>
    public Action onItemHoverExit;
    /// <summary>
    /// 重建整个网格（格子+物品），尺寸根据 gridArea 计算。
    /// </summary>
    /// <remarks>静态调用链：上游 InventoryGridView.Rebuild(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;)、Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;Image&gt;()、Dictionary&lt;GridItem, RectTransform&gt;.Add(GridItem, RectTransform)、Dictionary&lt;GridItem, RectTransform&gt;.Clear()、Dictionary&lt;GridItem, RectTransform&gt;.Remove(GridItem)、Dictionary&lt;GridItem, RectTransform&gt;.TryGetValue(GridItem, out RectTransform) 等共 35 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="caseWidth">中文含义：公文包宽度。使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    /// <param name="caseHeight">中文含义：公文包高度。使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    /// <param name="items">中文含义：物品列表。使用位置：作为 AttacheCasePresentationController.Synchronize(IReadOnlyList&lt;GridItem&gt;) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)。</param>
    private void RebuildInternal(int caseWidth, int caseHeight, IReadOnlyList<GridItem> items)
    {
        if (gridContainer == null)
        {
            return;
        }

        CacheReusableChildren();
        HideSelected();
        HideHover();
        previousWidgets.Clear();
        // entry（当前键值配对）：每轮从物品控件集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<GridItem, RectTransform>.Add(GridItem, RectTransform) 的参数。
foreach (var entry in itemWidgets)
        {
            previousWidgets.Add(entry.Key, entry.Value);
        }

        itemWidgets.Clear();
        desiredItems.Clear();
        desiredSet.Clear();
        if (items != null)
        {
            // gi（背包网格物品）：每轮从物品列表取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<GridItem>.Add(GridItem) 的参数、作为 List<GridItem>.Add(GridItem) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
foreach (var gi in items)
            {
                if (gi == null || gi.item == null || !desiredSet.Add(gi))
                {
                    continue;
                }

                desiredItems.Add(gi);
            }
        }

        // 先回收本轮不再需要的物品，确保同容量替换优先使用已有池对象。
        previousEntries.Clear();
        // entry（当前键值配对）：每轮从前一次控件集合取得一个元素，仅代表当前这一项。使用位置：作为 List<KeyValuePair<GridItem, RectTransform>>.Add(KeyValuePair<GridItem, RectTransform>) 的参数。
foreach (var entry in previousWidgets)
        {
            previousEntries.Add(entry);
        }

        // previous（前一次）：每轮从前一次条目集合取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<GridItem, RectTransform>.Remove(GridItem) 的参数、作为 HashSet<GridItem>.Contains(GridItem) 的参数、作为 InventoryGridView.ReleaseItemWidget(RectTransform) 的参数。
foreach (var previous in previousEntries)
        {
            if (desiredSet.Contains(previous.Key))
            {
                continue;
            }

            ReleaseItemWidget(previous.Value);
            previousWidgets.Remove(previous.Key);
        }

        if (caseWidth <= 0 || caseHeight <= 0)
        {
            // cellRt（单元格矩形变换）：每轮从可复用的单元格集合取得一个元素，仅代表当前这一项。使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
foreach (var cellRt in reusableCells)
            {
                if (cellRt != null)
                {
                    cellRt.gameObject.SetActive(false);
                }
            }

            // oldWidget（旧界面控件）：每轮从前一次控件集合的数值集合取得一个元素，仅代表当前这一项。使用位置：作为 InventoryGridView.ReleaseItemWidget(RectTransform) 的参数。
foreach (var oldWidget in previousWidgets.Values)
            {
                ReleaseItemWidget(oldWidget);
            }

            gridContainer.sizeDelta = Vector2.zero;
            CurrentCell = 0f;
            TotalWidth = 0f;
            TotalHeight = 0f;
            return;
        }

        // 根据网格区域实际尺寸计算单元格大小
        // areaRect（区域矩形）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
Rect areaRect;
        if (gridArea != null)
        {
            areaRect = gridArea.rect;
        }
        else
        {
            areaRect = new Rect(0, 0, Screen.width, Screen.height);
        }

        // availW（可用宽度）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
float availW = areaRect.width;
        // availH（可用高度）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
float availH = areaRect.height;
        // fitW（适配宽度）：使用位置：作为 Mathf.Min(float, float) 的参数。
float fitW = (availW - (caseWidth - 1) * cellSpacing) / caseWidth;
        // fitH（适配高度）：使用位置：作为 Mathf.Min(float, float) 的参数。
float fitH = (availH - (caseHeight - 1) * cellSpacing) / caseHeight;
        // cell（单元格）：保存 Mathf.Min(float, float) 的返回结果，供当前步骤使用。使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数、作为 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool) 的参数、作为 InventoryGridView.GridToLocal(int, int, float) 的参数 等共 5 处。
float cell = Mathf.Min(fitW, fitH);
        CurrentCell = cell;
        // totalW（总计宽度）：使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
float totalW = caseWidth * (cell + cellSpacing) - cellSpacing;
        // totalH（总计高度）：使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
float totalH = caseHeight * (cell + cellSpacing) - cellSpacing;
        TotalWidth = totalW;
        TotalHeight = totalH;
        // 居中放置网格
        gridContainer.anchorMin = new Vector2(0.5f, 0.5f);
        gridContainer.anchorMax = new Vector2(0.5f, 0.5f);
        gridContainer.pivot = new Vector2(0.5f, 0.5f);
        gridContainer.anchoredPosition = Vector2.zero;
        gridContainer.sizeDelta = new Vector2(totalW, totalH);
        // requiredCellCount（所需单元格数量）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
int requiredCellCount = caseWidth * caseHeight;
        while (reusableCells.Count < requiredCellCount)
        {
            reusableCells.Add(CreateEmptyCell());
        }

        // i（当前遍历索引）：使用位置：作为 List<RectTransform>.this[int] 的参数、作为 Transform.SetSiblingIndex(int) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
for (int i = 0; i < reusableCells.Count; i++)
        {
            // cellRt（单元格矩形变换）：使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数、作为 Mathf.Approximately(float, float) 的参数、作为 Vector2.Distance(Vector2, Vector2) 的参数 等共 4 处。
RectTransform cellRt = reusableCells[i];
            // active（是否生效）：使用位置：作为 GameObject.SetActive(bool) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
bool active = i < requiredCellCount;
            if (cellRt == null)
            {
                continue;
            }

            cellRt.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            // x（横向）：使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数、作为 InventoryGridView.GridToLocal(int, int, float) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
int x = i % caseWidth;
            // y（纵向）：使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数、作为 InventoryGridView.GridToLocal(int, int, float) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
int y = i / caseWidth;
            // expectedName（预期名称）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
string expectedName = $"Cell_{x}_{y}";
            // cellImage（单元格图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
Image cellImage = cellRt.GetComponent<Image>();
            // expectedColor（是否预期颜色）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
bool expectedColor;
            if (cellBackgroundSprite != null)
            {
                expectedColor = cellImage != null && cellImage.sprite == cellBackgroundSprite && cellImage.color == Color.white;
            }
            else
            {
                expectedColor = cellImage != null && cellImage.sprite == null && cellImage.color == cellFallbackColor;
            }

            // configured（是否已配置）：使用位置：InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
bool configured = cellRt.name == expectedName && Mathf.Approximately(cellRt.sizeDelta.x, cell) && Mathf.Approximately(cellRt.sizeDelta.y, cell) && Vector2.Distance(cellRt.anchoredPosition, GridToLocal(x, y, cell)) < 0.001f && expectedColor && cellImage != null && cellImage.raycastTarget && cellRt.GetComponent<EventTrigger>() != null && cellRt.GetComponent<EventTrigger>().triggers.Count == 1;
            if (!configured)
            {
                ConfigureCell(cellRt, x, y, cell);
            }

            // 底格固定在前面，避免扩容时覆盖物品控件。
            cellRt.SetSiblingIndex(i);
        }

        // gi（背包网格物品）：每轮从期望物品列表取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<GridItem, RectTransform>.Add(GridItem, RectTransform) 的参数、作为 Dictionary<GridItem, RectTransform>.Remove(GridItem) 的参数、作为 Dictionary<GridItem, RectTransform>.TryGetValue(GridItem, out RectTransform) 的参数 等共 5 处。
foreach (var gi in desiredItems)
        {
            // widget（界面控件）：使用位置：作为 Dictionary<GridItem, RectTransform>.Add(GridItem, RectTransform) 的参数、作为 Dictionary<GridItem, RectTransform>.TryGetValue(GridItem, out RectTransform) 的参数、作为 Dictionary<RectTransform, GridItem>.this[RectTransform] 的参数 等共 6 处。
RectTransform widget;
            // sameBinding（是否相同绑定）：使用位置：作为 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
bool sameBinding = false;
            if (previousWidgets.TryGetValue(gi, out widget))
            {
                previousWidgets.Remove(gi);
                sameBinding = widgetBindings.TryGetValue(widget, out GridItem /* 中文含义：已绑定物品。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ boundItem) && boundItem == gi;
            }
            else if (pooledItemWidgets.Count > 0)
            {
                // lastIndex（上一次索引）：使用位置：作为 List<RectTransform>.RemoveAt(int) 的参数、作为 List<RectTransform>.this[int] 的参数。
int lastIndex = pooledItemWidgets.Count - 1;
                widget = pooledItemWidgets[lastIndex];
                pooledItemWidgets.RemoveAt(lastIndex);
            }
            else
            {
                widget = CreateEmptyItemWidget();
            }

            ConfigureItemWidget(widget, gi, cell, !sameBinding);
            itemWidgets.Add(gi, widget);
            widgetBindings[widget] = gi;
            widget.SetAsLastSibling();
        }

        // oldWidget（旧界面控件）：每轮从前一次控件集合的数值集合取得一个元素，仅代表当前这一项。使用位置：作为 InventoryGridView.ReleaseItemWidget(RectTransform) 的参数。
foreach (var oldWidget in previousWidgets.Values)
        {
            ReleaseItemWidget(oldWidget);
        }

        // receiverValue3（判空前缓存的公文包与物品的三维展示组件）：从公文包与物品的三维展示组件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventoryGridView.RebuildInternal(int, int, IReadOnlyList<GridItem>)。
global::AttacheCasePresentationController receiverValue3 = threeDimensionalPresentation;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Synchronize(items);
        }

        // 高亮层置于全部物品之上。
        if (hoverOverlay != null)
        {
            hoverOverlay.transform.SetAsLastSibling();
        }

        if (selectedOverlay != null)
        {
            selectedOverlay.transform.SetAsLastSibling();
        }
    }

    /// <summary>网格坐标转局部坐标（中心对齐）</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.ConfigureCell(RectTransform, int, int, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gridX">中文含义：网格横向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="gridY">中文含义：网格纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="cell">中文含义：单元格。使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.GridToLocal(int, int, float)。</param>
    public Vector2 GridToLocal(int gridX, int gridY, float cell)
    {
        // step（步进）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float step = cell + cellSpacing;
        return new Vector2(gridX * step - TotalWidth / 2f + cell / 2f, -(gridY * step) + TotalHeight / 2f - cell / 2f);
    }

    /// <summary>局部坐标转网格坐标</summary>
    /// <remarks>静态调用链：上游 AttacheCaseProjectionInput.Find(Vector2)、AttacheCaseProjectionInput.OnPointerClick(PointerEventData)；下游（含内部回调体） Mathf.FloorToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="localPoint">中文含义：局部坐标点。使用位置：作为 Mathf.FloorToInt(float) 的参数。</param>
    /// <param name="cell">中文含义：单元格。使用位置：InventoryGridView.LocalToGrid(Vector2, float, out int, out int)。</param>
    /// <param name="gridX">中文含义：网格横向。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.LocalToGrid(Vector2, float, out int, out int)。</param>
    /// <param name="gridY">中文含义：网格纵向。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.LocalToGrid(Vector2, float, out int, out int)。</param>
    public bool LocalToGrid(Vector2 localPoint, float cell, out int gridX, out int gridY)
    {
        // step（步进）：使用位置：作为 Mathf.FloorToInt(float) 的参数。
float step = cell + cellSpacing;
        gridX = Mathf.FloorToInt((localPoint.x + TotalWidth / 2f) / step);
        gridY = Mathf.FloorToInt((TotalHeight / 2f - localPoint.y) / step);
        return gridX >= 0 && gridY >= 0;
    }

    /// <summary>屏幕坐标转换为网格容器内的局部坐标。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)、InventoryUI.OnItemDrag(GridItem, Vector2)、InventoryUI.OnItemPointerDown(GridItem, Vector2)、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2)；下游（含内部回调体） AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2)、Component.GetComponentInParent&lt;Canvas&gt;()、RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="screenPoint">中文含义：屏幕坐标点。使用位置：作为 AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2) 的参数、作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数。</param>
    /// <param name="localPoint">中文含义：局部坐标点。输出参数，由本入口写入并交给调用方。使用位置：作为 AttacheCasePresentationController.ScreenRotateMesh(Vector2, out Vector2) 的参数、作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)。</param>
    public bool TryScreenPointToLocal(Vector2 screenPoint, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (threeDimensionalPresentation != null)
        {
            return threeDimensionalPresentation.ScreenRotateMesh(screenPoint, out localPoint);
        }

        if (gridContainer == null)
        {
            return false;
        }

        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)。
Canvas canvas = gridContainer.GetComponentInParent<Canvas>();
        // eventCamera（事件摄像机）：使用位置：作为 RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, Vector2, Camera, out Vector2) 的参数、InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)。
Camera eventCamera;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera;
        }
        else
        {
            eventCamera = null;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(gridContainer, screenPoint, eventCamera, out localPoint);
    }

    /// <summary>
    /// 根据物品视觉中心计算落格坐标，保留玩家按下时的抓取偏移。
    /// </summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)、InventoryUI.OnItemPointerUp(GridItem, Vector2)；下游（含内部回调体） InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2)、Mathf.RoundToInt(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)。</param>
    /// <param name="screenPoint">中文含义：屏幕坐标点。使用位置：作为 InventoryGridView.TryScreenPointToLocal(Vector2, out Vector2) 的参数。</param>
    /// <param name="pointerOffset">中文含义：指针偏移量。使用位置：InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)。</param>
    /// <param name="gridX">中文含义：网格横向。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)。</param>
    /// <param name="gridY">中文含义：网格纵向。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.TryGetDropGrid(GridItem, Vector2, Vector2, out int, out int)。</param>
    public bool TryGetDropGrid(GridItem item, Vector2 screenPoint, Vector2 pointerOffset, out int gridX, out int gridY)
    {
        gridX = 0;
        gridY = 0;
        if (item == null || CurrentCell <= 0f)
        {
            return false;
        }

        if (!TryScreenPointToLocal(screenPoint, out Vector2 /* 中文含义：指针局部。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pointerLocal))
        {
            return false;
        }

        // itemCenter（物品中心）：使用位置：作为 Mathf.RoundToInt(float) 的参数。
Vector2 itemCenter = pointerLocal - pointerOffset;
        // step（步进）：使用位置：作为 Mathf.RoundToInt(float) 的参数。
float step = CurrentCell + cellSpacing;
        // pixelWidth（像素宽度）：使用位置：作为 Mathf.RoundToInt(float) 的参数。
float pixelWidth = item.Width * CurrentCell + (item.Width - 1) * cellSpacing;
        // pixelHeight（像素高度）：使用位置：作为 Mathf.RoundToInt(float) 的参数。
float pixelHeight = item.Height * CurrentCell + (item.Height - 1) * cellSpacing;
        gridX = Mathf.RoundToInt((itemCenter.x + TotalWidth * 0.5f - pixelWidth * 0.5f) / step);
        gridY = Mathf.RoundToInt((TotalHeight * 0.5f - pixelHeight * 0.5f - itemCenter.y) / step);
        return true;
    }

    /// <summary>物品占据区域的像素位置和尺寸</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)、InventoryGridView.PositionOverlay(Image, GridItem, float)、InventoryUI.OnItemPointerDown(GridItem, Vector2)；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。</param>
    /// <param name="cell">中文含义：单元格。使用位置：InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。</param>
    /// <param name="pos">中文含义：位置。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。</param>
    /// <param name="size">中文含义：尺寸。输出参数，由本入口写入并交给调用方。使用位置：InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。</param>
    public void GetItemRect(GridItem gi, float cell, out Vector2 pos, out Vector2 size)
    {
        // step（步进）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float step = cell + cellSpacing;
        // w（背包网格物品的宽度）：使用位置：InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。
int w = gi.Width;
        // h（背包网格物品的高度）：使用位置：InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)。
int h = gi.Height;
        // pixelW（像素宽度）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float pixelW = w * cell + (w - 1) * cellSpacing;
        // pixelH（像素高度）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
float pixelH = h * cell + (h - 1) * cellSpacing;
        size = new Vector2(pixelW, pixelH);
        pos = new Vector2(gi.gridX * step + pixelW / 2f - TotalWidth / 2f, -(gi.gridY * step + pixelH / 2f) + TotalHeight / 2f);
    }

    /// <summary>CreateCell 的业务处理入口；本方法直接依赖 InventoryGridView.ConfigureCell(RectTransform, int, int, float)、InventoryGridView.CreateEmptyCell()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.ConfigureCell(RectTransform, int, int, float)、InventoryGridView.CreateEmptyCell()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">中文含义：横向。使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数。</param>
    /// <param name="cell">中文含义：单元格。使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数。</param>
    RectTransform CreateCell(int x, int y, float cell)
    {
        // rt（界面矩形变换）：保存 InventoryGridView.CreateEmptyCell() 的返回结果，供当前步骤使用。使用位置：作为 InventoryGridView.ConfigureCell(RectTransform, int, int, float) 的参数、InventoryGridView.CreateCell(int, int, float)。
RectTransform rt = CreateEmptyCell();
        ConfigureCell(rt, x, y, cell);
        return rt;
    }

    /// <summary>CreateEmptyCell 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.CreateCell(int, int, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;RectTransform&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    RectTransform CreateEmptyCell()
    {
        // go（当前游戏对象）：使用位置：InventoryGridView.CreateEmptyCell()。
var go = new GameObject("Cell_复用", typeof(Image), typeof(EventTrigger));
        go.transform.SetParent(gridContainer, false);
        return go.GetComponent<RectTransform>();
    }

    /// <summary>ConfigureCell 的业务处理入口；本方法直接依赖 Action&lt;int, int&gt;.Invoke(int, int)、Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;Image&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、InventoryGridView.GridToLocal(int, int, float)、List&lt;EventTrigger.Entry&gt;.Add(EventTrigger.Entry) 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.CreateCell(int, int, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） Action&lt;int, int&gt;.Invoke(int, int)、Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;Image&gt;()、EventTrigger.Entry.Entry()、GameObject.AddComponent&lt;EventTrigger&gt;()、InventoryGridView.GridToLocal(int, int, float)、List&lt;EventTrigger.Entry&gt;.Add(EventTrigger.Entry) 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rt">中文含义：界面矩形变换。使用位置：InventoryGridView.ConfigureCell(RectTransform, int, int, float)。</param>
    /// <param name="x">中文含义：横向。使用位置：作为 InventoryGridView.GridToLocal(int, int, float) 的参数、InventoryGridView.ConfigureCell(RectTransform, int, int, float)。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 InventoryGridView.GridToLocal(int, int, float) 的参数、InventoryGridView.ConfigureCell(RectTransform, int, int, float)。</param>
    /// <param name="cell">中文含义：单元格。使用位置：作为 InventoryGridView.GridToLocal(int, int, float) 的参数、作为 Vector2.Vector2(float, float) 的参数。</param>
    void ConfigureCell(RectTransform rt, int x, int y, float cell)
    {
        if (rt == null)
        {
            return;
        }

        rt.gameObject.name = $"Cell_{x}_{y}";
        rt.SetParent(gridContainer, false);
        rt.sizeDelta = new Vector2(cell, cell);
        rt.anchoredPosition = GridToLocal(x, y, cell);
        // img（界面图像组件）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureCell(RectTransform, int, int, float)。
var img = rt.GetComponent<Image>();
        if (img != null)
        {
            if (cellBackgroundSprite != null)
            {
                img.sprite = cellBackgroundSprite;
                img.type = Image.Type.Simple;
                img.color = Color.white;
                img.preserveAspect = false;
            }
            else
            {
                img.sprite = null;
                img.color = cellFallbackColor;
            }

            img.raycastTarget = true;
        }

        // trigger（触发器）：保存 Component.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureCell(RectTransform, int, int, float)。
var trigger = rt.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = rt.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();
        // entry（当前入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureCell(RectTransform, int, int, float)。
var entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        // cx（列坐标）：使用位置：作为 Action<int, int>.Invoke(int, int) 的参数。
int cx = x;
        // cy（行坐标）：使用位置：作为 Action<int, int>.Invoke(int, int) 的参数。
int cy = y;
        entry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
        {
            // receiverValue5（判空前缓存的单元格已点击时的通知回调）：从单元格已点击时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<int, int> receiverValue5 = onCellClicked;
            if (!global::System.Object.ReferenceEquals(receiverValue5, null))
            {
                receiverValue5.Invoke(cx, cy);
            }
        });
        trigger.triggers.Add(entry);
    }

    /// <summary>CreateItemWidget 的业务处理入口；本方法直接依赖 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)、InventoryGridView.CreateEmptyItemWidget()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)、InventoryGridView.CreateEmptyItemWidget()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 Dictionary&lt;GridItem, RectTransform&gt;.this[GridItem] 的参数、作为 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool) 的参数、InventoryGridView.CreateItemWidget(GridItem, float)。</param>
    /// <param name="cell">中文含义：单元格。使用位置：作为 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool) 的参数。</param>
    void CreateItemWidget(GridItem gi, float cell)
    {
        if (gi == null || gi.item == null)
        {
            return;
        }

        // widget（界面控件）：保存 InventoryGridView.CreateEmptyItemWidget() 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<RectTransform, GridItem>.this[RectTransform] 的参数、作为 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool) 的参数、InventoryGridView.CreateItemWidget(GridItem, float)。
RectTransform widget = CreateEmptyItemWidget();
        ConfigureItemWidget(widget, gi, cell, true);
        itemWidgets[gi] = widget;
        widgetBindings[widget] = gi;
    }

    /// <summary>CreateEmptyItemWidget 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;()、GameObject.SetActive(bool)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.CreateItemWidget(GridItem, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.GetComponent&lt;Text&gt;()、GameObject.SetActive(bool)、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    RectTransform CreateEmptyItemWidget()
    {
        // go（当前游戏对象）：使用位置：InventoryGridView.CreateEmptyItemWidget()。
var go = new GameObject("Item_复用", typeof(RectMask2D), typeof(EventTrigger));
        go.transform.SetParent(gridContainer, false);
        // rt（界面矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、InventoryGridView.CreateEmptyItemWidget()。
var rt = go.GetComponent<RectTransform>();
        // iconGo（图标游戏对象）：使用位置：InventoryGridView.CreateEmptyItemWidget()。
var iconGo = new GameObject("Icon", typeof(Image));
        iconGo.transform.SetParent(rt, false);
        // iconRt（图标矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.CreateEmptyItemWidget()。
var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconGo.GetComponent<Image>().raycastTarget = true;
        // countGo（数量游戏对象）：使用位置：InventoryGridView.CreateEmptyItemWidget()。
var countGo = new GameObject("Count", typeof(Text));
        countGo.transform.SetParent(rt, false);
        // countRt（数量矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.CreateEmptyItemWidget()。
var countRt = countGo.GetComponent<RectTransform>();
        countRt.anchorMin = Vector2.one;
        countRt.anchorMax = Vector2.one;
        countRt.pivot = Vector2.one;
        countRt.anchoredPosition = new Vector2(-4, -4);
        countRt.sizeDelta = new Vector2(60, 30);
        // count（数量）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.CreateEmptyItemWidget()。
var count = countGo.GetComponent<Text>();
        // assignmentReceiver6（赋值前保存的目标数量）：从数量读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryGridView.CreateEmptyItemWidget()。
global::UnityEngine.UI.Text assignmentReceiver6 = count;
        if (visualTheme != null && visualTheme.interfaceFont != null)
        {
            assignmentReceiver6.font = visualTheme.interfaceFont;
        }
        else
        {
            assignmentReceiver6.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        count.fontSize = 18;
        count.color = Color.white;
        count.raycastTarget = false;
        countGo.SetActive(false);
        return rt;
    }

    /// <summary>ConfigureItemWidget 的业务处理入口；本方法直接依赖 Action.Invoke()、Action&lt;GridItem, PointerEventData.InputButton, Vector2&gt;.Invoke(GridItem, PointerEventData.InputButton, Vector2)、Action&lt;GridItem, Vector2&gt;.Invoke(GridItem, Vector2)、Action&lt;GridItem&gt;.Invoke(GridItem)、Color.Color(float, float, float, float)、Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;Image&gt;() 等共 30 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.CreateItemWidget(GridItem, float)、InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） Action.Invoke()、Action&lt;GridItem, PointerEventData.InputButton, Vector2&gt;.Invoke(GridItem, PointerEventData.InputButton, Vector2)、Action&lt;GridItem, Vector2&gt;.Invoke(GridItem, Vector2)、Action&lt;GridItem&gt;.Invoke(GridItem)、Color.Color(float, float, float, float)、Component.GetComponent&lt;EventTrigger&gt;()、Component.GetComponent&lt;Image&gt;() 等共 30 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rt">中文含义：界面矩形变换。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。</param>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 Action&lt;GridItem, PointerEventData.InputButton, Vector2&gt;.Invoke(GridItem, PointerEventData.InputButton, Vector2) 的参数、作为 Action&lt;GridItem, Vector2&gt;.Invoke(GridItem, Vector2) 的参数、作为 Action&lt;GridItem&gt;.Invoke(GridItem) 的参数 等共 7 处。</param>
    /// <param name="cell">中文含义：单元格。使用位置：作为 InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2) 的参数。</param>
    /// <param name="rebindCallbacks">中文含义：是否重新绑定回调集合。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。</param>
    void ConfigureItemWidget(RectTransform rt, GridItem gi, float cell, bool rebindCallbacks)
    {
        if (rt == null || gi == null || gi.item == null)
        {
            return;
        }

        rt.gameObject.SetActive(true);
        rt.gameObject.name = $"Item_{gi.item.displayName}";
        GetItemRect(gi, cell, out var /* 中文含义：位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pos, out var /* 中文含义：尺寸。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ size);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        // mask（遮罩）：保存 Component.GetComponent<RectMask2D>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var mask = rt.GetComponent<RectMask2D>();
        if (mask == null)
        {
            mask = rt.gameObject.AddComponent<RectMask2D>();
        }

        // padding（留白）：使用位置：作为 Vector4.Vector4(float, float, float, float) 的参数。
float padding = gi.item.iconPadding;
        mask.padding = new Vector4(padding, padding, padding, padding);
        // iconRt（图标矩形变换）：使用位置：作为 InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var iconRt = rt.Find("Icon") as RectTransform;
        if (iconRt == null)
        {
            // iconGo（图标游戏对象）：使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(rt, false);
            iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
        }

        // img（界面图像组件）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var img = iconRt.GetComponent<Image>();
        if (img == null)
        {
            img = iconRt.gameObject.AddComponent<Image>();
        }

        // finalRotation（最终旋转）：使用位置：作为 InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float) 的参数、作为 Quaternion.Euler(float, float, float) 的参数。
float finalRotation = gi.item.iconRotation + gi.rotation * 90f;
        iconRt.localRotation = Quaternion.Euler(0, 0, finalRotation);
        iconRt.localScale = Vector3.one;
        iconRt.anchoredPosition = gi.item.iconOffset;
        if (gi.item.icon != null)
        {
            img.sprite = gi.item.icon;
            img.color = Color.white;
            img.preserveAspect = false;
            FitIconWithoutDistortion(iconRt, gi.item.icon, size, finalRotation, gi.item.iconPadding, gi.item.iconScale);
        }
        else
        {
            img.sprite = null;
            img.color = new Color(0.6f, 0.5f, 0.2f, 0.8f);
            iconRt.sizeDelta = size;
        }

        if (threeDimensionalPresentation != null)
        {
            img.sprite = null;
            img.color = Color.clear;
            iconRt.localRotation = Quaternion.identity;
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = size;
        }

        img.raycastTarget = true;
        // trigger（触发器）：保存 Component.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var trigger = rt.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = rt.gameObject.AddComponent<EventTrigger>();
        }

        // 数据实例未更换时保留原有回调，避免重复分配闭包。
        if (rebindCallbacks)
        {
            trigger.triggers.Clear();
            // downEntry（向下入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var downEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };
            downEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
            {
                // pe（指针事件）：使用位置：作为 Action<GridItem, Vector2>.Invoke(GridItem, Vector2) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
var pe = (PointerEventData)e;
                if (pe.button == PointerEventData.InputButton.Left)
                {
                    // receiverValue8（判空前缓存的物品指针向下时的通知回调）：从物品指针向下时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<global::GridItem, global::UnityEngine.Vector2> receiverValue8 = onItemPointerDown;
                    if (!global::System.Object.ReferenceEquals(receiverValue8, null))
                    {
                        receiverValue8.Invoke(gi, pe.position);
                    }
                }
            });
            trigger.triggers.Add(downEntry);
            // dragEntry（拖拽入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var dragEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Drag
            };
            dragEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
            {
                // pe（指针事件）：使用位置：作为 Action<GridItem, Vector2>.Invoke(GridItem, Vector2) 的参数。
var pe = (PointerEventData)e;
                // receiverValue9（判空前缓存的物品拖拽时的通知回调）：从物品拖拽时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<global::GridItem, global::UnityEngine.Vector2> receiverValue9 = onItemDrag;
                if (!global::System.Object.ReferenceEquals(receiverValue9, null))
                {
                    receiverValue9.Invoke(gi, pe.position);
                }
            });
            trigger.triggers.Add(dragEntry);
            // upEntry（向上入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var upEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerUp
            };
            upEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
            {
                // pe（指针事件）：使用位置：作为 Action<GridItem, Vector2>.Invoke(GridItem, Vector2) 的参数。
var pe = (PointerEventData)e;
                // receiverValue10（判空前缓存的物品指针向上时的通知回调）：从物品指针向上时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<global::GridItem, global::UnityEngine.Vector2> receiverValue10 = onItemPointerUp;
                if (!global::System.Object.ReferenceEquals(receiverValue10, null))
                {
                    receiverValue10.Invoke(gi, pe.position);
                }
            });
            trigger.triggers.Add(upEntry);
            // clickEntry（点击入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var clickEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerClick
            };
            clickEntry.callback.AddListener( /* 中文含义：当前基础事件配置。 */e =>
            {
                // pe（指针事件）：使用位置：作为 Action<GridItem, PointerEventData.InputButton, Vector2>.Invoke(GridItem, PointerEventData.InputButton, Vector2) 的参数。
var pe = (PointerEventData)e;
                // receiverValue11（判空前缓存的物品已点击时的通知回调）：从物品已点击时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<global::GridItem, global::UnityEngine.EventSystems.PointerEventData.InputButton, global::UnityEngine.Vector2> receiverValue11 = onItemClicked;
                if (!global::System.Object.ReferenceEquals(receiverValue11, null))
                {
                    receiverValue11.Invoke(gi, pe.button, pe.position);
                }
            });
            trigger.triggers.Add(clickEntry);
            // enterEntry（进入入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var enterEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
            {
                // receiverValue12（判空前缓存的物品悬停进入时的通知回调）：从物品悬停进入时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action<global::GridItem> receiverValue12 = onItemHoverEnter;
                if (!global::System.Object.ReferenceEquals(receiverValue12, null))
                {
                    receiverValue12.Invoke(gi);
                }
            });
            trigger.triggers.Add(enterEntry);
            // exitEntry（退出入口）：使用位置：作为 List<EventTrigger.Entry>.Add(EventTrigger.Entry) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var exitEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener( /* 中文含义：当前流程中不使用的传入结果。 */_ =>
            {
                // receiverValue13（判空前缓存的物品悬停退出时的通知回调）：从物品悬停退出时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、作为 UnityEvent<BaseEventData>.AddListener(UnityAction<BaseEventData>) 的参数。
global::System.Action receiverValue13 = onItemHoverExit;
                if (!global::System.Object.ReferenceEquals(receiverValue13, null))
                {
                    receiverValue13.Invoke();
                }
            });
            trigger.triggers.Add(exitEntry);
        }

        // receiverValue14（判空前缓存的查找的返回结果）：从查找的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
global::UnityEngine.RectTransform receiverValue14 = (rt.Find("Count") as RectTransform);
        // count（数量）：使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
global::UnityEngine.UI.Text count;
        if (!global::System.Object.ReferenceEquals(receiverValue14, null))
        {
            count = receiverValue14.GetComponent<Text>();
        }
        else
        {
            count = default;
        }

        if (count == null)
        {
            // countGo（数量游戏对象）：使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var countGo = new GameObject("Count", typeof(Text));
            countGo.transform.SetParent(rt, false);
            count = countGo.GetComponent<Text>();
            count.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // countRt（数量矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
var countRt = countGo.GetComponent<RectTransform>();
            countRt.anchorMin = Vector2.one;
            countRt.anchorMax = Vector2.one;
            countRt.pivot = Vector2.one;
            countRt.anchoredPosition = new Vector2(-4, -4);
            countRt.sizeDelta = new Vector2(60, 30);
            count.fontSize = 18;
            count.color = Color.white;
            count.raycastTarget = false;
        }

        // countText（数量文字）：保存 int.ToString() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)。
string countText = gi.count.ToString();
        if (count.text != countText)
        {
            count.text = countText;
        }

        count.gameObject.SetActive(gi.count > 1);
    }

    /// <summary>ReleaseItemWidget 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;EventTrigger&gt;()、Dictionary&lt;RectTransform, GridItem&gt;.Remove(RectTransform)、GameObject.SetActive(bool)、List&lt;EventTrigger.Entry&gt;.Clear()、List&lt;RectTransform&gt;.Add(RectTransform)、List&lt;RectTransform&gt;.Contains(RectTransform)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） Component.GetComponent&lt;CanvasGroup&gt;()、Component.GetComponent&lt;EventTrigger&gt;()、Dictionary&lt;RectTransform, GridItem&gt;.Remove(RectTransform)、GameObject.SetActive(bool)、List&lt;EventTrigger.Entry&gt;.Clear()、List&lt;RectTransform&gt;.Add(RectTransform)、List&lt;RectTransform&gt;.Contains(RectTransform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="widget">中文含义：界面控件。使用位置：作为 Dictionary&lt;RectTransform, GridItem&gt;.Remove(RectTransform) 的参数、作为 List&lt;RectTransform&gt;.Add(RectTransform) 的参数、作为 List&lt;RectTransform&gt;.Contains(RectTransform) 的参数 等共 4 处。</param>
    void ReleaseItemWidget(RectTransform widget)
    {
        if (widget == null)
        {
            return;
        }

        // trigger（触发器）：保存 Component.GetComponent<EventTrigger>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ReleaseItemWidget(RectTransform)。
var trigger = widget.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            trigger.triggers.Clear();
        }

        widgetBindings.Remove(widget);
        // canvasGroup（画布组）：保存 Component.GetComponent<CanvasGroup>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.ReleaseItemWidget(RectTransform)。
var canvasGroup = widget.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        widget.gameObject.SetActive(false);
        if (!pooledItemWidgets.Contains(widget))
        {
            pooledItemWidgets.Add(widget);
        }
    }

    /// <summary>CacheReusableChildren 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;Image&gt;()、Dictionary&lt;GridItem, RectTransform&gt;.ContainsValue(RectTransform)、List&lt;RectTransform&gt;.Add(RectTransform)、List&lt;RectTransform&gt;.Contains(RectTransform)、object.ReferenceEquals(object, object)、string.StartsWith(string, StringComparison)、Transform.GetChild(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)；下游（含内部回调体） Component.GetComponent&lt;Image&gt;()、Dictionary&lt;GridItem, RectTransform&gt;.ContainsValue(RectTransform)、List&lt;RectTransform&gt;.Add(RectTransform)、List&lt;RectTransform&gt;.Contains(RectTransform)、object.ReferenceEquals(object, object)、string.StartsWith(string, StringComparison)、Transform.GetChild(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void CacheReusableChildren()
    {
        if (gridContainer == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 Transform.GetChild(int) 的参数、InventoryGridView.CacheReusableChildren()。
for (int i = 0; i < gridContainer.childCount; i++)
        {
            // child（子级）：保存 Transform.GetChild(int) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.CacheReusableChildren()。
Transform child = gridContainer.GetChild(i);
            if (child == null)
            {
                continue;
            }

            // leftOperand18（计算前缓存的左侧子级）：从子级读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryGridView.CacheReusableChildren()。
global::UnityEngine.Object leftOperand18 = child;
            // receiverValue16（判空前缓存的选定叠加层）：从选定叠加层读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventoryGridView.CacheReusableChildren()。
global::UnityEngine.UI.Image receiverValue16 = selectedOverlay;
            // optionalValue17（条件访问得到的接收者值16的变换）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：InventoryGridView.CacheReusableChildren()。
global::UnityEngine.Transform optionalValue17;
            if (!global::System.Object.ReferenceEquals(receiverValue16, null))
            {
                optionalValue17 = receiverValue16.transform;
            }
            else
            {
                optionalValue17 = default;
            }

            if (leftOperand18 == optionalValue17)
            {
                continue;
            }

            // leftOperand21（计算前缓存的左侧子级）：从子级读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryGridView.CacheReusableChildren()。
global::UnityEngine.Object leftOperand21 = child;
            // receiverValue19（判空前缓存的悬停叠加层）：从悬停叠加层读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、InventoryGridView.CacheReusableChildren()。
global::UnityEngine.UI.Image receiverValue19 = hoverOverlay;
            // optionalValue20（条件访问得到的接收者值19的变换）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：InventoryGridView.CacheReusableChildren()。
global::UnityEngine.Transform optionalValue20;
            if (!global::System.Object.ReferenceEquals(receiverValue19, null))
            {
                optionalValue20 = receiverValue19.transform;
            }
            else
            {
                optionalValue20 = default;
            }

            if (leftOperand21 == optionalValue20)
            {
                continue;
            }

            if (child.name.StartsWith("Cell_", StringComparison.Ordinal))
            {
                // rt（界面矩形变换）：使用位置：作为 List<RectTransform>.Add(RectTransform) 的参数、作为 List<RectTransform>.Contains(RectTransform) 的参数、InventoryGridView.CacheReusableChildren()。
var rt = child as RectTransform;
                if (rt != null && !reusableCells.Contains(rt))
                {
                    reusableCells.Add(rt);
                }
            }
            else if (child.name.StartsWith("Item_", StringComparison.Ordinal))
            {
                // rt（界面矩形变换）：使用位置：作为 Dictionary<GridItem, RectTransform>.ContainsValue(RectTransform) 的参数、作为 List<RectTransform>.Add(RectTransform) 的参数、作为 List<RectTransform>.Contains(RectTransform) 的参数 等共 4 处。
var rt = child as RectTransform;
                if (rt != null && !itemWidgets.ContainsValue(rt) && !pooledItemWidgets.Contains(rt))
                {
                    pooledItemWidgets.Add(rt);
                }
            }
            else if (child.name == "SelectedHighlight")
            {
                selectedOverlay = child.GetComponent<Image>();
            }
            else if (child.name == "HoverHighlight")
            {
                hoverOverlay = child.GetComponent<Image>();
            }
        }
    }

    /// <summary>
    /// 先按旋转后的可用宽高等比适配，再旋转图标。
    /// 这样九十度旋转不会继续沿用旋转前的拉伸矩形，也不会改变贴图比例。
    /// </summary>
    /// <remarks>静态调用链：上游 InventoryGridView.ConfigureItemWidget(RectTransform, GridItem, float, bool)；下游（含内部回调体） Mathf.Abs(int)、Mathf.Max(float, float)、Mathf.RoundToInt(float)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="iconRect">中文含义：图标矩形。使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。</param>
    /// <param name="sprite">中文含义：精灵图。使用位置：作为 Mathf.Max(float, float) 的参数、InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。</param>
    /// <param name="containerSize">中文含义：容器尺寸。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="rotationDegrees">中文含义：旋转度。使用位置：作为 Mathf.RoundToInt(float) 的参数。</param>
    /// <param name="padding">中文含义：留白。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    /// <param name="scale">中文含义：缩放。使用位置：作为 Mathf.Max(float, float) 的参数。</param>
    static void FitIconWithoutDistortion(RectTransform iconRect, Sprite sprite, Vector2 containerSize, float rotationDegrees, float padding, float scale)
    {
        if (iconRect == null || sprite == null)
        {
            return;
        }

        // safePadding（安全留白）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(float, float) 的参数。
float safePadding = Mathf.Max(0f, padding);
        // available（可用）：使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
Vector2 available = new Vector2(Mathf.Max(1f, containerSize.x - safePadding * 2f), Mathf.Max(1f, containerSize.y - safePadding * 2f));
        // quarterTurns（四分之一转数）：使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
int quarterTurns = Mathf.Abs(Mathf.RoundToInt(rotationDegrees / 90f)) % 2;
        if (quarterTurns == 1)
        {
            available = new Vector2(available.y, available.x);
        }

        // spriteWidth（精灵图宽度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
float spriteWidth = Mathf.Max(1f, sprite.rect.width);
        // spriteHeight（精灵图高度）：保存 Mathf.Max(float, float) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
float spriteHeight = Mathf.Max(1f, sprite.rect.height);
        // spriteAspect（精灵图宽高比）：使用位置：作为 Vector2.Vector2(float, float) 的参数、InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
float spriteAspect = spriteWidth / spriteHeight;
        // availableAspect（可用宽高比）：使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
float availableAspect = available.x / available.y;
        // fittedSize（适配后的尺寸）：使用位置：InventoryGridView.FitIconWithoutDistortion(RectTransform, Sprite, Vector2, float, float, float)。
Vector2 fittedSize;
        if (availableAspect > spriteAspect)
        {
            fittedSize = new Vector2(available.y * spriteAspect, available.y);
        }
        else
        {
            fittedSize = new Vector2(available.x, available.x / spriteAspect);
        }

        iconRect.sizeDelta = fittedSize * Mathf.Max(0.01f, scale);
    }

    // ====================== 高亮管理 ======================
    /// <summary>EnsureGlowSprite 的业务处理入口；本方法直接依赖 Color32.Color32(byte, byte, byte, byte)、Mathf.Min(int, int)、Rect.Rect(float, float, float, float)、Sprite.Create(Texture2D, Rect, Vector2, float, uint, SpriteMeshType, Vector4)、Texture2D.Apply()、Texture2D.SetPixels32(Color32[])、Texture2D.Texture2D(int, int, TextureFormat, bool) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.EnsureOverlay(string, Color, float)；下游（含内部回调体） Color32.Color32(byte, byte, byte, byte)、Mathf.Min(int, int)、Rect.Rect(float, float, float, float)、Sprite.Create(Texture2D, Rect, Vector2, float, uint, SpriteMeshType, Vector4)、Texture2D.Apply()、Texture2D.SetPixels32(Color32[])、Texture2D.Texture2D(int, int, TextureFormat, bool) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    Sprite EnsureGlowSprite()
    {
        if (glowSprite != null)
        {
            return glowSprite;
        }

        // size（尺寸）：使用位置：作为 Mathf.Min(int, int) 的参数、作为 Rect.Rect(float, float, float, float) 的参数、作为 Texture2D.Texture2D(int, int, TextureFormat, bool) 的参数 等共 4 处。
int size = 128;
        // border（边框）：使用位置：作为 Vector4.Vector4(float, float, float, float) 的参数、InventoryGridView.EnsureGlowSprite()。
int border = 4;
        // tex（纹理）：使用位置：作为 Sprite.Create(Texture2D, Rect, Vector2, float, uint, SpriteMeshType, Vector4) 的参数、InventoryGridView.EnsureGlowSprite()。
var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        // pixels（像素集合）：使用位置：作为 Texture2D.SetPixels32(Color32[]) 的参数、InventoryGridView.EnsureGlowSprite()。
var pixels = new Color32[size * size];
        // y（纵向）：使用位置：作为 Mathf.Min(int, int) 的参数、InventoryGridView.EnsureGlowSprite()。
for (int y = 0; y < size; y++)
        {
            // x（横向）：使用位置：作为 Mathf.Min(int, int) 的参数、InventoryGridView.EnsureGlowSprite()。
for (int x = 0; x < size; x++)
            {
                // dx（横向增量）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(int, int) 的参数。
int dx = Mathf.Min(x, size - 1 - x);
                // dy（纵向增量）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Min(int, int) 的参数。
int dy = Mathf.Min(y, size - 1 - y);
                // dist（距离）：保存 Mathf.Min(int, int) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.EnsureGlowSprite()。
int dist = Mathf.Min(dx, dy);
                // alpha（透明度）：使用位置：作为 Color32.Color32(byte, byte, byte, byte) 的参数、InventoryGridView.EnsureGlowSprite()。
float alpha = 0f;
                if (dist < border)
                {
                    // t（像素到高亮边界距离的归一化比例）：使用位置：InventoryGridView.EnsureGlowSprite()。
float t = (float)dist / border;
                    alpha = 1f - (1f - t) * (1f - t) * (1f - t);
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        return glowSprite;
    }

    /// <summary>EnsureOverlay 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、InventoryGridView.EnsureGlowSprite()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.ShowHover(GridItem)、InventoryGridView.ShowSelected(GridItem)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;RectTransform&gt;()、GameObject.SetActive(bool)、InventoryGridView.EnsureGlowSprite()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：InventoryGridView.EnsureOverlay(string, Color, float)。</param>
    /// <param name="padding">中文含义：留白。</param>
    Image EnsureOverlay(string name, Color color, float padding)
    {
        // go（当前游戏对象）：使用位置：InventoryGridView.EnsureOverlay(string, Color, float)。
var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(gridContainer, false);
        // rt（界面矩形变换）：保存 GameObject.GetComponent<RectTransform>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.EnsureOverlay(string, Color, float)。
var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // img（界面图像组件）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.EnsureOverlay(string, Color, float)。
var img = go.GetComponent<Image>();
        img.sprite = EnsureGlowSprite();
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        img.color = color;
        go.SetActive(false);
        return img;
    }

    /// <summary>显示选中高亮</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemClicked(GridItem, PointerEventData.InputButton, Vector2)、InventoryUI.OnItemPointerDown(GridItem, Vector2)、InventoryUI.RefreshGridInternal()、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)；下游（含内部回调体） GameObject.SetActive(bool)、InventoryGridView.EnsureOverlay(string, Color, float)、InventoryGridView.HideSelected()、InventoryGridView.PositionOverlay(Image, GridItem, float)、Transform.SetAsLastSibling()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数、InventoryGridView.ShowSelected(GridItem)。</param>
    public void ShowSelected(GridItem gi)
    {
        if (gi == null)
        {
            HideSelected();
            return;
        }

        if (selectedOverlay == null)
        {
            selectedOverlay = EnsureOverlay("SelectedHighlight", selectedColor, selectedPadding);
        }

        PositionOverlay(selectedOverlay, gi, selectedPadding);
        selectedOverlay.gameObject.SetActive(true);
        selectedOverlay.transform.SetAsLastSibling();
    }

    /// <summary>隐藏选中高亮</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.ShowSelected(GridItem)、InventoryUI.ClearDragState()、InventoryUI.HandleContextDiscardButtonClicked()、InventoryUI.HandleContextStoreButtonClicked() 等共 8 处；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideSelected()
    {
        if (selectedOverlay != null)
        {
            selectedOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>显示悬停高亮</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemHoverEnter(GridItem)、StorageBoxUI.OnInventoryItemHoverEnter(GridItem)；下游（含内部回调体） GameObject.SetActive(bool)、InventoryGridView.EnsureOverlay(string, Color, float)、InventoryGridView.HideHover()、InventoryGridView.PositionOverlay(Image, GridItem, float)、Transform.GetSiblingIndex()、Transform.SetSiblingIndex(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.PositionOverlay(Image, GridItem, float) 的参数、InventoryGridView.ShowHover(GridItem)。</param>
    public void ShowHover(GridItem gi)
    {
        if (gi == null)
        {
            HideHover();
            return;
        }

        if (hoverOverlay == null)
        {
            hoverOverlay = EnsureOverlay("HoverHighlight", hoverColor, hoverPadding);
        }

        PositionOverlay(hoverOverlay, gi, hoverPadding);
        hoverOverlay.gameObject.SetActive(true);
        // 悬停层在选中层之下
        if (selectedOverlay != null && selectedOverlay.gameObject.activeSelf)
        {
            hoverOverlay.transform.SetSiblingIndex(selectedOverlay.transform.GetSiblingIndex());
        }
    }

    /// <summary>隐藏悬停高亮</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.RebuildInternal(int, int, IReadOnlyList&lt;GridItem&gt;)、InventoryGridView.ShowHover(GridItem)、InventoryUI.OnItemHoverExit()、InventoryUI.PlayExitAnimation(Action)、StorageBoxUI.OnInventoryItemHoverExit()；下游（含内部回调体） GameObject.SetActive(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideHover()
    {
        if (hoverOverlay != null)
        {
            hoverOverlay.gameObject.SetActive(false);
        }
    }

    /// <summary>PositionOverlay 的业务处理入口；本方法直接依赖 InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryGridView.ShowHover(GridItem)、InventoryGridView.ShowSelected(GridItem)；下游（含内部回调体） InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="overlay">中文含义：叠加层。使用位置：InventoryGridView.PositionOverlay(Image, GridItem, float)。</param>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.GetItemRect(GridItem, float, out Vector2, out Vector2) 的参数。</param>
    /// <param name="padding">中文含义：留白。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    void PositionOverlay(Image overlay, GridItem gi, float padding)
    {
        GetItemRect(gi, CurrentCell, out var /* 中文含义：位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ pos, out var /* 中文含义：尺寸。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ size);
        // rt（界面矩形变换）：使用位置：InventoryGridView.PositionOverlay(Image, GridItem, float)。
var rt = overlay.rectTransform;
        rt.sizeDelta = new Vector2(size.x + padding * 2f, size.y + padding * 2f);
        rt.anchoredPosition = pos;
    }

    /// <summary>设置物品控件的拖拽透明度</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemPointerDown(GridItem, Vector2)、InventoryUI.OnItemPointerUp(GridItem, Vector2)、InventoryUI.RefreshGridInternal()、StorageBoxUI.OnInventoryItemPointerDown(GridItem, Vector2)、StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2) 等共 6 处；下游（含内部回调体） Component.GetComponent&lt;CanvasGroup&gt;()、GameObject.AddComponent&lt;CanvasGroup&gt;()、InventoryGridView.GetItemWidget(GridItem)、Transform.SetAsLastSibling()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="gi">中文含义：背包网格物品。使用位置：作为 InventoryGridView.GetItemWidget(GridItem) 的参数。</param>
    /// <param name="dragging">中文含义：是否拖拽中。使用位置：InventoryGridView.SetItemDragAlpha(GridItem, bool)。</param>
    public void SetItemDragAlpha(GridItem gi, bool dragging)
    {
        // widget（界面控件）：保存 InventoryGridView.GetItemWidget(GridItem) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.SetItemDragAlpha(GridItem, bool)。
RectTransform widget = GetItemWidget(gi);
        if (widget == null)
        {
            return;
        }

        // canvasGroup（画布组）：保存 Component.GetComponent<CanvasGroup>() 的返回结果，供当前步骤使用。使用位置：InventoryGridView.SetItemDragAlpha(GridItem, bool)。
CanvasGroup canvasGroup = widget.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = widget.gameObject.AddComponent<CanvasGroup>();
        }

        // assignmentReceiver22（赋值前保存的目标画布组）：从画布组读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryGridView.SetItemDragAlpha(GridItem, bool)。
global::UnityEngine.CanvasGroup assignmentReceiver22 = canvasGroup;
        if (dragging)
        {
            assignmentReceiver22.alpha = dragAlpha;
        }
        else
        {
            assignmentReceiver22.alpha = 1f;
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        if (dragging)
        {
            widget.SetAsLastSibling();
        }
    }

    /// <summary>获取指定数据实例对应的物品控件。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.LateUpdate()、InventoryGridView.SetItemDragAlpha(GridItem, bool)、InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)、InventoryUI.RefreshGridInternal()、StorageBoxUI.GetItemAnchoredPos(GridItem) 等共 6 处；下游（含内部回调体） Dictionary&lt;GridItem, RectTransform&gt;.TryGetValue(GridItem, out RectTransform)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 Dictionary&lt;GridItem, RectTransform&gt;.TryGetValue(GridItem, out RectTransform) 的参数、InventoryGridView.GetItemWidget(GridItem)。</param>
    public RectTransform GetItemWidget(GridItem item)
    {
        if (item == null)
        {
            return null;
        }

        if (itemWidgets.TryGetValue(item, out RectTransform /* 中文含义：界面控件。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ widget))
        {
            return widget;
        }
        else
        {
            return null;
        }
    }

    /// <summary>拖拽期间移动指定物品控件。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemDrag(GridItem, Vector2)、InventoryUI.RefreshGridInternal()、StorageBoxUI.OnInventoryItemDrag(GridItem, Vector2)、StorageBoxUI.RefreshInventoryGridInternal()；下游（含内部回调体） InventoryGridView.GetItemWidget(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 InventoryGridView.GetItemWidget(GridItem) 的参数。</param>
    /// <param name="anchoredPosition">中文含义：锚定位置。使用位置：InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)。</param>
    public void SetItemWidgetPosition(GridItem item, Vector2 anchoredPosition)
    {
        // widget（界面控件）：保存 InventoryGridView.GetItemWidget(GridItem) 的返回结果，供当前步骤使用。使用位置：InventoryGridView.SetItemWidgetPosition(GridItem, Vector2)。
RectTransform widget = GetItemWidget(item);
        if (widget != null)
        {
            widget.anchoredPosition = anchoredPosition;
        }
    }
}
