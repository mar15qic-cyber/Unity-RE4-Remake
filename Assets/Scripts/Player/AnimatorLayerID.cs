using UnityEngine;

/// <summary>
/// 当前玩家动画控制器的层索引兼容常量，由动画适配器及相关动作组件传给 Animator 层查询和播放接口。
/// 数值必须与实际控制器层顺序一致，本次只补用途说明，不调整层、资源或动画绑定。
/// </summary>
public static class AnimatorLayerID
{
    /// <summary>中文含义：基础。基础移动层，索引零。</summary>
    public const int Base = 0;
    /// <summary>中文含义：受伤。受伤移动层，索引一。</summary>
    public const int Injured = 1;
    /// <summary>中文含义：受伤下半身。受伤下身层，同步受伤移动，仅遮罩双腿和脚部 IK，索引二。</summary>
    public const int InjuredLowerBody = 2;
    /// <summary>中文含义：步枪蹲伏。步枪蹲伏适配层，索引三。</summary>
    public const int RifleCrouch = 3;
    /// <summary>中文含义：瞄准。瞄准表现层，索引四。</summary>
    public const int Aim = 4;
    /// <summary>中文含义：开火。开火表现层，索引五。</summary>
    public const int Fire = 5;
    /// <summary>中文含义：武器动作。武器动作事务表现层，索引六，具体播放由已有动画适配器执行。</summary>
    public const int WeaponAction = 6;
    /// <summary>中文含义：全身动作。全身动作事务表现层，索引七，具体播放遵守动作协调器审批结果。</summary>
    public const int FullBodyAction = 7;
}
