using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Effects.CriticalHits
{
    /// <summary>
    /// Full-screen radial white flash overlay for the moment a critical hit lands.
    ///
    /// Drives a single <see cref="Image"/> alpha on an unscaled-time coroutine so the
    /// flash still animates while <see cref="CritFreezeFrame"/> has time-scale at zero.
    ///
    /// Place this on a Canvas with a stretched Image filling the screen. The Image
    /// should ideally use a radial sprite (white center -> transparent edge) so the
    /// flash reads as a directional pop centered on the player. We don't <em>require</em>
    /// the radial sprite — a plain white fill still works — but the field tooltip
    /// asks for one.
    ///
    /// One singleton in the scene; <see cref="CritFxService"/> calls
    /// <see cref="Play"/> on each crit. Successive crits restart the routine instead
    /// of stacking, which keeps brightness consistent during fire-rate weapons.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CritFlash : MonoBehaviour
    {
        [Header("Overlay")]
        [Tooltip("Full-screen Image. Defaults to the Image on this GameObject. Use a radial-gradient sprite for a more directional 'pop' feel.")]
        [SerializeField] private Image overlay;

        [Header("Flash")]
        [Tooltip("Flash color. Alpha here is the PEAK alpha at t=0.")]
        [SerializeField] private Color flashColor = new Color(1f, 1f, 1f, 0.85f);

        [Tooltip("Total flash duration in seconds (unscaled). Spec target: 0.12s.")]
        [SerializeField, Min(0.01f)] private float duration = 0.12f;

        [Tooltip("Fraction of the duration spent rising from 0 to peak. Remainder is the fade-out.")]
        [SerializeField, Range(0.05f, 0.5f)] private float attackFraction = 0.15f;

        private Coroutine _routine;

        private void Awake()
        {
            if (overlay == null) overlay = GetComponent<Image>();
            if (overlay != null)
            {
                // Start invisible and never block taps.
                var c = overlay.color;
                c.a = 0f;
                overlay.color = c;
                overlay.raycastTarget = false;
            }
        }

        /// <summary>
        /// Trigger a single flash. Safe to call every frame; the existing routine
        /// is restarted so brightness doesn't stack.
        /// </summary>
        public void Play()
        {
            if (overlay == null) return;
            if (!isActiveAndEnabled)
            {
                // Can't run a coroutine on a disabled object; just snap the alpha to 0
                // and bail. Caller's contract is "fire and forget".
                return;
            }
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            float peak = flashColor.a;
            float attack = Mathf.Max(0.0001f, duration * attackFraction);
            float release = Mathf.Max(0.0001f, duration - attack);
            float t = 0f;

            // Attack: 0 -> peak.
            while (t < attack)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / attack);
                SetAlpha(peak * k);
                yield return null;
            }

            // Release: peak -> 0, squared ease-out for a snappier tail.
            t = 0f;
            while (t < release)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / release);
                SetAlpha(peak * k * k);
                yield return null;
            }

            SetAlpha(0f);
            _routine = null;
        }

        private void SetAlpha(float a)
        {
            if (overlay == null) return;
            var c = flashColor;
            c.a = a;
            overlay.color = c;
        }
    }
}
