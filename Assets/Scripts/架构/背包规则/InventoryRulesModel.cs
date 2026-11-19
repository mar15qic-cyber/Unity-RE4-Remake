using UnityEngine;
using System.Collections.Generic;

/// <summary>InventoryManager 同步转发拾取、移动、旋转、整理、合成、耐久和使用请求到本模型；模型拥有状态，先规划并提交数据，再通过变化、表现或诊断事件通知宿主。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.InventoryRules", "背包规则模型")]
public sealed class InventoryRulesModel
{
    /// <summary>中文含义：公文包宽度。背包网格宽度，Initialize 从宿主传入配置写入。</summary>
    private int caseWidth = 10;
    /// <summary>中文含义：公文包高度。背包网格高度，Initialize 从宿主传入配置写入。</summary>
    private int caseHeight = 6;
    /// <summary>中文含义：只读物品。实际实例列表的只读包装，供旧消费者实时查询。</summary>
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<GridItem> readOnlyItem;
    /// <summary>中文含义：表现请求。成功整理、合成或使用后同步请求表现反馈，宿主订阅处理音效等表现。</summary>
    public event System.Action PresentationRequest;
    /// <summary>中文含义：诊断。配置缺失等诊断消息的同步事件，模型不直接调用场景日志服务。</summary>
    public event System.Action<string> Diagnostics;
    /// <summary>宿主创建模型时建立只读列表包装并初始化空定义目录，正式尺寸与目录由 Initialize 提供。</summary>
    public InventoryRulesModel()
    {
        readOnlyItem = items.AsReadOnly();
        itemCatalog = System.Array.Empty<ItemData>();
    }

    /// <summary>宿主准备本局时调用；提高运行版本，清空物品和获得记录，克隆定义目录并确保规划网格尺寸。</summary>
    /// <param name="width">中文含义：宽度。本局公文包宽度。</param>
    /// <param name="height">中文含义：高度。本局公文包高度。</param>
    /// <param name="recipe">中文含义：配方。物品定义目录；为空则使用空数组，非空则克隆避免外部替换数组元素。</param>
    public void Initialize(int width, int height, ItemData[] recipe)
    {
        RunVersion++;
        caseWidth = width;
        caseHeight = height;
        items.Clear();
        acquiredItemIds.Clear();
        if (recipe == null)
        {
            itemCatalog = System.Array.Empty<ItemData>();
        }
        else
        {
            itemCatalog = (ItemData[])recipe.Clone();
        }

        EnsureGrid();
    }

    /// <summary>中文含义：物品列表。本模型唯一拥有的物品实例列表，所有增删和布局经模型入口提交。</summary>
    private List<GridItem> items = new List<GridItem>();
    /// <summary>中文含义：放置位置网格。与当前尺寸一致的放置规划占位缓冲，不是额外的物品数据副本。</summary>
    private bool[, ] placementGrid;
    /// <summary>中文含义：放置位置变化集合。新增和转移时复用的规划列表，失败清空，成功后统一应用。</summary>
    private readonly List<PlacementChange> placementChanges = new List<PlacementChange>();
    /// <summary>中文含义：回滚状态集合。合成期间保存的原实例字段快照，失败恢复后清空。</summary>
    private readonly List<ItemState> rollbackStates = new List<ItemState>();
    /// <summary>中文含义：运行版本。每次 Initialize 递增的运行版本，物品使用和延迟寄存据此拒绝旧回调。</summary>
    public int RunVersion { get; private set; }

    /// <summary>一次新增或合并计划，规划阶段记录目标及放置数值，所有位置成功后才统一提交。</summary>
    private struct PlacementChange
    {
        /// <summary>中文含义：目标背包网格物品。原物品实例引用，提交或回退时保留其身份。</summary>
        internal GridItem target;
        /// <summary>中文含义：数量。记录或规划的堆叠数量。</summary>
        internal int count;
        /// <summary>中文含义：横向。记录或规划的横坐标。</summary>
        internal int x;
        /// <summary>中文含义：纵向。记录或规划的纵坐标。</summary>
        internal int y;
        /// <summary>中文含义：旋转。记录或规划的四分之一圈旋转编号。</summary>
        internal int rotation;
    }

    /// <summary>合成事务的原实例与字段回退记录，失败时原位恢复，不替换实例身份。</summary>
    private struct ItemState
    {
        /// <summary>中文含义：目标背包网格物品。原物品实例引用，提交或回退时保留其身份。</summary>
        internal GridItem target;
        /// <summary>中文含义：当前物品数据配置。记录时的物品定义引用。</summary>
        internal ItemData item;
        /// <summary>中文含义：数量。记录或规划的堆叠数量。</summary>
        internal int count;
        /// <summary>中文含义：横向。记录或规划的横坐标。</summary>
        internal int x;
        /// <summary>中文含义：纵向。记录或规划的纵坐标。</summary>
        internal int y;
        /// <summary>中文含义：旋转。记录或规划的四分之一圈旋转编号。</summary>
        internal int rotation;
        /// <summary>中文含义：耐久。回退记录中的原始耐久。</summary>
        internal int durability;
    }

    /// <summary>中文含义：物品目录列表。宿主传入并克隆的物品定义目录，用于查找配方和损坏替代品。</summary>
    private ItemData[] itemCatalog;
    /// <summary>中文含义：需求缓冲区。配方解析共用的材料需求缓冲，按物品标识合并数量。</summary>
    private readonly List<CraftingIngredientRequirement> requirementBuffer = new List<CraftingIngredientRequirement>();
    /// <summary>中文含义：已取得物品编号集合。本局曾成功加入的物品标识集合，用于首次获得等资格查询。</summary>
    private readonly HashSet<string> acquiredItemIds = new HashSet<string>();
    /// <summary>中文含义：背包变化时的通知回调。完整数据提交后的同步事实通知，消费者读取当前结果刷新显示。</summary>
    public event System.Action OnInventoryChanged;
    /// <summary>兼容查询入口，返回当前实例的实时只读包装；独立值查询使用 ReadSnapshot。</summary>
    public IReadOnlyList<GridItem> GetItems() => readOnlyItem;
    /// <summary>宿主或新查询消费者调用，逐实例复制数值并返回独立只读集合，后续修改不追随更新。</summary>
    public System.Collections.ObjectModel.ReadOnlyCollection<ItemStateSnapshot> ReadSnapshot()
    {
        // 本次独立状态数组，不暴露内部实例列表。
        // 中文含义：result 为“当前物品状态快照数组”。
var result = new ItemStateSnapshot[items.Count];
        // 当前实例及结果数组的共用索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < items.Count; i++)
        {
            result[i] = new ItemStateSnapshot(items[i]);
        }

        return System.Array.AsReadOnly(result);
    }

    /// <summary>ItemUseService 成功提交角色效果和物品消费后调用，先同步请求表现，再发布背包变化。</summary>
    internal void CompleteUseFeedback()
    {
        // 本次表现通知接收者的委托快照，保持原条件调用的单次读取语义。
        // 中文含义：presentationHandler 为“表现处理器”。
System.Action presentationHandler = PresentationRequest;
        if (presentationHandler != null)
        {
            presentationHandler();
        }

        NotifyChanged();
    }

    /// <summary>兼容显示刷新入口，只转发已有变化通知，不改变背包数据。</summary>
    public void NotificationViewRefresh() => NotifyChanged();
    /// <summary>中文含义：公文包宽度。消费者只读查询当前公文包宽度。</summary>
    public int CaseWidth => caseWidth;
    /// <summary>中文含义：公文包高度。消费者只读查询当前公文包高度。</summary>
    public int CaseHeight => caseHeight;

    /// <summary>放置相关入口共用的缓冲准备，尺寸变化时重新分配规划网格；不重新创建物品实例。</summary>
    void EnsureGrid()
    {
        // 当前宽度限制到非负后用于分配缓冲。
        // 中文含义：width 为“宽度”。
int width = Mathf.Max(0, caseWidth);
        // 当前高度限制到非负后用于分配缓冲。
        // 中文含义：height 为“高度”。
int height = Mathf.Max(0, caseHeight);
        if (placementGrid == null || placementGrid.GetLength(0) != width || placementGrid.GetLength(1) != height)
        {
            placementGrid = new bool[width, height];
        }
    }

    // ========================== 占用检查 ==========================
    /// <summary>移动、旋转和空位查找共用的占位判断，先检查边界，再逐格排除已有实例。</summary>
    /// <param name="x">中文含义：横向。拟放置左上角横坐标。</param>
    /// <param name="y">中文含义：纵向。拟放置左上角纵坐标。</param>
    /// <param name="w">中文含义：宽度。旋转后宽度。</param>
    /// <param name="h">中文含义：高度。旋转后高度。</param>
    /// <param name="excludeItem">中文含义：排除物品。移动或旋转时排除自身实例，允许为空。</param>
    bool CanPlaceAt(int x, int y, int w, int h, GridItem excludeItem = null)
    {
        EnsureGrid();
        if (x < 0 || y < 0 || x + w > caseWidth || y + h > caseHeight)
        {
            return false;
        }

        // 拟放置矩形内的横向偏移。
        // 中文含义：dx 为“横向增量”。
for (int dx = 0; dx < w; dx++)
        {
            // 拟放置矩形内的纵向偏移。
            // 中文含义：dy 为“纵向增量”。
for (int dy = 0; dy < h; dy++)
            {
                // 当前检查格子的绝对横坐标。
                // 中文含义：gx 为“网格横坐标”。
int gx = x + dx;
                // 当前检查格子的绝对纵坐标。
                // 中文含义：gy 为“网格纵坐标”。
int gy = y + dy;
                // 当前格子是否被排除实例以外的物品占用。
                // 中文含义：occupiedByOther 为“是否占用的依据另一个”。
bool occupiedByOther = false;
                // 本轮检查的已有实例，排除自身后核对占位。
                // 中文含义：gi 为“背包网格物品”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (var gi in items)
                {
                    if (gi == excludeItem)
                    {
                        continue;
                    }

                    if (gi.Occupies(gx, gy))
                    {
                        occupiedByOther = true;
                        break;
                    }
                }

                if (occupiedByOther)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>宿主格子查询入口，返回第一个占据该格子的实例，未命中返回 null。</summary>
    /// <param name="x">中文含义：横向。格子横坐标。</param>
    /// <param name="y">中文含义：纵向。格子纵坐标。</param>
    public GridItem GetItemAt(int x, int y)
    {
        // 本轮检查占位的实例，命中后直接返回。
        // 中文含义：gi 为“背包网格物品”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (var gi in items)
        {
            if (gi.Occupies(x, y))
            {
                return gi;
            }
        }

        return null;
    }

    // ========================== 添加物品 ==========================
    /// <summary>拾取宿主添加普通物品；TryAddPlanned 全部规划成功才提交，随后通知背包变化。</summary>
    /// <param name="item">中文含义：当前物品数据配置。要加入的物品定义。</param>
    /// <param name="count">中文含义：数量。本次加入数量，默认一。</param>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (!TryAddPlanned(item, count, ResolveInitialDurability(item), null))
        {
            return false;
        }

        NotifyChanged();
        return true;
    }

    /// <summary>使用、转移及布局入口核验归属，只接受本模型实际持有的非空实例。</summary>
    /// <param name="source">中文含义：来源背包网格物品。待核验物品实例。</param>
    public bool TryAddWeaponWithRearrangement(ItemData weapon)
    {
        if (weapon == null || weapon.itemType != ItemType.Weapon || weapon.gridWidth <= 0 || weapon.gridHeight <= 0)
        {
            return false;
        }

        EnsureGrid();
        var displaced = new List<GridItem>();
        for (int y = 0; y <= caseHeight - weapon.gridHeight; y++)
        {
            for (int x = 0; x <= caseWidth - weapon.gridWidth; x++)
            {
                var slot = new RectInt(x, y, weapon.gridWidth, weapon.gridHeight);
                if (!TryPlanWeaponSlot(slot, displaced))
                {
                    continue;
                }

                // 所有被挡住的物品均已找到位置，统一提交移动和拾取。
                foreach (PlacementChange change in placementChanges)
                {
                    change.target.gridX = change.x;
                    change.target.gridY = change.y;
                    change.target.rotation = change.rotation;
                }
                placementChanges.Clear();
                items.Add(new GridItem
                {
                    item = weapon,
                    count = 1,
                    gridX = x,
                    gridY = y,
                    currentDurability = ResolveInitialDurability(weapon)
                });
                RegisterAcquiredItem(weapon);
                NotifyChanged();
                return true;
            }
        }

        placementChanges.Clear();
        return false;
    }

    private bool TryPlanWeaponSlot(RectInt slot, List<GridItem> displaced)
    {
        placementChanges.Clear();
        displaced.Clear();
        System.Array.Clear(placementGrid, 0, placementGrid.Length);
        MarkPlacement(slot.x, slot.y, slot.width, slot.height);
        foreach (GridItem existing in items)
        {
            var occupied = new RectInt(existing.gridX, existing.gridY, existing.Width, existing.Height);
            if (slot.Overlaps(occupied))
            {
                displaced.Add(existing);
            }
            else
            {
                MarkPlacement(occupied.x, occupied.y, occupied.width, occupied.height);
            }
        }

        displaced.Sort((first, second) => (second.Width * second.Height).CompareTo(first.Width * first.Height));
        foreach (GridItem existing in displaced)
        {
            int width = existing.Width;
            int height = existing.Height;
            int rotation = existing.rotation;
            if (!FindPlannedSlot(width, height, out Vector2Int position))
            {
                if (width == height || !FindPlannedSlot(height, width, out position))
                {
                    return false;
                }
                (width, height) = (height, width);
                rotation = (rotation + 1) % 4;
            }

            MarkPlacement(position.x, position.y, width, height);
            placementChanges.Add(new PlacementChange { target = existing, x = position.x, y = position.y, rotation = rotation });
        }

        return true;
    }

    internal bool ContainsInstance(GridItem source) => source != null && items.Contains(source);
    /// <summary>使用或跨容器事务内部调用，只移除正数量的已持有实例；通知由整个事务提交后统一发送。</summary>
    /// <param name="source">中文含义：来源背包网格物品。待移除的已持有实例。</param>
    internal bool RemoveInstanceSilently(GridItem source)
    {
        return source != null && source.count > 0 && items.Remove(source);
    }

    /// <summary>仓库取出事务调用，带入原实例及耐久做完整放置规划，成功后由外部提交链统一通知。</summary>
    /// <param name="source">中文含义：来源背包网格物品。仓库拟转入的原实例，不得已归属本背包。</param>
    internal bool AddTransferredSilently(GridItem source)
    {
        return source != null && !items.Contains(source) && TryAddPlanned(source.item, source.count, source.currentDurability, source);
    }

    /// <summary>仓库确认双方数据都提交后调用，发布一次背包变化事实。</summary>
    internal void PublishTransfer() => NotifyChanged();
    /// <summary>添加与转移共用；先规划堆叠、旋转和全部占位，任一放置失败则不写实例，全部成功才提交并登记本局获得。</summary>
    /// <param name="item">中文含义：当前物品数据配置。目标定义。</param>
    /// <param name="count">中文含义：数量。待放入的数量，规划中逐步减少。</param>
    /// <param name="durability">中文含义：耐久。所有新增实例或合并检查使用的耐久。</param>
    /// <param name="source">中文含义：来源背包网格物品。跨容器时可复用的原实例，普通拾取为空。</param>
    private bool TryAddPlanned(ItemData item, int count, int durability, GridItem source)
    {
        if (item == null || count <= 0 || item.gridWidth <= 0 || item.gridHeight <= 0)
        {
            return false;
        }

        EnsureGrid();
        placementChanges.Clear();
        System.Array.Clear(placementGrid, 0, placementGrid.Length);
        // 本轮已有实例；先标记当前占位，合并阶段再核对定义、容量和耐久。
        // 中文含义：existing 为“已有”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (GridItem existing in items)
        {
            if (existing == null || existing.item == null)
            {
                continue;
            }

            MarkPlacement(existing.gridX, existing.gridY, existing.Width, existing.Height);
        }

        // 单堆叠有效上限，至少为一。
        // 中文含义：maximum 为“最大”。
int maximum = Mathf.Max(1, item.maxStack);
        if (maximum > 1)
        {
            // 本轮已有实例；先标记当前占位，合并阶段再核对定义、容量和耐久。
            // 中文含义：existing 为“已有”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (GridItem existing in items)
            {
                if (existing.item != item || existing.count >= maximum || item.usesDurability && existing.currentDurability != durability)
                {
                    continue;
                }

                // 本条规划可接收的数量，受剩余数量与堆叠容量限制。
                // 中文含义：amount 为“大小”。
int amount = Mathf.Min(count, maximum - existing.count);
                placementChanges.Add(new PlacementChange { target = existing, count = amount });
                count -= amount;
                if (count == 0)
                {
                    break;
                }
            }
        }

        while (count > 0)
        {
            // 新增堆叠的规划旋转，先尝试零，必要时尝试一。
            // 中文含义：rotation 为“旋转”。
int rotation = 0;
            // 新增堆叠的规划左上角位置。
            // 中文含义：position 为“位置”。
Vector2Int position;
            if (!FindPlannedSlot(item.gridWidth, item.gridHeight, out position))
            {
                if (item.gridWidth == item.gridHeight || !FindPlannedSlot(item.gridHeight, item.gridWidth, out position))
                {
                    placementChanges.Clear();
                    return false;
                }

                rotation = 1;
            }

            // 本条规划可接收的数量，受剩余数量与堆叠容量限制。
            // 中文含义：amount 为“大小”。
int amount = Mathf.Min(count, maximum);
            placementChanges.Add(new PlacementChange { count = amount, x = position.x, y = position.y, rotation = rotation });
            // 按规划旋转选择的占位宽度。
            // 中文含义：plannedWidth 为“计划的宽度”。
int plannedWidth;
            // 按规划旋转选择的占位高度。
            // 中文含义：plannedHeight 为“计划的高度”。
int plannedHeight;
            if (rotation == 0)
            {
                plannedWidth = item.gridWidth;
                plannedHeight = item.gridHeight;
            }
            else
            {
                plannedWidth = item.gridHeight;
                plannedHeight = item.gridWidth;
            }

            MarkPlacement(position.x, position.y, plannedWidth, plannedHeight);
            count -= amount;
        }

        // 是否已经复用跨容器源实例，最多复用一次。
        // 中文含义：reusedSource 为“是否复用的来源”。
bool reusedSource = false;
        // 当前待提交规划项，已有目标只加数量，新目标同时写入布局。
        // 中文含义：change 为“变化”。每轮从放置位置变化集合取得一个元素，仅代表当前这一项。
foreach (PlacementChange change in placementChanges)
        {
            if (change.target != null)
            {
                change.target.count += change.count;
                continue;
            }

            // 本条新增规划实际使用的实例，优先一次复用源对象。
            // 中文含义：added 为“新增”。
GridItem added;
            if (source != null && !reusedSource)
            {
                added = source;
            }
            else
            {
                added = new GridItem();
            }

            reusedSource = true;
            added.item = item;
            added.count = change.count;
            added.gridX = change.x;
            added.gridY = change.y;
            added.rotation = change.rotation;
            added.currentDurability = durability;
            items.Add(added);
        }

        placementChanges.Clear();
        RegisterAcquiredItem(item);
        return true;
    }

    /// <summary>新增规划链调用，按先行后列扫描规划网格，返回首个能容纳指定矩形的位置。</summary>
    /// <param name="width">中文含义：宽度。待放置宽度。</param>
    /// <param name="height">中文含义：高度。待放置高度。</param>
    /// <param name="position">中文含义：位置。成功时输出左上角位置，失败时输出默认值。</param>
    private bool FindPlannedSlot(int width, int height, out Vector2Int position)
    {
        // 候选矩形左上角的逐行纵坐标。
        // 中文含义：y 为“纵向”。
for (int y = 0; y <= caseHeight - height; y++)
        {
            // 候选矩形左上角的逐列横坐标。
            // 中文含义：x 为“横向”。
for (int x = 0; x <= caseWidth - width; x++)
            {
                // 当前候选矩形是否仍未发现占用。
                // 中文含义：available 为“是否可用”。
bool available = true;
                // 候选矩形内部纵向偏移，发现占用后提前结束。
                // 中文含义：dy 为“纵向增量”。
for (int dy = 0; dy < height && available; dy++)
                {
                    // 候选矩形内部横向偏移。
                    // 中文含义：dx 为“横向增量”。
for (int dx = 0; dx < width; dx++)
                    {
                        if (placementGrid[x + dx, y + dy])
                        {
                            available = false;
                            break;
                        }
                    }
                }

                if (available)
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    /// <summary>新增规划标记已占用矩形，只写规划缓冲，裁剪到当前网格边界。</summary>
    /// <param name="x">中文含义：横向。矩形横坐标。</param>
    /// <param name="y">中文含义：纵向。矩形纵坐标。</param>
    /// <param name="width">中文含义：宽度。矩形宽度。</param>
    /// <param name="height">中文含义：高度。矩形高度。</param>
    private void MarkPlacement(int x, int y, int width, int height)
    {
        // 裁剪后的绝对纵坐标。
        // 中文含义：dy 为“纵向增量”。
for (int dy = Mathf.Max(0, y); dy < Mathf.Min(caseHeight, y + height); dy++)
        {
            // 裁剪后的绝对横坐标。
            // 中文含义：dx 为“横向增量”。
for (int dx = Mathf.Max(0, x); dx < Mathf.Min(caseWidth, x + width); dx++)
            {
                placementGrid[dx, dy] = true;
            }
        }
    }

    /// <summary>两材料组合或数量配方合成开始前调用，保存每个原实例及字段用于原位恢复。</summary>
    private void CaptureTransaction()
    {
        rollbackStates.Clear();
        // 本轮保存的原实例，字段写入回退记录但不替换实例。
        // 中文含义：item 为“当前背包网格物品”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (GridItem item in items)
        {
            rollbackStates.Add(new ItemState { target = item, item = item.item, count = item.count, x = item.gridX, y = item.gridY, rotation = item.rotation, durability = item.currentDurability });
        }
    }

    /// <summary>合成扣料或放入产物失败时调用，恢复原实例集合及每个字段，再清空回退记录。</summary>
    private void RestoreTransaction()
    {
        items.Clear();
        // 本轮回退项，逐字段恢复原对象后重新加入集合。
        // 中文含义：state 为“当前物品状态”。每轮从回滚状态集合取得一个元素，仅代表当前这一项。
foreach (ItemState state in rollbackStates)
        {
            state.target.item = state.item;
            state.target.count = state.count;
            state.target.gridX = state.x;
            state.target.gridY = state.y;
            state.target.rotation = state.rotation;
            state.target.currentDurability = state.durability;
            items.Add(state.target);
        }

        rollbackStates.Clear();
    }

    /// <summary>首次获得消费者通过宿主查询本局曾加入记录，不取决于物品当前是否仍在背包。</summary>
    /// <param name="itemId">中文含义：物品编号。非空物品定义标识。</param>
    public bool HasAcquiredThisRun(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && acquiredItemIds.Contains(itemId);
    }

    /// <summary>新增规划实际提交后调用，将有效物品标识登记为本局已获得。</summary>
    /// <param name="item">中文含义：当前物品数据配置。已成功加入的定义。</param>
    void RegisterAcquiredItem(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId))
        {
            return;
        }

        acquiredItemIds.Add(item.itemId);
    }

    /// <summary>自动整理调用，逐位置使用 CanPlaceAt 校验，返回首个空位或无值。</summary>
    /// <param name="w">中文含义：宽度。目标宽度。</param>
    /// <param name="h">中文含义：高度。目标高度。</param>
    /// <param name="exclude">中文含义：排除。占位检查中排除的实例，整理时为空。</param>
    Vector2Int? FindEmptySlot(int w, int h, GridItem exclude)
    {
        // 按行扫描的候选纵坐标。
        // 中文含义：y 为“纵向”。
for (int y = 0; y <= caseHeight - h; y++)
        {
            // 当前行内扫描的候选横坐标。
            // 中文含义：x 为“横向”。
for (int x = 0; x <= caseWidth - w; x++)
            {
                if (CanPlaceAt(x, y, w, h, exclude))
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return null;
    }

    // ========================== 移动/旋转 ==========================
    /// <summary>布局宿主提交明确目标位置；检查实例归属和完整占位后写坐标并通知，失败保留原位置。</summary>
    /// <param name="item">中文含义：当前背包网格物品。本背包持有的实例。</param>
    /// <param name="newX">中文含义：新横向。目标横坐标。</param>
    /// <param name="newY">中文含义：新纵向。目标纵坐标。</param>
    public bool MoveItem(GridItem item, int newX, int newY)
    {
        if (!ContainsInstance(item))
        {
            return false;
        }

        EnsureGrid();
        // 当前旋转后的实例宽度。
        // 中文含义：w 为“物品的宽度”。
int w = item.Width;
        // 当前旋转后的实例高度。
        // 中文含义：h 为“物品的高度”。
int h = item.Height;
        if (!CanPlaceAt(newX, newY, w, h, item))
        {
            return false;
        }

        item.gridX = newX;
        item.gridY = newY;
        NotifyChanged();
        return true;
    }

    /// <summary>拖动宿主提交偏好位置；先尝试裁剪后位置，再按指定半径选择最近合法格子并提交。</summary>
    /// <param name="item">中文含义：当前背包网格物品。本背包持有的实例。</param>
    /// <param name="preferredX">中文含义：优先横向。偏好横坐标。</param>
    /// <param name="preferredY">中文含义：优先纵向。偏好纵坐标。</param>
    /// <param name="snapRadius">中文含义：吸附半径。目标附近吸附搜索半径，至少按零处理。</param>
    public bool MoveItemNear(GridItem item, int preferredX, int preferredY, int snapRadius = 1)
    {
        if (!ContainsInstance(item))
        {
            return false;
        }

        if (item == null)
        {
            return false;
        }

        EnsureGrid();
        // 当前旋转后的实例宽度。
        // 中文含义：width 为“宽度”。
int width = item.Width;
        // 当前旋转后的实例高度。
        // 中文含义：height 为“高度”。
int height = item.Height;
        // 不越过右边界的最大横坐标，至少为零。
        // 中文含义：maxX 为“最大横向”。
int maxX = Mathf.Max(0, caseWidth - width);
        // 不越过下边界的最大纵坐标，至少为零。
        // 中文含义：maxY 为“最大纵向”。
int maxY = Mathf.Max(0, caseHeight - height);
        preferredX = Mathf.Clamp(preferredX, 0, maxX);
        preferredY = Mathf.Clamp(preferredY, 0, maxY);
        if (CanPlaceAt(preferredX, preferredY, width, height, item))
        {
            item.gridX = preferredX;
            item.gridY = preferredY;
            NotifyChanged();
            return true;
        }

        // 搜索半径限制到非负后的值。
        // 中文含义：radius 为“半径”。
int radius = Mathf.Max(0, snapRadius);
        // 当前最优合法横坐标，-1 表示尚未找到。
        // 中文含义：bestX 为“最佳横向”。
int bestX = -1;
        // 当前最优合法纵坐标，-1 表示尚未找到。
        // 中文含义：bestY 为“最佳纵向”。
int bestY = -1;
        // 当前最优平方距离，初始为最大整数。
        // 中文含义：bestDistance 为“最佳距离”。
int bestDistance = int.MaxValue;
        // 吸附范围内候选纵坐标。
        // 中文含义：y 为“纵向”。
for (int y = Mathf.Max(0, preferredY - radius); y <= Mathf.Min(maxY, preferredY + radius); y++)
        {
            // 吸附范围内候选横坐标。
            // 中文含义：x 为“横向”。
for (int x = Mathf.Max(0, preferredX - radius); x <= Mathf.Min(maxX, preferredX + radius); x++)
            {
                if (!CanPlaceAt(x, y, width, height, item))
                {
                    continue;
                }

                // 候选相对偏好位置的横向偏差。
                // 中文含义：dx 为“横向增量”。
int dx = x - preferredX;
                // 候选相对偏好位置的纵向偏差。
                // 中文含义：dy 为“纵向增量”。
int dy = y - preferredY;
                // 候选平方距离；相同距离保留先找到的位置。
                // 中文含义：distance 为“距离”。
int distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestX = x;
                bestY = y;
            }
        }

        if (bestX < 0)
        {
            return false;
        }

        item.gridX = bestX;
        item.gridY = bestY;
        NotifyChanged();
        return true;
    }

    /// <summary>布局宿主请求顺时针转九十度；临时应用新布局校验，碰撞时恢复原坐标及旋转，成功才通知。</summary>
    /// <param name="item">中文含义：当前背包网格物品。要旋转的已持有实例。</param>
    public bool RotateItem(GridItem item)
    {
        if (!ContainsInstance(item))
        {
            return false;
        }

        EnsureGrid();
        // 顺时针增加一格并限制在四种方向内的目标编号。
        // 中文含义：newRotation 为“新旋转”。
int newRotation = (item.rotation + 1) % 4;
        // 目标旋转方向下的宽度。
        // 中文含义：newW 为“新宽度”。
int newW;
        // 目标旋转方向下的高度。
        // 中文含义：newH 为“新高度”。
int newH;
        if (newRotation % 2 == 0)
        {
            newW = item.item.gridWidth;
            newH = item.item.gridHeight;
        }
        else
        {
            newW = item.item.gridHeight;
            newH = item.item.gridWidth;
        }

        // 检查旋转后是否越界或碰撞
        // 为防越界而调整的目标横坐标。
        // 中文含义：newX 为“新横向”。
int newX = item.gridX;
        // 为防越界而调整的目标纵坐标。
        // 中文含义：newY 为“新纵向”。
int newY = item.gridY;
        if (newX + newW > caseWidth)
        {
            newX = caseWidth - newW;
        }

        if (newY + newH > caseHeight)
        {
            newY = caseHeight - newH;
        }

        if (newX < 0)
        {
            newX = 0;
        }

        if (newY < 0)
        {
            newY = 0;
        }

        // 临时保存原状态
        // 验证前保存的原旋转，失败时恢复。
        // 中文含义：oldRotation 为“旧旋转”。
int oldRotation = item.rotation;
        // 验证前保存的原横坐标，失败时恢复。
        // 中文含义：oldX 为“旧横向”。
int oldX = item.gridX;
        // 验证前保存的原纵坐标，失败时恢复。
        // 中文含义：oldY 为“旧纵向”。
int oldY = item.gridY;
        item.rotation = newRotation;
        item.gridX = newX;
        item.gridY = newY;
        if (!CanPlaceAt(newX, newY, newW, newH, item))
        {
            // 回滚
            item.rotation = oldRotation;
            item.gridX = oldX;
            item.gridY = oldY;
            return false;
        }

        NotifyChanged();
        return true;
    }

    // ========================== 一键整理 ==========================
    /// <summary>整理宿主调用；按定义面积降序排序，逐件尝试原向和旋转放置，最终请求表现并通知当前结果。</summary>
    public void Sort()
    {
        EnsureGrid();
        // 保存所有物品
        // 保留原实例引用的排序工作列表，随后逐件重新放入。
        // 中文含义：savedItems 为“已保存物品列表”。
var savedItems = new List<GridItem>(items);
        items.Clear();
        // 按面积降序排列
        // 排序委托参数 a、b 是两件候选实例，比较其定义占位面积，不修改候选本身。
        savedItems.Sort((/* 中文含义：当前背包网格物品。 */ a, /* 中文含义：当前背包网格物品。 */ b) =>
        {
            // 排序委托候选 a 的定义占位面积。
            // 中文含义：areaA 为“区域起点”。
int areaA = a.item.gridWidth * a.item.gridHeight;
            // 排序委托候选 b 的定义占位面积。
            // 中文含义：areaB 为“区域终点”。
int areaB = b.item.gridWidth * b.item.gridHeight;
            return areaB.CompareTo(areaA);
        });
        // 贪心放置
        // 本轮贪心放置的原实例。
        // 中文含义：gi 为“背包网格物品”。每轮从已保存物品列表取得一个元素，仅代表当前这一项。
foreach (var gi in savedItems)
        {
            // 尝试不旋转
            // 当前方向找到的可空位置，原向失败后可改查旋转方向。
            // 中文含义：pos 为“位置”。
var pos = FindEmptySlot(gi.item.gridWidth, gi.item.gridHeight, null);
            if (pos.HasValue)
            {
                gi.gridX = pos.Value.x;
                gi.gridY = pos.Value.y;
                gi.rotation = 0;
                items.Add(gi);
                continue;
            }

            // 尝试旋转
            if (gi.item.gridWidth != gi.item.gridHeight)
            {
                pos = FindEmptySlot(gi.item.gridHeight, gi.item.gridWidth, null);
                if (pos.HasValue)
                {
                    gi.gridX = pos.Value.x;
                    gi.gridY = pos.Value.y;
                    gi.rotation = 1;
                    items.Add(gi);
                    continue;
                }
            }

            // 放不下，原样保留
            gi.gridX = 0;
            gi.gridY = 0;
            items.Add(gi);
        }

        // 本次表现通知接收者的委托快照，保持原条件调用的单次读取语义。
        // 中文含义：presentationHandler 为“表现处理器”。
System.Action presentationHandler = PresentationRequest;
        if (presentationHandler != null)
        {
            presentationHandler();
        }

        NotifyChanged();
    }

    // ========================== 使用/移除 ==========================
    /// <summary>物品服务和宿主查询直接使用资格，允许弹药和武器；治疗品要求配置至少一种生命效果。</summary>
    /// <param name="item">中文含义：当前物品数据配置。待检查的物品定义。</param>
    public bool CanUseItem(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.itemType == ItemType.Ammo || item.itemType == ItemType.Weapon)
        {
            return true;
        }

        if (item.itemType != ItemType.Healing)
        {
            return false;
        }

        return item.healAmount > 0f || item.maxHealthBonus > 0f;
    }

    /// <summary>拖放宿主只读查询两件实例是否对应现有双材料配方，复用 FindCraftResult。</summary>
    /// <param name="first">中文含义：第一个背包网格物品。第一件候选实例。</param>
    /// <param name="second">中文含义：第二个背包网格物品。第二件候选实例。</param>
    public bool CanCombine(GridItem first, GridItem second)
    {
        return FindCraftResult(first, second) != null;
    }

    /// <summary>拖放组合提交入口；验证材料、保存事务、扣料并规划产物，失败恢复原实例，成功后发送表现与变化事实。</summary>
    /// <param name="first">中文含义：第一个背包网格物品。第一件材料实例。</param>
    /// <param name="second">中文含义：第二个背包网格物品。第二件材料实例，可与第一件为同一堆叠。</param>
    /// <param name="result">中文含义：当前物品数据配置。成功时的产物定义，失败时清为空。</param>
    public bool TryCombine(GridItem first, GridItem second, out ItemData result)
    {
        result = null;
        if (!ContainsInstance(first) || !ContainsInstance(second) || first.item == null || second.item == null || first.count <= 0 || second.count <= 0)
        {
            return false;
        }

        result = FindCraftResult(first, second);
        if (result == null)
        {
            return false;
        }

        // 两个输入是否引用同一堆叠，同堆叠至少需要两份。
        // 中文含义：sameStack 为“是否相同堆叠”。
bool sameStack = ReferenceEquals(first, second);
        if (sameStack && first.count < 2)
        {
            result = null;
            return false;
        }

        CaptureTransaction();
        ConsumeGridItem(first);
        ConsumeGridItem(second);
        // 单次组合产物数量，至少为一。
        // 中文含义：resultCount 为“结果数量”。
int resultCount = Mathf.Max(1, result.craftingResultCount);
        if (TryAddPlanned(result, resultCount, ResolveInitialDurability(result), null))
        {
            // 直接组合与配方分页使用相同的成功音效。
            // 本次表现通知接收者的委托快照，保持原条件调用的单次读取语义。
            // 中文含义：presentationHandler 为“表现处理器”。
System.Action presentationHandler = PresentationRequest;
            if (presentationHandler != null)
            {
                presentationHandler();
            }

            rollbackStates.Clear();
            NotifyChanged();
            return true;
        }

        RestoreTransaction();
        result = null;
        return false;
    }

    /// <summary>合成分页查询入口；收集允许显示的产物，按弹药、治疗、其他类别及显示名称排序返回新列表。</summary>
    public List<ItemData> GetCraftingRecipes()
    {
        EnsureItemCatalog();
        // 供本次分页查询返回的新产物列表。
        // 中文含义：recipes 为“配方集合”。
var recipes = new List<ItemData>();
        // 定义目录索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < itemCatalog.Length; i++)
        {
            // 当前目录候选定义。
            // 中文含义：item 为“当前物品数据配置”。
ItemData item = itemCatalog[i];
            if (IsCraftResult(item) && item.showInCraftingMenu)
            {
                recipes.Add(item);
            }
        }

        // 排序委托参数 left、right 是两个配方产物，先比较类别，再按显示名称序数排序。
        recipes.Sort((/* 中文含义：左侧。 */ left, /* 中文含义：右侧。 */ right) =>
        {
            // 排序委托先比较两产物类别的结果，只有相同时才比较名称。
            // 中文含义：typeComparison 为“类型比较”。
int typeComparison = GetCraftingSortOrder(left).CompareTo(GetCraftingSortOrder(right));
            if (typeComparison != 0)
            {
                return typeComparison;
            }

            return string.Compare(left.displayName, right.displayName, System.StringComparison.Ordinal);
        });
        return recipes;
    }

    /// <summary>配方列表排序委托调用，弹药优先、治疗其次，其他随后，空定义排最后。</summary>
    /// <param name="item">中文含义：当前物品数据配置。待排序产物定义。</param>
    static int GetCraftingSortOrder(ItemData item)
    {
        if (item == null)
        {
            return int.MaxValue;
        }

        if (item.itemType == ItemType.Ammo)
        {
            return 0;
        }

        if (item.itemType == ItemType.Healing)
        {
            return 1;
        }

        return 2;
    }

    /// <summary>合成宿主只读查询材料数量是否充足，使用合并需求缓冲逐条核对库存。</summary>
    /// <param name="result">中文含义：当前物品数据配置。拟合成的产物定义。</param>
    public bool CanCraft(ItemData result)
    {
        if (!TryBuildRequirements(result, requirementBuffer))
        {
            return false;
        }

        // 合并需求缓冲索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < requirementBuffer.Count; i++)
        {
            // 当前待核对数量的材料需求。
            // 中文含义：requirement 为“需求”。
CraftingIngredientRequirement requirement = requirementBuffer[i];
            if (!HasItem(requirement.itemId, requirement.count))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>数量配方提交入口；保存原实例，逐项静默扣料并规划产物，任一失败原位恢复，成功后统一通知。</summary>
    /// <param name="result">中文含义：当前物品数据配置。拟合成的产物定义。</param>
    /// <param name="craftedCount">中文含义：合成后的数量。成功时输出产物数量，失败时为零。</param>
    public bool TryCraft(ItemData result, out int craftedCount)
    {
        craftedCount = 0;
        if (!TryBuildRequirements(result, requirementBuffer) || !CanCraft(result))
        {
            return false;
        }

        CaptureTransaction();
        // 合并需求缓冲索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < requirementBuffer.Count; i++)
        {
            // 本轮需要静默扣除的材料及数量。
            // 中文含义：requirement 为“需求”。
CraftingIngredientRequirement requirement = requirementBuffer[i];
            if (!RemoveItemInternal(requirement.itemId, requirement.count))
            {
                RestoreTransaction();
                return false;
            }
        }

        craftedCount = Mathf.Max(1, result.craftingResultCount);
        if (TryAddPlanned(result, craftedCount, ResolveInitialDurability(result), null))
        {
            rollbackStates.Clear();
            // 合成成功后播放界面音效，暂停玩法时仍可听到。
            // 本次表现通知接收者的委托快照，保持原条件调用的单次读取语义。
            // 中文含义：presentationHandler 为“表现处理器”。
System.Action presentationHandler = PresentationRequest;
            if (presentationHandler != null)
            {
                presentationHandler();
            }

            NotifyChanged();
            return true;
        }

        RestoreTransaction();
        craftedCount = 0;
        return false;
    }

    /// <summary>合成显示链查询材料需求与现有数量，使用定义显示名，缺定义时使用标识。</summary>
    /// <param name="result">中文含义：当前物品数据配置。要显示的产物配方。</param>
    public string GetCraftingRequirementText(ItemData result)
    {
        if (!TryBuildRequirements(result, requirementBuffer))
        {
            return string.Empty;
        }

        // 每种材料的显示名与库存进度片段。
        // 中文含义：parts 为“部件集合”。
var parts = new List<string>();
        // 合并需求索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < requirementBuffer.Count; i++)
        {
            // 当前显示的材料需求。
            // 中文含义：requirement 为“需求”。
CraftingIngredientRequirement requirement = requirementBuffer[i];
            // 按材料标识从已注入目录找到的定义，可能为空。
            // 中文含义：ingredient 为“合成材料”。
ItemData ingredient = FindItemData(requirement.itemId);
            // 优先显示定义名称，缺定义时显示原材料标识。
            // 中文含义：ingredientName 为“合成材料名称”。
string ingredientName;
            if (ingredient != null)
            {
                ingredientName = ingredient.displayName;
            }
            else
            {
                ingredientName = requirement.itemId;
            }

            // 当前背包内该材料的合计数量。
            // 中文含义：owned 为“持有的”。
int owned = GetItemCount(requirement.itemId);
            parts.Add(ingredientName + " " + owned + "/" + requirement.count);
        }

        return string.Join("　", parts);
    }

    /// <summary>物品说明显示链查询该材料参与的配方，收集公式后组成提示；不执行合成。</summary>
    /// <param name="ingredient">中文含义：合成材料。待查询材料定义。</param>
    public string GetCraftingHint(ItemData ingredient)
    {
        if (ingredient == null)
        {
            return string.Empty;
        }

        EnsureItemCatalog();
        // 当前材料参与的所有配方公式片段。
        // 中文含义：hints 为“提示集合”。
var hints = new List<string>();
        // 产物目录索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < itemCatalog.Length; i++)
        {
            // 当前目录候选产物。
            // 中文含义：result 为“当前物品数据配置”。
ItemData result = itemCatalog[i];
            if (!IsCraftResult(result))
            {
                continue;
            }

            if (!TryBuildRequirements(result, requirementBuffer))
            {
                continue;
            }

            // 当前产物需求中是否出现待查询材料。
            // 中文含义：participates 为“是否参与的”。
bool participates = false;
            // 当前产物的合并材料需求索引。
            // 中文含义：requirementIndex 为“需求索引”。
for (int requirementIndex = 0; requirementIndex < requirementBuffer.Count; requirementIndex++)
            {
                if (requirementBuffer[requirementIndex].itemId == ingredient.itemId)
                {
                    participates = true;
                    break;
                }
            }

            if (!participates)
            {
                continue;
            }

            hints.Add(GetRecipeFormulaText(result));
        }

        if (hints.Count > 0)
        {
            return "可合成：" + string.Join("；", hints);
        }

        return string.Empty;
    }

    /// <summary>双材料资格及提交链共用查询，接受顺序交换或同种双份材料，返回首个符合目录配方的产物。</summary>
    /// <param name="first">中文含义：第一个背包网格物品。第一件材料实例。</param>
    /// <param name="second">中文含义：第二个背包网格物品。第二件材料实例。</param>
    ItemData FindCraftResult(GridItem first, GridItem second)
    {
        if (first == null || second == null || first.item == null || second.item == null)
        {
            return null;
        }

        EnsureItemCatalog();
        // 第一件材料的定义标识。
        // 中文含义：firstId 为“首个编号”。
string firstId = first.item.itemId;
        // 第二件材料的定义标识。
        // 中文含义：secondId 为“第二编号”。
string secondId = second.item.itemId;
        // 候选产物目录索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < itemCatalog.Length; i++)
        {
            // 本轮候选产物定义。
            // 中文含义：candidate 为“候选”。
ItemData candidate = itemCatalog[i];
            if (!IsCraftResult(candidate))
            {
                continue;
            }

            if (!TryBuildRequirements(candidate, requirementBuffer))
            {
                continue;
            }

            // 配方有两种各一份材料且与输入正向匹配。
            // 中文含义：forward 为“是否前向”。
bool forward = requirementBuffer.Count == 2 && requirementBuffer[0].count == 1 && requirementBuffer[1].count == 1 && requirementBuffer[0].itemId == firstId && requirementBuffer[1].itemId == secondId;
            // 配方有两种各一份材料且与输入反向匹配。
            // 中文含义：reverse 为“是否反向”。
bool reverse = requirementBuffer.Count == 2 && requirementBuffer[0].count == 1 && requirementBuffer[1].count == 1 && requirementBuffer[0].itemId == secondId && requirementBuffer[1].itemId == firstId;
            // 配方是同种材料两份且两个输入都对应该材料。
            // 中文含义：duplicate 为“是否重复”。
bool duplicate = requirementBuffer.Count == 1 && requirementBuffer[0].count == 2 && requirementBuffer[0].itemId == firstId && requirementBuffer[0].itemId == secondId;
            if (forward || reverse || duplicate)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>保留既有目录准备兼容入口；当前目录只由 Initialize 注入，此方法不加载资源或补建服务。</summary>
    void EnsureItemCatalog()
    {
    }

    /// <summary>配方提示和耐久损坏替代链查询已注入目录，返回首个标识匹配定义，未找到返回 null。</summary>
    /// <param name="itemId">中文含义：物品编号。待查询定义标识。</param>
    ItemData FindItemData(string itemId)
    {
        // 按现有目录顺序查找标识的索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < itemCatalog.Length; i++)
        {
            if (itemCatalog[i] != null && itemCatalog[i].itemId == itemId)
            {
                return itemCatalog[i];
            }
        }

        return null;
    }

    /// <summary>配方查询共用资格判断，优先接受非空数量配方，否则要求两个兼容材料标识均非空。</summary>
    /// <param name="item">中文含义：当前物品数据配置。待判断的产物定义。</param>
    static bool IsCraftResult(ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        if (item.craftingIngredients != null && item.craftingIngredients.Length > 0)
        {
            return true;
        }

        return !string.IsNullOrEmpty(item.craftIngredientA) && !string.IsNullOrEmpty(item.craftIngredientB);
    }

    /// <summary>合成与提示链共用解析，先清目标缓冲，优先解析数量配方并合并同标识，否则解析双材料兼容配方。</summary>
    /// <param name="result">中文含义：当前物品数据配置。待解析的产物定义。</param>
    /// <param name="target">中文含义：目标合成合成材料需求集合。接收合并材料需求的调用方缓冲，方法开头清空。</param>
    bool TryBuildRequirements(ItemData result, List<CraftingIngredientRequirement> target)
    {
        target.Clear();
        if (result == null)
        {
            return false;
        }

        if (result.craftingIngredients != null && result.craftingIngredients.Length > 0)
        {
            // 数量配方原始需求索引。
            // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < result.craftingIngredients.Length; i++)
            {
                // 当前原始材料需求，忽略空记录和空标识后按至少一份合并。
                // 中文含义：requirement 为“需求”。
CraftingIngredientRequirement requirement = result.craftingIngredients[i];
                if (requirement == null || string.IsNullOrEmpty(requirement.itemId))
                {
                    continue;
                }

                MergeRequirement(target, requirement.itemId, Mathf.Max(1, requirement.count));
            }

            return target.Count > 0;
        }

        if (string.IsNullOrEmpty(result.craftIngredientA) || string.IsNullOrEmpty(result.craftIngredientB))
        {
            return false;
        }

        MergeRequirement(target, result.craftIngredientA, 1);
        MergeRequirement(target, result.craftIngredientB, 1);
        return true;
    }

    /// <summary>需求解析内部调用，同标识累计数量，首次出现时创建一条需求记录。</summary>
    /// <param name="requirements">中文含义：需求集合。当前需求列表。</param>
    /// <param name="itemId">中文含义：物品编号。材料标识。</param>
    /// <param name="count">中文含义：数量。本次需要合并的数量。</param>
    static void MergeRequirement(List<CraftingIngredientRequirement> requirements, string itemId, int count)
    {
        // 现有合并需求索引，命中标识时直接累计。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < requirements.Count; i++)
        {
            if (requirements[i].itemId != itemId)
            {
                continue;
            }

            requirements[i].count += count;
            return;
        }

        requirements.Add(new CraftingIngredientRequirement { itemId = itemId, count = count });
    }

    /// <summary>材料提示链构造一条材料到产物公式，不包含库存变化或业务提交。</summary>
    /// <param name="result">中文含义：当前物品数据配置。公式对应的产物定义。</param>
    string GetRecipeFormulaText(ItemData result)
    {
        if (!TryBuildRequirements(result, requirementBuffer))
        {
            return string.Empty;
        }

        // 公式中的各材料名称与需求数量片段。
        // 中文含义：parts 为“部件集合”。
var parts = new List<string>();
        // 合并需求索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < requirementBuffer.Count; i++)
        {
            // 当前公式材料需求。
            // 中文含义：requirement 为“需求”。
CraftingIngredientRequirement requirement = requirementBuffer[i];
            // 当前材料定义，查不到时为空。
            // 中文含义：ingredient 为“合成材料”。
ItemData ingredient = FindItemData(requirement.itemId);
            // 优先使用材料显示名，缺定义时退回标识。
            // 中文含义：ingredientName 为“合成材料名称”。
string ingredientName;
            if (ingredient != null)
            {
                ingredientName = ingredient.displayName;
            }
            else
            {
                ingredientName = requirement.itemId;
            }

            parts.Add(ingredientName + "×" + requirement.count);
        }

        return string.Join(" + ", parts) + " → " + result.displayName + "×" + Mathf.Max(1, result.craftingResultCount);
    }

    /// <summary>使用或合成事务内部静默消费一份实例，数量降至零时移除，由外层统一通知。</summary>
    /// <param name="item">中文含义：当前背包网格物品。已经核验的待消费实例。</param>
    internal void ConsumeGridItem(GridItem item)
    {
        item.count--;
        if (item.count <= 0)
        {
            items.Remove(item);
        }
    }

    /// <summary>丢弃宿主按格子定位并移除整份堆叠，未命中无操作，成功后通知。</summary>
    /// <param name="x">中文含义：横向。格子横坐标。</param>
    /// <param name="y">中文含义：纵向。格子纵坐标。</param>
    public void DiscardItemAt(int x, int y)
    {
        // 指定格子命中的实例，未命中时为空。
        // 中文含义：gi 为“背包网格物品”。
var gi = GetItemAt(x, y);
        if (gi == null)
        {
            return;
        }

        items.Remove(gi);
        NotifyChanged();
    }

    /// <summary>宿主按标识移除数量，静默扣除成功后才发布一次变化通知。</summary>
    /// <param name="itemId">中文含义：物品编号。待移除的物品标识。</param>
    /// <param name="count">中文含义：数量。待移除数量，默认一。</param>
    public bool RemoveItem(string itemId, int count = 1)
    {
        if (!RemoveItemInternal(itemId, count))
        {
            return false;
        }

        NotifyChanged();
        return true;
    }

    /// <summary>数量合成及公开移除共用，先验证总量，再倒序逐堆叠扣除，不在中途通知。</summary>
    /// <param name="itemId">中文含义：物品编号。待移除标识。</param>
    /// <param name="count">中文含义：数量。尚待移除数量，循环中逐步减少。</param>
    bool RemoveItemInternal(string itemId, int count)
    {
        if (count <= 0 || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        if (!HasItem(itemId, count))
        {
            return false;
        }

        // 倒序实例索引，移除堆叠时不跳过后续待处理对象。
        // 中文含义：i 为“当前遍历索引”。
for (int i = items.Count - 1; i >= 0; i--)
        {
            // 当前可能匹配标识的堆叠实例。
            // 中文含义：gi 为“背包网格物品”。
var gi = items[i];
            if (gi.item.itemId != itemId)
            {
                continue;
            }

            // 本堆叠实际扣除量，不超过尚需数量和已有数量。
            // 中文含义：toRemove 为“目标移除”。
int toRemove = Mathf.Min(count, gi.count);
            gi.count -= toRemove;
            count -= toRemove;
            if (gi.count <= 0)
            {
                items.RemoveAt(i);
            }

            if (count <= 0)
            {
                break;
            }
        }

        return true;
    }

    /// <summary>库存消费者按标识汇总当前实例数量，不复制或修改集合。</summary>
    /// <param name="itemId">中文含义：物品编号。待统计的定义标识。</param>
    public int GetItemCount(string itemId)
    {
        // 所有匹配实例数量的累计值。
        // 中文含义：total 为“总计”。
int total = 0;
        // 本轮统计的已有实例，只有定义与标识有效匹配才累计。
        // 中文含义：gi 为“背包网格物品”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (var gi in items)
        {
            if (gi.item != null && gi.item.itemId == itemId)
            {
                total += gi.count;
            }
        }

        return total;
    }

    /// <summary>资格消费者以总量查询核对所需数量，复用 GetItemCount。</summary>
    /// <param name="itemId">中文含义：物品编号。待检查标识。</param>
    /// <param name="count">中文含义：数量。最低所需数量，默认一。</param>
    public bool HasItem(string itemId, int count = 1)
    {
        return GetItemCount(itemId) >= count;
    }

    /// <summary>使用及耐久链按现有列表顺序找到首个标识匹配的实例，不以新对象代替。</summary>
    /// <param name="itemId">中文含义：物品编号。待查询定义标识。</param>
    public GridItem GetFirstItem(string itemId)
    {
        // 当前实例列表索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < items.Count; i++)
        {
            // 本轮待检查定义标识的实例。
            // 中文含义：gridItem 为“背包网格物品”。
GridItem gridItem = items[i];
            if (gridItem.item != null && gridItem.item.itemId == itemId)
            {
                return gridItem;
            }
        }

        return null;
    }

    /// <summary>武器或物品宿主检查首个匹配实例是否无需耐久或仍有正数耐久。</summary>
    /// <param name="itemId">中文含义：物品编号。待查询定义标识。</param>
    public bool HasUsableDurability(string itemId)
    {
        // 首个匹配标识的实例，后续只检查它的耐久资格。
        // 中文含义：gridItem 为“背包网格物品”。
GridItem gridItem = GetFirstItem(itemId);
        return gridItem != null && (!gridItem.item.usesDurability || gridItem.currentDurability > 0);
    }

    /// <summary>耐久消费宿主调用；仅处理启用耐久的首个匹配实例，负初始值先恢复配置上限，归零时原位替换定义并通知。</summary>
    /// <param name="itemId">中文含义：物品编号。目标实例的定义标识。</param>
    /// <param name="amount">中文含义：大小。正数耐久消耗量。</param>
    /// <param name="broken">中文含义：是否损坏。输出本次消费后是否归零。</param>
    public bool TryConsumeDurability(string itemId, int amount, out bool broken)
    {
        return TryConsumeDurability(GetFirstItem(itemId), amount, out broken);
    }

    public bool TryConsumeDurability(GridItem gridItem, int amount, out bool broken, bool removeWhenBroken = false)
    {
        broken = false;
        if (amount <= 0)
        {
            return false;
        }

        // 首个匹配标识的耐久实例，沿用原对象进行消费或损坏替换。
        // 中文含义：gridItem 为“背包网格物品”。
        if (gridItem == null || !items.Contains(gridItem) || gridItem.item == null || !gridItem.item.usesDurability)
        {
            return false;
        }

        if (gridItem.currentDurability < 0)
        {
            gridItem.currentDurability = Mathf.Max(1, gridItem.item.maxDurability);
        }

        if (gridItem.currentDurability <= 0)
        {
            return false;
        }

        gridItem.currentDurability = Mathf.Max(0, gridItem.currentDurability - amount);
        broken = gridItem.currentDurability == 0;
        if (broken && removeWhenBroken)
        {
            items.Remove(gridItem);
        }
        else if (broken)
        {
            ReplaceBrokenItem(gridItem);
        }

        NotifyChanged();
        return true;
    }

    /// <summary>耐久归零链调用，按注入目录替换定义并保留原位置；找不到替代品时发送诊断并保留原定义。</summary>
    /// <param name="gridItem">中文含义：背包网格物品。已经损坏的原实例。</param>
    private void ReplaceBrokenItem(GridItem gridItem)
    {
        // 损坏定义指定的替代物品标识。
        // 中文含义：replacementId 为“替代品编号”。
string replacementId = gridItem.item.brokenReplacementItemId;
        if (string.IsNullOrEmpty(replacementId))
        {
            return;
        }

        EnsureItemCatalog();
        // 已注入目录中查到的替代定义，缺失时只诊断。
        // 中文含义：replacement 为“替代品”。
ItemData replacement = FindItemData(replacementId);
        if (replacement == null)
        {
            // 本次诊断通知的委托快照，避免重复读取事件。
            // 中文含义：diagnosticHandler 为“诊断处理器”。
System.Action<string> diagnosticHandler = Diagnostics;
            if (diagnosticHandler != null)
            {
                diagnosticHandler("找不到损坏物品：" + replacementId);
            }

            return;
        }

        gridItem.item = replacement;
        gridItem.count = 1;
        gridItem.currentDurability = 0;
    }

    /// <summary>新增与合成链读取定义耐久；启用耐久时至少一，否则返回既有未设置值 -1。</summary>
    /// <param name="item">中文含义：当前物品数据配置。拟创建实例的物品定义。</param>
    private static int ResolveInitialDurability(ItemData item)
    {
        if (item != null && item.usesDurability)
        {
            return Mathf.Max(1, item.maxDurability);
        }

        return -1;
    }

    /// <summary>所有完整提交链共用，复制一次订阅列表，再同步发布背包当前状态已变化的事实。</summary>
    void NotifyChanged()
    {
        // 本次背包变化接收者快照，数据已提交后同步调用。
        // 中文含义：changedHandler 为“变化处理器”。
System.Action changedHandler = OnInventoryChanged;
        if (changedHandler != null)
        {
            changedHandler();
        }
    }

    // ========================== 存档接口（预留） ==========================
    /// <summary>Serialize 临时构造的存档字段载体；Deserialize 仍为已有预留入口，不在本次重构中补实现。</summary>
    [System.Serializable]
    private class GridItemSaveData
    {
        /// <summary>中文含义：物品编号。序列化时复制的定义标识。</summary>
        public string itemId;
        /// <summary>中文含义：数量。记录或规划的堆叠数量。</summary>
        public int count;
        /// <summary>中文含义：网格横向。序列化时的网格横坐标。</summary>
        public int gridX;
        /// <summary>中文含义：网格纵向。序列化时的网格纵坐标。</summary>
        public int gridY;
        /// <summary>中文含义：旋转。记录或规划的四分之一圈旋转编号。</summary>
        public int rotation;
        /// <summary>中文含义：当前耐久。序列化时的实例耐久。</summary>
        public int currentDurability;
    }

    /// <summary>存档查询兼容入口，复制当前实例字段后交给既有 JsonUtility 调用；本次不改变原序列化格式。</summary>
    public string Serialize()
    {
        // 本次查询生成的存档字段列表，保持原 JsonUtility 格式。
        // 中文含义：data 为“当前背包网格物品保存数据集合”。
var data = new List<GridItemSaveData>();
        // 本轮复制存档字段的原物品实例。
        // 中文含义：gi 为“背包网格物品”。每轮从物品列表取得一个元素，仅代表当前这一项。
foreach (var gi in items)
        {
            data.Add(new GridItemSaveData { itemId = gi.item.itemId, count = gi.count, gridX = gi.gridX, gridY = gi.gridY, rotation = gi.rotation, currentDurability = gi.currentDurability });
        }

        return JsonUtility.ToJson(new { items = data }, true);
    }

    /// <summary>保留已有未实现的存档入口，本次可读性重构不新增恢复行为。</summary>
    /// <param name="json">中文含义：序列化文本。预留的存档文本输入，当前实现不读取。</param>
    public void Deserialize(string json)
    {
    // TODO: 存档系统集成后实现
    }
}
