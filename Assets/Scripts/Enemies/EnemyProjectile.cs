using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    /// Pooled projectile fired by ranged enemies (Spitter). Travels in a straight line,
    /// damages PlayerHealth on trigger enter, and returns to the pool on hit or lifetime expiry.
    /// Mirrors LoneFighter.Weapons.Projectile but targets the Player instead of enemies.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        private Rigidbody2D _rb;
        private float _damage;
        private float _lifeRemaining;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        public void Launch(Vector2 direction, float speed, float damage, float lifetime)
        {
            _damage = damage;
            _lifeRemaining = lifetime;
            _rb.linearVelocity = direction * speed;
        }

        private void Update()
        {
            _lifeRemaining -= Time.deltaTime;
            if (_lifeRemaining <= 0f) ReturnToPool();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Ignore other enemies and their projectiles outright.
            if (other.GetComponentInParent<EnemyBase>() != null) return;
            if (other.GetComponentInParent<EnemyProjectile>() != null) return;

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            health.ApplyDamage(_damage);

            if (FxService.Instance != null)
            {
                FxService.Instance.ProjectileImpact(transform.position);
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
