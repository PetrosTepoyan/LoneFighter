using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Static helper: spawns an AOE/explosion scorch mark (dark sooty ring) on the floor.
    /// Used by Bomber detonations and any other AOE event.
    ///
    /// Larger scale jitter than blood (explosions vary in size) and a longer fade (12 s).
    /// </summary>
    public static class ScorchMark
    {
        public const float DefaultFadeSeconds = 12f;
        // Dark grey with a hint of warm brown — survives bloom as a real shadow rather than a hole.
        public static readonly Color DefaultTint = new Color(0.15f, 0.12f, 0.10f, 1f);
        public const float MinScale = 1.0f;
        public const float MaxScale = 1.6f;
        public const float DefaultMaxAlpha = 0.85f;

        /// <summary>Spawn a scorch mark sized to the default explosion radius.</summary>
        public static void Spawn(Vector3 position)
            => Spawn(position, 1f);

        /// <summary>
        /// Spawn a scorch mark scaled by <paramref name="radiusScale"/> — multiply by an
        /// explosion's effective radius so big bombs leave big scars.
        /// </summary>
        public static void Spawn(Vector3 position, float radiusScale)
            => Spawn(position, radiusScale, DefaultTint, DefaultFadeSeconds);

        public static void Spawn(Vector3 position, float radiusScale, Color tint, float fadeSeconds)
        {
            var lib = DecalSpriteLibrary.Instance;
            if (lib == null) return;
            var sprite = DecalSpriteLibrary.Pick(lib.ScorchMarks);
            if (sprite == null) return;

            var mgr = DecalManager.GetOrCreate();
            float rot = Random.Range(0f, 360f);
            float scale = Mathf.Max(0.1f, radiusScale) * Random.Range(MinScale, MaxScale);
            mgr.Spawn(new DecalRequest
            {
                position    = position,
                rotation    = Quaternion.Euler(0f, 0f, rot),
                sprite      = sprite,
                tint        = tint,
                fadeSeconds = fadeSeconds,
                maxAlpha    = DefaultMaxAlpha,
                scale       = scale,
            });
        }
    }
}
