using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Static helper: spawns a small projectile-impact decal (bullet hole) on the floor.
    /// Smaller and shorter-lived than blood — fires get pummeled with these and we don't
    /// want the visual signal to overwhelm gameplay.
    /// </summary>
    public static class BulletHole
    {
        public const float DefaultFadeSeconds = 6f;
        // Near-black with a touch of cool grey — reads as a chip in the floor.
        public static readonly Color DefaultTint = new Color(0.08f, 0.08f, 0.10f, 1f);
        public const float MinScale = 0.6f;
        public const float MaxScale = 0.9f;
        public const float DefaultMaxAlpha = 0.85f;

        public static void Spawn(Vector3 position)
            => Spawn(position, DefaultTint, DefaultFadeSeconds);

        public static void Spawn(Vector3 position, Color tint, float fadeSeconds)
        {
            var lib = DecalSpriteLibrary.Instance;
            if (lib == null) return;
            var sprite = DecalSpriteLibrary.Pick(lib.BulletHoles);
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
