using UnityEngine;

namespace Character.Config
{
    /// <summary>
    /// 集中快照频率、插值和假网络参数，保证发布端与接收端使用一致的时间尺度和阈值。
    /// </summary>
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
