/// <summary>骨骼命中盒向伤害与受击链传递的部位标识；保持原枚举顺序与资源绑定，具体倍率由 BodyPartDamageRules 查询。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "HitboxType")]
public enum HitboxType
{
    /// <summary>中文含义：头部。头部，当前枪械规则倍率为 2。</summary>
    Head,
    /// <summary>中文含义：胸部。胸部，当前枪械规则使用基础倍率。</summary>
    Chest,
    /// <summary>中文含义：骨盆。骨盆，当前枪械规则使用基础倍率。</summary>
    Pelvis,
    /// <summary>中文含义：左侧手臂。左臂，当前枪械规则倍率为 0.8。</summary>
    LeftArm,
    /// <summary>中文含义：右侧手臂。右臂，当前枪械规则倍率为 0.8。</summary>
    RightArm,
    /// <summary>中文含义：左侧腿部。左腿，当前枪械规则倍率为 0.8；后续受击行为由角色模块决定。</summary>
    LeftLeg,
    /// <summary>中文含义：右侧腿部。右腿，当前枪械规则倍率为 0.8；后续受击行为由角色模块决定。</summary>
    RightLeg
}
