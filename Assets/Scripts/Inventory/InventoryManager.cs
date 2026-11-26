using UnityEngine;
using System.Collections.Generic;

/// <summary>背包与仓库组件 InventoryManager，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
/// <remarks>背包场景宿主，持有规则模型并向拾取、装备和界面暴露业务入口；数量、布局及耐久由规则写入口提交，显示读取快照或变化事实。</remarks>
[DefaultExecutionOrder(-100)]
public class InventoryManager : Singleton<InventoryManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>背包与仓库模块 InitialInventoryEntry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [System.Serializable]
    public class InitialInventoryEntry
    {
        /// <summary>当前物品数据配置。</summary>
        public ItemData item;
        /// <summary>资源集合路径。</summary>
        public string resourcesPath;
        /// <summary>数量。</summary>
        public int count = 1;
    }

    /// <summary>本局玩家。</summary>
    [Header("本局玩家")]
    [UnityEngine.Serialization.FormerlySerializedAs("本局玩家")]
    [UnityEngine.InspectorName("本局玩家")]
    public LeonController currentSessionPlayer;
    /// <summary>公文包宽度。</summary>
    [Header("公文包尺寸")]
    public int caseWidth = 10;
    /// <summary>公文包高度。</summary>
    public int caseHeight = 6;
    /// <summary>是否启用后自动加入演示所需的枪械和基础弹药。启用后自动加入演示所需的枪械和基础弹药。</summary>
    [Header("初始装备")]
    [Tooltip("启用后自动加入演示所需的枪械和基础弹药。")]
    public bool includeDefaultDemoLoadout = true;
    /// <summary>物品引用为空时会尝试使用资源路径加载。可在检视面板继续追加初始物品；物品引用为空时会尝试使用资源路径加载。</summary>
    [Tooltip("可在检视面板继续追加初始物品；物品引用为空时会尝试使用资源路径加载。")]
    public List<InitialInventoryEntry> initialItems = new List<InitialInventoryEntry>();
    /// <summary>使用物品音效。</summary>
    [Header("音效")]
    public string useItemSfx = "item_use";
    private bool deferringHealAudio;
    private string presentationAudioEvent;
    /// <summary>中文含义：拾取音效。</summary>
    public string pickupSfx = "item_pickup";
    /// <summary>模型。</summary>
    public InventoryRulesModel Model { get; } = new InventoryRulesModel();

    /// <summary>使用服务。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly ItemUseService useService = new ItemUseService();
    /// <summary>运行版本。</summary>
    internal int RunVersion => Model.RunVersion;
    /// <summary>公文包宽度。</summary>
    public int CaseWidth => Model.CaseWidth;
    /// <summary>公文包高度。</summary>
    public int CaseHeight => Model.CaseHeight;

    public event System.Action OnInventoryChanged { add => Model.OnInventoryChanged += value; remove => Model.OnInventoryChanged -= value; }

    /// <summary>GetItems 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetItems()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 DropResourceReader.Read()、InventoryUI.RefreshGridInternal()、PerformanceSamplingEntry.Drive(int, double)、StorageBoxUI.RefreshInventoryGridInternal()；下游（含内部回调体） InventoryRulesModel.GetItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public IReadOnlyList<GridItem> GetItems() => Model.GetItems();
    /// <summary>PlayUseAudioEffect 的业务处理入口；本方法直接依赖 AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryManager.UseItemAt(int, int)；下游（含内部回调体） AudioManager.PlaySfx(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void PlayUseAudioEffect()
    {
        if (deferringHealAudio)
        {
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue1 = AudioManager.Instance;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            string sound = useItemSfx;
            if (!string.IsNullOrEmpty(presentationAudioEvent))
            {
                sound = presentationAudioEvent;
            }

            receiverValue1.PlaySfx(sound);
        }
    }

    /// <summary>RecordDiagnostics 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogWarning(object, Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="content">内容。</param>
    void RecordDiagnostics(string content) => Debug.LogWarning("[背包] " + content, this);
    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Singleton&lt;InventoryManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        Model.PresentationRequest -= PlayUseAudioEffect;
        Model.Diagnostics -= RecordDiagnostics;
        base.OnDestroy();
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryManager.ResetForNewRun()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        Model.PresentationRequest += PlayUseAudioEffect;
        Model.Diagnostics += RecordDiagnostics;
        ResetForNewRun();
    }

    /// <summary>ResetForNewRun 的业务处理入口；本方法直接依赖 InventoryManager.AddItem(ItemData, int)、InventoryManager.EnsureInitialItem(string, string, int)、InventoryManager.HasItem(string, int)、InventoryRulesModel.Initialize(int, int, ItemData[])、InventoryRulesModel.NotificationViewRefresh()、Resources.Load&lt;ItemData&gt;(string)、Resources.LoadAll&lt;ItemData&gt;(string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryManager.Start()；下游（含内部回调体） InventoryManager.AddItem(ItemData, int)、InventoryManager.EnsureInitialItem(string, string, int)、InventoryManager.HasItem(string, int)、InventoryRulesModel.Initialize(int, int, ItemData[])、InventoryRulesModel.NotificationViewRefresh()、Resources.Load&lt;ItemData&gt;(string)、Resources.LoadAll&lt;ItemData&gt;(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetForNewRun()
    {
        Model.Initialize(caseWidth, caseHeight, Resources.LoadAll<ItemData>("Items"));
        if (includeDefaultDemoLoadout)
        {
            EnsureInitialItem("knife", "Items/Knife", 1);
            EnsureInitialItem("pistol", "Items/Pistol", 1);
            EnsureInitialItem("pistol_ammo", "Items/PistolAmmo", 24);
            EnsureInitialItem("first_aid_spray", "Items/FirstAidSpray", 1);
        }
        else
        {
            // 当前遍历索引。
for (int i = 0; i < initialItems.Count; i++)
            {
                // 当前初始背包入口。
InitialInventoryEntry entry = initialItems[i];
                if (entry == null || entry.count <= 0)
                {
                    continue;
                }

                // 当前物品数据配置。
ItemData item = entry.item;
                if (item == null && !string.IsNullOrWhiteSpace(entry.resourcesPath))
                {
                    item = Resources.Load<ItemData>(entry.resourcesPath);
                }

                if (item != null && !HasItem(item.itemId))
                {
                    AddItem(item, entry.count);
                }
            }
        }

        Model.NotificationViewRefresh();
    }

    /// <summary>EnsureInitialItem 的业务处理入口；本方法直接依赖 Debug.LogWarning(object, Object)、InventoryManager.AddItem(ItemData, int)、InventoryManager.HasItem(string, int)、Resources.Load&lt;ItemData&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryManager.ResetForNewRun()；下游（含内部回调体） Debug.LogWarning(object, Object)、InventoryManager.AddItem(ItemData, int)、InventoryManager.HasItem(string, int)、Resources.Load&lt;ItemData&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    /// <param name="resourcesPath">资源集合路径。</param>
    /// <param name="count">数量。</param>
    void EnsureInitialItem(string itemId, string resourcesPath, int count)
    {
        if (HasItem(itemId))
        {
            return;
        }

        // 当前物品数据配置。
ItemData item = Resources.Load<ItemData>(resourcesPath);
        if (item == null)
        {
            Debug.LogWarning("[背包] 无法加载初始物品：" + resourcesPath, this);
            return;
        }

        if (!AddItem(item, count))
        {
            Debug.LogWarning("[背包] 公文包空间不足，无法加入：" + item.displayName, this);
        }
    }

    /// <summary>GetItemAt 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetItemAt(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)、AttacheCaseProjectionInput.Find(Vector2)、InventoryUI.OnCellClicked(int, int)、InventoryUI.OnItemPointerUp(GridItem, Vector2)、InventoryUI.RefreshGridInternal()；下游（含内部回调体） InventoryRulesModel.GetItemAt(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">横向。</param>
    /// <param name="y">纵向。</param>
    public GridItem GetItemAt(int x, int y) => Model.GetItemAt(x, y);
    /// <summary>AddItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.AddItem(ItemData, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryManager.EnsureInitialItem(string, string, int)、InventoryManager.ResetForNewRun()、ItemPickup.Pickup()、PerformanceSamplingEntry.Tick()；下游（含内部回调体） InventoryRulesModel.AddItem(ItemData, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前物品数据配置。</param>
    /// <param name="count">数量。</param>
    public bool AddItem(ItemData item, int count = 1) => Model.AddItem(item, count);
    /// <summary>HasAcquiredThisRun 的业务处理入口；本方法直接依赖 InventoryRulesModel.HasAcquiredThisRun(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemPickup.Pickup()；下游（含内部回调体） InventoryRulesModel.HasAcquiredThisRun(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    public bool HasAcquiredThisRun(string itemId) => Model.HasAcquiredThisRun(itemId);
    /// <summary>MoveItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.MoveItem(GridItem, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） InventoryRulesModel.MoveItem(GridItem, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    /// <param name="newX">新横向。</param>
    /// <param name="newY">新纵向。</param>
    public bool MoveItem(GridItem item, int newX, int newY) => Model.MoveItem(item, newX, newY);
    /// <summary>MoveItemNear 的业务处理入口；本方法直接依赖 InventoryRulesModel.MoveItemNear(GridItem, int, int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemPointerUp(GridItem, Vector2)；下游（含内部回调体） InventoryRulesModel.MoveItemNear(GridItem, int, int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    /// <param name="preferredX">优先横向。</param>
    /// <param name="preferredY">优先纵向。</param>
    /// <param name="snapRadius">吸附半径。</param>
    public bool MoveItemNear(GridItem item, int preferredX, int preferredY, int snapRadius = 1) => Model.MoveItemNear(item, preferredX, preferredY, snapRadius);
    /// <summary>RotateItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.RotateItem(GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.RotateSelectedItem()、PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） InventoryRulesModel.RotateItem(GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前背包网格物品。</param>
    public bool RotateItem(GridItem item) => Model.RotateItem(item);
    /// <summary>Sort 的业务处理入口；本方法直接依赖 InventoryRulesModel.Sort()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonInputBridge.OnSort(InputAction.CallbackContext)、PerformanceSamplingEntry.Drive(int, double)；下游（含内部回调体） InventoryRulesModel.Sort()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Sort()
    {
        string previous = presentationAudioEvent;
        presentationAudioEvent = AudioEventNames.UICaseManipulate;
        try
        {
            Model.Sort();
        }
        finally
        {
            presentationAudioEvent = previous;
        }
    }
    /// <summary>CanUseItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.CanUseItem(ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ShowContextMenu(GridItem, Vector2)、InventoryUI.ShowDescription(GridItem)；下游（含内部回调体） InventoryRulesModel.CanUseItem(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前物品数据配置。</param>
    public bool CanUseItem(ItemData item) => Model.CanUseItem(item);
    /// <summary>CanCombine 的业务处理入口；本方法直接依赖 InventoryRulesModel.CanCombine(GridItem, GridItem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AttacheCasePresentationController.PreviewLandPoint(GridItem, Vector2, Vector2)；下游（含内部回调体） InventoryRulesModel.CanCombine(GridItem, GridItem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">第一个背包网格物品。</param>
    /// <param name="second">第二个背包网格物品。</param>
    public bool CanCombine(GridItem first, GridItem second) => Model.CanCombine(first, second);
    /// <summary>TryCombine 的业务处理入口；本方法直接依赖 InventoryRulesModel.TryCombine(GridItem, GridItem, out ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.OnItemPointerUp(GridItem, Vector2)；下游（含内部回调体） InventoryRulesModel.TryCombine(GridItem, GridItem, out ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="first">第一个背包网格物品。</param>
    /// <param name="second">第二个背包网格物品。</param>
    /// <param name="result">当前物品数据配置。输出参数，由本入口写入并交给调用方。</param>
    public bool TryCombine(GridItem first, GridItem second, out ItemData result)
    {
        string previous = presentationAudioEvent;
        presentationAudioEvent = AudioEventNames.UICaseCraft;
        try
        {
            return Model.TryCombine(first, second, out result);
        }
        finally
        {
            presentationAudioEvent = previous;
        }
    }
    /// <summary>GetCraftingRecipes 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetCraftingRecipes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.RebuildRecipeList(InventoryManager)、InventoryCraftingPanel.Refresh()；下游（含内部回调体） InventoryRulesModel.GetCraftingRecipes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public List<ItemData> GetCraftingRecipes() => Model.GetCraftingRecipes();
    /// <summary>CanCraft 的业务处理入口；本方法直接依赖 InventoryRulesModel.CanCraft(ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)、InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)；下游（含内部回调体） InventoryRulesModel.CanCraft(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前物品数据配置。</param>
    public bool CanCraft(ItemData result) => Model.CanCraft(result);
    /// <summary>TryCraft 的业务处理入口；本方法直接依赖 InventoryRulesModel.TryCraft(ItemData, out int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CraftSelectedRecipe()；下游（含内部回调体） InventoryRulesModel.TryCraft(ItemData, out int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前物品数据配置。</param>
    /// <param name="craftedCount">合成后的数量。输出参数，由本入口写入并交给调用方。</param>
    public bool TryCraft(ItemData result, out int craftedCount)
    {
        string previous = presentationAudioEvent;
        presentationAudioEvent = AudioEventNames.UICaseCraft;
        try
        {
            return Model.TryCraft(result, out craftedCount);
        }
        finally
        {
            presentationAudioEvent = previous;
        }
    }
    /// <summary>GetCraftingRequirementText 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetCraftingRequirementText(ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)、InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)；下游（含内部回调体） InventoryRulesModel.GetCraftingRequirementText(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="result">当前物品数据配置。</param>
    public string GetCraftingRequirementText(ItemData result) => Model.GetCraftingRequirementText(result);
    /// <summary>GetCraftingHint 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetCraftingHint(ItemData)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.ShowDescription(GridItem)；下游（含内部回调体） InventoryRulesModel.GetCraftingHint(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="ingredient">合成材料。</param>
    public string GetCraftingHint(ItemData ingredient) => Model.GetCraftingHint(ingredient);
    /// <summary>DiscardItemAt 的业务处理入口；本方法直接依赖 InventoryRulesModel.DiscardItemAt(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.HandleContextDiscardButtonClicked()；下游（含内部回调体） InventoryRulesModel.DiscardItemAt(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">横向。</param>
    /// <param name="y">纵向。</param>
    public void DiscardItemAt(int x, int y) => Model.DiscardItemAt(x, y);
    /// <summary>RemoveItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.RemoveItem(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.ConsumeReserveAmmo(WeaponType, int)；下游（含内部回调体） InventoryRulesModel.RemoveItem(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    /// <param name="count">数量。</param>
    public bool RemoveItem(string itemId, int count = 1) => Model.RemoveItem(itemId, count);
    /// <summary>GetItemCount 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetItemCount(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.GetReserveAmmo(WeaponType)、WeaponWheelUI.ResolveCount(WeaponWheelUI.WeaponSlot)；下游（含内部回调体） InventoryRulesModel.GetItemCount(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    public int GetItemCount(string itemId) => Model.GetItemCount(itemId);
    /// <summary>HasItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.HasItem(string, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryManager.EnsureInitialItem(string, string, int)、InventoryManager.ResetForNewRun()、LeonWeaponHandler.HasWeaponInInventory(WeaponType)、LeonWeaponVisualController.HasOwnedLongGun(WeaponType)；下游（含内部回调体） InventoryRulesModel.HasItem(string, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    /// <param name="count">数量。</param>
    public bool HasItem(string itemId, int count = 1) => Model.HasItem(itemId, count);
    /// <summary>GetFirstItem 的业务处理入口；本方法直接依赖 InventoryRulesModel.GetFirstItem(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatHUD.UpdateKnifeDurability()；下游（含内部回调体） InventoryRulesModel.GetFirstItem(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    public GridItem GetFirstItem(string itemId) => Model.GetFirstItem(itemId);
    /// <summary>HasUsableDurability 的业务处理入口；本方法直接依赖 InventoryRulesModel.HasUsableDurability(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.ValidKnife.get、LeonWeaponHandler.HasUsableKnife()；下游（含内部回调体） InventoryRulesModel.HasUsableDurability(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    public bool HasUsableDurability(string itemId) => Model.HasUsableDurability(itemId);
    /// <summary>TryConsumeDurability 的业务处理入口；本方法直接依赖 InventoryRulesModel.TryConsumeDurability(string, int, out bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.DeductDurability(int)、LeonCombat.ConsumeKnifeDurability(int)、LeonQTECoordinator.ConsumeAssassinationKnifeDurability()；下游（含内部回调体） InventoryRulesModel.TryConsumeDurability(string, int, out bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="itemId">物品编号。</param>
    /// <param name="amount">大小。</param>
    /// <param name="broken">是否损坏。输出参数，由本入口写入并交给调用方。</param>
    public bool TryConsumeDurability(string itemId, int amount, out bool broken) => Model.TryConsumeDurability(itemId, amount, out broken);

    internal const string KitchenKnifeId = "kitchen_knife";
    internal const string BootKnifeId = "boot_knife";

    public GridItem GetPreferredKnife()
    {
        return SelectKnifeWithDurability(0);
    }

    // 补刀先筛除付不起本次消耗的刀，然后沿用菜刀、靴刀、战斗刀的现有选刀顺序。
    internal GridItem SelectKnifeWithDurability(int minimum)
    {
        GridItem preferred = null;
        int priority = int.MaxValue;
        int durability = int.MaxValue;
        foreach (GridItem item in Model.GetItems())
        {
            if (item.item == null || !item.item.usesDurability || item.currentDurability == 0)
            {
                continue;
            }

            int rank;
            switch (item.item.itemId)
            {
                case KitchenKnifeId:
                    rank = 0;
                    break;
                case BootKnifeId:
                    rank = 1;
                    break;
                case "knife":
                    rank = 2;
                    break;
                default:
                    continue;
            }

            int remaining = item.currentDurability;
            if (remaining < 0)
            {
                remaining = item.item.maxDurability;
            }
            if (remaining < minimum)
            {
                continue;
            }
            if (rank < priority || rank == priority && remaining < durability)
            {
                preferred = item;
                priority = rank;
                durability = remaining;
            }
        }
        return preferred;
    }

    internal bool TryConsumeKnife(GridItem knife, int amount, out bool broken)
    {
        broken = false;
        if (knife == null || knife.item == null)
        {
            return false;
        }

        string id = knife.item.itemId;
        bool disposable = id == KitchenKnifeId || id == BootKnifeId;
        if (!disposable && id != "knife")
        {
            return false;
        }

        bool consumed = Model.TryConsumeDurability(knife, amount, out broken, disposable);
        if (consumed && broken && GetPreferredKnife() == null)
        {
            VillageLeonDialogue.Report(currentSessionPlayer, VillageSpeechCue.KnifeBroken);
        }
        return consumed;
    }

    internal static float GetKnifeDamageScale(GridItem knife)
    {
        if (knife != null && knife.item != null)
        {
            if (knife.item.itemId == KitchenKnifeId)
            {
                return .65f;
            }
            if (knife.item.itemId == BootKnifeId)
            {
                return .80f;
            }
        }
        return 1f;
    }
    /// <summary>Serialize 的业务处理入口；本方法直接依赖 InventoryRulesModel.Serialize()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryRulesModel.Serialize()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public string Serialize() => Model.Serialize();
    /// <summary>Deserialize 的业务处理入口；本方法直接依赖 InventoryRulesModel.Deserialize(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryRulesModel.Deserialize(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="json">序列化文本。</param>
    public void Deserialize(string json) => Model.Deserialize(json);
    /// <summary>UseItemAt 的业务处理入口；本方法直接依赖 InventoryManager.PlayUseAudioEffect()、InventoryRulesModel.GetItemAt(int, int)、InventoryRulesModel.NotificationViewRefresh()、ItemUseService.NewUseId()、ItemUseService.RequestUse(RequestContext, InventoryRulesModel, GridItem, IItemUseTarget)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.UseItem(GridItem)；下游（含内部回调体） InventoryManager.PlayUseAudioEffect()、InventoryRulesModel.GetItemAt(int, int)、InventoryRulesModel.NotificationViewRefresh()、ItemUseService.NewUseId()、ItemUseService.RequestUse(RequestContext, InventoryRulesModel, GridItem, IItemUseTarget)、Object.GetInstanceID()、ObjectIdentity.ObjectIdentity(int, long) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="x">横向。</param>
    /// <param name="y">纵向。</param>
    public void UseItemAt(int x, int y)
    {
        // 物品值。
var itemValue = Model.GetItemAt(x, y);
        if (itemValue == null || (itemValue.item.itemType != ItemType.Healing && itemValue.item.itemType != ItemType.Ammo))
        {
            return;
        }

        // 身份标识。
var identity = new GameArchitecture.ObjectIdentity(GetInstanceID(), RunVersion);
        // result（当前物品使用结果）：保存 ItemUseService.RequestUse(RequestContext, InventoryRulesModel, GridItem, IItemUseTarget) 的返回结果，供当前步骤使用。使用位置：InventoryManager.UseItemAt(int, int)。
        var request = new GameArchitecture.RequestContext("背包使用", identity, identity, useService.NewUseId());
        AudioManager audio = AudioManager.Instance;
        LeonController player = currentSessionPlayer;
        bool useHealSound = itemValue.item.itemType == ItemType.Healing && player != null && player.Health != null
            && audio != null && audio.library != null && audio.library.HasEvent(AudioEventNames.ItemHeal);
        float beforeHealth = 0f;
        float beforeMaximum = 0f;
        if (useHealSound)
        {
            beforeHealth = player.Health.CurrentHealth;
            beforeMaximum = player.Health.MaxHealth;
        }

        bool previousDeferral = deferringHealAudio;
        bool committed;
        try
        {
            deferringHealAudio = previousDeferral || useHealSound;
            committed = useService.RequestUse(request, Model, itemValue, player).Committed;
        }
        finally
        {
            deferringHealAudio = previousDeferral;
        }

        if (committed && useHealSound)
        {
            if (player.Health.CurrentHealth > beforeHealth || player.Health.MaxHealth > beforeMaximum)
            {
                audio.PlaySfx(AudioEventNames.ItemHeal);
            }
        }
        else if (!committed)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseError);
            Model.NotificationViewRefresh();
        }
        if (committed && itemValue.item.itemType == ItemType.Healing)
        {
            foreach (GridItem remaining in Model.GetItems())
            {
                if (remaining.item != null && remaining.item.itemType == ItemType.Healing)
                {
                    return;
                }
            }

            var enemies = EnemyAIManager.Instance;
            if (enemies != null && enemies.HasEngagedEnemy())
            {
                VillageLeonDialogue.Report(player, VillageSpeechCue.LastHealCombat);
            }
            else
            {
                VillageLeonDialogue.Report(player, VillageSpeechCue.LastHealQuiet);
            }
        }
    }
}
