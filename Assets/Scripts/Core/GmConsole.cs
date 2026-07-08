using AI;
using Character.Controller;
using Mirror;
using UnityEngine;
namespace Core
{
    public struct GmForceGuardMsg : NetworkMessage
    {
        public uint ExcludedPlayerNetId;
        public float PlayerHoldDuration;
        public float NpcHoldDuration;
    }
    public sealed class GmConsole : MonoBehaviour
    {
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 28f;
        [SerializeField] private float _playerGuardHoldDuration = 30f;
        [SerializeField] private float _npcGuardHoldDuration = 30f;
        private bool _isOpen;
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
        }
        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<GmForceGuardMsg>();
            NetworkServer.UnregisterHandler<GmForceGuardMsg>();
        }
        private void OnGUI()
        {
            var toggleRect = new Rect(8f, 8f, ButtonWidth, ButtonHeight);
            if (GUI.Button(toggleRect, _isOpen ? "GM ^" : "GM v"))
                _isOpen = !_isOpen;
            if (!_isOpen) return;
            GUI.Box(new Rect(8f, 8f + ButtonHeight, ButtonWidth, ButtonHeight + 8f), GUIContent.none);
            if (GUI.Button(new Rect(12f, 12f + ButtonHeight, ButtonWidth - 8f, ButtonHeight), "所有角色防御"))
                RequestForceGuard();
        }
        private void RequestForceGuard()
        {
            var msg = new GmForceGuardMsg
            {
                ExcludedPlayerNetId = ResolveControlledPlayerNetId(),
                PlayerHoldDuration = _playerGuardHoldDuration,
                NpcHoldDuration = _npcGuardHoldDuration
            };
            if (NetworkServer.active)
            {
                ApplyNpcGuardOnServer(msg.NpcHoldDuration);
                NetworkServer.SendToAll(msg);
                ApplyLocalPlayerGuard(msg);
                return;
            }
            if (NetworkClient.active)
            {
                NetworkClient.Send(msg);
                return;
            }
            ApplyAllOffline(msg);
        }
        private static void OnServerForceGuard(NetworkConnectionToClient conn, GmForceGuardMsg msg)
        {
            ApplyNpcGuardOnServer(msg.NpcHoldDuration);
            NetworkServer.SendToAll(msg);
        }
        private static void OnClientForceGuard(GmForceGuardMsg msg)
        {
            ApplyLocalPlayerGuard(msg);
        }
        private static void ApplyLocalPlayerGuard(GmForceGuardMsg msg)
        {
            if (NetworkClient.localPlayer == null) return;
            var identity = NetworkClient.localPlayer;
            if (identity.netId == msg.ExcludedPlayerNetId) return;
            var player = identity.GetComponent<PlayerController>();
            player?.ForceEnterGuard(msg.PlayerHoldDuration);
        }
        private static void ApplyNpcGuardOnServer(float holdDuration)
        {
            if (!NetworkServer.active) return;
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc?.ServerTryEnterGuard(holdDuration);
        }
        private static void ApplyAllOffline(GmForceGuardMsg msg)
        {
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var player in players)
                player?.ForceEnterGuard(msg.PlayerHoldDuration);
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc?.ForceEnterGuard(msg.NpcHoldDuration);
        }
        private static uint ResolveControlledPlayerNetId()
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
                return NetworkClient.localPlayer.netId;
            return 0;
        }
    }
}