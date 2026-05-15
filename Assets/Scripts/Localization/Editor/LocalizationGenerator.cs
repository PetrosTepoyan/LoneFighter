#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LoneFighter.Localization.EditorTools
{
    /// <summary>
    /// Editor utility that:
    ///  1. Reads every <c>public const string</c> on <see cref="LocalizationKeys"/> via reflection.
    ///  2. Generates / refreshes <c>Assets/Data/Localization/English.asset</c> with one entry per key.
    ///  3. Generates / refreshes <c>Assets/Resources/Localization/LocaleRegistry.asset</c>
    ///     pointing at the English baseline (and any other locales already present in the folder).
    ///
    /// Invoke via the menu: <b>LoneFighter -> Localization -> Generate English Baseline</b>.
    ///
    /// Re-running is safe and **non-destructive** for existing English values:
    ///  - For keys that already exist in the asset, the existing value is preserved.
    ///  - New keys are added with the default English text from <see cref="DefaultsTable"/>
    ///    (or a sensible title-cased version of the key suffix if no explicit default).
    ///  - Stale keys (in the asset but no longer in <see cref="LocalizationKeys"/>) are removed.
    /// </summary>
    public static class LocalizationGenerator
    {
        private const string DataFolder     = "Assets/Data/Localization";
        private const string ResourcesFolder = "Assets/Resources/Localization";
        private const string EnglishAssetPath = DataFolder + "/English.asset";
        private const string RegistryAssetPath = ResourcesFolder + "/LocaleRegistry.asset";

        [MenuItem("LoneFighter/Localization/Generate English Baseline")]
        public static void GenerateEnglishBaseline()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(ResourcesFolder);

            var english = LoadOrCreateEnglish();
            var defaults = DefaultsTable();
            var keys = CollectKeysFromConstants();

            // Preserve any user-edited English strings already in the asset.
            var existing = new Dictionary<string, string>();
            if (english.keys != null && english.values != null)
            {
                var count = Mathf.Min(english.keys.Length, english.values.Length);
                for (var i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(english.keys[i])) continue;
                    existing[english.keys[i]] = english.values[i] ?? string.Empty;
                }
            }

            var newKeys = new string[keys.Count];
            var newValues = new string[keys.Count];
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                newKeys[i] = key;
                if (existing.TryGetValue(key, out var existingValue) && !string.IsNullOrEmpty(existingValue))
                {
                    newValues[i] = existingValue;
                }
                else if (defaults.TryGetValue(key, out var def))
                {
                    newValues[i] = def;
                }
                else
                {
                    newValues[i] = HumanizeKey(key);
                }
            }

            english.localeCode = "en";
            english.displayName = "English";
            english.keys = newKeys;
            english.values = newValues;
            EditorUtility.SetDirty(english);

            var registry = LoadOrCreateRegistry();
            EnsureLocaleInRegistry(registry, english);
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[LocalizationGenerator] English baseline generated with {newKeys.Length} keys -> {EnglishAssetPath}\nRegistry: {RegistryAssetPath}");
            EditorGUIUtility.PingObject(english);
        }

        // -------- Internals --------

        private static LocalizationData LoadOrCreateEnglish()
        {
            var asset = AssetDatabase.LoadAssetAtPath<LocalizationData>(EnglishAssetPath);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<LocalizationData>();
            asset.localeCode = "en";
            asset.displayName = "English";
            AssetDatabase.CreateAsset(asset, EnglishAssetPath);
            return asset;
        }

        private static LocaleRegistry LoadOrCreateRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<LocaleRegistry>(RegistryAssetPath);
            if (registry != null) return registry;

            registry = ScriptableObject.CreateInstance<LocaleRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            return registry;
        }

        private static void EnsureLocaleInRegistry(LocaleRegistry registry, LocalizationData locale)
        {
            // Also pull in any other LocalizationData assets present in the Data folder so
            // newly-added languages show up automatically.
            var found = AssetDatabase.FindAssets($"t:{nameof(LocalizationData)}", new[] { DataFolder });
            var list = new List<LocalizationData>();
            list.Add(locale); // English first → becomes the fallback per LocaleRegistry.Default.

            foreach (var guid in found)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<LocalizationData>(path);
                if (data == null || data == locale) continue;
                if (!list.Contains(data)) list.Add(data);
            }

            registry.locales = list.ToArray();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static List<string> CollectKeysFromConstants()
        {
            var keys = new List<string>();
            var fields = typeof(LocalizationKeys).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            foreach (var f in fields)
            {
                if (!f.IsLiteral || f.IsInitOnly) continue;     // const string only
                if (f.FieldType != typeof(string)) continue;
                var v = (string)f.GetRawConstantValue();
                if (string.IsNullOrEmpty(v)) continue;
                if (!keys.Contains(v)) keys.Add(v);
            }
            keys.Sort(System.StringComparer.Ordinal);
            return keys;
        }

        /// <summary>
        /// Default English strings, keyed by the canonical localization key. Anything not in
        /// the table falls back to a title-cased version of the key suffix.
        /// </summary>
        private static Dictionary<string, string> DefaultsTable()
        {
            return new Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                // UI
                { LocalizationKeys.UiPlay,       "Play"      },
                { LocalizationKeys.UiSettings,   "Settings"  },
                { LocalizationKeys.UiQuit,       "Quit"      },
                { LocalizationKeys.UiBack,       "Back"      },
                { LocalizationKeys.UiResume,     "Resume"    },
                { LocalizationKeys.UiPause,      "Pause"     },
                { LocalizationKeys.UiMenu,       "Menu"      },
                { LocalizationKeys.UiClose,      "Close"     },
                { LocalizationKeys.UiContinue,   "Continue"  },
                { LocalizationKeys.UiOk,         "OK"        },
                { LocalizationKeys.UiCancel,     "Cancel"    },

                // HUD
                { LocalizationKeys.HudKills,     "Kills"     },
                { LocalizationKeys.HudTimer,     "Time"      },
                { LocalizationKeys.HudLevel,     "Lv {0}"    },
                { LocalizationKeys.HudHp,        "HP"        },
                { LocalizationKeys.HudXp,        "XP"        },

                // Level-up
                { LocalizationKeys.LevelupTitle,    "Level Up!"              },
                { LocalizationKeys.LevelupSubtitle, "Choose an upgrade"      },
                { LocalizationKeys.LevelupSkip,     "Skip"                   },
                { LocalizationKeys.LevelupReroll,   "Reroll"                 },

                // Game-over
                { LocalizationKeys.GameoverVictory,      "Victory!"           },
                { LocalizationKeys.GameoverDefeat,       "You Died"           },
                { LocalizationKeys.GameoverRetry,        "Retry"              },
                { LocalizationKeys.GameoverMenu,         "Main Menu"          },
                { LocalizationKeys.GameoverTimeSurvived, "Time Survived"      },
                { LocalizationKeys.GameoverKills,        "Kills"              },
                { LocalizationKeys.GameoverLevel,        "Level"              },

                // Settings
                { LocalizationKeys.SettingsTitle,        "Settings"           },
                { LocalizationKeys.SettingsMaster,       "Master Volume"      },
                { LocalizationKeys.SettingsMusic,        "Music Volume"       },
                { LocalizationKeys.SettingsSfx,          "SFX Volume"         },
                { LocalizationKeys.SettingsHaptics,      "Haptics"            },
                { LocalizationKeys.SettingsTargetFps,    "Target FPS"         },
                { LocalizationKeys.SettingsQuality,      "Quality"            },
                { LocalizationKeys.SettingsLanguage,     "Language"           },
                { LocalizationKeys.SettingsReset,        "Reset to Defaults"  },
                { LocalizationKeys.SettingsFpsUncapped,  "Uncapped"           },
                { LocalizationKeys.SettingsQualityLow,   "Low"                },
                { LocalizationKeys.SettingsQualityMed,   "Medium"             },
                { LocalizationKeys.SettingsQualityHigh,  "High"               },

                // Unlocks
                { LocalizationKeys.UnlockFirstBlood, "First Blood"   },
                { LocalizationKeys.UnlockCenturion,  "Centurion"     },
                { LocalizationKeys.UnlockSurvivor,   "Survivor"      },
                { LocalizationKeys.UnlockMarathoner, "Marathoner"    },
                { LocalizationKeys.UnlockToastTitle, "Unlocked!"     },

                // Tutorial
                { LocalizationKeys.TutorialMove,     "Drag the stick to move"        },
                { LocalizationKeys.TutorialDash,     "Tap the dash button to dodge"  },
                { LocalizationKeys.TutorialAutofire, "Your weapons fire on their own" },
                { LocalizationKeys.TutorialLevelup,  "Pick an upgrade when you level up" },
                { LocalizationKeys.TutorialContinue, "Tap to continue"               },

                // Weapons
                { LocalizationKeys.WeaponPistol,         "Pistol"          },
                { LocalizationKeys.WeaponSpreadShotgun,  "Spread Shotgun"  },
                { LocalizationKeys.WeaponChainLightning, "Chain Lightning" },
                { LocalizationKeys.WeaponOrbitalBlade,   "Orbital Blade"   },

                // Enemies
                { LocalizationKeys.EnemyGrunt,   "Grunt"   },
                { LocalizationKeys.EnemyRunner,  "Runner"  },
                { LocalizationKeys.EnemyTank,    "Tank"    },
                { LocalizationKeys.EnemySpitter, "Spitter" },
                { LocalizationKeys.EnemyDasher,  "Dasher"  },
                { LocalizationKeys.EnemyBomber,  "Bomber"  },
                { LocalizationKeys.EnemyWarden,  "Warden"  },

                // Pickups
                { LocalizationKeys.PickupXpGem,  "XP Gem" },
                { LocalizationKeys.PickupHeal,   "Heal"   },
                { LocalizationKeys.PickupMagnet, "Magnet" },
                { LocalizationKeys.PickupBomb,   "Bomb"   },

                // Pause
                { LocalizationKeys.PauseTitle,   "Paused" },

                // Stats
                { LocalizationKeys.StatsTitle,       "Statistics"  },
                { LocalizationKeys.StatsBestTime,    "Best Time"   },
                { LocalizationKeys.StatsTotalKills,  "Total Kills" },
                { LocalizationKeys.StatsRunsPlayed,  "Runs Played" },
                { LocalizationKeys.StatsVictories,   "Victories"   },
            };
        }

        /// <summary>
        /// "ui.play" -> "Play", "settings.master_volume" -> "Master Volume".
        /// Last-ditch fallback when a key has no entry in <see cref="DefaultsTable"/>.
        /// </summary>
        private static string HumanizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var dot = key.LastIndexOf('.');
            var leaf = dot >= 0 && dot < key.Length - 1 ? key.Substring(dot + 1) : key;
            var parts = leaf.Split('_');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }
    }
}
#endif
