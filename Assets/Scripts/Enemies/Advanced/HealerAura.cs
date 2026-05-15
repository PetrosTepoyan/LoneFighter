using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// Pooled green healing-pulse visual. Two roles:
    ///   1. As the healer's pulse telegraph: expands from 0 to healRadius and fades out.
    ///   2. As a per-target aura on healed enemies: short, smaller pulse to confirm the tick.
    ///
    /// The visual is driven from script so the prefab only needs a single child SpriteRenderer
    /// (any soft-circle sprite — even Unity's built-in Knob works at low alpha) tinted green.
    /// If a ParticleSystem is present, it will be played alongside the script-driven scaling.
    public class HealerAura : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Max world-space radius the aura grows to. Configure() overrides this at runtime.")]
        [SerializeField] private float maxRadius = 3.5f;
        [Tooltip("Visual scale that corresponds to a radius of 1 unit. Tune to the source sprite.")]
        [SerializeField] private float visualScalePerUnit = 2f;
        [Tooltip("How long the aura lives, in seconds.")]
        [SerializeField] private float duration = 0.45f;

        [Header("Color")]
        [SerializeField] private Color tintStart = new Color(0.4f, 1f, 0.55f, 0.9f);
        [SerializeField] private Color tintEnd = new Color(0.4f, 1f, 0.55f, 0f);

        [Header("Refs (optional — auto-resolved)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private ParticleSystem particles;

        private float _elapsed;
        private bool _captured;

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (particles == null) particles = GetComponentInChildren<ParticleSystem>();
            _captured = true;
        }

        /// Drive the aura to a specific target radius (used by HealerEnemy).
        public void Configure(float radius)
        {
            if (radius > 0f) maxRadius = radius;
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            ApplyScale(0f);
            if (spriteRenderer != null) spriteRenderer.color = tintStart;
            if (particles != null)
            {
                particles.Clear(true);
                particles.Play(true);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            // Ease-out scale and color fade.
            float eased = 1f - (1f - t) * (1f - t);
            ApplyScale(Mathf.Lerp(0f, maxRadius, eased));
            if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(tintStart, tintEnd, t);

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
