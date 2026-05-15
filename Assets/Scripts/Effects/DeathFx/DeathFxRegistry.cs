using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Effects.DeathFx
{
    /// Inspector-authored mapping from enemy `displayName` (the same string used by
    /// `LoneFighter.Data.EnemyData.displayName`) → `DeathFxProfile`.
    ///
    /// Authoring rule: the lookup key is the enemy's *displayName*, NOT the SO file name.
    /// The dispatcher reads `EnemyBase.Data.displayName` at death time, so the canonical
    /// names from `ContentSeed` (Grunt / Runner / Tank / Spitter / Dasher / Bomber / Warden)
    /// are what you want here.
    ///
    /// Optional `defaultProfile` is used when no entry matches — letting designers ship a
    /// "generic explosion" baseline that covers any new archetypes that haven't gotten
    /// their own treatment yet.
    [CreateAssetMenu(
        menuName = "LoneFighter/Death FX Registry",
        fileName = "DeathFxRegistry")]
    public class DeathFxRegistry : ScriptableObject
    {
        /// One entry per enemy archetype. Keys are case-insensitive and trimmed at lookup
        /// time so trailing whitespace in either source doesn't break the map.
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Enemy `displayName` to match (case-insensitive). e.g. \"Grunt\", \"Warden\".")]
            public string enemyName;

            [Tooltip("Profile played when the enemy dies. Null entries are skipped (no FX).")]
            public DeathFxProfile profile;
        }

        [Tooltip("Per-archetype profiles. Duplicate keys — last-write-wins, like the audio bindings.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Tooltip("Profile used when no entry matches the dying enemy's displayName. Optional.")]
        [SerializeField] private DeathFxProfile defaultProfile;

        // Built lazily on first lookup, invalidated on OnValidate so inspector edits show up
        // without a domain reload. Case-insensitive comparer keeps "Grunt" == "grunt" working.
        private Dictionary<string, DeathFxProfile> _map;
        private bool _mapDirty = true;

        public DeathFxProfile DefaultProfile => defaultProfile;
        public IReadOnlyList<Entry> Entries => entries;

        private void OnEnable() { _mapDirty = true; }
        private void OnValidate() { _mapDirty = true; }

        /// Returns the profile bound to `enemyName`, or `defaultProfile`, or null.
        /// Never throws — accepts null / empty keys and returns the default in that case.
        public DeathFxProfile Resolve(string enemyName)
        {
            if (_mapDirty || _map == null) Rebuild();
            if (!string.IsNullOrWhiteSpace(enemyName))
            {
                if (_map.TryGetValue(enemyName.Trim(), out var profile) && profile != null)
                {
                    return profile;
                }
            }
            return defaultProfile;
        }

        /// Out-style variant for callers that want to know whether a match was found vs.
        /// fell back to the default.
        public bool TryResolve(string enemyName, out DeathFxProfile profile, out bool isDefault)
        {
            if (_mapDirty || _map == null) Rebuild();
            isDefault = false;
            if (!string.IsNullOrWhiteSpace(enemyName)
                && _map.TryGetValue(enemyName.Trim(), out profile) && profile != null)
            {
                return true;
            }
            profile = defaultProfile;
            isDefault = profile != null;
            return profile != null;
        }

        private void Rebuild()
        {
            if (_map == null)
            {
                _map = new Dictionary<string, DeathFxProfile>(
                    entries.Count, System.StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _map.Clear();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (string.IsNullOrWhiteSpace(e.enemyName)) continue;
                // Last-write-wins on duplicate keys, matching AudioManager's binding behavior.
                _map[e.enemyName.Trim()] = e.profile;
            }
            _mapDirty = false;
        }

        // -----------------------------------------------------------------------------------
        // Authoring helpers (used by the editor generator; safe to call at runtime too)
        // -----------------------------------------------------------------------------------

        /// Add or replace an entry for `enemyName`. Marks the asset dirty when called from
        /// the editor so the change persists.
        public void SetEntry(string enemyName, DeathFxProfile profile)
        {
            if (string.IsNullOrWhiteSpace(enemyName)) return;
            string key = enemyName.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].enemyName, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    var existing = entries[i];
                    existing.profile = profile;
                    entries[i] = existing;
                    _mapDirty = true;
                    return;
                }
            }
            entries.Add(new Entry { enemyName = key, profile = profile });
            _mapDirty = true;
        }
    }
}
