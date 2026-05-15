using UnityEngine;

namespace LoneFighter.Effects.Decals
{
    /// <summary>
    /// Lightweight, value-type description of a single floor decal spawn.
    /// Callers fill in only the fields that matter; defaults are picked so that
    /// the resulting decal is at least visible (alpha = 1, no fade) if the caller
    /// forgets to override them.
    ///
    /// Decals are floor stains — blood splatters, scorch marks, bullet holes —
    /// that linger after combat events. They sort BELOW enemies and are pooled
    /// behind a hard cap to keep the mobile budget bounded.
    /// </summary>
    public struct DecalRequest
    {
        /// <summary>World-space position. Z is preserved by the manager (it offsets within its parent layer).</summary>
        public Vector3 position;

        /// <summary>World-space rotation. Useful for randomizing splatter / scorch orientation.</summary>
        public Quaternion rotation;

        /// <summary>Sprite to display. If null the request is dropped.</summary>
        public Sprite sprite;

        /// <summary>Multiplicative tint. RGB is applied; the alpha here is overridden by <see cref="maxAlpha"/>.</summary>
        public Color tint;

        /// <summary>Seconds the decal lingers before being fully transparent. Linear fade from <see cref="maxAlpha"/> → 0.</summary>
        public float fadeSeconds;

        /// <summary>Starting alpha. The decal fades from this value down to 0 over <see cref="fadeSeconds"/>.</summary>
        public float maxAlpha;

        /// <summary>Optional uniform scale multiplier (1 = sprite's native size). Values &lt;= 0 are treated as 1.</summary>
        public float scale;

        /// <summary>
        /// Convenience constructor — most callers only care about position / sprite / tint / lifetime.
        /// Rotation defaults to identity; scale defaults to 1; maxAlpha defaults to 1.
        /// </summary>
        public DecalRequest(Vector3 position, Sprite sprite, Color tint, float fadeSeconds)
        {
            this.position = position;
            this.rotation = Quaternion.identity;
            this.sprite = sprite;
            this.tint = tint;
            this.fadeSeconds = fadeSeconds;
            this.maxAlpha = 1f;
            this.scale = 1f;
        }
    }
}
