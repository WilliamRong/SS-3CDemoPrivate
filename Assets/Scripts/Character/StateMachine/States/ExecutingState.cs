using Character.Config;
using Character.Execution;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{

    public sealed class ExecutingState : ICharacterState
    {
        private readonly CharacterMotor _motor;
        private readonly float _duration;

        private ExecutionSession _session;
        private bool _hasSession;
        private bool _isActive;

        public CharacterStateId Id => CharacterStateId.Executing;
        public ExecutionSession Session => _session;
        public float ElapsedTime { get; private set; }

        public bool HasReachedDuration =>
                    _isActive && ElapsedTime >= _duration;


        public ExecutingState(
                  CharacterMotor motor,
                  CharacterCombatConfig combat)
        {
            _motor = motor;
            _duration = Mathf.Max(
                0.01f,
                combat != null ? combat.executingDuration : 0.01f);
        }

        public bool TryPrepare(in ExecutionSession session,
            int ownerActorId)
        {
            if (!session.TryValidate(out _) || !session.IsActive || session.ExecutorActorId != ownerActorId || _isActive) return false;

            if (_hasSession) return _session.ExecutionId == session.ExecutionId;

            _session = session;
            _hasSession = true;
            return true;
        }

        public bool IsBoundTo(ulong executionId)
        {
            return _hasSession && _session.ExecutionId == executionId;
        }

        public void CancelPreparation(ulong executionId)
        {
            if (!_isActive && IsBoundTo(executionId)) ClearBinding();
        }

        private void ClearBinding()
        {
            _session = default;
            _hasSession = false;
        }

        public void Enter()
        {
            Debug.Assert(
               _hasSession,
               "ExecutingState entered without an execution session.");

            _isActive = true;
            ElapsedTime = 0f;

            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
            _motor.SetTurnRotationOverride(true);
            _motor.BeginAttackRootMotion();
        }

        public void Exit()
        {
            _motor.EndAttackRootMotion();
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);

            _isActive = false;
            ElapsedTime = 0f;
            ClearBinding();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (!_isActive) return;

            ElapsedTime += Mathf.Max(0f, deltaTime);

            _motor.Tick(default, deltaTime);
        }


    }
}