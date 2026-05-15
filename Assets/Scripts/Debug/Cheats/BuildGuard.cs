using UnityEngine;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// Gatekeeper for cheat enablement. Cheats are only allowed when running in the
    /// Unity editor or in a development build. In any other configuration the system
    /// hard-disables itself and logs a critical warning.
    /// </summary>
    public static class BuildGuard
    {
        private static bool _checked;
        private static bool _allowed;
        private static bool _warned;

        /// <summary>
        /// True when cheats may be activated (editor or development build).
        /// </summary>
        public static bool CheatsAllowed
        {
            get
            {
                if (!_checked)
                {
                    _allowed = UnityEngine.Debug.isDebugBuild || Application.isEditor;
                    _checked = true;
                }
                return _allowed;
            }
        }

        /// <summary>
        /// Assertion-style guard. Returns true when cheats are allowed; on the first
        /// violation it logs a critical error so the cheat system can self-disable.
        /// </summary>
        public static bool AssertAllowed()
        {
            if (CheatsAllowed) return true;
            if (!_warned)
            {
                _warned = true;
                UnityEngine.Debug.LogError(
                    "[CRITICAL] Cheat system attempted to activate in a non-development build. " +
                    "Self-disabling. This must never ship to players.");
            }
            return false;
        }

        /// <summary>
        /// Resets the cached check. Intended for tests only.
        /// </summary>
        internal static void _ResetForTests()
        {
            _checked = false;
            _allowed = false;
            _warned = false;
        }
    }
}
