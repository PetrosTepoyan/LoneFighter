using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyChaseAI))]
    public class EnemyBase : MonoBehaviour
    {
        [SerializeField] private EnemyData data;
        [SerializeField] private GameObject xpGemPrefab;

        private EnemyHealth _health;
        private EnemyChaseAI _ai;
        private float _lastContactTime = -999f;

        public EnemyData Data => data;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _ai = GetComponent<EnemyChaseAI>();
        }

        private void OnEnable()
        {
            if (data != null)
            {
                _health.Initialize(data.maxHealth);
                _ai.Configure(data.moveSpeed);
            }
            EnemyRegistry.Register(this);
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(this);
        }

        public void Configure(EnemyData newData, GameObject gemPrefab)
        {
            data = newData;
            xpGemPrefab = gemPrefab;
            if (isActiveAndEnabled && data != null)
            {
                _health.Initialize(data.maxHealth);
                _ai.Configure(data.moveSpeed);
            }
        }

        public void ApplyDamage(float amount)
        {
            _health.ApplyDamage(amount);
        }

        public void HandleDeath()
        {
            if (data != null && xpGemPrefab != null && Random.value <= data.gemDropChance)
            {
                var gem = PoolService.Instance != null
                    ? PoolService.Instance.Get(xpGemPrefab, transform.position, Quaternion.identity)
                    : Instantiate(xpGemPrefab, transform.position, Quaternion.identity);
                var pickup = gem.GetComponent<Pickups.XPGem>();
                if (pickup != null) pickup.SetValue(data.xpDrop);
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.EnemyExplosion(transform.position);
                FxService.Instance.LightShake();
            }

            if (GameManager.Instance != null) GameManager.Instance.RegisterKill();

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryDamagePlayer(collision.collider);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamagePlayer(other);
        }

        private void TryDamagePlayer(Collider2D other)
        {
            if (data == null) return;
            if (Time.time - _lastContactTime < data.contactCooldown) return;
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;
            _lastContactTime = Time.time;
            health.ApplyDamage(data.contactDamage);
        }
    }
}
