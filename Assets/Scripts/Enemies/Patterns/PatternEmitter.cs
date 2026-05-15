using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Enemies.Patterns
{
    /// <summary>
    /// Attach to any enemy / boss prefab to give it a ranged attack driven by a
    /// <see cref="BulletPattern"/> ScriptableObject. Sits alongside (rather than
    /// replacing) the enemy's existing AI — chase / dash / keep-distance
    /// behaviours continue running while the emitter fires at a fixed cadence.
    ///
    /// Reads <see cref="PlayerController.Instance"/> as the firing target.
    /// </summary>
    public class PatternEmitter : MonoBehaviour
    {
        [Header("Pattern")]
        [Tooltip("ScriptableObject describing the bullet pattern to execute on each interval.")]
        [SerializeField] private BulletPattern pattern;
        [Tooltip("Projectile prefab spawned by the pattern. Must carry an EnemyProjectile component.")]
        [SerializeField] private GameObject projectilePrefab;

        [Header("Cadence")]
        [Tooltip("Seconds between pattern executions.")]
        [SerializeField, Min(0.05f)] private float fireInterval = 1.5f;
        [Tooltip("Random offset (±) added to each interval to desync emitters.")]
        [SerializeField, Min(0f)] private float intervalJitter = 0.15f;
        [Tooltip("Delay before the first shot after this component enables.")]
        [SerializeField, Min(0f)] private float initialDelay = 0.5f;

        [Header("Targeting")]
        [Tooltip("Maximum distance to the player at which this emitter will fire. 0 disables the range check.")]
        [SerializeField, Min(0f)] private float fireRange = 0f;
        [Tooltip("If true, the emitter only fires while GameManager is in the Playing state. Disable for menus / debug scenes.")]
        [SerializeField] private bool requirePlayingState = true;

        private float _timer;

        /// <summary>Currently assigned pattern. Settable at runtime for scripted phase changes.</summary>
        public BulletPattern Pattern
        {
            get => pattern;
            set => pattern = value;
        }

        /// <summary>Currently assigned projectile prefab.</summary>
        public GameObject ProjectilePrefab
        {
            get => projectilePrefab;
            set => projectilePrefab = value;
        }

        private void OnEnable()
        {
            _timer = initialDelay + Random.Range(0f, intervalJitter);
        }

        private void Update()
        {
            if (requirePlayingState && !IsGamePlaying()) return;
            if (pattern == null || projectilePrefab == null) return;

            Transform target = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (target == null) return;

            if (fireRange > 0f)
            {
                float sqr = ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr > fireRange * fireRange)
                {
                    // Out of range — keep the timer hot so we can fire as soon as the player closes in.
                    _timer = Mathf.Min(_timer, 0.15f);
                    return;
                }
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            pattern.Execute(transform, target, projectilePrefab);
            _timer = Mathf.Max(0.05f, fireInterval + Random.Range(-intervalJitter, intervalJitter));
        }

        /// <summary>
        /// Resolve <c>GameManager.Instance.State == GameState.Playing</c> via reflection so the
        /// patterns assembly doesn't have to take a hard compile-time dependency on the
        /// systems assembly's GameManager type. Returns true if no GameManager is present
        /// (useful for sandbox / test scenes).
        /// </summary>
        private static bool IsGamePlaying()
        {
            var t = System.Type.GetType("LoneFighter.Systems.GameManager, Assembly-CSharp");
            if (t == null) return true;
            var instanceProp = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object gm = instanceProp != null ? instanceProp.GetValue(null) : null;
            if (gm == null) return true;
            var stateProp = t.GetProperty("State");
            if (stateProp == null) return true;
            object state = stateProp.GetValue(gm);
            return state != null && state.ToString() == "Playing";
        }
    }
}
