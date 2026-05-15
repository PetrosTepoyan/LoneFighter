using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Subscribes to <see cref="PlayerLevel.OnLevelUp"/> and emits a celebratory
    /// expanding ring burst that lerps from a bright white to a saturated cyan
    /// while fading and growing.
    /// </summary>
    public class LevelUpAura : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("PlayerLevel to subscribe to. Resolved from this GameObject / parents if null.")]
        [SerializeField] private PlayerLevel playerLevel;

        [Header("Sprite")]
        [Tooltip("Ring sprite (authored as a hollow circle). Required.")]
        [SerializeField] private Sprite ringSprite;

        [SerializeField] private string sortingLayerName = "";
        [SerializeField] private int sortingOrder = -4;

        [Header("Colors")]
        [ColorUsage(true, true)]
        [SerializeField] private Color startColor = new Color(1f, 1f, 1f, 1f);

        [ColorUsage(true, true)]
        [SerializeField] private Color endColor = new Color(0.2f, 1f, 1f, 1f);

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float duration = 0.7f;
        [SerializeField, Min(0.01f)] private float startScale = 0.4f;
        [SerializeField, Min(0.01f)] private float endScale = 3.5f;
        [SerializeField, Range(0f, 4f)] private float peakAlpha = 1.2f;

        private Transform _root;
        private SpriteRenderer _sr;
        private float _burstTime = -1f;

        private void Awake()
        {
            if (playerLevel == null) playerLevel = GetComponentInParent<PlayerLevel>();
        }

        private void OnEnable()
        {
            if (playerLevel != null) playerLevel.OnLevelUp += HandleLevelUp;
        }

        private void OnDisable()
        {
            if (playerLevel != null) playerLevel.OnLevelUp -= HandleLevelUp;
        }

        private void HandleLevelUp(int level)
        {
            EnsureRing();
            if (_sr == null) return;
            _burstTime = 0f;
            _root.localScale = Vector3.one * startScale;
            _sr.enabled = true;
        }

        private void EnsureRing()
        {
            if (_root != null) return;
            if (ringSprite == null) return;

            var go = new GameObject("LevelUpAura");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _root = go.transform;

            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = ringSprite;
            _sr.color = WithAlpha(startColor, 0f);
            if (!string.IsNullOrEmpty(sortingLayerName)) _sr.sortingLayerName = sortingLayerName;
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = false;
        }

        private void Update()
        {
            if (_burstTime < 0f || _sr == null) return;

            _burstTime += Time.deltaTime;
            float t = Mathf.Clamp01(_burstTime / duration);

            // Cubic ease-out for a snappy expansion that decelerates.
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            float scale = Mathf.Lerp(startScale, endScale, eased);
            // Alpha rises briefly, then fades to 0.
            float alphaCurve = Mathf.Sin(Mathf.PI * t); // 0..1..0 shape
            float alpha = peakAlpha * alphaCurve;

            var color = Color.Lerp(startColor, endColor, eased);
            _root.localScale = Vector3.one * scale;
            _sr.color = WithAlpha(color, alpha * color.a);

            if (t >= 1f)
            {
                _burstTime = -1f;
                _sr.enabled = false;
            }
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
