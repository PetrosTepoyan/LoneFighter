using System;
using UnityEngine;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// Central registry of cheat flags. Singleton. Each flag is a public bool with a
    /// Changed event so UI and enforcement layers can react. All writes are gated by
    /// <see cref="BuildGuard"/>: in a non-development build the setter is a no-op and
    /// the value stays false.
    /// </summary>
    public class CheatService
    {
        private static CheatService _instance;

        public static CheatService Instance
        {
            get
            {
                if (_instance == null) _instance = new CheatService();
                return _instance;
            }
        }

        // --- flags ----------------------------------------------------------

        private bool _godMode;
        private bool _oneHitKills;
        private bool _infiniteXp;
        private bool _noSpawn;
        private bool _autoFire;
        private bool _freezeEnemies;
        private bool _instantCooldowns;
        private bool _bigDamage;

        public bool GodMode
        {
            get => _godMode;
            set => Assign(ref _godMode, value, GodModeChanged);
        }

        public bool OneHitKills
        {
            get => _oneHitKills;
            set => Assign(ref _oneHitKills, value, OneHitKillsChanged);
        }

        public bool InfiniteXp
        {
            get => _infiniteXp;
            set => Assign(ref _infiniteXp, value, InfiniteXpChanged);
        }

        public bool NoSpawn
        {
            get => _noSpawn;
            set => Assign(ref _noSpawn, value, NoSpawnChanged);
        }

        /// <summary>
        /// AutoFire is a no-op marker because weapons in this project already auto-fire.
        /// Kept in the API so UI can show the toggle and so future hand-firing weapons
        /// can subscribe.
        /// </summary>
        public bool AutoFire
        {
            get => _autoFire;
            set => Assign(ref _autoFire, value, AutoFireChanged);
        }

        public bool FreezeEnemies
        {
            get => _freezeEnemies;
            set => Assign(ref _freezeEnemies, value, FreezeEnemiesChanged);
        }

        public bool InstantCooldowns
        {
            get => _instantCooldowns;
            set => Assign(ref _instantCooldowns, value, InstantCooldownsChanged);
        }

        public bool BigDamage
        {
            get => _bigDamage;
            set => Assign(ref _bigDamage, value, BigDamageChanged);
        }

        // --- events ---------------------------------------------------------

        public event Action<bool> GodModeChanged;
        public event Action<bool> OneHitKillsChanged;
        public event Action<bool> InfiniteXpChanged;
        public event Action<bool> NoSpawnChanged;
        public event Action<bool> AutoFireChanged;
        public event Action<bool> FreezeEnemiesChanged;
        public event Action<bool> InstantCooldownsChanged;
        public event Action<bool> BigDamageChanged;

        /// <summary>Fired after any flag changes. Useful for persistence.</summary>
        public event Action AnyChanged;

        // --- constants ------------------------------------------------------

        public const float BigDamageMultiplier = 1000f;
        public const float OneHitDamage = 99999f;
        public const float InstantCooldownSeconds = 0.05f;

        // --- internals ------------------------------------------------------

        private CheatService() { }

        private void Assign(ref bool field, bool value, Action<bool> evt)
        {
            // Enforce the build guard. In release builds, force every flag false.
            if (value && !BuildGuard.AssertAllowed()) value = false;
            if (field == value) return;
            field = value;
            try { evt?.Invoke(value); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
            try { AnyChanged?.Invoke(); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        /// <summary>
        /// Clears every flag. Used when the build guard refuses cheats.
        /// </summary>
        public void DisableAll()
        {
            GodMode = false;
            OneHitKills = false;
            InfiniteXp = false;
            NoSpawn = false;
            AutoFire = false;
            FreezeEnemies = false;
            InstantCooldowns = false;
            BigDamage = false;
        }
    }
}
