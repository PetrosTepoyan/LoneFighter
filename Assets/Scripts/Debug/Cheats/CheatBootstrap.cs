using UnityEngine;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// Auto-creates a DontDestroyOnLoad GameObject hosting the cheat infrastructure
    /// (CheatHooks + CheatUi + CheatPersistence) when the game boots. Skipped entirely
    /// in non-development builds — <see cref="BuildGuard"/> short-circuits everything
    /// downstream, but we also avoid creating the GameObject in the first place.
    ///
    /// Using <c>[RuntimeInitializeOnLoadMethod]</c> means no scene needs to be edited:
    /// the cheat system attaches itself wherever the game runs.
    /// </summary>
    public static class CheatBootstrap
    {
        private static GameObject _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!BuildGuard.CheatsAllowed)
            {
                // Defensive: force every flag false in case PlayerPrefs from a dev build
                // somehow survived into a release build through a borrowed device.
                CheatService.Instance.DisableAll();
                CheatPersistence.ClearAll();
                return;
            }

            if (_host != null) return;
            _host = new GameObject("[Cheats]");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<CheatPersistence>();
            _host.AddComponent<CheatHooks>();
            _host.AddComponent<CheatUi>();
        }
    }
}
