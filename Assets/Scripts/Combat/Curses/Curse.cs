// Curse.cs — data definition for a single negative pre-run modifier.
//
// A Curse is a ScriptableObject so designers can author / tweak balance in the
// Editor without touching code. Curses are picked from the main menu before a
// run via CurseSelectionUi, then applied by CurseManager when the GameManager
// transitions into the Playing state.

using UnityEngine;

namespace LoneFighter.Combat.Curses
{
    /// <summary>
    /// A pre-run hardship the player voluntarily takes on to boost rewards.
    /// Inspired by Hades' Heat and Vampire Survivors' Inverse Mode.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Curses/Curse", fileName = "Curse")]
    public class Curse : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable string id used for save data, telemetry, and lookup. " +
                 "Should be unique across all curse assets.")]
        public string id = "curse_unnamed";

        [Tooltip("Display name shown on the curse card and HUD tooltip.")]
        public string displayName = "Unnamed Curse";

        [TextArea(2, 5)]
        [Tooltip("One-or-two-sentence flavor + mechanical explanation. " +
                 "Use {magnitude} as a placeholder for the formatted magnitude.")]
        public string description = "Makes the run harder.";

        [Tooltip("Icon shown on the selection card and in the in-run HUD strip.")]
        public Sprite icon;

        [Header("Effect")]
        [Tooltip("Which family of effect this curse belongs to. " +
                 "CurseManager dispatches on this enum.")]
        public CurseKind kind = CurseKind.Bloodlust;

        [Tooltip("Strength of the effect, expressed as a unit-free multiplier in [0..1]. " +
                 "Interpretation depends on the kind — e.g. Bloodlust treats 0.30 as +30% damage, " +
                 "Fragile treats 0.25 as -25% max HP. See CurseManager for the exact mapping.")]
        [Range(0f, 5f)]
        public float magnitude = 0.30f;

        [Header("Reward")]
        [Tooltip("Bonus added to the run's reward multiplier when this curse is active. " +
                 "0.20 means +20% rewards (XP / gold / drop chance). Stacks additively with other " +
                 "active curses via CurseRewardCalculator.Multiplier.")]
        [Range(0f, 5f)]
        public float rewardBonus = 0.20f;

        /// <summary>
        /// Convenience: substitutes <c>{magnitude}</c> in the description with the
        /// configured magnitude formatted as a percentage. Falls back to the raw
        /// description if no placeholder is present.
        /// </summary>
        public string FormattedDescription
        {
            get
            {
                if (string.IsNullOrEmpty(description)) return string.Empty;
                if (!description.Contains("{magnitude}")) return description;
                return description.Replace("{magnitude}", Mathf.RoundToInt(magnitude * 100f) + "%");
            }
        }

        /// <summary>
        /// Convenience: substitutes <c>{magnitude}</c> in the description with the
        /// configured reward bonus formatted as a percentage.
        /// </summary>
        public string FormattedRewardLabel => "+" + Mathf.RoundToInt(rewardBonus * 100f) + "% rewards";
    }
}
