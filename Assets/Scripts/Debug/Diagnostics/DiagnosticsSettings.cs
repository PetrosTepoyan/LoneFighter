using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// PlayerPrefs-backed visibility toggles for every diagnostic overlay.
    ///
    /// Defaults differ between Editor (overlays default on) and Release (default off),
    /// but the PlayerPrefs value always wins once one has been written for the key.
    /// This lets a tester flip overlays on a device, ship a build, and persist the
    /// preference across launches without rebuilding.
    public static class DiagnosticsSettings
    {
        public const string PrefPool       = "LF.Diag.PoolOverlay";
        public const string PrefAiGizmos   = "LF.Diag.AiGizmos";
        public const string PrefMemory     = "LF.Diag.MemoryOverlay";
        public const string PrefFrameGraph = "LF.Diag.FrameGraph";
        public const string PrefBuildInfo  = "LF.Diag.BuildInfo";

        public static bool PoolOverlay
        {
            get => GetBool(PrefPool, DefaultOn);
            set => SetBool(PrefPool, value);
        }

        public static bool AiGizmos
        {
            get => GetBool(PrefAiGizmos, DefaultOn);
            set => SetBool(PrefAiGizmos, value);
        }

        public static bool MemoryOverlay
        {
            get => GetBool(PrefMemory, DefaultOn);
            set => SetBool(PrefMemory, value);
        }

        public static bool FrameGraph
        {
            get => GetBool(PrefFrameGraph, DefaultOn);
            set => SetBool(PrefFrameGraph, value);
        }

        public static bool BuildInfo
        {
            // BuildInfo is always-on by spec; the F8 toggle still flips this pref.
            get => GetBool(PrefBuildInfo, true);
            set => SetBool(PrefBuildInfo, value);
        }

        /// True when diagnostics are permitted to render at all. Mirrors the spec's gate:
        /// debug build or running in the Editor.
        public static bool DiagnosticsAllowed =>
#if UNITY_EDITOR
            true;
#else
            UnityEngine.Debug.isDebugBuild || Application.isEditor;
#endif

        private static bool DefaultOn => Application.isEditor;

        private static bool GetBool(string key, bool defaultValue)
            => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
