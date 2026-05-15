using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Per-enemy poll watcher that detects HP drops on <see cref="EnemyHealth"/>
    /// and reports them to <see cref="HitFeedbackService"/> as discrete hits.
    ///
    /// We can't subscribe to a "took damage" event because EnemyHealth only
    /// exposes <c>OnDied</c>. Instead we cache <c>Current</c> each frame and emit
    /// a hit whenever it drops. This is cheap — one float compare per enemy per
    /// frame — and naturally captures every damage source (melee, ranged, DoT,
    /// status effects) without coupling to any specific damage pipeline.
    ///
    /// Hit direction is best-effort: we use the vector from the player to the
    /// enemy, which is correct for melee and projectile contact and roughly
    /// correct for AoE / orbital weapons.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitListener : MonoBehaviour
    {
        [Tooltip("Below this damage threshold (per frame) we ignore the change. " +
                 "Prevents micro DoT ticks from spamming feedback.")]
        [SerializeField, Min(0f)] private float minReportableDamage = 0.01f;

        private EnemyBase _enemy;
        private EnemyHealth _health;
        private float _lastHp;
        private bool _initialized;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            // Defer initial HP capture until the enemy has been Initialize()'d.
            _initialized = false;
        }

        private void Update()
        {
            if (_health == null) return;

            if (!_initialized)
            {
                _lastHp = _health.Current;
                _initialized = true;
                return;
            }

            float current = _health.Current;
            if (current < _lastHp - minReportableDamage)
            {
                float delta = _lastHp - current;
                Vector2 dir = ResolveHitDirection();
                if (HitFeedbackService.Instance != null)
                {
                    HitFeedbackService.Instance.OnEnemyHit(_enemy, delta, dir);
                }
            }
            // Even on heals or no-change frames, snap our cache to current so
            // the next damage tick reports an accurate delta.
            _lastHp = current;
        }

        private Vector2 ResolveHitDirection()
        {
            // From player to enemy is the standard "push them away" direction.
            var player = PlayerController.Instance;
            if (player == null) return Vector2.right;
            Vector2 dir = (Vector2)transform.position - (Vector2)player.transform.position;
            if (dir.sqrMagnitude < 1e-6f) return Vector2.right;
            return dir.normalized;
        }
    }
}
