using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// Red expanding warning ring drawn during the teleporter's attack windup. Functions like
    /// EnemyShockwave's visual layer but does NOT apply damage — TeleporterEnemy handles the
    /// damage application itself when the windup ends, so this script is purely a player tell.
    ///
    /// Tuning intent (per game-feel notes):
    /// - Expand from 0 to the configured attack radius over the windup window.
    /// - Strobe slightly faster as the ring approaches full size so the player can feel the
    ///   "danger is imminent" beat without needing an audio cue.
    public class TeleportTelegraph : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Configured from TeleporterEnemy.Configure(): the AOE radius the ring grows to.")]
        [SerializeField] private float targetRadius = 1.6f;
        [Tooltip("Configured from TeleporterEnemy.Configure(): the time over which the ring expands.")]
        [SerializeField] private float duration = 0.6f;
        [Tooltip("Visual scale that corresponds to a radius of 1 unit. Tune to the ring sprite.")]
        [SerializeField] private float visualScalePerUnit = 2f;

        [Header("Color")]
        [SerializeField] private Color startColor = new Color(1f, 0.25f, 0.25f, 0.4f);
        [SerializeField] private Color endColor = new Color(1f, 0.1f, 0.1f, 0.95f);
        [Tooltip("Strobe frequency at t=0 in Hz.")]
        [SerializeField] private float strobeStartHz = 4f;
        [Tooltip("Strobe frequency at t=1 in Hz.")]
        [SerializeField] private float strobeEndHz = 12f;

        [Header("Refs (optional — auto-resolved)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer ringRenderer;

        private float _elapsed;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            if (ringRenderer == null) ringRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        /// Drive the ring to a specific radius and total duration (called by TeleporterEnemy).
        public void Configure(float radius, float windupDuration)
        {
            if (radius > 0f) targetRadius = radius;
            if (windupDuration > 0f) duration = windupDuration;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            ApplyScale(0f);
            if (ringRenderer != null) ringRenderer.color = startColor;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            // Expand.
            float radius = Mathf.Lerp(0f, targetRadius, t);
            ApplyScale(radius);

            // Strobe color (lerp base, then PingPong toward the alert color).
            if (ringRenderer != null)
            {
                float hz = Mathf.Lerp(strobeStartHz, strobeEndHz, t);
                float strobe = Mathf.PingPong(Time.time * hz, 1f);
                Color baseC = Color.Lerp(startColor, endColor, t);
                ringRenderer.color = Color.Lerp(baseC, endColor, strobe);
            }

            if (t >= 1f) Release();
        }

        private void ApplyScale(float radius)
        {
            if (visualRoot == null) return;
            float s = Mathf.Max(0.0001f, radius * visualScalePerUnit);
            visualRoot.localScale = new Vector3(s, s, 1f);
        }

        private void Release()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
