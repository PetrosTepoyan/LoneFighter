using UnityEngine;
using LoneFighter.Pickups;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// ScriptableObject describing the loot an Elite drops on death. Spawns its contents
    /// at a given world position; uses <see cref="PoolService"/> when available so
    /// pooled prefabs are recycled instead of <c>Instantiate</c>'d.
    ///
    /// Default contents (overridable in the inspector):
    ///   * Guaranteed: one gold <see cref="XPGem"/> with <c>value = 5</c>.
    ///   * 25% chance: a <see cref="HealthPickup"/>.
    ///   * 10% chance: a <see cref="MagnetPickup"/>.
    ///   *  5% chance: a <see cref="BombPickup"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Elite Drop Table", fileName = "EliteDropTable")]
    public class EliteDropTable : ScriptableObject
    {
        [Header("Guaranteed Gold Gem")]
        [Tooltip("XPGem prefab spawned 100% of the time on elite death.")]
        [SerializeField] private GameObject goldGemPrefab;

        [Tooltip("XP value assigned to the guaranteed gem.")]
        [Min(1)]
        [SerializeField] private int goldGemValue = 5;

        [Header("Chance Drops")]
        [SerializeField] private GameObject healthPickupPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float healthPickupChance = 0.25f;

        [SerializeField] private GameObject magnetPickupPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float magnetPickupChance = 0.10f;

        [SerializeField] private GameObject bombPickupPrefab;
        [Range(0f, 1f)]
        [SerializeField] private float bombPickupChance = 0.05f;

        [Header("Scatter")]
        [Tooltip("Random radius around the death point to scatter the pickups so they don't perfectly overlap.")]
        [Min(0f)]
        [SerializeField] private float scatterRadius = 0.4f;

        /// <summary>
        /// Spawn the contents of this table at <paramref name="position"/>. Safe to call
        /// from <see cref="EnemyHealth.OnDied"/>; this runs before the enemy returns to
        /// its pool, so the position is still valid.
        /// </summary>
        public void SpawnAt(Vector3 position)
        {
            // Guaranteed gold gem.
            var gem = Spawn(goldGemPrefab, position);
            if (gem != null)
            {
                var xp = gem.GetComponent<XPGem>();
                if (xp != null) xp.SetValue(goldGemValue);
            }

            TrySpawn(healthPickupPrefab, healthPickupChance, position);
            TrySpawn(magnetPickupPrefab, magnetPickupChance, position);
            TrySpawn(bombPickupPrefab, bombPickupChance, position);
        }

        private void TrySpawn(GameObject prefab, float chance, Vector3 position)
        {
            if (prefab == null || chance <= 0f) return;
            if (Random.value > chance) return;
            Spawn(prefab, position);
        }

        private GameObject Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return null;
            Vector3 jitter = scatterRadius > 0f
                ? (Vector3)(Random.insideUnitCircle * scatterRadius)
                : Vector3.zero;
            Vector3 final = position + jitter;
            return PoolService.Instance != null
                ? PoolService.Instance.Get(prefab, final, Quaternion.identity)
                : Object.Instantiate(prefab, final, Quaternion.identity);
        }
    }
}
