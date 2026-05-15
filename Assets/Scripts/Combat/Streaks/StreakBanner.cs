using System.Collections;
using TMPro;
using UnityEngine;

namespace LoneFighter.Combat.Streaks
{
    /// <summary>
    /// Large UI banner that slides in from the right edge of the screen on each
    /// streak-reward tier, holds briefly, then slides back out. Each tier escalates
    /// font size and color (white → yellow → orange → red → magenta).
    ///
    /// Authoring (one-time, in the Unity editor):
    /// <list type="number">
    ///   <item>Add a <see cref="UnityEngine.UI.CanvasScaler"/>-backed Canvas to the Game scene.</item>
    ///   <item>Create a child <c>StreakBanner</c> RectTransform anchored to center-right.</item>
    ///   <item>Add a <see cref="TMPro.TMP_Text"/> child (large, bold) and wire it to <see cref="label"/>.</item>
    ///   <item>Drop this <see cref="StreakBanner"/> component on the panel root and assign <see cref="panel"/>.</item>
    /// </list>
    ///
    /// Uses unscaled time so it animates correctly during the level-up modal
    /// (which freezes Time.timeScale to 0).
    /// </summary>
    public class StreakBanner : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Root RectTransform that slides in/out (usually this object's RectTransform).")]
        [SerializeField] private RectTransform panel;
        [Tooltip("Banner label text — preferably a TMP_Text with auto-size disabled.")]
        [SerializeField] private TMP_Text label;
        [Tooltip("Optional CanvasGroup on the panel — used to fade alongside the slide.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing (unscaled seconds)")]
        [SerializeField, Min(0.05f)] private float slideInSeconds = 0.18f;
        [SerializeField, Min(0.05f)] private float holdSeconds = 1.8f;
        [SerializeField, Min(0.05f)] private float slideOutSeconds = 0.25f;

        [Header("Slide")]
        [Tooltip("Off-screen X offset (positive = panel starts off-screen to the right).")]
        [SerializeField] private float offscreenX = 800f;

        [Header("Tier presentation (parallel arrays)")]
        [Tooltip("Streak counts that map 1:1 onto fontSizes/colors below.")]
        [SerializeField] private int[] tiers = { 10, 25, 50, 100, 250 };

        [Tooltip("Font size per tier (TMP units).")]
        [SerializeField] private float[] fontSizes = { 64f, 80f, 100f, 124f, 160f };

        [SerializeField] private Color[] tierColors = {
            Color.white,
            new Color(1f, 0.92f, 0.30f, 1f),   // yellow
            new Color(1f, 0.55f, 0.15f, 1f),   // orange
            new Color(1f, 0.25f, 0.20f, 1f),   // red
            new Color(1f, 0.30f, 0.95f, 1f),   // magenta
        };

        private Coroutine _routine;
        private Vector2 _restAnchoredPos;
        private bool _capturedRest;

        private void Awake()
        {
            if (panel == null) panel = transform as RectTransform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            CaptureRest();
            HideImmediate();
        }

        private void CaptureRest()
        {
            if (_capturedRest || panel == null) return;
            _restAnchoredPos = panel.anchoredPosition;
            _capturedRest = true;
        }

        private void HideImmediate()
        {
            if (panel == null) return;
            panel.anchoredPosition = _restAnchoredPos + new Vector2(offscreenX, 0f);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (label != null) label.text = string.Empty;
            gameObject.SetActive(true); // remain enabled so coroutines can run; visibility is via alpha + offset.
        }

        /// <summary>
        /// Public entry point — show the banner for the given streak count.
        /// Interrupts any in-flight animation.
        /// </summary>
        public void Show(int streak)
        {
            CaptureRest();
            if (panel == null) return;
            if (label == null) return;

            int tierIdx = PickTierIndex(streak);
            ApplyTierStyling(tierIdx);

            label.text = streak == 10 ? $"{streak} KILL STREAK!" : $"{streak}!";

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Animate());
        }

        private int PickTierIndex(int streak)
        {
            if (tiers == null || tiers.Length == 0) return 0;
            int idx = 0;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (streak >= tiers[i]) idx = i;
            }
            return idx;
        }

        private void ApplyTierStyling(int idx)
        {
            if (label == null) return;
            if (fontSizes != null && fontSizes.Length > 0)
            {
                int i = Mathf.Clamp(idx, 0, fontSizes.Length - 1);
                label.fontSize = fontSizes[i];
            }
            if (tierColors != null && tierColors.Length > 0)
            {
                int i = Mathf.Clamp(idx, 0, tierColors.Length - 1);
                var c = tierColors[i];
                c.a = 1f;
                label.color = c;
            }
        }

        private IEnumerator Animate()
        {
            Vector2 from = _restAnchoredPos + new Vector2(offscreenX, 0f);
            Vector2 to = _restAnchoredPos;

            // Slide in.
            float t = 0f;
            while (t < slideInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / slideInSeconds);
                // Ease-out cubic.
                float e = 1f - Mathf.Pow(1f - k, 3f);
                panel.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
                SetAlpha(e);
                yield return null;
            }
            panel.anchoredPosition = to;
            SetAlpha(1f);

            // Hold.
            float held = 0f;
            while (held < holdSeconds)
            {
                held += Time.unscaledDeltaTime;
                yield return null;
            }

            // Slide out.
            t = 0f;
            while (t < slideOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / slideOutSeconds);
                // Ease-in cubic.
                float e = k * k * k;
                panel.anchoredPosition = Vector2.LerpUnclamped(to, from, e);
                SetAlpha(1f - e);
                yield return null;
            }
            panel.anchoredPosition = from;
            SetAlpha(0f);
            if (label != null) label.text = string.Empty;
            _routine = null;
        }

        private void SetAlpha(float a)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = a;
            }
            else if (label != null)
            {
                var c = label.color;
                c.a = a;
                label.color = c;
            }
        }
    }
}
