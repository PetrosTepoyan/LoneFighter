using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Audio.Adaptive
{
    /// <summary>
    /// Singleton that produces a smoothed combat-intensity value in [0..1] every frame.
    /// <see cref="AdaptiveMusicController"/> samples <see cref="Intensity"/> to decide
    /// which stems should be audible.
    ///
    /// Intensity is rebuilt every Update from four read-only inputs:
    /// <list type="bullet">
    ///   <item>0.3 baseline scaled by <c>GameManager.ElapsedSeconds / 300</c> (time pressure).</item>
    ///   <item>+0.3 when <c>EnemyRegistry.Count &gt; 30</c> (swarm).</item>
    ///   <item>+0.2 when player HP fraction &lt; 0.3 (low HP).</item>
    ///   <item>+0.2 when at least one alive enemy's name contains "Boss" or "Warden".</item>
    /// </list>
    /// The raw value is clamped to [0..1] and then lerped toward via <see cref="intensitySmoothing"/>
    /// so the music doesn't pop on transient events. The resolver subscribes to nothing —
    /// it only reads existing public API surface.
    /// </summary>
    [DisallowMultipleComponent]
    public class IntensityResolver : MonoBehaviour
    {
        public static IntensityResolver Instance { get; private set; }

        [Header("Smoothing")]
        [Tooltip("Time constant for the lerp between raw and smoothed intensity, in seconds. " +
                 "Larger values mean slower reactions.")]
        [Min(0.01f)]
        public float intensitySmoothing = 0.5f;

        [Header("Tuning (rarely touched)")]
        [Tooltip("Seconds of run duration that maps to the full 0.3 time-pressure contribution.")]
        [Min(1f)]
        public float timePressureMaxSeconds = 300f;

        [Tooltip("Strength of the time-pressure contribution at its maximum.")]
        [Range(0f, 1f)]
        public float timePressureWeight = 0.3f;

        [Tooltip("Number of live enemies that triggers the swarm contribution.")]
        [Min(1)]
        public int swarmThreshold = 30;

        [Tooltip("Strength of the swarm contribution.")]
        [Range(0f, 1f)]
        public float swarmWeight = 0.3f;

        [Tooltip("HP fraction (Current/Max) at or below which the low-HP contribution applies.")]
        [Range(0f, 1f)]
        public float lowHpFraction = 0.3f;

        [Tooltip("Strength of the low-HP contribution.")]
        [Range(0f, 1f)]
        public float lowHpWeight = 0.2f;

        [Tooltip("Strength of the boss-alive contribution.")]
        [Range(0f, 1f)]
        public float bossWeight = 0.2f;

        [Header("Debug (read-only at runtime)")]
        [SerializeField, Range(0f, 1f)] private float _smoothedIntensity;
        [SerializeField, Range(0f, 1f)] private float _rawIntensity;
        [SerializeField] private bool _bossAlive;
        [SerializeField] private bool _swarmActive;
        [SerializeField] private bool _lowHpActive;

        /// <summary>Smoothed 0..1 intensity for this frame.</summary>
        public float Intensity => _smoothedIntensity;
        /// <summary>Most recent unsmoothed/raw intensity — useful for HUD debug.</summary>
        public float RawIntensity => _rawIntensity;
        /// <summary>True if a boss-tagged enemy is currently alive.</summary>
        public bool BossAlive => _bossAlive;
        /// <summary>True if the live enemy count exceeds <see cref="swarmThreshold"/>.</summary>
        public bool SwarmActive => _swarmActive;
        /// <summary>True if the cached <see cref="PlayerHealth"/> reports HP below the low-HP fraction.</summary>
        public bool LowHpActive => _lowHpActive;

        // Cached so we don't FindObjectOfType every frame. Resolved lazily and re-resolved
        // if the cached instance has been destroyed (scene transitions).
        private PlayerHealth _cachedPlayerHealth;

        /// <summary>
        /// Optional manual override — assign a <see cref="PlayerHealth"/> if you have one handy
        /// (saves an Object.FindObjectOfType). The resolver falls back to a scene search if null.
        /// </summary>
        public void SetPlayerHealth(PlayerHealth health) => _cachedPlayerHealth = health;

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
        }

        private void Update()
        {
            float raw = ComputeRawIntensity();
            _rawIntensity = raw;

            // Critically-damped-ish smoothing. We treat intensitySmoothing as the time constant:
            // alpha = 1 - exp(-dt / tau). Use unscaled time so paused / level-up screens still resolve
            // cleanly toward whatever the static state is, instead of getting stuck.
            float tau = Mathf.Max(0.01f, intensitySmoothing);
            float alpha = 1f - Mathf.Exp(-Time.unscaledDeltaTime / tau);
            _smoothedIntensity = Mathf.Lerp(_smoothedIntensity, raw, alpha);
        }

        private float ComputeRawIntensity()
        {
            float v = 0f;

            // Time pressure --------------------------------------------------------------------
            var gm = GameManager.Instance;
            if (gm != null && timePressureMaxSeconds > 0f)
            {
                float t = Mathf.Clamp01(gm.ElapsedSeconds / timePressureMaxSeconds);
                v += t * timePressureWeight;
            }

            // Swarm ----------------------------------------------------------------------------
            int enemyCount = EnemyRegistry.Count;
            _swarmActive = enemyCount > swarmThreshold;
            if (_swarmActive) v += swarmWeight;

            // Low HP ---------------------------------------------------------------------------
            _lowHpActive = false;
            var ph = ResolvePlayerHealth();
            if (ph != null && ph.Max > 0f)
            {
                float frac = ph.Current / ph.Max;
                if (frac < lowHpFraction)
                {
                    _lowHpActive = true;
                    v += lowHpWeight;
                }
            }

            // Boss alive -----------------------------------------------------------------------
            _bossAlive = AnyBossAlive();
            if (_bossAlive) v += bossWeight;

            return Mathf.Clamp01(v);
        }

        private PlayerHealth ResolvePlayerHealth()
        {
            if (_cachedPlayerHealth != null) return _cachedPlayerHealth;
#if UNITY_2023_1_OR_NEWER
            _cachedPlayerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
#else
            _cachedPlayerHealth = FindObjectOfType<PlayerHealth>();
#endif
            return _cachedPlayerHealth;
        }

        private static bool AnyBossAlive()
        {
            var enemies = EnemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                // Match against GameObject name and EnemyData asset name so prefab-instance
                // naming ("WardenBoss(Clone)") and data-asset naming ("Warden") both light up.
                string goName = e.gameObject.name;
                if (Contains(goName, "Boss") || Contains(goName, "Warden")) return true;
                var data = e.Data;
                if (data != null)
                {
                    string dataName = data.name;
                    if (Contains(dataName, "Boss") || Contains(dataName, "Warden")) return true;
                }
            }
            return false;
        }

        private static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;
            return haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
