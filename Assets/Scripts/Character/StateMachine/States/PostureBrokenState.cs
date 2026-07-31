using Character.Config;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{
    /// <summary>
    /// 在配置时长内阻断玩家移动和普通输入，退出条件只由权威超时或死亡转换决定。
    /// </summary>
    public sealed class PostureBrokenState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly CharacterMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private float _timer;

        public CharacterStateId Id => CharacterStateId.PostureBroken;

        public PostureBrokenState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            CharacterMotor motor,
            CharacterCombatConfig combat)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combat = combat;
        }

        public void Enter()
        {
            _timer = 0f;
            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
        }

        /// <summary>
        /// 崩防期间仍推进 Motor 的垂直链路但封锁水平移动，结束后再按当前输入选择 Idle 或 Move。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            _timer += deltaTime;
            _motor.Tick(intent, deltaTime);

            if (_timer < Mathf.Max(0.01f, _combat.postureBreakDuration))
                return;

            CharacterStateId target = intent.Move.sqrMagnitude > 0.0001f
                ? CharacterStateId.Move
                : CharacterStateId.Idle;

            _fsm.TryTransition(target, _registry, TransitionReason.Timeout);
        }

        public void Exit()
        {
            _timer = 0f;
            _motor.SetMovementBlocked(false);
        }
    }
}
