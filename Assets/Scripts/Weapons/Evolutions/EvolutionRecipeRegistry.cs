// LoneFighter - Weapon Evolution System
// New file. Do not modify existing files.

using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Weapons.Evolutions
{
    /// <summary>
    /// Project-wide registry of every <see cref="WeaponEvolutionRecipe"/> the game knows
    /// about. There is normally exactly one of these assets in
    /// <c>Assets/Data/Evolutions/EvolutionRecipeRegistry.asset</c>. The
    /// <see cref="EvolutionService"/> looks it up via <see cref="Instance"/> on first use.
    ///
    /// The registry is a plain SO list rather than Resources.LoadAll-based scan so that
    /// designers can disable specific recipes from a single build by removing them from
    /// the list (without deleting the asset).
    /// </summary>
    [CreateAssetMenu(
        fileName = "EvolutionRecipeRegistry",
        menuName = "LoneFighter/Evolutions/Recipe Registry",
        order = 201)]
    public class EvolutionRecipeRegistry : ScriptableObject
    {
        private const string ResourcesPath = "EvolutionRecipeRegistry";

        [Tooltip("All evolution recipes considered by the EvolutionService at runtime.")]
        [SerializeField] private List<WeaponEvolutionRecipe> recipes = new List<WeaponEvolutionRecipe>();

        private static EvolutionRecipeRegistry _cached;

        /// <summary>Read-only view of the recipes in this registry.</summary>
        public IReadOnlyList<WeaponEvolutionRecipe> Recipes => recipes;

        /// <summary>
        /// Static accessor. Looks for an instance in a Resources folder at
        /// <c>Resources/EvolutionRecipeRegistry.asset</c>. Returns null if no asset is
        /// authored yet (the service treats that as "no evolutions available" and stays
        /// silent rather than throwing).
        /// </summary>
        public static EvolutionRecipeRegistry Instance
        {
            get
            {
                if (_cached != null) return _cached;
                _cached = Resources.Load<EvolutionRecipeRegistry>(ResourcesPath);
                if (_cached == null)
                {
                    // Fallback: pick the first registry-typed asset anywhere in memory.
                    // This lets gameplay code work without a Resources/ subfolder during
                    // early prototyping.
                    var all = Resources.FindObjectsOfTypeAll<EvolutionRecipeRegistry>();
                    if (all != null && all.Length > 0) _cached = all[0];
                }
                return _cached;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only setter used by <c>EvolutionGenerator</c> after asset creation.</summary>
        public void EditorSetRecipes(List<WeaponEvolutionRecipe> newRecipes)
        {
            recipes = newRecipes ?? new List<WeaponEvolutionRecipe>();
        }
#endif

        /// <summary>Clears the cached <see cref="Instance"/>; useful for tests / domain reload disabled.</summary>
        public static void InvalidateCache() => _cached = null;
    }
}
