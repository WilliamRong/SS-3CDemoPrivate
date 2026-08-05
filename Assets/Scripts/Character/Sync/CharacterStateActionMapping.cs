using Character.StateMachine;

namespace Character.Sync
{
    /// <summary>
    /// Player 与 NPC 共用同一状态到动作映射，避免两个发布器对协议事件的选择产生分歧。
    /// </summary>
    public static class CharacterStateActionMapping
    {
        public static ActionType MapStateToActionType(CharacterStateId stateId)
        {
            return stateId switch
            {
                CharacterStateId.Attack => ActionType.AttackStart,
                CharacterStateId.Dodge => ActionType.DodgeStart,
                CharacterStateId.Hit => ActionType.Hit,
                CharacterStateId.Dead => ActionType.Dead,
                CharacterStateId.PostureBroken => ActionType.PostureBreak,
                CharacterStateId.Parry => ActionType.ParryStart,
                CharacterStateId.Parried => ActionType.Parried,
                _ => ActionType.None
            };
        }
    }
}
