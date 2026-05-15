using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Stacking DoT. Each stack does <see cref="damagePerStackPerTick"/> damage every <see cref="tickInterval"/>
    /// seconds for <see cref="duration"/> seconds. Default: 2 dmg / 0.5s / stack, 5s, up to 5 stacks.
    ///
    /// Stacking semantics (overrides the base "refresh-only" behavior):
    ///   - First application: 1 stack, full duration.
    ///   - Re-application while active: +1 stack (clamped to <see cref="maxStacks"/>) AND refresh duration.
    /// </summary>
    public class PoisonEffect : StatusEffect
    {
        public const string EffectId = "poison";

        public float damagePerStackPerTick = 2f;
        public int maxStacks = 5;

        public int Stacks { get; private set; } = 1;

        public PoisonEffect()
        {
            duration = 5f;
            tickInterval = 0.5f;
        }

        public override string Id => EffectId;
        public override string DisplayLabel => Stacks > 1 ? $"Poison x{Stacks}" : "Poison";
        public override Color IconColor => new Color(0.4f, 0.85f, 0.3f, 1f);

        public override void OnTick(StatusController controller)
        {
            if (controller == null) return;
            var go = controller.gameObject;
            float damage = damagePerStackPerTick * Stacks;

            if (go.TryGetComponent(out EnemyHealth enemyHp))
            {
                enemyHp.ApplyDamage(damage);
                return;
            }

            if (go.TryGetComponent(out PlayerHealth playerHp))
            {
                playerHp.ApplyDamage(damage);
            }
        }

        public override void OnReapply(StatusController controller, StatusEffect incoming)
        {
            // Add a stack (up to max) and refresh duration to the longer of (current remaining, incoming.duration).
            Stacks = Mathf.Min(maxStacks, Stacks + 1);

            float now = Time.time;
            float newExpiry = now + incoming.duration;
            if (newExpiry > ExpiresAt) ExpiresAt = newExpiry;
        }
    }
}
