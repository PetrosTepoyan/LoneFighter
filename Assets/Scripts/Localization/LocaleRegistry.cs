using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Localization
{
    /// <summary>
    /// A central catalog of every <see cref="LocalizationData"/> asset shipped with the game.
    ///
    /// One instance lives in <c>Assets/Resources/Localization/LocaleRegistry.asset</c> (so it
    /// can be <see cref="Resources.Load"/>ed without scene wiring). <see cref="LocalizationService"/>
    /// loads it on startup and uses <see cref="Get(string)"/> to pick the active locale.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Localization/Locale Registry", fileName = "LocaleRegistry", order = 1)]
    public class LocaleRegistry : ScriptableObject
    {
        [Tooltip("Every locale shipped with the game. The first entry is treated as the baseline / fallback.")]
        public LocalizationData[] locales = System.Array.Empty<LocalizationData>();

        /// <summary>Convenience: the first non-null locale. By convention this is English.</summary>
        public LocalizationData Default
        {
            get
            {
                if (locales == null) return null;
                for (var i = 0; i < locales.Length; i++)
                {
                    if (locales[i] != null) return locales[i];
                }
                return null;
            }
        }

        /// <summary>
        /// Case-insensitive lookup by locale code. Falls back to exact-match first, then
        /// language prefix (e.g. "pt-br" → match "pt"). Returns <c>null</c> on miss; callers
        /// are expected to fall back to <see cref="Default"/>.
        /// </summary>
        public LocalizationData Get(string localeCode)
        {
            if (locales == null || string.IsNullOrEmpty(localeCode)) return null;

            // Exact (case-insensitive) match first.
            for (var i = 0; i < locales.Length; i++)
            {
                var loc = locales[i];
                if (loc == null || string.IsNullOrEmpty(loc.localeCode)) continue;
                if (string.Equals(loc.localeCode, localeCode, System.StringComparison.OrdinalIgnoreCase))
                    return loc;
            }

            // Language-only fallback: "pt-br" → "pt".
            var dash = localeCode.IndexOf('-');
            if (dash > 0)
            {
                var prefix = localeCode.Substring(0, dash);
                for (var i = 0; i < locales.Length; i++)
                {
                    var loc = locales[i];
                    if (loc == null || string.IsNullOrEmpty(loc.localeCode)) continue;
                    if (string.Equals(loc.localeCode, prefix, System.StringComparison.OrdinalIgnoreCase))
                        return loc;
                }
            }

            return null;
        }

        /// <summary>Enumerates every non-null locale in the registry. Useful for language pickers.</summary>
        public IEnumerable<LocalizationData> All()
        {
            if (locales == null) yield break;
            for (var i = 0; i < locales.Length; i++)
            {
                if (locales[i] != null) yield return locales[i];
            }
        }
    }
}
