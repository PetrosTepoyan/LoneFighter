using UnityEngine;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Briefly pushes the enemy in the direction the hit came from, then yields
    /// back to whatever AI is driving the Rigidbody2D.
    ///
    /// We don't sleep the AI permanently — chase AIs like
    /// <c>EnemyChaseAI.FixedUpdate</c> overwrite linearVelocity every physics
    /// step, so a single AddForce gets erased. Instead we hold the velocity at
    /// the knockback vector for a tiny window (~0.1s) and then release control.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyKnockback : MonoBehaviour
    {
        [Tooltip("Initial knockback force magnitude in world units per second.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 6f;
        [Tooltip("How long the knockback velocity is held before AI takes over.")]
        [SerializeField, Min(0f)] private float duration = 0.1f;

        private Rigidbody2D _rb;
        private float _remaining;
        private Vector2 _velocity;

        // We need to suppress the AI's FixedUpdate-driven velocity for a moment.
        // Toggling EnemyChaseAI works for the most common case; other AIs that
        // also drive velocity may need their own integration, but for this
        // project the chase AI is the universal mover.
        private Behaviour _chaseAi;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            // EnemyChaseAI lives in a sibling project namespace. We resolve it
            // by type name to avoid a hard reference cycle.
            var components = GetComponents<Behaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                if (components[i].GetType().Name == "EnemyChaseAI")
                {
                    _chaseAi = components[i];
                    break;
                }
            }
        }

        private void OnDisable()
        {
            // Make sure we don't leave AI disabled when the enemy is pooled away.
            if (_chaseAi != null) _chaseAi.enabled = true;
            _remaining = 0f;
        }

        /// <summary>
        /// Apply a knockback impulse in <paramref name="direction"/>. The vector
        /// is normalized; passing Vector2.zero is a no-op.
        /// </summary>
        public void Apply(Vector2 direction)
        {
            if (_rb == null) return;
            if (direction.sqrMagnitude < 1e-6f) return;
            _velocity = direction.normalized * knockbackSpeed;
            _remaining = duration;
            if (_chaseAi != null) _chaseAi.enabled = false;
            // Kick the velocity immediately so the hit is felt this frame.
            _rb.linearVelocity = _velocity;
        }

        private void FixedUpdate()
        {
            if (_remaining <= 0f) return;
            // Hold the knockback velocity, decaying linearly toward zero so the
            // enemy slides instead of stopping on a dime.
            float t = Mathf.Clamp01(_remaining / Mathf.Max(0.0001f, duration));
            _rb.linearVelocity = _velocity * t;

            _remaining -= Time.fixedDeltaTime;
            if (_remaining <= 0f)
            {
                if (_chaseAi != null) _chaseAi.enabled = true;
            }
        }
    }
}
