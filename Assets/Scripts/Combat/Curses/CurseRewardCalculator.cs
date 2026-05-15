// CurseRewardCalculator.cs — pure helper that converts the set of active
// curses on CurseManager into a single scalar multiplier other systems can
// fold into XP gain, gold drops, gem drop chance, etc.
//
// Kept as a separate class (rather than a property on CurseManager) so it
// stays trivially unit-testable and can be called from edit-time tools.

using System.Collections.Generic;

namespace LoneFighter.Combat.Curses
{
    /// <summary>
    /// Stateless helper that computes the run-wide reward multiplier from a
    /// collection of active curses. Always returns >= 1.0.
    /// </summary>
    public static class CurseRewardCalculator
    {
        /// <summary>
        /// Convenience wrapper that reads the live active set from
        /// <see cref="CurseManager"/>. Returns 1.0 when no manager exists yet
        /// (e.g. on the main menu before a run is configured) so callers can
        /// safely use this from any scene.
        /// </summary>
        public static float Multiplier
        {
            get
            {
                if (CurseManager.Instance == null) return 1f;
                return ComputeMultiplier(CurseManager.Instance.ActiveCurses);
            }
        }

        /// <summary>
        /// Computes <c>1 + sum(rewardBonus)</c> over the supplied curses. Null
        /// or empty inputs return 1.0. Null entries inside the collection are
        /// silently skipped so callers don't need to pre-filter.
        /// </summary>
        public static float ComputeMultiplier(IReadOnlyList<Curse> curses)
        {
            if (curses == null || curses.Count == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < curses.Count; i++)
            {
                var c = curses[i];
                if (c == null) continue;
                // rewardBonus is clamped to [0..5] on the SO, but defend
                // against negative values in case the asset is hand-edited.
                if (c.rewardBonus > 0f) sum += c.rewardBonus;
            }
            return 1f + sum;
        }

        /// <summary>
        /// Same as <see cref="ComputeMultiplier"/> but takes an
        /// <see cref="IEnumerable{T}"/> for non-list callers.
        /// </summary>
        public static float ComputeMultiplier(IEnumerable<Curse> curses)
        {
            if (curses == null) return 1f;
            float sum = 0f;
            foreach (var c in curses)
            {
                if (c == null) continue;
                if (c.rewardBonus > 0f) sum += c.rewardBonus;
            }
            return 1f + sum;
        }
    }
}
