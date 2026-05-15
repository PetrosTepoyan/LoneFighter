using System.Collections;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Polish
{
    /// Eases Time.timeScale down to ~0.4 over 0.1s, holds, eases back to 1.0.
    /// Uses unscaled time for the easing so the ramp itself completes.
    /// Composes safely: re-trigger extends the hold and adopts the deeper intensity, never restarts the down-ease.
    public class SlowMoTrigger : MonoBehaviour
    {
        public static SlowMoTrigger Instance { get; private set; }

        [Header("Defaults")]
        [SerializeField, Range(0.05f, 1f)] private float defaultIntensity = 0.4f;
        [SerializeField, Min(0.0f)] private float defaultHoldSeconds = 0.15f;
        [SerializeField, Min(0.01f)] private float easeInSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float easeOutSeconds = 0.25f;

        [Header("Auto Triggers")]
        [SerializeField] private bool autoTriggerOnLevelUp = true;
        [SerializeField] private bool autoTriggerOnLowHpEntry = true;
        [SerializeField, Range(0.05f, 0.95f)] private float lowHpFraction = 0.3f;

        private Coroutine _routine;
        private float _activeIntensity = 1f;
        private float _holdRemaining;
        private bool _inActiveRamp;

        private PlayerHealth _health;
        private PlayerLevel _level;
        private bool _wasLowHp;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
        }

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_health != null) _health.OnHealthChanged += HandleHealthChanged;
            if (_level != null) _level.OnLevelUp += HandleLevelUp;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (!autoTriggerOnLowHpEntry || max <= 0f) return;
            bool nowLow = (current / max) <= lowHpFraction && current > 0f;
            if (nowLow && !_wasLowHp)
            {
                TriggerSlowMo(defaultIntensity, defaultHoldSeconds);
            }
            _wasLowHp = nowLow;
        }

        private void HandleLevelUp(int level)
        {
            if (!autoTriggerOnLevelUp) return;
            TriggerSlowMo(defaultIntensity, defaultHoldSeconds);
        }

        public void TriggerSlowMo(float intensity, float holdSeconds)
        {
            intensity = Mathf.Clamp(intensity, 0.05f, 1f);
            holdSeconds = Mathf.Max(0f, holdSeconds);

            // Don't fight pause/level-up modal timescales.
            if (GameManager.Instance != null &&
                GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            if (_inActiveRamp)
            {
                // Adopt the deeper intensity and extend the hold.
                _activeIntensity = Mathf.Min(_activeIntensity, intensity);
                _holdRemaining = Mathf.Max(_holdRemaining, holdSeconds);
                Time.timeScale = Mathf.Min(Time.timeScale, _activeIntensity);
                return;
            }

            _activeIntensity = intensity;
            _holdRemaining = holdSeconds;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _inActiveRamp = true;
            float startScale = Time.timeScale;

            // Ease in.
            float t = 0f;
            while (t < easeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / easeInSeconds);
                Time.timeScale = Mathf.Lerp(startScale, _activeIntensity, k);
                yield return null;
            }
            Time.timeScale = _activeIntensity;

            // Hold (may be extended by repeated triggers).
            while (_holdRemaining > 0f)
            {
                _holdRemaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            // Ease out.
            float fromScale = Time.timeScale;
            t = 0f;
            while (t < easeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / easeOutSeconds);
                Time.timeScale = Mathf.Lerp(fromScale, 1f, k);
                yield return null;
            }
            Time.timeScale = 1f;
            _inActiveRamp = false;
            _routine = null;
        }
    }
}
