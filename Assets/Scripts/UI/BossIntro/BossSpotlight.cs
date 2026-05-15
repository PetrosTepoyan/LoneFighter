using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.UI.BossIntro
{
    /// <summary>
    /// Fullscreen semi-transparent overlay that darkens everything except a circular
    /// "hole" anchored on the boss's current screen position. The hole is implemented
    /// with a single sprite <see cref="Image"/> (a radial-gradient or
    /// circle-with-feathered-edge texture) that follows the boss every frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the cheap, mobile-friendly path: one fullscreen quad (the dark cover)
    /// plus one sprite (the hole / spotlight glow). It assumes the overlay is drawn
    /// onto a <c>ScreenSpaceOverlay</c> or <c>ScreenSpaceCamera</c> canvas. The
    /// component does not modify the canvas itself; you wire it up in the editor.
    /// </para>
    /// <para>
    /// If <see cref="spotlightImage"/> is configured with a feathered radial-gradient
    /// sprite using a <c>Multiply</c> or <c>Subtractive</c> material, the dark cover
    /// behind it appears to be punched through. If you instead prefer a true sprite
    /// mask, swap the inspector reference for a <c>Mask</c>-based hierarchy — the
    /// public API of this component does not change.
    /// </para>
    /// <para>
    /// All fades are unscaled-time so the spotlight remains responsive while the
    /// surrounding <see cref="BossIntroSequence"/> slows the game down.
    /// </para>
    /// </remarks>
    public class BossSpotlight : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Fullscreen dark overlay. Color should be a near-opaque black; alpha is animated.")]
        [SerializeField] private Image darkCover;
        [Tooltip("Radial gradient / circle that masks the cover where the boss is. Optional but strongly recommended.")]
        [SerializeField] private Image spotlightImage;
        [Tooltip("Optional camera used to project world->screen. Defaults to Camera.main when null.")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("Optional RectTransform that hosts the spotlight image inside the overlay. Required if spotlightImage is set.")]
        [SerializeField] private RectTransform spotlightHost;

        [Header("Look")]
        [Tooltip("Final alpha of the dark cover when fully shown (0..1).")]
        [Range(0f, 1f)] [SerializeField] private float coverAlpha = 0.72f;
        [Tooltip("Diameter of the spotlight, in canvas units.")]
        [SerializeField] private float spotlightDiameter = 360f;
        [Tooltip("How aggressively the spotlight breathes (size pulse amplitude).")]
        [Range(0f, 0.5f)] [SerializeField] private float pulseAmount = 0.06f;
        [Tooltip("Pulse frequency in Hz.")]
        [SerializeField] private float pulseHz = 1.8f;

        private Transform _target;
        private Coroutine _fadeRoutine;
        private bool _visible;

        private void Awake()
        {
            EnsureHidden();
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            if (_target == null) return;
            UpdateSpotlightPosition();
            UpdateSpotlightPulse();
        }

        /// <summary>
        /// Reset the overlay to fully transparent and detach any current target.
        /// </summary>
        public void EnsureHidden()
        {
            _target = null;
            _visible = false;
            if (darkCover != null)
            {
                var c = darkCover.color;
                c.a = 0f;
                darkCover.color = c;
                darkCover.gameObject.SetActive(false);
            }
            if (spotlightImage != null)
            {
                var c = spotlightImage.color;
                c.a = 0f;
                spotlightImage.color = c;
            }
        }

        /// <summary>
        /// Begin tracking <paramref name="target"/> with a colour tint pulled from
        /// <paramref name="meta"/>. Fades the cover in over the default duration.
        /// </summary>
        public void Show(BossMeta meta, Transform target)
        {
            _target = target;
            _visible = true;

            if (darkCover != null) darkCover.gameObject.SetActive(true);
            if (spotlightImage != null && meta != null)
            {
                // Tint the spotlight halo lightly with the boss accent.
                var c = meta.accentColor;
                c.a = 0f; // will be faded in
                spotlightImage.color = c;
            }
            if (spotlightHost != null)
            {
                spotlightHost.sizeDelta = new Vector2(spotlightDiameter, spotlightDiameter);
            }

            UpdateSpotlightPosition();

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CoFade(0f, coverAlpha, 0.35f));
        }

        /// <summary>Fade the spotlight out over <paramref name="duration"/> seconds (unscaled).</summary>
        public void Hide(float duration)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CoFade(darkCover != null ? darkCover.color.a : 0f, 0f, Mathf.Max(0.01f, duration), hideAtEnd: true));
        }

        private IEnumerator CoFade(float from, float to, float duration, bool hideAtEnd = false)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Smoothstep for a soft ramp.
                float eased = u * u * (3f - 2f * u);
                float coverA = Mathf.Lerp(from, to, eased);
                if (darkCover != null)
                {
                    var c = darkCover.color;
                    c.a = coverA;
                    darkCover.color = c;
                }
                if (spotlightImage != null)
                {
                    var c = spotlightImage.color;
                    // Spotlight halo follows the cover alpha, but capped so it doesn't get blinding.
                    c.a = Mathf.Lerp(0f, 0.85f, eased * (to > 0f ? 1f : 0f) + (1f - eased) * (from > 0f ? 1f : 0f));
                    spotlightImage.color = c;
                }
                yield return null;
            }
            if (darkCover != null)
            {
                var c = darkCover.color;
                c.a = to;
                darkCover.color = c;
                if (hideAtEnd) darkCover.gameObject.SetActive(false);
            }
            if (hideAtEnd)
            {
                _visible = false;
                _target = null;
            }
            _fadeRoutine = null;
        }

        private void UpdateSpotlightPosition()
        {
            if (spotlightHost == null || _target == null) return;
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(_target.position);

            // Convert to anchored position relative to the host's parent. Works for both
            // ScreenSpaceOverlay (parent rect uses screen pixels) and ScreenSpaceCamera
            // canvases (RectTransformUtility handles the inverse).
            var parent = spotlightHost.parent as RectTransform;
            if (parent == null)
            {
                spotlightHost.position = screenPos;
                return;
            }

            // For overlay canvases, RectTransformUtility expects a null camera.
            var canvas = spotlightHost.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : (canvas != null ? canvas.worldCamera : cam);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, uiCam, out var local))
            {
                spotlightHost.anchoredPosition = local;
            }
        }

        private void UpdateSpotlightPulse()
        {
            if (spotlightHost == null) return;
            if (pulseAmount <= 0f) return;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f) * pulseAmount;
            float d = spotlightDiameter * pulse;
            spotlightHost.sizeDelta = new Vector2(d, d);
        }
    }
}
