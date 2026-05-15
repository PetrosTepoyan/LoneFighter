namespace LoneFighter.Combat.Elements
{
    /// <summary>
    /// Damage element types used by weapons, enemy resistances and status effects.
    /// Keep ordering stable - values are persisted in ScriptableObjects and prefabs.
    /// </summary>
    public enum Element
    {
        Physical = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Poison = 4,
    }
}
