using UnityEngine;

namespace LoneFighter.Combat.Crits
{
    /// <summary>
    /// Optional MonoBehaviour added to an enemy prefab to grant an extra crit
    /// chance bonus when this specific enemy is hit. The bonus is consumed by
    /// callers that pass an <c>extraChance</c> argument to
    /// <see cref="CritResolver.ResolveDamage(LoneFighter.Data.WeaponData, float, UnityEngine.Vector3, float, out bool)"/>.
    ///
    /// To use:
    ///   1. Add this component to the enemy prefab root (or any parent of its colliders).
    ///   2. Set <see cref="extraCritChance"/> in the inspector (e.g. 0.10 for +10%).
    ///   3. From the weapon hit path, look up the component on the impacted enemy:
    ///        <code>
    ///        var weak = hitEnemy.GetComponentInParent&lt;EnemyCritWeakness&gt;();
    ///        var bonus = weak != null ? weak.extraCritChance : 0f;
    ///        var dmg = CritResolver.ResolveDamage(weaponData, baseDamage, pos, bonus, out var crit);
    ///        </code>
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyCritWeakness : MonoBehaviour
    {
        [Tooltip("Additive crit chance bonus while attacking this enemy. 0.10 = +10 percentage points.")]
        [Range(-1f, 1f)] public float extraCritChance = 0.10f;

        /// <summary>Convenience accessor that tolerates a null receiver.</summary>
        public static float GetBonus(Component sourceOnEnemy)
        {
            if (sourceOnEnemy == null) return 0f;
            var weak = sourceOnEnemy.GetComponentInParent<EnemyCritWeakness>();
            return weak != null ? weak.extraCritChance : 0f;
        }

        /// <summary>Same as above, but takes a GameObject.</summary>
        public static float GetBonus(GameObject sourceOnEnemy)
        {
            if (sourceOnEnemy == null) return 0f;
            var weak = sourceOnEnemy.GetComponentInParent<EnemyCritWeakness>();
            return weak != null ? weak.extraCritChance : 0f;
        }
    }
}
