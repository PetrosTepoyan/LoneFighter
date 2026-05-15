using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LoneFighter.Enemies;

namespace LoneFighter.UI.EnemyHud
{
    /// <summary>
    /// Screen-locked boss health bar pinned to the top of the HUD. Bigger than the
    /// per-enemy bar and gold-tinted. Always visible while a boss is alive; hidden
    /// otherwise. Not gated by <see cref="EnemyHudSettings.ShowEnemyHealthBars"/>.
    ///
    /// Binding model — three options, in priority order:
    ///   1. External system calls <see cref="Bind"/> with a specific <see cref="EnemyHealth"/>.
    ///   2. If <see cref="autoDiscoverByKeyword"/> is true and nothing is bound, this
    ///      component polls <see cref="EnemyRegistry"/> each frame for an enemy whose
    ///      data tags / name match <see cref="bossKeywords"/> (or carries a <see cref="WardenBoss"/>).
    ///   3. Otherwise the bar stays hidden.
    ///
    /// Like <see cref="EnemyHealthBar"/>, we POLL <see cref="EnemyHealth.Current"/> for
    /// damage changes — <see cref="EnemyHealth"/> only exposes <c>OnDied</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealthBar : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Fill image driven by Current/Max.")]
        [SerializeField] private Image fillImage;
        [Tooltip("Background/track image (darker outline).")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Optional TMP_Text rendered above the bar (e.g. \"WARDEN BOSS\").")]
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("CanvasGroup used to fade the whole widget. Auto-grabbed if null.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Color applied to the fill — golden by default.")]
        [SerializeField] private Color fillTint = new Color(1f, 0.84f, 0.27f, 1f);

        [Header("Fade")]
        [Tooltip("Seconds to fade in once a boss is bound.")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [Tooltip("Seconds to fade out once the bound boss dies.")]
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Header("Auto-Discovery")]
        [Tooltip("If true and no target is set, scan EnemyRegistry every frame for a boss.")]
        [SerializeField] private bool autoDiscoverByKeyword = true;
        [Tooltip("Case-insensitive substrings used to match EnemyData asset/displayName when auto-discovering.")]
        [SerializeField] private string[] bossKeywords = new[] { "boss", "warden" };

        // --- Runtime state -------------------------------------------------------
        private EnemyHealth _target;
        private float _lastSeenCurrent;
        private bool _diedHandlerHooked;
        private bool _bossAlive;

        // -------------------------------------------------------------------------

        /// <summary>Currently bound boss, or null.</summary>
        public EnemyHealth Target => _target;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (fillImage != null) fillImage.color = fillTint;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnDisable()
        {
            UnhookTarget();
        }

        /// <summary>
        /// Bind to a specific boss. Pass null to clear. When already bound to a live boss
        /// this is a no-op for the current target unless <paramref name="health"/> differs.
        /// </summary>
        public void Bind(EnemyHealth health)
        {
            if (_target == health) return;
            UnhookTarget();

            _target = health;
            if (_target == null)
            {
                _bossAlive = false;
                return;
            }

            _lastSeenCurrent = _target.Current;
            _target.OnDied += HandleTargetDied;
            _diedHandlerHooked = true;
            _bossAlive = true;
            UpdateFill();
        }

        private void UnhookTarget()
        {
            if (_diedHandlerHooked && _target != null) _target.OnDied -= HandleTargetDied;
            _diedHandlerHooked = false;
            _target = null;
        }

        private void HandleTargetDied(EnemyHealth _)
        {
            _bossAlive = false;
        }

        /// <summary>
        /// Programmatically set the displayed name (e.g. "WARDEN BOSS"). No-op if the
        /// <see cref="nameLabel"/> field is unwired.
        /// </summary>
        public void SetDisplayName(string text)
        {
            if (nameLabel != null) nameLabel.text = text != null ? text : string.Empty;
        }

        private void Update()
        {
            // 1. Auto-discovery: if we have no live target and the flag is on, scan.
            if ((_target == null || !_bossAlive) && autoDiscoverByKeyword)
            {
                TryAutoDiscover();
            }

            // 2. Damage polling (cheap: one float compare per frame).
            if (_target != null)
            {
                float current = _target.Current;
                if (!Mathf.Approximately(current, _lastSeenCurrent))
                {
                    _lastSeenCurrent = current;
                    UpdateFill();
                }
            }

            // 3. Fade logic.
            float targetAlpha = (_target != null && _bossAlive) ? 1f : 0f;
            float speed = targetAlpha > canvasGroup.alpha
                ? 1f / Mathf.Max(0.01f, fadeInDuration)
                : 1f / Mathf.Max(0.01f, fadeOutDuration);
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.deltaTime);

            // 4. Once we've fully faded out a dead boss, drop the binding so the auto-discovery
            //    is free to pick up the next one without flicker.
            if (!_bossAlive && _target != null && canvasGroup.alpha <= 0.001f)
            {
                UnhookTarget();
            }
        }

        private void TryAutoDiscover()
        {
            var enemies = EnemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                if (!MatchesBoss(enemy)) continue;
                var health = enemy.GetComponent<EnemyHealth>();
                if (health == null) continue;

                Bind(health);
                if (nameLabel != null && enemy.Data != null && !string.IsNullOrEmpty(enemy.Data.displayName))
                {
                    nameLabel.text = enemy.Data.displayName.ToUpperInvariant();
                }
                return;
            }
        }

        private bool MatchesBoss(EnemyBase enemy)
        {
            if (enemy.GetComponent<WardenBoss>() != null) return true;
            if (bossKeywords == null) return false;
            var data = enemy.Data;
            if (data == null) return false;
            string assetName = data.name != null ? data.name.ToLowerInvariant() : string.Empty;
            string displayName = data.displayName != null ? data.displayName.ToLowerInvariant() : string.Empty;
            for (int i = 0; i < bossKeywords.Length; i++)
            {
                var k = bossKeywords[i];
                if (string.IsNullOrEmpty(k)) continue;
                k = k.ToLowerInvariant();
                if (assetName.Contains(k) || displayName.Contains(k)) return true;
            }
            return false;
        }

        private void UpdateFill()
        {
            if (fillImage == null || _target == null) return;
            float max = Mathf.Max(0.0001f, _target.Max);
            fillImage.fillAmount = Mathf.Clamp01(_target.Current / max);
        }
    }
}
