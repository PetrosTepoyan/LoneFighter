namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Lightweight value type describing a single elemental damage event before resistance is applied.
    /// </summary>
    public struct ElementalDamage
    {
        public float baseDamage;
        public Element element;
        public float elementalMultiplier;

        public ElementalDamage(float baseDamage, Element element, float elementalMultiplier = 1f)
        {
            this.baseDamage = baseDamage;
            this.element = element;
            this.elementalMultiplier = elementalMultiplier;
        }

        /// <summary>Damage before any enemy-side resistance multiplier.</summary>
        public float Pre => baseDamage * elementalMultiplier;
    }
}
