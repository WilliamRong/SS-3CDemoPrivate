using AI;
using Character.Combat;
using Character.Controller;
using Character.StateMachine;
using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 显式携带排除的本地玩家，避免 Host 在服务器和客户端两条路径上重复进入防御。
    /// </summary>
    public struct GmForceGuardMsg : NetworkMessage
    {
        public bool Guarded;
        public uint ExcludedPlayerNetId;
        public float PlayerHoldDuration;
        public float NpcHoldDuration;
    }

    /// <summary>
    /// 锁定开关只发送意图，最近目标始终由服务器按当前世界状态选择。
    /// </summary>
    public struct GmNpcLockNearestPlayerMsg : NetworkMessage
    {
        public bool Locked;
    }

    /// <summary>
    /// 由服务器把 NPC 与远端 Player 的架势提高到接近崩防，
    /// 排除发出命令的本地 Player。
    /// </summary>
    public struct GmSetPostureNearBreakMsg : NetworkMessage
    {
        public uint ExcludedPlayerNetId;
        public float TargetRatio;
        public float RecoveryDelay;
    }

    public struct GmReviveAllNpcsMsg : NetworkMessage
    {
        public bool Requested;
    }

    /// <summary>
    /// 让同一套调试入口覆盖 Offline、Host 和 Client，同时保持在线状态修改由服务器决定。
    /// </summary>
    public sealed class GmConsole : MonoBehaviour
    {
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 28f;
        private const float Margin = 8f;
        private const float PanelPadding = 4f;

        [SerializeField] private float _playerGuardHoldDuration = 30f;
        [SerializeField] private float _npcGuardHoldDuration = 30f;
        [SerializeField, Range(0f, 0.999f)]
        private float _nearBreakPostureRatio = 0.99f;
        [SerializeField, Min(0f)]
        private float _nearBreakPostureRecoveryDelay = 30f;

        private bool _isOpen;
        private bool _allCharactersGuarded;
        private bool _npcLockNearestPlayer;

        // ============ 生命周期与网络注册 ============

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
            NetworkServer.RegisterHandler<GmSetPostureNearBreakMsg>(OnServerSetPostureNearBreak, false);
            NetworkServer.RegisterHandler<GmReviveAllNpcsMsg>(OnServerReviveAllNpcs, false);
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<GmForceGuardMsg>();
            NetworkServer.UnregisterHandler<GmForceGuardMsg>();
            NetworkServer.UnregisterHandler<GmNpcLockNearestPlayerMsg>();
            NetworkServer.UnregisterHandler<GmSetPostureNearBreakMsg>();
            NetworkServer.UnregisterHandler<GmReviveAllNpcsMsg>();
        }

        /// <summary>
        /// 从屏幕右下角反向布局，使分辨率变化时调试入口仍保持固定边距且不会遮挡主要 HUD。
        /// </summary>
        private void OnGUI()
        {
            float panelHeight = ButtonHeight * 4f + PanelPadding * 5f;
            float x = Mathf.Max(Margin, Screen.width - ButtonWidth - Margin);
            float toggleY = Mathf.Max(Margin, Screen.height - ButtonHeight - Margin);
            float panelY = Mathf.Max(Margin, toggleY - panelHeight);

            var toggleRect = new Rect(x, toggleY, ButtonWidth, ButtonHeight);
            if (GUI.Button(toggleRect, _isOpen ? "GM ^" : "GM v"))
                _isOpen = !_isOpen;

            if (!_isOpen) return;

            GUI.Box(new Rect(x, panelY, ButtonWidth, panelHeight), GUIContent.none);

            string guardText = _allCharactersGuarded ? "所有角色解除防御" : "所有角色防御";
            if (GUI.Button(new Rect(x + PanelPadding, panelY + PanelPadding, ButtonWidth - PanelPadding * 2f, ButtonHeight), guardText))
                RequestToggleForceGuard();

            string npcLockText = _npcLockNearestPlayer ? "NPC解锁玩家" : "NPC锁最近玩家";
            if (GUI.Button(new Rect(x + PanelPadding, panelY + ButtonHeight + PanelPadding * 2f, ButtonWidth - PanelPadding * 2f, ButtonHeight), npcLockText))
                RequestToggleNpcLockNearestPlayer();

            if (GUI.Button(
                    new Rect(
                        x + PanelPadding,
                        panelY + ButtonHeight * 2f + PanelPadding * 3f,
                        ButtonWidth - PanelPadding * 2f,
                        ButtonHeight),
                    "NPC/远端架势99%"))
            {
                RequestSetPostureNearBreak();
            }

            if (GUI.Button(
                    new Rect(
                        x + PanelPadding,
                        panelY + ButtonHeight * 3f + PanelPadding * 4f,
                        ButtonWidth - PanelPadding * 2f,
                        ButtonHeight),
                    "所有NPC复活"))
            {
                RequestReviveAllNpcs();
            }
        }

        // ============ 本地请求入口 ============

        /// <summary>
        /// 按当前运行模式选择唯一发送路径，避免 Host 同时走 Client.Send 和服务器直调。
        /// </summary>
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

        /// <summary>
        /// 联机调试命令统一由服务器选目标，避免每个 Client 根据各自场景位置得到不同最近玩家。
        /// </summary>
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

        /// <summary>
        /// Host 直接走服务器权威，Client 只发送意图；Offline 只处理 NPC。
        /// </summary>
        private void RequestSetPostureNearBreak()
        {
            var msg = new GmSetPostureNearBreakMsg
            {
                ExcludedPlayerNetId = ResolveControlledPlayerNetId(),
                TargetRatio = _nearBreakPostureRatio,
                RecoveryDelay = _nearBreakPostureRecoveryDelay,
            };

            if (NetworkServer.active)
            {
                ApplyPostureNearBreakOnServer(msg);
                return;
            }

            if (NetworkClient.active)
            {
                NetworkClient.Send(msg);
                return;
            }

            ApplyNpcPostureNearBreakOffline(
                msg.TargetRatio,
                msg.RecoveryDelay);
        }

        private static void RequestReviveAllNpcs()
        {
            if (NetworkServer.active)
            {
                ApplyReviveAllNpcsOnAuthority();
                return;
            }

            if (NetworkClient.active)
            {
                NetworkClient.Send(new GmReviveAllNpcsMsg
                {
                    Requested = true,
                });
                return;
            }

            ApplyReviveAllNpcsOnAuthority();
        }

        // ============ 网络消息处理 ============

        private static void OnServerForceGuard(NetworkConnectionToClient conn, GmForceGuardMsg msg)
        {
            ApplyNpcGuardStateOnServer(msg);
            NetworkServer.SendToAll(msg);
        }

        private static void OnServerNpcLockNearestPlayer(NetworkConnectionToClient conn, GmNpcLockNearestPlayerMsg msg)
        {
            ApplyNpcLockNearestPlayerOnServer(msg.Locked);
        }

        private static void OnServerSetPostureNearBreak(
            NetworkConnectionToClient conn,
            GmSetPostureNearBreakMsg msg)
        {
            if (conn?.identity == null)
                return;

            msg.ExcludedPlayerNetId = conn.identity.netId;
            msg.TargetRatio = Mathf.Clamp(msg.TargetRatio, 0f, 0.999f);
            msg.RecoveryDelay = Mathf.Clamp(msg.RecoveryDelay, 0f, 300f);
            ApplyPostureNearBreakOnServer(msg);
        }

        private static void OnServerReviveAllNpcs(
            NetworkConnectionToClient conn,
            GmReviveAllNpcsMsg msg)
        {
            if (conn?.identity == null || !msg.Requested)
                return;

            ApplyReviveAllNpcsOnAuthority();
        }

        private static void OnClientForceGuard(GmForceGuardMsg msg)
        {
            ApplyLocalPlayerGuardState(msg);
        }

        // ============ 防御状态应用 ============

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
                npc?.SetGmGuardOverride(true, holdDuration);
        }

        private static void ApplyNpcExitGuardOnServer()
        {
            var npcs = FindObjectsByType<NpcCharacterDriver>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
                npc?.SetGmGuardOverride(false);
        }

        // ============ 架势调试应用 ============

        private static void ApplyPostureNearBreakOnServer(
            GmSetPostureNearBreakMsg msg)
        {
            if (!NetworkServer.active)
                return;

            CombatActor[] actors = FindObjectsByType<CombatActor>(
                FindObjectsSortMode.None);

            foreach (CombatActor actor in actors)
            {
                if (!CanPrimePosture(actor))
                    continue;

                if (actor.IsPlayerActor)
                {
                    NetworkIdentity identity =
                        actor.GetComponent<NetworkIdentity>();

                    if (identity != null &&
                        identity.netId == msg.ExcludedPlayerNetId)
                    {
                        continue;
                    }
                }

                RaisePostureToRatio(
                    actor,
                    msg.TargetRatio,
                    msg.RecoveryDelay);
            }
        }

        private static void ApplyNpcPostureNearBreakOffline(
            float targetRatio,
            float recoveryDelay)
        {
            CombatActor[] actors = FindObjectsByType<CombatActor>(
                FindObjectsSortMode.None);

            foreach (CombatActor actor in actors)
            {
                if (CanPrimePosture(actor) && !actor.IsPlayerActor)
                {
                    RaisePostureToRatio(
                        actor,
                        targetRatio,
                        recoveryDelay);
                }
            }
        }

        private static bool CanPrimePosture(CombatActor actor)
        {
            return actor != null &&
                   !actor.IsDead &&
                   actor.CurrentStateId is not (
                       CharacterStateId.PostureBroken or
                       CharacterStateId.Executing or
                       CharacterStateId.Executed or
                       CharacterStateId.Dead);
        }

        /// <summary>
        /// 只提高不降低架势，并保持未满，确保下一次正常架势伤害触发崩防。
        /// </summary>
        private static void RaisePostureToRatio(
            CombatActor actor,
            float targetRatio,
            float recoveryDelay)
        {
            float maxPosture = actor.MaxPosture;
            if (maxPosture <= 0f)
                return;

            float clampedRatio = Mathf.Clamp(targetRatio, 0f, 0.999f);
            float targetPosture = maxPosture * clampedRatio;
            actor.RaisePostureTo(targetPosture, recoveryDelay);
        }

        private static void ApplyReviveAllNpcsOnAuthority()
        {
            NpcCharacterDriver[] npcs = FindObjectsByType<NpcCharacterDriver>(
                FindObjectsSortMode.None);

            foreach (NpcCharacterDriver npc in npcs)
                npc?.TryReviveForGm();
        }

        // ============ NPC 目标应用 ============

        private static void ApplyNpcLockNearestPlayerOnServer(bool locked)
        {
            if (!NetworkServer.active) return;
            ApplyNpcLockNearestPlayer(locked);
        }

        /// <summary>
        /// 为每个 NPC 独立选择最近玩家，避免多人场景把所有 AI 强制指向同一个全局目标。
        /// </summary>
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

        /// <summary>
        /// 排除无效和已销毁对象后按平方距离比较，调试命令无需为开方支付额外成本。
        /// </summary>
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

        // ============ 离线兼容与查询 ============

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
