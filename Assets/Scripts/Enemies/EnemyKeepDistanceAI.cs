using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Enemies
{
    /// Rigidbody2D mover for ranged archetypes (Spitter): chases when the player is far,
    /// retreats when too close, and strafes when at the preferred range band.
    /// Pairs naturally with RangedEnemy. Replaces EnemyChaseAI for this archetype.
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyKeepDistanceAI : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;

        [Header("Distance Bands")]
        [Tooltip("Below this distance the enemy will retreat from the player.")]
        [SerializeField] private float minDistance = 4f;
        [Tooltip("Sweet spot. While inside [minDistance, maxDistance] the enemy strafes.")]
        [SerializeField] private float preferredDistance = 5.5f;
        [Tooltip("Above this distance the enemy will chase the player.")]
        [SerializeField] private float maxDistance = 7f;

        [Header("Strafe")]
        [Tooltip("Speed multiplier while strafing inside the preferred band.")]
        [Range(0f, 1.5f)] [SerializeField] private float strafeSpeedMultiplier = 0.6f;
        [Tooltip("Seconds before the strafe direction can flip again.")]
        [SerializeField] private float strafeFlipInterval = 1.5f;

        private Rigidbody2D _rb;
        private Transform _target;
        private int _strafeSign = 1;
        private float _nextStrafeFlipTime;

        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
        }

        private void OnEnable()
        {
            _target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            _strafeSign = Random.value < 0.5f ? -1 : 1;
            _nextStrafeFlipTime = Time.time + strafeFlipInterval;

            // Auto-pull move speed from EnemyData if available, so a single SO drives every AI.
            var enemy = GetComponent<EnemyBase>();
            if (enemy != null && enemy.Data != null) moveSpeed = enemy.Data.moveSpeed;
        }

        public void Configure(float speed)
        {
            moveSpeed = speed;
        }

        public void ConfigureDistances(float min, float preferred, float max)
        {
            minDistance = Mathf.Max(0f, min);
            preferredDistance = Mathf.Max(minDistance, preferred);
            maxDistance = Mathf.Max(preferredDistance, max);
        }

        private void FixedUpdate()
        {
            if (_target == null)
            {
                if (PlayerController.Instance != null) _target = PlayerController.Instance.transform;
                else { _rb.linearVelocity = Vector2.zero; return; }
            }

            Vector2 toPlayer = (Vector2)_target.position - (Vector2)transform.position;
            float distance = toPlayer.magnitude;
            if (distance < 0.0001f)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 dir = toPlayer / distance;

            Vector2 velocity;
            if (distance > maxDistance)
            {
                // Too far: chase.
                velocity = dir * moveSpeed;
            }
            else if (distance < minDistance)
            {
                // Too close: retreat.
                velocity = -dir * moveSpeed;
            }
            else
            {
                // In sweet spot: strafe perpendicular to player and edge toward preferred distance.
                if (Time.time >= _nextStrafeFlipTime)
                {
                    _strafeSign = -_strafeSign;
                    _nextStrafeFlipTime = Time.time + strafeFlipInterval;
                }
                Vector2 perpendicular = new Vector2(-dir.y, dir.x) * _strafeSign;
                float pull = Mathf.Clamp((distance - preferredDistance) / Mathf.Max(0.01f, maxDistance - minDistance), -1f, 1f);
                Vector2 desired = (perpendicular + dir * pull).normalized;
                velocity = desired * moveSpeed * strafeSpeedMultiplier;
            }

            _rb.linearVelocity = velocity;
        }
    }
}
