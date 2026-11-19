namespace GameArchitecture
{
    public readonly struct EnemyAttackWarningSnapshot
    {
        public readonly ActionHandle Action;
        public readonly double OpensAt;
        public readonly double ExpiresAt;
        public readonly bool CanDefend;

        public EnemyAttackWarningSnapshot(ActionHandle action, double opensAt, double expiresAt, bool canDefend)
        {
            Action = action;
            OpensAt = opensAt;
            ExpiresAt = expiresAt;
            CanDefend = canDefend;
        }

        public bool IsCurrent(ActionHandle current, double now)
        {
            return Action.Valid && Action == current && now >= OpensAt && now <= ExpiresAt;
        }
    }
}
