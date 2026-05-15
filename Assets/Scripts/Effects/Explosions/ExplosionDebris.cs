using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Pooled spinning sprite chunk flung radially outward from the blast origin.
    /// Each chunk is a self-contained <see cref="Rigidbody2D"/> so it can interact with
    /// the world if the designer wants collisions, but defaults to no collider so it
    /// can't accidentally interfere with gameplay (we set the Rigidbody to Kinematic
    /// when there's no collider and drive translation manually).
    ///
    /// Spawned in radial fans by <see cref="ExplosionService"/>. Each chunk picks its
    /// own random velocity, spin and lifetime inside the ranges on <see cref="ExplosionProfile"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExplosionDebris : MonoBehaviour
    {
        [Header("Render")]
        [SerializeField] private SpriteRenderer renderer2D;
        [SerializeField] private int sortingOrder = 60;

        [Header("Motion")]
        [Tooltip("Drag (1/s) applied each frame to slow the chunk to a stop instead of flying offscreen.")]
        [SerializeField, Range(0f, 10f)] private float linearDrag = 2.8f;

        private Vector2 _velocity;
        private float _angularVelocity;
        private float _lifetime;
        private float _elapsed;
        private float _startScale = 0.2f;
        private Color _color = Color.white;
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

        /// <summary>
        /// Launch one chunk. Caller chooses a direction so we can fan chunks radially
        /// instead of every chunk picking its own (which would clump them).
        /// </summary>
        public void Launch(Vector2 direction, ExplosionProfile profile)
        {
            if (profile == null) return;
            float speed = Random.Range(profile.debrisSpeedRange.x, profile.debrisSpeedRange.y);
            _velocity = direction.normalized * speed;
            _angularVelocity = Random.Range(profile.debrisSpinRange.x, profile.debrisSpinRange.y);
            _lifetime = Mathf.Max(0.05f, Random.Range(profile.debrisLifetimeRange.x, profile.debrisLifetimeRange.y));
            _elapsed = 0f;
            _color = profile.flashColor; // Warm tint matched to the flash
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _startScale = Mathf.Max(0.04f, profile.particleSize * 1.3f);
            transform.localScale = new Vector3(_startScale, _startScale, 1f);
            _playing = true;
        }

        private void Update()
        {
            if (!_playing) return;
            float dt = Time.deltaTime;
            _elapsed += dt;
            float t = Mathf.Clamp01(_elapsed / _lifetime);

            // Apply drag in unscaled-but-still-time-scaled time; the debris stops as the
            // explosion subsides instead of plowing through the entire screen.
            _velocity *= Mathf.Max(0f, 1f - linearDrag * dt);
            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, _angularVelocity * dt);

            // Fade alpha + shrink towards 0 over the lifetime.
            var c = _color;
            c.a = 1f - t;
            renderer2D.color = c;
            float scale = _startScale * Mathf.Lerp(1f, 0.25f, t);
            transform.localScale = new Vector3(scale, scale, 1f);

            if (t >= 1f)
            {
                _playing = false;
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else gameObject.SetActive(false);
            }
        }

        private void EnsureRenderer()
        {
            if (renderer2D != null) return;
            renderer2D = GetComponent<SpriteRenderer>();
            if (renderer2D == null) renderer2D = gameObject.AddComponent<SpriteRenderer>();
            renderer2D.sortingOrder = sortingOrder;
            if (renderer2D.sprite == null)
            {
                renderer2D.sprite = ExplosionPrimitiveSprites.WhiteSquare();
            }
        }
    }
}
