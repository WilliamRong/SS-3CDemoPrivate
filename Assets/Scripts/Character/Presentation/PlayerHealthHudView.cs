using Character.Controller;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Character.Presentation
{
    public sealed class PlayerHealthHudView : MonoBehaviour
    {
        private const float BindRetryInterval = 0.25f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Text _healthText;
        [SerializeField] private float _fillLerpSpeed = 12f;
        [SerializeField] private bool _hideWhenUnbound = true;

        private PlayerController _player;
        private float _targetRatio = 1f;
        private float _displayRatio = 1f;
        private float _bindRetryTimer;

        private void Awake()
        {
            CacheReferences();
            SetVisible(!_hideWhenUnbound);
        }

        private void OnEnable()
        {
            _bindRetryTimer = 0f;
            TryBindPlayer();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (_player == null)
            {
                _bindRetryTimer -= Time.unscaledDeltaTime;
                if (_bindRetryTimer <= 0f)
                {
                    _bindRetryTimer = BindRetryInterval;
                    TryBindPlayer();
                }
            }

            if (_fillImage != null)
            {
                _displayRatio = Mathf.MoveTowards(
                    _displayRatio,
                    _targetRatio,
                    Mathf.Max(0.01f, _fillLerpSpeed) * Time.unscaledDeltaTime);
                _fillImage.fillAmount = _displayRatio;
            }
        }

        private void TryBindPlayer()
        {
            PlayerController player = ResolveLocalPlayer();
            if (_player != null && player == _player) return;

            Unbind();

            if (player == null)
            {
                SetVisible(false);
                return;
            }

            _player = player;
            _player.HealthChanged += OnHealthChanged;
            SetVisible(true);
            OnHealthChanged(_player.CurrentHp, _player.MaxHp);
        }

        private static PlayerController ResolveLocalPlayer()
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                var localPlayer = NetworkClient.localPlayer.GetComponent<PlayerController>();
                if (localPlayer != null)
                    return localPlayer;
            }

            if (!NetworkClient.active)
                return FindFirstObjectByType<PlayerController>();

            return null;
        }

        private void OnHealthChanged(float currentHp, float maxHp)
        {
            float safeMax = Mathf.Max(1f, maxHp);
            float clampedCurrent = Mathf.Clamp(currentHp, 0f, safeMax);
            _targetRatio = Mathf.Clamp01(clampedCurrent / safeMax);

            if (_fillImage != null && (_canvas == null || !_canvas.enabled))
            {
                _displayRatio = _targetRatio;
                _fillImage.fillAmount = _displayRatio;
            }

            if (_healthText != null)
                _healthText.text = $"{Mathf.CeilToInt(clampedCurrent)}/{Mathf.CeilToInt(safeMax)}";
        }

        private void Unbind()
        {
            if (_player != null)
                _player.HealthChanged -= OnHealthChanged;

            _player = null;
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        private void CacheReferences()
        {
            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>(true);
            if (_fillImage == null)
                _fillImage = transform.Find("PlayerHealth/HealthBar/Fill")?.GetComponent<Image>();
            if (_healthText == null)
                _healthText = GetComponentInChildren<Text>(true);

            if (_fillImage != null)
            {
                _fillImage.type = Image.Type.Filled;
                _fillImage.fillMethod = Image.FillMethod.Horizontal;
                _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                _fillImage.fillAmount = _displayRatio;
            }

            if (_healthText != null && _healthText.font == null)
                _healthText.font = LoadBuiltinFont();
        }

        private static Font LoadBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return font;
        }
    }
}
