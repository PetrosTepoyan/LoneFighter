using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// Maps a <see cref="WeaponData"/> (by <c>displayName</c>) to a HUD icon
    /// <see cref="Sprite"/>. Authored as a ScriptableObject so a designer can drop
    /// in alternate icon art without editing code, and so the runtime HUD can fall
    /// back to a single default sprite when a weapon name is missing.
    ///
    /// Lookup precedence in <see cref="Get"/>:
    ///   1. Entry with matching <c>weaponName</c> (case-insensitive) → its <c>icon</c>.
    ///   2. The <c>data.icon</c> field already on <c>WeaponData</c>, if non-null.
    ///   3. <see cref="defaultIcon"/> (typically a white circle).
    ///   4. <c>null</c> — caller should leave the slot's icon untouched.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/UI/Weapon Icon Registry",
                     fileName = "WeaponIconRegistry")]
    public class WeaponIconRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            [Tooltip("Matched (case-insensitive) against WeaponData.displayName.")]
            public string weaponName;
            public Sprite icon;
        }

        [Tooltip("Per-weapon icon overrides. First match wins.")]
        public List<Entry> entries = new();

        [Tooltip("Used when no entry matches and WeaponData.icon is also null.")]
        public Sprite defaultIcon;

        private Dictionary<string, Sprite> _cache;

        private void RebuildCache()
        {
            _cache = new Dictionary<string, Sprite>(entries.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.weaponName) && e.icon != null && !_cache.ContainsKey(e.weaponName))
                    _cache.Add(e.weaponName, e.icon);
            }
        }

        private void OnValidate() => _cache = null;

        /// <summary>
        /// Resolve the best icon for the given weapon. May return <c>null</c> if
        /// neither the registry, the WeaponData, nor <see cref="defaultIcon"/> have
        /// a sprite — callers should guard against null.
        /// </summary>
        public Sprite Get(WeaponData data)
        {
            if (data == null) return defaultIcon;
            if (_cache == null) RebuildCache();
            if (!string.IsNullOrEmpty(data.displayName)
                && _cache != null
                && _cache.TryGetValue(data.displayName, out var sprite)
                && sprite != null)
            {
                return sprite;
            }
            if (data.icon != null) return data.icon;
            return defaultIcon;
        }
    }
}
