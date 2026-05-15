#if UNITY_EDITOR
using UnityEngine;

namespace LoneFighter.EditorTools.Art
{
    /// <summary>
    /// Builds RGBA32 <see cref="Texture2D"/> placeholders for every gameplay sprite
    /// LoneFighter needs. Each texture is small (16–64 px) so it stays crisp under
    /// 16-PPU pixel-art import settings, and is designed for additive bloom: bright
    /// saturated bodies with white-hot highlights against dark outlines.
    ///
    /// Each Build* method allocates a fresh Texture2D — callers (the EditorWindow)
    /// are responsible for encoding to PNG, importing, and destroying the temp object.
    /// </summary>
    public static class ProceduralSpriteGenerator
    {
        // ---------- Helpers ----------

        private static Texture2D NewTex(int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name        = name,
                filterMode  = FilterMode.Point,
                wrapMode    = TextureWrapMode.Clamp,
                alphaIsTransparency = true,
            };
            return tex;
        }

        private static Texture2D Finalize(Texture2D tex, Color32[] pixels)
        {
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private static Color32[] BlankBuffer(int w, int h)
        {
            var p = new Color32[w * h];
            // Color32 default is (0,0,0,0) which is exactly what we want, but be explicit.
            ShapeRasterizer.Clear(p, Palette.Clear);
            return p;
        }

        // ---------- Player ----------

        /// <summary>
        /// 32×32 cyan-blue circle body + lighter inner highlight + dark hard outline
        /// + a white-hot forward-facing notch on the right side (so designers can
        /// align rotation: sprite "forward" = +X).
        /// </summary>
        public static Texture2D BuildPlayer()
        {
            const int W = 32, H = 32;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;

            // Body
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 13, Palette.PlayerBlue);
            // Inner highlight (offset slightly up-left for a soft "lit from above" read)
            ShapeRasterizer.DrawCircle(px, W, H, cx - 2, cy + 2, 6, Palette.PlayerBlueLight);
            // Forward notch (small white-hot rect on the +X edge)
            ShapeRasterizer.DrawRect(px, W, H, cx + 9, cy - 2, 4, 5, Palette.PlayerNotch);
            // Hard outline
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, thickness: 1);

            return Finalize(NewTex(W, H, "Player"), px);
        }

        // ---------- Enemies ----------

        public static Texture2D BuildEnemyGrunt()
        {
            const int W = 24, H = 24;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 9, Palette.EnemyRed);
            // small light bite on top-left for visual interest
            ShapeRasterizer.DrawCircle(px, W, H, cx - 2, cy + 2, 3, Palette.WhiteHot);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemyGrunt"), px);
        }

        public static Texture2D BuildEnemyRunner()
        {
            const int W = 24, H = 24;
            var px = BlankBuffer(W, H);
            // Elongated chevron pointing right — runner = fast & directional
            ShapeRasterizer.DrawChevron(px, W, H, W / 2, H / 2, length: 18, halfHeight: 8, Palette.EnemyOrange);
            // Bright tip
            ShapeRasterizer.DrawRect(px, W, H, W - 6, H / 2 - 1, 3, 3, Palette.WhiteHot);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemyRunner"), px);
        }

        public static Texture2D BuildEnemyTank()
        {
            const int W = 40, H = 40;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            // Rounded square = big square with corner pixels removed via circle clipping trick:
            // draw a circle of radius=R, then a square that fills inside, gives rounded look.
            // Simpler: draw a slightly-undersized square, then overlay 4 quarter-circles at corners.
            int r = 15;
            ShapeRasterizer.DrawRect(px, W, H, cx - r, cy - r + 3, r * 2, r * 2 - 6, Palette.EnemyRed);
            ShapeRasterizer.DrawRect(px, W, H, cx - r + 3, cy - r, r * 2 - 6, r * 2, Palette.EnemyRed);
            // Corner circles to round it off
            ShapeRasterizer.DrawCircle(px, W, H, cx - r + 3, cy - r + 3, 3, Palette.EnemyRed);
            ShapeRasterizer.DrawCircle(px, W, H, cx + r - 3, cy - r + 3, 3, Palette.EnemyRed);
            ShapeRasterizer.DrawCircle(px, W, H, cx - r + 3, cy + r - 3, 3, Palette.EnemyRed);
            ShapeRasterizer.DrawCircle(px, W, H, cx + r - 3, cy + r - 3, 3, Palette.EnemyRed);
            // Darker armor studs (4 corners + center)
            ShapeRasterizer.DrawCircle(px, W, H, cx - 8, cy - 8, 2, Palette.EnemyRedDark);
            ShapeRasterizer.DrawCircle(px, W, H, cx + 8, cy - 8, 2, Palette.EnemyRedDark);
            ShapeRasterizer.DrawCircle(px, W, H, cx - 8, cy + 8, 2, Palette.EnemyRedDark);
            ShapeRasterizer.DrawCircle(px, W, H, cx + 8, cy + 8, 2, Palette.EnemyRedDark);
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 3, Palette.EnemyRedDark);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemyTank"), px);
        }

        public static Texture2D BuildEnemySpitter()
        {
            const int W = 28, H = 28;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            // Acid body
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 9, Palette.EnemyAcid);
            // Cannon protrusion (rectangle sticking right)
            ShapeRasterizer.DrawRect(px, W, H, cx + 6, cy - 2, 7, 5, Palette.EnemyAcid);
            // Bright muzzle mouth
            ShapeRasterizer.DrawRect(px, W, H, cx + 12, cy - 1, 1, 3, Palette.WhiteHot);
            // Dark inner eye
            ShapeRasterizer.DrawCircle(px, W, H, cx - 1, cy + 1, 2, Palette.OutlineDark);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemySpitter"), px);
        }

        public static Texture2D BuildEnemyDasher()
        {
            const int W = 28, H = 28;
            var px = BlankBuffer(W, H);
            // Magenta chevron silhouette — sharper / longer than the runner
            ShapeRasterizer.DrawChevron(px, W, H, W / 2, H / 2, length: 22, halfHeight: 10, Palette.EnemyMagenta);
            // Inner darker chevron "shadow" to give shape
            ShapeRasterizer.DrawChevron(px, W, H, W / 2 - 2, H / 2, length: 16, halfHeight: 6, Palette.WhiteHot);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemyDasher"), px);
        }

        public static Texture2D BuildEnemyBomber()
        {
            const int W = 28, H = 28;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2 - 2;
            // Body
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 9, Palette.EnemyOrange);
            // Fuse — vertical white line on top
            ShapeRasterizer.DrawRect(px, W, H, cx, cy + 9, 1, 5, Palette.WhiteHot);
            // Spark dot
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy + 14, 2, Palette.ProjectileYellow);
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy + 14, 1, Palette.WhiteHot);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "EnemyBomber"), px);
        }

        public static Texture2D BuildEnemyWarden()
        {
            const int W = 56, H = 56;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            // Purple hex
            ShapeRasterizer.DrawHexagon(px, W, H, cx, cy, 24, Palette.BossPurple);
            // Inner hex highlight
            ShapeRasterizer.DrawHexagon(px, W, H, cx, cy, 18, new Color32(
                (byte)Mathf.Min(255, Palette.BossPurple.r + 40),
                (byte)Mathf.Min(255, Palette.BossPurple.g + 40),
                (byte)Mathf.Min(255, Palette.BossPurple.b + 40),
                255));
            // Golden core
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 7, Palette.BossGold);
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 3, Palette.WhiteHot);
            // Thick outline (boss reads heavier)
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 2);
            return Finalize(NewTex(W, H, "EnemyWarden"), px);
        }

        // ---------- Projectile ----------

        public static Texture2D BuildProjectile()
        {
            const int W = 12, H = 24;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;

            // Outer yellow halo — vertical elongated radial fill.
            // We fake elongation by drawing a thinner radial then extending vertically.
            for (int y = 0; y < H; y++)
            {
                float ty = Mathf.Abs(y - cy) / (float)cy; // 0 at center, 1 at top/bottom
                float falloff = 1f - ty;
                if (falloff <= 0f) continue;
                int rowRadius = Mathf.RoundToInt(falloff * (W / 2));
                if (rowRadius <= 0) continue;
                for (int x = cx - rowRadius; x <= cx + rowRadius; x++)
                {
                    if (x < 0 || x >= W) continue;
                    float tx = Mathf.Abs(x - cx) / (float)rowRadius;
                    // Mix halo→core inward
                    Color32 outer = Palette.ProjectileYellow;
                    Color32 inner = Palette.WhiteHot;
                    float mix = tx; // outer at edge, inner at center
                    Color32 c = new Color32(
                        (byte)(inner.r + (outer.r - inner.r) * mix),
                        (byte)(inner.g + (outer.g - inner.g) * mix),
                        (byte)(inner.b + (outer.b - inner.b) * mix),
                        (byte)(255 * (1f - 0.5f * ty))); // soft falloff alpha
                    px[y * W + x] = c;
                }
            }
            // White core stripe down the middle
            ShapeRasterizer.DrawRect(px, W, H, cx, 2, 1, H - 4, Palette.WhiteHot);

            // Projectile is intentionally OUTLINE-LESS — additive streaks should bloom freely.
            return Finalize(NewTex(W, H, "Projectile"), px);
        }

        // ---------- XP Gem ----------

        public static Texture2D BuildXpGem()
        {
            const int W = 16, H = 16;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            ShapeRasterizer.DrawDiamond(px, W, H, cx, cy, 7, Palette.XpGreen);
            ShapeRasterizer.DrawDiamond(px, W, H, cx, cy, 3, Palette.XpHighlight);
            // Single white-hot facet pixel for sparkle
            ShapeRasterizer.DrawRect(px, W, H, cx - 1, cy + 1, 1, 1, Palette.WhiteHot);
            ShapeRasterizer.OutlineCurrent(px, W, H, Palette.OutlineDark, 1);
            return Finalize(NewTex(W, H, "XpGem"), px);
        }

        // ---------- FX Masks ----------

        /// <summary>64×64 soft white→transparent radial — additive-blend friendly explosion mask.</summary>
        public static Texture2D BuildExplosionMask()
        {
            const int W = 64, H = 64;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            // We want a smooth white-with-falling-alpha. Use RadialGradient from white→clear.
            ShapeRasterizer.RadialGradient(px, W, H, cx, cy, 30,
                innerColor: new Color32(255, 255, 255, 255),
                outerColor: new Color32(255, 255, 255,   0));
            // No outline — this is a soft mask.
            return Finalize(NewTex(W, H, "ExplosionMask"), px);
        }

        /// <summary>
        /// 16×32 vertical spark streak. White-hot core fading to transparent at the
        /// top and bottom — meant to be billboarded / stretched.
        /// </summary>
        public static Texture2D BuildSparkMask()
        {
            const int W = 16, H = 32;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            for (int y = 0; y < H; y++)
            {
                // Vertical alpha falloff (1 at center, 0 at top/bottom)
                float vy = 1f - Mathf.Abs((y - cy) / (float)cy);
                if (vy <= 0f) continue;
                for (int x = 0; x < W; x++)
                {
                    // Horizontal alpha falloff
                    float vx = 1f - Mathf.Abs((x - cx) / (float)cx);
                    if (vx <= 0f) continue;
                    float a = vx * vy;
                    byte aByte = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * W + x] = new Color32(255, 255, 255, aByte);
                }
            }
            return Finalize(NewTex(W, H, "SparkMask"), px);
        }

        // ---------- UI ----------

        /// <summary>
        /// 64×64 soft white circle on transparent. Intended as the BASE layer for
        /// upgrade icons — gameplay code overlays a glyph on top in-engine.
        /// </summary>
        public static Texture2D BuildUiCircleIcon()
        {
            const int W = 64, H = 64;
            var px = BlankBuffer(W, H);
            int cx = W / 2, cy = H / 2;
            // Solid white disc
            ShapeRasterizer.DrawCircle(px, W, H, cx, cy, 28, Palette.WhiteHot);
            // Soft outer halo (smaller alpha ring)
            ShapeRasterizer.DrawRing(px, W, H, cx, cy, 30, 2, new Color32(255, 255, 255, 96));
            // Dark thin outline at the very edge for legibility on light UI panels
            ShapeRasterizer.DrawRing(px, W, H, cx, cy, 28, 1, Palette.OutlineDark);
            return Finalize(NewTex(W, H, "UiCircleIcon"), px);
        }
    }
}
#endif
