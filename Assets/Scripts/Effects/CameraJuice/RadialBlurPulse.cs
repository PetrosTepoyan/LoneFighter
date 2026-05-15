using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Fakes a quick "radial blur" feel on huge events (explosions, boss spawn)
    /// by briefly increasing the URP <see cref="LensDistortion"/> intensity and
    /// fading it back over <see cref="fadeSeconds"/> (default 0.4s).
    ///
    /// URP's built-in volume overrides don't include a true radial-blur
    /// post-effect; lens distortion is the closest off-the-shelf surrogate —
    /// it scales screen-space radially from the lens center, which reads as a
    /// rapid zoom-blur smear at high intensities for a few frames.
    ///
    /// Reads the first <see cref="Volume"/> in the scene at <c>Start</c>.
    /// Gracefully no-ops if no Volume / no Lens Distortion override is found.
    /// All timing uses unscaled time so the pulse plays during hit-stop.
    /// </summary>
    [DisallowMultipleComponent]
    public class RadialBlurPulse : MonoBehaviour
    {
        public static RadialBlurPulse Instance { get; private set; }

        [Header("Volume Resolution")]
        [Tooltip("Optional. If null, the first Volume with a profile in the scene is used at Start.")]
        [SerializeField] private Volume volume;

        [Header("Pulse Targets (Lens Distortion intensity ranges -1..1)")]
        [SerializeField, Range(-1f, 1f)] private float explosionPeak = -0.5f;
        [SerializeField, Range(-1f, 1f)] private float bossSpawnPeak = -0.65f;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.4f;
        [Tooltip("How much of fadeSeconds is the hold at peak before the ease-out.")]
        [SerializeField, Range(0f, 0.5f)] private float holdFraction = 0.1f;

        private LensDistortion _ld;
        private float _baseline;
        private bool _captured;

        private Coroutine _routine;
        private float _activePeak;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ResolveVolume();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreBaseline();
        }

        private void ResolveVolume()
        {
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
            if (volume.profile.TryGet<LensDistortion>(out var ld))
            {
                _ld = ld;
                _baseline = _ld.intensity.value;
                _captured = true;
            }
        }

        /// <summary>Trigger an explosion-strength pulse.</summary>
        public void PulseExplosion(float scale01 = 1f)
        {
            float peak = Mathf.Lerp(_baseline, explosionPeak, Mathf.Clamp01(scale01));
            Pulse(peak);
        }

        /// <summary>Trigger a boss-spawn-strength pulse.</summary>
        public void PulseBossSpawn() => Pulse(bossSpawnPeak);

        /// <summary>
        /// Generic pulse to <paramref name="peak"/>. Re-triggering picks the
        /// peak further from baseline (in either direction) and restarts the
        /// fade.
        /// </summary>
        public void Pulse(float peak)
        {
            if (!_captured) return;
            peak = Mathf.Clamp(peak, -1f, 1f);

            // "Stronger" = further from baseline in same direction as new peak.
            if (Mathf.Abs(peak - _baseline) <= Mathf.Abs(_activePeak - _baseline) && _routine != null)
            {
                return;
            }

            _activePeak = peak;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _ld.active = true;
            _ld.intensity.Override(_activePeak);

            float hold = fadeSeconds * holdFraction;
            float t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            float ease = fadeSeconds - hold;
            t = 0f;
            float from = _activePeak;
            while (t < ease)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / ease);
                float eased = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
                _ld.intensity.Override(Mathf.Lerp(from, _baseline, eased));
                yield return null;
            }

            _ld.intensity.Override(_baseline);
            _activePeak = _baseline;
            _routine = null;
        }

        private void RestoreBaseline()
        {
            if (_ld == null) return;
            _ld.intensity.Override(_baseline);
        }
    }
}
