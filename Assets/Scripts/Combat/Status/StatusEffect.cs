using UnityEngine;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Base class for any time-bounded status effect (DoT, slow, stun, ...).
    /// A StatusEffect instance is owned by exactly one <see cref="StatusController"/>.
    /// Lifecycle, called by the owning controller:
    ///   - OnApply  : once, when first attached (or when re-applied with refresh-only semantics)
    ///   - OnTick   : every <see cref="tickInterval"/> seconds while alive (0 = once per FixedUpdate)
    ///   - OnRemove : once, when duration elapses or RemoveAll() is called
    ///
    /// Stacking semantics are decided by the controller via <see cref="OnReapply"/>:
    /// the default behavior is "refresh duration, ignore the new payload" which fits Burn / Slow / Stun.
    /// Poison-style stacking effects override OnReapply to add a stack instead.
    /// </summary>
    public abstract class StatusEffect
    {
        /// <summary>Total time-to-live in seconds (real-time, scaled).</summary>
        public float duration = 1f;

        /// <summary>Seconds between OnTick() calls. 0 = every FixedUpdate.</summary>
        public float tickInterval = 0.5f;

        /// <summary>
        /// Unique identifier for this effect *type*. Used by the controller to detect re-application
        /// and decide stacking rules. Burn = "burn", Poison = "poison", etc.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>Time-of-application (Time.time). Set by the controller; do not write from effect code.</summary>
        public float StartTime { get; internal set; }

        /// <summary>Time of the next scheduled tick. Set by the controller.</summary>
        public float NextTickTime { get; internal set; }

        /// <summary>Time at which this effect expires. Set by the controller.</summary>
        public float ExpiresAt { get; internal set; }

        /// <summary>Optional display label for HUD icons. Defaults to Id.</summary>
        public virtual string DisplayLabel => Id;

        /// <summary>Optional tint for the icon background. White = no tint.</summary>
        public virtual Color IconColor => Color.white;

        public virtual void OnApply(StatusController controller) { }
        public virtual void OnTick(StatusController controller) { }
        public virtual void OnRemove(StatusController controller) { }

        /// <summary>
        /// Invoked by the controller when an effect with the same <see cref="Id"/> is applied
        /// to a victim that already has it. Default = refresh duration only.
        /// </summary>
        public virtual void OnReapply(StatusController controller, StatusEffect incoming)
        {
            // refresh duration to the longer of (current remaining, incoming.duration)
            float now = Time.time;
            float newExpiry = now + incoming.duration;
            if (newExpiry > ExpiresAt) ExpiresAt = newExpiry;
        }
    }
}
