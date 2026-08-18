using System.Threading;
using Character.Config;
using Character.Execution;
using Character.Intent;
using Character.Motor;
using Opsive.BehaviorDesigner.Runtime.Tasks.Decorators;
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

        private readonly CharacterCombatConfig _combat;
        private ExecutionWarpContext _warpContext;

        public ExecutionWarpContext WarpContext => _warpContext;

        public float NormalizedTime => Mathf.Clamp01(ElapsedTime / _duration);

        public bool HasReachedDuration =>
                    _isActive && ElapsedTime >= _duration;


        public ExecutingState(
                  CharacterMotor motor,
                  CharacterCombatConfig combat)
        {
            _motor = motor;
            _combat = combat;

            _duration = Mathf.Max(
                0.01f,
                combat != null ? combat.executingDuration : 0.01f);
        }

        public bool TryPrepare(in ExecutionSession session,
            int ownerActorId)
        {
            if (!session.TryValidate(out _) || !session.IsActive || session.ExecutorActorId != ownerActorId || _isActive || _motor == null || _motor.Root == null) return false;

            if (_hasSession) return _session.ExecutionId == session.ExecutionId && _warpContext != null && _warpContext.IsActive;

            var initialPose = new ExecutionPose(_motor.Root.position, _motor.Root.eulerAngles.y);

            if (!ExecutionWarpContext.TryCreate(session, ownerActorId, _combat, initialPose, out ExecutionWarpContext warpContext))
            {
                return false;
            }

            _session = session;
            _warpContext = warpContext;
            _hasSession = true;
            return true;
        }


        public bool TryWarpRootMotion(Vector3 originalDeltaPosition, Quaternion originialDeltaRotation, out Vector3 correctedDeltaPosition, out Quaternion correctedDeltaRotation)
        {
            correctedDeltaPosition = originalDeltaPosition;
            correctedDeltaRotation = originialDeltaRotation;

            if (!_isActive ||
        !_hasSession ||
        _warpContext == null ||
        !_warpContext.IsActive ||
        _motor == null ||
        _motor.Root == null)
            {
                return false;
            }

            var currentPose = new ExecutionPose(_motor.Root.position, _motor.Root.eulerAngles.y);

            float originalDeltaYaw = Mathf.DeltaAngle(0f, originialDeltaRotation.eulerAngles.y);

            if (!_warpContext.TryWarp(currentPose, originalDeltaPosition, originalDeltaYaw, NormalizedTime, out correctedDeltaPosition, out float correctedDeltaYaw))
            {
                return false;
            }

            correctedDeltaRotation = Quaternion.Euler(0f, correctedDeltaYaw, 0f);

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
            if (_warpContext != null)
            {
                _warpContext.TryEnd(_warpContext.ExecutionId);
            }


            _warpContext = null;
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

            // CharacterController.Move 已经执行，现在读取碰撞后的真实残差。
            RefreshWarpResidual();
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

            var acutalPose = new ExecutionPose(_motor.Root.position, _motor.Root.eulerAngles.y);

            _warpContext.RefreshRemainingError(acutalPose);
        }


    }
}