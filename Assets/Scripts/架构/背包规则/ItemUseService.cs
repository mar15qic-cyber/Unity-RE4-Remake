using UnityEngine;
using GameArchitecture;

/// <summary>
/// 背包、仓库和角色效果之间的同步事务边界。背包宿主传入模型与 IItemUseTarget，服务直接调用已有写入口，
/// 成功使用后再让背包发布反馈；寄存动画通过 StoredId 和来源快照校验，旧回调不能转移新一轮物品。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.InventoryRules", "物品使用服务")]
public sealed class ItemUseService
{
    /// <summary>中文含义：已存储物品。寄存动画开始时锁定的物品实例，取消或完成时清空。</summary>
    private GridItem storedItem;
    /// <summary>中文含义：已存储背包。寄存来源背包模型，回调时核验是否仍为同一运行版本。</summary>
    private InventoryRulesModel storedInventory;
    /// <summary>中文含义：已存储定义。寄存开始时的定义引用，防止动画期间实例被替换成其他物品。</summary>
    private ItemData storedDefinition;
    /// <summary>中文含义：已存储数量。寄存开始时的数量，数量发生变化则拒绝旧来源转移。</summary>
    private int storedCount;
    /// <summary>中文含义：已存储会话版本。寄存开始时的背包运行版本。</summary>
    private int storedSessionVersion;
    /// <summary>中文含义：已存储场景。寄存开始时的场景标识，跨场景回调不能提交。</summary>
    private int storedScene;
    /// <summary>中文含义：已存储编号。寄存请求序号；开始与取消均递增，调用方在动画回调中带回。</summary>
    public int StoredId { get; private set; }
    /// <summary>中文含义：是否等待中已存储。是否仍持有等待寄存的实例，不额外保存另一份等待状态。</summary>
    public bool WaitingStored => storedItem != null;

    /// <summary>中文含义：是否提交中间。物品效果提交中的重入保护，Use 的 finally 必定恢复。</summary>
    private bool commitMiddle;
    /// <summary>中文含义：使用编号。普通物品使用请求编号序列，由 NewUseId 分配。</summary>
    private long useId;
    /// <summary>中文含义：已提交使用编号。最近已提交的使用请求编号，防止重复或旧回调再次消费。</summary>
    private long committedUseId;
    /// <summary>寄存宿主开始动画前调用；确认来源实例仍属于背包后保存回调所需的来源快照。</summary>
    /// <param name="inventory">中文含义：背包。来源背包规则模型。</param>
    /// <param name="itemValue">中文含义：物品值。准备寄存的已持有实例。</param>
    /// <param name="scene">中文含义：场景。宿主提供的当前场景标识。</param>
    /// <returns>新寄存编号；已有等待或来源无效时返回零。</returns>
    public int BeginStored(InventoryRulesModel inventory, GridItem itemValue, int scene)
    {
        if (WaitingStored || inventory == null || !inventory.ContainsInstance(itemValue))
        {
            return 0;
        }

        StoredId++;
        storedItem = itemValue;
        storedInventory = inventory;
        storedDefinition = itemValue.item;
        storedCount = itemValue.count;
        storedSessionVersion = inventory.RunVersion;
        storedScene = scene;
        return StoredId;
    }

    /// <summary>宿主取消或 CompleteStored 处理当前回调时调用，递增序号并清除保留引用。</summary>
    public void CancelStored()
    {
        StoredId++;
        storedItem = null;
        storedInventory = null;
        storedDefinition = null;
        storedCount = 0;
    }

    /// <summary>寄存动画回调入口；先核验编号和来源快照，再清除等待，最后按动作结果向仓库提交原子转移。</summary>
    /// <param name="id">中文含义：编号。动画开始时 BeginStored 返回的编号。</param>
    /// <param name="actionSuccess">中文含义：是否动作成功。动作是否正常完成。</param>
    /// <param name="hostValid">中文含义：宿主是否有效。宿主是否仍允许该次寄存提交。</param>
    /// <param name="scene">中文含义：场景。回调发生时的场景标识。</param>
    /// <param name="storage">中文含义：仓库。接收物品的仓库模型，沿用调用方保证有效的契约。</param>
    /// <returns>0 表示旧回调，1 表示提交成功，-1 表示动作取消，-2 表示来源变化或转移失败。</returns>
    public int CompleteStored(int id, bool actionSuccess, bool hostValid, int scene, StorageRulesModel storage)
    {
        if (id != StoredId || !WaitingStored)
        {
            return 0;
        }

        // 清除等待前保留本次物品，后续只操作这一次请求对应的实例。
        // 中文含义：itemValue 为“物品值”。
var itemValue = storedItem;
        // 清除等待前保留来源模型，用于版本核验和最终静默转移。
        // 中文含义：inventory 为“背包”。
var inventory = storedInventory;
        // 先校验宿主、场景和来源对象，再按原短路顺序核验版本、字段和仓库归属。
        // 中文含义：sourceValid 为“来源是否有效”。
bool sourceValid = hostValid && scene == storedScene && inventory != null;
        if (sourceValid)
        {
            sourceValid = inventory.RunVersion == storedSessionVersion && itemValue.item == storedDefinition && itemValue.count == storedCount;
        }

        if (sourceValid)
        {
            sourceValid = storage.ValidateInventorySource(itemValue, inventory);
        }

        CancelStored();
        if (!actionSuccess || itemValue.item == null)
        {
            return -1;
        }

        if (sourceValid && storage.CommitTransferToStorage(itemValue, inventory))
        {
            return 1;
        }

        return -2;
    }

    /// <summary>使用宿主构造新请求上下文前调用，返回严格递增的本服务请求编号。</summary>
    /// <returns>可写入 RequestContext.AssociationId 的编号。</returns>
    public long NewUseId()
    {
        return ++useId;
    }

    /// <summary>背包使用链的统一入口；校验请求和对象租用，成功提交效果后记录编号并发布背包反馈。</summary>
    /// <param name="request">中文含义：请求。本次请求上下文，目标租用版本必须匹配背包运行版本。</param>
    /// <param name="inventory">中文含义：背包。拥有物品的背包模型。</param>
    /// <param name="item">中文含义：当前背包网格物品。准备使用的已持有实例。</param>
    /// <param name="player">中文含义：玩家。已绑定的角色业务接口，不从场景中补找。</param>
    /// <returns>携带提交结论和原因的结果。</returns>
    public ItemUseResult RequestUse(RequestContext request, InventoryRulesModel inventory, GridItem item, IItemUseTarget player)
    {
        if (!request.Valid)
        {
            return new ItemUseResult(request, false, RejectionReason.ParameterInvalid);
        }

        if (commitMiddle)
        {
            return new ItemUseResult(request, false, RejectionReason.ActionConflict);
        }

        if (request.AssociationId <= committedUseId)
        {
            return new ItemUseResult(request, false, RejectionReason.CallbackHasExpired);
        }

        if (inventory == null || request.Target.LeaseVersion != inventory.RunVersion || !inventory.ContainsInstance(item) || player == null || !player.Valid)
        {
            return new ItemUseResult(request, false, RejectionReason.ObjectHasInvalidate);
        }

        // 保存唯一一次效果提交结果，反馈和返回值均依据它处理。
        // 中文含义：commit 为“是否提交”。
bool commit = Use(inventory, item, player);
        if (commit)
        {
            committedUseId = request.AssociationId;
            inventory.CompleteUseFeedback();
        }

        // 成功结论没有拒绝原因，失败保留原资源不足结果。
        // 中文含义：reason 为“原因”。
RejectionReason reason = RejectionReason.AssetInsufficient;
        if (commit)
        {
            reason = RejectionReason.None;
        }

        return new ItemUseResult(request, commit, reason);
    }

    /// <summary>RequestUse 的内部提交步骤，按物品类别同步执行角色效果和背包消费，finally 释放重入保护。</summary>
    /// <param name="inventory">中文含义：背包。物品所属背包模型。</param>
    /// <param name="itemValue">中文含义：物品值。已经核验所属关系的物品实例。</param>
    /// <param name="player">中文含义：玩家。效果接收方的角色业务接口。</param>
    /// <returns>本次是否成功消费物品并提交对应效果。</returns>
    private bool Use(InventoryRulesModel inventory, GridItem itemValue, IItemUseTarget player)
    {
        if (commitMiddle || inventory == null || !inventory.ContainsInstance(itemValue) || player == null)
        {
            return false;
        }

        commitMiddle = true;
        try
        {
            if (itemValue.item.itemType == ItemType.Healing)
            {
                if (!inventory.CanUseItem(itemValue.item) || (itemValue.item.maxHealthBonus <= 0 && player.CurrentHealth >= player.HealthMaximum))
                {
                    return false;
                }

                if (itemValue.item.maxHealthBonus > 0)
                {
                    player.IncreaseHealthMaximum(itemValue.item.maxHealthBonus);
                }

                if (itemValue.item.healAmount > 0)
                {
                    player.Healing(itemValue.item.healAmount);
                }

                inventory.ConsumeGridItem(itemValue);
                return true;
            }

            if (itemValue.item.itemType == ItemType.Ammo)
            {
                if (!player.HasWeaponModule)
                {
                    return false;
                }

                // 整个弹药实例一次转换为兼容备弹，保留每单位和堆叠数量的最小值处理。
                // 中文含义：countValue 为“数量值”。
int countValue = Mathf.Max(1, itemValue.item.ammoAmount) * Mathf.Max(1, itemValue.count);
                player.IncreaseCompatibleReserveAmmo(itemValue.item.ammoWeapon, countValue);
                return inventory.RemoveInstanceSilently(itemValue);
            }

            return false;
        }
        finally
        {
            commitMiddle = false;
        }
    }
}
