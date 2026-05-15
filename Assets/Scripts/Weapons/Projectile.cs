using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        private Rigidbody2D _rb;
        private float _damage;
        private int _remainingPierce;
        private float _lifeRemaining;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        public void Launch(Vector2 direction, float speed, float damage, int pierce, float lifetime)
        {
            _damage = damage;
            _remainingPierce = pierce;
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
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy == null) return;

            enemy.ApplyDamage(_damage);

            if (FxService.Instance != null)
            {
                FxService.Instance.ProjectileImpact(transform.position);
                FxService.Instance.DamagePopup(enemy.transform.position, _damage);
            }

            if (_remainingPierce > 0) _remainingPierce--;
            else ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
