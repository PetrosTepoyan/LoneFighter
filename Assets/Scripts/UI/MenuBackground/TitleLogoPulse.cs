// LoneFighter - Subtle main-menu title animation.
// Pulses the TMP_Text scale 1.0 <-> 1.04 on a 2.5s sine wave and slowly
// blends its color between two palette stops.

using UnityEngine;
using TMPro;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Drives a gentle "breathing" animation on the menu title text.
    /// Designed to be cheap and unobtrusive - no allocations per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleLogoPulse : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("TMP_Text to animate. Auto-fetched from this GameObject if null.")]
        [SerializeField] private TMP_Text title;

        [Header("Scale Pulse")]
        [SerializeField] private float minScale = 1.0f;
        [SerializeField] private float maxScale = 1.04f;
        [Tooltip("Full pulse cycle duration in seconds.")]
        [SerializeField] private float periodSeconds = 2.5f;

        [Header("Color Shift")]
        [SerializeField] private Color colorA = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color colorB = new Color(1f, 0.92f, 0.78f, 1f);
        [Tooltip("Multiplier on the scale-pulse phase; 1.0 = matches pulse, 0.5 = half-speed color shift.")]
        [SerializeField] private float colorPhaseMultiplier = 0.5f;

        private Vector3 baseScale;
        private float phase;
        private bool ready;

        private void Awake()
        {
            if (title == null)
            {
                title = GetComponent<TMP_Text>();
            }
            baseScale = transform.localScale;
            ready = title != null;
        }

        private void OnEnable()
        {
            // Reset phase on enable so the pulse always begins at neutral scale.
            phase = 0f;
            if (ready)
            {
                ApplyPulse(0f);
            }
        }

        private void OnDisable()
        {
            // Restore neutral state.
            transform.localScale = baseScale;
            if (ready)
            {
                title.color = colorA;
            }
        }

        private void Update()
        {
            if (!ready)
            {
                return;
            }

            float p = Mathf.Max(0.0001f, periodSeconds);
            phase += Time.unscaledDeltaTime / p;
            if (phase > 1024f)
            {
                phase -= 1024f;
            }
            ApplyPulse(phase);
        }

        private void ApplyPulse(float currentPhase)
        {
            // Sine wave in [0,1] - 0.5*(sin*+1) - so scale stays inside [min,max].
            float angle = currentPhase * Mathf.PI * 2f;
            float t01 = 0.5f * (Mathf.Sin(angle) + 1f);

            float s = Mathf.Lerp(minScale, maxScale, t01);
            transform.localScale = baseScale * s;

            float colorT = 0.5f * (Mathf.Sin(angle * Mathf.Max(0.01f, colorPhaseMultiplier)) + 1f);
            title.color = Color.Lerp(colorA, colorB, colorT);
        }
    }
}
