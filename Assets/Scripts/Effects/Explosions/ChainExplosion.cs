using System.Collections;
using UnityEngine;

namespace LoneFighter.Effects.Explosions
{
    /// <summary>
    /// Walks a chain of explosions outward from a seed position, each detonating after
    /// a tunable delay at the previous explosion's edge. Built for "everything dies"
    /// moments (boss arena clear, ultimate weapon firing, scripted set-piece).
    ///
    /// Two ways to use it:
    ///   - Static convenience: <see cref="Run(Vector3, ExplosionTier, int, float, float)"/>
    ///     spins up a temporary host GameObject and runs the coroutine. Auto-cleans up.
    ///   - Component: drop on a GameObject and call <see cref="Begin"/>. Survives the
    ///     calling context (good for scripted sequences in BossDeathMegaExplosion).
    ///
    /// The chain advances by sampling a random direction within an outward cone from
    /// the previous link, so the path looks organic rather than a perfect spiral. The
    /// step distance equals the previous tier's ring radius * <see cref="stepMultiplier"/>,
    /// so each detonation overlaps the previous edge for visual cohesion.
    /// </summary>
    public class ChainExplosion : MonoBehaviour
    {
        [Header("Chain")]
        [SerializeField] private ExplosionTier tier = ExplosionTier.Medium;
        [SerializeField, Min(1)] private int linkCount = 6;
        [SerializeField, Min(0f)] private float secondsBetweenLinks = 0.07f;
        [SerializeField, Min(0f)] private float damagePerLink = 0f;

        [Tooltip("Multiplier applied to the previous link's ring radius to compute the next link's step distance. 1.0 = each link sits at the very edge of the previous one. >1 = chain spreads more aggressively.")]
        [SerializeField, Range(0.4f, 2.5f)] private float stepMultiplier = 1.0f;

        [Tooltip("Cone half-angle (degrees) used to randomize the outward direction at each step. 0 = perfectly straight chain.")]
        [SerializeField, Range(0f, 180f)] private float coneHalfAngleDeg = 35f;

        [Tooltip("Initial outward direction when started programmatically. The static Run() can override per call.")]
        [SerializeField] private Vector2 initialDirection = Vector2.right;

        [Header("Lifecycle")]
        [Tooltip("If true, destroy this GameObject when the chain finishes. Use on disposable hosts spawned by Run().")]
        [SerializeField] private bool destroyOnFinish = false;

        private Coroutine _routine;

        /// <summary>
        /// One-shot helper: spawns a temporary host GameObject, runs a chain of
        /// <paramref name="linkCount"/> explosions of <paramref name="tier"/>, then disposes.
        /// Returns the host so callers can stop it early if needed.
        /// </summary>
        public static ChainExplosion Run(Vector3 startWorldPos,
                                         ExplosionTier tier,
                                         int linkCount = 6,
                                         float secondsBetweenLinks = 0.07f,
                                         float damagePerLink = 0f)
        {
            var go = new GameObject($"ChainExplosion ({tier} x{linkCount})");
            go.transform.position = startWorldPos;
            var chain = go.AddComponent<ChainExplosion>();
            chain.tier = tier;
            chain.linkCount = Mathf.Max(1, linkCount);
            chain.secondsBetweenLinks = Mathf.Max(0f, secondsBetweenLinks);
            chain.damagePerLink = Mathf.Max(0f, damagePerLink);
            chain.destroyOnFinish = true;
            chain.initialDirection = Random.insideUnitCircle.normalized;
            chain.Begin(startWorldPos);
            return chain;
        }

        /// <summary>Start the chain at the given world position.</summary>
        public void Begin(Vector3 worldStart)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(RunChain(worldStart));
        }

        /// <summary>Stop a running chain immediately. The current link still finishes its own animation in the pool.</summary>
        public void Stop()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (destroyOnFinish) Destroy(gameObject);
        }

        private IEnumerator RunChain(Vector3 worldStart)
        {
            // Service might not be wired yet on the very first frame of the scene.
            // We do not silently no-op; we wait one frame and then check once.
            if (ExplosionService.Instance == null) yield return null;

            Vector3 cursor = worldStart;
            Vector2 dir = initialDirection.sqrMagnitude > 0.0001f
                ? initialDirection.normalized
                : Vector2.right;

            for (int i = 0; i < linkCount; i++)
            {
                // Resolve profile/radius lazily — designer might swap profiles mid-chain.
                ExplosionProfile profile = ExplosionService.Instance != null
                    ? ExplosionService.Instance.GetProfile(tier)
                    : null;
                float ringRadius = profile != null ? profile.ringRadius : 1.5f;

                if (ExplosionService.Instance != null)
                {
                    ExplosionService.Instance.Detonate(cursor, tier, damagePerLink);
                }

                if (i < linkCount - 1)
                {
                    // Random outward step inside the cone, then advance.
                    float jitter = Random.Range(-coneHalfAngleDeg, coneHalfAngleDeg);
                    dir = Rotate(dir, jitter).normalized;
                    cursor += (Vector3)(dir * (ringRadius * stepMultiplier));

                    if (secondsBetweenLinks > 0f)
                    {
                        yield return new WaitForSecondsRealtime(secondsBetweenLinks);
                    }
                }
            }

            _routine = null;
            if (destroyOnFinish) Destroy(gameObject);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }
    }
}
