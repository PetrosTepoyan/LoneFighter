// LightningStrikes.cs
// High-intensity dramatic punctuation: occasional screen-edge flash + crack of light.
// Triggers only when ambient intensity exceeds an activation threshold (default 0.7).

using UnityEngine;

namespace LoneFighter.Effects.Ambient
{
    /// <summary>
    /// Bursts a one-shot white flash at the screen edge every 4-12 seconds when intensity
    /// is above <see cref="activationThreshold"/>. Uses an additive ParticleSystem burst
    /// for the "crack of light" and a brief full-screen white fade for the flash.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class LightningStrikes : AmbientFxModule
    {
        [Header("Camera Anchor")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Strike spawn distance from camera center, in camera-space units.")]
        [SerializeField] private float strikeRadius = 7f;

        [Tooltip("How far in front of the camera the strike sits, in camera-space Z.")]
        [SerializeField] private float strikeDepth = 5f;

        [Header("Activation")]
        [Tooltip("Intensity at and above which strikes can fire.")]
        [Range(0f, 1f)]
        [SerializeField] private float activationThreshold = 0.7f;

        [Header("Interval (seconds)")]
        [SerializeField] private float intervalMin = 4f;
        [SerializeField] private float intervalMax = 12f;

        [Header("Flash")]
        [Tooltip("Optional full-screen flash overlay. If null, only particle crack fires.")]
        [SerializeField] private SpriteRenderer flashOverlay;
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private float flashAttack = 0.04f;
        [SerializeField] private float flashDecay = 0.22f;

        [Header("Crack Particles")]
        [SerializeField] private int crackBurstCount = 24;
        [SerializeField] private float crackSpeed = 8f;
        [SerializeField] private float crackLifetime = 0.35f;
        [SerializeField] private float crackSize = 0.35f;

        private ParticleSystem ps;
        private float nextStrikeAt = -1f;
        private float flashLevel;
        private float flashPeak;
        private bool flashRising;
        private float flashTimer;

        protected override void OnEnable()
        {
            base.OnEnable();
            ps = GetComponent<ParticleSystem>();
            ConfigureParticleSystem();
            nextStrikeAt = -1f;
        }

        private void ConfigureParticleSystem()
        {
            if (ps == null) return;

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = crackLifetime;
            main.startSpeed = crackSpeed;
            main.startSize = crackSize;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 0) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.85f, 0.92f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.6f, 1f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3.5f;
                renderer.velocityScale = 0.15f;
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

            // Schedule first strike once we cross the threshold.
            if (intensity >= activationThreshold)
            {
                if (nextStrikeAt < 0f)
                {
                    nextStrikeAt = elapsedSeconds + Random.Range(intervalMin, intervalMax);
                }
                else if (elapsedSeconds >= nextStrikeAt)
                {
                    Strike(intensity);
                    nextStrikeAt = elapsedSeconds + Random.Range(intervalMin, intervalMax);
                }
            }
            else
            {
                // Hold first strike until intensity actually rises.
                nextStrikeAt = -1f;
            }

            // Animate flash fade independently of strike scheduling.
            UpdateFlash(deltaTime);
        }

        private void Strike(float intensity)
        {
            // Pick a random angle around the camera, place the burst near the screen edge.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 camSpacePos = dir * strikeRadius;
            camSpacePos.z = strikeDepth;

            Vector3 worldPos = targetCamera != null
                ? targetCamera.transform.TransformPoint(camSpacePos)
                : transform.position + dir * strikeRadius;

            transform.position = worldPos;
            // Orient stretch toward camera center for a streak-from-edge feel.
            transform.rotation = Quaternion.LookRotation(Vector3.forward, -dir);

            if (ps != null)
            {
                ps.Emit(crackBurstCount);
            }

            // Trigger flash envelope. Stronger flash at higher intensity.
            flashPeak = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(activationThreshold, 1f, intensity));
            flashRising = true;
            flashTimer = 0f;
        }

        private void UpdateFlash(float dt)
        {
            if (flashOverlay == null) return;

            if (flashRising)
            {
                flashTimer += dt;
                flashLevel = Mathf.Clamp01(flashTimer / Mathf.Max(0.001f, flashAttack)) * flashPeak;
                if (flashTimer >= flashAttack)
                {
                    flashRising = false;
                    flashTimer = 0f;
                }
            }
            else if (flashLevel > 0f)
            {
                flashTimer += dt;
                float k = 1f - Mathf.Clamp01(flashTimer / Mathf.Max(0.001f, flashDecay));
                flashLevel = flashPeak * k;
                if (flashTimer >= flashDecay)
                {
                    flashLevel = 0f;
                }
            }

            Color c = flashColor;
            c.a = flashColor.a * flashLevel;
            flashOverlay.color = c;
        }
    }
}
