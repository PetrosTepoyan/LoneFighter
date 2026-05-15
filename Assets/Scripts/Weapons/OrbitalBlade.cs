using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    /// A single blade orbiting the player. Position is driven each frame from a shared owner
    /// transform plus the blade's individual angle. Damages enemies on trigger overlap with a
    /// per-target re-hit cooldown so it doesn't tick-damage every frame.
    [RequireComponent(typeof(Collider2D))]
    public class OrbitalBlade : MonoBehaviour
    {
        [Tooltip("Re-hit cooldown applied per enemy so the blade doesn't deal damage every frame on overlap.")]
        [SerializeField] private float perTargetHitCooldown = 0.4f;

        private Transform _owner;
        private float _baseAngleRad;
        private float _radius;
        private float _angularSpeedRad;
        private float _damage;

        // Track per-enemy cooldowns. Keys are EnemyBase instances; values are the next time
        // that target may be hit again. We clean up null/destroyed entries lazily on iteration.
        private readonly Dictionary<EnemyBase, float> _nextHitTime = new();

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public void Configure(Transform owner, float baseAngleRad, float radius, float angularSpeedRad, float damage)
        {
            _owner = owner;
            _baseAngleRad = baseAngleRad;
            _radius = radius;
            _angularSpeedRad = angularSpeedRad;
            _damage = damage;
            SyncTransform();
        }

        public void SetBaseAngle(float baseAngleRad) => _baseAngleRad = baseAngleRad;
        public void SetRadius(float radius) => _radius = radius;
        public void SetAngularSpeed(float angularSpeedRad) => _angularSpeedRad = angularSpeedRad;
        public void SetDamage(float damage) => _damage = damage;

        private void LateUpdate()
        {
            SyncTransform();
        }

        private void SyncTransform()
        {
            if (_owner == null) return;
            float theta = _baseAngleRad + Time.time * _angularSpeedRad;
            Vector3 offset = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f) * _radius;
            transform.position = _owner.position + offset;
            // Rotate visually so the blade faces tangent to its orbit (just for visual flavor).
            float facingDeg = (theta * Mathf.Rad2Deg) + 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, facingDeg);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (_damage <= 0f) return;
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy == null || !enemy.isActiveAndEnabled) return;

            float now = Time.time;
            if (_nextHitTime.TryGetValue(enemy, out var nextOk) && now < nextOk) return;
            _nextHitTime[enemy] = now + perTargetHitCooldown;

            enemy.ApplyDamage(_damage);

            if (FxService.Instance != null)
            {
                FxService.Instance.ProjectileImpact(transform.position);
                FxService.Instance.DamagePopup(enemy.transform.position, _damage);
            }
        }

        private void OnDisable()
        {
            _nextHitTime.Clear();
        }
    }
}
