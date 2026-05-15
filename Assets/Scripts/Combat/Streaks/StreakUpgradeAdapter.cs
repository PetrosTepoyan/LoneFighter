using System;
using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Combat.Streaks
{
    /// <summary>
    /// Placeholder adapter for a future <c>"Streaker"</c> upgrade — described as:
    ///   <c>"+XP bonus per kill in streak"</c>
    ///
    /// The intent is that once the upgrade is added to the upgrade pool, this
    /// adapter is the single place that listens to streak events and translates
    /// them into rewards, so the rest of the system (KillStreakTracker / Rewards)
    /// does not need to know anything about upgrades.
    ///
    /// === Documented integration hooks (when the upgrade ships) ===
    ///
    /// 1. <see cref="UnityEngine.MonoBehaviour"/> ownership: this adapter should be
    ///    hosted alongside the other streak components (same persistent root). One
    ///    instance per run.
    ///
    /// 2. Acquire the upgrade stack: when <c>UpgradeService.Apply(upgrade)</c> is
    ///    called for a <c>StreakerUpgradeData</c> asset, that asset should call
    ///    <see cref="StackUp"/> on this adapter (via <see cref="Instance"/>). One
    ///    pick = one stack.
    ///
    /// 3. Per-kill XP grant: <see cref="HandleStreakChanged"/> already fires for
    ///    every kill that increments the streak. While <see cref="Stacks"/> &gt; 0,
    ///    convert the streak count into bonus XP using
    ///    <c>xpPerKill * Stacks * Mathf.Min(streak, maxScalingStreak)</c> and grant
    ///    it via <see cref="PlayerLevel.AddXp"/>.
    ///
    /// 4. UI surface: <see cref="OnBonusXpGranted"/> can be subscribed to by the
    ///    HUD to flash a small "+N STREAK XP" popup.
    ///
    /// 5. Run reset: <see cref="KillStreakTracker.OnStreakBroken"/> is **not**
    ///    needed here — streak-broken implies no future streak XP until the next
    ///    kill, which falls out naturally. Stacks persist across streak breaks,
    ///    because they're a run-scoped permanent upgrade pick.
    ///
    /// 6. Inter-run reset: subscribe to <c>GameManager.OnStateChanged</c> and zero
    ///    <see cref="Stacks"/> on transitions into <c>GameState.Playing</c> from
    ///    <c>Boot</c> (i.e. a fresh run).
    ///
    /// This file intentionally does **no** XP granting today — it's a stub that
    /// exposes the surface the upgrade asset will plug into. Wiring it up to
    /// `UpgradeService` requires editing `UpgradeService.cs` (out of scope for the
    /// folder this lives in), so the actual hookup is deferred.
    /// </summary>
    public class StreakUpgradeAdapter : MonoBehaviour
    {
        public static StreakUpgradeAdapter Instance { get; private set; }

        [Header("Tuning (used by the future upgrade)")]
        [Tooltip("Bonus XP per kill, per stack of the Streaker upgrade.")]
        [SerializeField] private int xpPerKillPerStack = 1;

        [Tooltip("Streak count beyond which the per-kill bonus stops scaling.")]
        [SerializeField, Min(1)] private int maxScalingStreak = 50;

        [Tooltip("If true, the adapter applies bonus XP today. Leave OFF until the upgrade is in.")]
        [SerializeField] private bool enableBonusXp;

        /// <summary>Current number of Streaker stacks (one per upgrade pick).</summary>
        public int Stacks { get; private set; }

        /// <summary>Per-kill tuning, exposed so the future upgrade asset can read it.</summary>
        public int XpPerKillPerStack => xpPerKillPerStack;

        /// <summary>Streak cap beyond which the per-kill bonus stops scaling.</summary>
        public int MaxScalingStreak => maxScalingStreak;

        /// <summary>
        /// Fires when bonus XP is granted from a kill while at least one Streaker
        /// stack is active. Payload is the XP amount. HUDs can listen for popups.
        /// </summary>
        public event Action<int> OnBonusXpGranted;

        private KillStreakTracker _tracker;
        private PlayerLevel _level;
        private int _lastStreakObserved;

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
            if (_tracker != null) _tracker.OnStreakChanged -= HandleStreakChanged;
        }

        private void OnEnable()
        {
            TryWire();
        }

        private void Start()
        {
            TryWire();
        }

        private void TryWire()
        {
            if (_tracker == null) _tracker = KillStreakTracker.Instance;
            if (_tracker != null)
            {
                _tracker.OnStreakChanged -= HandleStreakChanged;
                _tracker.OnStreakChanged += HandleStreakChanged;
                _lastStreakObserved = _tracker.Current;
            }
            if (_level == null && PlayerController.Instance != null)
            {
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
        }

        /// <summary>
        /// Called by the future StreakerUpgradeData asset on each upgrade pick.
        /// </summary>
        public void StackUp()
        {
            Stacks++;
        }

        /// <summary>Reset stacks (e.g. at the start of a new run).</summary>
        public void ResetStacks()
        {
            Stacks = 0;
        }

        private void HandleStreakChanged(int current)
        {
            int prev = _lastStreakObserved;
            _lastStreakObserved = current;

            // We only act on increments (i.e. kills). Resets to zero are ignored.
            if (current <= prev) return;
            if (!enableBonusXp || Stacks <= 0) return;

            int effectiveStreak = Mathf.Min(current, Mathf.Max(1, maxScalingStreak));
            int amount = xpPerKillPerStack * Stacks * effectiveStreak;
            if (amount <= 0) return;

            if (_level == null && PlayerController.Instance != null)
            {
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_level != null) _level.AddXp(amount);
            OnBonusXpGranted?.Invoke(amount);
        }
    }
}
