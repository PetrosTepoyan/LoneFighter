using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Static helper: spawns a random blood-splatter decal at a position.
    /// Uses a random sprite from <see cref="DecalSpriteLibrary"/>, a random rotation,
    /// a slight scale jitter, a red tint, and a ~10 s linear fade.
    ///
    /// All values are deliberately conservative for mobile — the per-decal cost is
    /// just one SpriteRenderer color tween in <see cref="DecalManager"/>.
    /// </summary>
    public static class BloodSplatter
    {
        // ---- Defaults ----
        public const float DefaultFadeSeconds = 10f;
        // Mid-saturation crimson; survives bloom without going pink, reads clearly on a dark floor.
        public static readonly Color DefaultTint = new Color(0.65f, 0.05f, 0.08f, 1f);
        public const float MinScale = 0.85f;
        public const float MaxScale = 1.25f;
        public const float DefaultMaxAlpha = 0.95f;

        /// <summary>Spawn a default-styled blood splatter at <paramref name="position"/>.</summary>
        public static void Spawn(Vector3 position)
            => Spawn(position, DefaultTint, DefaultFadeSeconds);

        /// <summary>Override tint and/or fade duration.</summary>
        public static void Spawn(Vector3 position, Color tint, float fadeSeconds)
        {
            var lib = DecalSpriteLibrary.Instance;
            if (lib == null) return; // no sprites assigned → silent no-op (game still runs)
            var sprite = DecalSpriteLibrary.Pick(lib.BloodSplatters);
            if (sprite == null) return;

            var mgr = DecalManager.GetOrCreate();
            float rot = Random.Range(0f, 360f);
            float scale = Random.Range(MinScale, MaxScale);
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
