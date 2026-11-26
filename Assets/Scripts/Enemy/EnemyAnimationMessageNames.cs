/// <summary>
/// 敌人动画状态行为使用的剩余消息名集中定义。
/// </summary>
public static class EnemyAnimationMessageNames
{
    /// <summary>攻击命中。编译期常量，保持既有值与消费者映射。</summary>
    public const string AttackHit = "OnAttackHit";
    /// <summary>攻击完成。编译期常量，保持既有值与消费者映射。</summary>
    public const string AttackComplete = "OnAttackComplete";
    /// <summary>招架窗口打开。编译期常量，保持既有值与消费者映射。</summary>
    public const string ParryWindowOpen = "OnParryWindowOpen";
    /// <summary>招架窗口关闭。编译期常量，保持既有值与消费者映射。</summary>
    public const string ParryWindowClose = "OnParryWindowClose";
    /// <summary>电锯攻击音频进入。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawAttackAudioEnter = "OnChainsawAttackAudioEnter";
    /// <summary>电锯命中音频进入。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawHitAudioEnter = "OnChainsawHitAudioEnter";
    /// <summary>电锯动作音频退出。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawActionAudioExit = "OnChainsawActionAudioExit";
    /// <summary>电锯起始完成。编译期常量，保持既有值与消费者映射。</summary>
    public const string ChainsawStartComplete = "OnChainsawStartComplete";
}
