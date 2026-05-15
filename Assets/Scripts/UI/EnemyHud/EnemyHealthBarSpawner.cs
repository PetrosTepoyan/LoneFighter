using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.UI.EnemyHud
{
    /// <summary>
    /// Singleton that watches <see cref="EnemyRegistry"/> for new/dead enemies and binds a
    /// pooled <see cref="EnemyHealthBar"/> to each one. Boss bars are NOT managed here —
    /// <see cref="BossHealthBar"/> is its own screen-space widget.
    ///
    /// Spawn strategy: each <see cref="Update"/> we walk the registry, diff it against the
    /// previous frame's snapshot (a cheap <see cref="HashSet{T}"/> lookup), and:
    ///   - Bind a new bar for any enemy we haven't seen.
    ///   - Release any bar whose enemy disappeared.
    /// This avoids modifying <see cref="EnemyRegistry"/> to add events.
    ///
    /// Toggle: gated by <see cref="EnemyHudSettings.ShowEnemyHealthBars"/>. When the toggle
    /// flips off mid-run, all active bars are released and new ones are not spawned.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBarSpawner : MonoBehaviour
    {
        public static EnemyHealthBarSpawner Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("Prefab whose root carries an EnemyHealthBar component. Use a world-space Canvas as the root.")]
        [SerializeField] private EnemyHealthBar healthBarPrefab;
        [Tooltip("Optional parent transform for instantiated bars (keeps the hierarchy tidy).")]
        [SerializeField] private Transform poolParent;

        [Header("Pooling")]
        [Tooltip("Number of bars to pre-instantiate on Start. Cheap and avoids hitches when a wave begins.")]
        [SerializeField] private int prewarmCount = 16;
        [Tooltip("Hard cap on simultaneously-active bars. Extra enemies just won't get a bar this frame; the pool catches up next.")]
        [SerializeField] private int maxActive = 128;

        [Header("Filtering")]
        [Tooltip("If true, the spawner will skip enemies whose EnemyData asset name (or displayName) contains any of the boss keywords below. " +
                 "Bosses are expected to use BossHealthBar instead.")]
        [SerializeField] private bool skipBosses = true;
        [Tooltip("Case-insensitive substrings used to identify bosses by EnemyData name. Edit per content.")]
        [SerializeField] private string[] bossKeywords = new[] { "boss", "warden" };

        // --- Runtime state -------------------------------------------------------
        private readonly Stack<EnemyHealthBar> _pool = new();
        private readonly Dictionary<EnemyBase, EnemyHealthBar> _active = new();
        private readonly HashSet<EnemyBase> _seenLastFrame = new();
        private readonly List<EnemyBase> _scratchDisappeared = new();
        private bool _toggleSubscribed;
        private bool _lastToggleState;

        // -------------------------------------------------------------------------

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_toggleSubscribed)
            {
                EnemyHudSettings.Changed -= HandleToggleChanged;
                _toggleSubscribed = false;
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Subscribe AFTER Awake so Instance is set and the static accessor has time to load.
            EnemyHudSettings.Changed += HandleToggleChanged;
            _toggleSubscribed = true;
            _lastToggleState = EnemyHudSettings.ShowEnemyHealthBars;

            Prewarm();
        }

        private void Prewarm()
        {
            if (healthBarPrefab == null || prewarmCount <= 0) return;
            for (int i = 0; i < prewarmCount; i++)
            {
                var bar = InstantiateBar();
                if (bar == null) break;
                bar.gameObject.SetActive(false);
                _pool.Push(bar);
            }
        }

        private EnemyHealthBar InstantiateBar()
        {
            if (healthBarPrefab == null) return null;
            var bar = Instantiate(healthBarPrefab, poolParent != null ? poolParent : transform);
            bar.gameObject.SetActive(false);
            return bar;
        }

        private void HandleToggleChanged()
        {
            bool now = EnemyHudSettings.ShowEnemyHealthBars;
            if (now == _lastToggleState) return;
            _lastToggleState = now;
            if (!now)
            {
                // Toggle flipped off — recycle everything.
                ReleaseAll();
            }
        }

        private void ReleaseAll()
        {
            foreach (var kvp in _active)
            {
                var bar = kvp.Value;
                if (bar == null) continue;
                bar.Bind(null);
                bar.gameObject.SetActive(false);
                _pool.Push(bar);
            }
            _active.Clear();
            _seenLastFrame.Clear();
        }

        private void Update()
        {
            // Cheap toggle gate: when disabled we still need to release bars whose enemies
            // died, but we skip spawning new ones.
            bool enabledNow = EnemyHudSettings.ShowEnemyHealthBars;

            ReconcileRegistry(enabledNow);
            TickActive();
        }

        private void ReconcileRegistry(bool spawnAllowed)
        {
            var enemies = EnemyRegistry.Enemies;

            // First pass: detect enemies that disappeared since last frame so we can release
            // their bars. We re-use _seenLastFrame; build a fresh "currently present" set
            // implicitly by checking enemies against _active in the second pass.
            _scratchDisappeared.Clear();
            foreach (var kvp in _active)
            {
                var enemy = kvp.Key;
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    _scratchDisappeared.Add(enemy);
                }
            }
            for (int i = 0; i < _scratchDisappeared.Count; i++)
            {
                ReleaseFor(_scratchDisappeared[i]);
            }

            // Second pass: spawn for any new enemy that doesn't yet have a bar.
            _seenLastFrame.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;
                _seenLastFrame.Add(enemy);

                if (!spawnAllowed) continue;
                if (_active.ContainsKey(enemy)) continue;
                if (_active.Count >= maxActive) break;
                if (skipBosses && IsBoss(enemy)) continue;

                SpawnFor(enemy);
            }
        }

        private bool IsBoss(EnemyBase enemy)
        {
            if (bossKeywords == null || bossKeywords.Length == 0) return false;
            var data = enemy.Data;
            if (data == null) return false;
            string assetName = data.name != null ? data.name.ToLowerInvariant() : string.Empty;
            string displayName = data.displayName != null ? data.displayName.ToLowerInvariant() : string.Empty;
            for (int i = 0; i < bossKeywords.Length; i++)
            {
                var k = bossKeywords[i];
                if (string.IsNullOrEmpty(k)) continue;
                k = k.ToLowerInvariant();
                if (assetName.Contains(k) || displayName.Contains(k)) return true;
            }
            // Also treat anything carrying a WardenBoss component as a boss.
            return enemy.GetComponent<WardenBoss>() != null;
        }

        private void SpawnFor(EnemyBase enemy)
        {
            if (healthBarPrefab == null) return;
            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null) return;

            EnemyHealthBar bar = _pool.Count > 0 ? _pool.Pop() : InstantiateBar();
            if (bar == null) return;

            bar.gameObject.SetActive(true);
            bar.Bind(health);
            _active[enemy] = bar;
        }

        private void ReleaseFor(EnemyBase enemy)
        {
            if (enemy == null)
            {
                // Defensive: an entry whose key was destroyed (Unity-null). Iterate to find it.
                EnemyBase deadKey = null;
                foreach (var kvp in _active)
                {
                    if (kvp.Key == null) { deadKey = kvp.Key; break; }
                }
                if (deadKey != null && _active.TryGetValue(deadKey, out var deadBar))
                {
                    _active.Remove(deadKey);
                    if (deadBar != null)
                    {
                        deadBar.Bind(null);
                        deadBar.gameObject.SetActive(false);
                        _pool.Push(deadBar);
                    }
                }
                return;
            }

            if (!_active.TryGetValue(enemy, out var bar)) return;
            _active.Remove(enemy);
            if (bar == null) return;
            bar.Bind(null);
            bar.gameObject.SetActive(false);
            _pool.Push(bar);
        }

        private void TickActive()
        {
            float dt = Time.deltaTime;
            foreach (var kvp in _active)
            {
                var bar = kvp.Value;
                if (bar != null) bar.Tick(dt);
            }
        }
    }
}
