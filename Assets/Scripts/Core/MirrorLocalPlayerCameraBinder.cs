using Character.LockOn;
using System.Collections;
using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 从 Mirror 的本地玩家生命周期接线场景相机，避免远端镜像争用同一个 Cinemachine Rig。
    /// </summary>
    public sealed class MirrorLocalPlayerCameraBinder : NetworkBehaviour
    {
        [Header("Camera Binding")]
        [SerializeField] private string _followPointTpName = "FollowPointTP";
        [SerializeField] private string _followPointLockOnName = "FollowPointLockOn";
        [SerializeField] private string _lookAtPointName = "LookAtPoint";
        [SerializeField] private float _retryIntervalSeconds = 0.2f;
        [SerializeField] private int _maxRetryCount = 30;
        [SerializeField] private bool _logBinding = true;

        // ============ Mirror 生命周期 ============

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            StartCoroutine(BindWhenCameraReady());
        }

        // ============ 延迟绑定 ============

        /// <summary>
        /// 场景相机和网络玩家可能分帧创建，因此使用有上限的重试而不是依赖不稳定的生成顺序。
        /// </summary>
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

        /// <summary>
        /// 只在 isLocalPlayer 后绑定，并允许等待场景相机出现，避免远端 Player 抢占唯一 Camera Rig。
        /// </summary>
        private bool TryBind()
        {
            Transform followTp = ResolveChildPoint(_followPointTpName);
            Transform followLockOn = ResolveChildPoint(_followPointLockOnName);
            Transform lookTarget = ResolveChildPoint(_lookAtPointName);
            if (followTp == null && followLockOn == null && lookTarget == null)
                return false;

            var rig = GetComponent<PlayerCameraRigController>();
            if (rig == null)
                rig = gameObject.AddComponent<PlayerCameraRigController>();

            if (GetComponent<LockOnReticleView>() == null)
                gameObject.AddComponent<LockOnReticleView>();

            var reticle = GetComponent<LockOnReticleView>();
            reticle.EnableForLocalPlayer();

            return rig.TryInitialize(followTp, followLockOn, lookTarget);
        }

        // ============ 层级查询 ============

        /// <summary>
        /// 优先使用明确命名的跟随点，缺失时回退角色根节点，保证旧预制体仍可运行。
        /// </summary>
        private Transform ResolveChildPoint(string pointName)
        {
            if (string.IsNullOrEmpty(pointName))
                return null;

            Transform direct = transform.Find(pointName);
            if (direct != null)
                return direct;

            foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name == pointName)
                    return t;
            }

            if (_logBinding)
                Debug.LogWarning($"[MirrorLocalPlayerCameraBinder] No child named '{pointName}' under player.");

            return null;
        }
    }
}
