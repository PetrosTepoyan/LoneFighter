using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Singleton-style registry that maps an <see cref="Element"/> to its <see cref="ElementProfile"/>.
    /// The active registry is registered automatically when its <c>OnEnable</c> runs (which happens when
    /// the asset is loaded by Unity, e.g. when referenced from a scene/prefab or via <see cref="Resources"/>).
    /// You can drop the asset under a <c>Resources/</c> folder to have it auto-loaded on first access.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Combat/Element Registry", fileName = "ElementRegistry", order = 101)]
    public sealed class ElementRegistry : ScriptableObject
    {
        private static ElementRegistry _active;

        [SerializeField] private List<ElementProfile> profiles = new List<ElementProfile>();

        private readonly Dictionary<Element, ElementProfile> _cache = new Dictionary<Element, ElementProfile>();

        /// <summary>Returns the currently active registry (or null if none loaded).</summary>
        public static ElementRegistry Active
        {
            get
            {
                if (_active == null)
                {
                    // Try to lazily load one from Resources as a convenience.
                    var loaded = Resources.Load<ElementRegistry>("ElementRegistry");
                    if (loaded != null)
                    {
                        loaded.Register();
                    }
                }
                return _active;
            }
        }

        /// <summary>Convenience accessor: returns the profile for <paramref name="element"/> or null.</summary>
        public static ElementProfile Get(Element element)
        {
            var reg = Active;
            return reg != null ? reg.GetProfile(element) : null;
        }

        /// <summary>Instance accessor: returns the profile for <paramref name="element"/> or null.</summary>
        public ElementProfile GetProfile(Element element)
        {
            if (_cache.Count == 0 && profiles != null && profiles.Count > 0)
            {
                RebuildCache();
            }
            return _cache.TryGetValue(element, out var p) ? p : null;
        }

        /// <summary>Makes this registry the active one. Called automatically on load.</summary>
        public void Register()
        {
            _active = this;
            RebuildCache();
        }

        private void OnEnable()
        {
            // Whichever registry is loaded last wins; in practice projects ship with exactly one.
            if (profiles != null && profiles.Count > 0)
            {
                Register();
            }
        }

        private void RebuildCache()
        {
            _cache.Clear();
            if (profiles == null) return;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                if (p == null) continue;
                _cache[p.Element] = p;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only helper used by the profile generator to populate the list.</summary>
        public void EditorSetProfiles(IList<ElementProfile> list)
        {
            profiles.Clear();
            if (list != null) profiles.AddRange(list);
            RebuildCache();
        }
#endif
    }
}
