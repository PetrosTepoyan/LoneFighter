using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Pooled expanding ring. Starts at radius 0, grows to <see cref="ExplosionProfile.ringRadius"/>
    /// over <see cref="ExplosionProfile.ringDuration"/>. Renders a single <see cref="SpriteRenderer"/>
    /// with an additive material so it blooms cleanly through URP post-processing.
    ///
    /// This is purely visual — area damage and direct hit-stop are handled in
    /// <see cref="ExplosionService"/> so the visual prefab can be re-used without
    /// double-applying damage if a designer drops one in the scene by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExplosionShockwave : MonoBehaviour
    {
        [Header("Render")]
        [SerializeField] private SpriteRenderer renderer2D;
        [SerializeField] private int sortingOrder = 65;

        [Tooltip("Sprite scale corresponding to a radius of 1 world unit. The default white-square primitive is 1u wide so this stays at 2 for a diameter mapping.")]
        [SerializeField] private float visualScalePerUnit = 2f;

        [Header("Animation")]
        [Tooltip("How fast the ring brightness fades. Higher = quicker fade.")]
        [SerializeField, Range(0.5f, 4f)] private float fadePower = 1.6f;

        private float _radius;
        private float _duration = 0.35f;
        private Color _peakColor = new Color(1f, 0.55f, 0.15f, 0.85f);
        private float _elapsed;
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

        /// <summary>Configure for a single playback.</summary>
        public void Play(ExplosionProfile profile)
        {
            if (profile == null) return;
            _radius = Mathf.Max(0f, profile.ringRadius);
            _duration = Mathf.Max(0.05f, profile.ringDuration);
            _peakColor = profile.ringColor;
            _elapsed = 0f;
            _playing = true;
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
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
            float currentRadius = Mathf.Lerp(0f, _radius, t);
            float s = Mathf.Max(0.0001f, currentRadius * visualScalePerUnit);
            transform.localScale = new Vector3(s, s, 1f);

            // Bright at the start, fades out as the ring expands so the trailing edge
            // is much fainter than the leading edge — sells the "shockwave passed by".
            float alpha = Mathf.Pow(1f - t, fadePower);
            var c = _peakColor;
            c.a *= alpha;
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
                renderer2D.sprite = ExplosionPrimitiveSprites.RingSprite();
            }
        }
    }
}
