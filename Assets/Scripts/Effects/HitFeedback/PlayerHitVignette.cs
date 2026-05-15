using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Drives a red <see cref="Vignette"/> override on a URP <see cref="Volume"/>
    /// for a fraction of a second whenever the player gets hit. Each pulse
    /// snaps the vignette to peak, then fades back to the captured original
    /// values.
    ///
    /// Best-effort: if no Volume / Vignette is present in the scene the
    /// component cleanly no-ops. We never throw on missing dependencies — the
    /// component is meant to be drop-in.
    ///
    /// Coexists with <c>LowHpHeartbeat</c> (which drives the same vignette
    /// for low-HP pulsing): we restore to whatever value the vignette had when
    /// our pulse started, so a heartbeat in progress won't be permanently
    /// overwritten by our pulse — it simply gets a flash on top.
    /// </summary>
    public class PlayerHitVignette : MonoBehaviour
    {
        [Header("URP Volume (optional — auto-resolved if empty)")]
        [SerializeField] private Volume volume;

        [Header("Pulse Shape")]
        [Tooltip("Total pulse duration (seconds, unscaled).")]
        [SerializeField, Min(0.01f)] private float duration = 0.35f;
        [Tooltip("Fraction of duration spent climbing to peak.")]
        [SerializeField, Range(0.05f, 0.5f)] private float attackFraction = 0.2f;
        [Tooltip("Peak vignette intensity at the top of the pulse.")]
        [SerializeField, Range(0f, 1f)] private float peakIntensity = 0.6f;
        [Tooltip("Color of the pulse. Deep red is the canonical 'damage' tint.")]
        [SerializeField] private Color pulseColor = new Color(0.85f, 0.05f, 0.05f);

        private Vignette _vignette;
        private bool _resolved;
        private bool _haveOriginal;
        private float _originalIntensity;
        private Color _originalColor;
        private bool _originalActive;

        private float _pulseTimer;   // counts up
        private bool _pulsing;

        private void OnEnable()
        {
            Resolve();
        }

        private void OnDisable()
        {
            RestoreVignette();
            _pulsing = false;
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
            if (volume.profile.TryGet<Vignette>(out var v))
            {
                _vignette = v;
            }
            _resolved = true;
        }

        /// <summary>
        /// Trigger one red vignette flash. Calling repeatedly while a pulse is
        /// in flight resets the timer so the player sees a sustained throb
        /// during chained damage.
        /// </summary>
        public void Pulse()
        {
            Resolve();
            if (_vignette == null) return;

            if (!_pulsing)
            {
                CaptureOriginal();
                _pulsing = true;
            }
            _pulseTimer = 0f;
        }

        private void CaptureOriginal()
        {
            if (_haveOriginal || _vignette == null) return;
            _originalActive = _vignette.active;
            _originalIntensity = _vignette.intensity.value;
            _originalColor = _vignette.color.value;
            _haveOriginal = true;
        }

        private void Update()
        {
            if (!_pulsing || _vignette == null) return;

            _pulseTimer += Time.unscaledDeltaTime;
            float k = EvaluatePulse(_pulseTimer);

            _vignette.active = true;
            float intensity = Mathf.Lerp(_originalIntensity, peakIntensity, k);
            _vignette.intensity.Override(intensity);
            _vignette.color.Override(Color.Lerp(_originalColor, pulseColor, k));

            if (_pulseTimer >= duration)
            {
                RestoreVignette();
                _pulsing = false;
            }
        }

        private float EvaluatePulse(float t)
        {
            float attack = Mathf.Max(0.0001f, duration * attackFraction);
            float release = Mathf.Max(0.0001f, duration - attack);
            if (t < attack) return Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((t - attack) / release);
            // Squared ease-out for a snappy tail.
            return (1f - r) * (1f - r);
        }

        private void RestoreVignette()
        {
            if (_vignette == null || !_haveOriginal) return;
            _vignette.active = _originalActive;
            _vignette.intensity.Override(_originalIntensity);
            _vignette.color.Override(_originalColor);
            _haveOriginal = false;
        }
    }
}
