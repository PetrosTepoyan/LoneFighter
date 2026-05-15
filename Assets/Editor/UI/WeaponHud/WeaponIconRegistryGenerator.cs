#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Data;
using LoneFighter.UI.WeaponHud;

namespace LoneFighter.EditorTools.UI.WeaponHud
{
    /// <summary>
    /// Editor menu item: <c>LoneFighter → UI → Generate Weapon Icon Registry</c>.
    ///
    /// Creates (or repopulates) a default <see cref="WeaponIconRegistry"/> asset at
    /// <c>Assets/Data/UI/WeaponIconRegistry.asset</c>.
    ///
    /// Icon source priority (per weapon):
    /// <list type="number">
    /// <item>If <c>WeaponData.icon</c> is already assigned in the SO → reuse it.</item>
    /// <item>Otherwise generate a 64×64 PNG circle (hashed-from-name color) and
    ///       import it as a Sprite under <c>Assets/Data/UI/Generated/</c>.</item>
    /// </list>
    ///
    /// A default white-circle sprite is also generated and assigned to
    /// <c>WeaponIconRegistry.defaultIcon</c> so the HUD always has *something* to
    /// draw — even for weapons added after the registry was generated.
    /// </summary>
    public static class WeaponIconRegistryGenerator
    {
        private const string RegistryDir = "Assets/Data/UI";
        private const string GeneratedDir = "Assets/Data/UI/Generated";
        private const string RegistryAssetPath = RegistryDir + "/WeaponIconRegistry.asset";
        private const string DefaultIconPath = GeneratedDir + "/Icon_Default.png";

        [MenuItem("LoneFighter/UI/Generate Weapon Icon Registry", priority = 200)]
        public static void Generate()
        {
            EnsureFolder(RegistryDir);
            EnsureFolder(GeneratedDir);

            // 1. Default icon (white circle) — always present so HUD never draws nothing.
            Sprite defaultIcon = GetOrCreateCircleSprite(
                DefaultIconPath, new Color(1f, 1f, 1f, 1f), 0.92f);

            // 2. Find every WeaponData in the project.
            var weaponGuids = AssetDatabase.FindAssets("t:" + nameof(WeaponData));
            var entries = new List<WeaponIconRegistry.Entry>(weaponGuids.Length);

            foreach (var guid in weaponGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
                if (data == null || string.IsNullOrEmpty(data.displayName)) continue;

                // Prefer the icon already on WeaponData.
                Sprite icon = data.icon;
                if (icon == null)
                {
                    string slug = Sanitize(data.displayName);
                    string genPath = GeneratedDir + "/Icon_Weapon_" + slug + ".png";
                    icon = GetOrCreateCircleSprite(genPath, HashColor(data.displayName), 0.85f);
                }

                entries.Add(new WeaponIconRegistry.Entry
                {
                    weaponName = data.displayName,
                    icon = icon,
                });
            }

            // 3. Load-or-create the ScriptableObject and overwrite its fields.
            var registry = AssetDatabase.LoadAssetAtPath<WeaponIconRegistry>(RegistryAssetPath);
            bool created = false;
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<WeaponIconRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
                created = true;
            }

            registry.defaultIcon = defaultIcon;
            registry.entries = entries;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);

            Debug.Log($"[WeaponIconRegistry] {(created ? "Created" : "Updated")} {RegistryAssetPath} " +
                      $"with {entries.Count} entries.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
            }
            string result = sb.ToString();
            return string.IsNullOrEmpty(result) ? "Weapon" : result;
        }

        /// <summary>
        /// Deterministic pastel-ish color from a string so the same weapon name
        /// always lands on the same icon hue across regenerations.
        /// </summary>
        private static Color HashColor(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s) h = h * 31 + c;
                float hue = ((h & 0xFFFF) / 65535f);
                // Pull saturation and value into a bright-but-not-blinding range.
                return Color.HSVToRGB(hue, 0.65f, 1.0f);
            }
        }

        /// <summary>
        /// Loads the sprite at <paramref name="pngPath"/> if it already exists, otherwise
        /// writes a 64×64 RGBA32 anti-aliased circle PNG, imports it as a Sprite, and
        /// returns the imported asset.
        /// </summary>
        private static Sprite GetOrCreateCircleSprite(string pngPath, Color color, float radiusFrac)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (existing != null) return existing;

            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, mipChain: false, linear: false);
            var pixels = new Color32[W * H];
            float cx = (W - 1) * 0.5f;
            float cy = (H - 1) * 0.5f;
            float radius = (W * 0.5f) * radiusFrac;
            float edge = 1.25f; // anti-alias width in pixels

            Color32 fill = color;
            Color32 outline = new(20, 20, 28, 255); // dark outline for legibility

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius - edge)
                    {
                        pixels[y * W + x] = fill;
                    }
                    else if (dist <= radius)
                    {
                        // Outline ring (last ~1.25 px).
                        float a = Mathf.Clamp01((radius - dist) / edge);
                        pixels[y * W + x] = new Color32(outline.r, outline.g, outline.b, (byte)(a * 255));
                    }
                    else
                    {
                        pixels[y * W + x] = new Color32(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(pngPath, png);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);

            // Re-import as a sprite at 64 PPU with point filtering for crisp HUD pixels.
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 64f;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }
    }
}
#endif
