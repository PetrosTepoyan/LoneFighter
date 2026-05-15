using System.Reflection;
using UnityEngine;
using LoneFighter.Weapons;

namespace LoneFighter.UI.WeaponHud
{
    /// <summary>
    /// Read-only reflection accessors for <see cref="WeaponBase"/>'s protected runtime
    /// fields. WeaponBase deliberately hides <c>_cooldownTimer</c>, <c>_cooldownMultiplier</c>,
    /// <c>_damageMultiplier</c>, <c>_projectileSpeedMultiplier</c>, and <c>_bonusPierce</c>
    /// from gameplay code, but the HUD needs to *observe* them to draw a cooldown
    /// radial-fill and an upgrade-tier badge.
    ///
    /// We're explicitly NOT mutating these values, and we're explicitly NOT modifying
    /// <c>WeaponBase</c>. The HARD RULE for this folder is "no edits outside
    /// Assets/Scripts/UI/WeaponHud/", so reflection is the only way to read these
    /// protected fields without changing gameplay code.
    ///
    /// FieldInfo handles are cached statically (one lookup per process). Each accessor
    /// returns a sensible fallback (NaN / 1f / 0) when the field is missing — that
    /// lets <see cref="WeaponSlotUi"/> degrade gracefully if WeaponBase is ever
    /// refactored to rename the backing fields.
    /// </summary>
    internal static class WeaponBaseExtensions
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static readonly FieldInfo CooldownTimerField =
            typeof(WeaponBase).GetField("_cooldownTimer", Flags);
        private static readonly FieldInfo CooldownMultiplierField =
            typeof(WeaponBase).GetField("_cooldownMultiplier", Flags);
        private static readonly FieldInfo DamageMultiplierField =
            typeof(WeaponBase).GetField("_damageMultiplier", Flags);
        private static readonly FieldInfo ProjectileSpeedMultiplierField =
            typeof(WeaponBase).GetField("_projectileSpeedMultiplier", Flags);
        private static readonly FieldInfo BonusPierceField =
            typeof(WeaponBase).GetField("_bonusPierce", Flags);

        /// <summary>
        /// Seconds remaining until the next <c>TryFire()</c>. Returns <see cref="float.NaN"/>
        /// if the field is unavailable, signalling to callers to fall back to local timing.
        /// </summary>
        public static float TryGetCooldownTimer(this WeaponBase weapon)
        {
            if (weapon == null || CooldownTimerField == null) return float.NaN;
            object boxed = CooldownTimerField.GetValue(weapon);
            return boxed is float f ? f : float.NaN;
        }

        public static float GetCooldownMultiplier(this WeaponBase weapon)
        {
            if (weapon == null || CooldownMultiplierField == null) return 1f;
            object boxed = CooldownMultiplierField.GetValue(weapon);
            return boxed is float f ? f : 1f;
        }

        public static float GetDamageMultiplier(this WeaponBase weapon)
        {
            if (weapon == null || DamageMultiplierField == null) return 1f;
            object boxed = DamageMultiplierField.GetValue(weapon);
            return boxed is float f ? f : 1f;
        }

        public static float GetProjectileSpeedMultiplier(this WeaponBase weapon)
        {
            if (weapon == null || ProjectileSpeedMultiplierField == null) return 1f;
            object boxed = ProjectileSpeedMultiplierField.GetValue(weapon);
            return boxed is float f ? f : 1f;
        }

        public static int GetBonusPierce(this WeaponBase weapon)
        {
            if (weapon == null || BonusPierceField == null) return 0;
            object boxed = BonusPierceField.GetValue(weapon);
            return boxed is int i ? i : 0;
        }

        /// <summary>
        /// Effective cooldown in seconds (base data cooldown × current cooldown multiplier).
        /// Used as the denominator when drawing the radial-fill on a slot.
        /// </summary>
        public static float GetEffectiveCooldown(this WeaponBase weapon)
        {
            if (weapon == null || weapon.Data == null) return 0.5f;
            return Mathf.Max(0.01f, weapon.Data.cooldown * weapon.GetCooldownMultiplier());
        }
    }
}
