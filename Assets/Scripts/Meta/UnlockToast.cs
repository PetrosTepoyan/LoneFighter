using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoneFighter.Meta
{
    // Slide-down notification panel for newly earned unlocks.
    //
    // Uses unscaled time because the game enters timeScale = 0 during LevelUp / Paused, and
    // unlocks can fire on Victory (which is awarded right at the wave ending). The toast must
    // continue to animate even if Time.timeScale is 0.
    public class UnlockToast : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root panel that slides in/out. Anchored to top-center.")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Animation")]
        [Tooltip("Y offset (in anchored units) where the panel is fully off-screen. Should be a positive number.")]
        [SerializeField] private float offscreenOffsetY = 200f;
        [Tooltip("Seconds for slide-in and slide-out tweens.")]
        [SerializeField] private float slideDuration = 0.25f;
        [Tooltip("Time the panel stays fully visible before sliding back out.")]
        [SerializeField] private float visibleDuration = 3f;

        private readonly Queue<UnlockRule> _queue = new Queue<UnlockRule>();
        private Coroutine _runner;
        private Vector2 _restingAnchoredPos;
        private bool _cachedRest;

        private void OnEnable()
        {
            if (MetaProgression.Instance != null)
            {
                MetaProgression.Instance.OnNewUnlock += Enqueue;
            }
            CacheRestPos();
            HidePanelInstant();
        }

        private void OnDisable()
        {
            if (MetaProgression.Instance != null)
            {
                MetaProgression.Instance.OnNewUnlock -= Enqueue;
            }
            if (_runner != null)
            {
                StopCoroutine(_runner);
                _runner = null;
            }
        }

        // Try to grab MetaProgression on Start as well, in case it spawned after this UI.
        private void Start()
        {
            if (MetaProgression.Instance != null)
            {
                // Avoid double subscribe.
                MetaProgression.Instance.OnNewUnlock -= Enqueue;
                MetaProgression.Instance.OnNewUnlock += Enqueue;
            }
        }

        private void CacheRestPos()
        {
            if (_cachedRest || panel == null) return;
            _restingAnchoredPos = panel.anchoredPosition;
            _cachedRest = true;
        }

        private void HidePanelInstant()
        {
            if (panel == null) return;
            CacheRestPos();
            panel.anchoredPosition = _restingAnchoredPos + new Vector2(0f, offscreenOffsetY);
            panel.gameObject.SetActive(false);
        }

        public void Enqueue(UnlockRule rule)
        {
            _queue.Enqueue(rule);
            if (_runner == null)
            {
                _runner = StartCoroutine(DrainQueue());
            }
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                var rule = _queue.Dequeue();
                yield return ShowOne(rule);
            }
            _runner = null;
        }

        private IEnumerator ShowOne(UnlockRule rule)
        {
            if (panel == null)
            {
                // No visual — nothing to do, but still respect timing so multiple unlocks
                // don't all log at once.
                yield return new WaitForSecondsRealtime(visibleDuration);
                yield break;
            }

            CacheRestPos();

            if (titleText != null) titleText.text = string.IsNullOrEmpty(rule.displayName) ? rule.id : rule.displayName;
            if (descriptionText != null) descriptionText.text = rule.description ?? string.Empty;

            panel.gameObject.SetActive(true);
            Vector2 hidden = _restingAnchoredPos + new Vector2(0f, offscreenOffsetY);
            Vector2 shown = _restingAnchoredPos;

            yield return Tween(panel, hidden, shown, slideDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, visibleDuration));
            yield return Tween(panel, shown, hidden, slideDuration);

            panel.gameObject.SetActive(false);
        }

        private static IEnumerator Tween(RectTransform rt, Vector2 from, Vector2 to, float duration)
        {
            if (rt == null || duration <= 0f)
            {
                if (rt != null) rt.anchoredPosition = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                // Smoothstep for a less linear feel; cheap and dependency-free.
                float eased = k * k * (3f - 2f * k);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }
            rt.anchoredPosition = to;
        }
    }
}
