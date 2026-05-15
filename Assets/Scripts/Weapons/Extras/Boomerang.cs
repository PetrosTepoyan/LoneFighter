using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons.Extras
{
    /// Runtime entity spawned by <see cref="BoomerangWeapon"/>. Lives in three phases:
    ///   1) Outbound: flies in the launch direction at outboundSpeed for outboundDuration seconds.
    ///   2) Decelerate / pivot: speed lerps toward 0 then reverses toward the owner.
    ///   3) Return: arcs back to the owner. When close enough (or maxLifetime expires) it returns
    ///      to the pool.
    ///
    /// Damage is applied via OnTriggerEnter2D / OnTriggerStay2D. Enemies are re-hittable across
    /// legs (the hit-set is cleared once when the return leg begins) so a well-aimed throw can
    /// damage a target twice. A per-leg pierce budget prevents indefinite damage on dense crowds.
    ///
    /// Required prefab components: Rigidbody2D (kinematic-friendly; gravity 0), Collider2D
    /// configured as a trigger, SpriteRenderer for visuals. Optional TrailRenderer for juice.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class Boomerang : MonoBehaviour
    {
        [Tooltip("Visual spin speed of the boomerang sprite, in degrees per second.")]
        [SerializeField] private float spinDegPerSec = 720f;

        [Tooltip("Distance at which the return leg considers itself 'caught' by the owner.")]
        [SerializeField] private float catchDistance = 0.35f;

        private Rigidbody2D _rb;
        private Transform _owner;
        private Vector2 _outboundDir;
        private float _outboundSpeed;
        private float _outboundDuration;
        private float _returnSpeedMultiplier;
        private float _maxLifetime;
        private float _damage;
        private int _basePiercePerLeg;

        private float _age;
        private bool _returning;
        private int _legPierceRemaining;

        // Hit-set is cleared once between legs so an enemy can be damaged on both outbound + return.
        private readonly HashSet<EnemyBase> _hitThisLeg = new();

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        public void Launch(Transform owner, Vector2 direction, float outboundSpeed,
            float outboundDuration, float returnSpeedMultiplier, float maxLifetime,
            float damage, int piercePerLeg)
        {
            _owner = owner;
            _outboundDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _outboundSpeed = Mathf.Max(0.1f, outboundSpeed);
            _outboundDuration = Mathf.Max(0.05f, outboundDuration);
            _returnSpeedMultiplier = Mathf.Max(0.1f, returnSpeedMultiplier);
            _maxLifetime = Mathf.Max(_outboundDuration + 0.1f, maxLifetime);
            _damage = damage;
            // Per-leg pierce gives each leg its own budget. A single hostile crowd won't soak the return.
            _basePiercePerLeg = Mathf.Max(0, piercePerLeg);

            _age = 0f;
            _returning = false;
            _legPierceRemaining = _basePiercePerLeg;
            _hitThisLeg.Clear();

            _rb.linearVelocity = _outboundDir * _outboundSpeed;
        }

        private void OnDisable()
        {
            // Pools recycle GameObjects without going through Awake again. Clear hit-set so a
            // re-launched instance doesn't carry stale references.
            _hitThisLeg.Clear();
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            _age += Time.fixedDeltaTime;
            if (_age >= _maxLifetime)
            {
                ReturnToPool();
                return;
            }

            if (!_returning)
            {
                // Outbound: linearly decelerate over the second half of the outbound window so the
                // pivot to return feels natural rather than a hard direction flip.
                float t = Mathf.Clamp01(_age / _outboundDuration);
                // Ease-out (decel) only on the back half.
                float speedScale = t < 0.5f ? 1f : Mathf.Lerp(1f, 0.1f, (t - 0.5f) * 2f);
                _rb.linearVelocity = _outboundDir * (_outboundSpeed * speedScale);

                if (_age >= _outboundDuration)
                {
                    BeginReturnLeg();
                }
            }
            else
            {
                // Return leg: chase the owner. If the owner vanished (rare; shouldn't happen mid-run)
                // continue on current heading until lifetime expires.
                if (_owner == null)
                {
                    return;
                }

                Vector2 toOwner = ((Vector2)_owner.position - (Vector2)transform.position);
                float dist = toOwner.magnitude;
                if (dist <= catchDistance)
                {
                    ReturnToPool();
                    return;
                }

                Vector2 returnDir = toOwner / Mathf.Max(0.0001f, dist);
                _rb.linearVelocity = returnDir * (_outboundSpeed * _returnSpeedMultiplier);
            }
        }

        private void Update()
        {
            // Visual spin runs independently of the physics step.
            transform.Rotate(0f, 0f, spinDegPerSec * Time.deltaTime, Space.Self);
        }

        private void BeginReturnLeg()
        {
            _returning = true;
            // Reset hit-set + pierce budget so the return can re-damage targets.
            _hitThisLeg.Clear();
            _legPierceRemaining = _basePiercePerLeg;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider2D other)
        {
            if (_damage <= 0f) return;
            var enemy = other.GetComponentInParent<EnemyBase>();
            if (enemy == null || !enemy.isActiveAndEnabled) return;
            if (_hitThisLeg.Contains(enemy)) return;

            _hitThisLeg.Add(enemy);
            enemy.ApplyDamage(_damage);

            if (FxService.Instance != null)
            {
                FxService.Instance.ProjectileImpact(transform.position);
                FxService.Instance.DamagePopup(enemy.transform.position, _damage);
            }

            // Pierce budget controls how many enemies one leg can hit before the boomerang
            // is "consumed". 0 budget means stop on first enemy of the leg, which on the
            // outbound leg force-starts the return early for a tactile feel.
            if (_legPierceRemaining > 0)
            {
                _legPierceRemaining--;
            }
            else
            {
                if (!_returning)
                {
                    BeginReturnLeg();
                }
                else
                {
                    // On return, run out of pierce just means we stop damaging further enemies;
                    // the boomerang still flies home so the visual feels coherent.
                }
            }
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
