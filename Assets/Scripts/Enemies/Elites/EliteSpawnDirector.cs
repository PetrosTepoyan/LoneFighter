using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// Singleton that watches <see cref="EnemyRegistry"/> for newly-spawned enemies and
    /// promotes them to elites on a schedule.
    ///
    /// Promotion criteria:
    ///  * Every <see cref="secondsBetweenElites"/> seconds (default 30s), the next
    ///    new enemy added to the registry is promoted.
    ///  * OR every <see cref="killsBetweenElites"/> kills (default 40), the next new
    ///    enemy is promoted. Either trigger arms the director — they're not additive.
    ///
    /// Why poll instead of an event subscription? <see cref="EnemyRegistry"/> is a
    /// plain static list with no growth hook; modifying it would violate "new files only".
    /// We watch <c>EnemyRegistry.Enemies</c> per-frame, diffing against the previous
    /// snapshot to detect entries appearing at the tail.
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteSpawnDirector : MonoBehaviour
    {
        public static EliteSpawnDirector Instance { get; private set; }

        [Header("Schedule")]
        [Tooltip("Seconds between elite spawn arms. After this many seconds, the next new enemy becomes an elite.")]
        [Min(1f)]
        [SerializeField] private float secondsBetweenElites = 30f;

        [Tooltip("Kill-based trigger. After this many kills since the last elite, the next new enemy becomes an elite.")]
        [Min(1)]
        [SerializeField] private int killsBetweenElites = 40;

        [Header("Drops")]
        [SerializeField] private EliteDropTable dropTable;

        [Header("Visuals (optional overrides)")]
        [SerializeField] private GameObject auraPrefabOverride;

        // Polling state
        private readonly HashSet<int> _knownInstanceIds = new();
        private float _secondsTimer;
        private int _killsAtLastElite;
        private bool _armed;

        // Event hook
        /// <summary>Raised whenever an elite is promoted. Banner + indicator listen.</summary>
        public static event Action<EliteModifier> OnEliteAppeared;

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
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            // Treat existing enemies as known so we don't promote pre-existing ones.
            SeedKnownIds();
            _secondsTimer = 0f;
            _armed = false;
            _killsAtLastElite = GameManager.Instance != null ? GameManager.Instance.Kills : 0;
        }

        private void Update()
        {
            // Skip while the run isn't actively in play.
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            UpdateArming();
            ScanForNewEnemies();
        }

        private void UpdateArming()
        {
            if (_armed) return;

            _secondsTimer += Time.deltaTime;
            if (_secondsTimer >= secondsBetweenElites)
            {
                _armed = true;
                return;
            }

            if (GameManager.Instance != null)
            {
                int killDelta = GameManager.Instance.Kills - _killsAtLastElite;
                if (killDelta >= killsBetweenElites)
                {
                    _armed = true;
                }
            }
        }

        private void ScanForNewEnemies()
        {
            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var enemy = list[i];
                if (enemy == null) continue;
                int id = enemy.GetInstanceID();
                if (_knownInstanceIds.Add(id))
                {
                    // Brand-new (or pooled-reuse-as-new) enemy.
                    if (_armed)
                    {
                        Promote(enemy);
                        _armed = false;
                        _secondsTimer = 0f;
                        _killsAtLastElite = GameManager.Instance != null ? GameManager.Instance.Kills : 0;
                    }
                }
            }

            // Drop unregistered ids occasionally so the set doesn't grow forever.
            // Cheap O(n) sweep, only on tick boundaries.
            if (_knownInstanceIds.Count > 512 && Time.frameCount % 120 == 0)
            {
                PruneKnownIds();
            }
        }

        private void SeedKnownIds()
        {
            _knownInstanceIds.Clear();
            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) _knownInstanceIds.Add(list[i].GetInstanceID());
            }
        }

        private void PruneKnownIds()
        {
            var alive = new HashSet<int>();
            var list = EnemyRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) alive.Add(list[i].GetInstanceID());
            }
            _knownInstanceIds.IntersectWith(alive);
        }

        private void Promote(EnemyBase enemy)
        {
            if (enemy == null) return;
            // Skip if somehow already promoted (e.g. pre-attached EliteModifier in a prefab).
            if (enemy.GetComponent<EliteModifier>() != null) return;

            var modifier = enemy.gameObject.AddComponent<EliteModifier>();
            modifier.Configure(dropTable, auraPrefabOverride);

            // AddComponent triggers OnEnable on the same frame (since the host is active),
            // which fires NotifyEliteAppeared for us. We don't need to raise here.
        }

        // ----- announce / static API ---------------------------------------------

        internal static void NotifyEliteAppeared(EliteModifier modifier)
        {
            if (modifier == null) return;
            try { OnEliteAppeared?.Invoke(modifier); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>Force-promote a specific enemy (for debug / cheats / scripted events).</summary>
        public void ForcePromote(EnemyBase enemy)
        {
            Promote(enemy);
            _armed = false;
            _secondsTimer = 0f;
            _killsAtLastElite = GameManager.Instance != null ? GameManager.Instance.Kills : 0;
        }
    }
}
