using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons.Extras
{
    /// Runtime entity spawned by <see cref="BlackHoleWeapon"/>. Pulls nearby enemies inward each
    /// FixedUpdate using a (1 - distance/radius) falloff. On death, runs an OverlapCircle damage
    /// sweep within <see cref="_detonationRadius"/>, plays the LevelUpBurst FX, and HeavyShakes
    /// the camera. The pull works by directly nudging each enemy's Rigidbody2D toward the hole
    /// (which cooperates with the chase AI overwriting velocity each FixedUpdate — we use
    /// MovePosition so the engine still resolves collisions cleanly).
    ///
    /// Prefab requirements:
    ///   - SpriteRenderer (visual; an additive/HDR sprite + a TrailRenderer or animated scale
    ///     looks great here, but is optional).
    ///   - No Collider2D required; this script uses Physics2D.OverlapCircle for the final AOE.
    public class BlackHole : MonoBehaviour
    {
        [Tooltip("If true, visual scale animates from 0 -> 1 over the first 20% of life, then holds.")]
        [SerializeField] private bool animateScaleIn = true;

        [Tooltip("Maximum visual scale multiplier (applied to the prefab's original localScale).")]
        [SerializeField] private float maxVisualScale = 1f;

        private float _lifetime;
        private float _age;
        private float _pullRadius;
        private float _detonationRadius;
        private float _pullStrength;
        private float _aoeDamage;
        private LayerMask _enemyLayers;
        private Vector3 _originalScale = Vector3.one;
        private bool _detonated;

        // Reusable buffer for OverlapCircle in the final AOE. 64 is plenty for a mobile arena.
        private static readonly Collider2D[] _overlapBuffer = new Collider2D[64];

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void Launch(float lifetime, float pullRadius, float detonationRadius,
            float pullStrength, float aoeDamage, LayerMask enemyLayers)
        {
            _lifetime = Mathf.Max(0.1f, lifetime);
            _pullRadius = Mathf.Max(0.1f, pullRadius);
            _detonationRadius = Mathf.Max(0.1f, detonationRadius);
            _pullStrength = pullStrength;
            _aoeDamage = aoeDamage;
            _enemyLayers = enemyLayers;
            _age = 0f;
            _detonated = false;

            // Reset scale; OnEnable from the pool may not have run yet on the first frame.
            transform.localScale = animateScaleIn ? Vector3.zero : _originalScale * maxVisualScale;
        }

        private void OnEnable()
        {
            _age = 0f;
            _detonated = false;
        }

        private void Update()
        {
            if (animateScaleIn && _lifetime > 0f)
            {
                float t = Mathf.Clamp01(_age / (_lifetime * 0.2f));
                transform.localScale = Vector3.LerpUnclamped(Vector3.zero, _originalScale * maxVisualScale, t);
            }
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (_detonated) return;

            _age += Time.fixedDeltaTime;

            ApplyPull();

            if (_age >= _lifetime)
            {
                Detonate();
            }
        }

        private void ApplyPull()
        {
            // Pull every live enemy within pullRadius toward the hole, scaled by (1 - d/r).
            // Iterating EnemyRegistry instead of OverlapCircle avoids physics-query allocations
            // on the hot path and is friendly to the EnemyChaseAI velocity-overwrite pattern.
            Vector2 center = transform.position;
            float radiusSq = _pullRadius * _pullRadius;
            var enemies = EnemyRegistry.Enemies;
            float dt = Time.fixedDeltaTime;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                Vector2 ep = e.transform.position;
                Vector2 delta = center - ep;
                float dsq = delta.sqrMagnitude;
                if (dsq > radiusSq || dsq < 0.0001f) continue;

                float d = Mathf.Sqrt(dsq);
                float falloff = 1f - (d / _pullRadius);
                Vector2 dir = delta / d;
                Vector2 step = dir * (_pullStrength * falloff * dt);

                // Prefer MovePosition on the rigidbody so collisions still resolve; fall back to
                // transform translation if no body is present.
                var rb = e.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.MovePosition(rb.position + step);
                }
                else
                {
                    e.transform.position = ep + step;
                }
            }
        }

        private void Detonate()
        {
            _detonated = true;

            int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, _detonationRadius, _overlapBuffer, _enemyLayers);
            for (int i = 0; i < hitCount; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                enemy.ApplyDamage(_aoeDamage);
                if (FxService.Instance != null)
                {
                    FxService.Instance.DamagePopup(enemy.transform.position, _aoeDamage);
                }
                _overlapBuffer[i] = null;
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.LevelUpBurst(transform.position);
                FxService.Instance.HeavyShake();
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _pullRadius > 0f ? _pullRadius : 3.5f);
            Gizmos.color = new Color(1f, 0.4f, 0.6f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, _detonationRadius > 0f ? _detonationRadius : 2.8f);
        }
#endif
    }
}
