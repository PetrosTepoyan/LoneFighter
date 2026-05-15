using UnityEngine;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Abstract base for a single phase of a multi-phase boss encounter.
    /// A phase owns whatever stateful adjustments it makes to the boss in <see cref="Enter"/>
    /// and is responsible for unwinding them in <see cref="Exit"/>. <see cref="Tick"/> runs
    /// every <see cref="BossEncounter"/> Update while this phase is active.
    /// </summary>
    public abstract class BossPhase
    {
        /// <summary>Human-readable name used for logs / debug HUDs.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// HP fraction at which this phase BEGINS (e.g. 1.0 = full health, 0.66 = 66%).
        /// Sorted descending by <see cref="PhaseManager"/>; the highest threshold less than or
        /// equal to the current HP fraction wins.
        /// </summary>
        public abstract float EntryHpFraction { get; }

        /// <summary>Called once when this phase becomes active.</summary>
        public virtual void Enter(BossEncounter ctx) { }

        /// <summary>Called once when transitioning away from this phase (including on death).</summary>
        public virtual void Exit(BossEncounter ctx) { }

        /// <summary>Per-frame update while this phase is active.</summary>
        public virtual void Tick(BossEncounter ctx) { }
    }
}
