using UnityEngine;

/// <summary>
/// 已提交的命中表现事实，携带目标对象、命中部位、结算伤害和击杀标记。
/// DamageResolutionModule.CommitEnemyHit 在目标扣血后构造，由 CombatEventDispatcher.PublishHit 广播给表现订阅者。
/// 目标引用用于识别命中对象；音频、粒子、界面和动画表现仍由订阅者负责。
/// </summary>
public struct HitResult
{
    /// <summary>被命中的目标对象。</summary>
    public GameObject target;
    /// <summary>命中点世界坐标。</summary>
    public Vector3 hitPoint;
    /// <summary>命中表面法线。</summary>
    public Vector3 hitNormal;
    public Vector3 bloodDirection;
    /// <summary>命中部位类型。</summary>
    public HitboxType hitboxType;
    /// <summary>基础伤害乘部位倍率后的结算值；实际扣血见 DamageResult。</summary>
    public float damage;
    /// <summary>伤害提交后目标生命是否已归零。</summary>
    public bool isKill;
    /// <summary>是否命中头部。</summary>
    public bool isHeadshot;
    // 伤害来源已在自身表现入口负责接触声。
    public bool impactSoundHandled;
    public ChapterOneEffect bloodEffect;
    public bool deferBloodPresentation;
    /// <summary>由伤害结算创建命中事实，供战斗事件分发器及其表现订阅者使用。</summary>
    /// <param name="target">目标游戏对象。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="hitNormal">命中表面法线。</param>
    /// <param name="hitboxType">命中盒类型。</param>
    /// <param name="damage">伤害。</param>
    /// <param name="isKill">是否击杀。</param>
    /// <param name="isHeadshot">是否爆头。</param>
    public HitResult(GameObject target, Vector3 hitPoint, Vector3 hitNormal, HitboxType hitboxType, float damage, bool isKill, bool isHeadshot, bool impactSoundHandled = false, ChapterOneEffect bloodEffect = ChapterOneEffect.None, bool deferBloodPresentation = false)
    {
        this.target = target;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.bloodDirection = hitNormal;
        this.hitboxType = hitboxType;
        this.damage = damage;
        this.isKill = isKill;
        this.isHeadshot = isHeadshot;
        this.impactSoundHandled = impactSoundHandled;
        this.bloodEffect = bloodEffect;
        this.deferBloodPresentation = deferBloodPresentation;
    }
}
