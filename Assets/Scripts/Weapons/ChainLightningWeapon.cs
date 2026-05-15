using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    /// On each fire, strikes the nearest enemy and chains to up to <see cref="maxJumps"/>
    /// additional nearest enemies within <see cref="chainRange"/>. Damage halves on each jump.
    /// Visualizes each segment with a pooled <see cref="LightningBolt"/> flash.
    public class ChainLightningWeapon : WeaponBase
    {
        [Header("Chain")]
        [Tooltip("Maximum number of additional enemies the strike chains to AFTER the initial target.")]
        [SerializeField] private int maxJumps = 3;

        [Tooltip("Maximum distance (world units) between consecutive chain targets.")]
        [SerializeField] private float chainRange = 4f;

        [Header("Visual")]
        [Tooltip("Pooled visual-only LineRenderer prefab (has a LightningBolt component).")]
        [SerializeField] private GameObject lightningBoltPrefab;

        [Tooltip("Base on-screen lifetime of each segment flash. Scales with projectile-speed upgrades for longer flashes.")]
        [SerializeField] private float baseBoltLifetime = 0.12f;

        // Reused per-fire to avoid allocations.
        private readonly HashSet<EnemyBase> _alreadyHit = new();

        protected override bool TryFire()
        {
            if (data == null) return false;

            EnemyBase target = EnemyRegistry.FindNearest(transform.position, data.range);
            if (target == null) return false;

            _alreadyHit.Clear();

            // Effective stats.
            float baseDamage = CurrentDamage;
            int totalJumps = Mathf.Max(0, maxJumps + _bonusPierce);
            // Projectile-speed upgrade interpreted as visual flash duration multiplier.
            float boltLifetime = baseBoltLifetime * Mathf.Max(0.05f, _projectileSpeedMultiplier);

            Vector3 sourcePosition = transform.position;
            EnemyBase current = target;
            int jumpIndex = 0;

            // Total iterations: 1 initial strike + totalJumps chain jumps.
            int totalSegments = 1 + totalJumps;
            for (int seg = 0; seg < totalSegments; seg++)
            {
                if (current == null) break;

                float segmentDamage = baseDamage * Mathf.Pow(0.5f, jumpIndex);
                ApplyStrike(sourcePosition, current, segmentDamage, boltLifetime, isFinal: seg == totalSegments - 1);

                _alreadyHit.Add(current);

                // Prepare next jump: find nearest enemy within chainRange that hasn't been hit yet.
                sourcePosition = current.transform.position;
                if (seg >= totalSegments - 1) break;
                current = FindNearestUnhit(sourcePosition, chainRange);
                jumpIndex++;
            }

            return true;
        }

        private void ApplyStrike(Vector3 from, EnemyBase to, float damage, float boltLifetime, bool isFinal)
        {
            if (to == null) return;

            to.ApplyDamage(damage);

            // Visual segment.
            if (lightningBoltPrefab != null)
            {
                Vector3 endPos = to.transform.position;
                Vector3 spawnPos = (from + endPos) * 0.5f;
                GameObject go = PoolService.Instance != null
                    ? PoolService.Instance.Get(lightningBoltPrefab, spawnPos, Quaternion.identity)
                    : Instantiate(lightningBoltPrefab, spawnPos, Quaternion.identity);

                var bolt = go.GetComponent<LightningBolt>();
                if (bolt != null) bolt.Configure(from, endPos, boltLifetime);
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.ProjectileImpact(to.transform.position);
                FxService.Instance.DamagePopup(to.transform.position, damage);
                if (isFinal) FxService.Instance.LightShake();
            }
        }

        private EnemyBase FindNearestUnhit(Vector3 origin, float maxRange)
        {
            EnemyBase best = null;
            float bestSq = maxRange * maxRange;
            var enemies = EnemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                if (_alreadyHit.Contains(e)) continue;
                float sq = ((Vector2)e.transform.position - (Vector2)origin).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }
            return best;
        }
    }
}
