using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// Stalker archetype that ducks in and out of the fight. Cycles through:
    ///
    ///   Visible -> Vanishing (alpha fade out) -> Hidden (no chase, no contact damage)
    ///   -> Reappearing (alpha fade in at a fresh position near the player)
    ///   -> Attacking (brief windup with a red telegraph ring, then a lunge / AOE punch)
    ///   -> Visible (cooldown back to the loop)
    ///
    /// Total cycle ~5-7s, tunable. EnemyChaseAI is disabled while Hidden so the body does not
    /// drift through the world. We never modify EnemyBase / EnemyChaseAI — we just toggle their
    /// `enabled` flag, which is part of the public MonoBehaviour API.
    ///
    /// Visuals are decoupled: a TeleportFlash prefab plays a quick particle burst on vanish and
    /// reappear, and a TeleportTelegraph prefab draws the expanding red ring during the windup.
    /// Both are optional — if unassigned the state machine still runs cleanly.
    [RequireComponent(typeof(EnemyBase))]
    public class TeleporterEnemy : MonoBehaviour
    {
        private enum State { Visible, Vanishing, Hidden, Reappearing, Attacking, Cooldown }

        [Header("Timing")]
        [Tooltip("How long the enemy is visible and chasing normally between teleports.")]
        [SerializeField] private float visibleDuration = 2.0f;
        [Tooltip("Alpha-fade-out duration for vanishing.")]
        [SerializeField] private float vanishDuration = 0.35f;
        [Tooltip("Time spent fully invisible before reappearing near the player.")]
        [SerializeField] private float hiddenDuration = 0.5f;
        [Tooltip("Alpha-fade-in duration for reappearing.")]
        [SerializeField] private float reappearDuration = 0.35f;
        [Tooltip("Telegraphed windup duration BEFORE the attack lands — the red ring expands across this window.")]
        [SerializeField] private float attackWindup = 0.6f;
        [Tooltip("Cooldown after the attack lands and before the cycle restarts.")]
        [SerializeField] private float postAttackCooldown = 0.7f;

        [Header("Teleport Geometry")]
        [Tooltip("Inner radius of the reappearance ring around the player.")]
        [SerializeField] private float teleportMinDistance = 4f;
        [Tooltip("Outer radius of the reappearance ring around the player.")]
        [SerializeField] private float teleportMaxDistance = 6f;

        [Header("Attack")]
        [Tooltip("Radius of the AOE attack landed at the end of the windup.")]
        [SerializeField] private float attackRadius = 1.6f;
        [Tooltip("Damage dealt by the AOE attack.")]
        [SerializeField] private float attackDamage = 12f;
        [Tooltip("Lunge speed during the attack (the body briefly dashes toward the player). 0 disables the lunge.")]
        [SerializeField] private float lungeSpeed = 7f;
        [Tooltip("Duration of the lunge in seconds.")]
        [SerializeField] private float lungeDuration = 0.18f;

        [Header("Visual Refs (optional — auto-resolved)")]
        [Tooltip("Sprite renderer faded during vanish/reappear. Defaults to the first SpriteRenderer in children.")]
        [SerializeField] private SpriteRenderer bodyRenderer;
        [Tooltip("Optional prefab spawned at the vanish AND reappear positions for a quick flash.")]
        [SerializeField] private GameObject teleportFlashPrefab;
        [Tooltip("Optional prefab spawned at the reappear position for the red expanding warning ring.")]
        [SerializeField] private GameObject teleportTelegraphPrefab;

        [Header("Hooks")]
        [Tooltip("Disabled while Hidden so the body does not drift. Auto-resolved from sibling.")]
        [SerializeField] private EnemyChaseAI chaseAI;
        [Tooltip("Disabled while Hidden so no contact damage triggers from an invisible enemy. Auto-resolved.")]
        [SerializeField] private Collider2D bodyCollider;
        [Tooltip("Zeroed while Hidden / windup. Auto-resolved.")]
        [SerializeField] private Rigidbody2D body;

        private EnemyBase _enemy;
        private State _state;
        private float _stateTimer;
        private float _lungeTimer;
        private Vector2 _lungeDirection;
        private Color _baseColor = Color.white;
        private bool _baseColorCaptured;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            if (chaseAI == null) chaseAI = GetComponent<EnemyChaseAI>();
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (bodyRenderer != null)
            {
                _baseColor = bodyRenderer.color;
                _baseColorCaptured = true;
            }
        }

        private void OnEnable()
        {
            SetAlpha(1f);
            SetChaseEnabled(true);
            SetColliderEnabled(true);
            EnterState(State.Visible);
        }

        private void OnDisable()
        {
            // Restore pooled-state invariants so the next consumer of this pooled object is sane.
            SetAlpha(1f);
            SetChaseEnabled(true);
            SetColliderEnabled(true);
            if (_baseColorCaptured && bodyRenderer != null) bodyRenderer.color = _baseColor;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            _stateTimer -= Time.deltaTime;

            switch (_state)
            {
                case State.Visible: TickVisible(); break;
                case State.Vanishing: TickVanishing(); break;
                case State.Hidden: TickHidden(); break;
                case State.Reappearing: TickReappearing(); break;
                case State.Attacking: TickAttacking(); break;
                case State.Cooldown: TickCooldown(); break;
            }
        }

        private void TickVisible()
        {
            if (_stateTimer <= 0f) EnterState(State.Vanishing);
        }

        private void TickVanishing()
        {
            float t = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.0001f, vanishDuration));
            SetAlpha(1f - t);
            if (_stateTimer <= 0f)
            {
                SpawnFlash(transform.position);
                EnterState(State.Hidden);
            }
        }

        private void TickHidden()
        {
            // While hidden, hold position and don't damage anything.
            if (body != null) body.linearVelocity = Vector2.zero;
            if (_stateTimer <= 0f)
            {
                TeleportNearPlayer();
                SpawnFlash(transform.position);
                EnterState(State.Reappearing);
            }
        }

        private void TickReappearing()
        {
            float t = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.0001f, reappearDuration));
            SetAlpha(t);
            if (body != null) body.linearVelocity = Vector2.zero;
            if (_stateTimer <= 0f)
            {
                SpawnTelegraph(transform.position);
                EnterState(State.Attacking);
            }
        }

        private void TickAttacking()
        {
            // First half = windup (held still). Second slice = lunge.
            if (_lungeTimer > 0f)
            {
                _lungeTimer -= Time.deltaTime;
                if (body != null) body.linearVelocity = _lungeDirection * lungeSpeed;
                if (_lungeTimer <= 0f && body != null) body.linearVelocity = Vector2.zero;
            }
            else if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (_stateTimer <= 0f)
            {
                LandAttack();
                EnterState(State.Cooldown);
            }
        }

        private void TickCooldown()
        {
            if (_stateTimer <= 0f) EnterState(State.Visible);
        }

        private void EnterState(State next)
        {
            _state = next;
            switch (next)
            {
                case State.Visible:
                    _stateTimer = visibleDuration;
                    SetAlpha(1f);
                    SetChaseEnabled(true);
                    SetColliderEnabled(true);
                    break;

                case State.Vanishing:
                    _stateTimer = vanishDuration;
                    SetChaseEnabled(false);
                    SetColliderEnabled(false);
                    break;

                case State.Hidden:
                    _stateTimer = hiddenDuration;
                    SetAlpha(0f);
                    SetChaseEnabled(false);
                    SetColliderEnabled(false);
                    break;

                case State.Reappearing:
                    _stateTimer = reappearDuration;
                    SetChaseEnabled(false);
                    // Keep the collider off during reappear so the player can't be cheap-shotted
                    // by the body before the telegraphed attack lands.
                    SetColliderEnabled(false);
                    break;

                case State.Attacking:
                    _stateTimer = attackWindup;
                    // Lunge kicks in late in the windup — start it at half-way so it overlaps
                    // the final flash of the telegraph ring.
                    _lungeTimer = Mathf.Min(lungeDuration, attackWindup * 0.5f);
                    var p = PlayerController.Instance;
                    _lungeDirection = p != null
                        ? ((Vector2)p.transform.position - (Vector2)transform.position).normalized
                        : Vector2.right;
                    SetColliderEnabled(true);
                    SetChaseEnabled(false);
                    break;

                case State.Cooldown:
                    _stateTimer = postAttackCooldown;
                    SetChaseEnabled(true);
                    SetColliderEnabled(true);
                    break;
            }
        }

        private void TeleportNearPlayer()
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            float min = Mathf.Max(0f, teleportMinDistance);
            float max = Mathf.Max(min + 0.01f, teleportMaxDistance);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(min, max);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            transform.position = (Vector2)player.transform.position + offset;
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private void LandAttack()
        {
            if (attackRadius <= 0f || attackDamage <= 0f) return;
            var player = PlayerController.Instance;
            if (player == null) return;
            float dSq = ((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (dSq > attackRadius * attackRadius) return;

            if (player.TryGetComponent<PlayerHealth>(out var hp))
            {
                hp.ApplyDamage(attackDamage);
            }

            if (FxService.Instance != null)
            {
                FxService.Instance.PlayerHit(player.transform.position);
                FxService.Instance.LightShake();
            }
        }

        private void SetAlpha(float a)
        {
            if (bodyRenderer == null) return;
            var c = bodyRenderer.color;
            c.a = Mathf.Clamp01(a);
            bodyRenderer.color = c;
        }

        private void SetChaseEnabled(bool on)
        {
            if (chaseAI != null && chaseAI.enabled != on) chaseAI.enabled = on;
        }

        private void SetColliderEnabled(bool on)
        {
            if (bodyCollider != null && bodyCollider.enabled != on) bodyCollider.enabled = on;
        }

        private void SpawnFlash(Vector3 position)
        {
            if (teleportFlashPrefab == null) return;
            var go = PoolService.Instance != null
                ? PoolService.Instance.Get(teleportFlashPrefab, position, Quaternion.identity)
                : Instantiate(teleportFlashPrefab, position, Quaternion.identity);
            if (go != null && go.TryGetComponent<TeleportFlash>(out var flash))
            {
                flash.Play();
            }
        }

        private void SpawnTelegraph(Vector3 position)
        {
            if (teleportTelegraphPrefab == null) return;
            var go = PoolService.Instance != null
                ? PoolService.Instance.Get(teleportTelegraphPrefab, position, Quaternion.identity)
                : Instantiate(teleportTelegraphPrefab, position, Quaternion.identity);
            if (go != null && go.TryGetComponent<TeleportTelegraph>(out var tele))
            {
                tele.Configure(attackRadius, attackWindup);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, attackRadius);
            var p = LoneFighter.Player.PlayerController.Instance;
            if (p != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawWireSphere(p.transform.position, teleportMinDistance);
                Gizmos.DrawWireSphere(p.transform.position, teleportMaxDistance);
            }
        }
#endif
    }
}
