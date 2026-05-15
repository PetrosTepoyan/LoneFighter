using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.UI.BossIntro.EditorTools
{
    /// <summary>
    /// Editor menu entry under <c>LoneFighter → UI → Generate Boss Meta</c> that
    /// materializes a starter <see cref="BossMeta"/> asset for the Warden mini-boss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asset is written to <c>Assets/Data/BossIntro/Warden.asset</c>. The
    /// generator is idempotent: re-running it updates the existing asset in place
    /// rather than creating a duplicate, so any scene references survive.
    /// </para>
    /// <para>
    /// A matching <see cref="BossMetaRegistry"/> is generated next to it at
    /// <c>Assets/Data/BossIntro/BossMetaRegistry.asset</c> and the Warden meta is
    /// inserted into its list, so the registry is ready to use the first time the
    /// game runs.
    /// </para>
    /// </remarks>
    public static class BossMetaGenerator
    {
        private const string OutputDir          = "Assets/Data/BossIntro";
        private const string WardenAssetPath    = "Assets/Data/BossIntro/Warden.asset";
        private const string RegistryAssetPath  = "Assets/Data/BossIntro/BossMetaRegistry.asset";

        [MenuItem("LoneFighter/UI/Generate Boss Meta", priority = 110)]
        public static void GenerateAll()
        {
            EnsureFolder(OutputDir);

            var warden = LoadOrCreate<BossMeta>(WardenAssetPath);
            ConfigureWarden(warden);
            EditorUtility.SetDirty(warden);

            var registry = LoadOrCreate<BossMetaRegistry>(RegistryAssetPath);
            EnsureRegistryContains(registry, warden);
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);

            Debug.Log($"[LoneFighter.BossIntro] Generated Warden BossMeta at {WardenAssetPath} and registered it in {RegistryAssetPath}.");
        }

        private static void ConfigureWarden(BossMeta meta)
        {
            meta.enemyDataName = "Warden";
            meta.title         = "WARDEN";
            meta.subtitle      = "Keeper of the Arena";
            // Deep, regal purple — the asset can be re-tinted later in the inspector.
            meta.accentColor   = new Color(0.55f, 0.20f, 0.85f, 1f);
            // Stinger is intentionally left null — designers wire a clip when one exists.
        }

        private static void EnsureRegistryContains(BossMetaRegistry registry, BossMeta meta)
        {
            // The registry's internal list is private; use SerializedObject so we touch it
            // through the public Unity serialisation path (and so we don't widen the type's
            // public surface just for editor tooling).
            var so = new SerializedObject(registry);
            var listProp = so.FindProperty("entries");
            if (listProp == null) return;

            // Skip if already present.
            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == meta) return;
            }

            int insertAt = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(insertAt);
            listProp.GetArrayElementAtIndex(insertAt).objectReferenceValue = meta;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            // AssetDatabase.CreateFolder needs each segment to exist. Walk the path
            // and create any missing segment under "Assets/".
            var parts = folderPath.Split('/');
            string accumulated = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{accumulated}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(accumulated, parts[i]);
                }
                accumulated = next;
            }
            // Belt-and-braces — make sure the OS folder is present too.
            var absolute = Path.Combine(Directory.GetCurrentDirectory(), folderPath);
            if (!Directory.Exists(absolute)) Directory.CreateDirectory(absolute);
        }
    }
}
