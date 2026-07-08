using Character.Combat;
using Character.Config;
using Character.Controller;
using Character.Intent;
using Character.Presentation;
using Character.StateMachine;
using Core;
using Mirror;
using UnityEngine;

namespace AI
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class NpcCharacterDriver : NetworkBehaviour, IAnimatorRootMotionReceiver
    {
        [SerializeField] private NpcAiIntentSource _intentSource;
        [SerializeField] private NpcMotor _motor;
        [SerializeField] private Animator _animator;

        private CharacterStateMachine _fsm;
        private CharacterStateRegistry _registry;

        private NpcIdleState _idle;
        private NpcMoveState _move;
        private NpcSprintState _sprint;
        private NpcAttackState _attack;
        private NpcDodgeState _dodge;
        private NpcGuardState _guard;
        private NpcHitState _hit;
        private NpcDeadState _dead;

        private CharacterCombatConfig combatConfig => GameDataManager.Instance.Npc.combat;


        public CharacterStateId CurrentStateId => _fsm?.CurrentState?.Id ?? CharacterStateId.None;

        public byte LastPreparedDodgeMode { get; private set; }


        private void Awake()
        {
            if (_intentSource == null) _intentSource = GetComponent<NpcAiIntentSource>();
            if (_motor == null) _motor = GetComponent<NpcMotor>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                _animator.applyRootMotion = true;
                var relay = _animator.GetComponent<AnimatorRootMotionRelay>();
                if (relay == null)
                    relay = _animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
                relay.Initialize(this);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _fsm = new CharacterStateMachine();
            _registry = new CharacterStateRegistry();
            _idle = new NpcIdleState(_fsm, _registry, _intentSource, _motor);
            _move = new NpcMoveState(_fsm, _registry, _intentSource, _motor);
            _sprint = new NpcSprintState(_fsm, _registry, _motor, combatConfig);
            _attack = new NpcAttackState(_fsm, _registry, _motor, combatConfig);
            _dodge = new NpcDodgeState(_fsm, _registry, _motor, combatConfig);
            _guard = new NpcGuardState(_fsm, _registry, _motor, combatConfig);
            _hit = new NpcHitState(_fsm, _registry, _motor, combatConfig);
            _dead = new NpcDeadState(_fsm, _registry, _motor);

            _registry.Register(_idle);
            _registry.Register(_move);
            _registry.Register(_sprint);
            _registry.Register(_attack);
            _registry.Register(_dodge);
            _registry.Register(_guard);
            _registry.Register(_hit);
            _registry.Register(_dead);

            _fsm.Initialize(_idle);
        }

        /// <summary>
        /// Must run after behavior trees produce intents — placed in LateUpdate to guarantee ordering.
        /// </summary>
        private void LateUpdate()
        {
            if (!isServer || _fsm == null) return;

            CharacterIntent intent = _intentSource != null ? _intentSource.BuildIntent() : default;

            _fsm.Tick(intent, Time.deltaTime);
        }

        public bool TryGetActiveSprintState(out NpcSprintState sprintState)
        {

            if (_fsm?.CurrentState is NpcSprintState active)
            {
                sprintState = active;
                return true;
            }

            sprintState = null;
            return false;
        }
        public bool TryGetActiveGuardState(out NpcGuardState guardState)
        {

            if (_fsm?.CurrentState is NpcGuardState active)
            {
                guardState = active;
                return true;
            }

            guardState = null;
            return false;
        }
        public bool TryGetActiveAttackState(out NpcAttackState attackState)
        {

            if (_fsm?.CurrentState is NpcAttackState active)
            {
                attackState = active;
                return true;
            }

            attackState = null;
            return false;
        }
        public bool TryGetActiveHitState(out NpcHitState hitState)
        {

            if (_fsm?.CurrentState is NpcHitState active)
            {
                hitState = active;
                return true;
            }

            hitState = null;
            return false;
        }
        public bool TryGetDodgePresentationContext(out DodgePresentationContext ctx)
        {

            if (_fsm?.CurrentState is NpcDodgeState dodge && dodge.PresentationContext.IsValid)
            {
                ctx = dodge.PresentationContext;
                return true;
            }

            ctx = default;
            return false;
        }
        public void SetLastPreparedDodgeMode(byte mode) => LastPreparedDodgeMode = mode;



        // —— Server 调试入口（验收用，后续可换成 BT Task）——
        public bool ServerTryEnterAttack(AttackMoveId attackId = AttackMoveId.Combo1)
        {
            if (!isServer || _attack == null) return false;
            _attack.Prepare(attackId);
            return _fsm.TryTransition(CharacterStateId.Attack, _registry, TransitionReason.InputAttack);
        }

        public bool ServerTryEnterAttack(byte attackStep)
        {
            return ServerTryEnterAttack(AttackMoveIdExtensions.FromByte(attackStep));
        }
        public bool ServerTryEnterDodge(DodgeMode mode, Vector3 worldDir, Vector2 blendLocal = default)
        {
            if (!isServer || _dodge == null) return false;
            _dodge.Prepare(mode, worldDir, blendLocal);
            SetLastPreparedDodgeMode((byte)mode);
            return _fsm.TryTransition(CharacterStateId.Dodge, _registry, TransitionReason.InputDodge);
        }
        public bool ServerTryEnterGuard(float loopHoldDuration = 2f)
        {
            if (!isServer) return false;
            return ForceEnterGuard(loopHoldDuration);
        }
        public bool ServerTryEnterHit(bool isHeavy = false)
        {
            if (!isServer || _hit == null) return false;
            _hit.Prepare(isHeavy);
            return _fsm.TryTransition(
                CharacterStateId.Hit,
                _registry,
                isHeavy ? TransitionReason.HitHeavy : TransitionReason.HitLight);
        }
        public bool ServerTryEnterDead()
        {
            if (!isServer) return false;
            return _fsm.TryTransition(CharacterStateId.Dead, _registry, TransitionReason.Death);
        }
        public bool ServerTryEnterSprint(float holdDuration = 1.5f)
        {
            if (!isServer || _sprint == null) return false;
            _sprint.Prepare(holdDuration);
            return _fsm.TryTransition(CharacterStateId.Sprint, _registry, TransitionReason.InputSprint);
        }

        public bool ServerTryRevive()
        {
            if (!isServer) return false;
            return _fsm.TryTransition(CharacterStateId.Idle, _registry, TransitionReason.Revive);
        }

        public void HandleAnimatorRootMotion(Vector3 deltaPosition, Quaternion deltaRotation)
        {
            if (!isServer || _motor == null)
                return;

            if (CurrentStateId is not (
                    CharacterStateId.Attack
                    or CharacterStateId.Hit
                    or CharacterStateId.Dead))
                return;

            _motor.ApplyRootMotionDelta(deltaPosition, deltaRotation);
        }


        public bool ForceEnterGuard(float loopHoldDuration = 2f)
        {
            if(_guard == null || _fsm == null || _registry == null) return false;
            _guard.Prepare(loopHoldDuration);
            return _fsm.TryTransition(CharacterStateId.Guard, _registry, TransitionReason.InputGuard) || CurrentStateId == CharacterStateId.Guard;
        }
    }
}
