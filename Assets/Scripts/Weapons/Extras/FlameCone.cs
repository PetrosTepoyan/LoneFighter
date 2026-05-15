using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Weapons.Extras
{
    /// Persistent child of <see cref="FlamethrowerWeapon"/>. Owns:
    ///   - a particle emitter that visually sprays in the current aim direction
    ///   - per-tick damage application via three overlapping Physics2D.OverlapBox queries arranged
    ///     in a fan (a cheap cone approximation, much cheaper than per-target angle math)
    ///
    /// Why three boxes? A single box is too narrow at long range; iterating arbitrary box counts
    /// gets expensive; three (center / +halfAngle / -halfAngle) covers the cone evenly with
    /// minimal physics queries, and resolves correctly on cones up to ~90deg total spread.
    ///
    /// This component is NOT pooled — it persists for the weapon's lifetime, only emitting on
    /// each Tick call. PoolService is reserved for transient spawns.
    public class FlameCone : MonoBehaviour
    {
        [Header("Particles")]
        [Tooltip("Optional particle system to emit on each tick. If absent, this script tries to use a ParticleSystem on this GameObject.")]
        [SerializeField] private ParticleSystem flameParticles;

        [Tooltip("Particles to emit per tick (Emit burst). Falls through to a passive ParticleSystem Emission module if 0.")]
        [SerializeField] private int particlesPerTick = 12;

        [Tooltip("If the ParticleSystem uses Shape = Cone, its angle is pushed to match the current cone half-angle each tick.")]
        [SerializeField] private bool driveParticleShapeAngle = true;

        [Header("Physics")]
        [Tooltip("Layers to query for enemies. Default '~0' = all layers; you can restrict to your Enemy layer in the inspector for perf.")]
        [SerializeField] private LayerMask enemyLayers = ~0;

        [Tooltip("Maximum simultaneous enemy hits per tick (per box). Limits the OverlapBoxNonAlloc buffer.")]
        [SerializeField] private int maxHitsPerBox = 16;

        // Buffer is shared across the three box queries each tick to avoid GC.
        private Collider2D[] _overlapBuffer;
        // We dedupe hits across the three sub-boxes so an enemy under two boxes only takes damage once per tick.
        private readonly System.Collections.Generic.HashSet<EnemyBase> _hitThisTick = new();

        private void Awake()
        {
            _overlapBuffer = new Collider2D[Mathf.Max(1, maxHitsPerBox)];
            if (flameParticles == null) flameParticles = GetComponent<ParticleSystem>();
        }

        /// Driven by <see cref="FlamethrowerWeapon"/> on every fire tick.
        public void Tick(Vector2 origin, Vector2 aim, float coneRange, float coneHalfAngleDeg, float damagePerTick)
        {
            if (coneRange <= 0f) return;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.right;
            Vector2 dir = aim.normalized;
            float baseAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // Anchor the cone at the weapon position with the visual oriented along the aim.
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, baseAngleDeg);

            EmitParticles(coneRange, coneHalfAngleDeg);

            ApplyConeDamage(origin, baseAngleDeg, coneRange, coneHalfAngleDeg, damagePerTick);
        }

        /// Stops particle emission (e.g. when the owning weapon is disabled).
        public void Quiet()
        {
            if (flameParticles != null) flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void EmitParticles(float coneRange, float coneHalfAngleDeg)
        {
            if (flameParticles == null) return;

            if (driveParticleShapeAngle)
            {
                var shape = flameParticles.shape;
                shape.angle = coneHalfAngleDeg;
            }

            var main = flameParticles.main;
            // Push the particle lifetime so visual reach roughly matches the cone range (assuming
            // the ParticleSystem's start speed is ~1; users can tune via inspector).
            float startSpeed = main.startSpeedMultiplier;
            if (startSpeed > 0.01f)
            {
                main.startLifetimeMultiplier = coneRange / startSpeed;
            }

            if (particlesPerTick > 0)
            {
                flameParticles.Emit(particlesPerTick);
            }
            else if (!flameParticles.isPlaying)
            {
                flameParticles.Play(true);
            }
        }

        private void ApplyConeDamage(Vector2 origin, float baseAngleDeg, float coneRange,
            float coneHalfAngleDeg, float damagePerTick)
        {
            if (damagePerTick <= 0f) return;
            _hitThisTick.Clear();

            // Three boxes: center, +halfAngle, -halfAngle. Each is centered halfway along the cone
            // length and sized to span its slice. This is a known fast approximation used by
            // contact-cone weapons in other survivor games on mobile.
            float halfRange = coneRange * 0.5f;
            // Box width is tuned so adjacent boxes overlap modestly, ensuring no gaps in the cone.
            // For a 22deg half-angle the slice width at range/2 is roughly 0.5*tan(half)*range, so
            // we use coneRange * sin(halfAngle/3) per slice plus a small overlap fudge.
            float halfAngleRad = coneHalfAngleDeg * Mathf.Deg2Rad;
            float sliceHalfWidth = coneRange * Mathf.Max(0.15f, Mathf.Sin(halfAngleRad)) * 0.75f;
            // Each box is "length x width" oriented along its sub-angle.
            Vector2 boxSize = new Vector2(coneRange, Mathf.Max(0.2f, sliceHalfWidth));

            // Center box.
            QueryBox(origin, baseAngleDeg, halfRange, boxSize, damagePerTick);
            // Side boxes at +/- halfAngle.
            if (coneHalfAngleDeg > 0.5f)
            {
                QueryBox(origin, baseAngleDeg + coneHalfAngleDeg, halfRange, boxSize, damagePerTick);
                QueryBox(origin, baseAngleDeg - coneHalfAngleDeg, halfRange, boxSize, damagePerTick);
            }
        }

        private void QueryBox(Vector2 origin, float angleDeg, float halfRange, Vector2 boxSize, float damagePerTick)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 center = origin + dir * halfRange;

            int n = Physics2D.OverlapBoxNonAlloc(center, boxSize, angleDeg, _overlapBuffer, enemyLayers);
            for (int i = 0; i < n; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;
                var enemy = col.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                if (_hitThisTick.Contains(enemy)) continue;
                _hitThisTick.Add(enemy);

                enemy.ApplyDamage(damagePerTick);
                if (FxService.Instance != null)
                {
                    // Skip per-tick popups would flood the UI at 10 Hz; keep impact but skip popup.
                    FxService.Instance.ProjectileImpact(enemy.transform.position);
                }
                _overlapBuffer[i] = null;
            }
        }

#if UNITY_EDITOR
        // Visualize the last cone in the editor for tuning.
        [Header("Editor")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private float gizmoConeRange = 3.5f;
        [SerializeField] private float gizmoConeHalfAngle = 22f;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Vector3 origin = transform.position;
            float baseAngle = transform.eulerAngles.z;
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            DrawConeGizmo(origin, baseAngle, gizmoConeRange, gizmoConeHalfAngle);
        }

        private static void DrawConeGizmo(Vector3 origin, float baseAngleDeg, float range, float halfAngleDeg)
        {
            const int segments = 12;
            float start = (baseAngleDeg - halfAngleDeg) * Mathf.Deg2Rad;
            float end = (baseAngleDeg + halfAngleDeg) * Mathf.Deg2Rad;
            Vector3 prev = origin;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(start, end, t);
                Vector3 p = origin + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * range;
                if (i > 0) Gizmos.DrawLine(prev, p);
                Gizmos.DrawLine(origin, p);
                prev = p;
            }
        }
#endif
    }
}
