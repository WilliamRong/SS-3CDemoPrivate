namespace Character.Sync
{
    public enum ActionType : byte
    {
        None = 0,
        AttackStart = 1,
        DodgeStart = 2,
        Hit = 3,
        Dead = 4,
        Revive = 5,
        GuardHit = 6,
        GuardBreak = 7,
        PostureBreak = 8,
        HealthResult = 9,
    }

    public static class ActionTypeExtensions
    {
        public static string ToDebugString(this ActionType type)
        {
            return type switch
            {
                ActionType.None => "None",
                ActionType.AttackStart => "AttackStart",
                ActionType.DodgeStart => "DodgeStart",
                ActionType.Hit => "Hit",
                ActionType.Dead => "Dead",
                ActionType.Revive => "Revive",
                ActionType.GuardHit => "GuardHit",
                ActionType.GuardBreak => "GuardBreak",
                ActionType.PostureBreak => "PostureBreak",
                ActionType.HealthResult => "HealthResult",
                _ => $"Unknown({(byte)type})"
            };
        }
    }
}
