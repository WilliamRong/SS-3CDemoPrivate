using System.Collections;
using Character.Controller;
using Character.Sync;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Character.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatActor))]
    public sealed class NpcHealthBarView : MonoBehaviour
    {
        [SerializeField] private CombatActor _actor;
        [SerializeField] private GameObject _healthBarPrefab;
        [SerializeField] private GameObject _damageTextPrefab;
        [SerializeField] private Vector3 _worldOffset = new(0f, 2.2f, 0f);
        [SerializeField] private float _damageTextRise = 0.45f;
        [SerializeField] private float _damageTextLifetime = 0.75f;
        [SerializeField, Min(0.1f)] private float _visibleAfterHitDuration = 3f;
        [SerializeField] private string _fillPath = "Background/Fill";
        [SerializeField] private string _postureFillPath = "PostureBackground/Fill";
        [SerializeField, Min(0f)] private float _postureBreakOutlineDuration = 1f;
        [SerializeField] private bool _hideForLocalPlayer = true;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _fillRect;
        private RectTransform _postureFillRect;
        private Outline _postureBreakOutline;
        private Font _font;
        private Camera _camera;
        private NetworkIdentity _networkIdentity;
        private PlayerController _playerController;
        private RemoteActionApplier _remoteActionApplier;
        private float _lastObservedPlayerHp = -1f;
        private float _postureBreakOutlineUntil = float.NegativeInfinity;
        private float _visibleUntil = float.NegativeInfinity;
        private int _lastObservedPostureBreakSeqId;
        private bool _isLockOnVisible;

        private void Awake()
        {
            if (_actor == null)
                _actor = GetComponent<CombatActor>();
            _networkIdentity = GetComponent<NetworkIdentity>();
            _playerController = GetComponent<PlayerController>();
            _remoteActionApplier = GetComponent<RemoteActionApplier>();

            _camera = Camera.main;
            EnsureUi();
        }

        private void OnEnable()
        {
            _visibleUntil = float.NegativeInfinity;
            _lastObservedPostureBreakSeqId =
                _remoteActionApplier != null
                    ? _remoteActionApplier.LastPostureBreakSeqId
                    : 0;
            ResetPostureBreakOutline();
            SetCanvasVisible(false);

            if (ShouldHideForLocalPlayer())
                return;

            _lastObservedPlayerHp = -1f;

            if (_actor != null)
            {
                _actor.PostureChanged += OnPostureChanged;
                OnPostureChanged(_actor.CurrentPosture, _actor.MaxPosture);
            }

            if (_playerController != null)
            {
                _playerController.HealthChanged += OnPlayerHealthChanged;
                if (_playerController.CurrentHp > 0f || _playerController.MaxHp > 1f)
                    OnPlayerHealthChanged(_playerController.CurrentHp, _playerController.MaxHp);
                return;
            }

            if (_actor == null) return;

            _actor.HealthChanged += OnHealthChanged;
            _actor.DamageTaken += OnDamageTaken;
            OnHealthChanged(_actor.CurrentHp, _actor.MaxHp);
        }

        private void OnDisable()
        {
            _visibleUntil = float.NegativeInfinity;
            ResetPostureBreakOutline();
            SetCanvasVisible(false);

            if (_playerController != null)
                _playerController.HealthChanged -= OnPlayerHealthChanged;

            if (_actor == null) return;

            _actor.PostureChanged -= OnPostureChanged;
            _actor.HealthChanged -= OnHealthChanged;
            _actor.DamageTaken -= OnDamageTaken;
        }

        private void LateUpdate()
        {
            if (_canvasRect == null) return;
            if (ShouldHideForLocalPlayer())
            {
                SetCanvasVisible(false);
                return;
            }

            UpdatePostureBreakOutline();

            bool shouldShow = _isLockOnVisible || Time.unscaledTime < _visibleUntil;
            SetCanvasVisible(shouldShow);
            if (!shouldShow)
                return;

            if (_camera == null)
                _camera = Camera.main;

            Transform canvasTransform = _canvasRect.transform;
            canvasTransform.position = transform.position + _worldOffset;

            if (_camera != null)
            {
                Vector3 toCamera = canvasTransform.position - _camera.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                    canvasTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }

        private void OnHealthChanged(float currentHp, float maxHp)
        {
            if (_fillRect == null) return;

            float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            _fillRect.anchorMax = new Vector2(ratio, 1f);
        }

        private void OnPostureChanged(float currentPosture, float maxPosture)
        {
            if (_postureFillRect == null) return;

            float ratio = maxPosture > 0f
                ? Mathf.Clamp01(currentPosture / maxPosture)
                : 0f;
            SetPostureFill(ratio);

            if (ratio >= 1f)
                BeginPostureBreakOutline();
        }

        private void OnDamageTaken(float damage)
        {
            if (_canvasRect == null || damage <= 0f) return;
            if (ShouldHideForLocalPlayer()) return;

            ShowForHit();
            StartCoroutine(PlayDamageNumber(damage));
        }

        public void ShowForHit()
        {
            if (_canvasRect == null || ShouldHideForLocalPlayer())
                return;

            _visibleUntil = Time.unscaledTime + Mathf.Max(0.1f, _visibleAfterHitDuration);
            SetCanvasVisible(true);
        }

        public void SetLockOnVisible(bool visible)
        {
            if (_isLockOnVisible == visible)
                return;

            _isLockOnVisible = visible;
            bool shouldShow =
                !ShouldHideForLocalPlayer() &&
                (visible || Time.unscaledTime < _visibleUntil);
            SetCanvasVisible(shouldShow);
        }

        private void OnPlayerHealthChanged(float currentHp, float maxHp)
        {
            if (_lastObservedPlayerHp >= 0f && currentHp < _lastObservedPlayerHp)
                OnDamageTaken(_lastObservedPlayerHp - currentHp);

            _lastObservedPlayerHp = currentHp;
            OnHealthChanged(currentHp, maxHp);
        }

        private void SetCanvasVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        private bool ShouldHideForLocalPlayer()
        {
            if (!_hideForLocalPlayer || _playerController == null)
                return false;

            if (!NetworkClient.active)
                return true;

            return _networkIdentity == null || _networkIdentity.isLocalPlayer;
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;

            _font = LoadBuiltinFont();

            if (_healthBarPrefab != null)
            {
                GameObject healthBarInstance = Instantiate(_healthBarPrefab, transform);
                healthBarInstance.name = _healthBarPrefab.name;
            }

            _canvas = GetComponentInChildren<Canvas>(true);
            if (_canvas == null)
            {
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} has no health bar prefab/canvas.", this);
                return;
            }

            _canvasRect = _canvas.GetComponent<RectTransform>();
            Transform fill = _canvasRect.Find(_fillPath);
            if (fill != null)
                _fillRect = fill as RectTransform;

            Transform postureFill = _canvasRect.Find(_postureFillPath);
            if (postureFill != null)
                _postureFillRect = postureFill as RectTransform;
            if (_postureFillRect != null)
                _postureBreakOutline = _postureFillRect.parent.GetComponent<Outline>();

            ResetPostureBreakOutline();

            if (_fillRect == null)
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} cannot find fill rect at '{_fillPath}'.", this);
            if (_postureFillRect == null)
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} cannot find posture fill rect at '{_postureFillPath}'.", this);
            if (_postureBreakOutline == null)
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} has no posture break outline.", this);
        }

        private void UpdatePostureBreakOutline()
        {
            if (_remoteActionApplier != null)
            {
                int postureBreakSeqId =
                    _remoteActionApplier.LastPostureBreakSeqId;
                if (postureBreakSeqId > 0 &&
                    postureBreakSeqId != _lastObservedPostureBreakSeqId)
                {
                    _lastObservedPostureBreakSeqId = postureBreakSeqId;
                    BeginPostureBreakOutline();
                }
            }

            if (_postureBreakOutline != null &&
                _postureBreakOutline.enabled &&
                Time.unscaledTime >= _postureBreakOutlineUntil)
            {
                ResetPostureBreakOutline();
            }
        }

        private void BeginPostureBreakOutline()
        {
            if (_postureBreakOutline == null ||
                _postureBreakOutline.enabled)
            {
                return;
            }

            _postureBreakOutline.enabled = true;
            _postureBreakOutlineUntil =
                Time.unscaledTime + Mathf.Max(0f, _postureBreakOutlineDuration);
            ShowForHit();
        }

        private void ResetPostureBreakOutline()
        {
            _postureBreakOutlineUntil = float.NegativeInfinity;
            if (_postureBreakOutline != null)
                _postureBreakOutline.enabled = false;
        }

        private void SetPostureFill(float ratio)
        {
            if (_postureFillRect == null)
                return;

            float halfWidth = Mathf.Clamp01(ratio) * 0.5f;
            _postureFillRect.anchorMin = new Vector2(0.5f - halfWidth, 0f);
            _postureFillRect.anchorMax = new Vector2(0.5f + halfWidth, 1f);
            _postureFillRect.offsetMin = Vector2.zero;
            _postureFillRect.offsetMax = Vector2.zero;
        }

        private IEnumerator PlayDamageNumber(float damage)
        {
            if (_damageTextPrefab == null)
            {
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} has no damage text prefab.", this);
                yield break;
            }

            GameObject textGo = Instantiate(_damageTextPrefab, _canvasRect);
            textGo.name = _damageTextPrefab.name;

            Text text = textGo.GetComponentInChildren<Text>(true);
            if (text == null)
            {
                Debug.LogWarning($"{nameof(NpcHealthBarView)} on {name} damage text prefab has no Text component.", this);
                Destroy(textGo);
                yield break;
            }

            if (text.font == null)
                text.font = _font;

            Color baseColor = text.color;
            text.raycastTarget = false;
            text.text = Mathf.CeilToInt(damage).ToString();

            RectTransform rect = textGo.GetComponent<RectTransform>();
            if (rect == null)
                rect = text.rectTransform;

            Vector2 start = new(0f, 18f);
            Vector2 end = start + new Vector2(0f, _damageTextRise * 100f);
            rect.anchoredPosition = start;

            float elapsed = 0f;
            float lifetime = Mathf.Max(0.01f, _damageTextLifetime);
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * (1f - t));
                yield return null;
            }

            Destroy(textGo);
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
