using System;
using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Combat.Crits
{
    /// <summary>
    /// Bridge between the project's UpgradeService and <see cref="CritService"/>.
    /// Because we cannot modify existing files, this adapter is intentionally
    /// permissive: it accepts crit-related upgrades through any of three
    /// channels so integrators can pick whichever requires the least change.
    ///
    /// Channels (highest specificity first):
    ///
    /// 1. <b>Direct API.</b>  Any code anywhere may call
    ///    <see cref="ApplyCritChance"/> / <see cref="ApplyCritMultiplier"/>.
    ///    Recommended for new upgrade-application code paths.
    ///
    /// 2. <b>UpgradeData name matching.</b>  Pass an <see cref="UpgradeData"/>
    ///    asset to <see cref="ApplyUpgrade"/>. If its
    ///    <see cref="UpgradeData.displayName"/> matches one of the names in
    ///    <see cref="critChanceUpgradeNames"/> (case-insensitive,
    ///    e.g. "Bloodthirst", "Sharpshooter", "Killing Edge"), its magnitude is
    ///    applied as a crit chance delta. Same for <see cref="critMultiplierUpgradeNames"/>.
    ///
    /// 3. <b>External enum extension.</b>  When the team wants to mint a new
    ///    real UpgradeKind value (e.g. <c>UpgradeKind.PlayerCritChance</c>),
    ///    they can call <see cref="ApplyExternalKind"/> with a string token of
    ///    their choice. The adapter only cares about the token text, not the
    ///    enum, so the actual enum doesn't have to live in this folder.
    ///
    /// Drop the component on the same GameObject as <see cref="CritService"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class CritUpgradeAdapter : MonoBehaviour
    {
        public static CritUpgradeAdapter Instance { get; private set; }

        [Header("Name-matched UpgradeData routing")]
        [Tooltip("UpgradeData.displayName values that should be treated as +crit chance. " +
                 "Magnitude is interpreted as an additive delta (0.05 = +5% crit chance).")]
        public List<string> critChanceUpgradeNames = new() { "Bloodthirst", "Sharpshooter", "Killing Edge" };

        [Tooltip("UpgradeData.displayName values that should be treated as +crit multiplier. " +
                 "Magnitude is added on top of the base multiplier.")]
        public List<string> critMultiplierUpgradeNames = new() { "Heavy Crits", "Overkill" };

        [Header("External-kind tokens")]
        [Tooltip("String tokens treated as +crit chance when forwarded via ApplyExternalKind. " +
                 "Useful if the UpgradeKind enum gains a value like 'PlayerCritChance' externally.")]
        public List<string> externalCritChanceTokens = new() { "PlayerCritChance", "CritChance" };

        [Tooltip("String tokens treated as +crit multiplier when forwarded via ApplyExternalKind.")]
        public List<string> externalCritMultiplierTokens = new() { "PlayerCritMultiplier", "CritMultiplier" };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Direct entry point: apply an additive crit chance delta.</summary>
        public void ApplyCritChance(float delta) => CritService.RaiseCritChanceModified(delta);

        /// <summary>Direct entry point: apply an additive crit multiplier delta.</summary>
        public void ApplyCritMultiplier(float delta) => CritService.RaiseCritMultiplierModified(delta);

        /// <summary>
        /// Inspect an <see cref="UpgradeData"/> and, if its name matches a
        /// configured crit upgrade, forward its magnitude to <see cref="CritService"/>.
        /// Returns true if the upgrade was handled here.
        /// </summary>
        public bool ApplyUpgrade(UpgradeData upgrade)
        {
            if (upgrade == null) return false;

            if (Matches(critChanceUpgradeNames, upgrade.displayName))
            {
                ApplyCritChance(upgrade.magnitude);
                return true;
            }
            if (Matches(critMultiplierUpgradeNames, upgrade.displayName))
            {
                ApplyCritMultiplier(upgrade.magnitude);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Forward an arbitrary string-named upgrade kind (e.g. one minted as a
        /// new enum value externally) with a magnitude. Returns true if the
        /// token was recognised.
        /// </summary>
        public bool ApplyExternalKind(string kindToken, float magnitude)
        {
            if (string.IsNullOrEmpty(kindToken)) return false;
            if (Matches(externalCritChanceTokens, kindToken))
            {
                ApplyCritChance(magnitude);
                return true;
            }
            if (Matches(externalCritMultiplierTokens, kindToken))
            {
                ApplyCritMultiplier(magnitude);
                return true;
            }
            return false;
        }

        private static bool Matches(List<string> list, string candidate)
        {
            if (list == null || string.IsNullOrEmpty(candidate)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.IsNullOrEmpty(list[i])) continue;
                if (string.Equals(list[i], candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
