using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.DeathFx
{
    /// Singleton that hands out pooled `GibChunk` instances configured to a burst recipe.
    /// One-stop entry point so the DeathFxDispatcher (and any future caller) doesn't have
    /// to think about pooling, prefab refs, or per-chunk randomization.
    ///
    /// `gibChunkPrefab` must be a prefab whose root has a `GibChunk` component. The
    /// editor generator can build one for you (see DeathFxProfileGenerator), or you can
    /// hand-author it — the only requirements are GibChunk + SpriteRenderer + Rigidbody2D,
    /// and GibChunk auto-adds the latter two if missing.
    public class GibSpawner : MonoBehaviour
    {
        public static GibSpawner Instance { get; private set; }

        [Header("Pooled prefab")]
        [Tooltip("Prefab with a GibChunk component on the root. Pooled via PoolService.")]
        [SerializeField] private GameObject gibChunkPrefab;

        [Header("Defaults (overridden per-profile by the dispatcher)")]
        [Tooltip("Cone (in degrees) the gib burst sprays into. 360 = full radial.")]
        [Range(10f, 360f)] public float spreadDegrees = 360f;

        [Tooltip("Random spin range applied to each chunk (degrees / second).")]
        public Vector2 spinRange = new Vector2(-540f, 540f);

        [Tooltip("Hard cap on chunks emitted in a single Spawn call — keeps a misconfigured " +
                 "profile from instantly hitting the pool ceiling.")]
        [Range(0, 64)] public int maxBurstCount = 24;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// Spawn `count` gibs from `origin`. Each chunk gets a random direction inside
        /// `spreadDegrees`, a random speed in `speedRange`, a random size in `sizeRange`,
        /// and the supplied `tint`.
        public void Spawn(
            Vector2 origin,
            int count,
            Color tint,
            Vector2 sizeRange,
            Vector2 speedRange,
            float lifetime)
        {
            if (gibChunkPrefab == null) return;
            if (count <= 0) return;
            count = Mathf.Min(count, maxBurstCount);

            // Pick a base aim direction; a 360 spread degenerates to "random outward" anyway.
            Vector2 baseDir = Random.insideUnitCircle.normalized;
            if (baseDir.sqrMagnitude < 0.001f) baseDir = Vector2.up;
            float halfSpread = spreadDegrees * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angleOffset = Random.Range(-halfSpread, halfSpread);
                Vector2 dir = Rotate(baseDir, angleOffset);

                float speed = Random.Range(speedRange.x, speedRange.y);
                float size = Random.Range(sizeRange.x, sizeRange.y);
                float spin = Random.Range(spinRange.x, spinRange.y);

                GameObject instance;
                if (PoolService.Instance != null)
                {
                    instance = PoolService.Instance.Get(gibChunkPrefab, origin, Quaternion.identity);
                }
                else
                {
                    instance = Instantiate(gibChunkPrefab, origin, Quaternion.identity);
                }
                if (instance == null) continue;

                var chunk = instance.GetComponent<GibChunk>();
                if (chunk == null)
                {
                    // Defensive: if someone wired a bare prefab without the component,
                    // attach one rather than silently dropping the gib.
                    chunk = instance.AddComponent<GibChunk>();
                }
                chunk.Launch(origin, dir, speed, size, lifetime, tint, spin);
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }
    }
}
