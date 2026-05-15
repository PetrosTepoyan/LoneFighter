using UnityEngine;

namespace LoneFighter.UI.Settings
{
    /// <summary>
    /// Ensures <see cref="GameSettings"/> is loaded and applied as early as possible so the
    /// very first frame of the very first scene uses the player's saved preferences.
    ///
    /// Two activation paths — both safe to use together:
    ///  1. <see cref="RuntimeInitializeOnLoadMethod"/> runs before any scene component
    ///     wakes up. This is the preferred path; no GameObject required.
    ///  2. Drop this component on a persistent GameObject (e.g. on the Bootstrap scene
    ///     or alongside <c>GameManager</c>) — it'll re-apply on <c>Awake</c>. This is a
    ///     safety net for editor "Play from current scene" flows where the static init
    ///     may not run.
    /// </summary>
    public class SettingsBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            GameSettings.Load();
            GameSettings.Apply();
        }

        private void Awake()
        {
            // Belt-and-braces in case the static init was stripped (very rare) or skipped.
            GameSettings.Load();
            GameSettings.Apply();
        }
    }
}
