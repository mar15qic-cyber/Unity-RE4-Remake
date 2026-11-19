namespace GameArchitecture
{
    /// <summary>濒死与复起不属于终局死亡，任务系统只统计 Dead。</summary>
    public enum EnemyLifePhase
    {
        Alive = 0,
        Dying = 1,
        Reviving = 2,
        Revived = 3,
        Dead = 4
    }
}
