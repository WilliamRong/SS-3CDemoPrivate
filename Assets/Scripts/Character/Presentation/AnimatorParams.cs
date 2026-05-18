using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// Animator parameter hashes and layer indices. Tuning values live in <see cref="Character.Config.CharacterPresentationConfig"/>.
    /// </summary>
    public static class AnimatorParams
    {
        public static readonly int VelocityX = Animator.StringToHash("VelocityX");
        public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");
        
        public const int LocomotionLayerIndex = 0;
        public const int UpperBodyLayerIndex = 1;
        public const int HitLayerIndex = 2;
        public const int DeathLayerIndex = 3;


        public static readonly int StateIdle = Animator.StringToHash("Idle");
        public static readonly int StateLocomotion = Animator.StringToHash("Locomotion");

        public static readonly int StateSprintStart = Animator.StringToHash("SprintStart");
        public static readonly int StateSprintLoop = Animator.StringToHash("SprintLoop");
        public static readonly int StateSprintBrake = Animator.StringToHash("SprintBrake");
        public static readonly int StateSprintTurn180 = Animator.StringToHash("SprintTurn180");
        
        public static readonly int StateHit =  Animator.StringToHash("Hit");
        public static readonly int StateDeath = Animator.StringToHash("Death");
    }
}
