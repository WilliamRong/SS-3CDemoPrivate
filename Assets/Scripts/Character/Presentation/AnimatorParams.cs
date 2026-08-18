using UnityEngine;

namespace Character.Presentation
{
    /// <summary>
    /// 集中保存 Animator 协议名和固定层索引，避免字符串拼写与 Layer 约定分散在各 Presenter 中。
    /// </summary>
    public static class AnimatorParams
    {
        public static readonly int VelocityX = Animator.StringToHash("VelocityX");
        public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");

        public const int LocomotionLayerIndex = 0;
        public const int UpperBodyLayerIndex = 1;
        public const int CombatLayerIndex = 2;
        public const int ReactionLayerIndex = 3;

        // ============ 基础移动 ============

        public static readonly int StateIdle = Animator.StringToHash("Idle");
        public static readonly int StateLocomotion = Animator.StringToHash("Locomotion");
        public static readonly int StateGuardWalk = Animator.StringToHash("GuardWalk");

        public static readonly int StateIdleTurnLeft = Animator.StringToHash("IdleTurnLeft");
        public static readonly int StateIdleTurnRight = Animator.StringToHash("IdleTurnRight");
        public static readonly int TurnSpeed = Animator.StringToHash("TurnSpeed");

        // ============ 冲刺 ============

        public static readonly int StateSprintStart = Animator.StringToHash("SprintStart");
        public static readonly int StateSprintLoop = Animator.StringToHash("SprintLoop");
        public static readonly int StateSprintBrake = Animator.StringToHash("SprintBrake");
        public static readonly int StateSprintTurn180 = Animator.StringToHash("SprintTurn180");

        // ============ 受击与死亡 ============

        public static readonly int StateHit = Animator.StringToHash("Hit");
        public static readonly int StateHit1 = Animator.StringToHash("Hit1");
        public static readonly int StateHit2 = Animator.StringToHash("Hit2");
        public static readonly int StateHit3 = Animator.StringToHash("Hit3");
        public static readonly int StateHit4 = Animator.StringToHash("Hit4");
        public static readonly int StateHit5 = Animator.StringToHash("Hit5");
        public static readonly int StateDeath = Animator.StringToHash("Death");
        public static readonly int StateExecuting = Animator.StringToHash("Executing");
        public static readonly int StateExecuted = Animator.StringToHash("Executed");
        public static readonly int StateExecutedDeath = Animator.StringToHash("ExecutedDeath");

        // ============ 闪避 ============

        public static readonly int DodgeInputX = Animator.StringToHash("DodgeInputX");
        public static readonly int DodgeInputZ = Animator.StringToHash("DodgeInputZ");

        public static readonly int StateDodgeBackStep = Animator.StringToHash("BackStep");
        public static readonly int StateDodgeNormal = Animator.StringToHash("NormalDodge");
        public static readonly int StateDodgeDirectional = Animator.StringToHash("DirectionalDodge");

        // ============ 格挡与架势 ============

        // GuardLoop 在 UpperBody 与 Combat 层保持同名，移动和静止格挡才能复用同一映射。
        public static readonly int StateGuardStart = Animator.StringToHash("GuardStart");
        public static readonly int StateGuardLoop = Animator.StringToHash("GuardLoop");
        public static readonly int StateGuardExit = Animator.StringToHash("GuardExit");
        public static readonly int StateGuardHit1 = Animator.StringToHash("GuardHit1");
        public static readonly int StateGuardHit2 = Animator.StringToHash("GuardHit2");
        public static readonly int StateGuardHit3 = Animator.StringToHash("GuardHit3");
        public static readonly int StateGuardBreak = Animator.StringToHash("GuardBreak");
        public static readonly int StatePostureBroken = Animator.StringToHash("PostureBroken");
        public static readonly int StateParry = Animator.StringToHash("Parry");
        public static readonly int StateParried = Animator.StringToHash("Parried");
        public static readonly int StateGuardTurnLeft = Animator.StringToHash("GuardTurnLeft");
        public static readonly int StateGuardTurnRight = Animator.StringToHash("GuardTurnRight");

        // ============ 攻击 ============

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
