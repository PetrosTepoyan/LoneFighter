#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Enemies.Patterns.EditorTools
{
    /// <summary>
    /// Editor menu that creates the six starter <see cref="BulletPattern"/>
    /// ScriptableObject assets under <c>Assets/Data/BulletPatterns/</c>.
    ///
    /// Idempotent: re-running the menu only creates assets that don't already
    /// exist, so designer tweaks made in the inspector are preserved.
    /// </summary>
    public static class BulletPatternGenerator
    {
        private const string OutputDir = "Assets/Data/BulletPatterns";

        [MenuItem("LoneFighter/Enemies/Generate Bullet Patterns")]
        public static void Generate()
        {
            EnsureFolder(OutputDir);

            int created = 0;
            int skipped = 0;

            created += CreateIfMissing<RadialBurst>("RadialBurst_Ring12", asset =>
            {
                SetPrivate(asset, "damage", 4f);
                SetPrivate(asset, "projectileLifetime", 3f);
                SetPrivate(asset, "count", 12);
                SetPrivate(asset, "speed", 4f);
                SetPrivate(asset, "randomizeOffset", true);
            }, ref skipped);

            created += CreateIfMissing<Spiral>("Spiral_DoubleArm", asset =>
            {
                SetPrivate(asset, "damage", 3f);
                SetPrivate(asset, "projectileLifetime", 3.5f);
                SetPrivate(asset, "speed", 3.5f);
                SetPrivate(asset, "angleStep", 17f);
                SetPrivate(asset, "arms", 2);
                SetPrivate(asset, "startAngle", 0f);
            }, ref skipped);

            created += CreateIfMissing<AimedBurst>("AimedBurst_Triple", asset =>
            {
                SetPrivate(asset, "damage", 5f);
                SetPrivate(asset, "projectileLifetime", 2.5f);
                SetPrivate(asset, "shots", 3);
                SetPrivate(asset, "delayBetweenShots", 0.08f);
                SetPrivate(asset, "speed", 6f);
                SetPrivate(asset, "reaimEachShot", false);
                SetPrivate(asset, "spreadDegrees", 2f);
            }, ref skipped);

            created += CreateIfMissing<LineShot>("LineShot_Sweep60", asset =>
            {
                SetPrivate(asset, "damage", 4f);
                SetPrivate(asset, "projectileLifetime", 3f);
                SetPrivate(asset, "count", 5);
                SetPrivate(asset, "sweepDegrees", 30f);
                SetPrivate(asset, "speed", 5f);
            }, ref skipped);

            created += CreateIfMissing<ConeSpray>("ConeSpray_Shotgun", asset =>
            {
                SetPrivate(asset, "damage", 3f);
                SetPrivate(asset, "projectileLifetime", 2f);
                SetPrivate(asset, "count", 6);
                SetPrivate(asset, "coneHalfAngle", 35f);
                SetPrivate(asset, "speed", 6f);
                SetPrivate(asset, "speedVariance", 1.2f);
            }, ref skipped);

            created += CreateIfMissing<Crossfire>("Crossfire_EightStar", asset =>
            {
                SetPrivate(asset, "damage", 4f);
                SetPrivate(asset, "projectileLifetime", 3.5f);
                SetPrivate(asset, "speed", 4.5f);
                SetPrivate(asset, "secondVolleyDelay", 0.18f);
                SetPrivate(asset, "alignToTarget", false);
            }, ref skipped);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BulletPatternGenerator] Created {created} pattern asset(s), skipped {skipped} existing under {OutputDir}.");
        }

        private static int CreateIfMissing<T>(string assetName, System.Action<T> configure, ref int skipped) where T : BulletPattern
        {
            string path = $"{OutputDir}/{assetName}.asset";
            if (AssetDatabase.LoadAssetAtPath<BulletPattern>(path) != null)
            {
                skipped++;
                return 0;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            configure?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return 1;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Set a [SerializeField] private field via reflection so the generator
        /// can stay in the Editor assembly without us having to widen the
        /// patterns' field visibility.
        /// </summary>
        private static void SetPrivate(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                type = type.BaseType;
            }
            if (field == null)
            {
                Debug.LogWarning($"[BulletPatternGenerator] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            // Coerce numeric types so callers can pass plain literals (int/float/bool).
            object coerced = value;
            if (field.FieldType == typeof(int) && value is float f) coerced = (int)f;
            else if (field.FieldType == typeof(float) && value is int i) coerced = (float)i;
            field.SetValue(target, coerced);
        }
    }
}
#endif
