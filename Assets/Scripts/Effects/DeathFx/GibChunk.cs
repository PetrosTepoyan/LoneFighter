using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.DeathFx
{
    /// One pooled flying sprite chunk. Lives `lifetime` seconds, fades over the last 50%
    /// of that time, then returns itself to the PoolService (or self-destroys when the
    /// pool isn't around — e.g. in unit tests).
    ///
    /// Auto-attaches a SpriteRenderer + Rigidbody2D in Awake so callers can just pool a
    /// bare `new GameObject("Gib")` from a prefab that only carries this component. That
    /// means the editor generator doesn't have to ship a hand-crafted prefab — the chunk
    /// builds itself.
    [DisallowMultipleComponent]
    public class GibChunk : MonoBehaviour
    {
        [Tooltip("Renderer used to display the chunk. Auto-added on Awake when missing.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Physics body used for the outward fling. Auto-added on Awake when missing.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("Sprite used by gibs. Editor generator fills this with a small soft square " +
                 "Texture2D wrapped as a runtime sprite; can be overridden in the prefab.")]
        [SerializeField] private Sprite chunkSprite;

        [Tooltip("Sorting layer name the gibs render on. Leaving the default keeps them on " +
                 "the same layer as enemies — change in the prefab to push behind/in front.")]
        [SerializeField] private string sortingLayer = "Default";

        [Tooltip("Order in layer offset relative to the default. Positive = on top of enemies.")]
        [SerializeField] private int sortingOrder = 5;

        [Tooltip("Linear drag applied to the rigidbody so chunks slow down rather than slide " +
                 "across the arena forever.")]
        [SerializeField, Range(0f, 10f)] private float linearDrag = 3f;

        [Tooltip("Angular drag applied so spin damps out toward the end of life.")]
        [SerializeField, Range(0f, 20f)] private float angularDrag = 6f;

        private float _lifetime = 0.8f;
        private float _age;
        private Color _baseColor = Color.white;
        private bool _active;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
                if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            }
            body.gravityScale = 0f;
            body.linearDamping = linearDrag;
            body.angularDamping = angularDrag;
            body.freezeRotation = false;
            // Sleep eagerly so a chunk that lands and damps to a stop doesn't keep waking
            // up the physics scheduler.
            body.sleepMode = RigidbodySleepMode2D.StartAwake;

            if (chunkSprite != null && spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = chunkSprite;
            }
            spriteRenderer.sortingLayerName = sortingLayer;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        /// Configure a freshly-pooled chunk and kick it outward.
        /// `direction` does NOT need to be normalized — we normalize internally.
        public void Launch(
            Vector2 position,
            Vector2 direction,
            float speed,
            float size,
            float lifetime,
            Color color,
            float spinDegPerSecond)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            transform.localScale = new Vector3(size, size, 1f);

            if (body != null)
            {
                body.linearVelocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.zero;
                body.angularVelocity = spinDegPerSecond;
                body.linearDamping = linearDrag;
                body.angularDamping = angularDrag;
            }

            _baseColor = color;
            if (spriteRenderer != null)
            {
                if (chunkSprite != null && spriteRenderer.sprite == null) spriteRenderer.sprite = chunkSprite;
                spriteRenderer.color = color;
            }

            _lifetime = Mathf.Max(0.05f, lifetime);
            _age = 0f;
            _active = true;
        }

        private void OnDisable()
        {
            // Pool reuse: reset transient state so a fresh Launch starts clean.
            _active = false;
            _age = 0f;
        }

        private void Update()
        {
            if (!_active) return;
            _age += Time.deltaTime;
            float t = _age / _lifetime;
            if (t >= 1f)
            {
                _active = false;
                ReturnToPool();
                return;
            }

            // Fade only over the last half of the lifetime so the chunk reads solid mid-flight.
            if (spriteRenderer != null)
            {
                float a = t < 0.5f ? _baseColor.a : Mathf.Lerp(_baseColor.a, 0f, (t - 0.5f) * 2f);
                var c = _baseColor;
                c.a = a;
                spriteRenderer.color = c;
            }
        }

        private void ReturnToPool()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
