/// <summary>
/// 枪械部位倍率的纯查询规则。DamageResolutionModule.QueryBodyPartMultiplier 直接转发调用，
/// 返回倍率交给伤害结算链使用，本类型不扣血、不选择受击动画。
/// </summary>
public static class BodyPartDamageRules
{
    /// <summary>根据命中盒部位返回既有倍率；未单独配置的部位及未知值沿用基础倍率。</summary>
    /// <param name="bodyPart">中文含义：身体部位。命中识别链传入的部位枚举。</param>
    /// <returns>头部 2，四肢 0.8，其余 1。</returns>
    public static float QueryBodyPartMultiplier(HitboxType bodyPart)
    {
        if (bodyPart == HitboxType.Head)
        {
            return 2f;
        }

        // 左右手臂共用四肢倍率，不影响未知枚举值的默认处理。
        // 中文含义：isArm 为“是否手臂”。
bool isArm = bodyPart == HitboxType.LeftArm || bodyPart == HitboxType.RightArm;
        // 左右腿同样使用四肢倍率，由角色模块另行解释受击动作。
        // 中文含义：isLeg 为“是否腿部”。
bool isLeg = bodyPart == HitboxType.LeftLeg || bodyPart == HitboxType.RightLeg;
        if (isArm || isLeg)
        {
            return 0.8f;
        }

        return 1f;
    }
}
