using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Full-screen UI flash for Large / Mega / Nuke tiers. Drives a stretched
    /// <see cref="Image"/>'s alpha on a coroutine that uses unscaled time, so it still
    /// animates while <c>Time.timeScale</c> is 0 inside the tier's hit-stop window.
    ///
    /// Three modes of usage, in priority order:
    ///   1. Drop a <c>ScreenFlashOverlay</c> on a Canvas+Image you authored. The component
    ///      reuses them.
    ///   2. Let <see cref="ExplosionService"/> instantiate a bare <see cref="GameObject"/>
    ///      with this script. <see cref="EnsureCanvas"/> creates a top-most overlay
    ///      Canvas, full-screen Image, and stays alive across scene loads via
    ///      <c>DontDestroyOnLoad</c>.
    ///   3. Multiple overlays are tolerated — each instance is independent and the
    ///      service only calls <c>Instance</c>, so the most recently enabled wins.
    ///
    /// This class is deliberately UI-only; world-space flash is handled by
    /// <see cref="ExplosionFlash"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFlashOverlay : MonoBehaviour
    {
        public static ScreenFlashOverlay Instance { get; private set; }

        [Header("Wiring")]
        [Tooltip("Image used for the full-screen flash. Auto-created if null.")]
        [SerializeField] private Image image;

        [Tooltip("If true, the overlay survives scene loads. Recommended when auto-created.")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Tooltip("Canvas sort order. Keep above gameplay HUD so the flash actually covers everything.")]
        [SerializeField] private int canvasSortOrder = 32000;

        private Coroutine _routine;

        private void Awake()
        {
            // Last-writer-wins on Instance — same convention as FxService / PoolService.
            Instance = this;
            EnsureCanvas();
            if (persistAcrossScenes && transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Lazy-create a singleton overlay if no instance exists yet. Returns the
        /// active <see cref="Instance"/> for chained calls.
        /// </summary>
        public static ScreenFlashOverlay GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("ScreenFlashOverlay (auto)");
            var overlay = go.AddComponent<ScreenFlashOverlay>();
            return overlay;
        }

        /// <summary>
        /// Fire a single flash. Re-firing during an ongoing flash restarts the routine
        /// so brightness doesn't stack across rapid detonations.
        /// </summary>
        public void Flash(Color color, float duration)
        {
            if (image == null) EnsureCanvas();
            if (image == null) return;

            if (!isActiveAndEnabled)
            {
                // Routine can't run on a disabled object. Just snap alpha down and bail.
                var c = image.color;
                c.a = 0f;
                image.color = c;
                return;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.01f, duration)));
        }

        private IEnumerator FlashRoutine(Color peak, float duration)
        {
            // Snap to peak immediately — the "punch" — then fade to 0 with smoothstep.
            float attack = Mathf.Min(0.05f, duration * 0.2f);
            float release = Mathf.Max(0.05f, duration - attack);
            float t = 0f;

            while (t < attack)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(t / attack);
                var c = peak;
                c.a = peak.a * a;
                image.color = c;
                yield return null;
            }

            t = 0f;
            while (t < release)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / release));
                var c = peak;
                c.a = peak.a * a;
                image.color = c;
                yield return null;
            }

            // Final reset so a stuck-on alpha never leaks between flashes.
            var final = peak;
            final.a = 0f;
            image.color = final;
            _routine = null;
        }

        private void EnsureCanvas()
        {
            if (image != null) return;

            // Look for an existing Image on this hierarchy first.
            image = GetComponentInChildren<Image>(includeInactive: true);
            if (image != null)
            {
                var existingCanvas = image.GetComponentInParent<Canvas>();
                if (existingCanvas != null)
                {
                    existingCanvas.sortingOrder = Mathf.Max(existingCanvas.sortingOrder, canvasSortOrder);
                }
                ConfigureImage(image);
                return;
            }

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, worldPositionStays: false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;
            canvasGo.AddComponent<CanvasScaler>();
            // No GraphicRaycaster — this overlay should never absorb input.

            var imgGo = new GameObject("Flash");
            imgGo.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            image = imgGo.AddComponent<Image>();
            ConfigureImage(image);
        }

        private static void ConfigureImage(Image img)
        {
            img.raycastTarget = false;
            var c = img.color;
            c.a = 0f;
            img.color = c;
        }
    }
}
