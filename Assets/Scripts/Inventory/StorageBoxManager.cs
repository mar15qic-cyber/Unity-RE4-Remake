using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 寄存箱管理器。存储玩家寄存的物品。
/// 武器视觉显隐唯一由 LeonWeaponVisualController 负责。
/// 当前武器必须先通过正式武器事务切换到仍在背包中的另一把枪，事务完成后才提交寄存。
/// 转移操作保证原子性：成功时来源与目标数量一致，失败时双方不变。
/// </summary>
/// <remarks>仓库场景宿主，连接已绑定玩家、背包及仓库规则；跨容器存取通过既有原子交接提交，界面不直接修改物品归属。</remarks>
[DefaultExecutionOrder(-100)]
public class StorageBoxManager : Singleton<StorageBoxManager>, IManualSingleton
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：本局背包。使用位置：SceneStartupEntry.ConfigurationError()、StorageBoxManager.TransferToInventory(GridItem)、StorageBoxManager.TransferToStorage(GridItem)。</summary>
    [Header("本局引用")]
    [UnityEngine.Serialization.FormerlySerializedAs("本局背包")]
    [UnityEngine.InspectorName("本局背包")]
    public InventoryManager currentSessionInventory;
    /// <summary>中文含义：本局玩家。使用位置：SceneStartupEntry.ConfigurationError()、StorageBoxManager.TransferToStorage(GridItem)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("本局玩家")]
    [UnityEngine.InspectorName("本局玩家")]
    public LeonController currentSessionPlayer;
    /// <summary>中文含义：是否关闭时保持旧版限制。启用后允许把当前手持武器存入箱子；关闭时保持旧版限制。使用位置：StorageBoxManager.TransferToStorage(GridItem)。</summary>
    [Header("存取规则")]
    [Tooltip("启用后允许把当前手持武器存入箱子；关闭时保持旧版限制。")]
    public bool allowStoringEquippedWeapon = true;
    /// <summary>中文含义：是否新游戏的寄存箱中预置一把满弹匣、无备用弹药的奖励步枪。新游戏的寄存箱中预置一把满弹匣、无备用弹药的奖励步枪。使用位置：StorageBoxManager.ResetForNewRun()。</summary>
    [Header("初始奖励")]
    [Tooltip("新游戏的寄存箱中预置一把满弹匣、无备用弹药的奖励步枪。")]
    public bool includeRewardRifle = true;
    /// <summary>中文含义：模型。使用位置：作为 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的参数、StorageBoxManager.CommitTransferToStorage(GridItem, InventoryManager)、StorageBoxManager.GetStorageItems() 等共 9 处。</summary>
    public StorageRulesModel Model { get; } = new StorageRulesModel();

    /// <summary>中文含义：使用服务。只读字段，初始化后不由普通写入口替换。使用位置：StorageBoxManager.ClearPendingTransfer()、StorageBoxManager.IsTransferPending.get、StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int) 等共 4 处。</summary>
    private readonly ItemUseService useService = new ItemUseService();
    public event System.Action OnStorageChanged { add => Model.OnStorageChanged += value; remove => Model.OnStorageChanged -= value; }

    /// <summary>中文含义：转移已失败时的通知回调。事件通信成员 OnTransferFailed，订阅方接收拥有者已提交的通知；注册与退订见实际调用位置。使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)、StorageBoxUI.BindManagerEvents()、StorageBoxUI.UnbindManagerEvents()。</summary>
    public event System.Action<string> OnTransferFailed;
    /// <summary>中文含义：是否打开。使用位置：InventoryUI.ShowContextMenu(GridItem, Vector2)、StorageBoxManager.Close()、StorageBoxManager.Open() 等共 4 处。</summary>
    public bool IsOpen { get; private set; }
    /// <summary>中文含义：是否转移待处理。使用位置：StorageBoxManager.TransferToStorage(GridItem)。</summary>
    public bool IsTransferPending => useService.WaitingStored;

    /// <summary>GetStorageItems 的业务处理入口；本方法直接依赖 StorageRulesModel.GetStorageItems()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)、StorageBoxUI.RefreshStorageList()；下游（含内部回调体） StorageRulesModel.GetStorageItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public IReadOnlyList<GridItem> GetStorageItems() => Model.GetStorageItems();
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StorageBoxManager.ResetForNewRun()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        ResetForNewRun();
    }

    /// <summary>清空上一局寄存内容，并恢复新局的初始奖励。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.Start()；下游（含内部回调体） Debug.LogWarning(object, Object)、Resources.Load&lt;ItemData&gt;(string)、StorageBoxManager.ClearPendingTransfer()、StorageRulesModel.Initialize(ItemData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResetForNewRun()
    {
        ClearPendingTransfer();
        IsOpen = false;
        // reward（奖励）：使用位置：作为 StorageRulesModel.Initialize(ItemData) 的参数、StorageBoxManager.ResetForNewRun()。
ItemData reward;
        if (includeRewardRifle)
        {
            reward = Resources.Load<ItemData>("Items/Rifle");
        }
        else
        {
            reward = null;
        }

        if (includeRewardRifle && reward == null)
        {
            Debug.LogWarning("[寄存箱] 无法加载奖励步枪物品数据。", this);
        }

        Model.Initialize(reward);
    }

    /// <summary>ValidateInventorySource 的业务处理入口；本方法直接依赖 StorageRulesModel.ValidateInventorySource(GridItem, InventoryRulesModel)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.TransferToStorage(GridItem)；下游（含内部回调体） StorageRulesModel.ValidateInventorySource(GridItem, InventoryRulesModel)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 StorageRulesModel.ValidateInventorySource(GridItem, InventoryRulesModel) 的参数。</param>
    /// <param name="invMgr">中文含义：背包管理器。使用位置：作为 StorageRulesModel.ValidateInventorySource(GridItem, InventoryRulesModel) 的参数、StorageBoxManager.ValidateInventorySource(GridItem, InventoryManager)。</param>
    bool ValidateInventorySource(GridItem item, InventoryManager invMgr) => invMgr != null && Model.ValidateInventorySource(item, invMgr.Model);
    /// <summary>CommitTransferToStorage 的业务处理入口；本方法直接依赖 StorageRulesModel.CommitTransferToStorage(GridItem, InventoryRulesModel)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.TransferToStorage(GridItem)；下游（含内部回调体） StorageRulesModel.CommitTransferToStorage(GridItem, InventoryRulesModel)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源背包网格物品。使用位置：作为 StorageRulesModel.CommitTransferToStorage(GridItem, InventoryRulesModel) 的参数。</param>
    /// <param name="invMgr">中文含义：背包管理器。使用位置：作为 StorageRulesModel.CommitTransferToStorage(GridItem, InventoryRulesModel) 的参数、StorageBoxManager.CommitTransferToStorage(GridItem, InventoryManager)。</param>
    bool CommitTransferToStorage(GridItem source, InventoryManager invMgr)
    {
        if (invMgr == null || !Model.CommitTransferToStorage(source, invMgr.Model))
        {
            return false;
        }

        AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        return true;
    }
    /// <summary>TransferToInventory 的业务处理入口；本方法直接依赖 StorageRulesModel.TransferToInventory(GridItem, InventoryRulesModel)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PerformanceSamplingEntry.Drive(int, double)、StorageBoxUI.ConfigureStorageEntry(GameObject, GridItem, bool)；下游（含内部回调体） StorageRulesModel.TransferToInventory(GridItem, InventoryRulesModel)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 StorageRulesModel.TransferToInventory(GridItem, InventoryRulesModel) 的参数。</param>
    public bool TransferToInventory(GridItem item)
    {
        if (this == null || !isActiveAndEnabled || !gameObject.scene.isLoaded)
        {
            return false;
        }

        // inventory（背包）：使用位置：作为 StorageRulesModel.TransferToInventory(GridItem, InventoryRulesModel) 的参数、StorageBoxManager.TransferToInventory(GridItem)。
InventoryManager inventory = currentSessionInventory;
        if (inventory == null || !Model.TransferToInventory(item, inventory.Model))
        {
            return false;
        }

        AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        return true;
    }

    /// <summary>
    /// 从背包存入箱子（原子操作）。
    /// 先验证全部条件，确认后再执行修改。
    /// </summary>
    /// <remarks>静态调用链：上游 InventoryUI.HandleContextStoreButtonClicked()、PerformanceSamplingEntry.Drive(int, double)、StorageBoxUI.OnInventoryItemClicked(GridItem, PointerEventData.InputButton, Vector2)、StorageBoxUI.OnInventoryItemPointerUp(GridItem, Vector2)；下游（含内部回调体） Component.GetComponent&lt;LeonWeaponHandler&gt;()、ItemUseService.BeginStored(InventoryRulesModel, GridItem, int)、LeonWeaponHandler.TrySwitchAwayForStorage(WeaponType, Action&lt;bool&gt;)、StorageBoxManager.ClearPendingTransfer()、StorageBoxManager.CommitTransferToStorage(GridItem, InventoryManager)、StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)、StorageBoxManager.ValidateInventorySource(GridItem, InventoryManager)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前背包网格物品。使用位置：作为 ItemUseService.BeginStored(InventoryRulesModel, GridItem, int) 的参数、作为 LeonWeaponHandler.TrySwitchAwayForStorage(WeaponType, Action&lt;bool&gt;) 的参数、作为 StorageBoxManager.CommitTransferToStorage(GridItem, InventoryManager) 的参数 等共 5 处。</param>
    public bool TransferToStorage(GridItem item)
    {
        if (this == null || !isActiveAndEnabled || !gameObject.scene.isLoaded)
        {
            return false;
        }

        if (item == null || item.item == null)
        {
            return false;
        }

        if (IsTransferPending)
        {
            return false;
        }

        // invMgr（背包管理器）：使用位置：作为 ItemUseService.BeginStored(InventoryRulesModel, GridItem, int) 的参数、作为 StorageBoxManager.CommitTransferToStorage(GridItem, InventoryManager) 的参数、作为 StorageBoxManager.ValidateInventorySource(GridItem, InventoryManager) 的参数。
InventoryManager invMgr = currentSessionInventory;
        if (!ValidateInventorySource(item, invMgr))
        {
            return false;
        }

        if (item.item.itemType == ItemType.Weapon)
        {
            // leon（里昂）：使用位置：StorageBoxManager.TransferToStorage(GridItem)。
LeonController leon = currentSessionPlayer;
            // isEquipped（是否已装备）：使用位置：StorageBoxManager.TransferToStorage(GridItem)。
bool isEquipped = leon != null && leon.currentWeapon == item.item.weaponType;
            if (isEquipped)
            {
                if (!allowStoringEquippedWeapon)
                {
                    return false;
                }

                // weaponHandler（管理当前装备与武器切换的组件）：保存 Component.GetComponent<LeonWeaponHandler>() 的返回结果，供当前步骤使用。使用位置：StorageBoxManager.TransferToStorage(GridItem)。
LeonWeaponHandler weaponHandler = leon.GetComponent<LeonWeaponHandler>();
                if (weaponHandler == null)
                {
                    return false;
                }

                // requestVersion（请求版本）：保存 ItemUseService.BeginStored(InventoryRulesModel, GridItem, int) 的返回结果，供当前步骤使用。使用位置：作为 StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int) 的参数、StorageBoxManager.TransferToStorage(GridItem)。
int requestVersion = useService.BeginStored(invMgr.Model, item, gameObject.scene.handle);
                if (requestVersion == 0)
                {
                    return false;
                }

                if (!weaponHandler.TrySwitchAwayForStorage(item.item.weaponType, /* 中文含义：是否成功。 */ success => OnStorageWeaponSwitchCompleted(success, requestVersion)))
                {
                    if (requestVersion == useService.StoredId)
                    {
                        ClearPendingTransfer();
                    }

                    return false;
                }

                return true;
            }
        }

        return CommitTransferToStorage(item, invMgr);
    }

    /// <summary>OnStorageWeaponSwitchCompleted 的业务处理入口；本方法直接依赖 Action&lt;string&gt;.Invoke(string)、ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.TransferToStorage(GridItem)；下游（含内部回调体） Action&lt;string&gt;.Invoke(string)、ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="success">中文含义：是否成功。使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。</param>
    /// <param name="requestVersion">中文含义：请求版本。使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。</param>
    void OnStorageWeaponSwitchCompleted(bool success, int requestVersion)
    {
        // valid（是否有效）：使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
bool valid = this != null && isActiveAndEnabled && gameObject.scene.isLoaded;
        // callReceiver6（调用前缓存的使用服务）：从使用服务读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
global::ItemUseService callReceiver6 = useService;
        // argumentValue3（按原顺序缓存的实参请求版本）：从请求版本读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的参数。
int argumentValue3 = requestVersion;
        // argumentValue4（按原顺序缓存的实参是否成功）：从成功读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的参数。
bool argumentValue4 = success;
        // argumentValue5（按原顺序缓存的实参是否有效）：从有效读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的参数。
bool argumentValue5 = valid;
        // selectedValue2（分支选出的游戏对象的场景的句柄）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的参数、StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
int selectedValue2;
        if (valid)
        {
            selectedValue2 = gameObject.scene.handle;
        }
        else
        {
            selectedValue2 = -1;
        }

        // result（结果）：保存 ItemUseService.CompleteStored(int, bool, bool, int, StorageRulesModel) 的返回结果，供当前步骤使用。使用位置：StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
int result = callReceiver6.CompleteStored(argumentValue3, argumentValue4, argumentValue5, selectedValue2, Model);
        if (result == 1)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseConfirm);
        }
        else if (result == -1)
        {
            // receiverValue7（判空前缓存的转移已失败时的通知回调）：从转移已失败时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
global::System.Action<string> receiverValue7 = OnTransferFailed;
            if (!global::System.Object.ReferenceEquals(receiverValue7, null))
            {
                receiverValue7.Invoke("武器切换被中断，未执行寄存。");
            }
        }
        else if (result == -2)
        {
            // receiverValue8（判空前缓存的转移已失败时的通知回调）：从转移已失败时的通知回调读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、StorageBoxManager.OnStorageWeaponSwitchCompleted(bool, int)。
global::System.Action<string> receiverValue8 = OnTransferFailed;
            if (!global::System.Object.ReferenceEquals(receiverValue8, null))
            {
                receiverValue8.Invoke("背包内容已变化，武器未能存入寄存箱。");
            }
        }
    }

    /// <summary>ClearPendingTransfer 的业务处理入口；本方法直接依赖 ItemUseService.CancelStored()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.OnDisable()、StorageBoxManager.ResetForNewRun()、StorageBoxManager.TransferToStorage(GridItem)；下游（含内部回调体） ItemUseService.CancelStored()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ClearPendingTransfer() => useService.CancelStored();
    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） StorageBoxManager.ClearPendingTransfer()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable() => ClearPendingTransfer();
    /// <summary>使用背包实际添加的完整放置规划，失败不改变任何物品实例。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Show(StorageBoxController)；下游（含内部回调体） StorageBoxManager.NotifyChanged()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Open()
    {
        IsOpen = true;
        NotifyChanged();
    }

    /// <summary>Close 的业务处理入口；本方法直接依赖 StorageBoxManager.NotifyChanged()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxUI.Close()；下游（含内部回调体） StorageBoxManager.NotifyChanged()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Close()
    {
        IsOpen = false;
        NotifyChanged();
    }

    /// <summary>NotifyChanged 的业务处理入口；本方法直接依赖 StorageRulesModel.NotifyChanged()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 StorageBoxManager.Close()、StorageBoxManager.Open()；下游（含内部回调体） StorageRulesModel.NotifyChanged()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void NotifyChanged()
    {
        Model.NotifyChanged();
    }
}
