using UnityEngine;

namespace LoneFighter.World
{
    /// <summary>
    /// Drives a stack of background <see cref="Transform"/> layers (typically SpriteRenderers
    /// set to <see cref="SpriteDrawMode.Tiled"/>) so they appear to scroll at fractional speeds
    /// relative to the tracked camera, and so each layer wraps every <see cref="tileSize"/>
    /// world units to fake an infinite plane.
    ///
    /// Layer factors:
    ///  - 0.0 = layer is glued to the camera (sky / vignette)
    ///  - 1.0 = layer doesn't move with the camera (in-world geometry)
    ///  - Values in between (0.1, 0.3, 0.5) create depth — far things drift slowly past, near things drift faster.
    ///
    /// Each layer's localPosition is rewritten every frame, so its authored offset is treated
    /// as the layer origin. Z is preserved per-layer so sorting still works.
    /// </summary>
    [DefaultExecutionOrder(100)] // After camera follow updates, before render.
    public class ParallaxBackground : MonoBehaviour
    {
        [Tooltip("Layer transforms in painter's order (back to front).")]
        [SerializeField] private Transform[] layers;

        [Tooltip("Parallax factor per layer (one per entry in 'layers'). 0=stuck to camera, 1=stuck to world. Try 0.1, 0.3, 0.5.")]
        [SerializeField] private float[] parallaxFactors;

        [Tooltip("World-units between wrap repeats. Match this to your tiled sprite's logical tile size so seams hide.")]
        [SerializeField] private float tileSize = 20f;

        [Tooltip("Camera to follow. Leaves blank to auto-resolve Camera.main on Awake.")]
        [SerializeField] private Camera trackedCamera;

        private Vector3[] _layerOrigins;
        private float[] _layerZ;

        private void Awake()
        {
            if (trackedCamera == null) trackedCamera = Camera.main;
            CacheLayers();
        }

        private void OnValidate()
        {
            // Keep arrays in sync when tweaking in Inspector at edit time.
            if (parallaxFactors == null || layers == null) return;
            if (parallaxFactors.Length != layers.Length)
            {
                var resized = new float[layers.Length];
                for (int i = 0; i < resized.Length; i++)
                    resized[i] = i < parallaxFactors.Length ? parallaxFactors[i] : Mathf.Clamp01(0.1f * (i + 1));
                parallaxFactors = resized;
            }
        }

        private void CacheLayers()
        {
            if (layers == null) { _layerOrigins = System.Array.Empty<Vector3>(); _layerZ = System.Array.Empty<float>(); return; }
            _layerOrigins = new Vector3[layers.Length];
            _layerZ = new float[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                _layerOrigins[i] = layers[i].position;
                _layerZ[i] = layers[i].position.z;
            }
        }

        private void LateUpdate()
        {
            if (trackedCamera == null) trackedCamera = Camera.main;
            if (trackedCamera == null || layers == null) return;
            if (_layerOrigins == null || _layerOrigins.Length != layers.Length) CacheLayers();

            Vector3 camPos = trackedCamera.transform.position;
            float half = tileSize * 0.5f;
            float invTile = tileSize > 0f ? 1f / tileSize : 0f;

            for (int i = 0; i < layers.Length; i++)
            {
                var t = layers[i];
                if (t == null) continue;
                float factor = (parallaxFactors != null && i < parallaxFactors.Length) ? parallaxFactors[i] : 0.5f;

                // Scrolling offset for this layer: at factor=1 the layer is world-locked (offset=0),
                // at factor=0 the layer sticks to the camera (offset=camPos-origin).
                Vector3 origin = _layerOrigins[i];
                float dx = (camPos.x - origin.x) * (1f - factor);
                float dy = (camPos.y - origin.y) * (1f - factor);

                if (tileSize > 0f)
                {
                    // Wrap around the camera so the sprite always covers the screen and the seam
                    // is invisible (assuming the texture itself tiles).
                    dx = Mathf.Repeat(dx + half, tileSize) - half;
                    dy = Mathf.Repeat(dy + half, tileSize) - half;

                    // Now snap the layer to follow the camera plus its wrapped offset.
                    t.position = new Vector3(camPos.x + dx, camPos.y + dy, _layerZ[i]);
                }
                else
                {
                    t.position = new Vector3(origin.x + dx, origin.y + dy, _layerZ[i]);
                }
            }
        }

        /// <summary>Re-cache layer origins (call after assigning layers programmatically).</summary>
        public void Rebind()
        {
            CacheLayers();
        }
    }
}
