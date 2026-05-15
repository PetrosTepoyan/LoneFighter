using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Effects.DeathFx
{
    /// Tiny companion component the dispatcher attaches to every enemy it observes.
    /// Subscribes to `EnemyHealth.OnDied` so the dispatcher can react without modifying
    /// `EnemyBase` or `EnemyHealth`.
    ///
    /// One reporter per enemy GameObject. The dispatcher's polling loop is idempotent —
    /// it only attaches a reporter once even when scanning the registry every frame.
    ///
    /// `OnDisable` unsubscribes so a pooled enemy that's recycled later still raises
    /// `OnDied` correctly. The dispatcher re-attaches on the next poll if the enemy
    /// gets re-registered via `EnemyRegistry.Register`.
    [DisallowMultipleComponent]
    public class DeathFxReporter : MonoBehaviour
    {
        private EnemyHealth _health;
        private EnemyBase _enemy;
        private bool _subscribed;

        // Set by the dispatcher when attaching so the reporter can call back without
        // touching the singleton (which makes it test-injectable and avoids race
        // conditions when the dispatcher is destroyed before its observed enemies).
        internal System.Action<EnemyBase> ReportDeath;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            _health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            if (_health == null) _health = GetComponent<EnemyHealth>();
            if (_health == null) return;
            _health.OnDied += HandleDied;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_health != null) _health.OnDied -= HandleDied;
            _subscribed = false;
        }

        private void HandleDied(EnemyHealth _)
        {
            // Important: EnemyHealth raises OnDied *before* EnemyBase.HandleDeath returns
            // the enemy to the pool, so transform position / data is still valid here.
            if (_enemy == null) _enemy = GetComponent<EnemyBase>();
            ReportDeath?.Invoke(_enemy);
        }
    }
}
