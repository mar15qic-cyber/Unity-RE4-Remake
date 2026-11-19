using UnityEngine;

/// <summary>
/// 已提交的命中表现事实，携带目标对象、命中部位、结算伤害和击杀标记。
/// DamageResolutionModule.CommitEnemyHit 在目标扣血后构造，由 CombatEventDispatcher.PublishHit 广播给表现订阅者。
/// 目标引用用于识别命中对象；音频、粒子、界面和动画表现仍由订阅者负责。
/// </summary>
public struct HitResult
{
    /// <summary>中文含义：目标游戏对象。被命中的目标 GameObject</summary>
    public GameObject target;
    /// <summary>中文含义：命中点位置。命中点世界坐标</summary>
    public Vector3 hitPoint;
    /// <summary>中文含义：命中表面法线。命中面法线</summary>
    public Vector3 hitNormal;
    public Vector3 bloodDirection;
    /// <summary>中文含义：命中盒类型。命中部位类型</summary>
    public HitboxType hitboxType;
    /// <summary>中文含义：伤害。基础伤害乘部位倍率得到的结算值；免疫、减伤后的实际扣血见 DamageResult。</summary>
    public float damage;
    /// <summary>中文含义：是否击杀。伤害提交后目标生命是否已归零；保留既有击杀显示标志。</summary>
    public bool isKill;
    /// <summary>中文含义：是否爆头。是否命中头部</summary>
    public bool isHeadshot;
    // 伤害来源已在自身表现入口负责接触声。
    public bool impactSoundHandled;
    public ChapterOneEffect bloodEffect;
    public bool deferBloodPresentation;
    /// <summary>构造并初始化 HitResult 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 DamageResolutionModule.CommitEnemyHit(VillagerController, HitboxType, float, Vector3, Vector3)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标游戏对象。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="hitPoint">中文含义：命中点位置。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="hitNormal">中文含义：命中表面法线。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="hitboxType">中文含义：命中盒类型。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="damage">中文含义：伤害。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="isKill">中文含义：是否击杀。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
    /// <param name="isHeadshot">中文含义：是否爆头。使用位置：HitResult.HitResult(GameObject, Vector3, Vector3, HitboxType, float, bool, bool)。</param>
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
