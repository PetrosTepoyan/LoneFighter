using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LoneFighter.Player;

namespace LoneFighter.Effects.CameraJuice
{
    /// <summary>
    /// Pulses the URP <see cref="ChromaticAberration"/> intensity on big moments:
    /// explosions, boss spawn, level-up, low-HP entry. Reads the first
    /// <see cref="Volume"/> in the scene at <c>Start</c> and gracefully no-ops if
    /// no Volume / no Chromatic Aberration override is present.
    ///
    /// The pulse is a quick spike from the captured baseline to a target peak,
    /// then a smooth ease back to baseline over <see cref="fadeSeconds"/>.
    /// Multiple pulses adopt the higher peak and reset the timer.
    ///
    /// Auto-subscribes to <see cref="PlayerLevel.OnLevelUp"/> and to
    /// <see cref="PlayerHealth.OnHealthChanged"/> for the low-HP entry edge.
    /// Explosion / boss-spawn pulses are dispatched externally
    /// (see <see cref="CameraJuiceService"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class ChromaticBurst : MonoBehaviour
    {
        public static ChromaticBurst Instance { get; private set; }

        [Header("Volume Resolution")]
        [Tooltip("Optional. If null, the first Volume with a profile in the scene is used at Start.")]
        [SerializeField] private Volume volume;

        [Header("Pulse Targets")]
        [SerializeField, Range(0f, 1f)] private float explosionPeak = 0.55f;
        [SerializeField, Range(0f, 1f)] private float bossSpawnPeak = 0.7f;
        [SerializeField, Range(0f, 1f)] private float levelUpPeak = 0.45f;
        [SerializeField, Range(0f, 1f)] private float lowHpPeak = 0.6f;

        [Header("Pulse Timing")]
        [SerializeField, Min(0.02f)] private float fadeSeconds = 0.45f;

        [Header("Auto Subscriptions")]
        [SerializeField] private bool autoSubscribeLevelUp = true;
        [SerializeField] private bool autoSubscribeLowHp = true;
        [SerializeField, Range(0.05f, 0.95f)] private float lowHpFraction = 0.3f;

        private ChromaticAberration _ca;
        private float _baseline;
        private bool _captured;

        private Coroutine _routine;
        private float _activePeak;

        private PlayerLevel _level;
        private PlayerHealth _health;
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

        private void Start()
        {
            ResolveVolume();
            ResolveSubscriptions();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
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
            if (volume.profile.TryGet<ChromaticAberration>(out var ca))
            {
                _ca = ca;
                _baseline = _ca.intensity.value;
                _captured = true;
            }
        }

        private void ResolveSubscriptions()
        {
            PlayerLevel level = null;
            PlayerHealth health = null;
            if (PlayerController.Instance != null)
            {
                level = PlayerController.Instance.GetComponent<PlayerLevel>();
                health = PlayerController.Instance.GetComponent<PlayerHealth>();
            }
            if (level == null) level = FindFirstObjectByType<PlayerLevel>();
            if (health == null) health = FindFirstObjectByType<PlayerHealth>();

            if (autoSubscribeLevelUp && level != null)
            {
                _level = level;
                _level.OnLevelUp += HandleLevelUp;
            }
            if (autoSubscribeLowHp && health != null)
            {
                _health = health;
                _health.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void HandleLevelUp(int _) => Pulse(levelUpPeak);

        private void HandleHealthChanged(float current, float max)
        {
            if (max <= 0f) return;
            bool nowLow = (current / max) <= lowHpFraction && current > 0f;
            if (nowLow && !_wasLowHp) Pulse(lowHpPeak);
            _wasLowHp = nowLow;
        }

        /// <summary>Burst pulse intended for explosions; intensity scales the explosionPeak.</summary>
        public void PulseExplosion(float intensity01 = 1f)
        {
            Pulse(explosionPeak * Mathf.Clamp01(intensity01));
        }

        /// <summary>Burst pulse for boss spawn.</summary>
        public void PulseBossSpawn() => Pulse(bossSpawnPeak);

        /// <summary>
        /// Generic pulse to <paramref name="peak"/>. Re-triggering during an
        /// active pulse adopts the higher peak and restarts the fade.
        /// </summary>
        public void Pulse(float peak)
        {
            if (!_captured) return;
            peak = Mathf.Clamp01(peak);
            if (peak <= _baseline) return;

            _activePeak = Mathf.Max(_activePeak, peak);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _ca.active = true;
            _ca.intensity.Override(_activePeak);

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeSeconds);
                float eased = 1f - Mathf.Pow(1f - k, 2f); // ease-out quad
                _ca.intensity.Override(Mathf.Lerp(_activePeak, _baseline, eased));
                yield return null;
            }

            _ca.intensity.Override(_baseline);
            _activePeak = 0f;
            _routine = null;
        }

        private void RestoreBaseline()
        {
            if (_ca == null) return;
            _ca.intensity.Override(_baseline);
        }
    }
}
