using UnityEngine;
using UnityEngine.InputSystem;
using LoneFighter.Enemies;

namespace LoneFighter.Polish
{
    /// Dev overlay (F3 to toggle). Shows smoothed FPS / ms, live enemy count, and a transient counter
    /// you can wire into projectiles / gems / particles via RegisterTransient / UnregisterTransient.
    /// Only active in Debug builds or in Editor.
    public class PerformanceMonitor : MonoBehaviour
    {
        private const int SAMPLE_COUNT = 30;

        private static int _transientCount;
        public static int TransientCount => _transientCount;
        public static void RegisterTransient() { _transientCount++; }
        public static void UnregisterTransient() { _transientCount = Mathf.Max(0, _transientCount - 1); }

        [SerializeField] private bool startVisible = false;
        [SerializeField] private Vector2 anchor = new Vector2(12f, 12f);

        private readonly float[] _samples = new float[SAMPLE_COUNT];
        private int _sampleIndex;
        private int _samplesFilled;
        private bool _visible;
        private GUIStyle _style;
        private bool _canShow;

        private void Awake()
        {
            _canShow = Debug.isDebugBuild || Application.isEditor;
            _visible = startVisible && _canShow;
        }

        private void Update()
        {
            if (!_canShow) return;
            _samples[_sampleIndex] = Time.unscaledDeltaTime;
            _sampleIndex = (_sampleIndex + 1) % SAMPLE_COUNT;
            if (_samplesFilled < SAMPLE_COUNT) _samplesFilled++;

            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_canShow || !_visible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
                {
                    fontSize = 14,
                    richText = true,
                    alignment = TextAnchor.UpperLeft
                };
                _style.normal.textColor = Color.white;
            }

            float avgDt = AverageDelta();
            float fps = avgDt > 0f ? 1f / avgDt : 0f;
            float ms = avgDt * 1000f;
            int enemies = EnemyRegistry.Count;

            string text =
                $"<b>FPS</b> {fps,5:0.0}   <b>ms</b> {ms,5:0.00}\n" +
                $"<b>Enemies</b> {enemies}\n" +
                $"<b>Transient</b> {_transientCount}";

            // Shadow for legibility.
            var shadowRect = new Rect(anchor.x + 1f, anchor.y + 1f, 320f, 80f);
            var rect = new Rect(anchor.x, anchor.y, 320f, 80f);
            var prevColor = _style.normal.textColor;
            _style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(shadowRect, text, _style);
            _style.normal.textColor = prevColor;
            GUI.Label(rect, text, _style);
        }

        private float AverageDelta()
        {
            if (_samplesFilled == 0) return Time.unscaledDeltaTime;
            float sum = 0f;
            for (int i = 0; i < _samplesFilled; i++) sum += _samples[i];
            return sum / _samplesFilled;
        }
    }
}
