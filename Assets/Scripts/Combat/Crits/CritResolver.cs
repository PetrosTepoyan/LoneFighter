using System;
using System.Reflection;
using UnityEngine;
using LoneFighter.Data;

namespace LoneFighter.Combat.Crits
{
    /// <summary>
    /// Static helper a weapon can call from its Fire path to roll a crit and
    /// receive the post-multiplier damage. Designed for opt-in retrofitting:
    /// nothing here touches the existing WeaponBase / Projectile pipeline -
    /// new weapons (or extension upgrades) explicitly invoke
    /// <see cref="ResolveDamage(WeaponData, float, out bool)"/>.
    ///
    /// Side effects: on a successful crit, fires the sibling
    /// <c>LoneFighter.Effects.CriticalHits.CritFxService.Instance.OnCrit(...)</c>
    /// via reflection so this module compiles and runs whether or not the FX
    /// track is present in the project.
    /// </summary>
    public static class CritResolver
    {
        // Cached reflection lookups. Resolved lazily on first crit so the
        // resolver pays nothing if the sibling module never ships.
        private static bool _fxResolved;
        private static PropertyInfo _fxInstanceProp;
        private static MethodInfo _fxOnCritMethod;
        private static int _fxArity;

        /// <summary>
        /// Roll a crit for the given weapon and return the damage actually dealt.
        /// </summary>
        /// <param name="data">Weapon source (used only for its displayName).</param>
        /// <param name="baseDamage">Damage before crit; usually <c>data.damage</c> after upgrades.</param>
        /// <param name="wasCrit">True if the hit was a crit.</param>
        /// <returns>Final damage to apply.</returns>
        public static float ResolveDamage(WeaponData data, float baseDamage, out bool wasCrit)
        {
            return ResolveDamage(data, baseDamage, Vector3.zero, 0f, out wasCrit);
        }

        /// <summary>
        /// As <see cref="ResolveDamage(WeaponData, float, out bool)"/> but with
        /// an explicit world-space hit position for the FX hook and an
        /// additional per-target chance bonus (e.g. from
        /// <see cref="EnemyCritWeakness"/>).
        /// </summary>
        public static float ResolveDamage(WeaponData data, float baseDamage, Vector3 hitPosition, float extraChance, out bool wasCrit)
        {
            wasCrit = false;
            var service = CritService.Instance;
            if (service == null)
            {
                return baseDamage;
            }

            var name = data != null ? data.displayName : null;
            if (!service.Roll(name, extraChance, out var multiplier))
            {
                return baseDamage;
            }

            wasCrit = true;
            var finalDamage = baseDamage * multiplier;
            InvokeFx(hitPosition, finalDamage, multiplier);
            return finalDamage;
        }

        private static void InvokeFx(Vector3 hitPosition, float finalDamage, float multiplier)
        {
            try
            {
                if (!_fxResolved)
                {
                    _fxResolved = true;
                    var fxType = Type.GetType(
                        "LoneFighter.Effects.CriticalHits.CritFxService, Assembly-CSharp", false);
                    if (fxType == null) return;
                    _fxInstanceProp = fxType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    // Look for the most informative overload first.
                    _fxOnCritMethod = fxType.GetMethod("OnCrit",
                        new[] { typeof(Vector3), typeof(float), typeof(float) });
                    _fxArity = 3;
                    if (_fxOnCritMethod == null)
                    {
                        _fxOnCritMethod = fxType.GetMethod("OnCrit",
                            new[] { typeof(Vector3), typeof(float) });
                        _fxArity = 2;
                    }
                    if (_fxOnCritMethod == null)
                    {
                        _fxOnCritMethod = fxType.GetMethod("OnCrit", Type.EmptyTypes);
                        _fxArity = 0;
                    }
                }

                if (_fxInstanceProp == null || _fxOnCritMethod == null) return;
                var instance = _fxInstanceProp.GetValue(null, null);
                if (instance == null) return;

                object[] args = _fxArity switch
                {
                    3 => new object[] { hitPosition, finalDamage, multiplier },
                    2 => new object[] { hitPosition, finalDamage },
                    _ => Array.Empty<object>(),
                };
                _fxOnCritMethod.Invoke(instance, args);
            }
            catch
            {
                // FX is best-effort. Silently swallow any reflection issue so
                // gameplay damage is never blocked by visual side effects.
            }
        }
    }
}
