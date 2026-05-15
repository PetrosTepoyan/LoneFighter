using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Animated sweep line that rotates around the radar's center, purely cosmetic.
    /// Auto-builds a tall thin <see cref="Image"/> rect anchored at center with its
    /// pivot at the base so rotating the <see cref="RectTransform"/> swings the bar
    /// like a clock hand.
    ///
    /// One full rotation per <see cref="periodSeconds"/>. Uses <see cref="Time.unscaledDeltaTime"/>
    /// so the sweep keeps spinning while the game is paused (level-up modal, settings, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public class RadarPulse : MonoBehaviour
    {
        [Tooltip("Seconds for one full 360 sweep. Default 2s.")]
        [SerializeField] private float periodSeconds = 2f;

        [Tooltip("Color of the sweep line. Alpha controls how aggressive it reads on top of the radar.")]
        [SerializeField] private Color sweepColor = new Color(0.4f, 1f, 0.6f, 0.35f);

        [Tooltip("Width of the sweep line in pixels.")]
        [SerializeField] private float sweepWidth = 2f;

        [Tooltip("If true, rotates clockwise (radar convention). Otherwise counter-clockwise.")]
        [SerializeField] private bool clockwise = true;

        [Tooltip("Use unscaled time so the sweep continues during gameplay pause.")]
        [SerializeField] private bool useUnscaledTime = true;

        private RectTransform _rt;
        private Image _sweepImage;
        private RectTransform _sweepRt;
        private float _angle;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            BuildSweepIfNeeded();
        }

        private void OnEnable()
        {
            BuildSweepIfNeeded();
        }

        private void Update()
        {
            if (_sweepRt == null) return;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float speed = periodSeconds > 0.0001f ? 360f / periodSeconds : 0f;
            _angle += (clockwise ? -speed : speed) * dt;
            // Keep the accumulator bounded so it can run forever without precision drift.
            if (_angle > 360f) _angle -= 360f;
            else if (_angle < -360f) _angle += 360f;
            _sweepRt.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }

        /// <summary>
        /// Lazy-build the sweep arm child. Anchored at center; pivot at the bottom-center so
        /// rotation pivots around the radar origin and the bar extends outward to the rim.
        /// </summary>
        private void BuildSweepIfNeeded()
        {
            if (_sweepRt != null) return;
            if (_rt == null) _rt = (RectTransform)transform;

            var go = new GameObject("SweepArm", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _sweepRt = (RectTransform)go.transform;
            _sweepImage = go.GetComponent<Image>();

            _sweepImage.raycastTarget = false;
            _sweepImage.color = sweepColor;

            _sweepRt.anchorMin = new Vector2(0.5f, 0.5f);
            _sweepRt.anchorMax = new Vector2(0.5f, 0.5f);
            _sweepRt.pivot = new Vector2(0.5f, 0f);
            _sweepRt.anchoredPosition = Vector2.zero;
            // Length: half the radar width (we size to parent rect at runtime).
            _sweepRt.sizeDelta = new Vector2(sweepWidth, ComputeArmLength());
        }

        private float ComputeArmLength()
        {
            if (_rt == null) return 64f;
            var size = _rt.rect.size;
            return 0.5f * Mathf.Min(size.x, size.y);
        }

        private void OnRectTransformDimensionsChange()
        {
            // Keep the sweep arm sized to the radar even if the layout changes (rotate device, etc.).
            if (_sweepRt != null)
            {
                _sweepRt.sizeDelta = new Vector2(sweepWidth, ComputeArmLength());
            }
        }

        public void SetColor(Color color)
        {
            sweepColor = color;
            if (_sweepImage != null) _sweepImage.color = color;
        }

        public void SetPeriod(float seconds)
        {
            periodSeconds = Mathf.Max(0.05f, seconds);
        }
    }
}
