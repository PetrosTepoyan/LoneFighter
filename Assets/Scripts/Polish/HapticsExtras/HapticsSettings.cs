using System;
using UnityEngine;

namespace LoneFighter.Polish.HapticsExtras
{
    /// <summary>
    /// PlayerPrefs-backed accessor for the haptic-strength multiplier (0..2).
    /// 0 = effectively muted via downgrade (intensity scalar drops everything to Light, then
    /// the underlying HapticsService skips Light on Android), 1 = play as authored, 2 = max
    /// punch — patterns above the chaos curve receive the full Heavy treatment.
    ///
    /// Deliberately separate from <see cref="LoneFighter.UI.Settings.GameSettings.HapticsEnabled"/>
    /// — the on/off master toggle there is still respected by callers that read it. This is the
    /// per-user strength dial that the new extras stack uses.
    /// </summary>
    public static class HapticsSettings
    {
        /// <summary>PlayerPrefs key. Namespaced to avoid collisions with the existing settings file.</summary>
        public const string KeyStrength = "LF.HapticsExtras.Strength";

        /// <summary>Minimum allowed multiplier (effectively off).</summary>
        public const float MinStrength = 0f;
        /// <summary>Maximum allowed multiplier (double-strength patterns).</summary>
        public const float MaxStrength = 2f;
        /// <summary>Default if the key is missing — neutral, play as authored.</summary>
        public const float DefaultStrength = 1f;

        private static bool _loaded;
        private static float _strength = DefaultStrength;

        /// <summary>Fired any time <see cref="Strength"/> changes via the setter or
        /// <see cref="ResetToDefaults"/>.</summary>
        public static event Action Changed;

        /// <summary>
        /// Global haptic-strength multiplier, clamped to [<see cref="MinStrength"/>,
        /// <see cref="MaxStrength"/>]. Persists immediately on set.
        /// </summary>
        public static float Strength
        {
            get { EnsureLoaded(); return _strength; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp(value, MinStrength, MaxStrength);
                if (Mathf.Approximately(clamped, _strength)) return;
                _strength = clamped;
                PlayerPrefs.SetFloat(KeyStrength, _strength);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// True when the global multiplier would render haptics essentially inaudible / unfelt
        /// (≤ a tiny epsilon). Lets directors short-circuit pattern playback entirely.
        /// </summary>
        public static bool IsEffectivelyMuted
        {
            get { EnsureLoaded(); return _strength <= 0.001f; }
        }

        /// <summary>
        /// Convert <see cref="Strength"/> to a 0..1 scalar suitable for
        /// <see cref="HapticPattern.Play(HapticsService, float)"/>. Values above 1 are clamped
        /// to 1 — they cannot make pulses more intense than Heavy, they just keep everything
        /// at the top of its scale.
        /// </summary>
        public static float AsPatternScalar()
        {
            EnsureLoaded();
            return Mathf.Clamp01(_strength);
        }

        /// <summary>Force-read PlayerPrefs into the in-memory cache. Safe to call multiple times.</summary>
        public static void Load()
        {
            _strength = Mathf.Clamp(PlayerPrefs.GetFloat(KeyStrength, DefaultStrength), MinStrength, MaxStrength);
            _loaded = true;
        }

        /// <summary>Reset to the default multiplier (1.0) and persist.</summary>
        public static void ResetToDefaults()
        {
            EnsureLoaded();
            _strength = DefaultStrength;
            PlayerPrefs.SetFloat(KeyStrength, _strength);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }
    }
}
