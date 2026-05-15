// WindGusts.cs
// Directional particle streaks that sweep across the arena every 8-15 seconds.
// Direction rotates slowly over the run, reinforcing the feeling of movement / pressure.

using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Bursts long horizontal streaks across the screen at randomized intervals. The
    /// wind direction rotates slowly so each gust comes from a slightly different angle.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class WindGusts : AmbientFxModule
    {
        [Header("Camera Anchor")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Distance from camera at which the gust line is spawned.")]
        [SerializeField] private float spawnDistance = 9f;

        [Tooltip("Line length perpendicular to the wind direction (covers screen width).")]
        [SerializeField] private float lineLength = 16f;

        [Header("Interval (seconds)")]
        [SerializeField] private float intervalMin = 8f;
        [SerializeField] private float intervalMax = 15f;

        [Header("Rotation")]
        [Tooltip("Degrees per second the base wind angle rotates.")]
        [SerializeField] private float rotationSpeedDegPerSec = 6f;

        [Tooltip("Random jitter in degrees added to each gust on top of the base angle.")]
        [SerializeField] private float angleJitterDeg = 20f;

        [Header("Gust Look")]
        [SerializeField] private int burstCountMin = 14;
        [SerializeField] private int burstCountMax = 28;
        [SerializeField] private float streakSpeed = 9f;
        [SerializeField] private float streakLifetime = 1.2f;
        [SerializeField] private float streakSize = 0.08f;
        [SerializeField] private Color streakColor = new Color(0.85f, 0.90f, 1f, 0.55f);

        [Header("Density Scaling")]
        [Tooltip("Burst count multiplier at minimum intensity.")]
        [SerializeField] private float countScaleMin = 0.6f;
        [Tooltip("Burst count multiplier at maximum intensity.")]
        [SerializeField] private float countScaleMax = 1.4f;

        private ParticleSystem ps;
        private float nextGustAt;
        private float baseAngleDeg;
        private bool initialized;

        protected override void OnEnable()
        {
            base.OnEnable();
            ps = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
            baseAngleDeg = Random.Range(0f, 360f);
            initialized = false;
        }

        private void ConfigureParticleSystem()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = streakLifetime;
            main.startSpeed = streakSpeed;
            main.startSize = streakSize;
            main.startColor = streakColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 512;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Edge;
            shape.radius = lineLength * 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(streakColor, 0f), new GradientColorKey(streakColor, 1f) },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(streakColor.a, 0.2f),
                    new GradientAlphaKey(streakColor.a, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 5f;
                renderer.velocityScale = 0.4f;
                if (renderer.sharedMaterial == null)
                {
                    var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 4f);
                        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                        renderer.material = mat;
                    }
                }
            }
        }

        public override void Tick(float intensity, float elapsedSeconds, float deltaTime)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            baseAngleDeg += rotationSpeedDegPerSec * deltaTime;

            if (!initialized)
            {
                nextGustAt = elapsedSeconds + Random.Range(intervalMin, intervalMax);
                initialized = true;
                return;
            }

            if (elapsedSeconds >= nextGustAt)
            {
                FireGust(intensity);
                nextGustAt = elapsedSeconds + Random.Range(intervalMin, intervalMax);
            }
        }

        private void FireGust(float intensity)
        {
            if (ps == null) return;

            float angleDeg = baseAngleDeg + Random.Range(-angleJitterDeg, angleJitterDeg);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 windDir = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f);

            // Spawn line on the upwind side of the camera.
            Vector3 anchor = targetCamera != null ? targetCamera.transform.position : transform.position;
            Vector3 spawnCenter = anchor - windDir * spawnDistance;

            transform.position = spawnCenter;
            // Orient the line so the Edge shape is perpendicular to wind direction; align
            // the local +X axis along wind so stretch streaks render along it.
            transform.rotation = Quaternion.AngleAxis(angleDeg + 90f, Vector3.forward);

            var main = ps.main;
            main.startSpeed = streakSpeed;

            // Force emitter to launch in the wind direction by setting start rotation
            // and using velocityOverLifetime aligned to wind.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = windDir.x * streakSpeed;
            velocity.y = windDir.y * streakSpeed;

            float scale = Mathf.Lerp(countScaleMin, countScaleMax, intensity);
            int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(burstCountMin, burstCountMax) * scale));
            ps.Emit(count);
        }
    }
}
