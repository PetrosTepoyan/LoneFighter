using System;
using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// F6-toggled IMGUI overlay showing process memory and GC behavior.
    ///
    /// Fields shown:
    ///   - Total managed memory: System.GC.GetTotalMemory(false) / 1024 / 1024 MB
    ///   - GC generation counts: GC.CollectionCount(0..2)
    ///   - Allocation rate: bytes allocated / second, measured over a 1s sliding window
    ///
    /// The allocation-rate sampling intentionally avoids any per-frame GC.GetTotalAllocatedBytes
    /// call on Mono (which is expensive); we sample once per second and compute the delta.
    public class MemoryOverlay : MonoBehaviour
    {
        private const float SAMPLE_INTERVAL = 1.0f;
        private const float PANEL_WIDTH     = 280f;
        private const float PANEL_HEIGHT    = 88f;
        private const float MARGIN          = 12f;

        private GUIStyle _style;
        private GUIStyle _bgStyle;
        private Texture2D _bgTex;

        private long _lastTotalBytes;
        private float _lastSampleTime;
        private float _allocBytesPerSec;
        private bool _seeded;

        private void OnDestroy()
        {
            if (_bgTex != null) Destroy(_bgTex);
        }

        private void OnEnable()
        {
            _seeded = false;
            _allocBytesPerSec = 0f;
        }

        private void OnGUI()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.MemoryOverlay) return;

            EnsureStyles();
            UpdateAllocationRate();

            // Anchor: top-right, just below where FrameTimeGraph lives.
            float x = Screen.width - PANEL_WIDTH - MARGIN;
            float y = MARGIN + 96f; // sits under the frame graph
            var panel = new Rect(x, y, PANEL_WIDTH, PANEL_HEIGHT);
            GUI.Box(panel, GUIContent.none, _bgStyle);

            long totalBytes = GC.GetTotalMemory(false);
            float mb = totalBytes / 1024f / 1024f;
            int g0 = GC.CollectionCount(0);
            int g1 = GC.CollectionCount(1);
            int g2 = GC.CollectionCount(2);

            string rate = FormatRate(_allocBytesPerSec);

            string text =
                $"<b>Memory (F6)</b>\n" +
                $"Managed: {mb,6:0.00} MB\n" +
                $"GC gens: {g0}/{g1}/{g2}\n" +
                $"Alloc:   {rate}/s";

            GUI.Label(new Rect(x + 8f, y + 4f, PANEL_WIDTH - 16f, PANEL_HEIGHT - 8f), text, _style);
        }

        private void UpdateAllocationRate()
        {
            long now = GC.GetTotalMemory(false);
            float t = Time.unscaledTime;

            if (!_seeded)
            {
                _lastTotalBytes = now;
                _lastSampleTime = t;
                _seeded = true;
                return;
            }

            float elapsed = t - _lastSampleTime;
            if (elapsed < SAMPLE_INTERVAL) return;

            long delta = now - _lastTotalBytes;
            // GC may have collected between samples, dropping the total. Clamp at 0 so we don't
            // surface negative "allocation rate" numbers; a future enhancement could track
            // GC.GetTotalAllocatedBytes for a strictly-monotonic counter when available.
            if (delta < 0) delta = 0;
            _allocBytesPerSec = delta / Mathf.Max(0.001f, elapsed);
            _lastTotalBytes = now;
            _lastSampleTime = t;
        }

        private static string FormatRate(float bytesPerSec)
        {
            if (bytesPerSec < 1024f)            return $"{bytesPerSec,7:0} B";
            if (bytesPerSec < 1024f * 1024f)    return $"{bytesPerSec / 1024f,7:0.0} KB";
            return $"{bytesPerSec / 1024f / 1024f,7:0.00} MB";
        }

        private void EnsureStyles()
        {
            if (_style != null) return;

            _style = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
            {
                fontSize = 12,
                richText = true,
                alignment = TextAnchor.UpperLeft,
            };
            _style.normal.textColor = Color.white;

            _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            _bgTex.Apply();
            _bgTex.hideFlags = HideFlags.DontSave;

            _bgStyle = new GUIStyle(GUI.skin != null ? GUI.skin.box : new GUIStyle());
            _bgStyle.normal.background = _bgTex;
        }
    }
}
