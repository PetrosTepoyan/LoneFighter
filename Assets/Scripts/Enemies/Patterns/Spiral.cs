using UnityEngine;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Fires one bullet per Execute, rotating the firing angle by
    /// <see cref="angleStep"/> degrees each time. Over many ticks this traces a
    /// classic Touhou-style spiral. Pair this with a low <c>PatternEmitter</c>
    /// fire interval (e.g. 0.06–0.12s) for a tight curl.
    ///
    /// Note: this pattern is stateful (it stores the last fired angle on the SO
    /// instance). Two enemies referencing the same Spiral asset will share that
    /// state — which produces a nicely synchronised spiral if both are firing,
    /// and is the intended behaviour for the shipped assets. Author a duplicate
    /// SO if you need an independent rotation.
    /// </summary>
    [CreateAssetMenu(menuName = "LoneFighter/Bullet Patterns/Spiral", fileName = "Spiral")]
    public class Spiral : BulletPattern
    {
        [Header("Spiral")]
        [Tooltip("Bullet speed.")]
        [SerializeField, Min(0.01f)] private float speed = 4f;
        [Tooltip("Degrees the firing angle advances each Execute.")]
        [SerializeField] private float angleStep = 17f;
        [Tooltip("Number of simultaneous arms in the spiral. 1 = single curl, 2 = double, etc.")]
        [SerializeField, Min(1)] private int arms = 2;
        [Tooltip("Starting angle when the SO is loaded.")]
        [SerializeField] private float startAngle = 0f;

        private float _lastAngle;
        private bool _initialized;

        public override void Execute(Transform origin, Transform target, GameObject projectilePrefab)
        {
            if (origin == null || projectilePrefab == null) return;

            if (!_initialized)
            {
                _lastAngle = startAngle;
                _initialized = true;
            }

            float armSpacing = 360f / arms;
            for (int i = 0; i < arms; i++)
            {
                float angle = _lastAngle + i * armSpacing;
                Vector2 dir = Rotate(Vector2.right, angle);
                SpawnBullet(origin, dir, speed, projectilePrefab);
            }

            _lastAngle = Mathf.Repeat(_lastAngle + angleStep, 360f);
        }

        /// <summary>
        /// Reset the spiral so the next Execute starts at <see cref="startAngle"/>.
        /// Useful for boss phase transitions.
        /// </summary>
        public void ResetSpiral()
        {
            _lastAngle = startAngle;
            _initialized = true;
        }
    }
}
