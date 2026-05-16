using AI;
using Character.Controller;
using Character.Presentation;
using Character.StateMachine;
using Core;
using Mirror;
using UnityEngine;

namespace Character.Sync
{
    /// <summary>
    /// Single LateUpdate entry for visual follow-up: remote interpolation, then locomotion presentation.
    /// Does not rely on Script Execution Order — call order is explicit in <see cref="TickLateUpdate"/>.
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

        private readonly CharacterLocomotionPresenter _locomotionPresenter = new();

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
        }

        /// <summary>
        /// Runs after gameplay Update (e.g. from <see cref="PlayerController"/> on local authority).
        /// </summary>
        public void TickLateUpdate()
        {
            if (_remoteInterpolator != null)
                _remoteInterpolator.TickInterpolation();

            if (_animator == null)
                return;

            ResolvePresentation(out var stateId, out var velocityXZ, out var isLockOn);
            _locomotionPresenter.Tick(
                _animator,
                transform,
                stateId,
                velocityXZ,
                isLockOn);
        }

        private void LateUpdate()
        {
            if (IsDrivenByPlayerControllerLateUpdate())
                return;

            TickLateUpdate();
        }

        private void ResolvePresentation(
            out CharacterStateId stateId,
            out Vector2 velocityXZ,
            out bool isLockOn)
        {
            isLockOn = _lockOnQuery != null && _lockOnQuery.IsLockOnActive;

            if (TryResolveFromLocalPlayer(out stateId, out velocityXZ))
                return;

            if (TryResolveFromServerNpc(out stateId, out velocityXZ))
                return;

            if (TryResolveFromRemoteSnapshot(out stateId, out velocityXZ))
                return;

            stateId = CharacterStateId.Idle;
            velocityXZ = Vector2.zero;
        }

        private bool TryResolveFromLocalPlayer(out CharacterStateId stateId, out Vector2 velocityXZ)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;

            if (_playerController == null)
                return false;

            if (!HasLocalPresentationAuthority())
                return false;

            stateId = _playerController.CurrentStateId;
            var velocity = _playerController.Velocity;
            velocityXZ = new Vector2(velocity.x, velocity.z);
            return true;
        }

        private bool HasLocalPresentationAuthority()
        {
            if (_authorityGate != null)
                return _authorityGate.CanProcessLocalInput;

            if (_networkIdentity != null)
                return _networkIdentity.isLocalPlayer;

            // No Mirror on this instance (offline test): PlayerController sim is authoritative.
            return true;
        }

        /// <summary>
        /// Host / dedicated server: NPC FSM and NavMesh run only on server — use sim state, not delayed snapshots.
        /// </summary>
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

        private bool TryResolveFromRemoteSnapshot(out CharacterStateId stateId, out Vector2 velocityXZ)
        {
            stateId = CharacterStateId.None;
            velocityXZ = Vector2.zero;

            if (_remoteInterpolator == null)
                return false;

            var snapshot = _remoteInterpolator.LastAppliedSnapshot;
            if (snapshot.Tick <= 0)
                return false;

            stateId = snapshot.StateId;
            velocityXZ = snapshot.VelocityXZ;
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
    }
}
