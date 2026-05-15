// LoneFighter - Main menu background controller.
// Top-level driver for the "alive" main-menu visual stack:
//   - ParallaxScroller (scrolling background)
//   - MenuParticleField (ambient drifting particles)
//   - MenuFakeCombat (silent sim of fake combat behind the title)
//   - TitleLogoPulse (gentle title animation)
//
// Lives on a MainMenu scene root GameObject and owns the sub-effects.
// Automatically disabled when the scene is unloaded.

using UnityEngine;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Top-level driver that wires up and ticks the main menu background effects.
    /// Attach to a root GameObject in the MainMenu scene. References can either
    /// be assigned in the Inspector, or - if left null - the controller will
    /// search its own children for the required components on Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuBackgroundController : MonoBehaviour
    {
        // PlayerPrefs key used to gate heavy effects on low-end devices.
        // Values expected: "low", "medium", "high" (default = "medium").
        public const string IntensityPrefKey = "LF_MenuFxIntensity";

        [Header("Sub-Effects (auto-located in children if left empty)")]
        [SerializeField] private ParallaxScroller parallax;
        [SerializeField] private MenuParticleField particleField;
        [SerializeField] private MenuFakeCombat fakeCombat;
        [SerializeField] private TitleLogoPulse titlePulse;

        [Header("Behaviour")]
        [Tooltip("If true, disables all sub-effects when this controller is disabled.")]
        [SerializeField] private bool stopChildrenOnDisable = true;

        [Tooltip("Force fake-combat off regardless of PlayerPrefs intensity. Useful for editor previews.")]
        [SerializeField] private bool disableFakeCombat;

        private bool initialized;

        /// <summary>Current intensity tier resolved from PlayerPrefs (lowercased).</summary>
        public string Intensity { get; private set; } = "medium";

        /// <summary>True when intensity is anything other than "low".</summary>
        public bool AllowExpensiveEffects => Intensity != "low";

        private void Awake()
        {
            ResolveReferences();
            Intensity = PlayerPrefs.GetString(IntensityPrefKey, "medium").ToLowerInvariant();
            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                return;
            }
            ApplyState(true);
        }

        private void OnDisable()
        {
            if (stopChildrenOnDisable)
            {
                ApplyState(false);
            }
        }

        private void ResolveReferences()
        {
            if (parallax == null)
            {
                parallax = GetComponentInChildren<ParallaxScroller>(true);
            }
            if (particleField == null)
            {
                particleField = GetComponentInChildren<MenuParticleField>(true);
            }
            if (fakeCombat == null)
            {
                fakeCombat = GetComponentInChildren<MenuFakeCombat>(true);
            }
            if (titlePulse == null)
            {
                titlePulse = GetComponentInChildren<TitleLogoPulse>(true);
            }
        }

        private void ApplyState(bool active)
        {
            if (parallax != null)
            {
                parallax.enabled = active;
            }
            if (particleField != null)
            {
                particleField.enabled = active;
            }
            if (titlePulse != null)
            {
                titlePulse.enabled = active;
            }
            if (fakeCombat != null)
            {
                bool combatAllowed = active && AllowExpensiveEffects && !disableFakeCombat;
                fakeCombat.enabled = combatAllowed;
                fakeCombat.gameObject.SetActive(combatAllowed);
            }
        }

        /// <summary>
        /// Force-refresh intensity from PlayerPrefs and re-apply gating. Call after
        /// the settings menu mutates the intensity preference.
        /// </summary>
        public void RefreshIntensity()
        {
            Intensity = PlayerPrefs.GetString(IntensityPrefKey, "medium").ToLowerInvariant();
            ApplyState(isActiveAndEnabled);
        }
    }
}
