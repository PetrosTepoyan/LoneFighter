// DustMotes.cs
// Slow drifting dust around the player camera. Subtle, low count.
// Uses Perlin-noise wander on the emission box position to make the field feel alive.

using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Subtle floating dust motes around the camera. Adds atmosphere to the empty arena.
    /// Stays low-count and low-alpha so it never competes with gameplay reads.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class DustMotes : AmbientFxModule
    {
        [Header("Camera Anchor")]
        [Tooltip("Camera to follow. Defaults to Camera.main at runtime.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Offset (in camera space units) from camera origin where motes spawn.")]
        [SerializeField] private Vector3 cameraSpaceOffset = new Vector3(0f, 0f, 5f);

        [Header("Field")]
        [Tooltip("Emission box (world units) around the camera in which motes drift.")]
        [SerializeField] private Vector3 fieldSize = new Vector3(12f, 18f, 0.5f);

        [Header("Counts")]
        [Tooltip("Rate per second at minimum intensity.")]
        [SerializeField] private float emissionMin = 4f;
        [Tooltip("Rate per second at maximum intensity.")]
        [SerializeField] private float emissionMax = 7f;

        [Header("Look")]
        [SerializeField] private Color color = new Color(0.85f, 0.82f, 0.72f, 0.35f);
        [SerializeField] private float minSize = 0.03f;
        [SerializeField] private float maxSize = 0.10f;
        [SerializeField] private float minSpeed = 0.05f;
        [SerializeField] private float maxSpeed = 0.20f;
        [SerializeField] private float lifetime = 6f;

        [Header("Wander")]
        [Tooltip("World-space jitter range applied via Perlin noise to the emitter position.")]
        [SerializeField] private float wanderRadius = 1.2f;
        [SerializeField] private float wanderSpeed = 0.15f;

        private ParticleSystem ps;
        private ParticleSystem.EmissionModule emission;
        private float noiseSeedX;
        private float noiseSeedY;
        private Vector3 anchorVelocity;

        protected override void OnEnable()
        {
            base.OnEnable();
            ps = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
            noiseSeedX = Random.value * 100f;
            noiseSeedY = Random.value * 100f;
        }

        private void ConfigureParticleSystem()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.gravityModifier = 0f;

            emission = ps.emission;
            emission.rateOverTime = emissionMin;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = fieldSize;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.12f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.25f), new GradientAlphaKey(color.a, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                if (renderer.sharedMaterial == null)
                {
                    var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        renderer.material = new Material(shader);
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

            // Smoothly follow camera with Perlin-wander on top to give the field life.
            Vector3 anchor;
            if (targetCamera != null)
            {
                anchor = targetCamera.transform.TransformPoint(cameraSpaceOffset);
            }
            else
            {
                anchor = transform.position;
            }

            float t = elapsedSeconds * wanderSpeed;
            float nx = (Mathf.PerlinNoise(noiseSeedX, t) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(t, noiseSeedY) - 0.5f) * 2f;
            anchor += new Vector3(nx * wanderRadius, ny * wanderRadius, 0f);

            transform.position = Vector3.SmoothDamp(transform.position, anchor, ref anchorVelocity, 0.5f);

            var em = ps.emission;
            em.rateOverTime = Mathf.Lerp(emissionMin, emissionMax, intensity);
        }
    }
}
