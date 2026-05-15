using UnityEngine;

namespace LoneFighter.Enemies.Elites
{
    /// <summary>
    /// Bridge between the host <see cref="EnemyHealth.OnDied"/> event and an
    /// <see cref="EliteDropTable"/>. Spawns the table's contents at the host's position
    /// the moment the host dies (which fires before <c>EnemyBase.HandleDeath</c> returns
    /// the GameObject to the pool — see <see cref="EnemyHealth.ApplyDamage"/>).
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class EliteDropOnDeath : MonoBehaviour
    {
        [SerializeField] private EliteDropTable dropTable;

        private EnemyHealth _health;
        private bool _subscribed;
        private bool _dropped;

        public void Configure(EliteDropTable table) => dropTable = table;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _dropped = false;
            if (_health == null) _health = GetComponent<EnemyHealth>();
            if (_health != null && !_subscribed)
            {
                _health.OnDied += HandleDied;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_health != null && _subscribed)
            {
                _health.OnDied -= HandleDied;
                _subscribed = false;
            }
        }

        private void HandleDied(EnemyHealth _)
        {
            if (_dropped) return;
            _dropped = true;
            if (dropTable == null) return;
            dropTable.SpawnAt(transform.position);
        }
    }
}
