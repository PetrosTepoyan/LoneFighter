using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Fire DoT. 5 damage per 0.5s tick for 3 seconds.
    /// Stacking rule: refresh duration only — re-applying does not stack damage.
    /// Damage is dispatched via TryGetComponent so the same effect works on both player and enemies.
    /// </summary>
    public class BurnEffect : StatusEffect
    {
        public const string EffectId = "burn";

        public float damagePerTick = 5f;

        public BurnEffect()
        {
            duration = 3f;
            tickInterval = 0.5f;
        }

        public override string Id => EffectId;
        public override string DisplayLabel => "Burn";
        public override Color IconColor => new Color(1f, 0.45f, 0.1f, 1f);

        public override void OnTick(StatusController controller)
        {
            if (controller == null) return;
            var go = controller.gameObject;

            if (go.TryGetComponent(out EnemyHealth enemyHp))
            {
                enemyHp.ApplyDamage(damagePerTick);
                return;
            }

            if (go.TryGetComponent(out PlayerHealth playerHp))
            {
                playerHp.ApplyDamage(damagePerTick);
            }
        }

        // OnReapply: default base behavior (refresh duration). Explicit override for clarity.
        public override void OnReapply(StatusController controller, StatusEffect incoming)
        {
            float now = Time.time;
            ExpiresAt = now + incoming.duration;
            // Re-align next tick so the refreshed burn keeps a consistent cadence.
            if (NextTickTime < now) NextTickTime = now + tickInterval;
        }
    }
}
