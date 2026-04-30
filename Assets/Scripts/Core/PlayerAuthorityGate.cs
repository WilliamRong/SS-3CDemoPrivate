using Mirror;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Centralized authority check for local input ownership.
    /// </summary>
    public sealed class PlayerAuthorityGate : NetworkBehaviour
    {
        public bool CanProcessLocalInput
        {
            get
            {
                // Keep offline workflow unchanged.
                if (!NetworkClient.active) return true;
                return isLocalPlayer;
            }
        }
    }
}
