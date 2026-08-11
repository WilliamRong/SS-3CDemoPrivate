using Character.Config;
using Character.Execution;
using Character.Intent;
using Character.StateMachine;
using UnityEngine;

namespace AI.NpcStates
{
    public sealed class NpcExecutedState : ICharacterState
    {
        private readonly NpcMotor _motor;
        private readonly float _duration;

        private ExecutionSession _session;
        private bool _hasSession;
        private bool _isActive;

        public CharacterStateId Id => CharacterStateId.Executed;
        public ExecutionSession Session => _session;
        public float ElapsedTime { get; private set; }

        public bool HasReachedDuration =>
            _isActive && ElapsedTime >= _duration;

        public NpcExecutedState(
            NpcMotor motor,
            CharacterCombatConfig combat)
        {
            _motor = motor;
            _duration = Mathf.Max(
                0.01f,
                combat != null ? combat.executedDuration : 0.01f);
        }

        public bool TryPrepare(
            in ExecutionSession session,
            int ownerActorId)
        {
            if (!session.TryValidate(out _) ||
                !session.IsActive ||
                session.TargetActorId != ownerActorId ||
                _isActive)
            {
                return false;
            }

            if (_hasSession)
                return _session.ExecutionId == session.ExecutionId;

            _session = session;
            _hasSession = true;
            return true;
        }

        public bool IsBoundTo(ulong executionId)
        {
            return _hasSession &&
                   _session.ExecutionId == executionId;
        }

        public void CancelPreparation(ulong executionId)
        {
            if (!_isActive && IsBoundTo(executionId))
                ClearBinding();
        }

        public void Enter()
        {
            Debug.Assert(
                _hasSession,
                "NpcExecutedState entered without an execution session.");

            _isActive = true;
            ElapsedTime = 0f;

            _motor?.Stop();
            _motor?.ResetPath();
            ApplyFixedPose();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (!_isActive)
                return;

            ElapsedTime += Mathf.Max(0f, deltaTime);
            ApplyFixedPose();
        }

        public void Exit()
        {
            _isActive = false;
            ElapsedTime = 0f;
            ClearBinding();
        }

        private void ApplyFixedPose()
        {
            if (!_hasSession || _motor == null)
                return;

            ExecutionPose pose = _session.FixedTargetPose;
            _motor.HoldPose(
                pose.Position,
                Quaternion.Euler(0f, pose.Yaw, 0f));
        }

        private void ClearBinding()
        {
            _session = default;
            _hasSession = false;
        }
    }
}