using System;
using UnityEngine;

namespace LoneFighter.Enemies
{
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyHealth : MonoBehaviour
    {
        public float Current { get; private set; }
        public float Max { get; private set; } = 10f;

        public event Action<EnemyHealth> OnDied;

        private EnemyBase _enemy;
        private bool _dead;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        public void Initialize(float maxHealth)
        {
            Max = Mathf.Max(1f, maxHealth);
            Current = Max;
            _dead = false;
        }

        public void ApplyDamage(float amount)
        {
            if (_dead) return;
            Current -= amount;
            if (Current <= 0f)
            {
                _dead = true;
                OnDied?.Invoke(this);
                _enemy.HandleDeath();
            }
        }
    }
}
