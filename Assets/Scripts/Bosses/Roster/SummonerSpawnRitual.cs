using UnityEngine;

namespace LoneFighter.Bosses.Roster
{
    /// Visual telegraph for the Summoner's pre-summon ritual. Draws a purple pentagram
    /// (a simple pulsing rotating decal) at a given world position for a fixed duration
    /// before disappearing. This component owns its own ParticleSystem when available
    /// and falls back to a procedural LineRenderer pentagram so the prefab works even
    /// without authored particle assets.
    ///
    /// Lifecycle:
    /// - SummonerBoss calls <see cref="Play"/> with a world position and duration
    ///   when it enters the "windup" sub-state.
    /// - The ritual disables itself after the duration elapses; if a PoolService is in
    ///   use and the object was pooled, the boss is expected to release it.
    ///
    /// This component is also designed to be attached directly to a child of the
    /// SummonerBoss prefab as a self-contained telegraph (no pooling required).
    [DisallowMultipleComponent]
    public class SummonerSpawnRitual : MonoBehaviour
    {
        [Header("Visuals")]
        [Tooltip("Optional ParticleSystem played for the duration of the ritual. Will be set to looping = false.")]
        [SerializeField] private ParticleSystem particles;
        [Tooltip("Optional LineRenderer used to draw a procedural pentagram. Auto-created if absent.")]
        [SerializeField] private LineRenderer linePentagram;
        [Tooltip("Outer radius of the pentagram in world units.")]
        [SerializeField] private float pentagramRadius = 0.9f;
        [Tooltip("Color used when the ritual is at full intensity.")]
        [SerializeField] private Color ritualColor = new Color(0.55f, 0.15f, 0.95f, 0.9f);
        [Tooltip("Rotation speed in degrees per second.")]
        [SerializeField] private float spinSpeed = 60f;

        [Header("Behavior")]
        [Tooltip("If true, the ritual auto-plays on enable using the serialized default duration.")]
        [SerializeField] private bool playOnEnable = false;
        [Tooltip("Default duration when auto-playing on enable.")]
        [SerializeField] private float defaultDuration = 1f;

        private float _remaining;
        private float _total;
        private bool _active;

        public bool IsActive => _active;

        private void Awake()
        {
            EnsurePentagram();
        }

        private void OnEnable()
        {
            if (playOnEnable) Play(transform.position, defaultDuration);
        }

        private void OnDisable()
        {
            _active = false;
            if (linePentagram != null) linePentagram.enabled = false;
            if (particles != null && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Play(Vector3 worldPosition, float duration)
        {
            transform.position = worldPosition;
            _total = Mathf.Max(0.01f, duration);
            _remaining = _total;
            _active = true;
            if (linePentagram != null) linePentagram.enabled = true;
            if (particles != null)
            {
                var main = particles.main;
                main.loop = false;
                main.duration = _total;
                main.startColor = ritualColor;
                particles.Clear(true);
                particles.Play(true);
            }
            ApplyVisual(0f);
        }

        public void Stop()
        {
            _active = false;
            _remaining = 0f;
            if (linePentagram != null) linePentagram.enabled = false;
            if (particles != null && particles.isPlaying) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (!_active) return;
            _remaining -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_remaining / _total);
            ApplyVisual(t);
            if (_remaining <= 0f) Stop();
        }

        private void ApplyVisual(float t)
        {
            // Spin & pulse the pentagram.
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            if (linePentagram != null)
            {
                float pulse = 0.7f + 0.3f * Mathf.Sin(t * Mathf.PI * 4f);
                Color c = ritualColor;
                c.a *= Mathf.Lerp(0.4f, 1f, pulse);
                linePentagram.startColor = c;
                linePentagram.endColor = c;
                linePentagram.widthMultiplier = Mathf.Lerp(0.05f, 0.1f, pulse) * pentagramRadius;
            }
        }

        private void EnsurePentagram()
        {
            if (linePentagram == null)
            {
                linePentagram = GetComponent<LineRenderer>();
                if (linePentagram == null) linePentagram = gameObject.AddComponent<LineRenderer>();
            }
            // Pentagram: 5 outer vertices connected in star order (skip 1 each step).
            // Indices: 0 -> 2 -> 4 -> 1 -> 3 -> 0 (closed loop, 6 points).
            const int starPoints = 6;
            linePentagram.useWorldSpace = false;
            linePentagram.loop = false;
            linePentagram.positionCount = starPoints;
            int[] order = { 0, 2, 4, 1, 3, 0 };
            for (int i = 0; i < starPoints; i++)
            {
                int corner = order[i];
                float angle = (corner / 5f) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * pentagramRadius;
                linePentagram.SetPosition(i, p);
            }
            linePentagram.startColor = ritualColor;
            linePentagram.endColor = ritualColor;
            linePentagram.widthMultiplier = 0.08f * pentagramRadius;
            linePentagram.enabled = false;
        }
    }
}
