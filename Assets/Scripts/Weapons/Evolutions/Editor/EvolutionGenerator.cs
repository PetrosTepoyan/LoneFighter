// LoneFighter - Weapon Evolution System
// Editor-only tool. New file. Do not modify existing files.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Weapons.Evolutions.Editor
{
    /// <summary>
    /// Menu: LoneFighter -> Weapons -> Generate Evolution Recipes.
    ///
    /// Creates three starter evolution recipes plus the three evolved WeaponData SOs they
    /// reference. The starter set mirrors the Vampire Survivors archetype matrix:
    ///   - Pistol         + "Bigger Bullets" -> "Hand Cannon"
    ///   - Spread Shotgun + "Quick Hands"    -> "Auto Shotgun"
    ///   - Lightning Chain + "Pierce"        -> "Tesla Coil"
    ///
    /// Output paths:
    ///   Assets/Data/Weapons/Evolved/HandCannon.asset
    ///   Assets/Data/Weapons/Evolved/AutoShotgun.asset
    ///   Assets/Data/Weapons/Evolved/TeslaCoil.asset
    ///   Assets/Data/Evolutions/Evolution_Pistol_To_HandCannon.asset
    ///   Assets/Data/Evolutions/Evolution_SpreadShotgun_To_AutoShotgun.asset
    ///   Assets/Data/Evolutions/Evolution_LightningChain_To_TeslaCoil.asset
    ///   Assets/Data/Evolutions/EvolutionRecipeRegistry.asset
    ///   Assets/Resources/EvolutionRecipeRegistry.asset (duplicate ref - lets EvolutionRecipeRegistry.Instance resolve)
    ///
    /// The generator looks up the project's existing base WeaponData assets by NAME -
    /// it does NOT modify them. If a base asset is not found it logs a warning and leaves
    /// the recipe's BaseWeapon field empty for the designer to link.
    ///
    /// Stat tuning is done by reflection over the project's <c>WeaponData</c> fields. The
    /// generator looks for commonly-named numeric fields ("damage", "cooldown",
    /// "projectileScale", "pellets", "jumps", "falloff", ...) and applies multipliers /
    /// overrides. Fields that don't exist are silently skipped, so generation succeeds even
    /// against an in-progress WeaponData schema.
    /// </summary>
    public static class EvolutionGenerator
    {
        private const string EvolvedWeaponDir = "Assets/Data/Weapons/Evolved";
        private const string EvolutionDir = "Assets/Data/Evolutions";
        private const string ResourcesDir = "Assets/Resources";
        private const string RegistryName = "EvolutionRecipeRegistry";

        [MenuItem("LoneFighter/Weapons/Generate Evolution Recipes")]
        public static void Generate()
        {
            EnsureDir(EvolvedWeaponDir);
            EnsureDir(EvolutionDir);
            EnsureDir(ResourcesDir);

            var weaponDataType = FindTypeByName("WeaponData");
            if (weaponDataType == null)
            {
                EditorUtility.DisplayDialog("Evolution Generator",
                    "WeaponData type not found. Cannot create evolved WeaponData SOs.\n\n" +
                    "Generate recipes anyway (with empty Evolved-weapon refs)?\nNo: cancel.",
                    "Cancel", "Cancel");
                return;
            }

            // Specs: each entry describes one starter recipe.
            var specs = new[]
            {
                new EvolutionSpec(
                    baseWeaponName: "Pistol",
                    requiredPassive: "Bigger Bullets",
                    evolvedAssetName: "HandCannon",
                    evolvedDisplayName: "Hand Cannon",
                    tuning: new Dictionary<string, float>
                    {
                        { "damage_mul", 4f },
                        { "cooldown_mul", 1.6f },
                        { "projectile_scale_mul", 2.5f },
                        { "projectile_size_mul", 2.5f },
                    }),
                new EvolutionSpec(
                    baseWeaponName: "Spread Shotgun",
                    requiredPassive: "Quick Hands",
                    evolvedAssetName: "AutoShotgun",
                    evolvedDisplayName: "Auto Shotgun",
                    tuning: new Dictionary<string, float>
                    {
                        { "cooldown_mul", 0.35f },
                        { "pellets_add", 2f },
                        { "pellet_count_add", 2f },
                        { "damage_mul", 1.15f },
                    }),
                new EvolutionSpec(
                    baseWeaponName: "Lightning Chain",
                    requiredPassive: "Pierce",
                    evolvedAssetName: "TeslaCoil",
                    evolvedDisplayName: "Tesla Coil",
                    tuning: new Dictionary<string, float>
                    {
                        { "jumps_add", 4f },
                        { "chain_count_add", 4f },
                        { "max_jumps_add", 4f },
                        { "falloff", 0f },              // override to zero
                        { "damage_falloff", 0f },
                        { "chain_falloff", 0f },
                    }),
            };

            var createdRecipes = new List<WeaponEvolutionRecipe>(specs.Length);

            foreach (var spec in specs)
            {
                var evolved = CreateEvolvedWeapon(weaponDataType, spec);
                if (evolved == null) continue;

                var recipe = CreateRecipe(spec, evolved);
                if (recipe != null) createdRecipes.Add(recipe);
            }

            var registry = CreateOrUpdateRegistry(createdRecipes);
            EnsureResourcesAlias(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Evolution Generator",
                "Created " + createdRecipes.Count + " starter evolution recipes.\n\n" +
                "Recipes:      " + EvolutionDir + "\n" +
                "Evolved SOs:  " + EvolvedWeaponDir + "\n" +
                "Registry:     " + EvolutionDir + "/" + RegistryName + ".asset",
                "OK");
        }

        // ---------------- Helpers ----------------

        private sealed class EvolutionSpec
        {
            public readonly string BaseWeaponName;
            public readonly string RequiredPassive;
            public readonly string EvolvedAssetName;
            public readonly string EvolvedDisplayName;
            public readonly Dictionary<string, float> Tuning;

            public EvolutionSpec(string baseWeaponName, string requiredPassive,
                string evolvedAssetName, string evolvedDisplayName,
                Dictionary<string, float> tuning)
            {
                BaseWeaponName = baseWeaponName;
                RequiredPassive = requiredPassive;
                EvolvedAssetName = evolvedAssetName;
                EvolvedDisplayName = evolvedDisplayName;
                Tuning = tuning ?? new Dictionary<string, float>();
            }
        }

        private static ScriptableObject CreateEvolvedWeapon(Type weaponDataType, EvolutionSpec spec)
        {
            var path = EvolvedWeaponDir + "/" + spec.EvolvedAssetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath(path, weaponDataType) as ScriptableObject;
            ScriptableObject so;
            if (existing != null)
            {
                so = existing;
            }
            else
            {
                so = ScriptableObject.CreateInstance(weaponDataType);
                so.name = spec.EvolvedAssetName;
                AssetDatabase.CreateAsset(so, path);
            }

            // Seed from base, then apply tuning.
            var basePath = FindAssetPathByName(spec.BaseWeaponName, weaponDataType);
            ScriptableObject baseSo = null;
            if (!string.IsNullOrEmpty(basePath))
                baseSo = AssetDatabase.LoadAssetAtPath(basePath, weaponDataType) as ScriptableObject;

            if (baseSo != null) CopySerializedFields(baseSo, so);

            ApplyDisplayName(so, spec.EvolvedDisplayName);
            ApplyTuning(so, spec.Tuning);
            EditorUtility.SetDirty(so);
            return so;
        }

        private static WeaponEvolutionRecipe CreateRecipe(EvolutionSpec spec, ScriptableObject evolved)
        {
            var recipePath = EvolutionDir + "/Evolution_" + Sanitize(spec.BaseWeaponName) +
                             "_To_" + spec.EvolvedAssetName + ".asset";
            var recipe = AssetDatabase.LoadAssetAtPath<WeaponEvolutionRecipe>(recipePath);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<WeaponEvolutionRecipe>();
                AssetDatabase.CreateAsset(recipe, recipePath);
            }

            // Set the three serialized fields via SerializedObject so the names match the
            // ones declared in WeaponEvolutionRecipe regardless of property visibility.
            var so = new SerializedObject(recipe);
            so.FindProperty("requiredPassiveUpgradeName").stringValue = spec.RequiredPassive;
            so.FindProperty("evolvedWeapon").objectReferenceValue = evolved;

            var weaponDataType = FindTypeByName("WeaponData");
            var basePath = FindAssetPathByName(spec.BaseWeaponName, weaponDataType);
            if (!string.IsNullOrEmpty(basePath))
            {
                so.FindProperty("baseWeapon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath(basePath, weaponDataType ?? typeof(ScriptableObject));
            }
            else
            {
                Debug.LogWarning("[EvolutionGenerator] Base WeaponData '" + spec.BaseWeaponName +
                                 "' not found in project. Recipe at " + recipePath +
                                 " was created with an empty BaseWeapon field - drag it in manually.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static EvolutionRecipeRegistry CreateOrUpdateRegistry(List<WeaponEvolutionRecipe> recipes)
        {
            var path = EvolutionDir + "/" + RegistryName + ".asset";
            var registry = AssetDatabase.LoadAssetAtPath<EvolutionRecipeRegistry>(path);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<EvolutionRecipeRegistry>();
                AssetDatabase.CreateAsset(registry, path);
            }
            registry.EditorSetRecipes(recipes);
            EditorUtility.SetDirty(registry);
            return registry;
        }

        private static void EnsureResourcesAlias(EvolutionRecipeRegistry registry)
        {
            // EvolutionRecipeRegistry.Instance looks for Resources/EvolutionRecipeRegistry.
            // If the registry asset is not in a Resources folder, create a sibling asset
            // pointing at the same recipes. (We copy by SerializedObject so the two assets
            // don't share identity but do share data.)
            var resourcesPath = ResourcesDir + "/" + RegistryName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<EvolutionRecipeRegistry>(resourcesPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<EvolutionRecipeRegistry>();
                AssetDatabase.CreateAsset(existing, resourcesPath);
            }
            existing.EditorSetRecipes(new List<WeaponEvolutionRecipe>(registry.Recipes));
            EditorUtility.SetDirty(existing);
        }

        private static void CopySerializedFields(ScriptableObject source, ScriptableObject target)
        {
            var src = new SerializedObject(source);
            var dst = new SerializedObject(target);
            var it = src.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;
                var dstProp = dst.FindProperty(it.propertyPath);
                if (dstProp == null) continue;
                try { dst.CopyFromSerializedProperty(it); } catch { /* ignore */ }
            }
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyDisplayName(ScriptableObject so, string displayName)
        {
            var sObj = new SerializedObject(so);
            foreach (var fieldName in new[] { "displayName", "name", "weaponName", "title" })
            {
                var p = sObj.FindProperty(fieldName);
                if (p != null && p.propertyType == SerializedPropertyType.String)
                {
                    p.stringValue = displayName;
                    break;
                }
            }
            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyTuning(ScriptableObject so, Dictionary<string, float> tuning)
        {
            if (tuning == null || tuning.Count == 0) return;
            var sObj = new SerializedObject(so);

            // Map of tuning keys -> (candidate field names, operation).
            //   "_mul"     multiplies the existing value
            //   "_add"     adds to the existing value
            //   otherwise  overrides the existing value
            foreach (var kv in tuning)
            {
                var key = kv.Key;
                var value = kv.Value;
                string op = "set";
                string baseKey = key;
                if (key.EndsWith("_mul")) { op = "mul"; baseKey = key.Substring(0, key.Length - 4); }
                else if (key.EndsWith("_add")) { op = "add"; baseKey = key.Substring(0, key.Length - 4); }

                foreach (var candidate in CandidateFieldNames(baseKey))
                {
                    var p = sObj.FindProperty(candidate);
                    if (p == null) continue;
                    if (p.propertyType == SerializedPropertyType.Float)
                    {
                        if (op == "mul") p.floatValue *= value;
                        else if (op == "add") p.floatValue += value;
                        else p.floatValue = value;
                        break;
                    }
                    if (p.propertyType == SerializedPropertyType.Integer)
                    {
                        if (op == "mul") p.intValue = Mathf.RoundToInt(p.intValue * value);
                        else if (op == "add") p.intValue += Mathf.RoundToInt(value);
                        else p.intValue = Mathf.RoundToInt(value);
                        break;
                    }
                }
            }

            sObj.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerable<string> CandidateFieldNames(string baseKey)
        {
            // damage -> damage, baseDamage, Damage
            // cooldown -> cooldown, fireCooldown, fireInterval, attackInterval
            // projectile_scale -> projectileScale, projectileSize, bulletScale
            // pellets -> pellets, pelletCount, projectilesPerShot
            // jumps -> jumps, maxJumps, chainCount
            // falloff -> damageFalloff, falloff, chainFalloff
            switch (baseKey)
            {
                case "damage":           yield return "damage"; yield return "baseDamage"; yield return "Damage"; yield break;
                case "cooldown":         yield return "cooldown"; yield return "fireCooldown"; yield return "fireInterval"; yield return "attackInterval"; yield break;
                case "projectile_scale": yield return "projectileScale"; yield return "projectileSize"; yield return "bulletScale"; yield break;
                case "projectile_size":  yield return "projectileSize"; yield return "projectileScale"; yield break;
                case "pellets":          yield return "pellets"; yield return "pelletCount"; yield return "projectilesPerShot"; yield break;
                case "pellet_count":     yield return "pelletCount"; yield return "pellets"; yield break;
                case "jumps":            yield return "jumps"; yield return "maxJumps"; yield return "chainCount"; yield break;
                case "chain_count":      yield return "chainCount"; yield return "jumps"; yield break;
                case "max_jumps":        yield return "maxJumps"; yield return "jumps"; yield break;
                case "falloff":          yield return "falloff"; yield return "damageFalloff"; yield return "chainFalloff"; yield break;
                case "damage_falloff":   yield return "damageFalloff"; yield return "falloff"; yield break;
                case "chain_falloff":    yield return "chainFalloff"; yield return "falloff"; yield break;
                default:                 yield return baseKey; yield break;
            }
        }

        private static string FindAssetPathByName(string assetName, Type type)
        {
            if (string.IsNullOrEmpty(assetName)) return null;
            var typeFilter = type != null ? "t:" + type.Name + " " : "";
            // Try exact name first, then a looser space-stripped match.
            var guids = AssetDatabase.FindAssets(typeFilter + "\"" + assetName + "\"");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath(p, type ?? typeof(UnityEngine.Object));
                if (asset != null && string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            var stripped = assetName.Replace(" ", "");
            guids = AssetDatabase.FindAssets(typeFilter + stripped);
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var asset = AssetDatabase.LoadAssetAtPath(p, type ?? typeof(UnityEngine.Object));
                if (asset == null) continue;
                if (string.Equals(asset.name.Replace(" ", ""), stripped, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private static Type FindTypeByName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t != null && t.Name == simpleName && typeof(ScriptableObject).IsAssignableFrom(t))
                        return t;
                }
            }
            return null;
        }

        private static void EnsureDir(string assetsRelativeDir)
        {
            if (AssetDatabase.IsValidFolder(assetsRelativeDir)) return;
            var parts = assetsRelativeDir.Split('/');
            var cur = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Unnamed";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars).Trim('_');
        }
    }
}
#endif
