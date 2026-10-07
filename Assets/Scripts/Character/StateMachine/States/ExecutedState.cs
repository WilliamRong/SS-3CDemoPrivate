using Character.Config;
using Character.Execution;
using Character.Intent;
using Character.Motor;
using UnityEngine;

namespace Character.StateMachine.States
{
    public sealed class ExecutedState : ICharacterState
    {
        private readonly CharacterMotor _motor;
        private readonly CharacterCombatConfig _combat;
        private readonly float _survivingDuration;
        private readonly float _lethalDuration;

        private ExecutionSession _session;
        private bool _hasSession;
        private bool _isActive;

        public CharacterStateId Id => CharacterStateId.Executed;
        public ExecutionSession Session => _session;
        public ExecutionWarpContext WarpContext => _warpContext;
        public float ElapsedTime { get; private set; }

        private ExecutionWarpContext _warpContext;

        private float CurrentDuration =>
            _session.TargetWillDie
                ? _lethalDuration
                : _survivingDuration;

        public float NormalizedTime =>
            Mathf.Clamp01(ElapsedTime / CurrentDuration);

        public bool HasReachedDuration =>
            _isActive &&
            ElapsedTime >= (_session.TargetWillDie
                ? _lethalDuration
                : _survivingDuration);

        public ExecutedState(
            CharacterMotor motor,
            CharacterCombatConfig combat)
        {
            _motor = motor;
            _combat = combat;
            _survivingDuration = Mathf.Max(
                0.01f,
                combat != null ? combat.executedDuration : 0.01f);
            _lethalDuration = Mathf.Max(
                0.01f,
                combat != null ? combat.executedDeathDuration : 0.01f);
        }

        public bool TryPrepare(
            in ExecutionSession session,
            int ownerActorId)
        {
            if (!session.TryValidate(out _) ||
                !session.IsActive ||
                session.TargetActorId != ownerActorId ||
                _isActive ||
                _motor == null ||
                _motor.Root == null)
            {
                return false;
            }

            if (_hasSession)
            {
                return _session.ExecutionId == session.ExecutionId &&
                       _warpContext != null &&
                       _warpContext.IsActive;
            }

            var initialPose = new ExecutionPose(
                _motor.Root.position,
                _motor.Root.eulerAngles.y);

            if (!ExecutionWarpContext.TryCreateTarget(
                    session,
                    ownerActorId,
                    _combat,
                    initialPose,
                    out ExecutionWarpContext warpContext))
            {
                return false;
            }

            _session = session;
            _warpContext = warpContext;
            _hasSession = true;
            return true;
        }

        public bool TryWarpRootMotion(
            Vector3 originalDeltaPosition,
            Quaternion originalDeltaRotation,
            out Vector3 correctedDeltaPosition,
            out Quaternion correctedDeltaRotation)
        {
            correctedDeltaPosition = originalDeltaPosition;
            correctedDeltaRotation = originalDeltaRotation;

            if (!_isActive ||
                !_hasSession ||
                _warpContext == null ||
                !_warpContext.IsActive ||
                _motor == null ||
                _motor.Root == null)
            {
                return false;
            }

            var currentPose = new ExecutionPose(
                _motor.Root.position,
                _motor.Root.eulerAngles.y);

            float originalDeltaYaw = Mathf.DeltaAngle(
                0f,
                originalDeltaRotation.eulerAngles.y);

            if (!_warpContext.TryWarp(
                    currentPose,
                    originalDeltaPosition,
                    originalDeltaYaw,
                    NormalizedTime,
                    out correctedDeltaPosition,
                    out float correctedDeltaYaw))
            {
                return false;
            }

            correctedDeltaRotation =
                Quaternion.Euler(0f, correctedDeltaYaw, 0f);

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
                "ExecutedState entered without an execution session.");

            _isActive = true;
            ElapsedTime = 0f;

            _motor.SetSprintActive(false);
            _motor.SetMovementBlocked(true);
            _motor.SetTurnRotationOverride(true);
            ApplyInitialPose();
            _motor.BeginReactionRootMotion();
        }

        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (!_isActive)
                return;

            ElapsedTime += Mathf.Max(0f, deltaTime);

            _motor.Tick(default, deltaTime);
            RefreshWarpResidual();
        }

        public void Exit()
        {
            _motor.EndReactionRootMotion();
            _motor.SetMovementBlocked(false);
            _motor.SetTurnRotationOverride(false);

            _isActive = false;
            ElapsedTime = 0f;
            ClearBinding();
        }

        private void ApplyInitialPose()
        {
            if (!_hasSession)
                return;

            ExecutionPose pose = _session.FixedTargetPose;
            _motor.Root.SetPositionAndRotation(
                pose.Position,
                Quaternion.Euler(0f, pose.Yaw, 0f));
        }

        private void ClearBinding()
        {
            if (_warpContext != null)
            {
                _warpContext.TryEnd(_warpContext.ExecutionId);
            }

            _warpContext = null;
            _session = default;
            _hasSession = false;
        }

        private void RefreshWarpResidual()
        {
            if (_warpContext == null ||
                !_warpContext.IsActive ||
                _motor == null ||
                _motor.Root == null)
            {
                return;
            }

            var actualPose = new ExecutionPose(
                _motor.Root.position,
                _motor.Root.eulerAngles.y);

            _warpContext.RefreshRemainingError(actualPose);
        }
    }
}
