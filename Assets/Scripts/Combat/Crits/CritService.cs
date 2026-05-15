using System;
using UnityEngine;

namespace LoneFighter.Combat.Crits
{
    /// <summary>
    /// Run-time crit roll service. Hosted as a singleton MonoBehaviour so any
    /// system (weapons, upgrade adapters, enemy weakness components) can
    /// consult or modify the player's current crit chance without coupling.
    ///
    /// The service holds a delta on top of the <see cref="CritProfile"/>:
    ///  - <see cref="ChanceBonus"/> is added to the resolved chance for every roll.
    ///  - <see cref="MultiplierBonus"/> is added to the resolved multiplier on crit.
    /// Both reset on <see cref="ResetRun"/> at the start of a new run.
    ///
    /// <para>
    /// Upgrade integration: subscribe to <see cref="OnCritChanceModified"/> from
    /// any external upgrade glue. Each invocation is an additive delta (positive
    /// or negative). See <see cref="CritUpgradeAdapter"/> for the recommended
    /// wiring against the project's UpgradeService.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CritService : MonoBehaviour
    {
        public static CritService Instance { get; private set; }

        [Header("Profile")]
        [Tooltip("Crit profile assigned in-inspector. If left null the service will attempt to " +
                 "load 'CritProfile' from any Resources folder, and finally fall back to a " +
                 "runtime-instantiated default (5% chance, x2 multiplier).")]
        [SerializeField] private CritProfile profile;

        [Header("Run state (read-only)")]
        [SerializeField, Range(-1f, 1f)] private float chanceBonus;
        [SerializeField] private float multiplierBonus;

        /// <summary>
        /// Fired whenever something nudges the player's global crit chance.
        /// The argument is the additive delta (e.g. +0.05 for "+5% crit chance").
        /// Subscribers (typically <see cref="CritService"/> itself) translate it
        /// into a running <see cref="ChanceBonus"/>.
        ///
        /// External upgrade glue should call <see cref="RaiseCritChanceModified"/>
        /// rather than poking the event directly.
        /// </summary>
        public static event Action<float> OnCritChanceModified;

        /// <summary>
        /// Fired whenever something modifies the player's crit multiplier.
        /// </summary>
        public static event Action<float> OnCritMultiplierModified;

        /// <summary>Current additive crit chance bonus from upgrades (clamped 0..1 after baseline).</summary>
        public float ChanceBonus => chanceBonus;

        /// <summary>Current additive crit multiplier bonus from upgrades.</summary>
        public float MultiplierBonus => multiplierBonus;

        /// <summary>The profile asset currently in use. Never null after Awake.</summary>
        public CritProfile Profile => profile;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureProfile();

            OnCritChanceModified += HandleChanceModified;
            OnCritMultiplierModified += HandleMultiplierModified;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OnCritChanceModified -= HandleChanceModified;
            OnCritMultiplierModified -= HandleMultiplierModified;
        }

        private void EnsureProfile()
        {
            if (profile != null) return;
            profile = Resources.Load<CritProfile>("CritProfile");
            if (profile != null) return;
            profile = ScriptableObject.CreateInstance<CritProfile>();
            profile.name = "DefaultCritProfile (runtime)";
        }

        private void HandleChanceModified(float delta)
        {
            chanceBonus = Mathf.Clamp(chanceBonus + delta, -1f, 1f);
        }

        private void HandleMultiplierModified(float delta)
        {
            multiplierBonus += delta;
        }

        /// <summary>
        /// Reset run-scoped state. Call from the same place that resets the run
        /// (e.g. UpgradeService.ResetRun analog at run start).
        /// </summary>
        public void ResetRun()
        {
            chanceBonus = 0f;
            multiplierBonus = 0f;
        }

        /// <summary>
        /// Public emitter so external upgrade glue (e.g. CritUpgradeAdapter or a
        /// hand-rolled extension upgrade) can announce a chance delta without
        /// taking a hard reference to the event field.
        /// </summary>
        public static void RaiseCritChanceModified(float delta)
        {
            OnCritChanceModified?.Invoke(delta);
        }

        /// <summary>
        /// Public emitter for multiplier deltas. See <see cref="RaiseCritChanceModified"/>.
        /// </summary>
        public static void RaiseCritMultiplierModified(float delta)
        {
            OnCritMultiplierModified?.Invoke(delta);
        }

        /// <summary>
        /// Roll for a crit. Returns true if the hit should be a crit, and outputs
        /// the multiplier to apply (always populated; 1.0 on a non-crit so the
        /// caller can multiply unconditionally if they prefer).
        /// </summary>
        /// <param name="weaponNameOrNull">
        /// Weapon identity used to look up per-weapon overrides. May be null/empty.
        /// </param>
        /// <param name="multiplier">Final multiplier (1.0 on miss, &gt;1.0 on crit).</param>
        public bool Roll(string weaponNameOrNull, out float multiplier)
        {
            return Roll(weaponNameOrNull, 0f, out multiplier);
        }

        /// <summary>
        /// Roll for a crit with an additional caller-supplied chance bonus
        /// (used e.g. by <see cref="EnemyCritWeakness"/> for per-target buffs).
        /// </summary>
        public bool Roll(string weaponNameOrNull, float extraChance, out float multiplier)
        {
            if (profile == null) EnsureProfile();

            profile.Resolve(weaponNameOrNull, out var chance, out var mult);
            chance = Mathf.Clamp01(chance + chanceBonus + extraChance);
            mult = Mathf.Max(1f, mult + multiplierBonus);

            if (chance <= 0f) { multiplier = 1f; return false; }
            if (UnityEngine.Random.value <= chance)
            {
                multiplier = mult;
                return true;
            }
            multiplier = 1f;
            return false;
        }
    }
}
