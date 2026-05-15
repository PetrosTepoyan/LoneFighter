using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using LoneFighter.Player;

namespace LoneFighter.Polish
{
    /// Full-screen UI flash for damage / level-up moments.
    /// Drives an Image's alpha on an unscaled-time coroutine so it still plays during hit-stop.
    [RequireComponent(typeof(Image))]
    public class ScreenFlash : MonoBehaviour
    {
        [Header("Overlay")]
        [Tooltip("Full-screen UI Image stretched to fill the canvas. Defaults to the Image on this GameObject.")]
        [SerializeField] private Image overlay;

        [Header("Damage Flash")]
        [SerializeField] private Color damageColor = new Color(1f, 0.1f, 0.1f, 0.55f);
        [SerializeField, Min(0.01f)] private float damageDuration = 0.18f;

        [Header("Level-Up Flash")]
        [SerializeField] private Color levelUpColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField, Min(0.01f)] private float levelUpDuration = 0.28f;

        private PlayerHealth _health;
        private PlayerLevel _level;
        private float _lastKnownHealth = -1f;
        private Coroutine _routine;

        private void Awake()
        {
            if (overlay == null) overlay = GetComponent<Image>();
            if (overlay != null)
            {
                var c = overlay.color;
                c.a = 0f;
                overlay.color = c;
                overlay.raycastTarget = false;
            }
        }

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                _health = PlayerController.Instance.GetComponent<PlayerHealth>();
                _level = PlayerController.Instance.GetComponent<PlayerLevel>();
            }
            if (_health != null)
            {
                _lastKnownHealth = _health.Current;
                _health.OnHealthChanged += HandleHealthChanged;
            }
            if (_level != null)
            {
                _level.OnLevelUp += HandleLevelUp;
            }
        }

        private void OnDestroy()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHealthChanged;
            if (_level != null) _level.OnLevelUp -= HandleLevelUp;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (_lastKnownHealth < 0f)
            {
                _lastKnownHealth = current;
                return;
            }
            if (current < _lastKnownHealth)
            {
                Flash(damageColor, damageDuration);
            }
            _lastKnownHealth = current;
        }

        private void HandleLevelUp(int level)
        {
            Flash(levelUpColor, levelUpDuration);
        }

        public void Flash(Color color, float duration)
        {
            if (overlay == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            float peakAlpha = color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Fast attack, ease-out fade.
                float curve = 1f - k;
                curve = curve * curve;
                var c = color;
                c.a = peakAlpha * curve;
                overlay.color = c;
                yield return null;
            }
            var done = color;
            done.a = 0f;
            overlay.color = done;
            _routine = null;
        }
    }
}
