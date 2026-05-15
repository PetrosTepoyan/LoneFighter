using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Systems;
using LoneFighter.Weapons;

namespace LoneFighter.Weapons.Extras
{
    /// Boomerang weapon. On TryFire, locks onto the nearest enemy (via EnemyRegistry), spawns
    /// a pooled <see cref="Boomerang"/> aimed in that direction. The boomerang flies outward for
    /// ~outboundDuration seconds, decelerates, then arcs back to the player while continuing
    /// to damage enemies it overlaps on both legs of the trip.
    ///
    /// WeaponData.weaponComponent: "Extras.BoomerangWeapon" (the WeaponInventory resolver
    /// prepends "LoneFighter.Weapons." for short names; sub-namespace prefix is required).
    /// A fully-qualified "LoneFighter.Weapons.Extras.BoomerangWeapon" also works.
    ///
    /// Suggested WeaponData values:
    ///   damage = 18
    ///   cooldown = 1.1
    ///   projectileSpeed = 9      (outbound speed)
    ///   projectileLifetime = 1.8 (informational; Boomerang manages its own lifetime)
    ///   pierce = 2               (per-leg pierce; further pierce upgrades stack on top)
    ///   range = 7                (used for target acquisition + outbound flight distance)
    ///   projectilePrefab = a Boomerang prefab (see Boomerang.cs for required components)
    public class BoomerangWeapon : WeaponBase
    {
        [Header("Flight")]
        [Tooltip("How long the boomerang flies outward before it begins decelerating and returning.")]
        [SerializeField] private float outboundDuration = 0.8f;

        [Tooltip("Maximum total time before the boomerang force-returns to the pool (safety clamp).")]
        [SerializeField] private float maxLifetime = 3.0f;

        [Tooltip("Speed multiplier applied to the return leg (the boomerang chases the player).")]
        [SerializeField] private float returnSpeedMultiplier = 1.15f;

        protected override bool TryFire()
        {
            if (data == null || data.projectilePrefab == null) return false;

            var target = EnemyRegistry.FindNearest(transform.position, data.range);
            if (target == null) return false;

            Vector2 origin = transform.position;
            Vector2 dir = ((Vector2)target.transform.position - origin).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(data.projectilePrefab, origin, rotation)
                : Instantiate(data.projectilePrefab, origin, rotation);

            var boomerang = instance.GetComponent<Boomerang>();
            if (boomerang == null) boomerang = instance.AddComponent<Boomerang>();

            boomerang.Launch(
                owner: transform,
                direction: dir,
                outboundSpeed: CurrentProjectileSpeed,
                outboundDuration: outboundDuration,
                returnSpeedMultiplier: returnSpeedMultiplier,
                maxLifetime: maxLifetime,
                damage: CurrentDamage,
                piercePerLeg: CurrentPierce);

            return true;
        }
    }
}
