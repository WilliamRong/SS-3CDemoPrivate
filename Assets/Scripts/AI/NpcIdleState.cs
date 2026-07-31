using Character.Intent;
using Character.Config;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 在待机态仲裁移动与离散转身，避免行为树、Motor 和表现层分别抢写角色朝向。
    /// </summary>
    public sealed class NpcIdleState : ICharacterState
    {

        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcAiIntentSource _intentSource;
        private readonly NpcMotor _motor;
        private readonly CharacterPresentationConfig _presentationConfig;

        private float _turnCooldown;
        private float _phaseTimer;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        private const float MinMoveDistSq = 0.0025f; // 0.05f ^2

        public IdleState.IdlePhase CurrentPhase { get; private set; } = IdleState.IdlePhase.Normal;
        public float GetTargetTurnAngle() => _targetTurnAngle;
        public CharacterStateId Id => CharacterStateId.Idle;

        public NpcIdleState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcAiIntentSource intentSource,
            NpcMotor motor,
            CharacterPresentationConfig presentationConfig)
        {
            _fsm = fsm;
            _registry = registry;
            _intentSource = intentSource;
            _motor = motor;
            _presentationConfig = presentationConfig;
        }

        // ============ 状态生命周期 ============

        public void Enter()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = 0f;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
            _motor?.Stop();
        }

        /// <summary>
        /// 导航转换优先于自动转身，避免已有世界移动目标时 NPC 仍被原地朝向动画阻塞。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            if (_intentSource == null || _motor == null) return;

            if (_turnCooldown > 0f)
                _turnCooldown -= deltaTime;

            if (CurrentPhase is IdleState.IdlePhase.TurnLeft or IdleState.IdlePhase.TurnRight)
            {
                TickTurn(deltaTime);
                return;
            }

            if (ShouldMoveToDestination())
            {
                _fsm.TryTransition(CharacterStateId.Move, _registry, TransitionReason.InputMove);
                return;
            }

            if (ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
                BeginTurn(turnLeft, angleDelta);
        }

        public void Exit()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = 0f;
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
        }

        // ============ 移动判定 ============

        private bool ShouldMoveToDestination()
        {
            if (!_intentSource.TryGetMoveDestination(out var dest))
                return false;

            Vector3 delta = dest - _motor.Root.position;
            delta.y = 0f;
            return delta.sqrMagnitude >= MinMoveDistSq;
        }

        // ============ 锁定转身 ============

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta))
                return false;

            return CharacterTurnPlanner.ShouldTurn(_turnCooldown, angleDelta, _presentationConfig);
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            CurrentPhase = turnLeft ? IdleState.IdlePhase.TurnLeft : IdleState.IdlePhase.TurnRight;
            CharacterTurnPlan plan = CharacterTurnPlanner.BuildPlan(_motor.Root, turnLeft, angleDelta, _presentationConfig);
            _targetTurnAngle = plan.StepAngle;
            _turnTargetRotation = plan.TargetRotation;
            _phaseTimer = 0f;
            _turnDuration = plan.Duration;
            _turnYawSpeed = plan.YawSpeed;
            _motor.Stop();
        }

        /// <summary>
        /// 角速度和超时双重结束转身，既让逻辑朝向跟随动画，也避免异常配置导致状态无法退出。
        /// </summary>
        private void TickTurn(float deltaTime)
        {
            _motor.Stop();

            _phaseTimer += deltaTime;
            float maxDegreesDelta = Mathf.Max(_turnYawSpeed, 1f) * deltaTime;
            _motor.RotateTowards(_turnTargetRotation, maxDegreesDelta);

            if (CharacterTurnPlanner.IsAligned(_motor.Root.rotation, _turnTargetRotation, _presentationConfig)
                || _phaseTimer >= _turnDuration)
            {
                EndTurn();
            }
        }

        private void EndTurn()
        {
            CurrentPhase = IdleState.IdlePhase.Normal;
            _turnCooldown = CharacterTurnPlanner.GetCooldown(_presentationConfig);
            _phaseTimer = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor.Root.rotation;
        }

        private bool TryGetFacingAngleToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (!_intentSource.TryGetFacingTarget(out Vector3 targetPos))
                return false;

            return CharacterTurnPlanner.TryGetFacingDelta(
                _motor.Root,
                targetPos,
                out turnLeft,
                out angleDelta,
                out _);
        }
    }
}
