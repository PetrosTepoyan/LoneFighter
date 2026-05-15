using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.World
{
    /// <summary>
    /// Procedural star/dot field that follows the camera and recycles distant points back into view,
    /// giving the empty arena a "void of space" feel without a tilemap. Cheap: spawns once, then only
    /// translates a fixed pool of SpriteRenderers each frame.
    ///
    /// Drop on a child of the Arena root, optionally beneath a <see cref="ParallaxBackground"/> with
    /// a low parallaxFactor for the slowest-moving stars.
    /// </summary>
    public class StarfieldBackground : MonoBehaviour
    {
        [Header("Field")]
        [Tooltip("Side of the square region (in world units) that always stays roughly centered on the camera.")]
        [SerializeField] private float fieldSize = 40f;

        [Tooltip("How many stars are alive at once. Keep modest — every star is a SpriteRenderer.")]
        [SerializeField] private int starCount = 80;

        [Tooltip("Random seed so the layout is deterministic per scene.")]
        [SerializeField] private int seed = 1337;

        [Header("Look")]
        [Tooltip("Sprite for each star. Leave null to auto-generate a single white dot at runtime.")]
        [SerializeField] private Sprite starSprite;

        [Tooltip("Random size range per star.")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.04f, 0.12f);

        [Tooltip("Random brightness range (alpha multiplier).")]
        [SerializeField] private Vector2 brightnessRange = new Vector2(0.3f, 1f);

        [SerializeField] private Color tint = new Color(0.85f, 0.9f, 1f, 1f);

        [Tooltip("Sorting layer order (negative = far behind everything).")]
        [SerializeField] private int sortingOrder = -100;

        [Header("Camera")]
        [SerializeField] private Camera trackedCamera;

        [Tooltip("Parallax factor: 0 = stuck to camera, 1 = world-locked. Stars look best around 0.05–0.2.")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxFactor = 0.1f;

        private readonly List<Transform> _stars = new();
        private readonly List<Vector2> _localOffsets = new();
        private Vector3 _origin;

        private void Awake()
        {
            if (trackedCamera == null) trackedCamera = Camera.main;
            _origin = transform.position;
            BuildStars();
        }

        private void OnDisable()
        {
            // Children survive but we won't tick them.
        }

        private void BuildStars()
        {
            var rng = new System.Random(seed);
            float half = fieldSize * 0.5f;

            Sprite sprite = starSprite != null ? starSprite : BuildFallbackSprite();

            for (int i = 0; i < starCount; i++)
            {
                var go = new GameObject($"Star_{i}");
                go.transform.SetParent(transform, false);

                float x = (float)(rng.NextDouble() * fieldSize - half);
                float y = (float)(rng.NextDouble() * fieldSize - half);
                _localOffsets.Add(new Vector2(x, y));

                float scale = Mathf.Lerp(sizeRange.x, sizeRange.y, (float)rng.NextDouble());
                go.transform.localScale = new Vector3(scale, scale, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                float b = Mathf.Lerp(brightnessRange.x, brightnessRange.y, (float)rng.NextDouble());
                var c = tint;
                c.a *= b;
                sr.color = c;
                sr.sortingOrder = sortingOrder;

                _stars.Add(go.transform);
            }
        }

        private static Sprite BuildFallbackSprite()
        {
            // 4x4 soft white dot baked at runtime.
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "StarfieldDotTex"
            };
            var px = new Color[16];
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                float dx = (x + 0.5f) - 2f;
                float dy = (y + 0.5f) - 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 2f;
                float a = Mathf.Clamp01(1f - d);
                px[y * 4 + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private void LateUpdate()
        {
            if (trackedCamera == null) trackedCamera = Camera.main;
            if (trackedCamera == null || _stars.Count == 0) return;

            Vector3 camPos = trackedCamera.transform.position;
            // Reposition stars relative to a camera-tracked anchor, wrapped into a fieldSize torus.
            // Apply the parallax factor: the anchor moves slower than the camera so the field
            // appears to drift slightly while staying centered.
            float driftX = (camPos.x - _origin.x) * (1f - parallaxFactor);
            float driftY = (camPos.y - _origin.y) * (1f - parallaxFactor);
            float half = fieldSize * 0.5f;

            for (int i = 0; i < _stars.Count; i++)
            {
                var off = _localOffsets[i];
                float x = Mathf.Repeat(off.x + driftX + half, fieldSize) - half;
                float y = Mathf.Repeat(off.y + driftY + half, fieldSize) - half;
                _stars[i].position = new Vector3(camPos.x + x, camPos.y + y, transform.position.z);
            }
        }
    }
}
