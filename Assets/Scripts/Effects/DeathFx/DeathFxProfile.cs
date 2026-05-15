using UnityEngine;

namespace LoneFighter.Effects.DeathFx
{
    /// Strength tier for the camera shake kick a profile requests on enemy death.
    /// Light maps to FxService.LightShake() and Heavy maps to FxService.HeavyShake().
    /// `None` means: don't shake the camera for this enemy at all.
    public enum DeathFxShake
    {
        None,
        Light,
        Heavy,
    }

    /// Per-archetype death effect recipe. Authored as a ScriptableObject so designers can
    /// tweak colors / counts / sounds in the inspector without recompiling.
    ///
    /// The dispatcher resolves a profile by matching `EnemyData.displayName` against the
    /// `DeathFxRegistry`, then plays a particle burst, a gib spray, a screen shake, a
    /// hit-stop, and optionally a one-shot sound cue.
    ///
    /// Every field is optional: a profile with no particle prefab simply skips the burst,
    /// a gib count of 0 skips gibs, etc. This makes profiles incremental — author a Grunt
    /// with just a tint, layer in particles + sound later.
    [CreateAssetMenu(
        menuName = "LoneFighter/Death FX Profile",
        fileName = "DeathFxProfile")]
    public class DeathFxProfile : ScriptableObject
    {
        // ----- Identity -------------------------------------------------------------------
        [Header("Identity")]
        [Tooltip("Friendly label used only in the inspector / debug logs. The lookup key " +
                 "is set on DeathFxRegistry, not here.")]
        public string label = "Death FX";

        // ----- Particle burst -------------------------------------------------------------
        [Header("Particle burst")]
        [Tooltip("Optional ParticleSystem prefab pooled & played on death. Leave null to " +
                 "fall back to FxService.EnemyExplosion (the default explosion).")]
        public GameObject particlePrefab;

        [Tooltip("Tint applied to the particle prefab's main module startColor at play time. " +
                 "Leave alpha 0 to keep the prefab's authored color.")]
        [ColorUsage(showAlpha: true, hdr: true)]
        public Color particleTint = new Color(1f, 1f, 1f, 0f);

        [Tooltip("Multiplier applied to particle startSize. 1 = use the prefab's authored size.")]
        [Range(0.25f, 4f)] public float particleSizeMultiplier = 1f;

        [Tooltip("If > 0, overrides the prefab's first emission Burst.count with this value. " +
                 "Use 0 to keep the prefab's authored count.")]
        [Range(0, 256)] public int particleBurstCount = 0;

        // ----- Sound ----------------------------------------------------------------------
        [Header("Sound")]
        [Tooltip("AudioCue name played on death. Parsed against the LoneFighter.Systems.AudioCue " +
                 "enum at runtime — invalid / empty values fall back to AudioCue.EnemyDeath. " +
                 "Examples: \"EnemyDeath\", \"BossSlam\".")]
        public string soundCueName = "EnemyDeath";

        [Tooltip("Volume scale (0..1) passed to AudioManager.Play. 1 = use the cue's authored volume.")]
        [Range(0f, 1f)] public float soundVolumeScale = 1f;

        // ----- Camera shake / hit-stop ----------------------------------------------------
        [Header("Screen feel")]
        [Tooltip("Which FxService shake kick to fire. None disables shake for this archetype.")]
        public DeathFxShake shake = DeathFxShake.Light;

        [Tooltip("Seconds of hit-stop. 0 = no hit-stop. Heavy archetypes feel best in the " +
                 "0.04..0.08 range; bosses up to 0.15.")]
        [Range(0f, 0.25f)] public float hitStopSeconds = 0f;

        // ----- Gibs -----------------------------------------------------------------------
        [Header("Gibs")]
        [Tooltip("Number of small sprite chunks spawned outward on death. 0 disables gibs.")]
        [Range(0, 32)] public int gibCount = 6;

        [Tooltip("Tint applied to spawned gib chunks.")]
        [ColorUsage(showAlpha: true, hdr: false)]
        public Color gibColor = new Color(1f, 0.25f, 0.2f, 1f);

        [Tooltip("Random gib chunk size range. Each gib picks a uniform value in [min..max].")]
        public Vector2 gibSizeRange = new Vector2(0.10f, 0.18f);

        [Tooltip("Random gib launch speed range. Higher = chunks fly further before friction stops them.")]
        public Vector2 gibSpeedRange = new Vector2(2.5f, 5.5f);

        [Tooltip("Seconds a gib stays alive before fading out and returning to the pool.")]
        [Range(0.1f, 2f)] public float gibLifetime = 0.8f;

        // ----- Splat decal ----------------------------------------------------------------
        [Header("Splat decal")]
        [Tooltip("Distinct color of the optional ground splat decal left behind. Alpha 0 " +
                 "disables the decal entirely. The decal renderer is provided by the splat " +
                 "system (a separate concern) — DeathFxProfile only ships the color.")]
        [ColorUsage(showAlpha: true, hdr: false)]
        public Color splatDecalColor = new Color(0.4f, 0.05f, 0.08f, 0.85f);
    }
}
