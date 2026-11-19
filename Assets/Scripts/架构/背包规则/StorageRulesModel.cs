using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 仓库物品的唯一拥有者。StorageBoxManager 等宿主同步提交存取请求，模型与背包规则完成原子交接后，
/// 先让背包发布转移事实，再发送 OnStorageChanged；界面通过只读列表或值快照读取当前结果。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.InventoryRules", "仓库规则模型")]
public sealed class StorageRulesModel
{
    /// <summary>中文含义：仓库物品列表。仓库实际持有的物品实例集合，仅由初始化与转移入口修改。</summary>
    private readonly List<GridItem> storageItems = new List<GridItem>();
    /// <summary>中文含义：只读物品。同一集合的只读包装，供旧查询读取；其中实例的内部写权限仍由规则层控制。</summary>
    private readonly System.Collections.ObjectModel.ReadOnlyCollection<GridItem> readOnlyItem;
    /// <summary>中文含义：仓库变化集合。转移前规划的修改项，双方来源确认后才统一应用，失败时清空。</summary>
    private readonly List<StorageChange> storageChanges = new List<StorageChange>();
    /// <summary>中文含义：仓库变化时的通知回调。仓库数据已经提交后的同步通知，宿主和界面订阅并读取当前数据。</summary>
    public event System.Action OnStorageChanged;
    /// <summary>仓库宿主创建模型时构造一次只读包装，不复制物品集合。</summary>
    public StorageRulesModel()
    {
        readOnlyItem = storageItems.AsReadOnly();
    }

    /// <summary>兼容查询入口，返回实时只读集合包装；需要独立值副本的消费者使用 ReadSnapshot。</summary>
    /// <returns>当前仓库实例的只读视图。</returns>
    public IReadOnlyList<GridItem> GetStorageItems()
    {
        return readOnlyItem;
    }

    /// <summary>新查询链读取每个物品的值副本，不暴露定义对象或可变实例。</summary>
    /// <returns>独立的物品状态只读集合，后续仓库修改不会追随更新。</returns>
    public System.Collections.ObjectModel.ReadOnlyCollection<ItemStateSnapshot> ReadSnapshot()
    {
        // 本次查询专属数组，长度与当前仓库实例数一致。
        // 中文含义：result 为“当前物品状态快照数组”。
var result = new ItemStateSnapshot[storageItems.Count];
        // i 是仓库实例和结果数组共用的索引。
        // 中文含义：i 为“当前遍历索引”。
for (int i = 0; i < storageItems.Count; i++)
        {
            result[i] = new ItemStateSnapshot(storageItems[i]);
        }

        return System.Array.AsReadOnly(result);
    }

    /// <summary>存取宿主核验实例归属时调用，只读检查，不比较定义或物品标识代替实例身份。</summary>
    /// <param name="item">中文含义：当前背包网格物品。待检查实例。</param>
    /// <returns>当前集合确实持有该实例时为 true。</returns>
    public bool ContainsInstance(GridItem item)
    {
        return storageItems.Contains(item);
    }

    /// <summary>仓库宿主初始化本局时调用，清空旧内容，可放入一份初始奖励，然后发布变化。</summary>
    /// <param name="reward">中文含义：奖励。可选的奖励定义；为空时初始化为空仓库。</param>
    public void Initialize(ItemData reward)
    {
        storageItems.Clear();
        if (reward != null)
        {
            storageItems.Add(new GridItem { item = reward, count = 1, currentDurability = -1 });
        }

        NotifyChanged();
    }

    /// <summary>一条尚未提交的仓库堆叠修改或新增实例规划。</summary>
    private struct StorageChange
    {
        /// <summary>中文含义：目标背包网格物品。即将写入数量的目标实例。</summary>
        internal GridItem target;
        /// <summary>中文含义：数量。提交后的目标数量。</summary>
        internal int count;
        /// <summary>中文含义：是否新增。是否还需要把目标实例加入仓库集合。</summary>
        internal bool added;
    }

    /// <summary>初始化或转移提交完成后调用，复制一次订阅列表并同步通知消费者。</summary>
    public void NotifyChanged()
    {
        // 委托快照保持原条件调用的单次读取语义。
        // 中文含义：handler 为“处理器”。
System.Action handler = OnStorageChanged;
        if (handler != null)
        {
            handler();
        }
    }

    /// <summary>寄存开始、动画回调及实际提交共用的来源核验，防止从外部容器或重复实例寄存。</summary>
    /// <param name="item">中文含义：当前背包网格物品。拟寄存的物品实例。</param>
    /// <param name="invMgr">中文含义：背包管理器。声称持有该实例的背包模型。</param>
    /// <returns>实例有定义及正数数量、归属背包且尚未归属仓库时为 true。</returns>
    public bool ValidateInventorySource(GridItem item, InventoryRulesModel invMgr)
    {
        if (item == null || item.item == null || item.count <= 0 || invMgr == null)
        {
            return false;
        }

        return invMgr.ContainsInstance(item) && !storageItems.Contains(item);
    }

    /// <summary>寄存事务调用；先规划堆叠和拆分，背包静默移除成功后统一提交仓库，再通知双方消费者。</summary>
    /// <param name="source">中文含义：来源背包网格物品。背包持有的源实例，优先复用于第一份新增堆叠。</param>
    /// <param name="invMgr">中文含义：背包管理器。源背包规则模型。</param>
    /// <returns>双方数据均已交接时返回 true，来源无效或静默移除失败时不提交仓库规划。</returns>
    public bool CommitTransferToStorage(GridItem source, InventoryRulesModel invMgr)
    {
        if (!ValidateInventorySource(source, invMgr))
        {
            return false;
        }

        // 本次源定义，合并时要求引用一致，并遵守耐久匹配。
        // 中文含义：definition 为“定义”。
ItemData definition = source.item;
        // 尚待规划的源数量；只在全部规划后才修改实际实例。
        // 中文含义：remaining 为“剩余”。
int remaining = source.count;
        // 该定义的有效堆叠容量，至少为一。
        // 中文含义：maximum 为“最大”。
int maximum = Mathf.Max(1, definition.maxStack);
        storageChanges.Clear();
        if (maximum > 1)
        {
            // existing 是可合并候选，只记录将来的数量，不在规划阶段写入。
            // 中文含义：existing 为“已有”。每轮从仓库物品列表取得一个元素，仅代表当前这一项。
foreach (GridItem existing in storageItems)
            {
                if (existing.item != definition || existing.count >= maximum || definition.usesDurability && existing.currentDurability != source.currentDurability)
                {
                    continue;
                }

                // 本候选能够接收的数量，受剩余源数量及堆叠空位共同限制。
                // 中文含义：amount 为“大小”。
int amount = Mathf.Min(remaining, maximum - existing.count);
                storageChanges.Add(new StorageChange { target = existing, count = existing.count + amount });
                remaining -= amount;
                if (remaining == 0)
                {
                    break;
                }
            }
        }

        // 新增堆叠中只允许一次复用原实例，其余份额创建独立实例。
        // 中文含义：reused 为“是否复用的”。
bool reused = false;
        while (remaining > 0)
        {
            // 本次新增堆叠的规划数量。
            // 中文含义：amount 为“大小”。
int amount = Mathf.Min(remaining, maximum);
            // 本条规划的目标，首次复用源实例，之后复制定义、旋转及耐久。
            // 中文含义：target 为“目标背包网格物品”。
GridItem target;
            if (!reused)
            {
                target = source;
            }
            else
            {
                target = new GridItem
                {
                    item = definition,
                    rotation = source.rotation,
                    currentDurability = source.currentDurability
                };
            }

            reused = true;
            storageChanges.Add(new StorageChange { target = target, count = amount, added = true });
            remaining -= amount;
        }

        if (!invMgr.RemoveInstanceSilently(source))
        {
            storageChanges.Clear();
            return false;
        }

        // change 是已经确认来源可移除的一条规划，此时才向仓库实际写入。
        // 中文含义：change 为“变化”。每轮从仓库变化集合取得一个元素，仅代表当前这一项。
foreach (StorageChange change in storageChanges)
        {
            change.target.count = change.count;
            if (change.added)
            {
                storageItems.Add(change.target);
            }
        }

        storageChanges.Clear();
        invMgr.PublishTransfer();
        NotifyChanged();
        return true;
    }

    /// <summary>取出宿主调用；背包先静默规划接收，成功后仓库移除源实例，再通知双方读取最终状态。</summary>
    /// <param name="item">中文含义：当前背包网格物品。当前仓库持有的源实例。</param>
    /// <param name="invMgr">中文含义：背包管理器。目标背包规则模型。</param>
    /// <returns>成功完成双方交接时返回 true；空间不足或来源无效时保持原容器归属。</returns>
    public bool TransferToInventory(GridItem item, InventoryRulesModel invMgr)
    {
        if (item == null || item.item == null || item.count <= 0 || !storageItems.Contains(item))
        {
            return false;
        }

        if (invMgr == null || !invMgr.AddTransferredSilently(item))
        {
            return false;
        }

        storageItems.Remove(item);
        invMgr.PublishTransfer();
        NotifyChanged();
        return true;
    }
}
