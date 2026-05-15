using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Visual entry plotted on the radar. Just a colored, sized <see cref="Image"/>;
    /// the <see cref="MinimapController"/> owns the pool and decides type/position per frame.
    /// Markers are cheap: they live as inactive children of the radar root and are
    /// activated/repositioned each refresh rather than instantiated.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class MinimapMarker : MonoBehaviour
    {
        /// <summary>
        /// Marker kinds. Color + size palette is centralized in <see cref="GetStyle"/>
        /// so the controller only has to pick a type per entity.
        /// </summary>
        public enum MarkerType
        {
            Enemy,
            Elite,
            Boss,
            Xp,
            Health,
            Pickup,
            Player,
        }

        public readonly struct Style
        {
            public readonly Color color;
            public readonly float size;
            public readonly bool isPlus;

            public Style(Color color, float size, bool isPlus = false)
            {
                this.color = color;
                this.size = size;
                this.isPlus = isPlus;
            }
        }

        private RectTransform _rt;
        private Image _image;
        private MarkerType _type;

        /// <summary>The cross-bar used to render a "+" for health markers. Optional.</summary>
        private RectTransform _plusOverlay;

        public RectTransform RectTransform
        {
            get
            {
                if (_rt == null) _rt = (RectTransform)transform;
                return _rt;
            }
        }

        public Image Image
        {
            get
            {
                if (_image == null) _image = GetComponent<Image>();
                return _image;
            }
        }

        public MarkerType Type => _type;

        /// <summary>
        /// Returns the (color, size, isPlus) styling tuple for a given marker type.
        /// Boss markers are deliberately oversized so they read at a glance even when stacked.
        /// </summary>
        public static Style GetStyle(MarkerType type)
        {
            switch (type)
            {
                case MarkerType.Enemy:
                    return new Style(new Color(0.95f, 0.20f, 0.20f, 1f), 6f);
                case MarkerType.Elite:
                    return new Style(new Color(1f, 0.84f, 0.20f, 1f), 8f);
                case MarkerType.Boss:
                    return new Style(new Color(0.70f, 0.30f, 0.95f, 1f), 14f);
                case MarkerType.Xp:
                    return new Style(new Color(0.30f, 0.95f, 0.45f, 1f), 5f);
                case MarkerType.Health:
                    // Health pickups read as red with a plus overlay.
                    return new Style(new Color(0.95f, 0.30f, 0.40f, 1f), 7f, isPlus: true);
                case MarkerType.Pickup:
                    return new Style(Color.white, 5f);
                case MarkerType.Player:
                    return new Style(new Color(0.30f, 0.85f, 1f, 1f), 9f);
                default:
                    return new Style(Color.white, 5f);
            }
        }

        public void Configure(MarkerType type, Vector2 anchoredPosition)
        {
            _type = type;
            var style = GetStyle(type);

            var rt = RectTransform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(style.size, style.size);

            var img = Image;
            img.color = style.color;
            img.raycastTarget = false;

            EnsurePlusOverlay(style.isPlus, style.color, style.size);

            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// Lazy-builds (or shows/hides) a small horizontal bar child that gives the marker
        /// a visible "+" sign. We let the marker's own square act as the vertical bar, and
        /// overlay a thin horizontal rect for the cross stroke.
        /// </summary>
        private void EnsurePlusOverlay(bool wantOverlay, Color color, float size)
        {
            if (!wantOverlay)
            {
                if (_plusOverlay != null && _plusOverlay.gameObject.activeSelf)
                    _plusOverlay.gameObject.SetActive(false);
                return;
            }

            if (_plusOverlay == null)
            {
                var go = new GameObject("PlusBar", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                _plusOverlay = (RectTransform)go.transform;
                var olImg = go.GetComponent<Image>();
                olImg.raycastTarget = false;
                olImg.color = color;
                _plusOverlay.anchorMin = new Vector2(0.5f, 0.5f);
                _plusOverlay.anchorMax = new Vector2(0.5f, 0.5f);
                _plusOverlay.pivot = new Vector2(0.5f, 0.5f);
            }

            // Pull color in case it changed since last use.
            var img = _plusOverlay.GetComponent<Image>();
            if (img != null) img.color = color;

            // Horizontal bar: wider than the square, but thinner.
            _plusOverlay.sizeDelta = new Vector2(size * 1.6f, size * 0.45f);
            _plusOverlay.anchoredPosition = Vector2.zero;
            if (!_plusOverlay.gameObject.activeSelf) _plusOverlay.gameObject.SetActive(true);
        }
    }
}
