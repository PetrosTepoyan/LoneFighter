using System.Collections;
using TMPro;
using UnityEngine;

namespace LoneFighter.Polish
{
    /// On-screen "x{combo}!" label. Pulses on each kill, escalates color by tier, fades when the combo expires.
    public class ComboHud : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ComboCounter counter;
        [SerializeField] private TMP_Text label;

        [Header("Behavior")]
        [SerializeField, Min(0)] private int minComboToShow = 3;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.35f;
        [SerializeField, Min(1f)] private float pulseScale = 1.35f;
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.18f;

        [Header("Tier Colors (low to high)")]
        [SerializeField] private int[] tierThresholds = { 3, 8, 15, 25, 40 };
        [SerializeField] private Color[] tierColors = {
            Color.white,
            new Color(1f, 0.92f, 0.3f),   // yellow
            new Color(1f, 0.55f, 0.15f),  // orange
            new Color(1f, 0.25f, 0.2f),   // red
            new Color(1f, 0.3f, 0.95f)    // magenta
        };

        private int _lastShown = -1;
        private Coroutine _pulseRoutine;
        private Coroutine _fadeRoutine;
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            if (label != null)
            {
                _baseScale = label.rectTransform.localScale;
                SetAlpha(0f);
                label.text = string.Empty;
            }
        }

        private void OnEnable()
        {
            if (counter == null) counter = ComboCounter.Instance;
            if (counter != null) counter.OnComboChanged += HandleComboChanged;
        }

        private void OnDisable()
        {
            if (counter != null) counter.OnComboChanged -= HandleComboChanged;
        }

        private void Start()
        {
            // ComboCounter.Instance may not have been ready in OnEnable timing edge cases.
            if (counter == null)
            {
                counter = ComboCounter.Instance;
                if (counter != null) counter.OnComboChanged += HandleComboChanged;
            }
        }

        private void HandleComboChanged(int combo)
        {
            if (label == null) return;

            if (combo < minComboToShow)
            {
                if (_lastShown >= minComboToShow && _fadeRoutine == null)
                {
                    _fadeRoutine = StartCoroutine(FadeOut());
                }
                _lastShown = combo;
                return;
            }

            bool isNewKill = combo > _lastShown;
            _lastShown = combo;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            label.text = $"x{combo}!";
            label.color = PickTierColor(combo);
            SetAlpha(label.color.a == 0f ? 1f : label.color.a); // ensure visible

            if (isNewKill)
            {
                if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
                _pulseRoutine = StartCoroutine(Pulse());
            }
        }

        private Color PickTierColor(int combo)
        {
            if (tierThresholds == null || tierColors == null || tierThresholds.Length == 0 || tierColors.Length == 0)
                return Color.white;
            int n = Mathf.Min(tierThresholds.Length, tierColors.Length);
            Color picked = tierColors[0];
            for (int i = 0; i < n; i++)
            {
                if (combo >= tierThresholds[i]) picked = tierColors[i];
            }
            // Ensure alpha is preserved at 1 for visibility.
            picked.a = 1f;
            return picked;
        }

        private IEnumerator Pulse()
        {
            float t = 0f;
            Vector3 peak = _baseScale * pulseScale;
            // Up to peak.
            float up = pulseDuration * 0.35f;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / up);
                label.rectTransform.localScale = Vector3.LerpUnclamped(_baseScale, peak, k);
                yield return null;
            }
            // Down to base.
            t = 0f;
            float down = pulseDuration * 0.65f;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / down);
                label.rectTransform.localScale = Vector3.LerpUnclamped(peak, _baseScale, k);
                yield return null;
            }
            label.rectTransform.localScale = _baseScale;
            _pulseRoutine = null;
        }

        private IEnumerator FadeOut()
        {
            Color start = label.color;
            float t = 0f;
            while (t < fadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(start.a, 0f, t / fadeOutSeconds);
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(0f);
            label.text = string.Empty;
            _fadeRoutine = null;
        }

        private void SetAlpha(float a)
        {
            if (label == null) return;
            var c = label.color;
            c.a = a;
            label.color = c;
        }
    }
}
