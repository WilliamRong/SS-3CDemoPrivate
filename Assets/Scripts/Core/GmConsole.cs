using AI;
using Character.Controller;
using Mirror;
using UnityEngine;

namespace Core
{
    public struct GmForceGuardMsg : NetworkMessage
    {
        public bool Guarded;
        public uint ExcludedPlayerNetId;
        public float PlayerHoldDuration;
        public float NpcHoldDuration;
    }

    public struct GmNpcLockNearestPlayerMsg : NetworkMessage
    {
        public bool Locked;
    }

    public sealed class GmConsole : MonoBehaviour
    {
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 28f;

        [SerializeField] private float _playerGuardHoldDuration = 30f;
        [SerializeField] private float _npcGuardHoldDuration = 30f;

        private bool _isOpen;
        private bool _allCharactersGuarded;
        private bool _npcLockNearestPlayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnLoad()
        {
            if (FindFirstObjectByType<GmConsole>() != null) return;

            var go = new GameObject("GM Console");
            DontDestroyOnLoad(go);
            go.AddComponent<GmConsole>();
        }

        private void OnEnable()
        {
            NetworkClient.RegisterHandler<GmForceGuardMsg>(OnClientForceGuard, false);
            NetworkServer.RegisterHandler<GmForceGuardMsg>(OnServerForceGuard, false);
            NetworkServer.RegisterHandler<GmNpcLockNearestPlayerMsg>(OnServerNpcLockNearestPlayer, false);
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<GmForceGuardMsg>();
            NetworkServer.UnregisterHandler<GmForceGuardMsg>();
            NetworkServer.UnregisterHandler<GmNpcLockNearestPlayerMsg>();
        }

        private void OnGUI()
        {
            var toggleRect = new Rect(8f, 8f, ButtonWidth, ButtonHeight);
            if (GUI.Button(toggleRect, _isOpen ? "GM ^" : "GM v"))
                _isOpen = !_isOpen;

            if (!_isOpen) return;

            GUI.Box(new Rect(8f, 8f + ButtonHeight, ButtonWidth, ButtonHeight * 2f + 12f), GUIContent.none);

            string guardText = _allCharactersGuarded ? "所有角色解除防御" : "所有角色防御";
            if (GUI.Button(new Rect(12f, 12f + ButtonHeight, ButtonWidth - 8f, ButtonHeight), guardText))
                RequestToggleForceGuard();

            string npcLockText = _npcLockNearestPlayer ? "NPC解锁玩家" : "NPC锁最近玩家";
            if (GUI.Button(new Rect(12f, 16f + ButtonHeight * 2f, ButtonWidth - 8f, ButtonHeight), npcLockText))
                RequestToggleNpcLockNearestPlayer();
        }

        private void RequestToggleForceGuard()
        {
            _allCharactersGuarded = !_allCharactersGuarded;

            var msg = new GmForceGuardMsg
            {
                Guarded = _allCharactersGuarded,
                ExcludedPlayerNetId = ResolveControlledPlayerNetId(),
                PlayerHoldDuration = _playerGuardHoldDuration,
                NpcHoldDuration = _npcGuardHoldDuration
            };

            if (NetworkServer.active)
            {
                ApplyNpcGuardStateOnServer(msg);
                NetworkServer.SendToAll(msg);
                ApplyLocalPlayerGuardState(msg);
                return;
            }

            if (NetworkClient.active)
            {
                NetworkClient.Send(msg);
                return;
            }

            ApplyAllOffline(msg);
        }

        private void RequestToggleNpcLockNearestPlayer()
        {
            _npcLockNearestPlayer = !_npcLockNearestPlayer;
            var msg = new GmNpcLockNearestPlayerMsg
            {
                Locked = _npcLockNearestPlayer
            };

            if (NetworkServer.active)
            {
                ApplyNpcLockNearestPlayerOnServer(msg.Locked);
                return;
            }

            if (NetworkClient.active)
            {
                NetworkClient.Send(msg);
                return;
            }

            ApplyNpcLockNearestPlayer(msg.Locked);
        }

        private static void OnServerForceGuard(NetworkConnectionToClient conn, GmForceGuardMsg msg)
        {
            ApplyNpcGuardStateOnServer(msg);
            NetworkServer.SendToAll(msg);
        }

        private static void OnServerNpcLockNearestPlayer(NetworkConnectionToClient conn, GmNpcLockNearestPlayerMsg msg)
        {
            ApplyNpcLockNearestPlayerOnServer(msg.Locked);
        }

        private static void OnClientForceGuard(GmForceGuardMsg msg)
        {
            ApplyLocalPlayerGuardState(msg);
        }

        private static void ApplyLocalPlayerGuardState(GmForceGuardMsg msg)
        {
            if (NetworkClient.localPlayer == null) return;

            var identity = NetworkClient.localPlayer;
            if (identity.netId == msg.ExcludedPlayerNetId) return;

            var player = identity.GetComponent<PlayerController>();
            if (msg.Guarded)
                player?.ForceEnterGuard(msg.PlayerHoldDuration);
            else
                player?.ForceExitGuardToIdle();
        }

        private static void ApplyNpcGuardStateOnServer(GmForceGuardMsg msg)
        {
            if (!NetworkServer.active) return;

            if (msg.Guarded)
                ApplyNpcGuardOnServer(msg.NpcHoldDuration);
            else
                ApplyNpcExitGuardOnServer();
        }

        private static void ApplyNpcGuardOnServer(float holdDuration)
        {
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc?.ServerTryEnterGuard(holdDuration);
        }

        private static void ApplyNpcExitGuardOnServer()
        {
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc?.ForceExitGuardToIdle();
        }

        private static void ApplyNpcLockNearestPlayerOnServer(bool locked)
        {
            if (!NetworkServer.active) return;
            ApplyNpcLockNearestPlayer(locked);
        }

        private static void ApplyNpcLockNearestPlayer(bool locked)
        {
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (npc == null) continue;

                if (!locked)
                {
                    npc.ClearGmFacingTarget();
                    continue;
                }

                Transform nearestPlayer = FindNearestPlayer(npc.transform.position);
                if (nearestPlayer != null)
                    npc.SetGmFacingTarget(nearestPlayer);
                else
                    npc.ClearGmFacingTarget();
            }
        }

        private static Transform FindNearestPlayer(Vector3 from)
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Transform nearest = null;
            float nearestDistanceSq = float.PositiveInfinity;

            foreach (var player in players)
            {
                if (player == null) continue;

                float distanceSq = (player.transform.position - from).sqrMagnitude;
                if (distanceSq >= nearestDistanceSq) continue;

                nearestDistanceSq = distanceSq;
                nearest = player.transform;
            }

            return nearest;
        }

        private static void ApplyAllOffline(GmForceGuardMsg msg)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (msg.Guarded)
                    player?.ForceEnterGuard(msg.PlayerHoldDuration);
                else
                    player?.ForceExitGuardToIdle();
            }

            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (msg.Guarded)
                    npc?.ForceEnterGuard(msg.NpcHoldDuration);
                else
                    npc?.ForceExitGuardToIdle();
            }
        }

        private static uint ResolveControlledPlayerNetId()
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
                return NetworkClient.localPlayer.netId;

            return 0;
        }
    }
}
