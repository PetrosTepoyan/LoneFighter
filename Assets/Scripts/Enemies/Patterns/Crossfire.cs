using System.Collections;
using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Two-stage cross: fires four bullets in cardinal directions (N/E/S/W),
    /// waits <see cref="secondVolleyDelay"/> seconds, then fires four more
    /// rotated 45° (NE/SE/SW/NW). The two volleys combine into an 8-direction
    /// star with a satisfying tempo.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Crossfire", fileName = "Crossfire")]
    public class Crossfire : BulletPattern
    {
        [Header("Crossfire")]
        [Tooltip("Bullet speed.")]
        [SerializeField, Min(0.01f)] private float speed = 4.5f;
        [Tooltip("Delay between the cardinal volley and the diagonal volley.")]
        [SerializeField, Min(0f)] private float secondVolleyDelay = 0.18f;
        [Tooltip("If true, the cardinal axes are aimed toward the target instead of world-axis aligned.")]
        [SerializeField] private bool alignToTarget = false;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;

            float baseAngle = 0f;
            if (alignToTarget)
            {
                Vector2 toTarget = DirectionToTarget(origin, target);
                baseAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            }

            FireFour(origin, baseAngle, projectilePrefab);
            PatternCoroutineRunner.Run(FireDelayedDiagonals(origin, baseAngle, projectilePrefab));
        }

        private void FireFour(Transform origin, float baseAngle, GameObject projectilePrefab)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = baseAngle + i * 90f;
                Vector2 dir = Rotate(Vector2.right, a);
                SpawnBullet(origin, dir, speed, projectilePrefab);
            }
        }

        private IEnumerator FireDelayedDiagonals(Transform origin, float baseAngle, GameObject projectilePrefab)
        {
            if (secondVolleyDelay > 0f) yield return new WaitForSeconds(secondVolleyDelay);
            if (origin == null) yield break;
            FireFour(origin, baseAngle + 45f, projectilePrefab);
        }
    }
}
