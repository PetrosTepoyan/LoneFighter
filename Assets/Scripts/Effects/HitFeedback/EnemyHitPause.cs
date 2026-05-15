using UnityEngine;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// "Stagger" effect: disables the chase AI on this enemy for a tiny moment
    /// (default 40 ms) so the enemy briefly stops in its tracks when hit. The
    /// chase AI is the only mover for regular enemies; disabling it freezes
    /// their pursuit but leaves the Rigidbody2D in place so physics still
    /// settles normally.
    ///
    /// Skipped on bosses — they have their own choreographed timing and a
    /// per-bullet stagger would feel cheap and read as a bug. Boss detection
    /// is name-based (presence of a "WardenBoss"-style component on the same
    /// GameObject) to avoid taking a hard reference on the bosses project.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHitPause : MonoBehaviour
    {
        [Tooltip("How long the AI is suspended (seconds, unscaled).")]
        [SerializeField, Min(0f)] private float duration = 0.04f;

        [Tooltip("If true, presence of a boss-style component disables this stagger entirely.")]
        [SerializeField] private bool skipOnBosses = true;

        [Tooltip("Component type names that mark this enemy as a 'boss' (case-sensitive). " +
                 "Default covers WardenBoss; extend as new bosses ship.")]
        [SerializeField] private string[] bossComponentNames = { "WardenBoss" };

        private Behaviour _chaseAi;
        private bool _isBoss;
        private float _remaining;
        private bool _paused;

        private void Awake()
        {
            ResolveAi();
            _isBoss = DetectBoss();
        }

        private void OnEnable()
        {
            // Components may have been swapped on pool reuse — re-check.
            ResolveAi();
            _isBoss = DetectBoss();
            _remaining = 0f;
            _paused = false;
        }

        private void OnDisable()
        {
            if (_paused && _chaseAi != null) _chaseAi.enabled = true;
            _paused = false;
            _remaining = 0f;
        }

        private void ResolveAi()
        {
            _chaseAi = null;
            var components = GetComponents<Behaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;
                if (components[i].GetType().Name == "EnemyChaseAI")
                {
                    _chaseAi = components[i];
                    break;
                }
            }
        }

        private bool DetectBoss()
        {
            if (!skipOnBosses || bossComponentNames == null) return false;
            var all = GetComponents<Component>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].GetType().Name;
                for (int j = 0; j < bossComponentNames.Length; j++)
                {
                    if (!string.IsNullOrEmpty(bossComponentNames[j]) &&
                        bossComponentNames[j] == n)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Trigger the stagger. Stacks: additional calls during the pause window
        /// extend it to a full <see cref="duration"/> from now.
        /// </summary>
        public void Apply()
        {
            if (_isBoss) return;
            if (_chaseAi == null) return;
            if (!_paused)
            {
                _chaseAi.enabled = false;
                _paused = true;
            }
            _remaining = duration;
        }

        private void Update()
        {
            if (!_paused) return;
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                if (_chaseAi != null) _chaseAi.enabled = true;
                _paused = false;
            }
        }
    }
}
