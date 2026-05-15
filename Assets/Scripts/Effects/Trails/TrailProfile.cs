using UnityEngine;

namespace LoneFighter.Effects.Trails
{
    /// <summary>
    /// Authoring-time visual recipe for a projectile / weapon trail.
    /// One asset per weapon archetype (Pistol, SpreadShotgun, OrbitalBlade,
    /// ChainLightning, ...). Consumed by <see cref="BulletTrailController"/>
    /// and by any other component that wants the same look (e.g. the
    /// <see cref="LightningArcRenderer"/> reuses the gradient + width curve).
    ///
    /// The fields here intentionally mirror what a Unity <c>TrailRenderer</c>
    /// already exposes, so applying a profile is a straight copy with no
    /// extra math at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "TrailProfile", menuName = "LoneFighter/FX/Trail Profile", order = 220)]
    public sealed class TrailProfile : ScriptableObject
    {
        [Header("Color")]
        [Tooltip("Color gradient sampled across the trail length (head -> tail). " +
                 "Use HDR colors for bloom-friendly highlights.")]
        public Gradient colorGradient = DefaultGradient();

        [Header("Shape")]
        [Tooltip("Width multiplier sampled across the trail length (head -> tail). " +
                 "Combined with widthScale to get the final per-vertex width.")]
        public AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("Overall width in world units. Multiplied by widthCurve when applied " +
                 "to the underlying TrailRenderer.")]
        [Min(0f)]
        public float widthScale = 0.2f;

        [Header("Lifetime")]
        [Tooltip("How long each segment of the trail persists in seconds.")]
        [Min(0f)]
        public float fadeTime = 0.25f;

        [Tooltip("Minimum world-space distance the emitter must move before a new " +
                 "vertex is emitted. 0 = emit every frame.")]
        [Min(0f)]
        public float minVertexDistance = 0.05f;

        [Header("Rendering")]
        [Tooltip("When true, the trail is rendered with an additive blend material. " +
                 "When false, the default alpha-blended material is used.")]
        public bool additive = true;

        [Tooltip("Sorting order on the assigned sorting layer. Higher = renders on top.")]
        public int sortingOrder = 5;

        [Tooltip("Optional sorting-layer name. Leave blank to use the renderer's default.")]
        public string sortingLayerName = "";

        private static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            return g;
        }
    }
}
