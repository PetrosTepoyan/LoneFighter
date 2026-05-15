using System;
using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;

namespace LoneFighter.Combat.Streaks
{
    /// <summary>
    /// Uninterrupted kill-streak tracker. Increments on every kill registered with
    /// <see cref="GameManager"/>, and resets to zero the moment the player's current
    /// health decreases. This is intentionally distinct from
    /// <c>LoneFighter.Polish.ComboCounter</c>: that system uses a 3-second sliding
    /// window keyed off Time.time; this system measures **uninterrupted survival**.
    ///
    /// Drop a single instance into the Game scene (typically alongside
    /// <c>GameManager</c> or any persistent systems root). It auto-discovers the
    /// <see cref="PlayerHealth"/> on the active player.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class KillStreakTracker : MonoBehaviour
    {
        public static KillStreakTracker Instance { get; private set; }

        [Header("Behavior")]
        [Tooltip("If true, the tracker logs streak changes to the Console (Editor only).")]
        [SerializeField] private bool debugLog;

        /// <summary>Current uninterrupted kill count.</summary>
        public int Current { get; private set; }

        /// <summary>Highest streak achieved during the current run.</summary>
        public int BestThisRun { get; private set; }

        /// <summary>Fires whenever <see cref="Current"/> changes (including resets to zero).</summary>
        public event Action<int> OnStreakChanged;

        /// <summary>
        /// Fires when a non-zero streak is broken by the player taking damage.
        /// Payload is the value of <see cref="Current"/> at the moment of the break
        /// (i.e. the size of the streak that was just lost).
        /// </summary>
        public event Action<int> OnStreakBroken;

        // Internal bookkeeping.
        private int _lastObservedKillCount = -1;
        private float _lastObservedHealth = float.NaN;
        private PlayerHealth _health;
        private bool _subscribedToGameManager;
        private bool _subscribedToHealth;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeAll();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void Start()
        {
            // Late wire-up in case the dependencies booted after our OnEnable.
            TrySubscribe();
        }

        private void Update()
        {
            // Cheap, idempotent retry — survives scene reload / late-spawned player.
            if (!_subscribedToGameManager || !_subscribedToHealth)
            {
                TrySubscribe();
            }
        }

        private void TrySubscribe()
        {
            if (!_subscribedToGameManager && GameManager.Instance != null)
            {
                _lastObservedKillCount = GameManager.Instance.Kills;
                GameManager.Instance.OnKillsChanged += HandleKillsChanged;
                _subscribedToGameManager = true;
            }

            if (!_subscribedToHealth)
            {
                var player = PlayerController.Instance;
                if (player != null)
                {
                    _health = player.GetComponent<PlayerHealth>();
                    if (_health != null)
                    {
                        _lastObservedHealth = _health.Current;
                        _health.OnHealthChanged += HandleHealthChanged;
                        _subscribedToHealth = true;
                    }
                }
            }
        }

        private void UnsubscribeAll()
        {
            if (_subscribedToGameManager && GameManager.Instance != null)
            {
                GameManager.Instance.OnKillsChanged -= HandleKillsChanged;
            }
            _subscribedToGameManager = false;

            if (_subscribedToHealth && _health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
            _subscribedToHealth = false;
        }

        private void HandleKillsChanged(int totalKills)
        {
            // First sample after subscribing — record the baseline, don't fire.
            if (_lastObservedKillCount < 0)
            {
                _lastObservedKillCount = totalKills;
                return;
            }

            int delta = totalKills - _lastObservedKillCount;
            _lastObservedKillCount = totalKills;

            if (delta <= 0)
            {
                // GameManager.BeginRun() resets Kills to 0 and re-fires the event.
                // Treat that as a hard run-reset: clear streak silently and update baseline.
                if (Current != 0)
                {
                    int lost = Current;
                    Current = 0;
                    OnStreakChanged?.Invoke(Current);
                    if (debugLog) Debug.Log($"[KillStreakTracker] Run reset, dropped streak of {lost}.");
                }
                BestThisRun = 0;
                return;
            }

            // Accumulate new kills onto the streak.
            for (int i = 0; i < delta; i++)
            {
                Current++;
                if (Current > BestThisRun) BestThisRun = Current;
                OnStreakChanged?.Invoke(Current);
            }

            if (debugLog) Debug.Log($"[KillStreakTracker] Streak {Current} (best {BestThisRun}).");
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (float.IsNaN(_lastObservedHealth))
            {
                _lastObservedHealth = current;
                return;
            }

            // Damage = current decreased. Healing / max-HP raises do NOT break the streak.
            bool tookDamage = current < _lastObservedHealth - 0.0001f;
            _lastObservedHealth = current;

            if (!tookDamage) return;
            if (Current <= 0) return;

            int broken = Current;
            Current = 0;
            OnStreakBroken?.Invoke(broken);
            OnStreakChanged?.Invoke(Current);

            if (debugLog) Debug.Log($"[KillStreakTracker] Streak broken at {broken}.");
        }

        /// <summary>
        /// Manually clears the streak without firing <see cref="OnStreakBroken"/>.
        /// Intended for explicit run boundaries / tests; gameplay code should not need this.
        /// </summary>
        public void ResetSilently()
        {
            if (Current == 0) return;
            Current = 0;
            OnStreakChanged?.Invoke(Current);
        }
    }
}
