using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Weapons
{
    /// A pooled visual-only line segment between two world points. The owning weapon already
    /// applied damage when it spawned the bolt; this script just renders the flash and returns
    /// itself to the pool when its lifetime elapses.
    [RequireComponent(typeof(LineRenderer))]
    public class LightningBolt : MonoBehaviour
    {
        [Tooltip("Number of intermediate vertices (excluding the two endpoints) to jitter for the electric look.")]
        [SerializeField] private int intermediateVertices = 4;

        [Tooltip("Maximum perpendicular jitter applied per intermediate vertex, in world units.")]
        [SerializeField] private float jitterAmount = 0.25f;

        [Tooltip("Default lifetime if Configure is called without a lifetime override.")]
        [SerializeField] private float defaultLifetime = 0.12f;

        [Tooltip("Color at spawn time. Alpha fades to 0 over the bolt's lifetime.")]
        [SerializeField] private Color color = new Color(0.7f, 0.85f, 1f, 1f);

        [SerializeField] private float startWidth = 0.12f;
        [SerializeField] private float endWidth = 0.04f;

        private LineRenderer _line;
        private float _lifetime;
        private float _age;
        private Vector3 _start;
        private Vector3 _end;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
        }

        public void Configure(Vector3 start, Vector3 end, float lifetime = -1f)
        {
            _start = start;
            _end = end;
            _lifetime = lifetime > 0f ? lifetime : defaultLifetime;
            _age = 0f;

            int totalVerts = Mathf.Max(2, intermediateVertices + 2);
            _line.positionCount = totalVerts;
            _line.startWidth = startWidth;
            _line.endWidth = endWidth;
            _line.startColor = color;
            _line.endColor = color;

            Vector3 delta = end - start;
            float length = delta.magnitude;
            Vector3 dir = length > 0.0001f ? delta / length : Vector3.right;
            Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

            _line.SetPosition(0, start);
            _line.SetPosition(totalVerts - 1, end);

            // Jitter the intermediate vertices perpendicular to the segment so the bolt zig-zags.
            for (int i = 1; i < totalVerts - 1; i++)
            {
                float t = (float)i / (totalVerts - 1);
                Vector3 onLine = Vector3.Lerp(start, end, t);
                float offset = Random.Range(-jitterAmount, jitterAmount);
                _line.SetPosition(i, onLine + perp * offset);
            }
        }

        private void OnEnable()
        {
            _age = 0f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_lifetime <= 0f)
            {
                ReturnToPool();
                return;
            }

            float t = Mathf.Clamp01(_age / _lifetime);
            float alpha = 1f - t;
            if (_line != null)
            {
                Color faded = new Color(color.r, color.g, color.b, color.a * alpha);
                _line.startColor = faded;
                _line.endColor = faded;
            }

            if (_age >= _lifetime) ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
