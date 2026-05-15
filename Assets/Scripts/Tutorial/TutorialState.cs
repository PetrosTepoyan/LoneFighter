using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Tutorial
{
    /// <summary>
    /// Persistent record of which tutorial steps the player has already completed.
    /// Backed by <see cref="PlayerPrefs"/> under the key
    /// <see cref="PrefsKey"/> as a comma-separated list of step IDs.
    /// </summary>
    /// <remarks>
    /// The set is loaded lazily on first access and kept in sync with PlayerPrefs
    /// after each mutation. The class is intentionally static so any system in the
    /// game (gameplay or editor tooling) can ask whether a step has been seen
    /// without having to find a singleton in the scene.
    ///
    /// The special step id <see cref="BootstrapStepId"/> is recorded the very first
    /// time the tutorial runs to distinguish a brand-new player from a returning
    /// one (<see cref="IsFirstEverRun"/>).
    /// </remarks>
    public static class TutorialState
    {
        /// <summary>PlayerPrefs key used for persistence.</summary>
        public const string PrefsKey = "LoneFighter.Tutorial.CompletedSteps";

        /// <summary>
        /// Sentinel step ID recorded on the player's first ever run.
        /// When this entry is missing the player is considered a first-time user.
        /// </summary>
        public const string BootstrapStepId = "Tutorial.Bootstrap";

        private static HashSet<string> _completed;
        private static bool _loaded;

        /// <summary>
        /// True if the bootstrap marker has not yet been written — i.e. this is the
        /// first time the player has ever launched the tutorial system.
        /// </summary>
        public static bool IsFirstEverRun
        {
            get
            {
                EnsureLoaded();
                return !_completed.Contains(BootstrapStepId);
            }
        }

        /// <summary>Returns true if the given step has already been completed.</summary>
        public static bool IsCompleted(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return false;
            EnsureLoaded();
            return _completed.Contains(stepId);
        }

        /// <summary>
        /// Records a step as completed and immediately persists the new set to
        /// PlayerPrefs. Safe to call repeatedly with the same id.
        /// </summary>
        public static void MarkCompleted(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return;
            EnsureLoaded();
            if (_completed.Add(stepId))
            {
                Save();
            }
        }

        /// <summary>
        /// Wipes the persisted tutorial progress entirely (including the
        /// bootstrap marker, so the next run will be treated as first-ever).
        /// Intended for editor tooling and QA.
        /// </summary>
        public static void ResetAll()
        {
            _completed = new HashSet<string>();
            _loaded = true;
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _completed = new HashSet<string>();
            var raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split(',');
                for (int i = 0; i < parts.Length; i++)
                {
                    var trimmed = parts[i]?.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) _completed.Add(trimmed);
                }
            }
            _loaded = true;
        }

        private static void Save()
        {
            // Always materialise an empty string for "no completed steps" so the
            // PlayerPrefs entry sticks around predictably.
            var joined = _completed == null || _completed.Count == 0
                ? string.Empty
                : string.Join(",", _completed);
            PlayerPrefs.SetString(PrefsKey, joined);
            PlayerPrefs.Save();
        }
    }
}
