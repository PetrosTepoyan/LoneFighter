using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Owns the ordered list of <see cref="BossPhase"/>s for a single <see cref="BossEncounter"/>.
    /// On each <see cref="Tick"/> the manager polls the encounter's current HP fraction and,
    /// if a lower-threshold phase has been crossed, calls <see cref="BossPhase.Exit"/> on the
    /// previous phase and <see cref="BossPhase.Enter"/> on the new one. Multiple threshold
    /// crossings in a single frame (e.g. a huge nuke) are collapsed into a single transition
    /// to the final eligible phase so we never run two Enters back-to-back without an Exit.
    /// </summary>
    public class PhaseManager
    {
        private readonly List<BossPhase> _phases = new();
        private int _currentIndex = -1;

        public BossPhase Current => (_currentIndex >= 0 && _currentIndex < _phases.Count) ? _phases[_currentIndex] : null;
        public int CurrentIndex => _currentIndex;
        public int PhaseCount => _phases.Count;

        /// <summary>
        /// Add a phase. Phases must be added in highest-to-lowest <see cref="BossPhase.EntryHpFraction"/>
        /// order (Phase1 = 1.0, Phase2 = 0.66, Phase3 = 0.33). The manager will sort defensively
        /// before the first tick, but adding in order keeps debug output readable.
        /// </summary>
        public void AddPhase(BossPhase phase)
        {
            if (phase == null) return;
            _phases.Add(phase);
        }

        /// <summary>
        /// Sort phases descending by entry threshold so index 0 is the opening phase.
        /// </summary>
        public void Finalize()
        {
            _phases.Sort((a, b) => b.EntryHpFraction.CompareTo(a.EntryHpFraction));
        }

        /// <summary>
        /// Drive the state machine. Called by <see cref="BossEncounter.Update"/>.
        /// </summary>
        public void Tick(BossEncounter ctx, float hpFraction)
        {
            if (_phases.Count == 0) return;

            int target = ResolveTargetIndex(hpFraction);

            // Initial entry.
            if (_currentIndex < 0)
            {
                _currentIndex = target;
                _phases[_currentIndex].Enter(ctx);
                return;
            }

            // Threshold crossed (downward only — we never re-enter a higher-HP phase).
            if (target > _currentIndex)
            {
                _phases[_currentIndex].Exit(ctx);
                _currentIndex = target;
                _phases[_currentIndex].Enter(ctx);
            }

            _phases[_currentIndex].Tick(ctx);
        }

        /// <summary>
        /// Forcefully exits the current phase (used on death so cleanup runs).
        /// </summary>
        public void ForceExit(BossEncounter ctx)
        {
            if (_currentIndex >= 0 && _currentIndex < _phases.Count)
            {
                _phases[_currentIndex].Exit(ctx);
                _currentIndex = -1;
            }
        }

        private int ResolveTargetIndex(float hpFraction)
        {
            // Phases are sorted descending; pick the last one whose entry threshold is still >= hp.
            // Example with [1.0, 0.66, 0.33] and hp=0.5 → returns 1 (the 0.66 phase).
            int target = 0;
            for (int i = 0; i < _phases.Count; i++)
            {
                if (hpFraction <= _phases[i].EntryHpFraction) target = i;
                else break;
            }
            return target;
        }
    }
}
