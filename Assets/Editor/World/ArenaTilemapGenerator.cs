using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.EditorTools.World
{
    /// <summary>
    /// Editor utility that bakes a procedural 256x256 dark-grid texture into
    /// <c>Assets/Sprites/Generated/ArenaGrid.png</c>, then re-imports it with the
    /// pixel-art import settings the rest of the project uses (PPU 16, Point filter,
    /// Clamp wrap).
    ///
    /// Drag the imported sprite onto a SpriteRenderer set to <c>Tiled</c> draw mode in
    /// the Game scene to use it as the arena floor. The Console will print the exact
    /// follow-up steps after generation.
    /// </summary>
    public static class ArenaTilemapGenerator
    {
        private const string OutputDir = "Assets/Sprites/Generated";
        private const string OutputPath = OutputDir + "/ArenaGrid.png";

        // 256px texture, 16px cell — gives a 16x16 grid pattern that tiles perfectly at PPU 16.
        private const int TexSize = 256;
        private const int CellSize = 16;

        [MenuItem("LoneFighter/World/Generate Arena Background")]
        public static void Generate()
        {
            EnsureDirectory(OutputDir);
            var tex = BuildGridTexture(TexSize, CellSize);
            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(OutputPath, bytes);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            // Re-apply import settings so the sprite looks right.
            var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = CellSize; // 1 cell = 1 world unit
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                // Per-platform: keep it uncompressed on desktop & mobile (it's only 256x256).
                var settings = importer.GetDefaultPlatformTextureSettings();
                settings.format = TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(settings);

                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh();

            Debug.Log(
                "[LoneFighter.World] Generated arena grid sprite at " + OutputPath + "\n" +
                "Next steps:\n" +
                "  1) In Game.unity, create a child GameObject under your Arena root called 'Floor'.\n" +
                "  2) Add a SpriteRenderer; assign the generated sprite.\n" +
                "  3) Set Draw Mode = Tiled, Size = (arena width, arena height) e.g. 30 x 30.\n" +
                "  4) Set Sorting Layer = Background (or Order in Layer = -50 if no layer exists).\n" +
                "  5) Optional: drop the same sprite into a deeper layer of ParallaxBackground at a low parallax factor (~0.2)."
            );

            // Ping the file so the user sees it in the Project view.
            var asset = AssetDatabase.LoadAssetAtPath<Object>(OutputPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            // Recursively create missing folders.
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Builds a subtle dark-grid pattern: near-black base with slightly lighter thin lines
        /// every CellSize pixels. Adds a faint vignette per cell to avoid a flat repeat.
        /// </summary>
        private static Texture2D BuildGridTexture(int size, int cell)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "ArenaGrid",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color baseColor = new Color(0.055f, 0.06f, 0.08f, 1f);   // deep blue-black
            Color lineColor = new Color(0.13f, 0.16f, 0.22f, 1f);    // soft cool grey
            Color cellHighlight = new Color(0.08f, 0.10f, 0.13f, 1f); // mild center glow

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int yMod = y % cell;
                for (int x = 0; x < size; x++)
                {
                    int xMod = x % cell;
                    Color c = baseColor;

                    // Thin 1px line at every cell boundary.
                    bool onLine = (xMod == 0) || (yMod == 0);
                    if (onLine) c = lineColor;

                    // Soft cell-center highlight to add texture; cheap, no Perlin.
                    float fx = (xMod + 0.5f) / cell - 0.5f;
                    float fy = (yMod + 0.5f) / cell - 0.5f;
                    float d = Mathf.Sqrt(fx * fx + fy * fy);
                    float glow = Mathf.Clamp01(1f - d * 2.2f) * 0.35f;
                    c = Color.Lerp(c, cellHighlight, glow);

                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }
    }
}
