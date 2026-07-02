using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Character.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatActor))]
    public sealed class NpcHealthBarView : MonoBehaviour
    {
        [SerializeField] private CombatActor _actor;
        [SerializeField] private Vector3 _worldOffset = new(0f, 2.2f, 0f);
        [SerializeField] private Vector2 _barSize = new(1.25f, 0.12f);
        [SerializeField] private Color _barColor = new(0.9f, 0.05f, 0.04f, 1f);
        [SerializeField] private Color _backgroundColor = new(0f, 0f, 0f, 0.65f);
        [SerializeField] private float _damageTextRise = 0.45f;
        [SerializeField] private float _damageTextLifetime = 0.75f;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _fillRect;
        private Font _font;
        private Camera _camera;

        private void Awake()
        {
            if (_actor == null)
                _actor = GetComponent<CombatActor>();

            _camera = Camera.main;
            EnsureUi();
        }

        private void OnEnable()
        {
            if (_actor == null) return;

            _actor.HealthChanged += OnHealthChanged;
            _actor.DamageTaken += OnDamageTaken;
            OnHealthChanged(_actor.CurrentHp, _actor.MaxHp);
        }

        private void OnDisable()
        {
            if (_actor == null) return;

            _actor.HealthChanged -= OnHealthChanged;
            _actor.DamageTaken -= OnDamageTaken;
        }

        private void LateUpdate()
        {
            if (_canvasRect == null) return;

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

            if (_canvas != null)
                _canvas.enabled = currentHp < maxHp;
        }

        private void OnDamageTaken(float damage)
        {
            if (_canvasRect == null || damage <= 0f) return;

            _canvas.enabled = true;
            StartCoroutine(PlayDamageNumber(damage));
        }

        private void EnsureUi()
        {
            if (_canvas != null) return;

            _font = LoadBuiltinFont();

            var canvasGo = new GameObject("NpcHealthBar");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 20;

            _canvasRect = _canvas.GetComponent<RectTransform>();
            _canvasRect.sizeDelta = new Vector2(120f, 28f);
            _canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var background = CreateImage("Background", _canvasRect, _backgroundColor);
            background.anchorMin = new Vector2(0.5f, 0.5f);
            background.anchorMax = new Vector2(0.5f, 0.5f);
            background.pivot = new Vector2(0.5f, 0.5f);
            background.sizeDelta = new Vector2(_barSize.x * 100f, _barSize.y * 100f);
            background.anchoredPosition = Vector2.zero;

            _fillRect = CreateImage("Fill", background, _barColor);
            _fillRect.anchorMin = Vector2.zero;
            _fillRect.anchorMax = Vector2.one;
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
        }

        private IEnumerator PlayDamageNumber(float damage)
        {
            var textGo = new GameObject("DamageText");
            textGo.transform.SetParent(_canvasRect, false);

            Text text = textGo.AddComponent<Text>();
            text.raycastTarget = false;
            text.font = _font;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = _barColor;
            text.text = Mathf.CeilToInt(damage).ToString();

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80f, 28f);

            Vector2 start = new(0f, 18f);
            Vector2 end = start + new Vector2(0f, _damageTextRise * 100f);

            float elapsed = 0f;
            float lifetime = Mathf.Max(0.01f, _damageTextLifetime);
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                text.color = new Color(_barColor.r, _barColor.g, _barColor.b, 1f - t);
                yield return null;
            }

            Destroy(textGo);
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;

            return image.rectTransform;
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
