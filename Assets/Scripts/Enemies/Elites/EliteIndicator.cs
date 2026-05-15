using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// Off-screen indicator: while any <see cref="EliteModifier"/> is alive but outside
    /// the camera's viewport, an arrow is drawn at the edge of the screen pointing
    /// toward it.
    ///
    /// Drives a small pool of <see cref="Image"/>-based arrows under
    /// <see cref="arrowParent"/>. If <see cref="arrowPrefab"/> is null, a minimal default
    /// arrow Image is constructed at runtime so the indicator still functions in a
    /// bare scene (gold square — replace with a real sprite in production).
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteIndicator : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Camera used for off-screen tests. Defaults to Camera.main.")]
        [SerializeField] private Camera trackingCamera;

        [Tooltip("RectTransform under which arrow indicators are spawned. Should fill the screen-space canvas.")]
        [SerializeField] private RectTransform arrowParent;

        [Tooltip("Prefab with an Image (or any RectTransform-rooted graphic). If null, a default arrow is built at runtime.")]
        [SerializeField] private RectTransform arrowPrefab;

        [Header("Layout")]
        [Tooltip("Distance in pixels from the screen edge that the arrow sits.")]
        [SerializeField] private float edgePadding = 48f;

        [Tooltip("Optional pulsing colour for visibility.")]
        [SerializeField] private Color arrowColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Tooltip("If true, the arrow's local rotation is set so its 'right' axis points toward the elite.")]
        [SerializeField] private bool rotateArrow = true;

        // Tracks each elite => its arrow rect. Arrows are reused via a stack pool so we
        // don't allocate per frame.
        private readonly Dictionary<EliteModifier, RectTransform> _arrows = new();
        private readonly Stack<RectTransform> _pool = new();
        private readonly List<EliteModifier> _toRemove = new();

        private Canvas _canvas;
        private RectTransform _canvasRect;

        private void Awake()
        {
            if (trackingCamera == null) trackingCamera = Camera.main;
            if (arrowParent != null)
            {
                _canvas = arrowParent.GetComponentInParent<Canvas>();
                if (_canvas != null) _canvasRect = _canvas.transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            EliteSpawnDirector.OnEliteAppeared += HandleEliteAppeared;
        }

        private void OnDisable()
        {
            EliteSpawnDirector.OnEliteAppeared -= HandleEliteAppeared;
            // Return all arrows to the pool.
            foreach (var kv in _arrows)
            {
                if (kv.Value != null)
                {
                    kv.Value.gameObject.SetActive(false);
                    _pool.Push(kv.Value);
                }
            }
            _arrows.Clear();
        }

        private void HandleEliteAppeared(EliteModifier modifier)
        {
            if (modifier == null || _arrows.ContainsKey(modifier)) return;
            var arrow = GetOrCreateArrow();
            if (arrow == null) return;
            _arrows[modifier] = arrow;
            arrow.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (_arrows.Count == 0) return;
            if (trackingCamera == null)
            {
                trackingCamera = Camera.main;
                if (trackingCamera == null) return;
            }
            if (arrowParent == null) return;

            // Tick each tracked elite. Cull entries whose host has been destroyed/disabled.
            _toRemove.Clear();
            foreach (var kv in _arrows)
            {
                var modifier = kv.Key;
                var arrow = kv.Value;
                if (modifier == null || modifier.Host == null || !modifier.Host.isActiveAndEnabled || !modifier.enabled)
                {
                    _toRemove.Add(modifier);
                    continue;
                }
                UpdateArrow(modifier, arrow);
            }
            foreach (var modifier in _toRemove)
            {
                if (_arrows.TryGetValue(modifier, out var rt) && rt != null)
                {
                    rt.gameObject.SetActive(false);
                    _pool.Push(rt);
                }
                _arrows.Remove(modifier);
            }
        }

        private void UpdateArrow(EliteModifier modifier, RectTransform arrow)
        {
            Vector3 worldPos = modifier.Host.transform.position;
            Vector3 viewport = trackingCamera.WorldToViewportPoint(worldPos);
            bool behindCamera = viewport.z < 0f;
            bool offscreen = behindCamera || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;

            if (!offscreen)
            {
                // On-screen elites don't need an indicator. Hide but keep the pool entry
                // so we don't churn allocations.
                if (arrow.gameObject.activeSelf) arrow.gameObject.SetActive(false);
                return;
            }
            if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);

            // Compute screen-space target relative to canvas.
            Vector2 canvasSize = _canvasRect != null ? _canvasRect.rect.size : new Vector2(Screen.width, Screen.height);
            float halfW = canvasSize.x * 0.5f;
            float halfH = canvasSize.y * 0.5f;

            // Vector from screen centre to target in canvas coordinates.
            // viewport (0..1) maps to (-halfW..halfW). Behind-camera: flip.
            Vector2 dir;
            if (behindCamera)
            {
                // Mirror; we still want the arrow on the side toward the elite.
                dir = new Vector2(-(viewport.x - 0.5f), -(viewport.y - 0.5f));
            }
            else
            {
                dir = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            }

            // Clamp the indicator to a rectangle inset by `edgePadding`.
            float maxX = halfW - edgePadding;
            float maxY = halfH - edgePadding;
            // Scale dir so its largest component lands on the rectangle boundary.
            float ax = Mathf.Abs(dir.x);
            float ay = Mathf.Abs(dir.y);
            float scale;
            if (ax < 1e-4f && ay < 1e-4f)
            {
                // Degenerate (elite essentially at camera centre — shouldn't be off-screen).
                arrow.anchoredPosition = Vector2.zero;
                return;
            }

            if (ax * maxY > ay * maxX)
            {
                scale = maxX / ax;
            }
            else
            {
                scale = maxY / Mathf.Max(ay, 1e-4f);
            }

            Vector2 pos = dir * scale;
            arrow.anchoredPosition = pos;
            if (rotateArrow)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private RectTransform GetOrCreateArrow()
        {
            if (arrowParent == null) return null;

            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }

            RectTransform rt;
            if (arrowPrefab != null)
            {
                rt = Instantiate(arrowPrefab, arrowParent);
            }
            else
            {
                rt = BuildDefaultArrow();
            }
            rt.gameObject.SetActive(false);
            return rt;
        }

        private RectTransform BuildDefaultArrow()
        {
            var go = new GameObject("EliteArrow");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(arrowParent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(36f, 36f);

            var image = go.AddComponent<Image>();
            image.color = arrowColor;
            image.raycastTarget = false;

            return rt;
        }
    }
}
