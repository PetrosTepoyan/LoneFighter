// ContentSeederWindow.cs — Editor tool that materializes the ContentSeed numeric
// constants into real ScriptableObject .asset files under Assets/Data/*.
//
// This tool is idempotent: re-running it updates field values in place on
// existing assets rather than recreating them, so any references the user has
// wired in the Inspector (e.g. enemy prefabs, projectile prefabs, sprites)
// survive across re-seeds.
//
// Output layout:
//   Assets/Data/Weapons/{Name}.asset
//   Assets/Data/Enemies/{Name}.asset
//   Assets/Data/Upgrades/{Name}.asset
//   Assets/Data/Waves/MainRun.asset
//
// Names with whitespace are sanitized for file paths (spaces removed) — e.g.
// "Spread Shotgun" → "SpreadShotgun.asset".

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.EditorTools.Content
{
    public sealed class ContentSeederWindow : EditorWindow
    {
        // ----- Canonical output paths --------------------------------------
        const string DataRoot     = "Assets/Data";
        const string WeaponsDir   = "Assets/Data/Weapons";
        const string EnemiesDir   = "Assets/Data/Enemies";
        const string UpgradesDir  = "Assets/Data/Upgrades";
        const string WavesDir     = "Assets/Data/Waves";
        const string WaveAssetName = "MainRun";

        // Cached status so the small UI label doesn't re-scan AssetDatabase
        // every IMGUI frame.
        int _weaponAssetCount;
        int _enemyAssetCount;
        int _upgradeAssetCount;
        bool _waveAssetExists;

        [MenuItem("LoneFighter/Content/Content Seeder")]
        public static void Open()
        {
            var win = GetWindow<ContentSeederWindow>(false, "Content Seeder", true);
            win.minSize = new Vector2(360f, 320f);
            win.RefreshStatus();
            win.Show();
        }

        void OnEnable()  => RefreshStatus();
        void OnFocus()   => RefreshStatus();

        void OnGUI()
        {
            EditorGUILayout.LabelField("LoneFighter Content Seeder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Generates WeaponData / EnemyData / UpgradeData / WaveConfig assets " +
                "from LoneFighter.Data.ContentSeed.\n\nSafe to re-run: existing assets " +
                "are updated in place (prefab / sprite references are preserved).",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            DrawStatus();

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Seed All", GUILayout.Height(30f)))
            {
                SeedAll();
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed Weapons"))  SeedWeapons(showProgress: true);
                if (GUILayout.Button("Seed Enemies"))  SeedEnemies(showProgress: true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Seed Upgrades"))    SeedUpgrades(showProgress: true);
                if (GUILayout.Button("Seed Wave Config")) SeedWaveConfig(showProgress: true);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Wire Cross-References"))
            {
                WireCrossReferences(showProgress: true);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Open Output Folder"))
            {
                OpenOutputFolder();
            }
        }

        // -------------------------------------------------------------------
        // Status display
        // -------------------------------------------------------------------

        void DrawStatus()
        {
            EditorGUILayout.LabelField("Current assets on disk", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                $"  Weapons:  {_weaponAssetCount} / {ContentSeed.Weapons.Length}");
            EditorGUILayout.LabelField(
                $"  Enemies:  {_enemyAssetCount} / {ContentSeed.Enemies.Length}");
            EditorGUILayout.LabelField(
                $"  Upgrades: {_upgradeAssetCount} / {ContentSeed.Upgrades.Length}");
            EditorGUILayout.LabelField(
                $"  Waves:    {(_waveAssetExists ? "MainRun.asset present" : "missing")}");
        }

        void RefreshStatus()
        {
            int weapons = 0;
            foreach (var s in ContentSeed.Weapons)
                if (AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponPath(s.name)) != null) weapons++;
            _weaponAssetCount = weapons;

            int enemies = 0;
            foreach (var s in ContentSeed.Enemies)
                if (AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyPath(s.name)) != null) enemies++;
            _enemyAssetCount = enemies;

            int upgrades = 0;
            foreach (var s in ContentSeed.Upgrades)
                if (AssetDatabase.LoadAssetAtPath<UpgradeData>(UpgradePath(s.name)) != null) upgrades++;
            _upgradeAssetCount = upgrades;

            _waveAssetExists = AssetDatabase.LoadAssetAtPath<WaveConfig>(WavePath()) != null;
            Repaint();
        }

        // -------------------------------------------------------------------
        // Path helpers
        // -------------------------------------------------------------------

        static string Sanitize(string name) => name.Replace(" ", string.Empty);

        static string WeaponPath(string name)  => $"{WeaponsDir}/{Sanitize(name)}.asset";
        static string EnemyPath(string name)   => $"{EnemiesDir}/{Sanitize(name)}.asset";
        static string UpgradePath(string name) => $"{UpgradesDir}/{Sanitize(name)}.asset";
        static string WavePath()               => $"{WavesDir}/{WaveAssetName}.asset";

        // -------------------------------------------------------------------
        // Folder setup
        // -------------------------------------------------------------------

        static void EnsureFolders()
        {
            // AssetDatabase.CreateFolder requires the parent to exist first, so
            // walk down level by level. Strings are kept literal so the user can
            // grep for the canonical paths.
            EnsureFolder("Assets", "Data");
            EnsureFolder(DataRoot, "Weapons");
            EnsureFolder(DataRoot, "Enemies");
            EnsureFolder(DataRoot, "Upgrades");
            EnsureFolder(DataRoot, "Waves");
        }

        static void EnsureFolder(string parent, string leaf)
        {
            string full = $"{parent}/{leaf}";
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        // -------------------------------------------------------------------
        // Asset get-or-create
        // -------------------------------------------------------------------

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        // -------------------------------------------------------------------
        // Top-level orchestration
        // -------------------------------------------------------------------

        public void SeedAll()
        {
            try
            {
                EnsureFolders();

                // Run each section without its own progress bar / save — we
                // batch the final save and refresh after the whole pipeline so
                // the cross-reference pass can see the newly created assets.
                SeedWeaponsBatch();
                SeedEnemiesBatch();
                SeedUpgradesBatch();
                SeedWaveConfigBatch();

                // Persist before wiring so LoadAssetAtPath is guaranteed to
                // find the assets created above.
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar(
                    "Seed All", "Wiring cross-references", 0.9f);
                WireUpgradeWeaponGrants();
                WireWaveEnemies();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                RefreshStatus();
            }

            Debug.Log("[ContentSeeder] Seed All complete. " +
                      "Assets written under Assets/Data/. " +
                      "Drag enemy/projectile prefabs into the generated assets when ready.");
        }

        void SeedWeaponsBatch()
        {
            var specs = ContentSeed.Weapons;
            for (int i = 0; i < specs.Length; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Seed All", $"Weapons: {specs[i].name}", 0.10f + 0.15f * (i + 1f) / specs.Length);
                SeedOneWeapon(specs[i]);
            }
        }

        void SeedEnemiesBatch()
        {
            var specs = ContentSeed.Enemies;
            for (int i = 0; i < specs.Length; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Seed All", $"Enemies: {specs[i].name}", 0.30f + 0.20f * (i + 1f) / specs.Length);
                SeedOneEnemy(specs[i]);
            }
        }

        void SeedUpgradesBatch()
        {
            var specs = ContentSeed.Upgrades;
            for (int i = 0; i < specs.Length; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Seed All", $"Upgrades: {specs[i].name}", 0.55f + 0.25f * (i + 1f) / specs.Length);
                SeedOneUpgrade(specs[i]);
            }
        }

        void SeedWaveConfigBatch()
        {
            EditorUtility.DisplayProgressBar("Seed All", "WaveConfig: MainRun", 0.85f);
            SeedWaveConfigInner();
        }

        // -------------------------------------------------------------------
        // Weapons
        // -------------------------------------------------------------------

        public void SeedWeapons(bool showProgress)
        {
            EnsureFolders();
            var specs = ContentSeed.Weapons;
            try
            {
                for (int i = 0; i < specs.Length; i++)
                {
                    var s = specs[i];
                    if (showProgress)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Seeding Weapons", s.name, (i + 1f) / specs.Length);
                    }
                    SeedOneWeapon(s);
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                if (showProgress)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshStatus();
                }
            }
        }

        static void SeedOneWeapon(ContentSeed.WeaponSpec s)
        {
            string path = WeaponPath(s.name);
            var data = LoadOrCreate<WeaponData>(path);

            data.displayName        = s.name;
            data.damage             = s.damage;
            data.cooldown           = s.cooldown;
            data.projectileSpeed    = s.projectileSpeed;
            data.projectileLifetime = s.projectileLifetime;
            data.pierce             = s.pierce;
            data.range              = s.range;
            // Intentionally NOT touching: icon, projectilePrefab — user-wired.

            EditorUtility.SetDirty(data);
        }

        // -------------------------------------------------------------------
        // Enemies
        // -------------------------------------------------------------------

        public void SeedEnemies(bool showProgress)
        {
            EnsureFolders();
            var specs = ContentSeed.Enemies;
            try
            {
                for (int i = 0; i < specs.Length; i++)
                {
                    var s = specs[i];
                    if (showProgress)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Seeding Enemies", s.name, (i + 1f) / specs.Length);
                    }
                    SeedOneEnemy(s);
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                if (showProgress)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshStatus();
                }
            }
        }

        static void SeedOneEnemy(ContentSeed.EnemySpec s)
        {
            string path = EnemyPath(s.name);
            var data = LoadOrCreate<EnemyData>(path);

            data.displayName     = s.name;
            data.maxHealth       = s.hp;
            data.moveSpeed       = s.speed;
            data.contactDamage   = s.damage;
            data.contactCooldown = s.contactCooldown;
            data.xpDrop          = s.xp;
            data.gemDropChance   = Mathf.Clamp01(s.gemDropChance);
            // Intentionally NOT touching: sprite, prefab — user-wired.

            EditorUtility.SetDirty(data);
        }

        // -------------------------------------------------------------------
        // Upgrades
        // -------------------------------------------------------------------

        public void SeedUpgrades(bool showProgress)
        {
            EnsureFolders();
            var specs = ContentSeed.Upgrades;
            try
            {
                for (int i = 0; i < specs.Length; i++)
                {
                    var s = specs[i];
                    if (showProgress)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Seeding Upgrades", s.name, (i + 1f) / specs.Length);
                    }
                    SeedOneUpgrade(s);
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                if (showProgress)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshStatus();
                }
            }
        }

        static void SeedOneUpgrade(ContentSeed.UpgradeSpec s)
        {
            string path = UpgradePath(s.name);
            var data = LoadOrCreate<UpgradeData>(path);

            data.displayName = s.name;
            data.description = s.description;
            data.kind        = s.kind;
            data.magnitude   = s.magnitude;
            // UpgradeData clamps these via [Range]; mirror to be safe for re-imports.
            data.maxStacks   = Mathf.Clamp(s.maxStacks, 1, 10);
            data.weight      = Mathf.Clamp(s.weight, 1f, 10f);
            // weaponToGrant is resolved in WireCrossReferences (needs the weapon
            // assets to already exist). Don't clear it here — preserve any
            // hand-wired reference if the user already linked it.

            EditorUtility.SetDirty(data);
        }

        // -------------------------------------------------------------------
        // Wave config
        // -------------------------------------------------------------------

        public void SeedWaveConfig(bool showProgress)
        {
            EnsureFolders();

            if (showProgress)
            {
                EditorUtility.DisplayProgressBar(
                    "Seeding Wave Config", WaveAssetName, 0.5f);
            }

            try
            {
                SeedWaveConfigInner();
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                if (showProgress)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshStatus();
                }
            }
        }

        static void SeedWaveConfigInner()
        {
            string path = WavePath();
            var wave = LoadOrCreate<WaveConfig>(path);
            wave.runDuration = ContentSeed.RunDurationSeconds;

            var src = ContentSeed.WaveTimeline;
            if (wave.entries == null)
            {
                wave.entries = new List<WaveEntry>(src.Length);
            }
            else
            {
                // We rebuild the entries list from scratch so its shape and
                // ordering match ContentSeed.WaveTimeline exactly. The enemy
                // reference per entry is re-resolved by WireCrossReferences
                // immediately after, so this is not a data-loss step in
                // practice.
                wave.entries.Clear();
                wave.entries.Capacity = src.Length;
            }

            for (int i = 0; i < src.Length; i++)
            {
                var e = src[i];
                wave.entries.Add(new WaveEntry
                {
                    startTime     = e.startTime,
                    endTime       = e.endTime,
                    spawnRate     = e.spawnRate,
                    concurrentCap = e.concurrentCap,
                    enemy         = null, // wired in WireCrossReferences
                });
            }

            EditorUtility.SetDirty(wave);
        }

        // -------------------------------------------------------------------
        // Cross-reference wiring (second pass)
        // -------------------------------------------------------------------

        public void WireCrossReferences(bool showProgress)
        {
            EnsureFolders();
            try
            {
                if (showProgress)
                {
                    EditorUtility.DisplayProgressBar(
                        "Wiring Cross-References", "upgrades → weapons", 0.25f);
                }
                WireUpgradeWeaponGrants();

                if (showProgress)
                {
                    EditorUtility.DisplayProgressBar(
                        "Wiring Cross-References", "wave entries → enemies", 0.75f);
                }
                WireWaveEnemies();
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
                if (showProgress)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    RefreshStatus();
                }
            }
        }

        static void WireUpgradeWeaponGrants()
        {
            foreach (var s in ContentSeed.Upgrades)
            {
                if (s.kind != UpgradeKind.GrantWeapon) continue;

                string upgradePath = UpgradePath(s.name);
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(upgradePath);
                if (upgrade == null)
                {
                    Debug.LogWarning(
                        $"[ContentSeeder] GrantWeapon upgrade asset missing at {upgradePath}; " +
                        $"run Seed Upgrades first.");
                    continue;
                }

                if (string.IsNullOrEmpty(s.grantsWeaponName))
                {
                    Debug.LogWarning(
                        $"[ContentSeeder] Upgrade '{s.name}' is GrantWeapon but has no " +
                        $"grantsWeaponName; leaving weaponToGrant unchanged.");
                    continue;
                }

                string weaponPath = WeaponPath(s.grantsWeaponName);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(weaponPath);
                if (weapon == null)
                {
                    Debug.LogWarning(
                        $"[ContentSeeder] Could not find WeaponData '{s.grantsWeaponName}' " +
                        $"at {weaponPath} for upgrade '{s.name}'. Run Seed Weapons first.");
                    continue;
                }

                upgrade.weaponToGrant = weapon;
                EditorUtility.SetDirty(upgrade);
            }
        }

        static void WireWaveEnemies()
        {
            string path = WavePath();
            var wave = AssetDatabase.LoadAssetAtPath<WaveConfig>(path);
            if (wave == null)
            {
                Debug.LogWarning(
                    $"[ContentSeeder] WaveConfig missing at {path}; run Seed Wave Config first.");
                return;
            }

            var src = ContentSeed.WaveTimeline;
            // The wave entries list should already mirror ContentSeed.WaveTimeline 1:1
            // (SeedWaveConfig clears + repopulates). If somehow the lengths don't match
            // we walk the shorter prefix so we don't IndexOutOfRange.
            int n = Mathf.Min(wave.entries.Count, src.Length);
            if (wave.entries.Count != src.Length)
            {
                Debug.LogWarning(
                    $"[ContentSeeder] WaveConfig.entries.Count ({wave.entries.Count}) " +
                    $"does not match ContentSeed.WaveTimeline.Length ({src.Length}). " +
                    $"Re-run Seed Wave Config to rebuild the entry list.");
            }

            for (int i = 0; i < n; i++)
            {
                string enemyName = src[i].enemyName;
                if (string.IsNullOrEmpty(enemyName)) continue;

                string enemyPath = EnemyPath(enemyName);
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyPath);
                if (enemy == null)
                {
                    Debug.LogWarning(
                        $"[ContentSeeder] Wave entry [{i}] references enemy '{enemyName}' " +
                        $"but no asset at {enemyPath}. Run Seed Enemies first.");
                    continue;
                }

                var entry = wave.entries[i];
                entry.enemy = enemy;
                wave.entries[i] = entry; // WaveEntry is a struct — write back
            }

            EditorUtility.SetDirty(wave);
        }

        // -------------------------------------------------------------------
        // Open Output Folder
        // -------------------------------------------------------------------

        void OpenOutputFolder()
        {
            EnsureFolders();
            var folder = AssetDatabase.LoadAssetAtPath<Object>(DataRoot);
            if (folder != null)
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
            else
            {
                // Fallback: reveal in OS file browser.
                string abs = Path.GetFullPath(DataRoot);
                EditorUtility.RevealInFinder(abs);
            }
        }
    }
}
