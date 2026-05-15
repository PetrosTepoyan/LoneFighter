using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    /// Fires multiple pooled <see cref="Projectile"/> instances per shot, evenly distributed
    /// across a cone aimed at the nearest enemy. Reuses the same projectile/pool pipeline as
    /// <see cref="ProjectileWeapon"/>; only the per-fire fan-out is added.
    public class SpreadShotgunWeapon : WeaponBase
    {
        [Header("Spread")]
        [Tooltip("Number of pellets fired per shot.")]
        [SerializeField] private int pelletCount = 5;

        [Tooltip("Total cone width in degrees. Pellets are distributed evenly across this arc.")]
        [SerializeField] private float spreadDegrees = 30f;

        protected override bool TryFire()
        {
            if (data == null || data.projectilePrefab == null) return false;

            var target = EnemyRegistry.FindNearest(transform.position, data.range);
            if (target == null) return false;

            Vector2 origin = transform.position;
            Vector2 baseDir = ((Vector2)target.transform.position - origin).normalized;
            float baseAngleDeg = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            int pellets = Mathf.Max(1, pelletCount);
            float damage = CurrentDamage;
            float speed = CurrentProjectileSpeed;
            int pierce = CurrentPierce;
            float lifetime = data.projectileLifetime;

            // Distribute pellets evenly across the cone. With one pellet we shoot straight ahead;
            // with N pellets the outer pellets sit at +/- spread/2 and the inner ones interpolate.
            for (int i = 0; i < pellets; i++)
            {
                float t = pellets == 1 ? 0.5f : (float)i / (pellets - 1);
                float offset = (t - 0.5f) * spreadDegrees;
                float angleDeg = baseAngleDeg + offset;
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                Quaternion rotation = Quaternion.Euler(0f, 0f, angleDeg);

                GameObject instance = PoolService.Instance != null
                    ? PoolService.Instance.Get(data.projectilePrefab, origin, rotation)
                    : Instantiate(data.projectilePrefab, origin, rotation);

                var projectile = instance.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Launch(dir, speed, damage, pierce, lifetime);
                }
            }

            return true;
        }
    }
}
