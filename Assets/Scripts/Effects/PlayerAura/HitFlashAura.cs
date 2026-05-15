using UnityEngine;
using LoneFighter.Player;

namespace LoneFighter.Effects.PlayerAura
{
    /// <summary>
    /// Subscribes to <see cref="PlayerHealth.OnHealthChanged"/> and flashes a red
    /// expanding ring sprite around the player whenever current HP decreases.
    ///
    /// A single SpriteRenderer child is created lazily and reused; each hit kicks
    /// off a short coroutine-free animation driven from Update.
    /// </summary>
    public class HitFlashAura : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("PlayerHealth to subscribe to. Resolved from this GameObject / parents if null.")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Sprite")]
        [Tooltip("Ring sprite (authored as a hollow circle). Required.")]
        [SerializeField] private Sprite ringSprite;

        [ColorUsage(true, true)]
        [SerializeField] private Color ringColor = new Color(1f, 0.15f, 0.15f, 1f);

        [SerializeField] private string sortingLayerName = "";
        [SerializeField] private int sortingOrder = -5;

        [Header("Animation")]
        [Tooltip("Total length of the flash animation in seconds.")]
        [SerializeField, Min(0.05f)] private float duration = 0.35f;

        [Tooltip("Starting world-space scale of the ring at the moment of impact.")]
        [SerializeField, Min(0.01f)] private float startScale = 0.6f;

        [Tooltip("Final world-space scale the ring expands to.")]
        [SerializeField, Min(0.01f)] private float endScale = 2.4f;

        [Tooltip("Peak alpha at the start of the flash (fades to 0).")]
        [SerializeField, Range(0f, 4f)] private float peakAlpha = 1f;

        private Transform _root;
        private SpriteRenderer _sr;
        private float _flashTime = -1f;
        private float _lastKnownHp = float.NaN;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                _lastKnownHp = playerHealth.Current;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (!float.IsNaN(_lastKnownHp) && current < _lastKnownHp - 0.001f)
            {
                Trigger();
            }
            _lastKnownHp = current;
        }

        private void Trigger()
        {
            EnsureRing();
            if (_sr == null) return;
            _flashTime = 0f;
            _root.localScale = Vector3.one * startScale;
            _sr.enabled = true;
        }

        private void EnsureRing()
        {
            if (_root != null) return;
            if (ringSprite == null) return;

            var go = new GameObject("HitFlashAura");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            _root = go.transform;

            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = ringSprite;
            _sr.color = WithAlpha(ringColor, 0f);
            if (!string.IsNullOrEmpty(sortingLayerName)) _sr.sortingLayerName = sortingLayerName;
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = false;
        }

        private void Update()
        {
            if (_flashTime < 0f || _sr == null) return;

            _flashTime += Time.deltaTime;
            float t = Mathf.Clamp01(_flashTime / duration);

            // Ease-out scale, ease-out alpha to zero.
            float eased = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(startScale, endScale, eased);
            float alpha = Mathf.Lerp(peakAlpha, 0f, eased);

            _root.localScale = Vector3.one * scale;
            _sr.color = WithAlpha(ringColor, alpha * ringColor.a);

            if (t >= 1f)
            {
                _flashTime = -1f;
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
