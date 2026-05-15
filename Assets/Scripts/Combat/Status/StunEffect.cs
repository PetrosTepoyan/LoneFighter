using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Halts movement for <see cref="duration"/> seconds (default 1s).
    /// Strategy:
    ///   - tickInterval = 0 → ticks every FixedUpdate, which is the same cadence EnemyChaseAI uses
    ///     to write Rigidbody2D.linearVelocity. By zeroing the velocity *after* the AI's FixedUpdate,
    ///     we win the tick (Unity calls FixedUpdate in script-execution-order; StatusController is
    ///     intentionally left at the default order so it runs *after* AI alphabetically:
    ///     EnemyChaseAI < StatusController). For belt-and-braces we also disable the AI component
    ///     while the stun is active.
    ///   - Re-applying refreshes duration.
    /// </summary>
    public class StunEffect : StatusEffect
    {
        public const string EffectId = "stun";

        private EnemyChaseAI _ai;
        private bool _aiWasEnabled;

        public StunEffect()
        {
            duration = 1f;
            tickInterval = 0f; // every FixedUpdate
        }

        public override string Id => EffectId;
        public override string DisplayLabel => "Stun";
        public override Color IconColor => new Color(1f, 1f, 0.3f, 1f);

        public override void OnApply(StatusController controller)
        {
            if (controller == null) return;

            // Best-effort: disable the AI so it stops driving Rigidbody2D.linearVelocity.
            if (controller.TryGetComponent(out _ai))
            {
                _aiWasEnabled = _ai.enabled;
                _ai.enabled = false;
            }

            if (controller.Body != null)
            {
                controller.Body.linearVelocity = Vector2.zero;
            }
        }

        public override void OnTick(StatusController controller)
        {
            // Re-zero every physics step in case anything else pushed the body.
            if (controller != null && controller.Body != null)
            {
                controller.Body.linearVelocity = Vector2.zero;
            }
        }

        public override void OnRemove(StatusController controller)
        {
            if (_ai != null)
            {
                _ai.enabled = _aiWasEnabled;
                _ai = null;
            }
        }

        public override void OnReapply(StatusController controller, StatusEffect incoming)
        {
            float now = Time.time;
            float newExpiry = now + incoming.duration;
            if (newExpiry > ExpiresAt) ExpiresAt = newExpiry;
        }
    }
}
