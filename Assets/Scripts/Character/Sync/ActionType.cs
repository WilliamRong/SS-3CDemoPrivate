namespace Character.Sync
{
    public enum ActionType : byte
    {
        None = 0,
        AttackStart = 1,
        DodgeStart = 2,
        Hit = 3,
        Dead = 4,
        Revive = 5
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
                _ => $"Unknown({(byte)type})"
            };
        }
    }
}
