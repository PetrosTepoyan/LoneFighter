using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    /// Suicide AOE enemy. When the player comes within armRange, the Bomber starts a fuse
    /// (visualized by an accelerating sprite flash). When the fuse ends OR the bomber collides
    /// directly with the player, it explodes, damaging anyone inside aoeRadius with linear
    /// distance falloff (full damage at center -> 0 at the radius edge), then returns to the pool.
    [RequireComponent(typeof(EnemyBase))]
    public class BomberBehavior : MonoBehaviour
    {
        [Header("Arming")]
        [Tooltip("Distance from the player at which the fuse starts.")]
        [SerializeField] private float armRange = 1.4f;
        [Tooltip("Seconds from arm to detonation.")]
        [SerializeField] private float fuseDuration = 1.0f;

        [Header("Explosion")]
        [Tooltip("Maximum damage dealt at the center of the blast. Falls off linearly to 0 at the edge.")]
        [SerializeField] private float maxDamage = 25f;
        [Tooltip("Radius of the explosion. Overrides EnemyData.aoeRadius when that field is 0.")]
        [SerializeField] private float fallbackRadius = 2.5f;

        [Header("Visuals (optional)")]
        [Tooltip("Renderer used for the fuse flash. If null, the first SpriteRenderer in children is used.")]
        [SerializeField] private SpriteRenderer flashRenderer;
        [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.2f, 1f);

        private EnemyBase _enemy;
        private bool _armed;
        private float _fuseRemaining;
        private Color _baseColor = Color.white;
        private bool _baseColorCaptured;
        private bool _detonated;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _armed = false;
            _detonated = false;
            _fuseRemaining = 0f;
            if (flashRenderer != null)
            {
                _baseColor = flashRenderer.color;
                _baseColorCaptured = true;
            }
        }

        private void OnDisable()
        {
            if (_baseColorCaptured && flashRenderer != null) flashRenderer.color = _baseColor;
        }

        private float Radius
        {
            get
            {
                if (_enemy != null && _enemy.Data != null && _enemy.Data.aoeRadius > 0f) return _enemy.Data.aoeRadius;
                return fallbackRadius;
            }
        }

        private void Update()
        {
            if (_detonated) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (PlayerController.Instance == null) return;

            if (!_armed)
            {
                float sq = ((Vector2)PlayerController.Instance.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sq <= armRange * armRange) Arm();
                return;
            }

            _fuseRemaining -= Time.deltaTime;
            if (flashRenderer != null && fuseDuration > 0f)
            {
                // Flash frequency ramps up as the fuse runs out.
                float t = 1f - Mathf.Clamp01(_fuseRemaining / fuseDuration);
                float frequency = Mathf.Lerp(3f, 20f, t);
                float pulse = Mathf.PingPong(Time.time * frequency, 1f);
                flashRenderer.color = Color.Lerp(_baseColor, flashColor, pulse);
            }

            if (_fuseRemaining <= 0f) Explode();
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryDirectDetonate(collision.collider);
        private void OnTriggerEnter2D(Collider2D other) => TryDirectDetonate(other);

        private void TryDirectDetonate(Collider2D other)
        {
            if (_detonated) return;
            if (other.GetComponentInParent<PlayerHealth>() == null) return;
            // Direct contact short-circuits any remaining fuse.
            Explode();
        }

        private void Arm()
        {
            _armed = true;
            _fuseRemaining = fuseDuration;
        }

        public void Explode()
        {
            if (_detonated) return;
            _detonated = true;

            Vector3 pos = transform.position;
            float radius = Mathf.Max(0.01f, Radius);

            // Hit the player with distance falloff.
            if (PlayerController.Instance != null)
            {
                Vector2 toPlayer = (Vector2)PlayerController.Instance.transform.position - (Vector2)pos;
                float dist = toPlayer.magnitude;
                if (dist <= radius)
                {
                    var health = PlayerController.Instance.GetComponent<PlayerHealth>();
                    if (health != null)
                    {
                        float falloff = 1f - (dist / radius);
                        health.ApplyDamage(maxDamage * falloff);
                    }
                }
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.EnemyExplosion(pos);
                FxService.Instance.HeavyShake();
                FxService.Instance.HitStop(0.04f);
            }

            // Restore color before pooling.
            if (_baseColorCaptured && flashRenderer != null) flashRenderer.color = _baseColor;

            // Use HandleDeath so XP / kill registration / pool release run through the canonical path.
            _enemy.HandleDeath();
        }
    }
}
