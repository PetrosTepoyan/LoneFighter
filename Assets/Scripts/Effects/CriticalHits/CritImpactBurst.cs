using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Effects.CriticalHits
{
    /// <summary>
    /// Pooled, world-space radial spark burst spawned at the crit hit position.
    /// Deliberately a separate component (and a separate ParticleSystem prefab)
    /// from <see cref="FxService.ProjectileImpact"/> so a designer can tune crit
    /// sparks independently of regular projectile-impact dust.
    ///
    /// Two ways to drive this:
    ///   1) Authored prefab path. Assign a ParticleSystem prefab in the Inspector
    ///      on the singleton instance in the scene and call <see cref="Play"/>.
    ///      The prefab should have:
    ///         - ParticleSystem.main.stopAction = Disable
    ///         - ParticleSystem.main.playOnAwake = true
    ///         - A <see cref="ParticleAutoRelease"/> component
    ///      so PoolService can recycle it cleanly.
    ///   2) Procedural fallback. If no prefab is assigned a one-shot runtime burst
    ///      is built and thrown away. Useful for testing without the full FX setup.
    /// </summary>
    public class CritImpactBurst : MonoBehaviour
    {
        public static CritImpactBurst Instance { get; private set; }

        [Header("Prefab")]
        [Tooltip("Optional. ParticleSystem prefab spawned through PoolService at the crit position. If null, a procedural burst is used.")]
        [SerializeField] private GameObject sparkPrefab;

        [Header("Procedural Fallback Tuning")]
        [Tooltip("Number of sparks for the runtime fallback (only used when sparkPrefab is null).")]
        [SerializeField, Min(1)] private int fallbackSparkCount = 18;
        [SerializeField] private Color fallbackColor = new Color(1f, 0.9f, 0.3f, 1f);
        [SerializeField] private float fallbackLifetime = 0.35f;
        [SerializeField] private Vector2 fallbackSpeed = new Vector2(4f, 7f);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Spawn the spark burst at <paramref name="worldPos"/>. Safe to call
        /// every frame; pooling keeps the alloc cost minimal.
        /// </summary>
        public void Play(Vector3 worldPos)
        {
            if (sparkPrefab != null)
            {
                SpawnFromPrefab(worldPos);
            }
            else
            {
                SpawnProcedural(worldPos);
            }
        }

        private void SpawnFromPrefab(Vector3 worldPos)
        {
            GameObject instance;
            if (PoolService.Instance != null)
            {
                instance = PoolService.Instance.Get(sparkPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                instance = Instantiate(sparkPrefab, worldPos, Quaternion.identity);
            }
            if (instance == null) return;

            // Match FxService.Spawn: kick the system fresh on each pool-get and
            // ensure ParticleAutoRelease is attached so it returns itself.
            var ps = instance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            if (instance.GetComponent<ParticleAutoRelease>() == null)
            {
                instance.AddComponent<ParticleAutoRelease>();
            }
        }

        private void SpawnProcedural(Vector3 worldPos)
        {
            // Build a one-shot ParticleSystem at runtime. Destroys itself when
            // the system finishes via StopAction.Destroy. Not pooled — this
            // path is only intended for editor sanity checks before the proper
            // prefab is wired up.
            var go = new GameObject("CritImpactBurst.Procedural");
            go.transform.position = worldPos;

            var ps = go.AddComponent<ParticleSystem>();
            // Setting these requires Clear+Stop first; ParticleSystem is added paused-ish
            // in code so we can safely tweak it before Play.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var m = ps.main;
            m.duration = 0.5f;
            m.loop = false;
            m.startLifetime = fallbackLifetime;
            m.startSpeed = new ParticleSystem.MinMaxCurve(fallbackSpeed.x, fallbackSpeed.y);
            m.startSize = 0.12f;
            m.startColor = fallbackColor;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0f;
            m.stopAction = ParticleSystemStopAction.Destroy;
            m.maxParticles = 64;
            ps.main = m;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, fallbackSparkCount) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient { mode = GradientMode.Blend };
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(fallbackColor, 0.5f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 1.4f;
                renderer.velocityScale = 0.2f;
                renderer.sortingOrder = 60;
            }

            ps.Play(true);
        }
    }
}
