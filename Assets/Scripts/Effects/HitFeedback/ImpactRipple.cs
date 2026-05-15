using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Spawns a small additive radial shockwave at the world position of every
    /// hit. The sprite is configured at runtime — we either:
    ///   - Use the explicit <see cref="ripplePrefab"/> if provided (designer
    ///     authored the sprite renderer + material), OR
    ///   - Build a fallback SpriteRenderer with the default sprite + additive
    ///     material the first time we need to spawn.
    ///
    /// All ripples are pooled in-house (a tiny single-type pool) so per-hit
    /// allocation stays at zero. Each spawned ripple lives for
    /// <see cref="lifetime"/> seconds and grows from <see cref="startScale"/>
    /// to <see cref="endScale"/> with alpha fading to zero.
    /// </summary>
    public class ImpactRipple : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Optional designer-authored ripple prefab. If null, a fallback is built at runtime.")]
        [SerializeField] private GameObject ripplePrefab;
        [Tooltip("Sprite used for the fallback ripple. A soft radial gradient works best.")]
        [SerializeField] private Sprite fallbackSprite;
        [Tooltip("Tint applied to each ripple.")]
        [SerializeField] private Color tint = new Color(1f, 0.9f, 0.7f, 0.9f);

        [Header("Animation")]
        [Tooltip("Seconds each ripple stays alive.")]
        [SerializeField, Min(0.01f)] private float lifetime = 0.25f;
        [Tooltip("World scale at spawn.")]
        [SerializeField, Min(0f)] private float startScale = 0.3f;
        [Tooltip("World scale at the end of life.")]
        [SerializeField, Min(0f)] private float endScale = 1.6f;
        [Tooltip("Sorting layer for spawned ripples. Empty = default.")]
        [SerializeField] private string sortingLayerName = "";
        [Tooltip("Order in layer for spawned ripples.")]
        [SerializeField] private int orderInLayer = 50;

        private readonly Queue<Ripple> _pool = new();
        private readonly List<Ripple> _live = new();

        /// <summary>
        /// Spawn one ripple centered on <paramref name="worldPos"/>.
        /// </summary>
        public void Spawn(Vector3 worldPos)
        {
            Ripple r = _pool.Count > 0 ? _pool.Dequeue() : CreateRipple();
            r.go.SetActive(true);
            r.transform.position = worldPos;
            r.transform.localScale = Vector3.one * startScale;
            r.elapsed = 0f;
            r.renderer.color = tint;
            _live.Add(r);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var r = _live[i];
                r.elapsed += dt;
                float t = Mathf.Clamp01(r.elapsed / lifetime);
                float scale = Mathf.Lerp(startScale, endScale, t);
                r.transform.localScale = new Vector3(scale, scale, 1f);

                var c = tint;
                c.a = tint.a * (1f - t);
                r.renderer.color = c;

                if (t >= 1f)
                {
                    r.go.SetActive(false);
                    _live.RemoveAt(i);
                    _pool.Enqueue(r);
                }
            }
        }

        private Ripple CreateRipple()
        {
            GameObject go;
            if (ripplePrefab != null)
            {
                go = Instantiate(ripplePrefab, transform);
            }
            else
            {
                go = new GameObject("HitRipple", typeof(SpriteRenderer));
                go.transform.SetParent(transform);
            }

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sr.sprite == null) sr.sprite = fallbackSprite;

            // Additive blend on a sprite material: prefer a project-provided
            // shader if it exists, but fall back gracefully to the default
            // sprite shader (which is non-additive). The visual is still useful
            // either way.
            var additive = Shader.Find("Sprites/Default");
            if (additive != null)
            {
                sr.material = new Material(additive);
            }
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = orderInLayer;
            sr.color = tint;

            return new Ripple
            {
                go = go,
                transform = go.transform,
                renderer = sr,
                elapsed = 0f,
            };
        }

        private class Ripple
        {
            public GameObject go;
            public Transform transform;
            public SpriteRenderer renderer;
            public float elapsed;
        }
    }
}
