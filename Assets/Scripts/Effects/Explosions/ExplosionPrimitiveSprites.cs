using UnityEngine;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Lazily-generated procedural sprites used by the pooled explosion visuals when
    /// no artist-authored sprite is assigned. Lets the entire explosion library work
    /// from a clean checkout with zero sprite art — the visual quality bar with these
    /// primitives is "shippable placeholder", not "final art".
    ///
    /// All sprites are cached statically so a project with hundreds of pooled
    /// explosion instances pays the texture creation cost at most once per sprite.
    /// Textures use point filtering so they composite predictably under URP 2D bloom.
    /// </summary>
    internal static class ExplosionPrimitiveSprites
    {
        private static Sprite _whiteSquare;
        private static Sprite _softDisc;
        private static Sprite _ringSprite;

        /// <summary>Solid white 8x8 square, used by the core flash and debris chunks.</summary>
        public static Sprite WhiteSquare()
        {
            if (_whiteSquare != null) return _whiteSquare;
            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "ExplosionPrim_WhiteSquare",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            _whiteSquare = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _whiteSquare.name = "ExplosionPrim_WhiteSquare";
            return _whiteSquare;
        }

        /// <summary>Radial gradient disc — solid white center, fades to transparent edge. Used for smoke.</summary>
        public static Sprite SoftDisc()
        {
            if (_softDisc != null) return _softDisc;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "ExplosionPrim_SoftDisc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // gentler falloff
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            _softDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _softDisc.name = "ExplosionPrim_SoftDisc";
            return _softDisc;
        }

        /// <summary>Hollow ring — bright at radius 0.7 of half-size, transparent at center and edge. Used by the shockwave.</summary>
        public static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "ExplosionPrim_Ring",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxR = size * 0.5f;
            float peak = 0.78f; // normalized radius of the brightest ring point
            float thickness = 0.18f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxR;
                if (d > 1f) { pixels[y * size + x] = new Color32(255, 255, 255, 0); continue; }
                float diff = Mathf.Abs(d - peak) / thickness;
                float a = Mathf.Clamp01(1f - diff);
                a = a * a;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _ringSprite.name = "ExplosionPrim_Ring";
            return _ringSprite;
        }
    }
}
