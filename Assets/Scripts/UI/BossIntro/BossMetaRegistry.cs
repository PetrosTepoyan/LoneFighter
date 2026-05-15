using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Authored list of every boss that should trigger a cinematic intro, keyed
    /// by <see cref="BossMeta.enemyDataName"/>. The registry exposes a static
    /// lookup so runtime code never needs to find the asset itself — it just asks
    /// for a boss by name and gets the meta back (or <c>null</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The active registry is set by the first instance to receive
    /// <c>OnEnable</c> (Unity calls this when the asset is loaded by a Resources
    /// reference or held by an open scene). For runtime systems that don't hold a
    /// direct reference, the registry can also be force-loaded from anywhere by
    /// calling <see cref="EnsureLoaded"/> with a list provided by the caller.
    /// </para>
    /// <para>
    /// The lookup is case-sensitive to match how <see cref="BossMeta.enemyDataName"/>
    /// is compared against <c>EnemyData.displayName</c> at the point of use.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "LoneFighter/UI/Boss Intro/Boss Meta Registry", fileName = "BossMetaRegistry")]
    public class BossMetaRegistry : ScriptableObject
    {
        [Tooltip("Drag every BossMeta asset you want to trigger an intro here.")]
        [SerializeField] private List<BossMeta> entries = new List<BossMeta>();

        /// <summary>The currently-active registry. Set by the first asset to load.</summary>
        public static BossMetaRegistry Active { get; private set; }

        private Dictionary<string, BossMeta> _byName;

        private void OnEnable()
        {
            RebuildLookup();
            if (Active == null) Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void OnValidate()
        {
            // Inspector edits — rebuild the lookup so editor-mode previews stay in sync.
            RebuildLookup();
        }

        /// <summary>
        /// Returns the <see cref="BossMeta"/> for a given enemy display name, or
        /// <c>null</c> if no boss is registered under that name. Tolerates a
        /// missing/uninitialised <see cref="Active"/> registry by returning <c>null</c>.
        /// </summary>
        public static BossMeta Get(string enemyDataName)
        {
            if (string.IsNullOrEmpty(enemyDataName)) return null;
            var reg = Active;
            if (reg == null) return null;
            if (reg._byName == null) reg.RebuildLookup();
            return (reg._byName != null && reg._byName.TryGetValue(enemyDataName, out var meta)) ? meta : null;
        }

        /// <summary>
        /// Read-only view of every registered entry. Useful for editor tooling and
        /// for tests that want to enumerate without poking the internal list.
        /// </summary>
        public IReadOnlyList<BossMeta> Entries => entries;

        /// <summary>
        /// Force <paramref name="registry"/> to become <see cref="Active"/>. Lets
        /// gameplay code wire the registry up explicitly (e.g. from a bootstrap
        /// component) instead of relying on Unity load order.
        /// </summary>
        public static void SetActive(BossMetaRegistry registry)
        {
            Active = registry;
            if (registry != null) registry.RebuildLookup();
        }

        private void RebuildLookup()
        {
            if (_byName == null) _byName = new Dictionary<string, BossMeta>(entries.Count);
            else _byName.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var meta = entries[i];
                if (meta == null) continue;
                if (string.IsNullOrEmpty(meta.enemyDataName)) continue;
                // Last-write-wins on duplicate names — keep authoring forgiving.
                _byName[meta.enemyDataName] = meta;
            }
        }
    }
}
