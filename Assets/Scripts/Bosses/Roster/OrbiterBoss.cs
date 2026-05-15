using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Bosses.Roster
{
    /// OrbiterBoss — circles the player at a fixed radius, periodically firing a radial
    /// burst of EnemyProjectile bullets. As its HP drops, both orbit speed and bullet count
    /// scale up; below 25% HP it adds a second wave of inward-traveling bullets, producing
    /// an alternating radial pattern that's much harder to dodge through.
    ///
    /// Composition:
    /// - Add this to a prefab with EnemyBase + EnemyHealth.
    /// - Sibling EnemyChaseAI is disabled at runtime; we drive a Rigidbody2D directly so
    ///   the orbit is deterministic and frame-rate-independent.
    /// - The bullet prefab must be an EnemyProjectile prefab (or compatible) so we can call
    ///   Launch(direction, speed, damage, lifetime) on it after PoolService.Get.
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class OrbiterBoss : MonoBehaviour
    {
        [Header("Orbit")]
        [Tooltip("Fixed orbital radius around the player, in world units.")]
        [SerializeField] private float orbitRadius = 6f;
        [Tooltip("Base angular speed (degrees per second) at full HP.")]
        [SerializeField] private float baseAngularSpeed = 45f;
        [Tooltip("Direction of orbit: +1 = counter-clockwise, -1 = clockwise.")]
        [SerializeField] private int orbitDirection = 1;

        [Header("Burst Cadence")]
        [Tooltip("Seconds between bursts.")]
        [SerializeField] private float burstInterval = 1.5f;
        [Tooltip("Bullets per burst at full HP.")]
        [SerializeField] private int baseBulletCount = 12;

        [Header("Bullet")]
        [Tooltip("Pooled EnemyProjectile prefab to spawn for each bullet.")]
        [SerializeField] private GameObject bulletPrefab;
        [Tooltip("Bullet travel speed.")]
        [SerializeField] private float bulletSpeed = 4.5f;
        [Tooltip("Damage applied to PlayerHealth on hit.")]
        [SerializeField] private float bulletDamage = 8f;
        [Tooltip("Lifetime of a bullet in seconds (and indirectly its range).")]
        [SerializeField] private float bulletLifetime = 2.5f;

        [Header("Phase Thresholds (HP fraction)")]
        [Tooltip("Below this HP fraction the bullet count doubles and orbit speed scales up.")]
        [Range(0.05f, 1f)] [SerializeField] private float phase2HpThreshold = 0.5f;
        [Tooltip("Below this HP fraction the alternating-inward-bullet pattern unlocks.")]
        [Range(0.05f, 1f)] [SerializeField] private float phase3HpThreshold = 0.25f;
        [Tooltip("Maximum angular speed multiplier reached at 0% HP.")]
        [SerializeField] private float maxAngularSpeedMultiplier = 1.8f;

        [Header("Visual")]
        [Tooltip("Optional OrbitalRing child to set up at runtime. We sync its radius to orbitRadius.")]
        [SerializeField] private OrbitalRing orbitalRing;
        [Tooltip("Prefab containing an OrbitalRing. Used if orbitalRing is null. PoolService is preferred when available.")]
        [SerializeField] private GameObject orbitalRingPrefab;

        // Cached.
        private EnemyBase _enemy;
        private EnemyHealth _health;
        private Rigidbody2D _rb;
        private EnemyChaseAI _chaseAi;

        // Orbit & cadence state.
        private float _angleDeg;
        private float _burstTimer;
        private OrbitalRing _spawnedRing;
        private bool _alternateInward; // toggles each phase-3 burst.

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
            _rb = GetComponent<Rigidbody2D>();
            _chaseAi = GetComponent<EnemyChaseAI>();
            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.freezeRotation = true;
            }
        }

        private void OnEnable()
        {
            if (_chaseAi != null) _chaseAi.enabled = false;
            _burstTimer = burstInterval;
            _alternateInward = false;
            // Seed the orbital angle from current world position so we don't teleport at spawn.
            if (PlayerController.Instance != null)
            {
                Vector2 toBoss = (Vector2)transform.position - (Vector2)PlayerController.Instance.transform.position;
                if (toBoss.sqrMagnitude > 0.0001f)
                {
                    _angleDeg = Mathf.Atan2(toBoss.y, toBoss.x) * Mathf.Rad2Deg;
                }
            }
            SpawnOrbitalRing();
        }

        private void OnDisable()
        {
            if (_chaseAi != null) _chaseAi.enabled = true;
            DespawnOrbitalRing();
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (PlayerController.Instance == null) return;

            // Advance orbit angle by current angular speed.
            _angleDeg += orbitDirection * CurrentAngularSpeed * Time.deltaTime;

            // Burst cadence.
            _burstTimer -= Time.deltaTime;
            if (_burstTimer <= 0f)
            {
                FireBurst();
                _burstTimer = burstInterval;
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || PlayerController.Instance == null) return;
            Vector3 playerPos = PlayerController.Instance.transform.position;
            float rad = _angleDeg * Mathf.Deg2Rad;
            Vector3 target = playerPos + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;

            // Drive position with a velocity that closes the gap in one physics step.
            Vector2 delta = (Vector2)target - (Vector2)transform.position;
            float dt = Time.fixedDeltaTime;
            _rb.linearVelocity = dt > 0f ? delta / dt : Vector2.zero;
        }

        // ---- phase helpers ----------------------------------------------------

        private float HpFraction
        {
            get
            {
                if (_health == null || _health.Max <= 0f) return 1f;
                return Mathf.Clamp01(_health.Current / _health.Max);
            }
        }

        private float CurrentAngularSpeed
        {
            get
            {
                // 1.0 at full HP, maxAngularSpeedMultiplier at 0% HP, lerped linearly.
                float mult = Mathf.Lerp(maxAngularSpeedMultiplier, 1f, HpFraction);
                return baseAngularSpeed * mult;
            }
        }

        private int CurrentBulletCount
        {
            get
            {
                int n = baseBulletCount;
                if (HpFraction <= phase2HpThreshold) n *= 2;
                return Mathf.Max(1, n);
            }
        }

        // ---- bullet firing ----------------------------------------------------

        private void FireBurst()
        {
            if (bulletPrefab == null) return;
            int count = CurrentBulletCount;
            float step = 360f / count;

            // Phase 1/2 bullets aim inward from the boss toward the player and fan out
            // around that direction.
            Vector2 origin = transform.position;
            Vector2 toPlayer = PlayerController.Instance != null
                ? ((Vector2)PlayerController.Instance.transform.position - origin)
                : Vector2.right;
            float baseAngle = toPlayer.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg
                : 0f;

            for (int i = 0; i < count; i++)
            {
                float a = baseAngle - 180f + step * i; // distribute around the boss
                FireOne(origin, a);
            }

            // Phase 3: alternate inward bullets, offset by half a step so they interleave.
            if (HpFraction <= phase3HpThreshold)
            {
                _alternateInward = !_alternateInward;
                if (_alternateInward)
                {
                    float offset = step * 0.5f;
                    for (int i = 0; i < count; i++)
                    {
                        // Inward = aim at player. We fire from a point on the ring at angle (baseAngle + offset + step*i)
                        // and direct the bullet from that origin straight toward the player.
                        float ringAngleDeg = baseAngle + offset + step * i;
                        float ringRad = ringAngleDeg * Mathf.Deg2Rad;
                        Vector2 spawnPos = origin + new Vector2(Mathf.Cos(ringRad), Mathf.Sin(ringRad)) * 0.4f;
                        Vector2 dir = ((Vector2)PlayerController.Instance.transform.position - spawnPos);
                        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
                        dir.Normalize();
                        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        LaunchProjectile(spawnPos, angleDeg, dir);
                    }
                }
            }
        }

        private void FireOne(Vector2 origin, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            LaunchProjectile(origin, angleDegrees, dir);
        }

        private void LaunchProjectile(Vector2 origin, float angleDeg, Vector2 dir)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg);
            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(bulletPrefab, origin, rot)
                : Instantiate(bulletPrefab, origin, rot);
            if (instance == null) return;

            var proj = instance.GetComponent<EnemyProjectile>();
            if (proj != null) proj.Launch(dir, bulletSpeed, bulletDamage, bulletLifetime);
        }

        // ---- orbital ring -----------------------------------------------------

        private void SpawnOrbitalRing()
        {
            if (orbitalRing != null)
            {
                orbitalRing.gameObject.SetActive(true);
                orbitalRing.SetRadius(orbitRadius);
                if (PlayerController.Instance != null) orbitalRing.SetCenter(PlayerController.Instance.transform);
                _spawnedRing = orbitalRing;
                return;
            }
            if (orbitalRingPrefab == null) return;

            Vector3 pos = PlayerController.Instance != null ? PlayerController.Instance.transform.position : transform.position;
            GameObject go = PoolService.Instance != null
                ? PoolService.Instance.Get(orbitalRingPrefab, pos, Quaternion.identity)
                : Instantiate(orbitalRingPrefab, pos, Quaternion.identity);
            if (go == null) return;
            _spawnedRing = go.GetComponent<OrbitalRing>();
            if (_spawnedRing != null)
            {
                _spawnedRing.SetRadius(orbitRadius);
                if (PlayerController.Instance != null) _spawnedRing.SetCenter(PlayerController.Instance.transform);
            }
        }

        private void DespawnOrbitalRing()
        {
            if (_spawnedRing == null) return;
            if (_spawnedRing == orbitalRing)
            {
                _spawnedRing.gameObject.SetActive(false);
            }
            else if (PoolService.Instance != null)
            {
                PoolService.Instance.Release(_spawnedRing.gameObject);
            }
            _spawnedRing = null;
        }
    }
}
