using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Bosses.Roster
{
    /// Procedural orbital-path ring drawn around the player. Used by OrbiterBoss as a
    /// constantly visible "you are at the edge of this circle" telegraph so the player
    /// can predict the boss's position even when it's just off-camera.
    ///
    /// The ring is rendered with a LineRenderer; if one is absent, it's added on Awake.
    /// The ring follows the player (or a configured target) in LateUpdate so the boss's
    /// orbit and the visual stay coherent for the eye.
    [DisallowMultipleComponent]
    public class OrbitalRing : MonoBehaviour
    {
        [Header("Geometry")]
        [Tooltip("Orbital radius in world units. Should match OrbiterBoss.orbitRadius.")]
        [SerializeField] private float radius = 6f;
        [Tooltip("Number of segments used to draw the circle. 48 looks smooth at portrait scale.")]
        [Range(12, 128)] [SerializeField] private int segments = 48;
        [Tooltip("Line width.")]
        [SerializeField] private float lineWidth = 0.06f;

        [Header("Appearance")]
        [Tooltip("Color of the ring. Tinted toward white-blue by default to read as 'danger zone'.")]
        [SerializeField] private Color ringColor = new Color(0.6f, 0.85f, 1f, 0.45f);
        [Tooltip("Pulse multiplier applied to alpha each second.")]
        [SerializeField] private float pulseSpeed = 1.5f;
        [Tooltip("Alpha amplitude around the base color (0..1).")]
        [Range(0f, 1f)] [SerializeField] private float pulseAmplitude = 0.25f;

        [Header("Target")]
        [Tooltip("Optional explicit center. When null, the ring tracks PlayerController.Instance if available.")]
        [SerializeField] private Transform center;

        private LineRenderer _line;
        private float _phase;

        public float Radius => radius;

        public void SetRadius(float newRadius)
        {
            radius = Mathf.Max(0.1f, newRadius);
            RebuildCircle();
        }

        public void SetCenter(Transform t)
        {
            center = t;
        }

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.widthMultiplier = lineWidth;
            RebuildCircle();
        }

        private void OnEnable()
        {
            _phase = 0f;
            if (_line != null) _line.enabled = true;
        }

        private void OnDisable()
        {
            if (_line != null) _line.enabled = false;
        }

        private void LateUpdate()
        {
            // Center follows the player (or explicit center transform).
            Vector3 c;
            if (center != null) c = center.position;
            else if (PlayerController.Instance != null) c = PlayerController.Instance.transform.position;
            else c = transform.position;
            transform.position = new Vector3(c.x, c.y, transform.position.z);

            // Pulse the alpha.
            _phase += Time.deltaTime * pulseSpeed;
            float a = Mathf.Clamp01(ringColor.a + Mathf.Sin(_phase * Mathf.PI * 2f) * pulseAmplitude);
            Color c1 = ringColor; c1.a = a;
            if (_line != null)
            {
                _line.startColor = c1;
                _line.endColor = c1;
            }
        }

        private void RebuildCircle()
        {
            if (_line == null) return;
            _line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float t = (i / (float)segments) * Mathf.PI * 2f;
                _line.SetPosition(i, new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            RebuildCircle();
        }
#endif
    }
}
