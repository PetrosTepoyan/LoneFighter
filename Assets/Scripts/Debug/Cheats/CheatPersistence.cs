using UnityEngine;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// Mirrors <see cref="CheatService"/> state to PlayerPrefs so cheats survive
    /// scene loads and editor restarts. Persistence is a no-op when the build guard
    /// refuses cheats — release players can never have cheat keys written.
    /// </summary>
    [DefaultExecutionOrder(9999)]
    public class CheatPersistence : MonoBehaviour
    {
        private const string Prefix = "lf.cheat.";
        private const string KGodMode          = Prefix + "godMode";
        private const string KOneHitKills      = Prefix + "oneHitKills";
        private const string KInfiniteXp       = Prefix + "infiniteXp";
        private const string KNoSpawn          = Prefix + "noSpawn";
        private const string KAutoFire         = Prefix + "autoFire";
        private const string KFreezeEnemies    = Prefix + "freezeEnemies";
        private const string KInstantCooldowns = Prefix + "instantCooldowns";
        private const string KBigDamage        = Prefix + "bigDamage";

        private bool _suppressSave;

        private void Awake()
        {
            if (!BuildGuard.CheatsAllowed) return;
            Load();
            CheatService.Instance.AnyChanged += HandleAnyChanged;
        }

        private void OnDestroy()
        {
            if (!BuildGuard.CheatsAllowed) return;
            CheatService.Instance.AnyChanged -= HandleAnyChanged;
        }

        private void HandleAnyChanged()
        {
            if (_suppressSave) return;
            Save();
        }

        /// <summary>
        /// Loads cheat state from PlayerPrefs into <see cref="CheatService"/>.
        /// Suppresses re-saving while loading so we don't write back what we read.
        /// </summary>
        public void Load()
        {
            if (!BuildGuard.CheatsAllowed) return;
            _suppressSave = true;
            try
            {
                var svc = CheatService.Instance;
                svc.GodMode          = GetBool(KGodMode);
                svc.OneHitKills      = GetBool(KOneHitKills);
                svc.InfiniteXp       = GetBool(KInfiniteXp);
                svc.NoSpawn          = GetBool(KNoSpawn);
                svc.AutoFire         = GetBool(KAutoFire);
                svc.FreezeEnemies    = GetBool(KFreezeEnemies);
                svc.InstantCooldowns = GetBool(KInstantCooldowns);
                svc.BigDamage        = GetBool(KBigDamage);
            }
            finally
            {
                _suppressSave = false;
            }
        }

        /// <summary>
        /// Writes current cheat state to PlayerPrefs. Safe to call from any flag-change
        /// event; PlayerPrefs.Save is called at the end.
        /// </summary>
        public void Save()
        {
            if (!BuildGuard.CheatsAllowed) return;
            var svc = CheatService.Instance;
            SetBool(KGodMode,          svc.GodMode);
            SetBool(KOneHitKills,      svc.OneHitKills);
            SetBool(KInfiniteXp,       svc.InfiniteXp);
            SetBool(KNoSpawn,          svc.NoSpawn);
            SetBool(KAutoFire,         svc.AutoFire);
            SetBool(KFreezeEnemies,    svc.FreezeEnemies);
            SetBool(KInstantCooldowns, svc.InstantCooldowns);
            SetBool(KBigDamage,        svc.BigDamage);
            PlayerPrefs.Save();
        }

        /// <summary>Removes every persisted cheat key.</summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(KGodMode);
            PlayerPrefs.DeleteKey(KOneHitKills);
            PlayerPrefs.DeleteKey(KInfiniteXp);
            PlayerPrefs.DeleteKey(KNoSpawn);
            PlayerPrefs.DeleteKey(KAutoFire);
            PlayerPrefs.DeleteKey(KFreezeEnemies);
            PlayerPrefs.DeleteKey(KInstantCooldowns);
            PlayerPrefs.DeleteKey(KBigDamage);
            PlayerPrefs.Save();
        }

        private static bool GetBool(string key) => PlayerPrefs.GetInt(key, 0) != 0;
        private static void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
    }
}
