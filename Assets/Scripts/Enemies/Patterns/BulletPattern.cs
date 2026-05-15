using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Systems;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Abstract base class for bullet-hell patterns. Each concrete pattern is a
    /// ScriptableObject so designers can mix-and-match them on any enemy via
    /// <see cref="PatternEmitter"/> without touching code.
    ///
    /// Patterns are stateless by default, but subclasses are allowed to keep
    /// runtime state (e.g. <c>Spiral</c> tracks its rotating angle). Because the
    /// same SO instance can be referenced by multiple enemies, designers
    /// should be aware that stateful patterns share that state across all
    /// emitters that point to the same asset — that is intentional for the
    /// patterns shipped here (it produces interesting synchronized fans), but
    /// is something to keep in mind when authoring new ones.
    ///
    /// All patterns spawn pooled <see cref="EnemyProjectile"/> instances via
    /// <see cref="PoolService"/> and configure direction, speed, damage, and
    /// lifetime on the projectile.
    /// </summary>
    public abstract class BulletPattern : ScriptableObject
    {
        [Header("Projectile")]
        [Tooltip("Damage applied by every bullet this pattern fires.")]
        [SerializeField] protected float damage = 5f;
        [Tooltip("How long a fired bullet lives before being recycled.")]
        [SerializeField] protected float projectileLifetime = 3f;

        /// <summary>
        /// Fire the pattern once. May spawn one bullet (Spiral), several
        /// (RadialBurst, Crossfire), or schedule a coroutine that fires over
        /// several frames (AimedBurst).
        /// </summary>
        /// <param name="origin">Transform of the enemy / muzzle firing the pattern.</param>
        /// <param name="target">Transform of the player (used for aimed patterns). May be null.</param>
        /// <param name="projectilePrefab">Prefab carrying an <see cref="EnemyProjectile"/> component.</param>
        public abstract void Execute(Transform origin, Transform target, GameObject projectilePrefab);

        // ----- Shared helpers -------------------------------------------------

        /// <summary>
        /// Spawn one pooled projectile heading in <paramref name="direction"/> at
        /// <paramref name="speed"/>. Returns the spawned GameObject (or null if
        /// the prefab / pool is missing).
        /// </summary>
        protected GameObject SpawnBullet(Vector2 origin, Vector2 direction, float speed, GameObject projectilePrefab)
        {
            if (projectilePrefab == null) return null;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject instance = PoolService.Instance != null
                ? PoolService.Instance.Get(projectilePrefab, origin, rotation)
                : Instantiate(projectilePrefab, origin, rotation);
            if (instance == null) return null;

            var projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile != null)
            {
                projectile.Launch(direction, speed, damage, projectileLifetime);
            }
            return instance;
        }

        /// <summary>
        /// Convenience overload that accepts a Transform origin.
        /// </summary>
        protected GameObject SpawnBullet(Transform origin, Vector2 direction, float speed, GameObject projectilePrefab)
        {
            if (origin == null) return null;
            return SpawnBullet((Vector2)origin.position, direction, speed, projectilePrefab);
        }

        /// <summary>
        /// Compute the direction from the firing origin to the target. Falls back
        /// to <see cref="Vector2.right"/> when the target is missing or coincident.
        /// </summary>
        protected static Vector2 DirectionToTarget(Transform origin, Transform target)
        {
            if (origin == null || target == null) return Vector2.right;
            Vector2 diff = (Vector2)target.position - (Vector2)origin.position;
            return diff.sqrMagnitude < 0.0001f ? Vector2.right : diff.normalized;
        }

        /// <summary>
        /// Rotate <paramref name="v"/> by <paramref name="degrees"/> around the origin.
        /// </summary>
        protected static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
