using System;

namespace GameArchitecture
{
    /// <summary>仅计算复起资格和状态；生命、时钟、随机数由宿主提供。</summary>
    public sealed class EnemyRevivalRules
    {
        public EnemyLifePhase Phase { get; private set; }
        public double Deadline { get; private set; }
        public long Revision { get; private set; }

        public static bool ShouldRevive(bool eligible, float probabilityPercent, float randomUnit)
        {
            if (!eligible || float.IsNaN(randomUnit) || randomUnit < 0f || randomUnit >= 1f)
            {
                return false;
            }
            return randomUnit * 100f < Math.Max(0f, Math.Min(100f, probabilityPercent));
        }

        public bool BeginDying(double deadline)
        {
            if (Phase != EnemyLifePhase.Alive || double.IsNaN(deadline) || double.IsInfinity(deadline))
            {
                return false;
            }
            Deadline = deadline;
            Phase = EnemyLifePhase.Dying;
            Revision++;
            return true;
        }

        public bool BeginReviving(long revision, double now)
        {
            if (revision != Revision || Phase != EnemyLifePhase.Dying || double.IsNaN(now)
                || double.IsInfinity(now) || now < Deadline)
            {
                return false;
            }
            Phase = EnemyLifePhase.Reviving;
            return true;
        }

        public bool ScheduleWait(long revision, double deadline)
        {
            if (revision != Revision || Phase != EnemyLifePhase.Dying || Deadline != double.MaxValue
                || double.IsNaN(deadline) || double.IsInfinity(deadline))
            {
                return false;
            }
            Deadline = deadline;
            return true;
        }

        public bool CompleteRevival(long revision)
        {
            if (revision != Revision || Phase != EnemyLifePhase.Reviving)
            {
                return false;
            }
            Phase = EnemyLifePhase.Revived;
            return true;
        }

        public bool ConfirmDeath()
        {
            if (Phase == EnemyLifePhase.Dead)
            {
                return false;
            }
            Phase = EnemyLifePhase.Dead;
            Revision++;
            return true;
        }

        public void InvalidatePendingTransition()
        {
            Revision++;
        }
    }
}
