using Character.Config;
using Character.Intent;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// 在服务器状态内完成防御阶段和离散转身，保证远端只表现权威相位而不自行选方向。
    /// </summary>
    public sealed class NpcGuardState : ICharacterState
    {
        private readonly CharacterStateMachine _fsm;
        private readonly CharacterStateRegistry _registry;
        private readonly NpcMotor _motor;
        private readonly CharacterCombatConfig _combatConfig;
        private readonly CharacterPresentationConfig _presentationConfig;
        private readonly NpcAiIntentSource _intentSource;

        private float _phaseTimer;
        private float _loopHoldDuration = 2f;
        private float _turnCooldown;
        private float _targetTurnAngle;
        private float _turnDuration;
        private float _turnYawSpeed;
        private Quaternion _turnTargetRotation = Quaternion.identity;

        public GuardState.GuardPhase CurrentPhase { get; private set; } = GuardState.GuardPhase.Start;
        public float GetTargetTurnAngle() => _targetTurnAngle;
        
        public CharacterStateId Id { get; } = CharacterStateId.Guard;
        
        public NpcGuardState(
            CharacterStateMachine fsm,
            CharacterStateRegistry registry,
            NpcMotor motor,
            CharacterCombatConfig combat,
            CharacterPresentationConfig presentationConfig,
            NpcAiIntentSource intentSource)
        {
            _fsm = fsm;
            _registry = registry;
            _motor = motor;
            _combatConfig = combat;
            _presentationConfig = presentationConfig;
            _intentSource = intentSource;
        }
        
        public void Prepare(float loopHoldDuration = 2f) => _loopHoldDuration = Mathf.Max(0.1f, loopHoldDuration);

        // ============ 状态生命周期 ============

        public void Enter()
        {
            SetPhase(GuardState.GuardPhase.Start);
            _motor?.Stop();
        }

        /// <summary>
        /// 防御阶段使用单一分派入口推进，保证服务器快照每帧只观察到一个确定阶段。
        /// </summary>
        public void Tick(CharacterIntent intent, float deltaTime)
        {
            switch (CurrentPhase)
            {
                case GuardState.GuardPhase.Start:
                    TickStart(deltaTime);
                    break;
                case GuardState.GuardPhase.Loop:
                    TickLoop(deltaTime);
                    break;
                case GuardState.GuardPhase.Exit:
                    TickExit(deltaTime);
                    break;
                case GuardState.GuardPhase.TurnLeft:
                case GuardState.GuardPhase.TurnRight:
                    TickTurn(deltaTime);
                    break;
            }
        }

        public void Exit()
        {
            _turnCooldown = 0f;
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            if (_motor != null)
                _turnTargetRotation = _motor.Root.rotation;
            SetPhase(GuardState.GuardPhase.Start);
        }

        // ============ 防御阶段推进 ============

        private void TickStart(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if(_phaseTimer >= _combatConfig.guardStartDuration)
                SetPhase(GuardState.GuardPhase.Loop);
        }

        /// <summary>
        /// 自动转身优先于持续时长退出，使 NPC 不会在刚发现目标偏角时恰好结束防御。
        /// </summary>
        private void TickLoop(float deltaTime)
        {
            if (_turnCooldown > 0f)
                _turnCooldown -= deltaTime;

            if (ShouldTurnToTarget(out bool turnLeft, out float angleDelta))
            {
                BeginTurn(turnLeft, angleDelta);
                return;
            }

            _phaseTimer += deltaTime;
            if(_phaseTimer >= _loopHoldDuration)
                SetPhase(GuardState.GuardPhase.Exit);
        }

        private void TickExit(float deltaTime)
        {
            _phaseTimer += deltaTime;
            if(_phaseTimer < _combatConfig.guardExitDuration) return;

            _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Timeout);
        }

        private void SetPhase(GuardState.GuardPhase phase)
        {
            CurrentPhase = phase;
            _phaseTimer = 0f;
        }

        // ============ 锁定转身 ============

        private bool ShouldTurnToTarget(out bool turnLeft, out float angleDelta)
        {
            turnLeft = false;
            angleDelta = 0f;

            if (_motor == null || _intentSource == null || _turnCooldown > 0f)
                return false;

            if (!TryGetFacingAngleToTarget(out turnLeft, out angleDelta))
                return false;

            return CharacterTurnPlanner.ShouldTurn(_turnCooldown, angleDelta, _presentationConfig);
        }

        private void BeginTurn(bool turnLeft, float angleDelta)
        {
            SetPhase(turnLeft ? GuardState.GuardPhase.TurnLeft : GuardState.GuardPhase.TurnRight);

            CharacterTurnPlan plan = CharacterTurnPlanner.BuildPlan(_motor.Root, turnLeft, angleDelta, _presentationConfig);
            _targetTurnAngle = plan.StepAngle;
            _turnTargetRotation = plan.TargetRotation;
            _turnDuration = plan.Duration;
            _turnYawSpeed = plan.YawSpeed;
            _motor.Stop();
        }

        /// <summary>
        /// 防御转身期间持续停止导航，并以对齐或超时结束，避免 NavMesh 与转身旋转同时写入朝向。
        /// </summary>
        private void TickTurn(float deltaTime)
        {
            if (_motor == null)
            {
                EndTurn();
                return;
            }

            _phaseTimer += deltaTime;
            _motor.Stop();

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
            SetPhase(GuardState.GuardPhase.Loop);
            _turnCooldown = CharacterTurnPlanner.GetCooldown(_presentationConfig);
            _turnDuration = 0f;
            _turnYawSpeed = 0f;
            _turnTargetRotation = _motor != null ? _motor.Root.rotation : Quaternion.identity;
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
