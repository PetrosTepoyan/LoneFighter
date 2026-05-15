using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Renders a small row of TMP-based icons above a victim, one per active status effect.
    /// Listens to a sibling <see cref="StatusController"/> and rebuilds the row when effects change.
    ///
    /// Icons are recycled inside an internal free-list to keep allocations at zero during gameplay.
    /// The Canvas is World-Space and parented to this transform; it follows the victim automatically.
    ///
    /// Inspector setup is optional — if no canvas or template are assigned, the component spins up
    /// a minimal procedural canvas + TMP template on Awake so the framework works headless.
    /// </summary>
    [RequireComponent(typeof(StatusController))]
    public class StatusIcons : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("World-space offset from the victim's pivot.")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.9f, 0f);
        [Tooltip("Horizontal spacing between icons, in canvas-local units (pixels before iconScale).")]
        [SerializeField] private float iconSpacing = 42f;
        [Tooltip("World scale applied to the entire canvas (1 px = iconScale world units).")]
        [SerializeField] private float iconScale = 0.012f;

        [Header("Optional pre-authored references (left blank = auto-created)")]
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private TMP_FontAsset fontAsset;

        private StatusController _controller;
        private readonly List<IconView> _live = new();
        private readonly Stack<IconView> _free = new();

        private struct IconView
        {
            public RectTransform Root;
            public TMP_Text Label;
        }

        private void Awake()
        {
            _controller = GetComponent<StatusController>();
            EnsureCanvas();
        }

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<StatusController>();
            _controller.OnEffectAdded += HandleChanged;
            _controller.OnEffectRemoved += HandleChanged;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnEffectAdded -= HandleChanged;
                _controller.OnEffectRemoved -= HandleChanged;
            }
            // Recycle all live icons so the next OnEnable starts clean.
            for (int i = _live.Count - 1; i >= 0; i--) Recycle(i);
        }

        private void LateUpdate()
        {
            if (worldCanvas == null) return;
            // If the canvas isn't a child of this transform (pre-authored case), follow the victim manually.
            if (worldCanvas.transform.parent != transform)
            {
                worldCanvas.transform.position = transform.position + worldOffset;
            }
        }

        private void HandleChanged(StatusController controller, StatusEffect effect) => Rebuild();

        private void Rebuild()
        {
            if (_controller == null) return;
            var effects = _controller.ActiveEffects;

            // Grow/shrink the pool of live icons to match the active-effect count.
            while (_live.Count < effects.Count) _live.Add(AcquireIcon());
            while (_live.Count > effects.Count) Recycle(_live.Count - 1);

            // Layout horizontally, centered around the victim.
            float total = (effects.Count - 1) * iconSpacing;
            float startX = -total * 0.5f;

            for (int i = 0; i < effects.Count; i++)
            {
                var fx = effects[i];
                var view = _live[i];
                if (view.Root == null) continue;

                view.Root.gameObject.SetActive(true);
                view.Root.anchoredPosition3D = new Vector3(startX + i * iconSpacing, 0f, 0f);

                if (view.Label != null)
                {
                    view.Label.text = fx.DisplayLabel;
                    view.Label.color = fx.IconColor;
                }
            }
        }

        private IconView AcquireIcon()
        {
            if (_free.Count > 0) return _free.Pop();

            // Build a minimal TMP-based icon from scratch.
            var go = new GameObject("StatusIcon", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(worldCanvas.transform, false);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(40f, 16f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.fontSize = 10f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return new IconView { Root = rt, Label = tmp };
        }

        private void Recycle(int index)
        {
            var view = _live[index];
            if (view.Root != null) view.Root.gameObject.SetActive(false);
            _free.Push(view);
            _live.RemoveAt(index);
        }

        private void EnsureCanvas()
        {
            if (worldCanvas != null) return;
            var canvasGo = new GameObject("StatusIconsCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = worldOffset;
            canvasGo.transform.localScale = Vector3.one * iconScale;

            worldCanvas = canvasGo.GetComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 50;
        }
    }
}
