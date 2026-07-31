namespace Character.StateMachine
{
    /// <summary>
    /// 使用稳定 byte 值表达逻辑窗口，便于调试和未来协议字段保持兼容。
    /// </summary>
    public enum StateWindowType : byte
    {
        Always = 0,
        PreHitWindow = 1,
        ActiveWindow = 2,
        RecoveryWindow = 3,
    }

    /// <summary>
    /// 将转换来源显式化，使同一目标状态可以按输入、受击或超时采用不同打断规则。
    /// </summary>
    public enum TransitionReason : byte
    {
        Any = 0,
        InputMove = 1,
        InputSprint = 2,
        InputAttack = 3,
        InputDodge = 4,
        HitLight = 5,
        HitHeavy = 6,
        Death = 7,
        Revive = 8,
        Timeout = 9,
        InputGuard = 10,
        PostureBreak = 11,
    }


    /// <summary>
    /// 用不可变值描述一条打断覆盖，避免运行时修改共享规则表。
    /// </summary>
    public readonly struct CharacterInterruptRule
    {
        public readonly CharacterStateId FromState;
        public readonly CharacterStateId IncomingState;
        public readonly StateWindowType WindowType;
        public readonly TransitionReason Reason;
        public readonly bool IsAllowed;

        public CharacterInterruptRule(CharacterStateId fromState, CharacterStateId incomingState, StateWindowType windowType, TransitionReason reason, bool isAllowed)
        {
            FromState = fromState;
            IncomingState = incomingState;
            WindowType = windowType;
            Reason = reason;
            IsAllowed = isAllowed;
        }
    }
}
