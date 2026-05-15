using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Sweeping line: fires <see cref="count"/> bullets in a single tick, fanned
    /// across a symmetric sweep from <c>-sweepDegrees</c> to <c>+sweepDegrees</c>
    /// around the direction to the target. With the default ±30°, a count of 5
    /// fires at -30, -15, 0, +15, +30 degrees from the player-facing direction.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Line Shot", fileName = "LineShot")]
    public class LineShot : BulletPattern
    {
        [Header("Line Sweep")]
        [Tooltip("Number of bullets in the sweep.")]
        [SerializeField, Min(1)] private int count = 5;
        [Tooltip("Half-angle of the sweep around the player-facing direction. ±30 = 60° total.")]
        [SerializeField, Min(0f)] private float sweepDegrees = 30f;
        [Tooltip("Bullet speed.")]
        [SerializeField, Min(0.01f)] private float speed = 5f;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;

            Vector2 baseDir = DirectionToTarget(origin, target);

            if (count == 1)
            {
                SpawnBullet(origin, baseDir, speed, projectilePrefab);
                return;
            }

            float total = sweepDegrees * 2f;
            float step = total / (count - 1);
            float start = -sweepDegrees;

            for (int i = 0; i < count; i++)
            {
                float angle = start + i * step;
                Vector2 dir = Rotate(baseDir, angle);
                SpawnBullet(origin, dir, speed, projectilePrefab);
            }
        }
    }
}
