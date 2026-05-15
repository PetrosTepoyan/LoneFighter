using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Applies a brief unscaled-time freeze when the player takes a HEAVY hit,
    /// defined as &gt; <see cref="heavyThresholdFraction"/> of their max HP lost
    /// in a single damage event.
    ///
    /// Two paths:
    ///   1. <see cref="HitFeedbackService.OnPlayerHit"/> calls <see cref="NotifyDamage"/>
    ///      with the raw damage. This is the authoritative path.
    ///   2. Fallback: if no one's notifying us, we subscribe to
    ///      <see cref="PlayerHealth.OnHealthChanged"/> and detect HP drops the
    ///      same way <see cref="HitListener"/> does for enemies. This lets the
    ///      effect kick in even if external systems forget to call the facade.
    ///
    /// Hit-stop dispatch goes through <see cref="FxService.HitStop"/> when
    /// available so we don't fight with the project's existing time-scale
    /// manager (it correctly captures/restores prior Time.timeScale across
    /// nested freezes). When FxService is missing we drive Time.timeScale
    /// ourselves with a coroutine.
    /// </summary>
    public class PlayerHitFreezeFrame : MonoBehaviour
    {
        [Tooltip("A hit counts as 'heavy' when it removes at least this fraction of max HP in one event.")]
        [SerializeField, Range(0.01f, 1f)] private float heavyThresholdFraction = 0.20f;
        [Tooltip("Freeze duration in seconds (unscaled).")]
        [SerializeField, Min(0f)] private float freezeSeconds = 0.08f;

        private PlayerHealth _health;
        private float _lastSeenHp = -1f;
        private float _selfFreezeRemaining;
        private float _capturedTimeScale = 1f;
        private bool _selfFreezing;

        private void OnEnable()
        {
            ResolveHealth();
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHpChanged;
            if (_selfFreezing) RestoreTimeScale();
        }

        private void ResolveHealth()
        {
            if (_health != null) return;
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
            }
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHpChanged;
                _lastSeenHp = _health.Current;
            }
        }

        private void Update()
        {
            // Lazy resolve in case the player wasn't spawned at OnEnable time.
            if (_health == null) ResolveHealth();

            if (_selfFreezing)
            {
                _selfFreezeRemaining -= Time.unscaledDeltaTime;
                if (_selfFreezeRemaining <= 0f) RestoreTimeScale();
            }
        }

        private void HandleHpChanged(float current, float max)
        {
            if (_lastSeenHp < 0f)
            {
                _lastSeenHp = current;
                return;
            }
            float dropped = _lastSeenHp - current;
            _lastSeenHp = current;
            if (dropped > 0f) NotifyDamage(dropped);
        }

        /// <summary>
        /// Authoritative entry point: pass the actual damage value taken this
        /// frame and we'll trigger a freeze if it crosses the heavy threshold.
        /// </summary>
        public void NotifyDamage(float damage)
        {
            if (_health == null) ResolveHealth();
            if (_health == null) return;
            float max = _health.Max;
            if (max <= 0f) return;
            float frac = damage / max;
            if (frac < heavyThresholdFraction) return;
            TriggerFreeze();
        }

        private void TriggerFreeze()
        {
            if (freezeSeconds <= 0f) return;

            if (FxService.Instance != null)
            {
                FxService.Instance.HitStop(freezeSeconds);
                return;
            }

            // Self-managed fallback.
            if (!_selfFreezing)
            {
                _capturedTimeScale = Time.timeScale;
                _selfFreezing = true;
            }
            _selfFreezeRemaining = freezeSeconds;
            Time.timeScale = 0f;
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = _capturedTimeScale;
            _selfFreezing = false;
            _selfFreezeRemaining = 0f;
        }
    }
}
