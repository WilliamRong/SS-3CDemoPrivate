using Character.Combat;
using Character.Controller;
using Character.StateMachine;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Character.Presentation
{
    /// <summary>
    /// 只绑定当前本地玩家的生命与架势事件，使同一场景中的远端 Player 不会争用屏幕 HUD。
    /// </summary>
    public sealed class PlayerHealthHudView : MonoBehaviour
    {
        private const float BindRetryInterval = 0.25f;

        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _fillImage;
        [SerializeField] private RectTransform _postureBar;
        [SerializeField] private Image _postureFillImage;
        [SerializeField] private Outline _postureBreakOutline;
        [SerializeField] private Text _healthText;
        [SerializeField] private float _fillLerpSpeed = 12f;
        [SerializeField, Min(0f)] private float _postureBreakEmphasisDuration = 1f;
        [SerializeField, Min(1f)] private float _postureBreakEmphasisScale = 1.2f;
        [SerializeField] private bool _hideWhenUnbound = true;

        private PlayerController _player;
        private CombatActor _actor;
        private float _targetRatio = 1f;
        private float _displayRatio = 1f;
        private float _postureRatio;
        private float _postureBreakEmphasisUntil = float.NegativeInfinity;
        private float _bindRetryTimer;
        private CharacterStateId _lastObservedStateId = CharacterStateId.None;
        private bool _isPostureBreakEmphasisActive;

        // ============ Unity 生命周期 ============

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

        /// <summary>
        /// 使用未缩放时间驱动绑定重试和 UI 插值，暂停战斗时间时 HUD 仍能完成网络绑定与崩防强调。
        /// </summary>
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

            UpdatePostureBreakPresentation();

            // Health can be applied before this HUD finishes binding during
            // Mirror player spawn. Reconcile the displayed value with the
            // authoritative PlayerController so a missed event cannot leave
            // the HUD at the old HP.
            if (_player != null)
            {
                float maxHp = Mathf.Max(1f, _player.MaxHp);
                float ratio = Mathf.Clamp01(_player.CurrentHp / maxHp);
                if (!Mathf.Approximately(ratio, _targetRatio))
                    OnHealthChanged(_player.CurrentHp, maxHp);
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

        // ============ 本地玩家绑定 ============

        /// <summary>
        /// Mirror 生成本地玩家存在时序差异，因此允许 HUD 低频重试绑定，而不是依赖场景初始化顺序。
        /// </summary>
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
            _actor = _player.GetComponent<CombatActor>();
            _player.HealthChanged += OnHealthChanged;
            if (_actor != null)
                _actor.PostureChanged += OnPostureChanged;

            SetVisible(true);
            OnHealthChanged(_player.CurrentHp, _player.MaxHp);
            if (_actor != null)
                OnPostureChanged(_actor.CurrentPosture, _actor.MaxPosture);

            _lastObservedStateId = CharacterStateId.None;
            UpdatePostureBreakPresentation();
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

        private void Unbind()
        {
            if (_player != null)
                _player.HealthChanged -= OnHealthChanged;
            if (_actor != null)
                _actor.PostureChanged -= OnPostureChanged;

            _player = null;
            _actor = null;
            _lastObservedStateId = CharacterStateId.None;
            ResetPosturePresentation();
        }

        // ============ 数值事件 ============

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

        private void OnPostureChanged(float currentPosture, float maxPosture)
        {
            _postureRatio = maxPosture > 0f
                ? Mathf.Clamp01(currentPosture / maxPosture)
                : 0f;

            if (_isPostureBreakEmphasisActive)
                return;

            SetPostureFill(_postureRatio);

            SetPostureBarVisible(_postureRatio > 0f);
        }

        // ============ UI 引用与可见性 ============

        private void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        /// <summary>
        /// 支持 Inspector 显式绑定和既有预制体路径回退，避免仅为整理 UI 强制迁移所有场景序列化数据。
        /// </summary>
        private void CacheReferences()
        {
            if (_canvas == null)
                _canvas = GetComponentInChildren<Canvas>(true);
            if (_fillImage == null)
                _fillImage = transform.Find("PlayerHealth/HealthBar/Fill")?.GetComponent<Image>();
            if (_postureBar == null)
            {
                Transform postureBarTransform =
                    transform.Find("PostureBar") ??
                    transform.Find("PlayerHealth/PostureBar");
                _postureBar = postureBarTransform as RectTransform;
            }
            if (_postureFillImage == null)
                _postureFillImage = _postureBar?.Find("Fill")?.GetComponent<Image>();
            if (_postureBreakOutline == null)
                _postureBreakOutline = _postureBar?.Find("Background")?.GetComponent<Outline>();
            if (_healthText == null)
                _healthText = GetComponentInChildren<Text>(true);

            if (_fillImage != null)
            {
                _fillImage.type = Image.Type.Filled;
                _fillImage.fillMethod = Image.FillMethod.Horizontal;
                _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                _fillImage.fillAmount = _displayRatio;
            }

            if (_postureFillImage != null)
            {
                _postureFillImage.type = Image.Type.Simple;
                _postureFillImage.fillAmount = 1f;
            }

            ResetPosturePresentation();

            if (_healthText != null && _healthText.font == null)
                _healthText.font = LoadBuiltinFont();
        }

        // ============ 崩防强调 ============

        /// <summary>
        /// 监听状态进入边沿而不是架势归零值，确保满槽被权威重置后仍能完整显示一秒崩防反馈。
        /// </summary>
        private void UpdatePostureBreakPresentation()
        {
            if (_player != null)
            {
                CharacterStateId currentStateId = _player.CurrentStateId;
                if (currentStateId == CharacterStateId.PostureBroken &&
                    _lastObservedStateId != CharacterStateId.PostureBroken)
                {
                    BeginPostureBreakEmphasis();
                }

                _lastObservedStateId = currentStateId;
            }

            if (!_isPostureBreakEmphasisActive ||
                Time.unscaledTime < _postureBreakEmphasisUntil)
            {
                return;
            }

            EndPostureBreakEmphasis();
        }

        /// <summary>
        /// 崩防期间冻结为满槽放大表现，不让随后到达的架势归零事件提前隐藏反馈。
        /// </summary>
        private void BeginPostureBreakEmphasis()
        {
            if (_postureBar == null || _postureFillImage == null)
                return;

            _postureRatio = 0f;
            _isPostureBreakEmphasisActive = true;
            _postureBreakEmphasisUntil =
                Time.unscaledTime + Mathf.Max(0f, _postureBreakEmphasisDuration);
            SetPostureFill(1f);
            _postureBar.localScale =
                Vector3.one * Mathf.Max(1f, _postureBreakEmphasisScale);
            if (_postureBreakOutline != null)
                _postureBreakOutline.enabled = true;
            SetPostureBarVisible(true);
        }

        private void EndPostureBreakEmphasis()
        {
            _isPostureBreakEmphasisActive = false;
            _postureBreakEmphasisUntil = float.NegativeInfinity;

            if (_postureBar != null)
                _postureBar.localScale = Vector3.one;
            SetPostureFill(_postureRatio);
            if (_postureBreakOutline != null)
                _postureBreakOutline.enabled = false;

            SetPostureBarVisible(_player != null && _postureRatio > 0f);
        }

        // ============ 架势条绘制 ============

        private void ResetPosturePresentation()
        {
            _postureRatio = 0f;
            _isPostureBreakEmphasisActive = false;
            _postureBreakEmphasisUntil = float.NegativeInfinity;

            if (_postureBar != null)
                _postureBar.localScale = Vector3.one;
            SetPostureFill(0f);
            if (_postureBreakOutline != null)
                _postureBreakOutline.enabled = false;

            SetPostureBarVisible(false);
        }

        private void SetPostureBarVisible(bool visible)
        {
            if (_postureBar != null && _postureBar.gameObject.activeSelf != visible)
                _postureBar.gameObject.SetActive(visible);
        }

        private void SetPostureFill(float ratio)
        {
            if (_postureFillImage == null)
                return;

            float halfWidth = Mathf.Clamp01(ratio) * 0.5f;
            RectTransform fillRect = _postureFillImage.rectTransform;
            fillRect.anchorMin = new Vector2(0.5f - halfWidth, 0f);
            fillRect.anchorMax = new Vector2(0.5f + halfWidth, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        // ============ 字体回退 ============

        private static Font LoadBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return font;
        }
    }
}
