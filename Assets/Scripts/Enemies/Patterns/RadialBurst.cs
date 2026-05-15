using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Fires <see cref="count"/> bullets evenly distributed around a full 360°
    /// circle. The classic "danmaku circle". Stateless — every execution
    /// produces the same shape (with an optional pseudo-random rotation
    /// offset to keep successive bursts from stacking).
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Radial Burst", fileName = "RadialBurst")]
    public class RadialBurst : BulletPattern
    {
        [Header("Radial")]
        [Tooltip("Number of bullets in the ring. 12 = every 30°.")]
        [SerializeField, Min(1)] private int count = 12;
        [Tooltip("Speed of every spawned bullet.")]
        [SerializeField, Min(0.01f)] private float speed = 4f;
        [Tooltip("If true, each burst is rotated by a random offset so successive bursts don't perfectly overlap.")]
        [SerializeField] private bool randomizeOffset = true;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;

            float step = 360f / count;
            float offset = randomizeOffset ? Random.Range(0f, step) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = offset + i * step;
                Vector2 dir = Rotate(Vector2.right, angle);
                SpawnBullet(origin, dir, speed, projectilePrefab);
            }
        }
    }
}
