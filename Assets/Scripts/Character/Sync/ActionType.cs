namespace Character.Sync
{
    /// <summary>
    /// 显式编号属于网络协议，新增动作只能追加，不能重排已有值。
    /// </summary>
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
        ParryStart = 10,
        Parried = 11,
    }

    /// <summary>
    /// 调试字符串与协议枚举集中维护，避免日志遗漏新动作时误判网络数据。
    /// </summary>
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
                ActionType.ParryStart => "ParryStart",
                ActionType.Parried => "Parried",
                _ => $"Unknown({(byte)type})"
            };
        }
    }
}
