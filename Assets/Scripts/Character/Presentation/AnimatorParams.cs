using UnityEngine;

namespace Character.Presentation
{
    public static class AnimatorParams
    {
        public static readonly int VelocityX = Animator.StringToHash("VelocityX");
        public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");

        
        //Locomotion相关
        public const int LocomotionLayerIndex = 0;

        public static readonly int StateIdle = Animator.StringToHash("Idle");
        public static readonly int StateLocomotion = Animator.StringToHash("Locomotion");

        public const float LocomotionCrossFadeDuration = 0.15f;

        /// <summary>Idle → Locomotion: shorter blend avoids idle2 flash in run tree.</summary>
        public const float LocomotionEnterCrossFadeDuration = 0.08f;

        /// <summary>
        /// World speed that maps to full run on the blend tree forward axis (<see cref="RunForwardBlendZ"/>).
        /// Align with <see cref="Character.Controller.PlayerController.MoveSpeed"/> (default 5).
        /// </summary>
        public const float LocomotionBlendReferenceSpeed = 5f;
        
        
        //冲刺相关
        public static readonly int StateSprintStart = Animator.StringToHash("SprintStart");
        public static readonly int StateSprintLoop = Animator.StringToHash("SprintLoop");
        public static readonly int StateSprintBrake = Animator.StringToHash("SprintBrake");
        public static readonly int StateSprintTurn180 =  Animator.StringToHash("SprintTurn180");
        
        public const float SprintCrossFadeDuration = 0.15f;
        

        public const float FreeMoveBlendScale = 1f;

        /// <summary>2D blend tree axis extent (run strafe / back at ±2).</summary>
        public const float BlendAxisMax = 2f;

        /// <summary>rig_Run_Forward_IPC — center of free-move locomotion (no walk in tree).</summary>
        public const float RunForwardBlendZ = 2f;
    }
}
