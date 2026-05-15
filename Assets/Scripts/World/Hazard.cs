using UnityEngine;
using LoneFighter.Player;
using LoneFighter.Systems;
using LoneFighter.Enemies;

namespace LoneFighter.World
{
    /// <summary>
    /// Abstract base for area-of-effect environmental hazards (poison puddles, lava patches, etc.).
    /// Subclasses only need to differ in collider shape and visuals — all damage cadence lives here.
    /// Requires a trigger Collider2D on the same GameObject.
    ///
    /// Damage cadence: damagePerTick is dealt every tickInterval seconds to anything overlapping.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class Hazard : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("Damage applied per tick to each affected target.")]
        [SerializeField] protected float damagePerTick = 5f;

        [Tooltip("Seconds between damage ticks.")]
        [SerializeField] protected float tickInterval = 0.5f;

        [Tooltip("If true, damages enemies overlapping this hazard.")]
        [SerializeField] protected bool damagesEnemies = false;

        [Tooltip("If true, damages the player overlapping this hazard.")]
        [SerializeField] protected bool damagesPlayer = true;

        [Header("FX")]
        [Tooltip("If true, calls FxService.PlayerHit on each player-damage tick.")]
        [SerializeField] protected bool fxOnPlayerTick = true;

        protected float _nextTickAt;

        protected virtual void OnEnable()
        {
            // Ensure the collider is a trigger so OnTriggerStay2D fires without physics resolution.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            _nextTickAt = Time.time + tickInterval;
        }

        protected virtual void OnTriggerStay2D(Collider2D other)
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
            if (Time.time < _nextTickAt) return;

            bool tickedSomething = false;

            if (damagesPlayer)
            {
                var ph = other.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    ph.ApplyDamage(damagePerTick);
                    if (fxOnPlayerTick && FxService.Instance != null)
                        FxService.Instance.PlayerHit(other.transform.position);
                    tickedSomething = true;
                }
            }

            if (damagesEnemies)
            {
                var eb = other.GetComponentInParent<EnemyBase>();
                if (eb != null)
                {
                    eb.ApplyDamage(damagePerTick);
                    tickedSomething = true;
                }
            }

            if (tickedSomething) _nextTickAt = Time.time + tickInterval;
        }
    }
}
