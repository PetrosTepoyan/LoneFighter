using System;
using UnityEngine;

namespace LoneFighter.UI.EnemyHud
{
    /// <summary>
    /// Static, PlayerPrefs-backed toggles for the optional in-world enemy HUD overlay
    /// (small health bars over enemies, plus the elite/boss name labels).
    ///
    /// Intentionally independent of <c>LoneFighter.UI.Settings.GameSettings</c> — this is a
    /// localized cosmetic toggle owned by the EnemyHud package and we do not want to grow
    /// the global settings surface every time a new optional widget ships. The PlayerPrefs
    /// key is namespaced (<c>LF.EnemyHud.*</c>) to avoid collisions with any other system.
    ///
    /// Boss bars are always-on by design and are NOT gated by this flag — see
    /// <see cref="EnemyHealthBarSpawner"/> and <see cref="BossHealthBar"/>.
    /// </summary>
    public static class EnemyHudSettings
    {
        // Namespaced PlayerPrefs keys — keep these stable; they're persisted on user devices.
        private const string KeyShowHealthBars = "LF.EnemyHud.ShowHealthBars";

        // Defaults — spec says off by default to keep the screen calm on first launch.
        private const bool DefaultShowHealthBars = false;

        private static bool _loaded;
        private static bool _showHealthBars;

        /// <summary>Fired after a setter has written through to PlayerPrefs.</summary>
        public static event Action Changed;

        /// <summary>
        /// When true, <see cref="EnemyHealthBarSpawner"/> will attach a small world-space
        /// health bar above every standard enemy. Bosses always show a screen-locked bar
        /// regardless of this setting.
        /// </summary>
        public static bool ShowEnemyHealthBars
        {
            get { EnsureLoaded(); return _showHealthBars; }
            set
            {
                EnsureLoaded();
                if (value == _showHealthBars) return;
                _showHealthBars = value;
                PlayerPrefs.SetInt(KeyShowHealthBars, _showHealthBars ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Force-read from PlayerPrefs. Safe to call multiple times.</summary>
        public static void Load()
        {
            _showHealthBars = PlayerPrefs.GetInt(KeyShowHealthBars, DefaultShowHealthBars ? 1 : 0) != 0;
            _loaded = true;
        }

        /// <summary>Reset all EnemyHud toggles to their defaults and persist immediately.</summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();
            _showHealthBars = DefaultShowHealthBars;
            PlayerPrefs.SetInt(KeyShowHealthBars, _showHealthBars ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }
    }
}
