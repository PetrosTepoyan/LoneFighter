using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Tutorial
{
    /// <summary>
    /// Single-instance HUD popup used to display contextual tutorial tips.
    /// </summary>
    /// <remarks>
    /// The popup intentionally runs on <b>unscaled</b> time so that fade and
    /// scale animations keep working during pause / level-up
    /// (<c>Time.timeScale == 0</c>) — those are exactly the moments a tip might
    /// need to appear or disappear.
    ///
    /// Wire <see cref="root"/> to a CanvasGroup parent so we can fade alpha
    /// while leaving the underlying layout untouched. Anchor the rect to top
    /// center on the HUD canvas, just below the timer / HP bar.
    /// </remarks>
    public class TipPopup : MonoBehaviour
    {
        [Header("UI refs")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TMP_Text headlineText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Image iconImage;

        [Header("Animation")]
        [Tooltip("Fade duration for both show and hide, in unscaled seconds.")]
        [SerializeField] private float fadeDuration = 0.25f;
        [Tooltip("Starting scale of the bounce-in. 1.0 = no bounce.")]
        [SerializeField] private float bounceStartScale = 0.92f;
        [Tooltip("If true, the popup uses unscaled time so it animates during pause.")]
        [SerializeField] private bool useUnscaledTime = true;

        /// <summary>The step currently displayed by this popup, or null.</summary>
        public TutorialStep CurrentStep { get; private set; }

        /// <summary>True if the popup is at least partially visible.</summary>
        public bool IsVisible { get; private set; }

        private Coroutine _animRoutine;
        private RectTransform _rootTransform;

        private void Awake()
        {
            if (root != null)
            {
                _rootTransform = root.transform as RectTransform;
                root.alpha = 0f;
                root.interactable = false;
                root.blocksRaycasts = false;
                if (_rootTransform != null) _rootTransform.localScale = Vector3.one;
                root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Display the given step's content with a bounce-in fade-in. Leaves
        /// the popup visible until <see cref="Hide"/> is called.
        /// </summary>
        public void Show(TutorialStep step)
        {
            if (step == null || root == null) return;

            CurrentStep = step;

            if (headlineText != null) headlineText.text = step.headline ?? string.Empty;
            if (bodyText != null) bodyText.text = step.body ?? string.Empty;
            if (iconImage != null)
            {
                if (step.icon != null)
                {
                    iconImage.sprite = step.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            root.gameObject.SetActive(true);
            IsVisible = true;
            StartAnim(AnimateShow());
        }

        /// <summary>
        /// Fade out and snap the scale back. Safe to call when not visible.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible || root == null) return;
            IsVisible = false;
            StartAnim(AnimateHide());
        }

        private void StartAnim(IEnumerator routine)
        {
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(routine);
        }

        private IEnumerator AnimateShow()
        {
            float t = 0f;
            float startAlpha = root.alpha;
            float startScale = _rootTransform != null
                ? _rootTransform.localScale.x
                : 1f;
            // If we're starting from invisible, snap the scale to bounceStart so
            // the ease-out reads as a "pop". If we're partway visible already
            // (rapid show after a half-fade-out), keep the current scale.
            if (startAlpha < 0.05f)
            {
                startScale = bounceStartScale;
                if (_rootTransform != null) _rootTransform.localScale = Vector3.one * startScale;
            }

            while (t < fadeDuration)
            {
                t += DeltaTime();
                float k = Mathf.Clamp01(t / fadeDuration);
                root.alpha = Mathf.Lerp(startAlpha, 1f, k);
                if (_rootTransform != null)
                {
                    // Ease-out cubic for a snappier landing.
                    float eased = 1f - Mathf.Pow(1f - k, 3f);
                    float s = Mathf.Lerp(startScale, 1f, eased);
                    _rootTransform.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }

            root.alpha = 1f;
            if (_rootTransform != null) _rootTransform.localScale = Vector3.one;
            _animRoutine = null;
        }

        private IEnumerator AnimateHide()
        {
            float t = 0f;
            float startAlpha = root.alpha;
            float startScale = _rootTransform != null
                ? _rootTransform.localScale.x
                : 1f;

            while (t < fadeDuration)
            {
                t += DeltaTime();
                float k = Mathf.Clamp01(t / fadeDuration);
                root.alpha = Mathf.Lerp(startAlpha, 0f, k);
                if (_rootTransform != null)
                {
                    float s = Mathf.Lerp(startScale, bounceStartScale, k);
                    _rootTransform.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }

            root.alpha = 0f;
            if (_rootTransform != null) _rootTransform.localScale = Vector3.one;
            root.gameObject.SetActive(false);
            CurrentStep = null;
            _animRoutine = null;
        }

        private float DeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}
