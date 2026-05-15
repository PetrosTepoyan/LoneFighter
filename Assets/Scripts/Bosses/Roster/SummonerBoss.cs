using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Bosses.Roster
{
    /// SummonerBoss — does not directly attack the player. It periodically conjures small
    /// minion enemies (Grunt by default) at random offsets around itself and slowly flees
    /// to maintain distance, forcing the player to push through the swarm to reach it.
    ///
    /// Phases (driven by current HP fraction of EnemyHealth.Max):
    ///   Phase 1 (HP > 50%):  summons every [4, 6] s, 3-5 minions per cast.
    ///   Phase 2 (HP <= 50%): summons every [2, 3] s, 3-5 minions per cast.
    ///   Phase 3 (HP <= 20%): additionally emits a BulletPattern.RadialBurst between casts
    ///                        if a sibling PatternEmitter component is attached. We dispatch
    ///                        via Unity's SendMessage so this code has zero compile-time
    ///                        dependency on the sibling Patterns track.
    ///
    /// Composition:
    /// - Add this MonoBehaviour to a prefab that already has EnemyBase + EnemyHealth.
    /// - At runtime we disable any sibling EnemyChaseAI so we can drive movement ourselves
    ///   (slow flee from the player).
    /// - Minion prefab is supplied via the inspector. It must be a fully configured Grunt-style
    ///   prefab with an EnemyBase that already has its EnemyData assigned (EnemyBase.OnEnable
    ///   initializes from data when present).
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class SummonerBoss : MonoBehaviour
    {
        [Header("Minion Spawning")]
        [Tooltip("Prefab spawned during each summon cast. Default boss authoring is a Grunt prefab. Must have EnemyBase with EnemyData assigned.")]
        [SerializeField] private GameObject minionPrefab;
        [Tooltip("Minimum and maximum number of minions per cast.")]
        [SerializeField] private Vector2Int minionsPerCast = new Vector2Int(3, 5);
        [Tooltip("Radius around the summoner at which minions appear.")]
        [SerializeField] private float summonRadius = 1.8f;
        [Tooltip("Tiny random jitter added to each minion's spawn radius.")]
        [SerializeField] private float summonRadiusJitter = 0.4f;

        [Header("Summon Cadence")]
        [Tooltip("Random interval (seconds) between summon casts in Phase 1.")]
        [SerializeField] private Vector2 phase1Interval = new Vector2(4f, 6f);
        [Tooltip("Random interval (seconds) between summon casts in Phase 2 (<=50% HP).")]
        [SerializeField] private Vector2 phase2Interval = new Vector2(2f, 3f);
        [Tooltip("Windup duration before each summon resolves (telegraph window).")]
        [SerializeField] private float windupDuration = 1f;

        [Header("Fleeing")]
        [Tooltip("Slow flee speed when the player is closer than fleeStartRange.")]
        [SerializeField] private float fleeSpeed = 1.2f;
        [Tooltip("Distance at which the summoner starts retreating from the player.")]
        [SerializeField] private float fleeStartRange = 6f;
        [Tooltip("Above this distance the summoner stays put (does not chase).")]
        [SerializeField] private float idleDistance = 8f;

        [Header("Phase 3 Pattern")]
        [Tooltip("HP fraction (0..1) at which the boss enters Phase 3 and starts firing radial bursts.")]
        [Range(0.05f, 0.5f)] [SerializeField] private float phase3HpThreshold = 0.2f;
        [Tooltip("Cooldown between radial bursts in Phase 3 (independent of summon cadence).")]
        [SerializeField] private float phase3BurstCooldown = 2.5f;

        [Header("Telegraph")]
        [Tooltip("Optional prefab containing a SummonerSpawnRitual to instance on each summon. If null, no ritual visual is shown.")]
        [SerializeField] private GameObject spawnRitualPrefab;
        [Tooltip("Optional child SummonerSpawnRitual on the boss itself. Used in place of the prefab when set.")]
        [SerializeField] private SummonerSpawnRitual ownedRitual;

        // Cached components.
        private EnemyBase _enemy;
        private EnemyHealth _health;
        private Rigidbody2D _rb;
        private EnemyChaseAI _chaseAi;

        // Patterns track (sibling Phases agent). Optional - referenced as a loose
        // MonoBehaviour and invoked via Unity's SendMessage so we never take a hard type
        // dependency on the sibling track.
        private MonoBehaviour _patternEmitter;

        // Cadence state.
        private float _summonTimer;
        private float _windupTimer;
        private float _burstTimer;
        private bool _winding;
        private SummonerSpawnRitual _activeRitual;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
            _rb = GetComponent<Rigidbody2D>();
            _chaseAi = GetComponent<EnemyChaseAI>();
        }

        private void OnEnable()
        {
            // Take over movement: disable chase, drive a slow flee in FixedUpdate.
            if (_chaseAi != null) _chaseAi.enabled = false;

            _summonTimer = NextSummonInterval();
            _windupTimer = 0f;
            _burstTimer = phase3BurstCooldown;
            _winding = false;

            // Resolve optional PatternEmitter sibling exactly once. We do NOT use reflection
            // at runtime - we only resolve the method handle here (cheap and one-shot).
            ResolvePatternEmitter();
        }

        private void OnDisable()
        {
            if (_chaseAi != null) _chaseAi.enabled = true;
            if (_activeRitual != null)
            {
                _activeRitual.Stop();
                _activeRitual = null;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            float hpFrac = HpFraction;

            // Tick windup -> resolve.
            if (_winding)
            {
                _windupTimer -= Time.deltaTime;
                if (_windupTimer <= 0f) ResolveSummon();
                return;
            }

            _summonTimer -= Time.deltaTime;
            if (_summonTimer <= 0f) BeginSummonWindup();

            // Phase 3: radial bursts (only when not currently winding up a summon).
            if (hpFrac <= phase3HpThreshold)
            {
                _burstTimer -= Time.deltaTime;
                if (_burstTimer <= 0f)
                {
                    TriggerRadialBurst();
                    _burstTimer = phase3BurstCooldown;
                }
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null) return;
            if (PlayerController.Instance == null) { _rb.linearVelocity = Vector2.zero; return; }

            // While winding up the summon, freeze in place so the telegraph reads cleanly.
            if (_winding) { _rb.linearVelocity = Vector2.zero; return; }

            Vector2 toPlayer = (Vector2)PlayerController.Instance.transform.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;
            if (dist < 0.0001f) { _rb.linearVelocity = Vector2.zero; return; }

            // Slow flee: only retreat when too close. Past idleDistance, stand still.
            if (dist < fleeStartRange)
            {
                Vector2 away = -(toPlayer / dist);
                _rb.linearVelocity = away * fleeSpeed;
            }
            else if (dist > idleDistance)
            {
                // Too far -> drift slightly toward the player to stay engaged but never catch up.
                _rb.linearVelocity = (toPlayer / dist) * (fleeSpeed * 0.4f);
            }
            else
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }

        // ---- summon flow ------------------------------------------------------

        private void BeginSummonWindup()
        {
            _winding = true;
            _windupTimer = windupDuration;
            ShowRitual(transform.position, windupDuration);
        }

        private void ResolveSummon()
        {
            _winding = false;
            HideRitual();

            int count = Random.Range(minionsPerCast.x, minionsPerCast.y + 1);
            for (int i = 0; i < count; i++) SpawnOneMinion();

            // Camera flair.
            if (FxService.Instance != null) FxService.Instance.LightShake();

            // Schedule the next summon based on the active phase.
            _summonTimer = NextSummonInterval();
        }

        private void SpawnOneMinion()
        {
            if (minionPrefab == null) return;
            float angle = Random.value * Mathf.PI * 2f;
            float r = summonRadius + Random.Range(0f, summonRadiusJitter);
            Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(minionPrefab, pos, Quaternion.identity)
                : Instantiate(minionPrefab, pos, Quaternion.identity);
            if (instance == null) return;

            // If the minion prefab carries no pre-baked EnemyData (e.g. the SO wasn't
            // assigned at edit-time), we leave it alone - EnemyBase.OnEnable handles its own
            // initialization from its serialized data field.
            var minion = instance.GetComponent<EnemyBase>();
            if (minion != null && minion.Data == null)
            {
                // Best-effort: try to pull the minion's own SO via the prefab to seed it.
                var prefabBase = minionPrefab.GetComponent<EnemyBase>();
                if (prefabBase != null && prefabBase.Data != null)
                {
                    minion.Configure(prefabBase.Data, null);
                }
            }
        }

        // ---- phase helpers ----------------------------------------------------

        private float HpFraction
        {
            get
            {
                if (_health == null || _health.Max <= 0f) return 1f;
                return Mathf.Clamp01(_health.Current / _health.Max);
            }
        }

        private float NextSummonInterval()
        {
            Vector2 band = HpFraction <= 0.5f ? phase2Interval : phase1Interval;
            return Random.Range(band.x, band.y);
        }

        // ---- pattern integration (sibling Phases agent) ----------------------

        private void ResolvePatternEmitter()
        {
            _patternEmitter = null;

            // Walk every MonoBehaviour on this GO and pick the first one named "PatternEmitter".
            // We avoid `GetType()` reflection on method handles by using Unity's SendMessage
            // to invoke the optional API (no MethodInfo cache, no Invoke).
            var components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;
                if (c.GetType().Name != "PatternEmitter") continue;
                _patternEmitter = c;
                break;
            }
        }

        private void TriggerRadialBurst()
        {
            if (_patternEmitter == null) return;
            // SendMessage hits any of: RadialBurst() OR Emit(string) OR Fire(string).
            // SendMessageOptions.DontRequireReceiver keeps this safe if the sibling shape changes.
            _patternEmitter.SendMessage("RadialBurst", SendMessageOptions.DontRequireReceiver);
            _patternEmitter.SendMessage("Emit", "RadialBurst", SendMessageOptions.DontRequireReceiver);
        }

        // ---- ritual visuals ---------------------------------------------------

        private void ShowRitual(Vector3 pos, float duration)
        {
            if (ownedRitual != null)
            {
                ownedRitual.Play(pos, duration);
                _activeRitual = ownedRitual;
                return;
            }
            if (spawnRitualPrefab == null) return;

            GameObject go = PoolService.Instance != null
                ? PoolService.Instance.Get(spawnRitualPrefab, pos, Quaternion.identity)
                : Instantiate(spawnRitualPrefab, pos, Quaternion.identity);
            if (go == null) return;
            _activeRitual = go.GetComponent<SummonerSpawnRitual>();
            if (_activeRitual != null) _activeRitual.Play(pos, duration);
        }

        private void HideRitual()
        {
            if (_activeRitual == null) return;
            _activeRitual.Stop();
            // If the ritual was a pooled instance (not our own child), return it.
            if (_activeRitual != ownedRitual && PoolService.Instance != null)
            {
                PoolService.Instance.Release(_activeRitual.gameObject);
            }
            _activeRitual = null;
        }
    }
}
