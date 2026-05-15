using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.EditorTools.Fx
{
    /// Central place for FX-generator output paths and small asset helpers.
    /// Keeping the paths in one file avoids drift between the prefab/material/profile generators.
    internal static class FxAssetPaths
    {
        public const string SettingsDir   = "Assets/Settings";
        public const string PrefabsFxDir  = "Assets/Prefabs/Fx";
        public const string PrefabsDir    = "Assets/Prefabs";

        public const string SoftCirclePath   = SettingsDir + "/Fx_SoftCircle.png";
        public const string AdditiveMatPath  = SettingsDir + "/Fx_Additive.mat";
        public const string PostProfilePath  = SettingsDir + "/PostProcessing.asset";

        public const string EnemyExplosionPath   = PrefabsFxDir + "/Fx_EnemyExplosion.prefab";
        public const string ProjectileImpactPath = PrefabsFxDir + "/Fx_ProjectileImpact.prefab";
        public const string PlayerHitPath        = PrefabsFxDir + "/Fx_PlayerHit.prefab";
        public const string XpPickupPath         = PrefabsFxDir + "/Fx_XpPickup.prefab";
        public const string LevelUpPath          = PrefabsFxDir + "/Fx_LevelUp.prefab";

        public const string FxRigPrefabPath        = PrefabsFxDir + "/FxRig.prefab";
        public const string CmPlayerFollowPath     = PrefabsDir + "/CM_PlayerFollow.prefab";

        /// Ensure a folder exists (relative to project root, e.g. "Assets/Prefabs/Fx").
        /// AssetDatabase.CreateFolder requires the parent to exist already, so we recurse.
        public static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string leaf   = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    /// Creates (or loads) a soft white radial-gradient Texture2D suitable as the particle base map.
    /// Saved as a PNG so it survives domain reloads and can be inspected / replaced by hand.
    internal static class FxSoftCircleTexture
    {
        private const int Size = 64;

        /// Returns the imported Texture2D, generating + importing it on first call.
        public static Texture2D GetOrCreate()
        {
            FxAssetPaths.EnsureFolder(FxAssetPaths.SettingsDir);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(FxAssetPaths.SoftCirclePath);
            if (existing != null) return existing;

            byte[] png = BuildSoftCirclePng();
            File.WriteAllBytes(FxAssetPaths.SoftCirclePath, png);
            AssetDatabase.ImportAsset(FxAssetPaths.SoftCirclePath, ImportAssetOptions.ForceSynchronousImport);

            ConfigureImporter(FxAssetPaths.SoftCirclePath);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(FxAssetPaths.SoftCirclePath);
        }

        private static byte[] BuildSoftCirclePng()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            var px = new Color[Size * Size];
            float half = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d  = Mathf.Sqrt(dx * dx + dy * dy);
                    // Smooth falloff from center=1 to edge=0; squared for a softer core that still kicks bloom.
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    px[y * Size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            byte[] data = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return data;
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType         = TextureImporterType.Default;
            importer.alphaSource         = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.sRGBTexture         = true;
            importer.SaveAndReimport();
        }
    }
}
