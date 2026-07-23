using System.Collections;
using Character.Combat;
using Character.Controller;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Character.LockOn
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLockOnController))]
    public sealed class LockOnTargetHudView : MonoBehaviour
    {
        [SerializeField] private PlayerLockOnController _lockOn;
        [SerializeField] private GameObject _hudPrefab;
        [SerializeField] private GameObject _damageTextPrefab;
        [SerializeField] private Vector2 _screenOffset = new(0f, 42f);
        [SerializeField] private float _damageTextRise = 0.45f;
        [SerializeField] private float _damageTextLifetime = 0.75f;
        [SerializeField] private string _barRootPath = "HealthBar";
        [SerializeField] private string _fillPath = "HealthBar/Background/Fill";

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _barRoot;
        private RectTransform _fillRect;
        private Font _font;
        private Camera _camera;
        private NetworkIdentity _networkIdentity;
        private ILockOnTarget _boundTarget;
        private CombatActor _boundActor;
        private PlayerController _boundPlayer;
        private float _lastObservedPlayerHp = -1f;

        private void Awake()
        {
            if (_lockOn == null)
                _lockOn = GetComponent<PlayerLockOnController>();
            _networkIdentity = GetComponent<NetworkIdentity>();
            _camera = Camera.main;
            _font = LoadBuiltinFont();
        }

        private void OnDisable()
        {
            UnbindTarget();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            UnbindTarget();

            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void LateUpdate()
        {
            if (!ShouldRenderForThisPlayer())
            {
                UnbindTarget();
                SetVisible(false);
                return;
            }

            EnsureUi();
            if (_canvasRect == null || _lockOn == null || !_lockOn.IsLockOnActive)
            {
                UnbindTarget();
                SetVisible(false);
                return;
            }

            ILockOnTarget target = _lockOn.CurrentLockOnTarget;
            Transform lockPoint = _lockOn.CurrentTarget;
            if (target == null || lockPoint == null)
            {
                UnbindTarget();
                SetVisible(false);
                return;
            }

            if (!ReferenceEquals(_boundTarget, target))
                BindTarget(target);

            if (_boundActor == null && _boundPlayer == null)
            {
                SetVisible(false);
                return;
            }

            UpdateScreenPosition(lockPoint);
        }

        private bool ShouldRenderForThisPlayer()
        {
            if (!NetworkClient.active)
                return true;

            return _networkIdentity != null && _networkIdentity.isLocalPlayer;
        }

        private void EnsureUi()
        {
            if (_canvas != null)
                return;

            if (_hudPrefab == null)
            {
                Debug.LogWarning($"{nameof(LockOnTargetHudView)} on {name} has no HUD prefab.", this);
                return;
            }

            GameObject hud = Instantiate(_hudPrefab);
            hud.name = _hudPrefab.name;

            _canvas = hud.GetComponentInChildren<Canvas>(true);
            if (_canvas == null)
            {
                Debug.LogWarning($"{nameof(LockOnTargetHudView)} HUD prefab has no Canvas.", this);
                Destroy(hud);
                return;
            }

            _canvasRect = _canvas.GetComponent<RectTransform>();
            Transform barRoot = _canvasRect.Find(_barRootPath);
            if (barRoot != null)
                _barRoot = barRoot as RectTransform;

            Transform fill = _canvasRect.Find(_fillPath);
            if (fill != null)
                _fillRect = fill as RectTransform;

            if (_barRoot == null || _fillRect == null)
                Debug.LogWarning($"{nameof(LockOnTargetHudView)} HUD prefab paths are invalid.", this);

            SetVisible(false);
        }

        private void BindTarget(ILockOnTarget target)
        {
            UnbindTarget();
            _boundTarget = target;
            _lastObservedPlayerHp = -1f;

            Transform root = target.Root;
            if (root == null)
                return;

            _boundPlayer = root.GetComponentInParent<PlayerController>();
            if (_boundPlayer != null)
            {
                _boundPlayer.HealthChanged += OnPlayerHealthChanged;
                if (_boundPlayer.CurrentHp > 0f || _boundPlayer.MaxHp > 1f)
                    OnPlayerHealthChanged(_boundPlayer.CurrentHp, _boundPlayer.MaxHp);
                SetVisible(true);
                return;
            }

            _boundActor = root.GetComponentInParent<CombatActor>();
            if (_boundActor != null)
            {
                _boundActor.HealthChanged += OnActorHealthChanged;
                _boundActor.DamageTaken += OnTargetDamageTaken;
                OnActorHealthChanged(_boundActor.CurrentHp, _boundActor.MaxHp);
                SetVisible(true);
            }
        }

        private void UnbindTarget()
        {
            if (_boundPlayer != null)
                _boundPlayer.HealthChanged -= OnPlayerHealthChanged;

            if (_boundActor != null)
            {
                _boundActor.HealthChanged -= OnActorHealthChanged;
                _boundActor.DamageTaken -= OnTargetDamageTaken;
            }

            _boundTarget = null;
            _boundActor = null;
            _boundPlayer = null;
            _lastObservedPlayerHp = -1f;
        }

        private void OnActorHealthChanged(float currentHp, float maxHp)
        {
            UpdateHealth(currentHp, maxHp);
        }

        private void OnPlayerHealthChanged(float currentHp, float maxHp)
        {
            if (_lastObservedPlayerHp >= 0f && currentHp < _lastObservedPlayerHp)
                OnTargetDamageTaken(_lastObservedPlayerHp - currentHp);

            _lastObservedPlayerHp = currentHp;
            UpdateHealth(currentHp, maxHp);
        }

        private void UpdateHealth(float currentHp, float maxHp)
        {
            if (_fillRect == null)
                return;

            float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
            _fillRect.anchorMax = new Vector2(ratio, 1f);
            SetVisible(_lockOn != null && _lockOn.IsLockOnActive);
        }

        private void OnTargetDamageTaken(float damage)
        {
            if (_barRoot == null || damage <= 0f || _damageTextPrefab == null)
                return;

            StartCoroutine(PlayDamageNumber(damage));
        }

        private void UpdateScreenPosition(Transform lockPoint)
        {
            if (_barRoot == null)
                return;

            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 screenPoint = _camera.WorldToScreenPoint(lockPoint.position);
            if (screenPoint.z <= 0f)
            {
                SetVisible(false);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            _barRoot.anchoredPosition = localPoint + _screenOffset;
            SetVisible(true);
        }

        private IEnumerator PlayDamageNumber(float damage)
        {
            GameObject textGo = Instantiate(_damageTextPrefab, _barRoot);
            textGo.name = _damageTextPrefab.name;

            Text text = textGo.GetComponentInChildren<Text>(true);
            if (text == null)
            {
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

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
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
