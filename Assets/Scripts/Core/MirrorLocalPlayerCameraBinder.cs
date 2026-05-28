using System.Collections;
using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Binds scene Cinemachine rigs to the local network player.
    /// Triggered from Mirror local-player lifecycle callback.
    /// </summary>
    public sealed class MirrorLocalPlayerCameraBinder : NetworkBehaviour
    {
        [Header("Camera Binding")]
        [SerializeField] private string _lookAtPointName = "LookAtPoint";
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
                        Debug.Log("[MirrorLocalPlayerCameraBinder] Bound player camera rig.");
                    yield break;
                }

                yield return new WaitForSeconds(_retryIntervalSeconds);
            }

            if (_logBinding)
                Debug.LogWarning("[MirrorLocalPlayerCameraBinder] Failed to bind camera rig.");
        }

        private bool TryBind()
        {
            Transform lookTarget = ResolveLookAtPoint();
            if (lookTarget == null)
                return false;

            var rig = GetComponent<PlayerCameraRigController>();
            if (rig == null)
                rig = gameObject.AddComponent<PlayerCameraRigController>();

            return rig.TryInitialize(lookTarget);
        }

        private Transform ResolveLookAtPoint()
        {
            if (string.IsNullOrEmpty(_lookAtPointName))
                return transform;

            Transform direct = transform.Find(_lookAtPointName);
            if (direct != null)
                return direct;

            foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name == _lookAtPointName)
                    return t;
            }

            if (_logBinding)
                Debug.LogWarning($"[MirrorLocalPlayerCameraBinder] No child named '{_lookAtPointName}' under player.");

            return null;
        }
    }
}
