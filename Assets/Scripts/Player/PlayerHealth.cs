using System;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float invulnerabilityWindow = 0.4f;

        public float Current { get; private set; }
        public float Max => maxHealth;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        private float _lastHitTime = -999f;
        private bool _dead;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(Current, Max);
        }

        public void SetMax(float value, bool refill)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill) Current = maxHealth;
            else Current = Mathf.Min(Current, maxHealth);
            OnHealthChanged?.Invoke(Current, Max);
        }

        public void ApplyDamage(float amount)
        {
            if (_dead) return;
            if (Time.time - _lastHitTime < invulnerabilityWindow) return;
            _lastHitTime = Time.time;
            Current = Mathf.Max(0f, Current - amount);
            OnHealthChanged?.Invoke(Current, Max);

            if (FxService.Instance != null)
            {
                FxService.Instance.PlayerHit(transform.position);
                FxService.Instance.HeavyShake();
            }

            if (Current <= 0f)
            {
                _dead = true;
                OnDied?.Invoke();
                if (FxService.Instance != null) FxService.Instance.HitStop(0.12f);
                if (GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
            }
        }

        public void Heal(float amount)
        {
            if (_dead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnHealthChanged?.Invoke(Current, Max);
        }
    }
}
