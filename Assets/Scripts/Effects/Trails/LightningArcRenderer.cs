using UnityEngine;

namespace LoneFighter.Effects.Trails
{
    /// <summary>
    /// Alternative chain-lightning visual built on a single <see cref="LineRenderer"/>.
    /// Sits next to (or replaces) the existing <c>LightningBolt</c> prefab — the API
    /// is intentionally compatible: call <see cref="Configure"/> with start, end and
    /// optional lifetime, and the renderer handles jitter + fade-out itself.
    ///
    /// The arc is drawn with 5-7 intermediate vertices (configurable) whose perpendicular
    /// offset from the straight line is randomized inside <c>+/- jitterAmount</c> world units.
    /// Width tapers from <c>startWidth</c> at the source to <c>endWidth</c> at the target so
    /// the bolt visually "lands" on the victim.
    ///
    /// Wire-up:
    /// - Create a prefab with a <c>LineRenderer</c> + this script.
    /// - Set the LineRenderer's material to an additive emissive one (the same material
    ///   the existing LightningBolt uses works perfectly).
    /// - Optionally point ChainLightningWeapon's lightningBoltPrefab at this prefab instead
    ///   of the default LightningBolt for a denser, more chaotic look.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public sealed class LightningArcRenderer : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Number of jittered intermediate vertices between the endpoints. " +
                 "Recommended 5-7. The two endpoints are added on top of this.")]
        [Range(3, 12)]
        [SerializeField] private int intermediateVertices = 6;

        [Tooltip("Maximum perpendicular jitter applied to each intermediate vertex, in world units.")]
        [Min(0f)]
        [SerializeField] private float jitterAmount = 0.30f;

        [Tooltip("Damps jitter near the endpoints so the bolt visibly anchors to source and target.")]
        [SerializeField] private bool taperEnds = true;

        [Header("Width")]
        [Tooltip("Width at the source end (world units).")]
        [Min(0f)]
        [SerializeField] private float startWidth = 0.14f;

        [Tooltip("Width at the target end (world units).")]
        [Min(0f)]
        [SerializeField] private float endWidth = 0.05f;

        [Header("Color")]
        [Tooltip("Base color. Alpha is multiplied by the fade curve over the bolt's lifetime.")]
        [SerializeField] private Color color = new(0.55f, 0.80f, 1.00f, 1f);

        [Header("Lifetime")]
        [Tooltip("Default lifetime when Configure is called without an override.")]
        [Min(0.01f)]
        [SerializeField] private float defaultLifetime = 0.18f;

        [Tooltip("Optional curve mapping age (0..1) -> alpha multiplier (0..1). " +
                 "Leave null for a simple linear fade.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("If true, re-jitters intermediate vertices every frame for a noisier crackle. " +
                 "Slightly more expensive (Random calls per frame per bolt).")]
        [SerializeField] private bool animateJitter = false;

        [Tooltip("How often (seconds) to re-jitter when animateJitter is true.")]
        [Min(0.01f)]
        [SerializeField] private float jitterInterval = 0.04f;

        private LineRenderer _line;
        private float _lifetime;
        private float _age;
        private float _jitterTimer;
        private Vector3 _start;
        private Vector3 _end;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
        }

        /// <summary>
        /// Configure the bolt's endpoints and lifetime, then begin fading.
        /// Resets internal age/jitter state so this method is safe to call on
        /// objects pulled out of a pool.
        /// </summary>
        public void Configure(Vector3 start, Vector3 end, float lifetime = -1f)
        {
            _start = start;
            _end = end;
            _lifetime = lifetime > 0f ? lifetime : defaultLifetime;
            _age = 0f;
            _jitterTimer = 0f;

            int totalVerts = Mathf.Max(2, intermediateVertices + 2);
            _line.positionCount = totalVerts;
            _line.startWidth = startWidth;
            _line.endWidth = endWidth;
            _line.startColor = color;
            _line.endColor = color;

            BuildVertices();
        }

        private void OnEnable()
        {
            _age = 0f;
            _jitterTimer = 0f;
        }

        private void Update()
        {
            if (_lifetime <= 0f) return;

            _age += Time.deltaTime;

            // Optional crackle: re-perturb mid-vertices on a fixed cadence.
            if (animateJitter)
            {
                _jitterTimer -= Time.deltaTime;
                if (_jitterTimer <= 0f)
                {
                    _jitterTimer = jitterInterval;
                    BuildVertices();
                }
            }

            float t = Mathf.Clamp01(_age / _lifetime);
            float alpha = (fadeCurve != null && fadeCurve.length > 0) ? fadeCurve.Evaluate(t) : (1f - t);

            var faded = new Color(color.r, color.g, color.b, color.a * alpha);
            _line.startColor = faded;
            _line.endColor = faded;
        }

        /// <summary>
        /// Has the bolt's authored lifetime elapsed? Cheap helper for pool owners
        /// that want to recycle the renderer themselves.
        /// </summary>
        public bool IsFinished => _lifetime > 0f && _age >= _lifetime;

        private void BuildVertices()
        {
            int totalVerts = _line.positionCount;
            if (totalVerts < 2) return;

            Vector3 delta = _end - _start;
            float length = delta.magnitude;
            Vector3 dir = length > 0.0001f ? delta / length : Vector3.right;

            // Perpendicular in the XY plane (2D game).
            Vector3 perp = new(-dir.y, dir.x, 0f);

            _line.SetPosition(0, _start);
            _line.SetPosition(totalVerts - 1, _end);

            for (int i = 1; i < totalVerts - 1; i++)
            {
                float t = (float)i / (totalVerts - 1);
                Vector3 onLine = Vector3.Lerp(_start, _end, t);

                // Reduce jitter at the endpoints so the bolt visibly anchors instead of
                // springing away from source/target. Bell shape: 4*t*(1-t) peaks at 1 mid-segment.
                float taper = taperEnds ? (4f * t * (1f - t)) : 1f;
                float offset = Random.Range(-jitterAmount, jitterAmount) * taper;

                _line.SetPosition(i, onLine + perp * offset);
            }
        }
    }
}
