using System;
using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Combat.Streaks
{
    /// <summary>
    /// Payload describing a temporary damage buff awarded for hitting a streak tier.
    /// Other combat systems (e.g. WeaponInventory, Projectile) can listen to
    /// <see cref="KillStreakRewards.OnStreakBuff"/> and apply <see cref="DamageMultiplier"/>
    /// for <see cref="DurationSeconds"/> seconds of unscaled time.
    /// </summary>
    public readonly struct StreakBuff
    {
        /// <summary>Streak tier that granted the buff (e.g. 10, 25, 50, 100, 250).</summary>
        public readonly int Tier;
        /// <summary>Multiplicative damage bonus, e.g. 1.10f = +10% damage.</summary>
        public readonly float DamageMultiplier;
        /// <summary>How long the buff should remain active, in unscaled seconds.</summary>
        public readonly float DurationSeconds;

        public StreakBuff(int tier, float damageMultiplier, float durationSeconds)
        {
            Tier = tier;
            DamageMultiplier = damageMultiplier;
            DurationSeconds = durationSeconds;
        }
    }

    /// <summary>
    /// Listens to <see cref="KillStreakTracker.OnStreakChanged"/> and fires off tiered
    /// rewards exactly once per run as the player crosses each threshold:
    ///
    /// <list type="bullet">
    ///   <item>Bonus XP via <see cref="PlayerLevel.AddXp"/>.</item>
    ///   <item>Temporary +damage buff broadcast through <see cref="OnStreakBuff"/>.</item>
    ///   <item>Free upgrade reroll signalled through <see cref="OnRerollGranted"/>.</item>
    ///   <item>Screen banner via <see cref="StreakBanner"/> (if one is assigned / present).</item>
    /// </list>
    ///
    /// Thresholds and per-tier amounts are configured inline below — no
    /// ScriptableObject required, to keep this drop-in.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class KillStreakRewards : MonoBehaviour
    {
        [Header("Refs (optional — auto-discovered if null)")]
        [SerializeField] private KillStreakTracker tracker;
        [SerializeField] private StreakBanner banner;

        [Header("Tier table (parallel arrays, ascending)")]
        [Tooltip("Streak counts at which rewards fire (must be ascending).")]
        [SerializeField] private int[] thresholds = { 10, 25, 50, 100, 250 };

        [Tooltip("Bonus XP awarded when each threshold is reached.")]
        [SerializeField] private int[] bonusXp = { 5, 15, 40, 100, 300 };

        [Tooltip("Damage multiplier per tier (1.10 = +10% damage).")]
        [SerializeField] private float[] buffDamageMultiplier = { 1.10f, 1.20f, 1.35f, 1.60f, 2.00f };

        [Tooltip("Buff duration per tier, in unscaled seconds.")]
        [SerializeField] private float[] buffDuration = { 8f, 10f, 12f, 15f, 20f };

        [Tooltip("Tiers that should grant a free upgrade reroll (one entry per threshold).")]
        [SerializeField] private bool[] grantReroll = { false, true, true, true, true };

        /// <summary>
        /// Fired when a tier grants a temporary damage buff. Other systems (weapon
        /// damage calculators, UI) should subscribe and apply the buff for its duration.
        /// </summary>
        public static event Action<StreakBuff> OnStreakBuff;

        /// <summary>
        /// Fired when a tier grants a free upgrade reroll. Payload is the streak tier.
        /// UI / UpgradeService listens for this to surface a reroll button or auto-reroll.
        /// </summary>
        public static event Action<int> OnRerollGranted;

        /// <summary>
        /// Fired whenever any reward tier is reached. Payload is the tier value.
        /// Convenience event for analytics / SFX hooks.
        /// </summary>
        public static event Action<int> OnRewardTierReached;

        // Highest tier index already awarded this streak. Resets when the streak resets.
        private int _highestTierAwarded = -1;

        private void OnEnable()
        {
            TryWire();
        }

        private void Start()
        {
            TryWire();
        }

        private void OnDisable()
        {
            if (tracker != null)
            {
                tracker.OnStreakChanged -= HandleStreakChanged;
                tracker.OnStreakBroken -= HandleStreakBroken;
            }
        }

        private void TryWire()
        {
            if (tracker == null) tracker = KillStreakTracker.Instance;
            if (tracker == null) return;

            // Re-subscribe defensively (no-op if already subscribed because of delegate dedup).
            tracker.OnStreakChanged -= HandleStreakChanged;
            tracker.OnStreakChanged += HandleStreakChanged;
            tracker.OnStreakBroken -= HandleStreakBroken;
            tracker.OnStreakBroken += HandleStreakBroken;

            if (banner == null) banner = FindBanner();
        }

        private static StreakBanner FindBanner()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<StreakBanner>(FindObjectsInactive.Include);
#else
            return UnityEngine.Object.FindObjectOfType<StreakBanner>(true);
#endif
        }

        private void HandleStreakChanged(int current)
        {
            if (current <= 0)
            {
                _highestTierAwarded = -1;
                return;
            }
            if (thresholds == null || thresholds.Length == 0) return;

            for (int i = _highestTierAwarded + 1; i < thresholds.Length; i++)
            {
                if (current < thresholds[i]) break;
                _highestTierAwarded = i;
                AwardTier(i);
            }
        }

        private void HandleStreakBroken(int _)
        {
            _highestTierAwarded = -1;
        }

        private void AwardTier(int idx)
        {
            int tier = thresholds[idx];

            // 1. Bonus XP.
            int xp = SafeGet(bonusXp, idx, 0);
            if (xp > 0)
            {
                var level = FindPlayerLevel();
                if (level != null) level.AddXp(xp);
            }

            // 2. Damage buff.
            float dmgMult = SafeGet(buffDamageMultiplier, idx, 1f);
            float dur = SafeGet(buffDuration, idx, 0f);
            if (dmgMult > 1f && dur > 0f)
            {
                OnStreakBuff?.Invoke(new StreakBuff(tier, dmgMult, dur));
            }

            // 3. Free reroll.
            bool reroll = SafeGet(grantReroll, idx, false);
            if (reroll)
            {
                Debug.Log($"[KillStreakRewards] Tier {tier} granted free upgrade reroll.");
                OnRerollGranted?.Invoke(tier);
            }

            // 4. Banner.
            if (banner != null) banner.Show(tier);

            OnRewardTierReached?.Invoke(tier);
        }

        private static PlayerLevel FindPlayerLevel()
        {
            if (PlayerController.Instance != null)
            {
                var pl = PlayerController.Instance.GetComponent<PlayerLevel>();
                if (pl != null) return pl;
            }
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<PlayerLevel>();
#else
            return UnityEngine.Object.FindObjectOfType<PlayerLevel>();
#endif
        }

        private static T SafeGet<T>(T[] arr, int idx, T fallback)
        {
            if (arr == null || idx < 0 || idx >= arr.Length) return fallback;
            return arr[idx];
        }
    }
}
