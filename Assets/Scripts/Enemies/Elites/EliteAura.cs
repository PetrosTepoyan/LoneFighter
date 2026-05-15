using UnityEngine;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// Golden swirling particle aura attached as a child of an Elite enemy.
    ///
    /// Two operating modes:
    ///  * If you assign a pre-authored <see cref="ParticleSystem"/> via the inspector
    ///    (the "preferred" path — drop in a real VFX prefab), <see cref="EliteAura"/>
    ///    simply plays it on enable and stops on disable.
    ///  * If no ParticleSystem is found on Awake, the component builds a minimal default
    ///    ParticleSystem at runtime so the elite is visually marked even in a bare scene.
    ///    The default is intentionally cheap (low rate, soft additive) so a few elites
    ///    on screen don't burn the mobile frame budget.
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteAura : MonoBehaviour
    {
        [SerializeField] private ParticleSystem authoredSystem;

        [Header("Default Aura (used only if no authored ParticleSystem is set)")]
        [SerializeField] private float defaultRadius = 0.55f;
        [SerializeField] private float defaultRate = 18f;
        [SerializeField] private float defaultLifetime = 0.9f;
        [SerializeField] private Color defaultColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private float defaultSwirlSpeed = 90f;
        [SerializeField] private float defaultParticleSize = 0.18f;

        private ParticleSystem _system;

        private void Awake()
        {
            if (authoredSystem != null)
            {
                _system = authoredSystem;
                return;
            }

            _system = GetComponent<ParticleSystem>();
            if (_system == null) _system = BuildDefaultSystem();
        }

        private void OnEnable()
        {
            if (_system != null && !_system.isPlaying) _system.Play(true);
        }

        private void OnDisable()
        {
            if (_system != null) _system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private ParticleSystem BuildDefaultSystem()
        {
            var ps = gameObject.AddComponent<ParticleSystem>();

            // Important: configure modules *before* enabling them where Unity insists
            // on it; for a runtime-created PS, mutating the structs and writing them
            // back is sufficient.
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = defaultLifetime;
            main.startSpeed = 0.4f;
            main.startSize = defaultParticleSize;
            main.startColor = defaultColor;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 64;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(defaultRate);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = defaultRadius;
            shape.donutRadius = 0.05f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Orbital swirl gives the "swirling aura" look without a custom shader.
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(defaultSwirlSpeed * Mathf.Deg2Rad);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(defaultColor, 0f),
                    new GradientColorKey(defaultColor, 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.7f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0.4f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer — additive sprite if Sprites/Default is available, otherwise
            // Unity supplies a default material so it still renders.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 10;
            }

            return ps;
        }
    }
}
