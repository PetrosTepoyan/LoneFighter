using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Drop on an enemy GameObject to give it per-element resistance/vulnerability multipliers.
    /// Multiplier semantics:
    ///   1.0 = neutral, &lt;1 = resistant (e.g. 0.5 takes half damage), &gt;1 = weak (e.g. 2.0 takes double).
    /// Missing entries default to <see cref="DefaultMultiplier"/> (= 1).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LoneFighter/Combat/Enemy Resistance")]
    public sealed class EnemyResistance : MonoBehaviour
    {
        public const float DefaultMultiplier = 1f;

        [System.Serializable]
        private struct Entry
        {
            public Element element;
            [Min(0f)] public float multiplier;
        }

        [Tooltip("Per-element multipliers. Entries omitted default to 1 (neutral).")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<Element, float> _map;

        private void Awake() => RebuildMap();
        private void OnValidate() => RebuildMap();

        private void RebuildMap()
        {
            if (_map == null) _map = new Dictionary<Element, float>(entries?.Count ?? 0);
            else _map.Clear();

            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                _map[e.element] = Mathf.Max(0f, e.multiplier);
            }
        }

        /// <summary>Returns the multiplier for <paramref name="element"/>, or 1 if not configured.</summary>
        public float GetMultiplier(Element element)
        {
            if (_map == null) RebuildMap();
            return _map != null && _map.TryGetValue(element, out var m) ? m : DefaultMultiplier;
        }

        /// <summary>Runtime setter for ailments / buffs that temporarily change resistance.</summary>
        public void SetMultiplier(Element element, float multiplier)
        {
            if (_map == null) RebuildMap();
            _map[element] = Mathf.Max(0f, multiplier);
        }
    }
}
