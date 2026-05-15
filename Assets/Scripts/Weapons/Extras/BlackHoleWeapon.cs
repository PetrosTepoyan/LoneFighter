using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Systems;
using LoneFighter.Weapons;

namespace LoneFighter.Weapons.Extras
{
    /// Black Hole weapon. On TryFire, samples enemy positions from EnemyRegistry, picks the
    /// densest cluster centroid within data.range, and spawns a pooled <see cref="BlackHole"/>
    /// there. The hole lives ~lifetime seconds, pulling enemies inward each FixedUpdate, then
    /// detonates with a big AOE damage burst + a heavy camera shake.
    ///
    /// WeaponData.weaponComponent: "Extras.BlackHoleWeapon" (or fully-qualified
    /// "LoneFighter.Weapons.Extras.BlackHoleWeapon"). The WeaponInventory resolver auto-prefixes
    /// short names with "LoneFighter.Weapons.", so the sub-namespace prefix "Extras." is required.
    ///
    /// Suggested WeaponData values:
    ///   damage = 40            (final AOE damage; pull tick uses a small fraction of this)
    ///   cooldown = 6.0
    ///   projectileSpeed = 1    (informational; pullStrengthMultiplier on the component drives the actual pull)
    ///   projectileLifetime = 2.5 (informational; the BlackHole component owns its own lifetime)
    ///   pierce = 0             (informational; each pierce upgrade widens the detonation radius via the component)
    ///   range = 8              (search radius for the densest enemy cluster)
    ///   projectilePrefab = a BlackHole prefab (Collider2D for AOE, SpriteRenderer for visual)
    public class BlackHoleWeapon : WeaponBase
    {
        [Header("Cluster Search")]
        [Tooltip("Radius used when scoring how 'dense' a candidate centroid is. Enemies within this distance contribute to the score.")]
        [SerializeField] private float densitySampleRadius = 2.5f;

        [Tooltip("Minimum number of enemies the densest cluster must contain to fire.")]
        [SerializeField] private int minClusterSize = 1;

        [Header("Black Hole Tuning")]
        [Tooltip("Lifetime of the black hole entity before it detonates.")]
        [SerializeField] private float lifetime = 2.5f;

        [Tooltip("Pull radius. Enemies within this distance are pulled in each FixedUpdate.")]
        [SerializeField] private float pullRadius = 3.5f;

        [Tooltip("Detonation radius (AOE damage on death).")]
        [SerializeField] private float detonationRadius = 2.8f;

        [Tooltip("Pull strength scalar applied to (1 - distance/radius) when moving enemies inward.")]
        [SerializeField] private float pullStrength = 5.5f;

        [Tooltip("Layer mask for enemy colliders during the final detonation OverlapCircle.")]
        [SerializeField] private LayerMask enemyLayers = ~0;

        protected override bool TryFire()
        {
            if (data == null || data.projectilePrefab == null) return false;
            if (EnemyRegistry.Count == 0) return false;

            if (!TryFindDensestCentroid(transform.position, data.range, densitySampleRadius,
                out Vector2 centroid, out int clusterSize))
            {
                return false;
            }
            if (clusterSize < minClusterSize) return false;

            var rotation = Quaternion.identity;
            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(data.projectilePrefab, centroid, rotation)
                : Instantiate(data.projectilePrefab, centroid, rotation);

            var hole = instance.GetComponent<BlackHole>();
            if (hole == null) hole = instance.AddComponent<BlackHole>();

            // Pierce upgrades widen the detonation radius (each pierce point = +25% radius);
            // damage upgrades scale both pull-tick and detonation; cooldown handled by WeaponBase;
            // projectile-speed upgrades juice the pull strength so a "fast" black hole sucks harder.
            float pierceBonus = 1f + 0.25f * CurrentPierce;
            float effectiveDetonationRadius = detonationRadius * pierceBonus;
            float effectivePullStrength = pullStrength * Mathf.Max(0.1f, _projectileSpeedMultiplier);

            hole.Launch(
                lifetime: lifetime,
                pullRadius: pullRadius,
                detonationRadius: effectiveDetonationRadius,
                pullStrength: effectivePullStrength,
                aoeDamage: CurrentDamage,
                enemyLayers: enemyLayers);

            return true;
        }

        /// Finds the densest enemy cluster within <paramref name="searchRadius"/> of
        /// <paramref name="origin"/>. Implementation: every live enemy in EnemyRegistry within
        /// searchRadius is a candidate centroid; we score it by the number of other enemies
        /// within <paramref name="densityRadius"/> and pick the highest-scoring candidate's
        /// average position as the centroid. O(n^2) over enemy count, which is fine at this
        /// scale and runs at most once per cooldown.
        private static bool TryFindDensestCentroid(Vector2 origin, float searchRadius,
            float densityRadius, out Vector2 centroid, out int clusterSize)
        {
            centroid = origin;
            clusterSize = 0;

            var enemies = EnemyRegistry.Enemies;
            int count = enemies.Count;
            if (count == 0) return false;

            float searchSq = searchRadius * searchRadius;
            float densitySq = densityRadius * densityRadius;

            int bestScore = -1;
            Vector2 bestCentroid = origin;

            for (int i = 0; i < count; i++)
            {
                var a = enemies[i];
                if (a == null || !a.isActiveAndEnabled) continue;
                Vector2 pa = a.transform.position;
                if ((pa - origin).sqrMagnitude > searchSq) continue;

                int score = 0;
                Vector2 sum = Vector2.zero;
                for (int j = 0; j < count; j++)
                {
                    var b = enemies[j];
                    if (b == null || !b.isActiveAndEnabled) continue;
                    Vector2 pb = b.transform.position;
                    if ((pb - pa).sqrMagnitude <= densitySq)
                    {
                        score++;
                        sum += pb;
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCentroid = score > 0 ? (sum / score) : pa;
                }
            }

            if (bestScore <= 0) return false;

            centroid = bestCentroid;
            clusterSize = bestScore;
            return true;
        }
    }
}
