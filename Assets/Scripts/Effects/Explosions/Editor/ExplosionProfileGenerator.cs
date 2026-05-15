using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Effects.Explosions;
using LoneFighter.Polish;

namespace LoneFighter.Effects.Explosions.EditorTools
{
    /// <summary>
    /// One-click generator for the six tiered <see cref="ExplosionProfile"/> assets
    /// under <c>Assets/Data/Explosions/</c>. Mirrors the pattern used by
    /// <c>TrailProfileGenerator</c> and <c>CritPopupPrefabGenerator</c>:
    ///   - Recursively creates parent folders.
    ///   - CreateOrUpdate per asset so re-running the menu is idempotent.
    ///   - Logs once per write + a summary at the end.
    ///
    /// After running this menu, drag each generated SO into the
    /// <see cref="ExplosionService.profiles"/> array (or rely on the runtime
    /// <c>ExplosionService.RebuildLookup()</c> if you populate the array via code).
    /// </summary>
    public static class ExplosionProfileGenerator
    {
        private const string DataDir = "Assets/Data";
        private const string ExplosionsDir = "Assets/Data/Explosions";

        [MenuItem("LoneFighter/FX/Generate Explosion Profiles", priority = 31)]
        public static void GenerateAll()
        {
            EnsureFolder(ExplosionsDir);

            CreateOrUpdate("Explosion_Tiny",   ExplosionTier.Tiny,   BuildTiny);
            CreateOrUpdate("Explosion_Small",  ExplosionTier.Small,  BuildSmall);
            CreateOrUpdate("Explosion_Medium", ExplosionTier.Medium, BuildMedium);
            CreateOrUpdate("Explosion_Large",  ExplosionTier.Large,  BuildLarge);
            CreateOrUpdate("Explosion_Mega",   ExplosionTier.Mega,   BuildMega);
            CreateOrUpdate("Explosion_Nuke",   ExplosionTier.Nuke,   BuildNuke);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LoneFighter.Explosions] Generated 6 explosion profiles in " + ExplosionsDir);
        }

        // ------------------------------------------------------------------
        // Per-tier authoring
        // ------------------------------------------------------------------

        private static void BuildTiny(ExplosionProfile p)
        {
            p.particleCount = 16;
            p.particleLifetime = 0.35f;
            p.particleSpeed = 4f;
            p.particleSize = 0.10f;
            p.hdrIntensity = 1.5f;
            p.colorGradient = WarmGradient(1f);

            p.ringRadius = 0.6f;
            p.ringDuration = 0.22f;
            p.ringColor = new Color(1f, 0.7f, 0.2f, 0.7f);

            p.flashLifetime = 0.10f;
            p.flashSize = 0.45f;
            p.flashColor = new Color(1f, 0.95f, 0.7f, 1f);

            p.debrisCount = 0;
            p.spawnSmoke = false;

            p.screenFlashAlpha = 0f;
            p.shakeCount = 0;
            p.hitStopSeconds = 0f;
            p.enableHaptics = false;
            p.damageMultiplier = 0.5f;
            p.damageRadius = 0.6f;
        }

        private static void BuildSmall(ExplosionProfile p)
        {
            p.particleCount = 32;
            p.particleLifetime = 0.45f;
            p.particleSpeed = 5.5f;
            p.particleSize = 0.14f;
            p.hdrIntensity = 2f;
            p.colorGradient = WarmGradient(1.5f);

            p.ringRadius = 1.1f;
            p.ringDuration = 0.30f;
            p.ringColor = new Color(1f, 0.6f, 0.18f, 0.8f);

            p.flashLifetime = 0.15f;
            p.flashSize = 0.8f;
            p.flashColor = new Color(1f, 0.92f, 0.7f, 1f);

            p.debrisCount = 4;
            p.debrisSpeedRange = new Vector2(3f, 6f);
            p.debrisLifetimeRange = new Vector2(0.35f, 0.7f);
            p.spawnSmoke = true;
            p.smokeLifetime = 1.0f;
            p.smokeRadius = 0.9f;

            p.screenFlashAlpha = 0f;
            p.shakeCount = 0;
            p.hitStopSeconds = 0.02f;
            p.enableHaptics = true;
            p.hapticIntensity = HapticIntensity.Light;
            p.damageMultiplier = 1f;
            p.damageRadius = 1.1f;
        }

        private static void BuildMedium(ExplosionProfile p)
        {
            p.particleCount = 64;
            p.particleLifetime = 0.55f;
            p.particleSpeed = 7f;
            p.particleSize = 0.18f;
            p.hdrIntensity = 2.5f;
            p.colorGradient = WarmGradient(2f);

            p.ringRadius = 1.8f;
            p.ringDuration = 0.40f;
            p.ringColor = new Color(1f, 0.55f, 0.15f, 0.85f);

            p.flashLifetime = 0.20f;
            p.flashSize = 1.2f;
            p.flashColor = new Color(1f, 0.9f, 0.65f, 1f);

            p.debrisCount = 8;
            p.debrisSpeedRange = new Vector2(4f, 8f);
            p.debrisLifetimeRange = new Vector2(0.4f, 0.85f);
            p.spawnSmoke = true;
            p.smokeLifetime = 1.4f;
            p.smokeRadius = 1.4f;

            p.screenFlashAlpha = 0f;
            p.shakeCount = 1;
            p.hitStopSeconds = 0.04f;
            p.enableHaptics = true;
            p.hapticIntensity = HapticIntensity.Medium;
            p.damageMultiplier = 1f;
            p.damageRadius = 2.0f;
        }

        private static void BuildLarge(ExplosionProfile p)
        {
            p.particleCount = 130;
            p.particleLifetime = 0.7f;
            p.particleSpeed = 9f;
            p.particleSize = 0.22f;
            p.hdrIntensity = 3f;
            p.colorGradient = WarmGradient(2.5f);

            p.ringRadius = 2.8f;
            p.ringDuration = 0.50f;
            p.ringColor = new Color(1f, 0.5f, 0.13f, 0.9f);

            p.flashLifetime = 0.25f;
            p.flashSize = 1.8f;
            p.flashColor = new Color(1f, 0.92f, 0.7f, 1f);

            p.debrisCount = 12;
            p.debrisSpeedRange = new Vector2(5f, 10f);
            p.debrisLifetimeRange = new Vector2(0.5f, 1.0f);
            p.spawnSmoke = true;
            p.smokeLifetime = 1.8f;
            p.smokeRadius = 2.0f;

            p.screenFlashColor = new Color(1f, 0.85f, 0.55f, 1f);
            p.screenFlashAlpha = 0.30f;
            p.screenFlashDuration = 0.18f;
            p.shakeCount = 1;
            p.hitStopSeconds = 0.06f;
            p.enableHaptics = true;
            p.hapticIntensity = HapticIntensity.Medium;
            p.damageMultiplier = 1.1f;
            p.damageRadius = 3.0f;
        }

        private static void BuildMega(ExplosionProfile p)
        {
            p.particleCount = 230;
            p.particleLifetime = 0.95f;
            p.particleSpeed = 12f;
            p.particleSize = 0.28f;
            p.hdrIntensity = 4f;
            p.colorGradient = HotGradient(3f);

            p.ringRadius = 4.2f;
            p.ringDuration = 0.65f;
            p.ringColor = new Color(1f, 0.45f, 0.12f, 0.95f);

            p.flashLifetime = 0.32f;
            p.flashSize = 2.6f;
            p.flashColor = new Color(1f, 0.95f, 0.8f, 1f);

            p.debrisCount = 16;
            p.debrisSpeedRange = new Vector2(7f, 13f);
            p.debrisLifetimeRange = new Vector2(0.7f, 1.3f);
            p.spawnSmoke = true;
            p.smokeLifetime = 2.4f;
            p.smokeRadius = 3.0f;

            p.screenFlashColor = new Color(1f, 0.9f, 0.65f, 1f);
            p.screenFlashAlpha = 0.55f;
            p.screenFlashDuration = 0.25f;
            p.shakeCount = 2;
            p.hitStopSeconds = 0.10f;
            p.enableHaptics = true;
            p.hapticIntensity = HapticIntensity.Heavy;
            p.damageMultiplier = 1.25f;
            p.damageRadius = 4.5f;
        }

        private static void BuildNuke(ExplosionProfile p)
        {
            p.particleCount = 420;
            p.particleLifetime = 1.2f;
            p.particleSpeed = 16f;
            p.particleSize = 0.36f;
            p.hdrIntensity = 6f;
            p.colorGradient = NukeGradient();

            p.ringRadius = 6.5f;
            p.ringDuration = 0.85f;
            p.ringColor = new Color(1f, 0.85f, 0.55f, 1f);

            p.flashLifetime = 0.45f;
            p.flashSize = 4.0f;
            p.flashColor = new Color(1f, 1f, 0.95f, 1f);

            p.debrisCount = 24;
            p.debrisSpeedRange = new Vector2(10f, 18f);
            p.debrisLifetimeRange = new Vector2(0.9f, 1.6f);
            p.spawnSmoke = true;
            p.smokeLifetime = 3.0f;
            p.smokeRadius = 4.5f;

            p.screenFlashColor = Color.white;
            p.screenFlashAlpha = 0.95f;
            p.screenFlashDuration = 0.5f;
            p.shakeCount = 3;
            p.hitStopSeconds = 0.18f;
            p.enableHaptics = true;
            p.hapticIntensity = HapticIntensity.Heavy;
            p.damageMultiplier = 1.5f;
            p.damageRadius = 7.0f;
        }

        // ------------------------------------------------------------------
        // Gradient helpers
        // ------------------------------------------------------------------

        // Standard orange/yellow/red gradient. `intensity` boosts the HDR brightness of the head.
        private static Gradient WarmGradient(float intensity)
        {
            return Gradient(
                new[]
                {
                    (new Color(1f, 0.95f, 0.6f) * intensity, 0f),
                    (new Color(1f, 0.55f, 0.1f) * intensity, 0.4f),
                    (new Color(0.4f, 0.1f, 0.05f), 1f),
                },
                new[] { (1f, 0f), (0.8f, 0.5f), (0f, 1f) });
        }

        // Hotter ramp for Mega — adds a near-white head before falling into orange.
        private static Gradient HotGradient(float intensity)
        {
            return Gradient(
                new[]
                {
                    (new Color(1f, 1f, 0.85f) * intensity, 0f),
                    (new Color(1f, 0.75f, 0.25f) * intensity, 0.25f),
                    (new Color(1f, 0.4f, 0.08f) * intensity * 0.5f, 0.6f),
                    (new Color(0.35f, 0.08f, 0.04f), 1f),
                },
                new[] { (1f, 0f), (0.9f, 0.3f), (0.6f, 0.7f), (0f, 1f) });
        }

        // Orange -> white core ramp for Nuke (per the spec).
        private static Gradient NukeGradient()
        {
            return Gradient(
                new[]
                {
                    (new Color(1f, 1f, 1f) * 6f, 0f),       // white core, very hot
                    (new Color(1f, 0.9f, 0.55f) * 4f, 0.2f), // pale yellow
                    (new Color(1f, 0.55f, 0.12f) * 2.5f, 0.5f), // orange shell
                    (new Color(0.5f, 0.12f, 0.03f), 0.9f),  // dark red ember
                    (new Color(0.1f, 0.05f, 0.05f, 0f), 1f),// fade to black
                },
                new[] { (1f, 0f), (1f, 0.3f), (0.85f, 0.6f), (0.3f, 0.9f), (0f, 1f) });
        }

        // ------------------------------------------------------------------
        // Boilerplate
        // ------------------------------------------------------------------

        private static void CreateOrUpdate(string name, ExplosionTier tier, System.Action<ExplosionProfile> author)
        {
            string path = $"{ExplosionsDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ExplosionProfile>(path);
            bool created = false;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ExplosionProfile>();
                AssetDatabase.CreateAsset(asset, path);
                created = true;
            }
            asset.tier = tier;
            author(asset);
            EditorUtility.SetDirty(asset);
            Debug.Log($"[LoneFighter.Explosions] {(created ? "Created" : "Updated")} {path}");
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

        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath)) return;
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
