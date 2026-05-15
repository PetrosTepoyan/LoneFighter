using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Localization
{
    /// <summary>
    /// Runtime entry point for string lookups. Singleton initialized either lazily on first
    /// access or eagerly via <see cref="BootstrapBeforeSceneLoad"/>.
    ///
    /// Locale selection order (highest priority first):
    ///   1. <c>PlayerPrefs.GetString("LF.Locale")</c> — user override from settings UI.
    ///   2. <see cref="Application.systemLanguage"/> mapped to an ISO code.
    ///   3. The registry's <see cref="LocaleRegistry.Default"/> (typically English).
    ///
    /// All lookups go through a dictionary cache rebuilt whenever the locale changes, so
    /// per-frame <see cref="Get"/> calls are O(1).
    /// </summary>
    public class LocalizationService
    {
        /// <summary>PlayerPrefs key for the user-selected locale override.</summary>
        public const string PrefsLocaleKey = "LF.Locale";

        /// <summary>Path under <c>Resources/</c> where the registry asset lives.</summary>
        public const string RegistryResourcePath = "Localization/LocaleRegistry";

        private static LocalizationService _instance;

        /// <summary>Singleton accessor. Initializes on first use; safe to call from any thread that is also safe to call <see cref="Resources.Load"/> from (i.e. main thread).</summary>
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null) _instance = new LocalizationService();
                return _instance;
            }
        }

        /// <summary>Fired after <see cref="SetLocale"/> swaps the active locale. Subscribers (e.g. <see cref="LocalizedText"/>) re-pull their strings.</summary>
        public event Action OnLocaleChanged;

        /// <summary>Currently active locale code (e.g. "en"). Empty if no locale loaded.</summary>
        public string CurrentLocaleCode => _active != null ? _active.localeCode : string.Empty;

        /// <summary>The active <see cref="LocalizationData"/> SO. Useful for debug overlays.</summary>
        public LocalizationData CurrentLocale => _active;

        /// <summary>The loaded registry. May be <c>null</c> if no <c>LocaleRegistry.asset</c> shipped under <c>Resources/Localization/</c>.</summary>
        public LocaleRegistry Registry => _registry;

        private LocaleRegistry _registry;
        private LocalizationData _active;
        private LocalizationData _fallback;
        private readonly Dictionary<string, string> _cache = new Dictionary<string, string>(256);
        private readonly Dictionary<string, string> _fallbackCache = new Dictionary<string, string>(256);

        private LocalizationService()
        {
            LoadRegistry();
            ChooseInitialLocale();
        }

        /// <summary>
        /// Runs before the first scene loads so the very first frame already has localized
        /// strings available. Mirrors the pattern used by <c>SettingsBootstrap</c>.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BootstrapBeforeSceneLoad()
        {
            // Touch the singleton so the registry is loaded and the active locale picked
            // before any LocalizedText.OnEnable fires.
            var _ = Instance;
        }

        /// <summary>
        /// Resolves <paramref name="key"/> to a localized string.
        ///  - If the active locale has the key, returns its value.
        ///  - Else if the fallback locale has the key, returns that.
        ///  - Else returns <paramref name="fallback"/> when non-null, otherwise the key itself.
        ///
        /// Never returns <c>null</c>.
        /// </summary>
        public string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

            if (_cache.TryGetValue(key, out var value)) return value;
            if (_fallbackCache.TryGetValue(key, out var fb)) return fb;

            return fallback ?? key;
        }

        /// <summary>
        /// Switches to a new locale. Persists the choice in <see cref="PrefsLocaleKey"/>,
        /// rebuilds the lookup cache, and fires <see cref="OnLocaleChanged"/>. No-op if the
        /// requested code is already active.
        /// </summary>
        public void SetLocale(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            if (_active != null && string.Equals(_active.localeCode, code, StringComparison.OrdinalIgnoreCase))
                return;

            var picked = _registry != null ? _registry.Get(code) : null;
            if (picked == null)
            {
                Debug.LogWarning($"[LocalizationService] No locale registered for '{code}'. Keeping '{CurrentLocaleCode}'.");
                return;
            }

            _active = picked;
            RebuildCache(_cache, _active);

            PlayerPrefs.SetString(PrefsLocaleKey, code);
            PlayerPrefs.Save();

            OnLocaleChanged?.Invoke();
        }

        /// <summary>
        /// Force a reload of the registry from <c>Resources/</c> — useful in the editor
        /// after regenerating the English baseline.
        /// </summary>
        public void ReloadRegistry()
        {
            LoadRegistry();
            ChooseInitialLocale();
            OnLocaleChanged?.Invoke();
        }

        private void LoadRegistry()
        {
            _registry = Resources.Load<LocaleRegistry>(RegistryResourcePath);
            if (_registry == null)
            {
                Debug.LogWarning(
                    $"[LocalizationService] LocaleRegistry not found at Resources/{RegistryResourcePath}. " +
                    "Run LoneFighter -> Localization -> Generate English Baseline to create one. " +
                    "Lookups will fall through to the provided fallback string.");
            }
        }

        private void ChooseInitialLocale()
        {
            _cache.Clear();
            _fallbackCache.Clear();
            _fallback = _registry != null ? _registry.Default : null;
            _active = null;

            if (_registry == null) return;

            // 1) PlayerPrefs override.
            var pref = PlayerPrefs.GetString(PrefsLocaleKey, string.Empty);
            if (!string.IsNullOrEmpty(pref)) _active = _registry.Get(pref);

            // 2) System language mapped to ISO.
            if (_active == null)
            {
                var sys = MapSystemLanguage(Application.systemLanguage);
                _active = _registry.Get(sys);
            }

            // 3) Default (typically English).
            if (_active == null) _active = _fallback;

            if (_fallback != null && _fallback != _active)
                RebuildCache(_fallbackCache, _fallback);
            if (_active != null)
                RebuildCache(_cache, _active);
        }

        private static void RebuildCache(Dictionary<string, string> target, LocalizationData data)
        {
            target.Clear();
            if (data == null || data.keys == null || data.values == null) return;
            var count = Mathf.Min(data.keys.Length, data.values.Length);
            for (var i = 0; i < count; i++)
            {
                var k = data.keys[i];
                if (string.IsNullOrEmpty(k)) continue;
                target[k] = data.values[i] ?? string.Empty;
            }
        }

        /// <summary>
        /// Maps Unity's <see cref="SystemLanguage"/> enum to a short ISO-639-1 code we use as
        /// a locale identifier. Returns "en" for anything we don't explicitly support — that
        /// will fall through to the language-prefix matcher in <see cref="LocaleRegistry.Get"/>.
        /// </summary>
        public static string MapSystemLanguage(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.English:               return "en";
                case SystemLanguage.Spanish:               return "es";
                case SystemLanguage.French:                return "fr";
                case SystemLanguage.German:                return "de";
                case SystemLanguage.Italian:               return "it";
                case SystemLanguage.Portuguese:            return "pt";
                case SystemLanguage.Dutch:                 return "nl";
                case SystemLanguage.Russian:               return "ru";
                case SystemLanguage.Polish:                return "pl";
                case SystemLanguage.Japanese:              return "ja";
                case SystemLanguage.Korean:                return "ko";
                case SystemLanguage.ChineseSimplified:     return "zh-hans";
                case SystemLanguage.ChineseTraditional:    return "zh-hant";
                case SystemLanguage.Chinese:               return "zh";
                case SystemLanguage.Turkish:               return "tr";
                case SystemLanguage.Arabic:                return "ar";
                case SystemLanguage.Indonesian:            return "id";
                case SystemLanguage.Thai:                  return "th";
                case SystemLanguage.Vietnamese:            return "vi";
                case SystemLanguage.Swedish:               return "sv";
                case SystemLanguage.Norwegian:             return "no";
                case SystemLanguage.Danish:                return "da";
                case SystemLanguage.Finnish:               return "fi";
                case SystemLanguage.Czech:                 return "cs";
                case SystemLanguage.Hungarian:             return "hu";
                case SystemLanguage.Greek:                 return "el";
                case SystemLanguage.Hebrew:                return "he";
                case SystemLanguage.Ukrainian:             return "uk";
                case SystemLanguage.Romanian:              return "ro";
                default:                                   return "en";
            }
        }
    }
}
