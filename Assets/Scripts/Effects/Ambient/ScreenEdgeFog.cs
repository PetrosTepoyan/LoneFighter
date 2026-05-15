// ScreenEdgeFog.cs
// Soft dark particles hugging the screen edges. Density increases with intensity to
// create a claustrophobic, closing-in feel during the late run.

using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Spawns large soft dark particles around the screen perimeter. As intensity climbs,
    /// the perimeter fills out and feels like the world is closing in.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ScreenEdgeFog : AmbientFxModule
    {
        [Header("Camera Anchor")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Outer half-extent of the fog ring (camera-space units).")]
        [SerializeField] private Vector2 outerHalfSize = new Vector2(9f, 13f);

        [Tooltip("Inner half-extent (no fog inside this rectangle).")]
        [SerializeField] private Vector2 innerHalfSize = new Vector2(6f, 9f);

        [Tooltip("Depth offset from camera.")]
        [SerializeField] private float depth = 5f;

        [Header("Density (rate per second)")]
        [SerializeField] private float emissionMin = 2f;
        [SerializeField] private float emissionMax = 40f;
        [Tooltip("Exponent applied to intensity before lerping density.")]
        [SerializeField] private float densityExponent = 1.4f;

        [Header("Look")]
        [SerializeField] private Color fogColor = new Color(0.04f, 0.04f, 0.07f, 0.55f);
        [SerializeField] private float minSize = 1.5f;
        [SerializeField] private float maxSize = 3.5f;
        [SerializeField] private float minDrift = 0.05f;
        [SerializeField] private float maxDrift = 0.20f;
        [SerializeField] private float lifetime = 5f;

        [Header("Intensity Tint")]
        [Tooltip("Alpha at minimum intensity (0..1).")]
        [SerializeField] private float minAlphaScale = 0.4f;
        [Tooltip("Alpha at maximum intensity (0..1).")]
        [SerializeField] private float maxAlphaScale = 1.2f;

        private ParticleSystem ps;
        private float perimeterCircumference;
        private float spawnAccumulator;

        protected override void OnEnable()
        {
            base.OnEnable();
            ps = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
            // Approximate ring length so we distribute manually-emitted positions evenly.
            perimeterCircumference = 2f * (outerHalfSize.x + outerHalfSize.y) * 2f;
        }

        private void ConfigureParticleSystem()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = false; // we drive emission manually with Emit() for ring placement
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f; // velocity handled via velocityOverLifetime
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = fogColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = false; // positions are supplied explicitly via EmitParams

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-maxDrift, maxDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(-maxDrift, maxDrift);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(fogColor, 0f), new GradientColorKey(fogColor, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(fogColor.a, 0.3f),
                    new GradientAlphaKey(fogColor.a, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.8f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (renderer.sharedMaterial == null)
                {
                    // Fog uses alpha blend (not additive) to actually darken the edges.
                    var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f); // fade
                        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        renderer.material = mat;
                    }
                }
            }
        }

        public override void Tick(float intensity, float elapsedSeconds, float deltaTime)
        {
            if (ps == null) return;

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            Vector3 anchor = targetCamera != null
                ? targetCamera.transform.position + targetCamera.transform.forward * depth
                : transform.position;

            transform.position = anchor;

            // Update color/alpha tint with intensity.
            float alphaScale = Mathf.Lerp(minAlphaScale, maxAlphaScale, intensity);
            var main = ps.main;
            Color c = fogColor;
            c.a = Mathf.Clamp01(fogColor.a * alphaScale);
            main.startColor = c;

            // Manually accumulate emission so we can place particles on the ring.
            float shaped = Mathf.Pow(Mathf.Clamp01(intensity), densityExponent);
            float rate = Mathf.Lerp(emissionMin, emissionMax, shaped);
            spawnAccumulator += rate * deltaTime;
            int toSpawn = Mathf.FloorToInt(spawnAccumulator);
            if (toSpawn > 0)
            {
                spawnAccumulator -= toSpawn;
                EmitRing(toSpawn, anchor);
            }
        }

        private void EmitRing(int count, Vector3 anchor)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = RandomRingPoint();
                var emit = new ParticleSystem.EmitParams
                {
                    position = anchor + new Vector3(pos.x, pos.y, 0f),
                    applyShapeToPosition = false
                };
                ps.Emit(emit, 1);
            }
        }

        private Vector2 RandomRingPoint()
        {
            // Sample a point in the outer rectangle, reject if inside inner rectangle.
            // 8 tries max, fall through to outer edge if all rejected.
            for (int i = 0; i < 8; i++)
            {
                float x = Random.Range(-outerHalfSize.x, outerHalfSize.x);
                float y = Random.Range(-outerHalfSize.y, outerHalfSize.y);
                if (Mathf.Abs(x) > innerHalfSize.x || Mathf.Abs(y) > innerHalfSize.y)
                {
                    return new Vector2(x, y);
                }
            }
            // Fallback: snap to a random outer edge.
            float side = Random.value;
            if (side < 0.25f) return new Vector2(-outerHalfSize.x, Random.Range(-outerHalfSize.y, outerHalfSize.y));
            if (side < 0.5f)  return new Vector2( outerHalfSize.x, Random.Range(-outerHalfSize.y, outerHalfSize.y));
            if (side < 0.75f) return new Vector2(Random.Range(-outerHalfSize.x, outerHalfSize.x), -outerHalfSize.y);
            return new Vector2(Random.Range(-outerHalfSize.x, outerHalfSize.x),  outerHalfSize.y);
        }
    }
}
