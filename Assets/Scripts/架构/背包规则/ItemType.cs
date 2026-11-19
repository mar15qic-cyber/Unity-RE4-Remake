/// <summary>物品定义、使用资格及界面分类共享的种类标识；保留数值和序列化资源兼容，具体效果由规则服务提交。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "ItemType")]
public enum ItemType
{
    /// <summary>中文含义：治疗。草药、急救喷雾等治疗品，可按定义恢复生命或提升上限。</summary>
    Healing,
    /// <summary>中文含义：弹药。对应武器弹药，使用和装填通过已有库存服务处理。</summary>
    Ammo,
    /// <summary>中文含义：键。关键道具，由任务或交互消费者解释用途。</summary>
    Key,
    /// <summary>中文含义：宝物。宝藏类物品，按既有物品和界面流程展示。</summary>
    Treasure,
    /// <summary>中文含义：武器。可持有武器，装备资格同时检查背包及装备映射。</summary>
    Weapon,
    /// <summary>中文含义：合成合成材料。合成材料，由合成规则消费，不作为普通治疗或弹药使用。</summary>
    CraftingIngredient
}
