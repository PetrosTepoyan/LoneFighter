using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Combat.Crits
{
    /// <summary>
    /// ScriptableObject describing the base crit chance / multiplier for the run
    /// and any per-weapon overrides keyed by <see cref="LoneFighter.Data.WeaponData.displayName"/>
    /// (or any caller-supplied weapon name string).
    ///
    /// This asset is read-only at runtime; mutable run-time crit state lives on
    /// <see cref="CritService"/>. Modifiers from upgrades are applied to the
    /// service, not to this asset, so the asset itself survives play sessions
    /// untouched.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Combat/Crit Profile", fileName = "CritProfile")]
    public class CritProfile : ScriptableObject
    {
        [Serializable]
        public struct WeaponOverride
        {
            [Tooltip("Match against WeaponData.displayName (case-insensitive).")]
            public string weaponName;

            [Tooltip("If true, baseChance below replaces the profile's base chance for this weapon. " +
                     "If false, baseChance is added on top of the profile base chance.")]
            public bool replaceChance;

            [Range(0f, 1f)] public float baseChance;

            [Tooltip("If true, multiplier below replaces the profile multiplier for this weapon. " +
                     "If false, multiplier is added on top of the profile multiplier.")]
            public bool replaceMultiplier;

            [Tooltip("Crit damage multiplier override. Multiplier of 2.0 means 200% damage on crit.")]
            public float multiplier;
        }

        [Header("Base values")]
        [Range(0f, 1f)]
        [Tooltip("Base probability of a hit becoming a critical (0..1). Default 5%.")]
        public float baseCritChance = 0.05f;

        [Tooltip("Damage multiplier applied on a successful crit. Default 2.0 (200% damage).")]
        public float baseCritMultiplier = 2.0f;

        [Header("Per-weapon overrides")]
        [Tooltip("Optional list of overrides matched by weapon name. The first matching entry wins.")]
        public List<WeaponOverride> weaponOverrides = new();

        /// <summary>
        /// Resolves the configured chance and multiplier for a given weapon name.
        /// Returns the profile defaults if no override is present or the name is null/empty.
        /// </summary>
        public void Resolve(string weaponName, out float chance, out float multiplier)
        {
            chance = baseCritChance;
            multiplier = baseCritMultiplier;

            if (string.IsNullOrEmpty(weaponName) || weaponOverrides == null) return;

            for (int i = 0; i < weaponOverrides.Count; i++)
            {
                var ov = weaponOverrides[i];
                if (string.IsNullOrEmpty(ov.weaponName)) continue;
                if (!string.Equals(ov.weaponName, weaponName, StringComparison.OrdinalIgnoreCase)) continue;

                chance = ov.replaceChance ? ov.baseChance : (baseCritChance + ov.baseChance);
                multiplier = ov.replaceMultiplier ? ov.multiplier : (baseCritMultiplier + ov.multiplier);
                return;
            }
        }
    }
}
