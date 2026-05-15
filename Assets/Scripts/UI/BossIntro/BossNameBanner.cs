using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Bottom-left anchored UI banner that slides in from off-screen with a clip-mask
    /// reveal and a slight scale-bounce when it lands. Two TMP_Text rows: title
    /// (large, bold) and subtitle (smaller, mono). The accent colour from
    /// <see cref="BossMeta"/> is applied to the background, underline, and an
    /// optional glow image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The "clip-mask reveal" is implemented via a <see cref="RectMask2D"/> on the
    /// text container: the mask grows from a 0-width sliver to its full width as
    /// the banner slides in, so the text appears to wipe in from left to right.
    /// </para>
    /// <para>
    /// All animation uses unscaled time so the banner reads correctly even while
    /// the surrounding sequence has dropped <c>Time.timeScale</c> to 0.2x.
    /// </para>
    /// <para>
    /// Every visual reference is optional — the banner degrades gracefully if any
    /// inspector slot is left empty. The minimum useful configuration is a
    /// <c>RectTransform</c> root with the two <see cref="TMP_Text"/> labels assigned.
    /// </para>
    /// </remarks>
    public class BossNameBanner : MonoBehaviour
    {
        [Header("Containers")]
        [Tooltip("Banner root that slides on/off screen. Usually anchored bottom-left.")]
        [SerializeField] private RectTransform root;

        [Tooltip("Inner content used for the scale-bounce when the banner lands.")]
        [SerializeField] private RectTransform content;

        [Tooltip("RectMask2D that clips the reveal. Width is animated from 0 to fullClipWidth.")]
        [SerializeField] private RectTransform clipMask;

        [Header("Labels")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;

        [Header("Style (optional accents)")]
        [Tooltip("Optional background panel tinted by BossMeta.accentColor.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Optional underline / accent bar tinted by BossMeta.accentColor.")]
        [SerializeField] private Image underline;
        [Tooltip("Optional glow / halo image, alpha is multiplied by a pulse.")]
        [SerializeField] private Image glow;

        [Header("Layout")]
        [Tooltip("Anchored X position when the banner is fully off-screen (to the left).")]
        [SerializeField] private float offscreenX = -640f;
        [Tooltip("Anchored X position when the banner is fully visible.")]
        [SerializeField] private float onscreenX = 40f;
        [Tooltip("Full clip-mask width once the reveal completes.")]
        [SerializeField] private float fullClipWidth = 600f;
        [Tooltip("Extra pop applied to the content at the end of the slide-in.")]
        [SerializeField] private float bouncePeakScale = 1.08f;

        private Coroutine _routine;

        private void Reset()
        {
            // Sensible defaults when the script is freshly added — anchor bottom-left.
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot     = new Vector2(0f, 0f);
            }
        }

        private void Awake()
        {
            if (root == null) root = transform as RectTransform;
            EnsureHidden();
        }

        /// <summary>Snap the banner off-screen with zero clip width. Idempotent.</summary>
        public void EnsureHidden()
        {
            if (root != null)
            {
                var p = root.anchoredPosition;
                p.x = offscreenX;
                root.anchoredPosition = p;
            }
            if (clipMask != null)
            {
                var size = clipMask.sizeDelta;
                size.x = 0f;
                clipMask.sizeDelta = size;
            }
            if (content != null) content.localScale = new Vector3(0.92f, 0.92f, 1f);
            if (glow != null)
            {
                var c = glow.color;
                c.a = 0f;
                glow.color = c;
            }
        }

        /// <summary>Animate the banner in with the given meta over <paramref name="duration"/> seconds (unscaled).</summary>
        public void SlideIn(BossMeta meta, float duration)
        {
            if (meta == null) return;
            ApplyMeta(meta);
            if (_routine != null) StopCoroutine(_routine);
            gameObject.SetActive(true);
            _routine = StartCoroutine(CoSlideIn(Mathf.Max(0.01f, duration)));
        }

        /// <summary>Animate the banner back out over <paramref name="duration"/> seconds (unscaled).</summary>
        public void SlideOut(float duration)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CoSlideOut(Mathf.Max(0.01f, duration)));
        }

        private void ApplyMeta(BossMeta meta)
        {
            if (titleLabel != null) titleLabel.text = meta.title;
            if (subtitleLabel != null) subtitleLabel.text = meta.subtitle;

            var accent = meta.accentColor;
            if (backgroundImage != null)
            {
                var c = accent;
                c.a = Mathf.Max(accent.a, 0.6f);
                backgroundImage.color = c;
            }
            if (underline != null)
            {
                var c = accent;
                c.a = 1f;
                underline.color = c;
            }
            if (glow != null)
            {
                var c = accent;
                c.a = 0f;
                glow.color = c;
            }
        }

        private IEnumerator CoSlideIn(float duration)
        {
            float t = 0f;
            float startX = offscreenX;
            float endX = onscreenX;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Ease out cubic — fast start, smooth landing.
                float eased = 1f - Mathf.Pow(1f - u, 3f);

                if (root != null)
                {
                    var p = root.anchoredPosition;
                    p.x = Mathf.Lerp(startX, endX, eased);
                    root.anchoredPosition = p;
                }
                if (clipMask != null)
                {
                    var size = clipMask.sizeDelta;
                    size.x = Mathf.Lerp(0f, fullClipWidth, eased);
                    clipMask.sizeDelta = size;
                }
                if (content != null)
                {
                    // Scale lerps from 0.92 -> 1.0 in the main reveal, then a small bounce on top.
                    float baseScale = Mathf.Lerp(0.92f, 1f, eased);
                    content.localScale = new Vector3(baseScale, baseScale, 1f);
                }
                if (glow != null)
                {
                    var c = glow.color;
                    c.a = eased * 0.7f;
                    glow.color = c;
                }
                yield return null;
            }

            // Bounce: 1.0 -> peak -> 1.0 over ~0.18 unscaled seconds.
            const float bounceDuration = 0.18f;
            float bt = 0f;
            while (bt < bounceDuration && content != null)
            {
                bt += Time.unscaledDeltaTime;
                float bu = Mathf.Clamp01(bt / bounceDuration);
                // PingPong: 0 at start, 1 at middle, 0 at end.
                float pingPong = 1f - Mathf.Abs(bu * 2f - 1f);
                float scale = Mathf.Lerp(1f, bouncePeakScale, pingPong);
                content.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (content != null) content.localScale = Vector3.one;
            _routine = null;
        }

        private IEnumerator CoSlideOut(float duration)
        {
            float t = 0f;
            float startX = root != null ? root.anchoredPosition.x : onscreenX;
            float endX = offscreenX;
            float startClipWidth = clipMask != null ? clipMask.sizeDelta.x : fullClipWidth;
            float startGlow = glow != null ? glow.color.a : 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Ease in cubic — slow pull, sharp exit.
                float eased = u * u * u;

                if (root != null)
                {
                    var p = root.anchoredPosition;
                    p.x = Mathf.Lerp(startX, endX, eased);
                    root.anchoredPosition = p;
                }
                if (clipMask != null)
                {
                    var size = clipMask.sizeDelta;
                    size.x = Mathf.Lerp(startClipWidth, 0f, eased);
                    clipMask.sizeDelta = size;
                }
                if (glow != null)
                {
                    var c = glow.color;
                    c.a = Mathf.Lerp(startGlow, 0f, eased);
                    glow.color = c;
                }
                yield return null;
            }

            EnsureHidden();
            _routine = null;
        }
    }
}
