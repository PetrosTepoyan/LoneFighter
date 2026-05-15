// LoneFighter - Subtle hover/touch FX for menu buttons.
// Attach to any UI Button - on pointer enter (or touch down) the button
// scales up slightly and an optional Image glow alpha fades in.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Adds a gentle hover/touch effect to a UI Button. Uses an attached
    /// EventTrigger (created at runtime if absent) so it works without
    /// modifying the Button prefab. Safe to attach to any Selectable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class MenuButtonHoverFx : MonoBehaviour
    {
        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float scaleLerpSpeed = 12f;

        [Header("Glow (optional)")]
        [Tooltip("Optional Image used as a glow underlay. Alpha is animated between hidden and visible values.")]
        [SerializeField] private Graphic glowGraphic;
        [SerializeField, Range(0f, 1f)] private float glowAlphaHidden = 0f;
        [SerializeField, Range(0f, 1f)] private float glowAlphaVisible = 0.85f;
        [SerializeField] private float glowLerpSpeed = 10f;

        private RectTransform rt;
        private Vector3 baseScale;
        private float currentScaleT; // 0 = base, 1 = hovered
        private float currentGlowT;
        private bool hovered;

        private void Awake()
        {
            rt = (RectTransform)transform;
            baseScale = rt.localScale;
            EnsureEventTrigger();
            if (glowGraphic != null)
            {
                SetGlowAlpha(glowAlphaHidden);
            }
        }

        private void OnDisable()
        {
            // Snap back to neutral so the button doesn't get stuck mid-hover.
            hovered = false;
            currentScaleT = 0f;
            currentGlowT = 0f;
            if (rt != null)
            {
                rt.localScale = baseScale;
            }
            if (glowGraphic != null)
            {
                SetGlowAlpha(glowAlphaHidden);
            }
        }

        private void Update()
        {
            float target = hovered ? 1f : 0f;
            currentScaleT = Mathf.MoveTowards(currentScaleT, target, scaleLerpSpeed * Time.unscaledDeltaTime);
            currentGlowT = Mathf.MoveTowards(currentGlowT, target, glowLerpSpeed * Time.unscaledDeltaTime);

            float s = Mathf.Lerp(1f, hoverScale, currentScaleT);
            rt.localScale = baseScale * s;

            if (glowGraphic != null)
            {
                SetGlowAlpha(Mathf.Lerp(glowAlphaHidden, glowAlphaVisible, currentGlowT));
            }
        }

        private void SetGlowAlpha(float a)
        {
            var c = glowGraphic.color;
            c.a = a;
            glowGraphic.color = c;
        }

        private void EnsureEventTrigger()
        {
            var trigger = GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = gameObject.AddComponent<EventTrigger>();
            }

            AddTrigger(trigger, EventTriggerType.PointerEnter, _ => hovered = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => hovered = false);
            // Treat touch press as hover-on so mobile gets the same feedback.
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => hovered = true);
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => hovered = false);
            // Keyboard / controller focus support.
            AddTrigger(trigger, EventTriggerType.Select, _ => hovered = true);
            AddTrigger(trigger, EventTriggerType.Deselect, _ => hovered = false);
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
