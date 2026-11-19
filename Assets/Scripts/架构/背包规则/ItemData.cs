using UnityEngine;

/// <summary>单次合成所需材料的配置记录，物品资产序列化保存，背包解析时按标识合并数量，不在资产上累计运行库存。</summary>
[System.Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "CraftingIngredientRequirement")]
public class CraftingIngredientRequirement
{
    /// <summary>中文含义：材料的唯一标识。材料定义的唯一标识，背包按该键查询库存并匹配需求。</summary>
    [Tooltip("材料的唯一标识")]
    public string itemId;
    /// <summary>中文含义：单次合成需要消耗的数量。单次合成所需数量；背包解析时按至少一份处理。</summary>
    [Min(1)]
    [Tooltip("单次合成需要消耗的数量")]
    public int count = 1;
}

/// <summary>物品的共享定义资产，由资源或配置注入背包、拾取、使用、合成与检视宿主。实例数量、布局和当前耐久归 GridItem 与规则模型，本资产只提供配置。</summary>
[CreateAssetMenu(fileName = "Item", menuName = "RE4/Item Data")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "ItemData")]
public class ItemData : ScriptableObject
{
    /// <summary>中文含义：唯一标识，用于存档查询。物品唯一标识，库存、配方、动态资源与存档映射使用此键，修改须同步消费者。</summary>
    [Header("基础信息")]
    [Tooltip("唯一标识，用于存档查询")]
    public string itemId;
    /// <summary>中文含义：显示名称。玩家界面显示名称，也用于配方列表的同类别名称排序。</summary>
    [Tooltip("显示名称")]
    public string displayName;
    /// <summary>中文含义：物品图标。背包、检视及无模型掉落标识使用的图标切片。</summary>
    [Tooltip("物品图标")]
    public Sprite icon;
    /// <summary>中文含义：物品类型。使用、合成排序及界面分类共享的物品种类。</summary>
    [Tooltip("物品类型")]
    public ItemType itemType;
    /// <summary>中文含义：物品在网格中占用的宽（格）。未旋转时的背包占位宽度，实际布局由实例旋转决定。</summary>
    [Header("网格尺寸")]
    [Tooltip("物品在网格中占用的宽（格）")]
    public int gridWidth = 1;
    /// <summary>中文含义：物品在网格中占用的高（格）。未旋转时的背包占位高度。</summary>
    [Tooltip("物品在网格中占用的高（格）")]
    public int gridHeight = 1;
    /// <summary>中文含义：最大堆叠数。单实例堆叠上限，新增及跨容器规划按至少一处理。</summary>
    [Header("堆叠")]
    [Tooltip("最大堆叠数")]
    public int maxStack = 99;
    /// <summary>中文含义：治疗量（0=非治疗品）。治疗品每次使用的恢复数值，由物品使用服务交给角色生命适配器。</summary>
    [Header("治疗效果")]
    [Tooltip("治疗量（0=非治疗品）")]
    public float healAmount = 0f;
    /// <summary>中文含义：使用后永久增加的生命上限（0=不增加）。治疗品使用时增加的生命上限，物品服务先提升上限再执行治疗。</summary>
    [Tooltip("使用后永久增加的生命上限（0=不增加）")]
    public float maxHealthBonus = 0f;
    /// <summary>中文含义：弹药对应武器类型。弹药物品对应的武器种类，物品效果或装填适配据此匹配。</summary>
    [Header("弹药补充")]
    [Tooltip("弹药对应武器类型")]
    public WeaponType ammoWeapon = WeaponType.Pistol;
    /// <summary>中文含义：补充弹药数。单单位弹药物品转换的备弹数量，使用服务结合堆叠数计算。</summary>
    [Tooltip("补充弹药数")]
    public int ammoAmount = 0;
    /// <summary>中文含义：武器类型（仅当 itemType=Weapon 时有效）。武器物品对应的装备种类，用于背包持有与装备映射。</summary>
    [Header("武器")]
    [Tooltip("武器类型（仅当 itemType=Weapon 时有效）")]
    public WeaponType weaponType = WeaponType.Pistol;
    /// <summary>中文含义：该物品是否拥有独立的耐久度。是否为每个实例保存独立耐久，启用后不能合并耐久不同的堆叠。</summary>
    [Header("耐久度")]
    [Tooltip("该物品是否拥有独立的耐久度")]
    public bool usesDurability;
    /// <summary>中文含义：每个物品实例的最大耐久度。实例初始化和归一化显示使用的耐久上限，实际剩余耐久归实例。</summary>
    [Tooltip("每个物品实例的最大耐久度")]
    [Min(1)]
    public int maxDurability = 100;
    /// <summary>中文含义：耐久耗尽后替换成的物品标识。耐久归零时在定义目录查询的替代物品标识，保留原实例布局。</summary>
    [Tooltip("耐久耗尽后替换成的物品标识")]
    public string brokenReplacementItemId;
    /// <summary>中文含义：世界空间拾取物的3D模型预制体（为空时使用占位几何体）。世界拾取模型预制件；掉落宿主未配置时使用既有占位表现。</summary>
    [Header("世界模型")]
    [Tooltip("世界空间拾取物的3D模型预制体（为空时使用占位几何体）")]
    public GameObject worldModelPrefab;
    /// <summary>中文含义：模型缩放（相对于拾取物）。相对拾取物根节点的模型缩放配置。</summary>
    [Tooltip("模型缩放（相对于拾取物）")]
    public Vector3 modelScale = Vector3.one;
    /// <summary>中文含义：模型位置偏移（相对于拾取物中心）。相对拾取物中心的模型位置偏移。</summary>
    [Tooltip("模型位置偏移（相对于拾取物中心）")]
    public Vector3 modelOffset = Vector3.zero;
    /// <summary>中文含义：模型旋转偏移（欧拉角）。世界拾取模型的欧拉角偏移。</summary>
    [Tooltip("模型旋转偏移（欧拉角）")]
    public Vector3 modelRotation = Vector3.zero;
    /// <summary>中文含义：留空时尝试复用世界模型预制体。首次获得检视专用模型，GetInspectionModelPrefab 优先返回此引用。</summary>
    [Header("首次获得检视")]
    [Tooltip("首次获得界面使用的三维模型；留空时尝试复用世界模型预制体")]
    public GameObject inspectionModelPrefab;
    /// <summary>中文含义：检视模型的初始旋转（欧拉角）。检视模型的初始欧拉角配置。</summary>
    [Tooltip("检视模型的初始旋转（欧拉角）")]
    public Vector3 inspectionModelRotation = Vector3.zero;
    /// <summary>中文含义：模型自动居中后的构图位置微调。检视自动居中后的构图位置修正。</summary>
    [Tooltip("模型自动居中后的构图位置微调")]
    public Vector3 inspectionModelOffset = Vector3.zero;
    /// <summary>中文含义：模型在检视画面中的占比修正，1 为自动构图结果。检视自动构图后的画面占比修正，供检视宿主读取。</summary>
    [Range(0.5f, 1.5f)]
    [Tooltip("模型在检视画面中的占比修正，1 为自动构图结果")]
    public float inspectionViewScale = 1f;
    /// <summary>中文含义：图标旋转。背包图标基础角度，与实例四分之一圈旋转共同决定显示方向。</summary>
    [Header("背包图标表现")]
    [Tooltip("图标基础旋转角度（欧拉Z轴），与 GridItem.rotation × 90° 叠加")]
    public float iconRotation = 0f;
    /// <summary>中文含义：图标缩放倍数（1=原始大小）。图标显示缩放倍率。</summary>
    [Tooltip("图标缩放倍数（1=原始大小）")]
    public float iconScale = 1f;
    /// <summary>中文含义：图标位置偏移（像素，相对于占位区域中心）。图标相对占位区域中心的像素偏移。</summary>
    [Tooltip("图标位置偏移（像素，相对于占位区域中心）")]
    public Vector2 iconOffset = Vector2.zero;
    /// <summary>中文含义：图标内边距（像素，向内收缩可见区域）。图标可见区域向内收缩的像素间距。</summary>
    [Tooltip("图标内边距（像素，向内收缩可见区域）")]
    public float iconPadding = 0f;
    /// <summary>中文含义：配置后优先于下方的兼容配方。新版数量配方材料数组，配置非空时优先于兼容双材料字段。</summary>
    [Header("合成配方")]
    [Tooltip("新版数量配方；配置后优先于下方的兼容配方")]
    public CraftingIngredientRequirement[] craftingIngredients;
    /// <summary>中文含义：单次合成获得的产物数量。单次合成产物数量，规则按至少一份处理。</summary>
    [Min(1)]
    [Tooltip("单次合成获得的产物数量")]
    public int craftingResultCount = 1;
    /// <summary>中文含义：是否草药拖放配方可关闭此项。是否将该产物加入合成分页查询，关闭仍可供既有拖放配方匹配。</summary>
    [Tooltip("是否在合成分页中显示；草药拖放配方可关闭此项")]
    public bool showInCraftingMenu = true;
    /// <summary>中文含义：兼容已有草药数据：合成该物品所需的第一种材料标识。兼容双材料配方的第一材料标识，新版配方存在时不使用。</summary>
    [Tooltip("兼容已有草药数据：合成该物品所需的第一种材料标识")]
    public string craftIngredientA;
    /// <summary>中文含义：兼容已有草药数据：合成该物品所需的第二种材料标识。兼容双材料配方的第二材料标识，新版配方存在时不使用。</summary>
    [Tooltip("兼容已有草药数据：合成该物品所需的第二种材料标识")]
    public string craftIngredientB;
    /// <summary>中文含义：描述。背包与检视显示的物品说明文本，不作为规则判断键。</summary>
    [Header("描述")]
    [TextArea]
    public string description;
    /// <summary>首次获得检视宿主查询可用模型，优先专用检视预制件，未配置时返回世界拾取模型；不实例化或改变资产。</summary>
    public GameObject GetInspectionModelPrefab()
    {
        if (inspectionModelPrefab != null)
        {
            return inspectionModelPrefab;
        }

        return worldModelPrefab;
    }
}
