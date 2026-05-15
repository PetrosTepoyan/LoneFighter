using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Effects.Trails;

namespace LoneFighter.Effects.Trails.EditorTools
{
    /// <summary>
    /// One-click generator for the four starter <see cref="TrailProfile"/> assets
    /// under <c>Assets/Data/TrailProfiles/</c>:
    ///
    ///   - <c>Trail_Pistol</c>           white core, hard fade
    ///   - <c>Trail_SpreadShotgun</c>    orange spark, short lifetime
    ///   - <c>Trail_OrbitalBlade</c>     cyan ribbon, long lifetime
    ///   - <c>Trail_ChainLightning</c>   electric blue, fast crackle
    ///
    /// Idempotent: re-running updates the existing assets in place so prefab
    /// references survive across regenerations.
    /// </summary>
    public static class TrailProfileGenerator
    {
        private const string DataDir = "Assets/Data";
        private const string TrailDir = "Assets/Data/TrailProfiles";

        [MenuItem("LoneFighter/FX/Generate Trail Profiles", priority = 30)]
        public static void GenerateAll()
        {
            EnsureFolder(TrailDir);

            CreateOrUpdate("Trail_Pistol",         BuildPistol);
            CreateOrUpdate("Trail_SpreadShotgun",  BuildSpreadShotgun);
            CreateOrUpdate("Trail_OrbitalBlade",   BuildOrbitalBlade);
            CreateOrUpdate("Trail_ChainLightning", BuildChainLightning);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LoneFighter.Trails] Generated 4 trail profiles in " + TrailDir);
        }

        // ------------------------------------------------------------------
        // Per-profile authoring
        // ------------------------------------------------------------------

        // Pistol: clean white streak. Bright HDR head, fades to fully transparent over a quarter second.
        private static void BuildPistol(TrailProfile p)
        {
            p.colorGradient = Gradient(
                new[] { (Color.white * 2.0f, 0f), (Color.white, 0.5f), (Color.white * 0.6f, 1f) },
                new[] { (1f, 0f), (0.6f, 0.5f), (0f, 1f) });
            p.widthCurve   = ThreePoint(1f, 0.55f, 0f);
            p.widthScale   = 0.18f;
            p.fadeTime     = 0.22f;
            p.minVertexDistance = 0.05f;
            p.additive     = true;
            p.sortingOrder = 5;
        }

        // Spread shotgun pellets: warm orange spark, short and punchy.
        private static void BuildSpreadShotgun(TrailProfile p)
        {
            Color hot  = new Color(1.00f, 0.55f, 0.10f, 1f) * 2.5f;
            Color warm = new Color(1.00f, 0.35f, 0.05f, 1f) * 1.2f;
            Color cool = new Color(0.55f, 0.12f, 0.02f, 1f);
            p.colorGradient = Gradient(
                new[] { (hot, 0f), (warm, 0.4f), (cool, 1f) },
                new[] { (1f, 0f), (0.7f, 0.3f), (0f, 1f) });
            p.widthCurve   = ThreePoint(1f, 0.4f, 0f);
            p.widthScale   = 0.16f;
            p.fadeTime     = 0.18f;
            p.minVertexDistance = 0.04f;
            p.additive     = true;
            p.sortingOrder = 5;
        }

        // Orbital blade: bright cyan ribbon. Long-lived because the blade keeps circling.
        private static void BuildOrbitalBlade(TrailProfile p)
        {
            Color core = new Color(0.55f, 1.00f, 1.00f, 1f) * 2.2f;
            Color body = new Color(0.20f, 0.85f, 1.00f, 1f) * 1.4f;
            Color tail = new Color(0.05f, 0.45f, 0.85f, 1f);
            p.colorGradient = Gradient(
                new[] { (core, 0f), (body, 0.5f), (tail, 1f) },
                new[] { (1f, 0f), (0.75f, 0.5f), (0f, 1f) });
            p.widthCurve   = ThreePoint(1f, 0.7f, 0.05f);
            p.widthScale   = 0.28f;
            p.fadeTime     = 0.55f;
            p.minVertexDistance = 0.03f;
            p.additive     = true;
            p.sortingOrder = 6;
        }

        // Chain lightning: electric blue with white-hot core, short and snappy.
        private static void BuildChainLightning(TrailProfile p)
        {
            Color core  = new Color(0.85f, 0.95f, 1.00f, 1f) * 3.0f;
            Color flash = new Color(0.55f, 0.75f, 1.00f, 1f) * 1.8f;
            Color edge  = new Color(0.25f, 0.40f, 0.95f, 1f);
            p.colorGradient = Gradient(
                new[] { (core, 0f), (flash, 0.35f), (edge, 1f) },
                new[] { (1f, 0f), (0.8f, 0.4f), (0f, 1f) });
            p.widthCurve   = ThreePoint(1f, 0.6f, 0.1f);
            p.widthScale   = 0.22f;
            p.fadeTime     = 0.15f;
            p.minVertexDistance = 0.02f;
            p.additive     = true;
            p.sortingOrder = 7;
        }

        // ------------------------------------------------------------------
        // Small authoring helpers
        // ------------------------------------------------------------------

        private static void CreateOrUpdate(string name, System.Action<TrailProfile> author)
        {
            string path = $"{TrailDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TrailProfile>(path);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TrailProfile>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }
            author(asset);
            EditorUtility.SetDirty(asset);
            Debug.Log($"[LoneFighter.Trails] {(created ? "Created" : "Updated")} {path}");
        }

        private static Gradient Gradient((Color color, float time)[] colorKeys,
                                         (float alpha, float time)[] alphaKeys)
        {
            var g = new Gradient { mode = GradientMode.Blend };
            var ck = new GradientColorKey[colorKeys.Length];
            for (int i = 0; i < colorKeys.Length; i++)
                ck[i] = new GradientColorKey(colorKeys[i].color, colorKeys[i].time);
            var ak = new GradientAlphaKey[alphaKeys.Length];
            for (int i = 0; i < alphaKeys.Length; i++)
                ak[i] = new GradientAlphaKey(alphaKeys[i].alpha, alphaKeys[i].time);
            g.SetKeys(ck, ak);
            return g;
        }

        private static AnimationCurve ThreePoint(float a, float b, float c)
        {
            // Smooth curve from a (t=0) -> b (t=0.5) -> c (t=1) with auto-tangents.
            var curve = new AnimationCurve(
                new Keyframe(0f, a),
                new Keyframe(0.5f, b),
                new Keyframe(1f, c));
            for (int i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            return curve;
        }

        // Mirror of FxAssetPaths.EnsureFolder so we don't take an editor-assembly dependency.
        private static void EnsureFolder(string assetFolderPath)
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
}
