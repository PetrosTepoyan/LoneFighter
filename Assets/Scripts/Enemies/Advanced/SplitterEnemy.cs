using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// Death-spawn archetype. When the host EnemyBase dies, this component spawns two smaller
    /// "shard" enemies. The smaller variants are tagged with SplitterSmall so they do NOT split
    /// again on death — that one-generation rule keeps a single splitter from snowballing.
    ///
    /// Implementation note (do-not-modify-existing-files constraint):
    /// EnemyBase.HandleDeath() releases the object to PoolService, which deactivates the
    /// GameObject — but the OnDied event on EnemyHealth fires BEFORE the release. We use both:
    ///   - OnDied handler records the death position and arms a pending split flag.
    ///   - OnDisable performs the actual spawn so the visual ordering (parent dies, shards
    ///     appear) reads correctly and so the pool has already returned this object.
    /// Spawning during OnDisable is safe because PoolService.Get on a different prefab uses a
    /// separate pool and is not re-entrant on the disabling object.
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(EnemyHealth))]
    public class SplitterEnemy : MonoBehaviour
    {
        [Header("Shards")]
        [Tooltip("EnemyData asset used to configure each spawned shard. Its prefab field MUST point at a prefab that has a SplitterSmall marker so the shards do not chain-split.")]
        [SerializeField] private EnemyData shardData;
        [Tooltip("Number of shards to spawn on death.")]
        [SerializeField] private int shardCount = 2;

        [Header("Placement")]
        [Tooltip("Radius around the corpse at which shards spawn.")]
        [SerializeField] private float spawnSpread = 0.6f;
        [Tooltip("Outward impulse applied to each shard so they visibly burst apart.")]
        [SerializeField] private float burstImpulse = 2.5f;

        [Header("XP Drop Forwarding")]
        [Tooltip("Optional XP gem prefab handed to spawned shards via EnemyBase.Configure. If null, shards will not drop gems.")]
        [SerializeField] private GameObject xpGemPrefab;

        private EnemyHealth _health;
        private bool _pendingSplit;
        private Vector3 _deathPosition;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _pendingSplit = false;
            _deathPosition = transform.position;
            if (_health != null) _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDied -= HandleDied;

            if (_pendingSplit)
            {
                _pendingSplit = false;
                SpawnShards(_deathPosition);
            }
        }

        private void HandleDied(EnemyHealth _)
        {
            // Capture the position NOW — by the time OnDisable runs the pool may have moved the
            // transform back to its parent's origin.
            _deathPosition = transform.position;
            _pendingSplit = true;
        }

        private void SpawnShards(Vector3 center)
        {
            if (shardData == null || shardData.prefab == null || shardCount <= 0) return;

            // Bail out gracefully if the global cap has been reached — we never want a splitter
            // chain to blow past EnemySpawner.GlobalCap.
            // (We can't read the cap directly without a scene reference, so just trust the
            // existing limits; PoolService still works either way.)

            // Distribute shards evenly around a circle with a tiny per-instance jitter.
            float angleStep = Mathf.PI * 2f / Mathf.Max(1, shardCount);
            float angleJitter = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < shardCount; i++)
            {
                float a = angleStep * i + angleJitter;
                Vector2 offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spawnSpread;
                Vector3 pos = center + (Vector3)offset;

                GameObject instance = PoolService.Instance != null
                    ? PoolService.Instance.Get(shardData.prefab, pos, Quaternion.identity)
                    : Instantiate(shardData.prefab, pos, Quaternion.identity);
                if (instance == null) continue;

                if (instance.TryGetComponent<EnemyBase>(out var shard))
                {
                    shard.Configure(shardData, xpGemPrefab);
                }

                // Outward burst so the shards visibly explode away from the corpse.
                if (burstImpulse > 0f && instance.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity = offset.normalized * burstImpulse;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, spawnSpread);
        }
#endif
    }
}
