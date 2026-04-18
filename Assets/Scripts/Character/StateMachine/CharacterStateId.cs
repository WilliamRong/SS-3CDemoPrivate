namespace Character.StateMachine
{
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
    }
}