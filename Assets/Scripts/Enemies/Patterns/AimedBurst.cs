using System.Collections;
using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Three-shot tight burst aimed at the target with a short delay between
    /// shots. Each shot snapshots the origin's current position so a moving
    /// enemy traces a slight arc; the direction is locked to the target at the
    /// moment the burst starts (no homing).
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Aimed Burst", fileName = "AimedBurst")]
    public class AimedBurst : BulletPattern
    {
        [Header("Burst")]
        [Tooltip("Number of shots in the burst.")]
        [SerializeField, Min(1)] private int shots = 3;
        [Tooltip("Delay between consecutive shots within the burst.")]
        [SerializeField, Min(0f)] private float delayBetweenShots = 0.08f;
        [Tooltip("Bullet speed.")]
        [SerializeField, Min(0.01f)] private float speed = 6f;
        [Tooltip("If true, each shot re-aims at the target (its current position) when fired.")]
        [SerializeField] private bool reaimEachShot = false;
        [Tooltip("Random angular spread (±degrees) applied to each shot to soften the burst.")]
        [SerializeField, Min(0f)] private float spreadDegrees = 2f;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;
            PatternCoroutineRunner.Run(FireBurst(origin, target, projectilePrefab));
        }

        private IEnumerator FireBurst(Transform origin, Transform target, GameObject projectilePrefab)
        {
            Vector2 lockedDir = DirectionToTarget(origin, target);
            var wait = delayBetweenShots > 0f ? new WaitForSeconds(delayBetweenShots) : null;

            for (int i = 0; i < shots; i++)
            {
                // The origin may have been destroyed (enemy died mid-burst).
                if (origin == null) yield break;

                Vector2 dir = reaimEachShot ? DirectionToTarget(origin, target) : lockedDir;
                if (spreadDegrees > 0f)
                {
                    dir = Rotate(dir, Random.Range(-spreadDegrees, spreadDegrees));
                }
                SpawnBullet(origin, dir, speed, projectilePrefab);

                if (i < shots - 1 && wait != null) yield return wait;
            }
        }
    }
}
