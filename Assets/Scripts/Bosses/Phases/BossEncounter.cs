using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Bosses
{
    /// <summary>
    /// Top-level component attached to a boss prefab. Wires the boss's <see cref="EnemyHealth"/>
    /// into a <see cref="PhaseManager"/> populated by a per-encounter setup method.
    ///
    /// Integration contract:
    ///   * The same GameObject (or a parent/child) must carry an <see cref="EnemyHealth"/> —
    ///     we resolve it via <see cref="Component.TryGetComponent{T}"/> in <see cref="Awake"/>
    ///     and subscribe to <see cref="EnemyHealth.OnDied"/>.
    ///   * Phase scripts read <see cref="HpFraction"/> via this component; we never reach into
    ///     EnemyHealth directly from phases so subclasses (e.g. boss with shielded HP) can lie.
    ///   * <see cref="BossDeathSequence"/> (if attached) is invoked on death before the boss's
    ///     normal pool-release path runs — see that file's notes on Time.timeScale.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossEncounter : MonoBehaviour
    {
        [Header("Phase Setup")]
        [Tooltip("Which built-in phase set to install. Custom encounters can set this to None and call AddPhase manually from another component before Start.")]
        [SerializeField] private PhaseSet phaseSet = PhaseSet.Warden;

        [Header("Death Sequence")]
        [Tooltip("If null, the BossDeathSequence on this GameObject (or a child) is auto-resolved on Awake.")]
        [SerializeField] private BossDeathSequence deathSequence;

        [Header("Debug")]
        [SerializeField] private bool logTransitions = true;

        public enum PhaseSet
        {
            None,
            Warden,
        }

        public PhaseManager Phases { get; private set; }
        public EnemyHealth Health { get; private set; }
        public bool IsAlive { get; private set; } = true;

        /// <summary>Current HP fraction in [0,1]. Returns 0 if health is missing or already dead.</summary>
        public float HpFraction
        {
            get
            {
                if (Health == null || Health.Max <= 0f) return 0f;
                return Mathf.Clamp01(Health.Current / Health.Max);
            }
        }

        private void Awake()
        {
            Phases = new PhaseManager();

            if (!TryGetComponent(out EnemyHealth health))
            {
                health = GetComponentInChildren<EnemyHealth>();
            }
            Health = health;

            if (deathSequence == null)
            {
                deathSequence = GetComponent<BossDeathSequence>();
                if (deathSequence == null) deathSequence = GetComponentInChildren<BossDeathSequence>();
            }

            // Install the requested phase set. Custom encounters can extend by adding phases
            // after this from another Awake() with a higher script-execution order, or by
            // setting phaseSet = None and pushing phases from elsewhere.
            switch (phaseSet)
            {
                case PhaseSet.Warden:
                    WardenPhases.Install(Phases);
                    break;
                case PhaseSet.None:
                default:
                    break;
            }

            Phases.Finalize();
        }

        private void OnEnable()
        {
            if (Health != null)
            {
                Health.OnDied += HandleDeath;
            }
            IsAlive = true;
        }

        private void OnDisable()
        {
            if (Health != null)
            {
                Health.OnDied -= HandleDeath;
            }
        }

        private void Update()
        {
            if (!IsAlive || Health == null) return;
            float prevIndex = Phases.CurrentIndex;
            Phases.Tick(this, HpFraction);
            if (logTransitions && Phases.CurrentIndex != prevIndex && Phases.Current != null)
            {
                Debug.Log($"[BossEncounter] {name}: entering phase {Phases.CurrentIndex} ({Phases.Current.Name}) @ hp={HpFraction:P0}");
            }
        }

        private void HandleDeath(EnemyHealth _)
        {
            if (!IsAlive) return;
            IsAlive = false;

            // Let the active phase clean up before the death sequence runs.
            Phases.ForceExit(this);

            if (deathSequence != null)
            {
                deathSequence.Play(this);
            }
        }
    }
}
