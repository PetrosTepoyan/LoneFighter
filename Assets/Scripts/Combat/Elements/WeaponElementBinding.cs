using UnityEngine;

namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Drop on a weapon GameObject to declare which <see cref="Element"/> its hits deal.
    /// Consumed via <see cref="Component.TryGetComponent"/> by status / VFX systems.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LoneFighter/Combat/Weapon Element Binding")]
    public sealed class WeaponElementBinding : MonoBehaviour
    {
        [SerializeField] private Element element = Element.Physical;

        [Tooltip("Optional per-weapon multiplier applied to the elemental portion of a hit (1 = no change).")]
        [Min(0f)]
        [SerializeField] private float elementalMultiplier = 1f;

        /// <summary>Element this weapon currently deals damage as.</summary>
        public Element Get() => element;

        /// <summary>Per-weapon elemental damage multiplier (defaults to 1).</summary>
        public float Multiplier => elementalMultiplier <= 0f ? 1f : elementalMultiplier;

        /// <summary>Runtime setter for upgrades / rune system.</summary>
        public void Set(Element value) => element = value;

        /// <summary>Runtime setter for the multiplier.</summary>
        public void SetMultiplier(float value) => elementalMultiplier = Mathf.Max(0f, value);
    }
}
