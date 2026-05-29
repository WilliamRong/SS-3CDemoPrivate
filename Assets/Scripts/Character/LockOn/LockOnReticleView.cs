using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Character.LockOn
{
    /// <summary>
    /// Screen-space lock reticle projected from the target LockPoint.
    /// Updates only after Cinemachine moves the camera; canvas is not parented to the player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerLockOnController))]
    public sealed class LockOnReticleView : MonoBehaviour
    {
        [SerializeField] private PlayerLockOnController _lockOn;
        [SerializeField] private Sprite _reticleSprite;
        [SerializeField] private float _reticleSize = 22f;
        [SerializeField] private Color _reticleColor = Color.white;
        [SerializeField] private float _maxJumpPixels = 120f;

        private RectTransform _canvasRect;
        private RectTransform _reticleRect;
        private Image _reticleImage;
        private bool _uiBuilt;
        private bool _isLocalPlayer;
        private bool _hasValidPosition;
        private Vector2 _lastAnchoredPosition;
        private Transform _trackedLockPoint;

        private void Awake()
        {
            if (_lockOn == null)
                _lockOn = GetComponent<PlayerLockOnController>();

            enabled = false;
        }

        public void EnableForLocalPlayer()
        {
            _isLocalPlayer = true;
            enabled = true;
            EnsureUi();
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDestroy()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);

            if (_canvasRect != null)
                Destroy(_canvasRect.gameObject);
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            if (!_isLocalPlayer || brain == null)
                return;

            UpdateReticle(brain.OutputCamera);
        }

        private void UpdateReticle(Camera camera)
        {
            if (!_uiBuilt || _lockOn == null)
                return;

            Transform lockPoint = _lockOn.CurrentTarget;
            if (!_lockOn.IsLockOnActive || lockPoint == null || camera == null)
            {
                _trackedLockPoint = null;
                _hasValidPosition = false;
                SetReticleVisible(false);
                return;
            }

            if (_trackedLockPoint != lockPoint)
            {
                _trackedLockPoint = lockPoint;
                _hasValidPosition = false;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(lockPoint.position);
            if (screenPoint.z <= 0f)
            {
                SetReticleVisible(false);
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            if (_hasValidPosition)
            {
                float jump = Vector2.Distance(localPoint, _lastAnchoredPosition);
                if (jump > _maxJumpPixels)
                    return;
            }

            _lastAnchoredPosition = localPoint;
            _hasValidPosition = true;
            _reticleRect.anchoredPosition = localPoint;
            SetReticleVisible(true);
        }

        private void EnsureUi()
        {
            if (_uiBuilt)
                return;

            if (_reticleSprite == null)
                _reticleSprite = LockOnReticleSprite.CreateDefault();

            var canvasGo = new GameObject("LockOnReticleCanvas");

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = false;

            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            _canvasRect = canvas.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;

            var reticleGo = new GameObject("Reticle");
            reticleGo.transform.SetParent(_canvasRect, false);

            _reticleImage = reticleGo.AddComponent<Image>();
            _reticleImage.sprite = _reticleSprite;
            _reticleImage.color = _reticleColor;
            _reticleImage.raycastTarget = false;
            _reticleImage.maskable = false;

            _reticleRect = _reticleImage.rectTransform;
            _reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
            _reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
            _reticleRect.pivot = new Vector2(0.5f, 0.5f);
            _reticleRect.sizeDelta = new Vector2(_reticleSize, _reticleSize);

            SetReticleVisible(false);
            _uiBuilt = true;
        }

        private void SetReticleVisible(bool visible)
        {
            if (_reticleImage != null)
                _reticleImage.enabled = visible;
        }
    }

    internal static class LockOnReticleSprite
    {
        private static Sprite _cached;

        public static Sprite CreateDefault()
        {
            if (_cached != null)
                return _cached;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LockOnReticleDot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float coreRadius = size * 0.12f;
            float edgeRadius = size * 0.22f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = 1f;
                    if (distance > coreRadius)
                        alpha = 1f - Mathf.InverseLerp(coreRadius, edgeRadius, distance);

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            _cached = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);

            return _cached;
        }
    }
}
