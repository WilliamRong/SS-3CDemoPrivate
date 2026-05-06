using Character.StateMachine;

namespace Character.Sync
{
    /// <summary>
    /// 玩家 LocalSyncPublisher 与 NPC NpcAuthoritySyncPublisher 共用的状态→动作事件映射。
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
                _ => ActionType.None
            };
        }
    }
}
