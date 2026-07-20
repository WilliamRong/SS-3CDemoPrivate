using AI;
using Character.Combat;
using Character.Config;
using Character.Controller;
using Character.LockOn;
using Character.Presentation;
using Character.StateMachine;
using Character.StateMachine.States;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// Single LateUpdate entry for visual follow-up: remote interpolation, then presentation routing.
    /// Priority: reaction/dodge/attack → guard (full-body or upper-body overlay) → Sprint → locomotion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLateUpdatePipeline : MonoBehaviour
    {
        [SerializeField] private RemoteInterpolator _remoteInterpolator;
        [SerializeField] private RemoteActionApplier _remoteActionApplier;
        [SerializeField] private Animator _animator;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;
        private PlayerAuthorityGate _authorityGate;
        private NpcCharacterDriver _npcDriver;
        private NpcMotor _npcMotor;
        private ILockOnLocomotionQuery _lockOnQuery;

        private CharacterLocomotionPresenter _locomotionPresenter;
        private CharacterTurnPresenter _turnPresenter;
        private CharacterSprintPresenter _sprintPresenter;
        private CharacterCombatPresenter _combatPresenter;


        private CharacterStateId _lastPresentationStateId = CharacterStateId.None;

        private void Awake()
        {
            if (_remoteInterpolator == null)
                _remoteInterpolator = GetComponent<RemoteInterpolator>();

            if (_remoteActionApplier == null)
                _remoteActionApplier = GetComponent<RemoteActionApplier>();

            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _npcDriver = GetComponent<NpcCharacterDriver>();
            _npcMotor = GetComponent<NpcMotor>();
            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            EnsurePresenters();
        }

        public void TickLateUpdate()
        {
            EnsurePresenters();

            if (_remoteInterpolator != null)
                _remoteInterpolator.TickInterpolation();

            if (_animator == null)
                return;

            var frame = BuildPresentationFrame();

            if (TryPresentGuardReaction())
            {
                CommitPresentationFrame(frame);
                return;
            }

            if (TryPresentCombat(frame)
                || TryPresentIdleTurn(frame)
                || TryPresentGuardTurn(frame)
                || TryPresentGuard(frame)
                || TryPresentSprint(frame)
                || TryPresentAfterSprint(frame))
            {
                CommitPresentationFrame(frame);
                return;
            }

            PresentLocomotion(frame);
            CommitPresentationFrame(frame);
        }

        private void LateUpdate()
        {
            if (IsDrivenByPlayerControllerLateUpdate())
                return;

            TickLateUpdate();
        }

        private PresentationFrame BuildPresentationFrame()
        {
            ResolvePresentation(
                out var stateId,
                out var velocityXZ,
                out var moveInput,
                out var isLockOn,
                out var lockTargetNetId,
                out var sprintPhase,
                out var guardPhase,
                out var idlePhase,
                out var attackComboStep);

            return new PresentationFrame(
                _lastPresentationStateId,
                stateId,
                velocityXZ,
                moveInput,
                isLockOn,
                lockTargetNetId,
                sprintPhase,
                guardPhase,
                idlePhase,
                attackComboStep);
        }

        private void CommitPresentationFrame(PresentationFrame frame)
        {
            _lastPresentationStateId = frame.StateId;
        }


        private bool TryPresentIdleTurn(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Idle)
                return false;

            IdleState.IdlePhase phase = ResolveIdlePhase(frame);
            float targetAngle = ResolveIdleTurnAngle(frame, phase);

            return _turnPresenter.TickIdleTurn(_animator, phase, targetAngle);
        }


        private IdleState.IdlePhase ResolveIdlePhase(PresentationFrame frame)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                return idleState.CurrentPhase;
            }

            return frame.IdlePhase;
        }

        private float ResolveIdleTurnAngle(PresentationFrame frame, IdleState.IdlePhase phase)
        {
            if (phase is not (IdleState.IdlePhase.TurnLeft or IdleState.IdlePhase.TurnRight))
                return 0f;

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                return idleState.GetTargetTurnAngle();
            }

            if (_npcDriver != null
                && _networkIdentity != null
                && _networkIdentity.isServer
                && _npcDriver.TryGetActiveIdleState(out var npcIdleState))
            {
                return npcIdleState.GetTargetTurnAngle();
            }

            CharacterPresentationConfig presentation = ResolvePresentationConfig();
            return presentation != null ? presentation.turnStepAngle : 180f;
        }

        private bool TryPresentCombat(PresentationFrame frame)
        {
            if (!IsCombatPresentationState(frame.StateId))
                return false;

            ReleaseSprintOverlayIfNeeded(frame);

            var dodgeCtx = ResolveDodgePresentationContext(frame.StateId);
            int actionParams = ResolveActionParams(frame.StateId);
            return _combatPresenter.TickCombat(
                _animator,
                frame.StateId,
                actionParams,
                dodgeCtx: dodgeCtx,
                attackComboStep: frame.AttackComboStep);
        }

        private int ResolveActionParams(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Hit || _remoteActionApplier == null)
                return 0;

            return _remoteActionApplier.CurrentRemoteAction == ActionType.Hit
                ? _remoteActionApplier.LastHitParam
                : 0;
        }

        private bool TryPresentGuard(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Guard)
            {
                if (frame.PreviousStateId == CharacterStateId.Guard)
                    _combatPresenter.ResetGuardLayers(_animator);

                return false;
            }

            ReleaseSprintOverlayIfNeeded(frame);

            bool hasMove = frame.VelocityXZ.sqrMagnitude > 0.01f;
            var phase = ResolveGuardPhase(frame);
            return _combatPresenter.TickGuard(_animator, phase, hasMove);
        }


        private bool TryPresentGuardTurn(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Guard)
            {
                if (frame.StateId != CharacterStateId.Idle)
                    _turnPresenter.Reset();

                return false;
            }

            GuardState.GuardPhase phase = ResolveGuardPhase(frame);
            if (phase is not (GuardState.GuardPhase.TurnLeft or GuardState.GuardPhase.TurnRight))
            {
                _turnPresenter.Reset();
                return false;
            }

            float targetAngle = ResolveGuardTurnAngle(frame, phase);
            _combatPresenter.ResetGuardLayers(_animator);
            return _turnPresenter.TickGuardTurn(_animator, phase, targetAngle);
        }

        private float ResolveGuardTurnAngle(PresentationFrame frame, GuardState.GuardPhase phase)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                return guardState.GetTargetTurnAngle();
            }

            if (_npcDriver != null
                && _networkIdentity != null
                && _networkIdentity.isServer
                && _npcDriver.TryGetActiveGuardState(out var npcGuardState))
            {
                return npcGuardState.GetTargetTurnAngle();
            }

            CharacterPresentationConfig presentation = ResolvePresentationConfig();
            return presentation != null ? presentation.turnStepAngle : 180f;
        }

        private bool TryPresentGuardReaction()
        {
            if (_combatPresenter == null || _animator == null)
                return false;

            if (_remoteActionApplier != null
                && _remoteActionApplier.TryConsumeGuardReaction(out var reaction))
            {
                CharacterCombatConfig combat = ResolveCombatConfig();
                float hitDuration = combat != null ? combat.guardHitReactionDuration : 0.28f;
                float breakDuration = combat != null ? combat.guardBreakReactionDuration : 0.65f;
                return _combatPresenter.PlayGuardReaction(_animator, reaction, hitDuration, breakDuration);
            }

            return _combatPresenter.TickGuardReaction(_animator);
        }

        private GuardState.GuardPhase ResolveGuardPhase(PresentationFrame frame)
        {
            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                return guardState.CurrentPhase;
            }

            return frame.GuardPhase;
        }

        private DodgePresentationContext ResolveDodgePresentationContext(CharacterStateId stateId)
        {
            if (stateId != CharacterStateId.Dodge)
                return default;

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetDodgePresentationContext(out var ctx))
            {
                return ctx;
            }

            var combat = ResolveCombatConfig();
            if (_remoteInterpolator != null)
            {
                var snapshot = ResolveRemoteDodgeSnapshot(_remoteInterpolator.LastAppliedSnapshot);
                if (snapshot.Tick > 0 && snapshot.TryBuildDodgePresentationContext(combat, out var remoteCtx))
                    return remoteCtx;
            }

            return new DodgePresentationContext(
                DodgeMode.ForwardAlongMove,
                Vector2.zero,
                combat.dodgeEvadeDuration,
                combat.GetDodgeMoveDuration(DodgeMode.ForwardAlongMove),
                transform.forward);
        }

        private CharacterCombatConfig ResolveCombatConfig()
        {
            return _playerController != null
                ? GameDataManager.Instance.Player.combat
                : GameDataManager.Instance.Npc.combat;
        }

        private StateSnapshot ResolveRemoteDodgeSnapshot(StateSnapshot snapshot)
        {
            if (snapshot.StateId != CharacterStateId.Dodge || snapshot.DodgeMode != 0)
                return snapshot;

            if (_remoteActionApplier == null || _remoteActionApplier.LastDodgeMode == 0)
                return snapshot;

            return snapshot.WithDodgeMode(_remoteActionApplier.LastDodgeMode);
        }

        private bool TryPresentSprint(PresentationFrame frame)
        {
            if (frame.StateId != CharacterStateId.Sprint)
                return false;

            if (frame.LeftCombat)
                _combatPresenter.ResetAllCombatLayers(_animator);

            _locomotionPresenter.ReleaseLayerToSprint();

            if (_playerController != null
                && HasLocalPresentationAuthority()
                && _playerController.TryGetActiveSprintState(out var sprintState))
            {
                _sprintPresenter.Tick(_animator, sprintState);
            }
            else
            {
                _sprintPresenter.TickRemotePhase(_animator, frame.SprintPhase);
            }

            return true;
        }

        private bool TryPresentAfterSprint(PresentationFrame frame)
        {
            if (!frame.LeftSprint)
                return false;

            _sprintPresenter.Reset();
            _locomotionPresenter.TickLeavingSprint(
                _animator,
                transform,
                frame.StateId,
                frame.VelocityXZ,
                frame.MoveInput,
                frame.IsLockOn);

            return true;
        }

        private void PresentLocomotion(PresentationFrame frame)
        {
            if (frame.LeftCombat)
                _combatPresenter.ResetAllCombatLayers(_animator);

            _locomotionPresenter.Tick(
                _animator,
                transform,
                frame.StateId,
                frame.VelocityXZ,
                frame.MoveInput,
                frame.IsLockOn);
        }

        private void ReleaseSprintOverlayIfNeeded(PresentationFrame frame)
        {
            if (frame.PreviousStateId != CharacterStateId.Sprint)
                return;

            _sprintPresenter.Reset();
            _locomotionPresenter.ReleaseLayerToSprint();
        }

        private static bool IsCombatPresentationState(CharacterStateId id)
        {
            return id is CharacterStateId.Hit
                or CharacterStateId.Dead
                or CharacterStateId.Attack
                or CharacterStateId.Dodge;
        }

        private void EnsurePresenters()
        {
            var presentation = ResolvePresentationConfig();
            _locomotionPresenter ??= new CharacterLocomotionPresenter(presentation);
            _turnPresenter ??= new CharacterTurnPresenter(presentation);
            _sprintPresenter ??= new CharacterSprintPresenter(presentation);
            _combatPresenter ??= new CharacterCombatPresenter(presentation);
        }

        private CharacterPresentationConfig ResolvePresentationConfig()
        {
            return _playerController != null
                ? GameDataManager.Instance.Player.presentation
                : GameDataManager.Instance.Npc.presentation;
        }

        private void ResolvePresentation(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep)
        {
            stateId = CharacterStateId.Idle;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;

            if (TryResolveFromLocalPlayer(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep))
                return;

            if (TryResolveFromServerNpc(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep))
                return;

            if (TryResolveFromRemoteSnapshot(
                    out stateId,
                    out velocityXZ,
                    out moveInput,
                    out isLockOn,
                    out lockTargetNetId,
                    out sprintPhase,
                    out guardPhase,
                    out idlePhase,
                    out attackComboStep))
                return;
        }

        private bool TryResolveFromLocalPlayer(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;

            if (_playerController == null)
                return false;

            if (!HasLocalPresentationAuthority())
                return false;

            stateId = _playerController.CurrentStateId;
            var velocity = _playerController.Velocity;
            velocityXZ = new Vector2(velocity.x, velocity.z);
            moveInput = _playerController.LastMoveInput;
            isLockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;

            Transform lockTarget = isLockOn ? _lockOnQuery.CurrentTarget : null;
            if (lockTarget != null)
            {
                var lockOnTarget = lockTarget.GetComponentInParent<LockOnTarget>();
                if (lockOnTarget != null)
                    lockOnTarget.TryGetNetworkId(out lockTargetNetId);
            }
            else
            {
                isLockOn = false;
            }

            if (stateId == CharacterStateId.Sprint
                && _playerController.TryGetActiveSprintState(out var sprintState))
            {
                sprintPhase = sprintState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Guard
                && _playerController.TryGetActiveGuardState(out var guardState))
            {
                guardPhase = guardState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Idle
                && _playerController.TryGetActiveIdleState(out var idleState))
            {
                idlePhase = idleState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Attack
                && _playerController.TryGetActiveAttackState(out var attackState))
            {
                attackComboStep = attackState.CurrentComboStep;
            }

            return true;
        }

        private bool HasLocalPresentationAuthority()
        {
            if (_authorityGate != null)
                return _authorityGate.CanProcessLocalInput;

            if (_networkIdentity != null)
                return _networkIdentity.isLocalPlayer;

            return true;
        }

        private bool TryResolveFromServerNpc(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;

            if (_npcDriver == null || _playerController != null)
                return false;

            if (_networkIdentity == null || !_networkIdentity.isServer || !NetworkServer.active)
                return false;

            stateId = _npcDriver.CurrentStateId;

            var agent = _npcMotor != null ? _npcMotor.Agent : null;
            if (agent != null)
                velocityXZ = new Vector2(agent.velocity.x, agent.velocity.z);

            if (stateId == CharacterStateId.Dodge
                && _npcDriver.TryGetDodgePresentationContext(out var dodgeCtx)
                && dodgeCtx.Mode == DodgeMode.LockOn8Way)
            {
                velocityXZ = dodgeCtx.BlendLocal;
            }

            if (stateId == CharacterStateId.Sprint
                && _npcDriver.TryGetActiveSprintState(out var sprintState))
            {
                sprintPhase = sprintState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Guard
                && _npcDriver.TryGetActiveGuardState(out var guardState))
            {
                guardPhase = guardState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Idle
                && _npcDriver.TryGetActiveIdleState(out var idleState))
            {
                idlePhase = idleState.CurrentPhase;
            }

            if (stateId == CharacterStateId.Attack
                && _npcDriver.TryGetActiveAttackState(out var attackState))
            {
                attackComboStep = attackState.CurrentComboStep;
            }

            return true;
        }

        private bool TryResolveFromRemoteSnapshot(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out Vector2 moveInput,
            out bool isLockOn,
            out uint lockTargetNetId,
            out SprintState.SprintPhase sprintPhase,
            out GuardState.GuardPhase guardPhase,
            out IdleState.IdlePhase idlePhase,
            out byte attackComboStep)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            moveInput = Vector2.zero;
            isLockOn = false;
            lockTargetNetId = 0;
            sprintPhase = SprintState.SprintPhase.Loop;
            guardPhase = GuardState.GuardPhase.Start;
            idlePhase = IdleState.IdlePhase.Normal;
            attackComboStep = 1;

            if (_remoteInterpolator == null)
                return false;

            var snapshot = _remoteInterpolator.LastAppliedSnapshot;
            if (snapshot.Tick <= 0)
                return false;

            stateId = snapshot.StateId;
            velocityXZ = snapshot.VelocityXZ;
            moveInput = snapshot.GetMoveInputOrDefault();
            isLockOn = snapshot.IsLockOnActive;
            lockTargetNetId = snapshot.LockTargetNetId;
            sprintPhase = snapshot.GetSprintPhaseOrDefault();
            guardPhase = snapshot.GetGuardPhaseOrDefault();
            idlePhase = snapshot.GetIdlePhaseOrDefault();
            attackComboStep = snapshot.GetAttackComboStepOrDefault();
            return true;
        }

        private bool IsDrivenByPlayerControllerLateUpdate()
        {
            if (_playerController == null)
                _playerController = GetComponent<PlayerController>();

            if (_playerController == null)
                return false;

            if (_networkIdentity != null && _networkIdentity.isLocalPlayer)
                return true;

            if (_authorityGate == null)
                _authorityGate = GetComponent<PlayerAuthorityGate>();

            return _authorityGate != null && _authorityGate.CanProcessLocalInput;
        }

        private readonly struct PresentationFrame
        {
            public PresentationFrame(
                CharacterStateId previousStateId,
                CharacterStateId stateId,
                Vector2 velocityXZ,
                Vector2 moveInput,
                bool isLockOn,
                uint lockTargetNetId,
                SprintState.SprintPhase sprintPhase,
                GuardState.GuardPhase guardPhase,
                IdleState.IdlePhase idlePhase,
                byte attackComboStep)
            {
                PreviousStateId = previousStateId;
                StateId = stateId;
                VelocityXZ = velocityXZ;
                MoveInput = moveInput;
                IsLockOn = isLockOn;
                LockTargetNetId = lockTargetNetId;
                SprintPhase = sprintPhase;
                GuardPhase = guardPhase;
                IdlePhase = idlePhase;
                AttackComboStep = attackComboStep;
            }

            public CharacterStateId PreviousStateId { get; }
            public CharacterStateId StateId { get; }
            public Vector2 VelocityXZ { get; }
            public Vector2 MoveInput { get; }
            public bool IsLockOn { get; }
            public uint LockTargetNetId { get; }
            public SprintState.SprintPhase SprintPhase { get; }
            public GuardState.GuardPhase GuardPhase { get; }
            public IdleState.IdlePhase IdlePhase { get; }
            public byte AttackComboStep { get; }

            public bool LeftSprint =>
                PreviousStateId == CharacterStateId.Sprint && StateId != CharacterStateId.Sprint;

            public bool LeftCombat =>
                IsCombatState(PreviousStateId) && !IsCombatState(StateId);

            private static bool IsCombatState(CharacterStateId id)
            {
                return id is CharacterStateId.Hit
                    or CharacterStateId.Dead
                    or CharacterStateId.Attack
                    or CharacterStateId.Dodge
                    or CharacterStateId.Guard;
            }
        }
    }
}
