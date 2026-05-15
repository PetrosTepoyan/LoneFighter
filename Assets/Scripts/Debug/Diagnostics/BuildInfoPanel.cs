using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// "Always-on" build identification panel, toggleable separately via F8.
    ///
    /// Bottom-left corner, tiny text. Shows:
    ///   - Application.version + Application.buildGUID (truncated)
    ///   - Unity version
    ///   - Platform (Application.platform)
    ///   - Device model
    ///
    /// The cached label is computed once on enable; it doesn't change across the run, so we
    /// avoid string allocs in OnGUI.
    public class BuildInfoPanel : MonoBehaviour
    {
        private const float PANEL_WIDTH  = 380f;
        private const float PANEL_HEIGHT = 56f;
        private const float MARGIN       = 8f;

        private string _cachedText;
        private GUIStyle _style;
        private GUIStyle _shadowStyle;

        private void OnEnable()
        {
            _cachedText = BuildText();
        }

        private static string BuildText()
        {
            string buildGuid = Application.buildGUID;
            if (!string.IsNullOrEmpty(buildGuid) && buildGuid.Length > 8)
            {
                buildGuid = buildGuid.Substring(0, 8);
            }

            string version    = string.IsNullOrEmpty(Application.version) ? "?" : Application.version;
            string unityVer   = Application.unityVersion;
            string platform   = Application.platform.ToString();
            string deviceMode = SystemInfo.deviceModel;
            string buildTag   = Debug.isDebugBuild ? "DEBUG" : "RELEASE";

            return
                $"v{version} ({buildGuid})  {buildTag}\n" +
                $"Unity {unityVer}  {platform}\n" +
                $"{deviceMode}";
        }

        private void OnGUI()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.BuildInfo) return;
            if (string.IsNullOrEmpty(_cachedText)) _cachedText = BuildText();

            EnsureStyles();

            float x = MARGIN;
            float y = Screen.height - PANEL_HEIGHT - MARGIN;
            var rect       = new Rect(x,        y,        PANEL_WIDTH, PANEL_HEIGHT);
            var shadowRect = new Rect(x + 1f,   y + 1f,   PANEL_WIDTH, PANEL_HEIGHT);

            GUI.Label(shadowRect, _cachedText, _shadowStyle);
            GUI.Label(rect,       _cachedText, _style);
        }

        private void EnsureStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
            {
                fontSize = 10,
                alignment = TextAnchor.LowerLeft,
                richText = false,
            };
            _style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            _shadowStyle = new GUIStyle(_style);
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }
    }
}
