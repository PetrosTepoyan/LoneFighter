using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Wide shotgun-style cone in front of the enemy, aimed at the target. Looser
    /// and faster than <see cref="LineShot"/>: each bullet is jittered both in
    /// angle and in speed so the cloud spreads naturally. Designed to be paired
    /// with a fast emitter cadence for sustained pressure at mid range.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Cone Spray", fileName = "ConeSpray")]
    public class ConeSpray : BulletPattern
    {
        [Header("Cone")]
        [Tooltip("Number of bullets per spray.")]
        [SerializeField, Min(1)] private int count = 6;
        [Tooltip("Half-angle of the cone. 45° → total 90° spread.")]
        [SerializeField, Min(0f)] private float coneHalfAngle = 35f;
        [Tooltip("Mean bullet speed.")]
        [SerializeField, Min(0.01f)] private float speed = 6f;
        [Tooltip("Random speed variation (±) applied per bullet for a natural spread.")]
        [SerializeField, Min(0f)] private float speedVariance = 1.2f;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;

            Vector2 baseDir = DirectionToTarget(origin, target);

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(-coneHalfAngle, coneHalfAngle);
                Vector2 dir = Rotate(baseDir, angle);
                float s = Mathf.Max(0.5f, speed + Random.Range(-speedVariance, speedVariance));
                SpawnBullet(origin, dir, s, projectilePrefab);
            }
        }
    }
}
