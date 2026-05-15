using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Maps a weapon (looked up by <see cref="LoneFighter.Data.WeaponData.displayName"/>)
    /// to a <see cref="WeaponAuraProfile"/>.
    ///
    /// String-based lookup is used to avoid coupling this asset to direct WeaponData
    /// references, which keeps the registry safe to author independently of weapon
    /// assets and unaffected by hard ScriptableObject reference churn.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Effects/Weapon Aura Registry",
                     fileName = "WeaponAuraRegistry")]
    public class WeaponAuraRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Case-insensitive match against WeaponData.displayName.")]
            public string weaponDisplayName;
            public WeaponAuraProfile profile;
        }

        [Tooltip("Lookup table from WeaponData.displayName -> aura profile.")]
        public List<Entry> entries = new List<Entry>();

        [Tooltip("Fallback profile used when no explicit entry matches an active weapon. " +
                 "Set to null to suppress the aura for unmapped weapons.")]
        public WeaponAuraProfile defaultProfile;

        // Lazy lookup cache, rebuilt on demand from the inspector list.
        private Dictionary<string, WeaponAuraProfile> _cache;

        private void OnEnable()
        {
            _cache = null;
        }

        private void OnValidate()
        {
            _cache = null;
        }

        private void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<string, WeaponAuraProfile>(
                System.StringComparer.OrdinalIgnoreCase);
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (string.IsNullOrWhiteSpace(e.weaponDisplayName) || e.profile == null) continue;
                _cache[e.weaponDisplayName.Trim()] = e.profile;
            }
        }

        /// <summary>Returns the profile for a weapon display name, or the default if no entry matches.</summary>
        public WeaponAuraProfile Resolve(string weaponDisplayName)
        {
            EnsureCache();
            if (!string.IsNullOrWhiteSpace(weaponDisplayName))
            {
                if (_cache.TryGetValue(weaponDisplayName.Trim(), out var p) && p != null) return p;
            }
            return defaultProfile;
        }
    }
}
