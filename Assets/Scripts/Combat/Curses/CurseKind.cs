// CurseKind.cs — strongly-typed identifiers for the eight starter curses.
//
// New kinds can be added at the end of the enum (do NOT reorder — values are
// persisted on Curse ScriptableObjects on disk via Unity's serializer).

namespace LoneFighter.Combat.Curses
{
    /// <summary>
    /// Logical effect family of a <see cref="Curse"/>.
    /// The numeric value of each entry is part of the on-disk asset format,
    /// so existing entries must not be re-ordered or re-numbered.
    /// </summary>
    public enum CurseKind
    {
        /// <summary>+X% enemy contact damage.</summary>
        Bloodlust    = 0,

        /// <summary>+X% enemy spawn rate (requires WaveManager extension).</summary>
        Swarm        = 1,

        /// <summary>-X% player max HP at run start.</summary>
        Fragile      = 2,

        /// <summary>-X% player move speed.</summary>
        Slowed       = 3,

        /// <summary>Disables crit hits (signalled via static flag).</summary>
        NoCrits      = 4,

        /// <summary>+X% player damage, -X% max HP.</summary>
        GlassCannon  = 5,

        /// <summary>Visual: heavy screen-edge vignette darkens the arena.</summary>
        DenseFog     = 6,

        /// <summary>Boss spawn count doubled (requires WaveManager extension).</summary>
        ExtraBosses  = 7,
    }
}
