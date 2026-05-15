using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// F7-toggled rolling frame-time graph drawn in the top-right corner.
    ///
    /// Records 120 frames of Time.unscaledDeltaTime * 1000 (one screenful at 120Hz) and renders
    /// them as a column chart using GUI.DrawTexture for each bar. Color tiers:
    ///   green  &lt; 8ms
    ///   yellow &lt; 16ms
    ///   red    &gt;= 16ms
    ///
    /// 16ms corresponds to the 60Hz budget; at 120Hz the soft target is 8.33ms, so green is the
    /// 120-fps band, yellow is "still smooth at 60", red is a dropped frame.
    public class FrameTimeGraph : MonoBehaviour
    {
        private const int   SAMPLE_COUNT = 120;
        private const float GRAPH_WIDTH  = 240f;
        private const float GRAPH_HEIGHT = 80f;
        private const float MARGIN       = 12f;
        private const float MAX_MS       = 33.34f; // 30 fps floor for scaling; bars cap visually.

        private readonly float[] _samples = new float[SAMPLE_COUNT];
        private int _writeIndex;
        private int _filled;

        private Texture2D _green;
        private Texture2D _yellow;
        private Texture2D _red;
        private Texture2D _bg;
        private Texture2D _grid;
        private GUIStyle _labelStyle;

        private void OnEnable()
        {
            _writeIndex = 0;
            _filled = 0;
        }

        private void OnDestroy()
        {
            DestroySafe(ref _green);
            DestroySafe(ref _yellow);
            DestroySafe(ref _red);
            DestroySafe(ref _bg);
            DestroySafe(ref _grid);
        }

        private static void DestroySafe(ref Texture2D t)
        {
            if (t != null) Destroy(t);
            t = null;
        }

        private void Update()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.FrameGraph) return;

            _samples[_writeIndex] = Time.unscaledDeltaTime * 1000f;
            _writeIndex = (_writeIndex + 1) % SAMPLE_COUNT;
            if (_filled < SAMPLE_COUNT) _filled++;
        }

        private void OnGUI()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.FrameGraph) return;

            EnsureAssets();

            float x = Screen.width - GRAPH_WIDTH - MARGIN;
            float y = MARGIN;
            var bgRect = new Rect(x, y, GRAPH_WIDTH, GRAPH_HEIGHT);
            GUI.DrawTexture(bgRect, _bg);

            // Reference grid lines at 8ms and 16ms.
            DrawHLine(x, y, GRAPH_WIDTH,  8f);
            DrawHLine(x, y, GRAPH_WIDTH, 16f);

            float barWidth = GRAPH_WIDTH / SAMPLE_COUNT;
            int count = _filled;
            // Render oldest sample on the left, newest on the right.
            for (int i = 0; i < count; i++)
            {
                int idx = (_writeIndex - count + i + SAMPLE_COUNT) % SAMPLE_COUNT;
                float ms = _samples[idx];
                float h = Mathf.Clamp01(ms / MAX_MS) * GRAPH_HEIGHT;
                float bx = x + i * barWidth;
                float by = y + (GRAPH_HEIGHT - h);
                var tex = ms < 8f ? _green : (ms < 16f ? _yellow : _red);
                GUI.DrawTexture(new Rect(bx, by, Mathf.Max(1f, barWidth - 0.5f), h), tex);
            }

            // Label: latest ms / FPS in the corner of the graph.
            float latest = count > 0 ? _samples[(_writeIndex - 1 + SAMPLE_COUNT) % SAMPLE_COUNT] : 0f;
            float fps = latest > 0f ? 1000f / latest : 0f;
            string label = $"{latest:0.0}ms  {fps:0} fps";
            GUI.Label(new Rect(x + 4f, y + 2f, GRAPH_WIDTH - 8f, 16f), label, _labelStyle);
        }

        private void DrawHLine(float x, float y, float width, float ms)
        {
            float ny = y + GRAPH_HEIGHT * (1f - Mathf.Clamp01(ms / MAX_MS));
            GUI.DrawTexture(new Rect(x, ny, width, 1f), _grid);
        }

        private void EnsureAssets()
        {
            if (_green == null)  _green  = SolidTexture(new Color(0.20f, 0.95f, 0.30f, 0.90f));
            if (_yellow == null) _yellow = SolidTexture(new Color(0.98f, 0.85f, 0.20f, 0.90f));
            if (_red == null)    _red    = SolidTexture(new Color(0.98f, 0.25f, 0.25f, 0.95f));
            if (_bg == null)     _bg     = SolidTexture(new Color(0.00f, 0.00f, 0.00f, 0.55f));
            if (_grid == null)   _grid   = SolidTexture(new Color(1.00f, 1.00f, 1.00f, 0.25f));

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
                {
                    fontSize = 11,
                    alignment = TextAnchor.UpperLeft,
                };
                _labelStyle.normal.textColor = Color.white;
            }
        }

        private static Texture2D SolidTexture(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.DontSave;
            return t;
        }
    }
}
