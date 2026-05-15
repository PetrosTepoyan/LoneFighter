// EmbersAndSparks.cs
// Rising orange/yellow embers across the screen. Density scales with run intensity:
// barely-there at the start, swarming the screen by minute 5.

using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Drifting embers that rise from the bottom of the screen. Additive-blended for glow.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class EmbersAndSparks : AmbientFxModule
    {
        [Header("Camera Anchor")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Emission strip is anchored at this offset from the camera (camera space).")]
        [SerializeField] private Vector3 cameraSpaceOffset = new Vector3(0f, -6f, 5f);

        [Header("Field")]
        [Tooltip("Width and height of the spawn strip in world units.")]
        [SerializeField] private Vector2 stripSize = new Vector2(14f, 0.5f);

        [Header("Density (rate per second)")]
        [SerializeField] private float emissionMin = 1f;
        [SerializeField] private float emissionMax = 35f;
        [Tooltip("Exponent applied to intensity before lerping density. >1 keeps early game calm.")]
        [SerializeField] private float densityExponent = 1.6f;

        [Header("Look")]
        [SerializeField] private Color emberHot = new Color(1.0f, 0.55f, 0.10f, 1f);
        [SerializeField] private Color emberCool = new Color(1.0f, 0.85f, 0.30f, 1f);
        [SerializeField] private float minSize = 0.06f;
        [SerializeField] private float maxSize = 0.14f;
        [SerializeField] private float minRiseSpeed = 0.4f;
        [SerializeField] private float maxRiseSpeed = 1.2f;
        [SerializeField] private float lifetime = 3.5f;

        private ParticleSystem ps;

        protected override void OnEnable()
        {
            base.OnEnable();
            ps = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
        }

        private void ConfigureParticleSystem()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(minRiseSpeed, maxRiseSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(emberHot, emberCool);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;
            main.gravityModifier = -0.05f; // negative -> rises

            var emission = ps.emission;
            emission.rateOverTime = emissionMin;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(stripSize.x, stripSize.y, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.40f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.8f, 0.8f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(emberHot, 0f),
                    new GradientColorKey(emberCool, 0.6f),
                    new GradientColorKey(new Color(0.6f, 0.15f, 0f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (renderer.sharedMaterial == null)
                {
                    var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        // Try to flag additive blend if the shader supports it.
                        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 4f); // additive in Standard Particle Unlit
                        if (mat.HasProperty("_BlendOp")) mat.SetFloat("_BlendOp", 0f);
                        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
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

            if (targetCamera != null)
            {
                transform.position = targetCamera.transform.TransformPoint(cameraSpaceOffset);
            }

            float shaped = Mathf.Pow(Mathf.Clamp01(intensity), densityExponent);
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Lerp(emissionMin, emissionMax, shaped);
        }
    }
}
