using LoneFighter.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoneFighter.EditorTools.Fx
{
    /// One-click generator for every Fx_*.prefab the FxService expects.
    /// All five prefabs share the additive HDR material from MaterialGenerator so they
    /// all kick Bloom hard at HDR intensities. Each prefab has a single burst, a Stop Action
    /// of Disable, and a ParticleAutoRelease component so PoolService can recycle them.
    public static class FxPrefabGenerator
    {
        // Hex-coded HDR colors. The 4th channel here is alpha (1.0); HDR brightness comes from the
        // float multiplier in BuildHDRColor below.
        // Note: ParticleSystem startColor and gradients store raw HDR linear values, so multiplying
        // an LDR base by an intensity > 1 is the correct way to get bloom-friendly highlights.
        private static readonly Color OrangeBase = new(1.00f, 0.55f, 0.10f, 1f);
        private static readonly Color RedBase    = new(1.00f, 0.18f, 0.10f, 1f);
        private static readonly Color YellowBase = new(1.00f, 0.95f, 0.30f, 1f);
        private static readonly Color CyanBase   = new(0.30f, 0.95f, 1.00f, 1f);
        private static readonly Color GreenBase  = new(0.30f, 1.00f, 0.45f, 1f);

        [MenuItem("LoneFighter/FX/Generate FX Prefabs", priority = 20)]
        public static void GenerateAll()
        {
            FxAssetPaths.EnsureFolder(FxAssetPaths.PrefabsFxDir);

            var mat = MaterialGenerator.GetOrCreate();
            if (mat == null)
            {
                Debug.LogError("[LoneFighter.FX] Aborting prefab generation: additive material could not be created.");
                return;
            }

            BuildEnemyExplosion(mat);
            BuildProjectileImpact(mat);
            BuildPlayerHit(mat);
            BuildXpPickup(mat);
            BuildLevelUp(mat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LoneFighter.FX] Generated 5 FX prefabs in " + FxAssetPaths.PrefabsFxDir);
        }

        // ---------------------------------------------------------------------
        // Individual prefab builders
        // ---------------------------------------------------------------------

        private static void BuildEnemyExplosion(Material mat)
        {
            var go = NewFxGameObject("Fx_EnemyExplosion");
            var ps = go.GetComponent<ParticleSystem>();

            // Main: short, fast, shrinking, world space, gravity off, disable on stop.
            var m = ps.main;
            m.duration               = 0.5f;
            m.loop                   = false;
            m.startLifetime          = 0.4f;
            m.startSpeed             = new ParticleSystem.MinMaxCurve(4f, 8f);
            m.startSize              = 0.3f;
            m.startColor             = BuildHDRColor(OrangeBase, 3f);
            m.gravityModifier        = 0f;
            m.simulationSpace        = ParticleSystemSimulationSpace.World;
            m.stopAction             = ParticleSystemStopAction.Disable;
            m.maxParticles           = 64;
            m.playOnAwake            = true;
            ps.main = m;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

            var shape = ps.shape;
            shape.enabled         = true;
            shape.shapeType       = ParticleSystemShapeType.Circle;
            shape.radius          = 0.05f;
            shape.radiusMode      = ParticleSystemShapeMultiModeValue.Random;
            shape.arc             = 360f;
            shape.arcMode         = ParticleSystemShapeMultiModeValue.Random;

            // Color over lifetime: white → HDR orange → red → transparent
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (Color.white,                     0.0f),
                (BuildHDRColor(OrangeBase, 3f),   0.35f),
                (BuildHDRColor(RedBase,    2.5f), 0.7f),
                (new Color(0.4f, 0.05f, 0.05f, 0f), 1.0f)));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size    = new ParticleSystem.MinMaxCurve(1f, BuildShrinkCurve());

            ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard);
            EnsureAutoRelease(go);
            SaveAsPrefab(go, FxAssetPaths.EnemyExplosionPath);
        }

        private static void BuildProjectileImpact(Material mat)
        {
            var go = NewFxGameObject("Fx_ProjectileImpact");
            var ps = go.GetComponent<ParticleSystem>();

            var m = ps.main;
            m.duration               = 0.25f;
            m.loop                   = false;
            m.startLifetime          = 0.18f;
            m.startSpeed             = new ParticleSystem.MinMaxCurve(3f, 6f);
            m.startSize              = 0.08f;
            m.startColor             = Color.white;
            m.gravityModifier        = 0f;
            m.simulationSpace        = ParticleSystemSimulationSpace.World;
            m.stopAction             = ParticleSystemStopAction.Disable;
            m.maxParticles           = 32;
            m.playOnAwake            = true;
            ps.main = m;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.enabled    = true;
            shape.shapeType  = ParticleSystemShapeType.Circle;
            shape.radius     = 0.05f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arcMode    = ParticleSystemShapeMultiModeValue.Random;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (Color.white,                       0.0f),
                (BuildHDRColor(YellowBase, 2.5f),   0.5f),
                (new Color(1f, 0.7f, 0.1f, 0f),     1.0f)));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size    = new ParticleSystem.MinMaxCurve(1f, BuildShrinkCurve());

            // Stretched billboard for spark streaks; length scale 2 ties stretch to speed.
            ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Stretch, lengthScale: 2f, speedScale: 0.2f);
            EnsureAutoRelease(go);
            SaveAsPrefab(go, FxAssetPaths.ProjectileImpactPath);
        }

        private static void BuildPlayerHit(Material mat)
        {
            // Player hit is special: a parent ParticleSystem (the big radial flash) plus a child
            // sub-emitter style burst of sparks. We keep both on the same root so the prefab is a
            // single ParticleSystem that ParticleAutoRelease can manage.
            var go = NewFxGameObject("Fx_PlayerHit");
            var flash = go.GetComponent<ParticleSystem>();

            // --- Flash: single large particle that scales 0→2→1 ---
            var m = flash.main;
            m.duration        = 0.3f;
            m.loop            = false;
            m.startLifetime   = 0.25f;
            m.startSpeed      = 0f;
            m.startSize       = 1f; // sizeOverLifetime drives the 0→2→1 shape
            m.startColor      = BuildHDRColor(RedBase, 4f);
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.stopAction      = ParticleSystemStopAction.Disable;
            m.maxParticles    = 8;
            m.playOnAwake     = true;
            flash.main = m;

            var flashEmission = flash.emission;
            flashEmission.enabled = true;
            flashEmission.rateOverTime = 0f;
            flashEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var flashShape = flash.shape;
            flashShape.enabled = true;
            flashShape.shapeType = ParticleSystemShapeType.Circle;
            flashShape.radius    = 0.01f;

            var flashCol = flash.colorOverLifetime;
            flashCol.enabled = true;
            flashCol.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (BuildHDRColor(RedBase, 4f),   0.0f),
                (Color.white,                  0.5f),
                (new Color(1f, 1f, 1f, 0f),    1.0f)));

            // 0 → 2 → 1 size curve via three keys
            var flashSize = flash.sizeOverLifetime;
            flashSize.enabled = true;
            var flashCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.4f, 2f),
                new Keyframe(1f, 1f));
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, flashCurve);

            ConfigureRenderer(flash, mat, ParticleSystemRenderMode.Billboard);

            // --- Sparks: child GameObject with its own ParticleSystem (16 small radial sparks) ---
            var sparksGo = new GameObject("Sparks");
            sparksGo.transform.SetParent(go.transform, false);
            var sparks = sparksGo.AddComponent<ParticleSystem>();

            var sm = sparks.main;
            sm.duration        = 0.3f;
            sm.loop            = false;
            sm.startLifetime   = 0.3f;
            sm.startSpeed      = new ParticleSystem.MinMaxCurve(3f, 6f);
            sm.startSize       = 0.1f;
            sm.startColor      = BuildHDRColor(YellowBase, 2.5f);
            sm.gravityModifier = 0f;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            sm.stopAction      = ParticleSystemStopAction.None; // parent's Disable releases the whole prefab
            sm.maxParticles    = 32;
            sm.playOnAwake     = true;
            sparks.main = sm;

            var sparksEmission = sparks.emission;
            sparksEmission.enabled = true;
            sparksEmission.rateOverTime = 0f;
            sparksEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var sparksShape = sparks.shape;
            sparksShape.enabled    = true;
            sparksShape.shapeType  = ParticleSystemShapeType.Circle;
            sparksShape.radius     = 0.05f;
            sparksShape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            sparksShape.arcMode    = ParticleSystemShapeMultiModeValue.Random;

            var sparksCol = sparks.colorOverLifetime;
            sparksCol.enabled = true;
            sparksCol.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (Color.white,                     0.0f),
                (BuildHDRColor(YellowBase, 2.5f), 0.5f),
                (new Color(1f, 0.4f, 0.4f, 0f),   1.0f)));

            var sparksSize = sparks.sizeOverLifetime;
            sparksSize.enabled = true;
            sparksSize.size    = new ParticleSystem.MinMaxCurve(1f, BuildShrinkCurve());

            ConfigureRenderer(sparks, mat, ParticleSystemRenderMode.Stretch, lengthScale: 1.5f, speedScale: 0.2f);

            EnsureAutoRelease(go);
            SaveAsPrefab(go, FxAssetPaths.PlayerHitPath);
        }

        private static void BuildXpPickup(Material mat)
        {
            var go = NewFxGameObject("Fx_XpPickup");
            var ps = go.GetComponent<ParticleSystem>();

            var m = ps.main;
            m.duration        = 0.4f;
            m.loop            = false;
            m.startLifetime   = 0.3f;
            m.startSpeed      = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            m.startSize       = 0.1f;
            m.startColor      = new ParticleSystem.MinMaxGradient(
                                    BuildHDRColor(GreenBase, 2f),
                                    BuildHDRColor(CyanBase, 2f));
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.stopAction      = ParticleSystemStopAction.Disable;
            m.maxParticles    = 16;
            m.playOnAwake     = true;
            ps.main = m;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.enabled    = true;
            shape.shapeType  = ParticleSystemShapeType.Circle;
            shape.radius     = 0.1f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arcMode    = ParticleSystemShapeMultiModeValue.Random;

            // Upward drift: constant +Y velocity in world space.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.x       = 0f;
            vel.y       = new ParticleSystem.MinMaxCurve(2f);
            vel.z       = 0f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (Color.white,                    0.0f),
                (BuildHDRColor(CyanBase, 2f),    0.5f),
                (new Color(0.3f, 1f, 0.6f, 0f),  1.0f)));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size    = new ParticleSystem.MinMaxCurve(1f, BuildShrinkCurve());

            ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Billboard);
            EnsureAutoRelease(go);
            SaveAsPrefab(go, FxAssetPaths.XpPickupPath);
        }

        private static void BuildLevelUp(Material mat)
        {
            var go = NewFxGameObject("Fx_LevelUp");
            var ps = go.GetComponent<ParticleSystem>();

            var m = ps.main;
            m.duration        = 1.0f;
            m.loop            = false;
            m.startLifetime   = 0.8f;
            m.startSpeed      = new ParticleSystem.MinMaxCurve(6f, 10f);
            m.startSize       = 0.2f;
            m.startColor      = BuildHDRColor(YellowBase, 5f);
            m.gravityModifier = 0f;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.stopAction      = ParticleSystemStopAction.Disable;
            m.maxParticles    = 128;
            m.playOnAwake     = true;
            ps.main = m;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

            // Ring burst: Circle shape, Loop arc mode so particles fan around the full circle evenly.
            var shape = ps.shape;
            shape.enabled    = true;
            shape.shapeType  = ParticleSystemShapeType.Circle;
            shape.radius     = 0.2f;
            shape.arc        = 360f;
            shape.arcMode    = ParticleSystemShapeMultiModeValue.Loop;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color   = new ParticleSystem.MinMaxGradient(BuildGradient(
                (Color.white,                     0.0f),
                (BuildHDRColor(YellowBase, 5f),   0.25f),
                (BuildHDRColor(CyanBase,   3f),   0.7f),
                (new Color(0.3f, 0.9f, 1f, 0f),   1.0f)));

            // Expand fast, then slow: velocity multiplier decays over lifetime.
            var vel = ps.velocityOverLifetime;
            vel.enabled = false; // direct dampen via limitVelocityOverLifetime feels better here

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.space   = ParticleSystemSimulationSpace.World;
            limit.dampen  = 0.3f; // 30% drag per second → fast burst, soft settle
            limit.limit   = new ParticleSystem.MinMaxCurve(20f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size    = new ParticleSystem.MinMaxCurve(1f, BuildShrinkCurve());

            ConfigureRenderer(ps, mat, ParticleSystemRenderMode.Stretch, lengthScale: 3f, speedScale: 0.15f);
            EnsureAutoRelease(go);
            SaveAsPrefab(go, FxAssetPaths.LevelUpPath);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static GameObject NewFxGameObject(string name)
        {
            var go = new GameObject(name);
            // Adding ParticleSystem also adds the renderer automatically.
            go.AddComponent<ParticleSystem>();
            return go;
        }

        private static void ConfigureRenderer(
            ParticleSystem ps,
            Material mat,
            ParticleSystemRenderMode mode,
            float lengthScale = 1f,
            float speedScale  = 0f)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;
            renderer.renderMode   = mode;
            renderer.material     = mat;
            renderer.sharedMaterial = mat;
            renderer.trailMaterial = mat;
            renderer.lengthScale  = lengthScale;
            renderer.velocityScale = speedScale;
            renderer.sortingOrder = 50; // draw over gameplay sprites
            renderer.alignment    = ParticleSystemRenderSpace.View;
        }

        private static void EnsureAutoRelease(GameObject go)
        {
            if (go.GetComponent<ParticleAutoRelease>() == null)
            {
                go.AddComponent<ParticleAutoRelease>();
            }
        }

        private static void SaveAsPrefab(GameObject go, string path)
        {
            // Overwrite if it already exists — running the menu again should be idempotent.
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        /// Build an HDR color by multiplying linear RGB by an intensity (keeps alpha at 1).
        private static Color BuildHDRColor(Color baseColor, float intensity)
        {
            return new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, baseColor.a);
        }

        /// Build a simple multi-stop gradient. Colors carry HDR values where intensity > 1.
        private static Gradient BuildGradient(params (Color color, float time)[] stops)
        {
            var g = new Gradient { mode = GradientMode.Blend };
            var colorKeys = new GradientColorKey[stops.Length];
            var alphaKeys = new GradientAlphaKey[stops.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(stops[i].color, stops[i].time);
                alphaKeys[i] = new GradientAlphaKey(stops[i].color.a, stops[i].time);
            }
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        /// Standard ease-out shrink curve used by every prefab's sizeOverLifetime.
        private static AnimationCurve BuildShrinkCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.6f, 0.7f),
                new Keyframe(1f, 0f));
        }
    }
}
