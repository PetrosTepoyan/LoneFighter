using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LoneFighter.Player;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Sub-25% HP "this hurts" filter. Drives two URP overrides simultaneously:
    ///   - <see cref="ChromaticAberration"/>: pushes channels apart at the edges,
    ///     creating a slight retro/painful look.
    ///   - <see cref="LensDistortion"/>: pinches the edges of the screen inward,
    ///     simulating tunnel vision / heart-pounding panic.
    ///
    /// Strength ramps linearly from 0 at the trigger threshold to MAX at 0 HP.
    /// Each frame's strength is pulsed with a low-frequency sine so the warp
    /// breathes instead of sitting static.
    ///
    /// Best-effort: gracefully no-ops if the scene has no URP Volume / those
    /// specific overrides aren't on the profile.
    /// </summary>
    public class LowHpScreenWarp : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField, Range(0.05f, 0.95f)] private float thresholdFraction = 0.25f;

        [Header("URP Volume (optional — auto-resolved if empty)")]
        [SerializeField] private Volume volume;

        [Header("Chromatic Aberration")]
        [SerializeField, Range(0f, 1f)] private float caMax = 0.55f;

        [Header("Lens Distortion")]
        [SerializeField, Range(-1f, 1f)] private float distortionMax = -0.35f;

        [Header("Breath Pulse")]
        [Tooltip("Hz of the breathing pulse layered over the base intensity.")]
        [SerializeField, Min(0.05f)] private float pulseHz = 1.1f;
        [Tooltip("How much of the strength is modulated by the breath pulse.")]
        [SerializeField, Range(0f, 1f)] private float pulseDepth = 0.35f;

        private ChromaticAberration _ca;
        private LensDistortion _ld;
        private bool _resolved;

        private bool _haveCaOriginal;
        private float _caOriginalIntensity;
        private bool _caOriginalActive;

        private bool _haveLdOriginal;
        private float _ldOriginalIntensity;
        private bool _ldOriginalActive;

        private PlayerHealth _health;
        private float _hpFraction = 1f;
        private bool _active;

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
            }
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHpChanged;
                _hpFraction = _health.Max > 0f ? _health.Current / _health.Max : 1f;
            }
            Resolve();
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHpChanged;
            Restore();
        }

        private void Resolve()
        {
            if (_resolved) return;
            if (volume == null)
            {
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
            if (volume.profile.TryGet<ChromaticAberration>(out var ca))
            {
                _ca = ca;
                _caOriginalActive = ca.active;
                _caOriginalIntensity = ca.intensity.value;
                _haveCaOriginal = true;
            }
            if (volume.profile.TryGet<LensDistortion>(out var ld))
            {
                _ld = ld;
                _ldOriginalActive = ld.active;
                _ldOriginalIntensity = ld.intensity.value;
                _haveLdOriginal = true;
            }
            _resolved = true;
        }

        /// <summary>
        /// Optional manual trigger — useful when the OnHealthChanged event was
        /// already consumed elsewhere and we want to re-evaluate now.
        /// </summary>
        public void Refresh()
        {
            if (_health == null) return;
            _hpFraction = _health.Max > 0f ? _health.Current / _health.Max : 1f;
        }

        private void HandleHpChanged(float current, float max)
        {
            _hpFraction = max > 0f ? current / max : 1f;
        }

        private void Update()
        {
            Resolve();
            bool shouldBeActive = _hpFraction > 0f && _hpFraction <= thresholdFraction;

            if (!shouldBeActive)
            {
                if (_active)
                {
                    Restore();
                    _active = false;
                }
                return;
            }

            _active = true;
            float t = 1f - (_hpFraction / Mathf.Max(0.0001f, thresholdFraction));
            t = Mathf.Clamp01(t);

            // Breath pulse: sin between 0..1.
            float breath = (Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f) + 1f) * 0.5f;
            float strength = t * Mathf.Lerp(1f - pulseDepth, 1f, breath);

            if (_ca != null)
            {
                _ca.active = true;
                _ca.intensity.Override(Mathf.Lerp(_caOriginalIntensity, caMax, strength));
            }
            if (_ld != null)
            {
                _ld.active = true;
                _ld.intensity.Override(Mathf.Lerp(_ldOriginalIntensity, distortionMax, strength));
            }
        }

        private void Restore()
        {
            if (_ca != null && _haveCaOriginal)
            {
                _ca.active = _caOriginalActive;
                _ca.intensity.Override(_caOriginalIntensity);
            }
            if (_ld != null && _haveLdOriginal)
            {
                _ld.active = _ldOriginalActive;
                _ld.intensity.Override(_ldOriginalIntensity);
            }
        }
    }
}
