using UnityEngine;

namespace Character.Config
{
    [CreateAssetMenu(fileName = "NetworkSyncConfig", menuName = "SS3C/Character/Network Sync Config")]
    public sealed class NetworkSyncConfig : ScriptableObject
    {
        [Header("Tick")]
        public int tickRate = 20;
        public int maxTicksPerFrame = 8;

        [Header("Snapshot Publish")]
        public float minPosDeltaToSend = 0.001f;
        public float minYawDeltaToSend = 0.1f;
        public int snapshotBufferMaxSize = 64;

        [Header("Remote Interpolation")]
        public float bufferDelaySec = 0.12f;
        public float largeErrorThreshold = 1.5f;
        public float snapLerpSpeed = 12f;
        public float normalLerpSpeed = 8f;

        [Header("Fake Network (Debug)")]
        public float baseLatencyMs = 80f;
        public float jitterMs = 20f;
        [Range(0f, 1f)]
        public float packetLossRate = 0.02f;
    }
}
