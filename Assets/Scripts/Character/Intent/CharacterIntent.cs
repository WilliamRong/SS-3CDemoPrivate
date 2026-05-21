using UnityEngine;

namespace Character.Intent
{
    /// <summary>
    /// Per-frame input intent consumed by the character FSM.
    /// Triggered flags are edge-triggered (true only on the frame they fire).
    /// </summary>
    public struct CharacterIntent
    {
        public Vector2 Move;
        public bool IsSprintHeld;
        public bool IsJumpPressed;
        public bool IsAttackPressed;
        public bool IsDodgePressed;
        public bool IsGuardHeld;
    }
}
