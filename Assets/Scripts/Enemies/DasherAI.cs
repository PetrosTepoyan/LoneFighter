using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Enemies
{
    /// State-machine mover for the Dasher archetype.
    ///
    /// Idle (drifts slowly toward the player) -> Windup (telegraphs with a sprite-color tween)
    /// -> Dash (high-velocity straight at the last-known player position) -> Recover -> Idle.
    ///
    /// MUTUALLY EXCLUSIVE with EnemyChaseAI. Disable EnemyChaseAI on the Dasher prefab and
    /// attach this component instead. EnemyBase still requires an EnemyChaseAI on the GameObject
    /// (for legacy compatibility); leaving it disabled is sufficient because its FixedUpdate
    /// will not run and EnemyBase only calls a stateless Configure(speed) on it.
    [RequireComponent(typeof(Rigidbody2D))]
    public class DasherAI : MonoBehaviour
    {
        private enum State { Idle, Windup, Dash, Recover }

        [Header("Movement")]
        [Tooltip("Slow drift speed while in the Idle phase between dashes.")]
        [SerializeField] private float idleSpeed = 1.2f;
        [Tooltip("Burst speed during a dash.")]
        [SerializeField] private float dashSpeed = 9f;

        [Header("Timing")]
        [Tooltip("Seconds spent loitering before the next windup.")]
        [SerializeField] private float idleDuration = 1.0f;
        [Tooltip("Telegraph duration. The sprite flashes during this window.")]
        [SerializeField] private float windupDuration = 0.5f;
        [Tooltip("Maximum dash duration. The dash also ends early when within stopDistance.")]
        [SerializeField] private float dashDuration = 0.45f;
        [Tooltip("Seconds spent stationary after a dash before the next idle phase.")]
        [SerializeField] private float recoverDuration = 0.4f;

        [Header("Dash")]
        [Tooltip("Distance at which the dash terminates early (avoids overshoot through the player).")]
        [SerializeField] private float stopDistance = 0.3f;

        [Header("Telegraph (optional)")]
        [Tooltip("Sprite renderer used for the windup flash. If null, the first SpriteRenderer in children is used.")]
        [SerializeField] private SpriteRenderer flashRenderer;
        [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.35f, 1f);

        private Rigidbody2D _rb;
        private Transform _target;
        private State _state;
        private float _stateTimer;
        private Vector2 _dashDirection;
        private Color _baseColor = Color.white;
        private bool _baseColorCaptured;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;

            // Auto-pull idle speed from EnemyData (the Dasher's "base" move speed).
            var enemy = GetComponent<EnemyBase>();
            if (enemy != null && enemy.Data != null) idleSpeed = enemy.Data.moveSpeed;

            if (flashRenderer != null)
            {
                _baseColor = flashRenderer.color;
                _baseColorCaptured = true;
            }
            EnterState(State.Idle);
        }

        private void OnDisable()
        {
            if (_baseColorCaptured && flashRenderer != null)
            {
                flashRenderer.color = _baseColor;
            }
        }

        private void FixedUpdate()
        {
            if (_target == null)
            {
                if (PlayerController.Instance != null) _target = PlayerController.Instance.transform;
                else { _rb.linearVelocity = Vector2.zero; return; }
            }

            _stateTimer -= Time.fixedDeltaTime;

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Windup: TickWindup(); break;
                case State.Dash: TickDash(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickIdle()
        {
            Vector2 dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _rb.linearVelocity = dir * idleSpeed;
            if (_stateTimer <= 0f) EnterState(State.Windup);
        }

        private void TickWindup()
        {
            _rb.linearVelocity = Vector2.zero;
            if (flashRenderer != null)
            {
                // Pulse between base color and flash color through the windup.
                float t = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.01f, windupDuration));
                float pulse = Mathf.PingPong(t * 6f, 1f);
                flashRenderer.color = Color.Lerp(_baseColor, flashColor, pulse);
            }
            if (_stateTimer <= 0f)
            {
                // Lock in the dash direction at the moment of commit.
                _dashDirection = ((Vector2)_target.position - (Vector2)transform.position).normalized;
                if (_dashDirection.sqrMagnitude < 0.0001f) _dashDirection = Vector2.right;
                if (flashRenderer != null) flashRenderer.color = _baseColor;
                EnterState(State.Dash);
            }
        }

        private void TickDash()
        {
            _rb.linearVelocity = _dashDirection * dashSpeed;

            float distSq = ((Vector2)_target.position - (Vector2)transform.position).sqrMagnitude;
            if (_stateTimer <= 0f || distSq <= stopDistance * stopDistance)
            {
                EnterState(State.Recover);
            }
        }

        private void TickRecover()
        {
            _rb.linearVelocity = Vector2.zero;
            if (_stateTimer <= 0f) EnterState(State.Idle);
        }

        private void EnterState(State next)
        {
            _state = next;
            switch (next)
            {
                case State.Idle: _stateTimer = idleDuration; break;
                case State.Windup: _stateTimer = windupDuration; break;
                case State.Dash: _stateTimer = dashDuration; break;
                case State.Recover: _stateTimer = recoverDuration; break;
            }
        }
    }
}
