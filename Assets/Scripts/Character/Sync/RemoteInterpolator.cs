using Character.Config;
using Core;
using Mirror;
using Character.Combat;
using UnityEngine;

namespace Character.Sync
{
    public class RemoteInterpolator : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RemoteSnapshotBuffer _buffer;

        [Header("Debug")]
        [SerializeField] private bool _logState;

        private CombatActor _combatActor;

        private NetworkSyncConfig Sync => GameDataManager.Instance.NetworkSync;

        private NetworkIdentity _networkIdentity;

        public StateSnapshot LastAppliedSnapshot { get; private set; }
        public float LastPosError { get; private set; }

        private void Awake()
        {
            if (_buffer == null) _buffer = GetComponent<RemoteSnapshotBuffer>();
            _networkIdentity = GetComponent<NetworkIdentity>();
            _combatActor = GetComponent<CombatActor>();
        }

        public void TickInterpolation()
        {
            if (_buffer == null || _buffer.Count == 0) return;

            float targetTime = Time.unscaledTime - Sync.bufferDelaySec;

            if (!_buffer.TrySampleByArrivalTime(targetTime, out var from, out var to, out float t))
                return;

            bool isLocalPlayer = _networkIdentity != null && _networkIdentity.isLocalPlayer;

            // 所属 Client 只接收自己的数值纠正，绝不能插值自身位置。
            if (!isLocalPlayer)
            {
                Vector3 targetPos;
                float targetYaw;

                if (from.Tick == to.Tick)
                {
                    targetPos = from.Position;
                    targetYaw = from.Yaw;
                }
                else
                {
                    targetPos = Vector3.Lerp(from.Position, to.Position, t);
                    targetYaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
                }

                LastPosError = Vector3.Distance(transform.position, targetPos);

                float lerpSpeed = LastPosError > Sync.largeErrorThreshold
                    ? Sync.snapLerpSpeed
                    : Sync.normalLerpSpeed;
                float k = Mathf.Clamp01(lerpSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, k);

                var curEuler = transform.rotation.eulerAngles;
                float yaw = Mathf.LerpAngle(curEuler.y, targetYaw, k);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            else
            {
                LastPosError = 0f;
            }

            LastAppliedSnapshot = to;

            // Dedicated Server 和 Host 都已经持有权威 HP，不能重复覆盖。
            if (!NetworkServer.active && _combatActor != null)
            {
                if (to.HasAuthoritativeHealth != 0)
                {
                    _combatActor.ApplyAuthoritativeHealth(
                        to.CurrentHp,
                        to.MaxHp,
                        to.HealthRevision);
                }

                if (to.HasAuthoritativePosture != 0)
                {
                    _combatActor.ApplyAuthoritativePosture(
                        to.CurrentPosture,
                        to.MaxPosture);
                }
            }

            if (_logState)
            {
                Debug.Log($"[RemoteInterp] targetTime={targetTime}, from={from.Tick}, to={to.Tick}, err={LastPosError:F2}");
            }
        }
    }
}
