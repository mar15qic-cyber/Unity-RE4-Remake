using UnityEngine;

/// <summary>
/// 准心渲染类型 — 决定使用哪种 ICrosshairRenderer 策略
/// </summary>
public enum CrosshairType
{
    /// <summary>中文含义：跨越。</summary>
    Cross, // 四线段十字 (Handgun / Rifle)
    /// <summary>中文含义：点积。</summary>
    Dot, // 单点 (未来精密武器)
    /// <summary>中文含义：圆形。</summary>
    Circle, // 圆形扩散圈 (Shotgun)
    /// <summary>中文含义：激光。</summary>
    Laser, // 激光线 (未来特殊武器)
    /// <summary>中文含义：无（未选择或尚未发生）。</summary>
    None // 无准心 (Knife)
}

/// <summary>
/// 命中类型 — 驱动准心命中反馈颜色
/// </summary>
public enum HitType
{
    /// <summary>中文含义：普通状态。</summary>
    Normal, // 命中身体
    /// <summary>中文含义：爆头。</summary>
    Headshot, // 命中头部
    /// <summary>中文含义：弱点。</summary>
    Weakspot, // 命中弱点
    /// <summary>中文含义：击杀。</summary>
    Kill // 击杀
}

/// <summary>
/// 准心显示阶段 — 控制可见性和行为模式
/// </summary>
public enum CrosshairPhase
{
    /// <summary>中文含义：已隐藏。</summary>
    Hidden, // 完全隐藏
    /// <summary>中文含义：瞄准。</summary>
    Aim, // 瞄准模式
    /// <summary>中文含义：髋部开火。</summary>
    HipFire, // 腰射模式
    /// <summary>中文含义：换弹。</summary>
    Reload, // 换弹中
    /// <summary>中文含义：切换中。</summary>
    Switching // 武器切换中
}

/// <summary>
/// 准心生命周期事件 — 由游戏系统层通过事件推送
/// </summary>
public enum CrosshairLifecycleEvent
{
    /// <summary>中文含义：暂停已进入。</summary>
    PauseEntered,
    /// <summary>中文含义：暂停已退出。</summary>
    PauseExited,
    /// <summary>中文含义：死亡。</summary>
    Death,
    /// <summary>中文含义：重生。</summary>
    Respawn,
    /// <summary>中文含义：背包已打开。</summary>
    InventoryOpened,
    /// <summary>中文含义：背包已关闭。</summary>
    InventoryClosed,
    /// <summary>中文含义：过场动画已开始。</summary>
    CutsceneStarted,
    /// <summary>中文含义：过场动画已结束。</summary>
    CutsceneEnded
}

/// <summary>
/// 命中信息 — 通过 ICrosshairDataProvider.OnHit 事件传递
/// </summary>
public struct HitInfo
{
    /// <summary>当前命中类型。</summary>
    public HitType type;
    /// <summary>命中点位置。</summary>
    public Vector3 hitPoint;
    /// <summary>伤害。</summary>
    public float damage;
    /// <summary>构造并初始化 HitInfo 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.ProcessHit(RaycastHit, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中类型。</param>
    /// <param name="hitPoint">命中点位置。</param>
    /// <param name="damage">伤害。</param>
    public HitInfo(HitType type, Vector3 hitPoint, float damage)
    {
        this.type = type;
        this.hitPoint = hitPoint;
        this.damage = damage;
    }
}
