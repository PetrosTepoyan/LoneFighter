using UnityEngine;
using UnityEngine.InputSystem;

namespace LoneFighter.Debugging.Console
{
    /// <summary>
    /// In-game developer console (IMGUI). Active only in debug builds and the Editor. Renders a
    /// dark, monospace overlay across the top 40% of the screen showing the captured log buffer
    /// plus a single-line input field at the bottom. Toggle with the keyboard backquote (~) key,
    /// or with a simultaneous four-finger touch on mobile.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DevConsole : MonoBehaviour
    {
        private const string InputControlName = "DevConsoleInput";
        private const int    MaxVisibleLines  = 200;

        // ---------------------------------------------------------------------
        // Singleton plumbing
        // ---------------------------------------------------------------------
        public static DevConsole Instance { get; private set; }

        /// <summary>Lazy auto-bootstrap: spawn a DevConsole the first time a scene loads in a debug build.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (!(Debug.isDebugBuild || Application.isEditor)) return;
            if (Instance != null) return;
            var go = new GameObject("[DevConsole]");
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<DevConsole>();
        }

        // ---------------------------------------------------------------------
        // State
        // ---------------------------------------------------------------------
        private readonly ConsoleLogCapture _capture = new();
        private readonly ConsoleHistory    _history = new();
        private readonly ConsoleLogCapture.Entry[] _drawBuffer = new ConsoleLogCapture.Entry[MaxVisibleLines];
        private int _drawCount;

        private bool   _visible;
        private bool   _canRun;
        private bool   _focusInputNextLayout;
        private string _input = string.Empty;
        private Vector2 _scroll;

        // 4-finger toggle latch — guarantees a single open/close per gesture.
        private bool _fourFingerLatch;

        // Cached GUI resources (built on first OnGUI).
        private GUIStyle _logStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _hintStyle;
        private Texture2D _panelTex;
        private Font     _monoFont;

        public bool IsVisible => _visible;
        public ConsoleLogCapture LogCapture => _capture;

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _canRun = Debug.isDebugBuild || Application.isEditor;
            if (!_canRun)
            {
                enabled = false;
                return;
            }

            _capture.Attach();
            _capture.Push("LoneFighter dev console ready. Type 'help' for commands.", LogType.Log);
        }

        private void OnDestroy()
        {
            _capture.Detach();
            if (_panelTex != null) Destroy(_panelTex);
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------------
        // Input handling
        // ---------------------------------------------------------------------
        private void Update()
        {
            if (!_canRun) return;

            // Tilde / backquote toggle (Input System).
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            // Four-finger simultaneous touch toggle on mobile / touchscreen.
            var ts = Touchscreen.current;
            if (ts != null)
            {
                int active = 0;
                var touches = ts.touches;
                int n = touches.Count;
                for (int i = 0; i < n; i++)
                {
                    if (touches[i].press.isPressed) active++;
                }
                bool fourDown = active >= 4;
                if (fourDown && !_fourFingerLatch)
                {
                    _fourFingerLatch = true;
                    Toggle();
                }
                else if (!fourDown && active == 0)
                {
                    _fourFingerLatch = false;
                }
            }
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible) _focusInputNextLayout = true;
        }

        public void Show() { if (!_visible) Toggle(); }
        public void Hide() { if (_visible) Toggle(); }

        // ---------------------------------------------------------------------
        // Public API for command handlers
        // ---------------------------------------------------------------------
        public void ClearLog() => _capture.Clear();

        public void PrintLine(string line, LogType type = LogType.Log) => _capture.Push(line, type);

        // ---------------------------------------------------------------------
        // IMGUI
        // ---------------------------------------------------------------------
        private void OnGUI()
        {
            if (!_canRun || !_visible) return;

            EnsureStyles();

            // Top 40% of the screen.
            float panelHeight = Screen.height * 0.4f;
            var panelRect = new Rect(0f, 0f, Screen.width, panelHeight);
            GUI.depth = -1000;

            // Background.
            GUI.DrawTexture(panelRect, _panelTex, ScaleMode.StretchToFill);

            float padding = 8f;
            float inputHeight = 28f;
            float hintHeight = 18f;
            float logHeight  = panelHeight - inputHeight - hintHeight - padding * 3f;

            var hintRect  = new Rect(padding, padding, panelRect.width - padding * 2f, hintHeight);
            var logRect   = new Rect(padding, hintRect.yMax + padding, panelRect.width - padding * 2f, logHeight);
            var inputRect = new Rect(padding, logRect.yMax + padding, panelRect.width - padding * 2f, inputHeight);

            // Header / hint line.
            GUI.Label(hintRect,
                "<b>DevConsole</b>  ~ to toggle  Tab=autocomplete  Up/Down=history  Enter=submit  Esc=close",
                _hintStyle);

            // Log view.
            DrawLog(logRect);

            // Input row.
            HandleInputKeys(inputRect);

            GUI.SetNextControlName(InputControlName);
            _input = GUI.TextField(inputRect, _input ?? string.Empty, _inputStyle);

            if (_focusInputNextLayout && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(InputControlName);
                _focusInputNextLayout = false;
            }
        }

        private void DrawLog(Rect rect)
        {
            _drawCount = _capture.CopyRecent(_drawBuffer, _drawBuffer.Length);

            // Build content rect tall enough for all visible lines.
            float lineHeight = _logStyle.lineHeight > 0f ? _logStyle.lineHeight : 16f;
            float contentHeight = Mathf.Max(rect.height, _drawCount * lineHeight + 8f);
            var viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);

            // Auto-scroll to bottom whenever new content arrives.
            _scroll.y = Mathf.Max(0f, contentHeight - rect.height);

            _scroll = GUI.BeginScrollView(rect, _scroll, viewRect, false, true);
            float y = 0f;
            for (int i = 0; i < _drawCount; i++)
            {
                var entry = _drawBuffer[i];
                var line = new Rect(0f, y, viewRect.width, lineHeight);
                GUI.Label(line, entry.Rich, _logStyle);
                y += lineHeight;
            }
            GUI.EndScrollView();
        }

        private void HandleInputKeys(Rect inputRect)
        {
            // Operate only on KeyDown events while our text field is focused — or while the
            // console is visible at all (we steal Tab/Enter so the OS field doesn't eat them).
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SubmitCurrentInput();
                    e.Use();
                    break;

                case KeyCode.Escape:
                    Hide();
                    e.Use();
                    break;

                case KeyCode.UpArrow:
                    _input = _history.MoveUp(_input ?? string.Empty);
                    _focusInputNextLayout = true;
                    e.Use();
                    break;

                case KeyCode.DownArrow:
                    _input = _history.MoveDown(_input ?? string.Empty);
                    _focusInputNextLayout = true;
                    e.Use();
                    break;

                case KeyCode.Tab:
                    {
                        string completed = ConsoleAutoComplete.Complete(_input ?? string.Empty, out var matches);
                        if (matches != null && matches.Count > 1)
                        {
                            _capture.Push("Matches: " + string.Join(", ", matches), LogType.Log);
                        }
                        _input = completed;
                        _focusInputNextLayout = true;
                        e.Use();
                    }
                    break;

                case KeyCode.BackQuote:
                    // Eat the backquote so it doesn't get typed into the input field.
                    // The actual toggle is handled in Update() against Keyboard.current.
                    e.Use();
                    break;
            }
        }

        private void SubmitCurrentInput()
        {
            string line = _input ?? string.Empty;
            _input = string.Empty;
            if (string.IsNullOrWhiteSpace(line)) { _focusInputNextLayout = true; return; }

            _capture.Push("> " + line, LogType.Log);
            _history.Add(line);

            string response = ConsoleCommandRegistry.Execute(line);
            if (!string.IsNullOrEmpty(response))
            {
                _capture.Push(response, LogType.Log);
            }
            _focusInputNextLayout = true;
        }

        // ---------------------------------------------------------------------
        // GUI setup
        // ---------------------------------------------------------------------
        private void EnsureStyles()
        {
            if (_panelTex == null)
            {
                _panelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _panelTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.07f, 0.92f));
                _panelTex.Apply();
                _panelTex.hideFlags = HideFlags.HideAndDontSave;
            }

            if (_monoFont == null)
            {
                // Built-in monospace fallback chain. CourierNew exists on most platforms; if the
                // OS lookup fails we fall back to Unity's default GUI font.
                _monoFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Menlo", "Consolas", "Courier New", "Liberation Mono", "Monaco" }, 14);
            }

            if (_logStyle == null)
            {
                _logStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
                {
                    fontSize = 13,
                    richText = true,
                    wordWrap = false,
                    alignment = TextAnchor.UpperLeft,
                    clipping = TextClipping.Clip,
                };
                if (_monoFont != null) _logStyle.font = _monoFont;
                _logStyle.normal.textColor = new Color(0.9f, 0.95f, 0.95f, 1f);
            }

            if (_inputStyle == null)
            {
                _inputStyle = new GUIStyle(GUI.skin != null ? GUI.skin.textField : new GUIStyle())
                {
                    fontSize = 14,
                    richText = false,
                    alignment = TextAnchor.MiddleLeft,
                };
                if (_monoFont != null) _inputStyle.font = _monoFont;
                _inputStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
                _inputStyle.focused.textColor = new Color(1f, 1f, 1f, 1f);
            }

            if (_panelStyle == null)
            {
                _panelStyle = new GUIStyle();
                _panelStyle.normal.background = _panelTex;
            }

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
                {
                    fontSize = 11,
                    richText = true,
                    alignment = TextAnchor.MiddleLeft,
                };
                if (_monoFont != null) _hintStyle.font = _monoFont;
                _hintStyle.normal.textColor = new Color(0.6f, 0.65f, 0.7f, 1f);
            }
        }
    }
}
