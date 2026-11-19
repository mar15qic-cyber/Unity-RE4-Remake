using UnityEngine;

/// <summary>
/// 背包或仓库规则拥有的单个可变物品实例。规则层内部修改数量、位置、旋转和耐久，
/// 旧消费者通过只读属性读取；跨边界的新查询使用 ItemStateSnapshot，避免持有可写状态。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "GridItem")]
[System.Serializable]
public class GridItem
{
    /// <summary>中文含义：当前物品数据配置。物品定义引用，由规则模型在创建或转移时保留，提供尺寸及耐久配置。</summary>
    public ItemData item { get; internal set; }
    /// <summary>中文含义：数量。该实例的堆叠数量，只有同程序集规则写入口可以提交修改。</summary>
    public int count { get; internal set; }
    /// <summary>中文含义：网格横向。占位矩形左上角的横坐标。</summary>
    public int gridX { get; internal set; }
    /// <summary>中文含义：网格纵向。占位矩形左上角的纵坐标。</summary>
    public int gridY { get; internal set; }
    /// <summary>中文含义：当前耐久。实例当前耐久；-1 保留未设置或不适用的既有语义。</summary>
    public int currentDurability { get; internal set; } = -1;
    /// <summary>中文含义：旋转。四分之一圈旋转编号：0、1、2、3 分别对应 0、90、180、270 度。</summary>
    public int rotation { get; internal set; }

    /// <summary>中文含义：宽度。放置判定读取的实际宽度；奇数旋转交换定义的宽高。</summary>
    public int Width
    {
        get
        {
            if (rotation % 2 == 0)
            {
                return item.gridWidth;
            }

            return item.gridHeight;
        }
    }

    /// <summary>中文含义：高度。放置判定读取的实际高度；奇数旋转交换定义的宽高。</summary>
    public int Height
    {
        get
        {
            if (rotation % 2 == 0)
            {
                return item.gridHeight;
            }

            return item.gridWidth;
        }
    }

    /// <summary>中文含义：耐久归一化。界面读取的归一化耐久，限制在零到一；无定义或不使用耐久时返回零。</summary>
    public float DurabilityNormalized
    {
        get
        {
            if (item != null && item.usesDurability)
            {
                return Mathf.Clamp01(currentDurability / (float)Mathf.Max(1, item.maxDurability));
            }

            return 0f;
        }
    }

    /// <summary>背包占位与格子查询调用，按当前旋转后的矩形检查单个格子。</summary>
    /// <param name="x">中文含义：横向。待查询格子的横坐标。</param>
    /// <param name="y">中文含义：纵向。待查询格子的纵坐标。</param>
    /// <returns>格子处于本实例占位矩形内时为 true，右侧和下侧边界不包含。</returns>
    public bool Occupies(int x, int y)
    {
        return x >= gridX && x < gridX + Width && y >= gridY && y < gridY + Height;
    }
}
