using System.Collections;
using TMPro;
using UnityEngine;

namespace LoneFighter.Combat.Streaks
{
    /// <summary>
    /// Small persistent HUD chip showing the current uninterrupted kill streak.
    /// Suggested anchor: top-center, just below the run timer. Pulses on each
    /// new kill, fades to a dim "0" between streaks (or hides entirely — see
    /// <see cref="hideAtZero"/>).
    ///
    /// Authoring:
    /// <list type="number">
    ///   <item>Add a chip-shaped UI panel to the HUD canvas (top-center, ~120×40px).</item>
    ///   <item>Add a child <see cref="TMP_Text"/> for the number; assign it to <see cref="label"/>.</item>
    ///   <item>Add this component to the panel root.</item>
    /// </list>
    ///
    /// Uses unscaled time so it animates correctly during the level-up modal.
    /// </summary>
    public class StreakChip : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private KillStreakTracker tracker;
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional CanvasGroup on this object — used for hide-at-zero fading.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Optional RectTransform to scale-pulse. Defaults to label's RectTransform.")]
        [SerializeField] private RectTransform pulseTarget;

        [Header("Display")]
        [SerializeField] private string prefix = "STREAK ";
        [Tooltip("Hide the chip entirely when streak is 0 instead of showing 'STREAK 0'.")]
        [SerializeField] private bool hideAtZero = true;
        [SerializeField, Min(1)] private int minToShow = 1;

        [Header("Pulse")]
        [SerializeField, Min(1f)] private float pulseScale = 1.25f;
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.16f;

        [Header("Tier colors (mirror StreakBanner)")]
        [SerializeField] private int[] tierThresholds = { 10, 25, 50, 100, 250 };
        [SerializeField] private Color[] tierColors = {
            Color.white,
            new Color(1f, 0.92f, 0.30f, 1f),
            new Color(1f, 0.55f, 0.15f, 1f),
            new Color(1f, 0.25f, 0.20f, 1f),
            new Color(1f, 0.30f, 0.95f, 1f),
        };

        private Vector3 _baseScale = Vector3.one;
        private Coroutine _pulseRoutine;
        private bool _capturedScale;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (pulseTarget == null && label != null) pulseTarget = label.rectTransform;
            CaptureScale();
            ApplyVisibility(0);
        }

        private void CaptureScale()
        {
            if (_capturedScale || pulseTarget == null) return;
            _baseScale = pulseTarget.localScale;
            _capturedScale = true;
        }

        private void OnEnable()
        {
            TryWire();
        }

        private void Start()
        {
            TryWire();
            // Reflect the current state on enable so the chip doesn't lie after a scene reload.
            if (tracker != null) HandleStreakChanged(tracker.Current);
        }

        private void OnDisable()
        {
            if (tracker != null) tracker.OnStreakChanged -= HandleStreakChanged;
        }

        private void TryWire()
        {
            if (tracker == null) tracker = KillStreakTracker.Instance;
            if (tracker == null) return;
            tracker.OnStreakChanged -= HandleStreakChanged;
            tracker.OnStreakChanged += HandleStreakChanged;
        }

        private void HandleStreakChanged(int current)
        {
            ApplyVisibility(current);
            if (current >= minToShow)
            {
                if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
                _pulseRoutine = StartCoroutine(Pulse());
            }
        }

        private void ApplyVisibility(int current)
        {
            if (label == null) return;

            if (current < minToShow)
            {
                if (hideAtZero)
                {
                    SetAlpha(0f);
                    label.text = string.Empty;
                }
                else
                {
                    SetAlpha(0.35f);
                    label.text = $"{prefix}{current}";
                    label.color = PickColor(0);
                }
                return;
            }

            SetAlpha(1f);
            label.text = $"{prefix}{current}";
            label.color = PickColor(current);
        }

        private Color PickColor(int current)
        {
            if (tierThresholds == null || tierColors == null) return Color.white;
            int n = Mathf.Min(tierThresholds.Length, tierColors.Length);
            if (n == 0) return Color.white;
            Color picked = tierColors[0];
            for (int i = 0; i < n; i++)
            {
                if (current >= tierThresholds[i]) picked = tierColors[i];
            }
            picked.a = 1f;
            return picked;
        }

        private void SetAlpha(float a)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = a;
                return;
            }
            if (label != null)
            {
                var c = label.color;
                c.a = a;
                label.color = c;
            }
        }

        private IEnumerator Pulse()
        {
            CaptureScale();
            if (pulseTarget == null) yield break;

            Vector3 peak = _baseScale * pulseScale;
            float t = 0f;
            float up = pulseDuration * 0.35f;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / up);
                pulseTarget.localScale = Vector3.LerpUnclamped(_baseScale, peak, k);
                yield return null;
            }
            t = 0f;
            float down = pulseDuration * 0.65f;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / down);
                pulseTarget.localScale = Vector3.LerpUnclamped(peak, _baseScale, k);
                yield return null;
            }
            pulseTarget.localScale = _baseScale;
            _pulseRoutine = null;
        }
    }
}
