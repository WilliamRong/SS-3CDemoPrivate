namespace Character.StateMachine
{
    /// <summary>
    /// 枚举值会进入快照和序列化资产，因此只允许追加，不能重排既有数值。
    /// </summary>
    public enum CharacterStateId : byte
    {
        None = 0,
        Idle = 1,
        Move = 2,
        Sprint = 3,
        Attack = 4,
        Dodge = 5,
        Hit = 6,
        Dead = 7,
        Guard = 8,
        PostureBroken = 9,
    }
}
