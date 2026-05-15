using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Pooled lingering smoke puff. Uses a single sprite renderer for cheapness
    /// (one quad, no particle system needed) and expands outward from the blast
    /// origin while fading grey to transparent over <see cref="ExplosionProfile.smokeLifetime"/>.
    ///
    /// If you'd rather use a real <see cref="ParticleSystem"/> for the smoke, drop one
    /// onto the prefab and the <see cref="ParticleAutoRelease"/> hook will return the
    /// instance to the pool — this script simply won't drive it. The sprite-based path
    /// here is the default so the system works without authoring particle prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExplosionSmoke : MonoBehaviour
    {
        [Header("Render")]
        [SerializeField] private SpriteRenderer renderer2D;
        [SerializeField] private int sortingOrder = 55;
        [SerializeField] private Color smokeColor = new Color(0.35f, 0.32f, 0.30f, 0.85f);

        [Header("Animation")]
        [Tooltip("Drift direction (world units per second). Smoke rises slightly by default.")]
        [SerializeField] private Vector2 drift = new Vector2(0f, 0.6f);

        private float _radius = 1.2f;
        private float _lifetime = 1.5f;
        private float _elapsed;
        private Vector3 _origin;
        private bool _playing;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _playing = true;
        }

        private void OnDisable()
        {
            _playing = false;
        }

        public void Play(ExplosionProfile profile)
        {
            if (profile == null) return;
            _radius = Mathf.Max(0.05f, profile.smokeRadius);
            _lifetime = Mathf.Max(0.05f, profile.smokeLifetime);
            _elapsed = 0f;
            _origin = transform.position;
            _playing = true;
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _lifetime);
            ApplyFrame(t);

            if (t >= 1f)
            {
                _playing = false;
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else gameObject.SetActive(false);
            }
        }

        private void ApplyFrame(float t)
        {
            EnsureRenderer();
            // Expand from ~30% to 100% of radius over the lifetime.
            float s = _radius * Mathf.Lerp(0.3f, 1f, t);
            transform.localScale = new Vector3(s, s, 1f);
            transform.position = _origin + (Vector3)(drift * (_elapsed));

            // Stay solid through the first third, then fade.
            float alpha = smokeColor.a * (1f - Mathf.SmoothStep(0.33f, 1f, t));
            var c = smokeColor;
            c.a = alpha;
            renderer2D.color = c;
        }

        private void EnsureRenderer()
        {
            if (renderer2D != null) return;
            renderer2D = GetComponent<SpriteRenderer>();
            if (renderer2D == null) renderer2D = gameObject.AddComponent<SpriteRenderer>();
            renderer2D.sortingOrder = sortingOrder;
            if (renderer2D.sprite == null)
            {
                renderer2D.sprite = ExplosionPrimitiveSprites.SoftDisc();
            }
        }
    }
}
