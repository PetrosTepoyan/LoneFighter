using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using LoneFighter.Data;
using LoneFighter.Enemies;
using LoneFighter.Player;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LoneFighter.Debugging.Console
{
    /// <summary>
    /// Static registry of developer-console commands. Commands are registered by lowercase name
    /// with a handler that receives the parsed argument array (excluding the command name itself)
    /// and returns a string response to print into the console log. Built-in commands are
    /// auto-registered the first time any of the public members are touched.
    /// </summary>
    public static class ConsoleCommandRegistry
    {
        public sealed class Command
        {
            public string Name;
            public string Description;
            public Func<string[], string> Handler;
        }

        private static readonly Dictionary<string, Command> _commands =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool _builtinsRegistered;

        // Cached enemy data lookup (built lazily on first `spawn` call).
        private static Dictionary<string, EnemyData> _enemyByName;

        // ---------------------------------------------------------------------
        // Registration
        // ---------------------------------------------------------------------
        public static void Register(string name, Func<string[], string> handler, string description)
        {
            EnsureBuiltins();
            RegisterInternal(name, handler, description);
        }

        public static bool Unregister(string name)
        {
            EnsureBuiltins();
            if (string.IsNullOrEmpty(name)) return false;
            return _commands.Remove(name.ToLowerInvariant());
        }

        public static bool TryGet(string name, out Command cmd)
        {
            EnsureBuiltins();
            cmd = null;
            if (string.IsNullOrEmpty(name)) return false;
            return _commands.TryGetValue(name.ToLowerInvariant(), out cmd);
        }

        public static IReadOnlyDictionary<string, Command> All
        {
            get { EnsureBuiltins(); return _commands; }
        }

        public static List<string> AllNames()
        {
            EnsureBuiltins();
            var names = new List<string>(_commands.Count);
            foreach (var kv in _commands) names.Add(kv.Value.Name);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        /// <summary>
        /// Parses a raw input line and dispatches it. Returns the textual response (which the
        /// console pushes into the log). Empty input yields an empty response.
        /// </summary>
        public static string Execute(string line)
        {
            EnsureBuiltins();
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;

            string[] tokens = Tokenize(line);
            if (tokens.Length == 0) return string.Empty;

            string name = tokens[0];
            string[] args = new string[tokens.Length - 1];
            Array.Copy(tokens, 1, args, 0, args.Length);

            if (!_commands.TryGetValue(name.ToLowerInvariant(), out var cmd))
            {
                return $"Unknown command: '{name}'. Type 'help' for the list.";
            }

            try
            {
                return cmd.Handler(args) ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"[error] {ex.GetType().Name}: {ex.Message}";
            }
        }

        public static string[] Tokenize(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return Array.Empty<string>();
            // Simple whitespace tokenizer that supports double-quoted strings for multi-word args.
            var list = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list.ToArray();
        }

        // ---------------------------------------------------------------------
        // Built-ins
        // ---------------------------------------------------------------------
        private static void RegisterInternal(string name, Func<string[], string> handler, string description)
        {
            if (string.IsNullOrWhiteSpace(name) || handler == null) return;
            string canonical = name.Trim();
            _commands[canonical.ToLowerInvariant()] = new Command
            {
                Name = canonical,
                Description = description ?? string.Empty,
                Handler = handler
            };
        }

        private static void EnsureBuiltins()
        {
            if (_builtinsRegistered) return;
            _builtinsRegistered = true;

            RegisterInternal("help",       Cmd_Help,       "Lists all registered commands.");
            RegisterInternal("clear",      Cmd_Clear,      "Clears the console log.");
            RegisterInternal("give_xp",    Cmd_GiveXp,     "Usage: give_xp <amount> — adds XP to the player.");
            RegisterInternal("spawn",      Cmd_Spawn,      "Usage: spawn <enemyName> [count] — spawns enemies via EnemySpawner.");
            RegisterInternal("set_hp",     Cmd_SetHp,      "Usage: set_hp <amount> — sets PlayerHealth.Current.");
            RegisterInternal("god_mode",   Cmd_GodMode,    "Usage: god_mode <on|off> — toggles invulnerability via the Cheats system if present.");
            RegisterInternal("time_scale", Cmd_TimeScale,  "Usage: time_scale <0..2> — sets Time.timeScale.");
            RegisterInternal("next_level", Cmd_NextLevel,  "Forces an instant level-up.");
            RegisterInternal("kill_all",   Cmd_KillAll,    "Damages every registered enemy for 99999.");
            RegisterInternal("quit",       Cmd_Quit,       "Quits the application.");
            RegisterInternal("version",    Cmd_Version,    "Prints the bundle version and build number.");
        }

        // ---- help ---------------------------------------------------------------
        private static string Cmd_Help(string[] args)
        {
            var names = AllNames();
            var sb = new StringBuilder();
            sb.Append("Commands (").Append(names.Count).Append("):\n");
            foreach (var n in names)
            {
                if (_commands.TryGetValue(n.ToLowerInvariant(), out var c))
                {
                    sb.Append("  ").Append(c.Name);
                    if (!string.IsNullOrEmpty(c.Description))
                    {
                        sb.Append(" — ").Append(c.Description);
                    }
                    sb.Append('\n');
                }
            }
            return sb.ToString().TrimEnd();
        }

        // ---- clear --------------------------------------------------------------
        private static string Cmd_Clear(string[] args)
        {
            // Sentinel — DevConsole intercepts a null response from this exact command.
            // We also call DevConsole.Instance directly when available so external callers behave the same.
            var inst = DevConsole.Instance;
            if (inst != null) inst.ClearLog();
            return string.Empty;
        }

        // ---- give_xp ------------------------------------------------------------
        private static string Cmd_GiveXp(string[] args)
        {
            if (args.Length < 1) return "Usage: give_xp <amount>";
            if (!int.TryParse(args[0], out int amount)) return $"Invalid amount: '{args[0]}'";
            var pc = PlayerController.Instance;
            if (pc == null) return "PlayerController.Instance is null (no active player).";
            var level = pc.GetComponent<PlayerLevel>();
            if (level == null) return "PlayerLevel component not found on the player.";
            level.AddXp(amount);
            return $"Added {amount} XP. Level={level.Level} XP={level.CurrentXp}/{level.XpToNext}.";
        }

        // ---- spawn --------------------------------------------------------------
        private static string Cmd_Spawn(string[] args)
        {
            if (args.Length < 1) return "Usage: spawn <enemyName> [count]";
            int count = 1;
            if (args.Length >= 2 && !int.TryParse(args[1], out count))
                return $"Invalid count: '{args[1]}'";
            count = Mathf.Clamp(count, 1, 200);

            var spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner == null) return "No EnemySpawner found in the active scene.";

            EnsureEnemyDataCache();
            string key = args[0].ToLowerInvariant();
            if (!_enemyByName.TryGetValue(key, out var data) || data == null)
            {
                // Fall back to substring match if the exact name lookup failed.
                EnemyData partial = null;
                foreach (var kv in _enemyByName)
                {
                    if (kv.Key.Contains(key)) { partial = kv.Value; break; }
                }
                if (partial == null)
                {
                    return $"No EnemyData matches '{args[0]}'. Available: {string.Join(", ", _enemyByName.Keys)}";
                }
                data = partial;
            }

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (spawner.TrySpawn(data)) spawned++;
            }
            return $"Spawned {spawned}/{count} '{data.displayName}'.";
        }

        private static void EnsureEnemyDataCache()
        {
            if (_enemyByName != null) return;
            _enemyByName = new Dictionary<string, EnemyData>(StringComparer.OrdinalIgnoreCase);

            // Primary path: Resources folders.
            var loaded = Resources.LoadAll<EnemyData>(string.Empty);
            for (int i = 0; i < loaded.Length; i++)
            {
                AddEnemyToCache(loaded[i]);
            }

#if UNITY_EDITOR
            // Editor fallback: scan the AssetDatabase so the dev console works even when
            // EnemyData assets live outside a Resources folder.
            if (_enemyByName.Count == 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:EnemyData");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                    AddEnemyToCache(data);
                }
            }
#endif
        }

        private static void AddEnemyToCache(EnemyData data)
        {
            if (data == null) return;
            if (!string.IsNullOrEmpty(data.displayName))
            {
                _enemyByName[data.displayName] = data;
                _enemyByName[data.displayName.Replace(" ", string.Empty)] = data;
            }
            if (!string.IsNullOrEmpty(data.name))
            {
                _enemyByName[data.name] = data;
            }
        }

        // ---- set_hp -------------------------------------------------------------
        private static string Cmd_SetHp(string[] args)
        {
            if (args.Length < 1) return "Usage: set_hp <amount>";
            if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float amount))
            {
                return $"Invalid amount: '{args[0]}'";
            }

            var pc = PlayerController.Instance;
            if (pc == null) return "PlayerController.Instance is null (no active player).";
            var hp = pc.GetComponent<PlayerHealth>();
            if (hp == null) return "PlayerHealth component not found on the player.";

            // Prefer reflection on the backing field so we honour the public API surface
            // (Current has a private setter). Fall back to ApplyDamage/Heal arithmetic.
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = typeof(PlayerHealth).GetProperty("Current", Flags);
            float clamped = Mathf.Clamp(amount, 0f, hp.Max);

            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(hp, clamped);
                return $"HP set to {clamped}/{hp.Max} (via property setter).";
            }

            // Reflection on the auto-generated backing field for { get; private set; }.
            var field = typeof(PlayerHealth).GetField("<Current>k__BackingField", Flags);
            if (field != null)
            {
                field.SetValue(hp, clamped);
                return $"HP set to {clamped}/{hp.Max} (via backing field).";
            }

            // Last resort: bring HP to 0 then heal back up.
            hp.ApplyDamage(hp.Current);
            if (clamped > 0f) hp.Heal(clamped);
            return $"HP approximated to ~{clamped}/{hp.Max} (via ApplyDamage+Heal fallback).";
        }

        // ---- god_mode -----------------------------------------------------------
        private static string Cmd_GodMode(string[] args)
        {
            if (args.Length < 1) return "Usage: god_mode <on|off>";
            bool on = ParseBool(args[0]);

            // Reflection-based discovery so we don't take a hard dependency on a sibling
            // Cheats system that may or may not exist in this branch.
            var cheatsType = FindTypeByName("Cheats")
                          ?? FindTypeByName("CheatsService")
                          ?? FindTypeByName("CheatManager");
            if (cheatsType != null)
            {
                if (TryInvokeCheatsToggle(cheatsType, on, out string result)) return result;
            }

            // Fallback: directly clamp PlayerHealth.Current to Max repeatedly via a coroutine-free
            // approach is not possible from a static class — instead we just heal the player and
            // print a hint. We at least tell the user what we tried.
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var hp = pc.GetComponent<PlayerHealth>();
                if (hp != null && on) hp.Heal(hp.Max);
            }
            return on
                ? "god_mode: no Cheats system found — healed player to full as a partial fallback."
                : "god_mode: no Cheats system found.";
        }

        private static bool TryInvokeCheatsToggle(Type cheatsType, bool on, out string result)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.Instance | BindingFlags.Static;

            // Locate a singleton/instance accessor.
            object instance = null;
            var instanceProp = cheatsType.GetProperty("Instance", Flags);
            if (instanceProp != null) instance = instanceProp.GetValue(null);
            if (instance == null)
            {
                var instanceField = cheatsType.GetField("Instance", Flags);
                if (instanceField != null) instance = instanceField.GetValue(null);
            }
            if (instance == null && typeof(UnityEngine.Object).IsAssignableFrom(cheatsType))
            {
                instance = UnityEngine.Object.FindFirstObjectByType(cheatsType);
            }

            // Try a method first: SetGodMode(bool), ToggleGodMode(bool), GodMode(bool).
            string[] candidateMethods = { "SetGodMode", "ToggleGodMode", "GodMode", "SetInvulnerable" };
            for (int i = 0; i < candidateMethods.Length; i++)
            {
                var m = cheatsType.GetMethod(candidateMethods[i], Flags, null, new[] { typeof(bool) }, null);
                if (m != null)
                {
                    m.Invoke(m.IsStatic ? null : instance, new object[] { on });
                    result = $"god_mode {(on ? "on" : "off")} (via {cheatsType.Name}.{candidateMethods[i]}).";
                    return true;
                }
            }

            // Fall back to writable property/field.
            string[] candidateMembers = { "GodMode", "Invulnerable", "IsGod" };
            for (int i = 0; i < candidateMembers.Length; i++)
            {
                var p = cheatsType.GetProperty(candidateMembers[i], Flags);
                if (p != null && p.CanWrite)
                {
                    p.SetValue(p.GetSetMethod(true) != null && p.GetSetMethod(true).IsStatic ? null : instance, on);
                    result = $"god_mode {(on ? "on" : "off")} (via {cheatsType.Name}.{candidateMembers[i]}).";
                    return true;
                }
                var f = cheatsType.GetField(candidateMembers[i], Flags);
                if (f != null)
                {
                    f.SetValue(f.IsStatic ? null : instance, on);
                    result = $"god_mode {(on ? "on" : "off")} (via {cheatsType.Name}.{candidateMembers[i]} field).";
                    return true;
                }
            }

            result = $"Found type '{cheatsType.Name}' but no compatible godmode member.";
            return false;
        }

        // ---- time_scale ---------------------------------------------------------
        private static string Cmd_TimeScale(string[] args)
        {
            if (args.Length < 1) return "Usage: time_scale <0..2>";
            if (!float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float ts))
            {
                return $"Invalid value: '{args[0]}'";
            }
            ts = Mathf.Clamp(ts, 0f, 2f);
            Time.timeScale = ts;
            return $"Time.timeScale = {ts:0.##}";
        }

        // ---- next_level ---------------------------------------------------------
        private static string Cmd_NextLevel(string[] args)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return "PlayerController.Instance is null (no active player).";
            var level = pc.GetComponent<PlayerLevel>();
            if (level == null) return "PlayerLevel component not found on the player.";
            int needed = Mathf.Max(1, level.XpToNext);
            // A massive overshoot guarantees a level-up regardless of XpMultiplier.
            level.AddXp(needed * 10);
            return $"Forced level-up. Now level {level.Level}.";
        }

        // ---- kill_all -----------------------------------------------------------
        private static string Cmd_KillAll(string[] args)
        {
            int killed = 0;
            var list = EnemyRegistry.Enemies;
            // Iterate over a snapshot — EnemyHealth.ApplyDamage will mutate the registry on death.
            var snapshot = new List<EnemyBase>(list.Count);
            for (int i = 0; i < list.Count; i++) snapshot.Add(list[i]);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var e = snapshot[i];
                if (e == null) continue;
                var hp = e.GetComponent<EnemyHealth>();
                if (hp == null) continue;
                hp.ApplyDamage(99999f);
                killed++;
            }
            return $"kill_all: damaged {killed} enemies.";
        }

        // ---- quit ---------------------------------------------------------------
        private static string Cmd_Quit(string[] args)
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
            return "Stopping play mode.";
#else
            Application.Quit();
            return "Quitting...";
#endif
        }

        // ---- version ------------------------------------------------------------
        private static string Cmd_Version(string[] args)
        {
            string bundle = Application.version;
            string build = "?";

#if UNITY_EDITOR
            // PlayerSettings is editor-only; mirror the requested behaviour while staying compilable in players.
            try
            {
                bundle = PlayerSettings.bundleVersion;
#if UNITY_IOS
                build = PlayerSettings.iOS.buildNumber;
#elif UNITY_ANDROID
                build = PlayerSettings.Android.bundleVersionCode.ToString();
#else
                build = PlayerSettings.macOS.buildNumber;
#endif
            }
            catch (Exception) { /* leave defaults */ }
#endif
            return $"version: {bundle}  build: {build}  unity: {Application.unityVersion}";
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static bool ParseBool(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Trim().ToLowerInvariant();
            return s == "on" || s == "1" || s == "true" || s == "yes" || s == "enable" || s == "enabled";
        }

        private static Type FindTypeByName(string simpleName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try { types = assemblies[i].GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                for (int t = 0; t < types.Length; t++)
                {
                    var ty = types[t];
                    if (ty == null) continue;
                    if (ty.Name == simpleName) return ty;
                }
            }
            return null;
        }
    }
}
