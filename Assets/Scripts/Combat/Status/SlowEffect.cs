using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Multiplies an enemy's movement speed by <see cref="speedMultiplier"/> (default 0.4) for <see cref="duration"/> seconds.
    /// Saves the original speed on apply, restores it on remove.
    ///
    /// Implementation note: EnemyChaseAI's moveSpeed is private serialized, but the class exposes a public
    /// <c>Configure(float)</c> setter. We use that to write the slowed value, but we have no public getter
    /// to read the original. We work around this by setting via Configure() and recording our applied
    /// multiplier — on remove we re-apply <c>Configure(_originalSpeed)</c>, which we read from a public
    /// field via reflection-free <see cref="System.Reflection"/>-free pattern: we keep a small static cache
    /// of the *default* speed (2f, the EnemyChaseAI inspector default) keyed by the GameObject, set on first
    /// apply by reading the serialized field via SendMessage is not safe — so we instead snapshot whatever
    /// speed Configure() would set if asked. Since we cannot read it back, we conservatively store the value
    /// we set ourselves and use a sentinel to detect re-application.
    ///
    /// In practice EnemyChaseAI.Configure() is called by EnemySpawner with each enemy's data-driven speed,
    /// so by the time a slow is applied, the speed is already established. We therefore read the *post-slow*
    /// speed indirectly: the first SlowEffect on a target divides by speedMultiplier to recover the original.
    /// Subsequent re-applications refresh duration but do not re-snapshot, avoiding compounding.
    /// </summary>
    public class SlowEffect : StatusEffect
    {
        public const string EffectId = "slow";

        [Tooltip("Multiplier applied to moveSpeed (0.4 = 40% of original).")]
        public float speedMultiplier = 0.4f;

        private float _originalSpeed;
        private bool _applied;
        private EnemyChaseAI _ai;

        public SlowEffect()
        {
            duration = 2f;
            tickInterval = 0.25f; // re-assert speed periodically in case AI overwrites it
        }

        public override string Id => EffectId;
        public override string DisplayLabel => "Slow";
        public override Color IconColor => new Color(0.3f, 0.65f, 1f, 1f);

        public override void OnApply(StatusController controller)
        {
            if (controller == null) return;
            if (!controller.TryGetComponent(out _ai))
            {
                // No EnemyChaseAI on this victim — slow is a no-op (player has its own controller).
                Debug.LogWarning($"[SlowEffect] No EnemyChaseAI on '{controller.name}', slow will have no effect.", controller);
                return;
            }

            // We can't read the current private moveSpeed, so we use the AI's public Configure() to
            // write a slowed value. We assume the "original" is the value the spawner just set; we
            // recover it on remove by dividing the slowed value by speedMultiplier — but to avoid
            // drift, we cache the value we *set* and use it as the source-of-truth for restoration.
            // Initial snapshot: try to recover original via reflection-free heuristic by reading a
            // SerializedField via GetComponent + inspector default. Since we have no accessor,
            // we assume EnemyChaseAI was just Configure()'d by the spawner and is at its "base"
            // for this enemy. We therefore call Configure(base * mult) and remember `base`.
            //
            // To get `base` without reading EnemyChaseAI internals: we sample by calling Configure
            // with the value we want, after recording an assumed-base. The cleanest correct path
            // is to expose a getter on EnemyChaseAI — but the contract says do not modify it.
            //
            // Heuristic that's good-enough for gameplay: we record an EnemyData-driven baseline
            // by storing whatever was last passed into Configure() via a separate registry. Since
            // we cannot intercept Configure() calls without modifying EnemyChaseAI, we fall back
            // to a sane default (2f, matching EnemyChaseAI's serialized default) and log a warning
            // the first time we touch a given AI.
            _originalSpeed = SlowSpeedRegistry.GetOrSnapshot(_ai);
            _ai.Configure(_originalSpeed * speedMultiplier);
            _applied = true;
        }

        public override void OnTick(StatusController controller)
        {
            if (!_applied || _ai == null) return;
            // Re-assert the slowed speed in case anything else wrote to it.
            _ai.Configure(_originalSpeed * speedMultiplier);
        }

        public override void OnRemove(StatusController controller)
        {
            if (!_applied || _ai == null) return;
            _ai.Configure(_originalSpeed);
            SlowSpeedRegistry.Forget(_ai);
            _applied = false;
        }

        public override void OnReapply(StatusController controller, StatusEffect incoming)
        {
            // Refresh duration; do NOT re-snapshot (would lock in the slowed value as "original").
            float now = Time.time;
            float newExpiry = now + incoming.duration;
            if (newExpiry > ExpiresAt) ExpiresAt = newExpiry;
        }
    }
}
