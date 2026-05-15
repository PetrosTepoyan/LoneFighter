using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.Minimap
{
    /// <summary>
    /// Runtime helper that guarantees a radar root is rendered as a circle, even when no
    /// circle sprite has been wired up in the inspector. Generates a smooth anti-aliased
    /// circle sprite procedurally (cached per resolution) and attaches a uGUI <see cref="Mask"/>
    /// so that any child markers / sweep lines are clipped to the radar disc.
    ///
    /// Safe to call multiple times — it short-circuits if a Mask + circle sprite already exist.
    /// Lives in <c>Assets/Scripts</c> rather than <c>Editor/</c> on purpose so it can be used
    /// from runtime code paths (e.g. spawning the minimap from a prefab at scene load).
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapMaskSetup : MonoBehaviour
    {
        [Tooltip("Sprite resolution used for the procedural circle (square texture).")]
        [SerializeField, Range(64, 512)] private int circleResolution = 256;

        [Tooltip("Background color baked into the circle sprite. Alpha is what creates the disc.")]
        [SerializeField] private Color baseColor = new Color(0.05f, 0.08f, 0.12f, 0.78f);

        [Tooltip("Soft border thickness (0..0.2 of radius). Higher = softer anti-aliased edge.")]
        [Range(0f, 0.2f)] [SerializeField] private float edgeSoftness = 0.025f;

        [Tooltip("If true, mask graphic is shown. Disable if you want the circle invisible but still clipping.")]
        [SerializeField] private bool showMaskGraphic = true;

        // Cached so multiple radars can share one sprite without re-generating.
        private static Sprite s_cachedCircle;
        private static int s_cachedResolution;
        private static Color s_cachedColor;
        private static float s_cachedSoftness;

        private void Awake()
        {
            EnsureMask(gameObject, circleResolution, baseColor, edgeSoftness, showMaskGraphic);
        }

        /// <summary>
        /// Idempotently configures <paramref name="target"/> as a circular mask root.
        /// Adds an <see cref="Image"/> + <see cref="Mask"/> if missing, and assigns a
        /// procedurally generated circle sprite when no sprite has been provided yet.
        /// </summary>
        public static void EnsureMask(GameObject target, int resolution, Color color, float softness, bool showMaskGraphic)
        {
            if (target == null) return;

            var image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;

            if (image.sprite == null)
            {
                image.sprite = GetOrCreateCircleSprite(resolution, color, softness);
                // The sprite carries the alpha falloff; tint the image white so we don't double-darken.
                image.color = Color.white;
            }

            var mask = target.GetComponent<Mask>();
            if (mask == null) mask = target.AddComponent<Mask>();
            mask.showMaskGraphic = showMaskGraphic;
        }

        /// <summary>
        /// Returns a cached circular sprite, regenerating only when the requested parameters
        /// have changed (so reloading the scene doesn't leak textures).
        /// </summary>
        public static Sprite GetOrCreateCircleSprite(int resolution, Color color, float softness)
        {
            resolution = Mathf.Clamp(resolution, 8, 1024);
            softness = Mathf.Clamp(softness, 0f, 0.5f);

            if (s_cachedCircle != null
                && s_cachedResolution == resolution
                && s_cachedColor == color
                && Mathf.Approximately(s_cachedSoftness, softness))
            {
                return s_cachedCircle;
            }

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "MinimapCircle",
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color[resolution * resolution];
            float half = resolution * 0.5f;
            float radius = half - 1f;
            float invRadius = 1f / Mathf.Max(radius, 0.0001f);
            float edge = Mathf.Max(softness, 0.001f);

            for (int y = 0; y < resolution; y++)
            {
                float dy = y - half + 0.5f;
                int row = y * resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - half + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * invRadius; // 0 at center, 1 at radius
                    // Smooth alpha falloff at the edge.
                    float a = 1f - Mathf.SmoothStep(1f - edge, 1f, d);
                    a = Mathf.Clamp01(a);
                    var c = color;
                    c.a *= a;
                    pixels[row + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, resolution, resolution),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "MinimapCircleSprite";
            sprite.hideFlags = HideFlags.DontSave;

            s_cachedCircle = sprite;
            s_cachedResolution = resolution;
            s_cachedColor = color;
            s_cachedSoftness = softness;
            return sprite;
        }
    }
}
