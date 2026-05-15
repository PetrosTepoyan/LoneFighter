// LoneFighter - Weapon Evolution System
// New file. Do not modify existing files.

using UnityEngine;

namespace LoneFighter.Weapons.Evolutions
{
    /// <summary>
    /// ScriptableObject describing a single Vampire-Survivors-style evolution recipe.
    /// Author one of these per evolution pair: a base weapon + a required passive upgrade
    /// yields a more powerful "evolved" weapon when both prerequisites are met.
    ///
    /// Authoring workflow (designer):
    ///   1. Right-click in Project: Create -> LoneFighter -> Evolutions -> Weapon Evolution Recipe.
    ///   2. Drag the base WeaponData into <see cref="BaseWeapon"/>.
    ///   3. Type the exact display name of the passive UpgradeData into
    ///      <see cref="RequiredPassiveUpgradeName"/>. Matching is case-insensitive.
    ///   4. Drag the evolved WeaponData into <see cref="EvolvedWeapon"/>.
    ///   5. Register the recipe in the project's <see cref="EvolutionRecipeRegistry"/> asset.
    ///
    /// The recipe is satisfied when (a) the base weapon is in the player's WeaponInventory
    /// at its max rank, and (b) the named passive upgrade has at least one stack in the
    /// UpgradeService. EvolutionService raises <c>OnEvolutionAvailable</c> on level-up.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WeaponEvolutionRecipe",
        menuName = "LoneFighter/Evolutions/Weapon Evolution Recipe",
        order = 200)]
    public class WeaponEvolutionRecipe : ScriptableObject
    {
        [Tooltip("The base weapon that must be at max rank in the inventory.")]
        [SerializeField] private ScriptableObject baseWeapon;

        [Tooltip("Display name of the passive UpgradeData required to unlock the evolution. " +
                 "Match is case-insensitive but otherwise exact.")]
        [SerializeField] private string requiredPassiveUpgradeName;

        [Tooltip("The evolved super-weapon granted in place of the base weapon.")]
        [SerializeField] private ScriptableObject evolvedWeapon;

        /// <summary>
        /// The base WeaponData asset that must be in the WeaponInventory and maxed.
        /// Typed as ScriptableObject so this assembly compiles even before WeaponData exists;
        /// at runtime the EvolutionService passes the object through to WeaponInventory APIs
        /// without needing to know the concrete type.
        /// </summary>
        public ScriptableObject BaseWeapon => baseWeapon;

        /// <summary>Display name of the prerequisite passive upgrade.</summary>
        public string RequiredPassiveUpgradeName => requiredPassiveUpgradeName;

        /// <summary>The evolved WeaponData asset granted when the recipe fires.</summary>
        public ScriptableObject EvolvedWeapon => evolvedWeapon;

        /// <summary>Quick validity check used by EvolutionService before evaluating.</summary>
        public bool IsValid()
        {
            return baseWeapon != null
                && evolvedWeapon != null
                && !string.IsNullOrWhiteSpace(requiredPassiveUpgradeName);
        }
    }
}
