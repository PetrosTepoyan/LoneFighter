using System.Collections;
using TMPro;
using UnityEngine;

namespace LoneFighter.UI.RunSummary
{
    /// <summary>
    /// Small utility that tween-animates an integer count on a <see cref="TMP_Text"/>.
    /// Uses unscaled time so it keeps ticking while the game is paused (the run-summary
    /// panel typically shows while Time.timeScale is still being reset).
    ///
    /// Easing: cubic ease-out (1 - (1-t)^3) — fast start, soft landing, that "satisfying
    /// tick-up" feel.
    ///
    /// Add to any GameObject that already has a <see cref="TMP_Text"/> (e.g. a
    /// <c>TextMeshProUGUI</c>). The label reference auto-resolves in <c>Awake</c> if left null.
    /// </summary>
    public class CountUpText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional prefix, e.g. \"x\" or \"Kills: \".")]
        [SerializeField] private string prefix = string.Empty;
        [Tooltip("Optional suffix, e.g. \" XP\".")]
        [SerializeField] private string suffix = string.Empty;

        private Coroutine _routine;
        private int _displayed;

        private void Awake()
        {
            if (label == null) label = GetComponent<TMP_Text>();
        }

        /// <summary>
        /// Animate the displayed value from its current state up to <paramref name="finalValue"/>
        /// over <paramref name="durationSeconds"/> seconds of unscaled time. A duration of
        /// zero (or less) snaps instantly.
        /// </summary>
        public void SetTarget(int finalValue, float durationSeconds = 0.6f)
        {
            if (label == null)
            {
                Debug.LogWarning($"[{nameof(CountUpText)}] No TMP_Text assigned; cannot animate.", this);
                return;
            }

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (durationSeconds <= 0f || !isActiveAndEnabled)
            {
                SetDisplayed(finalValue);
                return;
            }

            _routine = StartCoroutine(CountUp(_displayed, finalValue, durationSeconds));
        }

        /// <summary>Snap the displayed value without animating. Useful for resets.</summary>
        public void SetImmediate(int value)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            SetDisplayed(value);
        }

        private IEnumerator CountUp(int from, int to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var u = Mathf.Clamp01(t / duration);
                // Cubic ease-out: fast start, gentle settle.
                var eased = 1f - Mathf.Pow(1f - u, 3f);
                var current = Mathf.RoundToInt(Mathf.LerpUnclamped(from, to, eased));
                SetDisplayed(current);
                yield return null;
            }
            SetDisplayed(to);
            _routine = null;
        }

        private void SetDisplayed(int value)
        {
            _displayed = value;
            if (label != null) label.text = $"{prefix}{value}{suffix}";
        }
    }
}
