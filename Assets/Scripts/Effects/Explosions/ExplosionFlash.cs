using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Pooled world-space flash core. Drives a single <see cref="SpriteRenderer"/>
    /// from white-hot through yellow to fully transparent over <see cref="ExplosionProfile.flashLifetime"/>.
    ///
    /// The flash is the loudest visual element in the first ~50ms of the explosion and
    /// the cheapest to render — a single quad — so we render it on top of everything else
    /// via a high sorting order. We do NOT use a particle system for the flash because we
    /// need deterministic single-quad sizing tied to the profile.
    ///
    /// Returns itself to <see cref="PoolService"/> when the lifetime elapses. Safe to
    /// call <see cref="Play"/> on a pooled instance to (re)start the animation.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExplosionFlash : MonoBehaviour
    {
        [Header("Render")]
        [Tooltip("SpriteRenderer used for the flash. Auto-created at runtime if absent so a prefab is not strictly required.")]
        [SerializeField] private SpriteRenderer renderer2D;

        [Tooltip("Sorting order: stack above the player / projectiles. Keep in sync with the rest of the FX layer.")]
        [SerializeField] private int sortingOrder = 70;

        [Header("Animation")]
        [Tooltip("How quickly the flash decays. Higher = snappier. The animation always lasts profile.flashLifetime.")]
        [SerializeField, Range(0.5f, 4f)] private float decayPower = 2.2f;

        private float _lifetime = 0.18f;
        private float _peakSize = 1f;
        private Color _peakColor = Color.white;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void OnEnable()
        {
            // Reset; the pool reuses this instance and OnEnable is the canonical reset hook.
            _elapsed = 0f;
            _playing = true;
        }

        private void OnDisable()
        {
            _playing = false;
        }

        /// <summary>Configure for one playback. Idempotent across pool fetches.</summary>
        public void Play(ExplosionProfile profile)
        {
            if (profile == null) return;
            _lifetime = Mathf.Max(0.02f, profile.flashLifetime);
            _peakSize = Mathf.Max(0.01f, profile.flashSize);
            _peakColor = profile.flashColor;
            _elapsed = 0f;
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
            // Start at full size; decay with a configurable power curve so the flash
            // "punches" out then collapses quickly, matching the timing of the spark
            // burst that arrives ~1-2 frames later.
            float invT = 1f - t;
            float scale = _peakSize * Mathf.Lerp(0.55f, 1f, invT);
            transform.localScale = new Vector3(scale, scale, 1f);

            float alpha = Mathf.Pow(invT, decayPower);
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
            // Use a built-in white square so the prefab works even before art is wired.
            if (renderer2D.sprite == null)
            {
                renderer2D.sprite = ExplosionPrimitiveSprites.WhiteSquare();
            }
        }
    }
}
