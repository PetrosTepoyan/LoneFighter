using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Bosses.Roster
{
    /// Pairs two MirrorBoss instances (the original and its clone) so that the fight only
    /// ends when both are dead. When the first twin dies, this component enrages the
    /// survivor (+30% contact damage, +20% move speed by default) and forgets the link.
    ///
    /// MirrorLink is created and owned by the spawning <see cref="MirrorBoss"/>; it
    /// subscribes to both twins' <see cref="EnemyHealth.OnDied"/> events and tears itself
    /// down once the pair is fully resolved.
    ///
    /// MirrorLink does NOT "share HP" between the two copies (Unity events on the EnemyHealth
    /// don't lend themselves to a shared float pointer). Instead, the clone is spawned with
    /// half max HP (see MirrorBoss), and the pair fight is balanced around two independent HP
    /// bars. The README documents this trade-off in detail.
    public class MirrorLink : MonoBehaviour
    {
        [Header("Enrage Buff (applied to the survivor)")]
        [Tooltip("Multiplier applied to the EnemyData.contactDamage of the surviving twin when its partner dies.")]
        [SerializeField] private float damageMultiplier = 1.3f;
        [Tooltip("Multiplier applied to move speed (via EnemyChaseAI.Configure) of the survivor.")]
        [SerializeField] private float speedMultiplier = 1.2f;

        private MirrorBoss _twinA;
        private MirrorBoss _twinB;
        private EnemyHealth _healthA;
        private EnemyHealth _healthB;
        private bool _enragedTriggered;

        public bool BothDead { get; private set; }
        public bool PairResolved => BothDead;

        public void Bind(MirrorBoss a, MirrorBoss b)
        {
            _twinA = a;
            _twinB = b;
            _healthA = a != null ? a.GetComponent<EnemyHealth>() : null;
            _healthB = b != null ? b.GetComponent<EnemyHealth>() : null;
            if (_healthA != null) _healthA.OnDied += HandleDeath;
            if (_healthB != null) _healthB.OnDied += HandleDeath;
            if (_twinA != null) _twinA.OnLinkAttached(this);
            if (_twinB != null) _twinB.OnLinkAttached(this);
        }

        private void OnDestroy()
        {
            if (_healthA != null) _healthA.OnDied -= HandleDeath;
            if (_healthB != null) _healthB.OnDied -= HandleDeath;
        }

        private void HandleDeath(EnemyHealth dead)
        {
            if (dead == null) return;
            MirrorBoss survivor = null;

            if (_healthA == dead)
            {
                _healthA = null;
                survivor = _twinB;
                _twinA = null;
            }
            else if (_healthB == dead)
            {
                _healthB = null;
                survivor = _twinA;
                _twinB = null;
            }

            // Both gone? Resolve the pair.
            if (_healthA == null && _healthB == null)
            {
                BothDead = true;
                survivor = null;
            }

            if (survivor != null && !_enragedTriggered)
            {
                _enragedTriggered = true;
                ApplyEnrage(survivor);
            }

            if (BothDead)
            {
                // We're done; clean up.
                if (this != null) Destroy(this);
            }
        }

        private void ApplyEnrage(MirrorBoss survivor)
        {
            if (survivor == null) return;

            // Boost contact damage on the EnemyData. We DO NOT mutate the shared ScriptableObject -
            // we route through MirrorBoss.ApplyEnrageMultipliers so the boss can keep a local copy
            // and restore on disable. This avoids polluting the SO across runs.
            survivor.ApplyEnrageMultipliers(damageMultiplier, speedMultiplier);
        }
    }
}
