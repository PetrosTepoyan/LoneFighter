using System.Collections.Generic;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// Static catalog of named, ready-to-play <see cref="HapticPattern"/> instances.
    /// Patterns are built lazily on first access and cached. Look up by name via
    /// <see cref="TryGet"/> or grab a strongly-typed pattern via the named properties.
    ///
    /// All durations are in seconds, gaps in milliseconds — matching the contract on
    /// <see cref="HapticPattern.Step"/>. Step "duration" only matters as a hold-before-next-step
    /// window: the underlying <see cref="HapticsService"/> picks the actual pulse length per
    /// platform.
    /// </summary>
    public static class HapticPatternLibrary
    {
        // ---- Tuning constants (seconds) ----------------------------------------------------
        // We use a small "pulse hold" so that two adjacent steps don't collide with each other
        // on the gamepad path (which holds the motors for ~80ms by default in HapticsService).
        private const float DefaultStepHold = 0.04f;
        private const float ShortPulseHold = 0.02f;

        // ---- Cached pattern instances. Built once on first access. -------------------------
        private static HapticPattern _doublePulse;
        private static HapticPattern _triplePulse;
        private static HapticPattern _risingRumble;
        private static HapticPattern _explosion;
        private static HapticPattern _bossArrival;
        private static HapticPattern _victory;
        private static HapticPattern _defeat;
        private static HapticPattern _levelUpFlourish;
        private static HapticPattern _dashTick;

        // ---- Named pattern accessors ------------------------------------------------------

        /// <summary>Two quick Light taps separated by 60ms. Good for "pickup confirmed" feedback.</summary>
        public static HapticPattern DoublePulse => _doublePulse ??= HapticPattern.FromTuples(
            "DoublePulse",
            (HapticIntensity.Light, DefaultStepHold, 60f),
            (HapticIntensity.Light, DefaultStepHold, 0f));

        /// <summary>Three Medium pulses with 50ms gaps. Used as the every-Nth-kill cue.</summary>
        public static HapticPattern TriplePulse => _triplePulse ??= HapticPattern.FromTuples(
            "TriplePulse",
            (HapticIntensity.Medium, DefaultStepHold, 50f),
            (HapticIntensity.Medium, DefaultStepHold, 50f),
            (HapticIntensity.Medium, DefaultStepHold, 0f));

        /// <summary>Ramping Light → Medium → Heavy with 80ms tail on each. Combo escalator.</summary>
        public static HapticPattern RisingRumble => _risingRumble ??= HapticPattern.FromTuples(
            "RisingRumble",
            (HapticIntensity.Light, DefaultStepHold, 80f),
            (HapticIntensity.Medium, DefaultStepHold, 80f),
            (HapticIntensity.Heavy, DefaultStepHold, 80f));

        /// <summary>Big Heavy thump immediately followed by two Light "debris" pulses.</summary>
        public static HapticPattern Explosion => _explosion ??= HapticPattern.FromTuples(
            "Explosion",
            (HapticIntensity.Heavy, DefaultStepHold, 0f),
            (HapticIntensity.Light, DefaultStepHold, 100f),
            (HapticIntensity.Light, DefaultStepHold, 0f));

        /// <summary>Slow, ominous: three Heavy pulses with 200ms gaps. Boss has entered the arena.</summary>
        public static HapticPattern BossArrival => _bossArrival ??= HapticPattern.FromTuples(
            "BossArrival",
            (HapticIntensity.Heavy, DefaultStepHold, 200f),
            (HapticIntensity.Heavy, DefaultStepHold, 200f),
            (HapticIntensity.Heavy, DefaultStepHold, 0f));

        /// <summary>Ascending victory fanfare: Light, Light, Medium, Heavy with 100ms gaps.</summary>
        public static HapticPattern Victory => _victory ??= HapticPattern.FromTuples(
            "Victory",
            (HapticIntensity.Light, DefaultStepHold, 100f),
            (HapticIntensity.Light, DefaultStepHold, 100f),
            (HapticIntensity.Medium, DefaultStepHold, 100f),
            (HapticIntensity.Heavy, DefaultStepHold, 0f));

        /// <summary>Descending dirge: Heavy, Medium, Light with 200ms gaps. Run is over.</summary>
        public static HapticPattern Defeat => _defeat ??= HapticPattern.FromTuples(
            "Defeat",
            (HapticIntensity.Heavy, DefaultStepHold, 200f),
            (HapticIntensity.Medium, DefaultStepHold, 200f),
            (HapticIntensity.Light, DefaultStepHold, 0f));

        /// <summary>Medium → Light → Light → Medium flourish with 80ms gaps. Level-up celebration.</summary>
        public static HapticPattern LevelUpFlourish => _levelUpFlourish ??= HapticPattern.FromTuples(
            "LevelUpFlourish",
            (HapticIntensity.Medium, DefaultStepHold, 80f),
            (HapticIntensity.Light, DefaultStepHold, 80f),
            (HapticIntensity.Light, DefaultStepHold, 80f),
            (HapticIntensity.Medium, DefaultStepHold, 0f));

        /// <summary>Single, very short Light tick. Use for movement abilities like dash.</summary>
        public static HapticPattern DashTick => _dashTick ??= HapticPattern.FromTuples(
            "DashTick",
            (HapticIntensity.Light, ShortPulseHold, 0f));

        // ---- Lookup -----------------------------------------------------------------------

        /// <summary>
        /// Return all patterns in the library — useful for editor inspectors / tests.
        /// </summary>
        public static IEnumerable<HapticPattern> All
        {
            get
            {
                yield return DoublePulse;
                yield return TriplePulse;
                yield return RisingRumble;
                yield return Explosion;
                yield return BossArrival;
                yield return Victory;
                yield return Defeat;
                yield return LevelUpFlourish;
                yield return DashTick;
            }
        }

        /// <summary>
        /// Look up a pattern by its (case-insensitive) name. Returns true if found.
        /// </summary>
        public static bool TryGet(string name, out HapticPattern pattern)
        {
            pattern = null;
            if (string.IsNullOrEmpty(name)) return false;

            foreach (var p in All)
            {
                if (string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    pattern = p;
                    return true;
                }
            }
            return false;
        }
    }
}
