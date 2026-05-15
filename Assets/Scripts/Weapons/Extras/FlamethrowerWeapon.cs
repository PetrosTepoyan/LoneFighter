using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Systems;
using LoneFighter.Weapons;

namespace LoneFighter.Weapons.Extras
{
    /// Flamethrower weapon. Continuous fire — TryFire runs every CurrentCooldown seconds (driven
    /// by WeaponBase). Default cooldown should be ~0.1 s for a feel-of-continuous stream. Each
    /// tick aims at the nearest enemy (via EnemyRegistry) and asks the persistent <see cref="FlameCone"/>
    /// child to spray + damage in that direction. Per-tick damage is low; the DPS comes from the
    /// 10 Hz tick rate.
    ///
    /// WeaponData.weaponComponent: "Extras.FlamethrowerWeapon" (or fully-qualified
    /// "LoneFighter.Weapons.Extras.FlamethrowerWeapon").
    ///
    /// Suggested WeaponData values:
    ///   damage = 3              (per tick; 10 ticks/sec -> 30 base DPS)
    ///   cooldown = 0.1
    ///   projectileSpeed = 1     (multiplier on base cone range — 1.0 means use baseConeRange as-is)
    ///   projectileLifetime = 0  (unused)
    ///   pierce = 0              (each pierce upgrade widens the cone — see FlameCone)
    ///   range = 3.5             (search radius for the aim target)
    ///   projectilePrefab = unused (the FlameCone is a persistent child of this weapon)
    ///
    /// Upgrade mapping:
    ///   - Damage upgrades scale per-tick damage (via WeaponBase).
    ///   - Cooldown upgrades shrink the tick interval (via WeaponBase).
    ///   - ProjectileSpeed upgrades extend the cone range.
    ///   - Pierce upgrades widen the cone half-angle.
    public class FlamethrowerWeapon : WeaponBase
    {
        [Header("Cone")]
        [Tooltip("Base cone length in world units (extended by projectile-speed upgrades).")]
        [SerializeField] private float baseConeRange = 3.5f;

        [Tooltip("Base cone half-angle in degrees (widened by pierce upgrades).")]
        [SerializeField] private float baseConeHalfAngleDeg = 22f;

        [Tooltip("Degrees added to cone half-angle per pierce point from upgrades.")]
        [SerializeField] private float halfAngleDegPerPierce = 6f;

        [Header("Aim")]
        [Tooltip("If no enemy is in range, the flame fires in this fallback direction (player's last aim, treated as local +X).")]
        [SerializeField] private Vector2 fallbackDir = Vector2.right;

        [Header("Authoring")]
        [Tooltip("Existing FlameCone child to drive. If null, this weapon will auto-create one on Initialize.")]
        [SerializeField] private FlameCone flameCone;

        [Tooltip("Optional prefab used to spawn the FlameCone child when no flameCone is wired up. Should contain a particle ParticleSystem + (optional) Collider2D — the FlameCone script will be added if absent.")]
        [SerializeField] private GameObject flameConePrefab;

        // Cached last-aim direction; persists between ticks so the visual cone doesn't snap back
        // to the fallback the instant the last enemy dies.
        private Vector2 _lastAimDir = Vector2.right;

        public override void Initialize(WeaponData newData)
        {
            base.Initialize(newData);
            EnsureFlameCone();
        }

        private void EnsureFlameCone()
        {
            if (flameCone != null) return;

            GameObject host;
            if (flameConePrefab != null)
            {
                host = Instantiate(flameConePrefab, transform.position, Quaternion.identity, transform);
                host.transform.localPosition = Vector3.zero;
                host.transform.localRotation = Quaternion.identity;
            }
            else
            {
                host = new GameObject("FlameCone");
                host.transform.SetParent(transform, false);
            }
            flameCone = host.GetComponent<FlameCone>();
            if (flameCone == null) flameCone = host.AddComponent<FlameCone>();
        }

        protected override bool TryFire()
        {
            if (data == null) return false;

            EnsureFlameCone();

            // Aim acquisition: nearest enemy within data.range; otherwise reuse last aim.
            Vector2 origin = transform.position;
            var target = EnemyRegistry.FindNearest(origin, data.range);
            Vector2 aim;
            if (target != null)
            {
                aim = ((Vector2)target.transform.position - origin);
                if (aim.sqrMagnitude < 0.0001f) aim = _lastAimDir;
                else aim = aim.normalized;
                _lastAimDir = aim;
            }
            else
            {
                aim = _lastAimDir.sqrMagnitude > 0.0001f ? _lastAimDir.normalized : fallbackDir.normalized;
            }

            // Effective stats.
            float coneRange = baseConeRange * Mathf.Max(0.1f, _projectileSpeedMultiplier);
            float coneHalfAngle = baseConeHalfAngleDeg + (halfAngleDegPerPierce * CurrentPierce);
            float tickDamage = CurrentDamage;

            if (flameCone != null)
            {
                flameCone.Tick(origin, aim, coneRange, coneHalfAngle, tickDamage);
            }

            // Returning true gates the WeaponBase cooldown to CurrentCooldown (the configured tick rate).
            return true;
        }

        private void OnDisable()
        {
            if (flameCone != null) flameCone.Quiet();
        }
    }
}
