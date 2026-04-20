using UnityEngine;

namespace Character.Intent
{
   public struct CharacterIntent
   {
      public Vector2 Move;//移动
      public bool IsSprintHeld;//是否按住冲刺
      public bool IsJumpPressed;//本帧触发，边沿触发
      public bool IsAttackPressed;//本帧触发
      public bool IsDodgePressed;//本帧触发
   }
}
