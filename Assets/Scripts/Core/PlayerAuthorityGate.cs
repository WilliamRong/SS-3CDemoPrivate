using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 集中封装 Mirror 输入所有权，并明确保留未启动网络时的本地控制路径。
    /// </summary>
    public sealed class PlayerAuthorityGate : NetworkBehaviour
    {
        public bool CanProcessLocalInput
        {
            get
            {
                // 离线测试没有 NetworkIdentity 所有权，必须显式放行。
                if (!NetworkClient.active) return true;
                return isLocalPlayer;
            }
        }
    }
}
