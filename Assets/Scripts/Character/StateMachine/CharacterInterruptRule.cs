namespace Character.StateMachine
{
    public enum StateWindowType  : byte
    {
        Always = 0,
        PreHitWindow = 1,
        ActiveWindow = 2,
        RecoveryWindow = 3,
    }

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
    }


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
