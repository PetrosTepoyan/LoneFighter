using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Bosses.Roster
{
    /// One-shot visual flourish for the moment a MirrorBoss splits into two:
    ///   - brief slow-motion flash (uses FxService.HitStop if available),
    ///   - two ghostly trails radiating from the split origin to the original and clone,
    ///   - white-blue color burst.
    ///
    /// Lifecycle:
    ///   MirrorBoss instantiates (or pools) this and calls <see cref="Play"/> with the
    ///   origin position and the two target transforms (original / clone). The vfx
    ///   self-disables when the trail phase ends.
    [DisallowMultipleComponent]
    public class MirrorSplitVfx : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Duration of the trail phase. The vfx self-disables after this.")]
        [SerializeField] private float trailDuration = 0.4f;
        [Tooltip("Duration of the slow-mo / hit-stop flash. Routed through FxService.HitStop.")]
        [Range(0f, 0.2f)] [SerializeField] private float slowmoDuration = 0.06f;

        [Header("Visual")]
        [Tooltip("Width of the ghostly trail line.")]
        [SerializeField] private float trailWidth = 0.18f;
        [Tooltip("Color of the trail at start.")]
        [SerializeField] private Color trailColor = new Color(0.8f, 0.95f, 1f, 0.9f);

        [Tooltip("Optional ParticleSystem burst played on split (auto-stops).")]
        [SerializeField] private ParticleSystem burstParticles;

        // Procedural trails: two LineRenderers anchored at the split origin and lerped
        // toward each twin's position over trailDuration.
        private LineRenderer _trailA;
        private LineRenderer _trailB;

        private Vector3 _origin;
        private Transform _targetA;
        private Transform _targetB;
        private float _elapsed;
        private bool _active;

        private void Awake()
        {
            _trailA = AcquireOrCreateLine("TrailA");
            _trailB = AcquireOrCreateLine("TrailB");
            ConfigureLine(_trailA);
            ConfigureLine(_trailB);
        }

        private void OnDisable()
        {
            _active = false;
            if (_trailA != null) _trailA.enabled = false;
            if (_trailB != null) _trailB.enabled = false;
        }

        public void Play(Vector3 origin, Transform original, Transform clone)
        {
            transform.position = origin;
            _origin = origin;
            _targetA = original;
            _targetB = clone;
            _elapsed = 0f;
            _active = true;

            if (_trailA != null) _trailA.enabled = true;
            if (_trailB != null) _trailB.enabled = true;

            if (burstParticles != null)
            {
                burstParticles.transform.position = origin;
                burstParticles.Clear(true);
                burstParticles.Play(true);
            }

            // Brief slow-motion flash.
            if (slowmoDuration > 0f && FxService.Instance != null)
            {
                FxService.Instance.HitStop(slowmoDuration);
                FxService.Instance.HeavyShake();
            }
        }

        private void Update()
        {
            if (!_active) return;
            _elapsed += Time.unscaledDeltaTime; // immune to hit-stop time-scale
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, trailDuration));

            UpdateTrail(_trailA, _targetA, t);
            UpdateTrail(_trailB, _targetB, t);

            if (t >= 1f)
            {
                _active = false;
                if (_trailA != null) _trailA.enabled = false;
                if (_trailB != null) _trailB.enabled = false;
                // Pooled or not, release back if PoolService manages us; otherwise just disable.
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else gameObject.SetActive(false);
            }
        }

        private void UpdateTrail(LineRenderer line, Transform target, float t)
        {
            if (line == null) return;
            if (target == null)
            {
                line.enabled = false;
                return;
            }
            line.SetPosition(0, _origin);
            line.SetPosition(1, Vector3.Lerp(_origin, target.position, t));
            // Fade out across the trail's life.
            Color c = trailColor;
            c.a = Mathf.Lerp(trailColor.a, 0f, t);
            line.startColor = c;
            line.endColor = c;
            line.widthMultiplier = Mathf.Lerp(trailWidth, trailWidth * 0.2f, t);
        }

        private LineRenderer AcquireOrCreateLine(string childName)
        {
            var t = transform.Find(childName);
            if (t == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                t = go.transform;
            }
            var lr = t.GetComponent<LineRenderer>();
            if (lr == null) lr = t.gameObject.AddComponent<LineRenderer>();
            return lr;
        }

        private void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.loop = false;
            line.widthMultiplier = trailWidth;
            line.startColor = trailColor;
            line.endColor = trailColor;
            line.enabled = false;
        }
    }
}
