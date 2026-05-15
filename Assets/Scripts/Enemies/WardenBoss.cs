using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Enemies
{
    /// Mini-boss that periodically slams the ground, spawning an EnemyShockwave at its feet.
    /// Coexists with EnemyChaseAI for between-slam movement: this component disables/re-enables
    /// the chase AI during the windup/slam so the Warden stands still while telegraphing.
    [RequireComponent(typeof(EnemyBase))]
    public class WardenBoss : MonoBehaviour
    {
        [Header("Slam Timing")]
        [Tooltip("Random interval (seconds) between slams.")]
        [SerializeField] private Vector2 slamInterval = new Vector2(4f, 6f);
        [Tooltip("Telegraph duration before the shockwave spawns.")]
        [SerializeField] private float windupDuration = 0.6f;
        [Tooltip("Brief pause after the shockwave spawns before resuming movement.")]
        [SerializeField] private float recoverDuration = 0.4f;

        [Header("Shockwave")]
        [Tooltip("Prefab with an EnemyShockwave component to spawn at the Warden's feet.")]
        [SerializeField] private GameObject shockwavePrefab;
        [Tooltip("Falls back to EnemyData.aoeRadius when this is 0.")]
        [SerializeField] private float shockwaveRadius = 0f;
        [Tooltip("Damage of the shockwave. The shockwave prefab's own value is used when this is 0.")]
        [SerializeField] private float shockwaveDamage = 14f;

        [Header("Visuals (optional)")]
        [SerializeField] private SpriteRenderer flashRenderer;
        [SerializeField] private Color flashColor = new Color(0.6f, 0.8f, 1f, 1f);

        private EnemyBase _enemy;
        private EnemyChaseAI _chaseAi;
        private Rigidbody2D _rb;

        private enum State { Moving, Windup, Slam, Recover }
        private State _state;
        private float _stateTimer;
        private Color _baseColor = Color.white;
        private bool _baseColorCaptured;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _chaseAi = GetComponent<EnemyChaseAI>();
            _rb = GetComponent<Rigidbody2D>();
            if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (flashRenderer != null)
            {
                _baseColor = flashRenderer.color;
                _baseColorCaptured = true;
            }
            EnterMoving();
        }

        private void OnDisable()
        {
            if (_chaseAi != null) _chaseAi.enabled = true;
            if (_baseColorCaptured && flashRenderer != null) flashRenderer.color = _baseColor;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            _stateTimer -= Time.deltaTime;

            switch (_state)
            {
                case State.Moving:
                    if (_stateTimer <= 0f) EnterWindup();
                    break;
                case State.Windup:
                    TickWindup();
                    if (_stateTimer <= 0f) EnterSlam();
                    break;
                case State.Slam:
                    // Single-frame state, handled on entry.
                    EnterRecover();
                    break;
                case State.Recover:
                    if (_stateTimer <= 0f) EnterMoving();
                    break;
            }
        }

        private void TickWindup()
        {
            // Hold still while telegraphing.
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            if (flashRenderer != null)
            {
                float t = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.01f, windupDuration));
                float pulse = Mathf.PingPong(t * 8f, 1f);
                flashRenderer.color = Color.Lerp(_baseColor, flashColor, pulse);
            }
        }

        private void EnterMoving()
        {
            _state = State.Moving;
            _stateTimer = Random.Range(slamInterval.x, slamInterval.y);
            if (_chaseAi != null) _chaseAi.enabled = true;
            if (_baseColorCaptured && flashRenderer != null) flashRenderer.color = _baseColor;
        }

        private void EnterWindup()
        {
            _state = State.Windup;
            _stateTimer = windupDuration;
            if (_chaseAi != null) _chaseAi.enabled = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
        }

        private void EnterSlam()
        {
            _state = State.Slam;
            _stateTimer = 0f;
            SpawnShockwave();
            if (FxService.Instance != null)
            {
                FxService.Instance.HeavyShake();
                FxService.Instance.HitStop(0.05f);
            }
            if (_baseColorCaptured && flashRenderer != null) flashRenderer.color = _baseColor;
        }

        private void EnterRecover()
        {
            _state = State.Recover;
            _stateTimer = recoverDuration;
        }

        private void SpawnShockwave()
        {
            if (shockwavePrefab == null) return;
            Vector3 pos = transform.position;
            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(shockwavePrefab, pos, Quaternion.identity)
                : Instantiate(shockwavePrefab, pos, Quaternion.identity);
            if (instance == null) return;

            var wave = instance.GetComponent<EnemyShockwave>();
            if (wave != null)
            {
                float radius = shockwaveRadius > 0f
                    ? shockwaveRadius
                    : (_enemy != null && _enemy.Data != null ? _enemy.Data.aoeRadius : 0f);
                wave.Configure(radius, 0f, shockwaveDamage);
            }
        }
    }
}
