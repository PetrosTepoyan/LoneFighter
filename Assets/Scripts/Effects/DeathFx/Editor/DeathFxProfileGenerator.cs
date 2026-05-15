// DeathFxProfileGenerator.cs — Editor tool that creates the 7 starter DeathFxProfile
// ScriptableObjects, a DeathFxRegistry wired to them, and a default GibChunk prefab.
//
// Output layout (all under Assets/Data/DeathFx/):
//   Assets/Data/DeathFx/DeathFxRegistry.asset
//   Assets/Data/DeathFx/Profiles/Grunt.asset
//   Assets/Data/DeathFx/Profiles/Runner.asset
//   Assets/Data/DeathFx/Profiles/Tank.asset
//   Assets/Data/DeathFx/Profiles/Spitter.asset
//   Assets/Data/DeathFx/Profiles/Dasher.asset
//   Assets/Data/DeathFx/Profiles/Bomber.asset
//   Assets/Data/DeathFx/Profiles/Warden.asset
//   Assets/Data/DeathFx/Gibs/GibChunkSprite.png
//   Assets/Data/DeathFx/Gibs/GibChunk.prefab
//
// Idempotent: re-running the generator updates fields in place on the existing assets
// (so the registry SO's GUID survives across re-seeds and any scene refs hold).

using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Effects.DeathFx;

namespace LoneFighter.Effects.DeathFx.EditorTools
{
    public static class DeathFxProfileGenerator
    {
        // ----- Output paths --------------------------------------------------------------
        const string DataRoot      = "Assets/Data";
        const string DeathFxDir    = "Assets/Data/DeathFx";
        const string ProfilesDir   = "Assets/Data/DeathFx/Profiles";
        const string GibsDir       = "Assets/Data/DeathFx/Gibs";
        const string RegistryPath  = "Assets/Data/DeathFx/DeathFxRegistry.asset";
        const string GibSpritePath = "Assets/Data/DeathFx/Gibs/GibChunkSprite.png";
        const string GibPrefabPath = "Assets/Data/DeathFx/Gibs/GibChunk.prefab";

        [MenuItem("LoneFighter/FX/Generate Death FX Profiles", priority = 30)]
        public static void GenerateAll()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(DeathFxDir);
            EnsureFolder(ProfilesDir);
            EnsureFolder(GibsDir);

            // The gib sprite + prefab give the GibSpawner something to pool out of the box.
            // Authors can replace either freely afterwards — the prefab carries a GibChunk
            // component which is the only contract the spawner cares about.
            var gibSprite = GetOrCreateGibSprite();
            var gibPrefab = GetOrCreateGibPrefab(gibSprite);

            var registry = GetOrCreate<DeathFxRegistry>(RegistryPath);

            // --- 7 starter profiles, one per archetype. Numbers tuned to "feel right" on
            //     the canonical TimeFromContentSeed enemy stats; designers will iterate. ---

            // Grunt — small red poof. Cheap, fires constantly.
            var grunt = MakeProfile("Grunt",
                label:                "Grunt — Small Red Poof",
                particleTint:         HDR(new Color(1.00f, 0.18f, 0.10f), 2.5f),
                particleSize:         0.8f,
                particleBurst:        12,
                soundCue:             "EnemyDeath",
                soundVolume:          0.85f,
                shake:                DeathFxShake.Light,
                hitStop:              0f,
                gibCount:             4,
                gibColor:             new Color(0.85f, 0.18f, 0.18f, 1f),
                gibSize:              new Vector2(0.08f, 0.14f),
                gibSpeed:             new Vector2(2.5f, 4.5f),
                gibLifetime:          0.7f,
                splatColor:           new Color(0.45f, 0.08f, 0.10f, 0.85f));

            // Runner — orange streaks. Slightly more particles, faster gibs.
            var runner = MakeProfile("Runner",
                label:                "Runner — Orange Streaks",
                particleTint:         HDR(new Color(1.00f, 0.55f, 0.10f), 3f),
                particleSize:         0.9f,
                particleBurst:        14,
                soundCue:             "EnemyDeath",
                soundVolume:          0.9f,
                shake:                DeathFxShake.Light,
                hitStop:              0f,
                gibCount:             5,
                gibColor:             new Color(1.00f, 0.55f, 0.20f, 1f),
                gibSize:              new Vector2(0.07f, 0.13f),
                gibSpeed:             new Vector2(4.0f, 7.0f),
                gibLifetime:          0.75f,
                splatColor:           new Color(0.60f, 0.30f, 0.05f, 0.80f));

            // Tank — big crimson, lots of gibs, heavy shake, brief hit-stop.
            var tank = MakeProfile("Tank",
                label:                "Tank — Big Crimson",
                particleTint:         HDR(new Color(0.85f, 0.05f, 0.05f), 3.5f),
                particleSize:         1.7f,
                particleBurst:        28,
                soundCue:             "EnemyDeath",
                soundVolume:          1f,
                shake:                DeathFxShake.Heavy,
                hitStop:              0.05f,
                gibCount:             12,
                gibColor:             new Color(0.70f, 0.10f, 0.10f, 1f),
                gibSize:              new Vector2(0.12f, 0.22f),
                gibSpeed:             new Vector2(2.5f, 5.0f),
                gibLifetime:          0.9f,
                splatColor:           new Color(0.35f, 0.04f, 0.06f, 0.90f));

            // Spitter — acid green slime.
            var spitter = MakeProfile("Spitter",
                label:                "Spitter — Acid Green Slime",
                particleTint:         HDR(new Color(0.30f, 1.00f, 0.35f), 2.5f),
                particleSize:         1.0f,
                particleBurst:        16,
                soundCue:             "EnemyDeath",
                soundVolume:          0.85f,
                shake:                DeathFxShake.Light,
                hitStop:              0f,
                gibCount:             7,
                gibColor:             new Color(0.40f, 0.95f, 0.30f, 1f),
                gibSize:              new Vector2(0.09f, 0.16f),
                gibSpeed:             new Vector2(2.0f, 4.0f),
                gibLifetime:          1.0f,
                splatColor:           new Color(0.20f, 0.55f, 0.15f, 0.80f));

            // Dasher — magenta sparks, longer fast gibs.
            var dasher = MakeProfile("Dasher",
                label:                "Dasher — Magenta Sparks",
                particleTint:         HDR(new Color(1.00f, 0.25f, 0.85f), 3f),
                particleSize:         0.8f,
                particleBurst:        18,
                soundCue:             "EnemyDeath",
                soundVolume:          0.9f,
                shake:                DeathFxShake.Light,
                hitStop:              0.03f,
                gibCount:             6,
                gibColor:             new Color(1.00f, 0.30f, 0.90f, 1f),
                gibSize:              new Vector2(0.06f, 0.12f),
                gibSpeed:             new Vector2(5.0f, 8.5f),
                gibLifetime:          0.7f,
                splatColor:           new Color(0.55f, 0.10f, 0.50f, 0.75f));

            // Bomber — big orange BOOM. Heavy shake, longer hit-stop, lots of gibs.
            var bomber = MakeProfile("Bomber",
                label:                "Bomber — Big Orange BOOM",
                particleTint:         HDR(new Color(1.00f, 0.50f, 0.10f), 4f),
                particleSize:         2.0f,
                particleBurst:        36,
                soundCue:             "BossSlam",
                soundVolume:          1f,
                shake:                DeathFxShake.Heavy,
                hitStop:              0.08f,
                gibCount:             14,
                gibColor:             new Color(1.00f, 0.55f, 0.15f, 1f),
                gibSize:              new Vector2(0.10f, 0.20f),
                gibSpeed:             new Vector2(4.0f, 8.0f),
                gibLifetime:          0.9f,
                splatColor:           new Color(0.50f, 0.20f, 0.05f, 0.85f));

            // Warden — purple + gold, massive. Boss-tier hit-stop and shake.
            var warden = MakeProfile("Warden",
                label:                "Warden — Purple + Gold Massive",
                particleTint:         HDR(new Color(0.80f, 0.45f, 1.00f), 4.5f),
                particleSize:         2.4f,
                particleBurst:        48,
                soundCue:             "BossSlam",
                soundVolume:          1f,
                shake:                DeathFxShake.Heavy,
                hitStop:              0.15f,
                gibCount:             20,
                gibColor:             new Color(1.00f, 0.85f, 0.30f, 1f), // gold gibs
                gibSize:              new Vector2(0.12f, 0.24f),
                gibSpeed:             new Vector2(3.0f, 7.5f),
                gibLifetime:          1.2f,
                splatColor:           new Color(0.45f, 0.20f, 0.65f, 0.90f));

            // Wire the registry. SetEntry is last-write-wins so re-runs stay clean.
            registry.SetEntry("Grunt",   grunt);
            registry.SetEntry("Runner",  runner);
            registry.SetEntry("Tank",    tank);
            registry.SetEntry("Spitter", spitter);
            registry.SetEntry("Dasher",  dasher);
            registry.SetEntry("Bomber",  bomber);
            registry.SetEntry("Warden",  warden);
            EditorUtility.SetDirty(registry);

            // Ping the registry so the user can see the generated assets immediately.
            Selection.activeObject = registry;
            EditorGUIUtility.PingObject(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LoneFighter.DeathFx] Generated 7 DeathFxProfile assets + registry under " +
                      DeathFxDir + ". Gib prefab: " + GibPrefabPath +
                      ". Drop the registry onto a DeathFxDispatcher in your scene to activate.");
        }

        // -----------------------------------------------------------------------------------
        // Profile builder
        // -----------------------------------------------------------------------------------

        private static DeathFxProfile MakeProfile(
            string assetFileName,
            string label,
            Color particleTint,
            float particleSize,
            int particleBurst,
            string soundCue,
            float soundVolume,
            DeathFxShake shake,
            float hitStop,
            int gibCount,
            Color gibColor,
            Vector2 gibSize,
            Vector2 gibSpeed,
            float gibLifetime,
            Color splatColor)
        {
            string path = ProfilesDir + "/" + Sanitize(assetFileName) + ".asset";
            var profile = GetOrCreate<DeathFxProfile>(path);

            profile.label                  = label;
            // particlePrefab intentionally left alone — designers wire it later, like the
            // ContentSeeder leaves `prefab` / `sprite` refs untouched.
            profile.particleTint           = particleTint;
            profile.particleSizeMultiplier = particleSize;
            profile.particleBurstCount     = particleBurst;
            profile.soundCueName           = soundCue;
            profile.soundVolumeScale       = soundVolume;
            profile.shake                  = shake;
            profile.hitStopSeconds         = hitStop;
            profile.gibCount               = gibCount;
            profile.gibColor               = gibColor;
            profile.gibSizeRange           = gibSize;
            profile.gibSpeedRange          = gibSpeed;
            profile.gibLifetime            = gibLifetime;
            profile.splatDecalColor        = splatColor;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        // -----------------------------------------------------------------------------------
        // GibChunk sprite + prefab (only built on first run; existing ones are preserved).
        // -----------------------------------------------------------------------------------

        private static Sprite GetOrCreateGibSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(GibSpritePath);
            if (existing != null) return existing;

            // 16x16 soft white square, drawn procedurally so we don't ship a PNG in source.
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Soft alpha falloff toward edges so the chunk reads round-ish even at
                    // sprite scale, without needing a separate alpha mask asset.
                    float dx = (x - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                    float dy = (y - (size - 1) * 0.5f) / ((size - 1) * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d * 0.85f);
                    a = a * a;
                    byte ab = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, ab);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            File.WriteAllBytes(GibSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(GibSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(GibSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType         = TextureImporterType.Sprite;
                importer.spriteImportMode    = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled       = false;
                importer.wrapMode            = TextureWrapMode.Clamp;
                importer.filterMode          = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 64f; // matches the 16px sprite ≈ 0.25 world units
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(GibSpritePath);
        }

        private static GameObject GetOrCreateGibPrefab(Sprite gibSprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GibPrefabPath);
            if (existing != null) return existing;

            var go = new GameObject("GibChunk");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = gibSprite;
            sr.color = Color.white;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = false;
            rb.linearDamping = 3f;
            rb.angularDamping = 6f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            rb.bodyType = RigidbodyType2D.Dynamic;

            // GibChunk auto-wires references in Awake; we add it last so Reset() doesn't
            // grab a half-built renderer.
            go.AddComponent<GibChunk>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, GibPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // -----------------------------------------------------------------------------------
        // Asset helpers
        // -----------------------------------------------------------------------------------

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            return instance;
        }

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

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Profile";
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            }
            return sb.Length == 0 ? "Profile" : sb.ToString();
        }

        /// HDR-bright Color = base * intensity, alpha kept at 1 (ColorOverLifetime owns alpha).
        private static Color HDR(Color baseColor, float intensity)
        {
            return new Color(baseColor.r * intensity, baseColor.g * intensity, baseColor.b * intensity, 1f);
        }
    }
}
