// LoneFighter - Diagonal parallax background scroller.
// Tiles a sprite via material offset (or transform fallback) using Mathf.Repeat
// so the visible texture wraps seamlessly.

using UnityEngine;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Slowly scrolls a tiling background sprite diagonally. Attach to a
    /// GameObject with either:
    ///   - a SpriteRenderer using a material with `_MainTex` whose texture is
    ///     set to Repeat wrap mode (preferred), or
    ///   - a child Transform that should simply translate (fallback).
    /// </summary>
    [DisallowMultipleComponent]
    public class ParallaxScroller : MonoBehaviour
    {
        [Header("Scroll Speed (UV units per second)")]
        [Tooltip("Horizontal scroll speed. Small values look best (e.g. 0.01).")]
        [SerializeField] private float speedX = 0.015f;

        [Tooltip("Vertical scroll speed. Small values look best (e.g. 0.01).")]
        [SerializeField] private float speedY = 0.008f;

        [Header("Targets (optional - auto-detected)")]
        [Tooltip("Renderer whose material tex offset will be scrolled. Falls back to transform translate if null.")]
        [SerializeField] private Renderer targetRenderer;

        [Tooltip("Material texture property to scroll (Unity URP/2D default: _MainTex).")]
        [SerializeField] private string textureProperty = "_MainTex";

        [Header("Transform-Fallback Loop")]
        [Tooltip("World-space tile size used when scrolling via transform translation. Wraps after this many units.")]
        [SerializeField] private Vector2 worldTileSize = new Vector2(10f, 10f);

        private Material runtimeMaterial;
        private Vector3 startLocalPos;
        private Vector2 offset;
        private bool useMaterialPath;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            if (targetRenderer != null && targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(textureProperty))
            {
                // Instance the material so we don't mutate the shared asset.
                runtimeMaterial = targetRenderer.material;
                useMaterialPath = true;
            }
            else
            {
                useMaterialPath = false;
                startLocalPos = transform.localPosition;
            }
        }

        private void OnDisable()
        {
            // Reset state so re-enabling looks identical to first run.
            offset = Vector2.zero;
            if (useMaterialPath && runtimeMaterial != null)
            {
                runtimeMaterial.SetTextureOffset(textureProperty, Vector2.zero);
            }
            else
            {
                transform.localPosition = startLocalPos;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            offset.x = Mathf.Repeat(offset.x + speedX * dt, 1f);
            offset.y = Mathf.Repeat(offset.y + speedY * dt, 1f);

            if (useMaterialPath && runtimeMaterial != null)
            {
                runtimeMaterial.SetTextureOffset(textureProperty, offset);
                return;
            }

            // Transform fallback: convert normalized offset to world units and
            // wrap inside a tile-sized window so the sprite appears to loop.
            float xRange = Mathf.Max(0.0001f, worldTileSize.x);
            float yRange = Mathf.Max(0.0001f, worldTileSize.y);
            Vector3 p = startLocalPos;
            p.x += Mathf.Repeat(offset.x * xRange, xRange) - xRange * 0.5f;
            p.y += Mathf.Repeat(offset.y * yRange, yRange) - yRange * 0.5f;
            transform.localPosition = p;
        }
    }
}
