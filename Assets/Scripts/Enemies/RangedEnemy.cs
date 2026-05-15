using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    /// Drives the Spitter's firing loop. Pairs with EnemyKeepDistanceAI for movement.
    /// Reads projectile prefab / damage / cooldown from EnemyData when available, otherwise
    /// falls back to the locally serialized overrides.
    [RequireComponent(typeof(EnemyBase))]
    public class RangedEnemy : MonoBehaviour
    {
        [Header("Targeting")]
        [Tooltip("Maximum distance at which the enemy will attempt to shoot. Outside this range it conserves ammo.")]
        [SerializeField] private float fireRange = 8f;
        [Tooltip("Maximum distance at which a fired projectile is allowed to live.")]
        [SerializeField] private float projectileLifetime = 2.5f;
        [Tooltip("Speed of the spawned projectile.")]
        [SerializeField] private float projectileSpeed = 6f;

        [Header("Fallbacks (used when EnemyData is missing)")]
        [SerializeField] private GameObject projectilePrefabFallback;
        [SerializeField] private float damageFallback = 5f;
        [SerializeField] private float cooldownFallback = 1.5f;

        [Header("Firing")]
        [Tooltip("Random offset added to the cooldown to desync salvos.")]
        [SerializeField] private float cooldownJitter = 0.2f;
        [Tooltip("Delay before the first shot after spawn.")]
        [SerializeField] private float initialDelay = 0.5f;

        private EnemyBase _enemy;
        private float _cooldownTimer;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        private void OnEnable()
        {
            _cooldownTimer = initialDelay + Random.Range(0f, cooldownJitter);
        }

        private GameObject ProjectilePrefab => _enemy != null && _enemy.Data != null && _enemy.Data.projectilePrefab != null
            ? _enemy.Data.projectilePrefab
            : projectilePrefabFallback;

        private float Damage => _enemy != null && _enemy.Data != null ? _enemy.Data.projectileDamage : damageFallback;
        private float Cooldown => _enemy != null && _enemy.Data != null && _enemy.Data.fireCooldown > 0f ? _enemy.Data.fireCooldown : cooldownFallback;

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (PlayerController.Instance == null) return;
            if (ProjectilePrefab == null) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer > 0f) return;

            Vector2 origin = transform.position;
            Vector2 toPlayer = (Vector2)PlayerController.Instance.transform.position - origin;
            float dist = toPlayer.magnitude;
            if (dist > fireRange || dist < 0.0001f)
            {
                // Skip the shot but don't fully reset cooldown so we can fire as soon as the player re-enters range.
                _cooldownTimer = 0.15f;
                return;
            }

            Vector2 dir = toPlayer / dist;
            Fire(origin, dir);
            _cooldownTimer = Mathf.Max(0.05f, Cooldown + Random.Range(-cooldownJitter, cooldownJitter));
        }

        private void Fire(Vector2 origin, Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(ProjectilePrefab, origin, rotation)
                : Instantiate(ProjectilePrefab, origin, rotation);
            if (instance == null) return;

            var projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Launch(dir, projectileSpeed, Damage, projectileLifetime);
            }
        }
    }
}
