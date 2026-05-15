using System;
using UnityEngine;

namespace LoneFighter.Tutorial
{
    /// <summary>
    /// What gameplay event causes a tutorial step to attempt to display.
    /// </summary>
    public enum TutorialTrigger
    {
        /// <summary>Show immediately when the run starts (subject to displayDelaySeconds).</summary>
        OnRunStart,
        OnFirstKill,
        OnFirstXpPickup,
        OnFirstLevelUp,
        /// <summary>Show when the player's HP drops below the low-HP threshold.</summary>
        OnLowHealth,
        /// <summary>Show after a short grace period to teach the pause button.</summary>
        OnPauseAvailable,
        /// <summary>Step is opted in to no automatic trigger; must be shown via API.</summary>
        Manual,
    }

    /// <summary>
    /// What action will dismiss a currently-visible tutorial step and mark it as complete.
    /// </summary>
    public enum TutorialCondition
    {
        OnMoveDistance,
        OnFirstKill,
        OnFirstXpPickup,
        OnFirstLevelUp,
        /// <summary>User must explicitly tap the popup.</summary>
        OnUserDismiss,
        /// <summary>Auto-dismiss after a fixed duration (see <see cref="TipPopup"/>).</summary>
        OnTimeout,
        /// <summary>Dismiss once the player has picked an upgrade from the level-up modal.</summary>
        OnUpgradePicked,
    }

    /// <summary>
    /// Lightweight, inspector-editable lookup that resolves an audio cue by a
    /// human-friendly name. The name string is intentional — Agent 4's audio
    /// authoring system is still in flux and renaming an enum mid-flight would
    /// break tutorial assets, so the tutorial code keeps a soft reference and
    /// audibly no-ops if the cue cannot be resolved.
    /// </summary>
    [Serializable]
    public struct AudioCueRef
    {
        [Tooltip("Logical name of the cue, resolved at runtime. Leave blank for no audio.")]
        public string cueName;
    }

    /// <summary>
    /// Authoring-time description of a single contextual tutorial popup.
    /// </summary>
    /// <remarks>
    /// Steps are pure data: they describe <i>when</i> to show, <i>what</i> to
    /// display, and <i>what dismisses</i> them. The runtime
    /// (<see cref="TutorialManager"/>) is responsible for wiring the triggers
    /// and conditions up to actual game events.
    /// </remarks>
    [Serializable]
    public class TutorialStep
    {
        [Tooltip("Stable, globally-unique step identifier. Used as the PlayerPrefs completion key.")]
        public string id;

        [Tooltip("Short attention-grabbing headline shown in bold at the top of the popup.")]
        public string headline;

        [TextArea]
        [Tooltip("Optional body copy. Keep it short — players are mid-action.")]
        public string body;

        [Tooltip("Game event that causes this step to attempt to display.")]
        public TutorialTrigger trigger = TutorialTrigger.OnRunStart;

        [Tooltip("Game event that dismisses this step once shown.")]
        public TutorialCondition completion = TutorialCondition.OnUserDismiss;

        [Tooltip("Delay between the trigger firing and the popup actually appearing, in seconds.")]
        [Min(0f)]
        public float displayDelaySeconds = 0f;

        [Tooltip("Optional icon shown beside the headline.")]
        public Sprite icon;

        [Tooltip("Optional audio cue played when the popup appears.")]
        public AudioCueRef cue;
    }
}
