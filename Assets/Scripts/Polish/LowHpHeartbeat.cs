using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LoneFighter.Player;

namespace LoneFighter.Polish
{
    /// Pulses a red vignette and fires Light haptics when player HP is below threshold.
    /// Gracefully no-ops if no URP Volume / Vignette override is found in the scene.
    public class LowHpHeartbeat : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField, Range(0.05f, 0.95f)] private float lowHpFraction = 0.3f;

        [Header("Vignette")]
        [SerializeField] private Volume volume;
        [SerializeField, Range(0f, 1f)] private float baseIntensity = 0.15f;
        [SerializeField, Range(0f, 1f)] private float peakIntensity = 0.55f;
        [SerializeField] private Color heartbeatColor = new Color(0.9f, 0.05f, 0.05f);
        [SerializeField, Min(0.1f)] private float pulseHz = 1.4f; // ~84bpm

        [Header("Haptics")]
        [SerializeField] private bool emitHaptics = true;
        [SerializeField, Min(0.25f)] private float hapticInterval = 1.5f;

        private PlayerHealth _health;
        private Vignette _vignette;
        private float _hpFraction = 1f;
        private float _nextHapticTime;
        private bool _vignetteOriginalActive;
        private float _vignetteOriginalIntensity;
        private Color _vignetteOriginalColor;
        private bool _capturedOriginal;
        private bool _active;

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
            }
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _hpFraction = _health.Max > 0f ? _health.Current / _health.Max : 1f;
            }

            ResolveVignette();
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            RestoreVignette();
        }

        private void ResolveVignette()
        {
            if (volume == null)
            {
                // Best-effort lookup: any global Volume in the scene.
                var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
                for (int i = 0; i < volumes.Length; i++)
                {
                    if (volumes[i] != null && volumes[i].profile != null)
                    {
                        volume = volumes[i];
                        break;
                    }
                }
            }
            if (volume == null || volume.profile == null) return;
            if (volume.profile.TryGet<Vignette>(out var v))
            {
                _vignette = v;
                _vignetteOriginalActive = _vignette.active;
                _vignetteOriginalIntensity = _vignette.intensity.value;
                _vignetteOriginalColor = _vignette.color.value;
                _capturedOriginal = true;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            _hpFraction = max > 0f ? current / max : 1f;
        }

        private void Update()
        {
            bool isLow = _hpFraction <= lowHpFraction && _hpFraction > 0f;
            if (isLow)
            {
                if (!_active) _active = true;
                DrivePulse();
                if (emitHaptics && Time.unscaledTime >= _nextHapticTime)
                {
                    _nextHapticTime = Time.unscaledTime + hapticInterval;
                    if (HapticsService.Instance != null) HapticsService.Instance.Pulse(HapticIntensity.Light);
                }
            }
            else if (_active)
            {
                _active = false;
                RestoreVignette();
            }
        }

        private void DrivePulse()
        {
            if (_vignette == null) return;
            float s = (Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(baseIntensity, peakIntensity, s);
            _vignette.active = true;
            _vignette.intensity.Override(intensity);
            _vignette.color.Override(heartbeatColor);
        }

        private void RestoreVignette()
        {
            if (_vignette == null || !_capturedOriginal) return;
            _vignette.active = _vignetteOriginalActive;
            _vignette.intensity.Override(_vignetteOriginalIntensity);
            _vignette.color.Override(_vignetteOriginalColor);
        }
    }
}
