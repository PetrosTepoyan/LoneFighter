#if UNITY_EDITOR
using UnityEngine;

namespace LoneFighter.EditorTools.Art
{
    /// <summary>
    /// Pixel-precise (NO antialiasing) shape rasterizer that writes into a flat
    /// <see cref="Color32"/> buffer addressed as <c>pixels[y * w + x]</c>.
    ///
    /// All draw ops overwrite the destination pixel with the supplied color when the
    /// source alpha is non-zero. We deliberately do NOT alpha-blend — for placeholder
    /// pixel art we want sharp, predictable, bloom-friendly silhouettes.
    ///
    /// Origin (0,0) is the BOTTOM-LEFT (matches Unity texture convention).
    /// </summary>
    public static class ShapeRasterizer
    {
        // ---------- Internals ----------

        private static bool InBounds(int x, int y, int w, int h) =>
            x >= 0 && y >= 0 && x < w && y < h;

        private static void Plot(Color32[] pixels, int w, int h, int x, int y, Color32 color)
        {
            if (!InBounds(x, y, w, h)) return;
            if (color.a == 0) return;
            pixels[y * w + x] = color;
        }

        // ---------- Fill ----------

        /// <summary>Fills the entire buffer with the given color (use Palette.Clear to wipe).</summary>
        public static void Clear(Color32[] pixels, Color32 color)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        // ---------- Rect ----------

        /// <summary>Filled rectangle. <paramref name="x"/>,<paramref name="y"/> is the bottom-left corner.</summary>
        public static void DrawRect(Color32[] pixels, int w, int h, int x, int y, int rectW, int rectH, Color32 color)
        {
            int x0 = Mathf.Max(0, x);
            int y0 = Mathf.Max(0, y);
            int x1 = Mathf.Min(w - 1, x + rectW - 1);
            int y1 = Mathf.Min(h - 1, y + rectH - 1);
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
                Plot(pixels, w, h, px, py, color);
        }

        // ---------- Circle ----------

        /// <summary>Filled circle. Uses squared-radius compare for crisp pixel-art edges.</summary>
        public static void DrawCircle(Color32[] pixels, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            if (radius <= 0) return;
            int r2 = radius * radius;
            for (int py = cy - radius; py <= cy + radius; py++)
            {
                if (py < 0 || py >= h) continue;
                int dy = py - cy;
                for (int px = cx - radius; px <= cx + radius; px++)
                {
                    if (px < 0 || px >= w) continue;
                    int dx = px - cx;
                    if (dx * dx + dy * dy <= r2)
                        Plot(pixels, w, h, px, py, color);
                }
            }
        }

        /// <summary>Ring/annulus. <paramref name="thickness"/> is measured inward from <paramref name="radius"/>.</summary>
        public static void DrawRing(Color32[] pixels, int w, int h, int cx, int cy, int radius, int thickness, Color32 color)
        {
            if (radius <= 0 || thickness <= 0) return;
            int rOuter2 = radius * radius;
            int inner = Mathf.Max(0, radius - thickness);
            int rInner2 = inner * inner;
            for (int py = cy - radius; py <= cy + radius; py++)
            {
                if (py < 0 || py >= h) continue;
                int dy = py - cy;
                for (int px = cx - radius; px <= cx + radius; px++)
                {
                    if (px < 0 || px >= w) continue;
                    int dx = px - cx;
                    int d2 = dx * dx + dy * dy;
                    if (d2 <= rOuter2 && d2 >= rInner2)
                        Plot(pixels, w, h, px, py, color);
                }
            }
        }

        // ---------- Triangle (barycentric, integer) ----------

        private static int Edge(int ax, int ay, int bx, int by, int cx, int cy) =>
            (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);

        /// <summary>Filled triangle via barycentric inclusion test. No AA.</summary>
        public static void DrawTriangle(Color32[] pixels, int w, int h,
                                        int x0, int y0, int x1, int y1, int x2, int y2,
                                        Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxX = Mathf.Min(w - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxY = Mathf.Min(h - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

            // Total signed area; we accept either winding by checking sign consistency.
            int area = Edge(x0, y0, x1, y1, x2, y2);
            if (area == 0) return;

            for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                int e0 = Edge(x1, y1, x2, y2, px, py);
                int e1 = Edge(x2, y2, x0, y0, px, py);
                int e2 = Edge(x0, y0, x1, y1, px, py);
                bool inside = (e0 >= 0 && e1 >= 0 && e2 >= 0) ||
                              (e0 <= 0 && e1 <= 0 && e2 <= 0);
                if (inside)
                    Plot(pixels, w, h, px, py, color);
            }
        }

        // ---------- Hexagon ----------

        /// <summary>Flat-top regular hexagon, filled. <paramref name="radius"/> is center-to-vertex.</summary>
        public static void DrawHexagon(Color32[] pixels, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            // Decompose into 2 triangles + a center rectangle for crisp pixel hexagons.
            // Flat-top vertices (radius along x):
            //   (cx-r, cy), (cx-r/2, cy+h'), (cx+r/2, cy+h'), (cx+r, cy), (cx+r/2, cy-h'), (cx-r/2, cy-h')
            // where h' = round(r * sqrt(3)/2)
            int halfR = Mathf.RoundToInt(radius * 0.5f);
            int hh    = Mathf.RoundToInt(radius * 0.8660254f);

            int v0x = cx - radius, v0y = cy;
            int v1x = cx - halfR,  v1y = cy + hh;
            int v2x = cx + halfR,  v2y = cy + hh;
            int v3x = cx + radius, v3y = cy;
            int v4x = cx + halfR,  v4y = cy - hh;
            int v5x = cx - halfR,  v5y = cy - hh;

            DrawTriangle(pixels, w, h, v0x, v0y, v1x, v1y, v2x, v2y, color);
            DrawTriangle(pixels, w, h, v0x, v0y, v2x, v2y, v3x, v3y, color);
            DrawTriangle(pixels, w, h, v0x, v0y, v3x, v3y, v4x, v4y, color);
            DrawTriangle(pixels, w, h, v0x, v0y, v4x, v4y, v5x, v5y, color);
        }

        // ---------- Diamond ----------

        /// <summary>Filled diamond (square rotated 45°). <paramref name="radius"/> is the half-diagonal.</summary>
        public static void DrawDiamond(Color32[] pixels, int w, int h, int cx, int cy, int radius, Color32 color)
        {
            for (int py = cy - radius; py <= cy + radius; py++)
            {
                if (py < 0 || py >= h) continue;
                int span = radius - Mathf.Abs(py - cy);
                for (int px = cx - span; px <= cx + span; px++)
                    Plot(pixels, w, h, px, py, color);
            }
        }

        // ---------- Chevron ----------

        /// <summary>
        /// Filled chevron / arrow head pointing along +X (right).
        /// <paramref name="length"/> = pixel length tip-to-base, <paramref name="halfHeight"/> = wing half-height.
        /// </summary>
        public static void DrawChevron(Color32[] pixels, int w, int h, int cx, int cy, int length, int halfHeight, Color32 color)
        {
            // Two triangles forming an outer arrow + a notch carved out is the "true" chevron,
            // but for placeholder readability we use a solid filled arrow (forward triangle).
            int tipX  = cx + length / 2;
            int baseX = cx - length / 2;
            DrawTriangle(pixels, w, h,
                tipX, cy,
                baseX, cy + halfHeight,
                baseX, cy - halfHeight,
                color);
        }

        // ---------- Outline (8-neighbor) ----------

        /// <summary>
        /// Adds a hard-edged outline around every non-transparent pixel in the buffer.
        /// Runs <paramref name="thickness"/> passes of 8-neighbor erode/dilate detection.
        /// </summary>
        public static void OutlineCurrent(Color32[] pixels, int w, int h, Color32 color, int thickness = 1)
        {
            // We track which pixels were already opaque BEFORE this call so subsequent
            // passes treat freshly-written outline pixels as part of the silhouette.
            bool[] opaque = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) opaque[i] = pixels[i].a != 0;

            for (int pass = 0; pass < thickness; pass++)
            {
                bool[] newOpaque = (bool[])opaque.Clone();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (opaque[idx]) continue; // only paint into transparent cells
                    // 8-neighbor check
                    bool border = false;
                    for (int oy = -1; oy <= 1 && !border; oy++)
                    for (int ox = -1; ox <= 1 && !border; ox++)
                    {
                        if (ox == 0 && oy == 0) continue;
                        int nx = x + ox, ny = y + oy;
                        if (!InBounds(nx, ny, w, h)) continue;
                        if (opaque[ny * w + nx]) border = true;
                    }
                    if (border)
                    {
                        pixels[idx]   = color;
                        newOpaque[idx] = true;
                    }
                }
                opaque = newOpaque;
            }
        }

        // ---------- Radial gradient (for explosion masks) ----------

        /// <summary>
        /// Linearly interpolates from <paramref name="innerColor"/> at the center to
        /// <paramref name="outerColor"/> at <paramref name="radius"/>. Pixels beyond
        /// <paramref name="radius"/> are left untouched.
        ///
        /// Note: this is NOT anti-aliased per-pixel — but the gradient itself smoothly
        /// transitions alpha, which is what we want for additive bloom-friendly soft FX.
        /// </summary>
        public static void RadialGradient(Color32[] pixels, int w, int h, int cx, int cy, int radius,
                                          Color32 innerColor, Color32 outerColor)
        {
            if (radius <= 0) return;
            float rf = radius;
            for (int py = cy - radius; py <= cy + radius; py++)
            {
                if (py < 0 || py >= h) continue;
                int dy = py - cy;
                for (int px = cx - radius; px <= cx + radius; px++)
                {
                    if (px < 0 || px >= w) continue;
                    int dx = px - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > rf) continue;
                    float t = d / rf;
                    Color32 c = Lerp32(innerColor, outerColor, t);
                    // For FX masks we want to write even when destination is opaque so
                    // explosions overpaint cleanly.
                    pixels[py * w + px] = c;
                }
            }
        }

        private static Color32 Lerp32(Color32 a, Color32 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                (byte)(a.a + (b.a - a.a) * t));
        }
    }
}
#endif
