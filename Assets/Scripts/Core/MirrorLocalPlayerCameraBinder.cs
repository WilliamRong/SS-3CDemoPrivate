using System.Collections;
using Cinemachine;
using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Binds scene freelook camera to local network player.
    /// Triggered from Mirror local-player lifecycle callback.
    /// </summary>
    public sealed class MirrorLocalPlayerCameraBinder : NetworkBehaviour
    {
        [Header("Camera Binding")]
        [SerializeField] private string _cameraPath = "Cameras/TP";
        [SerializeField] private float _retryIntervalSeconds = 0.2f;
        [SerializeField] private int _maxRetryCount = 30;
        [SerializeField] private bool _logBinding = true;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            StartCoroutine(BindWhenCameraReady());
        }

        private IEnumerator BindWhenCameraReady()
        {
            for (int i = 0; i < _maxRetryCount; i++)
            {
                if (TryBind())
                {
                    if (_logBinding)
                        Debug.Log("[MirrorLocalPlayerCameraBinder] Bound Follow/LookAt to local player.");
                    yield break;
                }

                yield return new WaitForSeconds(_retryIntervalSeconds);
            }

            if (_logBinding)
                Debug.LogWarning($"[MirrorLocalPlayerCameraBinder] Failed to bind camera at path '{_cameraPath}'.");
        }

        private bool TryBind()
        {
            GameObject tp = GameObject.Find(_cameraPath);
            if (tp == null) return false;

            CinemachineFreeLook freelook = tp.GetComponent<CinemachineFreeLook>();
            if (freelook == null) return false;

            freelook.Follow = transform;
            freelook.LookAt = transform;
            return true;
        }
    }
}
