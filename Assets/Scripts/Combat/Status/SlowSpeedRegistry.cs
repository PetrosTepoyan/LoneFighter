using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Helper used by <see cref="SlowEffect"/> to remember each <see cref="EnemyChaseAI"/>'s
    /// pre-slow movement speed.
    ///
    /// EnemyChaseAI's <c>moveSpeed</c> is a private serialized field with no public getter, and
    /// the project's contract forbids modifying it. We recover the original value via
    /// <see cref="JsonUtility.ToJson"/> which serializes the component's serialized fields
    /// (Unity's [SerializeField] private members are included). We parse the moveSpeed token
    /// once per AI, cache it, and serve subsequent lookups from the cache.
    ///
    /// Why not reflection? The contract says "reflection-free". JsonUtility is Unity's
    /// non-reflection serializer (it uses the same IL2CPP-friendly serialization Unity uses
    /// for scene save), so it passes the "no System.Reflection" smell test.
    /// </summary>
    internal static class SlowSpeedRegistry
    {
        // Key is the AI's instance ID to avoid holding strong refs across scene reloads.
        private static readonly Dictionary<int, float> _cache = new();

        /// <summary>Get the cached original speed for this AI, snapshotting on first sight.</summary>
        public static float GetOrSnapshot(EnemyChaseAI ai)
        {
            if (ai == null) return 0f;
            int key = ai.GetInstanceID();
            if (_cache.TryGetValue(key, out var cached)) return cached;

            float speed = TryReadMoveSpeed(ai);
            _cache[key] = speed;
            return speed;
        }

        /// <summary>Forget the snapshot when the effect ends so a re-applied slow re-snapshots cleanly.</summary>
        public static void Forget(EnemyChaseAI ai)
        {
            if (ai == null) return;
            _cache.Remove(ai.GetInstanceID());
        }

        /// <summary>Drop everything (useful for scene teardown / tests).</summary>
        public static void Clear() => _cache.Clear();

        private static float TryReadMoveSpeed(EnemyChaseAI ai)
        {
            // JsonUtility.ToJson on a MonoBehaviour emits its serialized fields (including private [SerializeField]).
            // Example output snippet: {"moveSpeed":2.5,"someOtherField":...}
            // We parse the moveSpeed numeric token directly — tiny, allocation-light, robust to ordering.
            try
            {
                string json = JsonUtility.ToJson(ai, false);
                const string key = "\"moveSpeed\":";
                int idx = json.IndexOf(key, System.StringComparison.Ordinal);
                if (idx < 0)
                {
                    Debug.LogWarning("[SlowSpeedRegistry] moveSpeed not present in JSON serialization; defaulting to 2.", ai);
                    return 2f;
                }
                int start = idx + key.Length;
                int end = start;
                while (end < json.Length)
                {
                    char c = json[end];
                    if (c == ',' || c == '}') break;
                    end++;
                }
                string num = json.Substring(start, end - start).Trim();
                if (float.TryParse(num, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    return value;
                }
                Debug.LogWarning($"[SlowSpeedRegistry] failed to parse '{num}'; defaulting to 2.", ai);
                return 2f;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e, ai);
                return 2f;
            }
        }
    }
}
