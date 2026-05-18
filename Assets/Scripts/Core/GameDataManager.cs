using Character.Config;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// Loads <see cref="GameDataCatalog"/> at boot; gameplay code reads config only through here.
    /// Place on a scene object that exists before characters spawn (e.g. NetSystem / GameManager).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameDataManager : MonoBehaviour
    {
        public static GameDataManager Instance { get; private set; }

        [SerializeField] private GameDataCatalog _catalog;

        public CharacterDefinition Player => _catalog.player;
        public CharacterDefinition Npc => _catalog.npc;
        public NetworkSyncConfig NetworkSync => _catalog.networkSync;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
