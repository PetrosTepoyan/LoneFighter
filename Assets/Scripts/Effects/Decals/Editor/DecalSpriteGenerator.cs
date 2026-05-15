#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Effects.Decals.EditorTools
{
    /// <summary>
    /// Editor utility that procedurally generates the eight decal placeholder PNGs the
    /// runtime decal system consumes:
    ///   • 4 blood splatters — random-walk blobs
    ///   • 2 scorch marks    — irregular sooty rings
    ///   • 2 bullet holes    — small chip-on-floor circles
    ///
    /// Output: <c>Assets/Sprites/Generated/Decals/</c>
    /// Import settings (applied on a second pass after AssetDatabase.Refresh):
    ///   - Texture Type: Sprite (Single)
    ///   - Pixels Per Unit: 32
    ///   - Filter: Point
    ///   - alphaIsTransparency: true
    ///   - Mips: off
    ///   - Wrap: Clamp
    ///   - Pivot: Center (0.5, 0.5)
    ///
    /// Algorithms:
    ///   * Blood splatter: start at center, run N independent random walkers; each step
    ///     plants a small filled circle, biased toward staying within a soft radius cap
    ///     so the blob has a body but with droplet protrusions.
    ///   * Scorch mark: stamp a ring of darker-than-clear pixels along an irregular
    ///     polar curve (radius = base ± noise) then add a few outer specks for crust.
    ///   * Bullet hole: filled ellipse with a darker inner core.
    /// </summary>
    public static class DecalSpriteGenerator
    {
        // ---------- Public menu entry ----------

        private const string MenuPath           = "LoneFighter/FX/Generate Decal Sprites";
        private const string OutputDirAssetPath = "Assets/Sprites/Generated/Decals";
        private const int    PixelsPerUnit      = 32;

        // Texture sizes — chosen to match SPRITES convention (small, point-filtered).
        private const int BloodSize   = 48;
        private const int ScorchSize  = 64;
        private const int BulletSize  = 16;

        // ---------- Generator manifest ----------

        private struct Job
        {
            public string fileName;
            public Func<int, Texture2D> build;     // takes a per-call seed for variation
            public int seed;                       // deterministic seed so re-runs are stable
        }

        private static readonly Job[] Jobs = new[]
        {
            new Job { fileName = "Blood_01.png", build = BuildBloodSplatter, seed = 1101 },
            new Job { fileName = "Blood_02.png", build = BuildBloodSplatter, seed = 1207 },
            new Job { fileName = "Blood_03.png", build = BuildBloodSplatter, seed = 1303 },
            new Job { fileName = "Blood_04.png", build = BuildBloodSplatter, seed = 1409 },
            new Job { fileName = "Scorch_01.png", build = BuildScorchMark,   seed = 2101 },
            new Job { fileName = "Scorch_02.png", build = BuildScorchMark,   seed = 2207 },
            new Job { fileName = "Bullet_01.png", build = BuildBulletHole,   seed = 3101 },
            new Job { fileName = "Bullet_02.png", build = BuildBulletHole,   seed = 3207 },
        };

        [MenuItem(MenuPath)]
        public static void GenerateAll()
        {
            try
            {
                EnsureOutputDirectory();
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < Jobs.Length; i++)
                {
                    var job = Jobs[i];
                    EditorUtility.DisplayProgressBar("Decals", job.fileName, i / (float)Jobs.Length);
                    WriteOne(job);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[DecalSpriteGenerator] " + ex);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                // Apply importer settings now that the asset exists on disk.
                for (int i = 0; i < Jobs.Length; i++)
                {
                    ApplyImportSettings(AssetPathFor(Jobs[i].fileName));
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[DecalSpriteGenerator] Wrote {Jobs.Length} decal sprites to {OutputDirAssetPath}.");
            }
        }

        // ---------- Texture builders ----------

        /// <summary>
        /// Random-walk blob. White-on-clear so the runtime tint sets the actual color.
        /// We use a per-pixel float "ink" accumulator so overlapping walker steps build
        /// up smoothly, then threshold + soften the alpha for a slightly fuzzy edge.
        /// </summary>
        private static Texture2D BuildBloodSplatter(int seed)
        {
            int W = BloodSize, H = BloodSize;
            var rng = new System.Random(seed);

            float[] ink = new float[W * H];

            // Run several walkers from the center. More walkers = chunkier core; long walks
            // produce droplet tails. Tuned by eye on a 48px canvas.
            int walkers = 5;
            int stepsPerWalker = 90;
            float cx = W * 0.5f;
            float cy = H * 0.5f;

            for (int w = 0; w < walkers; w++)
            {
                float x = cx, y = cy;
                float vx = ((float)rng.NextDouble() - 0.5f) * 2f;
                float vy = ((float)rng.NextDouble() - 0.5f) * 2f;
                for (int s = 0; s < stepsPerWalker; s++)
                {
                    // Slight inward pull so the walker doesn't escape the canvas.
                    float pullX = (cx - x) * 0.02f;
                    float pullY = (cy - y) * 0.02f;
                    vx += ((float)rng.NextDouble() - 0.5f) * 1.2f + pullX;
                    vy += ((float)rng.NextDouble() - 0.5f) * 1.2f + pullY;
                    // Clamp velocity so walkers don't drift too fast.
                    float v = Mathf.Sqrt(vx * vx + vy * vy);
                    if (v > 1.5f) { vx *= 1.5f / v; vy *= 1.5f / v; }
                    x += vx;
                    y += vy;
                    StampDisc(ink, W, H, x, y, radius: 2.2f, strength: 1f);
                }
            }

            // A small number of detached "droplets" outside the main mass for character.
            int droplets = 6;
            for (int d = 0; d < droplets; d++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist  = (float)(rng.NextDouble() * (W * 0.4f)) + W * 0.15f;
                float dx = cx + Mathf.Cos(angle) * dist;
                float dy = cy + Mathf.Sin(angle) * dist;
                float rad = 1.0f + (float)rng.NextDouble() * 1.5f;
                StampDisc(ink, W, H, dx, dy, rad, 1f);
            }

            // Threshold + soft edge: anywhere ink >= 0.5 is opaque, then ramp down.
            var px = new Color32[W * H];
            for (int i = 0; i < ink.Length; i++)
            {
                float a = Mathf.Clamp01(ink[i]);
                // Slight contrast boost so soft halo doesn't muddy the silhouette.
                a = Mathf.SmoothStep(0.2f, 0.7f, a);
                if (a <= 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                byte ab = (byte)Mathf.RoundToInt(a * 255f);
                // White RGB — runtime tint multiplies this to whatever color the user wants.
                px[i] = new Color32(255, 255, 255, ab);
            }
            return Finalize(W, H, "BloodSplatter", px);
        }

        /// <summary>
        /// Scorch mark: irregular sooty ring, semi-transparent, with a slight inner
        /// darker fill so it reads as a burn rather than a hole.
        /// </summary>
        private static Texture2D BuildScorchMark(int seed)
        {
            int W = ScorchSize, H = ScorchSize;
            var rng = new System.Random(seed);

            float[] ink = new float[W * H];

            float cx = W * 0.5f;
            float cy = H * 0.5f;
            float baseRadius = W * 0.32f;

            // Stamp a ring of soft discs along an angle-noisy radius.
            int ringSteps = 96;
            for (int i = 0; i < ringSteps; i++)
            {
                float t = i / (float)ringSteps;
                float angle = t * Mathf.PI * 2f;
                // Two octaves of "noise" via sine sums seeded by rng.
                float wob = ((float)rng.NextDouble() - 0.5f) * 4f
                          + Mathf.Sin(angle * 3f + (float)rng.NextDouble() * 6f) * 2f;
                float r = baseRadius + wob;
                float x = cx + Mathf.Cos(angle) * r;
                float y = cy + Mathf.Sin(angle) * r;
                StampDisc(ink, W, H, x, y, radius: 3.5f, strength: 0.8f);
            }

            // Inner fill — softer than the ring so the ring still reads as a darker rim.
            int fillStamps = 40;
            for (int i = 0; i < fillStamps; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist  = (float)(rng.NextDouble()) * (baseRadius - 4f);
                float x = cx + Mathf.Cos(angle) * dist;
                float y = cy + Mathf.Sin(angle) * dist;
                StampDisc(ink, W, H, x, y, radius: 4f, strength: 0.25f);
            }

            // Outer specks — embers / soot debris.
            int specks = 12;
            for (int i = 0; i < specks; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist = baseRadius + 4f + (float)rng.NextDouble() * 6f;
                float x = cx + Mathf.Cos(angle) * dist;
                float y = cy + Mathf.Sin(angle) * dist;
                StampDisc(ink, W, H, x, y, radius: 1.2f, strength: 1f);
            }

            var px = new Color32[W * H];
            for (int i = 0; i < ink.Length; i++)
            {
                float a = Mathf.Clamp01(ink[i]);
                a = Mathf.SmoothStep(0.1f, 0.85f, a);
                if (a <= 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                byte ab = (byte)Mathf.RoundToInt(a * 255f);
                // White-on-clear so the runtime tint (dark grey) controls the visual.
                px[i] = new Color32(255, 255, 255, ab);
            }
            return Finalize(W, H, "ScorchMark", px);
        }

        /// <summary>
        /// Tiny bullet hole: filled circle with a slightly off-center "dark core" for
        /// the impact point. The runtime tint makes it dark; this texture is pure
        /// shape/alpha.
        /// </summary>
        private static Texture2D BuildBulletHole(int seed)
        {
            int W = BulletSize, H = BulletSize;
            var rng = new System.Random(seed);

            float[] ink = new float[W * H];
            float cx = W * 0.5f + ((float)rng.NextDouble() - 0.5f) * 1.5f;
            float cy = H * 0.5f + ((float)rng.NextDouble() - 0.5f) * 1.5f;

            // Outer ring (lighter alpha)
            StampDisc(ink, W, H, cx, cy, radius: W * 0.30f, strength: 0.55f);
            // Inner core (full alpha)
            StampDisc(ink, W, H, cx, cy, radius: W * 0.18f, strength: 1f);

            // A couple of asymmetric chips — fragments around the hole.
            int chips = 3;
            for (int i = 0; i < chips; i++)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float dist = W * 0.32f;
                float x = cx + Mathf.Cos(angle) * dist;
                float y = cy + Mathf.Sin(angle) * dist;
                StampDisc(ink, W, H, x, y, radius: 0.9f, strength: 0.8f);
            }

            var px = new Color32[W * H];
            for (int i = 0; i < ink.Length; i++)
            {
                float a = Mathf.Clamp01(ink[i]);
                a = Mathf.SmoothStep(0.15f, 0.9f, a);
                if (a <= 0f) { px[i] = new Color32(0, 0, 0, 0); continue; }
                byte ab = (byte)Mathf.RoundToInt(a * 255f);
                px[i] = new Color32(255, 255, 255, ab);
            }
            return Finalize(W, H, "BulletHole", px);
        }

        // ---------- Raster helpers ----------

        /// <summary>
        /// Stamps a filled disc with a soft falloff into the ink buffer. <c>strength</c>
        /// multiplies the contribution so callers can layer "weak" and "strong" stamps.
        /// </summary>
        private static void StampDisc(float[] ink, int w, int h, float cx, float cy, float radius, float strength)
        {
            if (radius <= 0f) return;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius + 1));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius + 1));
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float d2 = dx * dx + dy * dy;
                if (d2 > r2) continue;
                float t = 1f - Mathf.Sqrt(d2) / radius; // 1 at center → 0 at rim
                int idx = y * w + x;
                ink[idx] = Mathf.Min(1f, ink[idx] + t * strength);
            }
        }

        private static Texture2D Finalize(int w, int h, string name, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                alphaIsTransparency = true,
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---------- IO / importer ----------

        private static string AssetPathFor(string fileName) => $"{OutputDirAssetPath}/{fileName}";

        private static string AbsolutePathFor(string fileName)
        {
            var parent = Directory.GetParent(Application.dataPath);
            string projectRoot = parent != null ? parent.FullName : Application.dataPath;
            return Path.Combine(projectRoot, AssetPathFor(fileName).Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureOutputDirectory()
        {
            string abs = Path.Combine(Application.dataPath, "Sprites", "Generated", "Decals");
            if (!Directory.Exists(abs))
            {
                Directory.CreateDirectory(abs);
                AssetDatabase.Refresh();
            }
        }

        private static void WriteOne(Job job)
        {
            Texture2D tex = null;
            try
            {
                tex = job.build(job.seed);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(AbsolutePathFor(job.fileName), png);
                AssetDatabase.ImportAsset(AssetPathFor(job.fileName), ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void ApplyImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[DecalSpriteGenerator] No TextureImporter found for {assetPath}");
                return;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode          = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.npotScale           = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot     = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            settings.spriteExtrude   = 0;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}
#endif
