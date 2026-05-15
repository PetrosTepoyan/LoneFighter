using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Player;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Full-screen "I am dying" effect: when the player's HP drops below
    /// <see cref="thresholdFraction"/>, four edge-anchored red splatter Images
    /// fade in around the borders of the screen. They get denser (more opaque)
    /// the closer the player is to zero HP, then fade back when the player
    /// heals.
    ///
    /// We auto-build the four corner Images on a child Canvas at runtime if the
    /// designer hasn't pre-authored them, so dropping this component on an
    /// empty GameObject "just works" in a test scene. For shipping you'll
    /// usually want to assign hand-painted splatter sprites in inspector.
    /// </summary>
    public class BloodSplashOverlay : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("HP fraction below which the overlay starts to appear.")]
        [SerializeField, Range(0.05f, 0.95f)] private float thresholdFraction = 0.25f;

        [Header("Visuals")]
        [Tooltip("Splatter sprite. If null, a plain red rect is used.")]
        [SerializeField] private Sprite splatterSprite;
        [Tooltip("Splatter tint. Alpha here is the max possible alpha at 0 HP.")]
        [SerializeField] private Color splatterColor = new Color(0.6f, 0f, 0f, 0.85f);
        [Tooltip("Size of each corner splatter as a fraction of the screen's shorter axis.")]
        [SerializeField, Range(0.1f, 1f)] private float splatterSizeFraction = 0.45f;
        [Tooltip("Sort order for the overlay canvas. Set high so it renders on top.")]
        [SerializeField] private int sortingOrder = 1000;

        [Header("Optional Pre-Built References")]
        [Tooltip("If you've authored your own canvas, drop the splatter Images in here. " +
                 "Leave empty to have the component build them at runtime.")]
        [SerializeField] private Image[] preBuiltSplatters;

        private PlayerHealth _health;
        private Canvas _canvas;
        private Image[] _splatters;
        private float _hpFraction = 1f;

        private void Start()
        {
            ResolveHealth();
            BuildOrAdoptOverlay();
            ApplyOpacity(0f);
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHpChanged;
        }

        private void ResolveHealth()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
            }
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHpChanged;
                _hpFraction = _health.Max > 0f ? _health.Current / _health.Max : 1f;
            }
        }

        private void BuildOrAdoptOverlay()
        {
            if (preBuiltSplatters != null && preBuiltSplatters.Length > 0)
            {
                _splatters = preBuiltSplatters;
                return;
            }

            // Build a screen-space-overlay canvas with four splatters anchored
            // to the screen corners.
            var canvasGo = new GameObject("BloodSplashCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;

            _splatters = new Image[4];
            // Anchor convention: x/y in {0,1} = corner.
            Vector2[] anchors = {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Splatter{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_canvas.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = anchors[i];
                rt.anchorMax = anchors[i];
                rt.pivot = anchors[i];
                // Size is set in OnRectTransformDimensionsChange; default to 256.
                rt.sizeDelta = new Vector2(256f, 256f);
                rt.anchoredPosition = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.sprite = splatterSprite;
                img.color = new Color(splatterColor.r, splatterColor.g, splatterColor.b, 0f);
                img.raycastTarget = false;
                img.preserveAspect = splatterSprite != null;
                _splatters[i] = img;
            }
        }

        private void Update()
        {
            // Size the splatters relative to the actual screen each frame in
            // case the user rotates the device or we hot-reload aspect.
            if (_splatters == null || _canvas == null) return;
            float side = Mathf.Min(Screen.width, Screen.height) * splatterSizeFraction;
            for (int i = 0; i < _splatters.Length; i++)
            {
                if (_splatters[i] == null) continue;
                var rt = _splatters[i].rectTransform;
                rt.sizeDelta = new Vector2(side, side);
            }
        }

        private void HandleHpChanged(float current, float max)
        {
            _hpFraction = max > 0f ? current / max : 1f;
            Refresh();
        }

        /// <summary>
        /// Public refresh hook — useful when an external caller knows the player
        /// took damage but the OnHealthChanged event hasn't fired yet (rare).
        /// </summary>
        public void Refresh()
        {
            if (_health != null) _hpFraction = _health.Max > 0f ? _health.Current / _health.Max : 1f;

            float t;
            if (_hpFraction >= thresholdFraction) t = 0f;
            else
            {
                // Linear ramp from threshold -> 0: 0 at threshold, 1 at 0 HP.
                t = 1f - (_hpFraction / Mathf.Max(0.0001f, thresholdFraction));
                t = Mathf.Clamp01(t);
            }
            ApplyOpacity(t);
        }

        private void ApplyOpacity(float t)
        {
            if (_splatters == null) return;
            for (int i = 0; i < _splatters.Length; i++)
            {
                if (_splatters[i] == null) continue;
                var c = _splatters[i].color;
                c.r = splatterColor.r;
                c.g = splatterColor.g;
                c.b = splatterColor.b;
                c.a = splatterColor.a * t;
                _splatters[i].color = c;
            }
        }
    }
}
