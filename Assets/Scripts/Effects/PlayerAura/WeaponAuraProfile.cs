using UnityEngine;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Visual configuration for a per-weapon "aura" rendered behind the player.
    /// Authors a radial glow sprite, an HDR-friendly tint, a pulse rate, a base
    /// size, and how the intensity scales with the weapon's level.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Effects/Weapon Aura Profile",
                     fileName = "WeaponAuraProfile")]
    public class WeaponAuraProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Optional display label, useful for debugging in the inspector.")]
        public string displayName = "Aura";

        [Header("Sprite")]
        [Tooltip("Radial glow / soft-circle sprite. Should be authored centered with " +
                 "alpha falling off to the edges. Used for the aura SpriteRenderer.")]
        public Sprite sprite;

        [Header("Color")]
        [Tooltip("HDR-friendly base tint. Multiplied by intensity at runtime; use values " +
                 ">1 for bloom-friendly glow with URP 2D + Bloom post FX.")]
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.7f, 0.3f, 1f);

        [Header("Motion")]
        [Tooltip("Pulse frequency in Hz (cycles per second). 0 = no pulse.")]
        [Min(0f)] public float pulseHz = 1.25f;

        [Tooltip("How much the sprite scale modulates each pulse cycle (0 = static, " +
                 "0.15 = ±15% breathing).")]
        [Range(0f, 1f)] public float pulseScaleAmplitude = 0.08f;

        [Tooltip("How much the alpha intensity modulates each pulse cycle.")]
        [Range(0f, 1f)] public float pulseAlphaAmplitude = 0.25f;

        [Header("Size")]
        [Tooltip("World-space base scale of the aura SpriteRenderer (uniform).")]
        [Min(0.01f)] public float baseScale = 1.8f;

        [Tooltip("Additional scale added per weapon level above 1. " +
                 "Final = baseScale + (level-1) * scalePerLevel.")]
        public float scalePerLevel = 0.12f;

        [Header("Intensity")]
        [Tooltip("Base alpha multiplier on the color (0..N). Multiplied with the pulse " +
                 "envelope to produce the final emission.")]
        [Range(0f, 4f)] public float baseIntensity = 1f;

        [Tooltip("Extra intensity added per level above 1. Allows higher-tier weapons " +
                 "to glow more aggressively.")]
        public float intensityPerLevel = 0.15f;

        [Header("Render")]
        [Tooltip("Sorting layer name applied to the aura SpriteRenderer. Leave empty to " +
                 "inherit the default layer.")]
        public string sortingLayerName = "";

        [Tooltip("Sorting order applied to the aura SpriteRenderer. Negative values keep " +
                 "the aura behind the player sprite.")]
        public int sortingOrder = -10;

        /// <summary>Computes the scale to apply at a given weapon level (1-based).</summary>
        public float GetScaleForLevel(int level)
        {
            int l = Mathf.Max(1, level);
            return Mathf.Max(0.01f, baseScale + (l - 1) * scalePerLevel);
        }

        /// <summary>Computes the intensity multiplier at a given weapon level (1-based).</summary>
        public float GetIntensityForLevel(int level)
        {
            int l = Mathf.Max(1, level);
            return Mathf.Max(0f, baseIntensity + (l - 1) * intensityPerLevel);
        }
    }
}
