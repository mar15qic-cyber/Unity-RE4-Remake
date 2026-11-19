using UnityEngine;
using System.Collections.Generic;

/// <summary>本局掉落执行宿主。敌人死亡或容器破坏直接提交位置，宿主读取当前资源快照交给权重规则，完成独立金钱与物品随机后创建拾取物；连续未掉落和重复类别状态只归本宿主拥有。</summary>
public class DropManager : Singleton<DropManager>
{
    /// <summary>中文含义：切换场景时是否保留此单例对象。掉落管理器不跨场景存活，单例生命周期由基类管理。</summary>
    protected override bool Persistent => false;

    /// <summary>中文含义：掉落表。本局动态掉落配置；未绑定时沿用 Resources/DropTable 路径加载。</summary>
    [Header("配置")]
    [Tooltip("掉落表（为空时从 Resources/DropTable 加载）")]
    public DropTable dropTable;
    /// <summary>中文含义：当前区域。当前区域标识，RollItemDrop 用它选择区域倍率。</summary>
    [Tooltip("当前区域")]
    public DropZone currentZone = DropZone.Village;
    /// <summary>中文含义：占位网格。物品无世界模型且不使用图标标识时的占位网格。</summary>
    [Header("占位资源")]
    [Tooltip("占位网格（无 worldModelPrefab 时使用，留空则掉落物不可见但可拾取）")]
    public Mesh placeholderMesh;
    /// <summary>中文含义：占位材质。占位网格使用的可选共享材质。</summary>
    [Tooltip("占位材质")]
    public Material placeholderMaterial;
    // ═══════════════════ 运行时状态 ═══════════════════
    /// <summary>中文含义：连续无掉落物集合。连续未掉落普通物品的次数，金钱独立结果不计入；供幸运因子使用。</summary>
    private int consecutiveNoDrops;
    /// <summary>中文含义：上一次掉落类别。上次物品结果类别，参与每个候选的重复惩罚选择。</summary>
    private DropCategory lastDropCategory = DropCategory.Nothing;
    /// <summary>中文含义：当前重复数量。连续相同类别的计数；不同类别成功后从一重新开始。</summary>
    private int currentRepeatCount;
    private int reserveKnivesDropped;
    // ═══════════════════ 生命周期 ═══════════════════
    /// <summary>Unity 启动回调，只有未绑定掉落表时才从既有路径加载，不改动资源内容。</summary>
    void Start()
    {
        if (dropTable == null)
        {
            dropTable = Resources.Load<DropTable>("DropTable");
        }
    }

    // ═══════════════════ 公开接口 ═══════════════════
    /// <summary>区域切换调用方同步提交区域，只改变后续掉落选用的配置标识。</summary>
    /// <param name="zone">中文含义：区。后续随机使用的区域标识。</param>
    public void SetZone(DropZone zone)
    {
        currentZone = zone;
    }

    /// <summary>敌人死亡提交入口，资格通过后先独立摇金钱，再摇普通物品，传入同一死亡位置。</summary>
    /// <param name="position">中文含义：位置。死亡掉落的世界位置。</param>
    public void OnEnemyDeath(Vector3 position)
    {
        OnEnemyDeath(position, false);
    }

    internal void OnEnemyDeath(Vector3 position, bool allowReserveKnife)
    {
        if (!CanRollDrop())
        {
            return;
        }

        // 1. 金钱独立摇奖
        RollMoneyDrop(position);
        // 2. 物品加权随机
        if (!allowReserveKnife || !TryDropReserveKnife(position))
        {
            RollItemDrop(position, false);
        }
    }

    private bool TryDropReserveKnife(Vector3 position)
    {
        if (reserveKnivesDropped >= 2)
        {
            return false;
        }

        float roll = Random.value;
        if (roll >= .06f)
        {
            return false;
        }

        string path = "Items/KitchenKnife";
        if (roll >= .04f)
        {
            path = "Items/BootKnife";
        }

        ItemData knife = Resources.Load<ItemData>(path);
        if (knife == null || knife.worldModelPrefab == null || knife.icon == null)
        {
            return false;
        }

        SpawnPickup(knife, 1, position);
        reserveKnivesDropped++;
        consecutiveNoDrops = 0;
        currentRepeatCount = 0;
        return true;
    }

    /// <summary>可破坏容器提交入口，允许独立选择金钱流程与物品保底；资格仍由同一入口校验。</summary>
    /// <param name="position">中文含义：位置。容器破坏的世界位置。</param>
    /// <param name="guaranteeItemDrop">中文含义：是否保底物品掉落。是否跳过总物品概率和不掉落候选，但仍需有正权重候选。</param>
    /// <param name="includeMoneyDrop">中文含义：是否包含金钱掉落。是否同时执行独立金钱随机。</param>
    public void OnContainerBreak(Vector3 position, bool guaranteeItemDrop, bool includeMoneyDrop = true)
    {
        if (!CanRollDrop())
        {
            return;
        }

        if (includeMoneyDrop)
        {
            RollMoneyDrop(position);
        }

        RollItemDrop(position, guaranteeItemDrop);
    }

    /// <summary>死亡与容器入口共用的资格判断，确保掉落表可用且当前玩家不处于死亡状态。</summary>
    bool CanRollDrop()
    {
        if (dropTable == null)
        {
            dropTable = Resources.Load<DropTable>("DropTable");
            if (dropTable == null)
            {
                return false;
            }
        }

        // 玩家死亡时不掉落
        if (LeonController.Instance == null)
        {
            return false;
        }

        if (LeonController.Instance.currentState == LeonState.Dead)
        {
            return false;
        }

        return true;
    }

    // ═══════════════════ 金钱摇奖 ═══════════════════
    /// <summary>资格通过后的金钱流程，按幸运倍率修正概率，成功后随机数量并创建拾取物。</summary>
    /// <param name="position">中文含义：位置。拾取物生成的基础世界位置。</param>
    void RollMoneyDrop(Vector3 position)
    {
        if (dropTable.moneyItem == null)
        {
            return;
        }

        // 连续未掉落对应的幸运倍率，单次读取后用于本次概率。
        // 中文含义：luckFactor 为“运气系数”。
float luckFactor = WeightCalculator.CalculateLuckFactor(dropTable, consecutiveNoDrops);
        // 金钱基础概率乘幸运后限制到不超过一的结果。
        // 中文含义：chance 为“概率”。
float chance = Mathf.Min(1f, dropTable.moneyDropChance * luckFactor);
        if (Random.value <= chance)
        {
            // 本次成功金钱随机的整数数量，保留原上下限包含语义。
            // 中文含义：amount 为“大小”。
int amount = Random.Range(dropTable.moneyMin, dropTable.moneyMax + 1);
            SpawnPickup(dropTable.moneyItem, amount, position);
        }
    }

    // ═══════════════════ 物品加权随机 ═══════════════════
    /// <summary>普通物品流程，先核对总概率，再读取一次资源快照计算所有候选权重；随机完成后更新本局计数并创建拾取物。</summary>
    /// <param name="position">中文含义：位置。拾取物生成的基础世界位置。</param>
    /// <param name="guaranteeItemDrop">中文含义：是否保底物品掉落。是否排除总概率失败和不掉落候选。</param>
    void RollItemDrop(Vector3 position, bool guaranteeItemDrop)
    {
        // 当前区域在配置表中的首个匹配配置，未配置时为空。
        // 中文含义：zoneConfig 为“区配置”。
ZoneConfig zoneConfig = dropTable.GetZoneConfig(currentZone);
        // 本轮统一使用的幸运倍率。
        // 中文含义：luckFactor 为“运气系数”。
float luckFactor = WeightCalculator.CalculateLuckFactor(dropTable, consecutiveNoDrops);
        // 普通敌人先经过一次总掉落概率判定，容器保底流程仍然直接进入加权表。
        // 普通物品基础概率乘幸运后限制到零至一。
        // 中文含义：itemDropChance 为“物品掉落概率”。
float itemDropChance = Mathf.Clamp01(dropTable.itemDropChance * luckFactor);
        if (!guaranteeItemDrop && Random.value > itemDropChance)
        {
            consecutiveNoDrops++;
            lastDropCategory = DropCategory.Nothing;
            currentRepeatCount = 0;
            return;
        }

        // 本轮候选共用的一次资源读取快照，避免逐条重新查询场景。
        // 中文含义：asset 为“资源”。
var asset = DropResourceReader.Read();
        // 构建加权列表
        // 本轮条目与最终权重的候选列表，保持配置顺序。
        // 中文含义：candidates 为“候选集合”。
var candidates = new List<(DropEntry /* 中文含义：掉落候选条目。 */ entry, float /* 中文含义：该掉落候选的抽取权重。 */ weight)>();
        // 当前配置候选，缺条目或物品时跳过。
        // 中文含义：entry 为“当前掉落入口”。每轮从掉落表的条目集合取得一个元素，仅代表当前这一项。
foreach (var entry in dropTable.entries)
        {
            if (entry == null || entry.item == null)
            {
                continue;
            }

            // 只有候选类别等于上一结果类别时才带入现有重复次数。
            // 中文含义：repeatCount 为“重复数量”。
int repeatCount = 0;
            if (lastDropCategory == entry.category)
            {
                repeatCount = currentRepeatCount;
            }

            // 权重规则同步返回的非负最终值，只有正数加入竞争。
            // 中文含义：weight 为“权重”。
float weight = DropWeightRules.CalculateWeight(entry, dropTable, consecutiveNoDrops, repeatCount, zoneConfig, asset);
            if (weight > 0f)
            {
                candidates.Add((entry, weight));
            }
        }

        // 不掉落条目（幸运因子反向影响：越幸运，不掉落权重越低）
        if (!guaranteeItemDrop)
        {
            // 非保底流程中不掉落候选的权重，幸运越高越低。
            // 中文含义：nothingW 为“无掉落宽度”。
float nothingW = dropTable.nothingWeight / Mathf.Max(1f, luckFactor);
            candidates.Add((null, nothingW));
        }

        // 加权随机返回的条目，null 表示没有选中实际物品。
        // 中文含义：selected 为“选定”。
DropEntry selected = WeightedSelect(candidates);
        if (selected == null)
        {
            consecutiveNoDrops++;
            lastDropCategory = DropCategory.Nothing;
            currentRepeatCount++;
            return;
        }

        consecutiveNoDrops = 0;
        if (lastDropCategory == selected.category)
        {
            currentRepeatCount++;
        }
        else
        {
            lastDropCategory = selected.category;
            currentRepeatCount = 1;
        }

        // 本次条目随机得到的整数堆叠数量。
        // 中文含义：count 为“数量”。
int count = Random.Range(selected.minCount, selected.maxCount + 1);
        SpawnPickup(selected.item, count, position);
    }

    /// <summary>物品随机链内部按候选权重求和并累计选择，保持既有随机调用次数和列表顺序。</summary>
    /// <param name="candidates">中文含义：候选集合。包含条目与权重的候选列表，null 条目代表不掉落。</param>
    static DropEntry WeightedSelect(List<(DropEntry /* 中文含义：掉落候选条目。 */ entry, float /* 中文含义：该掉落候选的抽取权重。 */ weight)> candidates)
    {
        // 全部候选权重之和，非正数时不调用随机。
        // 中文含义：totalWeight 为“总计权重”。
float totalWeight = 0f;
        // 拆包变量 w 是当前候选权重，条目本身在求和阶段使用弃元忽略。
        foreach (var(_, /* 中文含义：当前掉落候选的抽取权重。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ w)in candidates)
        {
            totalWeight += w;
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        // 单次随机数映射到总权重区间的位置。
        // 中文含义：roll 为“随机抽取”。
float roll = Random.value * totalWeight;
        // 按现有列表顺序累计的权重上界。
        // 中文含义：accumulated 为“累计”。
float accumulated = 0f;
        // 拆包变量 entry 是候选条目，w 是其权重；按原列表顺序累计命中区间。
        foreach (var(/* 中文含义：当前掉落入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ entry, /* 中文含义：当前掉落候选的抽取权重。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ w)in candidates)
        {
            accumulated += w;
            if (roll <= accumulated)
            {
                return entry;
            }
        }

        return null;
    }

    // ═══════════════════ 生成拾取物 ═══════════════════
    /// <summary>外部生成调用或内部随机成功后调用，创建根对象、占位表现、触发碰撞体及 ItemPickup，并写入物品定义和数量。</summary>
    /// <param name="item">中文含义：当前物品数据配置。拾取物定义，为空时无操作。</param>
    /// <param name="count">中文含义：数量。数量按至少一处理。</param>
    /// <param name="position">中文含义：位置。基础世界位置，根对象向上偏移半米。</param>
    private readonly HitQueryBuffer floorQueries = new HitQueryBuffer();

    public void SpawnItemPickup(ItemData item, int count, Vector3 position)
    {
        if (item == null)
        {
            return;
        }

        count = Mathf.Max(1, count);
        // 本次新建的拾取物根对象，不使用现有对象池，保留原生命周期。
        // 中文含义：go 为“当前游戏对象”。
var go = new GameObject($"Drop_{item.itemId}");
        go.transform.position = FindDropSupport(position);
        // 占位网格：仅在 ItemData 没有 worldModelPrefab 时添加
        // （ItemPickup.Start 会移除占位并替换为真实模型）
        if (item.worldModelPrefab == null && item.icon != null)
        {
            CreateAtlasPickupMarker(go.transform, item);
        }
        else if (item.worldModelPrefab == null && placeholderMesh != null)
        {
            // 占位 MeshFilter 组件，绑定配置网格。
            // 中文含义：mf 为“网格过滤器”。
var mf = go.AddComponent<MeshFilter>();
            // 占位 MeshRenderer 组件，可绑定共享材质。
            // 中文含义：mr 为“网格渲染器”。
var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = placeholderMesh;
            if (placeholderMaterial != null)
            {
                mr.sharedMaterial = placeholderMaterial;
            }
        }

        // Collider（ItemPickup 需要）
        // 拾取触发碰撞体，使用既有尺寸。
        // 中文含义：col 为“列”。
var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.5f, 0.5f, 0.5f);
        // ItemPickup 组件
        // 实际拾取业务组件，后续由它管理显示和交互。
        // 中文含义：pickup 为“拾取”。
var pickup = go.AddComponent<ItemPickup>();
        pickup.ConfigureGroundDrop();
        pickup.item = item;
        pickup.count = count;
        pickup.itemId = item.itemId;
    }

    /// <summary>内部金钱和物品随机共用的转发入口，实际创建统一交给 SpawnItemPickup。</summary>
    /// <param name="item">中文含义：当前物品数据配置。拾取定义。</param>
    /// <param name="count">中文含义：数量。随机得到的数量。</param>
    /// <param name="position">中文含义：位置。基础生成世界位置。</param>
    void SpawnPickup(ItemData item, int count, Vector3 position)
    {
        SpawnItemPickup(item, count, position);
    }

    private Vector3 FindDropSupport(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * .5f;
        RaycastHit[] hits = floorQueries.Raycast(origin, Vector3.down, 2f, 1, QueryTriggerInteraction.Ignore, out int count);
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = hits[index];
            if (hit.distance >= nearest || hit.normal.y < .55f || hit.collider.attachedRigidbody != null ||
                hit.collider.GetComponentInParent<DestructibleWoodenBox>() != null)
            {
                continue;
            }

            nearest = hit.distance;
            position = hit.point;
        }

        return position;
    }

    /// <summary>拾取物没有世界模型而有图标时调用，创建两个正交图标切片作为既有占位表现。</summary>
    /// <param name="parent">中文含义：父级。拾取根节点，两个标识都作为其子节点。</param>
    /// <param name="item">中文含义：当前物品数据配置。提供图标切片的定义。</param>
    private static void CreateAtlasPickupMarker(Transform parent, ItemData item)
    {
        if (parent == null || item == null || item.icon == null)
        {
            return;
        }

        var marker = new GameObject("图集拾取标识");
        marker.transform.SetParent(parent, false);
        marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        marker.transform.localScale = Vector3.one * .72f;
        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = item.icon;
        renderer.color = Color.white;
        renderer.sortingOrder = 50;
    }

    public string GetDebugState()
    {
        return $"Zone={currentZone} | NoDrops={consecutiveNoDrops} | LastCat={lastDropCategory} | Repeat={currentRepeatCount}";
    }

    /// <summary>编辑器组件上下文菜单入口，将对象前方位置提交给同一敌人掉落流程，不绕开资格判断。</summary>
    [ContextMenu("测试掉落")]
    void TestDrop()
    {
        OnEnemyDeath(transform.position + transform.forward * 2f);
    }
}
