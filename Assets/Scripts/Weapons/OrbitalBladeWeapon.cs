using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Weapons
{
    /// Spawns and maintains a set of <see cref="OrbitalBlade"/> entities that orbit the player.
    /// Unlike <see cref="ProjectileWeapon"/>, this weapon doesn't fire on a cooldown — the blades
    /// are persistent and apply damage on contact. Upgrades reinterpret as follows:
    ///   - Damage bonus       => scales blade damage
    ///   - Cooldown reduction => increases orbit angular speed
    ///   - Projectile speed   => increases orbit angular speed
    ///   - Pierce             => adds an extra blade (and redistributes angles)
    public class OrbitalBladeWeapon : WeaponBase
    {
        [Header("Orbit")]
        [SerializeField] private int bladeCount = 2;
        [SerializeField] private float orbitRadius = 1.5f;
        [SerializeField] private float orbitSpeedDegPerSec = 240f;

        [Header("Prefab")]
        [Tooltip("Prefab with an OrbitalBlade component (and a trigger Collider2D). Spawned as a non-pooled child of this weapon.")]
        [SerializeField] private GameObject bladePrefab;

        private readonly List<OrbitalBlade> _blades = new();
        private int _lastSpawnedCount = -1;

        public override void Initialize(WeaponData newData)
        {
            base.Initialize(newData);
            EnsureBlades();
            ApplyBladeParameters();
        }

        protected override void Update()
        {
            // Skip the base cooldown/TryFire pipeline entirely — orbital blades are persistent.
            if (data == null) return;
            if (Systems.GameManager.Instance != null && Systems.GameManager.Instance.State != Systems.GameState.Playing) return;

            // bladeCount and upgrade-applied pierce changes are detected each frame so upgrades
            // that bump pierce (reinterpreted as +1 blade) take effect without an explicit hook.
            int targetCount = EffectiveBladeCount;
            if (targetCount != _lastSpawnedCount)
            {
                EnsureBlades();
            }
            ApplyBladeParameters();
        }

        // TryFire is a no-op — orbital blades are always-on contact damage, not periodic spawns.
        protected override bool TryFire() => false;

        // We reinterpret pierce upgrades as "extra blades". Base inspector count + upgrade bonus.
        // Note: data.pierce (typically 999 for this weapon) is intentionally ignored here.
        private int EffectiveBladeCount => Mathf.Max(0, bladeCount + _bonusPierce);

        private void EnsureBlades()
        {
            if (bladePrefab == null) return;
            int target = EffectiveBladeCount;

            // Grow.
            while (_blades.Count < target)
            {
                var go = Instantiate(bladePrefab, transform.position, Quaternion.identity, transform);
                var blade = go.GetComponent<OrbitalBlade>();
                if (blade == null) blade = go.AddComponent<OrbitalBlade>();
                blade.Configure(transform, 0f, orbitRadius, 0f, 0f); // params overwritten below
                _blades.Add(blade);
            }

            // Shrink (e.g. on a hypothetical respec; safe to keep around).
            while (_blades.Count > target)
            {
                int last = _blades.Count - 1;
                var blade = _blades[last];
                _blades.RemoveAt(last);
                if (blade != null) Destroy(blade.gameObject);
            }

            RedistributeAngles();
            _lastSpawnedCount = target;
        }

        private void RedistributeAngles()
        {
            int n = _blades.Count;
            if (n == 0) return;
            float step = (Mathf.PI * 2f) / n;
            for (int i = 0; i < n; i++)
            {
                var blade = _blades[i];
                if (blade == null) continue;
                blade.SetBaseAngle(step * i);
            }
        }

        private void ApplyBladeParameters()
        {
            // Cooldown-reduction and projectile-speed upgrades both push the orbit faster.
            // _cooldownMultiplier shrinks on speed-up (e.g. 1.0 -> 0.88), so we invert it.
            float cooldownFactor = _cooldownMultiplier > 0.0001f ? (1f / _cooldownMultiplier) : 1f;
            float speedFactor = _projectileSpeedMultiplier;
            float angularDegPerSec = orbitSpeedDegPerSec * cooldownFactor * speedFactor;
            float angularRadPerSec = angularDegPerSec * Mathf.Deg2Rad;

            float damage = (data != null ? data.damage : 0f) * _damageMultiplier;

            for (int i = 0; i < _blades.Count; i++)
            {
                var blade = _blades[i];
                if (blade == null) continue;
                blade.SetRadius(orbitRadius);
                blade.SetAngularSpeed(angularRadPerSec);
                blade.SetDamage(damage);
                // Owner transform doesn't change after spawn, but Configure handles a null guard.
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _blades.Count; i++)
            {
                var blade = _blades[i];
                if (blade != null) blade.gameObject.SetActive(true);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _blades.Count; i++)
            {
                var blade = _blades[i];
                if (blade != null) blade.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _blades.Count; i++)
            {
                var blade = _blades[i];
                if (blade != null) Destroy(blade.gameObject);
            }
            _blades.Clear();
        }
    }
}
