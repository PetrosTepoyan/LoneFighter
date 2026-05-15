using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    public class ProjectileWeapon : WeaponBase
    {
        protected override bool TryFire()
        {
            if (data == null || data.projectilePrefab == null) return false;

            var target = EnemyRegistry.FindNearest(transform.position, data.range);
            if (target == null) return false;

            Vector2 origin = transform.position;
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(data.projectilePrefab, origin, rotation)
                : Instantiate(data.projectilePrefab, origin, rotation);

            var projectile = instance.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Launch(dir, CurrentProjectileSpeed, CurrentDamage, CurrentPierce, data.projectileLifetime);
            }
            return true;
        }
    }
}
