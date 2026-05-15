using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.UI.Settings;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// Singleton MonoBehaviour that subscribes to every meaningful gameplay event in the run and
    /// dispatches a matching <see cref="HapticPattern"/> through the existing
    /// <see cref="HapticsService"/>.
    ///
    /// <para><b>Why a separate director?</b></para>
    /// The original <see cref="HapticsBridge"/> covers single-pulse Light/Medium/Heavy on three
    /// events. This file augments (does NOT replace) that behavior with multi-step patterns
    /// for: level-ups, terminal game states, big damage hits, boss arrivals, kill milestones, and
    /// combo thresholds. Rate-limiting prevents haptic spam when many of these fire close
    /// together.
    ///
    /// <para><b>Composability</b></para>
    /// You can run both <see cref="HapticsBridge"/> and <see cref="HapticDirector"/> at the same
    /// time: bridge gives you single-thump damage feedback, director adds the dramatic
    /// multi-pulse flourishes on top. If that's too much, leave only one active in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class HapticDirector : MonoBehaviour
    {
        public static HapticDirector Instance { get; private set; }

        // ---- Inspector knobs -------------------------------------------------------------

        [Header("Channels")]
        [SerializeField] private bool enableLevelUp = true;
        [SerializeField] private bool enableTerminalStates = true;
        [SerializeField] private bool enableBigDamage = true;
        [SerializeField] private bool enableBossArrival = true;
        [SerializeField] private bool enableKillMilestones = true;
        [SerializeField] private bool enableComboThreshold = true;

        [Header("Big Damage Threshold")]
        [Tooltip("Fraction of MAX health lost in a single hit that counts as 'big damage'.")]
        [SerializeField, Range(0.05f, 1f)] private float bigDamageMaxFraction = 0.2f;

        [Header("Kill Milestones")]
        [Tooltip("Fire TriplePulse every Nth kill.")]
        [SerializeField, Min(1)] private int killMilestoneN = 10;

        [Header("Combo")]
        [Tooltip("Combo count at which RisingRumble plays.")]
        [SerializeField, Min(2)] private int comboThreshold = 10;
        [Tooltip("Minimum seconds between consecutive combo rumbles to avoid spam.")]
        [SerializeField, Min(0f)] private float comboCooldownSeconds = 2f;

        [Header("Boss Detection")]
        [Tooltip("How often (seconds) we sweep EnemyRegistry for boss-named enemies.")]
        [SerializeField, Min(0.1f)] private float bossPollInterval = 0.5f;
        [Tooltip("Case-insensitive substrings that mark an EnemyData.displayName as a boss.")]
        [SerializeField] private string[] bossNameTokens = { "boss", "warden", "elite" };

        [Header("Spam Limit")]
        [Tooltip("Minimum seconds between any two pattern starts. Single pulses bypass this.")]
        [SerializeField, Min(0f)] private float globalCooldownSeconds = 0.25f;

        [Header("Intensity Curve")]
        [Tooltip("Use the optional IntensityResolver-driven curve to scale pattern strength.")]
        [SerializeField] private bool useIntensityCurve = true;

        // ---- Internal state ---------------------------------------------------------------

        private PlayerHealth _health;
        private PlayerLevel _level;
        private float _lastKnownHealth = -1f;
        private int _lastObservedKills;
        private int _lastMilestoneBucket;
        private float _lastPatternTime = -10f;
        private float _lastComboPatternTime = -10f;
        private float _nextBossPoll;
        private readonly HashSet<int> _knownBossInstanceIds = new();
        private GameState _lastBroadcastState = GameState.Boot;
        private Coroutine _runningPattern;
        private HapticIntensityCurve _curve;

        // ComboCounter is accessed via reflection-friendly direct ref. It lives in the same
        // namespace, so a hard reference is fine.
        private ComboCounter _combo;
        private int _lastComboValue;

        // ---- Lifecycle --------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _curve = new HapticIntensityCurve();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            SubscribeAll();
        }

        private void OnEnable()
        {
            // Re-subscribe if we were toggled off/on at runtime.
            if (Instance == this && _health == null) SubscribeAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void Update()
        {
            // Boss polling — cheap, only fires every bossPollInterval seconds.
            if (enableBossArrival && Time.unscaledTime >= _nextBossPoll)
            {
                _nextBossPoll = Time.unscaledTime + bossPollInterval;
                PollForBosses();
            }
        }

        // ---- Subscription wiring ---------------------------------------------------------

        private void SubscribeAll()
        {
            // Player components
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_health != null)
            {
                _lastKnownHealth = _health.Current;
                _health.OnHealthChanged += HandleHealthChanged;
            }
            if (_level != null)
            {
                _level.OnLevelUp += HandleLevelUp;
            }

            // GameManager — state changes + kill milestones
            if (GameManager.Instance != null)
            {
                _lastObservedKills = GameManager.Instance.Kills;
                _lastMilestoneBucket = _lastObservedKills / Mathf.Max(1, killMilestoneN);
                _lastBroadcastState = GameManager.Instance.State;
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                GameManager.Instance.OnKillsChanged += HandleKillsChanged;
            }

            // ComboCounter — sibling Polish singleton.
            _combo = ComboCounter.Instance;
            if (_combo != null)
            {
                _lastComboValue = _combo.Current;
                _combo.OnComboChanged += HandleComboChanged;
            }
        }

        private void UnsubscribeAll()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
            }
            if (_combo != null) _combo.OnComboChanged -= HandleComboChanged;

            _health = null;
            _level = null;
            _combo = null;
        }

        // ---- Event handlers --------------------------------------------------------------

        private void HandleLevelUp(int newLevel)
        {
            if (!enableLevelUp) return;
            Play(HapticPatternLibrary.LevelUpFlourish);
        }

        private void HandleStateChanged(GameState prev, GameState next)
        {
            _lastBroadcastState = next;
            if (!enableTerminalStates) return;

            switch (next)
            {
                case GameState.Victory:
                    Play(HapticPatternLibrary.Victory);
                    break;
                case GameState.GameOver:
                    Play(HapticPatternLibrary.Defeat);
                    break;
                case GameState.Playing:
                    // Fresh run — reset milestone tracking so the player doesn't get
                    // a stale buzz at kill 0.
                    if (GameManager.Instance != null)
                    {
                        _lastObservedKills = GameManager.Instance.Kills;
                        _lastMilestoneBucket = _lastObservedKills / Mathf.Max(1, killMilestoneN);
                    }
                    _knownBossInstanceIds.Clear();
                    break;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastKnownHealth < 0f)
            {
                _lastKnownHealth = current;
                return;
            }

            if (enableBigDamage && current < _lastKnownHealth && max > 0f)
            {
                float lost = _lastKnownHealth - current;
                float fraction = lost / max;
                if (fraction >= bigDamageMaxFraction)
                {
                    // Big hit — use the Explosion pattern (Heavy thump + Light debris).
                    Play(HapticPatternLibrary.Explosion);
                }
            }
            _lastKnownHealth = current;
        }

        private void HandleKillsChanged(int totalKills)
        {
            if (totalKills < _lastObservedKills)
            {
                // Run reset.
                _lastObservedKills = totalKills;
                _lastMilestoneBucket = totalKills / Mathf.Max(1, killMilestoneN);
                return;
            }

            if (!enableKillMilestones)
            {
                _lastObservedKills = totalKills;
                return;
            }

            int n = Mathf.Max(1, killMilestoneN);
            int nextBucket = totalKills / n;
            _lastObservedKills = totalKills;
            if (nextBucket > _lastMilestoneBucket)
            {
                _lastMilestoneBucket = nextBucket;
                Play(HapticPatternLibrary.TriplePulse);
            }
        }

        private void HandleComboChanged(int current)
        {
            if (!enableComboThreshold)
            {
                _lastComboValue = current;
                return;
            }

            // Only fire when CROSSING the threshold upward; don't refire while it stays above it.
            if (current >= comboThreshold && _lastComboValue < comboThreshold)
            {
                if (Time.unscaledTime - _lastComboPatternTime >= comboCooldownSeconds)
                {
                    _lastComboPatternTime = Time.unscaledTime;
                    Play(HapticPatternLibrary.RisingRumble);
                }
            }
            _lastComboValue = current;
        }

        // ---- Boss polling ----------------------------------------------------------------

        private void PollForBosses()
        {
            if (bossNameTokens == null || bossNameTokens.Length == 0) return;

            var enemies = EnemyRegistry.Enemies;
            if (enemies == null || enemies.Count == 0)
            {
                // No enemies alive — drop stale ids so a future identical id can re-trigger.
                if (_knownBossInstanceIds.Count > 0) _knownBossInstanceIds.Clear();
                return;
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null || !e.isActiveAndEnabled || e.Data == null) continue;
                if (!IsBossName(e.Data.displayName)) continue;

                int id = e.GetInstanceID();
                if (_knownBossInstanceIds.Add(id))
                {
                    Play(HapticPatternLibrary.BossArrival);
                    // Only one announcement per poll tick — otherwise multiple simultaneous
                    // boss spawns would stack rumbles.
                    break;
                }
            }
        }

        private bool IsBossName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return false;
            for (int i = 0; i < bossNameTokens.Length; i++)
            {
                var tok = bossNameTokens[i];
                if (string.IsNullOrEmpty(tok)) continue;
                if (displayName.IndexOf(tok, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ---- Pattern dispatch ------------------------------------------------------------

        /// <summary>
        /// Play a pattern, respecting global cooldown, the user's <see cref="HapticsSettings"/>
        /// strength multiplier, and the master <see cref="GameSettings.HapticsEnabled"/> toggle.
        /// Safe to call when no service is present. Public so external systems (e.g. dash, pickup
        /// FX) can trigger named patterns through the director's rate-limiter.
        /// </summary>
        public void Play(HapticPattern pattern)
        {
            if (pattern == null) return;
            if (!GameSettings.HapticsEnabled) return;
            if (HapticsSettings.IsEffectivelyMuted) return;

            float now = Time.unscaledTime;
            if (now - _lastPatternTime < globalCooldownSeconds) return;
            _lastPatternTime = now;

            var svc = HapticsService.Instance;
            if (svc == null) return;

            // Compose the final scalar from user-pref strength * intensity curve.
            float userScalar = HapticsSettings.AsPatternScalar();
            float curveMultiplier = useIntensityCurve && _curve != null ? _curve.Evaluate() : 1f;
            float finalScalar = Mathf.Clamp01(userScalar * curveMultiplier);

            // Cancel an in-flight pattern so a dramatic event (boss arrival, victory) supersedes
            // a mundane one (kill milestone) instead of queueing behind it.
            if (_runningPattern != null) StopCoroutine(_runningPattern);
            _runningPattern = StartCoroutine(RunPattern(pattern, svc, finalScalar));
        }

        /// <summary>Play a pattern by name. Returns false if the name isn't in the library.</summary>
        public bool PlayByName(string name)
        {
            if (HapticPatternLibrary.TryGet(name, out var pattern))
            {
                Play(pattern);
                return true;
            }
            return false;
        }

        private IEnumerator RunPattern(HapticPattern pattern, HapticsService svc, float scalar)
        {
            yield return pattern.Play(svc, scalar);
            _runningPattern = null;
        }
    }
}
