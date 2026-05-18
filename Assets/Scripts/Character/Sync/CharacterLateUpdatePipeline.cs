using AI;
using Character.Config;
using Character.Controller;
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
    /// Priority: combat overlay (Hit/Dead) → Sprint (layer 0) → post-sprint locomotion → default locomotion.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLateUpdatePipeline : MonoBehaviour
    {
        [SerializeField] private RemoteInterpolator _remoteInterpolator;
        [SerializeField] private Animator _animator;

        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;
        private PlayerAuthorityGate _authorityGate;
        private NpcCharacterDriver _npcDriver;
        private NPCMotor _npcMotor;
        private ILockOnLocomotionQuery _lockOnQuery;

        private CharacterLocomotionPresenter _locomotionPresenter;
        private CharacterSprintPresenter _sprintPresenter;
        private CharacterCombatPresenter _combatPresenter;

        private CharacterStateId _lastPresentationStateId = CharacterStateId.None;

        private void Awake()
        {
            if (_remoteInterpolator == null)
                _remoteInterpolator = GetComponent<RemoteInterpolator>();

            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _authorityGate = GetComponent<PlayerAuthorityGate>();
            _npcDriver = GetComponent<NpcCharacterDriver>();
            _npcMotor = GetComponent<NPCMotor>();
            _lockOnQuery = GetComponent<ILockOnLocomotionQuery>();

            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_animator != null)
                _animator.applyRootMotion = false;

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

            if (TryPresentCombat(frame)
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
                out var isLockOn,
                out var sprintPhase);

            return new PresentationFrame(
                _lastPresentationStateId,
                stateId,
                velocityXZ,
                isLockOn,
                sprintPhase);
        }

        private void CommitPresentationFrame(PresentationFrame frame)
        {
            _lastPresentationStateId = frame.StateId;
        }

        private bool TryPresentCombat(PresentationFrame frame)
        {
            if (!IsCombatPresentationState(frame.StateId))
                return false;

            ReleaseSprintOverlayIfNeeded(frame);
            _combatPresenter.Tick(_animator, frame.StateId);
            return true;
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
            out bool isLockOn,
            out SprintState.SprintPhase sprintPhase)
        {
            isLockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;
            sprintPhase = SprintState.SprintPhase.Loop;

            if (TryResolveFromLocalPlayer(out stateId, out velocityXZ, out sprintPhase))
                return;

            if (TryResolveFromServerNpc(out stateId, out velocityXZ))
                return;

            if (TryResolveFromRemoteSnapshot(out stateId, out velocityXZ, out sprintPhase))
                return;

            stateId = CharacterStateId.Idle;
            velocityXZ = Vector2.zero;
        }

        private bool TryResolveFromLocalPlayer(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out SprintState.SprintPhase sprintPhase)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            sprintPhase = SprintState.SprintPhase.Loop;

            if (_playerController == null)
                return false;

            if (!HasLocalPresentationAuthority())
                return false;

            stateId = _playerController.CurrentStateId;
            var velocity = _playerController.Velocity;
            velocityXZ = new Vector2(velocity.x, velocity.z);

            if (stateId == CharacterStateId.Sprint
                && _playerController.TryGetActiveSprintState(out var sprintState))
            {
                sprintPhase = sprintState.CurrentPhase;
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

        private bool TryResolveFromServerNpc(out CharacterStateId stateId, out Vector2 velocityXZ)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;

            if (_npcDriver == null || _playerController != null)
                return false;

            if (_networkIdentity == null || !_networkIdentity.isServer || !NetworkServer.active)
                return false;

            stateId = _npcDriver.CurrentStateId;

            var agent = _npcMotor != null ? _npcMotor.Agent : null;
            if (agent != null)
                velocityXZ = new Vector2(agent.velocity.x, agent.velocity.z);

            return true;
        }

        private bool TryResolveFromRemoteSnapshot(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out SprintState.SprintPhase sprintPhase)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;
            sprintPhase = SprintState.SprintPhase.Loop;

            if (_remoteInterpolator == null)
                return false;

            var snapshot = _remoteInterpolator.LastAppliedSnapshot;
            if (snapshot.Tick <= 0)
                return false;

            stateId = snapshot.StateId;
            velocityXZ = snapshot.VelocityXZ;
            sprintPhase = snapshot.GetSprintPhaseOrDefault();
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
                bool isLockOn,
                SprintState.SprintPhase sprintPhase)
            {
                PreviousStateId = previousStateId;
                StateId = stateId;
                VelocityXZ = velocityXZ;
                IsLockOn = isLockOn;
                SprintPhase = sprintPhase;
            }

            public CharacterStateId PreviousStateId { get; }
            public CharacterStateId StateId { get; }
            public Vector2 VelocityXZ { get; }
            public bool IsLockOn { get; }
            public SprintState.SprintPhase SprintPhase { get; }

            public bool LeftSprint =>
                PreviousStateId == CharacterStateId.Sprint && StateId != CharacterStateId.Sprint;

            public bool LeftCombat =>
                IsCombatState(PreviousStateId) && !IsCombatState(StateId);

            private static bool IsCombatState(CharacterStateId id)
            {
                return id is CharacterStateId.Hit
                    or CharacterStateId.Dead
                    or CharacterStateId.Attack
                    or CharacterStateId.Dodge;
            }
        }
    }
}
