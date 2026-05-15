using UnityEngine;

namespace LoneFighter.Localization
{
    /// <summary>
    /// A single locale's key/value table, authored as a ScriptableObject.
    ///
    /// We deliberately use **parallel string arrays** rather than a serialized
    /// <c>Dictionary&lt;,&gt;</c> or list of structs:
    ///  - Survives <see cref="JsonUtility"/> round-trips (dictionaries don't serialize).
    ///  - Plays nicely with Unity's serializer in the inspector.
    ///  - Trivially diff-friendly in version control (one key per line in YAML).
    ///
    /// Lookup is O(n) but n is tiny (hundreds of strings, not thousands) and
    /// <see cref="LocalizationService"/> caches into a dictionary at load time, so the
    /// linear scan here is only paid once when the asset is parsed.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Localization/Locale Data", fileName = "Locale", order = 0)]
    public class LocalizationData : ScriptableObject
    {
        /// <summary>
        /// ISO-639-1 (or BCP-47) locale code. Examples: "en", "es", "fr", "pt-br".
        /// Matched case-insensitively in <see cref="LocaleRegistry.Get"/>.
        /// </summary>
        [Tooltip("Locale code, e.g. \"en\", \"es\", \"pt-br\". Used as the lookup key in the LocaleRegistry.")]
        public string localeCode = "en";

        /// <summary>
        /// Optional human-readable name shown in language pickers (e.g. "English", "Espanol").
        /// </summary>
        [Tooltip("Optional display name shown in language pickers. e.g. \"English\".")]
        public string displayName = "English";

        /// <summary>Parallel array with <see cref="values"/>. Index <c>i</c> in keys maps to index <c>i</c> in values.</summary>
        public string[] keys = System.Array.Empty<string>();

        /// <summary>Parallel array with <see cref="keys"/>. Index <c>i</c> in keys maps to index <c>i</c> in values.</summary>
        [TextArea(1, 4)]
        public string[] values = System.Array.Empty<string>();

        /// <summary>
        /// Linear scan over <see cref="keys"/>. Returns <c>true</c> + the value on hit, otherwise <c>false</c>.
        /// Most call sites should go through <see cref="LocalizationService.Get"/> which caches into a dictionary.
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            if (keys == null || values == null || string.IsNullOrEmpty(key))
            {
                value = null;
                return false;
            }

            var count = Mathf.Min(keys.Length, values.Length);
            for (var i = 0; i < count; i++)
            {
                if (keys[i] == key)
                {
                    value = values[i];
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
