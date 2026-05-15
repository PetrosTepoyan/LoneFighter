using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// "AN ELITE APPEARS!" banner that fades in across the top of the screen for a
    /// short interval whenever <see cref="EliteSpawnDirector.OnEliteAppeared"/> fires.
    ///
    /// Hook this up on a Canvas object in the Game scene. Either:
    ///  * Assign <see cref="label"/> to a pre-built TMP_Text under the canvas (preferred:
    ///    designer controls font / layout). Optionally assign <see cref="background"/>
    ///    for a panel that fades together.
    ///  * Or leave them null and the banner builds a minimal Canvas+TMP_Text+Image stack
    ///    at runtime so the feature still functions in a bare scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class EliteBanner : MonoBehaviour
    {
        [Header("Refs (optional — auto-built if missing)")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;

        [Header("Timing")]
        [Tooltip("Seconds the banner stays at full alpha.")]
        [Min(0.1f)]
        [SerializeField] private float holdSeconds = 1.6f;
        [Min(0.05f)]
        [SerializeField] private float fadeInSeconds = 0.15f;
        [Min(0.05f)]
        [SerializeField] private float fadeOutSeconds = 0.4f;

        [Header("Style")]
        [SerializeField] private string message = "AN ELITE APPEARS!";
        [SerializeField] private Color textColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
        [Tooltip("If a TMP font isn't already assigned on the label, this is used as a fallback.")]
        [SerializeField] private TMP_FontAsset fallbackFont;

        private Coroutine _routine;
        private bool _selfBuilt;

        private void Awake()
        {
            EnsureUi();
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            EliteSpawnDirector.OnEliteAppeared += HandleEliteAppeared;
        }

        private void OnDisable()
        {
            EliteSpawnDirector.OnEliteAppeared -= HandleEliteAppeared;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void HandleEliteAppeared(EliteModifier _)
        {
            if (canvasGroup == null) return;
            if (label != null) label.text = message;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Fade in
            float t = 0f;
            while (t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Hold
            t = 0f;
            while (t < holdSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < fadeOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutSeconds);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            _routine = null;
        }

        private void EnsureUi()
        {
            if (canvasGroup != null && label != null) return;

            // Look first for any in-children references the designer may have wired up
            // without assigning to the serialized fields.
            if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (background == null) background = GetComponentInChildren<Image>(true);

            if (canvasGroup != null && label != null) return;

            // Fall back to a self-built minimal banner.
            _selfBuilt = true;
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var root = new GameObject("EliteBanner_Auto");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            // Anchor to top-center, stretched horizontally.
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -64f);
            rect.sizeDelta = new Vector2(0f, 96f);

            canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(root.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background = bgGo.AddComponent<Image>();
            background.color = backgroundColor;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = message;
            label.color = textColor;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 56f;
            if (label.font == null && fallbackFont != null) label.font = fallbackFont;
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 72f;
        }

        private void OnDestroy()
        {
            // Self-built UI tracks no extra owners, but null the references for hygiene.
            if (_selfBuilt)
            {
                canvasGroup = null;
                label = null;
                background = null;
            }
        }
    }
}
