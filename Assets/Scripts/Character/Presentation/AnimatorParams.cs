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
        public const int CombatLayerIndex = 2;
        public const int ReactionLayerIndex = 3;


        //locomotion
        public static readonly int StateIdle = Animator.StringToHash("Idle");
        public static readonly int StateLocomotion = Animator.StringToHash("Locomotion");
        public static readonly int StateGuardWalk = Animator.StringToHash("GuardWalk");

        //sprint
        public static readonly int StateSprintStart = Animator.StringToHash("SprintStart");
        public static readonly int StateSprintLoop = Animator.StringToHash("SprintLoop");
        public static readonly int StateSprintBrake = Animator.StringToHash("SprintBrake");
        public static readonly int StateSprintTurn180 = Animator.StringToHash("SprintTurn180");
        
        //reaction
        public static readonly int StateHit =  Animator.StringToHash("Hit");
        public static readonly int StateDeath = Animator.StringToHash("Death");
        
        //dodge
        public static readonly int DodgeInputX = Animator.StringToHash("DodgeInputX");
        public static readonly int DodgeInputZ = Animator.StringToHash("DodgeInputZ");

        public static readonly int StateDodgeBackStep = Animator.StringToHash("BackStep");
        public static readonly int StateDodgeNormal = Animator.StringToHash("NormalDodge");
        public static readonly int StateDodgeDirectional = Animator.StringToHash("DirectionalDodge");
        
        //Guard
        //GuardLoop exists on UpperBody and Combat layers with same state name.
        public static readonly int StateGuardStart = Animator.StringToHash("GuardStart");
        public static readonly int StateGuardLoop  = Animator.StringToHash("GuardLoop");
        public static readonly int StateGuardExit  = Animator.StringToHash("GuardExit");
        
        
        //attack
        public static readonly int AttackCombo1 = Animator.StringToHash("Attack_Combo1");
        public static readonly int AttackCombo2 = Animator.StringToHash("Attack_Combo2");
        public static readonly int AttackCombo3 = Animator.StringToHash("Attack_Combo3");
        public static readonly int AttackCombo4 = Animator.StringToHash("Attack_Combo4");
        public static readonly int AttackHeavy1 = Animator.StringToHash("Attack_Heavy1");
        public static readonly int AttackHeavy1Start = Animator.StringToHash("Attack_Heavy1_Start");
        public static readonly int AttackHeavy2 = Animator.StringToHash("Attack_Heavy2");
        public static readonly int AttackSprint = Animator.StringToHash("Attack_Sprint");
        public static readonly int AttackDodge = Animator.StringToHash("Attack_Dodge");
        public static readonly int AttackDodgeToCombo1 = Animator.StringToHash("Attack_Dodge_to_Combo1");
    }
}
