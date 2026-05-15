using UnityEngine;
using UnityEngine.InputSystem;

namespace LoneFighter.Debugging.Diagnostics
{
    /// Singleton entry point for the in-game diagnostics overlays.
    ///
    /// One DiagnosticsHub lives in the scene (or is auto-spawned at runtime). It owns the
    /// sub-overlay components, listens to the F4..F8 hotkeys via the new Input System, and
    /// gates every overlay behind DiagnosticsSettings.DiagnosticsAllowed (debug build / Editor).
    ///
    /// Hotkeys:
    ///   F4 - PoolDiagnosticsOverlay
    ///   F5 - EnemyAiVisualizer
    ///   F6 - MemoryOverlay
    ///   F7 - FrameTimeGraph
    ///   F8 - BuildInfoPanel
    ///
    /// The hub is intentionally lightweight: each sub-overlay reads its own visibility from
    /// DiagnosticsSettings, and the hub just flips those toggles in response to keypresses.
    [DefaultExecutionOrder(-100)]
    public class DiagnosticsHub : MonoBehaviour
    {
        public static DiagnosticsHub Instance { get; private set; }

        [Tooltip("If true, the hub will auto-create any missing sub-overlay components on Awake.")]
        [SerializeField] private bool autoCreateOverlays = true;

        private PoolDiagnosticsOverlay _pool;
        private EnemyAiVisualizer      _ai;
        private MemoryOverlay          _memory;
        private FrameTimeGraph         _frameGraph;
        private BuildInfoPanel         _buildInfo;

        /// Auto-spawn one DiagnosticsHub before any scene loads. Cheap, harmless if a manually
        /// placed one already exists (the Awake guard turns the extra into a no-op).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (Instance != null) return;

            var go = new GameObject("[DiagnosticsHub]");
            go.AddComponent<DiagnosticsHub>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (!DiagnosticsSettings.DiagnosticsAllowed)
            {
                // Quietly disable in non-debug builds. Nothing to render, nothing to listen for.
                enabled = false;
                return;
            }

            if (autoCreateOverlays) EnsureOverlays();
            SyncOverlayVisibility();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureOverlays()
        {
            _pool       = GetOrAdd<PoolDiagnosticsOverlay>();
            _ai         = GetOrAdd<EnemyAiVisualizer>();
            _memory     = GetOrAdd<MemoryOverlay>();
            _frameGraph = GetOrAdd<FrameTimeGraph>();
            _buildInfo  = GetOrAdd<BuildInfoPanel>();
        }

        private T GetOrAdd<T>() where T : Component
        {
            var c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        private void Update()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            bool changed = false;

            if (kb.f4Key.wasPressedThisFrame)
            {
                DiagnosticsSettings.PoolOverlay = !DiagnosticsSettings.PoolOverlay;
                changed = true;
            }
            if (kb.f5Key.wasPressedThisFrame)
            {
                DiagnosticsSettings.AiGizmos = !DiagnosticsSettings.AiGizmos;
                changed = true;
            }
            if (kb.f6Key.wasPressedThisFrame)
            {
                DiagnosticsSettings.MemoryOverlay = !DiagnosticsSettings.MemoryOverlay;
                changed = true;
            }
            if (kb.f7Key.wasPressedThisFrame)
            {
                DiagnosticsSettings.FrameGraph = !DiagnosticsSettings.FrameGraph;
                changed = true;
            }
            if (kb.f8Key.wasPressedThisFrame)
            {
                DiagnosticsSettings.BuildInfo = !DiagnosticsSettings.BuildInfo;
                changed = true;
            }

            if (changed) SyncOverlayVisibility();
        }

        /// Push the latest pref values into the sub-overlay `enabled` flags. Each overlay also
        /// re-checks the pref defensively, but pushing once on toggle avoids a per-frame poll
        /// inside each overlay's OnGUI hot path.
        private void SyncOverlayVisibility()
        {
            if (_pool       != null) _pool.enabled       = DiagnosticsSettings.PoolOverlay;
            if (_ai         != null) _ai.enabled         = DiagnosticsSettings.AiGizmos;
            if (_memory     != null) _memory.enabled     = DiagnosticsSettings.MemoryOverlay;
            if (_frameGraph != null) _frameGraph.enabled = DiagnosticsSettings.FrameGraph;
            if (_buildInfo  != null) _buildInfo.enabled  = DiagnosticsSettings.BuildInfo;
        }
    }
}
