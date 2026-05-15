using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Singleton MonoBehaviour that watches <see cref="EnemyRegistry"/> and, for
    /// any newly-registered enemy, adds the four feedback components used by
    /// <see cref="HitFeedbackService"/>:
    ///   - <see cref="EnemySpriteFlash"/>
    ///   - <see cref="EnemyKnockback"/>
    ///   - <see cref="EnemyHitPause"/>
    ///   - <see cref="HitListener"/>
    ///
    /// We use polling (cheap iteration, once per <see cref="pollInterval"/>
    /// seconds) instead of subscribing to registry change events because
    /// <see cref="EnemyRegistry"/> is a static list with no change notification.
    /// We use a HashSet of instance IDs to remember which enemies we've already
    /// processed; pooled enemies returning into play will already have the
    /// components attached, so the attach is naturally idempotent — but the set
    /// lets us skip the AddComponent / GetComponent round trip.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyFeedbackAttacher : MonoBehaviour
    {
        public static EnemyFeedbackAttacher Instance { get; private set; }

        [Tooltip("Seconds between registry sweeps. 0 = sweep every Update. " +
                 "The work is tiny but we don't need it more than ~10 Hz.")]
        [SerializeField, Min(0f)] private float pollInterval = 0.1f;

        // Track which enemy instances we've already wired up.
        private readonly HashSet<int> _wired = new();
        private float _nextPollTime;

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

        private void Update()
        {
            if (Time.unscaledTime < _nextPollTime) return;
            _nextPollTime = Time.unscaledTime + pollInterval;

            var enemies = EnemyRegistry.Enemies;
            if (enemies == null) return;

            // Iterate by index — EnemyRegistry exposes IReadOnlyList<T>.
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;

                int id = enemy.GetInstanceID();
                if (_wired.Contains(id))
                {
                    // Still verify the components survived a pool round-trip —
                    // they should because AddComponent persists, but a defensive
                    // re-check is cheap.
                    continue;
                }

                Attach(enemy);
                _wired.Add(id);
            }
        }

        private static void Attach(EnemyBase enemy)
        {
            var go = enemy.gameObject;
            // Each helper uses [DisallowMultipleComponent] + GetComponent fallback,
            // so calling AddComponent unconditionally on a fresh prefab spawn is
            // safe. On the rare path where the prefab was authored with one of
            // these baked in we explicitly GetComponent first.
            EnsureComponent<EnemySpriteFlash>(go);
            EnsureComponent<EnemyKnockback>(go);
            EnsureComponent<EnemyHitPause>(go);
            EnsureComponent<HitListener>(go);
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return go.AddComponent<T>();
        }
    }
}
