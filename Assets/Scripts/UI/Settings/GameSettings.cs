using System;
using UnityEngine;

namespace LoneFighter.UI.Settings
{
    /// <summary>
    /// Strongly-typed, PlayerPrefs-backed game settings.
    /// Values are loaded lazily on first read, persisted on every set, and broadcast
    /// to subscribers via <see cref="Changed"/>. Call <see cref="Apply"/> to push the
    /// current values into Unity engine state (AudioListener, QualitySettings, target FPS).
    /// </summary>
    public static class GameSettings
    {
        // PlayerPrefs keys — namespaced to avoid collisions with any other systems.
        private const string KeyMaster        = "LF.Settings.MasterVolume";
        private const string KeyMusic         = "LF.Settings.MusicVolume";
        private const string KeySfx           = "LF.Settings.SfxVolume";
        private const string KeyHaptics       = "LF.Settings.HapticsEnabled";
        private const string KeyTargetFps     = "LF.Settings.TargetFps";
        private const string KeyQualityLevel  = "LF.Settings.QualityLevel";

        // Defaults — spec defaults.
        private const float DefaultMaster   = 0.8f;
        private const float DefaultMusic    = 0.5f;
        private const float DefaultSfx      = 0.8f;
        private const bool  DefaultHaptics  = true;
        private const int   DefaultFps      = 120;
        private const int   DefaultQuality  = 1;

        // In-memory cache populated lazily so the first getter triggers a Load().
        private static bool  _loaded;
        private static float _master;
        private static float _music;
        private static float _sfx;
        private static bool  _haptics;
        private static int   _targetFps;
        private static int   _qualityLevel;

        /// <summary>Fired after any setter has written through to PlayerPrefs.</summary>
        public static event Action Changed;

        public static float MasterVolume
        {
            get { EnsureLoaded(); return _master; }
            set
            {
                EnsureLoaded();
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, _master)) return;
                _master = clamped;
                PlayerPrefs.SetFloat(KeyMaster, _master);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static float MusicVolume
        {
            get { EnsureLoaded(); return _music; }
            set
            {
                EnsureLoaded();
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, _music)) return;
                _music = clamped;
                PlayerPrefs.SetFloat(KeyMusic, _music);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfx; }
            set
            {
                EnsureLoaded();
                var clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, _sfx)) return;
                _sfx = clamped;
                PlayerPrefs.SetFloat(KeySfx, _sfx);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool HapticsEnabled
        {
            get { EnsureLoaded(); return _haptics; }
            set
            {
                EnsureLoaded();
                if (value == _haptics) return;
                _haptics = value;
                PlayerPrefs.SetInt(KeyHaptics, _haptics ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>60 / 90 / 120 / -1 (uncapped). Other values are sanitized to <see cref="DefaultFps"/>.</summary>
        public static int TargetFps
        {
            get { EnsureLoaded(); return _targetFps; }
            set
            {
                EnsureLoaded();
                var sanitized = SanitizeFps(value);
                if (sanitized == _targetFps) return;
                _targetFps = sanitized;
                PlayerPrefs.SetInt(KeyTargetFps, _targetFps);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>0=Low, 1=Medium, 2=High. Out-of-range values are clamped.</summary>
        public static int QualityLevel
        {
            get { EnsureLoaded(); return _qualityLevel; }
            set
            {
                EnsureLoaded();
                var clamped = Mathf.Clamp(value, 0, 2);
                if (clamped == _qualityLevel) return;
                _qualityLevel = clamped;
                PlayerPrefs.SetInt(KeyQualityLevel, _qualityLevel);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Force-read from PlayerPrefs into the in-memory cache. Safe to call multiple times.</summary>
        public static void Load()
        {
            _master       = PlayerPrefs.GetFloat(KeyMaster, DefaultMaster);
            _music        = PlayerPrefs.GetFloat(KeyMusic, DefaultMusic);
            _sfx          = PlayerPrefs.GetFloat(KeySfx, DefaultSfx);
            _haptics      = PlayerPrefs.GetInt(KeyHaptics, DefaultHaptics ? 1 : 0) != 0;
            _targetFps    = SanitizeFps(PlayerPrefs.GetInt(KeyTargetFps, DefaultFps));
            _qualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(KeyQualityLevel, DefaultQuality), 0, 2);
            _loaded = true;
        }

        /// <summary>
        /// Pushes the current settings into Unity engine state. Idempotent — safe to call
        /// after every change or once at startup.
        /// </summary>
        public static void Apply()
        {
            EnsureLoaded();

            // Master gates the entire audio bus; per-source music/sfx volumes are owned by
            // gameplay code (AudioManager) and not forced here so we don't fight that system.
            AudioListener.volume = _master;

            // QualitySettings — guard against an out-of-range index if the URP project asset
            // has fewer than 3 levels configured.
            var names = QualitySettings.names;
            if (names != null && names.Length > 0)
            {
                var level = Mathf.Clamp(_qualityLevel, 0, names.Length - 1);
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: true);
            }

            // Target frame rate. Unity convention: -1 = uncapped (platform default).
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _targetFps;
        }

        /// <summary>Reset every setting to its default and persist immediately.</summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();
            _master       = DefaultMaster;
            _music        = DefaultMusic;
            _sfx          = DefaultSfx;
            _haptics      = DefaultHaptics;
            _targetFps    = DefaultFps;
            _qualityLevel = DefaultQuality;

            PlayerPrefs.SetFloat(KeyMaster, _master);
            PlayerPrefs.SetFloat(KeyMusic, _music);
            PlayerPrefs.SetFloat(KeySfx, _sfx);
            PlayerPrefs.SetInt(KeyHaptics, _haptics ? 1 : 0);
            PlayerPrefs.SetInt(KeyTargetFps, _targetFps);
            PlayerPrefs.SetInt(KeyQualityLevel, _qualityLevel);
            PlayerPrefs.Save();

            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        private static int SanitizeFps(int fps)
        {
            // Whitelist of valid options; anything else falls back to the default.
            return fps == 60 || fps == 90 || fps == 120 || fps == -1 ? fps : DefaultFps;
        }
    }
}
