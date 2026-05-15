// LoneFighter - Ambient drifting particles for the main menu.
// Configures a ParticleSystem (auto-created if needed) to emit slow dust motes
// with occasional brighter ember bursts in world space.

using UnityEngine;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Drives a `ParticleSystem` (and an optional secondary ember sub-emitter)
    /// to provide a subtle drifting-particles atmosphere behind the main menu.
    /// The script will configure a ParticleSystem on the same GameObject in
    /// world simulation space; values can be overridden via the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public class MenuParticleField : MonoBehaviour
    {
        [Header("Dust Motes")]
        [SerializeField] private float dustRateOverTime = 8f;
        [SerializeField] private float dustLifetime = 6f;
        [SerializeField] private float dustStartSpeedMin = 0.05f;
        [SerializeField] private float dustStartSpeedMax = 0.25f;
        [SerializeField] private float dustStartSizeMin = 0.02f;
        [SerializeField] private float dustStartSizeMax = 0.08f;
        [SerializeField] private Color dustColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Embers (optional)")]
        [Tooltip("Optional secondary ParticleSystem used for occasional brighter embers.")]
        [SerializeField] private ParticleSystem embers;
        [SerializeField] private float emberRateOverTime = 0.6f;
        [SerializeField] private float emberLifetime = 4f;
        [SerializeField] private float emberStartSize = 0.12f;
        [SerializeField] private Color emberColor = new Color(1f, 0.7f, 0.3f, 0.9f);

        [Header("Emission Area (world units)")]
        [SerializeField] private Vector3 emissionBoxSize = new Vector3(12f, 18f, 0.1f);

        private ParticleSystem dust;
        private bool configured;

        private void Awake()
        {
            dust = GetComponent<ParticleSystem>();
            Configure();
        }

        private void OnEnable()
        {
            if (!configured)
            {
                Configure();
            }
            if (dust != null && !dust.isPlaying)
            {
                dust.Play(true);
            }
            if (embers != null && !embers.isPlaying)
            {
                embers.Play(true);
            }
        }

        private void OnDisable()
        {
            if (dust != null)
            {
                dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (embers != null)
            {
                embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Configure()
        {
            if (dust == null)
            {
                dust = GetComponent<ParticleSystem>();
            }
            if (dust != null)
            {
                ConfigureDust(dust);
            }
            if (embers != null)
            {
                ConfigureEmbers(embers);
            }
            configured = true;
        }

        private void ConfigureDust(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = dustLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(dustStartSpeedMin, dustStartSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(dustStartSizeMin, dustStartSizeMax);
            main.startColor = dustColor;
            main.maxParticles = 200;
            main.gravityModifier = 0f;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = dustRateOverTime;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emissionBoxSize;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(dustColor, 0f),
                    new GradientColorKey(dustColor, 1f),
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(dustColor.a, 0.25f),
                    new GradientAlphaKey(dustColor.a, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLife.color = grad;
        }

        private void ConfigureEmbers(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = emberLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = emberStartSize;
            main.startColor = emberColor;
            main.maxParticles = 60;
            main.gravityModifier = -0.05f; // gentle rise
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = emberRateOverTime;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emissionBoxSize;
        }
    }
}
