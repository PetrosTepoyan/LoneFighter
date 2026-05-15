using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Advanced
{
    /// Support archetype. Stays at range (pair with EnemyKeepDistanceAI on the prefab — disable
    /// EnemyChaseAI in the editor) and periodically emits a healing pulse that restores HP to all
    /// allied enemies within healRadius. High-priority kill target: a HealerPriorityIndicator on
    /// the same prefab makes them visually obvious to the player.
    ///
    /// Healing path note:
    /// EnemyHealth (in the canonical project) does NOT expose a public Heal() method and its
    /// Current setter is private. To stay within the "do not modify existing files" rule we use
    /// the public ApplyDamage(float) entrypoint with a NEGATIVE amount, which adds HP. The
    /// canonical EnemyHealth implementation:
    ///
    ///     public void ApplyDamage(float amount)
    ///     {
    ///         if (_dead) return;
    ///         Current -= amount;
    ///         if (Current <= 0f) { _dead = true; OnDied?.Invoke(this); _enemy.HandleDeath(); }
    ///     }
    ///
    /// so ApplyDamage(-heal) effectively increases Current. Because that path does not clamp to
    /// Max, we pre-clamp the heal amount to (Max - Current) before applying. If EnemyHealth later
    /// grows a real public Heal(float) method, swap the call in ApplyHeal() — the rest of this
    /// component remains identical.
    [RequireComponent(typeof(EnemyBase))]
    public class HealerEnemy : MonoBehaviour
    {
        [Header("Healing Pulse")]
        [Tooltip("Seconds between healing pulses.")]
        [SerializeField] private float pulseInterval = 2f;
        [Tooltip("Radius (world units) of the healing pulse. Enemies inside this circle around the healer are healed.")]
        [SerializeField] private float healRadius = 3.5f;
        [Tooltip("HP restored to each ally per pulse. Pre-clamped to (Max - Current) so it never overheals.")]
        [SerializeField] private float healAmountPerPulse = 6f;
        [Tooltip("If true, the healer also heals itself in each pulse.")]
        [SerializeField] private bool healsSelf = true;

        [Header("Pulse Telegraph (optional, sibling component)")]
        [Tooltip("Spawned at the healer's position when a pulse fires; gives a visible green ring.")]
        [SerializeField] private GameObject pulseTelegraphPrefab;

        [Header("Per-Target Aura (optional)")]
        [Tooltip("Spawned at each healed enemy's position; gives a per-target tick visual.")]
        [SerializeField] private GameObject healAuraPrefab;

        private EnemyBase _enemy;
        private float _nextPulseTime;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        private void OnEnable()
        {
            // Stagger first pulse slightly so multiple healers don't fire in lockstep.
            _nextPulseTime = Time.time + Random.Range(0.1f, pulseInterval);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (Time.time < _nextPulseTime) return;
            _nextPulseTime = Time.time + pulseInterval;
            EmitPulse();
        }

        private void EmitPulse()
        {
            Vector2 origin = transform.position;
            float r2 = healRadius * healRadius;
            int healedCount = 0;

            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var other = list[i];
                if (other == null || !other.isActiveAndEnabled) continue;
                if (!healsSelf && other == _enemy) continue;

                Vector2 p = other.transform.position;
                if ((p - origin).sqrMagnitude > r2) continue;

                if (other.TryGetComponent<EnemyHealth>(out var hp))
                {
                    if (ApplyHeal(hp, healAmountPerPulse))
                    {
                        SpawnAura(other.transform);
                        healedCount++;
                    }
                }
            }

            if (healedCount > 0 || pulseTelegraphPrefab != null)
            {
                SpawnPulseTelegraph(origin);
            }
        }

        /// Restore HP up to Max. Returns true if any healing was applied.
        /// See file header for why this uses ApplyDamage with a negative amount.
        private bool ApplyHeal(EnemyHealth hp, float amount)
        {
            if (hp == null || amount <= 0f) return false;
            float room = hp.Max - hp.Current;
            if (room <= 0.01f) return false;
            float actual = Mathf.Min(room, amount);
            hp.ApplyDamage(-actual);
            return true;
        }

        private void SpawnPulseTelegraph(Vector2 position)
        {
            if (pulseTelegraphPrefab == null) return;
            var go = PoolService.Instance != null
                ? PoolService.Instance.Get(pulseTelegraphPrefab, position, Quaternion.identity)
                : Instantiate(pulseTelegraphPrefab, position, Quaternion.identity);
            if (go != null && go.TryGetComponent<HealerAura>(out var aura))
            {
                aura.Configure(healRadius);
            }
        }

        private void SpawnAura(Transform target)
        {
            if (healAuraPrefab == null || target == null) return;
            var go = PoolService.Instance != null
                ? PoolService.Instance.Get(healAuraPrefab, target.position, Quaternion.identity)
                : Instantiate(healAuraPrefab, target.position, Quaternion.identity);
            if (go != null && go.TryGetComponent<HealerAura>(out var aura))
            {
                aura.Configure(0.6f);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.45f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, healRadius);
        }
#endif
    }
}
