#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.EditorTools.Art
{
    /// <summary>
    /// Editor menu entry: <b>LoneFighter → Art → Sprite Generator</b>.
    ///
    /// Generates placeholder PNG sprites under <c>Assets/Sprites/Generated/</c> and
    /// configures the importer for crisp pixel-art-friendly defaults (16 PPU, Point
    /// filtering, no mip, clamp wrap, alphaIsTransparency on).
    ///
    /// IMPORT-SETTINGS APPROACH:
    /// We use a direct, one-shot <see cref="TextureImporter"/> modification immediately
    /// after <see cref="AssetDatabase.ImportAsset(string)"/>. This is simpler than a
    /// global <see cref="AssetPostprocessor"/> because:
    ///   1. We don't want to alter the import settings of OTHER textures the user might
    ///      drop into the project later — only the ones THIS tool wrote.
    ///   2. The tool is run on-demand, so we know exactly which files need touching.
    /// If the user wants a permanent rule for the Generated/ folder, swapping to an
    /// AssetPostprocessor that filters on the path prefix is trivial (see comment in
    /// <see cref="ApplyImportSettings"/>).
    /// </summary>
    public class SpriteGeneratorWindow : EditorWindow
    {
        // ---------- Constants ----------

        private const string OutputDirAssetPath = "Assets/Sprites/Generated";
        private const string MenuPath           = "LoneFighter/Art/Sprite Generator";
        private const int    PixelsPerUnit      = 16;

        // ---------- Generator registry ----------

        /// <summary>One entry per art piece. Order here defines button order in the UI.</summary>
        private static readonly (string fileName, Func<Texture2D> build)[] Generators = new (string, Func<Texture2D>)[]
        {
            ("Player.png",        ProceduralSpriteGenerator.BuildPlayer),
            ("EnemyGrunt.png",    ProceduralSpriteGenerator.BuildEnemyGrunt),
            ("EnemyRunner.png",   ProceduralSpriteGenerator.BuildEnemyRunner),
            ("EnemyTank.png",     ProceduralSpriteGenerator.BuildEnemyTank),
            ("EnemySpitter.png",  ProceduralSpriteGenerator.BuildEnemySpitter),
            ("EnemyDasher.png",   ProceduralSpriteGenerator.BuildEnemyDasher),
            ("EnemyBomber.png",   ProceduralSpriteGenerator.BuildEnemyBomber),
            ("EnemyWarden.png",   ProceduralSpriteGenerator.BuildEnemyWarden),
            ("Projectile.png",    ProceduralSpriteGenerator.BuildProjectile),
            ("XpGem.png",         ProceduralSpriteGenerator.BuildXpGem),
            ("ExplosionMask.png", ProceduralSpriteGenerator.BuildExplosionMask),
            ("SparkMask.png",     ProceduralSpriteGenerator.BuildSparkMask),
            ("UiCircleIcon.png",  ProceduralSpriteGenerator.BuildUiCircleIcon),
        };

        // ---------- Menu ----------

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var win = GetWindow<SpriteGeneratorWindow>("Sprite Generator");
            win.minSize = new Vector2(360, 520);
            win.Show();
        }

        // ---------- GUI ----------

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("LoneFighter — Procedural Sprite Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates placeholder pixel-art PNGs under " + OutputDirAssetPath + "\n" +
                "Import settings: Sprite, 16 PPU, Point, Clamp, no mips, alphaIsTransparency.\n" +
                "Drop a same-named PNG into Assets/Sprites/ to override the placeholder.",
                MessageType.Info);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate All Sprites", GUILayout.Height(32)))
                {
                    GenerateAll();
                }
                if (GUILayout.Button("Open Output Folder", GUILayout.Height(32)))
                {
                    OpenOutputFolder();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Regenerate Individual Sprites", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in Generators)
            {
                if (GUILayout.Button(entry.fileName))
                {
                    GenerateOne(entry.fileName, entry.build);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------- Operations ----------

        private static void GenerateAll()
        {
            try
            {
                EnsureOutputDirectory();
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < Generators.Length; i++)
                {
                    var (fileName, build) = Generators[i];
                    float progress = i / (float)Generators.Length;
                    EditorUtility.DisplayProgressBar("Generating Sprites", fileName, progress);
                    WriteSprite(fileName, build);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SpriteGenerator] Failed: " + e);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                // Second pass: apply importer settings now that the assets exist.
                foreach (var (fileName, _) in Generators)
                {
                    ApplyImportSettings(AssetPathFor(fileName));
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[SpriteGenerator] Generated {Generators.Length} sprites into {OutputDirAssetPath}.");
            }
        }

        private static void GenerateOne(string fileName, Func<Texture2D> build)
        {
            try
            {
                EnsureOutputDirectory();
                EditorUtility.DisplayProgressBar("Generating Sprite", fileName, 0.5f);
                WriteSprite(fileName, build);
                AssetDatabase.Refresh();
                ApplyImportSettings(AssetPathFor(fileName));
                AssetDatabase.SaveAssets();
                Debug.Log($"[SpriteGenerator] Regenerated {fileName}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpriteGenerator] Failed on {fileName}: {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ---------- Filesystem ----------

        private static string AssetPathFor(string fileName) => $"{OutputDirAssetPath}/{fileName}";

        private static string AbsolutePathFor(string fileName)
        {
            // Application.dataPath ends in ".../Assets", strip and rebuild.
            var parent = Directory.GetParent(Application.dataPath);
            string projectRoot = parent != null ? parent.FullName : Application.dataPath;
            return Path.Combine(projectRoot, AssetPathFor(fileName).Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureOutputDirectory()
        {
            string abs = Path.Combine(Application.dataPath, "Sprites", "Generated");
            if (!Directory.Exists(abs))
            {
                Directory.CreateDirectory(abs);
                AssetDatabase.Refresh();
            }
        }

        private static void WriteSprite(string fileName, Func<Texture2D> build)
        {
            Texture2D tex = null;
            try
            {
                tex = build();
                byte[] png = tex.EncodeToPNG();
                string absPath = AbsolutePathFor(fileName);
                File.WriteAllBytes(absPath, png);
                AssetDatabase.ImportAsset(AssetPathFor(fileName), ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        // ---------- Importer setup ----------

        /// <summary>
        /// Configure the TextureImporter for one generated PNG.
        /// If you want this rule to apply automatically to ANY png placed under
        /// <c>Assets/Sprites/Generated/</c> (e.g. for hand-edited drop-ins), convert
        /// this method into an <see cref="AssetPostprocessor.OnPreprocessTexture"/>
        /// that gates on <c>assetPath.StartsWith(OutputDirAssetPath)</c>.
        /// </summary>
        private static void ApplyImportSettings(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SpriteGenerator] No TextureImporter found for {assetPath}");
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

            // Force center pivot via TextureImporterSettings (the only reliable way).
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot     = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            settings.spriteExtrude   = 0;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        // ---------- Open folder ----------

        private static void OpenOutputFolder()
        {
            EnsureOutputDirectory();
            string abs = Path.Combine(Application.dataPath, "Sprites", "Generated");
            EditorUtility.RevealInFinder(abs);
        }
    }
}
#endif
