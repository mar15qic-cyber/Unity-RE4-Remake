/// <summary>
/// 背包和仓库 ReadSnapshot 生成的物品值快照，复制标识、数量、位置和耐久。
/// 消费者通过返回的只读快照集合读取，不持有 GridItem 或物品定义的可变引用。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Game.InventoryRules", "物品状态快照")]
public readonly struct ItemStateSnapshot
{
    /// <summary>中文含义：定义编号。定义中的物品标识字符串；原实例没有定义时为 null。</summary>
    public string DefinitionId { get; }
    /// <summary>中文含义：数量。构造时的堆叠数量，之后容器变化不会追随更新。</summary>
    public int Count { get; }
    /// <summary>中文含义：水平坐标。构造时的占位横坐标。</summary>
    public int HorizontalCoordinate { get; }
    /// <summary>中文含义：竖直坐标。构造时的占位纵坐标。</summary>
    public int VerticalCoordinate { get; }
    /// <summary>中文含义：旋转。构造时的四分之一圈旋转编号。</summary>
    public int Rotation { get; }
    /// <summary>中文含义：耐久。构造时的实例耐久，保留原始数值。</summary>
    public int Durability { get; }

    /// <summary>背包或仓库规则遍历当前物品时调用，按值复制当前状态，不触发容器事件。</summary>
    /// <param name="itemValue">中文含义：物品值。容器已经确认存在的物品实例。</param>
    internal ItemStateSnapshot(GridItem itemValue)
    {
        if (itemValue.item != null)
        {
            DefinitionId = itemValue.item.itemId;
        }
        else
        {
            DefinitionId = null;
        }

        Count = itemValue.count;
        HorizontalCoordinate = itemValue.gridX;
        VerticalCoordinate = itemValue.gridY;
        Rotation = itemValue.rotation;
        Durability = itemValue.currentDurability;
    }
}
