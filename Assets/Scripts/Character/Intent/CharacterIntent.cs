using UnityEngine;

namespace Character.Intent
{
    /// <summary>
    /// 以逐帧值对象隔离输入来源与状态机，使 Player 输入和 NPC AI 可以复用相同状态接口。
    /// </summary>
    public struct CharacterIntent
    {
        public Vector2 Move;
        public bool IsSprintHeld;
        public bool IsJumpPressed;
        public bool IsAttackPressed;
        public bool IsDodgePressed;
        public bool IsGuardHeld;
        public bool IsParryPressed;
    }
}
